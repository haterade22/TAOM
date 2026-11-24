# Codex pre-review: the warg in the gait stride gate (#1565 item 2, 2026-11-24)

You are the reviewer (`.ai/roles/reviewer.md`). This is a review-only assignment: do not modify,
create or delete any file, do not run a build that deploys (`./build.ps1`), do not run any tool with
`--apply`, and do not commit. Read `AGENTS.md`, `.ai/policy.md` and `.ai/review-reference.md` first.
Every claim needs a source location you read in this run; mark anything you cannot prove UNVERIFIED.

## The change

Uncommitted working tree in the worktree `E:\repos\taom-warg-stride-gate`, branch
`fix/warg-stride-gate`, HEAD `d46191792`. Run `git status --short` and `git diff`; one file is
untracked (`tools/tests/test_gait_stride_creatures.py`), beside this prompt. Issue #1565, item 2:
"Make `check_gait_stride.py` find a master by the GUID in the record, give it a per-creature slip
setting, and add the warg." Follow-up issue #1591 holds what the first run on the warg found and
this change does not correct.

- `tools/check_gait_stride.py`: a read-only gate. New: `LIMITS` and `limits()` (a creature's own
  `max_slip` and `min_duty`; a command-line argument goes over the creature's value);
  `judge(..., min_duty=)`; `stamp_state()` (a cache entry is held to the stored checksum of its
  record, through `set_clip_balance_name.parse`, `rdc_path` and `rdc_current`); `report()` split out
  of `main()`. In `report` a gait's master is the SkeletalAnimation item whose GUID the record names
  (`master_by_guid` over every `*_geo.tpac` under the creature's folder), where the gate took the file
  `<clip>_geo.tpac` and its first animation. A creature's `cooked: "stamp"` chooses `stamp_state`
  over the date rule `cache_state`. A creature's `known` dict prints a gait that fails the stride or
  slip judgement as `KNOWN` and does not count it; a listed gait that passes, or that the run does
  not measure, is a FAIL. New `CREATURES["warg"]`.
- `tools/data_gates.py`: `STRIDE_TOOL_GLOBS` (the tool and every module it imports), now part of
  `ELEPHANT_GLOBS` and `SPIDER_GLOBS`; `WARG_STRIDE_GLOBS`; the row `warg-stride`.
- Tests: `tools/tests/test_gait_stride_creatures.py` (new), `tools/tests/test_data_gates.py`.
- Docs: `tools/README.md` (the row of `check_gait_stride.py`),
  `docs/reference/lotrlome-warg-gait-changes.md` ("Check and gate", "Known limitations"),
  `docs/reference/lotrlome-dire-wolf-changes.md` (one paragraph in "Gaits"),
  `docs/features/warg-combat.md`, `docs/reference/feature-map.md`,
  `.claude/rules/traps/creatures-assets.md`, `docs/reviews/lessons/build-tooling-workflow.md` (the
  last entry).

No file of the live Armory was written. The gate reads
`E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\LOTRLOME_Armory`:
`Assets/creature/warg/**`, `ModuleData/action_sets.xml`, `RuntimeDataCache/*.rdc`.

Read-only checks you may run:
`python tools/check_gait_stride.py --creature warg` (also `elephant`, `spider`),
`python tools/data_gates.py`, and
`python -m unittest tools.tests.test_gait_stride_creatures tools.tests.test_elephant_gait_tools tools.tests.test_spider_gait tools.tests.test_warg_gait_plan tools.tests.test_data_gates`
(they need `numpy`, `lz4` and `xxhash`; say so if your sandbox lacks them). Trunk's tool, for a
comparison, is `E:\repos\TAOM\tools\check_gait_stride.py`.

## Claims to verify (each is the author's, none is settled)

1. **No pass on what was not read.** For the warg, every clip of `as_warg` that claims travel is in
   exactly one of: a measured gait, the `known` list (measured, printed, not counted), or the
   `pending` list (printed as not checked). Is there a state of the installed data, or of the
   `CREATURES["warg"]` table, in which the tool exits 0 and a gait was not judged? Consider a record
   whose GUID is in two master files, a `gaits` name that no set binds, a `known` clip that fails for
   a NEW reason beside its listed one, and a `known` clip that is also `pending`.
2. **The `known` list is a ratchet.** A listed clip that starts to fit fails the gate. A listed clip
   whose cache entry is not current still fails. Is either statement false on some path of `report`?
   Is "measured and not counted" a sound way to ship a gate over two records that fail, or does it
   weaken a check that other creatures rely on? The elephant and the spider have no `known` list;
   can a table edit give them one without a test failing?
3. **The cook rule.** `stamp_state` holds an entry to its record's checksum and does not look at the
   master's date. The author's ground: 32 warg masters are dated 2026-08-31 09:01 and every record
   no tool has rewritten 2026-08-28 20:33, all later than the entries cooked on 2026-08-28 at 18:08,
   and five masters cooked again in November gave the August payload byte for byte. Read
   `set_clip_balance_name.rdc_current` and `repoint_anim_clip.cook_fault`. What can change under a
   warg gait that the stamp does not see, and is that stated in the tool and the ledger? A value of
   `cooked` other than `"stamp"` falls back to the date rule without a word: is that a defect for a
   gate?
4. **The elephant and the spider are unchanged.** The author compared both reports byte for byte
   with trunk's tool on this install. Find an input on which the old lookup (`<clip>_geo.tpac`,
   first animation) and the new one (GUID) differ, and say which is right. Does `load_anim` still
   have a caller?
5. **Limits.** The warg's `max_slip` 1.0 m and `min_duty` 0.15 are set just above what the bought
   clips have (0.91 m, the gallop's left hind paw; a trot fore paw planted for 3 of 16 keys). Is a
   ratchet of that kind worth a row in a gate, or does it state a quality nobody holds? Is the
   `--max-slip` default now `None`, and does a 0.0 given on the command line still count as given?
6. **The moving paces' strafe records.** The four `pending` clips play their pace's forward keys.
   The author did not read what the movement node does with a strafe record's travel in a moving
   pace, and the tool's reason says only that the keys do not move sideways. Is anything in the docs
   or the tool stated about engine behaviour that no line of
   `docs/features/elephant.md` ("What the engine does with a clip record") or of the decompiles
   cited there supports?
7. **The data gate.** Do the globs of `warg-stride` select everything the command reads and runs,
   and nothing that selected a gate before is lost from `elephant-gaits`, `elephant-clips`,
   `spider-gaits`, `spider-clips`, `spider-body`?
8. **The tests.** Does a test pass for a reason other than the one it names? Which behaviour of
   `report`, `limits`, `stamp_state` or the warg's table has no test that fails without it? Is
   `InstalledWargTests` safe on a machine with a partial install?
9. **The docs.** Numbers to check against the tool's output and the files: 0.91 m, 3 of 16 keys,
   32 masters, 30 of 41 clips, 0.73 m and 1.00 m against 1.60 m, 1.17 m against 2.08 m, 0.10 m and
   0.08 m. Prose rules: `docs/reference/writing-tells.md`, no em or en dash.

## Output

Findings first, most severe first, each with: severity (P1 blocks the commit, P2 should be fixed
before it, P3 may follow), file and line, the input or state that shows it, and the smallest fix.
Then what you verified and how, and what stays UNVERIFIED. Do not restate the change.
