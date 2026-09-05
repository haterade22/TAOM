# Armour items

## What this file is

An armour file is a list of `<Item>` rows, each one a helmet, chest, boots, gloves or cape that a troop, a lord or the player can wear. Every row ties three things together: an id that rosters point at, a `mesh=` name that the art tree must actually contain, and an `<Armor>` block holding the protection numbers. TAOM's own armour lives in `LOTRLOME_Armory`, but vanilla armour loads alongside it and TAOM troops wear 32 vanilla pieces today, so "the armoury" is not the whole item pool.

## Where it lives and how it is registered

TAOM-authored armour is one file per slot inside a per-culture folder:

```
LOTRLOME_Armory/ModuleData/LOTRLOME_items/gondor/head_armors.xml
LOTRLOME_Armory/ModuleData/LOTRLOME_items/gondor/body_armors.xml
LOTRLOME_Armory/ModuleData/LOTRLOME_items/gondor/leg_armors.xml
LOTRLOME_Armory/ModuleData/LOTRLOME_items/gondor/arm_armors.xml
LOTRLOME_Armory/ModuleData/LOTRLOME_items/gondor/shoulder_armors.xml
LOTRLOME_Armory/ModuleData/LOTRLOME_items/gondor/starter_armors.xml
```

This file lives in the game install, not the repo; a module reinstall reverts hand edits, so land a repo-side validator gate with any fix.

**Registration.** `LOTRLOME_Armory/SubModule.xml` carries 33 `<XmlName>` rows, of which 21 register items:

<!-- excerpt file="LOTRLOME_Armory/SubModule.xml" -->
```xml
	<Xmls>
		<XmlNode>
			<XmlName id="Items" path="LOTRLOME_items/gondor"/>
			<IncludedGameTypes>
				<GameType value = "Campaign"/>
				<GameType value = "CampaignStoryMode"/>
				<GameType value = "CustomGame"/>
				<GameType value = "EditorGame"/>
			</IncludedGameTypes>
		</XmlNode>
```

That `path` names a **folder**, and the loader takes every `.xml` file inside it. Three of the rows instead name a **single file** without its extension, as in `path="LOTRLOME_items/LOTRAOM_weapons"`. Eighteen folders are registered (`arnor`, `dale`, `dol_guldur`, `dunland`, `erebor`, `gondor`, `gundabad`, `harad`, `iron_hills`, `isengard`, `mercenary`, `mirkwood`, `mordor`, `rhun`, `rivendell`, `rohan`, `thenn`, `troll`) plus three single files (`LOTRAOM_weapons`, `LOTRAOM_shields`, `LOTRAOM_horses`).

- **Root element** `<Items>`, **per-entry element** `<Item>` (weapons in the same registry also use `<CraftedItem>`, covered in [items-weapons-and-crafting](items-weapons-and-crafting.md)).
- **Engine class** `TaleWorlds.Core.ItemObject`, registered as `RegisterType<ItemObject>("Item", "Items", 4u)` (`Game.cs:309`). The deserializer is `ItemObject.Deserialize` (`ItemObject.cs:420-706`).
- **Vanilla items load too, and you can change one without touching the base game.** `SandBoxCore/SubModule.xml` registers `<XmlName id="Items" path="items"/>`, and `Main/_Module/ModuleData/` holds 8 XSLT files, none of them for items. That is not the same as having no seam. Every file registered under the id `Items` is merged into one document before a single row is deserialized, and because `Items.xsd` makes `Item@id` unique (`Items.xsd:499-503`), a later module's row carrying a vanilla id is layered onto the earlier one rather than added beside it: the later attributes overwrite one at a time, and every attribute the later row omits keeps its vanilla value (`MBObjectManager.cs:799-817` and `:846-874`). The schema marks `<ItemComponent>` and `<Armor>` `AlwaysPreferMerge`, so the protection block inside merges the same way. To raise a vanilla helmet's `head_armor`, redefine that id in `LOTRLOME_Armory`, which already registers `Items` and loads after `SandBoxCore`, and write only the attributes you mean to change. If you want the new row to stand on its own rather than inherit, add `_replaceWhileMerging="true"` to it: the engine injects that attribute into every type in every schema before validating (`MBObjectManager.cs:1092`), and it clears the earlier row's attributes and children before merging (`MBObjectManager.cs:804-808`, `:829-832`). Do not edit `SandBoxCore/ModuleData/items/head_armors.xml`: a Steam file verification or a game patch puts it straight back.
- **The folder name is not a culture id.** The folder `dol_guldur` holds items whose attribute reads `culture="Culture.dolguldur"`. `Main/_Module/ModuleData/taom_spcultures.xml` defines 24 cultures; eight of their ids match a folder once `dolguldur` is mapped to `dol_guldur`, eight more are `_raiders` or `_soldiers` style variants of one, and eight (`abanissa`, `bluecraig`, `goblin`, `lindon`, `lothlorien`, `mistymountainorcs`, `shaghana`, `umbar`) have no folder under any spelling. Where their armour is meant to live is not written down anywhere in `docs/`, so the honest answer today is "read the folder list above and pick the nearest one".
- **Ten folders have no id in that file**: `arnor`, `dale`, `dunland`, `harad`, `iron_hills`, `mercenary`, `rhun`, `rohan`, `thenn`, `troll`. They serve cultures TAOM builds by renaming a vanilla one in `Main/_Module/ModuleData/spcultures.xslt`, which is why 677 armoury items carry `culture="Culture.khuzait"` (renamed to Easterlings at `spcultures.xslt:911-917`). See [cultures](cultures.md).

## Attributes

### `<Item>`, the attributes an armour row uses

<!-- engine-table type="TaleWorlds.Core.ItemObject" file="Core/TaleWorlds.Core/TaleWorlds.Core/ItemObject.cs" method="Deserialize" inert="using_arm_band,lod_atlas_index" -->

| Attribute | Type | Required | Default when absent | What it does | Read at (file:line) |
|---|---|---|---|---|---|
| `id` | string | yes | none, the load throws | The permanent code-name every roster points at. Renaming it breaks every reference. | `ItemObject.cs:420` |
| `name` | localized string | yes | none, the load throws | The inventory name. Write it as `{=key}English text`; see [strings-and-localization](strings-and-localization.md). | `ItemObject.cs:492` |
| `mesh` | string | no | null, nothing is drawn | The multi-mesh name in the art tree. This is the art hook: a wrong string means an invisible piece. | `ItemObject.cs:503` |
| `Type` | enum | no | `Invalid` | Which slot the piece occupies: `HeadArmor`, `BodyArmor`, `LegArmor`, `HandArmor`, `Cape`, `HorseHarness`. Parsed with `ignoreCase`. Omit it and the item is `Invalid` even with a perfect `<Armor>` block. | `ItemObject.cs:625` |
| `culture` | ref, prefixed | no | null | `culture="Culture.gondor"`. Drives shop stock and AI kit choice only. A bare id with no dot throws; a wrong id after the dot silently creates a blank placeholder. | `ItemObject.cs:540` |
| `weight` | float | no | `1.0` | Kilograms. Feeds encumbrance. An armour piece with no `weight` silently weighs 1 kg. | `ItemObject.cs:546` |
| `appearance` | float | no | `0.5` | A price multiplier, not a combat stat: `value = base * 2.75^tier * (1 + 0.2*(appearance-1)) + 100*max(0, appearance-1)`. | `ItemObject.cs:553` |
| `difficulty` | int | no | `0` | Minimum skill to use the item without the not-qualified penalty. `0` for all shipped armour. | `ItemObject.cs:548` |
| `is_merchandise` | string compare | no | absent means it IS merchandise | `is_merchandise="false"` keeps the piece out of shops and loot. Any value other than the literal `true` also counts as false. | `ItemObject.cs:498` |
| `item_category` | ref, bare id | no | null, then auto-assigned from type and tier | The trade-goods bucket. Leave it off armour and let the engine classify; an unknown id resolves to null with no warning. | `ItemObject.cs:541` |
| `value` | int | no | absent runs the price formula | A hard gold price that bypasses the formula outright. | `ItemObject.cs:672` |
| `tier_override` | float | no | absent computes the tier from stats | Forces the tier, which drives price, shop stock and AI gear preference. | `ItemObject.cs:666` |
| `scale_factor` | float | no | `1.0` | Uniform size multiplier on the model. | `ItemObject.cs:566` |
| `prefab` | string | no | `""` | A scene prefab bolted onto the model for extra props or particles. | `ItemObject.cs:531` |
| `using_tableau` | bool | no | `false` | The texture is painted at runtime from a heraldry tableau. Used by shields, not by armour. | `ItemObject.cs:560` |
| `multiplayer_item` | string compare | no | `false` | Marks the item multiplayer-only. Leave it off in a single-player conversion. | `ItemObject.cs:493` |

### `<Item>`, the rest of what the same deserializer reads

These belong to weapons, mounts and crafted items. They are listed so the table is complete against `ItemObject.Deserialize`, not because an armour author sets them.

<!-- engine-table type="TaleWorlds.Core.ItemObject" file="Core/TaleWorlds.Core/TaleWorlds.Core/ItemObject.cs" method="Deserialize" inert="using_arm_band,lod_atlas_index" -->

| Attribute | Type | Required | Default when absent | What it does | Read at (file:line) |
|---|---|---|---|---|---|
| `body_name` | string | no | null | Physics body used while the item is held or on the ground. | `ItemObject.cs:525` |
| `shield_body_name` | string | no | null | The blocking surface of a shield. See [items-shields](items-shields.md). | `ItemObject.cs:529` |
| `holster_body_name` | string | no | null | Physics body used while the item is sheathed. | `ItemObject.cs:528` |
| `holster_mesh` | string | no | null | Mesh drawn while the weapon is sheathed. | `ItemObject.cs:508` |
| `holster_mesh_with_weapon` | string | no | null | Scabbard mesh drawn while the weapon is out. | `ItemObject.cs:509` |
| `flying_mesh` | string | no | null | Mesh used while a projectile is in flight. | `ItemObject.cs:510` |
| `item_holsters` | string list, `:` separated | no | four null slots | Which body attachment points the item may hang from. | `ItemObject.cs:512` |
| `has_lower_holster_priority` | strict bool | no | `false` | Yields the holster slot to another item. Read only inside the `item_holsters` branch, so it is ignored without it. | `ItemObject.cs:515` |
| `holster_position_shift` | `x,y,z` | no | zero vector | Nudges the sheathed item on the body. Fewer than three components throws. | `ItemObject.cs:524` |
| `recalculate_body` | strict bool | no | `false` | Rebuilds the physics body from the mesh at spawn. | `ItemObject.cs:530` |
| `skeleton_name` | string | no | null | Skeleton for items that animate on their own, such as banners. | `ItemObject.cs:526` |
| `static_animation_name` | string | no | null | A looping animation played on the item. | `ItemObject.cs:527` |
| `AmmoOffset` | `x,y,z` | no | unset | Moves the nocked arrow or bolt. Note the capital A. Putting it on an item with no `<Weapon>` is an immediate crash at load. | `ItemObject.cs:644` |
| `IsFood` | bool | no | `false` | Marks the item as party food. Note the capital I. | `ItemObject.cs:555` |
| `using_arm_band` | string | no | null | Read but has no effect: no consumer anywhere in the v1.4.8 dump and no use in vanilla data. | `ItemObject.cs:561` |
| `lod_atlas_index` | int | no | `-1` | Selects a shared texture atlas for the distant model. Read and stored, but its effect is not determined from the engine: no managed consumer exists in the dump. | `ItemObject.cs:547` |
| `crafting_template` | ref, bare id | on `<CraftedItem>` | none, the load throws | Which crafting template a pre-built weapon is assembled from. | `ItemObject.cs:434` |
| `has_modifier` | string compare | no | `true` | `has_modifier="false"` stops a crafted weapon rolling a quality modifier. | `ItemObject.cs:435` |
| `modifier_group` | ref, bare id | no | the template's own group | On `<CraftedItem>` only. On a plain `<Item>` this attribute belongs on the component, not here. | `ItemObject.cs:436` |

### `<Armor>`, the protection block

<!-- engine-table type="TaleWorlds.Core.ArmorComponent" file="Core/TaleWorlds.Core/TaleWorlds.Core/ArmorComponent.cs" method="Deserialize" inert="no_slim,tail_cover_type" -->

| Attribute | Type | Required | Default when absent | What it does | Read at (file:line) |
|---|---|---|---|---|---|
| `head_armor` | int | no | `0` | Protection on the head. Which of the four numbers matters is decided by the item's `Type`. | `ArmorComponent.cs:149` |
| `body_armor` | int | no | `0` | Protection on the torso. Doubles as the armour value of a `HorseHarness`. | `ArmorComponent.cs:150` |
| `leg_armor` | int | no | `0` | Protection on the legs. | `ArmorComponent.cs:151` |
| `arm_armor` | int | no | `0` | Protection on the arms. | `ArmorComponent.cs:152` |
| `family_type` | int | no | `0`, which means human | Must match the mount's Monster family type or the harness cannot be fitted, with no error message. See [items-mounts-and-harness](items-mounts-and-harness.md). | `ArmorComponent.cs:153` |
| `maneuver_bonus` | int | no | `0` | Flat addition to the mount's manoeuvre when worn as a harness. Does nothing on human armour. | `ArmorComponent.cs:154` |
| `speed_bonus` | int | no | `0` | Flat addition to the mount's speed. Harness only. | `ArmorComponent.cs:155` |
| `charge_bonus` | int | no | `0` | Flat addition to the mount's charge damage. Harness only. | `ArmorComponent.cs:156` |
| `material_type` | enum | no | `None` | `None`, `Cloth`, `Leather`, `Chainmail`, `Plate`. Drives hit sounds and impact effects. Parsed **case sensitively**: `plate` throws and kills the load, `Plate` works. | `ArmorComponent.cs:157` |
| `has_gender_variations` | bool | no | **`true`** | When true the engine looks for gendered mesh variants. The default is the opposite of what shipped data implies. | `ArmorComponent.cs:160` |
| `body_mesh_type` | string | no | `Normal` | Only the exact lowercase `upperbody` or `shoulders` do anything; any other text, a typo included, silently means normal. Unused in the armoury. | `ArmorComponent.cs:165` |
| `body_deform_type` | string | no | `Medium` | Only `large` or `skinny` do anything. Unused in the armoury. | `ArmorComponent.cs:178` |
| `hair_cover_type` | enum | no | `None` | `None`, `Type1` to `Type4`, `All`. Hides hair so it does not poke through a helmet. Parsed with `ignoreCase`, so `all` works. | `ArmorComponent.cs:190` |
| `beard_cover_type` | enum | no | `None` | Same value set and same case handling, for the beard. | `ArmorComponent.cs:191` |
| `mane_cover_type` | enum | no | `None` | `None`, `Type1`, `Type2`, `All`. Hides a mount's mane under a harness. | `ArmorComponent.cs:192` |
| `tail_cover_type` | enum | no | `None` | `None` or `All`. Read and stored, but no consumer was found in the dump, so its effect is not determined from the engine. | `ArmorComponent.cs:193` |
| `stealth_factor` | int | no | `0` | Shown as a stealth bonus in the inventory. Unused in the armoury. | `ArmorComponent.cs:194` |
| `reins_mesh` | string | no | `""` | Harness reins mesh. Unused in the armoury. | `ArmorComponent.cs:195` |
| `covers_head` | bool | no | `false` | Hides the bare head skin. Set on zero shipped items, TAOM and vanilla alike, because it also switches off facegen head scaling (`ItemObject.cs:130-140`). | `ArmorComponent.cs:196` |
| `covers_body` | bool | no | `false` | Hides the bare torso skin. | `ArmorComponent.cs:197` |
| `covers_hands` | bool | no | `false` | Hides the bare hand skin. Set it when your sleeve mesh already draws the forearm. | `ArmorComponent.cs:198` |
| `covers_legs` | bool | no | `false` | Hides the bare leg skin. Set it on boots and greaves whose mesh draws the shin. | `ArmorComponent.cs:199` |
| `no_slim` | bool | no | `false` | Read but has no effect: no consumer in the dump and no use in vanilla data. | `ArmorComponent.cs:216` |

The four `covers_*` attributes are **visibility switches, not coverage claims**. The deserializer builds a mask of what stays visible and only clears a bit when the attribute is true (`ArmorComponent.cs:196-215`). Omitting `covers_legs` on a boot leaves the bare leg drawn through the boot, which looks like a missing mesh but is not one. An armour piece that is genuinely invisible has a bad `mesh=` string or sits in an unregistered file.

### `modifier_group`, read by the shared component base

<!-- engine-table type="TaleWorlds.Core.ItemComponent" file="Core/TaleWorlds.Core/TaleWorlds.Core/ItemComponent.cs" method="Deserialize" -->

| Attribute | Type | Required | Default when absent | What it does | Read at (file:line) |
|---|---|---|---|---|---|
| `modifier_group` | ref, bare id | no | null, so the item never rolls a quality | Which pool of Rusty / Fine / Masterwork / Legendary variants the item draws from. Written on the `<Armor>` element, but read by the base every component shares. An unknown id resolves to null with no warning. | `ItemComponent.cs:21` |

The legal ids are the 20 defined in `Native/ModuleData/item_modifiers_groups.xml`, the only file that defines them:

<!-- engine-ref type="TaleWorlds.Core.ItemModifierGroup" file="Core/TaleWorlds.Core/TaleWorlds.Core/ItemModifierGroup.cs" lines="43-49" -->

| Armour groups | Weapon groups | Other |
|---|---|---|
| `plate`, `chain`, `leather`, `cloth`, `cloth_unarmoured` | `sword`, `axe`, `mace`, `polearm`, `bow`, `crossbow`, `arrow`, `bolt`, `cheap_weapon`, `axe_throwing`, `knife_throwing`, `spear_dart_throwing` | `shield`, `horse`, `companion` |

`legendary_plate` and its siblings are **ItemModifier ids, not group ids**. The armour a legendary roll adds is `plate` +12, `chain` +9, `shield` +8, `leather` +7, `cloth` +5, `cloth_unarmoured` +3, applied flat and independently to every non-zero stat, so a two-stat cape takes the bonus twice and a stat of exactly `0` is immune ([armor-balance](../features/armor-balance.md) "two-tier invariant").

## Child elements

<!-- engine-table type="TaleWorlds.Core.ItemObject" file="Core/TaleWorlds.Core/TaleWorlds.Core/ItemObject.cs" method="Deserialize" inert="using_arm_band,lod_atlas_index" -->

| Element | Type | Required | Default when absent | What it does | Read at (file:line) |
|---|---|---|---|---|---|
| `<ItemComponent>` | wrapper | no | the item is a bare object at tier 1 | Holds the one component that gives the item its behaviour. | `ItemObject.cs:570` |
| `<Armor>` | component | for armour | no component | The protection block above. A second `<Armor>` sibling silently discards the first. | `ItemObject.cs:579` |
| `<Weapon>` | component | for weapons | no component | See [items-weapons-and-crafting](items-weapons-and-crafting.md). Repeated `<Weapon>` children append rather than replace. | `ItemObject.cs:582` |
| `<Horse>` | component | for mounts | no component | See [items-mounts-and-harness](items-mounts-and-harness.md). | `ItemObject.cs:585` |
| `<Trade>` | component | for trade goods | no component | Trade-good pricing. | `ItemObject.cs:588` |
| `<Food>` | component | never | no component | Retired. It logs an assert and leaves the item with no component at all. Use `<Trade>`. | `ItemObject.cs:591` |
| `<Banner>` | component | for banners | no component | See [banners-and-heraldry](banners-and-heraldry.md). | `ItemObject.cs:596` |
| `<Flags>` | flag set | no | no flags | A sibling of `<ItemComponent>`, not a child. Fully defines the flag set: the field is zeroed first, so there is nothing to inherit. | `ItemObject.cs:611` |
| `<CraftedItem>` | entry element | no | not applicable | The alternative per-entry element for a pre-built crafted weapon, handled in its own branch. | `ItemObject.cs:421` |
| `<Pieces>` | list | on `<CraftedItem>` | none, the load throws | The crafting pieces a `<CraftedItem>` is assembled from. | `ItemObject.cs:451` |
| `<Piece>` | list entry | on `<CraftedItem>` | none, the load throws | One piece. A duplicate `Type` silently discards the earlier one. | `ItemObject.cs:456` |

Any other element name inside `<ItemComponent>` throws `Wrong ItemComponent type.` and stops the load (`ItemObject.cs:599`). That is the one armour mistake that fails loudly.

### `<Flags>` attributes

Flag names are read by walking the C# enum, not the XML, so an unrecognised attribute name is ignored in silence. A flag is set when the attribute exists and its value is anything other than `false`.

<!-- engine-ref type="TaleWorlds.Core.ItemFlags" file="Core/TaleWorlds.Core/TaleWorlds.Core/ItemFlags.cs" lines="6-27" -->

| Flag | What it does for an armour author |
|---|---|
| `UseTeamColor` | Tints the mesh with the faction colour. This is what made the 57 per-colour Erebor meshes redundant. |
| `Civilian` | The piece is allowed in town and civilian outfits. |
| `DoesNotHideChest` | A cape or cloak that must not suppress the body mesh. |
| `NotUsableByFemale`, `NotUsableByMale` | The mesh exists for one gender only. |
| `Stealth` | Counts toward stealth handling. |
| `ForceAttachOffHandPrimaryItemBone`, `ForceAttachOffHandSecondaryItemBone`, `AttachmentMask` | Shield attachment, see [items-shields](items-shields.md). |
| `DropOnWeaponChange`, `DropOnAnyAction`, `CannotBePickedUp`, `CanBePickedUpFromCorpse`, `QuickFadeOut`, `WoodenAttack`, `WoodenParry`, `HeldInOffHand`, `HasToBeHeldUp`, `DoNotScaleBodyAccordingToWeaponLength`, `NotStackable`, `DoesNotSpawnWhenDropped` | Weapon and prop behaviour, not armour. |

## Worked example

A Dol Amroth helmet, copied whole out of the shipped file:

<!-- example file="LOTRLOME_Armory/ModuleData/LOTRLOME_items/gondor/head_armors.xml" id="sk_gd_dol_helmet_med_a" -->
```xml
    <Item
        id="sk_gd_dol_helmet_med_a"
        name="{=aom_sk_gd_dol_helmet_med_a_name}[Gondor] Dol Amroth Helmet"
        subtype="head_armor"
        mesh="sk_gd_dol_helmet_med_a"
        culture="Culture.gondor"
        is_merchandise="true"
        weight="2.5"
        difficulty="0"
        appearance="3"
        Type="HeadArmor">
        <ItemComponent>
            <Armor head_armor="24" has_gender_variations="false" hair_cover_type="type2" modifier_group="chain" material_type="Chainmail" beard_cover_type="none" />
        </ItemComponent>
        <Flags UseTeamColor="true" />
    </Item>
```

The three attributes a reader changes first:

1. **`head_armor="24"`** is the only combat number on the row. 24 is the `medium` head baseline in `tools/rebalance_armor.py`, and the troop wearing it is level 16, inside the 14 to 18 medium band.
2. **`weight="2.5"`** is the medium head weight from the same table. Protection and weight move together, or the culture's identity drifts.
3. **`appearance="3"`** never touches combat. It multiplies price. With `head_armor="24"` on a `HeadArmor`, the engine computes a tier of 3.056 and a price of 3,281 denars; drop `appearance` to 1 and the same helmet costs 2,201.

Two attributes worth reading rather than changing: `subtype="head_armor"` is **not read by the engine at all** (no deserializer in the v1.4.8 dump reaches for it), and `hair_cover_type="type2"` is what stops hair poking through the helmet. Note that `covers_head` is absent, deliberately: setting it would hide the face and switch off facegen head scaling.

A body row from the same culture, showing the two-stat shape that tiers up fast:

<!-- example file="LOTRLOME_Armory/ModuleData/LOTRLOME_items/gondor/body_armors.xml" id="sk_gd_ano_inf_chest_heavy_a" -->
```xml
    <Item id="sk_gd_ano_inf_chest_heavy_a" name="{=aom_sk_gd_ano_inf_chest_heavy_a_name}[Gondor] Anorien Infantry Heavy Armour A" subtype="body_armor" mesh="sk_gd_ano_inf_chest_heavy_a" culture="Culture.gondor" is_merchandise="true" weight="18.0" difficulty="0" appearance="4" Type="BodyArmor">
        <ItemComponent>
            <Armor body_armor="43" arm_armor="14" has_gender_variations="false" covers_body="true" modifier_group="plate" material_type="Plate" />
        </ItemComponent>
        <Flags UseTeamColor="true" />
    </Item>
```

All four armour numbers on one row sum into the tier, so this chest reaches tier 5.300 and 41,203 denars off `43 + 14`. That is the mechanism behind expensive-looking TAOM gear: nobody set a price, the stats did.

The roster that puts the helmet on a soldier lives in the repo, not the armoury:

<!-- excerpt file="Main/_Module/ModuleData/troops/troops_gondor.xml" -->
```xml
    <Equipments>
      <EquipmentRoster>
        <equipment slot="Item0" id="Item.wm_gondor_sword_a01" />
        <equipment slot="Item1" id="Item.gond_shield_two_swan" />
        <equipment slot="Head" id="Item.sk_gd_dol_helmet_med_a" />
        <equipment slot="Body" id="Item.sk_gd_dol_chainmail_a" />
        <equipment slot="Cape" id="Item.sk_gd_dol_pauld_noble_med_a" />
        <equipment slot="Gloves" id="Item.sk_gd_dol_bracer_med_a" />
        <equipment slot="Leg" id="Item.sk_gd_dol_grvs_light_a" />
      </EquipmentRoster>
      <EquipmentSet id="battania_troop_civilian_template_t2" equipmentType="Civilian" />
    </Equipments>
```

That is `gondor_da_noble`, level 16. Slot names and roster shapes are [equipment-rosters](equipment-rosters.md); the troop row around them is [troops](troops.md).

## Recipes: Add / Modify / Delete

### Add

1. **Find the canonical folder before anything else.** Grep every folder for the id prefix you are about to use: `grep -rl 'sk_gd_dol_' <armory>/ModuleData/LOTRLOME_items/`. The first folder that already holds that prefix is the home. Authoring the same id into a second folder does not give you two items and it does not hide one of them. Both files are registered under the id `Items`, so the two rows are merged into a single hybrid before the engine reads either: the file the loader reaches second wins attribute by attribute and the first one supplies everything the second leaves out (`MBObjectManager.cs:846-874`). That is harder to spot than a plain duplicate, because nothing is missing and nothing is logged. The per-prefix table is in [armory-guide](../reference/armory-guide.md).
2. **Confirm the mesh exists** before you write its name. Search the generated inventory: `grep '^sk_gd_dol_helmet' docs/reference/armory-catalogue/catalogue.tsv`. The `referenced` column says whether any item already names it. If the mesh does not exist yet, it has to come through the art pipeline first: FBX and textures compiled into a `.tpac`, which is [bannerlord-engine-and-toolchain](../reference/bannerlord-engine-and-toolchain.md) section 6, not this file.
3. **Copy a sibling row in the same file** and change it. Do not write a row from scratch: the sibling already carries the file's `Type`, the culture's `modifier_group` and the house `<Flags>`.
4. **Set the four numbers from the curve.** `tools/rebalance_armor.py` holds `SLOT_BASELINES` (line 112) and `CULTURAL_MODS` (line 173); `final = baseline[tier][slot] + culture protection`, and weight is `baseline_weight * culture weight_mult`. The per-culture generators keep their own `STAT_TIERS` copies and those have gone stale before, so treat `rebalance_armor.py` as the source ([tools README](../../tools/README.md) row for `generate_black_numenorean_armor.py`).
5. **Set the cover flags by what your mesh hides, not by which slot it is.** A boot mesh that draws the shin needs `covers_legs="true"`; a sleeve that draws the forearm needs `covers_hands="true"`. Leave `covers_head` off on helmets.
6. **Pick one of the 20 legal `modifier_group` ids.** For armour that is `plate`, `chain`, `leather`, `cloth` or `cloth_unarmoured`. Anything else resolves to null and the item never rolls a quality, with no warning.
7. **Set `material_type` with a capital letter.** `Plate`, `Chainmail`, `Leather`, `Cloth`. A lowercase value throws and stops the whole file loading.
8. **Register the display string.** The `{=key}` in `name=` needs the same treatment as any other player-facing text: see [strings-and-localization](strings-and-localization.md).
9. **Put it on somebody.** An item nothing references exists only in shops. Add the `Item.<id>` reference to a roster in `Main/_Module/ModuleData/troops/`.

Check: `python tools/validate_moduledata.py --code BROKEN_ITEM_REF --code DUPLICATE_ITEM_DEF` then `python tools/validate_mesh_refs.py --scan-bodies`
Takes effect: full game restart
Code: No code changes needed

### Modify

1. **Decide which of the four numbers you are moving** and leave the others alone. `head_armor` on a helmet, `body_armor` (plus `arm_armor` on a sleeved chest) on a chest, `leg_armor` on boots, `arm_armor` on gloves, `body_armor` plus `arm_armor` on a cape.
2. **Read the drift report first.** `python tools/analyze_armor_balance.py --culture gondor --stdout` prints where the culture sits against the curve without writing anything.
3. **Stay inside the baseline band for the tier.** A piece that jumps two tiers beats a legendary roll of the tier below it, which is the defect the two-tier invariant exists to stop.
4. **Move weight with protection.** Protection alone re-tiers the piece and re-prices it; weight is what keeps the culture feeling like itself.
5. **Do not touch `material_type` to change loot rolls.** It drives hit sounds and effects and stays lore-correct; `modifier_group` is the roll lever, and the engine reads the two independently.
6. **Do not add `value=`.** An explicit price bypasses the formula for good, so later stat work stops re-pricing the item. No armour item in the armoury sets it; all 56 uses sit in `LOTRAOM_weapons.xml` (53) and `LOTRAOM_horses.xml` (3), and several of those are the starter weapons that were re-priced by hand.
7. **For a whole culture**, `python tools/rebalance_armor.py --dry-run --cultures dale` and read the diff before anything else. Never `--apply` against `gondor`, `mordor`, `isengard`, `dol_guldur`, `gundabad`, `erebor` or `iron_hills`: those are hand-authored and listed in `PRESERVE_CULTURES` (line 43).

Check: `python tools/analyze_armor_balance.py --culture <c> --stdout` then `python tools/validate_moduledata.py --code BROKEN_ITEM_REF`
Takes effect: next save load. A stat change inside an already-registered item file needs no process restart: `Campaign.cs:1471` calls `LoadXML("Items")` on every campaign load, new game and saved game alike, and the file is re-read from disk each time (`MBObjectManager.cs:1343-1358`). Registering a new file or a new folder is the case that still needs a full game restart.
Code: No code changes needed

### Delete

1. **Sweep the consumers before you delete anything.** `python tools/audit_item_refs.py --show-locations` lists every place the id is named. A 2026-09-01 cleanup skipped this and broke 212 references across 159 consumers, found from blank icons in a screenshot ([RCA](../reviews/rca-armoury-keyforce-cleanup-2026-09-01.md)).
2. **Run both gates, not one.** `validate_mesh_refs.py` cannot see an item that was deleted outright, and `validate_moduledata.py` cannot see an item whose art vanished. An art reorganisation does both at once.
3. **Re-point rather than delete where a replacement exists.** `tools/apply_dead_mesh_item_swaps.py` is the pattern: an explicit `ITEM_SWAPS` mapping rewrites `Item.<old>` references, `MESH_REPOINTS` changes only the `mesh=` and keeps the stats, and `DELETE_ITEMS` removes definitions. Writing the mapping down is what makes the decision reviewable.
4. **Check crafting references too.** An item can be named as `<UsablePiece piece_id=>` or `<Piece id=>` rather than `Item.<id>`, and an audit that matches only the latter calls it an orphan ([armoury-mesh-cleanup](../features/armoury-mesh-cleanup.md)).
5. **Never leave a backup ending in `.xml` in a registered folder.** The engine globs every `.xml` in the folder, so `body_armors.bak.xml` loads as real data and merges back into every id it holds, quietly restoring what you just removed. Use a suffix that replaces the extension, `body_armors.xml.bak-<topic>-<date>`, per [module-backup-sweep](../reference/module-backup-sweep.md).
6. **Regenerate the inventory** so the next person has something to diff against: `python tools/generate_armory_catalogue.py --check`.
7. **Open question:** what a save that already holds the deleted item does on load is not recorded anywhere in TAOM. Treat a delete as safe on a fresh campaign and test an existing save before shipping one.

Check: `python tools/audit_item_refs.py --show-locations` then `python tools/audit_deleted_mesh_impact.py` then `python tools/validate_moduledata.py --code BROKEN_ITEM_REF`
Takes effect: full game restart
Code: No code changes needed

## Gotchas: what fails silently and what crashes

- **A typo in a roster's item id makes a naked troop, not an error.** `GetObject<ItemObject>` returns null, `IsItemFitsToSlot` returns true by design for a null item, and the slot is filled with an empty element. Only an external tool catches it (`Equipment.cs:213-217`, `Equipment.cs:445-451`).
- **A wrong slot does log.** A `HeadArmor` written into the `Body` slot fails the fit test and raises an assert, leaving the slot empty (`Equipment.cs:220`).
- **`material_type="plate"` kills the load.** It is the one enum on `<Armor>` parsed without `ignoreCase`, while `hair_cover_type="all"` beside it is fine (`ArmorComponent.cs:157` against `ArmorComponent.cs:190-193`).
- **A missing `Type=` leaves the item `Invalid` in silence.** The whole type block sits behind a null check, so an armour row with a perfect `<Armor>` block and no `Type` fills no slot (`ItemObject.cs:625`).
- **`has_gender_variations` defaults to `true`.** Omitting it takes the same dead branch as writing `true`, and TAOM ships no female armour art at all, so `false` is the safer omission to correct (`ArmorComponent.cs:160`).
- **The `covers_*` flags do not gate the mesh.** They clear skin-visibility bits only, and the masks of the Head, Body, Leg, Gloves and Cape slots are combined with AND (`MBEquipmentMissionExtensions.cs:7-20`). A genuinely invisible piece is a bad `mesh=` or an unregistered file, never a missing cover flag.
- **An unknown `modifier_group` is silent.** It resolves to null and the item simply never rolls a quality (`ItemComponent.cs:21-25`). The armoury carries 12 such attributes today: `shield_wood` on 10 items, `mail` on 1, and the literal `false` on 1.
- **A second `<Armor>` sibling discards the first**, and any unrecognised element name inside `<ItemComponent>` throws and stops the load (`ItemObject.cs:599`).
- **A `<Food>` component yields an item with no component at all**, which then reports tier 1 and a price of 1 (`ItemObject.cs:591-594`).
- **Price is exponential in tier.** `2.75^tier` means one tier of stats roughly triples the price, and all four armour numbers on a row sum into the tier before the slot factor is applied (`DefaultItemValueModel.cs:9-29`, `:219-252`).
- **A low-stat item can land on an invalid tier.** The displayed tier is `Clamp(Round(Tierf), 0, 6) - 1`, so an item whose computed tier rounds to 0, which every 5-armour starter piece does, produces the enum value -1 (`ItemObject.cs:186`).
- **Vanilla armour is live, and it is overridable without editing the base game.** TAOM troops wear 32 `SandBoxCore` armour pieces. `Main/_Module/ModuleData/` has no items XSLT and does not need one: redefining the same `Item@id` in a module that loads later merges onto vanilla's row instead of replacing or shadowing it (`MBObjectManager.cs:846-874`). Nothing in TAOM does this today, so the seam is read out of the engine rather than out of shipped data: none of the armoury's 3,584 ids collides with one of the 1,268 `SandBoxCore` item ids. <!-- measured: python ElementTree id-set intersection of SandBoxCore/ModuleData/items/*.xml against LOTRLOME_items/**/*.xml 2026-09-05 --> Any claim that the armoury is the only armour tree the game loads is still wrong.
- **A starter kit is chest and legs only.** Six items per culture, not fifteen, and the career wiring clears Head, Cape and Gloves ([starting-equipment-tuning](../features/starting-equipment-tuning.md)).
- **`subtype=` is not read by the engine.** It appears on 2,040 armoury items and no deserializer in the v1.4.8 dump reaches for it. Changing it does nothing and adding it fixes nothing.
- **A roster reference is split on the first dot and only the tail is used** (`Equipment.cs:211`), so `Item.sk_gd_dol_helmet_med_a` and a bare `sk_gd_dol_helmet_med_a` resolve the same. That is why a wrong prefix never announces itself.
- **The running game reads the loose `Assets/` tree, not a cooked pack.** The Armory ships no `AssetPackages/` at all, and where a module ships both, the engine's own load log names `Assets` ([armory-guide](../reference/armory-guide.md) "Two asset trees"). A mesh dropped into `Assets/` is live at the next restart.

### Not answered anywhere in TAOM

These come up on every armour job and no doc in the repo settles them. Say so rather than guessing.

- **Where armour goes for a culture with no folder.** Eight cultures have none. The folder list and `LOTRLOME_Armory/SubModule.xml` are the only evidence; adding a folder also means adding its `<XmlName>` row.
- **What a save holding a deleted item does on load.** Never tested here. The reference side is well covered by [rca-armoury-keyforce-cleanup-2026-09-01](../reviews/rca-armoury-keyforce-cleanup-2026-09-01.md); the save side is not.
- **Whether the ten unused `<Armor>` attributes are unused on purpose.** `body_mesh_type`, `body_deform_type`, `stealth_factor`, `no_slim`, `tail_cover_type`, `reins_mesh` and the three mount bonuses appear zero times in the armoury. `ArmorComponent.cs:146-217` is the only description of what they would do.
- **Which `item_category` a helmet or chest should carry.** Ten armoury items set one and the rest let the engine classify. The id registry is `DefaultItemCategories.cs`; nothing in `docs/` picks a convention.

## Numbers in this chapter

| Number | Command |
|---|---|
| 3,240 `<Item>` and 344 `<CraftedItem>` across 104 XML files in `LOTRLOME_items/`, 0 duplicate ids | Python `ElementTree` walk of `LOTRLOME_items/**/*.xml` counting `Item` / `CraftedItem` tags and `id` values <!-- measured: python ElementTree walk of LOTRLOME_items 2026-09-05 --> |
| 0 of the armoury's 3,584 item ids collide with a `SandBoxCore` item id (1,268 vanilla ids) | Python `ElementTree` id-set intersection of `SandBoxCore/ModuleData/items/*.xml` against `LOTRLOME_items/**/*.xml`, counting `Item` and `CraftedItem` <!-- measured: python ElementTree id-set intersection of SandBoxCore items against LOTRLOME_items 2026-09-05 --> |
| 2,938 items carry an `<Armor>` component | same walk, counting `./ItemComponent/Armor` <!-- measured: python ElementTree walk of LOTRLOME_items 2026-09-05 --> |
| `Type` spread: HeadArmor 1021, BodyArmor 746, Cape 448, HandArmor 350, LegArmor 339, Shield 224, Bow 35, HorseHarness 34, Arrows 28, Horse 10, Crossbow 3, Bolts 2 | same walk, counting the `Type` attribute <!-- measured: python ElementTree walk of LOTRLOME_items 2026-09-05 --> |
| `material_type`: Plate 1871, Chainmail 590, Leather 383, Cloth 94, absent 0 | same walk <!-- measured: python ElementTree walk of LOTRLOME_items 2026-09-05 --> |
| `covers_body` true 744, `covers_legs` 323, `covers_hands` 147, `covers_head` 0 | same walk <!-- measured: python ElementTree walk of LOTRLOME_items 2026-09-05 --> |
| `covers_head="true"` on 0 vanilla armour items | `rg -c 'covers_head="true"' head_armors.xml body_armors.xml leg_armors.xml arm_armors.xml shoulder_armors.xml` in `SandBoxCore/ModuleData/items/` <!-- measured: rg -c covers_head SandBoxCore items 2026-09-05 --> |
| `hair_cover_type`: `all` 891, `type2` 117, `type1` 8, `none` 4 | `rg -o 'hair_cover_type="[^"]*"' --glob '*.xml' . \| sort \| uniq -c` <!-- measured: rg -o hair_cover_type LOTRLOME_items 2026-09-05 --> |
| `modifier_group` illegal values: `shield_wood` 10, `mail` 1, `false` 1 | `rg -o 'modifier_group="[^"]*"'` over `LOTRLOME_items/**/*.xml`, joined against the group ids <!-- measured: rg -o modifier_group LOTRLOME_items 2026-09-05 --> |
| 20 legal `ItemModifierGroup` ids | `rg -o '<ItemModifierGroup\s+id="[^"]+"' Native/ModuleData/item_modifiers_groups.xml` <!-- measured: rg -o ItemModifierGroup id item_modifiers_groups.xml 2026-09-05 --> |
| Legendary armour bonuses: plate 12, chain 9, shield 8, leather 7, cloth 5, cloth_unarmoured 3 | Python scan of `Native/ModuleData/item_modifiers.xml` for `quality="legendary"` rows carrying `armor=` <!-- measured: python scan of item_modifiers.xml 2026-09-05 --> |
| 33 `<XmlName>` rows in the Armory SubModule.xml, 21 of them `id="Items"`, 18 folders plus 3 files | `rg -c 'XmlName' SubModule.xml` and `rg -o 'path="LOTRLOME_items/[^"]*"' SubModule.xml` <!-- measured: rg XmlName LOTRLOME_Armory SubModule.xml 2026-09-05 --> |
| Gondor folder: 346 items (head 116, body 110, shoulder 66, arm 26, leg 22, starter 6) | Python `ElementTree` count per file in `LOTRLOME_items/gondor/` <!-- measured: python ElementTree count of LOTRLOME_items/gondor 2026-09-05 --> |
| 13 `starter_armors.xml` files, 6 items each | `ls */starter_armors.xml \| wc -l` and `rg -c '<Item ' */starter_armors.xml` <!-- measured: ls and rg over LOTRLOME_items starter_armors.xml 2026-09-05 --> |
| 32 vanilla armour pieces worn by TAOM troops, out of 2,557 distinct `Item.` references | Python join of every `id="Item.<x>"` in `Main/_Module/ModuleData/troops/*.xml` against the five `SandBoxCore/ModuleData/items/*_armors.xml` id sets <!-- measured: python join of troops refs against SandBoxCore armour ids 2026-09-05 --> |
| 8 XSLT files in `Main/_Module/ModuleData/`, none for items | `ls Main/_Module/ModuleData/*.xslt` <!-- measured: ls Main/_Module/ModuleData/*.xslt 2026-09-05 --> |
| Attributes at 0 uses in the armoury: `body_mesh_type`, `body_deform_type`, `stealth_factor`, `no_slim`, `tail_cover_type`, `reins_mesh`, `maneuver_bonus`, `speed_bonus`, `charge_bonus`, `tier_override` | `rg -o '\b<attr>="' --glob '*.xml' . \| wc -l` per attribute <!-- measured: rg -o per attribute over LOTRLOME_items 2026-09-05 --> |
| `value=` on 56 items, all in `LOTRAOM_weapons.xml` (53) and `LOTRAOM_horses.xml` (3); `item_category` on 10, `appearance` on 3,235, `family_type` on 38, `mane_cover_type` on 35 | same per-attribute count, plus `rg -c '\bvalue="' --glob '*.xml' .` <!-- measured: rg -o and rg -c per attribute over LOTRLOME_items 2026-09-05 --> |
| Helmet tier 3.056 and price 3,281; at `appearance="1"` price 2,201 | `python -c "t=(1.2*24)*1.2*0.1-0.4; print(round(t,3), int(100*2.75**t*(1+0.2*(3-1))+100*2))"` <!-- measured: python price formula for sk_gd_dol_helmet_med_a 2026-09-05 --> |
| Chest tier 5.300 and price 41,203 | `python -c "t=(43+14)*1.0*0.1-0.4; print(round(t,3), int(120*2.75**t*(1+0.2*(4-1))+100*3))"` <!-- measured: python price formula for sk_gd_ano_inf_chest_heavy_a 2026-09-05 --> |
| `catalogue.tsv`: 4,839 rows, `referenced` Y 4,175 / N 524 / SLIM 140 | Python count of non-comment lines and of the `referenced` column <!-- measured: python count over docs/reference/armory-catalogue/catalogue.tsv 2026-09-05 --> |
| 10 non-`.xml` sidecar files under `LOTRLOME_items/` | Python glob of everything under `LOTRLOME_items/` not ending in `.xml` <!-- measured: python glob of LOTRLOME_items sidecars 2026-09-05 --> |
| 24 cultures in `taom_spcultures.xml`; 8 ids match a folder, 8 are suffixed variants, 8 have no folder; 10 folders have no id there | Python set difference between `<Culture id=>` values and the folder listing of `LOTRLOME_items/` <!-- measured: python diff of taom_spcultures.xml culture ids against LOTRLOME_items folders 2026-09-05 --> |
| 677 armoury items carry `culture="Culture.khuzait"` | `rg -o 'culture="Culture\.([a-z_]+)"' --glob '*.xml' . \| sort \| uniq -c` <!-- measured: rg -o culture over LOTRLOME_items 2026-09-05 --> |
| Head `medium` baseline is 24 armour and 2.5 weight | `sed -n '70,77p' tools/rebalance_armor.py` <!-- measured: sed HEAD_BASELINES in tools/rebalance_armor.py 2026-09-05 --> |
| `subtype=` on 2,040 armoury items, read by nothing | `rg -o 'subtype="' --glob '*.xml' .` over `LOTRLOME_items/`, against the read-name set of `ItemObject.Deserialize` <!-- measured: rg -o subtype over LOTRLOME_items 2026-09-05 --> |

## Read next

- [armory-guide](../reference/armory-guide.md) for the canonical-folder table, the Gondor regional prefixes and the two asset trees.
- [armor-balance](../features/armor-balance.md) for the tier curve, the cultural modifiers and the two-tier invariant.
- [multi-culture-armor-revamp](../features/multi-culture-armor-revamp.md) for how the per-culture generators were built and where they mis-filed items.
- [starting-equipment-tuning](../features/starting-equipment-tuning.md) for the career starter kit and how item value works.
- [armoury-mesh-cleanup](../features/armoury-mesh-cleanup.md) for the orphan-is-not-unreferenced trap and the `_slim` finding.
- [armory-catalogue README](../reference/armory-catalogue/README.md) for what the generated mesh inventory contains and how to diff it.
- [rca-armoury-keyforce-cleanup-2026-09-01](../reviews/rca-armoury-keyforce-cleanup-2026-09-01.md) for why one gate is never enough on a deletion.
- [module-backup-sweep](../reference/module-backup-sweep.md) for the backup-suffix rule that keeps a sidecar out of the loader.
- [bannerlord-engine-and-toolchain](../reference/bannerlord-engine-and-toolchain.md) section 6 for the art half: FBX and textures through to a `.tpac` and the name you type into `mesh=`.
- [author-armor skill](../../.claude/skills/author-armor/SKILL.md) for the repo's own step order, read with the cover-flag correction in this chapter.

---

<!-- backlinks-start auto-generated; edit lint_docs.py / build_backlinks.py to change -->

## Referenced by

- [docs/INDEX.md](../INDEX.md)
- [docs/modding/balance-levers.md](./balance-levers.md)
- [docs/modding/equipment-rosters.md](./equipment-rosters.md)
- [docs/modding/file-catalogue.md](./file-catalogue.md)
- [docs/modding/items-mounts-and-harness.md](./items-mounts-and-harness.md)
- [docs/modding/items-shields.md](./items-shields.md)
- [docs/modding/items-weapons-and-crafting.md](./items-weapons-and-crafting.md)
- [docs/modding/load-order-and-dependencies.md](./load-order-and-dependencies.md)
- [docs/modding/module-armory.md](./module-armory.md)
- [docs/modding/modules-overview.md](./modules-overview.md)
- [docs/modding/README.md](./README.md)
- [docs/modding/recipe-new-mod-from-zero.md](./recipe-new-mod-from-zero.md)
- [docs/modding/recipe-retire-content.md](./recipe-retire-content.md)
- [docs/modding/troops.md](./troops.md)
- [docs/modding/troubleshooting.md](./troubleshooting.md)

<!-- backlinks-end -->
