using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Features.SettlementNameplateRelation;

namespace TAOM.Tests.Features.SettlementNameplateRelation;

/// <summary>
/// The presenter's pure decision, the text-curve anchor (#596). The widget-writing methods need
/// a live UIContext and are covered by the in-game checklist.
/// </summary>
[TestClass]
public class SettlementPlatePresenterTests
{
    private INameplateRelationSettingsProvider _settings = null!;

    [TestInitialize]
    public void Setup()
    {
        _settings = Substitute.For<INameplateRelationSettingsProvider>();
        _settings.NeutralPlateAlpha.Returns(0.2f);
        _settings.RelationPlateAlpha.Returns(0.6f);
    }

    [TestMethod]
    public void RestingAlpha_Neutral_UsesNeutralOpacity()
        => Assert.AreEqual(0.2f, SettlementPlatePresenter.RestingAlpha(_settings, NameplateRelationPalette.Neutral, isTracked: false));

    [TestMethod]
    public void RestingAlpha_ColouredRelations_UseRelationOpacity()
    {
        Assert.AreEqual(0.6f, SettlementPlatePresenter.RestingAlpha(_settings, NameplateRelationPalette.SameFaction, isTracked: false));
        Assert.AreEqual(0.6f, SettlementPlatePresenter.RestingAlpha(_settings, NameplateRelationPalette.Enemy, isTracked: false));
        Assert.AreEqual(0.6f, SettlementPlatePresenter.RestingAlpha(_settings, NameplateRelationPalette.Ally, isTracked: false));
    }

    [TestMethod]
    public void RestingAlpha_Tracked_UsesVanillaMinimum()
    {
        // Vanilla owns the tracked 0.8; the sliders never move that plate, so its text anchor stays vanilla's.
        Assert.AreEqual(PlateAlphaPolicy.VanillaMinimumPlateAlpha,
            SettlementPlatePresenter.RestingAlpha(_settings, NameplateRelationPalette.Neutral, isTracked: true));
    }

    [TestMethod]
    public void RestingAlpha_UnknownRelationOrNoSettings_UsesVanillaMinimum()
    {
        Assert.AreEqual(PlateAlphaPolicy.VanillaMinimumPlateAlpha, SettlementPlatePresenter.RestingAlpha(_settings, 7, isTracked: false));
        Assert.AreEqual(PlateAlphaPolicy.VanillaMinimumPlateAlpha, SettlementPlatePresenter.RestingAlpha(_settings, -1, isTracked: false));
        Assert.AreEqual(PlateAlphaPolicy.VanillaMinimumPlateAlpha, SettlementPlatePresenter.RestingAlpha(null, NameplateRelationPalette.Enemy, isTracked: false));
    }
}
