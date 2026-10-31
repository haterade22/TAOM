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
        bool towerLocked = false) =>
        new(side, lane, order, live, center ?? new Vec3f(100f, 0f, 0f), nearestEnemy, towerId, towerDistance, towerLocked);

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
    public void Decide_FallBackBeatsAnEnemyInEngageRange()
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

    // 5. Hold: one stable target, the hold point before the next enemy tower

    [TestMethod]
    public void Decide_HoldWest_HoldsTheConfiguredDistanceBeforeTheNextTower()
    {
        var (rules, _) = Setup();

        var command = rules.Decide(Situation(order: LaneOrder.Hold, towerId: FordsTestLayout.EastOuter0, towerDistance: 540f));

        Assert.AreEqual(LaneCommand.HoldAt(new Vec3f(580f, 0f, 0f)), command);
    }

    [TestMethod]
    public void Decide_HoldEast_HoldsTheConfiguredDistanceBeforeTheNextTower()
    {
        var (rules, _) = Setup();

        var command = rules.Decide(Situation(side: MatchSide.East, order: LaneOrder.Hold, center: new Vec3f(900f, 0f, 0f),
            towerId: FordsTestLayout.WestOuter0, towerDistance: 540f));

        Assert.AreEqual(LaneCommand.HoldAt(new Vec3f(420f, 0f, 0f)), command);
    }

    [TestMethod]
    public void Decide_HoldAlreadyPastTheHoldPoint_FallsBackToTheHoldPoint()
    {
        var (rules, _) = Setup();

        var command = rules.Decide(Situation(order: LaneOrder.Hold, center: new Vec3f(620f, 0f, 0f),
            towerId: FordsTestLayout.EastOuter0, towerDistance: 20f));

        Assert.AreEqual(LaneCommand.HoldAt(new Vec3f(580f, 0f, 0f)), command);
    }

    [TestMethod]
    public void Decide_HoldEastAlreadyPastTheHoldPoint_FallsBackToTheHoldPoint()
    {
        var (rules, _) = Setup();

        var command = rules.Decide(Situation(side: MatchSide.East, order: LaneOrder.Hold, center: new Vec3f(380f, 0f, 0f),
            towerId: FordsTestLayout.WestOuter0, towerDistance: 20f));

        Assert.AreEqual(LaneCommand.HoldAt(new Vec3f(420f, 0f, 0f)), command);
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
}
