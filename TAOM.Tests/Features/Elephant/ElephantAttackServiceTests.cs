using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.Elephant;

// War-elephant attack — pure decision-service tests. The service has no TaleWorlds dependencies, so every
// branch is reachable without mocks; the engine values (facing dot, current time, blocking) are supplied by
// the behavior-tree nodes in-game. The 2026-06-10 cooldown rework replaced ADOD's per-tick probability roll
// (ShouldAiTrample) with deterministic cooldowns (ShouldEngage + IsOffCooldown) — the BT decides cadence.

namespace TAOM.Tests.Features.Elephant;

[TestClass]
public class ElephantAttackServiceTests
{
    private ElephantAttackService _sut = null!;

    [TestInitialize]
    public void Setup() => _sut = new ElephantAttackService();

    // ------------------------------------------------------------------ IsElephantMonster

    [TestMethod]
    public void IsElephantMonster_ElephantId_ReturnsTrue()
        => Assert.IsTrue(_sut.IsElephantMonster(ElephantConfig.ElephantMonsterId));

    [TestMethod]
    public void IsElephantMonster_OtherId_ReturnsFalse()
        => Assert.IsFalse(_sut.IsElephantMonster("horse"));

    [TestMethod]
    public void IsElephantMonster_Null_ReturnsFalse()
        => Assert.IsFalse(_sut.IsElephantMonster(null));

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
        // Strict "> TrampleFacingDot" (ADOD uses `> 0.25f`), so exactly at the threshold does not engage.
        => Assert.IsFalse(_sut.ShouldEngage(facingDot: ElephantConfig.TrampleFacingDot, alreadyAttacking: false));

    [TestMethod]
    public void ShouldEngage_NoEnemyFoundSentinel_ReturnsFalse()
        // The BT scan reports -1 when no live enemy is within trigger range — must never engage.
        => Assert.IsFalse(_sut.ShouldEngage(facingDot: -1f, alreadyAttacking: false));

    // ------------------------------------------------------------------ IsOffCooldown

    private static readonly DateTime Now = new(2026, 6, 10, 12, 0, 0);

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

    [TestMethod]
    public void ComputeInflictedDamage_TargetNotBlocking_ReturnsTwentyDamage()
        // ADOD: round(10 * 1) * 2 = 20.
        => Assert.AreEqual(20, _sut.ComputeInflictedDamage(targetBlocking: false));

    [TestMethod]
    public void ComputeInflictedDamage_TargetBlocking_ReturnsReducedDamage()
        // ADOD: round(10 * 0.25 = 2.5) * 2. Math.Round(2.5) = 2 (banker's rounding) → 4.
        => Assert.AreEqual(4, _sut.ComputeInflictedDamage(targetBlocking: true));
}
