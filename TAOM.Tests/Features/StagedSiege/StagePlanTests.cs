using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.StagedSiege.Domain;
using static TAOM.Tests.Features.StagedSiege.StagedSiegeFixtures;

namespace TAOM.Tests.Features.StagedSiege;

/// <summary>The stage table: what each team's AI does at each point, and when vanilla's siege tactics take over.</summary>
[TestClass]
public class StagePlanTests
{
    [TestMethod]
    public void TargetFor_Melee_IsTheZoneCentre_ForBothRoles()
    {
        var s = StageC();
        Assert.AreEqual(s.Zone.Center, StagePlan.TargetFor(isRanged: false, TacticMode.TakePoint, s));
        Assert.AreEqual(s.Zone.Center, StagePlan.TargetFor(isRanged: false, TacticMode.HoldPoint, s));
    }

    [TestMethod]
    public void TargetFor_RangedAttackers_StandAtThePreviousPoint()
    {
        var s = StageC();
        Assert.AreEqual(s.AttackerSupport!.Value, StagePlan.TargetFor(isRanged: true, TacticMode.TakePoint, s));
    }

    [TestMethod]
    public void TargetFor_RangedDefenders_StandAtTheHoldFrame()
    {
        var s = StageC();
        Assert.AreEqual(s.DefenderHold!.Value.Position, StagePlan.TargetFor(isRanged: true, TacticMode.HoldPoint, s));
    }

    [TestMethod]
    public void TargetFor_RangedInStageA_FallsBackToTheZoneCentre()
    {
        var s = StageSnapshot.Build(new object(), LinePlan(), new StageMachine(StagedSide.Attacker), 0f, CaptureStatus.Idle);
        Assert.AreEqual(s.Zone.Center, StagePlan.TargetFor(isRanged: true, TacticMode.TakePoint, s));
    }

    private static StageSnapshot StageC()
    {
        var m = new StageMachine(StagedSide.Attacker);
        m.Force(StageId.C);
        return StageSnapshot.Build(new object(), LinePlan(), m, 0f, CaptureStatus.Idle);
    }

    [TestMethod]
    public void ModeFor_A_AttackerTakesDefenderVanilla()
    {
        Assert.AreEqual(TacticMode.TakePoint, StagePlan.ModeFor(StageId.A, StagedSide.Attacker));
        Assert.AreEqual(TacticMode.Vanilla, StagePlan.ModeFor(StageId.A, StagedSide.Defender));
    }

    [TestMethod]
    public void ModeFor_B_BothVanilla()
    {
        Assert.AreEqual(TacticMode.Vanilla, StagePlan.ModeFor(StageId.B, StagedSide.Attacker));
        Assert.AreEqual(TacticMode.Vanilla, StagePlan.ModeFor(StageId.B, StagedSide.Defender));
    }

    [DataTestMethod]
    [DataRow(StageId.C)]
    [DataRow(StageId.D)]
    public void ModeFor_CAndD_AttackerTakesDefenderHolds(StageId stage)
    {
        Assert.AreEqual(TacticMode.TakePoint, StagePlan.ModeFor(stage, StagedSide.Attacker));
        Assert.AreEqual(TacticMode.HoldPoint, StagePlan.ModeFor(stage, StagedSide.Defender));
    }

    [TestMethod]
    public void TacticWeight_Active1000Vanilla0()
    {
        Assert.AreEqual(1000f, StagePlan.TacticWeight(StageId.A, StagedSide.Attacker));
        Assert.AreEqual(1000f, StagePlan.TacticWeight(StageId.D, StagedSide.Defender));
        Assert.AreEqual(0f, StagePlan.TacticWeight(StageId.A, StagedSide.Defender));
        Assert.AreEqual(0f, StagePlan.TacticWeight(StageId.B, StagedSide.Attacker));
    }

    [TestMethod]
    public void StopsMachines_PlayerCommandsTheSide_LeavesEveryTeamsEngines()
    {
        // StopUsingAllMachines clears ForcedUse on every engine of the SIDE, so an AI ally team would reset a player
        // general's engine orders unless the rule asks about the side, not the team.
        Assert.IsFalse(StagePlan.StopsMachines(StageId.A, StagedSide.Attacker, playerCommandsSide: true));
        Assert.IsTrue(StagePlan.StopsMachines(StageId.A, StagedSide.Attacker, playerCommandsSide: false));
        Assert.IsFalse(StagePlan.StopsMachines(StageId.A, StagedSide.Defender, playerCommandsSide: false));
        Assert.IsFalse(StagePlan.StopsMachines(StageId.B, StagedSide.Attacker, playerCommandsSide: false));
    }

    [TestMethod]
    public void StopsMachines_OnlyAttackerInA()
    {
        Assert.IsTrue(StagePlan.StopsMachines(StageId.A, StagedSide.Attacker));
        Assert.IsFalse(StagePlan.StopsMachines(StageId.A, StagedSide.Defender));
        Assert.IsFalse(StagePlan.StopsMachines(StageId.B, StagedSide.Attacker));
        Assert.IsFalse(StagePlan.StopsMachines(StageId.C, StagedSide.Attacker));
        Assert.IsFalse(StagePlan.StopsMachines(StageId.D, StagedSide.Attacker));
    }

    [TestMethod]
    public void NeedsDefendBehavior_AllButB()
    {
        Assert.IsTrue(StagePlan.NeedsDefendBehavior(StageId.A));
        Assert.IsFalse(StagePlan.NeedsDefendBehavior(StageId.B));
        Assert.IsTrue(StagePlan.NeedsDefendBehavior(StageId.C));
        Assert.IsTrue(StagePlan.NeedsDefendBehavior(StageId.D));
    }

    [TestMethod]
    public void FormationBit_DistinctFormations_GiveDistinctBits()
    {
        Assert.AreEqual(1, StagePlan.FormationBit(0));
        Assert.AreEqual(2, StagePlan.FormationBit(1));
        Assert.AreNotEqual(StagePlan.FormationBit(2), StagePlan.FormationBit(3));
    }

    [TestMethod]
    public void FormationBit_OutOfRange_IsZero()
    {
        Assert.AreEqual(0, StagePlan.FormationBit(-1));
        Assert.AreEqual(0, StagePlan.FormationBit(31));
    }

    [TestMethod]
    public void FormationMask_OneEmptiesWhileAnotherFills_ChangesEvenThoughTheCountIsSame()
    {
        var before = StagePlan.FormationBit(0) | StagePlan.FormationBit(1);
        var after = StagePlan.FormationBit(0) | StagePlan.FormationBit(2);

        Assert.AreNotEqual(before, after);
    }
}
