using System.Runtime.CompilerServices;
using HarmonyLib;
using TAOM.Composition;
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
/// (ADR-002); called once per process from that same callback, order unchanged. On a dedicated server the
/// captain tooltip and the four visual patches are skipped (ADR-013): they patch screens a server never shows, and
/// their targets live in view and view-model assemblies that do not resolve when TAOM boots there (the Coop module can
/// load the stage's copies by game init), so each block sits in a NoInlining helper a server never compiles.
/// </summary>
internal static class ManualPatchApplicator
{
    internal static void ApplyAll(Harmony harmony)
    {
        // ADR-013: the captain tooltip and the four visual patches patch screens a dedicated server never shows, in
        // view and view-model assemblies that may not resolve there; each block sits in a NoInlining helper a server
        // never compiles. The settlement-guard patches target SandBox.dll and apply everywhere. Apply order unchanged.
        if (!ClientComposition.IsDedicatedServer())
            ApplyCaptainTooltip(harmony);

        var settlementGuardService = IoC.Resolve<ISettlementGuardService>();
        GuardsCampaignBehavior_TakeGuardAgentData_Patch.Initialize(settlementGuardService);
        GuardsCampaignBehavior_GetSuitableSpear_Patch.Initialize(settlementGuardService);
        GuardsCampaignBehavior_InitializeGarrisonCharacters_Patch.Initialize(settlementGuardService);

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

        var garrisonScrubTarget = GuardsCampaignBehavior_InitializeGarrisonCharacters_Patch.TargetMethod();
        if (garrisonScrubTarget != null)
            harmony.Patch(garrisonScrubTarget, postfix: new HarmonyMethod(
                typeof(GuardsCampaignBehavior_InitializeGarrisonCharacters_Patch),
                nameof(GuardsCampaignBehavior_InitializeGarrisonCharacters_Patch.Postfix)));
        else
            IoC.Resolve<IModLogger>().LogWarning("[SettlementGuards] InitializeGarrisonCharacters not found — excluded-race garrison scrub will not apply (#346)");

        // Regression of #258: a hideout has no guards; vanilla's guard/disguise start lines must not answer there.
        int hideoutGuardTargets = 0;
        foreach (var target in GuardsCampaignBehavior_HideoutNoGuards_Patch.TargetMethods())
        {
            harmony.Patch(target, postfix: new HarmonyMethod(
                typeof(GuardsCampaignBehavior_HideoutNoGuards_Patch),
                nameof(GuardsCampaignBehavior_HideoutNoGuards_Patch.Postfix)));
            hideoutGuardTargets++;
        }
        if (hideoutGuardTargets != GuardsCampaignBehavior_HideoutNoGuards_Patch.TargetNames.Length)
            IoC.Resolve<IModLogger>().LogWarning("[SettlementGuards] hideout guard-dialog suppression: " +
                $"{hideoutGuardTargets} of {GuardsCampaignBehavior_HideoutNoGuards_Patch.TargetNames.Length} GuardsCampaignBehavior conditions found; the hideout boss fight can be blocked by the guard line again (#258)");

        if (!ClientComposition.IsDedicatedServer())
            ApplyVisualPatches(harmony);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void ApplyCaptainTooltip(Harmony harmony)
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
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void ApplyVisualPatches(Harmony harmony)
    {
        // Manual patch for the private nested MobilePartyVisualHelper (SandBox.View.dll).
        // v1.5.0 moved the party-icon colour work here out of MobilePartyVisual.AddCharacterToPartyIcon,
        // and deleted the teamColor parameters the old postfix wrote to, so this is a transpiler now.
        // Patch53_PartyIconScaleHumanVisual transpiles the SAME method (category apply, earlier in
        // OnGameInitializationFinished); Harmony chains them and the two match disjoint instructions.
        var mobilePartyTarget = MobilePartyVisualHelper_GetHumanAgentPartyVisual_Patch.TargetMethod();
        if (mobilePartyTarget != null)
            harmony.Patch(mobilePartyTarget, transpiler: new HarmonyMethod(
                typeof(MobilePartyVisualHelper_GetHumanAgentPartyVisual_Patch),
                nameof(MobilePartyVisualHelper_GetHumanAgentPartyVisual_Patch.Transpiler)));
        else
            IoC.Resolve<IModLogger>().LogWarning("[BannerColor] MobilePartyVisualHelper.GetHumanAgentPartyVisual not found, party icon colors will not persist");

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
