using System.Linq;
using System.Reflection;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.ServerSettings;
using TAOM.Features;
using TAOM.Features.BanditVoices;
using TAOM.Features.CoopInterop;

namespace TAOM.Tests.Features.BanditVoices;

[TestClass]
public class BanditVoicesSettingsProviderTests
{
    private static object Attribute(PropertyInfo property, string typeName) =>
        property.GetCustomAttributes(inherit: false).Single(a => a.GetType().Name == typeName);

    private static object Read(object attribute, string name) => attribute.GetType().GetProperty(name)!.GetValue(attribute)!;

    [TestMethod]
    public void NullMcm_DefaultsOn()
    {
        Assert.IsTrue(BanditVoicesSettingsProvider.From(null));
        Assert.IsTrue(new TaomSettings().EnableBanditGreetings);
        Assert.IsFalse(BanditVoicesSettingsProvider.From(new TaomSettings { EnableBanditGreetings = false }));
    }

    [TestMethod]
    public void TheProvider_ReadsTheSettingsObjectItWasGiven_LiveEachTime()
    {
        var settings = new TaomSettings();
        var provider = new BanditVoicesSettingsProvider(settings);

        Assert.IsTrue(provider.Enabled);
        settings.EnableBanditGreetings = false;
        Assert.IsFalse(provider.Enabled, "MCM edits its one settings object in place, so the provider must not copy the value");
    }

    [TestMethod]
    public void TheSetting_SitsInTheBanditVoicesGroup_AtOrder78_AsABool_NotNeedingARestart()
    {
        var property = typeof(TaomSettings).GetProperty(nameof(TaomSettings.EnableBanditGreetings))!;
        var group = Attribute(property, "SettingPropertyGroupAttribute");
        var flag = Attribute(property, "SettingPropertyBoolAttribute");

        Assert.AreEqual("World/Bandit Voices", McmText.Plain(Read(group, "GroupName") as string));
        Assert.AreEqual(78, Read(group, "GroupOrder"));
        Assert.AreEqual("Enable Bandit Clan Greetings", McmText.Plain(Read(flag, "DisplayName") as string));
        Assert.AreEqual(false, Read(flag, "RequireRestart"));
    }

    [TestMethod]
    public void NoOtherGroupTakesOrder78()
    {
        var holders = typeof(TaomSettings).GetProperties()
            .Where(p => p.GetCustomAttributes(inherit: false).Any(a => a.GetType().Name == "SettingPropertyGroupAttribute" && (int)Read(a, "GroupOrder") == 78))
            .Select(p => p.Name)
            .ToArray();

        CollectionAssert.AreEqual(new[] { nameof(TaomSettings.EnableBanditGreetings) }, holders);
    }

    [TestMethod]
    public void TheSetting_IsLocalText_SoCoopDoesNotCompareIt()
        => Assert.IsTrue(CoopSettingsRelevance.IsExcluded(nameof(TaomSettings.EnableBanditGreetings)));
}
