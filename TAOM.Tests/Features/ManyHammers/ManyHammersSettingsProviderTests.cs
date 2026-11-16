using System;
using System.Linq;
using System.Reflection;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.ServerSettings;
using TAOM.Features;
using TAOM.Features.ManyHammers;

namespace TAOM.Tests.Features.ManyHammers;

/// <summary>
/// Two switches, both default on. The compiled defaults live in the provider; the MCM properties are initialised from them,
/// so the fallback before MCM is up and the settings screen cannot drift apart.
/// </summary>
[TestClass]
public class ManyHammersSettingsProviderTests
{
    [TestMethod]
    public void Defaults_AreBothOn()
    {
        Assert.IsTrue(ManyHammersSettingsProvider.DefaultBulkEnabled);
        Assert.IsTrue(ManyHammersSettingsProvider.DefaultHideLockedEnabled);
    }

    [TestMethod]
    public void NoSettingsYet_FallsBackToTheCompiledDefaults()
    {
        Assert.AreEqual(ManyHammersSettingsProvider.DefaultBulkEnabled, ManyHammersSettingsProvider.BulkFrom(null));
        Assert.AreEqual(ManyHammersSettingsProvider.DefaultHideLockedEnabled, ManyHammersSettingsProvider.HideLockedFrom(null));
    }

    [TestMethod]
    public void TheMcmDefaults_MatchTheNoSettingsFallback()
    {
        var mcm = new TaomSettings();

        Assert.AreEqual(ManyHammersSettingsProvider.BulkFrom(null), ManyHammersSettingsProvider.BulkFrom(mcm));
        Assert.AreEqual(ManyHammersSettingsProvider.HideLockedFrom(null), ManyHammersSettingsProvider.HideLockedFrom(mcm));
    }

    [TestMethod]
    public void ValuesPassThrough()
    {
        var off = new TaomSettings { ManyHammersBulkEnabled = false, ManyHammersHideLockedEnabled = false };
        var on = new TaomSettings { ManyHammersBulkEnabled = true, ManyHammersHideLockedEnabled = true };

        Assert.IsFalse(ManyHammersSettingsProvider.BulkFrom(off));
        Assert.IsFalse(ManyHammersSettingsProvider.HideLockedFrom(off));
        Assert.IsTrue(ManyHammersSettingsProvider.BulkFrom(on));
        Assert.IsTrue(ManyHammersSettingsProvider.HideLockedFrom(on));
    }

    [TestMethod]
    public void TheInstanceProvider_ReadsThroughTheSettingsItWasGiven()
    {
        var provider = new ManyHammersSettingsProvider(new TaomSettings { ManyHammersBulkEnabled = false, ManyHammersHideLockedEnabled = true });

        Assert.IsFalse(provider.BulkEnabled);
        Assert.IsTrue(provider.HideLockedEnabled);
    }

    [TestMethod]
    public void TheInstanceProvider_SeesALaterChangeToTheSameSettings()
    {
        var settings = new TaomSettings();
        var provider = new ManyHammersSettingsProvider(settings);

        settings.ManyHammersBulkEnabled = false;

        Assert.IsFalse(provider.BulkEnabled);
    }

    private static readonly string[] NumberWords = { "zero", "one", "two", "three", "four", "five", "six", "seven", "eight", "nine", "ten" };

    private static object ValueAttribute(string property) =>
        typeof(TaomSettings).GetProperty(property)!.GetCustomAttributes(inherit: false)
            .Single(a => a.GetType().Name == "SettingPropertyBoolAttribute");

    private static object Read(object attribute, string name) => attribute.GetType().GetProperty(name)!.GetValue(attribute)!;

    [DataTestMethod]
    [DataRow(nameof(TaomSettings.ManyHammersBulkEnabled), "{=taom_mh_mcm_bulk}", "{=taom_mh_mcm_bulk_hint}")]
    [DataRow(nameof(TaomSettings.ManyHammersHideLockedEnabled), "{=taom_mh_mcm_hide_locked}", "{=taom_mh_mcm_hide_locked_hint}")]
    public void TheSettings_CarryTheirLocalizedKeys_AndNeedNoRestart(string property, string nameKey, string hintKey)
    {
        var attribute = ValueAttribute(property);

        StringAssert.StartsWith((string)Read(attribute, "DisplayName"), nameKey);
        StringAssert.StartsWith((string)Read(attribute, "HintText"), hintKey);
        Assert.AreEqual(false, Read(attribute, "RequireRestart"));
    }

    [TestMethod]
    public void TheBulkHint_StatesTheShiftCount_AndTheDefault()
    {
        var hint = (string)Read(ValueAttribute(nameof(TaomSettings.ManyHammersBulkEnabled)), "HintText");

        StringAssert.Contains(hint, "do " + NumberWords[BulkCraftRules.ShiftCount] + " at once");
        StringAssert.Contains(hint, ManyHammersSettingsProvider.DefaultBulkEnabled ? "Default: on." : "Default: off.");
    }

    [TestMethod]
    public void TheHideLockedHint_StatesTheDefault_AndWhenARowLeaves()
    {
        var hint = (string)Read(ValueAttribute(nameof(TaomSettings.ManyHammersHideLockedEnabled)), "HintText");

        StringAssert.Contains(hint, ManyHammersSettingsProvider.DefaultHideLockedEnabled ? "Default: on." : "Default: off.");
        // A row locked inside the smithy leaves at the next list rebuild, not at the click.
        StringAssert.Contains(hint, "next smelt or tab switch");
        Assert.IsFalse(hint.Contains("at once"), "a lock click does not remove the row at once");
    }

    [TestMethod]
    public void TheLabels_AreTitleCase_LikeTheOtherSmithingSettings()
    {
        Assert.AreEqual("{=taom_mh_mcm_bulk}Bulk Smelting and Refining", Read(ValueAttribute(nameof(TaomSettings.ManyHammersBulkEnabled)), "DisplayName"));
        Assert.AreEqual("{=taom_mh_mcm_hide_locked}Hide Locked Items from Smelting", Read(ValueAttribute(nameof(TaomSettings.ManyHammersHideLockedEnabled)), "DisplayName"));
    }
}
