using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Newtonsoft.Json.Linq;
using TAOM.Features.AdvancedCombat;
using TAOM.Features.LongDefeat;
using TAOM.Features.LongDefeat.Diagnostics;
using TAOM.Features.LongDefeat.Domain;
using TAOM.Tests.Features.LoadTimeStamps;

namespace TAOM.Tests.Features.LongDefeat;

/// <summary>
/// The class at the director (phase2-plan.md Slice 5): the kit is handed to the field at the run's start and at every respawn, each spawn is read back and judged by
/// the <c>class</c> check, a run with no class spawns the lord as before, and the report names the class that was played.
/// </summary>
[TestClass]
public class LongDefeatDirectorClassTests
{
    private static LdLoadout Archer() => new("archer", false, new[]
    {
        new LdKitSlot("Item0", "bow_a"), new LdKitSlot("Item1", "arrows_a"), new LdKitSlot("Horse", ""), new LdKitSlot("HorseHarness", ""),
    });

    private sealed class Rig
    {
        public FakeLongDefeatField Field { get; } = new();
        public RecordingLogger Log { get; } = new();
        public RecordingOutcomeLog Outcomes { get; } = new();
        public LongDefeatDirector Director { get; }

        public Rig(LdLoadout? loadout, bool autoplay = true, int stopAfter = 2)
        {
            var waves = new WavePlan(
                new[] { new WaveSpec(20, new[] { new WaveTroop("orc", FormationSlot.Infantry, 3), new WaveTroop("skirm", FormationSlot.Ranged, 1) }) },
                new WaveScaling(10, 300));
            var plan = new RunPlan(
                "helms",
                new[] { new GarrisonRow("fow_militia", FormationSlot.Infantry, 30) },
                waves,
                new ShopPlan(600, new[] { new ShopOffer("militia", "Militia", "fow_militia", FormationSlot.Infantry, 12, 180) }),
                RunRules.Default with { PreparationSeconds = 1f, IntermissionSeconds = 5f },
                new RunOptions { Autoplay = autoplay, StopAfterWaves = stopAfter, Seed = 1UL, ClassId = loadout?.ClassId })
            {
                Bounties = new Dictionary<string, int> { ["orc"] = 4, ["skirm"] = 6 },
                Loadout = loadout,
            };
            Director = new LongDefeatDirector(plan, Field, new LongDefeatSession(), Log, Outcomes, "m1", null, null);
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
            Assert.IsNotNull(Director.Result, "the run never ended");
        }
    }

    [TestInitialize]
    public void Init() => MissionThreadGuard.ResetForTests();

    [TestCleanup]
    public void Clean() => MissionThreadGuard.ResetForTests();

    [TestMethod]
    public void TheLord_SpawnsWithTheKit_AtTheRunsStart_AndTheSpawnIsReadBack()
    {
        var rig = new Rig(Archer());

        Assert.IsTrue(rig.Director.Start());

        Assert.AreEqual(1, rig.Field.Count("SpawnPlayer:False:archer"), string.Join(",", rig.Field.Calls));
        Assert.AreEqual(0, rig.Field.Count("SpawnPlayer:False") - rig.Field.Count("SpawnPlayer:False:archer"), "no spawn without the kit");
        Assert.IsTrue(rig.Field.Index("ReadKit") > rig.Field.Index("SpawnPlayer:False:archer"), "the read-back follows the spawn");
        Assert.AreEqual("archer", rig.Field.Loadouts[0]!.ClassId);
    }

    [TestMethod]
    public void TheClassCheck_IsWrittenOnceAtTheStart_AsAPass_KeyedByTheMission()
    {
        var rig = new Rig(Archer());

        rig.Director.Start();

        var lines = rig.Outcomes.Lines.Where(l => l.Contains(LongDefeatChecks.ClassId)).ToList();
        Assert.AreEqual(1, lines.Count, string.Join("\n", rig.Outcomes.Lines));
        StringAssert.StartsWith(lines[0], "pass taom.long-defeat.class m1 class=archer ");
    }

    [TestMethod]
    public void AKitTheFieldCouldNotFullyApply_FailsTheCheck_AndLogsIt()
    {
        var rig = new Rig(Archer());
        rig.Field.Kit = new LdKitFacts(true, Array.Empty<LdKitSlot>(), new[] { "Item1" }, false);

        rig.Director.Start();

        Assert.IsTrue(rig.Outcomes.Has("fail", LongDefeatChecks.ClassId));
        Assert.IsTrue(rig.Log.Lines.Any(l => l.Contains("[Class]") && l.Contains("skipped=Item1")), string.Join("\n", rig.Log.Lines));
    }

    [TestMethod]
    public void ALordTheFieldRefusedToSpawn_WritesNoClassLine()
    {
        var rig = new Rig(Archer());
        rig.Field.SpawnPlayerOk = false;

        rig.Director.Start();

        Assert.IsFalse(rig.Outcomes.Lines.Any(l => l.Contains(LongDefeatChecks.ClassId)));
        Assert.AreEqual(0, rig.Field.Count("ReadKit"));
    }

    [TestMethod]
    public void ARunWithNoClass_SpawnsTheLordAsBefore_AndWritesNoClassLine()
    {
        var rig = new Rig(null);

        rig.Director.Start();

        Assert.AreEqual(1, rig.Field.Count("SpawnPlayer:False"));
        Assert.IsNull(rig.Field.Loadouts.Single());
        Assert.AreEqual(0, rig.Field.Count("ReadKit"));
        Assert.IsFalse(rig.Outcomes.Lines.Any(l => l.Contains(LongDefeatChecks.ClassId)));
    }

    [TestMethod]
    public void ARespawn_GetsTheSameKit_AndIsReadBackAgain_UnderItsOwnKey()
    {
        var rig = new Rig(Archer(), autoplay: false, stopAfter: 3);
        Assert.IsTrue(rig.Director.Start());
        for (var i = 0; i < 200 && !rig.Field.Calls.Contains("Refresh"); i++)
            rig.Director.Tick(1f);

        // The lord dies in the wave; the wave clears and the director spawns him again in the intermission.
        rig.Director.OnAgentRemoved("P", true);
        rig.Field.MainBody = 0;
        rig.Director.Tick(1f);
        for (var n = rig.Field.AttackersStanding; n > 0; n--)
        {
            rig.Field.AttackersStanding--;
            rig.Director.OnAgentRemoved("A:orc", true);
        }

        rig.Director.Tick(1f);

        Assert.AreEqual(1, rig.Field.Count("SpawnPlayer:True:archer"), string.Join(",", rig.Field.Calls));
        var lines = rig.Outcomes.Lines.Where(l => l.Contains(LongDefeatChecks.ClassId)).ToList();
        Assert.AreEqual(2, lines.Count, string.Join("\n", rig.Outcomes.Lines));
        StringAssert.StartsWith(lines[1], "pass taom.long-defeat.class m1-d1 ");
    }

    [TestMethod]
    public void TheReport_NamesTheClassThatWasPlayed()
    {
        var rig = new Rig(Archer());

        rig.RunToTheEnd();

        Assert.AreEqual("archer", (string?)JObject.Parse(rig.Director.ReportText)["class"]);
    }
}
