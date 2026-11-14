using System.Linq;
using System.Reflection;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features;
using TAOM.Features.CoopInterop;
using TAOM.Features.EncounterLines;

namespace TAOM.Tests.Features.EncounterLines;

[TestClass]
public class EncounterLinesSettingsProviderTests
{
    private static object Attribute(PropertyInfo property, string typeName) =>
        property.GetCustomAttributes(inherit: false).Single(a => a.GetType().Name == typeName);

    private static object Read(object attribute, string name) => attribute.GetType().GetProperty(name)!.GetValue(attribute)!;

    [TestMethod]
    public void NullMcm_DefaultsOn()
    {
        Assert.IsTrue(EncounterLinesSettingsProvider.From(null));
        Assert.IsTrue(new TaomSettings().EnableEncounterLines);
        Assert.IsFalse(EncounterLinesSettingsProvider.From(new TaomSettings { EnableEncounterLines = false }));
    }

    [TestMethod]
    public void TheProvider_ReadsTheSettingsObjectItWasGiven_LiveEachTime()
    {
        var settings = new TaomSettings();
        var provider = new EncounterLinesSettingsProvider(settings);

        Assert.IsTrue(provider.Enabled);
        settings.EnableEncounterLines = false;
        Assert.IsFalse(provider.Enabled, "MCM edits its one settings object in place, so the provider must not copy the value");
    }

    [TestMethod]
    public void TheSetting_SitsInTheEncounterLinesGroup_AtOrder89_AsABool_NotNeedingARestart()
    {
        var property = typeof(TaomSettings).GetProperty(nameof(TaomSettings.EnableEncounterLines))!;
        var group = Attribute(property, "SettingPropertyGroupAttribute");
        var flag = Attribute(property, "SettingPropertyBoolAttribute");

        Assert.AreEqual("World/Encounter Lines", Read(group, "GroupName"));
        Assert.AreEqual(89, Read(group, "GroupOrder"));
        Assert.AreEqual("Enable Encounter Lines", Read(flag, "DisplayName"));
        Assert.AreEqual(false, Read(flag, "RequireRestart"));
    }

    [TestMethod]
    public void NoOtherGroupTakesOrder89()
    {
        var holders = typeof(TaomSettings).GetProperties()
            .Where(p => p.GetCustomAttributes(inherit: false).Any(a => a.GetType().Name == "SettingPropertyGroupAttribute" && (int)Read(a, "GroupOrder") == 89))
            .Select(p => p.Name)
            .ToArray();

        CollectionAssert.AreEqual(new[] { nameof(TaomSettings.EnableEncounterLines) }, holders);
    }

    [TestMethod]
    public void TheSetting_IsLocalText_SoCoopDoesNotCompareIt()
        => Assert.IsTrue(CoopSettingsRelevance.IsExcluded(nameof(TaomSettings.EnableEncounterLines)));

    [TestMethod]
    public void TheBanditGreetingHint_NoLongerSaysLootersAreUnchanged()
    {
        var property = typeof(TaomSettings).GetProperty(nameof(TaomSettings.EnableBanditGreetings))!;
        var hint = (string)Read(Attribute(property, "SettingPropertyBoolAttribute"), "HintText");

        StringAssert.Contains(hint, "Encounter Lines");
        Assert.IsFalse(hint.Contains("looters are unchanged"), hint);
    }
}
