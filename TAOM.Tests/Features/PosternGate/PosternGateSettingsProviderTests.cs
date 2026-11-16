using System.Linq;
using System.Reflection;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.ServerSettings;
using TAOM.Features;
using TAOM.Features.PosternGate;

namespace TAOM.Tests.Features.PosternGate;

[TestClass]
public class PosternGateSettingsProviderTests
{
    [TestMethod]
    public void NullMcm_DefaultsOn()
    {
        Assert.IsTrue(PosternGateSettingsProvider.From(null));
        Assert.IsTrue(new PosternGateSettingsProvider().CutEnabled);
        Assert.IsTrue(new TaomSettings().EnablePosternGate);
        Assert.IsFalse(PosternGateSettingsProvider.From(new TaomSettings { EnablePosternGate = false }));
    }

    private static object Attribute(PropertyInfo property, string typeName) =>
        property.GetCustomAttributes(inherit: false).Single(a => a.GetType().Name == typeName);

    private static object Read(object attribute, string name) => attribute.GetType().GetProperty(name)!.GetValue(attribute)!;

    [TestMethod]
    public void TheProperty_SitsInItsFiefManagementGroup_AppliesWithoutARestart_AndDefaultsOn()
    {
        // MCM reads these attributes by name; MCMv5 is runtime-only here, so the test does too.
        var property = typeof(TaomSettings).GetProperty(nameof(TaomSettings.EnablePosternGate))!;

        Assert.AreEqual("Fief Management/Postern Gate", McmText.Plain(Read(Attribute(property, "SettingPropertyGroupAttribute"), "GroupName") as string));
        var value = Attribute(property, "SettingPropertyBoolAttribute");
        Assert.AreEqual(false, Read(value, "RequireRestart"), "the cut is read at each break-in or break-out");
        Assert.IsTrue(new TaomSettings().EnablePosternGate);
        StringAssert.Contains((string)Read(value, "HintText"), "by default 50, 67 and 75 percent");
    }
}
