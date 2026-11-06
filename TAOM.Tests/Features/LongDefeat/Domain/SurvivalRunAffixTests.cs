using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.LongDefeat.Domain;

namespace TAOM.Tests.Features.LongDefeat.Domain;

/// <summary>
/// The run's affix state: the roll comes from the seed, the wave carries it from its start, the stat record rides the wave's start effect, and
/// the HUD's preview reads the same roll the wave will carry (phase2-plan.md section 2 "Affixes", and the Slice 2 note on the Slice 3 row).
/// </summary>
[TestClass]
public class SurvivalRunAffixTests
{
    private static readonly Dictionary<string, string> Names = AffixPlan.Ids.ToDictionary(id => id, id => "{=TAOM_LD_Test_" + id + "}Name of " + id);

    private static AffixPlan Affixes() => new(
        new[] { new AffixStage(5, 1), new AffixStage(10, 2), new AffixStage(15, 3) },
        new Dictionary<string, IReadOnlyDictionary<string, float>>
        {
            ["lash"] = new Dictionary<string, float> { ["swingSpeedPercent"] = 15f, ["attackEagernessPercent"] = 25f },
            ["drums"] = new Dictionary<string, float> { ["moveSpeedPercent"] = 12f, ["accelerationPercent"] = 25f },
            ["hide"] = new Dictionary<string, float> { ["armourPercent"] = 30f },
        },
        Names);

    private static RunPlan Plan(int startWave, ulong seed, int? stopAfter = null, AffixPlan? affixes = null, EndlessScale? endless = null)
    {
        var waves = new WavePlan(
            new[] { new WaveSpec(110, new[] { new WaveTroop("orc", FormationSlot.Infantry, 3), new WaveTroop("skirm", FormationSlot.Ranged, 1) }) },
            new WaveScaling(10, 300),
            endless);
        return new RunPlan(
            "helms",
            new[] { new GarrisonRow("fow_militia", FormationSlot.Infantry, 30) },
            waves,
            new ShopPlan(600, new[] { new ShopOffer("militia", "Militia", "fow_militia", FormationSlot.Infantry, 12, 180) }),
            RunRules.Default with { PreparationSeconds = 1f, IntermissionSeconds = 1f },
            new RunOptions { StartWave = startWave, Seed = seed, StopAfterWaves = stopAfter })
        {
            Bounties = new Dictionary<string, int> { ["orc"] = 4, ["skirm"] = 6 },
            Affixes = affixes ?? Affixes(),
        };
    }

    private static RunEffects TickToWaveStart(SurvivalRun run)
    {
        for (var i = 0; i < 20; i++)
        {
            var fx = run.Tick(new RunSnapshot(1f, 0, 0, 40, 0, 0, 1000));
            if (fx.StartWave != null)
                return fx;
        }

        Assert.Fail("the wave never started");
        return new RunEffects();
    }

    private static SurvivalRun Begun(RunPlan plan)
    {
        var run = new SurvivalRun(plan);
        run.Begin();
        return run;
    }

    [TestMethod]
    public void AWaveBeforeTheFirstScheduledOne_CarriesNoAffix_AndPublishesNoRecord()
    {
        var run = Begun(Plan(startWave: 4, seed: 1));

        var fx = TickToWaveStart(run);

        Assert.AreEqual(0, run.WaveAffixes.Count);
        Assert.IsNotNull(fx.PublishStats);
        Assert.IsNull(fx.PublishStats!.Attackers);
        Assert.AreEqual(0f, fx.PublishStats.AttackerArmourPercent);
    }

    [TestMethod]
    public void WaveFive_CarriesTheSeedsRoll_AndItsArmourRidesTheStartEffect()
    {
        var run = Begun(Plan(startWave: 5, seed: 1));

        var fx = TickToWaveStart(run);

        CollectionAssert.AreEqual(new[] { "hide" }, run.WaveAffixes.ToArray());
        Assert.AreEqual(5, fx.StartWave!.Wave);
        Assert.AreEqual(30f, fx.PublishStats!.AttackerArmourPercent, "the stat record is published with StartWave, before any batch");
    }

    [TestMethod]
    public void AStatPowersRecord_RidesTheStartEffect()
    {
        var run = Begun(Plan(startWave: 5, seed: 2));

        var fx = TickToWaveStart(run);

        CollectionAssert.AreEqual(new[] { "lash" }, run.WaveAffixes.ToArray());
        Assert.AreEqual(15f, fx.PublishStats!.Attackers!.SwingSpeedPercent);
        Assert.AreEqual(25f, fx.PublishStats.Attackers.AttackEagernessPercent);
    }

    [TestMethod]
    public void TheFirstBatchOfTheWave_AndItsPublishedStats_ComeInOneEffect()
    {
        // The director applies PublishStats before any batch of the same RunEffects (Apply order), so the first batch of a hide wave is armoured.
        var run = Begun(Plan(startWave: 5, seed: 1));
        RunEffects? first = null;
        for (var i = 0; i < 20 && first == null; i++)
        {
            var fx = run.Tick(new RunSnapshot(1f, 0, 0, 40, 0, 0, 1000));
            if (fx.StartWave != null)
                first = fx;
        }

        Assert.IsNotNull(first);
        Assert.IsTrue(first!.AttackerBatches.Count > 0, "the wave's first batch arrives in the same effect as its stats");
        Assert.IsNotNull(first.PublishStats);
    }

    [TestMethod]
    public void NextAffixes_BeforeAWave_IsTheRollTheWaveWillCarry()
    {
        var run = Begun(Plan(startWave: 5, seed: 1));

        var preview = run.NextAffixes.ToArray();
        TickToWaveStart(run);

        CollectionAssert.AreEqual(new[] { "hide" }, preview);
        CollectionAssert.AreEqual(preview, run.WaveAffixes.ToArray());
    }

    [TestMethod]
    public void NextAffixes_AfterAClear_IsTheNextWavesRoll()
    {
        var run = Begun(Plan(startWave: 9, seed: 1));
        TickToWaveStart(run);
        Assert.AreEqual(9, run.Wave);

        // wave 10 holds two affixes: the preview names them while wave 9 is still running.
        CollectionAssert.AreEqual(new[] { "drums", "terror" }, run.NextAffixes.ToArray());
    }

    [TestMethod]
    public void TheView_NamesTheNextWavesAffixes_BetweenWaves_AndTheCurrentOnesInAWave()
    {
        var run = Begun(Plan(startWave: 5, seed: 1));

        var prep = run.GetView();
        Assert.AreEqual("hide", prep.NextAffixes.Single().Id);
        Assert.AreEqual("{=TAOM_LD_Test_hide}Name of hide", prep.NextAffixes.Single().Name);
        Assert.AreEqual(0, prep.WaveAffixes.Count, "no wave is running yet");

        TickToWaveStart(run);
        var wave = run.GetView();
        Assert.AreEqual("hide", wave.WaveAffixes.Single().Id);
        Assert.AreEqual(0, wave.NextAffixes.Count, "the next wave's preview is built only between waves");
        Assert.AreEqual(1f, wave.NextHealthFactor);
        Assert.AreEqual(1f, wave.NextDamageFactor);
    }

    [TestMethod]
    public void TheView_FallsBackToThePowerId_WhenTheTableHasNoName()
    {
        var plan = new AffixPlan(new[] { new AffixStage(5, 1) }, new Dictionary<string, IReadOnlyDictionary<string, float>>(), new Dictionary<string, string>());
        var run = Begun(Plan(startWave: 5, seed: 1, affixes: plan));

        Assert.AreEqual("hide", run.GetView().NextAffixes.Single().Name);
    }

    [TestMethod]
    public void TheView_CarriesTheNextWavesFactors_FromTheWavePlan()
    {
        var growth = new EndlessScale(0.05f, 0.03f, 3f, 3, new Dictionary<string, string>());
        var run = Begun(Plan(startWave: 3, seed: 1, endless: growth));

        var view = run.GetView();

        // wave 3 is two waves past the one authored wave: 1 + 0.05 * 2 and 1 + 0.03 * 2.
        Assert.AreEqual(1.10f, view.NextHealthFactor, 1e-5f);
        Assert.AreEqual(1.06f, view.NextDamageFactor, 1e-5f);
    }

    [TestMethod]
    public void TheView_NextFactorsAreOne_InsideTheAuthoredList()
    {
        var growth = new EndlessScale(0.05f, 0.03f, 3f, 3, new Dictionary<string, string>());
        var run = Begun(Plan(startWave: 1, seed: 1, endless: growth));

        var view = run.GetView();

        Assert.AreEqual(1f, view.NextHealthFactor);
        Assert.AreEqual(1f, view.NextDamageFactor);
    }

    [TestMethod]
    public void AClearedWave_RecordsItsAffixesInTheStats()
    {
        var run = Begun(Plan(startWave: 5, seed: 1, stopAfter: 1));
        TickToWaveStart(run);
        for (var i = 0; i < 400 && run.Result == null; i++)
            run.Tick(new RunSnapshot(1f, 0, 0, 40, 0, 0, 1000));

        Assert.IsNotNull(run.Result);
        CollectionAssert.AreEqual(new[] { "hide" }, run.Stats.Waves.Single().Affixes.ToArray());
    }

    [TestMethod]
    public void TheHorn_NamesTheNewWavesAffixesAtOnce_NotThePreviousWavesList()
    {
        var run = Begun(Plan(startWave: 5, seed: 65));
        TickToWaveStart(run);
        Assert.AreEqual("hide", run.GetView().WaveAffixes.Single().Id);
        for (var i = 0; i < 400 && run.GetView().Phase != RunPhase.Intermission; i++)
            run.Tick(new RunSnapshot(1f, 0, 0, 40, 0, 0, 1000));
        Assert.AreEqual(RunPhase.Intermission, run.GetView().Phase, "the cleared wave leads to the intermission");

        run.TryStartWave();
        var view = run.GetView();

        Assert.AreEqual(RunPhase.Wave, view.Phase);
        Assert.AreEqual("drums", view.WaveAffixes.Single().Id, "seed 65 rolls hide at wave 5 and drums at wave 6, and the roll is taken when the horn sounds");
        CollectionAssert.AreEqual(new[] { "drums" }, run.WaveAffixes.ToArray());
    }

    [TestMethod]
    public void NoSeed_RollsFromSeedZero_SoATestPlanStillRuns()
    {
        var plan = Plan(startWave: 15, seed: 0) with { Options = new RunOptions { StartWave = 15 } };
        var run = Begun(plan);

        CollectionAssert.AreEqual(new[] { "lash", "drums", "discipline" }, run.NextAffixes.ToArray());
    }
}
