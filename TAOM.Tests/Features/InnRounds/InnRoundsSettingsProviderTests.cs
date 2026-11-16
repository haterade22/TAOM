using System.Linq;
using System.Reflection;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.ServerSettings;
using TAOM.Features;
using TAOM.Features.CoopInterop;
using TAOM.Features.InnRounds;

namespace TAOM.Tests.Features.InnRounds;

[TestClass]
public class InnRoundsSettingsProviderTests
{
    private static object Attribute(PropertyInfo property, string typeName) =>
        property.GetCustomAttributes(inherit: false).Single(a => a.GetType().Name == typeName);

    private static object Read(object attribute, string name) => attribute.GetType().GetProperty(name)!.GetValue(attribute)!;

    [TestMethod]
    public void NullMcm_DefaultsOn_WithScalesOfOne()
    {
        Assert.IsTrue(InnRoundsSettingsProvider.EnabledFrom(null));
        Assert.AreEqual(1f, InnRoundsSettingsProvider.MoraleScaleFrom(null));
        Assert.AreEqual(1f, InnRoundsSettingsProvider.RenownScaleFrom(null));
        Assert.IsTrue(new InnRoundsSettingsProvider().Enabled);
    }

    [TestMethod]
    public void TheDefaults_AreOn_AndScalesOfOne()
    {
        var settings = new TaomSettings();

        Assert.IsTrue(settings.EnableInnRounds);
        Assert.AreEqual(1f, settings.InnRoundsMoraleScale);
        Assert.AreEqual(1f, settings.InnRoundsRenownScale);
    }

    [TestMethod]
    public void TheProvider_ReadsTheSettingsObjectItWasGiven_LiveEachTime()
    {
        var settings = new TaomSettings();
        var provider = new InnRoundsSettingsProvider(settings);

        Assert.IsTrue(provider.Enabled);
        Assert.AreEqual(1f, provider.MoraleScale);
        settings.EnableInnRounds = false;
        settings.InnRoundsMoraleScale = 2.5f;
        settings.InnRoundsRenownScale = 0f;
        Assert.IsFalse(provider.Enabled, "MCM edits its one settings object in place, so the provider must not copy the value");
        Assert.AreEqual(2.5f, provider.MoraleScale);
        Assert.AreEqual(0f, provider.RenownScale);
    }

    [DataTestMethod]
    [DataRow(float.NaN)]
    [DataRow(float.PositiveInfinity)]
    [DataRow(float.NegativeInfinity)]
    [DataRow(-0.01f)]
    [DataRow(-5f)]
    [DataRow(3.01f)]
    [DataRow(40f)]
    public void AScaleThatIsNotFiniteOrOutsideZeroToThree_ReadsAsOne(float bad)
    {
        Assert.AreEqual(1f, InnRoundsSettingsProvider.MoraleScaleFrom(new TaomSettings { InnRoundsMoraleScale = bad }));
        Assert.AreEqual(1f, InnRoundsSettingsProvider.RenownScaleFrom(new TaomSettings { InnRoundsRenownScale = bad }));
    }

    [DataTestMethod]
    [DataRow(0f)]
    [DataRow(0.5f)]
    [DataRow(1f)]
    [DataRow(3f)]
    public void AScaleInsideZeroToThree_IsKept(float good)
    {
        Assert.AreEqual(good, InnRoundsSettingsProvider.MoraleScaleFrom(new TaomSettings { InnRoundsMoraleScale = good }));
        Assert.AreEqual(good, InnRoundsSettingsProvider.RenownScaleFrom(new TaomSettings { InnRoundsRenownScale = good }));
    }

    [TestMethod]
    public void TheSettings_SitInTheInnRoundsGroup_AtOrder79_NotNeedingARestart()
    {
        var enable = typeof(TaomSettings).GetProperty(nameof(TaomSettings.EnableInnRounds))!;
        var group = Attribute(enable, "SettingPropertyGroupAttribute");
        Assert.AreEqual("World/Inn Rounds", McmText.Plain(Read(group, "GroupName") as string));
        Assert.AreEqual(79, Read(group, "GroupOrder"));
        Assert.AreEqual(false, Read(Attribute(enable, "SettingPropertyBoolAttribute"), "RequireRestart"));

        foreach (var name in new[] { nameof(TaomSettings.InnRoundsMoraleScale), nameof(TaomSettings.InnRoundsRenownScale) })
        {
            var property = typeof(TaomSettings).GetProperty(name)!;
            Assert.AreEqual("World/Inn Rounds", McmText.Plain(Read(Attribute(property, "SettingPropertyGroupAttribute"), "GroupName") as string), name);
            var slider = Attribute(property, "SettingPropertyFloatingIntegerAttribute");
            Assert.AreEqual(0f, System.Convert.ToSingle(Read(slider, "MinValue")), name);
            Assert.AreEqual(3f, System.Convert.ToSingle(Read(slider, "MaxValue")), name);
            Assert.AreEqual(false, Read(slider, "RequireRestart"), name);
        }
    }

    private static string Hint(string property, string attribute) =>
        (string)Read(Attribute(typeof(TaomSettings).GetProperty(property)!, attribute), "HintText");

    // The hint texts are literals (an attribute argument must be a constant), so each test builds the text it expects from the
    // provider's constant: a default changed in one place and not the other fails here.
    [TestMethod]
    public void EveryHint_QuotesTheCompiledDefault()
    {
        StringAssert.EndsWith(Hint(nameof(TaomSettings.EnableInnRounds), "SettingPropertyBoolAttribute"),
            "Default: " + (InnRoundsSettingsProvider.DefaultEnabled ? "on." : "off."));
        foreach (var name in new[] { nameof(TaomSettings.InnRoundsMoraleScale), nameof(TaomSettings.InnRoundsRenownScale) })
            StringAssert.EndsWith(Hint(name, "SettingPropertyFloatingIntegerAttribute"),
                "Between 0 and " + InnRoundsSettingsProvider.MaxScale + ". Default: " + InnRoundsSettingsProvider.DefaultScale + ".");
    }

    [TestMethod]
    public void TheSettingDefaults_AreTheProvidersConstants()
    {
        var settings = new TaomSettings();

        Assert.AreEqual(InnRoundsSettingsProvider.DefaultEnabled, settings.EnableInnRounds);
        Assert.AreEqual(InnRoundsSettingsProvider.DefaultScale, settings.InnRoundsMoraleScale);
        Assert.AreEqual(InnRoundsSettingsProvider.DefaultScale, settings.InnRoundsRenownScale);
    }

    [TestMethod]
    public void TheHints_NameNoGender()
    {
        var gendered = new System.Text.RegularExpressions.Regex(
            @"\b(lads?|boys?|sir|sirs|madam|milady|milord|lords?|ladies|lady|kings?|queens?|masters?|mistress|brothers?|gentlem[ae]n|fellows?|sons?|daughters?|he|him|his|himself|she|her|hers|herself|woman|women|sisters?|girls?|lass|lasses|maids?|wenches?|(?!human)[\w-]*(?:man|men))\b",
            System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        var hints = new[]
        {
            Hint(nameof(TaomSettings.EnableInnRounds), "SettingPropertyBoolAttribute"),
            Hint(nameof(TaomSettings.InnRoundsMoraleScale), "SettingPropertyFloatingIntegerAttribute"),
            Hint(nameof(TaomSettings.InnRoundsRenownScale), "SettingPropertyFloatingIntegerAttribute"),
        };

        foreach (var hint in hints)
            Assert.IsFalse(gendered.IsMatch(hint), hint);
    }

    [TestMethod]
    public void NoOtherGroupTakesOrder79()
    {
        var holders = typeof(TaomSettings).GetProperties()
            .Where(p => p.GetCustomAttributes(inherit: false).Any(a => a.GetType().Name == "SettingPropertyGroupAttribute" && (int)Read(a, "GroupOrder") == 79))
            .Select(p => p.Name)
            .ToArray();

        CollectionAssert.AreEqual(new[] { nameof(TaomSettings.EnableInnRounds) }, holders);
    }

    [TestMethod]
    public void AllThreeSettings_AreSinglePlayerOnly_SoCoopDoesNotCompareThem()
    {
        Assert.IsTrue(CoopSettingsRelevance.IsExcluded(nameof(TaomSettings.EnableInnRounds)));
        Assert.IsTrue(CoopSettingsRelevance.IsExcluded(nameof(TaomSettings.InnRoundsMoraleScale)));
        Assert.IsTrue(CoopSettingsRelevance.IsExcluded(nameof(TaomSettings.InnRoundsRenownScale)));
    }
}
