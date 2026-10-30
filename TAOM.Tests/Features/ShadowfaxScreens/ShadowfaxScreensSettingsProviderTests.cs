using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.BattleLoadDiagnostics;
using TAOM.Features.ShadowfaxScreens;

namespace TAOM.Tests.Features.ShadowfaxScreens;

[TestClass]
public class ShadowfaxScreensSettingsProviderTests
{
    [TestMethod]
    public void NoMcm_FastOpenSwitchesDefaultOn_AndTheTimerOff()
    {
        var sut = new ShadowfaxScreensSettingsProvider();

        Assert.IsFalse(sut.LogScreenOpenTimes);
        Assert.IsTrue(sut.ClanFastOpen);
        Assert.IsTrue(sut.InventoryFastOpen);
        Assert.IsTrue(sut.PartyFastOpen);
        Assert.IsTrue(sut.QuestLogFastOpen);
    }

    [TestMethod]
    public void TheMcmPage_ShipsTheFastOpenSwitchesOn_AndTheTimerOff()
    {
        var settings = new BattleLoadDiagnosticsSettings();

        Assert.IsFalse(settings.LogScreenOpenTimes);
        Assert.IsTrue(settings.ClanFastOpen);
        Assert.IsTrue(settings.InventoryFastOpen);
        Assert.IsTrue(settings.PartyFastOpen);
        Assert.IsTrue(settings.QuestLogFastOpen);
    }

    [TestMethod]
    public void ReadsThroughTheCachedSettings()
    {
        var settings = new BattleLoadDiagnosticsSettings();
        var sut = new ShadowfaxScreensSettingsProvider(settings);
        Assert.IsFalse(sut.LogScreenOpenTimes);
        Assert.IsTrue(sut.ClanFastOpen);

        settings.LogScreenOpenTimes = true;
        settings.ClanFastOpen = false;
        settings.InventoryFastOpen = false;
        settings.PartyFastOpen = false;
        settings.QuestLogFastOpen = false;

        Assert.IsTrue(sut.LogScreenOpenTimes);
        Assert.IsFalse(sut.ClanFastOpen);
        Assert.IsFalse(sut.InventoryFastOpen);
        Assert.IsFalse(sut.PartyFastOpen);
        Assert.IsFalse(sut.QuestLogFastOpen);
    }
}
