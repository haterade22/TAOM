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
| 6 clip masters copied from the pack | `Assets/creature/direwolf/animations/` | `tools/apply_dire_wolf_skeleton.py --masters-from --masters-to --apply` |
| 40 clips `dire_wolf_<stem>_anm.tpac`, each the warg clip's flags on a wolf master | beside the masters | `tools/gen_dire_wolf_anim_clips.ps1 -Apply`, check with `-Verify` |
| RuntimeDataCache entries for the clips | `RuntimeDataCache/` | one Kit open, save and close (Mike). The 6 masters get no entry, which is normal: the warg has 56 such masters |

**A re-import of the grey mesh drops the skeleton item.** Run the skeleton tool and the capsule fit again after one.

## Data (2026-11-04, `tools/apply_dire_wolf_armory.py --apply`, backups `*.bak-direwolf-20261104-120720`)

| File | Change |
|---|---|
| `ModuleData/Monsters/LOTR/lotr_monster_dire_wolf.xml` | New: Monster `dire_wolf`, `monster_usage="warg"`, `action_set="as_dire_wolf"`, `family_type="1"`, the wolf's bone names, the warg's flags, `taom_body_length="240"` |
| `SubModule.xml` | The Monsters registration |
| `ModuleData/project.mbproj` | One `soln_monsters` row for the new file |
| `ModuleData/action_sets.xml` | `as_dire_wolf`, `as_dire_wolf_town_and_village`, `as_dire_wolf_map`: `as_warg` with the clip names and skeleton swapped. Action names stay `act_warg_*`. `as_warg` itself is byte-identical |
| `ModuleData/LOTRLOME_items/LOTRAOM_horses.xml` | Horse items `dire_wolf_grey`, `dire_wolf_white` (Gundabad), `dire_wolf_black`, `dire_wolf_brown` (Goblin); HorseHarness `dire_wolf_saddle` |
| `ModuleData/Languages/loc_LOTRAOM_horses.xml` | The 5 English item names |

## Installed TAOM module

Nothing. The repo's build deploys the troops, the costs and `DsAssetPackages/taom_creature_skeletons.tpac`, which
now holds the wolf skeleton (`python tools/build_ds_skeleton_pack.py --check`).

## Replay after a reinstall

1. Copy the meshes back from `E:\LOTRAOMAssets\_reskin_out\dire_wolf`, or run `prep_dire_wolf_meshes.py` again. Run
   `prep_dire_wolf_textures.py` for the textures, which are not in `_reskin_out`.
2. Import the meshes, materials and textures in the Modding Kit. Save and close.
3. Run the skeleton tool and the masters copy from the table above, then the capsule fit:
   ```
   blender -b -P tools/blender/export_skin_for_capsules.py -- <skin.json> <Armory>/AssetSources/creature/direwolf/mesh/dire_wolf_grey.fbx
   python tools/skeleton_hit_capsules.py fit --tpac <grey geo> --skin <skin.json> --mesh dire_wolf_grey --out <fit.json>
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
2. Remove the War Spoils cost rows, the marketplace rows, `dire_wolf` from `WargConfig` and `RiderlessMountRule`, and
   the wolf rows in `verify_mount_assets.py` and `audit_mount_parity.py`.
3. Remove the wolf blocks from the live files in the Data table, and delete the new Monster file.
4. Delete `Assets/creature/direwolf/`, `AssetSources/creature/direwolf/` and the package entries in `RuntimeDataCache/`.
5. Run `python tools/build_ds_skeleton_pack.py --apply`, and refresh `docs/reference/lotrlome-armory-snapshot/`.

## Snapshot

`docs/reference/lotrlome-armory-snapshot/action_sets.xml` and `project.mbproj` are byte copies of the live files after
the writer ran. Before that copy, each was byte-identical to its `.bak-direwolf` backup.
