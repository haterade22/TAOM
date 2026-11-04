using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Newtonsoft.Json.Linq;
using TAOM.Features.AdvancedCombat;
using TAOM.Features.LongDefeat;
using TAOM.Features.LongDefeat.Autorun;
using TAOM.Features.LongDefeat.Diagnostics;
using TAOM.Features.LongDefeat.Domain;
using TAOM.Tests.Features.LoadTimeStamps;

namespace TAOM.Tests.Features.LongDefeat;

[TestClass]
public class LongDefeatDirectorTests
{
    private sealed class Rig
    {
        public FakeLongDefeatField Field { get; } = new();
        public LongDefeatSession Session { get; } = new();
        public RecordingLogger Log { get; } = new();
        public RecordingOutcomeLog Outcomes { get; } = new();
        public LongDefeatDirector Director { get; set; } = null!;

        public List<string> Calls => Field.Calls;

        public void Tick(float dt = 1f) => Director.Tick(dt);

        public void Pump(int ticks, float dt = 1f)
        {
            for (var i = 0; i < ticks; i++)
                Director.Tick(dt);
        }

        public void PumpUntil(Func<bool> done, float dt = 1f, int limit = 200)
        {
            for (var i = 0; i < limit && !done(); i++)
                Director.Tick(dt);
            Assert.IsTrue(done(), "the condition never came true");
        }

        /// <summary>Starts the director, then ticks until the wave's first batch is out.</summary>
        public void StartAndSpawnWave()
        {
            Assert.IsTrue(Director.Start());
            PumpUntil(() => Calls.Contains("Refresh"));
        }

        /// <summary>Every attacker the field holds dies, one removal each, on the calling thread.</summary>
        public void KillAttackers(int count, string troop = "orc")
        {
            for (var i = 0; i < count; i++)
            {
                Field.AttackersStanding--;
                Director.OnAgentRemoved("A:" + troop, true);
            }
        }
    }

    [TestMethod]
    public void TheStateLine_ReportsTheFrameTimesOfItsWindow_AndStartsAgain()
    {
        var rig = NewRig();
        rig.StartAndSpawnWave();
        rig.Tick(6f);
        rig.Pump(5, 1.5f);

        var lines = rig.Log.Lines.Where(l => l.Contains("[State]")).ToList();
        Assert.IsTrue(lines.Count >= 2, "two windows");
        StringAssert.Contains(lines[lines.Count - 2], "max 6000.00 ms");
        StringAssert.Contains(lines[lines.Count - 1], "frame mean 1500.00 ms max 1500.00 ms");
    }

    private static Rig NewRig(RunOptions? options = null, RunRules? rules = null, int gold = 600, KeepPoint? keep = null,
        LongDefeatRunRecords? records = null, RunProvenance? provenance = null)
    {
        var rig = new Rig();
        var waves = new WavePlan(
            new[]
            {
                new WaveSpec(110, new[] { new WaveTroop("orc", FormationSlot.Infantry, 75), new WaveTroop("skirm", FormationSlot.Ranged, 25) }),
                new WaveSpec(120, new[] { new WaveTroop("orc", FormationSlot.Infantry, 100) }),
            },
            new WaveScaling(10, 300));
        var shop = new ShopPlan(gold, new[]
        {
            new ShopOffer("militia", "Militia", "fow_militia", FormationSlot.Infantry, 12, 180),
            new ShopOffer("yeomen", "Yeomen", "fow_yeomen", FormationSlot.Ranged, 10, 200),
        });
        var plan = new RunPlan(
            "helms",
            new[] { new GarrisonRow("fow_militia", FormationSlot.Infantry, 30), new GarrisonRow("fow_yeomen", FormationSlot.Ranged, 10) },
            waves,
            shop,
            rules ?? RunRules.Default with { PreparationSeconds = 1f, IntermissionSeconds = 1f },
            options ?? RunOptions.Default)
        {
            Bounties = new Dictionary<string, int> { ["orc"] = 4, ["skirm"] = 6 },
            Keep = keep,
            Provenance = provenance ?? RunProvenance.Unknown,
        };
        rig.Director = new LongDefeatDirector(plan, rig.Field, rig.Session, rig.Log, rig.Outcomes, "m1", records);
        return rig;
    }

    private static RunRules LongPrep() => RunRules.Default with { PreparationSeconds = 90f, IntermissionSeconds = 1f };

    [TestInitialize]
    public void Init() => MissionThreadGuard.ResetForTests();

    [TestCleanup]
    public void Clean() => MissionThreadGuard.ResetForTests();

    // ---- Start ----

    [TestMethod]
    public void Start_WhenBootFails_SpawnsNothingAndSaysWhy()
    {
        var rig = NewRig();
        rig.Field.BootOk = false;

        Assert.IsFalse(rig.Director.Start());
        rig.Tick();

        Assert.AreEqual(0, rig.Field.Count("Spawn"));
        Assert.IsFalse(rig.Session.IsActive);
        Assert.IsTrue(rig.Log.Lines.Any(l => l.StartsWith("ERROR") && l.Contains("no deployment plan")));
    }

    [TestMethod]
    public void Start_SizesThePlanFromTheGarrisonAndTheLiveCap()
    {
        var rig = NewRig();

        rig.Director.Start();

        Assert.AreEqual(1, rig.Field.Count("Boot:31/10/113/37"), string.Join(",", rig.Calls));
    }

    [TestMethod]
    public void Start_OpensInTheEngineOrder_PlayerBeforeTheFootBatchCloses()
    {
        var rig = NewRig();

        rig.Director.Start();

        var c = rig.Field;
        Assert.IsTrue(c.Index("BreakGates") < c.Index("Deploy"), "the hidden gate breaks before the engines deploy");
        Assert.IsTrue(c.Index("Deploy") < c.Index("Place:def:Ranged:10"));
        Assert.IsTrue(c.Index("Place:def:Infantry:21") >= 0, "the foot formation reserves the player's slot: " + string.Join(",", c.Calls));
        var player = c.Index("SpawnPlayer:False");
        Assert.IsTrue(player > c.Calls.FindLastIndex(x => x.StartsWith("Spawn:def")));
        Assert.IsTrue(c.Index("End:def:Infantry") > player, "the foot batch closes after the player spawns");
        Assert.IsTrue(c.Index("Finish") > c.Index("End:def:Infantry"));
        Assert.IsTrue(rig.Session.IsActive);
    }

    [TestMethod]
    public void Start_RecordsBootGeneralAndKeepChecks()
    {
        var rig = NewRig();

        rig.Director.Start();

        Assert.IsTrue(rig.Outcomes.Has("pass", LongDefeatChecks.BootId));
        Assert.IsTrue(rig.Outcomes.Has("pass", LongDefeatChecks.GeneralId));
        Assert.IsTrue(rig.Outcomes.Has("pass", LongDefeatChecks.KeepId));
    }

    [TestMethod]
    public void Start_FriendlyMission_FailsBoot()
    {
        var rig = NewRig();
        rig.Field.Facts = rig.Field.Facts with { IsFriendlyMission = true };

        rig.Director.Start();

        Assert.IsTrue(rig.Outcomes.Has("fail", LongDefeatChecks.BootId));
    }

    [TestMethod]
    public void Start_NoKeepPoint_FailsKeep_AndTheRunStillStarts()
    {
        var rig = NewRig();
        rig.Field.Keep = null;

        Assert.IsTrue(rig.Director.Start());

        Assert.IsTrue(rig.Outcomes.Has("fail", LongDefeatChecks.KeepId));
    }

    [TestMethod]
    public void Start_ARecordedKeepPoint_IsTheKeepTheFieldMeasures_WithItsOwnRadius()
    {
        var rig = NewRig(keep: new KeepPoint(10f, 20f, 30f, 7f));

        rig.Director.Start();
        rig.Tick();

        Assert.AreEqual(new LdPoint(10f, 20f, 30f), rig.Field.LastReadKeep);
        Assert.AreEqual(7f, rig.Field.LastReadRadius);
        var line = rig.Outcomes.Lines.Single(l => l.StartsWith("pass " + LongDefeatChecks.KeepId + " "));
        StringAssert.Contains(line, "source=recorded");
        StringAssert.Contains(line, "at=(10.0,20.0,30.0)");
    }

    [TestMethod]
    public void Start_NoRecordedKeep_FallsBackToTheSceneFrameAndTheRulesRadius()
    {
        var rig = NewRig();

        rig.Director.Start();
        rig.Tick();

        Assert.AreEqual(rig.Field.Keep, rig.Field.LastReadKeep);
        Assert.AreEqual(RunRules.Default.KeepRadius, rig.Field.LastReadRadius);
        StringAssert.Contains(rig.Outcomes.Lines.Single(l => l.StartsWith("pass " + LongDefeatChecks.KeepId + " ")), "source=scene");
    }

    [TestMethod]
    public void Start_WhenBootFails_RecordsAFailedBootCheckWithTheReason()
    {
        var rig = NewRig();
        rig.Field.BootOk = false;

        rig.Director.Start();

        var line = rig.Outcomes.Lines.Single(l => l.StartsWith("fail " + LongDefeatChecks.BootId + " "));
        StringAssert.Contains(line, "reason=no deployment plan");
    }

    [TestMethod]
    public void Start_AScaledGarrison_SizesThePlanFromTheCountsThatSpawn()
    {
        var rig = NewRig(new RunOptions { GarrisonScale = 0.2f });

        rig.Director.Start();

        Assert.AreEqual(1, rig.Field.Count("Boot:7/2/113/37"), string.Join(",", rig.Calls));
        Assert.AreEqual(1, rig.Field.Count("Place:def:Infantry:7"), "the foot formation plans six soldiers and the lord");
    }

    [TestMethod]
    public void Start_LogsTheGeneralsFormations_AndTheEnginesAtBoot()
    {
        var rig = NewRig();

        rig.Director.Start();

        Assert.IsTrue(rig.Log.Lines.Any(l => l.Contains("[LongDefeat][General] ai_formations=3 of 3")), string.Join("\n", rig.Log.Lines));
        Assert.IsTrue(rig.Log.Lines.Any(l => l.Contains("[LongDefeat][Engines]")));
    }

    // ---- Muster and placing ----

    [TestMethod]
    public void Muster_PlacesEachDefenderFormationOnce_LaterBatchesOnlyBegin()
    {
        var rig = NewRig(rules: LongPrep());
        rig.Director.Start();

        Assert.AreEqual(BuyResult.Ok, rig.Session.TryBuy("militia"));
        rig.Tick(5f);

        Assert.AreEqual(1, rig.Field.Count("Place:def:Infantry"));
        Assert.AreEqual(1, rig.Field.Count("Place:def:Ranged"));
        Assert.AreEqual(1, rig.Field.Count("Begin:def:Infantry:10"), "the muster's second batch: " + string.Join(",", rig.Calls));
        Assert.AreEqual(1, rig.Field.Count("Begin:def:Infantry:12"), "the bought pack");
    }

    [TestMethod]
    public void Muster_AFirstBatchThatNeverMovedOn_FailsTheMusterCheck()
    {
        var rig = NewRig(rules: LongPrep());
        rig.Field.SoldiersMoveOn = false;
        rig.Director.Start();

        rig.Tick(5f);

        Assert.IsTrue(rig.Outcomes.Has("fail", LongDefeatChecks.MusterId), string.Join("\n", rig.Outcomes.Lines));
        Assert.IsTrue(rig.Log.Lines.Any(l => l.Contains("[LongDefeat][Muster]") && l.Contains("overlap=10")));
    }

    [TestMethod]
    public void Muster_AFirstBatchThatMovedOn_Passes()
    {
        var rig = NewRig(rules: LongPrep());
        rig.Director.Start();

        rig.Tick(5f);

        Assert.IsFalse(rig.Outcomes.Has("fail", LongDefeatChecks.MusterId), string.Join("\n", rig.Outcomes.Lines));
        Assert.IsTrue(rig.Outcomes.Has("pass", LongDefeatChecks.MusterId));
    }

    [TestMethod]
    public void Muster_FarSoldier_FailsTheCheck()
    {
        var rig = NewRig(rules: LongPrep());
        rig.Director.Start();
        rig.Field.Far = 1;

        rig.Tick(5f);

        Assert.IsTrue(rig.Outcomes.Has("fail", LongDefeatChecks.MusterId));
        Assert.IsTrue(rig.Log.Lines.Any(l => l.Contains("spawn level WRONG")));
    }

    [TestMethod]
    public void Shop_PackBackInFull_PassesUnderTheOfferId_AShortPackFails()
    {
        var rig = NewRig(rules: LongPrep());
        rig.Director.Start();
        rig.Session.TryBuy("militia");
        rig.Tick();

        Assert.IsTrue(rig.Outcomes.Lines.Any(l => l.StartsWith("pass " + LongDefeatChecks.ShopId + " militia ")), string.Join("\n", rig.Outcomes.Lines));

        rig.Field.ShortBy = 1;
        rig.Session.TryBuy("yeomen");
        rig.Tick();

        Assert.IsTrue(rig.Outcomes.Lines.Any(l => l.StartsWith("fail " + LongDefeatChecks.ShopId + " yeomen ")));
        Assert.IsTrue(rig.Log.Lines.Any(l => l.StartsWith("WARN") && l.Contains("9 of 10")));
    }

    [TestMethod]
    public void Horn_PaysTheFormula_AndTheCheckSeesIt()
    {
        var rig = NewRig(rules: LongPrep());
        rig.Director.Start();
        rig.Tick(45f);

        Assert.AreEqual(StartResult.Ok, rig.Session.TryStartWave());
        rig.Tick();

        var line = rig.Outcomes.Lines.Single(l => l.StartsWith("pass " + LongDefeatChecks.EarlyStartId + " w1 "));
        StringAssert.Contains(line, "paid=90");
    }

    // ---- Wave spawn and thread rules ----

    [TestMethod]
    public void Wave_FirstBatchSpawnsAtMostEightPerTick_ThenTheTacticRefreshes()
    {
        var rig = NewRig();
        rig.Director.Start();

        var perTick = new List<int>();
        for (var i = 0; i < 200 && !rig.Calls.Contains("Refresh"); i++)
        {
            var before = rig.Field.AttackersStanding;
            rig.Tick();
            perTick.Add(rig.Field.AttackersStanding - before);
        }

        Assert.IsTrue(rig.Calls.Contains("Refresh"));
        Assert.AreEqual(110, rig.Field.AttackersStanding);
        Assert.AreEqual(1, rig.Field.Count("Place:att:Infantry"));
        Assert.AreEqual(1, rig.Field.Count("Place:att:Ranged"));
        Assert.AreEqual(1, rig.Field.Count("End:att:Infantry"));
        Assert.AreEqual(1, rig.Field.Count("End:att:Ranged"));
        Assert.IsTrue(perTick.Max() <= RunRules.Default.SpawnsPerTick, "per tick: " + string.Join(",", perTick));
        Assert.IsTrue(perTick.Count(n => n > 0) >= 110 / RunRules.Default.SpawnsPerTick, "spawns were spread over several ticks: " + string.Join(",", perTick));
    }

    [TestMethod]
    public void Wave_TheTacticRefreshesOnlyAfterBothAttackerBatchesClose()
    {
        var rig = NewRig();
        rig.Director.Start();

        rig.PumpUntil(() => rig.Calls.Contains("Refresh"));

        var refresh = rig.Field.Index("Refresh");
        Assert.IsTrue(refresh > rig.Field.Index("End:att:Infantry"), string.Join(",", rig.Calls));
        Assert.IsTrue(refresh > rig.Field.Index("End:att:Ranged"), string.Join(",", rig.Calls));
        Assert.IsTrue(refresh > rig.Calls.FindLastIndex(c => c.StartsWith("Spawn:att")), "the last spawn comes first");
    }

    [TestMethod]
    public void Wave_SpawnLevelIsLoggedOncePerFormation_WithTheTotalsOfTheWholeBatch()
    {
        var rig = NewRig();
        rig.Director.Start();
        rig.Field.Far = 1;

        rig.PumpUntil(() => rig.Calls.Contains("Refresh"));

        var line = rig.Log.Lines.Single(l => l.Contains("spawn level WRONG: attackers Infantry"));
        var pieces = rig.Field.Count("Spawn:att:orc");
        Assert.IsTrue(pieces > 1, "the batch spans several ticks");
        var match = System.Text.RegularExpressions.Regex.Match(line, @"Infantry (\d+) of (\d+), far (\d+),");
        Assert.IsTrue(match.Success, line);
        Assert.IsTrue(int.Parse(match.Groups[1].Value) > RunRules.Default.SpawnsPerTick, "the total is the whole batch, not one tick's slice: " + line);
        Assert.AreEqual(match.Groups[1].Value, match.Groups[2].Value);
        Assert.AreEqual(pieces, int.Parse(match.Groups[3].Value), "far is summed over every piece");
    }

    [TestMethod]
    public void Wave_AFloorThatIsRight_LogsSpawnLevelOkForBothSides()
    {
        // tools/run_long_defeat.py reads these two literals back; its tests read this file for them.
        var rig = NewRig();
        rig.StartAndSpawnWave();

        Assert.IsTrue(rig.Log.Lines.Any(l => l.Contains("spawn level ok: defenders")), string.Join("\n", rig.Log.Lines));
        Assert.IsTrue(rig.Log.Lines.Any(l => l.Contains("spawn level ok: attackers")), string.Join("\n", rig.Log.Lines));
    }

    [TestMethod]
    public void Muster_LogsTheBatchNumberOutOfTheTotal()
    {
        var rig = NewRig(rules: LongPrep());
        rig.Director.Start();

        rig.Tick(5f);

        Assert.IsTrue(rig.Log.Lines.Any(l => l.Contains("[Muster] batch 1/2 Infantry")), string.Join("\n", rig.Log.Lines));
        Assert.IsTrue(rig.Log.Lines.Any(l => l.Contains("[Muster] batch 2/2 Infantry")));
    }

    [TestMethod]
    public void StateLine_CarriesDefenderFleeingAndHigh_AndTheEnginesAndKeepLinesFollow()
    {
        var rig = NewRig(rules: LongPrep());
        rig.Field.DefendersFleeing = 3;
        rig.Field.AttackersHigh = 5;
        rig.Field.Sample = new[] { new LdPoint(1f, 2f, 3f), new LdPoint(4f, 5f, 6f) };
        rig.Director.Start();

        rig.Pump(6);

        Assert.IsTrue(rig.Log.Lines.Any(l => l.Contains("[State]") && l.Contains("high 5") && l.Contains("fleeing 3")), string.Join("\n", rig.Log.Lines));
        Assert.IsTrue(rig.Log.Lines.Count(l => l.Contains("[LongDefeat][Engines]")) >= 2, "the 5 s tick repeats the engines line");
        Assert.IsTrue(rig.Log.Lines.Any(l => l.Contains("[LongDefeat][Keep]") && l.Contains("positions=(1.0,2.0,3.0);(4.0,5.0,6.0)")));
    }

    [TestMethod]
    public void Removal_FromABackgroundThread_ParksAndLandsBeforeTheNextTick()
    {
        var rig = NewRig();
        rig.StartAndSpawnWave();
        var before = rig.Session.GetView()!.Gold;

        Task.Run(() => rig.Director.OnAgentRemoved("A:orc", true)).Wait();

        Assert.AreEqual(before, rig.Session.GetView()!.Gold, "an off-thread removal must not touch the run");
        rig.Tick();
        Assert.AreEqual(before + 4, rig.Session.GetView()!.Gold);
    }

    [TestMethod]
    public void Removal_OnTheMainThread_LandsAtOnce()
    {
        var rig = NewRig();
        rig.StartAndSpawnWave();
        var before = rig.Session.GetView()!.Gold;

        rig.Director.OnAgentRemoved("A:skirm", true);

        Assert.AreEqual(before + 6, rig.Session.GetView()!.Gold);
    }

    [TestMethod]
    public void Clear_NeverFiresBeforeAParkedBountyLands()
    {
        var rig = NewRig(new RunOptions { StopAfterWaves = 1 });
        rig.StartAndSpawnWave();
        rig.KillAttackers(109);
        rig.Field.AttackersStanding = 0;

        Task.Run(() => rig.Director.OnAgentRemoved("A:orc", true)).Wait();
        rig.Tick();

        Assert.IsNotNull(rig.Director.Result);
        var report = JObject.Parse(rig.Director.ReportText);
        Assert.AreEqual(110, (int)report["waves"]![0]!["kills"]!, "the clear must count the kill that was parked");
    }

    [TestMethod]
    public void Removal_ThatIsNotAKillOrKnockOut_ChangesNothing()
    {
        var rig = NewRig();
        rig.StartAndSpawnWave();
        var before = rig.Session.GetView()!.Gold;

        rig.Director.OnAgentRemoved("A:orc", false);
        rig.Director.OnAgentRemoved("D:militia", false);
        rig.Director.OnAgentRemoved("P", false);

        Assert.AreEqual(before, rig.Session.GetView()!.Gold, "a routed or deleted attacker pays no bounty");
        rig.Director.Shutdown();
        var report = JObject.Parse(rig.Director.ReportText);
        Assert.AreEqual(0, (int)report["kills"]!);
        Assert.AreEqual(0, (int)report["playerDeaths"]!);
    }

    [TestMethod]
    public void UnknownRemovalAndFleeing_ChangeNothing()
    {
        var rig = NewRig();
        rig.StartAndSpawnWave();
        var before = rig.Session.GetView()!.Gold;

        rig.Director.OnAgentRemoved("horse", true);
        rig.Director.OnAgentFleeing("D:militia");

        Assert.AreEqual(before, rig.Session.GetView()!.Gold);
        rig.Director.OnAgentFleeing("A:orc");
        rig.Director.Shutdown();
        Assert.AreEqual(1, (int)JObject.Parse(rig.Director.ReportText)["fled"]!);
    }

    [TestMethod]
    public void Tick_NaNOrZeroDt_DoesNothing()
    {
        var rig = NewRig();
        rig.Director.Start();
        var calls = rig.Calls.Count;

        rig.Tick(float.NaN);
        rig.Tick(0f);
        rig.Tick(-1f);

        Assert.AreEqual(calls, rig.Calls.Count);
    }

    // ---- Run end ----

    [TestMethod]
    public void ARunThatClearsItsTarget_WritesTheRunEndChecksAndTheReport()
    {
        var rig = NewRig(new RunOptions { StopAfterWaves = 1 });
        rig.StartAndSpawnWave();
        rig.KillAttackers(110);

        rig.Tick();

        Assert.AreEqual(RunEndReason.TargetReached, rig.Director.Result!.Reason);
        Assert.IsTrue(rig.Outcomes.Has("pass", LongDefeatChecks.GoldId), string.Join("\n", rig.Outcomes.Lines));
        Assert.IsTrue(rig.Outcomes.Has("pass", LongDefeatChecks.NoRoutId));
        Assert.IsTrue(rig.Outcomes.Has("pass", LongDefeatChecks.EndId));
        Assert.IsTrue(rig.Outcomes.Lines.Any(l => l.StartsWith("pass " + LongDefeatChecks.WaveId + " w1 ")));
        var report = JObject.Parse(rig.Director.ReportText);
        Assert.IsTrue((bool)report["final"]!);
        Assert.AreEqual(1, (int)report["wavesCleared"]!);
        Assert.AreEqual(110, (int)report["kills"]!);
        rig.Tick();
        Assert.AreEqual(1, rig.Outcomes.Lines.Count(l => l.StartsWith("pass " + LongDefeatChecks.EndId)), "ended runs tick no more");
    }

    [TestMethod]
    public void NoRout_AFleeingAttacker_FailsTheRunEndCheck()
    {
        var rig = NewRig(new RunOptions { StopAfterWaves = 1 });
        rig.StartAndSpawnWave();
        rig.Director.OnAgentFleeing("A:orc");
        rig.KillAttackers(110);

        rig.Tick();

        Assert.IsTrue(rig.Outcomes.Has("fail", LongDefeatChecks.NoRoutId));
    }

    [TestMethod]
    public void NoRout_AttackersTheFieldReadsAsFleeing_FailTheCheckWithNoCallback()
    {
        // The engine's own flee flag raises no callback (Agent.cs:1645), so the peak the field reads is the only sign.
        var rig = NewRig(new RunOptions { StopAfterWaves = 1 });
        rig.StartAndSpawnWave();
        rig.Field.AttackersFleeing = 1;
        rig.Tick();
        rig.Field.AttackersFleeing = 0;
        rig.KillAttackers(110);

        rig.Tick();

        Assert.IsTrue(rig.Outcomes.Lines.Any(l => l.StartsWith("fail " + LongDefeatChecks.NoRoutId) && l.Contains("fleeing_peak=1")),
            string.Join("\n", rig.Outcomes.Lines));
    }

    // ---- The keep ----

    [TestMethod]
    public void Keep_ThirtyAttackersAgainstNone_FallsAfterTheHoldTime_WithTheReportAndDoneReason()
    {
        var rig = NewRig();
        rig.Field.AttackersAtKeep = 30;
        rig.Field.DefendersAtKeep = 0;
        rig.StartAndSpawnWave();

        rig.PumpUntil(() => rig.Director.Result != null, limit: 200);

        Assert.AreEqual(RunEndReason.KeepLost, rig.Director.Result!.Reason);
        Assert.AreEqual("KeepLost", (string?)JObject.Parse(rig.Director.ReportText)["reason"]);
        Assert.AreEqual("lost_keep", LongDefeatAutorunDone.ReasonFor(rig.Director.Result.Reason));
        Assert.IsTrue(rig.Session.LiveSeconds >= RunRules.Default.KeepHoldSeconds, "the bar fills over the hold time, not at once: " + rig.Session.LiveSeconds);
    }

    [TestMethod]
    public void Keep_TheSideCountsAreNotSwapped_DefendersHoldingItNeverLoseIt()
    {
        var rig = NewRig();
        rig.Field.AttackersAtKeep = 0;
        rig.Field.DefendersAtKeep = 30;
        rig.StartAndSpawnWave();

        rig.Pump(100);

        Assert.AreNotEqual(RunEndReason.KeepLost, rig.Director.Result?.Reason);
    }

    [TestMethod]
    public void Keep_IsReadOnlyEveryHalfSecond()
    {
        var rig = NewRig(rules: LongPrep());
        rig.Director.Start();
        var before = rig.Field.Count("ReadKeep");

        rig.Pump(20, 0.1f);

        var reads = rig.Field.Count("ReadKeep") - before;
        Assert.IsTrue(reads >= 3 && reads <= 5, "20 ticks of 0.1 s hold four half seconds, read " + reads);
    }

    [TestMethod]
    public void FewQuietStragglers_FallWithNoCharge_AndTheDefendersKeepTheirTactic()
    {
        var rig = NewRig();
        rig.StartAndSpawnWave();
        rig.KillAttackers(108);
        rig.Field.AttackersStanding = 2;

        rig.PumpUntil(() => rig.Calls.Any(c => c.StartsWith("Fell:")), dt: 5f);

        Assert.AreEqual(1, rig.Field.Count("Fell:2"));
        Assert.AreEqual(-1, rig.Field.Index("Charge"));
        rig.Tick();
        Assert.AreEqual(-1, rig.Field.Index("Restore"));
        Assert.IsTrue(rig.Outcomes.Lines.Any(l => l.StartsWith("pass " + LongDefeatChecks.WaveId + " w1 ")), string.Join("\n", rig.Outcomes.Lines));
    }

    [TestMethod]
    public void Stalemate_ChargesThenFellsTheStragglers_AndTheWaveClearsAsAStaleOne()
    {
        var rig = NewRig(rules: new RunRules { StragglerQuietSeconds = 3600f });
        rig.StartAndSpawnWave();
        rig.KillAttackers(105);
        rig.Field.AttackersStanding = 5;

        rig.PumpUntil(() => rig.Calls.Any(c => c.StartsWith("Fell:")), dt: 30f);

        Assert.IsTrue(rig.Field.Index("Charge") >= 0 && rig.Field.Index("Charge") < rig.Calls.FindIndex(c => c.StartsWith("Fell:")));
        Assert.AreEqual(1, rig.Field.Count("Fell:5"));
        rig.Tick();
        Assert.IsTrue(rig.Calls.Contains("Restore"), "the defenders get their own tactic back at the clear");
        Assert.IsTrue(rig.Outcomes.Lines.Any(l => l.StartsWith("pass " + LongDefeatChecks.StaleId + " w1 ")), string.Join("\n", rig.Outcomes.Lines));
        Assert.IsTrue(rig.Outcomes.Lines.Any(l => l.StartsWith("pass " + LongDefeatChecks.WaveId + " w1 ")));
    }

    [TestMethod]
    public void Stalemate_TheFelledStragglersComeBackAsRemovals_AndPayNoBounty()
    {
        var rig = NewRig();
        rig.StartAndSpawnWave();
        rig.KillAttackers(105);
        rig.Field.AttackersStanding = 5;
        rig.Field.AttackerRemoved = () => rig.Director.OnAgentRemoved("A:orc", true);
        var gold = rig.Session.GetView()!.Gold;

        rig.PumpUntil(() => rig.Calls.Any(c => c.StartsWith("Fell:")), dt: 30f);

        Assert.AreEqual(gold, rig.Session.GetView()!.Gold, "a felled soldier is no kill: the engine's removal for it pays nothing");
        rig.Tick();
        Assert.AreEqual(105, (int)JObject.Parse(ReportOf(rig))["kills"]!);
    }

    private static string ReportOf(Rig rig)
    {
        rig.Director.Shutdown();
        return rig.Director.ReportText;
    }

    [TestMethod]
    public void Stalemate_AShortfallInTheFelling_IsWarnedAbout()
    {
        var rig = NewRig();
        rig.StartAndSpawnWave();
        rig.KillAttackers(105);
        rig.Field.AttackersStanding = 5;
        rig.Field.FellResult = 3;
        rig.Field.AttackerRemoved = () => rig.Director.OnAgentRemoved("A:orc", true);

        rig.PumpUntil(() => rig.Calls.Any(c => c.StartsWith("Fell:")), dt: 30f);

        Assert.IsTrue(rig.Log.Lines.Any(l => l.StartsWith("WARN") && l.Contains("asked to fell 5 attackers, 3 fell")), string.Join("\n", rig.Log.Lines));
    }

    [TestMethod]
    public void Stalemate_ChargeThatNeverClears_FailsStaleWhenTheRunEndsAsAStalemate()
    {
        var rig = NewRig();
        rig.StartAndSpawnWave();
        rig.Field.AttackersStanding = 30;

        rig.PumpUntil(() => rig.Director.Result != null, dt: 30f, limit: 60);

        Assert.AreEqual(RunEndReason.Stalemate, rig.Director.Result!.Reason);
        Assert.IsTrue(rig.Outcomes.Lines.Any(l => l.StartsWith("fail " + LongDefeatChecks.StaleId + " w1 ")));
    }

    [TestMethod]
    public void Shutdown_AbandonsTheRun_ClearsTheSessionAndKeepsTheResult()
    {
        var rig = NewRig();
        rig.StartAndSpawnWave();

        rig.Director.Shutdown();
        rig.Director.Shutdown();

        Assert.AreEqual(RunEndReason.Abandoned, rig.Session.Result!.Reason);
        Assert.IsFalse(rig.Session.IsActive);
        Assert.AreEqual(1, rig.Field.Count("Clear"));
        Assert.IsTrue(rig.Outcomes.Has("pass", LongDefeatChecks.EndId));
        Assert.AreEqual("Abandoned", (string?)JObject.Parse(rig.Director.ReportText)["reason"]);
    }

    [TestMethod]
    public void Shutdown_BeforeStart_ClearsWithoutAResult()
    {
        var rig = NewRig();

        rig.Director.Shutdown();

        Assert.IsNull(rig.Session.Result);
        Assert.AreEqual("", rig.Director.ReportText);
        Assert.AreEqual(1, rig.Field.Count("Clear"));
    }

    [TestMethod]
    public void Shutdown_LandsParkedRemovalsFirst()
    {
        var rig = NewRig();
        rig.StartAndSpawnWave();

        Task.Run(() => rig.Director.OnAgentRemoved("A:orc", true)).Wait();
        rig.Director.Shutdown();

        Assert.AreEqual(4, (int)JObject.Parse(rig.Director.ReportText)["gold"]!["bounty"]!);
    }

    // ---- The player ----

    [TestMethod]
    public void Autoplay_AdoptsMainWithTheAiOn_AndEachNewBodyAgain_AndTheBotBuys()
    {
        var rig = NewRig(new RunOptions { Autoplay = true }, LongPrep());
        rig.Director.Start();

        Assert.AreEqual(1, rig.Field.Count("Adopt:True"));
        rig.Tick();
        Assert.AreEqual(1, rig.Field.Count("Adopt:True"), "the same body is adopted once");
        rig.Field.MainBody = 2;
        rig.Tick();
        Assert.AreEqual(2, rig.Field.Count("Adopt:True"));
        Assert.IsTrue(rig.Session.GetView()!.Gold < 600, "the bot spent gold in Preparation");
    }

    [TestMethod]
    public void Autoplay_TheBotSoundsTheHornAfterHalfAMinuteOfAPhase_AndTheClockRestartsWithTheNextPhase()
    {
        var rules = RunRules.Default with { PreparationSeconds = 90f, IntermissionSeconds = 90f };
        var rig = NewRig(new RunOptions { Autoplay = true }, rules, gold: 0);
        rig.Director.Start();

        rig.Tick(20f);
        rig.Tick(1f);
        Assert.IsFalse(rig.Outcomes.Lines.Any(l => l.Contains(LongDefeatChecks.EarlyStartId)), "20 s into the phase is too early");
        rig.Tick(20f);
        rig.Tick(1f);
        Assert.IsTrue(rig.Outcomes.Lines.Any(l => l.StartsWith("pass " + LongDefeatChecks.EarlyStartId + " w1 ")), string.Join("\n", rig.Outcomes.Lines));

        rig.PumpUntil(() => rig.Calls.Contains("Refresh"));
        rig.KillAttackers(110);
        rig.Tick();
        rig.Pump(5);
        Assert.IsFalse(rig.Outcomes.Lines.Any(l => l.Contains(" w2 ") && l.Contains(LongDefeatChecks.EarlyStartId)),
            "the intermission starts its own clock: the 40 s of the first phase must not count");
        rig.Tick(30f);
        rig.Tick(1f);
        Assert.IsTrue(rig.Outcomes.Lines.Any(l => l.StartsWith("pass " + LongDefeatChecks.EarlyStartId + " w2 ")), string.Join("\n", rig.Outcomes.Lines));
    }

    [TestMethod]
    public void HumanRun_AdoptsMainWithTheAiOff_AndTheBotDoesNotBuy()
    {
        var rig = NewRig(rules: LongPrep());
        rig.Director.Start();
        rig.Pump(3);

        Assert.AreEqual(1, rig.Field.Count("Adopt:False"));
        Assert.AreEqual(600, rig.Session.GetView()!.Gold);
    }

    [TestMethod]
    public void PlayerDeath_RespawnsAtTheIntermission_AndTheCheckSeesTheAdoption()
    {
        var rig = NewRig();
        rig.StartAndSpawnWave();
        rig.Director.OnAgentRemoved("P", true);
        rig.Field.MainBody = 0;
        rig.Tick();
        Assert.AreEqual(0, rig.Field.Count("SpawnPlayer:True"), "no respawn during the wave");

        rig.KillAttackers(110);
        rig.Tick();
        Assert.AreEqual(1, rig.Field.Count("SpawnPlayer:True"));

        rig.Field.MainBody = 2;
        rig.Tick();

        Assert.IsTrue(rig.Outcomes.Lines.Any(l => l.StartsWith("pass " + LongDefeatChecks.PlayerDeathId + " d1 ") && l.Contains("via=respawn")),
            string.Join("\n", rig.Outcomes.Lines));
        Assert.AreEqual(1, rig.Session.GetView()!.WavesCleared);
        Assert.IsTrue(rig.Log.Lines.Any(l => l.Contains("[LongDefeat][Player] respawned main=fake")), string.Join("\n", rig.Log.Lines));
        Assert.IsTrue(rig.Log.Lines.Any(l => l.Contains("[LongDefeat][Player] wave 1 main=fake")));
    }

    [TestMethod]
    public void PlayerDeath_ATakeoverBeforeTheIntermission_NeedsNoRespawn()
    {
        var rig = NewRig();
        rig.StartAndSpawnWave();
        rig.Director.OnAgentRemoved("P", true);
        rig.Field.MainBody = 0;
        rig.Tick();
        rig.Field.MainBody = 3;
        rig.Tick();

        rig.KillAttackers(110);
        rig.Tick();

        Assert.AreEqual(0, rig.Field.Count("SpawnPlayer:True"));
        Assert.IsTrue(rig.Outcomes.Lines.Any(l => l.StartsWith("pass " + LongDefeatChecks.PlayerDeathId + " d1 ") && l.Contains("via=takeover")));
    }

    [TestMethod]
    public void PlayerDeath_ARespawnTheEngineRefuses_FailsTheCheck()
    {
        var rig = NewRig();
        rig.StartAndSpawnWave();
        rig.Director.OnAgentRemoved("P", true);
        rig.Field.MainBody = 0;
        rig.Field.SpawnPlayerOk = false;
        rig.Tick();

        rig.KillAttackers(110);
        rig.Tick();

        Assert.IsTrue(rig.Outcomes.Lines.Any(l => l.StartsWith("fail " + LongDefeatChecks.PlayerDeathId + " d1 ")));
    }

    [TestMethod]
    public void PlayerDeath_AtTheClearThatEndsTheRun_NeverRespawns()
    {
        var rig = NewRig(new RunOptions { StopAfterWaves = 1 });
        rig.StartAndSpawnWave();
        rig.Director.OnAgentRemoved("P", true);
        rig.Field.MainBody = 0;
        rig.Tick();

        rig.KillAttackers(110);
        rig.Tick();

        Assert.AreEqual(RunEndReason.TargetReached, rig.Director.Result!.Reason);
        Assert.AreEqual(0, rig.Field.Count("SpawnPlayer:True"), "a finished run has no intermission to respawn into");
    }

    [TestMethod]
    public void PlayerDeath_AliveAtTheIntermission_NeverRespawns()
    {
        var rig = NewRig();
        rig.StartAndSpawnWave();
        rig.KillAttackers(110);

        rig.Tick();

        Assert.AreEqual(0, rig.Field.Count("SpawnPlayer:True"));
    }

    // ---- Records: the report file and the best run ----

    private static string TempDir() => System.IO.Path.Combine(System.IO.Path.GetTempPath(), "taom_ld_dir_" + Guid.NewGuid().ToString("N"));

    private static (LongDefeatRunRecords Records, string Dir) Records()
    {
        var dir = TempDir();
        return (new LongDefeatRunRecords(new LongDefeatBestRunStore(dir), LongDefeatReportFile.For(System.IO.Path.Combine(dir, "taom_debug_2026-11-04_10-11-12.log"))), dir);
    }

    [TestMethod]
    public void AClear_RestsTheGarrison_AndTheLineNamesTheMeanHealthBeforeAndAfter()
    {
        var rig = NewRig(rules: new RunRules { RestShare = 0.5f });
        rig.Field.RestResult = new LdRest(150, 0.6f, 0.8f);
        rig.StartAndSpawnWave();
        Assert.IsFalse(rig.Calls.Contains("Rest:0.5"), "no rest before the clear");

        rig.KillAttackers(110);
        rig.Tick();

        CollectionAssert.Contains(rig.Calls, "Rest:0.5");
        Assert.IsTrue(rig.Log.Lines.Any(l => l.Contains("[Wave] 1 rest: defenders 150, mean health 60% to 80%")), string.Join("\n", rig.Log.Lines));
    }

    [TestMethod]
    public void AClear_WithAZeroShare_TouchesNobody()
    {
        var rig = NewRig(rules: new RunRules { RestShare = 0f });
        rig.StartAndSpawnWave();

        rig.KillAttackers(110);
        rig.Tick();

        Assert.IsFalse(rig.Calls.Any(c => c.StartsWith("Rest:")));
        Assert.IsFalse(rig.Log.Lines.Any(l => l.Contains(" rest: ")));
    }

    [TestMethod]
    public void TheReport_CarriesTheRunsTimes_AndThePlansProvenance()
    {
        var before = DateTime.UtcNow;
        var rig = NewRig(new RunOptions { StopAfterWaves = 1 }, provenance: new RunProvenance("b+abc", "hash16", new[] { "TAOM v2.0.36" }, true));

        ClearOneWave(rig);

        var report = Newtonsoft.Json.Linq.JObject.Parse(rig.Director.ReportText);
        Assert.AreEqual("b+abc", (string?)report["build"]);
        Assert.AreEqual("hash16", (string?)report["balance"]);
        Assert.AreEqual("TAOM v2.0.36", (string?)report["mods"]![0]);
        Assert.IsTrue((bool)report["cheatMode"]!);
        Assert.AreEqual(1, (int)report["targetWaves"]!);
        var started = DateTime.Parse(report["startedUtc"]!.ToString(Newtonsoft.Json.Formatting.None).Trim('"'), null, System.Globalization.DateTimeStyles.RoundtripKind);
        var ended = DateTime.Parse(report["endedUtc"]!.ToString(Newtonsoft.Json.Formatting.None).Trim('"'), null, System.Globalization.DateTimeStyles.RoundtripKind);
        Assert.AreEqual(DateTimeKind.Utc, started.Kind);
        Assert.IsTrue(before <= started && started <= ended && ended <= DateTime.UtcNow);
        Assert.AreEqual(1, (int)report["schema"]!);
    }

    private static void ClearOneWave(Rig rig)
    {
        rig.StartAndSpawnWave();
        rig.KillAttackers(110);
        rig.Tick();
        Assert.IsNotNull(rig.Director.Result);
    }

    [TestMethod]
    public void Finish_WritesTheReportFile_AndTheEndCheckNamesIt()
    {
        var (records, dir) = Records();
        try
        {
            var rig = NewRig(new RunOptions { StopAfterWaves = 1 }, records: records);

            ClearOneWave(rig);

            var path = records.Report!.PathFor("m1");
            Assert.AreEqual(rig.Director.ReportText, System.IO.File.ReadAllText(path));
            Assert.IsTrue(rig.Outcomes.Has("pass", LongDefeatChecks.EndId));
        }
        finally
        {
            System.IO.Directory.Delete(dir, true);
        }
    }

    [TestMethod]
    public void Finish_TheSessionKnowsTheReportPath_ForTheUnattendedDriversDoneFile()
    {
        var (records, dir) = Records();
        try
        {
            var rig = NewRig(new RunOptions { StopAfterWaves = 1 }, records: records);

            ClearOneWave(rig);

            Assert.AreEqual(records.Report!.PathFor("m1"), rig.Session.ReportPath);
        }
        finally
        {
            System.IO.Directory.Delete(dir, true);
        }
    }

    [TestMethod]
    public void Tick_AddsItsDtToTheSessionsLiveSeconds_AndOnlyWhileTheRunIsLive()
    {
        var rig = NewRig();
        rig.Director.Start();

        rig.Tick(0.5f);
        rig.Tick(0.25f);
        rig.Tick(float.NaN);
        rig.Tick(-3f);

        Assert.AreEqual(0.75f, rig.Session.LiveSeconds, 0.0001f);
        rig.Director.Shutdown();
        Assert.AreEqual(0.75f, rig.Session.LiveSeconds, 0.0001f, "the seconds outlive the mission");
    }

    [TestMethod]
    public void Finish_WhenTheReportFileCannotBeWritten_FailsTheEndCheckButStillEnds()
    {
        var dir = TempDir();
        System.IO.Directory.CreateDirectory(dir);
        var blocker = System.IO.Path.Combine(dir, "blocker");
        System.IO.File.WriteAllText(blocker, "x");
        try
        {
            var records = new LongDefeatRunRecords(new LongDefeatBestRunStore(dir),
                LongDefeatReportFile.For(System.IO.Path.Combine(blocker, "taom_debug_2026-11-04_10-11-12.log")));
            var rig = NewRig(new RunOptions { StopAfterWaves = 1 }, records: records);

            ClearOneWave(rig);

            Assert.IsTrue(rig.Outcomes.Has("fail", LongDefeatChecks.EndId), string.Join("\n", rig.Outcomes.Lines));
            Assert.AreEqual(RunEndReason.TargetReached, rig.Director.Result!.Reason);
        }
        finally
        {
            System.IO.Directory.Delete(dir, true);
        }
    }

    [TestMethod]
    public void Finish_SavesABetterRun_ReadsItBack_AndPassesBestRun()
    {
        var (records, dir) = Records();
        try
        {
            var rig = NewRig(new RunOptions { StopAfterWaves = 1 }, records: records);

            ClearOneWave(rig);

            Assert.AreEqual(new BestRun(1, 110), records.Best.Load("helms"));
            Assert.AreEqual(new BestRun(1, 110), rig.Session.Best, "the end panel reads the saved best");
            Assert.IsTrue(rig.Outcomes.Has("pass", LongDefeatChecks.BestRunId), string.Join("\n", rig.Outcomes.Lines));
        }
        finally
        {
            System.IO.Directory.Delete(dir, true);
        }
    }

    [TestMethod]
    public void Finish_ABetterRunThatCannotBeSaved_StillShowsTheStoredBest()
    {
        var (records, dir) = Records();
        try
        {
            records.Best.TrySave("helms", new BestRun(1, 50), out _);
            System.IO.Directory.CreateDirectory(records.Best.FilePath + ".tmp");
            var rig = NewRig(new RunOptions { StopAfterWaves = 1 }, records: records);

            ClearOneWave(rig);

            Assert.AreEqual(new BestRun(1, 50), records.Best.Load("helms"), "the write failed");
            Assert.AreEqual(new BestRun(1, 50), rig.Session.Best, "the panel shows the stored best, not 'No best run yet'");
        }
        finally
        {
            System.IO.Directory.Delete(dir, true);
        }
    }

    [TestMethod]
    public void Finish_AWorseRun_LeavesTheStoredBestAlone_AndSkipsTheCheck()
    {
        var (records, dir) = Records();
        try
        {
            records.Best.TrySave("helms", new BestRun(5, 900), out _);
            var rig = NewRig(new RunOptions { StopAfterWaves = 1 }, records: records);

            ClearOneWave(rig);

            Assert.AreEqual(new BestRun(5, 900), records.Best.Load("helms"));
            Assert.AreEqual(new BestRun(5, 900), rig.Session.Best, "the panel shows the stored best, not the worse run");
            Assert.IsTrue(rig.Outcomes.Has("skip", LongDefeatChecks.BestRunId));
            Assert.IsFalse(rig.Outcomes.Has("fail", LongDefeatChecks.BestRunId));
        }
        finally
        {
            System.IO.Directory.Delete(dir, true);
        }
    }

    [TestMethod]
    public void Finish_ARunThatClearedNoWave_StillSavesItAndPassesBestRun()
    {
        var (records, dir) = Records();
        try
        {
            var rig = NewRig(records: records);
            rig.StartAndSpawnWave();

            rig.Director.Shutdown();

            Assert.AreEqual(0, records.Best.Load("helms")!.Waves);
            Assert.IsTrue(rig.Outcomes.Has("pass", LongDefeatChecks.BestRunId), string.Join("\n", rig.Outcomes.Lines));
        }
        finally
        {
            if (System.IO.Directory.Exists(dir))
                System.IO.Directory.Delete(dir, true);
        }
    }

    [TestMethod]
    public void Finish_WhenTheBestRunCannotBeSaved_FailsBestRun()
    {
        var dir = TempDir();
        System.IO.Directory.CreateDirectory(dir);
        var blocker = System.IO.Path.Combine(dir, "blocker");
        System.IO.File.WriteAllText(blocker, "x");
        try
        {
            var records = new LongDefeatRunRecords(new LongDefeatBestRunStore(System.IO.Path.Combine(blocker, "in")), null);
            var rig = NewRig(new RunOptions { StopAfterWaves = 1 }, records: records);

            ClearOneWave(rig);

            Assert.IsTrue(rig.Outcomes.Has("fail", LongDefeatChecks.BestRunId));
            Assert.AreEqual(RunEndReason.TargetReached, rig.Director.Result!.Reason);
        }
        finally
        {
            System.IO.Directory.Delete(dir, true);
        }
    }

    [TestMethod]
    public void Finish_WithNoRecords_WritesNoBestRunLine()
    {
        var rig = NewRig(new RunOptions { StopAfterWaves = 1 });

        ClearOneWave(rig);

        Assert.IsFalse(rig.Outcomes.Lines.Any(l => l.Contains(LongDefeatChecks.BestRunId)));
    }
}
