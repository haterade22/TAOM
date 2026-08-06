using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.HeroRace.Diagnostics;

namespace TAOM.Tests.Features.HeroRace;

/// <summary>
/// Pins the trigger logic behind the #389 render-census instrumentation.
///
/// The tracker decides WHEN a character tableau is worth dumping a mesh/material census for — not
/// what is wrong with it. Vanilla <c>CharacterTableau.OnTick</c> shows a visual only once BOTH
/// <c>_agentVisualLoadingCounter</c> and <c>_mountVisualLoadingCounter</c> clear, so that moment is
/// exactly when the meshes and materials exist and are worth reading.
/// </summary>
[TestClass]
public class TableauResidencyTrackerTests
{
    private const string Uruk = "A1B2C3D4/urukhai_fighter";
    private const string Orc = "E5F6A7B8/isengard_orc_ravager";

    private TableauResidencyTracker _sut;

    [TestInitialize]
    public void Setup()
    {
        _sut = new TableauResidencyTracker(stuckThresholdTicks: 5, maxTrackedCharacters: 3);
    }

    /// <summary>
    /// THE sentinel-collision guard (`.claude/rules/harmony-patches.md`). The loading counter reads 0
    /// both BEFORE a refresh has armed it and AFTER loading completes. Reporting on the first kind
    /// would describe an entity with nothing attached AND burn the character's single report.
    /// </summary>
    [TestMethod]
    public void Observe_CountersClearButLoadingNeverObserved_ReturnsNone()
    {
        for (int i = 0; i < 10; i++)
        {
            var result = _sut.Observe(Uruk, agentVisualLoadingCounter: 0, mountVisualLoadingCounter: 0);
            Assert.AreEqual(TableauResidencyVerdict.None, result.Verdict, $"tick {i + 1}");
        }
    }

    [TestMethod]
    public void Observe_LoadingThenCountersClear_ReturnsReadyWithTickCount()
    {
        _sut.Observe(Uruk, 1, 0);
        _sut.Observe(Uruk, 1, 0);

        var result = _sut.Observe(Uruk, agentVisualLoadingCounter: 0, mountVisualLoadingCounter: 0);

        Assert.AreEqual(TableauResidencyVerdict.Ready, result.Verdict);
        Assert.AreEqual(3, result.Ticks);
    }

    [TestMethod]
    public void Observe_StillLoadingBelowThreshold_ReturnsNone()
    {
        for (int i = 0; i < 4; i++)
        {
            Assert.AreEqual(TableauResidencyVerdict.None, _sut.Observe(Uruk, 1, 0).Verdict, $"tick {i + 1}");
        }
    }

    [TestMethod]
    public void Observe_StillLoadingAtThreshold_ReturnsTimeoutExactlyAtThreshold()
    {
        for (int i = 0; i < 4; i++) _sut.Observe(Uruk, 1, 0);

        var result = _sut.Observe(Uruk, 1, 0);

        Assert.AreEqual(TableauResidencyVerdict.Timeout, result.Verdict);
        Assert.AreEqual(5, result.Ticks);
    }

    /// <summary>
    /// Vanilla gates the visibility swap on BOTH counters. A mounted troop (the Isengard warg riders
    /// are race=uruk_hai) whose mount is still streaming is not ready, and reporting it as such would
    /// describe a half-built entity.
    /// </summary>
    [TestMethod]
    public void Observe_AgentClearButMountStillLoading_DoesNotReportReady()
    {
        _sut.Observe(Uruk, 1, 1);

        var result = _sut.Observe(Uruk, agentVisualLoadingCounter: 0, mountVisualLoadingCounter: 1);

        Assert.AreEqual(TableauResidencyVerdict.None, result.Verdict);

        var afterMountLoads = _sut.Observe(Uruk, 0, 0);
        Assert.AreEqual(TableauResidencyVerdict.Ready, afterMountLoads.Verdict);
        Assert.AreEqual(3, afterMountLoads.Ticks);
    }

    [TestMethod]
    public void Observe_AfterReadyVerdict_ReturnsNoneForever()
    {
        _sut.Observe(Uruk, 1, 0);
        Assert.AreEqual(TableauResidencyVerdict.Ready, _sut.Observe(Uruk, 0, 0).Verdict);

        for (int i = 0; i < 20; i++)
        {
            Assert.AreEqual(TableauResidencyVerdict.None, _sut.Observe(Uruk, 0, 0).Verdict, $"tick {i}");
        }
    }

    [TestMethod]
    public void Observe_AfterTimeoutVerdict_ReturnsNoneForever()
    {
        for (int i = 0; i < 5; i++) _sut.Observe(Uruk, 1, 0);

        for (int i = 0; i < 20; i++)
        {
            Assert.AreEqual(TableauResidencyVerdict.None, _sut.Observe(Uruk, 1, 0).Verdict, $"tick {i}");
        }
    }

    [TestMethod]
    public void Observe_DistinctKeys_TrackedIndependently()
    {
        for (int i = 0; i < 4; i++) _sut.Observe(Uruk, 1, 0);

        _sut.Observe(Orc, 1, 0);
        var orcResult = _sut.Observe(Orc, 0, 0);
        var urukResult = _sut.Observe(Uruk, 1, 0);

        Assert.AreEqual(TableauResidencyVerdict.Ready, orcResult.Verdict);
        Assert.AreEqual(2, orcResult.Ticks);
        Assert.AreEqual(TableauResidencyVerdict.Timeout, urukResult.Verdict);
        Assert.AreEqual(5, urukResult.Ticks);
    }

    [TestMethod]
    public void Observe_NullKey_ReturnsNoneAndTracksNothing()
    {
        Assert.AreEqual(TableauResidencyVerdict.None, _sut.Observe(null, 1, 0).Verdict);
        Assert.AreEqual(0, _sut.TrackedCount);
    }

    [TestMethod]
    public void Observe_EmptyKey_ReturnsNoneAndTracksNothing()
    {
        Assert.AreEqual(TableauResidencyVerdict.None, _sut.Observe("   ", 1, 0).Verdict);
        Assert.AreEqual(0, _sut.TrackedCount);
    }

    [TestMethod]
    public void Observe_NegativeCounters_TreatedAsClearNotLoading()
    {
        _sut.Observe(Uruk, 1, 0);

        var result = _sut.Observe(Uruk, agentVisualLoadingCounter: -1, mountVisualLoadingCounter: -1);

        Assert.AreEqual(TableauResidencyVerdict.Ready, result.Verdict);
    }

    [TestMethod]
    public void Forget_KnownKey_AllowsTheCharacterToReportAgain()
    {
        _sut.Observe(Uruk, 1, 0);
        _sut.Observe(Uruk, 0, 0);
        Assert.AreEqual(TableauResidencyVerdict.None, _sut.Observe(Uruk, 0, 0).Verdict);

        _sut.Forget(Uruk);

        _sut.Observe(Uruk, 1, 0);
        Assert.AreEqual(TableauResidencyVerdict.Ready, _sut.Observe(Uruk, 0, 0).Verdict);
    }

    [TestMethod]
    public void Forget_UnknownKeyOrNull_DoesNotThrow()
    {
        _sut.Forget("never-seen");
        _sut.Forget(null);

        Assert.AreEqual(0, _sut.TrackedCount);
    }

    /// <summary>
    /// Silence must never be mistakable for a clean result: the registry tells the reader to compare
    /// troops, so a character dropped for capacity has to announce itself.
    /// </summary>
    [TestMethod]
    public void Observe_FirstNewKeyBeyondCapacity_ReportsCapacityReachedOnceThenGoesSilent()
    {
        _sut.Observe("a", 1, 0);
        _sut.Observe("b", 1, 0);
        _sut.Observe("c", 1, 0);

        Assert.AreEqual(TableauResidencyVerdict.CapacityReached, _sut.Observe("d", 1, 0).Verdict);
        Assert.AreEqual(TableauResidencyVerdict.None, _sut.Observe("e", 1, 0).Verdict);
        Assert.AreEqual(TableauResidencyVerdict.None, _sut.Observe("d", 1, 0).Verdict);
        Assert.AreEqual(3, _sut.TrackedCount);
    }

    [TestMethod]
    public void Observe_ExistingKeyWhileAtCapacity_StillProgressesToAVerdict()
    {
        _sut.Observe("a", 1, 0);
        _sut.Observe("b", 1, 0);
        _sut.Observe("c", 1, 0);

        TableauResidencyResult last = default;
        for (int i = 0; i < 4; i++) last = _sut.Observe("a", 1, 0);

        Assert.AreEqual(TableauResidencyVerdict.Timeout, last.Verdict);
        Assert.AreEqual(5, last.Ticks);
    }

    [TestMethod]
    public void Constructor_NonPositiveThreshold_FallsBackToDefault()
    {
        var tracker = new TableauResidencyTracker(stuckThresholdTicks: 0, maxTrackedCharacters: 8);

        Assert.AreEqual(TableauResidencyTracker.DefaultStuckThresholdTicks, tracker.StuckThresholdTicks);
    }

    [TestMethod]
    public void Constructor_NonPositiveCapacity_FallsBackToDefault()
    {
        var tracker = new TableauResidencyTracker(stuckThresholdTicks: 5, maxTrackedCharacters: -3);

        Assert.AreEqual(TableauResidencyTracker.DefaultMaxTrackedCharacters, tracker.MaxTrackedCharacters);
    }
}
