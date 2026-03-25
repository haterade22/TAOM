using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CampaignBehaviors;

namespace TAOM.Features.Diplomacy.Hooks;

[HarmonyPatch(typeof(AllianceCampaignBehavior), nameof(AllianceCampaignBehavior.EndAlliance))]
[HarmonyPatchCategory("Patch11_Diplomacy")]
public static class AllianceCampaignBehavior_EndAlliance_Patch
{
    private static IOnAllianceAction _hook;

    public static void Initialize(IOnAllianceAction hook)
    {
        _hook = hook;
    }

    [HarmonyPrefix]
    public static bool Prefix(Kingdom kingdom1, Kingdom kingdom2)
    {
        if (_hook == null) return true;

        if (_hook.ShouldPreventAllianceEnd(kingdom1.StringId, kingdom2.StringId))
        {
            return false;
        }

        return true;
    }
}
