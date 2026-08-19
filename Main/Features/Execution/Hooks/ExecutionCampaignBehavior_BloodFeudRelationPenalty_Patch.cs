using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CampaignBehaviors;

namespace TAOM.Features.Execution.Hooks;

/// <summary>
/// Restores TAOM's alignment rule to the relation half of executions, re-homed onto v1.5.0's Blood
/// Feud system.
///
/// <para>
/// Putting an orc captain to the sword should not cost a Gondorian standing with the free peoples.
/// Until v1.5.0 that rule lived in <c>TaomExecutionRelationModel</c>, overriding vanilla's
/// <c>ExecutionRelationModel</c>. v1.5.0 deleted that model engine-wide, so the override had no base
/// class and was removed with it. This patch puts the same rule back on the seam that replaced it.
/// </para>
///
/// <para>
/// v1.5.0 splits execution fallout in two, and only ONE half is suppressed here:
/// </para>
/// <list type="number">
///   <item>The feud itself. <c>OnBloodFeudStateChanged</c> sets the victim clan's relation with the
///   player to the minimum and starts a blood feud. That is LEFT ALONE and should be: a clan whose
///   kinsman you beheaded is entitled to hunt you regardless of which side either of you is on.</item>
///   <item>Third-party fallout. The same handler then loops every other clan in the world and
///   applies <c>GetBloodFeudStartRelationPenaltyToOtherClan</c>. That is the collective-punishment
///   half, and it is what the alignment rule exists to stop.</item>
/// </list>
///
/// <para>
/// The decision itself is unchanged and still lives in <see cref="IOnExecutionAction"/>: cross
/// alignment means observers who share the EXECUTOR's side (and neutrals) take nothing, while
/// observers who share the VICTIM's side still object. Same alignment means kinslaying and the
/// penalty is amplified. Returning zero makes the engine's own <c>!= 0</c> guard skip the clan
/// entirely, so it is not merely a smaller hit, it is no hit and no notification.
/// </para>
///
/// <para>
/// Patching the model method rather than the apply loop also keeps the pre-execution confirmation
/// hint honest: the same method feeds the "this will hurt your relations with N clans" tooltip, so
/// the warning and the outcome cannot disagree.
/// </para>
/// </summary>
[HarmonyPatch(typeof(ExecutionCampaignBehavior),
    nameof(ExecutionCampaignBehavior.GetBloodFeudStartRelationPenaltyToOtherClan))]
[HarmonyPatchCategory("Patch14_Execution")]
public static class ExecutionCampaignBehavior_BloodFeudRelationPenalty_Patch
{
    private static IOnExecutionAction? _hook;

    public static void Initialize(IOnExecutionAction hook)
    {
        _hook = hook;
    }

    [HarmonyPostfix]
    public static void Postfix(Hero dyingHero, Clan otherClan, ref int __result)
    {
        if (_hook == null || __result == 0) return;

        // Only the player executes through this path, so the executor is always the main hero.
        var executorKingdomId = Hero.MainHero?.Clan?.Kingdom?.StringId;
        var victimKingdomId = dyingHero?.Clan?.Kingdom?.StringId;
        var evaluatorKingdomId = otherClan?.Kingdom?.StringId;

        // Fall through to vanilla when a side is unknown: alignment only means something when both
        // the executor and the victim have a resolvable kingdom. An independent player, a clanless
        // victim or a bandit clan keeps whatever the engine decided.
        if (string.IsNullOrEmpty(executorKingdomId) || string.IsNullOrEmpty(victimKingdomId)) return;

        __result = _hook.GetRelationModifier(
            executorKingdomId, victimKingdomId, evaluatorKingdomId, __result);
    }
}
