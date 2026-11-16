using System.Linq;
using System.Reflection;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.ServerSettings;
using TAOM.Features;
using TAOM.Features.RedArrow;

namespace TAOM.Tests.Features.RedArrow;

[TestClass]
public class RedArrowSettingsProviderTests
{
    private static object Attribute(PropertyInfo property, string typeName) =>
        property.GetCustomAttributes(inherit: false).Single(a => a.GetType().Name == typeName);

    private static object Read(object attribute, string name) => attribute.GetType().GetProperty(name)!.GetValue(attribute)!;

    [TestMethod]
    public void NullMcm_DefaultsOn()
    {
        Assert.IsTrue(RedArrowSettingsProvider.From(null));
        Assert.IsTrue(new TaomSettings().EnableRedArrow);
        Assert.IsFalse(RedArrowSettingsProvider.From(new TaomSettings { EnableRedArrow = false }));
    }

    [TestMethod]
    public void TheProvider_ReadsTheSettingsObjectItWasGiven_LiveEachTime()
    {
        var settings = new TaomSettings();
        var provider = new RedArrowSettingsProvider(settings);

        Assert.IsTrue(provider.Enabled);

        settings.EnableRedArrow = false;
        Assert.IsFalse(provider.Enabled, "MCM edits its one settings object in place");
    }

    [TestMethod]
    public void TheProperty_SitsInTheArmiesGroup_AppliesWithoutARestart_AndSaysWhatStaysVanilla()
    {
        var property = typeof(TaomSettings).GetProperty(nameof(TaomSettings.EnableRedArrow))!;

        var group = Attribute(property, "SettingPropertyGroupAttribute");
        Assert.AreEqual("War of the Ring/Armies", McmText.Plain(Read(group, "GroupName") as string));
        var value = Attribute(property, "SettingPropertyBoolAttribute");
        Assert.AreEqual("Call armies during your siege", McmText.Plain(Read(value, "DisplayName") as string));
        Assert.AreEqual(false, Read(value, "RequireRestart"), "read each time the screen opens");
        var hint = (string)Read(value, "HintText");
        StringAssert.Contains(hint, "Defending a siege");
        StringAssert.Contains(hint, "co-op");
    }

    [TestMethod]
    public void TheGroupOrder_IsCarriedOnceAndNoOtherGroupSharesIt()
    {
        var groups = typeof(TaomSettings).GetProperties()
            .SelectMany(p => p.GetCustomAttributes(inherit: false).Where(a => a.GetType().Name == "SettingPropertyGroupAttribute"))
            .Select(a => (Name: McmText.Plain(McmText.Plain(Read(a, "GroupName") as string)), Order: (int)Read(a, "GroupOrder")))
            .Where(g => g.Order != 0)
            .ToList();

        var mine = groups.Where(g => g.Name == "War of the Ring/Armies").ToList();
        Assert.AreEqual(1, mine.Count, "the order is carried by the group's first property only");
        Assert.AreEqual(1, groups.Count(g => g.Order == mine[0].Order), "another group already uses order " + mine[0].Order);
    }
}
