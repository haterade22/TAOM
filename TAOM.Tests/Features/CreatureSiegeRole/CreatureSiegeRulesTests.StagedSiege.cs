using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.CreatureSiegeRole.Domain;
using static TAOM.Tests.Features.CreatureSiegeRole.CreatureSiegeFixtures;

namespace TAOM.Tests.Features.CreatureSiegeRole;

// The role yields to the staged siege plan (docs/features/staged-siege.md "Sharing the field").
public partial class CreatureSiegeRulesTests
{
    [DataTestMethod]
    [DataRow(SiegeSide.Attacker)]
    [DataRow(SiegeSide.Defender)]
    public void DecideRole_StagedPlanOwnsTheSide_IsReleased_ForTheStagedSiege(SiegeSide side)
    {
        AssertRole(SiegeRole.Release, RoleReason.StagedSiege, AttackerInputs(side: side, stagedPlanOwnsSide: true));
    }

    [TestMethod]
    public void DecideRole_StagedPlanOwnsTheSide_BeatsEveryGateAndRamRow()
    {
        AssertRole(SiegeRole.Release, RoleReason.StagedSiege, AttackerInputs(stagedPlanOwnsSide: true, ramWorking: true));
        AssertRole(SiegeRole.Release, RoleReason.StagedSiege, AttackerInputs(stagedPlanOwnsSide: true, outerPassable: true));
        AssertRole(SiegeRole.Release, RoleReason.StagedSiege,
            AttackerInputs(stagedPlanOwnsSide: true, outerPassable: true, innerPassable: true));
        AssertRole(SiegeRole.Release, RoleReason.StagedSiege, AttackerInputs(stagedPlanOwnsSide: true, pastOuterGate: true));
        AssertRole(SiegeRole.Release, RoleReason.StagedSiege,
            AttackerInputs(side: SiegeSide.Defender, stagedPlanOwnsSide: true, formation: AIFormation(SiegeFormationOrder.Charge)));
    }

    [TestMethod]
    public void DecideRole_StagedPlanOwnsTheSide_APlayerCommandedAttackerOnCharge_StillStrikesTheShutGate()
    {
        AssertRole(SiegeRole.StrikeOuter, RoleReason.OuterGateClosed,
            AttackerInputs(stagedPlanOwnsSide: true, formation: PlayerFormation(SiegeFormationOrder.Charge)));
        AssertRole(SiegeRole.StrikeOuter, RoleReason.OuterGateClosed,
            AttackerInputs(stagedPlanOwnsSide: true, formation: PlayerFormation(SiegeFormationOrder.ChargeToTarget)));
    }

    [TestMethod]
    public void DecideRole_StagedPlanOwnsTheSide_APlayerCommandedAttackerOnAnyOtherOrder_ObeysIt()
    {
        AssertRole(SiegeRole.Release, RoleReason.PlayerOrder,
            AttackerInputs(stagedPlanOwnsSide: true, formation: PlayerFormation(SiegeFormationOrder.Other)));
    }

    [TestMethod]
    public void DecideRole_StagedPlanOwnsTheSide_TheThreeEarlierReleasesKeepTheirReasons()
    {
        AssertRole(SiegeRole.Release, RoleReason.NotAIControlled, AttackerInputs(isAIControlled: false, stagedPlanOwnsSide: true));
        AssertRole(SiegeRole.Release, RoleReason.Fleeing, AttackerInputs(isFleeing: true, stagedPlanOwnsSide: true));
        AssertRole(SiegeRole.Release, RoleReason.RetreatOrder,
            AttackerInputs(formation: AIFormation(SiegeFormationOrder.Retreat), stagedPlanOwnsSide: true));
    }

    [TestMethod]
    public void DecideRole_StagedPlanDoesNotOwnTheSide_ChangesNothing()
    {
        AssertRole(SiegeRole.StrikeOuter, RoleReason.OuterGateClosed, AttackerInputs(stagedPlanOwnsSide: false));
        AssertRole(SiegeRole.StandOff, RoleReason.RamWorking, AttackerInputs(ramWorking: true, stagedPlanOwnsSide: false));
        AssertRole(SiegeRole.StrikeInner, RoleReason.InnerGateClosed, AttackerInputs(outerPassable: true, stagedPlanOwnsSide: false));
        AssertRole(SiegeRole.HoldCourtyard, RoleReason.BothGatesOpen,
            AttackerInputs(outerPassable: true, innerPassable: true, stagedPlanOwnsSide: false));
        AssertRole(SiegeRole.HoldGate, RoleReason.DefendingGate, AttackerInputs(side: SiegeSide.Defender, stagedPlanOwnsSide: false));
    }
}
