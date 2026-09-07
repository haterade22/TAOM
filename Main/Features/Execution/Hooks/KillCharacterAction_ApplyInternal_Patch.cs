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
    public static void Prefix(
        Hero victim,
        Hero killer,
        KillCharacterAction.KillCharacterActionDetail actionDetail,
        out bool __state)
    {
        __state = false;

        if (actionDetail != KillCharacterAction.KillCharacterActionDetail.Executed
            && actionDetail != KillCharacterAction.KillCharacterActionDetail.ExecutionAfterMapEvent)
            return;

        // Captured here, at the top of ApplyInternal, because the method destroys the victim's clan
        // (nulling Clan.Kingdom) before it fires OnHeroKilled, which is what drives the relation pass.
        __state = ExecutionContext.TrySet(
            victim?.Clan?.Kingdom?.StringId,
            victim?.Culture?.StringId,
            killer?.Clan?.Kingdom?.StringId,
            killer?.Culture?.StringId);
    }

    // ApplyInternal re-enters itself: destroying the victim's clan kills that clan's other heroes
    // (DestroyClanAction.cs:43), and each nested kill runs this finalizer while the outer execution
    // is still on the stack. Harmony's __state is per-invocation, so only the frame that actually
    // took the snapshot clears it.
    [HarmonyFinalizer]
    public static void Finalizer(bool __state)
    {
        ExecutionContext.ClearIfOwned(__state);
    }
}
