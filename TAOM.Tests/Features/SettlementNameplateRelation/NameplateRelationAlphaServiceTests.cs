using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Features.SettlementNameplateRelation;

namespace TAOM.Tests.Features.SettlementNameplateRelation;

[TestClass]
public class NameplateRelationAlphaServiceTests
{
    private INameplateRelationSettingsProvider _settings = null!;
    private NameplateRelationAlphaService _sut = null!;

    [TestInitialize]
    public void Setup()
    {
        // The defaults reproduce vanilla: neutral 0.35, own faction 0.5; enemy and allied are
        // lifted from vanilla's 0.35 to the same 0.5 (#591).
        _settings = Substitute.For<INameplateRelationSettingsProvider>();
        _settings.NeutralPlateAlpha.Returns(0.35f);
        _settings.RelationPlateAlpha.Returns(0.5f);
        _sut = new NameplateRelationAlphaService(_settings);
    }

    [TestMethod]
    public void Adjust_Neutral_ReturnsNeutralPlateAlpha()
        => Assert.AreEqual(0.35f, _sut.Adjust(0.35f, NameplateRelationPalette.Neutral, isTracked: false));

    [TestMethod]
    public void Adjust_SameFaction_ReturnsRelationPlateAlpha()
        => Assert.AreEqual(0.5f, _sut.Adjust(0.5f, NameplateRelationPalette.SameFaction, isTracked: false));

    [TestMethod]
    public void Adjust_EnemyUntracked_RaisesToRelationPlateAlpha()
        => Assert.AreEqual(0.5f, _sut.Adjust(0.35f, NameplateRelationPalette.Enemy, isTracked: false));

    [TestMethod]
    public void Adjust_AllyUntracked_RaisesToRelationPlateAlpha()
        => Assert.AreEqual(0.5f, _sut.Adjust(0.35f, NameplateRelationPalette.Ally, isTracked: false));

    [TestMethod]
    public void Adjust_CustomOpacities_ApplyByRelation()
    {
        // The MCM sliders (#596): players may raise neutral plates over dark terrain or dim them.
        _settings.NeutralPlateAlpha.Returns(0.6f);
        _settings.RelationPlateAlpha.Returns(0.75f);

        Assert.AreEqual(0.6f, _sut.Adjust(0.35f, NameplateRelationPalette.Neutral, isTracked: false));
        Assert.AreEqual(0.75f, _sut.Adjust(0.5f, NameplateRelationPalette.SameFaction, isTracked: false));
        Assert.AreEqual(0.75f, _sut.Adjust(0.35f, NameplateRelationPalette.Enemy, isTracked: false));
        Assert.AreEqual(0.75f, _sut.Adjust(0.35f, NameplateRelationPalette.Ally, isTracked: false));
    }

    [TestMethod]
    public void Adjust_CustomOpacities_CanLowerBelowVanilla()
    {
        _settings.NeutralPlateAlpha.Returns(0.2f);
        _settings.RelationPlateAlpha.Returns(0.3f);

        Assert.AreEqual(0.2f, _sut.Adjust(0.35f, NameplateRelationPalette.Neutral, isTracked: false));
        Assert.AreEqual(0.3f, _sut.Adjust(0.5f, NameplateRelationPalette.SameFaction, isTracked: false));
    }

    [TestMethod]
    public void Adjust_Tracked_Unchanged()
    {
        // Tracked inside the window is 0.8, tracked at the screen edge is 1; both stay vanilla.
        Assert.AreEqual(0.8f, _sut.Adjust(0.8f, NameplateRelationPalette.Enemy, isTracked: true));
        Assert.AreEqual(1f, _sut.Adjust(1f, NameplateRelationPalette.Neutral, isTracked: true));
    }

    [TestMethod]
    public void Adjust_ZeroTarget_StaysZero()
    {
        // Off-window plates come in at 0 and must stay hidden.
        Assert.AreEqual(0f, _sut.Adjust(0f, NameplateRelationPalette.Enemy, isTracked: false));
        Assert.AreEqual(0f, _sut.Adjust(0f, NameplateRelationPalette.Neutral, isTracked: false));
    }

    [TestMethod]
    public void Adjust_NaNTarget_ReturnsNaNUntouched()
        => Assert.IsTrue(float.IsNaN(_sut.Adjust(float.NaN, NameplateRelationPalette.Enemy, isTracked: false)));

    [TestMethod]
    public void Adjust_UnknownRelation_Unchanged()
    {
        Assert.AreEqual(0.35f, _sut.Adjust(0.35f, 7, isTracked: false));
        Assert.AreEqual(0.35f, _sut.Adjust(0.35f, -1, isTracked: false));
    }

    [TestMethod]
    public void Adjust_ProviderValueNotPositive_ReturnsVanillaTarget()
    {
        // The provider validates, but the service never trusts a value it is about to hand the
        // renderer: a zero or non-finite setting leaves vanilla's target alone.
        _settings.NeutralPlateAlpha.Returns(0f);
        _settings.RelationPlateAlpha.Returns(float.NaN);

        Assert.AreEqual(0.35f, _sut.Adjust(0.35f, NameplateRelationPalette.Neutral, isTracked: false));
        Assert.AreEqual(0.35f, _sut.Adjust(0.35f, NameplateRelationPalette.Enemy, isTracked: false));
    }
}
