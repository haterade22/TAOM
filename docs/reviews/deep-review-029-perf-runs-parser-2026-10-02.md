# Deep review: plan 029, perf_runs.py mission rows and A/B compare (2026-10-02)

```
DEEP REVIEW REPORT
===================
Feature: plan 029, tools/perf_runs.py: one row per mission from taom_debug logs, A/B compare,
         confounder flags; the [MissionPerf] twin pin
         (branch perf/029-perf-runs-parser, 0d1e91f0..63cc79a7)
Date: 2026-10-02

Scope:   Python tool and tests (tools/perf_runs.py, tools/tests/test_perf_runs.py), two C# doc
         comments, docs (mission-perf-heartbeat.md, tools/README.md). No XML, no harness files.
Blast radius: NOT IN SCOPE for C# (two comments, no type changed). The tool's only consumers are
         its tests and the doc; triage_battle_load.py is imported, not changed.
Waves:   Wave 1: Agent 1 (Standards), Agent 4 (Completeness), Tooling correctness.
         Agents 2, 3, 5, 6 and 7: NOT IN SCOPE (no engine, runtime, data-flow or XML change).
         Codex: not run (no paid dispatch for this item).
         Review lead (this report): verification, fixes, Step 4, RCA.

STANDARDS:     PASS on checks 1 to 10; 1 MEDIUM, 5 LOW, 2 nits: 5 fixed, 1 needs Mike (S6)
COMPATIBILITY: NOT IN SCOPE
EFFICIENCY:    NOT IN SCOPE
COMPLETENESS:  INCOMPLETE: GitHub issue drafted, not filed (needs Mike); test gaps fixed
DATA FLOW:     NOT IN SCOPE
DESIGN:        NOT IN SCOPE (one simplicity proposal from Tooling applied)
XML:           NOT IN SCOPE
TOOLING:       FAIL at review, fixed: 12 findings (2 MEDIUM, 10 LOW): 7 fixed, 1 false positive,
               4 need Mike or are follow-ups
```

## DETAILS

Every finding was re-read against the worktree before it was classified: the tool and its tests at
`63cc79a7`, plan 028's writer at `b3bac1a9` (`git -C wt-028 show HEAD:Main/Features/MissionPerf/TickProfileLines.cs`
and `HEAD:TAOM.Tests/Features/MissionPerf/TickProfileLinesTests.cs`), the heartbeat's clock in
`MissionPerfHeartbeatBehavior.cs:44,53,69` and `FrameStats.ShouldEmit`, FileLogger's prefix
(`FileLogger.cs:85-93`), and the 30 installed `taom_debug_*.log` files. Finding ids F1 to F10 are
the RCA's, `docs/reviews/rca-perf-runs-parser-2026-10-02.md`.

**Line contract against plan 028 (orchestrator note).** Verified independently of the lenses:
`PinnedTickProfile`, `PinnedHitch` and `PinnedPerfContext` equal this branch's `PINNED_*` strings;
every key `TickProfileLines.cs` writes is one the parser reads; `na` (allocKB, per-behaviour KB,
context fields), `top=none`, `agents=-1`, `scene=unknown` and `diag=none` all parse. The one
mismatch was on the test side: the `[TickSummary]` fixtures used `windows=` and a `[Hitch]`-style
`top`, which 028 never writes (F2). `PINNED_TICK_SUMMARY` is now 028's literal, character for
character, and its 13 fields are asserted. All 11 `[TickProfiler]` status line shapes, built as
`StatusLines_NeverContainADataTag` builds them, are now fed to the parser inside a mission:
malformed 0, `extra_tags` empty (F3).

### Agent 1: Standards

| Finding | Verdict | Action |
|---|---|---|
| S1 MEDIUM `[TickSummary]` fixtures not 028's line; no fourth twin pin | CONFIRMED (F2): fixtures at `test_perf_runs.py:514-516, 632-633` wrote `windows=12`; 028 writes `frames=... hitches=... worstHitchT=...` | `PINNED_TICK_SUMMARY` added with the twin-pin comment naming `BuildTickSummary_SampleMission_MatchesThePinnedLiteral`; both fixtures use it; all 13 fields and the `worstHitchT=na` variant asserted |
| S2 LOW `[TickProfiler]` status lines never reach the report | CONFIRMED as described (`perf_runs.py:354-371` falls through to a plain append) | Characterisation test applied (F3). Surfacing them is a cross-plan decision: NEEDS MIKE |
| S3 LOW doc promises gc1 per minute, the text row omits it | CONFIRMED (F7): `perf_runs.py:638-639` | Row prints `gc1/min`; asserted |
| S4 LOW doc date wrong for the campaign battle | CONFIRMED (F9): `grep -c "mission='Battle'"` is 0 in every 2026-10-01 and 2026-10-02 log; only `taom_debug_2026-09-29_08-29-26.log` has 2 | Reworded to "2026-09-29 to 2026-10-02" |
| S5 LOW docstring says `[MemSample]` is never re-implemented, but `_MEMLOAD_RE` re-parses it | CONFIRMED (F6) | Code made to match the docstring: the evidence line now comes from `triage_battle_load.parse_battle_load_log`, `_MEMLOAD_RE` deleted |
| S6 LOW commit trailers of `63cc79a7` do not parse | CONFIRMED: `git interpret-trailers --parse` prints nothing | Not fixable without rewriting the reviewed commit: NEEDS MIKE. This review's commit keeps its trailers in one final paragraph |
| Nit: `mission-perf-heartbeat.md:83` is 111 characters | CONFIRMED | Paragraph reflowed |
| Nit: example block drops lines with no ellipsis | CONFIRMED | "lines cut to `...`" and a closing `...` line |
| Follow-up: `aggregate_ticks` understates a behaviour hovering near rank N | Plausible; magnitude UNVERIFIED (no profiled log exists) | NOT APPLIED, NEEDS MIKE: the exact whole-mission totals in `[TickSummary]` could replace the sum once a profiled log exists |
| Follow-up: `tools/README.md:5` says every script supports `--dry-run` and `--apply` | Pre-existing | FOLLOW-UP |

### Agent 4: Completeness

| Finding | Verdict | Action |
|---|---|---|
| GitHub issue missing | CONFIRMED (drafted in the run's `issue-drafts.md`, section "## 029") | NEEDS MIKE: filing waits on the maintainer |
| MEDIUM parser side of 028's edge shapes unpinned; 6 mutants survive | CONFIRMED (F3) | `PlanTwentyEightShapeTests` (top=none on both lines, `na` KB, an unreadable context, the status lines, the shapes inside a mission). All 6 mutants now die |
| MEDIUM no test asserts the compare table or the row text; 7 mutants survive | CONFIRMED (F4) | `ReportTextTests`: the `fps_median` row exactly, the warning line, `load:`, `hitches:`, the disabled heartbeat, `... and N more unparsed`. All 7 mutants now die |
| LOW `[TickSummary]` fixture shape | Same as S1 (F2) | Fixed |
| LOW doc date | Same as S4 (F9) | Fixed |
| LOW "inside the mission" vs a segment that runs to the next mission start | CONFIRMED (F8): the real 11-38 row #4 carries `[MapLoad]` and `[SaveLoad]` | Docstring, feature doc and `tools/README.md` say "from this mission's start to the next mission's start" |
| Follow-up: `FrameStats.cs:54-55` still says "one interval into the mission" | Pre-existing, outside the plan's file list | FOLLOW-UP |
| Follow-up: 028's Tests paragraph should name `test_perf_runs.py` as the twin of its literals | Merge-time edit | For the orchestrator: whichever branch lands second makes it, together with "three data literals" becoming four in `TickProfileLinesTests.cs:11` |

### Tooling correctness

| Finding | Verdict | Action |
|---|---|---|
| 1 MEDIUM the spawn window counts as steady when its `t` is 30 or more | CONFIRMED (F1). The heartbeat's clock starts at `OnCreated` (`MissionPerfHeartbeatBehavior.cs:44,110`) and `FrameStats.ShouldEmit` opens the first window on the first tick, so window 0 lands one interval after the first tick; a long render wait puts it past 30 s and `perf_runs.py:487` then takes it into the steady stats | Steady windows are chosen by position (`windows[1:]`) and then by `t`. The lens's code (`start = windows[0].t - 5`) was not used: its own oracle (steady 6) contradicts it (it gives 2), and moving the 30 s anchor to the first tick is a separate design change (NEEDS MIKE). Five fixtures that opened on a `t=31` window, a shape no real log has, gained the real `t=6` spawn window |
| 2 MEDIUM 028's status lines vanish | CONFIRMED | APPLY part done (F3 test). Surfacing: NEEDS MIKE (same as S2) |
| 3 LOW/MEDIUM the `\bMissionOpenNew\b` guard has no test | CONFIRMED (F3): every real open is followed by `seq=2 t=+337ms phase=MissionOpenNewDone` | `test_mission_open_new_done_is_not_a_second_mission`; the no-`\b` mutant now dies |
| 4 LOW compare's refusal skips `na`, and the report then calls every row checked | CONFIRMED (F5) | `context_unchecked` counts rows with no context or `na` for build or texture quality; the warning says so |
| 5 LOW exit 0 when nothing was measured | Matches the documented contract ("0 rows found") | NOT APPLIED, behaviour-changing: NEEDS MIKE |
| 6 LOW exit 1 means both "no mission" and "crashed" | True; the crash needs a timestamp FileLogger cannot write (`DateTime.Now`, `FileLogger.cs:92`) | NOT APPLIED, behaviour-changing: NEEDS MIKE (with 5) |
| 7 LOW `[MemSample]` parsed twice by two rules | CONFIRMED (F6) | Fixed as in S5 |
| 8 LOW the FRAME_CAP rule is written twice | Re-verified: `frame_capped` and `frame_cap_evidence` compute the same plateau and spread tests | APPLIED in Step 4 (below) |
| 9 LOW docs say "inside the mission" | CONFIRMED (F8) | Fixed (wording). Closing the span at `ExitBegin` or `MapResumed`: NEEDS MIKE |
| 10 LOW `[TickSummary]` fixture | Same as S1 (F2) | Fixed |
| 11 LOW `NaN` and `Infinity` parse as numbers | CONFIRMED against the plan's contract (a non-number is malformed); reachability UNVERIFIED (028's values are finite by construction) | `_num` rejects non-finite values in every tick, hitch and context number |
| 12 LOW `--scene` is case-sensitive | FALSE POSITIVE: a scene id is an exact identifier, the row prints it as the log wrote it, and a mismatch fails loudly (exit 1, "no mission rows in group A") | None |
| Follow-up: clock rule one-sided; a forward wall-clock step splits a mission | Plausible, untested, rare | FOLLOW-UP |
| Follow-up: compare does not compare `diag` or `tickProfiler` | Design | FOLLOW-UP (NEEDS MIKE with the other compare items) |
| Follow-up: a valid `[TickProfile]` or `[Hitch]` before any mission is dropped uncounted | Unreachable with 028 (`FirstTick` writes `[PerfContext]` before the first `[TickProfile]`, `MissionTickProfilerBehavior.cs:105-107`) | FOLLOW-UP |
| Follow-up: 028 and 029 both edit `mission-perf-heartbeat.md` and will conflict | FALSE POSITIVE: `git merge-tree --write-tree` of 029 (before and after this review's commit) with 028 `b3bac1a9` exits 0 with no conflicted path, as Agents 1 and 4 also found | None |

### Mechanical checks run by the lead

- **Mutation, against scratch copies:** against HEAD's own 58 tests, the 14 mutants that apply to
  HEAD's code (7 misreading a real 028 line, 7 misprinting the report) all survive
  (`Ran 58 tests ... OK` each). Against the 71 tests now, 20 mutants plus a no-change control: the
  control survives (`Ran 71 tests ... OK`), 19 die. The survivor is equivalent (the triage parser
  drops the line before the mutated condition is reached); the fix it guards was proven RED against
  HEAD's loose regex instead (`test_evidence_cites_only_a_mem_sample_line_the_flag_reads`).
- **RED first:** the six behaviour tests failed against `63cc79a7` (`Ran 71 tests`,
  `FAILED (failures=6)`), then passed.
- **Real-log parity:** HEAD's tool and the fixed one over all 30 installed logs, `--json`: exit 0
  both, 16 rows both, malformed 0 both, `row field differences: 0`. No real row changes; the text
  report gains `gc1/min`.

## ACTION ITEMS

1. File the drafted plan 029 issue (Mike's word).
2. Decide how the `[TickProfiler]` warnings reach a row (028 writes `waitTickMs=na`, or 029 reads
   the status lines and flags the row).
3. At merge, whichever of 028 and 029 lands second: "three data literals" becomes four in
   `TickProfileLinesTests.cs:11`, and 028's Tests paragraph names `test_perf_runs.py`.

## IMPROVEMENTS (Step 4)

APPLIED:
- `tools/perf_runs.py` `frame_cap_evidence`: the FRAME_CAP rule and its evidence are one function
  that returns the evidence or None; `frame_capped` deleted (Tooling 8). Proven by the five FRAME_CAP
  tests (`FlagTests` and `FlagEvidenceTests`), green before and after, and the spread mutant
  (`frame_cap_spread_dropped`) dies.

NOT APPLIED:
- Tooling 1 alternative: the 30 s steady anchor measured from the first tick: behaviour-changing,
  NEEDS MIKE.
- Tooling 5 and 6: exit codes for "nothing measured" and "crashed": behaviour-changing, NEEDS MIKE.
- Tooling 9 fuller fix: close the mission span at `ExitBegin` or `MapResumed`: behaviour-changing,
  NEEDS MIKE.
- Surfacing `[TickProfiler]` warnings on the row (S2, Tooling 2): cross-plan design, NEEDS MIKE.
- `aggregate_ticks` from `[TickSummary]` totals (Standards follow-up): needs a profiled log; NEEDS
  MIKE.
- Compare checks `diag` and `tickProfiler` (Tooling follow-up): behaviour-changing, NEEDS MIKE.

FOLLOW-UP (pre-existing code, no issue filed: issues wait on the maintainer in this run):
- `Main/Features/MissionPerf/FrameStats.cs:54-55` "one interval into the mission" wording.
- `tools/README.md:5` claims every script supports `--dry-run` and `--apply`.
- The one-sided clock rule (a forward wall-clock step splits a mission) and orphan data lines
  before any mission.

## CODEX REVIEW

Codex not run: no paid dispatch was asked for this item.

| # | Codex Severity | Your Severity | Agree? | Reason |
|---|---------------|--------------|--------|--------|
| - | - | - | - | Codex not run |

**AGENTS.md lessons (pending):** none from Codex. The two testing lessons this review appended to
`docs/reviews/lessons/testing-qa.md` are candidates for the "Look harder here" list once the
orchestrator consolidates: a rule stated by position coded by a coinciding value, and a parser twin
pin that covers one sample literal but not the producer's edge shapes.

## Final suite

- Python: `Ran 3033 tests`, `FAILED (failures=3, skipped=8)`: exactly the three known base failures
  (`test_applying_every_spec_is_a_no_op`, `test_the_committed_career_file_is_what_the_rule_derives`,
  `test_default_is_on_the_e_drive`). Base: `Ran 3020 tests`, the same three. `tools.tests.test_perf_runs`:
  `Ran 71 tests`, `OK` (58 at base).
- dotnet: `Failed! - Failed: 1, Passed: 12345, Skipped: 2, Total: 12348`, identical to the base;
  the failure is `EveryLanguage_DeclaresARowForEveryEnglishKey` (translation rows, not this branch's).
- Hook suite not run: no hook, settings or `tools/test_hooks.sh` change. Gate sweep: none (no gate
  changed).

VERDICT: READY FOR COMMIT

## Convergence round 1

The convergence reviewer checked `63cc79a7..06584dd9` and raised three findings. Each was
re-verified against the code before it was fixed. All three were confirmed and fixed in the commit
that adds this section. All three are test-fixture defects; `tools/perf_runs.py` is unchanged.

| # | Sev | Finding | Outcome |
|---|-----|---------|---------|
| C1 | MED | The FRAME_CAP minimum-window boundary tests lost their spawn window when steady windows became positional | Fixed |
| C2 | LOW | `TICK_PROFILER_STATUS` no longer matched plan 028's status lines | Fixed |
| C3 | LOW | The derived no-hitch `[TickSummary]` fixture was a shape plan 028 cannot write | Fixed |

**C1 (fixed).** `test_no_frame_cap_with_fewer_than_four_steady_windows` and the empty
`flag_evidence` assertion in `test_every_raised_flag_has_evidence_and_no_other` built
`_mission(0, _steady(n=3))`, which has no spawn window. `summarize` drops `windows[0]` by position
(`perf_runs.py:494`), so only two steady windows reached the gate and `FRAME_CAP_MIN_WINDOWS = 3`
passed the suite. Both fixtures now lead with `dict(t=6)`. Proof, in a scratch copy of the tools
folder: with the mutant `FRAME_CAP_MIN_WINDOWS = 3`, the tests before the fix gave `Ran 71 tests`,
`OK`. After the fix they gave `Ran 71 tests`, `FAILED (failures=2)`, and the two failures are
exactly these tests. The unmutated control gave `Ran 71 tests`, `OK`.

**C2 (fixed).** The tuple held 11 lines from plan 028's first commit `b3bac1a9`. Five texts had
changed at 028's `e64529b3` and five shapes were missing. The tuple is rebuilt from `e64529b3`: the
16 lines `StatusLines_NeverContainADataTag` builds, then the six other variants
`StatusLines_MatchTheirPinnedLiterals` pins (22 lines, all distinct). A scratch check read
`TickProfileLinesTests.cs` with `git show` and found every one of the 17 pinned status lines in the
tuple. Every other entry is one of the five builder-computed lines, derived by hand from
`TickProfileLines.cs` at that commit. No entry carries a data tag or matches `_EXTRA_RE`. The
parser was already right: the old tuple could not catch a regression that lets `_EXTRA_RE` skip a
short prose lead-in, because none of its lines held a `key=value`. The new hitch-cap line
(`... reached at t=+412s: ...`) catches it. That mutant passed the old tuple (`Ran 71 tests`,
`OK`) and fails the new one (`FAILED (failures=1)`, the status-line test). The four data
literals (`PINNED_TICK_PROFILE`, `PINNED_HITCH`, `PINNED_PERF_CONTEXT`, `PINNED_TICK_SUMMARY`)
match `e64529b3` byte for byte, and every 028 data shape parses (`na` KB, `top=none`). Whichever
of plans 028 and 029 merges second re-syncs this copy.

**C3 (fixed).** The `no_hitch` variant set `allocKB=na` but kept numeric per-behaviour KB in `top`.
Plan 028 writes both from one `allocAvailable` flag (`TickProfileLines.cs` `AppendFullTop` and
`Kb`). It also pins the `na` form in `BuildTickSummary_NoHitches_WritesWorstHitchNa`. The test now
asserts the variant's `allocKB` and `top` fields. Before the fixture change that assertion failed
(`FAILED (failures=1)`, `/7680` and `/960` where `/na` was expected); the fixture now also replaces
both KB values with `na`.

**Context for ACTION ITEM 2 (no change made).** Plan 028 now writes at most 100 `[Hitch]` lines
per mission and reports the cap only in the hitch-cap status line, which this tool reads as prose.
A row's hitch count therefore stops at 100, while `[TickSummary] hitches=` (kept in `extra_tags`)
carries the full count. Deciding how a row should surface that belongs to the pending action item;
it is not new work here. The status-line contract in `plans/028-mission-tick-profiler.md` still
shows the older texts; that plan file belongs to plan 028's branch.

**Suite after the fixes.** Python: `Ran 3033 tests`, `FAILED (failures=3, skipped=8)`, the same
three known base failures. `tools.tests.test_perf_runs`: `Ran 71 tests`, `OK`. dotnet:
`Failed! - Failed: 1, Passed: 12345, Skipped: 2, Total: 12348`, identical to the base run
(`EveryLanguage_DeclaresARowForEveryEnglishKey`, translation rows, not this branch's). No gate
changed, so there is no gate sweep and the hook suite was not run.

## Convergence round 2

The convergence reviewer read `06584dd9..fc141af2` and raised one LOW finding, C4: the Not-tested
trailer of `fc141af2` does not parse as a git trailer, because its wrapped line starts without
whitespace. Not fixed: nothing in `tools/` or `.claude/hooks` reads trailers, and a fix rewrites
history, so it waits on the maintainer at merge (the RCA's "Convergence rounds").

## Orchestrator follow-ups after the review

- `50028d26`: three observations from running the tool over the 30 `taom_debug` logs on disk. A value
  with spaces (`registered=[ShieldWall*1.00, ...]`) was cut at its first space (489 values: the
  `[Doctrine]` lists only; round 3 below has the full count, which includes `[MapLoad]`) and key=value
  text inside a list became a key of its own; a multi-line entry's later lines had no timestamp (177
  entries); the top-level `--help` did not name `compare`. Four tests were red first; over the same
  logs the rows are identical outside `extra_tags`. Python suite `Ran 3037 tests`,
  `FAILED (failures=3, skipped=8)`, the three known base failures.
- The status tuple re-sync to plan 028's `7ca09cc2` (its three newest status line shapes).

Neither change has had a convergence review yet. Round 3 below is that review.

## Convergence round 3: the orchestrator's follow-ups

```
DEEP REVIEW REPORT
===================
Feature: plan 029 round 3, perf_runs.py extra-tag value rule, continuation timestamps, compare
         in the help, 028's newest status lines (branch perf/029-perf-runs-parser,
         fc141af2..f92a0bb4: 50028d26, 43f74556, f92a0bb4)
Date: 2026-10-02

Scope:   Python tool and tests (tools/perf_runs.py, tools/tests/test_perf_runs.py), the feature
         doc (mission-perf-heartbeat.md) and the review records. No C#, XML or harness file.
Blast radius: NOT IN SCOPE for C# (no type changed). The tool's consumers are its tests and the
         doc; nothing reads extra_tags fields today.
Waves:   Wave 1: Agent 1 (Standards), Agent 4 (Completeness), Tooling correctness.
         Agents 2, 3, 5, 6 and 7: NOT IN SCOPE (no engine, runtime, data-flow or XML change).
         Codex: not run (no paid dispatch for this item).
         Review lead (this section): verification, fixes, Step 4, RCA.

STANDARDS:     PASS on checks 1 to 10 (no C#), H4 prose and commit format; 1 MEDIUM, 6 LOW:
               F1 and F3 to F7 confirmed and fixed, F2 needs Mike (N1)
COMPATIBILITY: NOT IN SCOPE
EFFICIENCY:    NOT IN SCOPE
COMPLETENESS:  INCOMPLETE at review: issue unfiled (needs Mike, N2); the bracket rule and the
               continuation rule untested and the [MapLoad] change unrecorded: fixed
DATA FLOW:     NOT IN SCOPE
DESIGN:        NOT IN SCOPE
XML:           NOT IN SCOPE
TOOLING:       FAIL at review, fixed: 6 findings (2 MEDIUM, 4 LOW): 4 applied, T1 and T3's
               fallback not applied (behaviour changing, needs Mike)
```

### Details

Every finding was re-read against the worktree at `f92a0bb4` before it was classified: the tool
(`perf_runs.py:261-276`, `:348-352` at `f92a0bb4`), the writers the lenses cite
(`MapLoadHeartbeatService.cs:83-86`, `EnlistmentReconciler.cs:630` with
`PlayerPresenceSnapshot.Describe()`, `TeamDoctrineInstaller.cs:46`, `TaomBehaviorBase.cs:166`,
`TeamTacticProbe.cs:131,153`, `FileLogger.cs:90-95`), plan 028's `TickProfileLinesTests.cs` at
`7ca09cc2` and plan 040's `LoadTimeStampLines.cs` and pinned literals at
`perf/040-load-time-stamps` (`f1267a98`). Real-log claims were re-run over the 30
`taom_debug_*.log` files on disk with the base tool (`git show fc141af2:tools/perf_runs.py`) and
the worktree's, side by side.

| # | Sev | Lenses | Finding | Verdict | Resolution |
|---|-----|--------|---------|---------|------------|
| R1 | MEDIUM | A1 F1, T2, A4 F1 | No test guards the bracket depth: mutants M1 (depth never rises), M2 (only `[` tracked), M3 (no clamp at 0) and M4 (parentheses not tracked) each pass all 75 tests. The `[Doctrine]` bracket test went red first because of the space-led check (`Charge=` follows `{`), not the brackets | CONFIRMED (`Ran 75 tests ... OK` for each of M1 to M4) | Three tests on real shapes: `[MapLoad]` verbatim from a 2026-09-29 log, `[EnlistDiag]` as `EnlistmentReconciler.cs:630` writes it, and a stray `)` inside a `[Doctrine]` failure status. M1 to M4 now fail 1 to 3 tests each |
| R2 | LOW | A1 F1 and F7, T5, A4 F2 | The new rule folds `[MapLoad]`'s seven per-kind counts into `parties` (`2054(+2054) [lord=64 ... other=78]`) and `[EnlistDiag]`'s presence snapshot into `verdict`, and the records say "489 values" | CONFIRMED: over the 30 logs, 16 rows, 0 row differences outside `extra_tags`, 626 entries in the same order, 590 values changed (`[Doctrine]` 498, of which 9 `morale`; `[MapLoad]` `parties` 92) and 1018 keys dropped (`[Doctrine]` 374, `[MapLoad]` 644) | The fold kept: it loses no text, where the base kept `other=78]` as a value. Pinned by R1's tests and written into the module docstring, the function docstring and `mission-perf-heartbeat.md`; the counts corrected here, in the RCA and in the round 2 bullet above |
| R3 | LOW | A1 F3, T3, A4 F4 | An opener that never closes runs its value to the end of the line, so keys after it are kept as text; the base split them into keys. Reachable through `[Doctrine]` failure statuses, which embed a raw `ex.Message` (`TaomBehaviorBase.cs:166`) inside `formations=[...]` | CONFIRMED as an undocumented, untested behaviour. Reachability with a real message is UNVERIFIED (no such line in the 30 logs) | Documented and pinned (`test_an_unclosed_bracket_runs_its_value_to_the_end_of_the_line`). T3's fallback (rescan without brackets when one stays open) changes behaviour: NOT APPLIED, needs Mike (N3) |
| R4 | LOW | A1 focus 2, A4 F5 | The continuation timestamp is unpinned: M5 (update the entry stamp only on collected lines) passes all tests and would give a continuation an older, unrelated stamp | CONFIRMED (`Ran 75 tests ... OK` under M5) | `test_a_continuation_line_follows_the_newest_prefixed_line_even_an_uncollected_one`; M5 now fails it |
| R5 | LOW | A1 F4, T6 | The `[Doctrine]` fixture said "as a 2026-09-29 log writes it" but put `troops=1` after `registered=[...]`, which `TeamDoctrineInstaller.cs:46` writes last | CONFIRMED | The fixture is the 08:42:49 line verbatim, all nine fields asserted. A key after a list stays covered by the formations/taom test |
| R6 | LOW | A1 F5, A4 nit | The status tuple comment said `StatusLines_NeverContainADataTag` builds 16 lines; at `7ca09cc2` it builds 19 | CONFIRMED (`TickProfileLinesTests.cs:241-259` at `7ca09cc2`) | Says 19 |
| R7 | LOW | A1 F6, T4, A4 F6 | The help test depends on the terminal width: argparse wraps the epilog to `COLUMNS` | CONFIRMED: `COLUMNS=50` and `54` print the text on two lines; the test failed at 50 | Asserts on whitespace-normalised stdout; passes at 30, 50, 54 and 200 |
| R8 | LOW | A4 F2, A1 F1 | `mission-perf-heartbeat.md` said "next `key=`" without "space-led", and its reflow left a short line | CONFIRMED | Reworded with the rule's consequences; reflowed |

**Focus checks (orchestrator note), verified by the lead.**
- **Value rule against TAOM's writer shapes.** Plans 028 (`[TickSummary]`), 036 (`[AnimMem] t=`),
  039, 041 and 042's `type=` lines, plan 040's per-item lines and the existing `[MountSpawn]`,
  `[SiegeProps]`, `[SaveLoad]` and `[BattleSettings]` read the same under both rules (lens probes;
  the 30-log comparison above covers every shape on disk). Status and summary lines that start with
  prose stay uncollected. The shapes that change are `[Doctrine]` (intended), `[MapLoad]` and
  `[EnlistDiag]` (R2), an unclosed bracket (R3), and a bare word or prose after a value, which
  joins it: plan 040's `total` marker (N1).
- **Continuation timestamps.** FileLogger is the only writer of `taom_debug_*.log` and writes each
  entry with one `WriteLine` from one queue under its lock (`FileLogger.cs:90-109`), so the newest
  prefixed line above an unprefixed one is its own entry's head. `entry_timestamp` is local to
  `split_missions` and updates on every prefixed line before any branch (`perf_runs.py:348-352`),
  so it never crosses logs. Over the 30 logs all 177 collected continuation lines follow a
  `[Doctrine]` head (lens counts; 177 timestamps filled, 0 changed, re-run here). A file not
  written by FileLogger alone (joined by hand) is the residual case; no TAOM writer produces one.
- **Rows unchanged outside extra_tags.** Re-run here: 16 rows each, 0 malformed each, 0 rows
  differing outside `extra_tags`, 0 rows with a different tag order. The comparison after the fixes
  prints exactly the same output (no code line changed in this round).

### Codex review

Codex not run: no paid dispatch was authorised for this item, so there is no Phase 3d assessment.

| # | Codex Severity | Your Severity | Agree? | Reason |
|---|---------------|--------------|--------|--------|
| (none) | not run | | | |

**AGENTS.md lessons (pending)**, for the orchestrator's wrap-up (`.ai/review-reference.md` "Look
harder here"):
- A parser rule that decides where a value ends is checked against every producer's line shape,
  unmerged plans included, and its diff over a real corpus is counted per tag, not only for the tag
  that motivated it.
- A test that went red first proves the change mattered, not which guard did it; each guard line of
  a parsing rule needs its own killing test.

### Action items
1. N1 (needs Mike): plan 040's `total` marker. Either 040 writes it as a pair (`scope=total`) or
   029 ends a value at a bare letter-led word (T1's sketch). T1's rule would also cut a unit word
   after a number (`[CrashReport] vram=2.00 GB` reads `2.00`) and prose after a verdict. Whichever
   of plans 029 and 040 merges second re-checks the shape.
2. N2 (needs Mike): the plan 029 GitHub issue is drafted (`plans/_audit/2026-10-02-perf/issue-drafts.md`,
   "## 029"), not filed.
3. N3 (needs Mike): keep R3's behaviour (an unclosed bracket keeps its text) or take T3's fallback
   (rescan bracket-blind, which splits the rest into keys as the base did).

### Improvements (Step 4)

```
APPLIED:     tools/tests/test_perf_runs.py ExtraTagTests: three bracket tests (T2) and the
             continuation test, each killing a mutant that passed the committed 75 tests
             (mutate.py: M1 to M5 now FAILED, control OK); the fixture is the real line (T6).
             tools/tests/test_perf_runs.py CliTests: help asserted on normalised stdout (T4),
             RED at COLUMNS=50 first. Docstrings, feature doc and records reworded (T5).
NOT APPLIED: perf_runs.py:261-276 at f92a0bb4, T1 (a bare word ends a value): behaviour changing, also cuts
             unit words; needs Mike (N1). perf_runs.py:266-271, T3's bracket-blind fallback:
             behaviour changing; needs Mike (N3).
FOLLOW-UP:   perf_runs.py:107 `_EXTRA_RE` treats a data line whose body starts with a word as
             prose, so plan 036's `[AnimMem] summary:`, plan 040's `[LoadXml] summary`, plan 042's
             `[XmlMerge] summary` and `[MountSpawn] actionSet.IsValid=` never reach extra_tags.
             FileLogger.cs:92 formats `HH:mm:ss` with the current culture's time separator, so on
             a culture whose separator is not `:` no timestamp parses (which cultures: UNVERIFIED).
             TaomBehaviorBase.cs:166 and TaomTacticBase.cs:347 put a raw ex.Message in the status
             (a newline splits the line). TICK_PROFILER_STATUS needs a re-sync when plan 041 lands.
             No issue filed: this run files none; the orchestrator consolidates.
```

**Suite after the fixes.** `tools.tests.test_perf_runs`: `Ran 80 tests`, `OK`; the same module
against the base tool `fc141af2`: `FAILED (failures=9)`, the nine value, timestamp and help tests.
Full Python suite: `Ran 3042 tests`, `FAILED (failures=3, skipped=8)`, the three known base
failures (3037 at the base, five tests added). dotnet: `Failed! - Failed: 1, Passed: 12345,
Skipped: 2, Total: 12348`, identical to the base run (`EveryLanguage_DeclaresARowForEveryEnglishKey`,
translation rows, not this branch's). No gate changed, so there is no gate sweep and the hook suite
was not run.

VERDICT: READY FOR COMMIT (every confirmed finding fixed; N1 to N3 wait on the maintainer)

## Convergence round 1 after round 3 (df40fee6)

The convergence reviewer read `f92a0bb4..df40fee6` (round 3's fixes) and raised three LOW
findings. Each was re-verified against the code before it was acted on. Two are fixed in the
commit that adds this section; one waits on the maintainer. The parser's behaviour is unchanged:
the only edit to `tools/perf_runs.py` is docstring text, so the rows over the 30 logs are what
round 3 recorded.

| # | Sev | Finding | Outcome |
|---|-----|---------|---------|
| D1 | LOW | The docstring and two test comments describe brackets matched by kind; the code keeps one depth for all three kinds | Fixed |
| D2 | LOW | No test decides on braces alone, so a parser that stops tracking `{}` passed every test | Fixed |
| D3 | LOW | Two body lines of `df40fee6`'s message exceed 72 characters | Not fixed: needs a history rewrite |

**D1 (fixed).** Confirmed at `perf_runs.py:275-279`: `[{(` raise one shared depth and `]})`
lower it, so any closer closes the innermost opener whatever its kind. The `_instrument_fields`
docstring now says so ("One depth counts [], {} and () together: any closer closes the innermost
opener whatever its kind, and a closer with nothing open is ignored"), and the module docstring
names the shared depth. The stray-bracket test's comment now says the `)` closes the list and the
list's `]` is the closer with nothing open; the unclosed-bracket test's comment says the list's
`]` closes the `(`, which leaves the `[` open. The new test
`test_a_closer_of_another_kind_closes_the_open_bracket` pins the counter: `x=[a) b=1] c=2` reads
as `x=[a)`, `b=1]`, `c=2`. Matching closers by kind would change behaviour and stays with the
maintainer, next to N3.

**D2 (fixed).** `test_braces_keep_a_space_led_key_value_inside_the_value` pins `x={a=1 b=2} c=3`
as `x={a=1 b=2}`, `c=3`, where only the braces keep `b=` inside the value.

**Mutant proof for D1 and D2** (a scratch copy of `perf_runs.py` and its tests, the helper modules
from the worktree): M6 (braces no longer tracked) gives `Ran 82 tests`, `FAILED (failures=1)`, the
brace test. M7 (a kind-matched stack that ignores a wrong-kind closer) gives `Ran 82 tests`,
`FAILED (failures=1)`, the wrong-kind closer test. The unmutated tool gives `Ran 82 tests`, `OK`.
The reviewer's run had both mutants passing all 80 tests at `df40fee6`.

**D3 (not fixed).** Confirmed: `awk 'length>72'` over the message prints two lines (75 and 90
characters). Rewrapping rewrites an unpushed branch commit, and this pass may not rebase or amend
history, so it waits for the maintainer at merge or squash, as C4 did: break the body before
`RCA:` and fold the Not-tested trailer with a two-space continuation line.

**Note on continuation timestamps (no change).** `entry_timestamp` is set only from a line that
matches `_TS_RE` (`perf_runs.py:357-361`), so a line without the prefix never sets it. A
continuation therefore takes the newest prefixed line's time, as the docstring says. If a writer
other than FileLogger put an unprefixed `[Tag] key=value` line into the log, it would carry the
time of the logger entry above it. FileLogger is the only writer (round 3's focus check:
FileLogger.cs:49 holds the file with FileShare.Read and writes each entry in one WriteLine under its
lock), so a continuation always takes its own entry's time; a log joined by hand is the residual case.
(The first wording called this UNVERIFIED; corrected after the next convergence round.)

**Suite after the fixes.** `tools.tests.test_perf_runs`: `Ran 82 tests`, `OK`. Full Python suite:
`Ran 3044 tests`, `FAILED (failures=3, skipped=8)`, the three known base failures (3042 at the
start of this pass, two tests added). dotnet: `Failed! - Failed: 1, Passed: 12345, Skipped: 2,
Total: 12348`, identical to the run at the start of this pass
(`EveryLanguage_DeclaresARowForEveryEnglishKey`, translation rows, not this branch's). No gate
changed, so there is no gate sweep and the hook suite was not run.

## Convergence round 2 after round 3 (`df40fee6..973f35d9`)

The convergence reviewer found two LOW items, both in the records; the review workflow's last round
runs no fix pass, so the orchestrator closed them in the commit
`docs(perf): v2.0.32 - record plan 029's last convergence rounds`: the continuation-timestamp note above
now states the verified fact instead of calling it UNVERIFIED, and the RCA and REVIEW-LOG now record D1
to D3 (the RCA's "Convergence round 4" section), the module's 82 tests and this round's two maintainer
items. No code changed.
