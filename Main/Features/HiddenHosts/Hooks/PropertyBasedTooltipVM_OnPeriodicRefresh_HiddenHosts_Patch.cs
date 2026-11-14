using System;
using HarmonyLib;
using TAOM.Core.Diagnostics;
using TaleWorlds.Core.ViewModelCollection.Information;
using TaleWorlds.Library;

namespace TAOM.Features.HiddenHosts.Hooks;

/// <summary>
/// Patch105 (docs/reference/harmony-patch-registry.md "Patch105_HiddenHosts", tooltips). A property-based tooltip re-runs
/// every row's value function every two seconds from <c>OnPeriodicRefresh</c>, long after the builder returned, and the
/// troop count rows are such functions: a bracket on the builders alone would show the mask for one frame. This prefix
/// opens a fresh frame for the subject the tooltip was invoked with (<c>_invokedType</c> and <c>_invokedArgs</c> of
/// <c>TooltipBaseVM</c>), with a fresh distance, and the finalizer closes it. Any tooltip type but the five subject types
/// opens nothing. Main thread, from the UI tick.
/// </summary>
[HarmonyPatch(typeof(PropertyBasedTooltipVM), "OnPeriodicRefresh")]
[HarmonyPatchCategory(HiddenHostsModule.PatchCategory)]
[ClientOnly]
public static class PropertyBasedTooltipVM_OnPeriodicRefresh_HiddenHosts_Patch
{
    // Bound once, fail-soft: a missing field on a future engine leaves a null and the hook stands down, reported once.
    private static readonly AccessTools.FieldRef<TooltipBaseVM, Type>? InvokedType = Bind<Type>("_invokedType");
    private static readonly AccessTools.FieldRef<TooltipBaseVM, object[]>? InvokedArgs = Bind<object[]>("_invokedArgs");

    private static bool _bindReported;

    private static AccessTools.FieldRef<TooltipBaseVM, T>? Bind<T>(string field)
    {
        try { return AccessTools.FieldRefAccess<TooltipBaseVM, T>(field); }
        catch { return null; }
    }

    [HarmonyPrefix]
    public static void Prefix(PropertyBasedTooltipVM __instance, out ScopeToken __state)
    {
        __state = default;
        if (__instance == null || HiddenHostsCalls.Stopped) return;
        if (InvokedType == null || InvokedArgs == null)
        {
            ReportMissingField();
            return;
        }

        try
        {
            if (!HiddenHostsTooltipFrames.IsFrameType(InvokedType(__instance))) return;
            var args = InvokedArgs(__instance);
            if (args == null || args.Length == 0) return;
            __state = HiddenHostsTooltipFrames.Open(args[0], nameof(PropertyBasedTooltipVM_OnPeriodicRefresh_HiddenHosts_Patch));
        }
        catch (Exception ex)
        {
            HiddenHostsCalls.ReportFault(nameof(PropertyBasedTooltipVM_OnPeriodicRefresh_HiddenHosts_Patch), ex);
        }
    }

    [HarmonyFinalizer]
    public static void Finalizer(ScopeToken __state) => HiddenHostsTooltipScope.Close(in __state);

    private static void ReportMissingField()
    {
        if (_bindReported) return;
        _bindReported = true;
        // #1185: the hook does nothing from here on, and one fault never spends the budget, so report it here.
        SelfDisables.Report(HiddenHostsModule.PatchCategory, "TooltipBaseVM lacks _invokedType or _invokedArgs");
        HiddenHostsCalls.ReportFault(nameof(PropertyBasedTooltipVM_OnPeriodicRefresh_HiddenHosts_Patch),
            new MissingFieldException("TooltipBaseVM lacks _invokedType or _invokedArgs"));
    }
}
