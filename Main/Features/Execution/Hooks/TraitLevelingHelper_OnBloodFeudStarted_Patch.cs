using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CharacterDevelopment;

namespace TAOM.Features.Execution.Hooks;

// Suppresses the execution trait penalty when the player executes a lord of an ENEMY alignment,
// so putting an orc captain to the sword does not cost the same Honor as murdering a fellow man.
//
// v1.5.0 migration. The old target, TraitLevelingHelper.OnLordExecuted(), was deleted outright
// along with the whole ExecutionRelationModel (zero references remain anywhere in the engine).
// Its successor is OnBloodFeudStarted(Hero), called from BloodFeudCampaignBehavior's
// OnPlayerExecutedHero, and it applies the same penalties vanilla used to apply inline:
// Honor -1000 when the victim is honorable, otherwise Mercy -500, plus Mercy -500 either way.
//
// Two consequences of the new shape, both deliberate:
//   1. The victim arrives as a PARAMETER, so the ThreadLocal ExecutionContext shim that used to
//      carry it across from KillCharacterAction.ApplyInternal is gone. Both it and that prefix
//      were deleted with this change; nothing else read them.
//   2. The executor is always the main hero, because the only caller is the player-execution
//      path. Vanilla gates that call on !victim.Clan.HasBloodFeudWithPlayer, so the penalty is
//      now once per clan rather than once per execution. That is vanilla's behaviour, not ours.
//
// Only the TRAIT hit is suppressed. The blood feud itself still starts, so the victim's clan
// still hunts the player and the 1.5.0 feud system stays intact for every faction.
[HarmonyPatch(typeof(TraitLevelingHelper), nameof(TraitLevelingHelper.OnBloodFeudStarted))]
[HarmonyPatchCategory("Patch14_Execution")]
public static class TraitLevelingHelper_OnBloodFeudStarted_Patch
{
    private static IOnExecutionAction _hook;

    public static void Initialize(IOnExecutionAction hook)
    {
        _hook = hook;
    }

    [HarmonyPrefix]
    public static bool Prefix(Hero executedHero)
    {
        if (_hook == null) return true;

        var victimKingdomId = executedHero?.Clan?.Kingdom?.StringId;
        var executorKingdomId = Hero.MainHero?.Clan?.Kingdom?.StringId;

        // Fall through to vanilla when either kingdom is null or unknown (an independent player,
        // a clanless victim): alignment only means something when both sides are known.
        if (string.IsNullOrEmpty(victimKingdomId) || string.IsNullOrEmpty(executorKingdomId))
            return true;

        return _hook.ShouldApplyHonorPenalty(victimKingdomId, executorKingdomId);
    }
}
