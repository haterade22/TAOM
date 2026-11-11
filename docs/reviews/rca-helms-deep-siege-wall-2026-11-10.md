# RCA: Helm's Deep siege set-up throws, and The Long Defeat stalls in wave 1 (#1115, 2026-11-10)

## Top-line

The first battle on the Helm's Deep scene since its visual overhaul of 2026-11-06 ended in a stalemate in wave 1 of
The Long Defeat. The cause is one missing script in the scene file, not the engine update to v1.5.7 and not TAOM code.
The scene is fixed in the live `TAOM_Map` and in the testing channel copy, and a gate now reads every map scene for
the same fault.

## Symptom

An unattended run of The Long Defeat on the v2.0.38 tag build (`+5579a59a6`), Bannerlord v1.5.7, run folder
`E:\Temp\long-defeat-runs\20261110-160344`:

- `reason=stalemate`, wave 1 not cleared after 539 mission seconds, 82 of 110 attackers killed, 165 defenders standing.
- `[Check] id=taom.long-defeat.stale result=fail`. No crash, and the module unloaded cleanly.
- One exception at the mission start, caught by Mission Start Guard: `NullReferenceException` in
  `WallSegment.OnChooseUsedWallSegment`, called from `SiegeMissionPreparationHandler.ArrangeDestructedMeshes` and
  `SetUpScene`.

## Root cause

`OnChooseUsedWallSegment` calls `GetFirstScriptOfType<WallSegment>()` on the wall's `solid_child` and on its
`broken_child` with no null check ([the mechanism and its line numbers](../reference/engine/scene-gameentity-scriptcomponent.md#siege-walls-two-child-scripts-the-set-up-reads-with-no-null-check)).
In the live scene `taom_rohan_castle_helms_deep_forceatmo`, the right wall
(`Lotraom_comprehend_Helms_Deep_Wall_Complete_Right`) had a `broken_child`, `sm_helms_deep_wall_broken_right`, with no
`<scripts>` block. Its left twin, `sm_helms_deep_wall_broken_left`, had the `WallSegment` script.

The set-up took the right wall first. `FindRightMostWall` picks it whatever the list order: from the ram spawner at
(253.1, 315.3), the cross product of the two wall offsets is +5,700 one way and -5,700 the other, and a spawner places
its ram on itself (`SpawnerEntityMissionHelper.cs:41-48`). So the exception came on the first call, and neither wall
was arranged. For a solid wall, the lines that disable its over-debris navmesh faces come after the two script calls
and never ran. Whether those faces load enabled, and what the attackers then did, was not traced; the proof is the run
below.

**What changed, and when:** four older copies of the scene under `E:\LOTRAOMAssets` (June to August 2026) hold the
script on both breach children, then named `Lotraom_comprehend_Gondor_Wall_BROR` and `..._BRO`. The mirror repo shows
two commits on the scene file on 2026-11-06, "Helm's Deep Visual Overhaul Pt.1" and "Helm's Deep Hotfix". The overhaul
replaced both breach meshes and gave only the left one the script. No battle ran on the scene between that save
(2026-11-06 11:36) and 2026-11-10.

**Not the engine:** `OnChooseUsedWallSegment`, `ArrangeDestructedMeshes` and `SetUpScene` are identical in the
v1.5.6.125270 and v1.5.7.125802 decompiles.

## Proof

The left twin's `<scripts>` block was copied byte for byte into the right entity (841 bytes, CRLF kept). The same match
then ran again with the same seed (8795502808933483), class and build, run folder
`E:\Temp\long-defeat-runs\20261110-162357`:

| | Scene of 2026-11-06 | Scene with the script |
|---|---|---|
| Exception at the siege set-up | 1 | 0 |
| Wave 1 | stalemate after 539 s | cleared in 362 s |
| Wave 2 | not reached | cleared in 655 s, the gate fell |
| Wave 3 | not reached | in progress when the maintainer closed the game |

The run did not reach its five waves, so the launcher reports it as failed ("the game exited before the done file").
Not covered: a campaign siege of Helm's Deep, and a breach of either wall.

## Reach

- The fault also applies to a campaign or Custom Battle siege on this scene: the same base game set-up runs there.
- A scan of all 59 live `TAOM_Map` scene files found this one fault. As a control, 479 base game scene files with 462
  such walls hold none.
- Two copies were fixed on the maintainer's word: the live `TAOM_Map` and
  `E:\LOTRAOM_Releases\testing\Modules\TAOM_Map`. They were byte-identical before the fix and are byte-identical after
  it. The originals and the hashes are in `E:\Temp\taom-scene-fix\helms-deep-20261110\`.
- Not fixed: the mirror repo copy, and the patreon and public channel copies, which were not read. A sync from the
  mirror puts the fault back.

## Prevention

- **Gate:** `tools/check_siege_wall_scripts.py`, with its tests, a live-install test in the tool suite and the
  `siege-walls` row of `tools/data_gates.py`. It fails on the scene as it was (it names the wall and the child) and
  passes on the fixed one. It also fails a scene with an odd number of sided breach walls, because the set-up takes
  them in pairs.
- **Lesson:** [adapters-taleworlds-api.md](lessons/adapters-taleworlds-api.md), "A mission start that survives an
  exception is not a mission that started".
- **Release check:** `docs/reference/release-process.md` asks for the gate on each channel's `TAOM_Map`.

## Still open

- `taom.long-defeat.muster` fails for one batch of bought infantry (1 of 20 off the navmesh for a few seconds), with
  and without the fix. It is not caused by the wall.
- Mission Start Guard kept the mission alive, which is its job. Its traces were one `[ERROR]` line, one `[WARNING]`
  summary (`mission start survived 1 failed call(s)`) and an on-screen notice, and no check of an unattended run reads
  any of them. Whether a swallowed set-up exception should fail an unattended run is the maintainer's choice; the
  summary line is the one to match, and the Fords runner already fails a run on a `[CrashReport]` block.
- FOLLOW-UP no issue: every TAOM siege scene has two walls and a ram today. The gate fails an odd number of sided breach walls and does not judge an even wrong number or a missing ram: the set-up needs exactly two walls and a ram among the entities the mission loads, base game scenes hold two walls for each of three upgrade levels in one file, and which entities a level loads is native and was not verified.

## Review of the gate

Two reviewers (tooling, engine compatibility) and one checker read the gate before the commit. They reported no CRITICAL
or HIGH. Each confirmed finding was re-read against the code or the decompile before it was fixed.

| # | Severity | Finding | Done |
|---|---|---|---|
| 1 | MEDIUM | `data_gates.py` printed PASS for a named scene file that the gate never read: the tool ran bare, on the live install | The tool takes scene files, and the row is `files=True`. `data_gates.py` now fails on the scene as it was |
| 2 | MEDIUM | The doc said one or two walls are safe. One wall also throws, because the callers pass two hit-point entries | The doc and this record state the rule from the caller's loop, and the gate fails an odd number of walls |
| 3 | LOW | The catalogue row and the doc said a missing `broken_child` fails; neither the engine nor the gate takes such a wall. The row sat outside its table | Reworded, and the row is in the table with its flags |
| 4 | LOW | No test pinned the script's name or the case of `SideTag` | Two cases added |
| 5 | LOW | The record left open which wall the set-up took first | Computed from the scene: the right wall, so neither was arranged |
| 6 | LOW | "The only trace was one `[ERROR]` line": the guard also wrote a summary and showed a notice | Corrected here and in the lesson |
| 7 | LOW | `visible="false"` was listed as a need of the `broken_child` | The table calls it a convention |
| 8 | LOW | The tool and the doc stated the stall mechanism as fact | Both now say what is not verified |
| 9 | LOW | A scene with a namespaced root, or a scene file two folders deep, reads as wall-free | Not applied: no current input has either shape, and the guard would be speculative |
| 10 | LOW, unproven | A count rule per upgrade level | Not applied: it rests on native level loading that nobody verified. The odd-number rule needs no level reading |

Lessons: [build-tooling-workflow.md](lessons/build-tooling-workflow.md), "A gate that a named file selects judges that
file", and [adapters-taleworlds-api.md](lessons/adapters-taleworlds-api.md), "A rule about what a callee needs comes from
its caller's loop".
