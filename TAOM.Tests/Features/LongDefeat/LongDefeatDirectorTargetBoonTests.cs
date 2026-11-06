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
/// The troop and class boons at the director (phase2-plan.md section 4, "Apply order", and section 6, <c>boon</c>; Slice 6): a pick publishes the new table, refreshes and scales those
/// standing, before anything of that tick spawns; a pack bought after a health pick spawns with it; a body the lord takes over is refreshed and given the player's health; and the
/// pick's check reads a soldier of the target back, at the pick and again from the first pack spawned after it.
/// </summary>
[TestClass]
public class LongDefeatDirectorTargetBoonTests
{
    private static BoonRow Row(string id, BoonCategory category, string classId, params (string Key, float Value, BoonTarget Target)[] effects) =>
        new(id, category, "{=K_" + id + "}" + id, "{=T_" + id + "}text", effects.Select(e => new BoonEffect(e.Key, e.Value, e.Target)).ToList()) { ClassId = classId };

    private static BoonPlan InfantryBoon() => new(new[]
    {
        Row("tough", BoonCategory.Infantry, "", (BoonKeys.HealthPercent, 20f, BoonTarget.Infantry), (BoonKeys.SwingSpeedPercent, 10f, BoonTarget.Infantry)),
    }, 1);

    private sealed class Rig
    {
        public FakeLongDefeatField Field { get; } = new();
        public FakeLongDefeatEffects Effects { get; }
        public RecordingLogger Log { get; } = new();
        public RecordingOutcomeLog Outcomes { get; } = new();
        public LongDefeatDirector Director { get; }

        public Rig(BoonPlan boons, string? classId = null, bool withEffects = true, int stopAfter = 5)
        {
            Effects = new FakeLongDefeatEffects(Field.Calls);
            var waves = new WavePlan(
                new[] { new WaveSpec(20, new[] { new WaveTroop("orc", FormationSlot.Infantry, 3), new WaveTroop("skirm", FormationSlot.Ranged, 1) }) },
                new WaveScaling(10, 300));
            var plan = new RunPlan(
                "helms",
                new[] { new GarrisonRow("fow_militia", FormationSlot.Infantry, 30) },
                waves,
                new ShopPlan(100, new[] { new ShopOffer("militia", "Militia", "fow_militia", FormationSlot.Infantry, 12, 180) }),
                RunRules.Default with { PreparationSeconds = 1f, IntermissionSeconds = 60f },
                new RunOptions { Autoplay = true, StopAfterWaves = stopAfter, Seed = 1, ClassId = classId })
            {
                Bounties = new Dictionary<string, int> { ["orc"] = 4, ["skirm"] = 6 },
                Boons = boons,
                TroopGroups = new Dictionary<string, BoonTarget> { ["fow_militia"] = BoonTarget.Infantry },
            };
            Director = new LongDefeatDirector(plan, Field, new LongDefeatSession(), Log, Outcomes, "m1", null, withEffects ? Effects : null);
        }

        public void Step()
        {
            Director.Tick(1f);
            Field.AttackersStanding = 0;
        }

        public void RunToTheEnd()
        {
            Assert.IsTrue(Director.Start());
            for (var i = 0; i < 4000 && Director.Result == null; i++)
                Step();
            Assert.IsNotNull(Director.Result);
        }
    }

    [TestInitialize]
    public void Init() => MissionThreadGuard.ResetForTests();

    [TestCleanup]
    public void Clean() => MissionThreadGuard.ResetForTests();

    /// <summary>A soldier of the group that shows what the boon owned should have done: the health limit at the factor, swing speed at the percent.</summary>
    private static LdTargetSample Right(IReadOnlyList<string> stats, float health = 1.2f) =>
        new("fow_militia", 100f * health, 100f, stats.Select(s => new LdStatReading(s, 1f, s == "SwingSpeedMultiplier" ? 1.1f : 1f)).ToList());

    [TestMethod]
    public void ATroopPick_PublishesThenRefreshesThenScales_ThoseStanding()
    {
        var rig = new Rig(InfantryBoon());

        rig.RunToTheEnd();

        var calls = rig.Field.Calls;
        var scale = calls.IndexOf("ScaleHealth:Infantry:1.20");
        Assert.IsTrue(scale >= 2, string.Join(",", calls));
        Assert.AreEqual("RefreshStats:Infantry", calls[scale - 1]);
        StringAssert.StartsWith(calls[scale - 2], "Publish:");
        var published = rig.Effects.Published.Where(t => t.Infantry.Stats != null).ToList();
        Assert.IsTrue(published.Count >= 1);
        Assert.AreEqual(10f, published[0].Infantry.Stats!.SwingSpeedPercent);
        Assert.AreEqual(1, calls.Count(c => c == "ScaleHealth:Infantry:1.20"), "scaled once for the one pick");
        Assert.AreEqual(1, calls.Count(c => c == "RefreshStats:Infantry"));
    }

    [TestMethod]
    public void AHealthOnlyPick_ScalesWithoutARefresh_AndADamageOnlyPickPublishesWithNeither()
    {
        var health = new BoonPlan(new[] { Row("hp", BoonCategory.Infantry, "", (BoonKeys.HealthPercent, 20f, BoonTarget.Infantry)) }, 1);
        var damage = new BoonPlan(new[] { Row("dmg", BoonCategory.Infantry, "", (BoonKeys.DamagePercent, 20f, BoonTarget.Infantry)) }, 1);
        var a = new Rig(health);
        var b = new Rig(damage);

        a.RunToTheEnd();
        b.RunToTheEnd();

        Assert.IsTrue(a.Field.Calls.Contains("ScaleHealth:Infantry:1.20"));
        Assert.IsFalse(a.Field.Calls.Any(c => c == "RefreshStats:Infantry"));
        Assert.IsFalse(b.Field.Calls.Any(c => c.StartsWith("ScaleHealth", System.StringComparison.Ordinal) || c.StartsWith("RefreshStats:Infantry", System.StringComparison.Ordinal)));
        Assert.AreEqual(1.2f, b.Effects.Published.Last(t => t.Infantry.Damage != 1f).Infantry.Damage, 1e-5f);
    }

    [TestMethod]
    public void APackBoughtAfterAHealthPick_SpawnsWithTheBoonsFactor_TheGarrisonBeforeItWithOne()
    {
        var rig = new Rig(InfantryBoon());

        rig.RunToTheEnd();

        var defenders = rig.Field.Spawned.Where(s => !s.Attackers).Select(s => s.Batch).ToList();
        Assert.AreEqual(1f, defenders.First().HealthFactor, "the opening garrison");
        Assert.IsTrue(defenders.Any(b => b.Reinforcement && b.HealthFactor == 1.2f), string.Join(",", defenders.Select(b => b.HealthFactor)));
    }

    [TestMethod]
    public void TheLordAtTheStart_IsRefreshedAndGivenThePlayerHealth_AfterHeIsAdopted()
    {
        var rig = new Rig(BoonPlan.None);

        rig.Director.Start();

        var calls = rig.Field.Calls;
        var adopt = calls.IndexOf("Adopt:True");
        Assert.IsTrue(adopt >= 0 && calls.IndexOf("RefreshStats:Player") > adopt, string.Join(",", calls));
        Assert.AreEqual("SetPlayerHealth:1.00", calls[calls.IndexOf("RefreshStats:Player") + 1]);
    }

    [TestMethod]
    public void ATakenOverSoldier_IsRefreshedAndGivenThePlayersHealth_NotHisGroups()
    {
        var plan = new BoonPlan(new[] { Row("iron", BoonCategory.Class, "archer", (BoonKeys.HealthPercent, 50f, BoonTarget.Player)) }, 1);
        var rig = new Rig(plan, "archer", stopAfter: 6);
        rig.Director.Start();
        for (var i = 0; i < 4000 && !rig.Field.Calls.Contains("SetPlayerHealth:1.50"); i++)
            rig.Step();
        Assert.IsTrue(rig.Field.Calls.Contains("SetPlayerHealth:1.50"), "the pick set the lord standing to the combined factor");
        rig.Field.Calls.Clear();

        rig.Field.MainBody = 2;
        rig.Step();

        Assert.IsTrue(rig.Field.Calls.Contains("RefreshStats:Player"), string.Join(",", rig.Field.Calls));
        Assert.IsTrue(rig.Field.Calls.Contains("SetPlayerHealth:1.50"), "an absolute write, so the soldier's group factor is replaced and not stacked");
    }

    [TestMethod]
    public void ALordHealthPick_IsAnAbsoluteWrite_NeverAScale_SoABodyAdoptedInTheSameTickCannotTakeItTwice()
    {
        // The wave-clear respawn and the bot's pick can fall in one tick: the next tick adopts the new body (DressNewBody writes the product, this pick included) and only then
        // lands the pending boon. A relative scale would apply the pick's factor to a body that already holds it.
        var plan = new BoonPlan(new[] { Row("iron", BoonCategory.Class, "archer", (BoonKeys.HealthPercent, 60f, BoonTarget.Player)) }, 1);
        var rig = new Rig(plan, "archer", stopAfter: 6);

        rig.RunToTheEnd();

        var calls = rig.Field.Calls;
        Assert.IsTrue(calls.Contains("SetPlayerHealth:1.60"), string.Join(",", calls));
        Assert.IsFalse(calls.Any(c => c.StartsWith("ScaleHealth:Player", System.StringComparison.Ordinal)), "a relative scale of the lord stacks on a body adopted with the factor already in");
    }

    [TestMethod]
    public void TheBoonCheck_ReadsASoldierBack_AtThePick_AndPassesWhenTheNumbersReachedIt()
    {
        var rig = new Rig(InfantryBoon());
        rig.Effects.TargetFor = (_, _, stats) => Right(stats);

        rig.RunToTheEnd();

        var line = rig.Outcomes.Of(LongDefeatChecks.BoonId).First(l => l.StartsWith("p1 ", System.StringComparison.Ordinal));
        Assert.IsTrue(rig.Outcomes.Has("pass", LongDefeatChecks.BoonId), line);
        StringAssert.Contains(line, "read=pass");
        Assert.IsTrue(rig.Effects.TargetReads.Any(r => r.Target == BoonTarget.Infantry && !r.Newest && r.Stats.Contains("SwingSpeedMultiplier")));
    }

    [TestMethod]
    public void TheBoonCheck_FailsWhenTheSoldierKeptItsOldNumbers()
    {
        var rig = new Rig(InfantryBoon());
        rig.Effects.TargetFor = (_, _, stats) => Right(stats, health: 1f);

        rig.RunToTheEnd();

        Assert.IsTrue(rig.Outcomes.Has("fail", LongDefeatChecks.BoonId), string.Join("\n", rig.Outcomes.Of(LongDefeatChecks.BoonId)));
    }

    [TestMethod]
    public void TheBoonCheck_IsASkipRead_NotAFailure_WhenNoSoldierOfTheGroupStands()
    {
        var rig = new Rig(InfantryBoon());

        rig.RunToTheEnd();

        var line = rig.Outcomes.Of(LongDefeatChecks.BoonId).First(l => l.StartsWith("p1 ", System.StringComparison.Ordinal));
        Assert.IsTrue(rig.Outcomes.Has("pass", LongDefeatChecks.BoonId), line);
        StringAssert.Contains(line, "read=skip");
    }

    [TestMethod]
    public void TheFirstPackAfterThePick_IsReadBackOnce_FromTheNewestSoldierOfItsGroup()
    {
        var rig = new Rig(InfantryBoon());
        rig.Effects.TargetFor = (_, _, stats) => Right(stats);

        rig.RunToTheEnd();

        var spawned = rig.Outcomes.Lines.Where(l => l.Contains(LongDefeatChecks.BoonId) && l.Contains(" p1-infantry ")).ToList();
        Assert.AreEqual(1, spawned.Count, string.Join("\n", rig.Outcomes.Lines.Where(l => l.Contains("boon"))));
        StringAssert.StartsWith(spawned[0], "pass ");
        StringAssert.Contains(spawned[0], "read=spawned");
        Assert.IsTrue(rig.Effects.TargetReads.Any(r => r.Newest && r.Target == BoonTarget.Infantry));
    }

    [TestMethod]
    public void ASoldierSpawnedAfterThePick_WhoMissedTheFactor_FailsTheCheck()
    {
        var rig = new Rig(InfantryBoon());
        rig.Effects.TargetFor = (_, newest, stats) => newest ? Right(stats, health: 1f) : Right(stats);

        rig.RunToTheEnd();

        Assert.IsTrue(rig.Outcomes.Lines.Any(l => l.StartsWith("fail ", System.StringComparison.Ordinal) && l.Contains(" p1-infantry ")), string.Join("\n", rig.Outcomes.Of(LongDefeatChecks.BoonId)));
    }

    [TestMethod]
    public void WithNoEffectsAdapter_ATroopPickStillLandsInTheRun_AndWritesNoRead()
    {
        var rig = new Rig(InfantryBoon(), withEffects: false);

        rig.RunToTheEnd();

        var line = rig.Outcomes.Of(LongDefeatChecks.BoonId).Single();
        Assert.IsFalse(line.Contains("read="));
        Assert.IsTrue(rig.Field.Spawned.Any(s => !s.Attackers && s.Batch.HealthFactor == 1.2f));
    }
}
