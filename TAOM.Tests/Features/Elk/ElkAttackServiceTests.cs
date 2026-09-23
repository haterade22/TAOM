using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.Elk;
using TAOM.Features.ElephantLike;

// Great elk antler charge: pure decision-service tests (#636). The shared elephant-like decision surface,
// no TaleWorlds dependencies, every branch reachable without mocks. The antler charge is one 60 Blunt blow (Mike,
// 2026-09-23), scaled by the rider's career charge bonus when the behavior tree passes one in. The gate and cooldown
// tests mirror WarRamAttackServiceTests.

namespace TAOM.Tests.Features.Elk;

[TestClass]
public class ElkAttackServiceTests
{
    private ElkAttackService _sut = null!;

    [TestInitialize]
    public void Setup() => _sut = new ElkAttackService();

    // ------------------------------------------------------------------ IsCreatureMonster

    [TestMethod]
    public void IsCreatureMonster_ElkId_ReturnsTrue()
        => Assert.IsTrue(_sut.IsCreatureMonster(ElkConfig.ElkMonsterId));

    [TestMethod]
    public void IsCreatureMonster_WarRamId_ReturnsFalse()
        // The elk shares the ram's action set, never its Monster: a ram must not get an elk tree.
        => Assert.IsFalse(_sut.IsCreatureMonster("taom_war_ram"));

    [TestMethod]
    public void IsCreatureMonster_VanillaHorseId_ReturnsFalse()
        => Assert.IsFalse(_sut.IsCreatureMonster("horse"));

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
        // Strict "> AttackFacingDot", so exactly at the threshold does not engage.
        => Assert.IsFalse(_sut.ShouldEngage(facingDot: ElkConfig.AttackFacingDot, alreadyAttacking: false));

    [TestMethod]
    public void ShouldEngage_NoEnemyFoundSentinel_ReturnsFalse()
        // The BT scan reports -1 when no live enemy is within trigger range: never engage.
        => Assert.IsFalse(_sut.ShouldEngage(facingDot: -1f, alreadyAttacking: false));

    [TestMethod]
    public void ShouldEngage_FacingDotNaN_ReturnsFalse()
        // A NaN facing dot comes from corrupt native look/position state; "NaN > threshold" is false.
        => Assert.IsFalse(_sut.ShouldEngage(facingDot: float.NaN, alreadyAttacking: false));

    // ------------------------------------------------------------------ IsOffCooldown

    private static readonly DateTime Now = new(2026, 9, 22, 22, 0, 0);

    [TestMethod]
    public void IsOffCooldown_NeverFired_ReturnsTrue()
        => Assert.IsTrue(_sut.IsOffCooldown(lastFired: null, now: Now, cooldownSeconds: ElkConfig.AttackCooldownSeconds));

    [TestMethod]
    public void IsOffCooldown_JustFired_ReturnsFalse()
        => Assert.IsFalse(_sut.IsOffCooldown(lastFired: Now, now: Now, cooldownSeconds: ElkConfig.AttackCooldownSeconds));

    [TestMethod]
    public void IsOffCooldown_PartiallyElapsed_ReturnsFalse()
        => Assert.IsFalse(_sut.IsOffCooldown(lastFired: Now.AddSeconds(-5), now: Now, cooldownSeconds: ElkConfig.AttackCooldownSeconds));

    [TestMethod]
    public void IsOffCooldown_ExactlyAtCooldown_ReturnsTrue()
        // Inclusive ">= cooldown": the charge becomes available the moment the window closes.
        => Assert.IsTrue(_sut.IsOffCooldown(lastFired: Now.AddSeconds(-ElkConfig.AttackCooldownSeconds), now: Now, cooldownSeconds: ElkConfig.AttackCooldownSeconds));

    [TestMethod]
    public void IsOffCooldown_FullyElapsed_ReturnsTrue()
        => Assert.IsTrue(_sut.IsOffCooldown(lastFired: Now.AddSeconds(-60), now: Now, cooldownSeconds: ElkConfig.AttackCooldownSeconds));

    [TestMethod]
    public void IsOffCooldown_LastFiredInFuture_ReturnsFalse()
        // Clock skew or a bad stamp: a future lastFired reads as ON cooldown, not off.
        => Assert.IsFalse(_sut.IsOffCooldown(lastFired: Now.AddSeconds(30), now: Now, cooldownSeconds: ElkConfig.AttackCooldownSeconds));

    // ------------------------------------------------------------------ ComputeInflictedDamage
    // Mike, 2026-09-23: the antler charge is one 60 Blunt blow. Armour does not reduce it (CustomAttacksUtils writes
    // InflictedDamage directly), a SHIELD block quarters it, and the rider's career charge bonus scales it ("scale the
    // antler blows too"): the behavior tree passes that bonus in as riderMultiplier.

    [TestMethod]
    public void ComputeInflictedDamage_Unblocked_Is60WhateverTheRoll()
    {
        Assert.AreEqual(60, _sut.ComputeInflictedDamage(ElephantLikeAttackKind.Trample, targetBlocking: false, roll: 0f));
        Assert.AreEqual(60, _sut.ComputeInflictedDamage(ElephantLikeAttackKind.Trample, targetBlocking: false, roll: 1f));
    }

    [TestMethod]
    public void ComputeInflictedDamage_ShieldBlocked_IsAQuarter()
        => Assert.AreEqual(15, _sut.ComputeInflictedDamage(ElephantLikeAttackKind.Trample, targetBlocking: true, roll: 1f));

    [TestMethod]
    public void ComputeInflictedDamage_SideAttackSlot_IsTheSameCharge()
        // No side attack is wired; the band mirrors the charge so a stray call cannot misreport the elk.
        => Assert.AreEqual(60, _sut.ComputeInflictedDamage(ElephantLikeAttackKind.SideAttack, targetBlocking: false, roll: 0.5f));

    [TestMethod]
    public void ComputeInflictedDamage_RollNaN_StaysInTheBand()
        // A NaN roll must not slip past the band (NaN comparisons are always false).
        => Assert.AreEqual(60, _sut.ComputeInflictedDamage(ElephantLikeAttackKind.Trample, targetBlocking: false, roll: float.NaN));

    [TestMethod]
    public void ComputeInflictedDamage_RiderChargeBonus_ScalesTheCharge()
        // Antler Crash's +25% charge damage: 60 x 1.25.
        => Assert.AreEqual(75, _sut.ComputeInflictedDamage(ElephantLikeAttackKind.Trample, targetBlocking: false, roll: 0f, riderMultiplier: 1.25f));

    [TestMethod]
    public void ComputeInflictedDamage_RiderChargeBonusOnAShieldBlock_ScalesTheQuarter()
        // round(60 x 0.25 x 1.25) = round(18.75) = 19.
        => Assert.AreEqual(19, _sut.ComputeInflictedDamage(ElephantLikeAttackKind.Trample, targetBlocking: true, roll: 0f, riderMultiplier: 1.25f));

    [TestMethod]
    public void ComputeInflictedDamage_RiderChargeMalus_ScalesDown()
        // A malus is a legal career value (CareerAgentStatServiceTests): 60 x 0.5.
        => Assert.AreEqual(30, _sut.ComputeInflictedDamage(ElephantLikeAttackKind.Trample, targetBlocking: false, roll: 0f, riderMultiplier: 0.5f));

    [TestMethod]
    public void ComputeInflictedDamage_RiderMultiplierAtTheCap_IsApplied()
        => Assert.AreEqual(600, _sut.ComputeInflictedDamage(ElephantLikeAttackKind.Trample, targetBlocking: false, roll: 0f,
            riderMultiplier: ElephantLikeAttackService.MaxRiderMultiplier));

    [TestMethod]
    [DataRow(float.NaN)]
    [DataRow(float.PositiveInfinity)]
    [DataRow(float.NegativeInfinity)]
    [DataRow(0f)]
    [DataRow(-1f)]
    [DataRow(1e9f)]
    public void ComputeInflictedDamage_RiderMultiplierOutsideItsRange_CountsAsOne(float riderMultiplier)
        // The multiplier is career data multiplied at runtime. A NaN, a non-positive product or one past the cap is
        // garbage: it must neither zero the charge nor overflow the int cast (int.MinValue), so the charge lands unscaled.
        => Assert.AreEqual(60, _sut.ComputeInflictedDamage(ElephantLikeAttackKind.Trample, targetBlocking: false, roll: 0f, riderMultiplier: riderMultiplier));
}
