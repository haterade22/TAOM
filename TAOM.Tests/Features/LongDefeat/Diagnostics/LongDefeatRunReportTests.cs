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

        Assert.AreEqual(2, (int)report["schema"]!);
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

    [TestMethod]
    public void Build_SchemaTwoCarriesTheRunsSeedAndTestOptions()
    {
        var options = new RunOptions { Seed = 9007199254740991UL, StartWave = 12, StartRerolls = 1 };

        var text = Text(new RunResult(RunEndReason.TargetReached, 15, 4, 90), new RunStats(), new Purse(600), options);
        var report = JObject.Parse(text);

        StringAssert.Contains(text, "\"seed\": 9007199254740991", "the seed is written as exact digits");
        Assert.AreEqual(12, (int)report["startWave"]!);
        Assert.AreEqual(1, (int)report["startRerolls"]!);
        Assert.AreEqual("", (string?)report["class"], "a run with no class on offer reports an empty one");
    }

    [TestMethod]
    public void Build_TheClassIsTheOptionsResolvedClassId()
    {
        var options = new RunOptions { Seed = 1UL, ClassId = "archer" };

        var report = JObject.Parse(Text(new RunResult(RunEndReason.TargetReached, 2, 2, 9), new RunStats(), new Purse(600), options));

        Assert.AreEqual("archer", (string?)report["class"]);
    }

    [TestMethod]
    public void Build_AnUnseededOptionsRecordWritesNullNotAMadeUpSeed()
    {
        var report = Build(new RunResult(RunEndReason.DefendersDead, 1, 0, 0), new RunStats(), new Purse(600));

        Assert.AreEqual(JTokenType.Null, report["seed"]!.Type);
        Assert.AreEqual(1, (int)report["startWave"]!);
        Assert.AreEqual(0, (int)report["startRerolls"]!);
    }

    [TestMethod]
    public void Build_EveryNewFieldIsPresentAtItsEmptyValue()
    {
        var stats = new RunStats();
        stats.AttackerKilled();
        stats.CloseWave(1, 10f, 0);

        var report = Build(new RunResult(RunEndReason.TargetReached, 1, 1, 1), stats, new Purse(600));

        Assert.AreEqual(0, ((JArray)report["boons"]!).Count);
        var rerolls = (JObject)report["rerolls"]!;
        Assert.AreEqual((0, 0, 0), ((int)rerolls["earned"]!, (int)rerolls["used"]!, (int)rerolls["banked"]!));
        var wave = (JObject)report["waves"]!.Single();
        Assert.AreEqual(0, ((JArray)wave["affixes"]!).Count);
        Assert.AreEqual((1f, 1f), ((float)wave["healthFactor"]!, (float)wave["damageFactor"]!));
        Assert.AreEqual((0, 0, 0), ((int)wave["summoned"]!, (int)wave["summonKills"]!, (int)wave["summonFelled"]!));
        Assert.AreEqual(JTokenType.Null, wave["commander"]!.Type);
        var gold = (JObject)report["gold"]!;
        Assert.AreEqual((0, 0), ((int)gold["commander"]!, (int)gold["boon"]!));
        var poison = (JObject)report["poison"]!;
        Assert.AreEqual((0, 0, 0, 0, 0), ((int)poison["doses"]!, (int)poison["hits"]!, (int)poison["damage"]!, (int)poison["ownerMissing"]!, (int)poison["withheld"]!));
    }

    [TestMethod]
    public void Build_WritesWhatMorgulBladesDid_IntoThePoisonObject()
    {
        var stats = new RunStats();
        stats.Poison.Doses = 7;
        stats.Poison.Hits = 19;
        stats.Poison.Damage = 84;
        stats.Poison.OwnerMissing = 0;
        stats.Poison.Withheld = 2;

        var report = Build(new RunResult(RunEndReason.Abandoned, 5, 4, 0), stats, new Purse(600));

        var poison = (JObject)report["poison"]!;
        Assert.AreEqual((7, 19, 84, 0, 2), ((int)poison["doses"]!, (int)poison["hits"]!, (int)poison["damage"]!, (int)poison["ownerMissing"]!, (int)poison["withheld"]!));
    }

    [TestMethod]
    public void Build_ListsEachPickWithItsDrawsTheBoonTakenAndTheWaveItActsOn_AndTheRerollsAndTheBoonGold()
    {
        var picked = new BoonPickRecord(1, 3) { Taken = "stout", TakenBeforeWave = 4 };
        picked.Draws.Add(new BoonDraw(new[] { "a", "b", "c" }, 9, 9, 1));
        picked.Draws.Add(new BoonDraw(new[] { "d", "e", "stout" }, 9, 6, 0));
        var open = new BoonPickRecord(2, 6);
        open.Draws.Add(new BoonDraw(new[] { "f" }, 1, 1, 0));
        var lapsed = new BoonPickRecord(3, 9);
        var purse = new Purse(600);
        purse.Earn(PurseSource.Boon, 300);

        var report = JObject.Parse(LongDefeatRunReport.Build("helms", RunOptions.Default, RunProvenance.Unknown, new RunResult(RunEndReason.Abandoned, 10, 9, 5),
            new RunStats(), purse, Started, Started.AddMinutes(40), 1, new RunBoons(new[] { picked, open, lapsed }, 2, 1, 1)));

        var picks = (JArray)report["boons"]!;
        Assert.AreEqual(3, picks.Count);
        Assert.AreEqual((1, 3, "stout", 4), ((int)picks[0]["pick"]!, (int)picks[0]["wave"]!, (string?)picks[0]["taken"], (int)picks[0]["takenBeforeWave"]!));
        CollectionAssert.AreEqual(new[] { "a", "b", "c" }, picks[0]["offered"]![0]!.Select(t => (string)t!).ToArray());
        CollectionAssert.AreEqual(new[] { "d", "e", "stout" }, picks[0]["offered"]![1]!.Select(t => (string)t!).ToArray());
        Assert.AreEqual(JTokenType.Null, picks[1]["taken"]!.Type, "a pick still open at the end of the run took nothing");
        Assert.AreEqual(0, ((JArray)picks[2]["offered"]!).Count, "a lapsed pick offered nothing");
        var rerolls = (JObject)report["rerolls"]!;
        Assert.AreEqual((2, 1, 1), ((int)rerolls["earned"]!, (int)rerolls["used"]!, (int)rerolls["banked"]!));
        Assert.AreEqual(300, (int)report["gold"]!["boon"]!);
        Assert.AreEqual((long)report["gold"]!["earned"]!, (long)report["gold"]!["bounty"]! + (long)report["gold"]!["waveBonus"]! + (long)report["gold"]!["earlyStart"]!
            + (long)report["gold"]!["commander"]! + (long)report["gold"]!["boon"]!, "earned is the sum of its five sources");
    }

    [TestMethod]
    public void Build_AWaveWithACommanderAndASummon_ReportsTheirCountsAndTheCommandersOutcome()
    {
        var stats = new RunStats();
        var uses = new System.Collections.Generic.Dictionary<string, int> { ["war_cry"] = 3, ["summon_uruks"] = 1 };
        var commander = new CommanderRecord("lord_I1_1", "lieutenant", 0, CommanderOutcome.Killed, uses, 0, 61.5f);
        stats.CloseWave(8, 200f, 0, 1.2f, 1.1f, null, new WaveRoles(20, 20, 17, 3, commander));
        var purse = new Purse(600);
        purse.Earn(PurseSource.Commander, 250);

        var report = Build(new RunResult(RunEndReason.TargetReached, 8, 1, 0), stats, purse);

        var wave = (JObject)report["waves"]!.Single();
        Assert.AreEqual((20, 20, 17, 3), ((int)wave["summoned"]!, (int)wave["summonSpawned"]!, (int)wave["summonKills"]!, (int)wave["summonFelled"]!));
        var written = (JObject)wave["commander"]!;
        Assert.AreEqual(("lord_I1_1", "lieutenant", 0, "killed", 0), ((string?)written["troop"], (string?)written["rank"], (int)written["steps"]!, (string?)written["outcome"], (int)written["usesAfterEnd"]!));
        Assert.AreEqual((3, 1), ((int)written["uses"]!["war_cry"]!, (int)written["uses"]!["summon_uruks"]!));
        Assert.AreEqual(250, (int)report["gold"]!["commander"]!);
        Assert.AreEqual((long)report["gold"]!["earned"]!, (long)report["gold"]!["bounty"]! + (long)report["gold"]!["waveBonus"]! + (long)report["gold"]!["earlyStart"]!
            + (long)report["gold"]!["commander"]! + (long)report["gold"]!["boon"]!);
    }

    [TestMethod]
    public void Build_AWavePastTheAuthoredList_ReportsItsHealthAndDamageFactors()
    {
        var stats = new RunStats();
        stats.CloseWave(11, 100f, 0, 1.05f, 1.03f);
        stats.CloseWave(12, 100f, 0, 1.10f, 1.06f);

        var waves = ((JArray)Build(new RunResult(RunEndReason.TargetReached, 12, 2, 0), stats, new Purse(600))["waves"]!).Cast<JObject>().ToList();

        Assert.AreEqual((1.05f, 1.03f), ((float)waves[0]["healthFactor"]!, (float)waves[0]["damageFactor"]!));
        Assert.AreEqual((1.10f, 1.06f), ((float)waves[1]["healthFactor"]!, (float)waves[1]["damageFactor"]!));
    }

    [TestMethod]
    public void Build_AWaveThatCarriedAffixes_ReportsTheirPowerIdsInOrder()
    {
        var stats = new RunStats();
        stats.CloseWave(10, 100f, 0, 1f, 1f, new[] { "drums", "terror" });
        stats.CloseWave(11, 100f, 0);

        var waves = ((JArray)Build(new RunResult(RunEndReason.TargetReached, 11, 2, 0), stats, new Purse(600))["waves"]!).Cast<JObject>().ToList();

        CollectionAssert.AreEqual(new[] { "drums", "terror" }, ((JArray)waves[0]["affixes"]!).Select(a => (string)a!).ToArray());
        Assert.AreEqual(0, ((JArray)waves[1]["affixes"]!).Count, "a wave with none still carries the field, empty");
    }

    [TestMethod]
    public void Build_SchemaOneFieldsKeepTheirNamesAndMeaning()
    {
        var report = Build(new RunResult(RunEndReason.TargetReached, 1, 1, 0), new RunStats(), new Purse(600));

        foreach (var key in new[] { "fortress", "build", "balance", "cheatMode", "mods", "autoplay", "garrisonScale", "maxSpeed", "targetWaves", "startedUtc",
                     "endedUtc", "final", "reason", "wave", "wavesCleared", "kills", "fled", "fleeingPeak", "playerDeaths", "wavesFelled", "waves", "gold" })
            Assert.IsNotNull(report[key], "schema 2 is additive: " + key);
        foreach (var key in new[] { "start", "earned", "spent", "balance", "bounty", "waveBonus", "earlyStart" })
            Assert.IsNotNull(report["gold"]![key], "gold." + key);
    }
}
