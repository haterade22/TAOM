# Codex pre-review: side steps, the dire wolf's backward walk and the stride gate (#1591, 2026-11-24)

You are the reviewer (`.ai/roles/reviewer.md`). This is a review-only assignment: do not modify,
create or delete any file, do not run a build that deploys (`./build.ps1`), do not run any tool with
`--apply`, do not start the game or the Modding Kit, and do not commit. Read `AGENTS.md`,
`.ai/policy.md` and `.ai/review-reference.md` first. Every claim needs a source location you read in
this run; mark anything you cannot prove UNVERIFIED.

## The change

Uncommitted working tree in the worktree `E:\repos\taom-side-steps`, branch
`fix/side-steps-wolf-stride`, HEAD `13977f736`. Run `git status --short` and `git diff`. Issue #1591
(`gh issue view 1591`), the follow-up of #1565.

Repo files:

- `tools/check_gait_stride.py`, a read-only gate. The `known` list, `known_verdict`,
  `stride_finding` and the `axis` key are gone. New: a clip that only the standing strafe rows of
  the named sets bind (`side_clips`) gets a `side` line with travel / `Duration` beside the feet's
  sideways stride, and no stride verdict; `measure` returns `direction`; `report` adds a pace
  finding when the backward row's clip and the walk row's clip have the same direction;
  `played_keys`, `range_finding`, `record_findings` and `stand_findings` read a record whose second
  source is under its first as played backward; a creature key `ground: "lowest"` (`grounded`,
  `GROUND_RULES`); `CREATURES["dire_wolf"]`.
- `tools/dire_wolf_gait_plan.json`: two rows added, the backward records, sources 32 to 0 of
  `adod_wolf_walk`, 1.66 s, 2.08 m, limits -1.25 to -0.01.
- `tools/data_gates.py`: `DIRE_WOLF_STRIDE_GLOBS` and the row `dire-wolf-stride`.
- `tools/gen_dire_wolf_anim_clips.ps1`: a comment only.
- Tests: `tools/tests/test_gait_stride_creatures.py`, `tools/tests/test_dire_wolf_gait_plan.py`,
  `tools/tests/test_data_gates.py`, `tools/tests/test_elephant_gait_tools.py`.
- Docs: `tools/README.md` (the row of `check_gait_stride.py`), `docs/features/elephant.md`
  ("Standing pace", "Speed at model size"), `docs/reference/lotrlome-dire-wolf-changes.md` ("The
  backward walk"), `docs/reference/lotrlome-warg-gait-changes.md`, `docs/features/dire-wolf.md`,
  `docs/features/warg-combat.md`, `docs/features/spider.md` (one table row),
  `docs/reviews/lessons/animation-skeleton.md`, `docs/reviews/lessons/build-tooling-workflow.md`,
  `.claude/rules/traps/creatures-assets.md`.

Live files, written on 2026-11-24 at 17:36 by
`python tools/repoint_anim_clip.py --plan tools/dire_wolf_gait_plan.json --under creature/direwolf/animations --apply`
under `E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\LOTRLOME_Armory\Assets\creature\direwolf\animations\`:
`dire_wolf_walkbackfast_anm.tpac` and `dire_wolf_walkbackfast_straight_anm.tpac` (each with a backup
`.bak-repoint-20261124-173648` beside it). When this prompt was written the Modding Kit had not
cooked the two records again, so `check_gait_stride.py --creature dire_wolf` fails on those two
cache entries and `InstalledDireWolfTests` fails for that reason. That state is expected; report it
only if the gate fails for another reason.

Read-only checks you may run:
`python tools/check_gait_stride.py --creature dire_wolf` (also `warg`, `elephant`, `spider`),
`python tools/repoint_anim_clip.py --plan tools/dire_wolf_gait_plan.json --under creature/direwolf/animations --check`,
`python tools/data_gates.py`, and
`python -m unittest tools.tests.test_gait_stride_creatures tools.tests.test_dire_wolf_gait_plan tools.tests.test_elephant_gait_tools tools.tests.test_spider_gait tools.tests.test_data_gates`
(they need `numpy`, `lz4` and `xxhash`; say so if your sandbox lacks them). Trunk's tool, for a
comparison, is `E:\repos\TAOM\tools\check_gait_stride.py`. Native decompiles the author read are in
`E:\Temp\claude\1d198fa1037d6973\01dcd7bc-3507-4e6c-bf25-c43ab339ddcf\scratchpad\` (`n_5F75B0.c`,
`n_0x6459F0.c`, `749C20.c`, `749b10.c`, `74E9C0.c`); `python tools/native_decompile.py` makes
them again (`docs/features/ghidra-native-decompile.md`).

## Claims to verify (each is the author's, none is settled)

1. **The unit of the node's sideways input.** The docs now say: function 0x5F75B0 of
   `TaleWorlds.Native.dll` v1.5.7 writes the horse animation system's forward and sideways inputs
   as the agent's velocity in its own frame, m/s, divided by the field `IMBAgent.GetAgentScale`
   returns; the system's tick 0x6459F0 copies them into the movement node; so at the standing pace,
   inside the clamp of 0x749C20, the body moves `LoopDisplacement` / `Duration` in one loop. Read
   the decompiles. Is each step supported by a line? Is "the body moves travel / Duration in a
   loop" right, or does the clamp, a blend weight or the agent scale change it?
2. **A side step gets no verdict.** The author's ground: on this install no creature's standing
   strafe record has a free travel (the warg's right record also sets the rate of the turn in
   place, the elephant's play walk keys, the spider's also sits on every moving pace's strafe
   rows). Is "print and do not judge" a sound rule for a gate, or does it drop a check a creature
   had on trunk? Compare the spider's and the elephant's reports with trunk's tool: which lines
   were judged there and are not judged now, and can a defect in one of them now pass?
3. **`side_clips`.** A clip is a side clip when a strafe action of `pace_actions` binds it and no
   backward, walk or faster action does. Find a table or an action set in which a gait escapes the
   stride judgement through this rule, or a side clip is judged as a gait. What happens to a clip
   listed in `gaits` that is also a side clip, to one in `pending`, to one in `turns`?
4. **The direction rule.** `measure` takes the sign from the median of the planted feet's steps
   along Y. The finding fires when the walk's and the backward clip's signs are equal and not 0.
   Can a real backward walk fail it, or a forward-stepping one pass (a clip with no planted key, a
   set with no walk row, a backward clip that is `pending` or a side clip, two sets)?
5. **Reversed sources.** The claim: a clip record whose second source is under its first plays
   its master backward (`docs/reference/bannerlord-animation-system-map.md`, "Source 1, Source 2";
   vanilla `blocked_slashright_2h`). The two wolf records are looped movement clips with a
   `quad_movement` usage, not one-shot blocks. Is there evidence in the repo, the engine docs or
   the decompiles that a looped clip under the mount movement node plays reversed sources the same
   way, and that the Kit cooks such a record? What does `repoint_anim_clip.py` do with a reversed
   row (its `--check`, its cook test, its range rule)? What does `gen_dire_wolf_anim_clips.ps1
   -Verify` do with the two records now?
6. **The backward walk's numbers.** `Duration` 1.66 s and travel 2.08 m with limits -1.25 to
   -0.01, one record on the front row and the "none" row. The ledger says the fit is at the top of
   the pace and the paws slip under it. Check against `docs/features/elephant.md` ("What the
   engine does with a clip record") and 0x749B10 for a negative limit pair. Is 1.66 s the right
   end?
7. **`ground: "lowest"`.** Each foot's height is taken from its own minimum. The docstring says a
   foot that never comes down still reads as planted. Is the wolf's stride measure sound under it
   (compare `tools/tests/test_dire_wolf_gait_plan.py`, which measures the same paws)? Are the
   limits 0.35 m and 10% a ratchet or a blank cheque?
8. **The data gate.** Do the globs of `dire-wolf-stride` select everything the command reads and
   runs? Is anything that selected `warg-stride`, `dire-wolf-gaits`, `elephant-gaits` or
   `spider-gaits` before now lost?
9. **The tests.** Does a test pass for a reason other than the one it names? Which behaviour of
   `side_clips`, the direction rule, the reversed range, `grounded` or the wolf's table has no test
   that fails without it? Is `InstalledDireWolfTests` safe on a machine with a partial install?
10. **The docs.** Numbers to check against the tools' output and the files: 2.13 m (warg), 1.50 m
    against 1.20 m and the fitting 0.96 m (spider), 1.18 m (elephant, mumak), 1.60 m against 0.10 m
    and 0.08 m (wolf side steps), 1.17 m to 2.08 m and 1.00 s to 1.66 s (wolf backward), the trot's
    worst slip 0.30 m, "eight then, ten" generator records in the plan. Is any sentence about
    engine behaviour not supported by a cited line? Prose rules: `docs/reference/writing-tells.md`,
    no em or en dash.

## Output

Findings first, most severe first, each with: severity (P1 blocks the commit, P2 should be fixed
before it, P3 may follow), file and line, the input or state that shows it, and the smallest fix.
Then what you verified and how, and what stays UNVERIFIED. Do not restate the change.
