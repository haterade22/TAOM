using System.Reflection;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;

namespace TAOM.Features.Execution.Hooks;

[HarmonyPatchCategory("Patch14_Execution")]
public static class KillCharacterAction_ApplyInternal_Patch
{
    public static MethodBase TargetMethod()
    {
        return AccessTools.Method(typeof(KillCharacterAction), "ApplyInternal");
    }

    [HarmonyPrefix]
    public static void Prefix(Hero victim, Hero killer, KillCharacterAction.KillCharacterActionDetail actionDetail)
    {
        if (actionDetail != KillCharacterAction.KillCharacterActionDetail.Executed
            && actionDetail != KillCharacterAction.KillCharacterActionDetail.ExecutionAfterMapEvent)
            return;

        var victimKingdomId = victim?.Clan?.Kingdom?.StringId;
        var executorKingdomId = killer?.Clan?.Kingdom?.StringId;

        ExecutionContext.Set(victimKingdomId, executorKingdomId);
    }

    [HarmonyFinalizer]
    public static void Finalizer()
    {
        ExecutionContext.Clear();
    }
}
