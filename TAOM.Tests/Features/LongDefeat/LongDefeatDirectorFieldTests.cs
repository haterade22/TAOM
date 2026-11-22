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
/// A run on a field scene (#1105 slice 2): no gate is broken and no engine deployed, the scene gives no keep, both sides and the heroes start at the field's
/// own places, and the heroes do not wait for a wall lane. A plan with no <see cref="FieldPlaces"/> runs as a siege, as before.
/// </summary>
[TestClass]
public class LongDefeatDirectorFieldTests
{
    private static readonly MusterFrame DefendersAt = new(11f, 1f, 2f, 0f, 1f);
    private static readonly MusterFrame AttackersAt = new(77f, 3f, 4f, 0f, -1f);
    private static readonly HeroSpawn Boromir = new("boromir", "taom_hero_boromir", 5f);
    private static readonly HeroSpawn Aragorn = new("aragorn", "taom_hero_aragorn", 8f);

    private sealed class Rig
    {
        // The fake counts only the soldiers of a batch, so the lord and the heroes who stand with no garrison are given here.
        public FakeLongDefeatField Field { get; } = new() { RestHeals = true, Starts = new FieldPlaces(DefendersAt, AttackersAt), Defenders = 3 };
        public LongDefeatSession Session { get; } = new();
        public RecordingLogger Log { get; } = new();
        public RecordingOutcomeLog Outcomes { get; } = new();
        public LongDefeatDirector Director { get; set; } = null!;
    }

    private static Rig NewRig(bool field = true, int garrison = 0, HeroSpawn[]? heroes = null, KeepPoint? keep = null, MusterPoints? muster = null)
    {
        var rig = new Rig();
        var rows = garrison > 0 ? new[] { new GarrisonRow("gondor_guard", FormationSlot.Infantry, garrison) } : Array.Empty<GarrisonRow>();
        var plan = new RunPlan(
            "parth_galen",
            rows,
            new WavePlan(new[] { new WaveSpec(40, new[] { new WaveTroop("uruk", FormationSlot.Infantry, 1) }) }, new WaveScaling(10, 300)),
            new ShopPlan(0, Array.Empty<ShopOffer>()),
            RunRules.Default with { PreparationSeconds = 1f, IntermissionSeconds = 60f },
            new RunOptions { StopAfterWaves = 1 })
        {
            FreeHeroes = heroes ?? new[] { Boromir, Aragorn },
            Field = field ? new FieldPlaces(null, null) : null,
            Keep = keep,
            Muster = muster,
            Offered = FortOffer.None,
            Locations = LocationOffer.None,
        };
        var effects = new FakeLongDefeatEffects(rig.Field.Calls);
        rig.Director = new LongDefeatDirector(plan, rig.Field, rig.Session, rig.Log, rig.Outcomes, "m1", null, effects, rig.Field);
        return rig;
    }

    [TestInitialize]
    public void Init() => MissionThreadGuard.ResetForTests();

    [TestCleanup]
    public void Clean() => MissionThreadGuard.ResetForTests();

    [TestMethod]
    public void Start_OnAField_BreaksNoGate_DeploysNoEngine_AndTakesNoKeepFromTheScene()
    {
        var rig = NewRig();

        Assert.IsTrue(rig.Director.Start());

        Assert.AreEqual(0, rig.Field.Count("BreakGates"));
        Assert.AreEqual(0, rig.Field.Count("Deploy"));
        Assert.IsNull(rig.Field.LastReadKeep, "a field has no keep unless the mission records one");
        Assert.IsFalse(rig.Outcomes.Has("fail", LongDefeatChecks.KeepId), "no keep is no fault on a field");
    }

    [TestMethod]
    public void Start_OnAField_ARecordedKeep_IsStillUsed()
    {
        var rig = NewRig(keep: new KeepPoint(5f, 6f, 7f, 12f));

        Assert.IsTrue(rig.Director.Start());

        Assert.AreEqual(new LdPoint(5f, 6f, 7f), rig.Field.LastReadKeep);
    }

    [TestMethod]
    public void Start_OnAField_WritesTheFieldBootLine_AsAPass_WithNoSiegeAi()
    {
        var rig = NewRig();
        rig.Field.Facts = rig.Field.Facts with { SiegeTeamAi = false };

        Assert.IsTrue(rig.Director.Start());

        var boot = rig.Outcomes.Lines.Single(l => l.Contains(" " + LongDefeatChecks.BootId + " "));
        StringAssert.StartsWith(boot, "pass ");
        StringAssert.Contains(boot, "siege_ai=false");
        StringAssert.EndsWith(boot, "field=true");
    }

    [TestMethod]
    public void Start_OnAField_WithSiegeAi_FailsTheBootLine()
    {
        // A field scene opened with the siege list is the wrong world: the line must not read as a pass.
        var rig = NewRig();

        rig.Director.Start();

        StringAssert.StartsWith(rig.Outcomes.Lines.Single(l => l.Contains(" " + LongDefeatChecks.BootId + " ")), "fail ");
    }

    [TestMethod]
    public void Start_OnAField_TheFieldGivesNoStartPlace_RefusesTheRun()
    {
        var rig = NewRig();
        rig.Field.Starts = new FieldPlaces(DefendersAt, null);

        Assert.IsFalse(rig.Director.Start());

        Assert.IsTrue(rig.Outcomes.Has("fail", LongDefeatChecks.BootId));
        Assert.AreEqual(0, rig.Field.Count("Spawn"), "nothing spawns on a field with no place for a side");
    }

    [TestMethod]
    public void OnAField_TheLordSpawnsAlone_AndNoBatchKeepsASlotForHim()
    {
        var rig = NewRig(garrison: 30);
        rig.Field.Facts = rig.Field.Facts with { SiegeTeamAi = false };

        Assert.IsTrue(rig.Director.Start());

        // The garrison musters in batches of 20 (RunRules.MusterBatch); a siege opens the first one for 21, with the lord.
        Assert.AreEqual(1, rig.Field.Count("Place:def:Infantry:20"), string.Join(",", rig.Field.Calls));
        Assert.IsTrue(rig.Field.Index("End:def:Infantry") < rig.Field.Index("SpawnPlayer:False"), "the garrison's batch is closed before the lord spawns");
    }

    [TestMethod]
    public void OnAField_EveryDefenderBatchGoesToTheDefendersStart()
    {
        var rig = NewRig(garrison: 30);

        rig.Director.Start();

        Assert.AreEqual(1, rig.Field.Count("Muster:Infantry:11"));
    }

    [TestMethod]
    public void OnAField_ARecordedMusterPlaceWithNoNavmesh_FallsBackToTheDefendersStart()
    {
        // A field scene has no frame of its own to fall back on: the engine would throw for a formation nobody positioned (Mission.cs:4222).
        var rig = NewRig(garrison: 30, muster: new MusterPoints(new MusterFrame(55f, 0f, 0f, 0f, 1f), null));
        rig.Field.MusterRefusedX = 55f;

        Assert.IsTrue(rig.Director.Start());

        Assert.AreEqual(1, rig.Field.Count("Muster:Infantry:55"), "the recorded place is tried once");
        Assert.AreEqual(1, rig.Field.Count("Muster:Infantry:11"));
        Assert.AreEqual(rig.Field.Index("Muster:Infantry:55") + 1, rig.Field.Index("Muster:Infantry:11"), "the start place follows at once, before the batch spawns");
    }

    [TestMethod]
    public void OnAField_AStartPlaceTheFieldRefuses_EndsTheRunAsAFault()
    {
        var rig = NewRig(garrison: 30);
        rig.Field.MusterRefusedX = 11f;

        Assert.ThrowsException<InvalidOperationException>(() => rig.Director.Start());
    }

    [TestMethod]
    public void OnAField_EveryAttackerBatchGoesToTheAttackersStart_RightAfterItOpens()
    {
        var rig = NewRig();
        rig.Director.Start();

        for (var i = 0; i < 40; i++)
            rig.Director.Tick(0.1f);

        var opened = rig.Field.Count("Place:att:Infantry") + rig.Field.Count("Begin:att:Infantry");
        Assert.IsTrue(opened > 0, "the wave started: " + string.Join(",", rig.Field.Calls));
        for (var i = 0; i < rig.Field.Calls.Count; i++)
        {
            if (rig.Field.Calls[i].StartsWith("Place:att:", StringComparison.Ordinal) || rig.Field.Calls[i].StartsWith("Begin:att:", StringComparison.Ordinal))
                StringAssert.StartsWith(rig.Field.Calls[i + 1], "AttackerMuster:Infantry:77", rig.Field.Calls[i]);
        }
    }

    [TestMethod]
    public void OnAField_AWaveSpawnedOverSeveralTicks_GoesBackToTheStartBeforeEveryPart()
    {
        // Run 20261121-183158: the part of a batch that spawned on a later tick appeared at the formation's own order position, which a ranged
        // formation had already moved toward the enemy (4 of 12 skirmishers over 25 m from their start, beside the defenders).
        var rig = NewRig();
        rig.Director.Start();

        for (var i = 0; i < 40; i++)
            rig.Director.Tick(0.1f);

        var spawns = Enumerable.Range(0, rig.Field.Calls.Count).Where(i => rig.Field.Calls[i].StartsWith("Spawn:att:", StringComparison.Ordinal)).ToList();
        Assert.IsTrue(spawns.Count > 1, "40 attackers at 8 a tick spawn over several ticks: " + string.Join(",", rig.Field.Calls));
        foreach (var i in spawns)
            StringAssert.StartsWith(rig.Field.Calls[i - 1], "AttackerMuster:Infantry:77", "before " + rig.Field.Calls[i]);
    }

    [TestMethod]
    public void ASiegeRun_AContinuedDefenderBatch_IsNotMusteredAgain()
    {
        var rig = NewRig(field: false, garrison: 30, muster: new MusterPoints(new MusterFrame(55f, 0f, 0f, 0f, 1f), null));
        rig.Director.Start();

        var opened = rig.Field.Count("Place:def:Infantry") + rig.Field.Count("Begin:def:Infantry");
        Assert.AreEqual(opened, rig.Field.Count("Muster:Infantry:55"), "as before: once when the batch opens");
    }

    [TestMethod]
    public void OnAField_TheHeroesSpawnAtTheDefendersStart_AndDoNotWaitForAWallLane()
    {
        var rig = NewRig();
        rig.Field.LanesAssigned = false;

        rig.Director.Start();
        rig.Director.Tick(0.1f);

        CollectionAssert.AreEqual(new[] { Boromir, Aragorn }, rig.Field.HeroSpawns.Select(h => h.Hero).ToArray());
        Assert.IsTrue(rig.Field.HeroSpawns.All(h => h.Place == LdPlace.From(DefendersAt)));
        Assert.AreEqual(0, rig.Field.Count("LanesAssigned"), "a field has no wall lane to wait for");
    }

    [TestMethod]
    public void OnAField_TheHeroesAreNotHeldAtAPost_SoNoHoldAndNoHuntersLine()
    {
        // Run 20261121-184542: heroes held at a post in the open were passive (Aragorn landed 4 hits in 6 minutes), while the same heroes under the
        // team AI killed 12 attackers in 52 s (run 20261121-185311). So on a field they stand in their class formations under the AI, for either side.
        var rig = NewRig();
        rig.Field.HeroAnswer = null;
        rig.Director.Start();

        for (var i = 0; i < 60; i++)
            rig.Director.Tick(0.1f);

        Assert.AreEqual(0, rig.Field.Count("HoldHunters"));
        Assert.IsFalse(rig.Outcomes.Lines.Any(l => l.Contains(" " + LongDefeatHeroChecks.HuntersId + " ")), "the hunters line judges a held group, and a field has none");
    }

    [TestMethod]
    public void OnAField_WithASecondAttackerPlace_AGroupStartsAtThePlaceFartherFromTheDefenders()
    {
        var behind = new MusterFrame(-30f, 0f, 0f, 0f, 1f);
        var rig = NewRig(garrison: 30);
        rig.Field.Starts = new FieldPlaces(DefendersAt, AttackersAt, behind);
        rig.Field.SoldiersMoveOn = false;
        rig.Director.Start();

        for (var i = 0; i < 40; i++)
            rig.Director.Tick(0.1f);

        // The fake's first 20 defenders stand at x 0 to 19, mean 9.5: about 40 from the place behind (x -30) and 68 from the place ahead (x 77).
        Assert.IsTrue(rig.Field.Count("AttackerMuster:Infantry:77") > 0, string.Join(",", rig.Field.Calls));
        Assert.AreEqual(0, rig.Field.Count("AttackerMuster:Infantry:-30"));
    }

    [TestMethod]
    public void OnAField_WhenThePlayerAttacks_HisBandKeepsItsOneStartPlace()
    {
        var behind = new MusterFrame(500f, 0f, 0f, 0f, 1f);
        var rig = NewRig(garrison: 30);
        rig.Field.Starts = new FieldPlaces(DefendersAt, AttackersAt, behind);
        rig.Field.Facts = rig.Field.Facts with { PlayerAttacks = true };
        rig.Field.SoldiersMoveOn = false;
        rig.Director.Start();

        for (var i = 0; i < 40; i++)
            rig.Director.Tick(0.1f);

        Assert.IsTrue(rig.Field.Count("AttackerMuster:Infantry:77") > 0, string.Join(",", rig.Field.Calls));
        Assert.AreEqual(0, rig.Field.Count("AttackerMuster:Infantry:500"), "the place behind is far from every defender, and still not used");
    }

    [TestMethod]
    public void OnAField_TheStateLine_NeverAsksForTheSiegeEngines()
    {
        var rig = NewRig();
        rig.Director.Start();

        for (var i = 0; i < 80; i++)
            rig.Director.Tick(0.1f);

        Assert.AreEqual(0, rig.Field.EngineDescriptions);
    }

    [TestMethod]
    public void ASiegeRun_NeverAsksTheFieldForStartPlaces_AndNoAttackerBatchMusters()
    {
        var rig = NewRig(field: false, garrison: 30);
        rig.Director.Start();

        for (var i = 0; i < 40; i++)
            rig.Director.Tick(0.1f);

        Assert.AreEqual(0, rig.Field.Count("FieldStarts"));
        Assert.AreEqual(0, rig.Field.Count("AttackerMuster:"));
        Assert.AreEqual(1, rig.Field.Count("BreakGates"));
        Assert.AreEqual(1, rig.Field.Count("Deploy"));
        Assert.AreEqual(1, rig.Field.Count("Place:def:Infantry:21"), "a siege keeps the lord's slot in the foot batch");
    }
}
