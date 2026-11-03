# RCA: the Long Defeat Phase 0 spike could not run, and its evidence could lie (2026-11-03)

**Top line:** the first tier 1 review of the spike (#1079) found that it could not pass S1. At 0.5 s into the mission, `Mission.OnInitialSpawnCompleted` threw on the main thread, and the siege tactics threw on the AI thread, outside every catch. Both came from behaviors the launcher had removed from vanilla's list. The review also found that the run could report success with no evidence: a stalled run exited 0 as PASS, and the gate test could log "repaired" for the wrong gate. Nothing had shipped and no player path reaches this code. All of this is read from the decompile and the source: **the spike has never run in game, so no fix here is proved in game.** Codex did not run, because this work has no paid-dispatch word.

## Findings

Severities are the ones the refuters assigned. Refuters checked first-wave findings and the second wave's data-flow and engine re-review. I read the source behind the rest.

| # | Sev | Bug | Category | Why missed | Preventive action |
|---|---|---|---|---|---|
| 1 | HIGH | `GeneralsAndCaptainsAssignmentLogic` asks `CustomBattleInitializationModel`, which reads the absent `DefaultBattleMissionAgentSpawnLogic`, so Boot always faulted | Removed vanilla behavior | The plan's grep searched the named classes for the removed types, and this read is two hops away, through a game model | Logic removed; a test pins it; lifecycle doc section; lesson |
| 2 | HIGH | No `IBattlePowerCalculationLogic`, so `TeamQuerySystem` threw on the async AI tick, outside the runner's try | Removed vanilla behavior | The plan's list of removed behaviors did not name this one at all, so nobody checked it | `LongDefeatSpikeLogic` implements it; a test pins it; lifecycle doc section; lesson |
| 3 | HIGH | A destroyed gate leaves `ActiveMissionObjects`, so S4 repaired another gate or nothing, and could log "repaired" on a gate that never broke | False evidence | The list read was correct for a live gate, and no test reaches an engine list | Lists taken once; repair scheduled only after a real break; lifecycle doc section |
| 4 | MED | The repair undid only `CastleGate.OnDestroyed`, not `Mission.OnObjectDisabled`, so the gate stayed disabled while the line read clean | False evidence | I read the method the plan named and not the caller around it | Vanilla's own `OnMissionReset`, plus `disabled` in the line |
| 5 | MED | `live_limit` and `defenders_dead` were written as `reason=final`, and the judge checked nothing else, so a stalled run exited 0 as PASS | Gate proves nothing | The wrapper dropped the Fords report checks and put no evidence check in their place | The real reason is written; the judge needs the spike lines; tests |
| 6 | MED | The "high" count used the gate's height, which on Helm's Deep is 10.6 m above the wall top | False evidence | Written from Minas Tirith's shape, where it happens to work | High line from the wall segments, plus a z spread in the log |
| 7 | MED | `Team.ResetTactic` keeps one `TacticBreachWalls`, which latched a shock assault from the first spawn batch | Engine state across waves | The plan named `ResetTactic` as the fallback without reading what it keeps | A new tactic per wave |
| 8 | MED | `fow_mordor_orc_recruit` no longer exists, so Minas Tirith faulted on its first spawn | Stale data reference | The id was copied from an older roster, and the launcher checked only the player and cultures | Launcher checks every troop; a test pins the table to the shipped files |
| 9 | MED | `started` was stamped at the first mission tick, so the judge's crash scan skipped the mission load | Gate proves less than it says | Copied the Fords field and not where Fords stamps it | The judge scans the whole log |
| 10 | MED | The S7 lines read the equipment object just assigned, so they changed whatever the driven armour did; the swap does not update driven armour | False evidence | Not traced to `InitializeAgentStats` | Logs the driven values and writes them |
| 11 | MED | Without `SiegeDeploymentHandler`, deployed towers kept the prefab's 20000 hit points against vanilla's 5000 | Removed vanilla behavior | Same as 2 | Vanilla's hit points set after deployment |
| 12 | MED | No view declined the focus-lost Escape menu, so a lost focus paused an unattended run until the stall rule killed it | Sibling guard not copied | The guard lives in a Fords view that the plan did not list | `LongDefeatUnattendedView` |
| 13 | MED | The first spawn batch's `StopUsingAllMachines` cleared `ForcedUse` on towers and rams for good, and finding 7's fix did not restore it | Incomplete fix | The first wave looked at the latch, and only the re-review looked at what the latch had already done | `SetForcedUse(true)` before the reset; `forced` in the `[S3]` line |
| 14 | MED | My reason for the constant team power was false: I said a running total starts wave 2 near half. A new tactic takes its start from `RemainingPowerRatio`, so it starts at 1.0. I repeated this to Mike, in a code comment and in the engine doc | Claim not verified | I read `:596` and did not trace whether that branch runs. Three reviewers disproved it | Text corrected; the real trade-off is now written down; lesson |
| 15 | MED | The trap row I added broke both ADR-011 caps, so the doc gate exited 1 | Own change | I wrote the row before I ran the linter | Row removed; the Mission logic row already links the doc |
| 16 | MED | The module landed with no feature doc or map row, and the owed spikes (S6, S8, most of S7) were recorded only in private memory | Docs | The plan said "branch only", so the doc was left for later, and Mike's word changed the plan | Feature doc, map row, console row |
| 17 | MED | The arm-file parser had no test, and a renamed key passed every Python test | Test gap | The plan's test exemption named the runner, and the parser was outside it | Parser tests; a test ties each arm key to the C# switch |
| 18 | MED | The `[S4] repaired` and `[S5] deployed` lines printed whatever happened, so a failed repair still counted | False evidence | The label claimed success and the judge tests a substring | `NOT repaired`, `NOT deployed` |
| 19 | MED | The autostart used the service locator and static flags, where its caller could inject | Standards | Written before reading `FordsAutorunStart` closely | Instance built by the caller; the read fault is logged |
| 20 | LOW | Smaller items, each fixed: a second `Finish` could overwrite the done file; the shared Fords catch took Long Defeat faults; the repair timer lived in one phase; hidden ladders stayed enabled; the `destroyed` columns could never print True; the `CLEARED` label differed from the state line's; three members were reached by name with no binding row; the launcher's and the runner's comments stated wrong engine facts; the module was inserted first against the file's rule; the tool accepted bad values; dead try and unused test switches; the wave mean and the window max covered different spans | Mixed | Spike written fast, and all of it first read by the review | Fixed in this change |

## Root cause

Findings 1, 2 and 11 share one cause. The plan treated a behavior list as a set of independent parts, so it checked whether the AI classes name the removed types. The engine couples them through other routes: an interface looked up by type, a game model, an event that only the removed handler subscribed to. A grep of named classes cannot see that. Fords of War never met these paths, because it runs no team AI, and that made its recipe look like stronger evidence than it was.

Findings 3, 4, 5, 6, 10 and 18 share a second cause. A spike exists to produce evidence, and these lines reported what the code asked for, not what the engine did. The rule "a verdict must be earned" came from the Staged Siege run report the week before ([rca-staged-siege-run-report-2026-10-31.md](rca-staged-siege-run-report-2026-10-31.md)) and I did not apply it here.

Finding 14 is the repeat of "Evidence, never invention": I stated an engine result from one read of one branch, then told Mike. The fix behind it was right and the reason was not, which makes it easy to miss.

## Why each lens missed these

Only lenses found these; nothing else had reviewed the spike. Lenses 5, 2, Tooling and the Step 2c new-mission agent ran first at `max`. Between them they found every HIGH, and each refuter confirmed its lens's findings, with corrections. Lens 5 and lens 2 agreed on findings 1 and 2 and proposed different fixes. Lenses 1, 3, 4 and 6 ran in the second wave at `high` on the fixed tree, with the re-review of lenses 5 and 2. The second wave found 13 and 14, which the fixes themselves introduced or left open. No lens ran the game, so no finding about native behaviour is settled: whether the AI-thread throw would have crashed or hung stays unknown.

## Not applied

- A try around the runner's file writes: no trigger found, and a speculative guard is Mike's call (`simplicity-criterion.md`).
- Refusing the Fords runner's unused flags: no caller passes them, and Phase 1 replaces the wrapper.
- The `python3` shebang: the convention of 234 of 266 tools.

## Follow-up

FOLLOW-UP: `docs/reference/unattended-game-runs.md` says exit 0 needs a clean-unload line, and the runner only logs that line. no issue: pre-existing and outside this change; Phase 1 generalizes the runner and rewrites this page, so the correction lands there.

FOLLOW-UP: `show_results` in `tools/run_fords_autorun.py` prints Fords labels over spike data. no issue: wording only, and Phase 1 replaces the wrapper with a general option on the runner.

FOLLOW-UP: S6, S8 and the rest of S7, plus the Phase 1 notes in [long-defeat.md](../features/long-defeat.md), are owed under #1079.

## Feedback memories to codify

None. The lessons are in `docs/reviews/lessons/adapters-taleworlds-api.md` and `docs/reviews/lessons/testing-qa.md`.
