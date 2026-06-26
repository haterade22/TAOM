using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.CareerSystem;

namespace TAOM.Tests.Features.CareerSystem;

[TestClass]
public class CareerPassiveMathTests
{
    // ── BoostAmmo (the Ammo passive arithmetic, extracted from OnAgentBuild) ──

    [TestMethod]
    public void BoostAmmo_PositiveBonus_RoundsMultiplicativeIncrease()
    {
        // 30 * (1 + 0.10) = 33
        Assert.AreEqual(33, CareerPassiveMath.BoostAmmo(modifiedMaxAmount: 30, currentAmount: 30, bonus: 0.10f));
    }

    [TestMethod]
    public void BoostAmmo_ZeroBonus_ReturnsCurrentUnchanged()
    {
        Assert.AreEqual(30, CareerPassiveMath.BoostAmmo(30, 30, 0f));
    }

    [TestMethod]
    public void BoostAmmo_NegativeBonus_ReturnsCurrentUnchanged()
    {
        Assert.AreEqual(30, CareerPassiveMath.BoostAmmo(30, 30, -0.5f));
    }

    [TestMethod]
    public void BoostAmmo_BoostBelowCurrent_DoesNotShrink()
    {
        // 20 * 1.10 = 22, but current is already 40 → never shrink below current.
        Assert.AreEqual(40, CareerPassiveMath.BoostAmmo(modifiedMaxAmount: 20, currentAmount: 40, bonus: 0.10f));
    }

    [TestMethod]
    public void BoostAmmo_ExceedsShortMax_ClampsToShortMaxValue()
    {
        // 30000 * 1.5 = 45000 > short.MaxValue → clamp to 32767 (engine stores ammo as short).
        Assert.AreEqual(short.MaxValue, CareerPassiveMath.BoostAmmo(modifiedMaxAmount: 30000, currentAmount: 30000, bonus: 0.50f));
    }

    // ── ApplyStealthRatio (the StealthBonus inverse-direction math, extracted from the map model) ──

    [TestMethod]
    public void ApplyStealthRatio_PositiveBonus_LowersRatio()
    {
        // 1.0 * (1 - 0.20) = 0.80 — lower ratio = party harder to detect.
        Assert.AreEqual(0.80f, CareerPassiveMath.ApplyStealthRatio(1.0f, 0.20f), 0.001f);
    }

    [TestMethod]
    public void ApplyStealthRatio_ZeroBonus_ReturnsBaseUnchanged()
    {
        Assert.AreEqual(0.73f, CareerPassiveMath.ApplyStealthRatio(0.73f, 0f), 0.001f);
    }
}
