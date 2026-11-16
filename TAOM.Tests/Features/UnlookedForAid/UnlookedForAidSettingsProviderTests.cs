using System.Linq;
using System.Reflection;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.ServerSettings;
using TAOM.Features;
using TAOM.Features.UnlookedForAid;

namespace TAOM.Tests.Features.UnlookedForAid;

[TestClass]
public class UnlookedForAidSettingsProviderTests
{
    [TestMethod]
    public void NullMcm_DefaultsOn()
    {
        Assert.IsTrue(UnlookedForAidSettingsProvider.From(null));
        Assert.IsTrue(new TaomSettings().EnableUnlookedForAid);
        Assert.IsFalse(UnlookedForAidSettingsProvider.From(new TaomSettings { EnableUnlookedForAid = false }));
    }

    private static object Attribute(PropertyInfo property, string typeName) =>
        property.GetCustomAttributes(inherit: false).Single(a => a.GetType().Name == typeName);

    private static object Read(object attribute, string name) => attribute.GetType().GetProperty(name)!.GetValue(attribute)!;

    [TestMethod]
    public void TheProperty_SitsInItsWorldGroup_AppliesWithoutARestart_AndExplainsTheStrengthGuard()
    {
        // MCM reads these attributes by name; MCMv5 is runtime-only here, so the test does too.
        var property = typeof(TaomSettings).GetProperty(nameof(TaomSettings.EnableUnlookedForAid))!;

        Assert.AreEqual("World/Help Unlooked-For", McmText.Plain(Read(Attribute(property, "SettingPropertyGroupAttribute"), "GroupName") as string));
        var value = Attribute(property, "SettingPropertyBoolAttribute");
        Assert.AreEqual("Lords stop fleeing sieges", McmText.Plain(Read(value, "DisplayName") as string));
        Assert.AreEqual(false, Read(value, "RequireRestart"), "read on every avoid question");
        StringAssert.Contains((string)Read(value, "HintText"), "at least half as strong");
    }
}
