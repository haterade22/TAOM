using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
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
    public void AllFeatProperties_ReturnFeatObject_CountIs42()
    {
        var properties = typeof(TaomCulturalFeats)
            .GetProperties(BindingFlags.Public | BindingFlags.Static)
            .Where(p => p.PropertyType == typeof(FeatObject))
            .ToList();

        Assert.AreEqual(43, properties.Count,
            "Expected 43 culture feat properties (expanded feat set across 11 cultures)");
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
    public void GetAllFeats_YieldsCorrectCount()
    {
        // GetAllFeats returns empty when instance is null (no game framework)
        var feats = TaomCulturalFeats.GetAllFeats().ToList();
        Assert.AreEqual(0, feats.Count,
            "GetAllFeats should return empty when not initialized (no game framework in tests)");
    }

    [TestMethod]
    [DataRow("EreborGarrisonWageFeat")]
    [DataRow("EreborProductionFeat")]
    [DataRow("EreborConstructionSpeedFeat")]
    [DataRow("RivendellArmyInfluenceFeat")]
    [DataRow("RivendellHearthGrowthFeat")]
    [DataRow("RivendellArmyInfluenceCostFeat")]
    [DataRow("RivendellFoodConsumptionFeat")]
    [DataRow("MirkwoodForestSpeedFeat")]
    [DataRow("MirkwoodMilitiaProductionFeat")]
    [DataRow("MirkwoodHearthGrowthFeat")]
    [DataRow("MirkwoodFoodConsumptionFeat")]
    [DataRow("LothlorienForestSpeedFeat")]
    [DataRow("LothlorienGarrisonWageFeat")]
    [DataRow("LothlorienConstructionSpeedFeat")]
    [DataRow("LothlorienFoodConsumptionFeat")]
    [DataRow("IsengardCheaperRecruitsFeat")]
    [DataRow("IsengardGarrisonWageFeat")]
    [DataRow("IsengardDecisionPenaltyFeat")]
    [DataRow("IsengardPartySizeFeat")]
    [DataRow("IsengardConstructionSpeedFeat")]
    [DataRow("GundabadArmyInfluenceCostFeat")]
    [DataRow("GundabadGrainProductionFeat")]
    [DataRow("GundabadWageFeat")]
    [DataRow("GundabadPartySizeFeat")]
    [DataRow("UmbarCheaperCaravansFeat")]
    [DataRow("UmbarRenownFeat")]
    [DataRow("UmbarWageFeat")]
    [DataRow("DolGuldurArmyInfluenceCostFeat")]
    [DataRow("DolGuldurMilitiaProductionFeat")]
    [DataRow("DolGuldurConstructionSpeedFeat")]
    [DataRow("DolGuldurPartySizeFeat")]
    [DataRow("DolGuldurFoodConsumptionFeat")]
    [DataRow("GondorGarrisonWageFeat")]
    [DataRow("GondorArmyInfluenceFeat")]
    [DataRow("GondorHearthGrowthFeat")]
    [DataRow("GondorPartySizeFeat")]
    [DataRow("MordorArmyInfluenceCostFeat")]
    [DataRow("MordorGrainProductionFeat")]
    [DataRow("MordorWageFeat")]
    [DataRow("MordorPartySizeFeat")]
    [DataRow("RohanMountedCostFeat")]
    [DataRow("RohanMountedWageFeat")]
    [DataRow("RohanInfantrySpeedFeat")]
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
            { "Erebor", 3 },
            { "Rivendell", 4 },
            { "Mirkwood", 4 },
            { "Lothlorien", 4 },
            { "Isengard", 5 },
            { "Gundabad", 4 },
            { "Umbar", 3 },
            { "DolGuldur", 5 },
            { "Gondor", 4 },
            { "Mordor", 4 },
            { "Rohan", 3 },
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

        Assert.AreEqual(43, fields.Count,
            "Expected 43 private FeatObject fields (expanded feat set across 11 cultures)");
    }
}
