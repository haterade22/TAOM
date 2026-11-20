using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.Stamina.Domain;
using TaleWorlds.MountAndBlade;

namespace TAOM.Tests.Features.Stamina;

[TestClass]
public class StaminaInputRulesTests
{
    private const uint Forward = 0x1;
    private const uint TurnRight = 0x10;

    [TestMethod]
    public void Masks_MatchTheEngineEnums()
    {
        // The rules hold the engine's bit values as numbers, so the domain names no engine type. An engine update
        // that moves a bit must fail here, not in a battle.
        Assert.AreEqual((uint)Agent.MovementControlFlag.AttackMask, StaminaInputRules.AttackMask);
        Assert.AreEqual((uint)Agent.MovementControlFlag.DefendMask, StaminaInputRules.DefendMask);
        Assert.AreEqual((uint)Agent.MovementControlFlag.DefendBlock, StaminaInputRules.DefendBlock);
        Assert.AreEqual((uint)Agent.EventControlFlag.Jump, StaminaInputRules.JumpEvent);
        Assert.AreEqual((uint)Agent.EventControlFlag.Kick, StaminaInputRules.KickEvent);
    }

    [TestMethod]
    public void StripMovement_AttackAndDefendHeld_RemovesBothAndKeepsTheWalk()
    {
        var flags = Forward | TurnRight | (uint)Agent.MovementControlFlag.AttackUp | (uint)Agent.MovementControlFlag.DefendLeft;

        var stripped = StaminaInputRules.StripMovement(flags);

        Assert.AreEqual(Forward | TurnRight, stripped);
    }

    [TestMethod]
    public void StripMovement_EveryAttackAndDefendDirection_LeavesNone()
    {
        var stripped = StaminaInputRules.StripMovement(StaminaInputRules.AttackMask | StaminaInputRules.DefendMask);

        Assert.AreEqual(0u, stripped);
    }

    [TestMethod]
    public void StripMovement_TheBlockBitOutsideTheEnginesDefendMask_IsRemovedToo()
    {
        // Agent.MovementControlFlag.DefendMask (0x7C00) is the four directions and DefendAuto; DefendBlock (0x8000) is a
        // separate bit, so a strip of the mask alone would leave it.
        Assert.AreEqual(0u, (uint)Agent.MovementControlFlag.DefendMask & (uint)Agent.MovementControlFlag.DefendBlock);

        var stripped = StaminaInputRules.StripMovement(Forward | (uint)Agent.MovementControlFlag.DefendBlock);

        Assert.AreEqual(Forward, stripped);
    }

    [TestMethod]
    public void StripMovement_TheActionBit_Stays()
    {
        var action = (uint)Agent.MovementControlFlag.Action;

        Assert.AreEqual(action, StaminaInputRules.StripMovement(action));
    }

    [TestMethod]
    public void StripMovement_NoCombatInput_ChangesNothing()
    {
        Assert.AreEqual(Forward, StaminaInputRules.StripMovement(Forward));
    }

    [TestMethod]
    public void StripEvents_OnFootOnLand_RemovesJumpAndKickAndKeepsTheRest()
    {
        var other = (uint)Agent.EventControlFlag.Crouch;
        var flags = StaminaInputRules.JumpEvent | StaminaInputRules.KickEvent | other;

        var stripped = StaminaInputRules.StripEvents(flags, mounted: false, inWater: false);

        Assert.AreEqual(other, stripped);
    }

    [TestMethod]
    public void StripEvents_Mounted_KeepsTheHorsesJumpAndRemovesTheKick()
    {
        var flags = StaminaInputRules.JumpEvent | StaminaInputRules.KickEvent;

        var stripped = StaminaInputRules.StripEvents(flags, mounted: true, inWater: false);

        Assert.AreEqual(StaminaInputRules.JumpEvent, stripped);
    }

    [TestMethod]
    public void StripEvents_InWater_KeepsTheJumpThatRaisesASwimmer()
    {
        var flags = StaminaInputRules.JumpEvent | StaminaInputRules.KickEvent;

        var stripped = StaminaInputRules.StripEvents(flags, mounted: false, inWater: true);

        Assert.AreEqual(StaminaInputRules.JumpEvent, stripped);
    }
}
