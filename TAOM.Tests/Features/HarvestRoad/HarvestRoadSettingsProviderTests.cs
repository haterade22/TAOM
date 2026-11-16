using System.Linq;
using System.Reflection;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.ServerSettings;
using TAOM.Features;
using TAOM.Features.HarvestRoad;

namespace TAOM.Tests.Features.HarvestRoad;

[TestClass]
public class HarvestRoadSettingsProviderTests
{
    private static object Attribute(PropertyInfo property, string typeName) =>
        property.GetCustomAttributes(inherit: false).Single(a => a.GetType().Name == typeName);

    private static object Read(object attribute, string name) => attribute.GetType().GetProperty(name)!.GetValue(attribute)!;

    [TestMethod]
    public void NullMcm_DefaultsOn()
    {
        Assert.IsTrue(HarvestRoadSettingsProvider.From(null));
        Assert.IsTrue(new TaomSettings().EnableHarvestRoadNotices);
        Assert.IsFalse(HarvestRoadSettingsProvider.From(new TaomSettings { EnableHarvestRoadNotices = false }));
    }

    [TestMethod]
    public void TheProvider_ReadsTheSettingsObjectItWasGiven_LiveEachTime()
    {
        var settings = new TaomSettings();
        var provider = new HarvestRoadSettingsProvider(settings);

        Assert.IsTrue(provider.Enabled);
        settings.EnableHarvestRoadNotices = false;
        Assert.IsFalse(provider.Enabled, "MCM edits its one settings object in place, so the provider must not copy the value");
    }

    [TestMethod]
    public void TheSetting_SitsInTheNameplateGroup_AtOrderSeven_AsALocalizedBool_NotNeedingARestart()
    {
        var property = typeof(TaomSettings).GetProperty(nameof(TaomSettings.EnableHarvestRoadNotices))!;
        var group = Attribute(property, "SettingPropertyGroupAttribute");
        var bool_ = Attribute(property, "SettingPropertyBoolAttribute");

        Assert.AreEqual("Map UI/Settlement Nameplates", McmText.Plain(Read(group, "GroupName") as string));
        Assert.AreEqual(7, Read(bool_, "Order"));
        Assert.AreEqual(false, Read(bool_, "RequireRestart"));
        StringAssert.StartsWith((string)Read(bool_, "DisplayName"), "{=taom_hroad_mcm_enable}");
        StringAssert.StartsWith((string)Read(bool_, "HintText"), "{=taom_hroad_mcm_enable_hint}");
    }
}
