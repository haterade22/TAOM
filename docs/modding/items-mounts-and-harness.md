# Mounts and harness

## What this file is

A rideable creature is two records in two files: a `<Monster>` carrying the skeleton wiring, hit
points, flags and hit box, and an `<Item>` of `Type="Horse"` whose `<Horse>` component points at that
Monster and holds the numbers a player reads (speed, manoeuvre, charge damage). Its barding is a
third record, an `<Item>` of `Type="HorseHarness"` whose `<Armor>` component must declare the same
`family_type` as the mount's Monster or the inventory screen refuses it with nothing on screen. All
three live in the Armory module; the troop rosters that hand them out live in this repo.

**Two paths, and the whole difference is animation.** Decide which one you are on before you open a
file: the cheap path is one seven-attribute element, the expensive one is five authoring phases.

| | Reskin of an existing rig | Bespoke creature |
|---|---|---|
| What you author | one `<Monster>` with `base_monster=`, one `<Item>`, roster rows | skeleton, clips, `action_types`, `action_sets`, `monster_usage_sets`, rider partial, then all of the above |
| Animation data | none, all inherited | Phases 1 to 5 of [creature-mount-authoring](../ai-includes/creature-mount-authoring.md) |
| Shipped example | `taom_war_ram`, 7 attributes <!-- measured: python -c "import xml.etree.ElementTree as E;print(len(E.parse('LOTRLOME_Armory/ModuleData/Monsters/LOTR/lotr_monster_war_ram.xml').getroot().find('Monster').attrib))" run from the Modules folder 2026-09-05 --> | `warg`, 42 attributes plus `<Capsules>` and `<Flags>` <!-- measured: same command against lotr_monster_warg.xml, plus the child tag list 2026-09-05 --> |
| Typical failure | a typo in a reference, a `family_type` mismatch, a naked or riderless mount | native access violations on spawn, on ridden death and on dismount |
| C# needed | none | usually |

If your mesh is skinned to a rig the game already ships (the horse rig, the elephant rig), you are on
the cheap path even when the animal looks nothing like the donor. That is the war ram, below.

## Where it lives and how it is registered

| Record | File | Registered by | Root | Entry | Engine class |
|---|---|---|---|---|---|
| Mount and harness items | `LOTRLOME_Armory/ModuleData/LOTRLOME_items/LOTRAOM_horses.xml` | `<XmlName id="Items" path="LOTRLOME_items/LOTRAOM_horses"/>` at `LOTRLOME_Armory/SubModule.xml:304` | `<Items>` | `<Item>` | `TaleWorlds.Core.ItemObject` |
| Creature definitions | `LOTRLOME_Armory/ModuleData/monsters.xml` plus seven files under `LOTRLOME_Armory/ModuleData/Monsters/LOTR/` <!-- measured: ls of that folder, 7 xml files, from the Modules folder 2026-09-05 --> | eight `<XmlName id="Monsters" .../>` rows, `LOTRLOME_Armory/SubModule.xml:216-295` <!-- measured: rg -c 'XmlName id="Monsters"' LOTRLOME_Armory/SubModule.xml 2026-09-05 --> | `<Monsters>` | `<Monster>` | `TaleWorlds.Core.Monster` |
| Who rides it | `Main/_Module/ModuleData/troops/troops_<culture>.xml` and `Main/_Module/ModuleData/equipmentsets/` | this repo's own `SubModule.xml` | `<NPCCharacters>`, `<EquipmentRosters>` | `<equipment slot="Horse">` | see [Troops](troops.md) |

These Armory files live in the game install, not the repo; a module reinstall reverts hand edits, so
land a repo-side validator gate with any fix. The rosters that consume them are tracked here, so you
can lose the item and keep every reference to it.

**Monsters load first, before anything else the game reads.** `Game.LoadBasicFiles` calls
`ObjectManager.LoadXML("Monsters")` ahead of skeleton scales, item modifiers, crafting pieces, body
properties and skill sets (`Game.cs:435-445`). So a `<Monster>` may never reference an item, a
culture or a character, none of which exists yet, while the other direction always works: by the time
`HorseComponent.Deserialize` resolves `monster="Monster.taom_war_ram"` (`HorseComponent.cs:150`), the
Monster is registered.

**`<XmlName id="Monsters">` is what makes a Monster exist. `soln_monsters` is a different mechanism.**
The Armory's `ModuleData/project.mbproj` carries four `soln_monsters` rows
<!-- measured: rg -c 'id="soln_monsters"' LOTRLOME_Armory/ModuleData/project.mbproj from the Modules folder 2026-09-05 -->
that feed the native side, not the managed object manager; the war ram, the mumakil, the chariot and
the spider have no such row and load anyway. The file's own comment at
`LOTRLOME_Armory/ModuleData/project.mbproj:17-23` says not to add one "to be safe". Both mechanisms
are in [Submodule and registration](submodule-and-registration.md).

## Attributes

### `<Monster>`

`Monster.Deserialize` reads 76 attributes, seven indexed bone families and five child elements
<!-- measured: python -c "import sys;sys.path.insert(0,'tools');import check_handbook_attributes as C;from pathlib import Path;src=(Path(C.DEFAULT_DUMP_ROOT)/'Core/TaleWorlds.Core/TaleWorlds.Core/Monster.cs').read_text(errors='replace');r=C.extract_reads(src,'Deserialize');print(len(r['attributes']),len(r['prefixes']),len(r['elements']))" 2026-09-05 -->,
grouped below by what they do rather than by file order. `id` is not in that count: the base class
reads it unguarded, so a `<Monster>` without one is a null reference at load (`MBObjectBase.cs:58-62`).

**Read the "Default when absent" column as two answers.** Every default here is guarded behind "does
this element have a `base_monster`". With one, an attribute you leave out keeps the parent's value.
Without one, it falls to the bare default in the table: the invalid index for most bones, zero for
most numbers.

<!-- engine-table type="TaleWorlds.Core.Monster" file="Core/TaleWorlds.Core/TaleWorlds.Core/Monster.cs" method="Deserialize" inert="preliminary_collision_capsule_radius_multiplier,rider_preliminary_collision_capsule_height_multiplier,rider_preliminary_collision_capsule_height_adder" -->

| Attribute | Type | Required | Default when absent | What it does | Read at (file:line) |
|---|---|---|---|---|---|
| `base_monster` | id of a Monster already loaded | no | this is a from-scratch creature and every field starts at its bare default | Copies the parent's flags, action sets, monster usage, capsules and every attribute you do not name. One attribute that decides most of the file | `Monster.cs:193` |
| `action_set` | action-set id | no | inherited, else none | Which animation set binds to this creature. Parsed before any bone attribute, because bone names are resolved against the action set's skeleton, not against your mesh | `Monster.cs:304` |
| `female_action_set` | action-set id | no | inherited, else none | The female variant of the same | `Monster.cs:309` |
| `monster_usage` | usage-set id | no | inherited, else empty | The behaviour vocabulary: which action the engine fires to rear, to be struck, to jump. Inheriting this inherits the donor's behaviour, which is the reskin trap below | `Monster.cs:314` |
| `sound_and_collision_info_class` | class name | no | inherited, else none | Footstep, impact and voice class. Legal values come from the native collision and voice definitions and are not enumerated in the managed decompile | `Monster.cs:384` |

<!-- engine-table type="TaleWorlds.Core.Monster" file="Core/TaleWorlds.Core/TaleWorlds.Core/Monster.cs" method="Deserialize" -->

| Attribute | Type | Required | Default when absent | What it does | Read at (file:line) |
|---|---|---|---|---|---|
| `hit_points` | int | no | inherited, else 1 | The creature's health. A mount's health comes from here, never from the item: `HorseComponent.HitPoints` is a passthrough to `Monster.HitPoints` (`HorseComponent.cs:34`). The war ram sets 160 where the vanilla horse has 200 <!-- measured: the hit_points attributes of Monster taom_war_ram and of Monster horse in Native/ModuleData/monsters.xml 2026-09-05 --> | `Monster.cs:336` |
| `weight` | int | no | inherited, else 1 | Mass for collisions and knockdowns. The war ram sets 320 where the horse has 400 <!-- measured: the weight attributes of the same two Monsters 2026-09-05 --> | `Monster.cs:327` |
| `absorbed_damage_ratio` | float, clamped to zero or more | no | inherited, else 1.0 | Blanket damage multiplier for this creature | `Monster.cs:368` |
| `family_type` | int | no | inherited, else 0 (human) | The harness compatibility key. See the family table under `<Armor>` below | `Monster.cs:491` |
| `num_paces` | int | no | inherited, else 0 | Number of gait blends. Mount machinery indexes the gallop at 5, so a rideable creature wants 6. The runtime effect itself is native and unverified | `Monster.cs:341` |
| `arm_length`, `arm_weight` | float | no | inherited, else 0 | Rider-side values, on the humanoid Monster, not on the mount. Managed code only packs them into the spawn data, so the exact reach and inertia formulas are native | `Monster.cs:467, 472` |
| `walking_speed_limit`, `crouch_walking_speed_limit` | float | no | inherited; with no base, crouch falls back to the walking value | Gait speed caps. Units are not converted anywhere in managed code | `Monster.cs:346, 351` |
| `jump_acceleration`, `jump_speed_limit` | float | no | inherited, else 0 | Jump impulse and cap. The war ram sets acceleration 7.5 where the horse has 6.5 | `Monster.cs:363, 477` |
| `relative_speed_limit_for_charge` | float | no | inherited, else no limit | How fast the creature may be moving and still commit to a charge. The war ram sets 4.0 where the horse has 4.3 | `Monster.cs:486` |

<!-- engine-table type="TaleWorlds.Core.Monster" file="Core/TaleWorlds.Core/TaleWorlds.Core/Monster.cs" method="Deserialize" -->

| Attribute | Type | Required | Default when absent | What it does | Read at (file:line) |
|---|---|---|---|---|---|
| `standing_chest_height`, `standing_pelvis_height`, `standing_eye_height`, `crouch_eye_height` | float, metres | no | inherited, else 0 | The creature's own proportions on foot | `Monster.cs:419, 424, 429, 434` |
| `mounted_eye_height` | float | no | inherited, else 0 | Rider-side: where this creature's eyes sit once it is on something | `Monster.cs:439` |
| `eye_offset_wrt_head`, `first_person_camera_offset_wrt_head` | Vec3 written `x, y, z` | no | inherited, else (0.01, 0.01, 0.01) | Camera anchors relative to the head bone. Fewer than three comma-separated numbers throws during load | `Monster.cs:453, 462` |
| `rider_camera_height_adder`, `rider_eye_height_adder`, `rider_body_capsule_height_adder`, `rider_body_capsule_forward_adder` | float | no | inherited, else 0 | Mount-side: how the rider is seated on THIS animal. These are the seat-correction knobs, and putting them here rather than on the rider's Monster is what keeps the fix from moving every member of that race on foot | `Monster.cs:389, 444, 394, 399` |
| `rein_skeleton`, `rein_collision_body` | resource name | no | inherited, else none | The rein geometry and its physics body | `Monster.cs:541, 543` |
| `rein_handle_left_local_pos`, `rein_handle_right_local_pos` | Vec3 `x, y, z` | no | inherited, else zero | Where the rider's hands grip | `Monster.cs:531, 536` |
| `rein_handle_bone`, `rein_collision_1_bone`, `rein_collision_2_bone`, `rein_head_bone`, `rein_head_right_attachment_bone`, `rein_head_left_attachment_bone`, `rein_right_hand_bone`, `rein_left_hand_bone` | bone name | no | inherited, else invalid index | The eight bones the rein simulation runs on | `Monster.cs:552-559` |

Vanilla pairs `Mountable` with all twelve rein attributes without exception: the horse declares 12
<!-- measured: python -c "import xml.etree.ElementTree as E;m=[x for x in E.parse('Native/ModuleData/monsters.xml').getroot().findall('Monster') if x.get('id')=='horse'][0];print(len([k for k in m.attrib if k.startswith('rein')]))" from the Modules folder 2026-09-05 -->.
TAOM's declared counts are spider 5, warg 5, fell warg 5, elephant 0, mumakil 0, chariot 12; the war
ram declares none and inherits all twelve
<!-- measured: a loop over LOTRLOME_Armory/ModuleData/Monsters/LOTR/*.xml counting attributes whose name starts with "rein" 2026-09-05 -->.
Gotcha 18 of [creature-mount-authoring](../ai-includes/creature-mount-authoring.md) records that
v1.4.8 changed the native rein path that runs when a mounted agent dies, that an incomplete rein
surface is a TAOM-only shape, and that no tool gates it.

<!-- engine-table type="TaleWorlds.Core.Monster" file="Core/TaleWorlds.Core/TaleWorlds.Core/Monster.cs" method="Deserialize" -->

| Attribute | Type | Required | Default when absent | What it does | Read at (file:line) |
|---|---|---|---|---|---|
| `pelvis_bone`, `spine_lower_bone`, `spine_upper_bone`, `neck_root_bone`, `head_look_direction_bone`, `thorax_look_direction_bone`, `body_rotation_reference_bone` | bone name | no | inherited, else invalid index | The look, lean and turn chain | `Monster.cs:499-504, 550` |
| `right_upper_arm_bone`, `left_upper_arm_bone`, `main_hand_bone`, `off_hand_bone`, `main_hand_item_bone`, `off_hand_item_bone`, `main_hand_item_secondary_bone`, `off_hand_item_secondary_bone`, `off_hand_shoulder_bone` | bone name | no | inherited, else invalid index | Where weapons and shields attach | `Monster.cs:505-506, 514-520` |
| `primary_foot_bone`, `secondary_foot_bone`, `right_foot_ik_end_effector_bone`, `left_foot_ik_end_effector_bone`, `right_foot_ik_tip_bone`, `left_foot_ik_tip_bone` | bone name | no | inherited, else invalid index | Foot placement and inverse kinematics | `Monster.cs:523-528` |
| `rider_sit_bone` | bone name | no | inherited, else invalid index | Mount-side: the bone the rider is parented to. Vanilla horse uses `horsespine2` | `Monster.cs:551` |
| `fall_blow_damage_bone`, `terrain_decal_bone_0`, `terrain_decal_bone_1` | bone name | no | inherited, else invalid index | Fall damage origin and the two ground-decal bones | `Monster.cs:507-509` |
| `ragdoll_bone_to_check_for_corpses_<n>`, `ragdoll_fall_sound_bone_<n>`, `ragdoll_stationary_check_bone_<n>`, `move_adder_bone_<n>`, `splash_decal_bone_<n>`, `blood_burst_bone_<n>`, `bones_to_modify_on_sloping_ground_<n>` | bone name, numbered from `_0` | no | inherited per slot, else empty | Indexed families. They must be contiguous from zero: the loop stops at the first index that does not resolve, so a gap or a misspelling at `_3` throws away `_4` and everything after it. Caps are 11, 4, 8, 7, 6, 8 and 7 entries | `Monster.cs:497, 498, 510-513, 545` |
| `hand_num_bones_for_ik`, `foot_num_bones_for_ik`, `front_bone_to_detect_ground_slope_index`, `back_bone_to_detect_ground_slope_index` | sbyte | no | inherited, else 0 for the counts and -1 for the indices | How many bones the hand and foot IK chains use, and which slope bones to sample. These four are parsed with a bare `Parse`, not `TryParse`, so bad text throws | `Monster.cs:521, 529, 546, 548` |
| `preliminary_collision_capsule_radius_multiplier`, `rider_preliminary_collision_capsule_height_multiplier`, `rider_preliminary_collision_capsule_height_adder` | string | no | not applicable | Read but has no effect: the preliminary-capsule feature was removed and nothing consumes these | `Monster.cs:404, 409, 414` |

### `<Horse>` inside `<ItemComponent>`

This is the component that turns an item into a mount. It reads nine attributes of its own plus four
more on its child elements, and six child element names
<!-- measured: python -c "import sys;sys.path.insert(0,'tools');import check_handbook_attributes as C;from pathlib import Path;src=(Path(C.DEFAULT_DUMP_ROOT)/'Core/TaleWorlds.Core/TaleWorlds.Core/HorseComponent.cs').read_text(errors='replace');r=C.extract_reads(src,'Deserialize');print(len(r['attributes']),len(r['elements']))" 2026-09-05 -->.

<!-- engine-table type="TaleWorlds.Core.HorseComponent" file="Core/TaleWorlds.Core/TaleWorlds.Core/HorseComponent.cs" method="Deserialize" -->

| Attribute | Type | Required | Default when absent | What it does | Read at (file:line) |
|---|---|---|---|---|---|
| `monster` | Monster reference, written with the prefix `Monster.<id>` | in practice yes | none, and the mount has no skeleton | Joins the item to its creature. A value with no dot throws `MBInvalidReferenceException` (`MBObjectManager.cs:1524-1528`); a prefixed but unknown id does not throw, it registers an empty placeholder Monster (`MBObjectManager.cs:718-731`), which reads in game as a broken or invisible mount | `HorseComponent.cs:150` |
| `speed` | int | no | 0 | Top gallop speed | `HorseComponent.cs:146` |
| `maneuver` | int | no | 0 | Turning agility | `HorseComponent.cs:144` |
| `charge_damage` | int | no | 0 | Damage of a trample or couched impact | `HorseComponent.cs:145` |
| `body_length` | int | no | 0 | Hundredths of the authored size, and the only scale knob a mount has. 100 is identity. Zero is not a small mount: the engine skips the scale call entirely (`Mission.cs:4026-4032`) | `HorseComponent.cs:147` |
| `is_mountable` | bool | no | false | False makes it a herd animal rather than a mount | `HorseComponent.cs:148` |
| `is_pack_animal` | bool | no | false | Mule behaviour. Mountable and pack-animal together is not a mount | `HorseComponent.cs:149` |
| `extra_health` | int | no | 0 | Flat bonus on top of the Monster's hit points | `HorseComponent.cs:151` |
| `skeleton_scale` | bare id from `Native/ModuleData/skeleton_scales.xml` | no | none | Per-bone resize applied at mission start, a different mechanism from `body_length` | `HorseComponent.cs:152-156` |

The `<Item>` around this component takes the same attributes as any other item, so its reference
lives in [Items: armour](items-armor.md). Four of them decide how a mount behaves outside combat:
`Type="Horse"` is the sole authority for the slot it occupies (`ItemObject.cs:625-638`), `mesh` names
the multi-mesh drawn for it (`ItemObject.cs:503`), `culture` written as `culture="Culture.erebor"`
steers shop stock and AI picks (`ItemObject.cs:540`), and `value` overrides the computed price
(`ItemObject.cs:672`). Leave `value` out and the price follows an effectiveness score that multiplies
charge, speed, manoeuvre, body length and the Monster's hit points together (`ItemObject.cs:945`).

One attribute in the shipped data is read by nothing: `subtype="horse"` on `taom_war_ram_a`. The
string does not occur in the whole decompiled `Core` tree
<!-- measured: grep -rn '"subtype"' on the v1.4.8 Core decompile tree, zero hits 2026-09-05 -->,
so do not copy it into new entries and do not expect it to filter anything.

### `<Armor>` inside `<ItemComponent>` on a `Type="HorseHarness"` item

A harness is an armour item using the same component as a helmet, which is why most of the list below
is human armour and belongs to [Items: armour](items-armor.md). The rows that matter for barding are
the first six.

<!-- engine-table type="TaleWorlds.Core.ArmorComponent" file="Core/TaleWorlds.Core/TaleWorlds.Core/ArmorComponent.cs" method="Deserialize" inert="no_slim" -->

| Attribute | Type | Required | Default when absent | What it does | Read at (file:line) |
|---|---|---|---|---|---|
| `family_type` | int | in practice yes | 0, the human family | Must equal the mount Monster's `family_type` or the harness cannot be fitted. See the failure mode under Gotchas | `ArmorComponent.cs:153` |
| `body_armor` | int | no | 0 | The barding's protection value. On a harness this is the only one of the four armour numbers that does anything | `ArmorComponent.cs:150` |
| `maneuver_bonus`, `speed_bonus`, `charge_bonus` | int | no | 0 | Flat additions to the MOUNT's stats while this piece is worn (`EquipmentElement.cs:379, 399, 419`). They do nothing on human armour | `ArmorComponent.cs:154, 155, 156` |
| `mane_cover_type` | `None`, `Type1`, `Type2` or `All`, parsed ignoring case | no | `None` | Hides the mount's mane meshes under the barding. `all` works | `ArmorComponent.cs:192` |
| `tail_cover_type` | `None` or `All`, parsed ignoring case | no | `None` | Same for the tail | `ArmorComponent.cs:193` |
| `reins_mesh` | mesh name | no | empty | Reins drawn with this harness. A `_rope` suffixed variant is derived automatically (`ArmorComponent.cs:113`) | `ArmorComponent.cs:195` |
| `material_type` | `None`, `Cloth`, `Leather`, `Chainmail` or `Plate`, parsed CASE SENSITIVELY | no | `None` | Hit sounds and impact particles. `leather` throws where `Leather` works | `ArmorComponent.cs:157` |
| `head_armor`, `leg_armor`, `arm_armor` | int | no | 0 | Human armour slots, no effect on a harness | `ArmorComponent.cs:149, 151, 152` |
| `covers_head`, `covers_body`, `covers_hands`, `covers_legs`, `hair_cover_type`, `beard_cover_type`, `has_gender_variations`, `body_mesh_type`, `body_deform_type`, `stealth_factor` | see [Items: armour](items-armor.md) | no | see that chapter | Human armour behaviour. The cover flags are a visibility mask and are inverted from how they read | `ArmorComponent.cs:196-199, 190, 191, 160, 165, 178, 194` |
| `no_slim` | bool | no | false | Read but has no effect: nothing in the decompile consumes it | `ArmorComponent.cs:216` |

Vanilla's own `family_type` legend is a comment in the shipped data at
`SandBox/ModuleData/monsters.xml:3-11`: 0 human, 1 horse, 2 camel, 3 cow, 4 goose, 5 hog, 6 sheep,
7 hare. TAOM extends it: the spider, the warg, the fell warg and the war ram are all family 1, the
chariot is 4 and the elephant and mumakil are 10. Of the Armory's 34 harness items, 31 are family 1,
two are family 4 and one is family 10, and none is missing the attribute
<!-- measured: an ElementTree sweep of LOTRLOME_Armory/ModuleData/LOTRLOME_items/**/*.xml counting Type="HorseHarness" items by their <Armor family_type> 2026-09-05 -->.
Sharing family 1 is a deliberate trade (ram barding fits horses in the player's inventory and horse
caparisons fit the ram): family 1 is the number that carries vanilla's rider-death, dismount and
rider-fall surface, and the spider's isolated family 11 had no such surface at all
([lotrlome-war-ram-changes](../reference/lotrlome-war-ram-changes.md), lines 166-172).

## Child elements

### `<Flags>` and `<Capsules>` on a `<Monster>`

<!-- engine-table type="TaleWorlds.Core.Monster" file="Core/TaleWorlds.Core/TaleWorlds.Core/Monster.cs" method="Deserialize" -->

| Element or attribute | Type | Required | Default when absent | What it does | Read at (file:line) |
|---|---|---|---|---|---|
| `<Flags>` | one boolean attribute per agent flag, for example `<Flags Mountable="true" CanRear="true" />` | no | flags stay as inherited | Presence of the element wipes the inherited set and rebuilds it from the attributes present. To turn a flag OFF you must write `FlagName="false"`; omitting it is not enough, and any text other than the literal `false` counts as ON, so `CanRide="0"` means true | `Monster.cs:569-575` |
| `<Capsules>` | wrapper | no | inherited wholesale from `base_monster` | Holds the hit-box capsules. Only the capsules you actually supply overwrite the inherited ones | `Monster.cs:583` |
| `<body_capsule>`, `<crouched_body_capsule>` | `pos1`, `pos2`, `radius` | no | body capsule | The standing and crouched hit boxes. Routing is by the FIRST LETTER of the element name: a name starting with `c` writes the crouched capsule, anything else writes the standing one, so `<bodycapsule>` still lands somewhere and never reports the typo | `Monster.cs:589, 621-631` |
| `<preliminary_collision_capsule>` | as above | no | none | Dead feature: a name starting with `p` only fires a failed assertion | `Monster.cs:589, 621-624` |
| `pos1`, `pos2`, `radius` | Vec3 `x,y,z` and float | no | (0, 0, 0.01), (0, 0, 0) and 0.01 | The capsule's top point, bottom point and thickness, in metres in the creature's local space. Parsing is all or nothing: one malformed value discards the whole capsule and leaves the inherited or zero value with no message | `Monster.cs:597, 605, 613` |

A capsule that is too small lets arrows pass through the model; one that is too big gets the creature
shot from a metre away and jams it in doorways. It scales with the agent, so a mount at
`body_length="300"` gets a 3x capsule from a 1x definition, which is how the mumakil left enemies
running into its visual mesh before hitting anything ([mumakil](../features/mumakil.md), lines 70-77).

### `<Materials>` and `<AdditionalMeshes>` on a `<Horse>`

<!-- engine-table type="TaleWorlds.Core.HorseComponent" file="Core/TaleWorlds.Core/TaleWorlds.Core/HorseComponent.cs" method="Deserialize" -->

| Element or attribute | Type | Required | Default when absent | What it does | Read at (file:line) |
|---|---|---|---|---|---|
| `<Materials>` | wrapper for `<Material>` rows | no | one material, the mesh's own | Coat and hide variation across a herd of one item id | `HorseComponent.cs:159` |
| `<Material>`, `name` | element with one attribute | `name` yes | none | One material variant. `name` is dereferenced without a guard, so a `<Material>` without it is a null reference at load | `HorseComponent.cs:163, 167` |
| `<MeshMultipliers>`, `<MeshMultiplier>`, `mesh_multiplier`, `percentage` | hexadecimal string and float | no | 0 and 0 | Weighted bands within a material. `mesh_multiplier` is parsed as base 16, `percentage` as a float, and the rows are sorted ascending by percentage | `HorseComponent.cs:171, 177-180` |
| `<AdditionalMeshes>`, `<Mesh>` | `<Mesh name="..." affected_by_cover="..."/>` | no | none | Extra meshes bolted onto the mount: manes, blankets, tusks. A `<Mesh>` with no `name` is skipped in silence | `HorseComponent.cs:190, 196` |
| `affected_by_cover` | bool | no | false | Marks a mesh to hide when a harness with a matching mane or tail cover is fitted. Do not trust it: the engine assigns the PARSE SUCCESS flag instead of the parsed value, so `affected_by_cover="false"` ends up true and only unparseable text ends up false | `HorseComponent.cs:199-201` |

## Worked example

The war ram is the whole reskin path in one element, and its file opens by saying why.

<!-- excerpt file="LOTRLOME_Armory/ModuleData/Monsters/LOTR/lotr_monster_war_ram.xml" -->

```xml
  This is the cheapest creature mount TAOM ships, and deliberately so. Both ram body meshes
  (sk_eb_goat_a / sk_eb_goat_b) and all eight bardings are skinned to the STOCK VANILLA HORSE
  SKELETON, bone for bone: horsepelvis, horsespine1-3, horseneck1-2, horse_head, horsel/rfemur,
  horsetail1-3, including the _nub_notused bones.
```

<!-- example file="LOTRLOME_Armory/ModuleData/Monsters/LOTR/lotr_monster_war_ram.xml" id="taom_war_ram" -->

```xml
<Monsters>
	<Monster
		id="taom_war_ram"
		base_monster="horse"
		action_set="as_horse"
		weight="320"
		hit_points="160"
		jump_acceleration="7.5"
		relative_speed_limit_for_charge="4.0" />
</Monsters>
```

1. **`base_monster="horse"`** brings in `Mountable`, `CanRear`, `RunsAwayWhenHit`, `CanCharge`,
   `CanWander`, `family_type="1"`, `monster_usage="horse"`, `num_paces="6"`, every bone name, the
   ground-slope block and all twelve rein attributes.
2. **`action_set="as_horse"`** reuses vanilla's horse clips, so nothing is authored, and
   `as_horse_map` and `as_horse_town_and_village` already exist (a missing `_map` or
   `_town_and_village` child is the elephant's native access-violation class).
3. **`hit_points`, `weight`, `jump_acceleration`, `relative_speed_limit_for_charge`** are the
   balance numbers. Change these, leave the rest inherited.

The item that makes it rideable:

<!-- example file="LOTRLOME_Armory/ModuleData/LOTRLOME_items/LOTRAOM_horses.xml" id="taom_war_ram_a" -->

```xml
    <Item
        id="taom_war_ram_a"
        name="{=taom_war_ram_a}War Ram"
        mesh="sk_eb_goat_a"
        culture="Culture.erebor"
        subtype="horse"
        item_category="war_horse"
        value="950"
        weight="300"
        difficulty="0"
        is_merchandise="true"
        Type="Horse">
        <ItemComponent>
            <Horse
                monster="Monster.taom_war_ram"
                maneuver="75"
                speed="42"
                charge_damage="18"
                body_length="100"
                is_mountable="true"
                extra_health="20" />
        </ItemComponent>
        <Flags Civilian="true" />
    </Item>
```

1. **`speed`, `maneuver`, `charge_damage`** are the three numbers a balance pass touches.
2. **`monster="Monster.taom_war_ram"`** is the join to the element above, and the prefix is required.
3. **`body_length="100"`** means "ship at authored size", and it is not mount-only: read the resize
   recipe before changing it.

Its barding, the third record:

<!-- example file="LOTRLOME_Armory/ModuleData/LOTRLOME_items/LOTRAOM_horses.xml" id="taom_ram_barding_light_a" -->

```xml
    <Item
        id="taom_ram_barding_light_a"
        name="{=taom_ram_barding_light_a}[Erebor] Ram Barding I"
        mesh="sk_eb_goat_bard_light_a"
        culture="Culture.erebor"
        weight="12"
        is_merchandise="true"
        appearance="0.65"
        Type="HorseHarness">
        <ItemComponent>
            <Armor
                body_armor="20"
                mane_cover_type="all"
                family_type="1"
                modifier_group="leather"
                material_type="Leather" />
        </ItemComponent>
        <Flags Civilian="true" />
    </Item>
```

1. **`family_type="1"`** matches the ram's inherited family. Get it wrong and the harness is
   unequippable with nothing on screen to say why.
2. **`body_armor="20"`** is the protection number and the only armour value a harness uses, and
   **`mane_cover_type="all"`** hides the mane meshes the barding covers.

## Recipes: Add / Modify / Delete

### Add

**A harness for a mount that already exists.**

1. Read the mount's `family_type` off its `<Monster>`. If that element has a `base_monster` and no
   `family_type` of its own, follow the chain until you find one; the war ram's is the horse's 1.
2. Add an `<Item>` to `LOTRLOME_Armory/ModuleData/LOTRLOME_items/LOTRAOM_horses.xml` beside the
   bardings already there for that mount, copying the shape of `taom_ram_barding_light_a` above.
3. Set `Type="HorseHarness"`, `mesh` to your barding mesh, and `<Armor family_type>` to step 1.
4. Set `body_armor` (protection), `material_type` (hit sounds, case sensitive), and
   `mane_cover_type` or `tail_cover_type` if the barding covers those meshes. Give it a `{=key}`
   name so it can be translated and a `culture=` so it stocks in the right shops.
5. Hand it out with `<Equipment slot="HorseHarness" id="Item.<your id>" />` beside the `slot="Horse"`
   row in the roster, never on its own: a harness with no mount in the same set is unequipped on the
   next inventory transfer (`SPInventoryVM.cs:3919-3922`).

Check: `python tools/validate_moduledata.py --code MISSING_HARNESS_FAMILY_TYPE --code HARNESS_FAMILY_MISMATCH`
Takes effect: full game restart
Code: No code changes needed

**A reskinned mount, on a rig the game already ships.**

1. Confirm the mesh is skinned to the donor rig, bone for bone. If it is not, you are on the bespoke
   path and this recipe does not apply.
2. Create `LOTRLOME_Armory/ModuleData/Monsters/LOTR/lotr_monster_<name>.xml` with a `<Monsters>` root
   and one `<Monster>` carrying `base_monster=`, `action_set=` and only the numbers you are changing.
   Do not restate inherited attributes, and do not add a `<Flags>` child unless you mean to rebuild
   the whole flag set.
3. Register it with an `<XmlNode>` holding `<XmlName id="Monsters" path="Monsters/LOTR/lotr_monster_<name>"/>`
   in `LOTRLOME_Armory/SubModule.xml`, copying the `<IncludedGameTypes>` block from the row above.
   Do not add a `soln_monsters` row to `project.mbproj`.
4. Add the `Type="Horse"` `<Item>` to `LOTRAOM_horses.xml` with `monster="Monster.<your monster id>"`,
   `is_mountable="true"` and `body_length="100"`.
5. Put `<equipment slot="Horse" id="Item.<your item id>" />` in every battle `EquipmentRoster` of
   every troop that rides it, under `Main/_Module/ModuleData/troops/` and `.../equipmentsets/`. A
   troop with the mount in one set and not another spawns mounted only some of the time.
6. If the riders are dwarves, add the item id to `WAR_RAM_MOUNT_IDS` in `tools/taom_schema.py:186`.
   Every other mount handed to a dwarf is a `MOUNTED_DWARF` error on purpose, and the list is pinned
   by id rather than by prefix so a new one gets looked at.
7. Restart the game fully and check the mount spawns WITH its rider. A riderless mount means the
   skeleton resource did not ship, not that the data is wrong.

Check: `python tools/validate_moduledata.py` then `python tools/audit_mbproj_registration.py --all`
Takes effect: full game restart
Code: No code changes needed for a reskin. A bespoke creature needs a behaviour tree and usually a Harmony patch, so its trailer is `Code changes required in Main/Features/`, and its authoring sequence is [creature-mount-authoring](../ai-includes/creature-mount-authoring.md) Phases 1 to 5

### Modify

**Resize a mount.**

1. On-screen size is `body_length / 100` of the authored mesh, and `body_length` is on the
   `Type="Horse"` `<Item>`.
2. Before changing it: the scale block in `Mission.BuildAgent` is keyed on
   `EquipmentIndex.ArmorItemEndSlot`, the same enum value as `EquipmentIndex.Horse`
   (`EquipmentIndex.cs:21, 23`), it has no mount-only guard, and `BuildAgent` runs for the rider as
   well with the Horse item still in the rider's spawn equipment (`Mission.cs:4026-4032`).
   **Any value other than 100 scales the rider too.**
3. So either accept the shared scale (the mumakil at 300 and the wargs at 110 and 115 do) or leave
   the mount at 100 and rescale the mesh instead.
4. Re-check the hit box: the capsule scales with the agent, so a 1x capsule that fits becomes a 3x
   capsule that may not cover a longer body.
5. Tune the seat with `rider_eye_height_adder`, `rider_body_capsule_height_adder` and
   `rider_camera_height_adder` on the MOUNT's Monster, never the rider's, unless you want every
   member of that race moved on foot as well.

Check: `python tools/validate_moduledata.py --code MOUNTED_DWARF` then a custom battle, looking for the rider sitting on the mount rather than inside it
Takes effect: full game restart
Code: No code changes needed

**Retune a mount or a harness.**

1. Mount speed, manoeuvre and charge are on the `<Horse>` component; hit points are `hit_points` on
   the `<Monster>` plus the item's `extra_health`.
2. Harness protection is `body_armor` on `<Armor>`, and `maneuver_bonus`, `speed_bonus` and
   `charge_bonus` there add to the mount's own numbers.
3. Leave `value` off unless the price must be fixed: without it, price follows the stats through the
   effectiveness formula at `ItemObject.cs:945`.
4. Keep a barding ladder monotonic. TAOM's eight ram bardings run 20, 24, 30, 34, 40, 44, 50, 54,
   each step backed by measured mesh coverage
   ([lotrlome-war-ram-changes](../reference/lotrlome-war-ram-changes.md), lines 143-158).

Check: `python tools/validate_moduledata.py`
Takes effect: full game restart
Code: No code changes needed

### Delete

**Retire a mount.** Order matters: references first, the definition last.

1. Find every reference before deleting anything. For the war ram that is 12 rows in
   `Main/_Module/ModuleData/troops/troops_erebor.xml`, 3 in
   `Main/_Module/ModuleData/equipmentsets/taom_equipment_sets_erebor.xml`, 2 in
   `taom_career_starting_equipment.xml` and 1 in
   `Main/_Module/ModuleData/culture_marketplace/culture_marketplace_config.xml`
   <!-- measured: rg -c "taom_war_ram_a" against each of those four files 2026-09-05 -->,
   plus the item's name string in the Armory's `Languages/` folders.
2. Remove the `slot="Horse"` rows and the `slot="HorseHarness"` rows that went with them. A harness
   left with no mount in the same set is stripped on the next inventory transfer, which reads as a
   bug rather than as a leftover. Remove the marketplace stock and career starting-equipment rows too.
3. Remove the `Type="Horse"` `<Item>` and its bardings from `LOTRAOM_horses.xml`.
4. Only now remove the `<Monster>` file and its `<XmlName id="Monsters">` row in
   `LOTRLOME_Armory/SubModule.xml`. If the rider was a dwarf, take the id back out of
   `WAR_RAM_MOUNT_IDS` in `tools/taom_schema.py`.
5. Sweep for what you missed. A deletion that leaves references behind is a failure TAOM has already
   shipped: a seven-commit Armory reorganisation broke 212 item references across 159 consumers and
   was caught from a screenshot rather than from a gate
   ([rca-armoury-keyforce-cleanup-2026-09-01](../reviews/rca-armoury-keyforce-cleanup-2026-09-01.md)).

Check: `python tools/audit_item_refs.py --show-locations` then `python tools/validate_moduledata.py --code BROKEN_ITEM_REF`
Takes effect: full game restart, and existing saves keep any copy already in a player's inventory
Code: No code changes needed unless a feature names the id. The war ram's is pinned in `Main/Features/WarRam/WarRamConfig.cs`

## Gotchas: what fails silently and what crashes

- **A `family_type` mismatch has no error message.** The inventory screen compares the mount's
  `Monster.FamilyType` against the harness's `ArmorComponent.FamilyType` and returns false
  (`SPInventoryVM.cs:4112`); a harness placed by an equipment-set XML, which bypasses that check, is
  force-unequipped on the next transfer (`SPInventoryVM.cs:3923`). A missing `family_type` is worse
  than a wrong one, because 0 is the human family and fits nothing.
- **A typo in `monster="Monster.x"` does not crash and is not gated.** An unknown id registers an
  empty placeholder Monster (`MBObjectManager.cs:718-731`), so the mount loads with no skeleton and
  no hit points. The repo's reference sweep resolves `Item.`, `NPCCharacter.`, `Culture.` and
  `PartyTemplate.` prefixes only (`tools/taom_schema.py:161-167`), so nothing checks `Monster.`
  references. Read the id back off the Monster file by eye.
- **A bad `base_monster` silently kills the rest of the file.** The reference is dereferenced
  immediately after lookup (`Monster.cs:205-206`) and the load walk sits inside an empty catch
  (`MBObjectManager.cs:786-796`), so every `<Monster>` after the broken one is never registered: a
  batch of creatures that do not exist, not a crash. The named monster must appear EARLIER in the
  merged document, meaning earlier in the same file, in an earlier `<XmlName id="Monsters">` row, or
  in an earlier module. The same four attributes that throw on bad text
  (`hand_num_bones_for_ik`, `foot_num_bones_for_ik` and the two slope indices, `Monster.cs:521, 529,
  546, 548`) land you in this same silent abort.
- **The same monster id in two modules merges, it does not replace.** Attributes from the later
  module are copied onto the earlier node one at a time (`MBObjectManager.cs:799-817`), so you
  cannot remove an inherited attribute by leaving it out; you can only overwrite its value.
- **A reskin inherits the donor's behaviour, not just its look.** The inherited `monster_usage`
  means the engine fires the donor's actions on your creature. On the horse rig, `act_horse_rear` is
  typed `actt_rear` and `Agent.Mount` refuses a mount whose current action type is Rear, and
  `act_horse_strike_front` and `_back` sit in the band the engine reads as BEING struck. Vanilla
  horses have no attack animation at all; their only offensive action is `act_horse_kick`
  ([custom_creature_xml](../community/bannerlordmodding-lt/guides/custom_creature_xml.md), lines
  326-366).
- **A riderless mount is an asset failure, not a data failure.** A mesh re-export that ships without
  the creature's skeleton resource leaves agent creation with nothing to attach the rider to, and
  there is no crash and no log line (gotcha 17 of
  [creature-mount-authoring](../ai-includes/creature-mount-authoring.md)).
- **`affected_by_cover="false"` does not mean false.** The engine stores the parse success flag
  (`HorseComponent.cs:201`), so the only way to get false is text that does not parse. Do not rely
  on this attribute either way.
- **A backup file in an item folder loads as data.** Folder-registered item paths take every `.xml`
  in the folder, so `LOTRAOM_horses.bak.xml` would register a second copy of every id in it. Use the
  convention that replaces the extension instead, `LOTRAOM_horses.xml.bak-<topic>`, which is what the
  shipped sidecars do ([module-backup-sweep](../reference/module-backup-sweep.md)).
- **The gates do less than their names suggest.** `python tools/verify_mount_assets.py` knows three
  creatures, `spider`, `elephant` and `mumakil` (`tools/verify_mount_assets.py:37-68`); the warg, the
  fell warg, the chariot and the war ram are not covered. `python tools/audit_mount_parity.py` never
  exits non-zero and prints a diff you have to read, currently 431 lines
  <!-- measured: python tools/audit_mount_parity.py redirected to a file, then wc -l on it, exit code 0 2026-09-05 -->.
  `python tools/validate_moduledata.py` checks ids, duplicates and the harness family rules, not
  whether a number is sensible: there is no schema for item files at all, only for
  `taom_npccharacter`, `taom_spcultures` and `taom_equipmentsets`
  ([moduledata-validation](../features/moduledata-validation.md)).

**Two questions this chapter cannot answer, and where to look instead.** How a mesh becomes a
`mesh="..."` string, meaning the FBX and texture route into a `.tpac`, is the asset pipeline in
[bannerlord-engine-and-toolchain](../reference/bannerlord-engine-and-toolchain.md) section 6 and
[ue-to-bannerlord-asset-pipeline](../reference/ue-to-bannerlord-asset-pipeline.md); nothing about it
is settled from the decompile, because the running game reads loose `Assets/` trees in preference to
cooked packages ([armory-guide](../reference/armory-guide.md), lines 38-80). And what a rider race
needs beyond `race="..."` is not answerable from the decompile either: `skins.xml` has no managed
deserializer, so its attributes are readable only by experiment or from
[hero-race](../features/hero-race.md).

## Numbers in this chapter

| Number | Command | Date |
|---|---|---|
| `taom_war_ram` has 7 attributes, `warg` has 42 plus two child elements | `python -c "import xml.etree.ElementTree as E;m=E.parse('LOTRLOME_Armory/ModuleData/Monsters/LOTR/lotr_monster_war_ram.xml').getroot().find('Monster');print(len(m.attrib))"` and the same against `lotr_monster_warg.xml`, run from the Modules folder | 2026-09-05 |
| 7 monster files under `Monsters/LOTR/`, one `<Monster>` each | `python -c "import glob;print(len(glob.glob('LOTRLOME_Armory/ModuleData/Monsters/LOTR/*.xml')))"` from the Modules folder | 2026-09-05 |
| 70 `<Monster>` entries in the Armory's own `monsters.xml`, 16 in `Native/ModuleData/monsters.xml` | `python -c "import xml.etree.ElementTree as E;print(len(E.parse('LOTRLOME_Armory/ModuleData/monsters.xml').getroot().findall('Monster')))"` and the same for Native | 2026-09-05 |
| 8 `<XmlName id="Monsters">` rows, 4 `soln_monsters` rows | `rg -c 'XmlName id="Monsters"' LOTRLOME_Armory/SubModule.xml` and `rg -c 'id="soln_monsters"' LOTRLOME_Armory/ModuleData/project.mbproj` | 2026-09-05 |
| 10 `Type="Horse"` items and 34 `Type="HorseHarness"` items, all in `LOTRAOM_horses.xml`; harness families 31 at 1, 2 at 4, 1 at 10, none missing | ElementTree sweep of `LOTRLOME_Armory/ModuleData/LOTRLOME_items/**/*.xml` counting by `Type` and by `<Armor family_type>` | 2026-09-05 |
| 12 rein attributes on the vanilla horse; declared counts spider 5, warg 5, fell warg 5, elephant 0, mumakil 0, chariot 12, war ram 0 | a loop over `Monsters/LOTR/*.xml` counting attribute names beginning `rein`, plus the same against the `horse` entry in `Native/ModuleData/monsters.xml` | 2026-09-05 |
| `Monster.Deserialize` reads 76 attributes, 7 indexed families, 5 elements; `HorseComponent.Deserialize` 13 and 6; `ArmorComponent.Deserialize` 23 | `python -c "import sys;sys.path.insert(0,'tools');import check_handbook_attributes as C;from pathlib import Path;src=(Path(C.DEFAULT_DUMP_ROOT)/'Core/TaleWorlds.Core/TaleWorlds.Core/Monster.cs').read_text(errors='replace');r=C.extract_reads(src,'Deserialize');print(len(r['attributes']),len(r['prefixes']),len(r['elements']))"` and the same for the other two files | 2026-09-05 |
| `taom_war_ram_a` is referenced 12 times in `troops_erebor.xml`, 3 in `taom_equipment_sets_erebor.xml`, 2 in `taom_career_starting_equipment.xml`, 1 in `culture_marketplace_config.xml` | `rg -c "taom_war_ram_a" <file>` against each | 2026-09-05 |
| war ram against vanilla horse: hit points 160 vs 200, weight 320 vs 400, jump acceleration 7.5 vs 6.5, charge speed limit 4.0 vs 4.3; the horse declares `num_paces="6"`, which the ram inherits | reading those attributes off `Monster` `taom_war_ram` and `Monster` `horse` in `Native/ModuleData/monsters.xml` with ElementTree | 2026-09-05 |
| 8 ram bardings, values 20 to 54 | `rg -c 'id="taom_ram_barding' LOTRLOME_Armory/ModuleData/LOTRLOME_items/LOTRAOM_horses.xml`, values from [lotrlome-war-ram-changes](../reference/lotrlome-war-ram-changes.md) lines 143-152 | 2026-09-05 |
| `subtype` is read nowhere in the managed engine | `grep -rn '"subtype"' ` over the v1.4.8 `Core` decompile tree, zero hits | 2026-09-05 |
| the three harness and dwarf codes pass today | `python tools/validate_moduledata.py --code MISSING_HARNESS_FAMILY_TYPE --code HARNESS_FAMILY_MISMATCH --code MOUNTED_DWARF`, `PASS: no validation issues found`, exit 0 | 2026-09-05 |
| `audit_mount_parity.py` prints 431 lines and exits 0; `audit_mbproj_registration.py --all` audits 7 modules with 0 errors and 5 warnings | `python tools/audit_mount_parity.py > out.txt` then `wc -l out.txt`, and `python tools/audit_mbproj_registration.py --all` | 2026-09-05 |

## Read next

- [war-ram](../features/war-ram.md) and [lotrlome-war-ram-changes](../reference/lotrlome-war-ram-changes.md), the reskin worked end to end
- [mumakil](../features/mumakil.md), the same pattern taken to 3x scale
- [creature-mount-authoring](../ai-includes/creature-mount-authoring.md), the bespoke path and its 18-item gotcha index
- [custom_creature_xml](../community/bannerlordmodding-lt/guides/custom_creature_xml.md), the reskin trap and the registration split
- [armory-guide](../reference/armory-guide.md), the harness rule and the Armory's folder layout
- [Items: armour](items-armor.md) for the shared `<Item>` and `<Armor>` reference, [Troops](troops.md) and [Equipment rosters](equipment-rosters.md) for the slots that consume a mount
- [Submodule and registration](submodule-and-registration.md) for `<XmlName>` against `project.mbproj`
- [Validation and testing](validation-and-testing.md) and [recipe: retire content](recipe-retire-content.md)

---

<!-- backlinks-start auto-generated; edit lint_docs.py / build_backlinks.py to change -->

## Referenced by

- [docs/INDEX.md](../INDEX.md)
- [docs/modding/equipment-rosters.md](./equipment-rosters.md)
- [docs/modding/file-catalogue.md](./file-catalogue.md)
- [docs/modding/items-armor.md](./items-armor.md)
- [docs/modding/items-weapons-and-crafting.md](./items-weapons-and-crafting.md)
- [docs/modding/README.md](./README.md)
- [docs/modding/recipe-add-a-race-or-creature.md](./recipe-add-a-race-or-creature.md)
- [docs/modding/recipe-new-mod-from-zero.md](./recipe-new-mod-from-zero.md)
- [docs/modding/troubleshooting.md](./troubleshooting.md)

<!-- backlinks-end -->
