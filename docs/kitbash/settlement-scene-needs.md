# Settlement scene needs for a kingdom kit

What this is: what the scenes of a TAOM town or castle need from a settlement kit (walls, broken walls, towers, the gatehouse and its two gates, stairs, the siege placement), with the measured numbers, the tags and scripts, the counts in real scenes, and where each fact was proven.
Who reads it: a kit session of any kingdom, once, from its handover; it stands alone, so the Rivendell build record is not needed to use it.
Version 2, 2026-11-26 19:05 CST (version 1, of about 18:30 to 18:40, is kept as `settlement-scene-needs_v1.md` in the appendix folder); Bannerlord v1.5.7, decompile cache `v1.5.7-202611101605`.

## How to read this file

Each fact carries a kind and a source. ENGINE: game code reads it (the decompiled file and line, `Type.cs:N`).
CONVENTION: the vanilla and TAOM scenes do it (the scene and the count, or the research file that counted it). DECISION:
the owner chose it for the kits (the record section); "DECISION (lead)" where the lead of the Rivendell job chose it and
the owner has not yet confirmed. RECOMMENDATION: the lead's advice for a new kit, not decided by the owner. UNVERIFIED:
not proven; what is missing is named, and section 15 gives the smallest step that would settle it.

A Rivendell number (a wall length, a deck height) is a DECISION for Rivendell and a pattern for other kingdoms, not an
engine need. Where this file says "inferred", the fact is reasoning from measured facts, not a measurement. Terms are in
the glossary (section 18). "The editor" in this file is always the Modding Kit's scene editor.

**Frames.** Four frames are in use, and every table of positions names its frame.

| Frame | Axes | Where it is used |
|---|---|---|
| SCENE | The engine's scene axes, Z up; also an entity's local axes as a scene file writes them. In the TAOM scenes a wall runs along X with the attackers at -Y | scene positions, wall markers, ladder placement |
| GATE | A vanilla gate prefab's own frame: +Y to the field, the leaves swing to -Y (the castle side) | leaf, hinge and swing tables; the ram's rest point |
| GATEHOUSE | A vanilla gatehouse prefab's own frame: +Y inside, about 180 degrees from its gates | the vanilla gatehouse table; the rock pile and targets |
| KIT | The kit's Blender frame: X along the piece, the field face at +Y, Z up, Z 0 the ground line | every Rivendell and Gondor control number (`rivendell_gatehouse_geometry.py` lines 4 to 5; `control_geometry.md` line 3) |

The engine reads a kit FBX turned: a file point (x, y, z) arrives as (-x, z, y) x 0.01, so the KIT field face lands at
SCENE -Y of the piece (REC "The plan for the first Modding Kit import"; "The wall is in the Modding Kit", the Kit's bounds
equal the FBX bounds as (-x, z, y) x 0.01). **A vanilla gate inside a kit gatehouse:** in Dol Amroth the vanilla gates
stand turned 179.85 degrees from the Gondor kit gatehouse (gatehouse -134.65 degrees, gate 45.2 degrees), the outer gate at
Y -4.21 and the inner at +4.16 of the gatehouse's SCENE frame, so the mesh's +Y points inside (`gatehouse_layout.md`
section 4, lines 244 to 261). So a vanilla gate (+Y to the field) stands turned 180 degrees from a kit gatehouse, as from
a vanilla one. For a Rivendell gatehouse in the Kit this is inferred, not seen (Part B 2.4; section 15, item 15).

**Short names of sources.** Full paths are in section 19.

| Short | What |
|---|---|
| SRC | The v1.5.7 decompile cache (`pwsh tools/taom-src.ps1 path <Type>` prints the file) |
| REC | [rivendell-siege-set.md](rivendell-siege-set.md), the Rivendell build record, cited by section heading. Its headings name the owner by first name where this file says "the owner"; search a heading by its date and topic words |
| Part A, B, C | The three research parts behind this file, kept as its appendix in `E:/LOTRAOMAssets/_auto_workspace/kingdom_kits/_shared/scene_needs/` (`part_A_wall_line.md`, `part_B_gatehouse_gates.md`, `part_C_scenes_kingdoms.md`). The long trees and tables are there |
| WIRING | `E:/LOTRAOMAssets/_auto_workspace/kingdom_kits/rivendell/wiring/` (`siege_wiring_spec.md`, `catapult_platform.md`, `gatehouse_layout.md`, `gate_leaf_size.md`, `prefab_trees_gates.md`, `prefab_trees_gatehouses.md`, `scene_tables.md`) |
| SURV | `E:/LOTRAOMAssets/_auto_workspace/kingdom_kits/rivendell/kit_full/delivery/PREFAB_SURVEY.md` |
| GATES | [castle-gates.md](../reference/engine/castle-gates.md) |
| SGS | [scene-gameentity-scriptcomponent.md](../reference/engine/scene-gameentity-scriptcomponent.md) |
| NATIVE | `Modules/Native/Prefabs/` of the game install (`props_siege.xml`, `spawners.xml`) |

**Scenes counted** (read only, `scene.xscene` of each): DA `taom_gondor_town_dolamroth_forceatmo` (TAOM_Map, a working
town siege); PE `taom_gondor_town_pelargir_forceatmo` (TAOM_Map, a town); GC1 `taom_gondor_castle_001_forceatmo`
(TAOM_Map, the working castle siege); BCA `battania_castle_a` (SandBoxCore; the Rivendell, Mirkwood and Lothlorien
castles load it); EC4 `empire_castle_004` (SandBoxCore; Mordor, Isengard and Dol Guldur castles load it).

## 1. The checklist for a handover

Copy this table whole into a kit handover. For each thing: what THE KIT delivers, what THE SCENE BUILDER adds in the
scene, and THE OWNER'S DECISION that is still owed. What Rivendell has built is in section 17 only.

| # | Thing | The kit delivers | The scene builder adds | The owner's decision owed | Detail |
|---|---|---|---|---|---|
| 1 | The centre scene of a settlement | Nothing in the scene itself; its prefab file and showcase scene (row 27) | One `center` scene per settlement, used by the visit and the siege of all three wall levels (ENGINE); a new name `taom_<kingdom>_<kind>_<name>_forceatmo` | The `scene_name` edit in the live `settlements.xml` is his step; a new scene against an edit of a shared vanilla scene (row 28) | 2.1, 12.3 |
| 2 | Three wall levels in one scene file | One wall height and one gatehouse for all three levels (RECOMMENDATION) | A `<levels>` block on each siege entity; the pieces each level places | One height or three for this kingdom | 2.2, 2.4 |
| 3 | The solid wall | Pieces that join with no seam; deck 12.0 m over the ground line (RECOMMENDATION, 2.4); a foundation under it; a `bo_` body; levels 0, `.lod3`, `.lod6`; the railing as a sub-object (rule in 3.4) | Nothing: a plain wall needs no script (ENGINE) | Lengths and look for the kingdom | 3 |
| 4 | Parapet parts | Each part its own mesh `_mN` and body; in the prefab each part has a `DestructableComponent` and a `debris_holder` child tagged `reference` | `tower_merlon` on the parts where a siege tower's ramp lands | | 1.1, 3.1, 3.4 |
| 5 | The broken wall | A separate piece of each breakable length: gap about 12 m at the deck, a mound that troops walk (path 35 degrees or less, body ramp 30 degrees or less) | | Its look | 4.4 |
| 6 | The breach wall tree | In the prefab: three `WallSegment` scripts (root, solid child, broken child), the meshes, bodies and parts (16 entities for a 20 m wall with six parts) | Root tag, `SideTag`, the nine navmesh ids, the `solid_child` and `broken_child` tags, `visible="false"` on the broken child, the markers | Who builds the siege test prefab, and in which file: no record holds it | 1.1, 4.1 |
| 7 | Two sided breach walls a level, and a ram | | Exactly two walls with `SideTag` `left` and `right` and one ram in each siege level: one or three throw at set-up (ENGINE) | | 4.2 |
| 8 | Position markers | | `middle_pos`, `wait_pos` under both children; `attacker_wait_pos` under the broken child | | 4.3 |
| 9 | The wall tower | Mesh and body; doors on two sides so that the walk passes through; a platform for one machine's footprint (row 13); the interior and the stairs as their own entities | The machine slot and archer points on it | | 5.1 |
| 10 | The corner tower | As row 9, with two joint planes at a right angle | As row 9 | | 5.1 |
| 11 | The frontage chain | Drop-in pieces that join in any order | The order along each front | The chain for this kingdom (Rivendell's is decided) | 5.3 |
| 12 | Stairs to the walk and inside towers | A stair is its own mesh and body with the `has_steps` flag (ENGINE flag) | The flag, where a kit did not ship it | | 5.2 |
| 13 | Defender machine platforms | A level deck for the larger footprint of the weapons under one point: 5.50 x 5.72 m for a mangonel with crew and pile; no body in the 120 degree arc to the field | Four `DeploymentPoint` slots with `Side=Defender` a level, each with a `mangonel_b_spawner` and a `ballista_b_spawner` | | 8.1 |
| 14 | Archer positions and ammo | Archer windows and standing room behind them | Entities tagged `archer_position`, `strategic_archer_point`, `arrow_barrel` | | 8.3 |
| 15 | Ladder landings | A deck that the 14 m ladder serves (10.1 to 13.6 m, median 11.8) | `siege_ladder_<n>m_spawner` with `TargetWallSegmentTag`, `SideTag`, `OnWallNavMeshId`, barriers; 4 a level | | 6.1 |
| 16 | The siege tower's landing | A deck that the 12 m tower serves (hinge 11.90 m) | `siege_tower_<n>m_spawner` with a `<path>`, `SideTag`; `tower_merlon` parts; 2 a level | | 6.2 |
| 17 | The gatehouse | Room for an outer and an inner gate (vanilla gaps 7.15 to 9.81 m); passages, swing room, a flush deck with a rock hole over the room between the gates; merlons as entities | The gates, the rock pile, its targets and volume box | One gatehouse for three levels or three (RECOMMENDATION: one) | 7.3 |
| 18 | The outer gate | An opening that fits the vanilla leaves (4.70 m clear, 6.9 m high for the european l2 door) | A vanilla gate prefab (route A), tagged `outer_gate`, `SideTag middle`, face ids, `CanBeDestroyedInitially` false | Route B (own leaves) is the lead's choice, not confirmed (GD1) | 7.1, 7.2, 7.4 |
| 19 | The inner gate | An inner opening (4.52 m for the empire inner l3) with its floor set for the frame | A gate tagged `inner_gate`, visible; one in each siege level (ENGINE) | | 7.1 |
| 20 | Navmesh face ids | Nothing: the kit carries no navmesh | Castle-side faces whose ids end in 1; ladder landings never | | 3.1, 7.1 |
| 21 | Gatehouse deck and rock hole | The hole; a deck at most about 14 m over the passage targets | `throwable_rock_pile`, `murder_hole_target`, the volume box | | 7.3, 8.2 |
| 22 | The ram's approach | Open ground for at least 6.25 m to the field of the outer gate | `batteringram_a_spawner` with `GateTag outer_gate`, `SideTag middle`, a path | | 7.5 |
| 23 | The attackers' zone | Nothing; the field in front of the wall line stays open | Deployment areas, boundaries, machines, archer points | | 9 |
| 24 | Spawn sets and the scene navmesh | | One `battle_set` in a castle, two in a town (CONVENTION); a hand made `navmesh.bin` | | 2.3 |
| 25 | The civilian layer | The streets and houses (not covered here) | `spawnpoint_player` and the spawn tags of the visit | | 10 |
| 26 | The keep, the other interiors, the villages | Interior pieces, if the kingdom gets its own | Three keep scenes (one a wall level) with `FightAreaMarker` and `defender_*` points; tavern, arena, dungeon, houses, `village_center`, each its own scene | Whether the kingdom gets its own interiors | 10 |
| 27 | The kit's prefab file and showcase scene | `taom_<kingdom>_kitbash.xml` in `TAOM_Map/Prefabs`, written after the import with the Kit closed; the meshes in `AssetSources`; a count of its entities against the cap (6,193 spare on 2026-11-26) | The showcase scene `mod_taom_<kingdom>_kitbash` is made in the Kit | His word for the prefab file's copy and the Kit clicks | 11.1, 11.2 |
| 28 | A new scene or an edit of a shared vanilla scene | | | Every elven centre scene is shared with other cultures: his choice | 11.3 |
| 29 | A defence that is not masonry | Any look, with the engine's parts of rows 3 to 22 | As rows 6 to 24 | The look for a kingdom of trees, hedges or banks | 16 |

### 1.1 What a prefab holds and what a scene adds (SURV 3.5, 3.6)

CONVENTION, read from the Gondor control prefab `Lotraom_comprehend_Gondor_Wall_2_Complete.xml` and the DA scene (SURV
3.2, 3.3). No prefab file of Native, SandBox, TAOM_Map or NavalDLC holds `solid_child`, `broken_child` or
`wall_segment_left`/`_right` (SURV 3.2). The scene adds every siege tag.

| Wiring | In the prefab | Added in the scene | Evidence (SURV) |
|---|---|---|---|
| `WallSegment` on root, solid and broken child (ids `-1`, `SideTag` empty) | yes | values changed | prefab lines 8, 34, 258 |
| Solid mesh and body; broken mesh and body | yes | material overrides | prefab lines 29 to 31, 253 to 255 |
| Parapet parts under the solid child, with `DestructableComponent` and a `debris_holder` tagged `reference` | yes | | prefab lines 51 to 240 |
| Rail part with mesh and body only | yes (the control) | | prefab lines 241 to 246 |
| `record_to_scene_replay`; `mobility="1"` on parts and holders | yes | | prefab lines 3, 51, 81 |
| Root tag `wall_segment_left` or `_right`; `SideTag`; navmesh ids | no | yes | scene lines 127338 to 127355 |
| `solid_child`, `broken_child`; `visible="false"` on the broken child | no | yes | scene lines 127371, 127681, 127686 |
| `tower_merlon` on the parts where a tower lands | no | yes | scene line 127463 |
| `middle_pos`, `wait_pos` (both children), `attacker_wait_pos` (broken child) | no | yes | scene lines 127648 to 127770 |
| `mesh_bender` on the root | no | yes | DA's left wall root: `Bend Amount` -5.0, `Bend Center Ratio` 0.5 (SURV lines 399 to 404) |
| Ladder and tower spawners, gate, ram, archer points, deployment points | no | yes, prefab references | SURV 3.3 |
| `has_steps` | Gondor kitbash: no; Erebor kitbash and other prefabs: yes | yes | SURV 1.1, 3.3 |

`NavMeshPrefabName` is `value=""` on every `WallSegment` and `DestructableComponent` that SURV quotes, prefab and scene
(SURV lines 107, 313, 347, 396). What the engine does with a non-empty one: not read.

**The 16 entities of a 20 m breach wall with six parapet parts** (SURV 3.6, the control's form): root (`WallSegment`) >
solid child (mesh, body, `WallSegment`) > six parts `_m1` to `_m6` (mesh, body, the control's `DestructableComponent`)
each with its `debris_holder` tagged `reference`, and the rail `_m7` (mesh and body); root > broken child (mesh and body
`_dest`, `WallSegment`). That is 1 + 1 + 6 + 6 + 1 + 1. Nothing smaller is correct: the engine calls the scripts of both
children; the parts must be children of the solid child so that they go with it on a breach; a part can be hidden for a
siege tower only with a `SynchedMissionObject` script, and `DestructableComponent` is one
(`public class DestructableComponent : SynchedMissionObject`, `DestructableComponent.cs:14`); a part that sets
`ReferenceEntityTag reference` needs its `reference` child. With the rail joined to the wall as a sub-object (3.4), the
tree has 15 entities (inferred).

## 2. The scenes of a settlement

### 2.1 Which scene each location loads (Part C 1.1, 1.2)

The live `TAOM_Map/ModuleData/settlements.xml` (read only) holds 280 `center` locations (100 towns, 180 castles), 280
`lordshall`, 280 `prison`, 100 each of `arena`, `tavern`, `house_1` to `_3` and `alley` (towns; `alley` names no scene),
804 `village_center`, 181 `hideout_center`, 1 `retirement_retreat` (`fc_settlements.py`, run again 2026-11-26 19:00).
All 280 centres name one scene in all four slots. A castle has only `center`, `lordshall` and `prison`. CONVENTION.

ENGINE (Part C 1.1): `Settlement.cs:1020-1026` reads `scene_name` and `scene_name_1` to `_3` into four slots;
`Location.GetSceneName(upgradeLevel)` returns that level's slot, else `scene_name` (`Location.cs:277-285`); the level is
the fortification building's level (`Town.cs:659-676`); a village asks for slot 1 (`VillageEncounter.cs:19`). There is
no separate siege scene: the siege loads the same `center` scene. Vanilla names that hold "siege"
(`sturgia_castle_siege_001`, `empire_siege_001`) are ordinary centres.

Which mission passes which scene levels (ENGINE, Part C 1.2, where the full table and its lines are): a visit of the
centre `civilian level_N` (`DefaultLocationModel.cs:29-53`); the siege and a sally out `level_N siege`
(`SandBoxMissions.cs:791-792`; a sally asks for the spawn set `sally_out_set`, `:800`); the siege with no deployment
always `level_3 siege`; the fight in the lord's hall `siege`, in the `lordshall` scene of the wall level; an indoor visit
`civilian level_N`; the arena `""`; a prison break `prison_break` with the spawn tag `sp_prison_break`. So the centre
scene of a town or castle is six scenes in one file: the visit and the siege of each of three wall levels.

### 2.2 How a level changes the walls and gates (Part C 1.3, 4.2)

Each `scene.xscene` declares its levels in a header (`battania_castle_a/scene.xscene:4-11`: `base` 1, `level_1` 2,
`level_2` 4, `level_3` 8, `siege` 16, `civilian` 32). The masks differ between files (`battania_castle_b` swaps `siege`
and `civilian`); entities name levels, not masks. CONVENTION.

**The `sally` level.** `battania_town_a/scene.xscene` line 11 declares `<level name="sally" mask="64"/>`, and 932 of its
lines hold the word `sally` (grep of 2026-11-26 19:00). The only managed constant of that name is unused:
`BannerlordMissions.cs:34`, `private const string SallyOutTag = "sally";`, and no other line of that file names it; the
sandbox siege and sally pass `level_N siege` (2.1). So no managed code asks for `sally`; a new town scene does not need it
as far as managed code goes. Whether native code reads it: UNVERIFIED.

| Entity | Levels it lists (CONVENTION, all six elven vanilla scenes) | Effect |
|---|---|---|
| Wall roots, gates, gatehouse shells | one `level_N` with `siege` and `civilian` | a different wall pair, gate pair and gatehouse per wall level, the same in visit and siege |
| Ladders, siege towers, the ram, defender `DeploymentPoint`, `mangonel_b_spawner`, `ballista_b_spawner` | one `level_N` with `siege` | only in the siege of that level |
| Attacker `DeploymentPoint`, `trebuchet_a_spawner`, `mangonel_a_spawner`, `ballista_a_spawner`, the `battle_set` entity | `level_1, level_2, level_3, siege` | in every siege |

What the engine does with an entity outside the asked levels (hidden, removed or never made) is native and UNVERIFIED.
In GC1 the two walls and the outer gate list no `<levels>`; its only inner gate (`aserai_castle_gate_inner_l1`, scene line
31109) lists `level_1` to `level_3` and `siege`, is hidden and has face id -1; its siege works (WIRING
`siege_wiring_spec.md` section 5; `fc_levels.py` run again 2026-11-26 19:00).

### 2.3 What the siege reads from the centre scene, besides the wall line (Part C 1.4, 4.1)

- **Spawn set** (ENGINE, `BattleSpawnLogic.cs:21-40`): the mission finds the entity tagged with the set it asks for
  (`battle_set`, `sally_out_set`, `relief_force_attack_set`); when it finds it, it removes every other entity tagged
  `spawnpoint_set`; when it does not, it removes nothing. CONVENTION: `battle_set` 1 in each castle, 2 in each town;
  `sally_out_set` and `relief_force_attack_set` 0 in all six elven scenes, which hold `sally_out_ambush_battle_set` 1.
- **Navmesh** (CONVENTION): all six elven scenes have `nav_mesh_auto_generated_ false` and a `navmesh.bin` of 2,584,565
  to 6,883,977 bytes. A gate id that names no face gives a shut door that blocks nothing (GATES, "What a scene must set").
- **A missing scene folder crashes the game when the scene loads**: the claim of
  [scene-reference-audit.md](../reference/scene-reference-audit.md); no engine line read (UNVERIFIED).

**Per level in all six elven vanilla scenes** (CONVENTION, Part C 4.1, script `siege_by_level.py`): 2 sided breach
walls, 1 outer and 1 inner `CastleGate`, 4 `SiegeLadderSpawner`, 2 `SiegeTowerSpawner`, 1 `BatteringRamSpawner`, 4
defender `DeploymentPoint`, 4 `mangonel_b_spawner`, 4 `ballista_b_spawner`, 2 `tower_merlon` tags, 1 `StonePile`, with
the exceptions that Part C 4.1 lists. In every siege, all levels: `trebuchet_a_spawner` 4, `mangonel_a_spawner` 4,
`ballista_a_spawner` 4, attacker `DeploymentPoint` 7 (exceptions, Part C 4.1: `battania_town_a` holds 13
attacker points in the file, 4 for all levels plus 3 a level, so 7 in one siege; `battania_town_b` holds 13 too, and 8
`mangonel_a_spawner`).

### 2.4 The wall height by level

**The vanilla fact** (CONVENTION, my count of 2026-11-26 19:00,
`grep -o 'prefab="battania_castle_wall[^"]*"' battania_town_a/scene.xscene | sort | uniq -c`): the Battania walls are
prefabs by level: `battania_castle_wall_a_l1` 16, `_l2` 13, `_l3` 14; `battania_castle_wall_c_l1` 10, `_l2` 12, `_l3` 19;
`battania_castle_wall_d_l1` 9, `_l2` 9, `_l3` 8; `battania_castle_wall_gate_l1` to `_l3` 1 each; and
`battania_castle_gatehouse_a_l1` to `_l3` 2 each (the same grep for `gatehouse`). The machine lengths grow with the level too (Part C 4.2): ladders on level 1 are 6 to 8 m, level 2 8 to 11 m, level 3
11 to 14 m; towers 5 to 9 m, 9 to 12 m, 12 m. The wall heights themselves were not measured (section 15, item 24).
GC1 shows that one wall line with no `<levels>` holds a working siege (2.2).

**The maintainer's decision for Rivendell** (DECISION, REC, the section of 2026-11-25 14:32 on the owner's three answers: the wall's height, the first
Kit import, Tripo for the broken wall): "We build these walls for ladders. the editor has I believe 8, 10, 12, and 14 foot
ladders. I think we dont need much more than 14 ft total. But check the height of the siege tower and ladders in the
editor or vanilla prefabs, etc or scene data. Then account for that. Anything more is wasted material." The measure that
followed (REC "The machines' heights, measured, and the wall's foundation"): the 14 m ladder serves vanilla decks of 10.1
to 13.6 m (median 11.8), the 12 m tower 9.4 to 14.6 m (median 11.4); the 11 m ladder is at its limit on a 12 m deck; the
8 m ladder needs the wall sunk 2 m or more; the 9 m tower needs it sunk about 3 m. Decision: the deck stays at 12.0 m
over the ground line, the foundation ends 4 m under it.

**RECOMMENDATION (the lead, 2026-11-26) for a new kit:** one wall height for all three levels in a kit's first version:
a 12 m deck, which the 14 m ladder and the 12 m siege tower serve, so every level of the scene uses those two machines.
The scene shows the levels by which pieces it places (the length of the line, the towers, which walls can break). The
same for the gatehouse: one piece for all three levels. Three heights are a later decision of the owner.

## 3. The solid wall (Part A1)

### 3.1 What the engine reads

| Fact | Source | Kind |
|---|---|---|
| No managed code reads a wall's height (the wall segment, ladder, tower and ram scripts, the assault behaviours, `SiegeLane`, `SiegeWeapon` were searched for z or a height) | REC "The machines' heights, measured"; heights study section 6 | ENGINE (negative, in that scope) |
| A plain wall needs no script: an unsided `WallSegment` gets `BehaviorSideNotSet` (`WallSegment.cs:85-99`); the set-up takes only walls with a side and a `broken_child` (`SiegeMissionPreparationHandler.cs:141-143`); the AI lanes take sides 0 to 2 only (`TeamAISiegeComponent.cs:83-91`) | SRC | ENGINE |
| Unsided `WallSegment` scripts are common and harmless: PE holds 127 for 2 sided walls | Part A count | CONVENTION |
| The parapet must be separate entities: the set-up loop over every `SynchedMissionObject` hides an entity tagged `tower_merlon` when the attackers have any siege tower (`SiegeMissionPreparationHandler.cs:107, 113-119`). `DestructableComponent` is a `SynchedMissionObject` (`DestructableComponent.cs:14`). A plain mesh entity has no script and is never hidden. It hides every `tower_merlon` of the scene, on both sides | SRC | ENGINE |
| Parapet parts can be destroyed before the assault: the same loop calls `PreDestroy()` on a `DestructableComponent` with `CanBeDestroyedInitially` true, by chance `num3 = Lerp(0, 0.7, 1 - mean wall hit point ratio)` (`:106, 120-124`) | SRC | ENGINE |
| The debris spawns at the reference entity: the entity itself when `ReferenceEntityTag` is empty, else its first child with that tag (`DestructableComponent.cs:173`; the instantiate at `:580`, SURV 3.4). Because the parts set `ReferenceEntityTag=reference`, each needs a child tagged `reference`, the `debris_holder` | SRC | ENGINE |
| A `StrategicArea` binds every destructible whose physics centre is within 3 m and goes inactive when one is destroyed | `StrategicArea.cs:153-179` | ENGINE |
| Face ids: a defender on a navmesh face whose id does not end in 1, or an attacker on a face whose id ends in 1, is refused a strategic area and a usable machine; any siege agent on a face that does not end in 1 is refused the inner gate's slots | `StrategicArea.cs:258-266`; `UsableMachine.cs:844-853`; `CastleGate.cs:695-700` | ENGINE |
| At the default Environment Detail a mesh with levels 0, 3, 6 shows level 0 to 30 m, `.lod3` from 30 m, `.lod6` from 130 m, measured to the surface of its bounding sphere | [static-mesh-lod-selection.md](../reference/engine/static-mesh-lod-selection.md) lines 12 to 37 | ENGINE (by that doc) |

### 3.2 What the real scenes do

- The deck stands about 12 m above the ground: DA walls at z 102.72 with the deck at 117.72 and ladder spawners at
  105.44 to 105.80 (SCENE frame); PE deck 12.8 m up. Each scene sinks the Gondor wall 2 to 3 m (REC "The control group").
  CONVENTION.
- The Gondor one-piece 20 m wall `gondor_castle_wall_20m_l3_a` and its `_dest` twin (`meta_mesh_component name=` count):
  DA 8 and 2, PE 60 and 2, GC1 0 and 0. GC1 builds its walls from the l1 wall's parts: 82 placements of
  `gondor_castle_wall_20m_l1_a_m1` (meta mesh and body, first at scene line 12273; my count of 2026-11-26 19:00); its
  breach walls hold 177 child entities each (WIRING `siege_wiring_spec.md` 0, 1a). CONVENTION.
- The Gondor control solid wall (`control_geometry.md`, KIT frame): 20.0 x 6.874 x 35.059 m, deck Z 15.0, 6.0 m clear
  between the parapets, parapet 2.704 m (field), rail 0.906 m (town), bodies 2,202 triangles, five merlons and a rail.
  CONVENTION.
- The Gondor rail opening for stairs is 4.000 m; 6 of 143 wall positions in 9 TAOM_Map scenes have stairs at it (REC
  "The stairs measure"). CONVENTION.

### 3.3 The owner's decisions for the Rivendell wall (a pattern for other kits)

| Item | Decision | Source (REC section) |
|---|---|---|
| Lengths | 13.333, 20.0, 26.667 m: a whole number of 2.222 m tiles (texel density 0.45 UV units a metre); no stub bays on towers or gatehouses | "Names and build constants"; "the owner's decision on the stubs" |
| Deck | Z 12.0 over the ground line Z 0, built for the 14 m ladder and the 12 m tower (2.4) | "The machines' heights, measured"; "the owner's three answers" |
| Foundation | Feet 4 m under the ground line (`Z_FOOT` -4.0): 16.0 m from the bottom to the deck, 18.4 m to the parapet top; the ground falls up to 3.76 m (median 1.30) under the owner's placements; margin to the worst 0.24 m | "The machines' heights, measured" |
| Walk | One flat plane at the deck; 4.27 m clear between parapet and railing; the body's deck one plane, length x 5.0 m | "Round 9, stage 1"; "Wall type B, massing round 2" |
| Parapet | One part to an archer window; windows 1.3 m wide; sill 0.95 m; the same window on every piece; no stop body in a window | "Decisions taken (2026-11-25)"; "Round 9, stage 1" |
| Railing | Town side, one opening for stairs in the middle of every piece, 5.33 m clear. The owner: "You need to make enough room for stairs to connect to your wall, so soldiers can get up." | "Decisions taken (2026-11-25)"; "the owner chooses the opening's form" |
| Levels and bodies | Levels 0, `.lod3`, `.lod6`; a `bo_<name>` body for each visual object, closed shells | "The pure code for detail levels and bodies" |

### 3.4 One rule for parts and railings, and the entity tree of a plain solid wall

**The rule** (DECISION, [kingdom-kit-layout.md](kingdom-kit-layout.md) rule 10; REC "The maintainer's words of about
15:47 and 15:50", KS1 and KS3): a piece that a scene builder places as one thing is one asset in the Kit; its parts are
sub-objects `<piece>.<part>` of one mesh, which the Kit joins by the prefix before the first dot. A part is its own mesh,
with a suffix such as `_m1`, only where the engine needs its own entity: a parapet part that carries `tower_merlon` or a
`DestructableComponent` (of a wall, a tower or a gatehouse), a tower's interior, and a stair with `has_steps`. A railing
joins its piece as a sub-object. The owner, 2026-11-26: "These should be made all together, no one is going to need
stair railings. So stairs come with the railings."

Entity tree of a plain solid wall in a scene (CONVENTION, SURV 3.2; with the rule above):

| Entity | Script | Tags | Children |
|---|---|---|---|
| Wall piece (mesh with its railing sub-object, body `bo_<mesh>`) | none needed | none | parapet parts where a tower can land or merlons should break |
| Parapet part `_mN` (mesh, own body) | `DestructableComponent` (Gondor control: `DestructionStates=debris_vlandia_l3`, `DestroyedByStoneOnly=true`, `CanBeDestroyedInitially=true`, `MaxHitPoint=1`, `ReferenceEntityTag=reference`) | `tower_merlon` where a tower's ramp lands (scene) | `debris_holder`, tag `reference` |

## 4. The broken wall (Part A2)

### 4.1 The breach wall tree (ENGINE where bold)

| Entity | Script | Tags | Children | Set where |
|---|---|---|---|---|
| **Root** | **`WallSegment`**: nine navmesh ids, **`SideTag` `left` or `right`** (`middle` legal), `NavMeshPrefabName` (empty) | the tag that its ladders and tower name (Gondor `wall_segment_left`, `_right`; BCA `right_wall_lvl3`) | the two children | script in the prefab; tag, side and ids in the scene (1.1) |
| **Solid child** | **`WallSegment`** (ids -1, no side) | **`solid_child`** | solid mesh and body, parapet parts, **`middle_pos`** and **`wait_pos`** markers; vanilla adds `strategic_archer_point` | tag in the scene |
| **Broken child** | **`WallSegment`** (ids -1, no side) | **`broken_child`**, `visible="false"` | broken mesh and body, **`middle_pos`**, **`wait_pos`**, **`attacker_wait_pos`**; vanilla adds archer points, a plank ramp, barricades | tag and visibility in the scene |

What the code does with it (ENGINE, `WallSegment.cs`):

- `OnInit` reads the first `middle_pos` and `wait_pos` under the solid child (else the root) into `MiddleFrame` and
  `DefenseWaitFrame` (`:100-136`).
- `OnChooseUsedWallSegment` takes the `solid_child` and `broken_child` and calls `GetFirstScriptOfType<WallSegment>()` on
  each with no null check, so both children need the script (`:143-151`).
- On a breach the solid child is hidden, the broken child shown, the navmesh switches run, the strategic areas swap, the
  frames are read again under the broken child and its `attacker_wait_pos` becomes `AttackerWaitFrame` (`:150-245`);
  with no breach the reverse (`:247-279`).

### 4.2 How many walls, and which one breaks (ENGINE)

- `WallSectionCount` is 2 for a fortification (`Settlement.cs:149-158`); the campaign passes that list of hit point
  ratios to the mission (`PlayerSiege.cs:172`).
- The set-up collects the walls in `ActiveMissionObjects` that have a side and a direct child tagged `broken_child`
  (`SiegeMissionPreparationHandler.cs:141-143`), then for each of the 2 ratios calls `FindRightMostWall` and removes the
  wall it returns (`:145-158`).
- `FindRightMostWall` (`:183-199`): one wall returns itself; two are told apart by a cross product against the first
  `BatteringRam` (`.First()` throws when there is none); any other count returns `null`, and the next line calls a method
  on it. So one sided wall (the second call finds none), three, or four fail; exactly two and a ram work.
- A wall is breached only when its ratio is under 1e-5 (`:148-151`).
- When `num3 >= 0.1` (3.1), every primary siege weapon whose side equals a breached wall's side is disabled
  (`:163-180`).
- The repo gate `python tools/check_siege_wall_scripts.py <scene.xscene>` fails only an odd count (WIRING
  `siege_wiring_spec.md` 6; SGS "Two walls and a ram"); the engine wants two.

CONVENTION (solid and broken child tags): DA 2 and 2, PE 2 and 2, GC1 2 and 2, BCA 6 and 6, EC4 6 and 6 (vanilla holds
two walls for each of three levels in one file).

**The AI lanes** (ENGINE): the siege AI builds Left, Middle and Right lanes; each takes the primary siege weapons of its
side and the walls and gates whose `DefenseSide` is that side (`TeamAISiegeComponent.cs:52-91`). Gates carry
`SideTag=middle`, so the middle lane is the gate lane. A lane with a breached wall is a breach (`SiegeLane.cs:333-347`).
Ladders and towers name their side by their own `SideTag` (`SiegeLadder.cs:337-350`, `SiegeTower.cs:738-751`), so on
each side the wall, its ladders and its tower carry the same `SideTag` (pairing CONVENTION). A ladder or tower with no
`SideTag` joins the middle (gate) lane (`default: WeaponSide = Middle`, `SiegeLadder.cs:348-350`,
`SiegeTower.cs:749-751`), while a wall with no side is left out (`WallSegment.cs:96-98`).

### 4.3 Markers and navmesh ids

Markers (CONVENTION, DA, SCENE frame local to the wall's child, the Gondor deck at child-local z 15): solid `middle_pos`
(-1.758, -1.568, 15.000) on the deck; solid `wait_pos` (-0.712, 12.735, 2.880) inside the town; broken `wait_pos`
(1.759, 23.429, 2.670); broken `middle_pos` (-0.027, 6.545, 2.880) on the town side of the mound; broken
`attacker_wait_pos` (-8.287, -56.651, 0.780), 57 m out. Vanilla markers carry a `TacticalPosition` script (`_width` 8.0
or 5.0); TAOM markers carry tags only. What a null `MiddlePosition` does to the AI: UNVERIFIED.

The nine root ids drive face switches on a breach; an id of -1 or 0 skips its step (`WallSegment.cs:152-197`, ENGINE).
Vanilla BCA right: 328, 391, 342, -1, 371, 381, 341, 361, 351, in the order of the fields (glossary). GC1 uses 100 (left) and 200 (right)
for eight, with 122 and 222 on solid; the Gondor prefab -1 everywhere (WIRING `siege_wiring_spec.md` 1a, 1b, 2; SURV
3.2, 3.3). No TAOM scene shows the under and over debris switch (REC "Not checked").

### 4.4 The rubble mound: a walk for the attackers

| Item | Number | Kind, source |
|---|---|---|
| Gondor control `_dest` (KIT frame) | 20.0 x 19.343 x 35.631 m; gap at the deck 12.5 m (top hit test) or 11.5 m (thickness test); rubble 6.15 m and 6.6 m past the wall faces; 19 % of the rubble under 35 degrees; body 2,662 triangles | CONVENTION, `control_geometry.md`; REC "The control group" |
| Rivendell gap | 12.0 to 12.8 m at the deck; troops cross the crest at Z 4.3; end planes equal the solid wall's, vertex for vertex | DECISION, REC "The broken wall: its design" |
| Walk path | steepest node 35 degrees or less; 97 % or more of the path nodes under 32 degrees; largest step 0.10 m in 0.1 m; walkable share by area 19.3 % or more | DECISION, REC "The broken wall's mound module" |
| Body ramp on the path | 30 degrees at most, steps 0.06 m at most, never more than 0.02 m in front of the visual | DECISION, REC "The broken wall's mound module" |
| Stub body; loose blocks | the body's deck at most 0.35 m short of the visible edge (REC "The night"); loose blocks have no body, lie on 40 degrees or less, off the path (REC "The broken wall: its design") | DECISION |

The breach needs no body flag (no body of the control wall carries one, SURV 3.3). Vanilla broken children (BCA
`right_wall_l3`) hold a plank ramp, three `siege_barricade_a` with a `DestructableComponent` (`MaxHitPoint=200`,
`CanBeDestroyedInitially=false`) and 7 archer points. CONVENTION. The least breach gap and walk width that the AI takes
are not in our records (section 16).

## 5. Towers, stairs and the order of the front (Part A3)

### 5.1 Towers

- A tower is not a script: the Gondor catapult tower prefab `Lotraom_comprehend_Siege_9` has no script, tag or flag on
  its root; its eight merlons have the `DestructableComponent` and no `tower_merlon` (`catapult_platform.md` B1).
  CONVENTION. Its machines are scene entities (section 8).
- Gondor `gondor_castle_wall_tower_l1_b` (catapult tower, `control_tower.md`, KIT frame): 16.000 x 8.400 m, deck Z 10.0,
  largest clear rectangle 11.2 x 5.4 m, joins the walk over its whole flat side (14.4 m clear, no step); in DA its deck is
  level with the wall deck. `gondor_castle_wall_tower_l3_a` (stair tower): 10.82 m square, doors 0.88 x 2.34 m, a
  separate `_int` with `has_steps`. CONVENTION.

The owner's decisions (DECISION): the frontage must hold four catapults for the defenders, and towers that split up the
walls carry them; towers have levels, interior stairs and a door on two sides so that the walk passes through, the
catapult platform is the top level, doorways 1.4 m wide (REC "Decisions taken (2026-11-24)"); windows: "I'd open these up
as windows for archers", and to the town "so that people can shoot down incase they get through the gate and into a
courtyard", with a standing place at least 0.8 m deep and 2.0 m high behind (REC "the owner on tower a of version 7");
piece length 13.333 m (REC "The towers of version 7").

The Rivendell tower a plan (DECISION, KIT frame, REC "Tower a built at level 0", "Port step 3"): floors Z 0, 6, 12 and
the platform Z 17; the deck runs flush through both side doors, 1.4 m clear, ground door 1.6 m; every walked floor one
plane; the 5.50 x 5.72 m catapult footprint fits the platform.

**A gap (inferred, Part A3):** the DA tower mangonels put their pile and eight ammo points on the neighbouring wall deck.
A platform above the walk must hold the pile and its points on the platform, and the crew reach it only by the tower
stairs. A standing point must be within 1.5 m of the agent's height (`UsableMachine.cs:236-237, 261`, by the mangonel
research). Not tested.

### 5.2 Stairs

- `HasSteps = 0x800u` (`TaleWorlds.Engine.BodyFlags.cs:23`), written as
  `<physics shape="bo_..."><body_flags><body_flag name="has_steps"/></body_flags>` (REC "What the engine needs"). One body
  has one set of flags, so a stair is its own mesh and `bo_` body in its own entity. ENGINE for the flag; what it does to
  an agent is native and UNVERIFIED.
- Counts of `has_steps`: DA 0, PE 12, GC1 0, BCA 3, EC4 28. The Gondor kitbash prefab of a tower interior has no flag, so
  the scene builder adds it each time (SURV 3.3). CONVENTION.
- Vanilla gatehouse prefabs hold a `_stairs` child with its own body (`prefab_trees_gatehouses.md`). CONVENTION.
- A stair that comes with its railings is one mesh with one body that carries `has_steps` (3.4); whether the railing's
  collision may share that flag is UNVERIFIED (REC "The maintainer's words of about 15:47 and 15:50").
- Rivendell pattern (DECISION, REC "Kit step 1 built"): `rivendell_castle_stair_12m_a`, 48 risers of 0.25 m, treads
  0.28 m, 5.450 m wide; tower risers 0.25 m, goings 0.28 m, headroom 2.45 m. Gatehouse a has no stairs: "the wall pieces
  beside the gatehouse carry the stairs" (builder docstring, Part B row 7).

### 5.3 The frontage chain

DECISION for Rivendell (REC "Decisions taken (2026-11-24)"): **Tower, Wall (solid or broken), Wall, Tower, Gatehouse,
Tower, Wall, Wall (solid or broken), Tower.** Four towers, one catapult each. "Every piece is a separate drop-in piece, the
wall joins the gatehouse with no seam, and the solid wall and the broken wall are separate pieces, so that a scene
designer can set like pieces side by side and then work out the gate."

CONVENTION (the real fronts, `catapult_platform.md` B3; t is metres along the line from the gatehouse):

| Scene | Order | Defender points at t |
|---|---|---|
| DA | redoubt, breach wall (-48.3), tower (-39.6), solid wall (-29.6), gatehouse (0), solid wall (29.3), tower (40.6), breach wall (47.8), redoubt | -70.3, -39.7, +41.0, +70.6 |
| PE | solid walls, breach wall (-78.0), walls, square towers beside the gate (-13.4, +13.2, no point), gatehouse, walls, breach wall (97.3), walls | -96.2, -40.9, +36.0, +71.1 |

Neither scene puts a tower at each end of two walls; PE has no tower in the line. DA's breach wall roots stand 96.1 m
apart, PE's 175.3 m (SCENE frame, horizontal).

## 6. The attackers' machines against the wall (Part A4)

### 6.1 Ladders

| Item | Value | Kind, source |
|---|---|---|
| Sizes | 6, 7, 8, 11, 14 (`siege_ladder_<n>m_spawner`); no 10 and no 12; only Native defines them | CONVENTION, NATIVE `spawners.xml` lines 2 to 70 |
| Mesh length; top at the default lean of 19 degrees | 7.216, 8.475, 11.304, 13.687, 15.070 m (one reader); tops 6.82, 8.01, 10.69, 12.94, 14.25 m over the foot, the deck about 0.95 m below the top | CONVENTION, heights study 3 |
| The number in the name | feeds the ladder's AI queue (`num`, passed to `LadderQueueManager.Initialize`) | ENGINE, `SiegeLadder.cs:356-372` |
| Decks served | 14: vanilla 10.1 to 13.6 m, median 11.8 (152 placements), TAOM 11.3 to 12.8; 11: vanilla median 10.3, at its limit on a 12 m deck | CONVENTION, REC "The machines' heights, measured" |
| Lean | set by the scene (`UpperStateRotationDegree`); 148 of 152 vanilla spawners set their own, 13 to 40 degrees | CONVENTION |
| Its wall | the first entity tagged `TargetWallSegmentTag` that has a `WallSegment` | ENGINE, `SiegeLadder.cs:327-335` |
| Its side | `SideTag`; none means the middle lane (4.2) | ENGINE, `SiegeLadder.cs:337-350` |
| Landing faces | `OnWallNavMeshId`: unique per ladder in a level, never -1 or 0, never ending in 1 (editor warnings) | ENGINE, `SiegeLadderSpawner.cs:129-149` |
| AI barriers; merlons that must not break | entities tagged `BarrierTagToRemove`, shown or hidden with the ladder state; `IndestructibleMerlonsTag`: each tagged `DestructableComponent` disabled | ENGINE, `SiegeLadder.cs:264-273, 544-550` |
| How many | the editor warns, on the `left` wall only (in a single player scene), when its level does not hold exactly 4 ladder spawners | ENGINE (editor), `WallSegment.cs:290-306` |
| Counts | DA 4 x 14 m; PE 4 x 14 m; GC1 4 x 8 m; BCA 12; EC4 12 | CONVENTION |
| Placement | two per wall at wall-local x about -5.5 and +5.7, 5.6 to 7.2 m outside (GC1, SCENE frame local to the wall); feet 8.7 to 10.6 m from the wall root (DA), 9.9 m (PE); barriers `_barrier_ai_04x04m` on the walk | CONVENTION, `siege_wiring_spec.md` 4 |

What the engine does with a ladder head under a high parapet: UNVERIFIED (section 15, item 6).

### 6.2 The siege tower

| Item | Value | Kind, source |
|---|---|---|
| Sizes | 5, 9, 12 (`siege_tower_<n>m_spawner`) | CONVENTION, NATIVE `spawners.xml` lines 87 to 123 |
| Ramp hinge; open ramp at 80 degrees | hinge 5.072, 8.485, 11.900 m over its ground; tip 5.52, 9.21, 12.63 m; reach 2.54, 4.13, 4.13 m | CONVENTION, heights study 4 |
| Decks served | 12: vanilla 9.4 to 14.6 m, median 11.4; 124 of 152 vanilla towers set the ramp angle over 90 degrees | CONVENTION, REC "The machines' heights, measured" |
| Footprint (12 m) | about 6.8 x 6.8 m; top 13.394 m | CONVENTION, heights study 4 |
| Its wall | the first entity with `TargetWallSegmentTag` that has a `WallSegment` | ENGINE, `SiegeTower.cs:730-737` |
| Its side | `SideTag`; none means the middle lane | ENGINE, `SiegeTower.cs:738-751` |
| Its path | `PathEntityName` names a scene `<path>` (GC1: 7 to 9 points, ending 4 to 12 m outside the wall) | ENGINE field `SiegeTowerSpawner.cs:23`; CONVENTION |
| AI barriers | `BarrierTagToRemove`, hidden when in place, shown again when destroyed | ENGINE, `SiegeTower.cs:599, 920-923, 1007-1010` |
| The `breakable_wall` swap | the tower takes the nearest entity tagged `breakable_wall` and its `destroyed` and `non_destroyed` children; used by 0 TAOM scenes and 1 of 206 SandBoxCore scenes (`aserai_town_e`) | ENGINE `SiegeTower.cs:610-636`; CONVENTION |
| Counts | 2 per level: DA 2 x 12 m, PE 2 x 12 m, GC1 2 x 9 m, BCA 6, EC4 6 | CONVENTION |

Rule that follows (inferred): tag as `tower_merlon` the parapet parts that a tower's ramp meets, one or two per wall
(DA 2, GC1 2, BCA 6 for 3 levels, EC4 8; PE 0). In PE a tower meets a standing parapet: a trap, not tested in game.

## 7. The gatehouse and the gates (Part B)

### 7.1 A gate is one entity with two scripts (ENGINE)

`CastleGate` and a `DestructableComponent` on one root (GATES "What a castle gate is"). The siege AI takes the first
`CastleGate` tagged `outer_gate` and the first tagged `inner_gate` from `ActiveMissionObjects`, with no visibility filter
(`TeamAISiegeComponent.cs:54-57`). Three readers use them with no null check (`TacticDefendCastle.cs:396`,
`BehaviorAssaultWalls.cs:122`, `MovementOrder.cs:902`, by GATES). A hidden gate never ticks (`CastleGate.cs:609-622`)
and so never opens (GATES). A siege starts every gate closed, with no null check on the door skeleton
(`CastleGate.cs:357-363`); a peaceful visit opens and disables every gate.

The fields, their code defaults and the vanilla values, and the children the code reads (`clean`, the colliders,
`state1` to `state4`, `open_inside`, `close_inside`, `close_outside`, `middle_position`, `gate_plank`,
`battering_ram_rest_point`) are in Part B 1.1 with their lines (`CastleGate.cs:13-121, 738-753, 874-916`). The ones a
scene builder meets (positions in the GATE frame):

| Field or child | Default | Vanilla | Note |
|---|---|---|---|
| `NavigationMeshId` | 1000 | prefabs 461, 462, 411, 555; scenes overwrite to 502 (outer) and 521 (inner) | the scene navmesh faces the gate switches |
| `NavigationMeshIdToDisableOnOpen` | -1 | scenes 512 (outer), 531 (inner) | faces on at start, off while open |
| `SideTag` | none | `middle` | sets the lane (`CastleGate.cs:236-250`) |
| `AutoOpen` | false | overwritten at mission start by `IsSallyOutBattle` | |
| `CanBeDestroyedInitially` (`DestructableComponent`) | true | false on every gate prefab | left true, campaign wall damage may break the gate before the assault |
| `MaxHitPoint` | | outer 12000 to 18000; inner 1000; PE outer 25000; DA inner 1 (a scene override, scene lines 114221 to 114231) | a scene that copies DA copies a one-hit inner gate; its effect in game: UNVERIFIED |
| `open_inside`, `close_inside` | | Y -1.5 | `StandingPoint`s |
| `close_outside` | | Y +1.0 | |
| `middle_position` | | Y -1.4, tags `middle_pos` and `wait_pos`, `TacticalPosition` | exactly one `wait_pos` child or an editor warning |
| `battering_ram_rest_point` | | Y +6.25 | no C# reads the name; the ram path ends by it |

**The face ids** (ENGINE rule): any siege agent on a face whose id does not end in 1 is refused every inner gate slot
(`CastleGate.cs:689-700`). Every castle-side face ends in 1; field faces and ladder landings never do. CONVENTION over
206 SandBoxCore scenes (`fc_count.py`): outer 502 with 512 in 212 instances, 501 with 511 in 9, 502 with 502 in 1;
inner 521 with 531 in 222. TAOM: PE inner 463, DA inner 465; neither ends in 1 (Part B 1.4, scene lines given there).

**Editor checks** (`CastleGate.cs:1066-1118`, warnings only, nothing stops a save): both tags on one gate; not exactly one
`wait_pos`; an outer gate's middle position between the two gates; no inner gate; a non-uniform scale (seven TAOM
instances are non-uniform, GATES).

### 7.2 Leaf sizes, the opening and the swing (GATE frame)

| Prefab | Role | Closed door w x h (m) | Hinges X (m) | Hit points |
|---|---|---|---|---|
| `european_castle_gate_outer_l2` | outer | 4.695 x 6.838 (the outer extent of two parts); leaf body 0.24 thick; agent blocker 0.82 | +-2.363 | 15000 |
| `european_castle_gate_outer_l1` | outer | 3.437 x 4.673 | not measured | 12000 |
| `empire_castle_gate_outer_l3` | outer | 5.321 x 5.921 | +-2.361 | 18000 |
| `battania_castle_gate_outer_l3` | outer | 3.763 x 4.972 | -1.836, +1.822 | 18000 |
| `empire_castle_gate_inner_l3` | inner | frame 4.521 wide, Z -1.80 to 5.77; leaves 3.47 x 3.82 | -1.6885, +1.6868 | 1000 |

Source: `gate_leaf_size.md` (stored bounds of the Native packages) and the hinge dumps of GATES; the other families are in
Part B 1.5. CONVENTION.

**The opening** (CONVENTION): an outer passage at least 4.70 m clear and 6.9 m high for the european l2 door, 0.65 m of
depth for the door and 0.82 m for the agent blockers; an inner passage at least 4.52 m wide with the floor set for a frame
that reaches 1.8 m below its origin. The TAOM scenes do less: DA and PE scale the Gondor gatehouse to passages of 4.58 and
4.50 m, and GC1 stands the same outer gate in a kit arch 4.56 m wide and 5.24 m high, so the leaves overlap the jambs
(Part B 1.5). How that looks: UNVERIFIED.

**The swing** (ENGINE (data, the clips), measured from the master animation keys, GATES "The swing"; `GATE_SWING.md`):
no position keys, the leaves only turn, both to -Y (the castle side); the extra colliders sit at `frame.origin - ...`,
always on -Y (`CastleGate.cs:482-488`, code).

| Family | Open over | End angles | Swept box from the gate origin (GATE frame) |
|---|---|---|---|
| european outer l2_l3 | 3.0 s | -87.44, +89.33 deg | X -2.85 to +2.85, Y -2.34 to +0.25; leaf top 6.822 m |
| empire inner l3 | 3.0 s | -89.93, +90.13 deg | X -1.76 to +1.77, Y -1.93 to -0.02; leaf top 5.765 m |
| empire outer l3 | 3.0 s | -83.42, +83.78 deg | Y to -2.54 |

A gatehouse needs, behind each gate's hinge line on the castle side, a floor and walls clear of the swept box and a vault
over the leaf top. The hit clip shakes at most 3.95 deg; the door does not open under the ram.

### 7.3 The gatehouse piece

| Part | Need | Kind, source |
|---|---|---|
| The passage | Open from field to courtyard, floor at the gate origins' height, wide and high enough at each gate (7.2) | CONVENTION |
| The room between the gates | Deep enough for both swings and the hole above: vanilla gate distances 7.15 to 9.81 m unscaled | CONVENTION, `gatehouse_layout.md` 2 (line 343) |
| The deck | One flush walking floor ("a floor that troops walk on is one flush surface, with no lip, ring or step", the owner 2026-11-25; on a ring that was not flush: "makes it one flushed floor"); the rock hole over the room between the gates | DECISION, REC "the owner on gatehouse a of version 7" |
| Stairs | Vanilla: a `_stairs` child with its own body; Rivendell: the wall pieces beside the gatehouse carry them | CONVENTION; DECISION |
| Merlons | Separate entities: `DestructableComponent`, `DestroyedByStoneOnly` true, `CanBeDestroyedInitially` true, `MaxHitPoint` 1, `DestructionStates debris_vlandia_l1`, a `debris_holder` tagged `reference`. Vanilla counts: Battania l1 21, l2 22, l3 14; Empire l1 32, l2 24, l3 24 | CONVENTION, `prefab_trees_gatehouses.md` |
| A `WallSegment` | All 8 vanilla gatehouse and wall gate prefabs carry one with no side and ids -1; the Gondor gatehouse has none. With no side and no `broken_child` it is left out of the breach choice (`SiegeMissionPreparationHandler.cs:142-143`); what else reads it: UNVERIFIED | CONVENTION |
| Reverb boxes | `reverb_gatehouse`, `reverb_stone_corridor`, `reverb_wood_interior` | CONVENTION, optional |
| Nothing of the gate inside | No prefab file in TAOM_Map or Native nests a prefab reference (`grep -c '<game_entity prefab="'` is 0); no standing point, pile, target or gate is inside a vanilla gatehouse prefab: the scene places every one | CONVENTION, SURV 182-183 |
| Levels | Vanilla places a gatehouse prefab for each level (2.4). A town may place two gatehouse shells a level, one of them with the gate pair (`battania_town_a`: `battania_castle_gatehouse_a_lN` at two places, one outer and one inner `CastleGate` a level). RECOMMENDATION for a new kit: one gatehouse piece for all three levels, placed once or twice | CONVENTION; RECOMMENDATION |

**Vanilla gatehouses as measured** (CONVENTION, `vanilla_gatehouse_frames.md`, GATEHOUSE frame, +Y inside):

| Scene, level | Gatehouse prefab | Outer gate Y | Inner gate Y | Y difference (m) | Rock pile (X, Y, Z) |
|---|---|---|---|---|---|
| BCA L1 | `battania_castle_gatehouse_a_l1` | -6.07 | +2.68 | 8.75 | (-4.86, 1.13, 6.22) |
| BCA L2 | `battania_castle_gatehouse_a_l2` | -4.34 | +2.81 | 7.15 | (-5.54, 0.43, 8.36) |
| BCA L3 | `battania_castle_gatehouse_a_l3` | -6.30 | +2.52 | 8.82 | (-5.45, -0.99, 11.66) |
| EC4 L2 | `empire_castle_gatehouse_a_l2` | -0.89 | +6.75 | 7.64 | (2.75, 7.22, 8.33), (-5.44, -0.86, 15.90) |
| EC4 L3 | `empire_castle_gatehouse_a_l3` | -1.83 | +7.99 | 9.82 | three piles, Z 6.95 to 18.37 |

The distances between the gates are within 0.1 m of the Y differences (`gatehouse_layout.md` line 131; 9.81 m for EC4
L3). A vanilla gatehouse origin lies 0.8 to 3.1 m off the midpoint of its gates. Part B 2.2 holds EC4 L1 (gates scaled)
and the targets.

**The Gondor control gatehouse** `gondor_castle_gatehouse_l1_a` (CONVENTION, `control_geometry.md` 55-76, KIT frame):
16.4 x 12.4 x 20.05 m, deck Z 15.0, passage 5.75 m wide to Z 7.75, room ceiling Z 9.24, a solid deck with no rock hole.
DA scales it 0.805; its two gates stand 8.37 and 8.19 m apart in its unscaled frame.

**The owner's decisions for gatehouses** (DECISION, REC "Decisions taken (2026-11-24)" and "(2026-11-25)"): "Every
gatehouse is large and deep enough to hold an outer gate and an inner gate, and has a hole through which defenders throw
rocks down"; the joint of gatehouse and wall is seamless; every piece is a separate drop-in piece; no rock piles in the
piece. The lead's gate decisions GD1 to GD8 (glossary) are not confirmed by the owner (REC "Gates: how the game does
them, the routes, and the decisions").

**The Rivendell gatehouse a plan** (DECISION, KIT frame: X across the passage, the outer face +Y, Z 0 the ground and the
passage floor, `rivendell_gatehouse_geometry.py` lines 4 to 5; Part B 2.4): 22.222 m wide (joint planes at X +-11.111),
12.4 m deep (Y -6.2 to 6.2); hall 8.4 x 9.2 m, vault 9.6 m; tunnels 4.7 m wide, jambs to 7.0 m, apex 10.0 m, each 1.6 m
deep; deck Z 12.0; rock hole X -2.9 to 2.9, Y -2.6 to -1.0, a shaft from Z 9.0 through the deck; curb 0.3 m thick and
0.45 m high. Gate placeholders put the outer gate at Y +4.2 (field side) and the inner at Y -4.2, 8.4 m apart.

### 7.4 The routes for a kit to get gates (Part B 3)

| Route | What it is | State |
|---|---|---|
| A | A vanilla gate prefab placed in the kit gatehouse by the scene | Used in every TAOM siege scene (56 gate instances in 28 TAOM scene folders, Part B census; matches GATES) |
| B | Own leaf meshes skinned to a vanilla door skeleton (all 7 bones of `european_castle_gate_outer_l2_l3_skeleton` in the dump's order, no leaf bones), moved by the vanilla clips | Probe files built and checked; **the Kit probe has not run**; the owner has not confirmed the route. The probe goes into a throwaway module, never `TAOM_Map` (GD4) |
| C | Own skeleton, animation and clips | Nothing; only for another motion (a portcullis) on the owner's word |
| D | A static mesh of open leaves, no `CastleGate` | Not for a siege level: no AI gate, no ram target, no navmesh switch |

The tree musts of a custom gate (ENGINE, GATES "What a custom gate must keep"): `CanBeDestroyedInitially` false; both
door bone names set; the clean door tagged `operational` and, first, `reference`; every `gate` child with a `state_N` tag
and a `SynchedMissionObject`; no door collider on a wreck without a skeleton; the door entity at identity under the root.

### 7.5 The ram (ENGINE)

- `BatteringRamSpawner` passes its `GateTag` to the ram; the ram takes the first entity with that tag that has a
  `CastleGate` (`BatteringRam.cs:236-243`).
- The damage falls when the swing clip passes a fixed point, not by contact (GATES step 11, `BatteringRam.cs:442-473`);
  `DamageMultiplier` 300 in the prefab; a blow of 200 or more plays the hit clip (`HeavyBlowDamageLimit`,
  `CastleGate.cs:49`). The ram's formation goes to the inner gate once the outer is open or broken.
- The wall set-up needs a ram in the mission (`SiegeMissionPreparationHandler.cs:190`, section 4.2).
- Scene work (CONVENTION): one `batteringram_a_spawner` per level with `GateTag outer_gate`, `SideTag middle` and a
  `PathEntityName`; the path ends 0.28 to 1.38 m from the outer gate's rest point (GATE frame Y +6.25). Ram body (its
  prefab frame) x -3.48 to 3.43, y -5.69 to 4.39, z 0.21 to 4.33 (heights study 5).
- For the kit: the ground in front of the outer gate holds the ram's path for at least the 6.25 m to the rest point. For
  the Rivendell gatehouse (outer gate at KIT Y +4.2) the rest point is at KIT Y 10.45, 4.25 m beyond the front face
  (Part B's sum). The room the ram needs beyond that: UNVERIFIED.

### 7.6 Gate census (CONVENTION, Part B 1.3)

In vanilla the inner gate is often an outer prefab re-tagged `inner_gate` in the scene (european l1 65 times, european l2
39 times). Whether a scene's tag replaces or adds to the prefab's: UNVERIFIED. In `battania_castle_a`, `_b` and
`battania_town_d` levels 1 and 2 use the european l1 outer gate and level 3 the Battania l3 pair (the table by level is
in Part B 1.3); all three use face ids 502 with 512 (outer) and 521 with 531 (inner) and hold 3
`batteringram_a_spawner` with `GateTag outer_gate`. TAOM: 7 scenes use `european_castle_gate_outer_l2` as the outer gate, 18 `empire_castle_gate_inner_l3` as
the inner; 51 of 56 TAOM gate instances list no `<levels>`. Four TAOM Gondor scenes hold a hidden inner gate (version 1,
section 7.6; section 15, item 12).

Scenes move the outer gate's middle position inward and widen it: BCA level 2 to y -8.094 (GATE frame) with `_width 5.0`,
0.94 m beyond the inner gate (scene lines 4590 to 4607); DA to Y -8.323, PE -5.781.

## 8. The defenders' siege placement

### 8.1 Machine slots (Part A5; Part C 4.3)

- The slot is a `DeploymentPoint` with `Side=Defender`. The prefab `siege_deployment_placeholder_part_b` sets
  `Side=Attacker`, `Radius=4.160`, `SiegeWeaponTag=no_deploy` (NATIVE `props_siege.xml` lines 180 to 198); the scene sets
  `Side=Defender`. Code default `Radius` 3. A point takes a weapon that carries its `SiegeWeaponTag` (or whose parent
  does), or whose position lies within `Radius`, measured in 3D (`DeploymentPoint.cs:35, 186-202`). ENGINE.
- **One weapon a point** (ENGINE): a point holds one `DeployedWeapon` (`DeploymentPoint.cs:57`, `public
  SynchedMissionObject DeployedWeapon { get; private set; }`); `Deploy(Type t)` takes the first weapon of that type under
  the point and shows it (`:272-279`); a weapon that is not deployed is made invisible with its physics off (`:253-264`);
  `Hide()` hides every weapon under the point (`:302-313`). So a point shows one deployed weapon at a time, and a
  platform is sized for the larger footprint of the weapons under its point, with each origin within the `Radius`, not
  for two side by side. Not seen in the game.
- Four per level: DA 4, PE 4, GC1 4, BCA 12, EC4 12, all six elven scenes 4 per level. CONVENTION.
- Each point has one `mangonel_b_spawner` and one `ballista_b_spawner`: in DA and PE the mangonel 0.09 to 0.93 m from the
  point and the ballista 2.0 to 2.8 m, at the point's height; in four of the six elven scenes (`battania_castle_a`, `_b`,
  `battania_town_a`, `_d`) the mangonel 0.17 to 4.56 m and the ballista 2.04 to 5.99 m, with scene radii 4.94 to 11.33 m
  where set (Part C 4.3, flat distances). CONVENTION.
- Where they stand: DA two on the Gondor catapult towers and two on a redoubt's walls; PE all four on solid walls, 0.0 to
  0.10 m over the deck. CONVENTION.

**The mangonel** (`mangonel_b_spawner`, spawns `mangonel_b_grapeshot`; ENGINE unless marked): it fires along the body's
-Y, pitched by the release angle (`Mangonel.cs:127-135`); it turns 120 degrees in all (`DirectionRestriction = 2pi/3`,
`RangedSiegeWeapon.cs:375`); release angle -0.200 to 0.700 rad and stone speed 36 m/s (data,
`props_siege_mangonels.xml` 3479, 3484 to 3485); a thick ray (radius 0.295 m) to the target must hit no body, so a column
or roof in the arc blocks shots (by the mangonel research, `RangedSiegeWeapon.cs:1786-1917`); the crew leaves for an
enemy within 9 m (`Mangonel.cs:262`). Footprint (CONVENTION, `catapult_platform.md` B4): with crew and the eight ammo
points round the default pile 5.50 x 5.72 m; turned over the arc 5.24 x 4.82 m for machine and crew, rounded up from the
swept 5.24 x 4.75 m, so conservative; the pile does not turn (`journal_results.json` lines 184, 279). From a 12 m deck the
AI cannot shoot ground closer than about 35 m (a vacuum calculation, UNVERIFIED in game).

**The ballista** (`ballista_b_spawner`): `DirectionRestrictionDegree = 90` (`BallistaSpawner.cs:15, 28`; whole arc or one
side UNVERIFIED); 1.90 x 2.30 m by its standing point; no ammo points.

**What a catapult platform must give** (CONVENTION and ENGINE as marked): a clear level deck of 5.50 x 5.72 m for one
mangonel, crew and pile, or the turned 5.24 x 4.82 m with the pile elsewhere; the ballista under the same point needs no
room of its own, since only one of the two is deployed (ENGINE, above), but its origin stays within the point's radius
(4.16 m from the prefab); the deck walkable to every standing point, the point 0.0 to 0.1 m over it; no body in the 120
degree arc toward the field (ENGINE). A Gondor catapult tower's largest clear rectangle, 11.2 x 5.4 m, holds one mangonel
footprint with room to spare (inferred, `catapult_platform.md` lines 207 to 209).

### 8.2 Rock piles over the gate (Part B 2.5)

ENGINE (`StonePile.cs:159, 166, 305-307, 1036-1050`): targets are every entity tagged `throwing_target`; range 15 m
(`_givenItemRange`, a variable the scene can set: `[EditableScriptComponentVariable(true, "")]`, `:165-166`); a target
less than 75 degrees below the eye and reached by a ray from the eye; an attacker within 1.31 m of the target triggers
the throw. With the stock 15 m range and an eye height of 1.6 m assumed, a deck above about 14 m over the targets is too
high (inferred, `gatehouse_layout.md` line 352); a higher deck needs a lower deck or the range raised on the pile (line
353).

**The EC4 piles at Z 15.90 and 18.37 do not contradict it** (`gatehouse_layout.md` lines 329 to 330, 357): one pile a
level throws into the passage; the L2 pile 62338 and the L3 pile 2772 have their throw points outside the outer gate and
reach no target (6.9 to 11.9 m horizontal, dz 16 to 18 m). So the limit holds for the pile that throws into the passage.

CONVENTION (`gatehouse_layout.md` rows 8 to 18, GATEHOUSE frame): 3 to 6 `murder_hole_target` spheres per level at the
inner gate's Y plus 1.9 to 2.9 m toward the field, Z 1.0 to 2.2; one `throwable_rock_pile` per level, its throw points in
pairs on the deck either side of the axis and its `volume_box` on the passage floor; all placed by the scene. For the
Rivendell gatehouse (KIT frame) the hole at Y -2.6 to -1.0 covers that row for an inner gate at Y -4.2 (Part B's
reading). Whether a rock passes the hole in game: UNVERIFIED. No oil mechanic appears in any file read; the piles give
`boulder`, `boulder_carry` or `pot`.

### 8.3 Archer positions and other defender entities (Part A5)

| Entity | What the engine reads | Counts DA / PE / GC1 / BCA / EC4 | Source |
|---|---|---|---|
| Entities tagged `archer_position` (TAOM: the prefab `defender_archer_position`; BCA and EC4 tag other entities) | the defender AI takes every entity with the tag whose parent is null or visible | 4 / 4 / 4 / 3 / 12 | ENGINE `TeamAISiegeDefender.cs:22-25`; CONVENTION (my count: the prefab 4, 4, 4, 0, 0) |
| `strategic_archer_point` | `StrategicArea`, `_side=Defender`, `_width=1`, `_depth=1`, `_distanceToCheck=40` | 144 / 186 / 114 / 203 / 284 | ENGINE `StrategicArea.cs:127-145`; CONVENTION |
| Archer points under the wall's children | switch with the breach (BCA 6 under the solid child, 7 under the broken) | | ENGINE `WallSegment.cs:198-209` |
| `arrow_barrel` | archers' ammunition | 25 / 4 / 9 / 76 / 54 | CONVENTION |
| `throwable_rock_pile`, `stone_pile_b_usable` | throwers | rock 3 / 1 / 1 / 0 / 0; stone 0 / 0 / 0 / 2 / 11 (BCA places 3 rock piles inline) | CONVENTION |

`python tools/audit_siege_props.py --scene NAME` audits rock piles, arrow and javelin barrels and dead `GivenItemID`
references. The defenders' wait and middle frames of a wall are its markers (4.3); a gate carries its own
`middle_position` (7.1).

## 9. The attackers' side of the scene (Part A6)

The kit does not place these, but its test scene must, and the field in front of the wall line must leave room. The
rule (CONVENTION, two TAOM towns): keep the field in front of the wall line open for about 100 m for the attackers'
archers and boundary, about 135 m for the trebuchets, 150 to 215 m for the two tower paths and the ram path, with the
ladder feet about 10 m from the wall.

The engine reads (ENGINE): `DeploymentPoint` with `Side=Attacker` (`siege_deployment_placeholder_part_a`, `Radius=7.750`,
NATIVE `props_siege.xml` 295 to 313; the scene sets `SiegeWeaponTag` `tower_left`, `tower_right`); the deployment
boundaries, entities tagged `deployment_castle_boundary(_\d+)*` with the side's tag, one outline a tag
(`MBSceneUtilities.cs:88-121`); formation spawns by `<side>_<formation class>` tags; `archer_position_attacker`
(`TeamAISiegeAttacker.cs:18`). The entity list with the distance of each to the nearer breach wall root in DA, PE and GC1
is in Part A, section A6 "Deployment and spawn" (and in version 1, section 9). The minimum set a siege mission accepts:
UNVERIFIED.

## 10. The civilian layer and the interiors (Part C 1.4)

ENGINE: the visit puts the player at `spawnpoint_player` (or the caller's tag), else at the world origin
(`SandBoxHelpers.cs:99-116`); townspeople from `npc_common_limited`, then `npc_common` (`MissionAgentHandler.cs:762-766`);
guards `sp_guard`, `sp_guard_with_spear` (`:235`). The keep's hall fight reads `FightAreaMarker` scripts and
`defender_archer` (only on a navmesh face) and `defender_infantry` points in a marker's range
(`LordsHallFightMissionController.cs:98-155`); the arena reads `arena_set`, `sp_arena`, `sp_arena_respawn`
(`ArenaPracticeFightMissionController.cs:79-117`); the dungeon the level `prison_break` and the tag `sp_prison_break`
(`SandBoxMissions.cs:250, 266`). The keep is three scenes, one a wall level; the hall fight loads the one of the current
level. The spawn tag counts of each elven vanilla scene are in Part C 1.4 (and in version 1, section 10); they are what
the scenes hold, not a proven minimum. What happens with too few spawn points (`MissionAgentHandler.cs:380`): UNVERIFIED.
`python -I tag_census.py "$MODS" <scene>` (section 13) counts them for any scene.

## 11. How a kit reaches a scene (Part C 2)

### 11.1 The path

| Step | What | Who | Kind, source |
|---|---|---|---|
| 1 | Kit FBX files in `TAOM_Map/AssetSources/Scenes/<Kingdom>/<family>/`, `<kingdom>_<family>[_<kind>]_kit.fbx`, lower case | the kit session builds, a copy tool copies | DECISION, [kingdom-kit-layout.md](kingdom-kit-layout.md) rules 1 to 8 |
| 2 | Import in the Modding Kit | the owner | same doc |
| 3 | The prefab file `TAOM_Map/Prefabs/taom_<kingdom>_kitbash.xml`, written after its meshes are imported, with the Kit closed | the kit session, on the owner's word | DECISION, rule 9; SURV 4.4, 4.5; `PREFAB_OWNER_STEPS.md` |
| 4 | The showcase scene `mod_taom_<kingdom>_kitbash`, every piece laid out for scene builders | the Kit makes the scene | DECISION ("We can do one scene with all of rivendell's kitbash in it laid out so that sceners can use it", REC, the owner's answers for the first Kit import, 2026-11-25) |
| 5 | A settlement scene from the prefabs, with the siege wiring of sections 3 to 9 | a scene builder | CONVENTION |
| 6 | The `scene_name` edit in the live `settlements.xml` | the owner | [taom-map-settlement-naming.md](../reference/taom-map-settlement-naming.md) |

On disk (`ls` 2026-11-26 19:00): kitbash prefab files and showcase scenes for Erebor, Gondor, Mirkwood and Mordor; a
Rivendell prefab file and no Rivendell scene; nothing for Lothlorien. A showcase scene's folder by the four on disk:
`TAOM_Map/SceneObj/<scene>/` holds `scene.xscene`, `atmosphere.xml`, `terrain.bin`, `flora.bin`, `references.txt` and a
`ShaderCache` folder, and no `navmesh.bin`; `TAOM_Map/SceneEditData/<scene>/terrain_ed.bin` holds its terrain edit data.
Whether a copied folder under a new name is safe: UNVERIFIED.

### 11.2 The prefab entity cap (ENGINE limit)

[module-map.md](../modding/module-map.md) lines 356 to 357: every `<game_entity>` in every `Prefabs/` folder across all
loaded modules goes into a native queue with a hard capacity of 131,072; crossing it asserts before the Kit finishes
loading. A scene file does not count (SURV 4.1). On 2026-11-26 the five modules that the Kit loaded (`TAOM_Map` 98,104,
`Native` 23,828, `SandBox` 2,889, `StoryMode` 39, `LOTRLOME_Armory` 19) summed to 124,879, 6,193 spare; with `NavalDLC`
(9,927) the sum is 134,806, over the cap (`check_prefab_budget.py`, Part C 2.2). Moving a file back from
`TAOM_Map/Prefabs_Unused` "can cost you the entire margin" (module-map lines 391 to 392). Count again with
`python -B tools/check_prefab_budget.py --dir "$MODS/<module>/Prefabs"` for each module the Kit loads (the list is in the
session's `rgl_log`, module-map lines 368 to 370), then sum.

### 11.3 A new scene, or an edit of a shared vanilla scene

Every elven settlement loads a vanilla Battania scene, and those scenes are shared (CONVENTION, Part C 2.3, where the
settlement ids are): `battania_castle_a` 8 settlements of 5 cultures (rivendell 2, mirkwood 2, lothlorien 2, lindon 1,
isengard 1); `battania_castle_b` 7 of 4 (mirkwood 3, rivendell 2, lothlorien 1, lindon 1); `battania_town_a` lothlorien
only; `battania_town_b` mirkwood only; `battania_town_c` mirkwood and lindon; `battania_town_d` rivendell and lindon; the
Battania keep and dungeon 22 settlements of the four elven cultures; `arena_battania_a` and the Battania tavern the 7
elven towns.

| Route | Cost | What it breaks |
|---|---|---|
| A new scene `taom_<kingdom>_<kind>_<name>_forceatmo` in `TAOM_Map/SceneObj`, then the `scene_name` edit | a full scene with three levels, navmesh and siege wiring | nothing shared. 30 of the 35 TAOM_Map scenes that settlements load are named `taom_..._forceatmo`; a name holding `forceatmo` keeps the scene's own atmosphere ([atmosphere-persistence.md](../features/atmosphere-persistence.md); `AtmosphereOverrideService.cs:7`) |
| An edit of the vanilla scene in SandBoxCore | less work | every settlement of the table above changes with it; a game update or file check can restore it. Which module wins for a TAOM_Map scene of a vanilla name: UNVERIFIED |

**Names other TAOM data keys on:** `Main/_Module/ModuleData/story_missions/story_missions.json` lines 136 to 138 run the
story mission `lorien_defend` on `battania_town_a` by name; [story-missions.md](../features/story-missions.md) line 368
calls that scene a "Stand-in scene", so a new Lothlorien town scene can take the mission over by an edit of that row.
What the mission reads from its scene is in that feature doc. `Main/_Module/ModuleData/siege/staged_siege.json` lists
only `sturgia_castle_siege_001`, so a new scene needs its own recording ([staged-siege.md](../features/staged-siege.md)
line 68).

**The owner's step:** a reinstall of `TAOM_Map` reverts a hand edit of `settlements.xml`; a comment that is a direct
child of `<Components>` drops that settlement and every later one
([taom-map-settlement-naming.md](../reference/taom-map-settlement-naming.md) line 139). `tools/add_map_fortifications.py`
writes the four `center` slots with one scene (line 428) and the three keep slots.

## 12. What each kingdom loads today

Source: Part C 3 (the full table with every scene name, the kit folders and the showcase scenes), script
`culture_scenes.py` over the live `settlements.xml` (1,266 settlements). The row is the settlement's `culture=` attribute.
Six cultures keep a vanilla id: `vlandia` is Rohan, `empire` Dunland, `aserai` Harad, `khuzait` Rhûn, `sturgia` Dale,
`battania` Khand ([id-cheatsheet.md](../modding/id-cheatsheet.md)); so `battania` (Khand) loads khuzait scenes, and the
Battania scenes are loaded by the elves. "TAOM scene" says whether any location of the culture (centres, interiors and
villages) names a scene in `TAOM_Map/SceneObj`, with the number of distinct such names (`fc_taomcol.py`). "x2" is two
settlements on one scene.

| Culture id | People | Towns / castles / villages | Centre scenes (towns; castles) | TAOM scene |
|---|---|---|---|---|
| `gondor` | Gondorian | 11 / 16 / 70 | `taom_gondor_town_*_forceatmo` x7, `Lotraom_comprehend_generic_gondor_town_001`, `_002`; `taom_gondor_castle_001` to `_003_forceatmo`, `Lotraom_comprehend_generic_gondor_castle_001` | yes (22) |
| `khuzait` | Rhûn | 8 / 12 / 68 | `khuzait_town_004`, `_e`, `_f`, `_g`; `khuzait_castle_002`, `khuzait_castle_siege_001` | no |
| `mordor` | Mordor | 6 / 8 / 42 | `taom_mordor_town_01_forceatmo` x5, `taom_mordor_town_minas_forceatmo`; `empire_castle_004`, `_b`, `_c`, `_f`, `_g`, `empire_siege_001` | yes (6; castles vanilla) |
| `erebor` | Dwarves | 4 / 9 / 39 | `sturgia_town_a` to `_d`; `sturgia_castle_003`, `_c`, `sturgia_castle_siege_001` | no (two TAOM town scenes exist that no settlement loads) |
| `aserai` | Harad | 5 / 11 / 34 | `aserai_town_a`, `_b`, `_c`, `_f`, `_g`; `aserai_castle_002`, `_a`, `_c`, `_d` | no |
| `abanissa` | Âbanissa | 4 / 8 / 37 | `aserai_town_c` to `_f`; `aserai_castle_002`, `_a`, `_c`, `_d` | no |
| `vlandia` | Rohan | 7 / 7 / 34 | `taom_rohan_edoras_town_forceatmo`, `taom_rohan_castle_helms_deep_forceatmo`, `vlandia_city_a`, `vlandia_town_a` x2, `_c`, `_g`; `vlandia_castle_005a` x4, `_a`, `_c`, `_d` | yes (2) |
| `battania` | Khand | 4 / 7 / 37 | `khuzait_city_b`, `khuzait_town_e`, `_f`, `_g`; `khuzait_castle_002`, `khuzait_castle_siege_001` | no |
| `shaghana` | Shaghâna | 5 / 8 / 33 | `aserai_town_a`, `_b`, `_d`, `_e`, `_h`; `aserai_castle_002`, `_a`, `_c`, `_d` | no |
| `umbar` | Umbar | 2 / 8 / 30 | `aserai_town_g`, `_h`; `aserai_castle_002`, `_a`, `_c`, `_d` | no |
| `gundabad` | Gundabad Orcs | 3 / 6 / 29 | `sturgia_town_a`, `_f`, `_g`; `sturgia_castle_003`, `_c`, `_siege_001` | no |
| `sturgia` | Dale | 5 / 7 / 25 | `sturgia_town_b`, `_c`, `_d`, `_f`, `_g`; `sturgia_castle_003` x4, `_c`, `_siege_001`, `taom_dale_castle_001_forceatmo` | yes (1 castle) |
| `arthedain` | Arthedain | 3 / 7 / 24 | `empire_town_c`, `_d`, `_i`; `taom_gondor_castle_001` to `_003_forceatmo` | yes (7; towns vanilla) |
| `mistymountainorcs` | Misty Mountain Orcs | 3 / 7 / 23 | `sturgia_town_f`, `taom_dwarven_moria_e_forceatmo`, `_w_forceatmo`; `sturgia_castle_siege_001` x7 | yes (2) |
| `empire` | Dunland | 3 / 6 / 21 | `empire_town_g`, `_q`, `_r`; `empire_castle_004`, `_f`, `empire_siege_001` | no |
| `eredluin` | Dwarves of Ered Luin | 3 / 5 / 23 | `sturgia_town_b`, `_c`, `_g`; `sturgia_castle_003`, `_c`, `_siege_001` | no |
| `southrhovanion` | Northmen of Rhovanion | 2 / 4 / 25 | `vlandia_town_d`, `_e`; `vlandia_castle_a` to `_d` | no |
| `dorwinion` | Men of Dorwinion | 3 / 4 / 23 | `sturgia_town_a`, `_b`, `_e`; `sturgia_castle_003`, `_c`, `_siege_001`, `taom_dale_castle_001_forceatmo` | yes (1 castle) |
| `lindon` | Falathrim Elves | 3 / 3 / 22 | `battania_town_c`, `_d`, `_e`; `battania_castle_a`, `_b`, `_c` | no |
| `mirkwood` | Silvan Elves | 2 / 5 / 17 | `battania_town_b`, `_c`; `battania_castle_a` x2, `_b` x3 | no |
| `bluecraig` | Blue Craig Goblins | 2 / 4 / 18 | `sturgia_town_e`, `_f`; `sturgia_castle_siege_001` x4 | no |
| `dolguldur` | Dol Guldur Orcs | 1 / 5 / 17 | `empire_town_e`; `empire_castle_004`, `_b`, `_c`, `_f`, `_g` | no |
| `cardolan` | Cardolan | 3 / 2 / 18 | `empire_town_f`, `_k`, `_l`; `taom_gondor_castle_001`, `_002_forceatmo` | yes (6; towns vanilla) |
| `anduinvale` | Beornings | 2 / 4 / 16 | `sturgia_town_a`, `_e`; `sturgia_castle_003`, `_c` x2, `taom_dale_castle_001_forceatmo` | yes (1 castle) |
| `isengard` | Isengard | 1 / 3 / 17 | `taom_isengard_town_orthanc_forceatmo`; `battania_castle_a`, `empire_castle_004`, `empire_siege_001` | yes (1; castles vanilla) |
| `rivendell` | Ñoldor Elves | 1 / 4 / 16 | `battania_town_d`; `battania_castle_a` x2, `_b` x2 | no |
| `lothlorien` | Galadhrim Elves | 1 / 3 / 15 | `battania_town_a`; `battania_castle_a` x2, `_b` | no |
| `angaladh` | Woodmen | 1 / 4 / 14 | `sturgia_town_d`; `sturgia_castle_003` x2, `_c`, `taom_dale_castle_001_forceatmo` | yes (1 castle) |
| `rhudaur` | Rhudaur | 1 / 3 / 11 | `empire_town_m`; `taom_gondor_castle_001` to `_003_forceatmo` | yes (7; town vanilla) |
| `goblin` | Goblins | 1 / 0 / 6 | `taom_mordor_town_goblin_town_forceatmo`; none | yes (1) |
| bandit cultures | 9 cultures, 181 hideouts | 0 / 0 / 0 | | no |

The four elven cultures load only vanilla Battania scenes, interiors and villages included (11.3).

The siege content of the six elven centre scenes (per level in 2.3; the plain grep counts and the `navmesh.bin` sizes in
Part C 4.1, the ladders, towers, gates and gatehouse shells level by level in Part C 4.2). Lindon's `battania_castle_c`
and `battania_town_e` were not counted.

## 13. How to find a fact for a new kingdom

The census scripts are durable in `E:/LOTRAOMAssets/_auto_workspace/kingdom_kits/_shared/scene_needs/scripts`. Run them
from that folder, with `MODS` set to `E:/Steam/steamapps/common/Mount & Blade II Bannerlord/Modules`; each one reads only
and writes only the output path that you give it. Each command below was run once from that folder on 2026-11-26 at
about 19:00; its first output line is given.

| Need | Command (from the scripts folder) | First output line |
|---|---|---|
| Which scenes each culture loads | `python -I culture_scenes.py "$MODS" <out.json>` | `\| culture \| towns \| castles \| villages \| hideouts \| town centre scenes \| ...` (a Markdown table; the JSON holds `["<culture>"]["scenes"]`) |
| Siege content by level | `python -I siege_by_level.py "$MODS" battania_town_a` | `## battania_town_a  (SandBoxCore; header {'base': '1', ... 'sally': '64'}; nav_mesh_auto_generated_=false; navmesh.bin 6340024 bytes)` |
| Spawn tags and scripts of a scene | `python -I tag_census.py "$MODS" battania_town_d` | `## battania_town_d (SandBoxCore) levels=['base', 'level_1', 'level_2', 'level_3', 'civilian', 'siege'] navmesh.bin=yes` |
| Gate prefabs and their instances | `python -I gate_census.py <out.txt>` | none on screen; the file's first line is `# gate prefabs (Native)` |
| Siege counts of DA, PE, GC1, BCA, EC4 and the six elven scenes | `python -I fc_count.py "$MODS" <out.json>` | `ok` |
| Settlements and location counts | `python -I fc_settlements.py "$MODS/TAOM_Map/ModuleData/settlements.xml" "$MODS"` | `settlements 1266` |
| TAOM_Map scenes named per culture | `python -I fc_taomcol.py "$MODS/TAOM_Map/ModuleData/settlements.xml" "$MODS"` | `gondor distinct 22 locations 100 settlements 97` |
| Sided breach wall roots and their distance | `python -I fc_walls.py "$MODS/TAOM_Map/SceneObj/taom_gondor_town_dolamroth_forceatmo/scene.xscene"` | `taom_gondor_town_dolamroth_forceatmo [('left', 0, '658.180, 602.260, 102.720'), ('right', 0, '590.810, 533.790, 102.720')]` |
| The levels of walls and gates in a scene | `python -I fc_levels.py "$MODS/TAOM_Map/SceneObj/taom_gondor_castle_001_forceatmo/scene.xscene"` | `taom_gondor_castle_001_forceatmo` |
| Instances of `empire_castle_gate_inner_l3` | `python -I fc_inner.py "$MODS/TAOM_Map/SceneObj"` | `instances 19` |

The culture id comes from [id-cheatsheet.md](../modding/id-cheatsheet.md) lines 7 to 62, and an engine line from
`pwsh tools/taom-src.ps1 path <Type>` (SRC). The repo gates that a scene builder runs are named where they apply: the
wall gate (4.2, 14), `audit_siege_props.py` (8.3) and `check_prefab_budget.py` (11.2); this section lists only what was
run from the scripts folder.

## 14. Traps

| Trap | What to do | Source |
|---|---|---|
| A wall placed as a bare prefab instance escapes the wall gate; the gate also passes four walls | Break the prefab link (as DA did) before trusting a PASS; count the sided walls yourself: the engine wants two a level and a ram | SGS; SURV 4.5; 4.2 |
| A ladder or tower with no `SideTag` lands in the gate lane | Set `SideTag` on every ladder and tower | 4.2 |
| `tower_merlon` works only on an entity with a `SynchedMissionObject` script, and hides every tagged part of the scene | Tag only the parts where a ramp lands | 3.1, 6.2 |
| The ladder number is not its length; there is no 10 or 12; the 11 m ladder is at its limit on a 12 m deck | Plan a 12 m deck with the 14 m ladder and the 12 m tower | 2.4, 6.1 |
| The kit FBX is read turned (a file point (x, y, z) arrives as (-x, z, y) x 0.01) | The KIT +Y field face is SCENE -Y; every child position a generator writes depends on it | How to read, "Frames" |
| Navmesh face ids: defenders and inner gate slots need ids ending in 1; attackers and ladder landings never; an id that names no face does nothing | Paint and check the ids in the test scene | 3.1, 6.1, 7.1 |
| A level with no inner gate, or a hidden one | Three readers have no null check; a hidden gate never opens. Four TAOM Gondor scenes hold a hidden inner gate | 7.1, 7.6 |
| `CanBeDestroyedInitially` left true on a gate, or a scene `MaxHitPoint` of 1 copied from DA's inner gate | Set it false; check the hit points | 7.1 |
| Leaves wider than the opening, or no stone behind the hinges | Give the passage 4.70 m and stone behind the pins (GD6) | 7.2 |
| A route B skeleton of the vanilla name in `TAOM_Map` | Could reach every vanilla gate of that family: probe in a throwaway module | 7.4 |
| Upper case placeholder names in a kit file | The Kit lowercases on import; a prefab lookup is case sensitive: keep placeholders out of the kit file | SURV 553-561 |
| A rock hole with no clear room below, or a deck too high | The ray or the 15 m range fails | 8.2 |
| A platform sized for a mangonel and a ballista side by side, or one that ignores the turn | One weapon a point is deployed: size for the larger footprint, with the turned box and the arc | 8.1 |
| The Gondor kitbash tower interior has no `has_steps`; the scene builder adds it each time | Ship the stair body with the flag | 5.2 |
| A part split into its own mesh where no engine need asks for it | One asset a piece: only parapet parts, a tower's interior and a stair with `has_steps` stay separate | 3.4 |
| A scene parser that does not read prefab definitions puts a prefab's children at its root | Use a reader that composes prefab children (WIRING `helper_scripts/scene_model.py`) | `catapult_platform.md` lines 9 to 12 |
| A coordinate read in the wrong frame | Read the frame label of the table first; four frames are in use | How to read, "Frames" |

The trap sheets [mission.md](../../.claude/rules/traps/mission.md) and [campaign.md](../../.claude/rules/traps/campaign.md)
hold no castle gate row yet (Part B grep); REC "The gate reference is in the repository tree" holds the three owed.

## 15. Open or UNVERIFIED, and the smallest step to settle each

| # | Open item | Smallest step |
|---|---|---|
| 1 | Which entities a siege level loads for an entity outside the asked levels (hidden, removed, never made), and whether an entity with no `<levels>` is in every level | Grep the engine's native scene loader (`python tools/native_decompile.py`), or load BCA at level 1 in the Kit and list the visible gates |
| 2 | What `has_steps` does to an agent (native); whether a railing may share a stair body's flag | Native decompile of the body flag's reader, or one test with and without the flag on a stair in a custom battle scene |
| 3 | Whether the TAOM on-solid wall faces 122 and 222 are walk faces defenders stand on, and whether the `WallSegment` and gate ids name painted faces | Decode `navmesh.bin` of GC1, or open GC1 in the Kit and read the face ids |
| 4 | What a null `MiddlePosition` (markers with tags, no `TacticalPosition`) does to the defender AI | Read the callers of `WallSegment.MiddlePosition` in SRC |
| 5 | Whether `GetDeploymentBoundaries`'s `HasTag("Attacker")` matches the prefabs' `attacker` (case) | Read `GameEntity.HasTag` or its native side |
| 6 | A ladder head under a high parapet; whether troops cross a tower ramp tip above the deck | One siege run on the test scene |
| 7 | The ballista's 90 degree arc (whole or one side) | Read the use of `HorizontalDirectionRestriction` in SRC |
| 8 | The mangonel near limit of about 35 m from a 12 m deck | Trace the native flight, or one run |
| 9 | Whether a tower platform above the walk works for crew and ammo carriers with the pile on the platform | One siege run with a mangonel on such a tower |
| 10 | The minimum attacker and scaffolding set a siege mission accepts | Remove entities one by one in a test scene copy |
| 11 | Whether a scene's `<tags>` on a gate instance replace or add to the prefab's | Read the native prefab instance loader, or one Kit check |
| 12 | Which of two inner gates the AI takes, and so the effect of the four hidden TAOM inner gates in an ordinary siege | Read the order of `ActiveMissionObjects`, or one siege run in `taom_gondor_castle_002_forceatmo` |
| 13 | The ram's own reach in front of a gatehouse beyond the 6.25 m rest point | Read the ram prefab's path entity and its mesh bounds |
| 14 | The on-screen swing; a vanilla leaf taller than its kit arch (GC1: 6.84 m leaf, 5.24 m arch) | One look in the Kit at GC1's outer gate |
| 15 | The engine frame of an imported Rivendell gatehouse, and so the turn of a vanilla gate inside it (inferred from the wall's import and from DA) | Import gatehouse a and read its bounds as the wall check does |
| 16 | What the `WallSegment` script on a vanilla gatehouse does besides the breach choice | Grep SRC for `WallSegment` readers |
| 17 | Whether a rock passes a kit's rock hole; the eye height behind the 14 m deck limit (1.6 m assumed) | One siege run on a kit gatehouse, or read `GetEyeGlobalPosition` |
| 18 | Route B: whether the Kit accepts a skeleton of the vanilla name in another module | The probe's owner steps in a throwaway module (`PROBE_OWNER_STEPS.md`) |
| 19 | The least walk width and breach gap that the AI takes (section 16) | One siege run on a test scene with a walk and a gap of the planned widths |
| 20 | Whether a missing scene folder crashes the game | Read the scene loader in SRC, or trust the audit doc and keep `audit_scene_names.py` green |
| 21 | Which module wins for a TAOM_Map scene with a vanilla name | Do not do it; build a new name |
| 22 | What `MissionAgentHandler` does after "Trying to spawn ... but there are N suitable spawn points" | Read `MissionAgentHandler.cs` after line 380 |
| 23 | The callers of `OpenSiegeMissionNoDeployment`, `OpenPrisonBreakMission`, `OpenAlleyFightMission` | Grep SRC for the method names |
| 24 | The vanilla wall heights per level (read only from the ladder lengths) | Measure the Battania wall meshes `_l1`, `_l2`, `_l3` with the bounds tool |
| 25 | Which spawner a sally out uses when `sally_out_set` is missing; whether native code reads the `sally` level | Read the sally out deployment in SRC; a native grep for the level name |
| 26 | Whether a copied showcase scene folder under a new name is safe in the Kit | One copy in the Kit with the owner |
| 27 | The levels the elven settlements start at (`town_R1` has fortifications level 3, `settlements.xml:11072`) | Grep each elven settlement's `building_settlement_fortifications` |
| 28 | Lindon's `battania_castle_c` and `battania_town_e` | Run `siege_by_level.py` on them |
| 29 | What each debris prefab looks like, and whether a kit needs its own wooden or earth debris | Open the debris prefabs of section 16 in the Kit |
| 30 | Nothing here was run in the game; in the Kit only the Rivendell wall was imported | The siege test prefab, a test scene, one siege run |

The conflicts between our own records that version 1 listed here are in
`E:/LOTRAOMAssets/_auto_workspace/kingdom_kits/_shared/scene_needs/record_conflicts_for_the_lead.md`; this file states
the settled value.

## 16. A defence that is not masonry

A kingdom of trees, hedges or earth banks (a palisade, an earth bank, a hedge, platforms in trees) can have any look.
No managed code reads a wall's height (3.1, negative in the scope searched), and no line read here reads a wall's
material: the mesh is the look. What the engine still needs (ENGINE): two sided `WallSegment` trees a siege level, each
with a `solid_child` and a `broken_child` that carry the script (4.1, 4.2); a visible outer and inner `CastleGate` a level
with the face ids (7.1); a ram and its approach (7.5); parapet parts with a synched script (`DestructableComponent`)
where a siege tower lands (3.1); walk faces with the right ids (3.1); standing room for the machines (8.1); and a deck
that the 14 m ladder and the 12 m tower serve, or the matching shorter machines, whose sizes the Native prefabs fix (2.4,
6).

**The debris prefabs that exist** (my census of 2026-11-26 19:00 over every `*.xml` of the `Prefabs` folders, script
`debris_census.py` in the scripts folder of section 13; SandBoxCore has no `Prefabs` folder):

- Native: 12 top-level prefabs named `debris*`: `debris` (`props_siege.xml`), `debris_aserai_l1`, `debris_aserai_l3`,
  `debris_empire_l1`, `debris_empire_l2`, `debris_empire_l3`, `debris_khuzait_l1`, `debris_khuzait_l3`,
  `debris_sturgia_l3`, `debris_vlandia_l1`, `debris_vlandia_l1_roof`, `debris_vlandia_l3` (each in its
  `castle_<culture>_l<N>.xml`). The `DestructionStates` values used in Native: those eleven `debris_<culture>_*` names;
  `castle_wall_roof_aserai`, `_battania`, `_empire`, `_vlandia` (defined in `castle_wall_roof.xml`); the gate states
  `state1` to `state4`; `destroyed`; `european_sally_door_a_state2` to `_state4`; `hanging_pot_destroyed`;
  `pot_archery_destroyed`.
- SandBox: one value, `pot_archery_destroyed` (13 uses), and no debris prefab.
- The Battania castle prefabs use `debris_vlandia_l1` (96 uses in `castle_battania_l1.xml`, 96 in `_l2`); `_l3` uses
  `debris_sturgia_l3` 94, `debris_vlandia_l1` 4 and `debris_vlandia_l1_roof` 5. No prefab name says wood, timber,
  palisade or earth. What each looks like: UNVERIFIED (section 15, item 29).

**The least walk width and breach gap that the AI takes:** not in our records. A grep of the build record, the engine
docs (`docs/reference/engine/`), the wiring notes (`wiring/*.md`) and the three parts for walk width, narrowest, minimum
width and least width found no line on it. The only numbers are the measured examples: the Gondor walk 6.0 m clear
between parapets, the Rivendell walk 4.27 m; the breach gaps 11.5 to 12.5 m (Gondor) and 12.0 to 12.8 m (Rivendell).
UNVERIFIED (section 15, item 19).

## 17. What Rivendell has built (state of 2026-11-26, about 18:30)

From version 1's summary table, which read the build record and the module on that day; nothing was checked in game.
The solid wall is built and approved, imported into the Kit at 10:38, and in the prefab file
`TAOM_Map/Prefabs/taom_rivendell_kitbash.xml` since 16:24 (56,726 bytes; 0 `WallSegment`, 0 `DestructableComponent`, 0
`has_steps`); it holds its railing as its own mesh `_m7` (SURV 3.6), which is to change to a sub-object at the next
import (3.4; REC KS3). The broken wall is in work (stage 6). Tower a, the corner tower and gatehouse a are first builds in
the module's kit set 3 since 17:08, not yet imported; the prefab file still names the part meshes of kit set 2
([kingdom-kit-layout.md](kingdom-kit-layout.md), "State on 2026-11-26"); gatehouse b's seams failed 6 of 6. The 12 m
castle stair is built; its flag is not yet in the prefab file. Gates: route A works today, route B's Kit probe has not
run, GD6 is not in the gatehouse code of 18:08; gatehouse a's outer leaves clear the hall by 0.35 m against its code of
13:24 (`GATE_SWING.md` lines 73 to 85). On wall A the 14 m ladder rests on the wall at every lean, and the 12 m tower
lands on the deck in the low and middle ground cases, 33 of 33 positions; in the high ground case its ramp rests on the
wall's top edge and not on the deck (REC "Wall type B, massing round 2"). Owed: the
siege test prefab with the scripts, the showcase scene `mod_taom_rivendell_kitbash`, every settlement scene.

## 18. Glossary

| Term | Meaning |
|---|---|
| deck | The walking floor on top of a wall, tower or gatehouse |
| ground line | Z 0 of a kit piece: the ground level that the piece is designed for; the foundation goes below it |
| bay | One repeat of a wall's pattern between piers (Rivendell: 6.667 m, three tiles) |
| tile | One repeat of the tiling texture along a piece (Rivendell: 2.222 m) |
| texel density | UV units a metre of surface (Rivendell 0.45, the constant `TD`; REC "Names and build constants") |
| joint plane | The end plane of a piece where the next piece joins; both share one profile, so no seam shows |
| end section | The short length at the end of a piece that carries the shared joint profile (Rivendell gatehouse: 1.111 m, `rivendell_gatehouse_geometry.py` line 6) |
| redoubt | A small walled outwork in front of the wall line (DA's `Siege_12` with four nested walls, `catapult_platform.md` line 90) |
| the control, the control group | The Gondor castle pieces that the Rivendell job measured as its reference (`rivendell/control/`) |
| the heights study | `rivendell/build/v07/heights_prep/prefabs/tables.md`: the machine sizes from the Native prefabs |
| the mangonel research | `rivendell/build/v07/b1_concepts/journal_results.json`: the mangonel's footprint, arc and shot test |
| stage F, round N, stage 6, Kit step 1, kit set 3 | Names of build stages in the Rivendell record; they appear in section 17 and in record headings that are cited as sources |
| wall type A, wall type B | Two Rivendell wall designs; type A is approved |
| form B | The chosen shape of the Rivendell railing's stair opening, 5.33 m clear |
| GD1 to GD8 | The lead's gate decisions (REC lines 1063 to 1070): GD1 route B, with A until B is proven; GD2 the verifier's corrections; GD3 inner face ids end in 1 and the tree musts; GD4 the probe in a throwaway module; GD5 a fixed stone head above the leaves; GD6 stone behind the hinges, a stop, the head, the inner frame, the ram's room; GD7 three damage looks; GD8 carved wood leaves |
| route A to D | The four ways to get gates (7.4) |
| `num3` | `Lerp(0, 0.7, 1 - mean wall hit point ratio)`, `SiegeMissionPreparationHandler.cs:106`: the chance that a parapet part breaks before the assault, and the switch (0.1 or more) that disables machines on a breached side |
| primary siege weapon | A `SiegeWeapon` that implements `IPrimarySiegeWeapon`: the ladder, the siege tower and the ram (`SiegeLadder.cs:15`, `SiegeTower.cs:15`, `BatteringRam.cs:14`) |
| Environment Detail | The graphics option that sets the detail level distances (`static-mesh-lod-selection.md` line 12) |
| low, middle and high ground cases | The Rivendell job's three test cases of the ground under a wall in its machine check; their values are not in the sources of this file (UNVERIFIED) |
| the owner's placements | The places where the owner put the Gondor control walls in his scenes; the ground under them was read (REC "The machines' heights, measured") |
| lean | A raised ladder's angle from the vertical (default 19 degrees; the scene sets `UpperStateRotationDegree`) |
| ramp tip, reach | The height of an open siege tower ramp's free end over the tower's ground, and its horizontal distance from the hinge (heights study 4) |
| `sp_battle_set` | The Native prefab that carries the tags `battle_set` and `spawnpoint_set` (`editor_spawnpoints.xml` lines 508 to 512) |
| `TacticalPosition` | A script on a marker entity that the vanilla markers carry, with a `_width` (8.0 or 5.0) |
| `_barrier_ai_04x04m` | The prefab of an AI barrier box (placed at scale 4, 1, 4) that a ladder's or tower's `BarrierTagToRemove` shows or hides (`siege_wiring_spec.md` line 286) |
| the nine navmesh ids | The `WallSegment` fields `_properGroundOutside`, `_properGroundInside`, `_underDebrisOutside`, `_underDebrisInside`, `_overDebrisOutside`, `_overDebrisInside`, `_underDebrisGeneric`, `_overDebrisGeneric`, `_onSolidWallGeneric` + `NavmeshID` (`WallSegment.cs:23-48`) |
| face | One polygon of the scene navmesh; the scene gives it an id, and the siege code reads the id's last digit |
| the wall gate | The repo tool `tools/check_siege_wall_scripts.py` |
| `debris_holder` | An empty child entity tagged `reference` of a parapet part: the debris prefab appears at its frame when the part breaks |
| the editor | The Modding Kit's scene editor, and nothing else in this file |
| SCENE, GATE, GATEHOUSE, KIT | The four frames ("How to read", "Frames") |

## 19. Sources

**Appendix** (`E:/LOTRAOMAssets/_auto_workspace/kingdom_kits/_shared/scene_needs/`): `part_A_wall_line.md`,
`part_B_gatehouse_gates.md`, `part_C_scenes_kingdoms.md` (each ends with its "Sources read", every engine file and line
range); `settlement-scene-needs_v1.md`; the checks `check_facts.md` and `check_fresh_session.md`;
`record_conflicts_for_the_lead.md`; the `scripts` folder (section 13).

**Rivendell research** (`E:/LOTRAOMAssets/_auto_workspace/kingdom_kits/rivendell/`): `wiring/` (the files of WIRING and
`vanilla_gatehouse_frames.md`, `throw_line_of_sight.md`, `drop_by_level.md`, `vanilla_scene_ranking.md`);
`control/control_geometry.md`, `control/tower/control_tower.md`; `kit_full/delivery/` (SURV, `PREFAB_OWNER_STEPS.md`);
`kit_full/gates/` (`GATES_DECISIONS.md`, `swing/GATE_SWING.md`, `probe/PROBE_OWNER_STEPS.md`, `probe/PROBE_CHECK.md`);
`build/v07/heights_prep/prefabs/tables.md`; `build/v07/b1_concepts/journal_results.json`;
`build/v07/_src_castle/tools/oneoff/rivendell_gatehouse_geometry.py`.

**Repo documents:** [rivendell-siege-set.md](rivendell-siege-set.md) (list its headings with `grep -n '^## '`, never
read it whole), [kingdom-coverage.md](kingdom-coverage.md), [kingdom-kit-layout.md](kingdom-kit-layout.md),
[kingdom-kits-prompt.md](kingdom-kits-prompt.md), [campaign-to-mission-bridge.md](../reference/engine/campaign-to-mission-bridge.md),
[battle-scenes.md](../features/battle-scenes.md), and the documents linked in the text. The handovers that start from
this file: [mirkwood-kit-handover.md](mirkwood-kit-handover.md), [lothlorien-kit-handover.md](lothlorien-kit-handover.md).
**The game, read only:** `settlements.xml`, the scene files of `TAOM_Map`, `SandBox`, `SandBoxCore`, every `Prefabs`.
