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
/// <c>AccessTools</c> at apply time and fail-safes with a logged warning (vanilla behavior) when the
/// engine method has moved. Extracted verbatim from <c>SubModule.OnGameInitializationFinished</c>
/// (ADR-002); called once per process from that same callback, order unchanged.
/// </summary>
internal static class ManualPatchApplicator
{
    internal static void ApplyAll(Harmony harmony)
    {
        // CompanionTactics — manual patch for the PRIVATE method
        // OrderOfBattleHeroItemVM.GetCaptainTooltip (private in v1.3.15, can't use
        // [HarmonyPatch] attribute binding).
        var captainTooltipTarget = AccessTools.Method(typeof(OrderOfBattleHeroItemVM), "GetCaptainTooltip");
        if (captainTooltipTarget != null)
            harmony.Patch(captainTooltipTarget, postfix: new HarmonyMethod(
                typeof(Features.CompanionTactics.Roles.Hooks.Patch35_OOBHeroItem_GetCaptainTooltip),
                nameof(Features.CompanionTactics.Roles.Hooks.Patch35_OOBHeroItem_GetCaptainTooltip.Postfix)));
        else
            IoC.Resolve<IModLogger>().LogWarning("[CompanionTactics] OrderOfBattleHeroItemVM.GetCaptainTooltip not found — captain tooltip role hint will not appear");

        var settlementGuardService = IoC.Resolve<ISettlementGuardService>();
        GuardsCampaignBehavior_TakeGuardAgentData_Patch.Initialize(settlementGuardService);
        GuardsCampaignBehavior_GetSuitableSpear_Patch.Initialize(settlementGuardService);

        // Manual patches for private GuardsCampaignBehavior methods (SandBox.dll)
        var takeGuardTarget = GuardsCampaignBehavior_TakeGuardAgentData_Patch.TargetMethod();
        if (takeGuardTarget != null)
            harmony.Patch(takeGuardTarget, prefix: new HarmonyMethod(
                typeof(GuardsCampaignBehavior_TakeGuardAgentData_Patch),
                nameof(GuardsCampaignBehavior_TakeGuardAgentData_Patch.Prefix)));
        else
            IoC.Resolve<IModLogger>().LogWarning("[SettlementGuards] TakeGuardAgentDataFromGarrisonTroopList not found — custom guards will not apply");

        var spearTarget = GuardsCampaignBehavior_GetSuitableSpear_Patch.TargetMethod();
        if (spearTarget != null)
            harmony.Patch(spearTarget, prefix: new HarmonyMethod(
                typeof(GuardsCampaignBehavior_GetSuitableSpear_Patch),
                nameof(GuardsCampaignBehavior_GetSuitableSpear_Patch.Prefix)));
        else
            IoC.Resolve<IModLogger>().LogWarning("[SettlementGuards] GetSuitableSpear not found — culture-specific spears will not apply");

        // Manual patch for private MobilePartyVisual method (SandBox.View.dll)
        var mobilePartyTarget = MobilePartyVisual_AddCharacterToPartyIcon_Patch.TargetMethod();
        if (mobilePartyTarget != null)
            harmony.Patch(mobilePartyTarget, postfix: new HarmonyMethod(
                typeof(MobilePartyVisual_AddCharacterToPartyIcon_Patch),
                nameof(MobilePartyVisual_AddCharacterToPartyIcon_Patch.Postfix)));
        else
            IoC.Resolve<IModLogger>().LogWarning("[BannerColor] MobilePartyVisual.AddCharacterToPartyIcon not found — party icon colors will not persist");

        // Manual patch for AgentVisuals.Create (TaleWorlds.MountAndBlade.View.dll)
        var agentVisualsCreateTarget = AgentVisuals_Create_Patch.TargetMethod();
        if (agentVisualsCreateTarget != null)
            harmony.Patch(agentVisualsCreateTarget, prefix: new HarmonyMethod(
                typeof(AgentVisuals_Create_Patch),
                nameof(AgentVisuals_Create_Patch.Prefix)));
        else
            IoC.Resolve<IModLogger>().LogWarning("[BannerColor] AgentVisuals.Create not found — clan color randomness suppression will not apply");

        // Manual patches for MapConversationTableau (private methods in SandBox.View.dll)
        var leaderTarget = MapConversationTableau_SpawnOpponentLeader_Patch.TargetMethod();
        if (leaderTarget != null)
            harmony.Patch(leaderTarget, postfix: new HarmonyMethod(
                typeof(MapConversationTableau_SpawnOpponentLeader_Patch),
                nameof(MapConversationTableau_SpawnOpponentLeader_Patch.Postfix)));
        else
            IoC.Resolve<IModLogger>().LogWarning("[BannerColor] MapConversationTableau.SpawnOpponentLeader not found — conversation tableau leader colors will not apply");

        var bodyguardTarget = MapConversationTableau_SpawnOpponentBodyguard_Patch.TargetMethod();
        if (bodyguardTarget != null)
            harmony.Patch(bodyguardTarget, postfix: new HarmonyMethod(
                typeof(MapConversationTableau_SpawnOpponentBodyguard_Patch),
                nameof(MapConversationTableau_SpawnOpponentBodyguard_Patch.Postfix)));
        else
            IoC.Resolve<IModLogger>().LogWarning("[BannerColor] MapConversationTableau.SpawnOpponentBodyguardCharacter not found — conversation tableau bodyguard colors will not apply");
    }
}
