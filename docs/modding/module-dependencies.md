# TAOM.Dependencies: the libraries module

## What this module is

`TAOM.Dependencies` is the module that carries every third-party library TAOM's code needs at
runtime: Harmony, UIExtenderEx, ButterLib and MCM, plus the helper DLLs those four bring with them
(`Dependencies/_Module/THIRD-PARTY-LICENSES.txt`). It registers no game data at all: its manifest has
no `<Xmls>` block, only seven `<SubModule>` entries that boot other people's classes out of its own
`bin/` folder (`Dependencies/_Module/SubModule.xml:85-236`). A player needs only `TAOM` and
`TAOM.Dependencies` ticked in the launcher; the standalone `Bannerlord.Harmony`,
`Bannerlord.ButterLib`, `Bannerlord.UIExtenderEx` and `Bannerlord.MBOptionScreen` modules are not
required and should be uninstalled (`Dependencies/_Module/SubModule.xml:124-126`,
[dr3-maintenance.md](../migration/dr3-maintenance.md) line 309).

This chapter is a concept chapter: it explains what the four libraries are, why they are packaged
this way, the version pairing that a release must keep intact, the vendored-DLL allowlist, the
licence notice, the co-op manifest and the four alias stub modules. How a manifest is read in general
is in [submodule-and-registration](submodule-and-registration.md); how the launcher orders modules is
in [load-order-and-dependencies](load-order-and-dependencies.md); the other modules are in
[modules-overview](modules-overview.md).

## The four libraries in plain words

- **Harmony** (`0Harmony.dll`, NuGet package `Lib.Harmony 2.4.2`, MIT, Andreas Pardeike) lets a mod
  change what a method inside the game's own compiled code does without editing the game. A mod
  attaches a "prefix" that runs before the engine method or a "postfix" that runs after it.
  Everything in TAOM that alters vanilla behaviour goes through it
  (`Dependencies/_Module/THIRD-PARTY-LICENSES.txt:8-11`, `Dependencies/TAOM.Dependencies.csproj:70`).
  Harmony has no SubModule class of its own; the runtime loads `0Harmony.dll` the first time any
  code touches a `HarmonyLib` type (`Dependencies/_Module/SubModule.xml:142-146`).
- **UIExtenderEx** (`Bannerlord.UIExtenderEx.dll`, NuGet `Bannerlord.UIExtenderEx 2.13.2`, MIT, BUTR)
  does the same job for the game's screens: it injects widgets and view-model properties into
  TaleWorlds' Gauntlet UI instead of replacing whole screens
  (`Dependencies/_Module/THIRD-PARTY-LICENSES.txt:13-16`, `Dependencies/TAOM.Dependencies.csproj:71`).
- **ButterLib** (`Bannerlord.ButterLib.dll` plus one `Bannerlord.ButterLib.Implementation.1.4.N.dll`
  per game build, ButterLib 2.11.0, MIT, BUTR) is BUTR's shared utility layer: a dependency-injection
  container, logging through Serilog, and a crash-report renderer. It is not on NuGet as a runtime
  package; the DLLs are copied by hand from Steam Workshop item `2859232415`
  (`Dependencies/_Module/THIRD-PARTY-LICENSES.txt:18-21`, [dr3-maintenance.md](../migration/dr3-maintenance.md) lines 48-55).
- **MCM**, the Mod Configuration Menu (NuGet `Bannerlord.MCM 5.12.1` for the API in `MCMv5.dll`, and
  the vendored `Bannerlord.MBOptionScreen.v1.4.N.dll` set plus `MCM.UI.Adapter.MCMv5.dll` for the
  screen, 5.12.1, MIT, BUTR) is the in-game **Mod Options** tab. It turns a C# settings class into
  sliders and checkboxes. MCM the library is a `TAOM.Dependencies` concern; the `Main/Features/Mcm/`
  folder in the main module is only TAOM's layout fix for it ([mcm.md](../features/mcm.md) line 7).

Alongside those four the folder also ships six `BUTR.CrashReport*.dll` files (v14.0.0.99, required
by ButterLib: without them its type enumeration throws `ReflectionTypeLoadException` at SubModule
init, `Dependencies/_Module/SubModule.xml:112-115`), seven `Microsoft.Extensions*` and
`Microsoft.Bcl.HashCode` DLLs, three Serilog DLLs, and six `System.*` shims for .NET Framework 4.7.2
(`Dependencies/_Module/THIRD-PARTY-LICENSES.txt:34-62`).

## Why they live in a separate module

Three reasons, each written down in the repo.

1. **Load order.** `Dependencies/SubModule.cs:21-30` installs an `AssemblyResolve` redirect from a
   static constructor, so it runs before anything else in that assembly. Any module constructed
   before it can win the process slot with its own bundled `0Harmony.dll` or `MCMv5.dll`, after which
   TAOM's patches attach to a Harmony instance nobody else can see, and the failure is silent
   ([coop-interop.md](../features/coop-interop.md) lines 116-124). A module can be ordered above
   `Native`; a loose DLL inside `TAOM/bin/` cannot.
2. **One copy for everyone.** The seven `<SubModule>` entries boot UIExtenderEx, ButterLib, MCM and
   the MBOptionScreen loader from this one folder, so the player never has to install or order the
   four upstream modules (`Dependencies/_Module/SubModule.xml:124-126`).
3. **Independent versioning.** The Dependencies assembly changes on its own cadence, which is what
   the #371 pairing exists to track ([release-process.md](../reference/release-process.md) lines
   42-57). `Main/TAOM.csproj:89-91` takes a `<ProjectReference>` on it with `Private=False`, and the
   comment at `Main/TAOM.csproj:102-106` says TAOM resolves `HarmonyLib` and
   `Bannerlord.UIExtenderEx` types through that assembly at runtime.

## How the engine reads a manifest

A module is a folder under `Modules/` that contains a `SubModule.xml` at its root, nothing more:
`ModuleHelper.GetPhysicalModules` lists the directories, checks `Path.Combine(text, "SubModule.xml")`
and skips the folder when the file is absent (`ModuleHelper.cs:319-331`). It then calls
`ModuleInfo.LoadWithFullPath`, which parses exactly the elements in the first table. There is no
branch for `<DependedModuleMetadatas>`: a search for `DependedModuleMetadata` across the whole v1.4.8
managed decompile returns nothing. That element is vocabulary for the BUTR/BLSE launchers only, and
both TAOM manifests say so in their comments (`Main/_Module/SubModule.xml:20-22, 32-37`).

<!-- engine-table type="TaleWorlds.ModuleManager.ModuleInfo" file="Platform/TaleWorlds.ModuleManager/TaleWorlds.ModuleManager/ModuleInfo.cs" method="LoadWithFullPath" inert="RequiredBaseVersion" -->

| Attribute | Type | Required | Default when absent | What it does | Read at (file:line) |
|---|---|---|---|---|---|
| `Name@value` | string | yes | unguarded dereference: the module throws and is shown as "can't be loaded" (`ModuleHelper.cs:113-126`) | display name in the launcher list | `ModuleInfo.cs:81` |
| `Id@value` | string | yes | throws, as above | the module's identity; matched case-insensitively (`ModuleHelper.cs:37, 87`) | `ModuleInfo.cs:82` |
| `Version@value` | version string (`v2.0.6`) | yes | throws, as above | parsed with `ApplicationVersion.FromString`; the engine stores it and never compares it against anything | `ModuleInfo.cs:87` |
| `RequiredBaseVersion@value` | version string | no | unset | read but has no effect for a community module: the only consumer is the `NavalDLC` check at `ModuleHelper.cs:73-80` | `ModuleInfo.cs:88-91` |
| `DefaultModule@value` | `true` or anything else | no | `false` | sets `IsDefault`, which the launcher uses when deciding what to tick on first run | `ModuleInfo.cs:92` |
| `ModuleType@value` | `Community`, `Official`, `OfficialOptional` (`ModuleType.cs:3-8`) | no | `Community` (the enum's first member) | `IsOfficial` is `Type != Community` (`ModuleInfo.cs:27`) | `ModuleInfo.cs:93-97` |
| `ModuleCategory@value` | `Singleplayer`, `Multiplayer`, `MultiplayerOptional`, `Server` (`ModuleCategory.cs:3-9`) | no | `Singleplayer` | launcher filtering | `ModuleInfo.cs:99-104` |
| `DependedModules/DependedModule@Id` (+ `@DependentVersion`, `@Optional`) | list | no | empty list | the engine-honoured hard dependency list; see below for what it does and does not do | `ModuleInfo.cs:105-130` |
| `ModulesToLoadAfterThis/Module@Id` | list | no | empty list | soft "construct me before these" pin; unknown ids are stored and never matched | `ModuleInfo.cs:131-139` |
| `IncompatibleModules/Module@Id` | list | no | empty list | ids that must not be enabled alongside this module; neither TAOM manifest uses it | `ModuleInfo.cs:140-148` |
| `SubModules/SubModule` | list | no | none (the method returns) | one `SubModuleInfo` per entry; a throw inside one entry is caught and an empty entry is still added | `ModuleInfo.cs:149-166` |

**`DependedModuleMetadatas` is absent from that table because the engine never parses it.**
`LoadWithFullPath` has no branch for the element and the string appears nowhere in the decompile,
so it is a BUTR and BLSE launcher convention only. TAOM writes it as a version-pinned mirror of
`DependedModules`, which is the element the game itself honours.

<!-- engine-table type="TaleWorlds.ModuleManager.SubModuleInfo" file="Platform/TaleWorlds.ModuleManager/TaleWorlds.ModuleManager/SubModuleInfo.cs" method="LoadFrom" inert="" -->

| Attribute | Type | Required | Default when absent | What it does | Read at (file:line) |
|---|---|---|---|---|---|
| `Name@value` | string | yes | throws; caught per entry at `ModuleInfo.cs:156-164` | label for this entry point | `SubModuleInfo.cs:51` |
| `DLLName@value` | file name | yes | throws, as above | the assembly to load. Existence is probed at the literal path `bin\Win64_Shipping_Client\<DLLName>` (`SubModuleInfo.cs:54-57`); the real load path is `bin/<Common.ConfigName>/` (`Module.cs:1044-1045`), and `Common.ConfigName` is the name of the process's current working directory (`Common.cs:37`) | `SubModuleInfo.cs:52` |
| `SubModuleClassType@value` | fully qualified type name | yes | throws, as above | the `MBSubModuleBase` subclass to construct; a typo is a load-time failure, never a compile error | `SubModuleInfo.cs:65` |
| `Assemblies/Assembly@value` | list of file names | no | empty list | extra DLLs loaded from the same `bin/` folder before the entry's own DLL (`Module.cs:1048-1052`) | `SubModuleInfo.cs:66-74` |
| `Tags/Tag@key` + `@value` | key from `SubModuleTags` (`SubModuleInfo.cs:12-21`): `RejectedPlatform`, `ExclusivePlatform`, `DedicatedServerType`, `IsNoRenderModeElement`, `DependantRuntimeLibrary`, `PlayerHostedDedicatedServer`, `EngineType` | no | none | a key the enum does not know is dropped by `Enum.TryParse`; `DedicatedServerType` with any value other than `none` marks the DLL as TW-certified | `SubModuleInfo.cs:75-92` |

Two consequences of the second table matter for this module. The `DumpXML`, `LoaderFilter` and
`LoaderSubModuleOrder` keys that TAOM's manifest carries (`Dependencies/_Module/SubModule.xml:156,
231-233`) are not `SubModuleTags` members, so the engine drops them at `SubModuleInfo.cs:83`; they are
read by UIExtenderEx and the BUTR module loader from the XML themselves, which this chapter did not
trace. And the DLL existence probe is hardcoded to `Win64_Shipping_Client`, so a dedicated server
needs the same files mirrored into `bin/Win64_Shipping_Server/`, which the
`MirrorWin64ShippingClientToServer` target does (`Dependencies/TAOM.Dependencies.csproj:115-124`).

**What `<DependedModules>` does in singleplayer, and what it does not.** `Module.Initialize` passes
the launcher's saved id list to `ModuleHelper.InitializeModules` (`Module.cs:261`), which inserts each
found module into a dictionary in that order (`ModuleHelper.cs:85-100`); `GetModules()` returns that
dictionary's values (`ModuleHelper.cs:178-189`) and `LoadSubModules` walks them in that order
(`Module.cs:266-267`). Inside `InitializeModules` the parsed `DependedModules` are used for one thing
only: stamping a change-set number onto entries that name an official module (`ModuleHelper.cs:101-110`).
The only topological sort in the engine, `GetSortedModules` (`ModuleHelper.cs:271-280`, fed by
`GetDependentModulesOf` at `:252-269`), is called from `CustomBattleServer.cs:208` and
`LobbyClient.cs:474`, both multiplayer. So in singleplayer `<DependedModules>` is the launcher's
"grey this mod out if a dependency is missing" gate plus a documentation contract; it is not the thing
that orders XML merging. The launcher assembly that performs the greying is not in the shipping-client
decompile tree, so that half is cited from [dr3-maintenance.md](../migration/dr3-maintenance.md) lines
224-227 rather than verified here.

## The version pairing (#371)

Issue #371 is the release that shipped bind-posed characters. `Main/TAOM.csproj:89-91` resolves
Harmony and UIExtenderEx through `TAOM.Dependencies.dll`, so a new `TAOM.dll` run against a stale
`TAOM.Dependencies.dll` fails at the member level while patches apply, the HeroRace preview patches
never attach, and every character renders in bind pose (`Main/_Module/SubModule.xml:15-22`,
[release-process.md](../reference/release-process.md) lines 53-57). The root cause was that both
assemblies carried a frozen `AssemblyVersion` on every build (`TAOM 2.0.0.0`,
`TAOM.Dependencies 0.1.0.0`), so .NET bound any pair without complaint (`Directory.Build.props:8-16`).
The fix kept `AssemblyVersion` frozen (changing it alters binding identity for no benefit) and stamps
`InformationalVersion` as `build.yyyyMMdd-HHmmssZ` on each build, which both modules log at startup
(`Directory.Build.props:18-24`).

The release rule written in [release-process.md](../reference/release-process.md) lines 47-51 names
three fields:

| File | Field | When |
|---|---|---|
| `Main/_Module/SubModule.xml` | `<Version value="v2.0.X" />` | every release |
| `Dependencies/_Module/SubModule.xml` | `<Version value="v2.0.Y" />` | only when the Dependencies assembly changed |
| `Main/_Module/SubModule.xml` | `<DependedModuleMetadata id="TAOM.Dependencies" ... version="v2.0.Y" />` | must equal the line above |

**The third field is not in the file today.** `rg -n 'TAOM.Dependencies' Main/_Module/SubModule.xml`
returns one hit, line 15, inside a comment. <!-- measured: rg -n 'TAOM.Dependencies' Main/_Module/SubModule.xml 2026-09-05 -->
`git show cc1713eb -- Main/_Module/SubModule.xml` shows that commit (2026-08-11, a cultures commit
whose message does not mention it) removed these two lines:

```xml
    <DependedModule Id="TAOM.Dependencies" />
    <DependedModuleMetadata id="TAOM.Dependencies" order="LoadBeforeThis" version="v2.0.6" />
```

The #371 comment that introduced them survives at `Main/_Module/SubModule.xml:15-22` and still says
"`<DependedModule>` is the engine-honoured element ... so this is what actually blocks a broken
pairing", describing an element that is no longer below it. The `/release` skill's gate
(`.claude/skills/release/SKILL.md:51-57`) greps for `id="TAOM.Dependencies"[^>]*version=` in that file
and so matches nothing. As shipped, neither manifest blocks a mismatched TAOM and TAOM.Dependencies
pair. Whether the deletion was intended is an open question for the orchestrator (recorded at the end
of this chapter); a modder following the release rule should restore both lines, then cut the release
with `/release`, which is the named tool for the bump and is not run from this chapter.

Two facts about the numbers. The Dependencies `<Version>` is `v2.0.6` (`Dependencies/_Module/SubModule.xml:6`)
while Main's is `v2.0.28` (`Main/_Module/SubModule.xml:6`); the two are not meant to match, and
`v2.0.6` is not a phantom Main version ([release-process.md](../reference/release-process.md) line
152). The engine target is pinned separately: `NativeConstraint_MatchesPinnedGameVersion` asserts that
Main's `<DependedModuleMetadata id="Native" version="v1.4.8.*" />` equals
`.claude/pinned-game-version.txt` plus `.*`
(`TAOM.Tests/Infrastructure/Dependencies/BundledDependencyManifestTests.cs:200-224`); the pin file
reads `v1.4.8`. <!-- measured: cat .claude/pinned-game-version.txt 2026-09-05 -->

## Vendored DLLs, the allowlist and THIRD-PARTY-LICENSES

There are two vendored-binary folders in the repo, and they have different rules.

**`Dependencies/_Module/bin/Win64_Shipping_Client/`** holds 39 files on disk, of which 36 are tracked
in git; the 3 untracked ones are `System.Runtime.CompilerServices.Unsafe.dll`,
`TAOM.Dependencies.dll` and `TAOM.Dependencies.pdb`, which a build produces.
<!-- measured: ls Dependencies/_Module/bin/Win64_Shipping_Client | wc -l; git ls-files Dependencies/_Module/bin/Win64_Shipping_Client | wc -l 2026-09-05 -->
The three NuGet DLLs (`0Harmony.dll`, `Bannerlord.UIExtenderEx.dll`, `MCMv5.dll`) are not in that
folder at all; they arrive at build time and the live module's `bin/Win64_Shipping_Client/` holds 42
files, the same count as its `bin/Win64_Shipping_Server/` mirror.
<!-- measured: ls "<game>/Modules/TAOM.Dependencies/bin/Win64_Shipping_Client" | wc -l; ls ".../bin/Win64_Shipping_Server" | wc -l 2026-09-05 -->
The vendored set is 6 `Bannerlord.ButterLib.Implementation.1.4.N.dll` and 6
`Bannerlord.MBOptionScreen.v1.4.N.dll` files (`1.4.0` through `1.4.5`) plus the 6 `BUTR.CrashReport*`
files. <!-- measured: ls Dependencies/_Module/bin/Win64_Shipping_Client | rg -c 'ButterLib\.Implementation\.1\.4\.[0-9]+\.dll' (and the MBOptionScreen and BUTR.CrashReport patterns) 2026-09-05 -->
Their versions, read from the files: `Bannerlord.ButterLib.dll` FileVersion `2.11.0.0`,
`MCM.UI.Adapter.MCMv5.dll` `5.12.1.0`, `Bannerlord.ModuleLoader.Bannerlord.MBOptionScreen.dll`
`1.0.1.50`, `BUTR.CrashReport.dll` `14.0.0.99`.
<!-- measured: [Diagnostics.FileVersionInfo]::GetVersionInfo(<dll>).FileVersion over the four files 2026-09-05 -->

The `.gitignore` is what makes this folder committable. The top-level `bin/` rule matches everywhere,
so lines 44-45 un-ignore the two parent folders, line 51 re-ignores everything inside
`Win64_Shipping_Client/`, and lines 52-64 carry 13 `!`-prefixed allow patterns, one per DLL family
(`.gitignore:36-64`). <!-- measured: sed -n '52,64p' .gitignore | rg -c '^!' 2026-09-05 -->
A new vendored DLL that is not added to that list is silently never committed
([dr3-maintenance.md](../migration/dr3-maintenance.md) line 123). Where each family comes from is the
table at [dr3-maintenance.md](../migration/dr3-maintenance.md) lines 48-55: Steam Workshop item
`2859232415` (ButterLib, its implementations, the CrashReport family, the Microsoft, Serilog and
`System.*` companions) and `2859238197` (the MBOptionScreen set, the module loader, the adapter).

**`Main/_Module/bin/Win64_Shipping_Client/`** is a different pool with its own allowlist, and it is
exactly two files: `MinHook.x64.dll` and `TAOM.NativeSkinFixes.dll` (`.gitignore:73-80`,
`CLAUDE.md:179`). <!-- measured: git ls-files Main/_Module/bin | wc -l 2026-09-05 -->
`MCMv5.dll` is never vendored there: MCM's runtime comes from this module and the compile-time
reference comes from the `Bannerlord.MCM` NuGet with `IncludeAssets="compile"`
(`Main/TAOM.csproj:99`, `.gitignore:69-70`, [dr3-maintenance.md](../migration/dr3-maintenance.md)
line 121).

**One pin that looks like a typo and is not.** `Dependencies/TAOM.Dependencies.csproj:47` pins
`System.Runtime.CompilerServices.Unsafe` to package `4.5.3`, whose assembly version is `4.0.4.1`. The
vendored `System.Memory.dll` (assembly `4.0.1.1`) binds to that exact version under .NET Framework's
strict versioning, a module folder cannot carry a binding redirect, and ButterLib's first
`Trace.WriteLine` runs `System.Memory`'s static constructor. With `6.0.0` in the folder instead,
ButterLib died with a `TypeInitializationException` on every tick, which presented as Mod Options
hanging on open (`Dependencies/TAOM.Dependencies.csproj:40-46`,
`TAOM.Tests/Infrastructure/DependenciesPairingTests.cs:8-18`). Both the repo copy and the live copy
of that DLL read assembly version `4.0.4.1` today.
<!-- measured: [Reflection.AssemblyName]::GetAssemblyName(<dll>).Version on the repo and live Unsafe DLLs 2026-09-05 -->

**The licence notice is mandatory, not decoration.** The provenance rule says anything landing under
`*/_Module/bin/**` that is not TAOM-built needs a `redistributed` row in the register and an entry in
that module's `THIRD-PARTY-LICENSES.txt` ([provenance rule](../../.claude/rules/provenance.md), the
"Redistributing a binary means reproducing its notice" row). The register rows for this module are
Lib.Harmony, the BUTR stack, .NET Foundation, Serilog and BetaDeps
([provenance-register.md](../reference/provenance-register.md) lines 63-66 and 77). The notice file
names each binary family with its exact version, holder, licence and upstream URL, then the full MIT
and Apache-2.0 texts, then a closing paragraph scoping what is TAOM's own work
(`Dependencies/_Module/THIRD-PARTY-LICENSES.txt`, 124 lines). <!-- measured: wc -l Dependencies/_Module/THIRD-PARTY-LICENSES.txt 2026-09-05 -->
`ThirdPartyLicenses_NameTheActuallyShippedVersions` asserts that the versions named in it match the
DLLs on disk and the csproj pins
(`TAOM.Tests/Infrastructure/Dependencies/BundledDependencyManifestTests.cs:228-255`). The main module
has its own notice at `Main/_Module/THIRD-PARTY-LICENSES.txt`, which defers to this one for the BUTR
stack (its lines 16-18).

## The co-op module manifest

`Dependencies/_Module/coop-modules.txt` (31 lines) is a player-editable list of launcher ids that mean
"a co-op mod is running". <!-- measured: wc -l Dependencies/_Module/coop-modules.txt 2026-09-05 -->
It has two sections. `[modules]` lists 3 ids, `BannerlordTogether`, `BattleLinkMPClient` and `Coop`;
`[harmony-owner-prefixes]` is empty by design (`Dependencies/_Module/coop-modules.txt:10-31`).
<!-- measured: awk over the [modules] section of Dependencies/_Module/coop-modules.txt 2026-09-05 -->
When any listed module is active, PatchShield stops unpatching third-party Harmony patches and
SaveShield rethrows save and load failures instead of swallowing them (`coop-modules.txt:11-13`,
[coop-interop.md](../features/coop-interop.md) lines 85-87).

The parser, `Dependencies/Foundation/CoopModuleList.cs`, seeds the result from the compiled defaults
first and only ever adds to it (`CoopModuleList.cs:47-50, 65-67`): a corrupt or hostile file can add
ids but never remove one, so it can never unprotect the BUTR/MCM stack. The compiled defaults are the
same three ids (`Dependencies/Foundation/CoopPresence.cs:53-63`). Matching is exact, case-insensitive
equality, which is why `Coop` had to be added by name: nothing about `BannerlordTogether` matches it,
and BannerlordCoop was invisible to every shield until 2026-08-01
([coop-interop.md](../features/coop-interop.md) lines 71-76).

**Adding an id is four coupled edits in three files**, or a shield silently goes inert:
`CoopPresence.CompiledModuleDefaults`, `coop-modules.txt`, and `<ModulesToLoadAfterThis>` in both
`SubModule.xml` manifests ([coop-interop.md](../features/coop-interop.md) lines 80-83). Two tests pin
that: `ModulesToLoadAfterThis_ContainsEveryCoopModuleIdFromTheShippedConfig` checks both manifests
against the file (`BundledDependencyManifestTests.cs:265-301`), and
`CompiledModuleDefaults_MatchesTheShippedCoopModulesFile` checks the compiled array against the file
(`TAOM.Tests/Infrastructure/Dependencies/AssemblyRedirectListTests.cs:107-123`). The pin has to be in
`<ModulesToLoadAfterThis>` and not only in `<DependedModuleMetadatas>` because the engine never reads
the latter (`BundledDependencyManifestTests.cs:273-276`).

## The alias stub modules

TAOM.Dependencies registers itself as `TAOM.Dependencies`, so a third-party mod that declares
`<DependedModule Id="Bannerlord.Harmony"/>` sees no module by that id and is greyed out in the vanilla
launcher (`Stubs/Bannerlord.Harmony/_Module/SubModule.xml:5-10`). The answer is four passive stub
modules under `Stubs/`, one folder each for `Bannerlord.Harmony`, `Bannerlord.UIExtenderEx`,
`Bannerlord.ButterLib` and `Bannerlord.MBOptionScreen`, each holding a single `_Module/SubModule.xml`.
<!-- measured: ls -d Stubs/*/ | wc -l 2026-09-05 -->
Each stub declares the standard BUTR id, `<DefaultModule value="true"/>`,
`<DependedModule Id="TAOM.Dependencies"/>` so the real DLLs construct first, and one `<SubModule>`
pointing at `TAOM.Dependencies.AliasStubSubModule` (`Stubs/Bannerlord.Harmony/_Module/SubModule.xml:42-72`).

**The v99 version rule.** Stub versions are `v2.4.99.0` (Harmony), `v2.13.99.0` (UIExtenderEx),
`v2.11.99.0` (ButterLib) and `v5.12.99.0` (MBOptionScreen).
<!-- measured: rg -n '<Version value=' Stubs/*/_Module/SubModule.xml 2026-09-05 -->
`X.Y` is the minor of the version actually shipped (the csproj pin for the three NuGet packages, the
vendored DLL's own FileVersion for ButterLib) and `.99.0` satisfies any reasonable `vX.Y.*` lower
bound a third-party mod declares, without claiming a major jump. A minor bump needs a stub edit; a
patch bump does not ([dr3-maintenance.md](../migration/dr3-maintenance.md) lines 231-238). Both
derivations are asserted by `StubVersions_NuGetPinnedDeps_TrackPackageMinorAs99` and
`StubVersion_ButterLib_TracksVendoredDllMinorAs99` (`BundledDependencyManifestTests.cs:122-150`). The
stub's own comment warns that a legacy mod pinning a strict `v2.4.0.*` wildcard will not match
`v2.4.99.0` (`Stubs/Bannerlord.Harmony/_Module/SubModule.xml:19-20`).

**Why the stub has a SubModule entry at all.** An empty `<SubModules />` makes BLSE treat the stub as
metadata-only and its drag-to-reorder breaks with "Missing Bannerlord.Harmony a0.0.0.0 to a0.0.0.0"
(`Stubs/Bannerlord.Harmony/_Module/SubModule.xml:27-33`). `AliasStubSubModule` therefore exists, and
every call in its constructor is wrapped in `TrySwallow` because an uncaught exception in a stub
constructor breaks reordering for every other mod (`Dependencies/AliasStubSubModule.cs:29-52, 66-80`).

**Where they deploy, and the problem with it.** `DeployTAOMDependenciesStubs` copies
`Stubs/**\SubModule.xml` into the game's `Modules/` folder preserving the relative directory
(`Dependencies/TAOM.Dependencies.csproj:90-103`), so each file lands at
`Modules/<Id>/_Module/SubModule.xml`. On this machine each of the four deployed stub folders contains
exactly that one file and nothing at its root. <!-- measured: find "<game>/Modules/Bannerlord.Harmony" -type f (and the three siblings) 2026-09-05 -->
Those deployed folders live in the game install, not the repo; a module reinstall reverts hand edits,
so land a repo-side change and redeploy rather than editing them in place.
`ModuleHelper.GetPhysicalModules` looks for `SubModule.xml` at the folder root and skips the folder
otherwise (`ModuleHelper.cs:327-331`), and vanilla `Native/SubModule.xml` sits at the root. So
the vanilla engine does not see the four stubs at all. That matches the observation recorded at
`Dependencies/SubModule.cs:205-211` that the launcher never constructs `AliasStubSubModule`, and it
means the "auto-tick so third-party mods are not greyed out" purpose in
[dr3-maintenance.md](../migration/dr3-maintenance.md) line 227 is not what the vanilla engine does with
the files as deployed. Whether BLSE scans `_Module/` was not checked here; it is listed as an open
question. The `_Module/` destination is documented as intended at
[dr3-maintenance.md](../migration/dr3-maintenance.md) line 219.

## Worked example

The Dependencies manifest is 237 lines. <!-- measured: wc -l Dependencies/_Module/SubModule.xml 2026-09-05 -->
Its comment blocks at lines 11-20, 58-68, 86-140 and 160-162 contain characters the handbook lint
forbids, so they are not reproduced; the five slices below are every non-comment line of the file,
copied verbatim, and the omitted comments are summarised after each slice.

<!-- example file="Dependencies/_Module/SubModule.xml" id="TAOM.Dependencies" -->
Lines 1-10, the header:

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

1. `<Id value="TAOM.Dependencies" />` (line 4) must equal the folder name under `Modules/`; every
   other manifest's `<DependedModule Id>` matches on this string, lowercased (`ModuleHelper.cs:37`).
2. `<Version value="v2.0.6" />` (line 6) is the Dependencies half of the #371 pairing; bump it only
   when the assembly changes, and only through `/release`.
3. `<DependedModules />` (line 10) is empty on purpose. This module must be able to construct before
   `Native`; listing `Native` here would pull it below the vanilla stack and defeat the static
   constructor redirect ([coop-interop.md](../features/coop-interop.md) lines 108-124). The comment at
   lines 11-20 says the `<ModulesToLoadAfterThis>` list was expanded "from 5 vanilla entries to 26"
   in May 2026; the block now holds 35 ids. <!-- measured: sed -n '21,77p' Dependencies/_Module/SubModule.xml | rg -c '<Module Id=' 2026-09-05 -->

<!-- example file="Dependencies/_Module/SubModule.xml" id="TAOM.Dependencies" -->
Lines 21-57, the vanilla stack, the four stubs and the consumer mods known to bundle their own BUTR
copies:

```xml
	<ModulesToLoadAfterThis>
		<!-- TaleWorlds stack -->
		<Module Id="Native" />
		<Module Id="SandBoxCore" />
		<Module Id="Sandbox" />
		<Module Id="StoryMode" />
		<Module Id="CustomBattle" />
		<!-- BUTR alias stubs (also DependedModule us via Stubs/, redundant but explicit) -->
		<Module Id="Bannerlord.Harmony" />
		<Module Id="Bannerlord.UIExtenderEx" />
		<Module Id="Bannerlord.ButterLib" />
		<Module Id="Bannerlord.MBOptionScreen" />
		<!-- Consumer mods known to bundle their own MCMv5 / 0Harmony / UIExtenderEx /
		     ButterLib copies. Pinning order here costs nothing if they're not installed. -->
		<Module Id="DismembermentPlus" />
		<Module Id="XorberaxLegacy" />
		<Module Id="DynaCulture" />
		<Module Id="ArtemsLivelyAnimations" />
		<Module Id="FluidCombatLite" />
		<Module Id="Fluid_Combat_Lite" />
		<Module Id="BetterSmithing" />
		<Module Id="FasterTime" />
		<Module Id="BanditBlackHole" />
		<Module Id="PerfectFireArrows" />
		<Module Id="IDontCare" />
		<Module Id="ImprovedGarrisons" />
		<Module Id="DistinguishedService" />
		<Module Id="CulturedStartV2" />
		<Module Id="DiplomacyFixes" />
		<Module Id="Diplomacy" />
		<Module Id="CalradiaExpanded" />
		<Module Id="CalradiaExpandedKingdoms" />
		<Module Id="CargoHolds" />
		<Module Id="CrashDoctor" />
		<Module Id="AchievementUnblocker" />
		<Module Id="CREST" />
		<Module Id="Crest" />
```

The comment at lines 58-68 explains the co-op entries that follow: BannerlordTogether ships its own
`0Harmony.dll` at the same version TAOM deploys, Harmony's patch registry is static per assembly
instance, and if its copy wins the process slot TAOM ends up with two Harmony instances whose
`GetAllPatchedMethods()` cannot see each other, which blinds PatchShield and the HarmonyCensus report.

<!-- example file="Dependencies/_Module/SubModule.xml" id="TAOM.Dependencies" -->
Lines 69-84, the three co-op ids and the BLSE-only metadata mirror:

```xml
		<Module Id="BannerlordTogether" />
		<Module Id="BattleLinkMPClient" />
		<!-- BannerlordCoop (Workshop 3770450698), launcher id "Coop". Same reasoning as the two
		     above and then some: it bundles 0Harmony 2.4.2.0 (byte-identical to ours), plus
		     Mono.Cecil / MonoMod / Serilog 4.x. Constructing TAOM.Dependencies first lets the
		     AssemblyResolve redirect settle before its copies are touched.
		     Internals: docs/research/bannerlordcoop-internals.md -->
		<Module Id="Coop" />
	</ModulesToLoadAfterThis>
	<DependedModuleMetadatas>
		<DependedModuleMetadata id="Native" order="LoadAfterThis" optional="true" />
		<DependedModuleMetadata id="SandBoxCore" order="LoadAfterThis" optional="true" />
		<DependedModuleMetadata id="Sandbox" order="LoadAfterThis" optional="true" />
		<DependedModuleMetadata id="StoryMode" order="LoadAfterThis" optional="true" />
		<DependedModuleMetadata id="CustomBattle" order="LoadAfterThis" optional="true" />
	</DependedModuleMetadatas>
```

4. Lines 69, 70 and 76 are the 3 ids from `coop-modules.txt`, and the test at
   `BundledDependencyManifestTests.cs:265-301` fails if one is missing here or in Main's manifest.
5. Lines 79-83 are 5 `<DependedModuleMetadata>` rows, `LoadAfterThis` and `optional`, for the BUTR
   and BLSE launchers. <!-- measured: rg -c '<DependedModuleMetadata ' Dependencies/_Module/SubModule.xml 2026-09-05 -->
   The engine never reads them; they restate lines 23-27 for launchers that do.

Line 85 opens `<SubModules>` and lines 86-140 are the block comment that records where each pin
lives (the csproj for NuGet, the `bin/` folder for vendored files, Main's manifest for the engine
target, the test class for enforcement), the reason the six `BUTR.CrashReport*` DLLs are not optional,
how the BUTR meta-loader picks the highest `Implementation.1.4.N.dll` whose suffix is at or below the
running engine, and the construction order the seven entries follow.

<!-- example file="Dependencies/_Module/SubModule.xml" id="TAOM.Dependencies" -->
Lines 142-158, the note that Harmony has no SubModule class, and the first entry:

```xml
		<!-- Note: 0Harmony.dll (Lib.Harmony NuGet) has NO SubModule class because
		     HarmonyLib is a pure managed library. Mods reference HarmonyLib types
		     directly via `using HarmonyLib;` and instantiate `new Harmony(id)` at
		     OnSubModuleLoad. Bannerlord's CLR auto-loads 0Harmony.dll when first
		     HarmonyLib type is touched; no SubModule registration needed. -->

		<!-- UIExtenderEx bootstrap. Bannerlord.UIExtenderEx.dll is from
		     Bannerlord.UIExtenderEx NuGet (deployed automatically). -->
		<SubModule>
			<Name value="UIExtenderEx" />
			<DLLName value="Bannerlord.UIExtenderEx.dll" />
			<SubModuleClassType value="Bannerlord.UIExtenderEx.SubModule" />
			<Assemblies />
			<Tags>
				<Tag key="DumpXML" value="false" />
			</Tags>
		</SubModule>
```

<!-- example file="Dependencies/_Module/SubModule.xml" id="TAOM.Dependencies" -->
Lines 163-237, the remaining six entries (the comment at lines 160-162 says entry two is a thin stub
since NativeSkinFixes initialisation moved into `TAOM.dll` on 2026-05-26):

```xml
		<SubModule>
			<Name value="TAOM.Dependencies" />
			<DLLName value="TAOM.Dependencies.dll" />
			<SubModuleClassType value="TAOM.Dependencies.SubModule" />
			<Assemblies />
			<Tags />
		</SubModule>

		<!-- ButterLib core. -->
		<SubModule>
			<Name value="ButterLib" />
			<DLLName value="Bannerlord.ButterLib.dll" />
			<SubModuleClassType value="Bannerlord.ButterLib.ButterLibSubModule" />
			<Assemblies>
				<Assembly value="Microsoft.Bcl.HashCode.dll" />
				<Assembly value="Serilog.dll" />
				<Assembly value="Serilog.Extensions.Logging.dll" />
				<Assembly value="Serilog.Sinks.File.dll" />
			</Assemblies>
			<Tags>
				<Tag key="DedicatedServerType" value="none" />
				<Tag key="IsNoRenderModeElement" value="false" />
			</Tags>
		</SubModule>

		<!-- ButterLib implementation loader. Picks the correct versioned
		     Implementation.1.4.x.dll based on installed Bannerlord version. -->
		<SubModule>
			<Name value="ButterLib Implementation Loader" />
			<DLLName value="Bannerlord.ButterLib.dll" />
			<SubModuleClassType value="Bannerlord.ButterLib.ImplementationLoaderSubModule" />
			<Assemblies />
			<Tags>
				<Tag key="DedicatedServerType" value="none" />
				<Tag key="IsNoRenderModeElement" value="false" />
			</Tags>
		</SubModule>

		<!-- MCM (Mod Configuration Menu v5). MCMv5.dll is from Bannerlord.MCM NuGet. -->
		<SubModule>
			<Name value="MCMv5" />
			<DLLName value="MCMv5.dll" />
			<SubModuleClassType value="MCM.MCMSubModule" />
			<Assemblies />
			<Tags>
				<Tag key="DedicatedServerType" value="none" />
				<Tag key="IsNoRenderModeElement" value="false" />
			</Tags>
		</SubModule>
		<SubModule>
			<Name value="MCMv5 Basic Implementation" />
			<DLLName value="MCMv5.dll" />
			<SubModuleClassType value="MCM.Internal.MCMImplementationSubModule" />
			<Assemblies />
			<Tags>
				<Tag key="DedicatedServerType" value="none" />
				<Tag key="IsNoRenderModeElement" value="false" />
			</Tags>
		</SubModule>

		<!-- Bannerlord Module Loader for MBOptionScreen. Picks correct
		     MBOptionScreen.v1.4.x.dll based on installed Bannerlord version.
		     This is the UI screen that renders mod options. Do NOT rename. -->
		<SubModule>
			<Name value="Bannerlord Module Loader" />
			<DLLName value="Bannerlord.ModuleLoader.Bannerlord.MBOptionScreen.dll" />
			<SubModuleClassType value="Bannerlord.ModuleLoader.Bannerlord_MBOptionScreen" />
			<Tags>
				<Tag key="LoaderFilter" value="Bannerlord.MBOptionScreen.*.dll" />
				<Tag key="LoaderSubModuleOrder" value="MCM.UI.MCMUIAdapterSubModule" />
				<Tag key="LoaderSubModuleOrder" value="MCM.UI.MCMUISubModule" />
			</Tags>
		</SubModule>
	</SubModules>
</Module>
```

The seven `<SubModule>` entries, in construction order: <!-- measured: rg -c '^\s*<SubModule>' Dependencies/_Module/SubModule.xml 2026-09-05 -->

| # | `Name` | `DLLName` | `SubModuleClassType` | Lines | What it is |
|---|---|---|---|---|---|
| 1 | `UIExtenderEx` | `Bannerlord.UIExtenderEx.dll` (NuGet) | `Bannerlord.UIExtenderEx.SubModule` | 150-158 | boots UIExtenderEx; `DumpXML=false` is a UIExtenderEx debug switch the engine drops |
| 2 | `TAOM.Dependencies` | `TAOM.Dependencies.dll` (built) | `TAOM.Dependencies.SubModule` | 163-169 | this module's own class: the redirect, the Harmony guards and the shields (`Dependencies/SubModule.cs`) |
| 3 | `ButterLib` | `Bannerlord.ButterLib.dll` (vendored) | `Bannerlord.ButterLib.ButterLibSubModule` | 172-186 | ButterLib core; its 4 `<Assembly>` rows pre-load `Microsoft.Bcl.HashCode` and the three Serilog DLLs <!-- measured: sed -n '176,181p' Dependencies/_Module/SubModule.xml | rg -c '<Assembly ' 2026-09-05 --> |
| 4 | `ButterLib Implementation Loader` | `Bannerlord.ButterLib.dll` (same file) | `Bannerlord.ButterLib.ImplementationLoaderSubModule` | 190-199 | picks the `Implementation.1.4.N.dll` for the running engine |
| 5 | `MCMv5` | `MCMv5.dll` (NuGet) | `MCM.MCMSubModule` | 202-211 | the MCM API |
| 6 | `MCMv5 Basic Implementation` | `MCMv5.dll` (same file) | `MCM.Internal.MCMImplementationSubModule` | 212-221 | MCM's default implementation |
| 7 | `Bannerlord Module Loader` | `Bannerlord.ModuleLoader.Bannerlord.MBOptionScreen.dll` (vendored) | `Bannerlord.ModuleLoader.Bannerlord_MBOptionScreen` | 226-235 | the meta-loader that picks `Bannerlord.MBOptionScreen.v1.4.N.dll` via `LoaderFilter`, then boots the two classes named by `LoaderSubModuleOrder`; the comment says do not rename |

Nothing asserts the seven class names against the upstream BUTR manifests; the tests check versions
and file sets only. A rename upstream would be a silent no-op at load
([dr3-maintenance.md](../migration/dr3-maintenance.md) lines 191-204 tells you to read the new
upstream `SubModule.xml` when a major version lands).

<!-- example file="Main/_Module/SubModule.xml" id="TAOM" -->
The `<DependedModules>` block of the main module's manifest, lines 10-14. Line 23 closes the element;
lines 15-22 are the #371 comment quoted in the pairing section above, not reproduced because line 15
carries a forbidden character:

```xml
  <DependedModules>
    <DependedModule Id="Native" />
    <DependedModule Id="SandBoxCore" />
    <DependedModule Id="Sandbox" />
    <DependedModule Id="CustomBattle" />
```

6. These 4 rows are the vanilla stack TAOM needs. <!-- measured: rg -c '<DependedModule ' Main/_Module/SubModule.xml 2026-09-05 -->
   The row the #371 comment describes, `<DependedModule Id="TAOM.Dependencies" />`, sat between line
   14 and the comment until `cc1713eb` removed it.

<!-- example file="Stubs/Bannerlord.Harmony/_Module/SubModule.xml" id="Bannerlord.Harmony" -->
The Harmony alias stub, lines 40-73 (its 39-line header comment is summarised in the stub section):

```xml
<Module xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance"
        xsi:noNamespaceSchemaLocation="https://raw.githubusercontent.com/BUTR/Bannerlord.XmlSchemas/master/SubModule.xsd">
    <Id value="Bannerlord.Harmony" />
    <Name value="Harmony (provided by TAOM.Dependencies)" />
    <Version value="v2.4.99.0" />
    <!-- DefaultModule="true" auto-ticks the stub on first launch so third-party
         mods depending on Bannerlord.Harmony become toggleable immediately. -->
    <DefaultModule value="true" />
    <!-- BetaDeps parity: both old + new format tags for maximum launcher compat.
         MultiplayerModule=true covers the rare MP mod that depends on Harmony
         (closes Agent 5 Trace 9 gap from the 2026-05-25 deep-review). -->
    <SingleplayerModule value="true" />
    <MultiplayerModule value="true" />
    <Official value="false" />
    <ModuleCategory value="Singleplayer" />
    <ModuleType value="Community" />
    <Url value="" />

    <DependedModules>
        <DependedModule Id="TAOM.Dependencies" />
    </DependedModules>
    <DependedModuleMetadatas>
        <DependedModuleMetadata id="TAOM.Dependencies" order="LoadBeforeThis" />
    </DependedModuleMetadatas>

    <SubModules>
        <SubModule>
            <Name value="Bannerlord.Harmony Alias Stub" />
            <DLLName value="TAOM.Dependencies.dll" />
            <SubModuleClassType value="TAOM.Dependencies.AliasStubSubModule" />
            <Tags />
        </SubModule>
    </SubModules>
</Module>
```

7. `<Version value="v2.4.99.0" />` (line 44) tracks the minor of the `Lib.Harmony` pin.
8. `<SingleplayerModule>`, `<MultiplayerModule>` and `<Official>` (lines 51-53) are older spellings
   kept for launcher compatibility; `LoadWithFullPath` reads none of them (first engine table).

<!-- excerpt file="Dependencies/_Module/coop-modules.txt" -->
The `[modules]` section of the co-op list, lines 10-15 (line 18 adds `Coop` after a two-line comment):

```
[modules]
# Launcher module ids that mean "a co-op mod is running". When any of these is active,
# TAOM stops PatchShield from unpatching third-party Harmony patches and makes SaveShield
# rethrow save/load failures instead of swallowing them.
BannerlordTogether
BattleLinkMPClient
```

<!-- excerpt file="Dependencies/_Module/THIRD-PARTY-LICENSES.txt" -->
The first four notice paragraphs, lines 8-32:

```
0Harmony.dll  --  Lib.Harmony 2.4.2
Copyright (c) Andreas Pardeike
Licensed under the MIT License.
Source: https://github.com/pardeike/Harmony

Bannerlord.UIExtenderEx.dll  --  Bannerlord.UIExtenderEx 2.13.2
Copyright (c) BUTR (Bannerlord Unofficial Tools & Resources)
Licensed under the MIT License.
Source: https://github.com/BUTR/Bannerlord.UIExtenderEx

Bannerlord.ButterLib.dll + Bannerlord.ButterLib.Implementation.1.4.*.dll  --  ButterLib 2.11.0
Copyright (c) BUTR
Licensed under the MIT License.
Source: https://github.com/BUTR/Bannerlord.ButterLib

MCMv5.dll  --  Bannerlord.MCM 5.12.1
Copyright (c) BUTR
Licensed under the MIT License.
Source: https://github.com/BUTR/Bannerlord.MCM

Bannerlord.MBOptionScreen.v1.4.*.dll, Bannerlord.ModuleLoader.Bannerlord.MBOptionScreen.dll,
MCM.UI.Adapter.MCMv5.dll  --  Bannerlord.MBOptionScreen 5.12.1
Copyright (c) BUTR
Licensed under the MIT License.
Source: https://github.com/BUTR/Bannerlord.MCM
```

9. The version in each heading is what the test at `BundledDependencyManifestTests.cs:228-255`
   compares against the csproj pin or the DLL's FileVersion. Change one and the other must follow.

<!-- excerpt file="Dependencies/TAOM.Dependencies.csproj" -->
The three runtime pins, lines 70-72, and the Unsafe pin with its comment, lines 40-47:

```xml
		<PackageReference Include="Lib.Harmony" Version="2.4.2" />
		<PackageReference Include="Bannerlord.UIExtenderEx" Version="2.13.2" />
		<PackageReference Include="Bannerlord.MCM" Version="5.12.1" />
```

```xml
		<!-- 4.5.3 (assembly 4.0.4.1), NOT 6.0.0: the vendored System.Memory 4.0.1.1 binds to
		     Unsafe 4.0.4.1 EXACTLY (.NET Framework strict versioning, no redirect available in a
		     module folder), and ButterLib's first Trace.WriteLine dies in System.Memory's cctor
		     when only 6.0.0.0 is present (TypeInitializationException every tick; Mod Options
		     hang + teardown NRE were downstream). Nothing else here consumes Unsafe at runtime:
		     0Harmony carries no reference to it (MonoMod ships its own ILHelpers). This is the
		     exact pair upstream ButterLib distributes. Pinned by DependenciesPairingTests. -->
		<PackageReference Include="System.Runtime.CompilerServices.Unsafe" Version="4.5.3" />
```

10. `Main/TAOM.csproj:99, 110-111` pins the same three packages with `IncludeAssets="compile"`;
    `CompilePins_MainAndDependenciesCsproj_MatchExactly` fails the build when they differ
    (`BundledDependencyManifestTests.cs:103-118`).

## Recipes: Create / Update / Never do

### Create a dependencies module from zero

1. Make `Dependencies/` with `_Module/` inside it and `_Module/bin/Win64_Shipping_Client/` inside
   that. Everything under `_Module/` is copied to `Modules/TAOM.Dependencies/` with the `_Module`
   segment stripped; the repo and live `SubModule.xml` are byte-identical.
   <!-- measured: diff --strip-trailing-cr Dependencies/_Module/SubModule.xml "<game>/Modules/TAOM.Dependencies/SubModule.xml" 2026-09-05 -->
2. Write `Dependencies/TAOM.Dependencies.csproj` (126 lines in TAOM's).
   <!-- measured: wc -l Dependencies/TAOM.Dependencies.csproj 2026-09-05 -->
   The parts that matter: `<ModuleId>` and `<ModuleName>` (lines 7-8) name the destination folder;
   the `TaleWorlds.*.dll` reference glob with `<Private>False</Private>` (lines 16-20) references
   engine DLLs without copying them; `<Compile Remove="_Module\bin\**" />` (lines 24-26) keeps
   vendored DLLs out of the compile; `Bannerlord.BuildResources` (lines 36-39) supplies the deploy
   targets; the three pins (lines 70-72) and the Unsafe pin (line 47).
3. Pin the same three packages in `Main/TAOM.csproj` with `IncludeAssets="compile"` (lines 99,
   110-111) and take the `<ProjectReference>` with `Private=False` (lines 89-91).
4. Copy the vendored DLLs from the two Workshop folders into `_Module/bin/Win64_Shipping_Client/`
   following the table at [dr3-maintenance.md](../migration/dr3-maintenance.md) lines 48-55, all six
   `BUTR.CrashReport*` files included.
5. Add one `!Dependencies/_Module/bin/Win64_Shipping_Client/<pattern>` line per family to
   `.gitignore` under the parent un-ignores (`.gitignore:44-64`).
6. Write `_Module/SubModule.xml`: `<Id>`, `<Name>`, `<Version>`, `<DefaultModule value="false"/>`,
   category and type, an empty `<DependedModules />`, a `<ModulesToLoadAfterThis>` naming at least
   `Native`, `SandBoxCore`, `Sandbox`, `StoryMode` and `CustomBattle`, and one `<SubModule>` per
   library class in the order at `Dependencies/_Module/SubModule.xml:150-235`. Read each class name
   from the upstream module's own `SubModule.xml`.
7. Write `Dependencies/SubModule.cs` with the static-constructor `AssemblyResolve` redirect
   (`Dependencies/SubModule.cs:21-30, 123-162`) and, if you keep the shields, the 16 `Foundation/`
   files. <!-- measured: ls Dependencies/Foundation/*.cs | wc -l 2026-09-05 -->
8. Write `_Module/THIRD-PARTY-LICENSES.txt` in the shape of TAOM's: one paragraph per binary family
   with exact version, holder, licence and URL, then the licence texts.
9. Add the four stubs under `Stubs/<Id>/_Module/SubModule.xml` and the `DeployTAOMDependenciesStubs`
   target (`Dependencies/TAOM.Dependencies.csproj:90-103`), knowing the deployed depth problem
   described above.
10. Copy `BundledDependencyManifestTests.cs` (9 tests), `DependenciesPairingTests.cs` (2) and
    `AssemblyRedirectListTests.cs` (5) and point them at your paths; they assert relationships
    between declarations, never version literals (`BundledDependencyManifestTests.cs:20-22`).
    <!-- measured: rg -c '\[TestMethod\]' over the three test files 2026-09-05 -->
11. Build with Bannerlord closed: the deploy writes `0Harmony.dll` and `Bannerlord.ButterLib.dll`
    into the game install and the running game locks them
    ([dr3-maintenance.md](../migration/dr3-maintenance.md) line 40). Then run the smoke test at
    lines 138-157 of that doc: launch, confirm only `TAOM` and `TAOM.Dependencies` are needed,
    confirm a **Mod Options** tab exists and a changed value persists.

Check: `dotnet test TAOM.Tests --filter BundledDependencyManifest`
Takes effect: full game restart
Code: Code changes required in `Dependencies/SubModule.cs` and `Dependencies/TAOM.Dependencies.csproj`

### Update a library

1. **NuGet package** (Harmony, UIExtenderEx, MCM): bump the `Version=` in
   `Dependencies/TAOM.Dependencies.csproj:70-72` and the matching line in `Main/TAOM.csproj:99,
   110-111`. If the bump crosses a minor, also set the matching stub in `Stubs/<Id>/_Module/SubModule.xml`
   to the new minor's `.99.0` (MCM's stub is `Bannerlord.MBOptionScreen`). Run
   `dotnet restore Dependencies/TAOM.Dependencies.csproj`
   ([dr3-maintenance.md](../migration/dr3-maintenance.md) lines 29-38).
2. **Vendored DLL** (ButterLib, MBOptionScreen, their companions): copy the new files from the
   Workshop folder into `Dependencies/_Module/bin/Win64_Shipping_Client/`, add any new family to the
   `.gitignore` allowlist, `git add` the files. Every DLL in a family must come from one release;
   `VendoredButterLibDlls_AllShareOneVersion` and `VendoredMbOptionScreenDlls_AllShareOneVersion` fail
   on a half-finished refresh (`BundledDependencyManifestTests.cs:154-183`).
3. **Engine bump**: set Main's `<DependedModuleMetadata id="Native" version="v<new>.*" />` and
   `.claude/pinned-game-version.txt` together, and re-check the Workshop impl set. On this machine the
   ButterLib and MBOptionScreen Workshop folders each hold 9 versioned DLLs, `1.4.0` through `1.4.8`,
   while the vendored set stops at `1.4.5`; the meta-loader picks the highest suffix at or below the
   running engine, so TAOM on 1.4.8 is running the `1.4.5` implementations.
   <!-- measured: ls "<workshop>/2859232415/bin/Win64_Shipping_Client" | rg -c 'ButterLib\.Implementation\.1\.4\.[0-9]+\.dll' and the 2859238197 MBOptionScreen pattern 2026-09-05 -->
   The `/engine-bump` skill is the named workflow for this and is not run from this chapter.
4. Update `Dependencies/_Module/THIRD-PARTY-LICENSES.txt` so every heading names the version now
   shipped.
5. If the Dependencies assembly changed, bump `Dependencies/_Module/SubModule.xml:6` and restore and
   bump the matching `<DependedModule Id="TAOM.Dependencies" />` and
   `<DependedModuleMetadata id="TAOM.Dependencies" ... version="v2.0.Y" />` rows in
   `Main/_Module/SubModule.xml`, then cut the release with `/release`.
6. Build with the game closed and run the smoke test. `-p:DisableModuleCopy=true` alone does not stop
   the deploy into the game install; add `-p:ModuleId=` to skip all three copy targets
   ([agent-operating-manual.md](../ai-includes/agent-operating-manual.md) lines 49-51).

Check: `dotnet test TAOM.Tests --filter DependenciesPairing`
Takes effect: full game restart
Code: No code changes needed

### Never do

1. **Never vendor `MCMv5.dll` into `Main/_Module/bin/`.** The Main allowlist is exactly
   `MinHook.x64.dll` and `TAOM.NativeSkinFixes.dll` (`CLAUDE.md:179`, `.gitignore:79-80`). A second
   `MCMv5.dll` in the process is what the redirect list was written to fight
   (`Dependencies/SubModule.cs:39-46`).
2. **Never edit the live `bin/` folders by hand.** The next build copies `_Module/**` over them, and
   the server mirror never deletes, so a hand-dropped file survives in `Win64_Shipping_Server/` after
   the client copy is gone ([dr3-maintenance.md](../migration/dr3-maintenance.md) line 135).
3. **Never fill in `<DependedModules />` on this module.** It must be able to construct before
   `Native` (`Dependencies/_Module/SubModule.xml:10`, [coop-interop.md](../features/coop-interop.md)
   lines 108-114).
4. **Never bump one half of the pairing by hand.** Both `v2.0.Y` values move together through
   `/release` ([release-process.md](../reference/release-process.md) lines 56-57).
5. **Never pin `System.Runtime.CompilerServices.Unsafe` above 4.5.3.** The vendored `System.Memory`
   binds to assembly `4.0.4.1` exactly (`Dependencies/TAOM.Dependencies.csproj:40-46`).
6. **Never re-add `Serilog`, `System.Buffers`, `System.Memory`, `System.Numerics.Vectors` or
   `System.Runtime.CompilerServices.Unsafe` to `RedirectedSimpleNames`.** The redirect is version-blind
   and BannerlordCoop ships all five higher; `AssemblyRedirectListTests` fails the build if one comes
   back (`Dependencies/SubModule.cs:65-98`, `AssemblyRedirectListTests.cs:37-48`).
7. **Never express a load-order requirement in `<DependedModuleMetadatas>` alone.** The engine does
   not read it (first engine table).
8. **Never ship a binary under `_Module/bin/` without its notice paragraph** ([provenance rule](../../.claude/rules/provenance.md)).

Check: `dotnet test TAOM.Tests --filter BundledDependencyManifest`
Takes effect: full game restart
Code: No code changes needed

## Gotchas: what fails silently and what crashes

- **The #371 pin is absent from `Main/_Module/SubModule.xml`.** Both rows were deleted by
  `cc1713eb` on 2026-08-11 while the comment describing them stayed; the `/release` grep for the
  metadata row matches nothing. Nothing in either manifest blocks a mismatched pair today
  (`git show cc1713eb -- Main/_Module/SubModule.xml`; `.claude/skills/release/SKILL.md:51-57`).
- **The four alias stubs deploy to `Modules/<Id>/_Module/SubModule.xml`, one level too deep for the
  engine**, which requires `Modules/<Id>/SubModule.xml` and skips the folder otherwise. On the vanilla
  launcher the stubs are invisible (`ModuleHelper.cs:327-331`; `Dependencies/TAOM.Dependencies.csproj:99-101`;
  the deployed folders listed on this machine).
- **`-p:DisableModuleCopy=true` does not stop deployment.** `CopyBinariesWindows` and `CopyModule`
  fire regardless; the game holds `0Harmony.dll` and `Bannerlord.ButterLib.dll`, so a build with the
  game open fails with `UnauthorizedAccessException`. Use `-p:ModuleId=` to skip all three
  ([agent-operating-manual.md](../ai-includes/agent-operating-manual.md) lines 49-51).
- **The build output overwrites the vendored files on every deploy.** That is how the Unsafe 6.0.0
  regression shipped: the vendored file was right and the csproj pin clobbered it
  (`TAOM.Tests/Infrastructure/DependenciesPairingTests.cs:70-71`).
- **Both `DependenciesPairingTests` are vacuous on a fresh clone.** `System.Runtime.CompilerServices.Unsafe.dll`
  is not in the `.gitignore` allowlist and not tracked, so the first test `continue`s past every
  variant and the second returns `Inconclusive` without a `Dependencies/bin` folder
  (`DependenciesPairingTests.cs:34-38, 72-74`; `git ls-files Dependencies/_Module/bin/Win64_Shipping_Client`).
- **Five folders the live module needs exist in the game install only.** `AssetPackages/pack0.tpac`,
  `EmAssetPackages/GauntletUI/`, `GUI/Bannerlord.MBOptionScreenSpriteData.xml`,
  `ModuleData/Languages/` and `ModuleData/Languages_MCM/` (17 language folders each) are under
  `Modules/TAOM.Dependencies/` and nowhere under `Dependencies/`; no build step or doc in the repo
  produces them. A fresh clone plus build gives a TAOM.Dependencies with no MCM artwork.
  <!-- measured: ls "<game>/Modules/TAOM.Dependencies"; ls ".../ModuleData/Languages" | wc -l; ls ".../ModuleData/Languages_MCM" | wc -l 2026-09-05 -->
- **The vendored implementation set is three builds behind the Workshop.** Both Workshop folders hold
  `1.4.6`, `1.4.7` and `1.4.8`; TAOM vendors through `1.4.5`. [dr3-maintenance.md](../migration/dr3-maintenance.md)
  line 172 records the 2026-08-10 check as "MCM is clean" at `1.4.5`, which is no longer true of the
  MBOptionScreen folder either (the Workshop listing on this machine).
- **`SubModuleInfo` probes `bin\Win64_Shipping_Client` no matter which build runs**, while the load
  uses `bin/<Common.ConfigName>/`, the process's working-directory name. A dedicated server or editor
  build needs the mirrored folders (`SubModuleInfo.cs:54`; `Module.cs:1044`; `Common.cs:37`).
- **`Name`, `Id`, `Version`, and inside a `<SubModule>` `Name`, `DLLName`, `SubModuleClassType` are
  unguarded reads.** Omit one at module level and the module is shown as "can't be loaded" and does not
  exist; omit one inside an entry and an empty entry is added silently (`ModuleInfo.cs:81-87, 156-164`;
  `SubModuleInfo.cs:51-65`; `ModuleHelper.cs:121-126, 356-361`).
- **The redirect discards the requested version.** It returns the first loaded assembly with the same
  simple name (`Dependencies/SubModule.cs:144-162`), safe only while TAOM's copy is the newest in the
  process.
- **`Harmony.UnpatchAll(null)` is blocked.** A prefix returns `false` for a null id because that call
  would wipe every Harmony patch in the process (`Dependencies/SubModule.cs:294-309`).
- **A folder-name collision can overwrite a real BUTR module.** If a player has the standalone
  Workshop `Bannerlord.Harmony` module, the stub deploy overwrites its manifest and a later Steam update
  can overwrite the stub back ([dr3-maintenance.md](../migration/dr3-maintenance.md) lines 301-309).
- **A red `(!)` on a third-party mod is the launcher's unsigned-code warning, not a dependency error**
  ([dr3-maintenance.md](../migration/dr3-maintenance.md) line 244).
- **Under a co-op module two shields go quiet with no flag file involved.** PatchShield skips install
  and SaveShield rethrows the save-load category; "the shield did not run" is then expected, and the
  skip reason is logged in `Modules/TAOM.Dependencies/diag.log`
  ([coop-interop.md](../features/coop-interop.md) lines 400-435; [dr3-maintenance.md](../migration/dr3-maintenance.md) line 282).
- **`diag.log` is append-only and never rotated**: 3,150,674 bytes on this machine.
  <!-- measured: ls -la "<game>/Modules/TAOM.Dependencies" 2026-09-05 -->
  It is the first file to read for any incident ([dr3-maintenance.md](../migration/dr3-maintenance.md) line 267).
- **[dr3-maintenance.md](../migration/dr3-maintenance.md) line 248 still says the Foundation classes
  came from BetaDeps "via clean-room rewrite".** The provenance register retracts that: the headers
  say "Ports BetaDeps.Foundation.X", the derivation is `behavioural-port`, status `uncleared`
  ([provenance-register.md](../reference/provenance-register.md) lines 330-343). The shipped notice
  uses the corrected wording (`Dependencies/_Module/THIRD-PARTY-LICENSES.txt:107-118`).

## Numbers in this chapter

All measured 2026-09-05 from the repo at its current commit and the live install on this machine.

| Number | What | Command |
|---|---|---|
| 237 | lines in `Dependencies/_Module/SubModule.xml` | `wc -l Dependencies/_Module/SubModule.xml` |
| 126 | lines in `Dependencies/TAOM.Dependencies.csproj` | `wc -l Dependencies/TAOM.Dependencies.csproj` |
| 31 | lines in `Dependencies/_Module/coop-modules.txt` | `wc -l Dependencies/_Module/coop-modules.txt` |
| 124 | lines in `Dependencies/_Module/THIRD-PARTY-LICENSES.txt` | `wc -l Dependencies/_Module/THIRD-PARTY-LICENSES.txt` |
| 7 | `<SubModule>` entries in the Dependencies manifest | `rg -c '^\s*<SubModule>' Dependencies/_Module/SubModule.xml` |
| 35 | `<Module Id>` rows in its `<ModulesToLoadAfterThis>` | `sed -n '21,77p' Dependencies/_Module/SubModule.xml \| rg -c '<Module Id='` |
| 5 | `<DependedModuleMetadata>` rows in the Dependencies manifest | `rg -c '<DependedModuleMetadata ' Dependencies/_Module/SubModule.xml` |
| 4 | `<Assembly>` rows under the ButterLib entry | `sed -n '176,181p' Dependencies/_Module/SubModule.xml \| rg -c '<Assembly '` |
| 4 | `<DependedModule>` rows in `Main/_Module/SubModule.xml` | `rg -c '<DependedModule ' Main/_Module/SubModule.xml` |
| 1 (line 15, a comment) | mentions of `TAOM.Dependencies` in `Main/_Module/SubModule.xml` | `rg -n 'TAOM.Dependencies' Main/_Module/SubModule.xml` |
| 3 | ids in the `[modules]` section of `coop-modules.txt` | `awk '/^\[modules\]/{f=1;next} /^\[/{f=0} f && !/^#/ && NF' Dependencies/_Module/coop-modules.txt` |
| 4 | stub module folders | `ls -d Stubs/*/ \| wc -l` |
| v2.4.99.0, v2.13.99.0, v2.11.99.0, v5.12.99.0 | stub versions (Harmony, UIExtenderEx, ButterLib, MBOptionScreen) | `rg -n '<Version value=' Stubs/*/_Module/SubModule.xml` |
| 39 / 36 / 3 | vendored folder: files on disk / git-tracked / untracked | `ls Dependencies/_Module/bin/Win64_Shipping_Client \| wc -l`; `git ls-files Dependencies/_Module/bin/Win64_Shipping_Client \| wc -l` |
| 42 / 42 | live `bin/Win64_Shipping_Client` / `bin/Win64_Shipping_Server` file counts | `ls "<game>/Modules/TAOM.Dependencies/bin/Win64_Shipping_Client" \| wc -l` (and `_Server`) |
| 6 / 6 / 6 | vendored `ButterLib.Implementation.1.4.N` / `MBOptionScreen.v1.4.N` / `BUTR.CrashReport*` DLLs | `ls Dependencies/_Module/bin/Win64_Shipping_Client \| rg -c '<pattern>'` |
| 9 / 9 | Workshop `Implementation.1.4.N` / `MBOptionScreen.v1.4.N` DLLs (`1.4.0` to `1.4.8`) | `ls "<workshop>/261550/2859232415/bin/Win64_Shipping_Client" \| rg -c 'ButterLib\.Implementation\.1\.4\.[0-9]+\.dll'` (and `2859238197`) |
| 2 | files in the Main vendored allowlist | `git ls-files Main/_Module/bin \| wc -l` |
| 13 | `!` allow patterns at `.gitignore:52-64` | `sed -n '52,64p' .gitignore \| rg -c '^!'` |
| 16 | `.cs` files in `Dependencies/Foundation/` | `ls Dependencies/Foundation/*.cs \| wc -l` |
| 17 | simple names in `RedirectedSimpleNames` | `sed -n '39,98p' Dependencies/SubModule.cs \| rg -c '^\s*"[^"]+",'` |
| 9 / 2 / 5 | `[TestMethod]` in `BundledDependencyManifestTests` / `DependenciesPairingTests` / `AssemblyRedirectListTests` | `rg -c '\[TestMethod\]' <file>` |
| 2.11.0.0, 5.12.1.0, 1.0.1.50, 14.0.0.99 | FileVersion of `Bannerlord.ButterLib.dll`, `MCM.UI.Adapter.MCMv5.dll`, `Bannerlord.ModuleLoader.Bannerlord.MBOptionScreen.dll`, `BUTR.CrashReport.dll` | `[Diagnostics.FileVersionInfo]::GetVersionInfo(<dll>).FileVersion` |
| 2.4.2.0, 2.13.2.0, 5.12.1.0 | FileVersion of the live `0Harmony.dll`, `Bannerlord.UIExtenderEx.dll`, `MCMv5.dll` | same, over the live `bin/Win64_Shipping_Client` |
| 4.0.4.1 / 4.0.1.1 | assembly version of `System.Runtime.CompilerServices.Unsafe.dll` (repo and live) / vendored `System.Memory.dll` | `[Reflection.AssemblyName]::GetAssemblyName(<dll>).Version` |
| 17 / 17 | language folders under the live `ModuleData/Languages` / `ModuleData/Languages_MCM` | `ls "<game>/Modules/TAOM.Dependencies/ModuleData/Languages" \| wc -l` (and `_MCM`) |
| 3,150,674 | bytes in the live `diag.log` | `ls -la "<game>/Modules/TAOM.Dependencies"` |
| 19 | folders under the live `Modules/` | `ls "<game>/Modules" \| wc -l` |
| v1.4.8 | pinned engine version | `cat .claude/pinned-game-version.txt` |
| 2026-08-11 | date of `cc1713eb`, the commit that removed the #371 rows | `git log -1 --format='%h %ad' --date=short cc1713eb` |
| 0 | hits for `DependedModuleMetadata` in the v1.4.8 managed decompile | `rg -n DependedModuleMetadata <decompile root>` |

## Read next

- [dr3-maintenance.md](../migration/dr3-maintenance.md): the maintenance manual this chapter distils;
  categories, scenarios, the stub rule, the shields and the file inventory.
- [release-process.md](../reference/release-process.md): the three version fields and the release
  sequence.
- [coop-interop.md](../features/coop-interop.md): the load-order reasoning, the assembly-resolution
  table and the four coupled edits.
- [mcm.md](../features/mcm.md): where MCM the library ends and TAOM's layout fix begins.
- [provenance-register.md](../reference/provenance-register.md) and the
  [provenance rule](../../.claude/rules/provenance.md): what a shipped notice must say.
- [dependency-audit-2026-07-15.md](../migration/dependency-audit-2026-07-15.md): the audit that found
  the drift the tests now catch.
- [bannerlordcoop-internals.md](../research/bannerlordcoop-internals.md): the evidence behind the
  five excluded redirect names.
- [agent-operating-manual.md](../ai-includes/agent-operating-manual.md): the `-p:DisableModuleCopy`
  caveat.
