# RCA: removal of the battle-load stall watchdog (2026-11-12)

## Top-line

The maintainer removed `BattleLoadStallWatchdog` because a slow, cold-cache shader load was reported as a
crash (a player log held 1,388 `compile_shader` lines in the 301 s the watchdog waited). The removal built and
passed the C# suite. `/deep-review` (tier 1: data flow, engine compatibility, tooling; the Codex pass was
waived because no Codex usage is available) found no CRITICAL or HIGH defect and no runtime regression. It found that consumers
outside the C# build still described, pinned or advised on the removed output. The commit went out first
(045e7bc42, at the maintainer's request); the fixes are in the follow-up commit that carries this file.

## Findings

| # | Sev | Finding | Category | Why missed | Preventive action |
|---|---|---|---|---|---|
| 1 | MEDIUM | `.github/ISSUE_TEMPLATE/battle-load-hang.md` told players a crash ZIP arrives after about 5 minutes and asked only for `rgl_log_errors_*.txt`, while the triage tool's RENDER_WAIT step needs the full `rgl_log_<pid>.txt` that the ZIP used to carry | Stale consumer of a removed output | The sweep searched class and setting names. The template names neither: it states the promise ("the stall watchdog makes one") | Search the removed output by its claim and artifact words (`watchdog`, `bundle`, `ZIP`) in `.github/`, `tools/` and `docs/` |
| 2 | MEDIUM | The C# pin of the `[PerfContext]` line (`TickProfileLinesTests`) lost two `diag=` tokens; the Python twin pin in `test_perf_runs.py` kept all seven. Both files say "change both or neither" | Cross-language twin pin | No test reads the other language's file, so nothing went red; the Python test then covered only the old format | When a pinned literal changes, grep the repo for the comment that names its twin. Keep an old-format fixture beside the new pin (done: `OLD_PERF_CONTEXT`) |
| 3 | LOW | `triage_battle_load.py` still printed "the stall watchdog fires its bundle on exactly that reading" on every cold-cache RENDER_WAIT verdict | Stale advice in a report generator | Same as 1 | Same as 1; the test now asserts the phrase is absent |
| 4 | LOW | `BattleLoadLoadingWindow.OpenedAtUtc` (with its field, two usings and two tests) had no production reader after the removal; `CurrentStatusLine` and `BattleLoadPhase.StallWatchdog` were dead for the same reason (found before the lenses) | Dead code left by a removal | The build cannot flag a public member that only a deleted class read | After a removal, list the members the deleted class read and grep each for another reader |
| 5 | LOW | A comment in `AppDomainExceptionHook` named `MissionTickStallWatchdog` as a worker-thread crash capturer (an open #650 follow-up); that class has no crash-service reference (grep, this session). `CrashReportService` also listed the watchdog as a capture source | Comment names the wrong class | The comment predated the split of the two watchdogs | The removal closes the #650 follow-up; both comments now say so |
| 6 | LOW | About ten code comments and eight doc passages (`battle-load-diagnostics.md`, the registry, the feature map, the index, `crash-report.md`, the bridge page, `long-defeat.md`) described the watchdog as running; the settings count in the feature doc said 24 where 21 are left | Stale prose | Same as 1 | Same as 1 |
| 7 | NIT | The render-wait hook comment cited v1.4.8 engine lines; the engine lens re-verified them on v1.5.7 and the comment now cites those | Out-of-date citation | Not part of the removal | None |

## Root-cause pattern

The removal was searched by identifier. Its consumers named the output instead: a promise in a player template,
a log token pinned in a second language, an advice sentence in a report. A compile, a class-name grep and a green
C# suite cannot reach those. The Python test that should have caught finding 2 pinned the old format, so it stayed
green.

## Why each agent missed it

- **Standards, completeness, design lenses:** not run at tier 1 (the table launches data flow, engine compatibility
  and tooling for this change). Findings 1, 3 and 6 are completeness items; the data flow lens found them.
- **Engine compatibility lens:** correct to find no engine defect; it reported the stale comments it passed.
- **Codex:** did not run (no usage available); the maintainer waived it.
- **The orchestrator's own sweep:** grepped identifiers (`BattleLoadStallWatchdog`, `EnableStallWatchdog`,
  `SuppressStallDetection`) and the two token names in docs, not the promise words in `.github/`.

## Feedback memories to codify

None. The durable rule is the entry in `docs/reviews/lessons/misc.md`.

## Follow-ups

These are pre-existing gaps or decisions for the maintainer, found by the lenses and not caused by this change.

- FOLLOW-UP: `_render_silence` in `tools/triage_battle_load.py` used the watchdog line as its anchor; with the memory sampler off, a frozen render wait now reads as a cold cache. no issue: it changes a pinned reading and needs the maintainer's call; batched with the other unfiled items.
- FOLLOW-UP: the PRE_SCENE fallback in `tools/triage_battle_load.py` reports late phases (`TaomBehaviorAdded`, `MissionAfterStartDone`, `AgentBuildDone`) as "froze before scene selection". no issue: pre-existing, found by the tooling lens probe; batched.
- FOLLOW-UP: `tools/triage_battle_load.py` given a directory exits 1 with a traceback, where its docstring promises 2. no issue: pre-existing and minor; batched.
- FOLLOW-UP: the dedicated-server settings reader warns "unknown key ... (keys are case-sensitive)" for a removed key, which misleads an operator who still has one of the three removed keys in the file. no issue: only operators who set such a key see it; batched.

The Codex pre-review of this removal was waived by the maintainer (no Codex usage). The issue is
[#1123](https://github.com/haterade22/TAOM/issues/1123); its in-game check is still owed (label `triage-needs-ingame`).
