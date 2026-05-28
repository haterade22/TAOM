using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using System.Reflection;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CharacterDevelopment;
using TaleWorlds.Core;
using TAOM.Features.CulturalFeats;

namespace TAOM.Tests.Features.CulturalFeats;

/// <summary>
/// Tests the dispatch logic extracted from the 16 <c>Taom*Model</c> overrides
/// in <see cref="TAOM.Features.CulturalFeats.Models"/>. The service is tested
/// against a stubbed <see cref="ICultureFeatAdapter"/> so no live TaleWorlds
/// culture instances are required. <see cref="FeatObject"/> instances are
/// constructed with non-trivial <c>EffectBonus</c> values via reflection (the
/// vanilla <c>Initialize()</c> requires <c>Campaign.Current</c> which is not
/// available in unit tests).
/// </summary>
[TestClass]
public class CulturalFeatsServiceTests
{
    private ICulturalFeatsService _sut = null!;

    [TestInitialize]
    public void Init()
    {
        _sut = new CulturalFeatsService();
        EnsureFeatsInitialised();
    }

    // ── ArmyManagement ──────────────────────────────────────────────────

    [TestMethod]
    public void ApplyArmyInfluenceAward_NullCulture_ReturnsBaseAward()
    {
        var result = _sut.ApplyArmyInfluenceAward(null, 100f);
        Assert.AreEqual(100f, result);
    }

    [TestMethod]
    public void ApplyArmyInfluenceAward_RivendellOnly_AddsRivendellBonusToBase()
    {
        var culture = AdapterWith(TaomCulturalFeats.RivendellArmyInfluenceFeat);

        var result = _sut.ApplyArmyInfluenceAward(culture, 100f);

        Assert.AreEqual(
            100f + 100f * TaomCulturalFeats.RivendellArmyInfluenceFeat.EffectBonus,
            result);
    }

    [TestMethod]
    public void ApplyArmyInfluenceAward_GondorOnly_AddsGondorBonusToBase()
    {
        var culture = AdapterWith(TaomCulturalFeats.GondorArmyInfluenceFeat);

        var result = _sut.ApplyArmyInfluenceAward(culture, 100f);

        Assert.AreEqual(
            100f + 100f * TaomCulturalFeats.GondorArmyInfluenceFeat.EffectBonus,
            result);
    }

    [TestMethod]
    public void ApplyArmyInfluenceCost_NullCulture_ReturnsBaseCost()
    {
        var result = _sut.ApplyArmyInfluenceCost(null, 100);
        Assert.AreEqual(100, result);
    }

    [TestMethod]
    public void ApplyArmyInfluenceCost_GundabadOnly_AppliesGundabadFactor()
    {
        var culture = AdapterWith(TaomCulturalFeats.GundabadArmyInfluenceCostFeat);

        var result = _sut.ApplyArmyInfluenceCost(culture, 100);

        var expected = (int)(100 * (1f + TaomCulturalFeats.GundabadArmyInfluenceCostFeat.EffectBonus));
        Assert.AreEqual(expected, result);
    }

    [TestMethod]
    public void ApplyArmyInfluenceCost_MultipleFeats_StackAdditively()
    {
        var culture = AdapterWith(
            TaomCulturalFeats.RivendellArmyInfluenceCostFeat,
            TaomCulturalFeats.MordorArmyInfluenceCostFeat);

        var result = _sut.ApplyArmyInfluenceCost(culture, 100);

        var multiplier = TaomCulturalFeats.RivendellArmyInfluenceCostFeat.EffectBonus
                       + TaomCulturalFeats.MordorArmyInfluenceCostFeat.EffectBonus;
        var expected = (int)(100 * (1f + multiplier));
        Assert.AreEqual(expected, result);
    }

    [TestMethod]
    public void ApplyArmyInfluenceCost_NoMatchingFeats_LeavesCostUnchanged()
    {
        var culture = Substitute.For<ICultureFeatAdapter>();
        culture.HasFeat(Arg.Any<FeatObject>()).Returns(false);

        var result = _sut.ApplyArmyInfluenceCost(culture, 100);

        Assert.AreEqual(100, result);
    }

    // ── PartySpeed ──────────────────────────────────────────────────────

    [TestMethod]
    public void ApplyTerrainSpeedFeats_NullCulture_DoesNothing()
    {
        var en = new ExplainedNumber(1f);
        _sut.ApplyTerrainSpeedFeats(null, TerrainKind.Forest, isNight: false, ref en);
        Assert.AreEqual(1f, en.ResultNumber);
    }

    [TestMethod]
    public void ApplyTerrainSpeedFeats_NoneTerrain_DoesNothing()
    {
        var culture = AdapterWith(TaomCulturalFeats.MirkwoodForestSpeedFeat);
        var en = new ExplainedNumber(1f);

        _sut.ApplyTerrainSpeedFeats(culture, TerrainKind.None, isNight: false, ref en);

        Assert.AreEqual(1f, en.ResultNumber);
    }

    [TestMethod]
    public void ApplyTerrainSpeedFeats_WrongTerrain_DoesNothing()
    {
        // Mirkwood's forest feat must NOT apply when the party is on a plain.
        var culture = AdapterWith(TaomCulturalFeats.MirkwoodForestSpeedFeat);
        var en = new ExplainedNumber(1f);

        _sut.ApplyTerrainSpeedFeats(culture, TerrainKind.Plain, isNight: false, ref en);

        Assert.AreEqual(1f, en.ResultNumber);
    }

    [TestMethod]
    public void ApplyTerrainSpeedFeats_Forest_ElvenFeat_AppliesFlatTenPercent()
    {
        var culture = AdapterWith(TaomCulturalFeats.MirkwoodForestSpeedFeat);
        var en = new ExplainedNumber(1f);

        _sut.ApplyTerrainSpeedFeats(culture, TerrainKind.Forest, isNight: false, ref en);

        // Flat AddFactor of the feat's EffectBonus (0.10), NOT the old scaled value.
        Assert.AreEqual(1f * (1f + 0.1f), en.ResultNumber, 0.0001f);
    }

    [TestMethod]
    public void ApplyTerrainSpeedFeats_Snow_DwarfFeat_AppliesTenPercent()
    {
        var culture = AdapterWith(TaomCulturalFeats.EreborSnowSpeedFeat);
        var en = new ExplainedNumber(1f);

        _sut.ApplyTerrainSpeedFeats(culture, TerrainKind.Snow, isNight: false, ref en);

        Assert.AreEqual(1f * (1f + 0.1f), en.ResultNumber, 0.0001f);
    }

    [TestMethod]
    public void ApplyTerrainSpeedFeats_Steppe_KhandFeat_AppliesTenPercent()
    {
        var culture = AdapterWith(TaomCulturalFeats.KhandSteppeSpeedFeat);
        var en = new ExplainedNumber(1f);

        _sut.ApplyTerrainSpeedFeats(culture, TerrainKind.Steppe, isNight: false, ref en);

        Assert.AreEqual(1f * (1f + 0.1f), en.ResultNumber, 0.0001f);
    }

    [TestMethod]
    public void ApplyTerrainSpeedFeats_Desert_HaradFeat_AppliesTenPercent()
    {
        var culture = AdapterWith(TaomCulturalFeats.HaradDesertSpeedFeat);
        var en = new ExplainedNumber(1f);

        _sut.ApplyTerrainSpeedFeats(culture, TerrainKind.Desert, isNight: false, ref en);

        Assert.AreEqual(1f * (1f + 0.1f), en.ResultNumber, 0.0001f);
    }

    [TestMethod]
    public void ApplyTerrainSpeedFeats_Plain_MordorFeat_AppliesFivePercent()
    {
        // Mordor's terrain buff is deliberately smaller (5%) than the 10% others get.
        var culture = AdapterWith(TaomCulturalFeats.MordorPlainSpeedFeat);
        var en = new ExplainedNumber(1f);

        _sut.ApplyTerrainSpeedFeats(culture, TerrainKind.Plain, isNight: false, ref en);

        Assert.AreEqual(1f * (1f + 0.05f), en.ResultNumber, 0.0001f);
    }

    [TestMethod]
    public void ApplyTerrainSpeedFeats_Swamp_MordorFeat_AppliesFivePercent()
    {
        var culture = AdapterWith(TaomCulturalFeats.MordorSwampSpeedFeat);
        var en = new ExplainedNumber(1f);

        _sut.ApplyTerrainSpeedFeats(culture, TerrainKind.Swamp, isNight: false, ref en);

        Assert.AreEqual(1f * (1f + 0.05f), en.ResultNumber, 0.0001f);
    }

    [TestMethod]
    public void ApplyTerrainSpeedFeats_Plain_GondorFeat_AppliesTenPercent()
    {
        var culture = AdapterWith(TaomCulturalFeats.GondorPlainSpeedFeat);
        var en = new ExplainedNumber(1f);

        _sut.ApplyTerrainSpeedFeats(culture, TerrainKind.Plain, isNight: false, ref en);

        Assert.AreEqual(1f * (1f + 0.1f), en.ResultNumber, 0.0001f);
    }

    [TestMethod]
    public void ApplyTerrainSpeedFeats_Night_MordorFeat_AppliesTenPercent()
    {
        var culture = AdapterWith(TaomCulturalFeats.MordorNightSpeedFeat);
        var en = new ExplainedNumber(1f);

        // Night bonus is terrain-independent — passing None still applies it.
        _sut.ApplyTerrainSpeedFeats(culture, TerrainKind.None, isNight: true, ref en);

        Assert.AreEqual(1f * (1f + 0.1f), en.ResultNumber, 0.0001f);
    }

    [TestMethod]
    public void ApplyTerrainSpeedFeats_Day_MordorNightFeat_DoesNotApply()
    {
        var culture = AdapterWith(TaomCulturalFeats.MordorNightSpeedFeat);
        var en = new ExplainedNumber(1f);

        _sut.ApplyTerrainSpeedFeats(culture, TerrainKind.Plain, isNight: false, ref en);

        Assert.AreEqual(1f, en.ResultNumber);
    }

    [TestMethod]
    public void ApplyTerrainSpeedFeats_MordorPlainAtNight_StacksTerrainAndNight()
    {
        var culture = AdapterWith(
            TaomCulturalFeats.MordorPlainSpeedFeat,
            TaomCulturalFeats.MordorNightSpeedFeat);
        var en = new ExplainedNumber(1f);

        _sut.ApplyTerrainSpeedFeats(culture, TerrainKind.Plain, isNight: true, ref en);

        // 5% plain + 10% night, additive ExplainedNumber factors.
        Assert.AreEqual(1f * (1f + 0.05f + 0.1f), en.ResultNumber, 0.0001f);
    }

    [TestMethod]
    public void ApplyRohanInfantryPenalty_NoRohanFeat_DoesNothing()
    {
        var culture = Substitute.For<ICultureFeatAdapter>();
        culture.HasFeat(Arg.Any<FeatObject>()).Returns(false);
        var en = new ExplainedNumber(1f);

        _sut.ApplyRohanInfantryPenalty(culture, mountedCount: 0, totalCount: 10, ref en);

        Assert.AreEqual(1f, en.ResultNumber);
    }

    [TestMethod]
    public void ApplyRohanInfantryPenalty_MajorityInfantry_AppliesPenalty()
    {
        var culture = AdapterWith(TaomCulturalFeats.RohanInfantrySpeedFeat);
        var en = new ExplainedNumber(1f);

        // 4 mounted out of 10 — 40% mounted, so > 50% infantry → penalty applies
        _sut.ApplyRohanInfantryPenalty(culture, mountedCount: 4, totalCount: 10, ref en);

        Assert.AreEqual(
            1f * (1f + TaomCulturalFeats.RohanInfantrySpeedFeat.EffectBonus),
            en.ResultNumber,
            0.0001f);
    }

    [TestMethod]
    public void ApplyRohanInfantryPenalty_MostlyMounted_NoPenalty()
    {
        var culture = AdapterWith(TaomCulturalFeats.RohanInfantrySpeedFeat);
        var en = new ExplainedNumber(1f);

        // 6 mounted out of 10 — 60% mounted → no penalty
        _sut.ApplyRohanInfantryPenalty(culture, mountedCount: 6, totalCount: 10, ref en);

        Assert.AreEqual(1f, en.ResultNumber);
    }

    [TestMethod]
    public void ApplyRohanInfantryPenalty_ZeroTotal_NoPenalty()
    {
        var culture = AdapterWith(TaomCulturalFeats.RohanInfantrySpeedFeat);
        var en = new ExplainedNumber(1f);

        _sut.ApplyRohanInfantryPenalty(culture, mountedCount: 0, totalCount: 0, ref en);

        Assert.AreEqual(1f, en.ResultNumber);
    }

    // ── SettlementProsperity (hearth growth) ───────────────────────────

    [TestMethod]
    public void ApplyHearthGrowthFeats_NegativeResult_SkipsAllFeats()
    {
        var culture = AdapterWith(
            TaomCulturalFeats.RivendellHearthGrowthFeat,
            TaomCulturalFeats.MirkwoodHearthGrowthFeat,
            TaomCulturalFeats.GondorHearthGrowthFeat);
        var en = new ExplainedNumber(-5f);

        _sut.ApplyHearthGrowthFeats(culture, ref en);

        // Negative → all skipped per gamemodel guard
        Assert.AreEqual(-5f, en.ResultNumber);
    }

    [TestMethod]
    public void ApplyHearthGrowthFeats_PositiveResult_RivendellApplies()
    {
        var culture = AdapterWith(TaomCulturalFeats.RivendellHearthGrowthFeat);
        var en = new ExplainedNumber(10f);

        _sut.ApplyHearthGrowthFeats(culture, ref en);

        Assert.AreEqual(
            10f * (1f + TaomCulturalFeats.RivendellHearthGrowthFeat.EffectBonus),
            en.ResultNumber,
            0.0001f);
    }

    [TestMethod]
    public void ApplyHearthGrowthFeats_NullCulture_DoesNothing()
    {
        var en = new ExplainedNumber(10f);
        _sut.ApplyHearthGrowthFeats(null, ref en);
        Assert.AreEqual(10f, en.ResultNumber);
    }

    // ── SettlementMilitia ──────────────────────────────────────────────

    [TestMethod]
    public void ApplyVeteranMilitiaFeats_MirkwoodOnly_AddsBonus()
    {
        var culture = AdapterWith(TaomCulturalFeats.MirkwoodMilitiaProductionFeat);
        var en = new ExplainedNumber(0.1f);

        _sut.ApplyVeteranMilitiaFeats(culture, ref en);

        Assert.AreEqual(
            0.1f + TaomCulturalFeats.MirkwoodMilitiaProductionFeat.EffectBonus,
            en.ResultNumber,
            0.0001f);
    }

    [TestMethod]
    public void ApplyVeteranMilitiaFeats_NullCulture_DoesNothing()
    {
        var en = new ExplainedNumber(0.1f);
        _sut.ApplyVeteranMilitiaFeats(null, ref en);
        Assert.AreEqual(0.1f, en.ResultNumber);
    }

    // ── BuildingConstruction ───────────────────────────────────────────

    [TestMethod]
    public void ApplyConstructionSpeedFeats_EreborOnly_AppliesFactor()
    {
        var culture = AdapterWith(TaomCulturalFeats.EreborConstructionSpeedFeat);
        var en = new ExplainedNumber(20f);

        _sut.ApplyConstructionSpeedFeats(culture, ref en);

        Assert.AreEqual(
            20f * (1f + TaomCulturalFeats.EreborConstructionSpeedFeat.EffectBonus),
            en.ResultNumber,
            0.0001f);
    }

    [TestMethod]
    public void ApplyConstructionSpeedFeats_NullCulture_DoesNothing()
    {
        var en = new ExplainedNumber(20f);
        _sut.ApplyConstructionSpeedFeats(null, ref en);
        Assert.AreEqual(20f, en.ResultNumber);
    }

    // ── VillageProduction ──────────────────────────────────────────────

    [TestMethod]
    public void ApplyVillageProductionFeats_EreborOnly_GeneralProductionApplies()
    {
        var culture = AdapterWith(TaomCulturalFeats.EreborProductionFeat);
        var en = new ExplainedNumber(50f);

        _sut.ApplyVillageProductionFeats(culture, isGrain: false, ref en);

        Assert.AreEqual(
            50f * (1f + TaomCulturalFeats.EreborProductionFeat.EffectBonus),
            en.ResultNumber,
            0.0001f);
    }

    [TestMethod]
    public void ApplyVillageProductionFeats_GundabadGrain_OnlyAppliesWhenGrain()
    {
        var culture = AdapterWith(TaomCulturalFeats.GundabadGrainProductionFeat);
        var en = new ExplainedNumber(50f);

        _sut.ApplyVillageProductionFeats(culture, isGrain: false, ref en);

        Assert.AreEqual(50f, en.ResultNumber);
    }

    [TestMethod]
    public void ApplyVillageProductionFeats_GundabadGrain_AppliesWhenGrain()
    {
        var culture = AdapterWith(TaomCulturalFeats.GundabadGrainProductionFeat);
        var en = new ExplainedNumber(50f);

        _sut.ApplyVillageProductionFeats(culture, isGrain: true, ref en);

        Assert.AreEqual(
            50f * (1f + TaomCulturalFeats.GundabadGrainProductionFeat.EffectBonus),
            en.ResultNumber,
            0.0001f);
    }

    [TestMethod]
    public void ApplyVillageProductionFeats_NullCulture_DoesNothing()
    {
        var en = new ExplainedNumber(50f);
        _sut.ApplyVillageProductionFeats(null, isGrain: true, ref en);
        Assert.AreEqual(50f, en.ResultNumber);
    }

    // ── Caravan ────────────────────────────────────────────────────────

    [TestMethod]
    public void ApplyCaravanCost_NullCulture_ReturnsBaseCost()
    {
        Assert.AreEqual(1000, _sut.ApplyCaravanCost(null, 1000));
    }

    [TestMethod]
    public void ApplyCaravanCost_NoFeat_ReturnsBaseCost()
    {
        var culture = Substitute.For<ICultureFeatAdapter>();
        culture.HasFeat(Arg.Any<FeatObject>()).Returns(false);
        Assert.AreEqual(1000, _sut.ApplyCaravanCost(culture, 1000));
    }

    [TestMethod]
    public void ApplyCaravanCost_UmbarFeat_AppliesFactor()
    {
        var culture = AdapterWith(TaomCulturalFeats.UmbarCheaperCaravansFeat);

        var result = _sut.ApplyCaravanCost(culture, 1000);

        var expected = (int)System.Math.Round(
            1000 * (1f + TaomCulturalFeats.UmbarCheaperCaravansFeat.EffectBonus),
            System.MidpointRounding.AwayFromZero);
        Assert.AreEqual(expected, result);
    }

    // ── BattleReward ───────────────────────────────────────────────────

    [TestMethod]
    public void ApplyRenownFeats_UmbarOnly_AddsFactor()
    {
        var culture = AdapterWith(TaomCulturalFeats.UmbarRenownFeat);
        var en = new ExplainedNumber(50f);

        _sut.ApplyRenownFeats(culture, ref en);

        Assert.AreEqual(
            50f * (1f + TaomCulturalFeats.UmbarRenownFeat.EffectBonus),
            en.ResultNumber,
            0.0001f);
    }

    [TestMethod]
    public void ApplyRenownFeats_NullCulture_DoesNothing()
    {
        var en = new ExplainedNumber(50f);
        _sut.ApplyRenownFeats(null, ref en);
        Assert.AreEqual(50f, en.ResultNumber);
    }

    // ── PartyTroopUpgrade ──────────────────────────────────────────────

    [TestMethod]
    public void ApplyTroopUpgradeFeats_NotMounted_DoesNothing()
    {
        var culture = AdapterWith(
            TaomCulturalFeats.IsengardCheaperRecruitsFeat,
            TaomCulturalFeats.RohanMountedCostFeat);
        var en = new ExplainedNumber(100f);

        _sut.ApplyTroopUpgradeFeats(culture, isMounted: false, ref en);

        Assert.AreEqual(100f, en.ResultNumber);
    }

    [TestMethod]
    public void ApplyTroopUpgradeFeats_MountedIsengard_AppliesFactor()
    {
        var culture = AdapterWith(TaomCulturalFeats.IsengardCheaperRecruitsFeat);
        var en = new ExplainedNumber(100f);

        _sut.ApplyTroopUpgradeFeats(culture, isMounted: true, ref en);

        Assert.AreEqual(
            100f * (1f + TaomCulturalFeats.IsengardCheaperRecruitsFeat.EffectBonus),
            en.ResultNumber,
            0.0001f);
    }

    [TestMethod]
    public void ApplyTroopUpgradeFeats_NullCulture_DoesNothing()
    {
        var en = new ExplainedNumber(100f);
        _sut.ApplyTroopUpgradeFeats(null, isMounted: true, ref en);
        Assert.AreEqual(100f, en.ResultNumber);
    }

    // ── PartySize ──────────────────────────────────────────────────────

    [TestMethod]
    public void ApplyPartySizeFeats_MordorOnly_AppliesFactor()
    {
        var culture = AdapterWith(TaomCulturalFeats.MordorPartySizeFeat);
        var en = new ExplainedNumber(100f);

        _sut.ApplyPartySizeFeats(culture, ref en);

        Assert.AreEqual(
            100f * (1f + TaomCulturalFeats.MordorPartySizeFeat.EffectBonus),
            en.ResultNumber,
            0.0001f);
    }

    [TestMethod]
    public void ApplyPartySizeFeats_NullCulture_DoesNothing()
    {
        var en = new ExplainedNumber(100f);
        _sut.ApplyPartySizeFeats(null, ref en);
        Assert.AreEqual(100f, en.ResultNumber);
    }

    // ── FoodConsumption ────────────────────────────────────────────────

    [TestMethod]
    public void ApplyFoodConsumptionFeats_LothlorienOnly_AppliesFactor()
    {
        var culture = AdapterWith(TaomCulturalFeats.LothlorienFoodConsumptionFeat);
        var en = new ExplainedNumber(10f);

        _sut.ApplyFoodConsumptionFeats(culture, ref en);

        Assert.AreEqual(
            10f * (1f + TaomCulturalFeats.LothlorienFoodConsumptionFeat.EffectBonus),
            en.ResultNumber,
            0.0001f);
    }

    [TestMethod]
    public void ApplyFoodConsumptionFeats_DolGuldurOnly_IncreasesCost()
    {
        var culture = AdapterWith(TaomCulturalFeats.DolGuldurFoodConsumptionFeat);
        var en = new ExplainedNumber(10f);

        _sut.ApplyFoodConsumptionFeats(culture, ref en);

        Assert.AreEqual(
            10f * (1f + TaomCulturalFeats.DolGuldurFoodConsumptionFeat.EffectBonus),
            en.ResultNumber,
            0.0001f);
    }

    [TestMethod]
    public void ApplyFoodConsumptionFeats_NullCulture_DoesNothing()
    {
        var en = new ExplainedNumber(10f);
        _sut.ApplyFoodConsumptionFeats(null, ref en);
        Assert.AreEqual(10f, en.ResultNumber);
    }

    // ── SettlementLoyalty ──────────────────────────────────────────────

    [TestMethod]
    public void ApplyLoyaltyFeats_GondorOnly_AddsBonus()
    {
        var culture = AdapterWith(TaomCulturalFeats.GondorLoyaltyFeat);
        var en = new ExplainedNumber(2f);

        _sut.ApplyLoyaltyFeats(culture, ref en);

        Assert.AreEqual(
            2f + TaomCulturalFeats.GondorLoyaltyFeat.EffectBonus,
            en.ResultNumber,
            0.0001f);
    }

    [TestMethod]
    public void ApplyLoyaltyFeats_AllFiveStack_AddsSum()
    {
        var culture = AdapterWith(
            TaomCulturalFeats.GondorLoyaltyFeat,
            TaomCulturalFeats.EreborLoyaltyFeat,
            TaomCulturalFeats.LothlorienLoyaltyFeat,
            TaomCulturalFeats.RivendellLoyaltyFeat,
            TaomCulturalFeats.RohanLoyaltyFeat);
        var en = new ExplainedNumber(0f);

        _sut.ApplyLoyaltyFeats(culture, ref en);

        var expected =
            TaomCulturalFeats.GondorLoyaltyFeat.EffectBonus +
            TaomCulturalFeats.EreborLoyaltyFeat.EffectBonus +
            TaomCulturalFeats.LothlorienLoyaltyFeat.EffectBonus +
            TaomCulturalFeats.RivendellLoyaltyFeat.EffectBonus +
            TaomCulturalFeats.RohanLoyaltyFeat.EffectBonus;
        Assert.AreEqual(expected, en.ResultNumber, 0.0001f);
    }

    [TestMethod]
    public void ApplyLoyaltyFeats_NullCulture_DoesNothing()
    {
        var en = new ExplainedNumber(2f);
        _sut.ApplyLoyaltyFeats(null, ref en);
        Assert.AreEqual(2f, en.ResultNumber);
    }

    // ── PartyMorale ────────────────────────────────────────────────────

    [TestMethod]
    public void ApplyMoraleFeats_GondorOnly_AddsBonus()
    {
        var culture = AdapterWith(TaomCulturalFeats.GondorMoraleFeat);
        var en = new ExplainedNumber(50f);

        _sut.ApplyMoraleFeats(culture, ref en);

        Assert.AreEqual(
            50f + TaomCulturalFeats.GondorMoraleFeat.EffectBonus,
            en.ResultNumber,
            0.0001f);
    }

    [TestMethod]
    public void ApplyMoraleFeats_NullCulture_DoesNothing()
    {
        var en = new ExplainedNumber(50f);
        _sut.ApplyMoraleFeats(null, ref en);
        Assert.AreEqual(50f, en.ResultNumber);
    }

    // ── Smithing ───────────────────────────────────────────────────────

    [TestMethod]
    public void ApplySmithingFeats_EreborOnly_AppliesFactor()
    {
        var culture = AdapterWith(TaomCulturalFeats.EreborSmithingFeat);
        var en = new ExplainedNumber(100f);

        _sut.ApplySmithingFeats(culture, ref en);

        Assert.AreEqual(
            100f * (1f + TaomCulturalFeats.EreborSmithingFeat.EffectBonus),
            en.ResultNumber,
            0.0001f);
    }

    [TestMethod]
    public void ApplySmithingFeats_NullCulture_DoesNothing()
    {
        var en = new ExplainedNumber(100f);
        _sut.ApplySmithingFeats(null, ref en);
        Assert.AreEqual(100f, en.ResultNumber);
    }

    // ── ClanFinance (tariffs) ──────────────────────────────────────────

    [TestMethod]
    public void ApplyTariffIncomeFeats_UmbarOnly_AppliesFactor()
    {
        var culture = AdapterWith(TaomCulturalFeats.UmbarTariffIncomeFeat);
        var en = new ExplainedNumber(500f);

        _sut.ApplyTariffIncomeFeats(culture, ref en);

        Assert.AreEqual(
            500f * (1f + TaomCulturalFeats.UmbarTariffIncomeFeat.EffectBonus),
            en.ResultNumber,
            0.0001f);
    }

    [TestMethod]
    public void ApplyTariffIncomeFeats_NullCulture_DoesNothing()
    {
        var en = new ExplainedNumber(500f);
        _sut.ApplyTariffIncomeFeats(null, ref en);
        Assert.AreEqual(500f, en.ResultNumber);
    }

    // ── Raid ───────────────────────────────────────────────────────────

    [TestMethod]
    public void ApplyRaidDamageFeats_MordorOnly_AppliesFactor()
    {
        var culture = AdapterWith(TaomCulturalFeats.MordorRaidDamageFeat);
        var en = new ExplainedNumber(100f);

        _sut.ApplyRaidDamageFeats(culture, ref en);

        Assert.AreEqual(
            100f * (1f + TaomCulturalFeats.MordorRaidDamageFeat.EffectBonus),
            en.ResultNumber,
            0.0001f);
    }

    [TestMethod]
    public void ApplyRaidDamageFeats_NullCulture_DoesNothing()
    {
        var en = new ExplainedNumber(100f);
        _sut.ApplyRaidDamageFeats(null, ref en);
        Assert.AreEqual(100f, en.ResultNumber);
    }

    // ── Adapter helpers ────────────────────────────────────────────────

    /// <summary>
    /// Returns a substitute adapter that reports HasFeat==true for every
    /// FeatObject in <paramref name="present"/>, false for any other instance.
    /// </summary>
    private static ICultureFeatAdapter AdapterWith(params FeatObject[] present)
    {
        var adapter = Substitute.For<ICultureFeatAdapter>();
        adapter.HasFeat(Arg.Any<FeatObject>()).Returns(false);
        foreach (var feat in present)
            adapter.HasFeat(feat).Returns(true);
        return adapter;
    }

    /// <summary>
    /// We can't call <c>TaomCulturalFeats.CreateAndRegister()</c> in unit tests
    /// (no <c>Game.Current</c>), but we still need usable <see cref="FeatObject"/>
    /// references with a non-zero <c>EffectBonus</c> for assertions. This helper
    /// constructs a real FeatObject for every static getter on
    /// <see cref="TaomCulturalFeats"/>, mirrors the production EffectBonus values
    /// from <c>InitializeAll()</c>, and stuffs each into its matching backing
    /// field on the singleton instance.
    /// </summary>
    private static bool _featsInitialised;
    private static readonly object _featInitLock = new();

    private static void EnsureFeatsInitialised()
    {
        lock (_featInitLock)
        {
            if (_featsInitialised)
                return;

            var t = typeof(TaomCulturalFeats);
            var instanceField = t.GetField("_instance", BindingFlags.NonPublic | BindingFlags.Static);
            var instance = instanceField!.GetValue(null);
            if (instance == null)
            {
                // TaomCulturalFeats has no declared ctor — use the implicit public parameterless
                // ctor. Activator.CreateInstance reaches it irrespective of access modifier.
                instance = System.Activator.CreateInstance(t, nonPublic: true);
                instanceField.SetValue(null, instance);
            }

            // Map of (backing-field-name, stringId, effectBonus) matching InitializeAll().
            var entries = new (string field, string stringId, float bonus)[]
            {
                ("_ereborGarrisonWage", "taom_erebor_garrison_wage", -0.25f),
                ("_ereborProduction", "taom_erebor_production", 0.1f),
                ("_ereborConstructionSpeed", "taom_erebor_construction_speed", -0.15f),
                ("_ereborLoyalty", "taom_erebor_loyalty", 1f),
                ("_ereborMorale", "taom_erebor_morale", 5f),
                ("_ereborSmithing", "taom_erebor_smithing", -0.3f),

                ("_rivendellArmyInfluence", "taom_rivendell_army_influence", 0.35f),
                ("_rivendellHearthGrowth", "taom_rivendell_hearth_growth", 0.2f),
                ("_rivendellArmyInfluenceCost", "taom_rivendell_army_influence_cost", 0.25f),
                ("_rivendellFoodConsumption", "taom_rivendell_food_consumption", -0.15f),
                ("_rivendellLoyalty", "taom_rivendell_loyalty", 0.5f),

                ("_mirkwoodForestSpeed", "taom_mirkwood_forest_speed", 0.1f),
                ("_mirkwoodMilitiaProduction", "taom_mirkwood_militia_production", 0.25f),
                ("_mirkwoodHearthGrowth", "taom_mirkwood_hearth_growth", -0.2f),
                ("_mirkwoodFoodConsumption", "taom_mirkwood_food_consumption", -0.15f),
                ("_mirkwoodMorale", "taom_mirkwood_morale", 3f),

                ("_lothlorienForestSpeed", "taom_lothlorien_forest_speed", 0.1f),
                ("_lothlorienGarrisonWage", "taom_lothlorien_garrison_wage", -0.2f),
                ("_lothlorienConstructionSpeed", "taom_lothlorien_construction_speed", -0.1f),
                ("_lothlorienFoodConsumption", "taom_lothlorien_food_consumption", -0.15f),
                ("_lothlorienLoyalty", "taom_lothlorien_loyalty", 0.5f),
                ("_lothlorienMorale", "taom_lothlorien_morale", 3f),

                ("_isengardCheaperRecruits", "taom_isengard_cheaper_recruits", -0.15f),
                ("_isengardGarrisonWage", "taom_isengard_garrison_wage", -0.2f),
                ("_isengardDecisionPenalty", "taom_isengard_decision_penalty", 0.25f),
                ("_isengardPartySize", "taom_isengard_party_size", 0.2f),
                ("_isengardConstructionSpeed", "taom_isengard_construction_speed", 0.15f),
                ("_isengardSmithing", "taom_isengard_smithing", -0.2f),
                ("_isengardRaidDamage", "taom_isengard_raid_damage", 0.2f),

                ("_gundabadArmyInfluenceCost", "taom_gundabad_army_influence_cost", -0.4f),
                ("_gundabadGrainProduction", "taom_gundabad_grain_production", 0.15f),
                ("_gundabadWage", "taom_gundabad_wage", 0.1f),
                ("_gundabadPartySize", "taom_gundabad_party_size", 0.3f),
                ("_gundabadRaidDamage", "taom_gundabad_raid_damage", 0.25f),

                ("_umbarCheaperCaravans", "taom_umbar_cheaper_caravans", -0.25f),
                ("_umbarRenown", "taom_umbar_renown", 0.08f),
                ("_umbarWage", "taom_umbar_wage", 0.08f),
                ("_umbarTariffIncome", "taom_umbar_tariff_income", 0.15f),

                ("_dolguldurArmyInfluenceCost", "taom_dolguldur_army_influence_cost", -0.5f),
                ("_dolguldurMilitiaProduction", "taom_dolguldur_militia_production", 0.2f),
                ("_dolguldurConstructionSpeed", "taom_dolguldur_construction_speed", -0.2f),
                ("_dolguldurPartySize", "taom_dolguldur_party_size", 0.25f),
                ("_dolguldurFoodConsumption", "taom_dolguldur_food_consumption", 0.1f),

                ("_gondorGarrisonWage", "taom_gondor_garrison_wage", -0.2f),
                ("_gondorArmyInfluence", "taom_gondor_army_influence", 0.3f),
                ("_gondorHearthGrowth", "taom_gondor_hearth_growth", -0.15f),
                ("_gondorPartySize", "taom_gondor_party_size", 0.1f),
                ("_gondorLoyalty", "taom_gondor_loyalty", 1f),
                ("_gondorMorale", "taom_gondor_morale", 5f),

                ("_mordorArmyInfluenceCost", "taom_mordor_army_influence_cost", -0.6f),
                ("_mordorGrainProduction", "taom_mordor_grain_production", 0.2f),
                ("_mordorWage", "taom_mordor_wage", 0.2f),
                ("_mordorPartySize", "taom_mordor_party_size", 0.3f),
                ("_mordorRaidDamage", "taom_mordor_raid_damage", 0.25f),

                ("_rohanMountedCost", "taom_rohan_mounted_cost", -0.15f),
                ("_rohanMountedWage", "taom_rohan_mounted_wage", -0.15f),
                ("_rohanInfantrySpeed", "taom_rohan_infantry_speed", -0.1f),
                ("_rohanLoyalty", "taom_rohan_loyalty", 0.5f),
                ("_rohanMorale", "taom_rohan_morale", 5f),

                // Terrain movement-speed feats (issue: cultural terrain bonuses)
                ("_ereborSnowSpeed", "taom_erebor_snow_speed", 0.1f),
                ("_rivendellForestSpeed", "taom_rivendell_forest_speed", 0.1f),
                ("_isengardPlainSpeed", "taom_isengard_plain_speed", 0.1f),
                ("_isengardSwampSpeed", "taom_isengard_swamp_speed", 0.1f),
                ("_gundabadSnowSpeed", "taom_gundabad_snow_speed", 0.1f),
                ("_umbarDesertSpeed", "taom_umbar_desert_speed", 0.1f),
                ("_gondorPlainSpeed", "taom_gondor_plain_speed", 0.1f),
                ("_mordorPlainSpeed", "taom_mordor_plain_speed", 0.05f),
                ("_mordorSwampSpeed", "taom_mordor_swamp_speed", 0.05f),
                ("_mordorNightSpeed", "taom_mordor_night_speed", 0.1f),
                ("_rohanPlainSpeed", "taom_rohan_plain_speed", 0.1f),
                ("_dalePlainSpeed", "taom_dale_plain_speed", 0.1f),
                ("_khandSteppeSpeed", "taom_khand_steppe_speed", 0.1f),
                ("_rhunSteppeSpeed", "taom_rhun_steppe_speed", 0.1f),
                ("_haradDesertSpeed", "taom_harad_desert_speed", 0.1f),
                ("_dunlandPlainSpeed", "taom_dunland_plain_speed", 0.1f),
                ("_shaghanaDesertSpeed", "taom_shaghana_desert_speed", 0.1f),
                ("_abanissaDesertSpeed", "taom_abanissa_desert_speed", 0.1f),
            };

            var effectBonusProp = typeof(FeatObject).GetProperty(
                nameof(FeatObject.EffectBonus),
                BindingFlags.Public | BindingFlags.Instance);
            var effectBonusSetter = effectBonusProp!.GetSetMethod(nonPublic: true);

            foreach (var (fieldName, stringId, bonus) in entries)
            {
                var feat = new FeatObject(stringId);
                effectBonusSetter!.Invoke(feat, new object[] { bonus });
                var f = t.GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Instance);
                f!.SetValue(instance, feat);
            }

            _featsInitialised = true;
        }
    }
}
