// Adapted from yotthani's VanillaTuning (MIT, (c) 2026 yotthani), frame-buffer-guard and frame-buffer-watch.
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.SkeletonBuffer;

namespace TAOM.Tests.Features.SkeletonBuffer;

/// <summary>The exact text of every <c>[SkeletonBuffer]</c> log line.</summary>
[TestClass]
public class SkeletonBufferLinesTests
{
    [TestMethod]
    public void Lines_GuardOn_ListsTheConfigurationOfEachPool()
    {
        Assert.AreEqual(
            "[SkeletonBuffer] guard ON pool 1: site=0x69D14 resume=0x69D21 cave=0x7FF5FFF00000 counter=0x7FF5FFF01000 scanMs=12.3",
            SkeletonBufferLines.GuardOn(1, 0x69D14, 0x69D21, 0x7FF5FFF00000, 0x7FF5FFF01000, 12.34));
        Assert.AreEqual(
            "[SkeletonBuffer] guard ON pool 2: site=0x6AF50 resume=0x6AF5D cave=0x7FF5FFF00040 counter=0x7FF5FFF01040 scanMs=12.3",
            SkeletonBufferLines.GuardOn(2, 0x6AF50, 0x6AF5D, 0x7FF5FFF00040, 0x7FF5FFF01040, 12.34));
    }

    [TestMethod]
    public void Lines_GuardOffPool_NamesThePoolAndTheReason()
    {
        Assert.AreEqual("[SkeletonBuffer] guard OFF pool 2: the guard signature matched nothing in .text",
            SkeletonBufferLines.GuardOffPool(2, "the guard signature matched nothing in .text"));
    }

    [TestMethod]
    public void Lines_GuardOff_NamesTheReason()
    {
        Assert.AreEqual("[SkeletonBuffer] guard OFF: setting off (MCM Skeleton Buffer Guard)",
            SkeletonBufferLines.GuardOff("setting off (MCM Skeleton Buffer Guard)"));
    }

    [TestMethod]
    public void Lines_Peak_WithAGuard_ListsEveryFigure()
    {
        var state = new SkeletonBufferWatchState();
        state.Observe(41230, 38.24, () => 1650);

        Assert.AreEqual(
            "[SkeletonBuffer] mission peak: 41230 of 65536 entries (62.9 %), about 1472 skeletons, 1650 agents, 38.2 s into the mission; guard overflows this mission: 0",
            SkeletonBufferLines.Peak(state, SkeletonBufferLines.GuardOverflows(0), null));
    }

    [TestMethod]
    public void Lines_Peak_WithoutAGuard_SaysSo()
    {
        var state = new SkeletonBufferWatchState();
        state.Observe(100, 1.0, () => 5);

        StringAssert.EndsWith(SkeletonBufferLines.Peak(state, SkeletonBufferLines.NoGuard, null),
            "; no guard installed");
    }

    [TestMethod]
    public void Lines_Peak_UsesInvariantCultureForDecimals()
    {
        var previous = System.Threading.Thread.CurrentThread.CurrentCulture;
        try
        {
            System.Threading.Thread.CurrentThread.CurrentCulture = new System.Globalization.CultureInfo("de-DE");
            var state = new SkeletonBufferWatchState();
            state.Observe(41230, 38.24, () => 1650);

            StringAssert.Contains(SkeletonBufferLines.Peak(state, SkeletonBufferLines.NoGuard, null), "(62.9 %)");
            StringAssert.Contains(SkeletonBufferLines.Peak(state, SkeletonBufferLines.NoGuard, null), "38.2 s");
        }
        finally
        {
            System.Threading.Thread.CurrentThread.CurrentCulture = previous;
        }
    }

    [TestMethod]
    public void Lines_NoReadings_SaysNothingWasRead()
    {
        Assert.AreEqual("[SkeletonBuffer] mission ended with no readings of the skeleton buffer",
            SkeletonBufferLines.NoReadings);
    }

    [TestMethod]
    public void Lines_ForeignGuardNote_NamesAnotherModule()
    {
        Assert.AreEqual("guarded by another module", SkeletonBufferLines.ForeignGuard);
    }

    [TestMethod]
    public void Lines_Peak_WithPool2_NamesBothPeaksWithTheirCapacities()
    {
        var state = new SkeletonBufferWatchState();
        state.Observe(41230, 38.24, () => 1650);
        state.ObservePool2(70000);

        Assert.AreEqual(
            "[SkeletonBuffer] mission peak: 41230 of 65536 entries (62.9 %), about 1472 skeletons, 1650 agents, 38.2 s into the mission; guard overflows this mission: 0; "
            + "pool 2 peak: 70000 of 262144 entries (26.7 %); guard overflows this mission: 0",
            SkeletonBufferLines.Peak(state, SkeletonBufferLines.GuardOverflows(0), SkeletonBufferLines.GuardOverflows(0)));
    }

    [TestMethod]
    public void Lines_Peak_WithPool2_PutsEachPoolsGuardNoteNextToItsPeak()
    {
        var state = new SkeletonBufferWatchState();
        state.Observe(65000, 38.24, () => 1650);
        state.ObservePool2(70000);

        Assert.AreEqual(
            "[SkeletonBuffer] mission peak: 65000 of 65536 entries (99.2 %), over 90 %, about 2321 skeletons, 1650 agents, 38.2 s into the mission; guard overflows this mission: 2; "
            + "pool 2 peak: 70000 of 262144 entries (26.7 %); guard overflows this mission: 5",
            SkeletonBufferLines.Peak(state, SkeletonBufferLines.GuardOverflows(2), SkeletonBufferLines.GuardOverflows(5)));
    }

    [TestMethod]
    public void Lines_Peak_Pool1OverNinety_SaysSoAfterItsPercentage()
    {
        var state = new SkeletonBufferWatchState();
        state.Observe(58983, 1.0, () => 5);

        StringAssert.Contains(SkeletonBufferLines.Peak(state, SkeletonBufferLines.NoGuard, null),
            "58983 of 65536 entries (90.0 %), over 90 %, about 2106 skeletons");
    }

    [TestMethod]
    public void Lines_Peak_Pool1JustBelowNinety_HasNoMarker()
    {
        var state = new SkeletonBufferWatchState();
        state.Observe(58982, 1.0, () => 5);

        Assert.IsFalse(SkeletonBufferLines.Peak(state, SkeletonBufferLines.NoGuard, null).Contains("over 90 %"));
    }

    [TestMethod]
    public void Lines_Peak_Pool2WithoutReadings_HasNoPool2Clause()
    {
        var state = new SkeletonBufferWatchState();
        state.Observe(100, 1.0, () => 5);

        var line = SkeletonBufferLines.Peak(state, SkeletonBufferLines.NoGuard, SkeletonBufferLines.NoGuard);

        Assert.IsFalse(line.Contains("pool 2"));
        StringAssert.EndsWith(line, "; no guard installed");
    }

    [TestMethod]
    public void Lines_Peak_Pool2OverNinety_SaysSo()
    {
        var state = new SkeletonBufferWatchState();
        state.Observe(100, 1.0, () => 5);
        state.ObservePool2(250000);

        StringAssert.Contains(SkeletonBufferLines.Peak(state, SkeletonBufferLines.NoGuard, SkeletonBufferLines.NoGuard), "pool 2 peak: 250000 of 262144 entries (95.4 %), over 90 %; no guard installed");
    }

    [TestMethod]
    public void Lines_Pool2Off_SaysTheWatchReadsPool1Only()
    {
        Assert.AreEqual("[SkeletonBuffer] pool 2 watch OFF: the pool 2 signature matched nothing in .text; the watch reads pool 1 only",
            SkeletonBufferLines.Pool2Off("the pool 2 signature matched nothing in .text"));
    }

    [TestMethod]
    public void Lines_WatchFaults_SayWhatHappensNext()
    {
        Assert.AreEqual("[SkeletonBuffer] the watch could not start for this mission: IOException: gone",
            SkeletonBufferLines.WatchStartFault("IOException", "gone"));
        Assert.AreEqual("[SkeletonBuffer] one watch read failed (IOException: gone); the watch goes on, and this is the only read failure logged for this mission",
            SkeletonBufferLines.WatchReadFault("IOException", "gone"));
        Assert.AreEqual("[SkeletonBuffer] the mission-end peak line could not be written: IOException: gone",
            SkeletonBufferLines.WatchEndFault("IOException", "gone"));
    }
}
