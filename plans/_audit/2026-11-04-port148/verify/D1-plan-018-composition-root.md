# D1 verify: port plan 018 (Main/Composition feature-module runner) to the 1.4.x line before the bulk of the ports?

- Run: 2026-11-04-port148. Verifier D1, adversarial, read-only. Nothing was built or tested; no ref, index or worktree was changed.
- Refs: 1.4.x = `origin/bannerlord-1.4.5` at `1b00d1acd`. 1.5.x = `origin/bannerlord-1.5.x` at `e45f6df2c`. Plan 018 merge `1efdf2985` (first parent `998ae7e95`, the plan 009 merge). Series: `a0db0558b` (parent `4c728dac`), `9ed3ee308`, `44045b348`, `fa0b24e13`, `ecbc2199e`, `0f3af9577`.
- Engine evidence: installed v1.4.8 DLLs through `pwsh -NoProfile -File tools/taom-src.ps1 path <Type>` (cache `C:\Users\mikew\.taom-src\v1.4.8\`), and the v1.4.8 dump `C:\Decompiled_Bannerlord\_categories_v1.4.8` plus `_modules_build`.
- Scratch scripts and their outputs (data, not deliverables): `SP\d1\measure.py`, `notes.py`, `modules.py`, `reads145.py`, `falsepass.py`, `testrefs.py`, `repopaths.py`, `wps.py` with `*_out.txt` beside them (SP = the run scratchpad).

## Verdict

**Recommend (a): port the plan 018 infrastructure early, as its own work package in wave 0, directly after WP 104 (plan 009) and before every wave-1 WP.** Move `TAOM.Tests/Infrastructure/RepoPaths.cs` into the same package. Split the plan-018 group out of WP 118; the other five WP 118 groups stay in wave 2 (they do not build on 018, see claim 12).

This needs the maintainer's yes: plan 018 is not among the 1.4.5 ports Mike chose (`origin/bannerlord-1.5.x:plans/_audit/2026-09-23-opus/DECISIONS.md:35`, decision 28: 006, 007, 009, 012, 014, 015). If he declines, use **(b)**: `RepoPaths` plus `a0db0558b` in wave 0, hand-wire every module with the fallback paragraph in section 7, and serialize the single-owner edits. (c) is the worst option: 28 port commits need `RepoPaths` anyway and the false pass that step 0 fixes is live on 1.4.x.

Why (a), in four measured facts:

1. **17 feature modules are port payload**, in 14 port groups across 9 WPs; 10 of them sit in 5 wave-1 WPs (106, 109, 110, 111, 112) that the map runs before WP 118 (wave 2).
2. **Without 018 each must be re-wired by hand**: about 49 edit sites in the two single-owner files (22 in `Main/IoC.cs`, 27 in `Main/SubModule.cs`), 10 production patch files whose `[HarmonyPatchCategory(<Module>.<Const>)]` reads a constant that lives on the module class, and 25 test files (96 references to module classes) to rewrite. Wave 1 alone holds 28 of the 49 sites, spread over 5 WPs that would all edit the same two single-owner files.
3. **The cost is small and mostly mechanical on 1.4.x**: 467 lines of new production code that compiles unchanged against v1.4.8 (every engine member verified below), `IoC.cs` +19/-1 and `SubModule.cs` +13/-5 (merge numstat), 25 test conversions whose read counts match 1.4.x file for file, and +42 tests. With an empty module list it is behaviour-neutral; the pilot move is order-free on 1.4.x (verified).
4. **It removes a load-hang path for every module that adds a mission behavior**: on 1.4.x `AddTaomBehavior` calls `mission.AddMissionBehavior` unguarded, and on v1.4.8 a throw out of `OnMissionBehaviorInitialize` is swallowed by Patch37 and the mission reloads every tick. The runner catches per module.

## 1. Plan 018 end state on 1.5.x

### Types (state at the merge `1efdf2985`)

`fa0b24e13` deleted `ITaomFeatureModule.cs` and the `FeatureState` enum, so the plan text (Step 1.2) is not the end state. At the merge `Main/Composition/` holds five files:

| File | Lines | Content |
|---|---|---|
| `TaomFeatureModule.cs` | 61 | `enum ApplyPhase { ProcessLoad, MainMenu, GameInit, FirstMission }`; `abstract class TaomFeatureModule`: abstract `Id`; virtual `ParkedReason` (null = enabled; non-null = parked, services only), `OwnsSaveData` (false), `RegisterServices(IRegistrator)`, `InitializeStatics(IResolver)`, `PatchCategories`, `CampaignBehaviors`, `GameModels`, `MissionBehaviors` (empty arrays), `OnPhase(ApplyPhase, IResolver)` |
| `FeatureDecls.cs` | 104 | `PatchCategoryDecl(category, phase)`; `CampaignBehaviorDecl.Of<T>(Func<IResolver,T>)`; `enum ModelTarget { Campaign, CustomBattle }`; `GameModelDecl.Of<TSlot,TModel>(target, factory)`, added through the generic `AddModel<TSlot>` so the slot's previous model becomes `BaseModel`; `MissionBehaviorDecl.Of<T>(Func<Mission,IResolver,T>)` |
| `ModuleRunner.cs` | 119 | runs one step over the list in order; a throwing module is logged `[Module] <Id> failed in <step>: <ex>`, marked faulted and skipped for the session; an `OwnsSaveData` module fails closed (rethrows) in service registration, static initialisation and campaign start; parked modules get only `RegisterServices`; `TakeFaultSummary()` |
| `FeatureModuleHooks.cs` | 163 | `RunPhase(phase, Func<string,bool> tryPatchCategory)` applies the phase's categories through plan 009's `TryPatchCategory`, then `OnPhase`; `AddGameStartContent(IGameStarter)` (campaign behaviors and Campaign models on a `CampaignGameStarter`, CustomBattle models on a `BasicGameStarter`; every factory runs before anything is added); `AddMissionBehaviors(mission, AddTaomBehavior)`; `NoticeFor`: ProcessLoad holds, MainMenu shows an inquiry, later phases a red chat line; overloads taking runner and resolver are the test seam |
| `FeatureModules.cs` | 20 | `internal static readonly TaomFeatureModule[] All`, an explicit ordered list. One entry at the merge (`WandererAllegianceModule`), 82 at `e45f6df2c` |

### How a feature registers

A `XModule.cs` in the feature folder derives `TaomFeatureModule` and overrides only the dimensions it has; one line goes into `FeatureModules.All`; the feature's lines are deleted from `IoC.cs` and `SubModule.cs`. `FeatureModulesTests` fails until the hand wiring is gone: each behavior, mission behavior, model slot and category is declared once and not also built (`new X(` / `Resolve<X>()`) or applied in `SubModule.cs`, and a behavior whose `SyncData` has a body forces `OwnsSaveData`.

### What IoC.cs and SubModule.cs keep (merge diff `998ae7e95..1efdf2985`)

- `Main/IoC.cs` (+19/-1): every hand-wired `Register*Feature` call stays; after the last one, `var modules = new ModuleRunner(FeatureModules.All, () => container.Resolve<IModLogger>()); modules.RegisterServices(container); Modules = modules;` before `_container = container;`; the eager statics block and `InitializeCalculators` stay; `modules.InitializeStatics(container);` becomes the last statement of `Configure`; two members `internal static ModuleRunner? Modules { get; private set; }` and `internal static IResolver? Resolver => _container;`; the WandererAllegiance registration line goes.
- `Main/SubModule.cs` (+13/-5): all hand wiring stays, plus a `using TAOM.Composition;` and six one-line calls: `RunPhase(ProcessLoad)` after `TryPatchCategory("Patch42_CastleRecruitment")`; `RunPhase(MainMenu)` inside the once-only Patch55 block, before the startup `ReportPatchFailures`; `AddGameStartContent(gameStarterObject)` after the `CampaignGameStarter` branch of `OnGameStart`; `RunPhase(GameInit)` after `Patch69_TournamentEndGuard`, before `ReportPatchFailures(game initialization)`; `RunPhase(FirstMission)` inside the mission-time block, before `ReportPatchFailures(mission start)`; `AddMissionBehaviors(mission, AddTaomBehavior)` before the `MissionDiagnosticBehavior` kernel tail. The WandererAllegiance comment and `AddBehavior` go.

### Changed after the merge (do not port with 018)

- `RegisterCampaignObjects` in `TaomFeatureModule`, `ModuleRunner` and `FeatureModuleHooks`: added by `8b015667f` (Postern Gate, SKIP-FEATURE). `FeatureDecls.cs` is unchanged since the merge.
- `ClientComposition.cs`, `IClientComposition.cs`: TAOM.Client co-op split (`03d9db8dc` and successors, SKIP-FEATURE, batch 23 note 1).
- `MainMenuStep.cs`: issue-807 (`85c608963`, `2afea8878`), already in WP 111; it does not use the runner.
- Extra tip tests come from `da6af0c0b` (Sworn Anew), `8b015667f` and the Client commits; none is needed by an in-scope module.

## 2. Claim checks

| # | Claim | Source | Verdict | Evidence |
|---|---|---|---|---|
| 1 | 82 feature modules derive `TaomFeatureModule` on the tip | `9ed3ee308` port_notes; batch 5 note 6 | CONFIRMED | `git grep ":\s*TaomFeatureModule"` on `e45f6df2c`: 82 classes; `FeatureModules.All` has 82 entries |
| 2 | 92 commits touch `Main/Composition` | same | CONFIRMED (merges included) | `git log a4c6d7c30..e45f6df2c -- Main/Composition`: 92; 89 without merges |
| 3 | 78 commits add a feature `*Module.cs` | batch 5 note 6 | CONFIRMED | 78 non-merge commits add one; 83 files added, 82 remain |
| 4 | later 1.5.x fixes to migrated features "port cleanly only if this lands" | `9ed3ee308` port_notes | OVERSTATED | every in-scope module's port note names a hand-wiring alternative; the cost of that alternative is quantified in section 3 |
| 5 | hard prerequisite: plan 009 | `9ed3ee308` port_notes; `origin/bannerlord-1.5.x:plans/README.md:74` | CONFIRMED by design | `RunPhase` takes 009's `TryPatchCategory`; the kernel tests anchor on it and, after `0f3af9577`, on `b6cb6ff5d`'s localized `ReportPatchFailures(new TextObject(...))`. Plan 009 is absent on 1.4.x: no `Main/PatchCategoryApplier.cs`, zero `TryPatchCategory`/`ReportPatchFailures` and 82 direct `PatchCategory(` lines in `origin/bannerlord-1.4.5:Main/SubModule.cs` |
| 6 | `ecaa67d26` hard-requires `9ed3ee308` | task; `ecaa67d26` prereqs | REFUTED as hard; it is listed | its prereqs are `9ed3ee308, 6eb2e1b90, a9f10688f, a0db0558b`, but its note reads "Replace or port: the Main/Composition feature-module runner (9ed3ee308, plan 018; or wire the four modules in SubModule/IoC)". `346f3e093` is the same ("port those or adapt to 1.4.x's SubModule patch loop and IoC"). No port outside the 018 series hard-requires the runner; `e9b28e2ad`'s `RequiresGame` tag on `FeatureModuleHooksTests` exists only if 018 is ported |
| 7 | `a0db0558b` closes a false pass present on 1.4.x | `a0db0558b` why | CONFIRMED | `origin/bannerlord-1.4.5:TAOM.Tests/Migration/GameModelOverrideBindingTests.cs:55` reads `SubModule.cs` raw and `:60` tests `Contains($"new {m.Name}(")`; 47 model names match `new XModel(` in raw text, 46 after comment stripping; the only comment-only one is `TaomPartyNavigationModel` (`Main/SubModule.cs:938`). Neither single-owner file has a string literal containing `//` or `/*`, so the non-literal-aware stripper is safe |
| 8 | 25 of the 26 converted test files exist on 1.4.x | `a0db0558b` port_notes | CONFIRMED | `SignatureStrikesBindingTests.cs` is absent. Plan 018's own checker run read-only against the ref: 41 old-style reads (31 SubModule, 10 IoC), per file identical to `4c728dac` |
| 9 | `RepoPaths.cs` is absent on 1.4.x; 22-line base from `35007dbb8` | `a0db0558b` port_notes | CONFIRMED | `ls-tree` on the ref; `35007dbb8` version is 22 lines |
| 10 | WandererAllegiance is hand-wired at `SubModule.cs:1247` and `IoC.cs:106` | `44045b348` why | CONFIRMED | both lines on the ref |
| 11 | `fa0b24e13` deletes `ITaomFeatureModule` and `FeatureState` | `fa0b24e13` why | CONFIRMED | neither exists at `1efdf2985`; parked means `ParkedReason != null` |
| 12 | WP 118 = "Plan 018 ... and the architecture refactors that build on it" | `SP\agg\wp_map.json` | REFUTED for the other five groups | plans 021, 025, 026, issue-737 (`f7e9bd0d2`) and adr-003 (`583585818`) touch no `Main/Composition` file and list no 018 commit as prereq |
| 13 | plan 018's engine facts (written for v1.5.3) | plan 018 "Engine and library facts" | CONFIRMED on v1.4.8 | see section 4.2 |

## 3. Benefit, measured

### 3.1 Which port groups touch the composition surface

A scan of `SP\agg\groups.json` against the per-commit file lists in `SP\agg\classified.jsonl` (output `SP\d1\measure_out.txt`) finds **39 of 331 port groups, in 19 of 36 WPs**, with a commit that touches `Main/Composition/**`, a feature `*Module.cs`, `TAOM.Tests/Composition/**` or lists a plan 018 commit as prereq. After reading every port note (`SP\d1\notes_out.txt`) they split three ways:

- **14 groups in 9 WPs carry module wiring in the port payload: 17 modules** (table 3.2).
- **1 group is plan 018 itself** (WP 118).
- **24 groups touch the surface only in their OUT half or in tests**: the module or composition line belongs to a 1.5.x-only feature the note says to leave (`13fb30f9c` ServerSettings, `726975a52` and `db8f7c10e` FordsOfWar, `5e43518df` MinorClanMigration, `6ae924638` UnlookedForAid, `736fa2fef` LordBanners, `d469914ab` and `b0154d86e` CreatureBandits, `9e2a39f40` TournamentRewards, `a498e357e`/`70320bbe1` RaceAbilities, `e7cdb2e96` ReevesLedger, `4e7efe5a8` HallOfFire, `f382ff6fd` HardQuestions, `7abce5db9` BanditVoices), or they are test, tool or doc payloads (`b9eabd712`, `943ab74c0`, `e0ad0db76`, `d96f349f1`, `6ddcc8f9b`, `ee19f1052`, `e9b28e2ad`, `06252ae2c`, `2719e59e8`, `85c608963`/`2afea8878`). Some of these still couple to 018 if it lands (section 4.4).

### 3.2 The 17 modules and what each costs to hand-wire

Module contents read at each port commit (`SP\d1\modules_out.txt`). Sites assume the 1.4.x convention: one `XIoC.RegisterXFeature(container)` line and one `XIoC.InitializePatchStatics(container)` line in `IoC.cs`, one line per category, behavior or phase call in `SubModule.cs`. Inline registrations would cost more lines. Test files = files under `TAOM.Tests` that name the module class at the port commit (`SP\d1\testrefs.py`).

| WP | Wave | Port commit | Module | Services | Statics | Categories (phase) | Campaign / mission behaviors | OnPhase | IoC.cs sites | SubModule.cs sites | Test files |
|---|---|---|---|---|---|---|---|---|---|---|---|
| 106 | 1 | `ecaa67d26` | MissionStartGuard | 3 | yes | 1 (GameInit) | 0 / 0 | GameInit | 2 | 2 | 2 |
| 106 | 1 | `ecaa67d26` | SkeletonBuffer | 5 | no | 0 | 0 / 1 | MainMenu | 1 | 2 | 1 |
| 106 | 1 | `ecaa67d26` | NameplateCull | 3 | yes | 1 (GameInit) | 0 / 0 | GameInit | 2 | 2 | 2 |
| 106 | 1 | `ecaa67d26` | MapViewRelease | 3 | yes | 0 | 0 / 0 | GameInit | 2 | 1 | 1 |
| 109 | 1 | `c17541f2c` | BattleCorpses | 4 | no | 0 | 0 / 1 | MainMenu | 1 | 2 | 1 |
| 109 | 1 | `a913629cd` | StuckBattle | 2 | no | 0 | 1 / 0 | none | 1 | 1 | 1 |
| 110 | 1 | `84f0459bc` (UNSURE) | ShaderSourceInstall | 2 | no | 0 | 0 / 0 | ProcessLoad, MainMenu | 1 | 2 | 1 |
| 111 | 1 | `eed06b9c7` | BardTownGuard | 0 | yes | 1 (GameInit) | 0 / 0 | none | 1 | 1 | 2 |
| 112 | 1 | `a9f10688f` | AnimMemoryProbe | 3 | no | 0 | 0 / 1 | none | 1 | 1 | 1 |
| 112 | 1 | `639df895b` | LostFooting | 2 | no | 0 | 0 / 1 | none | 1 | 1 | 1 |
| 114 | 2 | `1dc0a8d7c` | MessengerSessionRules | 0 | yes | 1 (GameInit) | 0 / 0 | none | 1 | 1 | 2 |
| 117 | 2 | `346f3e093` | LoadTimeStamps | 6 | yes | 2 (ProcessLoad) | 0 / 0 | none | 2 | 2 | 2 |
| 117 | 2 | `1325c36ea` | ButterLibDistanceMatrix | 2 | no | 0 | 0 / 0 | MainMenu | 1 | 1 | 1 |
| 117 | 2 | `a27c6b338` | XmlMerge | 3 | yes | 1 (ProcessLoad) | 0 / 0 | none | 2 | 1 | 1 |
| 122 | 3 | `ea7cce7bc`..`b747cf398` | ShadowfaxScreens | 7 | yes | 4 (GameInit) | 0 / 0 | GameInit | 2 | 5 | 2 |
| 128 | 5 | `93684c32c` | AlignmentRecruitment | 0 | yes | 1 (ProcessLoad) | 0 / 0 | none | 1 | 1 | 2 |
| 128 | 5 | `d39370059` | TavernSpecialities | 0 | no | 1 (GameInit) | 0 / 0 | none | 0 | 1 | 2 |
| | | | **Total** | | | | | | **22** | **27** | **25** |

- Excluding the UNSURE ShaderSourceInstall: 21 + 25 = 46 sites, 24 test files.
- By wave: wave 1 has 10 modules and 28 sites (IoC 13, SubModule 15) in WPs 106, 109, 110, 111, 112; wave 2 has 4 modules (WPs 114, 117); wave 3 one (WP 122); wave 5 two (WP 128).
- Compile coupling: 10 production patch files in 8 modules carry `[HarmonyPatchCategory(<Module>.<Const>)]` (for example `eed06b9c7:Main/Features/BardTownGuard/Hooks/TavernEmployeesCampaignBehavior_BardCondition_BardTownGuard_Patch.cs:16`; LoadTimeStamps has 3 such files). A hand-wired port must move those constants and edit the patch files too.
- No in-scope module declares a game model or sets `OwnsSaveData` (all 17 read), so the CustomBattle model path and the undecided fail-closed path stay dormant.

### 3.3 What 1.5.x shows

After the merge, 77 non-merge commits touched `FeatureModules.cs` and 65 of them (84%) needed no edit to `SubModule.cs` or `IoC.cs`. The runner did not stop single-owner edits (59 non-merge commits after the merge still edited one of the two files, against 23 between the fork and the merge), and both files kept growing (`SubModule.cs` 2,360 lines and `IoC.cs` 289 at `e45f6df2c`, against 1,969 and 243 on the 1.4.x tip). The benefit is that new feature wiring stops landing in them, not that they shrink.

### 3.4 RepoPaths demand (independent of the runner)

28 PORT, PORT-PARTIAL or UNSURE commits mention `RepoPaths` or list `35007dbb8`, `a0db0558b` or `06252ae2c` as prereq (`SP\d1\repopaths.py`): 10 in wave 1 (WPs 106, 111, 113), 3 in wave 2, 7 in wave 5, 8 in wave 6. The file's base `35007dbb8` sits in WP 126 (wave 5, group issue-612-field-commission-races) and `06252ae2c` in WP 133 (wave 6). **That is a sequencing defect in the map whatever is decided about 018**: the file must land in wave 0. Three port commits also call `RepoPaths.CSharpFiles` (`eb5cf0289` in WP 114, `8caac7da5` and `cd4fb3719` in WP 133), which `03d9db8dc` added with `CodeRoots = { "Main", "Client" }`; `Client/` does not exist on 1.4.x.

### 3.5 Robustness: the runner isolates mission-behavior faults

- 1.4.x: `AddTaomBehavior` (`origin/bannerlord-1.4.5:Main/SubModule.cs:1782`) calls `mission.AddMissionBehavior(behavior)`; only the diagnostic log calls around it are in try blocks. A hand-wired `AddTaomBehavior(new X(IoC.Resolve<...>()))` that throws (a missing registration, a constructor fault) escapes `OnMissionBehaviorInitialize`.
- v1.4.8: `Mission.AfterStart` (`_categories_v1.4.8\MountAndBlade\TaleWorlds.MountAndBlade\TaleWorlds.MountAndBlade\Mission.cs:3799`) calls each submodule's `OnMissionBehaviorInitialize` at `:3815` with no try; `MissionState.FinishMissionLoading` clears `_missionInitializing` (`MissionState.cs:335`) before `AfterStart` (`:345`); `TickLoading` calls `LoadMission()` again while `_missionInitializing` is false (`:221-233`); 1.4.x Patch37 finalizes `Module.OnApplicationTick` with `HandleAndSwallow` (`origin/bannerlord-1.4.5:Main/Features/CrashReport/Hooks/Patch37_CrashReport.cs:54-60`). The full call chain from the application tick to `MissionState` was not traced line by line this session; the pieces above are each read.
- With 018, `FeatureModuleHooks.AddMissionBehaviors` runs each module inside `ModuleRunner.Run`'s try: a throwing module is logged, faulted for the session and reported with a red chat line, and the load continues. Four in-scope modules add mission behaviors (BattleCorpses, SkeletonBuffer, AnimMemoryProbe, LostFooting). This matters given the maintainer's history of infinite loading screens (brief fact 11).

## 4. Cost and risk on 1.4.x

### 4.1 Prerequisites

- **Plan 009 (WP 104, wave 0)**, at its series end through `27fd23bbb`, including `b6cb6ff5d` (localized `ReportPatchFailures`) and `7912fdd8f`. Without it there is no `TryPatchCategory` to hand the runner and the kernel test anchors do not exist.
- **RepoPaths**: absent on 1.4.x. Take `06252ae2c:TAOM.Tests/Infrastructure/RepoPaths.cs` (members `RepoPath`, `ReadSource`, `StripComments`, `MentionsOutsideComments`), plus `CSharpFiles` from `03d9db8dc` with `CodeRoots = { "Main" }`. Do not copy the tip version verbatim.
- `TAOM.Tests.csproj` has no `<PathMap>` (only `Main/TAOM.csproj:6` does), so `RepoPaths`' `[CallerFilePath]` locator holds.

### 4.2 Engine and build facts the infrastructure needs, verified for 1.4.x

- `MBGameModel<T> : GameModel` with `Initialize(T)`: `C:\Users\mikew\.taom-src\v1.4.8\TaleWorlds.Core.MBGameModel`1.cs:3-9`.
- `IGameStarter.AddModel(GameModel)` and `AddModel<T>(MBGameModel<T>)`: `TaleWorlds.Core.IGameStarter.cs:9,11`.
- `CampaignGameStarter(GameMenuManager, ConversationManager)` (`TaleWorlds.CampaignSystem.CampaignGameStarter.cs:23`), `AddBehavior` ignores null (`:35-41`), `AddModel<T>` chains the previous model (`:76-81`). `FeatureModuleHooksTests` builds `new CampaignGameStarter(null, null)`, which this constructor accepts.
- `BasicGameStarter()` (`TaleWorlds.MountAndBlade.BasicGameStarter.cs:12`) and its `AddModel<T>` (`:34-39`).
- `CampaignBehaviorBase()` sets `StringId = GetType().Name` (`TaleWorlds.CampaignSystem.CampaignBehaviorBase.cs:12-14`); `SyncData` is abstract (`:24`).
- `InquiryData` positional constructor (`TaleWorlds.Library.InquiryData.cs:33`); `InformationManager.DisplayMessage` (`:61`) and `ShowInquiry` (`:91`). 1.4.x already calls `ShowInquiry(new InquiryData(` at `Main/SubModule.cs:647`.
- Startup notice rule on v1.4.8: the initial screen clears the chat log (`_modules_build\Native__TaleWorlds.MountAndBlade.GauntletUI.cs:2552`, class `GauntletInitialScreen` at `:2493`); `GauntletQueryManager.CreateQuery` (`:3917`) queues an inquiry that is not equal to the active or a queued one (`:3925`, `:3935`); `Module.OnBeforeGameStart` (`Module.cs:1721`) clears messages at `:1757`, before `OnGameStart`. So `NoticeFor` (hold, inquiry, red line) is correct on v1.4.8.
- Build: DryIoc 4.8.8 (`Main/TAOM.csproj:100`), `net472`, `LangVersion 10.0`, `<Nullable>enable</Nullable>` (`Directory.Build.props:3,5,6`), `InternalsVisibleTo` block at `Main/TAOM.csproj:117-122`. The merge-state code uses no default interface members (the interface is gone).

### 4.3 Exact single-owner edits on 1.4.x (line numbers on `origin/bannerlord-1.4.5` before WP 104; find by text)

- `Main/IoC.cs` (243 lines): members after `private static IContainer _container;` (`:77`); runner block after `Features.UncapturableHeroes.UncapturableHeroesIoC.RegisterUncapturableHeroesFeature(container);` (`:191`) and before `_container = container;` (`:193`); `modules.InitializeStatics(container);` after `CareerSystemIoC.InitializeCalculators(...)` (`:205`), before `private static void RegisterCoreServices(` (`:208`); pilot deletion `:106`. `IoC.Configure()` runs at `SubModule.cs:114`, before every anchor; `IoC.Dispose()` (`IoC.cs:238-242`, called at `SubModule.cs:1948`) nulls `_container`, which `IoC.Resolver` relies on.
- `Main/SubModule.cs` (1,969 lines): ProcessLoad after Patch42 (`:520`); MainMenu inside `if (!_basicTableauGuardApplied)` (`:537`) after Patch55 (`:542`, today inside a try that WP 104 converts); `AddGameStartContent` after the `CampaignGameStarter` branch closes (`:732`; `OnGameLoaded` at `:743`; 1.4.x has no `RegisterCustomBattleModels`); GameInit after `Patch69_TournamentEndGuard` (`:1709`), before `ManualPatchApplicator.ApplyAll` (`:1715`); FirstMission inside `if (!_missionTimePatchesApplied)` (`:1765-1768`); mission behaviors after `AgentColorStoreCleanupBehavior` (`:1857`), before `MissionDiagnosticBehavior` (`:1868`); pilot deletion `:1244-1247`.

### 4.4 Ordering and coupling risks

- **Pilot, order-free on 1.4.x**: `RegisterWandererAllegianceFeature` has one caller (`IoC.cs:106`); the dialog behavior's lines run at priority 110 over vanilla's 100 (`Main/Features/WandererAllegiance/Hooks/WandererAllegianceDialogBehavior.cs:25`); its `SyncData` is empty (`:41`); `LotrIssueSuppression.SuppressAll` (`SubModule.cs:1303`) removes only vanilla issue types in the `TaleWorlds.CampaignSystem.Issues` and `SandBox.Issues` namespaces (`Main/Features/LotrIssues/LotrIssueSuppression.cs:161-162, 170-200`). Save data untouched.
- **End-of-phase placement**: a ported module runs after every hand-wired feature of its phase, as on 1.5.x, so its relative order matches the line where it was tested. Hand-wiring forces a position choice per port.
- **Kernel test anchor drift**: the mission-behavior anchor of `Kernel_SubModule_CallsEachRunnerHookOnce_AtTheEndOfItsFeatureBlock` moved three times on 1.5.x (`e0ad0db76` to the `BattleActionBarMissionView` add, `c91380274` to the view factory, `d96f349f1` to `MissionTickProfilerBehavior`). On 1.4.x, WP 119 (`e0ad0db76`, deletes `AgentColorStoreCleanupBehavior`) and WP 129 (`d96f349f1`, deletes `AddTaomBehavior(new BattleActionBarMissionView())`) remove lines that would be that anchor. Their port notes say "skip FeatureModulesTests", which is right only without 018; with 018 they must re-anchor the test.
- **Other couplings if 018 lands**: WP 133 `6ddcc8f9b` can keep scanning `FeatureModules` as well as the assembly; WP 133 `06252ae2c`'s `FeatureModulesTests` hunk applies; WP 133 `e9b28e2ad` tags `FeatureModuleHooksTests` only if plan 010's no-game run is ported; WP 111's `MainMenuStep` goes into the existing `Main/Composition`; WP 129 `b0154d86e`'s `NoCustomBattleModelSlot` test needs `RegisterCustomBattleModels`, absent on 1.4.x, so it stays out.
- **Dormant fail-closed semantics**: decision 48 (`DECISIONS.md:54`) leaves "what fail closed means at campaign start" open until the first `OwnsSaveData` module. None of the 17 owns save data. Add it as a STOP for any future module.
- **Fault notice text is literal English** in `ModuleRunner.TakeFaultSummary` and `FeatureModuleHooks` (also at the 1.5.x tip). Low risk; it appears only when a module faults.
- **Runtime cost**: no per-frame work; one loop per lifecycle step and one per mission over at most 18 modules. Negligible against the measured load times.
- **No in-game evidence on v1.4.8.** The 1.5.x index records the in-game check of 018 itself as owed (`plans/README.md:35`).
- **Process**: 1.4.x hand-edits `CHANGELOG.md` and enforces `v2.0.28` subjects; drop every 1.5.x CHANGELOG hunk and re-subject. 1.4.x has no CI (`ccef172ad`), so every gate runs locally.

### 4.5 Test delta on 1.4.x

+42 test methods against the post-WP-104 baseline: `RepoPathsTests` 6 (at `06252ae2c`), `GameModelOverrideBindingTests` +1 (`ParkedModels_AreRealModels_ThatSubModuleDoesNotRegister`, 3 to 4), `FeatureModulesTests` 12, `ModuleRunnerTests` 16, `FeatureModuleHooksTests` 7 (all at `0f3af9577`); `WandererAllegianceWiringTests` stays at 6 (three removed, three added). One more if an adapted `CSharpFiles_WithNoRoots` test comes with `CSharpFiles`.

## 5. Recommendation and sequencing

1. Wave 0, after WP 102 and WP 104: a new package (call it **WP 104b**, "RepoPaths and the plan 018 runner"), single owner of `IoC.cs` and `SubModule.cs` while it runs.
2. WP 118 keeps plans 021, 025, 026, issue-737 and adr-003 in wave 2, without the plan-018 group.
3. Re-point the module-carrying WPs (106, 109, 110, 111, 112, 114, 117, 122, 128) at "port the module and add one line to `FeatureModules.All`".
4. Give WP 119 and WP 129 the kernel-anchor duty.
5. Remove the `RepoPaths` file part from WP 126 (`35007dbb8` keeps its field-commission fix) and the `MentionsOutsideComments` part from WP 133 (`06252ae2c`).

Why not (b): it buys nothing (a) does not, costs about 49 single-owner edits, 10 patch-file edits and 25 test rewrites across 9 WPs (28 of the edits in parallel wave-1 WPs on two single-owner files), leaves mission-behavior ports unguarded, and every later 1.5.x fix to these 17 features needs translation again.

Recommended skills for the orchestrator (this verifier cannot run them): `/freeze` on the 104b file set during execution; `/verify` and `/deep-review` before merging 104b (C# across many files, both single-owner files); optionally `/new-adr` for the composition-root decision on 1.4.x (plan 018's maintenance notes list the ADR as deferred). No paid review is implied.

## 6. Plan inputs for WP 104b (also in the structured result)

See the `plan_inputs` field of the structured result; it is the authoritative, self-contained version.

## 7. Reusable fallback paragraph for other plans

> If `Main/Composition/FeatureModules.cs` exists on the branch, port the 1.5.x `XModule.cs` as is (keep its `PatchCategory` constants, since patch attributes read them), append `new Features.<Folder>.XModule(),` to the end of `FeatureModules.All` (earlier only for a named ordering constraint, pinned by an index test in `FeatureModulesTests`), keep the 1.5.x wiring tests, and run `FeatureModulesTests` plus the feature's tests: the double-wiring guards fail while any hand-wired line for the same type remains. Otherwise, translate each module member into 1.4.x hand wiring: `RegisterServices` becomes a static `XIoC.RegisterXFeature(IContainer container)` in the feature folder, called from `Main/IoC.cs` before `_container = container;` (example: `Features.UncapturableHeroes.UncapturableHeroesIoC.RegisterUncapturableHeroesFeature(container);`, `IoC.cs:191` on `origin/bannerlord-1.4.5`); `InitializeStatics` becomes `XIoC.InitializePatchStatics(container)` in the eager block after `_container = container;` (example: `Features.Refuge.RefugeIoC.InitializePatchStatics(container);`, `IoC.cs:200`); each `PatchCategoryDecl` becomes `TryPatchCategory("<category>")` (plan 009; before it lands, `_harmony.PatchCategory("<category>")`) in the hook of its phase: ProcessLoad at the end of `OnSubModuleLoad` next to Patch42 (`SubModule.cs:520`), MainMenu inside `if (!_basicTableauGuardApplied)` (`:537-547`), GameInit in the `OnGameInitializationFinished` batch before `ManualPatchApplicator.ApplyAll` (`:1709-1715`), FirstMission inside `if (!_missionTimePatchesApplied)` (`:1765-1768`); a `CampaignBehaviorDecl` becomes `campaignStarter.AddBehavior(...)` in `RegisterCampaignLifeBehaviors` before `SuppressAll` (example: WandererAllegiance, `SubModule.cs:1247`); a `MissionBehaviorDecl` becomes `AddTaomBehavior(...)` in `OnMissionBehaviorInitialize` before the MissionDiagnostic tail (example: `AgentColorStoreCleanupBehavior`, `SubModule.cs:1857`), wrapped in a try that logs and skips, because an unguarded throw there reloads the mission every tick on v1.4.8; `OnPhase(p)` becomes a direct call in the same hook. Move each `internal const string` category from the module class onto its patch class and update the `[HarmonyPatchCategory(...)]` attributes; rewrite tests that name the module class as `RepoPaths.ReadSource("Main/IoC.cs", stripComments: true)` / `("Main/SubModule.cs", ...)` text asserts (pattern: `TAOM.Tests/Features/WandererAllegiance/WandererAllegianceWiringTests.cs` on `origin/bannerlord-1.4.5`). Line numbers are the 1.4.x tip before WP 104; find anchors by text. `IoC.cs` and `SubModule.cs` are single-owner: one WP edits them at a time.

## 8. Unverified or open

- Any in-game behaviour of the runner on v1.4.8 (none exists yet), and the pilot's refusal dialogue after the move.
- Line numbers after WP 104: they will move; every anchor above must be re-found by text.
- Whether WP 104 keeps `b6cb6ff5d`'s localized `ReportPatchFailures` strings verbatim; the kernel test's three report anchors must copy whatever 1.4.x ends up with.
- Whether `FeatureModuleHooksTests` (it constructs real starters) runs green, rather than inconclusive, on the laptop: not run (no builds or tests in this run). The game is installed on both machines.
- The exact call chain from `Module.OnApplicationTick` to `MissionState`'s tick in section 3.5 (each end was read; the middle was not traced).
- The maintainer's decision on 018 for 1.4.x: not recorded anywhere read this session.
