using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Core.Logging;
using TAOM.Features.FordsOfWar;
using TAOM.Features.FordsOfWar.Diagnostics;
using TAOM.Features.FordsOfWar.Domain;

namespace TAOM.Tests.Features.FordsOfWar.Diagnostics;

/// <summary>The combine rule and the sixteen check rules, judged from hand-built evidence.</summary>
[TestClass]
public class FordsVerdictTests
{
    private static FordsPart P(FordsResult r) => new("p", r, "");

    private static FordsMatchFacts Facts() => new(60f, 8f, 12f, 2, 150, 200);

    private static FordsRunEvidence Run()
    {
        var run = new FordsRunEvidence { Run = 1, Facts = Facts() };
        for (var side = 0; side < 2; side++)
            for (var lane = 0; lane < 2; lane++)
                run.Lanes.Add(new FordsLaneEvidence { Side = side == 0 ? "West" : "East", Lane = lane });
        return run;
    }

    private static FordsVerdictRow Row(FordsSessionEvidence s, FordsRunEvidence r, int check) => FordsVerdicts.Judge(s, r)[check - 1];

    private static FordsResult Result(FordsRunEvidence r, int check) => Row(new FordsSessionEvidence(), r, check).Result;

    private static FordsResult PartOf(FordsVerdictRow row, string name) => row.Parts.Single(p => p.Name == name).Result;

    private static void Mark(FordsRunEvidence run, string name, double mt, double wall = 0) =>
        run.Stages.Add(new FordsStageMark { Name = name, Mt = mt, Wall = wall });

    [TestMethod]
    public void Combine_FailWins()
    {
        Assert.AreEqual(FordsResult.Fail, FordsResults.Combine(new[] { P(FordsResult.Pass), P(FordsResult.Fail), P(FordsResult.NotSeen) }));
    }

    [TestMethod]
    public void Combine_AllPass_IsPass()
    {
        Assert.AreEqual(FordsResult.Pass, FordsResults.Combine(new[] { P(FordsResult.Pass), P(FordsResult.Pass) }));
    }

    [TestMethod]
    public void Combine_AllNotSeen_IsNotSeen()
    {
        Assert.AreEqual(FordsResult.NotSeen, FordsResults.Combine(new[] { P(FordsResult.NotSeen), P(FordsResult.NotSeen) }));
        Assert.AreEqual(FordsResult.NotSeen, FordsResults.Combine(new List<FordsPart>()));
    }

    [TestMethod]
    public void Combine_PassAndNotSeen_IsPartial()
    {
        Assert.AreEqual(FordsResult.Partial, FordsResults.Combine(new[] { P(FordsResult.Pass), P(FordsResult.NotSeen) }));
    }

    [TestMethod]
    public void WordAndLetter_NameEveryResult()
    {
        Assert.AreEqual("NOT_SEEN", FordsResults.Word(FordsResult.NotSeen));
        Assert.AreEqual("PARTIAL", FordsResults.Word(FordsResult.Partial));
        Assert.AreEqual("PFRN", new string(new[] { FordsResult.Pass, FordsResult.Fail, FordsResult.Partial, FordsResult.NotSeen }.Select(FordsResults.Letter).ToArray()));
    }

    [TestMethod]
    public void Judge_ReturnsChecks1To16_EachWithRuleAndHumanText()
    {
        var rows = FordsVerdicts.Judge(new FordsSessionEvidence(), new FordsRunEvidence());

        CollectionAssert.AreEqual(Enumerable.Range(1, 16).ToArray(), rows.Select(r => r.Check).ToArray());
        foreach (var row in rows)
        {
            Assert.IsFalse(string.IsNullOrWhiteSpace(row.Title), "title " + row.Check);
            Assert.IsFalse(string.IsNullOrWhiteSpace(row.Rule), "rule " + row.Check);
            Assert.IsFalse(string.IsNullOrWhiteSpace(row.HumanOnly), "human " + row.Check);
            Assert.IsTrue(row.Parts.Count > 0, "parts " + row.Check);
        }
    }

    [TestMethod]
    public void EmptyEvidence_NoCheckPasses()
    {
        var rows = FordsVerdicts.Judge(new FordsSessionEvidence(), new FordsRunEvidence());

        Assert.IsFalse(rows.Any(r => r.Result == FordsResult.Pass));
    }

    [TestMethod]
    public void Judge_NoTextCarriesAnEmOrEnDash()
    {
        var rows = FordsVerdicts.Judge(new FordsSessionEvidence(), new FordsRunEvidence());

        foreach (var row in rows)
            Assert.IsFalse((row.Title + row.Rule + row.HumanOnly + row.Info).Any(c => c == '—' || c == '–'), "check " + row.Check);
    }

    // Check 1

    private static FordsSessionEvidence Menu(bool hidden = false)
    {
        var s = new FordsSessionEvidence();
        s.Menu.Installed = true;
        s.Menu.HasContent = true;
        s.Menu.Factions = 3;
        s.Menu.Maps = 1;
        s.Menu.Polls = 4;
        s.Menu.LastPollHidden = hidden;
        s.Menu.FirstPollHidden = hidden;
        return s;
    }

    private static FordsMenuFlow Flow(string ended, int run, params string[] shown)
    {
        var f = new FordsMenuFlow { Ended = ended, Run = run };
        f.Shown.AddRange(shown);
        return f;
    }

    [TestMethod]
    public void Check01_Pass_WhenInstalledPolledAFullFlowAndACancelWithNoRun()
    {
        var s = Menu();
        s.Menu.Flows.Add(Flow("start", 1, "faction", "enemy", "difficulty"));
        s.Menu.Flows.Add(Flow("cancel", 0, "faction"));

        Assert.AreEqual(FordsResult.Pass, Row(s, new FordsRunEvidence(), 1).Result);
    }

    [TestMethod]
    public void Check01_MapPrompt_IsRequiredOnlyWhenThereIsMoreThanOneMap()
    {
        var s = Menu();
        s.Menu.Maps = 2;
        s.Menu.Flows.Add(Flow("start", 1, "faction", "enemy", "difficulty"));

        Assert.AreEqual(FordsResult.NotSeen, PartOf(Row(s, new FordsRunEvidence(), 1), "flow"));

        s.Menu.Flows[0].Shown.Add("map");
        Assert.AreEqual(FordsResult.Pass, PartOf(Row(s, new FordsRunEvidence(), 1), "flow"));
    }

    [TestMethod]
    public void Check01_Fail_WhenTheEntryIsHiddenOrNotInstalledOrAFlowFaulted()
    {
        Assert.AreEqual(FordsResult.Fail, PartOf(Row(Menu(hidden: true), new FordsRunEvidence(), 1), "polled"));
        Assert.AreEqual(FordsResult.Fail, PartOf(Row(new FordsSessionEvidence(), new FordsRunEvidence(), 1), "installed"));
        var s = Menu();
        s.Menu.Flows.Add(Flow("fault", 0, "faction"));
        Assert.AreEqual(FordsResult.Fail, PartOf(Row(s, new FordsRunEvidence(), 1), "flow"));
    }

    [TestMethod]
    public void Check01_Flow_AnOlderFaultStillFails_EvenWhenANewerFlowIsGood()
    {
        var s = Menu();
        s.Menu.Flows.Add(Flow("fault", 0, "faction"));
        s.Menu.Flows.Add(Flow("start", 1, "faction", "enemy", "difficulty"));

        var part = Row(s, new FordsRunEvidence(), 1).Parts.Single(p => p.Name == "flow");

        Assert.AreEqual(FordsResult.Fail, part.Result);
        StringAssert.Contains(part.Numbers, "faults=1");
        StringAssert.Contains(part.Numbers, "good=1");
    }

    [TestMethod]
    public void Check01_Cancel_PassesOnACancelOrAnAbandonedFlow_AndIsNotSeenOtherwise()
    {
        var s = Menu();
        Assert.AreEqual(FordsResult.NotSeen, PartOf(Row(s, new FordsRunEvidence(), 1), "cancel"));

        s.Menu.Flows.Add(Flow("abandoned", 0, "faction"));
        Assert.AreEqual(FordsResult.Pass, PartOf(Row(s, new FordsRunEvidence(), 1), "cancel"));

        s.Menu.Flows.Add(Flow("cancel", 0, "faction"));
        Assert.AreEqual(FordsResult.Pass, PartOf(Row(s, new FordsRunEvidence(), 1), "cancel"));
    }

    [TestMethod]
    public void Check01_NoPolls_IsNotSeen()
    {
        var s = Menu();
        s.Menu.Polls = 0;
        s.Menu.LastPollHidden = null;

        Assert.AreEqual(FordsResult.NotSeen, PartOf(Row(s, new FordsRunEvidence(), 1), "polled"));
    }

    // Check 2

    private static FordsConsoleRecord Cmd(string command, string[] args, bool ran, string reply, int run = 0, bool started = false, string failure = "")
    {
        var r = new FordsConsoleRecord { Command = command, Ran = ran, Reply = reply, Run = run, MatchStarted = started, Failure = failure };
        r.Args.AddRange(args);
        return r;
    }

    [TestMethod]
    public void Check02_Pass_WhenNamedUnknownAndDefaultStartsAllBehave()
    {
        var s = new FordsSessionEvidence();
        s.Console.Add(Cmd("fords_start", new[] { "gondor" }, true, "Starting", 1, true));
        s.Console.Add(Cmd("fords_start", new[] { "nobody" }, true, "Unknown faction 'nobody'. Valid: x.", 0));
        s.Console.Add(Cmd("fords_start", new string[0], true, "Starting", 2, true));

        Assert.AreEqual(FordsResult.Pass, Row(s, new FordsRunEvidence(), 2).Result);
    }

    [TestMethod]
    public void Check02_Fail_WhenTheNamedStartRecordedAFailure()
    {
        var s = new FordsSessionEvidence();
        s.Console.Add(Cmd("fords_start", new[] { "gondor" }, true, "Starting", 1, false, "boom"));

        Assert.AreEqual(FordsResult.Fail, PartOf(Row(s, new FordsRunEvidence(), 2), "named"));
    }

    [TestMethod]
    public void Check02_RefusedByTheGate_IsCountedAndNeverPasses()
    {
        var s = new FordsSessionEvidence();
        s.Console.Add(Cmd("fords_start", new[] { "gondor" }, false, "console is off"));

        var row = Row(s, new FordsRunEvidence(), 2);

        Assert.AreEqual(FordsResult.NotSeen, row.Result);
        StringAssert.Contains(row.Info, "refused=1");
    }

    [TestMethod]
    public void Check02_UnknownPrefix_MatchesTheResolverError()
    {
        var catalog = FordsConfigCatalog.FromTexts(null, FordsConfigFixtures.Factions, FordsConfigFixtures.Maps, Substitute.For<IModLogger>());

        var ok = FordsSetupChoices.TryResolve(catalog, "no_such_faction", null, null, null, out _, out var error);

        Assert.IsFalse(ok);
        StringAssert.StartsWith(error, FordsVerdicts.UnknownPrefix);
    }

    // Check 3

    [TestMethod]
    public void Check03_Pass_Fail_NotSeen()
    {
        var run = Run();
        Assert.AreEqual(FordsResult.NotSeen, Result(run, 3));

        Mark(run, "matchStarted", 0);
        run.Spawn.Spawned = 5;
        Assert.AreEqual(FordsResult.Pass, Result(run, 3));

        run.Start.MissingIds.Add("hero_x");
        Assert.AreEqual(FordsResult.Fail, Result(run, 3));

        run.Start.MissingIds.Clear();
        run.Spawn.Failed = 1;
        Assert.AreEqual(FordsResult.Fail, Result(run, 3));
    }

    [TestMethod]
    public void Check03_Started_FailsOnAFailureWithoutMatchStarted_AnyRoute()
    {
        var run = Run();
        Assert.AreEqual(FordsResult.NotSeen, PartOf(Row(new FordsSessionEvidence(), run, 3), "started"), "no failure and no start");

        foreach (var route in new[] { "A", "B", "C" })
        {
            run.Failure = new FordsFailure { Route = route, Reason = "why " + route };
            var part = Row(new FordsSessionEvidence(), run, 3).Parts.Single(p => p.Name == "started");
            Assert.AreEqual(FordsResult.Fail, part.Result, route);
            StringAssert.Contains(part.Numbers, "route=" + route);
            StringAssert.Contains(part.Numbers, "why " + route);
        }

        Mark(run, "matchStarted", 0);
        Assert.AreEqual(FordsResult.Pass, PartOf(Row(new FordsSessionEvidence(), run, 3), "started"), "a start followed by a later failure is not a failed start");
    }

    // Check 4

    [TestMethod]
    public void Check04_Countdown_PassesOnlyOnceLive()
    {
        var run = Run();
        Mark(run, "matchStarted", 0, 1);
        Assert.AreEqual(FordsResult.NotSeen, PartOf(Row(new FordsSessionEvidence(), run, 4), "countdown"));

        Mark(run, "live", 10, 11.5);
        Assert.AreEqual(FordsResult.Pass, PartOf(Row(new FordsSessionEvidence(), run, 4), "countdown"));
    }

    [DataTestMethod]
    [DataRow(14, 14, 2, "Pass")]
    [DataRow(13, 14, 2, "Fail")]
    [DataRow(14, 14, 1, "Fail")]
    public void Check04_Towers(int built, int expected, int keeps, string wantName)
    {
        var want = (FordsResult)Enum.Parse(typeof(FordsResult), wantName);
        var run = Run();
        Mark(run, "matchStarted", 0);
        run.Start.TowersBuilt = built;
        run.Start.TowersExpected = expected;
        run.Start.KeepsBuilt = keeps;

        Assert.AreEqual(want, PartOf(Row(new FordsSessionEvidence(), run, 4), "towers"));
    }

    [TestMethod]
    public void Check04_Waves_PassFailNotSeen()
    {
        var run = Run();
        Mark(run, "live", 10);
        for (var i = 0; i < 4; i++)
            run.Lanes[i].FirstSpawnMt = 12;
        run.Perf.LiveSeconds = 30;
        Assert.AreEqual(FordsResult.Pass, PartOf(Row(new FordsSessionEvidence(), run, 4), "waves"));

        run.Lanes[3].FirstSpawnMt = null;
        Assert.AreEqual(FordsResult.NotSeen, PartOf(Row(new FordsSessionEvidence(), run, 4), "waves"), "under 60 live seconds");

        run.Perf.LiveSeconds = 60;
        Assert.AreEqual(FordsResult.Fail, PartOf(Row(new FordsSessionEvidence(), run, 4), "waves"));
    }

    [TestMethod]
    public void Check04_Waves_AFirstSpawnMoreThanAMinuteAfterLive_IsLate_AndFailsOnceAMinuteIsLive()
    {
        var run = Run();
        Mark(run, "live", 10);
        for (var i = 0; i < 4; i++)
            run.Lanes[i].FirstSpawnMt = 12;
        run.Lanes[3].FirstSpawnMt = 70;
        run.Perf.LiveSeconds = 120;
        Assert.AreEqual(FordsResult.Pass, PartOf(Row(new FordsSessionEvidence(), run, 4), "waves"), "exactly 60 s after live is on time");

        run.Lanes[3].FirstSpawnMt = 70.5;
        var row = Row(new FordsSessionEvidence(), run, 4);
        Assert.AreEqual(FordsResult.Fail, PartOf(row, "waves"));
        StringAssert.Contains(row.Parts.Single(p => p.Name == "waves").Numbers, "late=1");

        run.Perf.LiveSeconds = 30;
        Assert.AreEqual(FordsResult.NotSeen, PartOf(Row(new FordsSessionEvidence(), run, 4), "waves"), "under a minute of live play it may still arrive");
    }

    [TestMethod]
    public void Check04_Towers_WithoutMatchStarted_IsNotSeenEvenWhenTheCountsAgree()
    {
        var run = Run();
        run.Start.TowersBuilt = 14;
        run.Start.TowersExpected = 14;
        run.Start.KeepsBuilt = 2;

        Assert.AreEqual(FordsResult.NotSeen, PartOf(Row(new FordsSessionEvidence(), run, 4), "towers"));

        Mark(run, "matchStarted", 0);
        Assert.AreEqual(FordsResult.Pass, PartOf(Row(new FordsSessionEvidence(), run, 4), "towers"));
    }

    [TestMethod]
    public void Check04_March_PassFailNotSeen()
    {
        var run = Run();
        Assert.AreEqual(FordsResult.NotSeen, PartOf(Row(new FordsSessionEvidence(), run, 4), "march"));

        foreach (var lane in run.Lanes)
        {
            lane.LiveSeconds = 90;
            lane.MaxGain = 80;
        }

        Assert.AreEqual(FordsResult.Pass, PartOf(Row(new FordsSessionEvidence(), run, 4), "march"));

        run.Lanes[1].MaxGain = 4;
        run.Lanes[1].LastCommand = "HoldAt";
        var row = Row(new FordsSessionEvidence(), run, 4);
        Assert.AreEqual(FordsResult.Fail, PartOf(row, "march"));
        StringAssert.Contains(row.Parts.Single(p => p.Name == "march").Numbers, "HoldAt");
    }

    [TestMethod]
    public void Check04_FightAndKillGold()
    {
        var run = Run();
        Assert.AreEqual(FordsResult.NotSeen, PartOf(Row(new FordsSessionEvidence(), run, 4), "fight"));

        run.Kills.DeathsWest = 3;
        run.Kills.DeathsEast = 4;
        run.Kills.Credit["West:Paid"] = 4;
        var row = Row(new FordsSessionEvidence(), run, 4);
        Assert.AreEqual(FordsResult.Pass, PartOf(row, "fight"));
        Assert.AreEqual(FordsResult.Pass, PartOf(row, "killGold"));

        run.Kills.Credit.Clear();
        run.Kills.DeathsEast = 19;
        Assert.AreEqual(FordsResult.NotSeen, PartOf(Row(new FordsSessionEvidence(), run, 4), "killGold"));
        run.Kills.DeathsEast = 20;
        run.Kills.Credit["West:KillerUnknown"] = 20;
        var failing = Row(new FordsSessionEvidence(), run, 4);
        Assert.AreEqual(FordsResult.Fail, PartOf(failing, "killGold"));
        StringAssert.Contains(failing.Parts.Single(p => p.Name == "killGold").Numbers, "West:KillerUnknown=20");
    }

    [DataTestMethod]
    [DataRow(0, 0, "NotSeen")]
    [DataRow(10, 5, "Pass")]
    [DataRow(29, 0, "NotSeen")]
    [DataRow(30, 0, "Fail")]
    public void Check04_CameraInputs(int input, int changed, string wantName)
    {
        var want = (FordsResult)Enum.Parse(typeof(FordsResult), wantName);
        var run = Run();
        run.Client.PanInputFrames = input;
        run.Client.PanMovedFrames = changed;
        run.Client.RotateInputFrames = input;
        run.Client.TurnedFrames = changed;
        run.Client.ZoomInputFrames = input;
        run.Client.ZoomedFrames = changed;
        var row = Row(new FordsSessionEvidence(), run, 4);

        Assert.AreEqual(want, PartOf(row, "pan"));
        Assert.AreEqual(want, PartOf(row, "rotate"));
        Assert.AreEqual(want, PartOf(row, "zoom"));
    }

    [TestMethod]
    public void Check04_InsideAndOwnership()
    {
        var run = Run();
        run.Client.InsideSamples = 12;
        run.Client.OwnedFrames = 100;
        var row = Row(new FordsSessionEvidence(), run, 4);
        Assert.AreEqual(FordsResult.Pass, PartOf(row, "inside"));
        Assert.AreEqual(FordsResult.Pass, PartOf(row, "ownership"));

        run.Client.MaxOutsideRun = 2;
        run.Client.CameraFailed = true;
        row = Row(new FordsSessionEvidence(), run, 4);
        Assert.AreEqual(FordsResult.Fail, PartOf(row, "inside"));
        Assert.AreEqual(FordsResult.Fail, PartOf(row, "ownership"));

        Assert.AreEqual(FordsResult.NotSeen, PartOf(Row(new FordsSessionEvidence(), Run(), 4), "inside"));
    }

    // Check 5

    [TestMethod]
    public void Check05_PhotoAndOptions()
    {
        var run = Run();
        Assert.AreEqual(FordsResult.NotSeen, Result(run, 5));

        run.Client.PauseEdges = 2;
        var row = Row(new FordsSessionEvidence(), run, 5);
        Assert.AreEqual(FordsResult.Fail, PartOf(row, "photo"));
        StringAssert.Contains(row.Parts.Single(p => p.Name == "photo").Numbers, "CustomCamera");

        run.Client.PhotoVetoes = 1;
        run.Options.Component = true;
        run.Options.Requested = 1;
        Assert.AreEqual(FordsResult.Pass, Result(run, 5));

        run.Options.Component = false;
        Assert.AreEqual(FordsResult.Fail, PartOf(Row(new FordsSessionEvidence(), run, 5), "options"));
    }

    // Check 6

    [TestMethod]
    public void Check06_GrantBuyAndFeed()
    {
        var s = new FordsSessionEvidence();
        var run = Run();
        s.Console.Add(Cmd("fords_gold", new[] { "500" }, true, "Added 500 gold. You now hold 1500."));
        run.Client.Commands["BuyPack:Ok"] = 2;
        run.Client.PackBoughtLines = 2;
        Assert.AreEqual(FordsResult.Pass, Row(s, run, 6).Result);

        var idle = new FordsSessionEvidence();
        idle.Console.Add(Cmd("fords_gold", new[] { "500" }, true, FordsOfWarCommands.NoMatchReply));
        Assert.AreEqual(FordsResult.NotSeen, PartOf(Row(idle, run, 6), "grant"), "no match was live for that call");

        idle.Console[0].MatchLive = true;
        Assert.AreEqual(FordsResult.Fail, PartOf(Row(idle, run, 6), "grant"));
    }

    [TestMethod]
    public void Check06_Feed_FailsOnlyAtMissionEnd()
    {
        var run = Run();
        run.Client.Commands["BuyPack:Ok"] = 1;
        run.Client.PanelAttached = true;
        Assert.AreEqual(FordsResult.NotSeen, PartOf(Row(new FordsSessionEvidence(), run, 6), "feed"));

        run.Closed = true;
        Assert.AreEqual(FordsResult.Fail, PartOf(Row(new FordsSessionEvidence(), run, 6), "feed"));
    }

    [TestMethod]
    public void Check06_ReplyPrefixes_MatchFordsOfWarCommands()
    {
        var session = new FordsSession();
        var reply = FordsOfWarCommands.Gold(new[] { "5" }, session);
        Assert.AreEqual(FordsOfWarCommands.NoMatchReply, reply);
    }

    // Check 7

    private static FordsHeroMark Hero(string kind, string id, double mt, int request = -1, string side = "West") =>
        new() { Kind = kind, HeroId = id, Mt = mt, RequestLane = request, Side = side };

    [TestMethod]
    public void Check07_Field_ASpawnOfTheSameHeroWithinTheWindow_Passes()
    {
        var run = Run();
        run.Heroes.Add(Hero("fielded", "h1", 100));
        run.Heroes.Add(Hero("spawned", "h1", 101, 1));
        run.Mt = 110;

        Assert.AreEqual(FordsResult.Pass, PartOf(Row(new FordsSessionEvidence(), run, 7), "field"));
    }

    [TestMethod]
    public void Check07_Field_NoSpawnAfterTheWindow_Fails()
    {
        var late = Run();
        late.Heroes.Add(Hero("fielded", "h1", 100));
        late.Mt = 110;
        Assert.AreEqual(FordsResult.Fail, PartOf(Row(new FordsSessionEvidence(), late, 7), "field"), "10 s on and still no spawn");
    }

    [TestMethod]
    public void Check07_Field_TheMatchWasLeftBeforeTheSpawn_FailsOnTheRunsOwnClock()
    {
        var run = Run();
        run.Heroes.Add(Hero("fielded", "h1", 505));
        run.Mt = 515;

        Assert.AreEqual(FordsResult.Fail, PartOf(Row(new FordsSessionEvidence(), run, 7), "field"), "fielded at 505, no spawn, the clock reached 515");
    }

    [TestMethod]
    public void Check07_Field_NoSpawnYetInsideTheWindow_IsNotSeen()
    {
        var run = Run();
        run.Heroes.Add(Hero("fielded", "h1", 100));
        run.Mt = 102;

        Assert.AreEqual(FordsResult.NotSeen, PartOf(Row(new FordsSessionEvidence(), run, 7), "field"));
    }

    [TestMethod]
    public void Check07_Field_ASpawnOfTheOtherSide_DoesNotLinkTheMark()
    {
        var run = Run();
        run.Heroes.Add(Hero("fielded", "h1", 100));
        run.Heroes.Add(Hero("spawned", "h1", 101, 1, side: "East"));
        run.Mt = 110;

        Assert.AreEqual(FordsResult.Fail, PartOf(Row(new FordsSessionEvidence(), run, 7), "field"));
    }

    [TestMethod]
    public void Check07_Field_APurchaseAtTheEndOfTheMatch_IsNotSeenNotFail()
    {
        var run = Run();
        run.Heroes.Add(Hero("fielded", "h1", 100));
        run.Mt = 200;
        run.End.WinnerMt = 103;
        Assert.AreEqual(FordsResult.NotSeen, PartOf(Row(new FordsSessionEvidence(), run, 7), "field"), "the clock ended 3 s after the mark");

        run.End.WinnerMt = 106;
        Assert.AreEqual(FordsResult.Fail, PartOf(Row(new FordsSessionEvidence(), run, 7), "field"), "6 s is past the allowance");
    }

    [TestMethod]
    public void Check07_Field_ALatePurchaseDoesNotTakeAwayAPassTheOtherHeroesEarned()
    {
        var run = Run();
        run.Heroes.Add(Hero("fielded", "h1", 100));
        run.Heroes.Add(Hero("spawned", "h1", 101, 1));
        run.Heroes.Add(Hero("fielded", "h2", 900));
        run.Mt = 905;
        run.End.WinnerMt = 903;

        Assert.AreEqual(FordsResult.Pass, PartOf(Row(new FordsSessionEvidence(), run, 7), "field"), "h2 was bought as the keep fell: excused, not unseen");
    }

    [TestMethod]
    public void Check07_DiedAndRespawn()
    {
        var run = Run();
        run.Heroes.Add(Hero("died", "h1", 50));
        run.Heroes.Add(Hero("respawned", "h1", 90));
        run.Heroes.Add(Hero("spawned", "h1", 90.5, 0));
        var row = Row(new FordsSessionEvidence(), run, 7);
        Assert.AreEqual(FordsResult.Pass, PartOf(row, "died"));
        Assert.AreEqual(FordsResult.Pass, PartOf(row, "respawn"));

        run.Heroes.RemoveAt(2);
        run.Mt = 95;
        Assert.AreEqual(FordsResult.Fail, PartOf(Row(new FordsSessionEvidence(), run, 7), "respawn"));
    }

    // Check 8

    [TestMethod]
    public void Check08_Orders_NeedAllThree()
    {
        var run = Run();
        run.Client.Commands["Order:Push:Ok"] = 1;
        run.Client.Commands["Order:Hold:Ok"] = 1;
        Assert.AreEqual(FordsResult.NotSeen, PartOf(Row(new FordsSessionEvidence(), run, 8), "orders"));

        run.Client.Commands["Order:FallBack:Ok"] = 1;
        Assert.AreEqual(FordsResult.Pass, PartOf(Row(new FordsSessionEvidence(), run, 8), "orders"));
    }

    [TestMethod]
    public void Check08_Hold_PassFailNotSeen()
    {
        var run = Run();
        Assert.AreEqual(FordsResult.NotSeen, PartOf(Row(new FordsSessionEvidence(), run, 8), "hold"));

        run.Lanes[0].HoldSamples = 6;
        run.Lanes[0].MaxInFireRun = 1;
        Assert.AreEqual(FordsResult.Pass, PartOf(Row(new FordsSessionEvidence(), run, 8), "hold"));

        run.Lanes[2].HoldSamples = 3;
        run.Lanes[2].MaxInFireRun = 2;
        Assert.AreEqual(FordsResult.Fail, PartOf(Row(new FordsSessionEvidence(), run, 8), "hold"));
    }

    [TestMethod]
    public void Check08_Hold_AFailingLaneNamesTheTowerThatCoveredIt_AndTheRowCarriesTheBaseDistance()
    {
        var run = Run();
        run.Facts = Facts() with { BaseDistance = 920f };
        run.Lanes[0].HoldSamples = 4;
        run.Lanes[0].MaxInFireRun = 3;
        run.Lanes[0].FireTower = "tower_east_1_0";

        var row = Row(new FordsSessionEvidence(), run, 8);

        Assert.AreEqual(FordsResult.Fail, PartOf(row, "hold"));
        StringAssert.Contains(row.Parts.Single(p => p.Name == "hold").Numbers, "West0 in fire of tower_east_1_0");
        StringAssert.Contains(row.Info, "tower=tower_east_1_0");
        StringAssert.Contains(row.Info, "baseDistance=920.0");
    }

    [TestMethod]
    public void Check04_FirstSpawnInfo_PrintsOneValuePerRealLane()
    {
        var run = Run();
        run.Lanes[1].FirstSpawnMt = 12.5;

        var info = Row(new FordsSessionEvidence(), run, 4).Info;

        StringAssert.Contains(info, "firstSpawnMt=-,12.5,-,-");
    }

    // Checks 9 and 10

    private static FordsTowerEvidence Tower(FordsRunEvidence run, string id)
    {
        var t = new FordsTowerEvidence { Owner = "East" };
        run.Towers[id] = t;
        return t;
    }

    private static void Blow(FordsTowerEvidence t, TowerBlowClass c, TowerDamageReason o, long n = 1) => t.Blows[(int)c, (int)o] += n;

    [TestMethod]
    public void Check09_Lock_And_Melee()
    {
        var run = Run();
        var t = Tower(run, "t1");
        Assert.AreEqual(FordsResult.NotSeen, Result(run, 9));

        Blow(t, TowerBlowClass.Melee, TowerDamageReason.Locked);
        Blow(t, TowerBlowClass.Melee, TowerDamageReason.Applied, 3);
        Assert.AreEqual(FordsResult.Pass, Result(run, 9));

        t.DriftFlagged = true;
        Assert.AreEqual(FordsResult.Fail, PartOf(Row(new FordsSessionEvidence(), run, 9), "lock"));
    }

    [TestMethod]
    public void Check09_Melee_FailsWhenEnemiesStoodAtTheTowerAndNothingLanded()
    {
        var run = Run();
        var t = Tower(run, "t1");
        t.MeleeNearUnitSeconds = 30;

        Assert.AreEqual(FordsResult.Fail, PartOf(Row(new FordsSessionEvidence(), run, 9), "melee"));

        Blow(t, TowerBlowClass.Melee, TowerDamageReason.Weaponless, 4);
        var row = Row(new FordsSessionEvidence(), run, 9);
        StringAssert.Contains(row.Info, "Weaponless=4");
    }

    [TestMethod]
    public void Check10_OwnSide()
    {
        var run = Run();
        var t = Tower(run, "t1");
        Assert.AreEqual(FordsResult.NotSeen, Result(run, 10));

        Blow(t, TowerBlowClass.Ranged, TowerDamageReason.OwnSide);
        Assert.AreEqual(FordsResult.Pass, Result(run, 10));

        t.SideUnknownApplied = 2;
        StringAssert.Contains(Row(new FordsSessionEvidence(), run, 10).Info, "sideUnknownApplied=2");

        t.DriftFlagged = true;
        Assert.AreEqual(FordsResult.Fail, Result(run, 10));
    }

    // Check 11

    private static FordsVolleyHit Hit(double impact, float mult = 1f, double? magnitude = null, float total = 40f, float launch = 50f, bool shielded = false)
    {
        var scale = impact / 50.0;
        return new FordsVolleyHit((float)(magnitude ?? 40.0 * mult * scale * scale), 30, 5f, total, launch, (float)impact, mult, "Chest", shielded, false, true);
    }

    private static FordsRunEvidence VolleyRun(params FordsVolleyHit[] hits)
    {
        var run = Run();
        run.Volley.ItemReported = true;
        run.Volley.Item = new FordsVolleyItemInfo("javelin", 40, 50f, 90f);
        run.Volley.Hits.AddRange(hits);
        return run;
    }

    [TestMethod]
    public void Check11_RatioIsOne_ForASyntheticEngineHit()
    {
        var run = VolleyRun(Hit(50), Hit(42, 1.1f), Hit(33, 0.9f));

        var row = Row(new FordsSessionEvidence(), run, 11);

        Assert.AreEqual(FordsResult.Pass, row.Result);
        StringAssert.Contains(row.Parts.Single(p => p.Name == "hits").Numbers, "medianR=1.000");
    }

    [TestMethod]
    public void Check11_EveryMultiplierAt1Point5_StillGivesARatioOfOne()
    {
        var run = VolleyRun(Hit(50, 1.5f), Hit(42, 1.5f), Hit(33, 1.5f));

        var row = Row(new FordsSessionEvidence(), run, 11);

        Assert.AreEqual(FordsResult.Pass, row.Result);
        StringAssert.Contains(row.Parts.Single(p => p.Name == "hits").Numbers, "medianR=1.000");
    }

    [TestMethod]
    public void Check11_PerBodyPartMedians_LeaveOutShieldBlockedHits()
    {
        var blocked = new FordsVolleyHit(40f, 999, 77f, 40f, 50f, 50f, 1f, "Chest", true, false, true);
        var run = VolleyRun(Hit(50), Hit(50), Hit(50), blocked, blocked, blocked, blocked);

        var row = Row(new FordsSessionEvidence(), run, 11);

        StringAssert.Contains(row.Info, "Chest:inflicted=30.0,absorbed=5.0");
    }

    [TestMethod]
    public void Check11_MedianOutside_OrMismatchedItem_Fails()
    {
        var off = VolleyRun(Hit(50, 1f, 80), Hit(50, 1f, 80), Hit(50, 1f, 80));
        Assert.AreEqual(FordsResult.Fail, PartOf(Row(new FordsSessionEvidence(), off, 11), "hits"));

        var mismatch = VolleyRun(Hit(50, 1f, null, total: 41f));
        var row = Row(new FordsSessionEvidence(), mismatch, 11);
        Assert.AreEqual(FordsResult.Fail, PartOf(row, "hits"));
        StringAssert.Contains(row.Parts.Single(p => p.Name == "hits").Numbers, "override");
    }

    [TestMethod]
    public void Check11_FewerThanThreeQualifying_IsNotSeen_AndShieldedHitsDoNotCount()
    {
        var run = VolleyRun(Hit(50), Hit(50), Hit(50, shielded: true));

        var row = Row(new FordsSessionEvidence(), run, 11);

        Assert.AreEqual(FordsResult.NotSeen, PartOf(row, "hits"));
        Assert.AreEqual(FordsResult.Pass, PartOf(row, "item"));
    }

    [TestMethod]
    public void Check11_Item_FailsWhenMissingOrTooShort()
    {
        var run = Run();
        Assert.AreEqual(FordsResult.NotSeen, PartOf(Row(new FordsSessionEvidence(), run, 11), "item"));

        run.Volley.ItemReported = true;
        Assert.AreEqual(FordsResult.Fail, PartOf(Row(new FordsSessionEvidence(), run, 11), "item"));

        run.Volley.Item = new FordsVolleyItemInfo("javelin", 40, 50f, 50f);
        run.Volley.ReachShort = true;
        Assert.AreEqual(FordsResult.Fail, PartOf(Row(new FordsSessionEvidence(), run, 11), "item"));
    }

    // Check 12

    [TestMethod]
    public void Check12_Warg()
    {
        var run = Run();
        Assert.AreEqual(FordsResult.NotSeen, Result(run, 12));
        Assert.IsFalse(Row(new FordsSessionEvidence(), run, 12).Parts.Single(p => p.Name == "started").Numbers.Contains("wargsFielded"));
        StringAssert.Contains(Row(new FordsSessionEvidence(), run, 12).Parts.Single(p => p.Name == "started").Numbers, "field a warg rider");

        run.Warg.Started = 4;
        run.Warg.Restored = 4;
        run.Warg.Returned = 4;
        Assert.AreEqual(FordsResult.Pass, Result(run, 12));

        run.Warg.NotReturned = 1;
        Assert.AreEqual(FordsResult.Fail, PartOf(Row(new FordsSessionEvidence(), run, 12), "returned"));

        run.Warg.NullFormation = 1;
        Assert.AreEqual(FordsResult.Fail, PartOf(Row(new FordsSessionEvidence(), run, 12), "restored"));

        run.Warg.NullFormation = 0;
        Assert.AreEqual(FordsResult.Pass, PartOf(Row(new FordsSessionEvidence(), run, 12), "restored"));
        run.Warg.Stranded = 2;
        run.Warg.Open = 3;
        var row = Row(new FordsSessionEvidence(), run, 12);
        Assert.AreEqual(FordsResult.Fail, PartOf(row, "restored"));
        StringAssert.Contains(row.Parts.Single(p => p.Name == "restored").Numbers, "stranded=2");
        StringAssert.Contains(row.Info, "open=3");

        run.Warg.Superseded = 2;
        StringAssert.Contains(Row(new FordsSessionEvidence(), run, 12).Info, "superseded=2");
    }

    // Check 13

    [TestMethod]
    public void Check13_Capture()
    {
        var run = Run();
        Assert.AreEqual(FordsResult.NotSeen, Result(run, 13));

        run.Points["point_0"] = new FordsPointEvidence { Captures = 1, ContestedSeconds = 6 };

        Assert.AreEqual(FordsResult.Pass, Result(run, 13));
    }

    // Check 14

    [TestMethod]
    public void Check14_ExitViaButton_Passes()
    {
        var run = Run();
        run.Closed = true;
        var e = run.End;
        e.KeepFellMt = 900;
        e.Winner = "West";
        e.WinnerMt = 900;
        e.WinnerWall = 910;
        e.ResultSuccess = true;
        e.EndGameVia = "button";
        e.EndGameWall = 915;
        e.ClosedWall = 918;
        e.MenuReturnedWall = 925;

        Assert.AreEqual(FordsResult.Pass, Result(run, 14));
    }

    [TestMethod]
    public void Check14_ResultMismatch_Fails_AndSlowExitFails()
    {
        var run = Run();
        run.End.Winner = "East";
        run.End.ResultSuccess = true;
        Assert.AreEqual(FordsResult.Fail, PartOf(Row(new FordsSessionEvidence(), run, 14), "result"));

        run.End.ResultSuccess = false;
        run.End.EndGameVia = "auto";
        run.End.EndGameWall = 10;
        run.End.ClosedWall = 40;
        var row = Row(new FordsSessionEvidence(), run, 14);
        Assert.AreEqual(FordsResult.Pass, PartOf(row, "result"));
        Assert.AreEqual(FordsResult.Fail, PartOf(row, "exit"));
    }

    [TestMethod]
    public void Check14_ClosedWithNoResult_Fails_AndNoEndGameIsNotSeen()
    {
        var run = Run();
        run.End.Winner = "West";
        Assert.AreEqual(FordsResult.NotSeen, PartOf(Row(new FordsSessionEvidence(), run, 14), "result"));

        run.Closed = true;
        run.End.ClosedWall = 50;
        var row = Row(new FordsSessionEvidence(), run, 14);
        Assert.AreEqual(FordsResult.Fail, PartOf(row, "result"));
        Assert.AreEqual(FordsResult.NotSeen, PartOf(row, "exit"));
        StringAssert.Contains(row.Parts.Single(p => p.Name == "exit").Numbers, "escape menu");
    }

    // Check 15

    [TestMethod]
    public void Check15_RouteA_FullPath_Passes()
    {
        var run = Run();
        run.Failure = new FordsFailure { Route = "A", Wall = 5, Mt = 1 };
        run.Closed = true;
        run.End.ExitReason = "AfterFailedStart";
        run.End.ExitDueWall = 8.2;
        run.End.ExitDueMt = 3.0;
        run.End.EndGameVia = "auto";
        run.End.NoticeRequested = true;
        run.End.NoticeOk = true;
        run.End.NoticeAckWall = 20;
        run.End.MenuReturnedWall = 15;
        run.End.EndGameWall = 8.5;
        run.End.ClosedWall = 12;

        Assert.AreEqual(FordsResult.Pass, Result(run, 15));
    }

    [TestMethod]
    public void Check15_RouteA_AnExitThatTakesOverFifteenSecondsToClose_Fails()
    {
        var run = Run();
        run.Failure = new FordsFailure { Route = "A", Wall = 5, Mt = 1 };
        run.Closed = true;
        run.End.ExitReason = "AfterFailedStart";
        run.End.ExitDueWall = 8.2;
        run.End.ExitDueMt = 3.0;
        run.End.EndGameVia = "auto";
        run.End.EndGameWall = 8.5;
        run.End.ClosedWall = 23.6;

        Assert.AreEqual(FordsResult.Fail, PartOf(Row(new FordsSessionEvidence(), run, 15), "exit"));

        run.End.ClosedWall = 23.5;
        Assert.AreEqual(FordsResult.Pass, PartOf(Row(new FordsSessionEvidence(), run, 15), "exit"), "15 s is still inside the bound");
    }

    [TestMethod]
    public void Check15_RouteA_TheExitIsTimedInMissionSeconds_APauseDoesNotFailIt()
    {
        var run = Run();
        run.Failure = new FordsFailure { Route = "A", Wall = 5, Mt = 1 };
        run.Closed = true;
        run.End.ExitReason = "AfterFailedStart";
        run.End.ExitDueWall = 95;
        run.End.ExitDueMt = 4;
        run.End.EndGameVia = "auto";
        run.End.EndGameWall = 95.5;
        run.End.ClosedWall = 99;

        Assert.AreEqual(FordsResult.Pass, PartOf(Row(new FordsSessionEvidence(), run, 15), "exit"), "90 s of wall time, 3 mission seconds");

        run.End.ExitDueMt = 9.5;
        Assert.AreEqual(FordsResult.Fail, PartOf(Row(new FordsSessionEvidence(), run, 15), "exit"), "8.5 mission seconds is past the bound");
    }

    [TestMethod]
    public void Check15_RouteBOnly_IsNotSeenWithNote()
    {
        var run = Run();
        run.Failure = new FordsFailure { Route = "B", Reason = "x" };
        run.Closed = true;

        var row = Row(new FordsSessionEvidence(), run, 15);

        Assert.AreEqual(FordsResult.NotSeen, PartOf(row, "failure"));
        StringAssert.Contains(row.Parts.Single(p => p.Name == "failure").Numbers, "notice path via B or C");
    }

    [TestMethod]
    public void Check15_RouteA_SlowExitOrNoExitDue_Fails_AndNoFailureSaysUnreachable()
    {
        var run = Run();
        run.Failure = new FordsFailure { Route = "A", Wall = 5 };
        run.End.ExitDueMt = 9;
        Assert.AreEqual(FordsResult.Fail, PartOf(Row(new FordsSessionEvidence(), run, 15), "exit"));

        run.End.ExitDueMt = null;
        run.Closed = true;
        Assert.AreEqual(FordsResult.Fail, PartOf(Row(new FordsSessionEvidence(), run, 15), "exit"));

        var none = Row(new FordsSessionEvidence(), Run(), 15);
        StringAssert.Contains(none.Parts.Single(p => p.Name == "failure").Numbers, "unreachable");
    }

    [TestMethod]
    public void Check15_Notice()
    {
        var run = Run();
        run.End.NoticeRequested = true;
        run.End.NoticeOk = true;
        Assert.AreEqual(FordsResult.NotSeen, PartOf(Row(new FordsSessionEvidence(), run, 15), "notice"));

        run.End.NoticeOk = false;
        Assert.AreEqual(FordsResult.Fail, PartOf(Row(new FordsSessionEvidence(), run, 15), "notice"));
    }

    // Check 16

    [TestMethod]
    public void Check16_FramesAreInfoOnly_NoBudgetSet()
    {
        var run = Run();
        run.Perf.LiveSeconds = 120;
        run.Perf.PeakLiveWest = 100;
        run.Perf.PeakLiveEast = 120;
        run.Perf.Windows.Add(new FordsPerfWindow { Frames = 100, AvgMs = 900, P95Ms = 2000, MaxMs = 5000 });

        var row = Row(new FordsSessionEvidence(), run, 16);

        Assert.AreEqual(FordsResult.Pass, PartOf(row, "cap"));
        Assert.AreEqual(FordsResult.NotSeen, PartOf(row, "duration"));
        StringAssert.Contains(row.Info, "no budget set");
    }

    [TestMethod]
    public void Check16_PeakAboveTheCap_Fails_AndThirtyMinutesPassesDuration()
    {
        var run = Run();
        run.Perf.LiveSeconds = 1800;
        run.Perf.PeakLiveWest = 351;

        var row = Row(new FordsSessionEvidence(), run, 16);

        Assert.AreEqual(FordsResult.Fail, PartOf(row, "cap"));
        Assert.AreEqual(FordsResult.Pass, PartOf(row, "duration"));
    }

    [TestMethod]
    public void Check16_UnderAMinute_IsNotSeen()
    {
        var run = Run();
        run.Perf.LiveSeconds = 30;

        Assert.AreEqual(FordsResult.NotSeen, PartOf(Row(new FordsSessionEvidence(), run, 16), "cap"));
    }

    [TestMethod]
    public void Check16_Agents_PassBelowTheEngineLimit_FailAboveIt_AndNotSeenWithoutOne()
    {
        var run = Run();
        run.Facts = Facts() with { AgentLimit = 1000 };
        run.Perf.LiveSeconds = 120;
        run.Perf.PeakAgentsAll = 800;
        Assert.AreEqual(FordsResult.Pass, PartOf(Row(new FordsSessionEvidence(), run, 16), "agents"));

        run.Perf.PeakAgentsAll = 1001;
        var row = Row(new FordsSessionEvidence(), run, 16);
        Assert.AreEqual(FordsResult.Fail, PartOf(row, "agents"));
        StringAssert.Contains(row.Parts.Single(p => p.Name == "agents").Numbers, "limit=1000");

        run.Perf.PeakAgentsAll = 800;
        run.Perf.LiveSeconds = 30;
        Assert.AreEqual(FordsResult.NotSeen, PartOf(Row(new FordsSessionEvidence(), run, 16), "agents"));

        run.Perf.LiveSeconds = 120;
        run.Facts = Facts();
        Assert.AreEqual(FordsResult.NotSeen, PartOf(Row(new FordsSessionEvidence(), run, 16), "agents"), "no limit recorded");
    }

    [TestMethod]
    public void Check11_Hits_FailsWhenTheTowersNeverFireAfterTenRefusals()
    {
        var run = VolleyRun();
        run.Volley.NoTarget = 4;
        run.Volley.NoGunnerOrItem = 5;
        Assert.AreEqual(FordsResult.NotSeen, PartOf(Row(new FordsSessionEvidence(), run, 11), "hits"));

        run.Volley.NoGunnerOrItem = 6;
        Assert.AreEqual(FordsResult.Fail, PartOf(Row(new FordsSessionEvidence(), run, 11), "hits"));

        run.Volley.Launched = 1;
        Assert.AreEqual(FordsResult.NotSeen, PartOf(Row(new FordsSessionEvidence(), run, 11), "hits"), "some missiles flew");
    }

    [TestMethod]
    public void Check11_Hits_FailsWhenTwentyMissilesFlewAndNoHitWasSeen_AndPrintsTheFilteredHits()
    {
        var run = VolleyRun();
        run.Volley.Launched = 19;
        Assert.AreEqual(FordsResult.NotSeen, PartOf(Row(new FordsSessionEvidence(), run, 11), "hits"));

        run.Volley.Launched = 20;
        run.Volley.Filtered["otherItem"] = 7;
        var row = Row(new FordsSessionEvidence(), run, 11);
        Assert.AreEqual(FordsResult.Fail, PartOf(row, "hits"));
        StringAssert.Contains(row.Parts.Single(p => p.Name == "hits").Numbers, "otherItem=7");

        run.Volley.HitsSeen = 1;
        Assert.AreNotEqual(FordsResult.Fail, PartOf(Row(new FordsSessionEvidence(), run, 11), "hits"));
    }
}
