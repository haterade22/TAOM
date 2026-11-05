using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.BlowDiagnostics;

namespace TAOM.Tests.Features.BlowDiagnostics;

/// <summary>
/// The provider is read per damaging blow even while OFF, so it caches the MCM reference and reads
/// through it. <c>BlowDiagnosticsSettings.Instance</c> is null in tests (MCM is never initialised), so
/// the public constructor pins the fail-open-to-OFF fallback.
/// </summary>
[TestClass]
public class BlowDiagnosticsSettingsProviderTests
{
    [TestMethod]
    public void IsEnabled_NoMcm_DefaultsOff()
        => Assert.IsFalse(new BlowDiagnosticsSettingsProvider().IsEnabled);

    [TestMethod]
    public void CompiledDefault_MatchesTheProviderFallback()
        => Assert.IsFalse(new BlowDiagnosticsSettings().EnableBlowDiagnostics);

    [TestMethod]
    public void IsEnabled_ReadsThroughTheCachedSettings_SoLiveMcmEditsApply()
    {
        var mcm = new BlowDiagnosticsSettings();
        var sut = new BlowDiagnosticsSettingsProvider(mcm);

        Assert.IsFalse(sut.IsEnabled, "compiled default");
        mcm.EnableBlowDiagnostics = true;
        Assert.IsTrue(sut.IsEnabled, "after switching on in MCM");
        mcm.EnableBlowDiagnostics = false;
        Assert.IsFalse(sut.IsEnabled, "after switching off again");
    }
}
