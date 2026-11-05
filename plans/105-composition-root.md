# Plan 105: Add the plan 018 feature-module runner and move WandererAllegiance into the first module

> **Executor instructions**: Follow this plan step by step. Run every verification command and
> confirm the expected result before moving to the next step. If anything in "STOP conditions"
> occurs, stop and report; do not improvise. Do not edit `plans/README.md`: every WP branch shares
> it, so the integrator records this plan's status and test count when merging. Put the numbers
> listed in Step 12's hand-off report in your final message instead.
>
> **Drift check (run first, Step 0.2)**:
> `git diff --stat 1b00d1acd..HEAD -- Main/IoC.cs Main/SubModule.cs Main/Composition Main/Features/WandererAllegiance TAOM.Tests/Composition TAOM.Tests/Features/WandererAllegiance docs/features/wanderer-allegiance.md docs/features/composition-root.md`
> Earlier WPs of this programme are expected to have changed `Main/SubModule.cs` and
> `WandererAllegianceWiringTests.cs` (WP104). Compare the live files against "Current state" and
> STOP only on a real mismatch (an anchor in Step 0.4 missing or duplicated).

## Status

- **Priority**: P2; **Effort**: M (three commits, mostly transplanted code; one new doc)
- **Risk**: MED (both single-owner files; behaviour-neutral with an empty list; the pilot's
  behavior moves to the end of the add order)
- **Depends on**: WP104 (`plans/104-guarded-patch-categories.md`: plan 009's `TryPatchCategory`
  and localized `ReportPatchFailures`, `TAOM.Tests/Infrastructure/RepoPaths.cs`, `a0db0558b`)
- **Category**: tech-debt (architecture); **Executor**: laptop
- **Wave / lane**: wave 0, code lane (WP104 then WP105), beside the harness lane (WP102, WP103)
- **Decisions applied**: D2 (adopt plan 018, port end states), D1 (no plan stages `CHANGELOG.md`),
  D8 (`v2.0.28` subjects), D9 (no CI; gates run locally), D10 (ordinary session), D19 (git through
  the Bash tool only), D7 (no co-op client composition)
- **Planned at**: 1.4.x `origin/bannerlord-1.4.5` `1b00d1acd`; 1.5.x `origin/bannerlord-1.5.x`
  `e45f6df2c`; 2026-11-04
- **Issue**: Port of #662 (1.5.x plan 018). Pilot feature: #575 (Wanderer Allegiance). The
  porting-guide doc is new work under the same reference.

## Why this matters

The 1.5.x line wires 82 features through feature modules (`Main/Composition`, plan 018; 82
`TaomFeatureModule` subclasses and 82 `FeatureModules.All` entries at `e45f6df2c`). The 1.4.x
line has no runner, so every 1.5.x module this programme ports would otherwise be translated into
hand wiring: the D1 verification report counts 17 such modules, about 49 edit sites in
`Main/IoC.cs` and `Main/SubModule.cs`, 10 patch files and 25 test files to rewrite. With the runner
each arrives as its `XModule.cs` plus one `FeatureModules.All` line (decision D2). The runner also
isolates faults: on v1.4.8 a throw out of `OnMissionBehaviorInitialize` is swallowed by
SaveShield's MISSION-INIT finalizer and the mission loads again in a loop (the same symptom as the
infinite loading screens the maintainer has seen on 1.4.8), while a module's mission-behavior
factory runs inside the runner's try and is switched off for the session instead. This WP lands
the runner with an empty list (no behaviour change), moves the order-free WandererAllegiance
feature into the first module, and writes the porting guide later WPs follow.

## Current state

### Executor environment

- Laptop; repo `C:\Users\mikew\source\repos\TAOM`; Bannerlord v1.4.8 (build 119303) at
  `C:\Program Files (x86)\Steam\steamapps\common\Mount & Blade II Bannerlord`
  (`BANNERLORD_GAME_DIR`). Never write into the game install.
- Base branch: local `bannerlord-1.4.5` after WP101 (fast-forwarded to origin and merged with the
  #697 public tree) plus every WP merged before this one; WP104 must be among them.
- Worktree for this WP: `C:\Users\mikew\source\repos\taom-port\105`, branch
  `port148/105-composition-root`. That folder is outside the session's working directory, so a
  `cd` does not persist between Bash calls: start EVERY command with
  `cd /c/Users/mikew/source/repos/taom-port/105 &&`.
- Python: `python` (if Git Bash cannot find it, use `/c/Python314/python.exe`).

### Files and roles

| File | Role in this WP |
|---|---|
| `Main/IoC.cs` (single-owner) | DryIoc composition. Gains the runner block, two members, one statics line; loses the WandererAllegiance registration line |
| `Main/SubModule.cs` (single-owner) | Lifecycle hooks. Gains one `using` and six one-line runner calls; loses the WandererAllegiance `AddBehavior` block |
| `Main/Composition/*.cs` (new, 5 files) | The runner, copied byte for byte from the 1.5.x merge `1efdf2985` |
| `Main/Features/WandererAllegiance/WandererAllegianceModule.cs` (new) | The first module, from `1efdf2985` |
| `Main/Features/WandererAllegiance/WandererAllegianceIoC.cs` | Parameter `IContainer` becomes `IRegistrator`, doc comment updated (from `1efdf2985`) |
| `TAOM.Tests/Composition/*.cs` (new, 3 files) | 35 tests, from `0f3af9577`, plus `06252ae2c`'s one-line hunk in `FeatureModulesTests.cs` |
| `TAOM.Tests/Features/WandererAllegiance/WandererAllegianceWiringTests.cs` | Replaced by the `1efdf2985` version (6 tests stay 6) |
| `docs/features/wanderer-allegiance.md` | Two table rows and one test bullet (the `44045b348` hunks), plus a link |
| `docs/features/composition-root.md` (new) | Runner doc and the porting guide downstream WPs cite |

### End-state sources on 1.5.x (verified this session)

| Source | Lines | Blob |
|---|---|---|
| `1efdf2985:Main/Composition/TaomFeatureModule.cs` | 61 | `d25715037` |
| `1efdf2985:Main/Composition/FeatureDecls.cs` | 104 | `c51f382ea` |
| `1efdf2985:Main/Composition/ModuleRunner.cs` | 119 | `0a03bfa2e` |
| `1efdf2985:Main/Composition/FeatureModuleHooks.cs` | 163 | `8ede8533d` |
| `1efdf2985:Main/Composition/FeatureModules.cs` | 20 | `d5c06eb7c` |
| `1efdf2985:Main/Features/WandererAllegiance/WandererAllegianceModule.cs` | 29 | `14084672b` |
| `1efdf2985:Main/Features/WandererAllegiance/WandererAllegianceIoC.cs` | 21 | `f79574cb7` |
| `1efdf2985:TAOM.Tests/Features/WandererAllegiance/WandererAllegianceWiringTests.cs` | 127 | `d7040f387` |
| `0f3af9577:TAOM.Tests/Composition/FeatureModulesTests.cs` (12 tests) | 228 | `cf9a5cf47` |
| `0f3af9577:TAOM.Tests/Composition/ModuleRunnerTests.cs` (16 tests) | 321 | `4c7f8e015` |
| `0f3af9577:TAOM.Tests/Composition/FeatureModuleHooksTests.cs` (7 tests) | 188 | `717ca6797` |

- No long dashes in these files; no production file cites an engine version.
- The only post-merge contract change on 1.5.x is `RegisterCampaignObjects` (`8b015667f`, Postern
  Gate, 1.5.x-only): NOT ported; nor are `ClientComposition.cs`/`IClientComposition.cs` (D7) or
  `MainMenuStep.cs` (WP112). `ITaomFeatureModule.cs` and `FeatureState` (created by `9ed3ee308`,
  deleted by `fa0b24e13`) must not exist at the end.
- Between 1.4.x and `1efdf2985` the WandererAllegiance files differ only in
  `WandererAllegianceIoC.cs`, the new module, the wiring test, and `WandererAllegianceBindingTests.cs`
  (one line: `b8c000452`'s CI tag, plan 010, D9: leave that file alone).

### 1.4.x excerpts (`origin/bannerlord-1.4.5`, before WP104)

`Main/IoC.cs` (243 lines). WP104 does not touch it (plan 009's merge `998ae7e95` changes no
`IoC.cs` line). `:3` is `using TAOM.Adapters;`, `:6` `using TAOM.Core.Logging;`, `:77`
`private static IContainer _container;`, `:106` the WandererAllegiance line (its only caller),
`:208` `private static void RegisterCoreServices(IContainer container)`; `Dispose()` (`:238-242`)
nulls `_container`. The tail of `Configure`:

```csharp
// Main/IoC.cs:191-193 and :204-206 (the eager patch-static block :195-202 sits between them)
        Features.UncapturableHeroes.UncapturableHeroesIoC.RegisterUncapturableHeroesFeature(container);

        _container = container;
        // ...
        // Post-registration initialization
        CareerSystemIoC.InitializeCalculators(container.Resolve<Features.CareerSystem.Mutations.IMutationCalculatorRegistry>());
    }
```

`Main/SubModule.cs` (1,969 lines). `:22` is `using TAOM.Adapters;`; `IoC.Configure()` runs at
`:114` in `OnSubModuleLoad`, before every anchor; `IoC.Dispose()` at `:1948`. Before WP104,
`:520` reads `_harmony.PatchCategory("Patch42_CastleRecruitment");`, the Patch55 apply (`:542`)
sits in a try inside `if (!_basicTableauGuardApplied)` (`:537`), and `:1768` reads
`_harmony.PatchCategory("Patch_MissionTime_SetMovementOrder");`; WP104 turns each into
`TryPatchCategory(...)`. `OnGameStart` (`:697-733`) ends with
`RegisterCampaignLifeBehaviors(campaignStarter);` (`:731`) and the closing brace of its
`CampaignGameStarter` branch (`:732`); 1.4.x has no Custom Battle branch there.

```csharp
// Main/SubModule.cs:1244-1247 (inside RegisterCampaignLifeBehaviors, between the
// AlignmentDesertion block and the "// EliteEmissary" comment)
        // WandererAllegiance (#575): a wanderer refuses to be hired across the Free/Evil line. Two
        // condition-gated NPC lines on vanilla's companion_hire token (priority 110 over vanilla's
        // 100); stateless, reads MCM live, so registered unconditionally. No Harmony patch.
        campaignStarter.AddBehavior(IoC.Resolve<Features.WandererAllegiance.Hooks.WandererAllegianceDialogBehavior>());

// Main/SubModule.cs:1782-1787 (local function; the engine add is NOT guarded)
        void AddTaomBehavior(MissionBehavior behavior)
        {
            try { battleLoadDiagSvc?.LogTaomBehaviorAdded(behavior.GetType().Name); } catch { /* diagnostic only */ }
            mission.AddMissionBehavior(behavior);
            taomBehaviorCount++;
        }

// Main/SubModule.cs:1855-1859
        var colorStore = IoC.Resolve<IAgentColorStore>();
        if (colorStore != null)
            AddTaomBehavior(new AgentColorStoreCleanupBehavior(colorStore));

        // MissionDiagnostic: added LAST so it sees all behaviors added by TAOM AND
```

The `FeatureModules.cs` comment `ecbc2199e` wrote names four hand-wired blocks that still follow
a module call: the main-menu work after the once-only MainMenu block (1.4.x `:551` on),
`ManualPatchApplicator.ApplyAll` (`:1715`) and the co-op Harmony census (`:1717` on; on 1.4.x it
runs only when a co-op module is active) after the game-init batch, and the kernel tail of
`OnMissionBehaviorInitialize`. All four exist on 1.4.x, and the kernel tail is exactly the four
behaviors after `AgentColorStoreCleanupBehavior`: `MissionDiagnosticBehavior` (`:1868`),
`BattleLoadPhaseBehavior` (`:1888-1890`), `CrashReportDevTriggerMissionBehavior` (`:1895`) and
`CareerPerkMissionBehavior` (`:1897-1908`). So both `ecbc2199e` comments carry over verbatim.

### Expected shape after WP104 (the plan 009 end state, `998ae7e95:Main/SubModule.cs`)

```csharp
// 998ae7e95:Main/SubModule.cs:619
        TryPatchCategory("Patch42_CastleRecruitment");
// :641 and :647, inside if (!_basicTableauGuardApplied)
            TryPatchCategory("Patch55_BasicTableauRaceGuard");
            ReportPatchFailures(new TextObject("{=taom_patch_apply_phase_startup}startup"), persistent: true);
// :851
    private bool TryPatchCategory(string category) => _patches.TryApply(category);
// :1878-1879
        TryPatchCategory("Patch69_TournamentEndGuard");
        ReportPatchFailures(new TextObject("{=taom_patch_apply_phase_game_init}game initialization"));
// :1937-1938, inside if (!_missionTimePatchesApplied)
            TryPatchCategory("Patch_MissionTime_SetMovementOrder");
            ReportPatchFailures(new TextObject("{=taom_patch_apply_phase_mission_start}mission start"));
```

`TryPatchCategory` is a `private bool` method, so it passes as the runner's `Func<string, bool>`.
Plan 009's source gate (regex `\.PatchCategory\s*\(` in `PatchCategoryApplierTests`) cannot match
`ModuleRunner.cs`'s delegate call `tryPatchCategory(...)`. `git diff 998ae7e95 1efdf2985 --
Main/IoC.cs Main/SubModule.cs` is everything plan 018 changed in the two single-owner files;
Steps 2, 3, 6 and 7 reproduce exactly that. The merge's own resolution only kept each runner call
BEFORE plan 009's localized `ReportPatchFailures` line.

### Engine facts the transplanted code binds (installed v1.4.8 via taom-src, read this session)

| Member | Verified at |
|---|---|
| `public CampaignGameStarter(GameMenuManager gameMenuManager, ConversationManager conversationManager)` (stores both, nothing else) | `TaleWorlds.CampaignSystem.CampaignGameStarter.cs:23-27` |
| `public ICollection<CampaignBehaviorBase> CampaignBehaviors`; `AddBehavior` ignores null; `AddModel<T>(MBGameModel<T>)` initializes with the previous model | same file `:19`, `:35-41`, `:76-81` |
| `public BasicGameStarter()`; `AddModel<T>(MBGameModel<T>)` | `TaleWorlds.MountAndBlade.BasicGameStarter.cs:12-15`, `:34-39` |
| `IGameStarter.AddModel(GameModel)`, `void AddModel<T>(MBGameModel<T> gameModel) where T : GameModel` | `TaleWorlds.Core.IGameStarter.cs:9`, `:11` |
| `public abstract class MBGameModel<T> : GameModel where T : GameModel`, `public void Initialize(T baseModel)` | ``TaleWorlds.Core.MBGameModel`1.cs:3-10`` |
| `CampaignBehaviorBase(string stringId)`, `CampaignBehaviorBase()`, abstract `RegisterEvents()`, abstract `SyncData(IDataStore)` | `TaleWorlds.CampaignSystem.CampaignBehaviorBase.cs:7`, `:12`, `:17`, `:24` |
| `public static event Action<InquiryData, bool, bool> OnShowInquiry`; `DisplayMessage` only invokes an event (no-op without a subscriber); `ShowInquiry(InquiryData data, bool pauseGameActiveState = false, bool prioritize = false)` invokes `OnShowInquiry` | `TaleWorlds.Library.InformationManager.cs:46`, `:61-63`, `:91-93` |
| `InquiryData(string titleText, string text, bool isAffirmativeOptionShown, bool isNegativeOptionShown, string affirmativeText, string negativeText, Action affirmativeAction, Action negativeAction, string soundEventPath = "", ...)`; field `public string Text` | `TaleWorlds.Library.InquiryData.cs:33`, `:9` |
| `InformationMessage(string information, Color color)` (managed only); `Colors.Red` | `TaleWorlds.Library.InformationMessage.cs:21`; `TaleWorlds.Library.Colors.cs:7` |
| `MissionLogic : MissionBehavior` overrides `BehaviorType`, so an empty subclass compiles | `TaleWorlds.MountAndBlade.MissionLogic.cs:7-9` |
| `Mission.AddMissionBehavior(MissionBehavior)` | `TaleWorlds.MountAndBlade.Mission.cs:4615` |
| `MBSubModuleBase` hooks: `OnSubModuleLoad` `:8`, `OnBeforeInitialModuleScreenSetAsRoot` `:16`, `OnGameStart(Game, IGameStarter)` `:36`, `OnGameLoaded` `:52`, `OnGameInitializationFinished` `:84`, `OnMissionBehaviorInitialize(Mission)` `:101` | `TaleWorlds.MountAndBlade.MBSubModuleBase.cs` |
| `Module.OnBeforeGameStart` ends with `InformationManager.ClearAllMessages()` | `TaleWorlds.MountAndBlade.Module.cs:1721`, `:1757` |

v1.4.8 dump (`C:\Decompiled_Bannerlord\_categories_v1.4.8\MountAndBlade\TaleWorlds.MountAndBlade\TaleWorlds.MountAndBlade\`):
`Mission.AfterStart` (`Mission.cs:3799`) calls every submodule's `OnMissionBehaviorInitialize`
(`:3815`) with no try. `MissionState.FinishMissionLoading` (`MissionState.cs:333`) sets
`_missionInitializing = false` (`:335`) before `CurrentMission.AfterStart()` (`:345`), and
`TickLoading` (`:221`) calls `LoadMission()` (`:226`) whenever the flag is false. On 1.4.x,
`Dependencies/Foundation/SaveShield.cs:84` shields `MissionState.FinishMissionLoading` as
MISSION-INIT and `:283` keeps swallowing it. Startup notices
(`C:\Decompiled_Bannerlord\_modules_build\Native__TaleWorlds.MountAndBlade.GauntletUI.cs`):
`GauntletInitialScreen` (`:2493`) calls `InformationManager.ClearAllMessages()` (`:2552`);
`GauntletQueryManager.CreateQuery` (`:3917`) queues an inquiry that is not equal to the active or
a queued one (`:3925`, `:3935`). So the hooks' notice rule (hold during process load, an inquiry at
the main menu, a red chat line in game) holds on v1.4.8.

### Build facts (1.4.x)

- DryIoc `4.8.8` on both lines (`Main/TAOM.csproj:100`); no TAOM type is named `IResolver` or
  `IRegistrator`. `net472`, `LangVersion 10.0`, `<Nullable>enable</Nullable>`
  (`Directory.Build.props:3,5,6`); `InternalsVisibleTo` `TAOM.Tests` (`Main/TAOM.csproj:117-125`).
  Both projects use SDK default compile globbing: new `.cs` files need no csproj edit.
- `TAOM.Tests` references `Main` (`TAOM.Tests/TAOM.Tests.csproj:27`) and every game
  `TaleWorlds.*.dll` (`:31-34`), including `TaleWorlds.MountAndBlade` (`BasicGameStarter`).
  `FeatureModuleHooksTests` is the first 1.4.x test to build real starters or subscribe to
  `OnShowInquiry`; it passed on 1.5.x, and the v1.4.8 members it touches are listed above.
- Gates that see the new code and stay green: `IoCRegistrationDisciplineTests` (no
  `container.Resolve` in the register body), `ResetForUnloadSweepTests`,
  `GameModelOverrideBindingTests` (no module declares a model), and
  `FeatureModulesTests.IoCResolver_IsReadOnlyByTheFeatureModuleHooks` (no 1.4.x file reads
  `IoC.Resolver` today).

### Pilot facts (WandererAllegiance; 1.4.x, read this session)

- `WandererAllegianceDialogBehavior`: lines at priority 110 (`Hooks/WandererAllegianceDialogBehavior.cs:25`),
  `SyncData` empty (`:41`), registered on `companion_hire` (`:47`, `:56`); those are the only two
  TAOM lines on `companion_hire` (`git grep '"companion_hire"' -- Main`).
- `LotrIssueSuppression.SuppressAll` (`Main/Features/LotrIssues/LotrIssueSuppression.cs:170-203`)
  removes only a fixed list of vanilla issue behaviors (`:28-34` SandBox, `:40` on
  TaleWorlds.CampaignSystem.Issues). Adding the pilot after it changes nothing.
- The behavior's dependency graph is unchanged across lines (`WandererAllegianceService`,
  `WandererAllegianceConfigProvider(IPathService, IModLogger)`,
  `WandererAllegianceSettingsProvider`), so the merge-state container test resolves on 1.4.x.

### Conventions that bind this change

- **ADR-007 adapters**: services never touch sealed TaleWorlds types. The Composition types are
  composition-root infrastructure, not services: they touch `CampaignGameStarter`, `Mission` and
  `InformationManager` exactly as `SubModule` does. Do not wrap them in adapters.
- **ADR-002 thin entry points**: entry points delegate. `SubModule.cs` gains only six one-line
  calls and a `using`; every loop lives in `Main/Composition`.
- **ADR-003** no `#region`; **ADR-004** no `[Obsolete]`; **ADR-005** no `#if DEBUG` outside the IoC
  registration exception.
- **TDD**: the failing test comes first (Steps 1 and 5 are RED steps).
- **MCM json2 rule** (rename a persisted setting, never flip its default) and
  **`.claude/rules/harmony-patches.md`** (`MovementOrder` postfixes go through the shared deferred
  category): neither is touched; the runner never reads MCM, and the FirstMission call sits inside
  the existing once-only mission-time block.
- **Localization**: the fault notice is literal English, as on the 1.5.x tip. Add no `{=key}`
  strings (the unregistered-key ratchet would fail).

## Commands you will need

Start every command with `cd /c/Users/mikew/source/repos/taom-port/105 &&`.

| Purpose | Command | Expected |
|---|---|---|
| Restore | `dotnet restore TAOM.sln` | exit 0 |
| Build | `dotnet build TAOM.sln -p:DisableModuleCopy=true -p:ModuleId=` | exit 0, 0 errors (a BHA0001 Patch88 warning may still print if WP104 did not silence it; ignore it) |
| Test | `dotnet test TAOM.Tests --no-build -p:DisableModuleCopy=true -p:ModuleId=` | `Failed: 0`; the full suite takes minutes, so give the call a 600000 ms timeout |
| Filtered | `dotnet test TAOM.Tests --no-build -p:DisableModuleCopy=true -p:ModuleId= --filter "FullyQualifiedName~<Class>"` | as each step states |
| Docs | `python tools/lint_docs.py --summary --dash-base $(git merge-base HEAD bannerlord-1.4.5)` | `dead_links` and `orphan_features` equal Step 0.7's values; `ai_dashes: 0` |

Both `-p:` flags are mandatory on every build and test: `Main/TAOM.csproj:133-136` does not gate
the CopyModule target on `DisableModuleCopy`, so a build without `-p:ModuleId=` deploys into the
game install. Never `./build.ps1`. Never two builds at once in this worktree. Always build before a
`--no-build` test run. Give every Bash call that builds or tests a 600000 ms timeout: the Verify
blocks of Steps 2, 3, 6 and 7 chain a build and several filtered runs, which can outlast the
default 120000 ms.

## Scope

**In scope** (the only files you create or modify; 15, or 16 with the conditional one):

- Create: the five `Main/Composition/*.cs` files, `Main/Features/WandererAllegiance/WandererAllegianceModule.cs`,
  the three `TAOM.Tests/Composition/*.cs` files, `docs/features/composition-root.md`.
- Modify: `Main/Features/WandererAllegiance/WandererAllegianceIoC.cs`,
  `TAOM.Tests/Features/WandererAllegiance/WandererAllegianceWiringTests.cs` (replaced),
  `docs/features/wanderer-allegiance.md`; only if Step 0.3 shows the CRLF test missing,
  `TAOM.Tests/Infrastructure/RepoPathsTests.cs`.
- Modify, single-owner (this WP is their only editor while it runs): `Main/IoC.cs` (one `using`,
  two members, the runner block, one statics line; delete the WandererAllegiance line) and
  `Main/SubModule.cs` (one `using`, six calls; delete the WandererAllegiance block). No other edit.

**Out of scope** (do not touch): `CHANGELOG.md` (D1); `.claude/**`, `CLAUDE.md`, `AGENTS.md`,
`docs/reviews/**`, `docs/INDEX.md`, `docs/reference/feature-map.md`, `tools/README.md` (WPs 142 to
144 sync these); `docs/adrs/**` (maintainer only); `plans/README.md` (integrator); `Main/TAOM.csproj`,
`TAOM.Tests/TAOM.Tests.csproj`, `Directory.Build.props` (not needed);
`WandererAllegianceBindingTests.cs` (its 1.5.x diff is the plan 010 CI tag, D9);
`RegisterCampaignObjects`, `ClientComposition`, `IClientComposition`, `MainMenuStep` and every
other feature module (other WPs port them through this plan's guide); renaming or reformatting
anything nearby.

## Git workflow

- Create the worktree from the main checkout (Step 0.1); work and commit only there, on
  `port148/105-composition-root`. Never push, open a PR, tag, or merge into `bannerlord-1.4.5`. Run
  git only through the Bash tool (D19).
- Per-port procedure, end-state variant: plan 018 merged on 1.5.x with its own resolutions, so
  this WP never runs `git cherry-pick`. New files come from `git show <sha>:<path> > <path>` (LF
  endings; `core.autocrlf=true` normalises them on `git add`; a "LF will be replaced by CRLF"
  warning is expected). The two single-owner files are edited by hand at text anchors. Every 1.5.x
  bookkeeping hunk (CHANGELOG, review records, RCA, REVIEW-LOG, lessons, plan text) stays behind.
- Stage with explicit `git add <paths>` (never `git add -A`, `-u` or `.`), then run
  `git commit -F <file>` in the NEXT sequential Bash call: never the same command, never a parallel
  sibling call. Write each message with the Write tool to the file named in its step (outside the
  worktree, so it can never be staged) and pass the path to git in `C:/...` form so the subject
  hook can read it.
- Never `--no-verify`, `--amend`, `-C` or `-c`. If a hook denies a commit, STOP and report the
  message verbatim.
- Commit message template (CLAUDE.md "Commits" plus the programme rules): subject
  `<type>(<scope>): v2.0.28 - <description>`, at most 72 characters; a body for 1.4.x readers
  wrapped at 72 with no long dashes; trailers `Research:`, `Not-tested:`, `Save-compat:` when
  relevant, then one `Ported-from: <40-hex sha>` per included 1.5.x commit. A trailer value that
  wraps continues on lines that start with one space, or git stops reading the paragraph as
  trailers. No `Co-Authored-By` and no "Generated with" line. The exact messages are given in
  Steps 4, 9 and 11.

## Steps

### Step 0: Set up the worktree, check the prerequisites, record the baseline

**0.1 Worktree.**

```bash
cd /c/Users/mikew/source/repos/TAOM && git worktree add /c/Users/mikew/source/repos/taom-port/105 -b port148/105-composition-root bannerlord-1.4.5
```

**Verify**: prints `Preparing worktree (new branch 'port148/105-composition-root')`. If the branch
or the folder already exists: STOP.

**0.2 Drift check** (the command in the header). **Verify**: no change under `Main/Composition`,
`TAOM.Tests/Composition`, `Main/Features/WandererAllegiance`, `docs/features/composition-root.md`.
`Main/SubModule.cs` and `WandererAllegianceWiringTests.cs` changes are WP104's and expected. Any
other change: read it, then let 0.4 decide.

**0.3 WP104 prerequisites.**

```bash
cd /c/Users/mikew/source/repos/taom-port/105 && test -f Main/PatchCategoryApplier.cs && echo applier-ok; \
grep -cF 'private bool TryPatchCategory(string category)' Main/SubModule.cs; \
grep -cF 'public static string ReadSource(string relativePath, bool stripComments = false)' TAOM.Tests/Infrastructure/RepoPaths.cs; \
grep -cF 'public static string StripComments(string source)' TAOM.Tests/Infrastructure/RepoPaths.cs; \
grep -cF 'public static string RepoPath(params string[] parts)' TAOM.Tests/Infrastructure/RepoPaths.cs; grep -cF 'public static bool MentionsOutsideComments(string path, string needle, bool wholeWord = false)' TAOM.Tests/Infrastructure/RepoPaths.cs; \
grep -cF 'ParkedModels_AreRealModels_ThatSubModuleDoesNotRegister' TAOM.Tests/Migration/GameModelOverrideBindingTests.cs; \
grep -cF 'RepoPaths.ReadSource("Main/IoC.cs", stripComments: true)' TAOM.Tests/Features/WandererAllegiance/WandererAllegianceWiringTests.cs; \
grep -cF 'ReadSource_NormalisesAnExplicitCrlfFile_ToLf' TAOM.Tests/Infrastructure/RepoPathsTests.cs; \
grep -cF 'Include="DryIoc.dll" Version="4.8.8"' Main/TAOM.csproj; \
ls -d Main/Composition TAOM.Tests/Composition 2>&1
```

**Verify**: `applier-ok`, then `1` on each of the next seven lines; the CRLF-test line prints `1`
(or `0`: then Step 1 adds that test and the expected count rises by one); DryIoc prints `1`; both
`ls` lines say `No such file or directory`. Anything else: STOP (WP104 not merged, or a
Composition folder already exists).

**0.4 Anchors.** Each must exist exactly once.

```bash
cd /c/Users/mikew/source/repos/taom-port/105 && for a in \
 'using TAOM.Adapters;' \
 'TryPatchCategory("Patch42_CastleRecruitment");' \
 'protected override void OnBeforeInitialModuleScreenSetAsRoot()' \
 'TryPatchCategory("Patch55_BasicTableauRaceGuard");' \
 'ReportPatchFailures(new TextObject("{=taom_patch_apply_phase_startup}startup"), persistent: true);' \
 'RegisterCampaignLifeBehaviors(campaignStarter);' \
 'public override void OnGameLoaded(' \
 'TryPatchCategory("Patch69_TournamentEndGuard");' \
 'ReportPatchFailures(new TextObject("{=taom_patch_apply_phase_game_init}game initialization"));' \
 'TryPatchCategory("Patch_MissionTime_SetMovementOrder");' \
 'ReportPatchFailures(new TextObject("{=taom_patch_apply_phase_mission_start}mission start"));' \
 'new AgentColorStoreCleanupBehavior(colorStore)' \
 'new Features.MissionDiagnostic.Hooks.MissionDiagnosticBehavior(' \
 'campaignStarter.AddBehavior(IoC.Resolve<Features.WandererAllegiance.Hooks.WandererAllegianceDialogBehavior>());' \
 ; do printf '%s:%s  %s\n' "$(grep -cF "$a" Main/SubModule.cs)" "$(grep -nF "$a" Main/SubModule.cs | head -1 | cut -d: -f1)" "$a"; done; \
for a in 'using TAOM.Adapters;' 'private static IContainer _container;' \
 'Features.UncapturableHeroes.UncapturableHeroesIoC.RegisterUncapturableHeroesFeature(container);' \
 '_container = container;' 'CareerSystemIoC.InitializeCalculators(' 'private static void RegisterCoreServices(' \
 'Features.WandererAllegiance.WandererAllegianceIoC.RegisterWandererAllegianceFeature(container);' \
 ; do printf 'IoC %s:%s  %s\n' "$(grep -cF "$a" Main/IoC.cs)" "$(grep -nF "$a" Main/IoC.cs | head -1 | cut -d: -f1)" "$a"; done
```

**Verify**: every line starts with `1:`. The line numbers (after the colon) must also be ordered:
Patch42 < `OnBeforeInitialModuleScreenSetAsRoot`; Patch55 < the startup report; Patch69 end guard
< the game-init report; the mission-time category < the mission-start report;
`AgentColorStoreCleanupBehavior` < `MissionDiagnosticBehavior`. In `IoC.cs`: UncapturableHeroes <
`_container = container;` < `InitializeCalculators` < `RegisterCoreServices`. A missing anchor, a
count other than 1, or a wrong order: STOP and report the output (the kernel tests in Step 1 copy
these strings verbatim).

**0.5 Single caller.** `grep -rln "RegisterWandererAllegianceFeature" Main --include=*.cs` must list
exactly `Main/Features/WandererAllegiance/WandererAllegianceIoC.cs` and `Main/IoC.cs`. A third
file: STOP.

**0.6 Baseline.** Run Restore, Build, Test. **Verify**: build exit 0; tests `Failed: 0`. Record
the `Total`, `Passed` and `Skipped` numbers as **T0** (the WP101 baseline was 9154 total, 9151
passed, 3 skipped; WP104 added tests, so T0 is higher; it should equal the latest count the
integrator recorded in `plans/README.md`). Any failure: STOP.

**0.7 Docs baseline.** Run the Docs command. Record `dead_links` and `orphan_features`.

### Step 1 (RED): Add the runner tests

Sources: `9ed3ee308` (FeatureModulesTests, ModuleRunnerTests), `fa0b24e13` (FeatureModuleHooksTests
and the review-follow-up tests), `0f3af9577` (kernel anchors on plan 009's localized reports),
`06252ae2c` (one line of `FeatureModulesTests`). Method: end-state transplant from `0f3af9577`, plus
that line.

```bash
cd /c/Users/mikew/source/repos/taom-port/105 && mkdir -p TAOM.Tests/Composition && \
git show 0f3af9577:TAOM.Tests/Composition/ModuleRunnerTests.cs > TAOM.Tests/Composition/ModuleRunnerTests.cs && \
git show 0f3af9577:TAOM.Tests/Composition/FeatureModuleHooksTests.cs > TAOM.Tests/Composition/FeatureModuleHooksTests.cs && \
git show 0f3af9577:TAOM.Tests/Composition/FeatureModulesTests.cs > TAOM.Tests/Composition/FeatureModulesTests.cs
```

Then re-cite the one engine-version claim: in `TAOM.Tests/Composition/FeatureModuleHooksTests.cs`
line 19, change `/// v1.5.3), so the starters are real:` to `/// v1.4.8), so the starters are real:`
(re-verified: `CampaignGameStarter.cs:23-27`, `:35-41`, `BasicGameStarter.cs:12-15`).

Then apply `06252ae2c`'s one-line hunk: in `TAOM.Tests/Composition/FeatureModulesTests.cs` line 122
(inside `IoCResolver_IsReadOnlyByTheFeatureModuleHooks`), change

```csharp
            .Where(rel => RepoPaths.StripComments(File.ReadAllText(Path.Combine(mainDir, rel))).Contains("IoC.Resolver"))
```

to

```csharp
            .Where(rel => RepoPaths.MentionsOutsideComments(Path.Combine(mainDir, rel), "IoC.Resolver"))
```

The set of readers is unchanged: `MentionsOutsideComments` (WP104, Step 0.3) checks the raw text
first, strips comments only from a file that contains the name, and with `wholeWord` left at
`false` it is the same `Contains`. Change nothing else in the three files.

**Verify**:

```bash
cd /c/Users/mikew/source/repos/taom-port/105 && grep -cF 'RepoPaths.MentionsOutsideComments(Path.Combine(mainDir, rel), "IoC.Resolver")' TAOM.Tests/Composition/FeatureModulesTests.cs; grep -c 'RepoPaths.StripComments' TAOM.Tests/Composition/FeatureModulesTests.cs
```

Expected: `1`, then `0`. Anything else: STOP.

Only if Step 0.3 printed `0` for the CRLF test: add `fa0b24e13`'s test to
`TAOM.Tests/Infrastructure/RepoPathsTests.cs`, copied verbatim from
`git show 06252ae2c:TAOM.Tests/Infrastructure/RepoPathsTests.cs | sed -n '25,41p'` (the two comment
lines, `[TestMethod]` and `ReadSource_NormalisesAnExplicitCrlfFile_ToLf`), placed directly after
the existing `ReadSource_ReturnsTheFileWithLfLineEndingsOnly` test, plus `using System.IO;` at the
top if absent.

**Verify (RED)**:

```bash
cd /c/Users/mikew/source/repos/taom-port/105 && dotnet build TAOM.sln -p:DisableModuleCopy=true -p:ModuleId= > ../105-build.log 2>&1; \
echo "exit=$?"; grep -c "error CS" ../105-build.log; grep "error CS" ../105-build.log | grep -vc "Composition"
```

Expected: `exit=1`; the first count is greater than 0 (errors such as CS0234 "The type or
namespace name 'Composition' does not exist in the namespace 'TAOM'" and CS0246); the second count
is `0` (every error is about `TAOM.Composition` in `TAOM.Tests\Composition\`). If the build passes,
something already defines `TAOM.Composition`: STOP. (`../105-build.log` sits outside the worktree,
so it is never staged.)

### Step 2 (GREEN for the build): Add the composition types and the two IoC members

Sources: `9ed3ee308`, `fa0b24e13`, `ecbc2199e` as merged in `1efdf2985`. Method: end-state
transplant (the per-commit cherry-picks all conflict; see the Disposition table).

```bash
cd /c/Users/mikew/source/repos/taom-port/105 && mkdir -p Main/Composition && \
for f in TaomFeatureModule FeatureDecls ModuleRunner FeatureModuleHooks FeatureModules; do \
  git show 1efdf2985:Main/Composition/$f.cs > Main/Composition/$f.cs || echo "FAILED $f"; done; ls Main/Composition
```

Expected: no `FAILED`; five files listed.

Empty the list for this commit: in `Main/Composition/FeatureModules.cs` delete the one line
`        new Features.WandererAllegiance.WandererAllegianceModule(),` so the array reads:

```csharp
    internal static readonly TaomFeatureModule[] All =
    {
    };
```

Keep the class's doc comment exactly as extracted (it already names the four hand-wired blocks
that follow the module call, verified on 1.4.x above).

`FeatureModuleHooks.cs` reads `IoC.Modules` and `IoC.Resolver`, so add them now. In `Main/IoC.cs`:

1. After `using TAOM.Adapters;` add the line `using TAOM.Composition;`.
2. After `    private static IContainer _container;` insert (keep one blank line before
   `public static void Configure()`):

```csharp

    // The feature-module runner (Main/Composition), built by Configure. SubModule's hooks reach it
    // through FeatureModuleHooks. Null only before Configure has run.
    internal static ModuleRunner? Modules { get; private set; }

    // The container as a resolver for feature-module factories. Null before Configure and after Dispose.
    internal static IResolver? Resolver => _container;
```

**Verify**:

```bash
cd /c/Users/mikew/source/repos/taom-port/105 && dotnet build TAOM.sln -p:DisableModuleCopy=true -p:ModuleId= 2>&1 | tail -3; \
dotnet test TAOM.Tests --no-build -p:DisableModuleCopy=true -p:ModuleId= --filter "FullyQualifiedName~TAOM.Tests.Composition" 2>&1 | grep -E "Failed |Passed!|Failed!"
```

Expected: build `0 Error(s)`; the filtered run reports `Total: 35`, `Failed: 2`, `Passed: 33`, and
the two failures are `Kernel_IoCConfigure_RunsTheModuleStepsAfterEveryHandWiredRegistration` and
`Kernel_SubModule_CallsEachRunnerHookOnce_AfterItsFeatureBlock` (their runner calls do not exist
yet). Any other failure: if `FeatureModuleHooksTests` fails with a `TypeLoadException`,
`FileNotFoundException` or a native error rather than an assertion, STOP (do not add `[Ignore]`);
otherwise fix the transplant (it must match the source byte for byte) and retry once.

### Step 3 (GREEN): Wire the runner into IoC.Configure and the six SubModule hooks

Source: `9ed3ee308` and `fa0b24e13` as resolved in `1efdf2985` (the merge kept every runner call
before plan 009's report). Method: hand edits at the Step 0.4 anchors. Keep everything 1.4.x
already has; add only these lines.

`Main/IoC.cs`:

3. Between `Features.UncapturableHeroes.UncapturableHeroesIoC.RegisterUncapturableHeroesFeature(container);`
   and `_container = container;` the code must read:

```csharp
        Features.UncapturableHeroes.UncapturableHeroesIoC.RegisterUncapturableHeroesFeature(container);

        // Feature modules (Main/Composition/FeatureModules.cs) register after every hand-wired
        // feature above, so a module sees the container as the last hand-wired feature did. A module
        // that throws is logged and skipped for the session, unless it owns save data.
        var modules = new ModuleRunner(FeatureModules.All, () => container.Resolve<IModLogger>());
        modules.RegisterServices(container);
        Modules = modules;

        _container = container;
```

4. After `CareerSystemIoC.InitializeCalculators(container.Resolve<Features.CareerSystem.Mutations.IMutationCalculatorRegistry>());`,
   as the last statement of `Configure`, insert:

```csharp

        // Feature-module statics: after every registration, hand-wired and module alike (the rule
        // the patch-static block above states).
        modules.InitializeStatics(container);
```

`Main/SubModule.cs`:

5. After `using TAOM.Adapters;` add `using TAOM.Composition;`.
6. ProcessLoad: directly after `        TryPatchCategory("Patch42_CastleRecruitment");` insert
   `        FeatureModuleHooks.RunPhase(ApplyPhase.ProcessLoad, TryPatchCategory);`
7. MainMenu: directly after `            TryPatchCategory("Patch55_BasicTableauRaceGuard");` (inside
   `if (!_basicTableauGuardApplied)`, before the startup `ReportPatchFailures`) insert
   `            FeatureModuleHooks.RunPhase(ApplyPhase.MainMenu, TryPatchCategory);`
8. Game start: after the closing brace of `if (gameStarterObject is CampaignGameStarter campaignStarter) { ... }`
   in `OnGameStart` (the brace right after `RegisterCampaignLifeBehaviors(campaignStarter);`), and
   before the method's own closing brace, insert:

```csharp

        // Feature modules last: their behaviors and models follow every hand-wired one (a Custom
        // Battle starter gets only CustomBattle-target models).
        FeatureModuleHooks.AddGameStartContent(gameStarterObject);
```

9. GameInit: directly after `        TryPatchCategory("Patch69_TournamentEndGuard");` (before the
   game-init `ReportPatchFailures`) insert
   `        FeatureModuleHooks.RunPhase(ApplyPhase.GameInit, TryPatchCategory);`
10. FirstMission: directly after `            TryPatchCategory("Patch_MissionTime_SetMovementOrder");`
    (before the mission-start `ReportPatchFailures`) insert
    `            FeatureModuleHooks.RunPhase(ApplyPhase.FirstMission, TryPatchCategory);`
11. Mission behaviors: after `            AddTaomBehavior(new AgentColorStoreCleanupBehavior(colorStore));`
    and before the `// MissionDiagnostic: added LAST` comment insert:

```csharp

        // Feature modules' mission behaviors: after the feature behaviors above, before the kernel tail
        // below (MissionDiagnostic, BattleLoadPhase, the CrashReport dev trigger, CareerPerk).
        FeatureModuleHooks.AddMissionBehaviors(mission, AddTaomBehavior);
```

`AddTaomBehavior` is the local function at `SubModule.cs:1782` (pre-WP104 numbering); passing it
as `Action<MissionBehavior>` is the same shape the 1.5.x merge compiled.

**Verify**:

```bash
cd /c/Users/mikew/source/repos/taom-port/105 && dotnet build TAOM.sln -p:DisableModuleCopy=true -p:ModuleId= 2>&1 | tail -3; \
for f in TAOM.Tests.Composition PatchCategoryApplierTests WandererAllegiance GameModelOverrideBindingTests IoCRegistrationDisciplineTests; do \
  echo "== $f"; dotnet test TAOM.Tests --no-build -p:DisableModuleCopy=true -p:ModuleId= --filter "FullyQualifiedName~$f" 2>&1 | grep -E "Passed!|Failed!"; done; \
grep -c 'FeatureModuleHooks\.' Main/SubModule.cs; grep -cF 'using TAOM.Composition;' Main/SubModule.cs Main/IoC.cs
```

Expected: build `0 Error(s)`; `TAOM.Tests.Composition` `Passed!` with `Total: 35`; every other
filter `Passed!` (0 failed; `GameModelOverrideBindingTests` may report Inconclusive only if it did
so at Step 0.6 too); `6`; `Main/SubModule.cs:1` and `Main/IoC.cs:1`. A `PatchCategoryApplierTests`
failure means a call landed in the wrong place: fix the placement, never that test file. Then run
the full Test command: `Total` = T0 + 35 (+1 if Step 1 added the CRLF test), `Failed: 0`,
`Skipped` = T0's skipped.

### Step 4: Commit B (the empty runner)

```bash
cd /c/Users/mikew/source/repos/taom-port/105 && git add Main/Composition/TaomFeatureModule.cs Main/Composition/FeatureDecls.cs Main/Composition/ModuleRunner.cs Main/Composition/FeatureModuleHooks.cs Main/Composition/FeatureModules.cs Main/IoC.cs Main/SubModule.cs TAOM.Tests/Composition/FeatureModulesTests.cs TAOM.Tests/Composition/ModuleRunnerTests.cs TAOM.Tests/Composition/FeatureModuleHooksTests.cs && git status --porcelain
```

Add `TAOM.Tests/Infrastructure/RepoPathsTests.cs` to that `git add` only if Step 1 changed it.
**Verify**: every listed path shows `A ` or `M `, and nothing else is listed.

Write `C:\Users\mikew\source\repos\taom-port\105-msg-B.txt` with exactly:

```text
refactor(composition): v2.0.28 - add the feature-module runner, empty

Adds Main/Composition, the feature-module runner of the 1.5.x line's
plan 018, with an empty module list. A feature can now keep its wiring
in one XModule.cs listed in FeatureModules.All instead of lines in
Main/IoC.cs and Main/SubModule.cs.

IoC.Configure builds the runner after the last hand-wired registration
and runs module statics as its last statement. SubModule calls the
runner once per patch phase (process load, main menu, game init, first
mission), at game start and at every mission start, each call after
the hand-wired features of that hook. A module that throws is logged
and switched off for the session instead of breaking the load. With no
module listed nothing changes in game.

Trade-off: five small types and a few kernel lines before any module
uses them. Every later 1.5.x module then ports as one file and one
list line instead of edits to the two single-owner files.

Research: taom-src v1.4.8 CampaignGameStarter, BasicGameStarter,
 IGameStarter, MBGameModel`1, CampaignBehaviorBase, InformationManager,
 InquiryData, InformationMessage; v1.4.8 dump Mission.AfterStart,
 MissionState.TickLoading and FinishMissionLoading
Not-tested: the runner calls in a live game (empty list, nothing runs)
Ported-from: 9ed3ee308d3b65e3d22c1d81e1f63d0ed73cefab
Ported-from: fa0b24e139ae81d606630e78513462dc4b626b75
Ported-from: ecbc2199e49338916b912909586f105b2471b457
Ported-from: 0f3af95774a4c04429c0699dbfbebf4980bdc923
Ported-from: 1efdf29852998922128ad97db1896d569b084df3
Ported-from: 06252ae2c7efd68842d59ced471e8848c37d090a
```

The three continuation lines of `Research:` start with one space. Keep it: without it git does not
read the last paragraph as trailers, and the six `Ported-from` lines become invisible to
`%(trailers)` tooling.

Then, in the NEXT Bash call:

```bash
cd /c/Users/mikew/source/repos/taom-port/105 && git commit -F C:/Users/mikew/source/repos/taom-port/105-msg-B.txt && git log -1 --format='%h %s' && git log -1 --format='%(trailers:key=Ported-from,valueonly)' | grep -c .
```

**Verify**: one new commit with that subject; the trailer count prints `6` (any other count: STOP,
never amend); `git status --porcelain` is empty.

### Step 5 (RED): Convert the pilot's wiring test

Sources: `44045b348` (list check, no-hand-registration check, DryIoc handshake) and `fa0b24e13`
(the text test replaced by the container test). Method: whole-file transplant from `1efdf2985`;
it supersedes WP104's two-line `RepoPaths` conversion of the same file.

```bash
cd /c/Users/mikew/source/repos/taom-port/105 && git show 1efdf2985:TAOM.Tests/Features/WandererAllegiance/WandererAllegianceWiringTests.cs > TAOM.Tests/Features/WandererAllegiance/WandererAllegianceWiringTests.cs && grep -c "\[TestMethod\]" TAOM.Tests/Features/WandererAllegiance/WandererAllegianceWiringTests.cs
```

Expected: `6`. The three tests `IoC_RegistersTheFeature`,
`WandererAllegianceIoC_RegistersEveryConsumerOfTheBehavior` and `SubModule_AddsTheDialogBehavior`
are replaced by `FeatureModules_ListTheWandererAllegianceModuleOnce`,
`IoC_NoLongerRegistersTheFeatureByHand` and
`Module_RegistersTheServiceGraph_AndItsBehaviorDeclResolvesTheSingleton`.

**Verify (RED)**:

```bash
cd /c/Users/mikew/source/repos/taom-port/105 && dotnet build TAOM.sln -p:DisableModuleCopy=true -p:ModuleId= > ../105-build.log 2>&1; \
echo "exit=$?"; grep "error CS" ../105-build.log | grep -c "WandererAllegianceModule"; grep "error CS" ../105-build.log | grep -vc "WandererAllegianceWiringTests"
```

Expected: `exit=1`; the first count is greater than 0 (CS0246 for `WandererAllegianceModule`); the
second is `0`.

### Step 6 (GREEN for the build, RED for the double-wiring guards): Create the module

Source: `44045b348` as merged in `1efdf2985`. Method: transplant. First confirm no earlier WP
changed the feature folder:

```bash
cd /c/Users/mikew/source/repos/taom-port/105 && git diff --quiet 1b00d1acd HEAD -- Main/Features/WandererAllegiance && echo unchanged
```

If it prints `unchanged`:

```bash
cd /c/Users/mikew/source/repos/taom-port/105 && \
git show 1efdf2985:Main/Features/WandererAllegiance/WandererAllegianceModule.cs > Main/Features/WandererAllegiance/WandererAllegianceModule.cs && \
git show 1efdf2985:Main/Features/WandererAllegiance/WandererAllegianceIoC.cs > Main/Features/WandererAllegiance/WandererAllegianceIoC.cs && \
git diff --stat -- Main/Features/WandererAllegiance
```

Expected: `WandererAllegianceIoC.cs | 6 +++---` (the signature `RegisterWandererAllegianceFeature(IRegistrator container)`
and two doc-comment lines that now name `WandererAllegianceModule`); the module shows as untracked
in `git status`. If it did not print `unchanged`, STOP and report the diff (the hand-merge is the
integrator's call).

In `Main/Composition/FeatureModules.cs` put the line back inside `All`:

```csharp
    internal static readonly TaomFeatureModule[] All =
    {
        new Features.WandererAllegiance.WandererAllegianceModule(),
    };
```

**Verify**:

```bash
cd /c/Users/mikew/source/repos/taom-port/105 && dotnet build TAOM.sln -p:DisableModuleCopy=true -p:ModuleId= 2>&1 | tail -3; \
for f in WandererAllegianceWiringTests FeatureModulesTests; do echo "== $f"; \
dotnet test TAOM.Tests --no-build -p:DisableModuleCopy=true -p:ModuleId= --filter "FullyQualifiedName~$f" 2>&1 | grep -E "^\s*Failed |Passed!|Failed!"; done
```

Expected: build `0 Error(s)` (the `IoC.cs` call still compiles: `Container` is an
`IRegistrator`); `WandererAllegianceWiringTests` 1 failed, `IoC_NoLongerRegistersTheFeatureByHand`;
`FeatureModulesTests` 1 failed,
`EveryDeclaredCampaignBehavior_IsDeclaredOnce_AndSubModuleNoLongerAddsIt` (SubModule still resolves
the behavior). Exactly these two RED results; anything else: STOP.

### Step 7 (GREEN): Delete the hand wiring

1. `Main/IoC.cs`: delete the whole line
   `        Features.WandererAllegiance.WandererAllegianceIoC.RegisterWandererAllegianceFeature(container);`
2. `Main/SubModule.cs`: in `RegisterCampaignLifeBehaviors`, delete these four lines and the blank
   line after them, so exactly one blank line separates the AlignmentDesertion block from the
   `// EliteEmissary` comment:

```csharp
        // WandererAllegiance (#575): a wanderer refuses to be hired across the Free/Evil line. Two
        // condition-gated NPC lines on vanilla's companion_hire token (priority 110 over vanilla's
        // 100); stateless, reads MCM live, so registered unconditionally. No Harmony patch.
        campaignStarter.AddBehavior(IoC.Resolve<Features.WandererAllegiance.Hooks.WandererAllegianceDialogBehavior>());
```

**Verify**:

```bash
cd /c/Users/mikew/source/repos/taom-port/105 && dotnet build TAOM.sln -p:DisableModuleCopy=true -p:ModuleId= 2>&1 | tail -3; \
for f in TAOM.Tests.Composition WandererAllegiance IoCRegistrationDisciplineTests PatchCategoryApplierTests; do echo "== $f"; \
dotnet test TAOM.Tests --no-build -p:DisableModuleCopy=true -p:ModuleId= --filter "FullyQualifiedName~$f" 2>&1 | grep -E "Passed!|Failed!"; done; \
grep -c 'WandererAllegiance' Main/SubModule.cs; grep -c 'RegisterWandererAllegianceFeature' Main/IoC.cs
```

Expected: build `0 Error(s)`; every filter `Passed!`; then `0` and `0`. Then the full Test
command: `Total` = T0 + 35 (+1 if Step 1 added the CRLF test), `Failed: 0`.

### Step 8: Update the pilot's feature doc

Source: `44045b348`'s two hunks. Check the doc is untouched since the planned-at commit:

```bash
cd /c/Users/mikew/source/repos/taom-port/105 && git diff --quiet 1b00d1acd HEAD -- docs/features/wanderer-allegiance.md && echo unchanged
```

If `unchanged`, extract the merge state (it differs from 1.4.x only by these hunks):

```bash
cd /c/Users/mikew/source/repos/taom-port/105 && git show 1efdf2985:docs/features/wanderer-allegiance.md > docs/features/wanderer-allegiance.md && git diff --stat -- docs/features/wanderer-allegiance.md
```

Expected: `1 file changed, 3 insertions(+), 3 deletions(-)` (the `WandererAllegianceIoC.cs` row,
the `Main/SubModule.cs` row replaced by a `WandererAllegianceModule.cs` row, and the
`WandererAllegianceWiringTests` bullet). If the doc was not `unchanged`, apply only those two
hunks instead:

```bash
cd /c/Users/mikew/source/repos/taom-port/105 && git diff 1b00d1acd 1efdf2985 -- docs/features/wanderer-allegiance.md > ../105-doc.patch && git apply ../105-doc.patch && git diff --stat -- docs/features/wanderer-allegiance.md
```

If `git apply` reports a failed hunk: STOP and report.

**Verify**: `grep -c "WandererAllegianceModule" docs/features/wanderer-allegiance.md` prints `2` or
more, and `grep -c "beside AlignmentDesertion" docs/features/wanderer-allegiance.md` prints `0`.

### Step 9: Commit C (the pilot module)

```bash
cd /c/Users/mikew/source/repos/taom-port/105 && git add Main/Features/WandererAllegiance/WandererAllegianceModule.cs Main/Features/WandererAllegiance/WandererAllegianceIoC.cs Main/Composition/FeatureModules.cs Main/IoC.cs Main/SubModule.cs TAOM.Tests/Features/WandererAllegiance/WandererAllegianceWiringTests.cs docs/features/wanderer-allegiance.md && git status --porcelain
```

**Verify**: exactly those seven paths staged, nothing else listed.

Write `C:\Users\mikew\source\repos\taom-port\105-msg-C.txt` with exactly:

```text
refactor(composition): v2.0.28 - move WandererAllegiance into a module

Wanderer Allegiance (#575) is the first feature module.
WandererAllegianceModule registers the feature's services
(WandererAllegianceIoC now takes an IRegistrator) and declares its
dialog behavior. FeatureModules.All lists it, and its registration
line in Main/IoC.cs and its AddBehavior block in Main/SubModule.cs are
gone. The wiring test now checks the list entry, the absence of hand
registration and the service graph against a real DryIoc container.

The behavior is still a container singleton. It is now added after
every hand-wired campaign behavior, which changes nothing: its two
lines are the only TAOM lines on companion_hire and outrank vanilla's
reply (priority 110 over 100), and LotrIssueSuppression.SuppressAll
removes only its fixed list of vanilla issue behaviors.

Research: taom-src v1.4.8 CampaignGameStarter.AddDialogLine, AddBehavior
Not-tested: in-game wanderer refusal (needs a campaign)
Save-compat: the behavior's SyncData is empty; no save data touched
Ported-from: 44045b348dfd5ed0444c5868909d4180d2214a28
Ported-from: fa0b24e139ae81d606630e78513462dc4b626b75
Ported-from: 1efdf29852998922128ad97db1896d569b084df3
```

(`AddDialogLine`'s `priority` defaults to 100 and `AddBehavior` ignores null: installed v1.4.8
`TaleWorlds.CampaignSystem.CampaignGameStarter.cs:137` and `:35-41`.)

Then, in the NEXT Bash call:

```bash
cd /c/Users/mikew/source/repos/taom-port/105 && git commit -F C:/Users/mikew/source/repos/taom-port/105-msg-C.txt && git log -1 --format='%h %s' && git log -1 --format='%(trailers:key=Ported-from,valueonly)' | grep -c .
```

**Verify**: the new commit; the trailer count prints `3` (any other count: STOP, never amend);
`git status --porcelain` empty.

### Step 10: Write the composition-root doc and its porting guide

New work (the orchestrator's third note: downstream WPs cite this section). Create
`docs/features/composition-root.md` with exactly the content below, replacing `<YYYY-MM-DD>` with
today's date. Then in `docs/features/wanderer-allegiance.md`, at the end of the purpose cell of
the `WandererAllegianceModule.cs` row, after `after every hand-wired behavior`, append
` (the runner and the porting rules: [composition-root.md](composition-root.md))` before the
closing ` |`. That link is what keeps the new doc from being an orphan.

~~~~markdown
# Composition Root (feature modules)

## Overview

`Main/Composition` lets a feature own its wiring in its own folder. A feature module is one
`<Name>Module.cs` class that derives `TaomFeatureModule` and is listed once in
`FeatureModules.All`. The module runner registers its services, applies its Harmony patch
categories in the lifecycle phase each one needs, and adds its campaign behaviors, game models and
mission behaviors. A module that throws is logged and switched off for the session; the next
module still runs, and so does the game.

Ported to the 1.4.x line from the 1.5.x line's plan 018 (#662). The first module is
`WandererAllegianceModule` ([wanderer-allegiance.md](wanderer-allegiance.md)). Every feature not
listed in `FeatureModules.All` is still wired by hand in `Main/IoC.cs` and `Main/SubModule.cs`.

## Why This Exists

- **Vanilla behavior:** `Mission.AfterStart` calls every submodule's `OnMissionBehaviorInitialize`
  with no try (v1.4.8 `Mission.cs:3799`, `:3815`). `MissionState.FinishMissionLoading` clears
  `_missionInitializing` (`MissionState.cs:335`) before it calls `AfterStart` (`:345`), and
  `TickLoading` calls `LoadMission()` again whenever that flag is false (`:221-226`).
- **TAOM requirement:** a wiring fault at mission start must cost one feature, not the mission.
  SaveShield shields `MissionState.FinishMissionLoading` (MISSION-INIT,
  `Dependencies/Foundation/SaveShield.cs:84`) and swallows the throw, so a hand-wired
  `AddTaomBehavior(new X(...))` that throws turns into a mission that loads again in a loop.
  Feature wiring also needs a home outside the two single-owner files, so that a 1.5.x feature
  with a module (82 of them on that line at `e45f6df2c`) ports as a file plus one list line.
- **Without this feature:** each ported module is translated into hand wiring in `IoC.cs` and
  `SubModule.cs`, and its tests are rewritten into text asserts against those files.

## Architecture

### Design Challenge

`SubModule` hooks run at different points (process load, the first main menu, game start, game
initialization, mission start), and some patch categories may apply only at one of them (Patch55
at the main menu, `MovementOrder` categories only once `Mission.Current` exists). A chat line is
never queued (`InformationManager.DisplayMessage` only raises an event), and the initial screen
clears the chat log (`GauntletInitialScreen` calls `InformationManager.ClearAllMessages`), so a
startup fault has to wait for an inquiry at the first main menu.

### Solution Approach

- `TaomFeatureModule.cs`: the contract. Every member has an empty default; a module overrides
  only what it has: `Id`, `ParkedReason`, `OwnsSaveData`, `RegisterServices(IRegistrator)`,
  `InitializeStatics(IResolver)`, `PatchCategories`, `CampaignBehaviors`, `GameModels`,
  `MissionBehaviors`, `OnPhase(ApplyPhase, IResolver)`. `ApplyPhase` is `ProcessLoad`, `MainMenu`,
  `GameInit` or `FirstMission`.
- `FeatureDecls.cs`: `PatchCategoryDecl(category, phase)`, `CampaignBehaviorDecl.Of<T>(...)`,
  `GameModelDecl.Of<TSlot, TModel>(target, ...)` (added through the generic `AddModel<TSlot>`,
  which hands the slot's previous model to `Initialize` as `BaseModel`) and
  `MissionBehaviorDecl.Of<T>(...)`.
- `ModuleRunner.cs`: runs one step over the list in order. A module that throws is logged
  `[Module] <Id> failed in <step>: <exception>`, marked faulted and skipped for the rest of the
  session. A module with `OwnsSaveData` fails closed (the throw propagates) in service
  registration, static initialisation and campaign start. A parked module
  (`ParkedReason != null`) gets only `RegisterServices`.
- `FeatureModuleHooks.cs`: the calls `SubModule` makes. Faults from `IoC.Configure` and process
  load are held for the first main-menu inquiry; later faults show a red chat line. Every
  behavior and model factory runs before anything is handed to the engine, so a module whose
  factory throws adds nothing.
- `FeatureModules.cs`: `FeatureModules.All`, an explicit ordered list. List order is registration
  order, behavior add order and mission-behavior add order.

| Step | Call | Position |
|---|---|---|
| Service registration | `modules.RegisterServices(container)` | `IoC.Configure`, after the last hand-wired `Register*Feature` call, before `_container = container;` |
| Static initialisation | `modules.InitializeStatics(container)` | last statement of `IoC.Configure` |
| ProcessLoad | `FeatureModuleHooks.RunPhase(ApplyPhase.ProcessLoad, TryPatchCategory)` | `OnSubModuleLoad`, after `TryPatchCategory("Patch42_CastleRecruitment")` |
| MainMenu | `FeatureModuleHooks.RunPhase(ApplyPhase.MainMenu, TryPatchCategory)` | once-only Patch55 block of `OnBeforeInitialModuleScreenSetAsRoot`, before the startup patch report |
| Game start | `FeatureModuleHooks.AddGameStartContent(gameStarterObject)` | end of `OnGameStart`, outside the `CampaignGameStarter` branch |
| GameInit | `FeatureModuleHooks.RunPhase(ApplyPhase.GameInit, TryPatchCategory)` | `OnGameInitializationFinished`, after `Patch69_TournamentEndGuard`, before the game-init patch report |
| FirstMission | `FeatureModuleHooks.RunPhase(ApplyPhase.FirstMission, TryPatchCategory)` | once-only mission-time block of `OnMissionBehaviorInitialize`, before the mission-start patch report |
| Mission start | `FeatureModuleHooks.AddMissionBehaviors(mission, AddTaomBehavior)` | `OnMissionBehaviorInitialize`, after the feature behaviors, before the kernel tail (MissionDiagnostic, BattleLoadPhase, the CrashReport dev trigger, CareerPerk) |

A module therefore runs after every hand-wired feature of the same hook. Categories go through
`TryPatchCategory` (`Main/PatchCategoryApplier.cs`): a failed category is the applier's to log
and report, not a module fault.

### Component Diagram

```
FeatureModules.All -> ModuleRunner (isolation, engine-free) -> FeatureModuleHooks (IoC.Modules, IoC.Resolver)
                                                                  called from IoC.Configure and SubModule hooks
```

## Configuration

None. The list is compiled in and nothing reads MCM; a parked module is parked in code.

## Key Files

| File | Purpose |
|------|---------|
| `Main/Composition/TaomFeatureModule.cs` | The contract and `ApplyPhase` |
| `Main/Composition/FeatureDecls.cs` | Category, campaign behavior, game model and mission behavior declarations |
| `Main/Composition/ModuleRunner.cs` | Per-step loop and fault policy |
| `Main/Composition/FeatureModuleHooks.cs` | The calls `SubModule` makes; fault notices |
| `Main/Composition/FeatureModules.cs` | `FeatureModules.All` |
| `Main/IoC.cs` | Builds the runner (`IoC.Modules`); `IoC.Resolver` for the hooks only |
| `Main/SubModule.cs` | The six hook calls |
| `Main/Features/WandererAllegiance/WandererAllegianceModule.cs` | The first module |

## Dependencies

- DryIoc 4.8.8 `IRegistrator` (no `Resolve`, so an eager resolve during registration does not
  compile) and `IResolver`; `TryPatchCategory` and `Main/PatchCategoryApplier.cs` (plan 009);
  `IModLogger` for the `[Module]` lines.

## Tests

- `TAOM.Tests/Composition/ModuleRunnerTests.cs` (16): the isolation policy.
- `TAOM.Tests/Composition/FeatureModuleHooksTests.cs` (7): the hooks against real
  `CampaignGameStarter` and `BasicGameStarter` instances, and the held startup fault.
- `TAOM.Tests/Composition/FeatureModulesTests.cs` (12): unique ids, no declared type also wired by
  hand in `SubModule.cs`, non-empty `SyncData` forces `OwnsSaveData`, only the hooks read
  `IoC.Resolver`, and two kernel tests that pin every runner call between its anchors.
- `TAOM.Tests/Features/WandererAllegiance/WandererAllegianceWiringTests.cs`: the pilot.

## How to port a 1.5.x feature module onto 1.4.x

Decision D2 of the 2026-11-04 port programme: a 1.5.x feature that has a `*Module.cs` arrives on
1.4.x as that module plus one `FeatureModules.All` line, keeping its 1.5.x wiring tests and patch
numbers. Never hand-wire into `Main/IoC.cs` or `Main/SubModule.cs` something that has a 1.5.x
module. A 1.4.x feature that never got a module keeps its hand-wired pattern.

1. Read the module at the commit you port: `git show <sha>:Main/Features/<Folder>/<Name>Module.cs`.
   Note which members it overrides.
2. Stop and report instead of porting when the module:
   - sets `OwnsSaveData => true` (what failing closed means at campaign start is unsettled on
     the 1.5.x line, its decision 48);
   - overrides `RegisterCampaignObjects` (absent on 1.4.x: it came with the 1.5.x-only Postern
     Gate, `8b015667f`);
   - declares `GameModels` (on 1.4.x
     `GameModelOverrideBindingTests.EveryTaomGameModel_IsRegistered_InSubModule` requires
     `new <Model>(` in `Main/SubModule.cs`, so a model declared by a module fails it until that
     test learns the module route);
   - references `ClientComposition`, `IClientComposition` or anything under `Client/` (the
     1.5.x co-op client split, decision D7).
3. Copy the module file as it is, with its `internal const string` category constants: the
   feature's `[HarmonyPatchCategory(<Name>Module.<Const>)]` attributes read them. Re-cite any
   engine line in its comments to v1.4.8 (`pwsh -NoProfile -File tools/taom-src.ps1 path <Type>`).
4. Append `new Features.<Folder>.<Name>Module(),` at the END of `FeatureModules.All`. Put it
   earlier only for a named ordering constraint: say why on its line and pin the order with an
   index test in `FeatureModulesTests`.
5. If 1.4.x already wires the same feature by hand, delete those lines from `Main/IoC.cs` and
   `Main/SubModule.cs` in the same commit. `FeatureModulesTests` fails while a declared behavior,
   mission behavior or patch category is still built or applied in `SubModule.cs`. It does not
   read `IoC.cs`, so the feature's own wiring test asserts that `IoC.cs` no longer registers it
   (pattern: `WandererAllegianceWiringTests.IoC_NoLongerRegistersTheFeatureByHand`).
6. Inside a module, take dependencies from the `IRegistrator`, `IResolver` and `Mission`
   arguments. Never read `IoC.Resolver` (`FeatureModulesTests.IoCResolver_IsReadOnlyByTheFeatureModuleHooks`
   fails) and never call `.PatchCategory(` (plan 009's
   `MainSource_AppliesEveryPatchCategoryThroughTheGuardedHelper` fails): declare a
   `PatchCategoryDecl` instead.
7. Keep the 1.5.x tests that name the module. A test that reads `Main/SubModule.cs` or
   `Main/IoC.cs` reads it through `RepoPaths.ReadSource(<path>, stripComments: true)`.
8. If your change deletes or renames a line that a kernel test uses as an anchor
   (`FeatureModulesTests.Kernel_IoCConfigure_RunsTheModuleStepsAfterEveryHandWiredRegistration`,
   `Kernel_SubModule_CallsEachRunnerHookOnce_AfterItsFeatureBlock`; for example
   `new AgentColorStoreCleanupBehavior(colorStore)`), move that anchor in the same commit to the
   nearest surviving line before the hook. Never delete the assertion.
9. Verify: build, then
   `dotnet test TAOM.Tests --no-build -p:DisableModuleCopy=true -p:ModuleId= --filter "FullyQualifiedName~TAOM.Tests.Composition"`
   and the feature's own tests, then the full suite.

## Performance

No per-frame work: one loop per lifecycle step and one per mission start over the list.

## Changelog

- <YYYY-MM-DD>: ported from the 1.5.x line (plan 018, #662): the runner, WandererAllegiance as the
  first module, and the porting rules above.

## GitHub Issue

- **Issue:** #662 on the 1.5.x line (plan 018, composition root first steps); this is its 1.4.x
  port, `plans/105-composition-root.md`.
- **Status:** ported; in-game check owed.
~~~~

**Verify**:

```bash
cd /c/Users/mikew/source/repos/taom-port/105 && python tools/lint_docs.py --summary --dash-base $(git merge-base HEAD bannerlord-1.4.5); \
grep -c '](composition-root.md)' docs/features/wanderer-allegiance.md; \
grep -c '<YYYY-MM-DD>' docs/features/composition-root.md
```

Expected: `dead_links` and `orphan_features` equal Step 0.7's values, `ai_dashes: 0`; then `1`;
then `0`.

### Step 11: Commit D (the doc)

```bash
cd /c/Users/mikew/source/repos/taom-port/105 && git add docs/features/composition-root.md docs/features/wanderer-allegiance.md && git status --porcelain
```

**Verify**: `A  docs/features/composition-root.md` and `M  docs/features/wanderer-allegiance.md`,
nothing else listed.

Write `C:\Users\mikew\source\repos\taom-port\105-msg-D.txt` with exactly:

```text
docs(composition): v2.0.28 - how to port a 1.5.x feature module

Adds docs/features/composition-root.md: what the feature-module
runner does on the 1.4.x line, where each runner call sits, and the
rules for porting a 1.5.x feature module (copy XModule.cs, append one
FeatureModules.All line, delete any hand wiring of the same feature,
keep the 1.5.x tests) together with the cases that must stop instead.
wanderer-allegiance.md links to it.
```

Then, in the NEXT Bash call:

```bash
cd /c/Users/mikew/source/repos/taom-port/105 && git commit -F C:/Users/mikew/source/repos/taom-port/105-msg-D.txt && git log -1 --format='%h %s'
```

**Verify**: the new commit; `git status --porcelain` empty.

### Step 12: Review gate, final verification, hand-off

1. Run the Done criteria commands below; every one must hold.
2. Review gate: run `/deep-review` on `git diff $(git merge-base HEAD bannerlord-1.4.5)..HEAD`
   (C# across many files, both single-owner files). Ask it to probe the spots under "Maintenance
   notes". Act only on findings that change runtime behaviour or break this plan; land each fix as
   one extra commit `fix(composition): v2.0.28 - review follow-ups` (tests first, the same commit
   procedure) and re-run the Done criteria. Never rewrite the earlier commits. A fix that would
   change a file the Done criteria hold byte-identical to 1.5.x (`Main/Composition/*.cs`,
   `Main/Features/WandererAllegiance/*`, the wiring test, the three `TAOM.Tests/Composition`
   files, beyond the differences the Done criteria name) or add a file outside Scope is not applied here: report the finding in the hand-off for
   the integrator (that identity is what lets later 1.5.x hunks apply cleanly). If your session
   cannot invoke skills, say so in the hand-off and the integrator runs it. Never run
   `/review-codex` or `/codex-verify` (paid; maintainer only).
3. Hand-off report (your final message): the branch name; each commit's short SHA and subject;
   T0 (total, passed, skipped) and the final counts; whether Step 1 added the CRLF test; the Step
   0.4 output if anything was unusual; the `/deep-review` outcome (or that the session could not
   run it); and the "In-game checks owed" list.

## Disposition (every commit in the pack)

Cherry-pick simulation, re-run this session:
`git merge-tree --write-tree --name-only --no-messages --merge-base=<sha>^ origin/bannerlord-1.4.5 <sha>`.

| 1.5.x commit | Subject | Simulation (exit 1 = conflict) | Disposition |
|---|---|---|---|
| `a0db0558b` | test(composition): v2.0.30 - read SubModule and IoC through one reader | 1: six binding tests (`Patch80`, `Patch82`, `Patch84`, `Patch85`, `Patch86`, `Patch87`), `SignatureStrikesBindingTests.cs`, `RepoPaths.cs` | **Not here: WP104 ports it** (orchestrator note). Step 0.3 checks it landed |
| `9ed3ee308` | refactor(composition): v2.0.30 - add the feature-module runner, empty | 1: `Main/SubModule.cs` (1.4.x has no `TryPatchCategory` context until WP104) | **Transplanted** in commit B from the `1efdf2985` end state; `ITaomFeatureModule.cs` not created (deleted by `fa0b24e13`); IoC/SubModule edits by hand (Steps 2 and 3) |
| `44045b348` | refactor(composition): v2.0.30 - move WandererAllegiance into a module | 1: `CHANGELOG.md`, `FeatureModules.cs`, `FeatureModulesTests.cs`, `WandererAllegianceWiringTests.cs` | **Transplanted** in commit C (module, IoC signature, list line, wiring test, doc hunks); CHANGELOG hunk dropped (D1) |
| `fa0b24e13` | fix(composition): v2.0.30 - review follow-ups for plan 018 | 1: the five Composition files, `SubModule.cs`, three test files, `RepoPathsTests.cs`, `REVIEW-LOG.md`, `lessons/testing-qa.md` (plus CHANGELOG) | **Transplanted** in B and C (runner fail-closed fix, `ITaomFeatureModule`/`FeatureState` deletion, `FeatureModuleHooksTests`, pilot container test). Its `RepoPathsTests` CRLF test comes with WP104's `RepoPathsTests` (Step 0.3; Step 1 adds it if missing). Review record, RCA, REVIEW-LOG, lessons, CHANGELOG dropped (bookkeeping; WP142 syncs lessons) |
| `ecbc2199e` | fix(composition): v2.0.30 - convergence fixes for plan 018 | 1: `FeatureModuleHooks.cs`, `FeatureModules.cs`, review record, lesson, RCA, CHANGELOG | **Transplanted**: its two code comments are in the merge-state files; the "four hand-wired blocks" they name verified on 1.4.x; prose hunks dropped |
| `0f3af9577` | fix(composition): v2.0.30 - kernel test anchors follow 009's localized reports | 1: `FeatureModulesTests.cs` | **Transplanted**: `FeatureModulesTests.cs` at this commit (needs WP104's localized report lines, Step 0.4) |
| `06252ae2c` | test(infra): v2.0.34 - faster wiring scans, pin the comment scan | 1: all eight files it touches are absent at `1b00d1acd` | **Adapted, one hunk**: its `FeatureModulesTests` line is applied in commit B (Step 1), because the file first exists here and `MentionsOutsideComments` exists from WP104. Its other hunks are in the row below "Related changes" |
| `667002d8d` | docs(reviews): v2.0.30 - Codex prompt and re-cut plan 018 | 1: `plans/018-composition-root-first-steps.md` | **Skipped**: bookkeeping (1.5.x review prompt and plan text) |
| `71fd21698` | docs(changelog): v2.0.30 - cite #662 for plan 018 | 1: `CHANGELOG.md` | **Skipped**: CHANGELOG only (D1) |
| merge `1efdf2985` | merge(improve): v2.0.30 - plan 018 composition root first steps (#662) | not simulated (merges are outside the 1,114-commit list) | **Source of the end state**; its own resolution (runner calls before 009's localized reports) is reproduced by Step 3's anchors |

Related changes explicitly not taken here:

| Change | Why |
|---|---|
| `8b015667f` (feat(buildings): v2.0.35 - Postern Gate ...) `RegisterCampaignObjects` in the contract | SKIP-FEATURE; orchestrator note excludes every tip-only contract |
| `03d9db8dc` (feat(coop): v2.0.35 - TAOM.Client submodule ...) and successors: `ClientComposition.cs`, `IClientComposition.cs` | co-op client split, D7 |
| `85c608963`, `2afea8878`: `MainMenuStep.cs` | WP112 ports it into this WP's `Main/Composition` |
| `06252ae2c`'s other hunks (`RepoPaths.cs`, `RepoPathsTests.cs`, five wiring tests) | owned by WP104's group: it adds `MentionsOutsideComments` and its test rows and skips the wiring-test edits. Only the `FeatureModulesTests` hunk, which WP104 cannot take because the file does not exist there, is applied here (Step 1) |
| `b8c000452`'s `RequiresGameIL` tag in `WandererAllegianceBindingTests.cs` | plan 010 CI, D9 |

## Test plan

- New: `TAOM.Tests/Composition/ModuleRunnerTests.cs` (16), `FeatureModuleHooksTests.cs` (7),
  `FeatureModulesTests.cs` (12) = 35 test methods, none data-driven. Replaced 3-for-3:
  `WandererAllegianceWiringTests.cs` stays at 6. Conditional: `RepoPathsTests` +1 (Step 1).
  The pack's "T0 + 42" (verify report D1, section 4.5) also counted 6 `RepoPathsTests` and
  `GameModelOverrideBindingTests.ParkedModels_AreRealModels_ThatSubModuleDoesNotRegister`; WP104
  carries those 7, so this WP adds 35.
- Cells covered: list order; parked modules; a throwing module (logged, faulted, skipped later,
  next module runs); save owners (fail closed in registration, statics and campaign start, also
  after an earlier fault; isolated in a phase; never when parked); categories per phase; the fault
  summary; a throwing logger; starter order, throwing factories, Custom Battle; held startup
  faults; null runner or resolver; double-wiring guards; kernel anchors; `IoC.Resolver` readers.
- Structural pattern for any later module test: `TAOM.Tests/Features/WandererAllegiance/WandererAllegianceWiringTests.cs`
  (list entry, no hand registration, container handshake).
- Not unit-testable (commit trailers say so): the runner calls inside a live game lifecycle; the
  wanderer refusal dialogue in a campaign.
- Verification: the full Test command reports `Total` = T0 + 35 (+1), `Failed: 0`.

## Done criteria

Run in the worktree after Step 11 (and again after any review follow-up commit). ALL must hold:

- [ ] `dotnet build TAOM.sln -p:DisableModuleCopy=true -p:ModuleId=` exits 0 with 0 errors.
- [ ] `dotnet test TAOM.Tests --no-build -p:DisableModuleCopy=true -p:ModuleId=` reports
      `Failed: 0` and `Total` = T0 + 35 (+1 if Step 1 added the CRLF test).
- [ ] `git diff --quiet 1efdf2985 HEAD -- Main/Composition Main/Features/WandererAllegiance TAOM.Tests/Features/WandererAllegiance/WandererAllegianceWiringTests.cs && echo same`
      prints `same` (the transplant is byte-identical to the 1.5.x merge state).
- [ ] `git diff --quiet 0f3af9577 HEAD -- TAOM.Tests/Composition/ModuleRunnerTests.cs && echo same`
      prints `same`; `git diff 0f3af9577 HEAD -- TAOM.Tests/Composition/FeatureModuleHooksTests.cs | grep -cE '^[-+][^-+]'`
      prints `2` (only the v1.5.3 to v1.4.8 re-cite); `git diff 0f3af9577 HEAD -- TAOM.Tests/Composition/FeatureModulesTests.cs | grep -cE '^[-+][^-+]'`
      prints `2` (only `06252ae2c`'s line).
- [ ] `ls Main/Composition | wc -l` prints `5`; `grep -c 'new Features\.' Main/Composition/FeatureModules.cs` prints `1`.
- [ ] `grep -c 'FeatureModuleHooks\.' Main/SubModule.cs` prints `6`;
      `grep -cE 'modules\.RegisterServices\(container\);|Modules = modules;|modules\.InitializeStatics\(container\);' Main/IoC.cs`
      prints `3`; `grep -cF 'using TAOM.Composition;' Main/SubModule.cs Main/IoC.cs` prints `1` for each.
- [ ] `grep -c 'WandererAllegiance' Main/SubModule.cs` prints `0`;
      `grep -c 'RegisterWandererAllegianceFeature' Main/IoC.cs` prints `0`.
- [ ] `grep -rnE 'ITaomFeatureModule|FeatureState|RegisterCampaignObjects|ClientComposition' Main TAOM.Tests --include=*.cs`
      prints nothing; `grep -rn '\.PatchCategory(' Main/Composition` prints nothing.
- [ ] `python tools/lint_docs.py --summary --dash-base $(git merge-base HEAD bannerlord-1.4.5)`:
      `dead_links` and `orphan_features` equal Step 0.7's values; `ai_dashes: 0`.
- [ ] `git diff --name-only $(git merge-base HEAD bannerlord-1.4.5)..HEAD` lists only the in-scope
      files (15, or 16 with `RepoPathsTests.cs`), and `grep -c CHANGELOG.md` on that list prints `0`.
- [ ] `git log --format=%s $(git merge-base HEAD bannerlord-1.4.5)..HEAD | awk 'length($0) > 72 || $0 !~ / v2\.0\.28 - /'`
      prints nothing (three subjects, or four with a review follow-up).
- [ ] `git log --format='%(trailers:key=Ported-from,valueonly)' $(git merge-base HEAD bannerlord-1.4.5)..HEAD | grep -c .`
      prints `9` (six from commit B, three from commit C: git parsed both trailer blocks).
- [ ] `git status --porcelain` prints nothing.

## STOP conditions

Stop and report (do not improvise) if:

- Step 0.3 fails: `Main/PatchCategoryApplier.cs`, `private bool TryPatchCategory(string category)`,
  the `RepoPaths` members, the ParkedModels test or the converted wiring-test reads are missing
  (WP104 is not merged), or `Main/Composition` / `TAOM.Tests/Composition` already exists.
- Any Step 0.4 anchor is missing, appears more than once, or is out of order. In particular, if
  the three localized `ReportPatchFailures(new TextObject(...))` lines differ from the excerpt,
  report the real text: the kernel test copies them verbatim and the integrator decides.
- `RegisterWandererAllegianceFeature` has a caller other than `Main/IoC.cs`.
- DryIoc is not `4.8.8`, or the build reports an error in the new files that this plan does not
  explain (for example CS8701 or an ambiguous `IResolver`).
- A RED step does not fail as described, or a GREEN step leaves a failure other than the ones
  named.
- `FeatureModuleHooksTests` fails with a `TypeLoadException`, `FileNotFoundException` or a native
  exception instead of an assertion. Do not add `[Ignore]` or a category to hide it.
- A `PatchCategoryApplierTests` test fails after Step 3 (a runner call is misplaced; never edit
  that test file).
- The full suite after Step 3 or Step 7 shows any failure.
- `Main/Features/WandererAllegiance` changed since `1b00d1acd` (Step 6 check).
- The work seems to need a file outside Scope, or any `IoC.cs` / `SubModule.cs` edit not listed
  in Steps 2, 3 and 7.
- Any module would need `OwnsSaveData => true` (D2: unsettled fail-closed semantics).
- A hook denies a commit. Report its message verbatim; never bypass it.
- A step's verification fails twice after a reasonable fix attempt.

## In-game checks owed (maintainer)

Executors cannot run the game. Once, on the merged branch, in a build that ships both modules:

1. Boot to the main menu. Expected: no inquiry saying `TAOM: feature modules failed`; the TAOM
   log (`Logs\taom_debug_<timestamp>.log` under the game process's working directory;
   `Main/Core/Logging/FileLogger.cs:26-41`) has no `[Module]` line.
2. Wanderer refusal still works (from `docs/features/wanderer-allegiance.md` "How to verify in
   game", items 1, 2 and 5): Mordor character in Bree (`town_EN2`), Aragorn, "I can use someone
   like you in my company": the Free refusal line, gold unchanged. Gondor character in Isengard
   (`town_isengard`), Maztog: the Evil refusal line. Mod Options, TAOM, World / Wanderer
   Allegiance, master toggle off: vanilla hire returns without a restart.
3. Save, reload from the main menu, repeat the Aragorn check: still refused (the module adds the
   behavior on a loaded campaign too).
4. One field battle and one town visit: both load; the log's `[BattleLoad]` TAOM-behaviors count
   equals a pre-WP105 build's for the same kind of mission (no module adds a mission behavior yet).
5. Custom Battle: starts and plays (the `BasicGameStarter` branch of `AddGameStartContent`).

## Maintenance notes

- **Downstream WPs** port 1.5.x modules through `docs/features/composition-root.md`, section "How
  to port a 1.5.x feature module onto 1.4.x". WP112 puts `MainMenuStep` into `Main/Composition`.
- **Kernel anchors will move.** `FeatureModulesTests.Kernel_SubModule_CallsEachRunnerHookOnce_AfterItsFeatureBlock`
  anchors the mission-behavior hook after `new AgentColorStoreCleanupBehavior(colorStore)`. WP123
  ports `e0ad0db76`, which deletes that behavior: on 1.5.x the same commit moved the anchor to
  `AddTaomBehavior(new Features.CompanionTactics.BattleActionBar.Hooks.BattleActionBarMissionView());`,
  and WP123 must do the same. WP130 ports `d96f349f1`, which changes how that view registers (on
  1.5.x it re-anchored to `new Features.MissionPerf.Hooks.MissionTickProfilerBehavior(`): WP130
  re-anchors to whatever line then precedes the hook. Never delete the assertion.
- **Fail-closed is dormant.** No module owns save data. On 1.4.x a campaign-start rethrow would
  meet Patch37's finalizer on `Module.OnApplicationTick`
  (`Main/Features/CrashReport/Hooks/Patch37_CrashReport.cs:54`); what the player then sees is
  UNVERIFIED. D2 makes any `OwnsSaveData = true` module a STOP until that is settled.
- **Literal English notices**: `ModuleRunner.TakeFaultSummary` and `FeatureModuleHooks` show
  English text (as on the 1.5.x tip); they appear only when a module faults.
- **Comments kept byte-identical on purpose**: `FeatureModuleHooks.cs` mentions
  `RegisterCustomBattleModels` (a 1.5.x method; 1.4.x has none) and the "startup inquiry rule" in
  `docs/reviews/lessons/localization-ui.md` (the 1.4.x copy lacks that entry until WP142 or WP104
  brings it). Identical files keep later 1.5.x hunks applying cleanly.
- **Deferred**: an ADR for the composition root (maintainer, `/new-adr`; D2 is the decision of
  record for this programme).
- **What a reviewer should probe**: (1) the fail-closed rethrow is limited to `OwnsSaveData`
  modules in the three named steps; (2) `FeatureModuleHooks` builds every behavior and model
  before adding any; (3) `IoC.Modules` is assigned after `RegisterServices` and before
  `_container`, and `InitializeStatics` is the last statement of `Configure`; (4) each of the six
  `SubModule` calls sits at its anchor, the ProcessLoad and MainMenu calls before plan 009's
  startup inquiry, `AddGameStartContent` outside the campaign branch; (5) `GameModelDecl.Of` uses
  the generic `AddModel<TSlot>`; (6) no module fault uses `DisplayMessage` before a game exists;
  (7) the pilot's behavior is still a container singleton and now follows `LotrIssueSuppression`.
