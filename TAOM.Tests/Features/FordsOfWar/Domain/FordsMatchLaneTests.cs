using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.FordsOfWar.Domain;
using static TAOM.Tests.Features.FordsOfWar.Domain.FordsMatchFixture;

namespace TAOM.Tests.Features.FordsOfWar.Domain;

/// <summary>
/// The lane commands the match issues from per-unit positions: the lead group stands for the lane, the march only goes forward,
/// and every tick leaves a decision trace. Test map: lanes along X, West base x=0, East base x=1000, East outer tower at x=640.
/// </summary>
[TestClass]
public class FordsMatchLaneTests
{
    private static List<Vec3f> Units(float x, int count, float y = 0f) =>
        Enumerable.Range(0, count).Select(_ => new Vec3f(x, y, 0f)).ToList();

    /// <summary>A lane as the builder reports it: units at the given positions, centre their mean.</summary>
    private static LaneSnapshot LaneOf(MatchSide side, int lane, List<Vec3f> positions, float nearestEnemy = float.MaxValue)
    {
        var center = new Vec3f(positions.Average(p => p.X), positions.Average(p => p.Y), positions.Average(p => p.Z));
        return new LaneSnapshot(side, lane, positions.Count, center, nearestEnemy, positions);
    }

    private static List<Vec3f> Join(params List<Vec3f>[] groups) => groups.SelectMany(g => g).ToList();

    private static LaneCommand WestLane0(MatchEffects effects) =>
        effects.Commands.Single(c => c.Side == MatchSide.West && c.Lane == 0).Command;

    private static LaneDecision DecisionOf(FordsMatch match, MatchSide side, int lane) =>
        match.LastDecisions.Single(d => d.Side == side && d.Lane == lane);

    [TestMethod]
    public void Tick_LeadWithinAttackRangeWhileStragglersFarBack_IssuesAttackTower()
    {
        var match = Create();
        var west = LaneOf(MatchSide.West, 0, Join(Units(630f, 40), Units(3f, 10)));

        var effects = match.Tick(Snap(1f, new[] { west }));

        Assert.AreEqual(LaneCommand.AttackTower(FordsTestLayout.EastOuter0), WestLane0(effects));
    }

    [TestMethod]
    public void Tick_WholeLaneInAttackRange_StillIssuesAttackTower()
    {
        var match = Create();
        var west = LaneOf(MatchSide.West, 0, Units(630f, 10));

        var effects = match.Tick(Snap(1f, new[] { west }));

        Assert.AreEqual(LaneCommand.AttackTower(FordsTestLayout.EastOuter0), WestLane0(effects));
    }

    [TestMethod]
    public void Tick_NewWaveAtTheBase_MarchTargetNeverStepsBackward()
    {
        var match = Create();
        var before = match.Tick(Snap(1f, new[] { LaneOf(MatchSide.West, 0, Units(450f, 40)) }));

        var after = match.Tick(Snap(1f, new[] { LaneOf(MatchSide.West, 0, Join(Units(455f, 40), Units(3f, 10))) }));

        Assert.AreEqual(LaneCommand.MoveTo(new Vec3f(500f, 0f, 0f)), WestLane0(before));
        Assert.AreEqual(LaneCommand.MoveTo(new Vec3f(500f, 0f, 0f)), WestLane0(after));
    }

    [TestMethod]
    public void Tick_LeadFallsBackWhilePushing_KeepsTheForwardWaypoint()
    {
        var match = Create();
        match.Tick(Snap(1f, new[] { LaneOf(MatchSide.West, 0, Units(450f, 40)) }));

        var effects = match.Tick(Snap(1f, new[] { LaneOf(MatchSide.West, 0, Units(150f, 40)) }));

        Assert.AreEqual(LaneCommand.MoveTo(new Vec3f(500f, 0f, 0f)), WestLane0(effects));
    }

    [TestMethod]
    public void Tick_LaneWipedOutThenRespawned_StartsTheMarchOverFromTheLead()
    {
        var match = Create();
        match.Tick(Snap(1f, new[] { LaneOf(MatchSide.West, 0, Units(450f, 40)) }));
        match.Tick(Snap(1f));

        var effects = match.Tick(Snap(1f, new[] { LaneOf(MatchSide.West, 0, Units(150f, 10)) }));

        Assert.AreEqual(LaneCommand.MoveTo(new Vec3f(200f, 0f, 0f)), WestLane0(effects));
    }

    [TestMethod]
    public void Tick_EnemyNearTheLeadButFarFromTheCenter_Charges()
    {
        var match = Create();
        var west = LaneOf(MatchSide.West, 0, Join(Units(500f, 40), Units(3f, 10)), nearestEnemy: 130f);
        var east = LaneOf(MatchSide.East, 0, Units(520f, 5), nearestEnemy: 20f);

        var effects = match.Tick(Snap(1f, new[] { west, east }));

        Assert.AreEqual(LaneCommand.Charge, WestLane0(effects));
    }

    [TestMethod]
    public void Tick_EnemyFarFromTheLeadEvenIfNearTheStragglers_DoesNotCharge()
    {
        var match = Create();
        var west = LaneOf(MatchSide.West, 0, Join(Units(500f, 40), Units(3f, 10)), nearestEnemy: 20f);
        var east = LaneOf(MatchSide.East, 0, Units(30f, 5), nearestEnemy: 20f);

        var effects = match.Tick(Snap(1f, new[] { west, east }));

        Assert.AreNotEqual(LaneCommand.Charge, WestLane0(effects));
    }

    [TestMethod]
    public void Tick_NoUnitPositionsInTheSnapshot_TheCenterStandsInForTheLead()
    {
        var match = Create();

        var effects = match.Tick(Snap(1f, new[] { Live(MatchSide.West, 0, 10, new Vec3f(637f, 0f, 0f)) }));

        Assert.AreEqual(LaneCommand.AttackTower(FordsTestLayout.EastOuter0), WestLane0(effects));
        var decision = DecisionOf(match, MatchSide.West, 0);
        Assert.AreEqual(new Vec3f(637f, 0f, 0f), decision.Lead);
        Assert.AreEqual(10, decision.LeadCount);
    }

    [TestMethod]
    public void LastDecisions_BeforeAnyTick_IsEmpty()
    {
        Assert.AreEqual(0, Create().LastDecisions.Count);
    }

    [TestMethod]
    public void Tick_LaneWithUnits_RecordsTheLeadTheTowerDistancesAndTheBranch()
    {
        var match = Create();
        var west = LaneOf(MatchSide.West, 0, Join(Units(630f, 40), Units(3f, 10)));

        match.Tick(Snap(1f, new[] { west }));

        var d = DecisionOf(match, MatchSide.West, 0);
        Assert.AreEqual(LaneBranch.AttackTower, d.Branch);
        Assert.AreEqual(LaneOrder.Push, d.Order);
        Assert.AreEqual(50, d.LiveUnits);
        Assert.AreEqual(40, d.LeadCount);
        Assert.AreEqual(630f, d.Lead!.Value.X, 1e-3f);
        Assert.AreEqual(630f, d.LeadArc, 1e-3f);
        Assert.AreEqual(FordsTestLayout.EastOuter0, d.NextEnemyTowerId);
        Assert.AreEqual(10f, d.NextEnemyTowerDistance, 1e-3f);
        Assert.AreEqual(west.Center!.Value.Distance2D(new Vec3f(640f, 0f, 0f)), d.CenterTowerDistance, 1e-3f);
        Assert.IsFalse(d.Locked);
        Assert.AreEqual(LaneCommand.AttackTower(FordsTestLayout.EastOuter0), d.Command);
    }

    [TestMethod]
    public void Tick_MarchingLane_RecordsTheWaypointIndex()
    {
        var match = Create();

        match.Tick(Snap(1f, new[] { LaneOf(MatchSide.West, 0, Units(450f, 40)) }));

        var d = DecisionOf(match, MatchSide.West, 0);
        Assert.AreEqual(LaneBranch.March, d.Branch);
        Assert.AreEqual(5, d.WaypointIndex);
    }

    [TestMethod]
    public void Tick_EveryLaneOfBothSides_GetsADecisionEvenWithoutUnits()
    {
        var match = Create();

        match.Tick(Snap(1f, new[] { LaneOf(MatchSide.West, 0, Units(450f, 40)) }));

        Assert.AreEqual(4, match.LastDecisions.Count);
        var empty = DecisionOf(match, MatchSide.East, 1);
        Assert.AreEqual(LaneBranch.None, empty.Branch);
        Assert.AreEqual(0, empty.LiveUnits);
        Assert.AreEqual(LaneCommand.None, empty.Command);
    }

    [TestMethod]
    public void Tick_NextTick_ReplacesTheDecisions()
    {
        var match = Create();
        match.Tick(Snap(1f, new[] { LaneOf(MatchSide.West, 0, Units(450f, 40)) }));

        match.Tick(Snap(1f));

        Assert.AreEqual(LaneBranch.None, DecisionOf(match, MatchSide.West, 0).Branch);
    }

    [TestMethod]
    public void Tick_Countdown_LeavesNoDecisions()
    {
        var match = Create(Config(c => c with { CountdownSeconds = 10f }));

        match.Tick(Snap(1f, new[] { LaneOf(MatchSide.West, 0, Units(450f, 40)) }));

        Assert.AreEqual(0, match.LastDecisions.Count);
    }

    [TestMethod]
    public void Tick_HoldingLane_RecordsTheHoldBranchAtTheBoundedPoint()
    {
        var match = Create();
        match.TrySetLaneOrder(WestSlot, 0, LaneOrder.Hold);

        match.Tick(Snap(1f, new[] { LaneOf(MatchSide.West, 0, Units(100f, 10)) }));

        var d = DecisionOf(match, MatchSide.West, 0);
        Assert.AreEqual(LaneBranch.Hold, d.Branch);
        Assert.AreEqual(LaneOrder.Hold, d.Order);
        Assert.AreEqual(LaneCommand.HoldAt(new Vec3f(500f, 0f, 0f)), d.Command);
    }

    [TestMethod]
    public void Tick_LeadBandFromTheConfig_DecidesHowManyUnitsLead()
    {
        var units = Join(Units(630f, 10), Units(600f, 10));
        var wide = Create(Config(c => c with { Lanes = c.Lanes with { LeadBand = 40f } }));
        var narrow = Create(Config(c => c with { Lanes = c.Lanes with { LeadBand = 10f } }));

        wide.Tick(Snap(1f, new[] { LaneOf(MatchSide.West, 0, units) }));
        narrow.Tick(Snap(1f, new[] { LaneOf(MatchSide.West, 0, units) }));

        Assert.AreEqual(20, DecisionOf(wide, MatchSide.West, 0).LeadCount);
        Assert.AreEqual(10, DecisionOf(narrow, MatchSide.West, 0).LeadCount);
    }

    [TestMethod]
    public void Tick_WestLeadPastAStandingTowerBeyondAttackRange_IssuesAttackTower()
    {
        var match = Create();

        var effects = match.Tick(Snap(1f, new[] { LaneOf(MatchSide.West, 0, Units(700f, 10)) }));

        Assert.AreEqual(LaneCommand.AttackTower(FordsTestLayout.EastOuter0), WestLane0(effects));
    }

    [TestMethod]
    public void Tick_EastLeadPastAStandingTowerBeyondAttackRange_IssuesAttackTower()
    {
        var match = Create();

        var effects = match.Tick(Snap(1f, new[] { LaneOf(MatchSide.East, 0, Units(300f, 10)) }));

        var command = effects.Commands.Single(c => c.Side == MatchSide.East && c.Lane == 0).Command;
        Assert.AreEqual(LaneCommand.AttackTower(FordsTestLayout.WestOuter0), command);
    }

    [TestMethod]
    public void Tick_OneRunawayUnitPastTheTowerWhile43StandAtIt_TheMainBodyIsTheLead()
    {
        var match = Create();
        var west = LaneOf(MatchSide.West, 0, Join(Units(630f, 43), Units(690f, 1)));

        var effects = match.Tick(Snap(1f, new[] { west }));

        var d = DecisionOf(match, MatchSide.West, 0);
        Assert.AreEqual(43, d.LeadCount);
        Assert.AreEqual(630f, d.Lead!.Value.X, 1e-3f);
        Assert.AreEqual(LaneCommand.AttackTower(FordsTestLayout.EastOuter0), WestLane0(effects));
    }

    [TestMethod]
    public void Tick_RunawayPackOfTwoPastTheMainBody_StillDoesNotLeadTheLane()
    {
        var match = Create();
        var west = LaneOf(MatchSide.West, 0, Join(Units(300f, 40), Units(520f, 2)));

        match.Tick(Snap(1f, new[] { west }));

        var d = DecisionOf(match, MatchSide.West, 0);
        Assert.AreEqual(40, d.LeadCount);
        Assert.AreEqual(300f, d.Lead!.Value.X, 1e-3f);
    }

    [TestMethod]
    public void Tick_ChargeCarriedPastTheTowerThenTheEnemyIsClear_IssuesAttackTower()
    {
        var match = Create();
        var charging = match.Tick(Snap(1f, new[] { LaneOf(MatchSide.West, 0, Units(700f, 10), nearestEnemy: 5f) }));

        var clear = match.Tick(Snap(1f, new[] { LaneOf(MatchSide.West, 0, Units(700f, 10)) }));

        Assert.AreEqual(LaneCommand.Charge, WestLane0(charging));
        Assert.AreEqual(LaneCommand.AttackTower(FordsTestLayout.EastOuter0), WestLane0(clear));
    }
}
