using System;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.CreatureSiegeRole.Domain;
using static TAOM.Tests.Features.CreatureSiegeRole.CreatureSiegeFixtures;

namespace TAOM.Tests.Features.CreatureSiegeRole;

// The role decision. One test per row of the design's table (synthesis B7), then the invariants over every input.
public partial class CreatureSiegeRulesTests
{
    private static RoleDecision Decide(RoleInputs inputs) => CreatureSiegeRules.DecideRole(in inputs);

    private static void AssertRole(SiegeRole role, RoleReason reason, RoleInputs inputs)
    {
        var decision = Decide(inputs);
        Assert.AreEqual(new RoleDecision(role, reason), decision, $"{decision} for {inputs}");
    }

    // --- not routed at all ------------------------------------------------------------------------------------------

    [TestMethod]
    public void DecideRole_NotAIControlled_IsReleased_ForEverySide()
    {
        AssertRole(SiegeRole.Release, RoleReason.NotAIControlled, AttackerInputs(isAIControlled: false));
        AssertRole(SiegeRole.Release, RoleReason.NotAIControlled, AttackerInputs(isAIControlled: false, side: SiegeSide.Defender));
    }

    [TestMethod]
    public void DecideRole_NotAIControlled_WinsOverFleeingAndRetreat()
    {
        AssertRole(SiegeRole.Release, RoleReason.NotAIControlled,
            AttackerInputs(isAIControlled: false, isFleeing: true, formation: AIFormation(SiegeFormationOrder.Retreat)));
    }

    [TestMethod]
    public void DecideRole_Fleeing_IsReleased()
    {
        AssertRole(SiegeRole.Release, RoleReason.Fleeing, AttackerInputs(isFleeing: true));
        AssertRole(SiegeRole.Release, RoleReason.Fleeing, AttackerInputs(isFleeing: true, side: SiegeSide.Defender));
    }

    [TestMethod]
    public void DecideRole_ARetreatOrder_IsReleased_UnderAnyCommand()
    {
        AssertRole(SiegeRole.Release, RoleReason.RetreatOrder, AttackerInputs(formation: AIFormation(SiegeFormationOrder.Retreat)));
        AssertRole(SiegeRole.Release, RoleReason.RetreatOrder, AttackerInputs(formation: PlayerFormation(SiegeFormationOrder.Retreat)));
        AssertRole(SiegeRole.Release, RoleReason.RetreatOrder,
            AttackerInputs(side: SiegeSide.Defender, formation: AIFormation(SiegeFormationOrder.Retreat)));
    }

    [TestMethod]
    public void DecideRole_NoSide_IsReleased()
    {
        AssertRole(SiegeRole.Release, RoleReason.NoSide, AttackerInputs(side: SiegeSide.None));
    }

    // --- attackers under AI command ---------------------------------------------------------------------------------

    [TestMethod]
    public void DecideRole_AttackerOuterGateShut_NoRam_StrikesTheOuterGate()
    {
        AssertRole(SiegeRole.StrikeOuter, RoleReason.OuterGateClosed, AttackerInputs());
    }

    [TestMethod]
    public void DecideRole_AttackerOuterGateShut_RamAtWork_StandsOff()
    {
        AssertRole(SiegeRole.StandOff, RoleReason.RamWorking, AttackerInputs(ramWorking: true));
    }

    [TestMethod]
    public void DecideRole_AttackerWithNoFormation_IsTreatedAsAIControlled()
    {
        AssertRole(SiegeRole.StrikeOuter, RoleReason.OuterGateClosed, AttackerInputs(formation: SiegeFormationState.None));
        AssertRole(SiegeRole.StrikeOuter, RoleReason.OuterGateClosed, AttackerInputs(formation: AIFormation()));
    }

    [TestMethod]
    public void DecideRole_AttackerOuterGatePassable_InnerGateShut_StrikesTheInnerGate()
    {
        AssertRole(SiegeRole.StrikeInner, RoleReason.InnerGateClosed, AttackerInputs(outerPassable: true));
    }

    [TestMethod]
    public void DecideRole_AttackerOuterGatePassable_InnerGatePassable_HoldsTheCourtyard()
    {
        AssertRole(SiegeRole.HoldCourtyard, RoleReason.BothGatesOpen, AttackerInputs(outerPassable: true, innerPassable: true));
    }

    [TestMethod]
    public void DecideRole_AttackerOuterGatePassable_NoInnerGate_HoldsTheCourtyard()
    {
        AssertRole(SiegeRole.HoldCourtyard, RoleReason.BothGatesOpen, AttackerInputs(outerPassable: true, innerPresent: false));
    }

    [TestMethod]
    public void DecideRole_AttackerPassableOuterGate_IgnoresTheRam()
    {
        // The ram deactivates once the gate it works on is open or destroyed; a stale "working" never holds a creature back.
        AssertRole(SiegeRole.StrikeInner, RoleReason.InnerGateClosed, AttackerInputs(outerPassable: true, ramWorking: true));
        AssertRole(SiegeRole.HoldCourtyard, RoleReason.BothGatesOpen,
            AttackerInputs(outerPassable: true, innerPassable: true, ramWorking: true));
    }

    [TestMethod]
    public void DecideRole_AttackerAlreadyThroughAShutOuterGate_SkipsTheOuterRole_ForTheInnerGate()
    {
        // The defenders opened the gate for two seconds, the creature went through, and the gate shut behind it.
        AssertRole(SiegeRole.StrikeInner, RoleReason.InnerGateClosed, AttackerInputs(pastOuterGate: true));
        AssertRole(SiegeRole.StrikeInner, RoleReason.InnerGateClosed, AttackerInputs(pastOuterGate: true, ramWorking: true));
    }

    [TestMethod]
    public void DecideRole_AttackerThroughAShutOuterGate_NoInnerGateToBreak_HoldsTheCourtyard()
    {
        AssertRole(SiegeRole.HoldCourtyard, RoleReason.BothGatesOpen, AttackerInputs(pastOuterGate: true, innerPresent: false));
        AssertRole(SiegeRole.HoldCourtyard, RoleReason.BothGatesOpen, AttackerInputs(pastOuterGate: true, innerPassable: true));
    }

    [TestMethod]
    public void DecideRole_AttackerNoInnerGate_OuterGateShut_StillStrikesTheOuterGate()
    {
        AssertRole(SiegeRole.StrikeOuter, RoleReason.OuterGateClosed, AttackerInputs(innerPresent: false));
    }

    // --- attackers the player commands ------------------------------------------------------------------------------

    [DataTestMethod]
    [DataRow(SiegeFormationOrder.Charge)]
    [DataRow(SiegeFormationOrder.ChargeToTarget)]
    public void DecideRole_PlayerChargesBeforeBothGatesAreOpen_TheCreatureGoesToTheGate(SiegeFormationOrder order)
    {
        AssertRole(SiegeRole.StrikeOuter, RoleReason.OuterGateClosed, AttackerInputs(formation: PlayerFormation(order)));
        AssertRole(SiegeRole.StandOff, RoleReason.RamWorking, AttackerInputs(formation: PlayerFormation(order), ramWorking: true));
        AssertRole(SiegeRole.StrikeInner, RoleReason.InnerGateClosed,
            AttackerInputs(formation: PlayerFormation(order), outerPassable: true));
    }

    [DataTestMethod]
    [DataRow(SiegeFormationOrder.Charge)]
    [DataRow(SiegeFormationOrder.ChargeToTarget)]
    public void DecideRole_PlayerChargesAfterBothGatesAreOpen_TheCreatureObeys(SiegeFormationOrder order)
    {
        // D7: after the breach the creature is released, and may take the stairs the player pointed it at.
        AssertRole(SiegeRole.Release, RoleReason.PlayerOrderAfterBreach,
            AttackerInputs(formation: PlayerFormation(order), outerPassable: true, innerPassable: true));
        AssertRole(SiegeRole.Release, RoleReason.PlayerOrderAfterBreach,
            AttackerInputs(formation: PlayerFormation(order), outerPassable: true, innerPresent: false));
    }

    [TestMethod]
    public void DecideRole_PlayerGivesAnyOtherOrder_TheCreatureObeys()
    {
        AssertRole(SiegeRole.Release, RoleReason.PlayerOrder, AttackerInputs(formation: PlayerFormation(SiegeFormationOrder.Other)));
        AssertRole(SiegeRole.Release, RoleReason.PlayerOrder,
            AttackerInputs(formation: PlayerFormation(SiegeFormationOrder.Other), outerPassable: true));
    }

    [TestMethod]
    public void DecideRole_PlayerGivesAnyOtherOrderAfterTheBreach_TheCreatureObeys()
    {
        AssertRole(SiegeRole.Release, RoleReason.PlayerOrderAfterBreach,
            AttackerInputs(formation: PlayerFormation(SiegeFormationOrder.Other), outerPassable: true, innerPassable: true));
    }

    // --- defenders --------------------------------------------------------------------------------------------------

    [TestMethod]
    public void DecideRole_DefenderUnderAIOrNoFormation_HoldsTheGate()
    {
        AssertRole(SiegeRole.HoldGate, RoleReason.DefendingGate, AttackerInputs(side: SiegeSide.Defender));
        AssertRole(SiegeRole.HoldGate, RoleReason.DefendingGate,
            AttackerInputs(side: SiegeSide.Defender, formation: AIFormation(SiegeFormationOrder.Charge)));
    }

    [TestMethod]
    public void DecideRole_DefenderKeepsHoldingTheGate_AfterTheBreach()
    {
        // D10: after the breach the troll holds its anchor and fights what reaches it; it never goes up to the wall.
        AssertRole(SiegeRole.HoldGate, RoleReason.DefendingGate,
            AttackerInputs(side: SiegeSide.Defender, outerPassable: true, innerPassable: true));
        AssertRole(SiegeRole.HoldGate, RoleReason.DefendingGate,
            AttackerInputs(side: SiegeSide.Defender, outerPassable: true, innerPresent: false, pastOuterGate: true));
    }

    [DataTestMethod]
    [DataRow(SiegeFormationOrder.Other)]
    [DataRow(SiegeFormationOrder.Charge)]
    [DataRow(SiegeFormationOrder.ChargeToTarget)]
    public void DecideRole_DefenderInAFormationThePlayerCommands_Obeys(SiegeFormationOrder order)
    {
        // D8.
        AssertRole(SiegeRole.Release, RoleReason.PlayerDefender,
            AttackerInputs(side: SiegeSide.Defender, formation: PlayerFormation(order)));
    }

    // --- invariants over every input --------------------------------------------------------------------------------

    [TestMethod]
    public void DecideRole_OverEveryInput_ObeysTheFixedPrecedence()
    {
        var formations = new[]
        {
            SiegeFormationState.None,
            AIFormation(SiegeFormationOrder.Other), AIFormation(SiegeFormationOrder.Charge),
            AIFormation(SiegeFormationOrder.ChargeToTarget), AIFormation(SiegeFormationOrder.Retreat),
            PlayerFormation(SiegeFormationOrder.Other), PlayerFormation(SiegeFormationOrder.Charge),
            PlayerFormation(SiegeFormationOrder.ChargeToTarget), PlayerFormation(SiegeFormationOrder.Retreat),
        };

        var rows = 0;
        foreach (var side in Enum.GetValues(typeof(SiegeSide)).Cast<SiegeSide>())
        foreach (var formation in formations)
        for (var bits = 0; bits < 64; bits++)
        {
            var inputs = new RoleInputs(
                IsAIControlled: (bits & 1) != 0, IsFleeing: (bits & 2) != 0, side, formation,
                OuterPassable: (bits & 4) != 0, InnerPresent: (bits & 8) != 0, InnerPassable: (bits & 16) != 0,
                PastOuterGate: (bits & 32) != 0, RamWorking: (bits & 4) == 0 && (bits & 2) == 0);
            var decision = Decide(inputs);
            rows++;

            if (!inputs.IsAIControlled)
                Assert.AreEqual(RoleReason.NotAIControlled, decision.Reason, inputs.ToString());
            else if (inputs.IsFleeing)
                Assert.AreEqual(RoleReason.Fleeing, decision.Reason, inputs.ToString());
            else if (formation.Order == SiegeFormationOrder.Retreat)
                Assert.AreEqual(RoleReason.RetreatOrder, decision.Reason, inputs.ToString());
            else if (side == SiegeSide.None)
                Assert.AreEqual(RoleReason.NoSide, decision.Reason, inputs.ToString());
            else if (side == SiegeSide.Defender && !formation.IsPlayerCommanded)
                Assert.AreEqual(SiegeRole.HoldGate, decision.Role, inputs.ToString());
            else if (side == SiegeSide.Defender)
                Assert.AreEqual(RoleReason.PlayerDefender, decision.Reason, inputs.ToString());
            else if (!formation.IsPlayerCommanded)
                Assert.IsTrue(decision.Role is SiegeRole.StandOff or SiegeRole.StrikeOuter or SiegeRole.StrikeInner or SiegeRole.HoldCourtyard,
                    $"an attacker under AI command is always routed: {decision} for {inputs}");

            // A role that is not Release names a reason that is not a release reason, and the reverse.
            Assert.AreEqual(decision.Role == SiegeRole.Release, IsReleaseReason(decision.Reason), $"{decision} for {inputs}");
        }

        Assert.AreEqual(3 * 9 * 64, rows);
    }

    private static bool IsReleaseReason(RoleReason reason) =>
        reason is RoleReason.NotAIControlled or RoleReason.Fleeing or RoleReason.RetreatOrder or RoleReason.NoSide
            or RoleReason.PlayerOrder or RoleReason.PlayerOrderAfterBreach or RoleReason.PlayerDefender;

    [TestMethod]
    public void DecideRole_AnAttackerOnlyStandsOffWhileTheOuterGateIsShutAndItIsNotThroughIt()
    {
        foreach (var outerPassable in new[] { false, true })
        foreach (var past in new[] { false, true })
        {
            var decision = Decide(AttackerInputs(outerPassable: outerPassable, pastOuterGate: past, ramWorking: true));

            Assert.AreEqual(!outerPassable && !past, decision.Role == SiegeRole.StandOff,
                $"outerPassable {outerPassable}, past {past}: {decision}");
        }
    }
}
