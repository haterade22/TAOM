using System.Linq;
using System.Reflection;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.ServerSettings;
using TAOM.Features;
using TAOM.Features.KingsMuster;

namespace TAOM.Tests.Features.KingsMuster;

[TestClass]
public class KingsMusterSettingsProviderTests
{
    private const string Group = "War of the Ring/Armies";

    private static object Attribute(PropertyInfo property, string typeName) =>
        property.GetCustomAttributes(inherit: false).Single(a => a.GetType().Name == typeName);

    private static object Read(object attribute, string name) => attribute.GetType().GetProperty(name)!.GetValue(attribute)!;

    [TestMethod]
    public void NullMcm_DefaultsOn()
    {
        Assert.IsTrue(KingsMusterSettingsProvider.DiscountFrom(null));
        Assert.IsTrue(new TaomSettings().EnableKingsMusterDiscount);
    }

    [TestMethod]
    public void TheSwitch_ReadsItsOwnSetting()
    {
        Assert.IsFalse(KingsMusterSettingsProvider.DiscountFrom(new TaomSettings { EnableKingsMusterDiscount = false }));
        Assert.IsTrue(KingsMusterSettingsProvider.DiscountFrom(new TaomSettings()));
    }

    [TestMethod]
    public void TheProvider_ReadsTheSettingsObjectItWasGiven_LiveEachTime()
    {
        var settings = new TaomSettings();
        var provider = new KingsMusterSettingsProvider(settings);

        Assert.IsTrue(provider.DiscountEnabled);

        settings.EnableKingsMusterDiscount = false;
        Assert.IsFalse(provider.DiscountEnabled, "MCM edits its one settings object in place");
    }

    [TestMethod]
    public void TheFeature_HasOneSwitch_NoHouseholdSetting()
    {
        // The household rule was removed: the clan screen's "Allow joining other armies" box is the player's switch (kings-muster.md).
        var names = typeof(TaomSettings).GetProperties().Select(p => p.Name).Where(n => n.StartsWith("EnableKingsMuster")).ToList();

        CollectionAssert.AreEqual(new[] { "EnableKingsMusterDiscount" }, names);
    }

    [TestMethod]
    public void TheSwitch_SitsInTheArmiesGroup_AppliesWithoutARestart_AndExplainsItself()
    {
        var property = typeof(TaomSettings).GetProperty(nameof(TaomSettings.EnableKingsMusterDiscount))!;

        var group = Attribute(property, "SettingPropertyGroupAttribute");
        Assert.AreEqual(Group, McmText.Plain(Read(group, "GroupName") as string));
        Assert.AreEqual(0, Read(group, "GroupOrder"), "the order is carried by the group's first property only (EnableRedArrow)");
        var value = Attribute(property, "SettingPropertyBoolAttribute");
        Assert.AreEqual("Rulers call hosts for less influence", McmText.Plain(Read(value, "DisplayName") as string));
        Assert.AreEqual(false, Read(value, "RequireRestart"), "read on every call");
        var hint = (string)Read(value, "HintText");
        StringAssert.Contains(hint, "co-op");
        StringAssert.Contains(hint, "army_targeting.json", "the factor lives in the army targeting file");
    }

    [TestMethod]
    public void TheHint_NamesTheFactorsDefaultAndNoGender()
    {
        var property = typeof(TaomSettings).GetProperty(nameof(TaomSettings.EnableKingsMusterDiscount))!;
        var hint = (string)Read(Attribute(property, "SettingPropertyBoolAttribute"), "HintText");

        StringAssert.Contains(hint, new TAOM.Features.ArmyTargeting.ArmyTargetingConfig().RulerCallCostFactor.ToString(System.Globalization.CultureInfo.InvariantCulture));
        var gendered = System.Text.RegularExpressions.Regex.Match(hint, @"\b(he|his|him|she|her|hers)\b", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        Assert.IsFalse(gendered.Success, "no gendered words in the hint: say 'the ruler' (found '" + gendered.Value + "')");
    }
}
