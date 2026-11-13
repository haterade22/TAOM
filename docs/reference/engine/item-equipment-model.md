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
- **`Equipment`** is the `EquipmentElement[12]` set, with `this[EquipmentIndex]` access, `FillFrom(other)` (a full
  copy: `this[i] = sourceEquipment[i]` for all 12 slots, empty ones included, Equipment.cs:184-194 on v1.5.6), `Clone`, civilian/battle variants.

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
  shadowing) and set **cover attributes** correctly (`covers_legs`/`covers_hands`; [data-content-cultures lessons](../../reviews/lessons/data-content-cultures.md), "LOTRLOME armor needs `covers_*` attributes...").
- **Modifier-preserving overloads** ⭐ — because the modifier is on the `EquipmentElement`: APIs taking
  `(ItemObject, int)` **drop the modifier**; the `(EquipmentElement, int)` overload **preserves** it. Always audit
  both when adding/transferring items ([adapters lessons](../../reviews/lessons/adapters-taleworlds-api.md), "Prefer the `(EquipmentElement, int)` overload..."; the "Sharp horse comes out
  stock" siege-dismount bug).
- **Inventory/equipment mutations** must route through `InventoryLogic.TransferCommand` + `AddTransferCommands`, NOT
  direct `equipment[slot] = element` — direct assignment duplicates inventory items, loses displaced gear, skips slot-fit
  + `AfterTransfer` UI refresh ([adapters lessons](../../reviews/lessons/adapters-taleworlds-api.md), "Mutate hero equipment from inventory only through vanilla `InventoryLogic.TransferCommand`").
- **`FillFrom` replaces all 12 slots**: TAOM's career starting-equipment override `FillFrom`s a roster onto the
  player's battle set (`PlayerEquipmentAdapter`), so a slot the roster leaves empty (Head, Cape, Gloves, and the
  Horse of a non-cavalry archetype) ends up empty, not inherited from the culture default
  ([starting-equipment-tuning.md](../../features/starting-equipment-tuning.md)).
- **Broken `Item.X` ref → `GetObject` null → no mesh = the "underwear bug"** (Phase 5). Run `tools/validate_moduledata.py`
  (BROKEN_ITEM_REF) before committing item/troop XML.
- The mount slot is `Horse`=`ArmorItemEndSlot`=10; `is_mountable="false"` on the `HorseComponent` keeps a creature mount
  from being rideable (TAOM creature-troops).

## Items made or edited at run time, and where values are read

yotthani's balance simulator records traps for code that clones or edits `ItemObject`s after the data load (`docs/balance-sim/app-0.4-checks.md@c45d8374`,
"Findings for Tasks 2-3" 1 to 3 and the hook-point table; read from the 1.5.6 decompile, then the item applier was checked in game on v1.5.6.125270,
`app-0.4-acceptance.md` row 3). TAOM re-read the ones it could on v1.5.7. TAOM has no such code: Grep of `Main/**/*.cs` for `new ItemObject(`,
`MBObjectManager` `.RegisterObject(`, `CreateObject<`, `.GetCopy()` and `SwingDamageFactor` found no run-time copy or registration (the
`RegisterObject` hits are `VisualTrackerManager`). Treat the list as a brief for the first feature that clones items (field ballista, armour acquisition).

- **The copy constructor shares the component.** `public ItemObject(ItemObject itemToCopy) : base(itemToCopy) { ItemComponent =
  itemToCopy.ItemComponent; ...` (`ItemObject.cs:296-299`); a private-setter edit on the copy edits the source too [TAOM-verified, v1.5.7
  ilspy]. `WeaponComponent.GetCopy()` is `new WeaponComponent(base.Item)` (`:32-35`), an empty component; yotthani adds that `ArmorComponent.GetCopy()`
  copies all armour values (not re-checked by TAOM).
- **`RegisterObject` of a taken id renames silently**, see [object-system-mbobjectmanager.md](object-system-mbobjectmanager.md).
- **Equipment sets are shared** between troops and the template roster, so clone an `EquipmentSet` before writing into it (yotthani, read from code; not
  re-checked by TAOM).
- **Swing and thrust damage factors do not come from weight and length on v1.5.7.** yotthani's record says `WeaponComponentData.SetDamageFactors`
  (`Core/WeaponComponentData.cs:472-501` in yotthani's 1.5.6 decompile) derives them from weight and length, bows, crossbows, thrown weapons and ammo
  getting 1, and crafted items taking them from the blade piece. On v1.5.7 `SetDamageFactors(float weight)` computes
  `sqrt(sqrt(weight / (WeaponLength * 0.01)))` into a local, scales it, and never reads it again; the IL sets both factors from the other local
  (`IL_00ce ldloc.2; ldc.r4 0.8; mul; stloc.2`, then `set_SwingDamageFactor` and `set_ThrustDamageFactor`, `IL_00d6-IL_00df`) [TAOM-verified, v1.5.7 ilspy].
  So both factors are Cut 0.4, Pierce 0.32, Blunt 0.8, and 1 for bow, crossbow, throwing axe, throwing knife, javelin, arrow and bolt; weight and length do
  not reach them. v1.5.6 was not checked; yotthani's reading may be right for that build. The crafted-item rule is not re-checked.
- **The probe `magnitude` is `BaseMagnitude` before the swing or thrust factor**; the factor shows only in the probe's `raw` and `applied` columns
  (yotthani's record, open point; not re-checked).
- **Modified values are read through two groups of getters.** Weapon damage: `WeaponComponentDataExtensions.GetModifiedThrustDamage`, `GetModifiedSwingDamage`,
  `GetModifiedMissileDamage(this WeaponComponentData, ItemModifier)` (`:5-30`); `EquipmentElement.GetModified*DamageForUsage` forwards to them (`:272-285`), and
  `MissionWeapon` uses them (`:236-248`; `:352` sets `result.ThrustDamage = weaponComponentData.GetModifiedThrustDamage(ItemModifier)`). Armour:
  `EquipmentElement.GetModifiedHeadArmor`, `Body`, `MountBody`, `Leg`, `Arm` (`:129-215`), summed by `Equipment` (`:279-334`) [TAOM-verified, v1.5.7 ilspy]. A reader of the raw
  `WeaponComponentData.ThrustDamage`, `SwingDamage` or `MissileDamage` sees no modifier delta. yotthani names native AI and ranged code as raw readers and says tooltip
  zero-checks read raw component values (`ItemMenuVM.cs:1172`, `TooltipRefresherCollection.cs:532-551`); TAOM did not read the native side. yotthani's patch of the getters
  (`HoN/SmithingExtended/Integration/MaterialDeltaPatches.cs@c45d8374:43-62`) showed in the tooltip on v1.5.6 ("40 Cut / 29 Pierce", probe of 03.11.), and yotthani lists combat damage as open because a
  JIT-inlined getter would be bypassed at its call site. Patch these getters, not the raw fields, for a modifier effect, and measure one hit in game.
- **Two engine facts yotthani read for hero gear (1.5.5 text, 1.5.6 said to match, partly seen in a v1.5.6 probe; not re-checked by TAOM):** heroes spawn with
  `FixedEquipment(true)`, so battle changes never flow back (write `Hero.BattleEquipment[slot] = new EquipmentElement(item, modifier)` yourself);
  troops re-roll every slot's modifier at each spawn from `ItemModifierGroup.GetRandomItemModifierLootScoreBased()`, and `LootDropScore` on an `ItemModifier`
  drives those rolls (`Equipment.cs:608-609`), so zeroing it changes enemy gear too. Loot has two sources: `MapEvent.LootCasualtyCharacter` (only when the
  winning party leader is `Hero.MainHero`) and `BattleWreckageCampaignBehavior.ProcessCasualtyLootSource`, which keeps only the item and drops the modifier.
  Source: `docs/smithing/2026-11-03-teil2-engine-recherche.md@c45d8374`, sections 2.1 to 2.4.
- **Run-time hook points** (yotthani's record, not re-checked): `OnGameInitializationFinished` runs after items, rosters and characters are registered and after
  `UnregisterNonReadyObjects`, before any mission; the agent copies `GetWeaponData` and `GetWeaponStatsData` to native at spawn, so a change made during a mission
  reaches managed reads but not the native copy.

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
- TAOM gotchas: `docs/reviews/lessons/adapters-taleworlds-api.md`, `docs/reviews/lessons/data-content-cultures.md`; `.claude/rules/moduledata-validation.md`.

---

<!-- backlinks-start auto-generated; edit lint_docs.py / build_backlinks.py to change -->

## Referenced by

- [docs/ai-includes/weapon-creation-workflow.md](../../ai-includes/weapon-creation-workflow.md)
- [docs/features/starting-equipment-tuning.md](../../features/starting-equipment-tuning.md)
- [docs/INDEX.md](../../INDEX.md)

<!-- backlinks-end -->
