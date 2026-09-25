# RCA: plan 013, Bash hook prefilter review (2026-09-24)

## Top-line

Plan 013 put a raw-text prefilter in front of the Python start-up in all 13 Bash hooks. Six
review lenses and a Codex adversarial pass agree the prefilters themselves are correct: each
accepts everything its hook's trigger needs, and two independent parity runs (156 and 300 cases)
found no changed decision. Every confirmed defect was in how the change was tested and described.
The one that matters: the new test section 4c used an oracle the code under test could swap out.
It counted starts of a fake interpreter, and `_pybin.sh` drops a pinned interpreter that misses
its 0.8 s probe and uses the real `python`, which counted nothing. On a loaded machine the check
reported "the prefilter is narrower than the hook's own trigger" for a hook whose prefilter was
byte-identical to seven that passed. The builder hit exactly that (`scratch/013/green.txt:193`),
a plan STOP condition, reran, got green, and committed without recording it.

All six code or doc findings are fixed. F1 to F3 have a failing proof first: a fake interpreter
slowed to 1 s read 0 starts where the trace showed the `source` (F1), and a planted `exit 3`
(F2) and a planted removal of suggest-compact's two arms (F3) passed the committed suite and fail
the new one. F4 to F6 are discovery and wording fixes with no failing proof: F4's evidence is that
discovery still finds the same 13 rows, and F5 and F6 are the rewording itself.

## Findings

| # | Sev | Bug | Category | Why Missed | Preventive Action |
|---|---|---|---|---|---|
| F1 | MED | 4c's interpreter-start count depends on a 0.8 s probe; under load it reads 0 and blames the prefilter, or passes a hook that does start Python. Comment said "deterministic on any platform" | Test oracle the code under test can replace | The plan designed the counter without reading `_pybin.sh`'s fallback path (`:98-114`); the premise check ran once, before the rows, and cannot see a later probe miss. When the STOP fired, a rerun passed and the flake was treated as noise | 4c reads a `bash -x` trace for the `source` of `_pybin.sh`, which has no timing; the count stays only as a second witness on the negative row. Lesson in `build-tooling-workflow.md`. Repeat of "A zero you did not prove is not a zero" (same file) |
| F2 | MED | Section 4's contract (exit code, JSON, 3 s) no longer reached any Bash hook's parse path: none of its payloads holds a trigger word | A fast path hides the slow path from existing tests | The plan listed section 4 as "must stay green" and checked only that it did; nobody asked which path its payloads now reach | `bash-trigger` payload `git status && dotnet --info`; a planted `exit 3` after the `source` now fails. Lesson in `build-tooling-workflow.md` |
| F3 | LOW | suggest-compact's `dotnet` and `build.ps1` prefilter arms had no 4c row; the row label "still parses" claimed more than `n >= 1` proves | Filter arm without a test row | Rows were chosen per event, not per filter arm, and the parity script excluded suggest-compact | Two rows added; a planted removal of both arms now fails. Same lesson as F2 |
| F4 | LOW | 4c discovery missed regex matchers, `*` and PostToolUseFailure | Discovery narrower than the harness | Written against today's `settings.json` matchers, not the matcher grammar | `re.fullmatch` with `''` and `*` as every tool; PostToolUseFailure included |
| F5 | LOW | "JSON never escapes an ASCII letter" in 12 hooks and the CHANGELOG; false of JSON, and the re-check after a Claude Code upgrade lived only in the plan | Assumed a format property instead of a producer property | The plan's caveat (plans/013:189, :696) did not reach its own prescribed comment text (:406) | Wording names Claude Code; `hooks-catalog.md` records the premise, its evidence and its re-check. Lesson in `build-tooling-workflow.md` |
| F6 | LOW | Catalog paragraph broke the following "This paragraph used to say" back-reference; stale 200 ms figure; "and" read as both required | Doc insertion without rereading neighbours | The insertion point was chosen by topic, and the next paragraph's deixis was not reread | Moved and corrected; one-off |

## Root-cause pattern

F1 to F3 share one theme: **an early exit changes which path every existing test exercises, and a
new test of the early exit must not depend on anything the exit itself can bypass.** F1's oracle
lived downstream of a timed fallback; F2's contract payloads now stop before the code they were
written to cover; F3's rows covered one arm of a three-arm filter. In each case the test stayed
green while its subject changed, which is the shape a correct result has.

## Why each agent missed these

The plan was written and executed by one builder; these are the review lenses, all of which ran.

| Lens | F1 | F2 | F3 | F4 | F5 | F6 |
|---|---|---|---|---|---|---|
| Standards | found | not raised: it checked section 4 passed, not what it reached | not raised | not raised | found | found |
| Data flow | found | found | found | found | found | found |
| Tooling | found | cross-lens note only | found | found | not raised (behaviour lens) | found |
| Efficiency | not raised: performance scope | cross-lens note | not raised | not raised | not raised | not raised |
| Completeness | found | found | found | not raised | found | NIT |
| Design | not raised: judged the proposal, not the oracle | found (P1) | noted as too small | not raised | noted as too small | not raised |
| Codex | UNVERIFIED: read git refs only, no run logs | missed | found | missed | found | missed |

Codex's miss on F1 is structural: it reviewed source through git and could not see
`scratch/013/green.txt`, so the one run that showed the flake was invisible to it. The lenses that
found F1 read the builder's scratch logs as well as the code.

## Feedback memories to codify

None beyond the lessons. The rules already exist ("A zero you did not prove is not a zero",
evidence-over-claims §B on quoting a single green run); F1 is a repeat of the first, applied to a
test oracle rather than a scan result.
