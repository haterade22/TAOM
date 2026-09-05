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

---

# Addendum — Phase 1 + spawn_troops review, 2026-08-01

Second `/deep-review` (3 agents: standards, engine API, data flow) over the ten commands built after
the Phase 0 contract. No crash-class defect shipped — the engine agent re-verified every signature
against the installed v1.4.7 DLLs and confirmed `spawn_troops` uses **named arguments** for all nine
bool/int parameters, which is what makes the `isAlarmed`/`wieldInitialWeapons` and
`formationTroopCount`/`formationTroopIndex` positional-swap class impossible.

| # | Sev | Finding | Why missed | Action |
|---|-----|---------|-----------|--------|
| A1 | MED | `AgentSnapshot.Health`/`MaxHealth` defaulted to `0f` on a failed read. Zero is a *plausible* value (a downed agent), so a throwing read renders as `hp=0.0/100.0` — a reader concludes the agent is dead. | The validate-before-lookup rigour was applied to `RaceId` (where the fallback is a *wrong name*) and not generalised to fields whose fallback is a *wrong number*. Same bug class, different data type. | Made both `float?`; the formatter renders `?`. Two tests. |
| A2 | MED | `IsHuman`/`IsMount` were read at the boundary, carried in the snapshot, set in a test — and never consumed by the formatter. The test `FormatAgent_MountItself_RendersItsRider` asserted only `RiderName`, so it passed against a formatter that ignored both flags. | The CrashReport `frames=null` class exactly. A test that *sets* a field is not a test that the field is *used*. | Formatter now renders `kind=`; test asserts it. |
| A3 | MED | `MomentumCheats.BuildPayload` was a COPY of `SyncData`'s serialization, while its own doc comment claimed it was "the same helper". | **This is the Phase 0 RCA's root pattern recurring within one day** — prose asserting a code relationship that was intended rather than verified. | Extracted `WarOfTheRingMomentumBehavior.BuildSavePayload`; both call sites now share it, so drift is impossible rather than merely unlikely. |
| A4 | LOW | The `ally` guard's message could never fire: `GetAgentTeam` falls back to `PlayerTeam` when `PlayerAllyTeam` is null, so `spawn_troops X N ally` in a town spawns onto the player's own team rather than refusing. | Inferred both branches from the enemy branch's shape instead of reading `GetAgentTeam`'s body. | Comment and message corrected to name the enemy case, which is the only one that returns null. |
| A5 | LOW | `AgentDiagnosticCheats.cs` was 160 lines, over ADR-002's 150. | Not checked before committing. | Boundary conversion extracted to `AgentSnapshotBuilder`; the cheat is now 99 lines. `MissionSpawnCheats` was 152 and is now 132. |
| A6 | LOW | The equipment block reads `SpawnEquipment` (build-time) but was labelled just "equipment", so a mid-fight reader would take a stale loadout for the live one. | — | Labelled "(at spawn)". |
| A7 | LOW | `docs/features/dev-console.md` still listed three shipped commands as unbuilt and had no rows for four others; `spawn_` was not in the documented mutating-verb list, and the binding test only *denies* prefixes rather than allow-listing verbs, so nothing caught the drift. | The doc was written before the commands and not re-read after. | Table and verb list corrected. |
| A8 | LOW | No test covered `Spawned == 0` with no `FailureReason` (every spawn threw). The code handles it correctly; the branch was unpinned. | — | Test added. |

## Root-cause pattern: the Phase 0 lesson recurred in one day (A3)

The Phase 0 RCA's finding was *prose asserting engine or code facts inferred from shape rather than
read from source*. A3 is the same failure applied to TAOM's own code: a comment claiming two call
sites shared a helper when they were independent copies. The lesson had been written down; writing it
down did not prevent the recurrence a day later.

What actually caught it was a review brief that named the specific question — *"is this a shared call
or a copy, and can they drift?"* — rather than a general instruction to check comments. **The
durable fix is not another rule; it is that any comment claiming two things are "the same" must be
made true structurally (extract the shared thing) rather than asserted.** A3's fix does that: after
the change the claim cannot be false, because there is only one expression.

Appended to `docs/reviews/lessons/adapters-taleworlds-api.md` as a scope extension of the existing
shipping-build lesson.

---

# Addendum 2 — damage_agent + requeue_settlement review, 2026-08-01

Three agents over the full twelve-command feature. No HIGH findings; the engine agent walked
`GetAttackCollisionDataForDebugPurpose`'s 37 arguments one by one against the installed v1.4.7
signature and found the positional mapping exact, including a `DamageType=2` literal that is
deliberately consistent with `DamageTypes.Blunt`.

| # | Sev | Finding | Why missed | Action |
|---|-----|---------|-----------|--------|
| B1 | MED | `AgentDamageCheats` omitted `GameNetwork.IsClientOrReplay` from its mode guard while the class comment claimed to model `KillAgentCheat` "including its mission-mode guard". `IsReplay` covers `MBCommon.GameType.SingleReplay` — reachable in singleplayer, not multiplayer-only. | Read the three `Mode` checks in the engine's guard and stopped there, then wrote a comment asserting full parity. **Third instance of this pattern in this feature.** | Full guard ported; comment now enumerates what it does and does not replicate. |
| B2 | MED | `(int)amount` in the blow had no upper bound. `TryParseAmount` rejects non-finite input but not a large finite float, and the cast is unchecked — `BaseMagnitude` would stay huge while `InflictedDamage` went degenerate/negative, reading downstream as healing. | The float→int cast class is documented in `csharp-architecture.md` with five prior instances; the guard was written as a sign check (`amount <= 0f`) without asking what the cast produces at the top of the range. | `MaxDamage = 100_000f` bound, gate written as a positive requirement. |
| B3 | MED | `SettlementEconomyCheats` re-derived the equilibrium target from raw `town.Prosperity` while `ComputeTownGoldChange` sanitizes non-finite prosperity to 0 internally. A corrupt save would print a finite `dailyChange` beside `target=NaN` in the same block. | The duplication was noticed in the previous round and judged low-risk; nobody asked what the *service* does to its input before using it. Duplicating a formula also duplicates the obligation to sanitize. | Cheat now sanitizes identically. The formula is still duplicated — a `ComputeEquilibriumTarget` accessor on the service remains the better fix and is not done. |
| B4 | MED | `requeue_settlement` was documented Tier B "idempotent by the very guard it tests". True for a tracked settlement; **false for a cross-culture-owned settlement with no store record** — there the first internal fire calls `StartPending` + `Put`, arming a real persisted timer that a later daily tick completes into an actual culture flip. Tier C behaviour from a command sold as a read-mostly guard. | Reasoned about the guard's behaviour in the state the command was designed for and never enumerated the state where the record is absent. | The command now refuses when no record exists and says why — it verifies a timer, it does not create one. |
| B5 | LOW | Two of `FormatRequeue`'s four branches (RESTARTED, second-fire-only) are unreachable from the live command: both fires are synchronous, so the owner culture is identical and the guard closes after whichever fires first. The test docstring implied all four were live outcomes. | — | Docstring states the reachability explicitly. The branches stay — they exist to catch a future service regression. |
| B6 | LOW | The command table listed `damage_agent` and `requeue_settlement` as "Phase 2 remaining" while both were implemented; `damage_`/`requeue_` were absent from the documented verb list. | **Second time this table has gone stale**, same cause both times: written alongside the code instead of re-read against it. | Table and verb list corrected, plus an inline note requiring the table be edited in the same commit as a new command. |

## The pattern that will not die (B1)

Three rounds, three instances of a comment asserting an engine or code relationship that was
*intended* rather than *read*: the `Game.CheatMode` mechanism (Phase 0), the momentum "shared helper"
claim (addendum 1), and now "including its mission-mode guard" when only three of four clauses were
ported. Writing the lesson down twice did not stop the third.

What has actually caught all three is a review brief naming the specific question — "is this claim
true?" — rather than a general instruction to check comments. The structural fix used in addendum 1
(extract the shared thing so the claim cannot be false) does not apply to a claim about *vanilla's*
code, which cannot be shared with. For that case the only reliable discipline is: **when a comment
says "including X", enumerate X and check each element**, which is what the engine agent did and what
the author did not.

## Not fixed, recorded

- `SettlementEconomyCheats` still duplicates the target formula rather than calling a service
  accessor (B3 fixed the divergence, not the duplication).
- `AgentDamageCheats.ApplyBlow` has no unit coverage — sealed engine types, no adapter seam. Its
  correctness rests on this review's argument-by-argument decompile diff, not on a regression test.
  Extracting a pure `ComputeInflictedDamage(float)` would close the cast half cheaply.

---

<!-- backlinks-start auto-generated; edit lint_docs.py / build_backlinks.py to change -->

## Referenced by

- [docs/reviews/REVIEW-LOG.md](./REVIEW-LOG.md)

<!-- backlinks-end -->
