using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.StagedSiege.Domain;

namespace TAOM.Tests.Features.StagedSiege;

/// <summary>The lord's hall run report (#1054): the facts, the check verdicts and the exact lines.</summary>
[TestClass]
public class StagedHallRunReportTests
{
    private static readonly string[] CheckOrder = { "hall_flag", "hall_capture", "hall_end", "hall_cue", "hud", "faults" };

    private static StagedHallRunReport Armed(int flags = 1)
    {
        var r = new StagedHallRunReport();
        r.NoteArmed("castle_sturgia_a", 3, 2, 0f);
        r.NoteFlags(flags);
        return r;
    }

    private static StagedHallRunReport Taken()
    {
        var r = Armed();
        r.NotePass(10f, 0f, 30, 40, 5, 1, CaptureStatus.Rising, 0.4f);
        r.NotePassCost(0.25);
        r.NotePass(14f, 4f, 30, 38, 5, 1, CaptureStatus.Captured, 1f);
        r.NotePassCost(0.75);
        r.NoteCaptured(14f);
        r.NoteCue(true);
        r.NoteMissionResult("AttackerVictory", true, false);
        r.NoteHud(new[] { "E:TakeHall", "E:HallTaken" }, new[] { "E" });
        return r;
    }

    private static string Line(IReadOnlyList<string> lines, string id)
    {
        var line = lines.FirstOrDefault(l => l.StartsWith("CHECK " + id + " ", StringComparison.Ordinal));
        Assert.IsNotNull(line, "no CHECK line for " + id);
        return line!;
    }

    private static void AssertVerdict(StagedHallRunReport r, string id, string verdict, string? evidence = null)
    {
        var line = Line(r.Build(), id);
        StringAssert.StartsWith(line, "CHECK " + id + " " + verdict + ": ");
        if (evidence != null)
            StringAssert.Contains(line, evidence);
    }

    [TestMethod]
    public void Build_TakenHall_EveryCheckPasses()
    {
        var lines = Taken().Build();

        foreach (var id in CheckOrder)
            StringAssert.StartsWith(Line(lines, id), "CHECK " + id + " PASS: ");
        Assert.AreEqual("REPORT summary: 6 checks, 6 PASS, 0 FAIL (none), 0 NOT SEEN (none); passes 2, slowest 0.75 ms, mean 0.50 ms; fault none",
            lines.Last());
    }

    [TestMethod]
    public void Build_TakenHall_LinesComeInOrder()
    {
        var lines = Taken().Build();

        StringAssert.StartsWith(lines[0], "REPORT hall: ");
        StringAssert.StartsWith(lines[1], "REPORT hall zone: ");
        var checks = lines.Where(l => l.StartsWith("CHECK ", StringComparison.Ordinal)).Select(l => l.Split(' ')[1]).ToList();
        CollectionAssert.AreEqual(CheckOrder, checks);
        Assert.AreEqual(2 + 6 + 1, lines.Count);
    }

    [TestMethod]
    public void Build_TakenHall_HeaderAndZoneLines()
    {
        var lines = Taken().Build();

        Assert.AreEqual("REPORT hall: settlement castle_sturgia_a, area 3, markers 2, armed 0.0 s, first standing attacker 30 defender 40, "
            + "last pass 14.0 s, captured 14.0 s, result AttackerVictory victory True defeated False", lines[0]);
        Assert.AreEqual("REPORT hall zone: peak attacker 5 defender 1, progress peak 1.00, rising 4.0 s falling 0.0 s contested 0.0 s idle 0.0 s",
            lines[1]);
    }

    [TestMethod]
    public void Build_NothingHappened_PrintsNoneForAbsentValues()
    {
        var lines = Armed().Build();

        Assert.AreEqual("REPORT hall: settlement castle_sturgia_a, area 3, markers 2, armed 0.0 s, first standing none, last pass none, "
            + "captured none, result none victory False defeated False", lines[0]);
    }

    [TestMethod]
    public void Build_UnderGermanCulture_PrintsInvariantNumbers()
    {
        var original = Thread.CurrentThread.CurrentCulture;
        try
        {
            Thread.CurrentThread.CurrentCulture = new CultureInfo("de-DE");
            var text = string.Join("\n", Taken().Build());

            StringAssert.Contains(text, "captured 14.0 s");
            StringAssert.Contains(text, "progress peak 1.00");
            StringAssert.Contains(text, "slowest 0.75 ms, mean 0.50 ms");
            Assert.IsFalse(text.Contains("0,5"), "a comma decimal leaked into the report");
        }
        finally
        {
            Thread.CurrentThread.CurrentCulture = original;
        }
    }

    [TestMethod]
    public void HallFlag_Spawned_Pass() => AssertVerdict(Armed(), "hall_flag", "PASS", "1 spawned");

    [TestMethod]
    public void HallFlag_NoneSpawned_Fail() => AssertVerdict(Armed(0), "hall_flag", "FAIL", "0 spawned");

    [TestMethod]
    public void HallFlag_ArmedButTheCountWasNeverRead_NotSeen()
    {
        var r = new StagedHallRunReport();
        r.NoteArmed("castle_sturgia_a", 3, 2, 0f);
        AssertVerdict(r, "hall_flag", "NOT SEEN", "never read");
    }

    [TestMethod]
    public void HallCapture_ByTheRule_Pass() => AssertVerdict(Taken(), "hall_capture", "PASS", "14.0 s");

    [TestMethod]
    public void HallCapture_NotCaptured_NotSeenWithResultAndPeaks()
    {
        var r = Armed();
        r.NotePass(10f, 0f, 30, 40, 2, 6, CaptureStatus.Falling, 0.1f);
        r.NoteMissionResult("DefenderVictory", false, true);

        AssertVerdict(r, "hall_capture", "NOT SEEN", "result DefenderVictory victory False defeated True; peak attacker 2 defender 6");
    }

    [TestMethod]
    public void HallEnd_CapturedWithAttackerVictory_Pass() => AssertVerdict(Taken(), "hall_end", "PASS", "AttackerVictory");

    [DataTestMethod]
    [DataRow("DefenderVictory", true, false)]
    [DataRow("AttackerVictory", false, false)]
    [DataRow("AttackerVictory", true, true)]
    public void HallEnd_CapturedWithAnotherResult_Fail(string state, bool victory, bool defeated)
    {
        var r = Armed();
        r.NotePass(14f, 4f, 30, 38, 5, 1, CaptureStatus.Captured, 1f);
        r.NoteCaptured(14f);
        r.NoteMissionResult(state, victory, defeated);
        AssertVerdict(r, "hall_end", "FAIL", state);
    }

    [TestMethod]
    public void HallEnd_CapturedWithNoResult_Fail()
    {
        var r = Armed();
        r.NoteCaptured(14f);
        AssertVerdict(r, "hall_end", "FAIL", "no result");
    }

    [TestMethod]
    public void HallEnd_NotCaptured_NotSeen() => AssertVerdict(Armed(), "hall_end", "NOT SEEN");

    [TestMethod]
    public void HallCue_Ran_Pass() => AssertVerdict(Taken(), "hall_cue", "PASS");

    [TestMethod]
    public void HallCue_Threw_Fail()
    {
        var r = Armed();
        r.NoteCaptured(14f);
        r.NoteCue(false);
        AssertVerdict(r, "hall_cue", "FAIL", "threw");
    }

    [TestMethod]
    public void HallCue_CapturedButNeverAttempted_Fail()
    {
        var r = Armed();
        r.NoteCaptured(14f);
        AssertVerdict(r, "hall_cue", "FAIL", "never ran");
    }

    [TestMethod]
    public void HallCue_NotCaptured_NotSeen() => AssertVerdict(Armed(), "hall_cue", "NOT SEEN");

    [TestMethod]
    public void Hud_TakeHallAndHallTaken_Pass() => AssertVerdict(Taken(), "hud", "PASS", "E:HallTaken, E:TakeHall");

    [TestMethod]
    public void Hud_CapturedButNoHallTaken_Fail()
    {
        var r = Armed();
        r.NoteCaptured(14f);
        r.NoteHud(new[] { "E:TakeHall" }, new[] { "E" });
        AssertVerdict(r, "hud", "FAIL", "E:HallTaken never shown");
    }

    [TestMethod]
    public void Hud_NeverShowedTakeHall_Fail()
    {
        var r = Armed();
        r.NoteHud(new[] { "E:HallTaken" }, new[] { "E" });
        AssertVerdict(r, "hud", "FAIL", "E:TakeHall never shown");
    }

    [TestMethod]
    public void Hud_PrintsTheMarkerLetters()
    {
        AssertVerdict(Taken(), "hud", "PASS", "shown E:HallTaken, E:TakeHall; markers E");

        var r = Armed();
        r.NoteHud(new[] { "E:TakeHall" }, Array.Empty<string>());
        AssertVerdict(r, "hud", "PASS", "markers none");
    }

    [TestMethod]
    public void Faults_ServiceAndBoundary_AreKeptAndMarked()
    {
        var r = Armed();
        r.NoteFault("Tick", "InvalidOperationException");
        r.NoteBoundaryFault("MissionEnded (hall)", "NullReferenceException");

        AssertVerdict(r, "faults", "FAIL", "service Tick: InvalidOperationException; boundary MissionEnded (hall): NullReferenceException");
    }

    [TestMethod]
    public void Hud_TakeHallBeforeAnyCapture_Pass()
    {
        var r = Armed();
        r.NoteHud(new[] { "E:TakeHall" }, new[] { "E" });
        AssertVerdict(r, "hud", "PASS");
    }

    [TestMethod]
    public void Hud_NeverRan_NotSeen()
    {
        var r = Armed();
        r.NoteHud(null, null);
        AssertVerdict(r, "hud", "NOT SEEN", "never ran");
    }

    [TestMethod]
    public void Faults_None_Pass() => AssertVerdict(Armed(), "faults", "PASS");

    [TestMethod]
    public void Faults_AfterAFault_FailsAndTheSummaryNamesIt()
    {
        var r = Armed();
        r.NoteFault("Tick", "InvalidOperationException");

        AssertVerdict(r, "faults", "FAIL", "Tick: InvalidOperationException");
        StringAssert.Contains(r.Build().Last(), "fault service Tick: InvalidOperationException");
    }

    [TestMethod]
    public void NotePass_FirstPassKeepsTheFirstStandingCounts()
    {
        var r = Armed();
        r.NotePass(5f, 0f, 30, 40, 0, 0, CaptureStatus.Idle, 0f);
        r.NotePass(10f, 5f, 12, 9, 0, 0, CaptureStatus.Idle, 0f);

        StringAssert.Contains(r.Build()[0], "first standing attacker 30 defender 40, last pass 10.0 s");
    }

    [TestMethod]
    public void NotePassSkipped_RecordsStandingAndTimeButNoZoneSeconds()
    {
        var r = Armed();
        r.NotePassSkipped(5f, 0, 12);

        var lines = r.Build();

        StringAssert.Contains(lines[0], "first standing attacker 0 defender 12, last pass 5.0 s");
        StringAssert.Contains(lines[1], "rising 0.0 s falling 0.0 s contested 0.0 s idle 0.0 s");
    }

    [TestMethod]
    public void NotePass_NonFiniteDelta_AddsNoSeconds()
    {
        var r = Armed();
        r.NotePass(5f, float.NaN, 30, 40, 4, 0, CaptureStatus.Rising, 0.2f);
        StringAssert.Contains(r.Build()[1], "rising 0.0 s");
    }

    [TestMethod]
    public void NotePassCost_NonFinite_IsIgnored()
    {
        var r = Armed();
        r.NotePass(5f, 5f, 30, 40, 4, 0, CaptureStatus.Rising, 0.2f);
        r.NotePassCost(double.PositiveInfinity);
        r.NotePassCost(1.5);
        StringAssert.Contains(r.Build().Last(), "passes 1, slowest 1.50 ms, mean 1.50 ms");
    }
}
