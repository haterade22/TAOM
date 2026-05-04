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
}
