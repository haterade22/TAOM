using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Core.Logging;
using TAOM.Features.CaravanTrade;
using TAOM.Features.Execution;

namespace TAOM.Tests.Features.CaravanTrade;

[TestClass]
public class CaravanTradeServiceTests
{
    private ICaravanTradeSettingsProvider _settings = null!;
    private IAlignmentService _alignment = null!;
    private IModLogger _logger = null!;
    private CaravanTradeService _sut = null!;

    [TestInitialize]
    public void Setup()
    {
        _settings = Substitute.For<ICaravanTradeSettingsProvider>();
        _alignment = Substitute.For<IAlignmentService>();
        _logger = Substitute.For<IModLogger>();

        // Default: feature on, applies to everyone, mid-range tuning.
        _settings.Enabled.Returns(true);
        _settings.ApplyToPlayerCaravans.Returns(true);
        _settings.RangeMultiplier.Returns(1.6f);
        _settings.DistanceDecayExponent.Returns(0.5f);
        _settings.NearFieldFlattenDays.Returns(2.0f);
        _settings.MaxCompensation.Returns(6.0f);
        _settings.AntiShuttlePenalty.Returns(0.5f);
        _settings.HomeDistanceReweight.Returns(true);
        _settings.WarTradePolicy.Returns(WarTradePolicy.SameAlignmentAndNeutral);
        _settings.BudgetFactorFloor.Returns(0.35f);
        _settings.InitialTradeGold.Returns(15000);
        _settings.MaxGoldPerCategory.Returns(1500);

        _sut = new CaravanTradeService(_settings, _alignment, _logger);
    }

    // ---------------- ReweightTradeScore ----------------

    [TestMethod]
    public void ReweightTradeScore_Disabled_ReturnsRawScore()
    {
        _settings.Enabled.Returns(false);
        Assert.AreEqual(42f, _sut.ReweightTradeScore(42f, 3f, false, false, 1f, false), 0.0001f);
    }

    [TestMethod]
    public void ReweightTradeScore_PlayerCaravanWhenPlayerScopeOff_ReturnsRawScore()
    {
        _settings.ApplyToPlayerCaravans.Returns(false);
        Assert.AreEqual(42f, _sut.ReweightTradeScore(42f, 3f, false, false, 1f, isPlayerCaravan: true), 0.0001f);
    }

    [TestMethod]
    public void ReweightTradeScore_RejectionScore_PassesThroughUnchanged()
    {
        // Vanilla returns -1 for non-navigable / distance-cut rejects.
        Assert.AreEqual(-1f, _sut.ReweightTradeScore(-1f, 3f, false, false, 1f, false), 0.0001f);
        Assert.AreEqual(0f, _sut.ReweightTradeScore(0f, 3f, false, false, 1f, false), 0.0001f);
    }

    [TestMethod]
    public void ReweightTradeScore_NaNRawScore_ReturnsRawScore()
    {
        // Positive-requirement gate: NaN must fail into the vanilla passthrough.
        Assert.IsTrue(float.IsNaN(_sut.ReweightTradeScore(float.NaN, 3f, false, false, 1f, false)));
    }

    [TestMethod]
    public void ReweightTradeScore_NonPositiveDays_ReturnsRawScore()
    {
        // days<=0 skips the distance reweight; a neutral recency factor leaves rawScore unchanged.
        Assert.AreEqual(10f, _sut.ReweightTradeScore(10f, 0f, false, false, 1f, false), 0.0001f);
    }

    [TestMethod]
    public void ReweightTradeScore_NaNDays_ReturnsRawScore()
    {
        Assert.AreEqual(10f, _sut.ReweightTradeScore(10f, float.NaN, false, false, 1f, false), 0.0001f);
    }

    [TestMethod]
    public void ReweightTradeScore_NavalCaravan_ReturnsRawScoreUnchanged()
    {
        // Naval uses a different vanilla distance factor; the shuttle is a land problem.
        Assert.AreEqual(10f, _sut.ReweightTradeScore(10f, 3f, isNaval: true, false, 1f, false), 0.0001f);
    }

    [TestMethod]
    public void ReweightTradeScore_HomeTown_NowCompressed()
    {
        // Home-rubber-band regression: with HomeDistanceReweight on (default), the home town is
        // distance-compressed identically to a non-home town at the same days — it no longer passes
        // through raw with its near-field proximity advantage.
        float home = _sut.ReweightTradeScore(10f, 1f, false, isHomeTown: true, 1f, false);
        float nonHome = _sut.ReweightTradeScore(10f, 1f, false, isHomeTown: false, 1f, false);
        Assert.AreEqual(nonHome, home, 0.0001f);
        Assert.IsTrue(home < 10f, $"home should be compressed below raw, was {home}");
    }

    [TestMethod]
    public void ReweightTradeScore_HomeTown_EscapeHatchOff_PassesDistanceButRecencyStillApplies()
    {
        // HomeDistanceReweight off restores the old home distance exemption: raw distance passes
        // through, but the recency penalty still applies to home.
        _settings.HomeDistanceReweight.Returns(false);
        Assert.AreEqual(10f, _sut.ReweightTradeScore(10f, 1f, false, isHomeTown: true, 1f, false), 0.0001f);
        Assert.AreEqual(5f, _sut.ReweightTradeScore(10f, 1f, false, isHomeTown: true, 0.5f, false), 0.0001f);
    }

    [TestMethod]
    public void ReweightTradeScore_RecencyAppliedToHome()
    {
        // With the escape hatch on (default), home is compressed AND recency-penalized.
        float noPenalty = _sut.ReweightTradeScore(10f, 1f, false, isHomeTown: true, 1f, false);
        float penalized = _sut.ReweightTradeScore(10f, 1f, false, isHomeTown: true, 0.5f, false);
        Assert.AreEqual(noPenalty * 0.5f, penalized, 0.0001f);
    }

    [TestMethod]
    public void ReweightTradeScore_LandTown_AppliesStripAndReweight()
    {
        // m = days / (flatten+days)^alpha = 3 / (2+3)^0.5 = 3 / 2.23607 = 1.34164; result = 10 * 1.34164.
        float result = _sut.ReweightTradeScore(10f, 3f, false, false, 1f, false);
        Assert.AreEqual(13.4164f, result, 0.001f);
    }

    [TestMethod]
    public void ReweightTradeScore_MoreProfitableFarTown_BeatsCloseTown_WhereVanillaWouldNot()
    {
        // Vanilla rawScore already embeds 1/days. Near town: days=1, base profit 100 -> rawScore 100.
        // Far town: days=5, base profit 200 -> rawScore 40. Vanilla picks near (100 > 40) despite the
        // far town being twice as profitable per trip; after the reweight the far town wins.
        float near = _sut.ReweightTradeScore(100f, 1f, false, false, 1f, false);
        float far = _sut.ReweightTradeScore(40f, 5f, false, false, 1f, false);
        Assert.IsTrue(far > near, $"expected far({far}) > near({near}) after reweight");
    }

    [TestMethod]
    public void ReweightTradeScore_EqualBaseProfit_StillPrefersNear_ButCompressed()
    {
        // Equal base profit P0=100: near rawScore=100/1, far rawScore=100/5=20.
        float near = _sut.ReweightTradeScore(100f, 1f, false, false, 1f, false);
        float far = _sut.ReweightTradeScore(20f, 5f, false, false, 1f, false);
        Assert.IsTrue(near > far, "near should still edge out an equally-profitable far town");
        Assert.IsTrue(near / far < 2.0f, $"advantage should be compressed well below vanilla's 5x (was {near / far:F2})");
    }

    [TestMethod]
    public void ReweightTradeScore_VeryFarTown_MultiplierClampedToMaxCompensation()
    {
        // m = 1000/(2+1000)^0.5 = 31.6, clamped to maxCompensation 6 -> result = 10 * 6.
        float result = _sut.ReweightTradeScore(10f, 1000f, false, false, 1f, false);
        Assert.AreEqual(60f, result, 0.01f);
    }

    [TestMethod]
    public void ReweightTradeScore_RecencyFactor_MultipliesResult()
    {
        // A recency factor multiplies the reweighted result (the working anti-shuttle penalty).
        float full = _sut.ReweightTradeScore(10f, 3f, false, false, 1f, false);
        float cut = _sut.ReweightTradeScore(10f, 3f, false, false, 0.65f, false);
        Assert.AreEqual(full * 0.65f, cut, 0.001f);
    }

    [TestMethod]
    public void ReweightTradeScore_NaNRecencyFactor_NoPenaltyApplied()
    {
        // Engine-Float gate: a NaN factor must be ignored (finite result == the un-penalized reweight).
        float full = _sut.ReweightTradeScore(10f, 3f, false, false, 1f, false);
        float withNaN = _sut.ReweightTradeScore(10f, 3f, false, false, float.NaN, false);
        Assert.AreEqual(full, withNaN, 0.0001f);
    }

    [TestMethod]
    public void ReweightTradeScore_RecencyFactorOutOfRange_Ignored()
    {
        float full = _sut.ReweightTradeScore(10f, 3f, false, false, 1f, false);
        Assert.AreEqual(full, _sut.ReweightTradeScore(10f, 3f, false, false, 1.5f, false), 0.0001f);
        Assert.AreEqual(full, _sut.ReweightTradeScore(10f, 3f, false, false, -0.1f, false), 0.0001f);
    }

    // ---------------- ScaleVeryFarDistance ----------------

    [TestMethod]
    public void ScaleVeryFarDistance_Disabled_ReturnsVanilla()
    {
        _settings.Enabled.Returns(false);
        Assert.AreEqual(12f, _sut.ScaleVeryFarDistance(12f), 0.0001f);
    }

    [TestMethod]
    public void ScaleVeryFarDistance_Enabled_ScalesByRangeMultiplier()
    {
        Assert.AreEqual(19.2f, _sut.ScaleVeryFarDistance(12f), 0.0001f); // 12 * 1.6
    }

    // ---------------- AllowWartimeTrade ----------------

    [TestMethod]
    public void AllowWartimeTrade_Disabled_ReturnsFalse()
    {
        _settings.Enabled.Returns(false);
        Assert.IsFalse(_sut.AllowWartimeTrade("gondor", "gondor", "rohan", "rohan", false));
    }

    [TestMethod]
    public void AllowWartimeTrade_PlayerCaravanWhenPlayerScopeOff_ReturnsFalse()
    {
        _settings.ApplyToPlayerCaravans.Returns(false);
        Assert.IsFalse(_sut.AllowWartimeTrade("gondor", "gondor", "rohan", "rohan", isPlayerCaravan: true));
    }

    [TestMethod]
    public void AllowWartimeTrade_PolicyNone_ReturnsFalse()
    {
        _settings.WarTradePolicy.Returns(WarTradePolicy.None);
        Assert.IsFalse(_sut.AllowWartimeTrade("gondor", "gondor", "rohan", "rohan", false));
    }

    [TestMethod]
    public void AllowWartimeTrade_PolicyIgnoreWar_ReturnsTrue()
    {
        _settings.WarTradePolicy.Returns(WarTradePolicy.IgnoreWar);
        Assert.IsTrue(_sut.AllowWartimeTrade("gondor", "gondor", "mordor", "mordor", false));
    }

    [TestMethod]
    public void AllowWartimeTrade_SameAlignment_SameSide_ReturnsTrue()
    {
        // Free caravan reaching another Free town despite the war.
        _alignment.GetKingdomSide("empire_w").Returns(FactionSide.Free);
        _alignment.GetKingdomSide("vlandia").Returns(FactionSide.Free);
        Assert.IsTrue(_sut.AllowWartimeTrade("empire_w", "gondor", "vlandia", "vlandia", false));
    }

    [TestMethod]
    public void AllowWartimeTrade_SameAlignment_OppositeSides_ReturnsFalse()
    {
        // A Free caravan must NOT resupply an Evil town.
        _alignment.GetKingdomSide("empire_w").Returns(FactionSide.Free);
        _alignment.GetKingdomSide("empire_s").Returns(FactionSide.Evil);
        Assert.IsFalse(_sut.AllowWartimeTrade("empire_w", "gondor", "empire_s", "mordor", false));
    }

    [TestMethod]
    public void AllowWartimeTrade_SameAlignment_NeutralCaravan_ReturnsTrue()
    {
        // Neutral (Umbar etc.) trades with anyone — regression guard for the AreEnemyAlignments
        // inversion (which treats Neutral as an enemy of everyone). This is the bug the deep-review
        // data-flow agent caught: the shipped default policy silently blocked neutral trade.
        _alignment.GetKingdomSide("umbar").Returns(FactionSide.Neutral);
        _alignment.GetCultureSide("umbar").Returns(FactionSide.Neutral);
        _alignment.GetKingdomSide("empire_w").Returns(FactionSide.Free);
        Assert.IsTrue(_sut.AllowWartimeTrade("umbar", "umbar", "empire_w", "gondor", false));
    }

    [TestMethod]
    public void AllowWartimeTrade_SameAlignment_NeutralTarget_ReturnsTrue()
    {
        _alignment.GetKingdomSide("empire_s").Returns(FactionSide.Evil);
        _alignment.GetKingdomSide("umbar").Returns(FactionSide.Neutral);
        _alignment.GetCultureSide("umbar").Returns(FactionSide.Neutral);
        Assert.IsTrue(_sut.AllowWartimeTrade("empire_s", "mordor", "umbar", "umbar", false));
    }

    [TestMethod]
    public void AllowWartimeTrade_PlayerFoundedKingdom_SidedByCulture_BlocksAcrossLine()
    {
        // A player-founded kingdom (id "new_kingdom") is absent from alignment.json -> GetKingdomSide
        // returns Neutral, which would wrongly let it trade across the Free/Evil line. Culture fallback
        // sides it Free (gondor culture), so it must NOT trade with an Evil town. (Codex MED, mirrors
        // WarOfTheRingMomentum's player-founded-kingdom culture fallback.)
        _alignment.GetKingdomSide("new_kingdom").Returns(FactionSide.Neutral);
        _alignment.GetCultureSide("gondor").Returns(FactionSide.Free);
        _alignment.GetKingdomSide("empire_s").Returns(FactionSide.Evil);
        Assert.IsFalse(_sut.AllowWartimeTrade("new_kingdom", "gondor", "empire_s", "mordor", false));
    }

    [TestMethod]
    public void AllowWartimeTrade_PlayerFoundedKingdom_SidedByCulture_AllowsSameSide()
    {
        _alignment.GetKingdomSide("new_kingdom").Returns(FactionSide.Neutral);
        _alignment.GetCultureSide("gondor").Returns(FactionSide.Free);
        _alignment.GetKingdomSide("vlandia").Returns(FactionSide.Free);
        Assert.IsTrue(_sut.AllowWartimeTrade("new_kingdom", "gondor", "vlandia", "vlandia", false));
    }

    // ---------------- ApplyBudgetFactorFloor ----------------

    [TestMethod]
    public void ApplyBudgetFactorFloor_Disabled_ReturnsVanilla()
    {
        _settings.Enabled.Returns(false);
        Assert.AreEqual(0.1f, _sut.ApplyBudgetFactorFloor(0.1f, false), 0.0001f);
    }

    [TestMethod]
    public void ApplyBudgetFactorFloor_PlayerScopeOff_ReturnsVanilla()
    {
        _settings.ApplyToPlayerCaravans.Returns(false);
        Assert.AreEqual(0.1f, _sut.ApplyBudgetFactorFloor(0.1f, isPlayerCaravan: true), 0.0001f);
    }

    [TestMethod]
    public void ApplyBudgetFactorFloor_NaN_ReturnsVanilla()
    {
        Assert.IsTrue(float.IsNaN(_sut.ApplyBudgetFactorFloor(float.NaN, false)));
    }

    [TestMethod]
    public void ApplyBudgetFactorFloor_BelowFloor_ReturnsFloor()
    {
        Assert.AreEqual(0.35f, _sut.ApplyBudgetFactorFloor(0.1f, false), 0.0001f);
    }

    [TestMethod]
    public void ApplyBudgetFactorFloor_AboveFloor_ReturnsVanilla()
    {
        Assert.AreEqual(0.8f, _sut.ApplyBudgetFactorFloor(0.8f, false), 0.0001f);
    }

    // ---------------- ResolveInitialTradeGold ----------------

    [TestMethod]
    public void ResolveInitialTradeGold_Disabled_ReturnsVanilla()
    {
        _settings.Enabled.Returns(false);
        Assert.AreEqual(10000, _sut.ResolveInitialTradeGold(10000, false));
    }

    [TestMethod]
    public void ResolveInitialTradeGold_PlayerScopeOff_ReturnsVanilla()
    {
        _settings.ApplyToPlayerCaravans.Returns(false);
        Assert.AreEqual(10000, _sut.ResolveInitialTradeGold(10000, isPlayerCaravan: true));
    }

    [TestMethod]
    public void ResolveInitialTradeGold_VanillaBelowFloor_ReturnsFloor()
    {
        Assert.AreEqual(15000, _sut.ResolveInitialTradeGold(10000, false));
    }

    [TestMethod]
    public void ResolveInitialTradeGold_VanillaAboveFloor_NeverLowers()
    {
        // Large caravan / main hero bonus must be preserved.
        Assert.AreEqual(22500, _sut.ResolveInitialTradeGold(22500, false));
    }

    // ---------------- ResolveMaxGoldPerCategory ----------------

    [TestMethod]
    public void ResolveMaxGoldPerCategory_Disabled_ReturnsVanilla()
    {
        _settings.Enabled.Returns(false);
        Assert.AreEqual(1500, _sut.ResolveMaxGoldPerCategory(1500, false));
    }

    [TestMethod]
    public void ResolveMaxGoldPerCategory_Enabled_ReturnsConfiguredValue()
    {
        _settings.MaxGoldPerCategory.Returns(2500);
        Assert.AreEqual(2500, _sut.ResolveMaxGoldPerCategory(1500, false));
    }
}
