# Plan 018: Start the feature-module composition root with one shared source reader, the module contract, an empty module list and one pilot feature

> **Executor instructions**: Follow this plan step by step. Run every
> verification command and confirm the expected result before moving to the
> next step. If anything in the "STOP conditions" section occurs, stop and
> report; do not improvise. Do NOT edit `plans/README.md`: it has no row for
> this plan, and the orchestrator maintains that index.
>
> **Shell**: every `grep`, `git` and `python` verification in this plan is
> written for the **Bash tool** (Git Bash). Run `dotnet` from either shell.
> Never type the literal word pair "git" + "commit" inside a read-only Bash
> command (for example in a grep pattern): a PreToolUse hook matches that text
> and may deny the command.
>
> **Precondition (run first)**: this plan builds on plan 009
> (`plans/009-guarded-patch-category-apply.md`) as it stands at commit
> `4c728dac`, the reviewed tip of branch `improve/009-guarded-patch-category-apply`
> (009's first commit `45bcf80b` plus its review follow-ups `9da9b5b9`,
> `bdf7d515` and convergence fixes `4c728dac`). The worktree is created at
> `4c728dac` (see "Git workflow"). From the worktree root:
> `test -f Main/PatchCategoryApplier.cs && grep -c "ReportPatchFailures(" Main/SubModule.cs && grep -c "private bool TryPatchCategory(string category)" Main/SubModule.cs && grep -c 'ReportPatchFailures("startup", persistent: true);' Main/SubModule.cs`
> Expected: the file exists, then `4`, then `1`, then `1`. (The four are the
> `startup`, `game initialization` and `mission start` calls plus the
> definition; `bdf7d515` removed the module-load call.) Anything else is a
> STOP condition.
>
> **Drift check (run second, from the worktree root)**: first
> `git rev-parse --short=8 HEAD` → `4c728dac`. Then:
> `git diff --stat b2e387db..HEAD -- Main/SubModule.cs Main/IoC.cs Main/Composition Main/Features/WandererAllegiance TAOM.Tests/Infrastructure/RepoPaths.cs TAOM.Tests/Infrastructure/RepoPathsTests.cs TAOM.Tests/Composition docs/features/wanderer-allegiance.md TAOM.Tests/Features/AutoResolveDiagnostics/AutoResolveDiagnosticsWiringTests.cs TAOM.Tests/Features/BanditManagement/Patch86HideoutBossFightBindingTests.cs TAOM.Tests/Features/BannerColorPersistence/BannerTripletOrderingTests.cs TAOM.Tests/Features/BattleLoadDiagnostics/ExitStallDisarmTests.cs TAOM.Tests/Features/CompanionTactics/SharedMovementOrderPostfixTests.cs TAOM.Tests/Features/CoopInterop/ResetForUnloadSweepTests.cs TAOM.Tests/Features/Diplomacy/Patch80KingdomVoteDeadlockBindingTests.cs TAOM.Tests/Features/Enlistment/Patch85EnlistedDetachDeferralBindingTests.cs TAOM.Tests/Features/FiefManagement/FiefHubCampaignBehaviorTests.cs TAOM.Tests/Features/FieldCamp/FieldCampWiringTests.cs TAOM.Tests/Features/HeroRace/HeroRaceWiringTests.cs TAOM.Tests/Features/HeroRace/RacePersistenceBehaviorTests.cs TAOM.Tests/Features/LordPartyTemplates/Patch88LordPartyTemplateTests.cs TAOM.Tests/Features/MapEventGuard/Patch82MapEventObserverInvariantBindingTests.cs TAOM.Tests/Features/MapEventGuard/Patch84SiegeAftermathMenuGuardTests.cs TAOM.Tests/Features/Messengers/MessengerCampaignBehaviorTests.cs TAOM.Tests/Features/MountDespawn/MountDespawnWiringTests.cs TAOM.Tests/Features/Refuge/RefugeWiringTests.cs TAOM.Tests/Features/ReturnToArmy/Patch87ReturnToArmyTests.cs TAOM.Tests/Features/SettlementGuards/SettlementGuardsWiringTests.cs TAOM.Tests/Features/SiegeDismount/SiegeDismountWiringTests.cs TAOM.Tests/Features/SiegePropDiagnostics/SiegePropDiagnosticsWiringTests.cs TAOM.Tests/Features/SignatureStrikes/SignatureStrikesBindingTests.cs TAOM.Tests/Features/UncapturableHeroes/UncapturableHeroesWiringTests.cs TAOM.Tests/Features/WandererAllegiance/WandererAllegianceWiringTests.cs TAOM.Tests/Migration/GameModelOverrideBindingTests.cs`
> Expected: exactly ten files listed, summary `10 files changed, 168 insertions(+), 159 deletions(-)`: `Main/SubModule.cs` plus these nine test files (plan 009's edits): Patch86HideoutBossFightBindingTests, Patch80KingdomVoteDeadlockBindingTests, Patch85EnlistedDetachDeferralBindingTests, FieldCampWiringTests, Patch88LordPartyTemplateTests, Patch82MapEventObserverInvariantBindingTests, Patch84SiegeAftermathMenuGuardTests, RefugeWiringTests, Patch87ReturnToArmyTests. `b2e387db` is where this plan was first cut; `4b5662b2` and `7f02fc8d` between it and 009 touched none of these paths.
>
> Then run the same path list through `git log --oneline b2e387db..HEAD -- <the same paths>`.
> Expected: exactly three commits, all plan 009's: `4c728dac` (`convergence fixes for plan 009`), `bdf7d515` (`review follow-ups for plan 009`) and `45bcf80b` (`apply every patch category through one guard`). 009's `9da9b5b9` (`correct Patch37 coverage, name the guard`) touches none of these paths; it is allowed if it appears. Any other commit is a STOP condition.
>
> Then check that the nine test files changed only in their category strings (one Bash command; the nine paths are the nine test files named above, each under `TAOM.Tests/Features/`):
> `git diff b2e387db..HEAD -- <the nine test files> | grep -E "^[-+] " | grep -vc "PatchCategory("` → `0` (every changed line holds a category string), and
> `git diff b2e387db..HEAD -- <the nine test files> | grep -cE "^\+ .*TryPatchCategory\("` → `9`.
>
> Both results were re-measured at `4c728dac` during the re-cut. On any other result, compare the "Current state" excerpts against the live code; a mismatch is a STOP condition.

## Status

- **Priority**: P3
- **Effort**: L (three commits: S-M mechanical test migration, M new composition types plus 8 kernel lines, S pilot move)
- **Risk**: MED. Both single-owner files are edited, `bannerlord-1.5.x` has moved past this plan's base in both (commit `709649c3`; see the merge note in "Git workflow"), and the runner sits on every lifecycle hook. Parity is proven by the full suite, the pilot by its own tests.
- **Depends on**: `plans/009-guarded-patch-category-apply.md` at `4c728dac` (the runner applies module categories through its `TryPatchCategory` helper, sits before its `ReportPatchFailures` calls, and follows its startup inquiry rule)
- **Category**: tech-debt
- **Planned at**: commit `4c728dac`, 2026-09-24 (re-cut; first cut at `b2e387db`, 2026-09-23)
- **Re-cut**: 2026-09-24, after an executor stopped at the precondition (`ReportPatchFailures(` counted 4, not 5). Plan 009's review follow-ups removed `ReportPatchFailures("module load")` from `OnSubModuleLoad` and replaced `ReportPatchFailures("main menu setup")` with `ReportPatchFailures("startup", persistent: true)`, an inquiry (009 convergence finding C1). Changed: the precondition, the drift check and worktree base, every SubModule anchor and line number, the kernel test's ProcessLoad and MainMenu anchors, `FeatureModuleHooks.ReportFaults` (startup faults are held for one main-menu inquiry instead of a red chat line nothing receives), one new test for that policy, the test totals and the merge note. Design and scope are otherwise unchanged.
- **Issue**: create before implementation lands (orchestrator)

## Why this matters

`Main/SubModule.cs` is 2,157 lines at `4c728dac` (758 in June) and every feature is wired into it and into `Main/IoC.cs` by hand: services, patch categories, hook handshakes, campaign behaviors, game models and mission behaviors. Since the June audit (measured at `b2e387db`), 131 of the 349 commits that touched `Main/Features/` (38%) also had to edit one of those two single-owner files, which is why parallel sessions keep colliding there (while plan 009 was in review, `709649c3` edited both on `bannerlord-1.5.x`). The 26 test files that guard this wiring read the two files as raw text, so they pin its spelling and cannot tell code from a comment: `GameModelOverrideBindingTests` counts `TaomPartyNavigationModel` as registered only because a commented-out line contains `new TaomPartyNavigationModel(`. This plan lays the first three stones of the approved design: one shared, comment-aware source reader for those tests (with the parked model made explicit), the feature-module contract with an empty ordered list and a runner called once at the end of each lifecycle phase (so behaviour is identical and, from then on, the single-owner files only lose lines), and one pilot feature, WandererAllegiance, moved into its own module with its text asserts replaced.

## Current state

All excerpts are from commit `4c728dac`, which already contains every plan 009 edit. Line numbers are for `4c728dac`; find every site by its text, not its number. Under `Main/` and `TAOM.Tests/`, plan 009 changed only `Main/SubModule.cs`, the nine test files named in the drift check, `Patch65LandlessCultureSpawnGuardBindingTests.cs` (not read here) and its two new files (`Main/PatchCategoryApplier.cs`, `TAOM.Tests/Infrastructure/PatchCategoryApplierTests.cs`); `git diff --name-only b2e387db 4c728dac -- Main TAOM.Tests` lists nothing else apart from the Nazgul sound files, `module_sounds.xml` and one JSON config that `4b5662b2` changed. The nine test files each changed one category-string line, so no test-file line number in this plan moved.

### Files and roles

- `Main/SubModule.cs` (2,157 lines): the module entry point (`MBSubModuleBase`). **Single-owner** (CLAUDE.md: "`Main/IoC.cs` and `Main/SubModule.cs` are single-owner: recommend, don't edit"). The orchestrator's dispatch of this plan authorizes the exact edits listed under "Scope", on your worktree branch only. Do not halt on that CLAUDE.md line; do not make any other edit there.
- `Main/IoC.cs` (251 lines): the DryIoc composition root, `public static class IoC` with `Configure()`, `Resolve<T>()`, `ResolveAll<T>()`, `Dispose()`. **Single-owner**, same authorization and limits as above.
- `TAOM.Tests/Infrastructure/RepoPaths.cs`: the layout-proof repo locator (5 users at `4c728dac`: `EnlistmentDiagnosticsSettingsProviderTests`, `FieldCommissionConfigProviderTests`, `FieldCommissionSettingsProviderTests`, `ShippedFertilityConfigTests`, and plan 009's `PatchCategoryApplierTests`).
- The 26 test files that read `Main/SubModule.cs` or `Main/IoC.cs` as text (table in Step 0.3).
- `Main/PatchCategoryApplier.cs` and `TAOM.Tests/Infrastructure/PatchCategoryApplierTests.cs` (plan 009, read-only here). The test file is a 27th reader of `Main/SubModule.cs`, but it already strips comments with its own `CommentPattern` before every source gate, so it has no comment-shaped hole; it stays out of scope. (`IoCRegistrationDisciplineTests` also matches a naive grep for `IoC.cs"`, but it reads feature `*IoC.cs` files, not `Main/IoC.cs`.)
- `Main/Features/WandererAllegiance/WandererAllegianceIoC.cs` and `Hooks/WandererAllegianceDialogBehavior.cs`: the pilot feature.
- New: `Main/Composition/*.cs` (six files), `TAOM.Tests/Infrastructure/RepoPathsTests.cs`, `TAOM.Tests/Composition/ModuleRunnerTests.cs`, `TAOM.Tests/Composition/FeatureModulesTests.cs`, `Main/Features/WandererAllegiance/WandererAllegianceModule.cs`.

### The shared reader today (`TAOM.Tests/Infrastructure/RepoPaths.cs`, whole file)

```csharp
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;

namespace TAOM.Tests.Infrastructure;

/// <summary>
/// Locates a repo file from THIS source file's compile-time path, so a test that reads shipped
/// source or ModuleData does not depend on the test assembly's output layout (bin/Debug/net472
/// depth) staying what it is today. Import with <c>using static TAOM.Tests.Infrastructure.RepoPaths;</c>.
/// </summary>
public static class RepoPaths
{
    public static string RepoPath(params string[] parts)
    {
        // TWO levels: <repo>/TAOM.Tests/Infrastructure -> TAOM.Tests -> <repo>.
        var repoRoot = Path.GetFullPath(Path.Combine(ThisFile(), "..", ".."));
        return Path.Combine(new[] { repoRoot }.Concat(parts).ToArray());
    }

    private static string ThisFile([CallerFilePath] string path = "") => Path.GetDirectoryName(path)!;
}
```

It is layout-proof only because `TAOM.Tests/TAOM.Tests.csproj` sets no `<PathMap>` (`Main/TAOM.csproj:6` does, for Main only). If `PathMap` ever reaches the test project, `[CallerFilePath]` becomes `/_/...` and every user breaks loudly.

The only comment-stripping reader in the suite, `TAOM.Tests/Features/CoopInterop/CoopVetoClassificationTests.cs:304-312`, which this plan lifts:

```csharp
    private static readonly Regex CommentPattern = new(
        @"/\*.*?\*/|//[^\n]*", RegexOptions.Compiled | RegexOptions.Singleline);

    /// <summary>
    /// Blanks comments while preserving BOTH length and newlines — offsets stay comparable between
    /// the class and prefix scans, and the multiline `^` anchor still sees real line starts.
    /// </summary>
    private static string StripComments(string source) =>
        CommentPattern.Replace(source, m => Regex.Replace(m.Value, "[^\n]", " "));
```

It blanks comments to spaces, so length and line breaks survive and `IndexOf` offsets stay valid. It is not string-literal aware; re-measured at `4c728dac`, neither `Main/SubModule.cs` nor `Main/IoC.cs` has a string literal containing `//` or `/*` (checked with a Python scan of every literal on every line). A simulation of stripping both files against every string literal in the 26 test files found no literal the tests assert on SubModule/IoC text that exists only inside a comment (first cut, at `b2e387db`); the re-cut re-ran the literal-versus-stripped comparison at both commits and got the identical result set, so plan 009's comment rewrites opened no new case and switching all 26 to the stripped view should keep them green.

### The false pass (`TAOM.Tests/Migration/GameModelOverrideBindingTests.cs:46-62`; the method's `[TestMethod]` is at 44 and its closing brace at 69)

```csharp
    public void EveryTaomGameModel_IsRegistered_InSubModule()
    {
        if (!_gameLoaded)
            Assert.Inconclusive("Game assemblies not loaded: " + string.Join("; ", GameAssemblies.Diagnostics));

        var models = DiscoverGameModels();
        if (models.Count < 20)
            Assert.Inconclusive($"Only {models.Count} GameModel subclasses discovered (expected ~37) — assembly-load problem, not a genuine pass.");

        var subModule = ReadRepoFile("Main", "SubModule.cs");
        if (subModule == null)
            Assert.Inconclusive("Main/SubModule.cs not found — run from repo root.");

        var unregistered = models
            .Where(m => !subModule.Contains($"new {m.Name}("))
            .Select(m => m.FullName)
            .ToList();
```

`ReadRepoFile` (lines 190-198) walks up from the working directory to `TAOM.sln` and returns raw text or null. `DiscoverGameModels` (153-164) returns every non-abstract TAOM type deriving from `TaleWorlds.Core.GameModel`. `TaomPartyNavigationModel` (`Main/Features/NavalTravel/Models/TaomPartyNavigationModel.cs:28`, namespace `TAOM.Features.NavalTravel.Models`, `: DefaultPartyNavigationModel`) is parked; its only `new TaomPartyNavigationModel(` in `SubModule.cs` is the comment at line 1063:

```csharp
        // campaignStarter.AddModel(new TaomPartyNavigationModel(IoC.Resolve<INavalTravelService>(), IoC.Resolve<IModLogger>()));
```

Re-measured at `4c728dac`: of every `new XxxModel(` in SubModule.cs, `TaomPartyNavigationModel` is the only one that exists in the raw text but not in the comment-stripped text. The orchestrator's baseline run at `b2e387db` had 0 inconclusive results, so this test runs (and passes) on the desktop.

### Engine and library facts (verified during planning; do not re-derive)

DryIoc is `DryIoc.dll` 4.8.8 (`Main/TAOM.csproj:100`). From its net45 assembly: `public interface IContainer : IRegistrator, IResolverContext, IResolver, IServiceProvider, IDisposable`; the extensions used here are `public static TService Resolve<TService>(this IResolver resolver, IfUnresolved ifUnresolved = IfUnresolved.Throw)`, `public static void Register<TService, TImplementation>(this IRegistrator registrator, IReuse reuse = null, Made made = null, Setup setup = null, IfAlreadyRegistered? ifAlreadyRegistered = null, object serviceKey = null) where TImplementation : TService`, a matching one-type `Register<TService>(this IRegistrator registrator, IReuse reuse = null, ...)`, and `RegisterInstance<T>(this IRegistrator registrator, T instance, ...)`. So a feature's `Register*Feature` method compiles unchanged when its parameter type changes from `IContainer` to `IRegistrator`, and a `Container` still converts to it.

Bannerlord v1.5.3, from `pwsh tools/taom-src.ps1 path <Type>`:

- `TaleWorlds.Core.IGameStarter`: `void AddModel(GameModel gameModel);` and `void AddModel<T>(MBGameModel<T> gameModel) where T : GameModel;`
- `TaleWorlds.CampaignSystem.CampaignGameStarter : IGameStarter`: `public void AddBehavior(CampaignBehaviorBase campaignBehavior)` (ignores null), and `public void AddModel<T>(MBGameModel<T> gameModel) where T : GameModel { T model = GetModel<T>(); gameModel.Initialize(model); _models.Add(gameModel); }` (the generic overload chains the previous model of that slot as `BaseModel`).
- `TaleWorlds.MountAndBlade.BasicGameStarter : IGameStarter`: the same two `AddModel` overloads.
- `TaleWorlds.Core.MBGameModel<T>`: `public abstract class MBGameModel<T> : GameModel where T : GameModel` with `public void Initialize(T baseModel)`.
- `TaleWorlds.CampaignSystem.CampaignBehaviorBase`: `public abstract class CampaignBehaviorBase : ICampaignBehavior`; its parameterless constructor only sets `StringId = GetType().Name` (safe to construct in a unit test); `public abstract void SyncData(IDataStore dataStore);` (`IDataStore` is in `TaleWorlds.CampaignSystem`).
- `TaleWorlds.MountAndBlade.Mission`: `public void AddMissionBehavior(MissionBehavior missionBehavior)`.
- `TaleWorlds.Library`: `InformationManager.DisplayMessage(InformationMessage message)`, `new InformationMessage(string information, Color color)`, `Colors.Red`, `public static void ShowInquiry(InquiryData data, bool pauseGameActiveState = false, bool prioritize = false)`, and `public InquiryData(string titleText, string text, bool isAffirmativeOptionShown, bool isNegativeOptionShown, string affirmativeText, string negativeText, Action affirmativeAction, Action negativeAction, string soundEventPath = "", float expireTime = 0f, ...)` (`InquiryData` is in `TaleWorlds.Library`).
- **Who receives a notice, and when (the startup inquiry rule, `docs/reviews/lessons/localization-ui.md`, "Nothing receives a chat message before the initial screen", from plan 009's RCA finding 1)**: `DisplayMessage` is `DisplayMessageInternal?.Invoke(message)` with no queue, and its single-player subscribers (`MPChatVM`, `ChatLogMessageManager`) are built by `GauntletChatLogView` in Native's `OnBeforeInitialModuleScreenSetAsRoot`, after every module's `OnSubModuleLoad`. A chat line sent from `OnSubModuleLoad` goes nowhere; one sent from the first `OnBeforeInitialModuleScreenSetAsRoot` is hidden by the splash video and then cleared by `GauntletInitialScreen.OnInitialize` (`ClearAllMessages()`, `GauntletInitialScreen.cs:77` in the v1.5.3 decompile). A startup notice therefore waits for TAOM's `OnBeforeInitialModuleScreenSetAsRoot` and goes through `ShowInquiry`, which `GauntletQueryManager` queues (`CreateQuery` enqueues an inquiry that is not equal to the active or a queued one, `GauntletQueryManager.cs:164-174`), and the initial screen does not clear it. Checked during the re-cut: the only other `ClearAllMessages()` callers in v1.5.3 are the `chatlog.clear` console command (`GauntletUISubModule.cs:261`) and `Module.OnBeforeGameStart` (`Module.cs:1785`), which `MBGameManager.StartNewGame` calls (`MBGameManager.cs:48`) before it pushes the loading state, so before any `OnGameStart`. A red chat line from `OnGameStart`, `OnGameInitializationFinished` or `OnMissionBehaviorInitialize` has a receiver and nothing clears it; plan 009 keeps its red line in the last two for that reason.
- `MBSubModuleBase` hooks TAOM overrides: `OnSubModuleLoad()`, `OnBeforeInitialModuleScreenSetAsRoot()`, `OnGameStart(Game game, IGameStarter gameStarterObject)`, `OnGameInitializationFinished(Game game)`, `OnMissionBehaviorInitialize(Mission mission)`, `OnSubModuleUnloaded()`.

Language: `Directory.Build.props` sets `<TargetFramework>net472</TargetFramework>` and `<LangVersion>10.0</LangVersion>`. **Default interface members are not available** (the .NET Framework runtime does not support them; the compiler reports CS8701), which is why the design's `OnPhase(...) { }` default lives in the abstract base class, not the interface. `Main/TAOM.csproj` exposes internals to `TAOM.Tests` (the `TAOM.Tests` InternalsVisibleTo attribute at `Main/TAOM.csproj:119-121`, inside the ItemGroup at 117-125), so the new types are `internal`.

### IoC.Configure today (`Main/IoC.cs:178-214`)

```csharp
        Features.Enlistment.EnlistmentIoC.RegisterEnlistmentFeature(container);
        Features.Enlistment.Duties.DutiesIoC.RegisterEnlistmentDutiesFeature(container);
        // AFTER Enlistment: FieldCommission registers a NullEnlistmentStateQuery with
        // IfAlreadyRegistered.Keep — the real query must already be in the container.
        Features.FieldCommission.FieldCommissionIoC.RegisterFieldCommissionFeature(container);
        ...
        Features.UncapturableHeroes.UncapturableHeroesIoC.RegisterUncapturableHeroesFeature(container);

        _container = container;

        // Eager patch-static initialisation runs ONLY after the last registration above: ...
        FieldCampIoC.InitializePatchStatics(container);
        Features.Refuge.RefugeIoC.InitializePatchStatics(container);
        Features.UncapturableHeroes.UncapturableHeroesIoC.InitializePatchStatics(container);
        NameplateRelationIoC.InitializeWidgetStatics(container);

        // Post-registration initialization
        CareerSystemIoC.InitializeCalculators(container.Resolve<Features.CareerSystem.Mutations.IMutationCalculatorRegistry>());
    }
```

Line 109 is the pilot's registration: `        Features.WandererAllegiance.WandererAllegianceIoC.RegisterWandererAllegianceFeature(container);`. No test calls `IoC.Configure()` (`git grep -n "IoC.Configure()" -- TAOM.Tests` finds two comments and no call: `EconomyDiagnosticsWiringTests.cs:12` and `SiegeDismountWiringTests.cs:71`).

### Plan 009's helpers at `4c728dac` (read-only here)

`Main/PatchCategoryApplier.cs` (`internal sealed class PatchCategoryApplier`, namespace `TAOM`), its three members' signatures:

- `internal PatchCategoryApplier(Action<string> apply, IModLogger logger)`
- `internal bool TryApply(string category)`: "Applies the category; on a throw, logs it, records it and returns false." (logged `[PatchApply] <category> FAILED ...`)
- `internal string? TakeFailureSummary(string phase)`: "One player-facing line naming every category that failed since the last call, or null when none did. Clears the list, so each phase reports only its own failures."

`SubModule` builds it in `OnSubModuleLoad` (`SubModule.cs:203-205`) as `_patches = new PatchCategoryApplier(category => _harmony.PatchCategory(typeof(SubModule).Assembly, category), IoC.Resolve<IModLogger>());`, the one direct `.PatchCategory(` call 009's source gate allows.

`Main/SubModule.cs:851-872`:

```csharp
    private bool TryPatchCategory(string category) => _patches.TryApply(category);

    // One notice per phase naming every category that failed, so a dead crash guard is never
    // silent: a red chat line, or an inquiry the player dismisses when a screen change would clear
    // the chat log first. The notice itself must never break the phase, hence the catch.
    private void ReportPatchFailures(string phase, bool persistent = false)
    {
        var summary = _patches.TakeFailureSummary(phase);
        if (summary == null) return;
        try
        {
            if (persistent)
                InformationManager.ShowInquiry(new InquiryData(
                    "TAOM", summary, true, false, "OK", string.Empty, null, null));
            else
                InformationManager.DisplayMessage(new InformationMessage(summary, Colors.Red));
        }
        catch (System.Exception ex)
        {
            IoC.Resolve<IModLogger>().LogError($"[PatchApply] failure notice not shown: {ex.Message}");
        }
    }
```

Both helpers are private instance members of `SubModule`, so `Main/Composition` cannot call `ReportPatchFailures` and does not try to: a module's patch category goes through the `TryPatchCategory` delegate the kernel passes in, so its failure lands in `_patches` and is reported by 009's next `ReportPatchFailures` call, as long as the runner call sits before that call (every anchor below does). A module FAULT (a module that throws) is the runner's, and `FeatureModuleHooks` reports it with the same `InquiryData` shape or red line (Step 1.4).

### SubModule phase anchors (at `4c728dac`)

Plan 009 replaced every `_harmony.PatchCategory("X")` with `TryPatchCategory("X")` and reports per phase: no report in `OnSubModuleLoad` (its failures wait), one persistent `startup` inquiry at the main menu for `OnSubModuleLoad`'s and Patch55's failures together, and a red line after the game-init batch and the first-mission category. The anchors this plan inserts next to:

- `OnSubModuleLoad` ends (`SubModule.cs:619-624`):
  ```csharp
          TryPatchCategory("Patch42_CastleRecruitment");
          // No ReportPatchFailures here: nothing receives a message yet (see the startup report in
          // OnBeforeInitialModuleScreenSetAsRoot), so this phase's failures wait for it.

          InformationManager.DisplayMessage(new InformationMessage("TAOM loaded successfully!", Colors.Green));
      }
  ```
  The next member is `    protected override void OnBeforeInitialModuleScreenSetAsRoot()` (626).
- `OnBeforeInitialModuleScreenSetAsRoot` (`SubModule.cs:638-648`):
  ```csharp
          if (!_basicTableauGuardApplied)
          {
              _basicTableauGuardApplied = true;
              TryPatchCategory("Patch55_BasicTableauRaceGuard");
              // Reports OnSubModuleLoad's failures and Patch55's together. The earliest a notice can
              // be shown: Native's GauntletUISubModule, which runs before TAOM, creates the chat log
              // and the inquiry manager in this hook, and InformationManager queues nothing sent
              // before them. An inquiry, not a chat line: the initial screen clears the chat log
              // after the splash video (GauntletInitialScreen.OnInitialize, ClearAllMessages).
              ReportPatchFailures("startup", persistent: true);
          }
  ```
  `_basicTableauGuardApplied` is a `private static bool` (`SubModule.cs:110`), so this block runs once per process although the hook fires on every return to the main menu.
- `OnGameStart` (`SubModule.cs:797-835`, untouched by 009) ends:
  ```csharp
              RegisterSpecialResourcesAndCareers(campaignStarter, careerPassives);
              RegisterCampaignLifeBehaviors(campaignStarter);
          }
      }
  ```
  Before the `if`, it calls `RegisterCustomBattleModels(gameStarterObject);` (810), which returns unless the starter is a `BasicGameStarter` and not a `CampaignGameStarter` (`1279-1287`). The next member after `OnGameStart` is `    public override void OnGameLoaded(Game game, object initializerObject)` (845).
- `OnGameInitializationFinished`: after the once-per-process guard `if (_gameInitPatchesApplied) return;` / `_gameInitPatchesApplied = true;` (`1483-1484`), the batch ends (`SubModule.cs:1870-1874`):
  ```csharp
          TryPatchCategory("Patch69_TournamentRosterGuard");
          TryPatchCategory("Patch69_TournamentEndGuard");
          ReportPatchFailures("game initialization");

          // Manual patches for PRIVATE engine methods (AccessTools-resolved targets; can't use
  ```
  followed by `ManualPatchApplicator.ApplyAll(_harmony);` (1877).
- `OnMissionBehaviorInitialize` (`SubModule.cs:1919`) opens with (`1927-1932`):
  ```csharp
          if (!_missionTimePatchesApplied)
          {
              _missionTimePatchesApplied = true;
              TryPatchCategory("Patch_MissionTime_SetMovementOrder");
              ReportPatchFailures("mission start");
          }
  ```
  then the `[BattleLoad]` bracket and the local function `void AddTaomBehavior(MissionBehavior behavior)` (`1945-1950`), the feature adds, and (`2029-2042`, untouched by 009):
  ```csharp
          var colorStore = IoC.Resolve<IAgentColorStore>();
          if (colorStore != null)
              AddTaomBehavior(new AgentColorStoreCleanupBehavior(colorStore));

          // MissionDiagnostic: added LAST so it sees all behaviors added by TAOM AND
          ...
              AddTaomBehavior(new Features.MissionDiagnostic.Hooks.MissionDiagnosticBehavior(diagSvc, raceMgr, diagLogger));
  ```

The pilot's lines in `RegisterCampaignLifeBehaviors` (`1399-1408`, untouched by 009):

```csharp
        campaignStarter.AddBehavior(new Features.AlignmentDesertion.Hooks.AlignmentDesertionBehavior(
            IoC.Resolve<Features.AlignmentDesertion.IAlignmentDesertionService>(),
            IoC.Resolve<IModLogger>()));

        // WandererAllegiance (#575): a wanderer refuses to be hired across the Free/Evil line. Two
        // condition-gated NPC lines on vanilla's companion_hire token (priority 110 over vanilla's
        // 100); stateless, reads MCM live, so registered unconditionally. No Harmony patch.
        campaignStarter.AddBehavior(IoC.Resolve<Features.WandererAllegiance.Hooks.WandererAllegianceDialogBehavior>());

        // EliteEmissary — buy a faction's elite troops for its special resource at key settlements.
```

### The pilot feature

`Main/Features/WandererAllegiance/WandererAllegianceIoC.cs` (body):

```csharp
public static class WandererAllegianceIoC
{
    public static void RegisterWandererAllegianceFeature(IContainer container)
    {
        container.Register<IWandererAllegianceConfigProvider, WandererAllegianceConfigProvider>(Reuse.Singleton);
        container.Register<IWandererAllegianceSettingsProvider, WandererAllegianceSettingsProvider>(Reuse.Singleton);
        container.Register<IWandererAllegianceService, WandererAllegianceService>(Reuse.Singleton);
        container.Register<Hooks.WandererAllegianceDialogBehavior>(Reuse.Singleton);
    }
}
```

Its dependencies from other features resolve lazily: `WandererAllegianceService(IAlignmentService alignment, IWandererAllegianceSettingsProvider settings, INamedCompanionConfigProvider namedCompanions)` (`IAlignmentService` in `TAOM.Features.Execution`, `INamedCompanionConfigProvider` in `TAOM.Features.NamedCompanions`, read lazily); `WandererAllegianceSettingsProvider(IWandererAllegianceConfigProvider)` calls `GetConfig()` in its constructor; `WandererAllegianceConfigProvider(IPathService pathService, IModLogger logger)` falls back to defaults with a warning when `wanderer_allegiance/wanderer_allegiance_config.json` is missing under `IPathService.ModuleDataPath` (`IPathService` in `TAOM.Core.Infrastructure`). `WandererAllegianceDialogBehavior(IWandererAllegianceService service, IModLogger logger) : CampaignBehaviorBase` registers two dialog lines on `OnSessionLaunchedEvent` at `private const int Priority = 110;` and has an empty `public override void SyncData(IDataStore dataStore) { }`. `git grep` at `4c728dac` finds `RegisterWandererAllegianceFeature` called only from `Main/IoC.cs:109`, and no other feature registers any WandererAllegiance service type.

Why the pilot's move to the end of the campaign-start phase is order-free: its two lines are the only TAOM lines on the `companion_hire` token, and vanilla's reply sits at priority 100, so priority, not add order, decides; and the `LotrIssueSuppression.SuppressAll` call that ends `RegisterCampaignLifeBehaviors` removes only the vanilla issue behavior types in its own list (`Main/Features/LotrIssues/LotrIssueSuppression.cs:170-197`, `RemoveBehaviors<T>` per vanilla type), so a TAOM behavior added after it is untouched. Its services move from IoC position 109 to after the last hand-wired registration; that is order-free because DryIoc resolves lazily, the feature registers no `IfAlreadyRegistered` or contributor-collection type, and nothing resolves its types during `Configure`.

`TAOM.Tests/Features/WandererAllegiance/WandererAllegianceWiringTests.cs` has six tests; two are text asserts on the composition root that this plan converts:

```csharp
    [TestMethod]
    public void IoC_RegistersTheFeature()
    {
        var src = ReadSource("Main", "IoC.cs");

        StringAssert.Contains(src, "WandererAllegianceIoC.RegisterWandererAllegianceFeature(container)",
            "Main/IoC.cs no longer registers WandererAllegiance; SubModule's Resolve would throw at campaign start.");
    }
    ...
    [TestMethod]
    public void SubModule_AddsTheDialogBehavior()
    {
        var src = ReadSource("Main", "SubModule.cs");

        StringAssert.Contains(src, "WandererAllegianceDialogBehavior>()",
            "SubModule.cs no longer adds WandererAllegianceDialogBehavior; the refusal lines are never registered.");
        StringAssert.Contains(src, "campaignStarter.AddBehavior(IoC.Resolve<Features.WandererAllegiance.Hooks.WandererAllegianceDialogBehavior>())",
            "The behavior must be resolved from IoC (it needs the service) and added via AddBehavior.");
    }
```

The other four (`WandererAllegianceIoC_RegistersEveryConsumerOfTheBehavior`, `DialogBehavior_RegistersBothRefusalsOnCompanionHire_ToLordPretalk`, `DialogBehavior_PriorityIsAboveVanillasHundred`, `RefusalStrings_AreRegisteredForTranslation`) read the feature's own files and stay.

### Ordering constraints found in the code (awareness; only the WandererAllegiance facts above apply to this plan)

Line numbers are `Main/SubModule.cs` at `4c728dac` unless a file is named (re-mapped from the first cut with a line-level diff and spot-read). When a later migration moves a feature to the end-of-phase module loop, check it against this table.

| Constraint | Evidence | Where it lands in the design |
|---|---|---|
| CrashReport patch first, before any other apply | `188-223` | kernel, before the loop |
| Patch41 MCM layout and Patch83/58/61/62/89/90 must apply in `OnSubModuleLoad`, not later | `229-236`, `307-315`, `317-333`, `350-351` | `ApplyPhase.ProcessLoad` on the category decl |
| Patch55 must apply at the main menu | `631-637` | `ApplyPhase.MainMenu` |
| Mission-time category only once `Mission.Current` exists | `1923-1932` | `ApplyPhase.FirstMission` |
| Game-init batch once per process (re-apply duplicates patches and breaks the DeliverOffSpring transpiler) | `1476-1484` | kernel flag around the `GameInit` loop |
| Patch65/Patch88 must be in the standard game-init batch, not lazier (new-game spawn path) | `1606-1620` | `ApplyPhase.GameInit`; the paragraph moves to the patch class |
| FieldCommission after Enlistment (`IfAlreadyRegistered.Keep`) | `IoC.cs:180-182`, `FieldCommissionIoC.cs:31` | list order; index test |
| UncapturableHeroes after Enlistment (single `IInquiryAdapter` registration) | `IoC.cs:194-199` | list order; the existing `IndexOf` test becomes a list-index test |
| Eager patch statics only after every registration | `IoC.cs:203-210`, `IoCRegistrationDisciplineTests` | phase 2 `InitializeStatics`; `IRegistrator` has no `Resolve`, so an eager resolve does not compile |
| Contributor collections complete before first resolve (`ICampOverlayContributor`, `IPartySpottingContributor`) | `FieldCampIoC.cs:19-27` | same two-phase rule; `ResolveMany` order is registration order, so module order also fixes contributor order |
| One engine model per slot: MarriageModel, AgentStatCalculateModel, AgentApplyDamageModel, BattleMoraleModel, MapVisibilityModel, BattleBannerBearersModel, BattleInitializationModel | `1078-1085`, `1233-1269`, `FieldCampIoC.cs:19-20` | a slot type in exactly one module's `GameModels` per target; a generic test enforces it |
| TAOM models registered in `OnGameStart` so they follow SandBox's defaults | `1050-1053`, `1252-1265` | the model loop runs in `OnGameStart` |
| Custom Battle mirrors two models on `BasicGameStarter` only | `1272-1287` | `ModelTarget.CustomBattle` |
| Vanilla behavior removal before its TAOM replacement | `1025-1027` (InitialChildGeneration), `1462-1465` (LotrIssues) | inside the owning module's factory; `SuppressAll` removes vanilla types only (see above) |
| Player Switcher character-creation handler at priority 1100, after TAOM's 1050; equal priorities throw | `988-991`; `CharacterCreationRegistrationBehavior.cs:9` | independent of list order; a later test reads both constants |
| Mission behaviors tick in reverse add order; the tree logic must be added before `AdvancedCombatBehavior` | `1961-1967` | list order, asserted by index |
| `MissionDiagnosticBehavior` added last among TAOM's inspected adds, `BattleLoadPhaseBehavior` after TAOM's adds | `2033-2064` | kernel tail |
| Harmony census after every patch | `1879-1883` | kernel tail |
| A notice raised before the main menu has no receiver; startup problems are shown once, in an inquiry, at the first `OnBeforeInitialModuleScreenSetAsRoot` (the startup inquiry rule, see Engine facts) | `620-621`, `642-647`; `lessons/localization-ui.md`; 009's `PatchCategoryApplierTests.SubModuleSource_OnSubModuleLoad_DoesNotReportPatchFailures` | `FeatureModuleHooks.NoticeFor`: ProcessLoad holds, MainMenu shows an inquiry (Step 1.4) |

Two constraints in the prose are stale (lane finding COMP-06): FieldCamp's position comment in `IoC.cs:184-186` (the eager resolve it describes was removed in `16a58b51`) and the Patch25 "must be first" comment (`270`). Before the first gameplay feature with patches moves, the design calls for a one-off reflection script that lists every engine method patched by two or more TAOM categories; each such pair becomes an explicit list-order constraint with a test. Not part of this plan.

### Conventions that bind this change

- **ADR-002 (thin entry points, under 150 lines)**: entry points delegate to testable classes. `SubModule.cs` is already far over (known; this plan starts the fix). Add to it only the six one-line runner calls and the `using`; every loop lives in `Main/Composition`.
- **ADR-007 (adapters for sealed TaleWorlds types)**: services take adapters, never sealed engine types. The new types are composition-root infrastructure, not services: `ModuleRunner` is engine-free and unit-tested; the decls and `FeatureModuleHooks` touch `CampaignGameStarter`, `Mission` and `InformationManager` the same way `SubModule` does today. Do not challenge the adapter pattern itself.
- **ADR-008 (service testability)**: `ModuleRunner` takes its modules and a logger factory through its constructor and makes no static TaleWorlds call, so it is constructible with NSubstitute fakes.
- **`.claude/rules/csharp-architecture.md`**: constructor injection, no service locator inside services, NSubstitute for mocks, `Reuse.Singleton` for services. **ADR-003/004/005**: no `#region`, no `[Obsolete]`, no `#if DEBUG`.
- **`.claude/rules/simplicity-criterion.md`**: the contract carries every dimension now although the list is empty and the pilot uses two. The trade-off, stated: win, every later migration only deletes lines from the two single-owner files; cost, six small types and eight kernel lines before a second module uses them. That is the "improvement large enough to dominate its cost" row; say it in the commit body.
- **Plan 009's source gate** (`PatchCategoryApplierTests.MainSource_AppliesEveryPatchCategoryThroughTheGuardedHelper`) fails on any direct `.PatchCategory(` call in `Main/**/*.cs` other than the one delegate in `SubModule.cs`. Never write `.PatchCategory(` in new Main code; modules apply categories through the `Func<string, bool>` the kernel passes in.
- **Localization**: the fault notice (the inquiry's title `TAOM`, button `OK` and body, and the red line) is literal English, exactly like plan 009's `ReportPatchFailures`. Do not use a `{=key}` string (the localization ratchet tests would fail on an unregistered key).
- **Startup notices (the startup inquiry rule, `docs/reviews/lessons/localization-ui.md`)**: nothing receives a chat line before the initial screen, and the initial screen clears the chat log after the splash video. A problem found during startup is held until TAOM's first `OnBeforeInitialModuleScreenSetAsRoot` and shown there with `InformationManager.ShowInquiry`; never a `DisplayMessage` from `OnSubModuleLoad` or `IoC.Configure`. See "Engine and library facts" for the receivers.
- **Tests model**: container tests follow `TAOM.Tests/Features/AutoResolveDiagnostics/AutoResolveDiagnosticsWiringTests.cs` (`new Container()`, `RegisterInstance(Substitute.For<IModLogger>())`, `using var container`); logger assertions follow `TAOM.Tests/Core/Domain/RaceManagerTests.cs` (`_logger.Received(1).LogError(Arg.Is<string>(...))`).

### Decisions already taken (do not reopen)

- **Pilot is WandererAllegiance.** Of the three suggested pilots, none has every dimension: ReturnToArmy has a category, a statics handshake and an unload reset but no services; SiegePropDiagnostics has services and a mission behavior; WandererAllegiance has services and a campaign behavior. WandererAllegiance exercises both single-owner files (the `IoC.cs` register loop and a `SubModule` behavior add), is order-free (shown above), owns no save data, and has no `ResetForUnload`, so it does not drag the unresolved unload-sweep question in.
- **No `ResetForUnload` in the contract.** The design adds it "only if COMP-05 keeps the sweep"; COMP-05 (does the engine ever reload TAOM in-process?) is an open decision for the maintainer. `ResetForUnloadSweepTests` keeps scanning `public static void ResetForUnload(` declarations and is not affected.
- **The pilot's behavior stays a container singleton** (`r => r.Resolve<WandererAllegianceDialogBehavior>()`), exactly as `SubModule` resolved it. The design's `Reuse.Transient` for behaviors (lane finding COMP-02) is a separate, deferred change.
- **`RegisterServices` takes `IRegistrator`**, so an eager `Resolve` during registration does not compile; `InitializeStatics` takes `IResolver`.
- **Parked modules get only `RegisterServices`** (their services stay resolvable, as NavalTravel's are today); the runner skips their statics, categories, behaviors, models, mission behaviors and `OnPhase`. No log line for a parked module.
- **Fault policy**: a module that throws is logged `[Module] <Id> failed in <step>: <exception>`, marked faulted and skipped in every later step of the session; the next module still runs. Each report point names the modules that faulted since the last one, in one notice chosen by who can receive it: faults from `IoC.Configure` (service registration, static initialisation) and from `ApplyPhase.ProcessLoad` are held; the `ApplyPhase.MainMenu` point shows them, with any MainMenu fault, in one inquiry of the same shape as 009's startup report (queued just before it); campaign start, `ApplyPhase.GameInit`, `ApplyPhase.FirstMission` and every mission start show a red chat line. (Re-cut: the first cut showed a red line at every point, including the two that nothing receives.) A module with `OwnsSaveData = true` fails CLOSED (rethrows) in service registration, static initialisation and campaign start. A failed patch category is plan 009's business (logged `[PatchApply]`, reported by `ReportPatchFailures`) and does not fault the module.
- **No auto-discovery of modules, no `CoopRelevance`, no per-frame module tick, and the loop never reads MCM.** An explicit ordered list is what the ordering constraints need.
- **The "declared category exists in the assembly" reflection test is deferred** to the first module that declares a category (nothing to check yet). This plan adds only the double-apply guard.

## Commands you will need

Run all of them from the worktree root. Never `./build.ps1` (it deploys into the game install).

| Purpose | Command | Expected on success |
|---|---|---|
| Build | `dotnet build Main/TAOM.csproj -p:DisableModuleCopy=true -p:ModuleId=` | exit 0, `0 Error(s)` |
| Tests | `dotnet test TAOM.Tests -p:DisableModuleCopy=true -p:ModuleId=` | only the two known Armory tests may fail (below) |
| Filtered tests | `dotnet test TAOM.Tests -p:DisableModuleCopy=true -p:ModuleId= --filter "FullyQualifiedName~<ClassName>"` | as stated per step |
| Data | `python tools/validate_moduledata.py` | 0 errors (not affected; run once at the end) |
| Docs | `python tools/lint_docs.py` | exit 0, 0 dead links |
| Docs, commit gate | `python tools/lint_docs.py --fail-on-drift` | exit 0 (a PreToolUse hook runs this on commits touching feature docs) |

**Known baseline at `4c728dac`** (recorded in the Convergence section of plan 009's deep review, `docs/reviews/deep-review-009-guarded-patch-category-apply-2026-09-24.md`, after its last test edit; `4c728dac` itself changed no test): **Failed 2, Passed 10248, Skipped 2, Total 10252**. That is the first cut's `b2e387db` baseline (10,239 tests, 0 inconclusive) plus 009's 13 `PatchCategoryApplierTests`. The 2 failures are `ElkConfigTests.TheElkItem_DeclaresTheScaleTheReachIsTunedFor` and `AnimaliaMountWiringTests.AnimaliaActionSets_BindOnlyHorseActions_ToClipsThatExist`; both read the live, unversioned Armory install, which other sessions edit. They are not caused by this or any plan: do not chase them and do not edit either file. The 2 not executed are deliberate `[Ignore]`s in `WargAttackServiceTests`. Record your own Step 0.0 total as `T0` even if it differs from 10,252 (the Armory tests can pass or fail with the live install); any failure other than the two Armory tests is yours.

## Scope

**In scope** (the only files you may create or modify, on your worktree branch):

- `TAOM.Tests/Infrastructure/RepoPaths.cs` (add `ReadSource` and `StripComments`)
- `TAOM.Tests/Infrastructure/RepoPathsTests.cs` (new)
- The 26 test files in the Step 0.3 table: only their reads of `Main/SubModule.cs` and `Main/IoC.cs`, the guards around those reads, orphaned private locator helpers, and one `using`. In `GameModelOverrideBindingTests.cs` also the parked allowlist and one new test (Step 0.2). In `WandererAllegianceWiringTests.cs` also the two converted tests (Step 2).
- `Main/Composition/ITaomFeatureModule.cs`, `FeatureDecls.cs`, `TaomFeatureModule.cs`, `ModuleRunner.cs`, `FeatureModules.cs`, `FeatureModuleHooks.cs` (all new)
- `TAOM.Tests/Composition/ModuleRunnerTests.cs`, `TAOM.Tests/Composition/FeatureModulesTests.cs` (new)
- `Main/IoC.cs`: **single-owner, listed explicitly.** Only: one `using`, two `internal static` members, the runner construction plus `RegisterServices` call, the `InitializeStatics` call (Step 1.4), and deleting line 109 (Step 2.4).
- `Main/SubModule.cs`: **single-owner, listed explicitly.** Only: one `using`, six one-line runner calls (Step 1.4), and deleting the WandererAllegiance comment and `AddBehavior` line (Step 2.4).
- `Main/Features/WandererAllegiance/WandererAllegianceModule.cs` (new) and `WandererAllegianceIoC.cs` (parameter type only)
- `docs/features/wanderer-allegiance.md` (three table and list lines, Step 2.5)

**Out of scope** (do NOT touch, even though they look related):

- `Main/TAOM.csproj`, `TAOM.Tests/TAOM.Tests.csproj`, `Directory.Build.props`: not needed (SDK-style globbing picks up new `.cs` files). If you believe one needs a change, STOP and report the exact line.
- Any other feature's wiring in `SubModule.cs` or `IoC.cs`, `ManualPatchApplicator.cs`, `PatchCategoryApplier.cs`, and plan 009's helpers.
- Any test file not listed, including `ResetForUnloadSweepTests`'s declaration scan, `CoopVetoClassificationTests` (leave its private `StripComments` in place) and plan 009's `PatchCategoryApplierTests` (it already strips comments itself; its source gates must stay green unchanged).
- `CHANGELOG.md` (another session holds uncommitted edits; the orchestrator writes the entry), everything under `.claude/` (including `.claude/rules/gamemodels.md`), `docs/INDEX.md`, `docs/reference/feature-map.md`, `plans/README.md`.
- Behavior reuse (`Reuse.Singleton` stays), any save-format change, any MCM setting.

## Git workflow

- Work in a new worktree at commit `4c728dac` (the reviewed tip of `improve/009-guarded-patch-category-apply`; plan 009 is not yet merged into `bannerlord-1.5.x`), never in `E:\repos\TAOM` itself (its working tree holds other sessions' uncommitted edits):
  `git -C E:/repos/TAOM worktree add E:/repos/wt-plan-018 -b plan/018-composition-root 4c728dac`
  Run every command in this plan from `E:/repos/wt-plan-018`. If the branch or directory already exists, STOP and report; do not delete, reuse or reset either. Do not create archive extractions or other copies of the repo (the C: drive is nearly full); the worktree on E: is the only checkout you need.
- The worktree checks out with CRLF line endings (`core.autocrlf=true`) and `SubModule.cs` starts with a UTF-8 BOM. Use the Edit tool; never `sed -i`. Create new files with the Write tool (write scripts to the session scratchpad, not heredocs).
- Commit subject: `<type>(<scope>): v<version> - <description>`, where the version is the `<Version value="..."/>` in `Main/_Module/SubModule.xml` (`v2.0.30` at planning time; re-read it before each commit). At most 72 characters (check with `git log -1 --format=%s | awk '{print length}'`), body wrapped at 72, **no AI attribution trailer** (no `Co-Authored-By`).
- Stage explicit paths only (`git add <path> ...`), never `git add -A` or `git commit -a`.
- Three commits, each on a green suite:
  1. `test(composition): v2.0.30 - read SubModule and IoC through one reader` (70 chars): RepoPaths, RepoPathsTests, the 26 test files. Trailer: `Not-tested: none (test-only change)`.
  2. `refactor(composition): v2.0.30 - add the feature-module runner, empty` (69 chars): the six `Main/Composition` files, both `TAOM.Tests/Composition` files, `Main/IoC.cs`, `Main/SubModule.cs`. Body states the simplicity trade-off (see Conventions). Trailers: `Not-tested: FeatureModuleHooks against a live engine (needs the game)` and `Save-compat: no save data touched`.
  3. `refactor(composition): v2.0.30 - move WandererAllegiance into a module` (70 chars): the module, `WandererAllegianceIoC.cs`, `FeatureModules.cs`, `FeatureModulesTests.cs`, `WandererAllegianceWiringTests.cs`, `Main/IoC.cs`, `Main/SubModule.cs`, `docs/features/wanderer-allegiance.md`. Trailers: `Not-tested: in-game wanderer refusal (needs a campaign)` and `Save-compat: the behavior's SyncData is empty; no save data touched`.
- No commit stages a `.claude/*` path. The CHANGELOG hook (`.claude/hooks/check-changelog-changed.sh`) can still deny one: it changes to `CLAUDE_PROJECT_DIR` (the main tree, `E:\repos\TAOM`) and reads that tree's index, so another session's staged `.claude/` files there block your commits. If any hook denies a commit, STOP and report its message verbatim; never bypass a hook and never edit an out-of-scope file to satisfy one.
- Never push, never open a PR, never merge. **Merge note for the orchestrator**: this branch sits on `4c728dac`, so it carries plan 009's four commits; merge plan 009 first (or the two together). `bannerlord-1.5.x` has two commits that `4c728dac` lacks: `473e4ccc` (docs only) and `709649c3`, which edits both single-owner files: `Main/IoC.cs` gains the Animalia and MonsterSize registrations after the Elk registration (line 123), and `Main/SubModule.cs` gains four lines above the game-init guard (a `MonsterSize` `ApplyMonsterSizes()` call after the `StampSaveLoadPhase` line) and one `AddTaomBehavior(new Features.Animalia.AnimaliaMissionBehavior());` after the Elk mission behavior. None of those lines is next to an anchor this plan uses. `709649c3` also adds `AnimaliaWiringTests` and `MonsterSizeWiringTests`, which read `SubModule.cs` or `IoC.cs` as raw text; they stay raw (outside this plan's 26). After the merge, re-run `FeatureModulesTests` (the kernel wiring test pins the runner calls between their anchors), `PatchCategoryApplierTests` and the full suite.

## Steps

### Step 0.0: Set up and record the baseline

Create the worktree, run the precondition and the drift check (top of this file), then:

**Verify**:
- Build command → exit 0.
- Tests command → only the two known Armory tests fail (or none, if the Armory is back in step), 2 not executed. Write down the total; later steps state their expected totals as deltas from it (call it `T0`).

### Step 0.1 (RED, then GREEN): Add the shared reader and its tests

Create `TAOM.Tests/Infrastructure/RepoPathsTests.cs` with exactly this content:

```csharp
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace TAOM.Tests.Infrastructure;

/// <summary>
/// Pins <c>RepoPaths.ReadSource</c>, the one reader every test that scans Main/SubModule.cs or
/// Main/IoC.cs goes through: LF line endings, either path separator, a missing file FAILS (a wiring
/// gate that goes Inconclusive stops guarding without saying so), and the comment-stripped view keeps
/// length and line breaks while hiding commented-out code.
/// </summary>
[TestClass]
public class RepoPathsTests
{
    [TestMethod]
    public void ReadSource_ReturnsTheFileWithLfLineEndingsOnly()
    {
        var text = RepoPaths.ReadSource("TAOM.Tests/Infrastructure/RepoPaths.cs");

        StringAssert.Contains(text, "public static class RepoPaths");
        Assert.IsFalse(text.Contains("\r"), "ReadSource must normalise CRLF to LF.");
    }

    [TestMethod]
    public void ReadSource_AcceptsEitherPathSeparator()
    {
        Assert.AreEqual(
            RepoPaths.ReadSource("TAOM.Tests/Infrastructure/RepoPaths.cs"),
            RepoPaths.ReadSource(@"TAOM.Tests\Infrastructure\RepoPaths.cs"));
    }

    [TestMethod]
    public void ReadSource_MissingFile_FailsTheTestInsteadOfSkippingIt()
    {
        Assert.ThrowsException<AssertFailedException>(
            () => RepoPaths.ReadSource("Main/NoSuchFile_Plan018.cs"));
    }

    [TestMethod]
    public void StripComments_BlanksLineAndBlockComments_KeepingLengthAndLineBreaks()
    {
        const string source =
            "keep();\n" +
            "// campaignStarter.AddModel(new ParkedModel());\n" +
            "live(); /* block\n spans */ tail();\n";

        var stripped = RepoPaths.StripComments(source);

        Assert.AreEqual(source.Length, stripped.Length);
        CollectionAssert.AreEqual(
            source.Select((c, i) => c == '\n' ? i : -1).Where(i => i >= 0).ToArray(),
            stripped.Select((c, i) => c == '\n' ? i : -1).Where(i => i >= 0).ToArray());
        StringAssert.Contains(stripped, "keep();");
        StringAssert.Contains(stripped, "live();");
        StringAssert.Contains(stripped, "tail();");
        Assert.IsFalse(stripped.Contains("new ParkedModel("), "A commented-out registration must not survive.");
        Assert.IsFalse(stripped.Contains("block") || stripped.Contains("spans"), "A block comment must not survive.");
    }
}
```

**Verify (RED)**: filtered tests with `RepoPathsTests` → the test build fails with `error CS0117` (`'RepoPaths' does not contain a definition for 'ReadSource'`, and the same for `StripComments`).

Then replace the whole of `TAOM.Tests/Infrastructure/RepoPaths.cs` with:

```csharp
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace TAOM.Tests.Infrastructure;

/// <summary>
/// Locates a repo file from THIS source file's compile-time path, so a test that reads shipped
/// source or ModuleData does not depend on the test assembly's output layout (bin/Debug/net472
/// depth) staying what it is today. Import with <c>using static TAOM.Tests.Infrastructure.RepoPaths;</c>.
/// </summary>
public static class RepoPaths
{
    private static readonly Regex CommentPattern =
        new(@"/\*.*?\*/|//[^\n]*", RegexOptions.Compiled | RegexOptions.Singleline);

    public static string RepoPath(params string[] parts)
    {
        // TWO levels: <repo>/TAOM.Tests/Infrastructure -> TAOM.Tests -> <repo>.
        var repoRoot = Path.GetFullPath(Path.Combine(ThisFile(), "..", ".."));
        return Path.Combine(new[] { repoRoot }.Concat(parts).ToArray());
    }

    /// <summary>
    /// The text of a repo file for a source-scanning test, CRLF normalised to LF. A missing file FAILS
    /// the test: a wiring gate that goes Inconclusive when it cannot find its file silently stops
    /// guarding. With <paramref name="stripComments"/>, comments are blanked (see
    /// <see cref="StripComments"/>), so a commented-out line can no longer satisfy a "this is wired"
    /// assertion.
    /// </summary>
    /// <param name="relativePath">Repo-relative, either separator, for example "Main/SubModule.cs".</param>
    public static string ReadSource(string relativePath, bool stripComments = false)
    {
        var path = RepoPath(relativePath.Split('/', '\\'));
        if (!File.Exists(path))
            Assert.Fail($"Source file not found: {path}. RepoPaths resolves from its own compile-time path "
                + "([CallerFilePath]); a <PathMap> on TAOM.Tests would break that.");

        var text = File.ReadAllText(path).Replace("\r\n", "\n");
        return stripComments ? StripComments(text) : text;
    }

    /// <summary>
    /// Blanks // and /* */ comments to spaces, keeping length and line breaks so IndexOf offsets and
    /// line numbers still line up. Not string-literal aware: a "//" inside a string literal also blanks
    /// the rest of that line. Main/SubModule.cs and Main/IoC.cs had no such literal at 4c728dac.
    /// </summary>
    public static string StripComments(string source) =>
        CommentPattern.Replace(source, m => Regex.Replace(m.Value, "[^\n]", " "));

    private static string ThisFile([CallerFilePath] string path = "") => Path.GetDirectoryName(path)!;
}
```

**Verify (GREEN)**: filtered tests with `RepoPathsTests` → 4 passed, 0 failed.

### Step 0.2 (RED, then GREEN): Prove the false pass, then make the parked model explicit

In `TAOM.Tests/Migration/GameModelOverrideBindingTests.cs`:

1. Add `using TAOM.Tests.Infrastructure;` to the usings.
2. In `EveryTaomGameModel_IsRegistered_InSubModule`, replace the three lines
   ```csharp
           var subModule = ReadRepoFile("Main", "SubModule.cs");
           if (subModule == null)
               Assert.Inconclusive("Main/SubModule.cs not found — run from repo root.");
   ```
   with
   ```csharp
           // Comment-stripped: a commented-out AddModel line is not a registration.
           var subModule = RepoPaths.ReadSource("Main/SubModule.cs", stripComments: true);
   ```
3. Delete the now-unused `ReadRepoFile` helper (lines 190-198, the method starting `private static string ReadRepoFile(params string[] relativeParts)`).

**Verify (RED, this is the proof of the false pass)**: filtered tests with `GameModelOverrideBindingTests` → exactly 1 failure, `EveryTaomGameModel_IsRegistered_InSubModule`, whose message lists exactly one model: `TAOM.Features.NavalTravel.Models.TaomPartyNavigationModel`. If the test reports Inconclusive instead, or lists any other model, STOP (see STOP conditions).

Then:

4. Add this field to the class, directly under `private static bool _gameLoaded;`:
   ```csharp
       /// <summary>
       /// GameModels that compile but are deliberately NOT registered, by class name, with the reason.
       /// Checked against the comment-stripped SubModule, so a commented-out AddModel line no longer
       /// counts as a registration; a parked model is reported as parked, never as registered.
       /// </summary>
       private static readonly Dictionary<string, string> ParkedModels = new(StringComparer.Ordinal)
       {
           ["TaomPartyNavigationModel"] =
               "NavalTravel parked 2026-06-26 at the SubModule wiring (#296/#120): TAOM_Map has no naval navmesh",
       };
   ```
5. In `EveryTaomGameModel_IsRegistered_InSubModule`, change the query to skip parked models and report them:
   ```csharp
           foreach (var parked in models.Where(m => ParkedModels.ContainsKey(m.Name)))
               Console.WriteLine($"Parked, not registered by design: {parked.FullName} ({ParkedModels[parked.Name]})");

           var unregistered = models
               .Where(m => !ParkedModels.ContainsKey(m.Name))
               .Where(m => !subModule.Contains($"new {m.Name}("))
               .Select(m => m.FullName)
               .ToList();
   ```
6. Add this test after `EveryTaomGameModel_IsRegistered_InSubModule`:
   ```csharp
       [TestMethod]
       [TestCategory("BindingVerification")]
       public void ParkedModels_AreRealModels_ThatSubModuleDoesNotRegister()
       {
           if (!_gameLoaded)
               Assert.Inconclusive("Game assemblies not loaded: " + string.Join("; ", GameAssemblies.Diagnostics));

           var names = DiscoverGameModels().Select(m => m.Name).ToList();
           var subModule = RepoPaths.ReadSource("Main/SubModule.cs", stripComments: true);

           foreach (var parked in ParkedModels)
           {
               Assert.IsTrue(names.Contains(parked.Key),
                   $"{parked.Key} is listed as parked but no longer exists as a GameModel: remove it from ParkedModels.");
               Assert.IsFalse(subModule.Contains($"new {parked.Key}("),
                   $"{parked.Key} is registered again: remove it from ParkedModels ({parked.Value}).");
           }
       }
   ```
   `Dictionary`, `StringComparer`, `Console` and LINQ are covered by the file's existing `using System;`, `using System.Collections.Generic;` and `using System.Linq;`.

**Verify (GREEN)**: filtered tests with `GameModelOverrideBindingTests` → 0 failed; the class has one more test than before.

### Step 0.3: Move the other 25 files onto the reader

For each file in the table (paths under `TAOM.Tests/`, line numbers at `4c728dac`, the same as at the first cut's `b2e387db` because plan 009 replaced one category-string line in nine of these files without adding or removing lines; `GameModelOverrideBindingTests` was done in 0.2), apply these rules and nothing else:

- **Rule A (replace the read)**: every expression that reads `Main/SubModule.cs` or `Main/IoC.cs` becomes `RepoPaths.ReadSource("Main/SubModule.cs", stripComments: true)` or `RepoPaths.ReadSource("Main/IoC.cs", stripComments: true)`. Where the read was spread over a path variable, a `File.Exists` assert and a `File.ReadAllText`, collapse it into one declaration that keeps the name the assertions use (for example `var source = RepoPaths.ReadSource("Main/SubModule.cs", stripComments: true);`). If a `var repoRoot = FindRepoRoot();` local fed only that path, delete it too (Patch80 line 289, SharedMovementOrderPostfixTests line 56). Always call it qualified as `RepoPaths.ReadSource`: several files have their own private `ReadSource`, which would otherwise win.
- **Rule A, inline reads (rows 7 and 14)**: in `Patch80KingdomVoteDeadlockBindingTests.SubModule_AppliesThePatchCategory` and in the Patch82 test, `subModule` is the *path* and the text is passed inline as `File.ReadAllText(subModule)`. Reuse the name `subModule` for the text. Patch80 before (lines 289-294; the expected string is as plan 009 left it):
  ```csharp
          var repoRoot = FindRepoRoot();
          var subModule = Path.Combine(repoRoot, "Main", "SubModule.cs");
          Assert.IsTrue(File.Exists(subModule), "Main/SubModule.cs not found at " + subModule);

          StringAssert.Contains(
              File.ReadAllText(subModule),
  ```
  After:
  ```csharp
          var subModule = RepoPaths.ReadSource("Main/SubModule.cs", stripComments: true);

          StringAssert.Contains(
              subModule,
  ```
  Patch82 (lines 135-138) is the same shape without the `repoRoot` line: its `var subModule = Path.Combine(FindRepoRoot(), "Main", "SubModule.cs");` becomes `var subModule = RepoPaths.ReadSource("Main/SubModule.cs", stripComments: true);`, the `File.Exists` assert goes, and `File.ReadAllText(subModule),` becomes `subModule,`.
- **Rule B (delete the guard)**: delete the null check or `File.Exists` assert and its `Assert.Inconclusive(...)` (and the `return;` inside it) that existed only for that read.
- **Rule C (using)**: add `using TAOM.Tests.Infrastructure;` if the file lacks it.
- **Rule D (helpers)**: delete a private locator or reader helper only when it has no remaining caller in its file (the table says which). Leave `using` directives that become unused.
- **Rule E**: change no assertion, message, expected string or other read. Reads of feature files (for example `FieldCampIoC.cs`, `RefugeIoC.cs`, `HeroRaceIoC.cs`) stay as they are.

| # | File | SubModule.cs reads | IoC.cs reads | Guards to delete | Helper after the edit |
|---|---|---|---|---|---|
| 1 | `Features/AutoResolveDiagnostics/AutoResolveDiagnosticsWiringTests.cs` | 108 | 93 | 94-98 and 109-113 (braces, Inconclusive, `return;`) | delete `ReadProjectSource` (120-131) |
| 2 | `Features/BanditManagement/Patch86HideoutBossFightBindingTests.cs` | 235-237 | | the `File.Exists` assert at 236 | delete `FindRepoRoot` (267-273) |
| 3 | `Features/BannerColorPersistence/BannerTripletOrderingTests.cs` | 57 | | none | keep `FindRepoRoot` (4 other reads) |
| 4 | `Features/BattleLoadDiagnostics/ExitStallDisarmTests.cs` | 107 | | none | keep `FromRepoRoot` (reads a patch file at 114) |
| 5 | `Features/CompanionTactics/SharedMovementOrderPostfixTests.cs` | 56-59 | | the `File.Exists` assert at 58 | keep `FindRepoRoot` (other reads) |
| 6 | `Features/CoopInterop/ResetForUnloadSweepTests.cs` | 80 | | none | keep `FindMainSourceDir` (the declaration scan uses `mainDir`) |
| 7 | `Features/Diplomacy/Patch80KingdomVoteDeadlockBindingTests.cs` | 289-291 plus the inline `File.ReadAllText(subModule)` at 294; and 368 | | the `File.Exists` assert at 291 | delete `FindRepoRoot` (299-305) |
| 8 | `Features/Enlistment/Patch85EnlistedDetachDeferralBindingTests.cs` | 128-131 | | the `File.Exists` assert at 129 | delete `FindRepoRoot` (141 onward) |
| 9 | `Features/FiefManagement/FiefHubCampaignBehaviorTests.cs` | 132 | | 133-134 | keep `ReadProjectSource` (reads the behavior file twice) |
| 10 | `Features/FieldCamp/FieldCampWiringTests.cs` | 179, 189 | | none | keep `ReadSource` |
| 11 | `Features/HeroRace/HeroRaceWiringTests.cs` | 65, 89, 100 | | none | keep `ReadSource` |
| 12 | `Features/HeroRace/RacePersistenceBehaviorTests.cs` | 102 | 103 | 104-105 | keep `ReadProjectSource` (reads the behavior file) |
| 13 | `Features/LordPartyTemplates/Patch88LordPartyTemplateTests.cs` | 161 | | none | delete `FindRepoRoot` (231 onward) |
| 14 | `Features/MapEventGuard/Patch82MapEventObserverInvariantBindingTests.cs` | 135-136 plus the inline `File.ReadAllText(subModule)` at 138 | | the `File.Exists` assert at 136 | delete `FindRepoRoot` (143 onward) |
| 15 | `Features/MapEventGuard/Patch84SiegeAftermathMenuGuardTests.cs` | 201-204 | | the `File.Exists` assert at 202 | delete `FindRepoRoot` (214 onward) |
| 16 | `Features/Messengers/MessengerCampaignBehaviorTests.cs` | 47 | 34 | 35-36 and 48-49 | delete `ReadProjectSource` (125 onward) |
| 17 | `Features/MountDespawn/MountDespawnWiringTests.cs` | 35 | 23 | 24-25 and 36-37 | delete `ReadProjectSource` (73 onward) |
| 18 | `Features/Refuge/RefugeWiringTests.cs` | 60, 70 | | none | keep `ReadSource` |
| 19 | `Features/ReturnToArmy/Patch87ReturnToArmyTests.cs` | 252-255 | | the `File.Exists` assert at 253 | delete `FindRepoRoot` (295 onward) |
| 20 | `Features/SettlementGuards/SettlementGuardsWiringTests.cs` | 49 | 36 | 37-38 and 50-51 | keep `ReadProjectSource` (reads `ManualPatchApplicator.cs` at 64 and 87) |
| 21 | `Features/SiegeDismount/SiegeDismountWiringTests.cs` | 48 | 35 | 36-37 and 49-50 | delete `ReadProjectSource` (81 onward) |
| 22 | `Features/SiegePropDiagnostics/SiegePropDiagnosticsWiringTests.cs` | 33 | 20 | 21-22 and 34-35 | delete `ReadProjectSource` (51-62) |
| 23 | `Features/SignatureStrikes/SignatureStrikesBindingTests.cs` | 206-210 | | 207-208 | delete the `RepoRoot` property (32-33) |
| 24 | `Features/UncapturableHeroes/UncapturableHeroesWiringTests.cs` | 90, 99 | 63, 79 | none | keep `ReadSource` |
| 25 | `Features/WandererAllegiance/WandererAllegianceWiringTests.cs` | 56 | 36 | none | keep `ReadSource` |

Example, row 22 before (`SiegePropDiagnosticsWiringTests.cs:18-23`):

```csharp
    public void MainIoCConfigure_IncludesSiegePropDiagnosticsRegistration()
    {
        var iocSource = ReadProjectSource("Main", "IoC.cs");
        if (iocSource == null)
            Assert.Inconclusive("Main/IoC.cs not found — run from repo root or check working directory");

        StringAssert.Contains(iocSource,
```

After:

```csharp
    public void MainIoCConfigure_IncludesSiegePropDiagnosticsRegistration()
    {
        var iocSource = RepoPaths.ReadSource("Main/IoC.cs", stripComments: true);

        StringAssert.Contains(iocSource,
```

Then write this checker into your session scratchpad as `check_reads_018.py` (Write tool) and run `python <scratchpad>/check_reads_018.py` from the worktree root:

```python
import pathlib, re, sys

FILES = """TAOM.Tests/Features/AutoResolveDiagnostics/AutoResolveDiagnosticsWiringTests.cs
TAOM.Tests/Features/BanditManagement/Patch86HideoutBossFightBindingTests.cs
TAOM.Tests/Features/BannerColorPersistence/BannerTripletOrderingTests.cs
TAOM.Tests/Features/BattleLoadDiagnostics/ExitStallDisarmTests.cs
TAOM.Tests/Features/CompanionTactics/SharedMovementOrderPostfixTests.cs
TAOM.Tests/Features/CoopInterop/ResetForUnloadSweepTests.cs
TAOM.Tests/Features/Diplomacy/Patch80KingdomVoteDeadlockBindingTests.cs
TAOM.Tests/Features/Enlistment/Patch85EnlistedDetachDeferralBindingTests.cs
TAOM.Tests/Features/FiefManagement/FiefHubCampaignBehaviorTests.cs
TAOM.Tests/Features/FieldCamp/FieldCampWiringTests.cs
TAOM.Tests/Features/HeroRace/HeroRaceWiringTests.cs
TAOM.Tests/Features/HeroRace/RacePersistenceBehaviorTests.cs
TAOM.Tests/Features/LordPartyTemplates/Patch88LordPartyTemplateTests.cs
TAOM.Tests/Features/MapEventGuard/Patch82MapEventObserverInvariantBindingTests.cs
TAOM.Tests/Features/MapEventGuard/Patch84SiegeAftermathMenuGuardTests.cs
TAOM.Tests/Features/Messengers/MessengerCampaignBehaviorTests.cs
TAOM.Tests/Features/MountDespawn/MountDespawnWiringTests.cs
TAOM.Tests/Features/Refuge/RefugeWiringTests.cs
TAOM.Tests/Features/ReturnToArmy/Patch87ReturnToArmyTests.cs
TAOM.Tests/Features/SettlementGuards/SettlementGuardsWiringTests.cs
TAOM.Tests/Features/SiegeDismount/SiegeDismountWiringTests.cs
TAOM.Tests/Features/SiegePropDiagnostics/SiegePropDiagnosticsWiringTests.cs
TAOM.Tests/Features/SignatureStrikes/SignatureStrikesBindingTests.cs
TAOM.Tests/Features/UncapturableHeroes/UncapturableHeroesWiringTests.cs
TAOM.Tests/Features/WandererAllegiance/WandererAllegianceWiringTests.cs
TAOM.Tests/Migration/GameModelOverrideBindingTests.cs""".split()

OLD = re.compile(r'(?<![A-Za-z])(SubModule|IoC)\.cs"')
old, new = [], {"Main/SubModule.cs": 0, "Main/IoC.cs": 0}
for f in FILES:
    text = pathlib.Path(f).read_text(encoding="utf-8-sig")
    for k in new:
        new[k] += text.count(f'RepoPaths.ReadSource("{k}", stripComments: true)')
    for i, line in enumerate(text.splitlines(), 1):
        s = line.strip()
        if s.startswith("//") or "RepoPaths.ReadSource(" in s:
            continue
        if OLD.search(s):
            old.append(f"{f}:{i}: {s}")
print("old-style reads:", len(old))
print("\n".join(old))
print("new reads:", new)
sys.exit(1 if old else 0)
```

**Verify**:
- Before your edits (run it once at the start of this step, before touching any of the 25 files): `old-style reads: 41` (31 SubModule reads, 10 IoC reads), `new reads: {'Main/SubModule.cs': 2, 'Main/IoC.cs': 0}`, exit 1. At `4c728dac` the 26 files hold 42 old-style reads (32 SubModule, 10 IoC; re-counted during the re-cut, the same as at `b2e387db`); Step 0.2 already converted the one at `GameModelOverrideBindingTests.cs:55` and added a second `RepoPaths.ReadSource("Main/SubModule.cs", stripComments: true)` in the new `ParkedModels` test, which the checker counts as new reads. If the old-style count is not 41 before you start, STOP.
- After your edits: `old-style reads: 0`, `new reads: {'Main/SubModule.cs': 33, 'Main/IoC.cs': 10}`, exit 0 (31 converted SubModule reads plus the two in `GameModelOverrideBindingTests`).
- Build command → exit 0.
- Tests command → only the two known Armory tests may fail; total = `T0` + 5 (4 `RepoPathsTests` plus `ParkedModels_AreRealModels_ThatSubModuleDoesNotRegister`). If any of the 26 files' tests fails, see STOP conditions.

Then make commit 1 (Git workflow).

### Step 1.1 (RED): Write the runner tests

Create `TAOM.Tests/Composition/ModuleRunnerTests.cs` with exactly this content:

```csharp
using System;
using System.Collections.Generic;
using DryIoc;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;

// DryIoc and NSubstitute both export an `Arg` type (the DischargeConsequenceServiceTests.cs
// precedent). Every `Arg` here is an NSubstitute argument matcher.
using Arg = NSubstitute.Arg;
using TAOM.Composition;
using TAOM.Core.Logging;

namespace TAOM.Tests.Composition;

/// <summary>
/// Pins the feature-module runner's isolation policy: list order, parked modules registered but
/// otherwise skipped, a throwing module logged and skipped for the rest of the session while the
/// next module still runs, save-owning modules failing closed in the steps that decide whether their
/// SyncData runs, categories applied only in their own phase through the kernel's guarded applier,
/// and one fault summary per report point.
/// </summary>
[TestClass]
public class ModuleRunnerTests
{
    private IModLogger _logger = null!;
    private List<string> _calls = null!;

    [TestInitialize]
    public void Setup()
    {
        _logger = Substitute.For<IModLogger>();
        _calls = new List<string>();
    }

    private ModuleRunner Runner(params ITaomFeatureModule[] modules) => new(modules, () => _logger);

    private RecordingModule Module(string id) => new(id, _calls);

    [TestMethod]
    public void RegisterServices_VisitsEveryModuleInListOrder_ParkedIncluded()
    {
        var parked = Module("P");
        parked.StateValue = FeatureState.Parked;
        using var container = new Container();

        Runner(Module("A"), parked, Module("B")).RegisterServices(container);

        CollectionAssert.AreEqual(new[] { "A:register", "P:register", "B:register" }, _calls);
    }

    [TestMethod]
    public void InitializeStatics_SkipsParkedModules()
    {
        var parked = Module("P");
        parked.StateValue = FeatureState.Parked;
        using var container = new Container();

        Runner(Module("A"), parked, Module("B")).InitializeStatics(container);

        CollectionAssert.AreEqual(new[] { "A:statics", "B:statics" }, _calls);
    }

    [TestMethod]
    public void AModuleThatThrows_IsLoggedAndFaulted_AndTheNextModuleStillRuns()
    {
        var a = Module("A");
        a.ThrowIn = "register";
        using var container = new Container();
        var runner = Runner(a, Module("B"));

        runner.RegisterServices(container);

        CollectionAssert.AreEqual(new[] { "A:register", "B:register" }, _calls);
        _logger.Received(1).LogError(Arg.Is<string>(s =>
            s.Contains("[Module] A failed in service registration") && s.Contains("A broke in register")));
        Assert.IsTrue(runner.IsFaulted("A"));
        Assert.IsFalse(runner.IsFaulted("B"));
    }

    [TestMethod]
    public void AFaultedModule_IsSkippedInEveryLaterStep()
    {
        var a = Module("A");
        a.ThrowIn = "register";
        using var container = new Container();
        var runner = Runner(a, Module("B"));
        runner.RegisterServices(container);
        _calls.Clear();

        runner.InitializeStatics(container);
        runner.RunPhase(ApplyPhase.GameInit, _ => true, container);
        runner.Run("campaign start", includeParked: false, failClosed: false, m => _calls.Add(m.Id + ":step"));

        CollectionAssert.AreEqual(new[] { "B:statics", "B:phase:GameInit", "B:step" }, _calls);
    }

    [TestMethod]
    public void ASaveOwningModule_FailsClosed_InAFailClosedStep()
    {
        var a = Module("A");
        a.OwnsSave = true;
        a.ThrowIn = "statics";
        using var container = new Container();
        var runner = Runner(a, Module("B"));

        var ex = Assert.ThrowsException<InvalidOperationException>(() => runner.InitializeStatics(container));

        StringAssert.Contains(ex.Message, "A broke in statics");
        CollectionAssert.AreEqual(new[] { "A:statics" }, _calls, "Nothing after a fail-closed throw may run.");
        _logger.Received(1).LogError(Arg.Is<string>(s => s.Contains("[Module] A failed in static initialisation")));
    }

    [TestMethod]
    public void ASaveOwningModule_IsIsolated_InAFailOpenStep()
    {
        var a = Module("A");
        a.OwnsSave = true;
        a.ThrowIn = "phase";
        using var container = new Container();
        var runner = Runner(a, Module("B"));

        runner.RunPhase(ApplyPhase.MainMenu, _ => true, container);

        CollectionAssert.AreEqual(new[] { "A:phase:MainMenu", "B:phase:MainMenu" }, _calls);
        Assert.IsTrue(runner.IsFaulted("A"));
    }

    [TestMethod]
    public void RunPhase_AppliesOnlyThatPhasesCategories_InOrder_ThenCallsOnPhase()
    {
        var a = Module("A");
        a.Categories.Add(new PatchCategoryDecl("Cat_Load", ApplyPhase.ProcessLoad));
        a.Categories.Add(new PatchCategoryDecl("Cat_Init1", ApplyPhase.GameInit));
        a.Categories.Add(new PatchCategoryDecl("Cat_Init2", ApplyPhase.GameInit));
        using var container = new Container();

        Runner(a).RunPhase(ApplyPhase.GameInit, category => { _calls.Add("apply:" + category); return true; }, container);

        CollectionAssert.AreEqual(new[] { "apply:Cat_Init1", "apply:Cat_Init2", "A:phase:GameInit" }, _calls);
    }

    [TestMethod]
    public void RunPhase_AFailedCategory_DoesNotFaultTheModule()
    {
        var a = Module("A");
        a.Categories.Add(new PatchCategoryDecl("Cat_Init", ApplyPhase.GameInit));
        using var container = new Container();
        var runner = Runner(a);

        runner.RunPhase(ApplyPhase.GameInit, _ => false, container);

        Assert.IsFalse(runner.IsFaulted("A"), "A failed category is the applier's to report, not a module fault.");
        CollectionAssert.AreEqual(new[] { "A:phase:GameInit" }, _calls);
    }

    [TestMethod]
    public void RunPhase_SkipsParkedModules()
    {
        var parked = Module("P");
        parked.StateValue = FeatureState.Parked;
        parked.Categories.Add(new PatchCategoryDecl("Cat_Init", ApplyPhase.GameInit));
        using var container = new Container();

        Runner(parked).RunPhase(ApplyPhase.GameInit, category => { _calls.Add("apply:" + category); return true; }, container);

        Assert.AreEqual(0, _calls.Count);
    }

    [TestMethod]
    public void TakeFaultSummary_IsNull_WhenNothingFailed()
    {
        using var container = new Container();
        var runner = Runner(Module("A"));
        runner.RegisterServices(container);

        Assert.IsNull(runner.TakeFaultSummary());
    }

    [TestMethod]
    public void TakeFaultSummary_NamesEachFaultedModuleWithItsStep_ThenClears()
    {
        var a = Module("A");
        a.ThrowIn = "register";
        var b = Module("B");
        b.ThrowIn = "phase";
        using var container = new Container();
        var runner = Runner(a, b);

        runner.RegisterServices(container);
        runner.RunPhase(ApplyPhase.ProcessLoad, _ => true, container);

        Assert.AreEqual(
            "TAOM: feature modules failed and are off this session: A (service registration), B (ProcessLoad). "
            + "The TAOM log names the cause.",
            runner.TakeFaultSummary());
        Assert.IsNull(runner.TakeFaultSummary());
    }

    [TestMethod]
    public void AThrowingLogger_DoesNotBreakTheStep()
    {
        _logger.When(l => l.LogError(Arg.Any<string>())).Do(_ => throw new InvalidOperationException("log down"));
        var a = Module("A");
        a.ThrowIn = "register";
        using var container = new Container();

        Runner(a, Module("B")).RegisterServices(container);

        CollectionAssert.AreEqual(new[] { "A:register", "B:register" }, _calls);
    }

    [TestMethod]
    public void BaseModule_DefaultsToAnEnabledModuleThatDeclaresNothing()
    {
        var module = new EmptyModule();
        using var container = new Container();

        Assert.AreEqual(FeatureState.Enabled, module.State);
        Assert.IsNull(module.ParkedReason);
        Assert.IsFalse(module.OwnsSaveData);
        Assert.AreEqual(0, module.PatchCategories.Count);
        Assert.AreEqual(0, module.CampaignBehaviors.Count);
        Assert.AreEqual(0, module.GameModels.Count);
        Assert.AreEqual(0, module.MissionBehaviors.Count);
        module.RegisterServices(container);
        module.InitializeStatics(container);
        module.OnPhase(ApplyPhase.GameInit, container);
    }

    private sealed class EmptyModule : TaomFeatureModule
    {
        public override string Id => "Empty";
    }

    private sealed class RecordingModule : TaomFeatureModule
    {
        private readonly string _id;
        private readonly List<string> _calls;

        internal RecordingModule(string id, List<string> calls)
        {
            _id = id;
            _calls = calls;
        }

        internal FeatureState StateValue = FeatureState.Enabled;
        internal bool OwnsSave;
        internal string? ThrowIn;
        internal readonly List<PatchCategoryDecl> Categories = new();

        public override string Id => _id;
        public override FeatureState State => StateValue;
        public override string? ParkedReason => StateValue == FeatureState.Parked ? "test" : null;
        public override bool OwnsSaveData => OwnsSave;
        public override IReadOnlyList<PatchCategoryDecl> PatchCategories => Categories;

        public override void RegisterServices(IRegistrator registrator) => Record("register");
        public override void InitializeStatics(IResolver resolver) => Record("statics");
        public override void OnPhase(ApplyPhase phase, IResolver resolver) => Record("phase:" + phase);

        private void Record(string what)
        {
            _calls.Add(_id + ":" + what);
            if (ThrowIn != null && what.StartsWith(ThrowIn, StringComparison.Ordinal))
                throw new InvalidOperationException(_id + " broke in " + what);
        }
    }
}
```

**Verify (RED)**: filtered tests with `ModuleRunnerTests` → the test build fails with `error CS0234` or `CS0246` for `TAOM.Composition` / `ModuleRunner` (the namespace and types do not exist yet). It must NOT report `error CS0104` (`'Arg' is an ambiguous reference between 'DryIoc.Arg' and 'NSubstitute.Arg'`): DryIoc 4.8.8 exports `DryIoc.Arg`, and the `using Arg = NSubstitute.Arg;` alias above is what prevents it. If CS0104 appears, the alias was dropped; restore it exactly as shown, then re-run.

### Step 1.2 (GREEN): Create the composition types

Create these five files in `Main/Composition/` (Write tool). Keep every type `internal`. Do not write `.PatchCategory(` anywhere.

`Main/Composition/ITaomFeatureModule.cs`:

```csharp
using System.Collections.Generic;
using DryIoc;

namespace TAOM.Composition;

/// <summary>When a feature module's patch categories apply: one value per SubModule hook that applies categories.</summary>
internal enum ApplyPhase
{
    /// <summary>OnSubModuleLoad, once per process.</summary>
    ProcessLoad,

    /// <summary>The first OnBeforeInitialModuleScreenSetAsRoot, once per process.</summary>
    MainMenu,

    /// <summary>The first OnGameInitializationFinished, once per process.</summary>
    GameInit,

    /// <summary>The first OnMissionBehaviorInitialize, once Mission.Current exists, once per process.</summary>
    FirstMission,
}

/// <summary>Compile-time only; never read from MCM (persisted MCM values outlive default flips).</summary>
internal enum FeatureState
{
    Enabled,
    Parked,
}

/// <summary>
/// One feature's complete wiring, owned by the feature's folder instead of SubModule.cs and IoC.cs.
/// The runner visits modules in <see cref="FeatureModules.All"/> order: <see cref="RegisterServices"/>
/// for every module (parked ones included), then <see cref="InitializeStatics"/> after every
/// registration, then per lifecycle phase its categories, <see cref="OnPhase"/>, behaviors, models
/// and mission behaviors. Derive from <see cref="TaomFeatureModule"/>, which supplies empty defaults
/// (net472 has no default interface members).
/// </summary>
internal interface ITaomFeatureModule
{
    /// <summary>The feature name, as in "WandererAllegiance"; used in every log line and test message.</summary>
    string Id { get; }

    FeatureState State { get; }

    /// <summary>Issue references for a parked module; null when enabled.</summary>
    string? ParkedReason { get; }

    /// <summary>True when any of the module's behaviors persists data in SyncData; such a module fails closed.</summary>
    bool OwnsSaveData { get; }

    /// <summary>Container registrations only. IRegistrator has no Resolve, so an eager resolve does not compile.</summary>
    void RegisterServices(IRegistrator registrator);

    /// <summary>Patch-static handshakes, after every module and hand-wired feature has registered.</summary>
    void InitializeStatics(IResolver resolver);

    IReadOnlyList<PatchCategoryDecl> PatchCategories { get; }

    IReadOnlyList<CampaignBehaviorDecl> CampaignBehaviors { get; }

    IReadOnlyList<GameModelDecl> GameModels { get; }

    IReadOnlyList<MissionBehaviorDecl> MissionBehaviors { get; }

    /// <summary>Non-patch side effects for a phase (hotkeys, watchdog starts), after that phase's categories.</summary>
    void OnPhase(ApplyPhase phase, IResolver resolver);
}
```

`Main/Composition/FeatureDecls.cs`:

```csharp
using System;
using DryIoc;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;
using TaleWorlds.MountAndBlade;

namespace TAOM.Composition;

/// <summary>A Harmony patch category a module applies, and the phase it must apply in.</summary>
internal sealed class PatchCategoryDecl
{
    internal PatchCategoryDecl(string category, ApplyPhase phase)
    {
        if (string.IsNullOrWhiteSpace(category))
            throw new ArgumentException("A patch category needs a name.", nameof(category));
        Category = category;
        Phase = phase;
    }

    internal string Category { get; }

    internal ApplyPhase Phase { get; }
}

/// <summary>A campaign behavior a module adds at campaign start, and how to build it.</summary>
internal sealed class CampaignBehaviorDecl
{
    private CampaignBehaviorDecl(Type behaviorType, Func<IResolver, CampaignBehaviorBase> create)
    {
        BehaviorType = behaviorType;
        Create = create;
    }

    internal Type BehaviorType { get; }

    internal Func<IResolver, CampaignBehaviorBase> Create { get; }

    internal static CampaignBehaviorDecl Of<TBehavior>(Func<IResolver, TBehavior> create)
        where TBehavior : CampaignBehaviorBase =>
        new(typeof(TBehavior), resolver => create(resolver));
}

/// <summary>Which game starter a model is added to.</summary>
internal enum ModelTarget
{
    /// <summary>The CampaignGameStarter in OnGameStart, after SandBox's defaults.</summary>
    Campaign,

    /// <summary>The BasicGameStarter Custom Battle hands OnGameStart.</summary>
    CustomBattle,
}

/// <summary>
/// A game model a module adds, keyed by its engine slot. Added through the generic
/// <c>AddModel&lt;TSlot&gt;</c>, which chains the slot's previous model as BaseModel, exactly as the
/// hand-written AddModel calls in SubModule resolve today.
/// </summary>
internal sealed class GameModelDecl
{
    private GameModelDecl(Type slotType, Type modelType, ModelTarget target,
        Func<IResolver, GameModel> create, Action<IGameStarter, GameModel> add)
    {
        SlotType = slotType;
        ModelType = modelType;
        Target = target;
        Create = create;
        Add = add;
    }

    internal Type SlotType { get; }

    internal Type ModelType { get; }

    internal ModelTarget Target { get; }

    internal Func<IResolver, GameModel> Create { get; }

    internal Action<IGameStarter, GameModel> Add { get; }

    internal static GameModelDecl Of<TSlot, TModel>(ModelTarget target, Func<IResolver, TModel> create)
        where TSlot : GameModel
        where TModel : MBGameModel<TSlot> =>
        new(typeof(TSlot), typeof(TModel), target,
            resolver => create(resolver),
            (starter, model) => starter.AddModel<TSlot>((MBGameModel<TSlot>)model));
}

/// <summary>A mission behavior a module adds at every mission start, and how to build it.</summary>
internal sealed class MissionBehaviorDecl
{
    private MissionBehaviorDecl(Type behaviorType, Func<Mission, IResolver, MissionBehavior> create)
    {
        BehaviorType = behaviorType;
        Create = create;
    }

    internal Type BehaviorType { get; }

    internal Func<Mission, IResolver, MissionBehavior> Create { get; }

    internal static MissionBehaviorDecl Of<TBehavior>(Func<Mission, IResolver, TBehavior> create)
        where TBehavior : MissionBehavior =>
        new(typeof(TBehavior), (mission, resolver) => create(mission, resolver));
}
```

`Main/Composition/TaomFeatureModule.cs`:

```csharp
using System;
using System.Collections.Generic;
using DryIoc;

namespace TAOM.Composition;

/// <summary>
/// Empty defaults for <see cref="ITaomFeatureModule"/>: an enabled module that registers, declares
/// and does nothing. A feature overrides only the dimensions it has.
/// </summary>
internal abstract class TaomFeatureModule : ITaomFeatureModule
{
    public abstract string Id { get; }

    public virtual FeatureState State => FeatureState.Enabled;

    public virtual string? ParkedReason => null;

    public virtual bool OwnsSaveData => false;

    public virtual void RegisterServices(IRegistrator registrator) { }

    public virtual void InitializeStatics(IResolver resolver) { }

    public virtual IReadOnlyList<PatchCategoryDecl> PatchCategories => Array.Empty<PatchCategoryDecl>();

    public virtual IReadOnlyList<CampaignBehaviorDecl> CampaignBehaviors => Array.Empty<CampaignBehaviorDecl>();

    public virtual IReadOnlyList<GameModelDecl> GameModels => Array.Empty<GameModelDecl>();

    public virtual IReadOnlyList<MissionBehaviorDecl> MissionBehaviors => Array.Empty<MissionBehaviorDecl>();

    public virtual void OnPhase(ApplyPhase phase, IResolver resolver) { }
}
```

`Main/Composition/ModuleRunner.cs`:

```csharp
using System;
using System.Collections.Generic;
using DryIoc;
using TAOM.Core.Logging;

namespace TAOM.Composition;

/// <summary>
/// Runs one lifecycle step over every feature module in list order, isolating each module. A module
/// that throws is logged, marked faulted and skipped in every later step of the session (half-wired
/// is worse than absent), and the next module still runs. A module that owns save data fails CLOSED
/// instead in the steps that decide whether its SyncData runs (service registration, static
/// initialisation, campaign start): the throw propagates, because a campaign that runs without the
/// behavior persisting its data can lose that data on the next save. Parked modules get only their
/// service registration. Engine-free and unit-tested; the engine-facing loops are in
/// <see cref="FeatureModuleHooks"/>.
/// </summary>
internal sealed class ModuleRunner
{
    private readonly IReadOnlyList<ITaomFeatureModule> _modules;
    private readonly Func<IModLogger?> _logger;
    private readonly HashSet<string> _faulted = new(StringComparer.Ordinal);
    private readonly List<string> _unreported = new();

    internal ModuleRunner(IReadOnlyList<ITaomFeatureModule> modules, Func<IModLogger?> logger)
    {
        _modules = modules ?? throw new ArgumentNullException(nameof(modules));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    internal bool IsFaulted(string moduleId) => _faulted.Contains(moduleId);

    internal void RegisterServices(IRegistrator registrator) =>
        Run("service registration", includeParked: true, failClosed: true, m => m.RegisterServices(registrator));

    internal void InitializeStatics(IResolver resolver) =>
        Run("static initialisation", includeParked: false, failClosed: true, m => m.InitializeStatics(resolver));

    /// <summary>
    /// Applies each module's categories for <paramref name="phase"/> through the kernel's guarded
    /// applier (a failed category is the applier's to log and report, not a module fault), then
    /// calls the module's OnPhase.
    /// </summary>
    internal void RunPhase(ApplyPhase phase, Func<string, bool> tryPatchCategory, IResolver resolver) =>
        Run(phase.ToString(), includeParked: false, failClosed: false, m =>
        {
            foreach (var decl in m.PatchCategories)
            {
                if (decl.Phase == phase)
                    tryPatchCategory(decl.Category);
            }

            m.OnPhase(phase, resolver);
        });

    internal void Run(string step, bool includeParked, bool failClosed, Action<ITaomFeatureModule> action)
    {
        foreach (var module in _modules)
        {
            if (!includeParked && module.State == FeatureState.Parked) continue;
            if (_faulted.Contains(module.Id)) continue;

            try
            {
                action(module);
            }
            catch (Exception ex)
            {
                _faulted.Add(module.Id);
                _unreported.Add($"{module.Id} ({step})");
                Log($"[Module] {module.Id} failed in {step}: {ex}");
                if (failClosed && module.OwnsSaveData)
                    throw;
            }
        }
    }

    /// <summary>
    /// One player-facing line naming every module that faulted since the last call, or null when none
    /// did. Clears the list, so each report point shows only new faults.
    /// </summary>
    internal string? TakeFaultSummary()
    {
        if (_unreported.Count == 0) return null;

        var summary = "TAOM: feature modules failed and are off this session: "
            + string.Join(", ", _unreported) + ". The TAOM log names the cause.";
        _unreported.Clear();
        return summary;
    }

    private void Log(string message)
    {
        try
        {
            _logger()?.LogError(message);
        }
        catch
        {
            // The fault record must never be the thing that breaks the step.
        }
    }
}
```

`Main/Composition/FeatureModules.cs`:

```csharp
namespace TAOM.Composition;

/// <summary>
/// The ordered feature-module list, one line per migrated feature. The runner visits modules in
/// this order in every step, so list order is registration order, behavior add order and
/// mission-behavior add order. Features not migrated yet are still wired by hand in IoC.cs and
/// SubModule.cs, and every module here runs AFTER all of them in each phase. Append; when a module
/// must precede another, say why on its line and pin it with an index test in FeatureModulesTests.
/// </summary>
internal static class FeatureModules
{
    internal static readonly ITaomFeatureModule[] All =
    {
    };
}
```

**Verify (GREEN)**: build command → exit 0. Filtered tests with `ModuleRunnerTests` → 13 passed, 0 failed.

### Step 1.3 (RED): Write the module-list and kernel wiring tests

Create `TAOM.Tests/Composition/FeatureModulesTests.cs` with exactly this content:

```csharp
using System;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Composition;
using TAOM.Tests.Infrastructure;

namespace TAOM.Tests.Composition;

/// <summary>
/// Generic tests over <see cref="FeatureModules.All"/>: they replace per-feature text asserts on
/// SubModule.cs and IoC.cs as features migrate. The two Kernel tests pin the runner calls in the two
/// single-owner files, which are kernel wiring and never migrate.
/// </summary>
[TestClass]
public class FeatureModulesTests
{
    [TestMethod]
    public void EveryModule_HasAUniqueNonEmptyId()
    {
        var ids = FeatureModules.All.Select(m => m.Id).ToList();

        Assert.IsTrue(ids.All(id => !string.IsNullOrWhiteSpace(id)), "A feature module has an empty Id.");
        var duplicates = ids.GroupBy(id => id, StringComparer.Ordinal).Where(g => g.Count() > 1).Select(g => g.Key).ToList();
        Assert.AreEqual(0, duplicates.Count, "Duplicate module ids: " + string.Join(", ", duplicates));
    }

    [TestMethod]
    public void ParkedModules_CarryAReason_AndEnabledModulesDoNot()
    {
        foreach (var module in FeatureModules.All)
        {
            if (module.State == FeatureState.Parked)
                Assert.IsFalse(string.IsNullOrWhiteSpace(module.ParkedReason), $"{module.Id} is parked with no reason; name the issue.");
            else
                Assert.IsNull(module.ParkedReason, $"{module.Id} is enabled but carries a parked reason.");
        }
    }

    [TestMethod]
    public void Kernel_IoCConfigure_RunsTheModuleStepsAfterEveryHandWiredRegistration()
    {
        var code = RepoPaths.ReadSource("Main/IoC.cs", stripComments: true);

        AssertOnceBetween(code, "RegisterUncapturableHeroesFeature(container);",
            "modules.RegisterServices(container);", "_container = container;");
        AssertOnceBetween(code, "CareerSystemIoC.InitializeCalculators(",
            "modules.InitializeStatics(container);", "private static void RegisterCoreServices(");
    }

    [TestMethod]
    public void Kernel_SubModule_CallsEachRunnerHookOnce_AtTheEndOfItsFeatureBlock()
    {
        var code = RepoPaths.ReadSource("Main/SubModule.cs", stripComments: true);

        // OnSubModuleLoad reports nothing (plan 009: no receiver yet), so its runner call is pinned
        // between its last category and the next method, not before a report call.
        AssertOnceBetween(code, "TryPatchCategory(\"Patch42_CastleRecruitment\");",
            "FeatureModuleHooks.RunPhase(ApplyPhase.ProcessLoad, TryPatchCategory);",
            "protected override void OnBeforeInitialModuleScreenSetAsRoot()");
        // Before 009's startup inquiry, so a module category that fails at MainMenu is in it.
        AssertOnceBetween(code, "TryPatchCategory(\"Patch55_BasicTableauRaceGuard\");",
            "FeatureModuleHooks.RunPhase(ApplyPhase.MainMenu, TryPatchCategory);",
            "ReportPatchFailures(\"startup\", persistent: true);");
        AssertOnceBetween(code, "RegisterCampaignLifeBehaviors(campaignStarter);",
            "FeatureModuleHooks.AddGameStartContent(gameStarterObject);", "public override void OnGameLoaded(");
        AssertOnceBetween(code, "TryPatchCategory(\"Patch69_TournamentEndGuard\");",
            "FeatureModuleHooks.RunPhase(ApplyPhase.GameInit, TryPatchCategory);", "ReportPatchFailures(\"game initialization\");");
        AssertOnceBetween(code, "TryPatchCategory(\"Patch_MissionTime_SetMovementOrder\");",
            "FeatureModuleHooks.RunPhase(ApplyPhase.FirstMission, TryPatchCategory);", "ReportPatchFailures(\"mission start\");");
        AssertOnceBetween(code, "new AgentColorStoreCleanupBehavior(colorStore)",
            "FeatureModuleHooks.AddMissionBehaviors(mission, AddTaomBehavior);", "new Features.MissionDiagnostic.Hooks.MissionDiagnosticBehavior(");
    }

    private static void AssertOnceBetween(string code, string after, string call, string before)
    {
        var at = code.IndexOf(call, StringComparison.Ordinal);
        Assert.AreNotEqual(-1, at, $"Missing runner call: {call}");
        Assert.AreEqual(-1, code.IndexOf(call, at + call.Length, StringComparison.Ordinal), $"Runner call appears twice: {call}");

        var afterAt = code.IndexOf(after, StringComparison.Ordinal);
        var beforeAt = code.IndexOf(before, StringComparison.Ordinal);
        Assert.IsTrue(afterAt >= 0 && beforeAt >= 0, $"An anchor around {call} is missing: '{after}' or '{before}'.");
        Assert.IsTrue(afterAt < at && at < beforeAt, $"{call} must sit after '{after}' and before '{before}'.");
    }
}
```

**Verify (RED)**: filtered tests with `FeatureModulesTests` → 2 passed, 2 failed: both `Kernel_*` tests, each with a "Missing runner call" message.

### Step 1.4 (GREEN): Add the engine-facing hooks and the eight kernel lines

Create `Main/Composition/FeatureModuleHooks.cs`:

```csharp
using System;
using System.Collections.Generic;
using DryIoc;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;

namespace TAOM.Composition;

/// <summary>How a report point shows the modules that faulted since the last one.</summary>
internal enum FaultNotice
{
    /// <summary>Nothing receives a notice yet: keep the faults for the main-menu inquiry.</summary>
    Hold,

    /// <summary>
    /// An inquiry: GauntletQueryManager queues it and the initial screen does not clear it, unlike
    /// the chat log, which the initial screen clears after the splash video.
    /// </summary>
    Inquiry,

    /// <summary>A red chat line: in a game the chat log exists and nothing clears it first.</summary>
    ChatLine,
}

/// <summary>
/// The one line each SubModule hook calls: the engine-facing half of <see cref="ModuleRunner"/>.
/// Each method runs the modules for its phase and then reports any module that faulted, in the one
/// notice a player can see at that point (<see cref="NoticeFor"/>; the startup inquiry rule in
/// docs/reviews/lessons/localization-ui.md): faults from IoC.Configure and OnSubModuleLoad wait for
/// the main-menu inquiry, in-game faults get a red line. Nothing here throws, except the runner's
/// deliberate fail-closed rethrow for a save-owning module at campaign start. Every factory runs
/// before anything is handed to the engine, so a module whose factory throws adds nothing.
/// </summary>
internal static class FeatureModuleHooks
{
    internal static void RunPhase(ApplyPhase phase, Func<string, bool> tryPatchCategory)
    {
        var runner = IoC.Modules;
        var resolver = IoC.Resolver;
        if (runner == null || resolver == null) return;

        runner.RunPhase(phase, tryPatchCategory, resolver);
        ReportFaults(runner, NoticeFor(phase));
    }

    /// <summary>
    /// ProcessLoad runs in OnSubModuleLoad, before Native builds the chat log and the inquiry manager,
    /// so its faults (and those of service registration and static initialisation, which run in
    /// IoC.Configure even earlier) are held. MainMenu runs once per process in the first
    /// OnBeforeInitialModuleScreenSetAsRoot, where only an inquiry survives the splash video. GameInit
    /// and FirstMission run inside a game, where the chat log receives a red line.
    /// </summary>
    internal static FaultNotice NoticeFor(ApplyPhase phase) => phase switch
    {
        ApplyPhase.ProcessLoad => FaultNotice.Hold,
        ApplyPhase.MainMenu => FaultNotice.Inquiry,
        _ => FaultNotice.ChatLine,
    };

    /// <summary>
    /// OnGameStart: campaign behaviors and campaign models on a CampaignGameStarter; Custom Battle
    /// models on a BasicGameStarter (the same split RegisterCustomBattleModels makes).
    /// </summary>
    internal static void AddGameStartContent(IGameStarter gameStarter)
    {
        var runner = IoC.Modules;
        var resolver = IoC.Resolver;
        if (runner == null || resolver == null) return;

        if (gameStarter is CampaignGameStarter campaignStarter)
        {
            runner.Run("campaign start", includeParked: false, failClosed: true, module =>
            {
                var behaviors = new List<CampaignBehaviorBase>();
                foreach (var decl in module.CampaignBehaviors)
                    behaviors.Add(decl.Create(resolver));
                var models = CreateModels(module, ModelTarget.Campaign, resolver);

                foreach (var behavior in behaviors)
                    campaignStarter.AddBehavior(behavior);
                foreach (var (decl, model) in models)
                    decl.Add(campaignStarter, model);
            });
        }
        else if (gameStarter is BasicGameStarter basicStarter)
        {
            runner.Run("custom battle start", includeParked: false, failClosed: false, module =>
            {
                foreach (var (decl, model) in CreateModels(module, ModelTarget.CustomBattle, resolver))
                    decl.Add(basicStarter, model);
            });
        }

        // OnGameStart runs during game loading, after Module.OnBeforeGameStart's ClearAllMessages.
        ReportFaults(runner, FaultNotice.ChatLine);
    }

    /// <summary>OnMissionBehaviorInitialize: hands each behavior to SubModule's AddTaomBehavior, which stamps [BattleLoad].</summary>
    internal static void AddMissionBehaviors(Mission mission, Action<MissionBehavior> addTaomBehavior)
    {
        var runner = IoC.Modules;
        var resolver = IoC.Resolver;
        if (runner == null || resolver == null) return;

        runner.Run("mission start", includeParked: false, failClosed: false, module =>
        {
            var behaviors = new List<MissionBehavior>();
            foreach (var decl in module.MissionBehaviors)
                behaviors.Add(decl.Create(mission, resolver));
            foreach (var behavior in behaviors)
                addTaomBehavior(behavior);
        });

        ReportFaults(runner, FaultNotice.ChatLine);
    }

    private static List<(GameModelDecl Decl, GameModel Model)> CreateModels(
        ITaomFeatureModule module, ModelTarget target, IResolver resolver)
    {
        var models = new List<(GameModelDecl Decl, GameModel Model)>();
        foreach (var decl in module.GameModels)
        {
            if (decl.Target == target)
                models.Add((decl, decl.Create(resolver)));
        }

        return models;
    }

    // Hold leaves the runner's list untouched, so the next report point (the main-menu inquiry)
    // still names every earlier fault. The inquiry is built exactly like plan 009's startup report in
    // SubModule.ReportPatchFailures; when both have something to say, GauntletQueryManager queues
    // them one after the other (this one first, because the runner call precedes 009's report).
    private static void ReportFaults(ModuleRunner runner, FaultNotice notice)
    {
        if (notice == FaultNotice.Hold) return;

        var summary = runner.TakeFaultSummary();
        if (summary == null) return;

        try
        {
            if (notice == FaultNotice.Inquiry)
                InformationManager.ShowInquiry(new InquiryData(
                    "TAOM", summary, true, false, "OK", string.Empty, null, null));
            else
                InformationManager.DisplayMessage(new InformationMessage(summary, Colors.Red));
        }
        catch
        {
            // The notice must never break the phase; the [Module] log line already names the fault.
        }
    }
}
```

Then append this test to `TAOM.Tests/Composition/FeatureModulesTests.cs`, inside the class, directly after `Kernel_SubModule_CallsEachRunnerHookOnce_AtTheEndOfItsFeatureBlock` (it pins the startup inquiry rule for module faults, as 009's `SubModuleSource_OnSubModuleLoad_DoesNotReportPatchFailures` does for patch failures):

```csharp
    // Nothing receives a chat line before the initial screen, and the initial screen clears the chat
    // log after the splash video (docs/reviews/lessons/localization-ui.md). Startup faults are held
    // for the main-menu inquiry; in-game phases have a chat log to take a red line.
    [TestMethod]
    public void FaultNotice_HoldsProcessLoad_InquiresAtMainMenu_AndUsesAChatLineInGame()
    {
        Assert.AreEqual(FaultNotice.Hold, FeatureModuleHooks.NoticeFor(ApplyPhase.ProcessLoad));
        Assert.AreEqual(FaultNotice.Inquiry, FeatureModuleHooks.NoticeFor(ApplyPhase.MainMenu));
        Assert.AreEqual(FaultNotice.ChatLine, FeatureModuleHooks.NoticeFor(ApplyPhase.GameInit));
        Assert.AreEqual(FaultNotice.ChatLine, FeatureModuleHooks.NoticeFor(ApplyPhase.FirstMission));
    }
```

It calls only `NoticeFor`, which touches no engine type, so it runs without the game.

Edit `Main/IoC.cs` (Edit tool; nothing else):

1. Add `using TAOM.Composition;` to the using block (for example directly after `using TAOM.Adapters;`).
2. Directly after `    private static IContainer _container;` add:
   ```csharp

       // The feature-module runner (Main/Composition), built by Configure. SubModule's hooks reach it
       // through FeatureModuleHooks. Null only before Configure has run.
       internal static ModuleRunner? Modules { get; private set; }

       // The container as a resolver for feature-module factories. Null before Configure and after Dispose.
       internal static IResolver? Resolver => _container;
   ```
3. Directly after the line `        Features.UncapturableHeroes.UncapturableHeroesIoC.RegisterUncapturableHeroesFeature(container);` and before the blank line and `        _container = container;`, add:
   ```csharp

           // Feature modules (Main/Composition/FeatureModules.cs) register after every hand-wired
           // feature above, so a module sees the container as the last hand-wired feature did. A module
           // that throws is logged and skipped for the session, unless it owns save data.
           var modules = new ModuleRunner(FeatureModules.All, () => container.Resolve<IModLogger>());
           modules.RegisterServices(container);
           Modules = modules;
   ```
4. Directly after the line `        CareerSystemIoC.InitializeCalculators(container.Resolve<Features.CareerSystem.Mutations.IMutationCalculatorRegistry>());` (the last statement of `Configure`), add:
   ```csharp

           // Feature-module statics: after every registration, hand-wired and module alike (the rule
           // the patch-static block above states).
           modules.InitializeStatics(container);
   ```

Edit `Main/SubModule.cs` (Edit tool; nothing else):

1. Add `using TAOM.Composition;` to the using block (for example directly after `using TAOM.Adapters;`).
2. `OnSubModuleLoad`: on a new line directly after `        TryPatchCategory("Patch42_CastleRecruitment");` (`SubModule.cs:619`) and before the comment `        // No ReportPatchFailures here: nothing receives a message yet (see the startup report in`, add `        FeatureModuleHooks.RunPhase(ApplyPhase.ProcessLoad, TryPatchCategory);`. Do not add any report call here: `FeatureModuleHooks` holds ProcessLoad faults itself, and 009's `SubModuleSource_OnSubModuleLoad_DoesNotReportPatchFailures` gate fails on a `ReportPatchFailures(` in this method.
3. `OnBeforeInitialModuleScreenSetAsRoot`: inside `if (!_basicTableauGuardApplied)`, on a new line directly after `            TryPatchCategory("Patch55_BasicTableauRaceGuard");` (`SubModule.cs:641`) and before the comment `            // Reports OnSubModuleLoad's failures and Patch55's together. The earliest a notice can`, add `            FeatureModuleHooks.RunPhase(ApplyPhase.MainMenu, TryPatchCategory);`. It must precede `ReportPatchFailures("startup", persistent: true);` so a module category that fails here is in 009's startup inquiry.
4. `OnGameStart`: after the closing brace of `if (gameStarterObject is CampaignGameStarter campaignStarter) { ... }` and before the method's own closing brace, add (with a blank line above it):
   ```csharp

           // Feature modules last: their behaviors and models follow every hand-wired one (a Custom
           // Battle starter gets only CustomBattle-target models).
           FeatureModuleHooks.AddGameStartContent(gameStarterObject);
   ```
5. `OnGameInitializationFinished`: on the line directly before `        ReportPatchFailures("game initialization");` (`SubModule.cs:1872`, after `        TryPatchCategory("Patch69_TournamentEndGuard");`), add `        FeatureModuleHooks.RunPhase(ApplyPhase.GameInit, TryPatchCategory);`.
6. `OnMissionBehaviorInitialize`, first-mission block: inside `if (!_missionTimePatchesApplied)`, on the line directly before `            ReportPatchFailures("mission start");` (`SubModule.cs:1931`), add `            FeatureModuleHooks.RunPhase(ApplyPhase.FirstMission, TryPatchCategory);`.
7. `OnMissionBehaviorInitialize`, behaviors: directly after
   ```csharp
           if (colorStore != null)
               AddTaomBehavior(new AgentColorStoreCleanupBehavior(colorStore));
   ```
   and before the comment `        // MissionDiagnostic: added LAST so it sees all behaviors added by TAOM AND`, add (blank line above and below):
   ```csharp
           // Feature modules' mission behaviors, after every hand-wired one and before the kernel tail.
           FeatureModuleHooks.AddMissionBehaviors(mission, AddTaomBehavior);
   ```

**Verify (GREEN, parity)**:
- Build command → exit 0, 0 errors.
- `grep -c "FeatureModuleHooks\." Main/SubModule.cs` → `6`.
- `grep -c "modules\.RegisterServices(container);\|modules\.InitializeStatics(container);" Main/IoC.cs` → `2`.
- `grep -rn "\.PatchCategory(" Main/Composition` → no output.
- `grep -c "ReportPatchFailures(" Main/SubModule.cs` → `4` (unchanged: this plan adds no report call).
- Filtered tests with `FeatureModulesTests` → 5 passed (the four from Step 1.3 plus `FaultNotice_HoldsProcessLoad_InquiresAtMainMenu_AndUsesAChatLineInGame`). Filtered tests with `PatchCategoryApplierTests` → 13 passed, 0 failed (plan 009's source gates still see exactly one direct `.PatchCategory(` call, no report in `OnSubModuleLoad` and the startup inquiry in `OnBeforeInitialModuleScreenSetAsRoot`).
- Tests command → only the two known Armory tests may fail; total = `T0` + 5 + 18 (13 `ModuleRunnerTests`, 5 `FeatureModulesTests`). With an empty module list the runtime behaviour is unchanged; the full suite is the parity proof.

Then make commit 2.

### Step 2.1: Add the generic declaration tests

Append these members to `FeatureModulesTests` (inside the class, after `ParkedModules_CarryAReason_AndEnabledModulesDoNot`), and add `using System.Collections.Generic;`, `using System.Reflection;`, `using System.Text.RegularExpressions;` and `using TaleWorlds.CampaignSystem;` to its usings:

```csharp
    [TestMethod]
    public void EveryDeclaredCampaignBehavior_IsDeclaredOnce_AndSubModuleNoLongerAddsIt()
    {
        var declared = FeatureModules.All
            .SelectMany(m => m.CampaignBehaviors.Select(d => (Module: m.Id, Type: d.BehaviorType))).ToList();

        AssertDeclaredOnce(declared.Select(d => d.Type.FullName!), "campaign behavior");
        AssertNotHandWiredToo(declared, "added");
    }

    [TestMethod]
    public void EveryDeclaredMissionBehavior_IsDeclaredOnce_AndSubModuleNoLongerAddsIt()
    {
        var declared = FeatureModules.All
            .SelectMany(m => m.MissionBehaviors.Select(d => (Module: m.Id, Type: d.BehaviorType))).ToList();

        AssertDeclaredOnce(declared.Select(d => d.Type.FullName!), "mission behavior");
        AssertNotHandWiredToo(declared, "added");
    }

    [TestMethod]
    public void EveryDeclaredGameModel_SlotIsDeclaredOncePerTarget_AndSubModuleNoLongerAddsIt()
    {
        var declared = FeatureModules.All.SelectMany(m => m.GameModels.Select(d => (Module: m.Id, Decl: d))).ToList();

        // One engine model per slot: a second AddModel for the same slot silently shadows the first.
        AssertDeclaredOnce(declared.Select(d => d.Decl.Target + ":" + d.Decl.SlotType.FullName), "model slot");
        AssertNotHandWiredToo(declared.Select(d => (d.Module, Type: d.Decl.ModelType)).ToList(), "registered");
    }

    [TestMethod]
    public void EveryDeclaredPatchCategory_IsDeclaredOnce_AndSubModuleNoLongerAppliesIt()
    {
        var declared = FeatureModules.All.SelectMany(m => m.PatchCategories.Select(d => (Module: m.Id, d.Category))).ToList();
        var subModule = RepoPaths.ReadSource("Main/SubModule.cs", stripComments: true);

        AssertDeclaredOnce(declared.Select(d => d.Category), "patch category");
        foreach (var (module, category) in declared)
        {
            Assert.IsFalse(subModule.Contains("\"" + category + "\""),
                $"{category} is declared by the {module} module AND still applied in SubModule.cs: Harmony would "
                + "apply it twice (duplicated prefixes and postfixes). Delete the SubModule line.");
        }
    }

    [TestMethod]
    public void ModulesWhoseBehaviorsPersistData_DeclareOwnsSaveData()
    {
        foreach (var module in FeatureModules.All)
        {
            foreach (var decl in module.CampaignBehaviors)
            {
                var syncData = decl.BehaviorType.GetMethod("SyncData",
                    BindingFlags.Public | BindingFlags.Instance, null, new[] { typeof(IDataStore) }, null);
                var il = syncData?.GetMethodBody()?.GetILAsByteArray();

                // An empty override compiles to "nop; ret" (2 bytes, Debug) or "ret" (1 byte, Release).
                if (il != null && il.Length > 2)
                    Assert.IsTrue(module.OwnsSaveData,
                        $"{decl.BehaviorType.Name} persists data in SyncData, so the {module.Id} module must set OwnsSaveData "
                        + "(it then fails closed instead of running a campaign without its persistence).");
            }
        }
    }

    private static void AssertDeclaredOnce(IEnumerable<string> keys, string what)
    {
        var duplicates = keys.GroupBy(k => k, StringComparer.Ordinal).Where(g => g.Count() > 1).Select(g => g.Key).ToList();
        Assert.AreEqual(0, duplicates.Count, $"Declared more than once ({what}): " + string.Join(", ", duplicates));
    }

    // Transitional, until the last feature migrates: a type a module declares must not also be built
    // by hand in SubModule.cs ("new X(" or "Resolve<...X>()"), or the engine gets it twice.
    private static void AssertNotHandWiredToo(IReadOnlyList<(string Module, Type Type)> declared, string verb)
    {
        var subModule = RepoPaths.ReadSource("Main/SubModule.cs", stripComments: true);
        foreach (var (module, type) in declared)
        {
            var name = Regex.Escape(type.Name);
            var handWired = Regex.IsMatch(subModule,
                @"new\s+(?:\w+\.)*" + name + @"\s*\(|Resolve<(?:\w+\.)*" + name + @">\s*\(");
            Assert.IsFalse(handWired,
                $"{type.Name} is declared by the {module} module AND still {verb} by hand in SubModule.cs. Delete the SubModule line.");
        }
    }
```

**Verify**: build command → exit 0. Filtered tests with `FeatureModulesTests` → 10 passed (the five new ones pass trivially on the empty list).

### Step 2.2 (RED): Convert the pilot's text asserts

In `TAOM.Tests/Features/WandererAllegiance/WandererAllegianceWiringTests.cs`:

1. Delete the two tests `IoC_RegistersTheFeature` and `SubModule_AddsTheDialogBehavior` (the whole methods, including their `[TestMethod]` lines).
2. Add these usings: `using DryIoc;`, `using NSubstitute;`, `using TAOM.Composition;`, `using TAOM.Core.Infrastructure;`, `using TAOM.Core.Logging;`, `using TAOM.Features.Execution;`, `using TAOM.Features.NamedCompanions;`, `using TAOM.Features.WandererAllegiance;`, `using TAOM.Features.WandererAllegiance.Hooks;` (keep the existing ones, including `using TAOM.Tests.Infrastructure;` from Step 0.3).
3. Add these three tests where the two deleted ones were:
   ```csharp
       [TestMethod]
       public void FeatureModules_ListTheWandererAllegianceModuleOnce()
       {
           Assert.AreEqual(1, FeatureModules.All.OfType<WandererAllegianceModule>().Count(),
               "WandererAllegianceModule must be listed exactly once in Main/Composition/FeatureModules.cs, or the "
               + "refusal lines are never registered (or registered twice).");
       }

       [TestMethod]
       public void IoC_NoLongerRegistersTheFeatureByHand()
       {
           var src = RepoPaths.ReadSource("Main/IoC.cs", stripComments: true);

           Assert.IsFalse(src.Contains("RegisterWandererAllegianceFeature"),
               "Main/IoC.cs registers WandererAllegiance by hand AND through its module: every service gets a second "
               + "default registration and Resolve throws at campaign start.");
       }

       [TestMethod]
       public void Module_RegistersTheServiceGraph_AndItsBehaviorDeclResolvesTheSingleton()
       {
           using var container = new Container();
           var paths = Substitute.For<IPathService>();
           paths.ModuleDataPath.Returns(Path.Combine(Path.GetTempPath(), "taom-plan018-" + Guid.NewGuid().ToString("N")));
           container.RegisterInstance(paths);
           container.RegisterInstance(Substitute.For<IModLogger>());
           container.RegisterInstance(Substitute.For<IAlignmentService>());
           container.RegisterInstance(Substitute.For<INamedCompanionConfigProvider>());
           var module = new WandererAllegianceModule();

           module.RegisterServices(container);

           Assert.AreEqual(1, module.CampaignBehaviors.Count);
           var decl = module.CampaignBehaviors[0];
           Assert.AreEqual(typeof(WandererAllegianceDialogBehavior), decl.BehaviorType);
           var behavior = decl.Create(container);
           Assert.IsInstanceOfType(behavior, typeof(WandererAllegianceDialogBehavior));
           Assert.AreSame(behavior, decl.Create(container),
               "Parity: the behavior stays a container singleton, as it was when SubModule resolved it.");
       }
   ```
   The file already has `using System;`, `using System.IO;` and `using System.Linq;`.

**Verify (RED)**: filtered tests with `WandererAllegianceWiringTests` → the test build fails with `error CS0246` for `WandererAllegianceModule`.

### Step 2.3 (GREEN for the build, RED for the double-wiring guards): Create the module

1. `Main/Features/WandererAllegiance/WandererAllegianceIoC.cs`: change only the parameter type, `public static void RegisterWandererAllegianceFeature(IContainer container)` → `public static void RegisterWandererAllegianceFeature(IRegistrator container)`. Keep the name `container` (the `IoCRegistrationDisciplineTests` scan reads `container.Resolve` inside register bodies). In its class summary, the fragment to replace spans two lines (lines 7-8 at `4c728dac`; match both lines exactly, as one Edit):
   ```csharp
   /// and resolved by <c>SubModule.OnGameStart</c> for <c>AddBehavior</c> (the FieldCommission
   /// precedent). Depends on <c>IAlignmentService</c> (ExecutionIoC) and
   ```
   Replace those two lines with:
   ```csharp
   /// and resolved by <see cref="WandererAllegianceModule"/>'s behavior decl at campaign start.
   /// Depends on <c>IAlignmentService</c> (ExecutionIoC) and
   ```
   Verify: `grep -c "FieldCommission" Main/Features/WandererAllegiance/WandererAllegianceIoC.cs` prints `0` and `grep -c "WandererAllegianceModule" Main/Features/WandererAllegiance/WandererAllegianceIoC.cs` prints `1`. If the first prints anything else, the Edit did not match; STOP and re-read the file.
2. Create `Main/Features/WandererAllegiance/WandererAllegianceModule.cs`:
   ```csharp
   using System.Collections.Generic;
   using DryIoc;
   using TAOM.Composition;

   namespace TAOM.Features.WandererAllegiance;

   /// <summary>
   /// Wanderer Allegiance (#575) as a feature module, the first feature moved off SubModule.cs and
   /// IoC.cs. Two dimensions only: the service graph and one stateless dialog behavior. No patch, no
   /// model, no mission behavior, and the behavior's SyncData is empty, so the module owns no save
   /// data. The behavior stays a container singleton, as SubModule resolved it before. Adding it after
   /// every hand-wired behavior is order-free: its two lines are the only TAOM lines on companion_hire
   /// and outrank vanilla's reply by priority (110 over 100), and LotrIssueSuppression.SuppressAll
   /// removes only vanilla issue types.
   /// </summary>
   internal sealed class WandererAllegianceModule : TaomFeatureModule
   {
       private static readonly CampaignBehaviorDecl[] Behaviors =
       {
           CampaignBehaviorDecl.Of(resolver => resolver.Resolve<Hooks.WandererAllegianceDialogBehavior>()),
       };

       public override string Id => "WandererAllegiance";

       public override void RegisterServices(IRegistrator registrator) =>
           WandererAllegianceIoC.RegisterWandererAllegianceFeature(registrator);

       public override IReadOnlyList<CampaignBehaviorDecl> CampaignBehaviors => Behaviors;
   }
   ```
3. `Main/Composition/FeatureModules.cs`: put one line inside the array initializer:
   ```csharp
           new Features.WandererAllegiance.WandererAllegianceModule(),
   ```

**Verify**:
- Build command → exit 0.
- Filtered tests with `WandererAllegianceWiringTests` → 1 failed, `IoC_NoLongerRegistersTheFeatureByHand`; the other six pass.
- Filtered tests with `FeatureModulesTests` → 1 failed (the other nine pass), `EveryDeclaredCampaignBehavior_IsDeclaredOnce_AndSubModuleNoLongerAddsIt`, naming `WandererAllegianceDialogBehavior` and the `WandererAllegiance` module. These two failures are the guards catching a migration that has not deleted its hand wiring yet.

### Step 2.4 (GREEN): Delete the hand wiring

1. `Main/IoC.cs`: delete the whole line `        Features.WandererAllegiance.WandererAllegianceIoC.RegisterWandererAllegianceFeature(container);` (line 109 at `4c728dac`).
2. `Main/SubModule.cs`, in `RegisterCampaignLifeBehaviors`: delete the three comment lines that start `        // WandererAllegiance (#575): a wanderer refuses to be hired across the Free/Evil line. Two`, the line `        campaignStarter.AddBehavior(IoC.Resolve<Features.WandererAllegiance.Hooks.WandererAllegianceDialogBehavior>());`, and one of the two blank lines that then sit together, so exactly one blank line separates the AlignmentDesertion block from the `// EliteEmissary` comment.

**Verify**:
- Build command → exit 0.
- `grep -c "WandererAllegiance" Main/SubModule.cs` → `0`; `grep -c "WandererAllegiance" Main/IoC.cs` → `0`.
- `grep -c "new Features.WandererAllegiance.WandererAllegianceModule()" Main/Composition/FeatureModules.cs` → `1`.
- `grep -rn "RegisterWandererAllegianceFeature" Main --include=*.cs` → exactly two hits, the definition in `WandererAllegianceIoC.cs` and the call in `WandererAllegianceModule.cs` (plain `grep`, because `git grep` does not see the still-untracked module file).
- Filtered tests with `WandererAllegianceWiringTests` → 7 passed. Filtered tests with `FeatureModulesTests` → 10 passed.
- Rerun `python <scratchpad>/check_reads_018.py` → `old-style reads: 0`, `new reads: {'Main/SubModule.cs': 32, 'Main/IoC.cs': 10}` (Step 2.2 deleted one SubModule read and one IoC read and added one IoC read).

### Step 2.5: Update the feature doc

In `docs/features/wanderer-allegiance.md` (no em or en dashes in anything you write):

Each block below holds the exact new line (the fence is not part of the text).

1. Replace the table row that begins with the `WandererAllegianceIoC.cs` path (line 179 at `4c728dac`, ending "called from `Main/IoC.cs` after MarriageAlignment") with:
   ```markdown
   | `Main/Features/WandererAllegiance/WandererAllegianceIoC.cs` | DryIoc registration, called by `WandererAllegianceModule.RegisterServices` |
   ```
2. Replace the table row that begins with the `Main/SubModule.cs` (`OnGameStart`) path (line 180, "beside AlignmentDesertion") with:
   ```markdown
   | `Main/Features/WandererAllegiance/WandererAllegianceModule.cs` | The feature module, listed in `Main/Composition/FeatureModules.cs`: registers the services and declares the dialog behavior, which the module runner adds at campaign start after every hand-wired behavior |
   ```
3. Replace the list item that begins `- ` + `WandererAllegianceWiringTests` + `:` (line 199) with:
   ```markdown
   - `WandererAllegianceWiringTests`: the module is listed once in `FeatureModules.All`, `IoC.cs` no longer registers the feature by hand, the module's service graph resolves its behavior (still a container singleton), both lines sit on `companion_hire`, return to `lord_pretalk`, and pass a priority above 100, and both string ids are registered for translation. `FeatureModulesTests` checks that no module-declared type is also wired by hand in `SubModule.cs`.
   ```

**Verify**: `python tools/lint_docs.py` → exit 0, 0 dead links. `python tools/lint_docs.py --fail-on-drift` → exit 0. `grep -c "beside AlignmentDesertion" docs/features/wanderer-allegiance.md` → `0`. `grep -c "WandererAllegianceModule" docs/features/wanderer-allegiance.md` → `2`.

### Step 2.6: Final verification and commit

**Verify**:
- Build command → exit 0, 0 errors.
- Tests command → only `TheElkItem_DeclaresTheScaleTheReachIsTunedFor` and `AnimaliaActionSets_BindOnlyHorseActions_ToClipsThatExist` may fail; total = `T0` + 29 (Step 0: +5, Step 1: +18, Step 2: +6, which is five generic tests plus three pilot tests minus the two deleted).
- `python tools/validate_moduledata.py` → 0 errors.
- `git status --porcelain` → only in-scope paths. Build and test output (`bin/`, `obj/`, `TestResults/`) is gitignored; if any other path appears, STOP and report it rather than deleting it.

Then make commit 3. Do not push.

## Test plan

| Where | Test | Covers |
|---|---|---|
| `TAOM.Tests/Infrastructure/RepoPathsTests.cs` | `ReadSource_ReturnsTheFileWithLfLineEndingsOnly` | CRLF to LF |
| same | `ReadSource_AcceptsEitherPathSeparator` | `/` and `\` |
| same | `ReadSource_MissingFile_FailsTheTestInsteadOfSkippingIt` | Fail, never Inconclusive |
| same | `StripComments_BlanksLineAndBlockComments_KeepingLengthAndLineBreaks` | a commented-out registration disappears; offsets survive |
| `TAOM.Tests/Migration/GameModelOverrideBindingTests.cs` | `EveryTaomGameModel_IsRegistered_InSubModule` (changed) | RED run proves the false pass; GREEN with the parked allowlist |
| same | `ParkedModels_AreRealModels_ThatSubModuleDoesNotRegister` (new) | the allowlist cannot go stale in either direction |
| 25 other files | unchanged assertions on the comment-stripped view | the comment-shaped hole is closed for every wiring assert |
| `TAOM.Tests/Composition/ModuleRunnerTests.cs` | 13 tests (Step 1.1) | list order; parked (registered, otherwise skipped); isolation; fault persists; save owners fail closed in a fail-closed step and are isolated in a fail-open one; category phase filter and order; failed category not a fault; summary text and clearing; throwing logger; base defaults |
| `TAOM.Tests/Composition/FeatureModulesTests.cs` | 10 tests (Steps 1.3, 1.4, 2.1) | unique ids; parked reasons; kernel runner calls once each between their anchors in `IoC.cs` and `SubModule.cs`; startup module faults held for the main-menu inquiry, in-game faults a red line (`NoticeFor`); each behavior, mission behavior, model slot and category declared once and not also hand-wired; save-persisting behaviors force `OwnsSaveData` |
| `TAOM.Tests/Features/WandererAllegiance/WandererAllegianceWiringTests.cs` | 3 new, 2 removed | listed once; not hand-registered in IoC; module handshake against a real DryIoc container with fakes, singleton parity |

Structural pattern: `ModuleRunnerTests` follows `TAOM.Tests/Core/Domain/RaceManagerTests.cs` for NSubstitute logger assertions; the handshake follows `TAOM.Tests/Features/AutoResolveDiagnostics/AutoResolveDiagnosticsWiringTests.cs`.

Structurally untestable here (name them in the `Not-tested:` trailers): `FeatureModuleHooks` against a live engine (a real `CampaignGameStarter`, `BasicGameStarter`, `Mission` and `InformationManager`, including whether the main-menu fault inquiry and the in-game red line actually appear; only the phase-to-notice mapping is unit-tested), and the in-game refusal dialogue after the move. Owed by whoever deploys this: start a campaign with a Free-aligned player, talk to an Evil-culture wanderer, confirm the refusal line; the log must show no `[Module]` line.

## Done criteria

ALL must hold, run from the worktree root:

- [ ] `dotnet build Main/TAOM.csproj -p:DisableModuleCopy=true -p:ModuleId=` exits 0 with 0 errors
- [ ] `dotnet test TAOM.Tests -p:DisableModuleCopy=true -p:ModuleId=`: the only failures are the two named Armory tests; total is `T0` + 29
- [ ] `dotnet test TAOM.Tests -p:DisableModuleCopy=true -p:ModuleId= --filter "FullyQualifiedName~TAOM.Tests.Composition"` → 23 passed, 0 failed
- [ ] `python <scratchpad>/check_reads_018.py` exits 0 and prints `new reads: {'Main/SubModule.cs': 32, 'Main/IoC.cs': 10}`
- [ ] `grep -c "FeatureModuleHooks\." Main/SubModule.cs` → `6`
- [ ] `grep -c "WandererAllegiance" Main/SubModule.cs Main/IoC.cs` → `Main/SubModule.cs:0` and `Main/IoC.cs:0`
- [ ] `grep -rn "\.PatchCategory(" Main/Composition Main/Features/WandererAllegiance` → no output
- [ ] `grep -rln "#region\|\[Obsolete\|#if DEBUG" Main/Composition Main/Features/WandererAllegiance/WandererAllegianceModule.cs` → no output
- [ ] `python tools/lint_docs.py` exits 0
- [ ] `git status --porcelain` prints nothing after the three commits; `git diff --name-only 4c728dac..HEAD` lists only in-scope paths (no `.claude/`, `CHANGELOG.md` or `plans/` path); `git log --format=%B -3` shows no `Co-Authored-By`

## STOP conditions

Stop and report back (do not improvise) if:

- The precondition fails: `HEAD` is not `4c728dac`, `Main/PatchCategoryApplier.cs` is missing, `ReportPatchFailures(` does not count 4, `ReportPatchFailures("startup", persistent: true);` does not count 1, or `TryPatchCategory` is not a `private bool TryPatchCategory(string category)` in `SubModule.cs` (the worktree is not at plan 009's reviewed tip, or 009 changed again; every SubModule anchor in this plan depends on it).
- The drift check shows any change beyond plan 009's three commits on these paths (listed at the top), or a "Current state" excerpt does not match the live code.
- `git worktree add` fails because `plan/018-composition-root` or `E:/repos/wt-plan-018` already exists.
- The checker's first run (start of Step 0.3, after Step 0.2) prints a count other than 41 old-style reads (a test was added, removed or rewritten since planning).
- The Step 0.2 RED run is Inconclusive (game assemblies not loaded on this machine) or lists any model other than `TAOM.Features.NavalTravel.Models.TaomPartyNavigationModel`.
- Any test in the 26 files fails after it moves to the comment-stripped view: that test asserts on text that lives only in a comment. Report the test and the asserted string; do not switch the file back to raw text.
- A `Main/SubModule.cs` or `Main/IoC.cs` anchor named in Step 1.4 or 2.4 is missing or appears more than once.
- A `PatchCategoryApplierTests` test fails after Step 1.4 (009's startup-report gates or its one-direct-call gate): the kernel lines landed in the wrong place. Do not edit that test file.
- The `FaultNotice_...` test fails with a `TypeLoadException` or `FileNotFoundException` for a TaleWorlds assembly (the test assumes `FeatureModuleHooks.NoticeFor` runs without the game, as `CampaignBehaviorBase` subclasses already do in this suite).
- Step 1.4's full suite shows any failure besides the two Armory tests (parity is broken with an empty list).
- `grep -rn "RegisterWandererAllegianceFeature" Main TAOM.Tests --include=*.cs` at the start of Step 2 finds a call other than the one in `Main/IoC.cs` (moving it would drop that caller's registration); the definition and the text asserts in `WandererAllegianceWiringTests.cs` are expected hits.
- `TAOM.Tests/TAOM.Tests.csproj` or `Directory.Build.props` contains `PathMap` or `ContinuousIntegrationBuild` (the `RepoPaths` locator assumption no longer holds).
- The build reports `CS8701` or any error in the new files that the code in this plan does not explain, or DryIoc is no longer `4.8.8` (`Main/TAOM.csproj`).
- The fix seems to need `Main/TAOM.csproj`, `TAOM.Tests/TAOM.Tests.csproj`, `Directory.Build.props`, `CHANGELOG.md`, any `.claude/` file, or any `SubModule.cs`/`IoC.cs` edit not listed in Scope.
- Any hook denies a commit (for example `check-changelog-changed.sh`, the commit-subject version check, or the doc-drift gate). Report the deny message verbatim; do not bypass it.
- A step's verification fails twice after a reasonable fix attempt.

## Maintenance notes

- **What changes for the next feature author**: a feature can now live entirely in its folder. Add `XModule.cs` beside the feature (derive from `TaomFeatureModule`, override only the dimensions it has), append one line to `Main/Composition/FeatureModules.cs`, then only DELETE that feature's lines from `SubModule.cs` and `IoC.cs`; the `FeatureModulesTests` double-wiring guards go red until the deletion is done. Moving to the end-of-phase loop changes the feature's position: check it against the ordering-constraints table in this plan (copy it into the next plan) and, before the first gameplay feature with patches moves, run the multi-patched-method script the design calls for.
- **Suggested migration order** (from the design): the remaining pilots with an existing text test (SiegePropDiagnostics, ReturnToArmy), then the diagnostics features (fail-open by design), then gameplay leaves, then the constrained cores last (Enlistment, FieldCommission, CareerSystem, HeroRace, the creature mounts, LotrIssues). Roughly 20 small commits; each is S with build, full suite and, for gameplay features, a two-campaign smoke.
- **What a reviewer should probe** (the orchestrator runs `/deep-review` before merging): (1) the fail-closed rethrow is limited to `OwnsSaveData` modules in the three named steps; (2) `FeatureModuleHooks` builds every behavior and model before adding any; (3) `IoC.Modules` is assigned after `RegisterServices` and before `_container`, and `InitializeStatics` is the last statement of `Configure`; (4) the six SubModule calls sit exactly at their anchors (the kernel test pins this), and the ProcessLoad and MainMenu calls sit before 009's startup inquiry so module category failures reach it; (5) `GameModelDecl.Of` uses the generic `AddModel<TSlot>`, which chains `BaseModel` like today's calls; (6) the 26 test conversions changed no assertion; (7) no module fault is shown with `DisplayMessage` before a game exists: ProcessLoad and the two `IoC.Configure` steps hold, MainMenu shows an inquiry (the startup inquiry rule), and a held fault still reaches that inquiry (`Hold` never calls `TakeFaultSummary`).
- **Deferred on purpose**:
  - `ResetForUnload` in the contract: COMP-05 is answered (Mike, 2026-09-24, decision 22 in `_audit/2026-09-23-opus/DECISIONS.md`): nothing reloads TAOM in-process, so the contract gets no `ResetForUnload`.
  - **What "fail closed" means at campaign start** (decision 48): the runner's rethrow is swallowed by `Patch37_CrashReport`'s finalizer on `Module.OnApplicationTick` while crash capture is on, and `GameLoadingState.OnTick` then re-runs the loading step next tick (review of this plan, row 2; in-game end state UNVERIFIED). No module owns save data yet, so this is deliberately undecided. **Precondition for migrating the first `OwnsSaveData` module:** settle it first (a notice and a return to the main menu, or a Patch37 exemption so the load stops at a crash report), with an in-game check of the chosen end state.
  - Behaviors and models as `Reuse.Transient` (COMP-02): a separate, per-behavior change.
  - A reflection test that every declared category exists as a `[HarmonyPatchCategory]` literal in the assembly, and that every `GameModel`/`CampaignBehaviorBase` subclass is declared by exactly one module or a named allowlist: add them with the first module that declares a category, model or behavior they would check.
  - `.claude/rules/gamemodels.md:40` (rule 7 tells authors to write `new TaomXxxModel(` because the binding test greps it): update it when the first model moves into a module.
  - The last migration step deletes the transitional "not also hand-wired" branches, the per-feature `Register*Feature` calls in `IoC.cs`, and `ManualPatchApplicator` (its three features declare their manual patches through `OnPhase(GameInit)`).
  - An ADR recording the composition-root decision (the orchestrator, through `/new-adr`), a CHANGELOG entry, the GitHub issue, and the stale FieldCamp and Patch25 comments (COMP-06).
  - The wider test-locator collapse (TEST-L5-03: 47 source scrapers and 28 `FindRepoRoot` copies onto `RepoPaths`): this plan moved only the 26 files that read `SubModule.cs` or `IoC.cs`. Plan 009's `PatchCategoryApplierTests` (its own `CommentPattern`) and `709649c3`'s `AnimaliaWiringTests` and `MonsterSizeWiringTests` (raw reads, on `bannerlord-1.5.x` only) are candidates for `RepoPaths.ReadSource` in that pass.
  - One merged startup inquiry for patch failures and module faults: it needs `SubModule.ReportPatchFailures` (plan 009's helper, out of scope here) to take the runner's summary. Until then a startup with both shows two queued inquiries.
