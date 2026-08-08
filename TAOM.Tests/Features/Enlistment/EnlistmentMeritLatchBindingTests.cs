using System.Reflection;
using HarmonyLib;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.Enlistment.Content;
using TAOM.Tests.Migration;

namespace TAOM.Tests.Features.Enlistment;

/// <summary>
/// Drift-guard for the "did this battle actually resolve?" latch. Verified against installed
/// v1.4.7: <c>Mission.MissionResult</c> is assigned in exactly one place, <c>CheckMissionEnded</c>,
/// which then calls <c>OnMissionResultReady</c> on every MissionLogic — while a player-initiated
/// exit goes <c>RetreatMission()</c>/<c>SurrenderMission()</c> → <c>EndMission()</c> and never
/// produces a result. That asymmetry IS the walkout detector.
///
/// Both directions of drift are silent and both are bad. If TaleWorlds renames or drops the hook,
/// or a refactor drops our override, <c>_battleResolved</c> never latches and EVERY enlisted
/// battle scores as a walkout. If someone starts latching on the <c>missionResult</c> argument
/// instead of the call, a logic that returns true from <c>MissionEnded(ref)</c> without populating
/// the ref would read as a walkout too.
/// </summary>
[TestClass]
public class EnlistmentMeritLatchBindingTests
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
    public void OnMissionResultReady_EngineHook_IsVirtualAndTakesAMissionResult()
    {
        RequireGame();
        var missionLogic = AccessTools.TypeByName("TaleWorlds.MountAndBlade.MissionLogic");
        Assert.IsNotNull(missionLogic, "MissionLogic did not resolve.");

        var hook = AccessTools.Method(missionLogic, "OnMissionResultReady");
        Assert.IsNotNull(hook, "MissionLogic.OnMissionResultReady is gone — the merit sampler has no way to tell a resolved battle from a walkout.");
        Assert.IsTrue(hook.IsVirtual, "OnMissionResultReady is no longer virtual — the override would never be called.");

        var parameters = hook.GetParameters();
        Assert.AreEqual(1, parameters.Length, "OnMissionResultReady arity drifted.");
        Assert.AreEqual("MissionResult", parameters[0].ParameterType.Name, "OnMissionResultReady no longer takes a MissionResult.");
    }

    [TestMethod]
    [TestCategory("BindingVerification")]
    public void OnMissionResultReady_IsOverriddenByTheMeritSampler()
    {
        RequireGame();

        // Reached through a TAOM type with no engine base class, so this test does not risk a
        // JIT-time type load of MissionLogic before RequireGame() has had its say. GetType with
        // throwOnError:false returns null rather than throwing if the engine is still missing.
        var behavior = typeof(MeritGeometryAccumulator).Assembly
            .GetType("TAOM.Features.Enlistment.Hooks.EnlistmentMeritMissionBehavior", throwOnError: false);
        Assert.IsNotNull(behavior, "EnlistmentMeritMissionBehavior did not resolve.");

        var ours = behavior!.GetMethod(
            "OnMissionResultReady",
            BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly);

        Assert.IsNotNull(ours, "EnlistmentMeritMissionBehavior stopped overriding OnMissionResultReady — every enlisted battle would now score as a walkout.");
    }
}
