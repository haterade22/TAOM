using System;
using System.Collections.Generic;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Issues;
using TAOM.Core.Logging;

namespace TAOM.Features.LotrIssues;

/// <summary>
/// The authoritative set of vanilla procedural issue behaviors TAOM suppresses so only LOTR custom
/// issues spawn. 36 are registered in <c>SandBoxManager.Initialize</c> (TaleWorlds.CampaignSystem.Issues)
/// and 7 in <c>SandBoxSubModule.InitializeGameStarter</c> (SandBox.Issues) — both run before TAOM's
/// <c>OnGameStart</c>, so <see cref="SuppressAll"/> (called from there) removes them after registration.
/// The host <c>IssuesCampaignBehavior</c> spawner is intentionally NOT removed.
/// </summary>
/// <remarks>
/// The 36 CampaignSystem types are bound via compile-checked <c>typeof</c>. The 7 SandBox types are
/// resolved by assembly-qualified name via <c>Type.GetType</c> so the list builds even where SandBox.dll
/// is absent (the unit-test host) and degrades gracefully if the engine renames a type after a bump
/// (a missing type becomes a logged no-op, never a crash — plan risk #7).
/// </remarks>
public static class LotrIssueSuppression
{
    /// <summary>The 7 SandBox-module issue behaviors, assembly-qualified (resolved lazily).</summary>
    internal static readonly string[] SandBoxIssueTypeNames =
    {
        "SandBox.Issues.RivalGangMovingInIssueBehavior, SandBox",
        "SandBox.Issues.RuralNotableInnAndOutIssueBehavior, SandBox",
        "SandBox.Issues.FamilyFeudIssueBehavior, SandBox",
        "SandBox.Issues.NotableWantsDaughterFoundIssueBehavior, SandBox",
        "SandBox.Issues.TheSpyPartyIssueQuestBehavior, SandBox",
        "SandBox.Issues.ProdigalSonIssueBehavior, SandBox",
        "SandBox.Issues.SnareTheWealthyIssueBehavior, SandBox",
    };

    /// <summary>The 36 TaleWorlds.CampaignSystem.Issues behaviors (SandBoxManager.Initialize:162-197).</summary>
    internal static readonly IReadOnlyList<Type> CampaignSystemIssueTypes = new List<Type>
    {
        typeof(ArmyNeedsSuppliesIssueBehavior),
        typeof(ArtisanCantSellProductsAtAFairPriceIssueBehavior),
        typeof(ArtisanOverpricedGoodsIssueBehavior),
        typeof(CapturedByBountyHuntersIssueBehavior),
        typeof(CaravanAmbushIssueBehavior),
        typeof(EscortMerchantCaravanIssueBehavior),
        typeof(ExtortionByDesertersIssueBehavior),
        typeof(GangLeaderNeedsToOffloadStolenGoodsIssueBehavior),
        typeof(GangLeaderNeedsWeaponsIssueQuestBehavior),
        typeof(RevenueFarmingIssueBehavior),
        typeof(HeadmanNeedsGrainIssueBehavior),
        typeof(HeadmanNeedsToDeliverAHerdIssueBehavior),
        typeof(HeadmanVillageNeedsDraughtAnimalsIssueBehavior),
        typeof(LadysKnightOutIssueBehavior),
        typeof(LandLordCompanyOfTroubleIssueBehavior),
        typeof(LandLordTheArtOfTheTradeIssueBehavior),
        typeof(LandlordNeedsAccessToVillageCommonsIssueBehavior),
        typeof(LandLordNeedsManualLaborersIssueBehavior),
        typeof(LandlordTrainingForRetainersIssueBehavior),
        typeof(LordNeedsGarrisonTroopsIssueQuestBehavior),
        typeof(TheConquestOfSettlementIssueBehavior),
        typeof(VillageNeedsCraftingMaterialsIssueBehavior),
        typeof(SmugglersIssueBehavior),
        typeof(LordNeedsHorsesIssueBehavior),
        typeof(LordsNeedsTutorIssueBehavior),
        typeof(LordWantsRivalCapturedIssueBehavior),
        typeof(MerchantArmyOfPoachersIssueBehavior),
        typeof(MerchantNeedsHelpWithOutlawsIssueQuestBehavior),
        typeof(NearbyBanditBaseIssueBehavior),
        typeof(RaidAnEnemyTerritoryIssueBehavior),
        typeof(ScoutEnemyGarrisonsIssueBehavior),
        typeof(VillageNeedsToolsIssueBehavior),
        typeof(GangLeaderNeedsRecruitsIssueBehavior),
        typeof(GangLeaderNeedsSpecialWeaponsIssueBehavior),
        typeof(LesserNobleRevoltIssueBehavior),
        typeof(BettingFraudIssueBehavior),
    };

    /// <summary>The full intended count of vanilla issue behaviors (36 CampaignSystem + 7 SandBox).</summary>
    public static int ExpectedVanillaIssueCount => CampaignSystemIssueTypes.Count + SandBoxIssueTypeNames.Length;

    /// <summary>
    /// The vanilla issue behavior types resolvable in this process: the 36 CampaignSystem types plus
    /// whichever SandBox types resolve (all 7 in-game; 0 in the SandBox-less unit-test host).
    /// </summary>
    private static IReadOnlyList<Type> _cachedResolved;

    public static IReadOnlyList<Type> VanillaIssueBehaviorTypes
    {
        get
        {
            if (_cachedResolved != null) return _cachedResolved;
            var list = new List<Type>(CampaignSystemIssueTypes);
            foreach (var name in SandBoxIssueTypeNames)
            {
                var t = Type.GetType(name, throwOnError: false);
                if (t != null) list.Add(t);
            }
            _cachedResolved = list;
            return _cachedResolved;
        }
    }

    /// <summary>
    /// Un-register every resolvable vanilla issue behavior from the starter. Called from
    /// <c>SubModule.OnGameStart</c> after Sandbox has registered them. Each removal is guarded so an
    /// engine-bump quirk degrades to a logged no-op rather than a crash.
    /// </summary>
    public static void SuppressAll(CampaignGameStarter starter, IModLogger logger)
    {
        if (starter == null) return;
        var removeBehaviors = typeof(CampaignGameStarter).GetMethod("RemoveBehaviors");
        if (removeBehaviors == null)
        {
            logger?.LogError("LotrIssues: CampaignGameStarter.RemoveBehaviors not found — vanilla issues NOT suppressed");
            return;
        }

        var types = VanillaIssueBehaviorTypes;
        int removed = 0;
        foreach (var t in types)
        {
            try
            {
                removeBehaviors.MakeGenericMethod(t).Invoke(starter, null);
                removed++;
            }
            catch (Exception ex)
            {
                logger?.LogWarning($"LotrIssues: failed to suppress {t?.Name}: {ex.Message}");
            }
        }
        logger?.LogInfo($"LotrIssues: suppressed {removed}/{types.Count} vanilla issue behaviors (intended {ExpectedVanillaIssueCount})");
        if (types.Count < ExpectedVanillaIssueCount)
            logger?.LogWarning($"LotrIssues: only {types.Count}/{ExpectedVanillaIssueCount} vanilla issue types resolved — some SandBox issues may not be suppressed");
    }
}
