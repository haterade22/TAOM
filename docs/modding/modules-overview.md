# Modules overview

The eight modules TAOM runs on, what each one owns, which copy of each is the real one, what reaches
a player, how the four TAOM version numbers relate, and where the launcher's load order comes from.

## What this file is

This chapter explains what a Bannerlord module is and walks the four vanilla modules (`Native`,
`SandBoxCore`, `SandBox`, `CustomBattle`) and the four TAOM modules (`TAOM.Dependencies`, `TAOM`,
`LOTRLOME_Armory`, `TAOM_Map`) as they sit on disk on 2026-09-05. It is orientation only: registration
detail belongs to [Submodule and registration](submodule-and-registration.md), ordering rules to
[Load order and dependencies](load-order-and-dependencies.md), and each TAOM module has its own
`module-*` chapter. Read it once before opening any other chapter, because two of the four TAOM modules
are not in the repo at all and the file you are about to edit may be a dead copy.

## A module is a folder with a SubModule.xml

The engine looks in exactly one place. `ModuleHelper.GetPhysicalModules` lists every directory under
the game's `Modules/` folder, builds `<dir>/SubModule.xml`, skips the directory when that file is
missing, and otherwise hands the folder to `ModuleInfo.LoadWithFullPath` (`ModuleHelper.cs:319-334`,
`ModuleInfo.cs:68-75`). Nothing else is required: no `bin/`, no `ModuleData/`, no C#. Two of TAOM's
modules ship no assembly at all and still carry every TAOM-authored item and the whole campaign map.

On this install there are 19 module folders, 15 of them with a root `SubModule.xml`. <!-- measured: ls -d "<game>/Modules"/*/ | wc -l; for d in "<game>/Modules"/*/; do test -f "$d/SubModule.xml" && echo yes || echo NO; done 2026-09-05 -->
The 4 without one are `Bannerlord.Harmony`, `Bannerlord.ButterLib`, `Bannerlord.UIExtenderEx` and
`Bannerlord.MBOptionScreen`, the alias stubs TAOM deploys one folder too deep (see "Where the launcher
load order really comes from" below).

`LoadWithFullPath` parses the manifest with a bare `XmlDocument` and no schema, and the two tables
below list every element it reads. `<Xmls>` is read later by `XmlResource.GetXmlListAndApply`
(`XmlResource.cs:142-149`), `ModuleData/project.mbproj` by `XmlResource.GetMbprojxmls`
(`XmlResource.cs:107-117`), and `<DependedModuleMetadatas>` has no branch anywhere in
`ModuleInfo.cs:105-149`.

<!-- engine-table type="TaleWorlds.ModuleManager.ModuleInfo" file="Platform/TaleWorlds.ModuleManager/TaleWorlds.ModuleManager/ModuleInfo.cs" method="LoadWithFullPath" inert="RequiredBaseVersion" -->

| Attribute | Type | Required | Default when absent | What it does | Read at (file:line) |
|---|---|---|---|---|---|
| `Name@value` | string | yes (dereferenced without a null check; a missing element throws and the module is dropped) | none | Launcher display name. The engine compares it to `"NavalDLC"` and nothing else (`ModuleHelper.cs:71-83`) | `ModuleInfo.cs:81` |
| `Id@value` | string | yes | none | The module's identity. Every lookup lower-cases it (`ModuleHelper.cs:30-43`, `:87-97`); every other module's `<DependedModule Id>` must match it, not the folder name | `ModuleInfo.cs:82` |
| `Version@value` | version string (`v2.0.28`) | yes | none | Parsed by `ApplicationVersion.FromString`. For `Official` modules the changeset is overwritten with the engine build (`ModuleInfo.cs:179-182`, called from `ModuleHelper.cs:93`); a `Community` value survives verbatim into crash bundles | `ModuleInfo.cs:87` |
| `RequiredBaseVersion@value` | version string | no | unset | Read but has no effect for a community module: the only consumer is the `NavalDLC` check in `ModuleHelper.cs:71-83` | `ModuleInfo.cs:88-91` |
| `DefaultModule@value` | the literal `true` | no | `false` | Sets `IsDefault`, which the launcher uses to tick a module on first launch (`LauncherModsVM.cs:162`) | `ModuleInfo.cs:92` |
| `ModuleType@value` | enum `Community`, `Official`, `OfficialOptional` | no | `Community` (enum value 0, `ModuleType.cs:5`) | `IsOfficial` and `IsRequiredOfficial` derive from it (`ModuleInfo.cs:27,31`); official modules get the version rewrite and the certificate check | `ModuleInfo.cs:93-97` |
| `ModuleCategory@value` | enum `Singleplayer`, `Multiplayer`, `MultiplayerOptional`, `Server` | no | `Singleplayer` | Which launcher tab lists the module (`LauncherModsVM.cs:169-180`) | `ModuleInfo.cs:99-104` |
| `DependedModules/DependedModule@Id` (plus `@DependentVersion`, `@Optional`) | list | no | empty list | The hard dependency. The launcher greys the module out when one is missing (`LauncherModsVM.cs:226-234`) and feeds it to the topology sort (`ModuleHelper.cs:252-260`) | `ModuleInfo.cs:105-130` |
| `ModulesToLoadAfterThis/Module@Id` | list | no | empty list | A reverse ordering edge: the named module sorts after this one, and an unknown id is simply never matched (`ModuleHelper.cs:262-268`) | `ModuleInfo.cs:131-139` |
| `IncompatibleModules/Module@Id` | list | no | empty list | Ticking this module unticks the named one in the launcher (`LauncherModsVM.cs:213-216`, `:235-243`) | `ModuleInfo.cs:140-148` |
| `SubModules/SubModule` | list | no | empty list | Each child goes through `SubModuleInfo.LoadFrom` inside a try/catch; a malformed entry is still added, empty (`ModuleInfo.cs:157-165`) | `ModuleInfo.cs:149-166` |

<!-- engine-table type="TaleWorlds.ModuleManager.SubModuleInfo" file="Platform/TaleWorlds.ModuleManager/TaleWorlds.ModuleManager/SubModuleInfo.cs" method="LoadFrom" inert="" -->

| Attribute | Type | Required | Default when absent | What it does | Read at (file:line) |
|---|---|---|---|---|---|
| `Name@value` | string | yes | none | Label only | `SubModuleInfo.cs:51` |
| `DLLName@value` | file name | yes | none | Existence and certificate probe at the literal path `bin\Win64_Shipping_Client\<DLLName>` (`SubModuleInfo.cs:54-63`). The real load happens elsewhere, from `bin/<ConfigName>/` (`Module.cs:1044-1058`) | `SubModuleInfo.cs:52` |
| `SubModuleClassType@value` | fully qualified type name | yes | none | The `MBSubModuleBase` class the engine constructs | `SubModuleInfo.cs:65` |
| `Assemblies/Assembly@value` | list of file names | no | empty list | Loaded before the SubModule's own DLL, from the same `bin/<ConfigName>/` folder with a fallback to the game's managed folder (`Module.cs:1048-1057`) | `SubModuleInfo.cs:66-74` |
| `Tags/Tag@key` + `@value` | key from the closed set `RejectedPlatform`, `ExclusivePlatform`, `DedicatedServerType`, `IsNoRenderModeElement`, `DependantRuntimeLibrary`, `PlayerHostedDedicatedServer`, `EngineType` (`SubModuleInfo.cs:12-21`) | no | none | An unrecognised key is dropped by `Enum.TryParse`. `DedicatedServerType` with any value other than `none` forces `IsTWCertifiedDLL = true` (`SubModuleInfo.cs:87-90`) | `SubModuleInfo.cs:75-92` |

Three facts to carry into the module table:

- **The `bin/` subfolder is picked at run time.** `Module.LoadSubModules` loads the DLL from
  `Path.Combine(FolderPath, "bin", Common.ConfigName)` (`Module.cs:1044`), and `Common.ConfigName` is
  the process's working directory name (`Common.cs:37`): `Win64_Shipping_Client` for the game,
  `Win64_Shipping_wEditor` for the Modding Kit, `Win64_Shipping_Server` for a dedicated server. The
  deployed `TAOM` therefore carries four `bin/` subfolders (Client 10 files, wEditor 12, Server 10,
  Gaming.Desktop 4) and `TAOM.Dependencies` three populated ones (Client 42, Server 42, Gaming.Desktop 3,
  wEditor 0). <!-- measured: for d in "<game>/Modules/TAOM/bin"/*; do ls "$d" | wc -l; done (and the same for TAOM.Dependencies) 2026-09-05 -->
- **Data registers before any DLL loads.** `LoadSubModules` calls `GetMbprojxmls` then
  `GetXmlListAndApply` for every module (`Module.cs:1029-1033`) before it loads a single assembly, so a
  module with an empty `<SubModules/>` still registers all of its XML.
- **The registry is read once, at process launch** (`Module.cs:261-267`). A file added, or a
  registration added, while the game runs does not exist to the engine until a full restart, which is
  why a validator can pass on a file the running game has never seen.

## The eight modules TAOM runs on

The first live-install path below is `Native/SubModule.xml`. This file lives in the game install, not
the repo; a module reinstall reverts hand edits, so land a repo-side validator gate with any fix. The
same sentence applies to every `SandBoxCore/`, `SandBox/`, `CustomBattle/`, `TAOM_Map/` and
`LOTRLOME_Armory/` path in this chapter.

| Module | What it is | Where you edit it | `<Version>` | C# entry points | Managed XML registrations | `project.mbproj` rows | Size on this install |
|---|---|---|---|---|---|---|---|
| `Native` | The engine's own data: skeletons, action sets, item modifiers, banner icons, the 39 `soln_*` native ids | Never. Game install, `Official` | `v1.4.8` (`Native/SubModule.xml:5`) | vanilla | 24 `XmlNode` rows across 18 ids | 50 `<file>` rows <!-- measured: for m in Native SandBoxCore SandBox CustomBattle TAOM TAOM_Map LOTRLOME_Armory TAOM.Dependencies; do grep -c '<file ' "<game>/Modules/$m/ModuleData/project.mbproj"; done 2026-09-05 --> | not counted |
| `SandBoxCore` | Vanilla items, cultures, characters, rosters, skills, body properties | Never. Game install, `Official`, depends on `Native` (`SandBoxCore/SubModule.xml:10`) | `v1.4.8` | vanilla | 8 rows across 6 ids | none (no `project.mbproj`) | not counted |
| `SandBox` | The vanilla campaign: settlements, kingdoms, clans, heroes, workshops, concepts, music. **Folder `SandBox`, id `Sandbox`** (`SandBox/SubModule.xml:4`); every TAOM manifest writes `Sandbox` on purpose | Never. Game install, `Official`, depends on `Native` and `SandBoxCore` (`SandBox/SubModule.xml:10-11`) | `v1.4.8` | vanilla | 31 rows across 16 ids | 0 `<file>` rows | not counted |
| `CustomBattle` | The custom-battle game type (`CustomGame`) and its scene list | Never. Game install, `Official`, depends on `Native` and `SandBoxCore` (`CustomBattle/SubModule.xml:10-11`) | `v1.4.8` | vanilla | 2 rows across 2 ids | none (no `project.mbproj`) | not counted |
| `TAOM.Dependencies` | The library module: Harmony, UIExtenderEx, ButterLib, MCM and TAOM's shield layer, booted from one folder so a player enables only two TAOM entries | Repo, [`Dependencies/_Module/`](../../Dependencies/_Module/SubModule.xml); the build copies it | `v2.0.6` (`Dependencies/_Module/SubModule.xml:6`) | 7 `<SubModule>` entries | 0 | 0 (no `project.mbproj`) | 46 MB, 42 of it `bin/` <!-- measured: du -sm "<game>/Modules/TAOM.Dependencies"/* 2026-09-05 --> |
| `TAOM` | The code and data module: `TAOM.dll`, every troop, lord, culture, kingdom, clan, roster, string, GUI and config | Repo, [`Main/_Module/`](../../Main/_Module/SubModule.xml); the build copies it | `v2.0.28` (`Main/_Module/SubModule.xml:6`) | 1 (`TAOM.dll`, `TAOM.SubModule`) | 100 `XmlNode` rows across 12 ids <!-- measured: grep -c "<XmlNode>" Main/_Module/SubModule.xml 2026-09-05 --> | 5 `<file>` rows (4 voice definitions, 1 module sound) | 6,147 MB, of which 5,141 MB is `RuntimeDataCache` <!-- measured: du -sm "<game>/Modules/TAOM" and du -sm "<game>/Modules/TAOM"/* 2026-09-05 --> |
| `LOTRLOME_Armory` | Every TAOM-authored item, crafting piece, monster, race skin, action set and creature asset. Vanilla items keep loading beside them, from `SandBoxCore/SubModule.xml:15`. Data plus art, no C# | Game install only, `LOTRLOME_Armory/SubModule.xml`; the reinstall warning above applies | `v2.0.23` (`LOTRLOME_Armory/SubModule.xml:4`) | 0 (`<SubModules/>`, line 20) | 33 rows: 21 `Items`, 8 `Monsters`, 1 each `CraftingPieces`, `CraftingTemplates`, `WeaponDescriptions`, `ModuleSounds` <!-- measured: grep -o 'XmlName id="[^"]*"' LOTRLOME_Armory/SubModule.xml | sort | uniq -c 2026-09-05 --> | 11 `<file>` rows | 35,595 MB: 18,415 `AssetSources`, 12,978 `RuntimeDataCache`, 4,148 `Assets`, 22 `ModuleData` <!-- measured: du -sm "<game>/Modules/LOTRLOME_Armory"/* 2026-09-05 --> |
| `TAOM_Map` | The Middle-earth campaign map: `SceneObj/Main_map`, `settlements.xml`, the distance cache, prefabs, atmospheres | Game install only, `TAOM_Map/SubModule.xml`; same warning | `v2.0.23` (`TAOM_Map/SubModule.xml:4`) | 0 (`<SubModules/>`, line 21) | 8 rows across 8 ids, 7 of them pointing at Kit template stubs | 9 `<Module>` rows, all inert (see below) | 56,404 MB: 21,205 `RuntimeDataCache`, 15,286 `AssetSources`, 12,859 `AssetPackages`, 3,929 `Assets`, 2,219 `SceneEditData`, 735 `SceneObj`, 14 `ModuleData` <!-- measured: du -sm "<game>/Modules/TAOM_Map"/* 2026-09-05 --> |

The vanilla row counts come from the same `grep -o 'XmlName id=' | sort | uniq -c` over each
vanilla manifest. <!-- measured: for f in Native SandBoxCore SandBox CustomBattle; do grep -o 'XmlName id="[^"]*"' "<game>/Modules/$f/SubModule.xml" | sort | uniq -c; done 2026-09-05 -->

**Dependencies.** `TAOM`, `TAOM_Map` and `LOTRLOME_Armory` each declare exactly four `<DependedModule>`
rows, `Native`, `SandBoxCore`, `Sandbox`, `CustomBattle` (`Main/_Module/SubModule.xml:11-14`,
`TAOM_Map/SubModule.xml:9-12`, `LOTRLOME_Armory/SubModule.xml:9-12`). `TAOM.Dependencies` declares
none (`Dependencies/_Module/SubModule.xml:10`), because it must sit above `Native` itself, and orders
the other way round with 35 `<ModulesToLoadAfterThis>` rows (`:21-77`). <!-- measured: python ElementTree parse of the five manifests, len(findall('ModulesToLoadAfterThis/Module')) 2026-09-05 -->
No TAOM manifest names another TAOM module as a hard dependency; the only cross-reference is
`TAOM_Map/SubModule.xml:19`, a `<DependedModuleMetadata id="TAOM" order="LoadBeforeThis" />` row, in
the element the engine never parses.

**The other eleven folders in `Modules/`** (`StoryMode`, `NavalDLC`, `BirthAndDeath`, `FastMode`,
`Multiplayer`, `SandBoxCoreMP`, `Bannerlord.Diplomacy`, and the four stubs) are not part of the TAOM
set. `StoryMode` is in `TAOM.Dependencies`' load-after block (`Dependencies/_Module/SubModule.xml:26`);
`NavalDLC` matters only because it also ships a `SceneObj/Main_map`, and three installed modules do:
`NavalDLC`, `SandBox` and `TAOM_Map`. Load order picks the winner (see below). <!-- measured: for d in "<game>/Modules"/*/; do test -d "$d/SceneObj/Main_map" && basename "$d"; done 2026-09-05 -->

## Who owns what data

A file loads through one of three channels, an `<Xmls>` row in `SubModule.xml`, a `<file>` row in
`ModuleData/project.mbproj`, or the unregistered `ModuleData/Languages/` scan, and a file in none of
them is not loaded however correct it looks ([Submodule and registration](submodule-and-registration.md)).
Who registers what, measured from the four manifests:

| Object id | `TAOM` | `TAOM_Map` | `LOTRLOME_Armory` | `TAOM.Dependencies` |
|---|---|---|---|---|
| `NPCCharacters` | 44 | 1 (Kit stub) | 0 | 0 |
| `EquipmentRosters` | 27 | 0 | 0 | 0 |
| `GameText` | 15 | 0 | 0 | 0 |
| `SPCultures`, `Kingdoms`, `Factions`, `Heroes`, `SkillSets` | 2 each | 1 each except `Heroes` and `SkillSets` (Kit stubs) | 0 | 0 |
| `partyTemplates`, `BodyProperties`, `BannerIcons`, `CustomBattleScenes` | 1 each | `partyTemplates` 1 (Kit stub) | 0 | 0 |
| `Items` | 0 | 1 (Kit stub) | 21 | 0 |
| `Monsters` | 0 | 0 | 8 | 0 |
| `CraftingPieces`, `CraftingTemplates`, `WeaponDescriptions`, `ModuleSounds` | 0 | 0 | 1 each | 0 |
| `WorkshopTypes` | 0 | 1 (Kit stub) | 0 | 0 |
| `Settlements` | **0** | **1** | 0 | 0 |
| total `<XmlNode>` | 100 | 8 | 33 | 0 <!-- measured: grep -o 'XmlName id="[^"]*"' <manifest> | sort | uniq -c, over Main/_Module/SubModule.xml, Dependencies/_Module/SubModule.xml, TAOM_Map/SubModule.xml, LOTRLOME_Armory/SubModule.xml 2026-09-05 --> |

Three consequences, each with its file-level detail in [File catalogue](file-catalogue.md):

- **`TAOM_Map`'s seven stub rows contribute nothing.** The seven files are 197 to 245 bytes each and
  still carry the Modding Kit's placeholder comment; the one registration that makes the map real is
  `<XmlName id="Settlements" path="settlements"/>` at `TAOM_Map/SubModule.xml:73`. <!-- measured: wc -c on the seven files, and head of each 2026-09-05 -->
  The reverse case sits in the same folder: `settlement_tracks.xml` (7,390 bytes of `<MusicTrack>`
  rows) and `settlement_track_instruments.xml` (3,346 bytes of `<MusicInstrument>` rows) are real
  content that neither `SubModule.xml` nor `project.mbproj` mentions at all. <!-- measured: wc -c on both files; grep -c settlement_track over TAOM_Map/SubModule.xml and TAOM_Map/ModuleData/project.mbproj, 0 and 0 2026-09-05 -->
- **The repo's `settlements.xml` is a dead copy.** `id="Settlements"` occurs 0 times in
  `Main/_Module/SubModule.xml`; the repo file holds 863 `<Settlement>` elements in 1,023,041 bytes, the
  live `TAOM_Map/ModuleData/settlements.xml` 988 in 1,153,217 bytes ([CLAUDE.md](../../CLAUDE.md) Traps,
  "TAOM_Map settlements"). <!-- measured: grep -c 'id="Settlements"' Main/_Module/SubModule.xml; python re.findall(rb'<Settlement\s') over both files 2026-09-05 -->
- **`TAOM_Map`'s `project.mbproj` is inert.** 9 `<Module id=...>` rows and 0 `<file>` rows, and
  `GetMbprojxmls` selects only `base/file` nodes (`XmlResource.cs:117`); `TAOM`'s mbproj has 5 `<file>`
  rows and `LOTRLOME_Armory`'s 11, the form `Native` uses. <!-- measured: grep -c '<file ' and grep -c '<Module ' on each project.mbproj 2026-09-05 -->
  The channel's gate, `python tools/audit_mbproj_registration.py`, reports 39 vanilla `soln_*` ids,
  0 errors and 1 warning today ([lotrlome-soln-id-fix](../reference/lotrlome-soln-id-fix.md)). <!-- measured: python tools/audit_mbproj_registration.py 2026-09-05 -->

## Which folder is authoritative: repo or game install

**Repo modules: `TAOM` and `TAOM.Dependencies`.** `Main/_Module/` and `Dependencies/_Module/` are the
modules and the build copies them: `build.ps1` only restores and builds `TAOM.sln` (`build.ps1:20`,
`:27`); the `Bannerlord.BuildResources` 1.1.0.129 package's `CopyModule` target copies `<project>/_Module`
to `<game>/Modules/<ModuleId>` with `Clean="false"` (`Basic.targets:64-65`). [Module TAOM](module-taom.md)
walks every copy target. Two facts follow from `Clean="false"`:

- **Deployment is additive.** The repo `Main/_Module/ModuleData` has 367 files; the deployed
  `TAOM/ModuleData` has 371, and a file deleted from the repo lives on in the install. <!-- measured: find Main/_Module/ModuleData -type f | wc -l; find "<game>/Modules/TAOM/ModuleData" -type f | wc -l 2026-09-05 -->
  The deployed copy also holds `RuntimeDataCache/`, `Shaders/` and, in `TAOM.Dependencies`, runtime
  files no build step produces. <!-- measured: ls "<game>/Modules/TAOM"; ls "<game>/Modules/TAOM.Dependencies" 2026-09-05 -->
- **`-p:DisableModuleCopy=true` does not stop the copy.** Only the `PostBuildCopyToModules` wrapper
  carries that condition (`Basic.targets:47`); `CopyBinariesWindows` (`:53`) and `CopyModule` (`:64`)
  run regardless. Use `-p:ModuleId=` to skip all three ([agent-operating-manual](../ai-includes/agent-operating-manual.md), lines 49-51).

Both repo modules keep their whole `_Module` folder in the repo, and so do the 4 alias-stub manifests
under `Stubs/`. `Main/_Module/bin` holds 7 files, of which only `MinHook.x64.dll` and
`TAOM.NativeSkinFixes.dll` are vendored binaries the repo is allowed to carry ([CLAUDE.md](../../CLAUDE.md)
Traps, "Vendored DLLs"); the rest is build output. `Dependencies/_Module/bin` holds 42, the bundled
BUTR stack ([Module Dependencies](module-dependencies.md) has the allowlist). <!-- measured: find Main/_Module/bin -type f | wc -l; find Dependencies/_Module/bin -type f | wc -l; find Stubs -name SubModule.xml | wc -l 2026-09-05 -->

**Live-only modules: `TAOM_Map` and `LOTRLOME_Armory`.** The repo holds no copy of either module: the
only directory under the repo root carrying one of those names is a report output folder,
`tools/reports/mesh-audit/LOTRLOME_Armory`. <!-- measured: find . -type d -name TAOM_Map -o -type d -name LOTRLOME_Armory 2026-09-05 -->
There is no build and no deploy; an edit to `LOTRLOME_Armory/ModuleData/LOTRLOME_items/gondor/body_armors.xml`
is the deployment, and a module reinstall silently reverts it ([CLAUDE.md](../../CLAUDE.md) Traps, "A fix
in a dependency module"). The standing mitigation is an idempotent replay script plus an in-repo gate,
and for the highest-value Armory files a restore point under
[`docs/reference/lotrlome-armory-snapshot/`](../reference/lotrlome-armory-snapshot/README.md), whose
"DO NOT REGISTER" section (lines 41-43) is load-bearing: referencing those copies from
`Main/_Module/SubModule.xml` loads the same ids twice.

The validators reach the three surfaces unevenly. The commit hook reads the staged file list and keeps
only `Main/_Module/ModuleData/*.xml`
([`check-moduledata-validation.sh`](../../.claude/hooks/check-moduledata-validation.sh), lines 59 and 71),
so an edit in either live module is gated only when you run `python tools/validate_moduledata.py` by
hand ([moduledata-validation](../features/moduledata-validation.md)).
Counted today: `Main/_Module/ModuleData` holds 284 XML and 8 XSLT, `LOTRLOME_Armory/ModuleData` 425 XML
and 8 XSLT, `TAOM_Map/ModuleData` 44 XML and 1 XSLT. <!-- measured: find <root> -name "*.xml" | wc -l and -name "*.xslt" | wc -l over the three ModuleData folders 2026-09-05 -->

## What ships to players

A release is not the dev install zipped. `python tools/package_release.py --source "<game>/Modules" --dest <out> --dry-run`
copies an allow-list into a fresh folder and never deletes from the source
([`tools/package_release.py`](../../tools/package_release.py), lines 12-21); its default module set is
`TAOM TAOM_Map LOTRLOME_Armory TAOM.Dependencies` (`:53`). The dry run on this install today:
<!-- measured: python tools/package_release.py --source "<game>/Modules" --dest <scratch> --dry-run 2026-09-05 -->

| Module | Ships | Dropped | Files shipped |
|---|---|---|---|
| `TAOM` | 0.41 GB | 5.59 GB | 2,277 |
| `TAOM_Map` | 19.39 GB | 35.68 GB | 2,519 |
| `LOTRLOME_Armory` | 4.10 GB | 30.64 GB | 4,990 |
| `TAOM.Dependencies` | 0.04 GB | 0.00 GB | 142 |
| total | 23.94 GB of a 95.84 GB source | 71.91 GB | 9,928 |

It drops, by rule: `RUNTIME_DATA_CACHE` 38.38 GB (editor-generated; the shipping client reads it and
can never write it, `package_release.py:3-10`), `ASSET_SOURCES` 33.44 GB, `PREFABS_UNUSED` 0.05 GB,
`NATIVE_DEBUG` 0.03 GB, plus `*.xml.bak` files, the three runtime state files and any `.vs` folder
(`:103-143`). Two "candidates" ship because nobody has proved them droppable, `EmAssetPackages/`
(0.0 GB today) and `Assets/Race Test/` (0.96 GB), each removable with `--exclude-candidate`
(`:145-156`); an unrecognised top-level entry stops the run until you pass `--allow-unknown`
(`:355-359`), and `project.mbproj` is never excluded because the shipping runtime reads it
(`:126-135`). The four asset folders, and why `LOTRLOME_Armory` ships no `AssetPackages/` at all, are
[bannerlord-engine-and-toolchain](../reference/bannerlord-engine-and-toolchain.md) section 6.1 and
[CLAUDE.md](../../CLAUDE.md) Traps, "Armory asset trees".

Backup sidecars leave the modules first, because `.bak` breaks the Cloudflare distribution
([module-backup-sweep](../reference/module-backup-sweep.md)). `pwsh tools/sweep_module_backups.ps1` is
dry-run by default and `-Apply` moves them into a dated quarantine with a SHA256 manifest; today's dry
run found 40 files, 441.9 MB (9 under the repo's own `Main/_Module/ModuleData`, 9 under the deployed
`TAOM/ModuleData`, 13 under `LOTRLOME_Armory/ModuleData`, 9 Kit scene backups under `TAOM_Map` at
437.3 MB), no orphans. <!-- measured: pwsh tools/sweep_module_backups.ps1 (dry run) 2026-09-05 -->
The repo tree counts as a root because `CopyModule` redeploys a sidecar left there on the next build
while `.gitignore` hides it. `Main/_Module/` and `Dependencies/_Module/` each carry a
`THIRD-PARTY-LICENSES.txt` and the two live modules carry none, so for whatever they redistribute the
register is the only record: [provenance-register](../reference/provenance-register.md) names each
source and its licence, and its lines 20-21 point at the two files that do exist. <!-- measured: ls of the four module roots 2026-09-05 -->

## The four versions and how they pair

| Module | Value today | Where | When it changes |
|---|---|---|---|
| `TAOM` | `v2.0.28` | `Main/_Module/SubModule.xml:6` | Every release. It is what every crash bundle reports as `TaomVersion`, so it changes only in a release commit that is tagged `vX.Y.Z` and pushed ([release-process](../reference/release-process.md), "The contract") |
| `TAOM.Dependencies` | `v2.0.6` | `Dependencies/_Module/SubModule.xml:6` | Only when the Dependencies assembly changes ([release-process](../reference/release-process.md), "The three version fields"). `v2.0.6` is not a phantom TAOM version, it is this module's number (same doc, "The five phantom versions") |
| `TAOM_Map` | `v2.0.23` | `TAOM_Map/SubModule.xml:4` | By hand, in the live file. Not in git, so this string is the module's only version marker |
| `LOTRLOME_Armory` | `v2.0.23` | `LOTRLOME_Armory/SubModule.xml:4` | By hand, in the live file. Same |

**Issue #371** is why the first two are a pair: `Main/TAOM.csproj:89-91` resolves HarmonyLib and
UIExtenderEx through `TAOM.Dependencies.dll`, so a `TAOM.dll` run against a stale copy fails at the
member level while patches apply and every character renders in bind pose
(`Main/_Module/SubModule.xml:15-22`; [release-process](../reference/release-process.md), "The three
version fields"). That doc names a third field, a `<DependedModuleMetadata id="TAOM.Dependencies" version="v2.0.Y"/>`
row in `TAOM`'s manifest, and **that field is not in the file**. `TAOM.Dependencies` occurs exactly
once in `Main/_Module/SubModule.xml`, on line 15, as the opening line of the #371 comment itself;
neither the metadata row nor a `<DependedModule Id="TAOM.Dependencies" />` sibling is present, while
that comment still describes both as present. <!-- measured: grep -n "TAOM.Dependencies" Main/_Module/SubModule.xml 2026-09-05 -->
As shipped, only the `/release` skill's check enforces the pairing ([release-process](../reference/release-process.md),
"Cutting a release"); when and why the two rows left the file is an open question for the maintainer,
and [Module Dependencies](module-dependencies.md) carries the pairing in full.

The other two numbers pair with nothing: `TAOM_Map`'s only tie to `TAOM` is the versionless metadata
row at line 19, and `LOTRLOME_Armory` names `TAOM` nowhere. Both live modules still pin the engine at
`v1.4.5.*` in their `Native` metadata row (`TAOM_Map/SubModule.xml:15`, `LOTRLOME_Armory/SubModule.xml:15`)
while `TAOM` pins `v1.4.8.*` (`Main/_Module/SubModule.xml:25`) and the installed game reports `v1.4.8`
(`bin/Win64_Shipping_Client/Version.xml`, matching the repo pin file). <!-- measured: cat "<game>/bin/Win64_Shipping_Client/Version.xml"; cat .claude/pinned-game-version.txt 2026-09-05 -->
The stale values are harmless only because no launcher on this machine reads them. The alias stubs
carry a fifth kind of number, `v2.4.99.0` at `Stubs/Bannerlord.Harmony/_Module/SubModule.xml:44`: the
shipped package's minor with `.99`, so any `v2.4.*` lower bound in a third-party manifest is satisfied
([dr3-maintenance](../migration/dr3-maintenance.md), "Stub modules", the v99 rule).

## Where the launcher load order really comes from

`Bannerlord.exe` is the launcher, and the game starts inside the same process once you press Play
(`Program.cs:111`, `:115-121`). The order modules load in, and therefore the order XML from different
modules merges in, is decided in six steps:

1. **The launcher reads its saved list**, `<Documents>\Mount and Blade II Bannerlord\Configs\LauncherData.xml`
   (`UserDataManager.cs:10-12`, `:21-34`, `:42-58`). On this machine it holds 14 `<UserModData>` rows,
   each `Id`, `LastKnownVersion`, `IsSelected`; `LastKnownVersion` is a cache from the last launch, not
   a source of truth (it reads `v2.0.27.0` for `TAOM` while the manifest says `v2.0.28`). <!-- measured: grep -c "<UserModData>" "<Documents>/Mount and Blade II Bannerlord/Configs/LauncherData.xml" 2026-09-05 -->
2. **Saved order first, then everything else.** `LauncherModsVM.LoadSubModules` adds modules in the
   saved order, then appends any installed module the file does not mention (`LauncherModsVM.cs:138-157`).
3. **Then a topology sort.** `MBMath.TopologySort` with `ModuleHelper.GetDependentModulesOf` as the
   edge function (`LauncherModsVM.cs:158`): edges are each module's `<DependedModules>`
   (`ModuleHelper.cs:252-260`) and, in reverse, any module whose `<ModulesToLoadAfterThis>` names it
   (`:262-268`). The sort is a depth-first walk in input order (`MBMath.cs:930-958`), so modules with no
   edge between them keep the saved order; dragging a row re-runs it (`LauncherModsVM.cs:182-199`),
   which is why you cannot drag a module above something it depends on.
4. **Ticks.** A module is selected when it is `Native`, or when it was selected last time (or is
   official or `DefaultModule` on a first launch) and all of its non-optional dependencies are present
   (`LauncherModsVM.cs:160-163`, `:226-234`).
5. **The list becomes a string.** `ModuleListCode` builds `_MODULES_*Id*Id*..._MODULES_` from the
   selected modules (`LauncherModsVM.cs:27-45`), appended to the game arguments (`LauncherUI.cs:49`,
   `Program.cs:117-118`); the list is written back to `LauncherData.xml` on every start (`LauncherVM.cs:459-469`).
6. **The engine walks that string in order.** `Utilities.GetModulesNames()` splits it on `*`
   (`Utilities.cs:243-246`); `ModuleHelper.InitializeModules` inserts each id in that order
   (`ModuleHelper.cs:63-100`), `GetModules()` walks `_loadedModules` in the order the ids went in
   (`:178-189`), and `LoadSubModules` registers each module's `project.mbproj` and `<Xmls>` in that
   order (`Module.cs:261-267`, `:1029-1033`).

So `<DependedModules>` does not order singleplayer loading by itself (the engine's own sort,
`GetSortedModules`, has only multiplayer callers) and `<DependedModuleMetadatas>` is read by nobody on
a vanilla install: the whole `TaleWorlds.MountAndBlade.Launcher.Library` decompile contains no reference
to the word `Metadata`, so every version pin TAOM writes in that block is documentation until a BUTR
launcher reads it, and BLSE is not installed on this machine. <!-- measured: grep -rn GetSortedModules over the v1.4.8 category tree, 3 hits (the definition plus CustomBattleServer.cs:208 and LobbyClient.cs:474); grep -rn "Metadata" over the Launcher.Library folder, none; ls "<game>/bin/Win64_Shipping_Client" and "<game>/Modules" for BLSE, none 2026-09-05 -->
[Load order and dependencies](load-order-and-dependencies.md) section B carries both corrections and
the merge rule. Why the order matters: `TAOM.Dependencies` must construct before `Native` because it
installs an `AssemblyResolve` redirect from a static constructor ([coop-interop](../features/coop-interop.md),
"Load order"), and `TAOM_Map` must merge after `SandBox` because `GetMainMapModule` returns the last
active module that owns `SceneObj/Main_map/scene.xscene` (`MapScene.cs:203-211`,
[worldmap-battle-scene-grid](../reference/worldmap-battle-scene-grid.md), "last active module wins").
The chain the project expects, from [coop-interop](../features/coop-interop.md) lines 111-114:
`TAOM.Dependencies`, the BUTR alias stubs, `Native`, `SandBoxCore`, `Sandbox`, `StoryMode`,
`CustomBattle`, `TAOM`, `TAOM_Map`, `LOTRLOME_Armory`, then any co-op mod.

**The four alias stubs are invisible to the vanilla launcher as deployed.** `DeployTAOMDependenciesStubs`
copies `Stubs/**/SubModule.xml` preserving the `_Module` segment (`Dependencies/TAOM.Dependencies.csproj:90-103`),
so the deployed `Modules/Bannerlord.Harmony/` contains only `_Module/SubModule.xml`, one level below
where `GetPhysicalModules` looks (`ModuleHelper.cs:327-330`). <!-- measured: ls -R "<game>/Modules/Bannerlord.Harmony" 2026-09-05 -->
None of the 14 `<Id>` rows in `LauncherData.xml` is a stub id; the four stub DLL names do occur there,
4 times, but only as `<DLLName>` rows under `<DLLCheckData>`, the DLL check, not the module list. <!-- measured: grep -c "<Id>Bannerlord\.\(Harmony\|ButterLib\|UIExtenderEx\|MBOptionScreen\)<" LauncherData.xml returns 0 (the only Bannerlord.* id row is Bannerlord.Diplomacy); grep -n "Bannerlord\.\(Harmony\|ButterLib\|UIExtenderEx\|MBOptionScreen\)" returns 4 DLLName lines 2026-09-05 -->
So `<DefaultModule value="true" />` at `Stubs/Bannerlord.Harmony/_Module/SubModule.xml:47` cannot tick
anything, and the `<SingleplayerModule>`, `<MultiplayerModule>`, `<Official>` and `<Url>` rows beside it
are read by no branch of `ModuleInfo.LoadWithFullPath` at all: they are launcher-compatibility
vocabulary carried from BetaDeps (comment at lines 48-50 of the same file). Whether BLSE scans a level
deeper is an open question; [Module Dependencies](module-dependencies.md) owns the stubs.

To change the order on your own machine: tick or drag in the launcher and press Play. Editing
`LauncherData.xml` by hand does not override the manifests, because step 3 re-sorts whatever it reads.

## Worked example

The four TAOM manifests side by side, with each element explained once. These are contiguous slices of
the files, not whole files, and none of them is a data entry addressed by an `id=` of its own, so each
block is marked as an excerpt rather than an example.

**`TAOM`, the identity block and the four hard dependencies** (`Main/_Module/SubModule.xml`, lines 1-14):

<!-- excerpt file="Main/_Module/SubModule.xml" -->
```xml
<?xml version="1.0" encoding="UTF-8"?>
<Module xmlns:xsi='http://www.w3.org/2001/XMLSchema-instance'
        xsi:noNamespaceSchemaLocation="https://raw.githubusercontent.com/BUTR/Bannerlord.XmlSchemas/master/SubModule.xsd">
  <Id value="TAOM" />
  <Name value="TAOM" />
  <Version value="v2.0.28" />
  <DefaultModule value="false" />
  <ModuleCategory value="Singleplayer"/>
  <ModuleType value="Community" />
  <DependedModules>
    <DependedModule Id="Native" />
    <DependedModule Id="SandBoxCore" />
    <DependedModule Id="Sandbox" />
    <DependedModule Id="CustomBattle" />
```

1. `<Id value="TAOM" />` is what every other manifest and every crash bundle refers to. It matches the
   folder name here, but `Sandbox` proves it need not (`SandBox/SubModule.xml:4`).
2. `<Version value="v2.0.28" />` changes only in a tagged release commit; it is the crash-bundle key.
3. `<DependedModule Id="Sandbox" />` uses the declared id, lower-case b; `SandBox` would match nothing
   in `GetDependentModulesOf`, which compares ids case-sensitively (`ModuleHelper.cs:256`).

**`TAOM`, the C# entry point** (`Main/_Module/SubModule.xml`, lines 56-66):

<!-- excerpt file="Main/_Module/SubModule.xml" -->
```xml
  <SubModules>
    <SubModule>
      <Name value="TAOM" />
      <DLLName value="TAOM.dll" />
      <SubModuleClassType value="TAOM.SubModule" />
      <Tags>
        <Tag key="DedicatedServerType" value="none" />
        <Tag key="IsNoRenderModeElement" value="false" />
      </Tags>
    </SubModule>
  </SubModules>
```

1. `<DLLName value="TAOM.dll" />` is probed at `bin\Win64_Shipping_Client` and loaded from
   `bin/<ConfigName>`; `Main/TAOM.csproj:166-195` mirrors the client folder into the others.
2. `<Tag key="DedicatedServerType" value="none" />` is the vanilla `SandBox` pair; any other value
   forces the DLL to count as certified (`SubModuleInfo.cs:87-90`), so shipping server binaries does
   not by itself make TAOM server-capable ([bannerlord-engine-and-toolchain](../reference/bannerlord-engine-and-toolchain.md), section 1.2).

**`TAOM.Dependencies`, the header** (`Dependencies/_Module/SubModule.xml`, lines 1-10):

<!-- excerpt file="Dependencies/_Module/SubModule.xml" -->
```xml
<?xml version="1.0" encoding="UTF-8"?>
<Module xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance"
        xsi:noNamespaceSchemaLocation="https://raw.githubusercontent.com/BUTR/Bannerlord.XmlSchemas/master/SubModule.xsd">
	<Id value="TAOM.Dependencies" />
	<Name value="TAOM Dependencies" />
	<Version value="v2.0.6" />
	<DefaultModule value="false" />
	<ModuleCategory value="Singleplayer" />
	<ModuleType value="Community" />
	<DependedModules />
```

1. `<DependedModules />` is empty on purpose: adding `Native` here would force the module below
   `Native` in the sort and defeat the static-constructor redirect.
2. `<Version value="v2.0.6" />` moves only when the Dependencies assembly changes.

**`TAOM.Dependencies`, its own entry** (`Dependencies/_Module/SubModule.xml`, lines 163-169):

<!-- excerpt file="Dependencies/_Module/SubModule.xml" -->
```xml
		<SubModule>
			<Name value="TAOM.Dependencies" />
			<DLLName value="TAOM.Dependencies.dll" />
			<SubModuleClassType value="TAOM.Dependencies.SubModule" />
			<Assemblies />
			<Tags />
		</SubModule>
```

1. Seven `<SubModule>` blocks in this one manifest boot other projects' classes out of this module's
   `bin/`: UIExtenderEx, TAOM.Dependencies itself, ButterLib twice, MCMv5 twice, and the MBOptionScreen
   loader (`SubModuleClassType` rows at lines 153, 166, 175, 193, 205, 215 and 229).
2. The loader entry at lines 226-235 carries `<Tag key="LoaderFilter">` and two
   `<Tag key="LoaderSubModuleOrder">` rows. Neither key is in the engine's `SubModuleTags` enum
   (`SubModuleInfo.cs:12-21`), so `Enum.TryParse` drops both silently and only the BUTR loader class
   reads them back out of the file.

**`TAOM_Map`, the whole header** (`TAOM_Map/SubModule.xml`, lines 1-21):

<!-- excerpt file="TAOM_Map/SubModule.xml" -->
```xml
<Module>
	<Name value="TAOM_Map"/>
	<Id value="TAOM_Map"/>
	<Version value="v2.0.23" />
	<DefaultModule value="false" />
	<ModuleCategory value="Singleplayer"/>
	<ModuleType value="Community" />
	<DependedModules>
		<DependedModule Id="Native" />
		<DependedModule Id="SandBoxCore" />
		<DependedModule Id="Sandbox" />
		<DependedModule Id="CustomBattle" />
	</DependedModules>
	<DependedModuleMetadatas>
		<DependedModuleMetadata id="Native" order="LoadBeforeThis" version="v1.4.5.*" />
		<DependedModuleMetadata id="SandBoxCore" order="LoadBeforeThis" />
		<DependedModuleMetadata id="Sandbox" order="LoadBeforeThis" />
		<DependedModuleMetadata id="CustomBattle" order="LoadBeforeThis" />
		<DependedModuleMetadata id="TAOM" order="LoadBeforeThis" />
	</DependedModuleMetadatas>
	<SubModules/>
```

1. `<SubModules/>` is self-closing: the map ships no code, and both of its `bin/` subfolders are empty.
2. `<DependedModuleMetadata id="TAOM" order="LoadBeforeThis" />` is the only place any TAOM manifest
   names another TAOM module, and it sits in the element the engine does not parse.
3. The one live registration is `<XmlName id="Settlements" path="settlements"/>` at line 73; the other
   seven `<XmlNode>` rows point at Kit stubs.

**`LOTRLOME_Armory`, the header and the first `Items` folder** (`LOTRLOME_Armory/SubModule.xml`, lines
2-30; line 1 is `<Module>` preceded by a byte-order mark):

<!-- excerpt file="LOTRLOME_Armory/SubModule.xml" -->
```xml
	<Name value="LOTRLOME_Armory"/>
	<Id value="LOTRLOME_Armory"/>
	<Version value="v2.0.23" />
  <DefaultModule value="false" />
  <ModuleCategory value="Singleplayer"/>
  <ModuleType value="Community" />
  <DependedModules>
    <DependedModule Id="Native" />
    <DependedModule Id="SandBoxCore" />
    <DependedModule Id="Sandbox" />
    <DependedModule Id="CustomBattle" />
  </DependedModules>
  <DependedModuleMetadatas>
    <DependedModuleMetadata id="Native" order="LoadBeforeThis" version="v1.4.5.*" />
    <DependedModuleMetadata id="SandBoxCore" order="LoadBeforeThis" />
    <DependedModuleMetadata id="Sandbox" order="LoadBeforeThis" />
    <DependedModuleMetadata id="CustomBattle" order="LoadBeforeThis" />
  </DependedModuleMetadatas>
	<SubModules/>
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

1. `path="LOTRLOME_items/gondor"` names a folder, so every `*.xml` inside it loads and a backup named
   `*.xml` there becomes duplicate item ids ([Submodule and registration](submodule-and-registration.md)).
2. `<GameType value = "EditorGame"/>` is what makes the items visible inside the Modding Kit.
3. `version="v1.4.5.*"` on the `Native` row is stale against the installed `v1.4.8`, and inert on a
   vanilla launcher.

## Reading order for a brand-new modder

1. [README](README.md), then this chapter, so you know which of the eight modules a job lands in.
2. [Editing safely](editing-safely.md): backups, byte-order marks, line endings, the `.xml` glob trap,
   the parser smoke test, and the one micro-recipe every Modify recipe reuses.
3. [Submodule and registration](submodule-and-registration.md): the `<Xmls>` rows, the `project.mbproj`
   rows, and the file-or-folder rule that decides whether a new file loads.
4. [Load order and dependencies](load-order-and-dependencies.md): merge order, the XSLT layer, forward
   references, and when a change shows up (restart, new campaign, next load).
5. [Id cheatsheet](id-cheatsheet.md) and [File catalogue](file-catalogue.md), open in a second window.
6. [Module Dependencies](module-dependencies.md), [Module TAOM](module-taom.md),
   [Module Armory](module-armory.md), [Module Map](module-map.md): one chapter per TAOM module.
7. [Recipe: new mod from zero](recipe-new-mod-from-zero.md): an empty folder to a module the launcher
   lists, then to data.
8. The file chapter for whatever you are changing: [Items: armour](items-armor.md) and its three item
   siblings, [Troops](troops.md), [Equipment rosters](equipment-rosters.md), [Cultures](cultures.md),
   [Party templates](party-templates.md), [Clans](clans.md), [Kingdoms](kingdoms.md),
   [Settlements](settlements.md), [Banners and heraldry](banners-and-heraldry.md),
   [Strings and localization](strings-and-localization.md), the character and `configs-*` chapters.
9. [Validation and testing](validation-and-testing.md) before you claim anything works, and
   [Troubleshooting](troubleshooting.md) when it does not.

## Numbers in this chapter

All measured 2026-09-05 on this machine; the game install is referred to as `<game>`.

- 19 module folders, 15 with a root `SubModule.xml`, 4 without: `ls -d "<game>/Modules"/*/ | wc -l` and a `test -f "$d/SubModule.xml"` loop.
- Manifest element counts (Main / Dependencies / Map / Armory): `<XmlNode>` 100 / 0 / 8 / 33; `<DependedModule>` 4 / 0 / 4 / 4; `<ModulesToLoadAfterThis>` rows 3 / 35 / 0 / 0; `<DependedModuleMetadata>` 7 / 5 / 5 / 4; `<SubModule>` 1 / 7 / 0 / 0: Python `xml.etree.ElementTree` `findall` over each file.
- Registered ids per manifest (the "Who owns what data" table and the vanilla counts): `grep -o 'XmlName id="[^"]*"' <file> | sort | uniq -c`.
- `project.mbproj` rows: `TAOM` 5 `<file>` (4 voice definitions, 1 module sound), `LOTRLOME_Armory` 11 `<file>`, `TAOM_Map` 0 `<file>` and 9 `<Module>`, `Native` 50 `<file>`, `SandBox` 0 `<file>`; `SandBoxCore`, `CustomBattle` and `TAOM.Dependencies` have no `project.mbproj` at all: `grep -c '<file '` and `grep -c '<Module '` over each, and `test -f`.
- 39 distinct `soln_*` ids in `Native/ModuleData/project.mbproj` (`grep -o 'id="[^"]*"' <file> | sort -u | wc -l`), which is the whole vocabulary the audit checks against (`audit_mbproj_registration.py:96-100`); the audit itself reports the same 39, 0 errors and 1 warning: `python tools/audit_mbproj_registration.py`.
- `TAOM_Map` stub data files 197 to 245 bytes: `wc -c` on the seven files its manifest names. Its two unregistered music files are 7,390 and 3,346 bytes, and `grep -c settlement_track` returns 0 for both its manifest and its `project.mbproj`.
- Settlements: repo shadow 863 elements in 1,023,041 bytes, live 988 in 1,153,217 bytes: Python `re.findall(rb'<Settlement\s', data)` and `len(data)`; 0 `id="Settlements"` rows in `Main/_Module/SubModule.xml`: `grep -c`.
- `ModuleData` file counts: repo 367, deployed 371 (`find -type f | wc -l`); XML / XSLT: `Main` 284 / 8, `LOTRLOME_Armory` 425 / 8, `TAOM_Map` 44 / 1 (`find -name "*.xml" | wc -l`, `-name "*.xslt"`).
- Repo module folders: `Main/_Module/bin` 7 files, `Dependencies/_Module/bin` 42, 4 stub manifests under `Stubs/`, and no `TAOM_Map` or `LOTRLOME_Armory` module directory anywhere under the repo root: `find <path> -type f | wc -l`, `find Stubs -name SubModule.xml | wc -l`, `find . -type d -name <name>`.
- Modules owning a `SceneObj/Main_map`: 3 (`NavalDLC`, `SandBox`, `TAOM_Map`): a `test -d "$d/SceneObj/Main_map"` loop over `<game>/Modules`.
- Deployed `bin/` folders: `TAOM` Client 10, wEditor 12, Server 10, Gaming.Desktop 4; `TAOM.Dependencies` Client 42, Server 42, wEditor 0, Gaming.Desktop 3: `ls <folder> | wc -l`.
- Sizes: `TAOM` 6,147 MB (RuntimeDataCache 5,141); `TAOM.Dependencies` 46 MB (bin 42); `TAOM_Map` 56,404 MB (RuntimeDataCache 21,205, AssetSources 15,286, AssetPackages 12,859, Assets 3,929, SceneEditData 2,219, SceneObj 735, ModuleData 14); `LOTRLOME_Armory` 35,595 MB (AssetSources 18,415, RuntimeDataCache 12,978, Assets 4,148, ModuleData 22): `du -sm "<game>/Modules/<Id>"` and `du -sm "<game>/Modules/<Id>"/*`.
- Release dry run: ships 23.94 GB of 95.84 GB, drops 71.91 GB (RUNTIME_DATA_CACHE 38.38, ASSET_SOURCES 33.44, PREFABS_UNUSED 0.05, NATIVE_DEBUG 0.03), per module `TAOM` 0.41 GB / 2,277 files, `TAOM_Map` 19.39 / 2,519, `LOTRLOME_Armory` 4.10 / 4,990, `TAOM.Dependencies` 0.04 / 142, candidates RACE_TEST 0.96 GB and EM_ASSET_PACKAGES 0.0 GB: `python tools/package_release.py --source "<game>/Modules" --dest <scratch> --dry-run`.
- Backup sweep dry run: 40 files, 441.9 MB (repo `Main/_Module` 9, deployed `TAOM` 9, `LOTRLOME_Armory` 13, `TAOM_Map` scene backups 9 at 437.3 MB), 0 orphans: `pwsh tools/sweep_module_backups.ps1`.
- `LauncherData.xml`: 14 `<UserModData>` rows, 0 `<Id>` rows naming a stub, 4 `<DLLName>` rows naming a stub DLL: `grep -c "<UserModData>"`, `grep -c "<Id>Bannerlord\.\(Harmony\|ButterLib\|UIExtenderEx\|MBOptionScreen\)<"`, `grep -n "Bannerlord\.\(Harmony\|ButterLib\|UIExtenderEx\|MBOptionScreen\)"`.
- `TAOM.Dependencies` mentions in `Main/_Module/SubModule.xml`: 1, on line 15, inside a comment: `grep -n "TAOM.Dependencies" Main/_Module/SubModule.xml`.
- Seven `<SubModule>` blocks in `Dependencies/_Module/SubModule.xml`, at `<SubModuleClassType>` lines 153, 166, 175, 193, 205, 215, 229: `grep -n "<SubModuleClassType" Dependencies/_Module/SubModule.xml`.
- `GetSortedModules` call sites: 3 hits, the definition plus two multiplayer callers; `Metadata` in `TaleWorlds.MountAndBlade.Launcher.Library`: 0 hits: `grep -rn` over the v1.4.8 category decompile.
- Engine version `v1.4.8`: `cat "<game>/bin/Win64_Shipping_Client/Version.xml"` and `cat .claude/pinned-game-version.txt`.
- `THIRD-PARTY-LICENSES.txt` present in `Main/_Module/` and `Dependencies/_Module/`, absent from both live modules: `ls` of the four module roots.

## Read next

- [bannerlord-engine-and-toolchain](../reference/bannerlord-engine-and-toolchain.md): the two builds, the four `bin/` folders, the four asset folders.
- [release-process](../reference/release-process.md): the version contract, the #371 pairing, the phantom versions.
- [module-backup-sweep](../reference/module-backup-sweep.md): what the sweep moves and why the repo tree is a root.
- [`tools/package_release.py`](../../tools/package_release.py) and [`tools/README.md`](../../tools/README.md): the packager and the full tool registry.
- [dr3-maintenance](../migration/dr3-maintenance.md): the Dependencies module's maintenance manual, the stubs and the v99 rule.
- [coop-interop](../features/coop-interop.md): the load-order chain and why Dependencies sits above Native.
- [lotrlome-soln-id-fix](../reference/lotrlome-soln-id-fix.md) and [`lotrlome-armory-snapshot/README.md`](../reference/lotrlome-armory-snapshot/README.md): the native registration channel and the Armory restore point.
- [moduledata-validation](../features/moduledata-validation.md) and the [moduledata-validation rule](../../.claude/rules/moduledata-validation.md): what each validator reaches in each module.
- [worldmap-battle-scene-grid](../reference/worldmap-battle-scene-grid.md), [main-map-vista](../reference/main-map-vista.md) and [taom-map-settlement-naming](../reference/taom-map-settlement-naming.md): the map module's own references.
- [provenance-register](../reference/provenance-register.md): what the shipped modules redistribute and under which terms.
- [CLAUDE.md](../../CLAUDE.md) Traps ("TAOM_Map settlements", "A fix in a dependency module", "Three-module data surface", "Vendored DLLs", "Armory asset trees") and [agent-operating-manual](../ai-includes/agent-operating-manual.md) for the `-p:DisableModuleCopy=true` caveat.

---

<!-- backlinks-start auto-generated; edit lint_docs.py / build_backlinks.py to change -->

## Referenced by

- [docs/INDEX.md](../INDEX.md)
- [docs/modding/module-dependencies.md](./module-dependencies.md)
- [docs/modding/module-map.md](./module-map.md)
- [docs/modding/module-taom.md](./module-taom.md)
- [docs/modding/README.md](./README.md)
- [docs/modding/recipe-new-mod-from-zero.md](./recipe-new-mod-from-zero.md)
- [docs/modding/validation-and-testing.md](./validation-and-testing.md)

<!-- backlinks-end -->
