# RCA: Shadow Type instrumentation (#1103 Stage 1, deep review 2026-11-09)

## Top-line

Stage 1 of #1103 adds the player's Shadow Type and Shadow Filtering to the once-per-mission `[PerfContext]` line and
prints each group's shadow settings in `python tools/perf_runs.py compare`. The tier 1 review ran Codex
(`gpt-6-astra`, effort `ultra`) and three Claude lenses (5 Data flow, 2 Engine compatibility, Tooling). None found a
defect in the production path: the engine reads are plain loads of the value the options screen shows, the field
order agrees at every stage, and old logs still parse. They found one MEDIUM gap and seven LOW items, all fixed in the
same change with tests: 157 `test_perf_runs` tests and 395 MissionPerf C# tests pass (1 skipped, as before).

## Findings

| # | Sev | Finding | Category | Why missed | Preventive action |
|---|---|---|---|---|---|
| 1 | MEDIUM | A compare group that mixes Shadow Type (`1/2`) got no `INCONCLUSIVE` reason; only a reader who spotted the `/` would know the A/B was spoiled (Tooling F1, Codex O1, Data flow D1) | Display without a verdict | The plan said "prints the new keys per group", and the builder took the printed line as the safeguard. The tool already turns a mixed scene into `INCONCLUSIVE`, but the new keys were not put through the same check | New lesson in `lessons/build-tooling-workflow.md`. Fixed: a group with two known values of a shown key adds `group <side> mixes <key> <values>`; three tests |
| 2 | LOW | The pinned `[PerfContext]` sample repeats values across option slots (shadowQuality and shadowType both 2), so a swap between them passes every no-game test; only a `RequiresGame` test caught it (Data flow F1) | Weak pin | The builder extended the existing pinned sample instead of giving each new slot its own value | Repeat of `lessons/testing-qa.md` "assert every field with a distinct value" (the isolation entry). Fixed: `BuildPerfContext_DistinctOptionValues_EachLandsInItsOwnSlot` |
| 3 | LOW | `tools/README.md`, two `perf_runs.py` docstrings and the feature doc still described the old `compare` output and the old meaning of `na` (Data flow F2, Tooling F5) | Doc drift | The sweep covered the feature doc's changed lines, not every text that describes `compare` | Repeat of `lessons/build-tooling-workflow.md` "A change's sweep has two halves". Fixed in all four places |
| 4 | LOW | The `_SHOWN_KEYS` comment said the keys are not in `_CONTEXT_KEYS`, but `shadowQuality` is (Tooling F2) | Wrong comment | Written for the two new keys, then a third key was added to the tuple | One-off. Comment corrected |
| 5 | LOW | `test_a_log_from_before_the_shadow_fields_shows_na` deleted a key from a parsed dict and never parsed an old line (Tooling F3) | Test does not prove its name | The test built the old shape by hand instead of through `parse_context` | Repeat of `lessons/testing-qa.md` "at least one test per rule goes through the producer itself". Rewritten to parse a real old line |
| 6 | LOW | The sort of a group's values was guarded only by chance: with two values an unsorted set passed under 7 of 16 hash seeds (Tooling F4) | Weak assertion | Two values make a lucky order likely | One-off. The test now uses three values |
| 7 | LOW | The feature doc called the logged fields "raw engine option values"; the adapter rounds them and writes `na` for a rejected value (Codex O2) | Doc overreach | The phrase predates this change and was carried into the rewritten sentence | One-off. Wording corrected |
| 8 | LOW | The "row(s) without a [PerfContext] build and texture quality" note printed right under the new `settings` line and read as if it covered the shadow keys (Data flow D2) | Ambiguous output | The new line was appended at the end of the group block | One-off. The note now prints before the `settings` line |

Observation, not a defect (Engine compatibility U1): whether the engine applies a Shadow Type change to a running
session is UNVERIFIED. The job `NativeOptions.Apply` queues has a callback Ghidra cannot read. The feature doc now
says to restart the game after a change; the unattended A/B restarts the game for every run.

## Root-cause pattern

Findings 1 and 8 share one shape: a new piece of output was added beside the tool's existing checks without asking
whether those checks cover it. Findings 2, 3 and 5 repeat rules already on file; the builder did not read
`lessons/testing-qa.md` and `lessons/build-tooling-workflow.md` before touching `perf_runs.py` and its pins.

## Why each lens missed these

All eight came from the first wave; no lens missed a confirmed item that another lens found. Engine compatibility
reported no tool item because `tools/perf_runs.py` was outside its file list by design. Codex rated finding 1 an
observation and Tooling rated it MEDIUM; both proposed the same check.

## Feedback memories to codify

None. The new rule is a lesson entry, not a memory.
