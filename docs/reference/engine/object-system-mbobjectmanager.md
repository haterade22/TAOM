# Bannerlord object system — `MBObjectManager` + XML→object pipeline (Phase 5)

> **One process, traced from the decompile** (`TaleWorlds.ObjectSystem/MBObjectManager`, v1.4.5): how
> `monsters.xml`/items/characters/cultures/etc. become runtime objects, and how every feature resolves them by
> `StringId`. This is the **data backbone** the spawn chain (Phase 1, `mount.Item.HorseComponent.Monster`) and
> *every* TAOM feature use, and it's what the moduledata validator guards. Part of the phased engine study.

## WHAT it is

The engine's runtime registry of all "definition" objects (`MBObjectBase` subclasses: `Monster`, `ItemObject`,
`CharacterObject`, `CultureObject`, `Clan`, `Kingdom`, `PartyTemplateObject`, `Settlement`, `PerkObject`, …). Each
type is **registered** (mapping an XML element name + list name + a type id), its XML is **loaded** into objects,
and any object is **resolved** by its `StringId`. `MBObjectManager.Instance` is the singleton.

## HOW it works

### Type registration — `RegisterType<T>(classPrefix, classListPrefix, typeId, autoCreateInstance, isTemporary)` (ObjectSystem.cs:670)
Registers a type with: the **XML element name** (`classPrefix`, e.g. `"Monster"` = `<Monster>` rows), the **list
name** (`classListPrefix`, e.g. `"Monsters"`), and a **type id** (`typeId` — the object-type tag baked into each
object's `MBGUID`, used by save serialization). Confirmed registrations:

| Type | element | list | typeId | registered in |
|---|---|---|---|---|
| `Monster` | `Monster` | `Monsters` | 2 | Core.cs:14286 (+ MountAndBlade.cs:59584) |
| `ItemObject` | `Item` | `Items` | 4 | Core.cs:14288 |
| `CharacterObject` | `NPCCharacter` | `NPCCharacters` | 16 | CampaignSystem |
| `CultureObject` | `Culture` | `SPCultures` | 17 | CampaignSystem |
| `Clan` | `Faction` | `Factions` | 18 | CampaignSystem |
| `Kingdom` | `Kingdom` | `Kingdoms` | 20 | CampaignSystem |
| `PartyTemplateObject` | `PartyTemplate` | `partyTemplates` | 24 | CampaignSystem |
| `Settlement` | `Settlement` | `Settlements` | 25 | CampaignSystem |
| (+ Perk 19, Trait 21, VillageType 22, BuildingType 23, Feat, MobileParty 14, …) | | | | |

`autoCreateInstance`/`isTemporary` mark types whose instances are created at runtime (parties, clans, kingdoms) vs
purely XML-defined (Monster, Item).

### XML → objects — `LoadXML(listName)` (e.g. CampaignSystem.cs:10688+ — `LoadXML("SPCultures"/"Items"/"EquipmentRosters"/"partyTemplates")`)
At game start, the engine merges **every enabled module's** XML registered under a list name and `LoadXML(listName)`
deserializes each row into an object of the registered type (calling its `Deserialize(XmlNode)` — e.g.
`Monster.Deserialize`, Phase 3). **This is where `SubModule.xml` plugs in:**
```
<XmlNode><XmlName id="Monsters" path="Monsters/LOTR/lotr_monster_spider"/></XmlNode>
```
registers a file under the **`Monsters`** list; at load, all modules' `Monsters` XML is merged + `LoadXML("Monsters")`
turns it into `Monster` objects. (TAOM's `taom_npccharacter`/`Items`/`SPCultures`/`partyTemplates` registrations
work the same way.) **Cross-module merging means the load is order-tolerant** — by the time `LoadXML` runs, every
enabled module's rows are present, so an object defined in one module + referenced in another resolves regardless of
declared load order (the basis for the ADOD_Beasts finding that LOTRLOME needn't be a declared dependency).

### Resolution — `GetObject` (ObjectSystem.cs:813/867/981/994)
- `GetObject<T>(string objectName)` (867) — resolve by `StringId`; **returns clean `null` if not found** (no
  coerced fallback). e.g. `MBObjectManager.Instance.GetObject<Monster>("spider")`.
- `GetObject<T>(Func<T,bool> predicate)` (813) — first match by predicate.
- `GetObject(MBGUID)` (981) — resolve by GUID (used in save load).
- `GetObject(string typeName, string objectName)` (994) — resolve by type+name.

### Runtime objects — `RegisterPresumedObject<T>(obj)` (746)
Registers an object created at runtime (not from XML) into the registry so it's resolvable + savable (e.g. a
spawned party/clan). `RegisterObject` variants assign the `StringId`/`MBGUID`.

## WHY it's shaped this way

A single `StringId`-keyed registry lets data reference data by name across files + modules (a troop's
`Equipment id="Item.foo"`, a Monster on an item, a culture's `basic_troop`), and lets the save system reference any
object by its `MBGUID` (typeId + index). The XML-element/list-name registration is what makes `SubModule.xml`
`<XmlNode>` entries declarative — a module just points at a file under a known list id and the engine merges +
deserializes it.

## TAOM relevance + gotchas
- **Every TAOM feature resolves objects here:** `GetObject<Monster>("spider"|"taom_war_elephant")`, items,
  characters, cultures. The `StringId` is the key — the whole `*.xml` cross-reference web (troop→item, item→monster,
  culture→troop) resolves through `MBObjectManager`.
- **`GetObject` is null-on-missing** (unlike a coerced fallback). So a **broken `Item.X` ref → `GetObject` null →
  no mesh** (the "underwear bug" / failed creature spawn). This is exactly the class `tools/validate_moduledata.py`
  catches **statically** (BROKEN_ITEM_REF / BROKEN_TROOP_REF / UNKNOWN_CULTURE) before it becomes a runtime null —
  run it before committing ModuleData. (Sibling: `.claude/rules/moduledata-validation.md`.)
- **Cross-module resolution is order-tolerant** at runtime (everything's merged before `LoadXML`), so TAOM resolving
  LOTRLOME items/monsters works without declaring LOTRLOME as a dependency (ADOD_Beasts comparison finding) — but the
  *static* validator still needs `--game-modules` to see LOTRLOME.
- The `RegisterType` **typeId** (Monster=2, Item=4, NPCCharacter=16…) is the **object-type** tag in `MBGUID` — distinct
  from the `SaveableTypeDefiner` base ids used for non-`MBObjectBase` saveable classes (Phase 6).
- New object types a mod adds need a `RegisterType<T>` (in a `BeforeRegisterTypes`/`OnRegisterTypes` override) + the
  XML registered under the list name; TAOM mostly reuses vanilla types (Monster/Item/Character/Culture), so it
  rarely registers new ones.

## The native boundary
`MBObjectManager` is **managed** (the registry + resolution are C#). The objects it holds describe native resources
(a `Monster`'s skeleton, an `ItemObject`'s mesh) that the native engine loads — but the *registry/resolution* is
managed, which is why `GetObject<T>(stringId)` is a normal, safe call.

## Evidence (file:line, v1.4.5)
- `TaleWorlds.ObjectSystem.cs`:310 (`MBObjectManager`), 670 (`RegisterType<T>`), 746 (`RegisterPresumedObject<T>`), 813/867/981/994 (`GetObject` overloads; 867 null-on-missing).
- `TaleWorlds.Core.cs`:14286 `RegisterType<Monster>("Monster","Monsters",2u)`, 14288 `RegisterType<ItemObject>("Item","Items",4u)`; CampaignSystem registers NPCCharacter@16/Culture@17/Faction@18/Kingdom@20/PartyTemplate@24/Settlement@25 etc.
- `LoadXML` call sites: `TaleWorlds.CampaignSystem.cs`:10688+ (`SPCultures`/`Concepts`/`Items`/`EquipmentRosters`/`partyTemplates`).

---

<!-- backlinks-start auto-generated; edit lint_docs.py / build_backlinks.py to change -->

## Referenced by

- [docs/INDEX.md](../../INDEX.md)
- [docs/modding/id-cheatsheet.md](../../modding/id-cheatsheet.md)
- [docs/modding/load-order-and-dependencies.md](../../modding/load-order-and-dependencies.md)
- [docs/reference/engine/save-system.md](./save-system.md)

<!-- backlinks-end -->
