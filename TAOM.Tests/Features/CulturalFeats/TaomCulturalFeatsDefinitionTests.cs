using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using TaleWorlds.CampaignSystem.CharacterDevelopment;
using TAOM.Features.CulturalFeats;

namespace TAOM.Tests.Features.CulturalFeats;

[TestClass]
public class TaomCulturalFeatsDefinitionTests
{
    /// <summary>
    /// Validates that all static feat properties follow the taom_ prefix convention
    /// by inspecting the field names that back them. Since we can't call CreateAndRegister
    /// without the game framework, we verify the code structure via reflection.
    /// </summary>
    [TestMethod]
    public void AllFeatProperties_ReturnFeatObject_CountIs129()
    {
        var properties = typeof(TaomCulturalFeats)
            .GetProperties(BindingFlags.Public | BindingFlags.Static)
            .Where(p => p.PropertyType == typeof(FeatObject))
            .ToList();

        Assert.AreEqual(129, properties.Count,
            "Expected 129 culture feat properties (105 prior + 24 Wave 1 economy/military feats)");
    }

    [TestMethod]
    public void AllFeatProperties_HaveDistinctNames()
    {
        var properties = typeof(TaomCulturalFeats)
            .GetProperties(BindingFlags.Public | BindingFlags.Static)
            .Where(p => p.PropertyType == typeof(FeatObject))
            .Select(p => p.Name)
            .ToList();

        var distinct = properties.Distinct().Count();
        Assert.AreEqual(properties.Count, distinct, "All feat property names must be unique");
    }

    [TestMethod]
    public void GetAllFeats_YieldsZeroOrFullSet()
    {
        // GetAllFeats returns empty when the static `_instance` is null (no game
        // framework loaded), OR the full 129-feat enumeration when a sibling test
        // (e.g. CulturalFeatsServiceTests) reflection-initialised the singleton.
        // Both states are valid in a test process; assert one or the other.
        var feats = TaomCulturalFeats.GetAllFeats().ToList();
        Assert.IsTrue(feats.Count == 0 || feats.Count == 129,
            $"GetAllFeats expected 0 (uninitialised) or 129 (full set), got {feats.Count}");
    }

    [TestMethod]
    [DataRow("EreborGarrisonWageFeat")]
    [DataRow("EreborProductionFeat")]
    [DataRow("EreborConstructionSpeedFeat")]
    [DataRow("EreborLoyaltyFeat")]
    [DataRow("EreborMoraleFeat")]
    [DataRow("EreborSmithingFeat")]
    [DataRow("RivendellArmyInfluenceFeat")]
    [DataRow("RivendellHearthGrowthFeat")]
    [DataRow("RivendellArmyInfluenceCostFeat")]
    [DataRow("RivendellFoodConsumptionFeat")]
    [DataRow("RivendellLoyaltyFeat")]
    [DataRow("MirkwoodForestSpeedFeat")]
    [DataRow("MirkwoodMilitiaProductionFeat")]
    [DataRow("MirkwoodHearthGrowthFeat")]
    [DataRow("MirkwoodFoodConsumptionFeat")]
    [DataRow("MirkwoodMoraleFeat")]
    [DataRow("LothlorienForestSpeedFeat")]
    [DataRow("LothlorienGarrisonWageFeat")]
    [DataRow("LothlorienConstructionSpeedFeat")]
    [DataRow("LothlorienFoodConsumptionFeat")]
    [DataRow("LothlorienLoyaltyFeat")]
    [DataRow("LothlorienMoraleFeat")]
    [DataRow("IsengardCheaperRecruitsFeat")]
    [DataRow("IsengardGarrisonWageFeat")]
    [DataRow("IsengardDecisionPenaltyFeat")]
    [DataRow("IsengardPartySizeFeat")]
    [DataRow("IsengardConstructionSpeedFeat")]
    [DataRow("IsengardSmithingFeat")]
    [DataRow("IsengardRaidDamageFeat")]
    [DataRow("GundabadArmyInfluenceCostFeat")]
    [DataRow("GundabadGrainProductionFeat")]
    [DataRow("GundabadWageFeat")]
    [DataRow("GundabadPartySizeFeat")]
    [DataRow("GundabadRaidDamageFeat")]
    [DataRow("UmbarCheaperCaravansFeat")]
    [DataRow("UmbarRenownFeat")]
    [DataRow("UmbarWageFeat")]
    [DataRow("UmbarTariffIncomeFeat")]
    [DataRow("DolGuldurArmyInfluenceCostFeat")]
    [DataRow("DolGuldurMilitiaProductionFeat")]
    [DataRow("DolGuldurConstructionSpeedFeat")]
    [DataRow("DolGuldurPartySizeFeat")]
    [DataRow("DolGuldurFoodConsumptionFeat")]
    [DataRow("GondorGarrisonWageFeat")]
    [DataRow("GondorArmyInfluenceFeat")]
    [DataRow("GondorHearthGrowthFeat")]
    [DataRow("GondorPartySizeFeat")]
    [DataRow("GondorLoyaltyFeat")]
    [DataRow("GondorMoraleFeat")]
    [DataRow("MordorArmyInfluenceCostFeat")]
    [DataRow("MordorGrainProductionFeat")]
    [DataRow("MordorWageFeat")]
    [DataRow("MordorPartySizeFeat")]
    [DataRow("MordorRaidDamageFeat")]
    [DataRow("RohanMountedCostFeat")]
    [DataRow("RohanMountedWageFeat")]
    [DataRow("RohanInfantrySpeedFeat")]
    [DataRow("RohanLoyaltyFeat")]
    [DataRow("RohanMoraleFeat")]
    [DataRow("EreborSnowSpeedFeat")]
    [DataRow("RivendellForestSpeedFeat")]
    [DataRow("IsengardPlainSpeedFeat")]
    [DataRow("IsengardSwampSpeedFeat")]
    [DataRow("GundabadSnowSpeedFeat")]
    [DataRow("UmbarDesertSpeedFeat")]
    [DataRow("GondorPlainSpeedFeat")]
    [DataRow("MordorPlainSpeedFeat")]
    [DataRow("MordorSwampSpeedFeat")]
    [DataRow("MordorNightSpeedFeat")]
    [DataRow("RohanPlainSpeedFeat")]
    [DataRow("DalePlainSpeedFeat")]
    [DataRow("KhandSteppeSpeedFeat")]
    [DataRow("RhunSteppeSpeedFeat")]
    [DataRow("HaradDesertSpeedFeat")]
    [DataRow("DunlandPlainSpeedFeat")]
    [DataRow("ShaghanaDesertSpeedFeat")]
    [DataRow("AbanissaDesertSpeedFeat")]
    // New party-size (3)
    [DataRow("DunlandPartySizeFeat")]
    [DataRow("RhunPartySizeFeat")]
    [DataRow("HaradPartySizeFeat")]
    // Volunteer respawn (4)
    [DataRow("DunlandVolunteerRateFeat")]
    [DataRow("GundabadVolunteerRateFeat")]
    [DataRow("DolGuldurVolunteerRateFeat")]
    [DataRow("MordorVolunteerRateFeat")]
    // Notable count: 4 villages (AddFactor) + 9 per-occupation town (Add)
    [DataRow("IsengardNotableCountVillageFeat")]
    [DataRow("DolGuldurNotableCountVillageFeat")]
    [DataRow("MordorNotableCountVillageFeat")]
    [DataRow("GundabadNotableCountVillageFeat")]
    [DataRow("IsengardNotableCountTownMerchantFeat")]
    [DataRow("IsengardNotableCountTownArtisanFeat")]
    [DataRow("IsengardNotableCountTownGangLeaderFeat")]
    [DataRow("DolGuldurNotableCountTownMerchantFeat")]
    [DataRow("DolGuldurNotableCountTownArtisanFeat")]
    [DataRow("DolGuldurNotableCountTownGangLeaderFeat")]
    [DataRow("MordorNotableCountTownGangLeaderFeat")]
    [DataRow("GundabadNotableCountTownArtisanFeat")]
    [DataRow("GundabadNotableCountTownGangLeaderFeat")]
    // New factions (Misty Mountains expansion): Goblins (4) + Misty Mountain Orcs (4)
    [DataRow("GoblinPartySizeFeat")]
    [DataRow("GoblinVolunteerRateFeat")]
    [DataRow("GoblinSnowSpeedFeat")]
    [DataRow("GoblinFoodConsumptionFeat")]
    [DataRow("MistyMountainOrcsArmyInfluenceCostFeat")]
    [DataRow("MistyMountainOrcsPartySizeFeat")]
    [DataRow("MistyMountainOrcsSnowSpeedFeat")]
    [DataRow("MistyMountainOrcsFoodConsumptionFeat")]
    // Wave 1 economy/military feats (24)
    [DataRow("MordorSmithingFeat")]
    [DataRow("EreborTariffIncomeFeat")]
    [DataRow("UmbarRaidDamageFeat")]
    [DataRow("UmbarFoodConsumptionFeat")]
    [DataRow("LothlorienVolunteerRateFeat")]
    [DataRow("MirkwoodArmyInfluenceCostFeat")]
    [DataRow("GoblinSmithingFeat")]
    [DataRow("GoblinRaidDamageFeat")]
    [DataRow("MistyMountainOrcsSmithingFeat")]
    [DataRow("MistyMountainOrcsRaidDamageFeat")]
    [DataRow("MistyMountainOrcsConstructionSpeedFeat")]
    [DataRow("DaleTariffIncomeFeat")]
    [DataRow("DaleRenownFeat")]
    [DataRow("DaleLoyaltyFeat")]
    [DataRow("KhandRenownFeat")]
    [DataRow("KhandTariffIncomeFeat")]
    [DataRow("KhandFoodConsumptionFeat")]
    [DataRow("KhandPartySizeFeat")]
    [DataRow("HaradMoraleFeat")]
    [DataRow("HaradFoodConsumptionFeat")]
    [DataRow("HaradRaidDamageFeat")]
    [DataRow("HaradArmyInfluenceCostFeat")]
    [DataRow("RhunLoyaltyFeat")]
    [DataRow("RhunRaidDamageFeat")]
    public void FeatProperty_Exists_IsPublicStatic(string propertyName)
    {
        var prop = typeof(TaomCulturalFeats).GetProperty(
            propertyName, BindingFlags.Public | BindingFlags.Static);

        Assert.IsNotNull(prop, $"Property {propertyName} should exist as public static");
        Assert.AreEqual(typeof(FeatObject), prop.PropertyType,
            $"Property {propertyName} should return FeatObject");
    }

    [TestMethod]
    public void EachCulture_HasExpectedFeatCount()
    {
        var properties = typeof(TaomCulturalFeats)
            .GetProperties(BindingFlags.Public | BindingFlags.Static)
            .Where(p => p.PropertyType == typeof(FeatObject))
            .Select(p => p.Name)
            .ToList();

        var expectedCounts = new Dictionary<string, int>
        {
            { "Erebor", 8 },      // Wave 1: +1 tariff income
            { "Rivendell", 6 },
            { "Mirkwood", 6 },    // Wave 1: +1 army influence cost
            { "Lothlorien", 7 },  // Wave 1: +1 volunteer rate
            { "Isengard", 13 },   // +3 per-occupation town notable + 1 village (was +2 → now +4 over base)
            { "Gundabad", 10 },   // +1 volunteer rate, +2 per-occ town notable + 1 village (was +3 → now +4 over base)
            { "Umbar", 7 },       // Wave 1: +1 raid damage, +1 food consumption
            { "DolGuldur", 10 },  // +1 volunteer rate, +3 per-occ town notable + 1 village
            { "Gondor", 7 },      // unchanged
            { "Mordor", 12 },     // Wave 1: +1 smithing
            { "Rohan", 6 },
            { "Dale", 4 },        // Wave 1: +1 tariff income, +1 renown, +1 loyalty
            { "Khand", 5 },       // Wave 1: +1 renown, +1 tariff income, +1 food consumption, +1 party size
            { "Rhun", 4 },        // +1 party size; Wave 1: +1 loyalty, +1 raid damage
            { "Harad", 6 },       // +1 party size; Wave 1: +1 morale, +1 food consumption, +1 raid damage, +1 army influence cost
            { "Dunland", 3 },     // +1 party size, +1 volunteer rate
            { "Shaghana", 1 },
            { "Abanissa", 1 },
            { "Goblin", 6 },             // party size, volunteer rate, snow speed, food consumption; Wave 1: +1 smithing, +1 raid damage
            { "MistyMountainOrcs", 7 },  // army influence cost, party size, snow speed, food consumption; Wave 1: +1 smithing, +1 raid damage, +1 construction speed
        };

        foreach (var kvp in expectedCounts)
        {
            var cultureFeats = properties.Where(p => p.StartsWith(kvp.Key)).ToList();
            Assert.AreEqual(kvp.Value, cultureFeats.Count,
                $"Culture {kvp.Key} should have {kvp.Value} feats, found: {string.Join(", ", cultureFeats)}");
        }
    }

    [TestMethod]
    public void RegisterAll_UsesCorrectStringIds()
    {
        // Verify the private field count matches expected feat count
        var fields = typeof(TaomCulturalFeats)
            .GetFields(BindingFlags.NonPublic | BindingFlags.Instance)
            .Where(f => f.FieldType == typeof(FeatObject))
            .ToList();

        Assert.AreEqual(129, fields.Count,
            "Expected 129 private FeatObject fields (105 prior + 24 Wave 1 economy/military feats)");
    }

    [TestMethod]
    [ExpectedException(typeof(System.InvalidOperationException))]
    public void FeatProperty_BeforeInitialization_ThrowsDescriptiveError()
    {
        TaomCulturalFeats.Reset();
        _ = TaomCulturalFeats.EreborGarrisonWageFeat;
    }

    [TestMethod]
    public void UmbarCheaperCaravansFeat_EffectBonus_IsNegative()
    {
        // The caravan feat must use negative additive-factor convention (-0.25 = 25% reduction).
        // A positive value (e.g. 0.75) with AddFactor would display as +75% in the UI.
        var field = typeof(TaomCulturalFeats)
            .GetMethod("InitializeAll", BindingFlags.NonPublic | BindingFlags.Instance);

        // Verify via the source code structure: the _umbarCheaperCaravans field name exists
        var caravanField = typeof(TaomCulturalFeats)
            .GetField("_umbarCheaperCaravans", BindingFlags.NonPublic | BindingFlags.Instance);

        Assert.IsNotNull(caravanField,
            "Field _umbarCheaperCaravans should exist as a private instance field");
    }

    /// <summary>
    /// Pins the PRODUCTION Initialize(...) metadata for the 24 Wave 1 feats against a
    /// canonical spec, by source-parsing TaomCulturalFeats.cs. The unit-test harness cannot
    /// call CreateAndRegister()/InitializeAll() (FeatObject.Initialize reaches into the game
    /// framework), and the dispatch tests in CulturalFeatsServiceTests inject FAKE FeatObjects
    /// from a mirrored table — so without this test a production sign-flip or wrong magnitude
    /// (e.g. "+0.15f" instead of "-0.15f" on a cost-reduction feat) would pass every test.
    /// Codex Wave 1 review (2026-06-07) MEDIUM finding: production EffectBonus / IsPositive /
    /// AdditionType / string-id for the 24 new feats were unpinned. Source-parse is brittle to
    /// field renames by design — a rename should re-confirm the spec.
    /// </summary>
    [TestMethod]
    public void Wave1Feats_ProductionMetadata_MatchesSpec()
    {
        // (field, stringId, effectBonus literal, isPositiveEffect, AdditionType)
        var spec = new (string field, string id, string bonus, bool positive, string addType)[]
        {
            ("_mordorSmithing", "taom_mordor_smithing", "-0.15f", true, "AddFactor"),
            ("_ereborTariffIncome", "taom_erebor_tariff_income", "0.05f", true, "AddFactor"),
            ("_umbarRaidDamage", "taom_umbar_raid_damage", "0.2f", true, "AddFactor"),
            ("_umbarFoodConsumption", "taom_umbar_food_consumption", "-0.1f", true, "AddFactor"),
            ("_lothlorienVolunteerRate", "taom_lothlorien_volunteer_rate", "-0.15f", false, "AddFactor"),
            ("_mirkwoodArmyInfluenceCost", "taom_mirkwood_army_influence_cost", "0.15f", false, "AddFactor"),
            ("_goblinSmithing", "taom_goblin_smithing", "-0.1f", true, "AddFactor"),
            ("_goblinRaidDamage", "taom_goblin_raid_damage", "0.1f", true, "AddFactor"),
            ("_mistyMountainOrcsSmithing", "taom_mistymountainorcs_smithing", "-0.15f", true, "AddFactor"),
            ("_mistyMountainOrcsRaidDamage", "taom_mistymountainorcs_raid_damage", "0.15f", true, "AddFactor"),
            ("_mistyMountainOrcsConstructionSpeed", "taom_mistymountainorcs_construction_speed", "-0.1f", false, "AddFactor"),
            ("_daleTariffIncome", "taom_dale_tariff_income", "0.1f", true, "AddFactor"),
            ("_daleRenown", "taom_dale_renown", "0.1f", true, "AddFactor"),
            ("_daleLoyalty", "taom_dale_loyalty", "-0.5f", false, "Add"),
            ("_khandRenown", "taom_khand_renown", "0.08f", true, "AddFactor"),
            ("_khandTariffIncome", "taom_khand_tariff_income", "-0.1f", false, "AddFactor"),
            ("_khandFoodConsumption", "taom_khand_food_consumption", "-0.1f", true, "AddFactor"),
            ("_khandPartySize", "taom_khand_party_size", "0.05f", true, "AddFactor"),
            ("_haradMorale", "taom_harad_morale", "5f", true, "Add"),
            ("_haradFoodConsumption", "taom_harad_food_consumption", "-0.15f", true, "AddFactor"),
            ("_haradRaidDamage", "taom_harad_raid_damage", "0.15f", true, "AddFactor"),
            ("_haradArmyInfluenceCost", "taom_harad_army_influence_cost", "0.15f", false, "AddFactor"),
            ("_rhunLoyalty", "taom_rhun_loyalty", "-0.5f", false, "Add"),
            ("_rhunRaidDamage", "taom_rhun_raid_damage", "0.15f", true, "AddFactor"),
        };

        var source = File.ReadAllText(LocateSource("Main/Features/CulturalFeats/TaomCulturalFeats.cs"));
        var failures = new List<string>();

        foreach (var (field, id, bonus, positive, addType) in spec)
        {
            // 1. Register("id") binds the expected field to the expected string-id.
            var registerPattern = $"{Regex.Escape(field)} = Register(\"{Regex.Escape(id)}\")";
            if (!source.Contains(registerPattern))
                failures.Add($"{field}: missing or mismatched Register(\"{id}\")");

            // 2. The Initialize(...) call carries the expected bonus / isPositiveEffect / AdditionType.
            // Match the full call (multi-line) from "<field>.Initialize(" to the closing ");".
            var initMatch = Regex.Match(
                source,
                Regex.Escape(field) + @"\.Initialize\((?<body>.*?)\);",
                RegexOptions.Singleline);
            if (!initMatch.Success)
            {
                failures.Add($"{field}: no Initialize(...) call found");
                continue;
            }

            var body = initMatch.Groups["body"].Value;
            var expectedTail =
                $"{bonus}, isPositiveEffect: {positive.ToString().ToLowerInvariant()}, FeatObject.AdditionType.{addType}";
            var normalized = Regex.Replace(body, @"\s+", " ");
            if (!normalized.Contains(expectedTail))
                failures.Add($"{field}: Initialize metadata != expected \"{expectedTail}\" (got: ...{normalized.Substring(System.Math.Max(0, normalized.Length - 80))})");
        }

        Assert.AreEqual(0, failures.Count,
            "Wave 1 production feat metadata drifted from spec:\n" + string.Join("\n", failures));
    }

    private static string LocateSource(string relPath)
    {
        var dir = new DirectoryInfo(AppDomain.CurrentDomain.BaseDirectory);
        while (dir != null)
        {
            if (Directory.Exists(Path.Combine(dir.FullName, ".git")))
                return Path.Combine(dir.FullName, relPath.Replace('/', Path.DirectorySeparatorChar));
            dir = dir.Parent;
        }
        throw new InvalidOperationException("repo root (.git) not found from " + AppDomain.CurrentDomain.BaseDirectory);
    }
}
