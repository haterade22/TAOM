# RCA: guarded patch-category apply, plan 009 (2026-09-24)

## Summary

Plan 009 routed all 84 Harmony category applies through `PatchCategoryApplier` so one drifted
binding costs one category. Six deep-review lenses and a Codex adversarial review of
`7f02fc8d..9da9b5b9` produced 13 confirmed findings (1 HIGH, 2 MED, 7 LOW, 3 nits) and no false
positives. The HIGH one defeated the change's own promise: the failure notice for the 28 categories
applied in `OnSubModuleLoad` was sent before anything in the engine listens for messages, and the
list was cleared as it was sent, so a dead crash guard stayed silent on screen. The rest were
wording that assumed Harmony applies a category atomically and per category, review-harness text
still keyed to the old call spelling, and stale consumers of a deleted log line. Report:
`docs/reviews/deep-review-009-guarded-patch-category-apply-2026-09-24.md`.

## Findings

| # | Sev | Bug | Category | Why Missed | Preventive Action |
|---|---|---|---|---|---|
| 1 | HIGH | `ReportPatchFailures("module load")` at the end of `OnSubModuleLoad` drained the list into `InformationManager.DisplayMessage`, which has no queue and no subscriber until Native's `OnBeforeInitialModuleScreenSetAsRoot` builds the chat log | Dead / no-op code | The plan took the green "TAOM loaded successfully!" line at the same spot as precedent; that line has never displayed either. Nobody traced who subscribes to `DisplayMessageInternal`, or when | Report moved to the main-menu one-shot as an inquiry; `SubModuleSource_OnSubModuleLoad_DoesNotReportPatchFailures` gates it; lesson in `lessons/localization-ui.md` |
| 1b | (part of 1) | A chat line at the first main menu would also vanish: the splash video hides it and `GauntletInitialScreen.OnInitialize` calls `ClearAllMessages()` | Dead / no-op code | Codex's own fix suggestion stopped at "after the subscriber exists"; only the engine lens followed the line to the screen that clears it | Same test pins the inquiry path; same lesson |
| 2 | LOW | Summary "Those fixes are off this session" for a category Harmony left half applied | Other: assumed atomic library semantics | The plan described `PatchCategory` as all-or-nothing; the Harmony decompile in the plan quoted the throw, not the class loop around it | Wording test; lesson in `lessons/harmony-il.md` |
| 3 | MED | "Costs one category" is false when `BuildCategoryCache` cannot read an attribute: every category fails | Other: assumed per-category discovery | Same assumption as 2, one level up: the index is built from every type in the assembly on first use, and a throwing factory is never cached | Wording fixed; product decision to Mike; same lesson as 2 |
| 4 | MED | Lens 5 and the Harmony lesson grep for `_harmony.PatchCategory("<name>")` and flag its absence HIGH | Convention inconsistency | The plan's maintenance notes listed one rule line to update and did not grep `.claude/` and `docs/reviews/lessons/` for the old spelling | Both updated; lesson in `lessons/build-tooling-workflow.md` |
| 5 | LOW | `triage_battle_load.py` and `battle-load-diagnostics.md` pointed at the deleted Patch43 warning | Convention inconsistency | Collapsing nine catch blocks deleted nine log messages; nobody grepped the repo for their text | Repointed to `[PatchApply]`; same lesson as 4 |
| 6 | LOW | Failures left by an aborted game-init batch are labelled "mission start" | Stale state / lifecycle | Rides on the plan's deliberate residual (bare initializers can still abort the batch) | Deferred with the residual |
| 7 | LOW | `crash-report.md:284` implied Patch37 covers mods loading after TAOM | Other: doc half-corrected | The fix corrected TAOM's own case and kept the old sentence about other mods | Fixed |
| 8 | LOW | Patch77, Patch61, Patch83 comments still said an apply can throw there | Convention inconsistency | The plan said to keep those comments unchanged | Fixed |
| 9 | LOW | "logs and rethrows"; "another mod reshaping IL" as a cause | Other: wording | Paraphrased the engine instead of quoting `throw new Exception()`; name lookup is not IL | Fixed |
| 10 | LOW | Lifecycle doc row kept an em dash, "most", and stale line refs | Convention inconsistency | Edited one clause of a long table row | Fixed |
| 11 | LOW | Constructor guards and the Patch37, Patch77 and preview branches untested | Missing test | The plan's Step 5 greps ran once and were not turned into tests | Three tests added |
| 12, 13 | NIT | Duplicate comment; plan-numbered test ids | Other | Mechanical execution of the plan text | Fixed |

## Root-cause pattern

Findings 1, 2 and 3 share one shape: the plan described an engine or library boundary by its
signature and its throw, not by what surrounds the call. `DisplayMessage` compiles and never
throws at module load; `PatchCategory` throws, but inside a loop that keeps what it did and after
an index built from the whole assembly. Each claim was true of the line quoted and false of the
behaviour the player sees. The check that catches it is to follow the effect to its consumer: who
subscribes, when, and what clears it; what the loop around the throw has already done.

Findings 4 and 5 share a second shape: a rename or a deleted message has consumers outside the
compiled code (review lenses, lessons, triage tools, docs), and no build or test sees them.

## Why each agent missed these

- **Builder (plan executor):** implemented the plan as written. The plan specified the module-load
  report and the "off this session" wording (plan 009 Step 6 and the summary text), so both came
  from the plan, not the execution.
- **Agent 1 (Standards):** caught finding 4 and several wording items. Delivery of a message is
  runtime behaviour outside its checklist.
- **Agent 2 (Engine compatibility):** caught 1 and 1b, 2, 7 and 9. It did not raise 3; it verified
  the class processor, not the category index build.
- **Agent 3 (Efficiency):** no findings expected; its scope is cost.
- **Agent 4 (Completeness):** caught 4, 11 and the missing issue. Whether a notice reaches the
  screen is not a completeness item.
- **Agent 5 (Data Flow):** caught 1, 2, 3, 4, 5 and 6. It missed 1b: it proposed relabelling the
  main-menu call without following the line past the splash video.
- **Agent 6 (Design):** proposed the finding 1 fix, but as a chat line; it flagged the fade as
  UNVERIFIED rather than reading `GauntletInitialScreen`.
- **Codex:** caught 1 and 2 and quoted `BuildCategoryCache`, noting discovery is not
  category-local, without raising it as a finding. It missed 1b, 4 and 5.

## Feedback memories to codify

None beyond the three lessons appended:
- `docs/reviews/lessons/localization-ui.md`: nothing receives a chat message before the initial
  screen; report startup problems in an inquiry.
- `docs/reviews/lessons/harmony-il.md`: `PatchCategory` is neither atomic nor category-local.
- `docs/reviews/lessons/build-tooling-workflow.md`: renaming a convention or deleting a log line
  means grepping the review harness and tools for the old text.
