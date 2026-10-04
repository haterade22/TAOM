using System;
using System.Text.RegularExpressions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Tests.Infrastructure;

namespace TAOM.Tests.Features.MissionPerf;

/// <summary>
/// Source pins for the Patch97 tick profiler's two SubModule.cs lines. The installer must run inside the
/// once-per-process game-init block (a second game init must not re-apply a transpiler) and after Patch91,
/// whose agent-tick bracket it reads; the mission behaviour must sit beside the heartbeat so both 5 s
/// windows tick in the same OnMissionTick loop. Neither can be exercised without the game.
/// </summary>
[TestClass]
public class MissionTickProfilerWiringTests
{
    private const string InstallCall = "Features.MissionPerf.Hooks.MissionTickProfilerInstaller.InstallIfEnabled(";
    private const string BehaviorAdd = "AddTaomBehavior(new Features.MissionPerf.Hooks.MissionTickProfilerBehavior(";

    private static string Source() => RepoPaths.ReadSource("Main/SubModule.cs", stripComments: true);

    private static int Count(string text, string needle) => Regex.Matches(text, Regex.Escape(needle)).Count;

    // What makes the install once-per-process is the early return, not the latch assignment after it: the guard
    // must exist, come first, and sit in the same method as the install. Takes the source text so the mutation
    // tests below can run it on a damaged copy.
    private static void AssertInstallIsGuarded(string src)
    {
        var method = src.IndexOf("public override void OnGameInitializationFinished(Game game)", StringComparison.Ordinal);
        var guard = Regex.Match(src, @"if \(_gameInitPatchesApplied\)\s*return;");
        var latch = src.IndexOf("_gameInitPatchesApplied = true;", StringComparison.Ordinal);
        var install = src.IndexOf(InstallCall, StringComparison.Ordinal);

        Assert.IsTrue(method >= 0 && guard.Success && latch >= 0 && install >= 0,
            "The once-per-process guard, its latch or the install call is missing.");
        Assert.IsTrue(method < guard.Index && guard.Index < latch && latch < install,
            "Order must be: method start, early return, latch, install.");
        var between = src.Substring(method + 1, install - method - 1);
        Assert.IsFalse(Regex.IsMatch(between, @"\n    (?:public|private|protected|internal) "),
            "The install must sit in OnGameInitializationFinished itself, not in a later member.");
    }

    [TestMethod]
    public void SubModule_InstallsTheProfilerOnlyBehindTheOncePerProcessGuard()
    {
        var src = Source();

        Assert.AreEqual(1, Count(src, "_gameInitPatchesApplied = true;"), "One latch.");
        AssertInstallIsGuarded(src);
    }

    [TestMethod]
    public void InstallGuardCheck_WithTheEarlyReturnRemoved_Fails()
    {
        var mutated = Regex.Replace(Source(), @"if \(_gameInitPatchesApplied\)\s*return;", string.Empty);

        Assert.AreNotEqual(Source(), mutated, "The mutation found nothing to remove: the guard's shape changed.");
        Assert.ThrowsException<AssertFailedException>(() => AssertInstallIsGuarded(mutated));
    }

    [TestMethod]
    public void InstallGuardCheck_WithTheLatchRemoved_Fails()
    {
        var mutated = Source().Replace("_gameInitPatchesApplied = true;", string.Empty);

        Assert.AreNotEqual(Source(), mutated, "The mutation found nothing to remove: the latch's shape changed.");
        Assert.ThrowsException<AssertFailedException>(() => AssertInstallIsGuarded(mutated));
    }

    [TestMethod]
    public void InstallGuardCheck_WithTheInstallMovedIntoALaterMember_Fails()
    {
        var src = Source();
        var mutated = src.Insert(src.IndexOf(InstallCall, StringComparison.Ordinal), "\n    private void Elsewhere()\n    {\n");

        Assert.ThrowsException<AssertFailedException>(() => AssertInstallIsGuarded(mutated));
    }

    [TestMethod]
    public void SubModule_InstallsTheProfiler_InsideTheOncePerProcessGameInitBlock_AfterPatch91()
    {
        var src = Source();
        Assert.AreEqual(1, Count(src, InstallCall), "The installer is called exactly once.");
        var install = src.IndexOf(InstallCall);
        var guard = src.IndexOf("_gameInitPatchesApplied = true;");
        var patch91 = src.IndexOf("TryPatchCategory(\"Patch91_MissionTickStall\");");
        var missionInit = src.IndexOf("public override void OnMissionBehaviorInitialize");

        Assert.IsTrue(guard >= 0 && patch91 >= 0 && missionInit >= 0, "An anchor moved.");
        Assert.IsTrue(install > guard, "Install must be inside the once-per-process block.");
        Assert.IsTrue(install > patch91, "Install must follow Patch91.");
        Assert.IsTrue(install < missionInit, "Install must be in OnGameInitializationFinished, before OnMissionBehaviorInitialize.");
    }

    [TestMethod]
    public void SubModule_AddsTheProfilerBehavior_RightAfterTheHeartbeat()
    {
        var src = Source();
        Assert.AreEqual(1, Count(src, BehaviorAdd), "The profiler behaviour is added exactly once.");
        var add = src.IndexOf(BehaviorAdd);
        var heartbeat = src.IndexOf("AddTaomBehavior(new Features.MissionPerf.Hooks.MissionPerfHeartbeatBehavior(");
        var actionBar = src.IndexOf("BattleActionBarMissionView");

        Assert.IsTrue(heartbeat >= 0 && actionBar >= 0, "An anchor moved.");
        Assert.IsTrue(add > heartbeat && add < actionBar, "The profiler behaviour goes right after the heartbeat.");
    }
}
