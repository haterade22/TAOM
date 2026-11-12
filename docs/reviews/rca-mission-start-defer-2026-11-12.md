# RCA: queueing behaviour adds during Mission.AfterStart (#1028), deep-review 2026-11-12

## Top-line

`/deep-review` at tier 1 (Agent 5 data flow and Agent 2 engine compatibility, both `deep-reviewer` at `max`) reviewed the change that
queues a `Mission.AddMissionBehavior` made while `Mission.AfterStart` walks `MissionBehaviors`. The code was sound: no CRITICAL, no
HIGH, and no window, queue or flush path that loses or repeats an add. It found **2 MED and 4 LOW** items, and all but the design
options were fixed in the same session. The two MED items were wrong statements in the docs, written by the author. Codex did not
run: the account hit its usage limit, and the maintainer waived the pre-review on 2026-11-12. The maintainer also waived the in-game
check the same day; the verdict manifest stays and reads NOT REACHED until a session plays the path.

## Findings

| # | Sev | Finding | Category | Why missed | Preventive action |
|---|---|---|---|---|---|
| F1 | MED (both lenses) | The docs and two code comments said the practice fighters spawn inside `ArenaPracticeFightMissionController.AfterStart` and that the dialogue path spawns none inside `AfterStart`. The guard's own log line names `CampaignMissionComponent` as the owner. The real chain is `CampaignMissionComponent.AfterStart` raising `CampaignEvents.AfterMissionStarted`, then `ArenaMasterCampaignBehavior.AfterMissionStarted` (`:368-376`) calling `StartPlayerPractice()` on the menu path. | Causal chain taken from the first file read, not from the owner the evidence names | I opened the controller because its name matched the mission in the log and stopped there. The guard line had already named another owner, and I read it as "may only have called the code that made the change" without tracing which code. | Lesson `harmony-il.md` "Take the cause from the owner the evidence names". |
| F2 | MED (both lenses) | The registry named `Mission.AddMissionBehaviors`, which does not exist (the method is `InitializeStartingBehaviors`), said "three types" call `AddMissionBehavior`, and said only a mod calling from a start call lands in a window. The real scan finds eight caller methods, including four `OnMissionStarted` listeners that add inside `AfterStart`, outside the windows. | An empty or short search read as a complete list | I grepped the decompile cache (287 of the engine's types, not all of them) for `AddMissionBehavior(` and got three files. The doc named the scope ("decompile, three types") but treated the cache as complete. `evidence-over-claims.md` C4 covers this; I wrote the scope and then reasoned past it. | Same lesson: a caller list for a patched method comes from an IL scan of every referencing assembly. The registry now states what was and was not swept. |
| F3 | LOW | "This part removes the loop" overstated it: a remove or a direct list change inside the walk still reloads the mission. | Overclaim | Written before the limits were listed. | Doc reworded, limits listed. |
| F4 | LOW | The new patch class bound the engine's parameter by name (`missionBehavior`). `PatchCategoryIndex.Apply` stops at the first class that throws, and the new class applies before the `AfterStart` class, so an engine rename would also take down the older guard. | Blast radius of a new class in an existing category | I pinned the name in a binding test, which catches the rename at the next bump but does not contain it. | Bound by position (`__0`); the binding test pins the type and the `__0` shape. |
| F5 | LOW | Tests: the window test called `TryDefer` directly, so a broken toggle gate could not fail it; two negative tests asserted only the return value; there was no test of the flush after a throw (the case that matters, since DualWield never re-adds) and none that the two submodule helpers never open the window; one test name said `Open_` and called `Reset`. | Tests that pass for the wrong reason | I tested the state class and the helper separately, not the gate the engine's call reaches. | Fixed (10 tests added or tightened). |
| F6 | LOW | The docs overstated the DualWield cost ("the practice fighters get no dual-wield handling": respawns and the player's late re-registration are handled) and omitted that a queued behaviour has no `Mission` and has not run `OnCreated` until the flush. | Missing consequence | The cost was written from the postfix's early return only. | Doc rewritten with the cost and the limits. |

## Not applied (a decision for the maintainer, or out of this change)

- Agent 5's design option: replace the two `GetEnumerator()` calls in `AfterStart` with an enumerator over a copy, so the add is
  immediate and the first batch of agents is registered. It adds two swap sites and a new `ExpectedSites`. The maintainer chose queueing.
  FOLLOW-UP no issue: raise it only if the unregistered first batch matters in play.
- PatchShield can shield `AddMissionBehavior` and strip TAOM's prefix after a missing-API throw from a behaviour's `OnCreated`, with no
  warning. Adding the method to `ExcludedTargetMethods` is a guard for a speculative case.
  FOLLOW-UP no issue: the failure is named in the registry; the maintainer decides whether to guard it (`simplicity-criterion.md`).
- The MCM hint text for "Survive Mission Start Failures" (`CrashReportSettings.cs:42`) does not mention queued adds. It is player-facing
  and an English edit stales 12 languages.
  FOLLOW-UP no issue: change it only together with the next edit to that setting's text.
- A separate sub-category for the new class (its failure would then not stop the `AfterStart` class). `__0` removed the likely cause.

## Root-cause pattern

F1 and F2 share one habit: a statement about the engine written from a partial read, with the scope named but not respected. In both
cases the evidence to do better was in hand (the guard line named the owner; the cache's size was known). Both lenses found both
errors independently, which is the argument for running two lenses at tier 1 even when the code is sound.

## Why each lens caught it (and what did not run)

- **Agent 2 (engine compatibility)** read the installed IL and the vanilla sources, so it traced the listener chain and swept the
  assemblies for callers, which is the check I skipped.
- **Agent 5 (data flow)** traced the state's lifetime and found the tests that could not fail; the same trace found the doc errors.
- **Agents 1, 3, 4, 6** did not run (tier 1 rule). They found no part of this: the findings are engine facts and test gaps.
- **Codex** did not run (usage limit). The maintainer waived it on 2026-11-12.

## Follow-up review: the in-game proof (same day)

The commit after `6f32888fb` adds what `csharp-architecture.md` "Outcome lines" requires and `6f32888fb` shipped without: an
`IOutcomeLog` check line for the feature and a manifest in `tools/ingame_checks/`. That omission was my standards miss. The tier-1
lenses did not include the Standards lens (the tier rule runs it only in a second wave), and `/verify` does not check this rule.
`/deep-review` tier 2 (Agent 5 data flow at `high`) reviewed the follow-up: no HIGH, one MED and four LOW items, all but one fixed.

| # | Sev | Finding | Category | Why missed | Preventive action |
|---|---|---|---|---|---|
| G1 | MED | The manifest fails #1028 on any mission start loop in the session (a list change in either list the engine walks, or a "threw and was not survived" line for any cause), but its header and `needs` named only `MissionBehaviors`. | A verdict's fail patterns judge the whole session, not the path under test | I wrote the header from the one path I was fixing. | Header, `needs` and the feature doc now say so: a loop the fix does not cover should read FAIL. |
| G2 | LOW | Nothing tied the written line to the manifest's pass regex. The C# test checked only that the id strings appear in the TOML, and the pass pattern ended in `$`, so a changed line format would read NOT REACHED for ever, never FAIL. | A verdict that cannot fail | C# and Python each pinned half of the chain. | A C# test renders the exact lines through the real `OutcomeLog`; a Python test judges the same literals on the real manifest; the `$` is gone. Lesson in `testing-qa.md`. |
| G3 | LOW | `IOutcomeLog?` parameters with a null branch no caller reaches. | Dead branch | A defensive habit. | Removed. |
| G4 | LOW | The feature doc's test counts were stale (39, 21 and 15 against 53, 30 and 18). | Counts typed, not computed | They were typed when the tests were written. | Updated, with the command and the date. |
| G5 | LOW | In a reload loop the list-change outcome call builds its strings every frame before the once-per-key latch. | Letter of "Outcome cost" | None: it runs only while the game is already stuck in the loop. | Accepted, not changed. |

## Feedback memories to codify

None. The lesson goes into `docs/reviews/lessons/harmony-il.md`.
