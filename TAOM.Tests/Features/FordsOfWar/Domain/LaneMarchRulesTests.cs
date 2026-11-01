using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.FordsOfWar.Domain;

namespace TAOM.Tests.Features.FordsOfWar.Domain;

/// <summary>Every branch of the lane decision order (build-spec 3.4), in the order the rules check them.</summary>
[TestClass]
public class LaneMarchRulesTests
{
    private static readonly FordsMatchConfig Config = FordsMatchConfig.Default;

    private static (LaneMarchRules rules, TowerChain chain) Setup()
    {
        var layout = FordsTestLayout.Create();
        var chain = new TowerChain(layout, Config.Towers);
        return (new LaneMarchRules(Config.Lanes, Config.Towers, layout, chain), chain);
    }

    private static LaneSituation Situation(
        MatchSide side = MatchSide.West,
        int lane = 0,
        LaneOrder order = LaneOrder.Push,
        int live = 10,
        Vec3f? center = null,
        float nearestEnemy = float.MaxValue,
        string? towerId = null,
        float towerDistance = float.MaxValue,
        bool towerLocked = false,
        float? leadX = null,
        float centerTowerDistance = float.MaxValue) =>
        new(side, lane, order, live, center ?? new Vec3f(100f, 0f, 0f), nearestEnemy, towerId, towerDistance, towerLocked,
            leadX == null ? null : new Vec3f(leadX.Value, lane * 50f, 0f), leadX ?? 0f, leadX == null ? 0 : 7, centerTowerDistance);

    // 1. No units

    [TestMethod]
    public void Decide_NoUnits_IsNone()
    {
        var (rules, _) = Setup();

        Assert.AreEqual(LaneCommand.None, rules.Decide(Situation(live: 0)));
    }

    [TestMethod]
    public void Decide_NoUnitsEvenOnFallBackOrWithEnemiesNear_IsNone()
    {
        var (rules, _) = Setup();

        Assert.AreEqual(LaneCommand.None, rules.Decide(Situation(live: 0, order: LaneOrder.FallBack)));
        Assert.AreEqual(LaneCommand.None, rules.Decide(Situation(live: 0, nearestEnemy: 1f)));
    }

    [TestMethod]
    public void Decide_UnitsButNoCenter_IsNone()
    {
        var (rules, _) = Setup();
        var s = Situation() with { Center = null };

        Assert.AreEqual(LaneCommand.None, rules.Decide(s));
    }

    // 2. Fall back

    [TestMethod]
    public void Decide_FallBackWest_MovesToTheWaypointNearestTheOutermostOwnTower()
    {
        var (rules, _) = Setup();

        var command = rules.Decide(Situation(order: LaneOrder.FallBack, center: new Vec3f(500f, 0f, 0f)));

        Assert.AreEqual(LaneCommand.MoveTo(new Vec3f(400f, 0f, 0f)), command);
    }

    [TestMethod]
    public void Decide_FallBackEast_MovesToTheWaypointNearestTheOutermostOwnTower()
    {
        var (rules, _) = Setup();

        var command = rules.Decide(Situation(side: MatchSide.East, order: LaneOrder.FallBack, center: new Vec3f(500f, 0f, 0f)));

        Assert.AreEqual(LaneCommand.MoveTo(new Vec3f(600f, 0f, 0f)), command);
    }

    [TestMethod]
    public void Decide_FallBackAfterTheOuterTowerFell_UsesTheInnerOne()
    {
        var (rules, chain) = Setup();
        chain.OnDestroyed(FordsTestLayout.WestOuter0);

        var command = rules.Decide(Situation(order: LaneOrder.FallBack, center: new Vec3f(500f, 0f, 0f)));

        Assert.AreEqual(LaneCommand.MoveTo(new Vec3f(200f, 0f, 0f)), command);
    }

    [TestMethod]
    public void Decide_FallBackWithEveryLaneTowerGone_RetreatsToTheKeepWaypoint()
    {
        var (rules, chain) = Setup();
        chain.OnDestroyed(FordsTestLayout.WestOuter0);
        chain.OnDestroyed(FordsTestLayout.WestInner0);

        var command = rules.Decide(Situation(order: LaneOrder.FallBack, center: new Vec3f(500f, 0f, 0f)));

        Assert.AreEqual(LaneCommand.MoveTo(new Vec3f(0f, 0f, 0f)), command);
    }

    [TestMethod]
    public void Decide_FallBackWithTowersAndKeepGone_RetreatsToTheOwnBaseEnd()
    {
        var (rules, chain) = Setup();
        chain.OnDestroyed(FordsTestLayout.EastOuter0);
        chain.OnDestroyed(FordsTestLayout.EastInner0);
        chain.OnDestroyed(FordsTestLayout.EastKeep);

        var command = rules.Decide(Situation(side: MatchSide.East, order: LaneOrder.FallBack, center: new Vec3f(500f, 0f, 0f)));

        Assert.AreEqual(LaneCommand.MoveTo(new Vec3f(1000f, 0f, 0f)), command);
    }

    [TestMethod]
    public void Decide_FallBackOrderWithAnEnemyInEngageRange_FallsBack()
    {
        var (rules, _) = Setup();

        var command = rules.Decide(Situation(order: LaneOrder.FallBack, center: new Vec3f(500f, 0f, 0f), nearestEnemy: 3f));

        Assert.AreEqual(LaneCommandKind.MoveTo, command.Kind);
    }

    [TestMethod]
    public void Decide_FallBackOnAnUnknownLane_IsNone()
    {
        var (rules, _) = Setup();

        Assert.AreEqual(LaneCommand.None, rules.Decide(Situation(lane: 9, order: LaneOrder.FallBack)));
    }

    // 3. Engage

    [TestMethod]
    public void Decide_EnemyWithinEngageRange_Charges()
    {
        var (rules, _) = Setup();

        Assert.AreEqual(LaneCommand.Charge, rules.Decide(Situation(nearestEnemy: 10f)));
    }

    [TestMethod]
    public void Decide_EnemyExactlyAtEngageRange_Charges()
    {
        var (rules, _) = Setup();

        Assert.AreEqual(LaneCommand.Charge, rules.Decide(Situation(nearestEnemy: Config.Lanes.EngageRange)));
    }

    [TestMethod]
    public void Decide_EnemyJustBeyondEngageRange_DoesNotCharge()
    {
        var (rules, _) = Setup();

        var command = rules.Decide(Situation(nearestEnemy: Config.Lanes.EngageRange + 0.5f));

        Assert.AreNotEqual(LaneCommand.Charge, command);
    }

    [TestMethod]
    public void Decide_EnemyNearOnHold_Charges()
    {
        var (rules, _) = Setup();

        Assert.AreEqual(LaneCommand.Charge, rules.Decide(Situation(order: LaneOrder.Hold, nearestEnemy: 5f)));
    }

    [TestMethod]
    public void Decide_EnemyNearAndTowerInRange_ChargesFirst()
    {
        var (rules, _) = Setup();

        var command = rules.Decide(Situation(nearestEnemy: 5f, towerId: FordsTestLayout.EastOuter0, towerDistance: 3f));

        Assert.AreEqual(LaneCommand.Charge, command);
    }

    // 4. Attack the tower

    [TestMethod]
    public void Decide_TowerInRangeAndOpen_AttacksIt()
    {
        var (rules, _) = Setup();

        var command = rules.Decide(Situation(towerId: FordsTestLayout.EastOuter0, towerDistance: 4f));

        Assert.AreEqual(LaneCommand.AttackTower(FordsTestLayout.EastOuter0), command);
    }

    [TestMethod]
    public void Decide_TowerExactlyAtAttackRange_AttacksIt()
    {
        var (rules, _) = Setup();

        var command = rules.Decide(Situation(towerId: FordsTestLayout.EastOuter0, towerDistance: Config.Towers.AttackRange));

        Assert.AreEqual(LaneCommandKind.AttackTower, command.Kind);
    }

    [TestMethod]
    public void Decide_TowerBeyondAttackRange_KeepsMarching()
    {
        var (rules, _) = Setup();

        var command = rules.Decide(Situation(towerId: FordsTestLayout.EastOuter0, towerDistance: Config.Towers.AttackRange + 1f));

        Assert.AreEqual(LaneCommandKind.MoveTo, command.Kind);
    }

    [TestMethod]
    public void Decide_TowerInRangeButLocked_KeepsMarching()
    {
        var (rules, _) = Setup();

        var command = rules.Decide(Situation(towerId: FordsTestLayout.EastInner0, towerDistance: 3f, towerLocked: true));

        Assert.AreEqual(LaneCommandKind.MoveTo, command.Kind);
    }

    [TestMethod]
    public void Decide_NoTowerLeft_KeepsMarching()
    {
        var (rules, _) = Setup();

        var command = rules.Decide(Situation(towerId: null, towerDistance: 0f));

        Assert.AreEqual(LaneCommandKind.MoveTo, command.Kind);
    }

    [TestMethod]
    public void Decide_HoldWithTowerInRange_DoesNotAttack()
    {
        var (rules, _) = Setup();

        var command = rules.Decide(Situation(order: LaneOrder.Hold, towerId: FordsTestLayout.EastOuter0, towerDistance: 3f));

        Assert.AreEqual(LaneCommandKind.HoldAt, command.Kind);
    }

    [TestMethod]
    public void Decide_WestLeadPastTheTowerBeyondAttackRange_AttacksIt()
    {
        var (rules, _) = Setup();

        var command = rules.Decide(Situation(towerId: FordsTestLayout.EastOuter0, towerDistance: 60f, leadX: 700f));

        Assert.AreEqual(LaneCommand.AttackTower(FordsTestLayout.EastOuter0), command);
    }

    [TestMethod]
    public void Decide_EastLeadPastTheTowerBeyondAttackRange_AttacksIt()
    {
        var (rules, _) = Setup();

        var command = rules.Decide(Situation(side: MatchSide.East, towerId: FordsTestLayout.WestOuter0, towerDistance: 60f, leadX: 300f));

        Assert.AreEqual(LaneCommand.AttackTower(FordsTestLayout.WestOuter0), command);
    }

    [TestMethod]
    public void Decide_LeadExactlyLevelWithTheTower_AttacksIt()
    {
        var (rules, _) = Setup();

        var west = rules.Decide(Situation(towerId: FordsTestLayout.EastOuter0, towerDistance: 40f, leadX: 640f));
        var east = rules.Decide(Situation(side: MatchSide.East, towerId: FordsTestLayout.WestOuter0, towerDistance: 40f, leadX: 360f));

        Assert.AreEqual(LaneCommandKind.AttackTower, west.Kind);
        Assert.AreEqual(LaneCommandKind.AttackTower, east.Kind);
    }

    [TestMethod]
    public void Decide_LeadShortOfTheTowerBeyondAttackRange_KeepsMarching()
    {
        var (rules, _) = Setup();

        var west = rules.Decide(Situation(towerId: FordsTestLayout.EastOuter0, towerDistance: 60f, leadX: 580f));
        var east = rules.Decide(Situation(side: MatchSide.East, towerId: FordsTestLayout.WestOuter0, towerDistance: 60f, leadX: 420f));

        Assert.AreEqual(LaneCommandKind.MoveTo, west.Kind);
        Assert.AreEqual(LaneCommandKind.MoveTo, east.Kind);
    }

    [TestMethod]
    public void Decide_LeadPastALockedTower_KeepsMarching()
    {
        var (rules, _) = Setup();

        var command = rules.Decide(Situation(towerId: FordsTestLayout.EastInner0, towerDistance: 60f, towerLocked: true, leadX: 860f));

        Assert.AreEqual(LaneCommandKind.MoveTo, command.Kind);
    }

    [TestMethod]
    public void Decide_LeadPastTheTowerOnHold_Holds()
    {
        var (rules, _) = Setup();

        var command = rules.Decide(Situation(order: LaneOrder.Hold, towerId: FordsTestLayout.EastOuter0, towerDistance: 60f, leadX: 700f));

        Assert.AreEqual(LaneCommandKind.HoldAt, command.Kind);
    }

    [TestMethod]
    public void Decide_LeadPastTheTowerOnAnUnknownLane_DoesNotAttack()
    {
        var (rules, _) = Setup();

        var command = rules.Decide(Situation(lane: 9, towerId: FordsTestLayout.EastOuter0, towerDistance: 60f, leadX: 700f));

        Assert.AreEqual(LaneCommand.None, command);
    }

    // 5. Hold: one stable target, the hold point before the next enemy tower

    [TestMethod]
    public void Decide_HoldWestWhoseHoldPointIsPastTheMidpoint_HoldsAtTheMidpoint()
    {
        var (rules, _) = Setup();

        var command = rules.Decide(Situation(order: LaneOrder.Hold, towerId: FordsTestLayout.EastOuter0, towerDistance: 540f));

        Assert.AreEqual(LaneCommand.HoldAt(new Vec3f(500f, 0f, 0f)), command);
    }

    [TestMethod]
    public void Decide_HoldEastWhoseHoldPointIsPastTheMidpoint_HoldsAtTheMidpoint()
    {
        var (rules, _) = Setup();

        var command = rules.Decide(Situation(side: MatchSide.East, order: LaneOrder.Hold, center: new Vec3f(900f, 0f, 0f),
            towerId: FordsTestLayout.WestOuter0, towerDistance: 540f));

        Assert.AreEqual(LaneCommand.HoldAt(new Vec3f(500f, 0f, 0f)), command);
    }

    [TestMethod]
    public void Decide_HoldAlreadyPastTheHoldPoint_FallsBackToTheHoldPoint()
    {
        var (rules, _) = Setup();

        var command = rules.Decide(Situation(order: LaneOrder.Hold, center: new Vec3f(620f, 0f, 0f),
            towerId: FordsTestLayout.EastOuter0, towerDistance: 20f));

        Assert.AreEqual(LaneCommand.HoldAt(new Vec3f(500f, 0f, 0f)), command);
    }

    [TestMethod]
    public void Decide_HoldEastAlreadyPastTheHoldPoint_FallsBackToTheHoldPoint()
    {
        var (rules, _) = Setup();

        var command = rules.Decide(Situation(side: MatchSide.East, order: LaneOrder.Hold, center: new Vec3f(380f, 0f, 0f),
            towerId: FordsTestLayout.WestOuter0, towerDistance: 20f));

        Assert.AreEqual(LaneCommand.HoldAt(new Vec3f(500f, 0f, 0f)), command);
    }

    [TestMethod]
    public void Decide_HoldWhereverTheCenterDrifts_GivesTheSameCommand()
    {
        var (rules, _) = Setup();

        var first = rules.Decide(Situation(order: LaneOrder.Hold, center: new Vec3f(560f, 3f, 0f),
            towerId: FordsTestLayout.EastOuter0, towerDistance: 80f));
        var drifted = rules.Decide(Situation(order: LaneOrder.Hold, center: new Vec3f(571f, -4f, 0f),
            towerId: FordsTestLayout.EastOuter0, towerDistance: 69f));
        var past = rules.Decide(Situation(order: LaneOrder.Hold, center: new Vec3f(612f, 9f, 0f),
            towerId: FordsTestLayout.EastOuter0, towerDistance: 28f));

        Assert.AreEqual(first, drifted);
        Assert.AreEqual(first, past);
    }

    [TestMethod]
    public void Decide_HoldWithNoTowerLeft_IsNone()
    {
        var (rules, _) = Setup();

        var command = rules.Decide(Situation(order: LaneOrder.Hold, center: new Vec3f(300f, 0f, 0f), towerId: null));

        Assert.AreEqual(LaneCommand.None, command);
    }

    [TestMethod]
    public void Decide_HoldWithAnUnknownTowerId_IsNone()
    {
        var (rules, _) = Setup();

        var command = rules.Decide(Situation(order: LaneOrder.Hold, center: new Vec3f(300f, 0f, 0f), towerId: "barn", towerDistance: 300f));

        Assert.AreEqual(LaneCommand.None, command);
    }

    [TestMethod]
    public void Decide_HoldOnAnUnknownLane_IsNone()
    {
        var (rules, _) = Setup();

        var command = rules.Decide(Situation(lane: 9, order: LaneOrder.Hold, center: new Vec3f(300f, 0f, 0f),
            towerId: FordsTestLayout.EastOuter0, towerDistance: 300f));

        Assert.AreEqual(LaneCommand.None, command);
    }

    // 6. Otherwise march

    [TestMethod]
    public void Decide_PushWest_MovesToTheNextWaypointTowardTheEnemy()
    {
        var (rules, _) = Setup();

        var command = rules.Decide(Situation(center: new Vec3f(100f, 0f, 0f), towerId: FordsTestLayout.EastOuter0, towerDistance: 540f));

        Assert.AreEqual(LaneCommand.MoveTo(new Vec3f(200f, 0f, 0f)), command);
    }

    [TestMethod]
    public void Decide_PushEast_MovesToTheNextWaypointTowardTheEnemy()
    {
        var (rules, _) = Setup();

        var command = rules.Decide(Situation(side: MatchSide.East, center: new Vec3f(900f, 0f, 0f)));

        Assert.AreEqual(LaneCommand.MoveTo(new Vec3f(800f, 0f, 0f)), command);
    }

    [TestMethod]
    public void Decide_PushNearAWaypoint_SkipsToTheOneAfter()
    {
        var (rules, _) = Setup();

        var command = rules.Decide(Situation(center: new Vec3f(197f, 0f, 0f)));

        Assert.AreEqual(LaneCommand.MoveTo(new Vec3f(300f, 0f, 0f)), command);
    }

    [TestMethod]
    public void Decide_PushOnTheSecondLane_FollowsThatLanesPath()
    {
        var (rules, _) = Setup();

        var command = rules.Decide(Situation(lane: 1, center: new Vec3f(100f, 50f, 0f)));

        Assert.AreEqual(LaneCommand.MoveTo(new Vec3f(200f, 50f, 0f)), command);
    }

    [TestMethod]
    public void Decide_PushOnAnUnknownLane_IsNone()
    {
        var (rules, _) = Setup();

        Assert.AreEqual(LaneCommand.None, rules.Decide(Situation(lane: 9)));
    }

    // 7. The march follows the lead group, forward only

    [TestMethod]
    public void Decide_PushWithTheLeadAheadOfTheCenter_MarchesFromTheLead()
    {
        var (rules, _) = Setup();

        var command = rules.Decide(Situation(center: new Vec3f(100f, 0f, 0f), leadX: 450f));

        Assert.AreEqual(LaneCommand.MoveTo(new Vec3f(500f, 0f, 0f)), command);
    }

    [TestMethod]
    public void Decide_PushLeadWithinReachInArcOfAWaypoint_SkipsToTheOneAfter()
    {
        var (rules, _) = Setup();

        var command = rules.Decide(Situation(leadX: 295f));

        Assert.AreEqual(LaneCommand.MoveTo(new Vec3f(400f, 0f, 0f)), command);
    }

    [TestMethod]
    public void Decide_PushAfterTheLeadFallsBack_DoesNotReissueAnEarlierWaypoint()
    {
        var (rules, _) = Setup();
        var first = rules.Decide(Situation(leadX: 450f));

        var second = rules.Decide(Situation(leadX: 150f));

        Assert.AreEqual(LaneCommand.MoveTo(new Vec3f(500f, 0f, 0f)), first);
        Assert.AreEqual(first, second);
    }

    [TestMethod]
    public void Decide_PushLeadAdvancesAfterAFallBack_MovesOnToTheNextWaypoint()
    {
        var (rules, _) = Setup();
        rules.Decide(Situation(leadX: 450f));
        rules.Decide(Situation(leadX: 150f));

        var command = rules.Decide(Situation(leadX: 520f));

        Assert.AreEqual(LaneCommand.MoveTo(new Vec3f(600f, 0f, 0f)), command);
    }

    [TestMethod]
    public void Decide_EastPushAfterTheLeadFallsBack_DoesNotReissueAnEarlierWaypoint()
    {
        var (rules, _) = Setup();
        var first = rules.Decide(Situation(side: MatchSide.East, leadX: 550f));

        var second = rules.Decide(Situation(side: MatchSide.East, leadX: 900f));

        Assert.AreEqual(LaneCommand.MoveTo(new Vec3f(500f, 0f, 0f)), first);
        Assert.AreEqual(first, second);
    }

    [TestMethod]
    public void Decide_TwoLanesAndBothSides_KeepSeparateRatchets()
    {
        var (rules, _) = Setup();
        rules.Decide(Situation(lane: 0, leadX: 450f));
        rules.Decide(Situation(side: MatchSide.East, lane: 0, leadX: 550f));

        var lane1 = rules.Decide(Situation(lane: 1, leadX: 150f));
        var eastLane0 = rules.Decide(Situation(side: MatchSide.East, lane: 0, leadX: 900f));
        var westLane0 = rules.Decide(Situation(lane: 0, leadX: 150f));

        Assert.AreEqual(LaneCommand.MoveTo(new Vec3f(200f, 50f, 0f)), lane1);
        Assert.AreEqual(LaneCommand.MoveTo(new Vec3f(500f, 0f, 0f)), eastLane0);
        Assert.AreEqual(LaneCommand.MoveTo(new Vec3f(500f, 0f, 0f)), westLane0);
    }

    [TestMethod]
    public void Decide_PushAfterAFallBackTick_RatchetStartsOver()
    {
        var (rules, _) = Setup();
        rules.Decide(Situation(leadX: 450f));
        rules.Decide(Situation(order: LaneOrder.FallBack, leadX: 450f));

        var command = rules.Decide(Situation(leadX: 150f));

        Assert.AreEqual(LaneCommand.MoveTo(new Vec3f(200f, 0f, 0f)), command);
    }

    [TestMethod]
    public void Decide_PushAfterAHoldTick_RatchetStartsOver()
    {
        var (rules, _) = Setup();
        rules.Decide(Situation(leadX: 450f));
        rules.Decide(Situation(order: LaneOrder.Hold, towerId: FordsTestLayout.EastOuter0, towerDistance: 190f, leadX: 450f));

        var command = rules.Decide(Situation(leadX: 150f));

        Assert.AreEqual(LaneCommand.MoveTo(new Vec3f(200f, 0f, 0f)), command);
    }

    [TestMethod]
    public void Decide_PushAfterAChargeTick_RatchetStartsOver()
    {
        var (rules, _) = Setup();
        rules.Decide(Situation(leadX: 450f));
        rules.Decide(Situation(nearestEnemy: 5f, leadX: 450f));

        var command = rules.Decide(Situation(leadX: 150f));

        Assert.AreEqual(LaneCommand.MoveTo(new Vec3f(200f, 0f, 0f)), command);
    }

    [TestMethod]
    public void Decide_PushAfterAnAttackTick_RatchetStartsOver()
    {
        var (rules, _) = Setup();
        rules.Decide(Situation(leadX: 450f));
        rules.Decide(Situation(towerId: FordsTestLayout.EastOuter0, towerDistance: 4f, leadX: 450f));

        var command = rules.Decide(Situation(leadX: 150f));

        Assert.AreEqual(LaneCommand.MoveTo(new Vec3f(200f, 0f, 0f)), command);
    }

    [TestMethod]
    public void Decide_PushAfterTheLaneWasEmpty_RatchetStartsOver()
    {
        var (rules, _) = Setup();
        rules.Decide(Situation(leadX: 450f));
        rules.Decide(Situation(live: 0));

        var command = rules.Decide(Situation(leadX: 150f));

        Assert.AreEqual(LaneCommand.MoveTo(new Vec3f(200f, 0f, 0f)), command);
    }

    // 8. Hold is bounded by the lane midpoint and lands on a path vertex

    [TestMethod]
    public void Decide_HoldWestBeforeTheMidpoint_HoldsOnTheVertexBehindTheHoldPoint()
    {
        var layout = FordsTestLayout.Create();
        var chain = new TowerChain(layout, Config.Towers);
        var rules = new LaneMarchRules(Config.Lanes with { HoldDistanceBeforeTower = 200f }, Config.Towers, layout, chain);

        var command = rules.Decide(Situation(order: LaneOrder.Hold, towerId: FordsTestLayout.EastOuter0, towerDistance: 540f));

        Assert.AreEqual(LaneCommand.HoldAt(new Vec3f(400f, 0f, 0f)), command);
    }

    [TestMethod]
    public void Decide_HoldWhenTheNextTargetIsAKeep_HoldsAtTheMidpoint()
    {
        var (rules, chain) = Setup();
        chain.OnDestroyed(FordsTestLayout.EastOuter0);
        chain.OnDestroyed(FordsTestLayout.EastInner0);

        var command = rules.Decide(Situation(order: LaneOrder.Hold, towerId: FordsTestLayout.EastKeep, towerDistance: 900f));

        Assert.AreEqual(LaneCommand.HoldAt(new Vec3f(500f, 0f, 0f)), command);
    }

    [TestMethod]
    public void Decide_EastHoldInALaneWithNoWestTowers_HoldsNoFurtherThanTheMidpointAndOnAPathVertex()
    {
        var uneven = new List<Vec3f>
        {
            new(0f, 50f, 0f), new(130f, 50f, 0f), new(260f, 50f, 0f), new(410f, 50f, 0f), new(600f, 50f, 0f), new(800f, 50f, 0f),
            new(1000f, 50f, 0f),
        };
        var plain = FordsTestLayout.Create();
        var layout = plain with
        {
            Lanes = new List<LaneLayout> { plain.Lanes[0], new(1, uneven) },
            Towers = plain.Towers.Where(t => !(t.Owner == MatchSide.West && t.Kind == TowerKind.Tower && t.Lane == 1)).ToList(),
        };
        var rules = new LaneMarchRules(Config.Lanes, Config.Towers, layout, new TowerChain(layout, Config.Towers));

        var command = rules.Decide(Situation(side: MatchSide.East, lane: 1, order: LaneOrder.Hold,
            towerId: FordsTestLayout.WestKeep, towerDistance: 900f));

        Assert.AreEqual(LaneCommandKind.HoldAt, command.Kind);
        Assert.IsTrue(LaneGeometry.ArcPosition(uneven, command.Target) >= LaneGeometry.PathLength(uneven) / 2f,
            "an East hold must stay on East's half of the lane");
        CollectionAssert.Contains(uneven.ToList(), command.Target);
    }

    // 9. The decision trace

    [TestMethod]
    public void Evaluate_NoUnits_IsBranchNoneWithNoCommand()
    {
        var (rules, _) = Setup();

        var decision = rules.Evaluate(Situation(live: 0));

        Assert.AreEqual(LaneBranch.None, decision.Branch);
        Assert.AreEqual(LaneCommand.None, decision.Command);
        Assert.AreEqual(-1, decision.WaypointIndex);
    }

    [TestMethod]
    public void Evaluate_EachBranch_NamesItself()
    {
        var (rules, _) = Setup();

        Assert.AreEqual(LaneBranch.FallBack, rules.Evaluate(Situation(order: LaneOrder.FallBack)).Branch);
        Assert.AreEqual(LaneBranch.Charge, rules.Evaluate(Situation(nearestEnemy: 5f)).Branch);
        Assert.AreEqual(LaneBranch.AttackTower,
            rules.Evaluate(Situation(towerId: FordsTestLayout.EastOuter0, towerDistance: 4f)).Branch);
        Assert.AreEqual(LaneBranch.Hold,
            rules.Evaluate(Situation(order: LaneOrder.Hold, towerId: FordsTestLayout.EastOuter0, towerDistance: 400f)).Branch);
        Assert.AreEqual(LaneBranch.March, rules.Evaluate(Situation()).Branch);
    }

    [TestMethod]
    public void Evaluate_March_ReportsTheWaypointIndexOfTheTarget()
    {
        var (rules, _) = Setup();

        var decision = rules.Evaluate(Situation(leadX: 250f));

        Assert.AreEqual(3, decision.WaypointIndex);
        Assert.AreEqual(LaneCommand.MoveTo(new Vec3f(300f, 0f, 0f)), decision.Command);
    }

    [TestMethod]
    public void Evaluate_MarchAfterTheLeadFellBack_ReportsTheRatchetedIndex()
    {
        var (rules, _) = Setup();
        rules.Evaluate(Situation(leadX: 450f));

        var decision = rules.Evaluate(Situation(leadX: 150f));

        Assert.AreEqual(5, decision.WaypointIndex);
    }

    [TestMethod]
    public void Evaluate_NotMarching_HasNoWaypointIndex()
    {
        var (rules, _) = Setup();

        Assert.AreEqual(-1, rules.Evaluate(Situation(nearestEnemy: 5f)).WaypointIndex);
        Assert.AreEqual(-1, rules.Evaluate(Situation(order: LaneOrder.FallBack)).WaypointIndex);
    }

    [TestMethod]
    public void Evaluate_FullSituation_CarriesEveryFieldIntoTheDecision()
    {
        var (rules, _) = Setup();
        var s = Situation(side: MatchSide.East, lane: 1, live: 12, center: new Vec3f(800f, 50f, 0f), nearestEnemy: 77f,
            towerId: FordsTestLayout.WestOuter1, towerDistance: 33f, towerLocked: true, leadX: 700f, centerTowerDistance: 44f);

        var d = rules.Evaluate(s);

        Assert.AreEqual(MatchSide.East, d.Side);
        Assert.AreEqual(1, d.Lane);
        Assert.AreEqual(LaneOrder.Push, d.Order);
        Assert.AreEqual(12, d.LiveUnits);
        Assert.AreEqual(new Vec3f(800f, 50f, 0f), d.Center);
        Assert.AreEqual(new Vec3f(700f, 50f, 0f), d.Lead);
        Assert.AreEqual(700f, d.LeadArc);
        Assert.AreEqual(7, d.LeadCount);
        Assert.AreEqual(77f, d.NearestEnemyDistance);
        Assert.AreEqual(FordsTestLayout.WestOuter1, d.NextEnemyTowerId);
        Assert.AreEqual(33f, d.NextEnemyTowerDistance);
        Assert.AreEqual(44f, d.CenterTowerDistance);
        Assert.IsTrue(d.Locked);
    }

    [TestMethod]
    public void Evaluate_SituationWithoutALeadMeasure_UsesTheCenterAsTheLead()
    {
        var (rules, _) = Setup();

        var d = rules.Evaluate(Situation(center: new Vec3f(250f, 0f, 0f), live: 9));

        Assert.AreEqual(new Vec3f(250f, 0f, 0f), d.Lead);
        Assert.AreEqual(250f, d.LeadArc, 1e-3f);
        Assert.AreEqual(9, d.LeadCount);
    }
}

/// <summary>Production reads <see cref="LaneMarchRules.Evaluate"/>; the branch tests only look at the command it carries.</summary>
internal static class LaneMarchRulesTestExtensions
{
    public static LaneCommand Decide(this LaneMarchRules rules, LaneSituation situation) => rules.Evaluate(situation).Command;
}
