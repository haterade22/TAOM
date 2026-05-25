# Codex Adversarial Review - CrashReport feature - 2026-05-25

Verdict: ISSUES FOUND

Summary: CRITICAL: 0 | HIGH: 2 | MEDIUM: 4 | LOW: 2

Scope: Phase 2 adversarial review of the CrashReport feature. I read the requested feature docs/RCA/memory entries, TAOM source, settings read sites, and decompiled the requested installed v1.4.5 DLL targets with `ilspycmd` from the installed game/dependency locations.

## VANILLA CODE

Source note: these excerpts are from installed DLLs under `E:/Steam/steamapps/common/Mount & Blade II Bannerlord/...` unless otherwise noted. They were not taken from `E:/Decompiled_Bannerlord`.

### TaleWorlds.DotNet.Managed.ApplicationTick

DLL: `bin/Win64_Shipping_Client/TaleWorlds.DotNet.dll`

```csharp
[LibraryCallback(null, false)]
internal static void ApplicationTick(float dt)
{
    ManagedObject.HandleManagedObjects();
    DotNetObject.HandleDotNetObjects();
    NativeObject.HandleNativeObjects();
    ManagedObjectOwner.GarbageCollect();
    NativeTelemetryManager.Update();
    for (int i = 0; i < _components.Count; i++)
    {
        _components[i].OnApplicationTick(dt);
    }
}
```

### TaleWorlds.Engine.ScriptComponentBehavior.OnTick

DLL: `bin/Win64_Shipping_Client/TaleWorlds.Engine.dll`

```csharp
protected internal virtual void OnTick(float dt)
{
    Debug.FailedAssert("This base function should never be called.",
        "C:\\Develop\\MB3\\Source\\Bannerlord\\TaleWorlds.Engine\\ScriptComponentBehavior.cs",
        "OnTick", 265);
}
```

### TaleWorlds.MountAndBlade.Module.OnApplicationTick

DLL: `bin/Win64_Shipping_Client/TaleWorlds.MountAndBlade.dll`

```csharp
internal void OnApplicationTick(float dt)
{
    bool isOnlyCoreContentEnabled = IsOnlyCoreContentEnabled;
    IsOnlyCoreContentEnabled = Utilities.IsOnlyCoreContentEnabled();
    if (isOnlyCoreContentEnabled != IsOnlyCoreContentEnabled && isOnlyCoreContentEnabled)
    {
        InformationManager.ShowInquiry(new InquiryData(...), pauseGameActiveState: false);
    }
    if (_synchronizationContext == null)
    {
        _synchronizationContext = new SingleThreadedSynchronizationContext();
        SynchronizationContext.SetSynchronizationContext(_synchronizationContext);
    }
    _testContext.OnApplicationTick(dt);
    if (!GameNetwork.MultiplayerDisabled) OnNetworkTick(dt);
    if (GameStateManager.Current == null) GameStateManager.Current = GlobalGameStateManager;
    if (GameStateManager.Current == GlobalGameStateManager)
    {
        ... GlobalGameStateManager.OnTick(dt);
    }
    Utilities.RunJobs();
    foreach (MBSubModuleBase item in CollectSubModules()) item.OnApplicationTick(dt);
    JobManager.OnTick(dt);
    AvatarServices.UpdateAvatarServices(dt);
}
```

Vanilla itself calls `InformationManager.ShowInquiry` from application tick, so a main-thread tick finalizer calling it is not automatically invalid.

### TaleWorlds.MountAndBlade.View.MissionViews.MissionView.OnMissionScreenTick

DLL: `Modules/Native/bin/Win64_Shipping_Client/TaleWorlds.MountAndBlade.View.dll`

```csharp
public virtual void OnMissionScreenTick(float dt)
{
}
```

### TaleWorlds.ScreenSystem.ScreenManager.Tick and private Update()

DLL: `bin/Win64_Shipping_Client/TaleWorlds.ScreenSystem.dll`

```csharp
public static void Tick(float dt)
{
    if (DisableScreenManagerTicks) return;
    for (...) _globalLayers[i]?.EarlyTick(dt);
    Update();
    if (TopScreen != null)
    {
        TopScreen.FrameTick(dt);
        FindPredecessor(TopScreen)?.IdleTick(dt);
    }
    for (...) if (screenLayer != null && screenLayer.IsActive && !screenLayer.IsFinalized) screenLayer.Tick(dt);
    for (...) _globalLayers[k]?.Tick(dt);
    LateUpdate(dt);
    for (...) _globalLayers[l]?.LateTick(dt);
    if (TopScreen != null) TopScreen.PostFrameTick(dt);
    ShowScreenDebugInformation();
}

public static void Update(IReadOnlyList<int> lastKeysPressed)
{
    ... TopScreen.Update(_lastPressedKeys);
    ... globalLayer.Update(_lastPressedKeys);
}

private static void Update()
{
    int num = 0;
    for (...) if (SortedLayers[i].IsActive) num++;
    if (_sortedActiveLayersCopyForUpdate.Length < num) _sortedActiveLayersCopyForUpdate = new ScreenLayer[num];
    int num2 = 0;
    for (...)
    {
        ScreenLayer screenLayer = SortedLayers[j];
        if (screenLayer.IsActive) _sortedActiveLayersCopyForUpdate[num2++] = screenLayer;
    }
    for (int num3 = num2 - 1; num3 >= 0; num3--)
    {
        ScreenLayer screenLayer2 = _sortedActiveLayersCopyForUpdate[num3];
        if (!screenLayer2.IsFinalized) screenLayer2.ProcessEvents();
    }
    for (int k = 0; k < _sortedActiveLayersCopyForUpdate.Length; k++) _sortedActiveLayersCopyForUpdate[k] = null;
}
```

The no-arg `Update` target exists and is private static.

### TaleWorlds.MountAndBlade.Mission.Tick

DLL: `bin/Win64_Shipping_Client/TaleWorlds.MountAndBlade.dll`

```csharp
internal void Tick(float dt)
{
    MBAPI.IMBMission.Tick(Pointer, dt);
}
```

Relevant call-chain evidence for mission thread safety:

```csharp
public void OnTick(float dt, float realDt, bool updateCamera, bool doAsyncAITick)
{
    ...
    tickCompleted = false;
    for (int num2 = MissionBehaviors.Count - 1; num2 >= 0; num2--)
        MissionBehaviors[num2].OnMissionTick(dt);
    ...
    if (doAsyncAITick) TickAgentsAndTeamsAsync(dt);
    else TickAgentsAndTeamsImp(dt, tickPaused: false);
}

private void TickAgentsAndTeamsImp(float dt, bool tickPaused)
{
    float num = tickPaused ? 0f : dt;
    TWParallel.For(0, AllAgents.Count, num, AgentTickMT);
    foreach (Agent allAgent in AllAgents) allAgent.Tick(num);
    foreach (Team team in Teams) team.Tick(dt);
    tickCompleted = true;
    foreach (MBSubModuleBase cachedSubModule in _cachedSubModuleList) cachedSubModule.AfterAsyncTickTick(dt);
}

private void AgentTickMT(int startInclusive, int endExclusive, float dt)
{
    for (int i = startInclusive; i < endExclusive; i++) AllAgents[i].TickParallel(dt);
}
```

### TaleWorlds.MountAndBlade.MissionBehavior.OnMissionTick

DLL: `bin/Win64_Shipping_Client/TaleWorlds.MountAndBlade.dll`

```csharp
public abstract MissionBehaviorType BehaviorType { get; }

public virtual void OnMissionTick(float dt)
{
}
```

### TaleWorlds.MountAndBlade.MBSubModuleBase.OnSubModuleLoad

DLL: `bin/Win64_Shipping_Client/TaleWorlds.MountAndBlade.dll`

```csharp
protected internal virtual void OnSubModuleLoad()
{
}
```

### TaleWorlds.Library.InformationManager.ShowInquiry

DLL: `bin/Win64_Shipping_Client/TaleWorlds.Library.dll`

```csharp
public static event Action<InquiryData, bool, bool> OnShowInquiry;

public static void ShowInquiry(InquiryData data, bool pauseGameActiveState = false, bool prioritize = false)
{
    InformationManager.OnShowInquiry?.Invoke(data, pauseGameActiveState, prioritize);
}
```

This is direct event invocation, not a queue. It is fine from the main UI/application tick context because vanilla does that, but not safe to assume from arbitrary AppDomain worker threads.

### ButterLib ExceptionHandlerSubSystem.Disable()

DLL: `Dependencies/_Module/bin/Win64_Shipping_Client/Bannerlord.ButterLib.dll`

```csharp
public bool IsEnabled { get; private set; }
public bool CanBeDisabled => true;
public bool CanBeSwitchedAtRuntime => true;

public void Enable()
{
    if (!_wasInitialized)
    {
        _wasInitialized = true;
        if (!(SettingsProvider.PopulateSubSystemSettings(this) ?? true)) return;
    }
    if (!IsEnabled)
    {
        IsEnabled = true;
        if (!BEWPatch.IsDebuggerAttached()) SubscribeToUnhandledException();
        else if (_disableWhenDebuggerIsAttached) return;
        if (!_wasButrLoaderInterceptorCalled)
        {
            BEWPatch.Enable(Harmony);
            DetachWatchdog?.Invoke();
        }
    }
}

public void Disable()
{
    if (IsEnabled)
    {
        IsEnabled = false;
        UnsubscribeToUnhandledException();
        if (ModuleInfoHelper.GetLoadedModules().Any(m =>
            string.Equals(m.Id, "BetterExceptionWindow", StringComparison.InvariantCultureIgnoreCase)))
        {
            BEWPatch.Disable(Harmony);
        }
    }
}
```

`Disable()` is idempotent, and ButterLib explicitly declares runtime switching support.

### Harmony support decompile

DLL: NuGet `Lib.Harmony` 2.4.2, `0Harmony.dll`

```csharp
public void PatchCategory(Assembly assembly, string category)
{
    Dictionary<string, List<Type>> value = AssemblyCachedCategories.GetValue(assembly, BuildCategoryCache);
    if (value.TryGetValue(category, out var value2))
    {
        value2.Do(delegate(Type type) { CreateClassProcessor(type).Patch(); });
    }
}
```

`PatchCategory` is synchronous. Class processors synchronously build patch jobs and call `PatchFunctions.UpdateWrapper`/detour before returning.

Private method resolution uses non-public flags:

```csharp
public static readonly BindingFlags allDeclared =
    BindingFlags.DeclaredOnly | BindingFlags.Instance | BindingFlags.Static |
    BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.GetField |
    BindingFlags.SetField | BindingFlags.GetProperty | BindingFlags.SetProperty;

public static MethodInfo DeclaredMethod(Type type, string name, Type[] parameters = null, Type[] generics = null)
{
    MethodInfo methodInfo = ((parameters != null)
        ? type.GetMethod(name, allDeclared, null, parameters, modifiers: null)
        : type.GetMethod(name, allDeclared));
    ...
}
```

Harmony finalizers wrap postfixes in the generated replacement. A throwing postfix is inside the finalizer try/catch path; postfix priority and finalizer priority are sorted within their own patch lists, not as one combined list.

### MCM support decompile

DLL: NuGet `Bannerlord.MCM` 5.11.4, `MCMv5.dll`

```csharp
public static T? Instance
{
    get
    {
        if (!GlobalSettings.Cache.ContainsKey(typeof(T)))
        {
            GlobalSettings.Cache.TryAdd(typeof(T), new T().Id);
        }
        return BaseSettingsProvider.Instance?.GetSettings(GlobalSettings.Cache[typeof(T)]) as T;
    }
}
```

Provider lookup is not just a static field read:

```csharp
public override BaseSettings? GetSettings(string id)
{
    foreach (ISettingsContainer settingsContainer in _settingsContainers)
    {
        BaseSettings settings = settingsContainer.GetSettings(id);
        if (settings != null) return settings;
    }
    foreach (IExternalSettingsProvider externalSettingsProvider in _externalSettingsProviders)
    {
        BaseSettings settings2 = externalSettingsProvider.GetSettings(id);
        if (settings2 != null) return settings2;
    }
    _logger.LogWarning("GetSettings " + id + " returned null");
    return null;
}
```

## Feature-Specific Deep Analysis

### 1. MBSubModuleBase.OnSubModuleLoad finalizer chicken-and-egg - CONFIRMED PARTIAL

TAOM creates Harmony at `Main/SubModule.cs:96` and calls `_harmony.PatchCategory("Patch37_CrashReport")` at `Main/SubModule.cs:108`. Harmony 2.4.2 `PatchCategory` is synchronous, so there is no deferred attach gap after the call starts and returns.

The real gap is earlier: `IoC.Configure()` at `Main/SubModule.cs:88`, `UIExtender.Create/Register/Enable` at `Main/SubModule.cs:90-92`, `IoC.Resolve<ITimeAccelerationService>()` at `Main/SubModule.cs:94`, `_harmony` construction at `Main/SubModule.cs:96`, and the `CrashReportSettings.Instance` read at `Main/SubModule.cs:104` all happen before Patch37 is attached. Any throw in those lines is not catchable by this feature.

Finding: MEDIUM-01.

### 2. ScreenManager.Update private static target - DISPUTED

Installed v1.4.5 has the private no-arg `ScreenManager.Update()` plus the public `Update(IReadOnlyList<int>)`. TAOM uses `[HarmonyPatch(typeof(ScreenManager), "Update", new Type[0])]` at `Main/Features/CrashReport/Hooks/Patch37_CrashReport.cs:80`. Harmony 2.4.2 resolves declared methods with `BindingFlags.NonPublic | BindingFlags.Static`, so the attribute can see this private overload. No issue.

### 3. ResolveService lazy cache race and early init - CONFIRMED PARTIAL

IoC is configured before Patch37 registration (`Main/SubModule.cs:88` before `Main/SubModule.cs:108`), so normal first finalizer resolution has an IoC container. The no-lock `_service` lazy cache at `Main/Features/CrashReport/Hooks/CrashReportPatchHelper.cs:13` and `:35-40` can race on first exception, but duplicate singleton resolution is not the important failure mode.

The important issues are:

- The early init window before Patch37 is attached, covered in MEDIUM-01.
- The static `_service` cache persists across `OnSubModuleUnloaded`, covered in HIGH-01.

### 4. CrashNotifier.ShowInquiry from a Harmony finalizer - DISPUTED FOR MAIN TICK, CONFIRMED RISK FOR APPDOMAIN

`InformationManager.ShowInquiry` directly invokes `OnShowInquiry`; it does not enqueue. However, installed vanilla `Module.OnApplicationTick` itself calls `InformationManager.ShowInquiry` from the application tick path, so a main-thread Harmony finalizer doing the same after a caught tick exception is not inherently invalid.

The unsafe case is `AppDomainExceptionHook.OnUnhandled` at `Main/Features/CrashReport/Hooks/AppDomainExceptionHook.cs:45`, which may run on a worker thread and still reaches `CrashNotifier.Notify` at `Main/Features/CrashReport/CrashReportService.cs:106` and `InformationManager.ShowInquiry` at `Main/Features/CrashReport/UI/CrashNotifier.cs:31`. See MEDIUM-03.

### 5. CrashBundleWriter cannot read live TAOM debug log - DISPUTED

`FileLogger` opens the log with `new StreamWriter(_logPath, true)` at `Main/Core/Logging/FileLogger.cs:24`. The .NET Framework `StreamWriter(string, bool)` path opens the write handle with `FileShare.Read`, which permits another reader. `CrashBundleWriter.TryCopyFile` opens the reader with `FileAccess.Read` and `FileShare.ReadWrite` at `Main/Features/CrashReport/Rendering/CrashBundleWriter.cs:79`; that is compatible with the writer's share mode.

One caveat: `CrashReportService.WriteToLog` enqueues report lines at `Main/Features/CrashReport/CrashReportService.cs:193-200`, then immediately writes the bundle at `:100-103`. Since `FileLogger` drains asynchronously, the copied `taom_debug.log` can miss the just-enqueued `[CrashReport]` lines, but `report.txt` and `report.json` are still written directly into the ZIP.

### 6. Native2ManagedPatcher reflective patches - DISPUTED

TAOM wraps each `harmony.Patch` call in try/catch at `Main/Features/CrashReport/Hooks/Native2ManagedPatcher.cs:78-86`. Harmony's finalizer signature supports a static method returning `Exception` with an `Exception __exception` parameter; TAOM's bridge at `Main/Features/CrashReport/Hooks/Native2ManagedPatcher.cs:121-122` matches that shape. Harmony updates shared patch state after wrapper construction, and I found no confirmed partial-attach leak pattern in the 2.4.2 decompile.

This remains a compatibility surface worth logging, but I do not have a confirmed bug here.

### 7. AppDomainExceptionHook on worker threads - CONFIRMED

`AppDomainExceptionHook.OnUnhandled` calls `_service.HandleException` on the throwing thread at `Main/Features/CrashReport/Hooks/AppDomainExceptionHook.cs:39-46`. `CrashReportService.ComposeContext` then runs the campaign and mission collectors at `Main/Features/CrashReport/CrashReportService.cs:136-137`.

Vanilla mission ticking uses `TWParallel.For` for agent tick parallelism, and some engine paths use explicit MT lock patterns. TAOM's mission collector reads live `Mission.Current`, teams, formations, and agent state without a main-thread gate. See MEDIUM-03.

### 8. Crash signature collision top-5 frames - NEEDS-DESIGN-INPUT, NOT A CURRENT DEFECT

`CrashSignatureCalculator` uses `ExceptionType | originatingPatchTarget | top 5 frame method names` at `Main/Features/CrashReport/Collectors/CrashSignatureCalculator.cs:14-25`. This can collide for shared top-frame patterns such as `Mission.CheckMissionEnded` null entries.

Current v1 behavior does not suppress duplicate reports; the signature is used for report identity/filename, so this collision does not currently mask later bundles. If v2 adds duplicate suppression or server-side bucketing by signature, include more context in the signature: innermost exception message hash, module owner for the first patched frame, and selected diagnostic payload such as the null mission logic type when available.

### 9. CrashReportSettings.Instance in per-tick paths - CONFIRMED

`CrashReportApplicationTickTrigger.Postfix` reads `CrashReportSettings.Instance` every `Module.OnApplicationTick` at `Main/Features/CrashReport/DevTriggers/CrashReportApplicationTickTrigger.cs:19`. `CrashReportDevTriggerMissionBehavior.OnMissionTick` reads it every mission tick at `Main/Features/CrashReport/DevTriggers/CrashReportDevTrigger.cs:17`.

MCM's `Instance` getter does a cache lookup plus `BaseSettingsProvider.Instance.GetSettings(id)`, and the default provider scans settings containers/providers. This is not a plain static field read. See LOW-01.

### 10. Module.OnApplicationTick postfix/finalizer ordering - DISPUTED

The dev trigger postfix and the finalizer both target `Module.OnApplicationTick`. Harmony emits finalizers around prefixes/original/postfixes, so the `CrashReportApplicationTickTrigger` postfix throw is caught by the same method's finalizer. Postfix priority `900` versus finalizer priority `800` does not make the postfix bypass the finalizer because Harmony sorts patch types separately and wraps the whole replacement in the finalizer exception path.

### 11. ButterLib TrySuspend one-shot - CONFIRMED

TAOM sets `_butterLibSuspended = true` only after a successful `TrySuspend()` at `Main/Features/CrashReport/CrashReportService.cs:86-88`. ButterLib's installed subsystem has `CanBeSwitchedAtRuntime => true`, and `Enable()` can re-subscribe the handler later. Since ButterLib `Disable()` is idempotent, TAOM can safely re-check/re-disable. See MEDIUM-04.

### 12. AppDomainExceptionHook.Unsubscribe race - DISPUTED

`SubModule.OnSubModuleUnloaded` unsubscribes before `IoC.Dispose()` at `Main/SubModule.cs:639-643`. There is no synchronization with an in-flight `OnUnhandled`, but the worst confirmed case is one more report using still-live services during the small interval before IoC disposal. I do not see a confirmed correctness bug here beyond the separate static `_service` cache issue in HIGH-01.

## CONFIG CROSS-REFERENCE

No XML/JSON config exists for this feature.

MCM property read-site cross-reference:

- `EnableCrashCapture`: defined at `Main/Features/CrashReport/CrashReportSettings.cs:22`; read only during startup at `Main/SubModule.cs:104`. No runtime read in `CrashReportPatchHelper`, `AppDomainExceptionHook`, or the dev mission behavior. Finding HIGH-02.
- `SuspendButterLibHandler`: defined at `Main/Features/CrashReport/CrashReportSettings.cs:27`; read at `Main/Features/CrashReport/CrashReportService.cs:86`.
- `EnableNativeToManagedCapture`: defined at `Main/Features/CrashReport/CrashReportSettings.cs:32`; read only during startup at `Main/SubModule.cs:110`. This is acceptable if documented as startup-only, but the current MCM text does not say that.
- `WriteCrashBundle`: defined at `Main/Features/CrashReport/CrashReportSettings.cs:39`; read at `Main/Features/CrashReport/CrashReportService.cs:100`.
- `ThrowOnNextMissionTick`: defined at `Main/Features/CrashReport/CrashReportSettings.cs:46`; read and reset at `Main/Features/CrashReport/DevTriggers/CrashReportDevTrigger.cs:20-22`.
- `ThrowOnNextApplicationTick`: defined at `Main/Features/CrashReport/CrashReportSettings.cs:51`; read and reset at `Main/Features/CrashReport/DevTriggers/CrashReportApplicationTickTrigger.cs:21-22`.

## FINDINGS

### HIGH

[HIGH] Main/Features/CrashReport/Hooks/CrashReportPatchHelper.cs:13 - Lifecycle - Static cached service survives module unload/reload - Add a reset path or remove the cache.

What: `CrashReportPatchHelper` stores `private static ICrashReportService? _service` and `ResolveService()` returns it forever once populated (`Main/Features/CrashReport/Hooks/CrashReportPatchHelper.cs:35-40`). `SubModule.OnSubModuleUnloaded` unsubscribes AppDomain, unpatches Harmony, and disposes IoC (`Main/SubModule.cs:639-643`), but it never clears this static cache.

Why: Bannerlord can unload/reload a module in the same process. After reload, Patch37 finalizers are attached again, but the helper still points at the old CrashReportService graph. That old graph includes the disposed `FileLogger`; `FileLogger.Dispose()` stops the writer and sets `_logFile = null` at `Main/Core/Logging/FileLogger.cs:57-70`. A later exception can be swallowed by the finalizer while logging goes to a stopped logger or stale collector graph.

Suggested fix: add `CrashReportPatchHelper.ResetForUnload()` that sets `_service = null` and call it in `SubModule.OnSubModuleUnloaded` before `IoC.Dispose()`. Simpler: remove the static service cache and resolve each exception; exception-path cost is irrelevant compared to report capture.

Status: CONFIRMED.

[HIGH] Main/Features/CrashReport/CrashReportSettings.cs:20 - User-facing settings contract - Enable Crash Capture does not make finalizers no-op or unsubscribe AppDomain at runtime - Gate runtime handlers or change the text to startup-only.

What: The MCM hint promises: "When off, all Harmony Finalizers no-op and AppDomain hook unsubscribes" (`Main/Features/CrashReport/CrashReportSettings.cs:20-22`). The property is only read once during startup at `Main/SubModule.cs:104`. `CrashReportPatchHelper.HandleAndSwallow` never checks it before swallowing (`Main/Features/CrashReport/Hooks/CrashReportPatchHelper.cs:19-31`), and `AppDomainExceptionHook.OnUnhandled` never checks it before handling (`Main/Features/CrashReport/Hooks/AppDomainExceptionHook.cs:39-46`).

Why: If the user disables crash capture after startup, finalizers continue to capture and swallow exceptions. If capture is disabled at startup, `SubModule` still always adds `CrashReportDevTriggerMissionBehavior` at `Main/SubModule.cs:591-594`; that behavior can throw at `Main/Features/CrashReport/DevTriggers/CrashReportDevTrigger.cs:20-23` without Patch37 being attached.

Suggested fix: either make the setting explicitly startup-only in the MCM hint/docs, or implement the promised runtime behavior. A minimal runtime fix is:

- In `CrashReportPatchHelper.HandleAndSwallow`, if `CrashReportSettings.Instance?.EnableCrashCapture == false`, return the original exception.
- In `AppDomainExceptionHook.OnUnhandled`, return immediately when disabled.
- Gate `CrashReportDevTriggerMissionBehavior` addition and its throw path on `EnableCrashCapture`.
- Treat `EnableNativeToManagedCapture` as restart-required unless a detach path is implemented.

Status: CONFIRMED.

### MEDIUM

[MEDIUM] Main/SubModule.cs:90 - Startup coverage - Patch37 is not attached until after UIExtender and service resolution can throw - Move the attach earlier or document the remaining blind spot.

What: `OnSubModuleLoad` runs `IoC.Configure()` at `Main/SubModule.cs:88`, UIExtender create/register/enable at `Main/SubModule.cs:90-92`, `IoC.Resolve<ITimeAccelerationService>()` at `Main/SubModule.cs:94`, and `_harmony = new Harmony(...)` at `Main/SubModule.cs:96`. Patch37 attaches only at `Main/SubModule.cs:108`.

Why: Harmony `PatchCategory` is synchronous, so after `Main/SubModule.cs:108` returns the finalizers are attached. But any exception before that line is uncovered. `MBSubModuleBase.OnSubModuleLoad` is a virtual no-op, so there is no vanilla wrapper catching this for TAOM. The current comment says Patch37 is registered "FIRST", but there are several throwable operations before it.

Suggested fix: reduce the gap by creating Harmony and attaching Patch37 immediately after `IoC.Configure()` and before UIExtender/time-service setup. If `IoC.Configure()` itself should be covered, split out a minimal CrashReport bootstrap that does not depend on the full IoC graph; otherwise document `IoC.Configure()` as the unavoidable blind spot.

Status: CONFIRMED.

[MEDIUM] Main/Features/CrashReport/CrashReportService.cs:135 - Diagnostic correctness - Per-frame Harmony correlation is always empty - Pass raw StackFrame objects to the collector.

What: `CrashReportService.ComposeContext` builds only `IReadOnlyList<StackFrameSnapshot>` at `Main/Features/CrashReport/CrashReportService.cs:124` and calls `_harmony.Collect(stack)` at `Main/Features/CrashReport/CrashReportService.cs:135`. `HarmonyCorrelationCollector` only calls `Harmony.GetPatchInfo(mb)` when the optional raw frame list is non-null (`Main/Features/CrashReport/Collectors/HarmonyCorrelationCollector.cs:35-40`). With the production call shape, `mb` is always null and each frame's `Patches` list is empty.

Why: The feature documentation/changelog promise Harmony patches affecting every stack frame. The current implementation only produces the global owner summary; the per-frame correlation payload is dead.

Suggested fix: build a `StackTrace(exception, true)` once in `ComposeContext`, pass both the raw `StackFrame[]` and the projected `StackFrameSnapshot` list into the Harmony collector, or change `StackFrameSnapshotBuilder` to return a small result object containing both. Add a unit test where a patched method appears in the raw frame list and the per-frame patch list is non-empty.

Status: CONFIRMED.

[MEDIUM] Main/Features/CrashReport/Hooks/AppDomainExceptionHook.cs:45 - Thread safety - AppDomain unhandled exceptions run the full TaleWorlds collectors and UI notifier on the throwing thread - Use a thread-safe capture mode or marshal UI to main thread.

What: `AppDomainExceptionHook.OnUnhandled` calls `_service.HandleException(ex, "AppDomain.UnhandledException")` directly on the AppDomain event thread (`Main/Features/CrashReport/Hooks/AppDomainExceptionHook.cs:39-46`). The service then runs `CampaignStateCollector` and `MissionStateCollector` (`Main/Features/CrashReport/CrashReportService.cs:136-137`) and finally calls `CrashNotifier.Notify` (`Main/Features/CrashReport/CrashReportService.cs:106`).

Why: Vanilla mission code uses `TWParallel.For` for agent ticking and explicit MT locking in some engine paths. TAOM's mission collector reads live `Mission.Current`, `m.Teams?.ToList()`, `t.ActiveAgents?.Count`, `pt.FormationsIncludingEmpty`, `f.CountOfUnits`, and agent health/position/wielded weapon at `Main/Features/CrashReport/Collectors/MissionStateCollector.cs:13-81`. `InformationManager.ShowInquiry` directly invokes subscribers, so from an AppDomain worker thread it can call UI handlers off the UI thread. The collectors catch exceptions, but they still perform non-thread-safe reads while the engine may be mid-mutation.

Suggested fix: record the main thread id during startup. If `HandleException` is invoked from a non-main thread, use a reduced capture mode limited to exception/stack/modules/assemblies/process/log tails, and skip campaign/mission/UI inquiry. If the process is still alive and a main-thread queue exists, marshal only the notifier to main thread.

Status: CONFIRMED.

[MEDIUM] Main/Features/CrashReport/CrashReportService.cs:86 - BUTR coexistence - One-shot ButterLib suspension is not sticky after runtime re-enable - Re-check or expose IsEnabled.

What: TAOM sets `_butterLibSuspended = true` after the first successful `TrySuspend()` (`Main/Features/CrashReport/CrashReportService.cs:86-88`). ButterLib's installed subsystem declares `CanBeSwitchedAtRuntime => true`, and `Enable()` can re-subscribe its unhandled-exception handler after TAOM's first crash.

Why: If a user or MCM reload re-enables ButterLib after TAOM's first successful suspension, TAOM will not call `Disable()` again on later crashes because `_butterLibSuspended` remains true. The coexistence guarantee silently degrades to both handlers firing.

Suggested fix: because ButterLib `Disable()` is idempotent, either call `TrySuspend()` on every crash when `SuspendButterLibHandler` is enabled, or extend `IButterLibExceptionHandlerAdapter` with `IsEnabled` and re-disable only when enabled.

Status: CONFIRMED.

### LOW

[LOW] Main/Features/CrashReport/DevTriggers/CrashReportApplicationTickTrigger.cs:19 - Per-frame cost - CrashReportSettings.Instance is a provider lookup on every app and mission tick - Cache or throttle dev-trigger polling.

What: The application tick trigger reads `CrashReportSettings.Instance` every `Module.OnApplicationTick` at `Main/Features/CrashReport/DevTriggers/CrashReportApplicationTickTrigger.cs:19`. The mission trigger does the same every `OnMissionTick` at `Main/Features/CrashReport/DevTriggers/CrashReportDevTrigger.cs:17`.

Why: MCM's `AttributeGlobalSettings<T>.Instance` goes through `BaseSettingsProvider.Instance.GetSettings(id)`, and the default provider scans settings containers/providers. This is small, but not zero, and it is paid continuously for QA-only toggles.

Suggested fix: cache the settings object after first successful resolution, or poll at a low frequency for QA triggers. Also skip both trigger checks when `EnableCrashCapture` is false.

Status: CONFIRMED.

[LOW] Main/Features/CrashReport/Rendering/CrashBundleWriter.cs:51 - Diagnostic UX - Bundle writer returns a path even after mid-write failure - Return null or mark partial output explicitly.

What: `CrashBundleWriter.Write` returns `zipPath` from the catch block when ZIP creation fails mid-way (`Main/Features/CrashReport/Rendering/CrashBundleWriter.cs:51-56`). `CrashReportService` passes that path to the player-facing notifier at `Main/Features/CrashReport/CrashReportService.cs:100-107`.

Why: The player can be told to upload/open a bundle that is known to have failed during write and may be corrupt. The comment says the broken ZIP is left for inspection, which is reasonable for diagnostics, but the returned path is indistinguishable from a successful bundle path.

Suggested fix: return `null` on mid-write failure and log the partial path separately, or return a result object with `Path`, `Succeeded`, and `FailureReason`. If the UI shows the path, label it as partial.

Status: CONFIRMED.

## OBSERVATIONS

[OBSERVATION] Main/Features/CrashReport/Hooks/Patch37_CrashReport.cs:11 - Documentation - The comment says "10 Harmony Finalizers", but the file defines 9 static finalizer patch classes. No behavior bug found, but update the count or list the dynamic native-to-managed branch separately.

[OBSERVATION] Main/Features/CrashReport/Collectors/CrashSignatureCalculator.cs:14 - Crash signatures are shallow by design in v1. Since reports are not suppressed by signature yet, the top-5 collision risk is not currently data loss. Revisit before adding dedup suppression.

[OBSERVATION] Main/Features/CrashReport/Rendering/CrashBundleWriter.cs:79 - Live `taom_debug.log` copying is compatible with `FileLogger`'s writer handle. The more realistic limitation is async logger lag: the ZIP can copy the log before newly enqueued crash-report lines are flushed.

## Recommended Fix Order

1. Reset or remove `CrashReportPatchHelper._service` before IoC disposal. This is the highest lifecycle risk and simplest fix.
2. Decide whether `EnableCrashCapture` is runtime or startup-only. Implement the runtime guard if the current MCM promise remains.
3. Move Patch37 attachment earlier after IoC configuration, or split out a minimal crash-report bootstrap if IoC failures must be captured.
4. Pass raw stack frames into `HarmonyCorrelationCollector`.
5. Add a non-main-thread capture mode for AppDomain unhandled exceptions.
6. Make ButterLib suspension repeatable/idempotent.
7. Cache or throttle MCM dev-trigger reads and make bundle write success explicit.

## Quality Gates

- Decompiled installed patch targets: yes.
- Read TAOM source and docs/RCA/memory entries: yes.
- Grep/read settings read sites before missing-claim findings: yes.
- Decompiled Harmony 2.4.2 for private-method resolution, synchronous `PatchCategory`, and finalizer/postfix wrapping: yes.
- Traced worker-thread risk from AppDomain hook through TAOM collectors and vanilla mission async ticking: yes.

CRITICAL: 0 | HIGH: 2 | MEDIUM: 4 | LOW: 2
VERDICT: ISSUES FOUND
