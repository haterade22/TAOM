using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.ServerSettings;
using TAOM.Features;
using TAOM.Features.FromTheAshes;

namespace TAOM.Tests.Features.FromTheAshes;

[TestClass]
public class FromTheAshesSettingsProviderTests
{
    [TestMethod]
    public void NullMcm_DefaultsOn()
    {
        Assert.IsTrue(FromTheAshesSettingsProvider.From(null));
        Assert.IsTrue(new FromTheAshesSettingsProvider().Enabled);
        Assert.IsTrue(new TaomSettings().EnableFromTheAshes);
        Assert.IsFalse(FromTheAshesSettingsProvider.From(new TaomSettings { EnableFromTheAshes = false }));
    }

    private static object Attribute(PropertyInfo property, string typeName) =>
        property.GetCustomAttributes(inherit: false).Single(a => a.GetType().Name == typeName);

    private static object Read(object attribute, string name) => attribute.GetType().GetProperty(name)!.GetValue(attribute)!;

    [TestMethod]
    public void TheProperty_SitsInItsFiefManagementGroup_AppliesWithoutARestart_AndDefaultsOn()
    {
        // MCM reads these attributes by name; MCMv5 is runtime-only here, so the test does too.
        var property = typeof(TaomSettings).GetProperty(nameof(TaomSettings.EnableFromTheAshes))!;

        var group = Attribute(property, "SettingPropertyGroupAttribute");
        Assert.AreEqual("Fief Management/From the Ashes", McmText.Plain(Read(group, "GroupName") as string));
        Assert.AreEqual(1, Read(group, "GroupOrder"), "the group's first property carries GroupOrder");
        var value = Attribute(property, "SettingPropertyBoolAttribute");
        Assert.AreEqual("From the Ashes", McmText.Plain(Read(value, "DisplayName") as string));
        Assert.AreEqual(false, Read(value, "RequireRestart"), "the switch is read at each menu open");
        Assert.IsTrue(new TaomSettings().EnableFromTheAshes);
    }

    [TestMethod]
    public void TheHint_HardCodesNoNumbers()
    {
        // The numbers live in from_the_ashes.json; a hint that quoted them would drift from the file.
        var property = typeof(TaomSettings).GetProperty(nameof(TaomSettings.EnableFromTheAshes))!;
        var hint = (string)Read(Attribute(property, "SettingPropertyBoolAttribute"), "HintText");

        Assert.IsFalse(Regex.IsMatch(hint, @"\d"), hint);
    }
}
