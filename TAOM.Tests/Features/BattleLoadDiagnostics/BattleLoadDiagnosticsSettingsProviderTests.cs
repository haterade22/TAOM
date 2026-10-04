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
    public void MissionTickStallSamplerEnabled_NoMcmInstance_DefaultsTrue()
    {
        var sut = new BattleLoadDiagnosticsSettingsProvider();
        Assert.IsTrue(sut.MissionTickStallSamplerEnabled);
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

    // The tick profiler installs Harmony patches, so its toggle fails CLOSED when MCM is not ready,
    // unlike every sibling above.
    [TestMethod]
    public void TickProfilerEnabled_NoMcmInstance_DefaultsFalse()
    {
        var sut = new BattleLoadDiagnosticsSettingsProvider();
        Assert.IsFalse(sut.TickProfilerEnabled);
    }

    // The hitch probe installs Patch98, so it fails CLOSED when MCM is not ready, like the tick profiler,
    // although its compiled MCM default is on.
    [TestMethod]
    public void HitchProbeEnabled_NoMcmInstance_DefaultsFalse()
    {
        var sut = new BattleLoadDiagnosticsSettingsProvider();
        Assert.IsFalse(sut.HitchProbeEnabled);
    }

    [TestMethod]
    public void EnableHitchProbe_CompiledDefault_IsTrue()
    {
        Assert.IsTrue(new BattleLoadDiagnosticsSettings().EnableHitchProbe);
    }

    // The map profiler installs Harmony patches on the campaign map's per-frame methods, so its
    // toggle fails CLOSED too.
    [TestMethod]
    public void MapProfilerEnabled_NoMcmInstance_DefaultsFalse()
    {
        Assert.IsFalse(new BattleLoadDiagnosticsSettingsProvider().MapProfilerEnabled);
    }

    [TestMethod]
    public void TickProfilerTopN_NoMcmInstance_Defaults8()
    {
        var sut = new BattleLoadDiagnosticsSettingsProvider();
        Assert.AreEqual(8, sut.TickProfilerTopN);
    }

    [TestMethod]
    public void HitchThresholdMs_NoMcmInstance_Defaults250()
    {
        var sut = new BattleLoadDiagnosticsSettingsProvider();
        Assert.AreEqual(250d, sut.HitchThresholdMs);
    }

    [TestMethod]
    public void ValidateTickProfilerTopN_OutOfRange_Returns8()
    {
        Assert.AreEqual(8, BattleLoadDiagnosticsSettingsProvider.ValidateTickProfilerTopN(0));
        Assert.AreEqual(8, BattleLoadDiagnosticsSettingsProvider.ValidateTickProfilerTopN(21));
        Assert.AreEqual(8, BattleLoadDiagnosticsSettingsProvider.ValidateTickProfilerTopN(-5));
    }

    [TestMethod]
    public void ValidateTickProfilerTopN_RangeEdges_ReturnRaw()
    {
        Assert.AreEqual(1, BattleLoadDiagnosticsSettingsProvider.ValidateTickProfilerTopN(1));
        Assert.AreEqual(20, BattleLoadDiagnosticsSettingsProvider.ValidateTickProfilerTopN(20));
    }

    [TestMethod]
    public void ValidateHitchThresholdMs_NaN_Returns250()
        => Assert.AreEqual(250d, BattleLoadDiagnosticsSettingsProvider.ValidateHitchThresholdMs(double.NaN));

    [TestMethod]
    public void ValidateHitchThresholdMs_Infinity_Returns250()
    {
        Assert.AreEqual(250d, BattleLoadDiagnosticsSettingsProvider.ValidateHitchThresholdMs(double.PositiveInfinity));
        Assert.AreEqual(250d, BattleLoadDiagnosticsSettingsProvider.ValidateHitchThresholdMs(double.NegativeInfinity));
    }

    [TestMethod]
    public void ValidateHitchThresholdMs_OutOfRange_Returns250()
    {
        Assert.AreEqual(250d, BattleLoadDiagnosticsSettingsProvider.ValidateHitchThresholdMs(49d));
        Assert.AreEqual(250d, BattleLoadDiagnosticsSettingsProvider.ValidateHitchThresholdMs(2001d));
    }

    [TestMethod]
    public void ValidateHitchThresholdMs_RangeEdges_ReturnRaw()
    {
        Assert.AreEqual(50d, BattleLoadDiagnosticsSettingsProvider.ValidateHitchThresholdMs(50d));
        Assert.AreEqual(2000d, BattleLoadDiagnosticsSettingsProvider.ValidateHitchThresholdMs(2000d));
    }
}
