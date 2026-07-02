# Bannerlord item / equipment model (Phase 10)

> **One process, traced from the decompile** (`TaleWorlds.Core`, v1.4.5): how items are defined (`ItemObject` +
> `ItemComponent`) and equipped (`Equipment` + `EquipmentElement` + `EquipmentIndex`). TAOM authors **thousands** of
> items (LOTRLOME armor/weapons + the spider/elephant mount items + every troop's equipment), and this is the data
> model under phase 1's `EquipItemsFromSpawnEquipment` + phase 3's `HorseComponent.Monster`. Part of the phased study.

## WHAT it is

An **`ItemObject`** is one `<Item>` (a weapon, piece of armor, mount, shield, banner). It carries a **`Type`** and a
type-specific **`ItemComponent`** (the stats). An **`Equipment`** is an agent's loadout — a fixed array of
**`EquipmentElement`** (item + modifier) indexed by **`EquipmentIndex`** (the 12 slots). The spawn chain reads
`Equipment` to build the agent's visuals + weapons + mount.

## HOW it works

### `ItemObject` (ItemObject.cs:13 — `sealed : MBObjectBase`)
The item definition (from `Items` XML, registered `RegisterType<ItemObject>("Item","Items",4u)` — Phase 5). Holds:
`Type` (`ItemTypeEnum`: OneHandedWeapon/TwoHandedWeapon/Bow/Arrows/Shield/HeadArmor/BodyArmor/LegArmor/HandArmor/
Cape/Horse/HorseHarness/Banner/…), the **`ItemComponent`** (the type-specific data), `MultiMesh` (the mesh — e.g.
`mesh="sk_gd_…"`), `ItemCategory`, `Weight`, `Value`, `Culture`, and armor **cover flags** (`covers_head`/`covers_body`/
`covers_legs`/`covers_hands` — which body region the armor hides; getting these wrong = visible-underwear/clipping).

### `ItemComponent` (ItemComponent.cs:7 — `abstract : MBObjectBase`) + subtypes
- **`HorseComponent` (HorseComponent.cs:10 — `: ItemComponent`)** — the **mount** data: **`Monster`** (the creature
  rig! — Phase 3), `BodyLength` (scale: `SetInitialAgentScale(0.01f * BodyLength)`, Phase 1), `ChargeDamage`, `Speed`,
  `Maneuver`, `HitPoints`, `IsMountable`, and `<AdditionalMeshes>`/`<Materials>` (the warg/spider multi-mesh pattern).
  **This is what makes an item a rideable/creature mount** — the spider/elephant mount items are `Type="Horse"` with
  a `<Horse monster="Monster.X" …>`.
- **`WeaponComponentData`** — weapon stats (damage, speed, reach, swing/thrust, ammo, the `Weapons` sub-rows).
- **`ArmorComponent`** — armor values per body part + the cover behavior.

### `Equipment` (Equipment.cs:12 — `class`) + `EquipmentElement` + `EquipmentIndex`
- **`EquipmentIndex`** (EquipmentIndex.cs:3) — the 12 slots: `Weapon0..3` (0-3), `ExtraWeaponSlot` (4); armor
  `Head` (5), `Body` (6), `Leg` (7), `Gloves` (8), `Cape` (9); **`Horse` = `ArmorItemEndSlot` = 10**, `HorseHarness` = 11.
  (So the mount slot *is* `ArmorItemEndSlot` — phase 1's `SpawnMonster` puts the mount item there.)
- **`EquipmentElement` (EquipmentElement.cs:9 — `struct`)** = **`Item` (ItemObject) + `ItemModifier`** (+ `CosmeticItem`,
  `IsQuestItem`). ⭐ **The modifier (Sharp/Reinforced/Tempered/…) lives on the ELEMENT, not the ItemObject.**
  `IsEmpty => Item == null`.
- **`Equipment`** is the `EquipmentElement[12]` set, with `this[EquipmentIndex]` access, `FillFrom(other)` (slot-merge
  — copies non-empty slots from another set), `Clone`, civilian/battle variants.

### How it feeds the spawn chain (Phases 1, 3)
Troop XML `<Equipment><EquipmentRoster><equipment slot="Body" id="Item.X"/>…</EquipmentRoster></Equipment>` →
`Equipment` → at spawn, `EquipItemsFromSpawnEquipment` (Phase 1) equips the weapons + `AddSkinMeshes` (humanoid) /
the mount mesh; the mount is `Equipment[Horse=ArmorItemEndSlot]`, whose `Item.HorseComponent.Monster` drives
`CreateAgent` (Phase 3).

## WHY it's shaped this way

Separating `ItemObject` (the shared definition) from `EquipmentElement` (item + per-instance modifier) lets the same
sword exist once but be "Sharp" on one troop and "Rusty" on another without duplicating the item — the modifier is
instance state on the element. The fixed `EquipmentIndex` slots give the engine a stable layout to render + simulate.
`ItemComponent` polymorphism lets one `ItemObject` type carry weapon/armor/mount stats via the right subtype.

## TAOM relevance + gotchas
- **TAOM authors thousands of items** (LOTRLOME_Armory: `body_armors.xml`/`head_armors.xml`/`leg_armors.xml`/… +
  `LOTRAOM_weapons/shields/horses`). The **mount items** (spider_mount_a, taom_war_elephant) are `Type="Horse"` +
  `<Horse monster="Monster.X">`. Author per the **canonical-folder rule** (item id prefix → one folder, or dup-id
  shadowing) and set **cover attributes** correctly (`covers_legs`/`covers_hands` — `feedback_lotrlome_armor_cover_attributes`).
- **Modifier-preserving overloads** ⭐ — because the modifier is on the `EquipmentElement`: APIs taking
  `(ItemObject, int)` **drop the modifier**; the `(EquipmentElement, int)` overload **preserves** it. Always audit
  both when adding/transferring items (`feedback_adapter_modifier_preserving_overload` — the "Sharp horse comes out
  stock" siege-dismount bug).
- **Inventory/equipment mutations** must route through `InventoryLogic.TransferCommand` + `AddTransferCommands`, NOT
  direct `equipment[slot] = element` — direct assignment duplicates inventory items, loses displaced gear, skips slot-fit
  + `AfterTransfer` UI refresh (`feedback_inventory_mutations_via_vanilla_inventorylogic`).
- **`FillFrom` slot-merge** — TAOM's career starting-equipment override `FillFrom`s a roster onto the culture default;
  **non-cavalry archetypes need explicit empty `Horse`/`HorseHarness` overrides** or they inherit a mount (CLAUDE.md
  CareerSystem note).
- **Broken `Item.X` ref → `GetObject` null → no mesh = the "underwear bug"** (Phase 5). Run `tools/validate_moduledata.py`
  (BROKEN_ITEM_REF) before committing item/troop XML.
- The mount slot is `Horse`=`ArmorItemEndSlot`=10; `is_mountable="false"` on the `HorseComponent` keeps a creature mount
  from being rideable (TAOM creature-troops).

## The native boundary
`ItemObject`/`Equipment`/`EquipmentElement` are **managed** definitions (loaded from XML, resolved via
`MBObjectManager`). The meshes/weapon-physics they reference are native resources the engine loads at equip/preload
(Phase 1). So item *data + loadout* is managed; the *rendered/simulated* item is native.

## Evidence (file:line, v1.4.5)
- `EquipmentIndex.cs`:3-26 (slots; `Horse`=`ArmorItemEndSlot`=10, `HorseHarness`=11, `NumEquipmentSetSlots`=12).
- `EquipmentElement.cs`:9-24 (`struct`; `Item` + `ItemModifier` + `IsQuestItem` + `CosmeticItem`; `IsEmpty`).
- `ItemObject.cs`:13 (`sealed : MBObjectBase`), `ItemComponent.cs`:7 (`abstract : MBObjectBase`), `HorseComponent.cs`:10 (`: ItemComponent`, the mount — `Monster`/`BodyLength`/…).
- `Equipment.cs`:12 (the slot set; armor-slot iteration `NumAllWeaponSlots..ArmorItemEndSlot`).
- Registration: `RegisterType<ItemObject>("Item","Items",4u)` (Core.cs:14288, Phase 5). Spawn use: `EquipItemsFromSpawnEquipment` (Phase 1).
- TAOM gotchas: `feedback_adapter_modifier_preserving_overload`, `feedback_inventory_mutations_via_vanilla_inventorylogic`, `feedback_lotrlome_armor_cover_attributes`; `.claude/rules/moduledata-validation.md`.

---

<!-- backlinks-start auto-generated; edit lint_docs.py / build_backlinks.py to change -->

## Referenced by

- [docs/ai-includes/weapon-creation-workflow.md](../../ai-includes/weapon-creation-workflow.md)
- [docs/features/starting-equipment-tuning.md](../../features/starting-equipment-tuning.md)
- [docs/INDEX.md](../../INDEX.md)

<!-- backlinks-end -->
