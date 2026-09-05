using System.Linq;
using System.Reflection;
using HarmonyLib;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TaleWorlds.MountAndBlade;

namespace TAOM.Tests.Features.BattleLoadDiagnostics;

/// <summary>
/// Named binding checks for the two PRIVATE engine methods the three-bucket load split patches
/// (2026-08-07). <c>HarmonyPatchBindingTests</c> already resolves every attribute-bound target
/// generically via <c>AccessTools.Method</c> (which searches non-public), so these add no coverage
/// the suite lacks — they add a FAILURE MESSAGE that names the engine method that moved, instead of
/// the generic "did not resolve against the installed engine".
///
/// Verified against the installed v1.4.8 <c>TaleWorlds.MountAndBlade.MissionState</c>:
/// <c>private void TickLoading(float realDt)</c>, <c>private void FinishMissionLoading()</c>
/// and <c>protected override void OnTick(float realDt)</c>.
/// </summary>
[TestClass]
[TestCategory("BindingVerification")]
public class Patch43LoadPhaseBindingTests
{
    private const BindingFlags AnyInstance =
        BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;

    [TestMethod]
    public void MissionState_FinishMissionLoading_ResolvesAgainstInstalledEngine()
    {
        var target = AccessTools.Method(typeof(MissionState), "FinishMissionLoading");

        Assert.IsNotNull(target,
            "MissionState.FinishMissionLoading did not resolve. It is bound BY STRING (it is private), so "
            + "an engine rename silently drops the FinishMissionLoadingBegin/Done markers and the async-wait "
            + "bucket becomes unattributed again.");
        Assert.AreEqual(0, target.GetParameters().Length,
            "FinishMissionLoading is expected to be parameterless; a new parameter makes the string binding ambiguous.");
    }

    [TestMethod]
    public void MissionState_TickLoading_ResolvesAgainstInstalledEngine()
    {
        var target = AccessTools.Method(typeof(MissionState), "TickLoading");

        Assert.IsNotNull(target,
            "MissionState.TickLoading did not resolve. It is bound BY STRING (it is private). Losing it does "
            + "not lose a log line — it makes FinishMissionLoadingBegin report polls=0, which the feature doc "
            + "documents as 'the binding failed', not 'there was no wait'.");
        Assert.AreEqual(1, target.GetParameters().Length, "TickLoading is expected to take exactly (float realDt).");
        Assert.AreEqual(typeof(float), target.GetParameters()[0].ParameterType);
    }

    /// <summary>
    /// A future overload would make <c>[HarmonyPatch(typeof(MissionState), "TickLoading")]</c>
    /// ambiguous — Harmony would bind whichever AccessTools returns first, silently.
    /// </summary>
    [TestMethod]
    public void MissionState_TickLoading_IsNotOverloaded()
    {
        var overloads = typeof(MissionState)
            .GetMethods(AnyInstance)
            .Where(m => m.Name == "TickLoading")
            .ToList();

        Assert.AreEqual(1, overloads.Count,
            "MissionState.TickLoading is bound by name with no argument types. More than one overload makes "
            + "that binding ambiguous: " + string.Join(", ", overloads.Select(m => m.ToString())));
    }

    /// <summary>
    /// <c>MissionState.OnTick(float)</c> is the render-wait marker's host. It is the method that
    /// withholds <c>TickMission</c> behind <c>Handler.RenderIsReady()</c>, so it is the only place
    /// that can observe the FinishMissionLoadingDone -> BattlePlayable window from the inside
    /// (bundle b18f3441). It is <c>protected override</c>, so the binding is by string.
    /// </summary>
    [TestMethod]
    public void MissionState_OnTick_ResolvesAgainstInstalledEngine()
    {
        var target = AccessTools.Method(typeof(MissionState), "OnTick");

        Assert.IsNotNull(target,
            "MissionState.OnTick did not resolve. It is bound BY STRING (it is protected), and losing it "
            + "reopens the 290-second unattributed span between FinishMissionLoadingDone and BattlePlayable.");
        Assert.AreEqual(1, target.GetParameters().Length, "OnTick is expected to take exactly (float realDt).");
        Assert.AreEqual(typeof(float), target.GetParameters()[0].ParameterType);
    }

    /// <summary>
    /// <c>OnTick</c> is a common engine name and the patch binds it with no argument types. An
    /// overload declared on <c>MissionState</c> would make that binding silently ambiguous.
    /// </summary>
    [TestMethod]
    public void MissionState_OnTick_IsNotOverloaded()
    {
        var overloads = typeof(MissionState)
            .GetMethods(AnyInstance)
            .Where(m => m.Name == "OnTick")
            .ToList();

        Assert.AreEqual(1, overloads.Count,
            "MissionState.OnTick is bound by name with no argument types. More than one overload makes "
            + "that binding ambiguous: " + string.Join(", ", overloads.Select(m => m.ToString())));
    }

    /// <summary>
    /// The render-wait hook's cheap guard is <c>FirstMissionTickAfterLoading</c>: it is true from
    /// OnInitialize until the end of the first <c>TickMission</c>, which is exactly the window we
    /// want, and reading it is a field read on a path that runs every frame of every mission.
    /// </summary>
    [TestMethod]
    public void MissionState_FirstMissionTickAfterLoading_IsPubliclyReadable()
    {
        var prop = typeof(MissionState).GetProperty("FirstMissionTickAfterLoading");

        Assert.IsNotNull(prop,
            "MissionState.FirstMissionTickAfterLoading is the render-wait hook's frame guard. Without it "
            + "the hook would have to infer the window and would run past the first tick.");
        Assert.AreEqual(typeof(bool), prop.PropertyType);
        Assert.IsNotNull(prop.GetGetMethod(), "the getter must be public — the hook reads it every frame.");
    }
}
