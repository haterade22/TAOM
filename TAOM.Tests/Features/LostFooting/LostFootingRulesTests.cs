using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.LostFooting;
using TAOM.Features.LostFooting.Domain;

namespace TAOM.Tests.Features.LostFooting;

/// <summary>The pure Lost Footing rules: kinds, the three stranded classes, episodes, samples, hotspots, the pass cursor and pruning.</summary>
[TestClass]
public class LostFootingRulesTests
{
    private static readonly LostFootingTunables T = new LostFootingTunables();

    private LostFootingMissionState _s = null!;

    [TestInitialize]
    public void Setup() => _s = new LostFootingMissionState { SceneWaterLevel = 5f };

    private StrandEntry? Obs(AgentProbe p, float now) => LostFootingRules.Observe(_s, p, now, T);

    private static AgentProbe Soldier(object? handle = null) => new AgentProbe { Handle = handle ?? new object(), Speed = 3f, DistToDestination = 20f };

    private static AgentProbe Wading(object? handle = null) => new AgentProbe
    {
        Handle = handle ?? new object(), InWaterMode = true, Speed = 0f, DistToDestination = 20f, X = 10f, Y = 10f, Z = 4f,
    };

    private static AgentProbe OffMesh(object? handle = null) => new AgentProbe { Handle = handle ?? new object(), FaceMiss = true };

    // ----- kinds

    [TestMethod]
    public void Classify_ARiderlessMount_IsOne() =>
        Assert.AreEqual(AgentKind.RiderlessMount, LostFootingRules.Classify(new AgentProbe { IsHuman = false, IsMount = true }));

    [TestMethod]
    public void Classify_AHumanInAFormation_IsASoldier() =>
        Assert.AreEqual(AgentKind.Soldier, LostFootingRules.Classify(new AgentProbe { IsHuman = true, InFormation = true }));

    [TestMethod]
    public void Classify_AHumanOutsideAnyFormation_IsOther() =>
        Assert.AreEqual(AgentKind.Other, LostFootingRules.Classify(new AgentProbe { IsHuman = true, InFormation = false }));

    [TestMethod]
    public void Classify_ATrollWithATree_IsACreature_EvenAsAMount() =>
        Assert.AreEqual(AgentKind.Creature, LostFootingRules.Classify(new AgentProbe { IsHuman = false, IsMount = true, IsCreature = true }));

    [TestMethod]
    public void Classify_ARouterInAFormation_IsARouter_NotASoldier() =>
        Assert.AreEqual(AgentKind.Router, LostFootingRules.Classify(new AgentProbe { IsHuman = true, InFormation = true, IsRouter = true }));

    [TestMethod]
    public void Classify_AnAgentUsingAnObject_BeatsEveryOtherKind() =>
        Assert.AreEqual(AgentKind.UsingObject,
            LostFootingRules.Classify(new AgentProbe { IsHuman = true, InFormation = true, IsRouter = true, IsUsingObject = true }));

    // ----- exclusions

    [TestMethod]
    public void Observe_APlayerControlledAgent_IsNeverTracked()
    {
        var p = Wading();
        p.IsPlayerControlled = true;

        Assert.IsNull(Obs(p, 0f));
        Assert.IsNull(Obs(p, 50f));
        Assert.AreEqual(0, _s.Entries.Count);
    }

    // ----- off mesh

    [TestMethod]
    public void OffMesh_OneFaceMiss_IsOnlyASuspect()
    {
        var e = Obs(OffMesh(), 10f)!;

        Assert.IsFalse(e.OffMesh);
        Assert.IsFalse(e.IsStranded);
    }

    [TestMethod]
    public void OffMesh_AMissHeldForTheConfirmSeconds_IsOffMesh()
    {
        var h = new object();
        Obs(OffMesh(h), 10f);

        var e = Obs(OffMesh(h), 10f + T.OffMeshConfirmSeconds)!;

        Assert.IsTrue(e.OffMesh);
        Assert.IsTrue(e.IsStranded);
    }

    [TestMethod]
    public void OffMesh_ASecondMissBeforeTheConfirmSeconds_IsStillASuspect()
    {
        var h = new object();
        Obs(OffMesh(h), 10f);

        Assert.IsFalse(Obs(OffMesh(h), 10f + T.OffMeshConfirmSeconds - 0.5f)!.OffMesh);
    }

    [TestMethod]
    public void OffMesh_AnOnMeshSightingBetween_ResetsTheSuspicion()
    {
        var h = new object();
        Obs(OffMesh(h), 10f);
        Obs(Soldier(h), 11f);

        var e = Obs(OffMesh(h), 10f + T.OffMeshConfirmSeconds)!;

        Assert.IsFalse(e.OffMesh, "the clock restarted at the new miss");
    }

    [TestMethod]
    public void OffMesh_BackOnMesh_ClearsTheFlag_AndTheNextEpisodeConfirmsAgain()
    {
        var h = new object();
        Obs(OffMesh(h), 0f);
        var e = Obs(OffMesh(h), 3f)!;
        e.ConfirmTried = true;

        Obs(Soldier(h), 4f);

        Assert.IsFalse(e.OffMesh);
        Assert.IsFalse(e.ConfirmTried);
        Assert.IsNull(e.Detail);
    }

    // ----- water, still

    [TestMethod]
    public void Water_StillAndFarFromItsDestination_IsAdmittedAfterTheStillSeconds()
    {
        var h = new object();
        Assert.IsFalse(Obs(Wading(h), 0f)!.WaterStill, "first sighting only starts the clock");

        Assert.IsTrue(Obs(Wading(h), T.WaterStillSeconds)!.WaterStill);
    }

    [TestMethod]
    public void Water_StillForLessThanTheStillSeconds_IsNotAdmitted()
    {
        var h = new object();
        Obs(Wading(h), 0f);

        Assert.IsFalse(Obs(Wading(h), T.WaterStillSeconds - 0.5f)!.WaterStill);
    }

    [TestMethod]
    public void Water_MovingAtTheStillSpeed_IsNotAdmitted()
    {
        var h = new object();
        var p = Wading(h);
        p.Speed = T.StillSpeed;
        Obs(p, 0f);

        Assert.IsFalse(Obs(p, 30f)!.WaterStill, "speed must be under the still speed");
    }

    [TestMethod]
    public void Water_NearItsDestination_IsNotAdmitted()
    {
        var h = new object();
        var p = Wading(h);
        p.DistToDestination = T.MinDestinationMeters - 0.1f;
        Obs(p, 0f);

        Assert.IsFalse(Obs(p, 30f)!.WaterStill);
    }

    [TestMethod]
    public void Water_ExactlyTheMinimumDestinationDistance_IsAdmitted()
    {
        var h = new object();
        var p = Wading(h);
        p.DistToDestination = T.MinDestinationMeters;
        Obs(p, 0f);

        Assert.IsTrue(Obs(p, 30f)!.WaterStill);
    }

    [DataTestMethod]
    [DataRow(float.NaN)]
    [DataRow(float.PositiveInfinity)]
    [DataRow(float.NegativeInfinity)]
    public void Water_ANonFiniteSpeed_FailsTheGate(float speed)
    {
        var h = new object();
        var p = Wading(h);
        p.Speed = speed;
        Obs(p, 0f);

        Assert.IsFalse(Obs(p, 30f)!.WaterStill);
    }

    [DataTestMethod]
    [DataRow(float.NaN)]
    [DataRow(float.PositiveInfinity)]
    public void Water_ANonFiniteDestinationDistance_FailsTheGate(float distance)
    {
        var h = new object();
        var p = Wading(h);
        p.DistToDestination = distance;
        Obs(p, 0f);

        Assert.IsFalse(Obs(p, 30f)!.WaterStill);
    }

    [TestMethod]
    public void Water_OnLandMode_IsNotTheWaterClass()
    {
        var h = new object();
        var p = Wading(h);
        p.InWaterMode = false;
        p.Z = 100f;
        Obs(p, 0f);

        Assert.IsFalse(Obs(p, 30f)!.WaterStill);
    }

    [TestMethod]
    public void Water_MovingOnce_RestartsTheStillClock()
    {
        var h = new object();
        Obs(Wading(h), 0f);
        var moving = Wading(h);
        moving.Speed = 3f;
        Obs(moving, 1f);

        Assert.IsFalse(Obs(Wading(h), 1f + T.WaterStillSeconds - 0.5f)!.WaterStill);
    }

    // ----- land mode, below the water

    private static AgentProbe Sunk(object? handle = null) => new AgentProbe { Handle = handle ?? new object(), Speed = 0f, Z = 4f, X = 3f, Y = 3f };

    [TestMethod]
    public void LandBelowWater_SlowAndBelowTheSceneLevel_IsAdmittedAfterTheStillSeconds()
    {
        var h = new object();
        Obs(Sunk(h), 0f);

        var e = Obs(Sunk(h), T.WaterStillSeconds)!;

        Assert.IsTrue(e.LandBelow);
        Assert.IsFalse(e.WaterStill);
    }

    [TestMethod]
    public void LandBelowWater_AboveTheSceneLevel_IsNotAdmitted()
    {
        var h = new object();
        var p = Sunk(h);
        p.Z = 5f;
        Obs(p, 0f);

        Assert.IsFalse(Obs(p, 30f)!.LandBelow, "standing exactly at the level is not below it");
    }

    [TestMethod]
    public void LandBelowWater_MovingFast_IsNotAdmitted()
    {
        var h = new object();
        var p = Sunk(h);
        p.Speed = 3f;
        Obs(p, 0f);

        Assert.IsFalse(Obs(p, 30f)!.LandBelow);
    }

    [DataTestMethod]
    [DataRow(float.NaN)]
    [DataRow(float.PositiveInfinity)]
    [DataRow(float.NegativeInfinity)]
    public void LandBelowWater_ANonFiniteSceneWaterLevel_FailsTheGate(float level)
    {
        _s.SceneWaterLevel = level;
        var h = new object();
        Obs(Sunk(h), 0f);

        Assert.IsFalse(Obs(Sunk(h), 30f)!.LandBelow);
    }

    [TestMethod]
    public void LandBelowWater_ANonFiniteSpeedOrHeight_FailsTheGate()
    {
        var h = new object();
        var nanSpeed = Sunk(h);
        nanSpeed.Speed = float.NaN;
        Obs(nanSpeed, 0f);
        Assert.IsFalse(Obs(nanSpeed, 30f)!.LandBelow);

        var g = new object();
        var nanZ = Sunk(g);
        nanZ.Z = float.NaN;
        Obs(nanZ, 0f);
        Assert.IsFalse(Obs(nanZ, 30f)!.LandBelow);
    }

    [TestMethod]
    public void LandBelowWater_InWaterMode_BelongsToTheWaterClassInstead()
    {
        var h = new object();
        var p = Sunk(h);
        p.InWaterMode = true;
        p.DistToDestination = 20f;
        Obs(p, 0f);

        var e = Obs(p, 30f)!;

        Assert.IsFalse(e.LandBelow);
        Assert.IsTrue(e.WaterStill);
    }

    // ----- sample facts and episodes

    [TestMethod]
    public void Observe_RecordsThePositionAndDistancesOfTheLastSighting()
    {
        var p = Wading();
        p.DistToFormation = 7f;
        p.WaterLevelHere = 6f;

        var e = Obs(p, 3f)!;

        Assert.AreEqual(10f, e.X);
        Assert.AreEqual(4f, e.Z);
        Assert.AreEqual(2f, e.Depth, 0.001f, "water level minus height");
        Assert.AreEqual(7f, e.DistToFormation);
        Assert.AreEqual(20f, e.DistToDestination);
        Assert.AreEqual(3f, e.LastSeen);
    }

    [TestMethod]
    public void Episode_StartsAtTheEarliestEvidence_AndCountsOnceItEnds()
    {
        var h = new object();
        Obs(Wading(h), 10f);
        var e = Obs(Wading(h), 12f)!;
        Assert.AreEqual(10f, e.StrandedSince, "the episode began when the still clock started");
        Assert.AreEqual(0, _s.Episodes, "still open");

        var moving = Wading(h);
        moving.Speed = 3f;
        Obs(moving, 20f);

        Assert.AreEqual(1, _s.Episodes);
        Assert.AreEqual(10f, _s.LongestEpisode, 0.001f);
        Assert.IsTrue(float.IsNaN(e.StrandedSince));
    }

    [TestMethod]
    public void Episode_TheLongestOfSeveralIsKept()
    {
        var a = new object();
        var b = new object();
        Obs(Wading(a), 0f);
        Obs(Wading(a), 3f);
        Obs(Wading(b), 0f);
        Obs(Wading(b), 3f);
        var free = Wading(a);
        free.Speed = 3f;
        Obs(free, 5f);
        var freeB = Wading(b);
        freeB.Speed = 3f;
        Obs(freeB, 12f);

        Assert.AreEqual(2, _s.Episodes);
        Assert.AreEqual(12f, _s.LongestEpisode, 0.001f);
    }

    [TestMethod]
    public void CloseEpisodes_CountsTheOpenOnes_AtTheirLastSighting()
    {
        var h = new object();
        Obs(Wading(h), 0f);
        Obs(Wading(h), 4f);

        LostFootingRules.CloseEpisodes(_s);
        LostFootingRules.CloseEpisodes(_s);

        Assert.AreEqual(1, _s.Episodes, "closing twice never double counts");
        Assert.AreEqual(4f, _s.LongestEpisode, 0.001f);
    }

    // ----- tally and peaks

    [TestMethod]
    public void Tally_CountsEachClassByKind_AndTracksThePeak()
    {
        var router = Wading();
        router.IsRouter = true;
        var creature = Wading();
        creature.IsCreature = true;
        foreach (var p in new[] { Wading(), router, creature })
        {
            Obs(p, 0f);
            Obs(p, 3f);
        }

        var tally = LostFootingRules.Tally(_s);

        Assert.AreEqual(3, tally.Count(StrandClass.WaterStill));
        Assert.AreEqual(1, tally.Count(StrandClass.WaterStill, AgentKind.Soldier));
        Assert.AreEqual(1, tally.Count(StrandClass.WaterStill, AgentKind.Router));
        Assert.AreEqual(1, tally.Count(StrandClass.WaterStill, AgentKind.Creature));
        Assert.AreEqual(0, tally.Count(StrandClass.OffMesh));
        Assert.AreEqual(3, _s.PeakWaterStill);

        foreach (var e in _s.Entries.Values.ToList())
            e.WaterStill = false;
        LostFootingRules.Tally(_s);
        Assert.AreEqual(3, _s.PeakWaterStill, "a peak never goes down");
    }

    [TestMethod]
    public void Tally_AnAgentInTwoClasses_CountsInBoth()
    {
        var h = new object();
        var p = Wading(h);
        p.FaceMiss = true;
        Obs(p, 0f);
        Obs(p, 3f);

        var tally = LostFootingRules.Tally(_s);

        Assert.AreEqual(1, tally.Count(StrandClass.OffMesh));
        Assert.AreEqual(1, tally.Count(StrandClass.WaterStill));
    }

    [TestMethod]
    public void Tally_OnlyConfirmedOffMesh_AreCountedAsConfirmed()
    {
        var a = new object();
        var b = new object();
        Obs(OffMesh(a), 0f);
        Obs(OffMesh(b), 0f);
        Obs(OffMesh(a), 3f)!.Detail = new OffMeshDetail { Confirmed = true };
        Obs(OffMesh(b), 3f)!.Detail = new OffMeshDetail { Confirmed = false };

        var tally = LostFootingRules.Tally(_s);

        Assert.AreEqual(2, tally.Count(StrandClass.OffMesh));
        Assert.AreEqual(1, tally.Confirmed);
        Assert.AreEqual(1, _s.PeakConfirmed);
    }

    [TestMethod]
    public void Tally_NothingStranded_IsZero()
    {
        Obs(Soldier(), 0f);

        var tally = LostFootingRules.Tally(_s);

        foreach (var c in LostFootingTally.Classes)
            Assert.AreEqual(0, tally.Count(c), c.ToString());
    }

    // ----- samples

    [TestMethod]
    public void Samples_AreCappedAtTheLimit_LongestStrandedFirst()
    {
        var handles = Enumerable.Range(0, 8).Select(_ => new object()).ToList();
        for (var i = 0; i < handles.Count; i++)
        {
            var p = Wading(handles[i]);
            Obs(p, i);
            Obs(p, i + 3f);
        }

        var samples = LostFootingRules.Samples(_s, StrandClass.WaterStill, 5);

        Assert.AreEqual(5, samples.Count);
        Assert.AreEqual(0f, samples[0].StrandedSince);
        Assert.AreEqual(4f, samples[4].StrandedSince);
    }

    [TestMethod]
    public void Samples_ZeroLimit_ReturnsNone()
    {
        var h = new object();
        Obs(Wading(h), 0f);
        Obs(Wading(h), 3f);

        Assert.AreEqual(0, LostFootingRules.Samples(_s, StrandClass.WaterStill, 0).Count);
    }

    [TestMethod]
    public void Samples_OnlyTheRequestedClass()
    {
        var h = new object();
        Obs(Wading(h), 0f);
        Obs(Wading(h), 3f);

        Assert.AreEqual(0, LostFootingRules.Samples(_s, StrandClass.OffMesh, 5).Count);
        Assert.AreEqual(1, LostFootingRules.Samples(_s, StrandClass.WaterStill, 5).Count);
    }

    // ----- hotspots

    private void Strand(object h, float x, float y, float from, float to)
    {
        var p = Wading(h);
        p.X = x;
        p.Y = y;
        for (var t = from; t <= to; t += 1f)
            Obs(p, t);
    }

    [TestMethod]
    public void Hotspots_AddTheSecondsBetweenSightings_OfAStrandedAgent()
    {
        Strand(new object(), 12f, 7f, 0f, 10f);

        var cells = LostFootingRules.Hotspots(_s, 5);

        Assert.AreEqual(1, cells.Count);
        Assert.AreEqual(2, cells[0].CellX);
        Assert.AreEqual(1, cells[0].CellY);
        Assert.AreEqual(9f, cells[0].AgentSeconds, 0.001f, "sightings at 2 to 10 are stranded, one second apart; the first two build the evidence");
    }

    [TestMethod]
    public void Hotspots_AnAgentThatIsNotStranded_AddsNothing()
    {
        var p = Soldier();
        p.X = 1f;
        for (var t = 0f; t < 10f; t += 1f)
            Obs(p, t);

        Assert.AreEqual(0, LostFootingRules.Hotspots(_s, 5).Count);
    }

    [TestMethod]
    public void Hotspots_AreTheTopFive_ByAgentSeconds_TiesByCell()
    {
        for (var i = 0; i < 7; i++)
            Strand(new object(), i * 10f + 1f, 1f, 0f, 3f + (i == 6 ? 20 : 0));

        var cells = LostFootingRules.Hotspots(_s, LostFootingRules.TopHotspots);

        Assert.AreEqual(5, cells.Count);
        Assert.AreEqual(12, cells[0].CellX, "the longest strand first");
        Assert.AreEqual(0, cells[1].CellX, "then equal seconds in cell order");
        Assert.AreEqual(2, cells[2].CellX);
        Assert.AreEqual(4, cells[3].CellX);
        Assert.AreEqual(6, cells[4].CellX);
    }

    [TestMethod]
    public void Hotspots_NegativeCoordinates_FloorToTheirOwnCell()
    {
        Strand(new object(), -0.5f, -12f, 0f, 5f);

        var cell = LostFootingRules.Hotspots(_s, 5).Single();

        Assert.AreEqual(-1, cell.CellX);
        Assert.AreEqual(-3, cell.CellY);
    }

    // ----- the pass cursor

    [TestMethod]
    public void PlanSlice_TakesAtMostMax_AndAdvances()
    {
        var plan = LostFootingRules.PlanSlice(0, 64, 200);

        Assert.AreEqual(0, plan.Start);
        Assert.AreEqual(64, plan.Take);
        Assert.AreEqual(64, plan.Next);
        Assert.IsFalse(plan.Wrapped);
    }

    [TestMethod]
    public void PlanSlice_TheLastSlice_IsShort_AndWraps()
    {
        var plan = LostFootingRules.PlanSlice(192, 64, 200);

        Assert.AreEqual(192, plan.Start);
        Assert.AreEqual(8, plan.Take);
        Assert.AreEqual(0, plan.Next);
        Assert.IsTrue(plan.Wrapped);
    }

    [TestMethod]
    public void PlanSlice_AShrunkenList_RestartsAtZero()
    {
        var plan = LostFootingRules.PlanSlice(500, 64, 100);

        Assert.AreEqual(0, plan.Start);
        Assert.AreEqual(64, plan.Take);
        Assert.AreEqual(64, plan.Next);
    }

    [TestMethod]
    public void PlanSlice_AnEmptyList_TakesNothing_AndIsAFinishedSweep()
    {
        var plan = LostFootingRules.PlanSlice(5, 64, 0);

        Assert.AreEqual(0, plan.Take);
        Assert.AreEqual(0, plan.Next);
        Assert.IsTrue(plan.Wrapped, "an empty sweep still ends, so stale entries age out");
    }

    [TestMethod]
    public void PlanSlice_AListExactlyMax_WrapsInOnePass()
    {
        var plan = LostFootingRules.PlanSlice(0, 64, 64);

        Assert.AreEqual(64, plan.Take);
        Assert.IsTrue(plan.Wrapped);
        Assert.AreEqual(0, plan.Next);
    }

    // ----- pruning

    [TestMethod]
    public void EndSweep_KeepsAnEntryUnseenForOneSweep_ThenPrunesIt()
    {
        var h = new object();
        Obs(Soldier(h), 0f);

        LostFootingRules.EndSweep(_s);
        Assert.AreEqual(1, _s.Entries.Count, "seen in the sweep just ended");
        LostFootingRules.EndSweep(_s);
        Assert.AreEqual(1, _s.Entries.Count, "unseen for one sweep");
        LostFootingRules.EndSweep(_s);
        Assert.AreEqual(0, _s.Entries.Count, "unseen for two complete sweeps");
    }

    [TestMethod]
    public void EndSweep_ASeenAgentSurvives()
    {
        var h = new object();
        for (var i = 0; i < 6; i++)
        {
            Obs(Soldier(h), i);
            LostFootingRules.EndSweep(_s);
        }

        Assert.AreEqual(1, _s.Entries.Count);
    }

    [TestMethod]
    public void EndSweep_PruningAStrandedAgent_ClosesItsEpisode()
    {
        var h = new object();
        Obs(Wading(h), 0f);
        Obs(Wading(h), 3f);

        for (var i = 0; i < 4; i++)
            LostFootingRules.EndSweep(_s);

        Assert.AreEqual(0, _s.Entries.Count);
        Assert.AreEqual(1, _s.Episodes);
        Assert.AreEqual(3f, _s.LongestEpisode, 0.001f);
    }

    [TestMethod]
    public void Entries_AreKeyedOnTheObject_NotOnEquality()
    {
        var a = new AlwaysEqual();
        var b = new AlwaysEqual();

        Obs(Soldier(a), 0f);
        Obs(Soldier(b), 0f);

        Assert.AreEqual(2, _s.Entries.Count);
    }

    private sealed class AlwaysEqual
    {
        public override bool Equals(object? obj) => true;

        public override int GetHashCode() => 1;
    }

    [TestMethod]
    public void NoCensusIsDue_WhenNothingIsStranded()
    {
        Obs(Soldier(), 0f);
        Obs(OffMesh(), 0f);

        Assert.AreEqual(0, LostFootingRules.Tally(_s).Count(StrandClass.OffMesh), "a suspect is not stranded, so no census line follows");
    }

    [TestMethod]
    public void Observe_CarriesTheUnreachableFlagOntoTheEntry()
    {
        var p = Soldier();
        p.Unreachable = true;

        Assert.IsTrue(Obs(p, 0f)!.Unreachable);
    }

    // ----- where the agent is going (F2)

    private AgentProbe Goal(float toFormation, float toDest)
    {
        var p = Wading();
        p.DistToFormation = toFormation;
        p.DistToDestination = toDest;
        return p;
    }

    private bool Admitted(float toFormation, float toDest)
    {
        var h = new object();
        var p = Goal(toFormation, toDest);
        p.Handle = h;
        Obs(p, 0f);
        return Obs(p, 30f)!.WaterStill;
    }

    [TestMethod]
    public void Goal_TheFormationPosition_WinsWhenItIsKnown()
    {
        Assert.IsTrue(Admitted(20f, 0f), "far from the formation spot, whatever the move destination says");
        Assert.IsFalse(Admitted(1f, 20f), "next to the formation spot, so nowhere to go");
    }

    [TestMethod]
    public void Goal_NoFormationPosition_FallsBackToTheMoveDestination()
    {
        Assert.IsTrue(Admitted(float.NaN, 20f));
        Assert.IsFalse(Admitted(float.NaN, 1f));
    }

    [TestMethod]
    public void Goal_NeitherKnown_FailsTheGate()
    {
        Assert.IsFalse(Admitted(float.NaN, float.NaN));
        Assert.IsFalse(Admitted(float.PositiveInfinity, float.NaN));
        Assert.IsFalse(Admitted(float.NaN, float.PositiveInfinity));
    }

    [TestMethod]
    public void Goal_AKnownFormationDistance_IsNotOverriddenByABadDestination() =>
        Assert.IsTrue(Admitted(20f, float.NaN));

    // ----- the earliest evidence starts the episode

    [TestMethod]
    public void Episode_StartsAtTheEarliestOfTwoClassesThatCountTogether()
    {
        var slow = new LostFootingTunables { OffMeshConfirmSeconds = 10f, WaterStillSeconds = 5f };
        var h = new object();
        var miss = Soldier(h);
        miss.FaceMiss = true;
        var both = Wading(h);
        both.FaceMiss = true;

        LostFootingRules.Observe(_s, miss, 0f, slow);
        LostFootingRules.Observe(_s, both, 5f, slow);
        var e = LostFootingRules.Observe(_s, both, 10f, slow)!;

        Assert.IsTrue(e.OffMesh && e.WaterStill);
        Assert.AreEqual(0f, e.StrandedSince, "off-mesh evidence began at 0, water at 5");
    }

    // ----- how an episode ended (F9)

    [TestMethod]
    public void Episode_Recovered_IsCountedByKind()
    {
        var h = new object();
        var router = Wading(h);
        router.IsRouter = true;
        Obs(router, 0f);
        Obs(router, 3f);
        var free = Soldier(h);
        free.IsRouter = true;

        Obs(free, 5f);

        Assert.AreEqual(1, _s.Recovered);
        Assert.AreEqual(0, _s.Removed + _s.AtEnd);
        Assert.AreEqual(1, _s.EpisodeKinds[(int)AgentKind.Router]);
        Assert.AreEqual(1, _s.Episodes);
    }

    [TestMethod]
    public void Episode_Removed_WhenThePruneDropsAStrandedAgent()
    {
        var h = new object();
        Obs(Wading(h), 0f);
        Obs(Wading(h), 3f);

        for (var i = 0; i < 4; i++)
            LostFootingRules.EndSweep(_s);

        Assert.AreEqual(1, _s.Removed);
        Assert.AreEqual(0, _s.Recovered + _s.AtEnd);
        Assert.AreEqual(1, _s.EpisodeKinds[(int)AgentKind.Soldier]);
    }

    [TestMethod]
    public void Episode_AtEnd_WhenTheBattleEndsWhileStranded()
    {
        var h = new object();
        Obs(Wading(h), 0f);
        Obs(Wading(h), 3f);

        LostFootingRules.CloseEpisodes(_s);

        Assert.AreEqual(1, _s.AtEnd);
        Assert.AreEqual(0, _s.Recovered + _s.Removed);
    }

    // ----- a pause is not bridged (F16)

    [TestMethod]
    public void Pause_ClosesTheOpenEpisodes_AndForgetsEveryAgent()
    {
        var h = new object();
        Obs(Wading(h), 0f);
        Obs(Wading(h), 3f);

        LostFootingRules.Pause(_s);

        Assert.AreEqual(0, _s.Entries.Count);
        Assert.AreEqual(1, _s.AtEnd);
        Assert.AreEqual(0, _s.Cursor);
    }

    // ----- a position the engine could not give adds no hotspot (F16)

    [DataTestMethod]
    [DataRow(float.NaN, 5f)]
    [DataRow(float.PositiveInfinity, 5f)]
    [DataRow(float.NegativeInfinity, 5f)]
    [DataRow(5f, float.NaN)]
    [DataRow(5f, float.PositiveInfinity)]
    [DataRow(5f, float.NegativeInfinity)]
    public void Hotspots_ANonFinitePosition_AddsNothing(float x, float y)
    {
        var h = new object();
        var p = Wading(h);
        p.X = x;
        p.Y = y;
        for (var t = 0f; t <= 10f; t += 1f)
            Obs(p, t);

        Assert.AreEqual(0, LostFootingRules.Hotspots(_s, 5).Count);
    }
}
