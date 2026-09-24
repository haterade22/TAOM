# Verify COMP-01, lens: does it reproduce

Checker: fresh adversarial pass, baseline `b2e387db` (read via `git show b2e387db:Main/SubModule.cs`,
2,148 lines; the working tree HEAD is `4b5662b2`, not used). Engine: the v1.5.3 decompile under
`E:/Decompiled_Bannerlord/_categories_v1.5.3/`. Harmony: `ilspycmd` on
`~/.nuget/packages/lib.harmony/2.4.2/lib/net472/0Harmony.dll` (the pinned `Lib.Harmony 2.4.2`,
`Dependencies/TAOM.Dependencies.csproj:70`). No build or test was run.

## COMP-01: Guard every Harmony category apply

**Outcome: CONFIRMED** (mechanism and counts re-read link by link; the "silent / nothing logged"
part of the harm is wrong and is corrected below).

### Links re-read

1. **Counts.** Hand count of `git show b2e387db:Main/SubModule.cs | grep -n PatchCategory(`, each site
   checked for an enclosing `try`: OnSubModuleLoad 13 bare (223, 259, 291, 292, 302, 487, 528, 532,
   538, 543, 549, 565, 624) and 11 guarded (199, 320, 362, 372, 397, 410, 433, 452, 475, 559, 575);
   OnGameInitializationFinished 50 bare (1510 to 1745) and 8 guarded (1493, 1521, 1537, 1813, 1826,
   1850, 1861, 1863); OnMissionBehaviorInitialize 1 bare (1922); OnBeforeInitialModuleScreenSetAsRoot
   1 guarded (646). Total 64 bare, 20 guarded: matches the lane. June `141b749` has 46 calls, 45 of
   them outside a `try`, so +19 holds.
2. **Harmony throws on a drifted binding.** 0Harmony 2.4.2 `Harmony.PatchCategory(Assembly,string)`
   runs `value2.Do(type => CreateClassProcessor(type).Patch())` with no catch, so the first failing
   class aborts the rest of its category. `PatchClassProcessor.PatchWithAttributes` throws
   `ArgumentException("Undefined target method for patch method ...")` when the attribute target does
   not resolve, and `ReportException` rethrows it as `HarmonyException`; a `TargetMethod` returning
   null also throws (`"returned an unexpected result: null"`). Only a `Prepare` returning false or a
   `Cleanup` returning null skips silently. Concrete bare examples with a string or `nameof` binding
   and no `Prepare`: `Patch65_LandlessCultureSpawnGuard.cs:40`
   (`[HarmonyPatch(typeof(HeroSpawnCampaignBehavior), "SpawnLordParty")]`, applied bare at
   SubModule 1596) and `Main/Features/Mcm/Hooks/Patch41_McmLayoutFix.cs:26` (applied bare at 223).
   A per-file scan of the 64 bare categories found `Prepare` only in `Patch7_FactionMap` (3 of 4
   classes) among the late batch.
3. **Boot path (13 sites).** `Module.Initialize` (Module.cs:261, an `[MBCallback]`) calls
   `LoadSubModules(modules, loadNewModules: false)` (282), which calls `InitializeSubModuleBases`
   (1130); that wraps each `OnSubModuleLoad` in a catch that prints, sets the crash-report string and
   `throw new Exception()` (Module.cs:204-220): exactly as cited. TAOM's own crash capture does not
   rescue it: `Patch37`'s `MBSubModuleBase.OnSubModuleLoad` finalizer
   (`Main/Features/CrashReport/Hooks/Patch37_CrashReport.cs:113-120`) patches the base method body,
   not TAOM's override; the `Native2ManagedPatcher` finalizer on
   `CoreCallbacksGenerated.Module_Initialize` (`CoreCallbacksGenerated.cs:1080-1084`) is attached at
   SubModule 203, while that frame is already executing, so it cannot cover the in-flight call. The
   rest of TAOM's load after the throwing line is skipped (BannerColor `Initialize` calls 590-611,
   `Patch42` 624, the "TAOM loaded successfully!" message 626) and `Module.Initialize` stops before
   `SaveManager.InitializeGlobalDefinitionContext` (Module.cs:300). What native code does with the
   rethrown exception is UNVERIFIED, but the engine's explicit rethrow marks it fatal.
4. **Late batch (50 sites).** `_gameInitPatchesApplied = true` is set at 1465, before the batch.
   `MBGameManager.OnGameInitializationFinished` loops the submodules with no catch
   (MBGameManager.cs:110-115), as the last statement of `Campaign.OnInitialize` (Campaign.cs:1471).
   A throw at, for example, 1510 skips the other 49 bare categories, both watchdog starts
   (1818, 1831), the exit-stall sampler (1842), `ManualPatchApplicator.ApplyAll` (1869) and the census
   (1876-1908). Not in the lane text: it also skips every later submodule's
   `OnGameInitializationFinished` and the engine's own `SkeletonScale` loop that follows the submodule
   loop (MBGameManager.cs:116 onward).

### Corrected evidence (the harm after the first throw)

The lane marks this link UNVERIFIED and then states "nothing logged". On my reading it is not silent:

- The load runs under `Module.OnApplicationTick`: `GlobalGameStateManager.OnTick` (Module.cs:527)
  drives `GameLoadingState.OnTick` (GameLoadingState.cs:22-31), which calls
  `SandBoxGameManager.DoLoadingForGameManager` step 3 (SandBoxGameManager.cs:83-101), then
  `Game.DoLoading`, then `Campaign.DoLoadingForGameType(InitializeFirstStep)`, then `Game.Initialize`
  (Campaign.cs:1662), then `Campaign.OnInitialize`.
- `Patch37` puts a finalizer on `Module.OnApplicationTick` (Patch37_CrashReport.cs:54-61). With crash
  capture on (the default: `EnableCrashCapture ?? true` at SubModule 195, and
  `CrashReportPatchHelper.cs:29-54`), it swallows the exception after `CrashReportService.HandleException`
  writes the report to the log, writes a crash bundle and shows the notifier
  (`Main/Features/CrashReport/CrashReportService.cs:78-146`). So the first failure is loud.
- Neither step counter advances on a throw (GameType.cs:61-73, GameManagerBase.cs:213-217), so step 3
  runs again on the next tick. For a **saved game** `_loadedGameResult` was set to null at
  SandBoxGameManager.cs:96, before `DoLoading` at :100, so the retry calls
  `Game.LoadSaveGame(null, ...)`, which dereferences `loadResult.Root` (Game.cs:165-169) on every
  tick; the throttle then suppresses repeat bundles. Likely result: the save never finishes loading
  (UNVERIFIED in game). For a **new game** the retry builds a fresh campaign and TAOM returns early at
  1464, which is the half-patched session the lane describes, provided the second
  `Campaign.OnInitialize` completes (UNVERIFIED).
- With crash capture off, `HandleAndSwallow` returns the exception (CrashReportPatchHelper.cs:38-39),
  so it reaches native code (UNVERIFIED, probably a crash).

What still holds: one drifted binding at any of the 64 bare sites costs much more than one feature.
It fails the boot (13 sites) or fails the campaign load with a crash report (50 sites). The truly
silent case is narrower: the next game init in the same process after a throw that was swallowed.

**Fix note (not a finding):** `Harmony.PatchCategory(string)` picks its assembly from
`new StackTrace().GetFrame(1)` (0Harmony 2.4.2, `Harmony.PatchCategory`). A `TryPatch` helper inside
`SubModule` still resolves to TAOM.dll, but calling `PatchCategory(typeof(SubModule).Assembly, category)`
removes the dependence on stack depth.

**Impact today:** MED. No binding drifts on the pinned v1.5.3 today, but a force-bump or a changed
dependency turns a one-feature failure into a failed boot or a failed load. P1 stays "no" as the lane
says.

## What I did not cover

- Native behavior after the exception leaves `Module.Initialize` or reaches native code with crash
  capture off (native code; not readable here).
- Whether the new-game retry of `Campaign.OnInitialize` completes (not traced line by line) and
  what `CrashNotifier.Notify` lets the player do from a stuck loading screen.
- The lane's Risk line about text tests (`FieldCampWiringTests.cs:191`), and its Effort and Fix sketch
  apart from the stack-frame note above.
- A per-class review of every class in the 64 categories. The `Prepare` scan was a regex over each
  category's files, and it missed categories declared through a constant (Patch84, 86, 87, 88).
