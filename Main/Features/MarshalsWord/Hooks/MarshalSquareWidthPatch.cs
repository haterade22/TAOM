using HarmonyLib;
using TaleWorlds.MountAndBlade;
using TAOM.Features.MarshalsWord.Domain;

namespace TAOM.Features.MarshalsWord.Hooks;

/// <summary>
/// Patch141 (build-spec 9.1): <c>Formation.CalculateDesiredWidth</c> builds a line's flank width from the desired file count, which on a square means something else
/// (<c>SetFormOrder</c> writes that field from the custom width). For the plain engine square the width is its own side, clamped to the arrangement's bounds. It tests the
/// arrangement type alone and reads only the formation it is given, and the proof line shows the side right after the order, because a patch on a method the JIT inlined
/// never runs.
/// </summary>
[HarmonyPatch(typeof(Formation), "CalculateDesiredWidth")]
[HarmonyPatchCategory(MarshalsWordModule.PatchCategory)]
public static class MarshalSquareWidthPatch
{
    [HarmonyPrefix]
    public static bool Prefix(Formation __instance, ref float __result)
    {
        var arrangement = __instance.Arrangement;
        if (arrangement == null || arrangement.GetType() != typeof(SquareFormation))
            return true;
        if (!MarshalSquareRules.TryDesiredWidth(arrangement.Width, arrangement.MinimumWidth, arrangement.MaximumWidth, out var side))
            return true;
        __result = side;
        return false;
    }
}
