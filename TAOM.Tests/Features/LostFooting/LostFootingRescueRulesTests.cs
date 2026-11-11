using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.LostFooting;
using TAOM.Features.LostFooting.Domain;

namespace TAOM.Tests.Features.LostFooting;

/// <summary>Phase 2 (#894): which stranded agent gets which rescue, and when. Pure rules over the census entry.</summary>
[TestClass]
public class LostFootingRescueRulesTests
{
    private readonly LostFootingTunables _t = new LostFootingTunables();

    private static OffMeshDetail Confirmed() => new OffMeshDetail { Confirmed = true };

    private StrandEntry OffMeshSoldier(float since = 0f) => new StrandEntry
    {
        Kind = AgentKind.Soldier, OffMesh = true, MissSince = since, Detail = Confirmed(), Speed = 0f,
    };

    [TestMethod]
    public void AConfirmedOffMeshSoldier_IsMovedNear_OnceTheNearWaitHasPassed()
    {
        var e = OffMeshSoldier();

        Assert.AreEqual(RescueMove.None, LostFootingRules.PlanRescue(e, _t.RescueNearAfterSeconds - 0.1f, _t).Move);
        var plan = LostFootingRules.PlanRescue(e, _t.RescueNearAfterSeconds, _t);
        Assert.AreEqual(RescueMove.Near, plan.Move);
        Assert.AreEqual(RescueCause.OffMesh, plan.Cause);
        Assert.IsFalse(plan.NeedLand);
    }

    [TestMethod]
    public void AfterTheNearTry_TheFormationMoveWaitsForTheFarWait()
    {
        var e = OffMeshSoldier();
        e.NearTried = true;

        Assert.AreEqual(RescueMove.None, LostFootingRules.PlanRescue(e, _t.RescueFarAfterSeconds - 0.1f, _t).Move);
        Assert.AreEqual(RescueMove.Formation, LostFootingRules.PlanRescue(e, _t.RescueFarAfterSeconds, _t).Move);
    }

    [TestMethod]
    public void BothTriesSpent_NothingMoreThisEpisode()
    {
        var e = OffMeshSoldier();
        e.NearTried = true;
        e.FarTried = true;

        Assert.AreEqual(RescueMove.None, LostFootingRules.PlanRescue(e, 1000f, _t).Move);
    }

    [DataTestMethod]
    [DataRow(AgentKind.Router)]
    [DataRow(AgentKind.Creature)]
    [DataRow(AgentKind.UsingObject)]
    [DataRow(AgentKind.Other)]
    public void AKindTheIssueRulesOut_IsNeverMoved(AgentKind kind)
    {
        var e = OffMeshSoldier();
        e.Kind = kind;

        Assert.AreEqual(RescueMove.None, LostFootingRules.PlanRescue(e, 1000f, _t).Move);
    }

    [TestMethod]
    public void ARiderlessMount_GetsTheNearMoveOnly()
    {
        var e = OffMeshSoldier();
        e.Kind = AgentKind.RiderlessMount;

        Assert.AreEqual(RescueMove.Near, LostFootingRules.PlanRescue(e, 1000f, _t).Move);
        e.NearTried = true;
        Assert.AreEqual(RescueMove.None, LostFootingRules.PlanRescue(e, 1000f, _t).Move);
    }

    [TestMethod]
    public void AnUnconfirmedMiss_OrADynamicNavMesh_IsNeverMoved()
    {
        var e = OffMeshSoldier();
        e.Detail = null;
        Assert.AreEqual(RescueMove.None, LostFootingRules.PlanRescue(e, 1000f, _t).Move, "the second look has not run");

        e.Detail = new OffMeshDetail { Confirmed = false };
        Assert.AreEqual(RescueMove.None, LostFootingRules.PlanRescue(e, 1000f, _t).Move);

        e.Detail = new OffMeshDetail { Confirmed = true, OnDynamicNavMesh = true };
        Assert.AreEqual(RescueMove.None, LostFootingRules.PlanRescue(e, 1000f, _t).Move, "a siege tower deck or a ship moves with its agents");
    }

    [DataTestMethod]
    [DataRow(1f)]
    [DataRow(float.NaN)]
    public void AMovingAgent_OrAnUnknownSpeed_IsNotMoved(float speed)
    {
        var e = OffMeshSoldier();
        e.Speed = speed;

        Assert.AreEqual(RescueMove.None, LostFootingRules.PlanRescue(e, 1000f, _t).Move);
    }

    [TestMethod]
    public void ASoldierStillInWater_IsMovedToLand()
    {
        var e = new StrandEntry { Kind = AgentKind.Soldier, WaterStill = true, WaterSince = 0f, Speed = 0f };

        var plan = LostFootingRules.PlanRescue(e, _t.RescueNearAfterSeconds, _t);

        Assert.AreEqual(RescueMove.Near, plan.Move);
        Assert.AreEqual(RescueCause.Water, plan.Cause);
        Assert.IsTrue(plan.NeedLand);
    }

    [TestMethod]
    public void ASoldierOnLandBelowTheWater_IsMovedToLand()
    {
        var e = new StrandEntry { Kind = AgentKind.Soldier, LandBelow = true, LandSince = 0f, Speed = 0f };

        var plan = LostFootingRules.PlanRescue(e, _t.RescueNearAfterSeconds, _t);

        Assert.AreEqual(RescueCause.Water, plan.Cause);
        Assert.IsTrue(plan.NeedLand);
    }

    // ----- the sightings that feed the rescue

    private static AgentProbe Probe() => new AgentProbe { Speed = 0f, DistToFormation = 30f, X = 1f, Y = 1f, Z = 1f };

    [TestMethod]
    public void Observe_AFreedAgent_GetsBothTriesBack()
    {
        var state = new LostFootingMissionState();
        var p = Probe();
        p.FaceMiss = true;
        var e = LostFootingRules.Observe(state, p, 0f, _t)!;
        e.NearTried = true;
        e.FarTried = true;

        p.FaceMiss = false;
        LostFootingRules.Observe(state, p, 1f, _t);

        Assert.IsFalse(e.NearTried);
        Assert.IsFalse(e.FarTried);
    }

    [TestMethod]
    public void Observe_AStillStrandedAgent_KeepsItsTries()
    {
        var state = new LostFootingMissionState();
        var p = Probe();
        p.FaceMiss = true;
        var e = LostFootingRules.Observe(state, p, 0f, _t)!;
        e.NearTried = true;

        LostFootingRules.Observe(state, p, 1f, _t);

        Assert.IsTrue(e.NearTried);
    }

    [TestMethod]
    public void AnOffMeshSoldierAlsoInWater_IsMovedOffMeshFirst_ButStillToDryLand()
    {
        var e = OffMeshSoldier();
        e.WaterStill = true;
        e.WaterSince = 0f;

        var plan = LostFootingRules.PlanRescue(e, _t.RescueNearAfterSeconds, _t);

        Assert.AreEqual(RescueMove.Near, plan.Move);
        Assert.AreEqual(RescueCause.OffMesh, plan.Cause);
        Assert.IsTrue(plan.NeedLand);
    }
}
