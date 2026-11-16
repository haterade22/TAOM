using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.ServerSettings;
using TAOM.Features;
using TAOM.Features.LoremastersLine;

namespace TAOM.Tests.Features.LoremastersLine;

[TestClass]
public class LoremastersLineSettingsProviderTests
{
    [TestMethod]
    public void DefaultIsOn()
    {
        Assert.IsTrue(new TaomSettings().EnableLoremastersLine);
        Assert.IsTrue(LoremastersLineSettingsProvider.EnabledFrom(new TaomSettings()));
        Assert.IsTrue(new LoremastersLineSettingsProvider(new TaomSettings()).Enabled);
    }

    [TestMethod]
    public void WithoutMcm_IsOn()
    {
        Assert.IsTrue(LoremastersLineSettingsProvider.EnabledFrom(null));
        Assert.IsTrue(new LoremastersLineSettingsProvider().Enabled);
    }

    [TestMethod]
    public void Off_IsOff()
    {
        var settings = new TaomSettings { EnableLoremastersLine = false };

        Assert.IsFalse(LoremastersLineSettingsProvider.EnabledFrom(settings));
        Assert.IsFalse(new LoremastersLineSettingsProvider(settings).Enabled);
    }

    [TestMethod]
    public void TheSetting_IsDeclaredWithItsLocalizedNameAndHint_AndNeedsNoRestart()
    {
        var property = typeof(TaomSettings).GetProperty(nameof(TaomSettings.EnableLoremastersLine))!;
        var attribute = property.GetCustomAttributes(inherit: false).Single(a => a.GetType().Name == "SettingPropertyBoolAttribute");
        var type = attribute.GetType();

        StringAssert.StartsWith((string)type.GetProperty("DisplayName")!.GetValue(attribute)!, "{=taom_ll_mcm_enable}");
        StringAssert.StartsWith((string)type.GetProperty("HintText")!.GetValue(attribute)!, "{=taom_ll_mcm_enable_hint}");
        Assert.AreEqual(false, type.GetProperty("RequireRestart")!.GetValue(attribute));
        var group = property.GetCustomAttributes(inherit: false).Single(a => a.GetType().Name == "SettingPropertyGroupAttribute");
        Assert.AreEqual("Character Screen/Loremaster's Line", McmText.Plain(group.GetType().GetProperty("GroupName")!.GetValue(group) as string));
    }
}
