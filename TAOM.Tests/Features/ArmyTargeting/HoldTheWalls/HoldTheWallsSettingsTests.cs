using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Core.Logging;
using TAOM.Features.ServerSettings;
using TAOM.Features;
using TAOM.Features.ArmyTargeting;

namespace TAOM.Tests.Features.ArmyTargeting.HoldTheWalls;

/// <summary>
/// #871: "Good armies hold captured forts" sits in the AI Strategic Intelligence group. The switch is read through
/// <see cref="IArmyTargetingSettingsProvider"/> like its siblings; the service needs it and the group's master switch.
/// </summary>
[TestClass]
public class HoldTheWallsSettingsTests
{
    [TestMethod]
    public void McmDefault_IsOn()
    {
        Assert.IsTrue(new TaomSettings().HoldCapturedForts);
    }

    [TestMethod]
    public void TheSharedProvider_ReadsTheSwitchOn_WithNoMcmInstance()
    {
        Assert.IsNull(TaomSettings.Instance, "this test is about the no-instance fallback");

        Assert.IsTrue(new ArmyTargetingSettingsProvider(Substitute.For<IModLogger>()).HoldCapturedForts);
    }

    private static object Attribute(string typeName) =>
        typeof(TaomSettings).GetProperty(nameof(TaomSettings.HoldCapturedForts))!
            .GetCustomAttributes(inherit: false).Single(a => a.GetType().Name == typeName);

    private static object Read(object attribute, string name) => attribute.GetType().GetProperty(name)!.GetValue(attribute)!;

    [TestMethod]
    public void TheSetting_IsInTheAiStrategicIntelligenceGroup_WithoutARestart()
    {
        // MCM reads these attributes by name; MCMv5 is runtime-only here, so the test does too.
        Assert.AreEqual("AI Strategic Intelligence", McmText.Plain(Read(Attribute("SettingPropertyGroupAttribute"), "GroupName") as string));
        var value = Attribute("SettingPropertyBoolAttribute");
        Assert.AreEqual("Good armies hold captured forts", McmText.Plain(Read(value, "DisplayName") as string));
        Assert.AreEqual(false, Read(value, "RequireRestart"), "the pass reads the switch every hour: a restart is never needed");
    }

    [TestMethod]
    public void TheHint_IsKeyed_AndNamesTheMasterSwitchAndTheDefault()
    {
        var hint = (string)Read(Attribute("SettingPropertyBoolAttribute"), "HintText");

        StringAssert.StartsWith(hint, "{=taom_mcm_", "MCM hints carry a taom_mcm_ key (#1385)");
        StringAssert.Contains(hint, "AI Strategic Intelligence");
        StringAssert.Contains(hint, "Default: on");
    }

    [TestMethod]
    public void TheHint_SaysEarlyReleasesStopWorkingAfterALoad_AndStatesTheShippedRange()
    {
        var hint = (string)Read(Attribute("SettingPropertyBoolAttribute"), "HintText");

        StringAssert.Contains(hint, "after loading a save");
        StringAssert.Contains(hint, "12 to 30");
    }
}
