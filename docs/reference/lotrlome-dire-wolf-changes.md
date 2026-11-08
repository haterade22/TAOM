# LOTRLOME_Armory changes: the dire wolves (#1081)

The live `LOTRLOME_Armory` is not versioned, so this ledger records every change the dire wolves made outside the
repo, how to replay it after a reinstall, and how to remove it. Feature: [dire-wolf.md](../features/dire-wolf.md).

## Assets (2026-11-04)

| What | Where | By |
|---|---|---|
| Sources: 4 mesh FBX with LOD0 to LOD5, the saddle in the grey FBX only (it binds the warg's `orc_rider_saddle` material), and 21 texture maps | `AssetSources/creature/direwolf/{mesh,textures}` | `tools/blender/prep_dire_wolf_meshes.py`, `tools/prep_dire_wolf_textures.py`, from `E:\LOTRAOMAssets\ADOD_Beasts\AssetSources\wolves` |
| Kit packages: 4 `dire_wolf_<skin>_geo.tpac`, 13 materials and their textures | `Assets/creature/direwolf/` | Mike, Modding Kit import |
| The skeleton `adod_wolf_armature_unused`, bundled in the grey `_geo` tpac with `Usage` horse, 57 bodies and 56 constraints | `Assets/creature/direwolf/mesh/dire_wolf_grey_geo.tpac` (backup `.bak-skeleton-20261104-114926`) | `tools/apply_dire_wolf_skeleton.py --tpac <grey geo> --source <pack armature geo> --apply` |
| Hit capsules on 47 bones | the same tpac (backup `.bak-hitcapsules-*`) | `tools/skeleton_hit_capsules.py` |
| 6 clip masters copied from the pack (replaced on 2026-11-06 by Kit imports; the copy mode is removed) | `Assets/creature/direwolf/animations/` | the former `apply_dire_wolf_skeleton.py --masters-from --masters-to --apply` |
| 40 clips `dire_wolf_<stem>_anm.tpac`, each the warg clip's flags on a wolf master | beside the masters | `tools/gen_dire_wolf_anim_clips.ps1 -Apply`, check with `-Verify` |
| RuntimeDataCache entries for the clips | `RuntimeDataCache/` | one Kit open, save and close (Mike). The 6 masters get no entry, which is normal: the warg has 56 such masters |

**A re-import of the grey mesh can drop the skeleton item, or keep the old one.** Run the skeleton tool with
`--replace` and the capsule fit again after one.

## Resize to warg size (2026-11-06)

The rig was rebuilt at 1.73 times the pack's size ([dire-wolf.md](../features/dire-wolf.md) "Size"). Backup of every
file it replaced: `E:\LOTRAOMAssets\_backup_direwolf_resize_20261106\` (`AssetSources_mesh`, `Assets_mesh`,
`Assets_animations` with the 6 old masters and the 40 old clips, and `capsule_fit_x173.json`).

| What | Where | By |
|---|---|---|
| Scaled mesh FBX | `AssetSources/creature/direwolf/mesh/` | `prep_dire_wolf_meshes.py --scale 1.73` (the default then), output `E:\LOTRAOMAssets\_reskin_out\dire_wolf_x173\mesh` |
| Scaled animation FBX, the pack's take names | `AssetSources/creature/direwolf/animations/` (new folder) | `tools/blender/scale_dire_wolf_anims.py`, output `_reskin_out\dire_wolf_x173\animations` |
| Mesh packages, 1.616 m tall | `Assets/creature/direwolf/mesh/` | Mike, Kit re-import of the 4 meshes |
| The scaled skeleton (the Kit kept the old one) | grey `_geo` tpac (backup `.bak-skeleton-20261106-124548`) | `apply_dire_wolf_skeleton.py --replace --apply` |
| Hit capsules: 47 refit, 10 kept | the same tpac (backup `.bak-hitcapsules-20261106-124617`) | `skeleton_hit_capsules.py` with `--limit 0.346 --margin 0.052` |
| 6 masters, Kit-imported from the scaled FBX | `Assets/creature/direwolf/animations/` | Mike, Kit import; then `wire_anim_master_skeletons.ps1 -SkeletonGuid 0b57b590-4668-41e4-92b8-27db1c8fed86 -BoneNum 57 -Apply` (backups `.bak-preskel`) |
| 40 clips, generated again | beside the masters | the old clips deleted, then `gen_dire_wolf_anim_clips.ps1 -Apply` and `-Verify` |
| Monster lengths times 1.73, `taom_body_length` 110 | `lotr_monster_dire_wolf.xml` (backup `.bak-direwolf-x173-20261106-160216`) | by hand, to the text `apply_dire_wolf_armory.py` writes for a new file |
| `taom_body_length` 138 (25% larger in battle) and the new header comment | the same file (backup `.bak-direwolf-size138-20261106-174200`) | the same way; `test_the_live_monster_is_the_recipe` holds the file to the writer |
| The 10 gait clips again, travel times 1.73 only (the engine applies agent scale itself) | beside the masters | the 10 deleted, then `gen_dire_wolf_anim_clips.ps1 -Apply` and `-Verify` |

The new masters were compared with the pack's: the same durations and key counts, rotations within 0.006 rad, and
root motion 1.73 times the pack's.

## Seat under the warg's rider clip (2026-11-06)

In the first ride the rider sat over the hips: the engine seats a rider where the usage's rider clip puts it, and the
dire wolf uses the warg's ([dire-wolf.md](../features/dire-wolf.md) "Seat"). The rig was rebuilt at 2.08 times the
pack's size and moved 0.52 model units toward the tail. Backup of every file it replaced:
`E:\LOTRAOMAssets\_backup_direwolf_seat_20261106\` (`AssetSources_mesh`, `AssetSources_animations`, `Assets_clips_x173`
with the 40 old clips, `Assets_mesh_kit_x208`, `skin_x208.json`, `capsule_fit_x208.json`).

| What | Where | By |
|---|---|---|
| Mesh and animation FBX at 2.08, moved 0.52 toward the tail | `AssetSources/creature/direwolf/{mesh,animations}/` | `prep_dire_wolf_meshes.py` and `scale_dire_wolf_anims.py` (defaults), output `E:\LOTRAOMAssets\_reskin_out\dire_wolf_x208` |
| Mesh packages, 1.93 m tall | `Assets/creature/direwolf/mesh/` | Mike, Kit re-import of the 4 meshes |
| 6 masters, re-imported in place; the skeleton reference survived | `Assets/creature/direwolf/animations/` | Mike, Kit import; `wire_anim_master_skeletons.ps1 ... -Apply` patched 0 |
| The skeleton at 2.08, root 0.52 back | grey `_geo` tpac (backup `.bak-skeleton-20261106-192816`) | `apply_dire_wolf_skeleton.py --replace --apply` |
| Hit capsules: 47 refit, 10 kept | the same tpac (backup `.bak-hitcapsules-20261106-192829`) | `skeleton_hit_capsules.py` with `--limit 0.416 --margin 0.0624` |
| 40 clips, generated again (gait travel times 2.08) | beside the masters | the 40 deleted, then `gen_dire_wolf_anim_clips.ps1 -Apply` and `-Verify` |
| Monster lengths times 2.08, body capsule 0.52 back, `taom_body_length` 115 | `lotr_monster_dire_wolf.xml` (backup `.bak-direwolf-seat-20261106-192844`) | the text `apply_dire_wolf_armory.py` writes; `test_the_live_monster_is_the_recipe` |

The masters hold no Pelvis position track: `RootPositionFrames` is the motion from the skeleton's rest pose, and it is
exactly 2.08 times the pack's. The 0.52 move is in the skeleton's Pelvis rest position.

## Data (2026-11-04, `tools/apply_dire_wolf_armory.py --apply`, backups `*.bak-direwolf-20261104-120720`)

| File | Change |
|---|---|
| `ModuleData/Monsters/LOTR/lotr_monster_dire_wolf.xml` | New: Monster `dire_wolf`, `monster_usage="warg"`, `action_set="as_dire_wolf"`, `family_type="1"`, the wolf's bone names, the warg's flags, `taom_body_length="240"`, set to 190 on 2026-11-06 by hand (backup `.bak-direwolf-size-20261106-101400`), then to the resized rig's values (see "Resize to warg size") |
| `SubModule.xml` | The Monsters registration |
| `ModuleData/project.mbproj` | One `soln_monsters` row for the new file |
| `ModuleData/action_sets.xml` | `as_dire_wolf`, `as_dire_wolf_town_and_village`, `as_dire_wolf_map`: `as_warg` with the clip names and skeleton swapped. Action names stay `act_warg_*`. `as_warg` itself is byte-identical |
| `ModuleData/LOTRLOME_items/LOTRAOM_horses.xml` | Horse items `dire_wolf_grey`, `dire_wolf_white` (Gundabad), `dire_wolf_black`, `dire_wolf_brown` (Goblin); HorseHarness `dire_wolf_saddle` |
| `ModuleData/Languages/loc_LOTRAOM_horses.xml` | The 5 English item names |

## Installed TAOM module

Nothing. The repo's build deploys the troops, the costs and `DsAssetPackages/taom_creature_skeletons.tpac`, which
now holds the wolf skeleton (`python tools/build_ds_skeleton_pack.py --check`).

## Replay after a reinstall

1. Copy the meshes and animation FBX back from `E:\LOTRAOMAssets\_reskin_out\dire_wolf_x208`, or run
   `prep_dire_wolf_meshes.py` and `scale_dire_wolf_anims.py` again. Run `prep_dire_wolf_textures.py` for the
   textures, which are not in `_reskin_out`.
2. Import the meshes, materials and textures in the Modding Kit. Import the 6 animation FBX into
   `Assets/creature/direwolf/animations` with skeleton import off. Save and close.
3. Run the skeleton tool (`--replace` if the Kit kept an old skeleton) and `wire_anim_master_skeletons.ps1` from the
   resize table, then the capsule fit:
   ```
   blender -b -P tools/blender/export_skin_for_capsules.py -- <skin.json> <Armory>/AssetSources/creature/direwolf/mesh/dire_wolf_grey.fbx
   python tools/skeleton_hit_capsules.py fit --tpac <grey geo> --skin <skin.json> --mesh dire_wolf_grey --out <fit.json> --limit 0.416 --margin 0.0624
   python tools/skeleton_hit_capsules.py patch --tpac <grey geo> --fit <fit.json> --apply
   ```
4. In Windows PowerShell 5.1, run `powershell.exe -NoProfile -ExecutionPolicy Bypass -File tools/gen_dire_wolf_anim_clips.ps1 -Apply`.
5. Open the Kit once, save and close it, so that it writes the clip entries.
6. Run `python tools/check_rdc_entries.py --under creature/direwolf`.
7. Run `python tools/apply_dire_wolf_armory.py --apply`. A second run reports nothing to write.
8. Run `python tools/verify_mount_assets.py dire_wolf`, `python tools/audit_mount_parity.py`,
   `python tools/validate_xml_schemas.py --live` and `python tools/build_ds_skeleton_pack.py --check`.

## Removing the wolves

**Caution:** the `.bak-direwolf` backups hold the files as they were on 2026-11-04. Restoring one reverts every later
edit to that file, so remove the wolf blocks by hand unless nothing else changed it.

1. Repoint the rosters of the six troops at another mount and remove them from the repo's upgrade targets. A troop id
   stays once a save can hold it.
2. Remove the War Spoils cost rows, the marketplace rows, `dire_wolf` from `WargConfig`, and
   the wolf rows in `verify_mount_assets.py` and `audit_mount_parity.py`.
3. Remove the wolf blocks from the live files in the Data table, and delete the new Monster file.
4. Delete `Assets/creature/direwolf/`, `AssetSources/creature/direwolf/` and the package entries in `RuntimeDataCache/`.
5. Run `python tools/build_ds_skeleton_pack.py --apply`, and refresh `docs/reference/lotrlome-armory-snapshot/`.

## Snapshot

`docs/reference/lotrlome-armory-snapshot/action_sets.xml` and `project.mbproj` are byte copies of the live files after
the writer ran. Before that copy, each was byte-identical to its `.bak-direwolf` backup.
