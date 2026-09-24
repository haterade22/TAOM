# Workflow: a bought quadruped pack onto `horse_skeleton`, with its own animation

The procedure that brought the Fab "Animalia" elk and moose into TAOM on 2026-09-23 (#646,
[animalia-elk-moose.md](../features/animalia-elk-moose.md)): from a Fab purchase to a mount that plays its own
gaits, idles, hits, deaths and attack in a Custom Battle. Proven in game: "The running and walking and idle
animations look great" (Mike). Every stage names its tool, who runs it, what it writes and the gate that
proves it; the gotchas at the end are the ones this pack actually hit.

## When this route applies

Use it for a four-legged creature bought on its **own rig** (Fab, a marketplace) that should ride like a horse.
The result is a horse-skeleton reskin: the Monster is `base_monster="horse"`, so it inherits the horse's flags,
reins, bones and usage set, and none of the custom-creature Phases 1 to 5 of
[creature-mount-authoring.md](creature-mount-authoring.md) apply. What it adds over a plain reskin (the war ram,
the great elk) is the pack's **own clips**, bound in an action set of its own.

**The go / no-go measurement:** after one uniform scale, the pack's leg and spine joints must sit within about
10 cm of the horse's (the Animalia elk and moose: 2 to 11 cm). Head, neck and tail gaps are usually posture and
are fine. A body that cannot take horse proportions in one part keeps its own there through a per-animal profile
(the moose's neck, half a horse's), at the price of joints that no longer sit on the horse's. A creature whose
legs or spine are far off (a spider, an elephant) needs its own skeleton instead.

## The stages at a glance

| # | Stage | Who | Tool | Output | Gate |
|---|---|---|---|---|---|
| 0 | Get the pack into a UE project | Mike | Epic launcher / Fab | `Content/<Pack>/` | the folder exists |
| 1 | Export | Claude | `tools/oneoff/ue_export_cave_troll.py` | FBX, TGA, `inventory.json` | `export_report.json` failed lists empty |
| 2 | Reskin the mesh | Claude | `tools/blender/reskin_animalia_to_horse.py` | `<variant>.fbx` + LODs | report: no zero-weight vertex, max 4 influences, re-import drift 0; renders |
| 3 | Retarget the clips | Claude | `tools/blender/retarget_animalia_to_horse.py` | one FBX per clip | report: every clip order-ok and frame 0 at rest; side-by-side renders |
| 4 | Textures | Claude | `tools/oneoff/convert_tripo_prop_textures.py` | 1K `_d` `_n` `_s` | sizes 1024 |
| 5 | Clip metadata | Claude | `tools/blender/measure_animalia_clips.py` | `<pack>_clip_measure.json` | plausible speeds per gait |
| 6 | Modding Kit import | Mike | Modding Kit | `Assets/.../*_geo.tpac`, textures, materials | no material warning in the Kit log |
| 7 | Point the masters at `horse_skeleton` | Claude, Kit closed | `tools/wire_anim_master_skeletons.ps1` | 16-byte patch per master | census ok = every master |
| 8 | Clip resources | Claude, Kit closed | `tools/gen_animalia_anim_clips.ps1` | one `anim_<master>_anm.tpac` per clip | independent re-read clean, checksums 0 stale |
| 9 | Register the clips | Mike | open the Kit once, close it | RuntimeDataCache entries | `python tools/check_rdc_entries.py --under <folder>`: 0 without an entry |
| 10 | Armory data | Claude, Kit closed | `tools/apply_animalia_armory.py` | Monsters (with `taom_body_length`), action sets, action types, items | wiring tests, `audit_action_set_parity.py`, `validate_moduledata.py`, `validate_xml_schemas.py --live` |
| 11 | Test riders | Claude | a `troops_*_test.xml` (CustomGame only) | spawnable troops | full test suite |
| 12 | The attack | Claude | a small feature on the elephant-like engine | `Main/Features/<Name>/` | tests, then a build and deploy |
| 13 | In game, then size | Mike, then Claude | Custom Battle + console | verdicts, the Monster's `taom_body_length` | logs clean, Mike's eye |

Stages 1 to 5 run while Mike does nothing; stage 6 is the first hand-off; stages 7, 8 and 10 need the Kit and
the game closed, because the Kit scans packages at startup and a Kit save rewrites what it loaded. The three
writers refuse to `-Apply` / `--apply` while Bannerlord or the Kit runs (exit 2).

## Stage by stage

**0. Acquire.** Fab Library, Add To Project, into the UE 5.4 project `E:\LOTRAOMAssets\Troll_Animation_5_4`. If
the launcher lists no project, restart it (engines installed while it runs never reach its list):
[ue-to-bannerlord-asset-pipeline.md](../reference/ue-to-bannerlord-asset-pipeline.md) "Fab acquisition".

**1. Export.** Scope each run to the pack's own folder; packs installed together share `_Bones` / `_Shaders`.
```
$env:TAOM_UE_CONTENT_ROOT='/Game/Animalia/Elk_M'; $env:TAOM_UE_EXPORT_ROOT='E:\LOTRAOMAssets\_export\animalia_elk'
E:\UE_5.4\Engine\Binaries\Win64\UnrealEditor-Cmd.exe <uproject> -run=pythonscript -script=E:\repos\TAOM\tools\oneoff\ue_export_cave_troll.py -EnablePlugins=PythonScriptPlugin -stdout -unattended -nosplash -nullrhi
```
`TAOM_UE_INVENTORY_ONLY=1` first gives the rig, clip list and frame counts without exporting. Exit code 1 with
only GFur load errors is fine (no GFur plugin; fur is not portable). Read `inventory.json`: the skeleton's
bones, root-motion vs in-place clips, LODs as separate assets.

**2. Reskin.** One run per animal; a variant is a set of parts joined per LOD (body + antler set).
```
blender-launcher.exe -b -P tools/blender/reskin_animalia_to_horse.py -- --template C:\Users\mikew\Downloads\horse.fbx ^
  --map tools/blender/animalia_to_horse_map.json --src <export>\meshes\Meshes ^
  --variant animalia_elk_08=Elk_M_Body,Elk_Antlers_08 --material Elk_M_Material=animalia_elk_body ^
  --material Elk_M_Tess_Material=animalia_elk_body ... --out <dir> --preview <dir> [--profile moose]
```
Read the report's `segments` before the renders: any swing over about 30 deg or stretch outside 0.5 to 2 is a
joint that is not a segment (a pivot), a part that must not stretch (a short tail), or a part that needs its
own proportions (a profile). Then look at `preview\*_rest_side.png` against the horse. The mesh goes out on
TaleWorlds' own `horse.fbx` armature, the file shape `elk_001` ships on. A new pack on a different rig needs a
new map JSON (`joints`, `chains`, `weights`, `hoof_split`); the method is unchanged.

**3. Retarget.** Same map and the **same profile** as the mesh, or the clips and the mesh disagree.
```
blender-launcher.exe -b -P tools/blender/retarget_animalia_to_horse.py -- --template <horse.fbx> --map <map> ^
  --engine-skeleton tools/blender/horse_skeleton_engine.json --clips <export>\anims\Animations ^
  --prefix animalia_elk_ --out <dir> --preview-mesh <reskinned fbx> --preview <dir> [--profile moose]
```
The retarget carries each bone's motion through the rotation the mesh was bent by, so a clip's rest frame is the
horse rest. Pilot three clips (a walk, an idle, an attack) with `--only` and read the side-by-side renders
(retarget left, the pack's own mesh right) before the full run.

**4. Textures** at 1K (Mike's rule for these packs): `convert_tripo_prop_textures.py --src <export>\textures\Textures
--match <set prefix> --stem <kit material name> --dst <dir> --max-size 1024`. Name each set exactly as the FBX
materials (the Kit binds by name).

**5. Clip metadata.** `measure_animalia_clips.py` on the pack's root-motion clips: the ground covered per loop at
horse size (gaits), the hoof plants (footstep sounds), a death's fall point. Without the travel the engine cannot
match the legs to the ground speed and the hooves slide.

**6. Kit import (Mike).** Sources under `LOTRLOME_Armory/AssetSources/creature/<folder>/` (meshes, `animations/`,
`textures/`); textures first, then one material per set named exactly as the FBX material, then the meshes on
`horse_skeleton`, then every clip FBX as an animation on `horse_skeleton`; save, close. The Kit log's only mesh
messages should be the seven `*_nub_notused` "ignored joint" lines: the template armature's helpers, weightless.

**7. Masters onto `horse_skeleton`.** The Kit imported all 97 Animalia masters with an EMPTY skeleton reference.
`powershell.exe -File tools\wire_anim_master_skeletons.ps1 -Masters <Armory>\Assets\creature\<folder>\animations`
(census), then `-Apply`. It patches only EMPTY masters with the target rig's bone count (horse 32, human 28; another
rig needs `-BoneNum`); an EMPTY master with another bone count in the same folder is reported WRONG RIG and left
alone. The count is a rig check only between rigs whose counts differ: the horse's 32 is unique in the Armory, but
the chariot and the elephant both have 60.
Rerun the census, and the clip generator's `-Verify` (stage 8), after any clip re-import.

**8. Clip resources.** `powershell.exe -File tools\gen_animalia_anim_clips.ps1` (dry run), then `-Apply`. Each clip
is the vanilla horse clip `as_horse` binds to the same action, cloned (flags, priority, blends, sounds and usages
verbatim), with the measured travel and step points. Gaits get a `_stand` twin; the standing pace gets a
`_movement` clip; turns and jumps are left to the horse (see gotchas). Extend `$PLAN` for a new pack.

**9. Register the clips (Mike):** open the Kit once and close it. A clip package without a RuntimeDataCache entry
does not load in game; masters never get one and play anyway.

**10. Armory data.** `python tools/apply_animalia_armory.py` (dry run), then `--apply`: a Monster per animal
(`base_monster="horse"`, its own `action_set`), its registration in `SubModule.xml`, one action set per animal
(a child of `as_horse` whose overrides copy the vanilla action's attributes and change only the clip, plus the
`_map` twin the campaign map requires (`MobilePartyVisual` looks up `ActionSetCode + "_map"` and throws on a
miss) and the `_town_and_village` twin that only mirrors vanilla), the attack action declared `actt_kick` and bound, the
Horse items with `body_length="100"`, the placeholder `Items.xsd` requires (the size is the Monster's
`taom_body_length`, stage 13). The dry run must print
all five steps; nothing is written unless every step can succeed. Every edit is a byte-faithful insert with a write-once `.bak-*` beside it; record it in a ledger
([lotrlome-animalia-changes.md](../reference/lotrlome-animalia-changes.md)).

**11. Test riders.** Clone a culture's cavalry troop into `troops/troops_<name>_test.xml`, registered in
`Main/_Module/SubModule.xml` for **CustomGame only**, ids starting `taom_test_`, every Horse slot with a harness.
Exempt the riders from the armour and melee ladders (`tools/taom_schema.py` `_ARMOUR_LADDER_EXEMPT`,
`tools/melee_ladders.json`), or the validator groups their file as a kingdom and skews every culture's median;
the recruitment-reachability test already exempts the `taom_test_` prefix. The riders show in every Custom
Battle's picker for their culture, in English in every language: delete the file and its exemptions before a
player release (or set `is_obsolete="true"` to hide them; `taom.spawn_troops` still finds them). Deploy the two repo files by copy
(the deployed `SubModule.xml` is otherwise identical to the repo's) rather than a build that ships every
session's uncommitted code.

**12. The attack.** A small feature on the shared elephant-like engine (the ram's and great elk's tree): config
with the reach at 1.0x (`1.5 m` trigger, `2 m` radius) and `ReachScalesWithBody`, so the shared nodes multiply it
by the animal's live size and no C# constant carries the size, one
attack-service binding and one profile per animal, one mission behavior (`: MissionLogic`) with one tracker per
animal keyed on the Monster id, registration in `Main/IoC.cs` and `Main/SubModule.cs`, and the behavior added to
`BehaviorTreeMissionLogicInheritanceTests`. The action is its own `actt_kick` action, never `act_horse_kick`:
the horse usage set fires that itself. [animalia-elk-moose.md](../features/animalia-elk-moose.md) "The antler attack".

**13. In game, then size.** Custom Battle with cheat mode, console:
```
taom.spawn_troops taom_test_animalia_elk_rider 5 ally
taom.spawn_troops taom_test_animalia_moose_rider 5 enemy
```
Read the TAOM log (`<game>\bin\Win64_Shipping_Client\Logs\taom_debug_*.log`) for `[MonsterSize]` (the sizes
applied), `[MissionDiag] ActionSet '...' used by ... monster=...` and the feature's own `[Animalia]` lines, and the
engine's `rgl_log_*.txt` (one expected "taom_body_length ... not declared" line per sized Monster; the engine
prints it through the same call as the loader's `opening` lines, which land there).
`[MountSpawn]` comes only from the character-preview spawner, never from `taom.spawn_troops`. **Size lives on the
Monster:** `taom_body_length` (100 = as authored, whole numbers 10 to 1000), which TAOM copies into every Horse item
naming the Monster at game init ([monster-size.md](../features/monster-size.md)). Measure in metres before
changing it (withers and total height); a resize is one XML edit and a new Custom Battle, no rebuild.

## Gotchas this pack hit

| Symptom | Cause | Fix |
|---|---|---|
| Hips turned 90 deg, a torn flap on the rump | `horsepelvis` is a pivot 8 cm above `horsespine1`, not a spine segment | leave the pelvis to the global fit; the spine chain starts at `Spine1` |
| A long horse tail on an elk | the horse's tail segments are 5 to 6 times the elk's | anchor-only tail chain, no stretch, all tail weight on `horsetail1` |
| A horse's neck on the moose, no hump | its neck is half a horse's | `--profile moose`: neck moved, not stretched or swung (head pivots 0.54 m off) |
| Collarbone swung 73 deg | the pack's "collarbone" is a midline chest bone | front chains start at the upper leg for that animal |
| LOD0 antlers keep a UE material name | LOD0 uses the `_Tess` material instance | map every material instance, `_Tess` included |
| Every clip bundled a fur shell | the exporter took the first mesh on the skeleton | it now prefers the mesh named after the skeleton |
| `Permission denied` opening a folder as the map | PowerShell: `$M` and `$m` are one variable | names that differ by more than case |
| Masters not tied to a skeleton | the Kit imported them with an empty reference | stage 7 |
| A looping turn would double the turn | the retarget folds a turn clip's yaw into the pose, and the engine turns the agent too | leave turns to the horse, or re-export the turns with the yaw dropped |
| A jump would fly twice as high | the pack's jumps lift the pelvis about 2 m, and the engine flies a mount's jump | leave jumps to the horse until they are cut into start / loop / end with the lift removed |
| A slow-looking trot | the pack's trot covers 6.3 m per loop, so at horse trot speeds the legs play slower | correct (no slide); tune Duration or LoopDisplacement if it reads wrong |
| The validator reported a new "kingdom" | a `troops_*.xml` file is grouped by name | exempt the test riders (stage 11) |

## Adapting it to the next pack

What is Animalia-specific lives mainly in four tables: the bone map
(`tools/blender/animalia_to_horse_map.json`: `joints`, `chains`, `weights`, `hoof_split`, `profiles`), the clip
plan (`$PLAN` in `tools/gen_animalia_anim_clips.ps1`), the action overrides (`OVERRIDES` in
`tools/apply_animalia_armory.py`), and the measured stems (`--gaits` / `--deaths` of `measure_animalia_clips.py`).
Hard-coded beside them, and to change for a new pack: in `tools/apply_animalia_armory.py` the Monster file and its
path, `SUBMODULE_BLOCK`, `ITEMS`, `ANTLER`, the id tuples, the clip folder and the `as_animalia_` / `anim_animalia_`
prefixes; in `tools/gen_animalia_anim_clips.ps1` the `-Masters` default, the two measure files, the
`'elk', 'moose'` loops, the `animalia_${animal}_` prefix and the `$meas` keys; and the two animals built into
`Main/Features/Animalia/`. The
PowerShell stages need Windows PowerShell 5.1, TpacTool's `bin` and `TolerantTpacLoader.cs` (both outside the repo,
passed as `-TpacBin` / `-Loader`); the Blender stages detach, so their only completion signal is the report's
`.DONE` twin. For a pack on another rig family, write its map first and let the reskin report say whether the fit holds.
