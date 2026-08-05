using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.BattleLoadDiagnostics;

namespace TAOM.Tests.Features.BattleLoadDiagnostics;

/// <summary>
/// BattleLoadDiagnosticsSettings.Instance is null in test environments (MCM isn't loaded),
/// so the provider falls back to its compiled fail-open defaults — pinned here so drift
/// between the MCM attribute defaults and the provider fallbacks can't silently change the
/// pre-MCM posture. The interval validation itself is a pure seam (the MCM static can't be
/// faked), exercised directly with out-of-range/NaN inputs per the config-validation rule.
/// </summary>
[TestClass]
public class BattleLoadDiagnosticsSettingsProviderTests
{
    [TestMethod]
    public void MemorySamplerEnabled_NoMcmInstance_DefaultsTrue()
    {
        var sut = new BattleLoadDiagnosticsSettingsProvider();
        Assert.IsTrue(sut.MemorySamplerEnabled);
    }

    [TestMethod]
    public void MemorySampleIntervalSeconds_NoMcmInstance_Defaults30()
    {
        var sut = new BattleLoadDiagnosticsSettingsProvider();
        Assert.AreEqual(30d, sut.MemorySampleIntervalSeconds);
    }

    [TestMethod]
    public void ValidateSampleIntervalSeconds_BelowRange_Returns30()
        => Assert.AreEqual(30d, BattleLoadDiagnosticsSettingsProvider.ValidateSampleIntervalSeconds(5d));

    [TestMethod]
    public void ValidateSampleIntervalSeconds_AboveRange_Returns30()
        => Assert.AreEqual(30d, BattleLoadDiagnosticsSettingsProvider.ValidateSampleIntervalSeconds(200d));

    [TestMethod]
    public void ValidateSampleIntervalSeconds_NaN_Returns30()
        => Assert.AreEqual(30d, BattleLoadDiagnosticsSettingsProvider.ValidateSampleIntervalSeconds(double.NaN));

    [TestMethod]
    public void ValidateSampleIntervalSeconds_PositiveInfinity_Returns30()
        => Assert.AreEqual(30d, BattleLoadDiagnosticsSettingsProvider.ValidateSampleIntervalSeconds(double.PositiveInfinity));

    [TestMethod]
    public void ValidateSampleIntervalSeconds_InRange_ReturnsRaw()
        => Assert.AreEqual(45d, BattleLoadDiagnosticsSettingsProvider.ValidateSampleIntervalSeconds(45d));

    [TestMethod]
    public void ValidateSampleIntervalSeconds_RangeEdges_ReturnRaw()
    {
        Assert.AreEqual(10d, BattleLoadDiagnosticsSettingsProvider.ValidateSampleIntervalSeconds(10d));
        Assert.AreEqual(120d, BattleLoadDiagnosticsSettingsProvider.ValidateSampleIntervalSeconds(120d));
    }
}
