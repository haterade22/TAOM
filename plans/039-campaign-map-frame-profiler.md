# Plan 039: Attribute campaign-map frame time and allocation to TAOM's per-frame map code

> **Executor instructions**: Follow this plan step by step. Run every verification command and
> confirm the expected result before moving on. If anything in "STOP conditions" occurs, stop and
> report; do not improvise. Work in the worktree and on the branch you were given. The orchestrator
> keeps `plans/README.md`; do not edit it.
>
> **Drift check (run first)**: this plan builds on plan 028's code, so your branch starts from the
> tip of plan 028's branch, not from the planned-at commit. First run
> `git merge-base --is-ancestor 765d3759 HEAD && echo ANCESTOR` (765d3759 is plan 028's reviewed tip,
> which this plan's excerpts were checked against); it must print `ANCESTOR`, otherwise STOP. Then run
> `git diff --stat 765d3759..HEAD -- Main/SubModule.cs Main/Features/MapPerf Main/Features/MissionPerf/AllocationCounter.cs Main/Features/MissionPerf/BehaviorTickTable.cs Main/Features/MissionPerf/TickProfilerTranspiler.cs Main/Features/MissionPerf/TickProfileLines.cs Main/Features/BattleLoadDiagnostics/BattleLoadDiagnosticsSettings.cs Main/Features/BattleLoadDiagnostics/IBattleLoadDiagnosticsSettingsProvider.cs Main/Features/BattleLoadDiagnostics/BattleLoadDiagnosticsSettingsProvider.cs Main/Features/CoopInterop/CoopSettingsRelevance.cs Main/Features/TimeAcceleration/ITimeControlAdapter.cs Main/Features/TimeAcceleration/ITimeAccelerationSettingsProvider.cs Main/Features/CrashReport/Hooks/Patch37_CrashReport.cs Main/Features/WarOfTheRingMomentum/UI/MomentumIndicatorMapView.cs Main/Features/FieldCamp/UI/FieldCampMapView.cs Main/Features/RealmBorders/UI/RealmBordersMapView.cs Dependencies/Foundation/PatchShieldPolicy.cs Dependencies/Foundation/PatchShield.cs Dependencies/SubModule.cs TAOM.Tests/Features/MapPerf TAOM.Tests/Features/BattleLoadDiagnostics/BattleLoadDiagnosticsSettingsProviderTests.cs TAOM.Tests/Features/CoopInterop/SettingsFingerprintTests.cs TAOM.Tests/Features/Mcm/SettingRequireRestartPostureTests.cs TAOM.Tests/Migration/ReflectionSiteBindingTests.cs docs/features/map-perf-profiler.md docs/reference/harmony-patch-registry.md docs/reference/feature-map.md docs/reference/taleworlds-api-snapshot/reflection-sites.md docs/features/coop-interop.md docs/features/bannerlord-together-compat.md docs/features/mcm.md docs/reviews/lessons/harmony-il.md`.
> Expected drift: any plan 028 follow-up after 765d3759, and plans 040 and 041 if the
> orchestrator merged them into your base (both add MCM settings, so the settings counts, the
> restart allowlist and the `mcm.md` sentences quoted below may already have moved; Step 2 says
> how to adapt). For any other change in these paths, compare the "Current state" excerpts with
> the live code; a mismatch in an excerpt this plan relies on is a STOP condition.
>
> **PatchShield decision**: the orchestrator tells you whether the maintainer chose option A (five
> PatchShield exclusions, this plan's default text) or option B (two exclusions) for decision D1
> (Design, "PatchShield"). No word means option A.
>
> **Decided 2026-10-03 (D13): option B.** The maintainer chose option B: `Campaign.Tick` and
> `CampaignEvents.Tick` are the only excluded targets, and `MapState.OnTick`, `Campaign.RealTick` and
> `MapScreen.OnFrameTick` keep the shield. The option A text below stays as the record of the alternative; the
> branch's code, tests and docs carry option B. The same decision took `Mission.OnTick` and `Mission.OnPreTick`
> off plan 028's list.
>
> **Decided 2026-10-03 (FOR-MIKE 16r, with the Codex review):** the speed class is read from
> `Campaign.GetSimplifiedTimeControlMode()` (through `ITimeControlAdapter.SimplifiedTimeControlMode`), so a Stoppable
> mode while the main party waits reads `Stop`; and the six core hooks are looked at again at each session start, window
> start and measuring session end, a lost one stopping measuring behind one `[MapProfiler]` warning and a
> `reason=hooksLost` summary. The
> "Speed class" paragraph and the line contract below are the first build's record of both.

## Status

- **Priority**: P2
- **Effort**: M
- **Risk**: MED (a call-site transpiler on `CampaignEvents.Tick` and prefix/postfix pairs on five
  per-frame campaign methods, all default off; plus PatchShield exclusions that apply to every
  player: under option A, three of them change where a missing-API exception inside the campaign
  tick or the map screen's frame is caught, see "PatchShield")
- **Depends on**: `plans/028-mission-tick-profiler.md` (it shares 028's allocation counter, per-type
  accumulator, transpiler helper and line conventions), built on the tip of plan 028's branch that
  the orchestrator gives you; re-anchored on 028's reviewed tip before execution
- **Category**: perf / diagnostics
- **Planned at**: commit `0912e1b7` (program branch), 2026-10-02; plan 028's code read at
  `e64529b3` and re-checked at its reviewed tip `765d3759` (between the two only
  `TickProfileLines.cs` gained members, and the registry's Patch97 section and
  `docs/reviews/lessons/harmony-il.md` changed; `Dependencies/` and `Main/SubModule.cs` did not,
  so the `SubModule.cs` line numbers below hold at both)
- **Baseline at that commit**: at `0912e1b7`, dotnet `Failed! - Failed: 1, Passed: 12345, Skipped: 2,
  Total: 12348` (net472), failing `EveryLanguage_DeclaresARowForEveryEnglishKey` (English keys without
  rows in the other languages; the paid translator run waits on the maintainer). Plan 028's first
  commit `b3bac1a9` added 78 results (the orchestrator re-ran it: 12,423 passed of 12,426, the one
  failure the same); your Step 1 records your own base. Python suite at the base: `Ran 2962 tests`,
  `FAILED (failures=3, skipped=8)`, failing `test_applying_every_spec_is_a_no_op`,
  `test_the_committed_career_file_is_what_the_rule_derives`, `test_default_is_on_the_e_drive`; this
  plan touches no `tools/` file and does not run it. The RefAsm unit step (hosted CI's second step)
  fails three tests at the base: `EveryLanguage_DeclaresARowForEveryEnglishKey`,
  `Patch93_HasTheSevenPatchesInItsCategory` and `Patch94_HasTheMapIconNoParleyAndNoJoinPatches`
  (the last two a `FileNotFoundException` for `TaleWorlds.MountAndBlade.View`); plan 028's executor
  recorded `Failed! - Failed: 3, Passed: 10107, Skipped: 28, Total: 10138` on its branch. The RefAsm
  binding gate (hosted CI's third step) has not been run locally in this programme, so its base
  failure set is unknown: Step 1 records it.
- **Issue**: filed by the orchestrator before execution (draft title "diagnostics: attribute
  campaign-map frame time to TAOM's per-frame map code")

## Why this matters

On the maintainer's desktop a new campaign ran at 7 to 8 fps in fast-forward while lords and
villagers spawned in, with `Campaign.RealTick` averaging 20 ms per frame
(`docs/migration/v1.5.2-impact.md:147`): RealTick explains about 20 of each frame's ~130 ms, and
nothing says where the other ~110 ms went, nor how much of the frame is TAOM's code (eight
`CampaignEvents.TickEvent` listeners, three map views, `SubModule.OnApplicationTick`, and Harmony
patches on the per-frame map methods). The steady map on that machine is fast (fast-forward median
169.8 fps over 648 windows of the 18 sessions on disk), so the cost shows on slower CPUs and in the
spawn-in phase, where plan 037's campaign hot-path fixes must be judged by a number. This plan adds
the map's counterpart of plan 028's mission profiler: off by default and installing no patch unless
turned on, it writes a `[MapProfile]` line every 5 s of wall clock while the map ticks, splitting
the frame into the campaign tick, the tick-event dispatch (per listener, TAOM's and vanilla's), the
map screen (with TAOM's map views per type), TAOM's application tick and everything else, with
main-thread allocation, and a `[MapProfileSummary]` line when the campaign session ends.

## Current state

### Plan 028's primitives this plan reuses (read at `e64529b3`; reuse, do not change)

- `Main/Features/MissionPerf/AllocationCounter.cs`: `internal static class AllocationCounter` with
  `public static bool Available` and `public static long ReadOrZero()` (the CLR's per-thread
  `GC.GetAllocatedBytesForCurrentThread()`, bound by reflection; 0 when absent). Per thread: called
  on the main thread it measures main-thread allocation only.
- `Main/Features/MissionPerf/BehaviorTickTable.cs`: `public sealed class BehaviorTickTable`, keyed
  by `System.Type`, three layers (open frame, window, and a "mission" layer that this plan uses as
  the campaign session layer). Public members, verbatim signatures:
  ```csharp
  public int SlotFor(Type type)
  public void Record(int slot, long elapsedTicks, long allocBytes)
  public void FoldFrame()
  public void ResetFrame()
  public void ResetWindow() => Array.Clear(_window, 0, _count);
  public void ResetMission() => Array.Clear(_mission, 0, _count);
  public IReadOnlyList<BehaviorTotal> WindowTop(int n, long ticksPerSecond) => Top(n, ticksPerSecond, _window);
  public IReadOnlyList<BehaviorTotal> FrameTop(int n, long ticksPerSecond) => Top(n, ticksPerSecond, _frame);
  public IReadOnlyList<BehaviorTotal> MissionTop(int n, long ticksPerSecond) => Top(n, ticksPerSecond, _mission);
  ```
  Its summary: "Main thread only, NOT thread-safe". `Top` orders by total ticks, ties by ordinal
  name, lists only slots with calls. `public sealed class BehaviorTotal(string name, double ms, int calls,
  double maxMs, long allocBytes)` with properties `Name`, `Ms`, `Calls`, `MaxMs`, `AllocBytes`.
- `Main/Features/MissionPerf/TickProfilerTranspiler.cs`: `internal sealed class CallSwap(MethodInfo target,
  MethodInfo helper)` and
  ```csharp
  internal static List<CodeInstruction> Rewrite(
      IEnumerable<CodeInstruction> instructions,
      IReadOnlyList<CallSwap> swaps,
      string methodLabel,
      IModLogger? logger,
      out int swapped)
  ```
  Each swap must match (`CodeInstruction.Calls(target)`) exactly once, else the whole method is
  returned unmodified with one warning and `swapped = 0`; a helper must be static, return the
  target's return type and take the target's declaring type followed by the target's parameters
  (`Fits`). The swap mutates the existing instruction's opcode to `call` and its operand in place.
  Its warnings are built by `TickProfileLines` and therefore carry the `[TickProfiler]` tag (for
  example `[TickProfiler] CampaignEvents.Tick: MbEvent`1.Invoke matched 0 times, expected 1;
  CampaignEvents.Tick left vanilla, so the profiler records nothing for it`); this plan logs its own
  `[MapProfiler]` consequence line beside it.
- `Main/Features/MissionPerf/TickProfileLines.cs`: the 028 line conventions this plan copies:
  `CultureInfo.InvariantCulture`; ms as `0.00`; KB as `bytes / 1024` (integer division) or `na` when
  the counter is unavailable; `t` as whole seconds `0`; `top=none` when empty; status lines carry a
  tag that is not a data tag; exception messages quoted with `[` and `]` turned into `(` and `)`.
  Its number helpers are private: duplicate the five one-liners (`Num`, `Seconds`, `Int`, `Kb`,
  `Quote`) in this plan's line class rather than editing 028's file.
- `Main/Features/BattleLoadDiagnostics/BattleLoadDiagnosticsSettings.cs` at `e64529b3`, the
  Mission Performance group, lines 63-71:
  ```csharp
  [SettingPropertyGroup("Mission Performance")]
  [SettingPropertyBool("Enable Tick Profiler", Order = 1, RequireRestart = true,
      HintText = "Off by default. Times every mission behaviour's tick ...")]
  public bool EnableTickProfiler { get; set; } = false;

  [SettingPropertyGroup("Mission Performance")]
  [SettingPropertyInteger("Tick Profiler Top Behaviours", 1, 20, Order = 2, RequireRestart = false,
      HintText = "How many mission behaviours each [TickProfile] and [TickSummary] line lists, slowest first. Default 8. Read at each mission start.")]
  public int TickProfilerTopN { get; set; } = 8;
  ```
  The provider (`BattleLoadDiagnosticsSettingsProvider.cs:70-74`):
  ```csharp
  public bool TickProfilerEnabled =>
      BattleLoadDiagnosticsSettings.Instance?.EnableTickProfiler ?? false;

  public int TickProfilerTopN =>
      ValidateTickProfilerTopN(BattleLoadDiagnosticsSettings.Instance?.TickProfilerTopN ?? DefaultTickProfilerTopN);
  ```
  It is registered `Reuse.Singleton` in `BattleLoadDiagnosticsIoC.cs` (called from `Main/IoC.cs:178`).
  This plan reuses `TickProfilerTopN` for the map lines (no second top-N setting) and adds one toggle.
- `Main/SubModule.cs` at `e64529b3` (single-owner; exactly the two edits of Step 8). Plan 028
  installs its profiler inside the once-per-process block (after line 1922). The anchors this plan
  uses, lines 1553-1565:
  ```csharp
  // Armour acquisition (docs/features/armour-acquisition.md): every game init too, for the same reason, and
  // before a new game's workshops cache their items (OnNewGameCreatedPartialFollowUp runs after this hook).
  // Only a campaign has the markets, workshops and loot the gate reaches.
  IoC.Resolve<Features.ArmourAcquisition.IArmourGateService>().ApplyGating(game?.GameType is Campaign);

  // Harmony patches are process-global (applied to methods, persist across games). Apply this
  // whole per-game-init patch block ONCE per process ...
  if (_gameInitPatchesApplied) return;
  _gameInitPatchesApplied = true;
  ```
  and the end of `OnGameEnd` (the FactionUI `try` at lines 842-846, the method's closing brace at
  847, `OnGameStart` at 849):
  ```csharp
  try
  {
      IoC.Resolve<Features.FactionUI.FactionScreen.FactionScreenLauncher>()?.ResetForGameEnd();
  }
  catch { /* teardown is best-effort, never break OnGameEnd */ }
  }   // closes OnGameEnd

  protected override void OnGameStart(Game game, IGameStarter gameStarterObject)
  ```
  `OnApplicationTick` (line 2192 at `e64529b3`, 2177 at `0912e1b7`):
  ```csharp
  protected override void OnApplicationTick(float dt)
  {
      _timeAccelerationService?.OnTick();
      _factionUiTicker?.Tick(dt);
      // Shader pre-compilation walk ...
      var runner = _shaderRunner;
      if (runner != null && runner.IsActive) { ... }
  }
  ```
  The file is `namespace TAOM;`, `public class SubModule : MBSubModuleBase`.
  `SubModule.cs:2000` (inside `SettingsFingerprintLog.WriteAcross(...)`) already reads
  `BattleLoadDiagnosticsSettings.Instance` in `OnGameInitializationFinished`, and
  `SettingRequireRestartPostureTests`' summary records that `GlobalSettings<T>.Instance` is null only
  "until MCM's own OnBeforeInitialModuleScreenSetAsRoot", which precedes every game init: the toggle
  is readable where Step 8 reads it.

### What TAOM runs per campaign-map frame (read at `0912e1b7`; observed, not modified)

`CampaignEvents.TickEvent` listeners (`git grep -n "CampaignEvents.TickEvent.AddNonSerializedListener" -- Main`):
`FieldCommission/Hooks/FieldCommissionBehavior.cs:51`, `FieldCamp/Hooks/FieldCampCampaignBehavior.cs:53`,
`Enlistment/Hooks/EnlistmentMaintenanceBehavior.cs:46`, `Refuge/Hooks/RefugeCampaignBehavior.cs:51`,
`Messengers/MessengerCampaignBehavior.cs:449`, `RealmBorders/Hooks/RealmBordersCampaignBehavior.cs:52`
(a lambda, `_ => EnsureMapView()`), `QuickActions/Hooks/InventorySearchCampaignBehavior.cs:40`,
`SupplyLines/Hooks/SupplyLinesCampaignBehavior.cs:54` (all under `Main/Features/`). Every one passes
`this` as the owner. The Messenger listener removes itself from inside the dispatch
(`MessengerCampaignBehavior.cs:492`, `CampaignEvents.TickEvent.ClearListeners(this);`), so the
listener walk below must survive a self-removal exactly as vanilla does.

TAOM map views (`git grep -n ": MapView" -- Main`), each overriding only `OnMapScreenUpdate(float dt)`
among the per-frame virtuals: `WarOfTheRingMomentum/UI/MomentumIndicatorMapView.cs:55`,
`FieldCamp/UI/FieldCampMapView.cs:54`, `RealmBorders/UI/RealmBordersMapView.cs:58`.

Harmony patches already on per-frame map methods (they stay; this plan's brackets enclose them):
- `Main/Features/MapLoadDiagnostics/Hooks/Campaign_RealTick_MapLoad_Patch.cs:40-61`, category
  `Patch89_MapLoadDiagnostics` (applied in `OnSubModuleLoad`, `SubModule.cs:434`): a prefix stamping
  `Stopwatch.GetTimestamp()` and a postfix that feeds the `[MapLoad]` heartbeat and, on its emit
  frames (every 5 s), walks every mobile party for a census. `TargetMethod() =>
  AccessTools.Method(typeof(Campaign), "RealTick")`.
- `Main/Features/FiefManagement/Hooks/Patch36_MapScreenF6.cs:14-16`: `[HarmonyPatch(typeof(MapScreen),
  "OnFrameTick")]`, postfix, category `Patch36_FiefManagement`.
- `Main/Features/MapLoadDiagnostics/Hooks/CampaignLifecycle_Trace_Patch.cs:62-77`: a first-frame-only
  postfix on `MapScreen.OnFrameTick`, category `Patch89_MapLoadDiagnostics_MapScreen`.
- `Main/Features/BattleLoadDiagnostics/Hooks/MapState_OnTick_ExitPhase_Patch.cs:11-30`: a postfix on
  `MapState.OnTick`, category `Patch43_BattleLoadDiagnostics`, whose first statement is an inactive
  early-out.
- `Main/Features/MapLoadDiagnostics/Hooks/SceneReady_Trace_Patch.cs:30,44`: patches on
  `SceneView.CheckSceneReadyToRender` and `SceneView.ReadyToRender`, reached from the map screen's
  frame; their time lands inside the brackets below, not separately.

The existing `[MapLoad]` heartbeat (`Main/Features/MapLoadDiagnostics/MapLoadHeartbeatService.cs:83-87`)
writes `fps` and `tickMs` (the mean `Campaign.RealTick` time) every 5 s; it cannot split the frame.
It stays as it is.

The time-control seams this plan reads (no change to them):
- `Main/Features/TimeAcceleration/ITimeControlAdapter.cs`: `bool IsCampaignActive`, `int TimeControlMode
  { get; set; }`, `float SpeedUpMultiplier { get; set; }` (`TimeControlAdapter` reads
  `Campaign.Current?.TimeControlMode` and `Campaign.Current?.SpeedUpMultiplier ?? 1f`).
- `Main/Features/TimeAcceleration/ITimeAccelerationSettingsProvider.cs`: `int FastForwardMultiplier`
  (default 4), `int ExtraFastForwardMultiplier` (default 8, floored at the fast value), `int
  CtrlSpaceMultiplier` (default 16). Both are registered `Reuse.Singleton` by
  `TimeAccelerationIoC.RegisterTimeAccelerationFeature` (`Main/IoC.cs:145`).
- `Main/Features/TimeAcceleration/TimeAccelerationService.cs:7-13`: "CampaignTimeControlMode (v1.4.8):
  Stop 0, UnstoppablePlay 1, UnstoppableFastForward 2, StoppablePlay 3, StoppableFastForward 4,
  UnstoppableFastForwardForPartyWaitTime 5, FastForwardStop 6. Campaign.TickMapTime multiplies real
  time by SpeedUpMultiplier in the three fast-forward modes". The v1.5.3 enum
  (`TaleWorlds.CampaignSystem.CampaignTimeControlMode`) has the same order (verified below).

### Engine facts (v1.5.3; `pwsh tools/taom-src.ps1 path <Type>` and the IL of the installed DLLs)

- `TaleWorlds.CampaignSystem.GameState.MapState`, decompile lines 144-191 (condensed, every statement kept):
  ```csharp
  protected override void OnTick(float dt)
  {
      base.OnTick(dt);
      if (Campaign.Current.SaveHandler.IsSaving) { Campaign.Current.SaveHandler.SaveTick(); return; }
      if (_battleSimulation != null) _battleSimulation.Tick(dt);
      else if (AtMenu) OnMenuModeTick(dt);
      OnMapModeTick(dt);
      if (!Campaign.Current.SaveHandler.IsSaving) Campaign.Current.SaveHandler.CampaignTick();
  }
  private void OnMapModeTick(float dt)
  {
      if (_closeScreenNextFrame) { Game.Current.GameStateManager.CleanStates(); return; }
      if (Handler != null) Handler.BeforeTick(dt);
      if (Campaign.Current != null && base.GameStateManager.ActiveState == this)
      {
          Campaign.Current.RealTick(dt);
          Handler?.Tick(dt);
          Handler?.AfterTick(dt);
          Campaign.Current.Tick();
          Handler?.AfterWaitTick(dt);
      }
  }
  ```
  IL: `MapState::OnTick(System.Single)` is virtual, family (protected), 117 bytes, no exception
  clauses; `OnMapModeTick` holds exactly one `callvirt Campaign::RealTick(Single)` (IL_0048) and one
  `callvirt Campaign::Tick()` (IL_0076).
- `TaleWorlds.Core.GameStateManager.OnTick(float dt)` (lines 196-209): `ActiveState.OnIdleTick(dt)`
  when `ActiveStateDisabledByUser`, else `ActiveState.OnTick(dt)`. So `MapState.OnTick` runs once per
  application tick only while the map state is active and not disabled (the escape menu, an
  inventory or party screen, a mission and a loading state all stop it).
- `TaleWorlds.MountAndBlade.Module.OnApplicationTick(float dt)` (lines 478-542): `...
  GameManagerBase.Current.OnTick(dt);` (line 534, which reaches `GameStateManager.OnTick`) and then
  `foreach (MBSubModuleBase item in CollectSubModules()) item.OnApplicationTick(dt);` (536-539).
  So within one application tick `MapState.OnTick` runs at most once and BEFORE every submodule's
  `OnApplicationTick`, TAOM's included: between two consecutive `MapState.OnTick` calls of an
  unbroken run of map frames, TAOM's `OnApplicationTick` runs exactly once. The profiler uses that as
  its continuity test.
- `TaleWorlds.CampaignSystem.Campaign` (lines 905-922 and 974-1010): `internal void RealTick(float realDt)`
  (`WaitAsyncTasks(); CheckMainPartyNeedsUpdate(); TickMapTime(realDt); ... _tickData.RealTick(_dt, realDt);
  SiegeEventManager.Tick(_dt);`; 161 bytes of IL, one exception clause) and `internal void Tick()`
  (452 bytes, no exception clause):
  ```csharp
  if (_dt > 0f || CurrentTickCount < 3)
  {
      CampaignEventDispatcher.Instance.Tick(_dt);
      _campaignPeriodicEventManager.OnTick(_dt);
      MapEventManager.Tick();
      ...
  ```
  `TickMapTime` (860-896) sets `_dt` from the mode: Stop and FastForwardStop give 0; UnstoppablePlay
  `0.25 * realDt`; UnstoppableFastForward and UnstoppableFastForwardForPartyWaitTime
  `0.25 * realDt * SpeedUpMultiplier`; StoppablePlay and StoppableFastForward the same two values
  only while `!IsMainPartyWaiting` (`IsMainPartyWaiting = MobileParty.MainParty.ComputeIsWaiting()`),
  else 0. So a frame classed `Play` or `FF` below can advance no campaign time.
- `TaleWorlds.CampaignSystem.CampaignEventDispatcher.Tick(float dt)` (lines 1044-1051) loops
  `eventReceivers[i].Tick(dt)` (a virtual call on `CampaignEventReceiver`, so never inlined).
- `TaleWorlds.CampaignSystem.CampaignEvents` (lines 291, 855, 2083-2086):
  ```csharp
  private readonly MbEvent<float> _tickEvent = new MbEvent<float>();
  public static IMbEvent<float> TickEvent => Instance._tickEvent;
  public override void Tick(float dt) { Instance._tickEvent.Invoke(dt); }
  ```
  IL of the installed `CampaignEvents::Tick(System.Single)`: public, virtual, 17 bytes, no exception
  clauses, exactly:
  ```
  IL_0000: call CampaignEvents::get_Instance()
  IL_0005: ldfld CampaignEvents::_tickEvent : MbEvent`1<Single>
  IL_000a: ldarg.1
  IL_000b: callvirt MbEvent`1<Single>::Invoke(Single)
  IL_0010: ret
  ```
  `_tickEvent` is the only dispatch of `TickEvent`. `CampaignEvents` holds two `MbEvent<float>`
  fields (`_tickEvent`, `_missionTickEvent`); this plan touches only the first.
- `TaleWorlds.CampaignSystem.MbEvent<T>` (`public class`, whole file):
  ```csharp
  internal class EventHandlerRec<TS> { public EventHandlerRec<TS> Next;
      internal Action<TS> Action { get; private set; } internal object Owner { get; private set; } ... }
  private EventHandlerRec<T> _nonSerializedListenerList;
  public void AddNonSerializedListener(object owner, Action<T> action)  // prepends a new record
  public void Invoke(T t) { InvokeList(_nonSerializedListenerList, t); }
  private void InvokeList(EventHandlerRec<T> list, T t)
  {
      while (list != null) { list.Action(t); list = list.Next; }
  }
  public void ClearListeners(object o)  // unlinks the first record whose Owner is o
  ```
  IL of `InvokeList`: 26 bytes, no exception clauses; `callvirt get_Action()`, `ldarg.2`, `callvirt
  Invoke(T)`, then `ldfld Next` and `starg.s 1`: `Next` is read AFTER the call. Reflection on the
  installed `MbEvent<float>`: the record type is `MbEvent`1+EventHandlerRec`1` (nested, internal),
  with fields `<Action>k__BackingField : Action<float>`, `<Owner>k__BackingField : object` and the
  public `Next`. Consequences the walker must reproduce: listeners run in list order (newest first);
  an exception from a listener leaves the loop at once, propagates unchanged and skips the rest; a
  listener that removes ITSELF still continues to its old successor (its own `Next` is not cleared);
  a listener added during the dispatch is prepended and does not run in it.
- `SandBox.View.Map.MapScreen` (`public class MapScreen : ScreenBase, IMapStateHandler, ...`):
  `void IMapStateHandler.Tick(float dt)` (lines 1286-1313; called as `Handler?.Tick(dt)` from
  `MapState.OnMapModeTick`, so inside `MapState.OnTick`) ticks map views' `OnFrameTick` through
  `_mapViewsContainer.ForeachReverse(delegate (MapView view) { view.OnFrameTick(dt); })`;
  `MapState.OnMenuModeTick` reaches the views' `OnMenuModeTick` through `Handler?.OnMenuModeTick`
  (also inside `MapState.OnTick`); `IMapStateHandler.OnIdleTick` (from `MapState.OnIdleTick`, which
  runs only while the map state is disabled, never in a frame where `MapState.OnTick` ran) reaches
  `OnIdleTick`; and `protected override void OnFrameTick(float dt)` (lines 1332-1371) runs the menu
  view context, then
  `_mapViewsContainer.ForeachReverse(delegate (MapView view) { view.OnMapScreenUpdate(dt); });` and
  `SandBoxViewVisualManager.OnFrameTick(...)`. Both loops are compiler-generated closures (the shape
  plan 028 left out), so TAOM's views are timed on their own overrides instead.
  `public static MapScreen Instance { get; private set; }` (line 254).
- `SandBox.View.Map.MapView` (`public abstract class MapView : SandboxView`): per-frame virtuals
  `protected internal virtual void OnMenuModeTick(float dt)`, `OnMapScreenUpdate(float dt)`,
  `OnIdleTick(float dt)` (lines 61-71); `SandBox.View.SandboxView.OnFrameTick(float dt)` (line 28).
- `TaleWorlds.Engine.LoadingWindow.IsLoadingWindowActive` (`public static bool`, line 5);
  `TaleWorlds.ScreenSystem.ScreenManager.TopScreen` (`public static ScreenBase`, line 108).
- `TaleWorlds.CampaignSystem.CampaignTimeControlMode`: `Stop, UnstoppablePlay, UnstoppableFastForward,
  StoppablePlay, StoppableFastForward, UnstoppableFastForwardForPartyWaitTime, FastForwardStop` (0 to 6).
- Threads: every target above runs on the main thread (`Module.OnApplicationTick`, the game state
  tick and the screen tick). `Campaign.RealTick` may wait on and start campaign worker tasks; the
  profiler times wall time and main-thread allocation only.
- Inlining: `MapState.OnTick`, `CampaignEvents.Tick`, `MapScreen.OnFrameTick`, `SubModule.OnApplicationTick`
  and the map view overrides are reached only through virtual calls; `Campaign.RealTick` (161 bytes,
  a loop and an exception clause) and `Campaign.Tick` (452 bytes) are far too large to inline. No
  caller holds an inlined copy that would bypass a patch.

### PatchShield (read at `765d3759`; v1.5.3 decompiles for the engine frames)

`Dependencies/Foundation/PatchShieldPolicy.cs` `ExcludedTargetMethods` ends with plan 028's entries:
```csharp
    // Per-frame Mission targets (lessons/harmony-il.md, 2026-09-26 and 2026-09-28 rules), unconditional
    // like the entries above: ...
    // MissionTickProfilerBindingTests.ProfiledTargets_AreOnPatchShieldsExclusionList walks the real targets.
    "TaleWorlds.MountAndBlade.Mission.OnTick",
    "TaleWorlds.MountAndBlade.Mission.OnPreTick",
    "TaleWorlds.MountAndBlade.Mission.TickAgentsAndTeamsImp",
};
```
`IsExcludedTargetMethod(declaringType, name)` compares `"<FullTypeName>.<MethodName>"` ordinally (one
entry covers every overload).

**What PatchShield attaches to** (`Dependencies/Foundation/PatchShield.cs`, `Install`, lines 127-251):
every method patched at the time of a pass, except methods declared in a `TAOM*` assembly (lines
179-188, so TAOM's `SubModule.OnApplicationTick` and map view overrides are never shielded), the
excluded targets (lines 198-204; one `not shielding ...` diag.log line each, `LogHotMethodSkip`) and
SaveShield's targets. It installs nothing when a co-op module is active or `patchshield-disabled.flag`
exists (lines 129-142).

**When each map method is shielded today.** Pass 1 runs in the Dependencies module's `OnSubModuleLoad`
(`Dependencies/SubModule.cs:234`); pass 2 at the end of every game initialisation (`:293`; its summary
at `:273-275`: "at a second start, TAOM's late batch, about +140 attaches"). TAOM declares
`<DependedModule Id="TAOM.Dependencies" />` (`Main/_Module/SubModule.xml:21`), so the Dependencies
module's hooks run before TAOM's. `Main/SubModule.cs` (at `e64529b3`):
- `Campaign.RealTick` (Patch89, `TryPatchCategory("Patch89_MapLoadDiagnostics")` at line 434) and
  `MapScreen.OnFrameTick` (Patch89's first-frame trace, category `Patch89_MapLoadDiagnostics_MapScreen`,
  listed at line 443) are patched in `OnSubModuleLoad`, so pass 2 of the FIRST game start shields
  them, before any map frame.
- `MapState.OnTick` (Patch43, line 1913) and Patch36's postfix on `MapScreen.OnFrameTick` (line 1817)
  are applied in TAOM's once-per-process block of `OnGameInitializationFinished`, after that game
  start's pass 2, so `MapState.OnTick` is shielded only from a process's SECOND game start.
- `Campaign.Tick` and `CampaignEvents.Tick` carry no patch today. With the profiler on (applied at
  the first game start, after pass 2) and no exclusion, they would be shielded from a second start.

**What the finalizer does.** Harmony calls it on every call of the method (`PatchShield.cs:258-259`).
With an exception, `ShouldSwallow` (lines 288-328) swallows a `MissingMethodException`,
`MissingFieldException` or `TypeLoadException` WHATEVER threw it: a patch, the original body, or
anything the body calls. For `MapState.OnTick` that is the whole campaign tick (`Campaign.RealTick`,
`Campaign.Tick` with every `CampaignEvents.TickEvent` listener, the periodic and hourly events of every
mod's campaign behaviours, `SaveHandler.CampaignTick`); for `MapScreen.OnFrameTick`, the map views
and the menu view context. It logs one `swallowed ...` line per throw and, once per method, strips
every non-protected owner's prefixes, postfixes and transpilers on it (`TryUnpatchOffendingPatches`,
lines 330-420). TAOM's own owner id `com.taom.mod` (`Main/SubModule.cs:204`) is not protected
(`PatchShieldPolicy.CompiledProtectedOwnerPrefixes` holds `"TAOM"`, matched with `StartsWith`), so
that strip removes TAOM's patches on the method too. Every other exception is handed back with
`throw` (the finalizer returns a value, so Harmony never uses `rethrow` there;
`docs/reviews/lessons/harmony-il.md`, "A value-returning finalizer that hands back its exception
erases the throw site").

**Where an exception goes when the method is not shielded** (v1.5.3 decompiles; plan 028's lesson,
`harmony-il.md` "Name every patch target a shield or gate skips ...": follow the exception to its next
catch and list what one throw skips):
- Out of `MapState.OnTick` (and out of `Campaign.RealTick`, `Campaign.Tick` or `CampaignEvents.Tick`
  when `MapState.OnTick` is unshielded too): the rest of `MapState.OnTick` (from a throw in
  `Campaign.RealTick`, that includes the views' `Handler.Tick`, `Campaign.Tick` at line 189, so no
  campaign time passes, and `SaveHandler.CampaignTick` at 163); `GameStateManager.OnTick` (196-209,
  no catch); `Game.OnTick` (332-358: the throw leaves at `GameStateManager.OnTick(dt)`, line 336, so
  the game handlers' ticks, `AfterTick` and the save-completion check at 353-357 are skipped);
  `GameManagerBase.OnTick` (104-114); `Module.OnApplicationTick` (the call at 534, so every
  submodule's `OnApplicationTick` at 536-539, TAOM's included, `JobManager.OnTick` at 540 and
  `AvatarServices.UpdateAvatarServices` at 541 are skipped). It is caught there by Patch37's
  crash-capture finalizer (`Main/Features/CrashReport/Hooks/Patch37_CrashReport.cs:47-53`, applied in
  `OnSubModuleLoad` at `SubModule.cs:217`): `CrashReportPatchHelper.HandleAndSwallow` writes a crash
  report and swallows when "Enable Crash Capture" is on (its default), and `CrashReportService`
  throttles a recurring signature (one bundle, log lines at occurrences 1, 2, 10, 100). With crash
  capture off it hands the exception back. PatchShield's own finalizer also sits on
  `Module.OnApplicationTick` from the first game start (Patch37 patches it in `OnSubModuleLoad`); the
  order of the two finalizers there was not re-derived (UNVERIFIED).
- Out of `MapScreen.OnFrameTick`: `ScreenBase.FrameTick` (148-157), then `ScreenManager.Tick`
  (288-326), whose predecessor-screen `IdleTick` (302), screen-layer ticks (304-311, the map's UI
  layers), global layer ticks, `LateUpdate` (316) and `TopScreen.PostFrameTick` (321-324) are skipped.
  It is caught by Patch37's finalizer on `ScreenManager.Tick` (`Patch37_CrashReport.cs:56-62`), same
  behaviour.
- Out of `Campaign.Tick` or `CampaignEvents.Tick` while `MapState.OnTick` stays shielded (option B
  below, from a second game start): the rest of `MapState.OnTick` is skipped (`AfterWaitTick`,
  `SaveHandler.CampaignTick`), then `MapState.OnTick`'s shield swallows and strips the non-protected
  patches on `MapState.OnTick`, Patch43's and Patch101's among them (the profiler then loses its
  frame boundary for the process). In a first game they reach Patch37 as in the first bullet.

### Conventions that bind this change

- **ADR-002** (`docs/adrs/002-thin-entry-points.md`): Harmony classes and hooks are thin entry
  points under 150 lines; logic lives in pure classes.
- **ADR-003, ADR-004, ADR-005**: no `#region`, no `[Obsolete]`, no `#if DEBUG`.
- **ADR-007** (`docs/adrs/007-adapter-pattern.md`): nothing that takes a sealed TaleWorlds type goes
  into a service. The pure classes here (`MapFrameProfiler`, `MapSpeed`, `MapProfileLines`) take
  numbers, strings and `System.Type` only; engine reads stay in `Hooks/` and the existing
  `ITimeControlAdapter`.
- **ADR-008** (`docs/adrs/008-testability-requirements.md`): pure classes fully unit-tested; hooks at
  80%+ through `RequiresGame` tests; Harmony classes covered by the binding gate.
- `.claude/rules/harmony-patches.md` (read it, it is not loaded for you): read
  `docs/reviews/lessons/harmony-il.md` before writing a patch (mandatory), and the registry sections of
  Patch36, Patch43 and Patch89 in `docs/reference/harmony-patch-registry.md` before adding a sentence
  to them. Patches live in `Main/Features/<Feature>/Hooks/` with `[HarmonyPatch]` (or `TargetMethod()`
  / `TargetMethods()`), `[HarmonyPatchCategory]` and a `TryPatchCategory` call (here through the
  installer). Apply once per process. Transpilers soft-fail. An observe-only finalizer must be `void`;
  this plan uses prefix and postfix pairs, no finalizer, so no exception path changes.
- `.claude/rules/csharp-architecture.md`: MCM values pass through a validating provider; no locks on
  the main thread; a singleton holding per-campaign state needs a session-reset story (here: the
  profiler resets at every new campaign session, see Design).
- `.claude/rules/tests.md`: MSTest plus NSubstitute, `Method_Condition_Result` names; a test that
  executes engine code, or loads `SandBox*` assemblies or runs `Assembly.GetTypes()` on TAOM's
  assembly, is tagged `[TestCategory("RequiresGame")]`; a `BindingVerification` test that reads
  vanilla IL or module types also carries `[TestCategory("RequiresGameIL")]` on the method.
- Exemplars from plan 028 (on your base): `Main/Features/MissionPerf/Hooks/Patch97_MissionTickProfiler.cs`
  (patch shape), `MissionTickProfilerInstaller.cs`, `MissionTickProfilerHooks.cs`,
  `TAOM.Tests/Features/MissionPerf/MissionTickProfilerBindingTests.cs` (real IL through `Rewrite`,
  the `TargetOf` helper, the PatchShield walk), `MissionTickProfilerInstallerTests.cs` (fake category
  apply), `MissionTickProfilerWiringTests.cs` (source pins with `RepoPaths.ReadSource("Main/SubModule.cs",
  stripComments: true)`).

### Blast radius (`python tools/graphify_taom.py affected "<Type>" --depth 2`; graph built before `0912e1b7`, stale only for run-folder scripts)

- `BattleLoadDiagnosticsSettings`: No affected nodes found.
- `IBattleLoadDiagnosticsSettingsProvider`: `BattleLoadDiagnosticsService`, `BattleLoadStallWatchdog`,
  `ExitStallSampler`, `MemoryPressureSampler`, `MemoryStationSampler`, `MissionTickStallWatchdog`, their
  tests (`AgentBuildDiagnosticsTests`, `BattleLoadDiagnosticsServiceTests`, `ExitStallDisarmTests`,
  `MemoryPressureSamplerTests`, `MemoryStationSamplerTests`, `MissionTickStallWatchdogTests`) and the one
  implementer `BattleLoadDiagnosticsSettingsProvider`. Fakes are NSubstitute, so a new member breaks
  nothing.
- `BattleLoadDiagnosticsSettingsProvider`: the `ValidateSampleIntervalSeconds_*` tests.
- `CoopSettingsRelevance`: `SettingsFingerprintTests.AssertSplit`, `NoExcludedName_IsDead`,
  `EverySettingsClass_HasItsSplitPinned`.
- `PatchShieldPolicy`: `PatchShield.IsExcludedTarget`, `PatchShield.Install`,
  `PatchShield.TryUnpatchOffendingPatches`, `PatchShield.ShouldSwallow`, `PatchShieldPolicyTests`,
  `CreatureBanditsWiringTests.HotCreatureTargets_AreOnPatchShieldsExclusionList`,
  `Patch92BindingTests.EveryPatch92Target_IsOnPatchShieldsHotMethodList`,
  `Dependencies/SubModule.cs` (`OnSubModuleLoad`, `OnGameInitializationFinished`). Adding entries only
  widens an exclusion.
- `SubModule`: "No unique node match for SubModule" (the name is ambiguous in the graph); the two
  edits are pinned by `MapFrameProfilerWiringTests`.

Re-run these at your base (after `python tools/graphify_taom.py refresh --if-stale`) and put the
output in your report; plan 028 added consumers.

## Design (decided; implement it as written)

**Install point and toggle.** One new setting, `EnableMapProfiler` (default false,
`RequireRestart = true`, group "Map Performance"), on the Battle Load Diagnostics page. An installer
is called on EVERY `OnGameInitializationFinished`, before the once-per-process guard. Its first call
reads the toggle: off, it logs one `[MapProfiler] off:` line and applies nothing; on, it binds the
listener walk, applies two categories through `TryPatchCategory`, verifies every target is patched,
creates the profiler only when the core is complete, and logs one install line. It never applies
anything again. A later call (a second campaign in the process) only reports: toggle on but off at
the first game start gives the restart line; toggle on but the install failed gives the not-installed
line. The toggle is read again at each campaign session start, so turning it off stops measuring from
the next session while the patches stay until a restart (exactly plan 028's posture).

**Targets.** Category `Patch101_MapFrameProfiler` (six patch classes) and
`Patch101_MapFrameProfiler_Views` (one class, separate so a failing view cannot stop the core):

| Target | Patch | Measures |
|---|---|---|
| `MapState.OnTick(float)` | prefix `Priority.First`: frame boundary, then stamp; postfix `Priority.Last` | the frame boundary; `mapStateMs` |
| `Campaign.RealTick(float)` (internal) | prefix `First` / postfix `Last` | `realTickMs` (inside `mapStateMs`, Patch89's postfix included) |
| `Campaign.Tick()` (internal) | prefix `First` / postfix `Last` | `campaignTickMs` (inside `mapStateMs`) |
| `CampaignEvents.Tick(float)` | transpiler: the one `callvirt MbEvent<float>::Invoke(float)` becomes `call MapFrameProfilerHooks.TimedTickEvent(MbEvent<float>, float)` | `tickEventMs` (inside `campaignTickMs`) and one entry per listener owner type |
| `MapScreen.OnFrameTick(float)` | prefix `First` / postfix `Last` | `mapScreenMs` (Patch36's and Patch89's postfixes included) |
| `TAOM.SubModule.OnApplicationTick(float)` | prefix `First` / postfix `Last` | `appTickMs`, and the continuity count |
| every TAOM `MapView` subclass's own override of `OnFrameTick`, `OnMapScreenUpdate`, `OnMenuModeTick` or `OnIdleTick` (`TargetMethods()`, found by reflection: three `OnMapScreenUpdate` overrides at planning) | prefix `First` / postfix `Last`, `object __instance` | one entry per view type: an `OnMapScreenUpdate` entry inside `mapScreenMs`; an `OnFrameTick` or `OnMenuModeTick` entry inside `mapStateMs` (reached through `MapState.OnTick`); an `OnIdleTick` entry never lands in a closed frame (the map state is idle, so the frame is a gap) |

Prefix and postfix, never a finalizer: a call that throws records nothing for that call and Harmony
adds no exception handler to an engine method. The pair passes its start stamp through `__state`
(`long`, or a `ProbeStamp` struct for the views).

**Why a call-site swap plus a listener walk for `TickEvent` (the choice the brief left open).** The
alternatives were a prefix and postfix on each TAOM listener method, or a transpiler on
`MbEvent<float>.InvokeList`. Per-listener patches cover TAOM's listeners only (no vanilla split),
must target a compiler-generated lambda (`RealmBordersCampaignBehavior.cs:52`) whose name changes
with any edit, and miss every listener added later. `InvokeList` is a private method of a generic
type shared by `_missionTickEvent`, small enough that whether the JIT inlines it into `Invoke` is
unknown, so a patch there could be bypassed. The chosen design swaps the single, non-generic,
virtually dispatched call site in `CampaignEvents.Tick` with plan 028's tested `Rewrite` (soft-fail,
exactly one match), and the helper either calls `Invoke` unchanged (not measuring, or the walk not
bound) or walks the same list itself, reproducing `InvokeList` statement for statement: read the head
once, for each record read `Action` and `Owner`, time the call inside `try`/`finally`, then read
`Next` AFTER the call. Order, exception propagation (no catch: the first throwing listener's exception
leaves the walk unchanged and skips the rest, after its own call is recorded), self-removal and
added-during-dispatch behaviour are identical, which is the brief's STOP condition; a differential
test runs vanilla `Invoke` and the walk over identical lists and compares the call sequences, and a
binding test pins `InvokeList`'s IL shape so an engine change that adds a handler or moves the `Next`
read fails the gate. Each listener is keyed by its owner's type (`Owner.GetType()`; the action's
declaring type when the owner is null); TAOM-owned means `type.Assembly == typeof(MapFrameProfilerHooks).Assembly`.

**The listener walk's accessors.** Bound once at install with Harmony's field-reference delegates,
no per-call reflection: `AccessTools.FieldRefAccess<MbEvent<float>, object>(head)` for
`_nonSerializedListenerList`, `AccessTools.FieldRefAccess<object, object>(next)` for `Next`,
`AccessTools.FieldRefAccess<object, Action<float>>(action)` for `<Action>k__BackingField` and
`AccessTools.FieldRefAccess<object, object>(owner)` for `<Owner>k__BackingField` (Lib.Harmony 2.4.2's
documentation: `T` may be the declaring class or a parent class including `object`; a reference-type
`F` may be any type the field's type is assignable to). Any member missing or of an unexpected type:
the walk stays unbound, one warning says so, and `tickEventMs` still times the whole dispatch.

**Frames, skips and continuity.** The `MapState.OnTick` prefix is the frame boundary; one frame is one
application tick in which the map state ticked. At each boundary the hook reads three things: whether
the loading window is up (`LoadingWindow.IsLoadingWindowActive`), whether the map screen is the top
screen (`ScreenManager.TopScreen is MapScreen`) and the speed class. A frame is closed into the
window and the session only when (1) a previous boundary exists in this session, (2) at the frame's
START boundary the loading window was down and the map screen was on top, and (3) TAOM's
`OnApplicationTick` postfix ran exactly once since that boundary. Otherwise the frame is discarded
(phase sums and entry calls dropped) and counted as `loading`, `notTop` or `gap` (a gap is any break
in consecutive map ticks: the escape menu, a party or inventory screen, a battle, a save). Per closed
frame: `frameMs` (boundary to boundary), `mapStateMs`, `realTickMs`, `campaignTickMs`, `tickEventMs`,
`mapScreenMs`, `appTickMs`, `taomMs` (TAOM-owned listener and view entries plus `appTickMs`),
main-thread `allocBytes` (boundary to boundary), and `otherMs = max(0, frameMs - mapStateMs -
mapScreenMs - appTickMs)`: rendering, the native engine, UI, the other submodules' application ticks
and the profiler's own bookkeeping. With no clamping, `wallMs = mapStateMs + mapScreenMs +
appTickMs + otherMs` for every window. Entry calls and phase sums reach the window and the session
only when their frame closes (028's rule), so every total covers the same closed frames.

**Speed class.** Read at each boundary for the frame it starts, from `ITimeControlAdapter`:
Stop (modes 0 and 6), Play (1 and 3), and for the fast-forward modes (2, 4, 5): `FF` when the
multiplier is at most the MCM fast-forward multiplier, `FF2` when at most the extra fast-forward
multiplier, `FF3` above (Ctrl+Space turbo, or any larger value). An unknown mode or a non-finite
multiplier in a fast-forward mode is `Unknown`, printed `na` (NaN fails the gate,
`csharp-architecture.md`). The two multipliers are read from `ITimeAccelerationSettingsProvider` at
session start and again at each window line, never per frame. A window's `speed` is the class with
the most closed frames, ties to the faster class; `na` when no frame closed. The summary carries the
per-class split.

**Sessions.** A session is one `Campaign` instance. The boundary compares `Campaign.Current` with a
`WeakReference` to the session's campaign (no strong reference, so a finished campaign is never kept
alive); on a new campaign it first closes the previous session (reason `newCampaign`), then opens the
new one: reads `MapProfilerEnabled` and `TickProfilerTopN` and the two multipliers, resets every
accumulator, and logs the session line. `SubModule.OnGameEnd` closes the session with reason
`gameEnd` (an in-campaign load can skip `OnGameEnd`, which the `newCampaign` path covers; a process
killed from the map writes no summary, its windows are already in the log). Closing a measuring
session writes `[MapProfileSummary]` when at least one frame closed, else one no-frames line.

**Window.** Every 5 s of wall clock from the session's first boundary, checked at boundaries only: the
hook takes the window and writes `[MapProfile]`, including windows in which every frame was skipped
(`frames=0`, which records a long loading window, for example). `t` is seconds since the session's
first boundary. `parties` is `Campaign.Current.MobileParties.Count` read at the window, `na` when
unreadable. `gc0`, `gc1`, `gc2` are collection-count deltas since the previous window (since the
session start for the first).

**The line contract** (invariant culture, ms `0.00`, KB `bytes / 1024` or `na`, `t` `0`, `top=none`
when empty, `top=` last because its value holds commas; the entry format is 028's
`<Type>:<ms>/<calls>/<maxMs>/<KB>`):

```
[MapProfile] t=+<s>s frames=<n> wallMs=<x> realTickMs=<x> mapScreenMs=<x> otherMs=<x> allocKB=<x|na> speed=<Stop|Play|FF|FF2|FF3|na> parties=<n|na> mapStateMs=<x> campaignTickMs=<x> tickEventMs=<x> appTickMs=<x> taomMs=<x> maxFrameMs=<x> skipped=<n> gc0=<n> gc1=<n> gc2=<n> top=<Type>:<ms>/<calls>/<maxMs>/<KB>,...
[MapProfileSummary] reason=<gameEnd|newCampaign> session=<n> frames=<n> wallMs=<x> realTickMs=<x> mapScreenMs=<x> otherMs=<x> allocKB=<x|na> mapStateMs=<x> campaignTickMs=<x> tickEventMs=<x> appTickMs=<x> taomMs=<x> maxFrameMs=<x> windows=<n> skippedLoading=<n> skippedNotTop=<n> skippedGap=<n> byspeed=<Class>:<frames>/<wallMs>,... top=<Type>:<ms>/<calls>/<maxMs>/<KB>,...
```

The brief's fields (`t frames wallMs realTickMs mapScreenMs otherMs allocKB speed parties top`) come
first in its order; the rest are additions this plan makes so the line splits TAOM's share
(`taomMs`, `appTickMs`) from the engine's and says why frames were dropped. `byspeed` lists the
classes with frames in the order Stop, Play, FF, FF2, FF3, na. Pinned literals (Step 5's tests hold
these exact strings; the inputs are under Step 5):

```
[MapProfile] t=+65s frames=300 wallMs=5000.00 realTickMs=400.50 mapScreenMs=1200.25 otherMs=2283.75 allocKB=3072 speed=FF parties=2091 mapStateMs=1500.75 campaignTickMs=950.40 tickEventMs=310.20 appTickMs=15.25 taomMs=115.85 maxFrameMs=48.30 skipped=2 gc0=3 gc1=1 gc2=0 top=FieldCommissionBehavior:60.10/300/1.25/256,RealmBordersMapView:40.50/300/0.90/64
[MapProfileSummary] reason=gameEnd session=1 frames=9000 wallMs=150000.00 realTickMs=12000.50 mapScreenMs=36000.25 otherMs=68500.00 allocKB=102400 mapStateMs=45000.75 campaignTickMs=28000.00 tickEventMs=9300.50 appTickMs=499.00 taomMs=3600.25 maxFrameMs=912.40 windows=30 skippedLoading=180 skippedNotTop=12 skippedGap=7 byspeed=Stop:3000/50000.00,FF:6000/100000.00 top=FieldCommissionBehavior:1800.50/9000/2.50/7680,RealmBordersMapView:1300.75/9000/1.75/1024
```

**Status lines** carry the tag `[MapProfiler]`, which contains none of the data tags `[MapProfile]`,
`[MapProfileSummary]`, `[TickProfile]`, `[Hitch]`, `[PerfContext]`, `[MissionPerf]`, `[TickSummary]`,
`[MapLoad]` (plan 029's log tool collects any `[Tag] key=value` line generically and treats a tagged
line whose body does not start with `key=value` as prose, so status lines must never start their body
with `key=value` either). Every text is built in `MapProfileLines` and nowhere else; exactly:

```
[MapProfiler] off: 'Enable Map Profiler' is off at game start (or MCM was not ready); no patches installed
[MapProfiler] on in MCM but it was off at the first game start, so no patches are installed and nothing is measured; restart the game to measure
[MapProfiler] on in MCM but the install at the first game start failed, so nothing is measured; see the [MapProfiler] install line and [PatchApply]
[MapProfiler] install: category <applied|failed>, views category <applied|failed>, targets <n>/<m> patched (missing <comma list|none>), MapView overrides <n>/<m> patched, CampaignEvents.Tick sites <n>/1, listener walk <bound|unbound>, allocation counter <available|na>
[MapProfiler] TickEvent listener walk not bound (<detail>); tickEventMs still times the whole dispatch, but no listener is attributed and taomMs leaves the TAOM listeners out
[MapProfiler] CampaignEvents.Tick left vanilla (sites <n>/1); tickEventMs reads 0, no listener is attributed, and the dispatch stays inside campaignTickMs
[MapProfiler] <n> of <m> TAOM map view overrides patched; the others run unattributed inside mapScreenMs
[MapProfiler] session <n>: measuring, top <topN> entries per line, a window line every 5 s of wall clock while the map ticks, speed classes FF up to <ff>x, FF2 up to <extra>x, FF3 above
[MapProfiler] session <n>: not measuring, 'Enable Map Profiler' is off in MCM; the patches stay installed and only call through until a restart
[MapProfiler] session <n> end (<reason>): no frame closed while measuring, so no summary
[MapProfiler] <where> failed, measuring stopped for this campaign session: <ExceptionType>: <message>
```

The pinned install literal (everything found):
`[MapProfiler] install: category applied, views category applied, targets 6/6 patched (missing none), MapView overrides 3/3 patched, CampaignEvents.Tick sites 1/1, listener walk bound, allocation counter available`.
The six target names, in this order, are `MapState.OnTick`, `Campaign.RealTick`, `Campaign.Tick`,
`CampaignEvents.Tick`, `MapScreen.OnFrameTick`, `SubModule.OnApplicationTick`. `<where>` is one of
`frame boundary`, `session end`, `install`. Log levels: window, summary, session, off, restart and
install lines `LogInfo`; the walk-unbound, left-vanilla, views-short and not-installed lines
`LogWarning`; faults `LogError`. Nothing is logged per frame.

**Fault handling.** The boundary and the session end run inside `try`/`catch`: a fault stops measuring
for the session (no summary) and logs one `LogError`, never per frame. The phase helpers do no work
that can throw beyond two timestamps and an add. The listener walk does not catch (a listener's own
exception must propagate exactly as vanilla's), and its bookkeeping in `finally` is array arithmetic
on a slot it just obtained.

**Cost.** Off (the default): no patch exists; every player pays one `[MapProfiler] off:` line per
process and one settings read per later game init. On and measuring: per frame two timestamps per
phase bracket (five), one boundary (three engine reads, one allocation read), per listener two
timestamps, two allocation reads and one dictionary lookup, per TAOM view the same; no allocation
after a type's first sighting, no lock, nothing logged per frame. On but not measuring (toggle turned
off mid-process): every patch calls through after a null and a flag check.

**PatchShield (decision D1, the maintainer's; option A unless the orchestrator says B).** The house
rule (the 2026-09-26 and 2026-09-28 lessons in `docs/reviews/lessons/harmony-il.md`) puts a per-frame
engine target of a TAOM patch on `ExcludedTargetMethods` in the same change, with a binding test
walking the real targets. Three of this plan's five engine targets are already patched for every
player, so the rule changes crash handling for every player, profiler on or off. The facts are under
"Current state", PatchShield.

- **Option A (the house rule; this plan's text).** All five go on the list: `MapState.OnTick`,
  `Campaign.RealTick`, `Campaign.Tick`, `CampaignEvents.Tick`, `MapScreen.OnFrameTick`. Gained: no
  PatchShield finalizer on those five (today one runs once per frame on `Campaign.RealTick` and
  `MapScreen.OnFrameTick` from the first game start and on `MapState.OnTick` from the second; with the
  profiler on, `Campaign.Tick` and `CampaignEvents.Tick` would gain one from the second), so none runs
  inside `realTickMs`, `campaignTickMs`, `tickEventMs` or `mapStateMs`. Given up, for every player with PatchShield installed: (1) a
  missing-API exception thrown anywhere inside `Campaign.RealTick` or `MapScreen.OnFrameTick` (from
  the first game start) or `MapState.OnTick` (from a second), by a patch or by code they call such as
  another mod's campaign behaviour, is no longer swallowed there; it reaches Patch37's crash-capture
  finalizer on `Module.OnApplicationTick` or `ScreenManager.Tick`, so the player gets a crash report
  (game continuing, when crash capture is on) and that frame also skips the rest of the application
  tick (every module's `OnApplicationTick`, `JobManager.OnTick`, the game handlers, the
  save-completion check) or of the screen tick (the map's UI layer ticks, `LateUpdate`,
  `PostFrameTick`); (2) the strip no longer runs there, so a broken patch on those methods is not
  removed after its first throw and throws every frame: on `Campaign.RealTick` or `MapState.OnTick`
  each map frame then skips `Campaign.Tick` (campaign time stops) and every module's application
  tick, on `MapScreen.OnFrameTick` the map's UI layers stop ticking. Nothing is given up on
  `Campaign.Tick` and `CampaignEvents.Tick`, which nothing else patches.
- **Option B (narrower; needs the maintainer's word).** Only `Campaign.Tick` and `CampaignEvents.Tick`
  go on the list. A player with the profiler off sees no change. The three shared methods keep
  today's shield and its once-per-frame finalizer: `Campaign.RealTick`'s runs inside `mapStateMs`,
  `MapState.OnTick`'s and `MapScreen.OnFrameTick`'s after their own postfixes, so in `otherMs`. Under
  the profiler, a swallow on one of the three also strips Patch101's pair there (`com.taom.mod` is not
  protected), which silently ends that measurement for the process. The deltas from this plan's text,
  each step names them: two `PatchShieldPolicy` entries with comment B (Step 7); the binding test
  walks only the targets of `Campaign_Tick_MapProfiler_Patch` and `CampaignEvents_Tick_MapProfiler_Patch`
  and its RED failure names `TaleWorlds.CampaignSystem.Campaign.Tick`; no sentence in the Patch36,
  Patch43 and Patch89 registry sections; feature doc, registry and commit body use the B wording.

Either way, not every map-frame finalizer goes: Patch37's targets `Module.OnApplicationTick`,
`ScreenManager.Tick` and `ScreenManager.Update`, and any other patched method the frame calls (for
example Patch89's `SceneView` traces), keep PatchShield's finalizer once per frame; that cost lands
in `otherMs` or in the bracket enclosing the call (plan 034 owns it). PatchShield writes one
`not shielding ...` diag.log line per excluded method (plan 028's `FormatHotMethodSkip`). The commit
body names the trade-off of the option taken.

## Step 0: none

This plan edits no protected file (`.claude/settings.json`, `.claude/settings.local.json`,
`Directory.Build.props`, `docs/adrs/*.md`).

## Commands you will need

Prefix every dotnet command with `TEMP="<tmp>" TMP="<tmp>"` from your dispatch rules (quoted). Both
MSBuild flags go on build AND test. Never `./build.ps1`: it deploys into the game install.

| Purpose | Command | Expected on success |
|---|---|---|
| Build | `dotnet build Main/TAOM.csproj -p:DisableModuleCopy=true -p:ModuleId=` | exit 0, 0 errors |
| Tests | `dotnet test TAOM.Tests -p:DisableModuleCopy=true -p:ModuleId=` | your base's totals plus 83 passing results; the only failure `EveryLanguage_DeclaresARowForEveryEnglishKey` |
| One test class | `dotnet test TAOM.Tests -p:DisableModuleCopy=true -p:ModuleId= --filter "FullyQualifiedName~<ClassName>"` | the named tests run; a filter matching nothing proves nothing |
| Binding gate (local, game loaded) | `dotnet test TAOM.Tests/TAOM.Tests.csproj -p:DisableModuleCopy=true -p:ModuleId= --settings TAOM.Tests/binding-gate.runsettings --filter "TestCategory=BindingVerification"` | all pass, none inconclusive |
| RefAsm build (hosted CI's first step; Debug, as `.ai/verification.md` requires) | `env -u BANNERLORD_GAME_DIR -u BANNERLORD_OVERRIDE_DIR dotnet build TAOM.Tests -p:TaomGameRefs=RefAsm -p:DisableModuleCopy=true -p:ModuleId=` | exit 0, and `test -f TAOM.Tests/bin/Debug/net472/refasm-game/bin/Win64_Shipping_Client/Bannerlord.exe && echo REFASM-GAME` prints `REFASM-GAME`. It restores BUTR's reference assemblies from NuGet; if the restore cannot download them (no network), report "RefAsm not run (environment)" with the error and skip the two RefAsm test rows: that is not a failure of this plan |
| RefAsm unit step (run right after the RefAsm build) | `env -u BANNERLORD_GAME_DIR -u BANNERLORD_OVERRIDE_DIR dotnet test TAOM.Tests --no-build -p:TaomGameRefs=RefAsm -p:DisableModuleCopy=true -p:ModuleId= --filter "TestCategory!=RequiresGame&TestCategory!=LiveInstall&TestCategory!=BindingVerification"` | the failure set your Step 1 recorded (Status names the expected three), nothing in `TAOM.Tests/Features/MapPerf` |
| RefAsm binding gate (hosted CI's third step, right after the unit step) | `BANNERLORD_GAME_DIR="$(pwd -W)/TAOM.Tests/bin/Debug/net472/refasm-game" env -u BANNERLORD_OVERRIDE_DIR dotnet test TAOM.Tests --no-build -p:TaomGameRefs=RefAsm -p:DisableModuleCopy=true -p:ModuleId= --settings TAOM.Tests/binding-gate.runsettings --filter "TestCategory=BindingVerification&TestCategory!=RequiresGameIL&TestCategory!=LiveInstall"` (run from the worktree root in Git Bash; `pwd -W` gives the Windows form of the path, and CI sets the same variable to the same folder, `.github/workflows/csharp.yml:76-83`) | Step 1: record the totals line and every failing name; that is the gate's base set. Step 10: the same failure set. If most tests fail with an "Inconclusive" message, the game folder did not load (check the variable and the `REFASM-GAME` check); do not record that as the base |
| Data | `python tools/validate_moduledata.py` | 0 ERRORs (no ModuleData change; run once at the end) |
| Docs | `python tools/lint_docs.py --fail-on-drift` | exit 0 |
| Size check | `wc -l Main/Features/MapPerf/Hooks/*.cs` | every file under 150 lines |

## Scope

**In scope** (the only files you create or modify):

New, `Main/Features/MapPerf/`: `MapFrameProfiler.cs`, `MapSpeed.cs`, `MapProfileLines.cs`.

New, `Main/Features/MapPerf/Hooks/`: `MapFrameProfilerHooks.cs`, `MapSessionHooks.cs`,
`TickEventListenerWalker.cs`, `MapFrameProfilerInstaller.cs`, `Patch101_MapFrameProfiler.cs`,
`Patch101_MapFrameProfilerViews.cs`; and, only if `MapFrameProfilerInstaller.cs` reaches 150 lines,
`MapProfilerTargets.cs` (the one allowed split, Step 7).

New, `TAOM.Tests/Features/MapPerf/`: `MapSpeedTests.cs`, `MapFrameProfilerTests.cs`,
`MapProfileLinesTests.cs`, `TickEventSwapTests.cs`, `TickEventListenerWalkerTests.cs`,
`MapFrameProfilerHooksTests.cs`, `MapFrameProfilerInstallerTests.cs`, `MapFrameProfilerBindingTests.cs`,
`MapFrameProfilerWiringTests.cs`.

Modified:
- `Main/Features/BattleLoadDiagnostics/BattleLoadDiagnosticsSettings.cs` (one property; the
  `TickProfilerTopN` hint text)
- `Main/Features/BattleLoadDiagnostics/IBattleLoadDiagnosticsSettingsProvider.cs` (one getter; the
  `TickProfilerTopN` doc comment), `BattleLoadDiagnosticsSettingsProvider.cs` (one getter)
- `Main/Features/CoopInterop/CoopSettingsRelevance.cs` (one name on `Instrumentation`)
- `Dependencies/Foundation/PatchShieldPolicy.cs` (five entries on `ExcludedTargetMethods`; two under
  option B)
- `Main/SubModule.cs`: single-owner, exactly the two insertions of Step 8 and nothing else
- `TAOM.Tests/Features/BattleLoadDiagnostics/BattleLoadDiagnosticsSettingsProviderTests.cs` (one test)
- `TAOM.Tests/Features/CoopInterop/SettingsFingerprintTests.cs` (the `BattleLoadDiagnosticsSettings`
  `reflected:` count, plus one; nothing else)
- `TAOM.Tests/Features/Mcm/SettingRequireRestartPostureTests.cs` (one allowlist entry with its reason;
  the summary amended)
- `TAOM.Tests/Migration/ReflectionSiteBindingTests.cs` (four `[DataRow]`s)
- Docs: `docs/features/map-perf-profiler.md` (new), `docs/reference/feature-map.md`,
  `docs/reference/harmony-patch-registry.md`, `docs/reference/taleworlds-api-snapshot/reflection-sites.md`,
  `docs/features/coop-interop.md`, `docs/features/bannerlord-together-compat.md`, `docs/features/mcm.md`

**Out of scope** (do NOT touch, even though they look related):
- `Main/IoC.cs` (single-owner; every service this plan resolves is already registered: the settings
  provider in `BattleLoadDiagnosticsIoC`, the two time-acceleration seams in `TimeAccelerationIoC`).
  If you find you need it, STOP and report the exact line.
- `Main/TAOM.csproj` (SDK globbing picks up new files), `Directory.Build.props`, `.claude/settings*.json`,
  `docs/adrs/*.md`.
- `Main/Composition/*` and `FeatureModules.All`: wired by hand beside plan 028's profiler.
- Plan 028's files (`Main/Features/MissionPerf/**`, its tests and doc): reuse, do not change.
- The eight listener files, the three map view files, the Patch36, Patch43 and Patch89 patch files,
  `MapLoadHeartbeatService.cs`, `PatchShield.cs`, the TimeAcceleration files.
- Any fix of what the profiler finds (plan 037 and later work), the engine's own map rendering, and
  `tools/perf_runs.py` (a map mode for plan 029's tool is a follow-up).
- `CHANGELOG.md`, `plans/README.md`, `docs/reference/taleworlds-api-snapshot/patch-targets.md` (the
  orchestrator refreshes it).
- The gates themselves. Never turn a gate green by editing it: deleting or `[Ignore]`-ing a test,
  loosening an assertion, or adding an allowlist entry without its reason. The gate edits this plan
  makes are the designed procedures: the settings count moves with the classification (the comment at
  `SettingsFingerprintTests.cs:205-209` says so), the restart allowlist entry carries its reason, and
  the PatchShield entries carry their binding test. Anything else: STOP and report.

## Git workflow

- Commit on the branch you were given; never push or open a PR.
- One commit at the end of Step 10. Subject
  `feat(map-perf): <version> - campaign map frame profiler, off by default`, where `<version>` is the
  `<Version value=...>` in `Main/_Module/SubModule.xml` when you commit (`v2.0.32` when planned, giving
  69 characters; a hook refuses any other version). At most 72 characters.
- The body is the changelog entry, wrapped at 72, for a reader of the release note: what the profiler
  measures and why (the slow fast-forward while a campaign fills in), that it is off by default and
  installs nothing unless turned on (a restart applies it; turning it off stops measuring from the next
  campaign), the two data lines and what `taomMs` and `otherMs` mean, what it still costs when off (one
  status line per process), and the PatchShield trade-off of the option taken (re-check it against
  Design, "PatchShield", before you commit). Option A, in substance: PatchShield no longer shields
  `Campaign.RealTick`, `MapState.OnTick`, `MapScreen.OnFrameTick`, `Campaign.Tick` or
  `CampaignEvents.Tick`, for every player. A MissingMethod, MissingField or TypeLoad exception thrown
  anywhere inside the first three (by a patch, or by code they call such as another mod's campaign
  behaviour) is no longer swallowed there and a broken patch there is no longer removed; it reaches
  TAOM's crash capture on `Module.OnApplicationTick` or `ScreenManager.Tick`, which writes a crash
  report and lets the game continue, but each frame it recurs skips the rest of that application or
  screen tick (other mods' application ticks, the campaign tick after a throw in `RealTick`, the map's
  UI layers). Option B, in substance: only `Campaign.Tick` and `CampaignEvents.Tick`, which nothing
  else patches, are excluded, so nothing changes for a player with the profiler off, and PatchShield's
  once-per-frame finalizer on the other three stays inside the measurements. Trailers:
  `Not-tested: live patch application, the in-game numbers, and the engine reads in MapSessionHooks.OnFrameBoundary (maintainer's in-game check)`
  and `Save-compat: none (no saved state)`. No AI attribution trailer. Never edit `CHANGELOG.md`.
- Stage explicit paths only (every in-scope path you changed); write the message to a file and run
  `git commit -F "<file>"`. Never `--no-verify`.

## Steps

### Step 1: record the base

1. The drift check at the top (ancestry and diff). Record `git rev-parse --short HEAD` as `<start>`.
2. `git grep -n "Patch101_" -- Main Dependencies TAOM.Tests docs` prints nothing (plan 040 picks its own
   number from 100 upward and skips any number another plan names, so it will not take 101). A hit is a
   STOP: report it, do not renumber.
3. Confirm plan 028's members quoted under "Current state" exist with those signatures:
   `git grep -n -e "public int SlotFor(Type type)" -e "public IReadOnlyList<BehaviorTotal> MissionTop" -- Main/Features/MissionPerf/BehaviorTickTable.cs`,
   `git grep -n "internal static List<CodeInstruction> Rewrite(" -- Main/Features/MissionPerf/TickProfilerTranspiler.cs`,
   `git grep -n "public static long ReadOrZero" -- Main/Features/MissionPerf/AllocationCounter.cs`,
   `git grep -n "int TickProfilerTopN" -- Main/Features/BattleLoadDiagnostics/IBattleLoadDiagnosticsSettingsProvider.cs`.
   Each pattern matches exactly one line (five lines in all); otherwise STOP.
4. Before any edit, in this order: the RefAsm build, the RefAsm unit step and the RefAsm binding gate
   (Commands table; record each totals line and every failing name: the gate's failing names are its
   base set, which Status could not name), then the normal Build, the full Tests command and the local
   binding gate. Record all totals.
5. Re-run the blast-radius commands under "Current state" and keep the output for your report.

**Verify**: the Tests totals show one failure, `EveryLanguage_DeclaresARowForEveryEnglishKey`, and
Skipped 2, or the difference is explained by a plan merged into your base (name it). The RefAsm unit
step fails at most the three tests Status names. The RefAsm binding gate printed a totals line with
its game folder loaded (not a run of Inconclusive failures, see the Commands table); its failing
names, whatever they are, are recorded as its base set. Your report quotes every totals line.

### Step 2: the toggle, its provider, its co-op classification and the counts (TDD)

RED:
1. `BattleLoadDiagnosticsSettingsProviderTests.cs`: add `MapProfilerEnabled_NoMcmInstance_DefaultsFalse`
   (`Assert.IsFalse(new BattleLoadDiagnosticsSettingsProvider().MapProfilerEnabled)`), after plan 028's
   `TickProfilerEnabled_NoMcmInstance_DefaultsFalse`.
2. `SettingsFingerprintTests.cs`: in `AssertSplit(typeof(BattleLoadDiagnosticsSettings), reflected: N, covered: 0);`
   change N to N + 1 (12 to 13 on plan 028's tip; more if 040 or 041 landed first).
3. `SettingRequireRestartPostureTests.cs`: add to `RestartAllowlist`, after the `EnableTickProfiler` entry:
   `[$"{nameof(BattleLoadDiagnosticsSettings)}.{nameof(BattleLoadDiagnosticsSettings.EnableMapProfiler)}"] = "Read at the first game init, where Patch101 installs or is skipped: turning it on needs a restart (turning it off applies from the next campaign session)",`

Build the test project with the filtered command below. **Verify RED**: the build fails with
missing-member diagnostics (CS1061 or CS0117 class) naming `MapProfilerEnabled` and
`EnableMapProfiler`. Record the error list.

GREEN:
- `BattleLoadDiagnosticsSettings.cs`, after the last Mission Performance property:
  ```csharp
  [SettingPropertyGroup("Map Performance")]
  [SettingPropertyBool("Enable Map Profiler", Order = 0, RequireRestart = true,
      HintText = "Off by default. Times the campaign map's frame: the campaign tick, every per-frame tick-event listener by its owner, the map screen and TAOM's map views, and TAOM's application tick. Writes a [MapProfile] line to the TAOM debug log every 5 seconds while the map runs and a [MapProfileSummary] line when the campaign ends. Turning it on takes effect after a restart (the profiler is installed once, at game start); turning it off stops measuring from the next campaign session.")]
  public bool EnableMapProfiler { get; set; } = false;
  ```
  and change the `TickProfilerTopN` hint text to: `"How many entries each [TickProfile] and [TickSummary] line lists (mission behaviours) and each [MapProfile] and [MapProfileSummary] line lists (map tick listeners and TAOM map views), slowest first. Default 8. Read at each mission start and each campaign session start."`
- `IBattleLoadDiagnosticsSettingsProvider.cs`: add, after `TickProfilerEnabled`:
  ```csharp
  /// <summary>The Patch101 map profiler's toggle. Like the tick profiler's it fails CLOSED to false
  /// when MCM is not ready, because it installs Harmony patches on the campaign map's per-frame methods.
  /// Read at every game init (the first installs or skips Patch101, later ones only report) and at each
  /// campaign session start: turning it on needs a restart, turning it off stops measuring from the next
  /// campaign session (the patches stay).</summary>
  bool MapProfilerEnabled { get; }
  ```
  and extend the `TickProfilerTopN` summary to say it also sizes the `[MapProfile]` and
  `[MapProfileSummary]` lists, read at each campaign session start.
- `BattleLoadDiagnosticsSettingsProvider.cs`: `public bool MapProfilerEnabled => BattleLoadDiagnosticsSettings.Instance?.EnableMapProfiler ?? false;`
- `CoopSettingsRelevance.cs`: on `Instrumentation`, after plan 028's line
  `"EnableTickProfiler", "TickProfilerTopN", "HitchThresholdMs",`, add the comment
  `// The Patch101 campaign map profiler's toggle (<your commit date>): log lines, never a computation.`
  and the name `"EnableMapProfiler",`.
- `SettingRequireRestartPostureTests.cs`, the summary: replace the sentence that begins
  `/// Every TAOM setting but one is read live` through the line ending
  `/// setting a Harmony category is gated on at apply time (read once per process for Patch97).`
  with:
  ```csharp
  /// Nearly every TAOM setting is read live through its settings instance, so the honest posture
  /// is <c>RequireRestart = false</c> everywhere, and a new setting that omits the flag is a bug this
  /// test catches. The allowlist holds the exceptions, each with its reason: a setting whose consumer
  /// is parked (commented out in SubModule.cs), where a restart does not help either but flipping
  /// the flag would promise an effect that does not exist; and the profiler toggles a Harmony category
  /// is gated on at apply time, read once per process (<c>EnableTickProfiler</c> for Patch97,
  /// <c>EnableMapProfiler</c> for Patch101).
  ```
  If plan 041 landed in your base and its `EnableHitchProbe` entry is on the allowlist, name it in
  that list too. Keep the lines after it (`/// Note that no MCM setting can gate anything in
  OnSubModuleLoad:` onward) unchanged.
- `docs/features/coop-interop.md`, the "What is NOT done" bullet: the bold total plus one (`**340**`
  to `**341**` on plan 028's tip), the `BattleLoadDiagnosticsSettings` count plus one (`12 in` to
  `13 in`), the excluded count plus one (`The 123 excluded (` to `The 124 excluded (`) and, before
  that parenthetical's closing `)`, add `, and the map profiler toggle added <your commit date>`.
  `docs/features/bannerlord-together-compat.md`: `TAOM's 340 MCM settings` to `TAOM's 341 MCM settings`
  (again plus one from whatever your base says). The simulation-relevant count (217) does not move.
- `docs/features/mcm.md`, two edits. Replace `Two are allowlisted by \`Class.Property\`,
  each with a reason:` through `\`BattleLoadDiagnosticsSettings.EnableTickProfiler\` (see below).` with
  `Three are allowlisted by \`Class.Property\`, each with a reason: \`TaomSettings.EnableNativeSkinFixes\`
  (parked; its consumer is commented out, so no value of the flag is honest) and the two profiler
  toggles \`BattleLoadDiagnosticsSettings.EnableTickProfiler\` and
  \`BattleLoadDiagnosticsSettings.EnableMapProfiler\` (see below).`; and replace the sentence
  `\`BattleLoadDiagnosticsSettings.EnableTickProfiler\` is the one such setting today: it is read at`
  ... `stops measuring from the next mission while the patches stay until a restart.` with
  `The two profiler toggles are such settings: \`BattleLoadDiagnosticsSettings.EnableTickProfiler\` is
  read at the first game init, where Patch97 installs or is skipped, and again at each mission start;
  \`BattleLoadDiagnosticsSettings.EnableMapProfiler\` is read at the first game init, where Patch101
  installs or is skipped, and again at each campaign session start. Each carries \`RequireRestart = true\`
  and an allowlist entry: turning it on needs a restart, turning it off stops measuring from the next
  mission or campaign session while the patches stay until a restart.` If plan 041 landed first and
  these sentences already name a third toggle, keep it and count it (four allowlisted).
  Re-read both paragraphs after the edit.

**Verify**: `--filter "FullyQualifiedName~BattleLoadDiagnosticsSettingsProviderTests|FullyQualifiedName~SettingsFingerprintTests|FullyQualifiedName~SettingRequireRestartPostureTests"`
passes with `MapProfilerEnabled_NoMcmInstance_DefaultsFalse` among the executed names (this includes
`EveryDocQuotingTheSettingsCounts_AgreesWithReflection`, which checks the two docs). Each of these
returns nothing:
`git grep -n -e "Every TAOM setting but one" -e "setting a Harmony category is gated on at apply time" -- TAOM.Tests docs`
(the second phrase sits on one line of the old summary; the new text says "toggles a Harmony
category" and does not match it),
`git grep -n -e "is the one such setting" -e "Two are allowlisted" -- docs`.

### Step 3: `MapSpeed` (TDD)

RED: `TAOM.Tests/Features/MapPerf/MapSpeedTests.cs` (no category), each one `[TestMethod]` with one
assert per listed input:
`Classify_StopAndFastForwardStop_ReturnStop` (modes 0 and 6, multiplier 4),
`Classify_UnstoppableAndStoppablePlay_ReturnPlay` (1 and 3),
`Classify_FastForwardModesAtTheFastMultiplier_ReturnFF` (modes 2, 4, 5 at 4f with fast 4, extra 8; also 1f),
`Classify_AboveFastUpToExtra_ReturnsFF2` (4.5f and 8f),
`Classify_AboveExtra_ReturnsFF3` (16f),
`Classify_UnknownMode_ReturnsUnknown` (7 and -1),
`Classify_NonFiniteMultiplierInAFastForwardMode_ReturnsUnknown` (`float.NaN`, `float.PositiveInfinity`),
`Token_EveryClass_MatchesTheLineContract` (`Unknown` "na", `Stop` "Stop", `Play` "Play", `FF` "FF",
`FF2` "FF2", `FF3` "FF3"). Build; **Verify RED**: missing-type diagnostics for `MapSpeed` and
`MapSpeedClass`.

GREEN: `Main/Features/MapPerf/MapSpeed.cs`:
```csharp
/// <summary>A map frame's time-control class. Ordered so that a larger value is the faster class
/// (window ties go to it); Unknown loses every tie.</summary>
public enum MapSpeedClass { Unknown, Stop, Play, FF, FF2, FF3 }

public static class MapSpeed
{
    public static MapSpeedClass Classify(int mode, float multiplier, int fastForwardMultiplier, int extraFastForwardMultiplier)
    // 0, 6 -> Stop; 1, 3 -> Play; 2, 4, 5 -> Unknown when the multiplier is NaN or infinite, FF when
    // multiplier <= fast + 0.001, FF2 when <= extra + 0.001, else FF3; anything else -> Unknown.
    public static string Token(MapSpeedClass speed) // "na", "Stop", "Play", "FF", "FF2", "FF3"
}
```
with the mode numbers as named constants and a comment quoting the engine enum order and
`TickMapTime`'s rule from "Current state".

**Verify**: `--filter "FullyQualifiedName~MapSpeedTests"`: 8 passed.

### Step 4: `MapFrameProfiler` (TDD)

RED: `TAOM.Tests/Features/MapPerf/MapFrameProfilerTests.cs` (no category). Construct with
`new MapFrameProfiler(1000)` so a tick is a millisecond (window 5 s = 5000 ticks). Use
`typeof(string)` as a TAOM-owned entry and `typeof(int)` as a vanilla one (names `String`, `Int32`).
Every case starts with `BeginSession(0, true, 0, 0, 0)` unless it says otherwise, and its first
`Boundary` only stamps the clock. Cases, each one `[TestMethod]`:
1. `Boundary_FirstOfSession_StampsOnlyAndCountsNoFrame`: `BeginSession(0, true, 0, 0, 0)`, an entry and
   a phase recorded BEFORE the first `Boundary(100, ...)`, then `TakeWindow(100, 8, 0, 0, 0)`: frames 0,
   skipped 0, `Top` empty.
2. `Boundary_TwoFrames_SumIntoTheWindow`, with this oracle:
   ```
   a = SlotFor(typeof(string)); b = SlotFor(typeof(int));
   BeginSession(0, true, 0, 0, 0);
   Boundary(100, 0, MapSkip.None, MapSpeedClass.FF);                       // first: stamp only
   AddPhase(MapState, 20); AddPhase(RealTick, 5); AddPhase(CampaignTick, 8); AddPhase(TickEvent, 3);
   RecordEntry(a, 2, 1024, taomOwned: true); RecordEntry(b, 1, 0, taomOwned: false);
   AddPhase(MapScreen, 10); AddAppTick(1);
   Boundary(150, 4096, MapSkip.None, MapSpeedClass.FF);                    // frame 1: 50 ms
   AddPhase(MapState, 30); AddPhase(MapScreen, 5); AddAppTick(2);
   Boundary(250, 8192, MapSkip.None, MapSpeedClass.Stop);                  // frame 2: 100 ms
   w = TakeWindow(250, 8, 0, 0, 0);
   ```
   expect `Frames` 2, `WallMs` 150, `MapStateMs` 50, `RealTickMs` 5, `CampaignTickMs` 8, `TickEventMs` 3,
   `MapScreenMs` 15, `AppTickMs` 3, `OtherMs` 82 (19 + 63), `TaomMs` 5 (entry 2 + app 3), `MaxFrameMs`
   100, `AllocBytes` 8192, `Speed` FF (both frames STARTED under FF; the Stop passed at 250 belongs to
   frame 3), `Skipped` 0, `Top` = [`String` 2 ms / 1 call / max 2 / 1024 bytes, `Int32` 1 / 1 / 1 / 0];
   and assert `WallMs == MapStateMs + MapScreenMs + AppTickMs + OtherMs`.
3. `Boundary_OtherMs_NeverNegative` (`AddPhase(MapState, 80)` in a 50 ms frame: `OtherMs` 0).
4. `Boundary_LoadingAtTheFramesStart_DiscardsItAsLoading`: `Boundary(0, 0, Loading, FF)`, `AddAppTick(1)`,
   `Boundary(50, 0, None, FF)`, `AddAppTick(1)`, `Boundary(100, 0, None, FF)`: window frames 1, wall 50,
   skipped 1; `Summarize(8).SkippedLoading` 1.
5. `Boundary_NotTopAtTheFramesStart_DiscardsItAsNotTop` (the same with `NotTop`; `SkippedNotTop` 1).
6. `Boundary_NoAppTickInTheFrame_DiscardsItAsAGap` (`SkippedGap` 1, frames 0).
7. `Boundary_TwoAppTicksInTheFrame_DiscardsItAsAGap`.
8. `Boundary_DiscardedFrame_KeepsNoEntryOrPhaseTime` (a gap frame with an entry and phases: the window
   has `Top` empty and every phase 0).
9. `RecordEntry_TaomOwned_CountsTowardTaomMs_OthersDoNot` (`RecordEntry(a, 4, 0, true)`,
   `RecordEntry(b, 6, 0, false)`, `AddAppTick(1)` in one closed frame: `TaomMs` 5).
10. `TakeWindow_Speed_IsTheClassWithMostFrames` (frames started under FF, FF, Stop: FF).
11. `TakeWindow_SpeedTie_GoesToTheFasterClass` (one Stop frame, one FF frame: FF).
12. `TakeWindow_NoClosedFrame_SpeedIsUnknown`.
13. `TakeWindow_GcDeltas_AreSinceTheSessionStartThenThePreviousWindow`
    (`BeginSession(0, true, 10, 5, 1)`, `TakeWindow(.., 13, 6, 1)` gives 3/1/0, then
    `TakeWindow(.., 14, 6, 2)` gives 1/0/1).
14. `TakeWindow_ResetsTheWindowButNotTheSession` (a second `TakeWindow` is empty; `Summarize` still
    counts the frames).
15. `TakeWindow_OpenFrameEntries_LandInTheNextWindow` (record an entry, take the window before the
    boundary: `Top` empty; close the frame, take again: the entry is there).
16. `WindowDue_BeforeTheInterval_False_AtTheInterval_True` (`BeginSession(0, ...)`: 4999 false, 5000
    true; after `TakeWindow(5000, ...)`, 9999 false, 10000 true).
17. `MaxFrameMs_IsTheSlowestClosedFrame_PerWindowAndPerSession`.
18. `BeginSession_ResetsEverythingAndIncrementsTheSession` (frames before a second `BeginSession` are
    gone from the window and the summary; `Session` goes 1 to 2; `SecondsSinceSessionStart(now)`
    restarts).
19. `EndSession_CurrentSession_StopsMeasuring` (returns true, `Measuring` false).
20. `EndSession_StaleSession_ChangesNothing` (`s1 = BeginSession(...)`, `BeginSession(...)`,
    `EndSession(s1)` returns false and `Measuring` stays true).
21. `Summarize_CoversEveryClosedFrame_WithSkipReasonsSpeedsAndWindows`, with this oracle:
    ```
    BeginSession(0, true, 0, 0, 0);
    Boundary(0, 0, Loading, Stop);   AddAppTick(0);
    Boundary(40, 0, None, Stop);     // 0-40 discarded: loading
    AddPhase(MapState, 10); AddAppTick(1);
    Boundary(100, 0, None, FF);      // 40-100 closed: 60 ms, Stop
    TakeWindow(100, 8, 0, 0, 0);
    AddPhase(MapState, 20); AddAppTick(1);
    Boundary(200, 0, NotTop, FF);    // 100-200 closed: 100 ms, FF
    AddAppTick(1);
    Boundary(260, 0, None, FF);      // 200-260 discarded: notTop
    Boundary(300, 0, None, FF);      // 260-300 discarded: gap (no app tick)
    TakeWindow(300, 8, 0, 0, 0);
    s = Summarize(8);
    ```
    expect `Frames` 2, `WallMs` 160, `MapStateMs` 30, `AppTickMs` 2, `OtherMs` 128 (49 + 79),
    `MaxFrameMs` 100, `Windows` 2, `SkippedLoading` 1, `SkippedNotTop` 1, `SkippedGap` 1, `BySpeed` =
    [Stop 1 frame / 60 ms, FF 1 frame / 100 ms] in that order.

Build; **Verify RED**: missing-type diagnostics for `MapFrameProfiler`, `MapPhase`, `MapSkip`.

GREEN: `Main/Features/MapPerf/MapFrameProfiler.cs` (pure: no TaleWorlds type; main thread only, no
locks, documented as not thread-safe):
- `public enum MapPhase { MapState, RealTick, CampaignTick, TickEvent, MapScreen }`,
  `public enum MapSkip { None, Loading, NotTop }`.
- `public sealed class MapFrameProfiler`, constructor `(long ticksPerSecond, double windowSeconds = 5.0)`;
  `public BehaviorTickTable Entries { get; } = new BehaviorTickTable();` (plan 028's class; its
  "mission" layer is this profiler's session layer); `public bool Measuring`; `public int Session`;
  `public long SessionStartTicks`; `public double SecondsSinceSessionStart(long nowTicks)`.
- `public int BeginSession(long nowTicks, bool measuring, int gc0, int gc1, int gc2)`: resets every
  accumulator (frame, window, session, skip and speed counts, the window count), sets
  `_haveBoundary = false` (a `bool`, never a 0 sentinel: `harmony-patches.md` "Sentinel-Collision
  Check"), stamps `SessionStartTicks` and the window's open time to `nowTicks`, stores the GC counts,
  calls `Entries.ResetFrame()`, `ResetWindow()`, `ResetMission()`, increments `Session`, sets
  `Measuring`, returns `Session`.
- `public bool EndSession(int session)`: `Measuring = false` and true only when `session == Session`.
- `public void AddPhase(MapPhase phase, long elapsedTicks)`; `public void AddAppTick(long elapsedTicks)`
  (adds to the frame's app sum AND increments the frame's app-tick count);
  `public void RecordEntry(int slot, long elapsedTicks, long allocBytes, bool taomOwned)` (calls
  `Entries.Record` and, when `taomOwned`, adds to the frame's TAOM sum).
- `public void Boundary(long nowTicks, long allocBytesNow, MapSkip skipNow, MapSpeedClass speedNow)`:
  ```
  appTicks = _frameAppTicks;
  if (!_haveBoundary) { _haveBoundary = true; discard the frame (no count); }
  else if (_openSkip == Loading) { discard; count window skipped and session skippedLoading; }
  else if (_openSkip == NotTop)  { discard; count window skipped and session skippedNotTop; }
  else if (appTicks != 1)        { discard; count window skipped and session skippedGap; }
  else {
      frameMs = ToMs(nowTicks - _lastBoundary); other = max(0, frameMs - mapState - mapScreen - app);
      alloc = allocBytesNow - _lastAlloc;
      Entries.FoldFrame();
      add every phase, frameMs (to wallMs), other, alloc, taom into the window and the session totals;
      max frame into both; window speed frames[_openSpeed]++; session speed frames and wallMs[_openSpeed] += ;
  }
  "discard" = Entries.ResetFrame() and nothing added;
  then always: _lastBoundary = nowTicks; _lastAlloc = allocBytesNow; _openSkip = skipNow;
  _openSpeed = speedNow; zero the frame's phase, app, TAOM sums and app-tick count.
  ```
- `public bool WindowDue(long nowTicks)`: `nowTicks - _windowOpen >= windowSeconds * ticksPerSecond`.
- `public MapWindow TakeWindow(long nowTicks, int topN, int gc0, int gc1, int gc2)`: builds the window
  (speed: the class with the most frames, ties to the larger enum value, `Unknown` when none; top:
  `Entries.WindowTop(topN, ticksPerSecond)`; GC deltas against the stored counts, then stores these),
  then resets the window totals and `Entries.ResetWindow()`, sets `_windowOpen = nowTicks`, increments
  the session's window count. It never touches the open frame.
- `public MapSummary Summarize(int topN)`: the session totals, the window count, the three skip counts,
  `BySpeed` (classes with frames, order Stop, Play, FF, FF2, FF3, Unknown) and
  `Entries.MissionTop(topN, ticksPerSecond)`. Resets nothing.
- Same file: `public sealed class MapWindow` (public constructor taking, in line order: `int frames,
  double wallMs, double realTickMs, double mapScreenMs, double otherMs, long allocBytes, MapSpeedClass
  speed, double mapStateMs, double campaignTickMs, double tickEventMs, double appTickMs, double taomMs,
  double maxFrameMs, int skipped, int gc0, int gc1, int gc2, IReadOnlyList<BehaviorTotal> top`, with a
  get-only property per argument), `public sealed class MapSummary` (constructor `int session, int
  frames, double wallMs, double realTickMs, double mapScreenMs, double otherMs, long allocBytes, double
  mapStateMs, double campaignTickMs, double tickEventMs, double appTickMs, double taomMs, double
  maxFrameMs, int windows, int skippedLoading, int skippedNotTop, int skippedGap,
  IReadOnlyList<SpeedTotal> bySpeed, IReadOnlyList<BehaviorTotal> top`) and `public sealed class
  SpeedTotal(MapSpeedClass speed, int frames, double wallMs)`. A private `Totals` struct for the window
  and session sums is fine.

**Verify**: `--filter "FullyQualifiedName~MapFrameProfilerTests"`: 21 passed.

### Step 5: `MapProfileLines` (TDD, the contract)

RED: `TAOM.Tests/Features/MapPerf/MapProfileLinesTests.cs` (no category):
1. `BuildMapProfile_SampleWindow_MatchesThePinnedLiteral`: `BuildMapProfile(65.2, window, 2091, true)`
   equals the `[MapProfile]` literal in Design, where `window` = `new MapWindow(300, 5000, 400.5,
   1200.25, 2283.75, 3_145_728, MapSpeedClass.FF, 1500.75, 950.4, 310.2, 15.25, 115.85, 48.3, 2, 3, 1, 0,
   new[] { new BehaviorTotal("FieldCommissionBehavior", 60.1, 300, 1.25, 262_144), new BehaviorTotal("RealmBordersMapView", 40.5, 300, 0.9, 65_536) })`.
2. `BuildSummary_SampleSession_MatchesThePinnedLiteral`: `BuildSummary("gameEnd", summary, true)`
   equals the `[MapProfileSummary]` literal, where `summary` = `new MapSummary(1, 9000, 150000, 12000.5,
   36000.25, 68500, 104_857_600, 45000.75, 28000, 9300.5, 499, 3600.25, 912.4, 30, 180, 12, 7,
   new[] { new SpeedTotal(MapSpeedClass.Stop, 3000, 50000), new SpeedTotal(MapSpeedClass.FF, 6000, 100000) },
   new[] { new BehaviorTotal("FieldCommissionBehavior", 1800.5, 9000, 2.5, 7_864_320), new BehaviorTotal("RealmBordersMapView", 1300.75, 9000, 1.75, 1_048_576) })`.
3. `BuildInstallLine_AllFound_MatchesThePinnedLiteral`: `BuildInstallLine(true, true, 6, 6,
   Array.Empty<string>(), 3, 3, 1, true, true)` equals the install literal in Design.
4. `BuildInstallLine_MissingTargets_NamesThem` (`missing` = `MapScreen.OnFrameTick`, `Campaign.Tick`:
   the line contains `targets 4/6 patched (missing MapScreen.OnFrameTick,Campaign.Tick)` and
   `category failed` when `coreApplied` is false).
5. `BuildMapProfile_NoAllocationCounter_WritesNaForEveryKb` (`allocKB=na` and every entry ends `/na`).
6. `BuildMapProfile_NoEntries_WritesTopNone`.
7. `BuildMapProfile_PartiesUnreadable_WritesNa` (parties -1).
8. `BuildSummary_NoEntries_WritesTopNone`.
9. `Build_CommaDecimalCulture_StillWritesInvariantPoints` (set `CultureInfo.CurrentCulture` to `de-DE`
   inside `try`/`finally` and restore it; the window literal is unchanged).
10. `StatusLines_NeverContainADataTag`: `OffLine`, `RestartNeededLine`, `NotInstalledLine` and every
    builder called with sample arguments (`BuildInstallLine`, `BuildWalkerUnboundLine("x")`,
    `BuildTickEventVanillaLine(0)`, `BuildViewsShortLine(1, 3)`, `BuildSessionStartLine(1, 8, 4, 8)`,
    `BuildSessionOffLine(1)`, `BuildNoFramesLine(1, "gameEnd")`,
    `BuildFault("frame boundary", new InvalidOperationException("[x]"))`) start with `[MapProfiler] `,
    contain none of `[MapProfile]`, `[MapProfileSummary]`, `[TickProfile]`, `[Hitch]`, `[PerfContext]`,
    `[MissionPerf]`, `[TickSummary]`, `[MapLoad]`, and the text after the tag does not match
    `^\w+=` (so plan 029's tool reads them as prose).

**Verify RED**: missing-type diagnostics for `MapProfileLines`.

GREEN: `Main/Features/MapPerf/MapProfileLines.cs`, `public static class MapProfileLines`:
`public const string StatusTag = "[MapProfiler]";`, `OffLine`, `RestartNeededLine`, `NotInstalledLine`
(texts exactly as under "Status lines" in Design), and
`BuildMapProfile(double tSeconds, MapWindow w, int parties, bool allocAvailable)`,
`BuildSummary(string reason, MapSummary s, bool allocAvailable)`,
`BuildInstallLine(bool coreApplied, bool viewsApplied, int targetsPatched, int targetsTotal, IReadOnlyList<string> missing, int viewsPatched, int viewsFound, int tickEventSites, bool walkerBound, bool allocAvailable)`,
`BuildWalkerUnboundLine(string detail)`, `BuildTickEventVanillaLine(int sites)`,
`BuildViewsShortLine(int patched, int found)`, `BuildSessionStartLine(int session, int topN, int fastForward, int extraFastForward)`,
`BuildSessionOffLine(int session)`, `BuildNoFramesLine(int session, string reason)`,
`BuildFault(string where, Exception ex)` (type name and the message with `[` `]` turned into `(` `)`,
CR and LF into spaces). A `StringBuilder` for the data lines; private `Num` (`0.00`), `Seconds` (`0`),
`Int`, `Kb` (`bytes / 1024` or `na`) and `Quote` helpers, all invariant. A class summary stating the
contract and that every other file logs only these members.

**Verify**: `--filter "FullyQualifiedName~MapProfileLinesTests"`: 10 passed. Copy the two data literals
into your report.

### Step 6: the listener walk and its reflection sites (TDD)

RED:
1. `TAOM.Tests/Features/MapPerf/TickEventListenerWalkerTests.cs`, class-tagged
   `[TestCategory("RequiresGame")]` (it constructs and invokes a real `MbEvent<float>`). Test-local
   owner classes `OwnerA`, `OwnerB`; listeners append a label to a shared `List<string>`. A fresh
   `MapFrameProfiler(1000)` with `BeginSession(0, true, 0, 0, 0)` and a first `Boundary` before each
   walk, so a closed frame (`AddAppTick(0)` then a second `Boundary`) exposes the entries through
   `TakeWindow`. `[TestInitialize]` asserts `TickEventListenerWalker.TryBind(out _)`.
   - `TryBind_InstalledEngine_Binds` (true, `Bound` true, failure text empty).
   - `InvokeTimed_CallsListenersInTheSameOrderAsInvoke`: two events built with the same registrations
     (A1, B1, A2 in that order); `Invoke` on one, `InvokeTimed` on the other; the two label sequences
     are equal (and are newest first: A2, B1, A1).
   - `InvokeTimed_ListenerThrows_SameExceptionAndSkipsTheRestLikeInvoke_AndRecordsTheCall`: the middle
     listener throws a specific `InvalidOperationException` instance; both paths throw that same
     instance (`Assert.AreSame`), run the same labels before it, and the walk's window shows the
     throwing owner's call recorded.
   - `InvokeTimed_ListenerClearsItself_ContinuesToItsSuccessorLikeInvoke`: a listener calls
     `evt.ClearListeners(owner)` on its own owner (the Messenger shape); the sequences match and the
     successor ran.
   - `InvokeTimed_ListenerAddsAListener_NewOneWaitsForTheNextDispatchLikeInvoke`.
   - `InvokeTimed_RecordsOneEntryPerOwnerType_AndFlagsTheGivenAssemblyAsTaom`: owners `OwnerA`,
     `OwnerB` and a plain `new object()`; pass `typeof(OwnerA).Assembly` as the TAOM assembly: entries
     `OwnerA`, `OwnerB`, `Object`, and the window's `TaomMs` equals the sum of the two test-owned
     entries' ms.
   - `InvokeTimed_NoListeners_RecordsNothing`.
2. `TAOM.Tests/Migration/ReflectionSiteBindingTests.cs`: four `[DataRow]`s, under a comment
   `// --- MapPerf map profiler: the TickEvent listener walk (TickEventListenerWalker.cs) ---`:
   `("TaleWorlds.CampaignSystem.MbEvent`1", "MbEvent`1", "_nonSerializedListenerList", "Field", "TickEventListenerWalker.cs")`,
   `("TaleWorlds.CampaignSystem.MbEvent`1+EventHandlerRec`1", "EventHandlerRec`1", "Next", "Field", "TickEventListenerWalker.cs")`,
   the same type with `"<Action>k__BackingField"` and with `"<Owner>k__BackingField"`.

Build; **Verify RED**: missing-type diagnostics for `TickEventListenerWalker` (the DataRows compile and
pass already; they are the catalogue, not the RED).

GREEN: `Main/Features/MapPerf/Hooks/TickEventListenerWalker.cs`, `internal static class`:
```csharp
private static AccessTools.FieldRef<MbEvent<float>, object>? _head;
private static AccessTools.FieldRef<object, object>? _next;
private static AccessTools.FieldRef<object, Action<float>>? _action;
private static AccessTools.FieldRef<object, object>? _owner;
internal static bool Bound { get; private set; }

/// Binds the four members once; false with a one-line reason when any is missing or of another type.
internal static bool TryBind(out string failure)
// head = AccessTools.Field(typeof(MbEvent<float>), "_nonSerializedListenerList"); rec = head.FieldType;
// next = AccessTools.Field(rec, "Next") (FieldType must be rec);
// action = AccessTools.Field(rec, "<Action>k__BackingField") (FieldType must be typeof(Action<float>));
// owner = AccessTools.Field(rec, "<Owner>k__BackingField") (FieldType must be typeof(object));
// then the four FieldRefAccess calls from Design; all inside try/catch (failure = "<Type>: <message>").

internal static void Unbind() // tests only: Bound = false

/// Vanilla InvokeList, statement for statement (MbEvent<T>.InvokeList, v1.5.3), with each call timed.
internal static void InvokeTimed(MbEvent<float> tickEvent, float dt, MapFrameProfiler profiler, Assembly taomAssembly)
{
    var rec = _head!(tickEvent);
    while (rec != null)
    {
        var action = _action!(rec);
        var owner = _owner!(rec);
        var type = owner?.GetType() ?? action?.Method.DeclaringType ?? typeof(object);
        var slot = profiler.Entries.SlotFor(type);
        var taom = type.Assembly == taomAssembly;
        var a0 = AllocationCounter.ReadOrZero();
        var t0 = Stopwatch.GetTimestamp();
        try { action!(dt); }   // a null action throws NullReferenceException, exactly as vanilla's list.Action(t)
        finally { profiler.RecordEntry(slot, Stopwatch.GetTimestamp() - t0, AllocationCounter.ReadOrZero() - a0, taom); }
        rec = _next!(rec);     // read AFTER the call, as vanilla does: a self-removed record keeps its Next
    }
}
```
A class summary quoting the vanilla loop and the four behaviours the walk must keep (order, exception,
self-removal, added-during-dispatch).

Then `docs/reference/taleworlds-api-snapshot/reflection-sites.md`, Category B, after plan 028's
`WaitTickCompletion` row: four rows (`TaleWorlds.CampaignSystem.MbEvent`1` `_nonSerializedListenerList`,
and `…MbEvent`1+EventHandlerRec`1` `Next`, `<Action>k__BackingField`, `<Owner>k__BackingField`; kind
field; site `TickEventListenerWalker.cs`; purpose "Patch101 map profiler: the TickEvent listener walk.
Missing: the walk stays unbound and listeners are not attributed (one warning)").

**Verify**: `--filter "FullyQualifiedName~TickEventListenerWalkerTests|FullyQualifiedName~ReflectionSiteBindingTests"`:
the 7 walker tests pass and the four new rows pass. If `TryBind` fails on the installed engine, or any
differential test shows a difference from `Invoke`, STOP (see STOP conditions).

### Step 7: hooks, patches, installer and PatchShield entries (TDD)

Read `docs/reviews/lessons/harmony-il.md` and the Patch36, Patch43 and Patch89 sections of
`docs/reference/harmony-patch-registry.md` first.

RED:
1. `TAOM.Tests/Features/MapPerf/TickEventSwapTests.cs` (no category; metadata only, as plan 028's
   `TickProfilerTranspilerTests`): a synthetic body `ldnull`, `ldarg.1`,
   `callvirt <AccessTools.Method(typeof(MbEvent<float>), "Invoke", new[] { typeof(float) })>`, `ret`.
   - `TickEventSwaps_SyntheticTickBody_SwapsTheOneInvoke` (`TickProfilerTranspiler.Rewrite(...,
     MapFrameProfilerInstaller.TickEventSwaps(), "CampaignEvents.Tick", logger, out swapped)`:
     `swapped` 1, the instruction is `OpCodes.Call` with operand `MapFrameProfilerHooks.TimedTickEvent`,
     no `LogWarning`).
   - `TickEventSwaps_HelperFitsTheTarget` (the helper is static, returns void, parameters
     `MbEvent<float>`, `float`).
   - `TickEventSwaps_InvokeTwice_LeavesTheBodyVanilla` (`swapped` 0, one `LogWarning`, opcodes
     unchanged).
2. `TAOM.Tests/Features/MapPerf/MapFrameProfilerHooksTests.cs`, class-tagged `RequiresGame`.
   `[TestInitialize]`: `MapFrameProfilerHooks.Profiler = new MapFrameProfiler(1000, windowSeconds: 1)`
   (synthetic ticks, a window every 1000 ticks), an NSubstitute `IModLogger` into
   `MapFrameProfilerHooks.Logger`, NSubstitute `IBattleLoadDiagnosticsSettingsProvider`
   (`MapProfilerEnabled` true, `TickProfilerTopN` 8) and `ITimeAccelerationSettingsProvider` (4, 8, 16)
   into `MapSessionHooks`, `MapSessionHooks.PartyCount = () => 2091`, and
   `TickEventListenerWalker.TryBind(out _)`; `[TestCleanup]` calls `MapSessionHooks.ResetForTests()`
   and nulls the hooks' statics. Cases:
   - `TimedTickEvent_NotMeasuring_InvokesEveryListenerAndRecordsNothing`.
   - `TimedTickEvent_Measuring_TimesTheDispatchAndEachOwner` (session open through `Step`; after a
     closed frame the window lists both owners with 1 call each).
   - `TimedTickEvent_WalkerUnbound_InvokesThroughAndStillTimesTheDispatch` (`TickEventListenerWalker.Unbind()`:
     every listener runs, no entry recorded).
   - `EndPhase_StartZero_RecordsNothing`.
   - `RecordView_Measuring_RecordsAnEntryForTheViewTypeAsTaomOwned` (an instance of a test class
     `ProbeView`; entry `ProbeView`; `TaomMs` counts it).
   - `Step_FirstBoundaryOfACampaign_OpensASessionAndLogsTheHeader` (`LogInfo(MapProfileLines.BuildSessionStartLine(1, 8, 4, 8))` once).
   - `Step_ToggleOffAtSessionStart_LogsNotMeasuringAndRecordsNothing` (`BuildSessionOffLine(1)`;
     `Measuring` false).
   - `Step_AnotherCampaign_WritesThePreviousSummaryThenOpensTheNext` (closed frames under campaign
     object A, then a `Step` with object B: one `LogInfo` starting
     `[MapProfileSummary] reason=newCampaign session=1 frames=`, then the session 2 header).
   - `Step_WindowDue_LogsOneMapProfileLine` (frames closed past 1000 ticks: one `LogInfo` starting
     `[MapProfile] t=+` and containing ` parties=2091 `).
   - `EndSession_ClosedFrames_LogsTheSummaryOnce` (`EndSession("gameEnd")` twice: one line starting
     `[MapProfileSummary] reason=gameEnd `).
   - `EndSession_NoClosedFrame_LogsTheNoFramesLine` (`BuildNoFramesLine(1, "gameEnd")`).
   - `Step_LoggerThrows_StopsMeasuringAndLogsOneError` (the substitute throws for a window line: one
     `LogError` starting `[MapProfiler] frame boundary failed, measuring stopped`, `Measuring` false,
     and a further `Step` logs nothing).
   Here `Step` is `MapSessionHooks.Step(object campaign, long nowTicks, long allocBytesNow, MapSkip skip, int mode, float multiplier)`,
   called with plain `object` instances standing for campaigns and synthetic ticks, and frames close
   with `MapFrameProfilerHooks.EndAppTick(...)` (or the profiler's `AddAppTick`) between `Step` calls.
3. `TAOM.Tests/Features/MapPerf/MapFrameProfilerInstallerTests.cs`, class-tagged `RequiresGame`
   (model on plan 028's `MissionTickProfilerInstallerTests`). The category apply is a fake that sets
   `MapFrameProfilerHooks.TickEventSites` as the real transpiler would and returns a chosen result; the
   patched check is a fake `Func<MethodBase, bool>`. `[TestInitialize]` and `[TestCleanup]` call
   `MapFrameProfilerInstaller.ResetForTests()`. Cases:
   `OnGameInitialized_ToggleOff_LogsTheOffLineAndAppliesNothing`;
   `OnGameInitialized_AllPatched_IsInstalledAndLogsThePinnedInstallLine` (expects
   `LogInfo(MapProfileLines.BuildInstallLine(true, true, 6, 6, Array.Empty<string>(), v, v, 1, true, AllocationCounter.Available))`
   with `v = MapFrameProfilerInstaller.MapViewTargets().Count`, a non-null `Profiler`, and
   `TickEventListenerWalker.Bound`);
   `OnGameInitialized_CoreCategoryFails_IsNotInstalled`;
   `OnGameInitialized_ATargetUnpatched_IsNotInstalledAndTheLineNamesIt`;
   `OnGameInitialized_ViewsCategoryFails_StillInstalls`;
   `OnGameInitialized_TickEventSiteMissing_StillInstallsAndLogsTheVanillaLine`;
   `OnGameInitialized_SecondCall_AppliesNothingAgain`;
   `OnGameInitialized_LaterCall_OffAtFirstOnNow_LogsRestartNeeded`;
   `OnGameInitialized_LaterCall_InstallFailedToggleOn_LogsNotInstalled`;
   `OnGameInitialized_ApplyThrows_LogsOneFault` (`LogError(MapProfileLines.BuildFault("install", boom))`).
4. `TAOM.Tests/Features/MapPerf/MapFrameProfilerBindingTests.cs` (model on plan 028's
   `MissionTickProfilerBindingTests`; `[ClassInitialize]` sets `_gameLoaded = GameAssemblies.EnsureLoaded()`;
   each test starts with `if (!_gameLoaded) Assert.Inconclusive(...)`; every method carries
   `[TestCategory("BindingVerification")]`; the three that read vanilla IL or TAOM's module types
   (`CampaignEventsTickRewrite_...`, `MbEventInvokeList_...`, `MapViewTargets_...`) also carry
   `[TestCategory("RequiresGameIL")]`; `EveryCoreTarget_...` and `ProfiledEngineTargets_...` read
   only signatures and attributes and do not, like 028's `ProfiledTargets_AreOnPatchShieldsExclusionList`,
   so they run in the RefAsm binding gate):
   - `CampaignEventsTickRewrite_FindsTheOneInvoke_InInstalledEngine` (real IL of
     `AccessTools.Method(typeof(CampaignEvents), nameof(CampaignEvents.Tick), new[] { typeof(float) })`
     through `Rewrite` with `TickEventSwaps()`: no warning, `swapped` 1, exactly one operand declared on
     `MapFrameProfilerHooks`).
   - `MbEventInvokeList_ReadsNextAfterTheCall_AndHasNoHandler` (`AccessTools.Method(typeof(MbEvent<float>), "InvokeList")`:
     `GetMethodBody().ExceptionHandlingClauses.Count` 0; in `PatchProcessor.GetOriginalInstructions`
     exactly one `callvirt` whose operand is a method named `Invoke` declared on a type whose generic
     definition is `typeof(Action<>)`, exactly one `ldfld` of a field named `Next`, and the `ldfld`
     comes after the `callvirt`).
   - `EveryCoreTarget_ResolvesInInstalledEngine` (`MapFrameProfilerInstaller.CoreTargets()` has six
     non-null entries, in the Design order).
   - `MapViewTargets_AreTheTaomMapViewPerFrameOverrides` (every entry is declared on a non-abstract TAOM
     type assignable to `MapView`, is named one of the four per-frame virtuals, takes one `float`; and
     the set contains the `OnMapScreenUpdate` overrides of `FieldCampMapView`, `MomentumIndicatorMapView`
     and `RealmBordersMapView`).
   - `ProfiledEngineTargets_AreOnPatchShieldsExclusionList` (walk the targets of the five engine patch
     classes with a copy of plan 028's private `TargetOf` helper and assert `PatchShieldPolicy.IsExcludedTargetMethod`;
     option B: only `Campaign_Tick_MapProfiler_Patch` and `CampaignEvents_Tick_MapProfiler_Patch`).

Build; **Verify RED**: missing-type diagnostics for `MapFrameProfilerHooks`, `MapSessionHooks`,
`MapFrameProfilerInstaller` and the patch classes. After the GREEN code compiles but BEFORE the
`PatchShieldPolicy` edit, run `--filter "FullyQualifiedName~ProfiledEngineTargets_AreOnPatchShieldsExclusionList"`
once and quote the failure naming `TaleWorlds.CampaignSystem.GameState.MapState.OnTick` (option B:
`TaleWorlds.CampaignSystem.Campaign.Tick`).

GREEN:
- `Main/Features/MapPerf/Hooks/MapFrameProfilerHooks.cs`, `public static class`:
  `internal static MapFrameProfiler? Profiler; internal static IModLogger? Logger; internal static int TickEventSites;`
  `internal static readonly Assembly TaomAssembly = typeof(MapFrameProfilerHooks).Assembly;`
  `internal static long BeginPhase()` (a timestamp when `Profiler` is non-null and measuring, else 0);
  `internal static void EndPhase(MapPhase phase, long start)` (returns when `start == 0`, else
  `AddPhase` with the elapsed ticks); `internal static void EndAppTick(long start)` (same, `AddAppTick`);
  `internal static ProbeStamp BeginProbe()` and `internal static void RecordView(object instance, ProbeStamp start)`
  (`RecordEntry(SlotFor(instance.GetType()), elapsed, allocDelta, taomOwned: true)`);
  `public readonly struct ProbeStamp` (`long Ticks`, `long Alloc`, public constructor) in the same file;
  ```csharp
  /// Replaces CampaignEvents.Tick's `callvirt MbEvent<float>::Invoke(float)` (Patch101 transpiler).
  public static void TimedTickEvent(MbEvent<float> tickEvent, float dt)
  {
      var profiler = Profiler;
      if (profiler == null || !profiler.Measuring) { tickEvent.Invoke(dt); return; }
      var t0 = Stopwatch.GetTimestamp();
      try
      {
          if (TickEventListenerWalker.Bound) TickEventListenerWalker.InvokeTimed(tickEvent, dt, profiler, TaomAssembly);
          else tickEvent.Invoke(dt);
      }
      finally { profiler.AddPhase(MapPhase.TickEvent, Stopwatch.GetTimestamp() - t0); }
  }
  ```
- `Main/Features/MapPerf/Hooks/MapSessionHooks.cs`, `public static class`:
  statics `internal static IBattleLoadDiagnosticsSettingsProvider? Settings; internal static ITimeControlAdapter? TimeControl;
  internal static ITimeAccelerationSettingsProvider? Acceleration; internal static Func<int> PartyCount = ReadPartyCount;`
  (a method group, so no per-frame allocation), a `private static WeakReference? _session`, the cached
  `_topN`, `_fastForward`, `_extraFastForward`, and:
  ```csharp
  /// The Patch101 frame boundary (MapState.OnTick prefix): the engine reads, then Step.
  public static void OnFrameBoundary()
  {
      if (MapFrameProfilerHooks.Profiler == null) return;
      var campaign = Campaign.Current;
      if (campaign == null) return;
      var skip = LoadingWindow.IsLoadingWindowActive ? MapSkip.Loading
          : ScreenManager.TopScreen is MapScreen ? MapSkip.None : MapSkip.NotTop;
      var time = TimeControl;
      Step(campaign, Stopwatch.GetTimestamp(), AllocationCounter.ReadOrZero(), skip,
          time?.TimeControlMode ?? -1, time?.SpeedUpMultiplier ?? float.NaN);
  }
  ```
  wrapped in `try`/`catch` like `Step`. `internal static void Step(object campaign, long nowTicks, long allocBytesNow, MapSkip skip, int mode, float multiplier)`:
  inside `try`: when `_session?.Target != campaign`, `CloseSession("newCampaign")` then `OpenSession`
  (read `Settings?.TickProfilerTopN ?? 8` and the two multipliers, `?? 4` and `?? 8` when
  `Acceleration` is null, `measuring = Settings?.MapProfilerEnabled ?? false`,
  `BeginSession(nowTicks, measuring, GC.CollectionCount(0), GC.CollectionCount(1), GC.CollectionCount(2))`,
  `_session = new WeakReference(campaign)`, log the session line); return when not measuring;
  `Boundary(nowTicks, allocBytesNow, skip, MapSpeed.Classify(mode, multiplier, _fastForward, _extraFastForward))`;
  when `WindowDue(nowTicks)`: re-read the multipliers, `TakeWindow(nowTicks, _topN, GC counts)` and
  `LogInfo(MapProfileLines.BuildMapProfile(profiler.SecondsSinceSessionStart(nowTicks), window, PartyCount(), AllocationCounter.Available))`.
  `catch`: `Fault("frame boundary", ex)`. `public static void EndSession(string reason)` (returns when
  no profiler or no open session; else `CloseSession(reason)` in `try`/`catch` with `Fault("session end", ex)`).
  `CloseSession`: `_session = null`; when measuring, `Summarize(_topN)` and log the summary (frames > 0)
  or the no-frames line, then `EndSession(Session)`. `Fault`: `EndSession(Session)` and one `LogError`
  of `BuildFault`, itself inside `try { } catch { }`. `private static int ReadPartyCount()` returns
  `Campaign.Current?.MobileParties?.Count ?? -1`. `internal static void ResetForTests()` clears the
  session, the statics and restores `PartyCount`.
- `Main/Features/MapPerf/Hooks/Patch101_MapFrameProfiler.cs`: a header comment quoting the six target
  signatures and the IL facts from "Current state", then six `public static class`es, each with
  `[HarmonyPatchCategory(MapFrameProfilerInstaller.Category)]`; every prefix carries
  `[HarmonyPrefix, HarmonyPriority(Priority.First)]` and every postfix `[HarmonyPostfix, HarmonyPriority(Priority.Last)]`:
  ```csharp
  [HarmonyPatch(typeof(MapState), "OnTick", new[] { typeof(float) })]
  public static class MapState_OnTick_MapProfiler_Patch
  {
      public static void Prefix(out long __state) { MapSessionHooks.OnFrameBoundary(); __state = MapFrameProfilerHooks.BeginPhase(); }
      public static void Postfix(long __state) => MapFrameProfilerHooks.EndPhase(MapPhase.MapState, __state);
  }
  [HarmonyPatch]  // TargetMethod: AccessTools.Method(typeof(Campaign), "RealTick", new[] { typeof(float) })
  public static class Campaign_RealTick_MapProfiler_Patch { /* BeginPhase / EndPhase(MapPhase.RealTick) */ }
  [HarmonyPatch]  // TargetMethod: AccessTools.Method(typeof(Campaign), "Tick", Type.EmptyTypes)
  public static class Campaign_Tick_MapProfiler_Patch { /* MapPhase.CampaignTick */ }
  [HarmonyPatch(typeof(CampaignEvents), nameof(CampaignEvents.Tick), new[] { typeof(float) })]
  public static class CampaignEvents_Tick_MapProfiler_Patch
  {
      [HarmonyTranspiler]
      public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
      {
          var result = TickProfilerTranspiler.Rewrite(instructions, MapFrameProfilerInstaller.TickEventSwaps(),
              "CampaignEvents.Tick", MapFrameProfilerHooks.Logger, out var swapped);
          MapFrameProfilerHooks.TickEventSites = swapped;
          return result;
      }
  }
  [HarmonyPatch(typeof(MapScreen), "OnFrameTick", new[] { typeof(float) })]
  public static class MapScreen_OnFrameTick_MapProfiler_Patch { /* MapPhase.MapScreen */ }
  [HarmonyPatch]  // TargetMethod: AccessTools.Method(typeof(SubModule), "OnApplicationTick", new[] { typeof(float) })
  public static class SubModule_OnApplicationTick_MapProfiler_Patch { /* BeginPhase / EndAppTick */ }
  ```
- `Main/Features/MapPerf/Hooks/Patch101_MapFrameProfilerViews.cs`: one class
  `MapView_PerFrame_MapProfiler_Patch` with `[HarmonyPatch]`,
  `[HarmonyPatchCategory(MapFrameProfilerInstaller.ViewsCategory)]`,
  `static IEnumerable<MethodBase> TargetMethods() => MapFrameProfilerInstaller.MapViewTargets();`,
  `Prefix(out ProbeStamp __state) => __state = MapFrameProfilerHooks.BeginProbe();` and
  `Postfix(object __instance, ProbeStamp __state) => MapFrameProfilerHooks.RecordView(__instance, __state);`
  with the same priorities.
- `Main/Features/MapPerf/Hooks/MapFrameProfilerInstaller.cs`, `internal static class` with NO static
  field initializer that names an engine or module type (so the RefAsm unit step can load it for
  `TickEventSwapTests`):
  `internal const string Category = "Patch101_MapFrameProfiler"; internal const string ViewsCategory = "Patch101_MapFrameProfiler_Views";`
  flags `_attempted`, `_offAtFirst`, `_installed`;
  `internal static IReadOnlyList<CallSwap> TickEventSwaps()` (one swap: `MbEvent<float>.Invoke(float)` to
  `MapFrameProfilerHooks.TimedTickEvent`);
  `internal static IReadOnlyList<MethodBase?> CoreTargets()` and `internal static IReadOnlyList<string> CoreTargetNames`
  (the six, in the Design order);
  `internal static List<MethodBase> MapViewTargets()`: TAOM's assembly types (on a
  `ReflectionTypeLoadException` use its non-null `Types`), non-abstract and assignable to `MapView`,
  and for each the methods `OnFrameTick`, `OnMapScreenUpdate`, `OnMenuModeTick`, `OnIdleTick` found with
  `BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly` and
  parameter types `{ typeof(float) }`, sorted by declaring type full name then method name; never throws
  (returns what it found);
  `internal static void OnGameInitialized(IBattleLoadDiagnosticsSettingsProvider settings, ITimeControlAdapter timeControl, ITimeAccelerationSettingsProvider acceleration, IModLogger logger, Func<string, bool> tryPatchCategory)`
  delegating to an overload with `Func<MethodBase, bool> isPatched` (default `IsPatchedByThisProfiler`:
  `Harmony.GetPatchInfo(m)` has a prefix, postfix or transpiler whose `PatchMethod.DeclaringType` is in
  this namespace and named `*_MapProfiler_Patch`). The overload, entirely inside `try`/`catch`
  (`LogError(BuildFault("install", ex))`, nothing installed):
  ```
  if (_attempted) { LaterGameInit(settings, logger); return; }
  _attempted = true;
  if (!settings.MapProfilerEnabled) { _offAtFirst = true; logger.LogInfo(OffLine); return; }
  set MapFrameProfilerHooks.Logger, MapSessionHooks.Settings / TimeControl / Acceleration;
  walker = TickEventListenerWalker.TryBind(out var why); if (!walker) logger.LogWarning(BuildWalkerUnboundLine(why));
  core = tryPatchCategory(Category); views = tryPatchCategory(ViewsCategory);
  missing = names of CoreTargets() that are null or !isPatched;
  viewTargets = MapViewTargets(); viewsPatched = count isPatched;
  _installed = core && missing.Count == 0;
  MapFrameProfilerHooks.Profiler = _installed ? new MapFrameProfiler(Stopwatch.Frequency) : null;
  logger.LogInfo(BuildInstallLine(core, views, 6 - missing.Count, 6, missing, viewsPatched, viewTargets.Count,
      MapFrameProfilerHooks.TickEventSites, walker, AllocationCounter.Available));
  if (_installed && MapFrameProfilerHooks.TickEventSites != 1) logger.LogWarning(BuildTickEventVanillaLine(sites));
  if (_installed && viewsPatched < viewTargets.Count) logger.LogWarning(BuildViewsShortLine(viewsPatched, viewTargets.Count));
  ```
  `LaterGameInit`: nothing when installed or when the toggle reads off; else `LogInfo(RestartNeededLine)`
  when `_offAtFirst`, otherwise `LogWarning(NotInstalledLine)`. `internal static void ResetForTests()`
  clears the three flags and the hooks' statics.
  If this file reaches 150 lines, the one allowed split: move `TickEventSwaps`, `CoreTargets`,
  `CoreTargetNames` and `MapViewTargets` unchanged into `internal static class MapProfilerTargets` in
  `Main/Features/MapPerf/Hooks/MapProfilerTargets.cs` and point every caller (the patch classes and
  the tests) at it; nothing else moves.
- `Dependencies/Foundation/PatchShieldPolicy.cs`: append to `ExcludedTargetMethods`. Option A, with
  this comment:
  ```csharp
  // Per-frame campaign-map targets (Patch101 map profiler, <your commit date>), unconditional like the
  // entries above. Every player: Campaign.RealTick carries Patch89's heartbeat and MapScreen.OnFrameTick
  // Patch89's first-frame trace (both shielded from the first game start) and Patch36's F6 postfix;
  // MapState.OnTick carries Patch43's postfix (shielded from a second game start). With the profiler
  // on, all five carry Patch101; Campaign.Tick and CampaignEvents.Tick carry nothing else. Given up,
  // for any owner: the swallow of a missing-API exception thrown anywhere inside these methods (for
  // MapState.OnTick, the whole campaign tick) and the strip of their patches. Such an exception now
  // reaches Patch37's crash-capture finalizer on Module.OnApplicationTick or ScreenManager.Tick and
  // skips the rest of that application or screen tick, every frame it recurs
  // (docs/features/map-perf-profiler.md, "PatchShield").
  // MapFrameProfilerBindingTests.ProfiledEngineTargets_AreOnPatchShieldsExclusionList walks the real targets.
  "TaleWorlds.CampaignSystem.GameState.MapState.OnTick",
  "TaleWorlds.CampaignSystem.Campaign.RealTick",
  "TaleWorlds.CampaignSystem.Campaign.Tick",
  "TaleWorlds.CampaignSystem.CampaignEvents.Tick",
  "SandBox.View.Map.MapScreen.OnFrameTick",
  ```
  Option B, only the two Patch101-only entries, with this comment:
  ```csharp
  // Per-frame campaign-map targets that only the Patch101 map profiler patches (<your commit date>),
  // unconditional like the entries above; excluding them changes nothing for a player with the
  // profiler off. MapState.OnTick, Campaign.RealTick and MapScreen.OnFrameTick, which Patch43, Patch89
  // and Patch36 patch for every player, keep the shield by the maintainer's decision (<your commit
  // date>): their once-per-frame finalizer stays inside the map profiler's numbers, and a swallow there
  // also strips Patch101's pair on that method (docs/features/map-perf-profiler.md, "PatchShield").
  // MapFrameProfilerBindingTests.ProfiledEngineTargets_AreOnPatchShieldsExclusionList walks the real targets.
  "TaleWorlds.CampaignSystem.Campaign.Tick",
  "TaleWorlds.CampaignSystem.CampaignEvents.Tick",
  ```

**Verify**: `--filter "FullyQualifiedName~TickEventSwapTests|FullyQualifiedName~MapFrameProfilerHooksTests|FullyQualifiedName~MapFrameProfilerInstallerTests"`:
3 + 12 + 10 passed. The local binding gate: all pass, none inconclusive, including the five
`MapFrameProfilerBindingTests`, the four new `ReflectionSiteBindingTests` rows and
`HarmonyPatchBindingTests` (which now resolves the seven new patch classes, the views class through
`TargetMethods()`). `wc -l Main/Features/MapPerf/Hooks/*.cs`: every file under 150 lines.

### Step 8: the two `SubModule.cs` insertions (TDD for the wiring)

RED: `TAOM.Tests/Features/MapPerf/MapFrameProfilerWiringTests.cs` (no category; source pins on
`RepoPaths.ReadSource("Main/SubModule.cs", stripComments: true)`, the shape of plan 028's
`MissionTickProfilerWiringTests`):
- `SubModule_CallsTheInstaller_OnEveryGameInit_BeforeTheOncePerProcessGuard`: the text
  `Features.MapPerf.Hooks.MapFrameProfilerInstaller.OnGameInitialized(` appears exactly once, after
  `public override void OnGameInitializationFinished` and after `ApplyGating(`, and before
  `if (_gameInitPatchesApplied) return;`.
- `SubModule_EndsTheMapSession_InOnGameEnd`: `Features.MapPerf.Hooks.MapSessionHooks.EndSession("gameEnd")`
  appears exactly once, after `public override void OnGameEnd(Game game)` and before
  `protected override void OnGameStart(`.
Verify both fail.

GREEN, `Main/SubModule.cs` (nothing else in this file changes):
- Insertion 1, immediately after the line
  `IoC.Resolve<Features.ArmourAcquisition.IArmourGateService>().ApplyGating(game?.GameType is Campaign);`
  and its following blank line, before the `// Harmony patches are process-global` comment:
  ```csharp
  // Patch101 map frame profiler (default off; docs/features/map-perf-profiler.md): every game init,
  // before the once-per-process guard, because a later game init only reports (restart needed, or the
  // install failed). The installer applies its categories at most once per process and contains its
  // own failures; the four services it takes are registered singletons (BattleLoadDiagnosticsIoC,
  // TimeAccelerationIoC, the logger), resolved here before its try.
  Features.MapPerf.Hooks.MapFrameProfilerInstaller.OnGameInitialized(
      IoC.Resolve<Features.BattleLoadDiagnostics.IBattleLoadDiagnosticsSettingsProvider>(),
      IoC.Resolve<Features.TimeAcceleration.ITimeControlAdapter>(),
      IoC.Resolve<Features.TimeAcceleration.ITimeAccelerationSettingsProvider>(),
      IoC.Resolve<IModLogger>(),
      TryPatchCategory);

  ```
- Insertion 2, at the end of `OnGameEnd`, after the FactionUI `try`/`catch` and before the method's
  closing brace:
  ```csharp

  // [MapProfileSummary] for the campaign session that just ended (Patch101, default off; a no-op when
  // the map profiler is not installed). An in-campaign load can skip OnGameEnd, so the profiler also
  // closes a session when it sees a new campaign.
  try { Features.MapPerf.Hooks.MapSessionHooks.EndSession("gameEnd"); }
  catch { /* diagnostic is best-effort, never break OnGameEnd */ }
  ```

**Verify**: the Build exits 0; `--filter "FullyQualifiedName~MapFrameProfilerWiringTests|FullyQualifiedName~MissionTickProfilerWiringTests"`
passes (plan 028's pins must still hold); `git diff <start> -- Main/SubModule.cs` shows exactly these two
insertions.

### Step 9: docs

- `docs/features/map-perf-profiler.md` (new, from `docs/features/TEMPLATE.md`, adapting its sections):
  Overview; Why This Exists (the 7 to 8 fps spawn-in observation with its source line, the [MapLoad]
  gap); Architecture (the frame model, the targets table, nesting: `realTickMs` and `campaignTickMs`
  inside `mapStateMs`, `tickEventMs` inside `campaignTickMs`, listener entries inside `tickEventMs`;
  view entries per override: `OnMapScreenUpdate` inside `mapScreenMs` (TAOM's three views today),
  `OnFrameTick` and `OnMenuModeTick` inside `mapStateMs`, `OnIdleTick` never in a closed frame; that a
  `Play` or `FF` frame can advance no campaign time while the main party waits (StoppablePlay,
  StoppableFastForward); the identity `wallMs = mapStateMs + mapScreenMs + appTickMs +
  otherMs`; the continuity rule and the three skip reasons; sessions; why the listener walk and not
  per-listener patches); Configuration (the toggle, its default, restart behaviour, and that it reuses
  "Tick Profiler Top Behaviours"); **Log lines**: every line with its fields and the pinned examples
  (`[MapProfile]`, `[MapProfileSummary]` and all eleven `[MapProfiler]` status texts), the meaning of
  `taomMs` and `otherMs`, and that the `[TickProfiler]`-tagged warning from plan 028's transpiler can
  appear for `CampaignEvents.Tick`; what is NOT covered (vanilla map views individually, other mods'
  patches inside the brackets, Patch89's census inside `realTickMs` on its emit frames, periodic events
  and hourly ticks only as part of `campaignTickMs`, worker-thread allocation); Cost (off, on, on but
  not measuring); PatchShield (the option taken and its entries; when each method was shielded
  before; for option A, where an escaping exception is now caught, naming Patch37's
  "crash-capture finalizer" on `Module.OnApplicationTick` and `ScreenManager.Tick`, and what one
  throw skips, from "Current state", PatchShield; the per-frame finalizers that remain, from Design;
  the one `not shielding` diag.log line per entry); Key Files; Tests; Reading the lines
  (compare runs at the same speed class and party count; the summary's `byspeed`).
- `docs/reference/feature-map.md`: a `MapPerf` row right after the `MissionPerf` row:
  `| MapPerf | \`Main/Features/MapPerf/\`: off by default, the Patch101 campaign map profiler (\`[MapProfile]\` every 5 s while the map ticks: the campaign tick, each tick-event listener by owner, the map screen and TAOM's map views, TAOM's application tick, main-thread allocation; \`[MapProfileSummary]\` per campaign session). Toggle on the Battle Load Diagnostics page. See [map-perf-profiler.md](../features/map-perf-profiler.md) |`
- `docs/reference/harmony-patch-registry.md`: `## Patch101_MapFrameProfiler` after the last
  `## PatchNN_` section and before the `<!-- backlinks-start` marker: both categories, every target
  with its signature, the transpiler swap and the prefix/postfix pairs with their priorities, why the
  installer runs on every game init but applies once, the listener walk's equivalence to `InvokeList`,
  the soft-fail rule, threads (main only), the PatchShield entries, the MCM toggle (default off), status
  ACTIVE (off by default), and a pointer to `docs/features/map-perf-profiler.md`. Option A only: in
  the `## Patch36_FiefManagement`, `## Patch43_BattleLoadDiagnostics` and
  `## Patch89_MapLoadDiagnostics` sections, one sentence each, of this form: "Since the Patch101 map
  profiler, `<target>` is on PatchShield's `ExcludedTargetMethods` for every player: a missing-API
  exception thrown inside it, by this patch or by code it encloses, is no longer swallowed there and
  this patch is no longer stripped; it reaches Patch37's crash-capture finalizer on `<catcher>`
  (`docs/features/map-perf-profiler.md`, PatchShield)." with `<target>` and `<catcher>`:
  Patch36 `MapScreen.OnFrameTick` and `ScreenManager.Tick`; Patch43 `MapState.OnTick` and
  `Module.OnApplicationTick`; Patch89 `Campaign.RealTick` and `MapScreen.OnFrameTick`, caught on
  `Module.OnApplicationTick` and `ScreenManager.Tick` respectively. Option B: no sentence there; the
  Patch101 section says the three keep the shield and why.
- Re-read every sentence you wrote against the code it describes (the hint texts, the doc comments,
  the registry text and the feature doc).

**Verify**: `python tools/lint_docs.py --fail-on-drift` exits 0;
`git grep -n "## Patch101_MapFrameProfiler" -- docs/reference/harmony-patch-registry.md` prints one line.

### Step 10: full verification and commit

1. The RefAsm build, the RefAsm unit step and the RefAsm binding gate (Commands table): quote each
   totals line. No test in `TAOM.Tests/Features/MapPerf` may fail in the unit step; the binding gate's
   failure set equals Step 1's, and `EveryCoreTarget_ResolvesInInstalledEngine` and
   `ProfiledEngineTargets_AreOnPatchShieldsExclusionList` are among its passed tests. If a new row or
   test (the four `ReflectionSiteBindingTests` rows, those two, or `HarmonyPatchBindingTests` for
   `MapView_PerFrame_MapProfiler_Patch`) fails only here, STOP (see STOP conditions). Run these first;
   the normal build in item 2 then rebuilds against the installed game.
2. `dotnet build Main/TAOM.csproj -p:DisableModuleCopy=true -p:ModuleId=`: exit 0.
3. `dotnet test TAOM.Tests -p:DisableModuleCopy=true -p:ModuleId=`: quote the totals line. Expected:
   Step 1's totals plus 83 passing results (Test plan), the one failure
   `EveryLanguage_DeclaresARowForEveryEnglishKey`, Skipped unchanged. A new test reported skipped is
   named and explained.
4. The local binding gate: all pass, none inconclusive; quote its totals.
5. `python tools/validate_moduledata.py`: 0 ERRORs.
6. `python tools/lint_docs.py --fail-on-drift`: exit 0.
7. The Done-criteria greps.
8. `git status --porcelain`: only in-scope paths.
9. Commit as in "Git workflow".

**Verify**: `git log -1 --format=%s` shows the subject; `git status --porcelain` is empty for in-scope
paths.

## Test plan

- New classes under `TAOM.Tests/Features/MapPerf/`: `MapSpeedTests` (8), `MapFrameProfilerTests` (21),
  `MapProfileLinesTests` (10), `TickEventSwapTests` (3), `MapFrameProfilerWiringTests` (2): no
  category, so they also run in hosted CI's unit step. `TickEventListenerWalkerTests` (7),
  `MapFrameProfilerHooksTests` (12), `MapFrameProfilerInstallerTests` (10): `RequiresGame`.
  `MapFrameProfilerBindingTests` (5): `BindingVerification`, three of them plus `RequiresGameIL` (the
  core-target and PatchShield walks run in the RefAsm binding gate too). Plus one provider
  test in `BattleLoadDiagnosticsSettingsProviderTests` and four `[DataRow]`s in
  `ReflectionSiteBindingTests`. Total: 83 new results, each test method one result and each data row
  one.
- Changed pins: `SettingsFingerprintTests` (`BattleLoadDiagnosticsSettings` count plus one),
  `SettingRequireRestartPostureTests` (one allowlist entry with its reason).
- Patterns: plan 028's `MissionTickProfilerTests` (pure arithmetic with `ticksPerSecond: 1000`),
  `TickProfileLinesTests` (literal pins), `TickProfilerTranspilerTests` (synthetic IL),
  `MissionTickProfilerBindingTests` (real IL, `TargetOf`, the PatchShield walk),
  `MissionTickProfilerInstallerTests` (fake category apply), `MissionTickProfilerWiringTests` (source pins).
- Cannot be tested offline (the commit's `Not-tested:` trailer): Harmony actually applying Patch101
  in the game (including the patches on TAOM's own `SubModule.OnApplicationTick` and map views), the
  in-game numbers, and the engine reads in `MapSessionHooks.OnFrameBoundary` and `ReadPartyCount`.
  Everything else has a test that failed first.

## Done criteria

Machine-checkable. ALL must hold:

- [ ] `dotnet build Main/TAOM.csproj -p:DisableModuleCopy=true -p:ModuleId=` exits 0
- [ ] The full test command reports Step 1's totals plus 83 passing results, the one failure
      `EveryLanguage_DeclaresARowForEveryEnglishKey`, every new class among the executed
- [ ] The RefAsm unit step shows no failing test in `TAOM.Tests/Features/MapPerf`; the RefAsm binding
      gate (run with `BANNERLORD_GAME_DIR` on the `refasm-game` folder) has the failure set Step 1
      recorded; or both are reported "RefAsm not run (environment)" with the restore error
- [ ] The local binding gate passes with none inconclusive, `MapFrameProfilerBindingTests` included
- [ ] `git grep -n -E 'Log(Info|Warning|Error)\(\$?"' -- Main/Features/MapPerf` returns nothing (every
      text comes from `MapProfileLines`)
- [ ] `git grep -n "Patch101_MapFrameProfiler" -- Main/SubModule.cs` returns nothing (applied only through
      the installer), and `git grep -n "## Patch101_MapFrameProfiler" -- docs/reference/harmony-patch-registry.md`
      returns one line
- [ ] `git grep -n -e "MapState.OnTick\"" -e "Campaign.RealTick\"" -e "Campaign.Tick\"" -e "CampaignEvents.Tick\"" -e "MapScreen.OnFrameTick\"" -- Dependencies/Foundation/PatchShieldPolicy.cs`
      lists the five new entries (option B: `Campaign.Tick"` and `CampaignEvents.Tick"` only, and no
      `MapState.OnTick"`, `Campaign.RealTick"` or `MapScreen.OnFrameTick"` line)
- [ ] The trade-off is written as re-derived, not as "no longer rescues a patch": option A,
      `git grep -c "crash-capture finalizer" -- Dependencies/Foundation/PatchShieldPolicy.cs docs/features/map-perf-profiler.md docs/reference/harmony-patch-registry.md`
      shows all three files, the registry with at least 3; option B,
      `git grep -n "keep the shield by the maintainer's decision" -- Dependencies/Foundation/PatchShieldPolicy.cs`
      prints one line
- [ ] `git grep -n -e "Every TAOM setting but one" -e "setting a Harmony category is gated on at apply time" -e "is the one such setting" -e "Two are allowlisted" -- TAOM.Tests docs`
      returns nothing
- [ ] `wc -l Main/Features/MapPerf/Hooks/*.cs` shows every file under 150 lines
- [ ] `git grep -n -P "[\x{2013}\x{2014}]" -- Main/Features/MapPerf TAOM.Tests/Features/MapPerf docs/features/map-perf-profiler.md`
      returns nothing (no em or en dash in new prose)
- [ ] `python tools/lint_docs.py --fail-on-drift` exits 0
- [ ] `git diff <start> -- Main/SubModule.cs` shows exactly the two insertions of Step 8
- [ ] `git status --porcelain` lists only in-scope files; one commit on your branch
- [ ] Every comment, doc line and test oracle this plan supplied was re-checked against the code it
      describes (the literals and oracles included)

## STOP conditions

Stop and report (do not improvise) if:

- `765d3759` is not an ancestor of your HEAD, or plan 028's members quoted in "Current state" are
  missing or have other signatures (Step 1 item 3).
- The PatchShield facts under "Current state" no longer hold at your base (the drift check shows
  `PatchShield.cs`, `Dependencies/SubModule.cs`, `Patch37_CrashReport.cs` or the Patch36, Patch43 or
  Patch89 apply points in `Main/SubModule.cs` changed): the trade-off text would be wrong; report it.
- The installed `CampaignEvents.Tick` IL does not hold exactly one `callvirt MbEvent<float>::Invoke(float)`
  (`CampaignEventsTickRewrite_FindsTheOneInvoke_InInstalledEngine` fails, or `swapped` is not 1),
  including the case where Harmony's `CodeInstruction.Calls` does not match the closed-generic operand.
  Report the instructions you saw; do not loosen the match.
- `MbEvent<float>.InvokeList` has an exception handler, reads `Next` before the call, or calls the
  action more than once (`MbEventInvokeList_ReadsNextAfterTheCall_AndHasNoHandler` fails), or any
  `TickEventListenerWalkerTests` differential case shows the walk behaving differently from `Invoke`:
  the TickEvent dispatch could not be wrapped without changing invocation order or exception behaviour.
- `TickEventListenerWalker.TryBind` fails on the installed engine.
- One of the four new `ReflectionSiteBindingTests` rows passes locally but fails in the RefAsm binding
  gate (a compiler-generated backing field missing from the reference assemblies): report it; do not
  move or drop the rows.
- `HarmonyPatchBindingTests` passes locally but fails in the RefAsm binding gate for
  `MapView_PerFrame_MapProfiler_Patch` (for example "TargetMethods() returned an empty sequence",
  `HarmonyPatchBindingTests.cs:115`, when `MapView` or TAOM's views do not resolve against the
  reference assemblies), or `EveryCoreTarget_ResolvesInInstalledEngine` or
  `ProfiledEngineTargets_AreOnPatchShieldsExclusionList` fails only there: report the message; do not
  add a category or make `MapViewTargets()` return a placeholder to pass it.
- A signature or behaviour in "Engine facts" differs from the installed engine (report the mismatch;
  do not decompile and improvise).
- The transpiler would need anything beyond the single operand swap (an inserted instruction, a new
  branch, a removed instruction).
- The wiring needs anything beyond the two `SubModule.cs` insertions (an `IoC.cs` line, a
  `FeatureModules` entry, a csproj edit).
- `Patch101_` is already taken at your base.
- After Step 2 the reflected `BattleLoadDiagnosticsSettings` count is not your base's plus one, the
  total is not your base's plus one, or the simulation-relevant count moves from 217.
- A test outside this plan's new classes fails that did not fail in Step 1.
- A step's verification fails twice after a reasonable fix.

## Orchestrator steps (not the executor's)

- Issue: file it before dispatch from the drafted text and name it in the commit body if it exists by
  then.
- No `/localize`: the Battle Load Diagnostics MCM page is not localised (its hint texts carry no
  `{=KEY}`), and log lines are not player-facing text.
- Decision D1 before dispatch (the maintainer's, one question): PatchShield option A or B (Design,
  "PatchShield"). A follows the house rule for per-frame targets and removes PatchShield's finalizer
  from five map methods, but changes crash handling for every player on three methods other patches
  share: a missing-API exception there moves out to TAOM's crash capture, skips the rest of that
  application or screen tick, and a broken patch there is no longer removed. B excludes only the two
  methods nothing else patches, changes nothing for a player with the profiler off, and leaves one
  PatchShield finalizer per frame on each of the three inside the measurements until plan 034. The
  reviser's recommendation is B, because a default-off diagnostic should not change every player's
  crash handling; A needs no extra word because it is the house rule. Tell the executor the answer;
  no answer means A. Answered 2026-10-03: B (D13).
- This plan is anchored on plan 028's reviewed tip `765d3759`; if 028's branch moved past it, or 040
  or 041 landed first, tell the executor which (040 and 041 move the settings counts and the restart
  allowlist).
- After review, `/verify-bindings` to refresh `docs/reference/taleworlds-api-snapshot/patch-targets.md`
  (seven new patch classes).
- Add a backlog item for plan 029's tool: a map mode in `tools/perf_runs.py` that turns
  `[MapProfile]` and `[MapProfileSummary]` lines into per-session rows and an A/B compare by speed class
  (today the tool keeps them only as generic extra tags on the preceding mission's row).
- The feature doc and its feature-map row are written by the executor in Step 9; check them.

## After merge: the maintainer's actions

- Pull and build. To measure: MCM, Battle Load Diagnostics, Map Performance, "Enable Map Profiler" on
  (MCM asks to restart), restart, start or load a campaign. In-game check (FOR-MIKE): the install line
  `[MapProfiler] install: category applied, views category applied, targets 6/6 patched (missing none), MapView overrides 3/3 patched, CampaignEvents.Tick sites 1/1, listener walk bound, allocation counter available`
  at game start; a `[MapProfiler] session 1: measuring, ...` line; during the map load, `[MapProfile]`
  lines with `frames=0` and growing `skipped`; then a line every 5 s with `frames` near the `[MapLoad]`
  heartbeat's for the same window, `speed=Stop` paused and `speed=FF` in fast-forward, and TAOM
  listeners (`FieldCommissionBehavior`, `RealmBordersCampaignBehavior`, ...) and map views in `top=`;
  quit to the main menu and find one `[MapProfileSummary] reason=gameEnd`. Then the same with the
  toggle off: one `[MapProfiler] off:` line and no `[MapProfile]` line; compare the `[MapLoad]` fps with
  the profiler on and off at the same speed (the profiler's own overhead). Close the issue with
  `triage-needs-ingame` until that check is done.

## Maintenance notes

- The `[MapProfile]` and `[MapProfileSummary]` formats are pinned by `MapProfileLinesTests`; change them
  only together with the feature doc and any parser that reads them (plan 029's follow-up). Status
  lines stay on `[MapProfiler]` and never start their body with `key=value`.
- Plan 034 changes PatchShield; if it rewrites `ExcludedTargetMethods`, keep the Patch101 entries of
  the option taken and the binding test that walks them. If 034 makes PatchShield's no-exception path
  free, option A's three shared entries (`MapState.OnTick`, `Campaign.RealTick`,
  `MapScreen.OnFrameTick`) can go back to the shield there, which restores the swallow and the strip
  described under "Current state", PatchShield.
- Plans 040 and 041 also add MCM settings: the second to merge recomputes `SettingsFingerprintTests`
  and the two co-op docs' counts, and the restart-posture summary and `mcm.md` list every profiler
  toggle.
- Plan 037 (campaign hot-path fixes) is judged with this profiler: run the same save at the same speed
  class before and after, and compare `taomMs`, the listener entries and `campaignTickMs`.
- An engine bump that moves the `CampaignEvents.Tick` call site or changes `InvokeList` fails
  `MapFrameProfilerBindingTests` instead of silently attributing nothing; at run time the transpiler
  leaves vanilla IL and the install line reports `CampaignEvents.Tick sites 0/1`.
- Review probes: that the walk reads `Next` after the call and catches nothing; that `taomMs` counts
  only TAOM-owned entries plus `appTickMs`; the continuity rule (exactly one TAOM application tick per
  closed frame) and that the speed and skip of a frame are the values read at its START boundary; that
  the session holds the campaign only through a `WeakReference`; that nothing allocates per frame after
  warm-up (`PartyCount` is a cached method group); the installer's once-only apply and its later-init
  reports; that no status text contains a data tag.
- Deferred, with reasons: timing vanilla map views individually (their loops are closures; their time
  is inside `mapScreenMs`, or `mapStateMs` for their `OnFrameTick` and `OnMenuModeTick`); splitting other mods' and TAOM's patch bodies out of the brackets (an
  inner and outer bracket pair per target, worth it only if `realTickMs` or `mapScreenMs` turns out
  large); attributing the periodic events inside `Campaign.Tick` (hourly and daily ticks, partial hourly
  AI) per listener, which the same walk could do for the non-generic `MbEvent` lists; a map hitch line
  (`maxFrameMs` per window and per session records the worst frame for now).
