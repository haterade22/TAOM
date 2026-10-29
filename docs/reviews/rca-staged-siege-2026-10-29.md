# RCA: Staged Siege (#1054) Phase 1, first deep review (2026-10-29)

**Summary.** Staged Siege Phase 1 turns a campaign siege assault on a recorded scene into four capture stages (A outside
the walls, B the gatehouse, C, D), with the vanilla lord's hall as E when the player attacks. It was reviewed uncommitted
in the worktree `E:\repos\taom-staged-siege` (branch `taom-staged-siege`, base `9ade8175e`) after one spike session in
game (the flag object, the gates and the recorder) and before any Phase 1 game run. Risk tier 1 (two Harmony patches, a
mission-tick service, an AI-thread tactic, native calls). Three lenses ran in one wave: data flow, engine compatibility,
XML. Codex was not dispatched: the session had no paid-dispatch word for this work.

- **Severity:** 0 CRITICAL, 0 HIGH, 8 MEDIUM, 12 LOW. No second wave was due. The fix re-reviews added 2 MEDIUM and 7
  LOW: rows 21 to 26 (data flow) and 27 to 29 (engine).
- **Where they came from:** mostly the seams between this feature and the rest of the game: a vanilla tactic helper that
  ignores who commands a formation, a vanilla rout check that reads a value the staged timer made meaningless, the
  engine's refusal to split a player's formation, the sergeant popup keyed on the tactic's type name, the console thread.
- **Outcome:** all 29 findings fixed with tests (rows 1 to 26 in commit `77306cf6b`, rows 27 to 29 after it). The
  engine-compatibility re-review stopped once on the weekly usage limit and finished after it lifted. The first Phase 1
  game run is owed.

Feature doc: [staged-siege.md](../features/staged-siege.md). Fix list as applied: rows below.

## Findings

Severity is the finder's. "Lens" names the finder. Each MEDIUM was re-checked against the source or the installed v1.5.6
DLLs (taom-src cache) before it was entered here.

| # | Sev | Bug | Category | Why Missed | Preventive Action |
|---|---|---|---|---|---|
| 1 | MEDIUM | In stage A the tactic called `TacticComponent.StopUsingAllMachines()` on every team with a team AI. The helper stops every formation of the team on every siege weapon, with no `IsAIControlled` check (TacticComponent.cs:528-550), so a player general's own ram, tower and ladder orders were undone each second. Lenses: data flow, engine. | Vanilla helper with a wider reach than its name | The spec called it to "hold AI-led engines" from its name; vanilla only calls it at mission end, so its reach was never read. The tactic's steering half filtered player formations; this call sat before the filter. | Skip it when `Team.IsPlayerTeam && Team.IsPlayerGeneral`; both members pinned in the binding test. Lesson in `lessons/adapters-taleworlds-api.md`. |
| 2 | MEDIUM | Swapping to the custom reinforcement timer left `GlobalReinforcementInterval` at 3 s. Under the custom timer its only reader is BattleEndLogic's rout grace (`GetReinforcementInterval + 3`, BattleEndLogic.cs:299, :335), so a side whose remaining troops all fled lost as a rout while the stage budget held its reserve; the softlock rule covered only 0 active. Lens: data flow. | A replaced mechanism's leftover value still read elsewhere | The spec traced the interval into the spawn logic only, not every reader of `GetReinforcementInterval`. | Interval = max(old, trickle); the softlock counts STANDING agents (active and not running away), sampled once per pass. Tests on the budget, the service and the prefix. Lesson in `lessons/adapters-taleworlds-api.md`. |
| 3 | MEDIUM | In a defence the player leads, the detachment never formed: `TransferUnits` reaches `TransferUnitsAux(..., isPlayerOrder: false)`, which returns when `!IsSplittableByAI` (Formation.cs:2157-2160), and a player general's formations are not AI-owned; the log still said "from N formation(s)" and the doc described a path the engine never reaches. Lens: engine. | Engine refusal inside a transfer helper | The spec excluded only the formation holding the player. `lessons/adapters-taleworlds-api.md:565` already says to grep `IsAIOwned`/`IsSplittableByAI` before inventing a guard: a repeat of that lesson's ground. | Eligible infantry requires `IsSplittableByAI` (pinned); the doc says a player-led defence forms no detachment. |
| 4 | MEDIUM | A sergeant player would see "ERROR: Text with id str_team_ai_tactic_text doesn't exist! Variation: TacticStagedObjective": `TeamAIComponent.MakeDecision` shows that text keyed on the tactic type name (TeamAIComponent.cs:330-334). Lens: engine. | Type-name-keyed engine text | Culture Doctrine added its rows in 2026-09, but its test scanned only `TaomTacticBase` subclasses, and this tactic derives from `TacticComponent`. Repeat class: a gate scoped narrower than the rule it protects. | Row added and seeded; new test `EveryConcreteTacticComponentInTaom_HasASergeantPopupRow` over every concrete `TacticComponent` in TAOM.dll. Lesson in `lessons/localization-ui.md`. |
| 5 | MEDIUM | The `staged_siege_stage`, `open_gates` and `show` console commands wrote engine AI, gate navmesh and mission-object state from the console callback, which can run while the async AI tick iterates `FormationAI._behaviors`. Lenses: data flow, engine. | Console thread | `docs/features/culture-doctrine.md:923` records that console commands can run during an async tick, but it lived in one feature doc, not in a rule or lesson a builder reads. First time as a lesson. | Commands only enqueue; the behavior drains the queue on the main thread before the service ticks, armed or not. Tests on the queue. Lesson in `lessons/harmony-il.md`. |
| 6 | MEDIUM | The `captureRadius` tunable did nothing for recorded scenes: the recorder wrote `radius: 8` on every point and the catalog prefers a point's own radius. Lens: XML. | Validated value never fed | The recorder copied the default as a constant. Repeat of the "validated but never fed" row in `rca-lost-footing-2026-10-20.md` (row 11). | Points carry a radius only when one is typed; the shipped JSON names none, pinned by `ShippedFile_PointsNameNoRadius_SoTheCaptureRadiusTunableReachesThem`. |
| 7 | MEDIUM | `DataSource="{..}"` on the HUD movie root, which the engine never reads and which TAOM's `PrefabBindingWalker` stops at. Lens: XML. | Dead markup copied from a sibling | Copied from `BattleActionBar.xml:4`. Repeat of `lessons/localization-ui.md:883`. | Removed; `TheMovieRoot_CarriesNoDataSource` pins it. |
| 8 | MEDIUM | Creature Siege Role's per-agent scripts override the stage plan for attacking trolls too (A: strike the gate; C and D: hold the courtyard); the doc named only defenders. Lens: data flow. | Two features steering one agent | The spec checked defenders only. | Doc corrected; the adaptation is Phase 2 of #1054. |
| 9 | LOW | The HUD progress line joined two translated fragments with " - " in code. Lens: XML. | Fragment keys | Repeat of `lessons/localization-ui.md:707`. | Four whole-line keys; old rows removed from 13 files; `check_orphan_loc_rows.py` passes. |
| 10 | LOW | No test pinned the HUD prefab's bindings to its view model. Lens: XML. | Unpinned binding | | `StagedSiegeHudPrefabTests` walks the prefab both ways. |
| 11 | LOW | The HUD's failure latch was reset on the next frame, so a repeating fault would rebuild the layer every frame; the finalize detach had no try. Lenses: data flow, engine. | Latch | | `_failed` latch checked first; `TryDetach` everywhere. |
| 12 | LOW | The tactic's reapply check compared a count of formations, so one emptying while another filled left the new one unsteered. Lens: data flow. | Count as identity | | `FormationIndex` bitmask; tested. |
| 13 | LOW | The recorder accepted a radius and a facing the catalog refuses. Lens: data flow. | Producer looser than consumer | | Reuses the catalog's range and `StageFacing.TryNormalize`; tested. |
| 14 | LOW | `CountInZone` let a NaN position count (`>` comparisons). Lens: data flow. | NaN gate | Repeat of the NaN-gate rule in `.claude/rules/csharp-architecture.md`. | Negated comparisons. |
| 15 | LOW | Doc credited the wave count of 0 with capping the refill; the real cap is `InitialSpawnedNumber - active`. Lens: data flow. | Doc | | Corrected. |
| 16 | LOW | An "S2 release" log line was a reservation, not a batch. Lens: engine. | Log semantics | | Renamed `S2 reservation`; doc rows say "interval between reservations". |
| 17 | LOW | Doc and comment wording about the deployment plan and the adapter's try; `S4 gates opened` counted disabled gates. Lens: engine. | Docs, count | | Reworded; `OpenGates` skips `IsDisabled`. |
| 18 | LOW | The `in` write of the spawn settings was marked UNVERIFIED; the IL proves it reaches `Init`. Lens: engine. | Stale UNVERIFIED | | Comment and registry say the IL proves it, the S2 line confirms it. |
| 19 | LOW | The client HUD view is outside the binding test's PrepareMethod gate. Lens: engine. | Gate coverage | | First not applied (it failed alone with FileNotFoundException, misread as a probe gap); applied in row 28 once the real cause was found. |
| 20 | LOW | Doc owed list lacked two checks: the hall when about 20% of the defender reserve is unspent, and defender reinforcements possibly assigned into the detachment formation. Lens: engine. | Owed checks | | Both added to the doc's open list. |
| 21 | MEDIUM | Re-review of row 2's fix: the standing count was sampled once per 0.5 s pass, so a softlock kept approving on every engine tick after reinforcements began to spawn. Each approval runs `CheckReinforcementBatch`, which restarts every formation's spawn slot (`ResetReinforcementSpawnedUnitCountsPerFormation`, MissionBattleSideSpawnContext.cs:224-233, :503-517), so about 30 troops a side would spawn stacked on slot 0. Lens: data flow (fix re-review). | A cache sampled slower than its consumer acts | The fix moved the read from a live per-tick count to a per-pass cache without asking what each extra approval costs the engine. | `NoteAgentBuilt` raises the side's standing to at least 1, so the first built agent ends the softlock; RED-then-GREEN test `ShouldReleaseBatch_Softlock_EndsOnTheFirstBuiltAgent_BeforeTheNextPass`. Lesson in `lessons/adapters-taleworlds-api.md`. |
| 22 | LOW | `CountStanding` read only the main team of a side; vanilla's rout check walks every team of the side, and a campaign siege can field an ally team. Lens: data flow (fix re-review). | Scope narrower than the mirrored rule | | Counts every team with that side, as `CountInZone` does. |
| 23 | LOW | Spike-era recorder lines carry `radius: 8` on every point, and the shipped-config test banned any radius anywhere though the doc allows a per-point one. Lens: data flow (fix re-review). | Old data, over-broad test | | The test checks the pilot scene's points only; the recording steps say to re-record or drop spike-era point lines. |
| 24 | LOW | The steep-look refusal also refused point records, whose facing is never read. Lens: data flow (fix re-review). | Validation on an unused field | | Refused only for defender and detachment frames. |
| 25 | LOW | `trickleSeconds` now also sets the rout grace (interval plus 3 s), and the tunables table did not say so. Lens: data flow (fix re-review). | Doc | | Table row says it. |
| 26 | LOW | Nothing in the binding test said why the client view is absent from its list. Lens: data flow (fix re-review). | Unexplained omission | | A comment beside the list gives the reason (row 19). |
| 27 | MEDIUM | Re-review of row 1's fix: the guard asked about the player's own TEAM, but `StopUsingAllMachines` clears `ForcedUse` (the player's engine order) on every engine of the SIDE (TacticComponent.cs:536-540). An AI ally team on the player's side (a lord who joins the assault outside his army, MissionCombatantsLogic.cs:103-105) still reset a player general's orders in stage A. Lens: engine (fix re-review, resumed after the usage limit). | Per-team guard on per-side state | Row 1's fix read only the `StopUsingMachine` half of the helper, which does filter by team. | Mike chose the per-side rule: `StagePlan.StopsMachines(stage, side, playerCommandsSide)`, with `playerCommandsSide = Team.IsPlayerAlly && PlayerTeam.IsPlayerGeneral`; RED-then-GREEN unit test; both members pinned. Lesson in `lessons/adapters-taleworlds-api.md` (the "every reader and guard" entry covers it). |
| 28 | LOW | Row 19's reason was wrong: the test's assembly probe does cover Native's bin; the class just never called `GameAssemblies.EnsureLoaded()`. Lens: engine (fix re-review). | Wrong diagnosis | | A class initializer loads the assemblies, the client view joined the IL-compile list, and the test passes alone (1/1, run in isolation). Row 19 is now applied. |
| 29 | LOW | The registry heading said "confirmed at runtime" before any game run. Lens: engine (fix re-review). | Claim ahead of evidence | | "Verified by IL; the first game run confirms it." |

## Root-cause patterns

**Vanilla helpers and checks read past their names (rows 1, 2, 3, 4).** Every MEDIUM engine finding is one engine member
whose reach is wider than its name suggests: a "stop all machines" that ignores who commands the formation, an interval
that also feeds the rout check, a transfer that silently refuses a player's formation, a popup keyed on a class name. The
spec read each member's signature and its obvious caller, not every reader or its guard clauses. The planning-side fix
is the one `rca-lost-footing-2026-10-20.md` already named: an engine member a design depends on is read at its body and
at every reader before it goes into a spec.

**Gates scoped narrower than their rule (rows 4, 7, 9, 19).** The doctrine popup test covered one base class; the
prefab walker skips nothing but was never pointed at this prefab; the fragment-key lesson existed but no gate reads
player-facing joins. Where a cheap gate can cover the whole rule, widen the gate (row 4 did).

## Why each lens caught or missed what it did

- **Data flow** traced the staged values into their consumers outside the feature: rows 1, 2, 5, 6 (with XML), 8, 11 to
  15.
- **Engine compatibility** read the engine bodies behind the names: rows 1, 3, 4, 5, 16 to 20, and turned an UNVERIFIED
  item into proof (row 18).
- **XML** ran 12 gates (0 failed) and found the markup, the dead tunable and the fragment keys: rows 6, 7, 9, 10.
- **Standards, efficiency, completeness, design:** skipped by tier (no CRITICAL or HIGH).
- **Fix re-review:** data flow re-traced every fix and found rows 21 to 26; engine compatibility stopped on the weekly
  usage limit, resumed against commit `77306cf6b` once it lifted, verified 21 items and found rows 27 to 29.

## Feedback memories to codify

None. The lessons below carry the rules.

## Follow-ups

- FOLLOW-UP #1054: Creature Siege Role reads the stage (row 8), Phase 2.
- FOLLOW-UP #763: paid translation of the staged siege keys (all 12 languages hold English seeds).
- FOLLOW-UP no issue: `BattleActionBar.xml:4` carries the same dead root `DataSource` (row 7); pre-existing, outside this change, harmless at runtime; it goes with that feature's next edit.
