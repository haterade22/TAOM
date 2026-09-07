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
        float prosperity = 0f,
        params int[] normalVillageHearthLevels) =>
        new TownFoodSnapshot(isTown, isUnderSiege, rawGarrison, weightedGarrison,
            new List<int>(normalVillageHearthLevels), prosperity);

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

    // --- Hinterland production term (prosperity-scaled) ---

    [TestMethod]
    public void ComputeFoodDelta_HinterlandTerm_AddsProsperityTimesRate()
    {
        // The whole point: production now scales with prosperity, which vanilla only ever consumes by.
        var snapshot = Snapshot(isTown: true, prosperity: 4000f);
        var config = new SettlementFoodConfig { HinterlandFoodPerProsperity = 0.02f };

        Assert.AreEqual(80f, _sut.ComputeFoodDelta(snapshot, config, enabled: true), 0.001f); // 4000 * 0.02
    }

    [TestMethod]
    public void ComputeFoodDelta_HinterlandTerm_DefaultConfigIsVanilla()
    {
        // Default rate is 0, so an unedited config adds nothing however prosperous the fief is.
        var snapshot = Snapshot(isTown: true, prosperity: 5100f);

        Assert.AreEqual(0f, _sut.ComputeFoodDelta(snapshot, Vanilla(), enabled: true), 0.001f);
    }

    [TestMethod]
    public void ComputeFoodDelta_HinterlandTerm_SuppressedUnderSiege()
    {
        // Vanilla drops ALL production while besieged; the hinterland term must not smuggle any back
        // in, or a besieged high-prosperity town would be MORE food-secure than a peaceful one.
        var snapshot = Snapshot(isTown: true, isUnderSiege: true, prosperity: 4000f);
        var config = new SettlementFoodConfig { HinterlandFoodPerProsperity = 0.02f };

        Assert.AreEqual(0f, _sut.ComputeFoodDelta(snapshot, config, enabled: true), 0.001f);
    }

    [TestMethod]
    public void ComputeFoodDelta_HinterlandTerm_AppliesToCastlesToo()
    {
        var snapshot = Snapshot(isTown: false, prosperity: 950f);
        var config = new SettlementFoodConfig { HinterlandFoodPerProsperity = 0.02f };

        Assert.AreEqual(19f, _sut.ComputeFoodDelta(snapshot, config, enabled: true), 0.001f); // 950 * 0.02
    }

    [TestMethod]
    public void ComputeFoodDelta_HinterlandTerm_ComposesWithBaseVillageAndFlat()
    {
        // Orthanc under the shipped tuning: 1 village at hearth level 1, prosperity 4000.
        var snapshot = Snapshot(isTown: true, prosperity: 4000f, normalVillageHearthLevels: new[] { 1 });
        var config = new SettlementFoodConfig
        {
            TownBaseFood = 30f,
            VillageFoodMultiplier = 8f,
            FlatFoodBonus = 5f,
            HinterlandFoodPerProsperity = 0.02f,
        };

        // base (30-15)=15; village (1+1)*(8-6)=4; flat 5; hinterland 4000*0.02=80 => 104
        Assert.AreEqual(104f, _sut.ComputeFoodDelta(snapshot, config, enabled: true), 0.001f);
    }

    [TestMethod]
    public void ComputeFoodDelta_HinterlandTerm_Disabled_ReturnsZero()
    {
        var snapshot = Snapshot(isTown: true, prosperity: 4000f);
        var config = new SettlementFoodConfig { HinterlandFoodPerProsperity = 0.02f };

        Assert.AreEqual(0f, _sut.ComputeFoodDelta(snapshot, config, enabled: false), 0.001f);
    }

    // --- Engine-float safety on the prosperity input ---
    //
    // Town.Prosperity is engine-sourced and its setter only floors at 0 (`if (_prosperity < 0f)`),
    // which NaN passes, so a NaN is storable. If one reached the food delta it would poison the
    // ExplainedNumber, and Town.DailyTick's clamps (`< 0f`, `> cap`) are BOTH false for NaN, so
    // FoodStocks would stay NaN forever in a [SaveableProperty]. csharp-architecture.md
    // "Engine-Float Decision Gates" makes gating this mandatory.

    [TestMethod]
    public void ComputeFoodDelta_NaNProsperity_SkipsHinterlandAndStaysFinite()
    {
        var snapshot = Snapshot(isTown: true, prosperity: float.NaN);
        var config = new SettlementFoodConfig { HinterlandFoodPerProsperity = 0.02f };

        var delta = _sut.ComputeFoodDelta(snapshot, config, enabled: true);

        Assert.IsFalse(float.IsNaN(delta), "a NaN prosperity must never produce a NaN food delta");
        Assert.AreEqual(0f, delta, 0.001f);
    }

    [TestMethod]
    public void ComputeFoodDelta_InfiniteProsperity_SkipsHinterlandAndStaysFinite()
    {
        var snapshot = Snapshot(isTown: true, prosperity: float.PositiveInfinity);
        var config = new SettlementFoodConfig { HinterlandFoodPerProsperity = 0.02f };

        var delta = _sut.ComputeFoodDelta(snapshot, config, enabled: true);

        Assert.IsFalse(float.IsInfinity(delta), "an infinite prosperity must never produce an infinite delta");
        Assert.AreEqual(0f, delta, 0.001f);
    }

    [TestMethod]
    public void ComputeFoodDelta_NaNProsperity_OtherKnobsStillApply()
    {
        // Only the prosperity-dependent term is dropped; the rest of the tuning survives, so a
        // garbage prosperity degrades the feature rather than disabling it.
        var snapshot = Snapshot(isTown: true, prosperity: float.NaN, normalVillageHearthLevels: new[] { 1 });
        var config = new SettlementFoodConfig
        {
            TownBaseFood = 30f,
            VillageFoodMultiplier = 8f,
            FlatFoodBonus = 5f,
            HinterlandFoodPerProsperity = 0.02f,
        };

        // base (30-15)=15; village (1+1)*(8-6)=4; flat 5; hinterland skipped => 24
        Assert.AreEqual(24f, _sut.ComputeFoodDelta(snapshot, config, enabled: true), 0.001f);
    }

    [TestMethod]
    public void ApplyFoodAdjustment_NaNProsperity_LeavesResultFiniteAndUnchanged()
    {
        var snapshot = Snapshot(isTown: true, prosperity: float.NaN);
        var config = new SettlementFoodConfig { HinterlandFoodPerProsperity = 0.02f };
        var result = new ExplainedNumber(100f);

        _sut.ApplyFoodAdjustment(snapshot, config, enabled: true, ref result, includeDescriptions: false);

        Assert.IsFalse(float.IsNaN(result.ResultNumber), "a NaN must never reach the engine's ExplainedNumber");
        Assert.AreEqual(100f, result.ResultNumber, 0.001f);
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
        Assert.AreEqual(0f, c.HinterlandFoodPerProsperity, 0.000001f, "vanilla has no hinterland term");
        Assert.AreEqual(300, c.FoodStocksUpperLimit, "vanilla FoodStocksUpperLimit");
        Assert.AreEqual(150, c.CastleFoodStockUpperLimitBonus, "vanilla CastleFoodStockUpperLimitBonus");
    }
}
