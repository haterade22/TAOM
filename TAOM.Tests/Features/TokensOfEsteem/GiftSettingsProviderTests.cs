using System;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.ServerSettings;
using TAOM.Features;
using TAOM.Features.TokensOfEsteem;
using TAOM.Tests.Infrastructure;

namespace TAOM.Tests.Features.TokensOfEsteem;

/// <summary>
/// The MCM group is user-editable and MCM deserializes a hand-edited json verbatim (the slider range is a UI hint), so every
/// value is clamped here. The defaults and ranges live in the provider; the MCM properties are initialised from them, and the
/// hint texts quote the same numbers (a hint that says 500 to 5000 while the clamp says otherwise fails here).
/// </summary>
[TestClass]
public class GiftSettingsProviderTests
{
    [TestMethod]
    public void From_NoSettingsYet_IsTheCompiledDefaults()
    {
        Assert.AreEqual(new GiftSettings(true, 7, 15, true, 500, 2000, 5000), GiftSettingsProvider.From(null));
    }

    [TestMethod]
    public void From_TheMcmDefaults_MatchTheNoSettingsFallback()
    {
        Assert.AreEqual(GiftSettingsProvider.From(null), GiftSettingsProvider.From(new TaomSettings()));
    }

    [TestMethod]
    public void From_ValuesInRange_PassThrough()
    {
        var s = GiftSettingsProvider.From(new TaomSettings
        {
            GiftsEnabled = false,
            GiftCooldownDays = 21,
            GiftRelationCap = 20,
            GiftAlignmentRefusal = false,
            GiftPurseSmall = 600,
            GiftPurseMedium = 1500,
            GiftPurseLarge = 4500,
        });

        Assert.AreEqual(new GiftSettings(false, 21, 20, false, 600, 1500, 4500), s);
    }

    [DataTestMethod]
    [DataRow(int.MinValue, 1)]
    [DataRow(-5, 1)]
    [DataRow(0, 1)]
    [DataRow(1, 1)]
    [DataRow(60, 60)]
    [DataRow(61, 60)]
    [DataRow(int.MaxValue, 60)]
    public void From_CooldownDays_IsClampedToOneToSixty(int value, int expected)
    {
        Assert.AreEqual(expected, GiftSettingsProvider.From(new TaomSettings { GiftCooldownDays = value }).CooldownDays);
    }

    [DataTestMethod]
    [DataRow(int.MinValue, 1)]
    [DataRow(0, 1)]
    [DataRow(1, 1)]
    [DataRow(30, 30)]
    [DataRow(31, 30)]
    [DataRow(int.MaxValue, 30)]
    public void From_RelationCap_IsClampedToOneToThirty(int value, int expected)
    {
        Assert.AreEqual(expected, GiftSettingsProvider.From(new TaomSettings { GiftRelationCap = value }).RelationCap);
    }

    [TestMethod]
    public void ThePurseRange_IsDerivedFromTheBand_OneToTenPointsOfFiveHundredGold()
    {
        Assert.AreEqual(500, GiftSettingsProvider.MinPurse);
        Assert.AreEqual(5000, GiftSettingsProvider.MaxPurse);
        Assert.AreEqual(GiftService.PurseGoldPerPoint, GiftSettingsProvider.MinPurse);
        Assert.AreEqual(GiftService.PurseGoldPerPoint * GiftService.PurseMaxPoints, GiftSettingsProvider.MaxPurse);
    }

    [DataTestMethod]
    [DataRow(int.MinValue, 500)]
    [DataRow(-100, 500)]
    [DataRow(0, 500)]
    [DataRow(499, 500)]
    [DataRow(500, 500)]
    [DataRow(2500, 2500)]
    [DataRow(5000, 5000)]
    [DataRow(5001, 5000)]
    [DataRow(100000, 5000)]
    [DataRow(int.MaxValue, 5000)]
    public void From_EveryPurse_IsClampedToFiveHundredToFiveThousand(int value, int expected)
    {
        var small = GiftSettingsProvider.From(new TaomSettings { GiftPurseSmall = value });
        var medium = GiftSettingsProvider.From(new TaomSettings { GiftPurseMedium = value });
        var large = GiftSettingsProvider.From(new TaomSettings { GiftPurseLarge = value });

        Assert.AreEqual(expected, small.PurseSmall);
        Assert.AreEqual(expected, medium.PurseMedium);
        Assert.AreEqual(expected, large.PurseLarge);
    }

    // MCM reads these attributes by name; MCMv5 is runtime-only here, so the test does too (CreatureSiegeRoleSettingsProviderTests).
    private static object Attribute(PropertyInfo property, string typeName) =>
        property.GetCustomAttributes(inherit: false).Single(a => a.GetType().Name == typeName);

    private static object Read(object attribute, string name) => attribute.GetType().GetProperty(name)!.GetValue(attribute)!;

    private static object ValueAttribute(PropertyInfo property) =>
        property.GetCustomAttributes(inherit: false).Single(a => a.GetType().Name.StartsWith("SettingProperty", StringComparison.Ordinal)
            && a.GetType().Name != "SettingPropertyGroupAttribute");

    private static PropertyInfo[] GiftProperties() =>
        typeof(TaomSettings).GetProperties().Where(p => p.Name.StartsWith("Gift", StringComparison.Ordinal)).ToArray();

    [TestMethod]
    public void TaomSettings_DeclaresTheGroupOnceAtAnUnusedOrder_WithEverySettingInIt()
    {
        var source = RepoPaths.ReadSource("Main/Features/TaomSettings.cs", stripComments: true);

        Assert.IsTrue(source.Contains("[SettingPropertyGroup(\"{=taom_mcm_group_tokens_of_esteem}Tokens of Esteem\", GroupOrder = 62)]"));
        Assert.AreEqual(1, source.Split(new[] { "GroupOrder = 62" }, StringSplitOptions.None).Length - 1,
            "GroupOrder 62 is used by another group");

        var grouped = GiftProperties();
        Assert.AreEqual(7, grouped.Length, "enable, cooldown, cap, alignment refusal and three purses; scaling is not a setting");
        foreach (var property in grouped)
        {
            Assert.AreEqual("Tokens of Esteem", McmText.Plain(Read(Attribute(property, "SettingPropertyGroupAttribute"), "GroupName") as string), property.Name);
            Assert.AreEqual(false, Read(ValueAttribute(property), "RequireRestart"), property.Name + ": nothing is applied once at start-up");
        }
    }

    [TestMethod]
    public void ScalingIsNotASetting()
    {
        Assert.IsNull(typeof(TaomSettings).GetProperty("GiftRelationScaling"));
        Assert.IsFalse(typeof(GiftSettings).GetProperties().Any(p => p.Name.Contains("Scaling")));
    }

    [DataTestMethod]
    [DataRow(nameof(TaomSettings.GiftCooldownDays), GiftSettingsProvider.MinCooldownDays, GiftSettingsProvider.MaxCooldownDays)]
    [DataRow(nameof(TaomSettings.GiftRelationCap), GiftSettingsProvider.MinRelationCap, GiftSettingsProvider.MaxRelationCap)]
    [DataRow(nameof(TaomSettings.GiftPurseSmall), GiftSettingsProvider.MinPurse, GiftSettingsProvider.MaxPurse)]
    [DataRow(nameof(TaomSettings.GiftPurseMedium), GiftSettingsProvider.MinPurse, GiftSettingsProvider.MaxPurse)]
    [DataRow(nameof(TaomSettings.GiftPurseLarge), GiftSettingsProvider.MinPurse, GiftSettingsProvider.MaxPurse)]
    public void TheSliderRange_IsTheProvidersClampRange(string property, int min, int max)
    {
        var attribute = ValueAttribute(typeof(TaomSettings).GetProperty(property)!);

        Assert.AreEqual(min, Convert.ToInt32(Read(attribute, "MinValue")), property);
        Assert.AreEqual(max, Convert.ToInt32(Read(attribute, "MaxValue")), property);
    }

    // The hint texts are literals (an attribute argument must be a constant), so each test builds the number it expects from
    // the provider's constant: a default or a range changed in one place and not the other fails here.
    private static string Hint(string property) =>
        (string)Read(ValueAttribute(typeof(TaomSettings).GetProperty(property)!), "HintText");

    [TestMethod]
    public void EveryHint_QuotesTheCompiledDefault()
    {
        StringAssert.Contains(Hint(nameof(TaomSettings.GiftsEnabled)), "Default: " + (GiftSettingsProvider.DefaultEnabled ? "on." : "off."));
        StringAssert.Contains(Hint(nameof(TaomSettings.GiftAlignmentRefusal)), "Default: " + (GiftSettingsProvider.DefaultAlignmentRefusalEnabled ? "on." : "off."));
        StringAssert.Contains(Hint(nameof(TaomSettings.GiftCooldownDays)), $"Default: {GiftSettingsProvider.DefaultCooldownDays}.");
        StringAssert.Contains(Hint(nameof(TaomSettings.GiftRelationCap)), $"Default: {GiftSettingsProvider.DefaultRelationCap}.");
        StringAssert.Contains(Hint(nameof(TaomSettings.GiftPurseSmall)), $"Default: {GiftSettingsProvider.DefaultPurseSmall}.");
        StringAssert.Contains(Hint(nameof(TaomSettings.GiftPurseMedium)), $"Default: {GiftSettingsProvider.DefaultPurseMedium}.");
        StringAssert.Contains(Hint(nameof(TaomSettings.GiftPurseLarge)), $"Default: {GiftSettingsProvider.DefaultPurseLarge}.");
    }

    [TestMethod]
    public void TheHintDefaults_AreTheShippedNumbers()
    {
        Assert.AreEqual(7, GiftSettingsProvider.DefaultCooldownDays);
        Assert.AreEqual(15, GiftSettingsProvider.DefaultRelationCap);
        Assert.AreEqual(500, GiftSettingsProvider.DefaultPurseSmall);
        Assert.AreEqual(2000, GiftSettingsProvider.DefaultPurseMedium);
        Assert.AreEqual(5000, GiftSettingsProvider.DefaultPurseLarge);
        Assert.IsTrue(GiftSettingsProvider.DefaultEnabled && GiftSettingsProvider.DefaultAlignmentRefusalEnabled);
    }

    [DataTestMethod]
    [DataRow(nameof(TaomSettings.GiftPurseSmall))]
    [DataRow(nameof(TaomSettings.GiftPurseMedium))]
    [DataRow(nameof(TaomSettings.GiftPurseLarge))]
    public void ThePurseHints_QuoteTheBandAndTheRange(string property)
    {
        var hint = Hint(property);

        StringAssert.Contains(hint, $"1 relation per {GiftService.PurseGoldPerPoint} gold, at most {GiftService.PurseMaxPoints} ({GiftSettingsProvider.MaxPurse} gold)");
        StringAssert.Contains(hint, $"Between {GiftSettingsProvider.MinPurse} and {GiftSettingsProvider.MaxPurse} gold.");
    }

    [TestMethod]
    public void TheCapHint_SaysItIsWhatOneGiftAsksFor_AndThatCharmCanAddToWhatLands()
    {
        var hint = Hint(nameof(TaomSettings.GiftRelationCap));

        StringAssert.Contains(hint, "asks for");
        StringAssert.Contains(hint, "Charm");
    }

    [TestMethod]
    public void EveryHintAndLabel_IsGenderNeutral_AndNamesNoSetting_ThatDoesNotExist()
    {
        foreach (var property in GiftProperties())
        {
            var attribute = ValueAttribute(property);
            var text = McmText.Plain(Read(attribute, "DisplayName") as string) + " " + (string)Read(attribute, "HintText");
            Assert.IsFalse(Regex.IsMatch(text, @"\b(he|she|his|her|him|hers|himself|herself)\b", RegexOptions.IgnoreCase),
                property.Name + " names a gender: " + text);
            Assert.IsFalse(text.Contains("Gifts Matter Less"), property.Name + " still names the removed scaling setting");
        }
    }
}
