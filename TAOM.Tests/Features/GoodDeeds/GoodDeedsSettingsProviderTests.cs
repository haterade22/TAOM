using System;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.Relations;
using TAOM.Features.ServerSettings;
using TAOM.Features;
using TAOM.Features.GoodDeeds;
using TAOM.Tests.Infrastructure;

namespace TAOM.Tests.Features.GoodDeeds;

/// <summary>
/// The MCM group is user-editable and MCM deserializes a hand-edited json verbatim (the slider range is a UI hint), so every
/// value is clamped here and a non-finite float reverts to the compiled default.
/// </summary>
[TestClass]
public class GoodDeedsSettingsProviderTests
{
    private static readonly GoodDeedsSettings Defaults = new(
        true, true, true, true, 5, 2, 2, 5, 1,
        new SideMultipliers(1f, 0.75f, 0.5f), new SideMultipliers(0f, 1f, 2f), new SideMultipliers(1f, 1f, 0f));

    [TestMethod]
    public void From_NoSettingsYet_IsTheCompiledDefaults()
    {
        Assert.AreEqual(Defaults, GoodDeedsSettingsProvider.From(null));
    }

    [TestMethod]
    public void From_TheMcmDefaults_MatchTheNoSettingsFallback()
    {
        Assert.AreEqual(GoodDeedsSettingsProvider.From(null), GoodDeedsSettingsProvider.From(new TaomSettings()));
    }

    [TestMethod]
    public void From_ValuesInRange_PassThrough()
    {
        var s = GoodDeedsSettingsProvider.From(new TaomSettings
        {
            GoodDeedsEnabled = false,
            GoodDeedsReleaseEnabled = false,
            GoodDeedsDonationEnabled = false,
            GoodDeedsTournamentEnabled = false,
            GoodDeedsReleaseLordAmount = 8,
            GoodDeedsReleaseSpouseAmount = 3,
            GoodDeedsReleaseLeaderAmount = 4,
            GoodDeedsDonationAmount = 9,
            GoodDeedsTournamentAmount = 2,
            GoodDeedsReleaseSame = 1.5f,
            GoodDeedsReleaseNeutral = 1.25f,
            GoodDeedsReleaseOpposite = 0.25f,
            GoodDeedsDonationSame = 0.5f,
            GoodDeedsDonationNeutral = 3f,
            GoodDeedsDonationOpposite = 4f,
            GoodDeedsTournamentSame = 2f,
            GoodDeedsTournamentNeutral = 0.5f,
            GoodDeedsTournamentOpposite = 0.1f,
        });

        Assert.AreEqual(new GoodDeedsSettings(false, false, false, false, 8, 3, 4, 9, 2,
            new SideMultipliers(1.5f, 1.25f, 0.25f), new SideMultipliers(0.5f, 3f, 4f), new SideMultipliers(2f, 0.5f, 0.1f)), s);
    }

    [DataTestMethod]
    [DataRow(-3, 0)]
    [DataRow(0, 0)]
    [DataRow(20, 20)]
    [DataRow(21, 20)]
    [DataRow(int.MinValue, 0)]
    [DataRow(int.MaxValue, 20)]
    public void From_EveryReleaseAndDonationAmount_IsClampedToZeroToTwenty(int value, int expected)
    {
        var s = GoodDeedsSettingsProvider.From(new TaomSettings
        {
            GoodDeedsReleaseLordAmount = value,
            GoodDeedsReleaseSpouseAmount = value,
            GoodDeedsReleaseLeaderAmount = value,
            GoodDeedsDonationAmount = value,
        });

        Assert.AreEqual(expected, s.ReleaseLordAmount);
        Assert.AreEqual(expected, s.ReleaseSpouseAmount);
        Assert.AreEqual(expected, s.ReleaseLeaderAmount);
        Assert.AreEqual(expected, s.DonationAmount);
    }

    [DataTestMethod]
    [DataRow(-1, 0)]
    [DataRow(10, 10)]
    [DataRow(11, 10)]
    [DataRow(int.MaxValue, 10)]
    public void From_TournamentAmount_IsClampedToZeroToTen(int value, int expected)
    {
        Assert.AreEqual(expected, GoodDeedsSettingsProvider.From(new TaomSettings { GoodDeedsTournamentAmount = value }).TournamentAmount);
    }

    [DataTestMethod]
    [DataRow(-0.5f, 0f)]
    [DataRow(0f, 0f)]
    [DataRow(5f, 5f)]
    [DataRow(5.01f, 5f)]
    [DataRow(1e9f, 5f)]
    [DataRow(-1e9f, 0f)]
    public void From_EveryMultiplier_IsClampedToZeroToFive(float value, float expected)
    {
        var s = GoodDeedsSettingsProvider.From(new TaomSettings
        {
            GoodDeedsReleaseSame = value,
            GoodDeedsReleaseNeutral = value,
            GoodDeedsReleaseOpposite = value,
            GoodDeedsDonationSame = value,
            GoodDeedsDonationNeutral = value,
            GoodDeedsDonationOpposite = value,
            GoodDeedsTournamentSame = value,
            GoodDeedsTournamentNeutral = value,
            GoodDeedsTournamentOpposite = value,
        });

        foreach (var set in new[] { s.Release, s.Donation, s.Tournament })
            Assert.AreEqual(new SideMultipliers(expected, expected, expected), set);
    }

    [DataTestMethod]
    [DataRow(float.NaN)]
    [DataRow(float.PositiveInfinity)]
    [DataRow(float.NegativeInfinity)]
    public void From_ANonFiniteMultiplier_RevertsToItsOwnDefault(float value)
    {
        var s = GoodDeedsSettingsProvider.From(new TaomSettings
        {
            GoodDeedsReleaseSame = value,
            GoodDeedsReleaseNeutral = value,
            GoodDeedsReleaseOpposite = value,
            GoodDeedsDonationSame = value,
            GoodDeedsDonationNeutral = value,
            GoodDeedsDonationOpposite = value,
            GoodDeedsTournamentSame = value,
            GoodDeedsTournamentNeutral = value,
            GoodDeedsTournamentOpposite = value,
        });

        Assert.AreEqual(Defaults.Release, s.Release);
        Assert.AreEqual(Defaults.Donation, s.Donation);
        Assert.AreEqual(Defaults.Tournament, s.Tournament);
    }

    [TestMethod]
    public void Get_ReadsTheSettingsItWasGiven()
    {
        var provider = new GoodDeedsSettingsProvider(new TaomSettings { GoodDeedsDonationAmount = 7 });

        Assert.AreEqual(7, provider.Get().DonationAmount);
    }

    // --- multipliers are read as hundredths -----------------------------------------------------------------------------

    private static TaomSettings WithEveryMultiplier(float value) => new()
    {
        GoodDeedsReleaseSame = value,
        GoodDeedsReleaseNeutral = value,
        GoodDeedsReleaseOpposite = value,
        GoodDeedsDonationSame = value,
        GoodDeedsDonationNeutral = value,
        GoodDeedsDonationOpposite = value,
        GoodDeedsTournamentSame = value,
        GoodDeedsTournamentNeutral = value,
        GoodDeedsTournamentOpposite = value,
    };

    [DataTestMethod]
    [DataRow(0.69999999f, 0.7f)]
    [DataRow(0.6999999f, 0.7f)]
    [DataRow(0.7000001f, 0.7f)]
    [DataRow(1.236f, 1.24f)]
    [DataRow(0.004f, 0f)]
    [DataRow(5.004f, 5f)]
    public void From_AMultiplierFromTheSlider_IsRoundedToTwoDecimals(float slider, float expected)
    {
        var s = GoodDeedsSettingsProvider.From(WithEveryMultiplier(slider));

        foreach (var set in new[] { s.Release, s.Donation, s.Tournament })
            Assert.AreEqual(new SideMultipliers(expected, expected, expected), set);
    }

    [DataTestMethod]
    [DataRow(0.69999999f)]
    [DataRow(0.6999999f)]
    public void From_ASliderSevenTenths_GivesFourFromFive_NotThree(float slider)
    {
        var s = GoodDeedsSettingsProvider.From(WithEveryMultiplier(slider));

        Assert.AreEqual(4, GoodDeedsRules.Scale(5, s.Release.Same));
        Assert.AreEqual(4, GoodDeedsRules.Scale(5, s.Tournament.Opposite));
        Assert.AreEqual(4f, GoodDeedsRules.ScaleDonation(0f, 5, s.Donation.Neutral));
    }

    // --- the MCM group on TaomSettings -------------------------------------------------------------------------------------

    // MCM reads these attributes by name; MCMv5 is runtime-only here, so the test does too (GiftSettingsProviderTests).
    private static object Attribute(PropertyInfo property, string typeName) =>
        property.GetCustomAttributes(inherit: false).Single(a => a.GetType().Name == typeName);

    private static object Read(object attribute, string name) => attribute.GetType().GetProperty(name)!.GetValue(attribute)!;

    private static object ValueAttribute(PropertyInfo property) =>
        property.GetCustomAttributes(inherit: false).Single(a => a.GetType().Name.StartsWith("SettingProperty", StringComparison.Ordinal)
            && a.GetType().Name != "SettingPropertyGroupAttribute");

    private static PropertyInfo[] GoodDeedsProperties() =>
        typeof(TaomSettings).GetProperties().Where(p => p.Name.StartsWith("GoodDeeds", StringComparison.Ordinal)).ToArray();

    private static string Hint(string property) =>
        (string)Read(ValueAttribute(typeof(TaomSettings).GetProperty(property)!), "HintText");

    [TestMethod]
    public void TaomSettings_DeclaresTheGroupOnceAtAnUnusedOrder_WithEverySettingInIt()
    {
        var source = RepoPaths.ReadSource("Main/Features/TaomSettings.cs", stripComments: true);

        Assert.IsTrue(source.Contains("[SettingPropertyGroup(\"{=taom_mcm_group_good_deeds}Good Deeds\", GroupOrder = 65)]"));
        Assert.AreEqual(1, source.Split(new[] { "GroupOrder = 65" }, StringSplitOptions.None).Length - 1,
            "GroupOrder 65 is used by another group");

        var grouped = GoodDeedsProperties();
        Assert.AreEqual(18, grouped.Length, "four switches, five amounts and nine multipliers");
        foreach (var property in grouped)
        {
            Assert.AreEqual("Good Deeds", McmText.Plain(Read(Attribute(property, "SettingPropertyGroupAttribute"), "GroupName") as string), property.Name);
            Assert.AreEqual(false, Read(ValueAttribute(property), "RequireRestart"), property.Name + ": every value is read when the deed happens");
        }
    }

    [DataTestMethod]
    [DataRow(nameof(TaomSettings.GoodDeedsReleaseLordAmount), GoodDeedsSettingsProvider.MinAmount, GoodDeedsSettingsProvider.MaxAmount)]
    [DataRow(nameof(TaomSettings.GoodDeedsReleaseSpouseAmount), GoodDeedsSettingsProvider.MinAmount, GoodDeedsSettingsProvider.MaxAmount)]
    [DataRow(nameof(TaomSettings.GoodDeedsReleaseLeaderAmount), GoodDeedsSettingsProvider.MinAmount, GoodDeedsSettingsProvider.MaxAmount)]
    [DataRow(nameof(TaomSettings.GoodDeedsDonationAmount), GoodDeedsSettingsProvider.MinAmount, GoodDeedsSettingsProvider.MaxAmount)]
    [DataRow(nameof(TaomSettings.GoodDeedsTournamentAmount), GoodDeedsSettingsProvider.MinAmount, GoodDeedsSettingsProvider.MaxTournamentAmount)]
    public void TheAmountSliderRange_IsTheProvidersClampRange(string property, int min, int max)
    {
        var attribute = ValueAttribute(typeof(TaomSettings).GetProperty(property)!);

        Assert.AreEqual(min, Convert.ToInt32(Read(attribute, "MinValue")), property);
        Assert.AreEqual(max, Convert.ToInt32(Read(attribute, "MaxValue")), property);
    }

    [TestMethod]
    public void TheMultiplierSliderRange_IsTheProvidersClampRange()
    {
        var multipliers = GoodDeedsProperties().Where(p => p.PropertyType == typeof(float)).ToList();

        Assert.AreEqual(9, multipliers.Count);
        foreach (var property in multipliers)
        {
            var attribute = ValueAttribute(property);
            Assert.AreEqual(GoodDeedsSettingsProvider.MinMultiplier, Convert.ToSingle(Read(attribute, "MinValue")), property.Name);
            Assert.AreEqual(GoodDeedsSettingsProvider.MaxMultiplier, Convert.ToSingle(Read(attribute, "MaxValue")), property.Name);
        }
    }

    // The hint texts are literals (an attribute argument must be a constant), so each test builds the number it expects from the
    // provider's constant: a default or a range changed in one place and not the other fails here.
    [TestMethod]
    public void EveryHint_QuotesTheCompiledDefault()
    {
        var expected = new System.Collections.Generic.Dictionary<string, string>
        {
            [nameof(TaomSettings.GoodDeedsEnabled)] = "Default: " + (GoodDeedsSettingsProvider.DefaultEnabled ? "on." : "off."),
            [nameof(TaomSettings.GoodDeedsReleaseEnabled)] = "Default: " + (GoodDeedsSettingsProvider.DefaultReleaseEnabled ? "on." : "off."),
            [nameof(TaomSettings.GoodDeedsDonationEnabled)] = "Default: " + (GoodDeedsSettingsProvider.DefaultDonationEnabled ? "on." : "off."),
            [nameof(TaomSettings.GoodDeedsTournamentEnabled)] = "Default: " + (GoodDeedsSettingsProvider.DefaultTournamentEnabled ? "on." : "off."),
            [nameof(TaomSettings.GoodDeedsReleaseLordAmount)] = $"Default: {GoodDeedsSettingsProvider.DefaultReleaseLordAmount}.",
            [nameof(TaomSettings.GoodDeedsReleaseSpouseAmount)] = $"Default: {GoodDeedsSettingsProvider.DefaultReleaseSpouseAmount}.",
            [nameof(TaomSettings.GoodDeedsReleaseLeaderAmount)] = $"Default: {GoodDeedsSettingsProvider.DefaultReleaseLeaderAmount}.",
            [nameof(TaomSettings.GoodDeedsDonationAmount)] = $"Default: {GoodDeedsSettingsProvider.DefaultDonationAmount}.",
            [nameof(TaomSettings.GoodDeedsTournamentAmount)] = $"Default: {GoodDeedsSettingsProvider.DefaultTournamentAmount}.",
            [nameof(TaomSettings.GoodDeedsReleaseSame)] = Default(GoodDeedsSettingsProvider.DefaultReleaseSame),
            [nameof(TaomSettings.GoodDeedsReleaseNeutral)] = Default(GoodDeedsSettingsProvider.DefaultReleaseNeutral),
            [nameof(TaomSettings.GoodDeedsReleaseOpposite)] = Default(GoodDeedsSettingsProvider.DefaultReleaseOpposite),
            [nameof(TaomSettings.GoodDeedsDonationSame)] = Default(GoodDeedsSettingsProvider.DefaultDonationSame),
            [nameof(TaomSettings.GoodDeedsDonationNeutral)] = Default(GoodDeedsSettingsProvider.DefaultDonationNeutral),
            [nameof(TaomSettings.GoodDeedsDonationOpposite)] = Default(GoodDeedsSettingsProvider.DefaultDonationOpposite),
            [nameof(TaomSettings.GoodDeedsTournamentSame)] = Default(GoodDeedsSettingsProvider.DefaultTournamentSame),
            [nameof(TaomSettings.GoodDeedsTournamentNeutral)] = Default(GoodDeedsSettingsProvider.DefaultTournamentNeutral),
            [nameof(TaomSettings.GoodDeedsTournamentOpposite)] = Default(GoodDeedsSettingsProvider.DefaultTournamentOpposite),
        };

        CollectionAssert.AreEquivalent(GoodDeedsProperties().Select(p => p.Name).ToList(), expected.Keys.ToList(),
            "every setting needs a quoted default");
        foreach (var pair in expected)
            StringAssert.Contains(Hint(pair.Key), pair.Value, pair.Key);
    }

    private static string Default(float value) => "Default: " + value.ToString("0.00", CultureInfo.InvariantCulture) + ".";

    [TestMethod]
    public void TheTournamentHint_QuotesItsOwnRange_NotTheOtherAmountsRange()
    {
        StringAssert.Contains(Hint(nameof(TaomSettings.GoodDeedsTournamentAmount)),
            $"Between {GoodDeedsSettingsProvider.MinAmount} and {GoodDeedsSettingsProvider.MaxTournamentAmount}.");
        Assert.AreEqual(10, GoodDeedsSettingsProvider.MaxTournamentAmount);
        Assert.AreEqual(20, GoodDeedsSettingsProvider.MaxAmount);
    }

    [TestMethod]
    public void TheHintDefaults_AreTheShippedNumbers()
    {
        Assert.AreEqual((5, 2, 2, 5, 1), (GoodDeedsSettingsProvider.DefaultReleaseLordAmount, GoodDeedsSettingsProvider.DefaultReleaseSpouseAmount,
            GoodDeedsSettingsProvider.DefaultReleaseLeaderAmount, GoodDeedsSettingsProvider.DefaultDonationAmount, GoodDeedsSettingsProvider.DefaultTournamentAmount));
        Assert.AreEqual((1f, 0.75f, 0.5f), (GoodDeedsSettingsProvider.DefaultReleaseSame, GoodDeedsSettingsProvider.DefaultReleaseNeutral, GoodDeedsSettingsProvider.DefaultReleaseOpposite));
        Assert.AreEqual((0f, 1f, 2f), (GoodDeedsSettingsProvider.DefaultDonationSame, GoodDeedsSettingsProvider.DefaultDonationNeutral, GoodDeedsSettingsProvider.DefaultDonationOpposite));
        Assert.AreEqual((1f, 1f, 0f), (GoodDeedsSettingsProvider.DefaultTournamentSame, GoodDeedsSettingsProvider.DefaultTournamentNeutral, GoodDeedsSettingsProvider.DefaultTournamentOpposite));
        Assert.IsTrue(GoodDeedsSettingsProvider.DefaultEnabled && GoodDeedsSettingsProvider.DefaultReleaseEnabled
            && GoodDeedsSettingsProvider.DefaultDonationEnabled && GoodDeedsSettingsProvider.DefaultTournamentEnabled);
    }

    [TestMethod]
    public void EveryLabelAndHint_IsGenderNeutral()
    {
        foreach (var property in GoodDeedsProperties())
        {
            var attribute = ValueAttribute(property);
            var text = McmText.Plain(Read(attribute, "DisplayName") as string) + " " + (string)Read(attribute, "HintText");
            Assert.IsFalse(Regex.IsMatch(text, @"\b(he|she|his|her|him|hers|himself|herself|man|woman|men|women|kinsmen)\b", RegexOptions.IgnoreCase),
                property.Name + " names a gender: " + text);
        }
    }

    [TestMethod]
    public void TheReleaseLabelsAndHints_NameTheLordsSpouseAndClanLeaderNeutrally()
    {
        var spouse = ValueAttribute(typeof(TaomSettings).GetProperty(nameof(TaomSettings.GoodDeedsReleaseSpouseAmount))!);
        var leader = ValueAttribute(typeof(TaomSettings).GetProperty(nameof(TaomSettings.GoodDeedsReleaseLeaderAmount))!);

        StringAssert.Contains(McmText.Plain(Read(spouse, "DisplayName") as string), "The Lord's Spouse");
        StringAssert.Contains(McmText.Plain(Read(leader, "DisplayName") as string), "The Lord's Clan Leader");
        StringAssert.Contains((string)Read(leader, "HintText"), "share one relation");
    }

    [TestMethod]
    public void TheReleaseHint_NamesTheThreeSources_AndNotThePostBattleChoice()
    {
        var hint = Hint(nameof(TaomSettings.GoodDeedsReleaseEnabled));

        StringAssert.Contains(hint, "your party");
        StringAssert.Contains(hint, "conversation");
        StringAssert.Contains(hint, "party screen");
        StringAssert.Contains(hint, "dungeon");
        StringAssert.Contains(hint, "free to go");
        Assert.IsTrue(hint.IndexOf("earn nothing", StringComparison.Ordinal) > hint.IndexOf("free to go", StringComparison.Ordinal),
            "the post-battle choice is listed among the releases that earn nothing");
    }
}
