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
            // Audit Agent 2 (2026-05-22) — corrected v1.4.5 behavior:
            // The earlier "Phase 9b #152" comment claimed vanilla AddAllianceDecision
            // short-circuits on IsAlliedWith. Verified against the v1.4.5 decompile: NOT
            // TRUE — vanilla just removes any existing StartAllianceDecision then adds a
            // fresh one. The companion AllianceCampaignBehavior_AddAllianceDecision_Patch
            // now provides that gate explicitly so duplicate decisions don't accumulate
            // on Permanent-lore pairs after their alliance's natural EndTime passes.
            _logger?.LogDebug($"[Diplomacy] EndAlliance blocked: {kingdom1.StringId} ↔ {kingdom2.StringId}. " +
                              "Companion AddAllianceDecision_Patch will suppress the redundant downstream queuing.");
            return false;
        }

        return true;
    }
}
