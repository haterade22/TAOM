using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.CareerSystem.Domain;
using TAOM.Features.CareerSystem.UI;

namespace TAOM.Tests.Features.CareerSystem;

// Issue #388 — presentation map behind the diamond career screen: which banner icon a choice
// shows, and how a passive's magnitude reads in the Active Effects panel.
//
// The percent-vs-flat split is by effect TYPE, not by magnitude size. The reference
// implementation used "|value| < 1 means percent", which renders a 1.0 magnitude (=100%) as
// "+1" and would render a +1 companion limit as "+100%". TAOM's own data settles it: Damage /
// Ammo / MovementSpeed are authored as fractions (0.05, 0.10), Health / PartySize as counts
// (25, 50, 2, 4).
[TestClass]
public class CareerEffectDisplayMapTests
{
    [TestMethod]
    public void IconFor_KnownEffect_ReturnsBannerIconId()
    {
        Assert.AreEqual("30012", CareerEffectDisplayMap.IconFor(PassiveEffectType.Damage));
        Assert.AreEqual("10001", CareerEffectDisplayMap.IconFor(PassiveEffectType.Health));
        Assert.AreEqual("26109", CareerEffectDisplayMap.IconFor(PassiveEffectType.StealthBonus));
    }

    [TestMethod]
    public void IconFor_EveryEnumValue_HasAnIcon()
    {
        // A missing row renders a blank diamond with no error — pin the whole enum.
        foreach (PassiveEffectType type in System.Enum.GetValues(typeof(PassiveEffectType)))
        {
            var icon = CareerEffectDisplayMap.IconFor(type);
            Assert.IsFalse(string.IsNullOrEmpty(icon), $"{type} has no diamond icon mapped");
        }
    }

    [TestMethod]
    public void LabelFor_EveryEnumValue_HasALabel()
    {
        foreach (PassiveEffectType type in System.Enum.GetValues(typeof(PassiveEffectType)))
        {
            var label = CareerEffectDisplayMap.LabelFor(type);
            Assert.IsFalse(string.IsNullOrEmpty(label), $"{type} has no display label");
        }
    }

    [TestMethod]
    public void Format_FractionalEffect_RendersPercent()
    {
        Assert.AreEqual("+20% damage", CareerEffectDisplayMap.Format(PassiveEffectType.Damage, 0.20f));
        Assert.AreEqual("+7% movement speed", CareerEffectDisplayMap.Format(PassiveEffectType.MovementSpeed, 0.07f));
    }

    [TestMethod]
    public void Format_FlatEffect_RendersCount()
    {
        Assert.AreEqual("+25 max health", CareerEffectDisplayMap.Format(PassiveEffectType.Health, 25f));
        Assert.AreEqual("+4 party size", CareerEffectDisplayMap.Format(PassiveEffectType.PartySize, 4f));
    }

    [TestMethod]
    public void Format_FullMagnitudePercent_RendersHundredPercent_NotOne()
    {
        // The reference formatter's bug: |v| < 1 => percent, so exactly 1.0 fell through to
        // the flat branch and printed "+1 damage" for what is a doubling.
        Assert.AreEqual("+100% damage", CareerEffectDisplayMap.Format(PassiveEffectType.Damage, 1f));
    }

    [TestMethod]
    public void Format_NegativeMagnitude_KeepsSign()
    {
        Assert.AreEqual("-15% troop wages", CareerEffectDisplayMap.Format(PassiveEffectType.TroopWages, -0.15f));
    }

    [TestMethod]
    public void Format_RoundsToNearestWholeNumber()
    {
        Assert.AreEqual("+8% damage", CareerEffectDisplayMap.Format(PassiveEffectType.Damage, 0.075f));
    }

    [TestMethod]
    public void Format_NonFiniteMagnitude_ReturnsEmpty()
    {
        // Config floats are NaN-guarded at load, but the panel must never print "+NaN%".
        Assert.AreEqual("", CareerEffectDisplayMap.Format(PassiveEffectType.Damage, float.NaN));
        Assert.AreEqual("", CareerEffectDisplayMap.Format(PassiveEffectType.Damage, float.PositiveInfinity));
    }

    [TestMethod]
    public void Format_ZeroMagnitude_ReturnsEmpty()
    {
        Assert.AreEqual("", CareerEffectDisplayMap.Format(PassiveEffectType.Damage, 0f));
    }
}
