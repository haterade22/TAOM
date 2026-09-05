# Shields

## What this file is

Every shield TAOM ships is one `<Item Type="Shield">` row in a single file, `LOTRAOM_shields.xml`, and there are 224 of them. <!-- measured: python -c "import xml.etree.ElementTree as E,collections;s=[i for i in E.parse('LOTRLOME_Armory/ModuleData/LOTRLOME_items/LOTRAOM_shields.xml').getroot().findall('Item') if i.get('Type')=='Shield'];w=lambda i:i.find('ItemComponent/Weapon');print(len(s),collections.Counter(w(i).get('item_usage') for i in s),collections.Counter(w(i).get('weapon_class') for i in s))" run from the game's Modules folder 2026-09-05 --> A shield is not an armour item: it has a `<Weapon>` component, no `<Armor>` component, and the four numbers that decide how good it is (`hit_points`, `body_armor`, `thrust_speed`, `weapon_length`) all live inside that `<Weapon>` node. Three things have to agree on every row, and nothing in the build checks them for you: the grip (`item_usage`), the bone the shield hangs from (the `ForceAttachOffHand*ItemBone` flag), and the two collision bodies (`body_name` and `shield_body_name`).

## Where it lives and how it is registered

`LOTRLOME_Armory/ModuleData/LOTRLOME_items/LOTRAOM_shields.xml`. This file lives in the game install, not the repo; a module reinstall reverts hand edits, so land a repo-side validator gate with any fix.

| | |
|---|---|
| Registration | `<XmlName id="Items" path="LOTRLOME_items/LOTRAOM_shields"/>` at `LOTRLOME_Armory/SubModule.xml:323`, gated to the `Campaign`, `CampaignStoryMode`, `CustomGame` and `EditorGame` game types |
| Path form | a **file**, not a folder: the loader resolves `<module>/ModuleData/<path>.xml` first, so only `LOTRAOM_shields.xml` itself loads and a sibling `.xml` beside it does not join the registry. The folder form (`LOTRLOME_items/gondor` and 17 others: 18 of the Armory's 21 `Items` rows point at a folder) does glob every `.xml` inside, which is why backup naming matters there. See [Editing safely](editing-safely.md) and [SubModule and registration](submodule-and-registration.md) |
| Root element | `<Items>` |
| Per-entry element | `<Item>` |
| Engine class | `TaleWorlds.Core.ItemObject`, deserialized at `ItemObject.cs:420-706`; the `<Weapon>` node is read by `WeaponComponentData.Deserialize` (`WeaponComponentData.cs:346-469`) |
| Vanilla equivalents | `SandBoxCore/ModuleData/items/shields.xml` (75 shields) and `SandBoxCore/ModuleData/items/tournament_weapons.xml` (7). Both load alongside TAOM's <!-- measured: python -c "import xml.etree.ElementTree as E;print([sum(1 for i in E.parse(p).getroot().findall('Item') if i.get('Type')=='Shield') for p in ('SandBoxCore/ModuleData/items/shields.xml','SandBoxCore/ModuleData/items/tournament_weapons.xml')])" run from the game's Modules folder 2026-09-05 --> |

## Attributes

The complete `<Item>` and `<Weapon>` attribute surfaces belong to [Armour items](items-armor.md) and [Weapons and crafting](items-weapons-and-crafting.md). The two tables below are the shield subset: what every one of the 224 rows actually uses.

<!-- engine-ref type="TaleWorlds.Core.ItemObject" file="Core/TaleWorlds.Core/TaleWorlds.Core/ItemObject.cs" lines="492-566, 611-679" -->

| Attribute | Type | Required | Default when absent | What it does | Read at (file:line) |
|---|---|---|---|---|---|
| `id` | string | yes | none, a missing `id` is a NullReferenceException at load | The permanent code-name every roster points at. Never rename one | `MBObjectBase.cs:61` |
| `name` | localized string | yes | none, dereferenced without a null check | The inventory name. Write it as `{=key}English text`; the inline default is the English string | `ItemObject.cs:492` |
| `mesh` | string | in practice yes | null, and the shield renders as nothing | The multi-mesh drawn in the hand | `ItemObject.cs:503` |
| `body_name` | string | in practice yes | null, and the engine substitutes `bo_axe_short` after a failed assert | The `bo_cap_*` capsule used while the shield is held or on the ground | `ItemObject.cs:525` |
| `shield_body_name` | string | in practice yes | null | Stored as `CollisionBodyName`: the full `bo_*` body that arrows and blades collide with, that is, the blocking surface | `ItemObject.cs:529` |
| `recalculate_body` | bool, strict parse | no | `false` | Rebuilds the physics body from the mesh at spawn. Shields must leave this `false`; the engine asserts on a shield that sets it | `ItemObject.cs:530` |
| `Type` | enum | yes | `Invalid`, and the item is unusable | `Shield` here. Because the row has a `<Weapon>` node, `weapon_class` overwrites whatever you write, so the two must agree | `ItemObject.cs:625-638` |
| `culture` | prefixed ref | no | null | `Culture.mordor` form. A bare id with no dot throws; an unknown prefixed id creates a silent placeholder | `ItemObject.cs:540` |
| `weight` | float, kg | no | `1.0`, not zero | Carried weight, and the divisor in the shield tier formula. Heavier means cheaper and lower tier | `ItemObject.cs:546` |
| `appearance` | float | no | `0.5` | A price multiplier only, never a combat stat | `ItemObject.cs:553` |
| `using_tableau` | bool | no | `false` | Paints the owner's heraldry onto the shield face. 8 of TAOM's 224 set it | `ItemObject.cs:560` |
| `is_merchandise` | string compare | no | absent means the shield IS merchandise | `"false"` keeps it out of shops and loot. Any value other than the literal `true` also counts as not-merchandise | `ItemObject.cs:498` |
| `item_holsters`, `has_lower_holster_priority`, `holster_position_shift` | colon list, bool, `"x,y,z"` | no | null, `false`, zero | Where the shield sits when sheathed. The list holds holster set ids from `Native/ModuleData/item_holsters.xml` (`shield`, `shield_2`, `shield_3`, `shield_4`, `shield_kite`, `shield_oval`, `shield_round`), first one preferred; the priority bool is ignored without it; the shift nudges the shield on the back and uses strict `Vec3.Parse`, so garbage throws | `ItemObject.cs:512-524` |
| `tier_override`, `value` | float, int | no | absent means the formulas run | Two escape hatches: `tier_override` forces the tier (and therefore the price and the AI's opinion of the shield), `value` forces the gold price outright | `ItemObject.cs:666-675` |

The `<Weapon>` node is where a shield's stats live. `speed_rating` and `thrust_speed` are two different numbers with two different jobs, and TAOM sets them equal on most rows, which hides the difference.

<!-- engine-ref type="TaleWorlds.Core.WeaponComponentData" file="Core/TaleWorlds.Core/TaleWorlds.Core/WeaponComponentData.cs" lines="346-450" -->

| Attribute | Type | Required | Default when absent | What it does | Read at (file:line) |
|---|---|---|---|---|---|
| `weapon_class` | enum, case sensitive | yes | `Undefined` | `LargeShield` on all 224 TAOM shields and on all 82 vanilla ones, so `SmallShield` is unused in practice. This attribute, not `Type`, decides the item is a shield | `WeaponComponentData.cs:364` |
| `item_usage` | string | yes in practice | null | Picks the wielder's grip, stance and block animations: `shield` (forearm strapped) or `hand_shield` (centre grip), both defined in `Native/ModuleData/item_usage_sets.xml`. The engine keeps it as a raw string and resolves it natively, so a misspelled name fails in the animation layer rather than as a managed error ([armory-shield-audit.md](../reference/armory-shield-audit.md)) | `WeaponComponentData.cs:352` |
| `hit_points` | short | yes in practice | `0` | Shield health. Shares one field with `ammo_limit` and `stack_amount`, in that precedence order, so never write two of the three | `WeaponComponentData.cs:374-392` |
| `body_armor` | int | no | `0` | The shield's armour rating. Feeds the tier and effectiveness formulas and takes the item-modifier armour bonus. It does **not** add to the wearer's body armour: `GetHumanBodyArmorSum` starts its loop past the weapon slots | `WeaponComponentData.cs:348`, `Equipment.cs:285-296` |
| `thrust_speed` | int | no | `0` | The speed number the tier and effectiveness formulas read | `WeaponComponentData.cs:355` |
| `speed_rating` | int | no | `0` | Swing speed. This is the "Speed" the inventory panel prints for a shield, and no shield formula reads it | `WeaponComponentData.cs:354`, `ItemMenuVM.cs:897-901` |
| `weapon_length` | int, cm | no | `0` | Shield size. Worth 20 points per centimetre in the effectiveness score, so it is the quiet driver of which shield the AI prefers | `WeaponComponentData.cs:357` |
| `position` | `"x,y,z"` | no | identity | Where the mesh sits on the bone. Split on commas with `TryParse`, so a malformed component silently becomes 0 | `WeaponComponentData.cs:423-433` |
| `rotation` | `"x,y,z"` degrees | no | identity | The shield's pose on the bone. It is grip specific: the same numbers on the other bone give a different world pose | `WeaponComponentData.cs:434-448` |
| `center_of_mass` | `"x,y,z"` | no | zero | Strict `Vec3.Parse`, throws on garbage | `WeaponComponentData.cs:368` |
| `physics_material` | string | no | null | Names a row in `Native/ModuleData/physics_materials.xml`, for example `wood_shield`. It drives the impact sound and feel | `WeaponComponentData.cs:349` |
| `thrust_damage_type` | enum, case sensitive | no | `Blunt` | `Blunt` on shields. Lowercase throws and kills the load | `WeaponComponentData.cs:362` |
| `modifier_group` | bare id | no | null | The quality pool the shield rolls from, `shield` here. Bare id, no prefix, and an unknown one silently resolves to null | `ItemComponent.cs:21` |

## Child elements

<!-- engine-ref type="TaleWorlds.Core.ItemObject" file="Core/TaleWorlds.Core/TaleWorlds.Core/ItemObject.cs" lines="570-624" -->

| Element | Where it sits | Repeatable | What it does | Read at (file:line) |
|---|---|---|---|---|
| `<ItemComponent>` | child of `<Item>` | one | Wrapper. A child element name outside `Armor`, `Weapon`, `Horse`, `Trade`, `Food`, `Banner` throws `Wrong ItemComponent type.` and the load dies | `ItemObject.cs:570-599` |
| `<Weapon>` | child of `<ItemComponent>` | yes, appends | Each node adds one usage mode. A shield needs exactly one; the first node is the primary and alone decides the tier, the price and the effectiveness | `ItemObject.cs:582-583` |
| `<WeaponFlags>` | child of `<Weapon>` | one meaningful | Turns on weapon flags by attribute name | `WeaponComponentData.cs:451-464` |
| `<Flags>` | sibling of `<ItemComponent>`, child of `<Item>` | one meaningful | Turns on item flags by attribute name | `ItemObject.cs:611-622` |

The flags a shield row carries, and why:

<!-- engine-ref type="TaleWorlds.Core.ItemFlags" file="Core/TaleWorlds.Core/TaleWorlds.Core/ItemFlags.cs" lines="8-20" -->

| Flag | Element | What it does |
|---|---|---|
| `HasHitPoints` | `<WeaponFlags>` | Half of the engine's definition of a shield: `IsShield` is true only when the flags carry `HasHitPoints` plus `CanBlockRanged` and no weapon-mask bit (`WeaponComponentData.cs:111-121`) |
| `CanBlockRanged` | `<WeaponFlags>` | The other half. Drop either one and the item stops being a shield to the engine even with `Type="Shield"` |
| `ForceAttachOffHandPrimaryItemBone` | `<Flags>` | Hangs the shield on the wearer's `off_hand_item_bone`. Every one of the 14 rows in `LOTRLOME_Armory/ModuleData/monsters.xml` that names that bone sets it to `l_finger0`, the fist, so this is the centre-grip bone and it pairs with `item_usage="hand_shield"` (`Monster.cs:690-698`) <!-- measured: python -c "import re,collections;t=open('LOTRLOME_Armory/ModuleData/monsters.xml',encoding='utf-8',errors='replace').read();print(collections.Counter(re.findall(r'off_hand_item_bone=\"([^\"]+)\"',t)),collections.Counter(re.findall(r'off_hand_item_secondary_bone=\"([^\"]+)\"',t)))" run from the game's Modules folder 2026-09-05 --> |
| `ForceAttachOffHandSecondaryItemBone` | `<Flags>` | Hangs it on `off_hand_item_secondary_bone`, `l_foretwist1` on all 14, the forearm. This is the strapped bone and it pairs with `item_usage="shield"` |
| `HeldInOffHand` | `<Flags>` | Marks the item as an off-hand item |
| `WoodenParry` | `<Flags>` | Wooden block sounds |

## Worked example

<!-- example file="LOTRLOME_Armory/ModuleData/LOTRLOME_items/LOTRAOM_shields.xml" id="sm_mordor_shield_mid_a" -->

```xml
    <Item
        id="sm_mordor_shield_mid_a"
        name="{=aom_sm_mordor_shield_mid_a_name}[Mordor] Medium Shield I"
        body_name="bo_cap_mordor_shield_mid_a"
        shield_body_name="bo_mordor_shield_mid_a"
        recalculate_body="false"
        mesh="sm_mordor_shield_mid_a"
        culture="Culture.mordor"
        using_tableau="false"
        is_merchandise="true"
        weight="5.0"
        appearance="0.7"
        Type="Shield"
        item_holsters="shield:shield_2:shield_3:shield_4"
        has_lower_holster_priority="true"
        holster_position_shift="-0.1,-0.1,0">
        <ItemComponent>
            <Weapon
                weapon_class="LargeShield"
                body_armor="1"
                thrust_speed="89"
                thrust_damage_type="Blunt"
                speed_rating="89"
                physics_material="wood_shield"
                item_usage="shield"
                position="0.18, 0.14, -0.02"
                rotation="0.0,10.0,40.00"
                weapon_length="133"
                center_of_mass="-0.0,0.2,0.05"
                hit_points="450"
                modifier_group="shield">
                <WeaponFlags
                    CanBlockRanged="true"
                    HasHitPoints="true" />
            </Weapon>
        </ItemComponent>
        <Flags
            WoodenParry="true"
            HeldInOffHand="true"
            ForceAttachOffHandSecondaryItemBone="true" />
    </Item>
```

The three you reach for first:

1. **`hit_points="450"`.** The strongest lever in the file, and it is not linear. The tier formula raises hit points to the power 1.22, so 450 to 550 on this row moves it from Tierf 4.07 to Tierf 5.67, which is Tier4 to Tier6, and the shop price from 5,796 to 29,237 gold. <!-- measured: python -c "t=lambda hp,ba,ts,w:(hp**1.22+3*ba+ts)/(6+w**1.11)*0.04-2;p=lambda x,a:int(100*2.75**max(-1,min(7.5,x))*(1+0.2*(a-1))+100*max(0.0,a-1));[print(n,round(v,2),'Tier'+str(max(0,min(6,round(v)))),p(v,0.7)) for n,v in (('as shipped',t(450,1,89,5.0)),('hit_points 550',t(550,1,89,5.0)),('body_armor 6',t(450,6,89,5.0)),('weight 7.0',t(450,1,89,7.0)))]" 2026-09-05 --> Change it in steps of tens, not hundreds.
2. **`item_usage="shield"` with `ForceAttachOffHandSecondaryItemBone="true"`.** These two are one decision, not two attributes. Changing one without the other puts the shield on the wrong bone or plays the wrong block animation.
3. **`weight="5.0"`.** It divides the tier, so it is the brake on the hit-points lever: leaving hit points alone and taking this row from 5.0 kg to 7.0 kg drops it to Tierf 2.96 and 1,868 gold.

`body_armor="1"` looks like a fourth lever and mostly is not: raising it from 1 to 6 on this row moves the price by about 300 gold, because the tier formula weights it at 3 points against hit points raised to the power 1.22.

## Recipes: Add / Modify / Delete

### Add

1. Open `LOTRLOME_Armory/ModuleData/LOTRLOME_items/LOTRAOM_shields.xml` and back it up first, following the naming rule in [Editing safely](editing-safely.md).
2. Copy the nearest existing shield of the same shape and grip. Do not write a row from scratch; copying keeps the flags and the holster list correct by construction.
3. Change `id`, `name` and `mesh`. The `id` must be unique across every loaded module, and the id you pick is what rosters will reference forever.
4. Set `body_name` to the `bo_cap_*` capsule and `shield_body_name` to the matching `bo_*` body for your mesh. On 223 of the 224 shipped rows these are the same stem with and without the `bo_cap_` prefix; the one exception is deliberate and is listed under Gotchas.
5. Decide the grip and set all three of `item_usage`, the offhand bone flag and `rotation` together. Centre grip is `item_usage="hand_shield"` plus `ForceAttachOffHandPrimaryItemBone="true"`; strapped is `item_usage="shield"` plus `ForceAttachOffHandSecondaryItemBone="true"`. Never both flags, never neither.
6. Set the stats. TAOM's shipped ranges are `hit_points` 250 to 1000, `body_armor` 1 to 15, `thrust_speed` 65 to 89, `weapon_length` 61 to 285 and `weight` 2.0 to 9.5; stay inside them unless you mean to outclass everything. <!-- measured: python -c "import xml.etree.ElementTree as E;s=[i for i in E.parse('LOTRLOME_Armory/ModuleData/LOTRLOME_items/LOTRAOM_shields.xml').getroot().findall('Item') if i.get('Type')=='Shield'];g=lambda a:[int(i.find('ItemComponent/Weapon').get(a)) for i in s];print('hit_points',min(g('hit_points')),max(g('hit_points')),'body_armor',min(g('body_armor')),max(g('body_armor')),'thrust_speed',min(g('thrust_speed')),max(g('thrust_speed')),'weapon_length',min(g('weapon_length')),max(g('weapon_length')),'weight',min(float(i.get('weight')) for i in s),max(float(i.get('weight')) for i in s))" run from the game's Modules folder 2026-09-05 -->
7. Register the `{=key}` in the localization pipeline so the name is translatable. See [Strings and localization](strings-and-localization.md).
8. Put the shield on somebody, or nothing will ever spawn holding it. Rosters live in [Equipment rosters](equipment-rosters.md); a shield fits any of `Item0` through `Item3`, which the engine maps to `Weapon0` through `Weapon3` (`Equipment.cs:225-232`, `Equipment.cs:462-478`).

Check: `python tools/validate_mesh_refs.py --scan-bodies --code MISSING_BODY`
Takes effect: full game restart
Code: No code changes needed

### Modify

Changing the grip is the common case, and it is a change to three attributes at once.

1. Flip `item_usage` between `shield` and `hand_shield`.
2. Delete the offhand bone flag that no longer applies and add the other one. Deleting matters: inside `<Flags>` a flag is off only when the attribute is absent or its value is literally `false`, and setting both flags at once falls through the `AttachmentMask` switch to the main hand, so the shield ends up in the sword hand (`Monster.cs:690-698`).
3. Change `rotation` to a value used by shields that already have the target grip. The same rotation on a different bone is a different world pose, which is why 12 Rhûn and Dol Guldur tower shields still look wrong: they changed usage and flags and kept the old rotation ([armory-shield-audit.md](../reference/armory-shield-audit.md)).
4. Re-derive the invariant across the whole file, because nothing in the build does it for you.

Check: `python -c "import xml.etree.ElementTree as E;s=[i for i in E.parse('LOTRLOME_Armory/ModuleData/LOTRLOME_items/LOTRAOM_shields.xml').getroot().findall('Item') if i.get('Type')=='Shield'];f=lambda i,a:(i.find('Flags') is not None and i.find('Flags').get(a) not in (None,'false'));print([i.get('id') for i in s if not((i.find('ItemComponent/Weapon').get('item_usage')=='hand_shield' and f(i,'ForceAttachOffHandPrimaryItemBone') and not f(i,'ForceAttachOffHandSecondaryItemBone')) or (i.find('ItemComponent/Weapon').get('item_usage')=='shield' and f(i,'ForceAttachOffHandSecondaryItemBone') and not f(i,'ForceAttachOffHandPrimaryItemBone')))])"` run from the game's `Modules` folder, expecting `[]`
Takes effect: full game restart
Code: No code changes needed

### Delete

Deletion is the operation TAOM has already got badly wrong once: an Armory cleanup broke 212 item references across 159 consumers and was caught from a screenshot, not from a gate ([rca-armoury-keyforce-cleanup-2026-09-01.md](../reviews/rca-armoury-keyforce-cleanup-2026-09-01.md)). Find the consumers before you delete, not after.

1. List every reference to the id first: `python tools/audit_item_refs.py --show-locations`, or `rg -n 'Item\.<the id>' Main/_Module/ModuleData` for one id.
2. Edit or remove those roster entries. A roster line pointing at a deleted id does not error: `GetObject<ItemObject>` returns null, `IsItemFitsToSlot` returns true for a null item by design, and the slot is filled with an empty `EquipmentElement` (`Equipment.cs:204-222`, `Equipment.cs:445-451`). The troop just spawns without a shield.
3. Delete the `<Item>` block from `LOTRAOM_shields.xml`.
4. Remove the `{=key}` string from the localization files if nothing else uses it.
5. Leave the mesh and the collision bodies alone unless you have checked that no other row references them.

Check: `python tools/validate_moduledata.py --code BROKEN_ITEM_REF`
Takes effect: full game restart
Code: No code changes needed

## Gotchas: what fails silently and what crashes

- **The grip invariant has no automated gate.** No tool in `tools/` and no test in `TAOM.Tests/` reads `ForceAttachOffHand*` or `item_usage`; the only file that mentions them is a generator, `tools/generate_black_numenorean_weapons.py`. Run the parse in the Modify recipe by hand. Source: `docs/reference/armory-shield-audit.md` "Reproducing this audit", re-checked 2026-09-05.
- **Two `body_name` values in the file look like typos and must not be corrected.** `wm_isengard_shield_a04` carries `body_name="bo_capwm_isengard_shield_a02_clean"`, missing an underscore, and the asset really is packaged under that misspelling; the corrected spelling does not exist. Fixing it turns a resolving reference into a missing collision body. The second entry, `gond_shld4`, no longer exists: its definition was deleted and only stale localization rows survive. Source: [armory-shield-audit.md](../reference/armory-shield-audit.md).
- **`validate_all_troop_refs.py` does not cover this file the way the older docs say.** Its `ARMOR_PREFIX_RE` matches `sm_*` as well as `sk_*` and `ar_*`, so 80 of the 224 shield ids fall inside it and 144 (mostly the `wm_*` and `dunland*` families) do not. It also walks a hardcoded list of ten cultures and skips six troop files entirely, by its own in-file comment. Use `audit_item_refs.py` or `validate_moduledata.py --code BROKEN_ITEM_REF` when you need every reference. <!-- measured: python -c "import re,xml.etree.ElementTree as E;R=re.compile(r'^(sk_[a-z]+_|sm_[a-z]+_|ar_[a-z]+_|harad|clo_urukscout_|urukscout_)');ids=[i.get('id') for i in E.parse('LOTRLOME_Armory/ModuleData/LOTRLOME_items/LOTRAOM_shields.xml').getroot().findall('Item') if i.get('Type')=='Shield'];m=[i for i in ids if R.match(i)];print(len(ids),len(m),len(ids)-len(m))" run from the game's Modules folder 2026-09-05 -->
- **A `hand_shield` blocks a narrower cone than a `shield`.** The cross-body arc parameters are 0.8 on foot and 0.6 mounted for `shield`, and 0.55 for both for `hand_shield` (`Native/ModuleData/native_parameters.xml`, ids `left_horizontal_arc_limit_when_defending_right_side_with_shield*` and `..._with_hand_shield*`). Reclassifying a shield to centre grip makes it cover less, with no other stat changing.
- **Neither collision body fails loudly.** A missing `body_name` logs an assert and substitutes `bo_axe_short`, so the shield gets an axe-shaped collision body (`Mission.cs:3371-3378`); `shield_body_name` gets no fallback at all and goes straight to `PhysicsShape.GetFromResource` (`MissionWeapon.cs:305`). And `recalculate_body="true"` on a shield trips its own assert, `Shields should not have recalculate body flag.` (`Mission.cs:3509`), so leave it `false`.
- **Dropping `HasHitPoints` or `CanBlockRanged` stops the item being a shield** even though `Type="Shield"` is still there, because `IsShield` reads the flags, not the type (`WeaponComponentData.cs:111-121`). And inside `<WeaponFlags>` only the attribute's presence is tested, never its value, so `CanBlockRanged="false"` turns the flag ON (`WeaponComponentData.cs:459`). To remove a weapon flag you delete the attribute. `<Flags>` is the opposite: it does honour `="false"` (`ItemObject.cs:618`).
- **Never write `hit_points` next to `ammo_limit` or `stack_amount`.** All three assign the same field with `ammo_limit` first, `stack_amount` second and `hit_points` last, so a stray `stack_amount` silently becomes the shield's health (`WeaponComponentData.cs:374-392`).
- **The number the player sees is not the number the game balances on.** The inventory panel prints swing speed (`speed_rating`) and hit points for a shield (`ItemMenuVM.cs:897-901`), while the tier and the AI's gear ranking read `thrust_speed` (`DefaultItemValueModel.cs:157-161`, `ItemObject.cs:938-941`). Set both, and set them to the same value unless you mean the display to lie.
- **Item modifiers move a shield further than most authors expect.** `legendary_shield` adds 210 hit points and 8 armour at a 1.8 price factor, and `cracked_shield` subtracts 110 hit points and 4 armour (`Native/ModuleData/item_modifiers.xml`, group `ItemModifierGroup.shield`). Balance the base row knowing the rolled versions sit that far either side of it.
- **A shield's `body_armor` is not the wearer's armour.** `GetHumanBodyArmorSum` iterates from the first armour slot onward, so the four weapon slots, where the shield sits, are excluded (`Equipment.cs:285-296`).

## Numbers in this chapter

All commands were run on 2026-09-05. The ones that name `LOTRLOME_Armory/...` or `SandBoxCore/...` were run from the game install's `Modules` folder; the rest from the repo root.

| Number | Command |
|---|---|
| 224 shields, 115 `shield`, 109 `hand_shield`, all 224 `weapon_class="LargeShield"` | `python -c "import xml.etree.ElementTree as E,collections;s=[i for i in E.parse('LOTRLOME_Armory/ModuleData/LOTRLOME_items/LOTRAOM_shields.xml').getroot().findall('Item') if i.get('Type')=='Shield'];w=lambda i:i.find('ItemComponent/Weapon');print(len(s),collections.Counter(w(i).get('item_usage') for i in s),collections.Counter(w(i).get('weapon_class') for i in s))"` |
| 0 grip-invariant violations, 1 `body_name` stem mismatch (`wm_isengard_shield_a04`) | the parse quoted in the Modify recipe, plus `print([i.get('id') for i in s if i.get('body_name')!='bo_cap_'+(i.get('shield_body_name') or '   ')[3:]])` |
| Holster split: `shield`+`shield` 103, `hand_shield`+`shield_round` 55, `hand_shield`+`shield_kite` 54, `shield`+`shield_kite` 12 | `python -c "import xml.etree.ElementTree as E,collections;s=[i for i in E.parse('LOTRLOME_Armory/ModuleData/LOTRLOME_items/LOTRAOM_shields.xml').getroot().findall('Item') if i.get('Type')=='Shield'];print(collections.Counter((i.find('ItemComponent/Weapon').get('item_usage'),(i.get('item_holsters') or '').split(':')[0]) for i in s))"` |
| Stat ranges: `hit_points` 250 to 1000, `body_armor` 1 to 15, `thrust_speed` 65 to 89, `weapon_length` 61 to 285, `weight` 2.0 to 9.5 | quoted in the Add recipe, step 6 |
| 8 of 224 set `using_tableau="true"` | `python -c "import xml.etree.ElementTree as E,collections;print(collections.Counter(i.get('using_tableau') for i in E.parse('LOTRLOME_Armory/ModuleData/LOTRLOME_items/LOTRAOM_shields.xml').getroot().findall('Item') if i.get('Type')=='Shield'))"` |
| 80 of 224 shield ids match `ARMOR_PREFIX_RE`, 144 do not | quoted in the Gotchas |
| 2,304 references to 169 distinct shield ids across 58 files under `Main/_Module/ModuleData` | `rg -o --no-filename 'id="Item\.[^"]*shield[^"]*"' Main/_Module/ModuleData -g '*.xml' \| wc -l`, then `\| sort -u \| wc -l`, then `rg -l ... \| wc -l` |
| Tier and price walk for `sm_mordor_shield_mid_a` (Tierf 4.07 / Tier4 / 5,796 gold, and the three what-ifs) | quoted in the Worked example callout 1 |
| 21 `<XmlName id="Items">` rows in the Armory manifest, 18 of them folders, one of them `LOTRAOM_shields` | `rg -c 'XmlName id="Items"' LOTRLOME_Armory/SubModule.xml`, `rg -n 'LOTRAOM_shields' LOTRLOME_Armory/SubModule.xml`, and `python -c "import os,xml.etree.ElementTree as E;m='LOTRLOME_Armory/ModuleData/';ps=[n.find('XmlName').get('path') for n in E.parse('LOTRLOME_Armory/SubModule.xml').findall('Xmls/XmlNode') if n.find('XmlName').get('id')=='Items'];print(len(ps), sum((not os.path.isfile(m+p+'.xml')) and os.path.isdir(m+p) for p in ps))"` |
| 14 monster rows in the Armory name an offhand bone, all `l_finger0` and `l_foretwist1` | quoted in the flags table |
| Vanilla: 75 shields in `shields.xml`, 7 in `tournament_weapons.xml`, all 82 `weapon_class="LargeShield"` | the count command is quoted in the registration table; the class check is `python -c "import xml.etree.ElementTree as E,collections;c=collections.Counter();[c.update(i.find('ItemComponent/Weapon').get('weapon_class') for i in E.parse(p).getroot().findall('Item') if i.get('Type')=='Shield') for p in ('SandBoxCore/ModuleData/items/shields.xml','SandBoxCore/ModuleData/items/tournament_weapons.xml')];print(c)"` |

## Read next

- [armory-shield-audit.md](../reference/armory-shield-audit.md), the grip audit this chapter distils, including the do-not-fix entries and the 12 tower shields still carrying the wrong rotation.
- [armory-guide.md](../reference/armory-guide.md), the Armory's canonical-folder rule and the harness `family_type` rule.
- [mesh-ref-validation.md](../features/mesh-ref-validation.md), what `validate_mesh_refs.py` covers and what it does not.
- [moduledata-validation.md](../features/moduledata-validation.md), the schema and cross-reference walk behind `validate_moduledata.py`.
- [rca-armoury-keyforce-cleanup-2026-09-01.md](../reviews/rca-armoury-keyforce-cleanup-2026-09-01.md), the deletion that broke 212 references and why no gate fired.
- [tools/README.md](../../tools/README.md), the full validator and audit catalogue.
