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
ModuleData/action_sets.xml.bak-animalia-antler-20260923          (step 5, the antler bindings)
ModuleData/action_types.xml.bak-animalia-antler-20260923         (step 5, the antler actions)
ModuleData/LOTRLOME_items/LOTRAOM_horses.xml.bak-sizes-20260923  (before the moose went to 150)
*.bak-monstersize-20260923   (section 6: lotr_monster_animalia.xml, lotr_monster_elk.xml, LOTRAOM_horses.xml,
                              action_sets.xml, Languages/loc_LOTRAOM_horses.xml)
*.bak-placeholder-20260923   (LOTRAOM_horses.xml, lotr_monster_animalia.xml) DO NOT RESTORE: they hold the
                              schema-invalid state with no body_length on the three items
ModuleData/LOTRLOME_items/LOTRAOM_horses.xml.bak-market-20260923     (section 7, comment only)
ModuleData/LOTRLOME_items/LOTRAOM_horses.xml.bak-moosesale-20260923  (section 7, comment only)
ModuleData/LOTRLOME_items/LOTRAOM_horses.xml.bak-caravan-20260923    (section 7, comment only)
```

Each edit is a single insert: the file before and after differs only by the new block, with its BOM and line
endings kept (checked byte for byte on 2026-09-23). `lotr_monster_animalia.xml` is new; deleting it and the
`SubModule.xml` block reverts it.

## 1. `ModuleData/Monsters/LOTR/lotr_monster_animalia.xml`: new file

| Monster | base | action set | weight / hit points | `taom_body_length` (section 6) |
|---|---|---|---|---|
| `taom_animalia_elk` | `horse` | `as_animalia_elk` | 500 / 250 (the great elk's) | 100 |
| `taom_animalia_moose` | `horse` | `as_animalia_moose` | 600 / 300 | 150 |

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
`_town_and_village` and `_map` twins are empty children of the horse's variants. `_map` is required:
`MobilePartyVisual` looks up the Monster's action set + `"_map"` and `MBGlobals.GetActionSet` throws when it is
missing. `_town_and_village` only mirrors vanilla's `as_horse_town_and_village`; nothing in v1.5.3 appends it. The full binding list is `OVERRIDES` in
`tools/apply_animalia_armory.py`; `AnimaliaMountWiringTests` asserts every bound clip exists and every bound type
is an `as_horse` action.

## 4. `ModuleData/LOTRLOME_items/LOTRAOM_horses.xml`: two items

Inserted after `taom_elk_saddle_a`.

| id | mesh | stats |
|---|---|---|
| `taom_animalia_elk_a` "Mirkwood Elk" | `animalia_elk_08` | `Monster.taom_animalia_elk`, maneuver 74, speed 62, charge 50, extra_health 20, weight 450, value 1400 |
| `taom_animalia_moose_a` "Mirkwood Moose" | `animalia_moose_big` | `Monster.taom_animalia_moose`, maneuver 60, speed 56, charge 65, extra_health 40, weight 550, value 1800 |

Both `culture="Culture.mirkwood"`, `difficulty="0"`, `is_merchandise="false"`, `<Flags Civilian="true" />`.
`is_merchandise` keeps them out of vanilla loot, workshop output and tournament prizes; TAOM's CultureMarketplace and
vanilla caravans do not read it, so the Mirkwood culture pool can draw either into a Mirkwood market, and the Animalia elk is also routed there
with `min_stock` 1 (`culture_marketplace_config.xml`; section 7). **`body_length="100"` is only a placeholder** since section 6 (the engine's
`Items.xsd` requires the attribute): the size is each Monster's `taom_body_length`. Before that the items carried it: 100 for both (the authored size; both
meshes were fitted to the horse), the moose raised to 150 after the first battle (backup `.bak-sizes-20260923`). The
moose's gait `LoopDisplacement`s were measured at 100, so watch its hooves for slide at 150 (whether the engine
scales a clip's travel with the size is not established). The seat is `taom_elk_saddle_a` (#636) until the animals
get their own.

## 5. The antler attacks (step 5 of `tools/apply_animalia_armory.py`, 2026-09-23 evening)

| File | Edit |
|---|---|
| `ModuleData/action_types.xml` | `act_animalia_elk_antler` and `act_animalia_moose_antler` declared `actt_kick`, after `act_war_ram_butt`, with a comment. Backup `action_types.xml.bak-animalia-antler-20260923` |
| `ModuleData/action_sets.xml` | `as_animalia_elk` binds `act_animalia_elk_antler` to `anim_animalia_elk_attack_front_low`; `as_animalia_moose` binds `act_animalia_moose_antler` to `anim_animalia_moose_attack_head_01`. Backup `action_sets.xml.bak-animalia-antler-20260923` |

Both edits are pure inserts (5 lines and 2 lines; BOM and CRLF kept). Their own actions rather than
`act_horse_kick`, which the horse usage set fires itself. TAOM's `Main/Features/Animalia/` fires them;
`AnimaliaMountWiringTests.AntlerActions_AreKickTyped_AndBoundToTheirAttackClip` pins them.

## 6. Size on the Monster, names, comments (2026-09-23 evening, backups `.bak-monstersize-20260923`)

Mike: "The monster xml should control the size of the animal." Made by a one-off script (every edit exact-once, every
file parsed after, BOM and line endings kept); `tools/apply_animalia_armory.py` now writes the same recipe.

| File | Change |
|---|---|
| `Monsters/LOTR/lotr_monster_animalia.xml` | `taom_body_length="100"` on the elk, `"150"` on the moose; the header says the size lives here |
| `LOTRLOME_items/LOTRAOM_horses.xml` | `body_length` set to the placeholder 100 on `taom_animalia_elk_a` and `taom_animalia_moose_a` (and on the great elk's `taom_elk_a`, [elk ledger](lotrlome-elk-changes.md) section 7); the block comment rewritten |
| `action_sets.xml` | The twins comment no longer says the engine derives both (see section 3) |
| `Languages/loc_LOTRAOM_horses.xml` | English rows `taom_animalia_elk_a` "Mirkwood Elk", `taom_animalia_moose_a` "Mirkwood Moose" after the elk saddle's; the twelve languages are owed (`/localize`) |

TAOM copies each `taom_body_length` into the items at every game init ([monster-size.md](../features/monster-size.md)).
The engine's `Monsters.xsd` does not declare the attribute, so the rgl log shows one "not declared" validation line
per sized Monster at load; the file loads. The in-repo snapshot `docs/reference/lotrlome-armory-snapshot/` was
refreshed the same evening (`action_sets.xml` +81 lines, `action_types.xml` +5).

## Redo after a reinstall

Order matters: the recipe anchors on the war ram's edits (`act_war_ram_butt`, the `/WARG` marker:
[lotrlome-war-ram-changes.md](lotrlome-war-ram-changes.md)) and the great elk's (`taom_elk_saddle_a` and its Monsters
registration: [lotrlome-elk-changes.md](lotrlome-elk-changes.md)). Redo those first. Every writer below refuses to
run while Bannerlord or the Modding Kit is open (a Kit save rewrites what it loaded).

1. Restore the assets (Mike's layout, then a Kit load and save).
2. `powershell.exe -File tools\wire_anim_master_skeletons.ps1 -Masters <Armory>\Assets\creature\elk\animations -Apply`
3. `powershell.exe -File tools\gen_animalia_anim_clips.ps1 -Apply`, then one Kit load for the clips' RDC entries;
   `-Verify` afterwards (and after any clip re-import) reports orphaned or stale clips.
4. `python tools/apply_animalia_armory.py` (the dry run: all five steps must print), then `--apply`.
5. `dotnet test TAOM.Tests -p:DisableModuleCopy=true -p:ModuleId= --filter FullyQualifiedName~Animalia` (the wiring,
   the size pins and the antler pins), `python tools/audit_action_set_parity.py`, `python tools/validate_moduledata.py`,
   `python tools/validate_xml_schemas.py --live`, and `python -m pytest tools/tests/test_apply_animalia_armory.py`
   (its `LiveRecipeParityTests` compare the recipe with what you just wrote).
6. Re-add the English name rows (section 6) if the loc file came back without them.

Steps 2 and 3 need Windows PowerShell 5.1 (`powershell.exe -NoProfile -ExecutionPolicy Bypass -File ...`; `pwsh`
exits 2) and two things outside the repo: TpacTool's `bin` folder (`-TpacBin`, default
`E:\Bannerlord_Art\TpacTool_0.4.0\TpacTool\bin`) and `TolerantTpacLoader.cs` (`-Loader`, default
`E:\LOTRAOMAssets\_auto_workspace\chariot\TolerantTpacLoader.cs`). Both scripts exit 2 when either is missing.

## 7. Comments on the item block (2026-09-23 night)

Comment-only edits to the Animalia item block in `LOTRAOM_horses.xml`: first (`.bak-market-20260923`) that the elk
is routed into Mirkwood markets, then (`.bak-moosesale-20260923`) the market wording corrected to what TAOM's
CultureMarketplace does: it does not read `is_merchandise`, so the Mirkwood culture pool can draw either animal
(Mike: the moose may be sold), then (`.bak-caravan-20260923`) the caravan clause corrected (caravans do not read
`is_merchandise`). The comment matches `ITEMS` in `tools/apply_animalia_armory.py`; no test compares comments, so
diff them by hand after an edit.
