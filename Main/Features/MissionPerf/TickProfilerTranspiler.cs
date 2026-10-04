using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using TAOM.Core.Logging;

namespace TAOM.Features.MissionPerf;

/// <summary>One call-site swap: every <c>call</c>/<c>callvirt</c> of <see cref="Target"/> becomes a static
/// <c>call</c> of <see cref="Helper"/>, whose parameters are the target's declaring type followed by the
/// target's own parameters, so the evaluation stack is identical.</summary>
internal sealed class CallSwap
{
    public CallSwap(MethodInfo target, MethodInfo helper)
    {
        Target = target;
        Helper = helper;
    }

    public MethodInfo Target { get; }
    public MethodInfo Helper { get; }
}

/// <summary>
/// The pure IL rewrite behind Patch97. Each swap must match exactly once in the method; otherwise, or
/// when a helper does not fit its target, the whole method is returned unmodified with one warning
/// (soft-fail: a re-applied category or an engine bump degrades to vanilla, never to a crash; the
/// rerun on its own output finds no target and bails the same way). A swap mutates the existing
/// instruction's opcode and operand in place, so its labels and exception blocks stay attached; no
/// instruction is inserted or removed and no branch changes. Never throws.
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
                if (!Fits(swap))
                {
                    logger?.LogWarning(TickProfileLines.BuildHelperMismatchWarning(
                        methodLabel, Label(swap?.Helper), Label(swap?.Target)));
                    return list;
                }
            }

            var sites = new int[swaps.Count];
            for (var s = 0; s < swaps.Count; s++)
            {
                var count = 0;
                for (var i = 0; i < list.Count; i++)
                {
                    if (!list[i].Calls(swaps[s].Target))
                        continue;
                    count++;
                    sites[s] = i;
                }
                if (count != 1)
                {
                    logger?.LogWarning(TickProfileLines.BuildSiteCountWarning(methodLabel, Label(swaps[s].Target), count));
                    return list;
                }
            }

            for (var s = 0; s < swaps.Count; s++)
            {
                list[sites[s]].opcode = OpCodes.Call;
                list[sites[s]].operand = swaps[s].Helper;
            }
            swapped = swaps.Count;
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

    private static bool Fits(CallSwap? swap)
    {
        if (swap?.Target == null || swap.Helper == null || !swap.Helper.IsStatic)
            return false;
        if (swap.Helper.ReturnType != swap.Target.ReturnType)
            return false;
        var targetParams = swap.Target.GetParameters();
        var helperParams = swap.Helper.GetParameters();
        if (helperParams.Length != targetParams.Length + 1 || helperParams[0].ParameterType != swap.Target.DeclaringType)
            return false;
        for (var i = 0; i < targetParams.Length; i++)
        {
            if (helperParams[i + 1].ParameterType != targetParams[i].ParameterType)
                return false;
        }
        return true;
    }

    private static string Label(MethodInfo? method) =>
        method == null ? "(null)" : (method.DeclaringType?.Name ?? "?") + "." + method.Name;
}
