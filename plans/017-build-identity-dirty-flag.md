# Plan 017: Stamp a dirty-tree flag into every build and a structured build field into every crash bundle

> **Executor instructions**: Follow this plan step by step. Run every
> verification command and confirm the expected result before moving to the
> next step. If anything in the "STOP conditions" section occurs, stop and
> report; do not improvise. Do NOT edit `plans/README.md`: the orchestrator
> maintains that index.
>
> **Where you work (read this twice)**: all work happens in the worktree `E:/repos/wt-plan-017`,
> never in the main tree `E:/repos/TAOM`. The main tree holds another session's uncommitted edits
> (22 paths under `Main`, `Dependencies`, `Stubs` at planning time, plus `CHANGELOG.md` and
> `.claude/skills/release/SKILL.md`). Your shell working directory resets to `E:/repos/TAOM`
> between calls, so:
> - **Every Bash call begins with `cd /e/repos/wt-plan-017 && `** (the one exception is the
>   `git worktree add` in Step 0, which uses `git -C`). A relative path in a call without that
>   prefix runs against the main tree. The orchestrator confirms before dispatch that your
>   permissions allow this compound `cd` and writes under `E:/repos/wt-plan-017` and
>   `E:/repos/wt-plan-017-scratch`. If a call is refused or waits on a permission prompt anyway,
>   STOP and report it; do not move the work into the main tree.
> - **Every PowerShell call uses absolute paths** (`E:/repos/wt-plan-017/...`). This plan uses the
>   PowerShell tool only to read a DLL's version resource and for the one git-less build in Step 6.
> - **Every Read, Edit and Write path is absolute**: `E:/repos/wt-plan-017/<repo path>`. Never
>   open anything under `E:/repos/TAOM/` for editing.
> - **Scratch files** go in `E:/repos/wt-plan-017-scratch/` (Bash: `/e/repos/wt-plan-017-scratch/`),
>   created in Step 0, never inside the worktree.
> - **Tool timeout**: pass `timeout: 600000` on every call that runs `dotnet build` or `dotnet test`.
>   The 120 s default can kill them. Never run two `dotnet` commands at once in this worktree.
> - Never spell `python3`; use `python`. Write any multi-line script with the Write tool, never a
>   Bash heredoc.
>
> **Drift check (run first, after Step 0 creates the worktree)**:
> `cd /e/repos/wt-plan-017 && git diff --stat b2e387db..HEAD -- Directory.Build.props Main/Core/Diagnostics/BuildStampReport.cs Main/Features/CrashReport/Domain/IdentitySnapshot.cs Main/Features/CrashReport/Collectors/IdentityCollector.cs Main/Features/CrashReport/CrashReportService.cs Main/Features/CrashReport/Rendering/PlainTextCrashReportRenderer.cs Main/Features/CrashReport/Rendering/CrashBundleWriter.cs TAOM.Tests/Core/Diagnostics/BuildStampReportTests.cs TAOM.Tests/Features/CrashReport/PlainTextCrashReportRendererTests.cs TAOM.Tests/Features/CrashReport/CrashBundleWriterTests.cs tools/package_release.py tools/tests/test_package_release.py .claude/skills/release/SKILL.md docs/reference/release-process.md docs/features/crash-report.md`
> Expected: no output (at planning time `HEAD` was `4b5662b2`, which touched none of these).
> `CHANGELOG.md` is in scope but deliberately left out: every session appends to it.
> One drift is pre-cleared: if the only change is to `.claude/skills/release/SKILL.md` and
> `git diff b2e387db..HEAD -- .claude/skills/release/SKILL.md` shows nothing but an added Phase 1
> item 5 about `taom_test_` riders, proceed (you do not edit Phase 1). Any other output: STOP and
> report it; the excerpts below are then stale.

## Status

- **Priority**: P3
- **Effort**: S for the stamp and the crash field; M overall with the packager gate and the release docs (one MSBuild target, one record field, two render lines, one Python gate, four doc edits, 4 C# and 13 Python tests)
- **Risk**: LOW (build metadata and diagnostics only; no save-format change, no gameplay code, no Harmony patch, no IoC change)
- **Depends on**: none. It touches the same `InformationalVersion` that open issue #593 plans to extend with a Dependencies content hash; see Maintenance notes.
- **Category**: dx
- **Planned at**: commit `b2e387db`, 2026-09-23
- **Issue**: create before implementation lands (orchestrator)
- **Before dispatch (orchestrator)**: `Directory.Build.props` is hook-protected.
  `.claude/hooks/config-protection.sh`, registered on `Edit|Write` in `.claude/settings.json`, blocks
  that basename at any path (the worktree included) with exit 2 unless the maintainer has approved
  the edit. Get the maintainer's explicit approval for Step 5's edit and arrange the hook's
  user-approved override for the executor's session before dispatch; without it Step 5 STOPs.
- **Review**: the executor cannot invoke skills or spawn agents, so commits A and B (C#) land on the
  plan branch without `/deep-review`. The orchestrator runs `/deep-review` on the branch before any
  merge (reviewer focus: Maintenance notes).

## Why this matters

Every TAOM build writes a stamp into both assemblies: `build.<UTC time>Z+<commit SHA>`. The SHA is
HEAD's, whatever the working tree holds, so a build made from uncommitted edits reads exactly like a
clean build of that commit. At planning time the main tree had 22 uncommitted paths under `Main`,
`Dependencies` and `Stubs`, and the DLL built from it reads `build.20260924-020931Z+4b5662b2...`
with nothing to say that code no commit holds is inside. Triage of a player CTD can then chase code
that never shipped, or miss code that did, and a release can go out with such a DLL because the
packager has no git awareness at all. Separately, a crash bundle's structured Identity section names
only the `SubModule.xml` label (shared by every commit since the last bump) and a DLL hash nothing
maps back; the stamp is only in the bundled log. After this plan: the stamp says `.dirty` when the
tree was dirty and `nogit` when git could not tell, the crash report and its manifest print the
stamp on their own line, and `tools/package_release.py --require-build <tag>` refuses to package a
DLL that is not a clean build of the release tag.

## Current state

All excerpts are from commit `b2e387db`. None of the in-scope files differs between `b2e387db` and
the planning-time `HEAD` (`4b5662b2`). In the main tree, only `.claude/skills/release/SKILL.md`
(one added Phase 1 item) and `CHANGELOG.md` among them have uncommitted edits by another session;
your worktree starts from committed content, so you never see those.

### How the stamp is produced today (verified against the installed SDK at planning)

`Directory.Build.props` (repo root; single-owner file, **in scope for exactly the edit in Step 5**) sets
the stamp. Lines 23-24:

```xml
		<TaomBuildStamp Condition="'$(TaomBuildStamp)' == ''">$([System.DateTime]::UtcNow.ToString('yyyyMMdd-HHmmss'))Z</TaomBuildStamp>
		<InformationalVersion>build.$(TaomBuildStamp)</InformationalVersion>
```

The file is one `<Project>` holding one `<PropertyGroup>` (lines 2-42) and nothing else; the file has 43 lines and ends (lines 41-43):

```xml
		<GameBinariesFolder Condition="Exists('$(GameFolder)\bin\Gaming.Desktop.x64_Shipping_Client\Bannerlord.exe')">Gaming.Desktop.x64_Shipping_Client</GameBinariesFolder>
	</PropertyGroup>
</Project>
```

It is imported by all four projects (`Main/TAOM.csproj`, `Dependencies/TAOM.Dependencies.csproj`,
`TAOM.Tests/TAOM.Tests.csproj`, `tools/BannerlordCraftingTool/BannerlordCraftingTool.csproj`).
Nothing in the repo reads git (`git grep -n -i -E "SourceRevision|dirty|git (status|rev-parse)" b2e387db -- '*.csproj' '*.props' '*.targets' build.ps1` returns nothing).

The SHA comes from the .NET SDK, not from TAOM. Installed SDK at planning: `dotnet --version` =
`10.0.401`, no `global.json`. The relevant targets, read at planning:

- `C:\Program Files\dotnet\sdk\10.0.401\Sdks\Microsoft.Build.Tasks.Git\build\Microsoft.Build.Tasks.Git.targets:20,34`:
  target `InitializeSourceControlInformationFromSourceControlManager` runs the `LocateRepository`
  task with `<Output TaskParameter="RevisionId" PropertyName="SourceRevisionId" Condition="'$(SourceRevisionId)' == ''" />`
  (a managed task: it reads `.git` itself and needs no `git.exe`).
- `...\Sdks\Microsoft.SourceLink.Common\build\InitializeSourceControlInformation.targets:23-26`:
  `_InitializeSourceControlInformationFromSourceControlManager` depends on the target above, has
  `BeforeTargets="InitializeSourceControlInformation"` and `Condition="'$(EnableSourceControlManagerQueries)' == 'true'"`.
- `C:\Program Files\dotnet\sdk\10.0.401\Microsoft.Common.CurrentVersion.targets:803`: `<Target Name="InitializeSourceControlInformation" />` (empty; the hook point).
- `...\Sdks\Microsoft.NET.Sdk\targets\Microsoft.NET.GenerateAssemblyInfo.targets:62-73`:

```xml
  <Target Name="AddSourceRevisionToInformationalVersion"
          DependsOnTargets="GetAssemblyVersion;InitializeSourceControlInformation"
          Condition="'$(SourceControlInformationFeatureSupported)' == 'true' and '$(IncludeSourceRevisionInInformationalVersion)' == 'true'">
    <PropertyGroup Condition="'$(SourceRevisionId)' != ''">
      <!-- Follow SemVer 2.0 rules -->
      <_InformationalVersionContainsPlus>false</_InformationalVersionContainsPlus>
      <_InformationalVersionContainsPlus Condition="$(InformationalVersion.Contains('+'))">true</_InformationalVersionContainsPlus>

      <InformationalVersion Condition="!$(_InformationalVersionContainsPlus)">$(InformationalVersion)+$(SourceRevisionId)</InformationalVersion>
      <InformationalVersion Condition="$(_InformationalVersionContainsPlus)">$(InformationalVersion).$(SourceRevisionId)</InformationalVersion>
    </PropertyGroup>
  </Target>
```

So a target that runs `AfterTargets="InitializeSourceControlInformation"` sees `SourceRevisionId`
already set, and whatever it appends to `SourceRevisionId` lands in `InformationalVersion` after the
`+`. This is the mechanism Step 5 uses. Evidence that it is live today: the main tree's
`Main\bin\Debug\net472\TAOM.dll` (built 2026-09-23 21:09 local) has
`VersionInfo.ProductVersion` = `build.20260924-020931Z+4b5662b22afaa90aed8ba555b9d64ecc030cbe1c`,
and `TAOM.Dependencies.dll` carries the same string. The Win32 `ProductVersion` equals the
`InformationalVersion`, and the same text sits in the assembly metadata as plain ASCII (a byte scan
of the DLL for `build\.\d{8}-\d{6}Z[+.][0-9a-z.]+` finds exactly that one string).

`dotnet msbuild Main/TAOM.csproj -getProperty:TargetPath -p:DisableModuleCopy=true -p:ModuleId=`
printed `E:\repos\TAOM\Main\bin\Debug\net472\TAOM.dll`, so in your worktree the build outputs are
`E:/repos/wt-plan-017/Main/bin/Debug/net472/TAOM.dll` and
`E:/repos/wt-plan-017/Dependencies/bin/Debug/net472/TAOM.Dependencies.dll`.

Build outputs are ignored, so a build alone never dirties the tree: `git check-ignore -v --no-index`
matches `Main/bin/`, `Main/obj/` (`.gitignore:2-3`), `Main/_Module/bin/Win64_Shipping_Client/*`
(`.gitignore:75`, where `CopyBinariesToModuleFolder` copies the DLL even with
`DisableModuleCopy=true`), `Dependencies/_Module/bin/Win64_Shipping_Client/*` (`.gitignore:51`).
An existing built worktree (`E:/repos/taom-635`, `Main/bin` present) reports 0 lines for
`git --no-optional-locks status --porcelain -- Main Dependencies Stubs Directory.Build.props`.
Timing of that command on the main tree: 36 ms.

Why `Stubs` is in the scope: `Dependencies/TAOM.Dependencies.csproj` target
`DeployTAOMDependenciesStubs` copies `$(MSBuildThisFileDirectory)..\Stubs\**\SubModule.xml` into the
game, so it is deployed build input. `Main` covers both C# and the deployed `Main/_Module` data
(an uncommitted XML edit also ships in a release zip, so it rightly marks the build dirty).

### The stamp readers (all display-only)

`Main/Core/Diagnostics/BuildStampReport.cs` (206 lines). The summary comment at lines 18-21 is
wrong about the suffix's origin (the verifier found no git logic in `Bannerlord.BuildResources`;
the SDK appends it, with `+`):

```csharp
/// Directory.Build.props stamps <c>InformationalVersion</c> as <c>build.yyyyMMdd-HHmmssZ</c>;
/// Bannerlord.BuildResources then appends <c>.{commit-sha}</c>, so the stamp is NOT at the end of
/// the string. Both modules are produced by the same build, so their stamps should agree to within
/// seconds; a gap of hours means a hand-copied module.
```

`TryParseStamp` (lines 107-126) finds `build.` and slices a fixed 15 characters, so any tail after
the stamp (a `.dirty` included) still parses. The reader the crash field will reuse, lines 189-205:

```csharp
    private static string ReadInformationalVersion(Assembly? asm)
    {
        if (asm == null) return "<not loaded>";
        try
        {
            // Report BOTH: Directory.Build.props is imported before the csproj's own
            // PropertyGroup, so $(Version) is not yet defined there and cannot be folded into the
            // stamp. The assembly version carries the product version; the informational version
            // carries the per-build identity.
            string asmVer = asm.GetName().Version?.ToString() ?? "<unknown>";
            var attr = asm.GetCustomAttribute<AssemblyInformationalVersionAttribute>();
            return string.IsNullOrEmpty(attr?.InformationalVersion)
                ? asmVer
                : $"v{asmVer} {attr!.InformationalVersion}";
        }
        catch { return "<unreadable>"; }
    }
```

`Main/SubModule.cs:121-128` (single-owner, **not edited**) logs `BuildStampReport.BuildReport(...)`
as the `[BuildStamp]` line at startup. Other readers of the string, none of which parse it:
`Main/Features/SaveLoadDiagnostics/Hooks/MBSaveLoad_GetSaveMetaData_Patch.cs:18,38` (writes it into
save metadata as `TAOM_Build`), `tools/inspect_sav.py:85`, `tools/repair_sav_strings.py:217`,
`tools/repair_sav_strings.ps1:186-187`. A longer string is harmless to all of them.

### The crash bundle identity

`Main/Features/CrashReport/Domain/IdentitySnapshot.cs` (whole file):

```csharp
namespace TAOM.Features.CrashReport.Domain;

public sealed record IdentitySnapshot(
    string BannerlordVersion,
    string BannerlordExeFileVersion,
    string TaomVersion,
    string TaomDllSha1,
    string OriginatingPatchTarget,
    string LanguageCode);
```

Every construction site (`git grep -n "IdentitySnapshot(" b2e387db -- Main TAOM.Tests`), all positional:

| File:line | Call |
|---|---|
| `Main/Features/CrashReport/Collectors/IdentityCollector.cs:19-25` | named arguments, see below |
| `Main/Features/CrashReport/CrashReportService.cs:182-183` | `?? new IdentitySnapshot("?", "?", "?", "?", originatingPatchTarget, "?");` |
| `TAOM.Tests/Features/CrashReport/PlainTextCrashReportRendererTests.cs:197` | `Identity: new IdentitySnapshot("v1.4.5", "1.4.5.x", "v2.0.0", "sha1", "Some.Origin", "en-US"),` |
| `TAOM.Tests/Features/CrashReport/CrashBundleWriterTests.cs:128` | `Identity: new IdentitySnapshot("v1.5.2", "1.5.2.x", "v2.0.28", "sha1", "Some.Origin", "en-US"),` |

The new field goes **last**. Inserting it in the middle would silently shift the `"?"` strings in
`CrashReportService`'s positional fallback.

`Main/Features/CrashReport/Collectors/IdentityCollector.cs:9-26`:

```csharp
public sealed class IdentityCollector
{
    public IdentitySnapshot Collect(string originatingPatchTarget)
    {
        string blVersion = SafeRead(() => ModuleHelper.GetModuleInfo("Native")?.Version.ToString()) ?? "(unknown)";
        string blExeVersion = SafeRead(GetBannerlordExeFileVersion) ?? "(unknown)";
        string taomVersion = SafeRead(() => ModuleHelper.GetModuleInfo("TAOM")?.Version.ToString()) ?? "(unknown)";
        string taomDllSha1 = SafeRead(() => DllHasher.Sha1OfFile(typeof(IdentityCollector).Assembly.Location)) ?? "(unknown)";
        string language = SafeRead(() => BannerlordConfig.Language) ?? "(unknown)";

        return new IdentitySnapshot(
            BannerlordVersion: blVersion,
            BannerlordExeFileVersion: blExeVersion,
            TaomVersion: taomVersion,
            TaomDllSha1: taomDllSha1,
            OriginatingPatchTarget: originatingPatchTarget ?? "(unknown)",
            LanguageCode: language);
    }
```

Its usings are `System`, `System.Diagnostics`, `TaleWorlds.ModuleManager`,
`TaleWorlds.MountAndBlade`, `TAOM.Features.CrashReport.Domain`. It is registered in
`Main/Features/CrashReport/CrashReportIoC.cs` (`container.Register<IdentityCollector>(Reuse.Singleton);`);
no registration changes.

`Main/Features/CrashReport/Rendering/PlainTextCrashReportRenderer.cs:63-70`:

```csharp
    private static void RenderIdentity(StringBuilder sb, IdentitySnapshot id)
    {
        Section(sb, "Identity");
        sb.AppendLine($"Bannerlord: {id.BannerlordVersion} (exe {id.BannerlordExeFileVersion})");
        sb.AppendLine($"TAOM:       {id.TaomVersion} (dll sha1 {id.TaomDllSha1})");
        sb.AppendLine($"Origin:     {id.OriginatingPatchTarget}");
        sb.AppendLine($"Language:   {id.LanguageCode}");
    }
```

`Main/Features/CrashReport/Rendering/CrashBundleWriter.cs:101-109` (the `manifest.txt` a triager reads first):

```csharp
    internal static string BuildManifest(ExceptionContext c, string text, string json)
    {
        var sb = new StringBuilder(1024);
        sb.AppendLine($"TAOM CrashReport bundle");
        sb.AppendLine($"Captured: {c.CapturedAtUtc:O}");
        sb.AppendLine($"Signature: {c.CrashSignature}");
        sb.AppendLine($"TAOM version: {c.Identity.TaomVersion}");
        sb.AppendLine($"Bannerlord version: {c.Identity.BannerlordVersion}");
        sb.AppendLine($"Origin: {c.Identity.OriginatingPatchTarget}");
```

`Main/Features/CrashReport/Rendering/JsonCrashReportRenderer.cs:19-20` is
`JsonConvert.SerializeObject(context, Settings)` over the whole record, so `report.json` gains
`"TaomBuild"` with no edit. There is no HTML renderer (an audit note said there was; the folder
holds only `PlainTextCrashReportRenderer`, `JsonCrashReportRenderer`, `CrashBundleWriter`,
`MemoryPressureVerdict`, `ICrashReportRenderer`). No tool parses `report.txt` lines
(`git grep -n -E "Identity ---|dll sha1|TAOM version:" b2e387db -- tools .claude` finds nothing).

The bundle already carries the stamp indirectly: `CrashBundleWriter.cs:46` copies the whole session
`taom_debug.log` into the zip, and the `[BuildStamp]` line is near its top. This plan adds the
structured field so triage does not depend on that log.

### The packager and the release flow

`tools/package_release.py` (397 lines) copies an allow-listed set from a dev install's `Modules`
folder into a fresh destination. It has no git, tag, dirty or stamp logic. Facts you need:

- Imports (lines 36-45): `from __future__ import annotations`, `argparse`, `json`, `os`, `re`,
  `shutil`, `sys`, `dataclasses.dataclass, field`, `pathlib.Path, PurePosixPath`. No `subprocess`.
- Line 54: `DEFAULT_MODULES = ("TAOM", "TAOM_Map", "LOTRLOME_Armory", "TAOM.Dependencies")`. The
  two compiled DLLs therefore sit at `<Modules>/TAOM/bin/Win64_Shipping_Client/TAOM.dll` and
  `<Modules>/TAOM.Dependencies/bin/Win64_Shipping_Client/TAOM.Dependencies.dll`.
- `ModulePlan` (lines 187-202) has `name` (the module folder name) and `root` (its `Path`).
- `main()` (lines 299-393): argparse at 300-312; `plans` built at 320-333; `_report(plans, args)` at
  334; the unknown-entries listing at 336-345; then:

```python
    if args.json:
        manifest = {
```
  (line 347), later `if args.dry_run:` (line 371) returns 0, then the unknown refusal (375-379), the
  non-empty destination refusal (381-383), and the copy.
- Docstring usage block lines 28-34, ending:
  `Exit codes: 0 ok · 1 nothing to do · 2 bad input / unknown entries / non-empty destination.`

`tools/tests/test_package_release.py` (403 lines, stdlib `unittest`, 33 tests, all green at
planning: `python tools/tests/test_package_release.py` prints `Ran 33 tests` and `OK`). Helpers you
will reuse: `pr` (the module), `TOOL` (line 37, the script path), `_run(*args)` (lines 323-326,
runs the CLI with `capture_output=True, text=True`). Imports at lines 25-31: `json`, `os`,
`subprocess`, `sys`, `tempfile`, `unittest`, `Path`. The last lines are
`if __name__ == "__main__":` / `    unittest.main(verbosity=2)` (402-403).

`.claude/skills/release/SKILL.md` (117 lines at `b2e387db`): Phase 1 item 1 already requires
`git status --porcelain` to be **empty**; Phase 2 (lines 32-35) builds with `./build.ps1 -RunTests`
**before** Phase 3 bumps the version and Phase 6 commits, so the DLL that exists when Phase 7 cuts
the tag carries the parent commit's SHA. Nothing in the skill mentions packaging or
`package_release.py`. Phase 7 ends at line 104; `## Gotchas` starts at line 106. The Gotchas bullet
at lines 114-116:

```markdown
- **Version ≠ build stamp.** `Directory.Build.props` stamps `InformationalVersion` per build
  (`build.yyyyMMdd-HHmmssZ`) and freezes `AssemblyVersion` deliberately. The stamp identifies a
  build; the tag identifies a release.
```

`docs/reference/release-process.md` contradicts the skill at line 69 (step 1 of "Cutting a release"):

```markdown
1. Tree clean (or, when another session's edits are present, every path staged explicitly and theirs left out),
```

Staging keeps another session's edits out of the *commit*, but `build.ps1` still compiles and
deploys them. Other lines you edit: 59-63 (the stamp paragraph), 83 (step 7, the last numbered
step), and the "Resolving a crash report to a commit" section (lines 99-110).

`docs/features/crash-report.md:117` (the Identity row of the "Data captured" table):

```markdown
| Identity | `Native` + `TAOM` module versions, Bannerlord.exe FileVersion, TAOM.dll SHA1, language code |
```

### Binding conventions

- **ADR-002 (thin entry points under 150 lines)**: no entry point is touched. `SubModule.cs` and
  `IoC.cs` are single-owner and **not edited** (nothing here needs them).
- **ADR-007 (adapters wrap sealed TaleWorlds types)**: the new field is a plain `string` read by
  reflection from TAOM's own assembly; no TaleWorlds type crosses. `IdentityCollector` is an
  existing collector, not a service.
- **ADR-008 (services unit-testable without the game)**: every new branch is in a pure function
  (`BuildStampReport`, the renderer, the manifest, the Python stamp checks) and gets a test.
- **ADR-009 (self-documenting code)**: no comments inside new method bodies; summaries short.
- **ADR-003/004/005**: no `#region`, no `[Obsolete]`, no `#if`.
- **TDD (AGENTS.md "Always")**: every C# and Python change is preceded by a failing test (Steps 1,
  4, 8). Step 4's RED is a build observation, because an MSBuild target has no unit-test seam.
- **Human prose (AGENTS.md)**: no em or en dash (U+2014, U+2013) in any line you add to docs, the
  skill, CHANGELOG, comments or commit messages. Use a comma, colon, semicolon or parentheses.
  Existing lines you do not rewrite keep theirs.
- **Evidence over claims**: quote the command output that proves each step before moving on.

### Test baseline

The orchestrator's baseline at `b2e387db`: 10,239 tests, 10,235 passed, 2 failed, 2 ignored. The 2
failures are in `AnimaliaMountWiringTests` and the Elk tests (`TheElkItem_DeclaresTheScaleTheReachIsTunedFor`
and `AnimaliaActionSets_BindOnlyHorseActions_ToClipsThatExist`); both assert on the live, unversioned
Armory under the game install, which another session is editing, so no plan caused them. **Do not
chase them.** The 2 ignored are the deliberately `[Ignore]`d `WargAttack_FastWarg_InvokesRunningAttack`
and `WargAttack_SlowWarg_InvokesStandingAttack`. Your fresh worktree may differ slightly (those two
may pass); record your own Step 0 numbers and compare every later run against those.

## Commands you will need

Run each from the worktree (`cd /e/repos/wt-plan-017 && ...` in Bash).

| Purpose | Command | Expected on success |
|---|---|---|
| Build | `dotnet build Main/TAOM.csproj -p:DisableModuleCopy=true -p:ModuleId=` | exit 0, `0 Error(s)` |
| Tests | `dotnet test TAOM.Tests -p:DisableModuleCopy=true -p:ModuleId=` | the Step 0 numbers plus 4 passed; failures only the Step 0 ones |
| Filtered tests | `dotnet test TAOM.Tests -p:DisableModuleCopy=true -p:ModuleId= --filter "FullyQualifiedName~BuildStampReportTests\|FullyQualifiedName~PlainTextCrashReportRendererTests\|FullyQualifiedName~CrashBundleWriterTests"` | 36 passed, 0 failed (32 at `b2e387db`: 16 + 11 + 5) |
| Python tests | `python tools/tests/test_package_release.py` | `Ran 46 tests`, `OK` (33 at `b2e387db`) |
| Data | `python tools/validate_moduledata.py` | not run by this plan: no ModuleData change |
| Docs | `python tools/lint_docs.py` | exit 0; no `[em-dash]` line naming a file you changed |
| Read a DLL stamp (PowerShell tool) | `(Get-Item E:/repos/wt-plan-017/Main/bin/Debug/net472/TAOM.dll).VersionInfo.ProductVersion` | the stamp string |

Never `./build.ps1` (it deploys into the game install). In the Bash tool, the `|` inside the filter
string must stay inside the double quotes as shown (no backslash needed in Bash; the table escapes it
only for Markdown): `--filter "FullyQualifiedName~BuildStampReportTests|FullyQualifiedName~PlainTextCrashReportRendererTests|FullyQualifiedName~CrashBundleWriterTests"`.

## Scope

**In scope** (the only files you modify):

- `Directory.Build.props` (single-owner and hook-protected; exactly the target added in Step 5, nothing else, and only through the Edit tool)
- `Main/Core/Diagnostics/BuildStampReport.cs` (visibility of `ReadInformationalVersion`, summary comment lines 18-21)
- `Main/Features/CrashReport/Domain/IdentitySnapshot.cs`
- `Main/Features/CrashReport/Collectors/IdentityCollector.cs`
- `Main/Features/CrashReport/CrashReportService.cs` (the one fallback at line 183)
- `Main/Features/CrashReport/Rendering/PlainTextCrashReportRenderer.cs`
- `Main/Features/CrashReport/Rendering/CrashBundleWriter.cs`
- `TAOM.Tests/Core/Diagnostics/BuildStampReportTests.cs`
- `TAOM.Tests/Features/CrashReport/PlainTextCrashReportRendererTests.cs`
- `TAOM.Tests/Features/CrashReport/CrashBundleWriterTests.cs`
- `tools/package_release.py`
- `tools/tests/test_package_release.py`
- `.claude/skills/release/SKILL.md`
- `docs/reference/release-process.md`
- `docs/features/crash-report.md`
- `CHANGELOG.md`

**Out of scope** (do NOT touch, even though they look related):

- `Main/SubModule.cs`, `Main/IoC.cs`, `Main/TAOM.csproj`, `Dependencies/TAOM.Dependencies.csproj`:
  single-owner or unneeded. If you believe one needs a change, STOP and report the exact line.
- `build.ps1`, `.github/workflows/*`: CI and the deploy script are separate work.
- `BuildStampReport.Classify` / `DescribeVerdict` / `BuildReport`: the pairing verdict is issue #593's
  work, not this plan's.
- `Main/Features/SaveLoadDiagnostics/**`, `tools/inspect_sav.py`, `tools/repair_sav_strings.*`:
  they display the longer string unchanged.
- Any file under `E:/repos/TAOM/` (the main tree) and anything under `E:\Steam\...`.

## Git workflow

- **Worktree**: `git -C E:/repos/TAOM worktree add E:/repos/wt-plan-017 -b plan/017-build-identity bannerlord-1.5.x`
  (Step 0). If the branch or directory already exists, STOP and report; do not delete, reuse or reset either.
- **Stage explicit paths only**, by name (`cd /e/repos/wt-plan-017 && git add <path> <path>`), never
  `git add -A`, `git add .` or `git commit -a`. Commit through the **Bash tool** (the commit hooks are
  registered on Bash) with `cd /e/repos/wt-plan-017 && git commit -m "<subject>" -m "<body>"`.
- **Subject**: `<type>(<scope>): v<version> - <description>`, at most 72 characters, where
  `<version>` is the `<Version value="...">` in `Main/_Module/SubModule.xml` (`v2.0.30` at planning;
  re-read it with `cd /e/repos/wt-plan-017 && grep -o '<Version value="[^"]*"' Main/_Module/SubModule.xml | head -1`).
  Body wrapped at 72. **No AI attribution trailer** (no `Co-Authored-By`). Optional trailers:
  `Not-tested:`, `Research:`. The four subjects are given in the steps.
- **Never push**, never open a PR, never merge, never touch another branch.
- The PreToolUse commit gates `cd` to the main tree and read *its* index (for example
  `.claude/hooks/check-changelog-changed.sh:44,57`). If any hook denies or asks on your commit, or on
  an Edit or Write, STOP and report the message verbatim; never bypass a hook, and never write a
  file through Bash or PowerShell because the Edit or Write tool was refused.
- **No skills, no review**: you cannot invoke skills (so no `/deep-review`) or spawn agents. Commit
  as the steps say; the orchestrator reviews the branch before merge.

## Steps

### Step 0: Set up, confirm the SDK facts, record baselines

1. Create the worktree (Git workflow above) and the scratch folder:
   `cd /e/repos/wt-plan-017 && git rev-parse --show-toplevel && mkdir -p /e/repos/wt-plan-017-scratch && git rev-parse HEAD > /e/repos/wt-plan-017-scratch/step0-base.txt && cat /e/repos/wt-plan-017-scratch/step0-base.txt`
   → prints `E:/repos/wt-plan-017` then 40 hex. That SHA is **BASE**, the commit your branch starts
   from. Steps 12 and the Done criteria diff and log against BASE, never against
   `bannerlord-1.5.x`: another session works on that branch in the main tree and may commit to it
   during your run.
2. Run the drift check from the header. Expected: no output (or only the pre-cleared SKILL.md item).
3. Confirm the SDK target names this plan relies on (they were read at planning against SDK
   `10.0.401`; a different SDK may differ):
   `cd /e/repos/wt-plan-017 && V=$(dotnet --version) && echo "sdk=$V" && S="/c/Program Files/dotnet/sdk/$V" && grep -n 'Target Name="AddSourceRevisionToInformationalVersion"' "$S/Sdks/Microsoft.NET.Sdk/targets/Microsoft.NET.GenerateAssemblyInfo.targets" && grep -n 'Target Name="InitializeSourceControlInformation"' "$S/Microsoft.Common.CurrentVersion.targets" && grep -n 'PropertyName="SourceRevisionId"' "$S/Sdks/Microsoft.Build.Tasks.Git/build/Microsoft.Build.Tasks.Git.targets" && grep -n 'InformationalVersion)+\$(SourceRevisionId)' "$S/Sdks/Microsoft.NET.Sdk/targets/Microsoft.NET.GenerateAssemblyInfo.targets"`
   → `sdk=10.0.401`, then one match from each grep (at planning: line 62, line 803, line 34,
   line 70). Different line numbers are fine. **Any grep printing nothing, or a non-zero exit: STOP**
   (the SDK's hook points differ from what Step 5 assumes).
4. Baseline build: `cd /e/repos/wt-plan-017 && dotnet build Main/TAOM.csproj -p:DisableModuleCopy=true -p:ModuleId= 2>&1 | tail -n 6` (timeout 600000)
   → exit 0, `0 Error(s)`. Write the `Warning(s)` count to `E:/repos/wt-plan-017-scratch/step0-warnings.txt` for Step 5's comparison.
5. Baseline stamp, clean tree (PowerShell tool):
   `(Get-Item E:/repos/wt-plan-017/Main/bin/Debug/net472/TAOM.dll).VersionInfo.ProductVersion; git -C E:/repos/wt-plan-017 rev-parse HEAD; git -C E:/repos/wt-plan-017 --no-optional-locks status --porcelain -- Main Dependencies Stubs Directory.Build.props`
   → `build.<14 digits with a dash>Z+<40 hex>`, then the same 40 hex, then nothing. Write the three
   lines to `E:/repos/wt-plan-017-scratch/step0-stamp.txt`. If the status command prints any line,
   STOP: a fresh worktree must be clean over this scope, and Step 5's flag would then read `.dirty`
   on every build.
6. Baseline tests: `cd /e/repos/wt-plan-017 && dotnet test TAOM.Tests -p:DisableModuleCopy=true -p:ModuleId= > /e/repos/wt-plan-017-scratch/step0-tests.txt 2>&1; echo "rc=$?"; tail -n 5 /e/repos/wt-plan-017-scratch/step0-tests.txt` (timeout 600000)
   → the summary line. Expected about 10,239 total with 2 failures in `AnimaliaMountWiringTests` or
   Elk tests (or none) and 2 skipped. Record the failing test names:
   `grep -E "^\s*Failed " /e/repos/wt-plan-017-scratch/step0-tests.txt`. If failures appear outside
   Animalia/Elk, STOP and report them (the baseline is not what this plan assumes).
7. `cd /e/repos/wt-plan-017 && python tools/tests/test_package_release.py 2>&1 | tail -n 3` → `Ran 33 tests` and `OK`.

### Step 1 (RED): failing C# tests for the build field

Edit three test files (absolute paths under `E:/repos/wt-plan-017/`).

**1a.** `TAOM.Tests/Features/CrashReport/PlainTextCrashReportRendererTests.cs`:

- Add a constant as the first member of the class (after the opening brace at line 10):

```csharp
    private const string TestBuildStamp =
        "v2.0.0.0 build.20260923-184249Z+0123456789abcdef0123456789abcdef01234567.dirty";
```

- In `MakeMinimalContext` (line 197) give the identity a seventh argument:
  `Identity: new IdentitySnapshot("v1.4.5", "1.4.5.x", "v2.0.0", "sha1", "Some.Origin", "en-US", TestBuildStamp),`
- Add this test after `Render_MinimalContext_ProducesAllSections` (which ends at line 38):

```csharp
    [TestMethod]
    public void Render_Identity_PrintsTheTaomBuildStamp()
    {
        var text = new PlainTextCrashReportRenderer().Render(MakeMinimalContext());

        StringAssert.Contains(text, "Build:      " + TestBuildStamp,
            "the build stamp names the exact commit and whether the tree was dirty");
    }
```

**1b.** `TAOM.Tests/Features/CrashReport/CrashBundleWriterTests.cs`:

- Add the same `TestBuildStamp` constant as the first member of the class (after line 19,
  before `private string _dir`).
- In `MakeContext` (line 128): `Identity: new IdentitySnapshot("v1.5.2", "1.5.2.x", "v2.0.28", "sha1", "Some.Origin", "en-US", TestBuildStamp),`
- Add after `BuildManifest_SystemMemoryNull_OmitsMemoryLine` (ends at line 60):

```csharp
    [TestMethod]
    public void BuildManifest_CarriesTheTaomBuildStamp()
    {
        var manifest = CrashBundleWriter.BuildManifest(MakeContext(null, EmptyLogs()), "report", "{}");

        StringAssert.Contains(manifest, "TAOM build: " + TestBuildStamp,
            "the manifest is read first, so the build identity belongs there without unzipping");
    }
```

**1c.** `TAOM.Tests/Core/Diagnostics/BuildStampReportTests.cs`: add after
`TryParseStamp_MalformedStamp_ReturnsFalse` (ends at line 50):

```csharp
    [TestMethod]
    public void TryParseStamp_DirtyAndNoGitSuffixes_StillParse()
    {
        foreach (var s in new[]
        {
            "build.20260923-184249Z+0123456789abcdef0123456789abcdef01234567.dirty",
            "build.20260923-184249Z+0123456789abcdef0123456789abcdef01234567.nogit",
            "build.20260923-184249Z+nogit",
        })
        {
            Assert.IsTrue(BuildStampReport.TryParseStamp(s, out var stamp), s);
            Assert.AreEqual(new DateTime(2026, 9, 23, 18, 42, 49, DateTimeKind.Utc), stamp, s);
        }
    }

    [TestMethod]
    public void ReadInformationalVersion_TaomAssembly_CarriesTheBuildStamp()
    {
        string text = BuildStampReport.ReadInformationalVersion(typeof(BuildStampReport).Assembly);

        StringAssert.StartsWith(text, "v");
        Assert.IsTrue(BuildStampReport.TryParseStamp(text, out _), text);
    }
```

`TryParseStamp_DirtyAndNoGitSuffixes_StillParse` is a **guard** test: it pins that the new suffixes
do not break the startup `[BuildStamp]` pairing line, and it would pass on its own. The RED comes
from the compile errors below.

**Verify**: `cd /e/repos/wt-plan-017 && dotnet test TAOM.Tests -p:DisableModuleCopy=true -p:ModuleId= --filter "FullyQualifiedName~BuildStampReportTests" 2>&1 | grep -E "error CS" | sort -u` (timeout 600000)
→ build fails; the distinct errors are exactly `CS1729` ("'IdentitySnapshot' does not contain a
constructor that takes 7 arguments", once in each of the two CrashReport test files) and `CS0122`
(naming `BuildStampReport.ReadInformationalVersion` as inaccessible due to its protection level).
Duplicate lines from the build summary are fine. Any other error code means a typo in your test
code: fix it and re-run.

### Step 2 (GREEN): add the field and print it

1. `Main/Core/Diagnostics/BuildStampReport.cs`:
   - Line 189: change `private static string ReadInformationalVersion(Assembly? asm)` to
     `internal static string ReadInformationalVersion(Assembly? asm)` (TAOM.Tests sees internals
     through the `InternalsVisibleTo` in `Main/TAOM.csproj`).
   - Replace the four summary lines 18-21 (quoted in Current state) with:

```csharp
/// Directory.Build.props stamps <c>InformationalVersion</c> as <c>build.yyyyMMdd-HHmmssZ</c>; the
/// .NET SDK then appends <c>+{commit-sha}</c>, marked <c>.dirty</c> or <c>.nogit</c> by
/// Directory.Build.props when it must, so the stamp is NOT at the end of the string. Both modules
/// are produced by the same build, so their stamps should agree to within seconds; a gap of hours
/// means a hand-copied module.
```

2. `Main/Features/CrashReport/Domain/IdentitySnapshot.cs`: add a last parameter, so the record reads:

```csharp
public sealed record IdentitySnapshot(
    string BannerlordVersion,
    string BannerlordExeFileVersion,
    string TaomVersion,
    string TaomDllSha1,
    string OriginatingPatchTarget,
    string LanguageCode,
    string TaomBuild);
```

3. `Main/Features/CrashReport/Collectors/IdentityCollector.cs`: add `using TAOM.Core.Diagnostics;`
   to the usings; after the `language` line add
   `string taomBuild = BuildStampReport.ReadInformationalVersion(typeof(IdentityCollector).Assembly);`
   (it already catches everything and never throws); and pass `TaomBuild: taomBuild` as the last
   named argument after `LanguageCode: language`.
4. `Main/Features/CrashReport/CrashReportService.cs:183`: the fallback becomes
   `?? new IdentitySnapshot("?", "?", "?", "?", originatingPatchTarget, "?", "?");`
5. `Main/Features/CrashReport/Rendering/PlainTextCrashReportRenderer.cs`: in `RenderIdentity`, right
   after the `TAOM:` line (line 67), add
   `sb.AppendLine($"Build:      {id.TaomBuild}");` (the label is `Build:` plus six spaces, so the
   value column lines up with the other twelve-character labels).
6. `Main/Features/CrashReport/Rendering/CrashBundleWriter.cs`: in `BuildManifest`, right after the
   `TAOM version:` line (line 107), add `sb.AppendLine($"TAOM build: {c.Identity.TaomBuild}");`

**Verify**:
- `cd /e/repos/wt-plan-017 && dotnet test TAOM.Tests -p:DisableModuleCopy=true -p:ModuleId= --filter "FullyQualifiedName~BuildStampReportTests|FullyQualifiedName~PlainTextCrashReportRendererTests|FullyQualifiedName~CrashBundleWriterTests" 2>&1 | tail -n 3` (timeout 600000)
  → the summary reports 36 passed and 0 failed (the exact wording varies by SDK).
- `cd /e/repos/wt-plan-017 && git grep -n "new IdentitySnapshot(" -- Main TAOM.Tests` → exactly 4
  lines: `IdentityCollector.cs` (named arguments on the following lines), `CrashReportService.cs`
  and the two test helpers, the last three each with seven arguments on the line.

### Step 3: full suite, then commit A

- `cd /e/repos/wt-plan-017 && dotnet test TAOM.Tests -p:DisableModuleCopy=true -p:ModuleId= > /e/repos/wt-plan-017-scratch/step3-tests.txt 2>&1; echo "rc=$?"; tail -n 5 /e/repos/wt-plan-017-scratch/step3-tests.txt` (timeout 600000)
  → Step 0's totals plus 4 tests, plus 4 passed; the failing names identical to Step 0's list
  (compare with `grep -E "^\s*Failed " ...step3-tests.txt`).
- Commit A (6 source files + 3 test files):
  `cd /e/repos/wt-plan-017 && git add Main/Core/Diagnostics/BuildStampReport.cs Main/Features/CrashReport/Domain/IdentitySnapshot.cs Main/Features/CrashReport/Collectors/IdentityCollector.cs Main/Features/CrashReport/CrashReportService.cs Main/Features/CrashReport/Rendering/PlainTextCrashReportRenderer.cs Main/Features/CrashReport/Rendering/CrashBundleWriter.cs TAOM.Tests/Core/Diagnostics/BuildStampReportTests.cs TAOM.Tests/Features/CrashReport/PlainTextCrashReportRendererTests.cs TAOM.Tests/Features/CrashReport/CrashBundleWriterTests.cs && git status --porcelain`
  → only `M ` lines for those 9 paths (6 under `Main`, 3 under `TAOM.Tests`; `IdentitySnapshot.cs`
  is one of the 6). Then commit with subject
  `feat(crash): v2.0.30 - put the TAOM build stamp in crash bundles` and a body saying: the Identity
  section and `manifest.txt` now print the TAOM assembly's `InformationalVersion`
  (`report.json` gains `TaomBuild`), so triage no longer needs the bundled log's `[BuildStamp]` line;
  plus the trailer `Not-tested: IdentityCollector wiring (reads ModuleHelper; exercised only in game)`.
- **Verify**: `cd /e/repos/wt-plan-017 && git log -1 --format=%s && git status --porcelain` → the
  subject, then nothing.

### Step 4 (RED): show that a dirty tree builds with a clean-looking stamp

1. Make the tree dirty without touching code: create the untracked file
   `E:/repos/wt-plan-017/Main/_plan017_probe.txt` with the Write tool, content `probe`. (A `.txt`
   in `Main` is not compiled; it only makes `git status` non-empty.)
2. `cd /e/repos/wt-plan-017 && git --no-optional-locks status --porcelain -- Main Dependencies Stubs Directory.Build.props`
   → exactly `?? Main/_plan017_probe.txt`.
3. `cd /e/repos/wt-plan-017 && dotnet build Main/TAOM.csproj -p:DisableModuleCopy=true -p:ModuleId=` (timeout 600000) → exit 0.
4. PowerShell tool: `(Get-Item E:/repos/wt-plan-017/Main/bin/Debug/net472/TAOM.dll).VersionInfo.ProductVersion; git -C E:/repos/wt-plan-017 rev-parse HEAD`

**Verify (RED)**: the stamp ends with `+<the HEAD SHA>` and contains **no** `.dirty`. That is the
defect: a dirty build that looks clean. Save both lines to
`E:/repos/wt-plan-017-scratch/step4-red.txt`. Keep the probe file for Step 5.

### Step 5 (GREEN): add the working-tree target to `Directory.Build.props`

Use the **Edit tool** on `E:/repos/wt-plan-017/Directory.Build.props`. The `config-protection.sh`
hook guards this file (Status, "Before dispatch"). If the Edit is blocked or asks for approval, STOP
and report the hook's message verbatim. Never write this file through Bash or PowerShell (`sed`,
`Set-Content`, a redirect or a script): that would bypass a protection gate.

Insert this block between the closing `</PropertyGroup>` (line 42) and `</Project>` (line 43). Keep
the file's tab indentation. `2&gt;nul` is the XML-escaped `2>nul`: Exec runs `cmd.exe`, and git
writes line-ending warnings to stderr, which `ConsoleToMSBuild` would otherwise capture as output
lines and read as dirt.

```xml

	<!--
	  Working-tree flag (plan 017). The .NET SDK appends +$(SourceRevisionId), HEAD's commit SHA, to
	  InformationalVersion, and that SHA says nothing about uncommitted edits. This target runs after
	  InitializeSourceControlInformation sets SourceRevisionId and before
	  AddSourceRevisionToInformationalVersion appends it, and marks it:
	    <sha>.dirty   compiled or deployed inputs differ from HEAD (untracked files count)
	    <sha>.nogit   the repository was found, but the git command failed
	    nogit         no repository information at all (a source zip)
	  tools/package_release.py (require-build) refuses any of the three for a release.
	-->
	<Target Name="TaomStampWorkingTreeState"
	        AfterTargets="InitializeSourceControlInformation"
	        BeforeTargets="AddSourceRevisionToInformationalVersion">
		<Exec Command="git --no-optional-locks status --porcelain -- Main Dependencies Stubs Directory.Build.props 2&gt;nul"
		      WorkingDirectory="$(MSBuildThisFileDirectory)"
		      ConsoleToMSBuild="true"
		      IgnoreExitCode="true"
		      IgnoreStandardErrorWarningFormat="true"
		      StandardOutputImportance="low"
		      StandardErrorImportance="low"
		      EchoOff="true"
		      Condition="'$(SourceRevisionId)' != ''">
			<Output TaskParameter="ConsoleOutput" ItemName="_TaomGitStatusLine" />
			<Output TaskParameter="ExitCode" PropertyName="_TaomGitStatusExitCode" />
		</Exec>
		<PropertyGroup>
			<SourceRevisionId Condition="'$(SourceRevisionId)' == ''">nogit</SourceRevisionId>
			<SourceRevisionId Condition="'$(_TaomGitStatusExitCode)' != '' AND '$(_TaomGitStatusExitCode)' != '0'">$(SourceRevisionId).nogit</SourceRevisionId>
			<SourceRevisionId Condition="'$(_TaomGitStatusExitCode)' == '0' AND '@(_TaomGitStatusLine)' != ''">$(SourceRevisionId).dirty</SourceRevisionId>
		</PropertyGroup>
	</Target>
```

The order of the three property lines matters: when there is no SHA, `Exec` is skipped, the exit code
stays empty, and only the first line fires.

**Verify (GREEN)**:
1. `cd /e/repos/wt-plan-017 && dotnet build Main/TAOM.csproj -p:DisableModuleCopy=true -p:ModuleId=` (timeout 600000) → exit 0, `0 Error(s)`, and the same `Warning(s)` count as `step0-warnings.txt` (at planning the shipped warnings were the two BUTR analyzer ones, `BHA0001` and `BHA0006`; the new target must add none).
2. PowerShell tool: `(Get-Item E:/repos/wt-plan-017/Main/bin/Debug/net472/TAOM.dll).VersionInfo.ProductVersion; (Get-Item E:/repos/wt-plan-017/Dependencies/bin/Debug/net472/TAOM.Dependencies.dll).VersionInfo.ProductVersion; git -C E:/repos/wt-plan-017 rev-parse HEAD`
   → both stamps end with `+<the HEAD SHA>.dirty`. Save to `E:/repos/wt-plan-017-scratch/step5-green.txt`.

If the stamp still has no `.dirty`, re-run the build once with `-v:d` redirected to
`/e/repos/wt-plan-017-scratch/step5-diag.txt` and
`grep -n "TaomStampWorkingTreeState\|_TaomGitStatus" /e/repos/wt-plan-017-scratch/step5-diag.txt | head -n 20`.
If the target did not run, or ran before `InitializeSourceControlInformation`, STOP and report those
lines: do not re-order targets by trial and error.

### Step 6: the two git-less paths, then commit B

1. **No repository information** (`nogit`): SourceLink's lookup is skipped when
   `EnableSourceControlManagerQueries` is false, which leaves `SourceRevisionId` empty.
   `cd /e/repos/wt-plan-017 && dotnet build Main/TAOM.csproj -p:DisableModuleCopy=true -p:ModuleId= -p:EnableSourceControlManagerQueries=false` (timeout 600000) → exit 0.
   PowerShell tool: `(Get-Item E:/repos/wt-plan-017/Main/bin/Debug/net472/TAOM.dll).VersionInfo.ProductVersion`
   → ends with `Z+nogit`. If it ends with `Z` alone (no `+nogit`), or with a SHA, STOP and report.
2. **Git command unavailable** (`.nogit`), PowerShell tool, one call:
   `$env:PATH = ($env:PATH -split ';' | Where-Object { $_ -and -not (Test-Path (Join-Path $_ 'git.exe')) }) -join ';'; where.exe git; dotnet build E:/repos/wt-plan-017/Main/TAOM.csproj -p:DisableModuleCopy=true -p:ModuleId=; (Get-Item E:/repos/wt-plan-017/Main/bin/Debug/net472/TAOM.dll).VersionInfo.ProductVersion`
   (timeout 600000) → `where.exe` reports it could not find `git`, the build succeeds, and the stamp
   ends with `+<HEAD SHA>.nogit`. If `where.exe git` still prints a path, this sub-check cannot be
   exercised on this machine: skip it and add `Not-tested: the .nogit branch (git.exe could not be
   hidden from PATH)` to commit B's body. Any other result: STOP.
3. Delete `E:/repos/wt-plan-017/Main/_plan017_probe.txt`
   (`cd /e/repos/wt-plan-017 && rm Main/_plan017_probe.txt && git status --porcelain`)
   → exactly ` M Directory.Build.props`.
4. Commit B: `cd /e/repos/wt-plan-017 && git add Directory.Build.props && git commit -m "feat(build): v2.0.30 - mark dirty and git-less builds in the stamp" -m "<body>"`,
   body: the SDK appends HEAD's SHA whatever the tree holds; a new `TaomStampWorkingTreeState`
   target in Directory.Build.props runs `git status --porcelain` over Main, Dependencies, Stubs and
   the props file and appends `.dirty`, or `nogit` / `.nogit` when git cannot tell; about 40 ms per
   project; verified by building a dirty tree, a clean tree and a git-less build and reading the
   DLL's ProductVersion. Include the Step 6.2 `Not-tested:` trailer if you skipped it.
5. **Verify (clean)**: the tree is now clean, so rebuild and read the stamp.
   `cd /e/repos/wt-plan-017 && git status --porcelain && dotnet build Main/TAOM.csproj -p:DisableModuleCopy=true -p:ModuleId=` (timeout 600000) → no status lines, build exit 0.
   PowerShell tool: `(Get-Item E:/repos/wt-plan-017/Main/bin/Debug/net472/TAOM.dll).VersionInfo.ProductVersion; git -C E:/repos/wt-plan-017 rev-parse HEAD`
   → the stamp ends with `+<exactly the new HEAD SHA>` and has **no** `.dirty` or `nogit`. Save to
   `E:/repos/wt-plan-017-scratch/step6-clean.txt`. If it reads `.dirty` on a clean tree, STOP
   (something under the scope is modified by the build itself; report
   `git --no-optional-locks status --porcelain -- Main Dependencies Stubs Directory.Build.props`).

### Step 7: full suite after the props change

`cd /e/repos/wt-plan-017 && dotnet test TAOM.Tests -p:DisableModuleCopy=true -p:ModuleId= > /e/repos/wt-plan-017-scratch/step7-tests.txt 2>&1; echo "rc=$?"; tail -n 5 /e/repos/wt-plan-017-scratch/step7-tests.txt` (timeout 600000)
→ the same totals as Step 3; failing names identical to Step 0's list. (Every project now runs the
new target, including `TAOM.Tests`; the tree is clean, so no stamp changes shape.)

### Step 8 (RED): failing Python tests for the packager gate

Edit `E:/repos/wt-plan-017/tools/tests/test_package_release.py`:

1. Add `import shutil` to the imports (keep them alphabetical: after `import os`).
2. Insert this block immediately before the final `if __name__ == "__main__":` line:

```python
# --------------------------------------------------------------------------- #
# --require-build: only a clean build of the release commit may ship           #
# --------------------------------------------------------------------------- #
SHA = "0123456789abcdef0123456789abcdef01234567"
STAMP = "build.20260923-184249Z"


def _dll_bytes(stamp: str) -> bytes:
    """A stand-in DLL: the stamp sits in the bytes the way the metadata stores it (UTF-8)."""
    return b"MZ\x90\x00\x01\x00" + stamp.encode("ascii") + b"\x00\x00trailer"


class TestBuildStamp(unittest.TestCase):
    def _dll(self, td, stamp):
        p = Path(td) / "TAOM.dll"
        p.write_bytes(_dll_bytes(stamp))
        return p

    def test_reads_the_stamp_out_of_dll_bytes(self):
        with tempfile.TemporaryDirectory() as td:
            self.assertEqual(pr.read_build_stamp(self._dll(td, f"{STAMP}+{SHA}")), f"{STAMP}+{SHA}")

    def test_reads_a_dirty_stamp_with_its_flag(self):
        with tempfile.TemporaryDirectory() as td:
            self.assertEqual(pr.read_build_stamp(self._dll(td, f"{STAMP}+{SHA}.dirty")),
                             f"{STAMP}+{SHA}.dirty")

    def test_no_stamp_reads_as_none(self):
        with tempfile.TemporaryDirectory() as td:
            p = Path(td) / "TAOM.dll"
            p.write_bytes(b"\0" * 300)
            self.assertIsNone(pr.read_build_stamp(p))

    def test_clean_stamp_at_the_expected_commit_passes(self):
        self.assertIsNone(pr.check_build_stamp(f"{STAMP}+{SHA}", SHA))

    def test_older_dot_separator_passes(self):
        self.assertIsNone(pr.check_build_stamp(f"{STAMP}.{SHA}", SHA))

    def test_dirty_stamp_is_refused(self):
        self.assertIn("uncommitted", pr.check_build_stamp(f"{STAMP}+{SHA}.dirty", SHA))

    def test_nogit_stamps_are_refused(self):
        for stamp in (f"{STAMP}+nogit", f"{STAMP}+{SHA}.nogit"):
            self.assertIn("git could not", pr.check_build_stamp(stamp, SHA), stamp)

    def test_stamp_from_another_commit_is_refused(self):
        self.assertIn("built at ffffffffffff", pr.check_build_stamp(f"{STAMP}+{'f' * 40}", SHA))

    def test_missing_stamp_is_refused(self):
        self.assertIn("no single build stamp", pr.check_build_stamp(None, SHA))


@unittest.skipUnless(shutil.which("git"), "git is not on PATH")
class TestRequireBuildCli(unittest.TestCase):
    def setUp(self):
        self.head = subprocess.run(
            ["git", "rev-parse", "HEAD"], cwd=TOOL.parent.parent,
            capture_output=True, text=True, check=True,
        ).stdout.strip()

    def _modules(self, td, stamp):
        src = Path(td) / "Modules"
        dll = src / "TAOM/bin/Win64_Shipping_Client/TAOM.dll"
        dll.parent.mkdir(parents=True)
        dll.write_bytes(_dll_bytes(stamp))
        return src

    def _gate(self, src, td, rev="HEAD"):
        return _run("--source", str(src), "--dest", str(Path(td) / "out"), "--modules", "TAOM",
                    "--dry-run", "--require-build", rev)

    def test_accepts_a_clean_dll_built_at_the_rev(self):
        with tempfile.TemporaryDirectory() as td:
            r = self._gate(self._modules(td, f"{STAMP}+{self.head}"), td)
            self.assertEqual(r.returncode, 0, r.stderr)
            self.assertIn("build stamp OK", r.stdout)

    def test_refuses_a_dirty_dll(self):
        with tempfile.TemporaryDirectory() as td:
            r = self._gate(self._modules(td, f"{STAMP}+{self.head}.dirty"), td)
            self.assertEqual(r.returncode, 2)
            self.assertIn("uncommitted", r.stderr)

    def test_refuses_when_the_dll_is_missing(self):
        with tempfile.TemporaryDirectory() as td:
            src = Path(td) / "Modules"
            _write(src, "TAOM/SubModule.xml", 10)
            r = self._gate(src, td)
            self.assertEqual(r.returncode, 2)
            self.assertIn("is missing", r.stderr)

    def test_refuses_an_unresolvable_rev(self):
        with tempfile.TemporaryDirectory() as td:
            r = self._gate(self._modules(td, f"{STAMP}+{self.head}"), td, rev="no-such-rev-017")
            self.assertEqual(r.returncode, 2)
            self.assertIn("cannot resolve", r.stderr)


```

3. Update the module docstring's contract list (lines 11-23): add one bullet at the end,
   `  - --require-build: a DLL whose stamp is dirty, git-less or from another commit is refused`.

**Verify (RED)**: `cd /e/repos/wt-plan-017 && python tools/tests/test_package_release.py 2>&1 | tail -n 3`
→ `Ran 46 tests` and `FAILED (failures=4, errors=9)` (the nine `TestBuildStamp` tests error with
`AttributeError`; the four CLI tests fail because argparse rejects `--require-build`). If git is not on
PATH the CLI class is skipped instead: `FAILED (errors=9, skipped=4)`. Any other count: re-read your
test code before going on.

### Step 9 (GREEN): the `--require-build` gate in `tools/package_release.py`

Edit `E:/repos/wt-plan-017/tools/package_release.py`:

1. Add `import subprocess` to the imports (after `import shutil`, before `import sys`).
2. After the `CANDIDATE_RULES = ...` line (line 100) and its blank lines, add:

```python
REPO_ROOT = Path(__file__).resolve().parent.parent

# The two assemblies TAOM compiles. Each carries the build stamp from Directory.Build.props.
SHIPPED_DLLS = {
    "TAOM": "bin/Win64_Shipping_Client/TAOM.dll",
    "TAOM.Dependencies": "bin/Win64_Shipping_Client/TAOM.Dependencies.dll",
}

# InformationalVersion as the assembly metadata stores it (UTF-8): build.<yyyyMMdd-HHmmss>Z, then
# '+' ('.' on older builds), then the commit SHA or "nogit", then an optional .dirty or .nogit flag.
STAMP_RE = re.compile(rb"build\.\d{8}-\d{6}Z[+.](?:[0-9a-f]{40}|nogit)(?:\.(?:dirty|nogit))?")
STAMP_PARTS_RE = re.compile(r"Z[+.](?P<rev>[0-9a-f]{40}|nogit)(?:\.(?P<flag>dirty|nogit))?$")


def read_build_stamp(dll: Path) -> str | None:
    """The one build stamp in a compiled TAOM assembly; None when there is none or several."""
    found = {m.group(0).decode("ascii") for m in STAMP_RE.finditer(dll.read_bytes())}
    return found.pop() if len(found) == 1 else None


def check_build_stamp(stamp: str | None, expected_sha: str) -> str | None:
    """None when the stamp proves a clean build of expected_sha, otherwise why it does not."""
    if stamp is None:
        return "no single build stamp in the DLL (a pre-2026-08-01 build, or not a TAOM assembly)"
    m = STAMP_PARTS_RE.search(stamp)
    if m is None:
        return f"unrecognised build stamp '{stamp}'"
    if m["rev"] == "nogit" or m["flag"] == "nogit":
        return f"built where git could not report the tree ({stamp})"
    if m["flag"] == "dirty":
        return f"built from a working tree with uncommitted changes ({stamp})"
    if m["rev"] != expected_sha.lower():
        return f"built at {m['rev'][:12]}, but the release is {expected_sha[:12]} ({stamp})"
    return None


def resolve_commit(rev: str) -> str | None:
    """The full SHA of the commit `rev` names in this repository, or None."""
    try:
        r = subprocess.run(["git", "rev-parse", "--verify", "--quiet", f"{rev}^{{commit}}"],
                           cwd=REPO_ROOT, capture_output=True, text=True)
    except OSError:
        return None
    sha = r.stdout.strip()
    return sha if r.returncode == 0 and sha else None


def require_build(plans, rev: str) -> list:
    """Every reason the planned TAOM assemblies are not a clean build of `rev` (empty: all good)."""
    sha = resolve_commit(rev)
    if sha is None:
        return [f"cannot resolve '{rev}' to a commit in {REPO_ROOT}"]
    problems, checked = [], 0
    for p in plans:
        rel = SHIPPED_DLLS.get(p.name)
        if rel is None:
            continue
        dll = p.root / rel
        if not dll.is_file():
            problems.append(f"{p.name}: {rel} is missing")
            continue
        checked += 1
        reason = check_build_stamp(read_build_stamp(dll), sha)
        if reason:
            problems.append(f"{p.name}: {reason}")
    if checked == 0 and not problems:
        problems.append("neither TAOM nor TAOM.Dependencies is in the module set; nothing to verify")
    return problems
```

3. In `main()`, add after the `--json` argument (line 311):

```python
    ap.add_argument("--require-build", metavar="REV",
                    help="refuse unless TAOM.dll and TAOM.Dependencies.dll are clean builds of "
                         "this tag or commit (checked in --dry-run too)")
```

4. In `main()`, insert immediately before `    if args.json:` (line 347):

```python
    if args.require_build:
        problems = require_build(plans, args.require_build)
        if problems:
            print(f"\nERROR: not a clean build of {args.require_build}; refusing to package:",
                  file=sys.stderr)
            for msg in problems:
                print(f"  {msg}", file=sys.stderr)
            return 2
        print(f"\nbuild stamp OK: every TAOM assembly is a clean build of {args.require_build}")

```

5. Docstring: after the usage line `  python tools/package_release.py ... --exclude-candidate RACE_TEST --json manifest.json`
   (line 32) add `  python tools/package_release.py --source "<game>/Modules" --dest D:/taom-release --require-build v2.0.31 --dry-run`,
   and change the exit-code line to
   `Exit codes: 0 ok · 1 nothing to do · 2 bad input / unknown entries / non-empty destination / failed --require-build.`

**Verify (GREEN)**:
1. `cd /e/repos/wt-plan-017 && python tools/tests/test_package_release.py 2>&1 | tail -n 3` → `Ran 46 tests`, `OK` (or `OK (skipped=4)` without git).
2. Against the real DLLs in `Main/bin` and `Dependencies/bin` (last rebuilt by Step 7's `dotnet test`,
   a clean build of commit B):
   `cd /e/repos/wt-plan-017 && mkdir -p /e/repos/wt-plan-017-scratch/Modules/TAOM/bin/Win64_Shipping_Client /e/repos/wt-plan-017-scratch/Modules/TAOM.Dependencies/bin/Win64_Shipping_Client && cp Main/bin/Debug/net472/TAOM.dll /e/repos/wt-plan-017-scratch/Modules/TAOM/bin/Win64_Shipping_Client/ && cp Dependencies/bin/Debug/net472/TAOM.Dependencies.dll /e/repos/wt-plan-017-scratch/Modules/TAOM.Dependencies/bin/Win64_Shipping_Client/ && python tools/package_release.py --source /e/repos/wt-plan-017-scratch/Modules --dest /e/repos/wt-plan-017-scratch/out --modules TAOM TAOM.Dependencies --dry-run --require-build HEAD; echo "rc=$?"`
   → prints `build stamp OK: every TAOM assembly is a clean build of HEAD` and `rc=0`. (HEAD is still
   commit B: Step 9's edits are under `tools/`, outside the stamp's scope, and are not committed yet.)
3. Same command with `--require-build HEAD~1` → `rc=2`, stderr names both modules with
   `built at <12 hex>, but the release is <12 hex>`.

If 2 fails with `no single build stamp`, STOP and report the output of the PowerShell read of the
two DLLs' `ProductVersion` (the metadata encoding differs from what this plan measured).

### Step 10: commit C

`cd /e/repos/wt-plan-017 && git add tools/package_release.py tools/tests/test_package_release.py && git status --porcelain`
→ exactly those two `M ` lines. Commit with subject
`feat(release): v2.0.30 - refuse to package a dirty or off-tag DLL` and a body naming the new
`--require-build REV` flag, what it refuses (a `.dirty`, `nogit` or `.nogit` stamp, a SHA other than
REV's commit, a missing or unstamped DLL) and that it also runs under `--dry-run`, so it works as a
check on any Modules folder (13 new tests). Verify: `git log -1 --format=%s` → the subject;
`git status --porcelain` → nothing.

### Step 11: documentation, skill and CHANGELOG, then commit D

All new lines: no em or en dash.

1. `.claude/skills/release/SKILL.md` (if the pre-cleared Phase 1 item 5 from the drift check is
   already committed in your worktree, every SKILL.md line number below is 3 higher: Phase 7's
   fence 107, `## Gotchas` 109, the bullet 117-119; go by the quoted text, not the numbers):
   - Insert a new section between the end of Phase 7 (line 104, the `git describe` code block's
     closing fence) and `## Gotchas` (line 106):

```markdown
## Phase 8: Build and package at the tag

The Phase 2 build ran before the release commit existed, so its DLL carries the parent commit's SHA.
Rebuild at the tag before anything ships.

1. `git status --porcelain` is empty and `git rev-parse HEAD` equals `git rev-parse vX.Y.Z^{commit}`.
   Then run `./build.ps1`. If another session has started editing, do not build that tree: build a
   clean worktree of the tag instead (`git worktree add ../taom-release-vX.Y.Z vX.Y.Z`, run
   `./build.ps1` there, then `git worktree remove ../taom-release-vX.Y.Z`). `build.ps1` compiles and
   deploys every file in the tree, committed or not.
2. Gate the DLLs: `python tools/package_release.py --source "<game>/Modules" --dest <out> --require-build vX.Y.Z --dry-run`
   must print `build stamp OK` and exit 0. It refuses a `TAOM.dll` or `TAOM.Dependencies.dll` whose
   stamp says `.dirty` or `nogit`, or names a commit other than the tag's.
3. Package: the same command without `--dry-run` (plus `--keep-rdc` or `--allow-unknown` if the dry
   run's report calls for them).
4. If the player package is assembled somewhere else (the editor package in
   `E:\LOTRAOM_Releases\<channel>\Modules\`), run step 2's dry run with `--source` pointing at that
   folder before uploading it.
```

   - Replace the Gotchas bullet at lines 114-116 (quoted in Current state) with:

```markdown
- **Version ≠ build stamp.** `Directory.Build.props` stamps `InformationalVersion` per build
  (`build.yyyyMMdd-HHmmssZ`) and freezes `AssemblyVersion` deliberately. The SDK appends
  `+<commit SHA>`, and a build of a tree with uncommitted changes under `Main`, `Dependencies`,
  `Stubs` or `Directory.Build.props` appends `.dirty` after it (`nogit` or `.nogit` when git could
  not tell). The stamp identifies a build; the tag identifies a release.
```

2. `docs/reference/release-process.md`:
   - Line 69 is:

```markdown
1. Tree clean (or, when another session's edits are present, every path staged explicitly and theirs left out),
```

     Replace that one line with the line below (the continuation lines 70-71 stay as they are):

```markdown
1. Tree clean (`git status --porcelain` empty; if another session's edits are present, stop, or cut the release from a clean worktree of the release branch, because `build.ps1` compiles and deploys every file in the tree, committed or not),
```

   - After step 7 (line 83, the line starting `7. \`git tag -a vX.Y.Z`) add this line:

```markdown
8. Build at the tag and gate the DLLs: `python tools/package_release.py --source "<game>/Modules" --dest <out> --require-build vX.Y.Z --dry-run` must print `build stamp OK`, then package without `--dry-run` (the skill's Phase 8).
```

   - In the paragraph at lines 59-63, directly after the sentence that ends
     `which both modules log at startup so a` / `mismatched pair is one line in the log.` (it wraps
     across lines 61-62), insert this sentence:

```markdown
The .NET SDK appends `+<commit SHA>` to that stamp, and a build of a tree with uncommitted changes to its inputs appends `.dirty` after the SHA (`nogit` or `.nogit` when git could not tell).
```

   - At the end of "Resolving a crash report to a commit" (after line 110, the line starting
     `If the version is one of the five phantoms below`), add a new paragraph:

```markdown
A bundle's `report.txt` (Identity section, `Build:` line) and `manifest.txt` (`TAOM build:` line)
also carry the build stamp, for example `v2.0.0.0 build.20260923-184249Z+c79a585218ad...`. That SHA
is the commit the DLL was compiled from: `git show <sha>`. A `.dirty` suffix means the build also
held uncommitted edits, so the commit is only the nearest known state; `nogit` means git could not
tell. Bundles written before this field existed lack the line: read the `[BuildStamp]` line near the
top of the bundled `taom_debug.log` instead.
```

3. `docs/features/crash-report.md:117`: replace the Identity row (quoted in Current state) with:

```markdown
| Identity | `Native` + `TAOM` module versions, Bannerlord.exe FileVersion, TAOM.dll SHA1, TAOM build stamp (`InformationalVersion`: build time, commit SHA, `.dirty` or `nogit` flag; also in `manifest.txt`), language code |
```
4. `CHANGELOG.md`: if the first `## ` heading is today's date, add the entry directly under it;
   otherwise add a new `## <today, YYYY-MM-DD>` heading above the first one. Entry:

```markdown
### feat(build): v2.0.30 - dirty-tree flag in the build stamp, build field in crash bundles

A build stamp named HEAD's commit whatever the working tree held, so a DLL built from uncommitted
edits looked like a clean build of that commit, and a release could ship one.

- **Stamp**: a `TaomStampWorkingTreeState` target in `Directory.Build.props` runs
  `git status --porcelain` over `Main`, `Dependencies`, `Stubs` and the props file and appends
  `.dirty` to the SHA the SDK writes into `InformationalVersion` (`nogit` or `.nogit` when git
  cannot tell). About 40 ms per project build.
- **Crash bundles**: `report.txt` prints a `Build:` line in the Identity section, `manifest.txt` a
  `TAOM build:` line, and `report.json` gains `TaomBuild`.
- **Releases**: `tools/package_release.py --require-build <tag>` refuses a `TAOM.dll` or
  `TAOM.Dependencies.dll` that is dirty, git-less or built at another commit; `/release` gains
  Phase 8 (rebuild at the tag, then gate and package), and `release-process.md` no longer allows
  releasing from a tree that holds another session's edits.
- Tests: 4 new C# (`BuildStampReportTests`, `PlainTextCrashReportRendererTests`,
  `CrashBundleWriterTests`), 13 new Python (`test_package_release.py`).
```

   Replace `v2.0.30` with the version you read in the Git workflow step if it differs.

**Verify**:
- `cd /e/repos/wt-plan-017 && python tools/lint_docs.py > /e/repos/wt-plan-017-scratch/lint.txt 2>&1; echo "rc=$?"; grep -n "release/SKILL.md\|release-process.md\|crash-report.md\|CHANGELOG.md" /e/repos/wt-plan-017-scratch/lint.txt`
  → `rc=0`, and no line that is an `[em-dash]` finding for a file you changed (other findings on
  those files that existed at `b2e387db` are not yours; check with
  `git -C E:/repos/TAOM show b2e387db:<path> | grep -c "<the flagged text>"`).
- `cd /e/repos/wt-plan-017 && git diff -U0 -- .claude/skills/release/SKILL.md docs/reference/release-process.md docs/features/crash-report.md CHANGELOG.md | python -X utf8 -c "import sys; print(sum(1 for l in sys.stdin if l.startswith('+') and not l.startswith('+++') and (chr(0x2013) in l or chr(0x2014) in l)))"`
  → `0` (counts em and en dashes on the lines you added). The replaced Gotchas bullet keeps the
  existing `≠` sign; that is not a dash.
- Commit D: `cd /e/repos/wt-plan-017 && git add .claude/skills/release/SKILL.md docs/reference/release-process.md docs/features/crash-report.md CHANGELOG.md && git status --porcelain`
  → exactly those four `M ` lines. Subject `docs(release): v2.0.30 - build and package at the release tag`,
  body: the skill's Phase 8, the reconciled step 1, the crash-report triage paragraph. The
  `check-changelog-changed.sh` gate requires `CHANGELOG.md` in this commit because it touches
  `.claude/`; it is staged. Verify `git log -1 --format=%s` → the subject; `git status --porcelain` → nothing.

### Step 12: final verification

- `cd /e/repos/wt-plan-017 && dotnet build Main/TAOM.csproj -p:DisableModuleCopy=true -p:ModuleId=` (timeout 600000) → exit 0.
- PowerShell tool: `(Get-Item E:/repos/wt-plan-017/Main/bin/Debug/net472/TAOM.dll).VersionInfo.ProductVersion; git -C E:/repos/wt-plan-017 rev-parse HEAD`
  → stamp ends with `+<exactly HEAD>` (commit D), no `.dirty`, no `nogit`.
- `cd /e/repos/wt-plan-017 && dotnet test TAOM.Tests -p:DisableModuleCopy=true -p:ModuleId= > /e/repos/wt-plan-017-scratch/final-tests.txt 2>&1; echo "rc=$?"; tail -n 5 /e/repos/wt-plan-017-scratch/final-tests.txt` (timeout 600000)
  → Step 0 totals plus 4; failing names identical to Step 0's.
- `cd /e/repos/wt-plan-017 && python tools/tests/test_package_release.py 2>&1 | tail -n 3` → `Ran 46 tests`, `OK`.
- `cd /e/repos/wt-plan-017 && git log --format=%s $(cat /e/repos/wt-plan-017-scratch/step0-base.txt)..HEAD` → exactly the four subjects
  (newest first: docs(release), feat(release), feat(build), feat(crash)).
- `cd /e/repos/wt-plan-017 && git diff --name-only $(cat /e/repos/wt-plan-017-scratch/step0-base.txt)..HEAD | sort` → exactly the 16
  in-scope paths.
- Leave the worktree, the branch and `E:/repos/wt-plan-017-scratch/` in place for the orchestrator.

## Test plan

- **New C# tests (4)**:
  - `PlainTextCrashReportRendererTests.Render_Identity_PrintsTheTaomBuildStamp`: the Identity section prints `Build:      <stamp>`.
  - `CrashBundleWriterTests.BuildManifest_CarriesTheTaomBuildStamp`: `manifest.txt` prints `TAOM build: <stamp>`.
  - `BuildStampReportTests.ReadInformationalVersion_TaomAssembly_CarriesTheBuildStamp`: the real TAOM.dll the tests load carries a parseable stamp (ties the MSBuild output to the reader).
  - `BuildStampReportTests.TryParseStamp_DirtyAndNoGitSuffixes_StillParse`: guard; `.dirty`, `.nogit` and `+nogit` keep the startup pairing line working.
- **Changed C# tests**: the two helpers `MakeMinimalContext` and `MakeContext` pass a seventh `IdentitySnapshot` argument.
- **New Python tests (13)** in `tools/tests/test_package_release.py`: `TestBuildStamp` (9: read clean, read dirty, no stamp, clean passes, older `.` separator passes, dirty refused, both nogit forms refused, other commit refused, missing stamp refused) and `TestRequireBuildCli` (4: accepts a clean DLL at HEAD, refuses a dirty DLL, refuses a missing DLL, refuses an unresolvable rev). The CLI class is skipped without git on PATH.
- **Structural patterns**: C# tests follow the existing methods in the same three files; Python tests follow `TestCli` in the same file (`_run`, `tempfile`).
- **MSBuild target**: no unit-test seam. Verified by builds and DLL reads: dirty (Step 5), no repository (6.1), git missing (6.2), clean (6.5, 12).
- **Not tested** (name in the commit trailers): `IdentityCollector` wiring (it reads `ModuleHelper`, game only); the `.nogit` branch if Step 6.2 could not hide git; `/release` Phase 8 as a whole (a maintainer procedure that deploys).
- **Verification**: `dotnet test TAOM.Tests -p:DisableModuleCopy=true -p:ModuleId=` shows Step 0's totals plus 4 passing; `python tools/tests/test_package_release.py` shows `Ran 46 tests` and `OK`.

## Done criteria

ALL must hold (run from `/e/repos/wt-plan-017`):

- [ ] `dotnet build Main/TAOM.csproj -p:DisableModuleCopy=true -p:ModuleId=` exits 0.
- [ ] `dotnet test TAOM.Tests -p:DisableModuleCopy=true -p:ModuleId=` reports Step 0's totals plus 4 passed, and the only failures are Step 0's (the two Animalia/Elk tests, or none).
- [ ] `dotnet test ... --filter "FullyQualifiedName~BuildStampReportTests|FullyQualifiedName~PlainTextCrashReportRendererTests|FullyQualifiedName~CrashBundleWriterTests"` → `Passed: 36`, `Failed: 0`.
- [ ] `python tools/tests/test_package_release.py` → `Ran 46 tests`, `OK`.
- [ ] `E:/repos/wt-plan-017-scratch/step4-red.txt` shows a stamp without `.dirty`; `step5-green.txt` shows both DLL stamps ending `.dirty`; `step6-clean.txt` shows a stamp ending `+` and the HEAD SHA of commit B, with no suffix.
- [ ] After commit D, the rebuilt `TAOM.dll`'s `ProductVersion` ends with `+$(git rev-parse HEAD)` exactly.
- [ ] `git grep -n "TaomStampWorkingTreeState" -- Directory.Build.props` → 1 line; `git grep -n "TaomBuild" -- Main/Features/CrashReport` → lines in `IdentitySnapshot.cs`, `IdentityCollector.cs`, `PlainTextCrashReportRenderer.cs`, `CrashBundleWriter.cs`.
- [ ] `git grep -n "require-build" -- tools/package_release.py .claude/skills/release/SKILL.md docs/reference/release-process.md` → at least one line in each of the three files.
- [ ] `python tools/lint_docs.py` exits 0 with no `[em-dash]` finding on a line you added.
- [ ] `git diff --name-only <BASE>..HEAD | sort` (BASE from `step0-base.txt`) lists exactly the 16 in-scope paths; `git status --porcelain` is empty.
- [ ] `git log --format=%B <BASE>..HEAD | grep -ci "co-authored-by"` → `0`.

## STOP conditions

Stop and report back (do not improvise) if:

- The drift check prints anything beyond the pre-cleared SKILL.md item, or an excerpt in "Current state" does not match the file in your worktree. (SKILL.md line numbers 3 higher because of that pre-cleared item is not a mismatch.)
- Step 0.3: `dotnet --version` is not `10.0.401` **and** any of the four greps finds nothing. (A different version whose greps all match is fine; note it in commit B's body.)
- Step 0.5: a fresh worktree reports any line for `git --no-optional-locks status --porcelain -- Main Dependencies Stubs Directory.Build.props`.
- Step 5: the stamp does not gain `.dirty` on a dirty tree after one diagnostic build, or the target runs before `InitializeSourceControlInformation`.
- Step 6.1: the `EnableSourceControlManagerQueries=false` build does not end in `+nogit`.
- Step 6.5 or 12: a clean tree builds a stamp that carries `.dirty` or `nogit`, or a SHA other than HEAD.
- The new target adds build warnings or errors (compare the warning count with Step 0's build), or makes `dotnet test` fail anything new.
- Step 9.2: the real DLLs yield `no single build stamp` (the metadata encoding is not what was measured at planning).
- The change seems to need `Main/SubModule.cs`, `Main/IoC.cs`, `Main/TAOM.csproj`, `Dependencies/TAOM.Dependencies.csproj`, `build.ps1` or a workflow file. Report the exact line you would add instead.
- Test failures appear outside the Step 0 list, or a step's verification fails twice after a reasonable fix attempt.
- A commit hook denies or asks, or `git worktree add` fails because the branch or directory exists.
- An Edit or Write is blocked or asks for approval (expected only for `Directory.Build.props` in Step 5, via `config-protection.sh`). Report the message verbatim; never retry the write through Bash or PowerShell.
- A Bash call is refused or waits on a permission prompt (the compound `cd`, or a write under `E:/repos/wt-plan-017-scratch`).
- You find you edited, built or ran anything under `E:/repos/TAOM/` (the main tree). Report exactly what; do not revert main-tree files yourself.

## Maintenance notes

- **Issue #593 (open)** plans to add a Dependencies content hash to `InformationalVersion` "next to the existing `build.` marker" and to classify the pair by hash first. It will edit `Directory.Build.props` and `BuildStampReport.cs` too. This plan's flag rides on `SourceRevisionId`, so it always stays at the very end (`...+<sha>.dirty`); #593's hash belongs before the `+`. `TryParseStamp` slices a fixed width after `build.`, so both coexist. Whoever lands second: keep both, and extend `STAMP_RE` in `tools/package_release.py` if the hash changes what follows `Z`.
- **The scope list** `Main Dependencies Stubs Directory.Build.props` in the target is the definition of "compiled or deployed input". A new compiled project folder, or a new deployed folder outside `Main`, must be added there or its uncommitted edits will not mark the build.
- **A data-only edit marks the DLL dirty.** An uncommitted XML change under `Main/_Module` sets `.dirty` even though the code is identical. That is deliberate: the same tree's deploy ships that XML, and the release gate must refuse it.
- **CI** (once it compiles C#, audit seed F1): a checkout is clean, so CI DLLs carry a bare SHA; a shallow clone still has `.git`, so no `nogit`. If CI ever builds from an exported tarball, its DLLs read `+nogit` and the gate will refuse them by design.
- **Reviewer focus for `/deep-review`**: the target's ordering against the SDK targets (Step 0.3 greps, Step 5 result); the `2&gt;nul` redirect and `IgnoreExitCode`; that the record's new field is last so the positional fallback in `CrashReportService.cs:183` cannot shift; `STAMP_RE` against the real DLL bytes (Step 9.2); the `release-process.md` step 1 wording now agreeing with the skill's Phase 1; and that rewriting `SourceRevisionId` is global: every consumer of the property sees `<sha>.dirty` (harmless today, but SourceLink/NuGet `RepositoryCommit` would carry it if a project is ever packed).
- **Deferred**: building the release DLLs in CI on tag push and packaging from that artifact (needs CI to compile C# first); a registry of shipped DLL hashes (the stamp makes it unnecessary for new releases).
