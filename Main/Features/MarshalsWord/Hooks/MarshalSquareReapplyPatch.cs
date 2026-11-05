using HarmonyLib;
using TaleWorlds.MountAndBlade;
using TAOM.Features.MarshalsWord.Domain;

namespace TAOM.Features.MarshalsWord.Hooks;

/// <summary>
/// Patch141 (build-spec 9.1): <c>Formation.ReapplyFormOrder</c> copies <c>Arrangement.FlankWidth</c> into the custom width, and a square's flank width is its perimeter,
/// so the square would grow each time a unit joins or leaves. For the plain engine square this keeps the side instead, grown to the arrangement's minimum so no
/// soldier is left without a slot. It tests the arrangement type alone, so every other arrangement runs vanilla's code, and it can run on either thread, so it reads and
/// writes only the formation it is given.
/// </summary>
[HarmonyPatch(typeof(Formation), "ReapplyFormOrder")]
[HarmonyPatchCategory(MarshalsWordModule.PatchCategory)]
public static class MarshalSquareReapplyPatch
{
    [HarmonyPrefix]
    public static bool Prefix(Formation __instance)
    {
        var arrangement = __instance.Arrangement;
        if (arrangement == null || arrangement.GetType() != typeof(SquareFormation))
            return true;
        var formOrder = __instance.FormOrder;
        if (formOrder.OrderEnum != FormOrder.FormOrderEnum.Custom || __instance.ArrangementOrder.OrderEnum == ArrangementOrder.ArrangementOrderEnum.Circle)
            return true;
        if (!MarshalSquareRules.TryKeptSide(arrangement.Width, arrangement.MinimumWidth, out var side))
            return true;
        formOrder.CustomFlankWidth = side;
        __instance.SetFormOrder(formOrder, false);
        return false;
    }
}
