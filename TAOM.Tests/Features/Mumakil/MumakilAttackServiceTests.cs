using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.Mumakil;
using TAOM.Features.Mumakil.BehaviorTreeElements;

// Mûmakil attack — pure decision-service tests. The Mûmakil is a scaled-up elephant; the service has no
// TaleWorlds dependencies, so every branch is reachable without mocks (the engine values — facing dot, current
// time, blocking — are supplied by the behavior-tree nodes in-game). 1-for-1 with ElephantAttackServiceTests.

namespace TAOM.Tests.Features.Mumakil;

[TestClass]
public class MumakilAttackServiceTests
{
    private MumakilAttackService _sut = null!;

    [TestInitialize]
    public void Setup() => _sut = new MumakilAttackService();

    // ------------------------------------------------------------------ IsMumakilMonster

    [TestMethod]
    public void IsMumakilMonster_MumakilId_ReturnsTrue()
        => Assert.IsTrue(_sut.IsMumakilMonster(MumakilConfig.MumakilMonsterId));

    [TestMethod]
    public void IsMumakilMonster_OtherId_ReturnsFalse()
        => Assert.IsFalse(_sut.IsMumakilMonster("taom_war_elephant"));

    [TestMethod]
    public void IsMumakilMonster_Null_ReturnsFalse()
        => Assert.IsFalse(_sut.IsMumakilMonster(null));

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
        // Strict "> TrampleFacingDot" (elephant parity, `> 0.25f`), so exactly at the threshold does not engage.
        => Assert.IsFalse(_sut.ShouldEngage(facingDot: MumakilConfig.TrampleFacingDot, alreadyAttacking: false));

    [TestMethod]
    public void ShouldEngage_NoEnemyFoundSentinel_ReturnsFalse()
        // The BT scan reports -1 when no live enemy is within trigger range — must never engage.
        => Assert.IsFalse(_sut.ShouldEngage(facingDot: -1f, alreadyAttacking: false));

    // ------------------------------------------------------------------ IsOffCooldown

    private static readonly DateTime Now = new(2026, 6, 29, 12, 0, 0);

    [TestMethod]
    public void IsOffCooldown_NeverFired_ReturnsTrue()
        => Assert.IsTrue(_sut.IsOffCooldown(lastFired: null, now: Now, cooldownSeconds: 10.0));

    [TestMethod]
    public void IsOffCooldown_JustFired_ReturnsFalse()
        => Assert.IsFalse(_sut.IsOffCooldown(lastFired: Now, now: Now, cooldownSeconds: 10.0));

    [TestMethod]
    public void IsOffCooldown_PartiallyElapsed_ReturnsFalse()
        => Assert.IsFalse(_sut.IsOffCooldown(lastFired: Now.AddSeconds(-5), now: Now, cooldownSeconds: 10.0));

    [TestMethod]
    public void IsOffCooldown_ExactlyAtCooldown_ReturnsTrue()
        // Inclusive ">= cooldown" — the attack becomes available the moment the window closes.
        => Assert.IsTrue(_sut.IsOffCooldown(lastFired: Now.AddSeconds(-10), now: Now, cooldownSeconds: 10.0));

    [TestMethod]
    public void IsOffCooldown_FullyElapsed_ReturnsTrue()
        => Assert.IsTrue(_sut.IsOffCooldown(lastFired: Now.AddSeconds(-60), now: Now, cooldownSeconds: 10.0));

    [TestMethod]
    public void IsOffCooldown_LastFiredInFuture_ReturnsFalse()
        // Clock skew / bad stamp: a future lastFired must read as ON cooldown, not off.
        => Assert.IsFalse(_sut.IsOffCooldown(lastFired: Now.AddSeconds(30), now: Now, cooldownSeconds: 10.0));

    // ------------------------------------------------------------------ ComputeInflictedDamage
    // Per-hit randomized damage: each hit rolls in [min,max] by attack kind. The roll is a [0,1] float supplied
    // by the BT node (MBRandom.RandomFloat) so the service stays pure. Trample 50-100, tusk (SideAttack) 50-75;
    // a shield block scales the rolled value to BlockedDamageMultiplier (0.25).

    [TestMethod]
    public void ComputeInflictedDamage_TrampleRollZero_ReturnsMin()
        => Assert.AreEqual(50, _sut.ComputeInflictedDamage(MumakilAttackKind.Trample, targetBlocking: false, roll: 0f));

    [TestMethod]
    public void ComputeInflictedDamage_TrampleRollMax_ReturnsMax()
        => Assert.AreEqual(100, _sut.ComputeInflictedDamage(MumakilAttackKind.Trample, targetBlocking: false, roll: 1f));

    [TestMethod]
    public void ComputeInflictedDamage_TrampleRollHalf_ReturnsMidpoint()
        // 50 + round(0.5 * (100-50)) = 50 + 25 = 75.
        => Assert.AreEqual(75, _sut.ComputeInflictedDamage(MumakilAttackKind.Trample, targetBlocking: false, roll: 0.5f));

    [TestMethod]
    public void ComputeInflictedDamage_TrampleBlockingMaxRoll_ScaledToQuarter()
        // round(100 * 0.25) = 25.
        => Assert.AreEqual(25, _sut.ComputeInflictedDamage(MumakilAttackKind.Trample, targetBlocking: true, roll: 1f));

    [TestMethod]
    public void ComputeInflictedDamage_SideAttackRollZero_ReturnsMin()
        => Assert.AreEqual(50, _sut.ComputeInflictedDamage(MumakilAttackKind.SideAttack, targetBlocking: false, roll: 0f));

    [TestMethod]
    public void ComputeInflictedDamage_SideAttackRollMax_ReturnsMax()
        => Assert.AreEqual(75, _sut.ComputeInflictedDamage(MumakilAttackKind.SideAttack, targetBlocking: false, roll: 1f));

    [TestMethod]
    public void ComputeInflictedDamage_SideAttackBlockingMaxRoll_ScaledToQuarter()
        // round(75 * 0.25 = 18.75) = 19.
        => Assert.AreEqual(19, _sut.ComputeInflictedDamage(MumakilAttackKind.SideAttack, targetBlocking: true, roll: 1f));

    [TestMethod]
    public void ComputeInflictedDamage_RollNaN_TreatedAsMin()
        // Defensive: a NaN roll must not slip past the band (NaN comparisons are always false). Clamp → min.
        => Assert.AreEqual(50, _sut.ComputeInflictedDamage(MumakilAttackKind.Trample, targetBlocking: false, roll: float.NaN));

    [TestMethod]
    public void ComputeInflictedDamage_RollAboveOne_ClampedToMax()
        // Defensive: an out-of-range roll clamps to [0,1] → max, never above the band.
        => Assert.AreEqual(100, _sut.ComputeInflictedDamage(MumakilAttackKind.Trample, targetBlocking: false, roll: 5f));

    [TestMethod]
    public void ComputeInflictedDamage_RollBelowZero_ClampedToMin()
        => Assert.AreEqual(50, _sut.ComputeInflictedDamage(MumakilAttackKind.Trample, targetBlocking: false, roll: -3f));
}
