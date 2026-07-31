# RCA — DevConsole Phase 0 (shared console-command contract), 2026-07-31

**Scope reviewed:** `Main/Features/DevConsole/{TaomConsole,DevConsoleGuard,DevConsoleArgs,DevConsoleDiscoveryAudit}.cs`,
`Main/SubModule.cs` (audit call site), `Main/Features/SpecialResources/Cheats/SpecialResourceCheats.cs`
(migrated onto the shell), `TAOM.Tests/Features/DevConsole/*`, `docs/features/dev-console.md`.

**Review:** `/deep-review`, 5 agents. Standards PASS, API compatibility PASS (8/8 verified against the
installed v1.4.7 DLLs, 0 incompatible), Completeness COMPLETE, Efficiency 1 MED + 3 LOW, Data Flow
2 gaps + 3 inconsistencies.

**Top line:** no functional bug shipped in the C#. Every finding of substance was a **claim stated
with more confidence than the evidence supported** — five of the nine sit in comments and the feature
doc, not in code. That matters more here than it normally would, because this changeset's deliverable
*is* a contract document that ~20 future commands will be authored against. A wrong sentence in
`dev-console.md` is a defect with the same blast radius as a wrong line in `TaomConsole.cs`.

---

## Findings

| # | Sev | Bug | Category | Why missed | Preventive action |
|---|-----|-----|----------|-----------|-------------------|
| 1 | MED | `dev-console.md` listed `mission.list_agent_ids` as an existing vanilla command that must not be reimplemented. It exists **only** in `_editor_build`; the shipping client has **zero** occurrences. | Engine research | The session's opening inventory grep ran across the whole `E:\Decompiled_Bannerlord\` tree, which contains a dual `{_shipping_build,_editor_build}` pair. CLAUDE.md warns about the *strip* direction ("absent from the dump != doesn't exist") but says nothing about the *inverse*. | New lesson in `lessons/adapters-taleworlds-api.md`; explicit warning added to `dev-console.md`. |
| 2 | LOW | Same doc claimed "6 `mission.*` cheats"; the shipping build has 10. | Engine research | Same contaminated grep, plus the count was written from a partial reading rather than a counted one. | Covered by the same lesson. Counts in docs must come from a `wc -l`-style command run that turn. |
| 3 | MED | `TaomConsole.Run` null-coalesced `body`'s result (`?? string.Empty`) but returned `usage` raw on the help branch. A command authored with `usage: null` returns `null` across the native reverse-P/Invoke boundary. | Defensive-completeness | The class was written around "the *body* is the dangerous part". The help branch was treated as trivially safe because `usage` is a `const` at every current call site — true today, and exactly the assumption 20 future authors get to break. | Fixed (`usage ?? string.Empty`). Rule: in a shell whose contract is *nothing escapes*, every return path is defended identically — "this one can't be null today" is not a reason. |
| 4 | LOW | `DevConsoleArgs.TryParseSide` had zero production callers — written for `spawn_troops`, which the plan defers to Phase 2. | YAGNI | Misapplied the "pin shared sub-problems once before parallel dispatch" rule from `harness-facts.md`. That rule targets sub-problems appearing in **≥2** builder briefs; side-parsing appears in exactly one command. | Deleted (parser + 5 tests). `dev-console.md` now states that per-command parsers land with their command. |
| 5 | LOW | `dev-console.md` said all three gates answer `CampaignNotStarted` "when `Game.Current` is null". `RunInCampaign`'s gate never reads `Game.Current` — vanilla's `CheckCheatUsage` keys off `Campaign.Current`. | Over-unified explanation | Wrote one tidy sentence covering three gates that agree *in practice at the main menu* but do not check the same thing. Tidiness beat accuracy. | Doc now states the asymmetry and what happens in the divergent state. |
| 6 | LOW | `DevConsoleGuard`/`TaomConsole` comments justified the try-around-the-gate with "Game.CheatMode reads through GameManager, so the gate can throw." Agent 2 traced `Game.GameManager` → `MBGameManager.CheatMode` → `NativeConfig.CheatMode` and showed `GameManager` is never null while a `Game` exists. | Plausible-mechanism-as-fact | Named a failure mechanism from the shape of the call chain instead of reading it to the bottom. The *conclusion* (guard the gate) was right; the stated *reason* was invented. This is `evidence-over-claims.md` §C in miniature. | Comment rewritten to the real mechanism: vanilla's `CheckCheatUsage` dereferences `Game.Current.CheatMode` unguarded after its `Campaign.Current` check. |
| 7 | LOW–MED | A build declaring zero commands never reaches a conclusive verdict, so `Assembly.GetTypes()` + full method scan re-ran on every return to the main menu, unbounded. | Observation state machine | Designed `_settled` around the two expected outcomes (proven / too-early) and did not walk the degenerate third. | Fixed — the attributed-command set is cached for the assembly lifetime. |
| 8 | LOW | The inconclusive verdict logged "Will re-check", implying imminence. The only re-trigger is a genuine main-menu return, which an uninterrupted session never produces. | Log-message overpromise | Wrote the message from the call site's *name* rather than from when the engine actually invokes it. | Message now states the real trigger and its limitation. |
| 9 | LOW | `_settled` is an unsynchronised mutable static with no note on why that is safe. | Undocumented assumption | Single-threadedness was assumed silently. | Comment added naming the assumption and the benign worst case. |

Also noted and deliberately **not** changed: the binding tests re-run the assembly scan once per test
method (test-only, ~ms); `Exception.Message` is unguarded inside `Report` (no reachable exception type
whose `Message` getter throws); gate-before-help ordering (Agent 5 grepped 100+ vanilla command bodies
and confirmed TAOM matches the engine's own convention exactly — not a divergence).

---

## Root-cause pattern: confident prose outrunning read evidence

Findings 1, 2, 5, 6 and 8 are one failure wearing five hats. Each is a sentence — in a doc or a code
comment — asserting something about the engine or the code that was **inferred from shape** rather
than **read from source**, and each was written in the same pass as the code it describes rather than
after verifying it.

`.claude/rules/evidence-over-claims.md` §C already names this exactly, including the mechanical trap:
*"Writing the findings artifact before its evidence is in hand."* The rule's worked example is a
CHANGELOG authored before the proving `diff` was read. What this review adds is that **the same trap
applies to explanatory prose in a feature doc and to `///` comments justifying a design choice** — not
just to counts, hashes and file lists. A comment that says "we guard here because X can throw" is a
factual claim about the engine with the same evidentiary burden as "47 broken refs".

The blast radius is what makes it worth a lesson rather than a shrug: this doc is the authoring
contract for ~20 commands. Finding 1 would have sent a future author looking for a vanilla
agent-listing command that does not exist in the shipping build, and finding 5 would have taught them
the three gates are interchangeable when they are not.

## Why each agent missed these (and which caught them)

- **Agent 1 (Standards, haiku)** — correctly PASSED. Its rule set is ADR compliance; none of these
  are ADR violations. It did do the one thing asked of it that required judgment (ruling on the
  `try { IoC.Resolve } catch` question) and reasoned it correctly against the boundary-class carve-out.
- **Agent 2 (API compatibility, sonnet)** — **caught finding 6** and re-verified all 8 signatures
  against the installed DLLs, confirming the dump had not drifted. Its value here was going one level
  deeper than the changeset needed (`GameManagerBase` → `MBGameManager` → `NativeConfig`) rather than
  stopping at "the property exists."
- **Agent 3 (Efficiency, haiku)** — **caught finding 7** and correctly rated everything else LOW
  rather than inflating severity on a non-hot path, which is the failure mode this agent usually has.
- **Agent 4 (Completeness, haiku)** — reported COMPLETE, and its doc-accuracy check passed. It
  verified doc claims about *TAOM's own code* (which were accurate) but did not verify doc claims
  about the *engine* — the class all five prose findings fall into. Its brief asked it to check the
  doc against "the code", and it read that as TAOM's code.
- **Agent 5 (Data Flow, sonnet)** — **caught findings 1, 2, 3, 4, 5 and 8.** The highest-value agent
  again, and specifically because its brief told it to spot-check doc claims *against the decompile*
  and it grepped `_shipping_build` separately from the tree root. That single methodological choice is
  what surfaced the editor-build contamination.

The gap worth naming: **four of five prose findings landed on one agent.** Agent 4 owns "does the doc
match the code" and Agent 5 owns cross-system tracing; engine-fact verification in docs fell between
them and was only caught because Agent 5's brief happened to ask for it. Agent 4's standing prompt
should treat engine claims in a feature doc as in-scope.

## Lessons codified

- `docs/reviews/lessons/adapters-taleworlds-api.md` — grep the shipping build, not the dump root, when
  deciding what vanilla ships.
- `docs/reviews/lessons/testing-qa.md` — no per-turn RED-proof gap here; the five binding guards and
  four audit verdicts were each verified RED by defect injection before acceptance, which is why this
  RCA has no "guard never seen failing" entry.

No new always-on rule file. `evidence-over-claims.md` §C already covers the root pattern; what it
lacked was the explicit statement that *explanatory prose and code comments* are covered by it, and
that belongs as a scope clarification in the lesson, not a new rule competing with an existing one.
