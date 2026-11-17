using System;
using System.Linq;
using System.Reflection;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.ServerSettings;
using TAOM.Features;
using TAOM.Features.Provender;
using TAOM.Tests.Infrastructure;

namespace TAOM.Tests.Features.Provender;

/// <summary>
/// The MCM group is user-editable and MCM deserializes a hand-edited json verbatim (the slider range is a UI hint), so every
/// value is clamped here. The defaults and ranges live in the provider; the MCM properties are initialised from them, and the
/// hint texts quote the same numbers.
/// </summary>
[TestClass]
public class ProvenderSettingsProviderTests
{
    [TestMethod]
    public void From_NoSettingsYet_IsTheCompiledDefaults()
    {
        Assert.AreEqual(new ProvenderSettings(true, 3, 2, 50, 5, 100, 10, 3), ProvenderSettingsProvider.From(null));
    }

    [TestMethod]
    public void From_TheMcmDefaults_MatchTheNoSettingsFallback()
    {
        Assert.AreEqual(ProvenderSettingsProvider.From(null), ProvenderSettingsProvider.From(new TaomSettings()));
    }

    [TestMethod]
    public void From_ValuesInRange_PassThrough()
    {
        var s = ProvenderSettingsProvider.From(new TaomSettings
        {
            ProvenderEnabled = false,
            ProvenderLowFoodDays = 7,
            ProvenderKeepPerType = 5,
            ProvenderSmallPayGold = 80,
            ProvenderSmallPayMorale = 6,
            ProvenderLargePayGold = 300,
            ProvenderLargePayMorale = 20,
            ProvenderWageDaysKept = 9,
        });

        Assert.AreEqual(new ProvenderSettings(false, 7, 5, 80, 6, 300, 20, 9), s);
    }

    [DataTestMethod]
    [DataRow(int.MinValue, 1)]
    [DataRow(0, 1)]
    [DataRow(1, 1)]
    [DataRow(30, 30)]
    [DataRow(31, 30)]
    [DataRow(int.MaxValue, 30)]
    public void From_LowFoodDays_IsClampedToOneToThirty(int value, int expected)
    {
        Assert.AreEqual(expected, ProvenderSettingsProvider.From(new TaomSettings { ProvenderLowFoodDays = value }).LowFoodDays);
    }

    [DataTestMethod]
    [DataRow(-4, 0)]
    [DataRow(0, 0)]
    [DataRow(3, 3)]
    [DataRow(30, 30)]
    [DataRow(99, 30)]
    public void From_WageDaysKept_IsClampedToZeroToThirty(int value, int expected)
    {
        Assert.AreEqual(expected, ProvenderSettingsProvider.From(new TaomSettings { ProvenderWageDaysKept = value }).WageDaysKept);
    }

    [TestMethod]
    public void From_TheSaleCuts_AreClamped_AndTheSmallestIsNeverReadAboveTheLargest()
    {
        var s = ProvenderSettingsProvider.From(new TaomSettings { ProvenderSaleMaxFeePercent = 20, ProvenderSaleMinFeePercent = 60 });
        Assert.AreEqual(20, s.SaleMaxFeePercent);
        Assert.AreEqual(20, s.SaleMinFeePercent);

        var wild = ProvenderSettingsProvider.From(new TaomSettings { ProvenderSaleMaxFeePercent = 500, ProvenderSaleMinFeePercent = -5, ProvenderSaleTownCount = 99 });
        Assert.AreEqual(90, wild.SaleMaxFeePercent);
        Assert.AreEqual(0, wild.SaleMinFeePercent);
        Assert.AreEqual(5, wild.SaleTownCount);
        Assert.AreEqual(1, ProvenderSettingsProvider.From(new TaomSettings { ProvenderSaleTownCount = 0 }).SaleTownCount);
    }

    [TestMethod]
    public void From_NoSettings_KeepsTheDefaultWageDays()
    {
        Assert.AreEqual(ProvenderSettingsProvider.DefaultWageDaysKept, ProvenderSettingsProvider.From(null).WageDaysKept);
    }

    [DataTestMethod]
    [DataRow(int.MinValue, 0)]
    [DataRow(-1, 0)]
    [DataRow(0, 0)]
    [DataRow(20, 20)]
    [DataRow(21, 20)]
    [DataRow(int.MaxValue, 20)]
    public void From_KeepPerType_IsClampedToZeroToTwenty(int value, int expected)
    {
        Assert.AreEqual(expected, ProvenderSettingsProvider.From(new TaomSettings { ProvenderKeepPerType = value }).KeepPerType);
    }

    [DataTestMethod]
    [DataRow(int.MinValue, 10)]
    [DataRow(0, 10)]
    [DataRow(9, 10)]
    [DataRow(10, 10)]
    [DataRow(500, 500)]
    [DataRow(501, 500)]
    [DataRow(int.MaxValue, 500)]
    public void From_PayGold_IsClampedToTenToFiveHundred(int value, int expected)
    {
        Assert.AreEqual(expected, ProvenderSettingsProvider.From(new TaomSettings { ProvenderSmallPayGold = value }).SmallPayGold);
        Assert.AreEqual(expected, ProvenderSettingsProvider.From(new TaomSettings { ProvenderSmallPayGold = 10, ProvenderLargePayGold = value }).LargePayGold);
    }

    [DataTestMethod]
    [DataRow(int.MinValue, 1)]
    [DataRow(0, 1)]
    [DataRow(1, 1)]
    [DataRow(30, 30)]
    [DataRow(31, 30)]
    [DataRow(int.MaxValue, 30)]
    public void From_PayMorale_IsClampedToOneToThirty(int value, int expected)
    {
        Assert.AreEqual(expected, ProvenderSettingsProvider.From(new TaomSettings { ProvenderSmallPayMorale = value }).SmallPayMorale);
        Assert.AreEqual(expected, ProvenderSettingsProvider.From(new TaomSettings { ProvenderSmallPayMorale = 1, ProvenderLargePayMorale = value }).LargePayMorale);
    }

    [TestMethod]
    public void From_LargePayBelowSmallPay_IsRaisedToTheSmallPay_InGoldAndInMorale()
    {
        var s = ProvenderSettingsProvider.From(new TaomSettings
        {
            ProvenderSmallPayGold = 200, ProvenderLargePayGold = 60,
            ProvenderSmallPayMorale = 12, ProvenderLargePayMorale = 3,
        });

        Assert.AreEqual(200, s.LargePayGold);
        Assert.AreEqual(12, s.LargePayMorale);
        Assert.AreEqual(200, s.SmallPayGold, "the small tier is untouched");
        Assert.AreEqual(12, s.SmallPayMorale);
    }

    // MCM reads these attributes by name; MCMv5 is runtime-only here, so the test does too.
    private static object Attribute(PropertyInfo property, string typeName) =>
        property.GetCustomAttributes(inherit: false).Single(a => a.GetType().Name == typeName);

    private static object Read(object attribute, string name) => attribute.GetType().GetProperty(name)!.GetValue(attribute)!;

    private static object ValueAttribute(PropertyInfo property) =>
        property.GetCustomAttributes(inherit: false).Single(a => a.GetType().Name.StartsWith("SettingProperty", StringComparison.Ordinal)
            && a.GetType().Name != "SettingPropertyGroupAttribute");

    private static PropertyInfo[] Properties() =>
        typeof(TaomSettings).GetProperties().Where(p => p.Name.StartsWith("Provender", StringComparison.Ordinal)).ToArray();

    private static string Hint(string property) =>
        (string)Read(ValueAttribute(typeof(TaomSettings).GetProperty(property)!), "HintText");

    [TestMethod]
    public void TaomSettings_DeclaresTheGroupOnceAtAnUnusedOrder_WithEverySettingInIt()
    {
        var source = RepoPaths.ReadSource("Main/Features/TaomSettings.cs", stripComments: true);

        Assert.IsTrue(source.Contains("[SettingPropertyGroup(\"{=taom_mcm_group_provender_master}Provender-master\", GroupOrder = 70)]"));
        Assert.AreEqual(1, source.Split(new[] { "GroupOrder = 70" }, StringSplitOptions.None).Length - 1, "GroupOrder 70 is used by another group");

        var grouped = Properties();
        Assert.AreEqual(11, grouped.Length, "enable, low food days, livestock kept, the two pay tiers' gold and morale, the wage days kept and the three remote-sale values");
        foreach (var property in grouped)
        {
            Assert.AreEqual("Provender-master", McmText.Plain(Read(Attribute(property, "SettingPropertyGroupAttribute"), "GroupName") as string), property.Name);
            Assert.AreEqual(false, Read(ValueAttribute(property), "RequireRestart"), property.Name + ": every value is read when an order is judged");
        }
    }

    [DataTestMethod]
    [DataRow(nameof(TaomSettings.ProvenderLowFoodDays), ProvenderSettingsProvider.MinLowFoodDays, ProvenderSettingsProvider.MaxLowFoodDays)]
    [DataRow(nameof(TaomSettings.ProvenderKeepPerType), ProvenderSettingsProvider.MinKeepPerType, ProvenderSettingsProvider.MaxKeepPerType)]
    [DataRow(nameof(TaomSettings.ProvenderSmallPayGold), ProvenderSettingsProvider.MinPayGold, ProvenderSettingsProvider.MaxPayGold)]
    [DataRow(nameof(TaomSettings.ProvenderLargePayGold), ProvenderSettingsProvider.MinPayGold, ProvenderSettingsProvider.MaxPayGold)]
    [DataRow(nameof(TaomSettings.ProvenderSmallPayMorale), ProvenderSettingsProvider.MinPayMorale, ProvenderSettingsProvider.MaxPayMorale)]
    [DataRow(nameof(TaomSettings.ProvenderLargePayMorale), ProvenderSettingsProvider.MinPayMorale, ProvenderSettingsProvider.MaxPayMorale)]
    [DataRow(nameof(TaomSettings.ProvenderWageDaysKept), ProvenderSettingsProvider.MinWageDaysKept, ProvenderSettingsProvider.MaxWageDaysKept)]
    [DataRow(nameof(TaomSettings.ProvenderSaleMaxFeePercent), ProvenderSettingsProvider.MinSaleFeePercent, ProvenderSettingsProvider.MaxSaleFeePercent)]
    [DataRow(nameof(TaomSettings.ProvenderSaleMinFeePercent), ProvenderSettingsProvider.MinSaleFeePercent, ProvenderSettingsProvider.MaxSaleFeePercent)]
    [DataRow(nameof(TaomSettings.ProvenderSaleTownCount), ProvenderSettingsProvider.MinSaleTownCount, ProvenderSettingsProvider.MaxSaleTownCount)]
    public void TheSliderRange_IsTheProvidersClampRange(string property, int min, int max)
    {
        var attribute = ValueAttribute(typeof(TaomSettings).GetProperty(property)!);

        Assert.AreEqual(min, Convert.ToInt32(Read(attribute, "MinValue")), property);
        Assert.AreEqual(max, Convert.ToInt32(Read(attribute, "MaxValue")), property);
    }

    [TestMethod]
    public void EveryHint_QuotesTheCompiledDefault()
    {
        StringAssert.Contains(Hint(nameof(TaomSettings.ProvenderEnabled)), "Default: " + (ProvenderSettingsProvider.DefaultEnabled ? "on." : "off."));
        StringAssert.Contains(Hint(nameof(TaomSettings.ProvenderLowFoodDays)), $"Default: {ProvenderSettingsProvider.DefaultLowFoodDays}.");
        StringAssert.Contains(Hint(nameof(TaomSettings.ProvenderKeepPerType)), $"Default: {ProvenderSettingsProvider.DefaultKeepPerType}.");
        StringAssert.Contains(Hint(nameof(TaomSettings.ProvenderSmallPayGold)), $"Default: {ProvenderSettingsProvider.DefaultSmallPayGold}.");
        StringAssert.Contains(Hint(nameof(TaomSettings.ProvenderSmallPayMorale)), $"Default: {ProvenderSettingsProvider.DefaultSmallPayMorale}.");
        StringAssert.Contains(Hint(nameof(TaomSettings.ProvenderLargePayGold)), $"Default: {ProvenderSettingsProvider.DefaultLargePayGold}.");
        StringAssert.Contains(Hint(nameof(TaomSettings.ProvenderLargePayMorale)), $"Default: {ProvenderSettingsProvider.DefaultLargePayMorale}.");
        StringAssert.Contains(Hint(nameof(TaomSettings.ProvenderWageDaysKept)), $"Default: {ProvenderSettingsProvider.DefaultWageDaysKept}.");
        StringAssert.Contains(Hint(nameof(TaomSettings.ProvenderSaleMaxFeePercent)), $"Default: {ProvenderSettingsProvider.DefaultSaleMaxFeePercent}.");
        StringAssert.Contains(Hint(nameof(TaomSettings.ProvenderSaleMinFeePercent)), $"Default: {ProvenderSettingsProvider.DefaultSaleMinFeePercent}.");
        StringAssert.Contains(Hint(nameof(TaomSettings.ProvenderSaleTownCount)), $"Default: {ProvenderSettingsProvider.DefaultSaleTownCount}.");
    }

    [TestMethod]
    public void TheHintRanges_QuoteTheProvidersBounds()
    {
        StringAssert.Contains(Hint(nameof(TaomSettings.ProvenderKeepPerType)), $"Between {ProvenderSettingsProvider.MinKeepPerType} and {ProvenderSettingsProvider.MaxKeepPerType}.");
        StringAssert.Contains(Hint(nameof(TaomSettings.ProvenderLowFoodDays)), $"Between {ProvenderSettingsProvider.MinLowFoodDays} and {ProvenderSettingsProvider.MaxLowFoodDays}.");
        StringAssert.Contains(Hint(nameof(TaomSettings.ProvenderWageDaysKept)), $"Between {ProvenderSettingsProvider.MinWageDaysKept} and {ProvenderSettingsProvider.MaxWageDaysKept}.");
        foreach (var name in new[] { nameof(TaomSettings.ProvenderSaleMaxFeePercent), nameof(TaomSettings.ProvenderSaleMinFeePercent) })
            StringAssert.Contains(Hint(name), $"Between {ProvenderSettingsProvider.MinSaleFeePercent} and {ProvenderSettingsProvider.MaxSaleFeePercent}.");
        StringAssert.Contains(Hint(nameof(TaomSettings.ProvenderSaleTownCount)), $"Between {ProvenderSettingsProvider.MinSaleTownCount} and {ProvenderSettingsProvider.MaxSaleTownCount}.");
        foreach (var name in new[] { nameof(TaomSettings.ProvenderSmallPayGold), nameof(TaomSettings.ProvenderLargePayGold) })
            StringAssert.Contains(Hint(name), $"Between {ProvenderSettingsProvider.MinPayGold} and {ProvenderSettingsProvider.MaxPayGold}.");
        foreach (var name in new[] { nameof(TaomSettings.ProvenderSmallPayMorale), nameof(TaomSettings.ProvenderLargePayMorale) })
            StringAssert.Contains(Hint(name), $"Between {ProvenderSettingsProvider.MinPayMorale} and {ProvenderSettingsProvider.MaxPayMorale}.");
    }

    [TestMethod]
    public void TheLargePayHints_SayTheyAreNeverReadBelowTheSmallPay()
    {
        StringAssert.Contains(Hint(nameof(TaomSettings.ProvenderLargePayGold)), "Never read below the small pay");
        StringAssert.Contains(Hint(nameof(TaomSettings.ProvenderLargePayMorale)), "Never read below the small pay");
    }

    [TestMethod]
    public void TheShippedDefaults_AreThePlannedNumbers()
    {
        Assert.AreEqual(3, ProvenderSettingsProvider.DefaultLowFoodDays);
        Assert.AreEqual(2, ProvenderSettingsProvider.DefaultKeepPerType);
        Assert.AreEqual(50, ProvenderSettingsProvider.DefaultSmallPayGold);
        Assert.AreEqual(5, ProvenderSettingsProvider.DefaultSmallPayMorale);
        Assert.AreEqual(100, ProvenderSettingsProvider.DefaultLargePayGold);
        Assert.AreEqual(10, ProvenderSettingsProvider.DefaultLargePayMorale);
        Assert.AreEqual(3, ProvenderSettingsProvider.DefaultWageDaysKept);
        Assert.AreEqual(30, ProvenderSettingsProvider.DefaultSaleMaxFeePercent);
        Assert.AreEqual(10, ProvenderSettingsProvider.DefaultSaleMinFeePercent);
        Assert.AreEqual(3, ProvenderSettingsProvider.DefaultSaleTownCount);
        Assert.IsTrue(ProvenderSettingsProvider.DefaultEnabled);
    }
}
