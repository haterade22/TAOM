# The Armory module: LOTRLOME_Armory

## What this module is

`LOTRLOME_Armory` is TAOM's art-and-items module: the armour, weapons, shields, mount items, crafting pieces, race skins, monster definitions and animation sets that TAOM did not inherit from vanilla all live here. It ships no C# at all, because its `<SubModules/>` element is empty and self-closing, so everything it contributes is XML plus the packed art that XML names. It is the module a 3D artist spends most of their time in, and it is the only one of TAOM's four modules that exists nowhere in the repo.

## Where it lives, and the warning that governs every edit

The module root is `LOTRLOME_Armory/`, with exactly one top-level file, `LOTRLOME_Armory/SubModule.xml`, beside nine directories. This file lives in the game install, not the repo; a module reinstall reverts hand edits, so land a repo-side validator gate with any fix.

Three facts have to be held together, because any two of them alone give the wrong answer.

- **Your edits reach players.** `tools/package_release.py:53` sets `DEFAULT_MODULES = ("TAOM", "TAOM_Map", "LOTRLOME_Armory", "TAOM.Dependencies")`, so the Armory is packaged with every release. "Unversioned" means untracked in git, never undelivered. A fix belongs in the Armory's own file beside the cultures already there, not in a TAOM-side override.
- **A module refresh silently reverts your edits.** There is no git history to recover from and no error when the file goes back to its shipped state. The repo's answer is a three-part guard: an idempotent replay script that writes between markers, an in-repo gate that fails when the edit is gone, and an "APPLIED EDIT" block in [`docs/reference/lotrlome-armory-snapshot/README.md`](../reference/lotrlome-armory-snapshot/README.md). The worked case is `tools/register_one_handed_polearms.py` plus `tools/audit_polearm_shield_parity.py`.
- **Eight files have a tracked snapshot, and it is storage only.** `docs/reference/lotrlome-armory-snapshot/` holds copies of `action_sets.xml`, `action_types.xml`, `monsters.xml`, `skins.xml`, `monster_usage_sets.xml`, `monster_usage_sets.xslt`, `project.mbproj` and `weapon_descriptions.xslt`. That README's "Loading status" section forbids ever registering those copies from TAOM's own `SubModule.xml`, because the same ids would then load twice.

## Folder by folder

<!-- measured: ls -1 "<game>/Modules/LOTRLOME_Armory" 2026-09-05 -->

| Entry | What it holds | Needed at boot |
|---|---|---|
| `SubModule.xml` | The managed manifest: identity, four dependencies, an empty `<SubModules/>`, and 33 `<XmlNode>` registrations. 354 lines. | yes |
| `ModuleData/` | Every XML: `project.mbproj`, the item tree, races and animation data, cloth, physics, collision, sounds, `Languages/`. | yes |
| `Assets/` | The loose `.tpac` tree the engine actually loads. 38 top-level folders, 4,364 tpac files. | yes |
| `AssetSources/` | The FBX and PNG sources the Modding Kit imports from. 39 folders, 693 FBX, 2,746 PNG. Excluded from release packaging. | no |
| `ModuleSounds/` | The `.wav` banks `module_sounds.xml` names, paths relative to this folder. | no |
| `Prefabs/` | 8 prefab XML files (the howdah agents, the wolf target, two menu cameras). Auto-loaded, registered nowhere. | no |
| `SceneObj/`, `SceneEditData/` | 9 and 8 scene folders: the main-menu and cinematic scenes the Armory ships. | no |
| `Shaders/D3D11/` | `shader_compile_report.log` and `shader_mapping.bin`, both editor output. | no |
| `RuntimeDataCache/` | Engine-generated cache. Machine state, not authored content; `package_release.py` drops it unless you pass `--keep-rdc`. | no |

There is no `AssetPackages/` directory, no `GUI/`, no `bin/`, and no `THIRD-PARTY-LICENSES.txt`.

Inside `ModuleData/`, the files that matter most:

<!-- measured: python ElementTree parse of skins.xml, monsters.xml, action_sets.xml, action_types.xml, monster_usage_sets.xml, LOTRLOME_crafting_pieces.xml 2026-09-05 -->

| Path | Contents |
|---|---|
| `LOTRLOME_items/` | 18 culture folders plus 3 loose files. 2,904 `<Item>` in the folders, 336 in the files, plus 344 `<CraftedItem>` in `LOTRAOM_weapons.xml`. |
| `LOTRLOME_crafting_pieces.xml` | 672 `<CraftingPiece>`: every blade, guard, handle, pommel, head and shaft. |
| `crafting_templates.xslt`, `weapon_descriptions.xslt` | XSLT with no `.xml` sibling: pure additive overrides of vanilla's crafting tables. |
| `skins.xml` | 14 `<race>` blocks, 220,975 lines. The race registry. |
| `monsters.xml` | 70 `<Monster>`, which is the 14 races times five rows each (base, `_child`, `_settlement`, `_settlement_slow`, `_settlement_fast`). |
| `Monsters/LOTR/` | 7 creature monsters: spider, elephant, mumakil, chariot, warg, fell warg, war ram. |
| `action_sets.xml` | 1,229 `<action_set>`, 0 root-level `<action>`, 26 facegen sets, 6 standalone sets. |
| `action_types.xml` | 251 `<action>` declarations. |
| `monster_usage_sets.xml` | 4 sets: spider, elephant, chariot, warg. |
| `cloth_bodies.xml`, `cloth_materials.xml` | Cloth simulation data. Registered in neither manifest; see "What TAOM never worked out". |
| `physics_materials.xml`, `CollisionInfos/LOTR/` | The warg's collision class and its per-material impact table. |
| `module_sounds.xml` | The elephant and warg sound banks. |
| `Languages/` | 23 root files (22 `loc_*.xml` plus `language_data.xml`) and 12 language subfolders of 23 files each. |
| `Animations/`, `MonsterUsage/LOTR/` | Retired reference copies. Registered nowhere; `project.mbproj` says so in its own comments. |

<!-- measured: python ElementTree count of Culture ids in Main/_Module/ModuleData/taom_spcultures.xml 2026-09-05 -->

The folder name under `LOTRLOME_items/` is a filing convention, not a culture binding. Each item names its own culture in `culture="Culture.<id>"`, and they diverge: the three items in `LOTRLOME_items/troll/` all carry `culture="Culture.mordor"`. The 18 folders are also fewer than TAOM has cultures. `Main/_Module/ModuleData/taom_spcultures.xml` declares 24 `<Culture>` ids, and 8 of them (`abanissa`, `bluecraig`, `goblin`, `lindon`, `lothlorien`, `mistymountainorcs`, `shaghana`, `umbar`) have no folder here at all. Their troops are dressed from other folders or from vanilla. Vanilla armour does load and TAOM troops do wear it: `SandBoxCore` registers its own `items` tree, and TAOM has no XSLT that overrides it, so anything you read that calls this "the only armour tree the game loads" is wrong. See [items-armor](items-armor.md) and [`docs/features/armor-balance.md`](../features/armor-balance.md).

## The two registration channels

Nothing in `ModuleData/` loads because it is there. It loads because one of two manifests names it, and the two are not interchangeable.

<!-- engine-ref type="TaleWorlds.ObjectSystem.MBObjectManager" file="Core/TaleWorlds.ObjectSystem/TaleWorlds.ObjectSystem/MBObjectManager.cs" lines="877-982" -->

| | Managed channel | Native channel |
|---|---|---|
| Manifest | `SubModule.xml`, `<Xmls>/<XmlNode>/<XmlName id= path=>` | `ModuleData/project.mbproj`, `<base>/<file id= name= type=>` |
| Read by | `MBObjectManager.GetMergedXmlForManaged` (`MBObjectManager.cs:877`) | `XmlResource.GetMbprojxmls` (`XmlResource.cs:107`), then `GetMergedXmlForNative` (`MBObjectManager.cs:920`) |
| Path shape | `path="LOTRLOME_items/gondor"`, no `ModuleData/`, no `.xml` (`ModuleHelper.cs:232`) | `name="ModuleData/skins.xml"`, with both (`ModuleHelper.cs:211`) |
| Legal ids | The object-type names: `Items`, `Monsters`, `CraftingPieces`, `CraftingTemplates`, `WeaponDescriptions`, `ModuleSounds` | Only `soln_*` ids something asks for. An invented id is inert. |
| Carries | Items, crafting, monsters, sounds | Skins, action sets, action types, monster usage sets, monsters, physics, collision, sounds |

<!-- measured: grep -c "<XmlNode>" SubModule.xml; grep -o 'id="soln_[a-z_]*"' ModuleData/project.mbproj | sort | uniq -c 2026-09-05 -->

The Armory uses both. Its 33 `<XmlNode>` rows break down as 21 `Items`, 8 `Monsters`, 1 each of `CraftingPieces`, `CraftingTemplates`, `WeaponDescriptions` and `ModuleSounds`. Its `project.mbproj` carries 11 `<file>` rows over 8 distinct ids, with `soln_monsters` appearing four times.

### `path=` can name a folder or a file

`GetMergedXmlForManaged` tries `<module>/ModuleData/<path>.xml` first. If that file does not exist it strips the extension and, if a directory of that name exists, globs `GetFiles("*.xml")` inside it and merges every hit (`MBObjectManager.cs:894-909`). Both shapes ship here under one parent: `path="LOTRLOME_items/gondor"` is a folder of six files, `path="LOTRLOME_items/LOTRAOM_weapons"` is a single file. Two consequences follow. Dropping `starter_armors.xml` into `gondor/` needed no manifest edit at all, and a backup named `*.xml` inside a registered folder is loaded as content, injecting duplicate ids.

### An `.xslt` with no `.xml` is a working registration

For each contributor the engine also looks for `<path>.xsl`, then `<path>.xslt`. `CreateMergedXmlFile` walks contributors from index 1, applies that contributor's stylesheet to the document accumulated so far, and only then merges that contributor's own XML (`MBObjectManager.cs:966-982`). So a module that ships only a stylesheet still gets it applied: `CraftingTemplates` and `WeaponDescriptions` are registered here with no `.xml` file in the module at all, which is how the Armory adds pieces to vanilla's crafting templates without owning them. The same loop never applies the stylesheet of contributor 0, so a module that is the only contributor for an id would find its `.xslt` ignored. That does not bite the Armory, because Native and SandBoxCore always load first.

Every one of those override templates must end with `<xsl:apply-templates select="@*|node()"/>`, or vanilla's entries are dropped from the merged document. See [`docs/reference/item-usage-features.md`](../reference/item-usage-features.md).

### The `soln_*` id rule, and why an invented id is silent

`GetMergedXmlForNative` walks the rows parsed out of every module's `project.mbproj` and keeps only those whose `Id` matches the requested one exactly (`MBObjectManager.cs:932`). Requests come from nine managed call sites (`Module.cs:1366`, `:1378`, `:1389`, `:1419`, `:1430`, `:1449`, `:1482`, `:1493` and `MBMusicManager.cs:345`) plus one callback that builds the id as `"soln_" + xmlType` from whatever the native engine asks for (`Module.cs:1500-1504`). A row whose id nobody asks for is never read, and nothing is logged.

That cost TAOM a crash. `soln_spider_action_sets`, `soln_spider_action_types` and `soln_spider_monster_usage_sets` were invented ids, so `as_spider` and the `spider` usage set never registered, `GetMonsterUsageIndex("spider")` returned -1, and native agent creation divided by zero on the first spawn. The fix and the ledger are in [`docs/reference/lotrlome-soln-id-fix.md`](../reference/lotrlome-soln-id-fix.md); the gate is `python tools/audit_mbproj_registration.py --module LOTRLOME_Armory`.

### The MergeElements trap: never add a second `soln_action_sets` row

You may repeat a standard id. Whether that is safe depends on one thing: whether the id has an XSD. `CreateMergedXmlFile` calls `MergeTwoXmls` once per extra contributor against the fully accumulated document, and with an XSD path in hand that call routes into `MergeElements`, which indexes `XmlResource.XsdElementDictionary[xsdPath]` and then `elementSchema[GetFullXPathOfElement(...)]` with no `TryGetValue` (`MBObjectManager.cs:820-874`). Any XPath present in the accumulated tree but absent from that schema throws `KeyNotFoundException` at startup, before the main menu. That is the elephant "Crash #3".

So: `soln_action_sets` and `soln_action_types` have schemas, and a second row for either is forbidden. New action sets are appended into the single `action_sets.xml` instead. `soln_monsters`, `soln_physics_materials` and `soln_collision_infos` have no schema, so a repeat is a plain concatenation, which is why four `soln_monsters` rows ship today without incident. The file's own comment block records both halves.

## The art side: FBX to tpac to `mesh=`

<!-- measured: find Assets -name '*.tpac' | wc -l, once per _tex/_mtl/_geo/_anm suffix; ls -d AssetPackages 2026-09-05 -->

The Armory has no cooked asset tree: 0 packs against 4,364 loose `.tpac` under `Assets/`, of which 2,573 are `_tex`, 932 `_mtl`, 663 `_geo` and 196 `_anm`. The engine's own log names the loose tree, and it does so even for TAOM and TAOM_Map, which ship both, so loose does not merely fill in for a missing pack, it wins over one that exists ([`docs/reference/armory-guide.md`](../reference/armory-guide.md), "Two asset trees"). `Assets/` is the single source of truth for this module.

Art is built in the Bannerlord Modding Kit, never by a script. Import writes the source under `AssetSources/<folder>/` and the cooked file under `Assets/<folder>/`, and two editor behaviours bite: import textures first and meshes second, because slots bind by name, and the editor scans resources only at startup, so write tpacs with the editor closed ([`docs/reference/ue-to-bannerlord-asset-pipeline.md`](../reference/ue-to-bannerlord-asset-pipeline.md)). A mesh group becomes one `<Name>_geo.tpac`, a material one `_mtl`, and each material's diffuse, normal and specular maps three `_tex`.

The string you type into `mesh=` is the name inside the tpac, not the file name and not the editor label. `body_name` is `bo_` plus that exact mesh id, on blades, heads and bows only; guards, handles and pommels carry none. A `body_name` that resolves to nothing does not error: `PreloadHelper.WaitForMeshesToBeLoaded` polls until every registered body resolves, so one bad name spins the main thread forever with no crash and no log line ([`docs/features/mesh-ref-validation.md`](../features/mesh-ref-validation.md)).

<!-- measured: wc -l < docs/reference/armory-catalogue/catalogue.tsv 2026-09-05 -->

What exists is inventoried, not remembered. `docs/reference/armory-catalogue/catalogue.tsv` is 4,843 lines joining every packaged mesh to the XML that names it, regenerated from the live install by `python tools/generate_armory_catalogue.py`.

## Races, monsters and animation

A playable race is three files in this order, and the order is load-bearing.

<!-- engine-ref type="LOTRLOME_Armory race data" file="ModuleData/{skins,monsters,action_sets}.xml" lines="live module" -->

| File | What it adds | Rule |
|---|---|---|
| `ModuleData/skins.xml` | A `<race id="...">` block with its `<skin>` rows: skeleton, base meshes, scale, hair and beard tags. | **Append at the end only.** Race integers are merge-order indices in this list, so inserting one renumbers every hero in every existing save. |
| `ModuleData/monsters.xml` | Five `<Monster>` rows for the race: the base, `_child`, `_settlement`, `_settlement_slow`, `_settlement_fast`. | Names the action sets and the ragdoll bones. |
| `ModuleData/action_sets.xml` | `as_<race>_facegen` and `as_<race>_female_facegen`. | Copy `as_dwarf_facegen` verbatim and rename only `id` and `base_set`. A facegen set must declare everything itself. |

<!-- measured: python ElementTree iter('race') over ModuleData/skins.xml 2026-09-05 -->

The live file order is `dwarf`, `uruk`, `nazghul`, `orc`, `uruk_hai`, `berserker`, `cave_troll`, `hill_troll`, `pale_uruk`, `dg_uruk`, `goblin`, `elf`, `saruman`, `sauron`, and the `sauron` block carries the comment saying why it sits last. That is the complete list of legal `race=` values you may put on a troop, and `skins.xml` is where it is authored; nothing in the repo enumerates it. Human troops carry no `race=` at all, because human is index 0 from the merged vanilla list.

Facegen is the one that surprises people. The engine does not fall through `base_set` for the character-creation action families, so a slim facegen set renders the parent menu correctly and then breaks every later creation stage. Expect 106 male and 31 female actions per set, and read the checklist in [`docs/features/character-creation.md`](../features/character-creation.md) before authoring one.

Creature mounts follow the same shape one level down: a `<Monster>` under `Monsters/LOTR/`, its `act_<creature>_*` declarations in `action_types.xml`, an `as_<creature>` set plus the required `as_<creature>_map` child in `action_sets.xml`, a `<monster_usage_set>` in `monster_usage_sets.xml`, and rider-side rows injected into vanilla's `human` usage set through `monster_usage_sets.xslt`. The full walkthrough, with the v1.4.6 rules about total usage tables, is [`docs/community/bannerlordmodding-lt/guides/custom_creature_xml.md`](../community/bannerlordmodding-lt/guides/custom_creature_xml.md) and the [recipe-add-a-race-or-creature](recipe-add-a-race-or-creature.md) chapter.

## Worked example

The native manifest in full. Half of it is comments, and the comments are the record of two startup crashes.

<!-- excerpt file="LOTRLOME_Armory/ModuleData/project.mbproj" -->

```xml
<?xml version="1.0" encoding="utf-8"?>
<base xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance" xmlns:xsd="http://www.w3.org/2001/XMLSchema" type="solution">
	<file id="soln_skins" name="ModuleData/skins.xml" type="skin" />
	<file id="soln_action_sets" name="ModuleData/action_sets.xml" type="action_set" />
	<file id="soln_monsters" name="ModuleData/monsters.xml" type="monster" />
	<!-- Spider: the runtime loads a module's animation data ONLY from project.mbproj <file> entries
	     whose id is a RECOGNIZED standard value (soln_action_sets / soln_action_types /
	     soln_monster_usage_sets). The previous soln_spider_* CUSTOM ids were silently ignored at
	     runtime, so action_sets_spider.xml / action_types_spider.xml / lotr_monster_usage_spider.xml
	     never loaded -> as_spider + the "spider" monster_usage set never registered ->
	     GetMonsterUsageIndex("spider") = -1 -> native CreateAgent DivideByZero on spawn (2026-06-04).
	     Fix: standard ids only. as_spider is merged into the module's single action_sets.xml
	     (soln_action_sets, above); spider action_types + monster_usage_set are top-level files. The
	     old Animations/ + MonsterUsage/ subfolder copies are now superseded and unused. -->
	<file id="soln_action_types" name="ModuleData/action_types.xml" type="action_type" />
	<file id="soln_monster_usage_sets" name="ModuleData/monster_usage_sets.xml" type="monster_usage_set" />
	<!-- soln_spider_monster REMOVED 2026-08-28. It was a CUSTOM id, so GetMergedXmlForNative
	     (which matches MbprojXmls entries by exact id) never requested it and the row was inert,
	     exactly as the comment above warns. The spider Monster was never affected because it is
	     registered the managed way, in SubModule.xml <XmlName id="Monsters">, which is also how
	     the mumakil, chariot and war ram load. Do not re-add it as soln_monsters "to be safe":
	     that would start merging it into the native monster table, a change to a shipping feature
	     with nothing asking for it. Ledger: docs/reference/lotrlome-soln-id-fix.md -->
	<!-- soln_lotr_misc_action_types REMOVED 2026-08-28, same dead-custom-id class as the spider row
	     above, but this one had a real consequence: its 20 action declarations never reached the
	     engine while action_sets.xml referenced them 221 times. They now live in the single
	     soln_action_types file (action_types.xml, above); Animations/action_types_lotr_misc.xml is
	     retired to a .bak. A second soln_action_types row was rejected deliberately:
	     soln_action_types.xsd exists, so it would take the MergeElements path and risk the
	     elephant Crash #3 KeyNotFoundException. Ledger: docs/reference/lotrlome-soln-id-fix.md -->
	<!-- Elephant: monster def only. Elephant action_sets (as_elephant, as_elephant_town_and_village,
	     as_elephant_map) are merged into the SINGLE soln_action_sets file above (action_sets.xml),
	     mirroring the spider pattern. DO NOT add a second soln_action_sets entry pointing at a separate
	     action_sets_elephant.xml: MergeElements is called once per additional soln_action_sets entry on
	     the fully-accumulated element1 (all prior modules merged). If ANY child XPath in that accumulated
	     tree is absent from the action_sets XSD schema → KeyNotFoundException at startup (Crash #3,
	     2026-06-08). The standalone action_sets_elephant.xml still exists as a reference copy but is
	     NOT registered anywhere; the live entries are the appended block at the bottom of action_sets.xml. -->
	<file id="soln_monsters" name="ModuleData/Monsters/LOTR/lotr_monster_elephant.xml" type="monster" />
	<file id="soln_module_sound" name="ModuleData/module_sounds.xml" type="module_sound" />
	<!-- Warg (absorbed from Alliance.Wargs 2026-08-28; Byak0 assets, used with permission).
	     as_warg / as_warg_map / as_warg_town_and_village are merged into the SINGLE soln_action_sets
	     file above, and the 80 act_warg_* types into soln_action_types, per the elephant's Crash #3.
	     soln_monsters / soln_physics_materials / soln_collision_infos have NO XSD, so duplicating those
	     ids is a plain concat, not a MergeElements pass. Ledger: docs/reference/lotrlome-warg-changes.md -->
	<file id="soln_monsters" name="ModuleData/Monsters/LOTR/lotr_monster_warg.xml" type="monster" />
	<file id="soln_monsters" name="ModuleData/Monsters/LOTR/lotr_monster_fell_warg.xml" type="monster" />
	<file id="soln_physics_materials" name="ModuleData/physics_materials.xml" type="physics_material" />
	<file id="soln_collision_infos" name="ModuleData/CollisionInfos/LOTR/collision_infos_warg.xml" type="collision_infos" />
</base>
```

The three lines a reader changes first:

1. `id=` decides whether the row exists. Only an id something asks for is read; anything else is inert and silent.
2. `name=` is module-relative and includes both `ModuleData/` and the `.xml`, unlike `SubModule.xml`'s `path=`.
3. Adding a second row for an id that has a schema (`soln_action_sets`, `soln_action_types`) is the startup crash. Append into the existing file instead.

The managed manifest's first two registrations, which are the block you copy for a new culture folder:

<!-- excerpt file="LOTRLOME_Armory/SubModule.xml" -->

```xml
		<XmlNode>
			<XmlName id="Items" path="LOTRLOME_items/gondor"/>
			<IncludedGameTypes>
				<GameType value = "Campaign"/>
				<GameType value = "CampaignStoryMode"/>
				<GameType value = "CustomGame"/>
				<GameType value = "EditorGame"/>
			</IncludedGameTypes>
		</XmlNode>

		<XmlNode>
			<XmlName id="Items" path="LOTRLOME_items/mirkwood"/>
			<IncludedGameTypes>
				<GameType value = "Campaign"/>
				<GameType value = "CampaignStoryMode"/>
				<GameType value = "CustomGame"/>
				<GameType value = "EditorGame"/>
			</IncludedGameTypes>
		</XmlNode>
```

Two deviations elsewhere in the same file are deliberate, not sloppiness: the `CraftingTemplates` node omits `EditorGame`, and the `WeaponDescriptions` node has no `<IncludedGameTypes>` block at all, which means always included.

The item tree as it sits on disk:

<!-- excerpt file="LOTRLOME_Armory/ModuleData/LOTRLOME_items" -->
<!-- measured: ls -1 "<game>/Modules/LOTRLOME_Armory/ModuleData/LOTRLOME_items" 2026-09-05 -->

```
LOTRAOM_horses.xml
LOTRAOM_shields.xml
LOTRAOM_shields.xml.bak-shieldcase-20260901
LOTRAOM_weapons.xml
LOTRAOM_weapons.xml.bak-arrowmesh-20260901
LOTRAOM_weapons.xml.bak-arrowregroup-20260901
LOTRAOM_weapons.xml.bak-swordrebuild-20260901
arnor
dale
dol_guldur
dunland
erebor
gondor
gundabad
harad
iron_hills
isengard
mercenary
mirkwood
mordor
rhun
rivendell
rohan
thenn
troll
```

18 folders, 3 registered files, 4 backup sidecars. Note what the sidecars are not called: none ends in `.xml`, because the folder glob would load it.

A culture folder's canonical layout, with `gondor` as the model:

<!-- measured: python ElementTree count of Item elements per file in LOTRLOME_items/gondor 2026-09-05 -->

| File | Items |
|---|---|
| `arm_armors.xml` | 26 |
| `body_armors.xml` | 110 |
| `head_armors.xml` | 116 |
| `leg_armors.xml` | 22 |
| `shoulder_armors.xml` | 66 |
| `starter_armors.xml` | 6 |

The head of the dwarf race block in `skins.xml`, which is what a new race entry looks like:

<!-- example file="LOTRLOME_Armory/ModuleData/skins.xml" id="dwarf" -->

```xml
	<race id="dwarf">
		<skin
			gender="0"
			name="man"
			mesh_maturity_type="adult"
			morph_key="0"
			uses_stitching="true"
			body_mesh_suffix=""
			min_scale="1.05"
			skeleton="dwarf_skeleton_a"
			body_meta_mesh="sm_dwarf_basemesh_a1_body"
			body_meta_mesh_shoulders="sm_dwarf_basemesh_a1_shoulder"
			body_meta_mesh_upperbody="box_a"
			legs_mesh="sm_dwarf_basemesh_a1_legs"
			hands_mesh="sm_dwarf_basemesh_a1_arms"
			face_meta_mesh="sm_dwarf_basemesh_a1_head"
			underwear_bottom_mesh=""
			underwear_top_mesh="">
```

1. `skeleton=` picks the rig, and it decides what animation the race can play.
2. `body_meta_mesh` and its siblings are mesh ids that must exist in a tpac under `Assets/`, most often under `Assets/Race Test/`.
3. `min_scale=` is the race's height relative to a human, which is the single number that makes a dwarf a dwarf.

## Recipes

### Add: create the armoury module from zero

1. Make `<game>/Modules/<YourModule>/` and put one file in it, `SubModule.xml`, copying the identity block shape: `<Name>`, `<Id>`, `<Version>`, `<DefaultModule value="false"/>`, `<ModuleCategory value="Singleplayer"/>`, `<ModuleType value="Community"/>`.
2. Add `<DependedModules>` naming `Native`, `SandBoxCore`, `Sandbox` and `CustomBattle`, and a matching `<DependedModuleMetadatas>` block with `order="LoadBeforeThis"`. Match each dependency's declared `<Id>`, not its folder name: the folder is `SandBox` but `SandBox/SubModule.xml:4` declares `<Id value = "Sandbox"/>`, so the lowercase spelling is the correct one.
3. Add an empty, self-closing `<SubModules/>` and an empty `<Xmls>` block. Launch once and reach the main menu before adding data.
4. Create `ModuleData/` and your first item folder, mirroring `LOTRLOME_items/gondor/`: `arm_armors.xml`, `body_armors.xml`, `head_armors.xml`, `leg_armors.xml`, `shoulder_armors.xml`, `starter_armors.xml`, each a `<Items>` root of `<Item>` entries.
5. Register that folder (next recipe). Add `project.mbproj` only when you add native-side data (skins, action sets, action types, monster usage, physics, collision, sounds); an items-only module does not need one.

Check: `python tools/validate_moduledata.py`
Takes effect: full game restart
Code: No code changes needed

### Add: register a new item folder

1. Create `LOTRLOME_Armory/ModuleData/LOTRLOME_items/<culture>/` and put the item XML in it.
2. Open `LOTRLOME_Armory/SubModule.xml` and copy an existing `<XmlNode>` block (the `gondor` one above), changing only the `path=` value to `LOTRLOME_items/<culture>`. Position among the other `Items` nodes does not matter.
3. Grep every other folder for your id prefix first. A duplicate item id across two folders is shadowed silently, and the canonical home may not be the culture you expect: `sk_dwarf_iron_*` lives in `iron_hills/`, not `erebor/`. The prefix table is in [`docs/reference/armory-guide.md`](../reference/armory-guide.md).
4. Add `ModuleData/Languages/loc_<culture>.xml` and list it in `ModuleData/Languages/language_data.xml`. The English text a player sees comes from the inline `{=key}fallback` in the item XML, not from the English `loc_` rows.
5. Adding a further `.xml` to an already-registered folder needs no manifest edit; the folder is globbed.

Check: `python tools/validate_moduledata.py; python tools/validate_mesh_refs.py --scan-bodies`
Takes effect: full game restart
Code: No code changes needed

### Add: import an FBX and find its real mesh and `bo_` names

1. Import in the Modding Kit with the editor otherwise idle: textures first, then meshes. The importer writes `AssetSources/<folder>/<file>.fbx` and `Assets/<folder>/<file>_geo.tpac`.
2. Read the authoritative names out of the binary rather than trusting an editor label:
   `grep -aoE "(bo_)?wm_<culture>_[a-z0-9_]+" "<game>/Modules/LOTRLOME_Armory/Assets/<folder>/<file>_geo.tpac" | sort -u`
3. For a full listing of every asset item in the pack: `python tools/tpac_skeleton_scan.py "<game>/Modules/LOTRLOME_Armory/Assets/<folder>/<file>_geo.tpac" --all-types`
4. Author `mesh="<exact id>"`. For a blade, head or bow add `body_name="bo_<exact id>"`; guards, handles and pommels get no `body_name`.
5. Regenerate the inventory so the next person can find your mesh: `python tools/generate_armory_catalogue.py`

Check: `python tools/validate_mesh_refs.py --scan-bodies`
Takes effect: full game restart
Code: No code changes needed

### Add: a race entry

1. Append the `<race>` block at the END of `LOTRLOME_Armory/ModuleData/skins.xml`. Never insert or reorder: race integers are merge-order indices in this file, and shifting them renumbers every hero in every existing save.
2. Append five `<Monster>` rows to `LOTRLOME_Armory/ModuleData/monsters.xml`, following the `dwarf` block.
3. Copy `as_dwarf_facegen` and `as_dwarf_female_facegen` in `LOTRLOME_Armory/ModuleData/action_sets.xml` verbatim, renaming only `id` and `base_set`.
4. Copy the same three edits into `docs/reference/lotrlome-armory-snapshot/`, and record them as an APPLIED EDIT block in that folder's README.
5. The rest of the wiring (which cultures may pick the race, body properties, the position offset) is [recipe-add-a-race-or-creature](recipe-add-a-race-or-creature.md) and [`docs/features/hero-race.md`](../features/hero-race.md).

Check: `python tools/audit_action_set_parity.py; python tools/audit_civilian_action_set_coverage.py`
Takes effect: new campaign only
Code: Code changes required in `Main/Features/` for a race TAOM must persist; see [`docs/features/hero-race.md`](../features/hero-race.md)

### Modify: ship an Armory fix so a reinstall does not revert it

1. Make the edit in the live file, delimited by a marker comment naming the change (the shipped pattern is `TAOM-1H-POLEARM`).
2. Write a replay script in `tools/` that reproduces the edit between those markers, dry-run by default, `--apply` to write, `--revert` to remove, and a `.bak-<reason>-<date>` sidecar whose suffix replaces the extension rather than following it.
3. Write or extend an in-repo gate that fails when the edit is absent, the way `tools/audit_polearm_shield_parity.py` does.
4. Record it as an APPLIED EDIT block in `docs/reference/lotrlome-armory-snapshot/README.md`, and copy any race-defining file you touched into that folder.
5. Before a release, quarantine the sidecars: `pwsh tools/sweep_module_backups.ps1 -Apply`.

Check: the gate you wrote in step 3, plus `python tools/validate_moduledata.py`
Takes effect: full game restart
Code: Code changes required in `tools/` (the replay script and the gate)

### Delete: retire an item or a mesh

1. Find every consumer before deleting anything: `python tools/audit_item_refs.py --show-locations` for item ids, `python tools/audit_deleted_mesh_impact.py` for meshes.
2. Treat an ORPHAN verdict as a question, not an answer. That audit matches `Item.<id>` references and roster hops, and a crafting piece is named by neither: it is reached by `<UsablePiece piece_id=>` in `crafting_templates.xslt` and by `<Piece id=>` in a `<CraftedItem>`. Sweep for both before deleting.
3. Re-point what wore it, then delete, then regenerate the catalogue with `python tools/generate_armory_catalogue.py`.
4. Read the two write-ups first: [`docs/reviews/rca-armoury-keyforce-cleanup-2026-09-01.md`](../reviews/rca-armoury-keyforce-cleanup-2026-09-01.md) and [`docs/reviews/rca-armoury-dead-mesh-wave2-2026-09-01.md`](../reviews/rca-armoury-dead-mesh-wave2-2026-09-01.md). A seven-commit reorganisation broke hundreds of references and was caught from a screenshot, not from a gate. The general procedure is [recipe-retire-content](recipe-retire-content.md).

Check: `python tools/validate_moduledata.py; python tools/validate_mesh_refs.py --scan-bodies`
Takes effect: full game restart
Code: No code changes needed

## Gotchas: what fails silently and what crashes

- **A green validator run does not mean the engine loaded your file.** Bannerlord globs item XML once at process launch, with no hot reload. Naked troops plus a clean gate means the new file was not loaded yet, not that the data is wrong. Restart the game, not the save ([`docs/features/armoury-mesh-cleanup.md`](../features/armoury-mesh-cleanup.md)).
- **A typo'd item id in a roster produces a naked troop, not an error.** `GetObject<ItemObject>` returns null, `IsItemFitsToSlot` returns true for a null item by design, and the slot gets an empty `EquipmentElement` (`Equipment.cs:204-223`, `Equipment.cs:445-450`). Only `python tools/validate_moduledata.py` catches it.
- **A `.bak` file named `*.xml` inside a registered folder is loaded as content.** The folder glob takes every `*.xml`, so the backup injects duplicate ids and one entry is silently shadowed (`MBObjectManager.cs:903`). Use `.bak-<reason>-<date>`; see [`docs/reference/module-backup-sweep.md`](../reference/module-backup-sweep.md).
- **A name that looks like a typo may be the shipped name.** `wm_isengard_shield_a04` references `body_name="bo_capwm_isengard_shield_a02_clean"`, and the asset is packaged under that exact misspelling. Correcting it manufactures a hang. Only names the validator flags as `MISSING_BODY` are safe to rewrite ([`docs/reference/armory-shield-audit.md`](../reference/armory-shield-audit.md)).
- **A missing `bo_` collision body hangs the game instead of looking wrong.** No crash, no log, one core at 100 percent, mission never loads ([`docs/features/mesh-ref-validation.md`](../features/mesh-ref-validation.md)).
- **A root-level `<action>`, parented by `<action_sets>` rather than by an `<action_set>`, loads fine on the client and kills a dedicated server on boot** with `KeyNotFoundException` in `MergeElements`. The live file is clean today; the gate is `python tools/audit_action_set_parity.py` ([`docs/reference/armory-guide.md`](../reference/armory-guide.md)).
- **A standalone `action_set` (one with `skeleton=` and no `base_set`) inherits nothing and rots across engine updates.** `as_dwarf_warrior` drifted 423 action types behind Native by v1.4.6, and a dwarf walking into water crashed. Re-run `python tools/patch_dwarf_action_parity.py` after every engine bump, against the live file and the snapshot ([`docs/reference/lotrlome-armory-snapshot/README.md`](../reference/lotrlome-armory-snapshot/README.md), "Standalone combat action_set parity").
- **The rider partial `as_human_warrior` must be the first element in `action_sets.xml`.** `base_set` inheritance snapshots at definition time, so anything merged after a race set never reaches it. The file says so itself, in the comment at `action_sets.xml:3-6`.
- **A `Type="HorseHarness"` item with no `<Armor family_type="N">` deserializes to 0, which is human,** and the inventory then refuses the harness on every mount with no message. The mount's `Monster.family_type` is the only authority; `family_type` on `<Horse>` is never parsed. Codes `MISSING_HARNESS_FAMILY_TYPE` and `HARNESS_FAMILY_MISMATCH` in `python tools/validate_moduledata.py`; the family-type legend is in [`docs/reference/armory-guide.md`](../reference/armory-guide.md).
- **Single-piece weapon stats are schema-typed `unsignedInt`.** `weapon_length="210.04"` on a bow, crossbow, shield, javelin or thrown item is a hard load error. Crafting-piece `length`, `blade_length` and `blade_width` are floats and take decimals ([`docs/ai-includes/weapon-creation-workflow.md`](../ai-includes/weapon-creation-workflow.md)).
- **The two crafted-item gates fail differently.** A piece missing from a template's `<UsablePiece>` list makes `GenerateCraftedItem` return null and the item unregister itself, so every troop naming it holds a broken reference. A stale id in `weapon_descriptions.xslt` is merely inert ([`docs/features/weapon-xml-pipeline.md`](../features/weapon-xml-pipeline.md)).
- **The Armory ships no `THIRD-PARTY-LICENSES.txt`.** It absorbed Byak0's Alliance.Wargs assets on 2026-08-28 and `package_release.py` ships the module by default, so TAOM now redistributes them. The notice obligation is written down in [`docs/reference/provenance-register.md`](../reference/provenance-register.md) and is not yet met.

## What TAOM never worked out

Say "not determined" rather than guessing on these. Each names where to look.

- **How `cloth_bodies.xml` and `cloth_materials.xml` are loaded.** Neither appears in `SubModule.xml` nor in `project.mbproj`, and `cloth_bodies` appears nowhere in the shipping-client decompile. The dump is a shipping-client build, so search the editor build instead.
- **Which module-root folders the engine discovers with no registration at all.** `Languages/`, `Prefabs/`, `ModuleSounds/`, `SceneObj/` and `SceneEditData/` are all present and unregistered, and the pattern is clearly convention, but the discovery code path has not been read.
- **Whether the stale `<DependedModuleMetadata id="Native" version="v1.4.5.*"/>` is enforced against the installed v1.4.8 or advisory.** The game runs, which suggests advisory, but the launcher's version-constraint code has not been read.
- **How far the tracked snapshot has drifted from the live files.** The snapshot ledger stops at 2026-08-20 while live timestamps run to 2026-09-02. Timestamps were compared, contents were not diffed.
- **Which `hair_tag`, `beard_tag` and `tattoo_tag` names are legal per race.** The declaration site is the per-race block in `skins.xml`; the matching is native, so the decompile will not tell you. Working examples live in `Main/_Module/ModuleData/TAOM_bodyproperties.xml`.

One item that research left open is now settled: `AssetSources/` does **not** ship to players. `tools/package_release.py:116` returns an explicit `EXCLUDE` decision for it ("editor-only, never loaded at runtime") before the include list is ever consulted.

## Numbers in this chapter

All measured 2026-09-05 against the installed `Modules/LOTRLOME_Armory/` and the repo.

| Number | Command |
|---|---|
| 1 top-level file, 9 directories; no `AssetPackages/` | `ls -1 "<game>/Modules/LOTRLOME_Armory"` |
| 354 lines in `SubModule.xml`; 33 `<XmlNode>`; 21 Items, 8 Monsters, 1 each of CraftingPieces / CraftingTemplates / WeaponDescriptions / ModuleSounds | `wc -l < SubModule.xml; grep -c "<XmlNode>" SubModule.xml; grep -o '<XmlName id="[A-Za-z]*"' SubModule.xml \| sort \| uniq -c` |
| 50 lines, 11 `<file>` rows, 8 distinct ids, `soln_monsters` four times in `project.mbproj` | `wc -l < ModuleData/project.mbproj; grep -c "<file id=" ModuleData/project.mbproj; grep -o 'id="soln_[a-z_]*"' ModuleData/project.mbproj \| sort \| uniq -c` |
| 18 item folders, 3 registered files, 4 backup sidecars under `LOTRLOME_items/` | `ls -1 ModuleData/LOTRLOME_items/` |
| 2,904 items in the folders, 336 in the three files, 344 `<CraftedItem>` in `LOTRAOM_weapons.xml`; gondor 346, rhun 594, troll 3 | Python `ElementTree` count of `Item` and `CraftedItem` per file |
| gondor per file: 26 / 110 / 116 / 22 / 66 / 6 | Python `ElementTree` count of `Item` per file in `LOTRLOME_items/gondor` |
| 672 `<CraftingPiece>` in `LOTRLOME_crafting_pieces.xml` | Python `ElementTree` child-tag count |
| 14 races in `skins.xml`, in the order listed; 220,975 lines | Python `ElementTree` `iter('race')`; `wc -l < ModuleData/skins.xml` |
| 70 `<Monster>`, 1,229 `<action_set>`, 0 root-level `<action>`, 26 facegen sets, 6 standalone sets, 251 action types, 4 monster usage sets | Python `ElementTree` parse of `monsters.xml`, `action_sets.xml`, `action_types.xml`, `monster_usage_sets.xml` |
| 38 `Assets/` folders; 4,364 tpac = 2,573 `_tex` + 932 `_mtl` + 663 `_geo` + 196 `_anm`; 129 warg animation clips | `ls -1 Assets \| wc -l; find Assets -name '*.tpac' \| wc -l` and once per suffix |
| 39 `AssetSources/` folders, 693 FBX, 2,746 PNG | `ls -1 AssetSources \| wc -l; find AssetSources -iname '*.fbx' \| wc -l; find AssetSources -iname '*.png' \| wc -l` |
| 23 files at `Languages/` root, 12 language folders of 23 files each | `ls -1 ModuleData/Languages/*.xml \| wc -l; for d in BR CNs ... TR; do ls -1 ModuleData/Languages/$d \| wc -l; done` |
| 8 prefabs, 9 `SceneObj` folders, 8 `SceneEditData` folders | `ls -1 Prefabs \| wc -l; ls -1 SceneObj \| wc -l; ls -1 SceneEditData \| wc -l` |
| 13 `.bak-*` sidecars module-wide | `find "<game>/Modules/LOTRLOME_Armory" -name '*.bak*' \| wc -l` |
| 4,843 lines in the mesh catalogue | `wc -l < docs/reference/armory-catalogue/catalogue.tsv` |
| 24 `<Culture>` ids in TAOM, 8 with no Armory folder | Python `ElementTree` `iter('Culture')` over `Main/_Module/ModuleData/taom_spcultures.xml` |
| 9 managed `soln_*` request sites plus one dynamic callback | `grep -rn '"soln_' "<decompile root>"` |

## Read next

- [`docs/reference/armory-guide.md`](../reference/armory-guide.md): the canonical-folder-per-prefix table, the shield and harness rules, and the two-asset-tree correction.
- [`docs/reference/lotrlome-armory-snapshot/README.md`](../reference/lotrlome-armory-snapshot/README.md): the change ledger for a module with no git history, the facegen checklist and the applied-edit blocks.
- [`docs/reference/lotrlome-soln-id-fix.md`](../reference/lotrlome-soln-id-fix.md): the inert-id failure in full, with the exact-equality loop and the call sites named.
- [`docs/reference/armory-catalogue/README.md`](../reference/armory-catalogue/README.md): what the 4,843-row mesh catalogue contains and how to diff it after an art drop.
- [`docs/features/mesh-ref-validation.md`](../features/mesh-ref-validation.md): the three validation tiers, the `bo_` hang, and why a PASS can lie.
- [`docs/ai-includes/weapon-creation-workflow.md`](../ai-includes/weapon-creation-workflow.md): the four-file weapon path from FBX to shipped item, by hand.
- [`docs/features/weapon-xml-pipeline.md`](../features/weapon-xml-pipeline.md): the same path generated, and the two gates a piece id must clear.
- [`docs/reference/item-usage-features.md`](../reference/item-usage-features.md): the token vocabulary a `WeaponDescription` resolves against, and the mace-versus-axe trap.
- [`docs/reference/armory-shield-audit.md`](../reference/armory-shield-audit.md): the shield invariants and the two do-not-fix names.
- [`docs/features/armoury-mesh-cleanup.md`](../features/armoury-mesh-cleanup.md): deleting art and re-dressing what wore it, with the ORPHAN caveat.
- [`docs/reference/ue-to-bannerlord-asset-pipeline.md`](../reference/ue-to-bannerlord-asset-pipeline.md): FBX and texture to tpac, including the channel packing and the editor-startup rule.
- [`docs/features/hero-race.md`](../features/hero-race.md) and [`docs/features/character-creation.md`](../features/character-creation.md): race integers, the facegen requirement and the position offset.
- [`docs/community/bannerlordmodding-lt/guides/custom_creature_xml.md`](../community/bannerlordmodding-lt/guides/custom_creature_xml.md): the from-zero creature guide, measured against v1.4.8.
- [`docs/ai-includes/new-culture-authoring.md`](../ai-includes/new-culture-authoring.md): the tpac drop to registered culture folder walkthrough.
- [`docs/reference/module-backup-sweep.md`](../reference/module-backup-sweep.md): the backup naming rule and the release sweep.
- [`docs/reference/provenance-register.md`](../reference/provenance-register.md) and [`docs/reference/asset-provenance.md`](../reference/asset-provenance.md): who authored what, and the outstanding Byak0 notice.
- [`tools/README.md`](../../tools/README.md): every tool named above, with its flags.
