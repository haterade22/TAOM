using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Newtonsoft.Json.Linq;
using TAOM.Adapters;
using TAOM.Features.AdvancedCombat;
using TAOM.Features.LongDefeat;
using TAOM.Features.LongDefeat.Diagnostics;
using TAOM.Features.LongDefeat.Domain;
using TAOM.Tests.Features.LoadTimeStamps;

namespace TAOM.Tests.Features.LongDefeat;

/// <summary>
/// Morgul Blades at the director (phase2-plan.md Slice 9): a blades wave wants hits and a hit becomes a dose, the doses reach the effects adapter as poison ticks owned by the hitter, what the adapter
/// answers is booked in the run and its report, a removal makes the run forget the agent, and the wave's check is written from the run's own counts when the wave ends.
/// </summary>
[TestClass]
public class LongDefeatDirectorPoisonTests
{
    private static readonly Dictionary<string, string> Names = AffixPlan.Ids.ToDictionary(id => id, id => "{=TAOM_LD_Test_" + id + "}Name of " + id);

    private static AffixPlan Affixes() => new(
        new[] { new AffixStage(5, 1), new AffixStage(10, 2), new AffixStage(15, 3) },
        new Dictionary<string, IReadOnlyDictionary<string, float>>
        {
            ["blades"] = new Dictionary<string, float> { ["poisonPerSecond"] = 2f, ["poisonSeconds"] = 6f },
            ["hammer"] = new Dictionary<string, float> { ["engineHitPointsPercent"] = 50f },
        },
        Names);

    private static ulong SeedRolling(int wave, string power)
    {
        var plan = Affixes();
        for (ulong seed = 0; seed < 5000; seed++)
        {
            if (plan.Roll(seed, wave).SequenceEqual(new[] { power }))
                return seed;
        }

        Assert.Fail($"no seed rolls {power} at wave {wave}: is it in AffixPlan.Pool?");
        return 0;
    }

    private sealed class Rig
    {
        public FakeLongDefeatField Field { get; } = new();
        public FakeLongDefeatEffects Effects { get; }
        public RecordingLogger Log { get; } = new();
        public RecordingOutcomeLog Outcomes { get; } = new();
        public LongDefeatDirector Director { get; }

        public Rig(int startWave, ulong seed)
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
            Director = new LongDefeatDirector(plan, Field, new LongDefeatSession(), Log, Outcomes, "m1", null, Effects);
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

        public string BladesLine() => Outcomes.Of(LongDefeatChecks.AffixEffectId).Single(l => l.StartsWith("blades ", StringComparison.Ordinal));
    }

    [TestInitialize]
    public void Init() => MissionThreadGuard.ResetForTests();

    [TestCleanup]
    public void Clean() => MissionThreadGuard.ResetForTests();

    [TestMethod]
    public void OnlyABladesWave_WantsHits()
    {
        var blades = new Rig(5, SeedRolling(5, "blades"));
        var hammer = new Rig(5, SeedRolling(5, "hammer"));
        var plain = new Rig(2, 1);

        Assert.IsFalse(blades.Director.WantsHits, "before the run starts");
        blades.StartAndSpawnWave();
        hammer.StartAndSpawnWave();
        plain.StartAndSpawnWave();

        Assert.IsTrue(blades.Director.WantsHits);
        Assert.IsFalse(hammer.Director.WantsHits);
        Assert.IsFalse(plain.Director.WantsHits);
    }

    [TestMethod]
    public void TheWaveClear_EndsTheWantForHits()
    {
        var rig = new Rig(5, SeedRolling(5, "blades"));
        rig.StartAndSpawnWave();

        rig.ClearTheWave();

        Assert.IsFalse(rig.Director.WantsHits);
    }

    [TestMethod]
    public void AHit_NotWanted_IsNotEvenResolvedToKeys()
    {
        var rig = new Rig(5, SeedRolling(5, "hammer"));
        rig.StartAndSpawnWave();

        rig.Director.OnHit("D:man", "A:orc");

        Assert.AreEqual(0, rig.Field.HitAsks, "the registry is not touched for a hit nobody wants");
    }

    [TestMethod]
    public void AHitBeforeTheRunStarts_IsIgnored()
    {
        var rig = new Rig(5, SeedRolling(5, "blades"));

        rig.Director.OnHit("D:man", "A:orc");

        Assert.AreEqual(0, rig.Field.HitAsks);
    }

    [TestMethod]
    public void AHit_BecomesADose_AndTheNextTickPoisonsTheDefender_OwnedByTheHitter()
    {
        var rig = new Rig(5, SeedRolling(5, "blades"));
        rig.StartAndSpawnWave();

        rig.Director.OnHit("D:man", "A:orc");
        rig.Director.Tick(1f);

        var tick = rig.Effects.Poisoned.Single();
        Assert.AreEqual(rig.Field.KeyOf("D:man"), tick.Victim);
        Assert.AreEqual(rig.Field.KeyOf("A:orc"), tick.Poisoner);
        Assert.AreEqual(2, tick.Damage);
    }

    [TestMethod]
    public void AHitThatIsNotAnAttackersBlowOnADefender_MakesNoDose()
    {
        var rig = new Rig(5, SeedRolling(5, "blades"));
        rig.StartAndSpawnWave();

        rig.Director.OnHit("A:orc", "D:man");
        rig.Director.OnHit("D:man", "D:other");
        rig.Director.OnHit(null!, "A:orc");
        rig.Director.Tick(1f);

        Assert.AreEqual(0, rig.Effects.Poisoned.Count);
    }

    [TestMethod]
    public void AHitFromAnotherThread_IsParkedUntilTheMainThreadsNextTick()
    {
        var rig = new Rig(5, SeedRolling(5, "blades"));
        rig.StartAndSpawnWave();

        Task.Run(() => rig.Director.OnHit("D:man", "A:orc")).Wait();
        Assert.AreEqual(0, rig.Effects.Poisoned.Count, "the run is touched on the main thread only");
        rig.Director.Tick(1f);

        Assert.AreEqual(1, rig.Effects.Poisoned.Count);
    }

    [TestMethod]
    public void WhatTheAdapterAnswers_IsBookedInTheRunsReport()
    {
        var rig = new Rig(5, SeedRolling(5, "blades"));
        rig.Effects.PoisonFor = (_, _, damage) => new LdPoisonResult(damage, false, false, false, false);
        rig.StartAndSpawnWave();
        rig.Director.OnHit("D:man", "A:orc");
        rig.Director.OnHit("D:man", "A:orc");
        rig.Director.OnHit("D:other", "A:orc");

        rig.Director.Tick(1f);
        rig.Director.Shutdown();

        var poison = (JObject)JObject.Parse(rig.Director.ReportText)["poison"]!;
        Assert.AreEqual((2, 3, 4, 0, 0), ((int)poison["doses"]!, (int)poison["hits"]!, (int)poison["damage"]!, (int)poison["ownerMissing"]!, (int)poison["withheld"]!));
    }

    [TestMethod]
    public void AWithheldTick_AndAMissingOwner_AreCountedInTheReport()
    {
        var rig = new Rig(5, SeedRolling(5, "blades"));
        var answers = new Queue<LdPoisonResult>(new[] { new LdPoisonResult(1, false, true, false, false), new LdPoisonResult(0, false, false, true, false) });
        rig.Effects.PoisonFor = (_, _, _) => answers.Dequeue();
        rig.StartAndSpawnWave();
        rig.Director.OnHit("D:man", "A:orc");
        rig.Director.OnHit("D:other", "A:orc");

        rig.Director.Tick(1f);
        rig.Director.Shutdown();

        var poison = (JObject)JObject.Parse(rig.Director.ReportText)["poison"]!;
        Assert.AreEqual((1, 1), ((int)poison["ownerMissing"]!, (int)poison["withheld"]!));
    }

    [TestMethod]
    public void ARemovedDefender_LosesHisDose_AndALaterHitOnHimIsDropped()
    {
        var rig = new Rig(5, SeedRolling(5, "blades"));
        rig.StartAndSpawnWave();
        rig.Director.OnHit("D:man", "A:orc");

        rig.Director.OnAgentRemoved("D:man", true);
        rig.Director.OnHit("D:man", "A:orc");
        rig.Director.Tick(1f);

        Assert.AreEqual(0, rig.Effects.Poisoned.Count);
    }

    [TestMethod]
    public void ARemovedPoisoner_DropsALaterHit_ButHisDoseStillTicksForAnotherOwnerToBeChosen()
    {
        var rig = new Rig(5, SeedRolling(5, "blades"));
        rig.StartAndSpawnWave();
        rig.Director.OnHit("D:man", "A:orc");
        rig.Director.OnAgentRemoved("A:orc", false);

        rig.Director.OnHit("D:other", "A:orc");
        rig.Director.Tick(1f);

        var tick = rig.Effects.Poisoned.Single();
        Assert.AreEqual(rig.Field.KeyOf("D:man"), tick.Victim);
        Assert.AreEqual(rig.Field.KeyOf("A:orc"), tick.Poisoner, "the key stays; the adapter chooses a living owner");
    }

    [TestMethod]
    public void TheWavesCheck_PassesFromTheRunsCounts_WithOwnerMissingAtZero()
    {
        var rig = new Rig(5, SeedRolling(5, "blades"));
        rig.StartAndSpawnWave();
        rig.Director.OnHit("D:man", "A:orc");
        rig.Director.Tick(1f);

        rig.ClearTheWave();

        var line = rig.BladesLine();
        Assert.IsTrue(rig.Outcomes.Has("pass", LongDefeatChecks.AffixEffectId), line);
        StringAssert.Contains(line, "power=blades w=5 doses=1 hits=1 damage=2 owner_missing=0 withheld=0");
    }

    [TestMethod]
    public void TheWavesCheck_Fails_WhenAnOwnerWasNotActiveAtTheCall()
    {
        var rig = new Rig(5, SeedRolling(5, "blades"));
        rig.Effects.PoisonFor = (_, _, _) => new LdPoisonResult(0, false, false, true, false);
        rig.StartAndSpawnWave();
        rig.Director.OnHit("D:man", "A:orc");
        rig.Director.Tick(1f);

        rig.ClearTheWave();

        Assert.IsTrue(rig.Outcomes.Has("fail", LongDefeatChecks.AffixEffectId), string.Join("\n", rig.Outcomes.Lines));
        StringAssert.Contains(rig.BladesLine(), "owner_missing=1");
    }

    [TestMethod]
    public void TheWavesCheck_IsASkip_WhenNoHitLanded()
    {
        var rig = new Rig(5, SeedRolling(5, "blades"));
        rig.StartAndSpawnWave();

        rig.ClearTheWave();

        Assert.IsTrue(rig.Outcomes.Has("skip", LongDefeatChecks.AffixEffectId));
        StringAssert.Contains(rig.BladesLine(), "reason=no-hit");
    }

    [TestMethod]
    public void TheWavesCheck_CountsOnlyThatWavesShareOfTheRun()
    {
        var rig = new Rig(5, SeedRolling(5, "blades"));
        rig.StartAndSpawnWave();
        rig.Director.OnHit("D:man", "A:orc");
        rig.Director.Tick(1f);
        rig.ClearTheWave();

        StringAssert.Contains(rig.BladesLine(), "hits=1");
        Assert.AreEqual(1, rig.Outcomes.Of(LongDefeatChecks.AffixEffectId).Count(l => l.StartsWith("blades ", StringComparison.Ordinal)), "one line per blades wave");
    }

    [TestMethod]
    public void TheRunEndingInABladesWave_StillWritesTheCheck()
    {
        var rig = new Rig(5, SeedRolling(5, "blades"));
        rig.StartAndSpawnWave();
        rig.Director.OnHit("D:man", "A:orc");
        rig.Director.Tick(1f);

        rig.Director.Shutdown();

        Assert.AreEqual(1, rig.Outcomes.Of(LongDefeatChecks.AffixEffectId).Count(l => l.StartsWith("blades ", StringComparison.Ordinal)));
    }

    [TestMethod]
    public void ANonBladesWave_WritesNoBladesCheck_AndPoisonsNobody()
    {
        var rig = new Rig(5, SeedRolling(5, "hammer"));
        rig.StartAndSpawnWave();
        rig.Director.OnHit("D:man", "A:orc");
        rig.Director.Tick(1f);

        rig.ClearTheWave();

        Assert.AreEqual(0, rig.Effects.Poisoned.Count);
        Assert.AreEqual(0, rig.Outcomes.Of(LongDefeatChecks.AffixEffectId).Count(l => l.StartsWith("blades ", StringComparison.Ordinal)));
    }

    [TestMethod]
    public void AnAdapterThatKilledOrSubstitutedAnOwner_IsLoggedEvenAfterTheFirstTicks()
    {
        var rig = new Rig(5, SeedRolling(5, "blades"));
        rig.Effects.PoisonFor = (_, _, damage) => new LdPoisonResult(damage, true, false, false, true);
        rig.StartAndSpawnWave();
        rig.Director.OnHit("D:man", "A:orc");

        rig.Director.Tick(1f);

        Assert.IsTrue(rig.Log.Lines.Any(l => l.Contains("[Blades]") && l.Contains("killed") && l.Contains("substituted")), string.Join("\n", rig.Log.Lines.Where(l => l.Contains("Blades"))));
    }
}
