using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using TaleWorlds.CampaignSystem.CampaignBehaviors;

namespace TAOM.Features.WandererAllegiance.Hooks;

/// <summary>
/// Patch175, first class of the category: marks the vanilla routines that place an unhired wanderer in a town, so
/// <see cref="Patch175_WandererSideEnter"/> acts only inside them. All are private (v1.5.7): on
/// <c>CompanionsCampaignBehavior</c>, <c>CreateCompanionAndAddToSettlement</c> (:379-392, the daily spawn and the new-game fill)
/// and <c>SwapCompanions</c> (:250-276, the daily swap); on <c>HeroSpawnCampaignBehavior</c>, <c>OnHeroDailyTick</c> (:120-142,
/// teleports a fired or released wanderer) and <c>OnCompanionRemoved</c> (:94-100, the same teleport on dismissal). A void prefix
/// and a finalizer, so vanilla always runs; a void finalizer closes the scope. The Enter prefix only redirects an unhired
/// wanderer entering with no party, so lords, notables and prisoners pass through untouched. Every other caller of the same
/// engine action is outside the scope and keeps its own placement.
/// Callers, thread, PatchShield and co-op notes: docs/reference/harmony-patch-registry.md "Patch175_WandererSideTowns".
/// </summary>
[HarmonyPatch]
[HarmonyPatchCategory(Patch175_WandererSideEnter.Category)]
public static class Patch175_WandererSideScope
{
    [HarmonyTargetMethods]
    public static IEnumerable<MethodBase?> TargetMethods()
    {
        yield return AccessTools.Method(typeof(CompanionsCampaignBehavior), "CreateCompanionAndAddToSettlement");
        yield return AccessTools.Method(typeof(CompanionsCampaignBehavior), "SwapCompanions");
        yield return AccessTools.Method(typeof(HeroSpawnCampaignBehavior), "OnHeroDailyTick");
        yield return AccessTools.Method(typeof(HeroSpawnCampaignBehavior), "OnCompanionRemoved");
    }

    [HarmonyPrefix]
    public static void Prefix() => Patch175_WandererSideEnter.Depth++;

    [HarmonyFinalizer]
    public static void Finalizer()
    {
        if (Patch175_WandererSideEnter.Depth > 0)
            Patch175_WandererSideEnter.Depth--;
    }
}
