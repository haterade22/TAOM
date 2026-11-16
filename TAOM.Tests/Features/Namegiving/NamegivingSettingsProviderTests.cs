using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.ServerSettings;
using TAOM.Features;
using TAOM.Features.Namegiving;

namespace TAOM.Tests.Features.Namegiving;

[TestClass]
public class NamegivingSettingsProviderTests
{
    private static object Attribute(PropertyInfo property, string typeName) =>
        property.GetCustomAttributes(inherit: false).Single(a => a.GetType().Name == typeName);

    private static object Read(object attribute, string name) => attribute.GetType().GetProperty(name)!.GetValue(attribute)!;

    [TestMethod]
    public void NullMcm_DefaultsOn()
    {
        Assert.IsTrue(NamegivingSettingsProvider.From(null));
        Assert.IsTrue(new NamegivingSettingsProvider().Enabled);
        Assert.IsTrue(new TaomSettings().EnableNamegiving);
    }

    [TestMethod]
    public void TheSwitch_ReadsItsOwnProperty()
    {
        var off = new TaomSettings { EnableNamegiving = false };

        Assert.IsFalse(NamegivingSettingsProvider.From(off));
        Assert.IsFalse(new NamegivingSettingsProvider(off).Enabled);
    }

    [TestMethod]
    public void TheProperty_SitsInItsOwnFiefManagementGroup_AfterTheThreeSiblings()
    {
        var property = typeof(TaomSettings).GetProperty(nameof(TaomSettings.EnableNamegiving))!;

        var group = Attribute(property, "SettingPropertyGroupAttribute");
        Assert.AreEqual("Fief Management/Namegiving", McmText.Plain(Read(group, "GroupName") as string));
        Assert.AreEqual(3, Read(group, "GroupOrder"), "Postern Gate is 0, From the Ashes 1 and Reeve's Ledger 2");
        Assert.AreEqual(0, Read(Attribute(property, "SettingPropertyBoolAttribute"), "Order"));
    }

    [TestMethod]
    public void TheSwitch_AppliesWithoutARestart_AndTheHintHardCodesNoNumbers()
    {
        var value = Attribute(typeof(TaomSettings).GetProperty(nameof(TaomSettings.EnableNamegiving))!, "SettingPropertyBoolAttribute");

        Assert.AreEqual(false, Read(value, "RequireRestart"));
        Assert.IsFalse(Regex.IsMatch((string)Read(value, "HintText"), @"\d"), "the prices live in the service, not in a hint");
    }

    [TestMethod]
    public void TheSwitch_IsCountedForCoop_BecauseTheLoreHalfReactsToCapturesOnItsOwn()
    {
        // Like EnableFromTheAshes: the lore names change on a capture with no click, so two peers with different values would
        // reach different names. The Rename button alone would be player-local; the switch covers both halves.
        Assert.IsFalse(TAOM.Features.CoopInterop.CoopSettingsRelevance.IsExcluded(nameof(TaomSettings.EnableNamegiving)));
        Assert.IsTrue(TAOM.Features.CoopInterop.CoopSettingsRelevance.IsSimulationRelevant(typeof(TaomSettings).GetProperty(nameof(TaomSettings.EnableNamegiving))!));
    }
}
