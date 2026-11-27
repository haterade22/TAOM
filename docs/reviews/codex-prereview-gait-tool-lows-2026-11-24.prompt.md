# Codex pre-review: the low findings on the clip record tools (#1566, 2026-11-24)

You are the reviewer (`.ai/roles/reviewer.md`). This is a review-only assignment: do not modify,
create or delete any file, do not run a build that deploys (`./build.ps1`), do not run any tool with
`--apply`, do not start the game or the Modding Kit, and do not commit. Read `AGENTS.md`,
`.ai/policy.md` and `.ai/review-reference.md` first. Every claim needs a source location you read in
this run; mark anything you cannot prove UNVERIFIED.

## The change

Uncommitted working tree in the worktree `E:\repos\taom-gait-tools`, branch `fix/gait-tool-lows`,
HEAD `cdab59105`. Run `git status --short` and `git diff`. Issue #1566 (`gh issue view 1566
--comments`); the turn unit belongs to #1550.

- `tools/repoint_anim_clip.py`: `repoint` refuses a `source1` or `source2` that is not a finite
  number; `main` holds `--under` inside the module's `Assets` folder (realpath and commonpath);
  `apply` stops at the first row that fails and raises `RepointError` with "N of M record(s)
  written before it" (new helper `_replace`; an `OSError` is caught there); the `--check` summary is
  reworded; the docstrings of the module and of `_travel_at` are extended.
- `tools/set_clip_balance_name.py`: `plan` refuses, for a clip it would change, a
  RuntimeDataCache entry that `rdc_current` does not accept for the package's present bytes. Before,
  it wrote a new stamp into any entry of that item.
- `tools/tests/test_shellwords.py`: one timing test measures `time.process_time()` where it measured
  `time.perf_counter()`.
- `tools/spider_gait.py`, `tools/creature_fit.py`: one comment each.
- Tests: `tools/tests/test_repoint_anim_clip.py` (`LowFindingTests`, one test of
  `CookedCheckTests`), `tools/tests/test_set_clip_balance_name.py` (two tests in `MainTests`).
- Docs: `tools/README.md` (the rows of the two tools), `docs/features/elephant.md` ("Standing pace"
  and "Speed at model size" in "What the engine does with a clip record"),
  `docs/features/spider.md` (the first point of "Still open").

No live file was written. The tools read
`E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\LOTRLOME_Armory`.

Read-only checks you may run:
`python tools/repoint_anim_clip.py --plan tools/warg_gait_plan.json --under creature/warg/animations --check`,
the same with `tools/dire_wolf_gait_plan.json` and `creature/direwolf/animations`,
`python tools/data_gates.py`, and
`python -m unittest tools.tests.test_repoint_anim_clip tools.tests.test_set_clip_balance_name tools.tests.test_creature_fit tools.tests.test_shellwords`
(some need `xxhash`, `numpy` and `lz4`; say so if your sandbox lacks them). Native decompiles the
author read are in `E:\Temp\claude\1d198fa1037d6973\01dcd7bc-3507-4e6c-bf25-c43ab339ddcf\scratchpad\`
(`n_58D980.c`, `n_58D840.c`, `n_66AB54.c`, `n_66A0A0.c`, `n_66AE30_callers.c`,
`n_66BE20_callers.c`, `n_5F75B0.c`, `74E9C0.c`); `python tools/native_decompile.py --rva <rva>`
makes them again (`docs/features/ghidra-native-decompile.md`).

## Claims to verify (each is the author's, none is settled)

1. **A source that is not a number.** `repoint` now passes both sources through `_floats`. Is there
   a value a JSON plan can hold that still reaches `struct.pack_into` or the frame 0 rule wrongly (a
   bool, a numeric string, a very large number that packs as inf)? `float("12")` accepts a string:
   is a plan row with `"source1": "12"` accepted, and should it be?
2. **`--under`.** Find a value that leaves `Assets` and is not refused, or a value inside `Assets`
   that is refused (a junction, a path with other case, a trailing slash, an empty string, `.`).
   `--clips-dir` is not held: is that a hole or the stated escape for the hill troll's folder?
3. **`apply`.** After a failure on row k: are rows before k written with their backups, row k
   restored, rows after k untouched, and is the count right? Does the restore inside `_replace`
   hide the first error when the restore fails too? Is a `KeyboardInterrupt` or a `TypeError`
   handled as before? `main` prints "refused: ..." for a write failure: is that the right word and
   exit code for `tools/data_gates.py`, which never runs `--apply`?
4. **`set_clip_balance_name.plan`.** The new refusal fires for a clip the tool would change whose
   entry is not current. Which callers reach `plan` (`grep -rn "scb.plan\|scbn.plan\|\.plan(" tools`),
   and can a workflow the docs describe (`docs/features/troll-race.md`, the hill troll re-cut,
   `tools/bind_hill_troll_action_set.py`, `tools/wire_hill_troll_race.py`) now be refused where it
   worked? Is the order of the two checks (another item, then not current) right? Is there a state
   in which an entry holds old keys and the tool still restamps it?
5. **The timing test.** `process_time` counts CPU time of the process. Does the test still fail on
   the regression it guards (a regex whose time grows with the square of a delimiter's length)?
   The author measured that `git_call_dirs` already grows with the square of the number of `<<`
   lines (0.10 s for 750 lines, 2.46 s for 3,000) and did not change it. Is the limit of 5 s then a
   test of anything? Say what the test should assert.
6. **The stored-record reader.** The claim: on v1.5.7 the reader of a stored animation clip record
   is 0x58D980; after the usage name `quad_movement` it steps over four bytes without reading them
   and takes three floats into an object (travel at +8, the lower limit at +0xc, the upper at
   +0x10). Read `n_58D980.c`. Is each part supported by a line? Is the order of the three floats
   proved, or only that three floats are read?
7. **The turn input.** The claim in `docs/features/elephant.md`: the node's turn input is
   `*(*(agent+0x20)+0x138)`, a yaw rate in radians a second; 0x66AAE0 writes it; 0x66A0A0 adds it
   times the tick time to the yaw at +0x34 and wraps at pi; the agent tick 0x602040 passes
   `*(agent+0x20)`. Read the decompiles. Which steps are proved and which are inferred? From that
   the docs conclude that a turn record's travel is the clip's angle in radians / `Duration`, and
   that the spider's two turn records fit. Check the arithmetic and the conclusion.
8. **The tests.** Does a test pass for a reason other than the one it names? Which new behaviour
   has no test that fails without it?
9. **The docs.** Prose rules: `docs/reference/writing-tells.md`, no em or en dash. Is any sentence
   about engine behaviour not supported by a cited line?

## Output

Findings first, most severe first, each with: severity (P1 blocks the commit, P2 should be fixed
before it, P3 may follow), file and line, the input or state that shows it, and the smallest fix.
Then what you verified and how, and what stays UNVERIFIED. Do not restate the change.
