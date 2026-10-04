# Plan 029: Turn taom_debug logs into per-mission perf rows and A/B comparisons

> **Executor instructions**: Follow this plan step by step. Run every verification command and
> confirm the expected result before moving on. If anything in "STOP conditions" occurs, stop and
> report; do not improvise. Work in the worktree and on the branch you were given. The orchestrator
> keeps `plans/README.md`; do not edit it.
>
> **Drift check (run first)**:
> `git diff --stat dffdf879..HEAD -- tools/perf_runs.py tools/tests/test_perf_runs.py tools/triage_battle_load.py tools/tests/test_triage_battle_load.py tools/README.md Main/Features/MissionPerf/MissionPerfLine.cs Main/Features/MissionPerf/FrameStats.cs Main/Features/MissionPerf/Hooks/MissionPerfHeartbeatBehavior.cs TAOM.Tests/Features/MissionPerf/FrameStatsTests.cs Main/Core/Diagnostics/BuildStampReport.cs docs/features/mission-perf-heartbeat.md`
> Expected: no output. One drift is pre-cleared: plan 028 (the mission tick profiler) is written in
> parallel and may land first. If the only changes are 028's (new files or lines for `[TickProfile]`,
> `[Hitch]`, `[PerfContext]`, a changed sentence at `docs/features/mission-perf-heartbeat.md:37-39`,
> a new section in that doc), proceed, provided every excerpt in "Current state" still matches the
> live file. Any other change to an in-scope file, or any excerpt mismatch: STOP and report. When
> 028 has landed, its new C# test classes raise the dotnet totals: Step 1 records the totals of
> your base, and every later dotnet check compares against Step 1, not against "Baseline at that
> commit".

## Status

- **Priority**: P1
- **Effort**: M
- **Risk**: LOW (a new offline tool, two comment edits, docs; no runtime code changes)
- **Depends on**: none. It shares plan 028's line contract and its three literal pins, character
  for character (`plans/028-mission-tick-profiler.md`, "The three literal pins"; copied below);
  028 may land before or after this plan.
- **Category**: perf
- **Planned at**: commit `dffdf879`, 2026-10-02
- **Baseline at that commit**: dotnet `Failed! - Failed: 1, Passed: 12345, Skipped: 2, Total: 12348`
  (net472), failing: `EveryLanguage_DeclaresARowForEveryEnglishKey`
  (`TAOM.Tests/Infrastructure/Localization/LanguageFileCoverageTests.cs:157`; English keys without
  rows in other languages; the paid translator run waits on the maintainer). Python suite: not yet
  recorded for this run; you record it in Step 1 before any edit. At planning time
  `python -B -m unittest tools.tests.test_triage_battle_load` gave `Ran 153 tests` and `OK`.
- **Issue**: filed by the orchestrator before execution

## Why this matters

Every performance plan in this run is judged by `[MissionPerf]` numbers, and nothing reads them:
`git grep -n MissionPerf -- tools` finds no file, although `Main/Features/MissionPerf/MissionPerfLine.cs:7`
says "the parser in `tools/` has a single fixture to match". The one log reader that exists,
`tools/triage_battle_load.py`, triages the LAST mission of a log only, so four battles in one
session give one answer. The 2026-10-02 logs also show why raw numbers mislead: most windows sat
at 116 to 117 fps across battles of 83 to 273 agents, the signature of a frame limiter rather
than of load; one session's `[MemSample]` lines read `memLoad` 81 to 84% while another's read 45
to 48%; and `BattlePlayable` fires with `agents=0`, so the first `[MissionPerf]` window holds the
spawn burst (`gc0` 21 to 41 in the first window of the 2026-10-02 custom battles, 2 to 19 in later
windows). After this plan, `python tools/perf_runs.py <logs>` prints one row per mission
with spawn and steady-state numbers apart and confounder flags beside them, and
`perf_runs.py compare --a ... --b ...` prints per-metric medians, deltas and percentages for an
A/B, refusing groups that mix Debug and Release builds or texture settings.

## Current state

All excerpts are from `dffdf879`.

### Files and their roles

- `tools/triage_battle_load.py` (1,413 lines): the battle-load hang triager. Its parser is the one
  this plan reuses for `[BattleLoad]` and `[MemSample]`. **Not edited by this plan.**
- `tools/tests/test_triage_battle_load.py`: its tests (153 at planning time). **Not edited.**
  Lines 45-48 are the twin-pin convention this plan copies:
  ```python
  # The pinned [MemSample] log-line contract (issue #386). These literals are the
  # cross-language twin pin: MemoryPressureSamplerTests.FormatSample_KnownValues_
  # MatchesContractLine asserts the C# sampler emits EXACTLY these message bodies.
  PINNED_MEM_SESSION = "[MemSample] session totalPhysMB=16296 sysCommitLimitMB=31646"
  ```
- `Main/Features/MissionPerf/MissionPerfLine.cs`: the `[MissionPerf]` format. Lines 5-18:
  ```csharp
  /// <summary>
  /// The one line format, kept pure so the heartbeat behavior stays a thin reader of the engine
  /// and the parser in <c>tools/</c> has a single fixture to match.
  /// </summary>
  public static class MissionPerfLine
  {
      public static string Build(double tSeconds, FrameWindow window, int agents, int activeAgents, int formations, int gc0, int gc1, int gc2)
      {
          return string.Format(CultureInfo.InvariantCulture,
              "[MissionPerf] t=+{0:0}s frames={1} fps={2:0.0} avgMs={3:0.00} p95Ms={4:0.00} maxMs={5:0.0} agents={6} active={7} formations={8} gc0={9} gc1={10} gc2={11}",
              tSeconds, window.Frames, window.Fps, window.AverageMs, window.P95Ms, window.MaxMs,
              agents, activeAgents, formations, gc0, gc1, gc2);
      }
  }
  ```
- `TAOM.Tests/Features/MissionPerf/FrameStatsTests.cs`: **the C# twin of the `[MissionPerf]` pin
  already exists**, lines 133-143:
  ```csharp
      [TestMethod]
      public void BuildLine_FormatsEveryFieldInvariantly()
      {
          var window = new FrameWindow(frames: 300, seconds: 5.0, averageMs: 16.6667, p95Ms: 25.5, maxMs: 40.25);

          var line = MissionPerfLine.Build(tSeconds: 65.0, window, agents: 812, activeAgents: 640, formations: 9, gc0: 12, gc1: 3, gc2: 1);

          Assert.AreEqual(
              "[MissionPerf] t=+65s frames=300 fps=60.0 avgMs=16.67 p95Ms=25.50 maxMs=40.3 agents=812 active=640 formations=9 gc0=12 gc1=3 gc2=1",
              line);
      }
  ```
  So this plan adds no C# test (a new `MissionPerfLineTests.cs` would duplicate this one): it adds
  the Python half of the pin and a comment on this test naming it.
- `Main/Features/MissionPerf/Hooks/MissionPerfHeartbeatBehavior.cs`: writes the line. What `t=`
  means (lines 44, 52-53, 69-78, 107-111): `OnCreated` calls `Reset()`, which sets
  `_missionStart = Stopwatch.GetTimestamp()`; each `OnMissionTick` computes
  `nowSeconds = (now - _missionStart) / Frequency` and logs `MissionPerfLine.Build(nowSeconds, ...)`
  when `_stats.ShouldEmit(nowSeconds)`. `Main/Features/MissionPerf/FrameStats.cs:56-65`: the first
  `ShouldEmit` call opens the window and returns false; it returns true once 5 s have passed. So
  `t` restarts near zero in every mission, and the first line lands 5 s after the first tick. Line
  86 writes the one non-window line with the tag:
  `_logger.LogError($"[MissionPerf] heartbeat disabled for this mission after {ex.GetType().Name}: {ex.Message}")`.
- `docs/features/mission-perf-heartbeat.md` (68 lines): the feature doc. Line 47 is stale:
  "`t` is seconds since the mission was created; the first line lands at +5 s." Every real log
  shows the first line at `t=+6s` (custom and campaign battles) or `t=+7s` (a tournament). Lines
  58-68 today:
  ```markdown
  ## Tests

  `TAOM.Tests/Features/MissionPerf/FrameStatsTests.cs`: cadence, average, nearest-rank p95,
  single sample, empty window (zeros, not NaN), window reset, bounded sample set, clock reset,
  line format.

  ## Reading an A/B

  Take the lines from 30 s after both sides are AI-controlled (F6) to the first rout, compare the
  median `avgMs` and `p95Ms` between the two runs; `gc2` should not climb faster with the feature
  on. Three runs per cell is the floor, AI battles vary.
  ```
- `tools/README.md`: the tools catalogue. `triage_battle_load.py` has no row in it; the
  "Save-game diagnostics & recovery" section (lines 72-80) is followed by `---` (line 82) and
  `## Content Generation` (line 84). This plan adds a "Performance logs" section between them.

### The triage functions this plan reuses (import, never copy)

`tools/triage_battle_load.py`, public names and signatures at `dffdf879`:

- `def parse_battle_load_log(text: str) -> Timeline:` (line 374). Strips the FileLogger prefix,
  returns `Timeline(events=[PhaseEvent...], mem_samples=[MemSample...], ...)`. `PhaseEvent`
  (lines 293-299) has `seq`, `ms`, `phase`, `detail`, `slots`; `MemSample` (lines 312-324) has `mem_load`
  (an int, the `memLoad=N%` value).
- `def classify_phase_timings(tl: Timeline) -> dict | None:` (line 890). Returns
  `{"buckets": [{"name", "from", "to", "ms", "what"}...], "dominant": ..., ...}` or None. It anchors
  on the LAST mission-start marker in the timeline (lines 895-910, via `_last_mission_start_index`,
  which counts `MissionInitialize`, `MissionOpenNew` and `EncounterStart`) and returns None when no
  `MissionInitialize` follows that marker. The six buckets (lines 247-260) are `bucket1`
  MissionInitialize to MissionInitializeDone, `bucket2` to FinishMissionLoadingBegin, `bucket3a` to
  MissionAfterStartBegin, `bucket3b` to MissionAfterStartDone, `bucket3c` to
  FinishMissionLoadingDone, `bucket4` to BattlePlayable.

Per-mission reuse needs **no change** to `triage_battle_load.py`: `perf_runs.py` passes each
mission's own text to `parse_battle_load_log` and the resulting timeline to
`classify_phase_timings`. One trap, verified on a real log: a campaign session writes an
`EncounterStart` for an encounter that never opens a mission, and it lands after the previous mission's last line. If that line were inside the previous mission's text,
`classify_phase_timings` would anchor on it, find no `MissionInitialize` after it and return None,
wiping that mission's buckets. So the tool passes only the load part of each mission (its lines up
to and including the first `phase=BattlePlayable`). The test
`test_orphan_encounter_between_missions_keeps_the_previous_load_buckets` pins this.

### What the real logs say about mission boundaries

The writer read the ten logs dated 2026-09-29 to 2026-10-02 that carry `[MissionPerf]` lines (in
the game's `bin/Win64_Shipping_Client/Logs/` folder; these are leads for you, not files you need).
Phase names are those of `Main/Features/BattleLoadDiagnostics/Domain/BattleLoadPhase.cs`.

- Every mission, of every kind seen (`CustomBattle`, campaign `Battle`, `TournamentFight`,
  `TownCenter`, `CustomSiegeBattle`), starts with exactly one
  `[BattleLoad] seq=N t=+Nms phase=MissionOpenNew mission='<kind>' scene='<id>' ...`. Example
  (custom battle, `taom_debug_2026-10-02_11-38-06.log` lines 603-666, shortened):
  ```
  [2026-10-02 11:43:27] [INFO] [BattleLoad] seq=1 t=+0ms phase=MissionOpenNew mission='CustomBattle' scene='battle_terrain_biome_148'
  [2026-10-02 11:43:28] [INFO] [BattleLoad] seq=4 t=+501ms phase=MissionInitialize scene='battle_terrain_biome_148' gc=1369/448/75 heapMB=128 privMB=8779 wsMB=4764
  [2026-10-02 11:43:29] [INFO] [BattleLoad] seq=6 t=+1247ms phase=FinishMissionLoadingBegin polls=22 waitMs=745 gc=1369/448/75 heapMB=129 privMB=8663 wsMB=5033
  [2026-10-02 11:43:30] [INFO] [BattleLoad] seq=47 t=+2857ms phase=BattlePlayable scene='battle_terrain_biome_148' agents=0 gc=1376/450/75 heapMB=143 privMB=9791 wsMB=5156
  [2026-10-02 11:43:35] [INFO] [MissionPerf] t=+6s frames=508 fps=101.6 avgMs=9.84 p95Ms=9.66 maxMs=624.6 agents=273 active=273 formations=4 gc0=41 gc1=5 gc2=0
  [2026-10-02 11:43:40] [INFO] [MissionPerf] t=+11s frames=586 fps=117.0 avgMs=8.55 p95Ms=9.33 maxMs=14.4 agents=273 active=273 formations=4 gc0=6 gc1=0 gc2=0
  ```
  A campaign battle adds fields: `phase=MissionOpenNew mission='Battle' scene='battle_terrain_biome_046' encountered='Wild Trolls' side=Defender ...`.
- `MissionInitialize` is **not** a usable boundary: one `CustomSiegeBattle` load
  (`taom_debug_2026-09-30_12-41-24.log`) logged it 731 times without ever reaching `BattlePlayable`.
- `EncounterStart` is **not** a usable boundary: it precedes a campaign mission's `MissionOpenNew`,
  and also appears for encounters that open no mission (`taom_debug_2026-09-29_08-29-26.log` line
  933).
- In the 11-38 log, `ResourceClearOldBegin`/`Done` appear when a mission is left, before the next
  `MissionOpenNew` (lines 887-888), so they fall in the tail of the mission before.
- The first `[MissionPerf]` window is `t=+6s` (custom and campaign battles) or `t=+7s` (the
  tournament), never `t=+5s`, and `t` restarts in every mission. A "first window at `t=+5s`"
  boundary would therefore never match; the tool uses a clock rule instead: a window whose `t` does
  not advance with the FileLogger clock starts a new mission.
- Steady fps sits at 116 to 117 in most 2026-10-02 windows, and at 240 to 260 in one session
  (`taom_debug_2026-10-02_14-30-27.log`), so the frame limit moved between sessions.

The boundary rules the tool implements, in priority order:
1. A `[BattleLoad] ... phase=MissionOpenNew` line always starts a mission (`opened_by` =
   `MissionOpenNew`). `\bMissionOpenNew\b` must not match `MissionOpenNewDone`.
2. A `[PerfContext]` line (plan 028: written once per mission at its first tick) starts a mission
   unless the current mission has no `[PerfContext]` and no `[MissionPerf]` window yet.
3. A `[MissionPerf]` window starts a mission when none is open, or when the open mission already has
   a window and either `t` did not grow, or `t` grew by less than the FileLogger clock's growth
   minus 3 s (`CLOCK_SLACK_S`; a pause delays both clocks alike, so a resumed mission stays one
   row).

### The build-identity lines (dirty and MISMATCH are two facts)

A dirty build and the `[BuildStamp]` MISMATCH verdict are easy to conflate. The code says they are
two different facts:

- `Main/Core/Diagnostics/BuildStampReport.cs:149`: the startup line is
  `$"[BuildStamp] TAOM={mainVer} TAOM.Dependencies={depsVer}{verdict}"`.
- Lines 84-86: `MISMATCH` means the TAOM / TAOM.Dependencies **pair** was built more than 12 hours
  apart (`RebuildTolerance`, line 55): `$" MISMATCH — built {gap} apart. These modules were not built together; " + ...`
  (that dash is in the C# source; the tool tests it with `chr(0x2014)`).
- A dirty tree is the `.dirty` suffix on the SHA inside the TAOM stamp (`Directory.Build.props`
  lines 45-53 and 76, from plan 017).

A real line (11-38 log line 2) carries both:
`[BuildStamp] TAOM=v2.0.0.0 build.20261002-163736Z+bc39f6e4d0f55a70d4fbdc6b515b5439145ced21.dirty TAOM.Dependencies=v0.1.0.0 build.20261001-134226Z+6b00881b6db39cafd8f11933861400aca56085df.dirty MISMATCH — built 1d 02h 55m apart. ...`

So the tool raises `DIRTY_BUILD` when the TAOM token contains `.dirty`, and a separate
`BUILD_PAIR_MISMATCH` when the line contains ` MISMATCH `.

### The line contract shared with plan 028

Existing (pinned on both sides after this plan):
```
[MissionPerf] t=+65s frames=300 fps=60.0 avgMs=16.67 p95Ms=25.50 maxMs=40.3 agents=812 active=640 formations=9 gc0=12 gc1=3 gc2=1
```
New, defined by plan 028 (this plan must accept logs without them):
```
[TickProfile] t=+<s>s frames=<n> wallMs=<x> preDisplayMs=<x> missionTickMs=<x> preTickMs=<x> waitTickMs=<x> agentTickMs=<x> otherMs=<x> allocKB=<x|na> top=<Type>:<ms>/<calls>/<maxMs>/<KB>,<Type>:...
[Hitch] t=+<s>s frameMs=<x> preDisplayMs=<x> missionTickMs=<x> preTickMs=<x> waitTickMs=<x> agentTickMs=<x> otherMs=<x> gc0=<n> gc1=<n> gc2=<n> allocKB=<x|na> top=<Type>:<ms>,<Type>:<ms>,<Type>:<ms>
[PerfContext] build=<Debug|Release> jitOptimized=<true|false> clr=<version> serverGC=<bool> latency=<mode> missionInProcess=<n> scene=<id> agents=<n> textureQuality=<n|na> shadowQuality=<n|na> particleDetail=<n|na> ragdolls=<n|na> memLoad=<pct|na> availPhysMB=<n|na> tickProfiler=<on|off> diag=<list|none>
```
Plan 028 adds: numbers in invariant culture, ms with 2 decimals, KB integers, `Type` the
short type name, `top=` the top N behaviours by total ms (`top=none` when no behaviour ran). Every
line carries the FileLogger prefix `[YYYY-MM-DD HH:MM:SS] [LEVEL] `; the parser keys on the tag,
never the position. The parser reads these three lines as `key=value` tokens in any order, ignores
extra keys, accepts `na` as "not measured" (None, never 0), strips a trailing `%` from `memLoad`,
and counts a line missing a required key or holding a non-number where a number is required as
malformed.

**The three literal pins, shared with plan 028.** `plans/028-mission-tick-profiler.md` ("The three
literal pins") gives these exact strings for its C# test class
`TAOM.Tests/Features/MissionPerf/TickProfileLinesTests.cs` (methods
`BuildTickProfile_SampleWindow_MatchesThePinnedLiteral`,
`BuildHitch_SampleFrame_MatchesThePinnedLiteral`,
`BuildPerfContext_SampleContext_MatchesThePinnedLiteral`). This plan's `PINNED_TICK_PROFILE`,
`PINNED_HITCH` and `PINNED_PERF_CONTEXT` (Step 3) are the same three strings, character for
character:
```
[TickProfile] t=+65s frames=300 wallMs=5000.00 preDisplayMs=12.50 missionTickMs=812.40 preTickMs=40.10 waitTickMs=95.00 agentTickMs=1500.00 otherMs=4040.00 allocKB=2048 top=BehaviorTreeMissionLogic:410.20/300/3.10/512,AdvancedCombatBehavior:120.00/900/1.50/64
[Hitch] t=+72s frameMs=812.35 preDisplayMs=0.40 missionTickMs=5.20 preTickMs=0.30 waitTickMs=790.00 agentTickMs=795.10 otherMs=16.45 gc0=1 gc1=1 gc2=0 allocKB=96 top=BehaviorTreeMissionLogic:2.10,AdvancedCombatBehavior:1.30,MissionPerfHeartbeatBehavior:0.05
[PerfContext] build=Debug jitOptimized=false clr=4.0.30319.42000 serverGC=false latency=Interactive missionInProcess=1 scene=battle_terrain_029 agents=0 textureQuality=1 shadowQuality=2 particleDetail=1 ragdolls=3 memLoad=61 availPhysMB=12034 tickProfiler=on diag=battleLoad,stallWatchdog,stallBundle,exitSampler,freezeSampler,memSampler,missionPerf
```

**What `diag=` holds, and so what `DIAG_ON` means.** Plan 028 lists in `diag=` the toggles that
are on, from seven tokens: `battleLoad`, `stallWatchdog`, `stallBundle`, `exitSampler`,
`freezeSampler`, `memSampler`, `missionPerf`. All seven default on at `dffdf879`
(`Main/Features/BattleLoadDiagnostics/BattleLoadDiagnosticsSettings.cs` lines 21, 26, 31, 41, 46,
51, 61: `EnableBattleLoadDiagnostics`, `EnableStallWatchdog`, `EnableStallWatchdogBundle`,
`EnableExitStallSampler`, `EnableMissionTickStallSampler`, `EnableMemorySampler`,
`EnableMissionPerfHeartbeat`, each `= true`), and `missionPerf` is the heartbeat this tool reads, so
every measurable mission on a default install lists them. A flag that fired on any non-empty
`diag` would fire on every row. The tool therefore treats those seven as the measuring baseline
(`DIAG_BASELINE`) and raises `DIAG_ON` only for cost beyond it: `tickProfiler=on` (028's profiler
times every behaviour call; it defaults off) or a `diag=` token outside the seven (a diagnostic
added later).

### Conventions that bind this change

- `.claude/rules/moduledata-validation.md` loads on `tools/**/*.py`; its only relevant part is the
  `tools/README.md` "XML I/O convention", which does not apply (this tool writes nothing).
- Tools tests are stdlib `unittest`, discovered by `python -B -m unittest discover -s tools/tests -t .`
  and run in CI (`.github/workflows/python-tests.yml`, Python 3.14 on ubuntu). Model after
  `tools/tests/test_triage_battle_load.py`: synthetic logs in the real format, the tool imported
  through `sys.path.insert(0, <tools dir>)`, the CLI run through `subprocess` with
  `sys.executable`.
- `.claude/rules/csharp-architecture.md` and `.claude/rules/tests.md` load on the two C# files;
  this plan changes only XML doc comments there, so no architecture rule is exercised. ADR-002
  (entry points under 150 lines), ADR-007 (services take adapters, never sealed TaleWorlds types)
  and ADR-008 (service testability) are untouched: no service, adapter or entry point changes.
  ADR-009 (self-documenting code): the new comments say why the literal must not drift.
- AGENTS.md "Human prose": no em or en dash in docs, comments or the commit body. The Python files
  stay ASCII (`grep -nP "[^\x00-\x7F]"` finds nothing).

### Blast radius

Only comments change in C#. `git grep -n MissionPerfLine -- "*.cs"` at `dffdf879`:
`Main/Features/MissionPerf/Hooks/MissionPerfHeartbeatBehavior.cs:17` (a `<see cref>`), `:76` (the
call), `Main/Features/MissionPerf/MissionPerfLine.cs:9`, `TAOM.Tests/Features/MissionPerf/FrameStatsTests.cs:138`.
The code graph for the planning worktree was not built (`graphify_taom.py affected` said "no
graph"); see Step 1 for what you do.

## Commands you will need

| Purpose | Command | Expected on success |
|---|---|---|
| Build | `dotnet build Main/TAOM.csproj -p:DisableModuleCopy=true -p:ModuleId=` | exit 0, 0 errors |
| Tests | `dotnet test TAOM.Tests -p:DisableModuleCopy=true -p:ModuleId=` | Step 1's recorded totals line, unchanged, with the same failing tests (at `dffdf879`: `Failed! - Failed: 1, Passed: 12345, Skipped: 2, Total: 12348`, only `EveryLanguage_DeclaresARowForEveryEnglishKey` failing) |
| One test class | `dotnet test TAOM.Tests -p:DisableModuleCopy=true -p:ModuleId= --filter "FullyQualifiedName~FrameStatsTests"` | 10 tests run, all pass; a filter matching nothing proves nothing |
| New tool tests | `python -B -m unittest tools.tests.test_perf_runs` | `Ran 43 tests` then `OK` |
| Triage tests | `python -B -m unittest tools.tests.test_triage_battle_load` | `Ran 153 tests` then `OK` |
| Python tools | `timeout 900 python -B -m unittest discover -s tools/tests -t .` | Step 1's totals plus 43 tests, the same failure names |
| Data | `python tools/validate_moduledata.py` | not needed: no ModuleData changes |
| Docs | `python tools/lint_docs.py --fail-on-drift` | exit 0; "Em/en dashes in newly written prose: **0**" |

Both `-p:` flags go on build AND test; prefix every dotnet command with the `TEMP="<tmp>" TMP="<tmp>"`
your dispatch rules give, and give the tool call a 600000 ms timeout. Run every command from your
worktree root. Never `./build.ps1`: it deploys into the game install.

## Scope

**In scope** (the only files you create or modify):
- `tools/perf_runs.py` (new)
- `tools/tests/test_perf_runs.py` (new)
- `Main/Features/MissionPerf/MissionPerfLine.cs` (XML doc comment only, lines 5-8)
- `TAOM.Tests/Features/MissionPerf/FrameStatsTests.cs` (one XML doc comment above line 133)
- `docs/features/mission-perf-heartbeat.md` (line 47, the Tests paragraph, the "Reading an A/B" section)
- `tools/README.md` (a new "Performance logs" section)

**Out of scope** (do NOT touch, even though they look related):
- `tools/triage_battle_load.py` and `tools/tests/test_triage_battle_load.py`. The per-mission reuse
  needs no helper there (see Current state). If you find one is needed, STOP and report: its 153
  tests must stay green unchanged.
- `Main/Features/MissionPerf/FrameStats.cs`, `Hooks/MissionPerfHeartbeatBehavior.cs`: no runtime
  change. The first line landing at `t=+6s` is correct behaviour; only the doc sentence was wrong.
- Plan 028's C# twins for `[TickProfile]`, `[Hitch]`, `[PerfContext]`: 028 adds them.
- `docs/features/culture-doctrine.md` (its "Verification: the A/B protocol" could later point at the
  tool; a follow-up, not this plan).
- `docs/reference/feature-map.md`: no new feature; the tool hangs off the existing MissionPerf row.
- `Main/IoC.cs`, `Main/SubModule.cs`, `Main/TAOM.csproj`: nothing to register.
- `CHANGELOG.md` (generated at /release), `plans/README.md` (the orchestrator's).
- The gates themselves. Never turn a gate green by editing it: deleting or skipping a test,
  loosening an assertion, or adding an allowlist entry. STOP and report instead.

## Git workflow

- Commit on the branch you were given; never push or open a PR.
- Subject `<type>(<scope>): <version> - <description>`, at most 72 characters, `<version>` being the
  `<Version>` in `Main/_Module/SubModule.xml` when you commit (`v2.0.32` at planning time; a hook
  refuses any other). Suggested: `feat(perf): v2.0.32 - per-mission perf rows and A/B compare from logs`
  (69 characters).
- The body is the changelog entry, wrapped at 72, no AI attribution trailer. Write it to a file with
  the Write tool and run `git commit -F <file>`; stage the six in-scope paths by name. Draft body
  (re-check every claim against what you built before using it):
  ```
  tools/perf_runs.py turns any number of taom_debug logs into one row
  per mission and compares two groups of runs, so a frame-time or
  memory change is judged by numbers rather than by feel. Until now the
  [MissionPerf] heartbeat had no reader, and the one log tool,
  triage_battle_load.py, looks at the last mission of a log only.

  Each row carries the load buckets (through triage_battle_load.py's
  own parser), the spawn window apart from the steady state, steady
  medians of fps and of average and p95 frame time, the worst frame,
  GC collections per minute and, when the mission tick profiler's
  lines are present, per-behaviour cost and hitch counts. Flags say
  when the numbers may not mean what they seem: a frame cap, memory
  pressure, diagnostics beyond the default set, a dirty build, or a
  TAOM.Dependencies pair built apart. compare refuses to mix Debug and
  Release builds or texture settings.

  The [MissionPerf] line is now pinned on both sides: the C# test and
  the Python test assert the same literal.

  Not-tested: the [TickProfile], [Hitch] and [PerfContext] pins have no
  C# twin until the mission tick profiler lands.
  ```
  If Step 2 found plan 028 landed, delete the last paragraph (the `Not-tested:` trailer): its C#
  twins then exist. Check: `awk 'length > 72' <file>` prints nothing.

## Steps

### Step 1: record the base

1. Run the drift check above.
2. `TEMP="<tmp>" TMP="<tmp>" dotnet test TAOM.Tests -p:DisableModuleCopy=true -p:ModuleId=` (600000 ms
   timeout). Record the totals line and the failing test names.
3. `timeout 900 python -B -m unittest discover -s tools/tests -t . > "<your scratch folder>/py-base.log" 2>&1`,
   then read the `Ran N tests` line and the `OK` or `FAILED (...)` line, and list every `FAIL:` and
   `ERROR:` name (`grep -E "^(FAIL|ERROR):" "<your scratch folder>/py-base.log"`). This is the
   Python baseline for this run: report it. (`<your scratch folder>` is `<scratch>\<your label>\`
   from your dispatch rules, here and in every later step.)
4. `python -B -m unittest tools.tests.test_triage_battle_load` and record its totals.
5. Record `git status --porcelain` (the orchestrator may have placed `plans/` files there).
6. Blast radius: run `python tools/graphify_taom.py affected "MissionPerfLine" --depth 2`. If it
   prints "no graph at ...", record that and do not build one (the C# edits are comments; the
   blast radius is the four grep hits in Current state). Otherwise quote its output in your report.

**Verify**: the dotnet totals match "Baseline at that commit", or, when the drift check showed
plan 028 landed, they exceed it only by 028's new passing tests (`Failed: 1` and the same single
failing test still hold); either way the line you recorded is the one Step 9 and the Done criteria
hold you to. The triage line is `Ran 153 tests` and `OK`. A difference you cannot explain is a STOP.

### Step 2: check the three shared pins against plan 028 (if it landed)

Run `git grep -n -e "\[TickProfile\]" -e "\[Hitch\]" -e "\[PerfContext\]" -- Main TAOM.Tests`.

- **No output** (028 not on your base): nothing to compare. Step 3's three literals are the ones
  plan 028 will assert; keep the commit body's `Not-tested:` trailer.
- **Output** (028 landed): open `TAOM.Tests/Features/MissionPerf/TickProfileLinesTests.cs` and read
  the expected string in `BuildTickProfile_SampleWindow_MatchesThePinnedLiteral`,
  `BuildHitch_SampleFrame_MatchesThePinnedLiteral` and
  `BuildPerfContext_SampleContext_MatchesThePinnedLiteral` (join any C# string pieces split across
  lines). Each must equal, character for character, the matching literal in "The three literal
  pins" (Current state), which Step 3 writes as `PINNED_TICK_PROFILE`, `PINNED_HITCH` and
  `PINNED_PERF_CONTEXT`. If all three are equal, delete the `Not-tested:` paragraph from the commit
  body (Git workflow). If any differs, STOP and report both strings: the two plans agreed on these
  literals, and one side changes by decision, not by you.

**Verify**: you can state which branch you took, and on the 028 branch that all three strings are
equal.

### Step 3: write the failing tests (RED)

Create `tools/tests/test_perf_runs.py` with exactly this content (Write tool, never a heredoc):

```python
#!/usr/bin/env python3
"""Unit tests for the mission perf-row tool (tools/perf_runs.py).

Run:  python -B -m unittest tools.tests.test_perf_runs

Pure stdlib with synthetic taom_debug logs built in the REAL line formats: the FileLogger prefix
`[ts] [LEVEL]`, the [BattleLoad] load markers copied from a 2026-10-02 custom battle, and the
[MissionPerf] heartbeat. No game install needed.
"""
import json
import os
import subprocess
import sys
import tempfile
import unittest
from datetime import datetime, timedelta
from pathlib import Path

sys.path.insert(0, os.path.dirname(os.path.dirname(os.path.abspath(__file__))))

import perf_runs as pr  # noqa: E402

TOOL = Path(__file__).resolve().parent.parent / "perf_runs.py"

# The pinned [MissionPerf] contract. Cross-language twin pin: FrameStatsTests.
# BuildLine_FormatsEveryFieldInvariantly asserts MissionPerfLine.Build emits EXACTLY this body.
PINNED_MISSION_PERF = ("[MissionPerf] t=+65s frames=300 fps=60.0 avgMs=16.67 p95Ms=25.50 "
                       "maxMs=40.3 agents=812 active=640 formations=9 gc0=12 gc1=3 gc2=1")

# The mission tick profiler's three lines. Cross-language twin pin: TickProfileLinesTests.
# BuildTickProfile_SampleWindow_, BuildHitch_SampleFrame_ and BuildPerfContext_SampleContext_
# MatchesThePinnedLiteral assert the C# lines are EXACTLY these literals. Change both or neither.
PINNED_TICK_PROFILE = ("[TickProfile] t=+65s frames=300 wallMs=5000.00 preDisplayMs=12.50 "
                       "missionTickMs=812.40 preTickMs=40.10 waitTickMs=95.00 "
                       "agentTickMs=1500.00 otherMs=4040.00 allocKB=2048 "
                       "top=BehaviorTreeMissionLogic:410.20/300/3.10/512,"
                       "AdvancedCombatBehavior:120.00/900/1.50/64")
PINNED_HITCH = ("[Hitch] t=+72s frameMs=812.35 preDisplayMs=0.40 missionTickMs=5.20 "
                "preTickMs=0.30 waitTickMs=790.00 agentTickMs=795.10 otherMs=16.45 gc0=1 gc1=1 "
                "gc2=0 allocKB=96 top=BehaviorTreeMissionLogic:2.10,AdvancedCombatBehavior:1.30,"
                "MissionPerfHeartbeatBehavior:0.05")
PINNED_PERF_CONTEXT = ("[PerfContext] build=Debug jitOptimized=false clr=4.0.30319.42000 "
                       "serverGC=false latency=Interactive missionInProcess=1 "
                       "scene=battle_terrain_029 agents=0 textureQuality=1 "
                       "shadowQuality=2 particleDetail=1 ragdolls=3 memLoad=61 "
                       "availPhysMB=12034 tickProfiler=on diag=battleLoad,stallWatchdog,"
                       "stallBundle,exitSampler,freezeSampler,memSampler,missionPerf")
DEFAULT_DIAG = ("battleLoad", "stallWatchdog", "stallBundle", "exitSampler", "freezeSampler",
                "memSampler", "missionPerf")

BASE = datetime(2026, 10, 2, 12, 0, 0)
SCENE = "battle_terrain_biome_148"


def _at(seconds):
    return (BASE + timedelta(seconds=seconds)).strftime("%Y-%m-%d %H:%M:%S")


def _line(payload, at=0, level="INFO"):
    """One FileLogger line: [ts] [LEVEL] <payload>."""
    return f"[{_at(at)}] [{level}] {payload}"


def _perf(t, fps=117.0, avg=8.55, p95=9.20, mx=14.0, agents=100, active=None,
          gc=(3, 0, 0), frames=None):
    frames = int(round(fps * 5)) if frames is None else frames
    active = agents if active is None else active
    return (f"[MissionPerf] t=+{t}s frames={frames} fps={fps:.1f} avgMs={avg:.2f} "
            f"p95Ms={p95:.2f} maxMs={mx:.1f} agents={agents} active={active} formations=2 "
            f"gc0={gc[0]} gc1={gc[1]} gc2={gc[2]}")


def _load(start, scene=SCENE, kind="CustomBattle"):
    """A completed load, copied from a 2026-10-02 custom battle (seq numbers trimmed)."""
    return [
        _line(f"[BattleLoad] seq=1 t=+0ms phase=MissionOpenNew mission='{kind}' scene='{scene}'", start),
        _line(f"[BattleLoad] seq=4 t=+500ms phase=MissionInitialize scene='{scene}' "
              "gc=1369/448/75 heapMB=128 privMB=8779 wsMB=4764", start),
        _line(f"[BattleLoad] seq=5 t=+502ms phase=MissionInitializeDone scene='{scene}' "
              "gc=1369/448/75 heapMB=128 privMB=8783 wsMB=4764", start),
        _line("[BattleLoad] seq=6 t=+1247ms phase=FinishMissionLoadingBegin polls=22 waitMs=745 "
              "gc=1369/448/75 heapMB=129 privMB=8663 wsMB=5033", start + 1),
        _line("[BattleLoad] seq=7 t=+1442ms phase=MissionAfterStartBegin", start + 1),
        _line("[BattleLoad] seq=44 t=+1832ms phase=MissionAfterStartDone", start + 1),
        _line("[BattleLoad] seq=45 t=+1983ms phase=FinishMissionLoadingDone "
              "gc=1375/450/75 heapMB=141 privMB=8812 wsMB=5138", start + 1),
        _line(f"[BattleLoad] seq=47 t=+2857ms phase=BattlePlayable scene='{scene}' agents=0 "
              "gc=1376/450/75 heapMB=143 privMB=9791 wsMB=5156", start + 2),
    ]


def _windows(start, specs):
    """Heartbeat lines; each spec is a dict of _perf arguments. The FileLogger clock runs with t."""
    return [_line(_perf(**s), start + 1 + s["t"]) for s in specs]


def _steady(n=6, first_t=31, **kw):
    return [dict(t=first_t + 5 * i, **kw) for i in range(n)]


def _mission(start, specs, scene=SCENE, kind="CustomBattle", load=True):
    return (_load(start, scene, kind) if load else []) + _windows(start, specs)


def _rows(lines, name="taom_debug_test.log"):
    rows, _ = pr.rows_for_log("\n".join(lines), name)
    return rows


class PinnedLineTests(unittest.TestCase):
    def test_pinned_mission_perf_parses_to_its_numbers(self):
        for text in (PINNED_MISSION_PERF, _line(PINNED_MISSION_PERF)):
            w = pr.parse_mission_perf(text)
            self.assertIsNotNone(w)
            self.assertEqual((w.t, w.frames, w.agents, w.active, w.formations, w.gc0, w.gc1, w.gc2),
                             (65, 300, 812, 640, 9, 12, 3, 1))
            self.assertAlmostEqual(w.fps, 60.0)
            self.assertAlmostEqual(w.avg_ms, 16.67)
            self.assertAlmostEqual(w.p95_ms, 25.50)
            self.assertAlmostEqual(w.max_ms, 40.3)

    def test_pinned_tick_profile_parses_to_its_numbers(self):
        t = pr.parse_tick_profile(_line(PINNED_TICK_PROFILE))
        self.assertIsNotNone(t)
        self.assertEqual((t["t"], t["frames"]), (65, 300))
        self.assertAlmostEqual(t["wallMs"], 5000.0)
        self.assertAlmostEqual(t["missionTickMs"], 812.4)
        self.assertAlmostEqual(t["otherMs"], 4040.0)
        self.assertAlmostEqual(t["allocKB"], 2048.0)
        self.assertEqual([b["type"] for b in t["top"]],
                         ["BehaviorTreeMissionLogic", "AdvancedCombatBehavior"])
        self.assertEqual((t["top"][0]["ms"], t["top"][0]["calls"], t["top"][0]["max_ms"],
                          t["top"][0]["kb"]), (410.2, 300, 3.1, 512.0))

    def test_pinned_hitch_parses_and_names_its_dominant_phase(self):
        h = pr.parse_hitch(_line(PINNED_HITCH))
        self.assertIsNotNone(h)
        self.assertEqual((h["t"], h["gc0"], h["gc1"], h["gc2"]), (72, 1, 1, 0))
        self.assertAlmostEqual(h["frameMs"], 812.35)
        self.assertAlmostEqual(h["allocKB"], 96.0)
        self.assertEqual(h["phase"], "agentTickMs")  # 795.10 beats waitTickMs 790.00
        self.assertEqual([b["type"] for b in h["top"]],
                         ["BehaviorTreeMissionLogic", "AdvancedCombatBehavior",
                          "MissionPerfHeartbeatBehavior"])
        # "na" (no allocation counter) stays None, never 0.
        no_alloc = pr.parse_hitch(PINNED_HITCH.replace("allocKB=96", "allocKB=na"))
        self.assertIsNone(no_alloc["allocKB"])

    def test_pinned_perf_context_parses(self):
        c = pr.parse_context(_line(PINNED_PERF_CONTEXT))
        self.assertIsNotNone(c)
        self.assertEqual(c["build"], "Debug")
        self.assertEqual(c["missionInProcess"], 1)
        self.assertEqual(c["scene"], "battle_terrain_029")
        self.assertEqual(c["textureQuality"], "1")
        self.assertEqual(c["tickProfiler"], "on")
        self.assertEqual(c["memLoad"], 61.0)
        self.assertEqual(c["diag"], list(DEFAULT_DIAG))
        no_mem = pr.parse_context(PINNED_PERF_CONTEXT.replace("memLoad=61", "memLoad=na"))
        self.assertIsNone(no_mem["memLoad"])


class SegmentTests(unittest.TestCase):
    def test_one_mission_gives_one_row_with_its_load_buckets(self):
        rows = _rows(_mission(0, [dict(t=6)] + _steady()))
        self.assertEqual(len(rows), 1)
        r = rows[0]
        self.assertEqual((r["mission"], r["scene"], r["opened_by"]),
                         ("CustomBattle", SCENE, "MissionOpenNew"))
        self.assertEqual((r["windows"], r["steady_windows"]), (7, 6))
        self.assertEqual(r["load_ms"], 2857)
        self.assertEqual(r["load_buckets"], {"bucket1": 2, "bucket2": 745, "bucket3a": 195,
                                             "bucket3b": 390, "bucket3c": 151, "bucket4": 874})
        self.assertEqual(r["load_dominant"], "bucket4")

    def test_three_missions_in_one_log_give_three_rows_in_order(self):
        lines = (_mission(0, _steady(agents=273), scene="a")
                 + _mission(100, _steady(agents=83), scene="b")
                 + _mission(200, _steady(agents=164), scene="c"))
        rows = _rows(lines)
        self.assertEqual([r["scene"] for r in rows], ["a", "b", "c"])
        self.assertEqual([r["mission_index"] for r in rows], [1, 2, 3])
        self.assertEqual([r["agents_max"] for r in rows], [273, 83, 164])

    def test_log_without_the_profiler_lines_parses_without_them(self):
        r = _rows(_mission(0, _steady()))[0]
        self.assertIsNone(r["context"])
        self.assertIsNone(r["tick_profile"])
        self.assertEqual(r["hitches"]["count"], 0)
        self.assertEqual(r["malformed"], 0)

    def test_orphan_encounter_between_missions_keeps_the_previous_load_buckets(self):
        # 2026-09-29 campaign log shape: an EncounterStart that opened no mission sits after
        # mission 1, then the next encounter opens mission 2. The ledger anchors on the LAST
        # mission start, so mission 1's buckets survive only if its load is cut at BattlePlayable.
        lines = (_mission(0, _steady(), kind="Battle")
                 + [_line("[BattleLoad] seq=1 t=+0ms phase=EncounterStart mainPartySize=1", 90)]
                 + [_line("[BattleLoad] seq=1 t=+0ms phase=EncounterStart mainPartySize=1", 95)]
                 + _mission(100, _steady(), kind="Battle"))
        rows = _rows(lines)
        self.assertEqual(len(rows), 2)
        self.assertIsNotNone(rows[0]["load_buckets"])
        self.assertEqual(rows[0]["load_dominant"], "bucket4")

    def test_heartbeat_only_log_splits_on_a_clock_restart(self):
        lines = _mission(0, _steady(), load=False) + _mission(100, _steady(), load=False)
        rows = _rows(lines)
        self.assertEqual(len(rows), 2)
        self.assertEqual([r["opened_by"] for r in rows], ["MissionPerf", "MissionPerf"])

    def test_heartbeat_only_log_splits_when_t_grows_slower_than_the_wall_clock(self):
        # Mission 1 ends after one window at t=+6; mission 2's first window is t=+7, sixty
        # seconds of wall clock later. t grew by 1 while the clock grew by 60: a new mission.
        lines = _mission(0, [dict(t=6)], load=False) + _mission(60, [dict(t=7)], load=False)
        self.assertEqual(len(_rows(lines)), 2)

    def test_pause_inside_one_mission_keeps_one_row(self):
        # t and the wall clock jump together (64 s), so it is the same mission.
        lines = [_line(_perf(t=31), 32), _line(_perf(t=95), 96)]
        self.assertEqual(len(_rows(lines)), 1)

    def test_perf_context_opens_the_mission_when_battle_load_is_absent(self):
        ctx2 = PINNED_PERF_CONTEXT.replace("missionInProcess=1", "missionInProcess=3")
        lines = (_mission(0, _steady(), load=False)
                 + [_line(ctx2, 95)] + _mission(100, _steady(), load=False))
        rows = _rows(lines)
        self.assertEqual(len(rows), 2)
        self.assertIsNone(rows[0]["context"])
        self.assertEqual(rows[1]["context"]["missionInProcess"], 3)
        self.assertEqual(rows[1]["opened_by"], "PerfContext")

    def test_perf_context_after_mission_open_stays_in_that_mission(self):
        lines = _load(0) + [_line(PINNED_PERF_CONTEXT, 3)] + _windows(0, _steady())
        rows = _rows(lines)
        self.assertEqual(len(rows), 1)
        self.assertEqual(rows[0]["context"]["build"], "Debug")

    def test_heartbeat_disabled_line_is_noted_not_malformed(self):
        lines = _mission(0, _steady()) + [_line(
            "[MissionPerf] heartbeat disabled for this mission after NullReferenceException: x",
            70, level="ERROR")]
        r = _rows(lines)[0]
        self.assertTrue(r["heartbeat_disabled"])
        self.assertEqual(r["malformed"], 0)

    def test_mission_that_never_ticked_is_a_row_without_windows(self):
        r = _rows(_load(0)[:3])[0]
        self.assertEqual(r["windows"], 0)
        self.assertIsNone(r["fps_median"])
        self.assertIsNone(r["spawn_max_ms"])


class SteadyStateTests(unittest.TestCase):
    def test_spawn_window_is_reported_alone_and_excluded_from_steady_stats(self):
        specs = [dict(t=6, fps=101.6, mx=624.6, gc=(41, 5, 0))] + _steady(mx=14.0)
        r = _rows(_mission(0, specs))[0]
        self.assertAlmostEqual(r["spawn_max_ms"], 624.6)
        self.assertEqual(r["spawn_gc0"], 41)
        self.assertAlmostEqual(r["max_ms_max"], 14.0)
        self.assertAlmostEqual(r["fps_median"], 117.0)

    def test_windows_before_30s_or_with_no_active_agents_are_not_steady(self):
        specs = [dict(t=6), dict(t=26, fps=50.0), dict(t=31), dict(t=36, fps=10.0, active=0)]
        r = _rows(_mission(0, specs))[0]
        self.assertEqual(r["steady_windows"], 1)
        self.assertAlmostEqual(r["fps_median"], 117.0)

    def test_medians_max_and_gc_rate_per_minute(self):
        specs = [dict(t=31, fps=120.0, avg=8.0, p95=9.0, mx=12.0, gc=(3, 1, 0), frames=600),
                 dict(t=36, fps=120.0, avg=9.0, p95=10.0, mx=40.0, gc=(6, 0, 0), frames=600),
                 dict(t=41, fps=120.0, avg=10.0, p95=11.0, mx=15.0, gc=(9, 2, 1), frames=600)]
        r = _rows(_mission(0, specs))[0]
        self.assertAlmostEqual(r["avg_ms_median"], 9.0)
        self.assertAlmostEqual(r["p95_ms_median"], 10.0)
        self.assertAlmostEqual(r["max_ms_max"], 40.0)
        # 18 gen-0 collections over 3 x 5 s = 15 s is 72 a minute.
        self.assertAlmostEqual(r["gc0_per_min"], 72.0)
        self.assertAlmostEqual(r["gc1_per_min"], 12.0)
        self.assertAlmostEqual(r["gc2_per_min"], 4.0)

    def test_no_steady_windows_reports_none_not_zero(self):
        r = _rows(_mission(0, [dict(t=6), dict(t=11), dict(t=16)]))[0]
        self.assertEqual(r["steady_windows"], 0)
        for key in ("fps_median", "avg_ms_median", "p95_ms_median", "max_ms_max", "gc0_per_min"):
            self.assertIsNone(r[key], key)


class FlagTests(unittest.TestCase):
    def _flags(self, lines):
        return _rows(lines)[0]["flags"]

    def test_frame_cap_on_a_plateau(self):
        specs = [dict(t=31 + 5 * i, fps=f) for i, f in enumerate([117.0, 116.8, 117.2, 97.3, 117.1])]
        self.assertIn("FRAME_CAP", self._flags(_mission(0, specs)))

    def test_frame_cap_when_fps_holds_while_active_agents_fall(self):
        fps = [117.0, 100.0, 90.0, 80.0, 116.6]
        active = [300, 260, 230, 200, 150]
        specs = [dict(t=31 + 5 * i, fps=f, agents=300, active=a)
                 for i, (f, a) in enumerate(zip(fps, active))]
        self.assertIn("FRAME_CAP", self._flags(_mission(0, specs)))

    def test_no_frame_cap_when_fps_moves(self):
        specs = [dict(t=31 + 5 * i, fps=f) for i, f in enumerate([60.0, 75.0, 90.0, 105.0, 120.0])]
        self.assertNotIn("FRAME_CAP", self._flags(_mission(0, specs)))

    def test_no_frame_cap_with_fewer_than_four_steady_windows(self):
        self.assertNotIn("FRAME_CAP", self._flags(_mission(0, _steady(n=3))))

    def test_memory_pressure_from_a_mem_sample_at_80_percent(self):
        sample = ("[MemSample] privMB=7526 wsMB=4240 heapMB=77 sysCommitUsedMB=93799 "
                  "sysCommitLimitMB=128662 availPhysMB=10529 memLoad={}%")
        hot = _mission(0, _steady()) + [_line(sample.format(83), 50)]
        cool = _mission(0, _steady()) + [_line(sample.format(46), 50)]
        self.assertIn("MEMORY_PRESSURE", self._flags(hot))
        self.assertNotIn("MEMORY_PRESSURE", self._flags(cool))

    def test_memory_pressure_from_perf_context(self):
        hot = _load(0) + [_line(PINNED_PERF_CONTEXT.replace("memLoad=61", "memLoad=83"), 3)] \
            + _windows(0, _steady())
        cool = _load(0) + [_line(PINNED_PERF_CONTEXT, 3)] + _windows(0, _steady())
        self.assertIn("MEMORY_PRESSURE", self._flags(hot))
        self.assertNotIn("MEMORY_PRESSURE", self._flags(cool))

    def test_diag_on_only_beyond_the_default_diagnostics(self):
        # PINNED_PERF_CONTEXT lists all seven default-on diagnostics (missionPerf among them) and
        # tickProfiler=on. A default install lists the same seven with the profiler off: no flag.
        def flags(ctx):
            return self._flags(_load(0) + [_line(ctx, 3)] + _windows(0, _steady()))
        default = PINNED_PERF_CONTEXT.replace("tickProfiler=on", "tickProfiler=off")
        self.assertNotIn("DIAG_ON", flags(default))
        self.assertNotIn("DIAG_ON", flags(default.split(" diag=")[0] + " diag=none"))
        self.assertIn("DIAG_ON", flags(default.replace("missionPerf", "missionPerf,troopCountDiag")))
        self.assertIn("DIAG_ON", flags(PINNED_PERF_CONTEXT))

    def test_dirty_build_and_pair_mismatch_from_the_build_stamp_line(self):
        # Shapes from BuildStampReport.BuildReport. DescribeVerdict writes an em dash after
        # MISMATCH; chr(0x2014) keeps this file ASCII.
        dirty_mismatch = _line(
            "[BuildStamp] TAOM=v2.0.0.0 build.20261002-163736Z+bc39f6e4.dirty "
            "TAOM.Dependencies=v0.1.0.0 build.20261001-134226Z+6b00881b.dirty "
            "MISMATCH " + chr(0x2014) + " built 1d 02h 55m apart. These modules were not built "
            "together; update BOTH from the same release or expect the preview patches to fail "
            "(issue #371).")
        clean_paired = _line(
            "[BuildStamp] TAOM=v2.0.0.0 build.20261002-163736Z+bc39f6e4 "
            "TAOM.Dependencies=v0.1.0.0 build.20261002-163730Z+bc39f6e4 (pair OK)")
        flags = self._flags([dirty_mismatch] + _mission(10, _steady()))
        self.assertIn("DIRTY_BUILD", flags)
        self.assertIn("BUILD_PAIR_MISMATCH", flags)
        flags = self._flags([clean_paired] + _mission(10, _steady()))
        self.assertNotIn("DIRTY_BUILD", flags)
        self.assertNotIn("BUILD_PAIR_MISMATCH", flags)


class TickAndHitchTests(unittest.TestCase):
    def test_tick_profile_aggregates_per_behaviour_ms_per_second_and_share(self):
        lines = _mission(0, _steady()) + [_line(PINNED_TICK_PROFILE, 66),
                                          _line(PINNED_TICK_PROFILE.replace("t=+65s", "t=+70s"), 71)]
        tick = _rows(lines)[0]["tick_profile"]
        self.assertEqual(tick["windows"], 2)
        # 2 x 812.40 ms over 10 s of wall clock.
        self.assertAlmostEqual(tick["phase_ms_per_s"]["missionTickMs"], 162.48)
        bt = tick["behaviours"][0]
        self.assertEqual(bt["type"], "BehaviorTreeMissionLogic")
        # 820.4 ms over 10 s; the managed phases (preDisplay, missionTick, preTick) total
        # 2 x 865.00 ms, so the share is 820.4 / 1730; 1024 KB over 10 s is 102.4 KB/s.
        self.assertAlmostEqual(bt["ms_per_s"], 82.04)
        self.assertAlmostEqual(bt["share"], 820.4 / 1730.0)
        self.assertEqual(bt["calls"], 600)
        self.assertAlmostEqual(bt["kb_per_s"], 102.4)

    def test_hitches_counted_by_dominant_phase(self):
        tick_heavy = PINNED_HITCH.replace("missionTickMs=5.20", "missionTickMs=900.00")
        lines = _mission(0, _steady()) + [_line(PINNED_HITCH, 72), _line(PINNED_HITCH, 77),
                                          _line(tick_heavy, 82)]
        hitches = _rows(lines)[0]["hitches"]
        self.assertEqual(hitches["count"], 3)
        self.assertEqual(hitches["by_phase"], {"agentTickMs": 2, "missionTickMs": 1})
        self.assertAlmostEqual(hitches["max_frame_ms"], 812.35)


class MalformedTests(unittest.TestCase):
    def test_malformed_lines_are_skipped_and_counted(self):
        broken_perf = _perf(t=46).replace(" gc2=0", "")
        broken_tick = PINNED_TICK_PROFILE.replace(" wallMs=5000.00", "")
        lines = (_mission(0, _steady(n=3)) + [_line(broken_perf, 47), _line(broken_tick, 48)]
                 + _windows(0, [dict(t=51)]))
        r = _rows(lines)[0]
        self.assertEqual(r["malformed"], 2)
        self.assertEqual(r["windows"], 4)
        self.assertIsNone(r["tick_profile"])

    def test_malformed_line_before_any_mission_is_counted_too(self):
        rows, orphans = pr.rows_for_log(_line("[MissionPerf] t=+5s frames=x") + "\n", "x.log")
        self.assertEqual((rows, orphans), ([], 1))


class CompareTests(unittest.TestCase):
    def _group(self, fps_values, build=None, textures="1"):
        rows = []
        for i, fps in enumerate(fps_values):
            lines = _load(i * 100)
            if build is not None:
                ctx = PINNED_PERF_CONTEXT.replace("build=Debug", f"build={build}") \
                    .replace("textureQuality=1", f"textureQuality={textures}")
                lines.append(_line(ctx, i * 100 + 3))
            rows += _rows(lines + _windows(i * 100, _steady(fps=fps)))
        return rows

    def test_medians_delta_and_percent_with_n_per_group(self):
        result = pr.compare_rows(self._group([100.0, 110.0, 120.0]), self._group([90.0, 95.0, 100.0]))
        fps = next(m for m in result["metrics"] if m["metric"] == "fps_median")
        self.assertEqual((fps["n_a"], fps["n_b"]), (3, 3))
        self.assertAlmostEqual(fps["a"], 110.0)
        self.assertAlmostEqual(fps["b"], 95.0)
        self.assertAlmostEqual(fps["delta"], -15.0)
        self.assertAlmostEqual(fps["pct"], -13.636, places=2)
        self.assertEqual(result["context_unchecked"], {"a": 3, "b": 3})

    def test_refuses_mixed_builds_unless_allowed(self):
        a, b = self._group([100.0], build="Debug"), self._group([100.0], build="Release")
        with self.assertRaises(pr.MixedRunsError):
            pr.compare_rows(a, b)
        self.assertEqual(pr.compare_rows(a, b, allow_mixed=True)["a"]["rows"], 1)

    def test_refuses_mixed_texture_quality(self):
        a = self._group([100.0], build="Debug", textures="2")
        b = self._group([100.0], build="Debug", textures="4")
        with self.assertRaises(pr.MixedRunsError):
            pr.compare_rows(a, b)

    def test_same_build_and_textures_compare(self):
        a = self._group([100.0], build="Debug", textures="2")
        b = self._group([90.0], build="Debug", textures="2")
        self.assertEqual(pr.compare_rows(a, b)["context_unchecked"], {"a": 0, "b": 0})

    def test_flags_are_counted_per_group(self):
        a = self._group([117.0, 117.0])
        result = pr.compare_rows(a, a)
        self.assertEqual(result["flags"]["a"].get("FRAME_CAP"), 2)


class CliTests(unittest.TestCase):
    def setUp(self):
        self.tmp = tempfile.TemporaryDirectory()
        self.addCleanup(self.tmp.cleanup)

    def _write(self, name, lines):
        path = Path(self.tmp.name) / name
        path.write_text("\n".join(lines) + "\n", encoding="utf-8")
        return str(path)

    def _run(self, *args):
        return subprocess.run([sys.executable, "-B", str(TOOL), *args],
                              capture_output=True, text=True, timeout=120)

    def test_rows_exit_0_and_json_parses(self):
        log = self._write("a.log", _mission(0, _steady()))
        done = self._run(log, "--json")
        self.assertEqual(done.returncode, 0, done.stderr)
        self.assertEqual(len(json.loads(done.stdout)["rows"]), 1)

    def test_text_output_names_the_row_and_the_totals(self):
        done = self._run(self._write("a.log", _mission(0, _steady())))
        self.assertEqual(done.returncode, 0, done.stderr)
        self.assertIn(f"a.log #1 CustomBattle {SCENE}", done.stdout)
        self.assertIn("rows: 1  malformed lines skipped: 0", done.stdout)

    def test_exit_1_when_no_mission_is_found(self):
        done = self._run(self._write("empty.log", [_line("[Engine] Bannerlord=v1.5.3")]))
        self.assertEqual(done.returncode, 1)

    def test_exit_2_for_an_unreadable_file(self):
        done = self._run(str(Path(self.tmp.name) / "missing.log"))
        self.assertEqual(done.returncode, 2)

    def test_exit_2_for_no_arguments(self):
        self.assertEqual(self._run().returncode, 2)

    def test_compare_exit_0_and_refusal_exit_2(self):
        debug = PINNED_PERF_CONTEXT
        release = PINNED_PERF_CONTEXT.replace("build=Debug", "build=Release")
        a = self._write("a.log", _load(0) + [_line(debug, 3)] + _windows(0, _steady()))
        b = self._write("b.log", _load(0) + [_line(release, 3)] + _windows(0, _steady()))
        self.assertEqual(self._run("compare", "--a", a, "--b", a).returncode, 0)
        refused = self._run("compare", "--a", a, "--b", b)
        self.assertEqual(refused.returncode, 2)
        self.assertIn("refusing to compare", refused.stderr)
        self.assertEqual(self._run("compare", "--a", a, "--b", b, "--allow-mixed").returncode, 0)

    def test_compare_scene_filter_leaving_a_group_empty_exits_1(self):
        a = self._write("a.log", _mission(0, _steady()))
        self.assertEqual(self._run("compare", "--a", a, "--b", a, "--scene", "other").returncode, 1)


if __name__ == "__main__":
    unittest.main()
```

Run `python -B -m unittest tools.tests.test_perf_runs`.

**Verify**: the run fails at import, showing `ModuleNotFoundError: No module named 'perf_runs'`
and `FAILED (errors=1)`. Quote those two lines in your report.

### Step 4: write the tool (GREEN)

Create `tools/perf_runs.py` with exactly this content:

```python
#!/usr/bin/env python3
"""Turn TAOM debug logs into one performance row per mission, and compare two groups of runs.

A frame-time or memory change is judged by numbers, not by feel. The `[MissionPerf]` heartbeat
(Main/Features/MissionPerf) writes one line every five seconds of wall clock while a mission
ticks; this tool cuts any number of `taom_debug_*.log` files into missions and reduces each
mission to one row, then compares the rows of an A group against a B group.

INPUT LINES (each carries the FileLogger prefix `[YYYY-MM-DD HH:MM:SS] [LEVEL] `; the tool keys
on the tag, never on the position):
  [MissionPerf]  MissionPerfLine.Build, the twin pin is PINNED_MISSION_PERF in
                 tools/tests/test_perf_runs.py and FrameStatsTests.BuildLine_FormatsEveryFieldInvariantly
  [TickProfile], [Hitch], [PerfContext]
                 the mission tick profiler's lines (plan 028); optional, a log without them parses
  [BattleLoad], [MemSample]
                 read through triage_battle_load.py's own parser, never re-implemented here
  [BuildStamp]   the startup build identity line (Main/Core/Diagnostics/BuildStampReport.cs)

MISSION BOUNDARIES. A mission starts at its `[BattleLoad] phase=MissionOpenNew` line (one per
mission in custom battles, campaign battles, tournaments and town visits). When Battle Load
Diagnostics was off, a `[PerfContext]` line or the heartbeat's own clock starts it instead: `t=`
counts wall seconds from the mission's creation, so a window whose `t` does not advance with the
FileLogger clock belongs to a new mission.

ROW. Load buckets come from triage_battle_load.classify_phase_timings over the mission's load
lines (MissionOpenNew up to its first BattlePlayable). The first `[MissionPerf]` window is the
spawn window (BattlePlayable fires with agents=0, so spawning lands in it) and is reported on its
own. Steady-state stats use windows from t >= 30 s with active > 0.

CONFOUNDER FLAGS. FRAME_CAP, MEMORY_PRESSURE, DIAG_ON, DIRTY_BUILD, BUILD_PAIR_MISMATCH. A flag
says the numbers may not mean what they seem; it never changes them. DIAG_ON ignores the
default-on diagnostics (DIAG_BASELINE): it fires for the tick profiler or any other diagnostic.

Usage:
  python tools/perf_runs.py <log> [<log> ...] [--json]
  python tools/perf_runs.py compare --a <log> [...] --b <log> [...] [--scene <id>] [--allow-mixed] [--json]

Exit code: 0 rows found (compare: both groups have rows), 1 no mission found, 2 usage error,
unreadable file, or a compare refused over mixed build or texture settings.
"""
from __future__ import annotations

import argparse
import json
import os
import re
import statistics
import sys
from dataclasses import dataclass, field
from datetime import datetime
from pathlib import Path

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import triage_battle_load as tb  # noqa: E402

STEADY_FROM_S = 30
MEMORY_PRESSURE_PCT = 80
FRAME_CAP_MIN_WINDOWS = 4
FRAME_CAP_TOLERANCE_FPS = 1.0
FRAME_CAP_AGENT_SPREAD = 0.30
# Whole-second rounding of both `t=` and the FileLogger stamp can disagree by about 2 s.
CLOCK_SLACK_S = 3
# The seven `[PerfContext] diag=` tokens of the mission tick profiler. Every one defaults on
# (BattleLoadDiagnosticsSettings), and missionPerf is the heartbeat this tool reads, so a default
# install lists them all: they are the measuring baseline. DIAG_ON means cost beyond it.
DIAG_BASELINE = frozenset(("battleLoad", "stallWatchdog", "stallBundle", "exitSampler",
                           "freezeSampler", "memSampler", "missionPerf"))

_TS_RE = re.compile(r"^\[(\d{4}-\d\d-\d\d)[ T](\d\d:\d\d:\d\d)\]")
_OPEN_RE = re.compile(r"\[BattleLoad\]\s+seq=\d+\s+t=\+\d+ms\s+phase=MissionOpenNew\b(.*)$")
_PLAYABLE_RE = re.compile(r"\[BattleLoad\]\s+seq=\d+\s+t=\+\d+ms\s+phase=BattlePlayable\b")
_MISSION_KIND_RE = re.compile(r"\bmission='([^']*)'")
_SCENE_RE = re.compile(r"\bscene='([^']*)'")
_NUM = r"(\d+(?:\.\d+)?)"
_PERF_RE = re.compile(
    r"\[MissionPerf\]\s+t=\+(\d+)s\s+frames=(\d+)\s+fps=" + _NUM + r"\s+avgMs=" + _NUM
    + r"\s+p95Ms=" + _NUM + r"\s+maxMs=" + _NUM + r"\s+agents=(\d+)\s+active=(\d+)\s+"
    r"formations=(\d+)\s+gc0=(\d+)\s+gc1=(\d+)\s+gc2=(\d+)\s*$")
# MissionPerfHeartbeatBehavior's one ERROR line: carries the tag, is not a window, not malformed.
_PERF_DISABLED = "[MissionPerf] heartbeat disabled"
_KV_RE = re.compile(r"(\w+)=(\S*)")
_T_RE = re.compile(r"^\+(\d+)s$")
_STAMP_RE = re.compile(r"\[BuildStamp\]\s+TAOM=(.*?)\s+TAOM\.Dependencies=")

TAG_PERF = "[MissionPerf]"
TAG_TICK = "[TickProfile]"
TAG_HITCH = "[Hitch]"
TAG_CONTEXT = "[PerfContext]"

PHASES = ("preDisplayMs", "missionTickMs", "preTickMs", "waitTickMs", "agentTickMs", "otherMs")
_MANAGED_PHASES = ("preDisplayMs", "missionTickMs", "preTickMs")
_CONTEXT_KEYS = ("build", "jitOptimized", "clr", "serverGC", "latency", "missionInProcess",
                 "scene", "agents", "textureQuality", "shadowQuality", "particleDetail",
                 "ragdolls", "memLoad", "availPhysMB", "tickProfiler", "diag")

METRICS = ("fps_median", "avg_ms_median", "p95_ms_median", "max_ms_max", "gc0_per_min",
           "gc1_per_min", "gc2_per_min", "spawn_max_ms", "spawn_gc0", "agents_max", "load_ms",
           "mem_load_max")


# --------------------------------------------------------------------------- #
# Line parsers: each returns None for a malformed line                         #
# --------------------------------------------------------------------------- #
@dataclass
class Window:
    t: int
    frames: int
    fps: float
    avg_ms: float
    p95_ms: float
    max_ms: float
    agents: int
    active: int
    formations: int
    gc0: int
    gc1: int
    gc2: int

    @property
    def seconds(self) -> float | None:
        return self.frames / self.fps if self.fps > 0 else None


def parse_mission_perf(line: str) -> Window | None:
    m = _PERF_RE.search(line)
    if not m:
        return None
    g = m.groups()
    return Window(t=int(g[0]), frames=int(g[1]), fps=float(g[2]), avg_ms=float(g[3]),
                  p95_ms=float(g[4]), max_ms=float(g[5]), agents=int(g[6]), active=int(g[7]),
                  formations=int(g[8]), gc0=int(g[9]), gc1=int(g[10]), gc2=int(g[11]))


def _fields(line: str, tag: str) -> dict:
    return dict(_KV_RE.findall(line[line.index(tag) + len(tag):]))


def _seconds(value: str) -> int:
    m = _T_RE.match(value)
    if not m:
        raise ValueError(value)
    return int(m.group(1))


def _number_or_na(value: str) -> float | None:
    """`na` is the contract's "not measured"; it stays None and is never read as 0."""
    if value == "na":
        return None
    return float(value.rstrip("%"))


def parse_tick_profile(line: str) -> dict | None:
    f = _fields(line, TAG_TICK)
    try:
        out = {"t": _seconds(f["t"]), "frames": int(f["frames"]), "wallMs": float(f["wallMs"]),
               "allocKB": _number_or_na(f["allocKB"]), "top": []}
        for key in PHASES:
            out[key] = float(f[key])
        for item in f["top"].split(","):
            if not item or item == "none":
                continue
            name, _, nums = item.rpartition(":")
            ms, calls, max_ms, kb = nums.split("/")
            if not name:
                raise ValueError(item)
            out["top"].append({"type": name, "ms": float(ms), "calls": int(calls),
                               "max_ms": float(max_ms), "kb": _number_or_na(kb)})
    except (KeyError, ValueError):
        return None
    return out


def parse_hitch(line: str) -> dict | None:
    f = _fields(line, TAG_HITCH)
    try:
        out = {"t": _seconds(f["t"]), "frameMs": float(f["frameMs"]),
               "gc0": int(f["gc0"]), "gc1": int(f["gc1"]), "gc2": int(f["gc2"]),
               "allocKB": _number_or_na(f["allocKB"]), "top": []}
        for key in PHASES:
            out[key] = float(f[key])
        for item in f["top"].split(","):
            if not item or item == "none":
                continue
            name, _, ms = item.rpartition(":")
            if not name:
                raise ValueError(item)
            out["top"].append({"type": name, "ms": float(ms)})
    except (KeyError, ValueError):
        return None
    # The phase that held the most ms; ties go to the earlier phase in PHASES.
    out["phase"] = max(PHASES, key=lambda k: (out[k], -PHASES.index(k)))
    return out


def parse_context(line: str) -> dict | None:
    f = _fields(line, TAG_CONTEXT)
    if any(k not in f for k in _CONTEXT_KEYS):
        return None
    try:
        out = dict(f)
        out["missionInProcess"] = int(f["missionInProcess"])
        out["agents"] = int(f["agents"])
        out["memLoad"] = _number_or_na(f["memLoad"])
        out["availPhysMB"] = _number_or_na(f["availPhysMB"])
    except ValueError:
        return None
    out["diag"] = [] if f["diag"] in ("", "none") else f["diag"].split(",")
    return out


def parse_build_stamp(text: str) -> dict | None:
    """The first [BuildStamp] line of a log: TAOM's own stamp, and whether the startup report
    called the TAOM / TAOM.Dependencies pair a MISMATCH (BuildStampReport.DescribeVerdict)."""
    for raw in text.splitlines():
        if "[BuildStamp]" not in raw:
            continue
        m = _STAMP_RE.search(raw)
        return {"taom": m.group(1).strip() if m else None, "mismatch": " MISMATCH " in raw}
    return None


def _timestamp(raw: str) -> float | None:
    m = _TS_RE.match(raw)
    if not m:
        return None
    stamp = datetime.strptime(m.group(1) + " " + m.group(2), "%Y-%m-%d %H:%M:%S")
    return (stamp - datetime(1970, 1, 1)).total_seconds()


# --------------------------------------------------------------------------- #
# Mission segments                                                             #
# --------------------------------------------------------------------------- #
@dataclass
class Segment:
    log: str
    index: int
    start_line: int
    opened_by: str
    mission: str | None = None
    scene: str | None = None
    lines: list = field(default_factory=list)
    windows: list = field(default_factory=list)
    contexts: list = field(default_factory=list)
    ticks: list = field(default_factory=list)
    hitches: list = field(default_factory=list)
    malformed: int = 0
    heartbeat_disabled: bool = False
    last_perf_ts: float | None = None


def _starts_new_mission(seg: Segment, window: Window, ts: float | None) -> bool:
    """A heartbeat window belongs to a NEW mission when its clock restarted: `t` did not grow,
    or it grew 3+ s less than the FileLogger clock did. A pause stops both clocks' lines alike,
    so a resumed mission keeps its segment."""
    if not seg.windows:
        return False
    prev = seg.windows[-1]
    if window.t <= prev.t:
        return True
    if ts is not None and seg.last_perf_ts is not None:
        return (window.t - prev.t) < (ts - seg.last_perf_ts) - CLOCK_SLACK_S
    return False


def split_missions(text: str, log_name: str = "") -> tuple[list, int]:
    """Cut one log into mission segments. Returns (segments, malformed lines seen before the
    first mission)."""
    segments: list = []
    orphan_malformed = 0
    cur: Segment | None = None

    def open_segment(line_no: int, opened_by: str) -> Segment:
        seg = Segment(log=log_name, index=len(segments) + 1, start_line=line_no,
                      opened_by=opened_by)
        segments.append(seg)
        return seg

    for line_no, raw in enumerate(text.splitlines(), 1):
        m = _OPEN_RE.search(raw)
        if m:
            cur = open_segment(line_no, "MissionOpenNew")
            kind = _MISSION_KIND_RE.search(m.group(1))
            scene = _SCENE_RE.search(m.group(1))
            cur.mission = kind.group(1) if kind else None
            cur.scene = scene.group(1) if scene else None
            cur.lines.append(raw)
            continue
        if TAG_CONTEXT in raw:
            if cur is None or cur.contexts or cur.windows:
                cur = open_segment(line_no, "PerfContext")
            ctx = parse_context(raw)
            if ctx is None:
                cur.malformed += 1
            else:
                cur.contexts.append(ctx)
                if cur.scene is None:
                    cur.scene = ctx["scene"]
            cur.lines.append(raw)
            continue
        if TAG_PERF in raw:
            if _PERF_DISABLED in raw:
                if cur is not None:
                    cur.heartbeat_disabled = True
                    cur.lines.append(raw)
                continue
            window = parse_mission_perf(raw)
            if window is None:
                if cur is None:
                    orphan_malformed += 1
                else:
                    cur.malformed += 1
                    cur.lines.append(raw)
                continue
            ts = _timestamp(raw)
            if cur is None or _starts_new_mission(cur, window, ts):
                cur = open_segment(line_no, "MissionPerf")
            cur.windows.append(window)
            cur.last_perf_ts = ts
            cur.lines.append(raw)
            continue
        for tag, parser, bucket in ((TAG_TICK, parse_tick_profile, "ticks"),
                                    (TAG_HITCH, parse_hitch, "hitches")):
            if tag in raw:
                parsed = parser(raw)
                if cur is None:
                    orphan_malformed += parsed is None
                elif parsed is None:
                    cur.malformed += 1
                else:
                    getattr(cur, bucket).append(parsed)
                break
        if cur is not None:
            cur.lines.append(raw)
    return segments, orphan_malformed


# --------------------------------------------------------------------------- #
# Rows                                                                         #
# --------------------------------------------------------------------------- #
def _median(values: list) -> float | None:
    return statistics.median(values) if values else None


def _load_text(lines: list) -> str:
    """The mission's load: its lines up to and including the first BattlePlayable. What follows
    (ResourceClearOld* on the way out, the next encounter's EncounterStart) is not this load."""
    out = []
    for raw in lines:
        out.append(raw)
        if _PLAYABLE_RE.search(raw):
            break
    return "\n".join(out)


def _load_ms(timeline) -> int | None:
    opened = next((e for e in timeline.events if e.phase == "MissionOpenNew"), None)
    playable = next((e for e in timeline.events if e.phase == "BattlePlayable"), None)
    if opened is None or playable is None or opened.ms is None or playable.ms is None:
        return None
    return playable.ms - opened.ms if playable.ms >= opened.ms else None


def _per_minute(steady: list, attr: str) -> float | None:
    timed = [w for w in steady if w.seconds]
    seconds = sum(w.seconds for w in timed)
    if seconds <= 0:
        return None
    return sum(getattr(w, attr) for w in timed) * 60.0 / seconds


def frame_capped(steady: list) -> bool:
    """FRAME_CAP: fps that does not move with load. Either at least half the steady windows sit
    within 1 fps of one window's fps (a plateau), or the windows with the most and the fewest
    active agents (30% or more apart) run within 1 fps of each other."""
    if len(steady) < FRAME_CAP_MIN_WINDOWS:
        return False
    fps = [w.fps for w in steady]
    plateau = max(sum(1 for g in fps if abs(g - f) <= FRAME_CAP_TOLERANCE_FPS) for f in fps)
    if plateau * 2 >= len(fps):
        return True
    most = max(steady, key=lambda w: w.active)
    least = min(steady, key=lambda w: w.active)
    return (least.active <= (1.0 - FRAME_CAP_AGENT_SPREAD) * most.active
            and abs(most.fps - least.fps) <= FRAME_CAP_TOLERANCE_FPS)


def aggregate_ticks(ticks: list) -> dict | None:
    wall = sum(t["wallMs"] for t in ticks)
    if not ticks or wall <= 0:
        return None
    managed = sum(t[k] for t in ticks for k in _MANAGED_PHASES)
    by_type: dict = {}
    for t in ticks:
        for b in t["top"]:
            e = by_type.setdefault(b["type"], {"ms": 0.0, "calls": 0, "max_ms": 0.0, "kb": None})
            e["ms"] += b["ms"]
            e["calls"] += b["calls"]
            e["max_ms"] = max(e["max_ms"], b["max_ms"])
            if b["kb"] is not None:
                e["kb"] = (e["kb"] or 0.0) + b["kb"]
    behaviours = [{"type": name, "ms_per_s": e["ms"] * 1000.0 / wall,
                   "share": e["ms"] / managed if managed > 0 else None,
                   "calls": e["calls"], "max_ms": e["max_ms"],
                   "kb_per_s": e["kb"] * 1000.0 / wall if e["kb"] is not None else None}
                  for name, e in by_type.items()]
    behaviours.sort(key=lambda b: (-b["ms_per_s"], b["type"]))
    return {"windows": len(ticks), "wall_ms": wall,
            "phase_ms_per_s": {k: sum(t[k] for t in ticks) * 1000.0 / wall for k in PHASES},
            "behaviours": behaviours}


def summarize(seg: Segment, stamp: dict | None) -> dict:
    windows = seg.windows
    first = windows[0] if windows else None
    steady = [w for w in windows if w.t >= STEADY_FROM_S and w.active > 0]
    load_tl = tb.parse_battle_load_log(_load_text(seg.lines))
    timings = tb.classify_phase_timings(load_tl)
    whole = tb.parse_battle_load_log("\n".join(seg.lines))
    context = seg.contexts[0] if seg.contexts else None
    mem_loads = [s.mem_load for s in whole.mem_samples]
    if context is not None and context["memLoad"] is not None:
        mem_loads.append(context["memLoad"])
    hitch_phases: dict = {}
    for h in seg.hitches:
        hitch_phases[h["phase"]] = hitch_phases.get(h["phase"], 0) + 1
    row = {
        "log": seg.log, "mission_index": seg.index, "start_line": seg.start_line,
        "opened_by": seg.opened_by, "mission": seg.mission, "scene": seg.scene,
        "build_stamp": stamp["taom"] if stamp else None, "context": context,
        "windows": len(windows), "steady_windows": len(steady),
        "agents_max": max((w.agents for w in windows), default=None),
        "spawn_max_ms": first.max_ms if first else None,
        "spawn_gc0": first.gc0 if first else None,
        "fps_median": _median([w.fps for w in steady]),
        "avg_ms_median": _median([w.avg_ms for w in steady]),
        "p95_ms_median": _median([w.p95_ms for w in steady]),
        "max_ms_max": max((w.max_ms for w in steady), default=None),
        "gc0_per_min": _per_minute(steady, "gc0"),
        "gc1_per_min": _per_minute(steady, "gc1"),
        "gc2_per_min": _per_minute(steady, "gc2"),
        "load_ms": _load_ms(load_tl),
        "load_buckets": {b["name"]: b["ms"] for b in timings["buckets"]} if timings else None,
        "load_dominant": timings["dominant"] if timings else None,
        "mem_load_max": max(mem_loads) if mem_loads else None,
        "tick_profile": aggregate_ticks(seg.ticks),
        "hitches": {"count": len(seg.hitches), "by_phase": hitch_phases,
                    "max_frame_ms": max((h["frameMs"] for h in seg.hitches), default=None)},
        "heartbeat_disabled": seg.heartbeat_disabled,
        "malformed": seg.malformed,
    }
    flags = []
    if frame_capped(steady):
        flags.append("FRAME_CAP")
    if row["mem_load_max"] is not None and row["mem_load_max"] >= MEMORY_PRESSURE_PCT:
        flags.append("MEMORY_PRESSURE")
    if context is not None and (context["tickProfiler"] == "on"
                                or any(d not in DIAG_BASELINE for d in context["diag"])):
        flags.append("DIAG_ON")
    if stamp and stamp["taom"] and ".dirty" in stamp["taom"]:
        flags.append("DIRTY_BUILD")
    if stamp and stamp["mismatch"]:
        flags.append("BUILD_PAIR_MISMATCH")
    row["flags"] = flags
    return row


def rows_for_log(text: str, log_name: str) -> tuple[list, int]:
    """(rows, malformed lines outside any mission) for one log's text."""
    segments, orphan_malformed = split_missions(text, log_name)
    stamp = parse_build_stamp(text)
    return [summarize(s, stamp) for s in segments], orphan_malformed


# --------------------------------------------------------------------------- #
# Compare                                                                      #
# --------------------------------------------------------------------------- #
class MixedRunsError(ValueError):
    """The compared rows mix [PerfContext] build (Debug, Release) or textureQuality values."""


def _mixed(rows: list, key: str) -> list:
    values = {r["context"][key] for r in rows
              if r["context"] is not None and r["context"].get(key) not in (None, "na")}
    return sorted(values) if len(values) > 1 else []


def compare_rows(rows_a: list, rows_b: list, allow_mixed: bool = False) -> dict:
    if not allow_mixed:
        for key in ("build", "textureQuality"):
            mixed = _mixed(rows_a + rows_b, key)
            if mixed:
                raise MixedRunsError(f"{key} differs across the compared rows: "
                                     f"{', '.join(mixed)} (pass --allow-mixed to compare anyway)")
    metrics = []
    for name in METRICS:
        a = [r[name] for r in rows_a if r[name] is not None]
        b = [r[name] for r in rows_b if r[name] is not None]
        ma, mb = _median(a), _median(b)
        delta = mb - ma if ma is not None and mb is not None else None
        pct = delta * 100.0 / ma if delta is not None and ma else None
        metrics.append({"metric": name, "n_a": len(a), "a": ma, "n_b": len(b), "b": mb,
                        "delta": delta, "pct": pct})

    def flag_counts(rows: list) -> dict:
        counts: dict = {}
        for r in rows:
            for f in r["flags"]:
                counts[f] = counts.get(f, 0) + 1
        return dict(sorted(counts.items()))

    return {"a": {"rows": len(rows_a)}, "b": {"rows": len(rows_b)}, "metrics": metrics,
            "flags": {"a": flag_counts(rows_a), "b": flag_counts(rows_b)},
            "context_unchecked": {"a": sum(1 for r in rows_a if r["context"] is None),
                                  "b": sum(1 for r in rows_b if r["context"] is None)}}


# --------------------------------------------------------------------------- #
# Text output                                                                  #
# --------------------------------------------------------------------------- #
def _fmt(value, digits: int = 2) -> str:
    if value is None:
        return "-"
    if isinstance(value, float):
        return f"{value:.{digits}f}"
    return str(value)


def format_rows(rows: list, malformed: int) -> str:
    out = []
    for r in rows:
        out.append(
            f"{r['log']} #{r['mission_index']} {r['mission'] or '-'} {r['scene'] or '-'} "
            f"windows={r['windows']}/{r['steady_windows']} fps={_fmt(r['fps_median'], 1)} "
            f"avgMs={_fmt(r['avg_ms_median'])} p95Ms={_fmt(r['p95_ms_median'])} "
            f"maxMs={_fmt(r['max_ms_max'], 1)} gc0/min={_fmt(r['gc0_per_min'], 1)} "
            f"gc2/min={_fmt(r['gc2_per_min'], 1)} agents={_fmt(r['agents_max'])} "
            f"spawnMaxMs={_fmt(r['spawn_max_ms'], 1)} spawnGc0={_fmt(r['spawn_gc0'])} "
            f"loadMs={_fmt(r['load_ms'])} flags={','.join(r['flags']) or 'none'}")
        if r["load_buckets"]:
            out.append("  load: " + " ".join(f"{k}={_fmt(v)}" for k, v in r["load_buckets"].items())
                       + f" dominant={r['load_dominant'] or '-'}")
        tick = r["tick_profile"]
        if tick:
            out.append("  tick ms/s: " + " ".join(f"{k}={v:.1f}" for k, v in tick["phase_ms_per_s"].items()))
            for b in tick["behaviours"][:8]:
                share = f"{b['share'] * 100:.1f}%" if b["share"] is not None else "-"
                out.append(f"    {b['type']} {b['ms_per_s']:.2f} ms/s share={share}")
        if r["hitches"]["count"]:
            phases = ", ".join(f"{k} x{v}" for k, v in sorted(r["hitches"]["by_phase"].items()))
            out.append(f"  hitches: {r['hitches']['count']} ({phases})")
    out.append(f"rows: {len(rows)}  malformed lines skipped: {malformed}")
    return "\n".join(out)


def format_compare(result: dict) -> str:
    out = [f"A: {result['a']['rows']} rows   B: {result['b']['rows']} rows",
           f"{'metric':<15}{'N(A)':>5}{'A':>11}{'N(B)':>5}{'B':>11}{'delta':>10}{'pct':>9}"]
    for m in result["metrics"]:
        pct = f"{m['pct']:+.1f}%" if m["pct"] is not None else "-"
        delta = f"{m['delta']:+.2f}" if m["delta"] is not None else "-"
        out.append(f"{m['metric']:<15}{m['n_a']:>5}{_fmt(m['a']):>11}{m['n_b']:>5}"
                   f"{_fmt(m['b']):>11}{delta:>10}{pct:>9}")
    for side in ("a", "b"):
        flags = ", ".join(f"{k} x{v}" for k, v in result["flags"][side].items()) or "none"
        out.append(f"flags {side.upper()}: {flags}")
        if result["context_unchecked"][side]:
            out.append(f"  {result['context_unchecked'][side]} {side.upper()} row(s) carry no "
                       "[PerfContext]: build and texture settings unchecked")
    return "\n".join(out)


# --------------------------------------------------------------------------- #
# CLI                                                                          #
# --------------------------------------------------------------------------- #
def _read(path: str) -> str:
    with open(path, encoding="utf-8-sig", errors="replace") as fh:
        return fh.read()


def _load(paths: list) -> tuple[list, int]:
    rows, malformed = [], 0
    for path in paths:
        r, orphan = rows_for_log(_read(path), Path(path).name)
        rows.extend(r)
        malformed += orphan + sum(x["malformed"] for x in r)
    return rows, malformed


def _main_rows(argv: list) -> int:
    p = argparse.ArgumentParser(prog="perf_runs.py",
                                description="One performance row per mission in taom_debug logs.")
    p.add_argument("logs", nargs="+")
    p.add_argument("--json", action="store_true", help="print JSON instead of text")
    args = p.parse_args(argv)
    try:
        rows, malformed = _load(args.logs)
    except OSError as e:
        print(f"perf_runs: cannot read {e.filename}: {e.strerror}", file=sys.stderr)
        return 2
    if args.json:
        print(json.dumps({"rows": rows, "malformed": malformed}, indent=2))
    else:
        print(format_rows(rows, malformed))
    return 0 if rows else 1


def _main_compare(argv: list) -> int:
    p = argparse.ArgumentParser(prog="perf_runs.py compare",
                                description="Compare the mission rows of two groups of logs.")
    p.add_argument("--a", nargs="+", required=True, metavar="LOG")
    p.add_argument("--b", nargs="+", required=True, metavar="LOG")
    p.add_argument("--scene", help="keep only rows of this scene id")
    p.add_argument("--allow-mixed", action="store_true",
                   help="compare even when [PerfContext] build or textureQuality differ")
    p.add_argument("--json", action="store_true", help="print JSON instead of text")
    args = p.parse_args(argv)
    try:
        rows_a, _ = _load(args.a)
        rows_b, _ = _load(args.b)
    except OSError as e:
        print(f"perf_runs: cannot read {e.filename}: {e.strerror}", file=sys.stderr)
        return 2
    if args.scene:
        rows_a = [r for r in rows_a if r["scene"] == args.scene]
        rows_b = [r for r in rows_b if r["scene"] == args.scene]
    if not rows_a or not rows_b:
        print(f"perf_runs: no mission rows in group {'A' if not rows_a else 'B'}", file=sys.stderr)
        return 1
    try:
        result = compare_rows(rows_a, rows_b, allow_mixed=args.allow_mixed)
    except MixedRunsError as e:
        print(f"perf_runs: refusing to compare: {e}", file=sys.stderr)
        return 2
    print(json.dumps(result, indent=2) if args.json else format_compare(result))
    return 0


def main(argv: list | None = None) -> int:
    argv = list(sys.argv[1:] if argv is None else argv)
    if argv and argv[0] == "compare":
        return _main_compare(argv[1:])
    return _main_rows(argv)


if __name__ == "__main__":
    sys.exit(main())
```

Then run, in order:
1. `python -B -m unittest tools.tests.test_perf_runs` gives `Ran 43 tests` and `OK`.
2. `python -B -m unittest tools.tests.test_triage_battle_load` gives `Ran 153 tests` and `OK`, and
   `git status --porcelain -- tools/triage_battle_load.py tools/tests/test_triage_battle_load.py`
   prints nothing.
3. `grep -nP "[^\x00-\x7F]" tools/perf_runs.py tools/tests/test_perf_runs.py` prints nothing.
4. `python -B tools/perf_runs.py; echo "exit=$?"` prints argparse usage and `exit=2`.

**Verify**: all four hold. A failing test here: fix the implementation, never the test (a test whose
oracle you believe is wrong is a STOP with the evidence).

### Step 5: prove each guard can fail (mutation check, scratch only)

The RED in Step 3 proves only that the module was missing. Prove the key rules are guarded by
running eight mutants of the tool against the suite, in a scratch copy (the worktree is never
modified). Write this script to `<your scratch folder>/mutate_perf_runs.py` with the Write tool:

```python
"""Mutation check for tools/perf_runs.py: each mutant must make its named test fail.

Copies the worktree's tools/perf_runs.py and tools/tests/test_perf_runs.py into a scratch
folder, applies one edit at a time, and runs the suite there. The worktree is never modified.
Usage: PERF_WT=<worktree> PERF_SCRATCH=<scratch folder> python -B mutate_perf_runs.py
"""
import os
import shutil
import subprocess
import sys

WT = os.environ["PERF_WT"]
MUT = os.path.join(os.environ["PERF_SCRATCH"], "mut")
MUTANTS = {
    "whole_segment_load": (
        "    for raw in lines:\n        out.append(raw)\n        if _PLAYABLE_RE.search(raw):\n            break",
        "    for raw in lines:\n        out.append(raw)",
        "test_orphan_encounter_between_missions_keeps_the_previous_load_buckets"),
    "no_clock_rule": (
        "        return (window.t - prev.t) < (ts - seg.last_perf_ts) - CLOCK_SLACK_S",
        "        return False",
        "test_heartbeat_only_log_splits_when_t_grows_slower_than_the_wall_clock"),
    "context_never_opens": (
        "            if cur is None or cur.contexts or cur.windows:",
        "            if cur is None:",
        "test_perf_context_opens_the_mission_when_battle_load_is_absent"),
    "steady_includes_inactive": (
        "w.t >= STEADY_FROM_S and w.active > 0]",
        "w.t >= STEADY_FROM_S]",
        "test_windows_before_30s_or_with_no_active_agents_are_not_steady"),
    "no_spread_clause": (
        "    return (least.active <= (1.0 - FRAME_CAP_AGENT_SPREAD) * most.active",
        "    return False and (least.active <= (1.0 - FRAME_CAP_AGENT_SPREAD) * most.active",
        "test_frame_cap_when_fps_holds_while_active_agents_fall"),
    "disabled_counts_malformed": (
        "            if _PERF_DISABLED in raw:",
        "            if False:",
        "test_heartbeat_disabled_line_is_noted_not_malformed"),
    "mismatch_word": (
        '" MISMATCH " in raw',
        '"MISMATCHX" in raw',
        "test_dirty_build_and_pair_mismatch_from_the_build_stamp_line"),
    "diag_any_token": (
        'or any(d not in DIAG_BASELINE for d in context["diag"])',
        'or bool(context["diag"])',
        "test_diag_on_only_beyond_the_default_diagnostics"),
}
killed = 0
for name, (old, new, test) in MUTANTS.items():
    if os.path.exists(MUT):
        shutil.rmtree(MUT)
    os.makedirs(os.path.join(MUT, "tools", "tests"))
    shutil.copy(os.path.join(WT, "tools", "perf_runs.py"), os.path.join(MUT, "tools"))
    shutil.copy(os.path.join(WT, "tools", "tests", "test_perf_runs.py"),
                os.path.join(MUT, "tools", "tests"))
    open(os.path.join(MUT, "tools", "tests", "__init__.py"), "w").close()
    path = os.path.join(MUT, "tools", "perf_runs.py")
    text = open(path, encoding="utf-8").read()
    if old not in text:
        print(f"{name}: SOURCE TEXT NOT FOUND (the implementation differs from the plan)")
        continue
    open(path, "w", encoding="utf-8").write(text.replace(old, new))
    env = dict(os.environ, PYTHONPATH=os.path.join(WT, "tools"))
    run = subprocess.run([sys.executable, "-B", "-m", "unittest", "tools.tests.test_perf_runs"],
                         cwd=MUT, capture_output=True, text=True, env=env, timeout=300)
    failed = [l for l in run.stderr.splitlines() if l.startswith(("FAIL:", "ERROR:"))]
    hit = any(test in l for l in failed)
    killed += hit
    print(f"{name}: {'KILLED' if hit else 'SURVIVED'} by {test}" + ("" if hit else f" (failures: {failed})"))
shutil.rmtree(MUT, ignore_errors=True)
print(f"mutants killed: {killed} of {len(MUTANTS)}")
```

Run `PERF_WT="<your worktree>" PERF_SCRATCH="<your scratch folder>" timeout 900 python -B "<your scratch folder>/mutate_perf_runs.py"`.

**Verify**: eight lines ending `KILLED by test_...` and the last line `mutants killed: 8 of 8`. A
`SOURCE TEXT NOT FOUND` line means your tool differs from Step 4's text: STOP. A `SURVIVED` line
means a test no longer guards its rule: STOP.

### Step 6: real-log smoke (only if the logs exist on this machine)

If the game's `bin/Win64_Shipping_Client/Logs/` folder (under the Bannerlord install; the desktop
holds it) still contains `taom_debug_2026-10-02_11-38-06.log` (FileLogger keeps a limited number of
logs, so it may be gone), run `python -B tools/perf_runs.py "<that log>"; echo "exit=$?"`.

**Verify**: at planning time the prototype gave `exit=0`, `rows: 4  malformed lines skipped: 0`,
four `CustomBattle battle_terrain_biome_148` rows with `windows=5/0`, `14/9`, `25/20`, `25/20`,
`FRAME_CAP` on rows 2 and 3, and `MEMORY_PRESSURE,DIRTY_BUILD,BUILD_PAIR_MISMATCH` on all four. Report
what you got. If the log is gone, run the tool on the newest `taom_debug_*.log` there and report
the row count and exit code instead. If neither exists, record "smoke skipped: no logs" (this step is
evidence, not a gate). A traceback is a STOP.

### Step 7: the C# comments

1. `Main/Features/MissionPerf/MissionPerfLine.cs`: replace lines 5-8
   ```csharp
   /// <summary>
   /// The one line format, kept pure so the heartbeat behavior stays a thin reader of the engine
   /// and the parser in <c>tools/</c> has a single fixture to match.
   /// </summary>
   ```
   with
   ```csharp
   /// <summary>
   /// The one line format, kept pure so the heartbeat behavior stays a thin reader of the engine
   /// and <c>tools/perf_runs.py</c> has a single fixture to match: <c>PINNED_MISSION_PERF</c> in
   /// <c>tools/tests/test_perf_runs.py</c> and <c>FrameStatsTests.BuildLine_FormatsEveryFieldInvariantly</c>
   /// assert the same literal. Change both or neither.
   /// </summary>
   ```
2. `TAOM.Tests/Features/MissionPerf/FrameStatsTests.cs`: directly above the `[TestMethod]` on line 133
   (the one for `BuildLine_FormatsEveryFieldInvariantly`), insert
   ```csharp
       /// <summary>
       /// Cross-language twin pin: <c>PINNED_MISSION_PERF</c> in <c>tools/tests/test_perf_runs.py</c> is
       /// this literal, and <c>tools/perf_runs.py</c> must parse it to these numbers. Change both or neither.
       /// </summary>
   ```
3. Build: `TEMP="<tmp>" TMP="<tmp>" dotnet build Main/TAOM.csproj -p:DisableModuleCopy=true -p:ModuleId=`.
4. `TEMP="<tmp>" TMP="<tmp>" dotnet test TAOM.Tests -p:DisableModuleCopy=true -p:ModuleId= --filter "FullyQualifiedName~FrameStatsTests"`.

**Verify**: the build exits 0 with 0 errors; the filtered run reports 10 tests, all passed;
`git grep -n "the parser in <c>tools/</c>" -- ':!plans'` prints nothing (`plans/` is excluded
because this plan quotes the old text). (No RED step: comment-only edits change
no behaviour, and the pinned literal the comment names was already asserted at `dffdf879`.)

### Step 8: the docs

1. `docs/features/mission-perf-heartbeat.md` line 47: replace
   `` `t` is seconds since the mission was created; the first line lands at +5 s. ``
   with
   ```markdown
   `t` is wall seconds since the behavior's `OnCreated`. The first window opens at the first
   `OnMissionTick`, so the first line lands one interval after that tick: `t=+6s` in the 2026-10-02
   custom and campaign battle logs, `t=+7s` in a tournament.
   ```
2. Same file, the Tests paragraph (its last line is `line format.`): append these two lines to
   that paragraph, directly after `line format.`, so the paragraph ends with them:
   ```markdown
   The line-format test is a twin pin with `tools/tests/test_perf_runs.py` (`PINNED_MISSION_PERF`),
   which parses the same literal: change both or neither.
   ```
3. Same file, replace the whole "## Reading an A/B" section (the heading and its paragraph, the
   file's last lines; if plan 028 added sections after it, replace only this section) with:
   ~~~markdown
   ## Reading an A/B

   `tools/perf_runs.py` does the arithmetic. Run the same scene several times with the change off and
   several times with it on, keep each log, then:

   ```
   python tools/perf_runs.py <logs...>
   python tools/perf_runs.py compare --a <logs with it off...> --b <logs with it on...> --scene <scene id>
   ```

   The first command prints one row per mission (a log may hold several). Each row reports the first
   window on its own as the spawn window, because `BattlePlayable` fires with `agents=0` and the spawn
   burst lands in that window, and steady-state numbers over the windows from `t=+30s` with active
   agents: median fps, `avgMs` and `p95Ms`, the worst `maxMs`, and `gc0`, `gc1`, `gc2` per minute.
   `compare` prints the median of each metric per group, the delta and the percentage, with N per
   group, and refuses groups whose `[PerfContext]` build (Debug or Release) or texture quality differ unless given
   `--allow-mixed`. Press F6 on the first frame so both sides are AI-controlled, and end each run at
   the same point: steady windows run to the end of the mission, routs included. Three runs per cell
   is the floor; AI battles vary. `gc2` should not climb faster with the change on.

   Read the flags before the numbers:

   | Flag | Meaning |
   |---|---|
   | `FRAME_CAP` | With four or more steady windows: at least half of them sit within 1 fps of one window's fps, or the windows with the most and the fewest active agents, 30% or more apart, ran within 1 fps of each other. A frame limiter (in game or in the driver) may have set the frame time; lift it and rerun |
   | `MEMORY_PRESSURE` | The mission's `[PerfContext]`, or a `[MemSample]` line written between the mission's start and the next mission's start (so the campaign map after a battle counts toward that battle), read `memLoad` of 80% or more |
   | `DIAG_ON` | `[PerfContext]` shows cost beyond the default diagnostics: `tickProfiler=on`, or a `diag=` token other than the seven that default on (`battleLoad`, `stallWatchdog`, `stallBundle`, `exitSampler`, `freezeSampler`, `memSampler`, `missionPerf`) |
   | `DIRTY_BUILD` | The `[BuildStamp]` TAOM stamp ends in `.dirty`: the build held uncommitted edits, so say what was measured |
   | `BUILD_PAIR_MISMATCH` | The `[BuildStamp]` line says `MISMATCH`: TAOM and TAOM.Dependencies were built more than 12 hours apart |

   Exit codes: 0 when rows were found, 1 when no mission was found, 2 for a usage error, an unreadable
   file or a refused compare.
   ~~~
4. `tools/README.md`: replace
   ~~~markdown
   ---

   ## Content Generation
   ~~~
   with
   ~~~markdown
   ---

   ## Performance logs

   Offline reading of the performance lines in `taom_debug_*.log`, stdlib only, no game required.

   | Script | Purpose | CLI |
   |--------|---------|-----|
   | `perf_runs.py` | **One performance row per mission from any number of `taom_debug_*.log` files, and an A/B compare of two groups of runs** (read-only). Cuts each log into missions at `[BattleLoad] phase=MissionOpenNew` (with Battle Load Diagnostics off: at a `[PerfContext]` line or a restart of the `[MissionPerf]` clock), reuses `triage_battle_load.py`'s parser for the load buckets and `[MemSample]`, reports the spawn window (the first `[MissionPerf]` line) apart from steady-state medians over the windows from `t=+30s` with active agents, aggregates `[TickProfile]` and `[Hitch]` lines when present, and flags confounders: `FRAME_CAP`, `MEMORY_PRESSURE`, `DIAG_ON`, `DIRTY_BUILD`, `BUILD_PAIR_MISMATCH`. `compare` prints per-metric medians, delta, percentage and N per group, and refuses groups whose `[PerfContext]` build or texture quality differ unless `--allow-mixed`. Guide: `docs/features/mission-perf-heartbeat.md` "Reading an A/B". Tests: `tools/tests/test_perf_runs.py`. | `<log> [...]`, `--json`; `compare --a <logs> --b <logs>`, `--scene`, `--allow-mixed`, `--json`; exit 0 rows, 1 no mission, 2 usage, unreadable file or refused compare |

   ---

   ## Content Generation
   ~~~
5. Re-read every sentence you added against the code it describes (`perf_runs.py` constants
   `STEADY_FROM_S`, `MEMORY_PRESSURE_PCT`, `FRAME_CAP_*`, the exit codes in `main`,
   `BuildStampReport.cs` lines 55 and 84). They are drafts until you have.
6. `python tools/lint_docs.py --fail-on-drift; echo "exit=$?"`.

**Verify**: lint exits 0 and its summary shows "Em/en dashes in newly written prose: **0**";
`git grep -n "lands at +5 s" -- ':!plans'` prints nothing.

### Step 9: full verification

1. `TEMP="<tmp>" TMP="<tmp>" dotnet test TAOM.Tests -p:DisableModuleCopy=true -p:ModuleId=` (600000 ms).
2. `timeout 900 python -B -m unittest discover -s tools/tests -t . > "<your scratch folder>/py-after.log" 2>&1`,
   then compare its `Ran N tests` and `FAIL:`/`ERROR:` names with Step 1's.
3. `git status --porcelain`, compared with the one you recorded in Step 1.

**Verify**: dotnet prints exactly the totals line Step 1 recorded (this plan adds no C# test) with
the same failing tests; the Python run is Step 1's `N` plus 43 with the same failure names;
`git status` lists the six in-scope paths and nothing that was not already there in Step 1.

### Step 10: commit

Stage the six paths by name, write the message per "Git workflow", commit with `git commit -F`.

**Verify**: `git log -1 --format=%s` shows the subject; `git show --stat HEAD` lists exactly the six
files.

## Test plan

- `tools/tests/test_perf_runs.py`, 43 tests in eight classes:
  - `PinnedLineTests`: the exact `[MissionPerf]` sample parses to its numbers, with and without the
    FileLogger prefix (the twin of `FrameStatsTests.BuildLine_FormatsEveryFieldInvariantly`); plan
    028's three literals parse to their numbers (the twins of its `TickProfileLinesTests`
    `*_MatchesThePinnedLiteral` methods), and `na` reads as None.
  - `SegmentTests`: one mission with its six load buckets; three missions in one log; a log without
    the new lines; the orphan `EncounterStart` regression (from the 2026-09-29 campaign log);
    heartbeat-only logs split on a `t` restart and on `t` lagging the wall clock; a pause stays one
    mission; `[PerfContext]` opens a mission when `[BattleLoad]` is absent and stays inside one when
    it follows `MissionOpenNew`; the heartbeat-disabled line is noted, not malformed; a mission that
    never ticked is a row with no windows.
  - `SteadyStateTests`: the spawn-window exclusion; windows before 30 s or with `active=0` are not
    steady; medians, max and GC per minute (18 collections over 15 s is 72 a minute); no steady
    windows gives None, never 0.
  - `FlagTests`: each flag positive and negative (`FRAME_CAP` by plateau, by agent spread, not when
    fps moves, not with under four windows; `MEMORY_PRESSURE` from `[MemSample]` and from
    `[PerfContext]`; `DIAG_ON` silent for the seven default diagnostics and for `diag=none`, raised
    by `tickProfiler=on` or an extra token; `DIRTY_BUILD` and `BUILD_PAIR_MISMATCH`).
  - `TickAndHitchTests`: per-behaviour ms per second, share of the managed phases, KB per second;
    hitches counted by their dominant phase.
  - `MalformedTests`: malformed lines skipped with a count, inside and before a mission.
  - `CompareTests`: medians, delta, percentage and N per group; refusal on mixed build and mixed
    texture quality, `allow_mixed` overriding; flag counts per group.
  - `CliTests`: exit 0 with JSON that parses, the text row, exit 1 with no mission, exit 2 for an
    unreadable file and for no arguments, compare exit 0, refusal exit 2, a scene filter that
    empties a group exits 1.
- Pattern: `tools/tests/test_triage_battle_load.py` (real-format fixtures, twin-pin literals, CLI by
  `subprocess`).
- Not testable offline: nothing in the tool. If 028 has not landed, its three pins have no C#
  partner yet (the commit's `Not-tested:` trailer).

## Done criteria

Machine-checkable. ALL must hold:

- [ ] `python -B -m unittest tools.tests.test_perf_runs` prints `Ran 43 tests` and `OK`
- [ ] `python -B -m unittest tools.tests.test_triage_battle_load` prints `Ran 153 tests` and `OK`,
      and `git diff --stat dffdf879..HEAD -- tools/triage_battle_load.py tools/tests/test_triage_battle_load.py` is empty
- [ ] The mutation script prints `mutants killed: 8 of 8`
- [ ] The full Python suite has Step 1's failure set and 43 more tests
- [ ] `dotnet build Main/TAOM.csproj -p:DisableModuleCopy=true -p:ModuleId=` exits 0
- [ ] `dotnet test TAOM.Tests -p:DisableModuleCopy=true -p:ModuleId=` prints the totals line Step 1
      recorded, unchanged, with the same failing tests (at `dffdf879`: only
      `EveryLanguage_DeclaresARowForEveryEnglishKey`)
- [ ] `python tools/lint_docs.py --fail-on-drift` exits 0 with 0 dashes in new prose
- [ ] `git grep -n -e "the parser in <c>tools/</c>" -e "lands at +5 s" -- ':!plans'` prints nothing
- [ ] `grep -nP "[^\x00-\x7F]" tools/perf_runs.py tools/tests/test_perf_runs.py` prints nothing
- [ ] Before the commit, `git status --porcelain` adds only the six in-scope files to Step 1's
      list; after it, `git show --stat HEAD` lists exactly those six
- [ ] Every comment, doc line and test oracle this plan supplied was re-checked against the code it
      describes

## STOP conditions

Stop and report (do not improvise) if:

- An excerpt in "Current state" does not match the live file (beyond plan 028's pre-cleared drift).
- Reusing `triage_battle_load.py` per mission turns out to need a change to it, or its 153 tests
  stop passing unchanged. Report what you needed and why.
- The real logs (Step 6) show a mission boundary the three rules miss: two missions merged into one
  row, or one mission split into two. Report the log, the line numbers and the row output.
- Plan 028 has landed and a literal its `TickProfileLinesTests` asserts differs from this plan's
  matching `PINNED_*` literal (Step 2).
- A test in Step 4 fails and the fix would change the test rather than the tool.
- The mutation check reports `SURVIVED` or `SOURCE TEXT NOT FOUND`.
- A step's verification fails twice after a reasonable fix.
- The work seems to need an out-of-scope file, especially `Main/IoC.cs`, `Main/SubModule.cs`, the
  csproj or a protected file.
- The commit hook judges the main checkout instead of your worktree (its message names a path
  outside your worktree).

## Orchestrator steps (not the executor's)

- Issue: file it before dispatch (none exists for this work).
- `/localize`: none; the tool prints no player-facing text.
- Feature doc: `docs/features/mission-perf-heartbeat.md` is extended in Step 8; no new feature doc
  and no feature-map row (no new feature).

## After merge: the maintainer's actions

None required. To use it: `python tools/perf_runs.py <taom_debug logs>` for rows, and
`python tools/perf_runs.py compare --a <off logs> --b <on logs> --scene <id>` for an A/B. Lift the
external frame cap before an fps A/B, or every row will carry `FRAME_CAP`.

## Maintenance notes

- **Plan 028 contract**: `PINNED_TICK_PROFILE`, `PINNED_HITCH` and `PINNED_PERF_CONTEXT` are plan
  028's three literal pins, character for character, so its `TickProfileLinesTests` and this
  plan's `PinnedLineTests` assert one set of strings. A change to either side changes both, by
  decision. Whichever plan lands second re-checks (Step 2 here); the review diffs the two sets.
- **`DIAG_BASELINE`** is the seven `diag=` tokens 028 can write, all default on. If a later change
  adds a token that defaults on, add it to `DIAG_BASELINE` too, or `DIAG_ON` fires on every row of
  a default install again.
- **Flag thresholds** live as named constants at the top of `perf_runs.py` (`STEADY_FROM_S = 30`,
  `MEMORY_PRESSURE_PCT = 80`, `FRAME_CAP_MIN_WINDOWS = 4`, `FRAME_CAP_TOLERANCE_FPS = 1.0`,
  `FRAME_CAP_AGENT_SPREAD = 0.30`, `CLOCK_SLACK_S = 3`). `FRAME_CAP` is a prompt to check the
  limiter, not a verdict: a steady GPU-bound scene can also plateau. On the 2026-10-02 logs it fired
  on the 116 to 117 fps battles and the 117 fps town visit, and not on the session that wandered
  between 117 and 131 fps or the one at 240 to 260 fps.
- **Coupling to triage**: per-mission load buckets depend on `classify_phase_timings` anchoring on
  the last mission-start marker of the text it is given. If triage changes that anchor, rerun
  `test_orphan_encounter_between_missions_keeps_the_previous_load_buckets` and
  `test_one_mission_gives_one_row_with_its_load_buckets`.
- **Review probes**: the three boundary rules in `split_missions` and `_starts_new_mission`;
  `_load_text` (cut at the first `BattlePlayable`); `None` never rendered or averaged as 0
  (`_number_or_na`, `_median`, `_per_minute`); `compare_rows` refusal on the union of both groups;
  the regexes against the C# format strings.
- **Deferred**: `docs/features/culture-doctrine.md` "Verification: the A/B protocol" could name the
  tool; a per-mission `--mission <kind>` filter for compare (today `--scene` is the filter) waits for
  a real need.

## Amendment (orchestrator, 2026-10-03; binding)

1. **Generic tags.** Besides the tags this plan parses specifically, collect every other line inside a
   mission segment whose tag is in square brackets and whose body is `key=value` tokens (for example
   `[TickSummary]` from plan 028's amendment, and later `[AnimMem]`, `[MapProfile]`, `[LoadXml]`,
   `[PatchApply]` from plans 036 to 041): keep them on the row as `extra_tags` (tag, timestamp, a dict of
   its key=value pairs), print a one-line count per tag in the text report, and include them in `--json`.
   No specific columns for them in this plan. A line that matches no known tag and has no key=value body
   is not an error and is not counted as malformed.
2. **Comprehensive tool output** (the maintainer's instruction, DECISIONS D6): the report starts with a
   header naming each log read (path, size, line count, missions found); it states, per log, how many
   lines it could not parse for a known tag, with the first five verbatim; and every confounder flag it
   raises names the evidence (for FRAME_CAP the fps value and the windows; for MEMORY_PRESSURE the
   `[MemSample]` line). Never a silent skip. `--json` carries the same.
