# Plan 009: Apply every Harmony patch category through one guarded helper

> **Executor instructions**: Follow this plan step by step. Run every
> verification command and confirm the expected result before moving to the
> next step. If anything in the "STOP conditions" section occurs, stop and
> report; do not improvise. Do NOT edit `plans/README.md`: it has no row
> for this plan, and the orchestrator maintains that index.
>
> **Shell**: every `grep`, `git` and `python` verification in this plan is
> written for the **Bash tool** (Git Bash, GNU grep 3.0; the `\|` alternation
> and `grep -cF` need it). Run `dotnet` from either shell.
>
> **Drift check (run first, from the worktree root)**:
> `git diff --stat b2e387db..HEAD -- Main/SubModule.cs Main/PatchCategoryApplier.cs TAOM.Tests/Infrastructure/PatchCategoryApplierTests.cs TAOM.Tests/Features/BanditManagement/Patch86HideoutBossFightBindingTests.cs TAOM.Tests/Features/Diplomacy/Patch80KingdomVoteDeadlockBindingTests.cs TAOM.Tests/Features/Enlistment/Patch85EnlistedDetachDeferralBindingTests.cs TAOM.Tests/Features/FieldCamp/FieldCampWiringTests.cs TAOM.Tests/Features/LordPartyTemplates/Patch88LordPartyTemplateTests.cs TAOM.Tests/Features/MapEventGuard/Patch82MapEventObserverInvariantBindingTests.cs TAOM.Tests/Features/MapEventGuard/Patch84SiegeAftermathMenuGuardTests.cs TAOM.Tests/Features/Refuge/RefugeWiringTests.cs TAOM.Tests/Features/ReturnToArmy/Patch87ReturnToArmyTests.cs TAOM.Tests/Features/LordSpawnGuard/Patch65LandlessCultureSpawnGuardBindingTests.cs docs/features/crash-report.md docs/reference/engine/submodule-lifecycle-and-harmony.md`
> Expected: no output (at planning time `HEAD` was `4b5662b2`, which touched none of these).
> If any in-scope file changed since this plan was written, compare the
> "Current state" excerpts against the live code before proceeding; on a
> mismatch, treat it as a STOP condition.

## Status

- **Priority**: P2
- **Effort**: M (one small class, 84 mechanical call-site edits, 9 one-string test edits, 3 lines in 2 docs)
- **Risk**: MED (single-owner `Main/SubModule.cs`, which another session is also editing; three guarded sites whose `catch` side effects must be kept by hand)
- **Depends on**: none
- **Category**: bug
- **Planned at**: commit `b2e387db`, 2026-09-23
- **Issue**: create before implementation lands (orchestrator)

## Why this matters

`Main/SubModule.cs` applies TAOM's Harmony patches one category at a time with `_harmony.PatchCategory("PatchNN_X")`. Harmony 2.4.2 has no catch around a category: the first patch class whose target method no longer resolves (an engine rename after a Steam force-bump, or another mod reshaping IL) throws a `HarmonyException` straight out of the SubModule hook. 64 of the 84 call sites are bare. In `OnSubModuleLoad` (13 bare sites) the engine logs the throw and rethrows, so the game does not start with TAOM enabled. In `OnGameInitializationFinished` (50 bare sites) the once-per-process flag is set BEFORE the batch, so a throw skips every later category (including the crash guards Patch65, Patch82 and Patch84), the three watchdogs, `ManualPatchApplicator.ApplyAll` and the Harmony census; a crash bundle is written, a saved game never finishes loading, and a new game runs half-patched. After this plan, one drifted binding costs exactly one category: it is logged at Error with its cause, a red on-screen line names it, and every other category still applies.

## Current state

All excerpts are from commit `b2e387db`. Line numbers are for that commit; after Step 3 inserts lines, find each site by its category string, not its line number.

### Files and roles

- `Main/SubModule.cs` (2,148 lines at `b2e387db`): the module entry point; owns every `PatchCategory` call. **Single-owner file**: CLAUDE.md tells subagents "`Main/IoC.cs` and `Main/SubModule.cs` are single-owner: recommend, don't edit." The orchestrator's dispatch of this plan is the authorization to edit `SubModule.cs`, on your worktree branch only (see "Git workflow") and only within the edit list under "Scope". Do not halt on that CLAUDE.md line. `Main/IoC.cs` stays recommend-only.
- `Main/ManualPatchApplicator.cs`: applies the manual `harmony.Patch(...)` patches; the model for a root-level `internal static class` in namespace `TAOM`. Not edited.
- `Main/Core/Logging/IModLogger.cs`: the logger interface (`LogInfo`, `LogDebug`, `LogWarning`, `LogError`, `LogFilePath`). Not edited.
- New: `Main/PatchCategoryApplier.cs` (the guarded helper) and `TAOM.Tests/Infrastructure/PatchCategoryApplierTests.cs` (its tests plus the source gate).
- Nine test files that pin the exact spelling `_harmony.PatchCategory("...")` or `.PatchCategory("...")` in `SubModule.cs` text (listed in Step 7).

### The engine and Harmony facts (verified during planning; do not re-derive)

Harmony is `Lib.Harmony 2.4.2` (`Dependencies/TAOM.Dependencies.csproj:70`, `Main/TAOM.csproj:110`, `TAOM.Tests/TAOM.Tests.csproj:19`). Decompiled with `ilspycmd -t HarmonyLib.Harmony ~/.nuget/packages/lib.harmony/2.4.2/lib/net472/0Harmony.dll`:

```csharp
public void PatchCategory(string category)
{
    MethodBase method = new StackTrace().GetFrame(1).GetMethod();
    Assembly assembly = method.ReflectedType.Assembly;
    PatchCategory(assembly, category);
}

public void PatchCategory(Assembly assembly, string category)
{
    Dictionary<string, List<Type>> value = AssemblyCachedCategories.GetValue(assembly, BuildCategoryCache);
    if (value.TryGetValue(category, out var value2))
    {
        value2.Do(delegate(Type type)
        {
            CreateClassProcessor(type).Patch();
        });
    }
}
```

So: (1) the one-argument overload picks the assembly from the caller's stack frame, which is why the helper must call the two-argument overload with `typeof(SubModule).Assembly`; (2) there is no catch, so the first failing class aborts the rest of its category and throws to the caller; (3) an unknown category name applies nothing and does not throw. `PatchClassProcessor.PatchWithAttributes` throws `ArgumentException("Undefined target method for patch method " + ...)` when the target does not resolve, and `ReportException` wraps it: `throw new HarmonyException("Patching exception in method " + original.FullDescription(), exception);`. The useful text is therefore in the **inner** exception, so the helper logs `ex.ToString()` (which includes the inner exception).

Version caveat: `Main/TAOM.csproj:110` references Harmony with `IncludeAssets="compile"`, so in game the runtime Harmony is whatever the Bannerlord.Harmony module ships, not necessarily 2.4.2. The facts above are proven for the compile and test version. The helper does not depend on them: it catches any exception, and a runtime Harmony that already isolated categories would simply never reach the catch.

Engine v1.5.3, `pwsh tools/taom-src.ps1 path TaleWorlds.MountAndBlade.Module` then `TaleWorlds.MountAndBlade.Module.cs:201-223`:

```csharp
private void InitializeSubModuleBases()
{
    ...
        try
        {
            subModuleBasis.Value.OnSubModuleLoad();
        }
        catch (Exception ex)
        {
            ...
            MBDebug.Print(text2);
            TaleWorlds.Library.Debug.SetCrashReportCustomString(text2);
            throw new Exception();
        }
```

`TaleWorlds.MountAndBlade.MBGameManager.cs:110-115` loops the submodules with no catch:

```csharp
public override void OnGameInitializationFinished(Game game)
{
    foreach (MBSubModuleBase item in Module.CurrentModule.CollectSubModules())
    {
        item.OnGameInitializationFinished(game);
    }
```

### The 84 call sites at `b2e387db`

Counted with `git show b2e387db:Main/SubModule.cs | grep -n "_harmony\.PatchCategory("`: 84 live lines plus 2 commented-out lines (1753, 1760) in the parked NavalTravel block.

**Bare (64), each becomes a plain `TryPatchCategory("...")` by the Step 4 script and needs nothing else:**

- `OnSubModuleLoad` (13): 223 `Patch41_McmLayoutFix`, 259 `Patch25_LocalizationOverride`, 291 `Patch18_CulturalFeats`, 292 `Patch19_CustomBattles`, 302 `Patch58_SkipCampaignIntro`, 487 `Patch0_BattleScenes`, 528 `Patch21_ShaderPrecompilation`, 532 `Patch22_ArmyTargeting`, 538 `Patch49_ArmyGatheringNreGuard`, 543 `Patch59_CaravanTrade`, 549 `Patch81_MarriageAlignment`, 565 `Patch30_MixedFormations`, 624 `Patch42_CastleRecruitment`.
- `OnGameInitializationFinished` (50): lines 1510 to 1745, from `Patch6_BannerEditor` to `Patch53_PartyIconScale` (for example 1596 `Patch65_LandlessCultureSpawnGuard`, 1612 `Patch82_MapEventObserverInvariant`, 1620 `Patch84_SiegeAftermathMenuGuard`).
- `OnMissionBehaviorInitialize` (1): 1922 `Patch_MissionTime_SetMovementOrder`.

**Hand-guarded (20), each needs the Step 5 action in this table:**

| Line | Category | Try block contains | Step 5 action |
|---|---|---|---|
| 199 | `Patch37_CrashReport` | apply, then `AppDomainExceptionHook.Subscribe()`, then `Native2ManagedPatcher.AttachAll` | **A**: keep try/catch; wrap the two follow-up calls in `if (TryPatchCategory(...)) { ... }` |
| 320 | `Patch83_StaleCharacterRepair` | `Initialize(IoC.Resolve...)`, apply | **K**: keep as is |
| 362 | `Patch61_SaveLoadDiagnostics` | 15 `Initialize` calls, apply, then the loop below | **K**: keep outer try/catch |
| 372 | loop over three `Patch61_SaveLoadDiagnostics_*` | apply only | **C**: collapse inner try/catch |
| 397 | `Patch89_MapLoadDiagnostics` | 2 `Initialize` calls, apply, then the loop below | **K**: keep outer try/catch |
| 410 | loop over three `Patch89_MapLoadDiagnostics_*` | apply only | **C**: collapse inner try/catch |
| 433 | `Patch90_PreloadBodyGuard` | `Initialize`, apply | **K** |
| 452 | `Patch62_MovieReleaseAvGuard` | `Initialize`, apply | **K** |
| 475 | `Patch79_TooltipDiagnostics` | 2 `Initialize`, apply | **K** |
| 559 | `Patch68_EconomyDiagnostics` | apply only | **C** |
| 575 | `Patch63_BannerBearerSpawnGuard` | `Initialize`, apply | **K** |
| 646 | `Patch55_BasicTableauRaceGuard` | apply only | **C** |
| 1493 | loop over nine preview categories | apply, then `TableauDiagnostics.LogAlways("... applied OK.")`; catch logs `TableauDiagnostics.LogError` | **P**: replace try/catch with if/else (below) |
| 1521 | `Patch77_PlayerSwitcher` | 2 `Initialize`, apply; catch calls `DisableForSession` | **D**: keep try/catch; add `if (!TryPatchCategory(...))` calling `DisableForSession` |
| 1537 | `Patch78_PlayerSwitcher_CareerFastPath` | `Initialize`, apply | **K** |
| 1813 | `Patch43_BattleLoadDiagnostics` | apply only | **C** |
| 1826 | `Patch91_MissionTickStall` | apply only | **C** |
| 1850 | `Patch60_TournamentExitMovieRelease` | apply only | **C** |
| 1861 | `Patch69_TournamentRosterGuard` | apply only | **C** |
| 1863 | `Patch69_TournamentEndGuard` | apply only | **C** |

Why A, P and D matter: today a patch throw exits the try, which skips the follow-up calls (A), prints the failure line instead of "applied OK" (P), and disables the Player Switcher for the session (D). `TryPatchCategory` never throws, so without these edits the follow-ups would run on a failed patch, the preview log would claim success, and the switcher would stay enabled with its patch missing.

Excerpts of the three sites (b2e387db):

```csharp
// 194-210
        _harmony = new Harmony("com.taom.mod");
        if ((TAOM.Features.CrashReport.CrashReportSettings.Instance?.EnableCrashCapture) ?? true)
        {
            try
            {
                _harmony.PatchCategory("Patch37_CrashReport");
                IoC.Resolve<TAOM.Features.CrashReport.Hooks.AppDomainExceptionHook>().Subscribe();
                if ((TAOM.Features.CrashReport.CrashReportSettings.Instance?.EnableNativeToManagedCapture) ?? true)
                {
                    IoC.Resolve<TAOM.Features.CrashReport.Hooks.Native2ManagedPatcher>().AttachAll(_harmony);
                }
            }
            catch (System.Exception ex)
            {
                IoC.Resolve<IModLogger>().LogError($"[CrashReport] init failed: {ex.GetType().Name}: {ex.Message}");
            }
        }
```

```csharp
// 1478-1501
        foreach (var previewCategory in new[]
        {
            "Patch1_FirstTimeInit",
            ...
            "Patch72_TableauRacePosition",
        })
        {
            try
            {
                _harmony.PatchCategory(previewCategory);
                Features.HeroRace.Diagnostics.TableauDiagnostics.LogAlways($"PatchCategory '{previewCategory}' applied OK.");
            }
            catch (System.Exception ex)
            {
                Features.HeroRace.Diagnostics.TableauDiagnostics.LogError(
                    $"PatchCategory '{previewCategory}' FAILED — the character preview will fall back to vanilla resolution: {ex}");
            }
        }
```

```csharp
// 1517-1527
        try
        {
            TAOM.Features.PlayerSwitcher.Hooks.Patch77_BodyGeneratorView_Constructor.Initialize(IoC.Resolve<IModLogger>());
            TAOM.Features.PlayerSwitcher.Hooks.Patch77_BodyGeneratorView_OnFinalize.Initialize(IoC.Resolve<IModLogger>());
            _harmony.PatchCategory("Patch77_PlayerSwitcher");
        }
        catch (System.Exception ex)
        {
            IoC.Resolve<TAOM.Features.PlayerSwitcher.IPlayerSwitchPolicyProvider>()
                .DisableForSession($"Patch77 could not be applied: {ex.Message}");
        }
```

An example **C** site (557-564):

```csharp
        try
        {
            _harmony.PatchCategory("Patch68_EconomyDiagnostics");
        }
        catch (System.Exception ex)
        {
            logger.LogWarning($"[EconomyDiagnostics] Patch68 failed to apply: {ex.Message}");
        }
```

The once-per-process flag, which stays exactly as it is (1464-1465): `if (_gameInitPatchesApplied) return;` then `_gameInitPatchesApplied = true;`. The mission flag (1919-1923) also stays:

```csharp
        if (!_missionTimePatchesApplied)
        {
            _missionTimePatchesApplied = true;
            _harmony.PatchCategory("Patch_MissionTime_SetMovementOrder");
        }
```

The false comment to correct (187-193):

```csharp
        // Codex review #46 (2026-05-25) MED-01: attach Patch37_CrashReport IMMEDIATELY
        // after IoC.Configure() so its Finalizers cover the rest of OnSubModuleLoad
        // (UIExtender init, time-acceleration resolve, downstream PatchCategory calls).
        // Previous order left lines 88-107 uncatchable. The only unavoidable blind spot
        // is the IoC.Configure() call itself — if THAT throws, the entire feature is
        // unreachable. Split CrashReport bootstrap doesn't fix this without re-implementing
        // a manual DI container; accept and document the residual.
```

It is false because Patch37's only relevant target is `[HarmonyPatch(typeof(MBSubModuleBase), "OnSubModuleLoad")]` (`Main/Features/CrashReport/Hooks/Patch37_CrashReport.cs:113`): that finalizer patches the base method's body, TAOM's override is a different method, and it is already on the stack when Patch37 attaches. Patch37's other targets are tick methods (`Managed.ApplicationTick`, `Module.OnApplicationTick`, `ScreenManager.Tick`, `Mission.Tick` and others, lines 36-104).

The on-screen message precedent in the same method (626): `InformationManager.DisplayMessage(new InformationMessage("TAOM loaded successfully!", Colors.Green));`.

### Conventions that bind this change

- **ADR-002 (thin entry points, under 150 lines):** entry points delegate to testable classes. `SubModule.cs` is already far over (known, not this plan's job); this plan moves the guard logic into its own class and removes net lines from `SubModule.cs`. Do not add logic to `SubModule` beyond the two one-line helpers in Step 3.
- **ADR-007 (adapters for sealed TaleWorlds types):** the new class takes an `Action<string>` and an `IModLogger`, so it touches neither HarmonyLib nor any TaleWorlds type. The only TaleWorlds call (`InformationManager.DisplayMessage`) stays in `SubModule`.
- **ADR-008 (service testability):** no static TaleWorlds calls in the tested class; it must be constructible in a unit test with an NSubstitute logger.
- **`.claude/rules/csharp-architecture.md`:** constructor injection, no service locator inside the new class; NSubstitute for mocks. **ADR-003/004/005:** no `#region`, no `[Obsolete]`, no `#if DEBUG`.
- **`docs/reviews/lessons/harmony-il.md:172-188`** ("A sequence of unguarded `PatchCategory` calls fails as a group, and the log cannot tell you it did") prescribes exactly this: isolate each category and log its outcome so "a failure must name itself".
- **Logging tag:** use `[PatchApply]` (no existing use in `Main`, so it greps cleanly).
- **Tests model:** `TAOM.Tests/Core/Domain/RaceManagerTests.cs` (NSubstitute `Substitute.For<IModLogger>()`, `_logger.Received().LogError(Arg.Is<string>(s => s.Contains(...)))`). Repo paths in tests: `TAOM.Tests/Infrastructure/RepoPaths.cs` (`RepoPath("Main")`, same namespace as the new test file).
- **Text tests on `SubModule.cs`:** `HeroRaceWiringTests.Patch72_IsAppliedInTheGuardedPreviewBatch` locates the literal `foreach (var previewCategory in new[]` and the array's closing `})`. Keep that loop header and the array exactly as they are. `SharedMovementOrderPostfixTests` needs the strings `Patch_MissionTime_SetMovementOrder` and `_missionTimePatchesApplied` to stay.

### Decisions already taken (do not reopen)

- The helper is named `TryPatchCategory`, so `UncapturableHeroesWiringTests` (which matches the substring `PatchCategory("Patch76_UncapturableHeroes")`) keeps passing unchanged.
- Keep both once-per-process flags. The batch now always completes past patch application, so the flag no longer strands a half-patched process.
- The on-screen notice is **literal English**, like "TAOM loaded successfully!" beside it. It names internal category ids, fires only on a broken install, and a `{=taom_...}` key would need the 12-language `/localize` pipeline, which an executor cannot run (the `UnregisteredLocalizationKeyBaselineTests` and `LanguageFileCoverageTests` ratchets would fail on an unregistered key). Localizing it is a deferred follow-up for the orchestrator.
- Do not change `ManualPatchApplicator.cs`, the `Initialize(...)` calls, or any `IoC.Resolve` outside the listed sites.

## Commands you will need

Run all of them from the worktree root. Never `./build.ps1`. The `grep`, `git` and `python` checks in the steps run in the Bash tool (Git Bash).

| Purpose | Command | Expected on success |
|---|---|---|
| Build | `dotnet build Main/TAOM.csproj -p:DisableModuleCopy=true -p:ModuleId=` | exit 0, `0 Error(s)` |
| Tests | `dotnet test TAOM.Tests -p:DisableModuleCopy=true -p:ModuleId=` | exit may be non-zero only because of the two known Armory tests (below) |
| Filtered tests | `dotnet test TAOM.Tests -p:DisableModuleCopy=true -p:ModuleId= --filter "FullyQualifiedName~PatchCategoryApplierTests"` | all pass (from Step 6 on) |
| Data | `python tools/validate_moduledata.py` | 0 errors (not affected by this plan; run once at the end) |
| Docs | `python tools/lint_docs.py` | exit 0, no dead links |

**Known baseline (measured at `b2e387db` in a clean worktree):** 10,239 tests, 10,235 passed, 2 failed, 2 ignored (not executed). The 2 failures are `ElkConfigTests.TheElkItem_DeclaresTheScaleTheReachIsTunedFor` and `AnimaliaMountWiringTests.AnimaliaActionSets_BindOnlyHorseActions_ToClipsThatExist`; both read the live, unversioned Armory install that another session is editing right now. They are not caused by this or any plan: do not chase them, and do not edit either file. The 2 ignored are deliberate `[Ignore]`s in `WargAttackServiceTests`. Any other failure is yours.

## Scope

**In scope** (the only files you may modify or create, on your worktree branch):

- `Main/PatchCategoryApplier.cs` (new)
- `TAOM.Tests/Infrastructure/PatchCategoryApplierTests.cs` (new)
- `Main/SubModule.cs`: **single-owner file, listed explicitly.** Allowed edits, and only these: the new field, its construction, the two private helpers (Step 3); the call-site replacement (Step 4); the guarded-site actions A, C, D, P and the parked-comment tidy (Step 5); four `ReportPatchFailures` calls (Step 6); the comment at 187-193 (Step 8).
- The nine test files in Step 7 (one expected string each) and the doc comment in `TAOM.Tests/Features/LordSpawnGuard/Patch65LandlessCultureSpawnGuardBindingTests.cs:16-21`.
- `docs/features/crash-report.md` (line 284), `docs/reference/engine/submodule-lifecycle-and-harmony.md` (lines 20 and 30).

**Out of scope** (do NOT touch, even though they look related):

- `Main/IoC.cs`, `Main/TAOM.csproj`, `Directory.Build.props`: single-owner and not needed (the applier is constructed directly in `SubModule`, not registered in IoC; SDK-style globbing picks up the new `.cs` files). If you believe one needs a change, STOP and report the exact line.
- `Main/ManualPatchApplicator.cs` (its `harmony.Patch` calls are a separate, deferred question).
- `CHANGELOG.md` (another session holds uncommitted edits to it; the orchestrator writes the entry at merge).
- Everything under `.claude/`, including `.claude/rules/harmony-patches.md`. The PreToolUse hook `.claude/hooks/check-changelog-changed.sh` denies any commit that stages a `.claude/*` path without `CHANGELOG.md`, and CHANGELOG is out of scope, so the orchestrator makes that rule edit at merge (text in "Maintenance notes").
- `plans/README.md` (the orchestrator maintains the index).
- Any `Initialize(...)` or `IoC.Resolve` call that is not inside one of the 20 guarded blocks.
- `docs/reference/harmony-patch-registry.md` and every other doc not listed above.
- Any file under `Main/Features/**`.

## Git workflow

- Work in a new worktree off `bannerlord-1.5.x`, never in `E:\repos\TAOM` itself (its working tree holds another live session's uncommitted edits, including to `Main/SubModule.cs`):
  `git -C E:/repos/TAOM worktree add E:/repos/wt-plan-009 -b plan/009-guarded-patch-apply bannerlord-1.5.x`
  Then run every command in this plan from `E:/repos/wt-plan-009`. If that command fails because the branch or the directory already exists (for example on a retry), STOP and report; do not delete, reuse or reset either.
- The worktree checks out with CRLF line endings (`core.autocrlf=true`) and `SubModule.cs` starts with a UTF-8 BOM. Use the Edit tool or the byte-level Python script in Step 4; never `sed -i`.
- Commit subject: `<type>(<scope>): v2.0.30 - <description>` (the version is `<Version value="v2.0.30" />` in `Main/_Module/SubModule.xml`; re-read it before committing in case it moved). At most 72 characters, body wrapped at 72, **no AI attribution trailer** (no `Co-Authored-By`).
- Stage explicit paths only (`git add <path> <path> ...`), never `git add -A` or `git commit -a`.
- Suggested commits:
  1. `fix(harmony): v2.0.30 - apply every patch category through one guard` (68 chars): the applier, its tests, `SubModule.cs`, the nine test-string edits, the Patch65 comment. Trailers: `Not-tested: live engine apply path and the red notice (needs the game)` and `Save-compat: no save data touched`.
  2. `docs(harmony): v2.0.30 - correct Patch37 coverage, name the guard` (66 chars): the two docs (`docs/features/crash-report.md`, `docs/reference/engine/submodule-lifecycle-and-harmony.md`).
- Neither commit stages a `.claude/*` path, so the CHANGELOG hook has nothing to deny. If any hook denies a commit anyway, STOP and report its message verbatim; do not edit `CHANGELOG.md` or any other out-of-scope file to satisfy it, and never bypass a hook.
- Never push, never open a PR, never merge. **Merge note for the orchestrator:** the main tree has another session's uncommitted `SubModule.cs` edits (a `MonsterSize` call inserted just above the `_gameInitPatchesApplied` guard near line 1455, and one `AddTaomBehavior` line near 1964). Merging this branch needs a rebase over that work once it is committed; the hunk near 1455 sits next to this plan's context. After the rebase, re-run the gate test: any category the other session added with a bare `_harmony.PatchCategory` will fail it, which is the intended behavior.

## Steps

### Step 0: Set up and record the baseline

Create the worktree (Git workflow above), run the drift check, then:

**Verify**:
- `git show b2e387db:Main/SubModule.cs | grep -c "_harmony\.PatchCategory("` → `86`
- `grep -c "_harmony\.PatchCategory(" Main/SubModule.cs` (in the worktree) → `86`
- Build command → exit 0.
- Tests command → 10,239 total at `b2e387db` (more if `bannerlord-1.5.x` has moved), only the two known Armory tests in "Commands you will need" fail (or none, if the Armory is back in step), 2 ignored. Write down the total test count; you need it in Steps 7 and 9.

### Step 1 (RED): Write the failing tests

Create `TAOM.Tests/Infrastructure/PatchCategoryApplierTests.cs` with exactly this content:

```csharp
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using HarmonyLib;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Core.Logging;

namespace TAOM.Tests.Infrastructure;

/// <summary>
/// Pins <c>PatchCategoryApplier</c>: one category whose target no longer resolves must cost only
/// that category, name itself in the log, and appear in the phase's on-screen summary. Harmony
/// 2.4.2's <c>PatchCategory</c> has no catch, so before this guard a single drifted binding failed
/// the module load (OnSubModuleLoad) or skipped every later category in the game-init batch.
/// </summary>
[TestClass]
public class PatchCategoryApplierTests
{
    internal const string UnresolvableCategory = "Plan009_UnresolvableTargetProbe";

    private static readonly Regex CommentPattern =
        new(@"/\*.*?\*/|//[^\n]*", RegexOptions.Singleline | RegexOptions.Compiled);

    private static readonly Regex DirectPatchCategoryCall =
        new(@"\.PatchCategory\s*\(", RegexOptions.Compiled);

    private IModLogger _logger = null!;
    private List<string> _applied = null!;

    [TestInitialize]
    public void Setup()
    {
        _logger = Substitute.For<IModLogger>();
        _applied = new List<string>();
    }

    private PatchCategoryApplier ApplierFailingOn(params string[] failing)
    {
        return new PatchCategoryApplier(category =>
        {
            if (Array.IndexOf(failing, category) >= 0)
                throw new InvalidOperationException("Undefined target method for patch method " + category + "_Probe");
            _applied.Add(category);
        }, _logger);
    }

    [TestMethod]
    public void TryApply_WhenTheApplySucceeds_ReturnsTrueAndLogsNoError()
    {
        var sut = ApplierFailingOn();

        Assert.IsTrue(sut.TryApply("Patch_A"));
        CollectionAssert.AreEqual(new[] { "Patch_A" }, _applied);
        _logger.DidNotReceive().LogError(Arg.Any<string>());
    }

    [TestMethod]
    public void TryApply_WhenTheApplyThrows_ReturnsFalseAndLogsTheCategoryAndTheCause()
    {
        var sut = ApplierFailingOn("Patch_B");

        Assert.IsFalse(sut.TryApply("Patch_B"));
        _logger.Received(1).LogError(Arg.Is<string>(s =>
            s.Contains("[PatchApply]") && s.Contains("Patch_B") && s.Contains("FAILED")
            && s.Contains("Undefined target method for patch method Patch_B_Probe")));
    }

    // The regression this guard exists for: one bad category used to abort every later one.
    [TestMethod]
    public void TryApply_AfterAFailedCategory_StillAppliesTheNextOne()
    {
        var sut = ApplierFailingOn("Patch_B");

        sut.TryApply("Patch_A");
        sut.TryApply("Patch_B");
        sut.TryApply("Patch_C");

        CollectionAssert.AreEqual(new[] { "Patch_A", "Patch_C" }, _applied);
    }

    [TestMethod]
    public void TakeFailureSummary_WhenNothingFailed_ReturnsNull()
    {
        var sut = ApplierFailingOn();
        sut.TryApply("Patch_A");

        Assert.IsNull(sut.TakeFailureSummary("game initialization"));
    }

    [TestMethod]
    public void TakeFailureSummary_NamesThePhaseAndEveryFailedCategoryInOrder()
    {
        var sut = ApplierFailingOn("Patch_B", "Patch_D");
        foreach (var category in new[] { "Patch_A", "Patch_B", "Patch_C", "Patch_D" })
            sut.TryApply(category);

        Assert.AreEqual(
            "TAOM: patch groups failed to apply during game initialization: Patch_B, Patch_D. "
            + "Those fixes are off this session; the TAOM log names the cause.",
            sut.TakeFailureSummary("game initialization"));
    }

    [TestMethod]
    public void TakeFailureSummary_ClearsTheList_SoTheNextPhaseReportsOnlyItsOwnFailures()
    {
        var sut = ApplierFailingOn("Patch_B", "Patch_M");

        sut.TryApply("Patch_B");
        Assert.IsNotNull(sut.TakeFailureSummary("module load"));
        Assert.IsNull(sut.TakeFailureSummary("module load"));

        sut.TryApply("Patch_M");
        var second = sut.TakeFailureSummary("mission start")!;
        StringAssert.Contains(second, "Patch_M");
        Assert.IsFalse(second.Contains("Patch_B"), second);
    }

    // Pins the premise against the pinned Harmony 2.4.2: a category whose target does not resolve
    // THROWS out of PatchCategory (it is not a silent no-op), with the cause in the inner exception.
    [TestMethod]
    public void RealHarmony_ACategoryWhoseTargetDoesNotResolve_ThrowsHarmonyException()
    {
        var harmony = new Harmony("taom.tests.plan009.premise");

        var ex = Assert.ThrowsException<HarmonyException>(
            () => harmony.PatchCategory(typeof(PatchCategoryApplierTests).Assembly, UnresolvableCategory));

        StringAssert.Contains(ex.InnerException?.Message ?? string.Empty, "Undefined target method");
    }

    [TestMethod]
    public void TryApply_RealHarmonyCategoryWhoseTargetDoesNotResolve_ReturnsFalseAndLogsTheMissingTarget()
    {
        var harmony = new Harmony("taom.tests.plan009.applier");
        var sut = new PatchCategoryApplier(
            category => harmony.PatchCategory(typeof(PatchCategoryApplierTests).Assembly, category), _logger);

        Assert.IsFalse(sut.TryApply(UnresolvableCategory));
        _logger.Received(1).LogError(Arg.Is<string>(s =>
            s.Contains(UnresolvableCategory) && s.Contains("Undefined target method")));
    }

    // Source gate: the only direct Harmony PatchCategory call left in Main is the one inside the
    // applier's delegate in SubModule. A new bare call would re-open the fail-as-a-group hole.
    [TestMethod]
    public void MainSource_AppliesEveryPatchCategoryThroughTheGuardedHelper()
    {
        var hits = new List<string>();
        foreach (var file in Directory.GetFiles(RepoPaths.RepoPath("Main"), "*.cs", SearchOption.AllDirectories))
        {
            var sep = Path.DirectorySeparatorChar;
            if (file.Contains(sep + "obj" + sep) || file.Contains(sep + "bin" + sep))
                continue;

            var code = CommentPattern.Replace(File.ReadAllText(file), string.Empty);
            foreach (Match match in DirectPatchCategoryCall.Matches(code))
            {
                var lineStart = code.LastIndexOf('\n', match.Index) + 1;
                var lineEnd = code.IndexOf('\n', match.Index);
                if (lineEnd < 0) lineEnd = code.Length;
                hits.Add(Path.GetFileName(file) + ": " + code.Substring(lineStart, lineEnd - lineStart).Trim());
            }
        }

        Assert.AreEqual(1, hits.Count,
            "Every category must be applied with TryPatchCategory(\"...\") in SubModule.cs. Direct calls found: "
            + string.Join(" | ", hits));
        StringAssert.Contains(hits[0], "SubModule.cs: ");
        StringAssert.Contains(hits[0], "PatchCategory(typeof(SubModule).Assembly, category)");
    }
}

/// <summary>
/// A patch class whose target cannot resolve, for the real-Harmony tests above. Nothing outside
/// <c>PatchCategoryApplierTests</c> applies this category.
/// </summary>
[HarmonyPatchCategory(PatchCategoryApplierTests.UnresolvableCategory)]
[HarmonyPatch(typeof(PatchCategoryApplierTests), "NoSuchMethod_Plan009")]
internal static class Plan009UnresolvableTargetProbe
{
    internal static void Postfix() { }
}
```

**Verify**: filtered tests command → build fails with `error CS0246` naming `PatchCategoryApplier` (the class does not exist yet). That is the RED state.

### Step 2 (GREEN): Create the applier

Create `Main/PatchCategoryApplier.cs`:

```csharp
using System;
using System.Collections.Generic;
using TAOM.Core.Logging;

namespace TAOM;

/// <summary>
/// Applies one Harmony patch category at a time so a category whose target no longer resolves
/// costs only that category. Harmony's PatchCategory has no catch: the first class whose target is
/// missing throws a HarmonyException out of the caller, which in OnSubModuleLoad fails the module
/// load and in OnGameInitializationFinished skips every later category of the batch. Each failure
/// is logged at Error with its full cause and remembered until the phase summary is taken. The
/// apply delegate keeps HarmonyLib and the engine out of this class, so it is unit-testable.
/// </summary>
internal sealed class PatchCategoryApplier
{
    private readonly Action<string> _apply;
    private readonly IModLogger _logger;
    private readonly List<string> _failed = new();

    internal PatchCategoryApplier(Action<string> apply, IModLogger logger)
    {
        _apply = apply ?? throw new ArgumentNullException(nameof(apply));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>Applies the category; on a throw, logs it, records it and returns false.</summary>
    internal bool TryApply(string category)
    {
        try
        {
            _apply(category);
            return true;
        }
        catch (Exception ex)
        {
            _failed.Add(category);
            _logger.LogError(
                $"[PatchApply] {category} FAILED (Harmony stops a category at its first failing class): {ex}");
            return false;
        }
    }

    /// <summary>
    /// One player-facing line naming every category that failed since the last call, or null when
    /// none did. Clears the list, so each phase reports only its own failures.
    /// </summary>
    internal string? TakeFailureSummary(string phase)
    {
        if (_failed.Count == 0) return null;

        var summary = $"TAOM: patch groups failed to apply during {phase}: {string.Join(", ", _failed)}. "
            + "Those fixes are off this session; the TAOM log names the cause.";
        _failed.Clear();
        return summary;
    }
}
```

**Verify**: filtered tests command → 8 passed, 1 failed: `MainSource_AppliesEveryPatchCategoryThroughTheGuardedHelper`, with a message listing 84 direct calls in `SubModule.cs` (the gate stays red until Step 4). If either `RealHarmony_...` or `TryApply_RealHarmony...` fails, see the STOP conditions; if you then take the fallback there, the expected result here becomes 6 passed, 1 failed.

**Test counts on the fallback path:** this plan adds 9 tests. If you took the real-Harmony fallback, it adds 7: read every later "9" (Step 4, Step 7, Step 9, Done criteria) as 7.

### Step 3: Wire the helper into SubModule

In `Main/SubModule.cs`:

1. Add a field next to `private Harmony _harmony;` (line 102):
   ```csharp
       private PatchCategoryApplier _patches;
   ```
2. Directly after `_harmony = new Harmony("com.taom.mod");` (line 194) and before the `if ((TAOM.Features.CrashReport.CrashReportSettings.Instance?.EnableCrashCapture) ?? true)` line, insert:
   ```csharp
           // Every category goes through TryPatchCategory, so one binding that no longer resolves
           // costs one category instead of the module load or the rest of a batch. The explicit
           // assembly matters: the one-argument Harmony.PatchCategory(string) picks its assembly
           // from the caller's stack frame.
           _patches = new PatchCategoryApplier(
               category => _harmony.PatchCategory(typeof(SubModule).Assembly, category),
               IoC.Resolve<IModLogger>());
   ```
3. Directly before `private static void StampSaveLoadPhase(` (line 857), add:
   ```csharp
       private bool TryPatchCategory(string category) => _patches.TryApply(category);

       // One red line per phase naming every category that failed, so a dead crash guard is never
       // silent. The notice itself must never break the phase, hence the catch.
       private void ReportPatchFailures(string phase)
       {
           var summary = _patches.TakeFailureSummary(phase);
           if (summary == null) return;
           try
           {
               InformationManager.DisplayMessage(new InformationMessage(summary, Colors.Red));
           }
           catch (System.Exception ex)
           {
               IoC.Resolve<IModLogger>().LogError($"[PatchApply] failure notice not shown: {ex.Message}");
           }
       }
   ```
   Do not write the text `TryPatchCategory(` (with the parenthesis) in any comment you add; the Done criteria count it.

**Verify**: build command → exit 0, 0 errors. `grep -c "_harmony\.PatchCategory(" Main/SubModule.cs` → `87` (86 old plus the delegate).

### Step 4: Replace every direct call (mechanical, byte-level)

Write this script with the Write tool into your session scratchpad (not a heredoc), as `replace_patchcategory.py`, then run `python <scratchpad>/replace_patchcategory.py` from the worktree root:

```python
import pathlib
import re
import sys

path = pathlib.Path("Main/SubModule.cs")
data = path.read_bytes()  # bytes: keeps the BOM and CRLF endings untouched
pattern = re.compile(rb"_harmony\.PatchCategory\((?!typeof)")
count = len(pattern.findall(data))
print("replacements:", count)
if count != 86:
    sys.exit("expected 86 (84 live call lines + 2 commented parked lines); nothing written")
path.write_bytes(pattern.sub(b"TryPatchCategory(", data))
```

**Verify**:
- The script prints `replacements: 86` and exits 0.
- `grep -c "_harmony\.PatchCategory(" Main/SubModule.cs` → `1` (the delegate).
- `grep -c "TryPatchCategory(" Main/SubModule.cs` → `87` (86 replaced + the helper definition).
- Build command → exit 0, 0 errors.
- Filtered tests command → 9 passed, 0 failed (7 on the fallback path; the gate is green now).

### Step 5: Rework the 20 hand-guarded sites

Use the table in "Current state". Find each site by its category string.

- **K** (Patch83, Patch61 outer, Patch89 outer, Patch90, Patch62, Patch79, Patch63, Patch78): leave as the script left them. The try/catch now guards the `Initialize` and `IoC.Resolve` calls; the apply is guarded by the helper.
- **C** (collapse, 9 blocks): replace the whole `try { ... } catch (...) { ... }` with the single statement. The results must read:
  - Patch61 loop body: `TryPatchCategory(category);` (inside the existing `foreach`, braces kept).
  - Patch89 loop body: `TryPatchCategory(traceCategory);` (inside the existing `foreach`, braces kept).
  - `TryPatchCategory("Patch68_EconomyDiagnostics");`
  - `TryPatchCategory("Patch55_BasicTableauRaceGuard");` (inside the existing `if (!_basicTableauGuardApplied)` block, after `_basicTableauGuardApplied = true;`)
  - `TryPatchCategory("Patch43_BattleLoadDiagnostics");`
  - `TryPatchCategory("Patch91_MissionTickStall");`
  - `TryPatchCategory("Patch60_TournamentExitMovieRelease");`
  - `TryPatchCategory("Patch69_TournamentRosterGuard");` and `TryPatchCategory("Patch69_TournamentEndGuard");`
  Keep the comments above each block unchanged, and keep the local variables (`saveLoadLogger`, `mapLoadLogger`, `logger`, `tournamentExitLogger`): each is still used by an `Initialize` call.
- **A** (Patch37), inside the existing try:
  ```csharp
                  if (TryPatchCategory("Patch37_CrashReport"))
                  {
                      IoC.Resolve<TAOM.Features.CrashReport.Hooks.AppDomainExceptionHook>().Subscribe();
                      if ((TAOM.Features.CrashReport.CrashReportSettings.Instance?.EnableNativeToManagedCapture) ?? true)
                      {
                          IoC.Resolve<TAOM.Features.CrashReport.Hooks.Native2ManagedPatcher>().AttachAll(_harmony);
                      }
                  }
  ```
  The catch stays as it is.
- **P** (preview loop): keep `foreach (var previewCategory in new[]` and the nine-entry array byte for byte; replace only the loop body's try/catch with:
  ```csharp
              if (TryPatchCategory(previewCategory))
                  Features.HeroRace.Diagnostics.TableauDiagnostics.LogAlways($"PatchCategory '{previewCategory}' applied OK.");
              else
                  Features.HeroRace.Diagnostics.TableauDiagnostics.LogError(
                      $"PatchCategory '{previewCategory}' FAILED, the character preview will fall back to vanilla resolution; the [PatchApply] line in the TAOM log has the cause.");
  ```
- **D** (Patch77), inside the existing try, replace the `TryPatchCategory("Patch77_PlayerSwitcher");` statement with:
  ```csharp
              if (!TryPatchCategory("Patch77_PlayerSwitcher"))
              {
                  IoC.Resolve<TAOM.Features.PlayerSwitcher.IPlayerSwitchPolicyProvider>()
                      .DisableForSession("Patch77 could not be applied; the [PatchApply] line in the TAOM log has the cause");
              }
  ```
  The catch (`DisableForSession($"Patch77 could not be applied: {ex.Message}")`) stays for `Initialize` failures.
- **Parked NavalTravel comment** (around old line 1760): the script turned `// try { _harmony.PatchCategory("Patch57_NavalAtSeaLandRescueGuard"); }` into `// try { TryPatchCategory(...); }`. Replace that line and the `// catch (System.Exception ex) { navalRescueLogger.LogWarning(...); }` line after it with the single comment line `// TryPatchCategory("Patch57_NavalAtSeaLandRescueGuard");`. Leave the `Patch54` comment line (`// TryPatchCategory("Patch54_NavalTravelBoatVisual");`) as the script left it.

**Verify**:
- Build command → exit 0, 0 errors.
- `grep -c "TryPatchCategory(" Main/SubModule.cs` → `87`.
- `grep -c "Patch68 failed to apply\|Patch43 diagnostics failed to apply\|Patch91 probes failed to apply\|Patch60 tournament-exit movie release failed\|Patch69 tournament roster guard failed\|Patch69 tournament end guard failed\|Patch55_BasicTableauRaceGuard apply failed\|not applied (engine drift?)\|did not attach" Main/SubModule.cs` → `0` (the collapsed catch messages are gone).
- `grep -c "try { TryPatchCategory" Main/SubModule.cs` → `0`.
- `grep -c "DisableForSession" Main/SubModule.cs` → `2`.
- `grep -cF 'if (TryPatchCategory("Patch37_CrashReport"))' Main/SubModule.cs` → `1`.
- `grep -cF 'if (TryPatchCategory(previewCategory))' Main/SubModule.cs` → `1`.
- `grep -cF 'if (!TryPatchCategory("Patch77_PlayerSwitcher"))' Main/SubModule.cs` → `1`.
- `grep -cF 'foreach (var previewCategory in new[]' Main/SubModule.cs` → `1`.
- `grep -c "applied OK" Main/SubModule.cs` → `1`.

### Step 6: Report failures once per phase

In `Main/SubModule.cs` add exactly four calls:

1. `OnSubModuleLoad`: on the line directly before `InformationManager.DisplayMessage(new InformationMessage("TAOM loaded successfully!", Colors.Green));`, add `ReportPatchFailures("module load");`.
2. `OnBeforeInitialModuleScreenSetAsRoot`: directly after `TryPatchCategory("Patch55_BasicTableauRaceGuard");` inside the `if (!_basicTableauGuardApplied)` block, add `ReportPatchFailures("main menu setup");`.
3. `OnGameInitializationFinished`: directly after `TryPatchCategory("Patch69_TournamentEndGuard");` and before the comment `// Manual patches for PRIVATE engine methods`, add `ReportPatchFailures("game initialization");`.
4. `OnMissionBehaviorInitialize`: inside `if (!_missionTimePatchesApplied)`, directly after `TryPatchCategory("Patch_MissionTime_SetMovementOrder");`, add `ReportPatchFailures("mission start");`.

**Verify**: build command → exit 0. `grep -c "ReportPatchFailures(" Main/SubModule.cs` → `5` (definition plus four calls).

### Step 7: Update the text tests that spell the old call

Each of these asserts the literal old spelling in `SubModule.cs`. Change only the expected string, replacing `_harmony.PatchCategory(` or `.PatchCategory(` with `TryPatchCategory(`; leave messages and everything else alone:

| File:line (b2e387db) | Old expected string | New expected string |
|---|---|---|
| `TAOM.Tests/Features/BanditManagement/Patch86HideoutBossFightBindingTests.cs:238` | `"_harmony.PatchCategory(\"Patch86_HideoutBossFight\")"` | `"TryPatchCategory(\"Patch86_HideoutBossFight\")"` |
| `TAOM.Tests/Features/Diplomacy/Patch80KingdomVoteDeadlockBindingTests.cs:295` | `"_harmony.PatchCategory(\"" + Category + "\")"` | `"TryPatchCategory(\"" + Category + "\")"` |
| `TAOM.Tests/Features/Enlistment/Patch85EnlistedDetachDeferralBindingTests.cs:132` | `"_harmony.PatchCategory(\"Patch85_EnlistedDetachDeferral\")"` | `"TryPatchCategory(\"Patch85_EnlistedDetachDeferral\")"` |
| `TAOM.Tests/Features/FieldCamp/FieldCampWiringTests.cs:191` | `".PatchCategory(\"Patch74_FieldCampNameplateIcon\")"` | `"TryPatchCategory(\"Patch74_FieldCampNameplateIcon\")"` |
| `TAOM.Tests/Features/LordPartyTemplates/Patch88LordPartyTemplateTests.cs:163` | `"_harmony.PatchCategory(\"" + Category + "\")"` | `"TryPatchCategory(\"" + Category + "\")"` |
| `TAOM.Tests/Features/MapEventGuard/Patch82MapEventObserverInvariantBindingTests.cs:139` | `"_harmony.PatchCategory(\"Patch82_MapEventObserverInvariant\")"` | `"TryPatchCategory(\"Patch82_MapEventObserverInvariant\")"` |
| `TAOM.Tests/Features/MapEventGuard/Patch84SiegeAftermathMenuGuardTests.cs:205` | `"_harmony.PatchCategory(\"Patch84_SiegeAftermathMenuGuard\")"` | `"TryPatchCategory(\"Patch84_SiegeAftermathMenuGuard\")"` |
| `TAOM.Tests/Features/Refuge/RefugeWiringTests.cs:62` | `".PatchCategory(\"Patch75_Refuge\")"` | `"TryPatchCategory(\"Patch75_Refuge\")"` |
| `TAOM.Tests/Features/ReturnToArmy/Patch87ReturnToArmyTests.cs:256` | `"_harmony.PatchCategory(\"Patch87_ReturnToArmy\")"` | `"TryPatchCategory(\"Patch87_ReturnToArmy\")"` |

`UncapturableHeroesWiringTests.cs:92` (`"PatchCategory(\"Patch76_UncapturableHeroes\")"`) still matches as a substring: do not edit it.

Also replace the now-false doc comment in `TAOM.Tests/Features/LordSpawnGuard/Patch65LandlessCultureSpawnGuardBindingTests.cs`, lines 16-21, all six lines: line 16 begins `///    rethrows it as a <c>HarmonyException</c>. Since <c>SubModule</c>'s` and line 21 is `///    that cannot happen.)`. Line 22 (`///  - The finalizer declares ...`) stays. Replace the six lines with these four:

```csharp
///    rethrows it as a <c>HarmonyException</c>. <c>SubModule</c> applies the category through
///    <c>TryPatchCategory</c>, which logs that as a <c>[PatchApply]</c> error, shows a red notice
///    and skips only this category, so the landless-culture guard would be off for the session.
///    This test exists to turn that into a red build instead.
```

**Verify**: `grep -rn "_harmony\.PatchCategory(\|\"\.PatchCategory(" TAOM.Tests --include=*.cs` → no output. `grep -c "that cannot happen" TAOM.Tests/Features/LordSpawnGuard/Patch65LandlessCultureSpawnGuardBindingTests.cs` → `0`. Then the full tests command → only the two known Armory tests may fail; total = Step 0 total + 9; the 9 `PatchCategoryApplierTests` pass (7 and +7 on the fallback path); `HeroRaceWiringTests`, `SharedMovementOrderPostfixTests` and `UncapturableHeroesWiringTests` pass.

### Step 8: Correct the false comment and the docs

1. `Main/SubModule.cs`, the comment block at old lines 187-193 (starts `// Codex review #46 (2026-05-25) MED-01`), replace all seven lines with:
   ```csharp
           // Codex review #46 (2026-05-25) MED-01: attach Patch37_CrashReport first so its tick
           // finalizers (Module.OnApplicationTick, ScreenManager.Tick, Mission.Tick and the rest) are
           // live as early as possible. It does NOT cover this method's own body: its
           // MBSubModuleBase.OnSubModuleLoad finalizer patches the base method, and this override is
           // already on the stack when it attaches. A throw from here reaches the engine's
           // Module.InitializeSubModuleBases catch, which logs and rethrows, and the game does not
           // start; that is why every category below goes through TryPatchCategory.
   ```
2. `docs/features/crash-report.md:284`, the whole line that begins `- **`MBSubModuleBase.OnSubModuleLoad` chicken-and-egg.**`, replace it with exactly the one line inside this fence (the fence itself is not part of the text):
   ````markdown
   - **`MBSubModuleBase.OnSubModuleLoad` chicken-and-egg.** TAOM's own `OnSubModuleLoad` is what registers Patch37, and Patch37 cannot catch a throw from that method: its `MBSubModuleBase.OnSubModuleLoad` finalizer patches the base method's body, and TAOM's override is already running when it attaches. Throws in `OnSubModuleLoad` of mods that load BEFORE TAOM are not catchable by us either; those land in vanilla / BUTR. TAOM applies its own categories through `TryPatchCategory`, so a binding that no longer resolves logs a `[PatchApply]` error and skips one category instead of failing the load. Patch37 is still registered first so its tick finalizers are live as early as possible.
   ````
3. `docs/reference/engine/submodule-lifecycle-and-harmony.md`:
   - line 20: replace `apply most patch categories via **`_harmony.PatchCategory("PatchNN_X")`** (:133-242)` with `apply most patch categories via **`TryPatchCategory("PatchNN_X")`** (:133-242), the guarded helper over `_harmony.PatchCategory(assembly, category)`: a failure logs `[PatchApply]` and skips only that category`.
   - line 30: replace `**`_harmony.PatchCategory("PatchNN_X")`** applies all patches in that group.` with `**`TryPatchCategory("PatchNN_X")`** (in `SubModule`, through `PatchCategoryApplier`) applies all patches in that group. Harmony stops a category at its first class whose target does not resolve and throws; the helper contains that to the one category.`
Do not edit `.claude/rules/harmony-patches.md`; the orchestrator does (see "Scope" and "Maintenance notes").

No em or en dashes in any prose you add (commas, colons, parentheses instead).

**Verify**: `python tools/lint_docs.py` → exit 0, 0 dead links (report-only warnings, such as the size warning for path-scoped rules, do not count). `grep -c "PatchApply" docs/features/crash-report.md` → `1`. `grep -c "TryPatchCategory" docs/reference/engine/submodule-lifecycle-and-harmony.md` → `2`. Build command → exit 0.

### Step 9: Final verification and commit

**Verify**:
- Build command → exit 0, 0 errors.
- Tests command → only `TheElkItem_DeclaresTheScaleTheReachIsTunedFor` and `AnimaliaActionSets_BindOnlyHorseActions_ToClipsThatExist` may fail; total = Step 0 total + 9.
- `python tools/validate_moduledata.py` → 0 errors.
- `git status --porcelain` → only the in-scope paths listed under "Scope". Build and test output (`bin/`, `obj/`, `TestResults/`) is gitignored at `b2e387db`, so it does not appear; if any other path appears, STOP and report it rather than deleting it.

Then commit as described in "Git workflow" (two commits, explicit paths). Do not push. If a hook denies either commit, STOP (see "STOP conditions").

## Test plan

New tests in `TAOM.Tests/Infrastructure/PatchCategoryApplierTests.cs` (9, or 7 on the real-Harmony fallback path), modelled on `TAOM.Tests/Core/Domain/RaceManagerTests.cs`:

| Input | Branch | Test |
|---|---|---|
| apply succeeds | try path | `TryApply_WhenTheApplySucceeds_ReturnsTrueAndLogsNoError` |
| apply throws | catch path: returns false, logs `[PatchApply]`, category, `FAILED`, inner cause | `TryApply_WhenTheApplyThrows_ReturnsFalseAndLogsTheCategoryAndTheCause` |
| A ok, B throws, C ok | isolation (the regression) | `TryApply_AfterAFailedCategory_StillAppliesTheNextOne` |
| no failures | summary null | `TakeFailureSummary_WhenNothingFailed_ReturnsNull` |
| two failures of four | exact summary text, order kept | `TakeFailureSummary_NamesThePhaseAndEveryFailedCategoryInOrder` |
| failure, take, take, new failure, take | list cleared per phase | `TakeFailureSummary_ClearsTheList_SoTheNextPhaseReportsOnlyItsOwnFailures` |
| real Harmony, unresolvable target | premise: throws `HarmonyException` with inner "Undefined target method" | `RealHarmony_ACategoryWhoseTargetDoesNotResolve_ThrowsHarmonyException` |
| real Harmony through the applier | returns false, log names the missing target | `TryApply_RealHarmonyCategoryWhoseTargetDoesNotResolve_ReturnsFalseAndLogsTheMissingTarget` |
| `Main/**/*.cs` source | exactly one direct `.PatchCategory(` call, the delegate in `SubModule.cs` | `MainSource_AppliesEveryPatchCategoryThroughTheGuardedHelper` |

Structurally untestable here (name it in the commit's `Not-tested:` trailer): the live engine apply path inside `SubModule` (its hooks need a running game), the red `InformationMessage`, and the A, D and P site rewrites (their shape is pinned by the Step 5 greps and read by the reviewer).

## Done criteria

ALL must hold, run from the worktree root:

- [ ] `dotnet build Main/TAOM.csproj -p:DisableModuleCopy=true -p:ModuleId=` exits 0 with 0 errors
- [ ] `dotnet test TAOM.Tests -p:DisableModuleCopy=true -p:ModuleId=`: the 9 `PatchCategoryApplierTests` pass; the only failures are the two named Armory tests; total is the Step 0 total + 9 (7 and +7 on the fallback path)
- [ ] `dotnet test TAOM.Tests -p:DisableModuleCopy=true -p:ModuleId= --filter "FullyQualifiedName~PatchCategoryApplierTests"` → 0 failed
- [ ] `grep -c "_harmony\.PatchCategory(" Main/SubModule.cs` → `1`
- [ ] `grep -c "TryPatchCategory(" Main/SubModule.cs` → `87`
- [ ] `grep -c "ReportPatchFailures(" Main/SubModule.cs` → `5`
- [ ] `grep -c "DisableForSession" Main/SubModule.cs` → `2`
- [ ] `grep -c "_gameInitPatchesApplied = true" Main/SubModule.cs` → `1` and `grep -c "_missionTimePatchesApplied = true" Main/SubModule.cs` → `1`
- [ ] `grep -rn "_harmony\.PatchCategory(\|\"\.PatchCategory(" TAOM.Tests --include=*.cs` → no output
- [ ] `grep -c "Finalizers cover the rest of OnSubModuleLoad" Main/SubModule.cs` → `0`
- [ ] `python tools/lint_docs.py` exits 0
- [ ] `grep -c "that cannot happen" TAOM.Tests/Features/LordSpawnGuard/Patch65LandlessCultureSpawnGuardBindingTests.cs` → `0`
- [ ] `git status --porcelain` prints nothing after the two commits; `git diff --name-only b2e387db..HEAD` lists only in-scope paths (no `.claude/`, `CHANGELOG.md` or `plans/` path); `git log --format=%B -2` shows no `Co-Authored-By`

## STOP conditions

Stop and report back (do not improvise) if:

- The drift check prints anything, or `grep -c "_harmony\.PatchCategory(" Main/SubModule.cs` in the fresh worktree is not `86` (someone added or removed a category since `b2e387db`).
- The Step 4 script prints a count other than 86 (it writes nothing in that case).
- Any of the 20 guarded blocks does not contain what the "Current state" table says (for example a K site with no `Initialize` call, or a C site with more than the apply).
- `git worktree add` fails because `plan/009-guarded-patch-apply` or `E:/repos/wt-plan-009` already exists.
- Either real-Harmony test fails for any reason other than a missing `PatchCategoryApplier` type. The likeliest cause: Harmony's `BuildCategoryCache` reads custom attributes (`GetCustomAttributes(true)`) off every type in the `TAOM.Tests` assembly, and a type that references an assembly the test host cannot load (for example `TaleWorlds.MountAndBlade.View`, see the comment at `TAOM.Tests/Features/EconomyDiagnostics/EconomyDiagnosticsPatchDiscoveryTests.cs:52-57`) throws `FileNotFoundException`, `TypeLoadException` or `ReflectionTypeLoadException` from `BuildCategoryCache` instead of the expected `HarmonyException`. Fallback you may take without asking, for that or any other cause: delete those two tests and the `Plan009UnresolvableTargetProbe` class (and the now-unused `using HarmonyLib;` and `UnresolvableCategory` constant), keep the other seven, continue with the 7-test counts, and put the exact failure output in your report.
- The test build reports an error (not a warning) on `Plan009UnresolvableTargetProbe`, for example from a Harmony analyzer.
- `HeroRaceWiringTests`, `SharedMovementOrderPostfixTests`, `UncapturableHeroesWiringTests`, or any test other than the nine in Step 7 fails after Step 5 (a text test pinned something this plan did not list).
- The fix seems to need `Main/IoC.cs`, `Main/TAOM.csproj`, `Directory.Build.props`, `Main/ManualPatchApplicator.cs` or any file under `Main/Features/**`.
- `Lib.Harmony` is no longer `2.4.2` in `Dependencies/TAOM.Dependencies.csproj`, or `Harmony.PatchCategory(Assembly, string)` does not compile.
- Any hook denies a `git commit` (for example `check-changelog-changed.sh` asking for a CHANGELOG entry, or the commit-subject version check). Report the deny message verbatim. Do not edit `CHANGELOG.md`, any `.claude/` file or anything else out of scope to satisfy it, and do not try to bypass it.
- A step's verification fails twice after a reasonable fix attempt.

## Maintenance notes

- **What changes for the next patch author:** a new category is applied with `TryPatchCategory("PatchNN_X")`. The source gate fails the build on any direct `.PatchCategory(` call in `Main`.
- **Orchestrator follow-up at merge (not the executor's):** in `.claude/rules/harmony-patches.md:61`, after the words ``(`[HarmonyPatchCategory]` + the SubModule apply batch`` insert ``, applied with `TryPatchCategory("...")`, never a bare `_harmony.PatchCategory` (gate: `PatchCategoryApplierTests`)``, and stage it together with the CHANGELOG entry (the `check-changelog-changed.sh` hook requires both in one commit).
- **Runtime Harmony version:** tests prove the no-catch behavior only for the compile and test `Lib.Harmony 2.4.2`; in game the Bannerlord.Harmony module supplies Harmony (`IncludeAssets="compile"` at `Main/TAOM.csproj:110`). The helper is correct either way.
- **What a reviewer should probe:** (1) site A: `Subscribe`/`AttachAll` must still be skipped when Patch37 fails; (2) site D: a Patch77 failure must still disable the Player Switcher; (3) site P: the preview log must print FAILED, never "applied OK", on a failure, and the array must be unchanged; (4) the delegate uses `typeof(SubModule).Assembly`; (5) the behavior change at Patch61 and Patch89: a failed main category no longer skips its three sub-categories (deliberate: per-category isolation is the point); (6) the K sites' catch messages now fire only for `Initialize` failures, while patch failures log `[PatchApply]`.
- **Residual gaps, deferred on purpose:**
  - Bare `Initialize(...)` and `IoC.Resolve` calls in the game-init batch (for example the Patch80 seams at old lines 1555-1565) can still throw and abort the batch after the flag is set. Separate finding; not a `PatchCategory` call.
  - `ManualPatchApplicator.ApplyAll` calls `harmony.Patch(...)` unguarded.
  - A reflection test that every `[HarmonyPatchCategory]` literal in TAOM.dll is applied through the helper (lane finding COMP-03) belongs with the composition-root work.
  - Localizing the red notice (a `{=taom_...}` key through `/localize`), if the maintainer wants it translated.
  - The live-engine smoke: force a failure (for example rename one target string locally, never committed), confirm one `[PatchApply]` Error line, one red notice, and that the rest of the batch still applied. Owed by whoever deploys this.
