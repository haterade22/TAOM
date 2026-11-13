using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features;
using TAOM.Features.ReevesLedger;

namespace TAOM.Tests.Features.ReevesLedger;

[TestClass]
public class ReevesLedgerSettingsProviderTests
{
    [TestMethod]
    public void NullMcm_BothSwitchesDefaultOn()
    {
        var sut = new ReevesLedgerSettingsProvider();

        Assert.IsTrue(ReevesLedgerSettingsProvider.LedgerFrom(null));
        Assert.IsTrue(ReevesLedgerSettingsProvider.StepsFrom(null));
        Assert.IsTrue(sut.LedgerEnabled);
        Assert.IsTrue(sut.StepsEnabled);
        var settings = new TaomSettings();
        Assert.IsTrue(settings.EnableReevesLedger);
        Assert.IsTrue(settings.EnableReserveSteps);
    }

    [TestMethod]
    public void EachSwitch_ReadsItsOwnProperty()
    {
        var ledgerOff = new TaomSettings { EnableReevesLedger = false };
        var stepsOff = new TaomSettings { EnableReserveSteps = false };

        Assert.IsFalse(ReevesLedgerSettingsProvider.LedgerFrom(ledgerOff));
        Assert.IsTrue(ReevesLedgerSettingsProvider.StepsFrom(ledgerOff));
        Assert.IsTrue(ReevesLedgerSettingsProvider.LedgerFrom(stepsOff));
        Assert.IsFalse(ReevesLedgerSettingsProvider.StepsFrom(stepsOff));
        Assert.IsFalse(new ReevesLedgerSettingsProvider(ledgerOff).LedgerEnabled);
        Assert.IsFalse(new ReevesLedgerSettingsProvider(stepsOff).StepsEnabled);
    }

    private static object Attribute(PropertyInfo property, string typeName) =>
        property.GetCustomAttributes(inherit: false).Single(a => a.GetType().Name == typeName);

    private static object Read(object attribute, string name) => attribute.GetType().GetProperty(name)!.GetValue(attribute)!;

    [TestMethod]
    public void TheFirstProperty_CarriesTheGroupOrder_AfterTheTwoSiblingGroups()
    {
        // MCM reads these attributes by name; MCMv5 is runtime-only here, so the test does too.
        var first = typeof(TaomSettings).GetProperty(nameof(TaomSettings.EnableReevesLedger))!;
        var second = typeof(TaomSettings).GetProperty(nameof(TaomSettings.EnableReserveSteps))!;

        var group = Attribute(first, "SettingPropertyGroupAttribute");
        Assert.AreEqual("Fief Management/Reeve's Ledger", Read(group, "GroupName"));
        Assert.AreEqual(2, Read(group, "GroupOrder"), "Postern Gate is 0 and From the Ashes is 1; the first property carries the order");
        Assert.AreEqual("Fief Management/Reeve's Ledger", Read(Attribute(second, "SettingPropertyGroupAttribute"), "GroupName"));
        Assert.AreEqual(0, Read(Attribute(first, "SettingPropertyBoolAttribute"), "Order"));
        Assert.AreEqual(1, Read(Attribute(second, "SettingPropertyBoolAttribute"), "Order"));
    }

    [TestMethod]
    public void EveryFiefManagementSubgroup_HasItsOwnGroupOrder()
    {
        // A group's order comes from the property that carries it, so the largest value on the group's properties is its order.
        var orders = typeof(TaomSettings).GetProperties()
            .SelectMany(p => p.GetCustomAttributes(inherit: false))
            .Where(a => a.GetType().Name == "SettingPropertyGroupAttribute" && ((string)Read(a, "GroupName")).StartsWith("Fief Management/"))
            .GroupBy(a => (string)Read(a, "GroupName"), a => (int)Read(a, "GroupOrder"))
            .Select(g => g.Key + "=" + g.Max())
            .ToList();

        CollectionAssert.AreEquivalent(
            new[] { "Fief Management/Postern Gate=0", "Fief Management/From the Ashes=1", "Fief Management/Reeve's Ledger=2", "Fief Management/Namegiving=3", "Fief Management/Keep Wardens=4",
                "Fief Management/Watch-Silver=5", "Fief Management/Hall Seats=6" }, orders);
    }

    [TestMethod]
    public void BothSwitches_ApplyOnTheNextWindowOpen_NotARestart_AndHintsHardCodeNoNumbers()
    {
        foreach (var name in new[] { nameof(TaomSettings.EnableReevesLedger), nameof(TaomSettings.EnableReserveSteps) })
        {
            var value = Attribute(typeof(TaomSettings).GetProperty(name)!, "SettingPropertyBoolAttribute");

            Assert.AreEqual(false, Read(value, "RequireRestart"), name);
            Assert.IsFalse(Regex.IsMatch((string)Read(value, "HintText"), @"\d"), name + " hint: the step size lives in the service, not in a hint");
        }
    }
}
