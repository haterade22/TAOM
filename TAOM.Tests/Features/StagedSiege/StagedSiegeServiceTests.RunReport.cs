using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using NSubstitute.Core;
using NSubstitute.ExceptionExtensions;
using TAOM.Features.StagedSiege;
using TAOM.Features.StagedSiege.Domain;

namespace TAOM.Tests.Features.StagedSiege;

/// <summary>The walls service's run report and the running lines that fill the log's gaps (#1054).</summary>
public partial class StagedSiegeServiceTests
{
    private List<string> Logged() => _log.ReceivedCalls()
        .Where(c => c.GetMethodInfo().Name == "LogInfo")
        .Select(c => (string)c.GetArguments()[0]!)
        .ToList();

    private List<string> ReportLines() => Logged()
        .Where(l => l.StartsWith("[StagedSiege] REPORT ", StringComparison.Ordinal) || l.StartsWith("[StagedSiege] CHECK ", StringComparison.Ordinal))
        .ToList();

    private string CheckLine(string id) => ReportLines().Single(l => l.StartsWith("[StagedSiege] CHECK " + id + " ", StringComparison.Ordinal));

    private void RunToAttackerWin()
    {
        BecomeCustomBattle(StagedSide.Attacker);
        ArmAndSetUp();
        for (var i = 0; i < 4; i++)
            CaptureCurrent();
        Assert.IsTrue(_svc.TryGetEnd(out var end));
        _svc.AnnounceEnd(end);
    }

    // --- the report --------------------------------------------------------------------------------------------------

    [TestMethod]
    public void Shutdown_AfterAWonRun_LogsTheWholeReportInOrder()
    {
        RunToAttackerWin();
        _svc.NoteMissionResult("AttackerVictory", true, false);
        foreach (var pair in new[] { "A:Take", "B:Take", "C:Take", "D:Take" })
            StagedSiegeSlot.RecordHud(_token, pair);

        _svc.Shutdown();

        var lines = ReportLines();
        Assert.AreEqual(1 + 4 + 14 + 1, lines.Count);
        StringAssert.StartsWith(lines[0], "[StagedSiege] REPORT walls: scene sturgia_castle_siege_001, player Attacker, custom battle, armed 0.0 s, set up 1.0 s");
        StringAssert.Contains(lines[0], "stage reached D, end AttackerWon, result AttackerVictory victory True defeated False, "
            + "survivors attacker 100+200 defender 80+100");
        StringAssert.StartsWith(lines[1], "[StagedSiege] REPORT stage A: ");
        StringAssert.StartsWith(lines[5], "[StagedSiege] CHECK tactics ");
        StringAssert.StartsWith(lines[lines.Count - 1], "[StagedSiege] REPORT summary: 14 checks");
        StringAssert.Contains(CheckLine("end_chain"), "PASS");
        StringAssert.Contains(CheckLine("victory_cue"), "PASS");
        StringAssert.Contains(CheckLine("capture_rule"), "PASS: 4 stage(s) captured by the rule");
        StringAssert.Contains(CheckLine("gates"), "PASS: B captured, 2 opened");
        StringAssert.Contains(CheckLine("hud"), "PASS");
        StringAssert.Contains(CheckLine("spawn_settings"), "PASS");
        StringAssert.Contains(CheckLine("flags"), "PASS: 4 spawned");
        StringAssert.Contains(CheckLine("detachment"), "PASS: start 10");
    }

    [TestMethod]
    public void Shutdown_RunsTwice_WritesTheReportOnce()
    {
        RunToAttackerWin();

        _svc.Shutdown();
        _svc.Shutdown();

        Assert.AreEqual(1, ReportLines().Count(l => l.Contains("REPORT walls:")));
        Assert.AreEqual(1, ReportLines().Count(l => l.Contains("REPORT summary:")));
    }

    [TestMethod]
    public void Shutdown_AfterAFault_StillWritesTheReportWithTheFault()
    {
        ArmAndSetUp();
        _m.CountInZone(Arg.Any<StagePoint>(), Arg.Any<float>(), Arg.Any<float>()).Throws(new InvalidOperationException("boom"));
        At(1.5f);
        Assert.IsTrue(_svc.IsFaulted);

        _svc.Shutdown();

        StringAssert.Contains(CheckLine("faults"), "FAIL: service Tick: InvalidOperationException");
        StringAssert.Contains(CheckLine("tactics"), "NOT SEEN");
        StringAssert.Contains(ReportLines().Last(), "fault service Tick: InvalidOperationException");
    }

    [TestMethod]
    public void Setup_ColourFlagThrows_StillReportsTheFlagsSpawnedAndTheSetUp()
    {
        _m.When(m => m.ColourFlag(Arg.Any<StageId>(), Arg.Any<StagedSide>())).Do(_ => throw new InvalidOperationException("no flag"));
        ArmAndSetUp();
        Assert.IsTrue(_svc.IsFaulted);

        _svc.Shutdown();

        StringAssert.Contains(CheckLine("flags"), "PASS: 4 spawned");
        StringAssert.Contains(CheckLine("spawn_settings"), "PASS");
        StringAssert.Contains(ReportLines()[0], "set up 1.0 s");
    }

    [TestMethod]
    public void NoteBoundaryFault_IsRecordedWithoutFaultingOrDisarmingTheService()
    {
        ArmAndSetUp();

        _svc.NoteBoundaryFault("MissionEnded", new InvalidOperationException("edge"));

        Assert.IsTrue(_svc.IsArmed);
        Assert.IsFalse(_svc.IsFaulted);
        Assert.IsTrue(StagedSiegeSlot.HasArmed);
        _log.DidNotReceiveWithAnyArgs().LogError(default!);
        _svc.Shutdown();
        StringAssert.Contains(CheckLine("faults"), "FAIL: boundary MissionEnded: InvalidOperationException");
    }

    [TestMethod]
    public void NoteBoundaryFault_AfterAServiceFault_KeepsBoth()
    {
        ArmAndSetUp();
        _m.CountInZone(Arg.Any<StagePoint>(), Arg.Any<float>(), Arg.Any<float>()).Throws(new InvalidOperationException("boom"));
        At(1.5f);

        _svc.NoteBoundaryFault("OnAgentBuild", new ArgumentException("edge"));
        _svc.Shutdown();

        StringAssert.Contains(CheckLine("faults"), "service Tick: InvalidOperationException; boundary OnAgentBuild: ArgumentException");
    }

    [TestMethod]
    public void NoteMissionResult_AfterAFault_IsStillRecorded()
    {
        ArmAndSetUp();
        _m.CountInZone(Arg.Any<StagePoint>(), Arg.Any<float>(), Arg.Any<float>()).Throws(new InvalidOperationException("boom"));
        At(1.5f);

        _svc.NoteMissionResult("DefenderVictory", false, true);
        _svc.Shutdown();

        StringAssert.Contains(ReportLines()[0], "result DefenderVictory victory False defeated True");
    }

    [TestMethod]
    public void NoteMissionResult_AdapterReadThrows_StillRecordsTheResult()
    {
        ArmAndSetUp();
        _m.ReadSpawnCounts(StagedSide.Attacker).Throws(new InvalidOperationException("gone"));

        _svc.NoteMissionResult("AttackerVictory", true, false);
        _svc.Shutdown();

        StringAssert.Contains(ReportLines()[0], "result AttackerVictory victory True defeated False, survivors none");
    }

    [TestMethod]
    public void NoteMissionResult_AfterShutdown_IsIgnored()
    {
        ArmAndSetUp();
        _svc.Shutdown();

        _svc.NoteMissionResult("AttackerVictory", true, false);

        StringAssert.Contains(ReportLines()[0], "result none victory False defeated False");
    }

    [TestMethod]
    public void Shutdown_InertService_WritesNoReport()
    {
        _m.IsSiegeBattle.Returns(false);
        Assert.IsFalse(_svc.TryArm(true));

        _svc.NoteMissionResult("AttackerVictory", true, false);
        _svc.Shutdown();

        Assert.AreEqual(0, ReportLines().Count);
        AssertLogged("inert: NotASiege");
    }

    [TestMethod]
    public void Shutdown_NoHudRecord_HudCheckIsNotSeen()
    {
        ArmAndSetUp();

        _svc.Shutdown();

        StringAssert.Contains(CheckLine("hud"), "NOT SEEN: the HUD never ran");
    }

    [TestMethod]
    public void Shutdown_ReportsTheReinforcementsEveryAgentNotOnlyTheLoggedThree()
    {
        ArmAndSetUp();
        CaptureCurrent();
        var attackerFrame = _plan.AttackerSpawn(StageId.B)!.Value.Position;
        var defenderFrame = _plan.DefenderSpawn(StageId.B)!.Value.Position;
        for (var i = 0; i < 5; i++)
        {
            _svc.NoteAgentBuilt(StagedSide.Attacker, new StagePoint(attackerFrame.X + 2f, attackerFrame.Y, attackerFrame.Z));
            _svc.NoteAgentBuilt(StagedSide.Defender, new StagePoint(defenderFrame.X, defenderFrame.Y + 4f, defenderFrame.Z));
        }

        _svc.Shutdown();

        var stageB = ReportLines().Single(l => l.Contains("REPORT stage B:"));
        StringAssert.Contains(stageB, "reinforcements attacker 5 defender 5");
        StringAssert.Contains(stageB, "frame max attacker 2.0 m defender 4.0 m");
        StringAssert.Contains(CheckLine("reinforcement_frames"), "PASS");
    }

    [TestMethod]
    public void Shutdown_ReinforcementInStageA_HasNoFrameToMeasure()
    {
        ArmAndSetUp();

        _svc.NoteAgentBuilt(StagedSide.Attacker, new StagePoint(1f, 2f, 3f));
        _svc.Shutdown();

        StringAssert.Contains(ReportLines().Single(l => l.Contains("REPORT stage A:")), "reinforcements attacker 1 defender 0, frame max attacker none");
        StringAssert.Contains(CheckLine("reinforcement_frames"), "NOT SEEN");
    }

    [TestMethod]
    public void Shutdown_ASoftlockEndedByABuiltAgent_ReportsItsSpan()
    {
        ArmAndSetUp();
        _attackerCounts = new SpawnCounts(0, 50);
        _attackerStanding = 0;
        At(1.5f);
        _now = 2f;
        Assert.IsTrue(_svc.ShouldReleaseBatch(StagedSide.Attacker));
        _now = 5f;
        _svc.NoteAgentBuilt(StagedSide.Attacker, new StagePoint(1f, 2f, 3f));

        _svc.Shutdown();

        StringAssert.Contains(CheckLine("softlock"), "PASS: 1 span(s), longest 3.0 s");
        StringAssert.Contains(ReportLines().Single(l => l.Contains("REPORT stage A:")), "softlocks attacker 1 defender 0");
    }

    [TestMethod]
    public void Shutdown_ASoftlockStillOpen_CountsUpToTheLastPass()
    {
        ArmAndSetUp();
        _attackerCounts = new SpawnCounts(0, 50);
        _attackerStanding = 0;
        At(1.5f);
        _now = 2f;
        Assert.IsTrue(_svc.ShouldReleaseBatch(StagedSide.Attacker));
        for (var t = 2.5f; t <= 20f; t += 0.5f)
            At(t);

        _svc.Shutdown();

        StringAssert.Contains(CheckLine("softlock"), "FAIL");
        StringAssert.Contains(CheckLine("softlock"), "attacker 18.0 s");
    }

    [TestMethod]
    public void Shutdown_BudgetHeld_IsCountedInTheStageLine()
    {
        ArmAndSetUp();
        _attackerCounts = new SpawnCounts(100, 160);
        _now = 11f;
        Assert.IsFalse(_svc.ShouldReleaseBatch(StagedSide.Attacker));
        Assert.IsFalse(_svc.ShouldReleaseBatch(StagedSide.Attacker));

        _svc.Shutdown();

        StringAssert.Contains(ReportLines().Single(l => l.Contains("REPORT stage A:")), "budget holds attacker 1 defender 0");
    }

    [TestMethod]
    public void Shutdown_ForcedCapture_IsReportedAsForced()
    {
        ArmAndSetUp();

        _svc.ForceStage(StageId.C);
        _svc.Shutdown();

        var lines = ReportLines();
        StringAssert.Contains(lines.Single(l => l.Contains("REPORT stage A:")), "captured by forced");
        StringAssert.Contains(lines.Single(l => l.Contains("REPORT stage B:")), "skipped, captured by forced");
        StringAssert.Contains(CheckLine("capture_rule"), "NOT SEEN: no capture by the rule, 2 forced");
    }

    [TestMethod]
    public void Shutdown_Report_CostsAreRecordedPerPass()
    {
        ArmAndSetUp();
        ZoneHas(StageId.A, 1, 0);
        At(1.5f);
        At(2f);

        _svc.Shutdown();

        StringAssert.Contains(ReportLines().Last(), "passes 2, slowest ");
    }

    // --- the settlement record ---------------------------------------------------------------------------------------

    [TestMethod]
    public void Shutdown_CampaignBattle_RecordsTheSettlementInTheSlot()
    {
        _m.EncounterSettlementId.Returns("castle_sturgia_a");
        ArmAndSetUp();

        _svc.Shutdown();

        Assert.AreEqual("castle_sturgia_a", StagedSiegeSlot.CampaignSettlementId);
    }

    [TestMethod]
    public void Shutdown_CustomBattle_RecordsNoSettlement()
    {
        _m.EncounterSettlementId.Returns("castle_sturgia_a");
        BecomeCustomBattle(StagedSide.Attacker);
        ArmAndSetUp();

        _svc.Shutdown();

        Assert.IsNull(StagedSiegeSlot.CampaignSettlementId);
    }

    [TestMethod]
    public void Shutdown_Inert_RecordsNoSettlement()
    {
        _m.EncounterSettlementId.Returns("castle_sturgia_a");
        _m.IsSiegeBattle.Returns(false);
        _svc.TryArm(true);

        _svc.Shutdown();

        Assert.IsNull(StagedSiegeSlot.CampaignSettlementId);
    }

    [TestMethod]
    public void Shutdown_SettlementReadThrows_StillWritesTheReport()
    {
        _m.EncounterSettlementId.Throws(new InvalidOperationException("no encounter"));

        ArmAndSetUp();
        _svc.Shutdown();

        Assert.IsNull(StagedSiegeSlot.CampaignSettlementId);
        Assert.AreEqual(1, ReportLines().Count(l => l.Contains("REPORT walls:")));
    }

    // --- the running lines -------------------------------------------------------------------------------------------

    [TestMethod]
    public void Tick_ZonePass_LogsBothSidesTroopsWithTheZoneLine()
    {
        ArmAndSetUp();
        ZoneHas(StageId.A, 4, 2);

        At(1.5f);

        AssertLogged("stage A zone: attackers 4 defenders 2");
        AssertLogged("stage A troops: attacker active 100 standing 100 remaining 200, defender active 80 standing 80 remaining 100");
    }

    [TestMethod]
    public void Tick_TroopsLine_IsThrottledWithTheZoneLine()
    {
        ArmAndSetUp();
        ZoneHas(StageId.A, 4, 2);

        for (var t = 1.5f; t < 5f; t += 0.5f)
            At(t);

        Assert.AreEqual(1, Logged().Count(l => l.Contains("troops:")));
    }

    [TestMethod]
    public void Tick_DetachmentOrderChanges_LogsTheOldAndNewOrder()
    {
        ArmAndSetUp();
        var center = _plan.Zone(StageId.A).Center;
        _m.CountInZone(Arg.Is<StagePoint>(p => Near(p, center)), 16f, 10f).Returns(new ZoneCount(2, 0));
        _m.CountInZone(Arg.Is<StagePoint>(p => Near(p, center)), 24f, 10f).Returns(new ZoneCount(2, 0));

        for (var t = 1.5f; t <= 3.5f; t += 0.5f)
            At(t);

        Assert.AreEqual(1, Logged().Count(l => l.Contains("S3 detachment order Move -> Charge at ")));
    }

    [TestMethod]
    public void Tick_Capture_AppendsBothSidesCountsToTheCapturedLine()
    {
        ArmAndSetUp();

        CaptureCurrent();

        Assert.IsTrue(Logged().Any(l => l.Contains("stage A captured at ") && l.Contains("; next B")
            && l.EndsWith("; attacker active 100 remaining 200, defender active 80 remaining 100", StringComparison.Ordinal)));
    }

    [TestMethod]
    public void AnnounceEnd_AttackerWon_LogsTheCueOnce()
    {
        RunToAttackerWin();
        Assert.IsTrue(_svc.TryGetEnd(out var end));

        _svc.AnnounceEnd(end);

        Assert.AreEqual(1, Logged().Count(l => l.Contains("S5 victory cue shown")));
    }

    [TestMethod]
    public void AnnounceEnd_CueThrows_ReportsAFailedCueAndLogsNoCueLine()
    {
        BecomeCustomBattle(StagedSide.Attacker);
        _m.When(m => m.AnnounceVictory()).Do(_ => throw new InvalidOperationException("no mission screen"));
        ArmAndSetUp();
        for (var i = 0; i < 4; i++)
            CaptureCurrent();
        Assert.IsTrue(_svc.TryGetEnd(out var end));

        _svc.AnnounceEnd(end);
        _svc.Shutdown();

        Assert.AreEqual(0, Logged().Count(l => l.Contains("S5 victory cue shown")));
        StringAssert.Contains(CheckLine("victory_cue"), "FAIL: AttackerWon, the cue threw");
    }
}
