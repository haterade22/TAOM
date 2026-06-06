# Bannerlord Monster model — `monsters.xml` → the engine rig (Phase 3)

> **One process, traced from the decompile** (`TaleWorlds.Core/Monster.cs`, v1.4.5): the `Monster` is the single
> data source for a creature's entire rig — it's what phases 1–2 read from, and **exactly what TAOM authors per
> creature** (the spider/elephant `lotr_monster_*.xml`). This doc is the field-by-field `monsters.xml` schema +
> what each does + the gotchas. Part of the phased engine study; depends on
> [agent-spawn-and-render-pipeline.md](agent-spawn-and-render-pipeline.md) (Phase 1, `CreateAgent` reads the Monster)
> + [animation-binding-and-playback.md](animation-binding-and-playback.md) (Phase 2, `FillAnimationSystemData`).

## WHAT it is

A `Monster` (one `<Monster>` row in a `monsters.xml`) is the engine's description of a creature/agent body:
which **skeleton bones play which roles** (look-direction, ragdoll, IK feet, rider seat, reins), the **physics
capsule**, **behavior flags** (mountable, rears, flees, wanders, humanoid), the **animation binding**
(`action_set` + `monster_usage`), and **combat/movement stats** (weight, HP, speeds, charge, family). Every
creature feature — render, animation, ragdoll, IK grounding, mounting, AI — bottoms out in fields set here.
**The Monster is the contract between the creature's skeleton and the engine.**

## HOW it's read — `Monster.Deserialize(XmlNode)` (Monster.cs:~250-600)

Each `monsters.xml` attribute maps to a field. Grouped:

### Identity / animation binding
| Attr | Field | Role |
|---|---|---|
| `action_set` | `ActionSetCode` | the `MBActionSet` (act_* → clip map) — Phase 2. e.g. `as_spider`/`as_elephant` |
| `female_action_set` | `FemaleActionSetCode` | female variant |
| `monster_usage` | `MonsterUsage` | the monster-usage set (mount/dismount + movement vocab). 1.4.X animals use `"horse"`; empty if not set + not inheriting |

### Combat / classification
`weight` (default 1), `hit_points` (default 1), `absorbed_damage_ratio`, **`family_type`** (groups species — mount
AI / herd behavior keys off it), `sound_and_collision_info_class` (e.g. `bovine`).

### Movement
`num_paces`, `walking_speed_limit`, `crouch_walking_speed_limit` (defaults to walking), `jump_acceleration`,
`jump_speed_limit`, `relative_speed_limit_for_charge` (default `float.MaxValue`), `arm_length`, `arm_weight`.

### The bone-index map — resolved BY NAME against the skeleton ⭐
`DeserializeBoneIndex(node, "<attr>", …, validateHasParentBone: true/false)` looks the bone up **by name** on the
creature's skeleton and stores its **index**. `validateHasParentBone: true` validates the bone exists + has a
parent — **a wrong/missing bone name yields -1 / fails, silently breaking that feature.** Bones:
- **Look / pose:** `head_look_direction_bone`, `thorax_look_direction_bone`, `spine_lower_bone`, `spine_upper_bone`,
  `neck_root_bone`, `pelvis_bone`, `right/left_upper_arm_bone`, `body_rotation_reference_bone`, `fall_blow_damage_bone`.
- **Ragdoll / FX:** `ragdoll_bone_to_check_for_corpses_N`, `ragdoll_fall_sound_bone_N`,
  `ragdoll_stationary_check_bone_N`, `move_adder_bone_N`, `splash_decal_bone_N`, `blood_burst_bone_N`,
  `terrain_decal_bone_0/1`.
- **Hands / weapons (humanoid):** `main/off_hand_bone`, `main/off_hand_item_bone`,
  `main/off_hand_item_secondary_bone`, `off_hand_shoulder_bone`, `hand_num_bones_for_ik`.
- **Feet / IK (creatures!):** `primary/secondary_foot_bone`, `right/left_foot_ik_end_effector_bone`,
  `right/left_foot_ik_tip_bone`, `foot_num_bones_for_ik`. ⚠️ **The rig only exposes TWO foot-IK effectors
  (right/left)** — a >2-leg creature (spider) can only ground two legs through this; the rest follow the clip.
- **Slope adaptation:** `front/back_bone_to_detect_ground_slope_index`, `bones_to_modify_on_sloping_ground_N`.
- **Mount / rider:** `rider_sit_bone`, `rein_handle_bone`, `rein_handle_left/right_local_pos`, `rein_skeleton`,
  `rein_collision_body`, `rein_collision_1/2_bone`, `rein_head*`, `rein_right/left_hand_bone`. (Only meaningful for
  rideable mounts.)

### Capsules + flags (sub-nodes, parsed in the same Deserialize)
- `<Capsules><body_capsule radius= pos1= pos2=/>` → the physics collision capsule (PhysX — Phase 0 toolchain).
- `<Flags Mountable= CanRear= RunsAwayWhenHit= CanCharge= CanWander= IsHumanoid= …/>` → behavior flags. For a
  TAOM creature-troop: `Mountable="false"` (never a rideable mount — the map-icon `ForceUpdateBoneFrames` crash),
  `IsHumanoid="false"`.
- Eye/camera: `standing_eye_height`, `eye_offset_wrt_head`, `rider_camera_height_adder`.

### `base_monster` inheritance
The `flag` in Deserialize is "updating an existing base monster": a `<Monster>` can inherit from a base and
**override** only the attributes it specifies (unspecified ones keep the base value; specified ones replace). This
is how vanilla derives many monsters from a base humanoid/horse.

## WHY it's shaped this way

The engine drives look-at, ragdoll, foot-grounding-IK, slope-tilt, mounting, and FX **generically** — it doesn't
hard-code "the head is bone 8." Instead each Monster declares *which bone plays each role by name*, so the same
engine code works for a human, horse, or spider. The flags let the campaign/mission AI treat the creature
correctly (a `CanWander`/`RunsAwayWhenHit` animal vs a charging mount). `family_type` + `sound_and_collision_info_class`
group species for herd AI + foley. The capsule is the PhysX body. This name-based indirection is why **the bone
names in `monsters.xml` MUST exactly match the creature's skeleton** — and why a typo silently disables a feature.

## Where it's consumed (ties to other phases)
- `FillAnimationSystemData` (Phase 2) bundles `ActionSet` + `MonsterUsageSetIndex` + the bone map + speed limits
  into `AnimationSystemData`.
- `FillCapsuleData` / `FillSpawnData` (read in `CreateAgent`, Phase 1) supply the physics capsule + spawn data.
- `CreateAgent(monster, …)` (Phase 1) passes the Monster into native agent creation.
- The flags drive mount/AI; `Mountable` gates the map-party-icon path TAOM must avoid for creatures.

## TAOM relevance + gotchas
- This file is **exactly** what TAOM authors per creature: `lotr_monster_spider.xml`, `lotr_monster_elephant.xml`.
  Get the bone names right (match the skeleton) and the flags right (`Mountable=false`/`IsHumanoid=false` for a
  creature-troop) and most of the rig "just works."
- **Foot-IK is 2 effectors only** — a >2-leg spider can't fully ground via this; expect partial grounding
  (matches the AnimFlags `disable_foot_ik` note). Don't `disable_foot_ik` on grounded clips.
- **Wrong bone name = silent -1** (validateHasParentBone): broken look-at/ragdoll/IK with no error. Cross-check every
  `*_bone` attr against the actual skeleton (`tpac_skeleton_dump.py`).
- The mount/rein bones are dead weight for a non-rideable creature-troop — omit them.
- A missing `monster_usage`/`action_set` doesn't fail deserialize (defaults to ""), but yields no animation
  (Phase 2 diagnostic chain). Authoring a custom usage/action-set must match the skeleton.

## Evidence (file:line, v1.4.5)
- `TaleWorlds.Core/Monster.cs` `Deserialize`: action_set/female/monster_usage @304-318; weight/hit_points/num_paces/speeds/jump @327-367; arm/jump/charge/family_type @470-495; the full bone map @496-559 (`DeserializeBoneIndex`/`DeserializeBoneIndexArray`, `validateHasParentBone`); foot-IK @523-530; rein bones @531-559; capsules/flags/eye-heights sub-nodes parsed in the same method.
- Consumed: `Monster.FillAnimationSystemData` (`TaleWorlds.MountAndBlade.cs`:101588) + `CreateAgent` (`Mission.cs`:4040-4053) + `FillCapsuleData`/`FillSpawnData`.
