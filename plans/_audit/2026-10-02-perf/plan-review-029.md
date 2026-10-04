# Plan review 029, round 1 (cold)

Plan: `plans/029-perf-runs-parser.md`, read at worktree HEAD `e9cd8b39`. The two commits after
`dffdf879` touch no path in the plan's drift check (`git diff --stat dffdf879..HEAD -- <the eleven
paths>` printed nothing), so every excerpt was compared against code identical to `dffdf879`.
No earlier `plan-review-029*.md` existed.

## What I ran (evidence)

- Lifted the plan's three code blocks (Step 3 test file, Step 4 tool, Step 5 mutation script) into
  scratch with a script; the real `tools/triage_battle_load.py` was imported from the worktree.
- `python -B -m unittest tools.tests.test_perf_runs` with the Step 4 tool: `Ran 43 tests` then `OK`.
- Without the tool (Step 3 RED): `ModuleNotFoundError: No module named 'perf_runs'`, `Ran 1 test`,
  `FAILED (errors=1)`. Matches plan line 874-875.
- Mutation script (only change: PYTHONPATH also carried the worktree's `tools/`, because my scratch
  "worktree" held no triage module): all seven `KILLED`, `mutants killed: 7 of 7`.
- `python -B tools/perf_runs.py` with no arguments: argparse usage, `exit=2`.
- ASCII check on both Python files: no match.
- Step 6 on the real `taom_debug_2026-10-02_11-38-06.log`: `rows: 4  malformed lines skipped: 0`,
  `windows=5/0`, `14/9`, `25/20`, `25/20`, `FRAME_CAP` on rows 2 and 3,
  `MEMORY_PRESSURE,DIRTY_BUILD,BUILD_PAIR_MISMATCH` on all four, exit 0. Exactly plan lines 1622-1624.
  Also clean on the 09-29 campaign log (2 rows) and the 14-30-27 log (1 row, 255.7 fps, no FRAME_CAP).
- `python -B -m unittest tools.tests.test_triage_battle_load`: `Ran 153 tests`, `OK`.
- `python -B tools/lint_docs.py --fail-on-drift` at the base: exit 0,
  "Em/en dashes in newly written prose: **0**".
- Not run (read-only role): dotnet. The dotnet baseline totals are UNVERIFIED by this review.

## Blocking

1. **The "shared" literal contract with plan 028 is not shared** (plan lines 21-22, 420-435,
   1848-1851). Plan 028 (`plans/028-mission-tick-profiler.md:399-404`) gives three different
   literals ("use these exact strings in `TickProfileLinesTests`; plan 029 pins the same"), e.g.
   `[TickProfile] ... preDisplayMs=12.50 missionTickMs=812.40 ... top=BehaviorTreeMissionLogic:410.20/300/3.10/512,...`
   and `[PerfContext] ... missionInProcess=1 scene=battle_terrain_029 ... diag=battleLoad,...,missionPerf`.
   Plan 029's `PINNED_TICK_PROFILE`, `PINNED_HITCH`, `PINNED_PERF_CONTEXT` differ in every number,
   and its test comment (line 421) says 028 "must assert these same literals". Whichever plan lands,
   the comment and the twin-pin claim ship false. (028's three literals do parse with 029's parser:
   checked; the hitch's dominant phase would be `agentTickMs`.) Fix before dispatch: adopt 028's
   three literals as the PINNED_* values and recompute every oracle that reads them
   (`test_pinned_*`, `TickAndHitchTests`, `test_memory_pressure_from_perf_context`,
   `test_perf_context_*`, `CompareTests`), or change 028; and drop the Step 2 CS_* branch once both
   sides hold one literal.
2. **`DIAG_ON` will fire on every measured row once 028 lands** (plan lines 1331-1332, 704-708,
   1707). 028's `diag=` lists the enabled diagnostics, `missionPerf` among them
   (`028:404`, `028:711` `DiagTokens(..., bool missionPerf)`), and the heartbeat toggle defaults on
   (`Main/Features/BattleLoadDiagnostics/BattleLoadDiagnosticsSettings.cs:61`
   `EnableMissionPerfHeartbeat { get; set; } = true;`). Any mission with `[MissionPerf]` windows
   therefore has a non-empty `diag`, so the flag carries no information. The test's clean case
   `diag=none` (line 435) never occurs in a log this tool can measure. Define DIAG_ON on tokens
   beyond the measuring baseline (at least ignore `missionPerf`; decide whether `battleLoad` and
   `memSampler` count), and test it with 028's real literal.
3. **The pre-cleared "028 landed first" path breaks the fixed dotnet totals** (lines 10-14 allow
   it; lines 267, 356, 1751-1752, 1804 demand exactly `Failed: 1, Passed: 12345, Skipped: 2,
   Total: 12348`). 028 adds about a dozen C# test classes (`028:465-468`), so on that path Step 9 and
   the Done criteria fail and a weak executor STOPs. State the totals as "Step 1's recorded totals,
   unchanged" and let Step 1 accept 028's added tests when the drift check showed 028.
4. **The stale-claim greps match the plan itself once plans are committed** (Step 7 Verify line
   1659, Step 8 Verify line 1742, Done criterion line 1807). `plans/` is tracked (146 files; plans
   are committed before execution, e.g. `7f02fc8d`, `dd628416 ... plan 027 started`), and
   `plans/029-perf-runs-parser.md` contains both strings (lines 36, 47 area, 1659, 1742, 1807). Today
   it is untracked, so `git grep` passes; committed, it fails. Earlier plans exclude it:
   `plans/016-repo-hygiene-pins-readme.md:637` uses `-- ':!plans'`. Add that pathspec to all three.

## Non-blocking

1. Step 2's "028 landed" branch (lines 365-382) never names where 028's literals live:
   `TAOM.Tests/Features/MissionPerf/TickProfileLinesTests.cs`, methods
   `BuildTickProfile_SampleWindow_MatchesThePinnedLiteral`, `BuildHitch_SampleFrame_MatchesThePinnedLiteral`,
   `BuildPerfContext_SampleContext_MatchesThePinnedLiteral` (`028:690-693`). Nor does it tell the
   executor to rewrite the comment at line 420-421 and the commit body's `Not-tested:` trailer
   (lines 334-335), both false on that path. Moot if blocking 1 is fixed by unifying the literals.
2. "refusing groups that ran different builds" (line 47) and "compare refuses runs of different
   builds" (commit body, lines 328-329): the refusal reads only `[PerfContext] build=` (Debug or
   Release) and `textureQuality`, never the commit SHA, and an A/B of a code change always compares
   different SHAs. Say "Debug and Release builds".
3. MEMORY_PRESSURE attribution: `split_missions` appends every line after a mission opens
   (line 1207-1208), so `[MemSample]` lines written on the campaign map after a battle, until the next
   `MissionOpenNew`, count toward that battle. The doc row (line 1706) says "in the mission". Either
   cut MemSamples at the last window or word the doc as "after the mission opened".
4. Done criterion line 1809 checks `git status` for the six files, but after Step 10's commit they no
   longer appear; say "before the commit" or check `git show --stat HEAD` only.
5. Status line 29-30 leaves the Python suite baseline unrecorded; the template asks for failing names
   at the planned-at commit. Step 1 covers it, so this is a gap in the record, not in execution.
6. Line 98 refers to "the brief", which the executor never sees; harmless but confusing.
7. Line 1610 uses `<scratch>` for the mutation script, while the dispatch rules put scratch under
   `<scratch>\<label>\`; harmless.

## Excerpt mismatches (all content matched; these are line markers)

- Line 58: the `[MemSample]` twin-pin comment is at `tools/tests/test_triage_battle_load.py:45-48`,
  not 46-50 (line 44 is blank; 49-50 continue `PINNED_MEM_PERIODIC`).
- Line 135: `PhaseEvent` (`tools/triage_battle_load.py:293-299`) also has a `slots` field at 299;
  the plan lists four fields. Content otherwise matches.
- Everything else checked matched at `dffdf879`: `MissionPerfLine.cs:5-18` and `:7`;
  `FrameStatsTests.cs:133-143` and `:138` (10 `[TestMethod]`, no DataRow);
  `MissionPerfHeartbeatBehavior.cs:44, 52-53, 69-78, 86, 107-111`; `FrameStats.cs:56-65`;
  `docs/features/mission-perf-heartbeat.md` 68 lines, line 47, lines 58-68;
  `tools/README.md` lines 72-80, 82, 84, one `## Content Generation`, no `triage_battle_load` row;
  `triage_battle_load.py` 1,413 lines, `parse_battle_load_log` at 374, `classify_phase_timings` at
  890, `_BUCKETS` at 247-260, `_MISSION_START_PHASES`; `BuildStampReport.cs:55, 84-86, 149`;
  the four `MissionPerfLine` grep hits; `git grep -n MissionPerf -- tools` empty.

## Checklist

- TDD order: Python RED (import failure) then GREEN, then a mutation step proving each guard; C#
  edits are comments only with the existing pinned test named. Acceptable.
- Issue line: "filed by the orchestrator before execution", listed under Orchestrator steps.
- ADRs: 002, 007, 008, 009 named with one line each; rules `moduledata-validation.md`,
  `csharp-architecture.md`, `tests.md` named; their `paths:` scopes match.
- Protected files: none touched; no Step 0 needed. Single-owner files listed out of scope.
- STOP conditions: specific to this plan (triage reuse, boundary misses, 028 key drift, mutants).
- Drift-check paths include all six in-scope paths plus read-only references.
- Non-deploying dotnet commands carry `-p:DisableModuleCopy=true -p:ModuleId=`; no `./build.ps1`.
- No worktree path, branch name or local absolute path; no CHANGELOG step (out of scope, line 300).
- Dashes: only at lines 207 and 213, both inside code spans quoting the C# source and a log line
  (exempt). No secret values.
