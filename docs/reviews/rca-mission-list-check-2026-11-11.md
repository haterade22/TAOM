# RCA: mission list-change check (#1028, `/deep-review`, 2026-11-11)

## Top-line

The change adds a diagnostic for #1028 (a stealth hideout raid that loops the loading screen, crash bundle `taom_crash_20261111_145332_395a9270`): Patch103's `EarlyStart`, `AfterStart` and `AfterMissionStart` helpers now detect a change to the list `Mission.AfterStart` is walking and log which call it happened in. My first version compared the list's size. Two lenses found that the engine's enumerator throws on any version change, so an indexer write, a sort or an add paired with a remove would have gone unseen; the fix steps a copy of a `List<T>` enumerator taken before the call. Lens 5 also found a test that left a fake `Mission` in `Mission._current` for the rest of the run. Codex did not run (its usage limit), so that pass is owed before merge.

## Findings

| # | Sev | Bug | Category | Why missed | Preventive action |
|---|---|---|---|---|---|
| 1 | MEDIUM | Lens 2 and lens 5: the first check read the list's size before and after each call. `List<T>.Enumerator.MoveNext` throws when `_version` differs, and an indexer write, `Sort`, `Reverse` or an add paired with a remove change the version without changing the size. A miss costs the player a second round trip. | A proxy for the engine's own condition | I took "a behavior was added or removed" from #1028's hypothesis and tested only an add and a remove. | The helper takes `list.GetEnumerator()` before the call and steps a copy after it, which throws exactly when the engine's next step will. Tests cover an indexer write and an add plus remove. Lessons entry. |
| 2 | MEDIUM | Lens 5, lens 2 (LOW): `InstallMission` saved the current `Mission._current` on every call. The add test calls it twice, so cleanup restored the first fake and left it in the static. `BehaviorHelpers_NoCurrentMission_*` then ran against a three-item fake and passed without testing its named state. | Test isolation of a process-wide static | I wrote the save into the install helper, not into the test's setup, and the loop test installs twice. | The previous value is saved once per test (`_installed`), and the no-mission test sets null itself. Lessons entry. |
| 3 | LOW | Lens 5, lens 2: "newest item" is the last list item. It is the added item only for an append; after a remove or an `Insert` it names an unrelated item. | A label that claims more than the value | I printed the last item for every change. | It is printed only when the list grew, and is "n/a" otherwise. |
| 4 | LOW | Lens 5, lens 2: a comment and the log line said the call whose check differs "is the culprit". The check proves only that the change happened during that call. A mod's listener that the call triggers (for example `OnAgentCreated` during `HideoutAmbushMissionController.AfterStart`) would be charged to the vanilla owner. | A claim beyond the evidence | I wrote the conclusion I hoped the log would give. | The line, the comment, the registry and the feature doc say "during" and "the owner may only have called the code that made the change". Lessons entry (same as row 1). |
| 5 | LOW | Lens 5: the feature doc's Performance section still said the helpers allocate nothing and add only a `try`. | Stale doc | I updated the architecture text and not the cost text. | The section names the `Watch` cost. |
| 6 | LOW | Lens 5: no test ran the `MissionObjects` branch, so swapping the two arms would pass every test. | Test gap | `MissionObject` cannot be allocated in a test host, which I treated as the end of the matter. | `Watches_ReadTheMissionsOwnLists` and `NoteListChange_TheObjectsList_IsReportedAsMissionObjects` pin the mapping without a `MissionObject`. |
| 7 | LOW | Lens 2: the registry sentence about the `MoveNextRare` frames cited #1028, which does not hold the trace; the trace is in the crash bundle. | Claim without its source | I wrote from the bundle I had read this session. | The registry and the feature doc cite the bundle id. Posting the excerpt on #1028 needs the maintainer's word (below). |
| 8 | LOW | Lens 5, hypothetical: a call that finalizes the mission empties the list and nulls `Mission.Current`, and a check that re-reads `Mission.Current` afterwards would miss the change. | Re-reading a static after the call | The first version read the list again by `Mission.Current`. | The `Watch` holds the list reference taken before the call. |

## Root-cause pattern

Rows 1 and 4 share one habit: a diagnostic written from a hypothesis about the culprit, using a proxy (the size) and a conclusion (the culprit) that the engine's behaviour does not guarantee. Both were visible in the engine's own code: the enumerator's version check, and the fact that a wrapped call can trigger other code. Reading `List<T>.Enumerator` before choosing the signal would have shown both.

## Why each agent missed these

- **Lens 2 Engine compatibility** found rows 1, 2 (LOW), 3, 4 and 7, and proposed the enumerator probe that fixed row 1.
- **Lens 5 Data flow** found rows 1 to 6 and 8. Its trace of the enumerator version checks (T9) was the evidence for row 1.
- **Lenses 1, 3, 4, 6 and Tooling** were skipped by tier (tier 1, first wave only, no HIGH), so Step 4 is N/A.
- **Codex** did not run: the account hit its usage limit (retry at 2026-11-14 06:56). It is owed before merge.
- **The re-review of the fix** (lens 2, `effort: "high"`, fix diff only) is recorded in the commit body.

## Not fixed

- A change made outside the three loops' calls is not seen: `_battleSpawnPathSelector.Initialize()` and `_deploymentPlan.Initialize()` run between the loops and have their own `foreach` loops (lens 2, UNVERIFIED that the JIT never inlines one into the logged frame). If the new line never appears and #1028 recurs, look there.
- FOLLOW-UP: post the crash bundle result and the new log line on #1028; a public comment needs the maintainer's word. #1028

## Not run

- Codex pre-review (usage limit). No in-game check: the line is only proven by the next #1028 reproduction, so the issue stays open with its `triage-needs-ingame` label.
