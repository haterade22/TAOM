# RCA: plan 030, mission diagnostics diet (2026-10-02)

## Top-line

Branch `perf/030-mission-diagnostics-diet`, diff `0d1e91f0..494eec47`, reviewed by six `/deep-review`
lenses (standards, engine, efficiency, completeness, data flow, design). Codex was not run in the first pass; its
2026-10-03 review is under "Codex round" below. **No gameplay
defect was found**: every cost cut keeps the log's information (the census lines and their first agents,
the first hit of each career combination in full, the troll and creature diagnostic lines byte for byte),
and the deleted colour store, its interface, its cleanup behavior and the EquipItems prefix have no
remaining reader. Eleven findings were confirmed, one MEDIUM and the rest LOW or NIT, all in the new
diagnostic lines, their aggregates and the prose around them. All are fixed on the branch except the
`494eec47` commit body's "no log line changes", which cannot be edited without a rewrite and is corrected
in the follow-up commit's body (report: `docs/reviews/deep-review-030-mission-diagnostics-diet-2026-10-02.md`).
The shared root: **the plan's D6 rule asks every cost cut to keep its information and every skip to say
so, and the new "nothing happened" and aggregate lines were checked for presence, not for truth in every
mission the code can produce.**

## Findings + Root Cause Table

| # | Sev | Bug | Category | Why Missed | Preventive Action |
|---|---|---|---|---|---|
| R1 | MEDIUM | `CreatureBanditDiag.NoteMissionEnd` wrote "no creature registered ... and no diag line was written" whenever no creature registered, but a declined creature troop (`Patch93_CreatureBandits` to `NoteDeclined`) writes `spawn-declined` lines and makes the summary print (`SpawnsDeclined != 0`) without registering one, so the log ended with the summary followed by a line denying it | Logic error (diagnostic truth) | The line's condition was written from the callbacks' gate (`AnyRegistered`), not from the summary it complements; the test pinned only the empty mission | Condition is now the exact complement of `WriteSummary`'s (`AnyRegistered`, `SpawnsAttempted`, `SpawnsDeclined`), the absolute clause dropped; `NoteMissionEnd_ACreatureTroopDeclined_LeavesTheMissionToTheSummary` and `_ASpawnAttempted_` red first. Lesson in `lessons/misc.md` |
| R2 | LOW | The same line and its docs said "every engine callback" exits on one field read; only the seven agent callbacks do (`OnMissionTick`, `OnMissionResultReady` and `OnRemoveBehavior` do more), and the doc said "battle" for a line every mission writes | Inaccurate claim | Written as a summary of the gate, not checked per callback | Reworded to "agent callback" and "mission" in code, behavior comment and `creature-bandits.md` |
| R3 | LOW | `MissionDiagnosticBehavior` closed the census window on `_actionSetWindowSecondsLeft <= 0f`; a NaN `dt` makes the remaining time NaN, which fails that test, the window gate (`> 0f`) and the end-of-mission check, so the "census open" header never got its "closed" line | Engine-float gate polarity (repeat) | `csharp-architecture.md` "Engine-Float Decision Gates" names the rule; the new close check was written as the natural "ran out" test beside two positive checks | `if (!(_actionSetWindowSecondsLeft > 0f))`; `MissionDiagnosticBehaviorTests.OnMissionTick_NaNFrameTime_ClosesTheCensusOnce` red first. Recurrence lesson in `lessons/gamemodels-services.md` |
| R4 | LOW | The career hit tally added every hit's `baseResult` and `result` to `BaseSum`/`ResultSum` unchecked, so one non-finite hit turned the combination's `damage X -> Y` into NaN for the mission, and a NaN base with a finite multiplier was not even counted in `nonFinite` | Aggregate poisoning | Only the multiplier was finiteness-checked; the test locked `damage 50.0 -> NaN` in as expected output | A hit with any non-finite number is counted in `nonFinite` and kept out of every sum; `damage n/a` when no hit was finite; `ResetDiagnostics_NonFiniteDamage_CountsTheHitWithoutPoisoningTheTotals` red first. Lesson in `lessons/misc.md` |
| R5 | LOW | The career aggregate is written only at mission teardown (`OnEndMission`), so a crash or process kill mid-battle loses the counts of every hit after each combination's first; the old per-hit DEBUG lines reached disk within about 50 ms (`FileLogger.ProcessQueue`). The `8ebdbbcc` body says "No hit is dropped from the record" | Durability claim | D6 rule 4 was read as "an aggregate exists", not "an aggregate that survives the failures the log is for" | Limitation documented in `career-system.md` and the field comment; a second, non-clearing write at the battle result is a design choice left to Mike (`CareerPerkMissionBehavior` is `BehaviorType.Other` and never receives `OnMissionResultReady`). Same `misc.md` lesson |
| R6 | LOW | `career-system.md` and the tally's comment said ally-buff-only troops share the null subject; the key is `victimHeroId ?? troopLeaderHeroId`, so a troop with a party leader is keyed and labelled by the leader, and only leaderless victims land under `(ally-buffed agents)` | Inaccurate claim | The comment described the agent-index problem, not the key expression beside it | Comment and both doc passages corrected; `CalculateDamageReduction_FirstHit_WritesTheUnchangedDebugLine` pins a leader-keyed ally-buff line |
| R7 | LOW | `Agent_EquipItemsFromSpawnEquipment_BattleLoad_Patch.cs:14` and `battle-load-diagnostics.md:46` still said Patch23 has a prefix on `EquipItemsFromSpawnEquipment` | Stale doc after deletion (repeat) | The plan's sweep grepped the deleted class names; these lines name the category and the method | Comment line deleted; doc sentence now names only `Patch16_AtmospherePersistence`. Recurrence lesson in `lessons/misc.md` |
| R8 | LOW | `companion-tactics.md` still said `FormationPresets/Hooks/` holds 5 Harmony patches; 4 remain | Stale count after deletion | The plan's doc edits removed the deleted class's rows, not the count that included it | Count corrected. Same recurrence lesson |
| R9 | LOW | The `494eec47` body says "no log line changes"; with BattleLoad diagnostics on, each mission loses one `[BattleLoad] TaomBehaviorAdded behavior='AgentColorStoreCleanupBehavior'` line and `TaomBehaviorsDone count=` drops by one (`BattleLoadDiagnosticsService.cs:446-456`) | Inaccurate commit claim | The claim covered the deleted members' own logging, not the behavior list another feature logs | Recorded in the follow-up commit body; the old commit is not rewritten |
| R10 | NIT | `IMissionDiagnosticService.ResetForNewMission`'s comment said it resets "both action-set dedup sets"; it also zeroes the census count | Stale comment | Comment predates the census totals | Comment corrected |
| R11 | LOW | Test gaps: the first-hit career DEBUG lines were checked only with `Contains`, the per-creature line cap's silent refusal had no test, and the declined-only mission had none | Test coverage | The plan's tests pinned the new lines literally and the unchanged ones loosely | Literal pins for both first-hit lines (green on the plan's code, a characterisation), `TakeLine_PastThePerCreatureCap_RefusesWithoutAWarning`, and R1's tests |

## Root-cause pattern

R1, R4 and R5 share one cause. D6 asked for two kinds of new line: a reason line when a skip leaves the
log silent, and an aggregate when per-event lines are cut. Each was tested on the case it was written for
(an empty mission, a clean battle with finite hits) and nobody enumerated the other states the same
mission can reach: a declined creature, a NaN hit, a crash before teardown. A diagnostic line is evidence
in a crash log, so a false one is worse than a missing one.

R7 and R8 repeat the #644 lesson in `lessons/misc.md` ("A change that deletes or moves data invalidates
numbers and line refs elsewhere", with its plan 025 recurrence): the sweep searched for the deleted
names, while the stale lines named the method, the category or a count.

R3 is a further instance of the NaN-gate class, which `.claude/rules/csharp-architecture.md` says has
shipped six times and `lessons/gamemodels-services.md` records instance by instance (career cooldown
review 31, EditorCacheRebuild review 38, scene-scripts CS_Road, CombatMechanics 2026-07-02, a float-to-int
cast, the Enlistment grace window 2026-08-08, SettlementFood #546, an index bounds check 2026-09-30): the
rule exists and loads for every C# file; the check was written beside
two correct ones on the same field and read as obviously right.

## Why each agent missed these

The six lenses together found every finding here; none was found only by the lead. Per lens:

- **Agent 1 (Standards)** found R1, R3, R4, R5 and R7. It did not flag R6 or R8 because its checks are
  code standards; the docs it read were for the deleted names.
- **Agent 2 (Engine)** found R1, R2, R5 and R9, and verified every engine claim behind the census key and
  the troll latch. It does not grade aggregate arithmetic (R4) or doc counts (R8).
- **Agent 3 (Efficiency)** found R1, R5 and R7. Its rubric is cost; R3 and R4 cost nothing.
- **Agent 4 (Completeness)** found R1, R7, R8, R10 and R11. It took the DEBUG lane to be non-durable
  (`FileLogger.cs:86`), which is why it did not grade R5 as a loss; the writer thread drains DEBUG within
  about 50 ms.
- **Agent 5 (Data flow)** found R1, R3, R5, R6 and R7, the most of any lens, by tracing the subject key
  against the label and the docs.
- **Agent 6 (Design)** found R1 (as P3) and R7 (P5, P6). Its proposals P1, P2 and P4 were applied as
  behaviour-preserving improvements.

## Feedback memories to codify

None beyond the lessons entries: the NaN polarity and docs-sweep rules already exist and loaded for
these files; the new rule (a reason line is the complement of its summary; an aggregate survives the
failures it is for) is filed in `lessons/misc.md`.

## Convergence rounds

| Round | Diff read | Finding | Fixed in | Why missed | Preventive action |
|---|---|---|---|---|---|
| 1 | `494eec47..b812bb82` | C1: the `_hitTallies` comment said every tally subject is the victim's hero or party leader; the amplification path keys on the attacker | `8fee9625` | R6's rewrite followed the reduction key and not the amplification key that fills the same dictionary | Before describing a collection's keys, read every writer of it |
| 1 | same | C2: two test comments kept the "a battle with no creature skips every engine callback" wording that R1 and R2 made false | `8fee9625` | R2's reword swept the code, the behaviour comment and the feature doc but not the tests: the #644 sweep lesson (`lessons/misc.md`) recurring inside the review's own fix | The existing sweep lesson; tests and their comments are part of the sweep |
| 1 | same | C3: the feature docs did not list the new behaviour test file or the NaN close among the census summary's closes | `8fee9625` | The fix added a test file and a close path without opening the feature doc's Tests and "written when" lists | The feature doc's Tests and log sections are part of a fix's done criteria |
| 1 | same | C4: two counts in the review records (the NaN-gate class count, the number of tests proven RED) were wrong | `8fee9625` | Written from memory, not from the lessons file and the RED output | A count comes from the file that holds it, read in the same turn (`evidence-over-claims.md` C) |
| 2 | `b812bb82..8fee9625` | C5: this RCA and the plan's REVIEW-LOG entry did not record round 1, and the entry still listed the convergence pass as owed | the orchestrator's records commit after `8fee9625` | The review workflow asks its fix pass for the report's convergence section only, and no later step writes the RCA or the REVIEW-LOG | The orchestrator closes the RCA and the REVIEW-LOG entry after each convergence round. Asking the fix pass to do it (`.claude/skills/improve/workflows/review.js`, its fix prompt) is harness code and waits on the maintainer |

## Codex round (2026-10-03)

Codex read `0d1e91f0..37dab128` and confirmed one P2 and three P3 observations. It found no gameplay defect, no
regression in the damage calculations or the clan armour tint, and no patch-category problem. The P2 and the "nothing is
lost" wording share a root with R5: a diagnostic's write point was chosen from the callback the author had in mind, not
from every way the engine leaves the state it covers. Dispositions:
`docs/reviews/deep-review-030-mission-diagnostics-diet-2026-10-02.md`, "Codex round".

| # | Finding | Fixed in | Why missed | Preventive action |
|---|---|---|---|---|
| X1 | P2: the career summary was written only from `OnEndMission`, which runs only through `Mission.EndMission`; `GameStateManager.CleanStates` (the application shutting down, if a window close reaches the shutdown callback, unverified; or a mod that loads a save mid-mission) finalises the mission state without it, so those exits lost the counts and left the singleton's tally to ride into the next mission | `fix(diagnostics): v2.0.32 - review and Codex follow-ups for plan 030`: `OnRemoveBehavior` writes the summary too | R5 framed the limit as "a clean teardown versus a crash", and every lens took `OnEndMission` for every end | Read the engine's exit paths from the decompile before choosing a write point (`docs/reference/engine/mission-and-missionbehavior-lifecycle.md`, "Teardown paths"); lesson in `lessons/misc.md` |
| X2 | The aggregates do not keep every piece of the old per-hit information (D6's wording, the plan and the docs) | the same commit, in `career-system.md` | "Aggregate, never drop" was read as "an aggregate exists", and no doc named what the aggregate keeps and drops | A doc for an aggregate lists the statistics kept and the detail dropped, with one counterexample; lesson in `lessons/misc.md` |
| X3 | P3: the first-troll gate moved the spacing tracker's 0.5 s clock to the first troll tick, so a late troll is counted up to 0.5 s sooner and every later scan keeps the shifted phase, which lands a later re-space up to about 0.5 s sooner or later | the same commit (documented, no code change) | "Changes nothing once a troll exists" was reasoned from the width formula, not from the tracker's own self-throttle that the gate now starts later | A gate on a self-throttled consumer says where that consumer's clock starts; lesson in `lessons/misc.md` |
| X4 | P3: a refused creature event still built its thread label (88 bytes) | the same commit (`ThreadLabel`, built inside each granted branch) | The conversion put every `Line(...)` behind `TakeLine` and left one local above the switch | A "costs no string" claim gets an allocation test on the refused path of every kind; lesson in `lessons/misc.md` |
| X5 | P3: the tests prove services, not the engine loop (a source-order composition check, a census test with no mission) | the same commit: the two follow-up tests; the rest stays UNVERIFIED | The engine-side checks were declared as in-game work, but no record said which tests stand in for them | Say which tests are source-order or mission-less and which checks wait for the game |
