using System;
using System.Runtime.CompilerServices;
using HarmonyLib;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.MissionTickGuard;

namespace TAOM.Tests.Features.MissionTickGuard;

/// <summary>What Patch91b assumes about Harmony 2.4.2, on a stand-in shaped like v1.4.8 Mission.OnTick.</summary>
[TestClass]
public class TickHandshakeRecoveryHarmonyTests
{
    private const string Owner = "taom.tests.tick-recovery";
    private static TickHandshakeRecovery _recovery = null!;
    private Harmony _harmony = null!;

    public sealed class FakeMission
    {
        public bool tickCompleted = true;
        public bool ThrowBeforeLaunch;

        [MethodImpl(MethodImplOptions.NoInlining)]
        public void OnTick(bool doAsyncAITick)
        {
            tickCompleted = false;
            if (ThrowBeforeLaunch) throw new InvalidOperationException("an OnMissionTick threw");
            if (doAsyncAITick) TickAgentsAndTeamsAsync();   // the job runs on; the flag stays false
            else tickCompleted = true;                      // an inline agent tick reached :3613
        }

        // No NoInlining here, on purpose: the real method is small enough to inline, and
        // PatchRecovery's order (this target first) is what keeps the mark firing.
        public void TickAgentsAndTeamsAsync() { }
    }

    public static class Patches
    {
        public static void Prefix(FakeMission __instance, out bool __state) => __state = _recovery.Enter(__instance.tickCompleted);

        public static void Finalizer(FakeMission __instance, Exception? __exception, bool __state)
        {
            if (_recovery.Exit(__state, __exception != null, __instance.tickCompleted) != RecoveryDecision.None)
                __instance.tickCompleted = true;
        }

        public static void LaunchMark() => _recovery.MarkLaunch();
        public static void ThrowingPostfix() => throw new InvalidOperationException("a postfix threw after the launch");
        public static Exception? SwallowingFinalizer(Exception? __exception) => null;   // PatchShield's shape
    }

    [TestInitialize]
    public void Setup()
    {
        _recovery = new TickHandshakeRecovery();
        _recovery.Arm(true, true, true);
        _harmony = new Harmony(Owner);
    }

    [TestCleanup]
    public void Cleanup() => _harmony.UnpatchAll(Owner);

    private static HarmonyMethod At(string name, int priority) =>
        new HarmonyMethod(AccessTools.Method(typeof(Patches), name)) { priority = priority };

    private static System.Reflection.MethodInfo OnTickMethod() =>
        AccessTools.Method(typeof(FakeMission), nameof(FakeMission.OnTick));

    private void PatchRecovery(bool withPrefix)
    {
        _harmony.Patch(AccessTools.Method(typeof(FakeMission), nameof(FakeMission.TickAgentsAndTeamsAsync)),
            finalizer: At(nameof(Patches.LaunchMark), Priority.First));
        _harmony.Patch(OnTickMethod(),
            prefix: withPrefix ? At(nameof(Patches.Prefix), Priority.First) : null,
            finalizer: At(nameof(Patches.Finalizer), Priority.First));
    }

    [TestMethod]
    public void AThrowBeforeTheLaunch_RestoresTheFlag_AndStillReachesTheCaller()
    {
        PatchRecovery(true);
        var fake = new FakeMission { ThrowBeforeLaunch = true };

        Assert.ThrowsException<InvalidOperationException>(() => fake.OnTick(true));

        Assert.IsTrue(fake.tickCompleted, "a throw with no launch must restore the flag");
    }

    [TestMethod]
    public void AThrowFromAPostfixAfterTheLaunch_LeavesTheFlagDown()
    {
        PatchRecovery(true);
        _harmony.Patch(OnTickMethod(), postfix: At(nameof(Patches.ThrowingPostfix), Priority.Normal));
        var fake = new FakeMission { ThrowBeforeLaunch = false };

        Assert.ThrowsException<InvalidOperationException>(() => fake.OnTick(true));

        Assert.IsFalse(fake.tickCompleted, "the job launched: restoring would let behaviours overlap it");
    }

    [TestMethod]
    public void ALowerPrioritySwallow_StillLetsTheRecoveryRunFirst()
    {
        PatchRecovery(true);
        _harmony.Patch(OnTickMethod(),
            finalizer: new HarmonyMethod(AccessTools.Method(typeof(Patches), nameof(Patches.SwallowingFinalizer))));
        var fake = new FakeMission { ThrowBeforeLaunch = true };

        fake.OnTick(true);

        Assert.IsTrue(fake.tickCompleted, "the recovery must see the exception before a default-priority swallow");
    }

    [TestMethod]
    public void WithoutItsPrefix_TheFinalizerNeverRestores()
    {
        PatchRecovery(false);
        var fake = new FakeMission { ThrowBeforeLaunch = true };

        Assert.ThrowsException<InvalidOperationException>(() => fake.OnTick(true));

        Assert.IsFalse(fake.tickCompleted, "no prefix means __state stays false, so no restore");
    }
}
