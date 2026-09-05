# Module TAOM: the code-and-data module

## What this module is

`TAOM` is the module that carries the mod's one C# assembly (`TAOM.dll`, declared at `Main/_Module/SubModule.xml:59`) together with the campaign data the assembly does not own: cultures, kingdoms, clans, lords, troops, equipment rosters, strings, UI, sounds and a handful of cooked assets. Its source is the repo folder `Main/_Module/`, and the build copies that folder into the game install as `Modules/TAOM/` without ever deleting anything there (`CopyModule` runs with `Clean="false"`, Bannerlord.BuildResources `Basic.targets:65`). It depends on the four vanilla modules `Native`, `SandBoxCore`, `Sandbox` and `CustomBattle` (`Main/_Module/SubModule.xml:11-14`); the map lives in [TAOM_Map](module-map.md), the items in [LOTRLOME_Armory](module-armory.md) and the libraries in [TAOM.Dependencies](module-dependencies.md).

## Where it lives and how the engine finds it

- **Repo:** [`Main/_Module/`](../../Main/_Module/SubModule.xml) is the module root. The csproj sets `<ModuleId>$(MSBuildProjectName)</ModuleId>` and `<ModuleName>` to the same value (`Main/TAOM.csproj:7-8`), so the deployed folder is named `TAOM`, and the manifest's `<Id value="TAOM" />` (`Main/_Module/SubModule.xml:4`) matches it.
- **Install:** `Modules/TAOM/` under the game folder that `BANNERLORD_GAME_DIR` names. `Directory.Build.props:37-38` resolves `$(GameFolder)` from `BANNERLORD_OVERRIDE_DIR` when that folder holds `bin\Win64_Shipping_Client\Bannerlord.exe`, else from `BANNERLORD_GAME_DIR`.
- **What makes it a module:** `ModuleHelper.GetPhysicalModules` lists every directory under `Modules/` and keeps the ones where `SubModule.xml` exists (`ModuleHelper.cs:322-331`); everything else is skipped. `ModuleInfo.LoadWithFullPath` then reads `<Name>`, `<Id>` and `<Version>` with no null check (`ModuleInfo.cs:81-87`), so a manifest missing any of the three throws and the module does not exist as far as the game is concerned.
- **Load order is the launcher's sorted list.** `Module.Initialize` hands the launcher's id list to `ModuleHelper.InitializeModules` (`Module.cs:261`), which inserts modules in that order (`ModuleHelper.cs:85-99`), and `GetModules()` returns that insertion order (`ModuleHelper.cs:178-189`). The engine's own sort, `GetSortedModules` (`ModuleHelper.cs:271-280`), runs only for multiplayer (`CustomBattleServer.cs:208`, `LobbyClient.cs:474`), but the vanilla launcher topology-sorts its list over `<DependedModules>` and `<ModulesToLoadAfterThis>` every time it builds or reorders it (`LauncherModsVM.cs:34, 158, 195`, edges from `ModuleHelper.cs:252-268`), so both elements fix the relative order the game receives. The walk from `LauncherData.xml` to `LoadSubModules` is in [modules-overview](modules-overview.md), "Where the launcher load order really comes from"; what that order means for XML merging is in [load-order-and-dependencies](load-order-and-dependencies.md).
- **Three things happen at process start, in this order:** `LoadLocalizationXmls` (`Module.cs:262`), `GlobalTextManager.LoadDefaultTexts` (`Module.cs:263`), then `LoadSubModules` (`Module.cs:267`), which registers every module's `project.mbproj` rows and `<Xmls>` rows before it loads a single DLL (`Module.cs:1029-1033`).

## Folder by folder

The tree below is measured from the repo copy. Sizes are `du -sm`, rounded up to whole megabytes.

<!-- measured: ls -la Main/_Module; for d in Main/_Module/*/; do find "$d" -type f | wc -l; du -sm "$d"; done; wc -l -c Main/_Module/SubModule.xml; rg -o "<XmlNode>" Main/_Module/SubModule.xml | wc -l; wc -l Main/_Module/THIRD-PARTY-LICENSES.txt; git ls-files Main/_Module/bin 2026-09-05 -->
```
Main/_Module/                      978 MB in total
  SubModule.xml                    37,947 bytes, 971 lines, 100 <XmlNode> rows
  THIRD-PARTY-LICENSES.txt         117 lines
  AssetPackages/                   4 files, 35 MB
  AssetSources/                    86 files, 551 MB
  Assets/                          121 files, 1 MB
  GUI/                             1,319 files, 178 MB
  ModuleData/                      367 files, 32 MB
  ModuleSounds/                    436 files, 167 MB
  Prefabs/                         1 file
  bin/                             7 files, 17 MB (2 of them git-tracked)
```

| Folder or file | What it holds | Who reads it, and where |
|---|---|---|
| `SubModule.xml` | The manifest: identity, dependencies, the one `<SubModule>`, and 100 `<XmlNode>` data registrations across 12 `XmlName id` values <!-- measured: rg -o 'XmlName id="[^"]+"' Main/_Module/SubModule.xml \| sort -u \| wc -l 2026-09-05 --> | `ModuleInfo.LoadWithFullPath` opens `FolderPath + "/SubModule.xml"` (`ModuleInfo.cs:75-79`); `XmlResource.GetXmlListAndApply` opens the same file again to read `Module/Xmls/XmlNode` (`XmlResource.cs:144-149`) |
| `THIRD-PARTY-LICENSES.txt` | Redistribution notices; MinHook 1.3.4 is at line 22 <!-- measured: rg -n "MinHook" Main/_Module/THIRD-PARTY-LICENSES.txt 2026-09-05 --> | Nothing at runtime. `tools/package_release.py:65-68` lists it in `KNOWN_TOP_FILES` so it ships |
| `bin/Win64_Shipping_Client/` | `TAOM.dll` and `TAOM.pdb` (build output, ignored by git) plus the two vendored natives `MinHook.x64.dll` and `TAOM.NativeSkinFixes.dll` (tracked) and its `.pdb` | `Module.LoadSubModules` loads from `Path.Combine(FolderPath, "bin", Common.ConfigName)` (`Module.cs:1044`), where `Common.ConfigName` is the name of the process's current working directory (`Common.cs:37`). `SubModuleInfo.LoadFrom` separately probes the literal `bin\Win64_Shipping_Client` for its `DLLExists` flag (`SubModuleInfo.cs:54-57`) |
| `bin/Gaming.Desktop.x64_Shipping_Client/` | A second copy of `TAOM.dll` and `TAOM.pdb` | Written only by `CopyBinariesToModuleFolder` (`Main/TAOM.csproj:148,152-153`); the whole folder is ignored (`.gitignore:82`) |
| `ModuleData/` | 39 loose files at the root and 42 subfolders <!-- measured: find Main/_Module/ModuleData -maxdepth 1 -type f \| wc -l; find Main/_Module/ModuleData -mindepth 1 -maxdepth 1 -type d \| wc -l 2026-09-05 --> | A registered `path="X"` resolves to `<module>/ModuleData/X.xml` (`ModuleHelper.cs:232-235`); a stylesheet to `ModuleData/X.xsl`, then `.xslt` (`ModuleHelper.cs:237-240`, `MBObjectManager.cs:949-964`) |
| `ModuleData/project.mbproj` | 5 `<file>` rows: 4 voice definitions and `module_sounds.xml` <!-- measured: rg -c "<file " Main/_Module/ModuleData/project.mbproj 2026-09-05 --> | `XmlResource.GetMbprojxmls` (`XmlResource.cs:107-140`), called at `Module.cs:1031`. See the second channel below |
| `ModuleData/Languages/` | 169 files: the root `language_data.xml`, then 12 language folders each holding its own `language_data.xml` plus 13 `std_taom_*.xml` files <!-- measured: find Main/_Module/ModuleData/Languages -type f \| wc -l; for d in Main/_Module/ModuleData/Languages/*/; do ls $d \| wc -l; done 2026-09-05 --> | `LocalizedTextManager.LoadLocalizationXmls` searches `ModuleData/Languages` recursively for files named exactly `language_data.xml` (`LocalizedTextManager.cs:91-99`). No manifest row is involved |
| `ModuleData/<feature>/` (36 folders) | JSON and a few XML configs read by TAOM's own C#, none of them named in `SubModule.xml` | See "Folders the engine never registers" below |
| `GUI/` | `Brushes/` 11, `Fonts/` 6, `Prefabs/` 51, `SpriteData/` 227, `SpriteParts/` 1,023 files, and the generated manifest `TAOMSpriteData.xml` (428,445 bytes) <!-- measured: for d in Main/_Module/GUI/*/; do find "$d" -type f \| wc -l; done; wc -c Main/_Module/GUI/TAOMSpriteData.xml 2026-09-05 --> | Gauntlet. `SpriteParts/` is the source (`Config.xml` plus 1,022 PNGs in 5 categories), `TAOMSpriteData.xml` and `AssetSources/GauntletUI/` are what the sprite generator writes, `Assets/GauntletUI/` is the texture compile that follows (`docs/features/gui-sprite-system.md:110-116`) |
| `GUI/SpriteData/FactionMap/` | 226 loose PNGs <!-- measured: find Main/_Module/GUI/SpriteData/FactionMap -type f \| wc -l 2026-09-05 --> | Not sprites. `FactionImageWidget` loads them by path from the install (`docs/features/gui-sprite-system.md:59-84`) |
| `Assets/` | 121 compiled per-asset `.tpac` descriptors under `GauntletUI/`, `BannerIcons/`, `main_map_textures/`, plus one stray `taom_banners_dunland_alpha_01_tex.tpac.ptemp` <!-- measured: find Main/_Module/Assets -type f \| wc -l; ls Main/_Module/Assets/BannerIcons/*.ptemp 2026-09-05 --> | The editor and the game on a dev install (`docs/reference/bannerlord-engine-and-toolchain.md:266`) |
| `AssetSources/` | 86 raw art files (PSD and PNG), 551 MB | Nobody at runtime. `tools/package_release.py:116-117` excludes the whole folder from a release; the dev build still deploys it (`Main/TAOM.csproj:13`) |
| `AssetPackages/` | 4 cooked packs: `fieldcamp_camp_a`, `fieldcamp_palisade_ring`, `refuge_camp_a`, `refuge_palisade_ring` <!-- measured: ls Main/_Module/AssetPackages 2026-09-05 --> | Players; the runtime form in a release (`docs/reference/bannerlord-engine-and-toolchain.md:267`) |
| `ModuleSounds/` | 342 `.wav`, 93 `.mp3`, 1 `.ogg` under `LOTR/` and `Native/` <!-- measured: find Main/_Module/ModuleSounds -type f \| sed 's/.*\.//' \| sort \| uniq -c 2026-09-05 --> | `ModuleData/module_sounds.xml` names each file with a `path=` relative to this folder, for example `path="LOTR/Elves/Alert/elf_horn.wav"` (`Main/_Module/ModuleData/module_sounds.xml:5`) |
| `Prefabs/` | `taom_howdah_agent.xml`, 8,647 bytes <!-- measured: wc -c Main/_Module/Prefabs/taom_howdah_agent.xml 2026-09-05 --> | The scene system, with no manifest row |

Three folders exist only in the install copy, never in the repo <!-- measured: ls "<game>/Modules/TAOM"; ls "<game>/Modules/TAOM/RuntimeDataCache" \| wc -l; du -sm "<game>/Modules/TAOM/RuntimeDataCache" 2026-09-05 -->:

- `RuntimeDataCache/`: 115 entries, 5,141 MB, written by the editor build. `tools/package_release.py:104-113` excludes it from a release because the shipping client cannot write it.
- `Shaders/D3D11/shader_compile_report.log`: written by the engine's shader compile.
- `bin/Win64_Shipping_wEditor/` (12 files) and `bin/Win64_Shipping_Server/` (10 files): mirrors of the assembled client folder, produced by the two mirror targets in `Main/TAOM.csproj:166-195`. <!-- measured: for d in "<game>/Modules/TAOM"/bin/*/; do ls "$d" \| wc -l; done 2026-09-05 -->

## How build.ps1 deploys it, and why deploy never deletes

[`build.ps1`](../../build.ps1) is 43 lines and copies nothing itself. <!-- measured: cat -n build.ps1 2026-09-05 --> It reads the user-scope environment variable `BANNERLORD_GAME_DIR` and exits 1 if it is unset (`build.ps1:11-15`), runs `dotnet restore TAOM.sln` (`build.ps1:20`), `dotnet build TAOM.sln -c $Configuration --no-restore` (`build.ps1:27`), and with `-RunTests` runs `dotnet test TAOM.Tests -c $Configuration --no-build` (`build.ps1:37`). Every copy into the game install is MSBuild, from the `Bannerlord.BuildResources` 1.1.0.129 package (`Main/TAOM.csproj:95`) and from four targets TAOM adds in its own csproj.

**The package's three copy targets** (`Basic.targets` in the package's `build/` folder):

| Target | Line | What it does |
|---|---|---|
| `PostBuildCopyToModules` | 47-51 | A wrapper gated on `ModuleId != ''`, the game folder existing, and `DisableModuleCopy != 'true'`; it calls the two below |
| `CopyBinariesWindows` | 53-57 | Copies `$(TargetDir)` into `Modules/TAOM/bin/Win64_Shipping_Client` with `Regex=".*\.dll\|.*\.pdb\|.*\.config$"` (`Basic.props:6`) and `Clean="true"`. Clean deletes only destination files that match that regex, so a stale `.exp` or `.lib` in the install is never removed |
| `CopyModule` | 64-65 | Copies `$(ProjectDir)/_Module` into `Modules/TAOM` with `Regex="^.*$"` and **`Clean="false"`**. Nothing is ever deleted on the install side. `ExcludeSourceFiles` defaults to `true` (`Basic.props:7`), which would skip `_Module/Assets`, `_Module/AssetSources` and `_Module/GUI/SpriteParts` (`Basic.targets:155`), but `Main/TAOM.csproj:13` sets it to `false`, so all three deploy |

After the copy, `ReplaceFileText` rewrites the deployed `SubModule.xml`, substituting `$moduleid$`, `$version$` and the other tokens (`Basic.targets:114-118`). TAOM uses none of them (its `<Version>` is a literal at `Main/_Module/SubModule.xml:6`), so the repo and install copies of the manifest are byte-identical. <!-- measured: cmp Main/_Module/SubModule.xml "<game>/Modules/TAOM/SubModule.xml" 2026-09-05 -->

**TAOM's own four targets** (`Main/TAOM.csproj`):

- `FailOnIdeStateInModule` (`:137-144`) fails the build if a `.vs` folder sits anywhere under `_Module`, because with the package's exclusion list switched off `CopyModule` would ship it, absolute developer paths included (`:126-132`).
- `CopyBinariesToModuleFolder` (`:146-158`) copies the freshly built `TAOM.dll` and `.pdb` back into the repo's `_Module/bin/Win64_Shipping_Client/` and `_Module/bin/Gaming.Desktop.x64_Shipping_Client/`. It is `BeforeTargets="PostBuildCopyToModules"`, and MSBuild runs such a dependency before it evaluates the host target's own condition (`:133-136`), so this one runs even when deployment is skipped.
- `MirrorWin64ShippingClientToEditor` (`:166-175`) and `MirrorWin64ShippingClientToServer` (`:186-195`) copy the assembled install-side client folder into `bin/Win64_Shipping_wEditor/` and `bin/Win64_Shipping_Server/`. They mirror the assembled folder rather than the build output on purpose: that is where the vendored natives and the NuGet companions are (`:183-185`). Without the editor mirror the Modding Kit runs a stale DLL (`:160-164`); without the server mirror a dedicated server logs `Cannot find: ...\TAOM\bin\Win64_Shipping_Server\TAOM.dll` and runs a vanilla simulation over the TAOM map (`:177-181`).

**`-p:DisableModuleCopy=true` does not stop deployment.** Only the wrapper at `Basic.targets:47` checks that flag; `CopyBinariesWindows` (`:53`) and `CopyModule` (`:64`) each carry their own `AfterTargets="PostBuildEvent"` with conditions that omit it. While the game is running the copy fails on the DLLs it holds open. The command that skips all three copies is `dotnet build Main -p:DisableModuleCopy=true -p:ModuleId=`, because `ModuleId` is in every condition (`docs/ai-includes/agent-operating-manual.md:49-51`).

**Proof that deploy is additive.** The install's `ModuleData` holds 371 files to the repo's 367; the four extras are `.gitkeep`, `Languages/SP.zip`, `troops/troops_bluecraig.xml` and `troops/troops_mistymountainorcs.xml`, all deleted from the repo at some point and still on disk in the install. <!-- measured: find "<game>/Modules/TAOM/ModuleData" -type f \| wc -l; diff <(cd Main/_Module/ModuleData && find . -type f \| sort) <(cd "<game>/Modules/TAOM/ModuleData" && find . -type f \| sort) 2026-09-05 --> Neither troop file is registered in the current manifest, so both are inert; a stale file whose `<XmlName>` row still existed would load. The install's client `bin` also still carries `TAOM.NativeSkinFixes.exp` and `.lib`, and the `wEditor` mirror still carries `BehaviorTrees.dll` and `BehaviorTreeWrapper.dll`, which were folded into `TAOM.dll` on 2026-05-24 (`Main/TAOM.csproj:66-70`). <!-- measured: ls "<game>/Modules/TAOM/bin/Win64_Shipping_Client" "<game>/Modules/TAOM/bin/Win64_Shipping_wEditor" 2026-09-05 -->

**How a change reaches the game:**

| You changed | Do this | Then |
|---|---|---|
| Any `ModuleData` XML, XSLT or JSON | `./build.ps1` (or copy the file by hand) | Full game restart. `<Xmls>` rows are read once at process start (`Module.cs:1029-1033`) and directories are globbed when the data loads (`MBObjectManager.cs:900-910`); nothing reloads |
| C# | `./build.ps1` | Full game restart; the DLL lands in all four `bin/` folders |
| A prefab under `GUI/Prefabs/` | Copy the one file into the install | No bake; prefabs load at runtime (`docs/features/gui-sprite-system.md:166`) |
| A sprite PNG under `GUI/SpriteParts/` | Deploy, run the sprite generator against the install, then `pwsh tools/sync_sprite_bake.ps1` to pull exactly the manifest and the two `GauntletUI` folders back into the repo | Fully exit and relaunch: a rebake moves existing sprites to new atlas rects (`docs/features/gui-sprite-system.md:138, 167-169`) |
| A `GUI/SpriteData/FactionMap/*.png` | Copy it into the install | Nothing else; the widget reads the install path (`docs/features/gui-sprite-system.md:59-84`) |

## The two registration channels

A file under `ModuleData/` reaches the engine through one of two manifests, and the two never overlap. The general mechanism (folder-form globbing, game-type filters, the `<Module>` versus `<file>` mistake) is in [submodule-and-registration](submodule-and-registration.md); this section is what TAOM's copy of each file contains and what the engine reads from it.

### Channel 1: `SubModule.xml`

The `<Module>` root is parsed by `ModuleInfo.LoadWithFullPath`. `<DependedModuleMetadatas>` is not in this table because the method has no branch for it (`ModuleInfo.cs:105-166`). The TaleWorlds engine never reads that block; BUTR/BLSE launchers and the ButterLib, MCM and UIExtenderEx DLLs that TAOM.Dependencies ships do, which is the [module-dependencies](module-dependencies.md) chapter's story. The manifest's own comment at `Main/_Module/SubModule.xml:32-37` names the launcher half.

<!-- engine-table type="TaleWorlds.ModuleManager.ModuleInfo" file="Platform/TaleWorlds.ModuleManager/TaleWorlds.ModuleManager/ModuleInfo.cs" method="LoadWithFullPath" inert="" -->

| Element | Type | Required | Default when absent | What it does | Read at (file:line) |
|---|---|---|---|---|---|
| `Name@value` | string | yes | throws | Launcher display name. TAOM: `TAOM` (`SubModule.xml:5`) | `ModuleInfo.cs:81` |
| `Id@value` | string | yes | throws | The module identity every lookup keys on, lower-cased (`ModuleHelper.cs:87, 95-97`); dependency edges compare it with ordinal `==`, so a `<DependedModule Id>` must match it letter for letter (`ModuleHelper.cs:105, 256, 264`). TAOM: `TAOM` (`SubModule.xml:4`) | `ModuleInfo.cs:82` |
| `Version@value` | version string | yes | throws | Parsed with `ApplicationVersion.FromString`. TAOM: `v2.0.28` (`SubModule.xml:6`) | `ModuleInfo.cs:87` |
| `RequiredBaseVersion@value` | version string | no | not set | Parsed if present; the only consumer compares it for the module named `NavalDLC` (`ModuleHelper.cs:73-80`). Absent in TAOM | `ModuleInfo.cs:88-91` |
| `DefaultModule@value` | literal `true` | no | `false` | Case-sensitive `Equals("true")`. TAOM: `false` (`SubModule.xml:7`) | `ModuleInfo.cs:92` |
| `ModuleType@value` | enum | no | unchanged | `Enum.TryParse`. TAOM: `Community` (`SubModule.xml:9`), which keeps the declared `<Version>` intact; official modules get their changeset overwritten in `InitializeModules` (`ModuleHelper.cs:90-93`) | `ModuleInfo.cs:93-97` |
| `ModuleCategory@value` | enum | no | `Singleplayer` | Set to `Singleplayer` before the node is read. TAOM: `Singleplayer` (`SubModule.xml:8`) | `ModuleInfo.cs:99-104` |
| `DependedModules/DependedModule@Id` | string | yes per row | throws per row | Hard dependency the launcher enforces. TAOM: `Native`, `SandBoxCore`, `Sandbox`, `CustomBattle` (`SubModule.xml:11-14`). `Sandbox` is the id the vanilla folder `SandBox` declares at `SandBox/SubModule.xml:4` | `ModuleInfo.cs:105-110, 128` |
| `DependedModule@DependentVersion` | version string | no | `ApplicationVersion.Empty` | Parsed inside a try; a bad value falls back silently | `ModuleInfo.cs:113-123` |
| `DependedModule@Optional` | bool | no | `false` | `bool.TryParse` | `ModuleInfo.cs:124-127` |
| `ModulesToLoadAfterThis/Module@Id` | string | no | none | A launcher-sort edge: the named module is placed after this one on every list build and drag (`ModuleHelper.cs:262-268`, `LauncherModsVM.cs:158, 195`); an id that is not installed is never matched. TAOM: `BannerlordTogether`, `BattleLinkMPClient`, `Coop` (`SubModule.xml:48-55`) | `ModuleInfo.cs:131-139` |
| `IncompatibleModules/Module@Id` | string | no | none | Absent in TAOM | `ModuleInfo.cs:140-148` |
| `SubModules/SubModule` | element | no | none | Each handed to `SubModuleInfo.LoadFrom` inside a try that swallows the exception and still adds the entry | `ModuleInfo.cs:149-166` |

`SandBox/SubModule.xml` is a vanilla path. This file lives in the game install, not the repo; a module reinstall reverts hand edits, so land a repo-side validator gate with any fix.

The single `<SubModule>` block (`Main/_Module/SubModule.xml:57-65`):

<!-- engine-table type="TaleWorlds.ModuleManager.SubModuleInfo" file="Platform/TaleWorlds.ModuleManager/TaleWorlds.ModuleManager/SubModuleInfo.cs" method="LoadFrom" inert="" -->

| Element | Type | Required | Default when absent | What it does | Read at (file:line) |
|---|---|---|---|---|---|
| `Name@value` | string | yes | throws | Stored as a label. TAOM: `TAOM` | `SubModuleInfo.cs:51` |
| `DLLName@value` | file name | yes | throws | Probed at the literal `bin\Win64_Shipping_Client\<name>` to set `DLLExists` and the certificate flag; the real load path is `bin/<ConfigName>/` (`Module.cs:1044-1046`). TAOM: `TAOM.dll` | `SubModuleInfo.cs:52-64` |
| `SubModuleClassType@value` | type name | yes | throws | The `MBSubModuleBase` the engine instantiates. TAOM: `TAOM.SubModule` (`Main/SubModule.cs`) | `SubModuleInfo.cs:65` |
| `Assemblies/Assembly@value` | file name | no | none | Extra DLLs loaded before the main one from the same folder (`Module.cs:1048-1057`). Absent in TAOM | `SubModuleInfo.cs:66-74` |
| `Tags/Tag@key`, `@value` | enum key, string | no | none | Key must parse as a `SubModuleTags` member or the tag is dropped. `DedicatedServerType` with any value other than `none` forces `IsTWCertifiedDLL = true`. TAOM: `DedicatedServerType=none`, `IsNoRenderModeElement=false` (`SubModule.xml:61-64`) | `SubModuleInfo.cs:75-92` |

The `<Xmls>` block is not read by `ModuleInfo` at all. `XmlResource.GetXmlListAndApply` opens the manifest a second time (`XmlResource.cs:144-148`) and turns each `<XmlNode>` into one `MbObjectXmlInformation` record.

<!-- engine-table type="TaleWorlds.ObjectSystem.XmlResource" file="Core/TaleWorlds.ObjectSystem/TaleWorlds.ObjectSystem/XmlResource.cs" method="GetXmlListAndApply" inert="" -->

| Element | Type | Required | Default when absent | What it does | Read at (file:line) |
|---|---|---|---|---|---|
| `Module/Xmls/XmlNode` | element path | `<Module>` yes (dereferenced with no null check); `<Xmls>` no | a root other than `<Module>` throws; no `<Xmls>` means no rows | The rows the loop walks: every `<XmlNode>` under `<Xmls>` under the `<Module>` root | `XmlResource.cs:149` |
| `XmlName@id` | object-type id | yes | throws | Selects which `LoadXML("<id>")` call picks the row up and which `XmlSchemas/<id>.xsd` validates it (`ModuleHelper.cs:247-250`). TAOM uses 12 ids, and all 12 have a schema in the install's `XmlSchemas/` (51 schemas) <!-- measured: ls "<game>/XmlSchemas" \| wc -l; for id in Kingdoms SPCultures Factions NPCCharacters Heroes BodyProperties EquipmentRosters partyTemplates SkillSets GameText BannerIcons CustomBattleScenes; do test -f "<game>/XmlSchemas/$id.xsd"; done 2026-09-05 --> | `XmlResource.cs:157` |
| `XmlName@path` | path without `.xml` | yes | throws | Resolved to `ModuleData/<path>.xml` (`ModuleHelper.cs:234`). If that file is absent but a directory of that name exists, every `*.xml` in it is loaded (`MBObjectManager.cs:900-910`); if neither exists the row still contributes an empty slot and its stylesheet still runs (`MBObjectManager.cs:911-915`). TAOM's 8 `.xslt` files at the `ModuleData` root have no `.xml` sibling and work through that last branch <!-- measured: for x in Main/_Module/ModuleData/*.xslt; do test -f "${x%.xslt}.xml" \|\| echo xslt-only; done 2026-09-05 --> | `XmlResource.cs:158` |
| `IncludedGameTypes/*/@value` | game-type class name | no | empty list, which means every game type (`MBObjectManager.cs:884`) | Every child node's `value` attribute is read; a comment inside the element has no attributes and throws at startup | `XmlResource.cs:165-172` |

How the 100 rows split by id: `NPCCharacters` 44, `EquipmentRosters` 27, `GameText` 15, and 2 each of `SkillSets`, `SPCultures`, `Kingdoms`, `Heroes`, `Factions`, then 1 each of `partyTemplates`, `BodyProperties`, `BannerIcons`, `CustomBattleScenes`. <!-- measured: rg -o 'XmlName id="[^"]+"' Main/_Module/SubModule.xml | sort | uniq -c | sort -rn 2026-09-05 --> Two comment sentinels mark the regions the authoring tools rewrite: `TAOM-NEWFACTIONS-REG` at lines 348 and 394, `TAOM-NEWCULTURE-REG` at lines 395 and 441. <!-- measured: rg -n "TAOM-NEW(FACTIONS|CULTURE)-REG" Main/_Module/SubModule.xml 2026-09-05 --> The last two rows, `BannerIcons` and `CustomBattleScenes` (`SubModule.xml:964-969`), deliberately carry no `<IncludedGameTypes>` so they load everywhere.

### Channel 2: `ModuleData/project.mbproj`

This looks like a Modding Kit project file and is also a runtime registry. `Module.LoadSubModules` calls `XmlResource.GetMbprojxmls(module.Id)` one line before it reads the `<Xmls>` block (`Module.cs:1031-1032`). The path is `<module>/ModuleData/project.mbproj` (`ModuleHelper.cs:201-209`), and only `<file>` children of `<base>` are read (`XmlResource.cs:117`).

<!-- engine-table type="TaleWorlds.ObjectSystem.XmlResource" file="Core/TaleWorlds.ObjectSystem/TaleWorlds.ObjectSystem/XmlResource.cs" method="GetMbprojxmls" inert="" -->

| Attribute | Type | Required | Default when absent | What it does | Read at (file:line) |
|---|---|---|---|---|---|
| `base/file` | element path | `<base>` yes (dereferenced with no null check); `<file>` no | a root other than `<base>` throws; no `<file>` rows registers nothing | Only `<file>` children of `<base>` are visited. A row written as `<Module>` (the mistake the live `TAOM_Map` copy makes) is never seen | `XmlResource.cs:117` |
| `file@id` | `soln_*` id | yes | throws | Matched by exact string against the eight ids the engine asks for in `Module.cs` (`soln_skins` :1366, `soln_item_holsters` :1378, `soln_action_sets` :1389, `soln_action_types` :1419, `soln_animations` :1430, `soln_voice_definitions` :1449, `soln_sound_event_data` :1482, `soln_sound_parameter_data` :1493) plus a native callback building `"soln_" + xmlType` (:1504). An id nobody asks for is never opened and nothing logs it | `XmlResource.cs:124` |
| `file@name` | path with extension, relative to the module root | yes | throws | Resolved as module root + name (`ModuleHelper.cs:211-214`), the opposite convention from `XmlName@path`. Its stylesheet is `name` minus the last four characters plus `.xsl` (`ModuleHelper.cs:221-225`) | `XmlResource.cs:125` |

The three `<outputDirectory>`, `<XMLDirectory>` and `<ModuleAssemblyDirectory>` elements at `project.mbproj:3-5` are not selected by the loop either.

TAOM's five rows are its only registration for `lotr_uruk_voice_def.xml`, `lotr_dwarf_voice_def.xml`, `lotr_uruk_hai_voice_def.xml`, `VoiceDefinitions/LOTR/lotr_warg_voice_def.xml` and `module_sounds.xml`. None of those five names appears anywhere in `SubModule.xml`, so dropping `project.mbproj` from a release would silently kill every custom voice and sound while the build and the tests stayed green; `tools/package_release.py:126-135` carries a standing note never to exclude it. Two more facts about those rows:

- `soln_voice_definitions.xsd` exists in the install's `XmlSchemas/` and `soln_module_sound.xsd` does not. <!-- measured: test -f "<game>/XmlSchemas/soln_voice_definitions.xsd"; test -f "<game>/XmlSchemas/soln_module_sound.xsd" 2026-09-05 --> With a schema present, `GetMergedXmlForNative` keeps the schema path (`MBObjectManager.cs:925-929`) and the four voice rows merge through `MergeElements` instead of being appended; `python tools/audit_mbproj_registration.py --module TAOM` reports exactly that as one `MERGE-RISK` warning and zero errors. <!-- measured: python tools/audit_mbproj_registration.py --module TAOM 2026-09-05 --> After the merge, `CreateProcessedVoiceDefinitionsXMLForNative` folds every `voice_type_declarations` block into the first and merges `voice_definition` elements whose first attribute matches (`Module.cs:1447-1470`).
- The `<file>` rows carry a `type` attribute the engine never reads, so a wrong `type` is invisible and a wrong `id` is invisible; only the audit tool sees either.

### Files the engine opens with no registration at all

- **`ModuleData/Languages/**/language_data.xml`**: found by a recursive file search (`LocalizedTextManager.cs:99`). Details under "Add a new language folder" below.
- **`ModuleData/global_strings.xml`**: `GameTextManager.LoadDefaultTexts` opens `FolderPath + "/ModuleData/global_strings.xml"` for every module (`GameTextManager.cs:132-139`), separate from the `GameText` merge at `GameTextManager.cs:117`. TAOM's is 2,589 bytes. <!-- measured: wc -c Main/_Module/ModuleData/global_strings.xml 2026-09-05 -->
- **`ModuleData/sp_battle_scenes.xml`**: vanilla `Campaign.InitializeScenes` builds the literal path `ModuleData/sp_battle_scenes.xml` for every active module (`Campaign.cs:1330-1336`). TAOM re-points the load at its own copy in `Main/Features/BattleScenes/Hooks/Campaign_InitializeScenes_Patch.cs:16-22`.

## Folders the engine never registers: the C#-loaded data

Of the 42 subfolders under `ModuleData/`, 4 are reached through `<XmlNode>` rows (`characters/`, `troops/`, `equipmentsets/`, `named_companions/`), `VoiceDefinitions/` through `project.mbproj`, `Languages/` through the file search, and the remaining 36 are opened by TAOM's own code. <!-- measured: for d in Main/_Module/ModuleData/*/; do n=${d%/}; rg -q "path=\"$(basename $n)/" Main/_Module/SubModule.xml && echo REGISTERED \|\| echo CODE; done 2026-09-05 --> The table names the first C# file that mentions each folder; the per-file loader, reload scope and MCM precedence for all of them is the master table in [file-catalogue](file-catalogue.md). <!-- measured: for each folder, rg -l --glob '*.cs' -F '"<folder>"' Main 2026-09-05 -->

| `ModuleData/` folder | Loader (first match under `Main/Features/`) |
|---|---|
| `TroopWeights/` | `TroopWeight/TroopWeightXmlLoader.cs` |
| `alignment_desertion/` | `AlignmentDesertion/AlignmentDesertionConfigProvider.cs` |
| `bandit_management/` | `BanditManagement/BanditScalingConfigProvider.cs` |
| `banner_bearers/` | `BannerBearers/BannerBearerConfigProvider.cs` |
| `caravan_trade/` | `CaravanTrade/CaravanTradeConfigProvider.cs` |
| `career_system/` | `CareerSystem/CareerConfigProvider.cs`, `CareerSystem/CareerQuestConfigProvider.cs` |
| `castle_recruitment/` | `CastleRecruitment/CastleRecruitmentConfigProvider.cs` |
| `charactercreation/` | `CharacterCreation/CCBodyPropertiesProvider.cs`, `CharacterCreation/CareerMenuDataProvider.cs` |
| `clan_heraldry/` | not found by a grep for the folder name; see [file-catalogue](file-catalogue.md) |
| `combat_mechanics/` | `CombatMechanics/CombatMechanicsConfigProvider.cs` |
| `configs/` | one provider per file: `ArmyTargeting/ArmyTargetingConfigProvider.cs:28`, `BattleBalance/BattleBalanceConfigProvider.cs:29`, `EditorCacheRebuild/CacheRebuildConfigProvider.cs:36`, `RevoltTuning/RevoltTuningConfigProvider.cs:27`, and others |
| `culture_conversion/` | `CultureConversion/CultureConversionConfigProvider.cs` |
| `culture_marketplace/` | `CultureMarketplace/CultureMarketplaceConfigProvider.cs` |
| `custom_battle/` | `CustomBattles/Config/CustomBattleCommandersProvider.cs` |
| `diplomacy/` | `Diplomacy/DiplomacyConfigProvider.cs`, `Diplomacy/WarOfTheRingConfigProvider.cs` |
| `dread_aura/` | `DreadAura/DreadAuraConfigProvider.cs` |
| `elite_emissary/` | `EliteEmissary/EliteEmissaryConfigProvider.cs` |
| `enlistment/` | `Enlistment/Content/EnlistmentContentConfigProvider.cs` |
| `execution/` | `Execution/AlignmentConfigProvider.cs` |
| `factionmap/` | `FactionMap/FactionConfigProvider.cs` |
| `field_commission/` | `FieldCommission/FieldCommissionConfigProvider.cs` |
| `lotr_issues/` | `LotrIssues/LotrIssueConfigProvider.cs` |
| `messengers/` | `Messengers/MessengerConfigProvider.cs` |
| `momentum/` | `WarOfTheRingMomentum/MomentumConfigProvider.cs` |
| `naval_travel/` | `NavalTravel/NavalTravelConfigProvider.cs` |
| `raceage/` | `RaceAge/RaceAgeConfigProvider.cs` |
| `recruitment_alignment/` | `AlignmentRecruitment/RecruitmentAlignmentConfigProvider.cs` |
| `recruitment_pools/` | `TroopProgression/GondorRecruitmentJsonLoader.cs` |
| `settlement_economy/` | `SettlementEconomy/SettlementEconomyConfigProvider.cs` |
| `settlement_food/` | `SettlementFood/SettlementFoodConfigProvider.cs` |
| `settlement_guards/` | `SettlementGuards/SettlementGuardConfigProvider.cs` |
| `shader_precompilation/` | `ShaderPrecompilation/PrecompileSceneProvider.cs` |
| `siege/` | `Siege/SiegeDefenseConfigProvider.cs` |
| `special_resources/` | `SpecialResources/SpecialResourceConfigProvider.cs` |
| `startup_resources/` | `StartupResources/StartupResourcesConfigProvider.cs` |
| `uncapturable_heroes/` | `UncapturableHeroes/UncapturableHeroesConfigProvider.cs` |

Two loose root files are registered nowhere and loaded by nothing: `settlements.xml` (1,023,041 bytes) and `custom_settlements.xml` (51,752 bytes). <!-- measured: wc -c Main/_Module/ModuleData/settlements.xml Main/_Module/ModuleData/custom_settlements.xml; rg -n "settlements" Main/_Module/SubModule.xml 2026-09-05 --> The live `Settlements` registration belongs to `TAOM_Map/SubModule.xml`; the repo copy is the stale shadow the [settlements](settlements.md) chapter warns about, and it ships only because `CopyModule` copies everything.

MCM settings are not in the module either. `TaomSettings.FolderName` is `"TAOM"` (`Main/Features/TaomSettings.cs:15`), which places the persisted values under the player's `Documents\Mount and Blade II Bannerlord\Configs\ModSettings\Global\TAOM\`. MCM itself ships from TAOM.Dependencies, and TAOM adds no MCM file to its tree (`docs/features/mcm.md:7, 45-47`). A persisted MCM value survives a reinstall of this module.

## Vendored DLLs and the allowlist

`.gitignore:2` ignores every `bin/` and `.gitignore:4` every `_Module/bin/`. The Main module then un-ignores its client folder and re-ignores its contents (`.gitignore:73-75`) and allowlists exactly two files (`.gitignore:79-80`):

```
!Main/_Module/bin/
!Main/_Module/bin/Win64_Shipping_Client/
Main/_Module/bin/Win64_Shipping_Client/*
!Main/_Module/bin/Win64_Shipping_Client/MinHook.x64.dll
!Main/_Module/bin/Win64_Shipping_Client/TAOM.NativeSkinFixes.dll
```

`git ls-files Main/_Module/bin` returns those two paths and nothing else. <!-- measured: git ls-files Main/_Module/bin 2026-09-05 --> `TAOM.dll` and `TAOM.pdb` stay ignored as build output; the Gaming.Desktop sibling is ignored whole (`.gitignore:82`); and the comment at `.gitignore:69-70` records that `MCMv5.dll` comes from TAOM.Dependencies and the `Bannerlord.MCM` NuGet and must not be vendored here. `TAOM.NativeSkinFixes.dll` is TAOM-owned C++ rebuilt outside this repo (`.gitignore:70-72`); MinHook's licence is at `Main/_Module/THIRD-PARTY-LICENSES.txt:22`. The install's client folder holds 10 files, because `CopyBinariesWindows` also copies the NuGet companions (`DryIoc.dll`, `Newtonsoft.Json.dll`, `System.Runtime.CompilerServices.Unsafe.dll`) that match its regex. <!-- measured: ls "<game>/Modules/TAOM/bin/Win64_Shipping_Client" | wc -l 2026-09-05 -->

## Versioning

The version a player sees is `<Version value="v2.0.28" />` at `Main/_Module/SubModule.xml:6`, and only that. The crash reporter reads it through `ModuleHelper.GetModuleInfo("TAOM")?.Version` and stamps it into every bundle as `TaomVersion` (`docs/reference/release-process.md:8-13`). Because `<ModuleType>` is `Community`, `InitializeModules` leaves the declared value alone (`ModuleHelper.cs:90-93`), which is why the string survives into a bundle verbatim.

The contract (`docs/reference/release-process.md:22-41`): the field changes only in a release commit, that commit is tagged `vX.Y.Z` with an annotated tag, the tag is pushed with its own refspec, and a pushed tag never moves. The current value has its tag: `git tag --points-at` on the `v2.0.28` commit returns `v2.0.28`. <!-- measured: git tag --points-at $(git rev-list -n1 v2.0.28) 2026-09-05 --> Two later-numbered tags, `v2.1.0` and `v2.1.1`, also exist while the file declares `v2.0.28`; the release doc's tables stop before them, so which line is current is a question for the maintainer, not this chapter. <!-- measured: git tag -l 'v2.*' | sort -V | tail -8 2026-09-05 -->

One correction to the release doc. Its three-field table (`docs/reference/release-process.md:47-51`) says `Main/_Module/SubModule.xml` carries a `<DependedModuleMetadata id="TAOM.Dependencies" ... version="v2.0.Y" />` row that must match the Dependencies module's version. No such row exists: the only mention of `TAOM.Dependencies` in the manifest is the comment at `Main/_Module/SubModule.xml:15-22`, and there is no `<DependedModule Id="TAOM.Dependencies"/>` either. <!-- measured: rg -n "TAOM.Dependencies" Main/_Module/SubModule.xml 2026-09-05 --> The pairing the comment describes is enforced by tests and by the `/release` skill, not by the manifest. The [module-dependencies](module-dependencies.md) chapter owns that story.

## Worked example

Three verbatim pieces. The first is the whole of `project.mbproj`, the second is one `<XmlNode>` (`Main/_Module/SubModule.xml:180-188`), the third is a per-language manifest.

<!-- example file="Main/_Module/ModuleData/project.mbproj" id="soln_voice_definitions" -->
```xml
<?xml version="1.0" encoding="utf-8"?>
<base xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance" xmlns:xsd="http://www.w3.org/2001/XMLSchema" type="solution">
	<outputDirectory>..\MBModule\MBModule\</outputDirectory>
	<XMLDirectory>..\WOTS\Modules\TAOM/</XMLDirectory>
	<ModuleAssemblyDirectory>..\WOTS\TAOM\bin\</ModuleAssemblyDirectory>
	<file id="soln_voice_definitions" name="ModuleData/lotr_uruk_voice_def.xml" type="voice_definitions" />
	<file id="soln_voice_definitions" name="ModuleData/lotr_dwarf_voice_def.xml" type="voice_definitions" />
	<file id="soln_voice_definitions" name="ModuleData/lotr_uruk_hai_voice_def.xml" type="voice_definitions" />
	<file id="soln_voice_definitions" name="ModuleData/VoiceDefinitions/LOTR/lotr_warg_voice_def.xml" type="voice_definitions" />
	<file id="soln_module_sound" name="ModuleData/module_sounds.xml" type="module_sound" />
</base>
```

1. **`id`** is the only thing that decides whether the row loads. It must be one of the ids the engine asks for by name (`Module.cs:1366-1504`); `soln_voice_definitions` and `soln_module_sound` are two of them.
2. **`name`** is a path from the module root with its extension written out, so `ModuleData/` has to be part of it (`ModuleHelper.cs:211-214`).
3. **`type`** is never read (`XmlResource.cs:122-138`). Keep it for the Kit; do not expect the engine to react to it.

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

1. **`id`** picks the object type and the schema: `NPCCharacters` is loaded by the sandbox startup and validated by `XmlSchemas/NPCCharacters.xsd`.
2. **`path`** becomes `Main/_Module/ModuleData/troops/troops_gondor.xml` (`ModuleHelper.cs:234`); the forward slash is a subfolder and the `.xml` is appended for you.
3. **`GameType`** values are C# class names. `Campaign` and `CampaignStoryMode` are different classes, so a row listing only the first vanishes in a story-mode campaign (`GameTextManager.cs:115` shows the comparison is `GetType().Name`). The uneven spacing around `=` is harmless: the parser reads `Attributes["value"]` (`XmlResource.cs:170`).

<!-- example file="Main/_Module/ModuleData/Languages/DE/language_data.xml" id="Deutsch" -->
```xml
<?xml version="1.0" encoding="utf-8"?>
<LanguageData id="Deutsch">
  <LanguageFile xml_path="DE/std_taom_module_strings_deu-DE.xml" />
  <LanguageFile xml_path="DE/std_taom_wanderer_strings_deu-DE.xml" />
  <LanguageFile xml_path="DE/std_taom_named_companion_strings_deu-DE.xml" />
  <LanguageFile xml_path="DE/std_taom_cc_strings_deu-DE.xml" />
  <LanguageFile xml_path="DE/std_taom_career_strings_deu-DE.xml" />
  <LanguageFile xml_path="DE/std_taom_messenger_strings_deu-DE.xml" />
  <LanguageFile xml_path="DE/std_taom_lotr_issue_strings_deu-DE.xml" />
  <LanguageFile xml_path="DE/std_taom_xslt_strings_deu-DE.xml" />
  <LanguageFile xml_path="DE/std_taom_emissary_strings_deu-DE.xml" />
  <LanguageFile xml_path="DE/std_taom_wotr_strings_deu-DE.xml" />
  <LanguageFile xml_path="DE/std_taom_enlistment_strings_deu-DE.xml" />
  <LanguageFile xml_path="DE/std_taom_keybind_strings_deu-DE.xml" />
  <LanguageFile xml_path="DE/std_taom_player_switcher_strings_deu-DE.xml" />
</LanguageData>
```

1. **`id`** must be the string vanilla uses for that language; the engine finds an existing `LanguageData` by id and extends it (`LanguageData.cs:156-165`). `Deutsch` is what `Native/ModuleData/Languages/DE/language_data.xml` declares. <!-- measured: rg -o 'LanguageData id="[^"]+"' "<game>/Modules/Native/ModuleData/Languages/*/language_data.xml" 2026-09-05 -->
2. **`xml_path`** is joined to the scanned `ModuleData/Languages` root, not to this file's own folder (`LanguageData.cs:130`), which is why every row starts with `DE/`.
3. **The row count** is pinned at 13 by `AllLanguageDirs_HaveExactlyThirteenLanguageFiles` (`TAOM.Tests/Infrastructure/Localization/LanguageDataXmlTests.cs:142`). A 14th file means a new test name, not just a new number.

## Recipes

### Add: create the module folder from zero

A module is a folder with a `SubModule.xml` in it. Nothing else is required (`ModuleHelper.cs:327-331`); `TAOM_Map` and `LOTRLOME_Armory` both ship an empty `<SubModules/>` (`TAOM_Map/SubModule.xml:21`, `LOTRLOME_Armory/SubModule.xml:20`) and contribute data only.

1. Create `Modules/<YourId>/` in the game install (or, for a repo module, `<Project>/_Module/` and a csproj with `<ModuleId>` set, so `CopyModule` deploys it).
2. Write `Modules/<YourId>/SubModule.xml` with a `<Module>` root and, first, the three elements the engine dereferences unconditionally: `<Id value="<YourId>"/>`, `<Name value="..."/>`, `<Version value="v1.0.0"/>` (`ModuleInfo.cs:81-87`). Then `<DefaultModule value="false"/>`, `<ModuleCategory value="Singleplayer"/>`, `<ModuleType value="Community"/>`. Copy the shape from `Main/_Module/SubModule.xml:4-9`.
3. Add `<DependedModules>` with `<DependedModule Id="Native"/>`, `SandBoxCore`, and `Sandbox` (lower-case `b`, the id declared at `SandBox/SubModule.xml:4`); add `CustomBattle` only if your rows list the `CustomGame` game type. Copy from `Main/_Module/SubModule.xml:10-14`.
4. Add `<SubModules/>` empty. Add a `<SubModule>` block only when you have a DLL; every one of `Name`, `DLLName` and `SubModuleClassType` is dereferenced with no null check (`SubModuleInfo.cs:51-65`).
5. Add `<Xmls>` with your first `<XmlNode>`: copy the worked example above and change `id` and `path`. Put comments between `<XmlNode>` elements, never inside `<IncludedGameTypes>` (`XmlResource.cs:168-171`).
6. Create `ModuleData/<path>.xml` whose root element is the one that id expects, `<NPCCharacters>` for the example. Save as UTF-8 without a BOM and with CRLF line endings, matching the convention in `tools/README.md:7-27`.
7. Launch the game. The launcher lists any folder that passed step 1; tick the module and start a new campaign.

Check: `python tools/audit_mbproj_registration.py --module <YourId>` (it audits only a `project.mbproj`, so a module without one reports `0 module(s) with a project.mbproj audited` and proves nothing about `SubModule.xml`; the launcher listing in step 7 is the proof for that file)
Takes effect: full game restart
Code: No code changes needed

### Add: a new ModuleData file and register it

1. Create the file under `Main/_Module/ModuleData/`, in the subfolder its neighbours use (`troops/`, `characters/`, `equipmentsets/`), with the root element for its id. Keep the file's encoding and line endings byte-faithful (`tools/README.md:7-27`).
2. Add one `<XmlNode>` to `Main/_Module/SubModule.xml` inside `<Xmls>`. A new faction or culture goes between the sentinels at lines 348-394 or 395-441 so the generators can find it; anything else goes beside its neighbours. `path` is the file path from `ModuleData/` without `.xml`.
3. List `Campaign` and `CampaignStoryMode` in `<IncludedGameTypes>`; add `CustomGame` and `EditorGame` if custom battles and the Kit should see the data, as the troop rows do (`Main/_Module/SubModule.xml:183-186`).
4. If the file carries player-facing text, register the strings too: the [strings-and-localization](strings-and-localization.md) chapter has the `{=KEY}Fallback` rule and the 13-file step.
5. `./build.ps1` to deploy, then a full game restart. A registered file that did not exist when the process started is null in-engine until the restart, and every static gate stays green meanwhile (`Module.cs:1029-1033`).

Check: `python tools/validate_moduledata.py`
Takes effect: full game restart
Code: No code changes needed

### Add: a new language folder

1. Find the id vanilla uses for the language in `Native/ModuleData/Languages/<DIR>/language_data.xml` (12 of them are listed in `docs/features/localization.md:62-75`).
2. Create `Main/_Module/ModuleData/Languages/<DIR>/language_data.xml` with `<LanguageData id="<that id>">` and one `<LanguageFile xml_path="<DIR>/std_taom_<file>_<locale>.xml"/>` per source strings file; today that is 13 rows (the DE example above). The `xml_path` is relative to `ModuleData/Languages`, not to the new folder (`LanguageData.cs:130`).
3. Generate the 13 files. `python tools/generate_translation_template.py` writes English templates (`tools/README.md:292`); `python tools/translate_with_claude.py --lang <LANG> --module TAOM --apply` fills them (`tools/README.md:324`), and it only knows the codes in its `LANGUAGES` table (`tools/translate_with_claude.py:91`), so a language new to TAOM needs a row there first.
4. Do not create an English folder. `LoadLanguage` deserializes `<string>` rows only when the language id is not `English` (`LocalizedTextManager.cs:235`); the inline `{=KEY}Fallback` text in the source XML is the English.
5. `./build.ps1`, full restart, switch the game language.

Check: `./build.ps1 -RunTests` (runs `AllLanguageDirs_HaveExactlyThirteenLanguageFiles` at `TAOM.Tests/Infrastructure/Localization/LanguageDataXmlTests.cs:142`)
Takes effect: full game restart
Code: Code changes required in `tools/translate_with_claude.py` (the `LANGUAGES` table at line 91) when the language is new to the translator; the game needs none

### Modify: bump the version for a release

The full sequence, the Discord note, and the `/release` skill are in [`docs/reference/release-process.md`](../reference/release-process.md); this is the module-side part.

1. Tree clean, current version already tagged (`docs/reference/release-process.md:69`).
2. `./build.ps1 -RunTests` green (`:70`).
3. `pwsh tools/sweep_module_backups.ps1` reports 0 files; if not, `-Apply`. Backup sidecars must not ship (`:71-73`). The repo's `Main/_Module` tree is one of the roots it sweeps, because `CopyModule` would redeploy a sidecar left there (`docs/reference/module-backup-sweep.md:43-56`).
4. Edit `<Version value="vX.Y.Z" />` at `Main/_Module/SubModule.xml:6`. If the Dependencies assembly changed, bump `Dependencies/_Module/SubModule.xml` too (`docs/reference/release-process.md:49-50`).
5. Commit as `chore(release): TAOM vX.Y.Z`, staging the release paths explicitly; `git tag -a vX.Y.Z -m "..."`; `git push origin <branch> vX.Y.Z`. A plain `git push` does not push the tag (`:77-78, 85`).
6. `python tools/package_release.py --source "<game>/Modules" --dest <out> --dry-run` to see what would ship. It excludes `RuntimeDataCache`, `AssetSources`, `*.xml.bak`, the native debug artifacts and any `.vs` path (`tools/package_release.py:104-143`) and keeps `project.mbproj` (`:126-135`).

Check: `pwsh tools/sweep_module_backups.ps1` and `./build.ps1 -RunTests`
Takes effect: full game restart
Code: No code changes needed

### Delete: retire a ModuleData file

1. Remove the `<XmlNode>` first, then the file. A registered `path` whose file is gone is not an error: the row becomes an empty slot whose stylesheet, if any, still runs (`MBObjectManager.cs:911-915`).
2. Delete the file from the install copy by hand as well. The build never deletes (`Basic.targets:65`), and a file left behind with a still-registered path keeps loading.
3. Never rename a retired file to `<name>.bak.xml` inside a registered folder: the folder form globs `*.xml`, so it would parse as data and duplicate every id (`MBObjectManager.cs:903`, `docs/reference/module-backup-sweep.md:62-70`). `<name>.xml.bak-<topic>` is safe, and `.gitignore:24` hides it from git.

Check: `python tools/validate_moduledata.py`
Takes effect: full game restart
Code: No code changes needed

## Gotchas: what fails silently and what crashes

- **`-p:DisableModuleCopy=true` still deploys.** The wrapper checks the flag; the two copy targets do not (`Basic.targets:47, 53, 64`). Use `-p:ModuleId=` while the game is open (`docs/ai-includes/agent-operating-manual.md:49-51`).
- **Deploy never deletes.** `CopyModule` runs `Clean="false"` (`Basic.targets:65`); the install carries four `ModuleData` files the repo no longer has. A deleted file with a surviving `<XmlNode>` row keeps loading (measured above).
- **`Clean="true"` on the binaries copy cleans only its own regex.** `.*\.dll|.*\.pdb|.*\.config$` (`Basic.props:6`), so `.exp` and `.lib` files, and the mirrors that copy them onward, persist (`Basic.targets:56`).
- **The DLL path is the process's working directory name.** `Common.ConfigName` is `new DirectoryInfo(Directory.GetCurrentDirectory()).Name` (`Common.cs:37`). Launch the game from a different working directory and `bin/<that name>/TAOM.dll` does not exist, while `SubModuleInfo` still reports `DLLExists = true` from its hardcoded client probe (`SubModuleInfo.cs:54-57`).
- **`<DependedModuleMetadatas>` is not read by the engine** (`ModuleInfo.cs:105-166`), so a pin written only there establishes nothing for a vanilla-launcher user. `<DependedModules>` and `<ModulesToLoadAfterThis>` do shape the order, because the launcher topology-sorts on them before the engine sees the list (`LauncherModsVM.cs:158`, `ModuleHelper.cs:252-268`); the engine's own `GetSortedModules` (`ModuleHelper.cs:271`) has only its two multiplayer callers.
- **A comment inside `<IncludedGameTypes>` is a startup exception.** Every child node's `Attributes["value"]` is read (`XmlResource.cs:168-171`); the `XmlReaderSettings` with `IgnoreComments` on the line above is constructed and discarded (`XmlResource.cs:145`), and `Module.LoadSubModules` does not catch it (`Module.cs:1029-1033`).
- **An empty `<IncludedGameTypes>` means every game type,** not none (`MBObjectManager.cs:884`).
- **A `path` with no file is a silent stylesheet hook,** so a typo in `path` looks exactly like a deliberate XSLT-only row (`MBObjectManager.cs:911-915`).
- **The folder form globs `*.xml`,** so `x.bak.xml` is data and `x.xml.bak-topic` is invisible (`MBObjectManager.cs:903`). The repo tree holds 9 sidecars right now, 1 of them git-tracked (`sp_battle_scenes.xml.bak_scenes`); all 9 redeploy on the next build because `CopyModule` copies everything, and `.gitignore:24` hides them from `git status` (`docs/reference/module-backup-sweep.md:43-53`). <!-- measured: find Main/_Module/ModuleData -name '*.bak*' | wc -l; git ls-files Main/_Module/ModuleData | grep -c bak 2026-09-05 -->
- **`project.mbproj` is a runtime file.** Excluding it from a release removes every custom voice and `module_sounds.xml` with no error anywhere (`Module.cs:1031`, `tools/package_release.py:126-135`). An invented `soln_*` id is inert and unlogged (`Module.cs:1366-1504`); `python tools/audit_mbproj_registration.py` is the gate (`docs/reference/lotrlome-soln-id-fix.md`).
- **A new data file loads only at process launch** (`Module.cs:1029-1033`). A green validator with a naked troop in game means the file was not loaded, not that the data is wrong (`docs/ai-includes/agent-operating-manual.md:38`).
- **`settlements.xml` in this module is a shadow.** Nothing registers it; `TAOM_Map` owns `Settlements` (`Main/_Module/SubModule.xml`, no match for `settlements`).
- **`Languages/` paths are relative to `ModuleData/Languages`** (`LanguageData.cs:130`). A wrong path fails as `Could not parse: ...` in the log (`LocalizedTextManager.cs:216`) and the language simply stays English. English `<string>` rows are never deserialized (`LocalizedTextManager.cs:235`). The root `language_data.xml` with no rows is harmless but adds nothing: discovery is the recursive file search at `LocalizedTextManager.cs:92-99`.
- **`GUI/SpriteData/FactionMap/` is not sprites.** Editing those PNGs in the repo changes nothing in game until they are copied to the install, and a baked sprite needs the generator plus a full relaunch (`docs/features/gui-sprite-system.md:59-84, 138`). Sync the bake back with `pwsh tools/sync_sprite_bake.ps1`; a wider install-to-repo copy reverts uncommitted work (`docs/features/gui-sprite-system.md:168-169`).
- **`ExcludeSourceFilesFromModule=false` deploys 551 MB of `AssetSources` to every dev install** (`Main/TAOM.csproj:13`, `Basic.targets:155`); the packager removes it again (`tools/package_release.py:116-117`).
- **MCM values live outside the module** (`Main/Features/TaomSettings.cs:15`); a reinstall does not reset them.
- **`localization-map.md` says 11 language files per language and names an `...ElevenLanguageFiles` test** (`docs/reference/localization-map.md:12, 17`); the shipped count is 13 and the test is `AllLanguageDirs_HaveExactlyThirteenLanguageFiles` (`LanguageDataXmlTests.cs:142`). Trust the test file.

## Numbers in this chapter

All measured 2026-09-05 from the repo at `Main/_Module/` and the installed `Modules/TAOM/`.

| Number | Command |
|---|---|
| 978 MB module tree; per-folder MB | `du -sm Main/_Module; for d in Main/_Module/*/; do du -sm "$d"; done` |
| 4, 86, 121, 1,319, 367, 436, 1, 7 files per top-level folder | `for d in Main/_Module/*/; do find "$d" -type f \| wc -l; done` |
| 37,947 bytes, 971 lines in `SubModule.xml` | `wc -l -c Main/_Module/SubModule.xml` |
| 100 `<XmlNode>` rows, 12 distinct ids, 44/27/15/2/2/2/2/2/1/1/1/1 by id | `rg -o "<XmlNode>" Main/_Module/SubModule.xml \| wc -l; rg -o 'XmlName id="[^"]+"' Main/_Module/SubModule.xml \| sort \| uniq -c \| sort -rn` |
| Sentinel lines 348, 394, 395, 441 | `rg -n "TAOM-NEW(FACTIONS\|CULTURE)-REG" Main/_Module/SubModule.xml` |
| 117 lines in `THIRD-PARTY-LICENSES.txt`; MinHook at line 22 | `wc -l Main/_Module/THIRD-PARTY-LICENSES.txt; rg -n MinHook Main/_Module/THIRD-PARTY-LICENSES.txt` |
| 2 git-tracked files under `bin/` | `git ls-files Main/_Module/bin` |
| 39 root files and 42 subfolders under `ModuleData/` | `find Main/_Module/ModuleData -maxdepth 1 -type f \| wc -l; find Main/_Module/ModuleData -mindepth 1 -maxdepth 1 -type d \| wc -l` |
| 4 registered, 36 code-loaded subfolders | `for d in Main/_Module/ModuleData/*/; do rg -q "path=\"$(basename ${d%/})/" Main/_Module/SubModule.xml && echo REGISTERED \|\| echo CODE; done` |
| 5 `<file>` rows, 11 lines, 896 bytes in `project.mbproj` | `rg -c "<file " Main/_Module/ModuleData/project.mbproj; wc -l -c Main/_Module/ModuleData/project.mbproj` |
| 169 files under `Languages/`, 12 folders, 14 files each, 13 `<LanguageFile>` rows each | `find Main/_Module/ModuleData/Languages -type f \| wc -l; for d in Main/_Module/ModuleData/Languages/*/; do ls $d \| wc -l; rg -c '<LanguageFile' $d/language_data.xml; done` |
| 8 `.xslt` files, all without an `.xml` sibling | `for x in Main/_Module/ModuleData/*.xslt; do test -f "${x%.xslt}.xml" \|\| echo xslt-only; done` |
| `GUI/`: 11, 6, 51, 227, 1,023 per subfolder; 428,445-byte manifest; 563/369/78/9/3 PNGs per sprite category; 226 FactionMap PNGs | `for d in Main/_Module/GUI/*/; do find "$d" -type f \| wc -l; done; wc -c Main/_Module/GUI/TAOMSpriteData.xml; for d in Main/_Module/GUI/SpriteParts/*/; do find "$d" -name '*.png' \| wc -l; done; find Main/_Module/GUI/SpriteData/FactionMap -type f \| wc -l` |
| 4 `AssetPackages` tpac; 342 wav, 93 mp3, 1 ogg; 8,647-byte prefab | `ls Main/_Module/AssetPackages; find Main/_Module/ModuleSounds -type f \| sed 's/.*\.//' \| sort \| uniq -c; wc -c Main/_Module/Prefabs/taom_howdah_agent.xml` |
| 1,023,041 and 51,752 bytes for the two unregistered settlement files; 2,589 bytes `global_strings.xml` | `wc -c Main/_Module/ModuleData/settlements.xml Main/_Module/ModuleData/custom_settlements.xml Main/_Module/ModuleData/global_strings.xml` |
| 9 sidecars, 1 tracked | `find Main/_Module/ModuleData -name '*.bak*' \| wc -l; git ls-files Main/_Module/ModuleData \| grep -c bak` |
| 371 install-side `ModuleData` files, 4 extras | `find "<game>/Modules/TAOM/ModuleData" -type f \| wc -l; diff <(cd Main/_Module/ModuleData && find . -type f \| sort) <(cd "<game>/Modules/TAOM/ModuleData" && find . -type f \| sort)` |
| Install `bin/`: 10 client, 10 server, 12 wEditor, 4 Gaming.Desktop files | `for d in "<game>/Modules/TAOM"/bin/*/; do ls "$d" \| wc -l; done` |
| 115 `RuntimeDataCache` entries, 5,141 MB | `ls "<game>/Modules/TAOM/RuntimeDataCache" \| wc -l; du -sm "<game>/Modules/TAOM/RuntimeDataCache"` |
| Repo and install `SubModule.xml` identical | `cmp Main/_Module/SubModule.xml "<game>/Modules/TAOM/SubModule.xml"` |
| 51 schemas; all 12 TAOM ids present; `soln_voice_definitions.xsd` present, `soln_module_sound.xsd` absent | `ls "<game>/XmlSchemas" \| wc -l; test -f "<game>/XmlSchemas/<id>.xsd"` per id |
| 1 `MERGE-RISK` warning, 0 errors from the mbproj audit | `python tools/audit_mbproj_registration.py --module TAOM` |
| `v2.0.28` tagged; `v2.1.0` and `v2.1.1` also exist | `git tag --points-at $(git rev-list -n1 v2.0.28); git tag -l 'v2.*' \| sort -V \| tail -8` |
| 43 lines in `build.ps1` | `cat -n build.ps1` |

## Read next

- [`docs/reference/bannerlord-engine-and-toolchain.md`](../reference/bannerlord-engine-and-toolchain.md): the four engine builds (section 1), the per-module `bin/` mirrors (section 1.2), and the four asset folders (section 6.1).
- [`docs/features/gui-sprite-system.md`](../features/gui-sprite-system.md): the `GUI/` layout, the FactionMap trap, the bake pipeline table and the deploy rules.
- [`docs/features/localization.md`](../features/localization.md) and [`docs/reference/localization-map.md`](../reference/localization-map.md): the `Languages/` tree, the id-to-folder table and the translation tools.
- [`docs/reference/module-backup-sweep.md`](../reference/module-backup-sweep.md): why the repo `_Module` tree is a sweep root and the last-extension rule.
- [`docs/reference/release-process.md`](../reference/release-process.md): the version contract and the release sequence.
- [`docs/features/mcm.md`](../features/mcm.md): where MCM comes from and why TAOM ships no MCM file.
- [`docs/reference/lotrlome-soln-id-fix.md`](../reference/lotrlome-soln-id-fix.md): the `project.mbproj` inert-id failure and its gate.
- [`docs/ai-includes/agent-operating-manual.md`](../ai-includes/agent-operating-manual.md): the build commands and the `DisableModuleCopy` caveat.
- [`tools/README.md`](../../tools/README.md): the XML I/O convention and every tool named above.
