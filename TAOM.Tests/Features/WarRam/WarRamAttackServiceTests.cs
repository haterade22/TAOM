using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.WarRam;
using TAOM.Features.ElephantLike;

// War ram attack -- pure decision-service tests. Same shared elephant-like decision surface as the
// elephant/Mumakil (no TaleWorlds dependencies, every branch reachable without mocks), tuned far softer
// per WarRamConfig. Unlike the elephant/Mumakil, WarRamBehaviorTree wires only the kick (Trample-kind)
// branch -- WarRamAttackService binds the SideAttack band to the SAME numbers as the kick band (see
// WarRamAttackService's remarks), so the SideAttack-kind tests below assert that dead-but-reachable path
// stays sane rather than covering a distinct in-game behaviour. 1-for-1 structure with
// MumakilAttackServiceTests.

namespace TAOM.Tests.Features.WarRam;

[TestClass]
public class WarRamAttackServiceTests
{
    private WarRamAttackService _sut = null!;

    [TestInitialize]
    public void Setup() => _sut = new WarRamAttackService();

    // ------------------------------------------------------------------ IsCreatureMonster

    [TestMethod]
    public void IsCreatureMonster_WarRamId_ReturnsTrue()
        => Assert.IsTrue(_sut.IsCreatureMonster(WarRamConfig.WarRamMonsterId));

    [TestMethod]
    public void IsCreatureMonster_OtherId_ReturnsFalse()
        => Assert.IsFalse(_sut.IsCreatureMonster("taom_war_elephant"));

    [TestMethod]
    public void IsCreatureMonster_Null_ReturnsFalse()
        => Assert.IsFalse(_sut.IsCreatureMonster(null));

    // ------------------------------------------------------------------ ShouldEngage (facing + anim gates)

    [TestMethod]
    public void ShouldEngage_FacingTargetNotAttacking_ReturnsTrue()
        => Assert.IsTrue(_sut.ShouldEngage(facingDot: 0.9f, alreadyAttacking: false));

    [TestMethod]
    public void ShouldEngage_AlreadyAttacking_ReturnsFalse()
        => Assert.IsFalse(_sut.ShouldEngage(0.9f, alreadyAttacking: true));

    [TestMethod]
    public void ShouldEngage_NotFacingTarget_ReturnsFalse()
        => Assert.IsFalse(_sut.ShouldEngage(facingDot: 0.1f, alreadyAttacking: false));

    [TestMethod]
    public void ShouldEngage_FacingDotExactlyAtThreshold_ReturnsFalse()
        // Strict "> AttackFacingDot" (elephant-like parity, `> 0.25f`), so exactly at the threshold does not engage.
        => Assert.IsFalse(_sut.ShouldEngage(facingDot: WarRamConfig.AttackFacingDot, alreadyAttacking: false));

    [TestMethod]
    public void ShouldEngage_NoEnemyFoundSentinel_ReturnsFalse()
        // The BT scan reports -1 when no live enemy is within trigger range -- must never engage.
        => Assert.IsFalse(_sut.ShouldEngage(facingDot: -1f, alreadyAttacking: false));

    // ------------------------------------------------------------------ IsOffCooldown

    private static readonly DateTime Now = new(2026, 8, 28, 12, 0, 0);

    [TestMethod]
    public void IsOffCooldown_NeverFired_ReturnsTrue()
        => Assert.IsTrue(_sut.IsOffCooldown(lastFired: null, now: Now, cooldownSeconds: 6.0));

    [TestMethod]
    public void IsOffCooldown_JustFired_ReturnsFalse()
        => Assert.IsFalse(_sut.IsOffCooldown(lastFired: Now, now: Now, cooldownSeconds: 6.0));

    [TestMethod]
    public void IsOffCooldown_PartiallyElapsed_ReturnsFalse()
        => Assert.IsFalse(_sut.IsOffCooldown(lastFired: Now.AddSeconds(-3), now: Now, cooldownSeconds: 6.0));

    [TestMethod]
    public void IsOffCooldown_ExactlyAtCooldown_ReturnsTrue()
        // Inclusive ">= cooldown" -- the attack becomes available the moment the window closes.
        => Assert.IsTrue(_sut.IsOffCooldown(lastFired: Now.AddSeconds(-6), now: Now, cooldownSeconds: 6.0));

    [TestMethod]
    public void IsOffCooldown_FullyElapsed_ReturnsTrue()
        => Assert.IsTrue(_sut.IsOffCooldown(lastFired: Now.AddSeconds(-60), now: Now, cooldownSeconds: 6.0));

    [TestMethod]
    public void IsOffCooldown_LastFiredInFuture_ReturnsFalse()
        // Clock skew / bad stamp: a future lastFired must read as ON cooldown, not off.
        => Assert.IsFalse(_sut.IsOffCooldown(lastFired: Now.AddSeconds(30), now: Now, cooldownSeconds: 6.0));

    // ------------------------------------------------------------------ ComputeInflictedDamage
    // Per-hit randomized damage: the roll is a [0,1] float supplied by the BT node (MBRandom.RandomFloat)
    // so the service stays pure. Kick (Trample kind) 18-28 before block scaling -- far softer than the
    // elephant's 50-100, appropriate for a dwarf cavalry mount rather than a siege-beast (see
    // WarRamConfig's damage-band comment for the full justification). WarRamAttackService binds the
    // SideAttack band to the SAME 18-28, so those assertions double as proof the never-fired branch
    // would not misbehave if it were ever reached.

    [TestMethod]
    public void ComputeInflictedDamage_ButtRollZero_ReturnsMin()
        => Assert.AreEqual(18, _sut.ComputeInflictedDamage(ElephantLikeAttackKind.Trample, targetBlocking: false, roll: 0f));

    [TestMethod]
    public void ComputeInflictedDamage_ButtRollMax_ReturnsMax()
        => Assert.AreEqual(28, _sut.ComputeInflictedDamage(ElephantLikeAttackKind.Trample, targetBlocking: false, roll: 1f));

    [TestMethod]
    public void ComputeInflictedDamage_ButtRollHalf_ReturnsMidpoint()
        // 18 + round(0.5 * (28-18)) = 18 + 5 = 23.
        => Assert.AreEqual(23, _sut.ComputeInflictedDamage(ElephantLikeAttackKind.Trample, targetBlocking: false, roll: 0.5f));

    [TestMethod]
    public void ComputeInflictedDamage_ButtBlockingMaxRoll_ScaledToQuarter()
        // round(28 * 0.25) = 7.
        => Assert.AreEqual(7, _sut.ComputeInflictedDamage(ElephantLikeAttackKind.Trample, targetBlocking: true, roll: 1f));

    [TestMethod]
    public void ComputeInflictedDamage_SideAttackRollZero_ReturnsMin()
        => Assert.AreEqual(18, _sut.ComputeInflictedDamage(ElephantLikeAttackKind.SideAttack, targetBlocking: false, roll: 0f));

    [TestMethod]
    public void ComputeInflictedDamage_SideAttackRollMax_ReturnsMax()
        => Assert.AreEqual(28, _sut.ComputeInflictedDamage(ElephantLikeAttackKind.SideAttack, targetBlocking: false, roll: 1f));

    [TestMethod]
    public void ComputeInflictedDamage_SideAttackBlockingMaxRoll_ScaledToQuarter()
        => Assert.AreEqual(7, _sut.ComputeInflictedDamage(ElephantLikeAttackKind.SideAttack, targetBlocking: true, roll: 1f));

    [TestMethod]
    public void ComputeInflictedDamage_RollNaN_TreatedAsMin()
        // Defensive: a NaN roll must not slip past the band (NaN comparisons are always false). Clamp -> min.
        => Assert.AreEqual(18, _sut.ComputeInflictedDamage(ElephantLikeAttackKind.Trample, targetBlocking: false, roll: float.NaN));

    [TestMethod]
    public void ComputeInflictedDamage_RollAboveOne_ClampedToMax()
        // Defensive: an out-of-range roll clamps to [0,1] -> max, never above the band.
        => Assert.AreEqual(28, _sut.ComputeInflictedDamage(ElephantLikeAttackKind.Trample, targetBlocking: false, roll: 5f));

    [TestMethod]
    public void ComputeInflictedDamage_RollBelowZero_ClampedToMin()
        => Assert.AreEqual(18, _sut.ComputeInflictedDamage(ElephantLikeAttackKind.Trample, targetBlocking: false, roll: -3f));
}
