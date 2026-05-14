using HarmonyLib;
using TAOM.Core.Logging;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CampaignBehaviors;

namespace TAOM.Features.Diplomacy.Hooks;

[HarmonyPatch(typeof(AllianceCampaignBehavior), nameof(AllianceCampaignBehavior.EndAlliance))]
[HarmonyPatchCategory("Patch11_Diplomacy")]
public static class AllianceCampaignBehavior_EndAlliance_Patch
{
    private static IOnAllianceAction _hook;
    private static IModLogger _logger;

    public static void Initialize(IOnAllianceAction hook)
    {
        _hook = hook;
    }

    public static void Initialize(IModLogger logger)
    {
        _logger = logger;
    }

    [HarmonyPrefix]
    public static bool Prefix(Kingdom kingdom1, Kingdom kingdom2)
    {
        if (_hook == null)
        {
            _logger?.LogWarning("[Diplomacy] AllianceCampaignBehavior_EndAlliance_Patch: hook not initialized");
            return true;
        }

        if (_hook.ShouldPreventAllianceEnd(kingdom1.StringId, kingdom2.StringId))
        {
            // Phase 9b #152 — Vanilla callers (OnAllianceTimerExpired, OnWarDeclared) call EndAlliance
            // then AddAllianceDecision in sequence. When we skip EndAlliance, the subsequent
            // AddAllianceDecision queues a "propose new alliance" for kingdoms that are still allied.
            // Mitigation: vanilla AddAllianceDecision (decompiled) checks IsAlliedWith before
            // queuing the decision, so the duplicate is filtered at that layer. We log for
            // diagnostic visibility — if downstream "duplicate proposal" reports surface, the
            // mitigation has gapped and we need a Patch15 on AddAllianceDecision to filter.
            _logger?.LogDebug($"[Diplomacy] EndAlliance blocked: {kingdom1.StringId} ↔ {kingdom2.StringId}. " +
                              "Downstream AddAllianceDecision is expected to short-circuit on IsAlliedWith.");
            return false;
        }

        return true;
    }
}
