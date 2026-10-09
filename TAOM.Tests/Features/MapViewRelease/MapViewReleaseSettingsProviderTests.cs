using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.BattleLoadDiagnostics;
using TAOM.Features.MapViewRelease;

namespace TAOM.Tests.Features.MapViewRelease;

/// <summary>The map-view release's settings provider. Untagged: it needs no game assemblies, so a no-game run executes it.</summary>
[TestClass]
public class MapViewReleaseSettingsProviderTests
{
    [TestMethod]
    public void SettingsProvider_NoMcmInstance_DefaultsToOnEveryTwentieth()
    {
        var sut = new MapViewReleaseSettingsProvider();

        Assert.IsTrue(sut.ReleaseEnabled);
        Assert.AreEqual(20, sut.ReleaseInterval);
    }

    [TestMethod]
    public void SettingsProvider_ReadsThroughTheSettings_AndClampsTheInterval()
    {
        var settings = new BattleLoadDiagnosticsSettings();
        var sut = new MapViewReleaseSettingsProvider(settings);

        settings.ReleaseMapViewMemory = false;
        settings.MapViewReleaseInterval = 5000;

        Assert.IsFalse(sut.ReleaseEnabled);
        Assert.AreEqual(1000, sut.ReleaseInterval, "an out-of-range stored value is clamped, not trusted");

        settings.MapViewReleaseInterval = 0;
        Assert.AreEqual(1, sut.ReleaseInterval);
    }
}
