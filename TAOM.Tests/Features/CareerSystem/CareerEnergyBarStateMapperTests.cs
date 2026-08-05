using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.CareerSystem.Abilities;
using TAOM.Features.CareerSystem.Domain;
using TAOM.Features.CareerSystem.UI;

namespace TAOM.Tests.Features.CareerSystem;

// Issue #382 — pure mapping from CareerAbility state to the energy bar's visual state.
// The refill rescale: the cooldown starts at Activate() and keeps draining through the
// active window, so at drain-end ReadyProgress01 is already ActiveDuration/CooldownDuration
// (measured live: 0.13 at cast → 0.40 eight seconds later). Handing the bar the raw
// progress at that moment snaps it from empty to ~40%. Because both durations are known,
// the rescale origin is derivable — no captured state.
[TestClass]
public class CareerEnergyBarStateMapperTests
{
    private static CareerAbility NewAbility(float cooldown = 30f)
        => new CareerAbility("test", ChargeType.CooldownOnly, 0f, cooldown);

    [TestMethod]
    public void Map_NullAbility_AllStatesOff()
    {
        var s = CareerEnergyBarStateMapper.Map(null);

        Assert.IsFalse(s.IsReady);
        Assert.IsFalse(s.IsActive);
        Assert.IsFalse(s.IsCooldown);
        Assert.AreEqual(0f, s.Fill01, 0.001f);
    }

    [TestMethod]
    public void Map_Ready_FullBar()
    {
        var ability = NewAbility();

        var s = CareerEnergyBarStateMapper.Map(ability);

        Assert.IsTrue(s.IsReady);
        Assert.IsFalse(s.IsActive);
        Assert.IsFalse(s.IsCooldown);
        Assert.AreEqual(1f, s.Fill01, 0.001f);
    }

    [TestMethod]
    public void Map_Active_DrainsWithActiveProgress()
    {
        var ability = NewAbility();
        ability.Activate();
        ability.BeginActiveWindow(8f);
        ability.Tick(2f); // 6s of 8s remaining

        var s = CareerEnergyBarStateMapper.Map(ability);

        Assert.IsTrue(s.IsActive);
        Assert.IsFalse(s.IsReady);
        Assert.IsFalse(s.IsCooldown);
        Assert.AreEqual(0.75f, s.Fill01, 0.001f);
    }

    [TestMethod]
    public void Map_CooldownRightAfterActiveEnd_StartsAtZero_NoSnap()
    {
        var ability = NewAbility(cooldown: 30f);
        ability.Activate();
        ability.BeginActiveWindow(8f);
        ability.Tick(8f); // active just ended; raw ReadyProgress01 = 8/30 ≈ 0.267

        var s = CareerEnergyBarStateMapper.Map(ability);

        Assert.IsTrue(s.IsCooldown);
        Assert.AreEqual(0f, s.Fill01, 0.01f, "refill must start empty, not snap to raw progress");
    }

    [TestMethod]
    public void Map_MidCooldown_RescaledProportionally()
    {
        var ability = NewAbility(cooldown: 30f);
        ability.Activate();
        ability.BeginActiveWindow(8f);
        ability.Tick(19f); // raw = 19/30; rescaled = (19/30 - 8/30) / (1 - 8/30) = 11/22 = 0.5

        var s = CareerEnergyBarStateMapper.Map(ability);

        Assert.IsTrue(s.IsCooldown);
        Assert.AreEqual(0.5f, s.Fill01, 0.01f);
    }

    [TestMethod]
    public void Map_CooldownWithoutActiveWindow_UsesRawProgress()
    {
        // Ability activated but BeginActiveWindow never called (effect threw): no window
        // info exists, so the bar falls back to the raw cooldown progress.
        var ability = NewAbility(cooldown: 30f);
        ability.Activate();
        ability.Tick(15f);

        var s = CareerEnergyBarStateMapper.Map(ability);

        Assert.IsTrue(s.IsCooldown);
        Assert.AreEqual(0.5f, s.Fill01, 0.001f);
    }

    [TestMethod]
    public void Map_CooldownComplete_BecomesReadyFull()
    {
        var ability = NewAbility(cooldown: 30f);
        ability.Activate();
        ability.BeginActiveWindow(8f);
        ability.Tick(30f);

        var s = CareerEnergyBarStateMapper.Map(ability);

        Assert.IsTrue(s.IsReady);
        Assert.AreEqual(1f, s.Fill01, 0.001f);
    }

    [TestMethod]
    public void Map_ActiveWindowLongerThanCooldown_ClampsSafely()
    {
        // Degenerate config (duration >= cooldown): rescale denominator would be <= 0 —
        // fall back to raw progress rather than dividing by zero.
        var ability = NewAbility(cooldown: 8f);
        ability.Activate();
        ability.BeginActiveWindow(10f);
        ability.Tick(10f); // cooldown already finished during the window → ready

        var s = CareerEnergyBarStateMapper.Map(ability);

        Assert.IsTrue(s.IsReady);
        Assert.AreEqual(1f, s.Fill01, 0.001f);
    }
}
