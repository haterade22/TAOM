using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.FordsOfWar.Domain;

namespace TAOM.Tests.Features.FordsOfWar.Domain;

/// <summary>
/// A faction's lane tactic (Mike, 2026-11-07: Isengard and the dwarves "advance as a formation, get close and then attack"): march,
/// close on the enemy in the close arrangement inside the close range, charge inside the charge range. The test lane runs along
/// y = 0 with a vertex every 100 m.
/// </summary>
[TestClass]
public class LaneTacticRulesTests
{
    private static readonly FordsMatchConfig Config = FordsMatchConfig.Default;

    private static readonly LaneTactic Wall = new(LaneArrangement.Line, LaneArrangement.ShieldWall, CloseRange: 45f, ChargeRange: 6f);

    private static LaneMarchRules Rules(LaneTactic? west = null, LaneTactic? east = null)
    {
        var layout = FordsTestLayout.Create();
        var tactics = new Dictionary<MatchSide, LaneTactic>();
        if (west != null)
            tactics[MatchSide.West] = west;
        if (east != null)
            tactics[MatchSide.East] = east;

        return new LaneMarchRules(Config.Lanes, Config.Towers, layout, new TowerChain(layout, Config.Towers), tactics);
    }

    /// <summary>A West lane led from x = 330 with its nearest enemy straight ahead at the given distance.</summary>
    private static LaneSituation Facing(float enemyDistance, LaneOrder order = LaneOrder.Push, MatchSide side = MatchSide.West,
        string? towerId = null, float towerDistance = float.MaxValue, int attackers = -1, int defenders = -1, bool enemyPosition = true)
    {
        var lead = new Vec3f(330f, 0f, 0f);
        return new LaneSituation(side, 0, order, 10, lead, enemyDistance, towerId, towerDistance, false, lead, 330f, 7, towerDistance,
            attackers, defenders, NearestEnemy: enemyPosition ? new Vec3f(330f + enemyDistance, 0f, 0f) : null);
    }

    private static readonly LaneTactic Open = new(LaneArrangement.Loose, LaneArrangement.Loose, CloseRange: 50f, ChargeRange: 10f);

    /// <summary>A West lane led from x = 330 with its nearest enemy at the given point.</summary>
    private static LaneSituation FacingAt(Vec3f enemy) =>
        Facing(new Vec3f(330f, 0f, 0f).Distance2D(enemy)) with { NearestEnemy = enemy };

    [TestMethod]
    public void AnEnemyInsideTheCloseRange_TheLaneClosesOnItInTheCloseArrangement_AtItsPosition()
    {
        var decision = Rules(Wall).Evaluate(Facing(41f));

        Assert.AreEqual(LaneBranch.Close, decision.Branch);
        Assert.AreEqual(LaneCommand.MoveTo(new Vec3f(371f, 0f, 0f)), decision.Command, "the enemy stands on ground it walked on");
        Assert.AreEqual(LaneArrangement.ShieldWall, decision.Arrangement);
    }

    /// <summary>
    /// Codex and Lens 5, 2026-11-07: a target that followed the enemy's height (or snapped to a grid) changed on every frame on a
    /// slope, so the director sent a new Move each frame. The target stays while the enemy keeps within <see cref="LaneMarchRules.CloseRetargetDistance"/> of it.
    /// </summary>
    [TestMethod]
    public void AnEnemyThatMovesALittle_KeepsTheSameTarget_EvenOnASlope()
    {
        var rules = Rules(Wall);
        var first = rules.Evaluate(FacingAt(new Vec3f(371f, 0f, 0f))).Command;

        Assert.AreEqual(first, rules.Evaluate(FacingAt(new Vec3f(373f, 1f, 2.5f))).Command, "within 5 m, and only the height changed a lot");
        Assert.AreEqual(LaneCommand.MoveTo(new Vec3f(371f, 6f, 0f)), rules.Evaluate(FacingAt(new Vec3f(371f, 6f, 0f))).Command, "6 m away: a new target");
    }

    /// <summary>Codex, 2026-11-07: with a 1 m charge range a lead standing on a target 3.5 m from the enemy never charged.</summary>
    [TestMethod]
    public void ASmallChargeRange_MovesTheTargetBeforeTheEnemyIsOutOfChargeReachOfIt()
    {
        var rules = Rules(new LaneTactic(LaneArrangement.Line, LaneArrangement.ShieldWall, CloseRange: 20f, ChargeRange: 1f));
        rules.Evaluate(FacingAt(new Vec3f(345f, 0f, 0f)));

        Assert.AreEqual(LaneCommand.MoveTo(new Vec3f(346.5f, 0f, 0f)), rules.Evaluate(FacingAt(new Vec3f(346.5f, 0f, 0f))).Command);
    }

    [TestMethod]
    public void AClosingLaneThatCharges_ClosesOnTheNextEnemyAfresh()
    {
        var rules = Rules(Wall);
        rules.Evaluate(FacingAt(new Vec3f(371f, 0f, 0f)));
        rules.Evaluate(Facing(5f));
        rules.Evaluate(Facing(50f));

        Assert.AreEqual(LaneCommand.MoveTo(new Vec3f(373f, 0f, 0f)), rules.Evaluate(FacingAt(new Vec3f(373f, 0f, 0f))).Command);
    }

    /// <summary>Lens 5, 2026-11-07: a lead within attack range of its tower turned to close on an enemy 40 m back. The tower comes first.</summary>
    [TestMethod]
    public void ALeadInAttackRangeOfTheNextEnemyTower_AttacksIt_InsteadOfClosing()
    {
        var lead = new Vec3f(600f, 0f, 0f);
        var s = new LaneSituation(MatchSide.West, 0, LaneOrder.Push, 10, lead, 40f, FordsTestLayout.EastOuter0, 15f, false, lead, 600f, 7,
            15f, NearestEnemy: new Vec3f(560f, 0f, 0f));

        Assert.AreEqual(LaneBranch.AttackTower, Rules(Open).Evaluate(s).Branch);
    }

    /// <summary>
    /// Codex, 2026-11-07: a target snapped to the lane path stalled a lane beside an enemy 20 m off the path (at the keep the
    /// lanes meet). The target is the enemy's own position, so each order brings the lane nearer.
    /// </summary>
    [TestMethod]
    public void AnEnemyBesideThePath_IsTheTarget_NotTheLanesOwnVertex()
    {
        var lead = new Vec3f(900f, 0f, 0f);
        var s = new LaneSituation(MatchSide.West, 0, LaneOrder.Push, 10, lead, 20f, FordsTestLayout.EastKeep, 60f, false, lead, 900f, 7,
            60f, NearestEnemy: new Vec3f(900f, 20f, 0f));

        var decision = Rules(Wall).Evaluate(s);

        Assert.AreEqual(LaneBranch.Close, decision.Branch);
        Assert.AreEqual(LaneCommand.MoveTo(new Vec3f(900f, 20f, 0f)), decision.Command);
    }

    [TestMethod]
    public void ACharge_HoldsWhileTheEnemyStaysInsideTheCloseRange_SoTheFightDoesNotFlapBackIntoTheWall()
    {
        var rules = Rules(Wall);

        Assert.AreEqual(LaneBranch.Charge, rules.Evaluate(Facing(5f)).Branch);
        Assert.AreEqual(LaneBranch.Charge, rules.Evaluate(Facing(15f)).Branch, "still inside the close range");
        Assert.AreEqual(LaneBranch.March, rules.Evaluate(Facing(50f)).Branch, "the enemy left the close range");
        Assert.AreEqual(LaneBranch.Close, rules.Evaluate(Facing(15f)).Branch, "a new fight closes first");
    }

    [TestMethod]
    public void ALeadLevelWithTheNextEnemyTower_AttacksIt_InsteadOfClosingBack()
    {
        var lead = new Vec3f(650f, 0f, 0f);
        var s = new LaneSituation(MatchSide.West, 0, LaneOrder.Push, 10, lead, 30f, FordsTestLayout.EastOuter0, 10f, false, lead, 650f, 7,
            10f, NearestEnemy: new Vec3f(620f, 0f, 0f));

        var decision = Rules(Wall).Evaluate(s);

        Assert.AreEqual(LaneBranch.AttackTower, decision.Branch);
        Assert.AreEqual(LaneCommand.AttackTower(FordsTestLayout.EastOuter0), decision.Command);
    }

    [TestMethod]
    public void EastSide_AnEnemyPastTheNextEnemyTower_DoesNotDrawTheLanePastIt()
    {
        var lead = new Vec3f(400f, 0f, 0f);
        var s = new LaneSituation(MatchSide.East, 0, LaneOrder.Push, 10, lead, 45f, FordsTestLayout.WestOuter0, 40f, false, lead, 400f, 7,
            40f, NearestEnemy: new Vec3f(355f, 0f, 0f));

        Assert.AreNotEqual(LaneBranch.Close, Rules(east: Wall).Evaluate(s).Branch);
    }

    [TestMethod]
    public void ASeizingLane_KeepsItsObjective_AndOnlyFormsUp()
    {
        var rules = Rules(Wall);
        var objective = new SeizeTarget("cp_1", new Vec3f(330f, 60f, 0f), false);
        var calm = Facing(float.MaxValue, LaneOrder.Seize) with { Seize = objective, SeizeDistance = 60f };
        var closing = Facing(40f, LaneOrder.Seize) with { Seize = objective, SeizeDistance = 60f };

        Assert.AreEqual(LaneBranch.Seize, rules.Evaluate(calm).Branch);
        var decision = rules.Evaluate(closing);
        Assert.AreEqual(LaneBranch.Close, decision.Branch);
        Assert.AreEqual(LaneCommand.HoldAt(objective.Position), decision.Command);
        Assert.AreEqual(LaneArrangement.ShieldWall, decision.Arrangement);
    }

    [TestMethod]
    public void ATacticThatNamesNoMarch_GoesBackToLineAfterItsWall()
    {
        var rules = Rules(new LaneTactic(Close: LaneArrangement.ShieldWall, CloseRange: 45f, ChargeRange: 6f));

        Assert.AreEqual(LaneArrangement.ShieldWall, rules.Evaluate(Facing(40f)).Arrangement);
        Assert.AreEqual(LaneArrangement.Line, rules.Evaluate(Facing(5f)).Arrangement);
    }

    [TestMethod]
    public void AnEnemyInsideTheChargeRange_TheLaneCharges_InItsMarchArrangement()
    {
        var decision = Rules(Wall).Evaluate(Facing(5f));

        Assert.AreEqual(LaneBranch.Charge, decision.Branch);
        Assert.AreEqual(LaneCommand.Charge, decision.Command);
        Assert.AreEqual(LaneArrangement.Line, decision.Arrangement, "a charge leaves the wall, which would hold the fighters to 0.3 of run speed");
    }

    [TestMethod]
    public void NoEnemyInsideTheCloseRange_TheLaneMarches_InItsMarchArrangement()
    {
        var decision = Rules(Wall).Evaluate(Facing(50f));

        Assert.AreEqual(LaneBranch.March, decision.Branch);
        Assert.AreEqual(LaneArrangement.Line, decision.Arrangement);
    }

    [TestMethod]
    public void WithoutATactic_TheEngageRangeCharges_AndNoArrangementIsAsked()
    {
        var rules = Rules();

        var charge = rules.Evaluate(Facing(Config.Lanes.EngageRange));
        var march = rules.Evaluate(Facing(Config.Lanes.EngageRange + 5f));

        Assert.AreEqual(LaneBranch.Charge, charge.Branch);
        Assert.AreEqual(LaneBranch.March, march.Branch);
        Assert.IsNull(charge.Arrangement);
        Assert.IsNull(march.Arrangement);
    }

    [TestMethod]
    public void AChargeRangePastTheEngageRange_ChargesFromFurtherOut()
    {
        var swarm = new LaneTactic(LaneArrangement.Loose, ChargeRange: 35f);

        var decision = Rules(swarm).Evaluate(Facing(30f));

        Assert.AreEqual(LaneBranch.Charge, decision.Branch);
        Assert.AreEqual(LaneArrangement.Loose, decision.Arrangement);
    }

    [TestMethod]
    public void ACloseRangeInsideTheChargeRange_LeavesNoClosePhase()
    {
        var tactic = new LaneTactic(Close: LaneArrangement.ShieldWall, CloseRange: 10f, ChargeRange: 20f);

        Assert.AreEqual(LaneBranch.Charge, Rules(tactic).Evaluate(Facing(15f)).Branch);
        Assert.AreEqual(LaneBranch.March, Rules(tactic).Evaluate(Facing(21f)).Branch);
    }

    [TestMethod]
    public void AHeldLane_FormsItsWallAtItsHoldPoint_AndDoesNotAdvance()
    {
        var rules = Rules(Wall);
        var held = rules.Evaluate(Facing(float.MaxValue, LaneOrder.Hold, towerId: FordsTestLayout.EastOuter0, towerDistance: 310f));

        var closing = rules.Evaluate(Facing(40f, LaneOrder.Hold, towerId: FordsTestLayout.EastOuter0, towerDistance: 310f));

        Assert.AreEqual(LaneBranch.Hold, held.Branch);
        Assert.AreEqual(LaneBranch.Close, closing.Branch);
        Assert.AreEqual(held.Command, closing.Command);
        Assert.AreEqual(LaneArrangement.ShieldWall, closing.Arrangement);
    }

    /// <summary>
    /// Defenders behind their own tower do not draw a wall past it: a Move past a standing tower is what check 9 fails, and the
    /// tower rules deal with the tower first. The enemy at x 655 is nearest the vertex at 700, past the tower at 640.
    /// </summary>
    [TestMethod]
    public void AnEnemyNearestAVertexPastTheNextEnemyTower_DoesNotDrawTheLanePastIt()
    {
        var lead = new Vec3f(610f, 0f, 0f);
        var s = new LaneSituation(MatchSide.West, 0, LaneOrder.Push, 10, lead, 45f, FordsTestLayout.EastOuter0, 30f, false, lead,
            610f, 7, 30f, NearestEnemy: new Vec3f(655f, 0f, 0f));

        var decision = Rules(Wall).Evaluate(s);

        Assert.AreNotEqual(LaneBranch.Close, decision.Branch);
        Assert.AreEqual(LaneArrangement.Line, decision.Arrangement);
    }

    [TestMethod]
    public void NoEnemyPosition_TheLaneMarchesOn()
    {
        Assert.AreEqual(LaneBranch.March, Rules(Wall).Evaluate(Facing(40f, enemyPosition: false)).Branch);
    }

    [TestMethod]
    public void TheTacticBelongsToItsSide()
    {
        var rules = Rules(west: Wall);

        var east = rules.Evaluate(Facing(40f, side: MatchSide.East));

        Assert.AreEqual(LaneBranch.March, east.Branch);
        Assert.IsNull(east.Arrangement);
    }

    [TestMethod]
    public void FallBackAndASiege_OutrankTheClosePhase()
    {
        var rules = Rules(Wall);

        var back = rules.Evaluate(Facing(40f, LaneOrder.FallBack));
        var siege = rules.Evaluate(Facing(40f, towerId: FordsTestLayout.EastOuter0, towerDistance: 30f, attackers: 10, defenders: 2));

        Assert.AreEqual(LaneBranch.FallBack, back.Branch);
        Assert.AreEqual(LaneArrangement.Line, back.Arrangement);
        Assert.AreEqual(LaneBranch.AttackTower, siege.Branch);
        Assert.AreEqual(LaneArrangement.Line, siege.Arrangement);
    }

    [TestMethod]
    public void TheLogLine_NamesTheArrangement_AndReadsAsBeforeWithoutOne()
    {
        var wall = Rules(Wall).Evaluate(Facing(40f)).ToLogBody();
        var plain = Rules().Evaluate(Facing(40f)).ToLogBody();

        StringAssert.EndsWith(wall, " arr=ShieldWall");
        StringAssert.Contains(wall, " branch=Close ");
        Assert.IsFalse(plain.Contains("arr="), plain);
    }
}
