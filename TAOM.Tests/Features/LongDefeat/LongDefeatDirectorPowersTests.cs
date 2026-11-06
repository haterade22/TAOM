using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Adapters;
using TAOM.Features.AdvancedCombat;
using TAOM.Features.LongDefeat;
using TAOM.Features.LongDefeat.Diagnostics;
using TAOM.Features.LongDefeat.Domain;
using TAOM.Tests.Features.LoadTimeStamps;

namespace TAOM.Tests.Features.LongDefeat;

/// <summary>
/// The Hammer of the Underworld and Winged Terror at the director (phase2-plan.md Slice 8): the engines' hit points are written at each wave start before any attacker spawns (factor 1
/// restores the vanilla value), a hammer wave proves the write and logs the gate's state, a terror wave pulses the defenders' morale and writes its check from the pulses' own counts, and
/// a wave whose only power has no stat effect reads no soldier.
/// </summary>
[TestClass]
public class LongDefeatDirectorPowersTests
{
    private static readonly Dictionary<string, string> Names = AffixPlan.Ids.ToDictionary(id => id, id => "{=TAOM_LD_Test_" + id + "}Name of " + id);

    private static AffixPlan Affixes() => new(
        new[] { new AffixStage(5, 1), new AffixStage(10, 2), new AffixStage(15, 3) },
        new Dictionary<string, IReadOnlyDictionary<string, float>>
        {
            ["lash"] = new Dictionary<string, float> { ["swingSpeedPercent"] = 15f, ["attackEagernessPercent"] = 25f },
            ["hammer"] = new Dictionary<string, float> { ["engineHitPointsPercent"] = 50f },
            ["terror"] = new Dictionary<string, float> { ["radius"] = 8f, ["pulseSeconds"] = 2f, ["amount"] = 3f, ["floor"] = 35f },
        },
        Names);

    /// <summary>The first seed whose roll at <paramref name="wave"/> is exactly these powers (in any order), and, when named, whose next wave does not roll <paramref name="notNext"/>.</summary>
    private static ulong SeedRolling(int wave, string[] powers, string? notNext = null)
    {
        var plan = Affixes();
        for (ulong seed = 0; seed < 5000; seed++)
        {
            var roll = plan.Roll(seed, wave);
            if (roll.Count == powers.Length && !powers.Except(roll).Any() && (notNext == null || !plan.Roll(seed, wave + 1).Contains(notNext)))
                return seed;
        }

        Assert.Fail($"no seed rolls {string.Join("+", powers)} at wave {wave}: is each in AffixPlan.Pool?");
        return 0;
    }

    private sealed class Rig
    {
        public FakeLongDefeatField Field { get; } = new();
        public FakeLongDefeatEffects Effects { get; }
        public RecordingLogger Log { get; } = new();
        public RecordingOutcomeLog Outcomes { get; } = new();
        public LongDefeatDirector Director { get; }

        public Rig(int startWave, ulong seed, bool withEffects = true)
        {
            Effects = new FakeLongDefeatEffects(Field.Calls);
            var waves = new WavePlan(
                new[] { new WaveSpec(110, new[] { new WaveTroop("orc", FormationSlot.Infantry, 75), new WaveTroop("skirm", FormationSlot.Ranged, 25) }) },
                new WaveScaling(10, 300));
            var plan = new RunPlan(
                "helms",
                new[] { new GarrisonRow("fow_militia", FormationSlot.Infantry, 30) },
                waves,
                new ShopPlan(600, new[] { new ShopOffer("militia", "Militia", "fow_militia", FormationSlot.Infantry, 12, 180) }),
                RunRules.Default with { PreparationSeconds = 1f, IntermissionSeconds = 1f },
                new RunOptions { StartWave = startWave, Seed = seed })
            {
                Bounties = new Dictionary<string, int> { ["orc"] = 4, ["skirm"] = 6 },
                Affixes = Affixes(),
            };
            Director = new LongDefeatDirector(plan, Field, new LongDefeatSession(), Log, Outcomes, "m1", null, withEffects ? Effects : null);
        }

        public void StartAndSpawnWave()
        {
            Assert.IsTrue(Director.Start());
            PumpUntil(() => Field.Calls.Contains("Refresh"));
        }

        public void PumpUntil(Func<bool> done, int limit = 300, bool empty = false)
        {
            for (var i = 0; i < limit && !done(); i++)
            {
                if (empty)
                    Field.AttackersStanding = 0;
                Director.Tick(1f);
            }

            Assert.IsTrue(done(), "the condition never came true");
        }

        public void ClearTheWave() =>
            PumpUntil(() => Outcomes.Has("pass", LongDefeatChecks.WaveId) || Outcomes.Has("fail", LongDefeatChecks.WaveId), empty: true);

        public int FirstCall(string prefix) => Field.Calls.FindIndex(c => c.StartsWith(prefix, StringComparison.Ordinal));
    }

    [TestInitialize]
    public void Init() => MissionThreadGuard.ResetForTests();

    [TestCleanup]
    public void Clean() => MissionThreadGuard.ResetForTests();

    // ---- hammer ----

    [TestMethod]
    public void AHammerWave_WritesTheEngines_AfterThePublish_AndBeforeTheFirstAttackerSpawns()
    {
        var rig = new Rig(5, SeedRolling(5, new[] { "hammer" }));

        rig.StartAndSpawnWave();

        var publish = rig.FirstCall("Publish:");
        var engines = rig.Field.Calls.IndexOf("Engines:1.50");
        var firstAttacker = rig.FirstCall("Spawn:att");
        Assert.IsTrue(publish >= 0 && engines > publish && firstAttacker > engines, string.Join(",", rig.Field.Calls));
        CollectionAssert.AreEqual(new[] { 1.5f }, rig.Effects.EngineFactors.ToArray());
    }

    [TestMethod]
    public void EveryWaveStart_WritesTheEngines_SoAWaveWithNoHammerRestoresTheVanillaValue()
    {
        var rig = new Rig(4, seed: 1);

        rig.StartAndSpawnWave();

        CollectionAssert.AreEqual(new[] { 1f }, rig.Effects.EngineFactors.ToArray());
        Assert.AreEqual(0, rig.Outcomes.Of(LongDefeatChecks.AffixEffectId).Count, "no hammer, no hammer check");
    }

    [TestMethod]
    public void TheWaveAfterAHammerWave_WritesFactorOneAgain()
    {
        var rig = new Rig(5, SeedRolling(5, new[] { "hammer" }, notNext: "hammer"));
        rig.StartAndSpawnWave();
        rig.ClearTheWave();

        rig.PumpUntil(() => rig.Effects.EngineFactors.Count == 2);

        CollectionAssert.AreEqual(new[] { 1.5f, 1f }, rig.Effects.EngineFactors.ToArray());
    }

    [TestMethod]
    public void AHammerWave_ProvesTheWrite_LogsTheGate_AndReadsNoSoldier()
    {
        var rig = new Rig(5, SeedRolling(5, new[] { "hammer" }));

        rig.StartAndSpawnWave();

        var line = rig.Outcomes.Of(LongDefeatChecks.AffixEffectId).Single();
        Assert.IsTrue(rig.Outcomes.Has("pass", LongDefeatChecks.AffixEffectId), line);
        StringAssert.StartsWith(line, "hammer power=hammer ");
        StringAssert.Contains(line, "gate=standing");
        Assert.AreEqual(0, rig.Effects.StatsAsked.Count, "a power with no stat expectation reads nothing");
        Assert.IsTrue(rig.Log.Lines.Any(l => l.Contains("[Hammer]") && l.Contains("x1.50") && l.Contains("gate")), string.Join("\n", rig.Log.Lines.Where(l => l.Contains("Hammer"))));
    }

    [TestMethod]
    public void AHammerWave_Fails_WhenAnEngineKeptItsVanillaHitPoints()
    {
        var rig = new Rig(5, SeedRolling(5, new[] { "hammer" }));
        rig.Effects.EnginesFor = _ => new LdEngineReport(new[] { new LdEngineReading("ram", 3000f, 3000f, 3000f) }, 1, 0);

        rig.StartAndSpawnWave();

        Assert.IsTrue(rig.Outcomes.Has("fail", LongDefeatChecks.AffixEffectId), string.Join("\n", rig.Outcomes.Lines));
    }

    [TestMethod]
    public void AHammerWave_WithNoLiveEngine_IsASkip()
    {
        var rig = new Rig(5, SeedRolling(5, new[] { "hammer" }));
        rig.Effects.EnginesFor = _ => LdEngineReport.None;

        rig.StartAndSpawnWave();

        Assert.IsTrue(rig.Outcomes.Has("skip", LongDefeatChecks.AffixEffectId));
        StringAssert.Contains(rig.Outcomes.Of(LongDefeatChecks.AffixEffectId).Single(), "reason=no-engine");
    }

    [TestMethod]
    public void AHammerAndAStatPowerWave_ReadsTheStatPowerOnly_AndJudgesBoth()
    {
        var rig = new Rig(10, SeedRolling(10, new[] { "hammer", "lash" }));
        rig.Effects.StatsFor = stats => new LdStatSample("orc", stats.Select(s => new LdStatReading(s, 1f, s == "AIAttackOnDecideChance" ? 1.25f : 1.15f)).ToList());

        rig.StartAndSpawnWave();

        CollectionAssert.AreEquivalent(new[] { "SwingSpeedMultiplier", "AIAttackOnDecideChance" }, rig.Effects.StatsAsked.Single().ToArray());
        var lines = rig.Outcomes.Of(LongDefeatChecks.AffixEffectId);
        Assert.AreEqual(2, lines.Count, string.Join("\n", lines));
        Assert.IsTrue(lines.Any(l => l.StartsWith("hammer power=hammer ", StringComparison.Ordinal)));
        Assert.IsTrue(lines.Any(l => l.StartsWith("lash power=lash ", StringComparison.Ordinal)));
    }

    [TestMethod]
    public void ADirectorWithNoEffectsAdapter_WritesNoEngineAndNoHammerCheck()
    {
        var rig = new Rig(5, SeedRolling(5, new[] { "hammer" }), withEffects: false);

        rig.StartAndSpawnWave();

        Assert.AreEqual(0, rig.Effects.EngineFactors.Count);
        Assert.AreEqual(0, rig.Outcomes.Of(LongDefeatChecks.AffixEffectId).Count);
    }

    // ---- terror ----

    [TestMethod]
    public void ATerrorWave_PulsesAfterTheAttackersHaveSpawned_WithThePowersNumbers()
    {
        var rig = new Rig(5, SeedRolling(5, new[] { "terror" }));
        rig.StartAndSpawnWave();

        rig.PumpUntil(() => rig.Effects.Pulses.Count >= 3);

        Assert.IsTrue(rig.Effects.Pulses.All(p => p.Equals(new LdMoralePulse(8f, 3f, 35f))));
        Assert.IsTrue(rig.FirstCall("Pulse") > rig.FirstCall("Spawn:att"), "no attacker, no fear");
    }

    [TestMethod]
    public void EachPulseLine_NamesTheDefendersDrained_AndTheTimeItTook()
    {
        var rig = new Rig(5, SeedRolling(5, new[] { "terror" }));
        rig.Effects.PulseFor = () => new LdPulseResult(200, 30, 90f, 0, 1.25);
        rig.StartAndSpawnWave();

        rig.PumpUntil(() => rig.Effects.Pulses.Count >= 1);

        Assert.IsTrue(rig.Log.Lines.Any(l => l.Contains("[LongDefeat][Terror] pulse n=30 ms=1.25")), string.Join("\n", rig.Log.Lines.Where(l => l.Contains("Terror"))));
    }

    [TestMethod]
    public void ATerrorWave_WritesItsCheck_WhenTheWaveClears_FromThePulsesOwnCounts()
    {
        var rig = new Rig(5, SeedRolling(5, new[] { "terror" }));
        rig.StartAndSpawnWave();
        rig.PumpUntil(() => rig.Effects.Pulses.Count >= 2);

        rig.ClearTheWave();

        var line = rig.Outcomes.Of(LongDefeatChecks.AffixEffectId).Single();
        Assert.IsTrue(rig.Outcomes.Has("pass", LongDefeatChecks.AffixEffectId), line);
        StringAssert.StartsWith(line, "terror power=terror w=5 ");
        StringAssert.Contains(line, "pushed_below=0");
        Assert.AreEqual(0, rig.Effects.StatsAsked.Count, "terror reads no soldier either");
    }

    [TestMethod]
    public void ATerrorWave_Fails_WhenAPulseLeftSomeoneBelowTheFloor()
    {
        var rig = new Rig(5, SeedRolling(5, new[] { "terror" }));
        rig.Effects.PulseFor = () => new LdPulseResult(200, 30, 90f, 2, 0.5);
        rig.StartAndSpawnWave();
        rig.PumpUntil(() => rig.Effects.Pulses.Count >= 1);

        rig.ClearTheWave();

        Assert.IsTrue(rig.Outcomes.Has("fail", LongDefeatChecks.AffixEffectId), string.Join("\n", rig.Outcomes.Lines));
    }

    [TestMethod]
    public void ATerrorWave_Fails_WhenAPulseTookTheWholeFrame()
    {
        var rig = new Rig(5, SeedRolling(5, new[] { "terror" }));
        rig.Effects.PulseFor = () => new LdPulseResult(200, 30, 90f, 0, 16.5);
        rig.StartAndSpawnWave();
        rig.PumpUntil(() => rig.Effects.Pulses.Count >= 1);

        rig.ClearTheWave();

        Assert.IsTrue(rig.Outcomes.Has("fail", LongDefeatChecks.AffixEffectId));
        Assert.IsTrue(rig.Log.Lines.Any(l => l.Contains("[Terror]") && l.Contains("ms=16.50")), "a slow pulse is always logged");
    }

    [TestMethod]
    public void TheRunEndingInATerrorWave_StillWritesTheCheck()
    {
        var rig = new Rig(5, SeedRolling(5, new[] { "terror" }));
        rig.StartAndSpawnWave();
        rig.PumpUntil(() => rig.Effects.Pulses.Count >= 2);

        rig.Director.Shutdown();

        Assert.AreEqual(1, rig.Outcomes.Of(LongDefeatChecks.AffixEffectId).Count);
    }

    [TestMethod]
    public void ANonTerrorWave_NeverPulses_AndWritesNoTerrorCheck()
    {
        var rig = new Rig(5, SeedRolling(5, new[] { "hammer" }));
        rig.StartAndSpawnWave();

        for (var i = 0; i < 12; i++)
            rig.Director.Tick(1f);

        Assert.AreEqual(0, rig.Effects.Pulses.Count);
        Assert.AreEqual(0, rig.Field.Count("Pulse"));
    }

    [TestMethod]
    public void ATerrorWaveWithNoEffectsAdapter_PulsesNothing()
    {
        var rig = new Rig(5, SeedRolling(5, new[] { "terror" }), withEffects: false);
        rig.StartAndSpawnWave();

        for (var i = 0; i < 12; i++)
            rig.Director.Tick(1f);

        Assert.AreEqual(0, rig.Effects.Pulses.Count);
        Assert.AreEqual(0, rig.Outcomes.Of(LongDefeatChecks.AffixEffectId).Count);
    }
}
