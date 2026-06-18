using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TaleWorlds.CampaignSystem;
using TAOM.Features.SettlementFood;

namespace TAOM.Tests.Features.SettlementFood;

[TestClass]
public class SettlementFoodServiceTests
{
    private SettlementFoodService _sut = null!;

    [TestInitialize]
    public void Setup() => _sut = new SettlementFoodService();

    private static TownFoodSnapshot Snapshot(
        bool isTown = true,
        bool isUnderSiege = false,
        int rawGarrison = 0,
        int weightedGarrison = 0,
        params int[] normalVillageHearthLevels) =>
        new TownFoodSnapshot(isTown, isUnderSiege, rawGarrison, weightedGarrison,
            new List<int>(normalVillageHearthLevels));

    private static SettlementFoodConfig Vanilla() => new SettlementFoodConfig();

    // --- Master gate ---

    [TestMethod]
    public void ComputeFoodDelta_Disabled_ReturnsZero()
    {
        // Even with inflated garrison + non-vanilla production knobs, disabled => no adjustment.
        var snapshot = Snapshot(rawGarrison: 200, weightedGarrison: 400, normalVillageHearthLevels: new[] { 1, 2 });
        var config = new SettlementFoodConfig { TownBaseFood = 50f, VillageFoodMultiplier = 20f, FlatFoodBonus = 30f };

        Assert.AreEqual(0f, _sut.ComputeFoodDelta(snapshot, config, enabled: false), 0.001f);
    }

    // --- Garrison raw-count correction (the troop-weight leak fix) ---

    [TestMethod]
    public void ComputeFoodDelta_GarrisonInflated_AddsBackOverCountDividedByGarrisonDivisor()
    {
        // Base subtracted weighted/20; we want raw/20. Correction = (400-200)/20 = +10.
        var snapshot = Snapshot(rawGarrison: 200, weightedGarrison: 400);

        Assert.AreEqual(10f, _sut.ComputeFoodDelta(snapshot, Vanilla(), enabled: true), 0.001f);
    }

    [TestMethod]
    public void ComputeFoodDelta_GarrisonNotInflated_NoCorrection()
    {
        // Troop weight off / all weight-1 troops => weighted == raw => no correction, vanilla knobs => 0.
        var snapshot = Snapshot(rawGarrison: 250, weightedGarrison: 250);

        Assert.AreEqual(0f, _sut.ComputeFoodDelta(snapshot, Vanilla(), enabled: true), 0.001f);
    }

    [TestMethod]
    public void ComputeFoodDelta_RaisedGarrisonDivisor_ShrinksCorrection()
    {
        // Correction uses the (effective) garrison divisor so it stays consistent with base's term.
        var snapshot = Snapshot(rawGarrison: 200, weightedGarrison: 400);
        var config = new SettlementFoodConfig { GarrisonFoodDivisor = 40 };

        Assert.AreEqual(5f, _sut.ComputeFoodDelta(snapshot, config, enabled: true), 0.001f); // (400-200)/40
    }

    [TestMethod]
    public void ComputeFoodDelta_GarrisonCorrection_AppliesEvenUnderSiege()
    {
        // The weight inflation is a bug regardless of siege; the correction is NOT siege-gated.
        var snapshot = Snapshot(isUnderSiege: true, rawGarrison: 200, weightedGarrison: 400);

        Assert.AreEqual(10f, _sut.ComputeFoodDelta(snapshot, Vanilla(), enabled: true), 0.001f);
    }

    // --- Production knobs (siege-gated) ---

    [TestMethod]
    public void ComputeFoodDelta_TownProductionKnobs_AddsBasePlusVillagePlusFlat()
    {
        // base (25-15)=10; villages (1+1)*(10-6)=8 + (2+1)*(10-6)=12 => 20; flat +5 => 35.
        var snapshot = Snapshot(isTown: true, normalVillageHearthLevels: new[] { 1, 2 });
        var config = new SettlementFoodConfig { TownBaseFood = 25f, VillageFoodMultiplier = 10f, FlatFoodBonus = 5f };

        Assert.AreEqual(35f, _sut.ComputeFoodDelta(snapshot, config, enabled: true), 0.001f);
    }

    [TestMethod]
    public void ComputeFoodDelta_Castle_UsesCastleBaseFoodDelta()
    {
        // Castle base (20-10)=10; no villages; vanilla mult/flat => 10.
        var snapshot = Snapshot(isTown: false);
        var config = new SettlementFoodConfig { CastleBaseFood = 20f };

        Assert.AreEqual(10f, _sut.ComputeFoodDelta(snapshot, config, enabled: true), 0.001f);
    }

    [TestMethod]
    public void ComputeFoodDelta_BelowVanillaTownBaseFood_ProducesNegativeDelta()
    {
        // Replacement semantics (NOT relief-only): the base/village knobs are absolute values that
        // REPLACE the vanilla constant, so a below-vanilla value intentionally lowers production —
        // consistent with the divisors, which also tune both directions. Codex review 2026-06-18 (LOW).
        var snapshot = Snapshot(isTown: true);
        var config = new SettlementFoodConfig { TownBaseFood = 0f };

        Assert.AreEqual(-15f, _sut.ComputeFoodDelta(snapshot, config, enabled: true), 0.001f); // 0 - 15
    }

    [TestMethod]
    public void ComputeFoodDelta_VanillaVillageMultiplier_NoVillageDelta()
    {
        var snapshot = Snapshot(normalVillageHearthLevels: new[] { 0, 1, 2 });

        Assert.AreEqual(0f, _sut.ComputeFoodDelta(snapshot, Vanilla(), enabled: true), 0.001f);
    }

    [TestMethod]
    public void ComputeFoodDelta_UnderSiege_SuppressesProductionKnobs()
    {
        // Production is lost under siege (vanilla); only the garrison correction survives.
        var snapshot = Snapshot(isUnderSiege: true, rawGarrison: 200, weightedGarrison: 400,
            normalVillageHearthLevels: new[] { 1, 2 });
        var config = new SettlementFoodConfig { TownBaseFood = 50f, VillageFoodMultiplier = 20f, FlatFoodBonus = 30f };

        // garrison correction only: (400-200)/20 = 10
        Assert.AreEqual(10f, _sut.ComputeFoodDelta(snapshot, config, enabled: true), 0.001f);
    }

    [TestMethod]
    public void ComputeFoodDelta_CombinedGarrisonAndProduction_SumsBoth()
    {
        var snapshot = Snapshot(isTown: true, rawGarrison: 200, weightedGarrison: 300,
            normalVillageHearthLevels: new[] { 2 });
        var config = new SettlementFoodConfig { TownBaseFood = 20f, VillageFoodMultiplier = 8f, FlatFoodBonus = 3f };

        // garrison (300-200)/20=5; base (20-15)=5; village (2+1)*(8-6)=6; flat 3 => 19
        Assert.AreEqual(19f, _sut.ComputeFoodDelta(snapshot, config, enabled: true), 0.001f);
    }

    // --- ApplyFoodAdjustment (ExplainedNumber integration) ---

    [TestMethod]
    public void ApplyFoodAdjustment_NonZeroDelta_AddsToResult()
    {
        var snapshot = Snapshot(rawGarrison: 200, weightedGarrison: 400);
        var result = new ExplainedNumber(100f);

        _sut.ApplyFoodAdjustment(snapshot, Vanilla(), enabled: true, ref result, includeDescriptions: false);

        Assert.AreEqual(110f, result.ResultNumber, 0.001f);
    }

    [TestMethod]
    public void ApplyFoodAdjustment_ZeroDelta_LeavesResultUnchanged()
    {
        var snapshot = Snapshot(rawGarrison: 250, weightedGarrison: 250);
        var result = new ExplainedNumber(100f);

        _sut.ApplyFoodAdjustment(snapshot, Vanilla(), enabled: true, ref result, includeDescriptions: false);

        Assert.AreEqual(100f, result.ResultNumber, 0.001f);
    }

    [TestMethod]
    public void ApplyFoodAdjustment_Disabled_LeavesResultUnchanged()
    {
        var snapshot = Snapshot(rawGarrison: 200, weightedGarrison: 400);
        var result = new ExplainedNumber(100f);

        _sut.ApplyFoodAdjustment(snapshot, Vanilla(), enabled: false, ref result, includeDescriptions: false);

        Assert.AreEqual(100f, result.ResultNumber, 0.001f);
    }

    // --- Default config = vanilla constants ---

    [TestMethod]
    public void DefaultConfig_MatchesVanillaFoodModelConstants()
    {
        var c = new SettlementFoodConfig();
        Assert.AreEqual(20, c.GarrisonFoodDivisor, "vanilla NumberOfMenOnGarrisonToEatOneFood");
        Assert.AreEqual(40, c.ProsperityFoodDivisor, "vanilla NumberOfProsperityToEatOneFood");
        Assert.AreEqual(15f, c.TownBaseFood, 0.001f, "vanilla town lands-around food");
        Assert.AreEqual(10f, c.CastleBaseFood, 0.001f, "vanilla castle lands-around food");
        Assert.AreEqual(6f, c.VillageFoodMultiplier, 0.001f, "vanilla (hearthLevel+1)*6");
        Assert.AreEqual(0f, c.FlatFoodBonus, 0.001f);
        Assert.AreEqual(300, c.FoodStocksUpperLimit, "vanilla FoodStocksUpperLimit");
        Assert.AreEqual(150, c.CastleFoodStockUpperLimitBonus, "vanilla CastleFoodStockUpperLimitBonus");
    }
}
