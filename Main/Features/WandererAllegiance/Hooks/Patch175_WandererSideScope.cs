using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using TaleWorlds.CampaignSystem.CampaignBehaviors;

namespace TAOM.Features.WandererAllegiance.Hooks;

/// <summary>
/// Patch175, first class of the category: marks the two vanilla routines that place a wanderer in a town, so
/// <see cref="Patch175_WandererSideEnter"/> acts only inside them. Both are private on
/// <c>CompanionsCampaignBehavior</c> (v1.5.7): <c>CreateCompanionAndAddToSettlement</c> (:379-392, reached from the daily spawn
/// and from the new-game fill) and <c>SwapCompanions</c> (:250-276, the daily swap). A void prefix and a finalizer, so vanilla
/// always runs; a void finalizer closes the scope. Named companions, the messenger and every
/// other caller of the same engine action are outside the scope and keep their own placement.
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
