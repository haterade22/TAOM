using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Reflection;
using System.Text.RegularExpressions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.CoopInterop;

namespace TAOM.Tests.Features.CoopInterop;

/// <summary>
/// #370 — makes "did anyone consider co-op?" un-skippable at build time.
///
/// A TAOM prefix that returns <c>bool</c> can skip a vanilla method entirely. When that method
/// mutates campaign state a co-op host replicates, and TAOM's condition can evaluate differently on
/// two peers, the result is not a crash — it is two campaigns that disagree and two saves that are
/// both wrong. That is exactly how the diplomacy veto (D1) survived unnoticed for months: nothing
/// forced the question to be asked.
///
/// So every bool-returning prefix must be classified here. Adding one without classifying it fails
/// this test. The classification is a judgement, not a rubber stamp — read
/// <see cref="CoopVeto"/> before choosing one.
/// </summary>
[TestClass]
public class CoopVetoClassificationTests
{
    private enum CoopVeto
    {
        /// <summary>
        /// Skips a replicated campaign-state mutation AND its condition can differ between peers —
        /// because it reads TAOM `SyncData` state the co-op mod does not replicate, or shipped
        /// config the two players could have edited differently. Must defer under co-op
        /// (`ICoopPresenceProvider`).
        /// </summary>
        GatedForCoop,

        /// <summary>
        /// Reviewed and found safe to keep enforcing under co-op: either it does not skip a
        /// replicated campaign-state mutation, or its condition derives only from state both peers
        /// provably agree on. The reason string must say which.
        /// </summary>
        ReviewedSafe,

        /// <summary>Wiring is commented out; re-classify when it is revived.</summary>
        Parked,
    }

    private sealed class Disposition
    {
        public Disposition(CoopVeto veto, string reason) { Veto = veto; Reason = reason; }
        public CoopVeto Veto { get; }
        public string Reason { get; }
    }

    // Keyed by type name (TAOM convention: one patch class per file, same name).
    private static readonly Dictionary<string, Disposition> Registry = new()
    {
        // --- Deferred under co-op ------------------------------------------------------------
        ["DeclareWarAction_ApplyInternal_Patch"] = new(CoopVeto.GatedForCoop,
            "Skips a war declaration. Condition is static config (_permanentRelationships, alignment), " +
            "so identical installs agree — but mismatched TAOM versions or edited configs do not."),
        ["MakePeaceAction_ApplyInternal_Patch"] = new(CoopVeto.GatedForCoop,
            "Skips a peace. Condition reads IsWarOfTheRingActive -> WarOfTheRingService.CurrentPhase, " +
            "persisted as the WarOfTheRing_CurrentPhase SyncData key, which BT never replicates. " +
            "This is the confirmed divergence, not a hypothetical one."),
        ["AllianceCampaignBehavior_EndAlliance_Patch"] = new(CoopVeto.GatedForCoop,
            "Skips an alliance end. Static-config condition, same peer-mismatch exposure as war."),

        // --- Reviewed, safe to keep enforcing ------------------------------------------------
        ["AllianceCampaignBehavior_AddAllianceDecision_Patch"] = new(CoopVeto.ReviewedSafe,
            "Dedup guard, not a policy veto: skips queuing a start-alliance decision for a pair that " +
            "is already allied. Condition is Kingdom.IsAllyWith — replicated vanilla state, so both " +
            "peers compute the same answer. Gating it would restore the decision-queue saturation " +
            "it exists to prevent."),
        ["TraitLevelingHelper_OnLordExecuted_Patch"] = new(CoopVeto.ReviewedSafe,
            "Skips the vanilla honour penalty. Condition is AlignmentService.AreEnemyAlignments — a " +
            "static shipped kingdom->alignment table, no campaign state, so peers agree."),
        ["BesiegerCamp_GetSiegeCampPartyPosition_Patch"] = new(CoopVeto.ReviewedSafe,
            "Position override driven by static siege-camp config; deterministic across peers."),
        ["PartyBaseHelper_HasFeat_Patch"] = new(CoopVeto.ReviewedSafe,
            "A query, not a mutation — replaces a throwing vanilla body. Answer derives from static " +
            "culture-feat config."),
        ["Clan_UpdateBannerColorsAccordingToKingdom_Patch"] = new(CoopVeto.ReviewedSafe,
            "Cosmetic only (banner colours). Condition is __instance != Clan.PlayerClan, which IS " +
            "per-peer — each player has their own clan — but the divergence is confined to banner " +
            "tint and is inherent to co-op having two player clans."),
        ["PartyScreenLogic_AddCommand_Patch"] = new(CoopVeto.ReviewedSafe,
            "Blocks an upgrade the local player cannot afford. Reads _taom_specialResources, which " +
            "is TAOM campaign state — but it gates the acting player's OWN party screen, and each " +
            "peer owns its own resources."),
        ["Patch34_SellAllItemsMenu"] = new(CoopVeto.ReviewedSafe,
            "Player-local inventory UI action."),
        ["Patch36_GameStateScreenManager"] = new(CoopVeto.ReviewedSafe,
            "Screen routing for TAOM's fief-management state. UI only."),
        ["Patch58_SkipCampaignIntro"] = new(CoopVeto.ReviewedSafe,
            "New-game intro skip, before any session exists."),
        ["Campaign_InitializeScenes_Patch"] = new(CoopVeto.ReviewedSafe,
            "Startup scene-table load. Process init, identical on both peers."),
        ["MBMapScene_GetBattleSceneIndexMap_Patch"] = new(CoopVeto.ReviewedSafe,
            "Startup battle-scene index map. Process init."),
        ["MBTextManager_GetLocalizedText_Patch"] = new(CoopVeto.ReviewedSafe,
            "String lookup. Presentation only."),
        ["ActionSetCode_GenerateActionSetNameWithSuffix_Patch"] = new(CoopVeto.ReviewedSafe,
            "Animation action-set naming. Presentation only."),
        ["CharacterSpawner_InitWithCharacter_Patch"] = new(CoopVeto.ReviewedSafe,
            "Character visual spawn. Presentation only."),
        ["Banner_GetFirstIconColor_Patch"] = new(CoopVeto.ReviewedSafe,
            "Banner icon colour. Presentation only."),
        ["Mission_SpawnAgent_Patch"] = new(CoopVeto.ReviewedSafe,
            "Mission-scoped agent spawn. Not campaign state."),
        ["BannerBearerLogic_SpawnBannerBearer_Patch"] = new(CoopVeto.ReviewedSafe,
            "Mission-scoped reinforcement bearer spawn + AV guard. Not campaign state."),
        ["Patch30_FormationGetOrderPositionOfUnit"] = new(CoopVeto.ReviewedSafe,
            "Mission-scoped formation positioning. Not campaign state."),
        ["GuardsCampaignBehavior_GetSuitableSpear_Patch"] = new(CoopVeto.ReviewedSafe,
            "Settlement-guard equipment selection at spawn. Presentation/spawn only."),
        ["GuardsCampaignBehavior_TakeGuardAgentData_Patch"] = new(CoopVeto.ReviewedSafe,
            "Settlement-guard roster selection at spawn. Presentation/spawn only."),
        ["CustomBattleData_Characters_Patch"] = new(CoopVeto.ReviewedSafe,
            "Custom Battle setup. No campaign."),
        ["CustomBattleData_Factions_Patch"] = new(CoopVeto.ReviewedSafe,
            "Custom Battle setup. No campaign."),
        ["CustomBattleSideVM_OnCharacterSelection_Patch"] = new(CoopVeto.ReviewedSafe,
            "Custom Battle UI null-guard. No campaign."),
        ["CustomBattleSideVM_UpdateCharacterVisual_Patch"] = new(CoopVeto.ReviewedSafe,
            "Custom Battle UI null-guard. No campaign."),

        // Character creation (added 2026-08-01 alongside the BannerlordCoop interop work). All four
        // act only on CharacterCreationManager / its NarrativeMenu visual characters — they set a
        // __result list of NarrativeMenuCharacterArgs, strip non-human preview meshes, or advance the
        // CC menu cursor. None touches a replicated campaign object. Independently, BannerlordCoop's
        // CallOriginalPolicy treats character creation as an unsynced state (its ISyncPolicy only
        // reports CampaignState/MissionState as syncing), so no peer replication is in flight here.
        ["CharacterCreationCampaignBehavior_GetYouthMenuArgs_Patch"] = new(CoopVeto.ReviewedSafe,
            "Builds the CC youth-menu preview character list when the culture's CC roster has no " +
            "horse (Erebor et al). Condition derives from shipped culture equipment data; the write " +
            "is a local UI preview list, not campaign state."),
        ["CharacterCreationCampaignBehavior_GetAdultMenuArgs_Patch"] = new(CoopVeto.ReviewedSafe,
            "Same no-horse NRE guard for the adult menu. Local CC preview only."),
        ["CharacterCreationCampaignBehavior_GetAgeSelectionMenuArgs_Patch"] = new(CoopVeto.ReviewedSafe,
            "Same no-horse NRE guard for the age-selection menu. Local CC preview only."),
        ["TrySwitchToNextMenu_Patch"] = new(CoopVeto.ReviewedSafe,
            "Advances the CC narrative menu when SelectedOptions has no entry for the current menu " +
            "(vanilla throws KeyNotFoundException for custom cultures with no valid option). Reads " +
            "and writes only CharacterCreationManager cursor state, and preserves vanilla's " +
            "ModifyMenuCharacters side effect. No campaign object involved."),

        // --- Parked --------------------------------------------------------------------------
        ["Patch57_NavalAtSeaLandRescueGuard"] = new(CoopVeto.Parked,
            "NavalTravel is PARKED (#120/#296) and the category is commented out in SubModule.cs. " +
            "It moves parties on the campaign map — re-classify before reviving."),
    };

    // Scans SOURCE, not the loaded assembly, and that is deliberate.
    //
    // The first version of this test reflected over typeof(SubModule).Assembly. It under-reported
    // by four: a patch class whose signature references a View/Engine type that cannot load in the
    // test host comes back null from ReflectionTypeLoadException.Types and vanishes from the scan —
    // and those engine-coupled patches are precisely the ones most worth classifying. A scan that
    // silently misses the risky half is worse than no scan, because it reads as coverage.
    // The access modifier is OPTIONAL: several patches declare `static bool Prefix(` with no
    // modifier at all (implicitly private — Harmony does not care). Requiring one silently missed
    // four classes, three of which share a single file.
    private static readonly Regex BoolPrefixPattern = new(
        @"(?:(?:public|private|internal|protected)\s+)?static\s+bool\s+Prefix\s*\(",
        RegexOptions.Compiled);

    // Anchored to line start, and applied only after comments are stripped — otherwise the prose
    // "one patch class per file" in a doc comment parses as a class declaration named "per".
    private static readonly Regex ClassDeclPattern = new(
        @"^\s*(?:(?:public|private|internal|protected|static|sealed|partial|abstract)\s+)*class\s+(\w+)",
        RegexOptions.Compiled | RegexOptions.Multiline);

    private static readonly Regex CommentPattern = new(
        @"/\*.*?\*/|//[^\n]*", RegexOptions.Compiled | RegexOptions.Singleline);

    /// <summary>
    /// Blanks comments while preserving BOTH length and newlines — offsets stay comparable between
    /// the class and prefix scans, and the multiline `^` anchor still sees real line starts.
    /// </summary>
    private static string StripComments(string source) =>
        CommentPattern.Replace(source, m => Regex.Replace(m.Value, "[^\n]", " "));

    private static string FindMainSourceDir()
    {
        var dir = new DirectoryInfo(
            Path.GetDirectoryName(typeof(CoopVetoClassificationTests).Assembly.Location)!);

        while (dir != null)
        {
            var candidate = Path.Combine(dir.FullName, "Main", "Features");
            if (Directory.Exists(candidate)) return Path.Combine(dir.FullName, "Main");
            dir = dir.Parent;
        }

        return null;
    }

    /// <summary>
    /// Names of every CLASS declaring a bool-returning Harmony prefix.
    ///
    /// Keyed on the class, not the file: `CharacterCreationCampaignBehavior_GetYouthMenuArgs_Patch.cs`
    /// holds three separate patch classes, and file-name keying silently collapsed them to one.
    /// Each prefix is attributed to the nearest preceding `class` declaration.
    /// </summary>
    private static IReadOnlyList<string> BoolPrefixSources()
    {
        var mainDir = FindMainSourceDir();
        Assert.IsNotNull(mainDir,
            "could not locate the Main/ source tree from the test assembly — this test scans source, " +
            "so it cannot run from a detached bin drop");

        var names = new List<string>();

        foreach (var path in Directory
                     .EnumerateFiles(mainDir, "*.cs", SearchOption.AllDirectories)
                     .Where(p => !p.Contains(Path.DirectorySeparatorChar + "obj" + Path.DirectorySeparatorChar))
                     .Where(p => !p.Contains(Path.DirectorySeparatorChar + "bin" + Path.DirectorySeparatorChar)))
        {
            var text = StripComments(File.ReadAllText(path));
            if (!BoolPrefixPattern.IsMatch(text)) continue;

            var classes = ClassDeclPattern.Matches(text)
                .Cast<Match>()
                .Select(m => (Index: m.Index, Name: m.Groups[1].Value))
                .OrderBy(c => c.Index)
                .ToList();

            foreach (Match prefix in BoolPrefixPattern.Matches(text))
            {
                var owner = classes.LastOrDefault(c => c.Index < prefix.Index);
                names.Add(owner.Name ?? Path.GetFileNameWithoutExtension(path));
            }
        }

        return names.Distinct(StringComparer.Ordinal).OrderBy(n => n, StringComparer.Ordinal).ToList();
    }

    [TestMethod]
    public void EveryBoolPrefix_HasACoopDisposition()
    {
        var found = BoolPrefixSources().ToList();

        Assert.AreNotEqual(0, found.Count, "reflection found no bool-returning prefixes — scan is broken");

        var unclassified = found.Where(n => !Registry.ContainsKey(n)).ToList();

        Assert.AreEqual(0, unclassified.Count,
            "These prefixes can skip a vanilla method but carry no co-op disposition:\n  " +
            string.Join("\n  ", unclassified) +
            "\n\nAdd each to CoopVetoClassificationTests.Registry. Ask: can this skip a campaign-state " +
            "mutation the co-op host replicates, AND can my condition evaluate differently on two " +
            "peers? If yes -> gate it on ICoopPresenceProvider and mark GatedForCoop. If no -> mark " +
            "ReviewedSafe and say why in the reason string.");
    }

    [TestMethod]
    public void Registry_HasNoStaleEntries()
    {
        var found = new HashSet<string>(BoolPrefixSources(), StringComparer.Ordinal);
        var stale = Registry.Keys.Where(k => !found.Contains(k)).OrderBy(k => k).ToList();

        Assert.AreEqual(0, stale.Count,
            "Registry entries with no matching prefix (renamed or deleted?):\n  " +
            string.Join("\n  ", stale));
    }

    // --- The blind spot this test originally had (#370, deep review 2026-08-01) ----------------
    //
    // The first version scanned ONLY bool-returning Harmony prefixes. That is not where the veto
    // surface ends: the same three diplomacy rules are ALSO reachable through GameModel overrides
    // (TaomKingdomDecisionPermissionModel via DeclareWarDecision.IsAllowed /
    // MakePeaceKingdomDecision.IsAllowed, and TaomDiplomacyModel.IsAtConstantWar). A GameModel is
    // registered with campaignStarter.AddModel, not [HarmonyPatch], so it could never appear in the
    // registry above — the mechanism built to make "did anyone consider co-op?" un-skippable was
    // structurally blind to half the answer, and three ungated sites shipped behind it.
    //
    // So: any type reading one of the divergence-prone diplomacy rules must also read the co-op
    // flag. This is a coarse source check on purpose — it cannot prove the gate is CORRECT, only
    // that the author was forced to think about it. Correctness is pinned by the per-hook tests.

    private static readonly string[] DivergenceProneRules =
    {
        "IsWarAllowed",
        "ShouldBlockPeace",
        "IsAllianceDecisionAllowed",
    };

    [TestMethod]
    public void EveryConsumerOfADivergenceProneDiplomacyRule_AlsoReadsTheCoopFlag()
    {
        var mainDir = FindMainSourceDir();
        Assert.IsNotNull(mainDir, "could not locate Main/ source tree");

        var offenders = new List<string>();

        foreach (var path in Directory
                     .EnumerateFiles(mainDir, "*.cs", SearchOption.AllDirectories)
                     .Where(p => !p.Contains(Path.DirectorySeparatorChar + "obj" + Path.DirectorySeparatorChar))
                     .Where(p => !p.Contains(Path.DirectorySeparatorChar + "bin" + Path.DirectorySeparatorChar)))
        {
            var text = StripComments(File.ReadAllText(path));
            var name = Path.GetFileNameWithoutExtension(path);

            // The rules' own declaring service + its interface are the definitions, not consumers.
            if (name.EndsWith("Service", StringComparison.Ordinal)
                || name.StartsWith("I", StringComparison.Ordinal) && name.EndsWith("Service", StringComparison.Ordinal))
                continue;
            if (name is "DiplomacyService" or "IDiplomacyService"
                or "WarOfTheRingService" or "IWarOfTheRingService")
                continue;

            var callsRule = DivergenceProneRules.Any(r =>
                text.Contains("." + r + "("));
            if (!callsRule) continue;

            var readsCoop = text.Contains("IsCoopActive")
                            || text.Contains("CoopPresence")
                            || text.Contains("IsAuthority");

            if (!readsCoop) offenders.Add(name);
        }

        Assert.AreEqual(
            0, offenders.Count,
            "These types decide on a divergence-prone diplomacy rule without reading any co-op " +
            "flag:\n  " + string.Join("\n  ", offenders) +
            "\n\nUnder co-op the host's diplomacy is authoritative. A veto that evaluates " +
            "differently on two peers desyncs the campaign silently. Gate on " +
            "ICoopPresenceProvider, or if the site genuinely cannot diverge, say why in a comment " +
            "naming the co-op flag so this check sees it.");
    }

    [TestMethod]
    public void EveryDisposition_HasANonEmptyReason()
    {
        var empty = Registry
            .Where(kv => string.IsNullOrWhiteSpace(kv.Value.Reason))
            .Select(kv => kv.Key)
            .ToList();

        Assert.AreEqual(0, empty.Count,
            "A disposition without a reason is a rubber stamp: " + string.Join(", ", empty));
    }

    [TestMethod]
    public void GatedVetoes_AreExactlyTheDiplomacyThree()
    {
        // Pins the D1 fix. If a gate is removed, this fails rather than the divergence silently
        // returning — the failure mode is a corrupted co-op save, which no other test can catch.
        var gated = Registry
            .Where(kv => kv.Value.Veto == CoopVeto.GatedForCoop)
            .Select(kv => kv.Key)
            .OrderBy(k => k)
            .ToList();

        CollectionAssert.AreEqual(
            new[]
            {
                "AllianceCampaignBehavior_EndAlliance_Patch",
                "DeclareWarAction_ApplyInternal_Patch",
                "MakePeaceAction_ApplyInternal_Patch",
            },
            gated.ToArray());
    }
}
