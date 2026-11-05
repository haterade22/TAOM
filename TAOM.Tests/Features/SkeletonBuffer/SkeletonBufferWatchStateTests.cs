// Adapted from yotthani's VanillaTuning (MIT, (c) 2026 yotthani), frame-buffer-guard and frame-buffer-watch.
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.SkeletonBuffer;

namespace TAOM.Tests.Features.SkeletonBuffer;

/// <summary>The watch's peak tracking for both pools and its one-time warning level.</summary>
[TestClass]
public class SkeletonBufferWatchStateTests
{
    [TestMethod]
    public void Observe_FirstReading_BecomesThePeakWithItsContext()
    {
        var state = new SkeletonBufferWatchState();

        state.Observe(1200, 3.5, () => 640);

        Assert.IsTrue(state.HasSamples);
        Assert.AreEqual(1200, state.PeakFill);
        Assert.AreEqual(640, state.PeakAgents);
        Assert.AreEqual(3.5, state.PeakSeconds, 1e-9);
    }

    [TestMethod]
    public void Observe_FirstReadingOfZero_StillCountsAsASample()
    {
        var state = new SkeletonBufferWatchState();

        state.Observe(0, 0.1, () => 0);

        Assert.IsTrue(state.HasSamples);
        Assert.AreEqual(0, state.PeakFill);
    }

    [TestMethod]
    public void Observe_NoReadingYet_HasNoSamples()
    {
        Assert.IsFalse(new SkeletonBufferWatchState().HasSamples);
    }

    [TestMethod]
    public void Observe_HigherReading_ReplacesThePeakAndItsContext()
    {
        var state = new SkeletonBufferWatchState();
        state.Observe(1200, 3.5, () => 640);

        state.Observe(30000, 40.0, () => 1650);

        Assert.AreEqual(30000, state.PeakFill);
        Assert.AreEqual(1650, state.PeakAgents);
        Assert.AreEqual(40.0, state.PeakSeconds, 1e-9);
    }

    [TestMethod]
    public void Observe_LowerOrEqualReading_KeepsThePeakAndNeverAsksForTheAgentCount()
    {
        var state = new SkeletonBufferWatchState();
        state.Observe(30000, 40.0, () => 1650);
        var asked = 0;

        state.Observe(29999, 41.0, () => { asked++; return 1; });
        state.Observe(30000, 42.0, () => { asked++; return 1; });

        Assert.AreEqual(30000, state.PeakFill);
        Assert.AreEqual(1650, state.PeakAgents);
        Assert.AreEqual(40.0, state.PeakSeconds, 1e-9);
        Assert.AreEqual(0, asked, "the agent count is read only at a new peak");
    }

    [TestMethod]
    public void Percent_AndSkeletons_AreDerivedFromThePeak()
    {
        var state = new SkeletonBufferWatchState();
        state.Observe(41230, 38.2, () => 1650);

        Assert.AreEqual(62.9, state.PeakPercent, 0.05);
        Assert.AreEqual(1472, state.PeakSkeletons);   // 41230 / 28
        Assert.AreEqual(65536, SkeletonBufferWatchState.Capacity);
        Assert.AreEqual(28, SkeletonBufferWatchState.EntriesPerSkeleton);
    }

    [TestMethod]
    public void TakeWarning_BelowNinetyPercent_IsFalse()
    {
        var state = new SkeletonBufferWatchState();
        state.Observe(58982, 10, () => 1);   // 89.9985 %

        Assert.IsFalse(state.TakeWarning(guarded: false));
    }

    [TestMethod]
    public void TakeWarning_AtNinetyPercentWithoutAGuard_IsTrueOnceOnly()
    {
        var state = new SkeletonBufferWatchState();
        state.Observe(58983, 10, () => 1);

        Assert.IsTrue(state.TakeWarning(guarded: false));
        Assert.IsFalse(state.TakeWarning(guarded: false));
        state.Observe(65000, 11, () => 1);
        Assert.IsFalse(state.TakeWarning(guarded: false), "once per mission");
    }

    [TestMethod]
    public void TakeWarning_AtNinetyPercentWithAGuard_IsFalse()
    {
        var state = new SkeletonBufferWatchState();
        state.Observe(65000, 10, () => 1);

        Assert.IsFalse(state.TakeWarning(guarded: true));
    }

    [TestMethod]
    public void TakeWarning_AGuardedCallDoesNotSpendTheOneWarning()
    {
        var state = new SkeletonBufferWatchState();
        state.Observe(65000, 10, () => 1);

        Assert.IsFalse(state.TakeWarning(guarded: true));
        Assert.IsTrue(state.TakeWarning(guarded: false));
    }

    [TestMethod]
    public void ObservePool2_KeepsItsOwnPeakApartFromPool1()
    {
        var state = new SkeletonBufferWatchState();
        state.Observe(30000, 1.0, () => 5);

        state.ObservePool2(70000);
        state.ObservePool2(60000);

        Assert.IsTrue(state.Pool2HasSamples);
        Assert.AreEqual(70000, state.Pool2PeakFill);
        Assert.AreEqual(30000, state.PeakFill);
    }

    [TestMethod]
    public void ObservePool2_NoReadingYet_HasNoSamples()
    {
        Assert.IsFalse(new SkeletonBufferWatchState().Pool2HasSamples);
    }

    [TestMethod]
    public void ObservePool2_AFirstReadingOfZero_StillCountsAsASample()
    {
        var state = new SkeletonBufferWatchState();

        state.ObservePool2(0);

        Assert.IsTrue(state.Pool2HasSamples);
    }

    [TestMethod]
    public void Pool2Percent_IsAgainstItsOwnCapacity()
    {
        var state = new SkeletonBufferWatchState();
        state.ObservePool2(131072);

        Assert.AreEqual(262144, SkeletonBufferWatchState.Pool2Capacity);
        Assert.AreEqual(50.0, state.Pool2PeakPercent, 1e-9);
    }

    [TestMethod]
    public void Pool1OverNinety_IsTrueFrom58983EntriesAndNotBefore()
    {
        var state = new SkeletonBufferWatchState();
        state.Observe(58982, 1.0, () => 1);
        Assert.IsFalse(state.Pool1OverNinety);

        state.Observe(58983, 2.0, () => 1);

        Assert.IsTrue(state.Pool1OverNinety);
    }

    [TestMethod]
    public void Pool2OverNinety_IsTrueFrom235930EntriesAndNotBefore()
    {
        var state = new SkeletonBufferWatchState();
        state.ObservePool2(235929);
        Assert.IsFalse(state.Pool2OverNinety);

        state.ObservePool2(235930);

        Assert.IsTrue(state.Pool2OverNinety);
    }

    [TestMethod]
    public void TakeWarning_ReadsPool1OnlyAndIgnoresAFullPool2()
    {
        var state = new SkeletonBufferWatchState();
        state.Observe(100, 1.0, () => 1);
        state.ObservePool2(262000);

        Assert.IsFalse(state.TakeWarning(guarded: false));
    }
}
