# In-game verdicts

## Overview

TAOM writes a `[Check]` line when a feature does what it was built to do in a real game, and a
`[Session]` summary that reports the totals of the outcome calls (a repeated call adds to the
total but writes no second line), hot-path counters and swallowed exceptions.
`tools/ingame_verdict.py` reads a play session's logs against one manifest per issue and gives each
owed in-game check a verdict: PASS, FAIL or NOT REACHED, with the log lines that prove it. A person
is needed only for what a log cannot show, such as how a screen looks.

## Why This Exists

- **Before:** a closed issue labelled `triage-needs-ingame` waited for someone to play, then
  describe what they saw. On 2026-10-31, 252 issues carried the label (open and closed, as
  `--owed` lists them), and much of what they owe can be proved from a log line.
- **Requirement:** a verdict that is read from the logs, tied to the build that wrote them, and
  that tells the tester what to do next time when the path was never played.
- **Without it:** owed checks pile up, an empty `catch` hides a broken feature behind a clean log,
  and nobody can tell from a log how a session ended.

## Architecture

### Design Challenge

The proof has to come from code that runs on the async AI thread, TWParallel workers and native
callbacks, in hot paths such as model queries, without costing a string or a disk write there.
The reader has to tell "it failed" from "nobody played it" from "this build predates the fix".

### Solution Approach

- **Emit:** `IOutcomeLog` (`Main/Core/Logging/IOutcomeLog.cs`), implemented by `OutcomeLog` over
  `IModLogger`. Services take it by constructor; static Harmony patches and code that runs before
  IoC call the `Outcomes` facade in the same file. No member throws.
- **Summarise:** `WriteSummary(reason)` runs at four seams: `save` (`SaveManager_Save_Patch`),
  `exit` (`SubModule.OnSubModuleUnloaded`, and `AppDomain.ProcessExit` through
  `Outcomes.SummariseOnProcessExit`), `crash` (`CrashReportService`) and `heartbeat` (every 10
  minutes of real time from `OnApplicationTick`, so a debugger stop or a native crash still leaves a
  summary at most one interval older than the last application tick). The first heartbeat comes 10
  minutes after the module loads, and none is written while the main thread hangs. An exit summary
  is written once per process.
- **Read:** `tools/ingame_verdict.py`, with one TOML manifest per issue in `tools/ingame_checks/`.
  Evidence counts only from a session whose `[BuildStamp]` sha contains the manifest's `fixed_in`
  commit (`git merge-base --is-ancestor`).
- **Run:** `/verify` Step 6 and `/release` Phase 5 run
  `python tools/ingame_verdict.py --owed --sessions 10`, so a two-session check such as #1011 can
  pass. Both report and never block. (Those two skill hunks are plan 139 Part D, owed until the
  harness plans land; until then run the command by hand.)

### Component Diagram

```
feature code ── IOutcomeLog / Outcomes ── IModLogger ── taom_debug_*.log
  [Check] id=... result=pass|fail|skip ...        [Session] summary / counter / swallowed
                                                         |
tools/ingame_checks/<issue>.toml ── tools/ingame_verdict.py ── gh (triage-needs-ingame)
                                                         |
                                     /verify Step 6, /release Phase 5
```

## Configuration

### Config File: `tools/ingame_checks/<issue>.toml`

One manifest per issue, UTF-8, read with `tomllib`. Patterns are regexes in TOML literal strings
(single quotes), searched one log line at a time.

| Field | Type | Description |
|-------|------|-------------|
| `issue` | int | Must equal the file name's number |
| `title` | string | Optional, printed in the report |
| `fixed_in` | string | A commit sha; evidence counts only from a build that contains it |
| `logs` | list | Log kinds to read: `taom`, `taom-ds`, `diag`, `rgl`, `coop-client`, `coop-server`, `ds`; `taom` or `taom-ds` must be one |
| `allow_dirty` | bool | Count a `.dirty` build's evidence (default false) |
| `[[check]] id` | string | The check id, `^[a-z0-9]+(\.[a-z0-9-]+)+$` |
| `[[check]] needs` | string | What the tester does; printed whenever the check is not PASS |
| `[[check]] scope` | regex | Optional; cuts each log into one segment per match, judged apart (needs a single log kind) |
| `[[check]] reached` | list | Lines that prove the path ran |
| `[[check]] pass` | list | Required; each must match at least `min` lines |
| `[[check]] fail` | list | Any match is FAIL |
| `[[check]] min` | int | Default 1 |
| `[[check]] absence` | string | `fail` (default) or `not-reached`: what "reached, no pass line" means |
| `[[human]] claim` | string | Printed as still owed by a person, never counted |

### Current Values

One manifest per issue
in `tools/ingame_checks/` (list them with `ls tools/ingame_checks`). `tools/tests/fixtures/ingame_verdict/manifests/970.toml`
and `981.toml` are copies of the 1.5.x manifests that drive the reader's own tests; #981 (co-op server
settings) does not apply to this line.

## Key Files

| File | Purpose |
|------|---------|
| `Main/Core/Logging/IOutcomeLog.cs` | The interface and its line formats and thread rules |
| `Main/Core/Logging/OutcomeLog.cs` | `OutcomeLog`, `OutcomeCounter`, the `Outcomes` facade, `SessionHeartbeat` |
| `tools/ingame_verdict.py` | The reader |
| `tools/ingame_checks/<issue>.toml` | One manifest per issue |
| `tools/ingame_ledger.py` | Lists the issues closed since the last tag that still owe a check |
| `.claude/skills/verify/SKILL.md` | Step 6 runs the reader |
| `.claude/skills/release/SKILL.md` | Phase 5 runs the reader before the release notes |

## Dependencies

- `IModLogger` (Core) and `FileLogger`, which flushes INFO and WARNING lines to disk synchronously
  on the calling thread.
- `PatchShield.SwallowedTotal` (TAOM.Dependencies), reported as `shield_swallowed=`.
- `gh` for `--owed` and `--post`, `git` for the build binding.

## Tests

- `TAOM.Tests/Core/Logging/OutcomeLogTests.cs`, `OutcomesTests.cs`, `OutcomeSeamsTests.cs`: the
  latch, the 256-key cap, counters across threads, the summary contents, the four seams.
- Feature emitters, each with a fake `IOutcomeLog`: `BattleActionBarChecksTests.cs`, `StaleCharacterParentRepairTests.cs`.
- `tools/tests/test_ingame_verdict.py`: the verdict rules, build binding, session pairing, the
  `[Session]` summary report, and `RepoManifestGateTests`, which loads every manifest and fails a
  `[Check]` id that no `.cs` file under `Main` holds as a string literal.

## How to verify in game

**Preconditions:** a build deployed from a commit that contains the fix (`fixed_in`) with no
uncommitted changes (a `.dirty` build counts only when the manifest sets `allow_dirty`), a game
session played far enough to reach the check (each check's `needs` says how), then the game closed
or left running.

1. `python tools/ingame_verdict.py --issue <N>`: the verdict per check, the reason, up to three
   evidence lines as `file:line  text`, and the session's `summary` line.
2. `python tools/ingame_verdict.py --owed`: every issue labelled `triage-needs-ingame` that has a
   manifest, plus a `no manifest:` list.
3. `--sessions 2` for a save made in one session and loaded in the next. More sessions never hide
   a newer result: per check the newest PASS or FAIL wins. (`--ds-data` and `--ds-engine` read a dedicated
   server's logs; no manifest on this line uses them.)

An unreachable step is reported with the command tried and the unmet precondition. Never mark it
verified through another path.

## How to read a verdict

- **PASS:** every pass pattern matched, at least `min` times each, and no fail pattern did.
- **FAIL:** a fail pattern matched, or every `reached` pattern matched with no pass line and
  `absence = "fail"`. The evidence lines show which.
- **NOT REACHED:** the path was not played, the build predates `fixed_in`, the build is `.dirty`
  without `allow_dirty`, or a log the check needs was not found. The reason says which, and `needs`
  says what to do next time. A `result=skip` line gives the feature's own reason (for example
  `reason=toggle-on`).
- An issue is PASS when every check is, FAIL when any check is, else NOT REACHED. Exit codes: 0 all
  PASS, 1 any FAIL, 4 otherwise or nothing judged, 2 a bad argument or manifest, 3 `git` missing or
  `gh` failed; a build sha `git` cannot compare leaves that session NOT REACHED.
- **The session block:** each TAOM log's last `[Session] summary` line, for example
  `reason=exit uptime_s=1864 checks_pass=2 checks_fail=0 swallowed=14 shield_swallowed=4 checks_skip=0 errors=0 dropped=0`.
  `reason=heartbeat` as the last one means the game is still running, or the process ended without
  an exit or crash summary (a debugger stop, a native crash or a kill).
  `errors=` above 0 prints a WARNING: an outcome-log call wrote nothing (usually a check id that
  breaks the id pattern), so a `[Check]` line may be missing. Each `swallowed:` line names a site
  whose exceptions were caught, with its count and first exception type. "no [Session] summary
  line" means a build from before #1071, a game still running in its first 10 minutes, or a process
  that ended within 10 minutes of loading without a save, exit or crash summary. None of this
  changes a verdict.

## How to add a check

1. **The id:** a `const string` in the feature, lower case, owner first:
   `public const string AiOffCheckId = "taom.castle-recruit.ai-off";`
   (`CastleRecruitmentService.cs`). The manifest gate finds it as a string literal.
2. **The call:** a service takes `IOutcomeLog` by constructor; a static patch calls `Outcomes`.
   `Pass`, `Fail` or `Skip(id, key, detail)` writes `[Check] id=<id> result=<result> <detail>` the
   first time `(id, result, key)` is seen in the process, then only counts. `key` is the scope: `""`
   once per process, a mission serial once per mission, the campaign day once per day. `detail` is
   `name=value ...` with numbers formatted in the invariant culture. Call `Skip` with a `reason=`
   where the path ran but the feature was off, so NOT REACHED can say why. Write the fail branch
   too: a check that can only pass proves little.
3. **Test it** with a fake `IOutcomeLog`: one test per pass, fail and skip branch.
4. **The manifest:** `tools/ingame_checks/<issue>.toml` with `fixed_in` set to the fix commit the
   issue names (on this line
   the commit whose body carries `Ported-from:` for the 1.5.x fix, never a 1.5.x sha). A build
   between the fix and the emitter writes no `[Check]` line, so every `reached` pattern must be a
   `[Check]` line, or the check sets `absence = "not-reached"`; otherwise `absence` defaults to
   `fail` and such a build gets a false FAIL. Then the `[Check]` pattern with each dot escaped (`id=taom\.castle-recruit\.ai-off`),
   a `needs` a tester can follow, and `[[human]]` items for what a log cannot show. Pick
   `absence = "not-reached"` when a session can end (deployment, a crash, a kill) before the pass
   line is due.
5. Run `python -m unittest tools.tests.test_ingame_verdict`; `RepoManifestGateTests` fails until
   the id is in the source.

## Performance

**Hot paths take counters only.** Per hit, per agent, per tick, per model query or per frame, hold
an `OutcomeCounter` in a field (`_aiBlocked = outcomes.Counter(AiBlockedCounterId)`) and call
`Pass()` or `Fail()`: one `Interlocked` increment, no string, no I/O. The summary reports it as
`[Session] counter id=<id> pass=<n> fail=<n>`. `Pass`, `Fail` and `Skip` write a line, and
`FileLogger` flushes it to disk on the calling thread, so they run on the main thread
(`OnMissionTick`, or `DeferredCallbackQueue.RunOrDefer`) and only where the line is due. Build
`detail` only on the path that writes it. Off the main thread, use counters and `Swallowed` only.

## Swallowed exceptions

`Swallowed(feature, site, ex)` replaces an empty `catch`: one `[Swallowed]` WARNING line per
feature and site for the process, with the root cause, then a count that the summary reports as
`[Session] swallowed feature=<f> site=<s> n=<n> first=<type>`. The fallback behaviour of the catch
stays as it was. A catch that is silent on purpose carries `// swallow-ok: <reason>` instead; the
ratchet test that enforces the pair is `SilentCatchRatchetTests` (#1073).

## Changelog

- 2026-11-05: ported to the 1.4.x line by plan 139: `IOutcomeLog`, the reader, and check lines for the fixes this line carries (#1071 to #1074).

## GitHub Issue

- **Issue:** #1069, [In-game verdicts: logs that prove what works, read by a tool](https://github.com/haterade22/TAOM/issues/1069);
  this doc and the workflow: #1074
- **Status:** Open (1.4.x port: plan 139)
