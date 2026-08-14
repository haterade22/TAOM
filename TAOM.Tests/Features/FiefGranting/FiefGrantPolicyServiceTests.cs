using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Features.FiefGranting;

namespace TAOM.Tests.Features.FiefGranting;

/// <summary>
/// #458 — fief grants concentrated in one clan per kingdom. These pin the two levers the feature
/// owns: the merit multiplier layered on vanilla's <c>CalculateMeritOfOutcome</c>, and the King's
/// Vote gate that stops a rich ruling clan overriding the council on every single grant.
/// </summary>
[TestClass]
public class FiefGrantPolicyServiceTests
{
    private IFiefGrantSettingsProvider _settings = null!;
    private FiefGrantPolicyService _sut = null!;

    [TestInitialize]
    public void Setup()
    {
        _settings = Substitute.For<IFiefGrantSettingsProvider>();

        // Neutral baseline: every knob a no-op, so each test moves exactly one lever.
        _settings.IsEnabled.Returns(true);
        _settings.CapturerBonus.Returns(1f);
        _settings.LandlessBonus.Returns(1f);
        _settings.ConcentrationPenalty.Returns(0f);
        _settings.CultureMatchBonus.Returns(1f);
        _settings.CultureMismatchPenalty.Returns(1f);
        _settings.RulingClanFactor.Returns(1f);
        _settings.KingsVoteFiefShareCap.Returns(1f);
        _settings.ApplyPenaltiesToPlayerClan.Returns(true);

        _sut = new FiefGrantPolicyService(_settings);
    }

    private static FiefGrantCandidateFacts Clan(
        int owned = 1,
        bool ruling = false,
        bool capturer = false,
        bool cultureMatch = true,
        bool player = false) =>
        new FiefGrantCandidateFacts(owned, ruling, capturer, cultureMatch, player);

    // ---------------------------------------------------------------- disabled / vanilla parity

    [TestMethod]
    public void GetMeritMultiplier_WhenDisabled_IsExactlyVanilla()
    {
        _settings.IsEnabled.Returns(false);
        _settings.CapturerBonus.Returns(5f);
        _settings.ConcentrationPenalty.Returns(0.9f);

        Assert.AreEqual(1f, _sut.GetMeritMultiplier(Clan(owned: 9, capturer: true)));
    }

    [TestMethod]
    public void IsEnabled_MirrorsSettings()
    {
        _settings.IsEnabled.Returns(false);
        Assert.IsFalse(_sut.IsEnabled);
    }

    [TestMethod]
    public void GetMeritMultiplier_WithNeutralSettings_IsExactlyVanilla()
    {
        Assert.AreEqual(1f, _sut.GetMeritMultiplier(Clan(owned: 4, ruling: true, capturer: true)));
    }

    // ---------------------------------------------------------------- spread fiefs across clans

    [TestMethod]
    public void GetMeritMultiplier_DampsEachAdditionalFortification()
    {
        _settings.ConcentrationPenalty.Returns(0.5f);

        // 1 / (1 + owned * 0.5)
        Assert.AreEqual(1f, _sut.GetMeritMultiplier(Clan(owned: 0)), 0.0001f);
        Assert.AreEqual(1f / 1.5f, _sut.GetMeritMultiplier(Clan(owned: 1)), 0.0001f);
        Assert.AreEqual(1f / 4.5f, _sut.GetMeritMultiplier(Clan(owned: 7)), 0.0001f);
    }

    /// Multiplier comparison only. The multiplier is applied ON TOP of vanilla's merit, so a larger
    /// multiplier is not by itself a higher final score: a seven-fief clan can still out-score a
    /// one-fief clan if vanilla's own numerator (tier, strength, proximity) is far enough ahead.
    /// Naming this "outranks" would overclaim, which is exactly what an adversarial pass caught.
    [TestMethod]
    public void GetMeritMultiplier_DampsAnIncumbentMoreThanAChallenger()
    {
        _settings.ConcentrationPenalty.Returns(0.35f);

        var incumbent = _sut.GetMeritMultiplier(Clan(owned: 7));
        var challenger = _sut.GetMeritMultiplier(Clan(owned: 1));

        Assert.IsTrue(challenger > incumbent,
            $"a one-fief clan's multiplier ({challenger}) must exceed a seven-fief clan's ({incumbent})");
    }

    // ---------------------------------------------------------------- keep weak clans alive

    [TestMethod]
    public void GetMeritMultiplier_RewardsALandlessClan()
    {
        _settings.LandlessBonus.Returns(2f);

        Assert.AreEqual(2f, _sut.GetMeritMultiplier(Clan(owned: 0)), 0.0001f);
        Assert.AreEqual(1f, _sut.GetMeritMultiplier(Clan(owned: 1)), 0.0001f);
    }

    [TestMethod]
    public void GetMeritMultiplier_TreatsNegativeFiefCountAsLandless()
    {
        _settings.LandlessBonus.Returns(2f);
        _settings.ConcentrationPenalty.Returns(0.5f);

        // Defensive: a bad adapter read must not produce a multiplier above the landless case.
        Assert.AreEqual(2f, _sut.GetMeritMultiplier(Clan(owned: -3)), 0.0001f);
    }

    // ---------------------------------------------------------------- strong capturer claim

    [TestMethod]
    public void GetMeritMultiplier_RewardsTheClanThatTookIt()
    {
        _settings.CapturerBonus.Returns(2.5f);

        Assert.AreEqual(2.5f, _sut.GetMeritMultiplier(Clan(capturer: true)), 0.0001f);
        Assert.AreEqual(1f, _sut.GetMeritMultiplier(Clan(capturer: false)), 0.0001f);
    }

    /// Multiplier comparison only, same caveat as above: this pins the relative weighting the knobs
    /// produce, not the final election result.
    [TestMethod]
    public void GetMeritMultiplier_WeightsTheCapturerAboveTheRulingClanAtEqualHoldings()
    {
        _settings.CapturerBonus.Returns(2.5f);
        _settings.RulingClanFactor.Returns(0.75f);

        var capturer = _sut.GetMeritMultiplier(Clan(owned: 2, capturer: true));
        var ruler = _sut.GetMeritMultiplier(Clan(owned: 2, ruling: true));

        Assert.IsTrue(capturer > ruler,
            $"the capturer's multiplier ({capturer}) must exceed the king's ({ruler}) at equal holdings");
    }

    // ---------------------------------------------------------------- lore-correct ownership

    [TestMethod]
    public void GetMeritMultiplier_FavoursACultureMatch()
    {
        _settings.CultureMatchBonus.Returns(1.5f);
        _settings.CultureMismatchPenalty.Returns(0.6f);

        Assert.AreEqual(1.5f, _sut.GetMeritMultiplier(Clan(cultureMatch: true)), 0.0001f);
        Assert.AreEqual(0.6f, _sut.GetMeritMultiplier(Clan(cultureMatch: false)), 0.0001f);
    }

    [TestMethod]
    public void GetMeritMultiplier_CombinesTermsMultiplicatively()
    {
        _settings.ConcentrationPenalty.Returns(0.5f);
        _settings.CapturerBonus.Returns(2f);
        _settings.CultureMatchBonus.Returns(1.5f);

        // 1/(1+2*0.5) * 2 * 1.5
        Assert.AreEqual(1f / 2f * 2f * 1.5f,
            _sut.GetMeritMultiplier(Clan(owned: 2, capturer: true, cultureMatch: true)), 0.0001f);
    }

    // ---------------------------------------------------------------- player exemption

    [TestMethod]
    public void GetMeritMultiplier_WhenPlayerExempt_KeepsBonusesAndDropsPenalties()
    {
        _settings.ApplyPenaltiesToPlayerClan.Returns(false);
        _settings.ConcentrationPenalty.Returns(0.5f);
        _settings.RulingClanFactor.Returns(0.5f);
        _settings.CultureMismatchPenalty.Returns(0.5f);
        _settings.CapturerBonus.Returns(2f);

        // Only the capturer bonus survives.
        var player = _sut.GetMeritMultiplier(
            Clan(owned: 4, ruling: true, capturer: true, cultureMatch: false, player: true));

        Assert.AreEqual(2f, player, 0.0001f);
    }

    /// The ruling-clan factor spans 0.1 to 2.0, so above 1.0 it is a BONUS. An exempt player must
    /// still receive it: the exemption drops penalties, and skipping the whole term denied a player
    /// ruler the bonus they had just turned the slider up to get.
    [TestMethod]
    public void GetMeritMultiplier_WhenPlayerExempt_StillGetsARulingClanBonusAboveOne()
    {
        _settings.ApplyPenaltiesToPlayerClan.Returns(false);
        _settings.RulingClanFactor.Returns(2f);

        Assert.AreEqual(2f, _sut.GetMeritMultiplier(Clan(owned: 3, ruling: true, player: true)), 0.0001f);
    }

    [TestMethod]
    public void GetMeritMultiplier_WhenPlayerExempt_StillSkipsARulingClanPenaltyBelowOne()
    {
        _settings.ApplyPenaltiesToPlayerClan.Returns(false);
        _settings.RulingClanFactor.Returns(0.5f);

        Assert.AreEqual(1f, _sut.GetMeritMultiplier(Clan(owned: 3, ruling: true, player: true)), 0.0001f);
    }

    [TestMethod]
    public void GetMeritMultiplier_WhenPlayerNotExempt_IsScoredLikeAnyClan()
    {
        _settings.ApplyPenaltiesToPlayerClan.Returns(true);
        _settings.ConcentrationPenalty.Returns(0.5f);

        Assert.AreEqual(_sut.GetMeritMultiplier(Clan(owned: 4, player: false)),
            _sut.GetMeritMultiplier(Clan(owned: 4, player: true)), 0.0001f);
    }

    // ---------------------------------------------------------------- non-finite settings

    [TestMethod]
    public void GetMeritMultiplier_WithNonFiniteSetting_FallsBackToVanilla()
    {
        _settings.CapturerBonus.Returns(float.NaN);
        Assert.AreEqual(1f, _sut.GetMeritMultiplier(Clan(capturer: true)));

        _settings.CapturerBonus.Returns(float.PositiveInfinity);
        Assert.AreEqual(1f, _sut.GetMeritMultiplier(Clan(capturer: true)));
    }

    [TestMethod]
    public void GetMeritMultiplier_NeverReturnsZeroOrNegative()
    {
        _settings.CultureMismatchPenalty.Returns(-4f);
        _settings.CapturerBonus.Returns(0f);

        var m = _sut.GetMeritMultiplier(Clan(cultureMatch: false, capturer: true));

        Assert.IsTrue(m > 0f, $"a merit multiplier of {m} would invert or erase the ranking");
    }

    // ---------------------------------------------------------------- King's Vote gate

    [TestMethod]
    public void IsKingsVoteAllowed_WhenDisabled_IsAlwaysVanilla()
    {
        _settings.IsEnabled.Returns(false);
        _settings.KingsVoteFiefShareCap.Returns(0.1f);

        Assert.IsTrue(_sut.IsKingsVoteAllowed(9, 10));
    }

    [TestMethod]
    public void IsKingsVoteAllowed_BlocksARulerWhoAlreadyHoldsTooMuch()
    {
        _settings.KingsVoteFiefShareCap.Returns(0.34f);

        Assert.IsFalse(_sut.IsKingsVoteAllowed(7, 7), "a ruler holding every fief keeps overriding");
        Assert.IsFalse(_sut.IsKingsVoteAllowed(5, 10));
        Assert.IsTrue(_sut.IsKingsVoteAllowed(3, 10));
    }

    [TestMethod]
    public void IsKingsVoteAllowed_AtExactlyTheCap_IsStillAllowed()
    {
        _settings.KingsVoteFiefShareCap.Returns(0.5f);
        Assert.IsTrue(_sut.IsKingsVoteAllowed(5, 10));
    }

    [TestMethod]
    public void IsKingsVoteAllowed_WithNoKingdomFortifications_IsVanilla()
    {
        _settings.KingsVoteFiefShareCap.Returns(0f);

        Assert.IsTrue(_sut.IsKingsVoteAllowed(0, 0));
        Assert.IsTrue(_sut.IsKingsVoteAllowed(1, 0));
        Assert.IsTrue(_sut.IsKingsVoteAllowed(0, 5), "a landless king is not the hoarding case");
    }

    [TestMethod]
    public void IsKingsVoteAllowed_WithNonFiniteCap_DefersToVanilla()
    {
        _settings.KingsVoteFiefShareCap.Returns(float.NaN);
        Assert.IsTrue(_sut.IsKingsVoteAllowed(9, 10));
    }
}
