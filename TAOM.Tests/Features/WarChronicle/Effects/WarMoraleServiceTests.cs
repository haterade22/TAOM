using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Features.WarChronicle.Effects;

namespace TAOM.Tests.Features.WarChronicle.Effects;

/// <summary>
/// The Tidings of the war party morale bonus: the kingdom's PartyMorale multiplier minus one, times vanilla's
/// standard base morale (50), so +0.10 is +5 points; nothing for a player's clan (rule D4), a kingdom-less
/// party, no effect, or a value that is not a finite change. <see cref="WarMoraleService.Apply"/> only adds
/// this number to an ExplainedNumber, which needs the game, so the number is tested here.
/// </summary>
[TestClass]
public class WarMoraleServiceTests
{
    private IWarEffectService _war = null!;
    private WarMoraleService _sut = null!;

    [TestInitialize]
    public void Setup()
    {
        _war = Substitute.For<IWarEffectService>();
        _war.GetMultiplier(Arg.Any<string>(), Arg.Any<WarEffectKind>()).Returns(1f);
        _sut = new WarMoraleService(_war);
    }

    [TestMethod]
    public void IsActiveFor_IsTrueOnlyForAKingdomWithAMoraleEffect()
    {
        _war.GetMultiplier("vlandia", WarEffectKind.PartyMorale).Returns(1.1f);
        _war.GetMultiplier("empire_w", WarEffectKind.PartyMorale).Returns(1f);

        Assert.IsTrue(_sut.IsActiveFor("vlandia"));
        Assert.IsFalse(_sut.IsActiveFor("empire_w"));
        Assert.IsFalse(_sut.IsActiveFor(null));
    }

    [TestMethod]
    public void Bonus_PlusTenPercent_IsFivePointsOnABaseOfFifty()
    {
        _war.GetMultiplier("vlandia", WarEffectKind.PartyMorale).Returns(1.1f);

        Assert.AreEqual(5f, _sut.Bonus("vlandia", false, 50f), 1e-4f);
    }

    [TestMethod]
    public void Bonus_AMinus20PercentEffect_IsAPenalty()
    {
        _war.GetMultiplier("vlandia", WarEffectKind.PartyMorale).Returns(0.8f);

        Assert.AreEqual(-10f, _sut.Bonus("vlandia", false, 50f), 1e-4f);
    }

    [TestMethod]
    public void Bonus_APlayersClan_IsZeroWithoutAskingTheRegistry()
    {
        _war.GetMultiplier("vlandia", WarEffectKind.PartyMorale).Returns(1.1f);

        Assert.AreEqual(0f, _sut.Bonus("vlandia", true, 50f));
        _war.DidNotReceiveWithAnyArgs().GetMultiplier(default, default);
    }

    [TestMethod]
    public void Bonus_NoKingdom_IsZero()
    {
        Assert.AreEqual(0f, _sut.Bonus(null, false, 50f));
        _war.DidNotReceiveWithAnyArgs().GetMultiplier(default, default);
    }

    [TestMethod]
    public void Bonus_NoEffect_IsZero()
    {
        Assert.AreEqual(0f, _sut.Bonus("vlandia", false, 50f));
    }

    [DataTestMethod]
    [DataRow(float.NaN, 50f)]
    [DataRow(float.PositiveInfinity, 50f)]
    [DataRow(1.1f, float.NaN)]
    [DataRow(1.1f, float.PositiveInfinity)]
    public void Bonus_ANonFiniteInput_IsZero(float multiplier, float baseMorale)
    {
        _war.GetMultiplier("vlandia", WarEffectKind.PartyMorale).Returns(multiplier);

        Assert.AreEqual(0f, _sut.Bonus("vlandia", false, baseMorale));
    }
}
