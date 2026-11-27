# Bannerlord castle gates: CastleGate, its prefab, and what a siege scene must set

> **Traced from the decompile and the game data** (installed v1.5.7, 2026-11-26): how a gate opens, takes damage and
> steers the siege AI, how its prefab is built, what a scene must set. Builds on [usable-machines.md](usable-machines.md)
> and [scene-gameentity-scriptcomponent.md](scene-gameentity-scriptcomponent.md) (prefabs, the siege wall set-up).
> C# citations are v1.5.7 `taom-src` files (`pwsh tools/taom-src.ps1 path <Type>`); `MODS` is the game's `Modules`.

## What a castle gate is

One scene entity with two scripts: `CastleGate` (`CastleGate.cs:13`, `: UsableMachine`) and a `DestructableComponent`
on the same entity (`UsableMachine.cs:381`, `CastleGate.cs:199`). The door is a child entity with a skeleton of a root
and two hinge bones and a rigidly skinned mesh; only named animation clips move it. Defenders open and close it from
standing points; attackers break it with a battering ram. A siege scene needs one outer and one inner gate, navmesh
faces painted with each gate's ids, castle-side faces whose ids end in 1, and a ram path that ends at the outer gate.

## The life of a gate in a siege

1. **Set-up.** An assault removes every entity tagged `sally_out`, a sally-out every entity tagged `siege_assault`
   (`SiegeMissionPreparationHandler.cs:84-91`). A destructible whose `CanBeDestroyedInitially` is true may be
   pre-destroyed by campaign wall damage (`:121`); the field defaults to true (`DestructableComponent.cs:64`), and
   every vanilla gate prefab writes false (`MODS/Native/Prefabs/props_siege_gates_outer.xml:15`, `:4774`).
2. **Start closed.** `SetInitialStateOfGate` parks the closing clip at 0.99 and sets `State = Closed`, with no null
   check on the door skeleton (`CastleGate.cs:357-363`): a gate with no `gate` child or no skeleton fails here.
3. **The AI binds two gates.** `TeamAISiegeComponent` takes the first `CastleGate` tagged `outer_gate` and the first
   tagged `inner_gate` from `ActiveMissionObjects`, with no visibility filter (`TeamAISiegeComponent.cs:54-57`).
   `TacticDefendCastle.cs:396`, `BehaviorAssaultWalls.cs:122` and `MovementOrder.cs:902` read them with no null
   check. A hidden gate (`visible="false"` on it or a parent) never ticks, so it never opens while the AI counts it:
   the tick tests only `IsVisibleIncludeParents` (`CastleGate.cs:609`, `:619`; Helm's Deep's `visible="false"` inner
   gate, [long-defeat.md](../../features/long-defeat.md):51). For a gate outside the active level, see Not verified.
4. **Lanes.** `SideTag` `left`, `middle` or `right` puts the gate in that lane (`CastleGate.cs:236-250`). A lane is
   passable through its gates only when each is open or destroyed (`SiegeLane.cs:352-364`).
5. **Who opens it.** The AI gives defenders `Use` and attackers `AttackEntity` (`CastleGate.cs:183-194`). An agent on a
   standing point tagged `open` opens the gate; on any other point it closes it (`:738-753`). While the door swings,
   and on the point that matches the current state, the points are switched off (`:777`). `OwningTeam` does nothing
   on the outer and inner castle gate families: `SetUsableTeam` touches only `StandingPointWithTeamLimit` (`:288`), and
   those prefabs hold plain `StandingPoint`s with no `attacker` or `defender` tag (`StandingPoint.cs:145-153, 167-169`).
   It acts on the two sally doors, which hold one (`props_siege_gates_inner.xml:3111`, `_sally.xml:85`).
6. **AutoOpen and the sally-out.** At mission start `SetAutoOpenState(Mission.Current.IsSallyOutBattle)` overwrites
   the prefab's `AutoOpen` (`CastleGate.cs:303`). With AutoOpen on, the gate opens while defender AI paths cross it
   and closes when none do (`:755-769`); its faces stay enabled (`:592`, and `OpenDoor` and `CloseDoor` skip
   `SetGateNavMeshState`, `:406-413`, `:430-437`). Instead, while the gate is closed, the attacker AI agents active at
   that call are excluded from its faces (`SetAgentExcludeStateForFaceGroupId(NavigationMeshId, !isEnabled)` over
   `attackerTeam.ActiveAgents`, `:571-585`, `:593`, `:436`); opening lifts it (`:412`), and so does AutoOpen going off
   (`:604`). A hand on `open` turns AutoOpen off; a hand on `close` in a sally-out turns it on again (`:741-752`).
   `SallyOutMissionController` holds the gates open during the besieged deployment, then turns AutoOpen on, then
   opens a closed gate when the player's eye is within 5 m (`SallyOutMissionController.cs:103-139`). In an assault
   no code opens a gate by itself: outside `CastleGate`, only `SallyOutMissionController` and `MissionMultiplayerSiege`
   call `OpenDoor`, `CloseDoor` or `SetAutoOpenState` (search scope: the MountAndBlade.dll and SandBox.dll decompiles).
7. **The clips.** Clips are found by name (`MBAnimation.GetAnimationIndexWithName`, `CastleGate.cs:278-279`) and
   played on channel 0 of the door skeleton, with no action set (`:401-446`). Reversing mid-swing starts the other
   clip at 1 minus the progress (`:417-420`, `:441-444`). The bar (`plank` child) hides when the gate opens and shows
   when it is closed past 0.9 (`:421`, `:793-796`).
8. **Navigation.** With AutoOpen off, opening and closing switch the scene's navmesh faces with id `NavigationMeshId`
   (`:406-409`, `:430-433`, `:558-569`); with it on, they stay on (step 6). `NavigationMeshIdToDisableOnOpen` faces
   are switched on at start and off while the gate stands open, and on again once it is destroyed or closing past
   0.4 (`:350-356`, `:623-648`). The gate brings no navmesh of its own (`NavMeshPrefabName` is empty,
   `props_siege_gates_outer.xml:27, 51`): the scene's navmesh holds those faces.
9. **Hits.** A hit climbs from the struck entity through its parents until a script's `OnHit` returns true
   (`Mission.cs:2254-2273`); the base `MissionObject.OnHit` returns false (`MissionObject.cs:182-189`). A hit with an
   empty weapon does 0 damage unless a ram struck it (`DestructableComponent.cs:337-340`). Only a ram blow of 200 or
   more on a closed gate plays the hit clip, the plank hit clip and the door sound (`CastleGate.cs:1026-1034`); that
   clip is a shake of at most 3.95 deg, and the door does not open (The swing). `HitMeleeAnimationName` is declared
   (`:63`) and read nowhere (scope: the taom-src cache and the two full decompiles).
10. **Damage steps.** `DestructionStates` is a comma list (`DestructableComponent.cs:174-176`). With N names the step
    rises by one each time `MaxHitPoint / N` is lost (`:405-418`). Step k shows the child named by the k-th name, or
    else a prefab of that name (`:504`, `:557`, `:580`). Children with a dynamic body are thrown as debris
    (`:444-451`). `CastleGate`'s clamp `Min(index, count - 1)` (`CastleGate.cs:1054-1061`) receives the length of the
    string `DestructionStates` (`DestructableComponent.cs:418`), so it never binds: the killing blow shows the last
    listed state. The `HitPoint` setter clamps at 0 (`:117`), so a blow never indexes past the list. The out-of-range
    case is a step that goes down (a heal): see [usable-machines.md](usable-machines.md) and the `SetGateLevel`
    comment in `Main/Adapters/LongDefeatBattlefieldAdapter.Gates.cs:73-75`.
11. **The ram.** `BatteringRamSpawner` passes its `GateTag` to the ram (`BatteringRamSpawner.cs:20, 140`); the ram
    takes the first entity with that tag that has a `CastleGate` (`BatteringRam.cs:236-243`). It deals stored power
    times `DamageMultiplier`, divided by the elapsed part of the swing clip (progress times clip duration,
    `:468-469`), when the clip passes a fixed point, not on contact (`:442-473`); the vanilla ram prefab sets 300
    (`props_siege_batteringrams.xml:24`, code default 10, `BatteringRam.cs:96`). It stops once the gate is open or
    destroyed (`:408`). After it, the ram's formation is sent at the inner gate (`BatteringRamAI.cs:15-17`).
12. **Destruction.** `OnDestroyed` hides the plank, switches off the standing points, plays the break clip when a ram
    killed the gate, and opens the gate's faces (`CastleGate.cs:1036-1052`). It runs before the step change
    (`DestructableComponent.cs:372`, then `:385`), and the new state takes the clip over only when it has a skeleton
    (`:591-600`). The break clip throws the leaves open toward -y (The swing), but the vanilla wreck has no skeleton,
    so the clip plays on a door that is hidden at once (code reading).

**A peaceful visit.** In a campaign town or castle every gate is flagged civilian
(`MissionSettlementPrepareLogic.cs:10-24`), then opened with its faces on and disabled with its destructible
(`CastleGate.cs:365-372`).

## The gate as data

**The tree** of `european_castle_gate_outer_l2` (`MODS/Native/Prefabs/props_siege_gates_outer.xml:4761-5361`, line
of each entity in brackets). Other vanilla castle gates have the same shape; inner prefabs carry `inner_gate`. In the
third gate file, `props_siege_gates_sally.xml`, and in `castle_door_sally`, `middle_position` is child 0, not 5.

```
european_castle_gate_outer_l2 (4761)  tag outer_gate; DestructableComponent (4770: states "state1, state2, state3,
                                      state4", MaxHitPoint 15000, CanBeDestroyedInitially false, ReferenceEntityTag
                                      reference) and CastleGate (4789: the six clip names, LeftDoorBoneName
                                      bn_middle_l, RightDoorBoneName bn_middle_r, SideTag middle, NavigationMeshId 461)
  open_inside       (4815)  tag open; StandingPoint; y -1.5 (castle side)
  collider_agent_r  (4844)  tag collider_agent_r; body bo_..._ab_r          agent blocker, set to the right hinge
  clean             (4855)  tags operational, state_0, gate, reference; SynchedMissionObject; skeleton
                            european_castle_gate_outer_l2_l3_skeleton with the skinned mesh ..._state1   THE DOOR
    collider_l, _r  (4878, 4892)  tags collider_l, collider_r; bodies ..._state1_l, _r      the leaf bodies
    extra_collider_l, _r (4906, 4921)  extra colliders, on only during the swing (CastleGate.cs:468-548, 780-791)
  close_inside      (4938)  tags close, outside; StandingPoint; y -1.5
  close_outside     (4968)  tags close, outside; StandingPoint; y +1.0 (field side)
  middle_position   (4998)  tags middle_pos, wait_pos; TacticalPosition; y -1.4   the scene moves and widens it
  gate_plank        (5025)  tag plank; SynchedMissionObject; own plank skeleton and mesh                the bar
  battering_ram_rest_point (5045)  y +6.25; no tag, no script; no C# reads the name (0 hits in the taom-src cache)
  state1 .. state3  (5048, 5138, 5228)  hidden; tags state_1..3, gate, reference; the same door skeleton with the
                            meshes ..._state2 .. _state4; their own collider_l, _r and a particles child
  state4            (5318)  hidden; tags gate, state_4, reference; NO skeleton: the static wreck ..._state5
  collider_agent_l  (5349)  tag collider_agent_l; body bo_..._ab_l
```

The door is the `gate` child with the lowest `state_N` at start and the highest visible one after a step
(`CastleGate.cs:874-916`). The door's `collider_l`, `collider_r` and the root's `collider_agent_l`, `collider_agent_r`
follow the hinge bones named by `LeftDoorBoneName` and `RightDoorBoneName` (`:458-463`, `:992-993`); with a door
collider missing, the agent blockers are hidden (`:927-931`).

**The door skeleton and clips.** Each family has a door skeleton and a plank skeleton in Native's `skeletons.tpac`, one
master animation each in `animations.tpac`, and seven clips in `animation_clips.tpac` named `<family>_idle`, `_hit`,
`_hit_melee`, `_opening`, `_closing`, `_break` and `_plank_hit`. No XML defines them; the prefab names the skeleton
(`skeleton_model=`) and the clips (the `CastleGate` variables). On the european and empire families the master
frames are: idle 0 to 1, hit and hit_melee 1 to 30, opening 30 to 130 (3.0 s; empire 30 to 104, also 3.0 s), closing
the same frames backward, break 140 to 160. The skinning is rigid: every vertex of `empire_castle_gate_outer_l3_state_1`
sits 100 percent on one bone, and the rest pose is the closed gate. Bone order of the european l2_l3 skeleton (dump):
`bn_root`, `bn_middle_l`, `bn_handle_b_l`, `bn_handle_f_l`, `bn_middle_r`, `bn_handle_f_r`, `bn_handle_b_r`; the
empire inner l3 has `bn_root`, `bn_middle_l`, `bn_middle_r`. Read with `tools/dump_engine_skeleton.ps1`,
`tools/read_anim_keyframes_tpac.ps1` and a raw clip reader (TpacTool cannot open `animation_clips.tpac`).

**The vanilla families.** Sizes from the mesh package headers, hinges from skeleton dumps, hit points from the prefabs.

| Prefab | Role | Door skeleton (bones) | Closed door w x h (m) | Hinge x / z (m) | MaxHitPoint |
|---|---|---|---|---|---|
| `european_castle_gate_outer_l2_l3`, `_l2` | outer | `european_castle_gate_outer_l2_l3_skeleton` (7) | 4.695 x 6.838 | +-2.363 / 2.50 | 15000 |
| `european_castle_gate_outer_l1` | outer | its own | 3.437 x 4.673 | not dumped | 12000 |
| `empire_castle_gate_outer_l3` | outer | its own (3) | 5.321 x 5.921 | +-2.361 / 3.18 | 18000 |
| `aserai_castle_gate_outer_l1` | outer | its own (5) | 4.125 x 5.032 | +-1.78 / 2.50 | 12000 |
| `khuzait_castle_gate_outer_l1` | outer | its own (7) | 3.841 x 4.545 | -1.855, +1.873 / 1.27 | 12000 |
| `battania_castle_gate_outer_l3` | outer | its own (7) | 3.763 x 4.972 | -1.836, +1.822 / 1.52 | 18000 |
| `sturgia_castle_gate_outer_l3` | outer | its own (7) | 3.796 x 5.602 | +-1.848 / 1.27 | 18000 |
| `empire_castle_gate_inner_l3` | inner | its own (3) | 4.521 wide, z -1.80 to 5.77 | -1.6885, +1.6868 / 2.50 | 1000 |
| `european_sally_door_a` | sally | one leaf, `bn_middle_r` | 1.752 x 2.673 | 0 / 1.22 | 10 |

**The swing**, measured from the master keys of three families (161 keys a bone, no position keys on a hinge): each
leaf turns about a vertical hinge toward prefab -y, the castle side, by 83 to 90 deg over the opening clip; the
closing clip is the same frames backward. The code agrees: `CastleGate.cs:482-488` places the extra colliders at
`frame.origin - new Vec3(..., num * num3)`, always on -y.

| Family, leaf | Hinge (x, y), z | Leaf length (m) | Angle at the end of the opening clip | Free edge, open (x, y) |
|---|---|---|---|---|
| european outer l2_l3, left | (-2.363, +0.070), 2.5 | 2.362 | -87.44 deg (frame 130) | (-2.41, -2.30) |
| european outer l2_l3, right | (+2.363, +0.069), 2.5 | 2.362 | +89.33 deg | (+2.49, -2.30) |
| empire inner l3, left | (-1.689, -0.226), 2.5 | 1.706 | -89.93 deg (frame 104) | (-1.61, -1.93) |
| empire inner l3, right | (+1.687, -0.226), 2.5 | 1.691 | +90.13 deg | (+1.62, -1.92) |
| empire outer l3, left, right | (+-2.36, -0.084), 3.18 | 2.43, 2.46 | -83.42, +83.78 deg | (+-2.02, -2.49 / -2.52) |

The european leaves sweep x -2.85 to 2.85, y -2.34 to +0.25 (vertex bounds, conservative); their top is 6.822 m, the
inner leaves' 5.765 m. The hit clip is a damped shake of at most 3.95 deg (inner 3.23). The break clip throws the
european leaves open toward -y, to about 91 deg at key 153, settling near 87.5; the inner gate's holds its leaves
open near 90 deg on all 21 keys. The handle bones ride their leaves when opening and swing in hit and break.

## What a scene must set

A checklist for a siege level. **TRAP** marks a mistake that the build, the Kit or a test does not catch. The gate's
own checks run in the Kit and at `OnInit` (`CastleGate.cs:281`) and only warn.

- [ ] **One visible outer and one inner gate in every siege level**, each with its `<levels>` list, or with none as
  at Pelargir (vanilla: `vlandia_castle_a/scene.xscene:8875-8910, 9770-9797`). **TRAP:** a missing inner gate leaves
  `InnerGate` null for the three readers above; a hidden one never opens and holds its lane shut (step 3).
- [ ] **Prefer an inner prefab for the inner gate.** All three inner gates of `vlandia_castle_a` are outer prefabs
  tagged `inner_gate` in the scene (`scene.xscene:8875-8877, 16993, 34397`), which works only if a scene's tags
  replace the prefab's (UNVERIFIED; if they add, the gate carries both tags).
- [ ] **Never both tags on one gate.** `OnEditorInit` and `OnCheckForProblems` warn (`CastleGate.cs:335-338,
  1066-1070`), and `TeamAISiegeComponent` would bind one gate to both roles.
- [ ] **Gate face ids, set on each instance and painted in the scene navmesh.** Follow vanilla: outer 502 with 512
  to disable on open, inner 521 with 531 (`NavigationMeshId` 521 is set 222 times in 74 SandBoxCore scenes, 502 213
  times in 71: grep of `SandBoxCore/SceneObj/*/scene.xscene`, 2026-11-26). The inner gate's ids end in 1 because its
  faces are castle-side (next item). **TRAP:** an id that names no face in `navmesh.bin` gives a shut door that
  blocks no path (Helm's Deep before 2026-11-18, [long-defeat.md](../../features/long-defeat.md)).
- [ ] **Castle-side faces end in 1.** Every face a defender stands on inside the walls (courtyard, wall walks, the inner
  gate's faces) ends in 1; field-side faces and each ladder's own landing faces on the wall top (`OnWallNavMeshId`;
  `SiegeLadderSpawner.cs:135-137` warns) never do. The code tests the face under the agent. A defender on another face
  gets no machine, area or tower slot (`UsableMachine.cs:852`, from `:834`; `StrategicArea.cs:265`; `SiegeTower.cs:574`),
  no inner gate slot, and no outer gate slot when both gates lie ahead (`CastleGate.cs:689-724`). An attacker on an
  inside face gets no machine, area or tower slot (`UsableMachine.cs:848`, `StrategicArea.cs:261`, `SiegeTower.cs:585`),
  cannot panic (`CommonAIComponent.cs:183`) and counts as inside (`SiegeQuerySystem.cs:184, 257`). **TRAP:** paint
  only the gate faces 521 and 531, and the defenders lose those slots. Vanilla's inside ids: UNVERIFIED.
- [ ] **The ram.** One `batteringram_a_spawner` and path per siege level, each with `GateTag outer_gate`, `SideTag
  middle` and its `PathEntityName` (`vlandia_castle_a/scene.xscene:5455-5471` level 2, `:6616-6632` level 1,
  `:16626-16642` level 3), or one spawner listing every level (Pelargir `:70777-70795`). Each path ends by its level's
  outer gate's `battering_ram_rest_point`, which scales with the gate: vanilla 0.28, 0.36, 1.38 m (levels 2, 1, 3;
  gate scale 0.8 on 3), Pelargir 0.39 m (2026-11-26, last path point against the gate transform). The damage needs no contact.
- [ ] **The middle position.** Move and widen child `_index_ 5` (the `TacticalPosition`) of each castle gate inward:
  on level 1 vanilla sets `_width 7`, the outer's 12.8 m inward and beyond the inner gate, the inner's 5.7 m
  (`vlandia_castle_a/scene.xscene:9781-9786, 8894-8899`; level 2: 13.3 and 4.7 m; level 3: 17.1 and 3.1 m local, at
  gate scale 0.8). **TRAP:** the outer's must not lie between the two gates (`CastleGate.cs:1076-1100`, a warning).
- [ ] **Exactly one `wait_pos` child** (`CastleGate.cs:1071-1075`); the vanilla `middle_position` carries it.
- [ ] **Hit points** on the instance when the prefab's are wrong (vanilla inner 1000; TAOM Pelargir outer 25000).
  `MaxHitPoint 1` breaks the gate on the first blow that does damage.
- [ ] **Uniform scale only** (`CastleGate.cs:1113-1117`, a warning only). Dol Amroth scales its inner gate 1.126;
  seven TAOM gate instances in four scene folders are non-uniform (below), with the run-time effect not checked.
- [ ] **The gate is its own scene entity**, never a child of a gatehouse prefab: no prefab file in TAOM_Map or Native
  nests a prefab reference.

## What a custom gate must keep

**The tree musts**, each from the code above:

- `CastleGate` and `DestructableComponent` on the root; `CanBeDestroyedInitially false` (it defaults to true).
- `LeftDoorBoneName` and `RightDoorBoneName` set to the skeleton's hinge bones: the code defaults are `bn_bottom_l` and
  `bn_bottom_r` (`CastleGate.cs:71-73`). 14 of the 15 in Native's outer and inner files are `bn_middle_l`, `_r`
  (`props_siege_gates_outer.xml:41-42`); empty: `castle_door_sally` (`_inner.xml:3039`) and `european_sally_door_a`
  in the third file (`_sally.xml:24`).
- The clean door tagged `operational` and, as the first child with the root's `ReferenceEntityTag` (vanilla
  `reference`, `props_siege_gates_outer.xml:4778`, `:4860-4863`), `reference`. With no `operational` child the root
  is the original state (`DestructableComponent.cs:93`, `:220-222`), the first step hides it (`:497-498`), and the
  gate stops ticking (`CastleGate.cs:609-621`). The reference entity places a prefab state and carries the clip into
  the next state (`DestructableComponent.cs:173`, `:580`, `:591-600`); with none found the Kit warns (`:650-652`).
- Every child tagged `gate` has a `state_N` tag (`int.Parse` at `CastleGate.cs:898` when there are two or more) and
  a `SynchedMissionObject` (`_door.GameEntity` at `:916` throws on null; the static wreck has one too).
- A wreck state with no skeleton carries no door collider (`collider_l`, `collider_r`): they are collected under the
  current door (`:917-926`) and moved through `_doorSkeleton`, null there (`:450-452`, `:550-553`; code reading).
  The vanilla `state4` holds only a particles child (`props_siege_gates_outer.xml:5318-5348`).
- The door entity stays at identity under the root: the root's agent blockers take the bone frame, which is relative
  to the door (`:458-463`, `:877-878`).
- A `DestructionStates` list that names children; any number works (`DestructableComponent.cs:405-418`).
- A skinned door for a vanilla skeleton carries all of that skeleton's bones in the dump's order, the handle bones
  too: a mesh package holds bone indices, not names (`docs/changelog-archive/CHANGELOG-2026-H2-handwritten.md:10317`).
  Export with `add_leaf_bones=False` ([creature-mount-authoring.md](../../ai-includes/creature-mount-authoring.md):230)
  and keep the armature's real skeleton name for a mesh export (`:239-241`).
- **Co-op** (`E:/repos/BannerlordCoop/source`): it patches `CastleGate.OnHitTaken`
  (`GameInterface/Services/MapEvents/Patches/CastleGateHitCapturePatch.cs:14`), replays a ram hit through `_door`,
  `_doorSkeleton`, `_plank`, `HitAnimationName`, `PlankHitAnimationName`
  (`Missions/Battles/SiegeGateHitApplier.cs:18-39`) and syncs the destructible's `_currentStateIndex`
  (`Missions/Battles/SiegeMachineStateReplicator.cs:1421, 1782`). A gate that keeps the script and the vanilla tree
  goes through it unchanged; no prefab name is read.

**The routes**

| Route | What it is | What it costs | Weakest point |
|---|---|---|---|
| A | A vanilla gate prefab as it is; variant: a material per skinned sub-mesh from the scene (Dol Amroth) | Scene work only | The look is vanilla |
| B | Own leaves on a vanilla door skeleton, moved by the vanilla clips | Per gate: an FBX on the vanilla bone list, the damage meshes and the wreck, the `bo_` bodies, a material, a prefab copied from the vanilla tree; one Kit probe first | Whether the Kit registers a second skeleton of the vanilla name in another module, which could reach every vanilla gate of that family |
| C | Own skeleton, master animation and clip records | All of B plus the rig, the animation and the clips | High: the creature skeleton rules apply in full ([skeleton authoring](../bannerlord-skeleton-authoring.md)) |
| D | A static mesh of open leaves, no `CastleGate`; vanilla dressing of this form: `european_castle_gatehouse_portcullis_a`, a mesh and a body with no script (`MODS/Native/Prefabs/archhitecture_vlandia.xml:1151-1157`), placed on `vlandia_castle_c` level 3, siege and civilian (`scene.xscene:75219-75226`), about 190 m from that scene's gates | One mesh and one body | Not for a siege level (no AI gate, no ram target, no navmesh switch). The Osgiliath pattern below stands an invisible gate beside it; no mission has tested it |

## How TAOM uses gates today

- **Vanilla prefabs.** A scan of the live `TAOM_Map/SceneObj` on 2026-11-26 found a `CastleGate` in 28 scene folders
  (counting `wip_` and `temp_`). TAOM has no gate prefab of its own (`name="CastleGate"` in `TAOM_Map/Prefabs` and
  `Prefabs_Unused`: 0 hits). Seven scenes use `european_castle_gate_outer_l2`. Pelargir pairs it (MaxHitPoint
  25000) with `empire_castle_gate_inner_l3`, ids 463 and 464, which do not end in 1
  (`taom_gondor_town_pelargir_forceatmo/scene.xscene:3300-3319, 166084-166094`), both with no `<levels>`. The outer
  keeps the prefab's 461 (ends in 1) with 462 (`:3308-3311`, `props_siege_gates_outer.xml:4798`) and its middle
  position moved 5.781 m with no `_width` (`:3315-3317`); the inner's is default. Ids naming a face: UNVERIFIED.
- **The Dol Amroth re-dress.** The scene gives the inner gate `material="planks_skinned"` on its skinned door
  sub-meshes and `editor_gizmo` on the plank and states 2 to 5, scale 1.126, `MaxHitPoint 1.000`
  (`taom_gondor_town_dolamroth_forceatmo/scene.xscene:114221-114300`).
- **Non-uniform scale.** The same scan on 2026-11-26: 56 instances of Native's `CastleGate` prefabs, 15 scaled, one
  at 1.126 (Dol Amroth), seven non-uniform: `taom_dwarf_town_01_forceatmo:145950, 163051`;
  `taom_haterade_erebor_town_z:9404`; `taom_mordor_town_01_forceatmo:16080, 19168`;
  `wip_taom_gondor_castle_001:5941, 8094` (entity lines). Their run-time effect is not checked.
- **Inline trees.** Four scenes hold a copied gate tree with a broken prefab link:
  `taom_dale_castle_001_forceatmo:25688`, `taom_gondor_town_osgiliath_e_forceatmo:22096`,
  `taom_gondor_town_osgiliath_w_forceatmo:8495`, `temp_mission_scene:53336`. Osgiliath East and West and
  `temp_mission_scene` hold the same invisible outer gate: every clip name empty, `MaxHitPoint 1.000`,
  `NavigationMeshId 1111` (East `:22110-22133`, West `:8509-8533`, temp `:53350-53374`), `editor_gizmo` on its door
  meshes (`:22223-22224`, `:8622-8623`, `:53463-53464`). It gives the siege AI an outer gate; in Osgiliath East a
  static open door prop stands beside it; no mission has tested it in a siege.
- **Helm's Deep** has one visible inner gate with id 521 since 2026-11-18; the Long Defeat run `20261118-133313`
  saw the attackers break both gates ([long-defeat.md](../../features/long-defeat.md)).
- **An unused Minas Tirith gate** in `TAOM_Map/Assets/Scenes/Gondor/buildings/mt gate full_geo.tpac`: five state meshes,
  a top, bodies per state, and a source FBX rigged to `bn_root`, `bn_middle_l`, `bn_middle_r`; no skeleton, animation
  or clip item in any TAOM_Map package, and no scene uses it.
- **Code.** No TAOM Harmony patch targets `CastleGate`; `FordsTowerHitPatch` prefixes `DestructableComponent.OnHit` and
  passes other components through ([registry](../harmony-patch-registry.md)). The adapters find gates by type and tag:
  `LongDefeatBattlefieldAdapter.cs:149-169` breaks hidden gates, `LongDefeatBattlefieldAdapter.Gates.cs` sets gate
  levels and repairs a gate through `CastleGate.OnMissionReset` by reflection, `StagedSiegeMissionAdapter.cs:198-210`
  opens every gate, and `CreatureSiegeMissionAdapter.cs` and `CreatureSiegeAgentAdapter.cs` read gate state
  ([creature-siege-role.md](../../features/creature-siege-role.md)).
- The Rivendell gate set and its decisions: [rivendell-siege-set.md](../../kitbash/rivendell-siege-set.md).

## Not verified

- **In game:** the on-screen swing (native playback and blending; the keys are measured, the motion is not seen);
  that a siege scene with no inner gate throws (code reading and the mod's record; no mission has tested it); that
  the break clip is never seen; what Pelargir's inner ids do to the defenders; the Osgiliath gate's empty clip names
  (`GetAnimationIndexWithName` on a missing name is native); a non-uniform gate scale.
- **In the Kit:** whether a skinned mesh imported into a module other than Native, with a vanilla skeleton's name,
  makes a second skeleton item, and whether that item then serves every vanilla gate of that family; whether a
  scene's `<tags>` replace or add to a prefab's; the ram's reach in front of a gatehouse (a placed ram).
- **Native:** what `Scene.SetAbilityOfFacesWithId` and `Agent.SetAgentExcludeStateForFaceGroupId` do below the
  managed call; which faces vanilla gives each gate id or paints as castle-side (`navmesh.bin` not decoded); what the
  scene level system does to an entity outside the active level (hidden, removed or never made).
- **Not read:** the order of `MissionLogic.AfterStart` against `MissionObject.AfterMissionStart` for the civilian flag;
  what makes a material work on a skinned mesh (`planks_skinned`) and what `editor_gizmo` does at run time;
  `StonePile` mechanics for a rock hole; the callers of `hit_melee` (its keys equal the hit range); SandBox.View,
  TaleWorlds.MountAndBlade.View and the multiplayer DLLs for gate callers.
