# LOTRLOME_Armory changes for the Animalia elk and moose (2026-09-23)

The Animalia elk and moose (#646, [animalia-elk-moose.md](../features/animalia-elk-moose.md)) live in the
**external `LOTRLOME_Armory` module**, which this repo does not track. A module reinstall silently reverts every
change below, and nothing in CI sees them. This ledger records each edit, why it exists, and how to redo it.
Built as the great elk was ([lotrlome-elk-changes.md](./lotrlome-elk-changes.md)), with one difference: these
two animals play their OWN clips, so each has its own action set instead of borrowing one.

## Assets (Modding Kit, Mike, 2026-09-23)

| Path | Note |
|---|---|
| `AssetSources/creature/elk/animalia_elk_08.fbx`, `animalia_moose_big.fbx` | The two variants in use, from `tools/blender/reskin_animalia_to_horse.py` |
| `AssetSources/creature/elk/animations/{elk,moose}/*.fbx`, `textures/*.png` | 64 + 33 retargeted clips, 9 textures at 1K |
| `Assets/creature/elk/animalia_elk_08_geo.tpac`, `animalia_moose_big_geo.tpac`, `textures/` | Kit output: two meshes, 9 textures, 3 materials |
| `Assets/creature/elk/animations/{elk,moose}/*_geo.tpac` | 97 animation masters. The Kit left every master's Skeleton EMPTY; `tools/wire_anim_master_skeletons.ps1 -Apply` pointed all 97 at `horse_skeleton` (`.bak-preskel` beside each) |
| `Assets/creature/elk/animations/{elk,moose}/anim_animalia_*_anm.tpac` | 54 clips written by `tools/gen_animalia_anim_clips.ps1` (52, then the two `_movement` standing clips); they need one Kit load to get their RuntimeDataCache entries |

`AssetSources/creature/animalia/` is the first staging copy (all six variants, all clips); Mike's layout above
supersedes it.

## Backups

Written once by `tools/apply_animalia_armory.py --apply`, beside each edited file, with a non-`.xml` extension
(the engine globs `*.xml`):

```
SubModule.xml.bak-animalia-20260923
ModuleData/action_sets.xml.bak-animalia-20260923
ModuleData/LOTRLOME_items/LOTRAOM_horses.xml.bak-animalia-20260923
```

Each edit is a single insert: the file before and after differs only by the new block, with its BOM and line
endings kept (checked byte for byte on 2026-09-23). `lotr_monster_animalia.xml` is new; deleting it and the
`SubModule.xml` block reverts it.

## 1. `ModuleData/Monsters/LOTR/lotr_monster_animalia.xml`: new file

| Monster | base | action set | weight / hit points |
|---|---|---|---|
| `taom_animalia_elk` | `horse` | `as_animalia_elk` | 500 / 250 (the great elk's) |
| `taom_animalia_moose` | `horse` | `as_animalia_moose` | 600 / 300 |

`base_monster="horse"` inherits everything the war ram's and great elk's do (Flags, `family_type`,
`monster_usage="horse"`, `num_paces`, every bone, the slope block, the rein attributes).

## 2. `SubModule.xml`: one registration block

A `Monsters` `XmlNode` for `Monsters/LOTR/lotr_monster_animalia`, inserted after the great elk's block, same game
types. No `project.mbproj` entry, for the reason the war ram's ledger gives.

## 3. `ModuleData/action_sets.xml`: two action sets and their twins

Inserted after the war ram's sets, before the `/WARG` marker. `as_animalia_elk` (33 actions) and
`as_animalia_moose` (30) are children of `as_horse` on `horse_skeleton`; each action copies the vanilla
`as_horse` action's attributes (the `alternative_group`s) and changes only the clip. Bound: the standing pace
(`act_horse_stand_for_movement_data`), stands, idles, riderless idles, the forward gaits and their `_stand`
twins (both gallop feet on one clip), backward walk, the kick, the falls and their `_continue` holds; the elk also
binds `act_horse_rear` and the two hit reactions. Turns, jumps, strafes and quick stops stay the horse's. The
`_town_and_village` and `_map` twins are empty children of the horse's variants: the engine derives both from the
Monster's action set, and `_map` throws when missing. The full binding list is `OVERRIDES` in
`tools/apply_animalia_armory.py`; `AnimaliaMountWiringTests` asserts every bound clip exists and every bound type
is an `as_horse` action.

No antler-attack action yet: it needs an `action_types.xml` entry (typed `actt_kick`, as `act_war_ram_butt`) and
the C# that fires it. Its clips exist (`anim_animalia_elk_attack_front_low`, `anim_animalia_moose_attack_head_01`).

## 4. `ModuleData/LOTRLOME_items/LOTRAOM_horses.xml`: two items

Inserted after `taom_elk_saddle_a`.

| id | mesh | stats |
|---|---|---|
| `taom_animalia_elk_a` "Mirkwood Elk" | `animalia_elk_08` | `Monster.taom_animalia_elk`, maneuver 74, speed 62, charge 50, extra_health 20, `body_length="100"`, weight 450, value 1400 |
| `taom_animalia_moose_a` "Mirkwood Moose" | `animalia_moose_big` | `Monster.taom_animalia_moose`, maneuver 60, speed 56, charge 65, extra_health 40, `body_length="100"`, weight 550, value 1800 |

Both `culture="Culture.mirkwood"`, `difficulty="0"`, `is_merchandise="false"` (not in shops until they have
riders), `<Flags Civilian="true" />`. `body_length` 100 is the authored size: both meshes were fitted to the horse.
Size is a later decision; if it changes, the gait `LoopDisplacement`s were measured at 100. The seat is
`taom_elk_saddle_a` (#636) until the animals get their own.

## Redo after a reinstall

1. Restore the assets (Mike's layout, then a Kit load and save).
2. `powershell.exe -File tools\wire_anim_master_skeletons.ps1 -Masters <Armory>\Assets\creature\elk\animations -Apply`
3. `powershell.exe -File tools\gen_animalia_anim_clips.ps1 -Apply`, then one Kit load for the clips' RDC entries.
4. `python tools/apply_animalia_armory.py --apply` (Kit and game closed).
5. `dotnet test TAOM.Tests --filter FullyQualifiedName~AnimaliaMountWiringTests`, `python tools/audit_action_set_parity.py`,
   `python tools/validate_moduledata.py`.
