using HarmonyLib;
using TaleWorlds.CampaignSystem.CharacterDevelopment;

namespace TAOM.Features.Execution.Hooks;

[HarmonyPatch(typeof(TraitLevelingHelper), nameof(TraitLevelingHelper.OnLordExecuted))]
[HarmonyPatchCategory("Patch14_Execution")]
public static class TraitLevelingHelper_OnLordExecuted_Patch
{
    private static IOnExecutionAction _hook;

    public static void Initialize(IOnExecutionAction hook)
    {
        _hook = hook;
    }

    [HarmonyPrefix]
    public static bool Prefix()
    {
        if (_hook == null) return true;
        if (!ExecutionContext.HasContext) return true;

        var victimKingdomId = ExecutionContext.GetVictimKingdomId();
        var executorKingdomId = ExecutionContext.GetExecutorKingdomId();

        if (!_hook.ShouldApplyHonorPenalty(victimKingdomId, executorKingdomId))
            return false;

        return true;
    }
}
