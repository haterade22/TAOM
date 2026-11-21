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

    private static IStrandedPartyAdapter Stranded(string label)
    {
        var party = Substitute.For<IStrandedPartyAdapter>();
        party.Label().Returns(label);
        return party;
    }

    private void HaveStranded(params IStrandedPartyAdapter[] parties) =>
        _adapter.StrandedParties().Returns(new List<IStrandedPartyAdapter>(parties));

    [TestMethod]
    public void RepairAll_CoopSessionActive_StandsDown()
    {
        _coop.IsSessionActive.Returns(true);
        Have(Battle("a", missing: true, defenders: 2, attackers: 3));

        Assert.AreEqual(0, _service.RepairAll().Count);
        _adapter.DidNotReceive().AttachedFinalizedBattles();
        _adapter.DidNotReceive().StrandedParties();
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

    [TestMethod]
    public void RepairAll_StrandedParty_IsReleasedAndLogged()
    {
        Have();
        var party = Stranded("Vrakmaw's Party (Vrakmaw)");
        HaveStranded(party);

        var report = _service.RepairAll();

        party.Received(1).Release();
        Assert.AreEqual(1, report.Count);
        StringAssert.Contains(report[0], "Vrakmaw's Party");
        _logger.Received(1).LogWarning(Arg.Is<string>(s => s.StartsWith("[MapEventGuard] released a stranded party: Vrakmaw's Party")));
    }

    [TestMethod]
    public void RepairAll_BattlesAndStrandedParties_FinishesBattlesFirst()
    {
        var battle = Battle("Snagbut vs caravan", defenders: 4, attackers: 4);
        Have(battle);
        var party = Stranded("Hrakdush");
        HaveStranded(party);

        _service.RepairAll();

        // The scan runs after the finish, so it judges the side lists the finish has just emptied.
        Received.InOrder(() =>
        {
            battle.FinishEvent();
            _adapter.StrandedParties();
            party.Release();
        });
    }

    [TestMethod]
    public void RepairAll_StrandedReleaseThrows_OthersStillReleasedAndWarnsOnce()
    {
        Have();
        var bad = Stranded("bad");
        bad.When(p => p.Release()).Do(_ => throw new InvalidOperationException("no field"));
        var good = Stranded("good");
        HaveStranded(bad, good);

        _service.RepairAll();
        var report = _service.RepairAll();

        good.Received(2).Release();
        CollectionAssert.AreEqual(new[] { "good" }, new List<string>(report));
        _logger.Received(1).LogWarning(Arg.Is<string>(s => s.Contains("could not release stranded party bad") && s.Contains("no field")));
    }

    [DataTestMethod]
    [DataRow(true, false, false, false, true, DisplayName = "side set, not listed: stranded")]
    [DataRow(true, true, false, false, false, DisplayName = "side lists it: a normal battle member")]
    [DataRow(false, false, false, false, false, DisplayName = "no side: not in a battle")]
    [DataRow(true, false, true, false, false, DisplayName = "main party: left to vanilla")]
    [DataRow(true, false, false, true, false, DisplayName = "in the player's encounter: Patch170 owns it")]
    public void IsStranded_State_Expected(bool hasSide, bool sideListsIt, bool isMainParty, bool inPlayerBattle, bool expected)
    {
        Assert.AreEqual(expected, OrphanedBattleService.IsStranded(hasSide, sideListsIt, isMainParty, inPlayerBattle));
    }
}
