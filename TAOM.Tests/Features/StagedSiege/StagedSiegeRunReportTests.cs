using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.StagedSiege.Domain;

namespace TAOM.Tests.Features.StagedSiege;

/// <summary>The walls run report (#1054): the facts it collects, the check verdicts and the exact lines it prints.</summary>
[TestClass]
public class StagedSiegeRunReportTests
{
    private const int MinAttackers = 3;
    private const float RefillBurstSeconds = 20f;

    private static readonly string[] CheckOrder =
    {
        "tactics", "spawn_settings", "flags", "detachment", "capture_rule", "gates", "reinforcement_frames", "refill", "softlock",
        "ai_inward", "end_chain", "victory_cue", "hud", "faults",
    };

    private static StagedSiegeRunReport New() => new(MinAttackers, RefillBurstSeconds);

    private static StagedSiegeRunReport ArmedReport(StagedSide player = StagedSide.Attacker, bool custom = false)
    {
        var r = New();
        r.NoteArmed("scene_x", player, custom, 0f);
        return r;
    }

    private static void SetUp(StagedSiegeRunReport r, bool live = true, int flags = 4)
    {
        r.NoteSetUp(1f, live, new SpawnCounts(100, 200), new SpawnCounts(80, 100));
        r.NoteFlags(flags);
    }

    // One stage: start counts, a mid pass and a capturing pass, with the reinforcements both sides got.
    private static void Stage(StagedSiegeRunReport r, StageId stage, float start, float end, int attackers = 10, bool forced = false,
        int reinforcements = 2, float distance = 5f)
    {
        if (stage != StageId.A)
            r.NoteStageStarted(stage, start, new SpawnCounts(90, 150), new SpawnCounts(70, 80));
        r.NotePass(start + 5f, 5f, stage, attackers, 2, CaptureStatus.Rising, 0.5f, 90, 70);
        r.NotePassCost(0.25);
        if (stage != StageId.A)
            for (var i = 0; i < reinforcements; i++)
            {
                r.NoteReinforcement(StagedSide.Attacker, stage, distance);
                r.NoteReinforcement(StagedSide.Defender, stage, distance);
            }

        r.NotePass(end, end - start - 5f, stage, attackers, 2, CaptureStatus.Captured, 1f, 90, 70);
        r.NotePassCost(0.5);
        r.NoteStageCaptured(stage, end, forced);
        if (stage == StageId.B)
            r.NoteGatesOpened(2);
    }

    private static StagedSiegeRunReport GoodRun()
    {
        var r = ArmedReport();
        r.NoteTacticsInstalled(2);
        SetUp(r);
        r.NoteDetachmentFormed(10);
        r.NoteDetachmentReading(10);
        r.NoteDetachmentReading(6);
        Stage(r, StageId.A, 1f, 31f);
        r.NoteDetachmentReleased(31f, "stage A captured");
        Stage(r, StageId.B, 31f, 81f);
        r.NoteSoftlockOpened(StagedSide.Defender, StageId.B, 40f);
        r.NoteSoftlockClosed(StagedSide.Defender, 43f);
        Stage(r, StageId.C, 81f, 141f);
        Stage(r, StageId.D, 141f, 201f);
        r.NoteStagedEnd(StagedEnd.AttackerWon, 201f);
        r.NoteVictoryCue(true);
        r.NoteMissionResult("AttackerVictory", true, false, new SpawnCounts(50, 0), new SpawnCounts(0, 0));
        r.NoteHud(new[] { "A:Take", "B:Take", "C:Take", "D:Take" }, new[] { "A", "B", "C", "D" });
        return r;
    }

    private static string Line(IReadOnlyList<string> lines, string id)
    {
        var line = lines.FirstOrDefault(l => l.StartsWith("CHECK " + id + " ", StringComparison.Ordinal));
        Assert.IsNotNull(line, "no CHECK line for " + id);
        return line!;
    }

    private static void AssertVerdict(StagedSiegeRunReport r, string id, string verdict, string? evidence = null)
    {
        var line = Line(r.Build(), id);
        StringAssert.StartsWith(line, "CHECK " + id + " " + verdict + ": ");
        if (evidence != null)
            StringAssert.Contains(line, evidence);
    }

    // --- the whole run -----------------------------------------------------------------------------------------------

    [TestMethod]
    public void Build_GoodRun_EveryCheckPasses()
    {
        var lines = GoodRun().Build();

        foreach (var id in CheckOrder)
            StringAssert.StartsWith(Line(lines, id), "CHECK " + id + " PASS: ");
        StringAssert.Contains(lines[lines.Count - 1], "14 checks, 14 PASS, 0 FAIL (none), 0 NOT SEEN (none)");
    }

    [TestMethod]
    public void Build_GoodRun_LinesComeInTheSpecifiedOrder()
    {
        var lines = GoodRun().Build();

        StringAssert.StartsWith(lines[0], "REPORT walls: ");
        Assert.AreEqual("REPORT stage A:", lines[1].Substring(0, 15));
        Assert.AreEqual("REPORT stage D:", lines[4].Substring(0, 15));
        var checks = lines.Where(l => l.StartsWith("CHECK ", StringComparison.Ordinal)).Select(l => l.Split(' ')[1]).ToList();
        CollectionAssert.AreEqual(CheckOrder, checks);
        StringAssert.StartsWith(lines[lines.Count - 1], "REPORT summary: ");
        Assert.AreEqual(1 + 4 + 14 + 1, lines.Count);
    }

    [TestMethod]
    public void Build_GoodRun_HeaderLineHasEveryField()
    {
        var header = GoodRun().Build()[0];

        Assert.AreEqual("REPORT walls: scene scene_x, player Attacker, campaign, armed 0.0 s, set up 1.0 s, last pass 201.0 s, "
            + "stage reached D, end AttackerWon, result AttackerVictory victory True defeated False, "
            + "survivors attacker 50+0 defender 0+0", header);
    }

    [TestMethod]
    public void Build_CustomBattle_NamesTheKind()
    {
        var header = ArmedReport(StagedSide.Defender, custom: true).Build()[0];

        StringAssert.Contains(header, "player Defender, custom battle, armed 0.0 s, set up none, last pass none");
        StringAssert.Contains(header, "stage reached none, end None, result none victory False defeated False, survivors none");
    }

    [TestMethod]
    public void Build_StageLine_CarriesTheStageFacts()
    {
        var lines = GoodRun().Build();

        var b = lines[2];
        Assert.AreEqual("REPORT stage B: start 31.0 s, end 81.0 s, duration 50.0 s, captured by rule, zone peak attacker 10 defender 2, "
            + "progress peak 1.00, rising 50.0 s falling 0.0 s contested 0.0 s idle 0.0 s, reinforcements attacker 2 defender 2, "
            + "frame max attacker 5.0 m defender 5.0 m, unmeasured 0, start counts attacker 90+150 defender 70+80, "
            + "budget holds attacker 0 defender 0, softlocks attacker 0 defender 1", b);
    }

    [TestMethod]
    public void Build_StageLine_SecondsGoToTheirStatus()
    {
        var r = ArmedReport();
        SetUp(r);
        r.NotePass(6f, 5f, StageId.A, 4, 0, CaptureStatus.Rising, 0.2f, 100, 80);
        r.NotePass(9f, 3f, StageId.A, 4, 4, CaptureStatus.Contested, 0.2f, 100, 80);
        r.NotePass(10f, 1f, StageId.A, 0, 4, CaptureStatus.Falling, 0.1f, 100, 80);
        r.NotePass(12f, 2f, StageId.A, 0, 0, CaptureStatus.Idle, 0.1f, 100, 80);

        var stage = r.Build()[1];

        StringAssert.Contains(stage, ", not captured,");
        StringAssert.Contains(stage, "rising 5.0 s falling 1.0 s contested 3.0 s idle 2.0 s");
        StringAssert.Contains(stage, "zone peak attacker 4 defender 4");
        StringAssert.Contains(stage, "progress peak 0.20");
        StringAssert.Contains(stage, "end 12.0 s, duration 11.0 s");
    }

    [TestMethod]
    public void Build_SummaryLine_CountsPassesAndCost()
    {
        var summary = GoodRun().Build().Last();

        Assert.AreEqual("REPORT summary: 14 checks, 14 PASS, 0 FAIL (none), 0 NOT SEEN (none); passes 8, slowest 0.50 ms, mean 0.38 ms; fault none",
            summary);
    }

    [TestMethod]
    public void Build_SummaryLine_ListsFailAndNotSeenIds()
    {
        var r = ArmedReport();
        r.NoteTacticsInstalled(0);
        SetUp(r, live: false);

        var summary = r.Build().Last();

        StringAssert.Contains(summary, "FAIL (tactics, spawn_settings)");
        StringAssert.Contains(summary, "NOT SEEN (detachment,");
        StringAssert.Contains(summary, "passes 0, slowest none, mean none; fault none");
    }

    [TestMethod]
    public void Build_UnderGermanCulture_PrintsInvariantNumbers()
    {
        var original = Thread.CurrentThread.CurrentCulture;
        try
        {
            Thread.CurrentThread.CurrentCulture = new CultureInfo("de-DE");
            var text = string.Join("\n", GoodRun().Build());

            StringAssert.Contains(text, "armed 0.0 s");
            StringAssert.Contains(text, "progress peak 1.00");
            StringAssert.Contains(text, "slowest 0.50 ms, mean 0.38 ms");
            Assert.IsFalse(text.Contains("0,5"), "a comma decimal leaked into the report");
        }
        finally
        {
            Thread.CurrentThread.CurrentCulture = original;
        }
    }

    [TestMethod]
    public void Build_AfterFault_ReportsTheFaultAndKeepsWhatWasSeen()
    {
        var r = ArmedReport();
        r.NoteTacticsInstalled(2);
        r.NoteFault("Tick", "NullReferenceException");

        var lines = r.Build();

        AssertVerdict(r, "faults", "FAIL", "Tick: NullReferenceException");
        AssertVerdict(r, "tactics", "PASS");
        StringAssert.Contains(lines.Last(), "fault service Tick: NullReferenceException");
    }

    [TestMethod]
    public void Faults_ServiceAndBoundary_AreKeptEachWithItsSiteAndMarked()
    {
        var r = ArmedReport();
        r.NoteFault("Tick", "InvalidOperationException");
        r.NoteBoundaryFault("MissionEnded", "NullReferenceException");
        r.NoteBoundaryFault("OnAgentBuild", "ArgumentException");

        AssertVerdict(r, "faults", "FAIL",
            "service Tick: InvalidOperationException; boundary MissionEnded: NullReferenceException; boundary OnAgentBuild: ArgumentException");
        StringAssert.Contains(r.Build().Last(), "fault service Tick: InvalidOperationException; boundary MissionEnded: NullReferenceException");
    }

    [TestMethod]
    public void Faults_BoundaryAlone_FailsAndDoesNotTouchTheOtherChecks()
    {
        var r = ArmedReport();
        r.NoteTacticsInstalled(2);
        r.NoteBoundaryFault("AfterStart", "InvalidOperationException");

        AssertVerdict(r, "faults", "FAIL", "boundary AfterStart: InvalidOperationException");
        AssertVerdict(r, "tactics", "PASS");
    }

    [TestMethod]
    public void Build_NoFault_FaultsCheckPasses() => AssertVerdict(GoodRun(), "faults", "PASS");

    // --- tactics, settings, flags ------------------------------------------------------------------------------------

    [TestMethod]
    public void Tactics_OneOrMoreTeams_Pass() => AssertVerdict(GoodRun(), "tactics", "PASS", "2 team");

    [TestMethod]
    public void Tactics_ZeroTeams_Fail()
    {
        var r = ArmedReport();
        r.NoteTacticsInstalled(0);
        AssertVerdict(r, "tactics", "FAIL", "0 team");
    }

    [TestMethod]
    public void Tactics_NeverInstalled_NotSeen() => AssertVerdict(ArmedReport(), "tactics", "NOT SEEN");

    [TestMethod]
    public void SpawnSettings_Live_Pass()
    {
        var r = ArmedReport();
        SetUp(r);
        AssertVerdict(r, "spawn_settings", "PASS", "True");
    }

    [TestMethod]
    public void SpawnSettings_NotLive_Fail()
    {
        var r = ArmedReport();
        SetUp(r, live: false);
        AssertVerdict(r, "spawn_settings", "FAIL", "False");
    }

    [TestMethod]
    public void SpawnSettings_NoSetUp_NotSeen() => AssertVerdict(ArmedReport(), "spawn_settings", "NOT SEEN");

    [TestMethod]
    public void Flags_Four_Pass()
    {
        var r = ArmedReport();
        SetUp(r);
        AssertVerdict(r, "flags", "PASS", "4 spawned");
    }

    [TestMethod]
    public void Flags_Fewer_Fail()
    {
        var r = ArmedReport();
        SetUp(r, flags: 3);
        AssertVerdict(r, "flags", "FAIL", "3 spawned");
    }

    [TestMethod]
    public void Flags_SetUpButTheCountWasNeverRead_NotSeen()
    {
        var r = ArmedReport();
        r.NoteSetUp(1f, true, new SpawnCounts(1, 1), new SpawnCounts(1, 1));
        AssertVerdict(r, "flags", "NOT SEEN", "never read");
    }

    [TestMethod]
    public void Flags_NoSetUp_NotSeen() => AssertVerdict(ArmedReport(), "flags", "NOT SEEN");

    // --- detachment --------------------------------------------------------------------------------------------------

    [TestMethod]
    public void Detachment_NeverAboveItsStart_Pass() => AssertVerdict(GoodRun(), "detachment", "PASS", "start 10, min 6, max 10");

    [TestMethod]
    public void Detachment_ReleasedIsReportedWithTheReason() =>
        AssertVerdict(GoodRun(), "detachment", "PASS", "released 31.0 s (stage A captured)");

    [TestMethod]
    public void Detachment_ReinforcementsJoined_Fail()
    {
        var r = ArmedReport();
        SetUp(r);
        r.NoteDetachmentFormed(10);
        r.NoteDetachmentReading(14);
        AssertVerdict(r, "detachment", "FAIL", "max 14");
    }

    [TestMethod]
    public void Detachment_NoneFormed_NotSeenWithTheReason()
    {
        var r = ArmedReport();
        SetUp(r);
        r.NoteDetachmentNone("defender infantry 0, share 0.2, cap 40");
        AssertVerdict(r, "detachment", "NOT SEEN", "defender infantry 0, share 0.2, cap 40");
    }

    [TestMethod]
    public void Detachment_NoSetUp_NotSeen() => AssertVerdict(ArmedReport(), "detachment", "NOT SEEN");

    // --- captures and gates ------------------------------------------------------------------------------------------

    [TestMethod]
    public void CaptureRule_ByRule_Pass() => AssertVerdict(GoodRun(), "capture_rule", "PASS", "4 stage(s) captured by the rule");

    [TestMethod]
    public void CaptureRule_OnlyForced_NotSeenSaysHowManyWereForced()
    {
        var r = ArmedReport();
        SetUp(r);
        Stage(r, StageId.A, 1f, 31f, forced: true);
        Stage(r, StageId.B, 31f, 81f, forced: true);
        AssertVerdict(r, "capture_rule", "NOT SEEN", "2 forced");
    }

    [TestMethod]
    public void Gates_BCapturedAndOpened_Pass() => AssertVerdict(GoodRun(), "gates", "PASS", "2 opened");

    [TestMethod]
    public void Gates_BCapturedNoneOpened_Fail()
    {
        var r = ArmedReport();
        SetUp(r);
        Stage(r, StageId.A, 1f, 31f);
        r.NoteStageStarted(StageId.B, 31f, new SpawnCounts(1, 1), new SpawnCounts(1, 1));
        r.NoteStageCaptured(StageId.B, 60f, false);
        AssertVerdict(r, "gates", "FAIL", "0 opened");
    }

    [TestMethod]
    public void Gates_BNotCaptured_NotSeen()
    {
        var r = ArmedReport();
        SetUp(r);
        Stage(r, StageId.A, 1f, 31f);
        AssertVerdict(r, "gates", "NOT SEEN");
    }

    // --- reinforcement frames ----------------------------------------------------------------------------------------

    [TestMethod]
    public void ReinforcementFrames_AllWithinTolerance_Pass() => AssertVerdict(GoodRun(), "reinforcement_frames", "PASS", "max 5.0 m");

    [TestMethod]
    public void ReinforcementFrames_ExactlyAtTolerance_Pass()
    {
        var r = ArmedReport();
        SetUp(r);
        r.NoteStageStarted(StageId.B, 31f, new SpawnCounts(1, 1), new SpawnCounts(1, 1));
        r.NoteReinforcement(StagedSide.Attacker, StageId.B, StagedSiegeRunReport.FrameToleranceMetres);
        AssertVerdict(r, "reinforcement_frames", "PASS");
    }

    [TestMethod]
    public void ReinforcementFrames_AboveTolerance_Fail()
    {
        var r = ArmedReport();
        SetUp(r);
        r.NoteStageStarted(StageId.B, 31f, new SpawnCounts(1, 1), new SpawnCounts(1, 1));
        r.NoteReinforcement(StagedSide.Defender, StageId.B, 30f);
        AssertVerdict(r, "reinforcement_frames", "FAIL", "30.0 m");
    }

    [TestMethod]
    public void ReinforcementFrames_NoSample_NotSeen()
    {
        var r = ArmedReport();
        SetUp(r);
        r.NoteStageStarted(StageId.B, 31f, new SpawnCounts(1, 1), new SpawnCounts(1, 1));
        r.NoteReinforcement(StagedSide.Attacker, StageId.B, null);
        AssertVerdict(r, "reinforcement_frames", "NOT SEEN");
    }

    [DataTestMethod]
    [DataRow(float.NaN)]
    [DataRow(float.PositiveInfinity)]
    [DataRow(float.NegativeInfinity)]
    public void ReinforcementFrames_NonFiniteDistance_IsUnmeasuredAndNeverPasses(float distance)
    {
        var r = ArmedReport();
        SetUp(r);
        r.NoteStageStarted(StageId.B, 31f, new SpawnCounts(1, 1), new SpawnCounts(1, 1));
        r.NoteReinforcement(StagedSide.Attacker, StageId.B, distance);

        AssertVerdict(r, "reinforcement_frames", "NOT SEEN", "1 unmeasured");
        StringAssert.Contains(r.Build()[2], "frame max attacker none");
    }

    [TestMethod]
    public void ReinforcementFrames_NonFiniteBesideAMeasuredOne_NeverEntersTheMaximum()
    {
        var r = ArmedReport();
        SetUp(r);
        r.NoteStageStarted(StageId.B, 31f, new SpawnCounts(1, 1), new SpawnCounts(1, 1));
        r.NoteReinforcement(StagedSide.Attacker, StageId.B, 4f);
        r.NoteReinforcement(StagedSide.Attacker, StageId.B, float.NaN);
        r.NoteReinforcement(StagedSide.Attacker, StageId.B, float.PositiveInfinity);

        AssertVerdict(r, "reinforcement_frames", "PASS", "max 4.0 m");
        StringAssert.Contains(r.Build()[2], "reinforcements attacker 3");
        StringAssert.Contains(r.Build()[2], "unmeasured 2");
    }

    // --- refill ------------------------------------------------------------------------------------------------------

    [TestMethod]
    public void Refill_EverySideGotReinforcements_Pass() => AssertVerdict(GoodRun(), "refill", "PASS");

    [TestMethod]
    public void Refill_ALongStageWithNone_Fail()
    {
        var r = ArmedReport();
        SetUp(r);
        Stage(r, StageId.A, 1f, 31f);
        Stage(r, StageId.B, 31f, 81f, reinforcements: 0);
        AssertVerdict(r, "refill", "FAIL", "stage B attacker 0");
    }

    [TestMethod]
    public void Refill_ASideTheBudgetHeldOnPurpose_IsNotAMissAndIsNamed()
    {
        var r = ArmedReport();
        SetUp(r);
        Stage(r, StageId.A, 1f, 31f);
        r.NoteStageStarted(StageId.C, 31f, new SpawnCounts(90, 150), new SpawnCounts(70, 80));
        r.NoteBudgetHeld(StagedSide.Defender, StageId.C);
        r.NoteReinforcement(StagedSide.Attacker, StageId.C, 5f);
        r.NotePass(81f, 50f, StageId.C, 10, 2, CaptureStatus.Captured, 1f, 90, 70);
        r.NoteStageCaptured(StageId.C, 81f, false);
        AssertVerdict(r, "refill", "PASS", "stage C defender held by budget");
    }

    [TestMethod]
    public void Refill_AHoldOnOneSide_DoesNotExcuseTheOther()
    {
        var r = ArmedReport();
        SetUp(r);
        Stage(r, StageId.A, 1f, 31f);
        r.NoteStageStarted(StageId.C, 31f, new SpawnCounts(90, 150), new SpawnCounts(70, 80));
        r.NoteBudgetHeld(StagedSide.Defender, StageId.C);
        r.NotePass(81f, 50f, StageId.C, 10, 2, CaptureStatus.Captured, 1f, 90, 70);
        r.NoteStageCaptured(StageId.C, 81f, false);
        AssertVerdict(r, "refill", "FAIL", "stage C attacker 0");
    }

    [TestMethod]
    public void Refill_ASideWithNoReserve_IsNotRequired()
    {
        var r = ArmedReport();
        SetUp(r);
        Stage(r, StageId.A, 1f, 31f);
        r.NoteStageStarted(StageId.B, 31f, new SpawnCounts(90, 0), new SpawnCounts(70, 0));
        r.NotePass(60f, 29f, StageId.B, 10, 2, CaptureStatus.Captured, 1f, 90, 70);
        r.NoteStageCaptured(StageId.B, 60f, false);
        AssertVerdict(r, "refill", "PASS");
    }

    [TestMethod]
    public void Refill_OnlyShortStages_NotSeen()
    {
        var r = ArmedReport();
        SetUp(r);
        Stage(r, StageId.A, 1f, 31f);
        Stage(r, StageId.B, 31f, 41f, reinforcements: 0);
        AssertVerdict(r, "refill", "NOT SEEN");
    }

    [TestMethod]
    public void Refill_NothingBeyondA_NotSeen()
    {
        var r = ArmedReport();
        SetUp(r);
        AssertVerdict(r, "refill", "NOT SEEN");
    }

    // --- softlock ----------------------------------------------------------------------------------------------------

    [TestMethod]
    public void Softlock_ClosedQuickly_Pass() => AssertVerdict(GoodRun(), "softlock", "PASS", "longest 3.0 s");

    [TestMethod]
    public void Softlock_SpanOfFifteenSeconds_Fail()
    {
        var r = ArmedReport();
        SetUp(r);
        r.NoteSoftlockOpened(StagedSide.Attacker, StageId.A, 10f);
        r.NoteSoftlockClosed(StagedSide.Attacker, 25f);
        AssertVerdict(r, "softlock", "FAIL", "15.0 s");
    }

    [TestMethod]
    public void Softlock_JustUnderTheLimit_Pass()
    {
        var r = ArmedReport();
        SetUp(r);
        r.NoteSoftlockOpened(StagedSide.Attacker, StageId.A, 10f);
        r.NoteSoftlockClosed(StagedSide.Attacker, 24.9f);
        AssertVerdict(r, "softlock", "PASS");
    }

    [TestMethod]
    public void Softlock_StillOpenAtTheEnd_CountsUpToTheLastPass()
    {
        var r = ArmedReport();
        SetUp(r);
        r.NoteSoftlockOpened(StagedSide.Defender, StageId.A, 100f);
        r.NotePass(130f, 5f, StageId.A, 1, 1, CaptureStatus.Contested, 0f, 10, 0);

        AssertVerdict(r, "softlock", "FAIL", "defender 30.0 s");
    }

    [TestMethod]
    public void Softlock_OpenedTwice_KeepsTheLongestSpan()
    {
        var r = ArmedReport();
        SetUp(r);
        r.NoteSoftlockOpened(StagedSide.Attacker, StageId.A, 10f);
        r.NoteSoftlockClosed(StagedSide.Attacker, 12f);
        r.NoteSoftlockOpened(StagedSide.Attacker, StageId.A, 20f);
        r.NoteSoftlockClosed(StagedSide.Attacker, 28f);
        AssertVerdict(r, "softlock", "PASS", "longest 8.0 s");
    }

    [TestMethod]
    public void Softlock_ApprovedAgainWhileOpen_IsNotANewSpan()
    {
        var r = ArmedReport();
        SetUp(r);
        r.NoteSoftlockOpened(StagedSide.Attacker, StageId.A, 10f);
        r.NoteSoftlockOpened(StagedSide.Attacker, StageId.A, 14f);
        r.NoteSoftlockClosed(StagedSide.Attacker, 16f);
        AssertVerdict(r, "softlock", "PASS", "longest 6.0 s");
    }

    [TestMethod]
    public void Softlock_APassWithSomeoneStanding_ClosesTheSpan()
    {
        var r = ArmedReport();
        SetUp(r);
        r.NoteSoftlockOpened(StagedSide.Attacker, StageId.A, 10f);
        r.NotePass(14f, 4f, StageId.A, 1, 1, CaptureStatus.Contested, 0f, 5, 5);
        r.NotePass(60f, 46f, StageId.A, 1, 1, CaptureStatus.Contested, 0f, 5, 5);

        AssertVerdict(r, "softlock", "PASS", "longest 4.0 s");
    }

    [TestMethod]
    public void Softlock_NonFiniteTime_NeverPasses()
    {
        var r = ArmedReport();
        SetUp(r);
        r.NoteSoftlockOpened(StagedSide.Attacker, StageId.A, 10f);
        r.NoteSoftlockClosed(StagedSide.Attacker, float.NaN);
        AssertVerdict(r, "softlock", "FAIL");
    }

    [TestMethod]
    public void Softlock_None_NotSeen() => AssertVerdict(ArmedReport(), "softlock", "NOT SEEN");

    // --- ai inward ---------------------------------------------------------------------------------------------------

    [TestMethod]
    public void AiInward_CAndDHadAttackers_Pass() => AssertVerdict(GoodRun(), "ai_inward", "PASS", "C peak 10, D peak 10");

    [TestMethod]
    public void AiInward_ALongCWithFewAttackers_Fail()
    {
        var r = ArmedReport();
        SetUp(r);
        r.NoteStageStarted(StageId.C, 81f, new SpawnCounts(1, 1), new SpawnCounts(1, 1));
        r.NotePass(211f, 130f, StageId.C, 1, 5, CaptureStatus.Falling, 0f, 1, 1);
        AssertVerdict(r, "ai_inward", "FAIL", "C peak 1");
    }

    [TestMethod]
    public void AiInward_CNotReached_NotSeen()
    {
        var r = ArmedReport();
        SetUp(r);
        Stage(r, StageId.A, 1f, 31f);
        AssertVerdict(r, "ai_inward", "NOT SEEN", "C not reached");
    }

    [TestMethod]
    public void AiInward_CTooShortToJudge_NotSeen()
    {
        var r = ArmedReport();
        SetUp(r);
        r.NoteStageStarted(StageId.C, 81f, new SpawnCounts(1, 1), new SpawnCounts(1, 1));
        r.NotePass(100f, 19f, StageId.C, 1, 5, CaptureStatus.Falling, 0f, 1, 1);
        AssertVerdict(r, "ai_inward", "NOT SEEN", "too short");
    }

    // --- end chain, cue, hud -----------------------------------------------------------------------------------------

    [DataTestMethod]
    [DataRow(StagedEnd.DefenderPushedBack, "DefenderPullBack", false, false)]
    [DataRow(StagedEnd.AttackerWon, "AttackerVictory", true, false)]
    [DataRow(StagedEnd.PlayerDefeated, "AttackerVictory", false, true)]
    public void EndChain_ResultMatchesTheEnd_Pass(StagedEnd end, string state, bool victory, bool defeated)
    {
        var r = ArmedReport();
        SetUp(r);
        r.NoteStagedEnd(end, 100f);
        r.NoteMissionResult(state, victory, defeated, null, null);
        AssertVerdict(r, "end_chain", "PASS", state);
    }

    [DataTestMethod]
    [DataRow(StagedEnd.DefenderPushedBack, "AttackerVictory", true, false)]
    [DataRow(StagedEnd.AttackerWon, "AttackerVictory", false, false)]
    [DataRow(StagedEnd.AttackerWon, "DefenderVictory", true, false)]
    [DataRow(StagedEnd.PlayerDefeated, "AttackerVictory", false, false)]
    public void EndChain_DifferentResult_Fail(StagedEnd end, string state, bool victory, bool defeated)
    {
        var r = ArmedReport();
        SetUp(r);
        r.NoteStagedEnd(end, 100f);
        r.NoteMissionResult(state, victory, defeated, null, null);
        AssertVerdict(r, "end_chain", "FAIL");
    }

    [TestMethod]
    public void EndChain_EndWithNoResult_Fail()
    {
        var r = ArmedReport();
        SetUp(r);
        r.NoteStagedEnd(StagedEnd.AttackerWon, 100f);
        AssertVerdict(r, "end_chain", "FAIL", "no result");
    }

    [TestMethod]
    public void EndChain_NoStagedEnd_NotSeenNamesTheResultAndStage()
    {
        var r = ArmedReport();
        SetUp(r);
        Stage(r, StageId.A, 1f, 31f);
        r.NoteMissionResult("AttackerVictory", true, false, null, null);
        AssertVerdict(r, "end_chain", "NOT SEEN", "no staged end; result AttackerVictory victory True defeated False; stage reached A");
    }

    [TestMethod]
    public void VictoryCue_AttackerWonAndRan_Pass() => AssertVerdict(GoodRun(), "victory_cue", "PASS");

    [TestMethod]
    public void VictoryCue_AttackerWonAndThrew_Fail()
    {
        var r = ArmedReport();
        SetUp(r);
        r.NoteStagedEnd(StagedEnd.AttackerWon, 100f);
        r.NoteVictoryCue(false);
        AssertVerdict(r, "victory_cue", "FAIL", "threw");
    }

    [TestMethod]
    public void VictoryCue_AttackerWonButTheEndNeverReachedMissionEnded_NotSeen()
    {
        var r = ArmedReport();
        SetUp(r);
        r.NoteStagedEnd(StagedEnd.AttackerWon, 100f);
        AssertVerdict(r, "victory_cue", "NOT SEEN",
            "AttackerWon, but the staged end never reached MissionEnded (vanilla ended the battle first, or a boundary fault, see faults)");
    }

    [TestMethod]
    public void VictoryCue_OtherEnd_NotSeen()
    {
        var r = ArmedReport();
        SetUp(r);
        r.NoteStagedEnd(StagedEnd.DefenderPushedBack, 100f);
        AssertVerdict(r, "victory_cue", "NOT SEEN", "DefenderPushedBack");
    }

    [TestMethod]
    public void Hud_EveryReachedLetterShownWithTheRightKind_Pass() => AssertVerdict(GoodRun(), "hud", "PASS", "A:Take, B:Take, C:Take, D:Take");

    [TestMethod]
    public void Hud_DefenderPlayerNeedsHold()
    {
        var r = ArmedReport(StagedSide.Defender);
        SetUp(r);
        Stage(r, StageId.A, 1f, 31f);
        r.NoteHud(new[] { "A:Hold" }, new[] { "A" });
        AssertVerdict(r, "hud", "PASS");
    }

    [TestMethod]
    public void Hud_WrongKind_Fail()
    {
        var r = ArmedReport(StagedSide.Defender);
        SetUp(r);
        Stage(r, StageId.A, 1f, 31f);
        r.NoteHud(new[] { "A:Take" }, new[] { "A" });
        AssertVerdict(r, "hud", "FAIL", "A shown as Take, expected Hold");
    }

    [TestMethod]
    public void Hud_ALetterNeverShown_Fail()
    {
        var r = ArmedReport();
        SetUp(r);
        Stage(r, StageId.A, 1f, 31f);
        Stage(r, StageId.B, 31f, 81f);
        r.NoteHud(new[] { "A:Take" }, new[] { "A" });
        AssertVerdict(r, "hud", "FAIL", "B never shown");
    }

    [TestMethod]
    public void Hud_PrintsTheMarkerLettersWithoutJudgingThem()
    {
        AssertVerdict(GoodRun(), "hud", "PASS", "shown A:Take, B:Take, C:Take, D:Take; markers A, B, C, D");

        var r = ArmedReport();
        SetUp(r);
        Stage(r, StageId.A, 1f, 31f);
        r.NoteHud(new[] { "A:Take" }, Array.Empty<string>());
        AssertVerdict(r, "hud", "PASS", "shown A:Take; markers none");
    }

    [TestMethod]
    public void Hud_ShownButNoStageReached_NotSeen()
    {
        var r = ArmedReport();
        r.NoteHud(new[] { "A:Take" }, new[] { "A" });
        AssertVerdict(r, "hud", "NOT SEEN", "no stage reached");
    }

    [TestMethod]
    public void Hud_NeverRan_NotSeen()
    {
        var r = ArmedReport();
        SetUp(r);
        Stage(r, StageId.A, 1f, 31f);
        r.NoteHud(null, null);
        AssertVerdict(r, "hud", "NOT SEEN", "never ran");
    }

    [TestMethod]
    public void Hud_NothingReportedAtAll_NotSeen() => AssertVerdict(ArmedReport(), "hud", "NOT SEEN");

    [TestMethod]
    public void Hud_AStageSkippedByForce_IsNotExpected()
    {
        var r = ArmedReport();
        SetUp(r);
        r.NoteStageCaptured(StageId.A, 20f, true);
        r.NoteStageStarted(StageId.D, 20f, new SpawnCounts(1, 1), new SpawnCounts(1, 1));
        r.NoteHud(new[] { "A:Take", "D:Take" }, new[] { "A", "D" });
        AssertVerdict(r, "hud", "PASS");
    }

    // --- facts the stage lines carry ---------------------------------------------------------------------------------

    [TestMethod]
    public void StageLine_BudgetHoldsAreCountedPerSideAndStage()
    {
        var r = ArmedReport();
        SetUp(r);
        r.NoteStageStarted(StageId.B, 31f, new SpawnCounts(1, 1), new SpawnCounts(1, 1));
        r.NoteBudgetHeld(StagedSide.Defender, StageId.B);
        r.NoteBudgetHeld(StagedSide.Defender, StageId.B);
        r.NoteBudgetHeld(StagedSide.Attacker, StageId.A);

        var lines = r.Build();

        StringAssert.Contains(lines[1], "budget holds attacker 1 defender 0");
        StringAssert.Contains(lines[2], "budget holds attacker 0 defender 2");
    }

    [TestMethod]
    public void StageLine_NotCaptured_SaysSo()
    {
        var r = ArmedReport();
        SetUp(r);
        StringAssert.Contains(r.Build()[1], ", not captured,");
    }

    [TestMethod]
    public void StageLine_ForcedCapture_SaysForced()
    {
        var r = ArmedReport();
        SetUp(r);
        r.NoteStageCaptured(StageId.A, 20f, true);
        StringAssert.Contains(r.Build()[1], "captured by forced");
    }

    [TestMethod]
    public void StageLine_SkippedByForce_IsLabelled()
    {
        var r = ArmedReport();
        SetUp(r);
        r.NoteStageCaptured(StageId.A, 20f, true);
        r.NoteStageCaptured(StageId.B, 20f, true);
        r.NoteStageStarted(StageId.C, 20f, new SpawnCounts(1, 1), new SpawnCounts(1, 1));

        var lines = r.Build();

        Assert.AreEqual("REPORT stage B: skipped, captured by forced at 20.0 s", lines[2]);
    }

    [TestMethod]
    public void NotePass_NonFiniteDelta_AddsNoSeconds()
    {
        var r = ArmedReport();
        SetUp(r);
        r.NotePass(6f, float.NaN, StageId.A, 4, 0, CaptureStatus.Rising, 0.2f, 100, 80);
        StringAssert.Contains(r.Build()[1], "rising 0.0 s");
    }

    [TestMethod]
    public void NotePassCost_NonFinite_IsIgnored()
    {
        var r = ArmedReport();
        SetUp(r);
        r.NotePass(6f, 5f, StageId.A, 4, 0, CaptureStatus.Rising, 0.2f, 100, 80);
        r.NotePassCost(double.NaN);
        r.NotePassCost(2.0);
        StringAssert.Contains(r.Build().Last(), "passes 1, slowest 2.00 ms, mean 2.00 ms");
    }
}
