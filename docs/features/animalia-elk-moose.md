# Animalia Elk and Moose (Fab packs on horse_skeleton)

## Overview

Two purchased Fab packs, "Animalia - Elk (male)" and "Animalia - Moose (male)", brought into Bannerlord as
**horse-skeleton reskins with their own animation**. The meshes are bent onto the vanilla `horse_skeleton`
keeping the pack's hand-made weights, and the pack's clips are retargeted onto the same skeleton, so each
animal moves with its own gaits, idles, attacks, hit reactions and deaths instead of the horse's.

State on 2026-09-23 night: **gaits and idles seen in game; nothing built after 13:07 has been.** The two meshes,
97 masters (wired to `horse_skeleton`) and 54 clips are in the Armory; the Monsters, action sets, antler actions and
Horse items are written ([ledger](../reference/lotrlome-animalia-changes.md)); `Main/Features/Animalia/` fires each
animal's antler attack. **Riders (Mike, evening):** the moose carries Thranduil and the Mirkwood lords, the Animalia
elk the lower cavalry and the `elk_rider` career start (see "Who rides them"). **The installed `TAOM.dll` (14:45) is
older than this tree:** it has the antler attack with the old baked reach and no Monster size, while the live Armory
already holds the sizes on the Monsters and the items' placeholder 100, so until a deploy every animal builds at 1.0x
with the old reach. **Run the in-game checklist only after a deploy.** And until this tree is committed, HEAD's own
`ElkConfigTests` and `AnimaliaMountWiringTests` fail against the live Armory (they predate the move): the code, its
tests and the ledgers go in one commit. Not yet: the attack seen in game, the jumps.
Issue #646. The whole procedure, for the next pack: [quadruped-pack-to-horse-skeleton-workflow.md](../ai-includes/quadruped-pack-to-horse-skeleton-workflow.md).

**In game, 2026-09-23 (Custom Battle, both sessions from 12:48 and 13:07):** both animals spawn on their own
sets (`[MissionDiag]`: `as_animalia_elk`, `as_animalia_moose`) with no engine or TAOM log errors. Mike: "The
running and walking and idle animations look great." Still to judge: rear, kick, hit reactions, deaths, the
moose's hooves at `body_length` 150, the saddle on these bodies. The head-lowering attack seen in that battle was
the #636 great elk's; these two had no attack wired then.

### Testing it in game

1. The clips' RuntimeDataCache entries exist (a Kit load on 2026-09-23 wrote them;
   `python tools/check_rdc_entries.py --module <Armory> --under creature/elk` reads 0 missing). After any clip
   re-import, open the Kit once and close it again.
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

- **Vanilla behavior:** a mount on `horse_skeleton` plays the horse's clips. The horse rig's only attack is the
  rear kick, `act_horse_kick` (`actt_kick`; its clip carries `horse_kick_params`, the engine's hit detection), which
  the horse usage set fires itself at whatever stands behind the mount. There is no forward attack; otherwise
  horses damage by charge collision (see [war-ram.md](war-ram.md)).
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
| Who rides them (evening) | **Moose: Thranduil and the Mirkwood lords. Animalia elk: the lower Mirkwood cavalry and the `elk_rider` career start. The great elk keeps the top cavalry troop** (see "Who rides them") |
| Clip scope | Every vanilla horse action an Animalia clip fits, plus an antler attack of their own; no ambient behaviours played by our code |
| Moose proportions | **Keep the moose neck** (see "The moose keeps its neck") |
| Textures | **1K** (1024) for every map |
| Size (after the first battle, 2026-09-23) | Moose `body_length` 150 ("way bigger"); the Animalia elk stays 100. Mike also cut the #636 great elk from 200 to 120, then 110 after the next battle (sizes in metres: "Sizes in game") |
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
Monster (base_monster="horse") + action set (child of as_horse) + items + test riders + antler attack (C#)
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
pack's trot covers 6.3 m per loop, so at the horse's trot speeds the legs play at about a fifth to three fifths
of their authored rate (no hoof slide, but a slow trot is possible); tune Duration or LoopDisplacement there.

Proof: an independent re-read of all 52 against the 97 masters (each names a `horse_skeleton` master, range
1..Duration - 1, gaits carry their travel, no usage elsewhere, kicks keep `horse_kick_params`, the antler
clips carry none, idles carry no sound): 0 problems; item checksums 0 stale in both folders.

Before binding, read "The price of a reskin" in
[creature-mount-authoring.md](../ai-includes/creature-mount-authoring.md): the inherited `horse` usage set
fires `rear` and `kick` itself, and a clip on a mount rig needs the vanilla horse recipe (priority,
`enforce_lowerbody`) or it never shows in battle.

## The antler attack (`Main/Features/Animalia/`, 2026-09-23)

Each animal attacks with its own clip, fired by TAOM's behavior tree the way the great elk's charge is (#636): a
live enemy in front within the trigger range, the attack off its 10 s cooldown, then the clip plays and one blow
lands on the enemy the animal faces most squarely (the ram's #618 single-target rule), under any rider, the
player included, scaled by the rider's career charge bonus. Built on the shared elephant-like engine rather than
as two clones of the great elk's seven files:

| File | Role |
|---|---|
| `AnimaliaConfig.cs` | Both animals' tuning. The reach is the war ram's at 1.0x (1.5 m trigger, 2 m radius); `ReachScalesWithBody` makes the shared nodes multiply it by each animal's live size, which lives on its Monster ([monster-size.md](monster-size.md)) |
| `AnimaliaElkAttackService.cs`, `AnimaliaMooseAttackService.cs` (+ `IAnimaliaElkAttackService.cs`, `IAnimaliaMooseAttackService.cs`) | One binding of `ElephantLikeAttackService` per animal (monster gate, facing gate, damage), each behind its own marker interface so IoC keys them apart |
| `AnimaliaCombat.cs` | One static profile per animal (ranges, blow magnitude, the attack action in all four slots, single target, Blunt, the rider multiplier) |
| `AnimaliaBehaviorTree.cs` | One tree class; the profile is passed in, so both animals share it |
| `AnimaliaMissionBehavior.cs` | `: MissionLogic`; one `CreatureTreeTracker` and one registered tree name per animal, keyed on the Monster id; a start-up drift guard per animal (the action resolves, its set exists, the clip is bound) |
| `AnimaliaIoC.cs` | Registers both services; one line in `Main/IoC.cs`, one `AddTaomBehavior` line in `Main/SubModule.cs` |

| | Animalia elk | Animalia moose |
|---|---|---|
| Action (`action_types.xml`, `actt_kick`) | `act_animalia_elk_antler` | `act_animalia_moose_antler` |
| Clip | `anim_animalia_elk_attack_front_low` (2.5 s) | `anim_animalia_moose_attack_head_01` (1.57 s) |
| Reach (trigger / radius), at today's size | 1.5 m / 2 m (size 100) | 2.25 m / 3 m (size 150) |
| Damage | one 60 Blunt blow (the great elk's) | one 70 Blunt blow |
| Knockback | 35 (the ram's) | 45 |

Their own actions, never `act_horse_kick`: the inherited horse usage set fires that itself as its `kick_action`,
so the kick slot keeps its own clip (`attack_hind`, `attack_legs_01`) and the engine keeps firing it. Tests:
`AnimaliaConfigTests` (literal id pins, the 1.0x reach and its flag, cooldown past each clip, damage / knockback /
Blunt / single target, distinct actions), `AnimaliaWiringTests` (the IoC line, the `AddTaomBehavior` line, both
profiles passing `reachScalesWithBody`, both services resolving),
`AnimaliaAttackServiceTests` (each service answers only to its Monster, not the great elk's; each animal's one blow,
a quarter on a shield block, rounded: 15 and 18; facing and already-attacking gates),
`AnimaliaMountWiringTests.AntlerActions_AreKickTyped_AndBoundToTheirAttackClip` and
`AnimaliaMonsters_DeclareTheirSize_AndTheirItemsHoldTheSchemaPlaceholder`, and the behavior's entry in
`BehaviorTreeMissionLogicInheritanceTests`.

## Sizes in game (measured 2026-09-23)

`body_length / 100` times the mesh, the mesh measured in Blender at 1.0 (withers = the top of the back over
`horsespine3`):

| Mount | `body_length` | Withers | Total height (antlers) | Length |
|---|---|---|---|---|
| Vanilla horse | 100 | 1.57 m | 2.18 m | 2.69 m |
| Animalia elk | 100 | 1.68 m | 2.99 m | 2.57 m |
| Animalia moose | 150 | 2.74 m | 3.66 m | 3.85 m |
| Great elk (#636, `elk_001`) | 110 | 1.72 m | 3.38 m | 2.87 m |

The moose went 100 to 150 after the first battle ("way bigger"); the great elk 200 to 120 to 110 ("still a bit
too big"). That evening Mike moved the size onto the Monster ("The monster xml should control the size of the
animal"): `body_length` in the table is now each Monster's `taom_body_length`, copied into its Horse item at game
init, and the reach follows it with no C# constant ([monster-size.md](monster-size.md)). Whether the engine scales a
gait clip's `LoopDisplacement` with the size is not established: the managed code reads `BodyLength` only for the
agent scale, the camera and the item's effectiveness, so any travel scaling happens natively. The moose's travel was
measured at 100, so watch its hooves at 150.

## Who rides them

Mike, 2026-09-23 evening: the moose for Thranduil and the lords, the great elk for the highest Mirkwood cavalry,
the Animalia elk for the lower cavalry; the `elk_rider` career start follows the lower cavalry (Mike extended #629's
rule, that a career kit is the culture's lowest troop gear, to the mount; `generate_career_kits.py` itself never
touches Horse). Every one of them sits on `taom_elk_saddle_a`, the only elk seat.
These were the great elk's rows until then ([elk.md](elk.md) "Who rides it").

| Owner | File | Mount | Reaches existing saves? |
|---|---|---|---|
| `mirkwood_rochenlas` (41, upgrades to beleglas) | `troops/troops_mirkwood.xml` (troop-level override, so every set) | Animalia elk | Yes, after a restart |
| `mirkwood_beleglas` (46, the top) | same | great elk (unchanged) | Yes, after a restart |
| Thranduil (`thranduil_bat_equipment`) | `equipmentsets/taom_equipment_sets_mirkwood.xml` | moose | **New campaign only**: `Hero._battleEquipment` is a `[SaveableProperty(210)]` (v1.5.3), so an existing campaign keeps what he was created with: the `charger` in any campaign from a released build (v2.0.30 and earlier), the great elk only in one started on a development build that carried #636's lord wiring |
| The lords on `mirkwood_bat_template_medium_a..e` | same | moose | **New campaign only**, same reason and same two cases |
| Heroes equipped later from `taom_mirkwood_{lord,ruler}_battle_{male,female}` | `equipmentsets/taom_lord_template_equipment.xml` | moose | Yes, for heroes equipped after the restart |
| `elk_rider` career start (`player_career_mirkwood_cavalry_m` / `_f`) | `equipmentsets/taom_career_starting_equipment.xml` | Animalia elk | Character creation only |
| A player starting as a Mirkwood lord or ruler (Advanced Start) | the lord or ruler template above, through `CampaignAdvancedStartingPlayerOptionsCampaignBehavior.AssignMainHeroEquipmentKeepingHorse` | moose and elk saddle, into the party inventory | New game only |

Legolas keeps his horseless set and every civilian set keeps its horse. The lord-template file is generated, but
its generator is broken (#637), so the four rosters were edited by hand, as the great elk's were. Tests:
`AnimaliaMountWiringTests` (the rows above), `ElkMountWiringTests` (beleglas, and every elk or moose on the elk
saddle). The Animalia elk is guaranteed stock in every Mirkwood-owned town (Mike: "The starting elk should also be
available in the marketplace"): `culture_marketplace_config.xml` routes it to `mirkwood` with `min_stock="1"`,
beside the great elk and the saddle, so a player who loses the starting elk buys the same animal. **The moose is
sold too, by chance:** TAOM's culture pool takes every `Culture.mirkwood` item and does not read `is_merchandise`, so
each day's draw can put a moose in a Mirkwood-owned town (about once in two weeks per town; Mike, 2026-09-23: let it
appear). `is_merchandise="false"` still keeps both animals out of vanilla loot, workshop output and tournament
prizes; a caravan can buy one from a market, since `CaravansCampaignBehavior.BuyCategory` does not read it either.
`AnimaliaMountWiringTests.ElkRiderStartingMount_AndItsSaddle_AreGuaranteedStockInMirkwoodMarkets` reads the
career roster, so a new starting mount must be routed too.

**The two test riders stay visible** (Mike, 2026-09-23): `taom_test_animalia_elk_rider` and
`taom_test_animalia_moose_rider` (Soldier, Cavalry, Mirkwood, registered for CustomGame) appear in every Custom
Battle's Mirkwood cavalry picker, in English in every language. The elk rider is an exact twin of
`mirkwood_rochenlas` now. **Delete `troops/troops_animalia_test.xml`, its SubModule.xml node, its two ladder
exemptions (`tools/taom_schema.py`, `tools/melee_ladders.json`) and the `taom_test_` recruitment exemption before the
next player release**; `AnimaliaMountWiringTests.TestRiders_*` goes with them.

## Configuration

- **The antler attack:** compiled constants in `Main/Features/Animalia/AnimaliaConfig.cs`: the Monster and set ids,
  the 1.0x reach (1.5 m trigger, 2 m radius) and `ReachScalesWithBody`, one Blunt blow (elk 60, moose 70; a quarter on
  a shield block), knockback 35 / 45, facing 0.25, a 10 s cooldown, one target. A change needs a build.
- **Size:** each Monster's `taom_body_length` in the live `lotr_monster_animalia.xml` (elk 100, moose 150), read at
  every game init ([monster-size.md](monster-size.md)); no build.
- **Riders and market:** the repo data files in "Who rides them" and `culture_marketplace_config.xml` (the provider
  is a singleton: restart the game after an edit).

## Tests

- C#: `AnimaliaConfigTests` (5), `AnimaliaAttackServiceTests` (7), `AnimaliaWiringTests` (4) and
  `AnimaliaMountWiringTests` (12; its live-Armory reads are Inconclusive where the Armory is absent), the behavior's
  entry in `BehaviorTreeMissionLogicInheritanceTests`, and `ElkMountWiringTests`' elk-saddle check over all three
  animals.
- Tools: `tools/tests/test_apply_animalia_armory.py` (27, the live recipe parity pair included). The PowerShell tools
  have no test harness: `gen_animalia_anim_clips.ps1 -Verify` and the `wire_anim_master_skeletons.ps1` census are
  their checks.

## Performance

Per ridden animal, a tree pass about five times a second; each scan reads the native `AgentScale` once and queries
nearby agents over 2 m times the scale (2 m for the elk, 3 m for the moose). The attack fires at most once per 10 s.
Without these animals in a mission the behavior costs two empty prune loops per tick. The size pass is MonsterSize's,
once per game init.

## Dependencies

- **`LOTRLOME_Armory`** (unversioned): the Monsters, the two sets and twins, the two antler actions, the two items,
  the name rows, every clip and mesh package ([ledger](../reference/lotrlome-animalia-changes.md)). The recipe
  replays on top of the war ram's (`act_war_ram_butt`, the `/WARG` anchor) and the great elk's (`taom_elk_saddle_a`,
  its Monsters registration) edits, so after a reinstall redo those first.
- **The great elk (#636):** `taom_elk_saddle_a` is the seat of both animals. Its ledger's rollback to
  `.bak-elk-20260922` predates the Animalia items and would delete them.
- **`Main/Features/ElephantLike/`**: the shared attack service, profile and BT nodes, including
  `reachScalesWithBody`.
- **`Main/Features/MonsterSize/`**: the size ([monster-size.md](monster-size.md)).
- **`Main/Features/CareerSystem/`**: `ICareerAgentStatService.MountChargeMultiplier`, the rider's charge bonus.

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
| `Main/Features/Animalia/*.cs` | The antler attack (see "The antler attack") |
| `TAOM.Tests/Features/Animalia/AnimaliaConfigTests.cs`, `AnimaliaAttackServiceTests.cs`, `AnimaliaWiringTests.cs` | Reach, cooldown, damage pins; monster gates and damage; the IoC and mission-behavior lines, the reach flag, the container |
| `tools/tests/test_apply_animalia_armory.py` | The Armory writer on a synthetic tree (dry run, refusals, byte stability, backups, half-present steps) and its recipe against the live Armory, attribute for attribute |
| `docs/ai-includes/quadruped-pack-to-horse-skeleton-workflow.md` | The whole procedure as a workflow, for the next pack |
| `tools/gen_animalia_anim_clips.ps1` | The 54 `_anm.tpac` clips (52 + two `_movement` standing clips), cloned from vanilla horse clips, with the measured values |
| `tools/apply_animalia_armory.py` | The Armory edits: Monsters file + registration, `as_animalia_*` action sets and twins, the two Horse items (dry run / `--apply`) |
| `docs/reference/lotrlome-animalia-changes.md` | Ledger of every live Armory edit, backups, redo steps |
| `Main/_Module/ModuleData/troops/troops_animalia_test.xml` | The two test riders (CustomGame only), registered in `Main/_Module/SubModule.xml`; exempt from the armour and melee ladders and from the recruitment-reachability test, all marked for removal with the file |
| `TAOM.Tests/Features/Animalia/AnimaliaMountWiringTests.cs` | 12 tests: test riders pair mount and saddle; items name their Monster and mesh; Monsters are horses on their own set and registered; sets are children of `as_horse` with `_map` / `_town_and_village` twins; every bound clip exists and every bound type is an `as_horse` action (or the antler); the antler actions are `actt_kick` and bound; and the real riders ("Who rides them"): the lower cavalry, Thranduil and the lord templates, the generated lord and ruler templates, the career start; the career start's mount and saddle as guaranteed Mirkwood stock; each Monster's size and its item's placeholder |
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
   backups beside them. The Kit re-read them at the 12:39 load. OWED: a vanilla gallop on each mesh (the
   rest-pose and normal-map check). Re-run the census after any re-import of a clip: a re-import can bring the
   empty reference back.
2. **Clip resources:** DONE (54, above), with their RuntimeDataCache entries (the 12:39 Kit load;
   `check_rdc_entries.py` reads 0 missing). Jumps later: in `jump_run_high` the pelvis climbs to
   3.36 m, about 2 m above its standing 1.37 m, and the engine flies a mount's jump itself, so the jump clips
   need the vertical travel removed and a cut into start / loop / end before they can replace vanilla's.
3. **Game side:** Monsters, action sets, items and the antler actions DONE (both on `taom_elk_saddle_a`; the
   moose's hump may want its own saddle). The antler attack's C# is written and tested; real riders DONE (evening,
   "Who rides them"); the size moved onto the Monsters. OWED: a deploy, then the in-game checklist below, the
   twelve translations of the two item names (English rows registered; `/localize`, a paid run), deleting the test
   riders before the next release, the jumps.
4. In-game ladder per [creature-mount-authoring.md](../ai-includes/creature-mount-authoring.md).

### In-game checklist (owed, after a deploy)

1. **Sizes after the move** (a Custom Battle, then a campaign, then a second Custom Battle in the same process: the
   campaign re-reads its own Monster files, and the pass sits before the once-per-process guard so it runs each
   time): the TAOM log shows `[MonsterSize] 3 Monster size(s), 2 Horse item(s) resized: taom_elk_a=110 (was 100), taom_animalia_moose_a=150 (was 100)` (the Animalia elk is 100 on its Monster and its placeholder alike, so it needs no write). The rgl log shows one
   `The 'taom_body_length' attribute is not declared` line per sized Monster, no "required" line for `body_length`,
   and the size pass's `opening <path>` lines. Each animal is the size it was (the table above).
2. **The antler attack** (Custom Battle, Blow Diagnostics on: MCM `TAOM — Blow Diagnostics`): `[Animalia]
   Initialized`, then `Attached behavior trees to 0 elk(s) and 0 moose` in a Custom Battle (the trees arrive by
   late attach, `First late-spawn tree attached`, which does not say which animal), and none of the three
   `[Animalia]` error lines. Per hit one `[BlowDiag]` line, `dmgType=Blunt`, the elk `dmg=60 mag=35`, the moose
   `dmg=70 mag=45`; on a shield block 15 and 18; `attackerIdx` is the RIDER's index. Look for a hit owned by the
   MOUNT during the clip: that would be a native kick hit on top of TAOM's (the clips carry no combat parameter, so
   none is expected). The moose's antlers reach what they strike at 150.
3. **A campaign battle:** the Blunt blow can kill (Custom Battle kills every downed agent, so it proves nothing
   here): finish several troops with the attack alone and look for at least one kill.
4. **The `elk_rider` career start** (new character): the Animalia elk and the elk saddle in the inventory; the
   player-ridden attack fires; `dmg` above 60 while Antler Crash is active.
5. **The map:** in a new campaign, Thranduil's or a moose-riding lord's party icon and the `elk_rider` player's show
   their mount (at horse size: neither item sets `scale_factor`) with no "Invalid action set code". These are the
   first uses of `as_animalia_moose_map` and `as_animalia_elk_map`.
6. **An existing save** after a restart: it loads; `mirkwood_rochenlas` rides the Animalia elk (troops re-read
   their XML); the lords keep the mount they were saved with.
7. Rear, kick, hit reactions and deaths for both animals; the moose's hooves at 150; the saddle on both bodies.
8. **The market:** in a new campaign, a Mirkwood-owned town after its first day holds at least one Animalia elk and
   the elk saddle; over a couple of weeks a moose may turn up too (by chance, Mike's call).

## Known gaps

- The moose's head pivots about 0.54 m off `horse_head` (accepted trade, above).
- Hoof bones are not driven by the clips (the pack has no hoof joint): they hold their rest relative to the
  ankle.
- The curving gaits (`loco_*_l/_r`) lose their root turn; the engine steers, so they play as straight gaits.
- A slight pinch at the elk's front elbow in extreme poses (the rearing strike).
- Seen in game for gaits and idles only (Mike: "look great"); rear, kick, hits, deaths and the attack not yet
  judged.

## Changelog

- 2026-09-23: packs exported; four elk and two moose variants reskinned onto `horse_skeleton` with the
  pack's weights; 64 + 33 clips retargeted and checked; 1K textures; staged in the Armory's sources. Tools
  added: `reskin_animalia_to_horse.py`, `retarget_animalia_to_horse.py`, `animalia_to_horse_map.json`;
  `convert_tripo_prop_textures.py` gained `--match` and stopped missing `_ao` maps. Mike kept two variants,
  `animalia_elk_08` and `animalia_moose_big`, moved the sources into `AssetSources\creature\elk\` and imported
  textures, materials and both meshes in the Kit, then all 97 clips; the Kit left every master's skeleton
  reference empty, and `tools/wire_anim_master_skeletons.ps1` pointed all 97 at `horse_skeleton`. Then
  `tools/gen_animalia_anim_clips.ps1` wrote the 52 clip resources from vanilla horse templates with measured
  travel, hoof plants and fall points; turns and jumps stay vanilla. Game side: Monsters, action sets and items
  via `tools/apply_animalia_armory.py`, Custom-Battle test riders; first battle clean, gaits and idles confirmed
  by Mike; moose `body_length` 150. The antler attack: `Main/Features/Animalia/` on the elephant-like engine, its
  two `actt_kick` actions bound in the Armory, 9 new tests (suite 10,222). The workflow written up as
  `docs/ai-includes/quadruped-pack-to-horse-skeleton-workflow.md`. Evening: real riders (Mike): the moose to
  Thranduil, the five lord battle templates and the generated lord and ruler templates; the Animalia elk to
  `mirkwood_rochenlas` and the `elk_rider` career start; the great elk kept for `mirkwood_beleglas`. 13 Horse ids
  changed by hand-scoped edit, the rider pins moved from `ElkMountWiringTests` to `AnimaliaMountWiringTests`. Then
  the size moved onto the Monsters (`taom_body_length`, [monster-size.md](monster-size.md)) and the reach follows the
  live size; the deep review's fixes: the four service types split into their own files, literal id and behaviour
  pins, the antler-type reason corrected, the Armory snapshot refreshed, the two item names registered in English,
  the twin comment corrected, and the writer scripts guarded against a running game or Kit. Night: the starting
  elk routed into Mirkwood markets (`min_stock` 1); the final review (8 lenses and Codex): the skeleton patcher
  checks the rig's bone count again (it could re-point another rig's empty master), the clip generator refuses a
  failed measurement and checks clip names and checksums, the size pass warns on a size nothing rides, the three
  reflection targets joined the binding gate, the moose's sale made true in the docs (Mike: it may be sold), and the
  Animalia wiring and reach-flag pins added.

## GitHub Issue

- **Issue:** #646 [Animalia elk and moose: horse_skeleton reskin, retargeted clips, Mirkwood moose mount](https://github.com/haterade22/TAOM/issues/646)
- **Status:** Open
