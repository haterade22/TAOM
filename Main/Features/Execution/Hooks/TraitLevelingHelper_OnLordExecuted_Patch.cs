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

        // No execution in flight on this thread means something other than our patched path called
        // OnLordExecuted — leave vanilla alone rather than guess at the participants.
        if (!ExecutionContext.HasContext) return true;

        var victim = new ExecutionParticipant(
            ExecutionContext.GetVictimKingdomId(),
            ExecutionContext.GetVictimCultureId());
        var executor = new ExecutionParticipant(
            ExecutionContext.GetExecutorKingdomId(),
            ExecutionContext.GetExecutorCultureId());

        // Skip the vanilla -1000 Honor XP when this was a cross-alignment kill.
        return _hook.ShouldApplyHonorPenalty(victim, executor);
    }
}
