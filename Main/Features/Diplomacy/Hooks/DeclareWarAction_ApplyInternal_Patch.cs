using System.Reflection;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;

namespace TAOM.Features.Diplomacy.Hooks;

[HarmonyPatchCategory("Patch11_Diplomacy")]
public static class DeclareWarAction_ApplyInternal_Patch
{
    private static IOnAllianceAction _hook;

    public static void Initialize(IOnAllianceAction hook)
    {
        _hook = hook;
    }

    public static MethodBase TargetMethod()
    {
        return AccessTools.Method(typeof(DeclareWarAction), "ApplyInternal");
    }

    [HarmonyPrefix]
    public static bool Prefix(IFaction faction1, IFaction faction2)
    {
        if (_hook == null) return true;

        if (!faction1.IsKingdomFaction || !faction2.IsKingdomFaction)
            return true;

        if (_hook.ShouldPreventWarDeclaration(faction1.StringId, faction2.StringId))
        {
            return false;
        }

        return true;
    }
}
