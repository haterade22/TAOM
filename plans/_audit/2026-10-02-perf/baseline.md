# Baseline: 2026-10-02-perf

Commit `dffdf879` (`bannerlord-1.5.x`), measured in the program worktree before any edit.

| Check | Result | Wall time |
|---|---|---|
| `dotnet test TAOM.Tests -p:DisableModuleCopy=true -p:ModuleId=` (builds Main and the tests) | `Failed! - Failed: 1, Passed: 12345, Skipped: 2, Total: 12348, Duration: 30 s - TAOM.Tests.dll (net472)` | under 5 min including the build |
| Failing test | `EveryLanguage_DeclaresARowForEveryEnglishKey` (`TAOM.Tests/Infrastructure/Localization/LanguageFileCoverageTests.cs:157`): English keys without rows in other languages; the translator run is paid and waits on the maintainer | |
| Skipped | `WargAttack_FastWarg_InvokesRunningAttack`, `WargAttack_SlowWarg_InvokesStandingAttack` | |
| Build warnings | nullable warnings in tests (CS8600, CS8603, CS8619, CS8625), none in Main counted as errors | |

Executors compare failure sets by name: a failure other than the one above is theirs.

The Python suite (`python -B -m unittest discover -s tools/tests -t .`) is recorded by the first plan
that touches `tools/`, before its first edit. Plan 029's executor recorded it at the base: `Ran 2962
tests`, `FAILED (failures=3, skipped=8)`, failing `test_applying_every_spec_is_a_no_op`,
`test_the_committed_career_file_is_what_the_rule_derives` and `test_default_is_on_the_e_drive` (the
last depends on the worktree path); the orchestrator re-ran it in the program worktree with the same
three.

Trunk CI under reference assemblies (run 37080081643 at `dffdf879`) fails three tests: the one above
plus `Patch93_HasTheSevenPatchesInItsCategory` and `Patch94_HasTheMapIconNoParleyAndNoJoinPatches`
(`FileNotFoundException` for `TaleWorlds.MountAndBlade.View`).

## Battle frame times in the sessions on disk (before any perf change)

`tools/perf_runs.py` (plan 029, branch `perf/029-perf-runs-parser`) over the 30 `taom_debug` logs on
disk, 2026-09-28 to 2026-10-02: 16 missions, every build Debug (`DIRTY_BUILD` on all). Medians are
over the steady 5-second `[MissionPerf]` windows; `maxms` is the worst single frame.

| Log | Scene | Agents max | fps median | p95 ms | max ms | gc0/min | Flags |
|---|---|---|---|---|---|---|---|
| 09-29 19:47 | battle_terrain_n | 1,184 | 103.5 | 11.4 | 38.6 | 67.4 | |
| 09-29 19:47 | battle_terrain_n | 629 | 111.0 | 10.8 | 102.7 | 56.0 | |
| 09-30 13:13 | taom_rohan_edoras_town | 502 | 123.1 | 9.4 | 27.4 | 91.7 | |
| 09-29 19:29 | battle_terrain_030 | 430 | 116.9 | 9.5 | 100.8 | 46.8 | FRAME_CAP |
| 09-29 19:22 | battle_terrain_biome_148 | 371 | 117.5 | 9.3 | 65.6 | 40.9 | FRAME_CAP |
| 10-02 14:30 | battle_terrain_biome_075 | 231 | 255.7 | 4.5 | 52.5 | 61.3 | |
| 09-30 05:47 | battle_terrain_biome_053 | 207 | 192.6 | 9.8 | 36.5 | 46.3 | |
| 10-02 11:38 | battle_terrain_biome_148 | 165 | 120.7 | 9.1 | **913.5** | 31.2 | MEMORY_PRESSURE |
| 10-02 11:38 | battle_terrain_biome_148 | 164 | 116.1 | 9.4 | 52.6 | 209.2 | FRAME_CAP, MEMORY_PRESSURE |
| 10-02 11:38 | battle_terrain_biome_148 | 83 | 117.0 | 9.1 | **869.8** | 30.5 | FRAME_CAP, MEMORY_PRESSURE |
| 09-30 12:41 | taom_rohan_edoras_town | 47 | 117.1 | 9.2 | 18.2 | 43.8 | FRAME_CAP |
| 09-29 08:29 | battle_terrain_biome_046 | 4 | 116.9 | 9.3 | 20.2 | 26.4 | FRAME_CAP, MEMORY_PRESSURE |

Four more missions logged no steady window. Reading: on this machine the largest battle on disk (1,184
agents) ran at a median 103.5 fps; most sessions sit on a frame cap near 117 fps outside the engine
(two ran uncapped at 193 and 256 fps), so a CPU gain is invisible at the median until a run is
uncapped; the only long frames are the two 0.87 and 0.91 s hitches of 2026-10-02 (REPORT.md "The
battle hitches"). A comparison for any plan uses the same scene, agent count and cap, through
`perf_runs.py compare`.

## Campaign map frame rate in the sessions on disk

From the `[MapLoad]` heartbeat (5-second windows with the map on top and no loading window; script
`evidence/native/map_heartbeat.py`), 18 sessions:

| Time control | Windows | fps median | fps p10 | `Campaign.RealTick` ms per window (an average), median / p90 / max | Parties |
|---|---|---|---|---|---|
| Paused (Stop) | 440 | 133.1 | 115.2 | 0.30 / 0.30 / 5.80 | 2,033 to 3,114 |
| Fast forward | 648 | 169.8 | 111.1 | 0.30 / 0.40 / 2.40 | 2,039 to 3,146 |

No window with time at normal speed (Play) met the filter. The heartbeat's `tickMs` times only the
`Campaign.RealTick` call it brackets (`Campaign_RealTick_MapLoad_Patch.cs`), so it is not the whole
campaign simulation; plan 039's profiler is what attributes the map frame. On this machine the map is
not frame-bound; a slower CPU is where plans 037 and 039 matter.

Each window's `tickMs` is the window's average (`MapLoadHeartbeatService.TickMsAverage`, the sum over
the window's frames), so the "max" column is the worst window average, not the worst tick. A single slow
tick, such as a day rollover running every daily handler, is diluted by the 600 to 850 frames of its
window and does not show. Whether TAOM's hourly and daily handlers cause a map hitch is therefore
unmeasured: plan 039 records `maxFrameMs` per window, the first measurement of single slow map frames,
and its "Deferred" list names per-listener timing of the periodic events, which plan 040's listener
swap could extend to (FOR-MIKE 16k).
