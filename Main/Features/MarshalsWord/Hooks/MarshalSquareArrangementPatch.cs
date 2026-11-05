using HarmonyLib;
using TaleWorlds.MountAndBlade;

namespace TAOM.Features.MarshalsWord.Hooks;

/// <summary>
/// Patch141 (docs/features/marshals-word.md, build-spec 9.1): the hollow square. Vanilla's square order builds a <c>RectilinearSchiltronFormation</c>, which packs itself
/// and ignores width; the plain engine <c>SquareFormation</c> keeps a chosen side. While Marshal's Word is live and the setting holds, a Square order on a company of the
/// player's team that is not under AI control gets the plain class. A simulation copy takes its arrangement through <c>Clone</c>, so it never reaches this prefix.
/// </summary>
[HarmonyPatch(typeof(ArrangementOrder), nameof(ArrangementOrder.GetArrangement))]
[HarmonyPatchCategory(MarshalsWordModule.PatchCategory)]
public static class MarshalSquareArrangementPatch
{
    [HarmonyPrefix]
    public static bool Prefix(ref ArrangementOrder __instance, Formation formation, ref IFormationArrangement __result)
    {
        if (!MarshalHooks.IsLive || !MarshalHooks.HollowSquare || __instance.OrderEnum != ArrangementOrder.ArrangementOrderEnum.Square)
            return true;
        var team = formation?.Team;
        if (team == null || team != Mission.Current?.PlayerTeam || formation!.IsAIControlled)
            return true;
        __result = new SquareFormation(formation);
        return false;
    }
}
