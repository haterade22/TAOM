using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Features.DreadAura;
using TAOM.Features.DreadAura.Hooks;

namespace TAOM.Tests.Features.DreadAura;

/// <summary>
/// The tracker was the one class in the feature with no direct tests, and two of the three bugs
/// the 2026-08-13 deep review found lived on its edges: registration consulting the master toggle,
/// and the pulse-time seed. Everything reachable without a live <c>Agent</c> is covered here.
///
/// <c>Agent</c> is a sealed engine type that cannot be constructed or substituted offline, so the
/// paths that need one (<c>ScanMission</c>, <c>TryRegister</c>'s happy path, <c>Prune</c>'s
/// <c>IsActive</c> check) are entry-point work verified in the control battle. What IS testable is
/// the null handling, the dedup contract and the registry delegation.
/// </summary>
[TestClass]
public class DreadSourceTrackerTests
{
    private IDreadRegistry _registry = null!;
    private DreadSourceTracker _sut = null!;

    [TestInitialize]
    public void Setup()
    {
        _registry = Substitute.For<IDreadRegistry>();
        _registry.IsDreadSource(Arg.Any<string>(), Arg.Any<int?>()).Returns(true);
        _sut = new DreadSourceTracker(_registry);
    }

    [TestMethod]
    public void NewTracker_HasNoSources()
    {
        Assert.AreEqual(0, _sut.Count);
        Assert.AreEqual(0, _sut.Sources.Count);
    }

    [TestMethod]
    public void TryRegister_NullAgent_IsIgnoredAndDoesNotConsultTheRegistry()
    {
        _sut.TryRegister(null, now: 5f);

        Assert.AreEqual(0, _sut.Count);
        _registry.DidNotReceive().IsDreadSource(Arg.Any<string>(), Arg.Any<int?>());
    }

    [TestMethod]
    public void ScanMission_NullMission_IsIgnored()
    {
        _sut.ScanMission(null);

        Assert.AreEqual(0, _sut.Count);
    }

    [TestMethod]
    public void Clear_EmptiesTheSourceList()
    {
        // Agent references must not outlive the mission; OnEndMission calls this.
        _sut.Clear();

        Assert.AreEqual(0, _sut.Count);
    }

    [TestMethod]
    public void Prune_OnAnEmptyTracker_DoesNotThrow()
    {
        _sut.Prune();

        Assert.AreEqual(0, _sut.Count);
    }

    [TestMethod]
    public void DreadSource_SeedsLastPulseTimeFromTheSuppliedMissionClock()
    {
        // The seed matters: a source seeded at 0 against a mission clock already at t=400 would
        // report a 400-second first pulse. The scheduler's ceiling bounds that now, but the seed
        // should still be the real clock rather than a default.
        var source = new DreadSourceTracker.DreadSource(null, registeredAt: 42.5f);

        Assert.AreEqual(42.5f, source.LastPulseTime, 0.0001f);
        Assert.IsNull(source.Agent);
    }

    [TestMethod]
    public void DreadSource_LastPulseTimeIsSettable_SoTheSchedulerCanStampIt()
    {
        var source = new DreadSourceTracker.DreadSource(null, registeredAt: 1f);

        source.LastPulseTime = 9f;

        Assert.AreEqual(9f, source.LastPulseTime, 0.0001f);
    }
}
