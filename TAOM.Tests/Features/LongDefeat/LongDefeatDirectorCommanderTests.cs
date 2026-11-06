using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Newtonsoft.Json.Linq;
using TAOM.Features.AdvancedCombat;
using TAOM.Features.LongDefeat;
using TAOM.Features.LongDefeat.Diagnostics;
using TAOM.Features.LongDefeat.Domain;
using TAOM.Tests.Features.LoadTimeStamps;
using TAOM.Tests.Features.LongDefeat.Domain;

namespace TAOM.Tests.Features.LongDefeat;

/// <summary>
/// The commander at the director (phase2-plan.md sections 4 and 6): he is told to the run once the field has spawned him, a war cry refreshes the attackers after the table is published, a
/// removal is booked by its role, and the <c>commander</c> and <c>summon</c> checks are written when his wave clears.
/// </summary>
[TestClass]
public class LongDefeatDirectorCommanderTests
{
    private sealed class Rig
    {
        public FakeLongDefeatField Field { get; } = new();
        public FakeLongDefeatEffects Effects { get; }
        public LongDefeatSession Session { get; } = new();
        public RecordingLogger Log { get; } = new();
        public RecordingOutcomeLog Outcomes { get; } = new();
        public LongDefeatDirector Director { get; }

        public Rig(int startWave, RunRules? rules = null, int roster = 20, CommanderPlan? commanders = null)
        {
            Effects = new FakeLongDefeatEffects(Field.Calls);
            Effects.CommanderFor = () => new LdScaleSample("lugdush", 200f, 100f, 0);
            var waves = new WavePlan(
                new[] { new WaveSpec(roster, new[] { new WaveTroop("orc", FormationSlot.Infantry, 3), new WaveTroop("skirm", FormationSlot.Ranged, 1) }) },
                new WaveScaling(0, 300), null, commanders ?? CommanderPlanTests.Plan());
            var plan = new RunPlan(
                "helms",
                new[] { new GarrisonRow("fow_militia", FormationSlot.Infantry, 30) },
                waves,
                new ShopPlan(600, new[] { new ShopOffer("militia", "Militia", "fow_militia", FormationSlot.Infantry, 12, 180) }),
                rules ?? RunRules.Default with { PreparationSeconds = 1f, IntermissionSeconds = 1f },
                new RunOptions { StartWave = startWave, Seed = 1UL })
            {
                Bounties = new Dictionary<string, int> { ["orc"] = 4, ["skirm"] = 6, ["urukinf"] = 20, ["urukxbow"] = 25 },
            };
            Director = new LongDefeatDirector(plan, Field, Session, Log, Outcomes, "m1", null, Effects);
        }

        public void StartAndSpawnWave()
        {
            Assert.IsTrue(Director.Start());
            PumpUntil(() => Field.Calls.Contains("Refresh"));
        }

        public void PumpUntil(Func<bool> done, int limit = 200, float dt = 1f, bool empty = false)
        {
            for (var i = 0; i < limit && !done(); i++)
            {
                if (empty)
                    Field.AttackersStanding = 0;
                Director.Tick(dt);
            }

            Assert.IsTrue(done(), "the condition never came true");
        }

        public bool CommanderSpawned => Field.Spawned.Any(s => s.Attackers && s.Batch.Role == AttackerRole.Commander);

        public CommanderView? View => Session.GetView()?.Commander;

        /// <summary>The report the run writes when it ends: abandoned here, with the commander's wave in its rows if it cleared.</summary>
        public JObject Report()
        {
            Director.Shutdown();
            return JObject.Parse(Director.ReportText);
        }
    }

    [TestInitialize]
    public void Init() => MissionThreadGuard.ResetForTests();

    [TestCleanup]
    public void Clean() => MissionThreadGuard.ResetForTests();

    [TestMethod]
    public void TheCommander_IsSpawnedAsOneSoldierAfterTheFirstBatch_ToldUpToTheRun_AndReadBack()
    {
        var rig = new Rig(startWave: 4);
        rig.StartAndSpawnWave();

        rig.PumpUntil(() => rig.CommanderSpawned);

        var batch = rig.Field.Spawned.Single(s => s.Batch.Role == AttackerRole.Commander);
        Assert.AreEqual(("lugdush", 1, 2f), (batch.Batch.TroopId, batch.Batch.Count, batch.Batch.HealthFactor));
        Assert.IsTrue(rig.View!.Up, "the run was told he is up");
        Assert.IsTrue(rig.Field.Calls.Contains("ReadCommander"));
        Assert.IsTrue(rig.Log.Lines.Any(l => l.Contains("[Commander] w4 spawned lugdush rank=captain steps=0 health_x=2.00 read_x=2.00")), string.Join("\n", rig.Log.Lines.Where(l => l.Contains("Commander"))));
    }

    [TestMethod]
    public void ACommanderTheFieldCouldNotSpawn_IsGone_TheWaveGoesOn_AndTheCheckFails()
    {
        var rig = new Rig(startWave: 4);
        rig.Field.Unspawnable = "lugdush";
        rig.StartAndSpawnWave();
        rig.PumpUntil(() => rig.CommanderSpawned);

        rig.PumpUntil(() => rig.Outcomes.Has("fail", LongDefeatChecks.CommanderId) || rig.Outcomes.Has("pass", LongDefeatChecks.CommanderId), empty: true, limit: 300);

        Assert.IsTrue(rig.Log.Lines.Any(l => l.StartsWith("WARN") && l.Contains("lugdush could not be spawned")));
        Assert.IsTrue(rig.Outcomes.Has("fail", LongDefeatChecks.CommanderId), "a commander who never spawned is a defect, not a pass");
        StringAssert.Contains(string.Join("\n", rig.Outcomes.Of(LongDefeatChecks.CommanderId)), "outcome=left");
    }

    [TestMethod]
    public void AWarCry_PublishesTheTable_ThenRefreshesTheAttackers_AndAgainWhenItEnds()
    {
        var rig = new Rig(startWave: 4);
        rig.StartAndSpawnWave();
        rig.PumpUntil(() => rig.CommanderSpawned);

        rig.PumpUntil(() => rig.Effects.AttackerRefreshes >= 2, limit: 60);

        var calls = rig.Field.Calls;
        var cry = rig.Effects.Published.FindIndex(t => t.Attackers?.SwingSpeedPercent == 15f);
        Assert.IsTrue(cry >= 0, "the cry's table was published");
        var firstRefresh = calls.IndexOf("RefreshAttackers");
        Assert.IsTrue(firstRefresh >= 0, "the attackers were refreshed");
        Assert.IsTrue(calls.Take(firstRefresh).Count(c => c.StartsWith("Publish:", StringComparison.Ordinal)) > cry, "the cry's table was published before the first refresh read it");
        Assert.IsNull(rig.Effects.Published.Last().Attackers, "the last table is the wave's own: the cry is over");
        Assert.IsTrue(rig.Log.Lines.Any(l => l.Contains("[Commander] refresh attackers n=9")));
    }

    [TestMethod]
    public void ACommanderKilled_IsBookedAsOne_PaysHisRank_AndTheCheckPassesAtTheClear()
    {
        var rig = new Rig(startWave: 4);
        rig.StartAndSpawnWave();
        rig.PumpUntil(() => rig.CommanderSpawned);

        rig.Director.OnAgentRemoved("C:lugdush", true);
        rig.PumpUntil(() => rig.Outcomes.Has("pass", LongDefeatChecks.WaveId) || rig.Outcomes.Has("fail", LongDefeatChecks.WaveId), empty: true, limit: 300);

        var line = rig.Outcomes.Of(LongDefeatChecks.CommanderId).Single();
        StringAssert.StartsWith(line, "w4 w=4 troop=lugdush rank=captain steps=0 health_x=2.00 expected_x=2.00 outcome=killed");
        Assert.IsTrue(rig.Outcomes.Has("pass", LongDefeatChecks.CommanderId), line);
        Assert.AreEqual(150, (int)rig.Report()["gold"]!["commander"]!);
    }

    [TestMethod]
    public void TheReport_CarriesTheCommanderAndTheRosterCountsApart()
    {
        var rig = new Rig(startWave: 4, roster: 20);
        rig.StartAndSpawnWave();
        rig.PumpUntil(() => rig.CommanderSpawned);
        rig.Director.OnAgentRemoved("C:lugdush", true);
        rig.PumpUntil(() => rig.Outcomes.Has("pass", LongDefeatChecks.WaveId) || rig.Outcomes.Has("fail", LongDefeatChecks.WaveId), empty: true, limit: 300);

        var report = rig.Report();

        var wave = (JObject)report["waves"]!.Single();
        Assert.AreEqual("killed", (string?)wave["commander"]!["outcome"]);
        Assert.AreEqual(150, (int)report["gold"]!["commander"]!);
        Assert.AreEqual(0, (int)report["kills"]!, "the commander is no roster kill");
        Assert.AreEqual((long)report["gold"]!["earned"]!, (long)report["gold"]!["bounty"]! + (long)report["gold"]!["waveBonus"]! + (long)report["gold"]!["earlyStart"]!
            + (long)report["gold"]!["commander"]! + (long)report["gold"]!["boon"]!);
    }

    [TestMethod]
    public void ACommanderRemovedWithNoLoss_IsGoneAliveAndStopsHisClocks()
    {
        var rig = new Rig(startWave: 8);
        rig.StartAndSpawnWave();
        rig.PumpUntil(() => rig.CommanderSpawned);

        rig.Director.OnAgentRemoved("C:ugluk", false);
        rig.Director.Tick(1f);

        Assert.AreEqual(CommanderOutcome.Left, rig.View!.Outcome);
        rig.Director.Tick(60f);
        Assert.IsFalse(rig.Field.Spawned.Any(s => s.Batch.Role == AttackerRole.Summoned), "no summon after he left");
    }

    [TestMethod]
    public void ASummon_SpawnsItsSoldiers_AndWritesItsCheckAtTheClear()
    {
        var rig = new Rig(startWave: 8);
        rig.StartAndSpawnWave();
        rig.PumpUntil(() => rig.Field.Spawned.Count(s => s.Batch.Role == AttackerRole.Summoned) >= 2, limit: 80);

        rig.PumpUntil(() => rig.Outcomes.Has("pass", LongDefeatChecks.WaveId) || rig.Outcomes.Has("fail", LongDefeatChecks.WaveId), empty: true, limit: 300);

        Assert.AreEqual(20, rig.Field.Spawned.Where(s => s.Batch.Role == AttackerRole.Summoned).Sum(s => s.Batch.Count));
        var line = rig.Outcomes.Of(LongDefeatChecks.SummonId).Single();
        StringAssert.StartsWith(line, "w8s1 w=8 s=1 queued=20 expected=20 spawned=20");
        Assert.IsTrue(rig.Outcomes.Has("pass", LongDefeatChecks.SummonId), line);
        Assert.IsTrue(rig.Log.Lines.Any(l => l.Contains("[Commander] w8 summon use=1 count=20")));
    }

    [TestMethod]
    public void ARankWithTwoSummons_JudgesEachAgainstItsOwnCount()
    {
        var small = CommanderPlanTests.Summon(first: 5f, cooldown: 500f, count: 5);
        var large = CommanderPlanTests.Summon(first: 8f, cooldown: 500f, count: 8) with { Id = "summon_more" };
        var abilities = new Dictionary<string, CommanderAbility> { [small.Id] = small, [large.Id] = large };
        var ranks = new[] { new CommanderRank("captain", "{=TAOM_LD_Rank_Captain}Captain", "lugdush", 150, 2f, new[] { small.Id, large.Id }) };
        var rig = new Rig(startWave: 4, commanders: new CommanderPlan(4, ranks, abilities, new CommanderBeyond(0.25f, 0.9f, 5, 10)));
        rig.StartAndSpawnWave();
        rig.PumpUntil(() => rig.Field.Spawned.Where(s => s.Batch.Role == AttackerRole.Summoned).Sum(s => s.Batch.Count) >= 13, limit: 120);

        rig.PumpUntil(() => rig.Outcomes.Has("pass", LongDefeatChecks.WaveId) || rig.Outcomes.Has("fail", LongDefeatChecks.WaveId), empty: true, limit: 300);

        var lines = rig.Outcomes.Of(LongDefeatChecks.SummonId);
        Assert.AreEqual(2, lines.Count, string.Join("\n", lines));
        Assert.IsFalse(rig.Outcomes.Has("fail", LongDefeatChecks.SummonId), string.Join("\n", lines));
        Assert.AreEqual(2, rig.Outcomes.Lines.Count(l => l.StartsWith("pass " + LongDefeatChecks.SummonId + " ")), string.Join("\n", lines));
    }

    [TestMethod]
    public void ARunThatEndsBeforeASummonsSoldiersCame_WritesASkipForIt()
    {
        var rules = RunRules.Default with { PreparationSeconds = 1f, LiveCap = 101 };
        var rig = new Rig(startWave: 8, rules: rules);
        rig.StartAndSpawnWave();
        rig.PumpUntil(() => rig.CommanderSpawned);
        rig.Field.AttackersStanding = 101;
        rig.PumpUntil(() => rig.Log.Lines.Any(l => l.Contains("summon use=1")), limit: 60);

        rig.Director.Shutdown();

        Assert.IsTrue(rig.Outcomes.Has("skip", LongDefeatChecks.SummonId), string.Join("\n", rig.Outcomes.Lines));
        Assert.IsFalse(rig.Outcomes.Has("fail", LongDefeatChecks.SummonId));
    }

    [TestMethod]
    public void ASummonedKill_IsNotARosterKill_AndAFelledOneIsNotACountedKillEither()
    {
        var rig = new Rig(startWave: 8, roster: 20);
        rig.StartAndSpawnWave();
        rig.PumpUntil(() => rig.Field.Spawned.Count(s => s.Batch.Role == AttackerRole.Summoned) >= 1, limit: 80);

        rig.Director.OnAgentRemoved("S:urukinf", true);
        rig.Director.OnAgentRemoved("S:urukinf!", true);
        rig.Director.OnAgentRemoved("A:orc", true);
        rig.Director.OnAgentRemoved("A:orc!", true);

        var report = rig.Report();
        Assert.AreEqual(1, (int)report["kills"]!, "one roster kill; the summons and the felled soldier are not counted");
    }

    [TestMethod]
    public void WhileHeStands_EveryStateWindowLogsWhereHeIsAndWhatHisFormationDoes()
    {
        var rig = new Rig(startWave: 4);
        rig.StartAndSpawnWave();
        rig.PumpUntil(() => rig.CommanderSpawned);

        rig.Director.Tick(6f);

        Assert.IsTrue(rig.Log.Lines.Any(l => l.Contains("[LongDefeat][Commander] w4 troop=fake")), string.Join("\n", rig.Log.Lines.Where(l => l.Contains("Commander"))));
    }

    [TestMethod]
    public void AWaveWithNoCommander_WritesNoCommanderLineAndNoCheck()
    {
        var rig = new Rig(startWave: 3);
        rig.StartAndSpawnWave();

        rig.PumpUntil(() => rig.Outcomes.Has("pass", LongDefeatChecks.WaveId) || rig.Outcomes.Has("fail", LongDefeatChecks.WaveId), empty: true, limit: 300);

        Assert.AreEqual(0, rig.Outcomes.Of(LongDefeatChecks.CommanderId).Count);
        Assert.AreEqual(0, rig.Outcomes.Of(LongDefeatChecks.SummonId).Count);
        Assert.IsFalse(rig.Field.Spawned.Any(s => s.Batch.Role != AttackerRole.Roster));
    }
}
