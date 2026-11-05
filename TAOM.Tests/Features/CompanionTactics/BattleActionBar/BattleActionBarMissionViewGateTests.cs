using System.Reflection;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TaleWorlds.Core;


namespace TAOM.Tests.Features.CompanionTactics.BattleActionBar;

/// <summary>
/// Issue #970. Mission.Mode is Deployment when the screen initializes a field battle (DeploymentHandler.cs:41), so the
/// bar attaches on the first Battle-mode tick, and "field battle" is decided by Mission.IsFieldBattle, not the mode.
/// The view derives from a type in an assembly this project does not reference, so the gate is called by reflection.
/// </summary>
[TestClass]
public class BattleActionBarMissionViewGateTests
{
    [ClassInitialize]
    public static void Init(Microsoft.VisualStudio.TestTools.UnitTesting.TestContext _) =>
        TAOM.Tests.Migration.GameAssemblies.EnsureLoaded();

    private static bool ShouldAttach(bool enabled, bool isFieldBattle, MissionMode mode)
    {
        var type = typeof(SubModule).Assembly.GetType(
            "TAOM.Features.CompanionTactics.BattleActionBar.Hooks.BattleActionBarMissionView", throwOnError: true)!;
        var method = type.GetMethod("ShouldAttach", BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public);
        Assert.IsNotNull(method, "BattleActionBarMissionView.ShouldAttach must exist");
        return (bool)method!.Invoke(null, new object[] { enabled, isFieldBattle, mode })!;
    }

    [DataTestMethod]
    [DataRow(true, true, MissionMode.Deployment, false)]
    [DataRow(true, true, MissionMode.Battle, true)]
    [DataRow(true, false, MissionMode.Battle, false)]
    [DataRow(false, true, MissionMode.Battle, false)]
    [DataRow(true, true, MissionMode.StartUp, false)]
    public void ShouldAttach_RequiresEnabledFieldBattleInBattleMode(bool enabled, bool isFieldBattle, MissionMode mode, bool expected) =>
        Assert.AreEqual(expected, ShouldAttach(enabled, isFieldBattle, mode));
}
