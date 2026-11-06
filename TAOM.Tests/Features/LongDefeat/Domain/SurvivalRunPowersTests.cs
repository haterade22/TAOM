using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.LongDefeat.Domain;

namespace TAOM.Tests.Features.LongDefeat.Domain;

/// <summary>
/// The Hammer of the Underworld and Winged Terror in the run (phase2-plan.md Slice 8): every wave start carries the engines' hit point factor (1 without the hammer, so the next wave
/// restores the vanilla value), and a Winged Terror wave pulses every <c>pulseSeconds</c> of its fighting, once per tick however long the tick, never in another wave.
/// </summary>
[TestClass]
public class SurvivalRunPowersTests
{
    private static readonly Dictionary<string, string> Names = AffixPlan.Ids.ToDictionary(id => id, id => "{=TAOM_LD_Test_" + id + "}Name of " + id);

    private static AffixPlan Affixes() => new(
        new[] { new AffixStage(5, 1) },
        new Dictionary<string, IReadOnlyDictionary<string, float>>
        {
            ["hammer"] = new Dictionary<string, float> { ["engineHitPointsPercent"] = 50f },
            ["terror"] = new Dictionary<string, float> { ["radius"] = 8f, ["pulseSeconds"] = 2f, ["amount"] = 3f, ["floor"] = 35f },
        },
        Names);

    private static RunPlan Plan(int startWave, ulong seed, int? stopAfter = null)
    {
        var small = new WaveSpec(8, new[] { new WaveTroop("orc", FormationSlot.Infantry, 1) });
        return new RunPlan(
            "helms",
            new[] { new GarrisonRow("fow_militia", FormationSlot.Infantry, 30) },
            new WavePlan(Enumerable.Repeat(small, 8).ToList(), new WaveScaling(0, 8)),
            new ShopPlan(600, new[] { new ShopOffer("militia", "Militia", "fow_militia", FormationSlot.Infantry, 12, 180) }),
            RunRules.Default with { PreparationSeconds = 1f, IntermissionSeconds = 1f },
            new RunOptions { StartWave = startWave, Seed = seed, StopAfterWaves = stopAfter })
        {
            Bounties = new Dictionary<string, int> { ["orc"] = 4 },
            Affixes = Affixes(),
        };
    }

    /// <summary>
    /// The first seed whose roll at <paramref name="wave"/> is exactly <paramref name="power"/> (the schedule gives wave 5 one power), and whose roll at the next wave is exactly
    /// <paramref name="nextIs"/> when one is named, or is not <paramref name="nextIsNot"/> when that is.
    /// </summary>
    private static ulong SeedRolling(string power, int wave, string? nextIs = null, string? nextIsNot = null)
    {
        for (ulong seed = 0; seed < 5000; seed++)
        {
            var plan = Affixes();
            var next = plan.Roll(seed, wave + 1);
            if (plan.Roll(seed, wave).SequenceEqual(new[] { power })
                && (nextIs == null || next.SequenceEqual(new[] { nextIs }))
                && (nextIsNot == null || !next.SequenceEqual(new[] { nextIsNot })))
                return seed;
        }

        Assert.Fail($"no seed rolls {power} at wave {wave}: is it in AffixPlan.Pool?");
        return 0;
    }

    private static RunSnapshot Standing(float dt = 1f) => new(dt, 50, 0, 40, 0, 0, 1000);

    private static SurvivalRun Begun(RunPlan plan)
    {
        var run = new SurvivalRun(plan);
        run.Begin();
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

    [TestMethod]
    public void AHammerWave_CarriesTheEngineFactor_OnItsStartEffect()
    {
        var run = Begun(Plan(startWave: 5, seed: SeedRolling("hammer", 5)));

        var fx = TickToWaveStart(run);

        CollectionAssert.AreEqual(new[] { "hammer" }, run.WaveAffixes.ToArray());
        Assert.AreEqual(1.5f, fx.EngineHitPoints!.Value, 1e-6f);
        Assert.IsNull(fx.PublishStats!.Attackers, "the hammer is no stat power");
    }

    [TestMethod]
    public void EveryOtherWaveStart_CarriesFactorOne_SoTheVanillaHitPointsComeBack()
    {
        var before = Begun(Plan(startWave: 4, seed: 1));
        var other = Begun(Plan(startWave: 5, seed: SeedRolling("terror", 5)));

        Assert.AreEqual(1f, TickToWaveStart(before).EngineHitPoints!.Value, "a wave before the first affix still restores");
        Assert.AreEqual(1f, TickToWaveStart(other).EngineHitPoints!.Value, "a terror wave is no hammer wave");
    }

    [TestMethod]
    public void TheWaveAfterAHammerWave_RestoresTheVanillaHitPoints()
    {
        var seed = SeedRolling("hammer", 5, nextIsNot: "hammer");
        var run = Begun(Plan(startWave: 5, seed: seed));
        Assert.AreEqual(1.5f, TickToWaveStart(run).EngineHitPoints!.Value, 1e-6f);
        for (var i = 0; i < 200 && run.GetView().Phase != RunPhase.Intermission; i++)
            run.Tick(new RunSnapshot(1f, 0, 0, 40, 0, 0, 1000));
        Assert.AreEqual(RunPhase.Intermission, run.GetView().Phase);

        run.TryStartWave();
        RunEffects? next = null;
        for (var i = 0; i < 20 && next?.StartWave == null; i++)
            next = run.Tick(Standing());

        Assert.AreEqual(6, next!.StartWave!.Wave);
        Assert.AreEqual(1f, next.EngineHitPoints!.Value);
    }

    [TestMethod]
    public void AWaveWithNoStartEffect_CarriesNoEngineWrite()
    {
        var run = Begun(Plan(startWave: 5, seed: SeedRolling("hammer", 5)));
        TickToWaveStart(run);

        var later = run.Tick(Standing());

        Assert.IsNull(later.EngineHitPoints, "the engines are written at the wave's start and nowhere else");
    }

    [TestMethod]
    public void ATerrorWave_PulsesEveryIntervalOfItsFighting_OncePerTick()
    {
        var run = Begun(Plan(startWave: 5, seed: SeedRolling("terror", 5)));
        var pulses = new List<int>();
        var start = TickToWaveStart(run);
        if (start.MoralePulse != null)
            pulses.Add(0);

        for (var tick = 1; tick <= 8; tick++)
        {
            var fx = run.Tick(Standing());
            if (fx.MoralePulse != null)
            {
                pulses.Add(tick);
                Assert.AreEqual(new LdMoralePulse(8f, 3f, 35f), fx.MoralePulse);
            }
        }

        CollectionAssert.AreEqual(new[] { 1, 3, 5, 7 }, pulses, "the first pulse comes one interval into the fight (the opening tick counts as the first second)");
    }

    [TestMethod]
    public void ALongTick_StillPulsesOnce()
    {
        var run = Begun(Plan(startWave: 5, seed: SeedRolling("terror", 5)));
        TickToWaveStart(run);

        var fx = run.Tick(Standing(dt: 30f));
        Assert.IsNotNull(fx.MoralePulse);
        var next = run.Tick(Standing(dt: 0.5f));

        Assert.IsNull(next.MoralePulse, "thirty seconds of one tick owe no queue of pulses");
    }

    [TestMethod]
    public void ANonTerrorWave_NeverPulses()
    {
        var run = Begun(Plan(startWave: 5, seed: SeedRolling("hammer", 5)));
        TickToWaveStart(run);

        for (var tick = 0; tick < 10; tick++)
            Assert.IsNull(run.Tick(Standing()).MoralePulse);
    }

    [TestMethod]
    public void TheTerrorClock_RestartsWithEachWave()
    {
        var run = Begun(Plan(startWave: 5, seed: SeedRolling("terror", 5, nextIs: "terror")));
        TickToWaveStart(run);
        for (var i = 0; i < 200 && run.GetView().Phase != RunPhase.Intermission; i++)
            run.Tick(new RunSnapshot(1f, 0, 0, 40, 0, 0, 1000));
        Assert.AreEqual(RunPhase.Intermission, run.GetView().Phase);

        run.TryStartWave();
        var starting = TickToWaveStart(run);
        var next = run.Tick(Standing());

        Assert.IsNull(starting.MoralePulse, "a second of wave 6 is no interval: the clock of wave 5 does not carry over");
        Assert.IsNotNull(next.MoralePulse);
    }

    [TestMethod]
    public void ATerrorWave_PulsesNotAtTheTickThatClearsIt()
    {
        var run = Begun(Plan(startWave: 5, seed: SeedRolling("terror", 5)));
        TickToWaveStart(run);

        var clearing = run.Tick(new RunSnapshot(2f, 0, 0, 40, 0, 0, 1000));

        Assert.AreEqual(RunPhase.Intermission, run.GetView().Phase);
        Assert.IsNull(clearing.MoralePulse, "nobody stands to be feared");
    }
}
