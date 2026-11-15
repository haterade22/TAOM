# RCA: the scenario run stood still behind a story event window, #1075

The first unattended scenario run (`E:\Temp\scenario-runs\20261115-142426`, build cb470ab4e) reached `reason=ok` for 3 of 3
days, but it stood on campaign day 2 from 14:26:22 to about 14:35:24. At 14:26:22 the War Chronicle event `breaking` opened its
story window, which pauses the campaign with `GameStateManager.RegisterActiveStateDisableRequest`. The driver's status read
`screen=Map time_locked=0` the whole time, and it had no way to see or close that window. The window got focus at 14:35:23
(`rgl_log_36184.txt`), so a person most likely closed it. A 150-day run would have ended `stalled` at its first chronicle event.
The launcher then failed the good run, because it compared the build stamp with a HEAD that a commit had moved during the run.

The fix closes an open Realms popup from the driver's Running phase (`ICampaignAutorunHost.CloseEventWindow`). The review of the
fix ran at tier 1: lens 5 (data flow), lens 2 (engine compatibility) and the tooling lens on b82a57928, each at the agent
definition's `max`. Codex did not run: its usage limit blocks it until 2026-11-20, so that pass is owed. Lens 2 found no
incompatibility (13 API uses verified, the native frame order UNVERIFIED and not needed). No game run has proved the fix yet.

| # | Sev | Finding (source) | Category | Why missed | Preventive action |
|---|---|---|---|---|---|
| 1 | HIGH | The driver could not see or close TAOM's story event windows, which pause the campaign; the first run stood 9 minutes on day 2 (game run) | Unseen pause | The slice review listed what stops time from the engine (inquiries, scene notifications, menus, the time lock, focus loss) and never grepped TAOM's own code for the same pause call. `RealmsPopupLayer.cs:57-61` is the only TAOM caller | Fixed: `CloseEventWindow` in the host, called from `Running`, with tests. New lesson in `campaign-mechanics.md` |
| 2 | MED | The fix first counted event windows in `notices_hidden`, the counter the doc names as evidence for the scene notification rule (lens 5) | Shared counter | The counter was reused because both are "popups"; the doc's evidence line was not read against it | Fixed: `_eventWindows` and the done key `event_windows_closed`; a mutation back to the shared counter fails `Tick_Running_ClosesAnEventWindowLogsItAndCountsItApartFromNotices` |
| 3 | MED | The launcher judged the build against HEAD at judge time, so a commit during the run failed a good run (game run) | Timing | The sibling `run_long_defeat.py` reads HEAD once in `main`; the wrapper passed `tree_head_sha()` inside the closure | Fixed in b82a57928 with a test that fails without it |
| 4 | MED | The PASS text did not name the build it proved; after finding 3 a PASS can be quoted for a later commit (tooling lens F1) | Unbound PASS | The judge returned a fixed text; `run_long_defeat.py` names the build | Fixed: the PASS starts `build <sha>, `, with a test for clean and `.dirty` stamps |
| 5 | MED | Two docs said the launcher deploys the checkout, and the `game-run` skill said no scenario driver exists (tooling lens F2) | Doc from intent | The sentences were written from the plan, not from the launcher's code, which has no build step | Fixed in `faction-balance.md`, `scenario-autorun.md`, `tools/README.md`, the launcher docstring and `.claude/skills/game-run/SKILL.md` |
| 6 | LOW | An unknown HEAD failed the run only at the end, after the whole game had run (tooling lens F3) | Late refusal | Repeat of `build-tooling-workflow.md` "An unattended wrapper binds its PASS to the build under test" ("If git cannot answer, it refuses") | Fixed: `main` refuses before launch, with a test |
| 7 | LOW | The HEAD patch in `test_runner_judge_holds_the_run_to_the_armed_plan_and_this_tree` did nothing after finding 3 (tooling lens F4) | Inert test | The patch wrapped the judge call, but HEAD is now read in `main` | Fixed: `run_main` patches HEAD; the test asserts the HEAD it closes over |
| 8 | LOW | The `n=` of the new log line counted scene notifications too, and one new test could not fail (lens 5) | Test gap | The fake counts only while a window is open, and the test never opened one | Fixed with finding 2: the test now reads the done key and the log |
| 9 | LOW | Wording: the host comment said a later tick closes the window, and the interface named only the War Chronicle (lens 2, lens 5). On the re-check, the doc still said every window closes in the tick it opens (only true in Running) and gave 5 to 6 s per day where the 1-second stamps allow 5 to 7 (lens 5) | Comment from summary | A window opens and closes in the same application tick only in Running (`ClientSubModule.cs:95, 109`; `ScenarioAutorunDriver.cs:176`) | Fixed |

FOLLOW-UP #1075: the three unattended launchers disagree on the reference HEAD (`run_command_trial.py` reads it at judge time on
purpose, `seat-of-seeing.md` records that, and `run_scenario.py` accepts any `.dirty` stamp of HEAD). One rule belongs in the
shared base that #1075 already plans to extract; it needs the maintainer's word (tooling lens F5).

FOLLOW-UP no issue: the status line cannot name a pause from `ActiveStateDisabledByUser` (lens 5 trace 8). Lens 2 found no
other window a default run meets that stops time unseen, so a `paused` field is not added now (`simplicity-criterion.md`).

## Root-cause pattern

Findings 1, 5 and 9 share one cause: a statement about the system was written from a list or a plan (the engine's pause
sources, "the runner deploys", "a later tick closes it") and not from the code that produces the effect. Findings 3, 4 and 6
share a second cause: the wrapper copied the sibling's check but not the timing and the reporting around it. Finding 6 repeats a
lesson already on file.

## Why each lens caught or missed these

- No lens of the slice review found finding 1: lens 2 read the engine's pause sources, and no lens grepped TAOM's own code for
  them. The first game run found it.
- Lens 5 found 2 and 8 by tracing the counter to the doc line that reads it.
- The tooling lens found 4 to 7 by comparing the wrapper with `run_long_defeat.py` and by mutation runs.
- Lens 2 found the wording in 9 by reading the frame order of `ClientSubModule.OnApplicationTick`.

## Related, outside the code

The first launch of the run crashed during boot in the NVIDIA driver (`nvwgf2umx.dll+0x1681799`). The install's
`RuntimeDataCache` held a 4096x512 cache for `ui_taom_bannericons_2`, and the banner commits had made the sheet 4096x1024. The two
banner sheet caches were moved to `E:\Temp\rdc-backup-20261115-bannericons`, and the game built them again on the next launch.
The trap is already on file in `docs/reference/banner-icon-generation.md`. Whether a player who updates in place can meet the same
crash is UNVERIFIED and is the maintainer's release decision.

## In-game checks owed

A run with the fix (`python tools/run_scenario.py --days 3` after `pwsh ./build.ps1`) should log
`event window closed n=1 phase=Running` within seconds of `[WarChronicle] 'breaking' Occurred`, pass day 2 without a gap, and show
no `[TAOM.Realms]` error.
