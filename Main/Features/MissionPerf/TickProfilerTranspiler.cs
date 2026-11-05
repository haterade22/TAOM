using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using TAOM.Core.Logging;

namespace TAOM.Features.MissionPerf;

/// <summary>One call-site swap: every <c>call</c>/<c>callvirt</c> of <see cref="Target"/> becomes a static
/// <c>call</c> of a helper with the same evaluation stack. For an instance target the helper's parameters are
/// the target's declaring type followed by the target's own parameters; for a static target they are the
/// target's parameters exactly. <see cref="Helpers"/> holds one helper per occurrence, in stream order, so a
/// target that occurs N times needs N helpers (plan 041: the four <c>TWParallel.For</c> blocks).</summary>
internal sealed class CallSwap
{
    public CallSwap(MethodInfo target, MethodInfo helper) : this(target, new[] { helper }) { }

    public CallSwap(MethodInfo target, IReadOnlyList<MethodInfo> helpersByOccurrence)
    {
        Target = target;
        Helpers = helpersByOccurrence;
    }

    public MethodInfo Target { get; }
    public IReadOnlyList<MethodInfo> Helpers { get; }
}

/// <summary>
/// The pure IL rewrite behind Patch97. Each swap must match exactly as many times as it has helpers
/// (once, for a one-helper swap); otherwise, or when a helper does not fit its target, the whole method is
/// returned unmodified with one warning (soft-fail: a re-applied category or an engine bump degrades to
/// vanilla, never to a crash; the rerun on its own output finds no target and bails the same way). A swap
/// mutates the existing instruction's opcode and operand in place, so its labels and exception blocks stay
/// attached; no instruction is inserted or removed and no branch changes. Never throws.
/// </summary>
internal static class TickProfilerTranspiler
{
    internal static List<CodeInstruction> Rewrite(
        IEnumerable<CodeInstruction> instructions,
        IReadOnlyList<CallSwap> swaps,
        string methodLabel,
        IModLogger? logger,
        out int swapped)
    {
        swapped = 0;
        var list = new List<CodeInstruction>(instructions);
        try
        {
            foreach (var swap in swaps)
            {
                if (!AllFit(swap, out var misfit))
                {
                    logger?.LogWarning(TickProfileLines.BuildHelperMismatchWarning(
                        methodLabel, Label(misfit), Label(swap?.Target)));
                    return list;
                }
            }

            var sites = new List<int>[swaps.Count];
            for (var s = 0; s < swaps.Count; s++)
            {
                sites[s] = new List<int>(swaps[s].Helpers.Count);
                for (var i = 0; i < list.Count; i++)
                {
                    if (list[i].Calls(swaps[s].Target))
                        sites[s].Add(i);
                }
                if (sites[s].Count != swaps[s].Helpers.Count)
                {
                    logger?.LogWarning(TickProfileLines.BuildSiteCountWarning(methodLabel, Label(swaps[s].Target),
                        sites[s].Count, swaps[s].Helpers.Count));
                    return list;
                }
            }

            var total = 0;
            for (var s = 0; s < swaps.Count; s++)
            {
                for (var k = 0; k < sites[s].Count; k++)
                {
                    list[sites[s][k]].opcode = OpCodes.Call;
                    list[sites[s][k]].operand = swaps[s].Helpers[k];
                    total++;
                }
            }
            swapped = total;
            return list;
        }
        catch (Exception ex)
        {
            swapped = 0;
            try { logger?.LogWarning(TickProfileLines.BuildRewriteFault(methodLabel, ex)); }
            catch { /* diagnostic only */ }
            return new List<CodeInstruction>(list);
        }
    }

    /// <summary>Whether every helper fits the swap's target (and there is at least one); otherwise the
    /// first helper that does not, for the warning.</summary>
    private static bool AllFit(CallSwap? swap, out MethodInfo? misfit)
    {
        misfit = swap?.Helpers?.Count > 0 ? swap.Helpers[0] : null;
        if (swap?.Target == null || swap.Helpers == null || swap.Helpers.Count == 0)
            return false;
        foreach (var helper in swap.Helpers)
        {
            misfit = helper;
            if (!Fits(swap.Target, helper))
                return false;
        }
        misfit = null;
        return true;
    }

    private static bool Fits(MethodInfo target, MethodInfo? helper)
    {
        if (helper == null || !helper.IsStatic || helper.ReturnType != target.ReturnType)
            return false;
        var targetParams = target.GetParameters();
        var helperParams = helper.GetParameters();
        var offset = target.IsStatic ? 0 : 1;
        if (helperParams.Length != targetParams.Length + offset)
            return false;
        if (!target.IsStatic && helperParams[0].ParameterType != target.DeclaringType)
            return false;
        for (var i = 0; i < targetParams.Length; i++)
        {
            if (helperParams[i + offset].ParameterType != targetParams[i].ParameterType)
                return false;
        }
        return true;
    }

    private static string Label(MethodInfo? method) =>
        method == null ? "(null)" : (method.DeclaringType?.Name ?? "?") + "." + method.Name;
}
