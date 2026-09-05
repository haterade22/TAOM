# Load order and dependencies

## What this file is

This chapter is the spine every other chapter of the handbook points at: it explains what has to exist, and in which order, before a thing you add to TAOM shows up in the game. It covers how a file gets registered, the order the engine reads the registered ids in (v1.4.8), the two ways one entry can point at another and what happens when the target is missing, the existence ladder per content kind, how three modules' XML merge into one document, and which edits need a restart, a new campaign, or reach an existing save. Every claim cites the engine line or the TAOM doc it came from; the numbers are measured against the repo and the live game install on 2026-09-05.

The first live-module path below is `TAOM_Map/ModuleData/settlements.xml`. This file lives in the game install, not the repo; a module reinstall reverts hand edits, so land a repo-side validator gate with any fix.

## A. Registration before existence

Nothing loads because it exists on disk. A data file loads because a manifest names it, and there are two manifests with two different vocabularies. The full table of registrations lives in [submodule-and-registration](submodule-and-registration.md); this section only states the rules that decide whether a new file exists at all.

- **A module is a folder under `Modules/` that contains a `SubModule.xml`.** `ModuleHelper.GetPhysicalModules` lists the directories and keeps each one whose `SubModule.xml` exists (`ModuleHelper.cs:319-334`). No DLL is needed: `TAOM_Map` and `LOTRLOME_Armory` both declare `<SubModules/>` empty and still contribute data (`TAOM_Map/SubModule.xml:21`, `LOTRLOME_Armory/SubModule.xml:20`). The install has 19 such folders today. <!-- measured: ls "<game>/Modules" | wc -l 2026-09-05 -->
- **The data registry is built once, at process start, before any DLL loads.** `Module.Initialize` (`Module.cs:246`) calls `ModuleHelper.InitializeModules` with the launcher's id array (`Module.cs:261`), then `LoadSubModules` (`Module.cs:267`), which for every module calls `XmlResource.GetMbprojxmls` and then `XmlResource.GetXmlListAndApply` (`Module.cs:1031-1032`). `GetXmlListAndApply` re-opens `SubModule.xml`, walks `Module/Xmls/XmlNode`, and records one entry per node: `XmlName@id`, `XmlName@path`, the module, and every `value` under `IncludedGameTypes` (`XmlResource.cs:142-181`). Because this runs once, **a brand-new XML file or a new `<XmlNode>` needs a full game restart**; a save load or a new campaign never re-reads the manifest.
- **`path` is extension-less and relative to `ModuleData/`.** `ModuleHelper.GetXmlPath` returns `<module>/ModuleData/<path>.xml` (`ModuleHelper.cs:232-235`). Subfolders go inside `path` with forward slashes: `<XmlName id="Factions" path="characters/clans"/>` at `Main/_Module/SubModule.xml:139` resolves to `Main/_Module/ModuleData/characters/clans.xml`.
- **If `<path>.xml` is absent but a folder of that name exists, every `*.xml` in that folder loads, non-recursively.** `GetMergedXmlForManaged` strips `.xml`, tests `Directory.Exists`, and adds each `GetFiles("*.xml")` hit as its own merge entry (`MBObjectManager.cs:900-910`). This is how `SandBoxCore/SubModule.xml:15` (`<XmlName id="Items" path="items"/>`) loads the 10 files in `SandBoxCore/ModuleData/items/`, and how each `LOTRLOME_Armory/SubModule.xml` row such as line 83 (`path="LOTRLOME_items/erebor"`) loads a whole culture folder. <!-- measured: ls "<game>/Modules/SandBoxCore/ModuleData/items" | wc -l 2026-09-05 --> Two consequences: a new file dropped into a registered folder loads with no manifest edit, and a backup that keeps the `.xml` extension loads too and injects duplicate ids. TAOM's Armory backups use `.bak-*` extensions for exactly that reason ([lotrlome-soln-id-fix](../reference/lotrlome-soln-id-fix.md), lines 117-118).
- **If neither the file nor the folder exists, the entry is still real.** The engine adds an empty slot and still looks for a stylesheet (`MBObjectManager.cs:911-915`), trying `<path>.xsl` and then `<path>.xslt` (`MBObjectManager.cs:949-964`). That is how TAOM's 8 stylesheet-only registrations work: `spcultures`, `spkingdoms`, `spclans`, `lords`, `heroes`, `module_strings`, `action_strings` and `comment_strings` exist under `Main/_Module/ModuleData/` only as `.xslt` files. <!-- measured: ls Main/_Module/ModuleData/*.xslt 2026-09-05 --> The flip side: a typo in `path=` produces the same silent empty slot as a deliberate stylesheet-only row. Nothing logs it.
- **`project.mbproj` is a separate registry for native-side data** (skins, action sets, monster usage, sounds). `XmlResource.GetMbprojxmls` reads `<base>` and then `SelectNodes("file")`, so only `<file id= name=>` rows count (`XmlResource.cs:107-139`). `name` is the opposite convention from `path`: a full module-relative path with its extension, such as `ModuleData/skins.xml`. Those rows are consumed only when the engine asks for an id by exact string match (`MBObjectManager.cs:930-933`), and the engine only ever asks for the vanilla `soln_*` vocabulary, so an invented id is inert and looks exactly like registration ([lotrlome-soln-id-fix](../reference/lotrlome-soln-id-fix.md), lines 12-45). Two measured facts: TAOM's own `Main/_Module/ModuleData/project.mbproj` has 5 `<file>` rows, and `TAOM_Map/ModuleData/project.mbproj` has 9 rows written as `<Module id= name=>` and 0 as `<file>`, so every one of them is ignored. <!-- measured: grep -c "<file " Main/_Module/ModuleData/project.mbproj; grep -c "<Module " and grep -c "<file " on TAOM_Map/ModuleData/project.mbproj 2026-09-05 -->
- **A Monster does not need a native row.** `LOTRLOME_Armory/SubModule.xml:216-295` registers eight `Monsters` nodes through the managed path, and that is the path `Game.LoadBasicFiles` reads (`Game.cs:437`). <!-- measured: grep -c 'XmlName id="Monsters"' on LOTRLOME_Armory/SubModule.xml 2026-09-05 -->

## B. Engine load order (v1.4.8)

Two orders matter. The first is the order of modules, which fixes which module's XML lands on top of which. The second is the order of ids inside one campaign start, which fixes which objects already exist when a given file is read.

### Module order comes from the launcher, not from `<DependedModules>`

`Module.Initialize` hands the engine an id array from the native side (`Utilities.GetModulesNames()`, `Module.cs:261`). `ModuleHelper.InitializeModules` walks that array in order and inserts each match into `_loadedModules` (`ModuleHelper.cs:85-98`), and `ModuleHelper.GetModules` returns `_loadedModules.Values` in that insertion order (`ModuleHelper.cs:178-189`). `<DependedModules>` and `<ModulesToLoadAfterThis>` are parsed into `ModuleInfo` (`ModuleInfo.cs:105-131`, `132-140`), but the only topological sort in the engine, `GetSortedModules` (`ModuleHelper.cs:271-280`), has two callers, `CustomBattleServer.cs:208` and `LobbyClient.cs:474`, both multiplayer. <!-- measured: grep -rn GetSortedModules over the v1.4.8 decompile, 3 hits 2026-09-05 --> The vanilla launcher's own view model reads `ModulesToLoadAfterThis` to arrange its list (`LauncherModuleVM.cs:244-251`). So in singleplayer the dependency elements are a launcher-side constraint and a documentation contract; the list the launcher saves is what orders the merge.

The vanilla launcher persists that list to `Documents/Mount and Blade II Bannerlord/Configs/LauncherData.xml`. On the dev machine that file lists 14 modules, in this order: `TAOM.Dependencies`, `Native`, `SandBoxCore`, `Sandbox`, `StoryMode`, `CustomBattle`, `NavalDLC`, `BirthAndDeath`, `FastMode`, `TAOM_Map`, `LOTRLOME_Armory`, `TAOM`, `SandBoxCoreMP`, `Bannerlord.Diplomacy`. <!-- measured: python ElementTree over Configs/LauncherData.xml, SingleplayerData/ModDatas 2026-09-05 --> `TAOM` therefore sits after both live data modules, so its stylesheets run after their rows are in. Today that changes little: `TAOM_Map`'s `spcultures.xml` and `spnpccharacters.xml` are empty Kit stubs (`<SPCultures/>`, `<NPCCharacters/>`), and TAOM ships no stylesheet for any id the Armory registers. What the module order always decides is which module's rows win when two modules define the same id (section E). `TAOM_Map/SubModule.xml:19` asks BUTR-style launchers to put `TAOM` before it instead (`<DependedModuleMetadata id="TAOM" order="LoadBeforeThis"/>`); the vanilla engine has no branch for that element (`ModuleInfo.cs:105-150` parses only `DependedModules`, `ModulesToLoadAfterThis`, `IncompatibleModules` and `SubModules`; the word `Metadata` does not occur in the file <!-- measured: grep -c Metadata ModuleInfo.cs, 0 hits 2026-09-05 -->), and `Main/_Module/SubModule.xml:32-37` says so in-file. Which launcher a player uses decides which of the two orders they get; see the open questions at the end.

Inside one module, entries keep their `SubModule.xml` order, and that order only matters between rows that share an `id`. In `Main/_Module/SubModule.xml` the stylesheet rows come first and the plain files after them: `lords.xslt` at line 96 before `characters/lords` at 157, `heroes.xslt` at 106 before `characters/heroes` at 148, `spcultures.xslt` at 78 before `taom_spcultures` at 119, `spkingdoms.xslt` at 70 before `taom_spkingdoms` at 130, `spclans.xslt` at 88 before `characters/clans` at 139. <!-- measured: grep -n "<XmlName" Main/_Module/SubModule.xml 2026-09-05 --> Rows with different ids do not order each other: `troops/troops_erebor` is registered at line 199 and `equipmentsets/taom_equipment_sets_erebor` at line 470, and the rosters still load first, because the engine reads `EquipmentRosters` before `NPCCharacters` (next table).

### Id order inside one campaign start

Each `LoadXML(id)` call merges every active module's rows for that id and deserializes them (`MBObjectManager.cs:786-797`). The calls run in this sequence for both a new campaign and a loaded save unless a row says otherwise.

<!-- engine-ref type="TaleWorlds.Core.Game" file="Core/TaleWorlds.Core/TaleWorlds.Core/Game.cs" lines="435-445" -->

| Step | `LoadXML` id | Engine element / class | Read at | Runs on |
|---|---|---|---|---|
| 1 | `Monsters` | `Monster` / `Monster` | `Game.cs:437` | every campaign start |
| 2 | `SkeletonScales` | `Scale` / `SkeletonScale` (id and root element differ) | `Game.cs:438` | every campaign start |
| 3 | `ItemModifiers` | `ItemModifier` | `Game.cs:439` | every campaign start |
| 4 | `ItemModifierGroups` | `ItemModifierGroup` | `Game.cs:440` | every campaign start |
| 5 | `CraftingPieces` | `CraftingPiece` | `Game.cs:441` | every campaign start |
| 6 | `WeaponDescriptions` | `WeaponDescription` | `Game.cs:442` | every campaign start |
| 7 | `CraftingTemplates` | `CraftingTemplate` | `Game.cs:443` | every campaign start |
| 8 | `BodyProperties` | `BodyProperty` / `MBBodyProperty` | `Game.cs:444` | every campaign start |
| 9 | `SkillSets` | `SkillSet` / `MBCharacterSkills` | `Game.cs:445` | every campaign start |

`Game.LoadBasicFiles` is called from `Campaign.InitializeDefaultCampaignObjects` (`Campaign.cs:1470`), which runs for a loaded save at `Campaign.cs:1398` and for a new campaign through `OnNewCampaignStart` at `Campaign.cs:1524`. The same method then continues:

<!-- engine-ref type="TaleWorlds.CampaignSystem.Campaign" file="Campaign/TaleWorlds.CampaignSystem/TaleWorlds.CampaignSystem/Campaign.cs" lines="1462-1473" -->

| Step | `LoadXML` id | Engine element / class | Read at | Runs on |
|---|---|---|---|---|
| 10 | `Items` | `Item` / `ItemObject` | `Campaign.cs:1471` | every campaign start |
| 11 | `EquipmentRosters` | `EquipmentRoster` / `MBEquipmentRoster` | `Campaign.cs:1472` | every campaign start |
| 12 | `partyTemplates` | `PartyTemplate` / `PartyTemplateObject` (lower-case `p`) | `Campaign.cs:1473` | every campaign start |
| 13 | `SPCultures` | `Culture` / `CultureObject` | `Campaign.cs:1462`, called at `Campaign.cs:1410` | every campaign start |
| 14 | `Concepts` | `Concept` | `Campaign.cs:1463` | every campaign start |

Note the order of steps 10-13: in v1.4.8 items, rosters and party templates are read **before** cultures, because `InitializeBasicObjectXmls` is called at `Campaign.cs:1410`, after the branch at `Campaign.cs:1396-1408` that runs `InitializeDefaultCampaignObjects`. A roster's `culture="Culture.erebor"` (`MBEquipmentRoster.cs:61`) is therefore a forward reference; it works because of the presumed-object mechanism in section C.

`Campaign.cs:1415` then calls `SandBoxManager.OnCampaignStart`, which reaches `SandBoxSubModule.RegisterSubModuleObjects` (`SandBoxSubModule.cs:131`) and `SandBoxManager.InitializeSandboxXMLs`:

<!-- engine-ref type="TaleWorlds.CampaignSystem.SandBoxManager" file="Campaign/TaleWorlds.CampaignSystem/TaleWorlds.CampaignSystem/SandBoxManager.cs" lines="360-382" -->

| Step | `LoadXML` id | Engine element / class | Read at | Runs on |
|---|---|---|---|---|
| 15 | `NPCCharacters` | `NPCCharacter` / `CharacterObject` | `SandBoxManager.cs:362` | every campaign start |
| 16 | `Heroes` | `Hero` | `SandBoxManager.cs:363-366` | new campaign only |
| 17 | `MPCharacters` | `NPCCharacter` / `BasicCharacterObject` | `SandBoxManager.cs:367-370` | tutorial only |
| 18 | `Kingdoms` | `Kingdom` | `SandBoxManager.cs:371-373` | new campaign only |
| 19 | `Factions` | `Faction` / `Clan` (file `spclans.xml`) | `SandBoxManager.cs:374` | new campaign only |
| 20 | `WorkshopTypes` | `WorkshopType` | `SandBoxManager.cs:376` | every campaign start |
| 21 | `LocationComplexTemplates` | `LocationComplexTemplate` | `SandBoxManager.cs:377` | every campaign start |
| 22 | `Settlements` | `Settlement` (+ `Town`, `Village`, `Hideout` components) | `SandBoxManager.cs:378-381` | campaign mode, not the editor |

On a loaded save, `SandBoxManager.OnCampaignStart` also calls `MBObjectManager.RemoveTemporaryTypes` (`SandBoxManager.cs:344-347`), which unregisters every object of every type registered with `isTemporary: true` and removes the type record (`MBObjectManager.cs:655-669`). Those types are `MobileParty`, `Clan`, `Kingdom` and `Hero` (`Campaign.cs:1536`, `1543`, `1545`, `1555`). New rows in `Heroes`, `Kingdoms` or `Factions` therefore never reach an existing save; section F says what that means for you.

After all of that, `Campaign.cs:1438` calls `UnregisterNonReadyObjects`, which drops every ghost created by a dangling reference (section C).

## C. Two kinds of reference

Every `X="Type.id"` attribute in ModuleData is read by one of two mechanisms, and the difference decides whether a typo is a silent ghost, an empty slot, or a crash.

<!-- engine-ref type="TaleWorlds.ObjectSystem.MBObjectManager" file="Core/TaleWorlds.ObjectSystem/TaleWorlds.ObjectSystem/MBObjectManager.cs" lines="573-596, 713-735, 1517-1535" -->

| Mechanism | What it does | On a missing target | Read at |
|---|---|---|---|
| `ReadObjectReferenceFromXml` ("forward-safe") | Splits the value on the first `.` into a type name and an id, then calls `GetPresumedObject` | The dot is mandatory: a value with no `.` throws `MBInvalidReferenceException`. An unknown id gets a placeholder object of that type, registered with `IsReady = false` (auto-create is the default for every `RegisterType` call, `MBObjectManager.cs:376`). If a later file deserializes that id the placeholder becomes the real object. If nothing ever does, `UnregisterNonReadyObjects` prints `Null object reference found with ID: <id>` and removes it | `MBObjectManager.cs:1517-1535`, `713-735`, `1437-1459`; `MBObjectManager.cs:135-148`, `240`; `MBObjectBase.cs:41-45` |
| `GetObject<T>(id)` ("must already exist") | Looks the id up in the type record | Returns `null`, no log line; what the caller does with the null decides the outcome | `MBObjectManager.cs:573-596` |

Forward-safe attributes, with the line that reads each:

| Element | Attributes read with `ReadObjectReferenceFromXml` | Read at |
|---|---|---|
| `<NPCCharacter>` (troop, lord, notable, wanderer) | `culture`; `skill_template`; `<face_key_template value>`; `<upgrade_target id>`; `civilianTemplate`, `battleTemplate`; `upgrade_requires` | `BasicCharacterObject.cs:484`, `337`, `457`; `CharacterObject.cs:565`, `592`, `597`, `586` |
| `<EquipmentRoster>` (standalone roster) | `culture` | `MBEquipmentRoster.cs:61` |
| `<MBPartyTemplate>` stacks | `troop` | `PartyTemplateObject.cs:39` |
| `<Culture>` | every `*_party_template` binding, `basic_troop`, `elite_basic_troop`, the militia and service-NPC bindings, the `*_equipment_roster` bindings, `default_character_creation_body_property`; the notable, child, teen and caravan child lists | `CultureObject.cs:270-339`, `423-507` |
| `<Hero>` | `father`, `mother`, `spouse`, `faction` | `Hero.cs:1828-1834` |
| `<Faction>` (clan) | `owner`, `super_faction`, `initial_home_settlement`, `culture`, `default_party_template` | `Clan.cs:862-888` |
| `<Kingdom>` | `initial_home_settlement`, `owner` (a Hero; the ruling clan is that hero's clan), `<relationship kingdom>` or `clan` | `Kingdom.cs:762`, `765`, `783` |
| `<Settlement>` | `culture` (every load); `owner` (new campaign only); `<Village village_type>`, `bound` | `Settlement.cs:961`, `1036-1042`; `Village.cs:293`, `300` |
| `<Item>` / `<CraftedItem>` | `culture`; `<Horse monster>`; a crafting piece's `culture` | `ItemObject.cs:487`, `540`; `HorseComponent.cs:150`; `CraftingPiece.cs:160` |
| `<ItemModifier>` | `modifier_group` (the modifier adds itself to the group) | `ItemModifier.cs:76` |

A forward-safe reference to a typo is the worst kind of error to have, because the game runs. A troop whose `culture="Culture.erebro"` gets a culture object that no file ever fills, the ghost is dropped at `Campaign.cs:1438`, and the troop simply has no culture. The static gate for this class is `python tools/validate_moduledata.py` (`BROKEN_ITEM_REF`, `BROKEN_TROOP_REF`, `BROKEN_PARTY_TEMPLATE_REF`, `BROKEN_BODY_PROPERTY_REF`), which resolves ids across all three modules before the engine ever runs. <!-- measured: grep -o "BROKEN_[A-Z_]*" tools/validate_moduledata.py | sort -u 2026-09-05 -->

Must-already-exist lookups, and what each does with `null`:

| Where you write it | Lookup | On a missing target | Read at |
|---|---|---|---|
| `<EquipmentSet id="...">` inside a troop (a reference to a standalone roster, not the troop's own inline `<EquipmentRoster>`) | `GetObject<MBEquipmentRoster>` | `AddEquipmentRoster(null, ...)` dereferences the null: `NullReferenceException` while loading `NPCCharacters`, which ends that id's load (truncation rule below) | `BasicCharacterObject.cs:407`, `MBEquipmentRoster.cs:112` |
| `<equipment slot="..." id="Item.x"/>` in any roster or troop | `GetObject<ItemObject>` | `IsItemFitsToSlot` returns `true` for a null item and the slot gets an empty element: a silently naked troop, no log line | `Equipment.cs:204-223`, `445-450` |
| `base_monster="..."` on a `<Monster>` | `GetObject<Monster>` | `monster.BaseMonster` is read on the null: `NullReferenceException`. The base must be earlier in the merged `Monsters` document | `Monster.cs:193-214` |
| `<WeaponDescription id>` and `<UsablePiece piece_id>` under a crafting template | `GetObject<WeaponDescription>` / `GetObject<CraftingPiece>` | Skipped when null: the template silently loses the description or piece | `CraftingTemplate.cs:199-203`, `213-217` |
| `<Piece id="..." Type="...">` under a `<CraftedItem>` | `GetObject<CraftingPiece>` | Wrapped in a `WeaponDesignElement` with a null piece; the failure surfaces later, when the weapon is built | `ItemObject.cs:461-462`, `WeaponDesignElement.cs:174-177` |
| `modifier_group="..."` and `item_category="..."` on an item | `GetObject<ItemModifierGroup>` / `GetObject<ItemCategory>` | Null group means no quality modifiers; null category means no category. Both silent | `ItemObject.cs:436-440`, `541-545` |
| `<Building id="..." level="N"/>` under a town | `GetObject<BuildingType>` | `buildingType.StartLevel` on the null: `NullReferenceException`, new campaign only | `Town.cs:709-716` |

**The truncation rule.** `LoadXml` walks the merged document's entries in order and calls `Deserialize` on each (`MBObjectManager.cs:1387-1395`). The public `LoadXML` wraps that whole walk in `try { } catch (Exception) { }` (`MBObjectManager.cs:790-796`). So when one entry throws, every entry after it in that id's merged document is never read, and nothing is logged. "Half my lords are missing" means "the entry after the last survivor is broken", not "half the file is wrong". Because the merged document is all modules' rows for that id in module order, the missing half can belong to a different module than the broken entry.

## D. The existence ladder

To add a thing, everything below it on this ladder must already resolve. Each row names the chapter that owns the step; the sources are the engine lines above plus [kingdom-creation](../features/kingdom-creation.md), lines 49-66, and the playability checklist in [culture-playability-wiring](../features/culture-playability-wiring.md), lines 95-120.

| To add | These must already exist, in this order | Owning chapters |
|---|---|---|
| An armour piece or weapon | The mesh in the Armory's `Assets/` tree, then the `<Item>` or `<CraftedItem>` in a registered `LOTRLOME_items/<culture>/` file or folder (new file in a registered folder: no manifest edit; new folder: a new `<XmlNode>`), then any roster or troop that names `Item.<id>` | [items-armor](items-armor.md), [items-weapons-and-crafting](items-weapons-and-crafting.md), [module-armory](module-armory.md) |
| A troop | Every item it wears; any standalone roster it names in `<EquipmentSet id>` (must-already-exist); the `<NPCCharacter>` itself in a registered `troops/troops_<culture>.xml`; the parent's `<upgrade_target>` (forward-safe); a stack in the culture's party templates; a recruitment pool entry, which is C# for every culture except Gondor; and, for a militia or basic troop, the culture binding | [troops](troops.md), [party-templates](party-templates.md), [equipment-rosters](equipment-rosters.md) |
| A lord | The clan (`faction=`), the culture, a `<NPCCharacter is_hero="true">` in `characters/lords.xml` and a `<Hero>` of the same id in `characters/heroes.xml` (skip the second and the game crashes, [kingdom-creation](../features/kingdom-creation.md) line 55), a `SkillSet` for `skill_template`, and at least one settlement of that culture in the live map or the daily clan tick crashes (checklist row 11) | [lords-and-heroes](lords-and-heroes.md), [skill-sets](skill-sets.md), [clans](clans.md) |
| A clan | The culture; the owner hero (forward-safe, so the hero may come later in the same load); the kingdom for `super_faction`; a settlement for `initial_home_settlement` | [clans](clans.md) |
| A culture | The `<Culture>` block in `taom_spcultures.xml` (or a retag block in `spcultures.xslt`, which inherits Calradia for every attribute it does not name, section E); all twelve party templates and both caravan child lists; the notable, child, teen, education and lord equipment templates; the character-creation files; at least one settlement; then everything that names `Culture.<id>` | [cultures](cultures.md), [recipe-add-a-culture](recipe-add-a-culture.md) |
| A kingdom | The culture and the owner hero's clan first; `initial_home_settlement`; then every config that enumerates kingdoms | [kingdoms](kingdoms.md), [recipe-add-a-kingdom](recipe-add-a-kingdom.md), [configs-factions-and-world](configs-factions-and-world.md) |
| A settlement | A scene entity in the live map; the `<Settlement>` row in `TAOM_Map/ModuleData/settlements.xml` with its culture; a bound town before its villages (`bound=` is forward-safe, but the town must exist by the end of the load); the distance cache rebuilt after any add, move or delete | [settlements](settlements.md), [module-map](module-map.md) |
| A race | The `<race>` appended at the end of the Armory's `skins.xml` (race integers are merge-order indices, so never insert in the middle), the `Monster` rows, both facegen action sets, a `BodyProperty`, then any `race=` | [recipe-add-a-race-or-creature](recipe-add-a-race-or-creature.md), [body-properties](body-properties.md) |

The order of steps 10-22 in section B is why the ladder reads bottom-up: items exist when rosters load, rosters and templates exist when cultures load, all of those exist when troops load, and troops exist when settlements load.

## E. Cross-module merge and the XSLT layer

Each id's merged document is built by `CreateMergedXmlFile` (`MBObjectManager.cs:966-982`) from the entries collected in section A, in module order. For entry `i` (counting from 1) it first applies that module's stylesheet, if any, to the accumulated document, and then merges that module's own file on top. Entry 0 contributes its file only; its stylesheet is never read.

<!-- engine-ref type="TaleWorlds.ObjectSystem.MBObjectManager" file="Core/TaleWorlds.ObjectSystem/TaleWorlds.ObjectSystem/MBObjectManager.cs" lines="799-875, 966-1010" -->

| Rule | Behaviour | Read at |
|---|---|---|
| Schema decides merge versus append | `MergeTwoXmls` appends the second document's elements when there is no XSD for the id; with an XSD it calls `MergeElements` | `MBObjectManager.cs:997-1010` |
| Which ids have a schema | The install ships 51 XSD files under `XmlSchemas/`; every id in section B's tables that TAOM touches has one (`Items`, `EquipmentRosters`, `partyTemplates`, `NPCCharacters`, `SPCultures`, `Factions`, `Kingdoms`, `Heroes`, `Settlements`, `SkillSets`, `BodyProperties`, `Monsters`, `CraftingPieces`, `CraftingTemplates`, `WeaponDescriptions`, `BannerIcons`, `GameText`, `WorkshopTypes`) | `ModuleHelper.cs:247-250` <!-- measured: ls "<game>/XmlSchemas"/*.xsd | wc -l, plus a per-id test -f 2026-09-05 --> |
| Same id merges by the XSD's unique key | Children are grouped by element name and keyed on the `xs:unique` fields; a later element with the same key merges into the earlier one, otherwise it is appended as a sibling | `MBObjectManager.cs:837-871`; keys for `NPCCharacters.xsd` at lines 285-415 and 488-491 (`NPCCharacter@id`, `EquipmentSet@id+@civilian+@stealth`, `equipment@slot`, `skill@id`, `upgrade_target@id`), for `Items.xsd` at 459-462 and 500-509 |
| Attributes replace, one at a time | `MergeElementAttributes` sets only the attributes the later document declares; an attribute the later file omits keeps the earlier value | `MBObjectManager.cs:799-818` |
| `_replaceWhileMerging="true"` | Removes every attribute and every child of the earlier element before the later one lands; the only true replace. TAOM does not use it ([lord-identity-reconciliation](../features/lord-identity-reconciliation.md), lines 24-34) | `MBObjectManager.cs:804-808`, `829-832` |
| `AlwaysPreferMerge` | An XSD annotation that makes the merger recurse into the earlier parent's first child of that name without computing a key | `MBObjectManager.cs:851-855` |
| A stylesheet sees the accumulation | `ApplyXslt` runs a plain `XslCompiledTransform` over the whole merged document so far, not over the module's own file | `MBObjectManager.cs:984-995` |

Three consequences the artist meets in practice:

- **A duplicate id does not shadow, it merges.** A TAOM `<NPCCharacter id="X">` that shares an id with a vanilla one produces one hybrid object carrying vanilla's attributes plus TAOM's overrides. The same is true between TAOM's own stylesheet output and its plain files: `lords.xslt` runs first, `characters/lords.xml` merges on top, and the plain XML wins per attribute, not per node. Seventeen lords take their `is_female` from the stylesheet because the plain file never states one ([lord-identity-reconciliation](../features/lord-identity-reconciliation.md), lines 23-34).
- **A retag block in `spcultures.xslt` inherits every attribute it does not name, and what it inherits is Calradia.** The block's `<xsl:apply-templates select="@*"/>` copies the vanilla value in, so a missing binding is not "unchanged", it is a Calradian troop with nothing in the file to grep for. This shipped four times (Dale, Rohan, Khand, and nine town-owning cultures' patrols) ([xslt rule](../../.claude/rules/xslt.md), lines 20-36; [culture-playability-wiring](../features/culture-playability-wiring.md), lines 253-259). Child elements go the other way: `CultureObject.Deserialize` adds every `caravan_party_templates` child it meets (`CultureObject.cs:485-497`), so overriding one takes two edits, emit yours and filter vanilla's out of the passthrough ([xslt rule](../../.claude/rules/xslt.md), lines 38-50). The gate that reads the transform's output rather than its markup is `TAOM.Tests/Core/CulturePartyTemplateTests.cs` (lines 57-65 of the same rule).
- **A stylesheet can delete an entire earlier contribution.** `TAOM_Map/ModuleData/settlements.xslt` is an identity transform plus one empty template, `<xsl:template match="Settlement"/>`, so every vanilla settlement is removed before `TAOM_Map`'s own file merges: 494 vanilla rows out, 988 TAOM rows in. <!-- measured: grep -cP "<Settlement\b" on SandBox/ModuleData/settlements.xml and TAOM_Map/ModuleData/settlements.xml 2026-09-05 --> The deletion leaves no marker in the output, which is why any tool over merged data must replay the strip ([xslt-moduledata lessons](../reviews/lessons/xslt-moduledata.md), lines 116-135).

Two non-`MBObject` surfaces follow their own precedence. Banner icon groups merge by group id and an icon or colour id already present is kept, so the earliest module wins per id (`BannerIconGroup.cs:77-85`, `BannerManager.cs:240-247`, `258-259`). Gauntlet prefabs key on the file name without extension and a later module's file overwrites the dictionary entry after a `FailedAssert`, so the last module wins (`WidgetFactory.cs:78-93`). Details in [banners-and-heraldry](banners-and-heraldry.md) and [module-taom](module-taom.md).

## F. When your change shows up, and what a save keeps

| Column | What lands here | Source |
|---|---|---|
| **Full game restart** | Any new XML file, any new `<XmlNode>`, any change to `SubModule.xml` or `project.mbproj` (the registry is built once in `Module.Initialize`); every JSON config read by a `Reuse.Singleton` provider, which caches for the process, including `cc_body_properties.xml` | `Module.cs:246`, `261-267`, `1031-1032`; [character-creation-body-properties](../features/character-creation-body-properties.md), lines 127-129; [battle-balance](../features/battle-balance.md), line 103; [banner-bearers](../features/banner-bearers.md), line 109; [career-system](../features/career-system.md), line 300 |
| **New campaign only** | Every row in `Heroes`, `Kingdoms` and `Factions` (the loads are skipped and the types are removed on a saved campaign); settlement `owner`; town `prosperity` and `<Buildings>` levels; hero skills, which bake at hero creation; clan `color` and `color2`, which are `[SaveableProperty]`; family links and initial children, since they are Hero rows | `SandBoxManager.cs:344-347`, `363-375`; `Settlement.cs:1036-1042`; `Town.cs:688-716`; [settlement-building-levels](../features/settlement-building-levels.md), lines 11-21; [lord-skills](../features/lord-skills.md), lines 133-135; [clan-heraldry](../features/clan-heraldry.md), lines 45-49 |
| **Next campaign load, existing save included** | Everything in steps 1-15 and 20-22 of section B is re-deserialized on every load: item stats, standalone rosters, party templates, culture bindings, troop stats and inline rosters, settlement `culture`. A culture binding fix therefore reaches an old save for every party spawned after the load; parties already on the map keep the roster they were drawn with. `Settlement.Culture` is a bare field the save system never writes, so a culture retag lands on every existing save, not only new ones | `Campaign.cs:1398`, `1410`; `SandBoxManager.cs:362`, `376-381`; `Settlement.cs:961`; [culture-playability-wiring](../features/culture-playability-wiring.md), lines 122-130; [lord-spawn-guard](../features/lord-spawn-guard.md), lines 194-204 |
| **Persisted per player, outside the mod** | Every MCM value, written to `Documents/Mount and Blade II Bannerlord/Configs/ModSettings/Global/TAOM/TAOM.json`; on load MCM overrides the C# default for any property already present, so a retuned default only reaches fresh installs. The AI party-size knobs apply live, no restart and no new campaign | [bandit-management](../features/bandit-management.md), line 81; [war-of-the-ring](../features/war-of-the-ring.md), line 134; [ai-party-size](../features/ai-party-size.md), lines 240-265 |

Save rules that follow from the above:

- **Never rename a settlement, troop, clan, kingdom, hero or item id.** Ids are what a save stores; a renamed settlement id is a missing object on load ([xslt-moduledata lessons](../reviews/lessons/xslt-moduledata.md), lines 10-14). The troop rule is "Never change troop IDs", which is why `dg_uruk_veteran_warrior` was restatted in place rather than renamed ([troop-tree-revamp](../features/troop-tree-revamp.md), line 172). Retiring is covered in [recipe-retire-content](recipe-retire-content.md).
- **`is_obsolete="true"` is read, but it does less than the name suggests.** `BasicCharacterObject.cs:336` reads it, and the only consumer outside that line is `Hero.PreAfterLoad`, which skips supporter and companion re-wiring for an obsolete character (`Hero.cs:1537`). <!-- measured: grep -rn IsObsolete over the v1.4.8 decompile, 2 hits 2026-09-05 --> It is not a delete.
- **Race integers are merge-order indices.** The Armory's `skins.xml` says so beside its last `<race>`: "Appended at END - race ints are skins.xml merge-order indices (issue #321)" (`LOTRLOME_Armory/ModuleData/skins.xml:204236`). Inserting a race above an existing one renumbers every troop below it in every save.
- **Save type ids are arithmetic, not names.** A `SaveableTypeDefiner`'s global id is `base + localId`; a collision is a `Module.Initialize` crash before any save loads ([state-lifecycle-save lessons](../reviews/lessons/state-lifecycle-save.md), lines 98-103).
- **Never write a "this persists" claim from memory.** `Hero.Culture` and `Settlement.Culture` are both bare public fields; one persists and one does not ([state-lifecycle-save lessons](../reviews/lessons/state-lifecycle-save.md), lines 191-198). Check the field before promising a player anything about their save.

## Worked example

The Erebor chain, walked bottom-up through the shipped files, with every link measured on 2026-09-05.

| Rung | File | Registered at | What it contributes, and what it needs |
|---|---|---|---|
| Items | `LOTRLOME_Armory/ModuleData/LOTRLOME_items/erebor/` (6 files, 137 items) and `.../iron_hills/` (5 files, 168 items) | `LOTRLOME_Armory/SubModule.xml:83`, `:145` | The meshes named inside them live under the Armory's `Assets/` tree <!-- measured: ls each folder; cat folder/*.xml | grep -cP "<Item\b" 2026-09-05 --> |
| Standalone rosters | `Main/_Module/ModuleData/equipmentsets/taom_equipment_sets_erebor.xml`, 17 rosters | `Main/_Module/SubModule.xml:470` | 122 `Item.` references, 30 distinct: 16 resolve in `iron_hills/`, 8 in `LOTRAOM_weapons.xml`, 4 in `LOTRAOM_horses.xml`, 2 in `LOTRAOM_shields.xml`, none unresolved. These rosters are consumed by 2 bindings in `taom_spcultures.xml` and 72 `<EquipmentSet>` references in `characters/lords.xml`; the troop file does not use them <!-- measured: python re over the roster file against an id index of LOTRLOME_items/* and SandBoxCore/items 2026-09-05 --> |
| Troops | `Main/_Module/ModuleData/troops/troops_erebor.xml`, 66 `<NPCCharacter>` | `Main/_Module/SubModule.xml:199` | 65 carry `culture="Culture.erebor"` and one (`erebor_warriors_boss`) `Culture.erebor_warriors`; 54 `<upgrade_target>`; 1666 inline `Item.` references, 298 distinct: 122 in `erebor/`, 102 in `iron_hills/`, 40 in `LOTRAOM_weapons.xml`, 23 in `LOTRAOM_shields.xml`, 10 in `LOTRAOM_horses.xml`, 1 in `SandBoxCore/items`; 17 `<EquipmentSet id>` references, all to vanilla's `battania_troop_civilian_template_t1`; every `face_key_template` is `BodyProperty.fighter_erebor` <!-- measured: grep -cP and python re over troops_erebor.xml 2026-09-05 --> |
| Culture | `Main/_Module/ModuleData/taom_spcultures.xml`, `<Culture id="erebor">` at line 6, 73 attributes | `Main/_Module/SubModule.xml:119` | Binds `basic_troop="NPCCharacter.erebor_reg_miner"`, `elite_basic_troop="NPCCharacter.erebor_noble"`, the party templates (`default_party_template="PartyTemplate.kingdom_hero_party_erebor_template"`, `villager_`, `militia_`, `rebels_`, `vassal_reward_`) and the rosters `EquipmentRoster.erebor_bat_template_medium_a` and `erebor_civ_template_default_a` <!-- measured: python re over the first Culture element 2026-09-05 --> |
| Party templates | `Main/_Module/ModuleData/taom_partyTemplates.xml`, 21 templates with `erebor` in the id | `Main/_Module/SubModule.xml:331` | 67 distinct `troop=` references: 63 defined in `troops_erebor.xml`, 4 (`armed_trader_erebor`, `caravan_guard_erebor`, `veteran_caravan_guard_erebor`, `villager_erebor`) in `characters/npcs_erebor.xml` (74 entries, registered at line 461) <!-- measured: grep -o MBPartyTemplate ids; python re over the erebor blocks 2026-09-05 --> |
| Clans | `Main/_Module/ModuleData/characters/clans.xml`, `clan_erebor_1` to `clan_erebor_7` (lines 458-548) | `Main/_Module/SubModule.xml:139` | Each names its owner hero, `Culture.erebor`, `Kingdom.erebor`, a home settlement and a party template |
| Lord pair | `characters/lords.xml`, 36 `<NPCCharacter>` with `Culture.erebor`; `characters/heroes.xml`, 36 `<Hero>` with `Faction.clan_erebor_*`; `lords.xslt` and `heroes.xslt` carry no `lord_E` template, so Erebor lords are plain XML only | `Main/_Module/SubModule.xml:157`, `:148` | `lord_E1_1` names `SkillSet.taom_dwarf_king_skills` and `Culture.erebor`; his `<Hero>` names `Faction.clan_erebor_1` <!-- measured: grep -cP over lords.xml, heroes.xml, lords.xslt, heroes.xslt 2026-09-05 --> |
| Kingdom | `Main/_Module/ModuleData/taom_spkingdoms.xml`, `<Kingdom id="erebor">` at line 4 | `Main/_Module/SubModule.xml:130` | `owner="Hero.lord_E1_1"`, `initial_home_settlement="Settlement.town_E1"`, `culture="Culture.erebor"` |
| Settlements | `TAOM_Map/ModuleData/settlements.xml` (live), 52 rows with `culture="Culture.erebor"`: 4 `town_E*`, 9 `castle_E*`, 16 `village_E*`, 23 `castle_village_E*`; 13 of them owned by a `clan_erebor_*` | `TAOM_Map/SubModule.xml:73` | The `E` prefix, not `EB`, is the Erebor settlement family <!-- measured: grep -oP over the live settlements.xml 2026-09-05 --> |

Three verbatim rungs, so you can see the references in place.

<!-- example file="Main/_Module/ModuleData/characters/clans.xml" id="clan_erebor_1" -->
```xml
  <Faction
		id="clan_erebor_1"
		initial_home_settlement="Settlement.town_E1"
		name="{=aom_clan_erebor_1_name}Bit Durin"
		tier="6"
		owner="Hero.lord_E1_1"
		culture="Culture.erebor"
		super_faction="Kingdom.erebor"
		is_noble="true"
		color="FF153F1C"
		color2="FF964309"
		default_party_template="PartyTemplate.kingdom_hero_party_erebor_erebor_1_template"
		banner_key="11.100.75.4345.4345.764.764.1.0.0.521.172.100.51.51.652.641.0.0.267.521.172.100.45.45.583.712.0.0.267.521.172.100.35.35.602.802.0.0.267.521.172.100.51.51.877.641.0.1.87.521.172.100.45.45.944.712.0.1.87.521.172.100.35.35.925.802.0.1.87.24019.31.240.334.334.764.854.1.0.0.24510.31.240.167.167.764.589.1.0.0" />
```

1. `owner="Hero.lord_E1_1"` is forward-safe (`Clan.cs:862`), so the hero could be defined later; in practice it already exists, because `Heroes` (step 16) loads before `Factions` (step 19). Both are new-campaign-only rows.
2. `initial_home_settlement="Settlement.town_E1"` must end the load as a real settlement in the live map; a culture that owns no settlement crashes on the daily clan tick (checklist row 11; [lord-spawn-guard](../features/lord-spawn-guard.md)).
3. `default_party_template="PartyTemplate.kingdom_hero_party_erebor_erebor_1_template"` is one of the 21 templates above; the first thing to check when a clan's parties look Calradian.

<!-- example file="Main/_Module/ModuleData/characters/heroes.xml" id="lord_E1_1" -->
```xml
	<Hero
		id="lord_E1_1"
		faction="Faction.clan_erebor_1"
		text="{=dain_ironfoot_description}Dáin Ironfoot, the stalwart and unyielding King under the Mountain, stands as a paragon of dwarven resilience and valor. Renowned for his unwavering determination and tactical prowess, Dáin is a battle-hardened leader who inspires loyalty and courage in his kin. A veteran of countless conflicts, he led the dwarves of the Iron Hills with unmatched strength, bringing stability and prosperity to his people. His resolve in the face of adversity and his dedication to the defense of Erebor and its allies ensure that the legacy of Durin's folk endures through the darkest days of the Third Age." />
	<Hero
		id="lord_E1_2"
		father="Hero.lord_E1_1"
		mother="Hero.lord_E1_6"
		faction="Faction.clan_erebor_1" />
```

1. `faction="Faction.clan_erebor_1"` is read at `Hero.cs:1834` and is the attribute that ties this record to the clan above; a `<Hero>` with no `<NPCCharacter>` of the same id, or the reverse, is the crash in [kingdom-creation](../features/kingdom-creation.md), line 55.
2. `father="Hero.lord_E1_1"` and `mother="Hero.lord_E1_6"` on the second record are forward-safe (`Hero.cs:1828-1829`): `lord_E1_6` is defined further down the same file.
3. Every `<Hero>` row is new-campaign-only (step 16), so a family edit here never reaches an existing save.

<!-- example file="Main/_Module/ModuleData/characters/lords.xml" id="lord_E1_1" -->
```xml
    <NPCCharacter id="lord_E1_1" race="dwarf" name="{=aom_lord_E1_1_name}Dáin II Ironfoot" age="38" voice="earnest" is_hero="true" culture="Culture.erebor" occupation="Lord" default_group="Infantry" face_mesh_cache="true" skill_template="SkillSet.taom_dwarf_king_skills">
```

1. `culture="Culture.erebor"` (`BasicCharacterObject.cs:484`) and `skill_template="SkillSet.taom_dwarf_king_skills"` (`BasicCharacterObject.cs:337`) are both forward-safe, and both targets are already real objects here because `SPCultures` is step 13 and `SkillSets` is step 9.
2. `race="dwarf"` is an index into the Armory's `skins.xml` order, the merge-order rule in section F.
3. This line is the opening tag only; the element continues with the face key, the skills and this lord's share of the 72 `<EquipmentSet>` references to the standalone rosters counted above.

## Gates to run before launching

None of these is a recipe; they are the checks the recipes in other chapters name in their `Check:` line.

| Gate | What it proves | Command |
|---|---|---|
| Reference graph and duplicate ids across all three modules | Every `Item.`, `NPCCharacter.`, `Culture.`, `PartyTemplate.` and `BodyProperty.` reference resolves; no landless culture on a lord, clan or kingdom (`LANDLESS_CULTURE`) | `python tools/validate_moduledata.py` |
| `project.mbproj` rows the engine will actually read | No invented `soln_*` id, no action bound but never declared, a warning for a duplicated id that has an XSD | `python tools/audit_mbproj_registration.py` |
| Culture party-template bindings after the stylesheet runs | No binding left Calradian by an `spcultures.xslt` block that never named it | `dotnet test TAOM.Tests --filter CulturePartyTemplate` |
| Lord equipment templates per culture | Every culture's lord template binding resolves | `dotnet test TAOM.Tests --filter CultureLordTemplate` |
| Recruitment reachability | Every non-militia, non-boss troop is reachable from a recruitment pool root | `dotnet test TAOM.Tests --filter AllNonMilitiaNonBossTroops_AreReachable` |

The scope of each gate, and what none of them can see, is in [validation-and-testing](validation-and-testing.md). One limit belongs here because it is a load-order fact: a static gate proves references resolve on disk, not that the engine loaded a new file. A green gate plus a naked troop in game means the file is not registered or the game was not restarted (section A), not that the data is wrong.

## Gotchas: what fails silently and what crashes

- A new XML file with no `<XmlNode>` does not exist, and a new `<XmlNode>` does not exist until the game is restarted. `Module.cs:246`, `261-267`, `1031-1032`.
- A misspelled `path=` and a stylesheet-only registration look identical to the engine: an empty slot, no log line. `MBObjectManager.cs:911-915`.
- A backup that keeps its `.xml` extension inside a folder-form registration loads and duplicates every id in it. `MBObjectManager.cs:903-909`; [lotrlome-soln-id-fix](../reference/lotrlome-soln-id-fix.md), lines 117-118.
- An invented `soln_*` id in `project.mbproj` is inert; `TAOM_Map`'s nine `<Module>` rows are inert for a second reason, the wrong element name. `XmlResource.cs:107-139`; `MBObjectManager.cs:930-933`.
- `<DependedModules>` does not order singleplayer loading; the launcher's saved list does. `ModuleHelper.cs:85-97`, `271-280`; `CustomBattleServer.cs:208`; `LobbyClient.cs:474`.
- A forward-safe reference to a typo creates a ghost that is dropped with one log line, `Null object reference found with ID:`, and the game runs. `MBObjectManager.cs:713-735`, `1437-1459`; `Campaign.cs:1438`.
- A reference written without the `Type.` prefix throws `MBInvalidReferenceException` while that id loads. `MBObjectManager.cs:1523-1533`.
- `<EquipmentSet id>` pointing at a roster that does not exist is a `NullReferenceException` inside `NPCCharacters`, and every troop after it in the merged document is lost. `BasicCharacterObject.cs:407`; `MBEquipmentRoster.cs:112`; `MBObjectManager.cs:790-796`, `1387-1395`.
- An `Item.` id that does not exist is a silently empty slot, never an error. `Equipment.cs:204-223`, `445-450`.
- A `base_monster` that loads later than the monster naming it, or not at all, is a `NullReferenceException`. `Monster.cs:193-214`.
- A same-id entry in a later module merges attribute by attribute; what it omits survives from the earlier module. `MBObjectManager.cs:799-818`.
- An `spcultures.xslt` block inherits Calradia for every attribute it does not name, and emitting a caravan child list without filtering vanilla's leaves the culture with both. [xslt rule](../../.claude/rules/xslt.md), lines 20-50; `CultureObject.cs:485-497`.
- `Heroes`, `Kingdoms` and `Factions` never reload into a saved campaign, so testing a new clan or lord on an old save proves nothing. `SandBoxManager.cs:344-347`, `363-375`.
- `Settlement.Culture` is re-read from XML on every load, so a culture retag lands on every existing save. `Settlement.cs:961`; [lord-spawn-guard](../features/lord-spawn-guard.md), lines 194-204.
- A JSON config cached by a `Reuse.Singleton` provider needs a process restart; MCM values in `TAOM.json` beat the JSON on an existing install. [character-creation-body-properties](../features/character-creation-body-properties.md), lines 127-129; [bandit-management](../features/bandit-management.md), line 81.
- The repo's `Main/_Module/ModuleData/settlements.xml` is a stale shadow; `Settlements` is registered only by `TAOM_Map/SubModule.xml:73`. [xslt-moduledata lessons](../reviews/lessons/xslt-moduledata.md), lines 10-14.

## Numbers in this chapter

All measured 2026-09-05. Paths under the game install are written module-relative.

| Number | Command |
|---|---|
| 19 module folders | `ls "<game>/Modules" \| wc -l` |
| 51 XSD files | `ls "<game>/XmlSchemas"/*.xsd \| wc -l` |
| 10 files in `SandBoxCore/ModuleData/items/` | `ls "<game>/Modules/SandBoxCore/ModuleData/items" \| wc -l` |
| 971 lines, 100 `<XmlName>` rows in `Main/_Module/SubModule.xml` | `wc -l Main/_Module/SubModule.xml`; `grep -c "<XmlName" Main/_Module/SubModule.xml` |
| 8 stylesheet-only registrations | `ls Main/_Module/ModuleData/*.xslt` |
| 8 `<XmlName>` rows in `TAOM_Map/SubModule.xml`, 33 in `LOTRLOME_Armory/SubModule.xml`, 8 of them `Monsters` | `grep -c "<XmlName"` on each live manifest; `grep -c 'XmlName id="Monsters"'` on the Armory's |
| 5 `<file>` rows in `Main/_Module/ModuleData/project.mbproj`; 9 `<Module>` and 0 `<file>` rows in `TAOM_Map/ModuleData/project.mbproj` | `grep -c "<file "` and `grep -c "<Module "` on each |
| 14 modules in `Configs/LauncherData.xml`, in the order quoted in section B | `python` with `xml.etree.ElementTree`, `SingleplayerData/ModDatas` |
| 3 hits for `GetSortedModules` (one definition, two multiplayer callers) | `grep -rn GetSortedModules` over the v1.4.8 decompile |
| 2 hits for `IsObsolete` (the read and `Hero.cs:1537`) | `grep -rn IsObsolete` over the v1.4.8 decompile |
| 4 `BROKEN_*` codes plus `LANDLESS_CULTURE` in the validator | `grep -o "BROKEN_[A-Z_]*\|LANDLESS_CULTURE" tools/validate_moduledata.py \| sort -u` |
| 494 vanilla settlements, 988 TAOM_Map settlements | `grep -cP "<Settlement\b"` on `SandBox/ModuleData/settlements.xml` and `TAOM_Map/ModuleData/settlements.xml` |
| 52 Erebor settlements (4 town, 9 castle, 16 village, 23 castle village), 13 clan-owned | `grep -oP '<Settlement id="[^"]*"[^>]*culture="Culture.erebor"'` on the live file, then `sort \| uniq -c` on the id prefix and on `owner=` |
| 6 files, 137 items in `LOTRLOME_items/erebor/`; 5 files, 168 items in `LOTRLOME_items/iron_hills/` | `ls` each folder; `cat <folder>/*.xml \| grep -cP "<Item\b"` |
| 66 troops, 65 `Culture.erebor`, 54 `<upgrade_target>`, 1666 item references (298 distinct), 17 `<EquipmentSet>` references in `troops/troops_erebor.xml` | `grep -cP "<NPCCharacter\b"`; `grep -c 'culture="Culture.erebor"'`; python `re` over the file |
| 17 rosters, 122 item references (30 distinct) in `equipmentsets/taom_equipment_sets_erebor.xml`; consumed 2 times from `taom_spcultures.xml` and 72 times from `characters/lords.xml` | `grep -c "<EquipmentRoster "`; python `re` with an id index built from `LOTRLOME_items/*` and `SandBoxCore/items` |
| 73 attributes on `<Culture id="erebor">` | python `re` over the first `<Culture>` element of `taom_spcultures.xml` |
| 21 Erebor party templates, 67 distinct troops, 63 in `troops_erebor.xml`, 4 in `npcs_erebor.xml` (74 entries) | `grep -o '<MBPartyTemplate id="[^"]*erebor[^"]*"'`; python `re` over those blocks; `grep -cP "<NPCCharacter\b"` on `npcs_erebor.xml` |
| 7 Erebor clans | `grep -nP 'id="clan_erebor'` on `characters/clans.xml` |
| 36 Erebor lords in `lords.xml`, 36 Erebor heroes in `heroes.xml`, 0 `lord_E` templates in either stylesheet | `grep -cP '<NPCCharacter\b[^>]*culture="Culture.erebor"'`; `grep -cP 'faction="Faction.clan_erebor_'`; `grep -c "lord_E[0-9]"` on both `.xslt` files |

## Read next

- [object-system-mbobjectmanager](../reference/engine/object-system-mbobjectmanager.md): the engine reference this chapter's sections A to C distil (its line numbers are v1.4.5; the ones above are v1.4.8).
- [xslt rule](../../.claude/rules/xslt.md): passthrough, inheritance, the child-union rule and the sentinel test.
- [lord-identity-reconciliation](../features/lord-identity-reconciliation.md): the per-attribute merge between `lords.xslt` and `characters/lords.xml`.
- [kingdom-creation](../features/kingdom-creation.md): the thirteen-step filing order behind section D.
- [culture-playability-wiring](../features/culture-playability-wiring.md): the fourteen-row playability checklist and what a binding fix does to a save.
- [lord-spawn-guard](../features/lord-spawn-guard.md), [settlement-building-levels](../features/settlement-building-levels.md), [lord-skills](../features/lord-skills.md), [clan-heraldry](../features/clan-heraldry.md), [ai-party-size](../features/ai-party-size.md): the sources of the four reload columns.
- [state-lifecycle-save lessons](../reviews/lessons/state-lifecycle-save.md) and [xslt-moduledata lessons](../reviews/lessons/xslt-moduledata.md): why the save rules and the strip rule exist.
- [lotrlome-soln-id-fix](../reference/lotrlome-soln-id-fix.md): the `project.mbproj` id vocabulary and the backup-extension rule.
- [moduledata-validation](../features/moduledata-validation.md) and [tools README](../../tools/README.md): the gates.

---

<!-- backlinks-start auto-generated; edit lint_docs.py / build_backlinks.py to change -->

## Referenced by

- [docs/INDEX.md](../INDEX.md)
- [docs/modding/clans.md](./clans.md)
- [docs/modding/file-catalogue.md](./file-catalogue.md)
- [docs/modding/id-cheatsheet.md](./id-cheatsheet.md)
- [docs/modding/items-weapons-and-crafting.md](./items-weapons-and-crafting.md)
- [docs/modding/lords-and-heroes.md](./lords-and-heroes.md)
- [docs/modding/module-dependencies.md](./module-dependencies.md)
- [docs/modding/module-map.md](./module-map.md)
- [docs/modding/module-taom.md](./module-taom.md)
- [docs/modding/modules-overview.md](./modules-overview.md)
- [docs/modding/README.md](./README.md)
- [docs/modding/recipe-add-a-kingdom.md](./recipe-add-a-kingdom.md)
- [docs/modding/recipe-new-mod-from-zero.md](./recipe-new-mod-from-zero.md)
- [docs/modding/submodule-and-registration.md](./submodule-and-registration.md)
- [docs/modding/wanderers-and-named-companions.md](./wanderers-and-named-companions.md)
- [docs/reference/doc-lookup.md](../reference/doc-lookup.md)

<!-- backlinks-end -->
