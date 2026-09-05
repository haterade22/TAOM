# Recipe: a new mod from zero

## What this file is

The ordered walkthrough from an empty `Modules` folder to a total conversion the size of TAOM.
Each stage names the file you create, the registration that makes the engine read it, the command
that proves it, and the chapter that owns the detail. It puts the other chapters in a build order
that keeps your work checkable at every stage; it does not restate them. That is an authoring
order, not the only order the engine accepts, and the next section says why the two differ.

## Before you start

- **You need the game and the Modding Kit.** The Kit is not a separate program, it is a second engine
  build under `bin/Win64_Shipping_wEditor`, launched from the launcher's Modding Kit entry. See
  [bannerlord-engine-and-toolchain](../reference/bannerlord-engine-and-toolchain.md).
- **You do not need C# to start.** Two of TAOM's four modules ship no code at all: both
  `TAOM_Map/SubModule.xml` and `LOTRLOME_Armory/SubModule.xml` carry a self-closing `<SubModules/>`,
  and the Armory folder has no `bin` directory at any level. This file lives in the game install,
  not the repo; a module reinstall reverts hand edits, so land a repo-side validator gate with any
  fix.
- **Set `BANNERLORD_GAME_DIR` only when you reach C#.** `build.ps1:11-15` reads the user-scope
  variable and exits 1 with "Please run setup-dev-env.ps1 first" when it is unset. `setup-dev-env.ps1`
  at the repo root sets it.
- **Read [modules-overview](modules-overview.md) and [editing-safely](editing-safely.md) first.**
  This chapter assumes you know what a module is and which copy of a file is the live one.

## What the Check lines prove, and what they do not

Every stage below ends in a `Check:` command. Those commands are TAOM's own gates, written to guard
TAOM's files, and most of them point at TAOM by default. Read this once before you take a green run
as proof that `<YourModule>` is sound.

- `python tools/validate_moduledata.py` defaults to `--moduledata Main/_Module/ModuleData`, TAOM's
  own folder (`tools/validate_moduledata.py:50`, `95`). Point it at yours and it does collect your
  items, troops and party templates and check references between them. Three things stay bound to
  TAOM's own names even then. Cultures are read from one filename, `taom_spcultures.xml`
  (`tools/taom_schema.py:1719`), and body properties from `TAOM_bodyproperties.xml` (`:1728`). The
  other modules it sweeps for definitions are a fixed list that does not include yours (`:1704-1711`).
  And the three schemas that drive duplicate-id and enum checking match TAOM's path shapes,
  `equipmentsets/*.xml`, `troops/troops_*.xml`, `characters/*.xml` and `taom_spcultures.xml`
  (`tools/schemas/`), so a file laid out differently is swept for references but never
  schema-checked.
- **Nothing here checks that your XML is well formed.** The validator works on text, not a parser
  (the only `ET.parse` in `tools/taom_schema.py` is line 1631, in the settlement-economy pass). Run
  the parser yourself: `python -c "import xml.etree.ElementTree as ET,pathlib;[ET.parse(f) for f in pathlib.Path('<game>/Modules/<YourId>/ModuleData').rglob('*.xml')]"`
- `python tools/check_external_xslt.py` checks the repo copy plus a hardcoded `TAOM_Map` and
  `LOTRLOME_Armory` (`tools/check_external_xslt.py:47`). `--moduledata` swaps the repo copy for
  yours; there is no flag that adds a third live module.
- `python tools/validate_mesh_refs.py` defaults to the Armory's item tree and its asset packs, and
  takes `--items` and `--tpac-modules` to look at yours instead.
- `python tools/audit_mbproj_registration.py --module <YourId>` is the one gate that takes a module
  id directly and needs nothing else.
- `python tools/audit_scene_names.py` takes no arguments at all and reads four hardcoded settlement
  files (`tools/audit_scene_names.py:25-31`). Stage 16 says what that leaves uncovered.

Nothing in this repo validates a new module end to end. Where a stage's check cannot reach your
files, the stage says so rather than implying it passed.

## Why the stages are in this order

Two separate orders are in play, and confusing them is the usual first mistake. The engine has one
fixed load sequence you cannot change. The stages below are an authoring order you can, and they are
arranged so that each piece is checkable before the next one names it.

The engine sequence, read from the v1.4.8 dump, is not the one most people guess.
`Campaign.InitializeDefaultCampaignObjects` runs first (reached at `Campaign.cs:1398` on a loaded
save and at `Campaign.cs:1524` on a new campaign). It loads `Monsters`, `SkeletonScales`,
`ItemModifiers`, `ItemModifierGroups`, `CraftingPieces`, `WeaponDescriptions`, `CraftingTemplates`,
`BodyProperties` and `SkillSets` (`Game.cs:437-445`), then `Items`, `EquipmentRosters` and
`partyTemplates` (`Campaign.cs:1471-1473`). Only after that does `InitializeBasicObjectXmls` load
`SPCultures` and `Concepts` (`Campaign.cs:1462-1463`, called at `Campaign.cs:1410`), and last come
`NPCCharacters`, `Heroes`, `Kingdoms`, `Factions`, `WorkshopTypes`, `LocationComplexTemplates` and
`Settlements` (`SandBoxManager.cs:362-380`). **Cultures load after the rosters and party templates
that name them, not before.**

That works because most references are forward-safe. A dotted attribute such as
`culture="Culture.erebor"` is read by `ReadObjectReferenceFromXml`, which hands the id to
`GetPresumedObject` (`MBObjectManager.cs:1497-1514`, `713-730`). If nothing has created that object
yet, and the type was registered with auto-create (the default, `MBObjectManager.cs:376`), the engine
makes an empty placeholder and the real file fills it in whenever it loads. Anything still empty at
the end of the load is dropped with a `Null object reference found with ID` line
(`MBObjectManager.cs:1437-1455`), which is a log entry rather than a crash. That is also why a typo
in a dotted reference gives you a running game with missing content instead of an error.

The exceptions are attributes read through a direct `GetObject` lookup, which simply returns null
when the target is absent: a culture's `<default_policies>` children (`CultureObject.cs:373`), a
troop's `<EquipmentSet id="...">` reference to a standalone roster, `base_monster` on a `<Monster>`.
Those do need their target loaded first, and each fails differently.
[load-order-and-dependencies](load-order-and-dependencies.md) has both tables and the full existence
ladder.

So follow the stage order for your own sake, not the engine's. What it buys you is that every stage
leaves something you can point a validator or a game launch at before the next stage depends on it.

Three loads are skipped when the player loads a save rather than starting a campaign: `Heroes`,
`Kingdoms` and `Factions` sit behind `if (!isSavedCampaign)` (`SandBoxManager.cs:363-374`). New
lords, clans and kingdoms therefore never appear in an existing save.

### The one engine rule that bites on day one

<!-- engine-ref type="TaleWorlds.ObjectSystem.MBObjectManager" file="Core/TaleWorlds.ObjectSystem/TaleWorlds.ObjectSystem/MBObjectManager.cs" lines="877-982" -->

| What you did | What the engine does | Where |
|---|---|---|
| Registered an id, shipped `<path>.xml` | adds that file as one merge entry | `MBObjectManager.cs:894-898` |
| Registered an id, shipped a `<path>/` folder instead | globs `*.xml` in it, one entry per file, not recursive | `MBObjectManager.cs:900-909` |
| Registered an id with neither on disk | adds the pair `("","")` and still queues the stylesheet | `MBObjectManager.cs:913-915` |
| Ended up first in merge order | your stylesheet never runs, the loop starts at `i = 1` | `MBObjectManager.cs:969` |
| Left an id with no contributor at all | `toBeMerged[0]` is read with no count check | `MBObjectManager.cs:968` |

The last two rows are the ones that cost a day. `GetMergedXmlForManaged` is called before the
`try` that wraps deserialization (`MBObjectManager.cs:789-796`), so a failure there is not swallowed,
and `CreateDocumentFromXmlFile` hands the empty path straight to `new StreamReader`
(`MBObjectManager.cs:1347`). An XSLT-only registration works only because some earlier module
contributed a real file for that id. That is why stage 2 exists.

## Stage 1. Make the folder and the manifest

Create `<game>/Modules/<YourId>/` and put one file in it, `SubModule.xml`. That single file is what
makes a directory a module: `ModuleHelper.GetPhysicalModules` enumerates the `Modules` directory and
`continue`s past any folder where `Path.Combine(dir, "SubModule.xml")` does not exist
(`ModuleHelper.cs:319-331`). No DLL, no `ModuleData`, no `bin` is required. On this install 15 of
the 19 module folders have a root manifest and are visible to the engine; the other four contain
only a `_Module` subfolder and are skipped.
<!-- measured: for d in "$MODULES"/*/; do [ -f "$d/SubModule.xml" ] && n=$((n+1)); done 2026-09-05 -->

Write the five scalars first. `Name`, `Id` and `Version` are dereferenced with no null check
(`ModuleInfo.cs:81-87`), so omitting any one of them throws while the manifest is being parsed and
the module never appears.

```xml
<Module>
	<Name value="TAOM_Map"/>
	<Id value="TAOM_Map"/>
	<Version value="v2.0.23" />
	<DefaultModule value="false" />
	<ModuleCategory value="Singleplayer"/>
	<ModuleType value="Community" />
```
<!-- excerpt file="TAOM_Map/SubModule.xml" -->

**Check:** `python -c "import xml.etree.ElementTree as ET; r=ET.parse('<game>/Modules/<YourId>/SubModule.xml').getroot(); print(r.find('Id').get('value'), r.find('Name').get('value'), r.find('Version').get('value'))"`
**Takes effect:** full game restart
**Code:** No code changes needed

## Stage 2. Declare the vanilla dependencies, and never load first

Add `Native`, `SandBoxCore`, `Sandbox` and `CustomBattle` to `<DependedModules>`. Match the declared
`<Id>`, not the folder name: the folder is `SandBox` but `SandBox/SubModule.xml` declares
`<Id value = "Sandbox"/>` on line 4, and `ModuleHelper.GetOfficialModuleIds` returns the lowercase-b
spelling (`ModuleHelper.cs:314-317`).

Two reasons this block matters. First, those vanilla modules are what give every id the campaign loads
at least one real contributor, which is what keeps `toBeMerged[0]` from being read out of an empty
list. `SandBox` alone contributes `partyTemplates`, `Heroes`, `Kingdoms`, `Factions`,
`WorkshopTypes`, `LocationComplexTemplates`, `Settlements` and `Concepts`. Second, loading after
vanilla is what lets your stylesheets run at all.

Vanilla registration counts, measured on this install: `Native` 24 `<XmlName>` rows, `SandBoxCore` 8,
`SandBox` 31, `CustomBattle` 2.
<!-- measured: grep -c '<XmlName ' "$MODULES/<mod>/SubModule.xml" 2026-09-05 -->

Do not copy a dependency block wholesale from TAOM_Map. Line 19 of `TAOM_Map/SubModule.xml` is
`<DependedModuleMetadata id="TAOM" order="LoadBeforeThis" />`, a TAOM-specific row with no
counterpart in `<DependedModules>`. A standalone map drops that row and changes three ids, not two.

**Check:** `python -c "import xml.etree.ElementTree as ET; print([d.get('Id') for d in ET.parse('<game>/Modules/<YourId>/SubModule.xml').getroot().find('DependedModules')])"`
**Takes effect:** full game restart
**Code:** No code changes needed

## Stage 3. Add ModuleData and register your first file

Create `ModuleData/<name>.xml`, then add the registration. `ModuleHelper.GetXmlPath` builds
`<module>/ModuleData/<path>.xml` (`ModuleHelper.cs:232-235`), so `path=` carries no extension and no
`ModuleData/` prefix, and a subdirectory goes inside the value.

```xml
    <XmlNode>
      <XmlName id="NPCCharacters" path="troops/troops_erebor"/>
      <IncludedGameTypes>
        <GameType value ="Campaign"/>
        <GameType value ="CampaignStoryMode"/>
        <GameType value = "CustomGame"/>
        <GameType value = "EditorGame"/>
      </IncludedGameTypes>
    </XmlNode>
```
<!-- excerpt file="Main/_Module/SubModule.xml" -->

The `id` must be one the engine actually calls `LoadXML` for; an id nobody loads is dead data. The
`<GameType value>` strings are the class names of `GameType` subclasses (`GameType.cs:40`), and an
absent or empty `<IncludedGameTypes>` means load everywhere, because the filter short-circuits on
`GameTypesIncluded.Count != 0` (`MBObjectManager.cs:884`).

Position matters inside your own manifest. `XmlResource.GetXmlListAndApply` appends each `<XmlNode>`
to one global list in document order (`XmlResource.cs:154-181`), and the merger walks that list, so a
stylesheet registered after your own data file transforms your own data too.

**Check:** `python tools/validate_moduledata.py --moduledata "<game>/Modules/<YourId>/ModuleData"`,
plus the well-formedness one-liner above, since the validator never parses your XML. Neither knows
whether the `id=` you registered is one the engine loads; a wrong id is silent, so compare it
against the lists in [submodule-and-registration](submodule-and-registration.md).
**Takes effect:** full game restart
**Code:** No code changes needed

## Stage 4. Change vanilla instead of adding to it

To rewrite vanilla entries rather than append to them, register the vanilla id with a path that has
only a `.xslt` on disk. `HandleXsltList` probes `<path>.xsl` first and falls back to `<path>.xslt`
(`MBObjectManager.cs:949-964`), and the merged document is the accumulation of every earlier module,
so the transform sees all of it. TAOM's whole vanilla-rewrite layer is eight such registrations:
`spkingdoms`, `spcultures`, `spclans`, `lords`, `heroes`, `module_strings`, `action_strings` and
`comment_strings`, each present as a `.xslt` with no `.xml` and no `.xsl` sibling.
<!-- measured: for n in spkingdoms spcultures spclans lords heroes module_strings action_strings comment_strings; do ls Main/_Module/ModuleData/$n.*; done 2026-09-05 -->

Position the passthrough by hand; there is no single place it always belongs. Start with one global
identity template that copies whatever you did not name
(`<xsl:copy><xsl:apply-templates select="@*|node()"/></xsl:copy>` on `match="@*|node()"`,
`Main/_Module/ModuleData/spcultures.xslt:6-10`). Inside a template that changes something, three
rules decide the order:

- **Copy the vanilla attributes first, then write your overrides.** The last write of an attribute
  name replaces the earlier one, so `<xsl:apply-templates select="@*"/>` goes at the top of the
  template and your `<xsl:attribute>` rows follow it (`spcultures.xslt:14-23`). Put the copy after
  your overrides and vanilla's value wins back. The flip side, spelled out in the file itself at
  `spcultures.xslt:33-38`: an attribute your block never names silently keeps its Calradian value.
- **Never emit attributes after a child element.** XSLT builds an element's attributes before its
  content, so once the first child is written an `<xsl:attribute>` is an error: the processor either
  reports it or silently drops the attribute, and you cannot rely on which.
- **Pass through only the children you did not replace, in the position the schema wants them.**
  TAOM's `empire` template emits its own caravan lists and then filters ten element names out of
  the child passthrough so vanilla's copies do not come back alongside them
  (`spcultures.xslt:304-317`).
  <!-- measured: sed -n '317p' Main/_Module/ModuleData/spcultures.xslt | grep -o 'self::[a-z_]*' | wc -l 2026-09-05 -->
  The live `LOTRLOME_Armory/ModuleData/weapon_descriptions.xslt:672` puts its passthrough in the
  middle of a child list, with hand-written entries on both sides.

An unconditional empty template deletes everything it matches, which is exactly how
`TAOM_Map/ModuleData/settlements.xslt` clears vanilla Calradia before its own settlements merge in.

**Check:** `python tools/check_external_xslt.py --moduledata "<game>/Modules/<YourId>/ModuleData"`
proves your stylesheets are well formed and compile. It cannot tell you the transform produced the
output you meant; only reading the merged result or a test over it does that.
**Takes effect:** full game restart
**Code:** No code changes needed

## Stage 5. Decide whether you need C# at all

Skip this stage for a data or map module. If you do want code, add `bin/Win64_Shipping_Client/` and a
`<SubModules>` block. `<Name>`, `<DLLName>` and `<SubModuleClassType>` are all dereferenced without a
guard (`SubModuleInfo.cs:51-65`). The DLL existence probe is hardcoded to `bin\Win64_Shipping_Client`
while the actual load uses the running build's folder (`Module.cs:1044`), so mirror your binaries
into the editor and server folders if you care about the Kit or a dedicated server.

Harmony, ButterLib, UIExtenderEx and MCM belong in their own module, loaded before yours. TAOM's
reasons and the version-pairing rules are in [module-dependencies](module-dependencies.md) and
[dr3-maintenance](../migration/dr3-maintenance.md).

**Check:** `./build.ps1 -RunTests` with the game closed
**Takes effect:** full game restart
**Code:** Code changes required in `<YourModule>/SubModule.cs`

## Stage 6. Register the native-side files through project.mbproj

Skins, action sets, action types, sounds and voice definitions are not read through `<Xmls>`. They
come from `ModuleData/project.mbproj`, a separate registry read one line before the `<Xmls>` list
(`Module.cs:1031-1032`). Its `name=` attribute is module-root-relative and carries the `.xml`
extension, the opposite convention from `<XmlName path>`.

```xml
	<file id="soln_voice_definitions" name="ModuleData/lotr_uruk_voice_def.xml" type="voice_definitions" />
	<file id="soln_module_sound" name="ModuleData/module_sounds.xml" type="module_sound" />
```
<!-- excerpt file="Main/_Module/ModuleData/project.mbproj" -->

Use only ids the engine asks for. `GetMergedXmlForNative` matches on exact string equality and is
reached from a fixed set of call sites, so an invented `soln_*` id opens no file and logs nothing;
the row reads exactly like working registration. The audit prints the vanilla vocabulary, 39 ids.
<!-- measured: python tools/audit_mbproj_registration.py 2026-09-05 -->

**Monsters are the exception, and they go both ways.** `Monster` is a managed object type: the
engine registers it as `RegisterType<Monster>("Monster", "Monsters", 2u)` (`Game.cs:307`) and reads
it with `ObjectManager.LoadXML("Monsters")` (`Game.cs:437`), so a monster meant for a race, a mount
or any spawned agent needs an `<XmlName id="Monsters" path="..."/>` row in `SubModule.xml` like any
other data file. That is the copy `FaceGen` looks up at runtime, through
`Game.Current.ObjectManager.GetObject<Monster>` (`FaceGen.cs:38`, `56`). The `soln_monsters` row in
`project.mbproj` feeds the separate native monster table instead. The live
`LOTRLOME_Armory/SubModule.xml` carries 8 `<XmlName id="Monsters">` rows (spider, elephant, mumakil,
chariot, warg, fell warg, war ram and the module's own `monsters.xml`) alongside 4 `soln_monsters`
rows in its `project.mbproj`, and a comment in that file records why: a `soln_` row that was removed
changed nothing, because the spider Monster loads the managed way.
<!-- measured: grep -c 'XmlName id="Monsters"' "$MODULES/LOTRLOME_Armory/SubModule.xml"; grep -c 'id="soln_monsters"' "$MODULES/LOTRLOME_Armory/ModuleData/project.mbproj" 2026-09-05 -->
Register only through `project.mbproj` and your race's monsters never become objects, which shows up
as a null reference while a character or agent is being built rather than as a load error.

**Check:** `python tools/audit_mbproj_registration.py --module <YourId>` covers the `project.mbproj`
half for your module. Nothing audits the `<XmlName id="Monsters">` half; confirm by launching and
spawning the creature.
**Takes effect:** full game restart
**Code:** No code changes needed

## Stage 7. Text, before anything that references it

Player-facing text comes first because every later file names a string key. The `Languages` tree is
discovered, not registered: `LocalizedTextManager` does a recursive `GetFiles(module +
"/ModuleData/Languages", "language_data.xml", SearchOption.AllDirectories)` (`LocalizedTextManager.cs:99`)
and this runs at `Module.Initialize` before submodules load (`Module.cs:262`).

There is no English folder. `LoadLanguage` sets `flag = stringId != "English"` and only deserializes
`<string>` rows when that is true (`LocalizedTextManager.cs:235`), so the inline `{=KEY}Default text`
in your data file is the English text. TAOM's root anchor is three lines and declares no
`<LanguageFile>` at all, beside 12 translated language folders.
<!-- measured: ls -d Main/_Module/ModuleData/Languages/*/ | wc -l 2026-09-05 -->
Chapter: [strings-and-localization](strings-and-localization.md); translator workflow,
[TRANSLATOR_GUIDE](../localization/TRANSLATOR_GUIDE.md).

**Check:** `python tools/validate_moduledata.py --moduledata "<game>/Modules/<YourId>/ModuleData"`
**Takes effect:** full game restart
**Code:** No code changes needed

## Stage 8. First art, and the item files that name it

Import in the Modding Kit with the editor open and otherwise idle, textures first and meshes
second, because slots bind by name. Then read the authoritative mesh names out of the written tpac
rather than trusting an editor label. The rule about a closed editor is a different one and does
not apply here: the editor scans resources only at startup, so a tpac written by a script outside
the Kit has to land while the Kit is shut
([ue-to-bannerlord-asset-pipeline](../reference/ue-to-bannerlord-asset-pipeline.md), lines 36-37).

Item files go in a folder and the folder is registered once, because the engine globs `*.xml`
inside a registered directory (`MBObjectManager.cs:900-909`). Adding a new `.xml` to an already
registered folder needs no manifest edit; adding a new folder does.

TAOM's armour lives in 18 folders under `LOTRLOME_Armory/ModuleData/LOTRLOME_items/`, each with its
own `<XmlName id="Items" path="LOTRLOME_items/<folder>"/>` row.
<!-- measured: find "$MODULES/LOTRLOME_Armory/ModuleData/LOTRLOME_items" -maxdepth 1 -type d | tail -n +2 | wc -l 2026-09-05 -->
That folder set does not cover every TAOM culture, and vanilla items are live alongside it, so "the
culture folder" is not always the answer. A misspelled item id in a roster neither crashes nor logs;
only a tool catches it. Chapters: [items-armor](items-armor.md),
[items-weapons-and-crafting](items-weapons-and-crafting.md), [items-shields](items-shields.md),
[items-mounts-and-harness](items-mounts-and-harness.md), [module-armory](module-armory.md).

**Check:** `python tools/validate_mesh_refs.py --scan-bodies --items "<game>/Modules/<YourId>/ModuleData"
--tpac-modules <YourId> Native SandBoxCore`. Without the two flags it audits the Armory, not you.
**Takes effect:** full game restart
**Code:** No code changes needed

## Stage 9. First troop tree

One `<NPCCharacter>` per rung in `ModuleData/troops/troops_<culture>.xml`, each rung pointing at the
next through `<upgrade_targets>`. `troops_erebor.xml` holds 67 of them.
<!-- measured: grep -c "<NPCCharacter" Main/_Module/ModuleData/troops/troops_erebor.xml 2026-09-05 -->
The tree is inert until a culture binds its root and a party template spawns it, which is stages 11
and 12. Chapter: [troops](troops.md).

**Recruitment needs no code.** Vanilla hands out volunteers from the culture itself:
`DefaultVolunteerModel.GetBasicVolunteer` returns `sellerHero.Culture.EliteBasicTroop` for a rural
notable bound to a castle and `sellerHero.Culture.BasicTroop` otherwise
(`DefaultVolunteerModel.cs:111-118`), and an AI minor faction recruits its own
`ActualClan.BasicTroop` (`RecruitmentCampaignBehavior.cs:322`). A valid `basic_troop` and
`elite_basic_troop` on your culture, plus a clan basic troop, are enough. TAOM's 16 partial
`VolunteerRecruitmentService.<Culture>.cs` files exist because TAOM wanted weighted
per-settlement, per-clan and per-culture pools, not because the engine needs them:
`TaomVolunteerModel.GetBasicVolunteer` calls `base.GetBasicVolunteer` whenever its service
returns nothing (`Main/Features/TroopProgression/Models/TaomVolunteerModel.cs:55-67`).

**Check:** `python tools/validate_moduledata.py --code BROKEN_TROOP_REF --code UPGRADE_TIER_COLLAPSE
--moduledata "<game>/Modules/<YourId>/ModuleData"`
**Takes effect:** full game restart
**Code:** No code changes needed

## Stage 10. Equipment rosters for those troops

Rosters are their own object type, `EquipmentRosters`, loaded after `Items` and before
`partyTemplates` (`Campaign.cs:1471-1473`). Put them under `ModuleData/equipmentsets/` and register
each file; TAOM ships 32 files there and 27 `EquipmentRosters` registrations.
<!-- measured: ls Main/_Module/ModuleData/equipmentsets/ | wc -l; grep -o 'id="EquipmentRosters"' Main/_Module/SubModule.xml | wc -l 2026-09-05 -->
Note the path shape: they are under `equipmentsets/`, not at the `ModuleData` root. Chapter:
[equipment-rosters](equipment-rosters.md).

**Check:** `python tools/validate_moduledata.py --code BROKEN_ITEM_REF --code DUPLICATE_ROSTER_ID
--moduledata "<game>/Modules/<YourId>/ModuleData"`
**Takes effect:** full game restart
**Code:** No code changes needed

## Stage 11. The culture

Almost everything else names a `Culture.` reference, which makes the culture the hinge of the whole
build even though `SPCultures` loads well down the engine's sequence (`Campaign.cs:1462`, after the
items, rosters and party templates it points at). TAOM's `taom_spcultures.xml` carries 24
`<Culture>` blocks; the `erebor` block carries 73 attributes and 18 kinds of child element.
<!-- measured: python -c "import xml.etree.ElementTree as ET; r=ET.parse('Main/_Module/ModuleData/taom_spcultures.xml').getroot(); print(len(r)); c=[x for x in r if x.get('id')=='erebor'][0]; print(len(c.attrib), len({x.tag for x in c}))" 2026-09-05 -->

Which of those attributes a playable culture must carry, and the 14-row fatal-or-silent checklist
that decides whether it is playable at all, are in [cultures.md](cultures.md) and
[culture-playability-wiring](../features/culture-playability-wiring.md). Row 11 of that checklist is
the one that catches new modders: a culture owning no settlement crashes on the daily clan tick,
inside vanilla code with no frame of yours on the stack.

**Check:** `python tools/validate_moduledata.py --code UNKNOWN_CULTURE --code LANDLESS_CULTURE
--moduledata "<game>/Modules/<YourId>/ModuleData"`. Both codes read the culture registry, which is
the one filename-bound case from the section above: name your culture file anything but
`taom_spcultures.xml` and the two codes have nothing to compare against.
**Takes effect:** full game restart
**Code:** No code changes needed

## Stage 12. The twelve party templates

Nothing spawns without them. Villagers, caravans, militia, patrols, rebels, bandits and every lord
party come from `MBPartyTemplate` entries, and the culture binds them by attribute. TAOM's
`taom_partyTemplates.xml` holds 383 templates, 21 of them named for erebor.
<!-- measured: grep -c "<MBPartyTemplate " Main/_Module/ModuleData/taom_partyTemplates.xml; grep -o '<MBPartyTemplate id="[^"]*erebor[^"]*"' Main/_Module/ModuleData/taom_partyTemplates.xml | wc -l 2026-09-05 -->

An unbound template is silent: the culture just fields Calradians. A null or empty one is a crash in
vanilla `SpawnPatrolParty` or `SpawnCaravan`. Which twelve a culture needs is row 13 of the
playability checklist in [culture-playability-wiring](../features/culture-playability-wiring.md); the
binding contract and the min/max semantics are in [party-templates](party-templates.md) and
[party-template-sizing](../reference/party-template-sizing.md).

**Check:** `dotnet test TAOM.Tests --filter CulturePartyTemplate -p:DisableModuleCopy=true -p:ModuleId=`
proves TAOM's twelve bindings, not yours: the fixture walks up the tree for `Main/_Module/ModuleData`
and there is no way to point it elsewhere (`TAOM.Tests/Core/CultureDataFixture.cs:56-64`). For your
module, read the twelve binding attributes off your `<Culture>` block against the list in
[party-templates](party-templates.md) and confirm each id exists in your template file.
**Takes effect:** full game restart
**Code:** No code changes needed

## Stage 13. Notables, townsfolk and wanderers

Settlements need people to offer quests, sell goods and hand out volunteers. These are ordinary
`<NPCCharacter>` rows in `ModuleData/characters/npcs_<culture>.xml` plus the wanderer files. Shipped
files vary in size, so do not treat any one of them as the required set; the per-slot table lives in
[npcs-notables-and-townsfolk](npcs-notables-and-townsfolk.md) and
[wanderers-and-named-companions](wanderers-and-named-companions.md). Wanderer counts differ per
culture in TAOM's own data, so trust the file rather than any doc quoting a single number.

**Check:** `python tools/validate_moduledata.py --code DUPLICATE_NPC_ID --code BROKEN_BODY_PROPERTY_REF
--moduledata "<game>/Modules/<YourId>/ModuleData"`. `BROKEN_BODY_PROPERTY_REF` is the other
filename-bound case: it reads `TAOM_bodyproperties.xml` and nothing else.
**Takes effect:** new campaign only
**Code:** No code changes needed

## Stage 14. Clans, then lords, then heroes

The order inside this stage is fixed by the references. A `<Faction>` names its owner hero, its
culture, its kingdom and its home settlement; a lord is two rows, an `<NPCCharacter>` in
`characters/lords.xml` and a `<Hero>` of the same id in `characters/heroes.xml`. Every one of TAOM's
1001 `<Hero>` rows has a same-id `<NPCCharacter>`: 984 in `characters/lords.xml` and 17 in
`named_companions/named_companions.xml`, none unpaired.
<!-- measured: python -c "import xml.etree.ElementTree as ET,glob; h={c.get('id') for c in ET.parse('Main/_Module/ModuleData/characters/heroes.xml').getroot()}; l={c.get('id') for c in ET.parse('Main/_Module/ModuleData/characters/lords.xml').getroot()}; n=set(); [n.update(c.get('id') for c in ET.parse(f).getroot()) for f in glob.glob('Main/_Module/ModuleData/named_companions/*.xml')]; print(len(h), len(h&l), len(h&n), len(h-l-n))" 2026-09-05 -->

A `<Hero>` with no matching character entry is a documented crash class; see
[lords-and-heroes](lords-and-heroes.md) and the Known Crashes section of
[kingdom-creation](../features/kingdom-creation.md).

**Check:** `dotnet test TAOM.Tests --filter CultureLordTemplate -p:DisableModuleCopy=true -p:ModuleId=`
reads TAOM's `Main/_Module/ModuleData` and cannot be retargeted
(`TAOM.Tests/Core/CultureDataFixture.cs:56-64`). For your module the check that matters is the set
comparison in the marker above, with the two paths swapped: every `id` in your `<Hero>` file must
also appear as an `<NPCCharacter>` id.
**Takes effect:** new campaign only
**Code:** No code changes needed

## Stage 15. The kingdom

Last of the character-side files, because it names a culture that must exist, an owner hero that must
exist, and an `initial_home_settlement` that must exist. `Kingdoms` and `Factions` both load only on
a new campaign (`SandBoxManager.cs:371-375`). Chapters: [kingdoms](kingdoms.md),
[recipe-add-a-kingdom](recipe-add-a-kingdom.md), and
[banners-and-heraldry](banners-and-heraldry.md) for the `banner_key` grammar every kingdom row
needs. The 13-step filing order TAOM follows for a whole new realm, strings first and
character-creation JSON last, is `## Filing Order` in
[kingdom-creation](../features/kingdom-creation.md); the id patterns are its `## Naming Conventions`
table, distilled in [id-cheatsheet](id-cheatsheet.md).

**Check:** `python tools/validate_moduledata.py --moduledata "<game>/Modules/<YourId>/ModuleData"`
**Takes effect:** new campaign only
**Code:** No code changes needed

## Stage 16. The map module and its settlements

A campaign map is its own module with no code. The scene must be named `Main_map`, one named
`game_entity` must exist in it per settlement id, and the entity is what the settlement data binds
to. Settlement data with no scene entity crashes at map load. Then paint the navmesh, import the two
`world_map` grid textures losslessly, and rebuild the distance cache from a loaded campaign rather
than from the editor button.

`TAOM_Map/ModuleData/settlements.xml` holds 988 `<Settlement>` rows, 52 of them
`culture="Culture.erebor"`.
<!-- measured: grep -c "<Settlement " "$MODULES/TAOM_Map/ModuleData/settlements.xml"; grep -c 'culture="Culture.erebor"' "$MODULES/TAOM_Map/ModuleData/settlements.xml" 2026-09-05 -->
Chapters: [settlements](settlements.md), [module-map](module-map.md),
[worldmap-battle-scene-grid](../reference/worldmap-battle-scene-grid.md),
[editor-cache-rebuild](../features/editor-cache-rebuild.md).

**Check:** there is no gate for the crash this stage warns about, and running one that looks like
it covers it is worse than running none. `python tools/audit_scene_names.py` takes no arguments,
reads four hardcoded settlement files and compares their interior `scene_name` values against
`SceneObj` folders (`tools/audit_scene_names.py:25-31`, `84-100`). It never opens your settlement
file and it never looks at `Main_map` entities, so it can print a clean report immediately before
the map-load crash. What actually proves this stage: extract the `id` of every `<Settlement>` in
your file and confirm each one appears as an entity name in your `Main_map/scene.xscene`, then run
`python tools/audit_scene_names.py` separately for the interior scenes it does cover.
**Takes effect:** new campaign only
**Code:** No code changes needed

## Stage 17. A new race, if you need one

Three files in one order: append the `<race>` block at the end of `skins.xml` (race ints are
merge-order indices, so inserting renumbers every hero in every save); add the five `<Monster>`
rows and register them with an `<XmlName id="Monsters">` row, which is the managed half stage 6
describes; then author `as_<race>_facegen` and `as_<race>_female_facegen` by copying an existing
pair verbatim and renaming only `id` and `base_set`. A slim facegen set renders the first character-creation menu
and breaks every later stage. `skins.xml` has no managed deserializer in the shipping-client dump,
so its attribute meanings cannot be read from the decompile; the working blocks in the live file are
the reference. Chapters:
[recipe-add-a-race-or-creature](recipe-add-a-race-or-creature.md),
[body-properties](body-properties.md), [hero-race](../features/hero-race.md).

**Check:** `python tools/audit_action_set_parity.py`
**Takes effect:** full game restart
**Code:** No code changes needed

## Stage 18. Version, validate, package

`<Version>` in the manifest is the only link from a player's crash report back to a commit, so change
it only in a release commit and tag that commit. Sweep backups before packaging: a `.bak` file whose
name still ends in `.xml` is loaded by the folder glob, which is why retired files take a
non-`.xml` extension.

The full ordered gate sequence is [validation-and-testing](validation-and-testing.md). The release
contract is [release-process](../reference/release-process.md).

**Check:** `python tools/validate_moduledata.py --moduledata "<game>/Modules/<YourId>/ModuleData"`,
then `pwsh tools/sweep_module_backups.ps1`, then
`python tools/package_release.py --source "<game>/Modules" --dest <out> --dry-run`
**Takes effect:** full game restart
**Code:** No code changes needed

## Worked example: the Erebor chain

Erebor is one culture built the whole way up; every id below existed before the next row named it.

| Stage | What was created | Id | Where it lives |
|---|---|---|---|
| 3 | registration | `NPCCharacters` / `troops/troops_erebor` | `Main/_Module/SubModule.xml:199` |
| 8 | armour folders | `LOTRLOME_items/erebor`, `LOTRLOME_items/iron_hills` | Armory manifest lines 83 and 145 |
| 9 | troop tree | `erebor_reg_miner` up to `erebor_noble` | `Main/_Module/ModuleData/troops/troops_erebor.xml` |
| 10 | rosters | `taom_equipment_sets_erebor` | `Main/_Module/ModuleData/equipmentsets/` |
| 11 | culture | `erebor` | `Main/_Module/ModuleData/taom_spcultures.xml` |
| 12 | party templates | 21 ids containing `erebor` | `Main/_Module/ModuleData/taom_partyTemplates.xml` |
| 14 | clan and lord pair | `clan_erebor_1`, `lord_E1_1` | `characters/clans.xml`, `characters/lords.xml`, `characters/heroes.xml` |
| 15 | kingdom | `erebor` | `Main/_Module/ModuleData/taom_spkingdoms.xml` |
| 16 | settlements | `town_E1` and the other E-prefixed ids | `TAOM_Map/ModuleData/settlements.xml` |

The clan row is the reference hub. Five of its attributes name something an earlier stage had to
create:

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
		default_party_template="PartyTemplate.kingdom_hero_party_erebor_erebor_1_template" />
```

1. `culture="Culture.erebor"` needs stage 11. Wrong or missing and the clan is Calradian.
2. `owner="Hero.lord_E1_1"` needs stage 14, both halves of the pair.
3. `initial_home_settlement="Settlement.town_E1"` needs stage 16, in the map module.
4. `default_party_template="PartyTemplate.kingdom_hero_party_erebor_erebor_1_template"` needs stage
   12. This is the party the clan's lords field.
5. `super_faction="Kingdom.erebor"` needs stage 15, which is written after the clan even though it is
   referenced here; `Kingdoms` loads before `Factions` (`SandBoxManager.cs:373-374`).

The hero half of a lord is short. This one carries four attributes and nothing else:

<!-- example file="Main/_Module/ModuleData/characters/heroes.xml" id="lord_E1_2" -->
```xml
	<Hero
		id="lord_E1_2"
		father="Hero.lord_E1_1"
		mother="Hero.lord_E1_6"
		faction="Faction.clan_erebor_1" />
```

And the settlement the clan calls home, one line plus its components:

<!-- example file="TAOM_Map/ModuleData/settlements.xml" id="town_E1" -->
```xml
  <Settlement id="town_E1" name="{=Settlements.Settlement.name.town_E1}Erebor" owner="Faction.clan_erebor_1" posX="944.305" posY="1197.995" culture="Culture.erebor" gate_posX="944.689" gate_posY="1198.695">
```

Note the loop: the clan names the settlement and the settlement names the clan. Both resolve because
`Factions` loads before `Settlements`, and because a reference to something not yet loaded gets a
presumed placeholder object instead of failing on the spot (`MBObjectManager.cs:713-735`). Anything
still unresolved at the end is dropped with a `Null object reference found with ID` line
(`MBObjectManager.cs:1437-1455`), which is a log entry, not a crash.

## What TAOM never wrote down

Real gaps, not omissions from this chapter. Do not expect a repo doc to answer them.

- **The Modding Kit's New Module wizard.** No doc records the fields it asks for or the stub files it
  emits. The nearest evidence is the result: `TAOM_Map/ModuleData/` contains 16 untouched stub XMLs
  (`action_sets`, `action_types`, `collision_infos`, `combat_parameters`, `face_animations`,
  `item_holsters`, `items`, `native_parameters`, `partyTemplates`, `physics_materials`, `skins`,
  `spclans`, `spcultures`, `spkingdoms`, `spnpccharacters`, `spworkshops`), every one of them either
  a `replace_this_with_actual_nodes` placeholder or an empty root.
  <!-- measured: for f in action_sets action_types collision_infos combat_parameters face_animations item_holsters items native_parameters partyTemplates physics_materials skins spclans spcultures spkingdoms spnpccharacters spworkshops; do grep -q replace_this_with_actual_nodes "$f.xml" || [ $(wc -c < "$f.xml") -lt 400 ] && echo "$f"; done | wc -l 2026-09-05 -->
- **Painting a navmesh for a land map.** The only tile numbers TAOM records are the six water-framed
  ones in [warsails-custom-map-guide](../warsails-custom-map-guide.md) (shores, shallow and deep
  ocean, under bridges, rivers, holes). Land painting is undocumented, as is the face-group to
  terrain-type mapping beyond one line in
  [worldmap-battle-scene-grid](../reference/worldmap-battle-scene-grid.md).
- **Heightmap authoring and import.** `TAOM_Map/AssetSources/Support/` holds five plausible height
  sources (`Final Height.png`, `GAEA.png`, `GAEA02.png`, `heightmap_support.png`,
  `terrain_heightmap.png`); which is live, at what resolution, and how it reached the Terrain node is
  recorded nowhere. <!-- measured: ls "$MODULES/TAOM_Map/AssetSources/Support/" 2026-09-05 -->
- **What a settlement scene entity must carry.** Which scene tags and child entities are mandatory
  per settlement kind is written down nowhere. Read the working entities in
  `TAOM_Map/SceneObj/Main_map/scene.xscene` rather than inventing a set.
- **Two files that nothing loads.** `TAOM_Map/ModuleData/settlement_tracks.xml` (7,390 bytes of real
  `<MusicTracks>` rows) and `settlement_track_instruments.xml` (3,346 bytes of `<MusicInstruments>`)
  appear in neither that module's eight `<XmlName>` rows nor its `project.mbproj`.
  <!-- measured: wc -c "$MODULES/TAOM_Map/ModuleData/settlement_tracks.xml" "$MODULES/TAOM_Map/ModuleData/settlement_track_instruments.xml"; grep -c '<XmlName ' "$MODULES/TAOM_Map/SubModule.xml" 2026-09-05 -->
- **Which TAOM service loads each code-loaded directory.** `Main/_Module/ModuleData/` has 42
  subdirectories; only 4 are reachable through an `<XmlName path>` prefix (`characters`,
  `equipmentsets`, `named_companions`, `troops`), leaving 38 that are read by TAOM's own C# with no
  per-directory index anywhere.
  <!-- measured: python -c "import os,re; d=[x for x in os.listdir('Main/_Module/ModuleData') if os.path.isdir('Main/_Module/ModuleData/'+x)]; pre={p.split('/')[0] for p in re.findall(r'path=\"([^\"]+)\"', open('Main/_Module/SubModule.xml').read()) if '/' in p}; print(len(d), len([x for x in d if x in pre]), len([x for x in d if x not in pre]))" 2026-09-05 -->
  One worked case: `Main/Features/BattleBalance/BattleBalanceConfigProvider.cs:29` builds
  `Path.Combine(_pathService.ModuleDataPath, "configs", "battle_balance_config.json")`. Every other
  directory needs the same kind of grep.
- **The four alias stub modules.** They are deployed to `Modules/<Id>/_Module/SubModule.xml`, one
  level below where `ModuleHelper.GetPhysicalModules` looks (`ModuleHelper.cs:327-330`), so the
  vanilla engine cannot see them. Whether any launcher enumerates them is unanswered. Do not repeat
  the claim that they auto-tick in the vanilla launcher.
- **TAOM's own dependency pin.** `Main/_Module/SubModule.xml` lists four `<DependedModule>` rows and
  none of them is `TAOM.Dependencies`, while the comment above them describes that pin as present and
  [release-process](../reference/release-process.md) still documents the paired metadata row.
  Whether the removal was deliberate is unresolved.

## Numbers in this chapter

Measured 2026-09-05 against the installed v1.4.8 game and this repo. `$MODULES` is the install's `Modules` folder; every other command runs from the repo root.

| Number | Command |
|---|---|
| 19 module folders, 15 with a root `SubModule.xml` | `ls -d "$MODULES"/*/ \| wc -l`; `for d in "$MODULES"/*/; do [ -f "$d/SubModule.xml" ] && echo "$d"; done \| wc -l` |
| `<XmlName>` rows: Native 24, SandBoxCore 8, SandBox 31, CustomBattle 2, TAOM 100, TAOM_Map 8, LOTRLOME_Armory 33 | `grep -c '<XmlName ' "$MODULES/<mod>/SubModule.xml"` |
| TAOM's 100 rows cover 12 distinct ids | `grep -o '<XmlName id="[^"]*"' Main/_Module/SubModule.xml \| sort -u \| wc -l` |
| 8 XSLT-only registrations, each a `.xslt` with no `.xml` or `.xsl` sibling | `ls Main/_Module/ModuleData/{spkingdoms,spcultures,spclans,lords,heroes,module_strings,action_strings,comment_strings}.*` |
| 17 stylesheets across the three live modules, all clean | `python tools/check_external_xslt.py` |
| 39 vanilla `soln_*` ids | `python tools/audit_mbproj_registration.py` |
| Armory `<XmlName id="Monsters">` rows 8, `soln_monsters` rows 4 | `grep -c 'XmlName id="Monsters"' "$MODULES/LOTRLOME_Armory/SubModule.xml"`; `grep -c 'id="soln_monsters"' "$MODULES/LOTRLOME_Armory/ModuleData/project.mbproj"` |
| 10 child element names filtered out of the `empire` passthrough in `spcultures.xslt` | `sed -n '317p' Main/_Module/ModuleData/spcultures.xslt \| grep -o 'self::[a-z_]*' \| wc -l` |
| TAOM `project.mbproj`: 5 `<file>` rows | `grep -c '<file ' Main/_Module/ModuleData/project.mbproj` |
| 12 translated language folders, a 3-line English anchor | `ls -d Main/_Module/ModuleData/Languages/*/ \| wc -l`; `wc -l < Main/_Module/ModuleData/Languages/language_data.xml` |
| 42 subdirectories under `Main/_Module/ModuleData/`, 4 reached by a `path=` prefix, 38 not | the python one-liner in the marker beside that bullet below |
| 18 item folders under `LOTRLOME_items/`, erebor 6 files, iron_hills 5 | `find "$MODULES/LOTRLOME_Armory/ModuleData/LOTRLOME_items" -maxdepth 1 -type d \| tail -n +2 \| wc -l`; `ls .../erebor \| wc -l` |
| 24 `<Culture>` blocks, erebor with 73 attributes and 18 child element kinds | `python -c "import xml.etree.ElementTree as ET; r=ET.parse('Main/_Module/ModuleData/taom_spcultures.xml').getroot(); print(len(r)); c=[x for x in r if x.get('id')=='erebor'][0]; print(len(c.attrib), len({x.tag for x in c}))"` |
| 67 `<NPCCharacter>` rows in `troops_erebor.xml`, 16 troop files | `grep -c "<NPCCharacter" Main/_Module/ModuleData/troops/troops_erebor.xml`; `ls Main/_Module/ModuleData/troops/*.xml \| wc -l` |
| 32 files under `equipmentsets/`, 27 `EquipmentRosters` registrations | `ls Main/_Module/ModuleData/equipmentsets/ \| wc -l`; `grep -c 'id="EquipmentRosters"' Main/_Module/SubModule.xml` |
| 383 `<MBPartyTemplate>` rows, 21 named for erebor | `grep -c "<MBPartyTemplate " Main/_Module/ModuleData/taom_partyTemplates.xml`; `grep -o '<MBPartyTemplate id="[^"]*erebor[^"]*"' Main/_Module/ModuleData/taom_partyTemplates.xml \| wc -l` |
| 1001 `<Hero>` rows, 984 paired in `lords.xml`, 17 in `named_companions.xml`, 0 unpaired | the python one-liner in the marker at stage 14 |
| 988 `<Settlement>` rows in the live map, 52 with `culture="Culture.erebor"` | `grep -c "<Settlement " "$MODULES/TAOM_Map/ModuleData/settlements.xml"`; `grep -c 'culture="Culture.erebor"' "$MODULES/TAOM_Map/ModuleData/settlements.xml"` |
| 16 of 16 named `TAOM_Map` Kit files are stubs | `for f in action_sets action_types collision_infos combat_parameters face_animations item_holsters items native_parameters partyTemplates physics_materials skins spclans spcultures spkingdoms spnpccharacters spworkshops; do grep -q replace_this_with_actual_nodes "$f.xml" \|\| [ $(wc -c < "$f.xml") -lt 400 ] && echo "$f"; done \| wc -l` |
| `settlement_tracks.xml` 7,390 bytes, `settlement_track_instruments.xml` 3,346 bytes, neither in the 8 registrations | `wc -c "$MODULES/TAOM_Map/ModuleData/settlement_track*.xml"`; `grep -c '<XmlName ' "$MODULES/TAOM_Map/SubModule.xml"` |

## Read next

- [modules-overview](modules-overview.md), [submodule-and-registration](submodule-and-registration.md),
  [load-order-and-dependencies](load-order-and-dependencies.md)
- [module-taom](module-taom.md), [module-map](module-map.md), [module-armory](module-armory.md),
  [module-dependencies](module-dependencies.md)
- [recipe-add-a-culture](recipe-add-a-culture.md), [recipe-add-a-kingdom](recipe-add-a-kingdom.md),
  [recipe-add-a-race-or-creature](recipe-add-a-race-or-creature.md),
  [recipe-retire-content](recipe-retire-content.md)
- [validation-and-testing](validation-and-testing.md), [balance-levers](balance-levers.md),
  [troubleshooting](troubleshooting.md), [file-catalogue](file-catalogue.md)
- [kingdom-creation](../features/kingdom-creation.md),
  [culture-playability-wiring](../features/culture-playability-wiring.md),
  [new-culture-authoring](../ai-includes/new-culture-authoring.md), [tools README](../../tools/README.md)

---

<!-- backlinks-start auto-generated; edit lint_docs.py / build_backlinks.py to change -->

## Referenced by

- [docs/INDEX.md](../INDEX.md)
- [docs/modding/modules-overview.md](./modules-overview.md)
- [docs/modding/README.md](./README.md)
- [docs/reference/doc-lookup.md](../reference/doc-lookup.md)

<!-- backlinks-end -->
