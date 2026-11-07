using System;
using System.Collections.Generic;
using HarmonyLib;
using TAOM.Core.Logging;
using TAOM.Features.BannerColorPersistence.Hooks;
using TAOM.Features.SettlementGuards;
using TAOM.Features.SettlementGuards.Hooks;
using TaleWorlds.MountAndBlade.ViewModelCollection.OrderOfBattle;

namespace TAOM;

/// <summary>
/// Applies the Harmony patches that target PRIVATE engine methods and therefore cannot use
/// <c>[HarmonyPatch]</c> attribute binding + <c>PatchCategory</c>: each resolves its target via
/// <c>AccessTools</c> at apply time and, when the target is not found, logs a warning and keeps
/// vanilla behaviour. A throw from <c>harmony.Patch</c> or from resolving the target is contained
/// per patch by <see cref="TryPatch"/>, logged as a <c>[PatchApply]</c> error and returned by name
/// for the game-init failure notice, like a failed category: it would otherwise leave through
/// <c>MBGameManager</c>'s submodule loop, which has no
/// catch on v1.4.8, and skip the Harmony census after it. Extracted from
/// <c>SubModule.OnGameInitializationFinished</c> (ADR-002); called once per process from that same
/// callback, order unchanged.
/// </summary>
internal static class ManualPatchApplicator
{
    /// <summary>Runs one manual patch; false when it threw (logged, label added to
    /// <paramref name="failed"/>, never rethrown).</summary>
    internal static bool TryPatch(string label, IModLogger logger, ICollection<string> failed, Action apply)
    {
        try
        {
            apply();
            return true;
        }
        catch (Exception ex)
        {
            failed.Add(label);
            logger.LogError($"[PatchApply] manual patch {label} FAILED: {ex}");
            return false;
        }
    }

    /// <summary>Applies every manual patch; returns the labels of those that threw. A missing target
    /// is a warning, not a failure, so it is not among them.</summary>
    internal static IReadOnlyList<string> ApplyAll(Harmony harmony)
    {
        var logger = IoC.Resolve<IModLogger>();
        var failed = new List<string>();

        // CompanionTactics — manual patch for the PRIVATE method
        // OrderOfBattleHeroItemVM.GetCaptainTooltip (private in v1.3.15, can't use
        // [HarmonyPatch] attribute binding).
        TryPatch("[CompanionTactics] GetCaptainTooltip", logger, failed, () =>
        {
            var captainTooltipTarget = AccessTools.Method(typeof(OrderOfBattleHeroItemVM), "GetCaptainTooltip");
            if (captainTooltipTarget != null)
                harmony.Patch(captainTooltipTarget, postfix: new HarmonyMethod(
                    typeof(Features.CompanionTactics.Roles.Hooks.Patch35_OOBHeroItem_GetCaptainTooltip),
                    nameof(Features.CompanionTactics.Roles.Hooks.Patch35_OOBHeroItem_GetCaptainTooltip.Postfix)));
            else
                logger.LogWarning("[CompanionTactics] OrderOfBattleHeroItemVM.GetCaptainTooltip not found — captain tooltip role hint will not appear");
        });

        // The three guard patches read this service; without it they stay off.
        var guardsReady = TryPatch("[SettlementGuards] Initialize", logger, failed, () =>
        {
            var settlementGuardService = IoC.Resolve<ISettlementGuardService>();
            GuardsCampaignBehavior_TakeGuardAgentData_Patch.Initialize(settlementGuardService);
            GuardsCampaignBehavior_GetSuitableSpear_Patch.Initialize(settlementGuardService);
            GuardsCampaignBehavior_InitializeGarrisonCharacters_Patch.Initialize(settlementGuardService);
        });

        if (guardsReady)
        {
            // Manual patches for private GuardsCampaignBehavior methods (SandBox.dll)
            TryPatch("[SettlementGuards] TakeGuardAgentData", logger, failed, () =>
            {
                var takeGuardTarget = GuardsCampaignBehavior_TakeGuardAgentData_Patch.TargetMethod();
                if (takeGuardTarget != null)
                    harmony.Patch(takeGuardTarget, prefix: new HarmonyMethod(
                        typeof(GuardsCampaignBehavior_TakeGuardAgentData_Patch),
                        nameof(GuardsCampaignBehavior_TakeGuardAgentData_Patch.Prefix)));
                else
                    logger.LogWarning("[SettlementGuards] TakeGuardAgentDataFromGarrisonTroopList not found — custom guards will not apply");
            });

            TryPatch("[SettlementGuards] GetSuitableSpear", logger, failed, () =>
            {
                var spearTarget = GuardsCampaignBehavior_GetSuitableSpear_Patch.TargetMethod();
                if (spearTarget != null)
                    harmony.Patch(spearTarget, prefix: new HarmonyMethod(
                        typeof(GuardsCampaignBehavior_GetSuitableSpear_Patch),
                        nameof(GuardsCampaignBehavior_GetSuitableSpear_Patch.Prefix)));
                else
                    logger.LogWarning("[SettlementGuards] GetSuitableSpear not found — culture-specific spears will not apply");
            });

            TryPatch("[SettlementGuards] InitializeGarrisonCharacters", logger, failed, () =>
            {
                var garrisonScrubTarget = GuardsCampaignBehavior_InitializeGarrisonCharacters_Patch.TargetMethod();
                if (garrisonScrubTarget != null)
                    harmony.Patch(garrisonScrubTarget, postfix: new HarmonyMethod(
                        typeof(GuardsCampaignBehavior_InitializeGarrisonCharacters_Patch),
                        nameof(GuardsCampaignBehavior_InitializeGarrisonCharacters_Patch.Postfix)));
                else
                    logger.LogWarning("[SettlementGuards] InitializeGarrisonCharacters not found — excluded-race garrison scrub will not apply (#346)");
            });
        }

        // Manual patch for private MobilePartyVisual method (SandBox.View.dll)
        TryPatch("[BannerColor] AddCharacterToPartyIcon", logger, failed, () =>
        {
            var mobilePartyTarget = MobilePartyVisual_AddCharacterToPartyIcon_Patch.TargetMethod();
            if (mobilePartyTarget != null)
                harmony.Patch(mobilePartyTarget, postfix: new HarmonyMethod(
                    typeof(MobilePartyVisual_AddCharacterToPartyIcon_Patch),
                    nameof(MobilePartyVisual_AddCharacterToPartyIcon_Patch.Postfix)));
            else
                logger.LogWarning("[BannerColor] MobilePartyVisual.AddCharacterToPartyIcon not found — party icon colors will not persist");
        });

        // Manual patch for AgentVisuals.Create (TaleWorlds.MountAndBlade.View.dll)
        TryPatch("[BannerColor] AgentVisuals.Create", logger, failed, () =>
        {
            var agentVisualsCreateTarget = AgentVisuals_Create_Patch.TargetMethod();
            if (agentVisualsCreateTarget != null)
                harmony.Patch(agentVisualsCreateTarget, prefix: new HarmonyMethod(
                    typeof(AgentVisuals_Create_Patch),
                    nameof(AgentVisuals_Create_Patch.Prefix)));
            else
                logger.LogWarning("[BannerColor] AgentVisuals.Create not found — clan color randomness suppression will not apply");
        });

        // Manual patches for MapConversationTableau (private methods in SandBox.View.dll)
        TryPatch("[BannerColor] SpawnOpponentLeader", logger, failed, () =>
        {
            var leaderTarget = MapConversationTableau_SpawnOpponentLeader_Patch.TargetMethod();
            if (leaderTarget != null)
                harmony.Patch(leaderTarget, postfix: new HarmonyMethod(
                    typeof(MapConversationTableau_SpawnOpponentLeader_Patch),
                    nameof(MapConversationTableau_SpawnOpponentLeader_Patch.Postfix)));
            else
                logger.LogWarning("[BannerColor] MapConversationTableau.SpawnOpponentLeader not found — conversation tableau leader colors will not apply");
        });

        TryPatch("[BannerColor] SpawnOpponentBodyguard", logger, failed, () =>
        {
            var bodyguardTarget = MapConversationTableau_SpawnOpponentBodyguard_Patch.TargetMethod();
            if (bodyguardTarget != null)
                harmony.Patch(bodyguardTarget, postfix: new HarmonyMethod(
                    typeof(MapConversationTableau_SpawnOpponentBodyguard_Patch),
                    nameof(MapConversationTableau_SpawnOpponentBodyguard_Patch.Postfix)));
            else
                logger.LogWarning("[BannerColor] MapConversationTableau.SpawnOpponentBodyguardCharacter not found — conversation tableau bodyguard colors will not apply");
        });

        return failed;
    }
}
