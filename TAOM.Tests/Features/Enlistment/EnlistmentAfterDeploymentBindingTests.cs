using System.Reflection;
using HarmonyLib;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.Enlistment;
using TAOM.Tests.Migration;

namespace TAOM.Tests.Features.Enlistment;

/// <summary>
/// Drift-guards for the after-deployment correction (#576). Once the Order of Battle screen is
/// shut, vanilla's <c>GeneralsAndCaptainsAssignmentLogic</c> still runs its captain assignment over
/// every hero on the player team, the enlisted player included, and may move him into the
/// largest formation of his mount class or into the general's formation. The correction runs in
/// <c>MissionBehavior.OnAfterDeploymentFinished</c>, which the engine dispatches after every
/// <c>OnDeploymentFinished</c> handler, clears any captaincy handed to the player, and puts him
/// back in his assignment's formation. If the hook or either engine member drifts, the soldier
/// silently captains the wrong formation again.
/// </summary>
[TestClass]
public class EnlistmentAfterDeploymentBindingTests
{
    private static bool _gameLoaded;

    [ClassInitialize]
    public static void Init(TestContext _) => _gameLoaded = GameAssemblies.EnsureLoaded();

    private static void RequireGame()
    {
        if (!_gameLoaded)
            Assert.Inconclusive("Game assemblies not loaded: " + string.Join("; ", GameAssemblies.Diagnostics));
    }

    [TestMethod]
    [TestCategory("BindingVerification")]
    public void OnAfterDeploymentFinished_EngineHook_IsVirtualAndParameterless()
    {
        RequireGame();
        var behavior = AccessTools.TypeByName("TaleWorlds.MountAndBlade.MissionBehavior");
        Assert.IsNotNull(behavior, "MissionBehavior did not resolve.");

        var hook = AccessTools.Method(behavior, "OnAfterDeploymentFinished");
        Assert.IsNotNull(hook, "MissionBehavior.OnAfterDeploymentFinished is gone; the after-deployment correction has no hook to run from.");
        Assert.IsTrue(hook!.IsVirtual, "OnAfterDeploymentFinished is no longer virtual; the override would never be called.");
        Assert.AreEqual(0, hook.GetParameters().Length, "OnAfterDeploymentFinished arity drifted.");
    }

    [TestMethod]
    [TestCategory("BindingVerification")]
    public void OnAfterDeploymentFinished_IsOverriddenByTheFormationBehavior()
    {
        RequireGame();

        // Reached through a TAOM type with no engine base class, so this test does not risk a
        // JIT-time type load of MissionLogic before RequireGame() has had its say.
        var behavior = typeof(BattleCommandPolicy).Assembly
            .GetType("TAOM.Features.Enlistment.Hooks.EnlistmentBattleFormationMissionBehavior", throwOnError: false);
        Assert.IsNotNull(behavior, "EnlistmentBattleFormationMissionBehavior did not resolve.");

        var ours = behavior!.GetMethod(
            "OnAfterDeploymentFinished",
            BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly);

        Assert.IsNotNull(ours, "EnlistmentBattleFormationMissionBehavior stopped overriding OnAfterDeploymentFinished; vanilla's captain assignment would move the soldier out of his line unopposed.");
    }

    [TestMethod]
    [TestCategory("BindingVerification")]
    public void Formation_Captain_HasAPublicSetter()
    {
        RequireGame();
        var formation = AccessTools.TypeByName("TaleWorlds.MountAndBlade.Formation");
        Assert.IsNotNull(formation, "Formation did not resolve.");

        var captain = AccessTools.Property(formation, "Captain");
        Assert.IsNotNull(captain, "Formation.Captain is gone; the correction cannot clear a captaincy.");
        Assert.IsNotNull(captain!.GetSetMethod(nonPublic: false), "Formation.Captain lost its public setter; the correction cannot clear a captaincy.");
    }

    [TestMethod]
    [TestCategory("BindingVerification")]
    public void Team_FormationsIncludingSpecialAndEmpty_Exists()
    {
        RequireGame();
        var team = AccessTools.TypeByName("TaleWorlds.MountAndBlade.Team");
        Assert.IsNotNull(team, "Team did not resolve.");

        Assert.IsNotNull(AccessTools.Property(team, "FormationsIncludingSpecialAndEmpty"),
            "Team.FormationsIncludingSpecialAndEmpty is gone; the correction cannot find the formation the player was made captain of.");
    }
}
