using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.Relations;
using TAOM.Features.ServerSettings;
using TAOM.Features;
using TAOM.Features.LongMemory;
using TAOM.Tests.Infrastructure;

namespace TAOM.Tests.Features.LongMemory;

/// <summary>
/// The MCM group is user-editable and MCM deserializes a hand-edited json verbatim (the slider range is a UI hint), so every
/// value is clamped here, a negative amount is 0 (the sliders hold magnitudes and the service negates them, so a sign flip
/// can never become a gain), and a non-finite float reverts to the compiled default.
/// </summary>
[TestClass]
public class LongMemorySettingsProviderTests
{
    private static readonly LongMemorySettings Defaults = new(
        true, true, true, 50, 50, 20, 25, new SideMultipliers(1f, 0.5f, 0.1f));

    [TestMethod]
    public void From_NoSettingsYet_IsTheCompiledDefaults()
    {
        Assert.AreEqual(Defaults, LongMemorySettingsProvider.From(null));
    }

    [TestMethod]
    public void From_TheMcmDefaults_MatchTheNoSettingsFallback()
    {
        Assert.AreEqual(LongMemorySettingsProvider.From(null), LongMemorySettingsProvider.From(new TaomSettings()));
    }

    [TestMethod]
    public void From_ValuesInRange_PassThrough()
    {
        var s = LongMemorySettingsProvider.From(new TaomSettings
        {
            LongMemoryEnabled = false,
            LongMemoryRaidEnabled = false,
            LongMemoryCoercionEnabled = false,
            LongMemoryRaidOwnerAmount = 80,
            LongMemoryRaidNotableAmount = 90,
            LongMemoryCoercionOwnerAmount = 10,
            LongMemoryCoercionNotableAmount = 15,
            LongMemorySame = 1.5f,
            LongMemoryNeutral = 0.25f,
            LongMemoryOpposite = 0f,
        });

        Assert.AreEqual(new LongMemorySettings(false, false, false, 80, 90, 10, 15, new SideMultipliers(1.5f, 0.25f, 0f)), s);
    }

    [DataTestMethod]
    [DataRow(-3, 0)]
    [DataRow(int.MinValue, 0)]
    [DataRow(0, 0)]
    [DataRow(100, 100)]
    [DataRow(101, 100)]
    [DataRow(int.MaxValue, 100)]
    public void From_EveryRaidAmount_IsClampedToZeroToOneHundred(int value, int expected)
    {
        var s = LongMemorySettingsProvider.From(new TaomSettings
        {
            LongMemoryRaidOwnerAmount = value,
            LongMemoryRaidNotableAmount = value,
        });

        Assert.AreEqual(expected, s.RaidOwnerAmount);
        Assert.AreEqual(expected, s.RaidNotableAmount);
    }

    [DataTestMethod]
    [DataRow(-3, 0)]
    [DataRow(int.MinValue, 0)]
    [DataRow(0, 0)]
    [DataRow(50, 50)]
    [DataRow(51, 50)]
    [DataRow(int.MaxValue, 50)]
    public void From_EveryCoercionAmount_IsClampedToZeroToFifty(int value, int expected)
    {
        var s = LongMemorySettingsProvider.From(new TaomSettings
        {
            LongMemoryCoercionOwnerAmount = value,
            LongMemoryCoercionNotableAmount = value,
        });

        Assert.AreEqual(expected, s.CoercionOwnerAmount);
        Assert.AreEqual(expected, s.CoercionNotableAmount);
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
        var s = LongMemorySettingsProvider.From(new TaomSettings
        {
            LongMemorySame = value,
            LongMemoryNeutral = value,
            LongMemoryOpposite = value,
        });

        Assert.AreEqual(new SideMultipliers(expected, expected, expected), s.Multipliers);
    }

    [DataTestMethod]
    [DataRow(float.NaN)]
    [DataRow(float.PositiveInfinity)]
    [DataRow(float.NegativeInfinity)]
    public void From_ANonFiniteMultiplier_RevertsToItsOwnDefault(float value)
    {
        var s = LongMemorySettingsProvider.From(new TaomSettings
        {
            LongMemorySame = value,
            LongMemoryNeutral = value,
            LongMemoryOpposite = value,
        });

        Assert.AreEqual(Defaults.Multipliers, s.Multipliers);
    }

    [DataTestMethod]
    [DataRow(0.69999999f, 0.7f)]
    [DataRow(0.6999999f, 0.7f)]
    [DataRow(0.7000001f, 0.7f)]
    [DataRow(0.0999999f, 0.1f)]
    [DataRow(1.236f, 1.24f)]
    [DataRow(0.004f, 0f)]
    [DataRow(5.004f, 5f)]
    public void From_AMultiplierFromTheSlider_IsRoundedToTwoDecimals(float slider, float expected)
    {
        var s = LongMemorySettingsProvider.From(new TaomSettings { LongMemorySame = slider, LongMemoryNeutral = slider, LongMemoryOpposite = slider });

        Assert.AreEqual(new SideMultipliers(expected, expected, expected), s.Multipliers);
    }

    [TestMethod]
    public void From_ASliderSevenTenths_GivesThirtyFiveFromFifty_NotThirtyFour()
    {
        var s = LongMemorySettingsProvider.From(new TaomSettings { LongMemorySame = 0.6999999f });

        Assert.AreEqual(-35, LongMemoryRules.Penalty(50, s.Multipliers.Same));
    }

    [TestMethod]
    public void Get_ReadsTheSettingsItWasGiven()
    {
        var provider = new LongMemorySettingsProvider(new TaomSettings { LongMemoryRaidOwnerAmount = 7 });

        Assert.AreEqual(7, provider.Get().RaidOwnerAmount);
    }

    [TestMethod]
    public void TheCompiledDefaults_AreTheShippedNumbers()
    {
        Assert.AreEqual((50, 50, 20, 25), (LongMemorySettingsProvider.DefaultRaidOwnerAmount, LongMemorySettingsProvider.DefaultRaidNotableAmount,
            LongMemorySettingsProvider.DefaultCoercionOwnerAmount, LongMemorySettingsProvider.DefaultCoercionNotableAmount));
        Assert.AreEqual((1f, 0.5f, 0.1f), (LongMemorySettingsProvider.DefaultSame, LongMemorySettingsProvider.DefaultNeutral, LongMemorySettingsProvider.DefaultOpposite));
        Assert.IsTrue(LongMemorySettingsProvider.DefaultEnabled && LongMemorySettingsProvider.DefaultRaidEnabled && LongMemorySettingsProvider.DefaultCoercionEnabled);
        Assert.AreEqual((0, 100, 50), (LongMemorySettingsProvider.MinAmount, LongMemorySettingsProvider.MaxRaidAmount, LongMemorySettingsProvider.MaxCoercionAmount));
    }

    // --- the MCM group on TaomSettings -------------------------------------------------------------------------------------

    // MCM reads these attributes by name; MCMv5 is runtime-only here, so the test does too (GoodDeedsSettingsProviderTests).
    private static object Attribute(PropertyInfo property, string typeName) =>
        property.GetCustomAttributes(inherit: false).Single(a => a.GetType().Name == typeName);

    private static object Read(object attribute, string name) => attribute.GetType().GetProperty(name)!.GetValue(attribute)!;

    private static object ValueAttribute(PropertyInfo property) =>
        property.GetCustomAttributes(inherit: false).Single(a => a.GetType().Name.StartsWith("SettingProperty", StringComparison.Ordinal)
            && a.GetType().Name != "SettingPropertyGroupAttribute");

    private static PropertyInfo[] LongMemoryProperties() =>
        typeof(TaomSettings).GetProperties().Where(p => p.Name.StartsWith("LongMemory", StringComparison.Ordinal)).ToArray();

    private static string Hint(string property) =>
        (string)Read(ValueAttribute(typeof(TaomSettings).GetProperty(property)!), "HintText");

    [TestMethod]
    public void TaomSettings_DeclaresTheGroupOnceAtAnUnusedOrder_WithEverySettingInIt()
    {
        var source = RepoPaths.ReadSource("Main/Features/TaomSettings.cs", stripComments: true);

        Assert.IsTrue(source.Contains("[SettingPropertyGroup(\"{=taom_mcm_group_long_memory}Long Memory\", GroupOrder = 67)]"));
        Assert.AreEqual(1, source.Split(new[] { "GroupOrder = 67" }, StringSplitOptions.None).Length - 1,
            "GroupOrder 67 is used by another group");

        var grouped = LongMemoryProperties();
        Assert.AreEqual(10, grouped.Length, "three switches, four amounts and three multipliers");
        foreach (var property in grouped)
        {
            Assert.AreEqual("Long Memory", McmText.Plain(Read(Attribute(property, "SettingPropertyGroupAttribute"), "GroupName") as string), property.Name);
            Assert.AreEqual(false, Read(ValueAttribute(property), "RequireRestart"), property.Name + ": every value is read when the wrong happens");
        }
    }

    [TestMethod]
    public void EveryOrderInTheGroup_IsUnique()
    {
        var orders = LongMemoryProperties().Select(p => Convert.ToInt32(Read(ValueAttribute(p), "Order"))).ToList();

        Assert.AreEqual(orders.Count, orders.Distinct().Count());
    }

    [DataTestMethod]
    [DataRow(nameof(TaomSettings.LongMemoryRaidOwnerAmount), 0, 100)]
    [DataRow(nameof(TaomSettings.LongMemoryRaidNotableAmount), 0, 100)]
    [DataRow(nameof(TaomSettings.LongMemoryCoercionOwnerAmount), 0, 50)]
    [DataRow(nameof(TaomSettings.LongMemoryCoercionNotableAmount), 0, 50)]
    public void TheAmountSliderRange_IsTheProvidersClampRange(string property, int min, int max)
    {
        var attribute = ValueAttribute(typeof(TaomSettings).GetProperty(property)!);

        Assert.AreEqual(min, Convert.ToInt32(Read(attribute, "MinValue")), property);
        Assert.AreEqual(max, Convert.ToInt32(Read(attribute, "MaxValue")), property);
        Assert.AreEqual(LongMemorySettingsProvider.MinAmount, min);
        Assert.AreEqual(max, property.Contains("Raid") ? LongMemorySettingsProvider.MaxRaidAmount : LongMemorySettingsProvider.MaxCoercionAmount);
    }

    [TestMethod]
    public void TheMultiplierSliderRange_IsTheProvidersClampRange()
    {
        var multipliers = LongMemoryProperties().Where(p => p.PropertyType == typeof(float)).ToList();

        Assert.AreEqual(3, multipliers.Count);
        foreach (var property in multipliers)
        {
            var attribute = ValueAttribute(property);
            Assert.AreEqual(LongMemorySettingsProvider.MinMultiplier, Convert.ToSingle(Read(attribute, "MinValue")), property.Name);
            Assert.AreEqual(LongMemorySettingsProvider.MaxMultiplier, Convert.ToSingle(Read(attribute, "MaxValue")), property.Name);
        }
    }

    private static string Default(float value) => "Default: " + value.ToString("0.00", CultureInfo.InvariantCulture) + ".";

    private static string OnOff(bool value) => "Default: " + (value ? "on." : "off.");

    // The hint texts are literals (an attribute argument must be a constant), so each test builds the number it expects from the
    // provider's constant: a default or a range changed in one place and not the other fails here.
    [TestMethod]
    public void EveryHint_QuotesTheCompiledDefault()
    {
        var expected = new Dictionary<string, string>
        {
            [nameof(TaomSettings.LongMemoryEnabled)] = OnOff(LongMemorySettingsProvider.DefaultEnabled),
            [nameof(TaomSettings.LongMemoryRaidEnabled)] = OnOff(LongMemorySettingsProvider.DefaultRaidEnabled),
            [nameof(TaomSettings.LongMemoryCoercionEnabled)] = OnOff(LongMemorySettingsProvider.DefaultCoercionEnabled),
            [nameof(TaomSettings.LongMemoryRaidOwnerAmount)] = $"Default: {LongMemorySettingsProvider.DefaultRaidOwnerAmount}.",
            [nameof(TaomSettings.LongMemoryRaidNotableAmount)] = $"Default: {LongMemorySettingsProvider.DefaultRaidNotableAmount}.",
            [nameof(TaomSettings.LongMemoryCoercionOwnerAmount)] = $"Default: {LongMemorySettingsProvider.DefaultCoercionOwnerAmount}.",
            [nameof(TaomSettings.LongMemoryCoercionNotableAmount)] = $"Default: {LongMemorySettingsProvider.DefaultCoercionNotableAmount}.",
            [nameof(TaomSettings.LongMemorySame)] = Default(LongMemorySettingsProvider.DefaultSame),
            [nameof(TaomSettings.LongMemoryNeutral)] = Default(LongMemorySettingsProvider.DefaultNeutral),
            [nameof(TaomSettings.LongMemoryOpposite)] = Default(LongMemorySettingsProvider.DefaultOpposite),
        };

        CollectionAssert.AreEquivalent(LongMemoryProperties().Select(p => p.Name).ToList(), expected.Keys.ToList(),
            "every setting needs a quoted default");
        foreach (var pair in expected)
            StringAssert.Contains(Hint(pair.Key), pair.Value, pair.Key);
    }

    [TestMethod]
    public void EveryAmountHint_QuotesItsOwnRange()
    {
        foreach (var raid in new[] { nameof(TaomSettings.LongMemoryRaidOwnerAmount), nameof(TaomSettings.LongMemoryRaidNotableAmount) })
            StringAssert.Contains(Hint(raid), $"Between {LongMemorySettingsProvider.MinAmount} and {LongMemorySettingsProvider.MaxRaidAmount}.", raid);
        foreach (var coercion in new[] { nameof(TaomSettings.LongMemoryCoercionOwnerAmount), nameof(TaomSettings.LongMemoryCoercionNotableAmount) })
            StringAssert.Contains(Hint(coercion), $"Between {LongMemorySettingsProvider.MinAmount} and {LongMemorySettingsProvider.MaxCoercionAmount}.", coercion);
    }

    [TestMethod]
    public void EveryLabelAndHint_IsGenderNeutral_AndCarriesNoDash()
    {
        foreach (var property in LongMemoryProperties())
        {
            var attribute = ValueAttribute(property);
            var text = McmText.Plain(Read(attribute, "DisplayName") as string) + " " + (string)Read(attribute, "HintText");
            Assert.IsFalse(Regex.IsMatch(text, @"\b(he|she|his|her|him|hers|himself|herself|man|woman|men|women|kinsmen)\b", RegexOptions.IgnoreCase),
                property.Name + " names a gender: " + text);
            Assert.IsFalse(text.Contains('\u2014') || text.Contains('\u2013'), property.Name + " carries a dash");
        }
    }

    [TestMethod]
    public void TheHints_SayTheLossStacksOnVanillasAndNeverAppliesToAVillageTheClanOwns()
    {
        var master = Hint(nameof(TaomSettings.LongMemoryEnabled));
        var raid = Hint(nameof(TaomSettings.LongMemoryRaidEnabled));

        StringAssert.Contains(master, "on top of vanilla");
        StringAssert.Contains(master, "clan owns");
        StringAssert.Contains(raid, "lead");
        StringAssert.Contains(Hint(nameof(TaomSettings.LongMemoryCoercionEnabled)), "resist");
    }
}
