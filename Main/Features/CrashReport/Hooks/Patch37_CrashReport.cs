using System;
using HarmonyLib;
using TaleWorlds.DotNet;
using TaleWorlds.Engine;
using TaleWorlds.MountAndBlade;
using TaleWorlds.MountAndBlade.View.MissionViews;
using TaleWorlds.ScreenSystem;

namespace TAOM.Features.CrashReport.Hooks;

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
// Marker class for the Patch37_CrashReport category. No [HarmonyPatch] attribute —
// the category attribute is applied directly to each Finalizer class below.
[HarmonyPatchCategory("Patch37_CrashReport")]
public static class Patch37_CrashReport
{
    public const string Category = "Patch37_CrashReport";
}

[HarmonyPatch(typeof(Managed), "ApplicationTick")]
[HarmonyPatchCategory("Patch37_CrashReport")]
public static class ManagedApplicationTickFinalizer
{
    [HarmonyPriority(800)]
    private static Exception? Finalizer(Exception __exception)
        => CrashReportPatchHelper.HandleAndSwallow(__exception, "TaleWorlds.DotNet.Managed.ApplicationTick");
}

[HarmonyPatch(typeof(Module), "OnApplicationTick")]
[HarmonyPatchCategory("Patch37_CrashReport")]
public static class ModuleOnApplicationTickFinalizer
{
    [HarmonyPriority(800)]
    private static Exception? Finalizer(Exception __exception)
        => CrashReportPatchHelper.HandleAndSwallow(__exception, "TaleWorlds.MountAndBlade.Module.OnApplicationTick");
}

[HarmonyPatch(typeof(ScreenManager), "Tick")]
[HarmonyPatchCategory("Patch37_CrashReport")]
public static class ScreenManagerTickFinalizer
{
    [HarmonyPriority(800)]
    private static Exception? Finalizer(Exception __exception)
        => CrashReportPatchHelper.HandleAndSwallow(__exception, "TaleWorlds.ScreenSystem.ScreenManager.Tick");
}

// v1.4.5 ScreenManager.Update has two overloads:
//   private static void Update()                — what we want
//   public  static void Update(IReadOnlyList<int>) — keys-pressed flavour, fired by Tick
// We patch the inner no-arg one because that's the inner-loop and where most
// renderer throws originate. Explicit empty Type[] disambiguates from the overload.
[HarmonyPatch(typeof(ScreenManager), "Update", new Type[0])]
[HarmonyPatchCategory("Patch37_CrashReport")]
public static class ScreenManagerUpdateFinalizer
{
    [HarmonyPriority(800)]
    private static Exception? Finalizer(Exception __exception)
        => CrashReportPatchHelper.HandleAndSwallow(__exception, "TaleWorlds.ScreenSystem.ScreenManager.Update");
}

[HarmonyPatch(typeof(Mission), "Tick")]
[HarmonyPatchCategory("Patch37_CrashReport")]
public static class MissionTickFinalizer
{
    [HarmonyPriority(800)]
    private static Exception? Finalizer(Exception __exception)
        => CrashReportPatchHelper.HandleAndSwallow(__exception, "TaleWorlds.MountAndBlade.Mission.Tick");
}
