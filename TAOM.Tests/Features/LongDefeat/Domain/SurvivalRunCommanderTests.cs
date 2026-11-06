using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.LongDefeat.Domain;

namespace TAOM.Tests.Features.LongDefeat.Domain;

/// <summary>
/// The commander and his summons inside a run (phase2-plan.md section 2): the commander enters through the spawn queue, his abilities fire on their own clocks, a summon and a war cry
/// act as the plan says, every removal is booked by the role and felled mark it carries, and the bounties and counts stay exact.
/// </summary>
[TestClass]
public class SurvivalRunCommanderTests
{
    private const int Roster = 20;

    private static RunPlan Plan(int startWave, CommanderPlan? commanders = null, EndlessScale? endless = null, BoonPlan? boons = null, RunRules? rules = null, int roster = Roster) =>
        new(
            "helms",
            new[] { new GarrisonRow("fow_militia", FormationSlot.Infantry, 30) },
            new WavePlan(new[] { new WaveSpec(roster, new[] { new WaveTroop("orc", FormationSlot.Infantry, 3), new WaveTroop("skirm", FormationSlot.Ranged, 1) }) },
                new WaveScaling(0, 300), endless, commanders ?? CommanderPlanTests.Plan()),
            new ShopPlan(600, new[] { new ShopOffer("militia", "Militia", "fow_militia", FormationSlot.Infantry, 12, 180) }),
            rules ?? RunRules.Default with { PreparationSeconds = 1f, IntermissionSeconds = 10f },
            new RunOptions { StartWave = startWave, Seed = 1UL })
        {
            Bounties = new Dictionary<string, int> { ["orc"] = 4, ["skirm"] = 6, ["urukinf"] = 20, ["urukxbow"] = 25, ["lugdush"] = 0 },
            Boons = boons ?? BoonPlan.None,
        };

    /// <summary>A run in its wave: the standing count follows what the run spawned, and every batch is kept.</summary>
    private sealed class Field
    {
        public Field(RunPlan plan)
        {
            Run = new SurvivalRun(plan);
            Run.Begin();
        }

        public SurvivalRun Run { get; }
        public int Standing { get; set; }
        public int Defenders { get; set; } = 40;
        public List<SoldierBatch> Batches { get; } = new();
        public List<RunEvent> Events { get; } = new();

        public RunEffects Tick(float dt = 1f, int? attackers = null)
        {
            var fx = Run.Tick(new RunSnapshot(dt, attackers ?? Standing, 0, Defenders, 0, 0, 1000));
            Standing += fx.AttackerBatches.Sum(b => b.Count);
            Batches.AddRange(fx.AttackerBatches);
            Events.AddRange(fx.Events);
            return fx;
        }

        /// <summary>Starts the wave and ticks until its first batch has closed; the commander is not yet out.</summary>
        public Field Open()
        {
            Assert.AreEqual(StartResult.Ok, Run.TryStartWave());
            for (var i = 0; i < 100; i++)
            {
                if (Tick().RefreshAttackerTactic)
                    return this;
            }

            Assert.Fail("the first batch never closed");
            return this;
        }

        /// <summary>Ticks once more so the commander spawns, then tells the run he is up.</summary>
        public Field WithCommanderUp()
        {
            Tick();
            Assert.IsTrue(Batches.Any(b => b.Role == AttackerRole.Commander), "the commander spawned");
            Run.CommanderUp();
            return this;
        }

        public IReadOnlyList<RunEffects> TickFor(int ticks, float dt = 1f)
        {
            var all = new List<RunEffects>();
            for (var i = 0; i < ticks; i++)
                all.Add(Tick(dt));
            return all;
        }
    }

    // ---- The commander enters through the queue ----

    [TestMethod]
    public void TheCommander_IsTheNextSoldierAfterTheFirstBatch_WithHisRanksHealthFactor()
    {
        var field = new Field(Plan(startWave: 4)).Open();
        Assert.IsFalse(field.Batches.Any(b => b.Role == AttackerRole.Commander), "he is queued when the first batch closes, not inside it");

        field.Tick();

        var batch = field.Batches.Single(b => b.Role == AttackerRole.Commander);
        Assert.AreEqual(("lugdush", FormationSlot.Infantry, 1, false, true), (batch.TroopId, batch.Slot, batch.Count, batch.Place, batch.Reinforcement));
        Assert.AreEqual(0, batch.FirstIndex);
        Assert.AreEqual(2f, batch.HealthFactor, 1e-4f, "the captain's own factor, with no endless factor in play");
    }

    [TestMethod]
    public void AWaveThatIsNoCommanderWave_QueuesNoCommander()
    {
        var field = new Field(Plan(startWave: 3)).Open();

        field.TickFor(5);

        Assert.IsFalse(field.Batches.Any(b => b.Role != AttackerRole.Roster));
        Assert.IsNull(field.Run.Commander);
    }

    [TestMethod]
    public void TheCommander_TakesALiveSlot_SoHeWaitsForRoomLikeAnyone()
    {
        var rules = RunRules.Default with { PreparationSeconds = 1f, LiveCap = 101 };
        var field = new Field(Plan(startWave: 4, rules: rules)).Open();

        var full = field.Tick(attackers: 101);
        var room = field.Tick(attackers: 100);

        Assert.AreEqual(0, full.AttackerBatches.Count, "101 stand, so nobody spawns, the commander included");
        Assert.AreEqual(AttackerRole.Commander, room.AttackerBatches.Single().Role);
    }

    [TestMethod]
    public void TheCommanderHealth_MultipliesWithTheEndlessFactor_AndTheStepsOfWave16()
    {
        var endless = new EndlessScale(0.05f, 0.03f, 3f, 3, new Dictionary<string, string>());
        var field = new Field(Plan(startWave: 16, endless: endless)).Open();

        field.Tick();

        var general = field.Batches.Single(b => b.Role == AttackerRole.Commander);
        Assert.AreEqual("lurtz", general.TroopId);
        Assert.AreEqual(4f * 1.25f * 1.75f, general.HealthFactor, 1e-3f, "rank 4 with one step, times 1 + 0.05 * 15 waves past the list");
        Assert.AreEqual(1.75f, field.Batches.First(b => b.Role == AttackerRole.Roster).HealthFactor, 1e-4f, "the roster takes the endless factor alone");
    }

    // ---- War cry ----

    [TestMethod]
    public void AWarCry_FiresAfterItsFirstDelay_LastsItsDuration_AndFiresAgainAfterTheCooldown()
    {
        var field = new Field(Plan(startWave: 4)).Open().WithCommanderUp();

        var ticks = field.TickFor(81);

        for (var i = 0; i < 19; i++)
            Assert.IsNull(ticks[i].PublishStats, "tick " + (i + 1));
        var start = ticks[19];
        Assert.AreEqual(15f, start.PublishStats!.Attackers!.SwingSpeedPercent);
        Assert.AreEqual(10f, start.PublishStats.Attackers.MoveSpeedPercent);
        Assert.IsTrue(start.RefreshAttackers);
        Assert.IsTrue(start.Events.Any(e => e.Kind == RunEventKind.WarCry));
        Assert.IsNull(ticks[33].PublishStats, "still running one second before the duration is up");
        var end = ticks[34];
        Assert.IsNull(end.PublishStats!.Attackers, "the cry is over: the table is the wave's own again");
        Assert.IsTrue(end.RefreshAttackers);
        Assert.IsTrue(end.Events.Any(e => e.Kind == RunEventKind.WarCryEnded));
        Assert.IsTrue(ticks[79].Events.Any(e => e.Kind == RunEventKind.WarCry), "60 s after the first fire");
        Assert.AreEqual(2, field.Run.Commander!.Uses["war_cry"]);
    }

    [TestMethod]
    public void TheLegendCooldownBoon_StretchesTheCooldownButNotTheFirstDelay()
    {
        var row = new BoonRow("longer", BoonCategory.Legend, "{=K}longer", "{=T}text", new[] { new BoonEffect(BoonKeys.CommanderCooldownPercent, 50f) });
        var field = new Field(Plan(startWave: 1, boons: new BoonPlan(new[] { row }, 1)));
        ClearThree(field);
        Assert.AreEqual(PickResult.Ok, field.Run.TryPickBoon("longer"));
        field.Open();
        for (var i = 0; i < 5 && !field.Batches.Any(b => b.Role == AttackerRole.Commander); i++)
            field.Tick();

        field.Run.CommanderUp();
        var ticks = field.TickFor(115);

        Assert.IsTrue(ticks[19].Events.Any(e => e.Kind == RunEventKind.WarCry), "the first delay is still 20 s");
        Assert.IsFalse(ticks.Skip(20).Take(88).Any(f => f.Events.Any(e => e.Kind == RunEventKind.WarCry)), "no second cry inside 90 s");
        Assert.IsTrue(ticks[109].Events.Any(e => e.Kind == RunEventKind.WarCry), "60 s becomes 90 s");
    }

    private static void ClearThree(Field field)
    {
        var run = field.Run;
        for (var i = 0; i < 5000 && run.WavesCleared < 3 && run.Result == null; i++)
        {
            if (run.GetView().Phase == RunPhase.Intermission)
                run.TryStartWave();
            run.Tick(new RunSnapshot(1f, 0, 0, 40, 0, 0, 1000));
        }

        Assert.AreEqual(3, run.WavesCleared);
    }

    // ---- Summon ----

    [TestMethod]
    public void ASummon_QueuesItsCountAtTheHeadOfTheQueue_AsSummonedSoldiersOfOneUse()
    {
        var field = new Field(Plan(startWave: 8)).Open().WithCommanderUp();
        var before = field.Batches.Count;

        var ticks = field.TickFor(34);

        var fire = ticks.Single(f => f.Events.Any(e => e.Kind == RunEventKind.Summoned));
        StringAssert.Contains(fire.Events.Single(e => e.Kind == RunEventKind.Summoned).Detail, "use=1 count=20");
        var summoned = field.Batches.Skip(before).Where(b => b.Role == AttackerRole.Summoned).ToList();
        Assert.AreEqual(20, summoned.Sum(b => b.Count));
        Assert.IsTrue(summoned.All(b => b.Use == 1 && !b.Place && b.Reinforcement && b.HealthFactor == 1f));
        CollectionAssert.AreEquivalent(new[] { "urukinf", "urukxbow" }, summoned.Select(b => b.TroopId).Distinct().ToList());
        Assert.AreEqual(15, summoned.Where(b => b.TroopId == "urukinf").Sum(b => b.Count), "3 to 1 over the mix");
        Assert.AreEqual(1, field.Run.Commander!.Uses["summon_uruks"]);
    }

    [TestMethod]
    public void ASummon_NeverExceedsLiveRoom_ItsSoldiersWaitInTheQueue()
    {
        var rules = RunRules.Default with { PreparationSeconds = 1f, LiveCap = 101, SpawnsPerTick = 50 };
        var field = new Field(Plan(startWave: 8, rules: rules)).Open().WithCommanderUp();

        for (var i = 0; i < 34; i++)
            field.Tick(attackers: 101);
        var full = field.Tick(attackers: 101);
        var open = field.Tick(attackers: 95);

        Assert.AreEqual(0, full.AttackerBatches.Count);
        Assert.AreEqual(6, open.AttackerBatches.Sum(b => b.Count), "only the room that is free, 101 - 95");
        Assert.IsTrue(open.AttackerBatches.All(b => b.Role == AttackerRole.Summoned));
    }

    [TestMethod]
    public void QueuedSummons_CountInTheStalemateRulesWaveSize_SoStrandedOnesScaleTheStragglerShare()
    {
        var rules = RunRules.Default with { PreparationSeconds = 1f, SpawnsPerTick = 50 };
        var field = new Field(Plan(startWave: 8, rules: rules, roster: 110)).Open().WithCommanderUp();
        field.TickFor(34);
        field.Run.CommanderDown(false);
        Assert.AreEqual(131, field.Standing, "110 roster, the commander and 20 summoned all out");
        field.Tick(attackers: 12, dt: 60f);
        Assert.IsNull(field.Run.Result);

        var fx = field.Tick(attackers: 12, dt: 90f);

        Assert.AreEqual(12, fx.FellAttackers, "12 is within a tenth of 130 (13), not of 110 (11)");
        Assert.IsNull(field.Run.Result);
    }

    // ---- His end ----

    [TestMethod]
    public void AKilledCommander_PaysHisRanksBounty_IsNoRosterKill_AndStopsEveryClock()
    {
        var field = new Field(Plan(startWave: 8)).Open().WithCommanderUp();
        field.TickFor(25);
        var before = field.Run.Gold;
        var kills = field.Run.Stats.TotalKills;

        field.Run.AttackerDown("ugluk", AttackerRole.Commander, felled: false);

        Assert.AreEqual(before + 250, field.Run.Gold);
        Assert.AreEqual(250, field.Run.Purse.EarnedFrom(PurseSource.Commander));
        Assert.AreEqual(kills, field.Run.Stats.TotalKills, "a commander is not a roster kill");
        Assert.AreEqual(CommanderOutcome.Killed, field.Run.Commander!.Outcome);
        var next = field.Tick();
        Assert.IsNull(next.PublishStats!.Attackers, "a war cry running at his death ends");
        Assert.IsTrue(next.RefreshAttackers);
        var rest = field.TickFor(400);
        Assert.IsFalse(rest.Any(f => f.Events.Any(e => e.Kind is RunEventKind.WarCry or RunEventKind.Summoned)), "no ability fires after his end");
        Assert.AreEqual(0, field.Run.Commander.UsesAfterEnd);
    }

    [TestMethod]
    public void AFelledCommander_PaysNothing()
    {
        var field = new Field(Plan(startWave: 4)).Open().WithCommanderUp();
        var before = field.Run.Gold;

        field.Run.AttackerDown("lugdush", AttackerRole.Commander, felled: true);

        Assert.AreEqual(before, field.Run.Gold);
        Assert.AreEqual(CommanderOutcome.Felled, field.Run.Commander!.Outcome);
    }

    [TestMethod]
    public void ACommanderWhoLeavesAlive_StopsSummoningAndPaysNothing()
    {
        var field = new Field(Plan(startWave: 8)).Open().WithCommanderUp();
        field.TickFor(10);
        var before = field.Run.Gold;

        field.Run.CommanderDown(killed: false);

        Assert.AreEqual(before, field.Run.Gold);
        Assert.AreEqual(CommanderOutcome.Left, field.Run.Commander!.Outcome);
        Assert.IsFalse(field.TickFor(300).Any(f => f.Events.Any(e => e.Kind is RunEventKind.WarCry or RunEventKind.Summoned)));
    }

    [TestMethod]
    public void QueuedSummons_StillComeAfterHisEnd()
    {
        var rules = RunRules.Default with { PreparationSeconds = 1f, LiveCap = 101, SpawnsPerTick = 50 };
        var field = new Field(Plan(startWave: 8, rules: rules)).Open().WithCommanderUp();
        for (var i = 0; i < 35; i++)
            field.Tick(attackers: 101);

        field.Run.AttackerDown("ugluk", AttackerRole.Commander, felled: false);
        var after = field.Tick(attackers: 90);

        Assert.AreEqual(11, after.AttackerBatches.Sum(b => b.Count), "the summons still queued spawn as room allows");
    }

    [TestMethod]
    public void CommanderDownTwice_OrCommanderUpAfterIt_PaysOnceAndChangesNothingMore()
    {
        var field = new Field(Plan(startWave: 4)).Open().WithCommanderUp();
        field.Run.CommanderDown(killed: true);
        var gold = field.Run.Gold;

        field.Run.CommanderDown(killed: true);
        field.Run.CommanderDown(killed: false);
        field.Run.CommanderUp();

        Assert.AreEqual(gold, field.Run.Gold, "his bounty is paid once");
        Assert.AreEqual(CommanderOutcome.Killed, field.Run.Commander!.Outcome);
    }

    [TestMethod]
    public void CommanderUpOnAWaveWithNoCommander_IsIgnored()
    {
        var field = new Field(Plan(startWave: 3)).Open();

        field.Run.CommanderUp();
        field.Run.CommanderDown(killed: true);

        Assert.IsNull(field.Run.Commander);
    }

    // ---- Roles and the felled mark ----

    [TestMethod]
    public void RosterKillsPlusFelled_EqualTheRosterCount_WithAFelledSummonAndAFelledCommanderInTheWave()
    {
        var abilities = new Dictionary<string, CommanderAbility>
        {
            ["war_cry"] = CommanderPlanTests.WarCry(),
            ["summon_uruks"] = CommanderPlanTests.Summon(count: 4),
        };
        var commanders = new CommanderPlan(4, CommanderPlanTests.Plan().Ranks, abilities, CommanderBeyond.None);
        var field = new Field(Plan(startWave: 8, commanders: commanders)).Open().WithCommanderUp();
        field.TickFor(34);
        var gold = field.Run.Gold;

        for (var i = 0; i < 18; i++)
            field.Run.AttackerDown("orc");
        field.Run.AttackerDown("orc", AttackerRole.Roster, felled: true);
        field.Run.AttackerDown("skirm", AttackerRole.Roster, felled: true);
        field.Run.AttackerDown("urukinf", AttackerRole.Summoned, felled: false);
        for (var i = 0; i < 3; i++)
            field.Run.AttackerDown("urukinf", AttackerRole.Summoned, felled: true);
        field.Run.AttackerDown("ugluk", AttackerRole.Commander, felled: true);
        Assert.AreEqual(gold + 18 * 4 + 20, field.Run.Gold, "roster and summon kills pay their troops' bounties; the felled pay nothing");
        field.Run.Tick(new RunSnapshot(1f, 0, 0, 40, 0, 0, 1000));

        var wave = field.Run.Stats.Waves.Single();
        Assert.AreEqual((18, 2), (wave.Kills, wave.Felled));
        Assert.AreEqual(Roster, wave.Kills + wave.Felled, "leaderboard rule 5 holds with a felled summon and commander in the wave");
        Assert.AreEqual((4, 1, 3), (wave.Summoned, wave.SummonKills, wave.SummonFelled));
        Assert.AreEqual(wave.Summoned, wave.SummonKills + wave.SummonFelled);
        Assert.AreEqual(18, field.Run.Stats.TotalKills);
    }

    [TestMethod]
    public void TheWaveRecord_KeepsTheCommandersTroopRankStepsOutcomeAndUses()
    {
        var field = new Field(Plan(startWave: 16)).Open().WithCommanderUp();
        field.TickFor(25);
        field.Run.AttackerDown("lurtz", AttackerRole.Commander, felled: false);
        for (var i = 0; i < field.Standing - 1; i++)
            field.Run.AttackerDown("orc");
        field.Run.Tick(new RunSnapshot(1f, 0, 0, 40, 0, 0, 1000));

        var record = field.Run.Stats.Waves.Single().Commander!;

        Assert.AreEqual(("lurtz", "general", 1, CommanderOutcome.Killed), (record.TroopId, record.RankId, record.Steps, record.Outcome));
        Assert.AreEqual(1, record.Uses["war_cry"]);
        Assert.AreEqual(0, record.Uses["summon_uruks"], "25 s into his life the summon, due at 30 s, has not fired");
    }

    [TestMethod]
    public void AFellDecision_NoLongerSwallowsAnUnmarkedKill()
    {
        var field = new Field(Plan(startWave: 1, rules: new RunRules { StragglerCount = 0 }, roster: 110)).Open();
        field.Tick(attackers: 5, dt: 60f);
        var fx = field.Tick(attackers: 5, dt: 90f);
        Assert.AreEqual(5, fx.FellAttackers);
        var before = field.Run.Gold;

        field.Run.AttackerDown("orc");

        Assert.AreEqual(before + 4, field.Run.Gold, "a removal is felled only when it carries the mark");
        Assert.AreEqual(0, field.Run.Stats.Waves.Count, "and the decision itself books nothing");
    }

    // ---- The view ----

    [TestMethod]
    public void TheView_ShowsTheCommanderWhileHeStands_AndHisOutcomeAfterwards()
    {
        var field = new Field(Plan(startWave: 4)).Open().WithCommanderUp();
        field.TickFor(5);

        var standing = field.Run.GetView().Commander!;

        Assert.AreEqual(("{=TAOM_LD_Rank_Captain}Captain", CommanderOutcome.Standing), (standing.RankName, standing.Outcome));
        Assert.AreEqual(15f, standing.Abilities.Single().SecondsLeft, 0.01f);
        field.Run.AttackerDown("lugdush", AttackerRole.Commander, felled: false);
        Assert.AreEqual(CommanderOutcome.Killed, field.Run.GetView().Commander!.Outcome);
    }

    [TestMethod]
    public void TheView_HasNoCommanderOutsideACommanderWave()
    {
        var field = new Field(Plan(startWave: 3)).Open();

        Assert.IsNull(field.Run.GetView().Commander);
    }
}
