# RCA: FiefGranting deep review (#458), 2026-08-14

Seven independent reviews on the FiefGranting feature: six parallel Claude agents (standards, API
compatibility, efficiency, completeness, cross-system data flow, tooling correctness) plus a Codex
adversarial pass. **Sixteen findings across both, three refuted.** The Claude agents found eight (none
a functional defect in the election logic); Codex found eight more including one P1 and, more
importantly, **refuted the feature's central design claim**. See the appended Codex section.

Most load-bearing claims survived attempted refutation: the master toggle folds on every override, the
MCM clamp ranges match the attribute ranges knob-for-knob, the NaN polarity is a positive requirement
everywhere, the merit denominator is provably `>= 1f`, the 0/0 degenerate case short-circuits before
the division, and no engine path keys on the concrete decision type. The one that did NOT survive is
"merit alone decides the winner", which was the premise the whole design was argued from.

## Findings

| # | Sev | Finding | Category | Why missed | Preventive action |
|---|---|---|---|---|---|
| 1 | HIGH | `tools/apply_starting_fief_spread.py` used `read_text`/`write_text` on a BOM'd, CRLF live game file instead of the mandatory byte-level idiom. No damage occurred, but only by codec coincidence. | Tooling / data safety | The convention lives in `tools/README.md` and `.claude/rules/moduledata-validation.md`, which IS correctly path-scoped to `tools/**/*.py`. Either the scoped rule did not fire on a `Write` creating a new file, or it fired and was not applied. | Lesson entry (below). Empirical harness note: rule scoping may not fire on new-file `Write`; verify before relying on a scoped rule for a file you are creating. |
| 2 | MED | `Patch70._faultReported` was a bare `static bool`: a process-lifetime latch, so a fault in a second campaign in the same session was silently swallowed. Also latched BEFORE the log call, so a throwing logger would lose the only report. | State / lifecycle | Fourth instance of "once per process where once per session was meant". The exact pattern is already written down in `docs/reviews/lessons/state-lifecycle-save.md`, including the "latch after the successful pass" half. | Keyed on `Campaign.Current` identity, latched after the log succeeds. Lesson entry strengthened for repeat offence. |
| 3 | MED | Both `TaomSettlementClaimantDecision.Policy` and `Patch70.Resolve` re-entered `IoC.Resolve` on every call after a failure, because the cache field stayed null. `CalculateMeritOfOutcome` runs 3N times per election, so a missing registration meant 3N thrown exceptions per election, silently. | Correctness / perf | Wrote the standard `??=`-style lazy cache without asking what happens when the resolve *fails* rather than succeeds. The lazy-cache idiom in CLAUDE.md is written for the success path. | Separate "attempted" flag from "succeeded". Lesson entry. |
| 4 | LOW | `TaomSettlementClaimantDecision.cs` was 157 lines against ADR-002's 150 ceiling. | Standards | Not measured until an agent measured it. | Fixed by extracting `FiefGrantFactsBuilder` (106 lines now). Note the standards agent's proposed fix (move counting into the service) would have broken ADR-007; see "Disagreements". |
| 5 | LOW | Patch70's doc comment, the harmony registry row, and the feature doc all said "both producers" of `SettlementClaimantDecision`. There are **three**: `KingdomManager.RelinquishSettlementOwnership` was missed. | Documentation / evidence | Found the two producers by tracing from the capture path rather than by exhaustively grepping `new SettlementClaimantDecision(`. Stated a count that had not been proven. Direct violation of the Never Fabricate rule's "counts, IDs, names" clause. | Lesson entry: enumerate producers by grepping the constructor, not by tracing one path. |
| 6 | LOW | Vanilla's private `_capturerHero` is dropped by the swap. Inert today (never read in v1.4.8) but it IS in the save graph. | State-carry | The state-carry audit checked what the swap *copies*, not what the original *holds*. | Documented in the feature doc as an engine-bump re-check. |
| 7 | LOW | `SaveDefinerCollisionDetector.cs` comment still said "next free id is 726900901" after FiefGranting claimed it. | Documentation | Claimed the id without grepping for who else documented it. | Corrected, and the comment now points at its own first sentence warning against hardcoded copies. |
| 8 | INFO | Co-op: `DailyTickSettlementEvent` is client-blocked, but `HourlyTickEvent` (which reaches producer 2) is client-live, so a client genuinely can run vanilla scoring locally while the host runs TAOM scoring. The safety argument rests on AutoSync overwriting the client's result, which was **not** independently verified. | Co-op integration | The co-op gate was copied from `TaomKingdomDecisionPermissionModel` without tracing which producers are actually reachable on a client. | Recorded as an open verification item in the feature doc; `/research` follow-up on AutoSync coverage of `Settlement.OwnerClan` / `Kingdom._unresolvedDecisions`. |

## Disagreements worth recording

Two agent findings were **refuted** after independent checking, and both are worth keeping because
accepting either would have made the code worse:

1. **Efficiency agent, Issue 4:** claimed `_logger?.LogWarning($"...")` allocates the interpolated
   string even when `_logger` is null. It does not. The null-conditional operator short-circuits the
   entire invocation including argument evaluation. Acting on this would have added a redundant
   `if (_logger != null)` wrapper for nothing.
2. **Tooling agent, LOW:** called the docstring's "607 villages" figure stale, giving 767 as the real
   number. 767 is the count of *ownerless settlements*, which includes 159 hideouts. Re-derived:
   `total=988, villages=607, hideouts=159, fortifications=221, other=1`. The docstring was correct.

A third finding was **partially refuted**: the standards agent flagged the two counting loops as
ADR-002 violations and recommended extracting them "to a service". Extracting them to a *service*
would require passing `Clan` and `Settlement` across the service boundary, which ADR-007 forbids
outright. The finding was real (the class was over the line ceiling) but the prescribed fix broke a
stronger rule. Resolved by extracting to a **boundary builder** instead, which satisfies both.

## Root-cause pattern

Two of the three substantive findings (#1, #2) share a shape: **the C# feature work was held to the
project's rules, and the surrounding infrastructure was not.** The election logic itself was written
against the loaded rules and survived seven reviews with no functional defect. The defects were in a
Python tool that writes live game data, and in a static latch in a patch class. Both are places where
the work felt like plumbing rather than feature logic, and the plumbing got less scrutiny than the
thing it plumbed.

Finding #5 is a different shape and the more uncomfortable one: a count asserted from a partial trace
and then repeated into three artifacts. The Never Fabricate rule names this exactly ("counts, IDs,
names ... state them only from evidence you have actually read THIS turn"). Tracing forward from the
capture path found two producers; grepping `new SettlementClaimantDecision(` would have found three
in one command. The cheap exhaustive check was skipped in favour of the narrative one.

## Why each agent missed what it missed

- **Standards** caught the line count and the service-locator shape but proposed an ADR-007-breaking
  fix, because its prompt asks whether logic sits in an entry point, not where the logic could
  legally move to.
- **API compatibility** found the third producer, as a side effect of the exhaustive `GetType()`
  sweep it was asked to run. It would not have found it from its own checklist.
- **Efficiency** found the resolve-retry (#3) and the latch (#2) while costing the hot path, then
  produced one false positive on operator semantics.
- **Completeness** passed everything, correctly. It verified the MCM/clamp parity that would have
  been the most likely place for a real defect.
- **Data flow** found the latch independently and with the sharper diagnosis (latch-before-log), plus
  the co-op producer-reachability nuance (#8) that no other agent approached.
- **Tooling** found #1, which no C#-centric agent could have. This is the second time a dedicated
  tooling agent has caught a data-safety bug the core five structurally cannot see.

## Lessons to codify

Appended to `docs/reviews/lessons/build-tooling-workflow.md` and
`docs/reviews/lessons/state-lifecycle-save.md`:

1. **A tool that writes outside the repo gets the ModuleData I/O convention, even when the feature is
   C#.** Path-scoped rules key on repo paths; a script whose *target* is the live game install still
   needs the byte-level BOM/newline idiom, and the rule may not fire when you `Write` the file rather
   than `Read` it.
2. **Separate "resolve attempted" from "resolve succeeded" in every lazy service cache.** The `??=`
   idiom silently becomes a per-call exception when the resolve fails rather than returns null.
3. **Enumerate producers by grepping the constructor, not by tracing one path.** A count in a doc is a
   claim; `grep -rn "new <Type>("` is the proof, and it is one command.

## Verification after fixes

- `./build.ps1 -RunTests`: build succeeded, **6646 passed, 0 failed**, 2 pre-existing skips.
- `tools/apply_starting_fief_spread.py`: check mode exits 0, and the new byte-level round-trip was
  proven byte-identical against the real 1,153,215-byte live file (`had_bom: True`, length unchanged).
- Live map file independently verified undamaged by the earlier `--apply`: same length, BOM intact,
  15,472 CRLF pairs with zero bare LF, exactly 10 differing bytes matching the 10 assignments.
- `TaomSettlementClaimantDecision.cs` 157 → 106 lines; `Patch70` 149 lines. Both under the ceiling.

## Open

- In-game smoke tests. Still the only evidence that settles whether the campaign plays correctly.
- Co-op AutoSync verification (#8).
- Codex adversarial pass was still running when this was written; findings will be appended.

## Codex adversarial pass (appended after the Claude agents)

Codex returned **8 findings: 1 P1, 4 P2, 3 P3**, and refuted the feature's central design claim. It
was explicitly prompted to attack five stated claims; four survived, one did not.

| # | Sev | Finding | Disposition |
|---|---|---|---|
| C1 | P1 | `apply_starting_fief_spread.py` wrote the live file non-atomically. A kill or full disk midway leaves Bannerlord with partial XML and TAOM_Map failing to load. | **Fixed**: temp file + `flush` + `fsync` + `os.replace`. |
| C2 | P2 | **Merit does not exclusively decide the winner.** Support costs 20/60/100 influence, `DetermineSupportOption` downgrades an unaffordable vote, and points are 1/2/3, so a top-merit finalist with 59 influence casts 1 point and loses to two poorer finalists casting 3 each. Non-finalists vote too. | **Claim retracted**; docs and CHANGELOG corrected. Behavioural options deferred to #460. |
| C3 | P2 | The King's Vote cap binds AI rulers only: `OnPlayerSupport` assigns the player ruler's choice without reading `IsKingsVoteAllowed`. | **Documented**; deferred to #460. |
| C4 | P2 | Enablement is captured when the decision is CREATED, so toggling the feature does not retrofit a pending election, and a pre-feature save's pending decision runs vanilla once. Contradicted the "no reload" claim. | **Docs corrected** in three places. |
| C5 | P2 | Player exemption skipped the whole ruling-clan term, so an exempt player ruler lost a `RulingClanFactor` set ABOVE 1.0, which is a bonus. | **Fixed**, plus two regression tests. |
| C6 | P3 | Partial service resolution: `_policy` was assigned before `_coop` resolved, so a coop-provider failure left TAOM scoring active with the deferral gate absent. Also latched before resolving. | **Fixed**: resolve into locals, commit both or neither, no latch on failure, report once. |
| C7 | P3 | Two tests named "outranks" compare multipliers, never composed with vanilla merit. | **Fixed**: renamed and documented what they actually pin. |
| C8 | P3 | Producer count still said "two"/"both"/"neither" in `SubModule.cs`, the registry and the feature doc. | **Fixed** in all locations. |

**Survived refutation:** concrete-type safety (no `GetType()` consumer anywhere in Campaign, UI,
SandBox, StoryMode, shipping or editor decompiles), the `IsEnforced` ordering, producer reference
retention, and the save definer's id (checked against every `Main/` definer and 521 vanilla
definitions). Codex also independently confirmed the default weights materially oppose hoarding: a
six-fief culture-matched tier-6 ruler lands at 87.1 against a landless culture-matched tier-2 clan's
270.

### What C2 says about the process

C2 is the finding worth keeping. The claim that merit alone decides the winner was reached by reading
`DetermineSupportOption` and noticing the influence-downgrade loop, then reasoning past it in one
sentence ("the finalists are high-merit clans, so they can afford it"). The engine code that refutes
it had already been read. It was an inference layered on top of correctly-read source and never
checked against a concrete case, which is why no amount of re-reading the same file would have caught
it. What caught it was an adversary asked to break a specific named claim.

**Lesson, appended to `docs/reviews/lessons/campaign-mechanics.md`:** when a design rests on "these
values always tie" or "this branch is always taken", write the arithmetic for the case where it is
not, before building on it. Name the threshold and the input that crosses it.

---

<!-- backlinks-start auto-generated; edit lint_docs.py / build_backlinks.py to change -->

## Referenced by

- [docs/reviews/REVIEW-LOG.md](./REVIEW-LOG.md)

<!-- backlinks-end -->
