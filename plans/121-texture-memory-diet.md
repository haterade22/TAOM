# Plan 121: Ship TAOM's banner atlases and loading sheets compressed, drop 39 dead sprite sheets, and let the v1.4.8 Modding Kit load TAOM

> **Executor instructions**: This plan has five parts with different owners. Parts A and C are for an
> ordinary session working only in the WP121 git worktree. Parts B and D belong to the maintainer:
> they run the v1.4.8 Modding Kit and the game and write into the game install, which no session may
> do. Part E is conditional (a maintainer decision first, then a session for its gate). Follow your
> part step by step, run every verification command and confirm its expected result before moving
> on. If anything in "STOP conditions" occurs, stop and report; do not improvise. Do not edit
> `plans/README.md`: every WP branch shares it, so the integrator records this plan's status and test
> counts when merging. Put the "Hand-off report" items in your final message instead. Every code
> block starts at column 0: copy it exactly as shown.
>
> **Drift check (run first, Step A0.3)**:
> `git diff --stat 1b00d1acd..HEAD -- Dependencies/TAOM.Dependencies.csproj Dependencies/Foundation/IncompatibleModDetector.cs Main/_Module/Assets Main/_Module/AssetSources/GauntletUI Main/_Module/GUI/TAOMSpriteData.xml docs/features/scene-scripts.md docs/scene-scripts/map-maker-quickstart.md docs/reference/bannerlord-engine-and-toolchain.md docs/reference/banner-icon-generation.md docs/features/gui-sprite-system.md docs/modding/open-questions.md docs/modding/banners-and-heraldry.md docs/modding/module-taom.md`
> Expected: earlier WPs changed `Dependencies/TAOM.Dependencies.csproj` (WP115 removes the
> Gaming.Desktop lines and adds `FailOnGamePassStagingInModule` before `CopyBinariesToModuleFolder`;
> WP118 bumps package versions) and possibly `docs/reference/bannerlord-engine-and-toolchain.md`
> (WP114 and WP117 edit other rows) and `docs/modding/module-taom.md` (WP102 line 352, WP115 the
> `bin/` row). Step A0.3's
> anchor checks decide; STOP only when one of them fails.

## Status

- **Priority**: P1. The pack's draft said P0; D23 caps the claim at a texture-memory saving that most
  likely sits in GPU memory, so this follows the memory deep dive's P1 for MEMORY-02.
- **Effort**: M overall. Part A: S (three small commits). Part B: M (one maintainer session at the
  Kit, about two hours, plus three game launches). Part C: S. Part D: S, at release time. Part E: M,
  conditional.
- **Risk**: MED. The code is small and inert in the game (LOW). The Kit round trip has one known
  hazard: a texture descriptor and its compiled `RuntimeDataCache` entry that disagree render the
  sheet's sigils blank and crash the Kit on its next start. Every step that could cause it is fenced.
- **Depends on**: WP101 (`plans/101-baseline-sync.md`). By wave order WP102 to WP116 merge first;
  this plan relies only on WP102's rule that no plan stages `CHANGELOG.md` (D1).
- **Category**: perf (texture memory) and dx (Modding Kit enablement)
- **Wave / lane**: wave 2, lane "kit", parallel with WP117 to WP120 and WP122. WP117 edits
  `tools/audit_map_scene_memory.py`, which this plan only imports (its port, `3dac7ab5b`, leaves
  `iter_tpac_items` and `parse_texture_meta` unchanged). WP122's optional plan 019 part edits line 9
  of `Dependencies/TAOM.Dependencies.csproj`; this plan appends after its last target.
- **Executor**: Kit. Parts A and C: an ordinary session on the laptop in worktree
  `C:\Users\mikew\source\repos\taom-port\121`. Parts B and D: the maintainer at the laptop's v1.4.8
  Modding Kit (`<game>\bin\Win64_Shipping_wEditor\Version.xml` reads `v1.4.8`). Part E: the
  maintainer on a machine holding TAOM_Map's loose sources, then an ordinary session for the gate.
- **Decisions applied**: D1 (commit body is the changelog entry; never stage `CHANGELOG.md`), D7
  (`ClientDeploymentTests` stays out), D8 (subjects carry `v2.0.28`), D9 (no CI: every gate runs
  locally), D10, D19 (git only through the Bash tool), D20 (this plan changes no ModuleData), D23
  (port through a v1.4.8 Kit re-import and a pack republish; never promise private memory), D24 (BC7
  only for UI sheets the 1.4.x rule does not protect, each with a visual check).
- **Planned at**: 1.4.x `origin/bannerlord-1.4.5` `1b00d1acd`; 1.5.x `origin/bannerlord-1.5.x`
  `e45f6df2c`; 2026-11-04. (`origin/bannerlord-1.5.x` has since moved to `ca1cba35f`; none of the 13
  newer commits touches a path this plan reads.)
- **Issue**: tracking references #913 (`8cdb11408`, `ab338de2d`) and #1047 (`9985f2db3`,
  `b41de91e8`): "Port of #913 and #1047". New work (the D24 loading sheets, the texture-format gate,
  Part E): orchestrator creates the issue before implementation lands.

## Why this matters

On a 16 GB machine TAOM 1.4.8 sits at 9 to 10 GB on the campaign map (V07), and TAOM's own
`pack0.tpac` spends texture memory in three ways the 1.5.x line already fixed or documented. All 33
banner sigil atlases ship as uncompressed 4096x4096 RGBA8 with one mip: 64 MiB each, 2,112 MiB for
the set (decoded this session from the repo descriptors and the laptop's installed pack). The 19 that
a campaign's banner keys name are 1,216 MiB; as DXT1 they are 152 MiB (V07). That memory most likely
sits on the graphics card, not in private bytes (V07 section 2.4), so this plan promises a texture
memory saving and never a private-memory one (D23); it matters most on 8 GB cards such as the E1/E2
player's RTX 3070. The two always-loaded loading-screen sheets are RGBA8 with 13 mips, 85.3 MiB each,
held from the main menu to quit; BC7 makes them 21.3 MiB each (D24). And the pack carries 39 dead
banner-icon sprite sheets that the engine never asks for: 41,529,118 of the laptop pack's 188,902,921
bytes (the 1.5.x audit found that pack identical to the public channel's on 2026-10-03).

None of this can be fixed today, because the v1.4.8 Modding Kit cannot load TAOM on the 1.4.x line.
It looks for each submodule's DLL in the module's own `Win64_Shipping_wEditor` folder and then in its
own bin; TAOM.Dependencies has no such folder, and the community modules that once supplied Harmony,
MCMv5 and UIExtenderEx are DLL-less stubs. Once the Kit can load it, a Kit session would leave
TAOM.Dependencies' crash-loop marker behind and make the next game report a crash loop that never
happened. This plan ports the two 1.5.x fixes that let the Kit load TAOM, re-imports the textures in
the v1.4.8 Kit with the settings 1.5.x shipped, pins the result with tests, and routes the pack
republish to the next release. Part E plans the last map lever: the campaign map's 16K vista texture,
341.3 MiB that v1.4.8 holds in system memory and that vanilla's map does not spend.

## Current state

### Cherry-pick simulation (this session)

`git merge-tree --write-tree --name-only --no-messages --merge-base=<sha>^ origin/bannerlord-1.4.5 <sha>`:

| Commit | Files | Conflicted | Notes |
|---|---|---|---|
| `8cdb11408` | 1 | 0 | clean; not used (D23: the descriptor is re-saved by the v1.4.8 Kit) |
| `ab338de2d` | 34 | 1 | `docs/ai-includes/orientation.md`, absent on 1.4.x (modify/delete) |
| `9985f2db3` | 212 | 34 | csproj (only the trailing `<Import Project="..\GameReferences.targets" />` context, which 1.4.x lacks), 19 BannerIcons descriptors (the repack), `clans.xml`, `taom_spkingdoms.xml`, `ClientDeploymentTests.cs` (absent), docs and realm tools |
| `b41de91e8` | 27 | 22 | csproj, `clans.xml`, `factions.json`, `taom_spcultures.xml`, `ReflectionSiteBindingTests.cs`, docs, lessons, `pack_banner_sheets.py`, generators |

No commit is cherry-picked. The two #1047 commits are taken as path-limited hunks (a
`git apply --check` of `b41de91e8`'s detector and test hunks passed this session), and the 33
descriptors come from the Kit. The WP101 merge with `origin/fix/public-armory-ids-145` touches only
troop data and docs, none of these paths.

### Files and excerpts (origin/bannerlord-1.4.5)

- `Dependencies/TAOM.Dependencies.csproj` (126 lines): deploy targets. `DeployTAOMDependenciesStubs`
  (`:90-103`) deploys the DLL-less `Stubs/**/SubModule.xml`. The only mirror goes to the server bin,
  `:115-124` (excerpt below), followed by a blank line and `</Project>` at `:126`. The model is
  `Main/TAOM.csproj:166-175`, which mirrors TAOM itself into `Win64_Shipping_wEditor` with the same
  shape. A dry run this session confirmed that renaming the server target's item, target name and
  destination yields `b41de91e8`'s editor target line for line.

```
<Target Name="MirrorWin64ShippingClientToServer"
        AfterTargets="PostBuildCopyToModules"
        Condition="$(ModuleId) != '' AND Exists($(GameFolder)) AND $(DisableModuleCopy) != 'true' AND Exists('$(GameFolder)\Modules\$(ModuleId)\bin\Win64_Shipping_Client\')">
    <ItemGroup>
        <_TaomDepsServerMirrorFiles Include="$(GameFolder)\Modules\$(ModuleId)\bin\Win64_Shipping_Client\*.*" />
```

- `Dependencies/Foundation/IncompatibleModDetector.cs` (284 lines): `:32`
  `private static int _earlyPhaseRan;`, `:40` `public static void RunEarlyPhase()`, `:49` `try`,
  `:51` `var moduleDir = RuntimeLog.ModuleDir;`. The hunk contexts equal `b41de91e8^`; the only
  difference is comment text at `:171-196`. No 1.4.x test or snapshot doc pins a line of this file
  (`git grep` for `IncompatibleModDetector` and `GetActiveModules` in `TAOM.Tests` and
  `docs/reference/taleworlds-api-snapshot` is empty), so `b41de91e8`'s line-pin edits are not needed.
- Test reach: `TAOM.Tests` references `Main/TAOM.csproj`, which references
  `Dependencies/TAOM.Dependencies.csproj` (`Main/TAOM.csproj:89`), and
  `Dependencies/TAOM.Dependencies.csproj:32` grants `InternalsVisibleTo TAOM.Tests`. MSTest is 3.1.1
  and `[DataRow(null, ...)]` is already used. The csproj-pin pattern to copy is
  `TAOM.Tests/Infrastructure/Dependencies/BundledDependencyManifestTests.cs:27-31` (RepoRoot from
  `AppDomain.CurrentDomain.BaseDirectory` plus `..\..\..\..`, then `XDocument`).
- `Main/_Module/Assets/BannerIcons/`: 33 `_tex.tpac` (24 RGBA8, 9 RGBA8 with `has_alpha`, all
  4096x4096 with 1 mip), 33 `_mtl.tpac`, and a stray Kit temp file
  `taom_banners_dunland_alpha_01_tex.tpac.ptemp` (543 B). Tree hashes equal 1.5.x's before #913
  (`Assets/BannerIcons` `12e0f7d56570`, `AssetSources/BannerIcons` `30dab35505f0`, `banner_icons.xml`
  `76faa0c88cfc`, `TAOMSpriteData.xml` `73edf51d3b9b`, `GauntletUI` trees identical).
- `Main/_Module/Assets/GauntletUI/` (52 descriptors) and `Main/_Module/AssetSources/GauntletUI/`
  (52 PNGs, 576,855,466 B for the whole `AssetSources`). Decoded formats:

  | Sheets | Format | Mips | MiB each | Note |
  |---|---|---|---|---|
  | `ui_loading_1`, `_2` | R8G8B8A8_UNORM, has_alpha | 13 | 85.3 | D24 target: BC7 |
  | `ui_taom_bannericons_1`, `_2` | R8G8B8A8_UNORM, has_alpha | 1 | 64.0 | protected, stays |
  | `ui_taom_1` to `_5`, `ui_taom_career_system_1` | DXT5 | 13 | 21.3 | untouched |
  | `ui_custom_fonts_1` to `_3` | BC4 | 12 | 2.7 | untouched |
  | `ui_taom_bannericons_3` to `_41` | 37 RGBA8, 2 DXT5 | | 2,410.7 total | dead |

- `Main/_Module/GUI/TAOMSpriteData.xml:29-35`: `ui_taom_bannericons` declares
  `<SpriteSheetCount>2</SpriteSheetCount>`; its 369 sprite parts sit 225 on sheet 1 and 144 on sheet
  2. Declared counts: `ui_custom_fonts` 3, `ui_loading` 2, `ui_taom` 5, `ui_taom_bannericons` 2,
  `ui_taom_career_system` 1. `GUI/SpriteParts/Config.xml` marks all five `AlwaysLoad`. No file
  outside docs names a sheet 3 to 41.
- Docs: `docs/reference/banner-icon-generation.md:67` (Step 4a item 1) and `:81` (Step 4b item 3, the
  rule that `ui_taom_bannericons_NN` atlases keep **Do Not Compress** and **Do Not Generate Mips**);
  `docs/features/scene-scripts.md:124-145` (still tells Kit users to enable the four community mods);
  `docs/scene-scripts/map-maker-quickstart.md:7-13` (same); `docs/reference/bannerlord-engine-and-toolchain.md:65-76`
  (`:74-76` say an empty Dependencies editor bin "is expected, not a bug"; `module-taom.md:46` and
  `:48` cite this doc's lines 266 and 267, so the edit below keeps its line count);
  `docs/modding/open-questions.md:162`, `docs/modding/banners-and-heraldry.md:228` and `:253`,
  `docs/modding/module-taom.md:46-47` (the dead sheets and file counts); `docs/features/gui-sprite-system.md:171-178`
  (sprite gotchas table). Line endings: LF in the index, CRLF in the worktree.
- `tools/audit_map_scene_memory.py`: `iter_tpac_items(path, module)` yields
  `TpacItem(module, pack, kind, name, guid, meta, segments)`; `parse_texture_meta(meta)` returns
  `TextureInfo(source, flags, width, height, mips, faces, fmt, flags2)`, with `has_alpha` in `flags2`.
  Stdlib only plus `_gamedir`. `tools/tests/__init__.py` exists and tests put `tools/` on `sys.path`
  (`tools/tests/test_package_release.py:33-35`). pytest is not installed on the laptop, so new tool
  tests are plain `unittest`.

### Engine facts (v1.4.8, verified this session)

| Fact | Source |
|---|---|
| `SpriteCategory.Load` loads `"SpriteSheets\\" + Name + "\\" + Name + "_" + i` for `i = 1` to `SpriteSheetCount`, nothing else | taom-src `TaleWorlds.TwoDimension.SpriteCategory.cs:50-62` (loop `:56`) |
| `Module.LoadSubModules` tries `Path.Combine(module.FolderPath, "bin", Common.ConfigName)` + DLL name, then `ManagedDllFolder.Name + DLLName`; if neither exists it shows `"Cannot find: " + <first path>` | taom-src `TaleWorlds.MountAndBlade.Module.cs:1026`, `:1044-1046`, `:1082`; editor build `_editor_build\TaleWorlds.MountAndBlade.cs:103316-103393` (`:103336`, `:103372`) |
| `Common.ConfigName` is the working directory's name (`Win64_Shipping_wEditor` in the Kit); `ManagedDllFolder.Name` is `""` on PC, so the fallback is the Kit's own bin, which on the laptop holds no Harmony, MCM, UIExtenderEx, ButterLib or TAOM DLL | taom-src `TaleWorlds.Library.Common.cs:37`; editor build `TaleWorlds.Library.cs:5415-5437`; listing of the laptop's `bin\Win64_Shipping_wEditor` |
| A `ReflectionTypeLoadException` from `GetTypes()` returns `CriticalError`; `HandleSubmoduleLoadError` then shows `"<Module>.<SubModule>" submodule could not be loaded correctly due to a dependency conflict. This may cause the game to experience stability issues.` and throws | editor build `:102442-102484`, `:103396-103410`, `:103425`. The 1.5.x docs quote "missing or conflicting dependency"; that is NOT the v1.4.8 text |
| Tag check: `DedicatedServerType=none` passes outside a dedicated server, `IsNoRenderModeElement` passes on `"false"`, any other tag returns true | editor build `:103441-103531`; taom-src `Module.cs:1150` |

All seven TAOM.Dependencies submodules pass the tag check (`Dependencies/_Module/SubModule.xml`: four
carry `DedicatedServerType=none` and `IsNoRenderModeElement=false`; UIExtenderEx carries `DumpXML`,
the module loader `LoaderFilter` and `LoaderSubModuleOrder`, TAOM.Dependencies none). `RuntimeLog`
derives the module folder from the DLL location (`Dependencies/Foundation/RuntimeLog.cs:36-48`), so a
Kit session writes the same `Modules\TAOM.Dependencies\diag.log` and marker as the game. TAOM's own
`DependedModule`s are Native, SandBoxCore, Sandbox, CustomBattle and TAOM.Dependencies
(`Main/_Module/SubModule.xml:11-23`), so the Kit profile needs no map or Armory.

### Laptop install (read-only, 2026-11-04)

- `Modules\TAOM.Dependencies\bin`: `Gaming.Desktop.x64_Shipping_Client`, `Win64_Shipping_Client`,
  `Win64_Shipping_Server`; no `Win64_Shipping_wEditor`. `Modules\TAOM\bin` has one. The folders
  `Modules\Bannerlord.ButterLib`, `.Harmony`, `.MBOptionScreen`, `.UIExtenderEx` hold one file each.
  `Modules\TAOM.Dependencies` holds `diag.log` and no `session-launching.marker`.
- `Modules\TAOM` (SubModule.xml `v2.0.28`): `Assets\BannerIcons` 67 files, `Assets\GauntletUI` 52,
  `AssetSources\BannerIcons` 33, `AssetSources\GauntletUI` 52, `AssetPackages\pack0.tpac`
  188,902,921 B (2026-09-07), no `RuntimeDataCache`.
- That `pack0.tpac` holds 86 textures and 34 materials, exactly the textures and materials the repo
  descriptors name. Banner atlases: 33 x RGBA8, 1 mip (2,112.0 MiB decoded, 45,380,860 B stored).
  `ui_loading`: 2 x RGBA8, 13 mips (170.7 MiB, 56,464,697 B stored). Dead sheets 3 to 41: 39
  textures (2,410.7 MiB decoded, 41,529,118 B stored).
- TAOM_Map v2.0.27 (2026-09-07, cooked): `SceneObj\Main_map\scene.xscene` sets
  `vista_diffuse_name="16K_Vista_02"` and `vista_tileset=""`; its pack0 holds `16K_Vista_02` as DXT5
  16384x16384, 15 mips, 341.3 MiB. Vanilla SandBox: `vista_diffuse_name=""`,
  `vista_tileset="WorldMap"`. The 1.4.8 VMMap shows the vista as one private region of its 357,913,968
  bytes plus 43,664 (`docs/investigations/native-commit-audit-2026-08.md:530`, `:535-538`), the
  first region texture quality frees (`:641-644`).

### The 1.5.x result this plan reproduces

`ab338de2d` decoded: 24 DXT1 and 9 DXT5 (exactly the 9 `has_alpha` sheets), all 4096x4096 with 1 mip,
336 MiB. Every descriptor kept its texture item GUID (which its `_mtl.tpac` references: 33 of 33
checked) and its package GUID (bytes 8 to 23), and shrank by 11 bytes; no `_mtl.tpac` changed. So
Part C's oracle: format, size, mip count and `has_alpha` must equal `ab338de2d`'s; the item GUID must
equal `1b00d1acd`'s; the package GUID and file size are reported, not required.

The Abanissa sheet (icons 25000 to 25015, material `taom_banners_abanissa_alpha_01`) is drawn by the
banner keys of `clan_abanissa_1` to `_8`, `clan_shaghana_8` and the kingdom `abanissa`.

### Conventions that bind

- TDD: every step below writes its failing test first (RED), then the change (GREEN).
- ADR-007 adapters (services never touch sealed TaleWorlds types), ADR-002 thin entry points (under
  150 lines), ADR-003 no `#region`, ADR-004 no `[Obsolete]`, ADR-005 no `#if DEBUG` outside IoC
  registration. Nothing here adds a service, a GameModel, a Harmony patch or an MCM setting (so the
  json2 rename rule and `.claude/rules/harmony-patches.md` do not come into play).
- Single-owner files `Main/IoC.cs`, `Main/SubModule.cs` and `Main/TAOM.csproj` are not touched.
- Maintainer-only: `.claude/settings*.json`, `Directory.Build.props`, `docs/adrs/*`. Not touched.

## Commands

Each Bash call sets its own variables (they do not persist). Long runs need a 600000 ms timeout.

```
WT=C:/Users/mikew/source/repos/taom-port/121; AUX=C:/Users/mikew/source/repos/taom-port; MAIN=C:/Users/mikew/source/repos/TAOM
```

| Purpose | Command | Expected |
|---|---|---|
| Restore | `dotnet restore $WT/TAOM.sln` | exit 0 |
| Build | `dotnet build $WT/TAOM.sln -p:DisableModuleCopy=true -p:ModuleId= > $AUX/121-build.log 2>&1` | exit 0, `0 Error(s)` |
| Test | `dotnet test $WT/TAOM.Tests --no-build -p:DisableModuleCopy=true -p:ModuleId= > $AUX/121-test.log 2>&1` | `Failed:     0` |
| Filtered | the Test command plus `--filter FullyQualifiedName~<Class>` | as each step says |
| Tool tests | `cd $WT && python -m unittest discover -s tools/tests -t . > $AUX/121-pytests.txt 2>&1` | no new failure or error vs Step A0.4 |
| Texture gate | `cd $WT && python -m unittest discover -s tools/tests -t . -p "test_taom_texture_formats.py" -v` | as each step says |
| Docs | `cd $WT && python tools/lint_docs.py --summary` | no counter rises vs Step A0.4 |

Both `-p:` flags are mandatory on every build and test (`Main/TAOM.csproj:133-136`: a build without
`-p:ModuleId=` deploys into the game install). Never `./build.ps1` in Parts A, C or E. No ModuleData,
hook or `.claude/` file changes, so the Data and Hooks commands are not needed.

Message check, before every commit (pass the message file):

```
WT=C:/Users/mikew/source/repos/taom-port/121; AUX=C:/Users/mikew/source/repos/taom-port
cd $WT && python - "$AUX/121-msg.txt" <<'PY'
import re, sys
t = open(sys.argv[1], encoding="utf-8").read()
ver = re.search(r'<Version value="(v[0-9.]+)"', open("Main/_Module/SubModule.xml", encoding="utf-8-sig").read()).group(1)
subj = t.splitlines()[0]
ok = bool(re.match(r"^[a-z][a-z0-9]*(?:\([^)]+\))?!?: " + re.escape(ver) + r" - \S", subj)) and len(subj) <= 72
bad = any(c in t for c in "\u2013\u2014") or "Co-Authored-By" in t or "Generated with" in t or "CHANGELOG" in t
print("version", ver, "|", "subject ok" if ok else "SUBJECT BAD", "| dash/attribution/changelog:", bad, "| longest:", max(map(len, t.splitlines())))
PY
```

Expected: `version v2.0.28 | subject ok | dash/attribution/changelog: False | longest:` and a number of
at most 72. (D8: if `Main/_Module/SubModule.xml` reads a later version, the subjects below take that.)

## Scope

**In scope**
- Part A: `Dependencies/TAOM.Dependencies.csproj` (one appended target), `Dependencies/Foundation/IncompatibleModDetector.cs`,
  new `TAOM.Tests/Infrastructure/Dependencies/DependenciesEditorMirrorTests.cs`, new
  `TAOM.Tests/Infrastructure/IncompatibleModDetectorTests.cs`, new `tools/tests/test_taom_texture_formats.py`,
  deletion of `Main/_Module/Assets/GauntletUI/ui_taom_bannericons_{3..41}_tex.tpac`,
  `Main/_Module/AssetSources/GauntletUI/ui_taom_bannericons_{3..41}.png` and
  `Main/_Module/Assets/BannerIcons/taom_banners_dunland_alpha_01_tex.tpac.ptemp`, and the docs named
  in Steps A1 to A3.
- Part B (maintainer): the laptop game install's `Modules\TAOM` and `Modules\TAOM.Dependencies`, a
  backup folder outside the install and the repo, the Kit.
- Part C: the 33 `Main/_Module/Assets/BannerIcons/*_tex.tpac`, `Main/_Module/Assets/GauntletUI/ui_loading_{1,2}_tex.tpac`,
  `tools/tests/test_taom_texture_formats.py`, `docs/reference/banner-icon-generation.md`,
  `docs/features/gui-sprite-system.md`.
- Part E (conditional): new `tools/check_map_vista.py`, new `tools/tests/test_check_map_vista.py`;
  the maintainer's TAOM_Map package. Its `tools/README.md` row goes through the hand-off to WP142.
- Helper scripts in `C:\Users\mikew\source\repos\taom-port\` (outside every worktree), never committed.

**Single-owner files**: `Main/IoC.cs`, `Main/SubModule.cs`, `Main/TAOM.csproj` are not touched.
`Dependencies/TAOM.Dependencies.csproj` is shared with WP115, WP118 and WP122; this plan only appends
a target after `MirrorWin64ShippingClientToServer`.

**Out of scope** (do not touch)
- Everything else in `9985f2db3` and `b41de91e8`: the BannerIcons repack, realm banners, the sprite
  rebake (`ui_taom_1..5`, `ui_taom_bannericons_1/2`, fonts, `TAOMSpriteData.xml`, sprite parts),
  `banner_icons.xml`, clan, kingdom, culture and faction-map data, `tools/pack_banner_sheets.py` and
  its tests, the realm generator tools, `ClientDeploymentTests.cs` (D7), `ReflectionSiteBindingTests.cs`,
  `docs/reference/taleworlds-api-snapshot/reflection-sites.md`, lessons, RCAs, `tools/README.md`
  (the sync WPs own it: Part E's row goes through the hand-off to WP142), `.claude/`.
- `ui_taom_bannericons_1` and `_2`: they stay RGBA8 with one mip (Step 4b rule, D24).
- `CHANGELOG.md` (D1), `plans/README.md`, `docs/modding/modules-overview.md` (its bin counts are a
  dated install measurement; re-measuring it is WP144's).
- Resizing the banner atlases to vanilla's 2048 px, the module sounds, TAOM_Map prefabs (MEMORY-08),
  TAOM_Map authoring leftovers.
- Pushing, PRs, tags, releases, `gh issue create`.

## Git workflow

- Worktree `C:\Users\mikew\source\repos\taom-port\121`, branch `port148/121-texture-memory-diet`,
  created from local `bannerlord-1.4.5` (Step A0.1). Work only there.
- Commits on the WP branch: A1, A2, A3 (Part A); C1, C2 and, if Part B did the loading sheets, C3
  (Part C); E1 if Part E proceeds. One commit per step; never amend, never rewrite earlier commits.
- Every commit in this plan follows the same four moves: (1) stage with explicit
  `git -C $WT add <paths>` (or `git rm`) in one Bash call; (2) write the step's message to
  `C:\Users\mikew\source\repos\taom-port\121-msg.txt` with the Write tool; (3) run the message check
  (Commands); (4) `git -C $WT commit -F $AUX/121-msg.txt` in the NEXT sequential Bash call, never the
  same command, never a parallel call, never through PowerShell (D19). **Verify** after each commit:
  `git -C $WT log --oneline -1` shows the subject and `git -C $WT status --porcelain` is empty. If a
  hook refuses a commit,
  read its message: a CHANGELOG gate means WP102 is not merged (STOP); a subject-version gate means
  fix the subject.
- Commit message shape (D2 procedure): subject `<type>(<scope>): v2.0.28 - <description>` at most 72
  characters, keeping the 1.5.x type and scope; a body written for 1.4.x readers, wrapped at 72, with
  no em or en dash and no Co-Authored-By or "Generated with" line; then `Research:`, `Not-tested:` and
  one `Ported-from: <40-hex sha>` per included 1.5.x commit. A trailer value that wraps continues on
  lines indented by one space.
- No push, no PR, no tag. The integrator merges this branch.

## Part A: let the Kit load TAOM, drop the dead sheets (ordinary session)

### Step A0: set up and record baselines (no commit)

**A0.1** Confirm WP101 is merged, then create the worktree:

```
MAIN=C:/Users/mikew/source/repos/TAOM; WT=C:/Users/mikew/source/repos/taom-port/121
git -C $MAIN show bannerlord-1.4.5:plans/README.md | grep -c 'Baseline after WP101'
git -C $MAIN rev-parse --verify --quiet refs/heads/port148/121-texture-memory-diet; echo "branch exit=$?"
test -e $WT && echo "PATH EXISTS" || echo "path free"
```

Expected: a count of at least `1`, `branch exit=1`, `path free`. Then:

```
MAIN=C:/Users/mikew/source/repos/TAOM; WT=C:/Users/mikew/source/repos/taom-port/121
mkdir -p C:/Users/mikew/source/repos/taom-port && git -C $MAIN worktree add $WT -b port148/121-texture-memory-diet bannerlord-1.4.5 && git -C $WT log --oneline -1
```

**A0.2** Write the line-edit helper with the Write tool to
`C:\Users\mikew\source\repos\taom-port\docedit121.py` (outside git):

```python
"""Line-anchored text edits for plans/121. Keeps each file's line endings."""
import sys


def edit(path, prefix, new_lines, mode="replace", span=1):
    """Find the ONE line starting with `prefix`; replace it and the span-1 lines after it,
    or insert `new_lines` after it."""
    text = open(path, encoding="utf-8", newline="").read()
    eol = "\r\n" if "\r\n" in text else "\n"
    lines = text.split(eol)
    hits = [i for i, line in enumerate(lines) if line.startswith(prefix)]
    if len(hits) != 1:
        sys.exit(f"STOP: {path}: {len(hits)} lines start with {prefix!r}, expected exactly 1")
    i = hits[0]
    if mode == "replace":
        lines[i:i + span] = new_lines
    elif mode == "after":
        lines[i + 1:i + 1] = new_lines
    else:
        sys.exit(f"STOP: unknown mode {mode}")
    open(path, "w", encoding="utf-8", newline="").write(eol.join(lines))
    print(f"ok {mode} {path}:{i + 1}")
```

**A0.3** Drift and anchor checks (read-only):

```
WT=C:/Users/mikew/source/repos/taom-port/121
cd $WT && git diff --stat 1b00d1acd..HEAD -- Dependencies/TAOM.Dependencies.csproj Dependencies/Foundation/IncompatibleModDetector.cs Main/_Module/Assets Main/_Module/AssetSources/GauntletUI Main/_Module/GUI/TAOMSpriteData.xml docs/features/scene-scripts.md docs/scene-scripts/map-maker-quickstart.md docs/reference/bannerlord-engine-and-toolchain.md docs/reference/banner-icon-generation.md docs/features/gui-sprite-system.md docs/modding/open-questions.md docs/modding/banners-and-heraldry.md docs/modding/module-taom.md
cd $WT && python - <<'PY'
import os
c = open("Dependencies/TAOM.Dependencies.csproj", encoding="utf-8").read()
d = open("Dependencies/Foundation/IncompatibleModDetector.cs", encoding="utf-8").read()
print("server target:", c.count('<Target Name="MirrorWin64ShippingClientToServer"'), "| editor target:", c.count('Name="MirrorWin64ShippingClientToEditor"'))
print("IsEditorBin:", d.count("IsEditorBin"), "| earlyPhaseRan:", d.count("private static int _earlyPhaseRan;"))
for p in ("Main/_Module/Assets/GauntletUI", "Main/_Module/AssetSources/GauntletUI", "Main/_Module/Assets/BannerIcons"):
    print(p, len(os.listdir(p)))
print("tests exist:", [os.path.exists(p) for p in ("TAOM.Tests/Infrastructure/IncompatibleModDetectorTests.cs", "TAOM.Tests/Infrastructure/Dependencies/DependenciesEditorMirrorTests.cs", "tools/tests/test_taom_texture_formats.py")])
PY
```

Expected: `server target: 1 | editor target: 0`, `IsEditorBin: 0 | earlyPhaseRan: 1`, the three
folders at `52`, `52`, `67`, and `tests exist: [False, False, False]`. The `git diff --stat` may list
the csproj and docs changed by earlier WPs (see the drift note at the top); anything under
`Main/_Module/Assets`, `AssetSources/GauntletUI`, `TAOMSpriteData.xml` or the detector is a STOP.

**A0.4** Baselines (600000 ms timeout each):

```
WT=C:/Users/mikew/source/repos/taom-port/121; AUX=C:/Users/mikew/source/repos/taom-port
dotnet restore $WT/TAOM.sln > $AUX/121-restore.log 2>&1; echo "restore exit=$?"
dotnet build $WT/TAOM.sln -p:DisableModuleCopy=true -p:ModuleId= > $AUX/121-build.log 2>&1; echo "build exit=$?"; grep -c " error " $AUX/121-build.log
dotnet test $WT/TAOM.Tests --no-build -p:DisableModuleCopy=true -p:ModuleId= > $AUX/121-test.log 2>&1; echo "test exit=$?"; grep -E "Passed!|Failed!" $AUX/121-test.log
cd $WT && python -m unittest discover -s tools/tests -t . > $AUX/121-pytests.txt 2>&1; tail -3 $AUX/121-pytests.txt
cd $WT && python tools/lint_docs.py --summary | tail -15
```

Record the test line (`Passed: P`, `Skipped: S`, `Failed: 0`), the tool-test `Ran N tests` and result
line, and the lint summary. Expected: exit 0 everywhere except the tool tests, whose existing
failures and errors (for example modules that import the absent pytest) are the baseline. `Failed`
above 0 in the C# suite is a STOP.

### Step A1 (TDD): mirror TAOM.Dependencies into the Kit's bin (`9985f2db3`, comment from `b41de91e8`)

**A1.1 RED.** Create `TAOM.Tests/Infrastructure/Dependencies/DependenciesEditorMirrorTests.cs`:

```csharp
using System;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace TAOM.Tests.Infrastructure.Dependencies;

/// <summary>
/// Pins the TAOM.Dependencies editor mirror (1.5.x 9985f2db3, #1047). The v1.4.8 Modding Kit looks for each
/// submodule's DLL in the module's own <c>bin\Win64_Shipping_wEditor</c> folder, then in its own bin
/// (<c>Module.LoadSubModules</c>), which holds no Harmony, MCMv5 or UIExtenderEx. With the folder empty
/// TAOM.dll cannot load in the Kit, so no Kit job (plans/121's banner atlas re-import among them) can open TAOM.
/// </summary>
[TestClass]
public class DependenciesEditorMirrorTests
{
    private static string RepoRoot => Path.GetFullPath(
        Path.Combine(AppDomain.CurrentDomain.BaseDirectory, @"..\..\..\.."));

    private static XElement Target(string name) =>
        XDocument.Load(Path.Combine(RepoRoot, @"Dependencies\TAOM.Dependencies.csproj")).Descendants()
            .Single(t => t.Name.LocalName == "Target" && (string?)t.Attribute("Name") == name);

    [TestMethod]
    public void EditorMirror_CopiesTheAssembledClientBinIntoTheEditorBin()
    {
        var target = Target("MirrorWin64ShippingClientToEditor");

        Assert.AreEqual("PostBuildCopyToModules", (string?)target.Attribute("AfterTargets"),
            "The client bin is only populated once Bannerlord.BuildResources has copied into it.");
        var copy = target.Descendants("Copy").Single();
        StringAssert.Contains((string?)copy.Attribute("DestinationFolder") ?? "", "Win64_Shipping_wEditor",
            "Without it the Modding Kit finds no Harmony, MCMv5 or UIExtenderEx and TAOM fails to load there.");
        var source = target.Descendants("ItemGroup").Elements()
            .Single(i => $"@({i.Name.LocalName})" == (string?)copy.Attribute("SourceFiles"));
        StringAssert.Contains((string?)source.Attribute("Include") ?? "", @"bin\Win64_Shipping_Client\*.*");
    }

    [TestMethod]
    public void EditorMirror_IsSkippedByTheAgentBuild()
    {
        // Agents build with -p:DisableModuleCopy=true -p:ModuleId= and must never write into the game install.
        var condition = (string?)Target("MirrorWin64ShippingClientToEditor").Attribute("Condition") ?? "";

        StringAssert.Contains(condition, "$(DisableModuleCopy) != 'true'");
        StringAssert.Contains(condition, "$(ModuleId) != ''");
    }
}
```

**Verify**: Build exit 0; Filtered `DependenciesEditorMirrorTests` shows `Failed: 2` (`Single()` finds
no target).

**A1.2 GREEN.** Append the target by deriving it from the server mirror; the script refuses unless the
result equals `b41de91e8`'s target line for line:

```
WT=C:/Users/mikew/source/repos/taom-port/121
cd $WT && python - <<'PY'
import subprocess, sys
p = "Dependencies/TAOM.Dependencies.csproj"
text = open(p, encoding="utf-8", newline="").read()
eol = "\r\n" if "\r\n" in text else "\n"
lines = text.split(eol)
if any('Name="MirrorWin64ShippingClientToEditor"' in l for l in lines):
    sys.exit("STOP: the editor mirror target already exists")
start = [i for i, l in enumerate(lines) if '<Target Name="MirrorWin64ShippingClientToServer"' in l]
if len(start) != 1:
    sys.exit(f"STOP: {len(start)} server mirror targets")
end = next(i for i in range(start[0], len(lines)) if lines[i].strip() == "</Target>")
server = lines[start[0]:end + 1]
editor = [l.replace("_TaomDepsServerMirrorFiles", "_TaomDepsEditorMirrorFiles")
           .replace("MirrorWin64ShippingClientToServer", "MirrorWin64ShippingClientToEditor")
           .replace("Win64_Shipping_Server", "Win64_Shipping_wEditor") for l in server]
ref = subprocess.run(["git", "show", "b41de91e8:Dependencies/TAOM.Dependencies.csproj"],
                     capture_output=True, text=True, check=True).stdout.splitlines()
r0 = next(i for i, l in enumerate(ref) if 'Name="MirrorWin64ShippingClientToEditor"' in l)
r1 = next(i for i in range(r0, len(ref)) if ref[i].strip() == "</Target>")
if [l.strip() for l in editor] != [l.strip() for l in ref[r0:r1 + 1]]:
    sys.exit("STOP: the derived target differs from the 1.5.x one")
ind = server[0][: len(server[0]) - len(server[0].lstrip())]
comment = [
    ind + "<!-- The same mirror for the Modding Kit (Win64_Shipping_wEditor). The Kit looks for each submodule's",
    ind + "     DLL in that folder, then in its own bin folder, which holds none of these (v1.4.8",
    ind + "     Module.LoadSubModules). With it empty the Kit shows a \"Cannot find\" box per submodule, TAOM.dll's",
    ind + "     references to Harmony, MCMv5 and UIExtenderEx do not resolve, and the Kit stops its launch. The",
    ind + "     standalone Bannerlord.* community modules used to supply them; this module replaced them, and the",
    ind + "     stub manifests DeployTAOMDependenciesStubs deploys carry no DLL. Pinned by DependenciesEditorMirrorTests. -->",
]
lines[end + 1:end + 1] = [""] + comment + editor
open(p, "w", encoding="utf-8", newline="").write(eol.join(lines))
print("inserted", 1 + len(comment) + len(editor), "lines after line", end + 1)
PY
```

**Verify**: prints `inserted 17 lines after line N`; Build exit 0; Filtered
`DependenciesEditorMirrorTests` shows `Passed: 2`, `Failed: 0`.

**A1.3 Docs.** Rewrite the Kit facts for 1.4.x (the toolchain edit is line-count neutral on purpose):

```
WT=C:/Users/mikew/source/repos/taom-port/121
cd $WT && python - <<'PY'
import datetime, sys
sys.path.insert(0, "C:/Users/mikew/source/repos/taom-port")
from docedit121 import edit
today = datetime.date.today().isoformat()
T = "docs/reference/bannerlord-engine-and-toolchain.md"
edit(T, "| `MirrorWin64ShippingClientToServer` | `Dependencies/TAOM.Dependencies.csproj`",
     ["| `MirrorWin64ShippingClientToEditor` | `Dependencies/TAOM.Dependencies.csproj` | `Modules/TAOM.Dependencies/bin/Win64_Shipping_wEditor/` | same set as Client (since " + today + ", plans/121) |"], mode="after")
edit(T, "Both server targets run",
     [r'''All four targets run `AfterTargets="PostBuildCopyToModules"`, gated on `$(DisableModuleCopy) != 'true'`'''])
edit(T, "companions that only exist after `PostBuildCopyToModules`. There is no editor mirror in",
     [r'''companions that only exist after `PostBuildCopyToModules`. The Dependencies editor mirror is required: the Kit looks for each submodule's DLL in the module's `Win64_Shipping_wEditor` folder, then in its own bin folder, which holds none of these (v1.4.8 `Module.LoadSubModules`, `Module.cs:1044-1046`).'''])
edit(T, "`TAOM.Dependencies.csproj`, so `Modules/TAOM.Dependencies/bin/Win64_Shipping_wEditor/` being empty is",
     [r'''With that folder empty the Kit shows a "Cannot find" box per submodule, `TAOM.dll` then cannot resolve `0Harmony`, `MCMv5` and `Bannerlord.UIExtenderEx`, and the Kit stops its launch. The standalone `Bannerlord.*` community modules once supplied those assemblies; their ids survive only as the DLL-less stub manifests that `DeployTAOMDependenciesStubs` deploys.'''], span=2)
S = "docs/features/scene-scripts.md"
edit(S, "TAOM.dll loads in the standalone Bannerlord modding kit",
     [r'''TAOM.dll loads in the standalone Bannerlord modding kit (`Win64_Shipping_wEditor`) when the launcher's editor profile has `TAOM.Dependencies` and `TAOM` active. `TAOM.Dependencies` carries Harmony, UIExtenderEx, ButterLib and MCMv5. All seven of its submodules pass the Kit's tag check: four carry `DedicatedServerType=none` and `IsNoRenderModeElement=false`, which never block a process that is not a dedicated server, and the other three carry no platform, server or no-render tag (v1.4.8 `Module.CheckIfSubmoduleCanBeLoadable`). So the Kit loads all seven as the game does, and their DLLs satisfy TAOM.dll's references to those types.'''])
edit(S, "- `Bannerlord.Harmony`",
     [r'''The Kit looks for each submodule's DLL in that module's `bin/Win64_Shipping_wEditor/` folder, then in its own bin folder, which holds none of these (v1.4.8 `Module.LoadSubModules`, `Module.cs:1044-1046`). The `MirrorWin64ShippingClientToEditor` target in `Dependencies/TAOM.Dependencies.csproj` fills the module folder on every deploying build (ported to this line on ''' + today + r''', plans/121; pinned by `DependenciesEditorMirrorTests`). Before that the folder stayed empty: the standalone `Bannerlord.Harmony`, `Bannerlord.UIExtenderEx`, `Bannerlord.MBOptionScreen` and `Bannerlord.ButterLib` modules once supplied the assemblies, and their ids now survive only as the DLL-less stub manifests that `DeployTAOMDependenciesStubs` deploys.'''])
for b in ("- `Bannerlord.UIExtenderEx`", "- `Bannerlord.MBOptionScreen` (MCMv5)", "- `Bannerlord.ButterLib`"):
    edit(S, b, [])
edit(S, "These mods' SubModule.xml files carry", [], span=2)
edit(S, "**If you launch with only TAOM active**",
     [r'''**If the Dependencies editor folder is empty**, the Kit first shows a "Cannot find: ...\TAOM.Dependencies\bin\Win64_Shipping_wEditor\<dll>" box for each of its seven submodules. It then loads `Modules/TAOM/bin/Win64_Shipping_wEditor/TAOM.dll`, whose `Assembly.GetTypes()` throws `ReflectionTypeLoadException` (`0Harmony`, `MCMv5`, `Bannerlord.UIExtenderEx`), reports `"TAOM.TAOM" submodule could not be loaded correctly due to a dependency conflict` and stops the launch (`CollectModuleAssemblyTypes` returns `CriticalError`, then `HandleSubmoduleLoadError` throws). Fix: a deploying build (`./build.ps1`) with the Kit closed.'''])
edit(S, "1. Open your launcher, switch to the editor profile.",
     ["1. Open your launcher, switch to the editor profile. Verify enabled: TAOM.Dependencies, TAOM (plus TAOM_Map if you want the TAOM main map open by default)."])
Q = "docs/scene-scripts/map-maker-quickstart.md"
edit(Q, "Open your launcher and switch to the editor profile.",
     ["Open your launcher and switch to the editor profile. **Both of these mods must be enabled**, or the editor will stop on TAOM load:"])
for b in ("- Bannerlord.Harmony", "- Bannerlord.UIExtenderEx", "- Bannerlord.MBOptionScreen"):
    edit(Q, b, [])
edit(Q, "- Bannerlord.ButterLib", ["- TAOM.Dependencies"])
PY
```

**Verify**: 17 `ok` lines (4 toolchain, 8 scene-scripts, 5 quickstart) and no `STOP`;
`cd $WT && git diff --numstat -- docs/reference/bannerlord-engine-and-toolchain.md` shows equal
added and deleted counts (4 and 4) and `grep -n "^### 6.1" docs/reference/bannerlord-engine-and-toolchain.md`
still prints line `263`; `git diff -- docs/features/scene-scripts.md` shows the section as heading,
blank, three paragraphs separated by blank lines, then the unchanged
`` `Modules/TAOM/bin/Win64_Shipping_wEditor/` is automatically mirrored`` paragraph.

**A1.4 Commit.** Stage, then commit in the next call:

```
WT=C:/Users/mikew/source/repos/taom-port/121
git -C $WT add Dependencies/TAOM.Dependencies.csproj TAOM.Tests/Infrastructure/Dependencies/DependenciesEditorMirrorTests.cs docs/reference/bannerlord-engine-and-toolchain.md docs/features/scene-scripts.md docs/scene-scripts/map-maker-quickstart.md && git -C $WT status --short
```

Message (`$AUX/121-msg.txt`):

```
feat(banners): v2.0.28 - mirror TAOM.Dependencies into the Kit's bin

A deploying build now copies TAOM.Dependencies' assembled client bin
into its Win64_Shipping_wEditor folder, as Main/TAOM.csproj already
does for TAOM. The v1.4.8 Modding Kit looks for each submodule's DLL
in that folder and then in its own bin, which holds no Harmony, MCMv5
or UIExtenderEx, so until now the Kit could not load TAOM at all: it
showed a "Cannot find" box per submodule and stopped. The community
modules that once supplied those DLLs are DLL-less stubs on this
line. This unblocks every Kit job on 1.4.x, starting with the banner
atlas re-import. Agent builds skip the target, and players see no
change.

Research: v1.4.8 Module.LoadSubModules and CheckIfSubmoduleCanBeLoadable
 (installed DLL Module.cs 1026-1082, editor build)
Not-tested: the target runs only in a deploying build (plans/121 Part B)
Ported-from: 9985f2db3a0396eb8c209e8b1229f5368a658f21
Ported-from: b41de91e8e9b71b1c2003d367f7217b55713f8e2
```

Run the message check, then `git -C $WT commit -F $AUX/121-msg.txt` in a separate call.
**Verify**: `git -C $WT log --oneline -1` shows the subject; `git -C $WT status --porcelain` is empty.

### Step A2 (TDD): a Kit session leaves no crash-loop marker (`b41de91e8`)

**A2.1 RED.** Add the test file exactly as 1.5.x has it:

```
WT=C:/Users/mikew/source/repos/taom-port/121
cd $WT && git diff b41de91e8^ b41de91e8 -- TAOM.Tests/Infrastructure/IncompatibleModDetectorTests.cs | git apply -v
```

**Verify**: `Applied patch ... cleanly`; the Build command exits non-zero and `$AUX/121-build.log`
contains `CS0117` naming `IsEditorBin`.

**A2.2 GREEN.** Apply the detector hunks (the new `internal static bool IsEditorBin(string)` after
`_earlyPhaseRan`, and the early return as the first statement inside `RunEarlyPhase`'s `try`):

```
WT=C:/Users/mikew/source/repos/taom-port/121
cd $WT && git diff b41de91e8^ b41de91e8 -- Dependencies/Foundation/IncompatibleModDetector.cs | git apply -v
```

**Verify**: `Applied patch ... cleanly`; `grep -c IsEditorBin Dependencies/Foundation/IncompatibleModDetector.cs`
prints `2`; Build exit 0; Filtered `IncompatibleModDetectorTests` shows `Failed: 0` and 6 passing
results (one per DataRow; a runner that folds rows shows 1).

**A2.3 Docs.**

```
WT=C:/Users/mikew/source/repos/taom-port/121
cd $WT && python - <<'PY'
import sys
sys.path.insert(0, "C:/Users/mikew/source/repos/taom-port")
from docedit121 import edit
edit("docs/features/scene-scripts.md", "**If the Dependencies editor folder is empty**",
     ["", r'''A Kit session also runs TAOM.Dependencies' crash-loop detector, which skips its launch marker when it loads from the editor bin (`IncompatibleModDetector.IsEditorBin`), because the Kit never reaches the main-menu hook that deletes the marker. Before that check a Kit session left `session-launching.marker` behind, and the next game logged a crash loop that never happened.'''], mode="after")
PY
```

**A2.4 Commit.** `git -C $WT add Dependencies/Foundation/IncompatibleModDetector.cs TAOM.Tests/Infrastructure/IncompatibleModDetectorTests.cs docs/features/scene-scripts.md`,
then the message:

```
fix(banners): v2.0.28 - a Kit session leaves no crash-loop marker

TAOM.Dependencies writes session-launching.marker when it starts and
deletes it when the game reaches the main menu; a marker still there
at the next start reads as a crash loop. Now that the Modding Kit can
load TAOM.Dependencies, a Kit session would write the marker and never
reach the main menu, so the next game would report a crash loop that
never happened. The detector now skips its marker when its DLL loads
from the Kit's Win64_Shipping_wEditor bin. Game and server launches
are unchanged.

Research: v1.4.8 editor build Module.LoadSubModules loads the DLL from
 <module>\bin\<working directory name>; RuntimeLog.ModuleDir
Not-tested: a real Kit session (plans/121 Part B checks diag.log)
Ported-from: b41de91e8e9b71b1c2003d367f7217b55713f8e2
```

### Step A3 (TDD): drop the 39 dead sprite sheets and gate them (`9985f2db3`)

**A3.1 RED.** Create `tools/tests/test_taom_texture_formats.py`:

```python
#!/usr/bin/env python3
"""Pins the compiled form of TAOM's own textures (plans/121; ports of #913 and #1047).

Reads the repository's Main/_Module/Assets descriptors and GUI/TAOMSpriteData.xml with the decoders
in tools/audit_map_scene_memory.py: never a pixel, never the game install. The repo never tracks
pack0.tpac, so a published pack is checked by pointing TAOM_PACK0 at it; those tests skip otherwise.

Run:   python -m unittest discover -s tools/tests -t . -p "test_taom_texture_formats.py" -v
Pack:  TAOM_PACK0="<publish folder>/TAOM/AssetPackages/pack0.tpac" python -m unittest discover -s tools/tests -t . -p "test_taom_texture_formats.py" -v
"""
import glob
import os
import re
import sys
import unittest
import xml.etree.ElementTree as ET

TOOLS = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
sys.path.insert(0, TOOLS)

import audit_map_scene_memory as amsm  # noqa: E402

MODULE = os.path.join(os.path.dirname(TOOLS), "Main", "_Module")
PACK0 = os.environ.get("TAOM_PACK0", "").strip()
SHEET = re.compile(r"(.+)_(\d+)")

# Expected (format, mips) per UI sheet. docs/reference/banner-icon-generation.md Step 4b keeps the
# banner icon sprite sheets uncompressed with one mip, for a clean white sigil alpha.
UI_FORMATS = {
    "ui_taom_bannericons_1": ("R8G8B8A8_UNORM", 1),
    "ui_taom_bannericons_2": ("R8G8B8A8_UNORM", 1),
}


def decode(paths, kind="texture"):
    """{item name: TextureInfo (textures) or None (other kinds)} for every item of `kind`."""
    found = {}
    for path in paths:
        for item in amsm.iter_tpac_items(path, "TAOM"):
            if item.kind == kind:
                found[item.name] = amsm.parse_texture_meta(item.meta) if kind == "texture" else None
    return found


def textures_in(folder):
    return decode(sorted(glob.glob(os.path.join(MODULE, "Assets", folder, "*_tex.tpac"))))


def repo_items(pattern, kind):
    return decode(sorted(glob.glob(os.path.join(MODULE, "Assets", "**", pattern), recursive=True)), kind)


def declared_sheets():
    root = ET.parse(os.path.join(MODULE, "GUI", "TAOMSpriteData.xml")).getroot()
    return {c.findtext("Name"): int(c.findtext("SpriteSheetCount")) for c in root.iter("SpriteCategory")}


def is_declared(stem, counts):
    m = SHEET.fullmatch(stem)
    return bool(m) and 1 <= int(m.group(2)) <= counts.get(m.group(1), 0)


def ui_problems(tex):
    out = []
    for name, (fmt, mips) in sorted(UI_FORMATS.items()):
        t = tex.get(name)
        if t is None:
            out.append(f"{name}: missing")
        elif (t.fmt, t.mips) != (fmt, mips):
            out.append(f"{name}: {t.fmt} with {t.mips} mips, want {fmt} with {mips}")
    return out


class SpriteSheetFileTests(unittest.TestCase):
    """v1.4.8 SpriteCategory.Load asks for sheets 1 to SpriteSheetCount and no others, so any other
    sheet file is dead weight in every pack the Kit publishes (#1047 found 39)."""

    maxDiff = None

    def test_every_gauntlet_sheet_file_is_a_declared_sheet(self):
        counts = declared_sheets()
        stray = []
        for folder, suffix in (("Assets", "_tex.tpac"), ("AssetSources", ".png")):
            root = os.path.join(MODULE, folder, "GauntletUI")
            for name in sorted(os.listdir(root)):
                if not (name.endswith(suffix) and is_declared(name[: -len(suffix)], counts)):
                    stray.append(f"{folder}/GauntletUI/{name}")
        self.assertEqual([], stray, "files TAOMSpriteData.xml does not declare as a sheet")


class UiSheetFormatTests(unittest.TestCase):
    maxDiff = None

    def test_ui_sheets_keep_their_pinned_format(self):
        self.assertEqual([], ui_problems(textures_in("GauntletUI")))


@unittest.skipUnless(PACK0, "set TAOM_PACK0 to a published pack0.tpac to check it")
class PublishedPackTests(unittest.TestCase):
    """What players load. A publish from an install whose descriptors and RuntimeDataCache disagree,
    or that still holds a removed sheet, shows up here and nowhere else."""

    maxDiff = None

    @classmethod
    def setUpClass(cls):
        cls.textures = decode([PACK0])
        cls.materials = decode([PACK0], kind="material")

    def test_pack_holds_exactly_the_repo_textures_and_materials(self):
        self.assertEqual(sorted(repo_items("*_tex.tpac", "texture")), sorted(self.textures))
        self.assertEqual(sorted(repo_items("*_mtl.tpac", "material")), sorted(self.materials))

    def test_pack_ui_sheets_keep_their_pinned_format(self):
        self.assertEqual([], ui_problems(self.textures))

    def test_pack_has_no_undeclared_sheet(self):
        counts = declared_sheets()
        self.assertEqual([], sorted(n for n in self.textures if n.startswith("ui_") and not is_declared(n, counts)))


if __name__ == "__main__":
    unittest.main()
```

**Verify**: Texture gate prints `Ran 5 tests` and `FAILED (failures=1, skipped=3)`; the failure lists
78 paths (39 under `Assets/GauntletUI`, 39 under `AssetSources/GauntletUI`).

**A3.2 GREEN.** Remove the files:

```
WT=C:/Users/mikew/source/repos/taom-port/121
cd $WT && for n in $(seq 3 41); do git rm -q "Main/_Module/Assets/GauntletUI/ui_taom_bannericons_${n}_tex.tpac" "Main/_Module/AssetSources/GauntletUI/ui_taom_bannericons_${n}.png" || break; done; git rm -q "Main/_Module/Assets/BannerIcons/taom_banners_dunland_alpha_01_tex.tpac.ptemp"; git status --short | grep -c "^D "
```

**Verify**: prints `79`; the three folders hold 13, 13 and 66 files; Texture gate prints `Ran 5 tests`
and `OK (skipped=3)`.

**A3.3 Docs.** The script measures the new counts and refuses if they are not the planned ones:

```
WT=C:/Users/mikew/source/repos/taom-port/121
cd $WT && python - <<'PY'
import datetime, math, os, sys
sys.path.insert(0, "C:/Users/mikew/source/repos/taom-port")
from docedit121 import edit
today = datetime.date.today().isoformat()
n_assets = sum(len(fs) for _, _, fs in os.walk("Main/_Module/Assets"))
mib = math.ceil(sum(os.path.getsize(os.path.join(d, f)) for d, _, fs in os.walk("Main/_Module/AssetSources") for f in fs) / 2**20)
n_sources = sum(len(fs) for _, _, fs in os.walk("Main/_Module/AssetSources"))
if (n_assets, n_sources, mib) != (81, 47, 483):
    sys.exit(f"STOP: measured {n_assets} assets, {n_sources} sources, {mib} MiB; planned 81, 47, 483")
edit("docs/modding/open-questions.md", "- Whether GUI/SpriteParts/ui_taom_bannericons/22004.png",
     ["- Whether GUI/SpriteParts/ui_taom_bannericons/22004.png (a sprite with no matching <Icon id>) is safe to delete, or is pending an icon that was never wired. (The 39 unreferenced ui_taom_bannericons_3 to _41 sheets were removed on " + today + ", plans/121.)"])
H = "docs/modding/banners-and-heraldry.md"
edit(H, "- **The sprite side has one orphan and a pile of dead sheets.**",
     [r'''- **The sprite side has one orphan.** `GUI/SpriteParts/ui_taom_bannericons/` holds 369 PNGs for 368 icon ids: `22004.png` has no `<Icon id="22004">`, so nothing can ever reference it. The 39 sheets that earlier bakes left in `AssetSources/GauntletUI/` and `Assets/GauntletUI/` (`ui_taom_bannericons_3` to `_41`) were removed on ''' + today + r''' (plans/121), and `tools/tests/test_taom_texture_formats.py` now fails on any sheet file `TAOMSpriteData.xml` does not declare. The manifest is the authority, not the file list.'''])
edit(H, "- **369** PNGs in `GUI/SpriteParts/ui_taom_bannericons/`",
     [r'''- **369** PNGs in `GUI/SpriteParts/ui_taom_bannericons/` against **368** icon ids, leaving `22004` orphaned; **369** matching `<SpritePart>` rows in `TAOMSpriteData.xml` on **2** declared sheets, with **2** `ui_taom_bannericons_*.png` files on disk (41 before plans/121). <!-- measured: python over the sprite folder, TAOMSpriteData.xml and banner_icons.xml 2026-09-05; sheet files re-counted ''' + today + " -->"])
M = "docs/modding/module-taom.md"
edit(M, "| `Assets/` | 121 compiled per-asset",
     [r'''| `Assets/` | 81 compiled per-asset `.tpac` descriptors under `GauntletUI/`, `BannerIcons/`, `main_map_textures/` (121 files before plans/121 removed 39 dead sprite-sheet descriptors and a stray `taom_banners_dunland_alpha_01_tex.tpac.ptemp`) <!-- measured: find Main/_Module/Assets -type f \| wc -l ''' + today + r''' --> | The editor and the game on a dev install (`docs/reference/bannerlord-engine-and-toolchain.md:266`) |'''])
edit(M, "| `AssetSources/` | 86 raw art files",
     [r'''| `AssetSources/` | 47 raw art files (PSD and PNG), 483 MB (86 before plans/121 removed 39 dead sprite-sheet PNGs) | Nobody at runtime. `tools/package_release.py` (`classify`) excludes the whole folder from a release; the dev build still deploys it (`Main/TAOM.csproj:13`) |'''])
PY
```

**Verify**: five `ok` lines, no `STOP`.

**A3.4 Commit.** The deletions are staged by `git rm`. Add the rest:
`git -C $WT add tools/tests/test_taom_texture_formats.py docs/modding/open-questions.md docs/modding/banners-and-heraldry.md docs/modding/module-taom.md`,
then the message:

```
feat(banners): v2.0.28 - drop 39 dead banner icon sprite sheets

The ui_taom_bannericons sprite category declares two sheets and the
engine loads sheets 1 to SpriteSheetCount and nothing else, yet the
module still carried sheets 3 to 41 from an old 41-sheet bake: 39
texture descriptors, 39 source PNGs (70,479,496 bytes) and one stray
Kit temp file. Nothing loads them, but every published pack0.tpac
carries them: 41,529,118 of its 188,902,921 bytes today. They are
gone, and a new tool test fails on any sheet file the sprite data
does not declare. Players get the smaller pack with the next Kit
publish.

Research: v1.4.8 TaleWorlds.TwoDimension.SpriteCategory.Load; table of
 contents of the laptop's installed pack0.tpac
Ported-from: 9985f2db3a0396eb8c209e8b1229f5368a658f21
```

### Step A4: Part A gate and hand-off (no commit unless review finds something)

1. Run Build, Test, Tool tests and Docs. Expected: Build exit 0; Test `Failed: 0` with Passed equal to
   the A0.4 value plus 8 (2 mirror tests and 6 DataRows; plus 3 if the runner folds rows) and
   Skipped unchanged; Tool tests `Ran N+5`, skipped up by 3, failures and errors unchanged; the lint
   summary shows no counter above A0.4's.
2. `cd $WT && git diff --name-only $(git merge-base HEAD bannerlord-1.4.5)..HEAD | wc -l` prints `90`
   (A1: 5 files; A2: 2 more; A3: 79 deletions plus 4). A different number: list the files and STOP
   only if one is outside the Scope section.
3. Review gate: run `/deep-review` on `git diff $(git merge-base HEAD bannerlord-1.4.5)..HEAD` if your
   session can invoke skills; ask it to probe the spots in "Maintenance notes". Act only on findings
   that change behaviour or break this plan, each as one extra commit
   `fix(banners): v2.0.28 - review follow-ups` (test first). Otherwise list the review as owed.
   Never run `/review-codex` or `/codex-verify` (paid; maintainer only).
4. Write the Part A hand-off (see "Hand-off report") and stop. Part B starts from this worktree.

### Part A done criteria

- [ ] `git -C $WT log --oneline bannerlord-1.4.5..HEAD` lists exactly the three A subjects (plus any
      review follow-up)
- [ ] `grep -c 'Name="MirrorWin64ShippingClientToEditor"' $WT/Dependencies/TAOM.Dependencies.csproj` prints `1`
- [ ] `grep -c IsEditorBin $WT/Dependencies/Foundation/IncompatibleModDetector.cs` prints `2`
- [ ] `ls $WT/Main/_Module/Assets/GauntletUI | wc -l` and `ls $WT/Main/_Module/AssetSources/GauntletUI | wc -l` print `13`; `ls $WT/Main/_Module/Assets/BannerIcons | wc -l` prints `66`
- [ ] Build exit 0; Test `Failed: 0`; Texture gate `OK (skipped=3)`; no new tool-test failure
- [ ] No new docs line carries an em or en dash: the dash check under "Done criteria" prints `0`
- [ ] `git -C $WT status --porcelain` is empty

## Part B: re-import the textures in the v1.4.8 Kit (maintainer, laptop)

Close the game and the Kit before each step that writes files, and keep them closed while the other
runs. **From Step B6 until Part C's commits are in the WP121 branch, do not run any deploying build
(`./build.ps1`, or a build without `-p:ModuleId=`) from any tree, and from then until WP121 is merged
into `bannerlord-1.4.5`, deploy only from this worktree.** A deploying build copies the repo's
descriptors over the Kit-saved ones but keeps the new `RuntimeDataCache`, which renders the sigils
blank and crashes the Kit on its next start.

**B1. Back up** (PowerShell; copies only):

```powershell
$taom = Join-Path $env:BANNERLORD_GAME_DIR 'Modules\TAOM'
$bk   = 'C:\taom-kit-backup-' + (Get-Date -Format 'yyyy-MM-dd') + '\TAOM'
if (Test-Path -LiteralPath $bk) { throw "backup folder exists: pick another name" }
New-Item -ItemType Directory -Force (Join-Path $bk 'AssetPackages') | Out-Null
Copy-Item -LiteralPath (Join-Path $taom 'Assets') -Destination (Join-Path $bk 'Assets') -Recurse
Copy-Item -LiteralPath (Join-Path $taom 'AssetSources') -Destination (Join-Path $bk 'AssetSources') -Recurse
Copy-Item -LiteralPath (Join-Path $taom 'AssetPackages\pack0.tpac') -Destination (Join-Path $bk 'AssetPackages\pack0.tpac')
if (Test-Path -LiteralPath (Join-Path $taom 'RuntimeDataCache')) { Copy-Item -LiteralPath (Join-Path $taom 'RuntimeDataCache') -Destination (Join-Path $bk 'RuntimeDataCache') -Recurse }
Get-ChildItem -LiteralPath $bk -Recurse -File | Measure-Object Length -Sum | Select-Object Count, Sum
```

Expected: `Count` 208 and `Sum` near 765,800,000 bytes (121 descriptors, 86 sources, `pack0.tpac`;
the install's `Assets` and `AssetSources` equalled the repo's on 2026-11-04; no `RuntimeDataCache`).

**B2. Deploying build from the WP121 worktree** (the maintainer may run `./build.ps1`; sessions may not):

```powershell
Set-Location C:\Users\mikew\source\repos\taom-port\121
./build.ps1
$deps = Join-Path $env:BANNERLORD_GAME_DIR 'Modules\TAOM.Dependencies\bin'
(Get-ChildItem (Join-Path $deps 'Win64_Shipping_Client') -File).Count
(Get-ChildItem (Join-Path $deps 'Win64_Shipping_wEditor') -File).Count
```

Expected: `Build succeeded!`, a log line `TAOM.Dependencies: mirrored N files Win64_Shipping_Client
-> Win64_Shipping_wEditor`, and two equal counts.

**B3. Remove the dead sheets from the install** (a deploy never deletes). If a `RuntimeDataCache`
exists, each sheet's cache entry goes first; its name is bytes 8 to 23 of the descriptor read as a
.NET Guid in upper case (1.5.x banner guide; UNVERIFIED on v1.4.8, and the laptop has no cache today):

```powershell
$taom = Join-Path $env:BANNERLORD_GAME_DIR 'Modules\TAOM'
$rdc  = Join-Path $taom 'RuntimeDataCache'
foreach ($i in 3..41) {
  $n = "ui_taom_bannericons_$i"
  $tex = Join-Path $taom "Assets\GauntletUI\${n}_tex.tpac"
  if (Test-Path -LiteralPath $rdc) {
    $id = ([guid]::new([byte[]]([IO.File]::ReadAllBytes($tex)[8..23]))).ToString().ToUpper()
    $r = Join-Path $rdc "$id.rdc"
    if (Test-Path -LiteralPath $r) { Remove-Item -LiteralPath $r -Confirm:$false }
  }
  Remove-Item -LiteralPath $tex -Confirm:$false
  Remove-Item -LiteralPath (Join-Path $taom "AssetSources\GauntletUI\$n.png") -Confirm:$false
}
Remove-Item -LiteralPath (Join-Path $taom 'Assets\BannerIcons\taom_banners_dunland_alpha_01_tex.tpac.ptemp') -Confirm:$false
(Get-ChildItem (Join-Path $taom 'Assets\GauntletUI') -File).Count
(Get-ChildItem (Join-Path $taom 'AssetSources\GauntletUI') -File).Count
```

Expected: `13` and `13`.

**B4. Before measurement (recommended, not a gate).** Launch the game, load a campaign save (note its
name), leave the map untouched for 60 s, quit to desktop. In the newest
`<game>\bin\Win64_Shipping_Client\Logs\taom_debug_*.log` note the `[MemStation] enter
screen='MapScreen'` line and the next `[MemSample]` line (privMB, heapMB); note Task Manager >
Performance > GPU > Dedicated GPU memory at the 60 s mark. MCM "Enable Memory Sampler" must be on
(default on).

**B5. The Kit loads TAOM.** Launch the Modding Kit with an editor profile of Native, SandBoxCore,
Sandbox, CustomBattle, TAOM.Dependencies and TAOM (TAOM_Map and the Armory are not needed).
Expected: no "Cannot find" box and no "could not be loaded correctly due to a dependency conflict"
box; the Kit opens. `Get-Content (Join-Path $env:BANNERLORD_GAME_DIR 'Modules\TAOM.Dependencies\diag.log') -Tail 200 | Select-String 'loaded by the Modding Kit'`
finds `RunEarlyPhase: loaded by the Modding Kit; skipping crash-loop detection`. If the Kit opens
with no `RuntimeDataCache` and starts compiling TAOM's assets, let it finish.

**B6. Abanissa trial.** In the Texture Inspector for `taom_banners_abanissa_alpha_01`: keep Type
`Albedo (DXT1/DXT5 - RGBA)`, clear **Do Not Compress**, keep **Do Not Generate Mips** ticked,
re-import and save. Runtime Details should read `4096 x 4096, 1 Mip, DXT1, 8.000 Mb` (wording from
the 1.5.x Kit; Part C's decode is the binding check). Close the Kit.
`Test-Path (Join-Path $env:BANNERLORD_GAME_DIR 'Modules\TAOM.Dependencies\session-launching.marker')`
prints `False`.

**B7. Abanissa in game.** Launch the game (no build), load a campaign. The Encyclopedia's kingdom
`Abanissa` and the clans `clan_abanissa_1` to `_8` show their banners with solid sigils, neither
blank nor a faint outline; so do their parties on the map. The newest `diag.log` lines show
`previousCrashLoop=False`. Quit. Blank or faint sigils: STOP.

**B8. The other 32.** Relaunch the Kit and repeat B6's settings for the other 32
`taom_banners_*_tex` textures. Runtime Details: `DXT1, 8.000 Mb` for 24, `DXT5, 16.000 Mb` for the 9
with an alpha channel (`dolguldur_alpha_02`, `dunland_alpha_02`, `goblin_alpha_01`,
`gundabad_alpha_02`, `isengard_alpha_02`, `misc_alpha_01`, `mistymountainorcs_alpha_01`,
`mistymountainorcs_alpha_02`, `mordor_alpha_02`). Save.

**B9. Loading sheets to BC7 (D24).** In the Texture Inspector for `ui_loading_1` and `ui_loading_2`:
clear **Do Not Compress**, keep mips generated (13), and pick the texture type whose Runtime Details
then read BC7 (the type's name in the v1.4.8 Kit is UNVERIFIED). Expected: `4096 x 4096, 13 Mips, BC7`
and about 21.3 Mb each. If no type gives BC7, leave both sheets unchanged and record the types the
list offered: D24 decides BC7, so do not fall back to DXT5. Do not touch `ui_taom_bannericons_1/_2`.
Save, close the Kit, confirm the marker is still absent.

**B10. Hand the files to Part C now.** Do not build. Tell the Part C session (same day) which
textures were re-saved; it copies them into the worktree and commits C1 and C2 straight away, and
waits for your B11 result before C3.

**B11. In-game checks and after measurement** (no build in between). Load the same save as B4:
banners in the Encyclopedia (kingdoms and clans, including a Gundabad, a Harad, a Rhun, a Mordor and
a Rohan clan), map party banners, and flags or banners in one battle; open the clan banner editor and
place icons from the Goblin and Misty Mountain Orc groups (DXT5 sheets) on the preview; its icon list
(`ui_taom_bannericons_1/_2`, unchanged) should look as before. If B9 was done, let three or four
loading screens pass and look for banding or blockiness in skies and dark gradients. Repeat B4's
measurement. Any blank sigil, missing icon or visible loading-screen damage: STOP.

**B12. Record** for the Part C session and the integrator: date; the B2 mirror line and counts; B5
box and `diag.log` results; the textures re-saved with their Runtime Details; the B9 outcome (BC7
done, or the list of types offered); B7 and B11 results; the B4 and B11 numbers.

### Part B done criteria

- [ ] `Modules\TAOM.Dependencies\bin\Win64_Shipping_wEditor` exists with the same file count as
      `Win64_Shipping_Client`
- [ ] The Kit opened TAOM with no load-error box; `diag.log` holds the "loaded by the Modding Kit"
      line; no `session-launching.marker` after closing the Kit
- [ ] 33 banner textures re-saved (24 DXT1, 9 DXT5, 1 mip); B9 done or recorded as not possible
- [ ] B7 and B11 passed; the record of B12 handed over

## Part C: commit the Kit-saved descriptors (ordinary session, same day as Part B)

### Step C0: copy-and-check helper and tree comparison

Write `C:\Users\mikew\source\repos\taom-port\oracle121.py` with the Write tool:

```python
"""plans/121 Part C. Run from the worktree root.
  python <aux>/oracle121.py compare                 # install Assets/AssetSources vs the worktree
  python <aux>/oracle121.py copy-check NAME...      # copy each NAME's _tex.tpac from the install, then check it
  NAME is a texture name, or `banners` for all 33 banner atlases."""
import glob, io, os, shutil, subprocess, sys
sys.path.insert(0, "tools")
import audit_map_scene_memory as amsm

GAME_TAOM = os.path.join(os.environ["BANNERLORD_GAME_DIR"], "Modules", "TAOM")
REPO = "Main/_Module"
OLD, ORACLE = "1b00d1acd", "ab338de2d"
UI = {"ui_loading_1": ("BC7", 4096, 4096, 13, True), "ui_loading_2": ("BC7", 4096, 4096, 13, True)}


def folder(name):
    return "GauntletUI" if name.startswith("ui_") else "BannerIcons"


def blob(ref, rel):
    r = subprocess.run(["git", "show", f"{ref}:{rel}"], capture_output=True)
    return r.stdout if r.returncode == 0 else None


def texture(data):
    amsm.open = lambda p, mode="rb": io.BytesIO(data)
    item = next(i for i in amsm.iter_tpac_items("x", "TAOM") if i.kind == "texture")
    t = amsm.parse_texture_meta(item.meta)
    return item.guid, (t.fmt, t.width, t.height, t.mips, "has_alpha" in t.flags2)


def compare():
    diffs = []
    for top in ("Assets", "AssetSources"):
        a_root, b_root = os.path.join(GAME_TAOM, top), os.path.join(REPO, top)
        a = {os.path.relpath(os.path.join(d, f), a_root) for d, _, fs in os.walk(a_root) for f in fs}
        b = {os.path.relpath(os.path.join(d, f), b_root) for d, _, fs in os.walk(b_root) for f in fs}
        diffs += [f"only in install: {top}/{p}" for p in sorted(a - b)]
        diffs += [f"only in worktree: {top}/{p}" for p in sorted(b - a)]
        diffs += [f"differs: {top}/{p}" for p in sorted(a & b)
                  if open(os.path.join(a_root, p), "rb").read() != open(os.path.join(b_root, p), "rb").read()]
    print("\n".join(diffs))
    print(f"{len(diffs)} difference(s)")
    return 0


def copy_check(names):
    if names == ["banners"]:
        names = sorted(os.path.basename(p)[:-9] for p in glob.glob(f"{REPO}/Assets/BannerIcons/*_tex.tpac"))
    bad = 0
    for name in names:
        rel = f"{REPO}/Assets/{folder(name)}/{name}_tex.tpac"
        shutil.copyfile(os.path.join(GAME_TAOM, "Assets", folder(name), name + "_tex.tpac"), rel)
        new, old = open(rel, "rb").read(), blob(OLD, rel)
        problems, notes = [], [f"size {len(old)} -> {len(new)}"]
        if new[:8] != b"TPAC\x02\x00\x00\x00":
            problems.append("not a TPAC version 2 file")
        g_new, f_new = texture(new)
        g_old, _ = texture(old)
        want = UI.get(name) or texture(blob(ORACLE, rel))[1]
        if g_new != g_old:
            problems.append("texture item id changed: its material would lose it")
        if f_new != want:
            problems.append(f"format {f_new}, want {want}")
        if new[8:24] != old[8:24]:
            notes.append("package id changed (allowed)")
        print(("FAIL " if problems else "OK   ") + name + ": " + "; ".join(problems + notes))
        bad += bool(problems)
    print(f"{len(names) - bad} ok, {bad} failed")
    return 1 if bad else 0


if __name__ == "__main__":
    sys.exit(compare() if sys.argv[1] == "compare" else copy_check(sys.argv[2:]))
```

Then compare before copying anything:

```
WT=C:/Users/mikew/source/repos/taom-port/121
cd $WT && git status --porcelain && python C:/Users/mikew/source/repos/taom-port/oracle121.py compare
```

**Verify**: `git status --porcelain` prints nothing; the comparison lists exactly the re-saved
descriptors as `differs:` (33 `Assets/BannerIcons/taom_banners_*_tex.tpac`, plus the two
`Assets/GauntletUI/ui_loading_*_tex.tpac` if B9 was done) and nothing else. Any other line (a
`_mtl.tpac`, another texture, a `.ptemp`, a file only on one side): STOP and send the list to the
maintainer. Never copy a `.ptemp` into the repo.

### Step C1: the Abanissa sheet (`8cdb11408`, re-saved by the v1.4.8 Kit)

```
WT=C:/Users/mikew/source/repos/taom-port/121
cd $WT && python C:/Users/mikew/source/repos/taom-port/oracle121.py copy-check taom_banners_abanissa_alpha_01 && git status --short
```

**Verify**: `OK   taom_banners_abanissa_alpha_01:` with `size 545 -> ...` and `1 ok, 0 failed`; one
modified file. Stage it (`git -C $WT add Main/_Module/Assets/BannerIcons/taom_banners_abanissa_alpha_01_tex.tpac`),
then commit with (fill `{B7_DATE}` from the Part B record):

```
perf(banners): v2.0.28 - compress the Abanissa banner sheet to DXT1

The Abanissa sigil sheet now compiles as an 8 MB DXT1 texture instead
of a 64 MB uncompressed one. It was re-imported in the v1.4.8 Modding
Kit with one mip: a full mip chain fades thin line-art sigils such as
the winged sun. The descriptor matches the 1.5.x one in format, size
and mip count and keeps its texture id, so the banner material still
finds it. The Abanissa banners were checked in game on {B7_DATE}.
Players get the new pixels with the next pack0.tpac publish.

Research: descriptor decoded with tools/audit_map_scene_memory.py
 against 8cdb11408 and 1b00d1acd
Not-tested: graphics memory before and after (plans/121 records it)
Ported-from: 8cdb1140890211ee46d4393d5a0d71d382bbc1b9
```

### Step C2 (TDD): the other 32 sheets and the atlas gate (`ab338de2d`, re-saved by the Kit)

**C2.1 RED.** In `tools/tests/test_taom_texture_formats.py`, insert after `ui_problems`:

```python
def atlas_problems(tex):
    out = []
    for name, t in sorted(tex.items()):
        want = ("DXT5" if "has_alpha" in t.flags2 else "DXT1", 4096, 4096, 1)
        if (t.fmt, t.width, t.height, t.mips) != want:
            out.append(f"{name}: {t.fmt} {t.width}x{t.height} {t.mips} mips, want {want}")
    return out
```

insert before `class UiSheetFormatTests`:

```python
class BannerAtlasFormatTests(unittest.TestCase):
    """#913: each banner atlas is 4096 px with one mip, DXT5 when its source has alpha and DXT1
    otherwise; 2,112 MiB of RGBA8 for all 33 becomes 336 MiB. The single mip is deliberate: with a
    full chain the banner render reads a smaller mip and thin line-art sigils fade
    (docs/reference/banner-icon-generation.md Step 4a)."""

    maxDiff = None

    def test_every_banner_atlas_is_block_compressed_with_one_mip(self):
        tex = textures_in("BannerIcons")
        self.assertEqual(33, len(tex), "one _tex.tpac per banner material")
        self.assertEqual([], atlas_problems(tex))
```

and add to `PublishedPackTests`:

```python
    def test_pack_banner_atlases_are_block_compressed_with_one_mip(self):
        atlases = {n: t for n, t in self.textures.items() if n.startswith("taom_banners_")}
        self.assertEqual(33, len(atlases))
        self.assertEqual([], atlas_problems(atlases))
```

**Verify**: Texture gate `Ran 7 tests`, `FAILED (failures=1, skipped=4)`; the failure lists 32 RGBA8
atlases.

**C2.2 GREEN.**

```
WT=C:/Users/mikew/source/repos/taom-port/121
cd $WT && python C:/Users/mikew/source/repos/taom-port/oracle121.py copy-check banners && git diff --name-only -- Main/_Module/Assets/BannerIcons | wc -l
```

**Verify**: `33 ok, 0 failed` (Abanissa re-copied unchanged) and `32`; Texture gate `Ran 7 tests`,
`OK (skipped=4)`.

**C2.3 Doc.**

```
WT=C:/Users/mikew/source/repos/taom-port/121
cd $WT && python - <<'PY'
import datetime, sys
sys.path.insert(0, "C:/Users/mikew/source/repos/taom-port")
from docedit121 import edit
today = datetime.date.today().isoformat()
edit("docs/reference/banner-icon-generation.md", "1. **Texture**", [
    r'''   - **Import settings:** keep Type `Albedo (DXT1/DXT5 - RGBA)`, leave **Do Not Compress** clear and tick **Do Not Generate Mips**. Runtime Details must read `4096 x 4096, 1 Mip, DXT1, 8.000 Mb` (DXT5 and 16 Mb for a source with an alpha channel). An uncompressed sheet costs 64 Mb; on this line all 33 sheets were that way until ''' + today + r''' (2,112 Mb in total, now 336 Mb; `tools/tests/test_taom_texture_formats.py` pins the formats). Keep the single mip: with a full chain the banner render reads a smaller mip, and thin line-art sigils such as the Abanissa winged sun fade to a faint outline.''',
    r'''   - **Copy the saved `_tex.tpac` into the repo** (`Main/_Module/Assets/BannerIcons/`) before anyone runs a deploying build. Every deploying build copies the repo's descriptor over the live one but leaves the live `RuntimeDataCache`, so a reverted header meets a recompiled cache: the sheet's sigils render blank, and the Kit crashes on its next start while it recompiles (recorded on the 1.5.x Kit in October 2026 as a material with a null shader).'''], mode="after")
PY
```

**C2.4 Commit.** Stage the 32 descriptors, the test and the doc:

```
WT=C:/Users/mikew/source/repos/taom-port/121
cd $WT && git add $(git diff --name-only -- Main/_Module/Assets/BannerIcons) tools/tests/test_taom_texture_formats.py docs/reference/banner-icon-generation.md && git diff --cached --name-only | wc -l
```

Expected `34`. Message:

```
perf(banners): v2.0.28 - compress the other 32 banner sheets

All 33 banner sigil sheets now compile compressed: 24 as DXT1 (8 MB)
and the 9 whose source has an alpha channel as DXT5 (16 MB), each at
4096 px with one mip. Together they drop from 2,112 MB to 336 MB of
texture memory once every sheet is drawn; the 19 sheets a campaign's
banners name drop from 1,216 MB to 152 MB. That memory most likely
sits on the graphics card rather than in the game's private bytes.
The banner guide now gives the import settings and says to copy a
saved texture into the repo before any build, and a tool test fails
on an uncompressed or mipped atlas.

Research: descriptors decoded against ab338de2d; banner keys per the
 2026-11-04 memory verification (V07)
Not-tested: graphics memory before and after (plans/121 records it)
Ported-from: ab338de2d4aaca0f7d0fe9478c84fe74a1ff928f
```

### Step C3 (TDD, only if B9 re-saved both loading sheets): the loading sheets (D24, new work)

Start this step only after the maintainer reports B11's loading-screen check passed: its commit
message records that check. If it is not yet reported, wait; do not fill the date yourself.

**C3.1 RED.** Add to `UI_FORMATS` in `tools/tests/test_taom_texture_formats.py`:

```python
    # plans/121 (D24): the two AlwaysLoad loading sheets are BC7 and keep their 13 mips.
    "ui_loading_1": ("BC7", 13),
    "ui_loading_2": ("BC7", 13),
```

**Verify**: Texture gate `FAILED (failures=1, skipped=4)` naming `ui_loading_1` and `ui_loading_2`.

**C3.2 GREEN.**
`cd $WT && python C:/Users/mikew/source/repos/taom-port/oracle121.py copy-check ui_loading_1 ui_loading_2`
prints `2 ok, 0 failed`; Texture gate `OK (skipped=4)`.

**C3.3 Doc.**

```
WT=C:/Users/mikew/source/repos/taom-port/121
cd $WT && python - <<'PY'
import datetime, sys
sys.path.insert(0, "C:/Users/mikew/source/repos/taom-port")
from docedit121 import edit
today = datetime.date.today().isoformat()
edit("docs/features/gui-sprite-system.md", "| Wrong sprite name renders blank silently |", [
    r'''| A sheet bake may reset a texture's compression (unverified) | `ui_loading_1` and `_2` are BC7 with 13 mips since ''' + today + r''' (plans/121); `ui_taom_bannericons_NN` stay uncompressed with one mip ([banner-icon-generation.md](../reference/banner-icon-generation.md) Step 4b). After any bake run `python -m unittest discover -s tools/tests -t . -p "test_taom_texture_formats.py"` | `tools/tests/test_taom_texture_formats.py` |'''], mode="after")
PY
```

**C3.4 Commit.**
`git -C $WT add Main/_Module/Assets/GauntletUI/ui_loading_1_tex.tpac Main/_Module/Assets/GauntletUI/ui_loading_2_tex.tpac tools/tests/test_taom_texture_formats.py docs/features/gui-sprite-system.md`,
message (fill `{B11_DATE}`):

```
perf(ui): v2.0.28 - loading screen sheets load as BC7

The two always-loaded loading screen sheets, ui_loading_1 and _2, now
compile as BC7 with their 13 mips: 21.3 MB each instead of 85.3 MB of
uncompressed RGBA, held from the main menu until the game quits. The
banner icon sprite sheets keep their uncompressed format, as the
banner guide requires. Several loading screens were checked for
banding on {B11_DATE}.

Research: descriptors decoded with tools/audit_map_scene_memory.py
Not-tested: process memory before and after (plans/121 records it)
```

If B9 was not possible, skip C3 and say so in the hand-off.

### Step C4: Part C gate and hand-off

1. `cd $WT && python C:/Users/mikew/source/repos/taom-port/oracle121.py compare` prints
   `0 difference(s)`.
2. Build, Test, Tool tests, Docs: Build exit 0; Test `Failed: 0` (no new C# tests in Part C); Tool
   tests `Ran N+7` against A0.4, skipped up by 4, failures and errors unchanged; no lint counter up.
3. Hand-off (see "Hand-off report"). From now on a deploying build from this worktree is safe; one
   from any other tree is not, until the integrator merges WP121.

### Part C done criteria

- [ ] `git -C $WT log --oneline bannerlord-1.4.5..HEAD` adds C1, C2 (and C3 if B9 was done)
- [ ] Texture gate `Ran 7 tests`, `OK (skipped=4)`; `oracle121.py compare` prints `0 difference(s)`
- [ ] Build exit 0; Test `Failed: 0`; no new tool-test failure; `git -C $WT status --porcelain` empty

## Part D: publish and package at the next release (maintainer)

Players get the change only through a republished `pack0.tpac`; the repo never tracks it. Do this in
the next 1.4.x release (`/release`, D8: v2.4.0), after WP121 is merged and deployed from
`bannerlord-1.4.5`.

1. Publish TAOM from the laptop install (its `RuntimeDataCache` holds the Part B compile), as for
   earlier releases, into an empty folder outside the game install.
2. Check the published pack (from the main checkout or any tree with WP121):
   `TAOM_PACK0="<publish folder>/TAOM/AssetPackages/pack0.tpac" python -m unittest discover -s tools/tests -t . -p "test_taom_texture_formats.py" -v`
   (Git Bash syntax). Expected: `Ran 7 tests`, `OK` with no skip: the pack holds exactly the repo's 47 textures and 34
   materials, 33 compressed atlases, the pinned UI formats and no undeclared sheet. Record the new
   `pack0.tpac` size (188,902,921 B before).
3. Package per `/release` with `tools/package_release.py`. If `--keep-rdc` is used, its
   `RuntimeDataCache` must come from the same install as the publish.
4. In game on the packaged build: banners and loading screens as in B11.
5. On any other machine with a 1.4.x dev install and a `RuntimeDataCache`, delete the cache entries of
   the 33 atlases and two loading sheets (B3's GUID rule) before its first Kit start after deploying
   WP121, or re-import them there; otherwise its old cache meets the new descriptors.
6. If Part E landed: `python tools/check_map_vista.py --game-modules <release Modules folder>` exits 0
   for the TAOM_Map package the release pins.

## Part E (conditional): the campaign map vista (MEMORY-06)

**E0. Precondition and decision (maintainer).** Proceed only if one machine has (a) a v1.4.8
Modding Kit and (b) TAOM_Map's loose sources for the package the next 1.4.x release pins: not the
desktop's live TAOM_Map after 2026-10-29, which carries 1.5.x culture and kingdom ids. The laptop's
TAOM_Map is cooked and cannot be republished. Recommended change: re-import the source of
`16K_Vista_02` downscaled to 8192x8192, keeping the texture name (so `scene.xscene` needs no edit),
DXT5 and a full mip chain: 85.3 MiB instead of 341.3 MiB. Not recommended: blanking
`vista_diffuse_name` as vanilla does, because vanilla draws its horizon from
`vista_tileset="WorldMap"`, which TAOM_Map leaves empty. If (a) or (b) is missing, record "Part E
BLOCKED: no pinned 1.4.x TAOM_Map source in a v1.4.8 Kit" in the hand-off and stop Part E; the rest
of this plan is complete without it.

**E1 (TDD, ordinary session, only after E0 says go).** RED: create
`tools/tests/test_check_map_vista.py`:

```python
#!/usr/bin/env python3
"""Tests for tools/check_map_vista.py (plans/121 Part E). Pure functions, no game install."""
import os
import sys
import unittest

sys.path.insert(0, os.path.dirname(os.path.dirname(os.path.abspath(__file__))))

import check_map_vista as cmv  # noqa: E402


class VistaNamesTests(unittest.TestCase):
    def test_reads_only_the_vista_texture_attributes(self):
        xml = '<scene><terrain vista_diffuse_name="16K_Vista_02" vista_normalmap="" dynamic_flowmap_texture_name="flow" /></scene>'
        self.assertEqual({"16K_Vista_02"}, cmv.vista_names(xml))

    def test_a_vanilla_scene_names_no_vista(self):
        self.assertEqual(set(), cmv.vista_names('<scene><terrain vista_diffuse_name="" vista_tileset="WorldMap" /></scene>'))


class ProblemsTests(unittest.TestCase):
    def test_a_16k_vista_is_over_an_8k_cap(self):
        self.assertEqual(1, len(cmv.problems({"v"}, {"v": (16384, 16384)}, 8192)))

    def test_an_8k_vista_passes_an_8k_cap(self):
        self.assertEqual([], cmv.problems({"v"}, {"v": (8192, 8192)}, 8192))

    def test_a_named_texture_found_nowhere_is_a_problem(self):
        self.assertIn("no TAOM_Map pack or descriptor", cmv.problems({"v"}, {}, 8192)[0])

    def test_no_vista_no_problem(self):
        self.assertEqual([], cmv.problems(set(), {}, 8192))


if __name__ == "__main__":
    unittest.main()
```

`cd $WT && python -m unittest discover -s tools/tests -t . -p "test_check_map_vista.py"` errors
(`No module named 'check_map_vista'`). GREEN: create `tools/check_map_vista.py` (stdlib, read-only,
shaped like the other `tools/` CLIs):

- After `sys.path.insert(0, <its own folder>)`, import `audit_map_scene_memory as amsm` and
  `from _gamedir import game_modules`. `VISTA_ATTRS = tuple(a for a in amsm.TERRAIN_TEXTURE_ATTRS if a.startswith("vista_"))`.
- `vista_names(scene_xml: str) -> set`: `ET.fromstring`, then every attribute named in `VISTA_ATTRS`
  with a non-blank value, on any element.
- `texture_dims(paths, names) -> dict`: name to `(width, height)` for each texture item of
  `amsm.iter_tpac_items(path, "TAOM_Map")` whose name is in `names`, decoded with
  `amsm.parse_texture_meta`; when a name occurs twice keep the larger.
- `problems(names, dims, max_px) -> list`: for each name in sorted order,
  `"<name>: named by the scene but in no TAOM_Map pack or descriptor"` when absent from `dims`, or
  `"<name>: <w>x<h>, over the <max_px> px cap"` when `max(w, h) > max_px`.
- `main(argv=None) -> int`: `--game-modules` (default `str(game_modules(amsm.DEFAULT_GAME))`) and
  `--max-px` (int, default 8192). Scene `<modules>/TAOM_Map/SceneObj/Main_map/scene.xscene`, read
  with `encoding="utf-8-sig"`; texture packages `AssetPackages/*.tpac` plus `Assets/**/*_tex.tpac`.
  No scene or no packages: print `MAP_VISTA: no scene or no texture packages under <folder>`, return 2.
  Otherwise print `vista texture: <name>` per name and `MAP_VISTA_OVERSIZE: <problem>` per problem,
  then `OK` or `<n> problem(s)`; return 2 when any problem, else 0.
- Module docstring: why (TAOM_Map is unversioned, so a reinstall brings the 16K vista back; v1.4.8
  keeps its whole mip chain in system memory, `docs/investigations/native-commit-audit-2026-08.md:535-538`),
  what it reads (table of contents only), usage and exit codes. `if __name__ == "__main__": sys.exit(main())`.

Verify: the test module prints `Ran 6 tests` and `OK`; `cd $WT && python tools/check_map_vista.py`
against today's laptop install prints `MAP_VISTA_OVERSIZE: 16K_Vista_02: 16384x16384, over the 8192
px cap` and exits 2 (the live RED). Do not edit `tools/README.md`: the decisions brief reserves it
for the sync WPs, so the catalogue row goes through the hand-off to WP142 (see "Hand-off report").

Commit `feat(tools): v2.0.28 - gate the campaign map vista texture size` (stage the two new files;
body: what the gate reads, why TAOM_Map needs one, the 341.3 MiB figure;
`Not-tested: a pinned TAOM_Map with the 8K vista (plans/121 E2)`).

**E2 (maintainer).** Re-import the downscaled vista in the v1.4.8 Kit on the E0 machine, look at
the campaign map horizon in game (no seam, no blur that matters at the default camera), compare
B4-style `[MemStation]` numbers on the map, republish TAOM_Map, pin that package for the release, and
run `python tools/check_map_vista.py --game-modules <that Modules folder>`: expected `OK`, exit 0.

## Disposition

| Item | Disposition | Where |
|---|---|---|
| `8cdb11408` perf(banners): compress the Abanissa banner sheet to DXT1 | Re-implemented: the descriptor is re-saved by the v1.4.8 Kit with the same settings; the 1.5.x blob is only the oracle (D23, V07: a header copied without its compile mismatches the cache). Clean simulated pick not used | B6, C1 |
| `ab338de2d` perf(banners): compress the other 32 banner sheets | Re-implemented for the 32 descriptors (Kit); `banner-icon-generation.md` hunk adapted (date for this line, no 1.5.x native crash offset); `docs/ai-includes/orientation.md` hunk skipped: absent on 1.4.x (the one simulated conflict), docs-sync WPs own it | B8, C2 |
| `9985f2db3` feat(banners): realm banners, banner sheets packed | Partial. Taken: the `MirrorWin64ShippingClientToEditor` target; its `ClientDeploymentTests` pin re-homed as `DependenciesEditorMirrorTests`; Kit lines in `scene-scripts.md`, `map-maker-quickstart.md` and the toolchain doc rewritten for 1.4.x; deletion of the 39 dead GUI sheets and the stray `.ptemp`; the `open-questions.md` and `banners-and-heraldry.md` dead-sheet lines. Skipped (content, OUT, tied to 1.5.x-only realms; packing saves little once the sheets are DXT1): the BannerIcons repack, realm banners, sprite rebake, `banner_icons.xml`, clan and kingdom data, `pack_banner_sheets.py` and tests, generator edits, faction docs, `modules-overview.md` 1.5.x counts, `tools/README.md` | A1, A3, B3 |
| `b41de91e8` fix(banners): safe sheet packing, Cardolan and Lindon colours | Partial. Taken: `IncompatibleModDetector.IsEditorBin`, its early return and `IncompatibleModDetectorTests` (path-limited apply); the csproj comment; the Kit facts, re-verified on the v1.4.8 editor build (the load-error text differs from 1.5.x). Skipped: `ReflectionSiteBindingTests.cs` and `reflection-sites.md` line pins (1.4.x pins no detector line); Cardolan and Lindon colours (content); the packer rewrite and tests; the packing sections of the banner guide; 1.5.x counts in `module-taom.md` and `banners-and-heraldry.md` (A3 writes 1.4.x counts); lessons, RCA and `.claude/rules/moduledata-validation.md` (WP142, WP143); realm tools | A1, A2 |
| Shared hunks | None assigned to this WP (`shared_groups` is empty) | |
| MEMORY-02 banner atlases | Parts A to D | |
| MEMORY-03 UI sheets | D24: `ui_loading_1/_2` to BC7 (B9, C3); `ui_taom_bannericons_1/_2` stay RGBA8 with one mip, pinned by `UiSheetFormatTests` | B9, C3 |
| MEMORY-06 16K vista | Part E: maintainer step on a machine with the pinned TAOM_Map sources and a v1.4.8 Kit, plus the in-repo gate `tools/check_map_vista.py`; blocked and recorded if E0 fails | E |
| MEMORY-08 TAOM_Map prefabs | Deferred: the 141 `Prefabs/*.xml` (93,830 `<game_entity>` on the laptop's v2.0.27) are real, but the unreferenced list is a one-off string sweep that cannot see runtime-built names, the saving is unknown (probably tens of MB), and acting needs a TAOM_Map republish plus a load test. Needs its own plan with a reproducible sweep tool when TAOM_Map is next republished for 1.4.x | not planned |
| MEMORY-10 dead sprite sheets | Ported | A3, B3, D |
| MEMORY-10 module sounds | Skipped: download size only, no runtime memory (sounds open when played, per the 1.5.x reading). Routed to WP134: `Main/_Module/ModuleData/module_sounds.xml:5` registers `LOTR/Elves/Alert/elf_horn.wav`, but the file is `Main/_Module/ModuleSounds/LOTR/Elves/Alert/horn_elf.wav` (unfixed on both lines; nothing in `Main` names either). WP134 (`plans/134-campaign-data-defects.md`) owns its row and its fix-or-skip verdict; this plan changes no sound data | WP134 |
| MEMORY-10 TAOM_Map authoring leftovers | Skipped: unversioned module, download size only; belongs to a future TAOM_Map pass | not planned |
| Not ported by decision | None of D6, D9, D11, D12, D14, D16 applies here; D7 keeps `ClientDeploymentTests` out | |

## Test plan

- C#: `DependenciesEditorMirrorTests` (2: the copy shape, the agent-build skip), modelled on
  `BundledDependencyManifestTests`; `IncompatibleModDetectorTests` (6 DataRows: wEditor in two
  casings true; Client, Server, empty, null false), as 1.5.x wrote it.
- Python `tools/tests/test_taom_texture_formats.py`: `SpriteSheetFileTests` (no sheet file beyond the
  declared counts), `UiSheetFormatTests` (pinned UI formats; C3 adds the loading sheets),
  `BannerAtlasFormatTests` (C2), and `PublishedPackTests` (4, skipped unless `TAOM_PACK0` is set:
  exact texture and material set, UI formats, no undeclared sheet, compressed atlases). Pattern:
  `tools/tests/test_package_release.py`.
- Python `tools/tests/test_check_map_vista.py` (Part E, 6).
- Not unit-testable, named in `Not-tested:` trailers: the mirror target in a deploying build; a real
  Kit session; the pixels the Kit compiles; graphics and process memory before and after; the
  published pack (checked at Part D with `TAOM_PACK0`).
- Expected totals: C# Passed up by 8 against A0.4 (2 plus 6 DataRows), Failed 0; tool tests up by 7
  with 4 skipped (Part E adds 6 more).

## Done criteria

- [ ] Part A done criteria hold
- [ ] Part B record received (B12), or Part B is reported blocked with the reason
- [ ] Part C done criteria hold (or Part C is reported blocked because Part B is)
- [ ] Final tree: Build exit 0; Test `Failed: 0`; Texture gate `OK (skipped=4)`; `git -C $WT status --porcelain` empty
- [ ] Dash check prints `0` (no added docs line carries U+2013 or U+2014):

```
WT=C:/Users/mikew/source/repos/taom-port/121
cd $WT && git diff bannerlord-1.4.5..HEAD -- docs | python -c "import sys; t=sys.stdin.buffer.read().decode('utf-8','replace'); print(sum(1 for l in t.splitlines() if l.startswith('+') and ('\u2013' in l or '\u2014' in l)))"
```

- [ ] Part D listed as owed at the next release; Part E landed or recorded BLOCKED

## STOP conditions

Stop and report (do not improvise) if:

- A0.1: no `Baseline after WP101` line, or the branch or path already exists.
- A0.3: an anchor check differs from its expected value, or `git diff --stat` shows changes under
  `Main/_Module/Assets`, `AssetSources/GauntletUI`, `TAOMSpriteData.xml` or the detector.
- The A1.2 script prints any `STOP`, or `docedit121.py` reports an anchor not found exactly once.
- A `git apply` in A2 is not clean.
- A commit is refused by a CHANGELOG gate (WP102 not merged), or by any gate other than the subject
  version check.
- A3.3's measured counts differ from 81, 47 and 483.
- Part B: the Kit still shows "Cannot find" or a dependency-conflict box after B2, crashes, or does
  not open TAOM (send the box texts and the newest `rgl_log`; recommend `/investigate`, or
  `/native-crash-triage` for an access violation in `TaleWorlds.Native.dll`).
- Part B: Runtime Details show a format other than DXT1 for a no-alpha sheet, DXT5 for an alpha
  sheet, or more than one mip on a banner sheet.
- Part B: blank or faint sigils, missing icons or visibly damaged loading screens in B7 or B11.
  Recovery for a sheet: delete its `RuntimeDataCache` entry (B3's GUID rule), copy its descriptor
  back from the B1 backup, and the game falls back to `pack0.tpac`'s copy.
- A deploying build ran from any tree after B6 and before Part C's copy. Recovery: re-import the
  affected sheets in the Kit (or copy the Kit-saved descriptors back from the worktree if Part C
  already copied them), then repeat B11.
- C0's comparison shows anything besides the re-saved descriptors, or any `copy-check` line says
  `FAIL` (a changed texture item id breaks the material binding; a wrong format means Part B is wrong).
- A build or test fails twice after a reasonable fix, or a step seems to need `Main/IoC.cs`,
  `Main/SubModule.cs`, `Main/TAOM.csproj`, `Directory.Build.props` or `.claude/settings*.json`.

## In-game checks owed (maintainer)

1. B5: the Kit opens TAOM with no "Cannot find" or dependency-conflict box; `diag.log` has
   `RunEarlyPhase: loaded by the Modding Kit; skipping crash-loop detection`; no
   `session-launching.marker` after the Kit closes; the next game's `diag.log` reads
   `previousCrashLoop=False`.
2. B7: Abanissa kingdom and clan banners render solid sigils (Encyclopedia and map).
3. B11: banners on the map, in the Encyclopedia and in one battle; Gundabad, Harad, Rhun, Mordor and
   Rohan clans; Goblin and Misty Mountain Orc icons in the banner editor preview; the banner editor
   icon list unchanged.
4. B11 (if B9 was done): three or four loading screens without banding or blockiness.
5. B4 and B11: `[MemStation] enter screen='MapScreen'` and the next `[MemSample]` in
   `<game>\bin\Win64_Shipping_Client\Logs\taom_debug_*.log`, and Dedicated GPU memory, before and
   after (a record, not a gate; D23).
6. Part D: the `TAOM_PACK0` run passes with no skip; banners and loading screens on the packaged build.
7. Part E: the map horizon after the vista change; `check_map_vista.py` exits 0 on the pinned TAOM_Map.

## Maintenance notes

- The deploy-overwrites-descriptor hazard outlives this plan: after any Kit re-import, copy the saved
  `_tex.tpac` into the repo before any deploying build (now in `banner-icon-generation.md` Step 4a).
- A sprite bake through the editor may reset the loading sheets' compression (UNVERIFIED);
  `UiSheetFormatTests` fails if it does. New banner sheets must follow Step 4a's settings;
  `BannerAtlasFormatTests` fails otherwise and pins the count at 33, so a new sheet updates it.
- The gate proves the descriptors and, with `TAOM_PACK0`, the published pack. Neither proves which
  copy a dev install renders when loose descriptors and the pack both hold a texture; B11 is the
  visual check.
- Reviewer focus (`/deep-review`): the derived csproj target equals 1.5.x's and stays skipped under
  `-p:ModuleId=`; `IsEditorBin` cannot be true in the game or on a server (`Win64_Shipping_Client`,
  `Win64_Shipping_Server`); the early return sits inside the `try`, after the `_earlyPhaseRan`
  guard; the 79 deletions are exactly sheets 3 to 41 plus the `.ptemp`; the docs carry the v1.4.8
  load-error text, not 1.5.x's.
- `docs/modding/modules-overview.md:67-69` and `:531` still give the 2026-09-05 bin counts
  (`TAOM.Dependencies` wEditor 0); WP144 or the next re-measure updates them.
- The next 1.4.x release must pin both its TAOM_Map package (the desktop's live map carries 1.5.x
  ids) and a `pack0.tpac` published after WP121.
- MEMORY-08 and the TAOM_Map authoring leftovers wait for the next TAOM_Map republish; fold them into
  Part E's republish only with their own plan.

## Hand-off report

Put these in your final message (no file). The integrator records them in `plans/README.md` after the
merge; this plan closes no old-plan row there.

- Status of each part: DONE, BLOCKED with the reason, or owed.
- Part A: worktree and branch; the three commit SHAs and subjects; A0.4 and A4 test lines (Passed,
  Skipped, Failed), tool-test `Ran` and result lines before and after, lint summary deltas; the
  `/deep-review` outcome or "owed"; any STOP met.
- Part B (maintainer): the B12 record.
- Part C: C1 to C3 SHAs (or why C3 was skipped); the `copy-check` summary lines; `compare` result;
  final test and tool-test lines.
- Part E: E0 outcome (go, or BLOCKED with the reason); E1 SHA and the live gate output if done.
- For WP142 (Step E1, tools/README rows), only if E1 landed: add this row to `tools/README.md` after
  the `check_prefab_budget.py` row. If WP142 has already merged when E1 lands, the integrator adds it
  when merging this plan's E1 commit. If E1 never landed, no row is owed.

```
| `check_map_vista.py` | **Campaign map vista size gate** (read-only). Reads every `vista_*` texture name in `TAOM_Map/SceneObj/Main_map/scene.xscene` and that texture's size from TAOM_Map's packs and loose descriptors (table of contents only); exits 2 when one is over `--max-px` (default 8192) or found nowhere. TAOM_Map is unversioned, so this is the in-repo gate for the plans/121 vista change: a reinstall that brings back `16K_Vista_02` (341.3 MiB held in system memory on v1.4.8) fails it. Tests: `tools/tests/test_check_map_vista.py`. | `--game-modules`, `--max-px` |
```
- For the integrator: `elf_horn.wav` (see the Disposition row "MEMORY-10 module sounds") is WP134's;
  nothing is owed from this plan beyond confirming that `plans/134-campaign-data-defects.md` carries its row.
