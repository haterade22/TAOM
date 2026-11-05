# RCA: dire wolves pre-commit review (#1081, 2026-11-04)

**Summary.** The review ran at tier 1 because the change touches live Armory data. No lens or Codex pass found a defect in
the C#. All four HIGH findings were in the tools and gates written for this feature: three of them could report success
on a broken mount. The data findings were one free recruitment route and statements left stale when three cultures gained
a resource. Nothing has run in game, so these findings say nothing about the wolf's behavior in battle.

## Findings

| # | Sev | Bug | Category | Why missed | Preventive action |
|---|---|---|---|---|---|
| 1 | HIGH | `verify_mount_assets.py` looked for a clip name as a byte substring, so `x_trot` matched inside `x_trot_strafe`. 8 of 40 wolf clips could lose their tag or their file and still pass | Gate | The byte scan was inherited, and it was only ever run on healthy assets, where it passes | Exact package item names; tests with a prefix-named pair |
| 2 | HIGH | The same gate exited 0 with no movement rows or an absent action set | Gate | The tests rule names the empty input, but the gate had no test at all | FAIL on zero checked rows and on an absent set |
| 3 | HIGH | `gen_dire_wolf_anim_clips.ps1 -Verify` passed with no masters on disk and with stale checksums | Gate | `-Verify` compared one GUID to the pack and stopped; `-Apply` had separate logic | One `Get-ClipProblem` for every mode, a master map, the sibling's checksum dry run |
| 4 | HIGH | `prep_dire_wolf_meshes.py` could leave an earlier `ok` report in place after a crash before the report step | Tool | The tool wrote its own report although `blender_job.job` exists for this | Use `blender_job.job` |
| 5 | MED | The `untagged_ok` exemption keyed on the clip, so a moving action bound to a stand clip passed (Codex) | Gate | The exemption was written to quiet three known clips, not to state the row it allows | Exact (action, clip) pairs |
| 6 | MED | `apply_dire_wolf_armory.py` skipped the whole item group when one wolf item existed, and accepted any file at the Monster path (Codex) | Tool | The sets had a partial-family refusal, the items and names did not | The same refusal for each group; parse the existing Monster |
| 7 | MED | A derived action set was never checked after the text replaces | Tool | Each replace looked safe alone | Parse the block and compare id, skeleton and base set |
| 8 | MED | Three paid riders had no `recruit_cost`, so a notable sold them free. A prisoner recruit stays ungated and succeeds at a zero balance: the open #563, which Codex's re-review traced to these rows too | Data | The cost model was read from the upgrade path only | `recruit_cost="1"`; `DireWolfTroopDataTests` pins it. FOLLOW-UP: #563 |
| 9 | MED | The feature doc said "by upgrade only", and comments said goblins field no swap cavalry, but the swap may pick wolf riders | Docs | The garrison filter was not traced when the troops were added | Mike chose to allow it; docs and comments corrected; `CreatureMountRidersTests` row |
| 10 | MED | Statements that goblin cultures have no resource stayed in the kingdom recipe, Elite Emissary config, provider, test and doc | Docs | The search covered code that branches on the resource, not prose | Corrected; the emissary test now uses `lindon` |
| 11 | MED | The replay ledger missed the capsule commands, the texture source and the build pack check, and its removal steps reverted later edits | Docs | The ledger was written from memory of the steps | Corrected, with a caution on the backups |
| 12 | MED | A vanilla family 1 harness with reins fits the wolf and reaches a null rein skeleton in `AddHorseReinsClothMesh` (source-derived; not seen in game) | Known limitation | The 5-rein deviation was checked against the saddle only | Mike chose to ship: note in the commit and doc, test owed with `light_harness` |
| 13 | LOW | `--game` counted as a positional in `verify_mount_assets.py`, so the flag never worked | Tool | Pre-existing, and no test used it | argparse; test |
| 14 | LOW | The generator had no start guards, the skeleton tool let an `OSError` reach a traceback, and `--game-dir ""` fell through to the default install | Tool | Copied from a thinner sibling | Guards as in `gen_animalia_anim_clips.ps1` and `apply_wildmesh_armory.py`; tests |
| 15 | LOW | The bones test compared the tool's list with itself | Test | The two-edit test was not applied | A live parity test against the shipped skeleton |
| 16 | LOW | Stale comments: LOD chain, the master `.rdc` wording, the `<Materials>` location, counts in `special-resources.md` | Docs | Written before the Kit step proved otherwise | Corrected |

Findings from the re-review of the fixes:

| # | Sev | Bug | Category | Why missed | Preventive action |
|---|---|---|---|---|---|
| 17 | HIGH | The exact-name fix for row 1 ignored the item type, so a master named like a deleted clip still passed. Spider has 20 such names, and the wolf has none | Gate | The fix was tested on clips only, and a package holds masters too | Filter on the `AnimationClip` type GUID; test with a same-named master |
| 18 | LOW | The fix for row 2 failed only on zero rows, so one unbound gait row passed | Gate | The test written with the fix pinned this as correct | FAIL on any unbound movement row |
| 19 | MED | A commented-out item or name row counted as installed in the writer's partial check (Codex) | Tool | The check for row 6 searched raw text | Strip comments before the search; tests |

## Root-cause pattern

Rows 1 to 5 share one cause: **a gate proven only on the healthy state it was written in.** Each gate printed PASS on the
real wolf, and nobody broke the wolf to see it fail. `.claude/rules/tests.md` already requires a gate to be proven on an
empty input and on the states it will meet. These gates had no tests, because they were treated as checks on the work and
not as code. This pattern has shipped before: the `lessons/build-tooling-workflow.md` entry behind that rule.

Rows 9 and 10 share another cause: a fact that changes in data ("these cultures have no resource") also lives in prose,
and only code consumers were searched.

## Why each agent missed these

The first wave and Codex found these findings, and the second wave added the completeness and design rows. No agent reviewed
the first draft of the tools before this pass, because they were written during the feature. Lens 2 (Engine) had no gate
finding because gates are outside its scope.

## Deferred, not applied

| Item | Why |
|---|---|
| Design proposals: remove `merge_slots`, reduce `gate` arguments, reuse `world_bones`, drop `armature` | No behavior change. FOLLOW-UP: no issue, listed in a #1081 comment |
| Enlistment rosters for the wolf troops (`generate_enlistment_rosters.py --culture goblin --seed-missing`) and the coverage test comment | Generated content the maintainer should see in its own diff. FOLLOW-UP: no issue, listed in a #1081 comment |
| Regenerating `armory-catalogue/catalogue.tsv` and `troop-rosters.html` | Derived pages. FOLLOW-UP: no issue, listed in a #1081 comment |
| More guard tests for the skeleton tool, a missing-file test for `audit_mount_parity.py`, the upkeep pin in the career fork test | Tests for guards already read in review. FOLLOW-UP: no issue, listed in a #1081 comment |
| `docs/reference/engine/monster-model.md:40-41,91`, `creature-mount-authoring.md:296` wording | Pre-existing wording. FOLLOW-UP: no issue, listed in a #1081 comment |
| "Your warg" rage text, `IsWarg` delegating to `WargConfig`, the Lindon disabled-button flow | Pre-existing, outside this change. FOLLOW-UP: no issue, listed in a #1081 comment |
| `audit_fbx_lods --check` and `check_rdc_entries` reds, elephant and mumakil failing `verify_mount_assets` | Pre-existing; no wolf package named. The maintainer decides whether to file them. FOLLOW-UP: no issue, reported to the maintainer |
| Unpaid rungs at troop weight 1.0 | Balance, the maintainer's call in play |

## Feedback memories to codify

None. The rule exists: `tests.md` states it. The lesson entries below record what it missed.
