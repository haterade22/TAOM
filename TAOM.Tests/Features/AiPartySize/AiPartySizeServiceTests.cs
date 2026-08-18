using Microsoft.VisualStudio.TestTools.UnitTesting;
using TaleWorlds.CampaignSystem;
using TAOM.Features.AiPartySize;

namespace TAOM.Tests.Features.AiPartySize;

/// <summary>
/// Issue #461. AI lord parties spawn from a party template that never consults PartySizeLimit, then
/// get trimmed back to the vanilla 50-150 cap within a day. These cover the pure arithmetic behind
/// raising that cap, plus the two relief knobs that close the morale-desertion path the cap alone
/// does not touch. The engine-touching wrappers take sealed PartyBase/MobileParty and are verified
/// in game.
/// </summary>
[TestClass]
public class AiPartySizeScalingTests
{
    private const float Tolerance = 0.01f;

    [TestMethod]
    public void ApplyPartySizeScaling_FactorOnly_MultipliesTheLimit()
    {
        // A tier-4 clan-leader lord sits near 126 after his culture feat. At 10x he can hold the
        // ~1,270 men his template actually spawns instead of shedding to 126 on the first daily tick.
        var limit = new ExplainedNumber(126f);

        AiPartySizeService.ApplyPartySizeScaling(ref limit, factor: 10f, flatBonus: 0f);

        Assert.AreEqual(1260f, limit.ResultNumber, Tolerance);
    }

    [TestMethod]
    public void ApplyPartySizeScaling_FlatBonusIsNotAmplifiedByTheFactor()
    {
        // The discriminating case. ExplainedNumber.Add lands in the BASE frame and the result is
        // BaseNumber * (1 + SumOfFactors), so a raw Add(300) alongside a 10x factor would be worth
        // 3,000 men, not 300. Expected: 126*10 + 300.
        var limit = new ExplainedNumber(126f);

        AiPartySizeService.ApplyPartySizeScaling(ref limit, factor: 10f, flatBonus: 300f);

        Assert.AreEqual(1560f, limit.ResultNumber, Tolerance);
        Assert.AreNotEqual(4260f, limit.ResultNumber, Tolerance, "the flat bonus must not be scaled by the factor");
    }

    [TestMethod]
    public void ApplyPartySizeScaling_FlatBonusOnly_AddsExactly()
    {
        var limit = new ExplainedNumber(126f);

        AiPartySizeService.ApplyPartySizeScaling(ref limit, factor: 1f, flatBonus: 300f);

        Assert.AreEqual(426f, limit.ResultNumber, Tolerance);
    }

    [TestMethod]
    public void ApplyPartySizeScaling_FlatBonusOnTopOfACultureFeat_StillCostsExactlyItsOwnValue()
    {
        // Mordor's +20% party-size feat is already in the factor frame when this runs.
        var limit = new ExplainedNumber(105f);
        limit.AddFactor(0.20f);
        Assert.AreEqual(126f, limit.ResultNumber, Tolerance, "precondition: the feat frame is as expected");

        AiPartySizeService.ApplyPartySizeScaling(ref limit, factor: 1f, flatBonus: 300f);

        Assert.AreEqual(426f, limit.ResultNumber, Tolerance);
    }

    [TestMethod]
    public void ApplyPartySizeScaling_NeutralKnobs_LeaveVanillaUntouched()
    {
        var limit = new ExplainedNumber(126f);

        AiPartySizeService.ApplyPartySizeScaling(ref limit, factor: 1f, flatBonus: 0f);

        Assert.AreEqual(126f, limit.ResultNumber, Tolerance);
    }

    // Engine-float gate rule: a non-finite or out-of-range knob must FAIL the gate and leave vanilla
    // behaviour, never be coerced into a plausible-looking value. NaN fails every comparison, so the
    // gates are written as positive requirements.
    [TestMethod]
    public void ApplyPartySizeScaling_NaNFactor_IsIgnored()
    {
        var limit = new ExplainedNumber(126f);

        AiPartySizeService.ApplyPartySizeScaling(ref limit, factor: float.NaN, flatBonus: 0f);

        Assert.AreEqual(126f, limit.ResultNumber, Tolerance);
        Assert.IsFalse(float.IsNaN(limit.ResultNumber));
    }

    [TestMethod]
    public void ApplyPartySizeScaling_InfiniteFactor_IsIgnored()
    {
        var limit = new ExplainedNumber(126f);

        AiPartySizeService.ApplyPartySizeScaling(ref limit, factor: float.PositiveInfinity, flatBonus: 0f);

        Assert.AreEqual(126f, limit.ResultNumber, Tolerance);
    }

    [TestMethod]
    public void ApplyPartySizeScaling_FactorBelowOne_IsIgnoredRatherThanShrinkingTheLimit()
    {
        var limit = new ExplainedNumber(126f);

        AiPartySizeService.ApplyPartySizeScaling(ref limit, factor: 0.5f, flatBonus: 0f);

        Assert.AreEqual(126f, limit.ResultNumber, Tolerance);
    }

    [TestMethod]
    public void ApplyPartySizeScaling_FactorAboveCeiling_IsIgnored()
    {
        var limit = new ExplainedNumber(126f);

        AiPartySizeService.ApplyPartySizeScaling(ref limit, factor: AiPartySizeService.MaxFactor + 1f, flatBonus: 0f);

        Assert.AreEqual(126f, limit.ResultNumber, Tolerance);
    }

    [TestMethod]
    public void ApplyPartySizeScaling_NaNFlatBonus_IsIgnored()
    {
        var limit = new ExplainedNumber(126f);

        AiPartySizeService.ApplyPartySizeScaling(ref limit, factor: 1f, flatBonus: float.NaN);

        Assert.AreEqual(126f, limit.ResultNumber, Tolerance);
        Assert.IsFalse(float.IsNaN(limit.BaseNumber), "a NaN knob must not poison BaseNumber");
    }

    [TestMethod]
    public void ApplyPartySizeScaling_NegativeFlatBonus_IsIgnored()
    {
        var limit = new ExplainedNumber(126f);

        AiPartySizeService.ApplyPartySizeScaling(ref limit, factor: 1f, flatBonus: -500f);

        Assert.AreEqual(126f, limit.ResultNumber, Tolerance);
    }

    [TestMethod]
    public void AddResultFrameBonus_CancelledFactorFrame_SkipsRatherThanDividingByNearZero()
    {
        // SumOfFactors of -1 cancels the value to nothing; dividing the bonus by that scale is
        // meaningless and would explode.
        var limit = new ExplainedNumber(126f);
        limit.AddFactor(-1f);

        AiPartySizeService.AddResultFrameBonus(ref limit, 300f);

        Assert.AreEqual(0f, limit.ResultNumber, Tolerance);
    }
}

[TestClass]
public class AiPartySizeReliefTests
{
    private const float Tolerance = 0.01f;

    [TestMethod]
    public void ApplyRelief_Wage_ReducesTheBillByTheGivenFraction()
    {
        // A 1,500-man party costs far more per day than an AI clan's 200-600 wage bracket, which
        // pins HasUnpaidWages to 1.0 and costs a flat -20 morale.
        var wage = new ExplainedNumber(10000f);

        AiPartySizeService.ApplyRelief(ref wage, relief: 0.9f);

        Assert.AreEqual(1000f, wage.ResultNumber, Tolerance);
    }

    [TestMethod]
    public void ApplyRelief_FoodConsumption_MovesTowardZeroAndKeepsItsSign()
    {
        // Consumption is authored NEGATIVE by the vanilla model. A factor scales magnitude and
        // preserves sign, so the same helper serves both surfaces.
        var consumption = new ExplainedNumber(-100f);

        AiPartySizeService.ApplyRelief(ref consumption, relief: 0.9f);

        Assert.AreEqual(-10f, consumption.ResultNumber, Tolerance);
        Assert.IsTrue(consumption.ResultNumber < 0f, "relief must not flip consumption into production");
    }

    [TestMethod]
    public void ApplyRelief_OnTopOfACultureWageFeat_StacksAdditivelyInTheFactorFrame()
    {
        // Mordor pays +20%. Factors SUM rather than compose: 1 + 0.20 - 0.90 = 0.30.
        var wage = new ExplainedNumber(10000f);
        wage.AddFactor(0.20f);

        AiPartySizeService.ApplyRelief(ref wage, relief: 0.9f);

        Assert.AreEqual(3000f, wage.ResultNumber, Tolerance);
    }

    [TestMethod]
    public void ApplyRelief_WouldCancelTheFactorFrame_IsSkippedRatherThanFlippingTheSign()
    {
        // An existing -0.5 factor plus a 0.9 relief projects to -0.4, which would turn a wage BILL
        // into a wage rebate. The gate is on the PROJECTED frame for exactly this reason.
        var wage = new ExplainedNumber(10000f);
        wage.AddFactor(-0.5f);

        AiPartySizeService.ApplyRelief(ref wage, relief: 0.9f);

        Assert.AreEqual(5000f, wage.ResultNumber, Tolerance);
        Assert.IsTrue(wage.ResultNumber > 0f, "relief must never invert the sign of a wage bill");
    }

    [TestMethod]
    public void ApplyRelief_ZeroRelief_LeavesVanillaUntouched()
    {
        var wage = new ExplainedNumber(10000f);

        AiPartySizeService.ApplyRelief(ref wage, relief: 0f);

        Assert.AreEqual(10000f, wage.ResultNumber, Tolerance);
    }

    [TestMethod]
    public void ApplyRelief_NaN_IsIgnored()
    {
        var wage = new ExplainedNumber(10000f);

        AiPartySizeService.ApplyRelief(ref wage, relief: float.NaN);

        Assert.AreEqual(10000f, wage.ResultNumber, Tolerance);
        Assert.IsFalse(float.IsNaN(wage.ResultNumber));
    }

    [TestMethod]
    public void ApplyRelief_FullRelief_IsRejectedSoTheBillNeverReachesZero()
    {
        var wage = new ExplainedNumber(10000f);

        AiPartySizeService.ApplyRelief(ref wage, relief: 1f);

        Assert.AreEqual(10000f, wage.ResultNumber, Tolerance);
    }
}

[TestClass]
public class AiPartySizeGatingTests
{
    [TestMethod]
    public void IsScalableAiLordParty_AiLordWithLeader_IsScaled()
        => Assert.IsTrue(AiPartySizeService.IsScalableAiLordParty(
            isMainParty: false, isLordParty: true, hasLeaderHero: true, isPlayerClan: false));

    [TestMethod]
    public void IsScalableAiLordParty_MainParty_IsNotScaled()
        => Assert.IsFalse(AiPartySizeService.IsScalableAiLordParty(
            isMainParty: true, isLordParty: true, hasLeaderHero: true, isPlayerClan: true));

    // The exclusion this class originally missed. A party the player raises for a companion is a
    // LordPartyComponent, so it is IsLordParty and is NOT IsMainParty: testing only the main party
    // let the player's own clan parties collect the full AI treatment, food and wage relief
    // included. Deep review 2026-08-18.
    [TestMethod]
    public void IsScalableAiLordParty_PlayerClanPartyLedByACompanion_IsNotScaled()
        => Assert.IsFalse(AiPartySizeService.IsScalableAiLordParty(
            isMainParty: false, isLordParty: true, hasLeaderHero: true, isPlayerClan: true));

    // Caravans, villagers, militia and patrols are sized from entirely different vanilla branches;
    // scaling them would be a silent, unrelated balance change.
    [TestMethod]
    public void IsScalableAiLordParty_NonLordParty_IsNotScaled()
        => Assert.IsFalse(AiPartySizeService.IsScalableAiLordParty(
            isMainParty: false, isLordParty: false, hasLeaderHero: true, isPlayerClan: false));

    [TestMethod]
    public void IsScalableAiLordParty_LeaderlessParty_IsNotScaled()
        => Assert.IsFalse(AiPartySizeService.IsScalableAiLordParty(
            isMainParty: false, isLordParty: true, hasLeaderHero: false, isPlayerClan: false));
}
