using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.LongDefeat.Domain;

namespace TAOM.Tests.Features.LongDefeat.Domain;

[TestClass]
public class SurvivalRunTests
{
    private const int Big = 1000;

    private static WaveTroop T(string id, FormationSlot slot, int weight) => new(id, slot, weight);

    private static RunPlan Plan(RunRules? rules = null, RunOptions? options = null, GarrisonRow[]? garrison = null, int gold = 600,
        WaveSpec[]? waveSpecs = null)
    {
        var waves = new WavePlan(
            waveSpecs ?? new[]
            {
                new WaveSpec(110, new[] { T("orc", FormationSlot.Infantry, 75), T("skirm", FormationSlot.Ranged, 25) }),
                new WaveSpec(120, new[] { T("orc", FormationSlot.Infantry, 50), T("skirm", FormationSlot.Ranged, 50) }),
            },
            new WaveScaling(10, 300));
        var shop = new ShopPlan(gold, new[]
        {
            new ShopOffer("militia", "Militia", "fow_militia", FormationSlot.Infantry, 12, 180),
            new ShopOffer("yeomen", "Yeomen", "fow_yeomen", FormationSlot.Ranged, 10, 200),
        });
        return new RunPlan(
            "helms",
            garrison ?? new[]
            {
                new GarrisonRow("fow_militia", FormationSlot.Infantry, 30),
                new GarrisonRow("fow_yeomen", FormationSlot.Ranged, 10),
            },
            waves,
            shop,
            rules ?? RunRules.Default,
            options ?? RunOptions.Default)
        {
            Bounties = new Dictionary<string, int> { ["orc"] = 4, ["skirm"] = 6 },
        };
    }

    private static RunSnapshot Snap(float dt = 1f, int attackers = 0, int fleeing = 0, int defenders = 40, int atKeep = 0,
        int defAtKeep = 0, int battleSize = Big) =>
        new(dt, attackers, fleeing, defenders, atKeep, defAtKeep, battleSize);

    private static SurvivalRun Begun(RunPlan? plan = null)
    {
        var run = new SurvivalRun(plan ?? Plan());
        run.Begin();
        return run;
    }

    /// <summary>Ticks until the run is in Fighting for the wave it just started; returns every effect on the way.</summary>
    private static List<RunEffects> SpawnWave(SurvivalRun run, int defenders = 40, int battleSize = Big)
    {
        var all = new List<RunEffects>();
        var standing = 0;
        for (var i = 0; i < 100; i++)
        {
            var fx = run.Tick(Snap(attackers: standing, defenders: defenders, battleSize: battleSize));
            all.Add(fx);
            standing += fx.AttackerBatches.Sum(b => b.Count);
            if (fx.RefreshAttackerTactic)
                return all;
        }

        Assert.Fail("the wave never finished its first batch");
        return all;
    }

    // ---- Begin and muster ----

    [TestMethod]
    public void Begin_EmitsArchersAndFirstFootBatchThenPlayer()
    {
        var run = new SurvivalRun(Plan());

        var fx = run.Begin();

        Assert.IsTrue(fx.SpawnPlayer);
        Assert.IsTrue(fx.FinishInitialSpawn);
        var ranged = fx.DefenderBatches.Single(b => b.Slot == FormationSlot.Ranged);
        Assert.AreEqual((10, 0, 10, true, false), (ranged.Count, ranged.FirstIndex, ranged.BatchTotal, ranged.Place, ranged.Reinforcement));
        var foot = fx.DefenderBatches.Single(b => b.Slot == FormationSlot.Infantry);
        Assert.AreEqual((20, 0, 20, true, false), (foot.Count, foot.FirstIndex, foot.BatchTotal, foot.Place, foot.Reinforcement));
    }

    [TestMethod]
    public void Begin_CalledTwice_EmitsNothingSecondTime()
    {
        var run = new SurvivalRun(Plan());
        run.Begin();

        var again = run.Begin();

        Assert.AreEqual(0, again.DefenderBatches.Count);
        Assert.IsFalse(again.SpawnPlayer);
    }

    [TestMethod]
    public void Begin_FootFromTwoTroops_IndicesRunAcrossTheBatch()
    {
        var run = new SurvivalRun(Plan(garrison: new[]
        {
            new GarrisonRow("a", FormationSlot.Infantry, 15),
            new GarrisonRow("b", FormationSlot.Infantry, 15),
        }));

        var fx = run.Begin();

        var foot = fx.DefenderBatches.Where(b => b.Slot == FormationSlot.Infantry).ToList();
        CollectionAssert.AreEqual(new[] { "a", "b" }, foot.Select(b => b.TroopId).ToArray());
        CollectionAssert.AreEqual(new[] { 15, 5 }, foot.Select(b => b.Count).ToArray());
        CollectionAssert.AreEqual(new[] { 0, 15 }, foot.Select(b => b.FirstIndex).ToArray());
        Assert.IsTrue(foot.All(b => b.BatchTotal == 20));
        Assert.AreEqual(1, foot.Count(b => b.Place), "a formation is placed once, on its first batch");
    }

    [TestMethod]
    public void Muster_RestOfTheFootComesInTimedBatchesWithoutPlacing()
    {
        var run = Begun();

        Assert.AreEqual(0, run.Tick(Snap(dt: 3.9f)).DefenderBatches.Count);
        var fx = run.Tick(Snap(dt: 0.2f));

        var batch = fx.DefenderBatches.Single();
        Assert.AreEqual((10, 10, false, false), (batch.Count, batch.BatchTotal, batch.Reinforcement, batch.Place));
        Assert.AreEqual(0, run.Tick(Snap(dt: 10f)).DefenderBatches.Count, "all the foot is out");
    }

    [TestMethod]
    public void Muster_BatchesFollowTheRulesBatchSize()
    {
        var run = Begun(Plan(new RunRules { MusterBatch = 5, MusterBatchSeconds = 1f }));

        var fx = run.Tick(Snap(dt: 1f));

        Assert.AreEqual(5, fx.DefenderBatches.Single().Count);
    }

    // ---- Preparation, horn, intermission ----

    [TestMethod]
    public void Preparation_TimerExpiry_StartsWaveOneWithoutBonus()
    {
        var run = Begun();
        run.Tick(Snap(dt: 89f));
        Assert.AreEqual(RunPhase.Preparation, run.Phase);

        var fx = run.Tick(Snap(dt: 1f));

        Assert.AreEqual(RunPhase.Wave, run.Phase);
        Assert.AreEqual(1, run.Wave);
        Assert.AreEqual(1, fx.StartWave!.Wave);
        Assert.AreEqual(600, run.Gold);
    }

    [TestMethod]
    public void Horn_PaysTwoPerSecondLeftUpToTheCap()
    {
        var run = Begun();
        run.Tick(Snap(dt: 40f));

        Assert.AreEqual(StartResult.Ok, run.TryStartWave());

        Assert.AreEqual(100, run.LastHornBonus);
        Assert.AreEqual(700, run.Gold);
        Assert.AreEqual(RunPhase.Wave, run.Phase);
        Assert.AreEqual(1, run.Wave);
    }

    [TestMethod]
    public void Horn_AtTheFullTimer_IsCapped()
    {
        var run = Begun();

        run.TryStartWave();

        Assert.AreEqual(120, run.LastHornBonus);
        Assert.AreEqual(720, run.Gold);
    }

    [TestMethod]
    public void Horn_RoundsPartialSecondsUp()
    {
        var run = Begun();
        run.Tick(Snap(dt: 89.75f));

        run.TryStartWave();

        Assert.AreEqual(1, run.LastHornBonus, "0.25 s left at 2 per second is 0.5, rounded up");
    }

    [TestMethod]
    public void Horn_NaNRate_PaysNothingButStillStarts()
    {
        var run = Begun(Plan(new RunRules { EarlyGoldPerSecond = float.NaN }));

        Assert.AreEqual(StartResult.Ok, run.TryStartWave());

        Assert.AreEqual(0, run.LastHornBonus);
        Assert.AreEqual(600, run.Gold);
        Assert.AreEqual(RunPhase.Wave, run.Phase);
    }

    [TestMethod]
    public void Horn_InAWave_IsNotOpen()
    {
        var run = Begun();
        run.TryStartWave();

        Assert.AreEqual(StartResult.NotOpen, run.TryStartWave());
        Assert.AreEqual(1, run.Wave);
    }

    [TestMethod]
    public void Horn_StartsTheWaveOnTheNextTick_WithItsEvents()
    {
        var run = Begun();
        run.TryStartWave();

        var fx = run.Tick(Snap());

        Assert.AreEqual(1, fx.StartWave!.Wave);
        CollectionAssert.Contains(fx.Events.Select(e => e.Kind).ToList(), RunEventKind.Horn);
        Assert.IsTrue(fx.AttackerBatches.Count > 0, "the first attackers spawn on the tick the wave begins");
    }

    [TestMethod]
    public void Intermission_AfterTheTimer_StartsTheNextWave()
    {
        var run = ClearedWaveOne();

        run.Tick(Snap(dt: 59f));
        Assert.AreEqual(RunPhase.Intermission, run.Phase);
        var fx = run.Tick(Snap(dt: 1f));

        Assert.AreEqual(RunPhase.Wave, run.Phase);
        Assert.AreEqual(2, fx.StartWave!.Wave);
    }

    // ---- Shop ----

    [TestMethod]
    public void TryBuy_Ok_SpendsAtOnceAndTheNextTickEmitsAReinforcementPack()
    {
        var run = Begun();
        run.Tick(Snap(dt: 20f));

        Assert.AreEqual(BuyResult.Ok, run.TryBuy("militia"));

        Assert.AreEqual(420, run.Gold);
        var fx = run.Tick(Snap(dt: 0.1f));
        var pack = fx.DefenderBatches.Single(b => b.TroopId == "fow_militia");
        Assert.AreEqual((12, 12, true, false), (pack.Count, pack.BatchTotal, pack.Reinforcement, pack.Place));
        Assert.IsTrue(fx.Events.Any(e => e.Kind == RunEventKind.Bought && e.Detail.Contains("militia")));
        Assert.AreEqual(0, run.Tick(Snap(dt: 0.1f)).DefenderBatches.Count(b => b.TroopId == "fow_militia"), "a pack comes out once");
    }

    [TestMethod]
    public void TryBuy_InAWave_IsNotOpen()
    {
        var run = Begun();
        run.TryStartWave();

        Assert.AreEqual(BuyResult.NotOpen, run.TryBuy("militia"));
        Assert.AreEqual(720, run.Gold);
    }

    [TestMethod]
    public void TryBuy_UnknownOffer()
    {
        Assert.AreEqual(BuyResult.UnknownOffer, Begun().TryBuy("dragon"));
    }

    [TestMethod]
    public void TryBuy_WithoutTheGold_IsNoGoldAndSpendsNothing()
    {
        var run = Begun(Plan(gold: 100));

        Assert.AreEqual(BuyResult.NoGold, run.TryBuy("militia"));
        Assert.AreEqual(100, run.Gold);
    }

    [TestMethod]
    public void TryBuy_PastTheGarrisonCap_IsGarrisonFull_CountingPendingPacks()
    {
        var run = Begun(Plan(new RunRules { GarrisonCap = 50 }, gold: 5000));
        run.Tick(Snap(dt: 20f, defenders: 40));

        Assert.AreEqual(BuyResult.GarrisonFull, run.TryBuy("militia"), "40 standing plus 12 is 52 over 50");

        var roomy = Begun(Plan(new RunRules { GarrisonCap = 60 }, gold: 5000));
        roomy.Tick(Snap(dt: 20f, defenders: 40));
        Assert.AreEqual(BuyResult.Ok, roomy.TryBuy("militia"));
        Assert.AreEqual(BuyResult.GarrisonFull, roomy.TryBuy("militia"), "the first pack is pending, 40 plus 12 plus 12 is over 60");
    }

    [TestMethod]
    public void TryBuy_AfterTheRunEnded_IsRunOver()
    {
        var run = Begun();
        run.Abandon();

        Assert.AreEqual(BuyResult.RunOver, run.TryBuy("militia"));
        Assert.AreEqual(600, run.Gold);
    }

    // ---- Wave spawning ----

    [TestMethod]
    public void WaveStart_SpawnsEightPerTick_AndRefreshesTheTacticOnlyOnceTheFirstBatchIsOut()
    {
        var run = Begun();
        run.TryStartWave();

        // 150 battle size against 200 defenders leaves no room, so the 101 floor applies.
        var ticks = SpawnWave(run, defenders: 200, battleSize: 150);

        var spawnedBeforeRefresh = ticks.SelectMany(t => t.AttackerBatches).Sum(b => b.Count);
        Assert.AreEqual(101, spawnedBeforeRefresh);
        Assert.IsTrue(ticks.Take(ticks.Count - 1).All(t => !t.RefreshAttackerTactic && t.AttackerBatches.Sum(b => b.Count) <= 8));
        Assert.AreEqual(13, ticks.Count);
    }

    [TestMethod]
    public void WaveStart_FirstBatchIsTheWaveWhenThereIsRoom()
    {
        var run = Begun();
        run.TryStartWave();

        var ticks = SpawnWave(run);

        Assert.AreEqual(110, ticks.SelectMany(t => t.AttackerBatches).Sum(b => b.Count));
        var start = ticks.Select(t => t.StartWave).Single(s => s != null)!;
        Assert.AreEqual((1, 83, 27), (start.Wave, start.Foot, start.Ranged));
    }

    [TestMethod]
    public void WaveStart_PlacesEachClassOnceAndKeepsItsBatchOpenAcrossTicks()
    {
        var run = Begun();
        run.TryStartWave();

        var batches = SpawnWave(run).SelectMany(t => t.AttackerBatches).ToList();

        foreach (var slot in new[] { FormationSlot.Infantry, FormationSlot.Ranged })
        {
            var ofClass = batches.Where(b => b.Slot == slot).ToList();
            Assert.AreEqual(1, ofClass.Count(b => b.Place), slot + " is placed once");
            Assert.IsTrue(ofClass[0].Place);
            Assert.IsTrue(ofClass.All(b => b.Reinforcement));
            Assert.IsTrue(ofClass.All(b => b.BatchTotal == ofClass.Sum(x => x.Count)));
            var next = 0;
            foreach (var b in ofClass)
            {
                Assert.AreEqual(next, b.FirstIndex);
                next += b.Count;
            }
        }
    }

    [TestMethod]
    public void WaveStart_RangedSpawnFirst()
    {
        var run = Begun();
        run.TryStartWave();

        var firstTick = run.Tick(Snap());

        Assert.IsTrue(firstTick.AttackerBatches.All(b => b.Slot == FormationSlot.Ranged));
    }

    [TestMethod]
    public void Spawning_LaterSoldiers_GoOutInTheirOwnClosedBatches()
    {
        var run = Begun();
        run.TryStartWave();
        // 200 defenders, 150 battle size: first batch is 101 of the 110.
        var standing = 0;
        RunEffects fx;
        do
        {
            fx = run.Tick(Snap(attackers: standing, defenders: 200, battleSize: 150));
            standing += fx.AttackerBatches.Sum(b => b.Count);
        }
        while (!fx.RefreshAttackerTactic);

        // Eleven fell, so there is room for the last nine, eight to a tick.
        var trickle = run.Tick(Snap(attackers: standing - 11, defenders: 200, battleSize: 150));

        Assert.AreEqual(8, trickle.AttackerBatches.Sum(b => b.Count));
        Assert.IsTrue(trickle.AttackerBatches.All(b => !b.Place && b.FirstIndex == 0 && b.BatchTotal == b.Count && b.Reinforcement));
        Assert.IsFalse(trickle.RefreshAttackerTactic);
    }

    [TestMethod]
    public void Spawning_StopsWhenTheLiveRoomIsFull()
    {
        var run = Begun();
        run.TryStartWave();
        SpawnWave(run, defenders: 200, battleSize: 150);

        var fx = run.Tick(Snap(attackers: 101, defenders: 200, battleSize: 150));

        Assert.AreEqual(0, fx.AttackerBatches.Count, "101 standing fills the floor room");
    }

    // ---- Bounty, clear ----

    private static SurvivalRun InFighting(RunPlan? plan = null)
    {
        var run = Begun(plan);
        run.TryStartWave();
        SpawnWave(run);
        return run;
    }

    [TestMethod]
    public void AttackerDown_PaysTheTroopsBounty_AnUnknownTroopPaysNothing()
    {
        var run = InFighting();
        var before = run.Gold;

        run.AttackerDown("orc");
        run.AttackerDown("skirm");
        run.AttackerDown("mystery");

        Assert.AreEqual(before + 10, run.Gold);
        Assert.AreEqual(3, run.Stats.TotalKills);
    }

    [TestMethod]
    public void AStaleKeepCount_WithNobodyStanding_DoesNotRaiseTheBar()
    {
        var run = InFighting();
        run.Tick(Snap(attackers: 50, atKeep: 30, defAtKeep: 0, dt: 44.5f));
        Assert.IsTrue(run.GetView().KeepProgress > 0.98f);

        // The keep count is up to half a second older than the standing count: the last attackers died inside that window.
        run.Tick(Snap(attackers: 0, atKeep: 30, defAtKeep: 0, dt: 1f));

        Assert.IsNull(run.Result, "nobody stands at the keep, so the bar cannot fill");
        Assert.AreEqual(RunPhase.Intermission, run.Phase);
    }

    [TestMethod]
    public void Clear_PaysTheWaveBonus_ResetsKeepAndEntersIntermission()
    {
        var run = InFighting();
        run.Tick(Snap(attackers: 50, atKeep: 30, defAtKeep: 0, dt: 10f));
        Assert.IsTrue(run.GetView().KeepProgress > 0f);
        var before = run.Gold;

        var fx = run.Tick(Snap(attackers: 0, dt: 1f));

        Assert.AreEqual(RunPhase.Intermission, run.Phase);
        Assert.AreEqual(before + 200, run.Gold, "150 + 50 * wave 1");
        Assert.AreEqual(1, run.WavesCleared);
        Assert.AreEqual(0f, run.GetView().KeepProgress);
        Assert.IsTrue(fx.Events.Any(e => e.Kind == RunEventKind.WaveCleared));
        Assert.AreEqual(1, run.Stats.Waves.Count);
        Assert.AreEqual(60f, run.GetView().SecondsLeft);
    }

    [TestMethod]
    public void Clear_NeverFiresOnTheTickThatSpawnedTheLastSoldier()
    {
        var run = Begun();
        run.TryStartWave();
        var standing = 0;
        RunEffects fx;
        do
        {
            fx = run.Tick(Snap(attackers: standing, defenders: 200, battleSize: 150));
            standing += fx.AttackerBatches.Sum(b => b.Count);
        }
        while (!fx.RefreshAttackerTactic);

        // 9 are left: the second trickle tick empties the queue while the snapshot still shows no attackers from it.
        run.Tick(Snap(attackers: 0, defenders: 200, battleSize: 150));
        var trickle = run.Tick(Snap(attackers: 0, defenders: 200, battleSize: 150));

        Assert.AreEqual(1, trickle.AttackerBatches.Sum(b => b.Count));
        Assert.AreEqual(RunPhase.Wave, run.Phase);
    }

    [TestMethod]
    public void Clear_WaitsForTheQueue()
    {
        var run = Begun();
        run.TryStartWave();
        run.Tick(Snap(attackers: 0));

        run.Tick(Snap(attackers: 0));

        Assert.AreEqual(RunPhase.Wave, run.Phase);
        Assert.AreEqual(0, run.WavesCleared);
    }

    [TestMethod]
    public void Clear_AtTheWaveTarget_EndsAsTargetReachedAfterPayingTheBonus()
    {
        var run = InFighting(Plan(options: new RunOptions { StopAfterWaves = 1 }));
        var before = run.Gold;

        var fx = run.Tick(Snap(attackers: 0));

        Assert.AreEqual(RunPhase.Ended, run.Phase);
        Assert.AreEqual(RunEndReason.TargetReached, run.Result!.Reason);
        Assert.IsTrue(run.Result.IsFinal);
        Assert.AreEqual(1, run.Result.WavesCleared);
        Assert.AreEqual(before + 200, run.Gold);
    }

    // ---- Loss ----

    [TestMethod]
    public void Loss_NoDefendersAndNothingPending_EndsAsDefendersDead()
    {
        var run = Begun();
        run.Tick(Snap(dt: 10f));

        var fx = run.Tick(Snap(defenders: 0));

        Assert.AreEqual(RunEndReason.DefendersDead, run.Result!.Reason);
        Assert.AreEqual(RunPhase.Ended, run.Phase);
    }

    [TestMethod]
    public void Loss_WhileTheMusterIsStillComing_IsNotALoss()
    {
        var run = Begun();

        run.Tick(Snap(dt: 1f, defenders: 0));

        Assert.IsNull(run.Result);
    }

    [TestMethod]
    public void Loss_WhileABoughtPackIsPending_IsNotALoss()
    {
        var run = Begun(Plan(garrison: new[] { new GarrisonRow("fow_militia", FormationSlot.Infantry, 5) }));
        run.Tick(Snap(dt: 1f, defenders: 5));
        run.TryBuy("militia");

        run.Tick(Snap(dt: 0.1f, defenders: 0));

        Assert.IsNull(run.Result);
    }

    // ---- Keep ----

    [TestMethod]
    public void Keep_HeldByAttackersLongEnough_EndsAsKeepLost()
    {
        var run = InFighting();
        run.Tick(Snap(attackers: 50, atKeep: 30, defAtKeep: 0, dt: 44f));
        Assert.IsNull(run.Result);

        run.Tick(Snap(attackers: 50, atKeep: 30, defAtKeep: 0, dt: 1.5f));

        Assert.AreEqual(RunEndReason.KeepLost, run.Result!.Reason);
    }

    [TestMethod]
    public void Keep_ElevenUnopposed_NeverMoves()
    {
        var run = InFighting();

        run.Tick(Snap(attackers: 50, atKeep: 11, defAtKeep: 0, dt: 500f));

        Assert.AreEqual(0f, run.GetView().KeepProgress);
        Assert.IsNull(run.Result);
    }

    [TestMethod]
    public void Keep_DoesNotRunInPreparation()
    {
        var run = Begun();

        run.Tick(Snap(atKeep: 50, defAtKeep: 0, dt: 20f));

        Assert.AreEqual(0f, run.GetView().KeepProgress);
    }

    [TestMethod]
    public void Keep_NaNDt_LeavesProgressAlone()
    {
        var run = InFighting();
        run.Tick(Snap(attackers: 50, atKeep: 30, dt: 10f));
        var progress = run.GetView().KeepProgress;

        run.Tick(Snap(attackers: 50, atKeep: 30, dt: float.NaN));

        Assert.AreEqual(progress, run.GetView().KeepProgress);
    }

    // ---- Stalemate ----

    [TestMethod]
    public void Stalemate_QuietWithHalfTheWaveDown_OrdersTheCharge()
    {
        var run = InFighting();

        // The tick that finished the first batch already counted one second of quiet.
        var early = run.Tick(Snap(attackers: 55, dt: 58f));
        var fx = run.Tick(Snap(attackers: 55, dt: 1f));

        Assert.IsFalse(early.OrderCharge);
        Assert.IsTrue(fx.OrderCharge);
        Assert.IsTrue(fx.Events.Any(e => e.Kind == RunEventKind.Charge));
    }

    [TestMethod]
    public void Stalemate_AnAttackerDeathResetsTheQuiet()
    {
        AssertALossResetsTheQuiet(run => run.AttackerDown("orc"));
    }

    [TestMethod]
    public void Stalemate_ADefenderDeathResetsTheQuiet()
    {
        AssertALossResetsTheQuiet(run => run.DefenderDown());
    }

    private static void AssertALossResetsTheQuiet(Action<SurvivalRun> loss)
    {
        var run = InFighting();

        // The tick that finished the first batch already counted one second of quiet.
        Assert.IsFalse(run.Tick(Snap(attackers: 55, dt: 58f)).OrderCharge);
        loss(run);
        Assert.IsFalse(run.Tick(Snap(attackers: 55, dt: 1f)).OrderCharge, "the loss tick is not quiet");
        Assert.IsFalse(run.Tick(Snap(attackers: 55, dt: 59f)).OrderCharge, "59 s since the loss");
        Assert.IsTrue(run.Tick(Snap(attackers: 55, dt: 1f)).OrderCharge, "60 s since the loss");
    }

    [TestMethod]
    public void Stalemate_ElevenAgainstTwoInAWaveOf110_EndsAsStalemateFellingNothing()
    {
        var run = InFighting();
        run.Tick(Snap(attackers: 11, defenders: 2, dt: 60f));
        var before = run.Gold;

        var fx = run.Tick(Snap(attackers: 11, defenders: 2, dt: 90f));

        Assert.AreEqual(RunEndReason.Stalemate, run.Result!.Reason);
        Assert.AreEqual(0, fx.FellAttackers);
        Assert.AreEqual(before, run.Gold, "no bonus for a stalemate");
    }

    [TestMethod]
    public void Stalemate_AHandfulAgainstManyDefenders_FellsTheStragglersAndTheClearPaysTheBonus()
    {
        var run = InFighting();
        run.Tick(Snap(attackers: 5, defenders: 20, dt: 60f));

        var fx = run.Tick(Snap(attackers: 5, defenders: 20, dt: 90f));

        Assert.AreEqual(5, fx.FellAttackers);
        Assert.IsNull(run.Result);
        var before = run.Gold;
        for (var i = 0; i < 5; i++)
            run.AttackerDown("orc");
        Assert.AreEqual(before, run.Gold, "a felled soldier pays no bounty");
        var clear = run.Tick(Snap(attackers: 0, defenders: 20, dt: 1f));

        Assert.AreEqual(before + 200, run.Gold);
        Assert.IsTrue(clear.RestoreDefenderTactic, "the charge was ordered, so the defenders get their tactic back");
        Assert.AreEqual(5, run.Stats.Waves[0].Felled);
        Assert.AreEqual(1, run.Stats.WavesFelled);
        Assert.AreEqual(0, run.Stats.Waves[0].Kills, "felled soldiers are not kills");
    }

    [TestMethod]
    public void Stalemate_FellPendingResetsAtAClear_SoAShortFellNeverSwallowsALaterBounty()
    {
        var run = InFighting();
        run.Tick(Snap(attackers: 5, defenders: 20, dt: 60f));
        run.Tick(Snap(attackers: 5, defenders: 20, dt: 90f));
        // The engine reported only 3 of the 5 removals before the clear.
        run.AttackerDown("orc");
        run.AttackerDown("orc");
        run.AttackerDown("orc");
        run.Tick(Snap(attackers: 0, defenders: 20, dt: 1f));
        run.Tick(Snap(dt: 60f, defenders: 20));
        SpawnWave(run, defenders: 20);
        var before = run.Gold;

        run.AttackerDown("orc");

        Assert.AreEqual(before + 4, run.Gold);
    }

    [TestMethod]
    public void Stalemate_RunsOnlyOnceTheFirstBatchIsOut()
    {
        var run = Begun();
        run.TryStartWave();

        var fx = run.Tick(Snap(attackers: 55, dt: 500f));

        Assert.IsFalse(fx.OrderCharge);
    }

    // ---- Ticks and ended runs ----

    [TestMethod]
    [DataRow(float.NaN)]
    [DataRow(float.PositiveInfinity)]
    [DataRow(0f)]
    [DataRow(-1f)]
    public void Tick_BadDt_ReturnsEmptyEffectsAndMovesNothing(float dt)
    {
        var run = Begun();
        run.Tick(Snap(dt: 10f));
        var left = run.GetView().SecondsLeft;

        var fx = run.Tick(Snap(dt: dt));

        Assert.AreEqual(0, fx.DefenderBatches.Count + fx.AttackerBatches.Count + fx.Events.Count);
        Assert.IsNull(fx.StartWave);
        Assert.AreEqual(left, run.GetView().SecondsLeft);
        Assert.AreEqual(RunPhase.Preparation, run.Phase);
    }

    [TestMethod]
    public void Tick_BeforeBegin_DoesNothing()
    {
        var run = new SurvivalRun(Plan());

        var fx = run.Tick(Snap(dt: 100f, defenders: 0));

        Assert.IsNull(run.Result);
        Assert.AreEqual(0, fx.DefenderBatches.Count);
    }

    [TestMethod]
    public void Ended_EveryCallIsANoOp()
    {
        var run = InFighting();
        run.Abandon();
        var result = run.Result;
        var gold = run.Gold;

        var fx = run.Tick(Snap(attackers: 0, dt: 500f));
        run.AttackerDown("orc");
        run.DefenderDown();
        run.PlayerDown();
        run.Abandon();

        Assert.AreSame(result, run.Result);
        Assert.AreEqual(RunEndReason.Abandoned, result!.Reason);
        Assert.AreEqual(0, fx.AttackerBatches.Count);
        Assert.AreEqual(gold, run.Gold);
        Assert.AreEqual(StartResult.RunOver, run.TryStartWave());
        Assert.AreEqual(0, run.Stats.PlayerDeaths);
    }

    [TestMethod]
    public void Abandon_RecordsTheWaveAndKillsReached()
    {
        var run = InFighting();
        run.AttackerDown("orc");

        run.Abandon();

        Assert.AreEqual(1, run.Result!.Wave);
        Assert.AreEqual(0, run.Result.WavesCleared);
        Assert.AreEqual(1, run.Result.Kills);
    }

    [TestMethod]
    public void Counters_FleeingAndPlayerDeathsAreRecorded()
    {
        var run = InFighting();

        run.AttackerFled();
        run.PlayerDown();
        run.Tick(Snap(attackers: 50, fleeing: 7));

        Assert.AreEqual(1, run.Stats.Fled);
        Assert.AreEqual(1, run.Stats.PlayerDeaths);
        Assert.AreEqual(7, run.Stats.FleeingPeak);
    }

    [TestMethod]
    public void Garrison_ScaleShrinksTheStartingRows_NeverBelowOne()
    {
        var run = new SurvivalRun(Plan(options: new RunOptions { GarrisonScale = 0.2f }));

        var fx = run.Begin();

        Assert.AreEqual(2, fx.DefenderBatches.Single(b => b.Slot == FormationSlot.Ranged).Count);
        Assert.AreEqual(6, fx.DefenderBatches.Single(b => b.Slot == FormationSlot.Infantry).Count);
        var tiny = new SurvivalRun(Plan(options: new RunOptions { GarrisonScale = 0.001f }));
        Assert.AreEqual(1, tiny.Begin().DefenderBatches.Single(b => b.Slot == FormationSlot.Ranged).Count);
    }

    [TestMethod]
    public void Garrison_NaNScale_UsesTheFullGarrison()
    {
        var run = new SurvivalRun(Plan(options: new RunOptions { GarrisonScale = float.NaN }));

        var fx = run.Begin();

        Assert.AreEqual(10, fx.DefenderBatches.Single(b => b.Slot == FormationSlot.Ranged).Count);
    }

    // ---- Scripted run and the gold invariant ----

    private static SurvivalRun ClearedWaveOne()
    {
        var run = InFighting();
        run.Tick(Snap(attackers: 0));
        Assert.AreEqual(RunPhase.Intermission, run.Phase);
        return run;
    }

    [TestMethod]
    public void FiveWaveScript_GoldIsStartPlusBountyPlusBonusesMinusPurchases()
    {
        var run = Begun(Plan(options: new RunOptions { StopAfterWaves = 5 }));
        var expected = 600L;
        var wavePlan = Plan().Waves;
        var bounty = new Dictionary<string, int> { ["orc"] = 4, ["skirm"] = 6 };

        for (var wave = 1; wave <= 5; wave++)
        {
            if (wave % 2 == 0)
            {
                Assert.AreEqual(BuyResult.Ok, run.TryBuy("militia"));
                expected -= 180;
            }

            run.Tick(Snap(dt: 20f));
            run.TryStartWave();
            expected += run.LastHornBonus;
            var spawned = new List<string>();
            var standing = 0;
            RunEffects fx;
            do
            {
                fx = run.Tick(Snap(attackers: standing));
                var batch = fx.AttackerBatches.SelectMany(b => Enumerable.Repeat(b.TroopId, b.Count)).ToList();
                spawned.AddRange(batch);
                standing += batch.Count;
            }
            while (standing < wavePlan.ForWave(wave).Count);

            foreach (var troop in spawned)
            {
                run.AttackerDown(troop);
                expected += bounty[troop];
            }

            run.Tick(Snap(attackers: 0));
            expected += 150 + 50 * wave;
        }

        Assert.AreEqual(5, run.WavesCleared);
        Assert.AreEqual(RunEndReason.TargetReached, run.Result!.Reason);
        Assert.AreEqual(expected, run.Gold);
        Assert.AreEqual(wavePlan.Expand(1).Count + wavePlan.Expand(2).Count + wavePlan.Expand(3).Count
            + wavePlan.Expand(4).Count + wavePlan.Expand(5).Count, run.Stats.TotalKills);
    }

    // ---- Later waves, shop counts, rules 5, 6, 8, 11, 13 ----

    [TestMethod]
    public void WaveTwo_RestartsPlacementIndicesAndStatsAndTheCharge()
    {
        var run = InFighting();
        Assert.IsTrue(run.Tick(Snap(attackers: 5, defenders: 20, dt: 60f)).OrderCharge);
        Assert.AreEqual(5, run.Tick(Snap(attackers: 5, defenders: 20, dt: 90f)).FellAttackers);
        for (var i = 0; i < 5; i++)
            run.AttackerDown("orc");
        run.Tick(Snap(attackers: 0, defenders: 20, dt: 1f));
        Assert.AreEqual(5, run.Stats.Waves[0].Felled);

        Assert.AreEqual(StartResult.Ok, run.TryStartWave());
        var ticks = SpawnWave(run);
        var batches = ticks.SelectMany(t => t.AttackerBatches).ToList();

        Assert.AreEqual(2, ticks.Select(t => t.StartWave).Single(w => w != null)!.Wave);
        foreach (var slot in new[] { FormationSlot.Infantry, FormationSlot.Ranged })
        {
            var ofClass = batches.Where(b => b.Slot == slot).ToList();
            Assert.AreEqual(1, ofClass.Count(b => b.Place), slot + " is placed once in wave 2 as well");
            Assert.IsTrue(ofClass[0].Place);
            Assert.AreEqual(0, ofClass[0].FirstIndex, slot + " indices restart at 0");
            Assert.AreEqual(60, ofClass.Sum(b => b.Count));
        }

        var charge = run.Tick(Snap(attackers: 60, dt: 59f));
        Assert.IsTrue(charge.OrderCharge, "wave 1's charge does not carry into wave 2");
        run.Tick(Snap(attackers: 0, dt: 1f));

        var wave2 = run.Stats.Waves[1];
        Assert.AreEqual(0, wave2.Felled, "felled restarts per wave");
        Assert.AreEqual(ticks.Count + 59 + 1, wave2.Seconds, 0.01f, "seconds restart per wave");
    }

    [TestMethod]
    public void WaveStart_CappedFirstBatch_TotalsCoverOnlyTheBatch()
    {
        var run = Begun();
        run.TryStartWave();

        var ticks = SpawnWave(run, defenders: 40, battleSize: 141);

        var start = ticks.Select(t => t.StartWave).Single(w => w != null)!;
        Assert.AreEqual((74, 27), (start.Foot, start.Ranged));
        var batches = ticks.SelectMany(t => t.AttackerBatches).ToList();
        Assert.AreEqual(74, batches.Where(b => b.Slot == FormationSlot.Infantry).Max(b => b.BatchTotal));
        Assert.AreEqual(27, batches.Where(b => b.Slot == FormationSlot.Ranged).Max(b => b.BatchTotal));
        Assert.AreEqual(101, batches.Sum(b => b.Count));
    }

    [TestMethod]
    public void WaveStart_FirstBatchIsFixedAtTheStart_WhenDefendersChangeMidSpawn()
    {
        var run = Begun();
        run.TryStartWave();
        var standing = 0;
        var opening = 0;
        var done = false;
        for (var i = 0; i < 100 && !done; i++)
        {
            var fx = run.Tick(Snap(attackers: standing, defenders: i == 0 ? 40 : 20, battleSize: 141));
            var n = fx.AttackerBatches.Sum(b => b.Count);
            standing += n;
            opening += n;
            done = fx.RefreshAttackerTactic;
        }

        Assert.IsTrue(done);
        Assert.AreEqual(101, opening, "the opening is the 101 judged safe at the start, not a number recomputed per tick");
    }

    [TestMethod]
    public void Stalemate_AttackersIncludeTheQueue_SoABigWaveWaitsTheLongerLimit()
    {
        var plan = Plan(waveSpecs: new[] { new WaveSpec(300, new[] { T("orc", FormationSlot.Infantry, 1) }) });
        var run = InFighting(plan);

        // 150 standing and 150 queued is half the wave only if the queue is ignored.
        Assert.IsFalse(run.Tick(Snap(attackers: 150, dt: 60f)).OrderCharge);
        Assert.IsTrue(run.Tick(Snap(attackers: 150, dt: 120f)).OrderCharge);
    }

    [TestMethod]
    public void Stalemate_FellCountIsCappedByTheAttackersStanding()
    {
        var rules = new RunRules { LiveCap = 101, SpawnsPerTick = 1 };
        var plan = Plan(rules, waveSpecs: new[] { new WaveSpec(104, new[] { T("orc", FormationSlot.Infantry, 1) }) });
        var run = Begun(plan);
        run.TryStartWave();
        var standing = 0;
        for (var i = 0; i < 300; i++)
        {
            var fx = run.Tick(Snap(attackers: standing));
            standing += fx.AttackerBatches.Sum(b => b.Count);
            if (fx.RefreshAttackerTactic)
                break;
        }

        Assert.AreEqual(101, standing);
        Assert.IsTrue(run.Tick(Snap(attackers: 3, dt: 60f)).OrderCharge);
        var fell = run.Tick(Snap(attackers: 3, dt: 90f));

        Assert.AreEqual(3, fell.FellAttackers, "the queue still holds soldiers, but only 3 stand");
    }

    [TestMethod]
    public void Clear_WithoutACharge_DoesNotRestoreTheDefenderTactic()
    {
        var run = InFighting();

        var fx = run.Tick(Snap(attackers: 0, dt: 1f));

        Assert.AreEqual(1, run.WavesCleared);
        Assert.IsFalse(fx.RestoreDefenderTactic);
    }

    [TestMethod]
    public void Fighting_NoAttackersAndNoDefenders_EndsAsDefendersDead()
    {
        var run = InFighting();

        run.Tick(Snap(attackers: 0, defenders: 0, dt: 1f));

        Assert.AreEqual(RunEndReason.DefendersDead, run.Result!.Reason);
        Assert.AreEqual(0, run.WavesCleared);
    }

    [TestMethod]
    public void Preparation_NaNTimer_StartsTheWaveAtOnce()
    {
        var run = Begun(Plan(new RunRules { PreparationSeconds = float.NaN }));

        run.Tick(Snap(dt: 1f));

        Assert.AreEqual(RunPhase.Wave, run.Phase);
    }

    [TestMethod]
    public void View_CountsBoughtPacksPerClass()
    {
        var run = Begun();

        run.TryBuy("militia");
        run.TryBuy("militia");
        run.TryBuy("yeomen");

        var view = run.GetView();
        Assert.AreEqual((2, 1), (view.FootBought, view.RangedBought));
    }
}
