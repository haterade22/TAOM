using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Core.Logging;
using TAOM.Features.MountDespawn;

namespace TAOM.Tests.Features.MountDespawn;

/// <summary>
/// The scheduling half of dead-mount despawn. Everything that decides WHEN a killed mount is
/// retired lives here, so it is testable without a live <c>Mission</c>; the MissionBehavior owns
/// the engine <c>Agent</c> handles and does nothing but record, sweep and fade.
/// </summary>
[TestClass]
public class DeadMountDespawnServiceTests
{
    private IMountDespawnSettingsProvider _settings = null!;
    private IModLogger _logger = null!;
    private DeadMountDespawnService _sut = null!;

    [TestInitialize]
    public void Setup()
    {
        _settings = Substitute.For<IMountDespawnSettingsProvider>();
        _logger = Substitute.For<IModLogger>();
        _settings.IsEnabled.Returns(true);
        _settings.DespawnDelaySeconds.Returns(5f);

        _sut = new DeadMountDespawnService(_settings, _logger);
    }

    // -------- Timing --------

    [TestMethod]
    public void CollectDue_BeforeDelayElapses_ReturnsEmpty()
    {
        _sut.OnMountKilled(agentIndex: 42, missionTime: 0f);

        var due = _sut.CollectDue(missionTime: 4f);

        Assert.AreEqual(0, due.Count);
        Assert.AreEqual(1, _sut.PendingCount, "an entry that is not yet due must stay scheduled");
    }

    [TestMethod]
    public void CollectDue_ExactlyAtDelay_ReturnsIndex()
    {
        _sut.OnMountKilled(agentIndex: 42, missionTime: 0f);

        var due = _sut.CollectDue(missionTime: 5f);

        CollectionAssert.AreEqual(new[] { 42 }, due.ToArray());
    }

    [TestMethod]
    public void CollectDue_AfterDelay_ReturnsIndexAndDropsIt()
    {
        _sut.OnMountKilled(agentIndex: 42, missionTime: 10f);

        Assert.AreEqual(1, _sut.CollectDue(missionTime: 20f).Count);

        Assert.AreEqual(0, _sut.PendingCount);
        Assert.AreEqual(0, _sut.CollectDue(missionTime: 30f).Count,
            "a retired mount must never be handed out twice — the second FadeOut would touch a cleared agent");
    }

    // -------- Skip guards --------

    [TestMethod]
    public void Forget_BeforeDelayElapses_NeverBecomesDue()
    {
        _sut.OnMountKilled(agentIndex: 42, missionTime: 0f);

        _sut.Forget(42);

        Assert.AreEqual(0, _sut.PendingCount);
        Assert.AreEqual(0, _sut.CollectDue(missionTime: 60f).Count);
    }

    [TestMethod]
    public void Forget_UnknownIndex_DoesNotThrow()
    {
        _sut.Forget(999);

        Assert.AreEqual(0, _sut.PendingCount);
    }

    [TestMethod]
    public void OnMountKilled_FeatureDisabled_RecordsNothing()
    {
        _settings.IsEnabled.Returns(false);

        _sut.OnMountKilled(agentIndex: 42, missionTime: 0f);

        Assert.AreEqual(0, _sut.PendingCount);
    }

    [TestMethod]
    public void CollectDue_DisabledAfterRecording_ReturnsEmpty()
    {
        // Mid-battle MCM toggle-off. The schedule survives so re-enabling resumes, but nothing fades
        // while the player has the feature switched off.
        _sut.OnMountKilled(agentIndex: 42, missionTime: 0f);
        _settings.IsEnabled.Returns(false);

        Assert.AreEqual(0, _sut.CollectDue(missionTime: 60f).Count);
        Assert.AreEqual(1, _sut.PendingCount);
    }

    // -------- Per-sweep budget --------

    [TestMethod]
    public void CollectDue_MoreDueThanBudget_CapsAtBudget()
    {
        for (var i = 0; i < DeadMountDespawnService.MaxFadesPerSweep + 3; i++)
            _sut.OnMountKilled(agentIndex: i, missionTime: 0f);

        var first = _sut.CollectDue(missionTime: 60f);

        Assert.AreEqual(DeadMountDespawnService.MaxFadesPerSweep, first.Count,
            "a mass-casualty moment must not fade every corpse in one frame");
        Assert.AreEqual(3, _sut.PendingCount);
    }

    [TestMethod]
    public void CollectDue_RemainderComesBackOnNextSweep()
    {
        for (var i = 0; i < DeadMountDespawnService.MaxFadesPerSweep + 3; i++)
            _sut.OnMountKilled(agentIndex: i, missionTime: 0f);

        var firstSweep = _sut.CollectDue(missionTime: 60f).ToArray();
        var secondSweep = _sut.CollectDue(missionTime: 60f).ToArray();

        Assert.AreEqual(3, secondSweep.Length);
        Assert.AreEqual(0, firstSweep.Intersect(secondSweep).Count(), "sweeps must not overlap");
        Assert.AreEqual(0, _sut.PendingCount);
    }

    // -------- Session lifetime (the service is Reuse.Singleton, so it outlives the mission) --------

    [TestMethod]
    public void OnMissionEnd_ClearsSchedule_SecondMissionDoesNotInherit()
    {
        _sut.OnMountKilled(agentIndex: 42, missionTime: 100f);

        _sut.OnMissionEnd();

        Assert.AreEqual(0, _sut.PendingCount);
        Assert.AreEqual(0, _sut.CollectDue(missionTime: 1f).Count,
            "mission time restarts near zero, so a stale entry from the previous battle would fade " +
            "an agent index belonging to a completely different agent");
    }

    // -------- Non-finite input (the NaN-gate class, five prior shipped instances) --------

    [TestMethod]
    public void OnMountKilled_NaNMissionTime_RecordsNothing()
    {
        _sut.OnMountKilled(agentIndex: 42, missionTime: float.NaN);

        Assert.AreEqual(0, _sut.PendingCount);
    }

    [TestMethod]
    public void CollectDue_NaNMissionTime_ReturnsEmpty()
    {
        _sut.OnMountKilled(agentIndex: 42, missionTime: 0f);

        Assert.AreEqual(0, _sut.CollectDue(missionTime: float.NaN).Count);
        Assert.AreEqual(1, _sut.PendingCount);
    }

    [TestMethod]
    public void CollectDue_InfiniteMissionTime_ReturnsEmpty()
    {
        _sut.OnMountKilled(agentIndex: 42, missionTime: 0f);

        Assert.AreEqual(0, _sut.CollectDue(missionTime: float.PositiveInfinity).Count);
    }

    // -------- Delay clamp (one chokepoint, so a bad MCM value cannot reach the gate) --------

    [TestMethod]
    public void CollectDue_DelayIsNaN_FallsBackToDefault()
    {
        _settings.DespawnDelaySeconds.Returns(float.NaN);
        _sut.OnMountKilled(agentIndex: 42, missionTime: 0f);

        Assert.AreEqual(0, _sut.CollectDue(missionTime: 4f).Count);
        Assert.AreEqual(1, _sut.CollectDue(missionTime: DeadMountDespawnService.DefaultDelaySeconds).Count);
    }

    [TestMethod]
    public void CollectDue_DelayBelowMinimum_FallsBackToDefault()
    {
        _settings.DespawnDelaySeconds.Returns(0.1f);
        _sut.OnMountKilled(agentIndex: 42, missionTime: 0f);

        Assert.AreEqual(0, _sut.CollectDue(missionTime: 1f).Count,
            "a sub-minimum delay would pop the corpse mid death-animation");
        Assert.AreEqual(1, _sut.CollectDue(missionTime: DeadMountDespawnService.DefaultDelaySeconds).Count);
    }

    [TestMethod]
    public void CollectDue_DelayAboveMaximum_FallsBackToDefault()
    {
        _settings.DespawnDelaySeconds.Returns(9000f);
        _sut.OnMountKilled(agentIndex: 42, missionTime: 0f);

        Assert.AreEqual(1, _sut.CollectDue(missionTime: DeadMountDespawnService.DefaultDelaySeconds).Count);
    }

    [TestMethod]
    public void CollectDue_InvalidDelay_WarnsOncePerMission()
    {
        // The sweep runs twice a second. A hand-edited json2 value must say so in the log, and must
        // say it once rather than filling the file.
        _settings.DespawnDelaySeconds.Returns(9000f);
        _sut.OnMountKilled(agentIndex: 42, missionTime: 0f);

        _sut.CollectDue(missionTime: 1f);
        _sut.CollectDue(missionTime: 2f);
        _sut.CollectDue(missionTime: 3f);

        _logger.Received(1).LogWarning(Arg.Is<string>(m => m.Contains("[MountDespawn]")));

        // A fresh mission is allowed to say it again.
        _sut.OnMissionEnd();
        _sut.OnMountKilled(agentIndex: 7, missionTime: 0f);
        _sut.CollectDue(missionTime: 1f);

        _logger.Received(2).LogWarning(Arg.Is<string>(m => m.Contains("[MountDespawn]")));
    }

    [TestMethod]
    public void CollectDue_ValidDelay_NeverWarns()
    {
        _sut.OnMountKilled(agentIndex: 42, missionTime: 0f);

        _sut.CollectDue(missionTime: 10f);

        _logger.DidNotReceive().LogWarning(Arg.Any<string>());
    }

    [TestMethod]
    public void CollectDue_MissionTimeBeforeDeathTime_ReturnsEmpty()
    {
        // Negative elapsed. Reachable if an entry ever outlives its mission, since the next battle's
        // clock restarts near zero. It must read as "not due", never as "overdue".
        _sut.OnMountKilled(agentIndex: 42, missionTime: 500f);

        Assert.AreEqual(0, _sut.CollectDue(missionTime: 1f).Count);
        Assert.AreEqual(1, _sut.PendingCount);
    }

    [TestMethod]
    public void Ctor_NullDependency_Throws()
    {
        Assert.ThrowsException<System.ArgumentNullException>(
            () => new DeadMountDespawnService(null!, _logger));
        Assert.ThrowsException<System.ArgumentNullException>(
            () => new DeadMountDespawnService(_settings, null!));
    }

    [TestMethod]
    public void IsEnabled_MirrorsSettingsProvider()
    {
        _settings.IsEnabled.Returns(false);
        Assert.IsFalse(_sut.IsEnabled);

        _settings.IsEnabled.Returns(true);
        Assert.IsTrue(_sut.IsEnabled);
    }
}
