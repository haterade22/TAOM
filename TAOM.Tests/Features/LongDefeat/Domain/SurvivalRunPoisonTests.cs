using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.FordsOfWar.Domain;
using TAOM.Features.LongDefeat.Domain;

namespace TAOM.Tests.Features.LongDefeat.Domain;

/// <summary>
/// Morgul Blades in the run (phase2-plan.md Slice 9): a dose lands only in a blades wave, doses are counted apart from hits, the owner of a dose is the hitter, a key the registry forgot drops the hit,
/// and a wave's end takes its doses with it.
/// </summary>
[TestClass]
public class SurvivalRunPoisonTests
{
    private static readonly AgentKey Victim = new(1);
    private static readonly AgentKey Other = new(2);
    private static readonly AgentKey Poisoner = new(100);

    private static readonly Dictionary<string, string> Names = AffixPlan.Ids.ToDictionary(id => id, id => "{=TAOM_LD_Test_" + id + "}Name of " + id);

    private static AffixPlan Affixes() => new(
        new[] { new AffixStage(5, 1) },
        new Dictionary<string, IReadOnlyDictionary<string, float>>
        {
            ["blades"] = new Dictionary<string, float> { ["poisonPerSecond"] = 2f, ["poisonSeconds"] = 6f },
            ["hammer"] = new Dictionary<string, float> { ["engineHitPointsPercent"] = 50f },
        },
        Names);

    private static RunPlan Plan(int startWave, ulong seed)
    {
        var small = new WaveSpec(8, new[] { new WaveTroop("orc", FormationSlot.Infantry, 1) });
        return new RunPlan(
            "helms",
            new[] { new GarrisonRow("fow_militia", FormationSlot.Infantry, 30) },
            new WavePlan(Enumerable.Repeat(small, 8).ToList(), new WaveScaling(0, 8)),
            new ShopPlan(600, new[] { new ShopOffer("militia", "Militia", "fow_militia", FormationSlot.Infantry, 12, 180) }),
            RunRules.Default with { PreparationSeconds = 1f, IntermissionSeconds = 1f },
            new RunOptions { StartWave = startWave, Seed = seed })
        {
            Bounties = new Dictionary<string, int> { ["orc"] = 4 },
            Affixes = Affixes(),
        };
    }

    private static ulong SeedRolling(string power, int wave)
    {
        for (ulong seed = 0; seed < 5000; seed++)
        {
            if (Affixes().Roll(seed, wave).SequenceEqual(new[] { power }))
                return seed;
        }

        Assert.Fail($"no seed rolls {power} at wave {wave}: is it in AffixPlan.Pool?");
        return 0;
    }

    private static RunSnapshot Standing(float dt = 1f) => new(dt, 50, 0, 40, 0, 0, 1000);

    private static SurvivalRun InABladesWave()
    {
        var run = new SurvivalRun(Plan(5, SeedRolling("blades", 5)));
        run.Begin();
        TickToWaveStart(run);
        return run;
    }

    private static RunEffects TickToWaveStart(SurvivalRun run)
    {
        for (var i = 0; i < 20; i++)
        {
            var fx = run.Tick(Standing());
            if (fx.StartWave != null)
                return fx;
        }

        Assert.Fail("the wave never started");
        return new RunEffects();
    }

    private static void ClearTheWave(SurvivalRun run)
    {
        for (var i = 0; i < 200 && run.GetView().Phase != RunPhase.Intermission; i++)
            run.Tick(new RunSnapshot(1f, 0, 0, 40, 0, 0, 1000));
        Assert.AreEqual(RunPhase.Intermission, run.GetView().Phase);
    }

    [TestMethod]
    public void ABladesWave_TakesADoseFromAHit_AndTicksItToItsOwner()
    {
        var run = InABladesWave();

        run.DefenderHit(Victim, Poisoner);
        var fx = run.Tick(Standing());

        Assert.IsTrue(run.PoisonWave);
        Assert.AreEqual(1, fx.PoisonTicks.Count);
        Assert.AreEqual(new PoisonTick(Victim, Poisoner, 2), fx.PoisonTicks[0], "2 per second for the second the tick covered, to the hitter");
        Assert.AreEqual((1, 1), (run.Stats.Poison.Doses, run.Stats.Poison.Hits));
    }

    [TestMethod]
    public void AWaveWithoutBlades_TakesNoDose()
    {
        var run = new SurvivalRun(Plan(5, SeedRolling("hammer", 5)));
        run.Begin();
        TickToWaveStart(run);

        run.DefenderHit(Victim, Poisoner);
        var fx = run.Tick(Standing());

        Assert.IsFalse(run.PoisonWave);
        Assert.AreEqual(0, fx.PoisonTicks.Count);
        Assert.AreEqual((0, 0), (run.Stats.Poison.Doses, run.Stats.Poison.Hits));
    }

    [TestMethod]
    public void AWaveBeforeTheFirstAffix_TakesNoDose()
    {
        var run = new SurvivalRun(Plan(2, 1));
        run.Begin();
        TickToWaveStart(run);

        run.DefenderHit(Victim, Poisoner);

        Assert.AreEqual(0, run.Tick(Standing()).PoisonTicks.Count);
        Assert.AreEqual(0, run.Stats.Poison.Hits);
    }

    [TestMethod]
    public void BeforeTheFirstWaveStarts_ADefenderHitIsIgnored()
    {
        var run = new SurvivalRun(Plan(5, SeedRolling("blades", 5)));
        run.Begin();

        run.DefenderHit(Victim, Poisoner);

        Assert.AreEqual(0, run.Stats.Poison.Hits);
    }

    [TestMethod]
    public void DosesAreCountedApartFromHits()
    {
        var run = InABladesWave();

        run.DefenderHit(Victim, Poisoner);
        run.DefenderHit(Victim, Poisoner);
        run.DefenderHit(Victim, new AgentKey(101));
        run.DefenderHit(Other, Poisoner);

        Assert.AreEqual(2, run.Stats.Poison.Doses, "two victims");
        Assert.AreEqual(4, run.Stats.Poison.Hits, "every hit, a refresh included");
    }

    [TestMethod]
    public void AForgottenKey_DropsTheHit_AndTakesTheDose()
    {
        var run = InABladesWave();
        run.DefenderHit(Victim, Poisoner);

        run.Forget(Victim);
        run.DefenderHit(Victim, Poisoner);
        run.Forget(Poisoner);
        run.DefenderHit(Other, Poisoner);

        Assert.AreEqual(0, run.Tick(Standing()).PoisonTicks.Count, "the victim's dose went with him");
        Assert.AreEqual((1, 1), (run.Stats.Poison.Doses, run.Stats.Poison.Hits), "only the first hit was taken");
    }

    [TestMethod]
    public void TheWaveClear_TakesTheDosesWithIt_AndStopsTakingHits()
    {
        var run = InABladesWave();
        run.DefenderHit(Victim, Poisoner);

        ClearTheWave(run);
        run.DefenderHit(Other, Poisoner);

        Assert.IsFalse(run.PoisonWave, "the clear takes the wave's doses with it");
        Assert.AreEqual(1, run.Stats.Poison.Hits, "a hit between waves is not taken");
    }

    [TestMethod]
    public void TheNextWave_StartsWithNoDoses()
    {
        var run = InABladesWave();
        run.DefenderHit(Victim, Poisoner);
        ClearTheWave(run);

        run.TryStartWave();
        TickToWaveStart(run);

        Assert.AreEqual(0, run.Tick(Standing()).PoisonTicks.Count);
    }

    [TestMethod]
    public void WhatTheAdapterAppliedIsAddedToTheRunAndTheReport()
    {
        var run = InABladesWave();

        run.PoisonApplied(damage: 5, ownerMissing: false, withheld: false);
        run.PoisonApplied(damage: 0, ownerMissing: false, withheld: true);
        run.PoisonApplied(damage: 3, ownerMissing: true, withheld: false);

        var poison = run.Stats.Poison;
        Assert.AreEqual((8, 1, 1), (poison.Damage, poison.OwnerMissing, poison.Withheld));
    }

    [TestMethod]
    public void ATallySince_IsWhatCameAfterTheCopy()
    {
        var run = InABladesWave();
        run.DefenderHit(Victim, Poisoner);
        var before = run.Stats.Poison.Copy();

        run.DefenderHit(Other, Poisoner);
        run.PoisonApplied(4, false, false);
        var since = run.Stats.Poison.Since(before);

        Assert.AreEqual((1, 1, 4), (since.Doses, since.Hits, since.Damage));
    }

    [TestMethod]
    public void ABadDeltaTime_TicksNothing()
    {
        var run = InABladesWave();
        run.DefenderHit(Victim, Poisoner);

        Assert.AreEqual(0, run.Tick(new RunSnapshot(0f, 50, 0, 40, 0, 0, 1000)).PoisonTicks.Count);
        Assert.AreEqual(1, run.Tick(Standing()).PoisonTicks.Count, "the dose waits for a real tick");
    }
}
