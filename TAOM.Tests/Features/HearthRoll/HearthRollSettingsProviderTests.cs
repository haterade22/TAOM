using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.ServerSettings;
using TAOM.Features;
using TAOM.Features.CoopInterop;
using TAOM.Features.HearthRoll;

namespace TAOM.Tests.Features.HearthRoll;

[TestClass]
public class HearthRollSettingsProviderTests
{
    private static PropertyInfo Tint => typeof(TaomSettings).GetProperty(nameof(TaomSettings.EnableUnhappyCompanionTint))!;

    private static object Attribute(PropertyInfo property, string typeName) =>
        property.GetCustomAttributes(inherit: false).Single(a => a.GetType().Name == typeName);

    private static object Read(object attribute, string name) => attribute.GetType().GetProperty(name)!.GetValue(attribute)!;

    [TestMethod]
    public void NullMcm_DefaultsOn()
    {
        Assert.IsTrue(HearthRollSettingsProvider.From(null));
        Assert.IsTrue(new HearthRollSettingsProvider().Enabled);
        Assert.IsTrue(new TaomSettings().EnableUnhappyCompanionTint);
    }

    [TestMethod]
    public void TheSwitch_ReadsItsOwnProperty()
    {
        var off = new TaomSettings { EnableUnhappyCompanionTint = false };

        Assert.IsFalse(HearthRollSettingsProvider.From(off));
        Assert.IsFalse(new HearthRollSettingsProvider(off).Enabled);
    }

    [TestMethod]
    public void TheProperty_OpensItsOwnHearthRollGroup_AtOrder69()
    {
        var group = Attribute(Tint, "SettingPropertyGroupAttribute");

        Assert.AreEqual("Hearth Roll", McmText.Plain(Read(group, "GroupName") as string));
        Assert.AreEqual(69, Read(group, "GroupOrder"), "Help Unlooked-For is 68");
        Assert.AreEqual(0, Read(Attribute(Tint, "SettingPropertyBoolAttribute"), "Order"));
    }

    [TestMethod]
    public void NoOtherGroupTakesOrder69()
    {
        var holders = typeof(TaomSettings).GetProperties()
            .Where(p => p.GetCustomAttributes(inherit: false).Any(a => a.GetType().Name == "SettingPropertyGroupAttribute" && (int)Read(a, "GroupOrder") == 69))
            .Select(p => p.Name)
            .ToArray();

        CollectionAssert.AreEqual(new[] { nameof(TaomSettings.EnableUnhappyCompanionTint) }, holders);
    }

    [TestMethod]
    public void TheSwitch_AppliesWithoutARestart()
        => Assert.AreEqual(false, Read(Attribute(Tint, "SettingPropertyBoolAttribute"), "RequireRestart"));

    [TestMethod]
    public void TheHint_SaysWhenItApplies_HasNoDigitsAndNoGenderedPronouns()
    {
        var hint = (string)Read(Attribute(Tint, "SettingPropertyBoolAttribute"), "HintText");

        Assert.IsTrue(hint.EndsWith("Applies the next time the clan screen opens. Default: on.", System.StringComparison.Ordinal), hint);
        Assert.IsFalse(Regex.IsMatch(hint, @"\d"), "a number in a hint goes stale");
        Assert.IsFalse(Regex.IsMatch(hint, @"\b(he|him|his|she|her|hers)\b", RegexOptions.IgnoreCase), "gendered wording: " + hint);
    }

    [TestMethod]
    public void TheSwitch_IsPresentation_SoCoopIgnoresIt()
    {
        Assert.IsTrue(CoopSettingsRelevance.IsExcluded(nameof(TaomSettings.EnableUnhappyCompanionTint)));
        Assert.IsFalse(CoopSettingsRelevance.IsSimulationRelevant(Tint));
    }
}
