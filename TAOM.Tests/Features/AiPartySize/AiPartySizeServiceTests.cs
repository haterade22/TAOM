using Microsoft.VisualStudio.TestTools.UnitTesting;
using TaleWorlds.CampaignSystem;
using TAOM.Features;
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

/// <summary>
/// The player-clan branch, added 2026-09-01. A player who takes over an existing lord inherits a clan
/// whose rosters were filled at world generation against the AI-scaled limit, so without this the
/// limit collapses under them (#530). This is a SECOND predicate rather than a relaxation of
/// IsScalableAiLordParty, which stays exactly as the 2026-08-18 deep review left it, because only the
/// party size travels to the player: the food and wage relief must not.
/// </summary>
[TestClass]
public class AiPartySizePlayerClanGatingTests
{
    private const bool TakenOver = true;
    private const bool VanillaStart = false;

    private static bool Scaled(PlayerClanScalingMode mode, bool takenOver)
        => AiPartySizeService.IsScalablePlayerLordParty(
            isPlayerClan: true, isLordParty: true, hasLeaderHero: true,
            isTakenOverClan: takenOver, mode: mode);

    [TestMethod]
    public void Never_TakenOverClan_IsNotScaled()
        => Assert.IsFalse(Scaled(PlayerClanScalingMode.Never, TakenOver));

    [TestMethod]
    public void Never_VanillaStart_IsNotScaled()
        => Assert.IsFalse(Scaled(PlayerClanScalingMode.Never, VanillaStart));

    [TestMethod]
    public void TakenOverOnly_TakenOverClan_IsScaled()
        => Assert.IsTrue(Scaled(PlayerClanScalingMode.TakenOverOnly, TakenOver));

    // The shipped default must leave an ordinary campaign exactly as it was before this feature.
    [TestMethod]
    public void TakenOverOnly_VanillaStart_IsNotScaled()
        => Assert.IsFalse(Scaled(PlayerClanScalingMode.TakenOverOnly, VanillaStart));

    [TestMethod]
    public void Always_TakenOverClan_IsScaled()
        => Assert.IsTrue(Scaled(PlayerClanScalingMode.Always, TakenOver));

    [TestMethod]
    public void Always_VanillaStart_IsScaled()
        => Assert.IsTrue(Scaled(PlayerClanScalingMode.Always, VanillaStart));

    // An AI party must never reach the player branch, or it would collect the scaling twice.
    [TestMethod]
    public void AiParty_UnderAlways_IsNotScaledByThePlayerBranch()
        => Assert.IsFalse(AiPartySizeService.IsScalablePlayerLordParty(
            isPlayerClan: false, isLordParty: true, hasLeaderHero: true,
            isTakenOverClan: true, mode: PlayerClanScalingMode.Always));

    [TestMethod]
    public void NonLordParty_UnderAlways_IsNotScaled()
        => Assert.IsFalse(AiPartySizeService.IsScalablePlayerLordParty(
            isPlayerClan: true, isLordParty: false, hasLeaderHero: true,
            isTakenOverClan: true, mode: PlayerClanScalingMode.Always));

    [TestMethod]
    public void LeaderlessParty_UnderAlways_IsNotScaled()
        => Assert.IsFalse(AiPartySizeService.IsScalablePlayerLordParty(
            isPlayerClan: true, isLordParty: true, hasLeaderHero: false,
            isTakenOverClan: true, mode: PlayerClanScalingMode.Always));

    /// <summary>
    /// The decision this whole split exists to enforce. Party size travels to the player clan; the
    /// food and wage relief do not, because a 90% rebate is too large a gift to hand a player clan.
    /// It is NOT because the player is immune to the pressures: a player-clan companion party is not
    /// IsMainParty, so it runs vanilla's auto food-buy and starves like an AI party, and its full
    /// wage bill is drawn from clan gold. See #532. If someone later "simplifies" the two predicates
    /// into one, this test is what fails.
    /// </summary>
    [TestMethod]
    public void PlayerClanParty_GetsSizeScalingButNeverTheReliefGate()
    {
        Assert.IsTrue(Scaled(PlayerClanScalingMode.Always, TakenOver),
            "party size scaling should reach a player-clan lord party");
        Assert.IsFalse(AiPartySizeService.IsScalableAiLordParty(
            isMainParty: false, isLordParty: true, hasLeaderHero: true, isPlayerClan: true),
            "the relief gate must still reject it: food and wage relief stay AI-only");
    }
}

/// <summary>
/// Player Switcher leaves NO persisted marker that it ran, by design (both its behaviors have empty
/// SyncData). But `Campaign.PlayerDefaultFaction` IS [SaveableProperty(17)] and vanilla assigns it
/// exactly once, to the clan literally named "player_faction", so the clan id is a durable proxy for
/// "this player took over an existing lord". That is a coupling to a vanilla data id, which is why it
/// is pinned here rather than left as a bare string literal in the service.
/// </summary>
[TestClass]
public class AiPartySizeTakeoverDetectionTests
{
    [TestMethod]
    public void VanillaPlayerClanId_IsTheEngineId()
        => Assert.AreEqual("player_faction", AiPartySizeService.VanillaPlayerClanId);

    [TestMethod]
    public void IsTakenOverPlayerClan_VanillaClan_IsFalse()
        => Assert.IsFalse(AiPartySizeService.IsTakenOverPlayerClan("player_faction"));

    [TestMethod]
    public void IsTakenOverPlayerClan_AnExistingLordsClan_IsTrue()
        => Assert.IsTrue(AiPartySizeService.IsTakenOverPlayerClan("clan_empire_west_1"));

    // Outside a campaign, or before the clan resolves, treat it as a vanilla start: the safe answer
    // is the one that changes nothing.
    [TestMethod]
    public void IsTakenOverPlayerClan_MissingId_IsFalse()
    {
        Assert.IsFalse(AiPartySizeService.IsTakenOverPlayerClan(null));
        Assert.IsFalse(AiPartySizeService.IsTakenOverPlayerClan(""));
    }
}

/// <summary>
/// Dropdown resolution. Mirrors CaravanTradeSettingsProvider.ResolveWarPolicy: switch on
/// SelectedIndex, fall through to the compiled default, so a persisted json that has drifted outside
/// the known set cannot silently select a different branch.
/// </summary>
[TestClass]
public class AiPartySizeScalingModeTests
{
    [TestMethod]
    public void ResolvePlayerClanScaling_KnownIndices_MapInOrder()
    {
        Assert.AreEqual(PlayerClanScalingMode.Never, AiPartySizeService.ResolvePlayerClanScaling(0));
        Assert.AreEqual(PlayerClanScalingMode.TakenOverOnly, AiPartySizeService.ResolvePlayerClanScaling(1));
        Assert.AreEqual(PlayerClanScalingMode.Always, AiPartySizeService.ResolvePlayerClanScaling(2));
    }

    [TestMethod]
    public void ResolvePlayerClanScaling_UnknownOrMissing_FallsBackToTheCompiledDefault()
    {
        Assert.AreEqual(AiPartySizeService.DefaultPlayerClanScaling, AiPartySizeService.ResolvePlayerClanScaling(null));
        Assert.AreEqual(AiPartySizeService.DefaultPlayerClanScaling, AiPartySizeService.ResolvePlayerClanScaling(99));
        Assert.AreEqual(AiPartySizeService.DefaultPlayerClanScaling, AiPartySizeService.ResolvePlayerClanScaling(-1));
    }

    [TestMethod]
    public void DefaultPlayerClanScaling_IsTakenOverOnly()
        => Assert.AreEqual(PlayerClanScalingMode.TakenOverOnly, AiPartySizeService.DefaultPlayerClanScaling);

    // The MCM dropdown and the compiled default are two statements of the same choice; this is the
    // same drift guard as AiPartySizeShippedDefaultsTests.
    [TestMethod]
    public void McmDropdownDefault_ResolvesToTheCompiledDefault()
    {
        var dropdown = new TaomSettings().AiPlayerClanPartyScaling;

        Assert.AreEqual(3, dropdown.Count, "three modes: Never, Taken-over lords only, Always");
        Assert.AreEqual(AiPartySizeService.DefaultPlayerClanScaling,
            AiPartySizeService.ResolvePlayerClanScaling(dropdown.SelectedIndex));
    }
}

/// <summary>
/// Guards the shipped values of the two lord knobs. Every other test in this file passes the
/// numbers in as literal arguments, so none of them notices what the mod actually ships, and until
/// the constants landed each default was written twice (MCM initializer plus the service's `??`
/// fallback) with nothing watching the two for drift. Same guard, same reasoning, as
/// EnlistmentFeatureToggleTests.ResolveEnabled_DefaultMatchesTheCompiledSettingDefault.
/// </summary>
[TestClass]
public class AiPartySizeShippedDefaultsTests
{
    private const float Tolerance = 0.01f;

    [TestMethod]
    public void McmFactorDefault_IsTheServiceFallback()
        => Assert.AreEqual(AiPartySizeService.DefaultLordFactor,
            new TaomSettings().AiLordPartySizeFactor, Tolerance);

    [TestMethod]
    public void McmFlatBonusDefault_IsTheServiceFallback()
        => Assert.AreEqual(AiPartySizeService.DefaultLordFlatBonus,
            new TaomSettings().AiLordPartySizeFlatBonus, Tolerance);

    // Both knobs sit inside their own MCM slider range, so a default can never present as a value
    // the player is not allowed to dial back to.
    [TestMethod]
    public void ShippedDefaults_SitInsideTheirSliderRanges()
    {
        Assert.IsTrue(AiPartySizeService.DefaultLordFactor is >= 1.0f and <= 20.0f,
            "AI Lord Party Size Multiplier slider is [1.0, 20.0].");
        Assert.IsTrue(AiPartySizeService.DefaultLordFlatBonus is >= 0.0f and <= 2000.0f,
            "AI Lord Party Size Flat Bonus slider is [0, 2000].");
        Assert.IsTrue(AiPartySizeService.DefaultGarrisonFactor is >= 1.0f and <= 10.0f,
            "Garrison Size Multiplier slider is [1.0, 10.0].");
    }

    // Same drift guard as the lord knobs. The garrison default used to live twice, as the MCM
    // initializer and as a `?? 3f` fallback, with nothing watching the two.
    [TestMethod]
    public void McmGarrisonDefault_IsTheServiceFallback()
        => Assert.AreEqual(AiPartySizeService.DefaultGarrisonFactor,
            new TaomSettings().AiGarrisonSizeFactor, Tolerance);

    // Ships neutral: at 1.0 and 0 the feature contributes nothing to a lord party's limit, so the
    // out-of-box game is vanilla and a player opts in through MCM. If either of these moves off its
    // neutral value the change is deliberate and this test should be updated with it.
    [TestMethod]
    public void ShippedDefaults_AreNeutral_SoTheFeatureIsOptIn()
    {
        Assert.AreEqual(1.0f, AiPartySizeService.DefaultLordFactor, Tolerance);
        Assert.AreEqual(0.0f, AiPartySizeService.DefaultLordFlatBonus, Tolerance);
        Assert.AreEqual(1.0f, AiPartySizeService.DefaultGarrisonFactor, Tolerance);
        Assert.AreEqual(0.0f, AiPartySizeService.DefaultFoodRelief, Tolerance);
        Assert.AreEqual(0.0f, AiPartySizeService.DefaultWageRelief, Tolerance);
    }

    [TestMethod]
    public void McmReliefDefaults_AreTheServiceFallbacks()
    {
        var settings = new TaomSettings();

        Assert.AreEqual(AiPartySizeService.DefaultFoodRelief, settings.AiFoodConsumptionRelief, Tolerance);
        Assert.AreEqual(AiPartySizeService.DefaultWageRelief, settings.AiWageRelief, Tolerance);
    }

    // The whole point of shipping neutral: every knob at its default must leave the vanilla number
    // exactly as the engine computed it. If any default drifts off neutral this fails.
    [TestMethod]
    public void ShippedDefaults_LeaveAVanillaLimitUntouched()
    {
        var limit = new ExplainedNumber(126f);

        AiPartySizeService.ApplyPartySizeScaling(
            ref limit, AiPartySizeService.DefaultLordFactor, AiPartySizeService.DefaultLordFlatBonus);
        AiPartySizeService.ApplyPartySizeScaling(ref limit, AiPartySizeService.DefaultGarrisonFactor, 0f);

        Assert.AreEqual(126f, limit.ResultNumber, Tolerance);
    }

    [TestMethod]
    public void ShippedReliefDefaults_LeaveAVanillaWageAndFoodBillUntouched()
    {
        var wage = new ExplainedNumber(10000f);
        var food = new ExplainedNumber(-100f);

        AiPartySizeService.ApplyRelief(ref wage, AiPartySizeService.DefaultWageRelief);
        AiPartySizeService.ApplyRelief(ref food, AiPartySizeService.DefaultFoodRelief);

        Assert.AreEqual(10000f, wage.ResultNumber, Tolerance);
        Assert.AreEqual(-100f, food.ResultNumber, Tolerance);
    }

    // Pins the shipped pair end to end rather than the two numbers separately, because the flat
    // bonus is deliberately kept out of the factor frame and halving one knob alone lands at 60%,
    // not 50%. 126 is the tier-4 lord base docs/features/ai-party-size.md works from: that row read
    // 1560 until the 2026-09-01 halving.
    [TestMethod]
    public void ShippedDefaults_ProduceTheDocumentedTierFourLimit()
    {
        var limit = new ExplainedNumber(126f);

        AiPartySizeService.ApplyPartySizeScaling(
            ref limit, AiPartySizeService.DefaultLordFactor, AiPartySizeService.DefaultLordFlatBonus);

        Assert.AreEqual(126f, limit.ResultNumber, Tolerance);
    }
}
