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

- 2026-11-04: ported from the 1.5.x line (plan 018, #662): the runner, WandererAllegiance as the
  first module, and the porting rules above.

## GitHub Issue

- **Issue:** #662 on the 1.5.x line (plan 018, composition root first steps); this is its 1.4.x
  port, `plans/105-composition-root.md`.
- **Status:** ported; in-game check owed.
