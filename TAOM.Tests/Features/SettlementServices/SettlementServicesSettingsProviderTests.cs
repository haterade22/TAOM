using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.ServerSettings;
using TAOM.Features;
using TAOM.Features.SettlementServices;

namespace TAOM.Tests.Features.SettlementServices;

[TestClass]
public class SettlementServicesSettingsProviderTests
{
    private static object Attribute(PropertyInfo property, string typeName) =>
        property.GetCustomAttributes(inherit: false).Single(a => a.GetType().Name == typeName);

    private static object Read(object attribute, string name) => attribute.GetType().GetProperty(name)!.GetValue(attribute)!;

    [TestMethod]
    public void NullMcm_BothDefaultOn()
    {
        Assert.IsTrue(SettlementServicesSettingsProvider.WatchSilverFrom(null));
        Assert.IsTrue(SettlementServicesSettingsProvider.TidingsFrom(null));
        Assert.IsTrue(new SettlementServicesSettingsProvider().WatchSilverEnabled);
        Assert.IsTrue(new SettlementServicesSettingsProvider().TidingsEnabled);
    }

    [TestMethod]
    public void TheDefaults_AreOn()
    {
        var settings = new TaomSettings();

        Assert.IsTrue(settings.EnableWatchSilver);
        Assert.IsTrue(settings.EnableTidingsOfTheLists);
    }

    [TestMethod]
    public void EachSwitch_ReadsItsOwnProperty()
    {
        var silverOff = new TaomSettings { EnableWatchSilver = false };
        var tidingsOff = new TaomSettings { EnableTidingsOfTheLists = false };

        Assert.IsFalse(SettlementServicesSettingsProvider.WatchSilverFrom(silverOff));
        Assert.IsTrue(SettlementServicesSettingsProvider.TidingsFrom(silverOff));
        Assert.IsTrue(SettlementServicesSettingsProvider.WatchSilverFrom(tidingsOff));
        Assert.IsFalse(SettlementServicesSettingsProvider.TidingsFrom(tidingsOff));
        Assert.IsFalse(new SettlementServicesSettingsProvider(silverOff).WatchSilverEnabled);
        Assert.IsFalse(new SettlementServicesSettingsProvider(tidingsOff).TidingsEnabled);
    }

    [TestMethod]
    public void WatchSilver_SitsInItsOwnFiefManagementGroup_AfterTheFourSiblings()
    {
        var property = typeof(TaomSettings).GetProperty(nameof(TaomSettings.EnableWatchSilver))!;

        var group = Attribute(property, "SettingPropertyGroupAttribute");
        Assert.AreEqual("Fief Management/Watch-Silver", McmText.Plain(Read(group, "GroupName") as string));
        Assert.AreEqual(5, Read(group, "GroupOrder"), "Postern Gate is 0, From the Ashes 1, Reeve's Ledger 2, Namegiving 3 and Keep Wardens 4");
        Assert.AreEqual(0, Read(Attribute(property, "SettingPropertyBoolAttribute"), "Order"));
    }

    [TestMethod]
    public void Tidings_SitsInTheTournamentsGroup_AfterTheFourSettingsThere()
    {
        var property = typeof(TaomSettings).GetProperty(nameof(TaomSettings.EnableTidingsOfTheLists))!;

        var group = Attribute(property, "SettingPropertyGroupAttribute");
        Assert.AreEqual("Tournaments", McmText.Plain(Read(group, "GroupName") as string));
        Assert.AreEqual(4, Read(Attribute(property, "SettingPropertyBoolAttribute"), "Order"));
    }

    [TestMethod]
    public void BothSwitches_ApplyWithoutARestart_CarryNoNumbers_AndEndOnTheirDefault()
    {
        foreach (var name in new[] { nameof(TaomSettings.EnableWatchSilver), nameof(TaomSettings.EnableTidingsOfTheLists) })
        {
            var value = Attribute(typeof(TaomSettings).GetProperty(name)!, "SettingPropertyBoolAttribute");
            var hint = (string)Read(value, "HintText");

            Assert.AreEqual(false, Read(value, "RequireRestart"), name);
            Assert.IsTrue(hint.EndsWith("Single-player only. Default: on."), name);
            Assert.IsFalse(Regex.IsMatch(hint, @"\d"), name + ": the prices and the ceiling live in the services, not in a hint");
        }
    }

    [TestMethod]
    public void BothSwitches_AreExcludedFromCoop_AsPlayerLocal()
    {
        // Each switch only gates a player's click, and every write is behind the single-player predicate, so two peers holding
        // different values never compute differently.
        Assert.IsTrue(TAOM.Features.CoopInterop.CoopSettingsRelevance.IsExcluded(nameof(TaomSettings.EnableWatchSilver)));
        Assert.IsTrue(TAOM.Features.CoopInterop.CoopSettingsRelevance.IsExcluded(nameof(TaomSettings.EnableTidingsOfTheLists)));
    }
}
