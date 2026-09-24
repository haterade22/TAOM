# Plan 010: Compile and test C# on GitHub-hosted Windows runners against BUTR reference assemblies

> **Executor instructions**: Follow this plan step by step. Run every
> verification command and confirm the expected result before moving to the
> next step. If anything in the "STOP conditions" section occurs, stop and
> report; do not improvise. When done, report your status in your final
> message: the orchestrator of this run maintains `plans/README.md` (it is
> uncommitted in the main tree, and `plans/` is not in your worktree), so do
> not edit it.
>
> **This plan file lives only in the main tree** at
> `E:\repos\TAOM\plans\010-ci-on-hosted-windows.md` (untracked). Read it from there; do all edits in
> your worktree.
>
> **Worktree path discipline (read before any step).** Another session has uncommitted work in the
> main tree `E:\repos\TAOM` (including `CHANGELOG.md` and several test files this plan tags). You
> must never edit, build, test, stage or commit there. Your Bash working directory resets to
> `E:\repos\TAOM` on **every** call, so a `cd` in one call does not carry to the next. Therefore:
>
> - **Every Bash call** after Step 0.2 starts with `cd /e/repos/wt-010-ci-hosted && `. Every command
>   in this plan is written relative to that worktree root; the prefix is implied even where a step
>   shows only the command.
> - **Every Read, Edit and Write path** is absolute and starts with `E:\repos\wt-010-ci-hosted\`. A
>   path in this plan such as `Main/TAOM.csproj` means that file inside the worktree.
> - Logs, helper scripts and the commit message go in `E:\repos\wt-010-logs\` (Bash:
>   `/e/repos/wt-010-logs`), outside every git tree. Never put scratch files inside the worktree.
> - The only `E:\repos\TAOM` paths you may touch are this plan file (read only) and the read-only
>   commands of the drift check and Step 0.8.
>
> **Drift check (run first, from `E:\repos\TAOM`, read only).** Part A, the config and doc files:
> `cd /e/repos/TAOM && git diff --stat b2e387db..HEAD -- Main/TAOM.csproj Dependencies/TAOM.Dependencies.csproj TAOM.Tests/TAOM.Tests.csproj Directory.Build.props .github/workflows .ai/verification.md .claude/rules/tests.md .claude/pinned-game-version.txt TAOM.Tests/Migration/GameAssemblies.cs TAOM.Tests/Infrastructure/RepoPaths.cs`
> Part B, the 120 test files this plan tags (their paths are the second field of the manifest you
> write in Step 0.5; do Step 0.5 first if you need them, it writes only under `E:\repos\wt-010-logs\`):
> `cd /e/repos/TAOM && git diff --stat b2e387db..HEAD -- $(cut -d'|' -f2 /e/repos/wt-010-logs/manifest.txt | sort -u)`
> Expected: at `4b5662b2` (2026-09-23) both parts printed nothing. Once plan 008 has landed, Part A
> shows exactly plan 008's changes to `TAOM.Tests/TAOM.Tests.csproj`, `TAOM.Tests/Migration/GameAssemblies.cs`
> and `.github/workflows/build.yml` (Step 0.3 describes them); those are expected. Any other listed
> file that changed: compare its "Current state" excerpt against the live code; on a mismatch, STOP.

## Status

> **Amendment (orchestrator, 2026-09-24, after the first execution stopped at Step 4.4).** The RefAsm
> build failed with `CS1069` in `Main/Features/FactionMap/Widgets/PolygonWidget.cs(10,38)`: the
> package's `lib\net46\System.Numerics.Vectors.dll` is a type-forwarding facade (it forwards `Vector2`
> to `System.Numerics`), and `PolygonWidget.cs` reaches the type through `extern alias SNV`, which
> cannot follow a forward. The game's own copy and the package's `lib\netstandard2.0` copy both DEFINE
> the types (same identity, 4.1.3.0). **Amended Step 4:** point the RefAsm `TaomSystemNumericsVectorsDll`
> at `lib\netstandard2.0\System.Numerics.Vectors.dll` (done in the content below). If net472 then needs
> the netstandard facade, the SDK's implicit facade expansion should supply it; if the build still fails,
> STOP and report. Optional hardening, apply it: wrap the package root as
> `$([MSBuild]::EnsureTrailingSlash('$(NuGetPackageRoot)'))` wherever the targets build a package path, so a
> `NUGET_PACKAGES` value without a trailing backslash cannot glue the folder names together.
>
> **Amendment 2 (orchestrator, 2026-09-24, after the second execution stopped at Step 6).** With
> amendment 1 the RefAsm build succeeds, but RefAsm test runs fail with `FileNotFoundException` for
> `System.Numerics.Vectors, Version=4.1.3.0`: in install mode the DLL reaches `TAOM.Tests` output only
> because it sits in the game's bin beside the `Private=True` TaleWorlds references; BUTR's `ref/net472`
> has no copy, and Main's SNV reference is `Private=False`. **Amended Step 6:** in RefAsm mode only,
> copy `$(TaomSystemNumericsVectorsDll)` into the test project's output (for example a
> `<None Include="$(TaomSystemNumericsVectorsDll)" Link="System.Numerics.Vectors.dll" CopyToOutputDirectory="PreserveNewest" Condition="'$(TaomGameRefs)' == 'RefAsm'" />`
> in `TAOM.Tests.csproj` or the equivalent in `GameReferences.targets`, scoped to the test project), and into
> `refasm-game\bin\Win64_Shipping_Client` if the gate's `GameAssemblies` resolver loads from there.
> .NET Framework 4.7.2 ships `netstandard.dll` itself, so the `netstandard2.0` copy loads at runtime; if
> it does not, STOP. Then tag, under the plan's rule (a), the three classes that need the real game at
> runtime (`ConsoleCommandBindingTests`, `LiveTableauRefTests`,
> `Patch86HideoutBossFightBindingTests`) and re-run Step 6 (a) and (b).

- **Priority**: P2
- **Effort**: M
- **Risk**: MED (first compile of TAOM against reference assemblies; the GitHub run itself cannot
  be executed from here)
- **Depends on**: `plans/008-binding-gate-no-silent-skips.md` (its `TAOM.Tests/binding-gate.runsettings`
  and its `GameAssemblies` changes are used by the CI binding step)
- **Category**: dx
- **Planned at**: commit `b2e387db`, 2026-09-23
- **Issue**: create before implementation lands (orchestrator). An older issue may already cover
  this: `docs/audits/issue-triage-2026-08-08.md:297` lists "#421 No CI verifies any PR: Build & Test
  is skipped" as KEEP. The orchestrator decides whether to reuse it.

## Why this matters

No CI job compiles TAOM's C# on any branch. `.github/workflows/build.yml` triggers only on
`bannerlord-1.4.5`, and its C# job needs a self-hosted runner (none is registered) plus a
repository variable (unset), so it is skipped on every run; on `bannerlord-1.5.x`, where the work
lands, only a doc lint runs. A broken build or a red suite reaches the branch whenever the local
gate is skipped (for example commits made outside the Claude hooks, such as `c79a5852`, 43 C# and
project files, pushed with only the doc lint behind it). The job's own warning asks the owner to
register the game workstation as a runner on a public repo, which `.ai/policy.md:78-79` and
`.ai/verification.md:6-7` forbid for untrusted code. BUTR publishes metadata-only reference
assemblies for the exact installed build, so a throwaway GitHub-hosted Windows VM can compile every
project and run about 8,000 tests plus about 338 engine-binding checks, with no game and no
workstation. Local builds evaluate to exactly the same references by default.

## Current state

All excerpts are from `git show b2e387db:<path>` unless stated.

### Files and their roles

- `.github/workflows/build.yml` (278 lines): triggers (`:3-8`) `push` and `pull_request` on
  `bannerlord-1.4.5` only, plus `workflow_dispatch`. Jobs: `check-build-config` (`:15-25`, a
  warning), `validate-xml` (`:27-156`), `hook-harness` (`:159-190`), `python-tests` (`:192-236`),
  `build` (`:238-278`, the self-hosted C# job). The non-C# jobs are out of scope.
- `Main/TAOM.csproj` (197 lines, tab-indented, **single-owner**): the mod. Game references at
  `:25-59`; ProjectReference to Dependencies at `:89`.
- `Dependencies/TAOM.Dependencies.csproj` (126 lines, tab-indented, **single-owner**): Harmony,
  UIExtenderEx, MCM and ButterLib stack. Game references at `:16-20`.
- `TAOM.Tests/TAOM.Tests.csproj` (42 lines at `b2e387db`, tab-indented; plan 008 adds one
  `ItemGroup`): MSTest 3.1.1. Game references at `:34-39`.
- `Directory.Build.props` (43 lines, **single-owner, NOT edited by this plan**): resolves
  `GameFolder` and `GameBinariesFolder` for every project.
- `TAOM.Tests/Migration/GameAssemblies.cs`: loads the game's module DLLs for the binding suite
  (NOT edited by this plan; plan 008 edits it).
- `.ai/verification.md` (lines 21-23 state that a hosted runner cannot build TAOM).
- `.claude/rules/tests.md` (3,914 bytes): the path-scoped testing rule; gains a short section.

### The self-hosted job being replaced (`build.yml:238-278`, abridged)

```yaml
  build:
    name: Build & Test
    # SELF-HOSTED, and it has to be. Every project targets net472 and resolves the
    # engine through GameFolder/bin/Win64_Shipping_Client/TaleWorlds.*.dll HintPaths
    ...
    # There is also no Microsoft.NETFramework.ReferenceAssemblies package anywhere in
    # this repo, so the net472 targeting pack comes from Visual Studio or Build Tools
    ...
    runs-on: [self-hosted, windows]
    ...
    if: ${{ vars.BANNERLORD_GAME_DIR != '' && github.event_name != 'pull_request' }}
    env:
      BANNERLORD_GAME_DIR: ${{ vars.BANNERLORD_GAME_DIR }}
    steps:
      - uses: actions/checkout@v4
      - name: Restore
        run: dotnet restore TAOM.sln
      ...
      - name: Build
        run: dotnet build TAOM.sln -c Release --no-restore -p:DisableModuleCopy=true -p:ModuleId=
      - name: Test
        run: dotnet test TAOM.Tests -c Release --no-build -p:DisableModuleCopy=true -p:ModuleId= --verbosity normal
```

Plan 008 appends one more step to this job (`- name: Binding gate (a skip fails)`), which this plan
deletes along with the job. `build.yml:15-25` is the `check-build-config` job whose warning (`:24`)
tells the owner to register a self-hosted runner. Both jobs are deleted by Step 7.

### How every project references the game today

`Directory.Build.props:37-41`:

```xml
<GameFolder Condition="'$(BANNERLORD_OVERRIDE_DIR)' != '' AND Exists('$(BANNERLORD_OVERRIDE_DIR)\bin\Win64_Shipping_Client\Bannerlord.exe')">$(BANNERLORD_OVERRIDE_DIR)</GameFolder>
<GameFolder Condition="'$(GameFolder)' == ''">$(BANNERLORD_GAME_DIR)</GameFolder>

<GameBinariesFolder Condition="Exists('$(GameFolder)\bin\Win64_Shipping_Client\Bannerlord.exe')">Win64_Shipping_Client</GameBinariesFolder>
<GameBinariesFolder Condition="Exists('$(GameFolder)\bin\Gaming.Desktop.x64_Shipping_Client\Bannerlord.exe')">Gaming.Desktop.x64_Shipping_Client</GameBinariesFolder>
```

`Main/TAOM.csproj:25-28`, `:35-59` (the Include/Exclude strings this plan rewrites; everything else
in these elements stays):

```xml
		<Reference Include="$(GameFolder)\bin\$(GameBinariesFolder)\System.Management.dll">
			<HintPath>%(Identity)</HintPath>
			<Private>False</Private>
		</Reference>
...
		<Reference Include="$(GameFolder)\bin\$(GameBinariesFolder)\System.Numerics.Vectors.dll">
			<HintPath>%(Identity)</HintPath>
			<Private>False</Private>
			<Aliases>SNV</Aliases>
		</Reference>
		<Reference Include="$(GameFolder)\bin\$(GameBinariesFolder)\TaleWorlds.*.dll" Exclude="$(GameFolder)\bin\$(GameBinariesFolder)\TaleWorlds.Native.dll">
			<HintPath>%(Identity)</HintPath>
			<Private>False</Private>
		</Reference>
		<Reference Include="$(GameFolder)\Modules\Native\bin\$(GameBinariesFolder)\*.dll">
			...
		<Reference Include="$(GameFolder)\Modules\SandBox\bin\$(GameBinariesFolder)\*.dll">
			...
		<Reference Include="$(GameFolder)\Modules\SandBoxCore\bin\$(GameBinariesFolder)\*.dll">
			...
		<Reference Include="$(GameFolder)\Modules\CustomBattle\bin\$(GameBinariesFolder)\*.dll">
```

The comment at `TAOM.csproj:17-24` explains why System.Management must bind assembly 4.0.1.0 (the
game's copy). `Main/Features/FactionMap/Widgets/PolygonWidget.cs:1` uses `extern alias SNV;`, so the
`Aliases` metadata must survive. The last line of the file is `</Project>` at `:197`.

`Dependencies/TAOM.Dependencies.csproj:16-17` (last line `</Project>` at `:126`):

```xml
		<Reference Include="$(GameFolder)\bin\$(GameBinariesFolder)\TaleWorlds.*.dll"
				   Exclude="$(GameFolder)\bin\$(GameBinariesFolder)\TaleWorlds.Native.dll;$(GameFolder)\bin\$(GameBinariesFolder)\TaleWorlds.CampaignSystem.dll">
```

`TAOM.Tests/TAOM.Tests.csproj:35` (`Private` is `True` here, so the test output gets a copy of each
TaleWorlds DLL; the last line is `</Project>`):

```xml
		<Reference Include="$(GameFolder)\bin\$(GameBinariesFolder)\TaleWorlds.*.dll" Exclude="$(GameFolder)\bin\$(GameBinariesFolder)\TaleWorlds.Native.dll">
```

No test or tool parses these Reference items: `git grep -n -E "GameFolder|HintPath|GameBinariesFolder" b2e387db -- TAOM.Tests tools build.ps1`
finds only `GameAssemblies.cs` and the csproj itself. `BundledDependencyManifestTests.cs` names the
two csproj files at `:30-31` and reads them (`XDocument.Load`, `Descendants("PackageReference")` at
`:50-51`) for `PackageReference` versions only; this plan adds no `PackageReference` to any csproj.

**How "install mode is unchanged" is proved.** `dotnet msbuild <project> -getItem:Reference` prints
every evaluated `Reference` item as JSON. Each file item also carries the well-known metadata
`ModifiedTime`, `CreatedTime` and `AccessedTime`, and NTFS last-access updates are enabled on the
desktop (`fsutil behavior query disablelastaccess` prints `2 (System Managed, Last Access Time
Updates ENABLED)`), so every build that reads a game DLL changes `AccessedTime`. Measured while
revising this plan: two evaluations of `TAOM.Tests/TAOM.Tests.csproj` with only a `cmp` of a game DLL
between them differ in one `AccessedTime` line (60 references). The plan therefore never diffs the
raw JSON: `E:\repos\wt-010-logs\normrefs.py` (Appendix D) deletes every key ending in `Time` and keeps
all other keys (`Identity`, `HintPath`, `Private`, `Aliases`, `DefiningProjectFullPath` and the rest)
and the item order. On the same two evaluations its outputs were identical.

### What BUTR publishes (measured by the audit and re-checked while planning)

The nupkgs downloaded by the audit (session scratchpad, not in the repo) were opened while planning:

| Package (nuget.org), version `1.5.3.122374-beta` | `ref/net472` content | Replaces |
|---|---|---|
| `Bannerlord.ReferenceAssemblies.Core` | 50 `TaleWorlds.*.dll` (no `TaleWorlds.Native.dll`) plus 4 `.exe` | the install's 51 `bin\Win64_Shipping_Client\TaleWorlds.*.dll` minus Native |
| `Bannerlord.ReferenceAssemblies.Native` | 5 DLLs (`TaleWorlds.MountAndBlade.GauntletUI*.dll`, `...Platform.PC.dll`, `...View.dll`) | `Modules\Native\bin\...` |
| `Bannerlord.ReferenceAssemblies.SandBox` | 6 DLLs (`SandBox*.dll`) | `Modules\SandBox\bin\...` |
| `Bannerlord.ReferenceAssemblies.CustomBattle` | 2 DLLs | `Modules\CustomBattle\bin\...` |
| `Bannerlord.ReferenceAssemblies.StoryMode` | 5 DLLs | loaded at test time by `GameAssemblies.cs:33-34` |
| (none) | the installed `Modules\SandBoxCore\bin\Win64_Shipping_Client\` holds no DLL, and no SandBoxCore package exists (nuget.org 404) | nothing needed |

Each nuspec carries `buildId:25302170 appId:261550 moduleVersion:v1.5.3`, and
`E:\Steam\steamapps\appmanifest_261550.acf` has `"buildid" "25302170"`: the packages are the
installed build. `.claude/pinned-game-version.txt` is `v1.5.3`. nuget.org also lists
`1.4.8.119303` (for the `bannerlord-1.4.5` line; not compared with a 1.4.8 install).

The two BCL assemblies the game ships are not BUTR's. Read with `[Reflection.AssemblyName]::GetAssemblyName`
while planning, identities match exactly (the files are not byte-identical, which does not matter
for compiling):

- game `bin\Win64_Shipping_Client\System.Management.dll` and NuGet `System.Management 4.7.0`
  `lib/netstandard2.0/System.Management.dll`: both `System.Management, Version=4.0.1.0, PublicKeyToken=b03f5f7f11d50a3a`.
- game `System.Numerics.Vectors.dll` and NuGet `System.Numerics.Vectors 4.4.0`
  `lib/net46/System.Numerics.Vectors.dll`: both `Version=4.1.3.0, PublicKeyToken=b03f5f7f11d50a3a`.

The reference assemblies are "stripped metadata-only": they load at runtime and every method body
throws `NullReferenceException`. Measured by the audit with the test DLL's 50 TaleWorlds DLLs
swapped for BUTR's and `BANNERLORD_GAME_DIR` unset: 8,546 results passed and 1,307 new failures in
**100 classes in 92 files** executed engine code (1,301 of them a `NullReferenceException`). That
measurement swapped only the TaleWorlds DLLs. An install-mode test bin also holds five non-TaleWorlds
DLLs copied from the game's `bin\Win64_Shipping_Client\` as dependencies of the `Private=True`
TaleWorlds references: `System.Management.dll`, `System.Numerics.Vectors.dll`, `Steamworks.NET.dll`,
`GalaxyCSharp.dll` and `StbSharp.dll` (the plan reviewer found each byte-identical to the game's copy
with `cmp`). A RefAsm build will not copy them, so a test that loads one would fail in a way the audit
did not see; Step 6 makes that a STOP. With a fake game folder built from the packages, the `BindingVerification` category ran **338 passed, 30
failed, 0 skipped** of 368; the 30 need vanilla method IL, vanilla data files or engine execution.

### NuGet and SDK facts relied on

- `$(NuGetPackageRoot)` is defined by the generated `obj\<project>.nuget.g.props` with a trailing
  backslash, and NuGet itself addresses package files as `$(NuGetPackageRoot)<lowercase id>\<version>\...`
  (read in `Main/obj/TAOM.csproj.nuget.g.props:7,117` on the desktop:
  `<NuGetPackageRoot ...>$(UserProfile)\.nuget\packages\</NuGetPackageRoot>` and
  `<Import Project="$(NuGetPackageRoot)bannerlord.buildresources\1.1.0.129\build\Bannerlord.BuildResources.props" .../>`).
- `PackageDownload` items download a package at restore without referencing it; the version must be
  exact, in brackets (`Version="[1.2.3]"`).
- The .NET SDK adds `Microsoft.NETFramework.ReferenceAssemblies` by itself when the net472
  targeting pack is missing, so no explicit reference is needed on a runner without Visual Studio.
  Read in `C:\Program Files\dotnet\sdk\10.0.401\Sdks\Microsoft.NET.Sdk\targets\Microsoft.NET.Sdk.FrameworkReferenceResolution.targets:514-531`
  (target `IncludeTargetingPackReference`, which adds the package `IsImplicitlyDefined="true"` when
  `GetReferenceAssemblyPaths` finds no pack and the project has no such reference of its own) and
  `Microsoft.NET.Sdk.props:124-125` (`AutomaticallyUseReferenceAssemblyPackages` defaults to `true`,
  version `1.0.3`). The comment at `build.yml:248-252` claiming otherwise is deleted with the job.
- The desktop has SDK `10.0.401` (`dotnet --version`). `Dependencies/TAOM.Dependencies.csproj:12`
  sets `<LangVersion>preview</LangVersion>`, whose meaning depends on the SDK, so the workflow pins
  the SDK line with `actions/setup-dotnet` `dotnet-version: '10.0.x'`.

### `Bannerlord.BuildResources 1.1.0.129` with no game (read from the NuGet cache)

`build/Basic.targets`: `WarningsForEnvs` (`:12-16`) only warns when `BANNERLORD_GAME_DIR` is unset;
`PostBuildCopyToModules`, `CopyBinariesWindows`, `CopyModule` (`:47,53,64`) are all conditioned on
`Exists($(GameFolder))`, false for an empty `GameFolder`; `TestCheckForGameBinaries` (`:35-37`,
`BeforeTargets="Test"`) errors without a game, but `dotnet test` runs the `VSTest` target, not
`Test`, and the package is `PrivateAssets all` in Main (`TAOM.csproj:95-98`). Main's own targets
(`TAOM.csproj:137-195`) that touch the game are also conditioned on `Exists($(GameFolder))`;
`CopyBinariesToModuleFolder` (`:146-158`) copies TAOM.dll into the checkout's own `Main/_Module/bin/`
(git-ignored by `.gitignore:75,82`), which is harmless.

### How the binding suite finds a game (`TAOM.Tests/Migration/GameAssemblies.cs`, plan 008 state)

At `b2e387db`, `ResolveGameDir()` (`:111-122`) reads `BANNERLORD_OVERRIDE_DIR` (only if its
`bin\Win64_Shipping_Client\Bannerlord.exe` exists), then `BANNERLORD_GAME_DIR`. `ResolveBinFolder`
(`:124-131`) then requires `bin\Win64_Shipping_Client\Bannerlord.exe` (an empty file is enough).
`EnsureLoaded` (`:36-79`) loads every `TaleWorlds.*.dll` from the test output folder, then every
`*.dll` under `Modules\{SandBox,SandBoxCore,CustomBattle,StoryMode,Native}\bin\<BinFolder>\`.
Plan 008 adds a third fallback after the two environment variables: the `TaomGameFolder` assembly
metadata that `TAOM.Tests.csproj` fills with `$(GameFolder)` at build time. In a `RefAsm` build
`GameFolder` is empty, so that fallback resolves nothing and the default test run skips the
game-gated tests; the CI binding step sets `BANNERLORD_GAME_DIR` explicitly (the environment
variable wins over the fallback).

### Test categories today

Only `BindingVerification` exists (290 attributes). It is used at class level already, for example
`TAOM.Tests/Features/Enlistment/EnlistmentTickBindingTests.cs:17-18`:

```csharp
[TestCategory("BindingVerification")]
public class EnlistmentTickBindingTests
```

This plan adds three categories, each consumed only by the CI filter (local default runs are
unchanged):

| Category | Where | Meaning |
|---|---|---|
| `RequiresGame` | class level, 100 classes in 92 files (measured) | executes engine code, which throws on reference assemblies |
| `RequiresGameIL` | method level, 29 binding methods | needs vanilla method IL or vanilla data; metadata cannot give it |
| `LiveInstall` | class level, 10 files | reads the live Armory or the vanilla install (9 hard-code `E:\Steam\...`) |

The 30th failing binding method, `BannerBearerReplacementWeaponDataTests.ShippedCultures_EveryBannerBearerReplacementWeaponIsOneHanded`,
reads Armory items, so its class gets `LiveInstall` instead. `Core/LordFamilyTransformTests.cs`
reads the vanilla install with an `E:\Steam` fallback and is `LiveInstall` too.
`Features/NativeSkinFixes/NativeSkinFixesInstallerTests.cs:33,42` holds `E:\Steam\...` strings only
as input to a path classifier (no file access) and is NOT tagged.

Class-level `RequiresGame` also drops about 288 results in those classes that pass on stubs (the
audit's checker measured it); that trade-off was accepted in exchange for 100 attributes instead of
about 1,307. It also covers a vacuous pass: `PlayerClanLeadershipServiceTests.RepairIfNeeded_EngineDeclinedThePromotion_ReportsFalseAndStaysQuiet`
passes on stubs only because `PlayerClanLeadershipService.cs:57,96-100` catches the stub's throw.

### Decided; do NOT overturn

- **No self-hosted runner** (`.ai/policy.md:78-79`, `.ai/verification.md:6-7`). The `if:` guard at
  `build.yml:259` lives in the file a pushed branch can edit, so it protects nothing.
- **Plan 008's decision:** in the *default* suite a test skips, never fails, when the game or the
  Armory is absent. `MapInconclusiveToFailed` applies only to runs that pass
  `TAOM.Tests/binding-gate.runsettings` explicitly. Never set it globally (no `RunSettingsFilePath`,
  no root `.runsettings`). This plan's unit-test step uses default settings; only its binding step
  passes the file.
- **No automatic switch to reference assemblies when the game is missing.** Locally that would hide
  a missing install behind stubs. `RefAsm` is opt-in by `-p:TaomGameRefs=RefAsm`.
- **Nothing proprietary is committed.** The BUTR packages are downloaded at restore. Building
  metadata-only assemblies from the install and publishing them to a private feed (the fallback for a
  day BUTR has not published a build yet) is a licensing call for the maintainer: not in this plan.
- `.claude/rules/environment-failures.md`: a missing tool, SDK or network is reported, not fixed.

### Binding rules (the executor has not read them)

- **ADR-008 (testability):** tests run without game initialisation. The two new tests read repo
  files only.
- **`.claude/rules/tests.md`:** TDD (write the failing test first and see it fail), names
  `MethodName_StateUnderTest_ExpectedBehavior`, MSTest `[TestClass]`/`[TestMethod]`, AAA comments,
  tests mirror the source folder. Repo-file tests use the layout-proof helper
  `TAOM.Tests/Infrastructure/RepoPaths.cs` (`RepoPaths.RepoPath(params string[] parts)`, resolved
  from the source file's `[CallerFilePath]`). Exemplar of the XML reads only:
  `TAOM.Tests/Infrastructure/Dependencies/BundledDependencyManifestTests.cs:50-51` (`XDocument.Load`,
  `Descendants("PackageReference")`). Do not copy its path handling: it builds `RepoRoot` from
  `AppDomain.CurrentDomain.BaseDirectory` at `:27-28`, the older layout-dependent pattern. The Step 2
  code below already uses `RepoPaths`.
- **ADR-002 (entry points under 150 lines) and ADR-007 (services take adapters, never sealed
  TaleWorlds types):** not engaged; no production C# changes.
- **ADR-003 / ADR-004 / ADR-005:** no `#region`, no `[Obsolete]`, no `#if DEBUG`.
- **Nullable** is enabled for the test project; declare nullable locals as `string?`.
- **Single-owner files:** `Main/TAOM.csproj` and `Dependencies/TAOM.Dependencies.csproj` are edited
  only as Step 3 lists, character for character. `Directory.Build.props`, `Main/IoC.cs` and
  `Main/SubModule.cs` are not touched.
- **Prose rule:** no em or en dash (U+2014, U+2013) in any new prose line (markdown, XML and YAML
  comments, CHANGELOG, commit body). XML comments must not contain `--`.
- No TaleWorlds API is called, patched or overridden, so no engine signature is relied on and
  `pwsh tools/taom-src.ps1` is not needed.

### Baseline you must not chase

At `b2e387db` the orchestrator measured the full suite at **10,239 tests: 10,235 pass, 2 ignored**
(`WargAttack_FastWarg_InvokesRunningAttack`, `WargAttack_SlowWarg_InvokesStandingAttack`,
deliberate `[Ignore]`) and **2 fail**: `TheElkItem_DeclaresTheScaleTheReachIsTunedFor`
(`TAOM.Tests/Features/Elk/ElkConfigTests.cs`) and
`AnimaliaActionSets_BindOnlyHorseActions_ToClipsThatExist`
(`TAOM.Tests/Features/Animalia/AnimaliaMountWiringTests.cs`). Both read the live, unversioned
`LOTRLOME_Armory`, which another session is editing; they are not caused by any plan and must not
be investigated or fixed. Both classes are `LiveInstall` rows of the manifest, which is how Step
0.7 accepts them mechanically. Plan 008 adds 6 tests. Step 0.7 records the set your worktree
actually sees; later steps compare against that record.

## Commands you will need

Run from the worktree root (prefix every Bash call with `cd /e/repos/wt-010-ci-hosted && `).
`NOGAME` below is shorthand for the literal prefix `env -u BANNERLORD_GAME_DIR -u BANNERLORD_OVERRIDE_DIR`
(write it out in every command; it is not a shell variable).

| Purpose | Command | Expected on success |
|---|---|---|
| Build (install) | `dotnet build Main/TAOM.csproj -p:DisableModuleCopy=true -p:ModuleId=` | exit 0, 0 errors |
| Test (install, full) | `dotnet test TAOM.Tests -p:DisableModuleCopy=true -p:ModuleId=` | only the Step 0.7 failures |
| Filtered test | `dotnet test TAOM.Tests -p:DisableModuleCopy=true -p:ModuleId= --filter "FullyQualifiedName~GameReferencesTargetsTests"` | per step |
| Evaluated references | `dotnet msbuild <project> -getItem:Reference -p:DisableModuleCopy=true -p:ModuleId=` | JSON; normalized by `normrefs.py` (Appendix D), then compared by `diff` |
| Build (no game) | `NOGAME dotnet build TAOM.Tests -nr:false -p:TaomGameRefs=RefAsm -p:DisableModuleCopy=true -p:ModuleId=` | exit 0, 0 errors |
| Unit tests (no game) | `NOGAME dotnet test TAOM.Tests --no-build -p:TaomGameRefs=RefAsm -p:DisableModuleCopy=true -p:ModuleId= --filter "TestCategory!=RequiresGame&TestCategory!=LiveInstall&TestCategory!=BindingVerification" --logger "trx;LogFileName=unit.trx" --results-directory E:/repos/wt-010-logs/trx` | exit 0 |
| Data | `python tools/validate_moduledata.py` | same exit code and last line as Step 0.9 |
| Docs | `python tools/lint_docs.py --summary` and `python tools/lint_docs.py --fail-on-drift` | counts as Step 0.9; exit 0 |
| AI docs | `python tools/reviewctl.py lint` and `python -m unittest tools.tests.test_reviewctl tools.tests.test_ai_documentation -v` | as Step 0.9 |

Never run `./build.ps1` (it deploys into the game install). Every `dotnet build`/`test` carries
`-p:DisableModuleCopy=true -p:ModuleId=` (the game may be running). Redirect any `dotnet test` to a
log under `/e/repos/wt-010-logs/` and read it with `grep`, because tool output is truncated at about
30 KB. `dotnet test` prints counts space-padded (`Failed:     0,`): grep with `Failed:\s+0,`, never a
literal `Failed: 0`. `-nr:false` stops MSBuild reusing worker nodes started with the game variables
set.

**Switching between the install build and the reference-assembly build** reuses the same `bin/` and
`obj/`, and a stale copy of the other mode's TaleWorlds DLLs in `TAOM.Tests/bin` would poison the
next run. Before every switch, run exactly:
`cd /e/repos/wt-010-ci-hosted && rm -rf Main/bin Main/obj Dependencies/bin Dependencies/obj TAOM.Tests/bin TAOM.Tests/obj TestResults`
(all git-ignored build output inside the worktree; never run it anywhere else).

## Scope

**In scope** (the only files you may modify or create; all inside the worktree):

- `GameReferences.targets` (new, repo root)
- `Main/TAOM.csproj` (single-owner: the seven `Reference` Include/Exclude strings at `:25`, `:35`,
  `:40`, `:44`, `:48`, `:52`, `:56`, one `Condition` on the `:52` element, and one `Import` line
  before `</Project>`; nothing else)
- `Dependencies/TAOM.Dependencies.csproj` (single-owner: the Include/Exclude at `:16-17` and one
  `Import` line before `</Project>`; nothing else)
- `TAOM.Tests/TAOM.Tests.csproj` (the Include/Exclude of the TaleWorlds `Reference` line, `:35` at
  `b2e387db` but possibly shifted by plan 008's `ItemGroup`, and one `Import` line)
- `TAOM.Tests/Infrastructure/GameReferencesTargetsTests.cs` (new)
- The 120 test files named in the manifest (Step 0.5): one `[TestCategory("...")]` line per entry,
  nothing else
- `.github/workflows/csharp.yml` (new)
- `.github/workflows/build.yml` (delete the `check-build-config` job and the `build` job; nothing
  else)
- `.ai/verification.md` (lines 21-23 only)
- `.claude/rules/tests.md` (one new section)
- `CHANGELOG.md` (one entry, **the worktree's copy only**)

**Out of scope** (do NOT touch, even though they look related):

- Anything under `E:\repos\TAOM` (the main tree).
- `Directory.Build.props`, `Main/IoC.cs`, `Main/SubModule.cs`: single-owner and not needed. If you
  believe one must change, STOP and report the exact line.
- `TAOM.Tests/Migration/GameAssemblies.cs`, `TAOM.Tests/binding-gate.runsettings` and the `TaomGameFolder`
  `ItemGroup` in `TAOM.Tests.csproj`: plan 008's; use them as they are.
- The `on:` block and the `validate-xml`, `hook-harness`, `python-tests` jobs of `build.yml`
  (they have been red on `bannerlord-1.4.5` for many runs; repairing them is a separate item).
- `build.ps1`, `.claude/skills/verify/SKILL.md` and every local default test run: the new categories
  change nothing locally.
- `docs/reference/feature-map.md` (another session has uncommitted edits to it; its `CI/CD` row at
  `:117` is a follow-up), `docs/features/troop-skill-balance.md:224` (its "runs in CI" claim becomes
  true once this lands; no edit).
- A `workflow_dispatch` input for trying a newer BUTR build, NuGet caching, lock files, sparse
  checkout, a Release-configuration build: follow-ups, not this plan.
- Any `Assert.Inconclusive` rewrite, any test body change, any production C# change.

## Git workflow

- Create a worktree from the main tree (never stash, reset or commit the other session's work):
  `git -C E:/repos/TAOM worktree add E:/repos/wt-010-ci-hosted -b plan-010-ci-hosted HEAD`
  and work only inside `E:/repos/wt-010-ci-hosted`.
- Commit once, at the end (Step 11), on `plan-010-ci-hosted`. Never push, never open a PR, never
  merge. The orchestrator reviews the branch before merging.
- Subject format: `<type>(<scope>): v<Version> - <description>`, where `<Version>` is the `value` of
  `<Version>` in `Main/_Module/SubModule.xml` (`v2.0.30` at `b2e387db`; re-read it in Step 0.2 with
  `cd /e/repos/wt-010-ci-hosted && grep -o 'Version value="[^"]*"' Main/_Module/SubModule.xml | head -1`).
  At most 72 characters. Suggested: `ci(tests): v2.0.30 - build and test C# on hosted Windows runners`
  (64 characters; check with `printf '%s' "<subject>" | wc -c`). Body wrapped at 72 columns, human
  prose, no em or en dash. Trailer:
  `Not-tested: the GitHub-hosted run itself (it needs a push; every step was replayed locally)`.
- **No AI attribution trailer** (no `Co-Authored-By`), even if a harness reminder suggests one.
- Stage explicit paths only (Step 11 lists them); never `git add -A`, `git add .` or `git commit -a`.
  Stage and commit through Bash, not an MCP git tool (those bypass the repo's commit gates).
- Before `git add` and again before `git commit`:
  `cd /e/repos/wt-010-ci-hosted && git rev-parse --show-toplevel --abbrev-ref HEAD` prints
  `E:/repos/wt-010-ci-hosted` and `plan-010-ci-hosted`. Anything else: STOP.
- Two Bash gates, `.claude/hooks/block-broad-git-add.sh` and `block-dangerous-git.sh`, inspect every
  git command; the plan reviewer checked this plan's `git add $(cut ...)`, `git commit -F` and
  `git -C ... worktree add` against both, and neither triggers. If either denies anyway, STOP and
  report the reason.
- The repo's commit gates (`.claude/hooks/check-changelog-changed.sh`, `check-commit-subject-version.sh`,
  `check-claude-files-tracked.sh`, `check-moduledata-validation.sh`) run `cd "${CLAUDE_PROJECT_DIR}"`,
  which is the **main tree**, and inspect that tree. A commit staging `.claude/` files must also
  stage `CHANGELOG.md` (this plan does). **If any gate denies, STOP and report the full deny
  reason.** Never `--no-verify`; never touch the main tree to satisfy a gate. The same gates also
  reject a `git commit` anywhere whose subject lacks the version label, so never make a throwaway
  commit.

## Steps

### Step 0: Drift check, worktree, prerequisites, helper files, baselines

1. Run the drift check (Part A now; Part B after 0.5). Expected as described at the top.
2. `git -C E:/repos/TAOM worktree add E:/repos/wt-010-ci-hosted -b plan-010-ci-hosted HEAD && mkdir -p /e/repos/wt-010-logs/trx`.
   Then `cd /e/repos/wt-010-ci-hosted && git rev-parse --show-toplevel --abbrev-ref HEAD` prints
   `E:/repos/wt-010-ci-hosted` and `plan-010-ci-hosted`. Re-read the version (Git workflow).
3. **Plan 008 must be in your worktree.** Each prints the stated result:
   - `test -f TAOM.Tests/binding-gate.runsettings && echo yes` prints `yes`
   - `grep -c MapInconclusiveToFailed TAOM.Tests/binding-gate.runsettings` prints `1`
   - `grep -c TaomGameFolder TAOM.Tests/TAOM.Tests.csproj` prints `1` or more
   - `grep -c BuiltGameFolder TAOM.Tests/Migration/GameAssemblies.cs` prints `1` or more
   - `git grep -n RunSettingsFilePath -- '*.csproj' '*.props' '*.targets'` prints nothing
   If any fails: STOP ("plan 008 has not landed on the branch this worktree came from").
4. Environment: `printenv BANNERLORD_GAME_DIR` prints the install (on the desktop
   `E:\Steam\steamapps\common\Mount & Blade II Bannerlord`); `dotnet --version` prints `10.0.x`;
   `pwsh -v` prints a PowerShell 7 version; `python -c "import yaml; print('ok')"` prints `ok`.
   Any failure: STOP (environment fact: report, do not fix).
5. Write the four helper files with the **Write tool** (Bash heredocs mangle backslashes and
   quotes), exactly as given in Appendices A to D at the end of this plan:
   - `E:\repos\wt-010-logs\manifest.txt` (Appendix A, 139 lines)
   - `E:\repos\wt-010-logs\tag_categories.py` (Appendix B)
   - `E:\repos\wt-010-logs\trxsum.py` (Appendix C)
   - `E:\repos\wt-010-logs\normrefs.py` (Appendix D)
   Then run drift check Part B.
6. Install-mode snapshots (these must survive Step 3 and 4 unchanged). Build, then evaluate and
   normalize the three projects' references (the normalization drops the `*Time` keys; see "How
   'install mode is unchanged' is proved"):
   `cd /e/repos/wt-010-ci-hosted && dotnet build TAOM.Tests -p:DisableModuleCopy=true -p:ModuleId= > /e/repos/wt-010-logs/0-build.log 2>&1; echo "exit=$?"`
   `cd /e/repos/wt-010-ci-hosted && dotnet msbuild Main/TAOM.csproj -getItem:Reference -p:DisableModuleCopy=true -p:ModuleId= > /e/repos/wt-010-logs/0-refs-main.json`
   `cd /e/repos/wt-010-ci-hosted && dotnet msbuild Dependencies/TAOM.Dependencies.csproj -getItem:Reference -p:DisableModuleCopy=true -p:ModuleId= > /e/repos/wt-010-logs/0-refs-deps.json`
   `cd /e/repos/wt-010-ci-hosted && dotnet msbuild TAOM.Tests/TAOM.Tests.csproj -getItem:Reference -p:DisableModuleCopy=true -p:ModuleId= > /e/repos/wt-010-logs/0-refs-tests.json`
   `cd /e/repos/wt-010-logs && for p in main deps tests; do python normrefs.py 0-refs-$p.json 0-norm-$p.json; done`
   The last command prints three `references=<N>` lines with N above 0.

   **The normalized diff** used by Steps 3 and 4 and the Done criteria. It is one command; replace
   the `3` in `S=3` with the snapshot prefix the step names (`3`, `4` or `11`) and change nothing
   else:
   `cd /e/repos/wt-010-ci-hosted && S=3 && dotnet msbuild Main/TAOM.csproj -getItem:Reference -p:DisableModuleCopy=true -p:ModuleId= > /e/repos/wt-010-logs/$S-refs-main.json && dotnet msbuild Dependencies/TAOM.Dependencies.csproj -getItem:Reference -p:DisableModuleCopy=true -p:ModuleId= > /e/repos/wt-010-logs/$S-refs-deps.json && dotnet msbuild TAOM.Tests/TAOM.Tests.csproj -getItem:Reference -p:DisableModuleCopy=true -p:ModuleId= > /e/repos/wt-010-logs/$S-refs-tests.json && cd /e/repos/wt-010-logs && for p in main deps tests; do python normrefs.py $S-refs-$p.json $S-norm-$p.json; done && diff 0-norm-main.json $S-norm-main.json && diff 0-norm-deps.json $S-norm-deps.json && diff 0-norm-tests.json $S-norm-tests.json && echo IDENTICAL`
   Expected: three `references=<N>` lines equal to Step 0.6's, then `IDENTICAL`. Never diff the raw
   `*-refs-*.json` files: their `AccessedTime` values change whenever a build reads a game DLL.
7. Full-suite baseline with a trx:
   `cd /e/repos/wt-010-ci-hosted && dotnet test TAOM.Tests --no-build -p:DisableModuleCopy=true -p:ModuleId= --logger "trx;LogFileName=0-full.trx" --results-directory E:/repos/wt-010-logs/trx > /e/repos/wt-010-logs/0-full.log 2>&1; echo "exit=$?"; grep -E 'Passed!|Failed!' /e/repos/wt-010-logs/0-full.log; python /e/repos/wt-010-logs/trxsum.py E:/repos/wt-010-logs/trx/0-full.trx | tee /e/repos/wt-010-logs/0-sum.txt; sed -n 's/^FAILED //p' /e/repos/wt-010-logs/0-sum.txt > /e/repos/wt-010-logs/0-failed.txt`
   Then check that every failure belongs to a `LiveInstall` class of the manifest:
   `cd /e/repos/wt-010-logs && while IFS='|' read -r c m; do grep -q "^class|[^|]*|$c|LiveInstall[[:space:]]*$" manifest.txt || echo "NOT-LIVEINSTALL $c.$m"; done < 0-failed.txt`
   The total, `0-failed.txt` (one `Class|Method` line per failing test) and the check's output are
   "the Step 0.7 record".
8. Overlap with the other session (read only):
   `cd /e/repos/TAOM && git status --porcelain -- $(cut -d'|' -f2 /e/repos/wt-010-logs/manifest.txt | sort -u)`
   Record every path it prints. At planning time it printed `CustomAttacksUtilsBlowFlagsTests.cs`,
   `AnimaliaMountWiringTests.cs`, `HowdahPrefabTests.cs`, `ElkConfigTests.cs` and
   `MumakilPlatformTests.cs`. You still tag the committed versions in your worktree (one added line
   each); list these files in your final report as a merge dependency for the orchestrator.
9. Docs and data baselines: `python tools/lint_docs.py --summary` (record `dead_links:` and
   `ai_dashes:`), `python tools/lint_docs.py --fail-on-drift; echo "exit=$?"`,
   `python tools/reviewctl.py lint; echo "exit=$?"`,
   `python -m unittest tools.tests.test_reviewctl tools.tests.test_ai_documentation 2>&1 | tail -3`,
   `python tools/validate_moduledata.py > /e/repos/wt-010-logs/0-data.log 2>&1; echo "exit=$?"; tail -3 /e/repos/wt-010-logs/0-data.log`.

**Verify**: 0.2 to 0.5 as stated; 0.6 prints `exit=0` and three `references=<N>` lines with N above
0; 0.7's `NOT-LIVEINSTALL` check prints nothing. The expected failures are the two named in
"Baseline you must not chase" (`ElkConfigTests` and `AnimaliaMountWiringTests` are both `LiveInstall`
rows); a different set is fine as long as the check prints nothing: record it and continue. The
total is expected near 10,245 (10,239 plus plan 008's 6; commits landed since may add more): record
it whatever it is. Any `NOT-LIVEINSTALL` line: STOP.

### Step 1: Confirm the issue

Check this file's Status block. If the orchestrator has not recorded an issue URL, continue and say
so in your final report; do not create one yourself.

**Verify**: `grep -n '^- \*\*Issue\*\*' /e/repos/TAOM/plans/010-ci-on-hosted-windows.md` prints one
line containing either `https://github.com` or `create before`. Report which.

### Step 2 (RED): pin the reference-assembly setup with two tests

Create `E:\repos\wt-010-ci-hosted\TAOM.Tests\Infrastructure\GameReferencesTargetsTests.cs`:

```csharp
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace TAOM.Tests.Infrastructure;

/// <summary>
/// GameReferences.targets owns where every project finds the Bannerlord assemblies: the install
/// (TaomGameRefs=Install, the default) or BUTR's reference assemblies (RefAsm, used by the hosted
/// CI job). These pin the two relationships that rot silently: the BUTR build must be the pinned
/// game version, and no project may spell the install path itself (it would bypass RefAsm and
/// break only on CI).
/// </summary>
[TestClass]
public class GameReferencesTargetsTests
{
    private static readonly string[] GameProjects =
    {
        Path.Combine("Main", "TAOM.csproj"),
        Path.Combine("Dependencies", "TAOM.Dependencies.csproj"),
        Path.Combine("TAOM.Tests", "TAOM.Tests.csproj"),
    };

    [TestMethod]
    public void BannerlordRefAsmVersion_PinnedGameVersion_IsTheSameGameBuild()
    {
        // Arrange
        var targetsPath = RepoPaths.RepoPath("GameReferences.targets");
        Assert.IsTrue(File.Exists(targetsPath), $"{targetsPath} is missing.");
        var pin = File.ReadAllText(RepoPaths.RepoPath(".claude", "pinned-game-version.txt")).Trim().TrimStart('v');

        // Act
        var versions = XDocument.Load(targetsPath).Descendants("BannerlordRefAsmVersion")
            .Select(e => e.Value.Trim()).ToList();

        // Assert
        Assert.AreEqual(1, versions.Count, "GameReferences.targets must set BannerlordRefAsmVersion exactly once.");
        StringAssert.StartsWith(versions[0], pin + ".",
            $"BannerlordRefAsmVersion {versions[0]} is not a build of the pinned game version v{pin}. " +
            "Bump both together (engine bump) and use the BUTR build published for that version.");
    }

    [TestMethod]
    public void GameProjects_EveryGameReference_ComesFromGameReferencesTargets()
    {
        // Arrange
        var problems = new List<string>();

        foreach (var project in GameProjects)
        {
            // Act
            var doc = XDocument.Load(RepoPaths.RepoPath(project));
            var imports = doc.Descendants("Import")
                .Count(e => (string?)e.Attribute("Project") == @"..\GameReferences.targets");
            if (imports != 1)
                problems.Add($"{project}: expected one <Import Project=\"..\\GameReferences.targets\" />, found {imports}");

            foreach (var reference in doc.Descendants("Reference"))
            {
                var spelled = ((string?)reference.Attribute("Include") ?? "") + ((string?)reference.Attribute("Exclude") ?? "");
                if (spelled.Contains("$(GameFolder)"))
                    problems.Add($"{project}: <Reference Include=\"{(string?)reference.Attribute("Include")}\"> names $(GameFolder) directly");
            }
        }

        // Assert
        Assert.AreEqual(0, problems.Count, string.Join("\n", problems));
    }
}
```

**Verify (RED)**:
`cd /e/repos/wt-010-ci-hosted && dotnet test TAOM.Tests -p:DisableModuleCopy=true -p:ModuleId= --filter "FullyQualifiedName~GameReferencesTargetsTests" > /e/repos/wt-010-logs/2.log 2>&1; echo "exit=$?"; grep -E 'Passed!|Failed!|GameReferences.targets is missing|names \$\(GameFolder\)' /e/repos/wt-010-logs/2.log | head`
prints a non-zero exit, a summary matching `Failed:\s+2, Passed:\s+0,`, the message
`GameReferences.targets is missing`, and `names $(GameFolder) directly` lines. A compile error: fix
your test file (not production code) and re-run.

### Step 3 (GREEN): create `GameReferences.targets` (install half) and route the three projects through it

1. Create `E:\repos\wt-010-ci-hosted\GameReferences.targets` with exactly this content (two-space
   indentation, UTF-8, no BOM):

```xml
<Project>
  <!--
    Where every TAOM project finds the Bannerlord assemblies it compiles against. Imported at the
    end of Main/TAOM.csproj, Dependencies/TAOM.Dependencies.csproj and TAOM.Tests/TAOM.Tests.csproj,
    whose game Reference items name only the properties below (GameReferencesTargetsTests).

    TaomGameRefs=Install (the default): the Bannerlord install that Directory.Build.props resolves
    into GameFolder. These are exactly the paths the projects spelled out before this file existed.

    TaomGameRefs=RefAsm: BUTR's stripped, metadata-only reference assemblies from nuget.org
    (Bannerlord.ReferenceAssemblies.*, generated from the Steam build named in their nuspec tags),
    plus System.Management and System.Numerics.Vectors from their NuGet packages at the assembly
    versions the game ships (4.0.1.0 and 4.1.3.0). For a machine with no game: the GitHub-hosted
    job in .github/workflows/csharp.yml. Every method body in these assemblies throws, so a test
    that executes engine code carries [TestCategory("RequiresGame")] and that job filters it out.
    The packages are downloaded at restore and never committed.

    BannerlordRefAsmVersion is the BUTR build of the version in .claude/pinned-game-version.txt;
    GameReferencesTargetsTests fails when the two disagree. On an engine bump, change both.
  -->
  <PropertyGroup>
    <TaomGameRefs Condition="'$(TaomGameRefs)' == ''">Install</TaomGameRefs>
    <BannerlordRefAsmVersion>1.5.3.122374-beta</BannerlordRefAsmVersion>
  </PropertyGroup>

  <PropertyGroup Condition="'$(TaomGameRefs)' == 'Install'">
    <TaomGameBin>$(GameFolder)\bin\$(GameBinariesFolder)</TaomGameBin>
    <TaomNativeModuleBin>$(GameFolder)\Modules\Native\bin\$(GameBinariesFolder)</TaomNativeModuleBin>
    <TaomSandBoxModuleBin>$(GameFolder)\Modules\SandBox\bin\$(GameBinariesFolder)</TaomSandBoxModuleBin>
    <TaomSandBoxCoreModuleBin>$(GameFolder)\Modules\SandBoxCore\bin\$(GameBinariesFolder)</TaomSandBoxCoreModuleBin>
    <TaomCustomBattleModuleBin>$(GameFolder)\Modules\CustomBattle\bin\$(GameBinariesFolder)</TaomCustomBattleModuleBin>
    <TaomSystemManagementDll>$(GameFolder)\bin\$(GameBinariesFolder)\System.Management.dll</TaomSystemManagementDll>
    <TaomSystemNumericsVectorsDll>$(GameFolder)\bin\$(GameBinariesFolder)\System.Numerics.Vectors.dll</TaomSystemNumericsVectorsDll>
  </PropertyGroup>
</Project>
```

   (MSBuild evaluates all properties before any item, so the csproj items above the `Import` see
   these values; `GameBinariesFolder` is already set by `Directory.Build.props`, which the SDK
   imports before the project body.)

2. `E:\repos\wt-010-ci-hosted\Main\TAOM.csproj`, change only these attribute strings (tabs and
   child elements unchanged):
   - `:25` `Include="$(GameFolder)\bin\$(GameBinariesFolder)\System.Management.dll"` becomes `Include="$(TaomSystemManagementDll)"`
   - `:35` `Include="$(GameFolder)\bin\$(GameBinariesFolder)\System.Numerics.Vectors.dll"` becomes `Include="$(TaomSystemNumericsVectorsDll)"` (keep `<Aliases>SNV</Aliases>`)
   - `:40` becomes `<Reference Include="$(TaomGameBin)\TaleWorlds.*.dll" Exclude="$(TaomGameBin)\TaleWorlds.Native.dll">`
   - `:44` becomes `<Reference Include="$(TaomNativeModuleBin)\*.dll">`
   - `:48` becomes `<Reference Include="$(TaomSandBoxModuleBin)\*.dll">`
   - `:52` becomes `<Reference Include="$(TaomSandBoxCoreModuleBin)\*.dll" Condition="'$(TaomSandBoxCoreModuleBin)' != ''">`
     (the RefAsm half leaves this property empty; without the condition the glob would become
     `\*.dll` at the drive root)
   - `:56` becomes `<Reference Include="$(TaomCustomBattleModuleBin)\*.dll">`
   - insert a line `	<Import Project="..\GameReferences.targets" />` (one tab) directly above the final `</Project>`.
3. `E:\repos\wt-010-ci-hosted\Dependencies\TAOM.Dependencies.csproj`: `:16-17` become
   `<Reference Include="$(TaomGameBin)\TaleWorlds.*.dll"` and
   `Exclude="$(TaomGameBin)\TaleWorlds.Native.dll;$(TaomGameBin)\TaleWorlds.CampaignSystem.dll">`
   (keep the existing line break and indentation); insert `	<Import Project="..\GameReferences.targets" />`
   directly above the final `</Project>`.
4. `E:\repos\wt-010-ci-hosted\TAOM.Tests\TAOM.Tests.csproj`: the TaleWorlds reference line becomes
   `<Reference Include="$(TaomGameBin)\TaleWorlds.*.dll" Exclude="$(TaomGameBin)\TaleWorlds.Native.dll">`;
   insert `	<Import Project="..\GameReferences.targets" />` directly above the final `</Project>`.
   Leave plan 008's `TaomGameFolder` `ItemGroup` alone.

**Verify**:
- The normalized diff of Step 0.6 with `S=3` prints `IDENTICAL` (install mode evaluates to
  the same references, every metadata value equal except the file times).
- The filtered command of Step 2 prints a summary matching `Failed:\s+0, Passed:\s+2,`.
- `dotnet build Main/TAOM.csproj -p:DisableModuleCopy=true -p:ModuleId=` exits 0.

### Step 4: add the reference-assembly half, and a clear error for a missing game

1. In `GameReferences.targets`, insert directly above the closing `</Project>`:

```xml

  <PropertyGroup Condition="'$(TaomGameRefs)' == 'RefAsm'">
    <TaomGameBin>$(NuGetPackageRoot)bannerlord.referenceassemblies.core\$(BannerlordRefAsmVersion)\ref\net472</TaomGameBin>
    <TaomNativeModuleBin>$(NuGetPackageRoot)bannerlord.referenceassemblies.native\$(BannerlordRefAsmVersion)\ref\net472</TaomNativeModuleBin>
    <TaomSandBoxModuleBin>$(NuGetPackageRoot)bannerlord.referenceassemblies.sandbox\$(BannerlordRefAsmVersion)\ref\net472</TaomSandBoxModuleBin>
    <!-- Empty on purpose: the game's SandBoxCore bin holds no DLL and BUTR publishes no SandBoxCore package. -->
    <TaomSandBoxCoreModuleBin></TaomSandBoxCoreModuleBin>
    <TaomCustomBattleModuleBin>$(NuGetPackageRoot)bannerlord.referenceassemblies.custombattle\$(BannerlordRefAsmVersion)\ref\net472</TaomCustomBattleModuleBin>
    <TaomStoryModeModuleBin>$(NuGetPackageRoot)bannerlord.referenceassemblies.storymode\$(BannerlordRefAsmVersion)\ref\net472</TaomStoryModeModuleBin>
    <TaomSystemManagementDll>$(NuGetPackageRoot)system.management\4.7.0\lib\netstandard2.0\System.Management.dll</TaomSystemManagementDll>
    <TaomSystemNumericsVectorsDll>$(NuGetPackageRoot)system.numerics.vectors\4.4.0\lib\netstandard2.0\System.Numerics.Vectors.dll</TaomSystemNumericsVectorsDll>
  </PropertyGroup>

  <!-- Download only: the Reference items above point at the package folders themselves, which keeps
       Private and the per-project exclusions exactly as in install mode. -->
  <ItemGroup Condition="'$(TaomGameRefs)' == 'RefAsm'">
    <PackageDownload Include="Bannerlord.ReferenceAssemblies.Core" Version="[$(BannerlordRefAsmVersion)]" />
    <PackageDownload Include="Bannerlord.ReferenceAssemblies.Native" Version="[$(BannerlordRefAsmVersion)]" />
    <PackageDownload Include="Bannerlord.ReferenceAssemblies.SandBox" Version="[$(BannerlordRefAsmVersion)]" />
    <PackageDownload Include="Bannerlord.ReferenceAssemblies.CustomBattle" Version="[$(BannerlordRefAsmVersion)]" />
    <PackageDownload Include="Bannerlord.ReferenceAssemblies.StoryMode" Version="[$(BannerlordRefAsmVersion)]" />
    <PackageDownload Include="System.Management" Version="[4.7.0]" />
    <PackageDownload Include="System.Numerics.Vectors" Version="[4.4.0]" />
  </ItemGroup>

  <!-- One clear error instead of hundreds of CS0246 when the references cannot resolve. -->
  <Target Name="TaomCheckGameReferences" BeforeTargets="ResolveAssemblyReferences">
    <Error Condition="'$(TaomGameRefs)' != 'Install' AND '$(TaomGameRefs)' != 'RefAsm'"
           Text="TAOM: TaomGameRefs is '$(TaomGameRefs)'; use Install (the default) or RefAsm." />
    <Error Condition="'$(TaomGameRefs)' == 'Install' AND '$(GameBinariesFolder)' == ''"
           Text="TAOM: no Bannerlord install found. GameFolder is '$(GameFolder)'; BANNERLORD_GAME_DIR (or BANNERLORD_OVERRIDE_DIR) must name a folder holding bin\Win64_Shipping_Client\Bannerlord.exe. To build without the game, pass -p:TaomGameRefs=RefAsm." />
    <Error Condition="'$(TaomGameRefs)' == 'RefAsm' AND !Exists('$(TaomGameBin)\TaleWorlds.Library.dll')"
           Text="TAOM: TaomGameRefs=RefAsm but $(TaomGameBin)\TaleWorlds.Library.dll does not exist. Restore first, and check that Bannerlord.ReferenceAssemblies.Core $(BannerlordRefAsmVersion) is published on nuget.org." />
  </Target>

  <!-- RefAsm, test project only: lay the BUTR assemblies out the way GameAssemblies expects a game
       folder (bin\Win64_Shipping_Client\Bannerlord.exe, which may be empty, plus
       Modules\<module>\bin\Win64_Shipping_Client), so the binding gate can run with no game. The CI
       binding step points BANNERLORD_GAME_DIR at this folder for that one run. -->
  <Target Name="TaomWriteRefAsmGameFolder" AfterTargets="Build"
          Condition="'$(TaomGameRefs)' == 'RefAsm' AND '$(MSBuildProjectName)' == 'TAOM.Tests'">
    <PropertyGroup>
      <_TaomRefAsmGame>$(TargetDir)refasm-game</_TaomRefAsmGame>
    </PropertyGroup>
    <ItemGroup>
      <_TaomRefAsmGameBin Include="$(TaomGameBin)\TaleWorlds.*.dll" />
      <_TaomRefAsmNative Include="$(TaomNativeModuleBin)\*.dll" />
      <_TaomRefAsmSandBox Include="$(TaomSandBoxModuleBin)\*.dll" />
      <_TaomRefAsmCustomBattle Include="$(TaomCustomBattleModuleBin)\*.dll" />
      <_TaomRefAsmStoryMode Include="$(TaomStoryModeModuleBin)\*.dll" />
    </ItemGroup>
    <Copy SourceFiles="@(_TaomRefAsmGameBin)" DestinationFolder="$(_TaomRefAsmGame)\bin\Win64_Shipping_Client" SkipUnchangedFiles="true" />
    <Touch Files="$(_TaomRefAsmGame)\bin\Win64_Shipping_Client\Bannerlord.exe" AlwaysCreate="true" />
    <Copy SourceFiles="@(_TaomRefAsmNative)" DestinationFolder="$(_TaomRefAsmGame)\Modules\Native\bin\Win64_Shipping_Client" SkipUnchangedFiles="true" />
    <Copy SourceFiles="@(_TaomRefAsmSandBox)" DestinationFolder="$(_TaomRefAsmGame)\Modules\SandBox\bin\Win64_Shipping_Client" SkipUnchangedFiles="true" />
    <Copy SourceFiles="@(_TaomRefAsmCustomBattle)" DestinationFolder="$(_TaomRefAsmGame)\Modules\CustomBattle\bin\Win64_Shipping_Client" SkipUnchangedFiles="true" />
    <Copy SourceFiles="@(_TaomRefAsmStoryMode)" DestinationFolder="$(_TaomRefAsmGame)\Modules\StoryMode\bin\Win64_Shipping_Client" SkipUnchangedFiles="true" />
    <Message Importance="high" Text="TAOM: reference-assembly game folder for the binding gate: $(_TaomRefAsmGame)" />
  </Target>
```

2. Install mode is still unchanged: the normalized diff of Step 0.6 with `S=4` prints
   `IDENTICAL`. Also
   `cd /e/repos/wt-010-ci-hosted && dotnet msbuild Main/TAOM.csproj -getItem:PackageDownload -p:DisableModuleCopy=true -p:ModuleId= | grep -c -i "bannerlord.referenceassemblies"`
   prints `0` (install mode downloads nothing new).
3. The missing-game error, safely (a temp folder with no game in it; both deploy-skip flags stay on):
   `cd /e/repos/wt-010-ci-hosted && FAKE=$(cygpath -w "$(mktemp -d)"); env -u BANNERLORD_OVERRIDE_DIR BANNERLORD_GAME_DIR="$FAKE" dotnet build Dependencies/TAOM.Dependencies.csproj -nr:false -p:DisableModuleCopy=true -p:ModuleId= > /e/repos/wt-010-logs/4-nogame.log 2>&1; echo "exit=$?"; grep -c "TAOM: no Bannerlord install found" /e/repos/wt-010-logs/4-nogame.log; grep -c "error CS0246" /e/repos/wt-010-logs/4-nogame.log`
   prints a non-zero exit, a count of 1 or more, and `0`.
4. Switch to the reference-assembly build: run the clean command from "Commands you will need", then
   `cd /e/repos/wt-010-ci-hosted && env -u BANNERLORD_GAME_DIR -u BANNERLORD_OVERRIDE_DIR dotnet build TAOM.Tests -nr:false -p:TaomGameRefs=RefAsm -p:DisableModuleCopy=true -p:ModuleId= > /e/repos/wt-010-logs/4-refasm-build.log 2>&1; echo "exit=$?"; grep -E "error |Build succeeded|refasm-game" /e/repos/wt-010-logs/4-refasm-build.log | head -20`

**Verify**:
- 4.4 prints `exit=0`, `Build succeeded`, and the `TAOM: reference-assembly game folder` line.
- The test output holds BUTR's stub, not the game's DLL:
  `cd /e/repos/wt-010-ci-hosted && GP=$(cygpath -u "$(dotnet nuget locals global-packages --list | sed 's/^global-packages: //')"); cmp TAOM.Tests/bin/Debug/net472/TaleWorlds.Library.dll "$GP/bannerlord.referenceassemblies.core/1.5.3.122374-beta/ref/net472/TaleWorlds.Library.dll" && echo STUB`
  prints `STUB`.
- `ls TAOM.Tests/bin/Debug/net472/refasm-game/bin/Win64_Shipping_Client/Bannerlord.exe TAOM.Tests/bin/Debug/net472/refasm-game/Modules/SandBox/bin/Win64_Shipping_Client/SandBox.dll TAOM.Tests/bin/Debug/net472/refasm-game/Modules/StoryMode/bin/Win64_Shipping_Client/StoryMode.dll` succeeds.
- The test assembly does not point at the install:
  `grep -rh "TaomGameFolder" TAOM.Tests/obj --include="*AssemblyInfo.cs"` prints a line whose
  second argument is `""` (empty).

### Step 5: tag the tests

Run the tagger once over the worktree:
`cd /e/repos/wt-010-ci-hosted && python /e/repos/wt-010-logs/tag_categories.py . /e/repos/wt-010-logs/manifest.txt`
It inserts `[TestCategory("<category>")]` on the line after the class's `[TestClass]` (or the
method's `[TestMethod]` / `[DataTestMethod]`), with the same indentation, preserving line endings and
BOMs. It was dry-run while planning on `git archive b2e387db TAOM.Tests`: `added=139 present=0
errors=0`, and a second run `added=0 present=139 errors=0`.

If it reports `ERROR` lines (a declaration that moved or an attribute block with a comment inside
it), add that one attribute by hand with the Edit tool, in the same position, then re-run the
tagger until it prints `errors=0`.

**Verify** (each with the worktree prefix):
- A second run prints `added=0 present=139 errors=0`.
- `git grep -h -o 'TestCategory("RequiresGame")' -- TAOM.Tests | wc -l` prints `100`;
  `git grep -l 'TestCategory("RequiresGame")' -- TAOM.Tests | wc -l` prints `92`;
  `git grep -h -o 'TestCategory("RequiresGameIL")' -- TAOM.Tests | wc -l` prints `29`;
  `git grep -h -o 'TestCategory("LiveInstall")' -- TAOM.Tests | wc -l` prints `10`.
- Only attribute lines were added to the tagged files:
  `git diff --numstat -- $(cut -d'|' -f2 /e/repos/wt-010-logs/manifest.txt | sort -u) | awk '$2 != 0'`
  prints nothing (no removed lines), and
  `git diff -- $(cut -d'|' -f2 /e/repos/wt-010-logs/manifest.txt | sort -u) | grep '^+' | grep -v '^+++' | grep -v 'TestCategory("'`
  prints nothing.
- Every file holding an `E:\Steam` literal is tagged, except the path-classifier test:
  `for f in $(git grep -l -F 'E:\Steam' -- TAOM.Tests); do grep -q 'TestCategory("LiveInstall")' "$f" || echo "UNTAGGED $f"; done`
  prints only `UNTAGGED TAOM.Tests/Features/NativeSkinFixes/NativeSkinFixesInstallerTests.cs`.

### Step 6: run the CI test steps locally, with no game in the environment

Step 5 changed test sources, so rebuild in the reference-assembly mode first:
`cd /e/repos/wt-010-ci-hosted && env -u BANNERLORD_GAME_DIR -u BANNERLORD_OVERRIDE_DIR dotnet build TAOM.Tests -nr:false -p:TaomGameRefs=RefAsm -p:DisableModuleCopy=true -p:ModuleId= > /e/repos/wt-010-logs/6-build.log 2>&1; echo "exit=$?"`

(a) Unit tests:
`cd /e/repos/wt-010-ci-hosted && env -u BANNERLORD_GAME_DIR -u BANNERLORD_OVERRIDE_DIR dotnet test TAOM.Tests --no-build -p:TaomGameRefs=RefAsm -p:DisableModuleCopy=true -p:ModuleId= --filter "TestCategory!=RequiresGame&TestCategory!=LiveInstall&TestCategory!=BindingVerification" --logger "trx;LogFileName=6a-unit.trx" --results-directory E:/repos/wt-010-logs/trx > /e/repos/wt-010-logs/6a.log 2>&1; echo "exit=$?"; grep -E 'Passed!|Failed!' /e/repos/wt-010-logs/6a.log; python /e/repos/wt-010-logs/trxsum.py E:/repos/wt-010-logs/trx/6a-unit.trx`

(b) Binding gate against the reference-assembly game folder:
`cd /e/repos/wt-010-ci-hosted && env -u BANNERLORD_OVERRIDE_DIR BANNERLORD_GAME_DIR="$(cygpath -w "$PWD/TAOM.Tests/bin/Debug/net472/refasm-game")" dotnet test TAOM.Tests --no-build -p:TaomGameRefs=RefAsm -p:DisableModuleCopy=true -p:ModuleId= --settings TAOM.Tests/binding-gate.runsettings --filter "TestCategory=BindingVerification&TestCategory!=RequiresGameIL&TestCategory!=LiveInstall" --logger "trx;LogFileName=6b-gate.trx" --results-directory E:/repos/wt-010-logs/trx > /e/repos/wt-010-logs/6b.log 2>&1; echo "exit=$?"; grep -E 'Passed!|Failed!' /e/repos/wt-010-logs/6b.log; python /e/repos/wt-010-logs/trxsum.py E:/repos/wt-010-logs/trx/6b-gate.trx`

**Expected**: (a) `exit=0`, summary `Failed:\s+0,`, and `executed` between 7,000 and 9,000 (the
audit's stub run predicts about 8,200). (b) `exit=0`, summary `Failed:\s+0,` and `Skipped:\s+0,`,
`executed` about 338 (between 300 and 368). Record both `executed` values as **E** (unit) and **G**
(gate); Step 7 derives the CI floors from them.

**If (a) or (b) shows failures**, classify each failing test from the log (`grep -A 25 "Failed <TestName>"`):

- (a) The message or stack names `NullReferenceException` with a frame `at TaleWorlds.`, `at SandBox.`
  or `at StoryMode.`, or a `FileNotFoundException`/`FileLoadException` for a `TaleWorlds.*`,
  `SandBox*` or `StoryMode*` assembly: the class executes engine code. Append
  `class|<file>|<ClassName>|RequiresGame` to `manifest.txt`, re-run the tagger, rebuild (the command
  above) and re-run (a). Record each addition for the final report.
- (b) The message shows the test needs vanilla method IL, a vanilla ModuleData or prefab file, or
  engine execution (typically: a null or empty method body, a missing file under
  `refasm-game\Modules\`, or an exception thrown from a `TaleWorlds.`, `SandBox.` or `StoryMode.`
  frame): append `method|<file>|<MethodName>|RequiresGameIL`, re-run the tagger, rebuild, re-run
  (b). Record each addition.
- A `FileNotFoundException` or `FileLoadException` for `System.Management`,
  `System.Numerics.Vectors`, `Steamworks.NET`, `GalaxyCSharp` or `StbSharp`: STOP and report the test
  and message. These five come from the game's `bin` in install mode and a RefAsm build does not
  copy them ("What BUTR publishes"); the fix is a decision for the orchestrator, not a tag.
- Anything else (an assertion about TAOM's own data, a missing repo file, a timeout): STOP and
  report the test name and message. Do not tag it to make the run green.
- More than 10 additions in total: STOP (the measurement has drifted too far; the orchestrator
  re-measures).

### Step 7: add the hosted workflow and remove the self-hosted job

1. Create `E:\repos\wt-010-ci-hosted\.github\workflows\csharp.yml` with this content, replacing
   `<UNIT_FLOOR>` with `floor(E x 0.95 / 100) x 100` and `<GATE_FLOOR>` with `floor(G x 0.95 / 10) x 10`
   (for example E = 8,214 gives 7800; G = 338 gives 320), and `<DATE>` with today's date:

```yaml
name: C# build and tests (no game)

# Compiles every C# project and runs every test that can run without Bannerlord, on a
# GitHub-hosted Windows VM, for every push and pull request. There is no game on the runner, so
# the build uses BUTR's metadata-only reference assemblies (-p:TaomGameRefs=RefAsm, see
# GameReferences.targets) for the same Steam build as .claude/pinned-game-version.txt.
#
# Two test steps:
#   Unit tests: excludes RequiresGame (the test executes engine code, and every method body in a
#   reference assembly throws), LiveInstall (reads the live Armory or the vanilla install) and
#   BindingVerification (the next step runs those strictly).
#   Binding gate: the reference assemblies laid out as a game folder (refasm-game, written by the
#   RefAsm build) with binding-gate.runsettings, so a skipped check fails. RequiresGameIL checks
#   need vanilla method bodies or vanilla data, which metadata cannot give; they run only on a
#   machine with the game (the verify-bindings skill).
#
# Why not a self-hosted runner: the repository is public and contributor branches live in it, and
# a guard in this file cannot protect a runner from a branch that edits this file. A hosted VM is
# discarded after each run. .ai/policy.md forbids untrusted code on the personal game workstation.
#
# dotnet test exits 0 when a filter matches nothing, so each test step asserts a floor on the
# number of tests it executed. The floors were set about 5% below a local run of these exact
# steps on <DATE>.

on:
  push:
    branches: [bannerlord-1.5.x, bannerlord-1.4.5]
  pull_request:
    branches: [bannerlord-1.5.x, bannerlord-1.4.5]
  workflow_dispatch:

permissions:
  contents: read

env:
  DOTNET_NOLOGO: true
  DOTNET_CLI_TELEMETRY_OPTOUT: true

jobs:
  csharp:
    name: C# build and tests (reference assemblies)
    runs-on: windows-latest
    timeout-minutes: 30
    steps:
      - uses: actions/checkout@v4

      - uses: actions/setup-dotnet@v4
        with:
          dotnet-version: '10.0.x'

      - name: Build against reference assemblies
        id: build
        shell: pwsh
        run: |
          dotnet build TAOM.Tests -p:TaomGameRefs=RefAsm -p:DisableModuleCopy=true -p:ModuleId=
          exit $LASTEXITCODE

      - name: Unit tests (no game)
        id: unit
        if: ${{ !cancelled() && steps.build.outcome == 'success' }}
        shell: pwsh
        run: |
          dotnet test TAOM.Tests --no-build -p:TaomGameRefs=RefAsm -p:DisableModuleCopy=true -p:ModuleId= --filter "TestCategory!=RequiresGame&TestCategory!=LiveInstall&TestCategory!=BindingVerification" --logger "trx;LogFileName=unit.trx" --results-directory TestResults
          $rc = $LASTEXITCODE
          $c = ([xml](Get-Content -Raw TestResults/unit.trx)).TestRun.ResultSummary.Counters
          Write-Host "unit: total=$($c.total) executed=$($c.executed) passed=$($c.passed) failed=$($c.failed)"
          $floor = <UNIT_FLOOR>
          if ([int]$c.executed -lt $floor) { Write-Host "::error::Only $($c.executed) unit tests executed, expected at least $floor. A filter or discovery problem is not a pass."; exit 1 }
          exit $rc

      - name: Binding gate on reference assemblies (a skip fails)
        id: gate
        if: ${{ !cancelled() && steps.build.outcome == 'success' }}
        shell: pwsh
        run: |
          $game = Join-Path $PWD 'TAOM.Tests/bin/Debug/net472/refasm-game'
          if (-not (Test-Path (Join-Path $game 'bin/Win64_Shipping_Client/Bannerlord.exe'))) { Write-Host "::error::$game was not written by the RefAsm build (GameReferences.targets, TaomWriteRefAsmGameFolder)."; exit 1 }
          $env:BANNERLORD_GAME_DIR = $game
          dotnet test TAOM.Tests --no-build -p:TaomGameRefs=RefAsm -p:DisableModuleCopy=true -p:ModuleId= --settings TAOM.Tests/binding-gate.runsettings --filter "TestCategory=BindingVerification&TestCategory!=RequiresGameIL&TestCategory!=LiveInstall" --logger "trx;LogFileName=gate.trx" --results-directory TestResults
          $rc = $LASTEXITCODE
          $c = ([xml](Get-Content -Raw TestResults/gate.trx)).TestRun.ResultSummary.Counters
          Write-Host "gate: total=$($c.total) executed=$($c.executed) passed=$($c.passed) failed=$($c.failed)"
          $floor = <GATE_FLOOR>
          if ([int]$c.executed -lt $floor) { Write-Host "::error::Only $($c.executed) binding checks executed, expected at least $floor. A filter or discovery problem is not a pass."; exit 1 }
          exit $rc

      - name: Keep the test results
        if: ${{ always() }}
        uses: actions/upload-artifact@v4
        with:
          name: test-results
          path: TestResults/*.trx
          if-no-files-found: ignore
```

2. In `E:\repos\wt-010-ci-hosted\.github\workflows\build.yml`, delete the whole `check-build-config`
   job (from the line `  check-build-config:` through the line before `  validate-xml:`, which at
   `b2e387db` is `:15-26` including the trailing blank line) and the whole `build` job (from
   `  build:` to the end of the file, `:238-278` at `b2e387db` plus the step plan 008 appended).
   Leave the file ending with the `python-tests` job's last line (`          exit $RC`) and one
   newline. Change nothing else.
3. Replay the workflow's three steps locally, exactly as written in the YAML. Write
   `E:\repos\wt-010-logs\extract_steps.py` with the Write tool:

```python
import sys
import yaml

d = yaml.safe_load(open(sys.argv[1], encoding="utf-8"))
for step in d["jobs"]["csharp"]["steps"]:
    if step.get("id") in ("build", "unit", "gate"):
        path = f"{sys.argv[2]}/step-{step['id']}.ps1"
        open(path, "w", encoding="utf-8").write(step["run"])
        print("wrote", path)
```

   then run the clean command from "Commands you will need", stop any MSBuild worker nodes that
   still hold the game variables (`dotnet build-server shutdown`), and:
   `cd /e/repos/wt-010-ci-hosted && python /e/repos/wt-010-logs/extract_steps.py .github/workflows/csharp.yml E:/repos/wt-010-logs`
   `cd /e/repos/wt-010-ci-hosted && for s in build unit gate; do env -u BANNERLORD_GAME_DIR -u BANNERLORD_OVERRIDE_DIR pwsh -NoProfile -File E:/repos/wt-010-logs/step-$s.ps1 > /e/repos/wt-010-logs/7-$s.log 2>&1; echo "$s exit=$?"; done; grep -hE '^(unit|gate): ' /e/repos/wt-010-logs/7-unit.log /e/repos/wt-010-logs/7-gate.log`

**Verify**:
- `grep -c '<UNIT_FLOOR>\|<GATE_FLOOR>\|<DATE>' .github/workflows/csharp.yml` prints `0`.
- `python -c "import yaml; d=yaml.safe_load(open('.github/workflows/csharp.yml', encoding='utf-8')); on=d.get('on', d.get(True)); print(sorted(on['push']['branches']), [s.get('id') for s in d['jobs']['csharp']['steps'] if s.get('id')], d['permissions'])"`
  prints `['bannerlord-1.4.5', 'bannerlord-1.5.x'] ['build', 'unit', 'gate'] {'contents': 'read'}`
  (PyYAML reads the key `on` as `True`, hence the fallback).
- `python -c "import yaml; d=yaml.safe_load(open('.github/workflows/build.yml', encoding='utf-8')); print(sorted(d['jobs']))"`
  prints `['hook-harness', 'python-tests', 'validate-xml']`.
- `grep -c -i "self-hosted\|BANNERLORD_GAME_DIR" .github/workflows/build.yml` prints `0`.
- The replay prints `build exit=0`, `unit exit=0`, `gate exit=0`, and the `unit:` and `gate:`
  lines show `failed=0` with `executed` equal to E and G from Step 6.
- `git diff --stat -- .github/workflows/build.yml` shows only deletions.

### Step 8: correct the two docs that describe the old situation

1. `E:\repos\wt-010-ci-hosted\.ai\verification.md`, replace lines 21-23, which read

   ```text
   Do not use the deploying default of `build.ps1` as a review-time check. The game
   and .NET Framework 4.7.2 targeting pack are local dependencies, not provided by a
   generic hosted runner. Missing prerequisites mean not run, not passed.
   ```

   with

   ```text
   Do not use the deploying default of `build.ps1` as a review-time check. Without
   the game, add `-p:TaomGameRefs=RefAsm` to build against BUTR's metadata-only
   reference assemblies (`GameReferences.targets`, as `.github/workflows/csharp.yml`
   does). Such a run cannot execute the tests tagged `RequiresGame`,
   `RequiresGameIL` or `LiveInstall`, so it is partial evidence. Missing
   prerequisites mean not run, not passed.
   ```

2. `E:\repos\wt-010-ci-hosted\.claude\rules\tests.md`, insert directly above the line
   `## Test Organization`:

   ```markdown
   ## Test categories (hosted CI)

   `.github/workflows/csharp.yml` builds against metadata-only reference assemblies, where every
   engine method body throws. Tag the class `[TestCategory("RequiresGame")]` when a test executes
   engine code (constructing a `Vec2`, `TextObject`, `ExplainedNumber` or `CampaignBehaviorBase`
   counts, even through TAOM code), and `[TestCategory("LiveInstall")]` when it reads the live
   Armory or the vanilla install. A binding test that needs vanilla method IL or vanilla data
   carries `RequiresGameIL` on the method. An untagged test that executes engine code fails on CI
   with a `NullReferenceException` from a `TaleWorlds` frame: tag it, never catch the exception.

   ```

**Verify**:
- `python tools/reviewctl.py lint; echo "exit=$?"` and
  `python -m unittest tools.tests.test_reviewctl tools.tests.test_ai_documentation 2>&1 | tail -3`
  match the Step 0.9 record.
- `python tools/lint_docs.py --summary`: `dead_links:` no higher and `ai_dashes:` equal to Step 0.9;
  `python tools/lint_docs.py --fail-on-drift; echo "exit=$?"` prints `exit=0`.
- `wc -c < .claude/rules/tests.md` prints a number below 12288.

### Step 9: back to the install build: nothing local changed

Run the clean command, then:
`cd /e/repos/wt-010-ci-hosted && dotnet build Main/TAOM.csproj -p:DisableModuleCopy=true -p:ModuleId= > /e/repos/wt-010-logs/9-build.log 2>&1; echo "exit=$?"`
`cd /e/repos/wt-010-ci-hosted && dotnet test TAOM.Tests -p:DisableModuleCopy=true -p:ModuleId= --logger "trx;LogFileName=9-full.trx" --results-directory E:/repos/wt-010-logs/trx > /e/repos/wt-010-logs/9-full.log 2>&1; echo "exit=$?"; grep -E 'Passed!|Failed!' /e/repos/wt-010-logs/9-full.log; python /e/repos/wt-010-logs/trxsum.py E:/repos/wt-010-logs/trx/9-full.trx | tee /e/repos/wt-010-logs/9-sum.txt; sed -n 's/^FAILED //p' /e/repos/wt-010-logs/9-sum.txt > /e/repos/wt-010-logs/9-failed.txt; grep -vxF -f /e/repos/wt-010-logs/0-failed.txt /e/repos/wt-010-logs/9-failed.txt | sed 's/^/NEW-FAILURE /'`
`cd /e/repos/wt-010-ci-hosted && dotnet test TAOM.Tests --no-build -p:DisableModuleCopy=true -p:ModuleId= --settings TAOM.Tests/binding-gate.runsettings --filter "TestCategory=BindingVerification" > /e/repos/wt-010-logs/9-gate.log 2>&1; echo "exit=$?"; grep -E 'Passed!|Failed!' /e/repos/wt-010-logs/9-gate.log`

**Verify**: the build exits 0; the full run's `total=` is the Step 0.7 total plus 2, and no
`NEW-FAILURE` line is printed (its failures are the Step 0.7 record or a subset, if the Armory work
landed meanwhile); the strict
gate with the real game prints `exit=0` and a summary matching `Failed:\s+0,` and `Skipped:\s+0,`
(the 29 `RequiresGameIL` checks still run and pass here). Also
`cmp -s TAOM.Tests/bin/Debug/net472/TaleWorlds.Library.dll "$(cygpath -u "$BANNERLORD_GAME_DIR")/bin/Win64_Shipping_Client/TaleWorlds.Library.dll" && echo REAL`
prints `REAL`.

### Step 10: CHANGELOG entry

In `E:\repos\wt-010-ci-hosted\CHANGELOG.md` (the worktree's copy; never the main tree's), under the
`## <today's date, YYYY-MM-DD>` heading at the top (create it directly below the archive note if
today has none), add as the first entry of that day, with the heading equal to your commit subject:

```markdown
### ci(tests): v2.0.30 - build and test C# on hosted Windows runners

- **CI compiles C# again, with no game and no workstation.** No job compiled TAOM on any branch:
  the C# job needed a self-hosted runner that was never registered and ran only for
  `bannerlord-1.4.5`. The new `.github/workflows/csharp.yml` runs on GitHub-hosted Windows for
  every push and pull request on `bannerlord-1.5.x` and `bannerlord-1.4.5`. It builds against
  BUTR's metadata-only reference assemblies for the pinned Steam build, runs the unit tests that
  need no game (E executed locally) and the binding gate against those assemblies laid out as a
  game folder (G checks, skips fail). The self-hosted job and its warning are gone.
- **`GameReferences.targets` owns the game references.** `-p:TaomGameRefs=RefAsm` switches all
  three projects to the reference assemblies; the default, `Install`, evaluates to exactly the
  references the projects had before. A build with no install now stops with one error naming
  `BANNERLORD_GAME_DIR` instead of hundreds of CS0246. `GameReferencesTargetsTests` pins the BUTR
  build to `.claude/pinned-game-version.txt`: bump both on an engine bump.
- **Three test categories, used only by CI.** `RequiresGame` (100 classes that execute engine
  code), `RequiresGameIL` (29 binding checks that need vanilla IL or data) and `LiveInstall` (10
  classes that read the live Armory or the vanilla install). Local runs are unchanged;
  `.claude/rules/tests.md` says when to add each.
```

Replace `E` and `G` with the numbers from Step 6, and `v2.0.30` if the version you read differs.

**Verify**: `git diff -- CHANGELOG.md | grep -c '^+### ci(tests)'` prints `1`.

### Step 11: final verification, then commit

Every command carries the `cd /e/repos/wt-010-ci-hosted && ` prefix. The worktree is in install
mode after Step 9.

1. `dotnet build Main/TAOM.csproj -p:DisableModuleCopy=true -p:ModuleId=` exits 0.
2. `dotnet test TAOM.Tests -p:DisableModuleCopy=true -p:ModuleId= --filter "FullyQualifiedName~GameReferencesTargetsTests"` prints a summary matching `Failed:\s+0, Passed:\s+2,`.
3. `python tools/validate_moduledata.py > /e/repos/wt-010-logs/11-data.log 2>&1; echo "exit=$?"; tail -3 /e/repos/wt-010-logs/11-data.log` matches Step 0.9 (this plan touches no ModuleData).
4. Location check: `git rev-parse --show-toplevel --abbrev-ref HEAD` prints `E:/repos/wt-010-ci-hosted` and `plan-010-ci-hosted`.
   Then record the main tree's index before you stage anything:
   `git -C E:/repos/TAOM diff --cached --name-only > /e/repos/wt-010-logs/11-main-cached-before.txt`
   (it may be empty; that is fine).
5. Stage explicitly:
   `git add GameReferences.targets Main/TAOM.csproj Dependencies/TAOM.Dependencies.csproj TAOM.Tests/TAOM.Tests.csproj TAOM.Tests/Infrastructure/GameReferencesTargetsTests.cs .github/workflows/csharp.yml .github/workflows/build.yml .ai/verification.md .claude/rules/tests.md CHANGELOG.md`
   then `git add $(cut -d'|' -f2 /e/repos/wt-010-logs/manifest.txt | sort -u)`.
6. Dash check on the staged prose and config:
   `git diff --cached -U0 -- . ':(exclude)*.cs' | python -c "import sys; t=sys.stdin.buffer.read().decode('utf-8','replace'); bad=[l for l in t.splitlines() if l.startswith('+') and ('\u2014' in l or '\u2013' in l)]; print('\n'.join(bad)); sys.exit(1 if bad else 0)"`
   exits 0.
7. `git status --short` shows only staged paths (`A ` or `M `), 130 of them at the planned-at
   commit (10 named files plus the 120 test files, plus any Step 6 additions), and nothing unstaged
   or untracked (build output and `TestResults/` are git-ignored).
8. Write the commit message with the Write tool to `E:\repos\wt-010-logs\commit-msg.txt`: the
   subject from "Git workflow", a blank line, a short body (what changed and why; E, G and the
   Step 7 replay exit codes as evidence; that the hosted run is owed), a blank line, the
   `Not-tested:` trailer. Repeat the location check, then
   `git commit -F E:/repos/wt-010-logs/commit-msg.txt`
   (the `E:/` form: the commit-subject gate reads the file from Python while its working directory
   is the main tree). No push. If a gate denies, STOP and report the reason verbatim.

**Verify**: `git log -1 --format=%s` prints the subject; `git show --stat HEAD | tail -1` reports
130 files changed (plus Step 6 additions); `git log -1 --format=%B | grep -ci "co-authored-by"`
prints `0`; `git -C E:/repos/TAOM diff --cached --name-only | diff /e/repos/wt-010-logs/11-main-cached-before.txt - && echo MAIN-INDEX-UNCHANGED`
prints `MAIN-INDEX-UNCHANGED`.

## Test plan

- **New tests** in `TAOM.Tests/Infrastructure/GameReferencesTargetsTests.cs` (2):
  `BannerlordRefAsmVersion_PinnedGameVersion_IsTheSameGameBuild` (the BUTR version starts with the
  pinned version, and is set once) and `GameProjects_EveryGameReference_ComesFromGameReferencesTargets`
  (each of the three projects imports the targets file once and spells no `$(GameFolder)` in a
  `Reference`). Pattern for the XML reads: `TAOM.Tests/Infrastructure/Dependencies/BundledDependencyManifestTests.cs:50-51`;
  paths come from `RepoPaths`. Both read repo files only, so they run on CI too.
- **Equivalence proof** (Steps 3 and 4): install-mode `Reference` items, normalized by
  `normrefs.py` (file-time keys removed), identical to the baseline for all three projects.
- **No-game proof** (Steps 4, 6 and 7): the reference-assembly build succeeds with both game
  variables removed; the test output holds BUTR's stubs; the unit filter and the strict gate pass;
  the workflow's own `run:` blocks replayed through `pwsh` exit 0.
- **Local parity** (Step 9): the install build and full suite match the Step 0.7 record plus 2; the
  strict gate with the real game is green, including the 29 `RequiresGameIL` checks.
- **Structurally untestable here:** the GitHub-hosted run itself (runner image, `setup-dotnet`,
  the implicit net472 targeting package, nuget.org restore on the VM). It is owed after the
  orchestrator pushes: the first run's `unit:` and `gate:` lines must show `failed=0` and the local
  E and G. Name it in the `Not-tested:` trailer.

## Done criteria

ALL must hold, each run with the `cd /e/repos/wt-010-ci-hosted && ` prefix:

- [ ] `dotnet build Main/TAOM.csproj -p:DisableModuleCopy=true -p:ModuleId=` exits 0 (install mode).
- [ ] The normalized diff of Step 0.6 with `S=11` prints `IDENTICAL` after all edits.
- [ ] `dotnet test TAOM.Tests -p:DisableModuleCopy=true -p:ModuleId= --filter "FullyQualifiedName~GameReferencesTargetsTests"` prints `Failed:\s+0, Passed:\s+2,`.
- [ ] Step 9's full run printed no `NEW-FAILURE` line; its `total=` is the Step 0.7 total + 2.
- [ ] Step 7's replay printed `build exit=0`, `unit exit=0`, `gate exit=0`, with `failed=0` on both
      summary lines.
- [ ] `git grep -h -o 'TestCategory("RequiresGame")' -- TAOM.Tests | wc -l` is `100` (plus Step 6
      additions), `RequiresGameIL` is `29` (plus additions), `LiveInstall` is `10`.
- [ ] The `python -c` YAML checks of Step 7 print the expected job lists, and
      `grep -c -i "self-hosted\|BANNERLORD_GAME_DIR" .github/workflows/build.yml` prints `0`.
- [ ] `git grep -n RunSettingsFilePath -- '*.csproj' '*.props' '*.targets'` prints nothing.
- [ ] `git show --stat HEAD` lists only in-scope files; `Directory.Build.props`, `Main/IoC.cs`,
      `Main/SubModule.cs` are not among them; nothing is pushed (`git status -sb` shows no upstream).
- [ ] `python tools/lint_docs.py --fail-on-drift` exits 0.

## STOP conditions

Stop and report (do not improvise) if:

- Step 0.3 fails: plan 008 is not in the worktree.
- The drift check shows a change beyond plan 008's to an in-scope file and its excerpt no longer
  matches.
- Step 3 or 4's normalized diff is not `IDENTICAL`: install mode changed. Do not "fix" the
  baseline, and do not widen `normrefs.py` beyond the `*Time` keys. (A diff of the raw
  `*-refs-*.json` files always differs in `AccessedTime`; that alone is not a STOP.)
- The reference-assembly restore fails with `NU1101`/`NU1102` (package or version not found) or
  cannot reach nuget.org: environment or publication fact; report it.
- The reference-assembly build fails with compiler errors (`error CS...`): report the first 20
  distinct errors verbatim. Do not change production C#, do not add `#if`, do not change
  `LangVersion`, and do not reference the install as a fallback.
- The build needs `Directory.Build.props`, `Main/IoC.cs` or `Main/SubModule.cs` changed, or any
  `Main/TAOM.csproj` / `Dependencies/TAOM.Dependencies.csproj` edit beyond Step 3's list.
- Step 4's stub check does not print `STUB` (the test output holds the game's DLLs in RefAsm mode),
  or the `TaomGameFolder` line is not empty.
- Step 0.7's check prints a `NOT-LIVEINSTALL` line (a baseline failure that is not a `LiveInstall`
  class of the manifest).
- Step 6 finds a failure outside the two tagging rules (including a load failure for one of the
  five non-TaleWorlds game DLLs), or more than 10 additions.
- Step 6 (b) or the replayed gate shows any `Skipped:` count above 0 or an `executed` below 300.
- Step 9's full run shows a failure not in the Step 0.7 record, or the strict gate with the real
  game is not green.
- You find yourself setting `MapInconclusiveToFailed` for the default suite, adding a self-hosted
  runner, committing any DLL, or pointing CI at a game path: each overturns a recorded decision.
- Any command shows `E:/repos/TAOM` as its toplevel or `bannerlord-1.5.x` as its branch when it
  should be the worktree, or you notice an edit under `E:\repos\TAOM\`.
- A commit gate denies; `BANNERLORD_GAME_DIR` is unset in your shell; `dotnet`, `pwsh` or PyYAML is
  missing (environment facts: report, do not fix).
- A step's verification fails twice after a reasonable fix attempt.

## Maintenance notes

- **Engine bumps.** `/engine-bump` changes `.claude/pinned-game-version.txt`; the new
  `GameReferencesTargetsTests` then fails until `BannerlordRefAsmVersion` names the BUTR build of
  the new version. BUTR generates packages from Steam builds; the audit's checker read in BUTR's
  README that its registry checks for new builds every three hours, and found `1.5.3.122374-beta`
  on nuget.org dated the same day the desktop's install updated (not re-read while planning). If
  BUTR has not published yet, CI stays red on that test until it does. The
  fallback (building metadata-only assemblies from the install and publishing them to a private
  feed) is a licensing decision for the maintainer.
- **The tags drift.** The `RequiresGame` set was measured at `b2e387db`. A new test that executes
  engine code fails on CI with a stub `NullReferenceException`; the fix is a tag (the rule in
  `.claude/rules/tests.md`). Another session's untracked tests (`Features/Animalia/AnimaliaAttackServiceTests.cs`,
  `AnimaliaConfigTests.cs`, `AnimaliaWiringTests.cs`, `Features/ElephantLike/ElephantLikeReachTests.cs`,
  `Features/MonsterSize/`) were not measured; expect to tag some when they land. The five tagged
  files recorded in Step 0.8 have uncommitted edits in the main tree: merge after that work lands
  (the tagger is idempotent and can be re-run on the merged tree).
- **What CI still does not cover.** Eleven non-`BindingVerification` files gate on
  `GameAssemblies.EnsureLoaded` (for example `BattleBalance/PartyOwnerGetterBanTests.cs`,
  `PlayerSwitcher/InformationManagerClearBanTests.cs`, `DevConsole/ConsoleCommandBindingTests.cs`)
  and skip in the unit step because no game folder is set there; they still run locally. Class-level
  `RequiresGame` also drops about 288 results that pass on stubs. Only Debug is compiled.
- **Not a merge gate yet.** `bannerlord-1.5.x` has no branch protection (the audit read
  `protected: false`). Making the `C# build and tests (reference assemblies)` check required is a
  GitHub setting for the maintainer, outside the repo.
- **Port.** `bannerlord-1.4.5` gets hosted CI only when this lands there too, with
  `BannerlordRefAsmVersion` `1.4.8.119303` (never compared with a 1.4.8 install) and its own tag
  measurement. The separately red non-C# jobs on `bannerlord-1.4.5` are a different item.
- **Follow-ups deliberately left out:** the `CI/CD` row of `docs/reference/feature-map.md:117`
  (another session is editing that file); a `/verify` step that runs `LiveInstall` by name; NuGet
  caching or lock files; a sparse checkout (the HEAD tree is about 1.2 GB, mostly art); a
  `workflow_dispatch` input for a newer BUTR build (the audit's own checker refuted the value of
  running it ahead of Steam).
- **Reviewer focus:** that install mode is unchanged (the normalized `-getItem` diffs; only the
  `*Time` keys are dropped); the SandBoxCore
  condition (an empty glob root must never reach `\*.dll`); that `RefAsm` is never selected
  automatically; that the unit step uses default settings and only the gate step passes
  `binding-gate.runsettings`; that no tag was added to make an unexplained failure pass; the floor
  arithmetic; no dashes in the XML, YAML and markdown prose.
- The worktree and `E:\repos\wt-010-logs\` are left for the orchestrator to remove after merge.

## Appendix A: `E:\repos\wt-010-logs\manifest.txt` (139 lines)

Fields: kind, file, class or method, category. Measured at `b2e387db`: the `RequiresGame` rows are
the 100 classes whose tests newly failed with BUTR's assemblies in the test output; the
`RequiresGameIL` rows are the binding checks that failed against the BUTR game folder (the 30th,
in `BannerBearerReplacementWeaponDataTests`, is covered by its class's `LiveInstall`).

```text
class|TAOM.Tests/Core/CultureRaceConsistencyTests.cs|CultureRaceConsistencyTests|LiveInstall
class|TAOM.Tests/Core/LordFamilyTransformTests.cs|LordFamilyTransformTests|LiveInstall
class|TAOM.Tests/Features/Animalia/AnimaliaMountWiringTests.cs|AnimaliaMountWiringTests|LiveInstall
class|TAOM.Tests/Features/BannerBearers/BannerBearerReplacementWeaponDataTests.cs|BannerBearerReplacementWeaponDataTests|LiveInstall
class|TAOM.Tests/Features/Elephant/HowdahCrewLoadoutTests.cs|HowdahCrewLoadoutTests|LiveInstall
class|TAOM.Tests/Features/Elephant/HowdahHarnessItemTests.cs|HowdahHarnessItemTests|LiveInstall
class|TAOM.Tests/Features/Elephant/HowdahPrefabTests.cs|HowdahPrefabTests|LiveInstall
class|TAOM.Tests/Features/Elephant/LegacyHowdahPrefabTests.cs|LegacyHowdahPrefabTests|LiveInstall
class|TAOM.Tests/Features/Elk/ElkConfigTests.cs|ElkConfigTests|LiveInstall
class|TAOM.Tests/Features/Mumakil/MumakilPlatformTests.cs|MumakilPlatformTests|LiveInstall
method|TAOM.Tests/Features/WandererAllegiance/WandererAllegianceBindingTests.cs|AddHeroGeneralConversations_StillEmitsTheCompanionHireToken|RequiresGameIL
method|TAOM.Tests/Features/BanditManagement/Patch86HideoutBossFightBindingTests.cs|AmbushPadding_StillDrawsFromTheTypeCache|RequiresGameIL
method|TAOM.Tests/Features/Diplomacy/Patch80KingdomVoteDeadlockBindingTests.cs|ApplySelection_StillGatesOnIsCancelled|RequiresGameIL
method|TAOM.Tests/Features/LocalizationOverride/GlobalStringsOverridesTests.cs|Apply_ReplacesVanillasRowInPlace_AndAddsTaomOnlyRows|RequiresGameIL
method|TAOM.Tests/Features/LocalizationOverride/GlobalStringsOverridesTests.cs|Apply_TheRealGlobalStrings_MakesEveryAsoKingdomResolveOverVanilla|RequiresGameIL
method|TAOM.Tests/Migration/TranspilerSiteBindingTests.cs|BannerColorTranspiler_FindsBothFactionColourSites_InInstalledEngine|RequiresGameIL
method|TAOM.Tests/Features/CultureDoctrine/DoctrineSwitchInvariantTests.cs|BehaviorWeightApplier_TargetsOnlyBehavioursTeamAIGeneralRegisters|RequiresGameIL
method|TAOM.Tests/Features/BanditManagement/Patch86HideoutBossFightBindingTests.cs|BossPhaseCap_StillFeedsTheCampaignTrim|RequiresGameIL
method|TAOM.Tests/Features/LordPartyTemplates/Patch88LordPartyTemplateTests.cs|BothVanillaReads_StillGoThroughTheClanGetter|RequiresGameIL
method|TAOM.Tests/Features/MarriageAlignment/MarriageAlignmentBindingTests.cs|CheckNpcMarriages_ReadsClanAll_ExactlyTwice|RequiresGameIL
method|TAOM.Tests/Features/LocalizationOverride/GlobalStringsOverridesTests.cs|Engine_AddVariationWithId_AppendsAndTheFirstRowWins|RequiresGameIL
method|TAOM.Tests/Core/LordInlineSkillParityTests.cs|EveryInlineSkillBlockBesideATemplate_MatchesThatSkillSet|RequiresGameIL
method|TAOM.Tests/Migration/PrefabElementTypeBindingTests.cs|EveryPrefabElement_ResolvesToAWidgetTypeOrAPrefab|RequiresGameIL
method|TAOM.Tests/Migration/PrefabExtensionBindingTests.cs|EveryPrefabExtension_XPath_ResolvesAgainstTheWinningPrefab|RequiresGameIL
method|TAOM.Tests/Migration/PrefabCloneWidgetReferenceTests.cs|EveryShadowedVanillaWidgetReference_SurvivesInTheTaomClone|RequiresGameIL
method|TAOM.Tests/Migration/XsltTemplateCoverageTests.cs|EveryXsltTemplate_MatchesSomethingInTheVanillaFileItTransforms|RequiresGameIL
method|TAOM.Tests/Features/Diplomacy/Patch80KingdomVoteDeadlockBindingTests.cs|ExecuteDone_StillReadsChosenOutcomeText_WhichIsWhySeamBDoesNotCallIt|RequiresGameIL
method|TAOM.Tests/Features/CultureDoctrine/CultureDoctrinePhaseCBindingTests.cs|FormationAI_TickOnlyTicksTheActiveBehaviour_AndActivationCancelsTheOld|RequiresGameIL
method|TAOM.Tests/Features/UncapturableHeroes/UncapturableHeroesBindingTests.cs|MapEvent_StillFallsThroughToTheFugitiveAction|RequiresGameIL
method|TAOM.Tests/Features/AdvancedStartOptions/TaomStartOptionsProviderTests.cs|MergedOptions_VanillaThenTaom_CarryEveryTaomKingdomAndNoUnitedEmpire|RequiresGameIL
method|TAOM.Tests/Features/CultureDoctrine/CultureDoctrinePhaseCBindingTests.cs|MoraleModel_Seams_AndTheirCaller_StillExist|RequiresGameIL
method|TAOM.Tests/Migration/TranspilerSiteBindingTests.cs|PartyIconScale_IconSites_StillResolve_InInstalledEngine|RequiresGameIL
method|TAOM.Tests/Migration/TranspilerSiteBindingTests.cs|PartyIconScale_RelocatedPeopleSite_StillResolves_InInstalledEngine|RequiresGameIL
method|TAOM.Tests/Features/AdvancedStartOptions/TaomStartOptionsProviderTests.cs|Provider_BindsTheWayTheEngineDiscoversIt|RequiresGameIL
method|TAOM.Tests/Features/Diplomacy/Patch80KingdomVoteDeadlockBindingTests.cs|ReadyToAiChoose_StillRunsInsideStartElection_WhichIsWhatSeamDCatches|RequiresGameIL
method|TAOM.Tests/Features/CultureDoctrine/CultureDoctrinePhaseCBindingTests.cs|SergeantBehaviourText_IsLookedUpByTypeName|RequiresGameIL
method|TAOM.Tests/Features/CultureDoctrine/CultureDoctrineBindingTests.cs|TacticCharge_IsTheTypeMakeDecisionFallsBackTo|RequiresGameIL
method|TAOM.Tests/Features/CultureDoctrine/CultureDoctrinePhaseCBindingTests.cs|TroopClassOverride_IsAFuncEvent_AndTheEngineConsultsItFirst|RequiresGameIL
method|TAOM.Tests/Features/ReturnToArmy/Patch87ReturnToArmyTests.cs|VanillaConsequence_StillReachesTheWaitMenuSwitchAndTheVillageLeave|RequiresGameIL
class|TAOM.Tests/Adapters/MissionAdapterFactoryTests.cs|MissionAdapterFactoryTests|RequiresGame
class|TAOM.Tests/Features/AdvancedCombat/CreatureImpactSoundTests.cs|CreatureImpactSoundTests|RequiresGame
class|TAOM.Tests/Features/AdvancedCombat/CustomAttacksUtilsBlowFlagsTests.cs|CustomAttacksUtilsBlowFlagsTests|RequiresGame
class|TAOM.Tests/Features/AdvancedCombat/CustomAttacksUtilsTests.cs|CustomAttacksUtilsTests|RequiresGame
class|TAOM.Tests/Features/AdvancedCombat/SpatialGridRemovalTests.cs|SpatialGridRemovalTests|RequiresGame
class|TAOM.Tests/Features/AdvancedCombat/SyntheticBlowScopeTests.cs|SyntheticBlowScopeTests|RequiresGame
class|TAOM.Tests/Features/AiPartySize/AiPartySizeServiceTests.cs|AiPartySizeGatingTests|RequiresGame
class|TAOM.Tests/Features/AiPartySize/AiPartySizeServiceTests.cs|AiPartySizePlayerClanGatingTests|RequiresGame
class|TAOM.Tests/Features/AiPartySize/AiPartySizeServiceTests.cs|AiPartySizeReliefTests|RequiresGame
class|TAOM.Tests/Features/AiPartySize/AiPartySizeServiceTests.cs|AiPartySizeScalingModeTests|RequiresGame
class|TAOM.Tests/Features/AiPartySize/AiPartySizeServiceTests.cs|AiPartySizeScalingTests|RequiresGame
class|TAOM.Tests/Features/AiPartySize/AiPartySizeServiceTests.cs|AiPartySizeShippedDefaultsTests|RequiresGame
class|TAOM.Tests/Features/AiPartySize/AiPartySizeServiceTests.cs|AiPartySizeTakeoverDetectionTests|RequiresGame
class|TAOM.Tests/Features/AiPartySize/CaravanPartySizeTests.cs|CaravanPartySizeTests|RequiresGame
class|TAOM.Tests/Features/Arena/TournamentServiceTests.cs|TournamentServiceTests|RequiresGame
class|TAOM.Tests/Features/AutoResolveDiagnostics/AutoResolveDiagnosticsBehaviorTests.cs|AutoResolveDiagnosticsBehaviorTests|RequiresGame
class|TAOM.Tests/Features/AutoResolveDiagnostics/AutoResolveDiagnosticsWiringTests.cs|AutoResolveDiagnosticsWiringTests|RequiresGame
class|TAOM.Tests/Features/BanditManagement/TaomBanditDensityModelTests.cs|TaomBanditDensityModelTests|RequiresGame
class|TAOM.Tests/Features/BattleLoadDiagnostics/MemoryStationSamplerTests.cs|MemoryStationSamplerTests|RequiresGame
class|TAOM.Tests/Features/CareerSystem/CareerAgentStatServiceTests.cs|CareerAgentStatServiceTests|RequiresGame
class|TAOM.Tests/Features/CareerSystem/CareerChoiceObjectVMTests.cs|CareerChoiceObjectVMTests|RequiresGame
class|TAOM.Tests/Features/CareerSystem/CareerPassiveServiceTests.cs|CareerPassiveServiceTests|RequiresGame
class|TAOM.Tests/Features/CareerSystem/CareerPerkOffThreadTests.cs|CareerPerkOffThreadTests|RequiresGame
class|TAOM.Tests/Features/CareerSystem/CareerPersistenceTests.cs|CareerPersistenceTests|RequiresGame
class|TAOM.Tests/Features/CareerSystem/CareerScreenVMTests.cs|CareerScreenVMTests|RequiresGame
class|TAOM.Tests/Features/CareerSystem/TaomCareerHotKeyCategoryTests.cs|TaomCareerHotKeyCategoryTests|RequiresGame
class|TAOM.Tests/Features/CharacterCreation/CareerMenuServiceTests.cs|CareerMenuServiceTests|RequiresGame
class|TAOM.Tests/Features/CompanionTactics/FormationPresets/HoNFormationPresetSerializationTests.cs|HoNFormationPresetSerializationTests|RequiresGame
class|TAOM.Tests/Features/CoopInterop/CoopAuthorityGateTests.cs|CoopAuthorityGateTests|RequiresGame
class|TAOM.Tests/Features/CulturalFeats/CulturalFeatsServiceTests.cs|CulturalFeatsServiceTests|RequiresGame
class|TAOM.Tests/Features/CultureDoctrine/ArcherFlankGeometryTests.cs|ArcherFlankGeometryTests|RequiresGame
class|TAOM.Tests/Features/CultureDoctrine/CultureMoraleAndAggressionTests.cs|FormationRoutingTests|RequiresGame
class|TAOM.Tests/Features/CultureDoctrine/EnvelopAndVolleyTests.cs|CavalryThreatTests|RequiresGame
class|TAOM.Tests/Features/CultureDoctrine/EnvelopAndVolleyTests.cs|EnvelopGeometryTests|RequiresGame
class|TAOM.Tests/Features/CustomBattles/CuratedDropdownIndependenceTests.cs|CuratedDropdownIndependenceTests|RequiresGame
class|TAOM.Tests/Features/CustomBattles/CustomBattleCommandersHookTests.cs|CustomBattleCommandersHookTests|RequiresGame
class|TAOM.Tests/Features/CustomBattles/CustomBattleFactionsHookTests.cs|CustomBattleFactionsHookTests|RequiresGame
class|TAOM.Tests/Features/CustomBattles/CustomBattleTroopHookTests.cs|CustomBattleTroopHookTests|RequiresGame
class|TAOM.Tests/Features/CustomBattles/SideCommanderFilterTests.cs|SideCommanderFilterTests|RequiresGame
class|TAOM.Tests/Features/EconomyDiagnostics/EconomyDiagnosticsWiringTests.cs|EconomyDiagnosticsWiringTests|RequiresGame
class|TAOM.Tests/Features/EditorCacheRebuild/Caching/NavigationPathClonerTests.cs|NavigationPathClonerTests|RequiresGame
class|TAOM.Tests/Features/EditorCacheRebuild/Caching/PathReuseCacheTests.cs|PathReuseCacheTests|RequiresGame
class|TAOM.Tests/Features/EditorCacheRebuild/Caching/PersistentPathCacheTests.cs|PersistentPathCacheTests|RequiresGame
class|TAOM.Tests/Features/EditorCacheRebuild/RuntimeCacheRebuildServiceTests.cs|RuntimeCacheRebuildServiceTests|RequiresGame
class|TAOM.Tests/Features/Encyclopedia/TaomInformationRestrictionModelTests.cs|TaomInformationRestrictionModelTests|RequiresGame
class|TAOM.Tests/Features/Enlistment/Duties/FieldDutyRuntimeTests.cs|FieldDutyRuntimeTests|RequiresGame
class|TAOM.Tests/Features/Enlistment/ServiceVocabularyTests.cs|ServiceVocabularyTests|RequiresGame
class|TAOM.Tests/Features/EquipPresets/HoNEquipmentPresetTests.cs|HoNEquipmentPresetTests|RequiresGame
class|TAOM.Tests/Features/EquipPresets/PresetSaveableTypeDefinerTests.cs|PresetSaveableTypeDefinerTests|RequiresGame
class|TAOM.Tests/Features/FactionMap/FactionDisplayHelperTests.cs|FactionDisplayHelperTests|RequiresGame
class|TAOM.Tests/Features/FiefGranting/FiefGrantingBehaviorCaptureGateTests.cs|FiefGrantingBehaviorCaptureGateTests|RequiresGame
class|TAOM.Tests/Features/FiefGranting/FiefGrantingBehaviorSessionResetTests.cs|FiefGrantingBehaviorSessionResetTests|RequiresGame
class|TAOM.Tests/Features/FiefManagement/FiefHubCampaignBehaviorTests.cs|FiefHubCampaignBehaviorTests|RequiresGame
class|TAOM.Tests/Features/FieldCamp/CampServiceTests.cs|CampServiceTests|RequiresGame
class|TAOM.Tests/Features/FieldCamp/FieldCampBehaviorSessionResetTests.cs|FieldCampBehaviorSessionResetTests|RequiresGame
class|TAOM.Tests/Features/FieldCamp/FieldCampOverlayVMTests.cs|FieldCampOverlayVMTests|RequiresGame
class|TAOM.Tests/Features/FieldCamp/FieldCampWiringTests.cs|FieldCampWiringTests|RequiresGame
class|TAOM.Tests/Features/HeroRace/EyeHeightAdjustmentHookTests.cs|EyeHeightAdjustmentHookTests|RequiresGame
class|TAOM.Tests/Features/HeroRace/RacePersistenceBehaviorTests.cs|RacePersistenceBehaviorTests|RequiresGame
class|TAOM.Tests/Features/HeroRace/TableauPositionServiceTests.cs|TableauPositionServiceTests|RequiresGame
class|TAOM.Tests/Features/InitialChildGeneration/TaomInitialChildGenerationBehaviorTests.cs|TaomInitialChildGenerationBehaviorTests|RequiresGame
class|TAOM.Tests/Features/MapLoadDiagnostics/MapLoadDiagnosticsBehaviorTests.cs|MapLoadDiagnosticsBehaviorTests|RequiresGame
class|TAOM.Tests/Features/Messengers/MessengerCampaignBehaviorTests.cs|MessengerCampaignBehaviorTests|RequiresGame
class|TAOM.Tests/Features/MixedFormations/FormationLayoutServiceTests.cs|FormationLayoutServiceTests|RequiresGame
class|TAOM.Tests/Features/MountDespawn/MountDespawnOffThreadTests.cs|MountDespawnOffThreadTests|RequiresGame
class|TAOM.Tests/Features/MountDespawn/MountDespawnWiringTests.cs|MountDespawnWiringTests|RequiresGame
class|TAOM.Tests/Features/PlayerPossession/PlayerPossessionBehaviorPhaseGuardTests.cs|PlayerPossessionBehaviorPhaseGuardTests|RequiresGame
class|TAOM.Tests/Features/PlayerSwitcher/KingdomJoinOfferBehaviorTests.cs|KingdomJoinOfferBehaviorTests|RequiresGame
class|TAOM.Tests/Features/PlayerSwitcher/PlayerClanLeadershipServiceTests.cs|PlayerClanLeadershipServiceTests|RequiresGame
class|TAOM.Tests/Features/QuickActions/InventorySearchCampaignBehaviorTests.cs|InventorySearchCampaignBehaviorTests|RequiresGame
class|TAOM.Tests/Features/RaceAge/RaceAgeBehaviorTests.cs|RaceAgeBehaviorTests|RequiresGame
class|TAOM.Tests/Features/Refuge/RefugeCampaignBehaviorTests.cs|RefugeCampaignBehaviorTests|RequiresGame
class|TAOM.Tests/Features/Refuge/RefugeDamageReductionTests.cs|RefugeDamageReductionTests|RequiresGame
class|TAOM.Tests/Features/Refuge/RefugeServiceTests.cs|RefugeServiceTests|RequiresGame
class|TAOM.Tests/Features/SettlementFood/SettlementFoodServiceTests.cs|SettlementFoodServiceTests|RequiresGame
class|TAOM.Tests/Features/SettlementNameplateRelation/NameplateRelationPaletteTests.cs|NameplateRelationPaletteTests|RequiresGame
class|TAOM.Tests/Features/Siege/SiegeDefenseServiceTests.cs|SiegeDefenseServiceTests|RequiresGame
class|TAOM.Tests/Features/SignatureStrikes/StrikeRequestBufferTests.cs|StrikeRequestBufferTests|RequiresGame
class|TAOM.Tests/Features/SmartCavalryAI/CavalryChargeServiceTests.cs|CavalryChargeServiceTests|RequiresGame
class|TAOM.Tests/Features/SmartCavalryAI/CavalryPathPlannerTests.cs|CavalryPathPlannerTests|RequiresGame
class|TAOM.Tests/Features/SpecialResources/SpecialResourceMessagesTests.cs|SpecialResourceMessagesTests|RequiresGame
class|TAOM.Tests/Features/SpecialResources/SpecialResourceTroopBadgeTests.cs|SpecialResourceTroopBadgeTests|RequiresGame
class|TAOM.Tests/Features/SpecialResources/SpecialResourcesBehaviorPhaseGuardTests.cs|SpecialResourcesBehaviorPhaseGuardTests|RequiresGame
class|TAOM.Tests/Features/Spider/SpiderAttackServiceTests.cs|SpiderAttackServiceTests|RequiresGame
class|TAOM.Tests/Features/StartupResources/StartupResourcesBehaviorTests.cs|StartupResourcesBehaviorTests|RequiresGame
class|TAOM.Tests/Features/SupplyLines/SupplyGoodsSearchTests.cs|SupplyGoodsSearchTests|RequiresGame
class|TAOM.Tests/Features/SupplyLines/SupplyLinesCampaignBehaviorTests.cs|SupplyLinesCampaignBehaviorTests|RequiresGame
class|TAOM.Tests/Features/SupplyLines/SupplyOrderPrefabBindingTests.cs|SupplyOrderPrefabBindingTests|RequiresGame
class|TAOM.Tests/Features/SupplyLines/SupplyOrderScreenVMTests.cs|SupplyOrderScreenVMTests|RequiresGame
class|TAOM.Tests/Features/SupplyLines/SupplyOrderServiceTests.cs|SupplyOrderServiceTests|RequiresGame
class|TAOM.Tests/Features/TimeAcceleration/TaomTimeControlHotKeyCategoryTests.cs|TaomTimeControlHotKeyCategoryTests|RequiresGame
class|TAOM.Tests/Features/TroopProgression/WageModifierServiceTests.cs|WageModifierServiceTests|RequiresGame
class|TAOM.Tests/Features/TroopWeight/SizePenaltyTests.cs|ResultFramePenaltyTests|RequiresGame
class|TAOM.Tests/Features/TroopWeight/SizePenaltyTests.cs|SizePenaltyTests|RequiresGame
class|TAOM.Tests/Features/TroopWeight/TroopWeightCheatsFormatTests.cs|TroopWeightCheatsFormatTests|RequiresGame
class|TAOM.Tests/Features/TroopWeight/TroopWeightServiceTests.cs|TroopWeightServiceTests|RequiresGame
class|TAOM.Tests/Features/TroopWeight/WeightedFrameIdentityTests.cs|WeightedFrameIdentityTests|RequiresGame
class|TAOM.Tests/Features/Warg/WargAttackServiceTests.cs|WargAttackServiceTests|RequiresGame
class|TAOM.Tests/Infrastructure/Dependencies/AssemblyRedirectListTests.cs|AssemblyRedirectListTests|RequiresGame
class|TAOM.Tests/SceneScripts/Roads/RoadGeometryBuilderTests.cs|RoadGeometryBuilderTests|RequiresGame
```

## Appendix B: `E:\repos\wt-010-logs\tag_categories.py`

```python
"""Add MSTest [TestCategory("...")] attributes listed in a manifest.

Manifest line: kind|repo-relative file|class or method name|category
kind is "class" (insert under the class's [TestClass]) or "method" (insert under the
method's [TestMethod] / [DataTestMethod]). Idempotent: an entry whose attribute block already
holds the category is counted as "present" and left alone. Line endings and a UTF-8 BOM are
preserved. Exits 1 on any entry it cannot place exactly once (it still writes the others).
Usage: python tag_categories.py <repo root> <manifest.txt>
"""
import re
import sys
from pathlib import Path


def attribute_block(lines, decl_idx):
    """Indices of the attribute lines directly above decl_idx (a comment or blank line ends it)."""
    idx = decl_idx - 1
    block = []
    while idx >= 0 and lines[idx].lstrip().startswith("["):
        block.append(idx)
        idx -= 1
    return block


def main():
    root = Path(sys.argv[1])
    entries = [l.strip().split("|") for l in open(sys.argv[2], encoding="utf-8") if l.strip()]
    errors = 0
    added = present = 0
    by_file = {}
    for e in entries:
        if len(e) != 4 or e[0] not in ("class", "method"):
            print(f"BAD MANIFEST LINE: {e}")
            errors += 1
            continue
        by_file.setdefault(e[1], []).append(e)
    for rel, items in by_file.items():
        path = root / rel
        raw = path.read_bytes()
        bom = raw.startswith(b"\xef\xbb\xbf")
        text = raw.decode("utf-8-sig")
        eol = "\r\n" if "\r\n" in text else "\n"
        lines = text.split(eol)
        for kind, _, name, cat in items:
            if kind == "class":
                decl = re.compile(r"^\s*(?:(?:public|internal|sealed|static|partial|abstract)\s+)*class\s+"
                                  + re.escape(name) + r"\b")
                anchor = re.compile(r"^\s*\[\s*TestClass\s*(\(\s*\))?\s*\]\s*$")
            else:
                decl = re.compile(r"^\s*(?:(?:public|internal|static|async)\s+)*(?:void|Task)\s+"
                                  + re.escape(name) + r"\s*\(")
                anchor = re.compile(r"^\s*\[\s*(TestMethod|DataTestMethod)\s*(\(\s*\))?\s*\]\s*$")
            hits = [i for i, l in enumerate(lines) if decl.search(l)]
            if len(hits) != 1:
                print(f"ERROR {rel}: {kind} {name} declared {len(hits)} times")
                errors += 1
                continue
            block = attribute_block(lines, hits[0])
            if any(f'TestCategory("{cat}")' in lines[i] for i in block):
                present += 1
                continue
            anchors = [i for i in block if anchor.search(lines[i])]
            if len(anchors) != 1:
                print(f"ERROR {rel}: {kind} {name} has {len(anchors)} anchor attributes directly above it")
                errors += 1
                continue
            a = anchors[0]
            indent = re.match(r"^\s*", lines[a]).group(0)
            lines.insert(a + 1, f'{indent}[TestCategory("{cat}")]')
            added += 1
        path.write_bytes((b"\xef\xbb\xbf" if bom else b"") + eol.join(lines).encode("utf-8"))
    print(f"added={added} present={present} errors={errors}")
    sys.exit(1 if errors else 0)


if __name__ == "__main__":
    main()
```

## Appendix C: `E:\repos\wt-010-logs\trxsum.py`

MSTest writes an Inconclusive result as per-result `outcome="NotExecuted"` and does not count it in
`Counters`, so this prints both. It then prints one `FAILED <Class>|<Method>` line per failing test
(the class without its namespace, sorted, data rows collapsed), which Steps 0.7 and 9 compare. Run
while revising this plan on an existing trx with one failure, it printed
`total=44 executed=44 passed=43 failed=1 outcomes=Failed:1,Passed:43` and
`FAILED XsltTemplateCoverageTests|EveryXsltTemplate_MatchesSomethingInTheVanillaFileItTransforms`.

```python
import sys
import xml.etree.ElementTree as ET

ns = {"t": "http://microsoft.com/schemas/VisualStudio/TeamTest/2010"}
root = ET.parse(sys.argv[1]).getroot()
c = root.find(".//t:ResultSummary/t:Counters", ns).attrib
methods = {}
for u in root.iterfind(".//t:UnitTest", ns):
    m = u.find("t:TestMethod", ns)
    methods[u.get("id")] = (m.get("className"), m.get("name"))
outcomes = {}
failed = set()
for r in root.iterfind(".//t:UnitTestResult", ns):
    outcomes[r.get("outcome")] = outcomes.get(r.get("outcome"), 0) + 1
    if r.get("outcome") == "Failed":
        failed.add(methods.get(r.get("testId"), ("?", r.get("testName"))))
print("total=%s executed=%s passed=%s failed=%s" % (c["total"], c["executed"], c["passed"], c["failed"]),
      "outcomes=" + ",".join("%s:%d" % kv for kv in sorted(outcomes.items())))
for cls, name in sorted(failed):
    print("FAILED %s|%s" % (cls.rsplit(".", 1)[-1], name))
```

## Appendix D: `E:\repos\wt-010-logs\normrefs.py`

Tested while revising this plan: two evaluations of `TAOM.Tests/TAOM.Tests.csproj` whose raw JSON
differed in one `AccessedTime` line normalized to identical files (`references=60` each).

```python
"""Print `dotnet msbuild -getItem:Reference` JSON without the volatile file-time metadata.

MSBuild adds ModifiedTime, CreatedTime and AccessedTime to every file item. NTFS updates the
access time whenever a build reads a game DLL, so a raw diff of two evaluations differs even
when the references are identical. Every other key and the item order are kept.
Usage: python normrefs.py <in.json> <out.json>
"""
import json
import sys

data = json.load(open(sys.argv[1], encoding="utf-8-sig"))
items = data["Items"]["Reference"]
for item in items:
    for key in [k for k in item if k.endswith("Time")]:
        del item[key]
with open(sys.argv[2], "w", encoding="utf-8", newline="\n") as out:
    json.dump(data, out, indent=2, sort_keys=True)
    out.write("\n")
print(f"references={len(items)}")
```
