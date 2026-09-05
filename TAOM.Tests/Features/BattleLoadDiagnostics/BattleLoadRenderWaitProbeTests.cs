using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.BattleLoadDiagnostics;

namespace TAOM.Tests.Features.BattleLoadDiagnostics;

/// <summary>
/// The main-thread-to-watchdog-thread handoff for the shader-compile reading. The native
/// <c>Utilities.GetNumberOfShaderCompilationsInProgress()</c> is read ONLY on the main thread; the
/// background stall watchdog reads this static instead of calling into the engine from a
/// thread-pool thread.
/// </summary>
[TestClass]
public class BattleLoadRenderWaitProbeTests
{
    private static readonly DateTime T0 = new DateTime(2026, 9, 4, 23, 10, 37, DateTimeKind.Utc);

    [TestInitialize]
    public void Setup() => BattleLoadRenderWaitProbe.Reset();

    [TestCleanup]
    public void Cleanup() => BattleLoadRenderWaitProbe.Reset();

    [TestMethod]
    public void ShadersInFlight_BeforeAnySample_IsNeverSampledSentinel()
        => Assert.AreEqual(-1, BattleLoadRenderWaitProbe.ShadersInFlight);

    [TestMethod]
    public void Publish_FirstSample_RecordsCountAndChangeTime()
    {
        BattleLoadRenderWaitProbe.Publish(412, T0);

        Assert.AreEqual(412, BattleLoadRenderWaitProbe.ShadersInFlight);
        Assert.AreEqual(T0.Ticks, BattleLoadRenderWaitProbe.LastChangeUtcTicks);
    }

    // This is the reading the watchdog turns into a verdict: a count that has not moved is a
    // frozen queue, so the timestamp must NOT be refreshed by a repeat of the same value.
    [TestMethod]
    public void Publish_SameCountAgainLater_KeepsTheEarlierChangeTime()
    {
        BattleLoadRenderWaitProbe.Publish(412, T0);
        BattleLoadRenderWaitProbe.Publish(412, T0.AddSeconds(90));

        Assert.AreEqual(T0.Ticks, BattleLoadRenderWaitProbe.LastChangeUtcTicks);
    }

    [TestMethod]
    public void Publish_ChangedCount_AdvancesChangeTime()
    {
        BattleLoadRenderWaitProbe.Publish(412, T0);
        BattleLoadRenderWaitProbe.Publish(411, T0.AddSeconds(90));

        Assert.AreEqual(411, BattleLoadRenderWaitProbe.ShadersInFlight);
        Assert.AreEqual(T0.AddSeconds(90).Ticks, BattleLoadRenderWaitProbe.LastChangeUtcTicks);
    }

    // A count draining to zero is still a change — the watchdog stops deferring on the value, and
    // the timestamp has to be honest about when that happened.
    [TestMethod]
    public void Publish_CountDrainsToZero_RecordsZeroAndAdvancesChangeTime()
    {
        BattleLoadRenderWaitProbe.Publish(3, T0);
        BattleLoadRenderWaitProbe.Publish(0, T0.AddSeconds(2));

        Assert.AreEqual(0, BattleLoadRenderWaitProbe.ShadersInFlight);
        Assert.AreEqual(T0.AddSeconds(2).Ticks, BattleLoadRenderWaitProbe.LastChangeUtcTicks);
    }

    // Reset runs when a load window opens. Carrying the previous mission's reading into a new load
    // would let a stale "still compiling" defer a genuine wedge.
    [TestMethod]
    public void Reset_AfterSampling_ReturnsToNeverSampled()
    {
        BattleLoadRenderWaitProbe.Publish(412, T0);
        BattleLoadRenderWaitProbe.Reset();

        Assert.AreEqual(-1, BattleLoadRenderWaitProbe.ShadersInFlight);
        Assert.AreEqual(0L, BattleLoadRenderWaitProbe.LastChangeUtcTicks);
    }

    [TestMethod]
    public void SecondsSinceLastChange_NeverSampled_ReturnsNull()
        => Assert.IsNull(BattleLoadRenderWaitProbe.SecondsSinceLastChange(T0));

    [TestMethod]
    public void SecondsSinceLastChange_AfterSample_ReturnsElapsedSeconds()
    {
        BattleLoadRenderWaitProbe.Publish(412, T0);

        Assert.AreEqual(90d, BattleLoadRenderWaitProbe.SecondsSinceLastChange(T0.AddSeconds(90)).Value, 0.001d);
    }

    // ---- The continuous-compile clock (the churn backstop's clock) ----
    // Measured from the empty-to-non-empty edge and reset on every dip to zero, mirroring
    // ShaderPrecompileDecider's _activeCompileSinceMs. The first cut of the backstop capped
    // WINDOW time instead, which silently charged a slow pre-render scene load against the
    // shader-compile allowance.

    [TestMethod]
    public void SecondsCompilingContinuously_NeverSampled_ReturnsNull()
        => Assert.IsNull(BattleLoadRenderWaitProbe.SecondsCompilingContinuously(T0));

    [TestMethod]
    public void SecondsCompilingContinuously_QueueEmpty_ReturnsNull()
    {
        BattleLoadRenderWaitProbe.Publish(0, T0);

        Assert.IsNull(BattleLoadRenderWaitProbe.SecondsCompilingContinuously(T0.AddSeconds(30)));
    }

    [TestMethod]
    public void SecondsCompilingContinuously_FromFirstNonEmptySample_CountsUp()
    {
        BattleLoadRenderWaitProbe.Publish(412, T0);

        Assert.AreEqual(120d, BattleLoadRenderWaitProbe.SecondsCompilingContinuously(T0.AddSeconds(120)).Value, 0.001d);
    }

    // The headline behaviour: a count that keeps changing without ever draining must NOT keep
    // restarting its own backstop, or the cap can never trip.
    [TestMethod]
    public void SecondsCompilingContinuously_CountChangesButNeverDrains_KeepsTheOriginalStart()
    {
        BattleLoadRenderWaitProbe.Publish(412, T0);
        BattleLoadRenderWaitProbe.Publish(3, T0.AddSeconds(40));
        BattleLoadRenderWaitProbe.Publish(500, T0.AddSeconds(80));

        Assert.AreEqual(120d, BattleLoadRenderWaitProbe.SecondsCompilingContinuously(T0.AddSeconds(120)).Value, 0.001d);
    }

    // The mirror of it: a queue that actually drains is healthy, and its next burst starts a
    // fresh allowance rather than inheriting the previous one.
    [TestMethod]
    public void SecondsCompilingContinuously_AfterDrainToZero_RestartsOnTheNextBurst()
    {
        BattleLoadRenderWaitProbe.Publish(412, T0);
        BattleLoadRenderWaitProbe.Publish(0, T0.AddSeconds(100));
        BattleLoadRenderWaitProbe.Publish(7, T0.AddSeconds(200));

        Assert.AreEqual(30d, BattleLoadRenderWaitProbe.SecondsCompilingContinuously(T0.AddSeconds(230)).Value, 0.001d);
    }

    [TestMethod]
    public void Reset_AfterCompiling_ClearsTheContinuousClock()
    {
        BattleLoadRenderWaitProbe.Publish(412, T0);
        BattleLoadRenderWaitProbe.Reset();

        Assert.IsNull(BattleLoadRenderWaitProbe.SecondsCompilingContinuously(T0.AddSeconds(30)));
    }
}
