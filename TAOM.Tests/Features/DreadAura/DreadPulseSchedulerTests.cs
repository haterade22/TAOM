using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.DreadAura.Hooks;

namespace TAOM.Tests.Features.DreadAura;

/// <summary>
/// The round-robin that keeps a field full of wraiths from spiking one frame with proximity
/// queries. The scheduler never dereferences the Agent on a source, so these run with null agents.
/// </summary>
[TestClass]
public class DreadPulseSchedulerTests
{
    private DreadPulseScheduler _sut = null!;
    private List<DreadPulseScheduler.DuePulse> _due = null!;

    [TestInitialize]
    public void Setup()
    {
        _sut = new DreadPulseScheduler();
        _due = new List<DreadPulseScheduler.DuePulse>();
    }

    private static List<DreadSourceTracker.DreadSource> Sources(params float[] lastPulseTimes)
        => lastPulseTimes
            .Select(t => new DreadSourceTracker.DreadSource(null, t))
            .ToList();

    [TestMethod]
    public void SelectDue_NoSources_SelectsNothing()
    {
        _sut.SelectDue(Sources(), now: 10f, interval: 0.25f, budget: 2, _due);

        Assert.AreEqual(0, _due.Count);
    }

    [TestMethod]
    public void SelectDue_NullList_SelectsNothing()
    {
        _sut.SelectDue(null, now: 10f, interval: 0.25f, budget: 2, _due);

        Assert.AreEqual(0, _due.Count);
    }

    [TestMethod]
    public void SelectDue_SourceNotYetDue_IsSkipped()
    {
        _sut.SelectDue(Sources(9.9f), now: 10f, interval: 0.25f, budget: 4, _due);

        Assert.AreEqual(0, _due.Count);
    }

    [TestMethod]
    public void SelectDue_SourceExactlyAtInterval_IsDue()
    {
        _sut.SelectDue(Sources(9.75f), now: 10f, interval: 0.25f, budget: 4, _due);

        Assert.AreEqual(1, _due.Count);
        Assert.AreEqual(0.25f, _due[0].Elapsed, 0.0001f);
    }

    [TestMethod]
    public void SelectDue_ReportsElapsedSinceThatSourceLastPulsed_NotSinceTheLastFrame()
    {
        // The property that keeps the drain rate-exact while sources are round-robined. 0.6s is
        // a realistic frame-scheduling gap and sits under the catch-up ceiling, so it passes
        // through exactly.
        _sut.SelectDue(Sources(9.4f), now: 10f, interval: 0.25f, budget: 4, _due);

        Assert.AreEqual(0.6f, _due[0].Elapsed, 0.0001f);
    }

    // ---- Catch-up bound (deep-review 2026-08-13 finding 1, HIGH) ------------

    [TestMethod]
    public void SelectDue_LongSkippedWindow_ClampsElapsedToTheCatchUpCeiling()
    {
        // A source last pulsed 40 seconds ago because the player toggled the aura off and back
        // on. Without the ceiling the whole 40s is delivered as one pulse, which at the shipped
        // rate strips every enemy in radius from full morale to zero in a single frame.
        _sut.SelectDue(Sources(0f), now: 40f, interval: 0.25f, budget: 4, _due);

        Assert.AreEqual(1, _due.Count);
        Assert.AreEqual(1f, _due[0].Elapsed, 0.0001f,
            "Elapsed must be clamped to the catch-up ceiling, not delivered as a 40-second burst.");
    }

    [TestMethod]
    public void SelectDue_ElapsedBelowCeiling_IsNotClamped()
    {
        _sut.SelectDue(Sources(9.7f), now: 10f, interval: 0.25f, budget: 4, _due);

        Assert.AreEqual(0.3f, _due[0].Elapsed, 0.0001f);
    }

    [TestMethod]
    public void SelectDue_IntervalLongerThanTheCeiling_StillDeliversOneWholeInterval()
    {
        // A 3s configured interval must not be clamped down to the 1s default ceiling, or the
        // drain would silently run at a third of the configured rate.
        _sut.SelectDue(Sources(0f), now: 100f, interval: 3f, budget: 4, _due);

        Assert.AreEqual(3f, _due[0].Elapsed, 0.0001f);
    }

    [TestMethod]
    public void SelectDue_MoreDueThanBudget_StopsAtBudget()
    {
        var sources = Sources(0f, 0f, 0f, 0f, 0f);

        _sut.SelectDue(sources, now: 10f, interval: 0.25f, budget: 2, _due);

        Assert.AreEqual(2, _due.Count);
    }

    [TestMethod]
    public void SelectDue_AcrossFrames_RotatesThroughEverySource()
    {
        // Budget 2 over 5 sources: nobody may starve. This is why the cursor persists.
        var sources = Sources(0f, 0f, 0f, 0f, 0f);
        var seen = new HashSet<int>();

        for (var frame = 0; frame < 3; frame++)
        {
            _sut.SelectDue(sources, now: 10f + frame, interval: 0.25f, budget: 2, _due);
            foreach (var due in _due)
                seen.Add(due.Index);
        }

        CollectionAssert.AreEquivalent(new[] { 0, 1, 2, 3, 4 }, seen.ToArray(),
            "Round-robin starved a source: every wraith must get its turn.");
    }

    [TestMethod]
    public void SelectDue_StampsSelectedSources_SoTheyAreNotPickedTwiceInAFrame()
    {
        var sources = Sources(0f, 0f);

        _sut.SelectDue(sources, now: 10f, interval: 0.25f, budget: 4, _due);
        var firstPass = _due.Count;

        _sut.SelectDue(sources, now: 10f, interval: 0.25f, budget: 4, _due);

        Assert.AreEqual(2, firstPass);
        Assert.AreEqual(0, _due.Count, "A source pulsed this frame must not be selected again.");
        Assert.IsTrue(sources.All(s => s.LastPulseTime == 10f));
    }

    [TestMethod]
    public void SelectDue_ClearsTheOutputList()
    {
        _due.Add(new DreadPulseScheduler.DuePulse(99, 1f));

        _sut.SelectDue(Sources(9.9f), now: 10f, interval: 0.25f, budget: 4, _due);

        Assert.AreEqual(0, _due.Count, "Stale entries would re-pulse a source that is not due.");
    }

    [TestMethod]
    public void SelectDue_NaNInterval_SelectsNothing()
    {
        // `elapsed >= NaN` is false, but so is every other comparison — write the gate as a
        // positive requirement and a NaN interval selects nothing rather than everything.
        _sut.SelectDue(Sources(0f, 0f, 0f), now: 10f, interval: float.NaN, budget: 4, _due);

        Assert.AreEqual(0, _due.Count);
    }

    [TestMethod]
    public void SelectDue_ZeroBudget_SelectsNothing()
    {
        _sut.SelectDue(Sources(0f, 0f), now: 10f, interval: 0.25f, budget: 0, _due);

        Assert.AreEqual(0, _due.Count);
    }

    [TestMethod]
    public void SelectDue_NullSourceEntry_IsSkippedWithoutThrowing()
    {
        var sources = Sources(0f, 0f);
        sources[0] = null;

        _sut.SelectDue(sources, now: 10f, interval: 0.25f, budget: 4, _due);

        Assert.AreEqual(1, _due.Count);
        Assert.AreEqual(1, _due[0].Index);
    }

    [TestMethod]
    public void Reset_RestartsTheRotationAtTheTop()
    {
        var sources = Sources(0f, 0f, 0f);
        _sut.SelectDue(sources, now: 10f, interval: 0.25f, budget: 1, _due);
        var firstIndex = _due[0].Index;

        _sut.Reset();
        foreach (var source in sources)
            source.LastPulseTime = 0f;
        _sut.SelectDue(sources, now: 10f, interval: 0.25f, budget: 1, _due);

        Assert.AreEqual(firstIndex, _due[0].Index,
            "After OnEndMission the cursor must restart, or the next mission inherits a stale offset.");
    }
}
