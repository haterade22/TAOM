using System;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.ServerSettings;
using TAOM.Features;
using TAOM.Features.BondOfHire;
using TAOM.Tests.Infrastructure;

namespace TAOM.Tests.Features.BondOfHire;

/// <summary>
/// The MCM group is user-editable and MCM deserializes a hand-edited json verbatim (the slider range is a UI hint), so the
/// minimum is clamped to whole days from 0 (off) to 120 here. It is an integer, so no NaN reaches it.
/// </summary>
[TestClass]
public class BondOfHireSettingsProviderTests
{
    [TestMethod]
    public void From_NoSettingsYet_IsTheCompiledDefault()
    {
        Assert.AreEqual(30, BondOfHireSettingsProvider.From(null));
    }

    [TestMethod]
    public void From_TheMcmDefault_MatchesTheNoSettingsFallback()
    {
        Assert.AreEqual(BondOfHireSettingsProvider.From(null), BondOfHireSettingsProvider.From(new TaomSettings()));
    }

    [TestMethod]
    public void From_AValueInRange_PassesThrough()
    {
        Assert.AreEqual(45, BondOfHireSettingsProvider.From(new TaomSettings { BondOfHireMinimumDays = 45 }));
    }

    [DataTestMethod]
    [DataRow(-3, 0)]
    [DataRow(int.MinValue, 0)]
    [DataRow(0, 0)]
    [DataRow(120, 120)]
    [DataRow(121, 120)]
    [DataRow(int.MaxValue, 120)]
    public void From_TheMinimum_IsClampedToZeroToOneHundredTwenty(int value, int expected)
    {
        Assert.AreEqual(expected, BondOfHireSettingsProvider.From(new TaomSettings { BondOfHireMinimumDays = value }));
    }

    [TestMethod]
    public void MinimumDays_ReadsTheSettingsItWasGiven()
    {
        var provider = new BondOfHireSettingsProvider(new TaomSettings { BondOfHireMinimumDays = 7 });

        Assert.AreEqual(7, provider.MinimumDays);
    }

    [TestMethod]
    public void TheCompiledConstants_AreTheShippedNumbers()
    {
        Assert.AreEqual((30, 0, 120), (BondOfHireSettingsProvider.DefaultMinimumDays, BondOfHireSettingsProvider.MinDays, BondOfHireSettingsProvider.MaxDays));
    }

    // --- the MCM group on TaomSettings -------------------------------------------------------------------------------------

    // MCM reads these attributes by name; MCMv5 is runtime-only here, so the test does too (LongMemorySettingsProviderTests).
    private static object Attribute(PropertyInfo property, string typeName) =>
        property.GetCustomAttributes(inherit: false).Single(a => a.GetType().Name == typeName);

    private static object Read(object attribute, string name) => attribute.GetType().GetProperty(name)!.GetValue(attribute)!;

    private static object ValueAttribute(PropertyInfo property) =>
        property.GetCustomAttributes(inherit: false).Single(a => a.GetType().Name.StartsWith("SettingProperty", StringComparison.Ordinal)
            && a.GetType().Name != "SettingPropertyGroupAttribute");

    private static PropertyInfo[] GroupProperties() =>
        typeof(TaomSettings).GetProperties().Where(p => p.Name.StartsWith("BondOfHire", StringComparison.Ordinal)).ToArray();

    [TestMethod]
    public void TaomSettings_DeclaresTheGroupOnceAtAnUnusedOrder_WithItsOneSetting()
    {
        var source = RepoPaths.ReadSource("Main/Features/TaomSettings.cs", stripComments: true);

        Assert.IsTrue(source.Contains("[SettingPropertyGroup(\"{=taom_mcm_group_world}World/{=taom_mcm_group_bond_of_hire}Bond of Hire\", GroupOrder = 72)]"));
        Assert.AreEqual(1, source.Split(new[] { "GroupOrder = 72" }, StringSplitOptions.None).Length - 1, "GroupOrder 72 is used by another group");

        var property = GroupProperties().Single();
        Assert.AreEqual("World/Bond of Hire", McmText.Plain(Read(Attribute(property, "SettingPropertyGroupAttribute"), "GroupName") as string));
        Assert.AreEqual(false, Read(ValueAttribute(property), "RequireRestart"), "the minimum is read when a clan signs on");
        Assert.AreEqual(typeof(int), property.PropertyType);
    }

    [TestMethod]
    public void TheSliderRange_IsTheProvidersClampRange()
    {
        var attribute = ValueAttribute(GroupProperties().Single());

        Assert.AreEqual(BondOfHireSettingsProvider.MinDays, Convert.ToInt32(Read(attribute, "MinValue")));
        Assert.AreEqual(BondOfHireSettingsProvider.MaxDays, Convert.ToInt32(Read(attribute, "MaxValue")));
    }

    // The hint is a literal (an attribute argument must be a constant), so the test builds the numbers it expects from the
    // provider's constants: a default or a range changed in one place and not the other fails here.
    [TestMethod]
    public void TheHint_QuotesTheCompiledDefaultAndItsRange_AndSaysZeroIsOff()
    {
        var hint = (string)Read(ValueAttribute(GroupProperties().Single()), "HintText");

        StringAssert.Contains(hint, $"Default: {BondOfHireSettingsProvider.DefaultMinimumDays}.");
        StringAssert.Contains(hint, $"Between {BondOfHireSettingsProvider.MinDays} and {BondOfHireSettingsProvider.MaxDays}.");
        StringAssert.Contains(hint, "0 turns it off");
    }

    [TestMethod]
    public void TheHint_SaysWhoIsBoundAndWhoIsNot()
    {
        var hint = (string)Read(ValueAttribute(GroupProperties().Single()), "HintText");

        StringAssert.Contains(hint, "in debt");
        StringAssert.Contains(hint, "hired away");
        StringAssert.Contains(hint, "daily consideration");
        StringAssert.Contains(hint, "Your own clan is not bound");
    }

    // The minimum changes what AI clans do in the simulation (how long a hired clan stays), so the co-op fingerprint counts
    // it (the same pin as EnableNamegiving).
    [TestMethod]
    public void TheMinimum_IsCountedForCoop()
    {
        var property = typeof(TaomSettings).GetProperty(nameof(TaomSettings.BondOfHireMinimumDays))!;

        Assert.IsFalse(TAOM.Features.CoopInterop.CoopSettingsRelevance.IsExcluded(property.Name));
        Assert.IsTrue(TAOM.Features.CoopInterop.CoopSettingsRelevance.IsSimulationRelevant(property));
    }

    [TestMethod]
    public void TheLabelAndHint_AreGenderNeutral_AndCarryNoDash()
    {
        var attribute = ValueAttribute(GroupProperties().Single());
        var text = McmText.Plain(Read(attribute, "DisplayName") as string) + " " + (string)Read(attribute, "HintText");

        Assert.IsFalse(Regex.IsMatch(text, @"\b(he|she|his|her|him|hers|himself|herself|man|woman|men|women|kinsmen)\b", RegexOptions.IgnoreCase), text);
        Assert.IsFalse(text.Contains('\u2014') || text.Contains('\u2013'), "a dash in: " + text);
    }
}
