using System;
using System.Collections.Generic;
using System.Reflection;
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
            // Type.GetType("T, SandBox") is a Load-context bind: it probes only the appbase, where
            // module DLLs are NOT (they're Assembly.LoadFrom'd from Modules\SandBox\bin), and the
            // engine's AssemblyResolve handler matches exact FullName only — so it returns null
            // in-game for all 7 names (2026-07-21 crash report: the SandBox issues stayed alive).
            // Resolve against the already-loaded assembly by simple name instead.
            list.AddRange(ResolveTypesFromLoadedAssemblies("SandBox", SandBoxIssueTypeFullNames, AppDomain.CurrentDomain.GetAssemblies()));
            _cachedResolved = list;
            return _cachedResolved;
        }
    }

    private static IReadOnlyList<string> _sandBoxFullNames;

    /// <summary>The 7 SandBox type names without the assembly qualifier (namespace-qualified only).</summary>
    internal static IReadOnlyList<string> SandBoxIssueTypeFullNames
    {
        get
        {
            if (_sandBoxFullNames != null) return _sandBoxFullNames;
            var fullNames = new List<string>(SandBoxIssueTypeNames.Length);
            foreach (var name in SandBoxIssueTypeNames)
            {
                var comma = name.IndexOf(',');
                fullNames.Add(comma >= 0 ? name.Substring(0, comma).Trim() : name);
            }
            _sandBoxFullNames = fullNames;
            return _sandBoxFullNames;
        }
    }

    /// <summary>
    /// Resolve <paramref name="typeFullNames"/> from the already-loaded assembly whose SIMPLE name is
    /// <paramref name="assemblySimpleName"/>. Missing assembly or missing types degrade to an empty /
    /// shorter list — never a throw (the unit-test host has no SandBox.dll).
    /// </summary>
    internal static List<Type> ResolveTypesFromLoadedAssemblies(
        string assemblySimpleName, IEnumerable<string> typeFullNames, IEnumerable<Assembly> loadedAssemblies)
    {
        var result = new List<Type>();
        if (string.IsNullOrEmpty(assemblySimpleName) || typeFullNames == null || loadedAssemblies == null)
            return result;

        Assembly match = null;
        foreach (var asm in loadedAssemblies)
        {
            if (asm != null && string.Equals(asm.GetName().Name, assemblySimpleName, StringComparison.Ordinal))
            {
                match = asm;
                break;
            }
        }
        if (match == null) return result;

        foreach (var fullName in typeFullNames)
        {
            var t = match.GetType(fullName, throwOnError: false);
            if (t != null) result.Add(t);
        }
        return result;
    }

    /// <summary>
    /// True when <paramref name="issueType"/> (an issue/quest instance type OR its declaring behavior)
    /// belongs to the vanilla procedural-issue namespaces. Nested issue types report their declaring
    /// type's namespace, so a namespace check covers both. Drives the save-load safety-net sweep.
    /// </summary>
    public static bool IsVanillaIssueType(Type issueType)
    {
        var ns = issueType?.Namespace;
        return ns == "TaleWorlds.CampaignSystem.Issues" || ns == "SandBox.Issues";
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
        // RemoveBehaviors<T> is a silent no-op for an unregistered type, so "the call didn't throw"
        // proves nothing — count actual list shrinkage to keep the log honest (2026-07-21 review).
        int removed = 0;
        foreach (var t in types)
        {
            try
            {
                int before = starter.CampaignBehaviors.Count;
                removeBehaviors.MakeGenericMethod(t).Invoke(starter, null);
                if (starter.CampaignBehaviors.Count < before) removed++;
                else logger?.LogWarning($"LotrIssues: {t?.Name} resolved but was not registered — engine moved its registration?");
            }
            catch (Exception ex)
            {
                logger?.LogWarning($"LotrIssues: failed to suppress {t?.Name}: {ex.Message}");
            }
        }
        logger?.LogInfo($"LotrIssues: suppressed {removed}/{types.Count} vanilla issue behaviors (intended {ExpectedVanillaIssueCount})");
        // Under-count means vanilla issues are LIVE (they crash on TAOM data — e.g. the daughter
        // quest NREs on the XSLT-deleted steppe_bandits clan, 2026-07-21 report). Error, not warning.
        if (types.Count < ExpectedVanillaIssueCount)
            logger?.LogError($"LotrIssues: only {types.Count}/{ExpectedVanillaIssueCount} vanilla issue types resolved — unresolved SandBox issues are NOT suppressed and remain live");
    }
}
