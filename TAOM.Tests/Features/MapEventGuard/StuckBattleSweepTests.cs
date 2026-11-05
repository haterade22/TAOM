using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using TAOM.Adapters;
using TAOM.Core.Logging;
using TAOM.Features.CoopInterop;
using TAOM.Features.Enlistment;
using TAOM.Features.MapEventGuard;
using static TAOM.Tests.Features.MapEventGuard.StuckBattleServiceTests;

namespace TAOM.Tests.Features.MapEventGuard;

/// <summary>
/// <see cref="StuckBattleService.Resolve"/> and <see cref="StuckBattleService.Describe"/>: one write per battle per
/// pass, the label read before that write, a battle the sweep leaves alone never named, one failing battle never
/// stopping the rest, and no sweep at all while a co-op session is live or the co-op mod cannot be probed.
/// </summary>
[TestClass]
public class StuckBattleSweepTests
{
    private IStuckBattleAdapter _adapter = null!;
    private ICoopSessionProvider _coop = null!;
    private IModLogger _logger = null!;
    private StuckBattleService _sut = null!;

    [TestInitialize]
    public void Setup()
    {
        _adapter = Substitute.For<IStuckBattleAdapter>();
        _coop = Substitute.For<ICoopSessionProvider>();
        _logger = Substitute.For<IModLogger>();
        _sut = new StuckBattleService(Substitute.For<IEnlistmentStateQuery>(), _adapter, _coop, _logger);
    }

    private IStuckBattleEventAdapter Battle(StuckBattleSnapshot? snapshot, string label = "A vs B")
    {
        var battle = Substitute.For<IStuckBattleEventAdapter>();
        battle.Read().Returns(snapshot);
        battle.Label().Returns(label);
        return battle;
    }

    private void Live(params IStuckBattleEventAdapter[] battles) =>
        _adapter.LiveBattles().Returns(new List<IStuckBattleEventAdapter>(battles));

    [TestMethod]
    public void Resolve_EmptyDefenders_AwardsTheAttackers()
    {
        var battle = Battle(Snap(8000, 0));
        Live(battle);

        var report = _sut.Resolve();

        battle.Received(1).AwardVictory(attackerWins: true);
        Assert.AreEqual(1, report.Count);
        StringAssert.Contains(report[0], "attackers win");
        _logger.Received(1).LogInfo(Arg.Is<string>(s => s.Contains("attackers win")));
    }

    [TestMethod]
    public void Resolve_EmptyAttackers_AwardsTheDefenders()
    {
        var battle = Battle(Snap(0, 50));
        Live(battle);

        var report = _sut.Resolve();

        battle.Received(1).AwardVictory(attackerWins: false);
        StringAssert.Contains(report[0], "defenders win");
    }

    [TestMethod]
    public void Resolve_BothEmpty_TheDefendersHold()
    {
        var battle = Battle(Snap(0, 0));
        Live(battle);

        var report = _sut.Resolve();

        battle.Received(1).AwardVictory(attackerWins: false);
        StringAssert.Contains(report[0], "either side");
    }

    // One write per pass: the next pass judges the battle afresh after a detach.
    [TestMethod]
    public void Resolve_DestroyedParty_DetachesWithoutAwarding()
    {
        var battle = Battle(Snap(8000, 0, destroyedParties: 1));
        battle.DetachWrecks().Returns(1);
        Live(battle);

        var report = _sut.Resolve();

        battle.Received(1).DetachWrecks();
        battle.DidNotReceiveWithAnyArgs().AwardVictory(default);
        Assert.AreEqual(1, report.Count);
        StringAssert.Contains(report[0], "removed 1 wrecked");
    }

    [TestMethod]
    public void Resolve_HealthyBattle_WritesAndNamesNothing()
    {
        var battle = Battle(Snap(500, 400));
        Live(battle);

        Assert.AreEqual(0, _sut.Resolve().Count);
        battle.DidNotReceive().Label();
        battle.DidNotReceive().DetachWrecks();
        battle.DidNotReceiveWithAnyArgs().AwardVictory(default);
        _logger.DidNotReceiveWithAnyArgs().LogInfo(default!);
    }

    [TestMethod]
    public void Resolve_UnreadableBattle_IsSkipped()
    {
        var battle = Battle(null);
        Live(battle);

        Assert.AreEqual(0, _sut.Resolve().Count);
        battle.DidNotReceive().Label();
        battle.DidNotReceive().DetachWrecks();
        battle.DidNotReceiveWithAnyArgs().AwardVictory(default);
    }

    // A detach that finalizes the event re-points the side's leader, so the label is read before any write.
    [TestMethod]
    public void Resolve_ReadsTheLabelBeforeTheWrite()
    {
        var battle = Battle(Snap(8000, 0));
        Live(battle);

        _sut.Resolve();

        Received.InOrder(() =>
        {
            battle.Label();
            battle.AwardVictory(true);
        });
    }

    [TestMethod]
    public void Resolve_OnlyTheStuckBattle_IsWritten()
    {
        var healthy = Battle(Snap(500, 400));
        var stuck = Battle(Snap(8000, 0));
        Live(healthy, stuck);

        _sut.Resolve();

        healthy.DidNotReceiveWithAnyArgs().AwardVictory(default);
        stuck.Received(1).AwardVictory(true);
    }

    [TestMethod]
    public void Resolve_ThrowingBattle_DoesNotStopTheSweep()
    {
        var bad = Battle(Snap(8000, 0, destroyedParties: 1));
        bad.DetachWrecks().Throws(new System.InvalidOperationException("boom"));
        var good = Battle(Snap(8000, 0));
        Live(bad, good);

        var report = _sut.Resolve();

        good.Received(1).AwardVictory(true);
        bad.DidNotReceiveWithAnyArgs().AwardVictory(default);
        Assert.AreEqual(1, report.Count);
        _logger.Received(1).LogWarning(Arg.Is<string>(s => s.Contains("boom")));
    }

    [TestMethod]
    public void Resolve_SingleplayerWithCoopInstalled_Sweeps()
    {
        _coop.IsSessionActive.Returns(false);
        _coop.ShouldDeferToHost.Returns(false);
        Live();

        _sut.Resolve();

        _adapter.Received(1).LiveBattles();
    }

    // BannerlordCoop's Update prefix stops vanilla finishing any battle that holds a remote player's party, and the
    // player-battle test sees only the local player: a winner set there would commit and never finish.
    [TestMethod]
    public void Resolve_CoopSessionLive_StandsDown()
    {
        _coop.IsSessionActive.Returns(true);
        _coop.ShouldDeferToHost.Returns(false);

        Assert.AreEqual(0, _sut.Resolve().Count);
        _adapter.DidNotReceive().LiveBattles();
    }

    // A co-op mod TAOM cannot probe (BannerlordTogether): host cannot be told from client.
    [TestMethod]
    public void Resolve_UnprobeableCoopMod_StandsDown()
    {
        _coop.IsSessionActive.Returns(false);
        _coop.ShouldDeferToHost.Returns(true);

        Assert.AreEqual(0, _sut.Resolve().Count);
        _adapter.DidNotReceive().LiveBattles();
    }

    [TestMethod]
    public void IsStandingDown_FollowsBothCoopSignals()
    {
        Assert.IsFalse(_sut.IsStandingDown);

        _coop.IsSessionActive.Returns(true);
        Assert.IsTrue(_sut.IsStandingDown);

        _coop.IsSessionActive.Returns(false);
        _coop.ShouldDeferToHost.Returns(true);
        Assert.IsTrue(_sut.IsStandingDown);
    }

    [TestMethod]
    public void Describe_ListsEveryBattleWithItsVerdict_AndWritesNothing()
    {
        var healthy = Battle(Snap(500, 400), "Healthy");
        var stuck = Battle(Snap(8000, 0, destroyedParties: 1), "Stuck");
        Live(healthy, stuck);

        var lines = _sut.Describe();

        Assert.AreEqual(2, lines.Count);
        StringAssert.Contains(lines[0], "Healthy");
        StringAssert.Contains(lines[0], "None");
        StringAssert.Contains(lines[1], "DetachDestroyed");
        stuck.DidNotReceive().DetachWrecks();
        stuck.DidNotReceiveWithAnyArgs().AwardVictory(default);
    }

    [TestMethod]
    public void Describe_UnreadableBattle_IsSkipped()
    {
        Live(Battle(null), Battle(Snap(8000, 0)));

        Assert.AreEqual(1, _sut.Describe().Count);
    }
}
