# Plan 006: Cut the crash-capture sweep from 247 patched callbacks to a six-entry allowlist, make both capture toggles live, and delete four finalizers that can never fire

> **Executor instructions**: Follow this plan step by step. Run every
> verification command and confirm the expected result before moving to the
> next step. If anything in the "STOP conditions" section occurs, stop and
> report; do not improvise. The orchestrator maintains `plans/README.md`
> for this run: do NOT edit it; report your status in your final message.
>
> **Where you work (read this twice).** All work happens in ONE worktree:
> `E:\repos\wt-006-crash-capture` (Git Bash form `E:/repos/wt-006-crash-capture`, called **W** below).
> The main checkout `E:\repos\TAOM` holds another session's uncommitted edits to `Main/SubModule.cs`,
> `Main/IoC.cs`, `CHANGELOG.md` and several docs; touching it can commit their work. Your shell's
> working directory resets to `E:\repos\TAOM` on EVERY call, so a bare relative path lands in the wrong tree.
> Therefore, without exception:
> - Every shell call starts with `cd E:/repos/wt-006-crash-capture && `.
> - Every git call is `git -C E:/repos/wt-006-crash-capture ...` (the only exceptions are the drift
>   check below and the Step 0 `worktree add`, which name `E:/repos/TAOM` on purpose and write nothing there).
> - Every Read, Edit or Write path is absolute and starts with `E:\repos\wt-006-crash-capture\`.
>   Every repo-relative path in this plan (for example `Main/SubModule.cs`) means
>   `E:\repos\wt-006-crash-capture\Main\SubModule.cs`. Never edit a path under `E:\repos\TAOM\`.
> - If the orchestrator gave you a different worktree cut from `b2e387db`, substitute its absolute
>   path for W everywhere.
>
> **Shell**: run every command in this plan in **Git Bash (the Bash tool)**, not PowerShell. `sed`,
> `grep -c` and the `\"`-escaped `git grep` patterns only work there. `pwsh tools/...` commands are
> launched from Git Bash too.
>
> **Drift check (run first)**:
> `git -C E:/repos/TAOM diff --stat b2e387db..bannerlord-1.5.x -- Main/SubModule.cs Main/Features/CrashReport TAOM.Tests/Features/CrashReport TAOM.Tests/Features/Mcm/SettingRequireRestartPostureTests.cs docs/features/crash-report.md docs/features/mcm.md docs/features/hero-race.md docs/reference/engine/gauntletui-viewmodel-screen.md docs/reference/harmony-patch-registry.md docs/reference/taleworlds-api-snapshot/patch-targets.md docs/reference/taleworlds-api-snapshot/reflection-sites.md`
> Expected: no output (verified empty for the committed history at `4b5662b2`, 2026-09-23). It
> compares commits only and writes nothing. You work on a branch cut from `b2e387db`, so the excerpts
> below are exact for your worktree. If the command prints any file, the trunk moved under this
> plan: finish on your branch anyway, but list those files in your final report so the merge can be
> planned. If an excerpt below does not match YOUR worktree, that is a STOP condition.

## Status

- **Priority**: P1
- **Effort**: M (many small edits, each mechanical; about 12 files)
- **Risk**: LOW to MED (behaviour of a diagnostic safety net changes; see "riskiest assumption" in Maintenance notes)
- **Depends on**: none. Overlaps `plans/007-patchshield-skip-callback-shims.md` and
  `plans/009-guarded-patch-category-apply.md` (see Maintenance notes); do not execute 009 in the same worktree.
- **Category**: perf
- **Planned at**: commit `b2e387db`, 2026-09-23
- **Issue**: create before implementation lands (orchestrator)

## Why this matters

Every TAOM boot spends 29 to 33 seconds inside one block of `SubModule.OnSubModuleLoad`, measured
on 30 of 30 desktop launches (the gap between the `[SaveDefiners]` log line and
`[CrashReport] Native2Managed: attached 247 Finalizer(s)`). That block Harmony-patches all 247
static methods of the engine's three `*CallbacksGenerated` classes with a crash-capture finalizer,
at about 120 to 190 ms per `harmony.Patch` on this machine; in 30 logged sessions it never
captured a single exception. Players cannot switch it off: the MCM toggle is read at a point where
MCM's settings object is always null, so the code always takes its `?? true` fallback, while the
MCM hint text and the feature doc both claim the finalizers "cost zero". Separately, four of the
nine named crash finalizers sit on empty base virtual methods and can never run for the overrides
that actually throw. After this plan: the sweep patches six chosen callbacks (expected boot cost
about 1 s instead of about 30 s), both toggles work live without a restart, the dead finalizers are
gone with a test that stops them coming back, a crash that recurs every frame stops writing one
log line per frame, and the docs say what the code does. The capture stays ON by default: that
posture is the maintainer's decision and this plan does not change it.

## Current state

All excerpts below were read at `b2e387db` with `git show b2e387db:<path>`. Your worktree is cut
from that commit and is clean, so the same text is in your files.

### Files and their roles

- `Main/SubModule.cs` (single-owner, 2,148 lines): TAOM's `MBSubModuleBase`. Lines 187-210 install
  crash capture in `OnSubModuleLoad`. This plan edits ONLY lines 194-210 (listed in Scope).
- `Main/Features/CrashReport/Hooks/Native2ManagedPatcher.cs` (123 lines): the 247-method sweep
  (`AttachAll`, lines 36-97) and the `Native2ManagedBridge` finalizer (lines 117-123).
- `Main/Features/CrashReport/Hooks/Patch37_CrashReport.cs` (120 lines): nine attribute-driven Harmony
  finalizers in category `Patch37_CrashReport`; four of them are dead.
- `Main/Features/CrashReport/Hooks/CrashReportPatchHelper.cs` (69 lines): shared finalizer sink
  `HandleAndSwallow`. Read only, not edited.
- `Main/Features/CrashReport/CrashReportSettings.cs` (52 lines): the MCM page, including the two toggles.
- `Main/Features/CrashReport/CrashBundleThrottle.cs` (84 lines): per-signature occurrence counter and
  bundle admission.
- `Main/Features/CrashReport/CrashReportService.cs`: `HandleException`, which writes the per-occurrence
  suppression log line (lines 94-115).
- `TAOM.Tests/Features/Mcm/SettingRequireRestartPostureTests.cs` (113 lines): allowlists the two
  toggles as restart-required (lines 37-42).
- `TAOM.Tests/Features/CrashReport/CrashBundleThrottleTests.cs` (156 lines): existing MSTest class for
  the throttle (namespace `TAOM.Tests.Features.CrashReport`, already `using TAOM.Features.CrashReport;`).
- `TAOM.Tests/Migration/GameAssemblies.cs`: `internal static class GameAssemblies` in namespace
  `TAOM.Tests.Migration`, with `public static bool EnsureLoaded()` and `public static List<string> Diagnostics`.
  It pre-loads the game's module assemblies (needed to resolve `TaleWorlds.MountAndBlade.View`).
- `TAOM.Tests/Migration/HarmonyPatchBindingTests.cs`: the structural pattern for resolving a patch
  class's target from its `[HarmonyPatch]` attributes (`MergeSpec`, lines 184-211; `DiscoverPatchTypes`, lines 80-96).
- Docs that state the wrong facts: `docs/features/crash-report.md` (lines 5, 32, 34, 54-65, 69, 82,
  104, 106, 190, 191, 207, 213, 224, 273, 277-278, 284-285 and the `## Changelog` section),
  `docs/features/mcm.md:94-100`, `docs/features/hero-race.md:242-243`,
  `docs/reference/engine/gauntletui-viewmodel-screen.md:111-112`,
  `docs/reference/harmony-patch-registry.md:268` and `:270`,
  `docs/reference/taleworlds-api-snapshot/patch-targets.md` (rows 101, 102, 104, 108 and the
  `Patches: 247.` header line 7), and `docs/reference/taleworlds-api-snapshot/reflection-sites.md:111`.

### Excerpt: `Main/SubModule.cs:187-210`

```csharp
        // Codex review #46 (2026-05-25) MED-01: attach Patch37_CrashReport IMMEDIATELY
        // after IoC.Configure() so its Finalizers cover the rest of OnSubModuleLoad
        // (UIExtender init, time-acceleration resolve, downstream PatchCategory calls).
        // Previous order left lines 88-107 uncatchable. The only unavoidable blind spot
        // is the IoC.Configure() call itself — if THAT throws, the entire feature is
        // unreachable. Split CrashReport bootstrap doesn't fix this without re-implementing
        // a manual DI container; accept and document the residual.
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

Line 194 is `_harmony = new Harmony("com.taom.mod");`, line 210 is the closing `}` of the outer `if`,
and line 211 is blank. The em dash at line 191 is existing code comment text that this plan keeps.

**Why the two `Instance` reads are dead (verified in the installed MCMv5.dll, decompiled with
`ilspycmd` from `Modules\TAOM.Dependencies\bin\Win64_Shipping_Client\MCMv5.dll`):**
`GlobalSettings<T>.Instance` returns `BaseSettingsProvider.Instance?.GetSettings(...) as T`;
`BaseSettingsProvider.Instance` is `public static BaseSettingsProvider? Instance { get; internal set; }`
and its only assignment is in `MCMSubModule.OnBeforeInitialModuleScreenSetAsRoot`
(`BaseSettingsProvider.Instance = GenericServiceProvider.GetService<BaseSettingsProvider>();`), which the
engine calls long after every module's `OnSubModuleLoad`. So in `OnSubModuleLoad`
`CrashReportSettings.Instance` is always null and both conditions are always true. Deleting them
holds runtime parity exactly.

The runtime paths already honour `EnableCrashCapture` when an exception arrives:
`CrashReportPatchHelper.cs:36-41` returns the exception (pass-through) when
`CrashReportSettings.Instance != null && !CrashReportSettings.Instance.EnableCrashCapture`, and
`AppDomainExceptionHook.cs:60` returns early on the same test. Nothing honours
`EnableNativeToManagedCapture` at runtime yet; this plan adds that.

### Excerpt: `Main/Features/CrashReport/Hooks/Native2ManagedPatcher.cs` (key lines)

```csharp
 1 using System;
 2 using System.Linq;
 3 using System.Reflection;
 4 using HarmonyLib;
 5 using TAOM.Core.Logging;
 6
 7 namespace TAOM.Features.CrashReport.Hooks;
 8
 9 // Reflection-based Finalizer attachment on every static method in any
10 // *CallbacksGenerated type inside TaleWorlds.{MountAndBlade,Engine,DotNet}.AutoGenerated.dll.
...
16 // Finalizers cost ~0 unless an exception is thrown — Harmony only invokes them on
17 // the exception path. The MCM "EnableNativeToManagedCatch" toggle lets us
18 // disable this branch if perf or compatibility ever becomes an issue.
19 public sealed class Native2ManagedPatcher
...
36     public int AttachAll(Harmony harmony)
37     {
38         if (_attached) return 0;
39         _attached = true;
40
41         var finalizerMethod = typeof(CrashReportPatchHelper).GetMethod(
...
52         var bridge = typeof(Native2ManagedBridge).GetMethod(
53             "Finalizer",
54             BindingFlags.NonPublic | BindingFlags.Static);
...
72                 var types = SafeGetTypes(asm).Where(t => t != null && t.Name.EndsWith("CallbacksGenerated", StringComparison.Ordinal));
...
80                             harmony.Patch(m, finalizer: new HarmonyMethod(bridge));
...
95         _logger.LogInfo($"[CrashReport] Native2Managed: attached {patched} Finalizer(s)");
96         return patched;
97     }
98
99     private static Assembly? TryFindAssembly(string simpleName)
100    {
101        try
102        {
103            return AppDomain.CurrentDomain.GetAssemblies()
104                .FirstOrDefault(a => string.Equals(a.GetName().Name, simpleName, StringComparison.Ordinal));
105        }
106        catch { return null; }
107    }
108
109    private static Type[] SafeGetTypes(Assembly asm)
...
114    }
115 }
116
117 // Bridge methods so Harmony Finalizers have a `static Exception? Finalizer(Exception __exception)`
118 // signature without needing a per-target stringly-typed origin.
119 internal static class Native2ManagedBridge
120 {
121     internal static Exception? Finalizer(Exception __exception)
122         => CrashReportPatchHelper.HandleAndSwallow(__exception, "TaleWorlds.AutoGenerated.<callback>");
123 }
```

The 247 count is the number the live log printed. The sweep patches every static method of
`CoreCallbacksGenerated`, `EngineCallbacksGenerated` and `LibraryCallbacksGenerated`, including each
class's `Initialize()` and its `Delegates` property accessors.

### Excerpt: `Main/Features/CrashReport/Hooks/Patch37_CrashReport.cs`

Header, lines 11-27:

```csharp
// Patch37_CrashReport category — 9 Harmony Finalizers on TaleWorlds lifecycle methods,
// PLUS one dev-trigger Postfix (CrashReportApplicationTickTrigger in DevTriggers/),
// PLUS reflection-attached Finalizers on every *CallbacksGenerated method via
// Native2ManagedPatcher (run-time, hundreds of methods). All share this category so
// `_harmony.UnpatchCategory("Patch37_CrashReport")` would detach the lot in one call.
//
// A Finalizer that returns null swallows the exception (game continues); returning
// the exception lets it bubble. We always swallow (caller decision in helper).
//
// Priority 800 matches BetterExceptionWindow's published value — keeps us at the
// same priority tier so when both are installed, the "first runs last" Finalizer
// ordering produces deterministic behaviour. The service's TrySuspend on BUTR's
// handler should make co-existence rare in practice.
//
// MUST register FIRST in SubModule.OnSubModuleLoad to maximise coverage of
// other mods' OnSubModuleLoad throws. See docs/features/crash-report.md for the
// chicken-and-egg caveat.
```

The four dead classes (each is an 8-line block: two attributes, the class line, the opening brace,
`[HarmonyPriority(800)]`, the two-line expression-bodied `Finalizer`, the closing brace):

```csharp
45 [HarmonyPatch(typeof(ScriptComponentBehavior), "OnTick")]
46 [HarmonyPatchCategory("Patch37_CrashReport")]
47 public static class ScriptComponentBehaviorOnTickFinalizer
...
63 [HarmonyPatch(typeof(MissionView), "OnMissionScreenTick")]
64 [HarmonyPatchCategory("Patch37_CrashReport")]
65 public static class MissionViewOnMissionScreenTickFinalizer
...
104 [HarmonyPatch(typeof(MissionBehavior), "OnMissionTick")]
105 [HarmonyPatchCategory("Patch37_CrashReport")]
106 public static class MissionBehaviorOnMissionTickFinalizer
...
113 [HarmonyPatch(typeof(MBSubModuleBase), "OnSubModuleLoad")]
114 [HarmonyPatchCategory("Patch37_CrashReport")]
115 public static class MBSubModuleBaseOnSubModuleLoadFinalizer
```

So the blocks are lines 45-52, 63-70, 104-111 and 113-120; each except the last is followed by one
blank line. The five that stay: `ManagedApplicationTickFinalizer` (`Managed.ApplicationTick`, lines 36-43),
`ModuleOnApplicationTickFinalizer` (54-61), `ScreenManagerTickFinalizer` (72-79),
`ScreenManagerUpdateFinalizer` (86-93, `new Type[0]` picks the private no-arg overload),
`MissionTickFinalizer` (95-102). The class `Patch37_CrashReport` holds the `Category` const at line 33.
The file's `using` lines include `TaleWorlds.Engine` and `TaleWorlds.MountAndBlade.View.MissionViews`,
which become unused after the deletion; leave them (the build does not warn about unused usings).

### Engine facts (v1.5.3 decompile via `pwsh tools/taom-src.ps1 path <Type>`, cache `C:\Users\mikew\.taom-src\v1.5.3\`)

Why the four finalizers are dead: Harmony rewrites the body of the exact method it is given; an
override is a different method, so a finalizer on a base virtual only runs when an override calls
`base.X()` and the base throws. The four bases:

```csharp
// TaleWorlds.MountAndBlade.MissionBehavior.cs:150-152
	public virtual void OnMissionTick(float dt)
	{
	}
// TaleWorlds.MountAndBlade.MBSubModuleBase.cs:8-10
	protected internal virtual void OnSubModuleLoad()
	{
	}
// TaleWorlds.MountAndBlade.View.MissionViews.MissionView.cs:21-23
	public virtual void OnMissionScreenTick(float dt)
	{
	}
// TaleWorlds.Engine.ScriptComponentBehavior.cs:215-218
	protected internal virtual void OnTick(float dt)
	{
		Debug.FailedAssert("This base function should never be called.", ...);
	}
```

The five remaining targets are all non-virtual:
`TaleWorlds.DotNet.Managed.cs:290 internal static void ApplicationTick(float dt)`,
`TaleWorlds.MountAndBlade.Module.cs:478 internal void OnApplicationTick(float dt)`,
`TaleWorlds.MountAndBlade.Mission.cs:1839 internal void Tick(float dt)`,
`TaleWorlds.ScreenSystem.ScreenManager.cs:288 public static void Tick(float dt)`,
`TaleWorlds.ScreenSystem.ScreenManager.cs:699 private static void Update()`.
The dev-trigger target `Module.OnApplicationTick` is the same non-virtual method.

The allowlist (six shims), each verified in the decompile, with what it calls:

| Assembly | Type | Method (decompile line) | Calls | Why it is on the list |
|---|---|---|---|---|
| `TaleWorlds.Engine.AutoGenerated` | `ManagedCallbacks.EngineCallbacksGenerated` | `EngineScreenManager_PreTick(float dt)` (:406) | `EngineScreenManager.PreTick` → `ScreenManager.EarlyUpdate(...)` (`TaleWorlds.Engine.EngineScreenManager.cs:15-18`) | Screen early update; no Patch37 finalizer wraps it |
| same | same | `EngineScreenManager_LateTick(float dt)` (:382) | `ScreenManager.LateTick` (`ScreenManager.cs:328`), which runs every layer's `RenderTick` | UI render tick; no Patch37 finalizer wraps it |
| same | same | `EngineScreenManager_Update()` (:418) | `ScreenManager.Update(_lastPressedKeys)`, the PUBLIC overload (`ScreenManager.cs:565`) | Layer input handling; Patch37 wraps only the private no-arg `Update()` |
| same | same | `ManagedScriptHolder_TickComponents(int thisPointer, float dt)` (:600) | `ManagedScriptHolder.TickComponents` (`TaleWorlds.Engine.ManagedScriptHolder.cs:241`), which calls `scriptComponent.OnTick(dt)` at :252 | The real dispatcher of every `ScriptComponentBehavior.OnTick` override: it replaces the dead Patch37 `ScriptComponentBehavior.OnTick` finalizer |
| same | same | `ThumbnailCreatorView_OnThumbnailRenderComplete(IntPtr renderId, NativeObjectPointer renderTarget)` (:850) | `ThumbnailCreatorView.OnThumbnailRenderComplete` → `renderCallback(renderId, renderTarget)` (`TaleWorlds.Engine.ThumbnailCreatorView.cs:19-22`) | Portrait and item thumbnail completion; the 2D custom-race render path `docs/features/hero-race.md` covers |
| `TaleWorlds.MountAndBlade.AutoGenerated` | `ManagedCallbacks.CoreCallbacksGenerated` | `BannerlordTableauManager_RequestCharacterTableauSetup(int characterCodeId, NativeObjectPointer scene, NativeObjectPointer poseEntity)` (:534) | `BannerlordTableauManager.RequestCharacterTableauSetup` → `RequestCallback(...)` (`TaleWorlds.MountAndBlade.BannerlordTableauManager.cs:44-47`) | Character tableau setup, the 3D custom-race render path `hero-race.md` covers |

All six are `internal static` in `internal static class` types, so resolve them with
`BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public`.

Deliberately NOT on the list, with the reason:
- `EngineScreenManager_Tick` and `Managed_ApplicationTick`: their managed targets
  (`ScreenManager.Tick`, `Managed.ApplicationTick`) already carry a Patch37 finalizer.
- `BannerlordTableauManager_RegisterCharacterTableauScene`: its body is one array store
  (`TableauCharacterScenes[type] = scene;`, `BannerlordTableauManager.cs:50-53`).
- Mission and agent combat callbacks (`Mission_MeleeHitCallback`, `Mission_OnAgentRemoved`,
  `Agent_UpdateAgentStats` and the rest): not relied on by any doc; adding them is an open question
  for the maintainer (see Maintenance notes), priced at about 120 to 190 ms of boot each.

What the two docs that "rely on the sweep" actually need (verified chain, v1.5.3):
`docs/reference/engine/gauntletui-viewmodel-screen.md:111-112` is about a throw in
`MapConversationTableau.OnTick`. That call runs as
`ScreenManager.Tick` (`ScreenManager.cs:309`, `screenLayer.Tick(dt)`) → `GauntletLayer.Tick`
(`TaleWorlds.Engine.GauntletUI.GauntletLayer.cs:173-176`, `UIContext.Update(dt)`) →
`UIContext.Update` (`TaleWorlds.GauntletUI.UIContext.cs:351`, `EventManager.Update(dt)`) →
`EventManager.Update` (`TaleWorlds.GauntletUI.EventManager.cs:967`, `activeList2[j].Update(dt)`) →
`Widget.Update` (`TaleWorlds.GauntletUI.BaseTypes.Widget.cs:1811-1813`, `OnUpdate(dt)`) →
`TextureWidget.OnUpdate` (`TaleWorlds.GauntletUI.BaseTypes.TextureWidget.cs:169`, `TextureProvider?.Tick(dt)`) →
`SandBox.GauntletUI.MapConversationTextureProvider.Tick` (`_mapConversationTableau.OnTick(dt);`).
So the Patch37 finalizer on `ScreenManager.Tick` is what catches it, today and after this plan.
`docs/features/hero-race.md:242-243` makes the same claim about the same map-conversation path.
Both docs get a one-sentence correction (Step 10); neither loses coverage.

### Harmony and rule facts

- `.claude/rules/harmony-patches.md:50`: a Finalizer "runs after the original on every call, with a null
  `__exception` when nothing threw. `return null` swallows. Returning the exception makes Harmony
  `throw` it ..., which erases the throw site, so hand it back as
  `return RethrowStackPreserver.PreserveForRethrow(__exception, null);`." So "finalizers cost zero" is false:
  the null path is cheap, not free.
- `RethrowStackPreserver` lives in `Dependencies/Foundation/RethrowStackPreserver.cs`, namespace
  `TAOM.Dependencies.Foundation`, signature at line 63:
  `public static Exception? PreserveForRethrow(Exception? exception, MethodBase? rethrowSite)`. It returns
  the same instance and fails open. `Main/TAOM.csproj` already references the Dependencies project
  (`Main/SubModule.cs:126` uses `TAOM.Dependencies.Foundation.PatchShield`), and `TAOM.Dependencies.dll`
  is in the test bin.
- Harmony 2.4.2 (`Lib.Harmony` NuGet): `HarmonyLib.HarmonyPatchCategory` derives from `HarmonyAttribute`
  and has `.ctor(string)`; `HarmonyMethod` has a public field `category` (string). So a test can read a
  class's category with `((HarmonyAttribute)attr).info.category`. Verified by reflection on
  `%USERPROFILE%\.nuget\packages\lib.harmony\2.4.2\lib\net452\0Harmony.dll`.
- TAOM.Tests can load the AutoGenerated assemblies by name: `TAOM.Tests/TAOM.Tests.csproj:35-38` references
  every `TaleWorlds.*.dll` in the game's `bin` with `Private=True`, and `TaleWorlds.Engine.AutoGenerated.dll`,
  `TaleWorlds.MountAndBlade.AutoGenerated.dll` and `TaleWorlds.DotNet.AutoGenerated.dll` land in
  `TAOM.Tests\bin\Debug\net472\`. `TaleWorlds.MountAndBlade.View.dll` is NOT there; `GameAssemblies.EnsureLoaded()`
  loads it from `Modules\Native\bin`.
- `InternalsVisibleTo("TAOM.Tests")` is set in `Main/TAOM.csproj:117-121`, so tests can call `internal` members.

### Excerpt: `Main/Features/CrashReport/CrashReportSettings.cs:17-32`

```csharp
    // --- Master ---

    [SettingPropertyGroup("Master")]
    [SettingPropertyBool("Enable Crash Capture", Order = 0,
        HintText = "Master toggle. When off, all Harmony Finalizers no-op and AppDomain hook unsubscribes, immediately. Turning it back ON needs a restart: the patches are installed once at launch and only when this is on.")]
    public bool EnableCrashCapture { get; set; } = true;

    [SettingPropertyGroup("Master")]
    [SettingPropertyBool("Suspend BUTR Exception Handler", Order = 1, RequireRestart = false,
        HintText = "On first capture, calls ButterLib.ExceptionHandlerSubSystem.Disable() so TAOM's report wins. Default ON.")]
    public bool SuspendButterLibHandler { get; set; } = true;

    [SettingPropertyGroup("Master")]
    [SettingPropertyBool("Enable Native-to-Managed Capture", Order = 2,
        HintText = "Patches every static method in TaleWorlds.*.AutoGenerated.dll *CallbacksGenerated types — catches exceptions from native callback shims. Finalizers cost zero unless an exception throws. Default ON. Installed once at launch, so a change takes effect on the next start.")]
    public bool EnableNativeToManagedCapture { get; set; } = true;
```

The file already has `RequireRestart = false` at four other attributes (lines 25, 37, 44, 49).
MCM hints are plain strings in this repo (no `{=KEY}` localization; `TaomSettings.cs` has 243 plain
`HintText = "` and 0 localized), so keep the new hints plain.

### Excerpt: `TAOM.Tests/Features/Mcm/SettingRequireRestartPostureTests.cs`

Lines 23-29 (doc comment) and 37-42 (allowlist):

```csharp
/// Every TAOM setting is read live through <c>TaomSettings.Instance</c> (no Harmony category is
/// gated on a setting at apply time), so the honest posture is <c>RequireRestart = false</c>
/// everywhere, and a new setting that omits the flag is a bug this test catches. The allowlist
/// holds two kinds of exception: a setting whose consumer is parked (commented out in SubModule.cs),
/// where a restart does not help either but flipping the flag would promise an effect that does not
/// exist; and the two CrashReport gates that SubModule.OnSubModuleLoad reads once to decide whether
/// to install the patches at all, where a restart genuinely is the only way to turn them on.
...
    private static readonly IReadOnlyDictionary<string, string> RestartAllowlist = new Dictionary<string, string>
    {
        [$"{nameof(TaomSettings)}.{nameof(TaomSettings.EnableNativeSkinFixes)}"] = "PARKED 2026-07-08: the install call is commented out in SubModule.cs, the toggle drives nothing",
        [$"{nameof(CrashReportSettings)}.{nameof(CrashReportSettings.EnableCrashCapture)}"] = "gates PatchCategory(Patch37_CrashReport) in OnSubModuleLoad: off is live, on needs a launch",
        [$"{nameof(CrashReportSettings)}.{nameof(CrashReportSettings.EnableNativeToManagedCapture)}"] = "gates Native2ManagedPatcher.AttachAll in OnSubModuleLoad: installed once at launch",
    };
```

Its second test, `RestartAllowlist_NamesOnlyRealSettings_ThatStillRequireRestart`, fails if an allowlisted
setting already has `RequireRestart = false`; its first, `EveryValueSetting_IsReadLive_SoRequireRestartIsFalse`,
fails if a setting outside the allowlist lacks it, naming offenders as `CrashReportSettings.EnableCrashCapture`.
After this plan both CrashReport rows go. `using TAOM.Features.CrashReport;` (line 9) stays:
`CrashReportSettings` is still referenced in `SettingsClasses` (line 48).

### Excerpt: `Main/Features/CrashReport/CrashReportService.cs:94-115`

```csharp
            // Dedup chokepoint. The capture sources (9 per-tick Harmony Finalizers, the
            // AppDomain hook, the battle-load watchdog, native callback shims) all funnel
            // here, so a crash that recurs every frame would otherwise write a fresh bundle
            // each tick — re-zipping an ever-growing taom_debug.log. Compute the cheap
            // signature (reads the frozen stack only; no engine collectors) and let the
            // throttle decide. On suppression: one log line, no ComposeContext / bundle /
            // notify — this kills both the disk spam and the growing-log feedback loop.
            var earlyStack = StackFrameSnapshotBuilder.FromException(exception);
            ...
            var admission = _throttle.Admit(earlySignature);
            if (admission.Decision != CrashBundleDecision.WriteBundle)
            {
                _logger.LogError(
                    $"[CrashReport] {admission.Decision} {CrashSignatureCalculator.Short(earlySignature)} " +
                    $"({exception?.GetType().Name ?? "(unknown)"} @ {originatingPatchTarget}) " +
                    $"occurrence #{admission.Occurrence} — bundle suppressed");
                return null;
            }
```

`CrashBundleThrottle.Admit` (`CrashBundleThrottle.cs:60-83`) increments the per-signature count at
lines 65-66 before any decision, so `Occurrence` is 1 for the first sighting of a signature and 2 for
the first repeat of a bundled one (`SuppressDuplicate` starts at 2; `SuppressCap` and
`SuppressCooldown` can be 1).

### Conventions that bind this change

- **TDD (AGENTS.md):** RED, GREEN, REFACTOR, test first. Every C# behaviour change below has its failing
  test one step earlier. Step 7 (the `SubModule.cs` gate removal) is a parity-holding deletion, proven
  by the Step 6 posture test plus the build.
- **ADR-002 (thin entry points):** entry points stay under 150 lines and delegate. `SubModule.cs` already
  violates it; this plan only removes lines from it, never adds logic.
- **ADR-003 / ADR-004 / ADR-005:** no `#region`, no `[Obsolete]`, no `#if DEBUG`.
- **ADR-007 (adapters):** services never take sealed TaleWorlds types. The new `Native2ManagedTargets`
  holds strings and uses `System.Reflection` only; it touches no TaleWorlds type, so no adapter is needed.
- **ADR-008 (testability):** logic that can be tested without the engine is (the allowlist resolution,
  the bridge decision, the log cadence); the live Harmony attach and in-game timing are named in a
  `Not-tested:` trailer instead.
- **`.claude/rules/harmony-patches.md`:** a finalizer that hands an exception back must use
  `RethrowStackPreserver.PreserveForRethrow`; read `docs/reviews/lessons/harmony-il.md` (in W) before
  editing a patch file (read it, do not edit it).
- **`csharp-architecture` rule "Cache `IoC.Resolve` lazily on a hot path":** not touched; the finalizers
  read `CrashReportSettings.Instance` only when an exception is non-null, never per call.
- **Human prose:** no em or en dash in any line you add (code spans exempt, but avoid them anyway);
  the dash check in Done criteria covers every added line.
- **Edit tool, not `sed -i`:** several files are CRLF; `sed -i` corrupts them. Use your Edit tool with
  absolute paths under W.

### Test baseline at `b2e387db` (so you do not chase known failures)

The orchestrator's run at `b2e387db`: 10,239 tests, 10,235 passed, 2 failed, 2 not executed. The 2
failures are `TheElkItem_DeclaresTheScaleTheReachIsTunedFor` (Elk tests) and
`AnimaliaActionSets_BindOnlyHorseActions_ToClipsThatExist` (`AnimaliaMountWiringTests`). Both read the
live, unversioned Armory install that another session is editing right now; neither is caused by or
related to this plan, so they may pass, fail, or change while you work. Do not investigate or fix
them. The 2 not executed are deliberate `[Ignore]`s (`WargAttack_FastWarg_InvokesRunningAttack`,
`WargAttack_SlowWarg_InvokesStandingAttack`). The rule for every later full run: the failures must be
a subset of those 2 Armory tests plus whatever Step 0 recorded.

## Commands you will need

Every command runs in Git Bash and starts with `cd E:/repos/wt-006-crash-capture && ` (shown in full
here; the steps refer to these rows by name). Never run `./build.ps1`, never launch the game, never
write under `E:\Steam\` or `E:\repos\TAOM\`.

| Purpose | Command | Expected on success |
|---|---|---|
| Build | `cd E:/repos/wt-006-crash-capture && dotnet build Main/TAOM.csproj -p:DisableModuleCopy=true -p:ModuleId=` | exit 0, `0 Error(s)` |
| Test (full) | `cd E:/repos/wt-006-crash-capture && dotnet test TAOM.Tests -p:DisableModuleCopy=true -p:ModuleId=` | failures a subset of the baseline rule above |
| Test (filtered) | `cd E:/repos/wt-006-crash-capture && dotnet test TAOM.Tests -p:DisableModuleCopy=true -p:ModuleId= --filter "FullyQualifiedName~<ClassName>"` | `Failed: 0`, `Skipped: 0` |
| Data | `cd E:/repos/wt-006-crash-capture && python tools/validate_moduledata.py` | 0 ERRORs (no data changes in this plan; run once at the end) |
| Docs (report) | `cd E:/repos/wt-006-crash-capture && python tools/lint_docs.py` | exit 0 (always 0; it is a report) |
| Docs (dead-link gate) | `cd E:/repos/wt-006-crash-capture && python tools/lint_docs.py --quick --fail-on-dead` | exit 0 and `Dead links: **0**` |
| API snapshot check | `cd E:/repos/wt-006-crash-capture && pwsh tools/snapshot_api_surface.ps1 -Check` | exit 0 (needs a built `TAOM.Tests\bin\Debug\net472\TAOM.dll`) |
| Tree guard | `git -C E:/repos/wt-006-crash-capture rev-parse --abbrev-ref HEAD` | `plan-006-crash-capture-boot-cost` |

## Scope

Every path below is relative to W (`E:\repos\wt-006-crash-capture\`).

**In scope** (the only files you may modify or create):

- `Main/SubModule.cs`: lines 194-210 only (the exact replacement is in Step 7). Single-owner file: this
  edit is explicitly authorised for this branch; touch no other line.
- `Main/Features/CrashReport/Hooks/Native2ManagedPatcher.cs`
- `Main/Features/CrashReport/Hooks/Native2ManagedTargets.cs` (new)
- `Main/Features/CrashReport/Hooks/Patch37_CrashReport.cs`
- `Main/Features/CrashReport/CrashReportSettings.cs`
- `Main/Features/CrashReport/CrashBundleThrottle.cs`
- `Main/Features/CrashReport/CrashReportService.cs` (the comment at 94-100 and the branch at 108-115 only)
- `TAOM.Tests/Features/CrashReport/Patch37TargetShapeTests.cs` (new)
- `TAOM.Tests/Features/CrashReport/Native2ManagedTargetsTests.cs` (new)
- `TAOM.Tests/Features/CrashReport/Native2ManagedBridgeTests.cs` (new)
- `TAOM.Tests/Features/CrashReport/CrashBundleThrottleTests.cs`
- `TAOM.Tests/Features/Mcm/SettingRequireRestartPostureTests.cs`
- `docs/features/crash-report.md`, `docs/features/mcm.md`, `docs/features/hero-race.md` (lines 242-243 only),
  `docs/reference/engine/gauntletui-viewmodel-screen.md` (lines 111-112 only),
  `docs/reference/harmony-patch-registry.md` (lines 268 and 270 only),
  `docs/reference/taleworlds-api-snapshot/patch-targets.md`,
  `docs/reference/taleworlds-api-snapshot/reflection-sites.md` (line 111 only)

**Out of scope** (do NOT touch, even though they look related):

- Anything under `E:\repos\TAOM\` (the main checkout). All your edits are under W.
- `Main/IoC.cs` and `Main/Features/CrashReport/CrashReportIoC.cs`: no registration changes are needed
  (`Native2ManagedTargets` is a static class; `Native2ManagedPatcher`'s constructor is unchanged). If you
  think one is needed, STOP and report the exact line.
- `Main/TAOM.csproj`, `Directory.Build.props`, `TAOM.Tests/TAOM.Tests.csproj`.
- `CHANGELOG.md`, `docs/ai-includes/orientation.md`, `docs/reviews/lessons/*.md`, `plans/README.md`:
  another session is editing them in the main checkout, and a merge would conflict. Put the text you
  would have added in your final report instead (templates in Step 11).
- `Main/Features/CrashReport/CrashReportSettings.cs` defaults: `EnableCrashCapture` and
  `EnableNativeToManagedCapture` stay `= true`. Do not rename either setting.
- `Main/Features/CrashReport/Hooks/CrashReportPatchHelper.cs`, `AppDomainExceptionHook.cs`: read only.
- `Dependencies/**` (PatchShield belongs to plan 007).
- `docs/reference/engine/submodule-lifecycle-and-harmony.md` and any other doc not listed above.

## Git workflow

- **Worktree and branch** (Step 0):
  `git -C E:/repos/TAOM worktree add E:/repos/wt-006-crash-capture -b plan-006-crash-capture-boot-cost b2e387db`.
  This creates W and the branch; it does not modify the main checkout's files. If the orchestrator already
  gave you a worktree cut from `b2e387db`, use it instead (and substitute its path for W).
- **Before every commit**, run both and confirm the exact results:
  1. `git -C E:/repos/wt-006-crash-capture rev-parse --abbrev-ref HEAD` prints `plan-006-crash-capture-boot-cost`.
  2. `git -C E:/repos/wt-006-crash-capture diff --cached --name-only` lists exactly the files that step names, nothing else.
- **Stage and commit** with `git -C E:/repos/wt-006-crash-capture add <path> <path>` then
  `git -C E:/repos/wt-006-crash-capture commit -m "<subject>" -m "<body>" -m "<trailers>"`. Explicit
  paths only; never `git add -A`, `git add .` or `git commit -a`.
- **Commit subject:** `<type>(crash-report): v<version> - <description>`, at most 72 characters, where
  `<version>` is the `<Version value="...">` in `Main/_Module/SubModule.xml` (it reads `v2.0.30` at
  `b2e387db`; re-read it). Body wrapped at 72, prose without em or en dashes.
- **No AI attribution trailer** (no `Co-Authored-By`). Optional trailers: `Not-tested:`, `Research:`.
- **Never push**, never open a PR, never merge.
- Check each subject length: `git -C E:/repos/wt-006-crash-capture log -1 --format=%s | python -X utf8 -c "import sys; s=sys.stdin.read().strip(); print(len(s), s)"` prints a number at most 72.

## Steps

### Step 0: Create the worktree and record the baseline

1. Create the worktree (command in Git workflow). Run the Tree guard: it prints `plan-006-crash-capture-boot-cost`.
2. Confirm the excerpts:
   `git -C E:/repos/wt-006-crash-capture show HEAD:Main/SubModule.cs | sed -n 187,210p` matches the
   SubModule excerpt above line for line, and `git -C E:/repos/wt-006-crash-capture log -1 --format=%h` prints `b2e387db`.
3. Run Test (full). Write down every failing test name; this is the Step 0 list.
   - If a failure is in a class under `TAOM.Tests/Features/CrashReport/`, in
     `SettingRequireRestartPostureTests`, or in `HarmonyPatchBindingTests`: STOP (the ground this plan
     builds on is already broken).
   - Any other failure beyond the two Armory tests: record it, do not fix it, and continue.
4. Run the API snapshot check and note the exit code. Exit 0 means Step 10 may regenerate the snapshot;
   non-zero means the snapshot already drifted for reasons outside this plan, so Step 10 hand-edits
   `patch-targets.md` instead and skips regeneration.

**Verify**: the Tree guard prints the branch name; the full run completed and you have the Step 0 list.

### Step 1 (RED): Pin that no Patch37 target is an overridable virtual

Create `E:\repos\wt-006-crash-capture\TAOM.Tests\Features\CrashReport\Patch37TargetShapeTests.cs`, namespace
`TAOM.Tests.Features.CrashReport`, `[TestClass]` class `Patch37TargetShapeTests`, one test
`EveryPatch37Target_IsNotAnOverridableVirtual`. Usings: `System`, `System.Collections.Generic`,
`System.Linq`, `System.Reflection`, `HarmonyLib`, `Microsoft.VisualStudio.TestTools.UnitTesting`,
`TAOM.Tests.Migration`. Shape:

```csharp
private static bool _gameLoaded;

[ClassInitialize]
public static void Init(TestContext _) => _gameLoaded = GameAssemblies.EnsureLoaded();

[TestMethod]
public void EveryPatch37Target_IsNotAnOverridableVirtual()
{
    if (!_gameLoaded)
        Assert.Inconclusive("Game assemblies not loaded: " + string.Join("; ", GameAssemblies.Diagnostics));

    // Types of TAOM.dll (typeof(TAOM.IoC).Assembly); on ReflectionTypeLoadException use ex.Types
    // minus nulls, as HarmonyPatchBindingTests.DiscoverPatchTypes does.
    // Keep a type when it carries a HarmonyPatchCategory attribute whose
    // ((HarmonyAttribute)a).info.category == TAOM.Features.CrashReport.Hooks.Patch37_CrashReport.Category
    // AND at least one [HarmonyPatch] attribute (this skips the marker class Patch37_CrashReport).
    // Assert.IsTrue(count >= 5, ...) so an empty discovery cannot pass.
    // For each: merge the class-level [HarmonyPatch] infos (declaringType, methodName, argumentTypes;
    // later non-null wins, as MergeSpec does), resolve with
    // AccessTools.Method(declaringType, methodName, argumentTypes), Assert.IsNotNull on it, and
    // collect "<class> -> <declaringType>.<methodName>" when target.IsVirtual && !target.IsFinal.
    // Assert.AreEqual(0, offenders.Count, message) where the message explains:
    // "A finalizer on an overridable virtual never runs for an override (a different method), so it
    //  can never capture the throws it was written for. Patch the non-virtual dispatcher instead:"
    // followed by the offenders, one per line.
}
```

The comment block is the specification for the body; write it as code (model it on
`HarmonyPatchBindingTests.DiscoverPatchTypes`, lines 80-96, and `MergeSpec`, lines 184-211). The category
filter also picks up `CrashReportApplicationTickTrigger` (`DevTriggers/`, a postfix on the non-virtual
`Module.OnApplicationTick`); that is fine and intended.

**Verify**: Test (filtered) with `Patch37TargetShapeTests` → `Failed: 1`, and the failure message
lists exactly these four and nothing else:
`ScriptComponentBehaviorOnTickFinalizer`, `MissionViewOnMissionScreenTickFinalizer`,
`MissionBehaviorOnMissionTickFinalizer`, `MBSubModuleBaseOnSubModuleLoadFinalizer`.
If it is `Skipped` (Inconclusive), or lists any other class, STOP.

### Step 2 (GREEN): Delete the four dead finalizers

In `E:\repos\wt-006-crash-capture\Main\Features\CrashReport\Hooks\Patch37_CrashReport.cs`, delete the four
classes by deleting exactly these line ranges at `b2e387db`: 45-53 and 63-71 (each class plus the blank
line after it), and 103-120 (the blank line after `MissionTickFinalizer`, the last two dead classes and the
blank line between them). The file then ends with the closing brace of `MissionTickFinalizer`. Delete
from the bottom up (103-120, then 63-71, then 45-53) so the earlier numbers stay valid. Then replace the
header comment (lines 11-27) with:

```csharp
// Patch37_CrashReport category: 5 Harmony Finalizers on TaleWorlds lifecycle methods,
// PLUS one dev-trigger Postfix (CrashReportApplicationTickTrigger in DevTriggers/).
// Native2ManagedPatcher separately attaches the same kind of Finalizer, by hand with
// harmony.Patch, to the short callback-shim allowlist in Native2ManagedTargets.
//
// Every target here must be non-virtual (or a sealed override). A Finalizer on a base
// virtual never runs for an override, which is a different method: four such targets
// (MissionBehavior.OnMissionTick, MBSubModuleBase.OnSubModuleLoad,
// MissionView.OnMissionScreenTick, ScriptComponentBehavior.OnTick) could never fire and
// were removed on 2026-09-23. Patch37TargetShapeTests enforces the rule.
//
// A Finalizer that returns null swallows the exception (game continues); returning
// the exception lets it bubble. We always swallow (caller decision in helper).
//
// Priority 800 matches BetterExceptionWindow's published value, which keeps us at the
// same priority tier so when both are installed, the "first runs last" Finalizer
// ordering produces deterministic behaviour. The service's TrySuspend on BUTR's
// handler should make co-existence rare in practice.
//
// Registered first in SubModule.OnSubModuleLoad; see the comment there and
// docs/features/crash-report.md.
```

**Verify**: Build exits 0; Test (filtered) with `Patch37TargetShapeTests` → `Failed: 0, Passed: 1, Skipped: 0`;
`git -C E:/repos/wt-006-crash-capture grep -n -E "ScriptComponentBehaviorOnTickFinalizer|MissionViewOnMissionScreenTickFinalizer|MissionBehaviorOnMissionTickFinalizer|MBSubModuleBaseOnSubModuleLoadFinalizer" -- Main TAOM.Tests`
→ no output. Commit `Main/Features/CrashReport/Hooks/Patch37_CrashReport.cs` and
`TAOM.Tests/Features/CrashReport/Patch37TargetShapeTests.cs` as
`fix(crash-report): v2.0.30 - drop four finalizers on empty virtuals`.

### Step 3 (RED): Pin the allowlist against the installed engine

Create `E:\repos\wt-006-crash-capture\TAOM.Tests\Features\CrashReport\Native2ManagedTargetsTests.cs`, namespace
`TAOM.Tests.Features.CrashReport`, `[TestClass]` class `Native2ManagedTargetsTests`, `using TAOM.Features.CrashReport.Hooks;`,
with these tests (they reference the not-yet-existing `Native2ManagedTargets`):

1. `All_ResolvesEveryShimAgainstTheInstalledEngine`: call
   `Native2ManagedTargets.Resolve(Native2ManagedTargets.All, LoadByName, missing)` where
   `LoadByName = name => { try { return Assembly.Load(name); } catch { return null; } }` and
   `missing` is a `List<string>`. Assert `missing.Count == 0` (message: the missing names), resolved count
   equals `All.Count`, and every resolved method `IsStatic` and has `GetMethodBody() != null`.
2. `Resolve_ReportsAMissingMethod_AndSkipsIt`: targets =
   `new[] { ("TaleWorlds.Engine.AutoGenerated", "ManagedCallbacks.EngineCallbacksGenerated", "NoSuchCallback_Taom006") }`
   with `LoadByName`; expect 0 resolved and one `missing` entry containing `NoSuchCallback_Taom006`.
3. `Resolve_ReportsAMissingAssembly_AndSkipsIt`: same target with `findAssembly = _ => null`; expect 0
   resolved and one `missing` entry containing `TaleWorlds.Engine.AutoGenerated`.
4. `All_IsASmallDistinctAllowlist`: all entries distinct, and `All.Count <= 12`, with the message
   "each entry is one harmony.Patch at boot (about 120 to 190 ms on the maintainer's desktop); the
   old 247-method sweep cost 29 to 33 s of every launch. Add an entry only with a stated reason."

No `Assert.Inconclusive` here: the AutoGenerated DLLs are always in the test bin.

**Verify**: Test (filtered) with `Native2ManagedTargetsTests` fails to COMPILE with CS0103 or CS0246
naming `Native2ManagedTargets`. That compile failure is the RED.

### Step 4 (GREEN): Replace the sweep with the allowlist, and time the attach

1. Create `E:\repos\wt-006-crash-capture\Main\Features\CrashReport\Hooks\Native2ManagedTargets.cs` with exactly:

```csharp
using System;
using System.Collections.Generic;
using System.Reflection;

namespace TAOM.Features.CrashReport.Hooks;

// The native-to-managed callback shims that get a crash-capture Finalizer. An allowlist, not a
// sweep: each harmony.Patch costs about 120 to 190 ms at boot on the maintainer's desktop
// (2026-09-23 logs), so patching every static method of every *CallbacksGenerated type (247)
// cost 29 to 33 s of every launch and captured nothing in 30 logged sessions.
//
// An entry earns its place by covering managed code that no Patch37 finalizer already wraps.
// Names are pinned against the installed engine by Native2ManagedTargetsTests.
public static class Native2ManagedTargets
{
    private const string EngineAssembly = "TaleWorlds.Engine.AutoGenerated";
    private const string EngineCallbacks = "ManagedCallbacks.EngineCallbacksGenerated";
    private const string CoreAssembly = "TaleWorlds.MountAndBlade.AutoGenerated";
    private const string CoreCallbacks = "ManagedCallbacks.CoreCallbacksGenerated";

    public static readonly IReadOnlyList<(string AssemblyName, string TypeName, string MethodName)> All = new[]
    {
        // ScreenManager.EarlyUpdate: no Patch37 finalizer wraps it.
        (EngineAssembly, EngineCallbacks, "EngineScreenManager_PreTick"),
        // ScreenManager.LateTick: every screen layer's RenderTick.
        (EngineAssembly, EngineCallbacks, "EngineScreenManager_LateTick"),
        // ScreenManager.Update(IReadOnlyList<int>): layer input. Patch37 wraps only the private no-arg Update.
        (EngineAssembly, EngineCallbacks, "EngineScreenManager_Update"),
        // ManagedScriptHolder.TickComponents: calls every ScriptComponentBehavior.OnTick override.
        (EngineAssembly, EngineCallbacks, "ManagedScriptHolder_TickComponents"),
        // ThumbnailCreatorView.OnThumbnailRenderComplete: portrait and item thumbnail callbacks.
        (EngineAssembly, EngineCallbacks, "ThumbnailCreatorView_OnThumbnailRenderComplete"),
        // BannerlordTableauManager.RequestCharacterTableauSetup: character tableau setup.
        (CoreAssembly, CoreCallbacks, "BannerlordTableauManager_RequestCharacterTableauSetup"),
    };

    // Resolves each target to its MethodInfo. A target whose assembly, type or method is absent is
    // added to `missing` as "Assembly/Type.Method" and skipped, so an engine rename costs one
    // warning line, never the whole attach.
    public static List<MethodInfo> Resolve(
        IEnumerable<(string AssemblyName, string TypeName, string MethodName)> targets,
        Func<string, Assembly?> findAssembly,
        ICollection<string> missing)
    {
        var resolved = new List<MethodInfo>();
        foreach (var (assemblyName, typeName, methodName) in targets)
        {
            MethodInfo? method = null;
            try
            {
                var type = findAssembly(assemblyName)?.GetType(typeName, throwOnError: false);
                method = type?.GetMethod(methodName, BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public);
            }
            catch (Exception)
            {
                method = null;
            }

            if (method != null)
                resolved.Add(method);
            else
                missing.Add($"{assemblyName}/{typeName}.{methodName}");
        }
        return resolved;
    }
}
```

2. In `E:\repos\wt-006-crash-capture\Main\Features\CrashReport\Hooks\Native2ManagedPatcher.cs`, replace
   lines 1-115 (everything from `using System;` down to the closing `}` of `Native2ManagedPatcher`, the
   line before the blank line 116) with exactly the block below. Lines 116-123 (the blank line and
   `Native2ManagedBridge`) stay unchanged in this step.

```csharp
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using TAOM.Core.Logging;

namespace TAOM.Features.CrashReport.Hooks;

// Attaches the crash-capture Finalizer to the native-to-managed callback shims listed in
// Native2ManagedTargets (an allowlist; see the cost note there). Harmony runs a Finalizer on
// every call of its target with a null __exception when nothing threw, so the steady-state
// cost is small but not zero. The MCM toggle EnableNativeToManagedCapture is read by
// Native2ManagedBridge when an exception arrives, so it works live without a restart.
public sealed class Native2ManagedPatcher
{
    private readonly IModLogger _logger;
    private bool _attached;

    public Native2ManagedPatcher(IModLogger logger)
    {
        _logger = logger;
    }

    public int AttachAll(Harmony harmony)
    {
        if (_attached) return 0;
        _attached = true;
        var stopwatch = Stopwatch.StartNew();

        // We can't use HandleAndSwallow directly as a Harmony Finalizer because it
        // takes (Exception, string). Wrap with a dedicated bridge method.
        var bridge = typeof(Native2ManagedBridge).GetMethod(
            "Finalizer",
            BindingFlags.NonPublic | BindingFlags.Static);
        if (bridge == null)
        {
            _logger.LogError("[CrashReport] Native2Managed: bridge Finalizer missing, skip");
            return 0;
        }

        var missing = new List<string>();
        var targets = Native2ManagedTargets.Resolve(Native2ManagedTargets.All, TryFindAssembly, missing);
        foreach (var name in missing)
            _logger.LogWarning($"[CrashReport] Native2Managed: target not found, skipped: {name}");

        int patched = 0;
        foreach (var m in targets)
        {
            try
            {
                harmony.Patch(m, finalizer: new HarmonyMethod(bridge));
                patched++;
            }
            catch (Exception ex)
            {
                _logger.LogWarning($"[CrashReport] Native2Managed: skip {m.DeclaringType?.FullName}.{m.Name}: {ex.GetType().Name}");
            }
        }
        _logger.LogInfo($"[CrashReport] Native2Managed: attached {patched} of {Native2ManagedTargets.All.Count} Finalizer(s) in {stopwatch.ElapsedMilliseconds} ms");
        return patched;
    }

    private static Assembly? TryFindAssembly(string simpleName)
    {
        try
        {
            return AppDomain.CurrentDomain.GetAssemblies()
                .FirstOrDefault(a => string.Equals(a.GetName().Name, simpleName, StringComparison.Ordinal));
        }
        catch { return null; }
    }
}
```

This removes the `AutogenAssemblies` array, the unused `finalizerMethod` lookup (it only checked that
`HandleAndSwallow` exists), the per-assembly type sweep and `SafeGetTypes`; `TryFindAssembly` is unchanged.
The stopwatch makes the attach cost visible in `taom_debug.log`; it has no unit test (timing is only
observable in a game launch) and is named in the commit's `Not-tested:` trailer.

**Verify**: Build exits 0; Test (filtered) with `Native2ManagedTargetsTests` → `Failed: 0, Passed: 4, Skipped: 0`;
`git -C E:/repos/wt-006-crash-capture grep -n "EndsWith(\"CallbacksGenerated\"" -- Main` → no output. Commit
`Main/Features/CrashReport/Hooks/Native2ManagedTargets.cs`, `Main/Features/CrashReport/Hooks/Native2ManagedPatcher.cs`,
`TAOM.Tests/Features/CrashReport/Native2ManagedTargetsTests.cs` as
`perf(crash-report): v2.0.30 - allowlist six callback shims, not 247` with trailers
`Not-tested: the live attach and its boot time need a game launch` and
`Research: v1.5.3 decompile of ManagedCallbacks.*CallbacksGenerated`.

### Step 5 (RED): Pin the live toggles

1. Create `E:\repos\wt-006-crash-capture\TAOM.Tests\Features\CrashReport\Native2ManagedBridgeTests.cs`,
   namespace `TAOM.Tests.Features.CrashReport`, `[TestClass]` class `Native2ManagedBridgeTests`,
   `using TAOM.Features.CrashReport.Hooks;`:
   - `HandleOrPassThrough_WhenNativeCaptureIsOff_HandsBackTheSameException`: `var ex = new InvalidOperationException("taom-006");`
     `Assert.AreSame(ex, Native2ManagedBridge.HandleOrPassThrough(ex, nativeCaptureEnabled: false));`
   - `HandleOrPassThrough_WithNoException_ReturnsNull`: returns null for `(null, true)` and `(null, false)`.
   (Do not test the `true` branch with an exception: it reaches `IoC.Resolve`, which the test process
   has not configured.)
2. In `E:\repos\wt-006-crash-capture\TAOM.Tests\Features\Mcm\SettingRequireRestartPostureTests.cs`, delete
   the two CrashReport rows of `RestartAllowlist` (lines 40-41) and replace doc-comment lines 25-29 in full
   (line 25 starts `/// everywhere, and a new setting`, line 29 ends `the only way to turn them on.`) with
   these six lines:

   ```csharp
   /// everywhere, and a new setting that omits the flag is a bug this test catches. The allowlist
   /// holds settings whose consumer is parked (commented out in SubModule.cs), where a restart does
   /// not help either but flipping the flag would promise an effect that does not exist. Note that no
   /// MCM setting can gate anything in OnSubModuleLoad: GlobalSettings<T>.Instance is null until MCM's
   /// own OnBeforeInitialModuleScreenSetAsRoot, which is why the two CrashReport toggles left this
   /// list on 2026-09-23 and are read at capture time instead.
   ```

   Lines 23-24 and 30 onward stay as they are.

**Verify**: Test (filtered) with `Native2ManagedBridgeTests` fails to compile (CS0117,
`HandleOrPassThrough` not found). That is the RED for the bridge. The RED for the posture test can
only run once the project compiles again, so it is observed in Step 6, sub-step 2.

### Step 6 (GREEN): Read both toggles at capture time; drop the restart requirement

1. In `E:\repos\wt-006-crash-capture\Main\Features\CrashReport\Hooks\Native2ManagedPatcher.cs`, replace
   the bridge block at the end of the file (the `// Bridge methods so Harmony Finalizers ...` comment,
   its second comment line, and the `internal static class Native2ManagedBridge { ... }` block) with:

   ```csharp
   // Harmony Finalizer for the allowlisted callback shims. Reads the MCM toggle only when an
   // exception is on the wire, so the per-call cost of the null path stays one comparison.
   internal static class Native2ManagedBridge
   {
       private const string Origin = "TaleWorlds.AutoGenerated.<callback>";

       internal static Exception? Finalizer(Exception __exception)
       {
           if (__exception == null) return null;
           bool enabled = true;
           try { enabled = CrashReportSettings.Instance?.EnableNativeToManagedCapture ?? true; }
           catch { /* a settings read failure must not change the capture path */ }
           return HandleOrPassThrough(__exception, enabled);
       }

       internal static Exception? HandleOrPassThrough(Exception? exception, bool nativeCaptureEnabled)
       {
           if (exception == null) return null;
           if (!nativeCaptureEnabled)
               return RethrowStackPreserver.PreserveForRethrow(exception, null);
           return CrashReportPatchHelper.HandleAndSwallow(exception, Origin);
       }
   }
   ```

   Add `using TAOM.Dependencies.Foundation;` (for `RethrowStackPreserver`) to the usings. `CrashReportSettings`
   is in `TAOM.Features.CrashReport`, the parent namespace, so it resolves without a using; add one only if
   the compiler asks. The origin string must stay exactly `TaleWorlds.AutoGenerated.<callback>`
   (crash signatures and log greps key on it).
2. Run Test (filtered) with `SettingRequireRestartPostureTests`
   → `EveryValueSetting_IsReadLive_SoRequireRestartIsFalse` FAILS naming
   `CrashReportSettings.EnableCrashCapture` and `CrashReportSettings.EnableNativeToManagedCapture`. This is the RED.
3. In `E:\repos\wt-006-crash-capture\Main\Features\CrashReport\CrashReportSettings.cs`, replace the two
   `[SettingPropertyBool(...)]` attributes (lines 20-21 and 30-31) with:

   ```csharp
   [SettingPropertyBool("Enable Crash Capture", Order = 0, RequireRestart = false,
       HintText = "Master toggle. When off, every TAOM crash finalizer passes exceptions straight through and the AppDomain hook ignores them, so the game's own handler (or BUTR) takes over. Takes effect immediately: the finalizers are always installed at launch and check this toggle only when an exception arrives. Default ON.")]
   ```

   ```csharp
   [SettingPropertyBool("Enable Native-to-Managed Capture", Order = 2, RequireRestart = false,
       HintText = "Logs and survives exceptions thrown inside a short list of native-to-managed callbacks (screen early, late and input ticks, scene-script ticks, thumbnail and character tableau callbacks) instead of crashing. When off, those exceptions pass straight through. Takes effect immediately. Default ON.")]
   ```

   Keep both `= true` defaults, the `[SettingPropertyGroup("Master")]` lines and the class comment at lines 7-9.

**Verify**: Build exits 0; Test (filtered) with `Native2ManagedBridgeTests` → `Passed: 2`, `Failed: 0`, and
with `SettingRequireRestartPostureTests` → `Failed: 0`, `Skipped: 0`. Do not commit yet: Step 7 belongs
to the same commit.

### Step 7: Remove the dead install-time gates in `Main/SubModule.cs` (lines 194-210 only)

In `E:\repos\wt-006-crash-capture\Main\SubModule.cs` (NOT the main checkout's copy), replace lines 194-210
(from `_harmony = new Harmony("com.taom.mod");` through the closing brace of the outer `if`) with:

```csharp
        _harmony = new Harmony("com.taom.mod");
        // Installed unconditionally. CrashReportSettings.Instance is always null here: MCM builds
        // its settings provider in its own OnBeforeInitialModuleScreenSetAsRoot, after every
        // OnSubModuleLoad, so an MCM gate at this point always took its fallback. The toggles are
        // read at capture time instead (CrashReportPatchHelper.HandleAndSwallow,
        // Native2ManagedBridge.Finalizer, AppDomainExceptionHook).
        try
        {
            _harmony.PatchCategory("Patch37_CrashReport");
            IoC.Resolve<TAOM.Features.CrashReport.Hooks.AppDomainExceptionHook>().Subscribe();
            IoC.Resolve<TAOM.Features.CrashReport.Hooks.Native2ManagedPatcher>().AttachAll(_harmony);
        }
        catch (System.Exception ex)
        {
            IoC.Resolve<IModLogger>().LogError($"[CrashReport] init failed: {ex.GetType().Name}: {ex.Message}");
        }
```

Keep lines 187-193 (the Codex MED-01 comment) and blank line 211 unchanged. Touch no other line of the file.

**Verify** (the numbers were measured by applying this exact replacement to `b2e387db`):
- Build exits 0.
- `git -C E:/repos/wt-006-crash-capture grep -n -E "Instance\?\.EnableCrashCapture\) \?\? true|Instance\?\.EnableNativeToManagedCapture\) \?\? true" -- Main/SubModule.cs` → no output.
- `git -C E:/repos/wt-006-crash-capture diff --stat HEAD -- Main/SubModule.cs` → `1 file changed, 13 insertions(+), 14 deletions(-)`.
- `git -C E:/repos/wt-006-crash-capture diff -U0 HEAD -- Main/SubModule.cs | grep '^@@'` → exactly two lines,
  beginning `@@ -195 +195,6 @@` and `@@ -197,13 +202,7 @@`. Any other count or hunk range is a STOP.

Run the two pre-commit checks, then commit `Main/Features/CrashReport/Hooks/Native2ManagedPatcher.cs`,
`Main/Features/CrashReport/CrashReportSettings.cs`, `Main/SubModule.cs`,
`TAOM.Tests/Features/CrashReport/Native2ManagedBridgeTests.cs`,
`TAOM.Tests/Features/Mcm/SettingRequireRestartPostureTests.cs` as
`fix(crash-report): v2.0.30 - make both capture toggles live` with trailer
`Not-tested: MCM toggling in a running game`.

### Step 8 (RED): Pin the logarithmic suppression cadence

In `E:\repos\wt-006-crash-capture\TAOM.Tests\Features\CrashReport\CrashBundleThrottleTests.cs`, add inside the class:

```csharp
[DataTestMethod]
[DataRow(0, false)]
[DataRow(1, true)]
[DataRow(2, true)]
[DataRow(3, false)]
[DataRow(9, false)]
[DataRow(10, true)]
[DataRow(11, false)]
[DataRow(20, false)]
[DataRow(100, true)]
[DataRow(1000, true)]
[DataRow(1001, false)]
[DataRow(int.MaxValue, false)]
public void IsLoggedOccurrence_LogsTheFirstTwoAndThenPowersOfTen(int occurrence, bool expected)
    => Assert.AreEqual(expected, CrashBundleThrottle.IsLoggedOccurrence(occurrence));
```

Why 2 as well as 1, 10, 100: occurrence 1 is the only trace of a new signature suppressed by the
cap or cooldown, and occurrence 2 is the first repeat of a bundled crash (the bundle itself is
occurrence 1), which tells a reader "this recurs" without waiting for the tenth frame.

**Verify**: Test (filtered) with `CrashBundleThrottleTests` fails to compile (CS0117, `IsLoggedOccurrence`).

### Step 9 (GREEN): Log suppressions at 1, 2, 10, 100, ...

1. In `E:\repos\wt-006-crash-capture\Main\Features\CrashReport\CrashBundleThrottle.cs`, add to the class:

   ```csharp
   // Which occurrences of a suppressed signature get a log line: 1 and 2, then every power of
   // ten. A throw that recurs every frame otherwise writes one line per frame into the log
   // that the next bundle tails.
   public static bool IsLoggedOccurrence(int occurrence)
   {
       if (occurrence < 1) return false;
       if (occurrence <= 2) return true;
       while (occurrence % 10 == 0) occurrence /= 10;
       return occurrence == 1;
   }
   ```

2. In `E:\repos\wt-006-crash-capture\Main\Features\CrashReport\CrashReportService.cs`, wrap the
   `_logger.LogError(...)` at lines 110-113 in
   `if (CrashBundleThrottle.IsLoggedOccurrence(admission.Occurrence)) { ... }` (keep `return null;`
   outside the `if`, so suppression itself is unchanged), and change the message tail from
   `occurrence #{admission.Occurrence} — bundle suppressed` to
   `occurrence #{admission.Occurrence}, bundle suppressed (logged at 1, 2, 10, 100, ...)`.
3. Rewrite the comment at lines 94-100 as these seven lines (same indentation, no dashes):

   ```csharp
            // Dedup chokepoint. The capture sources (5 per-tick Harmony Finalizers, the
            // AppDomain hook, the battle-load watchdog, the allowlisted native callback shims)
            // all funnel here, so a crash that recurs every frame would otherwise write a fresh
            // bundle each tick, re-zipping an ever-growing taom_debug.log. Compute the cheap
            // signature (reads the frozen stack only; no engine collectors) and let the
            // throttle decide. On suppression: a log line at occurrences 1, 2, 10, 100 and so on,
            // no ComposeContext, bundle or notify, which kills the disk spam and the log feedback loop.
   ```

**Verify**: Build exits 0; Test (filtered) with `CrashBundleThrottleTests` → `Failed: 0`,
`Skipped: 0`, and the 12 new data rows pass. Commit `Main/Features/CrashReport/CrashBundleThrottle.cs`,
`Main/Features/CrashReport/CrashReportService.cs`, `TAOM.Tests/Features/CrashReport/CrashBundleThrottleTests.cs` as
`perf(crash-report): v2.0.30 - log repeat suppressions at powers of ten` with trailer
`Not-tested: the gated LogError inside HandleException (needs the full collector graph)`.

### Step 10: Correct the docs and the API snapshot

All paths are under `E:\repos\wt-006-crash-capture\`. Line numbers are at `b2e387db`; edit from the
bottom of each file upward so earlier numbers stay valid. Write every added line without em or en dashes
(rewrite a whole line rather than keep a dash).

1. `docs/features/crash-report.md`:
   - Line 5: "any of 10 TaleWorlds lifecycle methods (or the `*CallbacksGenerated` native↔managed shims)"
     → "any of 5 TaleWorlds lifecycle methods (or one of the allowlisted native-to-managed callback shims in `Native2ManagedTargets`)".
   - Lines 32 and 34 (inside the architecture code block): "(10 lifecycle methods)" → "(5 lifecycle methods)";
     "Native2ManagedPatcher (CallbacksGenerated *)" → "Native2ManagedPatcher (allowlisted shims)".
   - Lines 54-65 (catch-point table): delete rows 2, 4, 8 and 9 (`ScriptComponentBehavior.OnTick`,
     `MissionView.OnMissionScreenTick`, `MissionBehavior.OnMissionTick`, `MBSubModuleBase.OnSubModuleLoad`),
     renumber the rest 1 to 5, and replace row 10 with this row 6:

     ```text
     | 6 | Native2Managed allowlist (`Native2ManagedTargets.All`): `EngineScreenManager_PreTick`, `EngineScreenManager_LateTick`, `EngineScreenManager_Update`, `ManagedScriptHolder_TickComponents`, `ThumbnailCreatorView_OnThumbnailRenderComplete`, `BannerlordTableauManager_RequestCharacterTableauSetup` | Native-to-managed callback shims whose managed work no row above wraps; attached by hand at startup |
     ```
   - Line 69 → "All of them run at Harmony priority 800, matching BEW. Harmony runs a finalizer on every call of its target, with a null `__exception` when nothing threw, and that path returns at once. Four more targets (`ScriptComponentBehavior.OnTick`, `MissionView.OnMissionScreenTick`, `MissionBehavior.OnMissionTick`, `MBSubModuleBase.OnSubModuleLoad`) were removed on 2026-09-23: they are base virtuals with empty or assert-only bodies, and a finalizer on a base method never runs for an override, so they could never fire. `Patch37TargetShapeTests` now refuses an overridable virtual target."
   - Line 82 (the `CrashBundleThrottle` paragraph) → "[`CrashBundleThrottle`](../../Main/Features/CrashReport/CrashBundleThrottle.cs), a pure, lock-guarded, clock-injected session singleton, sits at the `HandleException` chokepoint (consulted right after the BUTR-suspend block, before the heavy `ComposeContext`). The signature is computed cheaply up front (frozen stack only, no engine collectors); on any non-`WriteBundle` decision the method returns at once, skipping collection, bundle, and the player inquiry. It writes a suppression log line only on occurrences 1, 2, 10, 100, 1000 and so on of that signature (`CrashBundleThrottle.IsLoggedOccurrence`), so a throw that recurs every frame no longer writes one line per frame into the log the bundle tails. This simultaneously caps disk writes **and** breaks the growing-log feedback loop."
   - Lines 104 and 106 → these two rows:

     ```text
     | Master | Enable Crash Capture | true | Master toggle. When off, every Patch37 finalizer and the AppDomain hook pass exceptions through untouched. Live: the patches are always installed. |
     | Master | Enable Native-to-Managed Capture | true | When off, the allowlisted callback-shim finalizers pass exceptions through untouched. Live: the shims are always patched. |
     ```
   - Line 190: "10 Harmony Finalizer patches + category marker" → "5 Harmony Finalizer patches + category marker".
   - Line 191: "Reflection-driven patcher for `*CallbacksGenerated`" → "Attaches the crash finalizer to the callback-shim allowlist, and holds `Native2ManagedBridge`"; then add this row after it:

     ```text
     | [Main/Features/CrashReport/Hooks/Native2ManagedTargets.cs](../../Main/Features/CrashReport/Hooks/Native2ManagedTargets.cs) | The callback-shim allowlist, one stated reason per entry |
     ```
   - Line 207 (the dependency bullet that begins with `TaleWorlds.MountAndBlade.View` and ends with `MissionView.OnMissionScreenTick` target): delete the whole line. After Step 2 no crash-report target lives in that assembly.
   - Line 213: "[TAOM.Tests/Features/CrashReport/](../../TAOM.Tests/Features/CrashReport): 88 tests (counted 2026-09-22):" → "[TAOM.Tests/Features/CrashReport/](../../TAOM.Tests/Features/CrashReport), by class:" (the count is stale and nothing recomputes it).
   - In that bullet list (the one containing `RingBufferTests`, line 219), add four bullets after the `RingBufferTests` bullet:
     "- `Patch37TargetShapeTests`: every Patch37 target resolves and is not an overridable virtual",
     "- `Native2ManagedTargetsTests`: every allowlisted shim resolves against the installed engine; a missing assembly or method is reported and skipped; the list stays small and distinct",
     "- `Native2ManagedBridgeTests`: with the toggle off the bridge hands back the same exception; with no exception it returns null",
     "- `CrashBundleThrottleTests`: dedup, session cap and cooldown admission, and the 1, 2, 10, 100 suppression-log cadence".
     (At `b2e387db` that list has no `CrashBundleThrottleTests` bullet; line 92 links the class instead.)
   - Line 224 (the bullet that starts `- The 10 Harmony Finalizers`) → "- The 5 Harmony Finalizers and the live Native2Managed attach: covered by manual QA via MCM dev triggers".
   - Line 273: "Restart the game. Patch37 won't apply; the other mod's Finalizers take over." → "No restart is needed: TAOM's finalizers stay installed but pass every exception through, so the other mod's Finalizers take over."
   - Lines 277-278 → two bullets:
     "- **Boot cost: one `harmony.Patch` per target.** Each attach cost about 120 to 190 ms on the maintainer's desktop on 2026-09-23 (the old sweep of all 247 `*CallbacksGenerated` methods cost 29 to 33 s on 30 of 30 launches, and PatchShield timed 186 ms per attach in the same process). That is why Native2Managed is an allowlist. `[CrashReport] Native2Managed: attached N of M Finalizer(s) in X ms` in `taom_debug.log` shows the current cost."
     "- **Steady-state cost: small, not zero.** Harmony runs a finalizer on every call of its target (with a null `__exception` on success, which returns at once), and the patched method becomes a replacement wrapped in try/catch."
   - Lines 284-285 → one bullet replacing both:
     "- **Other mods' `OnSubModuleLoad` throws are not captured.** TAOM applies Patch37 inside its own `OnSubModuleLoad`, and a finalizer on the base `MBSubModuleBase.OnSubModuleLoad` would never see an override's throw, so none is attached; those throws land in vanilla or BUTR."
   - Changelog section: add as the first entry under `## Changelog` (line 301):
     "- 2026-09-23: **Boot cost and dead finalizers** (plan 006). The Native2Managed sweep patched all 247 `*CallbacksGenerated` methods and cost 29 to 33 s of every launch; it is now an allowlist of six shims (`Native2ManagedTargets`) and logs its attach time. Four Patch37 finalizers on empty base virtuals were deleted. Both MCM toggles are read at capture time (the `OnSubModuleLoad` gate always saw a null MCM instance) and no longer ask for a restart. Suppression log lines for a recurring crash are written at occurrences 1, 2, 10, 100 and so on."
2. `docs/features/mcm.md` lines 94-100 → replace with:
   "classes and fails on any value attribute without the flag. One is allowlisted by `Class.Property` with a reason: `TaomSettings.EnableNativeSkinFixes` (parked; its consumer is commented out, so no value of the flag is honest). The two CrashReport toggles sat on that list until 2026-09-23 on the belief that `SubModule.OnSubModuleLoad` read them to decide whether to install the crash patches. It never could: `GlobalSettings<T>.Instance` is null until MCM's own `OnBeforeInitialModuleScreenSetAsRoot`, so the read always took its `?? true` fallback. Both are now read at capture time and apply live. A new setting whose consumer really does bind at process start goes on that list with its reason, not on a flag alone, and no MCM setting can gate anything in `OnSubModuleLoad`."
   (Line 93 ends with "reflects over the four settings"; keep it and make the join read as one sentence.)
3. `docs/features/hero-race.md` lines 242-243 → "- The silent death points to a native fault. TAOM's Patch37 finalizers on the screen and application ticks (`ScreenManager.Tick`, `Managed.ApplicationTick`) log and swallow a managed throw on the conversation's UI tick path instead of letting it crash."
4. `docs/reference/engine/gauntletui-viewmodel-screen.md` lines 111-112 → "`MapConversationTextureProvider.Clear` on a provider that is still ticking. That tick runs inside `ScreenManager.Tick` (`GauntletLayer.Tick` → `UIContext.Update` → `EventManager.Update` → `Widget.Update` → `TextureWidget.OnUpdate` → `MapConversationTextureProvider.Tick` → `MapConversationTableau.OnTick`, v1.5.3), so TAOM's Patch37 finalizer on `ScreenManager.Tick` would log and swallow such a throw rather than crash."
5. `docs/reference/harmony-patch-registry.md`:
   - Line 268 → "**Target:** 5 Finalizers on engine lifecycle methods (`Managed.ApplicationTick`, `Module.OnApplicationTick`, `ScreenManager.Tick`, `ScreenManager.Update()` (the no-arg inner overload, explicitly disambiguated), `Mission.Tick`), plus a dev-trigger Postfix on `Module.OnApplicationTick` (`CrashReportApplicationTickTrigger` in `DevTriggers/`, priority 900 so its MCM-toggled throw lands INTO the Finalizer), plus Finalizers that `Native2ManagedPatcher` attaches by hand to the six callback shims listed in `Native2ManagedTargets`. Every target must be non-virtual: a finalizer on a base virtual never runs for an override (four such targets were removed on 2026-09-23; `Patch37TargetShapeTests` enforces it)."
   - Line 270 (starts "TAOM's crash-capture pipeline.") → "TAOM's crash-capture pipeline. Every Finalizer (all at `[HarmonyPriority(800)]`, matching BetterExceptionWindow's published tier so co-installed ordering is deterministic; the service's `TrySuspend` on BUTR's handler makes co-existence rare) routes the exception through `CrashReportPatchHelper.HandleAndSwallow`, which captures the report and swallows (returns null) so the game continues. The five Finalizers and the dev trigger share this category; the Native2Managed Finalizers are attached with `harmony.Patch`, outside `PatchCategory`. Registered first in `SubModule.OnSubModuleLoad` (immediately after `IoC.Configure()`) so the tick Finalizers are live for the rest of TAOM's load; other mods' `OnSubModuleLoad` throws are not captured (see `docs/features/crash-report.md`)."
6. `docs/reference/taleworlds-api-snapshot/reflection-sites.md` line 111 → this row (keep the edit to that one line):

   ```text
   | `CrashReport/Hooks/Native2ManagedPatcher.cs`, `Native2ManagedTargets.cs` | `typeof(Native2ManagedBridge)`; `Assembly.GetType` and `GetMethod` by name on `ManagedCallbacks.*CallbacksGenerated` | the TAOM bridge type, plus six engine callback-shim names pinned offline by `Native2ManagedTargetsTests` (not by `ReflectionSiteBindingTests`) |
   ```

   The row stays in its current section (Category D, "TAOM-internal, intentionally not gated") even though
   it now names six engine members looked up by string, because a dedicated test pins them. Do not move it;
   the reviewer decides (see Maintenance notes).
7. `docs/reference/taleworlds-api-snapshot/patch-targets.md`: delete the four rows naming
   `MBSubModuleBaseOnSubModuleLoadFinalizer`, `MissionBehaviorOnMissionTickFinalizer`,
   `MissionViewOnMissionScreenTickFinalizer`, `ScriptComponentBehaviorOnTickFinalizer` (lines 101, 102,
   104, 108 at `b2e387db`) and change `Patches: 247.` to `Patches: 243.` in the header (line 7).
   Then, only if Step 0's snapshot check exited 0: run Test (full) so `TAOM.Tests\bin\Debug\net472\TAOM.dll`
   is current, then run the API snapshot check. Expected: exit 0 after your hand edit. If it exits non-zero,
   run `cd E:/repos/wt-006-crash-capture && pwsh tools/snapshot_api_surface.ps1` (write mode) and check that
   `git -C E:/repos/wt-006-crash-capture diff -- docs/reference/taleworlds-api-snapshot/` touches only
   `patch-targets.md`, and within it only those four rows and the count; anything else is a STOP (restore
   the file to your hand edit first).

**Verify**: Docs (dead-link gate) exits 0 with `Dead links: **0**`; the dash check in Done criteria prints
nothing. Run the pre-commit checks, then commit the seven doc files as
`docs(crash-report): v2.0.30 - correct capture cost and coverage`.

### Step 11: Final verification and report

Run every Done-criteria command. Then put these in your final report (you do not write them to files):

- **CHANGELOG entry** for the orchestrator to add (one paragraph, no dashes): "Crash capture no longer
  costs about 30 s of every boot: the native-to-managed sweep patched all 247 engine callback methods
  and now patches an allowlist of six. Four crash finalizers that could never fire were removed. Both
  crash-capture MCM toggles now work live without a restart (before, the game ignored them at
  launch). A crash that repeats every frame now logs its suppression line at occurrences 1, 2, 10, 100
  and so on instead of every frame."
- **Orientation trap row** to propose (orientation.md is another session's file):

  ```text
  | MCM in OnSubModuleLoad | GlobalSettings<T>.Instance is null until MCM's OnBeforeInitialModuleScreenSetAsRoot; a gate there always takes its fallback | [mcm](../features/mcm.md) |
  ```
- **Lesson** to propose for `docs/reviews/lessons/harmony-il.md`: "A Harmony finalizer on a base virtual
  never runs for an override; patch the non-virtual dispatcher (`Patch37TargetShapeTests`, plan 006)."
- The five commit hashes and subjects, the Step 0 failure list, the final test counts and failure
  names, the drift-check output, and the owed in-game checks (Maintenance notes).

## Test plan

- **New tests** (all in `TAOM.Tests/Features/CrashReport/`, namespace `TAOM.Tests.Features.CrashReport`):
  - `Patch37TargetShapeTests.EveryPatch37Target_IsNotAnOverridableVirtual`: RED lists exactly the four
    dead classes; GREEN after deletion. Uses `GameAssemblies.EnsureLoaded()` (View.dll lives in `Modules\Native\bin`).
  - `Native2ManagedTargetsTests` (4 tests): every allowlisted shim resolves (happy path); missing method
    reported and skipped; missing assembly reported and skipped; list distinct and at most 12.
  - `Native2ManagedBridgeTests` (2 tests): toggle off hands back the same exception instance (through
    `RethrowStackPreserver`, which returns the same instance); no exception returns null for both flag values.
  - `CrashBundleThrottleTests.IsLoggedOccurrence_LogsTheFirstTwoAndThenPowersOfTen` (12 data rows):
    below range (0), the two first occurrences (1, 2), non-logged small values (3, 9), powers of ten
    (10, 100, 1000), near misses (11, 20, 1001), and `int.MaxValue`.
- **Changed test**: `SettingRequireRestartPostureTests` loses two allowlist rows; its existing two tests
  then prove both CrashReport settings carry `RequireRestart = false`.
- **Structural patterns**: `TAOM.Tests/Migration/HarmonyPatchBindingTests.cs` (target resolution from
  attributes, `ReflectionTypeLoadException` handling), `TAOM.Tests/Features/CrashReport/CrashBundleThrottleTests.cs`
  (plain MSTest style for the pure helpers).
- **Structurally untestable** (name in `Not-tested:` trailers): the live `harmony.Patch` on the engine
  shims and its boot time (the stopwatch); MCM toggling in a running game; the gated `LogError` inside
  `CrashReportService.HandleException` (needs the 14-collector graph).
- **Verification**: Test (full) shows failures only from the baseline rule (the 2 Armory tests, if they
  still fail, plus the Step 0 list), and the total count rose by the new tests (1 + 4 + 2 + 12 data rows,
  reported however your runner counts data rows).

## Done criteria

ALL must hold. Every command runs in Git Bash; W is `E:/repos/wt-006-crash-capture`.

- [ ] `git -C E:/repos/wt-006-crash-capture rev-parse --abbrev-ref HEAD` prints `plan-006-crash-capture-boot-cost`.
- [ ] `cd E:/repos/wt-006-crash-capture && dotnet build Main/TAOM.csproj -p:DisableModuleCopy=true -p:ModuleId=` exits 0 with `0 Error(s)`.
- [ ] `cd E:/repos/wt-006-crash-capture && dotnet test TAOM.Tests -p:DisableModuleCopy=true -p:ModuleId=` reports
      failures only among `TheElkItem_DeclaresTheScaleTheReachIsTunedFor`,
      `AnimaliaActionSets_BindOnlyHorseActions_ToClipsThatExist` and the Step 0 list (zero failures also passes).
- [ ] `cd E:/repos/wt-006-crash-capture && dotnet test TAOM.Tests -p:DisableModuleCopy=true -p:ModuleId= --filter "FullyQualifiedName~Patch37TargetShapeTests|FullyQualifiedName~Native2ManagedTargetsTests|FullyQualifiedName~Native2ManagedBridgeTests|FullyQualifiedName~CrashBundleThrottleTests|FullyQualifiedName~SettingRequireRestartPostureTests"`
      reports `Failed: 0` and `Skipped: 0`.
- [ ] `git -C E:/repos/wt-006-crash-capture grep -n -E "ScriptComponentBehaviorOnTickFinalizer|MissionViewOnMissionScreenTickFinalizer|MissionBehaviorOnMissionTickFinalizer|MBSubModuleBaseOnSubModuleLoadFinalizer" -- Main TAOM.Tests docs/reference` prints nothing.
- [ ] `git -C E:/repos/wt-006-crash-capture grep -n "EndsWith(\"CallbacksGenerated\"" -- Main` prints nothing.
- [ ] `git -C E:/repos/wt-006-crash-capture grep -n -E "Instance\?\.Enable(CrashCapture|NativeToManagedCapture)\) \?\? true" -- Main/SubModule.cs` prints nothing.
- [ ] `git -C E:/repos/wt-006-crash-capture grep -n -i -E "cost zero|cost ~0|costs ~0|zero-cost|Steady-state cost: zero" -- Main/Features/CrashReport docs/features/crash-report.md` prints nothing.
- [ ] `git -C E:/repos/wt-006-crash-capture grep -n "RequireRestart = false" -- Main/Features/CrashReport/CrashReportSettings.cs` prints 6 lines
      (the 4 that already had it plus the 2 toggles).
- [ ] `git -C E:/repos/wt-006-crash-capture grep -n "= true;" -- Main/Features/CrashReport/CrashReportSettings.cs` still shows
      `EnableCrashCapture { get; set; } = true;` and `EnableNativeToManagedCapture { get; set; } = true;` (defaults unchanged).
- [ ] Dash check on every added line exits 0 and prints nothing (`-X utf8` is required: without it Python
      on this machine decodes the pipe as cp1252 and the check can never fail):
      `git -C E:/repos/wt-006-crash-capture diff b2e387db -U0 | python -X utf8 -c "import sys; bad=[l for l in sys.stdin.read().splitlines() if l.startswith('+') and not l.startswith('+++') and (chr(0x2014) in l or chr(0x2013) in l)]; print(chr(10).join(bad)); sys.exit(1 if bad else 0)"`
- [ ] `cd E:/repos/wt-006-crash-capture && python tools/lint_docs.py --quick --fail-on-dead` exits 0 with `Dead links: **0**`;
      `cd E:/repos/wt-006-crash-capture && python tools/validate_moduledata.py` reports 0 errors.
- [ ] `git -C E:/repos/wt-006-crash-capture diff --name-only b2e387db` lists only files from the Scope "In scope" list, and
      `git -C E:/repos/wt-006-crash-capture diff -U0 b2e387db -- Main/SubModule.cs | grep '^@@'` prints exactly two lines,
      beginning `@@ -195 +195,6 @@` and `@@ -197,13 +202,7 @@`.
- [ ] `git -C E:/repos/wt-006-crash-capture status --porcelain` is empty (everything committed), and every subject from
      `git -C E:/repos/wt-006-crash-capture log --format=%s b2e387db..HEAD` (five commits) matches `^[a-z]+\(crash-report\): v[0-9]+\.[0-9]+\.[0-9]+ - ` and is at most 72 characters.
- [ ] No commit carries a `Co-Authored-By` line: `git -C E:/repos/wt-006-crash-capture log --format=%B b2e387db..HEAD | grep -c -i "co-authored-by"` prints 0.
- [ ] Nothing landed in the main checkout's branch: `git -C E:/repos/TAOM log --format=%h b2e387db..bannerlord-1.5.x --grep="(crash-report)"` prints nothing.

## STOP conditions

Stop and report back (do not improvise) if:

- Any file you created, edited or staged resolves under `E:\repos\TAOM\` instead of W, or the Tree guard
  prints anything other than `plan-006-crash-capture-boot-cost` before a commit. Do not try to undo it
  with `git checkout`, `reset` or `stash` in the main checkout; report what you touched.
- Any excerpt in "Current state" does not match your worktree at `b2e387db`.
- Step 0's full run fails a test in `TAOM.Tests/Features/CrashReport/`, `SettingRequireRestartPostureTests`
  or `HarmonyPatchBindingTests`.
- Step 1's RED lists any class other than the four named, or is `Skipped`/Inconclusive (game assemblies
  not loaded): the virtual check or the environment is not what this plan assumes.
- `Native2ManagedTargetsTests.All_ResolvesEveryShimAgainstTheInstalledEngine` reports a missing shim.
  Do NOT substitute a similar-looking callback name; report which name and what the decompile shows
  (`pwsh tools/taom-src.ps1 path ManagedCallbacks.EngineCallbacksGenerated` or `...CoreCallbacksGenerated`).
- `RethrowStackPreserver.PreserveForRethrow` does not exist with the signature quoted above, or
  `Native2ManagedBridgeTests.HandleOrPassThrough_WhenNativeCaptureIsOff_HandsBackTheSameException` fails
  because it returns a different instance.
- Step 7's diff stat or hunk headers differ from the expected ones.
- A full run shows a failure outside the baseline rule, twice after a reasonable fix attempt of YOUR change.
  Never edit a file outside Scope to make a test pass.
- The change seems to need `Main/IoC.cs`, `CrashReportIoC.cs`, a csproj, `Directory.Build.props`, or any
  line of `Main/SubModule.cs` outside 194-210.
- You are tempted to flip a capture default, rename a setting, or add a mission or agent callback to the
  allowlist: those are maintainer decisions (Maintenance notes), not executor ones.
- Snapshot regeneration (Step 10.7) changes anything beyond the four rows and the count.
- You find evidence that a crash the maintainer cares about was being caught ONLY by one of the 241
  shims this plan drops (for example a `taom_debug*.log` or crash bundle whose origin is
  `TaleWorlds.AutoGenerated.<callback>`): report it with the file and line; do not widen the list yourself.

## Maintenance notes

- **Riskiest assumption:** that the six-shim allowlist keeps the safety net that matters. The dropped
  241 shims include the mission combat callbacks (`Mission_MeleeHitCallback`, `Mission_MissileHitCallback`,
  `Mission_OnAgentRemoved`, `Agent_UpdateAgentStats` and so on). Evidence for dropping them: 0 of 30 local
  `taom_debug_*.log` files (2026-09-19 to 2026-09-23, re-counted while planning with
  `grep -l -F "AutoGenerated.<callback>"`) ever carried the bridge origin, the audit's checker found the
  same for the 4 local crash bundles, and no doc relies on them. Evidence against: only one machine's logs were checked, not player bundles.
- **Open question for Mike (not decided here):** (1) whether crash capture should stay ON by default
  (this plan keeps it ON; the posture is recorded at `CrashReportSettings.cs:7-9`, and a default flip
  would need a renamed setting because MCM json2 keeps a persisted value, trap "Persisted MCM
  defaults"); (2) whether to add the mission combat callbacks to `Native2ManagedTargets`, at about 120 to
  190 ms of boot each on this desktop.
- **Owed in-game checks** (label the issue `triage-needs-ingame` if closed before they are done):
  (a) launch once: `taom_debug.log` shows `[CrashReport] Native2Managed: attached 6 of 6 Finalizer(s) in N ms`
  with N around 1,000 or less, and the gap between the `[SaveDefiners]` line and that line drops from
  29 to 33 s to about 1 to 2 s; (b) MCM `Throw On Next Application Tick` still produces one bundle with
  origin `Module.OnApplicationTick`; (c) turn `Enable Crash Capture` off in MCM, press Done: no restart
  prompt, and the next dev-trigger throw is not captured by TAOM.
- **Reviewer focus (`/deep-review`, orchestrator):** the `Native2ManagedBridge` pass-through path
  (value-returning finalizer, `RethrowStackPreserver`); that no finalizer reads MCM on the null path; the
  SubModule hunk holds parity; the allowlist reasons against the decompile; the hint texts; and whether
  the `reflection-sites.md` row (six engine names looked up by string, pinned by
  `Native2ManagedTargetsTests`) should move from Category D to a gated category.
- **Plan 007 interaction:** `plans/007-patchshield-skip-callback-shims.md` lists
  `Main/Features/CrashReport/Hooks/Native2ManagedPatcher.cs` in its drift check as read-only context. After
  006 lands that drift check fires by design; 007's own change (PatchShield skips the
  `ManagedCallbacks` namespace) stays valid, but its "about 247 of 372 attaches" figure becomes about 6.
- **Plan 009 interaction (merge conflict, plan the order):** `plans/009-guarded-patch-category-apply.md`
  is also cut from `b2e387db` and edits the same `Main/SubModule.cs` Patch37 site (its action A wraps the
  `_harmony.PatchCategory("Patch37_CrashReport")` call at line 199 and rewrites the MED-01 comment at
  187-193), and replaces `docs/features/crash-report.md:284`, which this plan also replaces. Its
  argument at its line 237 cites the `MBSubModuleBase.OnSubModuleLoad` finalizer
  (`Patch37_CrashReport.cs:113`) that this plan deletes. Whichever lands second must re-base: 009's
  SubModule edit applies to the 006 block (no outer `if`), and 009's crash-report.md text must drop
  the sentence about that finalizer.
- **Deferred, out of scope:** other MCM reads reached from `OnSubModuleLoad` may share the always-null
  problem (not audited); `docs/reference/engine/submodule-lifecycle-and-harmony.md:57` describes
  `Native2ManagedPatcher` as a way to hook native methods, which it is not. Whether
  `UnpatchCategory("Patch37_CrashReport")` would also detach the hand-attached Native2Managed finalizers
  is unverified against Harmony internals; Step 10 removes the registry's claim that it does rather than
  asserting either way. Raise each separately.
