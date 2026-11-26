# RCA: the review of the side steps and the dire wolf in the stride gate (#1591, 2026-11-24)

`/deep-review` at tier 1 (two live Armory clip records, a data gate, its tests and docs; no C#, no XML): a Codex
pre-review at `ultra` (prompt `docs/reviews/codex-prereview-side-steps-wolf-stride-2026-11-24.prompt.md`, raw
log `docs/reviews/raw/codex-prereview-side-steps-wolf-stride-2026-11-24.md`) and the Data flow, Engine
compatibility and Tooling lenses in one wave. The Tooling lens rated one finding HIGH, so this file exists and a
second wave ran on the corrected code (its result is at the end). Codex rated the same finding P2 and found no
P1. Every finding was read in the code, in the native decompile or in a run before it was fixed.

F1 repeats, from the other side, the cause of F1 in
[rca-warg-stride-gate-2026-11-24.md](rca-warg-stride-gate-2026-11-24.md). There a list excused more than its
reason covered. Here the list was removed, and the checks that the list had kept went with it.

## Findings

| # | Sev | Finding | Found by | Why missed | Fix |
|---|---|---|---|---|---|
| F1 | HIGH | A clip on the standing strafe rows lost its stance and slip checks with its stride verdict. A side-step master whose foot never plants, or slips 0.8 m, passed with exit 0. Trunk judged the spider's two side steps and the elephant's and the mumak's strafe clips as gaits, and held the warg's two through `known_verdict` | Tooling lens (three inputs run in a scratch copy), Data flow lens, Codex (P2, trunk exit 1 against exit 0 on one input) | The reason for the change covered one number, the travel. The author deleted the whole branch and the tests of the `known` list and of the `axis` key, and wrote "no verdict" in the docs as if it were the aim. The 31 mutations of the first run changed code that exists; none could show a check that was no longer there | The side branch measures the feet on both axes and judges stance and slip against the ground they cover on the axis they move along (`foot_lines`, the two foot lines of `judge`). Tests: a foot that never plants, a foot that slips, a clip on forward keys. The elephant's two play walk keys and are judged along the body |
| F2 | MED | With F1 fixed, the dire wolf's two side clips fail: they are the pack's turn clips, whose paws go round a point (1.10 m against a straight line, over the wolf's gait limit of 0.35 m) | The author's run after the F1 fix; the Tooling lens predicted it | The first version printed 0.10 m and 0.08 m of sideways travel for them and judged nothing | A creature key `side_slip`, {clip: metres} (the wolf's two clips: 1.2 m), a ratchet of the same kind as the warg's `max_slip`; a listed clip that is not a side clip is a finding. The ledger says that the pack has no side-step clip |
| F3 | MED | The direction rule read the backward action's clip only. The backward pace's two strafe rows bind a second record (`dire_wolf_walkbackfast`, one of the two this change wrote), and a forward-stepping copy of it passed the gate | Codex (P2, exit 0 with directions walk -1, front +1, strafe -1), Data flow lens | `pace_actions` names one backward action, and the author took the row for the pace | Every measured clip with negative pace limits is held, with the clip on the backward action. `test_a_second_record_of_the_backward_pace_is_held_the_same_way` |
| F4 | MED | The docs gave one rule for a side step's travel (stride x `Duration`) and called it settled. The strafe clip plays at a weight of min(1, sideways speed x loop time / travel) against the stand clip (0x74E9C0, line 185 of the decompile), so under the clamp's speed band the feet fit when the travel is the stride. A public comment on #1550 proposed 0.96 m for the spider on the first rule | Engine lens, Codex (P2) | The author read the loop time (0x749C20) and stopped; the weight is forty lines on in the caller, in a function the author had open for the turn rate | The rule is stated with its two cases in `elephant.md`, the warg ledger, `spider.md`, the lesson and the tool. The comment on #1550 is corrected. No record was changed on the first rule |
| F5 | MED | Under the ground rule `"lowest"` a foot that stays above the others for the whole loop read as planted (the rule takes each foot's ground from that foot). The docstring said so and nothing held it | Tooling lens (a treadmill with one foot raised 0.2 m), Codex (a stated limit) | The author wrote the limit down and took the slip limit for cover | `lifted_foot`: a foot whose lowest point is more than 2 x `CONTACT` above the lowest foot's is a finding. The wolf's paws are 0.04 m apart at most |
| F6 | LOW | The docs cited a one-shot block clip as the proof that reversed sources play a master backward. Vanilla has the looped case: `camel_walk_backwards` plays 45 to 1 with a `quad_movement` usage on the camel's backward rows | Engine lens (a scan of the vanilla clip table); the Tooling lens and Codex had marked the loop case UNVERIFIED | The author searched the repo's docs, not the vanilla data | Cited in the ledger, the lesson and the tool. The author read the record and the vanilla action set rows again |
| F7 | LOW | `elephant.md` called the node's input "the agent's velocity". The writer takes a field of the movement record, adds a position-correction term over the tick time and caps the pair's length | Engine lens, Codex (UNVERIFIED) | The summary was written from the lines the author needed (the scale division) | Stated; the unit is marked as inferred |
| F8 | LOW | The plan files did not select the stride gates, though a plan change, once applied, changes what those gates measure | Data flow lens | The gate does not read the plan, and the author stopped there | Both plans are in their creature's stride globs; the two tests that asserted the opposite are turned |
| F9 | LOW | The rollback note for the backward walk left the two rows in the plan, so the next `--apply` would reverse the records again | Data flow lens | Copied from the #1565 rollback, where the plan is the thing rolled back to | The note names the plan rows |
| F10 | LOW | One test wrote the fixture it already had (`test_a_reversed_copy_of_the_walk_passes`); the literal list of side clips left out the wolf; `assertIn("0 finding(s)")` also matches "10 finding(s)"; the plan test kept a second copy of `grounded` | Tooling lens, Codex | Tests written beside the code they came with | The first is replaced by F3's test; the others corrected |

## Root-cause pattern

F1, F3 and F5 are one fault: the change set its check by the name of a thing and not by what the thing holds.
"A side clip gets no verdict" dropped three checks to drop one. "The backward action" stood for the backward
pace. "Each foot's own ground" stood for the ground. The item 2 review found the same fault in an exemption
list, and its lesson says that everything else about an excused item is judged as for any other item. The
author removed the list and did not carry that sentence over to the code that replaced it.

A mutation run cannot find this. It changes lines that exist. The evidence that would have found it was one
command: the deleted tests. Each deleted test of a gate is a check that the gate no longer makes, and the
author deleted `KnownFailureTests` and the `axis` test without a list of what they held.

## Why each reviewer missed what another found

- **Codex** rated F1 P2 where the Tooling lens rated it HIGH. Both showed the same input. Codex did not run the
  installed gates (its sandbox has no `numpy`), so it did not see F2.
- **Data flow lens** traced the reversed record through every reader and found no gap; it did not read the
  weight in 0x74E9C0 (F4) and marked the units of the `side` line UNVERIFIED.
- **Engine lens** read the native code and the vanilla clip table; the gate's own logic (F1, F3, F5) was
  outside its lens.
- **Tooling lens** found F1 and F5 by running broken inputs; it did not find F3, which needs the wolf's second
  record in view.

## Not applied

- The first summary line of the report counts the pace, stand and range findings only, so it can say
  "0 finding(s)" above a line that counts two (Tooling lens, LOW). The format is older than this change and
  the exit code is right. The installed tests now match the exact text of that line.
- A test of `side_clips` across two named sets (Codex). The rule builds one set of front clips over every named
  set, and the mutation that drops it fails a test on one set.
- `InstalledDireWolfTests` skips on three paths, as the warg's does; an install with a partial clip folder
  fails it (Codex). The same guard is in `InstalledWargTests` since item 2.

## Follow-ups

- FOLLOW-UP (no issue: the tool is not changed here and the wolf's master is already played from frame 0):
  `repoint_anim_clip.py` builds its list of masters that play from frame 0 from every record in the folder,
  the one it is about to rewrite included, so a row keeps its own exemption (Tooling lens, LOW).
- FOLLOW-UP #1550: the spider's side-step travel, with the corrected rule. Nobody has measured the sideways
  speed of a real side step from a stand, so nobody knows which of the two cases of F4 a player sees; the
  comment there asks for that log first. The unit of the node's turn input is open in the same issue.
- FOLLOW-UP (no issue: a footstep's moment in a backward walk, and no gate or person has asked for it): the
  step points of the two reversed wolf records are the forward walk's.
- FOLLOW-UP (no issue: the beasts have no side-step clip and nobody has reported the motion): the elephant's
  and the mumak's strafe records play the walk's keys (`elephant.md`, "Standing pace").
- Owed in game, by a person's eye: a dire wolf that backs up. The cooked entry of a reversed record holds the
  forward walk's payload byte for byte (compared after the cook of 2026-11-24 18:45), so no file shows the
  direction; the vanilla camel is the evidence that the engine plays the sources downward.

## The second wave

Four lenses on the corrected code: Standards, Efficiency, Completeness, Design. No HIGH.

| # | Sev | Finding | Found by | Fix |
|---|---|---|---|---|
| S1 | MED | The wolf's `side_slip` was a limit for the whole creature: a side step the wolf gets later would be held to 1.2 m, the scope fault of F1 the other way round. It also ignored `--max-slip` without a word | Design lens, Standards lens | `side_slip` is {clip: metres}; a listed clip that is not on the standing strafe rows is a finding; the docstring says that the command line does not go over it. The Design lens proposed to drop the slip check for the two clips; a bounded number was kept, because an exemption with no number is F1 of the review before |
| S2 | MED | `docs/reference/release-process.md` named the warg's stride check for a channel package and not the wolf's, and did not say what an older package plays on the wolf's backward rows | Completeness lens | Both added |
| S3 | MED | #1591 asked the same side-step question for the elephant and the mumak, and no doc answered it | Completeness lens | `elephant.md` "Standing pace" and a follow-up line above |
| S4 | LOW | The docs quoted the slip of a side clip (0.28 m, 0.32 m, 1.10 m) and no run printed it | Standards lens | The `side` line ends with the worst slip |
| S5 | LOW | The side branch called `judge` with an infinite stride tolerance to get its two foot lines | Design lens (output byte-identical in a scratch copy) | `foot_lines`, called by `judge` and by the side branch |
| S6 | LOW | The spider's entry test lost its check that the two side-step clips are clips the generator makes, when the `axis` key went | Standards lens, against the sentence this change added to `data-scripts.md` | The two names are literals in that loop |
| S7 | LOW | A follow-up line cited #1591, which this change closes | Standards lens | Moved to #1550, or marked `no issue:` with its reason |
| S8 | LOW | Stale or thin text: the wolf ledger's "both checks below", its replay steps 4 and 8, the feature doc's issue line, key files and tests, "Three things" over four bullets in the warg ledger, `doc-lookup.md`, `feature-map.md`, the `dire-wolf-stride` note | Completeness lens, Standards lens | Each corrected |

Not applied from the second wave: a memo of each master file's bytes (about 0.4 s of the elephant's run, for a
parameter on three functions) and a batched form of the forward kinematics. Both are in code this change did
not touch, and the gate's run time equals trunk's (Efficiency lens, measured). The Design lens judged the
direction rule's key (negative pace limits), the ground rule with `lifted_foot`, and reversed sources in place
of a backward master made in Blender as already the simplest honest form.

A last pass on the edits of this wave found no defect in them (`foot_lines` prints what the `judge` call printed
for every input, read and run on the four creatures) and three LOW points. Two were wording and are corrected.
The third is not applied: a `side_slip` that is a number, the form of the first fix, raises a `TypeError` where
`cooked` and `ground` print a table finding. No table holds such a value, and `DireWolfEntryTests` holds the
wolf's as a dict.
