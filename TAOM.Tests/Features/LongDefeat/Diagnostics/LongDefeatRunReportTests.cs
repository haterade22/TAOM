using System;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Newtonsoft.Json.Linq;
using TAOM.Features.LongDefeat.Diagnostics;
using TAOM.Features.LongDefeat.Domain;

namespace TAOM.Tests.Features.LongDefeat.Diagnostics;

[TestClass]
public class LongDefeatRunReportTests
{
    private static readonly DateTime Started = new(2026, 11, 4, 12, 0, 0, DateTimeKind.Utc);

    private static string Text(RunResult result, RunStats stats, Purse purse, RunOptions? options = null, RunProvenance? provenance = null) =>
        LongDefeatRunReport.Build("helms", options ?? RunOptions.Default, provenance ?? RunProvenance.Unknown, result, stats, purse, Started, Started.AddMinutes(40));

    private static JObject Build(RunResult result, RunStats stats, Purse purse) =>
        JObject.Parse(Text(result, stats, purse));

    [TestMethod]
    public void Build_CarriesWhatALeaderboardChecks()
    {
        var provenance = new RunProvenance("v2.0.0.0 build.1+abc", "0123456789abcdef", new[] { "Native", "TAOM" }, true);
        var options = new RunOptions { Autoplay = true, GarrisonScale = 0.5f, StopAfterWaves = 5 };

        var text = Text(new RunResult(RunEndReason.DefendersDead, 2, 1, 80), new RunStats(), new Purse(600), options, provenance);
        var report = JObject.Parse(text);

        Assert.AreEqual(1, (int)report["schema"]!);
        Assert.AreEqual("v2.0.0.0 build.1+abc", (string?)report["build"]);
        Assert.AreEqual("0123456789abcdef", (string?)report["balance"]);
        Assert.IsTrue((bool)report["cheatMode"]!);
        CollectionAssert.AreEqual(new[] { "Native", "TAOM" }, report["mods"]!.Select(m => (string)m!).ToArray());
        Assert.IsTrue((bool)report["autoplay"]!);
        Assert.AreEqual(0.5f, (float)report["garrisonScale"]!);
        Assert.AreEqual(5, (int)report["targetWaves"]!);
        StringAssert.Contains(text, "\"startedUtc\": \"2026-11-04T12:00:00.0000000Z\"");
        StringAssert.Contains(text, "\"endedUtc\": \"2026-11-04T12:40:00.0000000Z\"");
    }

    [TestMethod]
    public void Build_CarriesTheHighestSpeedOfTheRun_AndOneWhenNoneIsGiven()
    {
        var result = new RunResult(RunEndReason.DefendersDead, 2, 1, 80);

        var fast = JObject.Parse(LongDefeatRunReport.Build("helms", RunOptions.Default, RunProvenance.Unknown, result, new RunStats(), new Purse(600),
            Started, Started.AddMinutes(40), 10));

        Assert.AreEqual(10, (int)fast["maxSpeed"]!);
        Assert.AreEqual(1, (int)Build(result, new RunStats(), new Purse(600))["maxSpeed"]!);
    }

    [TestMethod]
    public void Build_APlayersRun_ReadsAsCleanWithNoTarget()
    {
        var report = Build(new RunResult(RunEndReason.DefendersDead, 2, 1, 80), new RunStats(), new Purse(600));

        Assert.IsFalse((bool)report["autoplay"]!);
        Assert.IsFalse((bool)report["cheatMode"]!);
        Assert.AreEqual(1f, (float)report["garrisonScale"]!);
        Assert.AreEqual(JTokenType.Null, report["targetWaves"]!.Type);
    }

    [TestMethod]
    public void Build_CarriesTheResultTheWavesAndTheGoldBooks()
    {
        var stats = new RunStats();
        stats.AttackerKilled();
        stats.AttackerKilled();
        stats.DefenderLost();
        stats.CloseWave(1, 42.5f, felled: 3);
        stats.PlayerDied();
        stats.AttackerFled();
        stats.ObserveFleeing(4);
        var purse = new Purse(600);
        purse.Earn(PurseSource.Bounty, 8);
        purse.Earn(PurseSource.WaveBonus, 200);
        purse.Earn(PurseSource.EarlyStart, 30);
        purse.TrySpend(180);

        var report = Build(new RunResult(RunEndReason.TargetReached, 1, 1, 2), stats, purse);

        Assert.AreEqual("helms", (string?)report["fortress"]);
        Assert.IsTrue((bool)report["final"]!);
        Assert.AreEqual("TargetReached", (string?)report["reason"]);
        Assert.AreEqual(1, (int)report["wavesCleared"]!);
        Assert.AreEqual(2, (int)report["kills"]!);
        Assert.AreEqual(1, (int)report["fled"]!);
        Assert.AreEqual(4, (int)report["fleeingPeak"]!);
        Assert.AreEqual(1, (int)report["playerDeaths"]!);
        Assert.AreEqual(1, (int)report["wavesFelled"]!);
        var wave = (JObject)report["waves"]!.Single();
        Assert.AreEqual((1, 2, 3, 1), ((int)wave["wave"]!, (int)wave["kills"]!, (int)wave["felled"]!, (int)wave["lost"]!));
        Assert.AreEqual(42.5, (double)wave["seconds"]!, 0.001);
        var gold = (JObject)report["gold"]!;
        Assert.AreEqual((600, 238, 180, 658), ((int)gold["start"]!, (int)gold["earned"]!, (int)gold["spent"]!, (int)gold["balance"]!));
        Assert.AreEqual((8, 200, 30), ((int)gold["bounty"]!, (int)gold["waveBonus"]!, (int)gold["earlyStart"]!));
    }

    [TestMethod]
    public void Build_ALostRunIsNotFinal()
    {
        var report = Build(new RunResult(RunEndReason.DefendersDead, 2, 1, 80), new RunStats(), new Purse(600));

        Assert.IsFalse((bool)report["final"]!);
        Assert.AreEqual("DefendersDead", (string?)report["reason"]);
        Assert.AreEqual(0, report["waves"]!.Count());
    }

    [TestMethod]
    public void Build_NaNSeconds_StillParses()
    {
        var stats = new RunStats();
        stats.CloseWave(1, float.NaN, 0);

        var text = Text(new RunResult(RunEndReason.Abandoned, 1, 1, 0), stats, new Purse(0));

        Assert.IsNotNull(JObject.Parse(text), "a NaN timer must not break the report");
    }
}
