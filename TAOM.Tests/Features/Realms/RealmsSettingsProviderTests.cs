using System;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features;
using TAOM.Features.CoopInterop;
using TAOM.Features.Realms;
using TAOM.Features.Realms.Domain.Config;
using TAOM.Features.Realms.Domain.Titles;
using TAOM.Tests.Infrastructure;

namespace TAOM.Tests.Features.Realms;

/// <summary>
/// The "World/Realms" MCM group (docs/features/realms.md). MCM deserializes a hand-edited json verbatim (the slider range is
/// a UI hint), so the provider clamps costs to 10..500 percent and payments to 0..300 percent; the config applies them to
/// the prices and amounts in realms_titles.xml.
/// </summary>
[TestClass]
public class RealmsSettingsProviderTests
{
    // --- the provider ------------------------------------------------------------------------------------------------------

    [TestMethod]
    public void NoSettingsYet_IsEverythingAtItsDefault()
    {
        var provider = new RealmsSettingsProvider(null);

        Assert.AreEqual(100, provider.ClaimPricePercent);
        Assert.AreEqual(100, provider.InfluenceCostPercent);
        Assert.AreEqual(100, provider.TributePercent);
        Assert.AreEqual(100, provider.OfficeInfluencePercent);
        Assert.IsTrue(provider.AiClaims && provider.Resentment && provider.LiegePull && provider.LiegeArmyDiscount
            && provider.FamilyGrants && provider.TitleStyles);
    }

    [TestMethod]
    public void TheMcmDefaults_MatchTheNoSettingsFallback()
    {
        var none = new RealmsSettingsProvider(null);
        var fresh = new RealmsSettingsProvider(new TaomSettings());

        foreach (var property in typeof(IRealmsSettingsProvider).GetProperties())
            Assert.AreEqual(property.GetValue(none), property.GetValue(fresh), property.Name);
    }

    [TestMethod]
    public void EachSetting_ReadsItsOwnProperty()
    {
        var provider = new RealmsSettingsProvider(new TaomSettings
        {
            RealmsClaimPricePercent = 50,
            RealmsInfluenceCostPercent = 200,
            RealmsTributePercent = 0,
            RealmsOfficeInfluencePercent = 150,
            RealmsAiClaims = false,
            RealmsResentment = false,
            RealmsLiegePull = false,
            RealmsLiegeArmyDiscount = false,
            RealmsFamilyGrants = false,
            RealmsTitleStyles = false,
        });

        Assert.AreEqual((50, 200, 0, 150), (provider.ClaimPricePercent, provider.InfluenceCostPercent, provider.TributePercent,
            provider.OfficeInfluencePercent));
        Assert.IsFalse(provider.AiClaims || provider.Resentment || provider.LiegePull || provider.LiegeArmyDiscount
            || provider.FamilyGrants || provider.TitleStyles);
    }

    [DataTestMethod]
    [DataRow(int.MinValue, 10)]
    [DataRow(0, 10)]
    [DataRow(9, 10)]
    [DataRow(10, 10)]
    [DataRow(500, 500)]
    [DataRow(501, 500)]
    [DataRow(int.MaxValue, 500)]
    public void ACost_IsClampedToTenToFiveHundred(int value, int expected)
    {
        Assert.AreEqual(expected, RealmsSettingsProvider.Cost(value));
        Assert.AreEqual(expected, new RealmsSettingsProvider(new TaomSettings { RealmsInfluenceCostPercent = value }).InfluenceCostPercent);
    }

    [DataTestMethod]
    [DataRow(int.MinValue, 0)]
    [DataRow(-1, 0)]
    [DataRow(0, 0)]
    [DataRow(300, 300)]
    [DataRow(301, 300)]
    [DataRow(int.MaxValue, 300)]
    public void APayment_IsClampedToZeroToThreeHundred(int value, int expected)
    {
        Assert.AreEqual(expected, RealmsSettingsProvider.Payment(value));
        Assert.AreEqual(expected, new RealmsSettingsProvider(new TaomSettings { RealmsTributePercent = value }).TributePercent);
    }

    [TestMethod]
    public void TheProvider_ReadsThroughTheSettings_SoAChangeAppliesAtOnce()
    {
        var settings = new TaomSettings();
        var provider = new RealmsSettingsProvider(settings);
        Assert.IsTrue(provider.TitleStyles);

        settings.RealmsTitleStyles = false;
        settings.RealmsClaimPricePercent = 250;

        Assert.IsFalse(provider.TitleStyles);
        Assert.AreEqual(250, provider.ClaimPricePercent);
    }

    // --- the percentage ----------------------------------------------------------------------------------------------------

    [DataTestMethod]
    [DataRow(2500, 100, 2500)]
    [DataRow(2500, 50, 1250)]
    [DataRow(2500, 10, 250)]
    [DataRow(2500, 500, 12500)]
    [DataRow(40, 0, 0)]
    [DataRow(1, 10, 1)]
    [DataRow(5, 10, 1)]
    [DataRow(15, 10, 2)]
    [DataRow(0, 200, 0)]
    public void APercentage_ScalesAndRounds_AndNeverDropsAPositiveAmountToZero(int value, int percent, int expected)
    {
        Assert.AreEqual(expected, RealmsPercent.Of(value, percent));
    }

    [TestMethod]
    public void TheClaimsConfig_ScalesItsPricesAndInfluence()
    {
        var claims = new ClaimsConfig();
        var price = claims.BuyPrice(TitleRank.Duchy)!.Value;
        var usurp = claims.UsurpCost(TitleRank.Kingdom)!.Value;

        claims.BuyPricePercent = 50;
        claims.InfluencePercent = 200;

        Assert.AreEqual(RealmsPercent.Of(price, 50), claims.BuyPrice(TitleRank.Duchy));
        Assert.AreEqual(usurp * 2, claims.UsurpCost(TitleRank.Kingdom));
        Assert.AreEqual(5000, claims.ScaleInfluence(2500));
    }

    [TestMethod]
    public void TheFeudalConfig_ScalesRevokeAndTribute()
    {
        var feudal = RealmsConfig.Defaults().Feudal;
        var revoke = feudal.RevokeCost(TitleRank.Duchy)!.Value;
        var tribute = feudal.TributeOf(TitleRank.Kingdom);
        Assert.IsTrue(tribute > 0);

        feudal.InfluencePercent = 300;
        feudal.TributePercent = 0;

        Assert.AreEqual(revoke * 3, feudal.RevokeCost(TitleRank.Duchy));
        Assert.AreEqual(0, feudal.TributeOf(TitleRank.Kingdom));
    }

    // --- the MCM group on TaomSettings -------------------------------------------------------------------------------------

    private static object Attribute(PropertyInfo property, string typeName) =>
        property.GetCustomAttributes(inherit: false).Single(a => a.GetType().Name == typeName);

    private static object Read(object attribute, string name) => attribute.GetType().GetProperty(name)!.GetValue(attribute)!;

    private static object ValueAttribute(PropertyInfo property) =>
        property.GetCustomAttributes(inherit: false).Single(a => a.GetType().Name.StartsWith("SettingProperty", StringComparison.Ordinal)
            && a.GetType().Name != "SettingPropertyGroupAttribute");

    private static PropertyInfo[] GroupProperties() =>
        typeof(TaomSettings).GetProperties().Where(p => p.Name.StartsWith("Realms", StringComparison.Ordinal)).ToArray();

    private static PropertyInfo Property(string name) => typeof(TaomSettings).GetProperty(name)!;

    [TestMethod]
    public void TaomSettings_DeclaresTheGroupOnceAtAnUnusedOrder_WithItsTenSettings()
    {
        var source = RepoPaths.ReadSource("Main/Features/TaomSettings.cs", stripComments: true);

        Assert.IsTrue(source.Contains("[SettingPropertyGroup(\"World/Realms\", GroupOrder = 87)]"));
        Assert.AreEqual(1, source.Split(new[] { "GroupOrder = 87" }, StringSplitOptions.None).Length - 1, "GroupOrder 87 is used by another group");

        var properties = GroupProperties();
        Assert.AreEqual(10, properties.Length);
        foreach (var property in properties)
        {
            Assert.AreEqual("World/Realms", Read(Attribute(property, "SettingPropertyGroupAttribute"), "GroupName"), property.Name);
            Assert.AreEqual(false, Read(ValueAttribute(property), "RequireRestart"), property.Name + " is read live");
        }
        CollectionAssert.AreEquivalent(Enumerable.Range(0, 10).ToArray(),
            properties.Select(p => Convert.ToInt32(Read(ValueAttribute(p), "Order"))).ToArray(), "each setting has its own order");
    }

    [DataTestMethod]
    [DataRow(nameof(TaomSettings.RealmsClaimPricePercent), RealmsSettingsProvider.MinCostPercent, RealmsSettingsProvider.MaxCostPercent)]
    [DataRow(nameof(TaomSettings.RealmsInfluenceCostPercent), RealmsSettingsProvider.MinCostPercent, RealmsSettingsProvider.MaxCostPercent)]
    [DataRow(nameof(TaomSettings.RealmsTributePercent), RealmsSettingsProvider.MinPaymentPercent, RealmsSettingsProvider.MaxPaymentPercent)]
    [DataRow(nameof(TaomSettings.RealmsOfficeInfluencePercent), RealmsSettingsProvider.MinPaymentPercent, RealmsSettingsProvider.MaxPaymentPercent)]
    public void TheSliderRangeAndHint_AreTheProvidersClampRangeAndDefault(string name, int min, int max)
    {
        var attribute = ValueAttribute(Property(name));
        var hint = (string)Read(attribute, "HintText");

        Assert.AreEqual(min, Convert.ToInt32(Read(attribute, "MinValue")));
        Assert.AreEqual(max, Convert.ToInt32(Read(attribute, "MaxValue")));
        StringAssert.Contains(hint, $"Between {min} and {max}.");
        StringAssert.Contains(hint, $"Default: {RealmsSettingsProvider.DefaultPercent}.");
    }

    [TestMethod]
    public void TheInfluenceHint_QuotesTheShippedRestoreCost()
    {
        var path = RepoPaths.RepoPath("Main", "_Module", "ModuleData", "realms_titles.xml");
        var coronation = RealmsConfigXml.Parse(System.Xml.Linq.XDocument.Load(path)).Config.Coronations.Single();
        var hint = (string)Read(ValueAttribute(Property(nameof(TaomSettings.RealmsInfluenceCostPercent))), "HintText");

        StringAssert.Contains(hint, $"costs {coronation.Influence} at 100");
    }

    [TestMethod]
    public void TheToggles_DefaultOn_AndSaySo()
    {
        foreach (var property in GroupProperties().Where(p => p.PropertyType == typeof(bool)))
        {
            Assert.AreEqual(true, property.GetValue(new TaomSettings()), property.Name);
            StringAssert.EndsWith((string)Read(ValueAttribute(property), "HintText"), "Default: on.", property.Name);
        }
    }

    // Title styles only rename parties and the conversation header, so co-op peers may differ; every other setting changes
    // prices, payments or AI behavior and is part of the co-op fingerprint.
    [TestMethod]
    public void OnlyTheTitleStyles_AreLeftOutOfTheCoopFingerprint()
    {
        foreach (var property in GroupProperties())
        {
            var styles = property.Name == nameof(TaomSettings.RealmsTitleStyles);
            Assert.AreEqual(styles, CoopSettingsRelevance.IsExcluded(property.Name), property.Name);
            Assert.AreEqual(!styles, CoopSettingsRelevance.IsSimulationRelevant(property), property.Name);
        }
    }

    [TestMethod]
    public void TheLabelsAndHints_AreGenderNeutral_AndCarryNoDash()
    {
        foreach (var property in GroupProperties())
        {
            var attribute = ValueAttribute(property);
            var text = (string)Read(attribute, "DisplayName") + " " + (string)Read(attribute, "HintText");

            Assert.IsFalse(Regex.IsMatch(text, @"\b(he|she|his|her|him|hers|himself|herself|man|woman|men|women|kinsmen)\b", RegexOptions.IgnoreCase), text);
            Assert.IsFalse(text.Contains('—') || text.Contains('–'), "a dash in: " + text);
        }
    }
}
