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
/// The endless scale at the director (phase2-plan.md section 4, "Apply order", and section 6, <c>scale</c>): the wave's damage factor is
/// published before any attacker spawns, a wave past the authored list writes its <c>[Check] scale</c> line from the effects adapter's
/// reads, and an authored wave writes none.
/// </summary>
[TestClass]
public class LongDefeatDirectorScaleTests
{
    private sealed class Rig
    {
        public FakeLongDefeatField Field { get; } = new();
        public FakeLongDefeatEffects Effects { get; }
        public RecordingLogger Log { get; } = new();
        public RecordingOutcomeLog Outcomes { get; } = new();
        public LongDefeatDirector Director { get; }

        public Rig(int startWave)
        {
            Effects = new FakeLongDefeatEffects(Field.Calls);
            var growth = new EndlessScale(0.05f, 0.03f, 3f, 3, new Dictionary<string, string> { ["orc"] = "orc2" });
            var waves = new WavePlan(
                new[] { new WaveSpec(110, new[] { new WaveTroop("orc", FormationSlot.Infantry, 75), new WaveTroop("skirm", FormationSlot.Ranged, 25) }) },
                new WaveScaling(10, 300),
                growth);
            var shop = new ShopPlan(600, new[] { new ShopOffer("militia", "Militia", "fow_militia", FormationSlot.Infantry, 12, 180) });
            var plan = new RunPlan(
                "helms",
                new[] { new GarrisonRow("fow_militia", FormationSlot.Infantry, 30) },
                waves,
                shop,
                RunRules.Default with { PreparationSeconds = 1f, IntermissionSeconds = 1f },
                new RunOptions { StartWave = startWave })
            {
                Bounties = new Dictionary<string, int> { ["orc"] = 4, ["orc2"] = 6, ["skirm"] = 6 },
            };
            Director = new LongDefeatDirector(plan, Field, new LongDefeatSession(), Log, Outcomes, "m1", null, Effects);
        }

        public void StartAndSpawnWave()
        {
            Assert.IsTrue(Director.Start());
            PumpUntil(() => Field.Calls.Contains("Refresh"));
        }

        /// <summary>Ticks with nobody standing until the wave clears (the queue empties, then the field is empty).</summary>
        public void ClearTheWave()
        {
            PumpUntil(() => Outcomes.Has("pass", LongDefeatChecks.WaveId) || Outcomes.Has("fail", LongDefeatChecks.WaveId), limit: 400, empty: true);
        }

        public void PumpUntil(Func<bool> done, int limit = 200, bool empty = false)
        {
            for (var i = 0; i < limit && !done(); i++)
            {
                if (empty)
                    Field.AttackersStanding = 0;
                Director.Tick(1f);
            }

            Assert.IsTrue(done(), "the condition never came true");
        }
    }

    [TestInitialize]
    public void Init() => MissionThreadGuard.ResetForTests();

    [TestCleanup]
    public void Clean() => MissionThreadGuard.ResetForTests();

    [TestMethod]
    public void TheWavesDamageFactor_IsPublishedBeforeAnyAttackerSpawns()
    {
        var rig = new Rig(startWave: 3);

        rig.StartAndSpawnWave();

        var publish = rig.Field.Calls.IndexOf("Publish:1.06");
        Assert.IsTrue(publish >= 0, string.Join(",", rig.Field.Calls));
        Assert.IsTrue(publish < rig.Field.Calls.FindIndex(c => c.StartsWith("Spawn:att", StringComparison.Ordinal)), "armour and damage are read when a soldier is built");
        Assert.AreEqual(1, rig.Effects.Published.Count);
    }

    [TestMethod]
    public void TheWaveHealthFactor_RidesEveryAttackerBatchToTheField_AndNeverADefenderBatch()
    {
        var rig = new Rig(startWave: 3);

        rig.StartAndSpawnWave();

        var attackers = rig.Field.Spawned.Where(s => s.Attackers).ToList();
        Assert.IsTrue(attackers.Count > 0);
        Assert.IsTrue(attackers.All(s => Math.Abs(s.Batch.HealthFactor - 1.10f) < 1e-5f));
        Assert.IsTrue(rig.Field.Spawned.Where(s => !s.Attackers).All(s => s.Batch.HealthFactor == 1f));
    }

    [TestMethod]
    public void TheWaveStartLine_NamesTheFactors_OfAScaledWaveOnly()
    {
        var scaled = new Rig(startWave: 3);
        scaled.StartAndSpawnWave();
        var plain = new Rig(startWave: 1);
        plain.StartAndSpawnWave();

        var scaledLine = scaled.Log.Lines.Single(l => l.Contains("[Wave] 3 starts"));
        StringAssert.Contains(scaledLine, "health x1.10");
        StringAssert.Contains(scaledLine, "damage x1.06");
        StringAssert.Contains(scaledLine, "tier steps 0");
        Assert.IsFalse(plain.Log.Lines.Single(l => l.Contains("[Wave] 1 starts")).Contains("health x"));
    }

    [TestMethod]
    public void AScaledWave_ReadsASampleAtSpawn_AgainWhileItRuns_AndOnceMoreAtTheClear()
    {
        var rig = new Rig(startWave: 3);
        rig.StartAndSpawnWave();
        Assert.AreEqual(1, rig.Effects.Reads, "the sample at spawn");

        rig.Director.Tick(6f);
        rig.ClearTheWave();

        Assert.IsTrue(rig.Effects.Reads >= 3, "at spawn, at least one read while the wave runs, and the clear's own");
    }

    [TestMethod]
    public void AScaledWave_WritesItsScaleCheck_WhenTheSamplesMatchTheFactor()
    {
        var rig = new Rig(startWave: 3);
        rig.StartAndSpawnWave();

        rig.ClearTheWave();

        var line = rig.Outcomes.Of(LongDefeatChecks.ScaleId).Single();
        Assert.IsTrue(rig.Outcomes.Has("pass", LongDefeatChecks.ScaleId));
        StringAssert.StartsWith(line, "w3 w=3 health=1.100 spawn=1.100 ");
        StringAssert.Contains(line, "troop=orc in_mix=true");
        StringAssert.Contains(line, "damage=1.06 amplified=40 steps=0 over_room=0");
    }

    [TestMethod]
    public void AScaledWave_FailsItsScaleCheck_WhenTheSampleAtSpawnIsOffTheFactor()
    {
        var rig = new Rig(startWave: 3);
        rig.Effects.SampleFor = _ => new LdScaleSample("orc", 100f, 100f, 40);
        rig.StartAndSpawnWave();

        rig.ClearTheWave();

        Assert.IsTrue(rig.Outcomes.Has("fail", LongDefeatChecks.ScaleId));
        StringAssert.StartsWith(rig.Outcomes.Of(LongDefeatChecks.ScaleId).Single(), "w3 ");
    }

    [TestMethod]
    public void AScaledWave_FailsItsScaleCheck_WhenALaterReadFindsTheHealthUndone()
    {
        var rig = new Rig(startWave: 3);
        rig.Effects.SampleFor = n => n == 1 ? new LdScaleSample("orc", 110f, 100f, 40) : new LdScaleSample("orc", 100f, 100f, 40);
        rig.StartAndSpawnWave();

        rig.Director.Tick(6f);
        rig.ClearTheWave();

        var line = rig.Outcomes.Of(LongDefeatChecks.ScaleId).Single();
        Assert.IsTrue(rig.Outcomes.Has("fail", LongDefeatChecks.ScaleId));
        StringAssert.StartsWith(line, "w3 ");
        StringAssert.Contains(line, "late=1.000");
    }

    [TestMethod]
    public void AScaledWave_FailsItsScaleCheck_WhenTheStandingCountPassesTheRoomTheLastTickSpawnedInto()
    {
        var rig = new Rig(startWave: 3);
        rig.StartAndSpawnWave();

        rig.Field.AttackersStanding = 400;
        rig.Director.Tick(1f);
        rig.ClearTheWave();

        var line = rig.Outcomes.Of(LongDefeatChecks.ScaleId).Single();
        Assert.IsTrue(rig.Outcomes.Has("fail", LongDefeatChecks.ScaleId), line);
        Assert.IsFalse(line.EndsWith("over_room=0", StringComparison.Ordinal), "the engine's count is the observation, not the spawn rule's own arithmetic: " + line);
    }

    [TestMethod]
    public void AnAuthoredWave_WritesNoScaleCheck_AndReadsNothing()
    {
        var rig = new Rig(startWave: 1);
        rig.StartAndSpawnWave();

        rig.ClearTheWave();

        Assert.AreEqual(0, rig.Outcomes.Of(LongDefeatChecks.ScaleId).Count);
        Assert.AreEqual(0, rig.Effects.Reads);
        Assert.AreEqual(1f, rig.Effects.Published.Last().AttackerDamage, "an authored wave still resets the factor");
    }

    [TestMethod]
    public void ARunThatEndsInAScaledWave_StillWritesTheCheck_FromWhatItRead()
    {
        var rig = new Rig(startWave: 3);
        rig.StartAndSpawnWave();
        rig.Director.Tick(6f);

        rig.Director.Shutdown();

        Assert.IsTrue(rig.Outcomes.Has("pass", LongDefeatChecks.ScaleId));
        StringAssert.StartsWith(rig.Outcomes.Of(LongDefeatChecks.ScaleId).Single(), "w3 ");
    }

    [TestMethod]
    public void ADirectorWithNoEffectsAdapter_StillRuns_AndWritesNoScaleCheck()
    {
        var field = new FakeLongDefeatField();
        var outcomes = new RecordingOutcomeLog();
        var plan = new RunPlan(
            "helms",
            new[] { new GarrisonRow("fow_militia", FormationSlot.Infantry, 30) },
            new WavePlan(new[] { new WaveSpec(110, new[] { new WaveTroop("orc", FormationSlot.Infantry, 1) }) }, new WaveScaling(10, 300),
                new EndlessScale(0.05f, 0.03f, 3f, 3, new Dictionary<string, string>())),
            new ShopPlan(600, new[] { new ShopOffer("militia", "Militia", "fow_militia", FormationSlot.Infantry, 12, 180) }),
            RunRules.Default with { PreparationSeconds = 1f },
            new RunOptions { StartWave = 3 });
        var director = new LongDefeatDirector(plan, field, new LongDefeatSession(), new RecordingLogger(), outcomes, "m1");

        Assert.IsTrue(director.Start());
        for (var i = 0; i < 30; i++)
            director.Tick(1f);

        Assert.IsTrue(field.Calls.Contains("Refresh"));
        Assert.AreEqual(0, outcomes.Of(LongDefeatChecks.ScaleId).Count);
    }
}
