using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Library;
using TAOM.Core.Logging;

namespace TAOM.Features.BardTownGuard.Hooks;

/// <summary>
/// IL surgery for Patch110: rewrites the one <c>GetRandomElementWithPredicate&lt;Settlement&gt;</c> call in the musician's
/// condition in place to call <see cref="BardTownPickHook.PickTown"/>, which has the same signature, so the stack and
/// the labels on the instruction are unchanged. Any count other than one returns the original stream untouched with one
/// warning, never a half-patched method.
/// </summary>
public static class BardTownTranspiler
{
    public static IEnumerable<CodeInstruction> SwapPick(IEnumerable<CodeInstruction> instructions, IModLogger? logger)
    {
        var list = instructions.ToList();
        var sites = list.Where(IsPick).ToList();
        if (sites.Count != 1)
        {
            logger?.LogWarning($"[BardTownGuard] expected exactly one GetRandomElementWithPredicate<Settlement> call, found {sites.Count}: the musician fix is not applied");
            return list;
        }

        sites[0].opcode = OpCodes.Call;
        sites[0].operand = AccessTools.Method(typeof(BardTownPickHook), nameof(BardTownPickHook.PickTown));
        return list;
    }

    private static bool IsPick(CodeInstruction ci) =>
        (ci.opcode == OpCodes.Call || ci.opcode == OpCodes.Callvirt)
        && ci.operand is MethodInfo mi
        && mi.Name == "GetRandomElementWithPredicate"
        && mi.IsGenericMethod
        && mi.GetGenericArguments().SequenceEqual(new[] { typeof(Settlement) })
        && mi.GetParameters().Length == 2
        && mi.GetParameters()[0].ParameterType == typeof(MBReadOnlyList<Settlement>);
}
