using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features;
using TAOM.Features.RaceAbilities;

// The four MCM switches. TaomSettings.Instance is null in tests, so the public constructor pins the no-MCM
// fallbacks, which match the compiled MCM defaults; the internal constructor proves each getter reads its
// own setting through the cached object.

namespace TAOM.Tests.Features.RaceAbilities;

[TestClass]
public class RaceAbilitySettingsProviderTests
{
    [TestMethod]
    public void Getters_NoMcm_MatchTheCompiledDefaults()
    {
        var sut = new RaceAbilitySettingsProvider();
        var defaults = new TaomSettings();

        Assert.AreEqual(defaults.EnableRaceAbilities, sut.Enabled, nameof(sut.Enabled));
        Assert.AreEqual(defaults.RaceAbilityWarCries, sut.WarCries, nameof(sut.WarCries));
        Assert.AreEqual(defaults.RaceAbilityMessages, sut.Messages, nameof(sut.Messages));
        Assert.AreEqual(defaults.RaceAbilityDebugLog, sut.DebugLog, nameof(sut.DebugLog));
        Assert.IsTrue(sut.Enabled);
        Assert.IsFalse(sut.DebugLog);
    }

    [TestMethod]
    public void Getters_ReadEachSettingThroughTheCachedObject()
    {
        var mcm = new TaomSettings();
        var sut = new RaceAbilitySettingsProvider(mcm);

        mcm.EnableRaceAbilities = false;
        mcm.RaceAbilityWarCries = false;
        mcm.RaceAbilityMessages = false;
        mcm.RaceAbilityDebugLog = true;

        Assert.IsFalse(sut.Enabled, nameof(sut.Enabled));
        Assert.IsFalse(sut.WarCries, nameof(sut.WarCries));
        Assert.IsFalse(sut.Messages, nameof(sut.Messages));
        Assert.IsTrue(sut.DebugLog, nameof(sut.DebugLog));
    }
}
