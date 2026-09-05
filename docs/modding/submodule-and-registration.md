# SubModule.xml and registration

## What this file is

This chapter covers the two manifests that decide whether the game ever opens a data file: `SubModule.xml` at a module's root and `ModuleData/project.mbproj` beneath it. Nothing under `ModuleData/` loads because it exists; it loads because one of four mechanisms names it, and when the name is wrong three of the four say nothing at all. Read it before adding any XML, because "the file is there and the game ignores it" has already cost TAOM one crash and twenty action declarations that never reached the engine ([lotrlome-soln-id-fix](../reference/lotrlome-soln-id-fix.md)), and every case below shipped.

## The four ways a file gets loaded

| Mechanism | Who names the file | Engine entry point | TAOM today |
|---|---|---|---|
| 1. an `<XmlNode>` row in `SubModule.xml` | you, one row per file or per folder | `XmlResource.GetXmlListAndApply` reads `Module/Xmls/XmlNode` (`XmlResource.cs:149`); `MBObjectManager.GetMergedXmlForManaged` merges what it found (`MBObjectManager.cs:877-918`) | 100 rows in `Main/_Module/SubModule.xml` <!-- measured: rg -c "<XmlNode>" Main/_Module/SubModule.xml 2026-09-05 --> |
| 2. a `<file>` row in `ModuleData/project.mbproj` | you, standard `soln_*` ids only | `XmlResource.GetMbprojxmls` reads `base/file` (`XmlResource.cs:117`); `MBObjectManager.GetMergedXmlForNative` merges (`MBObjectManager.cs:920-947`) | 5 rows in the repo, 11 in `LOTRLOME_Armory` <!-- measured: rg -c "<file " Main/_Module/ModuleData/project.mbproj; rg -c "<file " LOTRLOME_Armory/ModuleData/project.mbproj 2026-09-05 --> |
| 3. a `language_data.xml` anywhere under `ModuleData/Languages/` | nobody, the engine scans the folder | `LocalizedTextManager.LoadLocalizationXmls` (`LocalizedTextManager.cs:91-99`) | 12 language folders <!-- measured: ls -d Main/_Module/ModuleData/Languages/*/ | wc -l 2026-09-05 --> |
| 4. a path built in C# | TAOM's own feature code, one file at a time | `CareerConfigProvider.cs:325`, `TroopWeightXmlLoader.cs:41` and 42 more files under `Main/Features/`; vanilla does the same for `ModuleData/global_strings.xml` (`GameTextManager.cs:134`) | 38 of the 42 `ModuleData/` subdirectories are named in no `SubModule.xml` path (one of them, `VoiceDefinitions/`, is reached through `project.mbproj` line 9, and `Languages/` is mechanism 3) <!-- measured: python -c "import os,xml.etree.ElementTree as E;p={n.find('XmlName').get('path').split('/')[0] for n in E.parse('Main/_Module/SubModule.xml').findall('Xmls/XmlNode')};d=[x for x in os.listdir('Main/_Module/ModuleData') if os.path.isdir('Main/_Module/ModuleData/'+x)];print(len(d),len([x for x in d if x not in p]))" 2026-09-05 --> |

All four run once, at process start. `Module.Initialize` takes the launcher's module list, loads localization, then calls `LoadSubModules` (`Module.cs:261-267`), and `LoadSubModules` calls `GetMbprojxmls` then `GetXmlListAndApply` for every module before it loads a single DLL (`Module.cs:1029-1033`). Two things follow. **A module with no code still contributes data**: `TAOM_Map` and `LOTRLOME_Armory` both ship `<SubModules/>` empty and register 8 and 33 rows respectively. <!-- measured: rg -c "<XmlNode>" TAOM_Map/SubModule.xml; rg -c "<XmlNode>" LOTRLOME_Armory/SubModule.xml 2026-09-05 --> `TAOM_Map/SubModule.xml` and `LOTRLOME_Armory/SubModule.xml` live in the game install, not the repo; a module reinstall reverts hand edits, so land a repo-side validator gate with any fix. And **a file registered while the game is running does not exist** until the next full restart; a green `python tools/validate_all_troop_refs.py` proves the reference resolves on disk, not that the engine opened the file.

The order the merge walks is the launcher's, not the order of `<DependedModules>`; that is covered in [Load order and dependencies](load-order-and-dependencies.md).

## Mechanism 1: an `<XmlNode>` row in `SubModule.xml`

Each row becomes one record with three fields (`XmlResource.cs:156-158, 165-171, 173-180`):

| Field | Where it comes from | What it does | Read at |
|---|---|---|---|
| `id` | `<XmlName id="...">` | names the registry the file joins, and picks the schema: the module's own `ModuleData/XmlSchemas/<id>.xsd` when it exists (no installed module ships one), else `XmlSchemas/<id>.xsd` at the game root <!-- measured: ls -d */ModuleData/XmlSchemas from the Modules folder, no such directory 2026-09-05 --> | `XmlResource.cs:157, 159-163`; `MBObjectManager.cs:888-892`; `ModuleHelper.cs:242-250` |
| `path` | `<XmlName path="...">` | resolved as `<module>/ModuleData/<path>.xml`: no extension, no `ModuleData/` prefix, forward slashes for subfolders | `XmlResource.cs:158`; `ModuleHelper.cs:232-235` |
| game types | every child of `<IncludedGameTypes>`, its `value` attribute | the game modes the row is included in; an absent or empty block means every mode | `XmlResource.cs:165-171`; `MBObjectManager.cs:884` |

**`id` and the root element inside the file are two different keys.** `id` selects the merge group and the schema. When the merged document is handed to `MBObjectManager.LoadXml`, the loader looks for a root element whose name matches a registered list name (`Items`, `NPCCharacters` and the rest: `Game.cs:309`, `Campaign.cs:1537`) and returns, loading nothing, when none does (`MBObjectManager.cs:1366-1386`). So a file registered under `id="NPCCharacters"` whose root is `<Troops>` merges fine and produces zero troops. It is not quite silent for TAOM's ids: all 12 have an `XmlSchemas/<id>.xsd`, so the schema pass that runs first writes an `Error:` line through `Debug.Print` (`MBObjectManager.cs:1107, 1324-1341`), which is not an assert or a popup. A root that matches a different registered list name (`<Items>` in a troop file) is worse: every child is created as that other type and handed to its `Deserialize` (`:1387-1396`). The id-to-root pairs are in the [File catalogue](file-catalogue.md). <!-- measured: a loop testing XmlSchemas/<id>.xsd at the game root for each of the 12 XmlName ids in Main/_Module/SubModule.xml, 12 hits 2026-09-05 -->

### Where `path` can point

`GetMergedXmlForManaged` tries three things in order (`MBObjectManager.cs:893-915`):

1. **A file.** If `<module>/ModuleData/<path>.xml` exists, that file is the entry, and a sibling `<path>.xsl` (then `<path>.xslt`) is queued as its stylesheet (`:893-898`, `:949-964`). The file check runs first, so `LOTRLOME_Armory/SubModule.xml:216` `path="monsters"` loads `monsters.xml` even though a `Monsters/` folder sits beside it.
2. **A folder.** If there is no such file but a directory of that name exists, every `*.xml` directly inside it becomes its own entry, each with its own `.xsl` sibling (`:900-910`). The glob is `GetFiles("*.xml")`: not recursive, and any file with an `.xml` extension counts. Vanilla ships this form as `SandBoxCore/SubModule.xml` `path="items"`, a folder of 10 files. <!-- measured: ls SandBoxCore/ModuleData/items/*.xml | wc -l 2026-09-05 --> The Armory registers 18 item folders this way, one per culture or grouping (`troll` and `mercenary` are not cultures), and `LOTRLOME_items/gondor/` alone holds 6 files. <!-- measured: python -c "import os,xml.etree.ElementTree as E;m='LOTRLOME_Armory/ModuleData/';print(sum((not os.path.isfile(m+p+'.xml')) and os.path.isdir(m+p) for p in (n.find('XmlName').get('path') for n in E.parse('LOTRLOME_Armory/SubModule.xml').findall('Xmls/XmlNode'))))"; ls LOTRLOME_Armory/ModuleData/LOTRLOME_items/gondor/*.xml | wc -l 2026-09-05 --> TAOM's own manifest uses the folder form nowhere; all 100 rows name a file or a stylesheet.
3. **Neither.** The row still counts: it contributes an empty slot and its `.xsl`/`.xslt` still runs against everything merged so far (`:911-915`). That is how TAOM's 8 stylesheet-only registrations work (`spkingdoms`, `spcultures`, `spclans`, `lords`, `heroes`, `module_strings`, `action_strings`, `comment_strings`), none of which has an `.xml` beside it. <!-- measured: ls Main/_Module/ModuleData/*.xslt | wc -l 2026-09-05 --> The Armory has two more, `crafting_templates` and `weapon_descriptions`. A misspelled `path` takes exactly this branch, with no log line, which is why the first check for a file that "does not load" is `ls` on the resolved path.

### The game-type filter

A row is skipped when the module is inactive, or when the filter is on, the row lists at least one game type, and the current one is not among them (`MBObjectManager.cs:884`; `ModuleHelper.cs:138-144`). The current game type is the **class name** of the running `GameType` (`GameType.cs:40`; `MBObjectManagerExtensions.cs:14-15`), so the strings are:

| Value | Class | Used when |
|---|---|---|
| `Campaign` | `Campaign.cs:41` | a sandbox campaign |
| `CampaignStoryMode` | `CampaignStoryMode.cs:11` | a story campaign; it extends `Campaign` but the string is different, so listing `Campaign` alone excludes it |
| `CustomGame` | `CustomGame : GameType` in the `CustomBattle` module assembly (not in the shipping-client dump; the module-build decompile `CustomBattle__TaleWorlds.MountAndBlade.CustomBattle.cs:4033`) | the custom battle menu |
| `EditorGame` | `EditorGame.cs:9` | the Modding Kit |

Matching is exact string `Contains`, case-sensitive. Every TAOM row that has the block lists `Campaign` and `CampaignStoryMode`; `CustomGame` is on 76 rows and `EditorGame` on 75. <!-- measured: rg -c "<IncludedGameTypes>" Main/_Module/SubModule.xml; rg -c 'GameType value ?= ?"Campaign"' Main/_Module/SubModule.xml; rg -c 'GameType value ?= ?"CampaignStoryMode"' Main/_Module/SubModule.xml; rg -c 'GameType value ?= ?"CustomGame"' Main/_Module/SubModule.xml; rg -c 'GameType value ?= ?"EditorGame"' Main/_Module/SubModule.xml 2026-09-05 --> The two rows without a block, `banner_icons` and `custom_battle_scenes` (`Main/_Module/SubModule.xml:964-969`), load in every mode. Some loaders switch the filter off for their own id: `CoreParameters` never passes a game type (`TaleWorlds.Core/ManagedParameters.cs:27`; the campaign assembly has an unrelated file of the same name), and a tutorial campaign reports `IsDevelopment` (`Campaign.cs:229`), which bypasses the filter for everything (`MBObjectManager.cs:788`).

**Keep comments outside `<IncludedGameTypes>`.** The reader takes every child node and dereferences its `value` attribute (`XmlResource.cs:168-171`), and the `IgnoreComments` setting on the line above is created and thrown away (`XmlResource.cs:145`), so a comment inside the block is a null reference at startup. All 98 TAOM blocks are clean today. <!-- measured: python -c "import re;s=open('Main/_Module/SubModule.xml',encoding='utf-8-sig').read();b=re.findall(r'<IncludedGameTypes>(.*?)</IncludedGameTypes>',s,re.S);print(len(b),sum('<!--' in x for x in b))" 2026-09-05 -->

## Mechanism 2: a `<file>` row in `project.mbproj`

`project.mbproj` looks like a Modding Kit file and is read by the shipping game. `GetMbprojxmls` opens `<module>/ModuleData/project.mbproj` (`ModuleHelper.cs:201-209`) and records every `<file>` child of `<base>` as an id plus a name (`XmlResource.cs:117-138`). Three rules differ from mechanism 1:

- **`name` is a path from the module root and keeps its extension**: `ModuleData/skins.xml`, resolved by plain concatenation (`ModuleHelper.cs:211-214`). The stylesheet for it is the same name with the last four characters replaced by `.xsl`, then `.xslt` (`ModuleHelper.cs:221-225`; `MBObjectManager.cs:943, 949-964`).
- **There is no game-type filter**; the list is created empty for every row (`XmlResource.cs:136`).
- **Nothing loads unless something asks for the id.** `GetMergedXmlForNative(id)` keeps only rows whose id is string-equal to the one requested (`MBObjectManager.cs:932`), and the requests come from nine call sites in `Module.cs`: eight literal ids (`soln_skins` `:1366`, `soln_item_holsters` `:1378`, `soln_action_sets` `:1389`, `soln_action_types` `:1419`, `soln_animations` `:1430`, `soln_voice_definitions` `:1449`, `soln_sound_event_data` `:1482`, `soln_sound_parameter_data` `:1493`) plus one that builds `"soln_" + xmlType` from a type name the native side supplies (`:1504`). <!-- measured: rg -c 'GetMergedXmlForNative\("' Module.cs 2026-09-05 --> An invented id is never requested, so its file is never opened and no warning is written ([lotrlome-soln-id-fix](../reference/lotrlome-soln-id-fix.md), lines 12-45).

The legitimate vocabulary is whatever `Native/ModuleData/project.mbproj` uses: 50 rows, 39 distinct ids. <!-- measured: rg -c "<file " Native/ModuleData/project.mbproj; rg -o 'id="soln_[a-z_]+"' Native/ModuleData/project.mbproj | sort -u | wc -l 2026-09-05 --> Seven installed modules carry a `project.mbproj` at all. <!-- measured: ls */ModuleData/project.mbproj | wc -l 2026-09-05 -->

### Duplicating an id

Several rows may share one id; Native itself has seven `soln_particle_systems` rows. <!-- measured: rg -c 'id="soln_particle_systems"' Native/ModuleData/project.mbproj 2026-09-05 --> Whether that is safe depends on one file: `XmlSchemas/<id>.xsd`. With no schema the merge is a plain append; with one, the second and later rows go through `MergeElements`, which indexes the schema dictionary without a guard and throws `KeyNotFoundException` at startup for any element the schema does not describe (`MBObjectManager.cs:925-929` picks the schema, `1001-1008` chooses the branch, `827-856` index the schema dictionary unguarded; [lotrlome-soln-id-fix](../reference/lotrlome-soln-id-fix.md), lines 72-93). The install ships 51 schemas, 14 of them `soln_*`: <!-- measured: ls XmlSchemas/*.xsd | wc -l; ls XmlSchemas/soln_*.xsd | wc -l 2026-09-05 --> `soln_action_sets`, `soln_action_types`, `soln_animations`, `soln_bone_body_types`, `soln_full_movement_sets`, `soln_item_holsters`, `soln_item_usage_sets`, `soln_monster_usage_sets`, `soln_movement_sets`, `soln_skins`, `soln_sound_event_data`, `soln_sound_parameter_data`, `soln_voice_definitions`, `soln_worldmap_color_grades`. `soln_monsters`, `soln_physics_materials`, `soln_collision_infos` and `soln_module_sound` have none, which is why the Armory ships four `soln_monsters` rows and exactly one `soln_action_sets` row ([lotrlome-warg-changes](../reference/lotrlome-warg-changes.md), lines 149-178). <!-- measured: rg -c 'id="soln_monsters"' LOTRLOME_Armory/ModuleData/project.mbproj 2026-09-05 -->

### `<file>` versus `<Module>`

The reader selects `file` children and nothing else (`XmlResource.cs:117`). `TAOM_Map/ModuleData/project.mbproj` writes all 9 of its rows as `<Module id=... name=...>`, so the engine registers none of them; it also labels `action_types.xml` as `soln_action_sets` on line 6. <!-- measured: rg -c "<Module " TAOM_Map/ModuleData/project.mbproj; grep -c "<file " TAOM_Map/ModuleData/project.mbproj 2026-09-05 --> Nothing is lost today because all nine targets are Kit stubs of 214 to 326 bytes, each a Kit comment plus a root element whose only child is `<replace_this_with_actual_nodes/>`, <!-- measured: wc -c TAOM_Map/ModuleData/{physics_materials,collision_infos,face_animations,action_sets,action_types,combat_parameters,native_parameters,item_holsters,skins}.xml 2026-09-05 --> but anyone who fills one of those stubs expecting it to load will wait a long time. `python tools/audit_mbproj_registration.py` reads `<file>` rows, so it cannot see this defect either; its default scope reports `TAOM_Map` as clean.

## Mechanism 3: `ModuleData/Languages/`

The engine walks every module's `ModuleData/Languages` folder recursively for files named exactly `language_data.xml` (`LocalizedTextManager.cs:91-99`) and takes the `<LanguageFile xml_path>` rows from each. Neither manifest is involved. English text is the inline `{=key}Default` in the data file itself; the rest is in [Strings and localization](strings-and-localization.md).

## Mechanism 4: files TAOM's code opens

Every feature config under `ModuleData/` that carries a feature's name (`career_system/`, `configs/`, `factionmap/`, `recruitment_pools/` and the rest) is opened by C# building the path by hand: `Path.Combine(_pathService.ModuleDataPath, "career_system", "taom_ability_templates.xml")` (`CareerConfigProvider.cs:325`) is the pattern, and 44 files under `Main/Features/` do it. <!-- measured: rg -l ModuleDataPath Main/Features | wc -l 2026-09-05 --> These files are never merged, never schema-validated and never game-type filtered; the feature decides what a missing or malformed file means. To find the loader for a folder, search `Main/Features/` for the folder name. The engine has one such path of its own: every module's `ModuleData/global_strings.xml` is read directly, without a row anywhere (`GameTextManager.cs:127-158`).

## Worked example

The Gondor troop file's row, `Main/_Module/SubModule.xml:180-188`, registered by file under the campaign and battle-related game types.

<!-- excerpt file="Main/_Module/SubModule.xml" -->
```xml
    <XmlNode>
      <XmlName id="NPCCharacters" path="troops/troops_gondor"/>
      <IncludedGameTypes>
        <GameType value ="Campaign"/>
        <GameType value ="CampaignStoryMode"/>
        <GameType value = "CustomGame"/>
        <GameType value = "EditorGame"/>
      </IncludedGameTypes>
    </XmlNode>
```

1. **`id="NPCCharacters"`**: the registry for troops, notables, wanderers and lords. Change it only when the file's root element changes.
2. **`path="troops/troops_gondor"`**: resolves to `Main/_Module/ModuleData/troops/troops_gondor.xml`. A new troop file is a copy of this row with a new `path`.
3. **The four `<GameType>` rows**: drop `EditorGame` and the Kit stops seeing these troops; drop `CustomGame` and custom battle does.

The Armory's native registry, file lines 2 to 16 and 39 to 50, with two of its five comment blocks, which record both traps this chapter is about.

<!-- excerpt file="LOTRLOME_Armory/ModuleData/project.mbproj" -->
```xml
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
```

File lines 17 to 38, left out here, are three more comment blocks: the `soln_spider_monster` row removed 2026-08-28 (a custom id, so inert), the `soln_lotr_misc_action_types` row removed the same day (its 20 action declarations never reached the engine while `action_sets.xml` referenced them 221 times), and the elephant note on why a second `soln_action_sets` row is forbidden (Crash #3). <!-- measured: sed -n '17,38p' LOTRLOME_Armory/ModuleData/project.mbproj 2026-09-05 -->

```xml
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

1. **`id`** must be one of the 39 vanilla ids. The spider comment records what an invented one costs.
2. **`name`** keeps `ModuleData/` and `.xml`; compare the `path` in the example above.
3. **One row for `soln_action_sets` and one for `soln_action_types`**, because both have a schema; four `soln_monsters` rows, because that id has none.

The cautionary case, quoted whole. Every row is a `<Module>` element, so `SelectNodes("file")` returns nothing and the file registers nothing.

<!-- excerpt file="TAOM_Map/ModuleData/project.mbproj" -->
```xml
<base type="solution">
	<Module id="soln_physics_materials" name="ModuleData/physics_materials.xml" type="physics_material"/>
	<Module id="soln_collision_infos" name="ModuleData/collision_infos.xml" type="collision_infos"/>
	<Module id="soln_face_animation_records" name="ModuleData/face_animations.xml" type="face_animation_record"/>
	<Module id="soln_action_sets" name="ModuleData/action_sets.xml" type="action_set"/>
	<Module id="soln_action_sets" name="ModuleData/action_types.xml" type="action_type"/>
	<Module id="soln_combat_system" name="ModuleData/combat_parameters.xml" type="animation_combat_parameters"/>
	<Module id="soln_combat_system" name="ModuleData/native_parameters.xml" type="native_parameters"/>
	<Module id="soln_item_holsters" name="ModuleData/item_holsters.xml" type="item_holsters"/>
	<Module id="soln_skins" name="ModuleData/skins.xml" type="skin"/>
</base>
```

1. **`<Module`** should read `<file`; that is the whole defect.
2. **Line 6** pairs `action_types.xml` with `soln_action_sets`; the correct id is `soln_action_types`.
3. **All nine targets are stubs**, so fixing this file changes nothing until one of them carries real data.

## Recipes

### Register a new repo XML

1. Put the file under `Main/_Module/ModuleData/`, for example `Main/_Module/ModuleData/characters/npcs_<id>.xml`, with the root element its `id` expects.
2. In `Main/_Module/SubModule.xml`, copy the neighbouring `<XmlNode>` with the same `id` (the troop rows start at line 180) and change only `path`: no `.xml`, no `ModuleData/`, forward slashes. [kingdom-creation](../features/kingdom-creation.md), lines 467-483, gives the three rows a new kingdom needs.
3. Keep `<IncludedGameTypes>` with at least `Campaign` and `CampaignStoryMode`, and keep any comment outside that block.
4. Rebuild (or copy the file and the manifest into `Modules/TAOM/`), then restart the game.

Check: `python tools/validate_moduledata.py`
Takes effect: full game restart
Code: No code changes needed

### Register a new Armory item folder

1. Create `LOTRLOME_Armory/ModuleData/LOTRLOME_items/<culture>/` and put the item files directly inside it; a subfolder inside it is invisible to the glob.
2. Before naming an item, grep every `LOTRLOME_items/*/` folder for the id prefix. Two loaded files defining the same id merge into one item, and nothing warns.
3. In `LOTRLOME_Armory/SubModule.xml`, copy the `arnor` block (lines 174 to 182) and change `path` to `LOTRLOME_items/<culture>`. A new file inside a folder that is already registered needs no row at all.
4. Restart the game.

Check: `python tools/validate_moduledata.py`
Takes effect: full game restart
Code: No code changes needed

### Add a native file to `project.mbproj`

1. Take the id from `Native/ModuleData/project.mbproj`. Never invent one; an unknown id is silent.
2. If `XmlSchemas/<id>.xsd` exists, do not add a second row for that id. Merge your nodes into the module's existing file for it, the way the Armory folds every action set into its single `action_sets.xml`.
3. Otherwise add `<file id="soln_<x>" name="ModuleData/<file>.xml" type="<type>" />` as a child of `<base>`, copying `type` from Native's row for the same id.
4. Restart the game.

Check: `python tools/audit_mbproj_registration.py --all`
Takes effect: full game restart
Code: No code changes needed

### Remove a registration without deleting the file

1. Delete the `<XmlNode>` or `<file>` row, or wrap it in a comment. Leave the file where it is. If a `.xsl` or `.xslt` sits beside it, that stylesheet stops running too.
2. For a file inside a folder-form registration there is no row to remove; rename the file to a non-`.xml` extension (the Armory's `.bak-<reason>-<date>` convention) or move it out of the folder.
3. Restart and confirm nothing else referenced an id the file defined.

Check: `python tools/validate_moduledata.py`
Takes effect: full game restart
Code: No code changes needed

## Gotchas: what fails silently and what crashes

- **A `path` that matches no file and no folder is not an error.** It is a stylesheet hook, and a typo behaves the same way: an empty slot, no log line. `MBObjectManager.cs:911-915`
- **The folder form globs `*.xml`, one level deep.** A backup saved with an `.xml` extension is a second copy of every id in it; a subfolder never loads. The Armory's backups use non-`.xml` extensions for this reason. `MBObjectManager.cs:903-909`; [lotrlome-soln-id-fix](../reference/lotrlome-soln-id-fix.md), lines 117-118
- **A recursive grep over a live `ModuleData/` folder reads those backups.** Resolve the file through the manifest first, then search that one path. [xslt-moduledata lessons](../reviews/lessons/xslt-moduledata.md), lines 175-194
- **A file wins over a folder of the same name.** `path="monsters"` opens `monsters.xml`, and the `Monsters/` folder beside it is reached only through its own rows. `MBObjectManager.cs:893-901`
- **A comment inside `<IncludedGameTypes>` is a startup null reference.** Comments go between `<XmlNode>` elements. `XmlResource.cs:145, 168-171`
- **Game-type strings are class names, matched exactly.** `Campaign` does not cover `CampaignStoryMode`; a case slip drops the row in that mode with no message. `GameType.cs:40`; `MBObjectManager.cs:884`
- **The right `id` with the wrong root element loads zero objects.** A root matching no registered list name returns from `LoadXml` with nothing loaded; for TAOM's 12 ids the schema pass writes a `Debug.Print` `Error:` line first, and a root that matches another registered list name has its children deserialized as that type. `MBObjectManager.cs:1107, 1324-1341, 1366-1396`
- **An invented `soln_*` id is inert.** The 2026-06 spider registered `soln_spider_*`, its usage set never loaded, and native `CreateAgent` divided by zero on spawn; two more dead rows survived until 2026-08-28. `MBObjectManager.cs:932`; [lotrlome-soln-id-fix](../reference/lotrlome-soln-id-fix.md), lines 12-45
- **`<Module>` rows in `project.mbproj` are never read**, and the audit that guards this file only sees `<file>` rows. `XmlResource.cs:117`
- **A second row for an id that has a schema crashes on any off-schema element.** `KeyNotFoundException` at startup, the elephant "Crash #3". `MBObjectManager.cs:925-929, 1001-1008, 827-856`; [lotrlome-soln-id-fix](../reference/lotrlome-soln-id-fix.md), lines 72-93
- **TAOM's own `project.mbproj` is in that class today.** It carries four `soln_voice_definitions` rows and `soln_voice_definitions.xsd` exists, so `python tools/audit_mbproj_registration.py` prints one `MERGE-RISK` warning for `TAOM` and no errors. The files merge cleanly on the current build; a new element in any of them is what would turn the warning into a crash. Output of the run on 2026-09-05: `3 module(s) with a project.mbproj audited: 0 error(s), 1 warning(s)`. <!-- measured: python tools/audit_mbproj_registration.py 2026-09-05 --> `Main/_Module/ModuleData/project.mbproj:6-9`
- **A Monster needs no native row.** Four creatures ship registered only through `<XmlName id="Monsters">`; adding a `soln_monsters` row "to be safe" starts merging that creature into the native monster table. [lotrlome-soln-id-fix](../reference/lotrlome-soln-id-fix.md), lines 97-107
- **A new file needs a full restart, and the reference validators cannot tell you that.** Registration runs once in `Module.Initialize`; naked troops with a green `validate_all_troop_refs.py` means the file was not loaded, not that the data is wrong. `Module.cs:261-267, 1031-1032`
- **Live-module manifests revert on reinstall.** `TAOM_Map/SubModule.xml`, `LOTRLOME_Armory/SubModule.xml` and both `project.mbproj` files are unversioned; the repo-side gates are `python tools/audit_mbproj_registration.py` and `python tools/check_external_xslt.py`, which today reports 17 stylesheets clean across the three modules. <!-- measured: python tools/check_external_xslt.py 2026-09-05 --> [lotrlome-soln-id-fix](../reference/lotrlome-soln-id-fix.md), lines 9-10

## Numbers in this chapter

All measured 2026-09-05. Commands that start with a module name were run from the game's `Modules/` folder; `XmlSchemas/` sits beside it at the game root.

| Number | What | Command |
|---|---|---|
| 971 | lines in `Main/_Module/SubModule.xml` | `wc -l Main/_Module/SubModule.xml` |
| 100 | `<XmlNode>` rows in `Main/_Module/SubModule.xml` | `rg -c "<XmlNode>" Main/_Module/SubModule.xml` |
| 12 | distinct `XmlName id` values in that file | `python -c "import xml.etree.ElementTree as E;print(len({n.find('XmlName').get('id') for n in E.parse('Main/_Module/SubModule.xml').findall('Xmls/XmlNode')}))"` |
| 12 / 0 | of those ids, the ones with an `XmlSchemas/<id>.xsd` at the game root; installed modules with a `ModuleData/XmlSchemas/` folder | a loop testing `XmlSchemas/<id>.xsd` for each id; `ls -d */ModuleData/XmlSchemas` from the `Modules/` folder |
| 98 / 98 / 98 / 76 / 75 | rows with `<IncludedGameTypes>`; rows naming `Campaign`, `CampaignStoryMode`, `CustomGame`, `EditorGame` | `rg -c "<IncludedGameTypes>" Main/_Module/SubModule.xml` and `rg -c 'GameType value ?= ?"<Value>"' Main/_Module/SubModule.xml` for each value |
| 0 | `<IncludedGameTypes>` blocks containing a comment | `python -c "import re;s=open('Main/_Module/SubModule.xml',encoding='utf-8-sig').read();b=re.findall(r'<IncludedGameTypes>(.*?)</IncludedGameTypes>',s,re.S);print(len(b),sum('<!--' in x for x in b))"` |
| 8 | stylesheet-only registrations in the repo | `ls Main/_Module/ModuleData/*.xslt \| wc -l` |
| 42 / 38 | `ModuleData/` subdirectories; those named in no `SubModule.xml` path | the `python -c` one-liner in the mechanisms table |
| 44 | files under `Main/Features/` that build a `ModuleDataPath` | `rg -l ModuleDataPath Main/Features \| wc -l` |
| 12 | language folders | `ls -d Main/_Module/ModuleData/Languages/*/ \| wc -l` |
| 5 | `<file>` rows in the repo's `project.mbproj` | `rg -c "<file " Main/_Module/ModuleData/project.mbproj` |
| 9 | `GetMergedXmlForNative(` call sites in `Module.cs` | `rg -c 'GetMergedXmlForNative\("' Module.cs` (decompile, v1.4.8) |
| 33 / 18 / 6 | `LOTRLOME_Armory/SubModule.xml` rows; folder-form rows; files in `LOTRLOME_items/gondor/` | `rg -c "<XmlNode>" LOTRLOME_Armory/SubModule.xml`; the file-first `python -c` one-liner in mechanism 1; `ls LOTRLOME_Armory/ModuleData/LOTRLOME_items/gondor/*.xml \| wc -l` |
| 11 / 4 | `<file>` rows in `LOTRLOME_Armory/ModuleData/project.mbproj`; `soln_monsters` rows among them | `rg -c "<file " LOTRLOME_Armory/ModuleData/project.mbproj`; `rg -c 'id="soln_monsters"' LOTRLOME_Armory/ModuleData/project.mbproj` |
| 8 | `<XmlNode>` rows in `TAOM_Map/SubModule.xml` | `rg -c "<XmlNode>" TAOM_Map/SubModule.xml` |
| 9 / 0 | `<Module>` rows and `<file>` rows in `TAOM_Map/ModuleData/project.mbproj` | `rg -c "<Module " TAOM_Map/ModuleData/project.mbproj`; `grep -c "<file " TAOM_Map/ModuleData/project.mbproj` |
| 214 to 326 | bytes in each of the nine `TAOM_Map` stub targets | `wc -c TAOM_Map/ModuleData/{physics_materials,collision_infos,face_animations,action_sets,action_types,combat_parameters,native_parameters,item_holsters,skins}.xml` |
| 10 | files in `SandBoxCore/ModuleData/items/` | `ls SandBoxCore/ModuleData/items/*.xml \| wc -l` |
| 7 | `soln_particle_systems` rows in `Native/ModuleData/project.mbproj` | `rg -c 'id="soln_particle_systems"' Native/ModuleData/project.mbproj` |
| 50 / 39 | `<file>` rows and distinct ids in `Native/ModuleData/project.mbproj` | `rg -c "<file " Native/ModuleData/project.mbproj`; `rg -o 'id="soln_[a-z_]+"' Native/ModuleData/project.mbproj \| sort -u \| wc -l` |
| 7 | installed modules with a `project.mbproj` | `ls */ModuleData/project.mbproj \| wc -l` |
| 51 / 14 | schemas in `XmlSchemas/`; `soln_*` schemas | `ls XmlSchemas/*.xsd \| wc -l`; `ls XmlSchemas/soln_*.xsd \| wc -l` |
| 0 errors, 1 warning | `python tools/audit_mbproj_registration.py` (default scope, 3 modules) | as written |
| 0 errors, 5 warnings | `python tools/audit_mbproj_registration.py --all` (7 modules; the four extra warnings are Native's own missing files) | as written |
| 17 | stylesheets reported clean by `python tools/check_external_xslt.py` (8 repo, 1 `TAOM_Map`, 8 `LOTRLOME_Armory`) | as written |

## Read next

- [lotrlome-soln-id-fix](../reference/lotrlome-soln-id-fix.md): the dead-id ledger, the `GetMergedXmlForNative` and `MergeTwoXmls` snippets, and the backup-extension rule.
- [lotrlome-warg-changes](../reference/lotrlome-warg-changes.md): section 7, registering three native files and why duplicating those ids was safe.
- [kingdom-creation](../features/kingdom-creation.md): the `SubModule.xml Registration` section a new kingdom's three rows come from.
- [xslt-moduledata lessons](../reviews/lessons/xslt-moduledata.md): the `grep -r` over backups lesson.
- [tools README](../../tools/README.md): the `audit_mbproj_registration.py` and `validate_moduledata.py` rows.
