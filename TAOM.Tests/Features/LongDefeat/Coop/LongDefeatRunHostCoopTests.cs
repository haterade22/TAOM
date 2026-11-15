using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Core.Logging;
using TAOM.Features.AdvancedCombat;
using TAOM.Features.LongDefeat;
using TAOM.Features.LongDefeat.Coop;
using TAOM.Features.LongDefeat.Domain;

namespace TAOM.Tests.Features.LongDefeat.Coop;

/// <summary>A co-op run (#1280) waits for Coop's host election: the elected host runs the director, a follower spawns only its lord.</summary>
[TestClass]
public class LongDefeatRunHostCoopTests
{
    private static readonly RunPlan Plan = new("helms", new[] { new GarrisonRow("t", FormationSlot.Infantry, 1) },
        new WavePlan(new[] { new WaveSpec(110, new[] { new WaveTroop("a", FormationSlot.Infantry, 1) }) }, new WaveScaling(10, 300)),
        new ShopPlan(0, Array.Empty<ShopOffer>()), RunRules.Default, RunOptions.Default);

    private readonly FakeLongDefeatField _field = new();
    private readonly FakeLongDefeatMenu _menu = new();
    private readonly LongDefeatSession _session = new();
    private readonly List<RunResult> _ended = new();
    private LongDefeatCoopRole _role = LongDefeatCoopRole.Pending;
    private int _becameHost;
    private int _missionEnded;

    [TestInitialize]
    public void Init() => MissionThreadGuard.ResetForTests();

    [TestCleanup]
    public void Clean() => MissionThreadGuard.ResetForTests();

    private LongDefeatRunHost NewHost(RunPlan? plan = null) =>
        new(plan ?? Plan, _session, Substitute.For<IModLogger>(), new RecordingOutcomeLog(), _menu.EndGame, "m1", () => _field,
            coop: new LongDefeatRunCoop(() => _role, () => _becameHost++, result => _ended.Add(result), () => _missionEnded++));

    [TestMethod]
    public void BeforeTheHostIsElected_NothingStarts()
    {
        var host = NewHost();
        host.MarkStartDue();

        host.Step(0.1f, () => 1f);
        host.Step(0.1f, () => 2f);

        Assert.AreEqual(0, _field.Calls.Count);
        Assert.AreEqual(0, _menu.EndGames);
    }

    [TestMethod]
    public void TheElectedHost_RunsTheDirector()
    {
        var host = NewHost();
        host.MarkStartDue();
        host.Step(0.1f, () => 1f);

        _role = LongDefeatCoopRole.Host;
        host.Step(0.1f, () => 1.1f);

        Assert.AreEqual(1, _field.Count("Boot:"));
        Assert.AreEqual(1, _field.Count("BreakGates"));
        Assert.AreEqual(1, _becameHost);
        Assert.IsTrue(_session.IsActive);
    }

    // A host at 4x and a follower at 1x would play two different fights.
    [TestMethod]
    public void TheElectedHost_KeepsTheSpeedAtOne()
    {
        _role = LongDefeatCoopRole.Host;
        var host = NewHost();
        host.MarkStartDue();
        host.Step(0.1f, () => 1f);

        _session.SetSpeed(4);
        _session.CycleSpeed();

        Assert.AreEqual(1, _session.Speed);
        Assert.AreEqual(1, _session.MaxSpeed, "the report must not claim a speed the run never ran at");
    }

    // The end panel, the escape menu and a retreat end the mission without the director's own end; the host still tells the others.
    [TestMethod]
    public void TheHostsMission_EndingEarly_ReportsTheAbandonedRun_ThenEndsTheCoopSide()
    {
        _role = LongDefeatCoopRole.Host;
        var host = NewHost();
        host.MarkStartDue();
        host.Step(0.1f, () => 1f);

        host.ShutDown();

        Assert.AreEqual(RunEndReason.Abandoned, _ended.Single().Reason);
        Assert.AreEqual(1, _missionEnded);
    }

    [TestMethod]
    public void AFollowersMission_Ending_EndsTheCoopSide_AndReportsNothing()
    {
        _role = LongDefeatCoopRole.Follower;
        var host = NewHost();
        host.MarkStartDue();
        host.Step(0.1f, () => 1f);

        host.ShutDown();

        Assert.AreEqual(0, _ended.Count);
        Assert.AreEqual(1, _missionEnded);
    }

    [TestMethod]
    public void AFollower_SpawnsOnlyItsLord_InItsOwnBatch()
    {
        _role = LongDefeatCoopRole.Follower;
        var host = NewHost();
        host.MarkStartDue();

        host.Step(0.1f, () => 1f);
        host.Step(30f, () => 31f);

        CollectionAssert.AreEqual(new[] { "Boot:2/0/" + Attackers(), "SpawnPlayer:True", "Finish" }, _field.Calls);
        Assert.IsFalse(_session.IsActive, "a follower has no HUD or shop");
        Assert.AreEqual(0, _becameHost);
    }

    [TestMethod]
    public void AFollower_WithAMountedClass_SpawnsOnFoot()
    {
        _role = LongDefeatCoopRole.Follower;
        var host = NewHost(Plan with { Loadout = new LdLoadout("cavalry", true, Array.Empty<LdKitSlot>()) });
        host.MarkStartDue();

        host.Step(0.1f, () => 1f);

        Assert.AreEqual(1, _field.Count("SpawnPlayer:True"));
        Assert.AreEqual(0, _field.Count("SpawnPlayer:True:cavalry"));
    }

    [TestMethod]
    public void AFollowerWhoseLordCannotSpawn_LeavesTheRun()
    {
        _role = LongDefeatCoopRole.Follower;
        _field.SpawnPlayerOk = false;
        var host = NewHost();
        host.MarkStartDue();

        host.Step(0.1f, () => 1f);

        Assert.AreEqual(1, _menu.EndGames);
        Assert.AreEqual("FollowerStart", _session.Fault);
    }

    // None means the co-op side already ended this game (Coop's end event, a promoted follower); a second end must not follow.
    [TestMethod]
    public void ARunWhoseSessionIsGone_StartsNothing_AndEndsNothing()
    {
        _role = LongDefeatCoopRole.None;
        var host = NewHost();
        host.MarkStartDue();

        host.Step(0.1f, () => 1f);
        host.Step(0.1f, () => 1.1f);

        Assert.AreEqual(0, _field.Calls.Count);
        Assert.AreEqual(0, _menu.EndGames);
    }

    [TestMethod]
    public void NoHostElected_JustBeforeTheLimit_StillWaits()
    {
        var host = NewHost();
        host.MarkStartDue();

        host.Step(LongDefeatRunHost.NoHostLimitSeconds - 0.5f, () => 1f);

        Assert.AreEqual(0, _menu.EndGames);
        Assert.IsNull(_session.Fault);
    }

    [TestMethod]
    public void NoHostElected_InTheWaitLimit_LeavesTheRun()
    {
        var host = NewHost();
        host.MarkStartDue();

        host.Step(1f, () => 1f);
        host.Step(LongDefeatRunHost.NoHostLimitSeconds, () => 2f);

        Assert.AreEqual(1, _menu.EndGames);
        Assert.AreEqual("NoHost", _session.Fault);
        Assert.AreEqual(0, _field.Calls.Count);
    }

    [TestMethod]
    public void TheHostsRunEnd_IsReportedOnce()
    {
        _role = LongDefeatCoopRole.Host;
        var host = NewHost();
        host.MarkStartDue();
        host.Step(0.1f, () => 1f);
        _field.AttackersStanding = 30;
        _field.Defenders = 1;

        for (int i = 0; i < 200 && _session.Result == null; i++)
            host.Step(30f, () => 100f);
        host.Step(30f, () => 100f);

        Assert.IsNotNull(_session.Result, "the run should end");
        Assert.AreEqual(_session.Result, _ended.Single());
    }

    private static string Attackers()
    {
        var ranged = RunRules.Default.LiveCap / 4;
        return (RunRules.Default.LiveCap - ranged) + "/" + ranged;
    }
}
