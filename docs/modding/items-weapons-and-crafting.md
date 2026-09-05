# Items: weapons and crafting

## What this file is

A TAOM weapon is either a **crafted weapon**, assembled at load time from up to four `<CraftingPiece>` parts through a `<CraftingTemplate>`, or a **single-piece `<Item>`** that carries its own stats (every bow, crossbow, arrow and bolt). The crafted path spreads one weapon across four files, and three of them must agree or the weapon is unregistered at load and every troop naming it holds an empty slot. This chapter covers those four files, the two gates a crafted weapon has to pass, and the `<Weapon>` component that a single-piece weapon uses instead.

Shields live in [Shields](items-shields.md), armour in [Armour](items-armor.md), and mounts and harness in [Mounts and harness](items-mounts-and-harness.md).

## Where it lives and how it is registered

All four weapon files live in the game install, in `LOTRLOME_Armory/ModuleData/`. This file lives in the game install, not the repo; a module reinstall reverts hand edits, so land a repo-side validator gate with any fix. Every path in this chapter is written relative to the game's `Modules` folder, and so is every counting command.

| File (under `LOTRLOME_Armory/ModuleData/`) | Root element | Per-entry element | `<XmlName id>` | `SubModule.xml` line | Engine class |
|---|---|---|---|---|---|
| `LOTRLOME_crafting_pieces.xml` | `<CraftingPieces>` | `<CraftingPiece>` | `CraftingPieces` | 195 | `TaleWorlds.Core.CraftingPiece` |
| `crafting_templates.xslt` | stylesheet, patches `<CraftingTemplates>` | `<UsablePiece>` rows | `CraftingTemplates` | 205 | `TaleWorlds.Core.CraftingTemplate` |
| `weapon_descriptions.xslt` | stylesheet, patches `<WeaponDescriptions>` | `<AvailablePiece>` rows | `WeaponDescriptions` | 213 | `TaleWorlds.Core.WeaponDescription` |
| `LOTRLOME_items/LOTRAOM_weapons.xml` | `<Items>` | `<CraftedItem>` and `<Item>` | `Items` | 313 | `TaleWorlds.Core.ItemObject` |

Two things about that table are worth reading twice.

- **`<CraftedItem>` has no registration of its own.** It is an `ItemObject` that lives inside an ordinary `<Items>` file next to plain `<Item>` rows, and `ItemObject.Deserialize` branches on the element name at `ItemObject.cs:420`. The root element is what routes a file, never the child names.
- **The two stylesheets carry no `.xml` of their own.** The Armory ships `crafting_templates.xslt` and `weapon_descriptions.xslt` with no sibling `crafting_templates.xml` or `weapon_descriptions.xml`, so both patch the vanilla files from `Native/ModuleData/`. How a stylesheet reaches another module's file is [Load order and dependencies](load-order-and-dependencies.md) section E; how an `<XmlNode>` row is written is [SubModule and registration](submodule-and-registration.md).

Vanilla supplies the categories the Armory extends: 12 `<CraftingTemplate>` ids and 22 `<WeaponDescription>` ids <!-- measured: grep -c '<CraftingTemplate$' Native/ModuleData/crafting_templates.xml && grep -c '<WeaponDescription$' Native/ModuleData/weapon_descriptions.xml 2026-09-05 -->. TAOM adds none of either: the Armory's stylesheets only extend the `<AvailablePieces>` and `<UsablePieces>` lists of vanilla's, across 14 weapon descriptions and 9 crafting templates <!-- measured: grep -c 'xsl:template match="WeaponDescription' LOTRLOME_Armory/ModuleData/weapon_descriptions.xslt && grep -c 'xsl:template match="CraftingTemplate' LOTRLOME_Armory/ModuleData/crafting_templates.xslt 2026-09-05 -->.

### Load order inside one module

Every id reference between these four files resolves through `MBObjectManager.GetObject`, which returns null for anything not yet loaded, and each call site then skips the null without a word. So the order of the `<XmlNode>` rows decides whether your piece exists:

- `CraftingTemplate`'s `<WeaponDescriptions>` skips an unresolved id (`CraftingTemplate.cs:198-200`).
- `CraftingTemplate`'s `<UsablePieces>` skips an unresolved `piece_id` (`CraftingTemplate.cs:212-214`).
- `WeaponDescription`'s `<AvailablePieces>` skips an unresolved `id` (`WeaponDescription.cs:53-55`).

`Native/SubModule.xml` orders them pieces (137), descriptions (146), templates (149). The Armory orders them pieces (195), templates (205), descriptions (213), with the item files after at 313. Both work because the vanilla objects already exist by the time the Armory's rows run; a new module writing its own `.xml` for all three should follow Native's order.

## Attributes

### `<CraftingPiece>`

<!-- engine-table type="TaleWorlds.Core.CraftingPiece" file="Core/TaleWorlds.Core/TaleWorlds.Core/CraftingPiece.cs" method="Deserialize" inert="CraftingCost,is_unique,required_skill_value" -->

| Attribute | Type | Required | Default when absent | What it does | Read at (file:line) |
|---|---|---|---|---|---|
| `id` | string | Yes | none, load crashes | The name you type in `<AvailablePiece>`, `<UsablePiece>` and `<Piece>`. Unique across every loaded module; a later module reusing it replaces the piece. | `MBObjectBase.cs:61` via `CraftingPiece.cs:155` |
| `name` | string, localised | Yes | none, load crashes | Display name in the smithing screen. Write it `{=key}Text`. | `CraftingPiece.cs:157` |
| `piece_type` | enum `Blade` \| `Guard` \| `Handle` \| `Pommel`, case-insensitive | Yes | none, load crashes | Which of the four slots the part fills. `Blade` is the head, `Handle` the grip or shaft. | `CraftingPiece.cs:158` |
| `mesh` | string | Yes | none, load crashes | The packaged mesh drawn for the part. Armory convention is `mesh` equal to `id`. | `CraftingPiece.cs:159` |
| `culture` | culture reference | No | null | Flavour and filtering. Must carry the prefix, `culture="Culture.gondor"`; a bare id throws. | `CraftingPiece.cs:160` |
| `appearance` | float | No | `0.5` | Price only, and only the pommel's value counts (the blade's if there is no pommel). | `CraftingPiece.cs:161` |
| `CraftingCost` | int, capital C | No | `0` | Read but has no effect in v1.4.8. Smithing cost comes from `<Materials>`. | `CraftingPiece.cs:162` |
| `weight` | float, kilograms | No | `0` | Summed across the four fitted parts. Weight drives inertia, which drives swing speed and handling. | `CraftingPiece.cs:163` |
| `length` | float, centimetres | Yes unless both distances are given | none | Part length. Setting it makes the part symmetric: the attachment point sits at the middle. | `CraftingPiece.cs:165` |
| `distance_to_next_piece` | float, centimetres | Yes when `length` is absent | none, load crashes | Pivot to the attachment toward the tip. Use for an asymmetric head. | `CraftingPiece.cs:174` |
| `distance_to_previous_piece` | float, centimetres | Yes when `length` is absent | none, load crashes | Pivot to the attachment toward the pommel. Pairs with the previous row. | `CraftingPiece.cs:175` |
| `center_of_mass` | float, fraction of the part's length | No | `0.5` | 0 is the pommel end, 1 the tip. Feeds balance and the sweet spot. | `CraftingPiece.cs:181` |
| `item_holster_pos_shift` | `"x,y,z"` floats, metres | No | `0,0,0`, and silently so when the string does not split into three | Nudges the sheathed weapon on the body. Added to the template's own offset. | `CraftingPiece.cs:184` |
| `tier` | int | No | `1` | The main balance dial. The finished weapon's tier is the mean of its fitted parts' tiers, and smithing difficulty is `tier * 50` per part. | `CraftingPiece.cs:197` |
| `is_unique` | bool | No | `false` | Read but has no effect in v1.4.8; no consumer of the property exists in the dump. | `CraftingPiece.cs:199` |
| `is_default` | bool | No | `false` | Unlocked in the smithing screen from the start instead of needing research. | `CraftingPiece.cs:200` |
| `is_hidden` | bool | No | `false` | Hides the part from the smithing designer. A `<CraftedItem>` can still use it, which is how a hero weapon stays uncopyable. | `CraftingPiece.cs:201` |
| `full_scale` | bool, exact string compare to `true` | No | `true` for Guard and Pommel, `false` for Blade and Handle | Whether weight scales cubically with the designer's scale slider. `full_scale="True"` reads as false. | `CraftingPiece.cs:202` |
| `excluded_item_usage_features` | string, colon separated | No | empty | Removes tokens from the weapon description's ability list. See the token rules below. | `CraftingPiece.cs:204` |
| `required_skill_value` | int | No | `0` | Read but has no effect in v1.4.8. Gate difficulty with `tier` instead. | `CraftingPiece.cs:206` |

### `<BladeData>` on a Blade piece

<!-- engine-table type="TaleWorlds.Core.BladeData" file="Core/TaleWorlds.Core/TaleWorlds.Core/BladeData.cs" method="Deserialize" inert="holster_mesh_length" -->

| Attribute | Type | Required | Default when absent | What it does | Read at (file:line) |
|---|---|---|---|---|---|
| `stack_amount` | short | No | `1` | Projectiles per thrown stack. Only read for Javelin, ThrowingAxe and ThrowingKnife, and only on the primary usage. | `BladeData.cs:46` |
| `blade_length` | float, centimetres | No | the piece's own `length` | Cutting-edge length. Its only live job in v1.4.8 is supplying the default for `blade_width`. | `BladeData.cs:47` |
| `blade_width` | float, centimetres | No | `0.15 + blade_length * 0.3` | Where a thrown axe plants itself when it lands. | `BladeData.cs:48` |
| `physics_material` | string | No | null | Impact sound and particle family. The Armory uses `metal_weapon` and `wood_weapon`. | `BladeData.cs:49` |
| `body_name` | string | No, but see the gotchas | null | The collision proxy, `bo_` plus the mesh name. A name the engine cannot resolve hangs mission load. | `BladeData.cs:50` |
| `holster_mesh` | string | No | null | The scabbard mesh drawn when the weapon is sheathed. | `BladeData.cs:51` |
| `holster_body_name` | string | No | falls back to `body_name` | Collision body for the sheathed weapon. | `BladeData.cs:52` |
| `holster_mesh_length` | float, centimetres | No | `0` | Read but no managed consumer exists in the v1.4.8 dump; presumed native. | `BladeData.cs:53` |
| `damage_type` | enum `Cut` \| `Pierce` \| `Blunt`, case-insensitive | Yes inside `<Thrust>` or `<Swing>` | none, load crashes | How that attack hurts. | `BladeData.cs:69`, `:77` |
| `damage_factor` | float | Yes inside `<Thrust>` or `<Swing>` | none, load crashes | The real damage dial. Only the Blade slot's value reaches the finished weapon. | `BladeData.cs:70`, `:78` |

### `<CraftingTemplate>`

You will rarely author one, because TAOM adds none, but you have to read them to know which descriptions a template offers and in what order.

<!-- engine-table type="TaleWorlds.Core.CraftingTemplate" file="Core/TaleWorlds.Core/TaleWorlds.Core/CraftingTemplate.cs" method="Deserialize" inert="always_show_holster_with_weapon,rotate_weapon_in_holster,piece_type_to_scale_holster_with,hidden_piece_types_on_holster" -->

| Attribute | Type | Required | Default when absent | What it does | Read at (file:line) |
|---|---|---|---|---|---|
| `id` | string | Yes | none, load crashes | Template id, named by `<CraftedItem crafting_template="...">`. | `CraftingTemplate.cs:198` and `MBObjectBase.cs:61` |
| `modifier_group` | group id, bare | No | null, no quality prefixes | Default quality pool for anything built from the template. | `CraftingTemplate.cs:148` |
| `item_type` | enum `ItemObject.ItemTypeEnum`, case-sensitive | Yes | none, load crashes | What comes out: `OneHandedWeapon`, `TwoHandedWeapon`, `Polearm`, `Thrown`. | `CraftingTemplate.cs:153` |
| `item_holsters` | string, colon separated | Yes | none, load crashes | Ordered sheath slots the finished weapon may occupy. | `CraftingTemplate.cs:154` |
| `default_item_holster_position_offset` | `"x,y,z"` via strict `Vec3.Parse` | Yes | none, load crashes | Baseline sheathed position. The blade's `item_holster_pos_shift` adds to it. | `CraftingTemplate.cs:155` |
| `use_weapon_as_holster_mesh` | bool | No | `false` | Draw the weapon itself on the belt instead of a scabbard mesh. | `CraftingTemplate.cs:156` |
| `always_show_holster_with_weapon` | bool | No | `false` | Read, and no managed consumer exists in the dump. | `CraftingTemplate.cs:157` |
| `rotate_weapon_in_holster` | bool | No | `false` | Read, and no managed consumer exists in the dump. | `CraftingTemplate.cs:158` |
| `piece_type_to_scale_holster_with` | enum piece type, case-sensitive | No | `Invalid` | Read, and no managed consumer exists in the dump. Three of vanilla's 12 templates set `Blade`. | `CraftingTemplate.cs:159` |
| `hidden_piece_types_on_holster` | string, colon separated piece types | No | nothing hidden | Which parts stop being drawn once sheathed. The accessor has no managed caller. | `CraftingTemplate.cs:161` |
| `piece_type` | enum piece type, case-sensitive | Yes on `<PieceData>` | none, load crashes | Declares that the template has this slot. A type not listed cannot be fitted at all. | `CraftingTemplate.cs:184` |
| `build_order` | int, may be negative | Yes on `<PieceData>` | none, load crashes | Assembly order along the axis. Vanilla `TwoHandedPolearm` uses Handle 0, Guard 1, Blade 2, Pommel -1. | `CraftingTemplate.cs:185` |
| `piece_id` | piece id | Yes on `<UsablePiece>` | none, load crashes | Adds a part to the pool this template may use. This is gate 1. | `CraftingTemplate.cs:212` |
| `weapon_description` | description id | No on `<StatsData>` | the block applies to every description | Restricts a set of display caps to one usage. An id not in this template indexes an array at -1 and throws. | `CraftingTemplate.cs:222` |
| `stat_type` | enum, case-sensitive | Yes on `<StatData>` | none, load crashes | Which stat bar the cap belongs to. | `CraftingTemplate.cs:232` |
| `max_value` | float | Yes on `<StatData>` | none, load crashes | Top of the displayed bar, not a clamp on the weapon. | `CraftingTemplate.cs:233` |

<!-- measured: grep -c 'piece_type_to_scale_holster_with="Blade"' Native/ModuleData/crafting_templates.xml 2026-09-05 -->

### `<WeaponDescription>`

<!-- engine-table type="TaleWorlds.Core.WeaponDescription" file="Core/TaleWorlds.Core/TaleWorlds.Core/WeaponDescription.cs" method="Deserialize" inert="rotated_in_hand,use_center_of_mass_as_hand_base" -->

| Attribute | Type | Required | Default when absent | What it does | Read at (file:line) |
|---|---|---|---|---|---|
| `id` | string | Yes | none, load crashes | Usage id, for example `OneHandedPolearm`. Also the `id` on each `<AvailablePiece>` row. | `MBObjectBase.cs:61`, `WeaponDescription.cs:53` |
| `weapon_class` | enum `WeaponClass`, case-sensitive | No | `Undefined` | Animation and behaviour family for this usage, and the switch onto the throwing code path. | `WeaponDescription.cs:29` |
| `item_usage_features` | string, colon separated | No | empty | The base ability list. Every token any fitted part excludes is removed, and the survivors are joined with underscores into the item usage name. | `WeaponDescription.cs:30` |
| `rotated_in_hand` | bool | No | `false` | Read, and no managed consumer exists in the dump. Vanilla `Javelin` sets it. | `WeaponDescription.cs:31` |
| `use_center_of_mass_as_hand_base` | bool | No | `false` | Read, and no managed consumer exists in the dump. Vanilla `Javelin` sets it. | `WeaponDescription.cs:32` |
| `value` | enum `WeaponFlags`, case-sensitive | Yes on `<WeaponFlag>` | none, load crashes | One behaviour flag for this usage, OR-ed with the flags the fitted parts contribute. | `WeaponDescription.cs:39` |

### `<Item>` and `<CraftedItem>`

One deserializer, two branches. `<CraftedItem>` reads only `id`, `name`, `crafting_template`, `has_modifier`, `modifier_group`, `multiplayer_item`, `is_merchandise`, `value` and `culture`, plus a mandatory `<Pieces>` block; everything else in this table belongs to the `<Item>` branch, which is what a bow or an arrow uses.

<!-- engine-table type="TaleWorlds.Core.ItemObject" file="Core/TaleWorlds.Core/TaleWorlds.Core/ItemObject.cs" method="Deserialize" inert="lod_atlas_index,using_arm_band" -->

| Attribute | Type | Required | Default when absent | What it does | Read at (file:line) |
|---|---|---|---|---|---|
| `id` | string | Yes | none, load crashes | The name every roster and equipment set points at. Renaming it breaks every reference and every save holding the item. | `MBObjectBase.cs:61` |
| `name` | string, localised | Yes | none, load crashes | Display name. Write it `{=key}Text`. | `ItemObject.cs:433` (crafted), `:492` (plain) |
| `crafting_template` | template id, bare | Yes on `<CraftedItem>` | none, load crashes | Which template assembles the parts. | `ItemObject.cs:434` |
| `has_modifier` | bool, only the literal `false` disables | No, `<CraftedItem>` only | `true` | Whether the weapon can roll Rusty, Fine, Masterwork. Set `false` for a fixed artefact. | `ItemObject.cs:435` |
| `modifier_group` | group id, bare | No | the crafting template's own group | Which quality pool the item rolls from. An unknown id resolves to null and the item never rolls anything. | `ItemObject.cs:436` |
| `multiplayer_item` | bool, exact compare to `true` | No | unchanged | Multiplayer-only. Not relevant to a singleplayer conversion. | `ItemObject.cs:423`, `:493` |
| `is_merchandise` | bool, inverted, exact compare to `true` | No | the item is merchandise | `is_merchandise="false"` keeps it out of shops and loot. Any value other than `true` also counts as not merchandise. | `ItemObject.cs:428`, `:498` |
| `mesh` | string | No | null, the item is invisible | The packaged multi-mesh drawn for the item. | `ItemObject.cs:503` |
| `holster_mesh` | string | No | null | Mesh drawn while sheathed, for example a bow in its case. | `ItemObject.cs:508` |
| `holster_mesh_with_weapon` | string | No | null | Empty scabbard or quiver kept on the body while the weapon is drawn. | `ItemObject.cs:509` |
| `flying_mesh` | string | No | null | The model an arrow or javelin uses in flight, usually a single shaft. | `ItemObject.cs:510` |
| `item_holsters` | string, colon separated, up to four | No | four null slots | Body attachment points, in preference order. Bows in the Armory use `bow_back:bow_back_2:bow_hip:bow_hip_2`. | `ItemObject.cs:512` |
| `has_lower_holster_priority` | bool, strict `bool.Parse` | No | `false`, and ignored entirely without `item_holsters` | Yields the slot to another item that wants the same spot. | `ItemObject.cs:515` |
| `holster_position_shift` | `"x,y,z"` via strict `Vec3.Parse` | No | zero | Nudges the sheathed item in metres when it clips the armour. | `ItemObject.cs:524` |
| `body_name` | string | No | null | Collision body while held or on the ground. | `ItemObject.cs:525` |
| `skeleton_name` | string | No | null | Skeleton for items that animate on their own. | `ItemObject.cs:526` |
| `static_animation_name` | string | No | null | Looping idle animation on the item itself. | `ItemObject.cs:527` |
| `holster_body_name` | string | No | null | Collision body while sheathed. | `ItemObject.cs:528` |
| `shield_body_name` | string | No | null | Stored as `CollisionBodyName`. Shields only; see [Shields](items-shields.md). | `ItemObject.cs:529` |
| `recalculate_body` | bool, strict `bool.Parse` | No | `false` | Rebuild the physics body from the mesh at spawn instead of trusting `body_name`. | `ItemObject.cs:530` |
| `prefab` | string | No | empty string | Scene prefab bolted onto the model for extra effects. | `ItemObject.cs:531` |
| `culture` | culture reference | No | null | Owning faction. Must carry the `Culture.` prefix; a bare id throws, an unknown prefixed id creates a blank placeholder. | `ItemObject.cs:485`, `:540` |
| `item_category` | category id, bare | No | picked automatically from type and tier | Trade bucket for the economy simulation. Leave it out for ordinary gear. | `ItemObject.cs:541` |
| `weight` | float, kilograms | No | `1.0`, not zero | Encumbrance, and weapon inertia and handling. | `ItemObject.cs:546` |
| `lod_atlas_index` | int | No | `-1` | Read, and no managed consumer exists in the dump; presumed native LOD atlasing. | `ItemObject.cs:547` |
| `difficulty` | int | No | `0` | Minimum weapon skill. Below it the wielder takes the not-qualified penalty and the inventory shows a red line. | `ItemObject.cs:548` |
| `appearance` | float | No | `0.5` | A price multiplier, not a combat stat. | `ItemObject.cs:553` |
| `IsFood` | bool, capital I | No | `false` | Party food, meaningful only with a `<Trade>` component. | `ItemObject.cs:555` |
| `using_tableau` | bool | No | `false` | Texture painted at runtime from heraldry. Shields use it. | `ItemObject.cs:560` |
| `using_arm_band` | string | No | null | Read, and no consumer and no vanilla use exist in v1.4.8. | `ItemObject.cs:561` |
| `scale_factor` | float | No | `1.0` | Scales the model and the effective reach together. | `ItemObject.cs:566` |
| `Type` | enum, capital T, case-insensitive | Advisory for weapons, authoritative for armour and horses | `Invalid`, and the whole block is skipped when the attribute is absent | For anything with a `<Weapon>` component it is overwritten by the type derived from `weapon_class`, with a red debug line. Change `weapon_class`, not `Type`. | `ItemObject.cs:625` |
| `AmmoOffset` | `"x,y,z"`, capital A | No | unset | Moves the nocked arrow on the string. Putting it on an item with no `<Weapon>` component throws a null reference at load. | `ItemObject.cs:644` |
| `tier_override` | float | No | tier is computed from the stats | Forces the tier, and therefore price, shop stock and the AI's idea of good gear, without touching damage. | `ItemObject.cs:666` |
| `value` | int, gold | No | the price formula runs | Hard price. Leave it out so rebalancing stats rebalances price. | `ItemObject.cs:477`, `:672` |

### `<Weapon>`, the component a single-piece weapon carries

<!-- engine-table type="TaleWorlds.Core.WeaponComponentData" file="Core/TaleWorlds.Core/TaleWorlds.Core/WeaponComponentData.cs" method="Deserialize" inert="fire_damage" -->

| Attribute | Type | Required | Default when absent | What it does | Read at (file:line) |
|---|---|---|---|---|---|
| `weapon_class` | enum `WeaponClass`, case-sensitive | Yes in practice | `Undefined` | The one attribute that decides what the item is. It overrides `Type`. | `WeaponComponentData.cs:364` |
| `ammo_class` | enum `WeaponClass`, case-sensitive | No | `Undefined` | Which ammo a bow or crossbow accepts, for example `Arrow`. | `WeaponComponentData.cs:365` |
| `item_usage` | string | Yes on a Bow, Crossbow, Sling, Pistol or Musket | null | Names a row in `Native/ModuleData/item_usage_sets.xml` and picks the stance and animation set. Missing on a bow, the price model calls `.Contains` on null and throws. | `WeaponComponentData.cs:352` |
| `speed_rating` | int | No | `0` | Swing speed. There is no `swing_speed` attribute. | `WeaponComponentData.cs:354` |
| `thrust_speed` | int | No | `0` | Thrust speed, and the value copied into Handling. | `WeaponComponentData.cs:355` |
| `swing_damage` | int | No | `0` | Swing damage. | `WeaponComponentData.cs:359` |
| `thrust_damage` | int | No | `0` | Thrust damage, and missile damage for ranged and ammo. | `WeaponComponentData.cs:358` |
| `swing_damage_type` | enum `Cut` \| `Pierce` \| `Blunt`, case-sensitive | No | `Blunt` | Damage type of the swing. Lowercase throws. | `WeaponComponentData.cs:363` |
| `thrust_damage_type` | enum, case-sensitive | No | `Blunt` | Damage type of the thrust. | `WeaponComponentData.cs:362` |
| `missile_speed` | int | No | `0` | Projectile speed for a bow, crossbow or thrown weapon. | `WeaponComponentData.cs:356` |
| `accuracy` | int | No | `100`, the one non-zero default | Ranged accuracy. | `WeaponComponentData.cs:361` |
| `weapon_length` | int, centimetres | No | `0` | Reach, and it also sets the centre of mass to half the length. | `WeaponComponentData.cs:357` |
| `weapon_balance` | int written as a percent, divided by 100 | No | `0` | Balance. | `WeaponComponentData.cs:353` |
| `body_armor` | int | No | `0` | Shield coverage; see [Shields](items-shields.md). | `WeaponComponentData.cs:348` |
| `fire_damage` | int | No | `0` | Read, and no consumer exists in the dump. | `WeaponComponentData.cs:360` |
| `reload_phase_count` | short | No | `1` | Crossbow reload stages. | `WeaponComponentData.cs:366` |
| `ammo_limit` | short | No | `0` | Arrows per quiver. First of three attributes that write one shared field. | `WeaponComponentData.cs:374` |
| `stack_amount` | short | No | `0` | Throwables per stack. Used only when `ammo_limit` is absent. | `WeaponComponentData.cs:375` |
| `hit_points` | short | No | `0` | Shield hit points. Used only when both of the above are absent. | `WeaponComponentData.cs:376` |
| `physics_material` | string | No | null | Impact sound and particle family. | `WeaponComponentData.cs:349` |
| `flying_sound_code` | string | No | null | Sound while the projectile is in the air. | `WeaponComponentData.cs:350` |
| `passby_sound_code` | string | No | null | Sound as it passes a listener. | `WeaponComponentData.cs:351` |
| `trail_particle_name` | string | No | null | Trail effect on a projectile. | `WeaponComponentData.cs:450` |
| `center_of_mass` | `"x,y,z"` via strict `Vec3.Parse` | No | zero | A vector, distinct from the scalar centre of mass the engine computes from `weapon_length`. | `WeaponComponentData.cs:368` |
| `rotation_speed` | `"x,y,z"` via strict `Vec3.Parse` | No | zero | Spin of a thrown weapon in flight. | `WeaponComponentData.cs:449` |
| `position` | `"x,y,z"`, tolerant parse | No | identity | Where the mesh sits in the hand. A malformed component silently becomes 0. | `WeaponComponentData.cs:423` |
| `rotation` | `"x,y,z"` in degrees, tolerant parse | No | identity | How the mesh is rotated in the hand. | `WeaponComponentData.cs:434` |
| `sticking_position` | `"x,y,z"`, tolerant parse | No | identity | Where an arrow or javelin plants itself in a target. | `WeaponComponentData.cs:395` |
| `sticking_rotation` | `"x,y,z"` in degrees, tolerant parse | No | identity | The angle it plants at. | `WeaponComponentData.cs:406` |

Six stats are not authorable at all: Handling is hard-set to `thrust_speed`, sweet-spot reach to `0.93`, the scalar centre of mass to `weapon_length * 0.005`, total inertia from item weight, and the swing and thrust damage factors from weight, length and damage type (`WeaponComponentData.cs:465-468`).

The legal `weapon_class` values, in enum order, are `Undefined`, `Dagger`, `OneHandedSword`, `TwoHandedSword`, `OneHandedAxe`, `TwoHandedAxe`, `Mace`, `Pick`, `TwoHandedMace`, `OneHandedPolearm`, `TwoHandedPolearm`, `LowGripPolearm`, `Arrow`, `Bolt`, `SlingStone`, `Cartridge`, `Bow`, `Crossbow`, `Sling`, `Stone`, `Boulder`, `ThrowingAxe`, `ThrowingKnife`, `Javelin`, `Pistol`, `Musket`, `BallistaBoulder`, `BallistaStone`, `SmallShield`, `LargeShield`, `Banner`.

<!-- engine-ref type="TaleWorlds.Core.WeaponClass" file="Core/TaleWorlds.Core/TaleWorlds.Core/WeaponClass.cs" lines="3-33" -->

`NumClasses` is the enum's terminator and must never be written. The Armory's single-piece weapons use four of these: 35 `Bow`, 28 `Arrow`, 3 `Crossbow`, 2 `Bolt` <!-- measured: grep -o 'weapon_class="[^"]*"' LOTRLOME_Armory/ModuleData/LOTRLOME_items/LOTRAOM_weapons.xml | sort | uniq -c 2026-09-05 -->.

## Child elements

### Under `<CraftingPiece>`

<!-- engine-table type="TaleWorlds.Core.CraftingPiece" file="Core/TaleWorlds.Core/TaleWorlds.Core/CraftingPiece.cs" method="Deserialize" inert="" -->

| Element or attribute | Type | Required | Default when absent | What it does | Read at (file:line) |
|---|---|---|---|---|---|
| `<BladeData>` | element | Yes on a Blade that will be fitted | no blade data | Damage, physics material and collision body. A fitted Blade with no `<BladeData>` throws a null reference while the item file loads. A second one replaces the first. | `CraftingPiece.cs:234` |
| `<StatContributions>` | element | No | all seven bonuses 0 | Seven bonus numbers. Only `armor_bonus`, and only on the Guard, has any effect in v1.4.8. A second element overwrites the first. | `CraftingPiece.cs:216` |
| `armor_bonus` | int | No | `0` | Hand armour, read from the Guard slot alone. Putting it on any other slot does nothing in combat. | `CraftingPiece.cs:218` |
| `handling_bonus` | int | No | `0` | Tooltip only in v1.4.8. Change `weight`, `length` and `center_of_mass` instead. | `CraftingPiece.cs:220` |
| `swing_damage_bonus` | int | No | `0` | Tooltip only. Edit `<Swing damage_factor>` instead. | `CraftingPiece.cs:222` |
| `swing_speed_bonus` | int | No | `0` | Tooltip only. | `CraftingPiece.cs:224` |
| `thrust_damage_bonus` | int | No | `0` | Tooltip only. Edit `<Thrust damage_factor>` instead. | `CraftingPiece.cs:226` |
| `thrust_speed_bonus` | int | No | `0` | Tooltip only. | `CraftingPiece.cs:228` |
| `accuracy_bonus` | int | No | `0` | Tooltip only. Throwing accuracy is hard-coded per weapon class. | `CraftingPiece.cs:230` |
| `<BuildData>` | element | No | all three offsets 0 | Slides the parts along the axis when the weapon is assembled. Used by 531 of vanilla's 805 pieces. | `CraftingPiece.cs:238` |
| `piece_offset` | float, centimetres | No | `0` | Slides this part without changing its length. | `CraftingPiece.cs:240` |
| `previous_piece_offset` | float, centimetres | No | `0` | Sinks the part toward the pommel into its neighbour, for example a guard into a handle socket. | `CraftingPiece.cs:241` |
| `next_piece_offset` | float, centimetres | No | `0` | Sinks the part toward the tip into its neighbour, for example a tang into a guard. | `CraftingPiece.cs:242` |
| `<Materials>` | element | No | the piece is an empty piece with no smithing difficulty and no smelt refund | Smithing ingredients. Present, it replaces any earlier list. | `CraftingPiece.cs:248` |
| `count` | int | Yes on `<Material>` | none, load crashes | Units of the ingredient. A count of zero or less is dropped from the recipe. | `CraftingPiece.cs:253` |
| `<Flags>` | element | No | no extra flags | Behaviour switches OR-ed onto the finished weapon from all four fitted parts. Present, it resets both accumulators first, so a second block discards the first. | `CraftingPiece.cs:263` |
| `type` | enum `WeaponFlags` \| `ItemFlags` | No on `<Flag>` | `WeaponFlags` | Which flag family `name` belongs to. Anything other than the exact string `WeaponFlags` falls to the `ItemFlags` branch. | `CraftingPiece.cs:269` |
| `<CraftingTemplates>` | element | No | no reverse links | Declares from the piece side that a template may use it. Vanilla never uses it, and it has no duplicate check, so declaring the link on both sides adds the piece twice. | `CraftingPiece.cs:282` |

<!-- measured: grep -c '<BuildData' Native/ModuleData/crafting_pieces.xml && grep -c '</CraftingPiece>' Native/ModuleData/crafting_pieces.xml 2026-09-05 -->

`<Material id>` and `<Flag name>` reuse the `id` and `name` rows in the attribute table above. `<Material id>` is read with a tolerant parse, so a misspelling such as `Iron_2` silently becomes `IronOre` rather than erroring; the legal names are `IronOre`, `Iron1` to `Iron6`, `Wood` and `Charcoal`. `<Flag name>` is parsed case-insensitively (`CraftingPiece.cs:272`, `:277`), which is why the four Armory pieces spelled `CanKnockdown` load exactly like the 141 spelled `CanKnockDown` <!-- measured: grep -o '<Flag name="[^"]*"' LOTRLOME_Armory/ModuleData/LOTRLOME_crafting_pieces.xml | sort | uniq -c 2026-09-05 -->.

### Under `<BladeData>`

<!-- engine-ref type="TaleWorlds.Core.BladeData" file="Core/TaleWorlds.Core/TaleWorlds.Core/BladeData.cs" lines="62-81" -->

| Element | Required | What happens when it is absent | Read at (file:line) |
|---|---|---|---|
| `<Thrust damage_type damage_factor>` | No | Thrust damage type stays `Invalid`, so the weapon cannot stab and the crafting screen hides its thrust bars | `BladeData.cs:65-81` |
| `<Swing damage_type damage_factor>` | No | Swing damage type stays `Invalid`, so the weapon cannot swing | `BladeData.cs:67-73` |

Both are matched by element name inside the `<BladeData>` loop rather than through the attribute idioms the handbook's table checker recognises, which is why this pair carries a reference marker instead of a table marker. Writing either one twice keeps the last.

### Under `<CraftingTemplate>` and `<WeaponDescription>`

<!-- engine-table type="TaleWorlds.Core.CraftingTemplate" file="Core/TaleWorlds.Core/TaleWorlds.Core/CraftingTemplate.cs" method="Deserialize" inert="" -->

| Element | Required | Merge behaviour | What it does | Read at (file:line) |
|---|---|---|---|---|
| `<PieceDatas>` | Yes in practice | replaces | Declares the template's slots and their build order. | `CraftingTemplate.cs:179` |
| `<WeaponDescriptions>` | Yes in practice | replaces | The ordered list of usages, and the order decides the primary. It also allocates the stats array, so it must appear before `<StatsData>`. | `CraftingTemplate.cs:192` |
| `<UsablePieces>` | No, but this is the normal way | appends, with a duplicate check | The pool of parts this template may use. Gate 1 sits here. | `CraftingTemplate.cs:208` |
| `<StatsData>` | No | repeatable, one block per usage | Display caps for the smithing bars. Unlisted stats are hidden. | `CraftingTemplate.cs:219` |

<!-- engine-table type="TaleWorlds.Core.WeaponDescription" file="Core/TaleWorlds.Core/TaleWorlds.Core/WeaponDescription.cs" method="Deserialize" inert="" -->

| Element | Required | Merge behaviour | What it does | Read at (file:line) |
|---|---|---|---|---|
| `<WeaponFlags>` | No | accumulates, never reset | Behaviour flags for this usage mode. | `WeaponDescription.cs:35` |
| `<AvailablePieces>` | No in code, required in practice | replaces | The whitelist that decides whether this usage applies to a set of parts. Gate 2 sits here. Ids that have not loaded yet are dropped in silence. | `WeaponDescription.cs:44` |

### Under `<Item>` and `<CraftedItem>`

<!-- engine-table type="TaleWorlds.Core.ItemObject" file="Core/TaleWorlds.Core/TaleWorlds.Core/ItemObject.cs" method="Deserialize" inert="" -->

| Element | Required | Merge behaviour | What it does | Read at (file:line) |
|---|---|---|---|---|
| `<CraftedItem>` | n/a, it is the row element | n/a | The pre-assembled branch of the same deserializer. | `ItemObject.cs:420` |
| `<Pieces>` | Yes on `<CraftedItem>` | indexed by slot | Holds the `<Piece>` rows. Omit it and the load throws on a null node. | `ItemObject.cs:446` |
| `<Piece>` | Yes, at least a Blade | a duplicate `Type` silently keeps the last | One fitted part: `id`, `Type` (capital T, case-sensitive) and optional `scale_factor`. Missing slots stay empty and that is legal. | `ItemObject.cs:454` |
| `<ItemComponent>` | No | wrapper only | Container for the one component element. | `ItemObject.cs:570` |
| `<Weapon>` | No | appends | The only component that accumulates. N sibling `<Weapon>` nodes give one item N usage modes, and the first is the primary that decides the item's type. | `ItemObject.cs:582` |
| `<Armor>` | No | replaces | Armour piece; see [Armour](items-armor.md). | `ItemObject.cs:579` |
| `<Horse>` | No | replaces | Mount; see [Mounts and harness](items-mounts-and-harness.md). | `ItemObject.cs:585` |
| `<Trade>` | No | replaces | Trade good. It never reads `modifier_group`. | `ItemObject.cs:588` |
| `<Food>` | never | none | Dead. It fires an assert and leaves the item with no component at all. Migrate to `<Trade>`. | `ItemObject.cs:591` |
| `<Banner>` | No | replaces | Banner item; it also accepts every `<Weapon>` attribute on the same element. | `ItemObject.cs:595` |
| `<Flags>` | No | fully defines the set | Item-level flags. Unlike `<WeaponFlags>`, this one honours values: `Civilian="false"` correctly leaves the flag off. | `ItemObject.cs:611` |

Any child of `<ItemComponent>` whose name is not one of those six throws `Wrong ItemComponent type.` and kills the load (`ItemObject.cs:599`).

### Under `<Weapon>`

<!-- engine-table type="TaleWorlds.Core.WeaponComponentData" file="Core/TaleWorlds.Core/TaleWorlds.Core/WeaponComponentData.cs" method="Deserialize" inert="" -->

| Element | Required | Merge behaviour | What it does | Read at (file:line) |
|---|---|---|---|---|
| `<WeaponFlags>` | No | accumulates across nodes, never reset | One attribute per flag you want on. The parser walks the flag enum and asks only whether an attribute of that name exists, so **`MeleeWeapon="false"` still turns the flag on**. To remove a flag, delete the attribute. | `WeaponComponentData.cs:453` |

The Armory's bows use `RangedWeapon`, `HasString`, `StringHeldByHand`, `NotUsableWithOneHand`, `TwoHandIdleOnMount`, `AutoReload` and `UnloadWhenSheathed`; its arrows use `Consumable`, `AmmoSticksWhenShot` and `AmmoBreaksOnBounceBack`. Copy a shipped set rather than inventing one: the engine recognises a bow by `StringHeldByHand` plus `AutoReload` and ammo by `Consumable` with no weapon-mask bit.

<!-- engine-ref type="TaleWorlds.Core.WeaponFlags" file="Core/TaleWorlds.Core/TaleWorlds.Core/WeaponComponentData.cs" lines="451-464" -->

## Worked example

### The crafted weapon: `anduril`

<!-- example file="LOTRLOME_Armory/ModuleData/LOTRLOME_items/LOTRAOM_weapons.xml" id="anduril" -->

```xml
    <CraftedItem
        id="anduril"
        name="{=aom_anduril_name}[Gondor] Anduril"
        crafting_template="TwoHandedSword"
        is_merchandise="true"
        culture="Culture.gondor">
        <Pieces>
            <Piece
                id="wm_anduril_sword_blade"
                Type="Blade"
                scale_factor="100" />
            <Piece
                id="wm_anduril_sword_guard"
                Type="Guard"
                scale_factor="100" />
            <Piece
                id="wm_anduril_sword_handle"
                Type="Handle"
                scale_factor="100" />
            <Piece
                id="wm_anduril_sword_pommel"
                Type="Pommel"
                scale_factor="100" />
        </Pieces>
    </CraftedItem>
```

The three attributes a reader changes first:

1. **`crafting_template`** decides the item type, the holster slots and the whole list of usages the weapon can get. Changing `TwoHandedSword` to `OneHandedSword` changes the animations, not just a label.
2. **`is_merchandise`** is the shop switch. Anduril ships as `true`, so shops may stock it. Only 8 of the 415 rows in this file set it to `false` <!-- measured: grep -c 'is_merchandise="false"' LOTRLOME_Armory/ModuleData/LOTRLOME_items/LOTRAOM_weapons.xml 2026-09-05 -->.
3. **`scale_factor`** is a percent, not a multiplier: 100 leaves the part alone, 110 makes it a tenth longer, and it raises smithing difficulty with it. Across this file the values are 100 (955 rows), 80 (46), 110 (14), 90 (7) and 70 (2) <!-- measured: grep -o 'scale_factor="[^"]*"' LOTRLOME_Armory/ModuleData/LOTRLOME_items/LOTRAOM_weapons.xml | sort | uniq -c | sort -rn 2026-09-05 -->.

Note what the item does not carry: no mesh, no weight, no damage, no `Type`. Every one of those comes from the pieces and the template.

### The blade it names

<!-- example file="LOTRLOME_Armory/ModuleData/LOTRLOME_crafting_pieces.xml" id="wm_anduril_sword_blade" -->

```xml
    <CraftingPiece
        id="wm_anduril_sword_blade"
        name="{=aom_wm_anduril_sword_blade_name}Anduril Sword Blade"
        tier="5"
        piece_type="Blade"
        mesh="wm_anduril_sword_blade"
        length="109.18"
        weight="0.87">
        <BladeData
            stack_amount="3"
            physics_material="metal_weapon"
            body_name="bo_wm_anduril_sword_blade"
            holster_mesh="">
            <Thrust
                damage_type="Pierce"
                damage_factor="3.5" />
            <Swing
                damage_type="Cut"
                damage_factor="5.2" />
        </BladeData>
        <BuildData
            piece_offset="0"
            previous_piece_offset="0"
            next_piece_offset="0" />
        <Flags>
            <Flag
                name="Civilian"
                type="ItemFlags" />
        </Flags>
        <Materials>
            <Material
                id="Iron6"
                count="9" />
        </Materials>
    </CraftingPiece>
```

1. **`damage_factor` inside `<Swing>` and `<Thrust>`** is the real damage dial, and only the Blade slot's copy reaches the finished weapon (`Crafting.cs:134-135`). Anduril's 5.2 swing sits well above the 2.0 to 3.5 that [weapon-creation-workflow.md](../ai-includes/weapon-creation-workflow.md) gives as the tier 2 to 4 range.
2. **`tier`** feeds three things: the weapon's tier is the mean of its parts' tiers, smithing difficulty is `tier * 50` per non-empty part weighted Blade 100, Handle 60, Guard 20, Pommel 20 (`DefaultSmithingModel.cs:26`, `:42-58`), and the campaign opens the lowest-tier unopened part first and never opens a hidden one (`CraftingCampaignBehavior.cs:660-681`).
3. **`length` is centimetres** and is multiplied by 0.01 into metres. Writing `1.09` where you meant `109.18` gives a blade a centimetre long, and nothing warns you.

### The two registration rows

<!-- excerpt file="LOTRLOME_Armory/ModuleData/weapon_descriptions.xslt" -->

```xml
	<xsl:template match="WeaponDescription[@id='TwoHandedSword']/AvailablePieces">
			<AvailablePiece id="wm_anduril_sword_blade"/>
```

<!-- excerpt file="LOTRLOME_Armory/ModuleData/crafting_templates.xslt" -->

```xml
	<xsl:template match="CraftingTemplate[@id='TwoHandedSword']/UsablePieces">
			<UsablePiece piece_id="wm_anduril_sword_blade"/>
```

The blade appears in `crafting_templates.xslt` under `OneHandedSword` (line 115) and `TwoHandedSword` (line 347), and in `weapon_descriptions.xslt` under `OneHandedSword` (116), `TwoHandedSword` (347) and `OneHandedBastardSword` (1282). Every one of the four pieces has to appear in both stylesheets, under the description the item's template actually uses, or the item is gone.

### The single-piece bow: `highelf_longbowa`

<!-- example file="LOTRLOME_Armory/ModuleData/LOTRLOME_items/LOTRAOM_weapons.xml" id="highelf_longbowa" -->

```xml
    <Item
        id="highelf_longbowa"
        name="{=aom_highelf_longbowa_name}[Noldor] Longbow I"
        body_name="bo_wm_elven_bow_v1"
        mesh="wm_elven_bow_v1"
        is_merchandise="true"
        culture="Culture.rivendell"
        weight="0.1"
        difficulty="100"
        appearance="0.1"
        Type="Bow"
        item_holsters="bow_back:bow_back_2:bow_hip:bow_hip_2">
        <ItemComponent>
            <Weapon
                weapon_class="Bow"
                ammo_class="Arrow"
                ammo_limit="1"
                thrust_speed="78"
                speed_rating="88"
                missile_speed="88"
                weapon_length="182"
                accuracy="100"
                thrust_damage="105"
                thrust_damage_type="Pierce"
                item_usage="bow"
                physics_material="wood_weapon"
                center_of_mass="0.15,0,0"
                modifier_group="bow"
                position="0.01, 0.0, 0.0">
                <WeaponFlags
                    RangedWeapon="true"
                    HasString="true"
                    StringHeldByHand="true"
                    NotUsableWithOneHand="true"
                    TwoHandIdleOnMount="true"
                    AutoReload="true"
                    UnloadWhenSheathed="true" />
            </Weapon>
        </ItemComponent>
        <Flags
            ForceAttachOffHandPrimaryItemBone="true" />
    </Item>
```

1. **Every number in `<Weapon>` is a whole number.** Single-piece weapon stats are schema-typed `unsignedInt`, so `weapon_length="182"` loads and `weapon_length="182.4"` throws a hard schema error naming the attribute. Crafting-piece `length`, `blade_length` and `blade_width` are floats and decimals are fine there.
2. **`thrust_damage` is the missile damage** on a ranged weapon, and `item_usage` is not optional: on a Bow, Crossbow, Sling, Pistol or Musket the price model calls `.Contains` on it and throws a null reference if it is missing.
3. **`difficulty="100"`** is a skill gate, not a stat. This bow is unusable below Bow skill 100; the starter variant beside it in the file sets `difficulty="0"`.

## Recipes: Add / Modify / Delete

Run every command from the repo root unless it says otherwise. The counting commands in this chapter run from the game's `Modules` folder.

### Add

#### A crafted four-piece weapon

Do these in order. Each step depends on the previous one existing.

1. **Name the meshes first.** The convention is `wm_<culture>_ws_<weapon>_<variant>_<role>` for the visible mesh and `bo_` plus the identical full mesh id for the collision proxy, with `<role>` one of `blade`, `guard`, `handle`, `pommel`, `head`. Blades, axe heads, spear heads and bows need a `bo_` twin; guards, handles and pommels do not.
2. **Add the `<CraftingPiece>` rows** to `LOTRLOME_Armory/ModuleData/LOTRLOME_crafting_pieces.xml`, one per part. The Blade carries `<BladeData>` with `body_name`, the damage elements and `<Materials>`; the Guard carries `<StatContributions armor_bonus="...">` if it should give hand armour. Set `tier` on all four.
3. **Decide `excluded_item_usage_features` on the head, from the description, not from the weapon's name.** Exclude `thrust` when the head has no `<Thrust>` and its description carries a `thrust` token, and exclude `swing` in the mirror case. `Mace` is `onehanded:block:shield:tipdraw:swing:thrust`, so a swing-only mace head must exclude `thrust`; `OneHandedAxe` is `onehanded:shield:axe` and has nothing to remove, which is why the axe example in [weapon-creation-workflow.md](../ai-includes/weapon-creation-workflow.md) omits it. Never declare damage you then exclude.
4. **Register every piece under `<UsablePieces>`** in `crafting_templates.xslt`, in the template your item will name. This is gate 1: a piece missing here fails `Template.Pieces.Contains` and `GenerateCraftedItem` returns null (`Crafting.cs:569-575`).
5. **Register every piece under `<AvailablePieces>`** in `weapon_descriptions.xslt`, in the description that template resolves to. This is gate 2. For a one-handed polearm that a shield-carrying troop must actually use, do not hand-edit: run `python tools/register_one_handed_polearms.py --apply`, which writes inside `TAOM-1H-POLEARM` markers and can be reverted.
6. **Add the `<CraftedItem>`** to `LOTRLOME_items/LOTRAOM_weapons.xml` with `crafting_template`, `culture` in prefixed form and a `<Pieces>` block naming each part by `id` and `Type`.
7. **Localise the new `{=key}` strings** through [Strings and localization](strings-and-localization.md).

Check: `python tools/validate_mesh_refs.py --scan-bodies` and `python tools/audit_polearm_shield_parity.py` and `python tools/check_external_xslt.py`
Takes effect: full game restart
Code: No code changes needed

#### A single-piece bow, crossbow or thrown weapon

1. **Add one `<Item>`** to `LOTRLOME_items/LOTRAOM_weapons.xml` with `mesh`, `body_name`, `weight`, `culture`, `item_holsters` and `Type`.
2. **Add exactly one `<Weapon>`** inside `<ItemComponent>` with `weapon_class`, `item_usage`, the stat numbers and a `<WeaponFlags>` block. Copy the flag set from a shipped weapon of the same class rather than inventing one; the flag combinations are what the engine uses to recognise a bow (`StringHeldByHand` plus `AutoReload`) or ammo (`Consumable` with no weapon-mask bit).
3. **Use whole numbers for every `<Weapon>` stat.** A decimal is a hard schema error at load.
4. **Skip the two stylesheets entirely.** A single-piece weapon has no crafting pieces, so nothing needs registering.

Check: `python tools/validate_mesh_refs.py --scan-bodies` then `python tools/validate_moduledata.py`
Takes effect: full game restart
Code: No code changes needed

There is a generator for both shapes. `python tools/build_weapon_xml.py --manifest <file> --apply` takes a small per-weapon manifest and writes all four files at once, deriving piece ids, mesh names and the `bo_` prefix; it routes to the single-piece path automatically when `weapon_class` is `Bow`, `Crossbow`, `Javelin`, `ThrowingKnife`, `ThrowingAxe` or `Stone`. Unrecognised attributes on a manifest piece pass through verbatim onto the emitted `<CraftingPiece>`, so `excluded_item_usage_features` needs no generator change, and nothing downstream checks that you set it.

### Modify

1. **Damage on a crafted weapon: edit the blade's `damage_factor`.** `<Swing damage_factor>` and `<Thrust damage_factor>` in the Blade piece's `<BladeData>` are the only damage inputs the engine reads (`Crafting.cs:134-135`). The seven `<StatContributions>` bonuses look like the obvious dial and are tooltip text in v1.4.8, with the single exception of `armor_bonus` on a Guard.
2. **Speed and handling: edit geometry, not bonuses.** Swing speed comes out of `weight`, `length` and `center_of_mass` through inertia. There is no speed attribute on a crafting piece.
3. **Reach: edit the pieces that carry it.** For a four-piece sword the pommel has `build_order="-1"` and contributes nothing to reach; length reduces to half the handle plus the guard minus its two offsets plus the blade.
4. **Damage on a single-piece weapon: edit `<Weapon>` directly.** `thrust_damage` doubles as missile damage for anything ranged.
5. **Price without touching damage: use `tier_override`, or `value`.** Price scales as `2.75` raised to the item's tier (`DefaultItemValueModel.cs:266`), so one whole tier of stats roughly triples the price; `value="..."` bypasses the formula outright.
6. **Never rename an id to rename a weapon.** The display name is the `name` attribute. Renaming `id` breaks every roster reference and every save holding the item.
7. **For a bulk damage pass**, `python tools/rebalance_weapons.py --dry-run` prints a points-based per-culture proposal. Read its paths before `--apply`: it targets a sibling `taommod` working copy plus a hard-coded install path for the crafting pieces, and it never writes `LOTRAOM_weapons.xml`, so bows and other single-piece weapons are outside its reach.

Check: `python tools/validate_moduledata.py` then `python tools/audit_polearm_shield_parity.py`
Takes effect: full game restart
Code: No code changes needed

### Delete

Deletion is the operation TAOM has got wrong most often. A seven-commit Armoury reorganisation on 2026-09-01 broke 275 references, including 212 `BROKEN_ITEM_REF` across 159 consumers from three deleted shield items alone, and it was caught from a screenshot rather than a gate, because the Armory is untracked so the commit hook never fired ([RCA](../reviews/rca-armoury-keyforce-cleanup-2026-09-01.md)).

1. **Find the consumers before deleting anything.** `python tools/audit_item_refs.py --show-locations` lists every `Item.<id>` reference that would break. For art you are also removing, `python tools/audit_deleted_mesh_impact.py` joins deleted mesh to item to troop or lord across five reference shapes, including the ones written in XSLT.
2. **Re-point the consumers first, delete second.** Every troop roster, equipment set, character-creation preset and career start naming the id has to move to a surviving item before the definition goes.
3. **Removing a `<UsablePiece>` line deletes the item, not just the smithing option.** Gate 1 runs on the template's piece pool, so a `<CraftedItem>` still naming a piece that is no longer usable fails to build, gets unregistered and vanishes (`ItemObject.cs:469-474`). Sweep the item file before touching `crafting_templates.xslt`.
4. **A stale `<AvailablePiece>` id is inert but not harmless.** `WeaponDescription.Deserialize` skips ids that do not resolve, so leaving one costs nothing today; it becomes wrong the moment somebody re-adds a piece under that id.
5. **Take a backup whose last extension is not `.xml`.** A folder-registered `<XmlName id="Items" path="LOTRLOME_items/gondor"/>` globs every `.xml` in that folder, so `body_armors.bak.xml` would load and shadow ids. Use the `.bak-<topic>-<date>` shape the sweep tool recognises; all 13 backup sidecars under the Armory's `ModuleData` follow it and none of them ends in `.xml` <!-- measured: find LOTRLOME_Armory/ModuleData -name '*.bak*' | wc -l && find LOTRLOME_Armory/ModuleData -name '*.bak*.xml' | wc -l 2026-09-05 -->. Rules and the sweep script: [module-backup-sweep.md](../reference/module-backup-sweep.md).

Check: `python tools/audit_item_refs.py --show-locations` then `python tools/validate_moduledata.py`
Takes effect: full game restart, and an existing save keeps the reference, so a deleted id shows as an empty equipment slot rather than a crash
Code: No code changes needed

## Gotchas: what fails silently and what crashes

- **The two gates fail differently, and knowing which you hit saves an hour.** Gate 1 (the piece is not in the template's `<UsablePieces>`) and a gate 2 with no matching description both end with the item unregistered and every consumer holding a broken reference. Gate 2 matching the *wrong* description leaves the item equipped with the wrong usage flags, so a troop carries it and never draws it. `docs/features/weapon-xml-pipeline.md:46-63`.
- **The first matching description becomes the primary usage, and order in the file decides it.** `Crafting.cs:566-610` walks the template's descriptions in order and takes the first match; later matches become alternatives. Native's `crafting_templates.xml` says so at line 2: "WeaponDescription order is fixed, don't change it."
- **This is why a spear beside a shield is a silent bug.** Vanilla's `TwoHandedPolearm` template lists `OneHandedPolearm` first and `TwoHandedPolearm` second. A spear whose pieces are not registered under `OneHandedPolearm` falls through to `TwoHandedPolearm`, whose features `polearm:block:long:shield:swing:thrust` minus `swing` resolve to the usage set `polearm_block_long_shield_thrust`, and that set carries the flag `requires_no_shield` (`Native/ModuleData/item_usage_sets.xml:10124-10129`). The name contains the word shield and the flag forbids one. The troop holds the spear until combat starts and then never draws it. Gate: `python tools/audit_polearm_shield_parity.py`.
- **A `bo_` collision body the engine cannot resolve hangs mission load instead of erroring.** `PreloadHelper.WaitForMeshesToBeLoaded` polls every registered body name and exits only when each resolves: no crash, no log line, one core at 100 percent. Two `body_name` typos shipped this way in Armory v2.0.8. `docs/features/mesh-ref-validation.md:5-9`.
- **The inverse also happens: a name that looks mistyped can be the correct one.** `wm_isengard_shield_a04` references `bo_capwm_isengard_shield_a02_clean` and the asset is packaged under exactly that misspelling. A pass from `validate_mesh_refs.py` on a name that looks wrong is evidence the name is right. `docs/features/mesh-ref-validation.md:13`.
- **`validate_mesh_refs.py` used to default to a directory that excluded the crafting pieces.** Its `--items` default is now `ModuleData/`; a clean run only ever means clean within the scope you pointed it at.
- **An unknown `modifier_group` resolves to null with no warning and the item never rolls a quality prefix.** The 20 legal group ids live in `Native/ModuleData/item_modifiers_groups.xml`: `arrow`, `axe`, `axe_throwing`, `bolt`, `bow`, `chain`, `cheap_weapon`, `cloth`, `cloth_unarmoured`, `companion`, `crossbow`, `horse`, `knife_throwing`, `leather`, `mace`, `plate`, `polearm`, `shield`, `spear_dart_throwing`, `sword` <!-- measured: grep -c '<ItemModifierGroup$' Native/ModuleData/item_modifiers_groups.xml 2026-09-05 -->. Names such as `legendary_plate` are ItemModifier ids from `item_modifiers.xml`, not group ids, and writing one is the same silent null. One row in the Armory's weapons file already has this defect: `wm_cave_troll_1h_mace_a` writes `modifier_group="false"` at `LOTRAOM_weapons.xml:931`, so that mace never rolls a modifier <!-- measured: grep -n 'modifier_group="false"' LOTRLOME_Armory/ModuleData/LOTRLOME_items/LOTRAOM_weapons.xml 2026-09-05 -->.
- **Culture is the one reference that throws instead of skipping.** `culture="mordor"` raises `MBInvalidReferenceException` and kills the load; `culture="Culture.mordor"` is required. An unknown but correctly prefixed id does not fail either: the object manager creates a blank placeholder culture (`MBObjectManager.cs:724-729`).
- **Booleans are parsed three different ways.** `is_unique`, `is_default` and `is_hidden` accept `true`, `True` and `1`. `full_scale`, `multiplayer_item`, `is_merchandise` and `has_modifier` are raw string compares, so `full_scale="True"` reads as false. `has_lower_holster_priority` and `recalculate_body` use a strict parse that throws on anything but `true` or `false`. Write them all lowercase.
- **Enum case-sensitivity is inconsistent.** Safe with any case: `piece_type` on `<CraftingPiece>`, `<Flag name>`, and the two `damage_type` attributes. Case-sensitive and fatal on a wrong capital: `item_type`, `<PieceData piece_type>`, `stat_type`, `weapon_class`, `<WeaponFlag value>` and the `Type` on a `<Piece>` row, which is the one attribute in these files spelled with a capital letter.
- **A misspelled `<Material id>` costs iron ore and says nothing.** The value goes through a tolerant enum parse and falls back to the first member, `IronOre`.
- **Most child elements replace rather than merge.** Inside one `<CraftingPiece>`, a second `<Materials>`, `<Flags>`, `<BladeData>` or `<StatContributions>` discards the first. The exceptions that append are the piece-side `<CraftingTemplates>` (no duplicate check) and the template-side `<UsablePieces>` (with one).
- **`item_usage_features` tokens are name fragments, not capabilities.** The surviving tokens are joined with underscores and the result must be one of the 58 rows in `Native/ModuleData/item_usage_sets.xml` <!-- measured: grep -c '<item_usage_set$' Native/ModuleData/item_usage_sets.xml 2026-09-05 -->. Nothing validates the joined name. `docs/reference/item-usage-features.md:20-24`.
- **An `<Item>` with no `Type` attribute stays `Invalid`.** The entire type block sits inside a null check (`ItemObject.cs:625`), so omitting it silently produces an item nothing can equip, even with a perfectly good `<Weapon>` component.
- **A shield needs exactly one offhand bone flag.** `item_usage="hand_shield"` pairs with `ForceAttachOffHandPrimaryItemBone="true"` and `item_usage="shield"` with `ForceAttachOffHandSecondaryItemBone="true"`, never both and never neither (`docs/reference/armory-shield-audit.md:36-37`). Bows use the primary bone flag, as the worked example above shows.

## Numbers in this chapter

Counting commands run from the game's `Modules` folder. Line references into the decompile are v1.4.8.

| Number | Command | Date |
|---|---|---|
| 672 `<CraftingPiece>` rows in the Armory (Blade 264, Handle 210, Pommel 108, Guard 90) | `grep -c '</CraftingPiece>' LOTRLOME_Armory/ModuleData/LOTRLOME_crafting_pieces.xml` and `grep -c 'piece_type="Blade"' ...` for each type | 2026-09-05 |
| 347 `<CraftedItem>` and 68 `<Item>` rows in `LOTRAOM_weapons.xml` | `grep -c '</CraftedItem>' LOTRLOME_Armory/ModuleData/LOTRLOME_items/LOTRAOM_weapons.xml` and `grep -c '</Item>' ...` | 2026-09-05 |
| Templates used by those 347: TwoHandedPolearm 104, OneHandedSword 81, TwoHandedSword 43, TwoHandedAxe 40, OneHandedAxe 33, TwoHandedMace 25, Mace 17, Pike 2, Javelin 2 | `grep -o 'crafting_template="[^"]*"' LOTRLOME_Armory/ModuleData/LOTRLOME_items/LOTRAOM_weapons.xml \| sort \| uniq -c \| sort -rn` | 2026-09-05 |
| The 68 single-piece items: 35 Bow, 28 Arrow, 3 Crossbow, 2 Bolt | `grep -o 'weapon_class="[^"]*"' LOTRLOME_Armory/ModuleData/LOTRLOME_items/LOTRAOM_weapons.xml \| sort \| uniq -c` | 2026-09-05 |
| 1312 `<AvailablePiece>` rows and 778 `<UsablePiece>` rows | `grep -c '<AvailablePiece ' LOTRLOME_Armory/ModuleData/weapon_descriptions.xslt` and `grep -c '<UsablePiece ' LOTRLOME_Armory/ModuleData/crafting_templates.xslt` | 2026-09-05 |
| 14 weapon descriptions and 9 crafting templates the Armory extends | `grep -c 'xsl:template match="WeaponDescription' LOTRLOME_Armory/ModuleData/weapon_descriptions.xslt` and `grep -c 'xsl:template match="CraftingTemplate' LOTRLOME_Armory/ModuleData/crafting_templates.xslt` | 2026-09-05 |
| Vanilla: 12 `<CraftingTemplate>`, 22 `<WeaponDescription>`, 805 `<CraftingPiece>`, 58 `<item_usage_set>`, 20 `<ItemModifierGroup>` | `grep -c '<CraftingTemplate$' Native/ModuleData/crafting_templates.xml`, and the same shape for `weapon_descriptions.xml`, `crafting_pieces.xml`, `item_usage_sets.xml`, `item_modifiers_groups.xml` | 2026-09-05 |
| 9 `<AvailablePiece>` rows inside the `TAOM-1H-POLEARM` marker block | `sed -n '673,683p' LOTRLOME_Armory/ModuleData/weapon_descriptions.xslt \| grep -c '<AvailablePiece'` | 2026-09-05 |
| 1 illegal `modifier_group` value in the weapons file, at line 931 | `grep -n 'modifier_group="false"' LOTRLOME_Armory/ModuleData/LOTRLOME_items/LOTRAOM_weapons.xml` | 2026-09-05 |
| 8 rows set `is_merchandise="false"` | `grep -c 'is_merchandise="false"' LOTRLOME_Armory/ModuleData/LOTRLOME_items/LOTRAOM_weapons.xml` | 2026-09-05 |
| `scale_factor` values: 100 x955, 80 x46, 110 x14, 90 x7, 70 x2 | `grep -o 'scale_factor="[^"]*"' LOTRLOME_Armory/ModuleData/LOTRLOME_items/LOTRAOM_weapons.xml \| sort \| uniq -c \| sort -rn` | 2026-09-05 |
| `excluded_item_usage_features` in the Armory: swing 22, thrust 21, widegrip 13, long 2 | `grep -o 'excluded_item_usage_features="[^"]*"' LOTRLOME_Armory/ModuleData/LOTRLOME_crafting_pieces.xml \| sort \| uniq -c` | 2026-09-05 |
| `<Flag name>` values, including 4 spelled `CanKnockdown` beside 141 `CanKnockDown` | `grep -o '<Flag name="[^"]*"' LOTRLOME_Armory/ModuleData/LOTRLOME_crafting_pieces.xml \| sort \| uniq -c` | 2026-09-05 |
| 13 backup sidecars under the Armory's `ModuleData`, 0 of them ending in `.xml` | `find LOTRLOME_Armory/ModuleData -name '*.bak*' \| wc -l` and `find LOTRLOME_Armory/ModuleData -name '*.bak*.xml' \| wc -l` | 2026-09-05 |
| 531 of vanilla's 805 crafting pieces carry `<BuildData>` | `grep -c '<BuildData' Native/ModuleData/crafting_pieces.xml` and `grep -c '</CraftingPiece>' Native/ModuleData/crafting_pieces.xml` | 2026-09-05 |
| 3 of vanilla's 12 crafting templates set `piece_type_to_scale_holster_with="Blade"` | `grep -c 'piece_type_to_scale_holster_with="Blade"' Native/ModuleData/crafting_templates.xml` | 2026-09-05 |
| SubModule rows: CraftingPieces 195, CraftingTemplates 205, WeaponDescriptions 213, the weapons item file 313 | `grep -n 'CraftingPieces\|CraftingTemplates\|WeaponDescriptions\|LOTRAOM_weapons' LOTRLOME_Armory/SubModule.xml` | 2026-09-05 |

## Read next

- [weapon-creation-workflow.md](../ai-includes/weapon-creation-workflow.md), the full asset-to-XML walkthrough including the mesh naming convention and the whole-number rule.
- [weapon-xml-pipeline.md](../features/weapon-xml-pipeline.md), the manifest-driven generator and the two-gate analysis this chapter distils.
- [item-usage-features.md](../reference/item-usage-features.md), the token vocabulary and vanilla's own exclusion convention.
- [mesh-ref-validation.md](../features/mesh-ref-validation.md), the collision-body load hang and the tool that catches it.
- [armory-shield-audit.md](../reference/armory-shield-audit.md), shield grips, block arcs and the offhand bone flags.
- [armory-guide.md](../reference/armory-guide.md), the canonical folder per item-id prefix and the duplicate-id shadowing rule.
- [module-backup-sweep.md](../reference/module-backup-sweep.md), the backup naming rule that keeps a sidecar from loading as data.
- [rca-armoury-keyforce-cleanup-2026-09-01.md](../reviews/rca-armoury-keyforce-cleanup-2026-09-01.md), what a deletion without a reference sweep costs.
- [tools/README.md](../../tools/README.md), the index of the data-generation and rebalancing scripts, and [tools/BannerlordCraftingTool/](../../tools/BannerlordCraftingTool/README.md), a standalone Windows app that previews assembled crafting-piece offsets without launching the game.

---

<!-- backlinks-start auto-generated; edit lint_docs.py / build_backlinks.py to change -->

## Referenced by

- [docs/INDEX.md](../INDEX.md)
- [docs/modding/equipment-rosters.md](./equipment-rosters.md)
- [docs/modding/file-catalogue.md](./file-catalogue.md)
- [docs/modding/items-armor.md](./items-armor.md)
- [docs/modding/items-shields.md](./items-shields.md)
- [docs/modding/load-order-and-dependencies.md](./load-order-and-dependencies.md)
- [docs/modding/README.md](./README.md)
- [docs/modding/recipe-new-mod-from-zero.md](./recipe-new-mod-from-zero.md)
- [docs/modding/troops.md](./troops.md)
- [docs/modding/troubleshooting.md](./troubleshooting.md)

<!-- backlinks-end -->
