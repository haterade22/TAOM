using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features;
using TAOM.Features.SiegePropDiagnostics;

namespace TAOM.Tests.Features.SiegePropDiagnostics;

/// <summary>
/// Read every frame by SiegePropDiagnosticsMissionBehavior, so the provider caches the MCM reference
/// and reads through it. <c>TaomSettings.Instance</c> is null in tests, so the public constructor pins
/// the no-MCM fallbacks (off: a diagnostic must cost nothing by default).
/// </summary>
[TestClass]
public class SiegePropDiagnosticsSettingsProviderTests
{
    [TestMethod]
    public void IsEnabled_NoMcm_DefaultsFalse()
        => Assert.IsFalse(new SiegePropDiagnosticsSettingsProvider().IsEnabled);

    [TestMethod]
    public void IsVerbose_NoMcm_DefaultsFalse()
        => Assert.IsFalse(new SiegePropDiagnosticsSettingsProvider().IsVerbose);

    [TestMethod]
    public void IsEnabled_ReadsThroughTheCachedSettings_SoLiveMcmEditsApply()
    {
        var mcm = new TaomSettings();
        var sut = new SiegePropDiagnosticsSettingsProvider(mcm);
        _ = sut.IsEnabled;
        _ = sut.IsVerbose;

        mcm.EnableSiegePropDiagnostics = true;

        Assert.IsTrue(sut.IsEnabled);
    }

    [TestMethod]
    public void IsVerbose_ReadsThroughTheCachedSettings_SoLiveMcmEditsApply()
    {
        var mcm = new TaomSettings();
        var sut = new SiegePropDiagnosticsSettingsProvider(mcm);
        _ = sut.IsEnabled;
        _ = sut.IsVerbose;

        mcm.SiegePropDiagnosticsVerbose = true;

        Assert.IsTrue(sut.IsVerbose);
    }
}
