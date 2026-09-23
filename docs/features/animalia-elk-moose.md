# Animalia Elk and Moose (Fab packs on horse_skeleton)

## Overview

Two purchased Fab packs, "Animalia - Elk (male)" and "Animalia - Moose (male)", brought into Bannerlord as
**horse-skeleton reskins with their own animation**. The meshes are bent onto the vanilla `horse_skeleton`
keeping the pack's hand-made weights, and the pack's clips are retargeted onto the same skeleton, so each
animal moves with its own gaits, idles, attacks, hit reactions and deaths instead of the horse's.

State on 2026-09-23: **set up for an in-game animation test.** The two meshes, 97 masters (wired to
`horse_skeleton`) and 54 clips are in the Armory; the Monsters, action sets and Horse items are written
([ledger](../reference/lotrlome-animalia-changes.md)); two Custom-Battle-only test riders spawn them. Not yet: the
antler attack (action type + C#), real riders (the moose goes to Thranduil and Mirkwood lords after #636), size.
Issue #646.

### Testing it in game

1. Open the Modding Kit once and close it (the two `_movement` clips need their RuntimeDataCache entries).
2. Launch with TAOM, cheat mode on (`engine_config.txt`), start any Custom Battle, open the console:
   ```
   taom.spawn_troops taom_test_animalia_elk_rider 5 ally
   taom.spawn_troops taom_test_animalia_moose_rider 5 enemy
   ```
   `taom.print_agent_info <name>` confirms the monster and action set (`as_animalia_elk` / `as_animalia_moose`).
3. Watch: standing still and idling, walk / trot / canter / gallop (slower cadence than a horse is expected, sliding
   hooves are not), backing up, the elk's rear and hit reactions, kicks, deaths and the lying hold. Turns and jumps
   are the horse's on purpose.

## Why This Exists

- **Vanilla behavior:** a mount on `horse_skeleton` plays the horse's clips. The horse rig has no attack
  animation at all (horses damage by charge collision), see [war-ram.md](war-ram.md).
- **TAOM requirement:** Mirkwood's mounts should look and move like an elk and a moose. The existing great
  elk (#636, [elk.md](elk.md)) is `elk_001` on the horse's clips with the ram's head-butt as its antler
  charge; it has no motion of its own.
- **Without this feature:** buying a creature pack gets you art on a rig the engine does not know. Giving it
  its own skeleton means the full custom-creature setup (the elephant and spider path, Phases 1 to 5 of
  [creature-mount-authoring.md](../ai-includes/creature-mount-authoring.md)).

### Decisions (Mike, 2026-09-23)

| Question | Decision |
|---|---|
| Skeleton | Both on the vanilla `horse_skeleton` (a reskin), with the pack's clips retargeted onto it |
| The Animalia elk vs `elk_001` | A **separate second elk**: its own item and Monster; `elk_001` and #636 stay as they are |
| Who rides the moose | Woodland Realm (Mirkwood): **Thranduil and some Mirkwood lords**. #636 currently puts Thranduil on the elk, so that change lands only after #636 is committed |
| Clip scope | Every vanilla horse action an Animalia clip fits, plus an antler attack of their own; no ambient behaviours played by our code |
| Moose proportions | **Keep the moose neck** (see "The moose keeps its neck") |
| Textures | **1K** (1024) for every map |
| Variants in use | **`animalia_elk_08` and `animalia_moose_big` only** ("those two are fine for what we are doing"). The other four were built and are kept in `E:\LOTRAOMAssets\_reskin_out\`, not in the Armory |
| Licence bookkeeping | Bought on Fab; no creator or tier research. Register rows say purchased, cleared |

## Architecture

### Design Challenge

The packs ship their own quadruped rig (`Rig*` bones: 41 tracks on the elk, 44 on the moose, which adds a
dewlap and two shoulder blades). `horse_skeleton` has 32 bones with different joint positions and posture.
A weight transfer from a donor (the troll's route, `reskin_to_human_skeleton.py`) needs the target mesh to
sit on the donor's joints already; these do not. And a clip carries rotations for the pack's joints, not the
horse's.

Measured before building anything, against the engine rest (2026-09-23): after one uniform scale and a
180 deg turn, the mapped leg and spine joints sit 2 to 11 cm from the horse's. The large gaps are the head,
neck and tail (0.3 to 0.7 m), and those are posture: the horse's rest holds its head high and its tail low.
So the mesh can be **bent** into the horse's rest pose by its own skinning rather than re-weighted.

### Solution Approach

```
Fab pack (UE 5.4 project)
   |  tools/oneoff/ue_export_cave_troll.py            FBX + TGA + inventory.json
   v
E:\LOTRAOMAssets\_export\animalia_{elk,moose}\
   |  tools/blender/reskin_animalia_to_horse.py       mesh bent onto horse joints, pack weights kept
   |  tools/blender/retarget_animalia_to_horse.py     clips onto the engine rig, same fit
   |  tools/oneoff/convert_tripo_prop_textures.py     1K d/n/s triples
   v            (bone map + per-animal profiles: tools/blender/animalia_to_horse_map.json)
LOTRLOME_Armory\AssetSources\creature\elk\  animalia_elk_08.fbx, animalia_moose_big.fbx,
                                           animations\{elk,moose}\, textures\
   |  Modding Kit import (Mike)  ->  Assets\creature\elk\ (same layout)
   v
Monster (base_monster="horse") + action set (child of as_horse) + items + troops   [owed]
```

**The fit (reskin).** For each pack bone the tool builds one transform: a global uniform scale, turn and
offset (least squares over the mapped joints), then per mapped bone a swing of the segment onto the horse
segment and a stretch along it, so the joint and the segment end both land exactly on horse joints. Bones
outside the chains (jaw, ears, chest, the last bone of a chain) move rigidly with their nearest fitted
ancestor. The mesh is moved by linear blend skinning with the pack's weights; then the weight groups are
renamed and merged onto horse bones, the ankle weight past the hoof joint moves to the hoof bone, and the
result is capped at four influences and normalised.

**The clips (retarget).** The mesh was bent into the horse's rest, so a clip's rest frame must stay the
horse's rest. Each horse bone takes its pack bone's world-space motion carried through the same fit
rotation the mesh was bent by:

```
R_t(f) = C . D_s(f) . C^-1 . R_t_rest      D_s = pack bone s away from its bind pose
                                            C   = (template to engine turn) . (fit rotation of s)
```

This is the opposite of the troll's retarget, which poses the target into the source's stance at rest (right
for a human body keeping human proportions, wrong for a mesh already bent onto the target). Root motion (the
UE root is the armature object) is dropped; turn clips fold the yaw into the pose so they turn in place.
Frame 0 is the rest frame, the Kit's 180 deg root yaw is baked in, and the export goes through the ram's
file-order rig so the engine reads each bone's track in its own slot
([bannerlord-skeleton-authoring.md](../reference/bannerlord-skeleton-authoring.md), fact 4).

**The export armature.** Meshes go out on the armature of TaleWorlds' own `horse.fbx` mesh export
(`horse_skeleton_notused`, 39 bones with 7 `_nub_notused`, `horseneck1` under `horsetail3`, facing -Y),
the same armature `elk_001` ships on, so the Kit gets a file shape it has already accepted. Its bone heads
match the engine rest within 2 cm on average (max 8.6 cm) under a 180 deg turn.

## The packs

Exported from `E:\LOTRAOMAssets\Troll_Animation_5_4` (UE 5.4.4) on 2026-09-23 with 0 failures.

| | Elk | Moose |
|---|---|---|
| Skeleton | `Elk_M_Skeleton`, 41 tracks (`RigRoot` becomes the armature object, 40 bones) | `Moose_M_Skeleton`, 44 tracks (43 bones) |
| Clips | 173 at 30 fps: 99 root-motion (23 additive `Add_*`) and 74 in-place `-IP` copies | 97: 57 root-motion and 40 `-IP` |
| Meshes | body, four antler sets (`04`, `06`, `08`, `Spike`), LOD1 to LOD4 as separate assets | body, `AntlersBig`, `AntlersSmall`, LODs |
| Textures | body and antler sets (BaseColor, Normal, Roughness, AO), 4K | one set for body and antlers (no AO), 4K |
| Not used | GFur fur (needs the GFur plugin; nothing in Bannerlord renders it), blend spaces, the demo map | same |

The export commandlet exits 1 on the GFur load errors alone; `export_report.json` is the record.

## Reskin results

`tools/blender/reskin_animalia_to_horse.py`, report `reskin_report.json` beside each output.

| Variant | LOD0 vertices | Materials | In use |
|---|---|---|---|
| `animalia_elk_08` | 8,377 | `animalia_elk_body`, `animalia_elk_antlers` | **yes** |
| `animalia_elk_06` | 7,837 | same | no |
| `animalia_elk_04` | 7,328 | same | no |
| `animalia_elk_spike` | 7,245 | same | no |
| `animalia_moose_big` | 6,903 | `animalia_moose` (one material for body and antlers) | **yes** |
| `animalia_moose_small` | 6,227 | same | no |

Each has `_lod1` to `_lod4` (the `elk_001` naming). Every vertex is weighted, none has more than 4
influences, 30 horse bones carry weight, and the re-imported FBX has the template's 39 bones at the same
heads (drift 0.0).

**Bend QA** (worst edge stretch, deformed over rest, under the same six bends applied to the template
armature; TaleWorlds' own `horse_brown` is the bar):

| Bend | Elk | Moose | TaleWorlds horse |
|---|---|---|---|
| foreleg lift | 3.54 | 3.95 | 6.10 |
| hind flex | 3.15 | 2.99 | 10.68 |
| neck (sagittal) | 1.39 | 2.53 | 2.53 |
| neck turn | 1.74 | 2.07 | 2.13 |
| spine flex | 3.03 | 4.27 | 4.39 |
| tail lift | 8.07 | 11.00 | 9.53 |

The moose's tail is the one reading above the horse's; it is a short tail on a long horse tail bone.

### What the first runs got wrong (all fixed in the map)

- **The horse's pelvis joint is a pivot, not a spine segment.** `horsepelvis` sits 8 cm straight above
  `horsespine1`. Fitting the pack's pelvis along its spine turned the hips 90 deg and tore the rump. The
  pelvis now moves with the global fit only; the spine chain starts at `Spine1`.
- **The elk's tail must not stretch.** The horse's tail segments are 5 to 6 times the elk's; a full fit
  grew a horse's tail. The tail is anchored at its root and swung, never stretched, and all three tail
  groups weight `horsetail1`, so it wags as one piece.
- **The pack's "collarbone" is a short midline chest bone, not a shoulder blade.** On the moose, fitting it
  to `horselleg1` swung it 73 deg and stretched it 2.2 times. The moose's front chains start at the upper
  leg. The elk's (39 deg) looked right in every render and was left as it is.

### The moose keeps its neck

The moose's neck is 0.44 m from its first neck joint to its head (at horse size), carried almost level; the
horse's is 0.90 m at about 45 deg. A full fit gave the moose a horse's neck and no shoulder hump. With
`--profile moose` the neck is moved into place without stretch or swing, which keeps the silhouette, and the
bends stay at the horse's quality. **The cost:** `horse_head` sits about 0.54 m from the moose's head, so a
head rotation pivots off the head and the head swings slightly more than it tilts. Check it in the Kit on a
vanilla gallop; the alternative, a moose skeleton of its own, was declined.

## Clips

`tools/blender/retarget_animalia_to_horse.py`, report `retarget_report.json`. **64 elk and 33 moose clips**,
all passing the re-import check (bone order equals the skeleton's file order, frame 0 at rest to 0.0 deg).
Names are `animalia_elk_<stem>` / `animalia_moose_<stem>`, lower-cased (`Loco_Walk` becomes
`animalia_elk_loco_walk`); the take name equals the file stem, which is the Kit master name.

Excluded by default: the additive `Add_*` layers and the sitting, sleeping and swimming set, which no horse
action can play. The `-IP` in-place copies are not needed: root motion is dropped anyway.

The previews (`preview\` beside each output) render the retarget beside the pack's own mesh at the same
frames. Walk, trot, idles, the rearing hoof strike, the antler thrust and the moose's head charge all match
their source.

### Planned bindings (set at binding time, after the Kit import)

| Horse action | Elk | Moose |
|---|---|---|
| idles and stands (`act_horse_idle_1..4`, `act_horse_stand_1..4`) | `stand_01..03`, `alert_looking_l/r` | `stand_00..02` |
| riderless idles (`act_horse_riderless_idle_1..4`) | `stand_eating_01..03`, `stand_drinking_01`, `vocalization_bugling` | `eating_01..03`, `drinking_01` |
| forward walk / trot | `loco_walk`, `loco_trot` | `loco_walk`, `loco_trot` |
| canter / gallop | `loco_run`, `loco_sprint` | `loco_run`, `loco_sprint` |
| `act_horse_backward_walk` | `loco_walkback` | `loco_walkback` |
| `act_horse_turn_left/right` | vanilla (the retarget baked each 90 deg turn into the pose, and the engine turns the agent itself, so a looping turn clip would double the turn) | vanilla |
| `act_horse_rear` | `attack_front_high` (rearing hoof strike) | none; keeps the horse's |
| `act_horse_kick` | `attack_hind` | `attack_legs_01` |
| `act_horse_strike_front/back` (hit reactions) | `hit_chest*`, `hit_head*` / `hit_pelvis*` | none (the pack's hits are additive); keeps the horse's |
| `act_horse_fall_left/right` (+ `_continue`) | `death_stand_l/r` (+ `_pose`) | `death_l/r` (+ `_pose`) |
| jumps | vanilla for now: the engine splits a jump into start / loop / end actions, and the pack's `jump_*` are single clips that lift the pelvis about 2 m; they need re-cutting and a re-import | same |
| antler attack (own action, `actt_kick` like `act_war_ram_butt`) | `attack_front_low` (a hoof strike rolling into a head-down antler lunge, 2.5 s; `defense_antlers_01/02` are 5 to 7 s displays) | `attack_head_01` |

### The clip resources (`_anm.tpac`, 2026-09-23)

`tools/gen_animalia_anim_clips.ps1` wrote **52 clips** (29 elk, 23 moose) beside their masters, each named
`anim_<master>` and a CLONE of the vanilla horse clip `as_horse` binds to the same action, read from
`Native/AssetPackages/animation_clips.tpac`. What vanilla does, per type, as read on the day:

| Type (vanilla clip) | Recipe |
|---|---|
| gaits (`horse_walkfast`, `anim_horse_trot_2`, `horse_canterfast`, `horse_forward_gallop_right_foot`, `horse_walkbackfast`) | priority 0, `make_walk_sound`, a `QuadMovementUsage` (LoopDisplacement per loop: walk 1.52 m in 0.8 s, trot 2.9, canter 4.0, gallop 8.5; PaceSwitchLimits walk 0.1 to 1.9, trot 1.6 to 4.6, canter 4.3 to 6.8, gallop 8 to 14.1, backward -1.25 to -0.1), StepPoints at the hoof plants |
| gait `_stand` twins (`gait_walkfast` ...) | the same master range, no usage |
| idles (`horse_idle_1`) / stands (`horse_stand_1`) | `enforce_lowerbody`, blends 0.9/0.3 or 0.8/0.8; the stands carry the `horse_eating` sound at 0.2 and 0.5 |
| `horse_rear` | priority 74, `lock_movement` `enforce_lowerbody` `update_bounding_volume` `ignore_slope`, rear foley |
| `horse_kick` | priority 34, blends 0.2/0.4, `enforce_lowerbody` `enforce_all`, `horse_kick_params`, the kick swing sound |
| hits (`horse_hit_from_front/back`) | priority 2, blends 0.2/0.4, `enforce_lowerbody` |
| deaths (`horse_death_left/right_side` + `_continue`) | priority 80, the fall flag set, `ContinueWithAction` to the `_continue` action, which holds the lying pose (`keep`) |

Per clip only these change: name, fresh GUID, the master, Source1 = 1, Source2 = master Duration - 1 (frame 0
is the rest frame; the war ram's proven clip is 1..105 of 106), Duration = frames / 30, and the measured
values from `tools/blender/measure_animalia_clips.py` (`tools/blender/animalia_{elk,moose}_clip_measure.json`):

| | Elk (per loop, speed) | Moose |
|---|---|---|
| walk | 2.02 m, 1.68 m/s | 2.11 m, 1.54 m/s |
| trot | 6.32 m, 7.90 m/s | 6.32 m, 7.89 m/s |
| canter (`loco_run`) | 7.40 m, 13.9 m/s | 16.84 m (a two-stride loop), 14.9 m/s |
| gallop (`loco_sprint`) | 8.75 m, 18.7 m/s | 10.10 m, 21.7 m/s |
| backward | 1.75 m, 0.58 m/s | 1.68 m, 0.67 m/s |

Gait StepPoints are the measured hoof plants (four sorted for walk, trot and backward, the first for canter
and gallop, as vanilla); a death's body-fall point (vanilla 0.35) is the measured fall, 0.52 for both. The
antler clips are the war ram's head-butt recipe (the kick's flags and blends without its combat parameter,
sound or step points). Idle clips drop the eating sound; eating and drinking keep it. **Watch in game:** the
pack's trot covers 6.3 m per loop, so at the horse's trot speeds the legs play at a quarter to three quarters
of their authored rate (no hoof slide, but a slow trot is possible); tune Duration or LoopDisplacement there.

Proof: an independent re-read of all 52 against the 97 masters (each names a `horse_skeleton` master, range
1..Duration - 1, gaits carry their travel, no usage elsewhere, kicks keep `horse_kick_params`, the antler
clips carry none, idles carry no sound): 0 problems; item checksums 0 stale in both folders.

Before binding, read "The price of a reskin" in
[creature-mount-authoring.md](../ai-includes/creature-mount-authoring.md): the inherited `horse` usage set
fires `rear` and `kick` itself, and a clip on a mount rig needs the vanilla horse recipe (priority,
`enforce_lowerbody`) or it never shows in battle.

## Textures

1K `_d` / `_n` / `_s` triples, made by `tools/oneoff/convert_tripo_prop_textures.py --match <set>
--max-size 1024`: `_d` = albedo times (1 - metallic), `_s` = R metallic, G gloss (255 - roughness), B AO,
the packing verified against the shipped kits. The packs have no metallic map (0); the elk antlers have no
roughness map (0.7); the moose has no AO (1.0). Sets: `animalia_elk_body`, `animalia_elk_antlers`,
`animalia_moose`, matching the FBX material names so the Kit binds them by name. Normal maps are copied as
UE exported them; whether Bannerlord wants the green channel flipped is settled by the Kit smoke (rerun with
`--flip-green` if the relief looks inverted).

## Key Files

| File | Purpose |
|------|---------|
| `tools/oneoff/ue_export_cave_troll.py` | UE export of a Fab creature pack (env-var roots); prefers the mesh named after the skeleton as each clip's preview mesh |
| `tools/blender/reskin_animalia_to_horse.py` | Mesh fit onto `horse_skeleton`, weights kept, LODs, bend QA, export on `horse.fbx`'s armature |
| `tools/blender/retarget_animalia_to_horse.py` | Clip retarget through the same fit, file-order export, re-import check, side-by-side previews |
| `tools/blender/animalia_to_horse_map.json` | Joints, chains, weight merges, hoof split, and `profiles` (the moose) |
| `tools/oneoff/convert_tripo_prop_textures.py` | 1K d/n/s triples (`--match` picks one set out of a folder) |
| `tools/wire_anim_master_skeletons.ps1` | Census of every master's skeleton reference; `-Apply` points EMPTY ones at `horse_skeleton` |
| `tools/blender/measure_animalia_clips.py` + `tools/blender/animalia_{elk,moose}_clip_measure.json` | Travel per loop, hoof plants, fall fraction, measured on the pack's root-motion clips |
| `tools/gen_animalia_anim_clips.ps1` | The 54 `_anm.tpac` clips (52 + two `_movement` standing clips), cloned from vanilla horse clips, with the measured values |
| `tools/apply_animalia_armory.py` | The Armory edits: Monsters file + registration, `as_animalia_*` action sets and twins, the two Horse items (dry run / `--apply`) |
| `docs/reference/lotrlome-animalia-changes.md` | Ledger of every live Armory edit, backups, redo steps |
| `Main/_Module/ModuleData/troops/troops_animalia_test.xml` | The two test riders (CustomGame only), registered in `Main/_Module/SubModule.xml`; exempt from the armour and melee ladders and from the recruitment-reachability test, all marked for removal with the file |
| `TAOM.Tests/Features/Animalia/AnimaliaMountWiringTests.cs` | 5 tests: riders pair mount and saddle; items name their Monster and mesh; Monsters are horses on their own set and registered; sets are children of `as_horse` with `_map` / `_town_and_village` twins; every bound clip exists and every bound type is an `as_horse` action |
| `C:\Users\mikew\Downloads\horse.fbx` | TaleWorlds' horse mesh export: the template armature (outside the repo) |
| `LOTRLOME_Armory\AssetSources\creature\elk\` | Mike's layout: `animalia_elk_08.fbx`, `animalia_moose_big.fbx`, `animations\elk\` (64), `animations\moose\` (33), `textures\` (9); beside `elk_001.fbx` |
| `LOTRLOME_Armory\Assets\creature\elk\` | What the Kit made of them: `animalia_elk_08_geo.tpac`, `animalia_moose_big_geo.tpac`, `textures\` (9 `_tex`, 3 `_mtl`), `animations\elk\`, `animations\moose\` |
| `E:\LOTRAOMAssets\_reskin_out\`, `_retarget_out\` | Reports and previews of the runs of record, and the four variants not in use |
| `E:\LOTRAOMAssets\animalia_to_import\` | The first staging copy (all six variants) |

## How to re-run or add a variant

1. Export the pack (`TAOM_UE_CONTENT_ROOT=/Game/Animalia/<Pack>`, `TAOM_UE_EXPORT_ROOT=<staging>`); the
   commandlet line is in the exporter's docstring.
2. Reskin: `reskin_animalia_to_horse.py -- --template <horse.fbx> --map tools/blender/animalia_to_horse_map.json
   --src <export>\meshes\Meshes --variant <name>=<BodyPart>,<AntlerPart> --material <UE material>=<kit name>
   --out <dir> --preview <dir>` (add `--profile moose` for the moose). Map every UE material instance of the
   parts, the `_Tess` ones included: LOD0 antlers use `Elk_Antlers_Tess_Material`.
3. Retarget: `retarget_animalia_to_horse.py -- --template <horse.fbx> --map <map> --engine-skeleton
   tools/blender/horse_skeleton_engine.json --clips <export>\anims\Animations --prefix animalia_<animal>_
   --out <dir> [--profile moose] [--only <stems>] --preview-mesh <reskinned fbx> --preview <dir>`. Use the same
   profile as the mesh, or the clips and the mesh disagree.
4. Textures: `convert_tripo_prop_textures.py --src <export>\textures\Textures --match <prefix> --stem <set>
   --dst <dir> --max-size 1024`.
5. Read each report before anything else: `joint_residual_after_m` lists only the joints left off on purpose
   (pelvis, unstretched tail, the moose neck), the retarget summary must list no failures.

The Blender launcher detaches: completion is the report's `.DONE` twin. In PowerShell, keep the map path and
the output folder in variables whose names differ by more than case (`$M` and `$m` are one variable).

## Owed

1. **Kit import (Mike):** DONE 10:28 to 10:32 for the 9 textures, the three materials and the two meshes in
   use (the Kit log shows no material warning; its only messages are the seven `*_nub_notused` joints it
   ignores by design, the template armature's helpers, which carry no weight). **Clips: all 97 imported**
   (`Assets\creature\elk\animations\{elk,moose}\*_geo.tpac`), every one with its animation and no stray
   skeleton, but **the Kit left every master's Skeleton reference EMPTY**. `tools/wire_anim_master_skeletons.ps1
   -Apply` (Kit closed) patched all 97 to `horse_skeleton` (`1163bb17-777d-49b1-b083-aad79dc544fd`), re-read
   each, refreshed the item checksums; the census now reads ok=97, 0 stale checksums, 97 `.bak-preskel`
   backups beside them. OWED: open the Kit once so it re-reads the patched masters, a vanilla gallop on each
   mesh (the rest-pose and normal-map check), a module save. Re-run the census after any re-import of a clip:
   a re-import can bring the empty reference back.
2. **Clip resources:** DONE (52, above). OWED: open the Kit once so it writes their RuntimeDataCache entries
   (a clip needs one; a master does not), save, close. Jumps later: in `jump_run_high` the pelvis climbs to
   3.36 m, about 2 m above its standing 1.37 m, and the engine flies a mount's jump itself, so the jump clips
   need the vertical travel removed and a cut into start / loop / end before they can replace vanilla's.
3. **Game side:** Monsters, action sets and items DONE for the test (both on `taom_elk_saddle_a`; the moose's
   hump may want its own saddle). OWED: the antler attack (an `action_types.xml` entry typed `actt_kick` plus
   the C# that fires it, after #636 lands, since it owns the elk behaviour code), real riders replacing the test
   file (Thranduil and the lords on the moose after #636), size (`body_length`), shop listing, `/localize` for the
   two item names.
4. In-game ladder per [creature-mount-authoring.md](../ai-includes/creature-mount-authoring.md).

## Known gaps

- The moose's head pivots about 0.54 m off `horse_head` (accepted trade, above).
- Hoof bones are not driven by the clips (the pack has no hoof joint): they hold their rest relative to the
  ankle.
- The curving gaits (`loco_*_l/_r`) lose their root turn; the engine steers, so they play as straight gaits.
- A slight pinch at the elk's front elbow in extreme poses (the rearing strike).
- Not yet seen in the Kit or in game: all QA so far is in Blender.

## Changelog

- 2026-09-23: packs exported; four elk and two moose variants reskinned onto `horse_skeleton` with the
  pack's weights; 64 + 33 clips retargeted and checked; 1K textures; staged in the Armory's sources. Tools
  added: `reskin_animalia_to_horse.py`, `retarget_animalia_to_horse.py`, `animalia_to_horse_map.json`;
  `convert_tripo_prop_textures.py` gained `--match` and stopped missing `_ao` maps. Mike kept two variants,
  `animalia_elk_08` and `animalia_moose_big`, moved the sources into `AssetSources\creature\elk\` and imported
  textures, materials and both meshes in the Kit, then all 97 clips; the Kit left every master's skeleton
  reference empty, and `tools/wire_anim_master_skeletons.ps1` pointed all 97 at `horse_skeleton`. Then
  `tools/gen_animalia_anim_clips.ps1` wrote the 52 clip resources from vanilla horse templates with measured
  travel, hoof plants and fall points; turns and jumps stay vanilla.

## GitHub Issue

- **Issue:** #646 [Animalia elk and moose: horse_skeleton reskin, retargeted clips, Mirkwood moose mount](https://github.com/haterade22/TAOM/issues/646)
- **Status:** Open
