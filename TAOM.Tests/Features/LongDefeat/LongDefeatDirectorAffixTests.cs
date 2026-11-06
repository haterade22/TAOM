using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.AdvancedCombat;
using TAOM.Features.LongDefeat;
using TAOM.Features.LongDefeat.Diagnostics;
using TAOM.Features.LongDefeat.Domain;
using TAOM.Tests.Features.LoadTimeStamps;

namespace TAOM.Tests.Features.LongDefeat;

/// <summary>
/// The affixes at the director (phase2-plan.md section 4, "Apply order", and section 6, <c>affix</c> and <c>affix-effect</c>): the wave's stat table is
/// published before any attacker spawns, the roll is checked against the schedule when the wave starts, and the stat powers and <c>hide</c> are read
/// back from one sampled attacker when the wave's first batch has closed.
/// </summary>
[TestClass]
public class LongDefeatDirectorAffixTests
{
    // Wave 5 rolls one power: seed 0 hide, 1 drums, 2 discipline, 4 eye, 6 terror, 8 hammer, 27 lash (AffixPlanTests). Wave 10 rolls two: seed 31 lash and eye.
    private const ulong Eye = 4, Hide = 0, Lash = 27, LashAndEye = 31;

    private static readonly Dictionary<string, string> Names = AffixPlan.Ids.ToDictionary(id => id, id => "{=TAOM_LD_Test_" + id + "}Name of " + id);

    private static AffixPlan Affixes() => new(
        new[] { new AffixStage(5, 1), new AffixStage(10, 2), new AffixStage(15, 3) },
        new Dictionary<string, IReadOnlyDictionary<string, float>>
        {
            ["lash"] = new Dictionary<string, float> { ["swingSpeedPercent"] = 15f, ["attackEagernessPercent"] = 25f },
            ["eye"] = new Dictionary<string, float>
            {
                ["aimErrorPercent"] = -30f, ["drawSpeedPercent"] = 15f, ["reloadSpeedPercent"] = 15f, ["missileSpeedPercent"] = 10f,
            },
            ["hide"] = new Dictionary<string, float> { ["armourPercent"] = 30f },
            ["hammer"] = new Dictionary<string, float> { ["engineHitPointsPercent"] = 50f },
        },
        Names);

    private sealed class Rig
    {
        public FakeLongDefeatField Field { get; } = new();
        public FakeLongDefeatEffects Effects { get; }
        public RecordingLogger Log { get; } = new();
        public RecordingOutcomeLog Outcomes { get; } = new();
        public LongDefeatDirector Director { get; }

        public Rig(int startWave, ulong seed, bool withEffects = true, AffixPlan? affixes = null)
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
                Affixes = affixes ?? Affixes(),
            };
            Director = new LongDefeatDirector(plan, Field, new LongDefeatSession(), Log, Outcomes, "m1", null, withEffects ? Effects : null);
        }

        public void StartAndSpawnWave()
        {
            Assert.IsTrue(Director.Start());
            for (var i = 0; i < 200 && !Field.Calls.Contains("Refresh"); i++)
                Director.Tick(1f);
            Assert.IsTrue(Field.Calls.Contains("Refresh"), "the wave never closed its first batch");
        }
    }

    [TestInitialize]
    public void Init() => MissionThreadGuard.ResetForTests();

    [TestCleanup]
    public void Clean() => MissionThreadGuard.ResetForTests();

    private static LdStatSample Sample(IReadOnlyList<string> stats, Func<string, float, float> actual, Func<string, float> baseline) =>
        new("orc", stats.Select(s => new LdStatReading(s, baseline(s), actual(s, baseline(s)))).ToList());

    [TestMethod]
    public void TheWavesTable_IsPublishedBeforeTheFirstAttackerSpawns_WithItsArmour()
    {
        var rig = new Rig(startWave: 5, seed: Hide);

        rig.StartAndSpawnWave();

        var publish = rig.Field.Calls.FindIndex(c => c.StartsWith("Publish:", StringComparison.Ordinal));
        var firstAttacker = rig.Field.Calls.FindIndex(c => c.StartsWith("Spawn:att", StringComparison.Ordinal));
        Assert.IsTrue(publish >= 0 && firstAttacker > publish, string.Join(",", rig.Field.Calls));
        Assert.AreEqual(30f, rig.Effects.Published.First().AttackerArmourPercent, "the first batch of a hide wave is spawned armoured");
        Assert.IsNull(rig.Effects.Published.First().Attackers);
    }

    [TestMethod]
    public void AStatPowerWave_PublishesItsRecordBeforeTheFirstAttacker()
    {
        var rig = new Rig(startWave: 5, seed: Lash);

        rig.StartAndSpawnWave();

        var table = rig.Effects.Published.First();
        Assert.AreEqual(15f, table.Attackers!.SwingSpeedPercent);
        Assert.IsTrue(rig.Field.Calls.FindIndex(c => c.StartsWith("Publish:", StringComparison.Ordinal)) < rig.Field.Calls.FindIndex(c => c.StartsWith("Spawn:att", StringComparison.Ordinal)));
    }

    [TestMethod]
    public void TheAffixCheck_IsWrittenWhenTheWaveStarts_AndLogged()
    {
        var rig = new Rig(startWave: 5, seed: Hide);

        rig.StartAndSpawnWave();

        var line = rig.Outcomes.Of(LongDefeatChecks.AffixId).Single();
        Assert.IsTrue(rig.Outcomes.Has("pass", LongDefeatChecks.AffixId));
        StringAssert.StartsWith(line, "w5 w=5 count=1 scheduled=1 powers=hide ");
        Assert.IsTrue(rig.Log.Lines.Any(l => l.Contains("[Affix] w5") && l.Contains("hide")), string.Join("\n", rig.Log.Lines.Where(l => l.Contains("Affix"))));
    }

    [TestMethod]
    public void AWaveWithNoAffix_WritesNoAffixCheck_AndReadsNoStats()
    {
        var rig = new Rig(startWave: 4, seed: Hide);

        rig.StartAndSpawnWave();

        Assert.AreEqual(0, rig.Outcomes.Of(LongDefeatChecks.AffixId).Count);
        Assert.AreEqual(0, rig.Outcomes.Of(LongDefeatChecks.AffixEffectId).Count);
        Assert.AreEqual(0, rig.Effects.StatsAsked.Count);
    }

    [TestMethod]
    public void AHideWave_ReadsTheArmourBack_WhenTheFirstBatchCloses_AndPasses()
    {
        var rig = new Rig(startWave: 5, seed: Hide);
        rig.Effects.StatsFor = stats => Sample(stats, (_, b) => b * 1.3f, _ => 10f);

        rig.StartAndSpawnWave();

        CollectionAssert.AreEquivalent(new[] { "ArmorHead", "ArmorTorso", "ArmorLegs", "ArmorArms" }, rig.Effects.StatsAsked.Single().ToArray());
        var line = rig.Outcomes.Of(LongDefeatChecks.AffixEffectId).Single();
        Assert.IsTrue(rig.Outcomes.Has("pass", LongDefeatChecks.AffixEffectId), line);
        StringAssert.StartsWith(line, "hide power=hide ");
    }

    [TestMethod]
    public void AStatPowerWave_FailsItsEffectCheck_WhenTheAgentIgnoredTheRecord()
    {
        var rig = new Rig(startWave: 5, seed: Lash);
        rig.Effects.StatsFor = stats => Sample(stats, (_, b) => b, _ => 0.5f);

        rig.StartAndSpawnWave();

        Assert.IsTrue(rig.Outcomes.Has("fail", LongDefeatChecks.AffixEffectId), string.Join("\n", rig.Outcomes.Lines));
        StringAssert.StartsWith(rig.Outcomes.Of(LongDefeatChecks.AffixEffectId).Single(), "lash power=lash ");
    }

    [TestMethod]
    public void AStatPowerWave_PassesItsEffectCheck_WhenTheAgentTookTheRecord()
    {
        var rig = new Rig(startWave: 5, seed: Lash);
        rig.Effects.StatsFor = stats => Sample(stats, (name, b) => name == "AIAttackOnDecideChance" ? b * 1.25f : b * 1.15f, _ => 0.5f);

        rig.StartAndSpawnWave();

        Assert.IsTrue(rig.Outcomes.Has("pass", LongDefeatChecks.AffixEffectId), string.Join("\n", rig.Outcomes.Lines));
    }

    [TestMethod]
    public void AWaveWithTwoStatPowers_ReadsOneSoldierOnce_AndJudgesEachPowerOnIt()
    {
        var rig = new Rig(startWave: 10, seed: LashAndEye);
        rig.Effects.StatsFor = stats => Sample(stats, (_, b) => b, _ => 1f);

        rig.StartAndSpawnWave();

        Assert.AreEqual(1, rig.Effects.StatsAsked.Count, "one read, so both powers are judged on the same soldier");
        Assert.AreEqual(6, rig.Effects.StatsAsked.Single().Count, "lash's two properties and eye's four");
        var lines = rig.Outcomes.Of(LongDefeatChecks.AffixEffectId);
        Assert.AreEqual(2, lines.Count);
        StringAssert.StartsWith(lines[0], "lash power=lash ");
        StringAssert.StartsWith(lines[1], "eye power=eye ");
    }

    [TestMethod]
    public void ADirectorWithNoEffectsAdapter_StillChecksTheRoll_AndWritesNoEffectCheck()
    {
        var rig = new Rig(startWave: 5, seed: Hide, withEffects: false);

        rig.StartAndSpawnWave();

        Assert.IsTrue(rig.Outcomes.Has("pass", LongDefeatChecks.AffixId));
        Assert.AreEqual(0, rig.Outcomes.Of(LongDefeatChecks.AffixEffectId).Count);
    }

    [TestMethod]
    public void AWaveWhoseCultureTableLacksANameForItsPower_FailsTheAffixCheck()
    {
        var bare = new AffixPlan(new[] { new AffixStage(5, 1) }, new Dictionary<string, IReadOnlyDictionary<string, float>>(), new Dictionary<string, string>());
        var rig = new Rig(startWave: 5, seed: Hide, affixes: bare);

        rig.StartAndSpawnWave();

        Assert.IsTrue(rig.Outcomes.Has("fail", LongDefeatChecks.AffixId));
    }

    [TestMethod]
    public void TheEyeWave_AsksForAllFourOfItsProperties()
    {
        var rig = new Rig(startWave: 5, seed: Eye);
        rig.Effects.StatsFor = stats => Sample(stats, (_, b) => b, _ => 1f);

        rig.StartAndSpawnWave();

        CollectionAssert.AreEquivalent(
            new[] { "AiShooterError", "ThrustOrRangedReadySpeedMultiplier", "ReloadSpeed", "MissileSpeedMultiplier" },
            rig.Effects.StatsAsked.Single().ToArray());
    }
}
