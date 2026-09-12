using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features;
using TAOM.Features.TimeAcceleration;

namespace TAOM.Tests.Features.TimeAcceleration;

/// <summary>
/// Pins the provider's no-MCM fallbacks to the compiled defaults, and the one ordering rule the
/// two fast-forward sliders carry: both range 1 to 128 independently in MCM, so without a floor a
/// player can make "extra" slower than "fast", which would also make the MapBar button's lit state
/// (a fast-forward mode running ABOVE the normal multiplier) unreachable.
/// </summary>
[TestClass]
public class TimeAccelerationSettingsProviderTests
{
    private static TimeAccelerationSettingsProvider Provider() => new TimeAccelerationSettingsProvider();

    [TestMethod]
    public void WithoutMcm_Multipliers_MatchTheCompiledDefaults()
    {
        var settings = new TaomSettings();
        var provider = Provider();

        Assert.AreEqual(settings.FastForwardMultiplier, provider.FastForwardMultiplier);
        Assert.AreEqual(settings.ExtraFastForwardMultiplier, provider.ExtraFastForwardMultiplier);
        Assert.AreEqual(settings.CtrlSpaceMultiplier, provider.CtrlSpaceMultiplier);
    }

    [TestMethod]
    public void CompiledDefaults_AreOrderedFastBelowExtraBelowTurbo()
    {
        var settings = new TaomSettings();

        Assert.IsTrue(settings.FastForwardMultiplier < settings.ExtraFastForwardMultiplier);
        Assert.IsTrue(settings.ExtraFastForwardMultiplier < settings.CtrlSpaceMultiplier);
    }

    [TestMethod]
    public void ClampExtra_ExtraBelowFast_ReturnsFast()
    {
        Assert.AreEqual(6, TimeAccelerationSettingsProvider.ClampExtra(fast: 6, extra: 2));
    }

    [TestMethod]
    public void ClampExtra_ExtraAboveFast_ReturnsExtra()
    {
        Assert.AreEqual(12, TimeAccelerationSettingsProvider.ClampExtra(fast: 4, extra: 12));
    }

    [TestMethod]
    public void ClampExtra_ExtraEqualsFast_ReturnsThatValue()
    {
        Assert.AreEqual(4, TimeAccelerationSettingsProvider.ClampExtra(fast: 4, extra: 4));
    }
}
