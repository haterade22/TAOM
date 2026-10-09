using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TaleWorlds.CampaignSystem;
using TAOM.Features.CulturalFeats;
using TAOM.Features.TroopProgression;
using TAOM.Features.WarChronicle.Effects;

namespace TAOM.Tests.Features.TroopProgression;

/// <summary>
/// The daily volunteer probability: the culture feats as TaomVolunteerModel applied them before the
/// extraction (an ExplainedNumber, ApplyVolunteerRespawnFeats, clamp to 0..1; the same result for every
/// finite value, while a non-finite base or feats result now returns vanilla's value), then the War
/// Chronicle's timed multiplier for the settlement owner's kingdom, skipped for the player's own clan.
/// Constructs ExplainedNumber, so the class needs the game.
/// </summary>
[TestClass]
[TestCategory("RequiresGame")]
public class VolunteerProductionServiceTests
{
    private ICulturalFeatsService _feats = null!;
    private IWarEffectService _war = null!;
    private ICultureFeatAdapter _culture = null!;
    private VolunteerProductionService _sut = null!;

    [TestInitialize]
    public void Setup()
    {
        _feats = Substitute.For<ICulturalFeatsService>();
        _war = Substitute.For<IWarEffectService>();
        _war.GetMultiplier(Arg.Any<string>(), Arg.Any<WarEffectKind>()).Returns(1f);
        _culture = Substitute.For<ICultureFeatAdapter>();
        _sut = new VolunteerProductionService(_feats, _war);
    }

    private void FeatsAddFactor(float factor)
    {
        _feats.When(f => f.ApplyVolunteerRespawnFeats(Arg.Any<ICultureFeatAdapter?>(), ref Arg.Any<ExplainedNumber>()))
            .Do(call =>
            {
                var number = (ExplainedNumber)call[1];
                number.AddFactor(factor);
                call[1] = number;
            });
    }

    [TestMethod]
    public void Compute_CultureNull_ReturnsTheBaseUntouchedAndAsksNoFeats()
    {
        var result = _sut.Compute(0.37f, null, null, false);

        Assert.AreEqual(0.37f, result);
        _feats.DidNotReceiveWithAnyArgs().ApplyVolunteerRespawnFeats(null, ref Arg.Any<ExplainedNumber>());
    }

    [TestMethod]
    public void Compute_CultureNullAndBaseAboveOne_StaysUnclampedLikeToday()
    {
        // Today's model returns the vanilla value as is when there is no culture.
        Assert.AreEqual(1.3f, _sut.Compute(1.3f, null, null, false));
    }

    [TestMethod]
    public void Compute_CultureGiven_AppliesTheRespawnFeats()
    {
        FeatsAddFactor(0.2f);

        var result = _sut.Compute(0.5f, _culture, null, false);

        Assert.AreEqual(0.6f, result, 1e-6f);
        _feats.Received(1).ApplyVolunteerRespawnFeats(_culture, ref Arg.Any<ExplainedNumber>());
    }

    [TestMethod]
    public void Compute_CultureWithNoFeat_ReturnsTheBase()
    {
        Assert.AreEqual(0.5f, _sut.Compute(0.5f, _culture, null, false), 1e-6f);
    }

    [TestMethod]
    public void Compute_CultureFeatsPushPastOne_ClampsToOne()
    {
        FeatsAddFactor(0.5f);

        Assert.AreEqual(1f, _sut.Compute(0.9f, _culture, null, false));
    }

    [TestMethod]
    public void Compute_CultureFeatsPushBelowZero_ClampsToZero()
    {
        FeatsAddFactor(-2f);

        Assert.AreEqual(0f, _sut.Compute(0.5f, _culture, null, false));
    }

    [TestMethod]
    public void Compute_MultiplierOne_ReturnsExactlyTodaysValue()
    {
        FeatsAddFactor(0.2f);
        var today = _sut.Compute(0.5f, _culture, null, false);

        var withKingdom = _sut.Compute(0.5f, _culture, "empire_w", false);

        Assert.AreEqual(today, withKingdom);
    }

    [TestMethod]
    public void Compute_MultiplierAboveOne_ScalesTheValue()
    {
        _war.GetMultiplier("empire_w", WarEffectKind.VolunteerRate).Returns(1.2f);

        Assert.AreEqual(0.6f, _sut.Compute(0.5f, null, "empire_w", false), 1e-6f);
    }

    [TestMethod]
    public void Compute_MultiplierBelowOne_ScalesTheValueDown()
    {
        _war.GetMultiplier("isengard", WarEffectKind.VolunteerRate).Returns(0.8f);

        Assert.AreEqual(0.4f, _sut.Compute(0.5f, null, "isengard", false), 1e-6f);
    }

    [TestMethod]
    public void Compute_FeatsThenMultiplier_ComposeInThatOrder()
    {
        FeatsAddFactor(0.2f);
        _war.GetMultiplier("empire_w", WarEffectKind.VolunteerRate).Returns(1.5f);

        Assert.AreEqual(0.9f, _sut.Compute(0.5f, _culture, "empire_w", false), 1e-6f);
    }

    [TestMethod]
    public void Compute_MultiplierPushesPastOne_ClampsToOne()
    {
        _war.GetMultiplier("empire_w", WarEffectKind.VolunteerRate).Returns(1.5f);

        Assert.AreEqual(1f, _sut.Compute(0.9f, null, "empire_w", false));
    }

    [TestMethod]
    public void Compute_NullKingdomKey_AsksForTheMultiplierWithNull_AndKeepsTheValue()
    {
        Assert.AreEqual(0.5f, _sut.Compute(0.5f, null, null, false));
        _war.Received(1).GetMultiplier(null, WarEffectKind.VolunteerRate);
    }

    [TestMethod]
    public void Compute_PlayerClanOwner_IgnoresTheWarMultiplier()
    {
        _war.GetMultiplier("empire_w", WarEffectKind.VolunteerRate).Returns(1.5f);

        Assert.AreEqual(0.5f, _sut.Compute(0.5f, null, "empire_w", true));
        _war.DidNotReceiveWithAnyArgs().GetMultiplier(default, default);
    }

    [TestMethod]
    public void Compute_PlayerClanOwnerWithCultureFeats_KeepsTheFeats()
    {
        FeatsAddFactor(0.2f);
        _war.GetMultiplier("empire_w", WarEffectKind.VolunteerRate).Returns(1.5f);

        Assert.AreEqual(0.6f, _sut.Compute(0.5f, _culture, "empire_w", true), 1e-6f);
    }

    [TestMethod]
    public void Compute_FeatsResultNaN_ReturnsTheBaseWithoutTheWarStep()
    {
        // A multiplier other than 1, so an early return (0.5) differs from a fall-through that scales (0.75).
        FeatsAddFactor(float.NaN);
        _war.GetMultiplier("empire_w", WarEffectKind.VolunteerRate).Returns(1.5f);

        Assert.AreEqual(0.5f, _sut.Compute(0.5f, _culture, "empire_w", false));
    }

    [TestMethod]
    public void Compute_ScaledOverflows_ReturnsThePreMultiplierValue()
    {
        // No culture, so the value is not clamped before the multiplier and MaxValue x 1.5 overflows to +Inf.
        _war.GetMultiplier("empire_w", WarEffectKind.VolunteerRate).Returns(1.5f);

        Assert.AreEqual(float.MaxValue, _sut.Compute(float.MaxValue, null, "empire_w", false));
    }

    [TestMethod]
    public void Compute_NaNBase_ReturnsItUnchanged()
    {
        _war.GetMultiplier("empire_w", WarEffectKind.VolunteerRate).Returns(1.5f);

        var result = _sut.Compute(float.NaN, _culture, "empire_w", false);

        Assert.IsTrue(float.IsNaN(result));
        _feats.DidNotReceiveWithAnyArgs().ApplyVolunteerRespawnFeats(null, ref Arg.Any<ExplainedNumber>());
    }

    [TestMethod]
    public void Compute_InfiniteBase_ReturnsItUnchanged()
    {
        _war.GetMultiplier("empire_w", WarEffectKind.VolunteerRate).Returns(1.5f);

        Assert.AreEqual(float.PositiveInfinity, _sut.Compute(float.PositiveInfinity, _culture, "empire_w", false));
        _feats.DidNotReceiveWithAnyArgs().ApplyVolunteerRespawnFeats(null, ref Arg.Any<ExplainedNumber>());
    }

    [TestMethod]
    public void Compute_NaNMultiplier_ReturnsThePreMultiplierValue()
    {
        _war.GetMultiplier("empire_w", WarEffectKind.VolunteerRate).Returns(float.NaN);

        Assert.AreEqual(0.5f, _sut.Compute(0.5f, null, "empire_w", false));
    }

    [TestMethod]
    public void Compute_InfiniteMultiplier_ReturnsThePreMultiplierValue()
    {
        _war.GetMultiplier("empire_w", WarEffectKind.VolunteerRate).Returns(float.PositiveInfinity);

        Assert.AreEqual(0.5f, _sut.Compute(0.5f, null, "empire_w", false));
    }

    [TestMethod]
    public void Compute_ZeroBaseWithAMultiplier_StaysZero()
    {
        _war.GetMultiplier("empire_w", WarEffectKind.VolunteerRate).Returns(1.5f);

        Assert.AreEqual(0f, _sut.Compute(0f, null, "empire_w", false));
    }
}
