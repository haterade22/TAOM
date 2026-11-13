using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features;
using TAOM.Features.HeroAuras;

// The two MCM settings. TaomSettings.Instance is null in tests, so the public constructor pins the no-MCM fallbacks;
// the internal constructor reads a TaomSettings the test controls.

namespace TAOM.Tests.Features.HeroAuras;

[TestClass]
public class HeroAuraSettingsProviderTests
{
    [TestMethod]
    public void Getters_NoMcm_MatchTheCompiledDefaults()
    {
        var sut = new HeroAuraSettingsProvider();
        var defaults = new TaomSettings();

        Assert.AreEqual(defaults.EnableHeroAuras, sut.Enabled);
        Assert.AreEqual(defaults.HeroAuraStrength, sut.Strength, 0.0001f);
        Assert.IsTrue(sut.Enabled);
    }

    [TestMethod]
    public void Enabled_SwitchOff_IsFalse()
    {
        var mcm = new TaomSettings { EnableHeroAuras = false, HeroAuraStrength = 1f };

        Assert.IsFalse(new HeroAuraSettingsProvider(mcm).Enabled);
    }

    [TestMethod]
    public void Enabled_StrengthZero_IsFalse()
    {
        // RaceAbilityService.Scale reads a factor of 0 or less as 1, so without this rule the MCM hint's
        // "0 turns every aura off" would run every aura at full strength.
        var mcm = new TaomSettings { EnableHeroAuras = true, HeroAuraStrength = 0f };

        Assert.IsFalse(new HeroAuraSettingsProvider(mcm).Enabled);
    }

    [TestMethod]
    public void Getters_ReadEachSettingThroughTheCachedObject()
    {
        var mcm = new TaomSettings();
        var sut = new HeroAuraSettingsProvider(mcm);

        mcm.HeroAuraStrength = 1.5f;

        Assert.IsTrue(sut.Enabled);
        Assert.AreEqual(1.5f, sut.Strength, 0.0001f);
    }
}
