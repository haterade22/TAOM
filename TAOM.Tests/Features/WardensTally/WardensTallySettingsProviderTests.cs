using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.ServerSettings;
using TAOM.Features;
using TAOM.Features.CoopInterop;
using TAOM.Features.WardensTally;

namespace TAOM.Tests.Features.WardensTally;

[TestClass]
public class WardensTallySettingsProviderTests
{
    private const int Order = 80;

    private static PropertyInfo Switch => typeof(TaomSettings).GetProperty(nameof(TaomSettings.WardensTallyEnabled))!;

    private static object Attribute(PropertyInfo property, string typeName) =>
        property.GetCustomAttributes(inherit: false).Single(a => a.GetType().Name == typeName);

    private static object Read(object attribute, string name) => attribute.GetType().GetProperty(name)!.GetValue(attribute)!;

    [TestMethod]
    public void NullMcm_DefaultsOn()
    {
        Assert.IsTrue(WardensTallySettingsProvider.DefaultEnabled);
        Assert.IsTrue(WardensTallySettingsProvider.From(null));
        Assert.IsTrue(new WardensTallySettingsProvider().Enabled);
        Assert.IsTrue(new TaomSettings().WardensTallyEnabled);
    }

    [TestMethod]
    public void TheSwitch_ReadsItsOwnProperty()
    {
        var off = new TaomSettings { WardensTallyEnabled = false };

        Assert.IsFalse(WardensTallySettingsProvider.From(off));
        Assert.IsFalse(new WardensTallySettingsProvider(off).Enabled);
    }

    [TestMethod]
    public void TheProvider_ReadsThroughTheCachedSettings()
    {
        var settings = new TaomSettings();
        var sut = new WardensTallySettingsProvider(settings);
        Assert.IsTrue(sut.Enabled);

        settings.WardensTallyEnabled = false;

        Assert.IsFalse(sut.Enabled);
    }

    [TestMethod]
    public void TheProperty_OpensItsOwnGroup_AtAnOrderNoOtherGroupTakes()
    {
        var group = Attribute(Switch, "SettingPropertyGroupAttribute");

        Assert.AreEqual("World/Warden's Tally", McmText.Plain(Read(group, "GroupName") as string));
        Assert.AreEqual(Order, Read(group, "GroupOrder"));
        Assert.AreEqual(0, Read(Attribute(Switch, "SettingPropertyBoolAttribute"), "Order"));

        var holders = typeof(TaomSettings).GetProperties()
            .Where(p => p.GetCustomAttributes(inherit: false).Any(a => a.GetType().Name == "SettingPropertyGroupAttribute" && (int)Read(a, "GroupOrder") == Order))
            .Select(p => p.Name)
            .ToArray();
        CollectionAssert.AreEqual(new[] { nameof(TaomSettings.WardensTallyEnabled) }, holders);
    }

    [TestMethod]
    public void TheSwitch_AppliesWithoutARestart_AndItsHintHasNoDash()
    {
        var attribute = Attribute(Switch, "SettingPropertyBoolAttribute");
        var hint = (string)Read(attribute, "HintText");

        Assert.AreEqual(false, Read(attribute, "RequireRestart"));
        Assert.IsFalse(Regex.IsMatch(hint, "[–—]"), hint);
        Assert.IsTrue(hint.EndsWith("Default: on.", System.StringComparison.Ordinal), hint);
    }

    [TestMethod]
    public void TheSwitch_IsPresentation_SoCoopIgnoresIt()
    {
        Assert.IsTrue(CoopSettingsRelevance.IsExcluded(nameof(TaomSettings.WardensTallyEnabled)));
        Assert.IsFalse(CoopSettingsRelevance.IsSimulationRelevant(Switch));
    }
}
