using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Features.FieldCamp;

namespace TAOM.Tests.Features.FieldCamp;

/// <summary>
/// The lookout sight bonus: the pure source math (AmbushMath.ComputeLookoutBonus = 0.2 +
/// scouting/200, LEADER-based at the call site) and the gates that are testable without a live
/// <c>MobileParty</c> (null tolerance, master toggle). The party-shaped path is boundary code,
/// exercised in game.
/// </summary>
[TestClass]
public class LookoutSpottingContributorTests
{
    [TestMethod]
    public void ComputeLookoutBonus_SourceFormula()
    {
        // 0.2 + 100/200 = 0.7
        Assert.AreEqual(0.7f, LookoutSpottingContributor.ComputeLookoutBonus(100f), 0.0001f);
    }

    [TestMethod]
    public void ComputeLookoutBonus_ZeroSkill_FlatBonusOnly()
    {
        Assert.AreEqual(0.2f, LookoutSpottingContributor.ComputeLookoutBonus(0f), 0.0001f);
    }

    [TestMethod]
    public void ComputeLookoutBonus_NegativeSkill_DegradesToFlatBonus()
    {
        Assert.AreEqual(0.2f, LookoutSpottingContributor.ComputeLookoutBonus(-50f), 0.0001f);
    }

    [TestMethod]
    public void ComputeLookoutBonus_ExtremeSkill_NoArbitraryCutoff()
    {
        // The port used to zero the whole skill term at >= 1000; the source had no such cliff.
        // 0.2 + 1200/200 = 6.2
        Assert.AreEqual(6.2f, LookoutSpottingContributor.ComputeLookoutBonus(1200f), 0.0001f);
    }

    [TestMethod]
    public void ComputeLookoutBonus_NonFiniteSkill_DegradesToFlatBonus()
    {
        Assert.AreEqual(0.2f, LookoutSpottingContributor.ComputeLookoutBonus(float.NaN), 0.0001f);
        Assert.AreEqual(0.2f, LookoutSpottingContributor.ComputeLookoutBonus(float.PositiveInfinity), 0.0001f);
    }

    [TestMethod]
    public void GetSpottingRangeBonusFactor_NullParty_ReturnsZero()
    {
        // Enabled=true so the null gate itself is what answers, not the settings gate.
        var settings = Substitute.For<ICampSettingsProvider>();
        settings.Enabled.Returns(true);
        var sut = new LookoutSpottingContributor(Substitute.For<ICampService>(), settings);

        Assert.AreEqual(0f, sut.GetSpottingRangeBonusFactor(null!));
    }

    [TestMethod]
    public void GetSpottingRangeBonusFactor_FeatureDisabled_ReturnsZeroWithoutTouchingTheBook()
    {
        // Toggle-off matrix: a leftover lookout must not keep boosting a simulation-relevant
        // range while the feature is off. The settings gate comes FIRST, before any camp probe.
        var camps = Substitute.For<ICampService>();
        var settings = Substitute.For<ICampSettingsProvider>();
        settings.Enabled.Returns(false);
        var sut = new LookoutSpottingContributor(camps, settings);

        Assert.AreEqual(0f, sut.GetSpottingRangeBonusFactor(null!));
        _ = camps.DidNotReceive().PlayerCamp;
    }
}
