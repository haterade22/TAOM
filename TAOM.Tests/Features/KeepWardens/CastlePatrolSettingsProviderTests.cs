using System.Linq;
using System.Reflection;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.ServerSettings;
using TAOM.Features;
using TAOM.Features.KeepWardens;

namespace TAOM.Tests.Features.KeepWardens;

[TestClass]
public class CastlePatrolSettingsProviderTests
{
    [TestMethod]
    public void NullMcm_DefaultsOn()
    {
        Assert.IsTrue(CastlePatrolSettingsProvider.From(null));
        Assert.IsTrue(new CastlePatrolSettingsProvider().Enabled);
        Assert.IsTrue(new TaomSettings().EnableCastlePatrols);
        Assert.IsFalse(CastlePatrolSettingsProvider.From(new TaomSettings { EnableCastlePatrols = false }));
    }

    [TestMethod]
    public void BuildPriority_NullMcm_DefaultsOn_AndFollowsTheSetting()
    {
        Assert.IsTrue(CastlePatrolSettingsProvider.BuildPriorityFrom(null));
        Assert.IsTrue(new CastlePatrolSettingsProvider().BuildPriorityEnabled);
        Assert.IsTrue(new TaomSettings().EnableGuardHouseBuildPriority);
        Assert.IsFalse(CastlePatrolSettingsProvider.BuildPriorityFrom(new TaomSettings { EnableGuardHouseBuildPriority = false }));
    }

    private static object Attribute(PropertyInfo property, string typeName) =>
        property.GetCustomAttributes(inherit: false).Single(a => a.GetType().Name == typeName);

    private static object Read(object attribute, string name) => attribute.GetType().GetProperty(name)!.GetValue(attribute)!;

    [TestMethod]
    public void TheProperty_SitsInItsFiefManagementGroup_AppliesWithoutARestart_AndDefaultsOn()
    {
        // MCM reads these attributes by name; MCMv5 is runtime-only here, so the test does too.
        var property = typeof(TaomSettings).GetProperty(nameof(TaomSettings.EnableCastlePatrols))!;

        Assert.AreEqual("Fief Management/Keep Wardens", McmText.Plain(Read(Attribute(property, "SettingPropertyGroupAttribute"), "GroupName") as string));
        var value = Attribute(property, "SettingPropertyBoolAttribute");
        Assert.AreEqual(false, Read(value, "RequireRestart"), "the patrol queue reads the switch at each visit");
        Assert.IsTrue(new TaomSettings().EnableCastlePatrols);
        StringAssert.StartsWith((string)Read(value, "DisplayName"), "{=taom_keep_wardens_label}");
        StringAssert.StartsWith((string)Read(value, "HintText"), "{=taom_keep_wardens_hint}");
    }

    [TestMethod]
    public void TheBuildPriorityProperty_SharesTheGroup_SitsAfterCastlePatrols_AndDefaultsOn()
    {
        var property = typeof(TaomSettings).GetProperty(nameof(TaomSettings.EnableGuardHouseBuildPriority))!;

        Assert.AreEqual("Fief Management/Keep Wardens", McmText.Plain(Read(Attribute(property, "SettingPropertyGroupAttribute"), "GroupName") as string));
        var value = Attribute(property, "SettingPropertyBoolAttribute");
        Assert.AreEqual(1, Read(value, "Order"), "after Castle Patrols (Order 0)");
        Assert.AreEqual(false, Read(value, "RequireRestart"), "the AI build pick reads the switch each time");
        StringAssert.StartsWith((string)Read(value, "DisplayName"), "{=taom_keep_wardens_build_label}");
        StringAssert.StartsWith((string)Read(value, "HintText"), "{=taom_keep_wardens_build_hint}");
    }
}
