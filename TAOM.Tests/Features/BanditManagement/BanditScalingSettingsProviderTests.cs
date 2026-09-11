using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features;
using TAOM.Features.BanditManagement;

namespace TAOM.Tests.Features.BanditManagement;

/// <summary>
/// The provider's fallback (what runs when <c>TaomSettings.Instance</c> is null, which is the
/// state outside the game) and the compiled MCM default must agree, or the feature behaves one way
/// before <c>TAOM.json</c> is first written and another way after. Until #559 these lived in a
/// shipped JSON that MCM shadowed on every real install and nothing pinned; now they are constants
/// in the provider and this test is the pin.
/// </summary>
[TestClass]
public class BanditScalingSettingsProviderTests
{
    private static BanditScalingSettingsProvider Provider() => new BanditScalingSettingsProvider();

    [TestMethod]
    public void WithoutMcm_IsEnabled_FallsBackToOn()
    {
        Assert.IsTrue(Provider().IsEnabled);
        Assert.IsTrue(new TaomSettings().EnableBanditScaling);
    }

    [TestMethod]
    public void WithoutMcm_Curves_MatchTheCompiledDefaults()
    {
        var settings = new TaomSettings();
        var provider = Provider();
        Assert.AreEqual(settings.BanditDensityCurve, provider.DensityCurve);
        Assert.AreEqual(settings.BanditPartySizeCurve, provider.PartySizeCurve);
        Assert.AreEqual(settings.BanditBossFightCurve, provider.BossFightCurve);
    }

    [TestMethod]
    public void WithoutMcm_Caps_MatchTheCompiledDefaults()
    {
        var settings = new TaomSettings();
        var provider = Provider();
        Assert.AreEqual(settings.BanditMaxHideoutsPerFaction, provider.MaxHideoutsPerFactionCap);
        Assert.AreEqual(settings.BanditMaxPartiesPerHideout, provider.MaxPartiesPerHideoutCap);
        Assert.AreEqual(settings.BanditInitialHideoutsPerFaction, provider.InitialHideoutsPerFaction);
    }

    [TestMethod]
    public void ShippedDefaults_AreTheOnesDecidedIn559()
    {
        // 8 bandit factions x 14 initial hideouts put 112 on a fresh map (vanilla 6 x 7 = 42), and
        // a parties-per-hideout cap of 3 equalled vanilla's base, so Density Curve could not move it.
        var settings = new TaomSettings();
        Assert.AreEqual(7, settings.BanditInitialHideoutsPerFaction, "vanilla per faction; 56 total across 8 factions");
        Assert.AreEqual(6, settings.BanditMaxPartiesPerHideout, "above vanilla's 3 so the curve has room");
    }

    [TestMethod]
    public void MinPartiesToInfest_IsOne_AndNeverExceedsTheCap()
    {
        var provider = Provider();
        Assert.AreEqual(1, provider.MinPartiesToInfest);
        Assert.IsTrue(provider.MinPartiesToInfest <= provider.MaxPartiesPerHideoutCap);
    }
}
