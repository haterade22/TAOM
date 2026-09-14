using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TAOM.Adapters;

namespace TAOM.Features.Execution.Hooks;

/// <summary>
/// The relation half of the alignment rule, on the seam that replaced <c>ExecutionRelationModel</c>.
///
/// <para>
/// v1.5.0 deleted that model engine-wide and <c>TaomExecutionRelationModel</c> with it. The engine
/// now splits execution fallout in two, and only ONE half is touched here. The feud itself
/// (<c>OnBloodFeudStateChanged</c> setting the victim clan's relation to the minimum) is left alone:
/// a clan whose kinsman you beheaded is entitled to hunt you whatever side either of you is on.
/// What is modified is the loop over every other clan that follows, which calls this static method
/// per clan and applies any non-zero result. Returning zero trips the engine's own <c>!= 0</c>
/// guard, so it is no hit and no count in the summary notice, not a smaller hit.
/// </para>
///
/// <para>
/// Patching the method rather than the loop keeps the pre-execution confirmation honest: the same
/// method feeds the "this will hurt your relations with N clans" tooltip, so the warning and the
/// outcome cannot disagree.
/// </para>
///
/// <para>
/// The method is reached on two paths. When the PLAYER executes a hero (the application loop in
/// <c>OnBloodFeudStateChanged</c> and the pre-execution tooltip) the executor is the main hero and
/// the rule applies. When an AI clan executes a member of the player's clan
/// (<c>OnPlayerClanMemberExecuted</c>), vanilla starts the feud the other way round and still runs
/// the same loop, charging the player's relations with third clans for a kill the player did not
/// commit. The signature carries no executor and the alignment rule has no verdict on the player
/// being the bereaved, so that path keeps vanilla's number. The victim's clan being the player's
/// clan is the discriminator, and the hook decides it.
/// </para>
///
/// <para>
/// Vanilla reaches the loop only while the feud clan still has a kingdom, so the live
/// <c>dyingHero.Clan.Kingdom</c> read here is populated on the player-executes path. The participant
/// carries the culture regardless, and the service resolves sides with a culture fallback and no
/// early return, so a kingdom-less executor (independent or enlisted player, mercenary clan) is
/// placed on a side rather than handing the calculation back to vanilla's flat penalty.
/// </para>
/// </summary>
[HarmonyPatch(typeof(ExecutionCampaignBehavior),
    nameof(ExecutionCampaignBehavior.GetBloodFeudStartRelationPenaltyToOtherClan))]
[HarmonyPatchCategory("Patch14_Execution")]
public static class ExecutionCampaignBehavior_BloodFeudRelationPenalty_Patch
{
    private static IOnExecutionAction _hook;
    private static IPlayerContextAdapter _playerContext;

    public static void Initialize(IOnExecutionAction hook, IPlayerContextAdapter playerContext)
    {
        _hook = hook;
        _playerContext = playerContext;
    }

    [HarmonyPostfix]
    public static void Postfix(Hero dyingHero, Clan otherClan, ref int __result)
    {
        var hook = _hook;
        var playerContext = _playerContext;
        if (hook == null || playerContext == null || __result == 0) return;
        if (hook.IsPlayerTheBereaved(dyingHero?.Clan?.StringId, playerContext.GetPlayerClanId())) return;

        // Past the bereaved gate the executor is the player: the tooltip and the application loop
        // both run for the main hero's own executions.
        var executor = new ExecutionParticipant(
            playerContext.GetPlayerKingdomId(),
            playerContext.GetPlayerCultureId());
        var victim = new ExecutionParticipant(
            dyingHero?.Clan?.Kingdom?.StringId,
            dyingHero?.Culture?.StringId);
        var evaluator = new ExecutionParticipant(
            otherClan?.Kingdom?.StringId,
            otherClan?.Culture?.StringId);

        __result = hook.GetRelationModifier(executor, victim, evaluator, __result);
    }
}
