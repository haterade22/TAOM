Plan 029: a TAOM-owned log tool that turns any number of taom_debug logs into one row per mission and
compares A/B groups of runs, so a frame-time or memory change is judged by numbers, not by feel.

WHY. No parser for `[MissionPerf]` exists anywhere in tools/ (grep 'MissionPerf' in tools: 0 files),
although Main/Features/MissionPerf/MissionPerfLine.cs:6-7 says "the parser in tools/ has a single
fixture to match". tools/triage_battle_load.py reads only the LAST mission in a log
(`_last_mission_segment`, tools/triage_battle_load.py:812-842), so N runs in one log give one row. The
2026-10-02 logs show why the tool must also flag confounders: 43 [MissionPerf] windows sat at 116 to 117
fps across 83 to 273 agents (a frame cap outside the game: engine_config has max_framerate 360,
force_vsync 0), one session ran at memLoad 82 to 84% while another ran at 46%, and BattlePlayable fires
with agents=0 so spawning lands in the first [MissionPerf] window (gen0 31 to 41 there against 3 to 7
later).

LINE FORMATS (the contract):
- Existing, from MissionPerfLine.Build (MissionPerfLine.cs:11-17):
  `[MissionPerf] t=+65s frames=300 fps=60.0 avgMs=16.67 p95Ms=25.50 maxMs=40.3 agents=812 active=640 formations=9 gc0=12 gc1=3 gc2=1`
- Existing [BattleLoad], [MemSample] and [MemStation] lines: parse them by importing tools/triage_battle_load.py's
  functions (parse_battle_load_log, its dataclasses, classify_phase_timings), never by copying them; if a
  function is needed per mission rather than for the last mission, add a small reusable helper there
  (in scope) with its own tests.
- New, defined by plan 028 (written in parallel; the parser must accept logs without them):
  `[TickProfile] t=+<s>s frames=<n> wallMs=<x> preDisplayMs=<x> missionTickMs=<x> preTickMs=<x> waitTickMs=<x> agentTickMs=<x> otherMs=<x> allocKB=<x|na> top=<Type>:<ms>/<calls>/<maxMs>/<KB>,<Type>:...`
  `[Hitch] t=+<s>s frameMs=<x> preDisplayMs=<x> missionTickMs=<x> preTickMs=<x> waitTickMs=<x> agentTickMs=<x> otherMs=<x> gc0=<n> gc1=<n> gc2=<n> allocKB=<x|na> top=<Type>:<ms>,<Type>:<ms>,<Type>:<ms>`
  `[PerfContext] build=<Debug|Release> jitOptimized=<true|false> clr=<version> serverGC=<bool> latency=<mode> missionInProcess=<n> scene=<id> agents=<n> textureQuality=<n|na> shadowQuality=<n|na> particleDetail=<n|na> ragdolls=<n|na> memLoad=<pct|na> availPhysMB=<n|na> tickProfiler=<on|off> diag=<list|none>`
  Each log line has the FileLogger prefix `[YYYY-MM-DD HH:MM:SS] [LEVEL] ` before the tag (see any
  taom_debug log); the parser keys on the tag, not the position.

WHAT THE TOOL DOES (tools/perf_runs.py, stdlib only, Python 3 as the repo's tools use):
1. `perf_runs.py <log> [<log> ...] [--json]`: one row per mission segment in every log (segment
   boundaries: the [BattleLoad] mission-open phase, or the first [MissionPerf] t=+5s after a gap; the
   writer reads the logs' real phase names and picks a boundary that holds for custom battles,
   tournaments and campaign battles; a log may hold several missions). Row fields: log name, scene, build
   stamp line if present, [PerfContext] fields if present, load buckets (reuse triage's phase timing per
   segment), first-window maxMs and gc0 (the spawn window), steady-state stats over windows from t >= 30 s
   with active > 0: median fps, median avgMs, median p95Ms, max maxMs, gc0/gc1/gc2 per minute, agents max;
   [TickProfile] aggregated over the segment: per behaviour total ms per second of wall time and share;
   [Hitch] count and, per hitch, the phase holding the most ms.
2. Confounder flags per row: FRAME_CAP when steady fps stays within 1 fps across windows whose agent
   count differs by 30% or more (or a fixed fps across all steady windows); MEMORY_PRESSURE when a
   [MemSample] in the segment shows memLoad >= 80; DIAG_ON when [PerfContext] diag lists diagnostics; and
   DIRTY_BUILD when the log has the BuildStamp MISMATCH line (find its exact text in the code).
3. `perf_runs.py compare --a <logs...> --b <logs...> [--scene <id>]`: per metric the median over each
   group's rows, the delta and the percentage, with N per group; refuse to compare groups that differ in
   [PerfContext] build or textureQuality unless --allow-mixed; print the confounder flags.
4. Exit codes: 0 rows found, 1 no mission found, 2 usage or unreadable file.

TESTS (tools/tests/test_perf_runs.py, unittest, synthetic fixture logs written in the test): one
mission, three missions in one log, a log without the new lines, the spawn-window exclusion, each
confounder flag (positive and negative), compare medians and refusal, malformed lines skipped with a
count. Twin pins: the exact sample [MissionPerf] line above must parse to its numbers, and the C# side
must assert the same literal: add (in scope) a test in TAOM.Tests/Features/MissionPerf/FrameStatsTests.cs
or a new MissionPerfLineTests.cs that builds that exact line from MissionPerfLine.Build, following the
existing cross-language twin pins in tools/tests/test_triage_battle_load.py:46-76 and their C# partners
(for example TAOM.Tests/Features/BattleLoadDiagnostics/MemoryPressureSamplerTests.cs). The three new
formats get Python-side literal pins only; plan 028 adds their C# twins.

ALSO IN SCOPE: fix MissionPerfLine.cs:6-7's comment to name the new tool; a short section in
docs/features/mission-perf-heartbeat.md "Reading an A/B" pointing to the tool; a row in tools/README.md.

STOP conditions to include: triage_battle_load.py's per-segment logic cannot be reused without changing
its existing outputs (its tests must stay green unchanged); the real logs' phase names do not allow a
reliable mission boundary.
