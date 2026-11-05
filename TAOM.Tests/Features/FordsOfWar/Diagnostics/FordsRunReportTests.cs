using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Newtonsoft.Json.Linq;
using TAOM.Core.Logging;
using TAOM.Features.FordsOfWar;
using TAOM.Features.FordsOfWar.Diagnostics;
using TAOM.Features.FordsOfWar.Domain;

namespace TAOM.Tests.Features.FordsOfWar.Diagnostics;

[TestClass]
public class FordsRunReportTests
{
    private sealed class CaptureLogger : IModLogger
    {
        private readonly object _gate = new();
        private readonly List<string> _lines = new();

        public CaptureLogger(string? path) => LogFilePath = path;

        public string? LogFilePath { get; }

        public List<string> Lines
        {
            get
            {
                lock (_gate)
                    return _lines.ToList();
            }
        }

        public void LogInfo(string message) => Add("INFO " + message);

        public void LogDebug(string message) => Add("DEBUG " + message);

        public void LogWarning(string message) => Add("WARN " + message);

        public void LogError(string message) => Add("ERROR " + message);

        public void Dispose()
        {
        }

        private void Add(string line)
        {
            lock (_gate)
                _lines.Add(line);
        }
    }

    private static readonly FordsMatchSetup Setup = new("scene_a", "gondor", "mordor", "hard", 2);
    private static readonly FordsMatchFacts Facts = new(60f, 8f, 12f, 2, 150, 200);

    private string _dir = "";
    private CaptureLogger _log = null!;
    private FordsRunReport _report = null!;

    [TestMethod]
    public void Volley_ARock_CountsApartFromTheJavelinsAndArrows_SoTheHitRateStaysLikeForLike()
    {
        StartedRun();

        _report.Volley(true, 3, rock: true);
        _report.Volley(true, 3);

        Assert.AreEqual(5, _report.Run!.Volley.Launched);
        Assert.AreEqual(1, _report.Run.Volley.RocksLaunched);
    }

    [TestMethod]
    public void TheReportJson_CarriesTheEndKeysTheTools_Read()
    {
        StartedRun();
        _report.EndedOnTime();
        _report.Winner(MatchSide.West);

        var end = JObject.Parse(File.ReadAllText(ReportPath(1)))["evidence"]!["End"]!;
        Assert.IsTrue((bool)end["EndedOnTime"]!, "tools/fords_match_review.py reads End.EndedOnTime");
        Assert.IsNotNull((double?)end["WinnerMt"], "tools/fords_match_review.py reads End.WinnerMt");
    }

    [TestMethod]
    public void EndedOnTime_IsRecorded()
    {
        StartedRun();
        Assert.IsFalse(_report.Run!.End.EndedOnTime);

        _report.EndedOnTime();

        Assert.IsTrue(_report.Run.End.EndedOnTime);
    }

    [TestInitialize]
    public void Init()
    {
        _dir = Path.Combine(Path.GetTempPath(), "fords_run_report_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_dir);
        _log = new CaptureLogger(Path.Combine(_dir, "taom_debug_2026-10-31_12-30-45.log"));
        _report = new FordsRunReport(_log);
    }

    [TestCleanup]
    public void Cleanup()
    {
        try { Directory.Delete(_dir, true); } catch (IOException) { }
    }

    private string ReportPath(int run) => Path.Combine(_dir, "fords_report_2026-10-31_12-30-45_run" + run + ".json");

    private List<string> Verdicts(string finalMarker) =>
        _log.Lines.Where(l => l.Contains("[FordsOfWar][Verdict]") && l.Contains(" final=" + finalMarker + " check=")).ToList();

    private int Totals(string finalMarker) =>
        _log.Lines.Count(l => l.Contains("[FordsOfWar][Verdict]") && l.Contains(" final=" + finalMarker + " total "));

    private static List<TowerSlot> Towers() => new()
    {
        new TowerSlot("keep_west", MatchSide.West, -1, 99, TowerKind.Keep, new Vec3f(0, 0, 0), 0f),
        new TowerSlot("keep_east", MatchSide.East, -1, 99, TowerKind.Keep, new Vec3f(900, 0, 0), 0f),
        new TowerSlot("east_outer_0", MatchSide.East, 0, 0, TowerKind.Tower, new Vec3f(600, 0, 0), 0f),
    };

    private void StartedRun()
    {
        _report.BeginRun("menu", Setup);
        _report.MatchStarted(Facts, Towers(), 3);
    }

    [TestMethod]
    public void BeginRun_LogsAbsoluteLogAndReportPaths_AndNumbersRuns()
    {
        var first = _report.BeginRun("menu", Setup);
        var second = _report.BeginRun("console", Setup);

        Assert.AreEqual(1, first);
        Assert.AreEqual(2, second);
        var begin = _log.Lines.Last(l => l.Contains("[FordsOfWar][Run]") && l.Contains("begin"));
        StringAssert.Contains(begin, "run=2");
        StringAssert.Contains(begin, "trigger=console");
        StringAssert.Contains(begin, "log=\"" + Path.GetFullPath(_log.LogFilePath!) + "\"");
        StringAssert.Contains(begin, "report=\"" + ReportPath(2) + "\"");
        Assert.IsTrue(Path.IsPathRooted(ReportPath(2)));
    }

    [TestMethod]
    public void BeginRun_TheSetupText_SaysWhetherAnAiPlaysTheWestSideToo()
    {
        _report.BeginRun("autorun", Setup with { WestAi = true });
        StringAssert.EndsWith(_report.Run!.Setup, " west_ai=1");

        _report.BeginRun("menu", Setup);
        StringAssert.EndsWith(_report.Run!.Setup, " west_ai=0");
    }

    [TestMethod]
    public void ConsoleCommand_AfterAnAutorunBegan_IsNotBoundToTheAutorunRun()
    {
        _report.BeginRun("autorun", Setup);

        _report.ConsoleCommand("fords_gold", new[] { "100" }, true, "No match");

        Assert.AreEqual(0, _report.Session.Console.Last().Run, "only taom.fords_start parks its run for the record that follows");
    }

    [TestMethod]
    public void Check06_GoldGrantOfAnEarlierRun_IsNotCountedForTheNextRun()
    {
        _report.BeginRun("menu", Setup);
        _report.Stage("live");
        _report.ConsoleCommand("fords_gold", new[] { "100" }, true, "Added 100 gold. You now hold 700.");
        _report.MissionClosed();
        _report.MainMenuReturned();

        _report.BeginRun("menu", Setup);
        _report.Stage("live");
        _report.PanelAttached();
        _report.Command("BuyPack", "Ok");
        _report.PackBoughtLine();

        var row = FordsVerdicts.Judge(_report.Session, _report.Run!)[5];

        Assert.AreEqual(6, row.Check);
        Assert.AreEqual(FordsResult.NotSeen, row.Parts.Single(p => p.Name == "grant").Result, "run 2 never granted gold");
    }

    [TestMethod]
    public void Check06_GoldGrantOfTheSameRun_StillPasses()
    {
        _report.BeginRun("menu", Setup);
        _report.Stage("live");
        _report.ConsoleCommand("fords_gold", new[] { "100" }, true, "Added 100 gold. You now hold 700.");

        var row = FordsVerdicts.Judge(_report.Session, _report.Run!)[5];

        Assert.AreEqual(FordsResult.Pass, row.Parts.Single(p => p.Name == "grant").Result);
    }

    [TestMethod]
    public void ConsoleCommand_AfterAConsoleStartBegan_IsBoundToThatRun()
    {
        var number = _report.BeginRun("console", Setup);

        _report.ConsoleCommand("fords_start", new string[0], true, "Starting");

        Assert.AreEqual(number, _report.Session.Console.Last().Run);
    }

    [TestMethod]
    public void BeginRun_ResetsEveryPerRunCounterAndCap()
    {
        StartedRun();
        _report.Stage("live");
        for (var i = 0; i < 15; i++)
            _report.TowerBlow("east_outer_0", TowerBlowClass.Melee, TowerDamageReason.Applied, 5, 5, new FordsAttacker(MatchSide.West));
        _report.CameraFrame(true, false, false, true, false, false);
        _report.Paused(true);
        _report.OptionsRequested();
        var firstEvidence = _report.Run!;

        _report.BeginRun("menu", Setup);

        var run = _report.Run!;
        Assert.AreNotSame(firstEvidence, run);
        Assert.AreEqual(2, run.Run);
        Assert.IsFalse(run.Live);
        Assert.AreEqual(0, run.Logged[(int)FordsLogCategory.TowerBlow]);
        Assert.AreEqual(0, run.Client.OwnedFrames);
        Assert.AreEqual(0, run.Client.PauseEdges);
        Assert.AreEqual(0, run.Options.Requested);
        Assert.AreEqual(0.0, _report.MissionSeconds);
        Assert.AreEqual(2, _report.Session.RunsBegun);
    }

    [TestMethod]
    public void ClientMembers_BeforeAnyRun_DoNothing()
    {
        _report.CameraFrame(true, true, true, true, true, true);
        _report.CameraInside(false);
        _report.CameraFailed();
        _report.PhotoVetoed();
        _report.Paused(true);
        _report.PanelAttached();
        _report.Command("Order:Hold", "Ok");
        _report.PackBoughtLine();

        Assert.IsNull(_report.Run);
        Assert.AreEqual(0, _log.Lines.Count);
    }

    [TestMethod]
    public void Winner_WritesAFinal0PassAndTheJson()
    {
        StartedRun();

        _report.Winner(MatchSide.West);

        Assert.AreEqual(16, Verdicts("0").Count);
        Assert.AreEqual(1, Totals("0"));
        var json = JObject.Parse(File.ReadAllText(ReportPath(1)));
        Assert.AreEqual(2, (int)json["schema"]!);
        Assert.AreEqual(1, (int)json["run"]!);
        Assert.IsFalse((bool)json["final"]!);
        Assert.AreEqual("result", (string?)json["reason"]);
        Assert.AreEqual("West", (string?)json["evidence"]!["End"]!["Winner"]);
        Assert.AreEqual(16, ((JArray)json["verdicts"]!).Count);
        Assert.AreEqual("Fail", (string?)json["verdicts"]![0]!["Result"], "an empty session fails check 1 'installed'; enums are strings");
    }

    [TestMethod]
    public void MissionClosed_WritesAFinal0Pass()
    {
        StartedRun();

        _report.MissionClosed();

        Assert.AreEqual(16, Verdicts("0").Count);
        Assert.IsTrue(_report.Run!.Closed);
        Assert.AreEqual("mission-end", (string?)JObject.Parse(File.ReadAllText(ReportPath(1)))["reason"]);
    }

    [TestMethod]
    public void MainMenuReturned_AfterClose_WritesFinal1Once()
    {
        StartedRun();
        _report.MissionClosed();

        _report.MainMenuReturned();
        _report.MainMenuReturned();

        Assert.AreEqual(16, Verdicts("1").Count);
        Assert.AreEqual(1, Totals("1"));
        var json = JObject.Parse(File.ReadAllText(ReportPath(1)));
        Assert.IsTrue((bool)json["final"]!);
        Assert.AreEqual("menu-return", (string?)json["reason"]);
        Assert.IsNotNull(_report.Run!.End.MenuReturnedWall);
    }

    [TestMethod]
    public void MainMenuReturned_WithNoClosedRun_DoesNothing()
    {
        _report.MainMenuReturned();

        Assert.AreEqual(0, _log.Lines.Count);
        Assert.IsFalse(File.Exists(ReportPath(1)));
    }

    [TestMethod]
    public void MainMenuReturned_WithAnOpenRun_RecordsRouteBAndFinalizes()
    {
        StartedRun();

        _report.MainMenuReturned();

        var run = _report.Run!;
        Assert.AreEqual("B", run.Failure!.Route);
        StringAssert.Contains(run.Failure.Reason, "the game ended before the match mission closed");
        Assert.IsTrue(run.Closed);
        Assert.AreEqual(16, Verdicts("1").Count);
        Assert.AreEqual(0, Verdicts("0").Count);
    }

    [TestMethod]
    public void NoticeAcknowledged_AfterClose_WritesFinal1Again()
    {
        StartedRun();
        _report.Failure("B", "the launcher opened no mission");
        _report.NoticeRequested(true);
        _report.MainMenuReturned();
        var before = Totals("1");

        _report.NoticeAcknowledged();

        Assert.AreEqual(before + 1, Totals("1"));
        Assert.IsTrue(_report.Run!.End.AckAfterMenu);
        Assert.AreEqual("notice-ok", (string?)JObject.Parse(File.ReadAllText(ReportPath(1)))["reason"]);
    }

    private string NoticePart(int run)
    {
        var verdicts = (JArray)JObject.Parse(File.ReadAllText(ReportPath(run)))["verdicts"]!;
        var check15 = verdicts.Single(v => (int)v["Check"]! == 15);
        return (string)check15["Parts"]!.Single(x => (string)x["Name"]! == "notice")["Result"]!;
    }

    [TestMethod]
    public void BeginRun_AfterANoticeNobodyAcknowledged_RewritesThePreviousRunWithNoticeFail()
    {
        StartedRun();
        _report.Failure("B", "the launcher opened no mission");
        _report.NoticeRequested(true);
        _report.MainMenuReturned();
        Assert.AreEqual("NotSeen", NoticePart(1));
        var before = Totals("1");

        _report.BeginRun("menu", Setup);

        Assert.AreEqual(before + 1, Totals("1"));
        Assert.AreEqual("Fail", NoticePart(1));
        Assert.AreEqual("notice-unacknowledged", (string?)JObject.Parse(File.ReadAllText(ReportPath(1)))["reason"]);
    }

    [TestMethod]
    public void BeginRun_AfterAnAcknowledgedNotice_DoesNotRewriteThePreviousRun()
    {
        StartedRun();
        _report.Failure("B", "the launcher opened no mission");
        _report.NoticeRequested(true);
        _report.MainMenuReturned();
        _report.NoticeAcknowledged();
        var before = Totals("1");

        _report.BeginRun("menu", Setup);

        Assert.AreEqual(before, Totals("1"));
        Assert.AreEqual("Pass", NoticePart(1));
    }

    [TestMethod]
    public void PromptFaultAfterAClosedRun_IsStampedWithRunZero()
    {
        StartedRun();
        _report.Failure("B", "the launcher opened no mission");

        _report.Failure("C", "a prompt step threw");

        var line = _log.Lines.Single(l => l.Contains("failure route=C"));
        StringAssert.Contains(line, "run=0 ");
    }

    [TestMethod]
    public void NoticeAcknowledged_BeforeMenu_SetsAckAfterMenuFalse()
    {
        StartedRun();
        _report.Failure("B", "x");
        _report.NoticeRequested(true);

        _report.NoticeAcknowledged();

        Assert.IsFalse(_report.Run!.End.AckAfterMenu);
        Assert.IsNotNull(_report.Run.End.NoticeAckWall);
    }

    [TestMethod]
    public void FailureRouteC_FinalizesAtOnce()
    {
        _report.BeginRun("console", Setup);

        _report.Failure("C", "the game manager factory returned null");

        Assert.AreEqual(16, Verdicts("1").Count);
        Assert.IsTrue(_report.Run!.Closed);
        Assert.IsTrue(_log.Lines.Any(l => l.StartsWith("WARN") && l.Contains("failure route=C")));
        _report.Failure("A", "later");
        Assert.AreEqual("C", _report.Run.Failure!.Route, "the first failure wins");
    }

    [TestMethod]
    public void MainMenuReturned_AfterARouteCFailure_WritesNoSecondFinalPass()
    {
        _report.BeginRun("console", Setup);
        _report.Failure("C", "the game manager factory returned null");
        Assert.AreEqual(1, Totals("1"));

        _report.MainMenuReturned();

        Assert.AreEqual(1, Totals("1"), "the run never left the menu: its return is not a second final pass");
        Assert.IsNull(_report.Run!.End.MenuReturnedWall);
    }

    [TestMethod]
    public void MenuEnds_AfterAFinalizedRun_RewriteItsFinalPass()
    {
        StartedRun();
        _report.MissionClosed();
        _report.MainMenuReturned();
        Assert.AreEqual(1, Totals("1"));

        _report.MenuClicked();
        _report.PromptShown("faction");
        _report.PromptCancelled("faction");
        Assert.AreEqual(2, Totals("1"), "a cancel after the last run is judged by check 1");
        Assert.AreEqual("Pass", (string?)JObject.Parse(File.ReadAllText(ReportPath(1)))["verdicts"]![0]!["Parts"]![3]!["Result"]);

        _report.MenuClicked();
        _report.PromptShown("faction");
        _report.MenuClicked();
        Assert.AreEqual(3, Totals("1"), "an abandoned flow is judged too");

        _report.PromptShown("faction");
        _report.Failure("C", "boom");
        Assert.AreEqual(4, Totals("1"), "a faulted flow is judged too");
    }

    [TestMethod]
    public void ConsoleRecord_AfterAFinalizedRun_RewritesItsFinalPassEvenWithNoRunNumber()
    {
        StartedRun();
        _report.MissionClosed();
        _report.MainMenuReturned();
        Assert.AreEqual(1, Totals("1"));

        _report.ConsoleCommand("fords_start", new[] { "nobody" }, true, "Unknown faction 'nobody'.");

        Assert.AreEqual(2, Totals("1"));
    }

    [TestMethod]
    public void FailureRouteA_StaysOpen_AndWritesNoFinalPass()
    {
        StartedRun();

        _report.Failure("A", "the match could not start");

        Assert.IsFalse(_report.Run!.Closed);
        Assert.AreEqual(0, Verdicts("1").Count);
    }

    [TestMethod]
    public void FailureWithNoRun_MarksTheOpenFlowFault()
    {
        _report.MenuClicked();
        _report.PromptShown("faction");

        _report.Failure("C", "boom");

        Assert.AreEqual("fault", _report.Session.Menu.Flows.Single().Ended);
    }

    [TestMethod]
    public void BeginRun_WithAnUnfinalizedRun_WritesItsSupersededPass()
    {
        _report.BeginRun("menu", Setup);

        _report.BeginRun("menu", Setup);

        Assert.AreEqual(16, Verdicts("1").Count);
        Assert.AreEqual("superseded", (string?)JObject.Parse(File.ReadAllText(ReportPath(1)))["reason"]);
    }

    [TestMethod]
    public void TowerBlowAndVolleyHit_FromParallelThreads_DuringAPass_NeverThrow()
    {
        StartedRun();
        var hit = new FordsVolleyHit(40f, 30, 5f, 40f, 50f, 50f, 1f, "Chest", false, false, true);
        var stop = false;
        Exception? failure = null;

        var writers = Enumerable.Range(0, 3).Select(i => Task.Run(() =>
        {
            try
            {
                while (!Volatile.Read(ref stop))
                {
                    _report.TowerBlow("east_outer_0", TowerBlowClass.Ranged, TowerDamageReason.Applied, 7, 7, i == 0 ? null : new FordsAttacker(MatchSide.West));
                    _report.VolleyHit(hit);
                }
            }
            catch (Exception ex)
            {
                failure = ex;
            }
        })).ToArray();

        for (var i = 0; i < 25; i++)
            _report.MissionClosed();
        _report.MainMenuReturned();
        Volatile.Write(ref stop, true);
        Task.WaitAll(writers);

        Assert.IsNull(failure);
        Assert.IsTrue(File.Exists(ReportPath(1)));
        Assert.IsFalse(_log.Lines.Any(l => l.Contains("faulted")));
    }

    [TestMethod]
    public void VolleyHits_StoreAtMost200_AndCountTheRest()
    {
        StartedRun();
        var hit = new FordsVolleyHit(40f, 30, 5f, 40f, 50f, 50f, 1f, "Chest", false, false, true);

        for (var i = 0; i < 250; i++)
            _report.VolleyHit(hit);

        Assert.AreEqual(200, _report.Run!.Volley.Hits.Count);
        Assert.AreEqual(250, _report.Run.Volley.HitsSeen);
        Assert.AreEqual(10, _log.Lines.Count(l => l.StartsWith("DEBUG") && l.Contains("[Volley]")));
        Assert.AreEqual(1, _log.Lines.Count(l => l.Contains("category=VolleyHit capped after 10")));
    }

    [TestMethod]
    public void VolleyHitFiltered_CountsEachReason()
    {
        StartedRun();

        _report.VolleyHitFiltered("otherItem");
        _report.VolleyHitFiltered("otherItem");
        _report.VolleyHitFiltered("notGunner");

        var filtered = _report.Run!.Volley.Filtered;
        Assert.AreEqual(2, filtered["otherItem"]);
        Assert.AreEqual(1, filtered["notGunner"]);
        Assert.AreEqual(0L, _report.Run.Volley.HitsSeen);
    }

    [TestMethod]
    public void TowerBlow_CountsByClassAndOutcome_AndSamplesTen()
    {
        StartedRun();

        for (var i = 0; i < 12; i++)
            _report.TowerBlow("east_outer_0", TowerBlowClass.Hero, TowerDamageReason.Locked, 9, 0, new FordsAttacker(MatchSide.West));
        _report.TowerBlow("east_outer_0", TowerBlowClass.Melee, TowerDamageReason.Applied, 9, 9, null);
        _report.TowerBlow("not_a_tower", TowerBlowClass.Melee, TowerDamageReason.Applied, 9, 9, new FordsAttacker(MatchSide.West));

        var tower = _report.Run!.Towers["east_outer_0"];
        Assert.AreEqual(12, tower.Blows[(int)TowerBlowClass.Hero, (int)TowerDamageReason.Locked]);
        Assert.AreEqual(1, tower.Blows[(int)TowerBlowClass.Melee, (int)TowerDamageReason.Applied]);
        Assert.AreEqual(1, tower.SideUnknownApplied);
        Assert.AreEqual(10, _log.Lines.Count(l => l.StartsWith("DEBUG") && l.Contains("[Tower]")));
    }

    [TestMethod]
    public void LaneCommand_LogsFirst30_ThenOneCappedLine()
    {
        StartedRun();

        for (var i = 0; i < 45; i++)
            _report.LaneCommand(MatchSide.West, 1, i % 2 == 0 ? LaneCommandKind.Charge : LaneCommandKind.HoldAt, true);

        Assert.AreEqual(30, _log.Lines.Count(l => l.Contains("[FordsOfWar][Lane]")));
        Assert.AreEqual(1, _log.Lines.Count(l => l.Contains("category=LaneCommand capped after 30")));
        Assert.AreEqual("Charge", _report.Run!.Lanes[1].LastCommand);
    }

    [TestMethod]
    public void MenuPolled_LogsFirstAndChangesOnly()
    {
        _report.MenuPolled(false);
        for (var i = 0; i < 20; i++)
            _report.MenuPolled(false);
        _report.MenuPolled(true);
        _report.MenuPolled(true);

        var menu = _report.Session.Menu;
        Assert.AreEqual(23, menu.Polls);
        Assert.AreEqual(1, menu.PollChanges);
        Assert.AreEqual(false, menu.FirstPollHidden);
        Assert.AreEqual(true, menu.LastPollHidden);
        Assert.AreEqual(2, _log.Lines.Count(l => l.Contains("[FordsOfWar][Menu]")));
    }

    [TestMethod]
    public void TowerHealth_DriftTwiceInARow_FlagsAndWarnsOnce()
    {
        StartedRun();

        _report.TowerHealth("east_outer_0", 100, 100f);
        _report.TowerHealth("east_outer_0", 100, 90f);
        Assert.IsFalse(_report.Run!.Towers["east_outer_0"].DriftFlagged);
        _report.TowerHealth("east_outer_0", 100, 90f);
        _report.TowerHealth("east_outer_0", 100, 90f);

        Assert.IsTrue(_report.Run.Towers["east_outer_0"].DriftFlagged);
        Assert.AreEqual(1, _log.Lines.Count(l => l.StartsWith("WARN") && l.Contains("drift tower=east_outer_0")));
        _report.TowerHealth("east_outer_0", 100, 100f);
        Assert.AreEqual(0, _report.Run.Towers["east_outer_0"].DriftRun);
    }

    [TestMethod]
    public void Field_MergesGainHoldMeleeAndContested()
    {
        StartedRun();
        FordsFieldSample Sample(float front, bool settled, int inFire, float holdFront, bool contested, int melee) => new(
            new[] { new FordsLaneSample(MatchSide.East, 1, 10, front, settled, inFire, holdFront) },
            new[] { new FordsTowerSample("east_outer_0", melee) },
            new[] { new FordsPointSample("point_0", contested, 20f) },
            40, 38, 12);

        _report.MissionTick(1f, 5.0);
        _report.Field(Sample(100f, false, 0, -1f, false, 0));
        _report.MissionTick(2f, 5.0);
        _report.Field(Sample(160f, true, 6, 55f, true, 3));
        _report.MissionTick(1f, 5.0);
        _report.Field(Sample(150f, true, 2, 65f, true, 4));

        var lane = _report.Run!.Lanes[3];
        Assert.AreEqual(100.0, lane.FirstFront);
        Assert.AreEqual(60.0, lane.MaxGain, 1e-4);
        Assert.AreEqual(2, lane.HoldSamples);
        Assert.AreEqual(1, lane.MaxInFireRun);
        Assert.AreEqual(55.0, lane.MinHoldFront, 1e-4);
        Assert.AreEqual(3.0 * 2 + 4.0 * 1, _report.Run.Towers["east_outer_0"].MeleeNearUnitSeconds, 1e-6);
        Assert.AreEqual(3.0, _report.Run.Points["point_0"].ContestedSeconds, 1e-6);
        Assert.AreEqual(20.0, _report.Run.Points["point_0"].NearestApproach, 1e-6);
        Assert.AreEqual(40, _report.Run.Perf.PeakLiveWest);
        Assert.AreEqual(12, _report.Run.Spawn.PeakBacklog);
    }

    [TestMethod]
    public void Field_ATowerNamedOnASettledInFireSample_IsKeptOnTheLane_AndAnUnsettledOneIsNot()
    {
        StartedRun();
        FordsFieldSample Sample(bool settled, int inFire, string? tower) => new(
            new[] { new FordsLaneSample(MatchSide.West, 0, 10, 100f, settled, inFire, 50f, tower) },
            new FordsTowerSample[0], new FordsPointSample[0], 10, 0, 0);

        _report.MissionTick(1f, 5.0);
        _report.Field(Sample(false, 8, "east_outer_1"));
        Assert.IsNull(_report.Run!.Lanes[0].FireTower);

        _report.Field(Sample(true, 8, "east_outer_1"));
        _report.Field(Sample(true, 1, "east_outer_0"));

        Assert.AreEqual("east_outer_1", _report.Run.Lanes[0].FireTower, "the settled sample that was in fire names it; a settled sample not in fire does not");
    }

    [TestMethod]
    public void Field_TheLaneNamesTheTowerOfItsLongestInFireRun_NotTheLatestOne()
    {
        StartedRun();
        FordsFieldSample Sample(bool inFire, string? tower) => new(
            new[] { new FordsLaneSample(MatchSide.West, 0, 10, 100f, true, inFire ? 8 : 0, 50f, tower) },
            new FordsTowerSample[0], new FordsPointSample[0], 10, 0, 0);

        _report.MissionTick(1f, 5.0);
        _report.Field(Sample(true, "east_outer_0"));
        _report.Field(Sample(true, "east_outer_0"));
        _report.Field(Sample(true, "east_outer_0"));
        _report.Field(Sample(false, null));
        _report.Field(Sample(true, "east_outer_1"));

        var lane = _report.Run!.Lanes[0];
        Assert.AreEqual(3, lane.MaxInFireRun);
        Assert.AreEqual("east_outer_0", lane.FireTower, "the run that failed check 8 was under east_outer_0");
    }

    [TestMethod]
    public void NoLogPath_WritesNoFile_AndSaysNone()
    {
        var quiet = new CaptureLogger(null);
        var report = new FordsRunReport(quiet);

        report.BeginRun("menu", Setup);
        report.MissionClosed();

        StringAssert.Contains(quiet.Lines.First(l => l.Contains("begin")), "report=\"none\"");
        StringAssert.Contains(quiet.Lines.First(l => l.Contains("begin")), "log=\"none\"");
        Assert.AreEqual(16, quiet.Lines.Count(l => l.Contains("[Verdict]") && l.Contains(" check=")));
        Assert.AreEqual(0, Directory.GetFiles(_dir, "fords_report_*").Length);
    }

    [TestMethod]
    public void Window_Every30MissionSeconds_WritesOneSummaryWithSixteenLetters()
    {
        StartedRun();
        _report.Stage("live");
        var windows = 0;

        for (var i = 0; i < 62; i++)
        {
            if (_report.MissionTick(1f, 8.0))
            {
                windows++;
                _report.Window(120, 90);
            }
        }

        Assert.AreEqual(2, windows);
        var summaries = _log.Lines.Where(l => l.Contains("[FordsOfWar][Summary]")).ToList();
        Assert.AreEqual(2, summaries.Count);
        StringAssert.Matches(summaries[0], new System.Text.RegularExpressions.Regex(@"checks=[PFRN]{16}$"));
        StringAssert.Contains(summaries[0], "phase=Live");
        StringAssert.Contains(summaries[0], "agentsAll=120");
        Assert.AreEqual(2, _report.Run!.Perf.Windows.Count);
        Assert.AreEqual(62.0, _report.Run.Perf.LiveSeconds, 1e-6);
    }

    [TestMethod]
    public void MissionTick_AdvancesTheRunsOwnClock_AndABeginRunStartsANewOne()
    {
        StartedRun();
        _report.MissionTick(1f, 5.0);
        _report.MissionTick(1f, 5.0);

        Assert.AreEqual(2.0, _report.Run!.Mt, 1e-9);
        Assert.AreEqual(2.0, _report.MissionSeconds, 1e-9);

        _report.BeginRun("menu", Setup);

        Assert.AreEqual(0.0, _report.Run!.Mt, 1e-9);
        Assert.AreEqual(0.0, _report.MissionSeconds, 1e-9);
    }

    [TestMethod]
    public void Spawned_StampsTheFirstSpawnOnTheLaneItself_AndNeverOutsideTheMatchsLanes()
    {
        StartedRun();
        _report.MissionTick(3f, 5.0);
        _report.Spawned(MatchSide.East, 1, "orc");
        _report.MissionTick(4f, 5.0);
        _report.Spawned(MatchSide.East, 1, "orc");
        _report.Spawned(MatchSide.West, 5, "orc");
        _report.Spawned(MatchSide.West, -1, "orc");

        var lanes = _report.Run!.Lanes;
        Assert.AreEqual(3.0, lanes[3].FirstSpawnMt);
        Assert.IsTrue(lanes.Where((_, i) => i != 3).All(l => l.FirstSpawnMt == null));
        Assert.AreEqual(4, _report.Run.Spawn.Spawned);
    }

    [TestMethod]
    public void TowerBlow_TraceCapIsPerTower_SoASecondTowerStillGetsItsFirstTen()
    {
        StartedRun();

        for (var i = 0; i < 14; i++)
            _report.TowerBlow("east_outer_0", TowerBlowClass.Melee, TowerDamageReason.Applied, 5, 5, new FordsAttacker(MatchSide.West));
        for (var i = 0; i < 14; i++)
            _report.TowerBlow("keep_east", TowerBlowClass.Melee, TowerDamageReason.Locked, 5, 0, new FordsAttacker(MatchSide.West));

        Assert.AreEqual(10, _log.Lines.Count(l => l.StartsWith("DEBUG") && l.Contains("blow tower=east_outer_0")));
        Assert.AreEqual(10, _log.Lines.Count(l => l.StartsWith("DEBUG") && l.Contains("blow tower=keep_east")));
        Assert.AreEqual(1, _log.Lines.Count(l => l.Contains("tower=east_outer_0") && l.Contains("capped after 10")));
        Assert.AreEqual(1, _log.Lines.Count(l => l.Contains("tower=keep_east") && l.Contains("capped after 10")));
    }

    [TestMethod]
    public void TowerBlow_ACountedBlowOfEveryReason_LandsInItsOwnCell()
    {
        StartedRun();

        foreach (TowerDamageReason reason in Enum.GetValues(typeof(TowerDamageReason)))
            _report.TowerBlow("east_outer_0", TowerBlowClass.Ranged, reason, 3, 0, new FordsAttacker(MatchSide.West));
        _report.TowerBlow("east_outer_0", TowerBlowClass.Ranged, TowerDamageReason.Weaponless, 0, 0, new FordsAttacker(MatchSide.West));

        var blows = _report.Run!.Towers["east_outer_0"].Blows;
        Assert.AreEqual(Enum.GetValues(typeof(TowerBlowClass)).Length, blows.GetLength(0));
        Assert.AreEqual(Enum.GetValues(typeof(TowerDamageReason)).Length, blows.GetLength(1));
        Assert.AreEqual(2L, blows[(int)TowerBlowClass.Ranged, (int)TowerDamageReason.Weaponless]);
        Assert.AreEqual(1L, blows[(int)TowerBlowClass.Ranged, (int)TowerDamageReason.Applied]);
        Assert.AreEqual(Enum.GetValues(typeof(TowerDamageReason)).Length + 1L, blows.Cast<long>().Sum());
    }

    [TestMethod]
    public void TheJson_WritesTheBlowTableAsRowsOfReasonsWithALegendFromTheEnums()
    {
        StartedRun();
        _report.TowerBlow("east_outer_0", TowerBlowClass.Hero, TowerDamageReason.Weaponless, 0, 0, new FordsAttacker(MatchSide.West));
        _report.Winner(MatchSide.West);

        var json = JObject.Parse(File.ReadAllText(ReportPath(1)));

        var table = (JArray)json["evidence"]!["Towers"]!["east_outer_0"]!["Blows"]!;
        Assert.AreEqual(Enum.GetValues(typeof(TowerBlowClass)).Length, table.Count);
        Assert.AreEqual(Enum.GetValues(typeof(TowerDamageReason)).Length, ((JArray)table[(int)TowerBlowClass.Hero]).Count);
        Assert.AreEqual(1, (int)table[(int)TowerBlowClass.Hero][(int)TowerDamageReason.Weaponless]!);
        var legend = (string?)json["blowsLegend"];
        foreach (var name in Enum.GetNames(typeof(TowerBlowClass)).Concat(Enum.GetNames(typeof(TowerDamageReason))))
            StringAssert.Contains(legend, name);
    }

    [TestMethod]
    public void Frames_PausedTicksAndTheFirstUntimedTickAreNotFrames()
    {
        StartedRun();
        _report.MissionTick(0f, 9000.0);
        _report.MissionTick(1f, -1.0);
        _report.MissionTick(1f, 5.0);
        _report.MissionTick(28f, 6.0);
        _report.Window(10, 9);

        Assert.AreEqual(2, _report.Run!.Perf.Windows.Single().Frames);
    }

    [TestMethod]
    public void BeginRun_StartsTheFrameHistogramOver()
    {
        StartedRun();
        _report.MissionTick(1f, 250.0);
        _report.BeginRun("menu", Setup);
        _report.MatchStarted(Facts, Towers(), 3);
        _report.MissionTick(30f, 5.0);
        _report.Window(10, 9);

        var window = _report.Run!.Perf.Windows.Single();
        Assert.AreEqual(1, window.Frames);
        Assert.AreEqual(5.0, window.MaxMs, 1e-9);
    }

    [TestMethod]
    public void Lines_UseInvariantCultureUnderGerman_AndContainNoEmOrEnDash()
    {
        var before = Thread.CurrentThread.CurrentCulture;
        try
        {
            Thread.CurrentThread.CurrentCulture = new CultureInfo("de-DE");
            StartedRun();
            _report.Stage("live");
            _report.MissionTick(30f, 8.5);
            _report.Window(10, 9);
            _report.Winner(MatchSide.East);
            _report.MissionClosed();
            _report.MainMenuReturned();
        }
        finally
        {
            Thread.CurrentThread.CurrentCulture = before;
        }

        var lines = _log.Lines;
        Assert.IsTrue(lines.Count > 40);
        Assert.IsFalse(lines.Any(l => l.Contains('—') || l.Contains('–')));
        Assert.IsTrue(lines.Any(l => l.Contains("liveS=30.0")), "decimal point, not a comma");
        StringAssert.Matches(string.Join("\n", lines), new System.Text.RegularExpressions.Regex(@"w=\d+\.\d{3} mt=\d+\.\d"));
        Assert.IsFalse(File.ReadAllText(ReportPath(1)).Contains("—"));
    }

    [TestMethod]
    public void ConsoleStart_BindsItsRecordToTheRunThatBeganInsideTheCommand()
    {
        _report.BeginRun("console", Setup);
        _report.Stage("matchStarted");

        _report.ConsoleCommand("fords_start", new[] { "gondor" }, true, "Starting");

        var record = _report.Session.Console.Single();
        Assert.AreEqual(1, record.Run);
        Assert.IsTrue(record.MatchStarted);
        StringAssert.Contains(record.Setup, "scene=scene_a");
        _report.Failure("A", "x");
        Assert.AreEqual("x", record.Failure);
    }

    [TestMethod]
    public void ConsoleStart_FailingOnRouteC_FinalBlockIncludesTheConsoleRecord()
    {
        _report.BeginRun("console", Setup);
        _report.Failure("C", "the game manager factory returned null");

        _report.ConsoleCommand("fords_start", new[] { "gondor" }, true, "Starting");

        var rows = Verdicts("1").Where(l => l.Contains(" check=02 ")).ToList();
        Assert.IsTrue(rows.Count > 0, "check 2 row present in the final=1 block");
        StringAssert.Contains(rows.Last(), "result=FAIL");
        Assert.AreEqual(2, Totals("1"), "the console record triggers a second final pass");
    }

    [TestMethod]
    public void MenuFlow_ClickShownAnsweredAbandoned()
    {
        _report.MenuClicked();
        _report.PromptShown("faction");
        _report.PromptAnswered("faction");
        _report.MenuClicked();

        var flows = _report.Session.Menu.Flows;
        Assert.AreEqual(2, flows.Count);
        Assert.AreEqual("abandoned", flows[0].Ended);
        CollectionAssert.AreEqual(new[] { "faction" }, flows[0].Answered);
        Assert.AreEqual("open", flows[1].Ended);
        Assert.AreEqual(2, _report.Session.Menu.Clicks);
    }

    [TestMethod]
    public void ColdMember_ThatThrows_WarnsOncePerName()
    {
        var report = new FordsRunReport(_log);
        report.BeginRun("menu", Setup);

        report.Preflight(1, null!);
        report.Preflight(1, null!);

        Assert.AreEqual(1, _log.Lines.Count(l => l.StartsWith("WARN") && l.Contains("Preflight faulted")));
    }

    [TestMethod]
    public void WallClock_IsInjectable_AndReadsZeroUntilTheFirstRunBegins()
    {
        var now = 500.0;
        var report = new FordsRunReport(_log, () => now);

        now = 530.0;
        report.MenuClicked();
        Assert.IsTrue(_log.Lines.Any(l => l.Contains(" w=0.000 ")), "no run yet: the wall column reads zero");

        report.BeginRun("menu", Setup);
        now = 560.0;
        report.Winner(MatchSide.West);

        Assert.AreEqual(30.0, report.Run!.End.WinnerWall!.Value, 1e-9, "wall seconds count from BeginRun");
    }

    [TestMethod]
    public void TheSystemClockConstructor_StaysThePublicOne_ForTheContainer()
    {
        var constructors = typeof(FordsRunReport).GetConstructors();

        Assert.AreEqual(1, constructors.Length);
        Assert.AreEqual(1, constructors[0].GetParameters().Length);
    }

    [DataTestMethod]
    [DataRow(1, 1, false, false, false, false, false, 0.24f, 0, "T/Pierce/tip0/pas0/alt0/arm0/mnt0/b<1")]
    [DataRow(1, 1, false, true, false, false, false, 0f, 0, "T/Pierce/tip0/pas1/alt0/arm0/mnt0/b0")]
    [DataRow(0, 0, false, false, false, false, false, 9.6f, 12, "S/Cut/tip0/pas0/alt0/arm0/mnt0/hit")]
    [DataRow(0, 2, false, false, true, true, false, 1f, 0, "S/Blunt/tip0/pas0/alt1/arm1/mnt0/b>=1")]
    [DataRow(1, 1, true, false, false, false, false, 16.6f, 16, "T/Pierce/tip1/pas0/alt0/arm0/mnt0/hit")]
    [DataRow(0, -1, false, false, false, false, false, 0f, 0, "S/Invalid/tip0/pas0/alt0/arm0/mnt0/b0")]
    [DataRow(0, 7, false, false, false, false, false, 2.5f, 0, "S/Invalid/tip0/pas0/alt0/arm0/mnt0/b>=1")]
    [DataRow(1, 1, false, false, false, false, true, 0.24f, 0, "T/Pierce/tip0/pas0/alt0/arm0/mnt1/b<1")]
    [DataRow(1, 1, false, false, false, false, false, float.NaN, 0, "T/Pierce/tip0/pas0/alt0/arm0/mnt0/bNaN")]
    [DataRow(1, 1, false, false, false, false, false, -1f, 0, "T/Pierce/tip0/pas0/alt0/arm0/mnt0/bNaN")]
    public void MeleeProbeKey_NamesTheStrikeTheFlagsAndTheBaseMagnitudeBucket(int strike, int damageType, bool tip, bool passive,
        bool alternative, bool armBone, bool mounted, float baseMagnitude, int raw, string expected) =>
        Assert.AreEqual(expected, FordsRunReport.MeleeProbeKey(strike, damageType, tip, passive, alternative, armBone, mounted, baseMagnitude, raw));

    private const string ZeroKey = "T/Pierce/tip0/pas0/alt0/arm0/mnt0/b<1";
    private const string HitKey = "S/Cut/tip0/pas0/alt0/arm0/mnt0/hit";

    private void BindProbe(object enemy, object friend)
    {
        _report.BindTowerLookup(c => c is string id ? id : null);
        _report.BindAttackerLookup(a => ReferenceEquals(a, enemy) ? new FordsAttacker(MatchSide.West, 0)
            : ReferenceEquals(a, friend) ? new FordsAttacker(MatchSide.East, 0) : null);
    }

    /// <summary>What the probe hook does: count the blow, and trace a line only when the report says it is within the cap.</summary>
    private bool Probe(string tower, object attacker, string key, string line)
    {
        var traced = _report.TowerMeleeProbe(tower, attacker, key);
        if (traced)
            _report.TraceTowerMelee(line);
        return traced;
    }

    [TestMethod]
    public void BindTowerLookup_IsAskedThroughProbeTowerId_AndIsGoneOnceCleared()
    {
        StartedRun();
        Assert.IsNull(_report.ProbeTowerId("east_outer_0"), "nothing is bound yet");

        _report.BindTowerLookup(c => c as string);
        Assert.AreEqual("east_outer_0", _report.ProbeTowerId("east_outer_0"));
        Assert.IsNull(_report.ProbeTowerId(null));

        _report.BindTowerLookup(null);
        Assert.IsNull(_report.ProbeTowerId("east_outer_0"));
    }

    [TestMethod]
    public void TowerMeleeProbe_CountsTheKeyOnTheTower_SkipsTheOwnersSide_AndATowerTheRunDoesNotKnow()
    {
        StartedRun();
        var enemy = new object();
        var friend = new object();
        BindProbe(enemy, friend);

        Probe("east_outer_0", enemy, ZeroKey, "tower=east_outer_0 key=zero");
        Probe("east_outer_0", enemy, ZeroKey, "tower=east_outer_0 key=zero");
        Probe("east_outer_0", enemy, HitKey, "tower=east_outer_0 key=hit");
        Probe("east_outer_0", new object(), ZeroKey, "tower=east_outer_0 key=unknownside");
        Probe("east_outer_0", friend, ZeroKey, "tower=east_outer_0 key=friend");
        Probe("not_a_tower", enemy, ZeroKey, "tower=not_a_tower key=zero");

        var probe = _report.Run!.Towers["east_outer_0"].MeleeProbe;
        Assert.AreEqual(3, probe[ZeroKey], "two from the enemy and one from an attacker whose side is unknown; the owner's own side is left out");
        Assert.AreEqual(1, probe[HitKey]);
        Assert.AreEqual(2, probe.Count);
        Assert.AreEqual(0, _log.Lines.Count(l => l.Contains("key=friend") || l.Contains("not_a_tower")));
    }

    [TestMethod]
    public void TowerMeleeProbe_TracesTheFirstFortyZeroBlowsAndTheFirstTenDamagingOnes_PerTower_ButCountsEveryOne()
    {
        StartedRun();
        var enemy = new object();
        BindProbe(enemy, new object());

        for (var i = 0; i < 50; i++)
            Probe("east_outer_0", enemy, ZeroKey, "tower=east_outer_0 key=" + ZeroKey + " n=" + i);
        for (var i = 0; i < 15; i++)
            Probe("east_outer_0", enemy, HitKey, "tower=east_outer_0 key=" + HitKey + " n=" + i);
        for (var i = 0; i < 50; i++)
            Probe("keep_east", enemy, ZeroKey, "tower=keep_east key=" + ZeroKey + " n=" + i);
        for (var i = 0; i < 15; i++)
            Probe("keep_east", enemy, HitKey, "tower=keep_east key=" + HitKey + " n=" + i);

        foreach (var tower in new[] { "east_outer_0", "keep_east" })
        {
            Assert.AreEqual(40, _log.Lines.Count(l => l.StartsWith("DEBUG") && l.Contains("[FordsOfWar][TowerMelee]") && l.Contains("tower=" + tower + " key=" + ZeroKey)), tower);
            Assert.AreEqual(10, _log.Lines.Count(l => l.StartsWith("DEBUG") && l.Contains("[FordsOfWar][TowerMelee]") && l.Contains("tower=" + tower + " key=" + HitKey)), tower);
            Assert.AreEqual(50, _report.Run!.Towers[tower].MeleeProbe[ZeroKey], tower);
            Assert.AreEqual(15, _report.Run.Towers[tower].MeleeProbe[HitKey], tower);
            Assert.AreEqual(1, _log.Lines.Count(l => l.Contains("tower=" + tower) && l.Contains("category=TowerProbeZero") && l.Contains("capped after 40")), tower);
            Assert.AreEqual(1, _log.Lines.Count(l => l.Contains("tower=" + tower) && l.Contains("category=TowerProbeHit") && l.Contains("capped after 10")), tower);
        }

        _report.BeginRun("menu", Setup);
        _report.MatchStarted(Facts, Towers(), 3);
        Probe("east_outer_0", enemy, ZeroKey, "tower=east_outer_0 key=again");
        Assert.AreEqual(1, _log.Lines.Count(l => l.Contains("key=again")), "a new run traces again");
    }

    [TestMethod]
    public void TowerMeleeProbe_ReportsNoTraceOncePastTheCap_SoTheCallerBuildsNoLine()
    {
        StartedRun();
        var enemy = new object();
        BindProbe(enemy, new object());

        var traced = Enumerable.Range(0, 12).Select(_ => _report.TowerMeleeProbe("east_outer_0", enemy, HitKey)).ToList();

        Assert.AreEqual(10, traced.Count(t => t));
        Assert.IsTrue(traced.Take(10).All(t => t), "the first ten are traced");
        Assert.IsFalse(traced[10] || traced[11]);
        Assert.AreEqual(12, _report.Run!.Towers["east_outer_0"].MeleeProbe[HitKey], "past the cap a blow is still counted");
    }

    [TestMethod]
    public void TowerMeleeProbe_ATowerThatHasFallen_IsNotCountedOrTraced()
    {
        StartedRun();
        var enemy = new object();
        BindProbe(enemy, new object());
        Assert.IsTrue(_report.TowerMeleeProbe("east_outer_0", enemy, ZeroKey));

        _report.TowerFell("east_outer_0", false);

        Assert.IsFalse(_report.TowerMeleeProbe("east_outer_0", enemy, ZeroKey), "the rubble is not a tower any more");
        Assert.AreEqual(1, _report.Run!.Towers["east_outer_0"].MeleeProbe[ZeroKey]);
        Assert.IsTrue(_report.TowerMeleeProbe("keep_east", enemy, ZeroKey), "another tower is still counted");
    }

    [TestMethod]
    public void TowerMeleeProbe_BeforeAnyRun_DoesNothing()
    {
        Assert.IsFalse(_report.TowerMeleeProbe("east_outer_0", new object(), ZeroKey));

        Assert.IsNull(_report.Run);
        Assert.AreEqual(0, _log.Lines.Count);
    }

    [TestMethod]
    public void TheJson_WritesTheMeleeProbeCountsOfEachTower()
    {
        StartedRun();
        var enemy = new object();
        BindProbe(enemy, new object());
        Probe("east_outer_0", enemy, ZeroKey, "tower=east_outer_0");
        Probe("east_outer_0", enemy, ZeroKey, "tower=east_outer_0");
        _report.Winner(MatchSide.West);

        var json = JObject.Parse(File.ReadAllText(ReportPath(1)));

        Assert.AreEqual(2, (int)json["evidence"]!["Towers"]!["east_outer_0"]!["MeleeProbe"]![ZeroKey]!);
    }

    [TestMethod]
    public void TowerBlow_AnAppliedBlowWithNoEngineDamage_IsCountedAsFloored_AndKeepsItsZeroInTheRawSummary()
    {
        StartedRun();

        _report.TowerBlow("east_outer_0", TowerBlowClass.Melee, TowerDamageReason.Applied, 0, 10, new FordsAttacker(MatchSide.West));
        _report.TowerBlow("east_outer_0", TowerBlowClass.Melee, TowerDamageReason.Applied, 0, 40, new FordsAttacker(MatchSide.West));
        _report.TowerBlow("east_outer_0", TowerBlowClass.Melee, TowerDamageReason.Applied, 9, 9, new FordsAttacker(MatchSide.West));
        _report.TowerBlow("east_outer_0", TowerBlowClass.Melee, TowerDamageReason.Locked, 0, 0, new FordsAttacker(MatchSide.West));

        var tower = _report.Run!.Towers["east_outer_0"];
        Assert.AreEqual(2L, tower.FlooredBlows);
        Assert.AreEqual(50L, tower.FlooredDamage);
        Assert.AreEqual(3, tower.MeleeRaw[0], "the engine's own raw damage is what the summary reads");
        Assert.AreEqual(59L, tower.AppliedDamage[(int)TowerBlowClass.Melee]);
    }
}
