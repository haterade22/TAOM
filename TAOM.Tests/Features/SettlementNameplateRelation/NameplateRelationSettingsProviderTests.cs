using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Core.Logging;
using TAOM.Features.SettlementNameplateRelation;

namespace TAOM.Tests.Features.SettlementNameplateRelation;

[TestClass]
public class NameplateRelationSettingsProviderTests
{
    // TaomSettings.Instance is null in test environments (MCM v5 is not loaded), so the provider
    // falls back to its compiled defaults. These pin the fallbacks to the MCM defaults declared in
    // TaomSettings.cs; drift would silently change the plates before MCM finishes initialising.
    // The live-instance mapping (property to property) is by inspection; a TaomSettings cannot be
    // constructed here without MCM.

    private IModLogger _logger = null!;
    private NameplateRelationSettingsProvider _sut = null!;

    [TestInitialize]
    public void Setup()
    {
        _logger = Substitute.For<IModLogger>();
        _sut = new NameplateRelationSettingsProvider(_logger);
    }

    [TestMethod]
    public void ColorsEnabled_NoMcmInstance_DefaultsToTrue()
        => Assert.IsTrue(_sut.ColorsEnabled);

    [TestMethod]
    public void TintStrength_NoMcmInstance_DefaultsToOne()
        => Assert.AreEqual(1f, _sut.TintStrength);

    [TestMethod]
    public void NeutralPlateAlpha_NoMcmInstance_DefaultsToVanillaNeutral()
        => Assert.AreEqual(0.35f, _sut.NeutralPlateAlpha, 0.0001f);

    [TestMethod]
    public void RelationPlateAlpha_NoMcmInstance_DefaultsToVanillaOwnFaction()
        => Assert.AreEqual(0.5f, _sut.RelationPlateAlpha, 0.0001f);

    [TestMethod]
    public void Defaults_NoMcmInstance_LogNothing()
    {
        _ = _sut.TintStrength;
        _ = _sut.NeutralPlateAlpha;
        _ = _sut.RelationPlateAlpha;

        _logger.DidNotReceive().LogWarning(Arg.Any<string>());
    }

    [TestMethod]
    public void NullLogger_InvalidValue_StillRevertsWithoutThrowing()
    {
        var quiet = new NameplateRelationSettingsProvider(null);

        Assert.AreEqual(0.5f, quiet.Read(0, "X", float.NaN, 50f, 10f, 100f), 0.0001f);
    }

    [TestMethod]
    public void NormalizePercent_InRange_ReturnsFraction()
    {
        Assert.AreEqual(0.35f, NameplateRelationSettingsProvider.NormalizePercent(35f, 50f, 10f, 100f), 0.0001f);
        Assert.AreEqual(1f, NameplateRelationSettingsProvider.NormalizePercent(100f, 50f, 0f, 100f), 0.0001f);
        Assert.AreEqual(0f, NameplateRelationSettingsProvider.NormalizePercent(0f, 50f, 0f, 100f), 0.0001f);
    }

    [TestMethod]
    public void NormalizePercent_NonFinite_ReturnsDefaultFraction()
    {
        // NaN fails every range compare, so it must be rejected before the range check.
        Assert.AreEqual(0.5f, NameplateRelationSettingsProvider.NormalizePercent(float.NaN, 50f, 10f, 100f), 0.0001f);
        Assert.AreEqual(0.5f, NameplateRelationSettingsProvider.NormalizePercent(float.PositiveInfinity, 50f, 10f, 100f), 0.0001f);
        Assert.AreEqual(0.5f, NameplateRelationSettingsProvider.NormalizePercent(float.NegativeInfinity, 50f, 10f, 100f), 0.0001f);
    }

    [TestMethod]
    public void NormalizePercent_OutOfRange_ReturnsDefaultFraction()
    {
        // A hand-edited TAOM.json can hold anything; below the slider floor or above 100 revert.
        Assert.AreEqual(0.5f, NameplateRelationSettingsProvider.NormalizePercent(5f, 50f, 10f, 100f), 0.0001f);
        Assert.AreEqual(0.5f, NameplateRelationSettingsProvider.NormalizePercent(150f, 50f, 10f, 100f), 0.0001f);
        Assert.AreEqual(0.5f, NameplateRelationSettingsProvider.NormalizePercent(-1f, 50f, 0f, 100f), 0.0001f);
    }

    [TestMethod]
    public void Read_ValidValue_ReturnsFractionAndLogsNothing()
    {
        Assert.AreEqual(0.2f, _sut.Read(0, "NameplateNeutralPlateOpacity", 20f, 35f, 10f, 100f), 0.0001f);

        _logger.DidNotReceive().LogWarning(Arg.Any<string>());
    }

    [TestMethod]
    public void Read_InvalidValue_RevertsAndWarnsOncePerProperty()
    {
        // The reads run every frame; the config-validation rule wants the reversion reported, not
        // the log flooded. One warning per property, naming the value, range and default.
        var first = _sut.Read(1, "NameplateNeutralPlateOpacity", -5f, 35f, 10f, 100f);
        var second = _sut.Read(1, "NameplateNeutralPlateOpacity", -5f, 35f, 10f, 100f);
        var third = _sut.Read(1, "NameplateNeutralPlateOpacity", float.NaN, 35f, 10f, 100f);

        Assert.AreEqual(0.35f, first, 0.0001f);
        Assert.AreEqual(0.35f, second, 0.0001f);
        Assert.AreEqual(0.35f, third, 0.0001f);
        _logger.Received(1).LogWarning(Arg.Is<string>(m =>
            m.Contains("NameplateNeutralPlateOpacity") && m.Contains("-5") && m.Contains("[10, 100]") && m.Contains("35")));
    }

    [TestMethod]
    public void Read_InvalidValuesOnTwoProperties_WarnsOnceEach()
    {
        _sut.Read(0, "NameplateRelationTintStrength", 250f, 100f, 0f, 100f);
        _sut.Read(0, "NameplateRelationTintStrength", 250f, 100f, 0f, 100f);
        _sut.Read(2, "NameplateRelationPlateOpacity", 0f, 50f, 10f, 100f);

        _logger.Received(1).LogWarning(Arg.Is<string>(m => m.Contains("NameplateRelationTintStrength")));
        _logger.Received(1).LogWarning(Arg.Is<string>(m => m.Contains("NameplateRelationPlateOpacity")));
    }
}
