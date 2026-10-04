# RCA: perf_runs.py mission rows and A/B compare, deep review (2026-10-02)

**Top line:** Plan 029's log tool was correct on all 30 installed logs (16 rows, 0 unparsed), and
its three tick-profiler literals already equalled plan 028's. The review still found ten confirmed
defects, none HIGH. One is a rule that held only by coincidence: the spawn window was kept out of
the steady statistics by its `t` being under 30, not by its position, so a long render wait before
the first tick would have put the spawn burst into the steady numbers. The rest are a test suite
that pinned one sample literal per line while plan 028's edge shapes, status lines and the printed
report went unasserted (14 mutants survived the committed suite), and five small contract or
wording gaps. All ten are
fixed with tests proven RED or mutation-checked; the real-log output is unchanged
(`row field differences: 0`).

## Findings

| # | Sev | Bug | Category | Why missed | Preventive action |
|---|-----|-----|----------|-----------|-------------------|
| F1 | MEDIUM | `summarize` chose steady windows by `t >= 30` only (`perf_runs.py:487`), so the spawn window joined the steady stats whenever its `t` reached 30. The heartbeat's clock starts at `OnCreated` and its first window closes one interval after the first tick, so a render wait over about 25 s does it (one is on record at 305 s) | Logic error: a rule stated by position coded by a coinciding value | Every real log opens at `t=+6s` or `+7s`, so the value test and the position rule agreed on all 30 logs. The plan's own test fixtures mostly opened on a `t=31` window with no spawn window at all, a shape no real log has, so a position rule would have failed them and the value rule was the one that passed | Steady windows are `windows[1:]` filtered by `t`; five fixtures gained the real spawn window; `test_spawn_window_after_a_late_first_tick_is_not_steady` (RED first). Lesson in `lessons/testing-qa.md` |
| F2 | MEDIUM | The `[TickSummary]` fixtures wrote `windows=12 ... top=Type:ms`, a shape plan 028 never writes; 028 pins a fourth data literal the parser side had no twin for | Contract drift between twin pins | 029 was built before 028's 2026-10-03 amendment added `[TickSummary]`; the generic extra-tag path parsed anything, so an invented line passed as well as the real one | `PINNED_TICK_SUMMARY` is 028's literal; its 13 fields and the `worstHitchT=na` variant asserted. Lesson in `lessons/testing-qa.md` |
| F3 | MEDIUM | The parser side of 028's edge shapes was unpinned: `top=none` on both lines, `na` per-behaviour KB, an all-`na` context, the 11 `[TickProfiler]` status lines, and `MissionOpenNewDone` after every open. Seven mutants that misread a real line left the suite green | Test gap | Each line had one pinned sample, taken from the producer's happy-path literal; the producer's edge-shape tests (`BuildTickProfile_NoAllocationCounter_WritesNaForEveryKb`, `StatusLines_NeverContainADataTag`) had no parser-side counterpart, and the load fixture left out the `MissionOpenNewDone` line the `\b` guard exists for | `PlanTwentyEightShapeTests`; all seven mutants die. Lesson in `lessons/testing-qa.md` |
| F4 | MEDIUM | No test asserted the compare table (the A/B verdict) or the row's `load:`, `hitches:`, disabled-heartbeat and "... and N more unparsed" lines; seven mutants survived, including B's median printed in A's column and a dropped sign | Test gap | The CLI tests checked exit codes and header lines; `compare_rows` was tested as data, and the formatting of that data was assumed to follow | `ReportTextTests` with golden lines; all seven mutants die |
| F5 | LOW | `compare` skips `na` when it looks for mixed settings, then counted only rows with no `[PerfContext]` as unchecked, so a row whose texture quality read `na` was reported as checked | Missing null guard (on the report side) | `na` was handled where values are compared and forgotten where the report says what was compared | `context_unchecked` counts `na` too; the warning names both settings; RED first |
| F6 | LOW | The `MEMORY_PRESSURE` evidence scanned `[MemSample]` with its own looser regex while the flag came from `triage_battle_load.py`'s strict one, so the cited line could be one the flag never read; the docstring said `[MemSample]` is never re-implemented | Convention inconsistency | The evidence was added in the plan's amendment as a scan for "the line to show", separate from the parse that decides the flag | The evidence uses `tb.parse_battle_load_log` per line; `_MEMLOAD_RE` deleted; RED first. Lesson in `lessons/build-tooling-workflow.md` |
| F7 | LOW | The text row printed `gc0/min` and `gc2/min` but not `gc1/min`, which the feature doc promises | Doc and code drift | The JSON carried all three, and the row format was written for width | Row prints `gc1/min`; asserted |
| F8 | LOW | The docstring, feature doc and `tools/README.md` said extra tags are collected "inside the mission"; a segment runs to the next mission's start, so a battle row carries the campaign map's `[MapLoad]` lines | Stale claim | The `MEMORY_PRESSURE` row was worded for the real span; the extra-tag text was written from intent | Reworded in all three places |
| F9 | LOW | The feature doc dated the `t=+6s` evidence to "the 2026-10-02 custom and campaign battle logs"; the only campaign battle log is 2026-09-29 | Stale claim | The date was taken from the custom battle log and extended to the campaign case without a grep | Reworded to "2026-09-29 to 2026-10-02" |
| F10 | LOW | `float()` reads `NaN` and `Infinity`, so a non-finite tick, hitch or context number parsed (and `--json` would print invalid JSON), against the plan's "a non-number is malformed" | Missing guard | `float()` was taken as the number check; reachability is UNVERIFIED (028's values are finite by construction) | `_num` rejects non-finite values; RED first |

Not entered as a defect: the commit trailers of `63cc79a7` do not parse (`git interpret-trailers
--parse` prints nothing). Fixing it means rewriting the reviewed commit, which is the maintainer's
call; nothing reads the trailers today.

## Root-cause pattern: a sample is not a contract

F1 to F4 share one cause. The tests were built from one sample of each thing (one literal per line
shape, one window layout, one compare result) and checked that the sample went through. Where the
sample happened to satisfy a weaker rule than the contract (a spawn window below 30 s, a
`[TickSummary]` with any key=value body, a table whose numbers are right but unprinted), the weaker
rule passed. Mutation found it in minutes: 14 mutants, each misreading a real 028 line or
misprinting the report, all survived the 58 tests as committed (`Ran 58 tests ... OK` for every
one), and all 14 die under the 71 tests now.

## Why each agent missed these

- **Agent 1 (Standards):** found F2, F6, F7 and F9. F1, F3 and F4 are behaviour and coverage
  questions outside its ten checks; it checked the line contract by parsing 028's real lines, which
  passed, not by mutating the parser.
- **Agent 4 (Completeness):** found F2 to F4, F8 and F9 by mutation. F1 needs the heartbeat's clock
  semantics, which completeness does not read. F5 to F7 and F10 are behaviour inside tested
  functions.
- **Tooling correctness:** found F1, F2, F5, F6, F8, F10 and part of F3 (the `\b` guard and the
  status lines). It did not mutate the report text (F4) and judged the 028 edge shapes from probes,
  not from the suite's ability to catch a misread.
- **Agents 2, 3, 5, 6 and 7:** not launched (no engine, runtime, data-flow or XML change). A data-flow
  lens would have been the natural owner of F1 (a clock that starts at one event and a window that
  opens at another).

## Feedback memories to codify

- A rule the contract states by position ("the first window") is coded by position, and fixtures
  carry the shape every real input has (`lessons/testing-qa.md`).
- A parser twin pin covers the producer's edge shapes and status lines, and the printed report is
  asserted line by line; mutate the guard lines before calling it done (`lessons/testing-qa.md`).
- Evidence for a flag comes from the same parse that raised it (`lessons/build-tooling-workflow.md`).

## Convergence rounds

| Round | Diff read | Finding | Fixed in | Why missed | Preventive action |
|---|---|---|---|---|---|
| 1 | `63cc79a7..06584dd9` | C1 (MED): F1 made steady windows positional, and the two FRAME_CAP minimum-window boundary tests built missions with no spawn window, so a mutant `FRAME_CAP_MIN_WINDOWS = 3` passed the suite | `fc141af2` | The fix changed what counts as a steady window but not the fixtures the boundary tests build, and no mutant was run against the boundary | After any change to what a boundary test counts, re-prove it with a mutant (`fc141af2`: the mutant now fails exactly those two tests) |
| 1 | same | C2: `TICK_PROFILER_STATUS` held plan 028's status lines from `b3bac1a9`; five texts had changed at `e64529b3` and five shapes were missing | `fc141af2` | The copy was taken from 028's first commit while 028's review was still changing it | The tuple names the 028 commit it copies, and whichever plan merges second re-syncs it. Re-synced again to `7ca09cc2` by the orchestrator after the review |
| 1 | same | C3: the derived no-hitch `[TickSummary]` fixture set `allocKB=na` and kept numeric per-behaviour KB, a shape 028 cannot write (one `allocAvailable` flag drives both) | `fc141af2` | The variant was made by editing one field of a pinned line, without reading how 028 builds the fields together | Derive a fixture variant from the writer's code path, not by editing its pinned line |
| 2 | `06584dd9..fc141af2` | C4: `fc141af2`'s Not-tested trailer does not parse as a git trailer (its wrapped line starts without whitespace) | not fixed | A wrapped trailer value needs a leading space on its continuation line, and nothing checked the trailer block | Nothing reads the trailers (no tool or hook calls `git interpret-trailers`), and a fix rewrites history, so the maintainer decides at merge. Later commits keep each trailer on one line (`50028d26` parses) |

**After the review.** The orchestrator's `50028d26` changes the tool after its last review: extra-tag
values with spaces stay whole, a continuation line takes its entry's timestamp, and the top-level help
names `compare` (three observations from running the tool over the 30 logs on disk; rows outside
`extra_tags` are identical over those logs). Over the same logs it also changed more than the
`[Doctrine]` lists it was written for: 590 values (`[Doctrine]` 498, `[MapLoad]` `parties` 92) and
1018 keys (`[Doctrine]` 374, `[MapLoad]`'s seven per-kind counts 644), which round 3 records.

## Convergence round 3 (`fc141af2..f92a0bb4`)

Three lenses (Standards, Completeness, Tooling correctness) read the orchestrator's `50028d26`,
`43f74556` and `f92a0bb4`. They agreed on one MEDIUM and seven LOW findings, all confirmed by the
lead and fixed; none HIGH. Report: `deep-review-029-perf-runs-parser-2026-10-02.md` "Convergence
round 3".

| # | Sev | Bug | Category | Why missed | Preventive action |
|---|-----|-----|----------|-----------|-------------------|
| R1 | MEDIUM | The bracket depth in `_instrument_fields` had no test: four mutants (depth never rises, only `[`, no clamp, no parentheses) passed all 75 tests | Test gap | The bracket test went red first, so it looked like proof; it went red because of the space-led check (`{Charge=` has no space before it), and no guard line was mutated. This file's own lesson ("mutate each guard line") was not applied to a commit made outside a review round | Three tests on real shapes kill all four; the round 3 lesson in `lessons/testing-qa.md` |
| R2 | LOW | The rule moved `[MapLoad]`'s per-kind counts into `parties` and `[EnlistDiag]`'s snapshot into `verdict`; the commit and records reported "489 values" | Unrecorded behaviour change | The before and after comparison was read for the tag that motivated the change; the per-tag diff of every other tag was not printed | Counts corrected; behaviour pinned and documented; lesson in `lessons/build-tooling-workflow.md` |
| R3 | LOW | An opener never closed runs its value to the end of the line (keys after it are kept as text), undocumented | Unstated edge case | The rule was designed from balanced real lines; the failure-status path that embeds `ex.Message` was not read | Documented and pinned; the alternative waits on the maintainer |
| R4 | LOW | The continuation timestamp was unpinned: updating the stamp only on collected lines passed every test | Test gap | The test placed the continuation straight under its head, where both rules agree | A test with an uncollected prefixed line in between |
| R5 | LOW | A fixture labelled "as a 2026-09-29 log writes it" was not that line (`troops=1` after `registered=[...]`, four fields dropped) | Fixture invented | The case wanted a key after a list and the comment was kept from the real line it was edited from; same class as C3 | The fixture is the real line verbatim; lesson in `lessons/testing-qa.md` |
| R6 | LOW | The status tuple comment said 16 lines; 028's test builds 19 at `7ca09cc2` | Stale claim | The commit id was updated, the count was not | Count corrected |
| R7 | LOW | The help test failed at a narrow terminal (`COLUMNS=50`) | Environment-dependent test | argparse wrapping was not considered | Asserts on whitespace-normalised text |
| R8 | LOW | The feature doc dropped "space-led" from the rule | Doc drift | The docstring and doc were written separately | Reworded |

**Root-cause pattern: a change measured by its motivating example.** R1, R2 and R5 share it. The
change was proven on the `[Doctrine]` lines that prompted it (a red-first test, a 489-value count,
a fixture edited from a `[Doctrine]` line), and what the rule did elsewhere (to `[MapLoad]`, to a
stray or unclosed bracket, to which guard made the test red) was never printed. R1 repeats the
lesson this RCA wrote the same day ("mutate each guard line"), which needs a stronger trigger: it
now applies to every commit that adds a guard, follow-up commits included.

**Why each agent missed these.** The three lenses that ran found all eight; no lens missed a
confirmed finding. The miss was upstream: `50028d26` was an orchestrator follow-up with no review
of its own until this round, so the mutation pass and the per-tag diff that the round 1 lessons ask
for were never run on it. Agents 2, 3, 5, 6 and 7 were not launched (no engine, runtime, data-flow
or XML change).

**Needs Mike.** N1: plan 040's bare `total` marker joins the value before it (`phase=GameInit
total`); fix in 040's writer or in 029's rule. N2: the plan 029 issue is unfiled. N3: keep the
unclosed-bracket behaviour or rescan bracket-blind.

## Convergence round 4 (`df40fee6..973f35d9`, then `..` the records commit)

| # | Sev | Bug | Category | Why missed | Preventive action |
|---|-----|-----|----------|-----------|-------------------|
| D1 | LOW | The docstrings and test comments described brackets matched by kind; the code keeps one depth for all three, so a closer of another kind closes the open bracket | Doc vs code | The text was written from the intended rule, not read back against the counter | Text describes the counter; `test_a_closer_of_another_kind_closes_the_open_bracket` pins it (red under a kind-matched mutant) |
| D2 | LOW | The `{}` kind of the bracket guard had no test: a mutant that stops tracking braces passed every test | Test gap, the third miss of the guard-mutant lesson (F2 to F4, R1, now D2), inside the commit that wrote the strengthened lesson | The mutation pass covered the two kinds R1 named and not the third the docstring promised | `test_braces_keep_a_space_led_key_value_inside_the_value` (red under the mutant). Stronger action: list every guard line from the code before writing mutants, never from the finding that prompted them |
| D3 | LOW | Two body lines of `df40fee6`'s message exceed 72 characters | Commit message | The trailer was kept on one line to avoid C4's parse problem; folding with an indented continuation also parses | Rewrap at merge (Needs Mike) |
| R4-1 | LOW | A record note called the single-writer question UNVERIFIED although round 3 verified it | Record contradicts itself | The fix pass wrote the note without re-reading round 3 | Note corrected |
| R4-2 | LOW | D1 to D3 were recorded only in the report; the REVIEW-LOG still said 80 tests | Records gap | The fix pass records a round only in the report | Closed by the orchestrator here |

**Needs Mike, added:** D3's rewrap of `df40fee6`'s body at merge, and whether brackets should match by
kind (next to N3).
