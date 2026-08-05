using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.CareerSystem.Abilities;
using TAOM.Features.CareerSystem.Domain;

namespace TAOM.Tests.Features.CareerSystem;

[TestClass]
public class CareerAbilityTests
{
    [TestMethod]
    public void AddCharge_CorrectType_Accumulates()
    {
        var ability = new CareerAbility("test", ChargeType.Kills, 100f, 0f);
        ability.AddCharge(10f, ChargeType.Kills);
        Assert.AreEqual(10f, ability.CurrentCharge, 0.001f);
    }

    [TestMethod]
    public void AddCharge_WrongType_DoesNotAccumulate()
    {
        var ability = new CareerAbility("test", ChargeType.Kills, 100f, 0f);
        ability.AddCharge(10f, ChargeType.DamageDone);
        Assert.AreEqual(0f, ability.CurrentCharge, 0.001f);
    }

    [TestMethod]
    public void AddCharge_CustomType_AcceptsAnySource()
    {
        var ability = new CareerAbility("test", ChargeType.Custom, 100f, 0f);
        ability.AddCharge(10f, ChargeType.DamageDone);
        ability.AddCharge(5f, ChargeType.Kills);
        Assert.AreEqual(15f, ability.CurrentCharge, 0.001f);
    }

    [TestMethod]
    public void AddCharge_CapsAtMax()
    {
        var ability = new CareerAbility("test", ChargeType.Kills, 50f, 0f);
        ability.AddCharge(100f, ChargeType.Kills);
        Assert.AreEqual(50f, ability.CurrentCharge, 0.001f);
    }

    [TestMethod]
    public void AddCharge_CooldownOnly_Ignored()
    {
        var ability = new CareerAbility("test", ChargeType.CooldownOnly, 0f, 10f);
        ability.AddCharge(10f, ChargeType.Kills);
        Assert.AreEqual(0f, ability.CurrentCharge, 0.001f);
    }

    [TestMethod]
    public void IsReady_ChargeType_TrueAtMaxCharge()
    {
        var ability = new CareerAbility("test", ChargeType.Kills, 10f, 0f);
        ability.AddCharge(10f, ChargeType.Kills);
        Assert.IsTrue(ability.IsReady);
    }

    [TestMethod]
    public void IsReady_ChargeType_FalseBelowMax()
    {
        var ability = new CareerAbility("test", ChargeType.Kills, 10f, 0f);
        ability.AddCharge(5f, ChargeType.Kills);
        Assert.IsFalse(ability.IsReady);
    }

    [TestMethod]
    public void IsReady_CooldownBased_TrueWhenNotOnCooldown()
    {
        var ability = new CareerAbility("test", ChargeType.CooldownOnly, 0f, 5f);
        Assert.IsTrue(ability.IsReady);
    }

    [TestMethod]
    public void IsReady_CooldownBased_FalseWhileCoolingDown()
    {
        var ability = new CareerAbility("test", ChargeType.CooldownOnly, 0f, 5f);
        ability.Activate();
        Assert.IsFalse(ability.IsReady);
    }

    [TestMethod]
    public void Activate_ChargeType_ResetsCharge()
    {
        var ability = new CareerAbility("test", ChargeType.Kills, 10f, 0f);
        ability.AddCharge(10f, ChargeType.Kills);
        ability.Activate();
        Assert.AreEqual(0f, ability.CurrentCharge, 0.001f);
    }

    [TestMethod]
    public void Activate_CooldownType_StartsCooldown()
    {
        var ability = new CareerAbility("test", ChargeType.CooldownOnly, 0f, 5f);
        ability.Activate();
        Assert.AreEqual(5f, ability.CooldownRemaining, 0.001f);
    }

    [TestMethod]
    public void Tick_ReducesCooldown()
    {
        var ability = new CareerAbility("test", ChargeType.CooldownOnly, 0f, 5f);
        ability.Activate();
        ability.Tick(3f);
        Assert.AreEqual(2f, ability.CooldownRemaining, 0.001f);
    }

    [TestMethod]
    public void Tick_CooldownDoesNotGoBelowZero()
    {
        var ability = new CareerAbility("test", ChargeType.CooldownOnly, 0f, 5f);
        ability.Activate();
        ability.Tick(10f);
        Assert.AreEqual(0f, ability.CooldownRemaining, 0.001f);
        Assert.IsTrue(ability.IsReady);
    }

    [TestMethod]
    public void SetMaxCharge_ClampsCurrentCharge()
    {
        var ability = new CareerAbility("test", ChargeType.Kills, 100f, 0f);
        ability.AddCharge(80f, ChargeType.Kills);
        ability.SetMaxCharge(50f);
        Assert.AreEqual(50f, ability.CurrentCharge, 0.001f);
    }

    [TestMethod]
    public void ReadyProgress01_CooldownOnly_StartsAtOne()
    {
        var ability = new CareerAbility("test", ChargeType.CooldownOnly, 0f, 30f);
        Assert.AreEqual(1f, ability.ReadyProgress01, 0.001f);
    }

    [TestMethod]
    public void ReadyProgress01_CooldownOnly_ZeroImmediatelyAfterActivate()
    {
        var ability = new CareerAbility("test", ChargeType.CooldownOnly, 0f, 30f);
        ability.Activate();
        Assert.AreEqual(0f, ability.ReadyProgress01, 0.001f);
    }

    [TestMethod]
    public void ReadyProgress01_CooldownOnly_HalfWayDuringCooldown()
    {
        var ability = new CareerAbility("test", ChargeType.CooldownOnly, 0f, 30f);
        ability.Activate();
        ability.Tick(15f);
        Assert.AreEqual(0.5f, ability.ReadyProgress01, 0.001f);
    }

    [TestMethod]
    public void ReadyProgress01_CooldownOnly_OneAfterFullCooldown()
    {
        var ability = new CareerAbility("test", ChargeType.CooldownOnly, 0f, 30f);
        ability.Activate();
        ability.Tick(30f);
        Assert.AreEqual(1f, ability.ReadyProgress01, 0.001f);
    }

    [TestMethod]
    public void ReadyProgress01_ChargeBased_TracksChargeRatio()
    {
        var ability = new CareerAbility("test", ChargeType.Kills, 10f, 0f);
        ability.AddCharge(4f, ChargeType.Kills);
        Assert.AreEqual(0.4f, ability.ReadyProgress01, 0.001f);
    }

    [TestMethod]
    public void ReadyProgress01_CooldownOnly_ZeroDuration_ReturnsOne()
    {
        var ability = new CareerAbility("test", ChargeType.CooldownOnly, 0f, 0f);
        Assert.AreEqual(1f, ability.ReadyProgress01, 0.001f);
    }

    // ── Issue #377 — active-window state (BeginActiveWindow / ActiveRemaining) ──
    // Before the fix the model was cooldown-only: the template's duration drove the buff
    // restore timers but never reached this state machine, so a HUD had to guess the
    // active window (the external reference module hardcoded 8s wall-clock and documents
    // the resulting drain oscillation).

    [TestMethod]
    public void BeginActiveWindow_SetsDurationAndRemaining()
    {
        var ability = new CareerAbility("test", ChargeType.CooldownOnly, 0f, 30f);

        ability.BeginActiveWindow(8f);

        Assert.AreEqual(8f, ability.ActiveDuration, 0.001f);
        Assert.AreEqual(8f, ability.ActiveRemaining, 0.001f);
        Assert.IsTrue(ability.IsActive);
        Assert.AreEqual(1f, ability.ActiveProgress01, 0.001f);
    }

    [TestMethod]
    public void IsActive_BeforeAnyActivation_False()
    {
        var ability = new CareerAbility("test", ChargeType.CooldownOnly, 0f, 30f);

        Assert.IsFalse(ability.IsActive);
        Assert.AreEqual(0f, ability.ActiveProgress01, 0.001f);
    }

    [TestMethod]
    public void Tick_DecrementsActiveRemaining()
    {
        var ability = new CareerAbility("test", ChargeType.CooldownOnly, 0f, 30f);
        ability.BeginActiveWindow(8f);

        ability.Tick(2f);

        Assert.AreEqual(6f, ability.ActiveRemaining, 0.001f);
        Assert.AreEqual(0.75f, ability.ActiveProgress01, 0.001f);
        Assert.IsTrue(ability.IsActive);
    }

    [TestMethod]
    public void Tick_ActiveWindowExpires_IsActiveFalseAndClampedAtZero()
    {
        var ability = new CareerAbility("test", ChargeType.CooldownOnly, 0f, 30f);
        ability.BeginActiveWindow(8f);

        ability.Tick(10f);

        Assert.AreEqual(0f, ability.ActiveRemaining, 0.001f);
        Assert.IsFalse(ability.IsActive);
        Assert.AreEqual(0f, ability.ActiveProgress01, 0.001f);
    }

    [TestMethod]
    public void Tick_ActiveWindowAndCooldownAdvanceTogether()
    {
        // The cooldown starts at Activate() and keeps running through the active window
        // (measured live: ReadyProgress01 0.13 at cast -> 0.40 eight seconds later). The
        // active window is independent state, not a pause on the cooldown.
        var ability = new CareerAbility("test", ChargeType.CooldownOnly, 0f, 30f);
        ability.Activate();
        ability.BeginActiveWindow(8f);

        ability.Tick(8f);

        Assert.AreEqual(0f, ability.ActiveRemaining, 0.001f);
        Assert.AreEqual(22f, ability.CooldownRemaining, 0.001f);
    }

    [TestMethod]
    public void BeginActiveWindow_ReActivation_RestartsWindow()
    {
        var ability = new CareerAbility("test", ChargeType.CooldownOnly, 0f, 30f);
        ability.BeginActiveWindow(8f);
        ability.Tick(5f);

        ability.BeginActiveWindow(10f);

        Assert.AreEqual(10f, ability.ActiveDuration, 0.001f);
        Assert.AreEqual(10f, ability.ActiveRemaining, 0.001f);
    }

    [TestMethod]
    public void BeginActiveWindow_NaN_Rejected()
    {
        // NaN-gate rule (csharp-architecture.md): a poisoned config float must not produce
        // a frozen "always active" state machine. Same guard family as AdjustCooldown.
        var ability = new CareerAbility("test", ChargeType.CooldownOnly, 0f, 30f);

        ability.BeginActiveWindow(float.NaN);

        Assert.IsFalse(ability.IsActive);
        Assert.AreEqual(0f, ability.ActiveRemaining, 0.001f);
    }

    [TestMethod]
    public void BeginActiveWindow_Infinity_Rejected()
    {
        var ability = new CareerAbility("test", ChargeType.CooldownOnly, 0f, 30f);

        ability.BeginActiveWindow(float.PositiveInfinity);

        Assert.IsFalse(ability.IsActive);
    }

    [TestMethod]
    public void BeginActiveWindow_ZeroOrNegative_Rejected()
    {
        var ability = new CareerAbility("test", ChargeType.CooldownOnly, 0f, 30f);

        ability.BeginActiveWindow(0f);
        Assert.IsFalse(ability.IsActive);

        ability.BeginActiveWindow(-3f);
        Assert.IsFalse(ability.IsActive);
    }
}
