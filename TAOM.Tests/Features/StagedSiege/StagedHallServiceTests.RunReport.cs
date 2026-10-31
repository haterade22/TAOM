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

/// <summary>The hall service's run report (#1054).</summary>
public partial class StagedHallServiceTests
{
    private List<string> ReportLines() => _log.ReceivedCalls()
        .Where(c => c.GetMethodInfo().Name == "LogInfo")
        .Select(c => (string)c.GetArguments()[0]!)
        .Where(l => l.StartsWith("[StagedSiege] REPORT ", StringComparison.Ordinal) || l.StartsWith("[StagedSiege] CHECK ", StringComparison.Ordinal))
        .ToList();

    private string CheckLine(string id) => ReportLines().Single(l => l.StartsWith("[StagedSiege] CHECK " + id + " ", StringComparison.Ordinal));

    [TestMethod]
    public void Shutdown_AfterATakenHall_LogsTheWholeReport()
    {
        ArmAndStart();
        CaptureTheHall();
        _svc.NoteMissionResult("AttackerVictory", true, false);
        StagedSiegeSlot.RecordHud(_token, "E:TakeHall");
        StagedSiegeSlot.RecordHud(_token, "E:HallTaken");

        _svc.Shutdown();

        var lines = ReportLines();
        Assert.AreEqual(2 + 6 + 1, lines.Count);
        StringAssert.StartsWith(lines[0], "[StagedSiege] REPORT hall: settlement castle_sturgia_a, area 3, markers 2, armed 0.0 s, "
            + "first standing attacker 30 defender 40, last pass ");
        StringAssert.Contains(lines[0], "result AttackerVictory victory True defeated False");
        StringAssert.StartsWith(lines[1], "[StagedSiege] REPORT hall zone: peak attacker 10 defender 0");
        foreach (var id in new[] { "hall_flag", "hall_capture", "hall_end", "hall_cue", "hud", "faults" })
            StringAssert.Contains(CheckLine(id), " PASS: ");
        StringAssert.Contains(lines.Last(), "REPORT summary: 6 checks, 6 PASS, 0 FAIL (none), 0 NOT SEEN (none)");
    }

    [TestMethod]
    public void Shutdown_RunsTwice_WritesTheReportOnce()
    {
        ArmAndStart();

        _svc.Shutdown();
        _svc.Shutdown();

        Assert.AreEqual(1, ReportLines().Count(l => l.Contains("REPORT hall:")));
        Assert.AreEqual(1, ReportLines().Count(l => l.Contains("REPORT summary:")));
    }

    [TestMethod]
    public void Shutdown_AfterAFault_StillWritesTheReportWithTheFault()
    {
        ArmAndStart();
        _m.CountInHall(Arg.Any<HallZone>()).Throws(new InvalidOperationException("scene gone"));
        At(1.5f);
        Assert.IsTrue(_svc.IsFaulted);

        _svc.NoteMissionResult("DefenderVictory", false, true);
        _svc.Shutdown();

        StringAssert.Contains(CheckLine("faults"), "FAIL: service Tick: InvalidOperationException");
        StringAssert.Contains(ReportLines()[0], "result DefenderVictory victory False defeated True");
        StringAssert.Contains(CheckLine("hall_capture"), "NOT SEEN");
    }

    [TestMethod]
    public void Shutdown_CueThrows_ReportsTheFailedCueAndTheFault()
    {
        ArmAndStart();
        _m.When(m => m.AnnounceHallTaken()).Do(_ => throw new InvalidOperationException("no screen"));
        _inE = new ZoneCount(10, 0);
        for (var i = 0; i < 100 && !_svc.IsFaulted; i++)
            At(_now + 0.5f);
        Assert.IsTrue(_svc.IsFaulted);

        _svc.Shutdown();

        StringAssert.Contains(CheckLine("hall_cue"), "FAIL: captured, the cue threw");
        StringAssert.Contains(CheckLine("faults"), "FAIL: service Tick: InvalidOperationException");
    }

    [TestMethod]
    public void TryArm_ColourFlagThrows_StillReportsTheFlagSpawnedAndTheHall()
    {
        _m.When(m => m.ColourFlag(Arg.Any<StagedSide>())).Do(_ => throw new InvalidOperationException("no flag"));

        Assert.IsFalse(_svc.TryArm(true));
        Assert.IsTrue(_svc.IsFaulted);
        _svc.Shutdown();

        StringAssert.Contains(CheckLine("hall_flag"), "PASS: 1 spawned");
        StringAssert.Contains(ReportLines()[0], "settlement castle_sturgia_a, area 3, markers 2");
    }

    [TestMethod]
    public void NoteBoundaryFault_IsRecordedWithoutFaultingOrDisarmingTheService()
    {
        ArmAndStart();

        _svc.NoteBoundaryFault("MissionEnded (hall)", new InvalidOperationException("edge"));

        Assert.IsTrue(_svc.IsArmed);
        Assert.IsFalse(_svc.IsFaulted);
        _log.DidNotReceiveWithAnyArgs().LogError(default!);
        _svc.Shutdown();
        StringAssert.Contains(CheckLine("faults"), "FAIL: boundary MissionEnded (hall): InvalidOperationException");
    }

    [TestMethod]
    public void Shutdown_Inert_WritesNoReport()
    {
        _m.IsInLordsHall.Returns(false);
        Assert.IsFalse(_svc.TryArm(true));

        _svc.NoteMissionResult("AttackerVictory", true, false);
        _svc.Shutdown();

        Assert.AreEqual(0, ReportLines().Count);
        AssertLogged("hall inert: NotInTheLordsHall");
    }

    [TestMethod]
    public void Shutdown_NoFlagSpawned_FailsTheFlagCheck()
    {
        _m.SpawnFlag(Arg.Any<StagePoint>()).Returns(0);
        ArmAndStart();

        _svc.Shutdown();

        StringAssert.Contains(CheckLine("hall_flag"), "FAIL: 0 spawned");
    }

    [TestMethod]
    public void Shutdown_ASideWithNobodyStanding_CountsAPassButNoZoneSeconds()
    {
        _attackersStanding = 0;
        ArmAndStart();

        _svc.Shutdown();

        var lines = ReportLines();
        StringAssert.Contains(lines[0], "first standing attacker 0 defender 40");
        StringAssert.Contains(lines[1], "peak attacker 0 defender 0");
        StringAssert.Contains(lines.Last(), "passes 1");
    }

    [TestMethod]
    public void Shutdown_NoHudRecord_HudCheckIsNotSeen()
    {
        ArmAndStart();

        _svc.Shutdown();

        StringAssert.Contains(CheckLine("hud"), "NOT SEEN: the HUD never ran");
    }

    [TestMethod]
    public void Shutdown_ArmedHall_RecordsTheSettlementInTheSlot()
    {
        ArmAndStart();

        _svc.Shutdown();

        Assert.AreEqual(Settlement, StagedSiegeSlot.CampaignSettlementId);
    }

    [TestMethod]
    public void Shutdown_Inert_RecordsNoSettlement()
    {
        _m.IsInLordsHall.Returns(false);
        _svc.TryArm(true);

        _svc.Shutdown();

        Assert.IsNull(StagedSiegeSlot.CampaignSettlementId);
    }

    [TestMethod]
    public void NoteMissionResult_AfterShutdown_IsIgnored()
    {
        ArmAndStart();
        _svc.Shutdown();

        _svc.NoteMissionResult("AttackerVictory", true, false);

        StringAssert.Contains(ReportLines()[0], "result none victory False defeated False");
    }
}
