using System;
using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using TAOM.Adapters;
using TAOM.Core.Logging;
using TAOM.Features.CoopInterop;
using TAOM.Features.MapEventGuard;

namespace TAOM.Tests.Features.MapEventGuard;

/// <summary>The sweep that finishes AI map events a battle-end crash left half finalized (player report, 2026-11-19).</summary>
[TestClass]
public class OrphanedBattleServiceTests
{
    private IOrphanedBattleAdapter _adapter = null!;
    private ICoopSessionProvider _coop = null!;
    private IModLogger _logger = null!;
    private OrphanedBattleService _service = null!;

    [TestInitialize]
    public void Setup()
    {
        _adapter = Substitute.For<IOrphanedBattleAdapter>();
        _coop = Substitute.For<ICoopSessionProvider>();
        _logger = Substitute.For<IModLogger>();
        _service = new OrphanedBattleService(_adapter, _coop, _logger);
    }

    private static IOrphanedBattleEventAdapter Battle(
        string label, bool finalized = true, bool missing = false, int defenders = 0, int attackers = 0)
    {
        var battle = Substitute.For<IOrphanedBattleEventAdapter>();
        battle.Label().Returns(label);
        battle.Read().Returns(new HalfFinishedEncounterSnapshot(true, finalized, missing, defenders, attackers));
        battle.EndSide(0).Returns(defenders);
        battle.EndSide(1).Returns(attackers);
        return battle;
    }

    private void Have(params IOrphanedBattleEventAdapter[] battles) =>
        _adapter.AttachedFinalizedBattles().Returns(new List<IOrphanedBattleEventAdapter>(battles));

    [TestMethod]
    public void RepairAll_CoopSessionActive_StandsDown()
    {
        _coop.IsSessionActive.Returns(true);
        Have(Battle("a", missing: true, defenders: 2, attackers: 3));

        Assert.AreEqual(0, _service.RepairAll().Count);
        _adapter.DidNotReceive().AttachedFinalizedBattles();
    }

    [TestMethod]
    public void RepairAll_DeferToHost_StandsDown()
    {
        _coop.ShouldDeferToHost.Returns(true);
        Have(Battle("a", missing: true, defenders: 2, attackers: 3));

        Assert.AreEqual(0, _service.RepairAll().Count);
        _adapter.DidNotReceive().AttachedFinalizedBattles();
    }

    [TestMethod]
    public void RepairAll_LiveBattle_Untouched()
    {
        var battle = Battle("live", finalized: false, defenders: 2, attackers: 3);
        Have(battle);

        Assert.AreEqual(0, _service.RepairAll().Count);
        battle.DidNotReceive().RestoreBackReference();
        battle.DidNotReceive().EndSide(Arg.Any<int>());
    }

    [TestMethod]
    public void RepairAll_CleanlyFinalized_Untouched()
    {
        var battle = Battle("done");
        Have(battle);

        Assert.AreEqual(0, _service.RepairAll().Count);
        battle.DidNotReceive().EndSide(Arg.Any<int>());
    }

    [TestMethod]
    public void RepairAll_AfterLoad_RestoresBackReferenceThenEndsDefendersThenAttackersThenFinishes()
    {
        var battle = Battle("Cair Andros", missing: true, defenders: 40, attackers: 30);
        Have(battle);

        var report = _service.RepairAll();

        Received.InOrder(() =>
        {
            battle.RestoreBackReference();
            battle.EndSide(0);
            battle.EndSide(1);
            battle.FinishEvent();
        });
        Assert.AreEqual(1, report.Count);
        StringAssert.Contains(report[0], "Cair Andros");
        StringAssert.Contains(report[0], "70");
    }

    [TestMethod]
    public void RepairAll_SameSession_KeepsBackReferenceAndEndsBothSides()
    {
        var battle = Battle("same session", missing: false, defenders: 1, attackers: 0);
        Have(battle);

        _service.RepairAll();

        battle.DidNotReceive().RestoreBackReference();
        battle.Received(1).EndSide(0);
        battle.Received(1).EndSide(1);
        battle.Received(1).FinishEvent();
    }

    [TestMethod]
    public void RepairAll_EndSideThrows_DoesNotFinish()
    {
        var battle = Battle("half", defenders: 2, attackers: 2);
        battle.When(b => b.EndSide(1)).Do(_ => throw new InvalidOperationException("z"));
        Have(battle);

        _service.RepairAll();

        battle.DidNotReceive().FinishEvent();
    }

    [TestMethod]
    public void RepairAll_OneBattleThrows_OthersStillRepaired()
    {
        var bad = Battle("bad", missing: true, defenders: 2);
        bad.When(b => b.EndSide(0)).Do(_ => throw new NullReferenceException("x"));
        var good = Battle("good", defenders: 2, attackers: 2);
        Have(bad, good);

        var report = _service.RepairAll();

        good.Received(1).EndSide(1);
        Assert.AreEqual(1, report.Count);
        StringAssert.Contains(report[0], "good");
        _logger.Received(1).LogWarning(Arg.Is<string>(s => s.Contains("bad") && s.Contains("NullReferenceException")));
    }

    [TestMethod]
    public void RepairAll_SameBattleFailsEveryPass_WarnsOnce()
    {
        var bad = Battle("bad", defenders: 2);
        bad.Read().Throws(new InvalidOperationException("y"));
        Have(bad);

        _service.RepairAll();
        _service.RepairAll();

        _logger.Received(1).LogWarning(Arg.Is<string>(s => s.Contains("bad")));
    }

    [TestMethod]
    public void RepairAll_Repaired_LogsOneWarningPerBattle()
    {
        Have(Battle("a", defenders: 1), Battle("b", attackers: 1));

        _service.RepairAll();

        _logger.Received(2).LogWarning(Arg.Is<string>(s => s.StartsWith("[MapEventGuard] finished an orphaned battle")));
    }
}
