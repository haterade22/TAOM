using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.BattleHud.UI;
using TAOM.Features.CareerSystem.Abilities;
using TAOM.Features.CareerSystem.Domain;
using TAOM.Features.CareerSystem.UI;

namespace TAOM.Tests.Features.BattleHud;

// Issue #1095: pure mapping for the circular battle HUD. Health is always a channel; the horse and
// shield channels exist only while the equipment does, and present channels pack inward so a
// missing one leaves no empty ring. The centre disk reuses CareerEnergyBarStateMapper's semantics.
[TestClass]
public class BattleHudStateMapperTests
{
    private static BattleHudInput Input(
        float health = 80f, float healthMax = 100f,
        bool hasHorse = false, float horse = 0f, float horseMax = 0f,
        bool hasShield = false, float shield = 0f, float shieldMax = 0f)
        => new BattleHudInput(health, healthMax, hasHorse, horse, horseMax, hasShield, shield, shieldMax);

    private static CareerAbility NewAbility(float cooldown = 30f)
        => new CareerAbility("test", ChargeType.CooldownOnly, 0f, cooldown);

    [TestMethod]
    public void Map_HealthOnly_HealthInSlotZeroAndOneRing()
    {
        var s = BattleHudStateMapper.Map(Input(), null, "");

        Assert.IsTrue(s.Health.IsVisible);
        Assert.AreEqual(0, s.Health.Slot);
        Assert.AreEqual(0.8f, s.Health.Fill01, 0.001f);
        Assert.IsFalse(s.Horse.IsVisible);
        Assert.IsFalse(s.Shield.IsVisible);
        Assert.AreEqual(1, s.RingCount);
    }

    [TestMethod]
    public void Map_HorseOnly_HorseTakesSlotOne()
    {
        var s = BattleHudStateMapper.Map(Input(hasHorse: true, horse: 150f, horseMax: 300f), null, "");

        Assert.IsTrue(s.Horse.IsVisible);
        Assert.AreEqual(1, s.Horse.Slot);
        Assert.AreEqual(0.5f, s.Horse.Fill01, 0.001f);
        Assert.IsFalse(s.Shield.IsVisible);
        Assert.AreEqual(-1, s.Shield.Slot);
        Assert.AreEqual(2, s.RingCount);
    }

    [TestMethod]
    public void Map_ShieldOnly_ShieldPacksIntoSlotOne()
    {
        var s = BattleHudStateMapper.Map(Input(hasShield: true, shield: 30f, shieldMax: 120f), null, "");

        Assert.IsFalse(s.Horse.IsVisible);
        Assert.AreEqual(-1, s.Horse.Slot);
        Assert.IsTrue(s.Shield.IsVisible);
        Assert.AreEqual(1, s.Shield.Slot);
        Assert.AreEqual(0.25f, s.Shield.Fill01, 0.001f);
        Assert.AreEqual(2, s.RingCount);
    }

    [TestMethod]
    public void Map_HorseAndShield_ShieldOutsideHorse()
    {
        var s = BattleHudStateMapper.Map(
            Input(hasHorse: true, horse: 300f, horseMax: 300f, hasShield: true, shield: 120f, shieldMax: 120f),
            null, "");

        Assert.AreEqual(0, s.Health.Slot);
        Assert.AreEqual(1, s.Horse.Slot);
        Assert.AreEqual(2, s.Shield.Slot);
        Assert.AreEqual(3, s.RingCount);
    }

    [TestMethod]
    public void Map_HasHorseButZeroMax_ChannelAbsent()
    {
        var s = BattleHudStateMapper.Map(Input(hasHorse: true, horse: 0f, horseMax: 0f), null, "");

        Assert.IsFalse(s.Horse.IsVisible);
        Assert.AreEqual(1, s.RingCount);
    }

    [TestMethod]
    public void Map_HasShieldButZeroMax_ChannelAbsent()
    {
        var s = BattleHudStateMapper.Map(Input(hasShield: true, shield: 10f, shieldMax: 0f), null, "");

        Assert.IsFalse(s.Shield.IsVisible);
        Assert.AreEqual(1, s.RingCount);
    }

    [TestMethod]
    public void Map_HealthMaxZero_HealthVisibleAndEmpty()
    {
        var s = BattleHudStateMapper.Map(Input(health: 50f, healthMax: 0f), null, "");

        Assert.IsTrue(s.Health.IsVisible);
        Assert.AreEqual(0f, s.Health.Fill01, 0.001f);
    }

    [TestMethod]
    public void Map_NaNHealth_FillsZero()
    {
        var s = BattleHudStateMapper.Map(Input(health: float.NaN), null, "");

        Assert.AreEqual(0f, s.Health.Fill01, 0.001f);
    }

    [TestMethod]
    public void Map_NaNHorseMax_ChannelAbsent()
    {
        var s = BattleHudStateMapper.Map(Input(hasHorse: true, horse: 10f, horseMax: float.NaN), null, "");

        Assert.IsFalse(s.Horse.IsVisible);
    }

    [TestMethod]
    public void Map_OverMaxHealth_ClampsToOne()
    {
        var s = BattleHudStateMapper.Map(Input(health: 140f, healthMax: 100f), null, "");

        Assert.AreEqual(1f, s.Health.Fill01, 0.001f);
    }

    [TestMethod]
    public void Map_NoAbility_NeutralCentreAndNoKey()
    {
        var s = BattleHudStateMapper.Map(Input(), null, "V");

        Assert.IsFalse(s.Centre.HasAbility);
        Assert.IsFalse(s.Centre.ShowKey);
        Assert.IsFalse(s.Centre.IsReady);
        Assert.IsFalse(s.Centre.IsActive);
        Assert.IsFalse(s.Centre.IsCooldown);
        Assert.AreEqual(0f, s.Centre.Fill01, 0.001f);
    }

    [TestMethod]
    public void Map_ReadyAbility_FullCentreAndKeyShown()
    {
        var s = BattleHudStateMapper.Map(Input(), NewAbility(), "G");

        Assert.IsTrue(s.Centre.HasAbility);
        Assert.IsTrue(s.Centre.IsReady);
        Assert.AreEqual(1f, s.Centre.Fill01, 0.001f);
        Assert.IsTrue(s.Centre.ShowKey);
    }

    [TestMethod]
    public void Map_AbilityWithUnboundKey_KeyHidden()
    {
        var s = BattleHudStateMapper.Map(Input(), NewAbility(), "");

        Assert.IsTrue(s.Centre.HasAbility);
        Assert.IsFalse(s.Centre.ShowKey);
    }

    [TestMethod]
    public void Map_AbilityWithNullKey_KeyHidden()
    {
        var s = BattleHudStateMapper.Map(Input(), NewAbility(), null);

        Assert.IsFalse(s.Centre.ShowKey);
    }

    [TestMethod]
    public void MapCentre_MatchesEnergyBarSemantics()
    {
        var bar = new CareerEnergyBarState { IsCooldown = true, Fill01 = 0.5f };

        var c = BattleHudStateMapper.MapCentre(true, bar, "V");

        Assert.IsTrue(c.IsCooldown);
        Assert.IsFalse(c.IsReady);
        Assert.IsFalse(c.IsActive);
        Assert.AreEqual(0.5f, c.Fill01, 0.001f);
    }

    [TestMethod]
    public void MapCentre_NoAbility_IgnoresBarState()
    {
        var bar = new CareerEnergyBarState { IsActive = true, Fill01 = 0.7f };

        var c = BattleHudStateMapper.MapCentre(false, bar, "V");

        Assert.IsFalse(c.IsActive);
        Assert.AreEqual(0f, c.Fill01, 0.001f);
        Assert.IsFalse(c.ShowKey);
    }
}
