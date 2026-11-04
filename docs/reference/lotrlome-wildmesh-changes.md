# LOTRLOME_Armory changes: the WildMesh3D mounts (#794)

The live `LOTRLOME_Armory` and the installed `TAOM` module are not versioned, so this ledger records every
change the WildMesh3D mounts made outside the repo, how to replay it after a reinstall, and how to remove it.
Feature: [wildmesh-mounts.md](../features/wildmesh-mounts.md).

## Assets (2026-10-14)

| What | Where | By |
|---|---|---|
| Sources: 5 mesh FBX, `animations/<animal>/` (74 clip FBX), `textures/` (20 PNG) | `AssetSources/creature/creatures/` | Mike, from `E:\LOTRAOMAssets\_kit_ready\wildmesh` |
| Kit packages: `wildmesh_<animal>_geo.tpac`, 10 materials and their `_d` / `_s` textures, 74 masters | `Assets/creature/creatures/` | Mike, Modding Kit import |
| Masters' skeleton reference set to `horse_skeleton` (74, write-once `.bak-preskel` beside each) | `Assets/creature/creatures/animations/<animal>/` | `tools/wire_anim_master_skeletons.ps1 -Apply` |
| 96 clips `anim_wildmesh_<animal>_<stem>[_stand\|_movement]_anm.tpac` | beside their masters | `tools/gen_animalia_anim_clips.ps1 -Pack wildmesh -Apply` |
| RuntimeDataCache entries for the clips | `RuntimeDataCache/` | one Kit open and close (Mike) |
| 2026-10-15, round 2: the 74 clip FBX replaced in place (uniform resample, exact FBX times; staged in `E:\LOTRAOMAssets\_kit_ready\wildmesh_r2`, the round-1 FBX stay in `_kit_ready\wildmesh`) | `AssetSources/creature/creatures/animations/<animal>/` | Claude (copy), then Mike's Kit re-import of the 74 masters (GUIDs kept) |
| 2026-10-15: 37 clips regenerated (the 34 gait clips and three deer idles); the round-1 files moved to `E:\LOTRAOMAssets\_clip_backup\wildmesh_r1_20261015` | beside their masters | `gen_animalia_anim_clips.ps1 -Pack wildmesh -Apply`, then one Kit open and close |

## Data (2026-10-14, `tools/apply_wildmesh_armory.py --apply`, backups `*.bak-wildmesh-20261014`)

| File | Change |
|---|---|
| `ModuleData/Monsters/LOTR/lotr_monster_wildmesh.xml` | New: `taom_wildmesh_{boar,warthog,mountaingoat,deer,moose}`, `base_monster="horse"`, own action set, `taom_body_length` 100 (the moose 150); the dwarf mounts take the war ram's weight, hit points, jump acceleration and charge speed limit |
| `SubModule.xml` | Monsters registration after the Animalia one |
| `ModuleData/action_sets.xml` | `as_wildmesh_<animal>` (children of `as_horse`, 24 or 25 overrides each) with `_town_and_village` and `_map` twins, before the `/WARG` marker |
| `ModuleData/LOTRLOME_items/LOTRAOM_horses.xml` | `taom_wildmesh_<animal>_a` after `taom_animalia_moose_a`: War Boar, Bristleback Boar, Iron Hills Goat (Erebor, the war ram's stats), Mirkwood Stag, Greenwood Moose (Mirkwood, the Animalia elk's and moose's stats); not merchandise |

## Data (2026-10-15, review fixes, backups `*.bak-wildmesh-20261015-082540`)

| File | Change |
|---|---|
| `ModuleData/Languages/loc_LOTRAOM_horses.xml` | The five English rows `taom_wildmesh_<animal>_a` after `taom_animalia_moose_a`, with the items' own default names. The 12 language folders are owed (`/localize`, paid, Mike's word); until then `check_external_loc_coverage.py` reports +5 untranslated per language |

Since 2026-10-15 the writer works per animal: each step inserts only the animals its file lacks, and each run backs
up under its own stamp, `*.bak-wildmesh-<YYYYMMDD-HHMMSS>`.

## Installed TAOM module

On 2026-10-14 the test riders' block was first inserted by hand into an older installed `SubModule.xml` (backup
`SubModule.xml.bak-wildmesh-20261014`). A full build and deploy at 20:56 the same day then replaced the whole file with
the repo's, so that backup is an older build's (`DependedModuleMetadata ... v1.5.4.*`): **do not restore it**; the
release sweep (`tools/sweep_module_backups.ps1`) removes it. The riders moved to `ModuleData/test_riders/wildmesh.xml`
on 2026-10-15; deployment only adds files, so the old installed `ModuleData/troops/troops_wildmesh_test.xml` is
deleted by hand when the new file is deployed.

## Replay after a reinstall

1. Restore the sources and Kit packages from a backup of `creature/creatures` (or re-import from
   `E:\LOTRAOMAssets\_kit_ready\wildmesh`).
2. `powershell.exe -File tools\wire_anim_master_skeletons.ps1 -Masters <Armory>\Assets\creature\creatures\animations -Apply`
3. `powershell.exe -File tools\gen_animalia_anim_clips.ps1 -Pack wildmesh -Apply`, then one Kit open and close.
4. `python tools/apply_wildmesh_armory.py --apply` (Kit and game closed).
5. Gates: `python tools/check_rdc_entries.py --under creature/creatures`, `gen_animalia_anim_clips.ps1 -Pack wildmesh -Verify`,
   `python tools/audit_action_set_parity.py`, `python tools/validate_moduledata.py`. The writer's
   `LiveRecipeParityTests` fail when a live hand edit (a resize) drifts from its recipe: update the recipe too.

## Removing the test riders

Delete `Main/_Module/ModuleData/test_riders/wildmesh.xml` and its block in `Main/_Module/SubModule.xml`. The riders
carry no ladder or recruitment exemptions any more (the file sits outside `troops/`). The three dwarf mounts stay
in `DWARF_MOUNT_IDS` once real dwarf troops ride them.

**Done for v2.0.35 (2026-10-18),** and the tool test that pinned the riders (`test_wildmesh_test_riders.py`) was deleted with them. A deploy only adds files, so the
installed `Modules/TAOM/ModuleData/test_riders/wildmesh.xml` stays until the `/release` Phase 8 prune deletes it.
Once this tree is deployed the file is inert, because the deployed `SubModule.xml` no longer names it; a deploy from
a worktree that still carries the block registers the riders again. The testing channel's v2.0.35 package of
2026-10-18 (built from `6553b8c70`) still ships and registers them. Mike kept the number v2.0.35 for this release
anyway (2026-10-18, overriding `/release` pre-flight 4b): replace that package from the tag build and delete its
`Modules/TAOM/ModuleData/test_riders/` folder.

---

<!-- backlinks-start auto-generated; edit lint_docs.py / build_backlinks.py to change -->

## Referenced by

- [docs/ai-includes/quadruped-pack-to-horse-skeleton-workflow.md](../ai-includes/quadruped-pack-to-horse-skeleton-workflow.md)
- [docs/features/wildmesh-mounts.md](../features/wildmesh-mounts.md)
- [docs/INDEX.md](../INDEX.md)
- [docs/modding/file-catalogue.md](../modding/file-catalogue.md)
- [docs/reference/lotrlome-armory-snapshot/README.md](lotrlome-armory-snapshot/README.md)

<!-- backlinks-end -->
