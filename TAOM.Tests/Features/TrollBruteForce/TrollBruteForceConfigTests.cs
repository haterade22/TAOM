using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.TrollBruteForce;

// Brute Force tuning invariants (#649): the ring's shape, the impact inside the clip, and a cooldown longer than
// the smash so a troll can never chain two.

namespace TAOM.Tests.Features.TrollBruteForce;

[TestClass]
public class TrollBruteForceConfigTests
{
    /// <summary>The prototype clip, the Fab anim_troll_attack1 (troll_danger_attack_0): 73 frames at 30 fps.</summary>
    private const float SmashClipSeconds = 72f / 30f;

    [TestMethod]
    public void Ring_HasAPositiveInnerRadiusInsideTheOuter()
    {
        Assert.IsTrue(TrollBruteForceConfig.InnerRadius > 0f);
        Assert.IsTrue(TrollBruteForceConfig.InnerRadius < TrollBruteForceConfig.OuterRadius);
    }

    [TestMethod]
    public void ImpactCentre_LiesInsideTheRing_SoTheSmashCanReachWhatTriggeredIt()
    {
        // The trigger enemy stands at most TriggerRange ahead; the centre is ImpactForward ahead, so the enemy is
        // at most |TriggerRange - ImpactForward| from it along the look line and must be inside the outer radius.
        Assert.IsTrue(System.Math.Abs(TrollBruteForceConfig.TriggerRange - TrollBruteForceConfig.ImpactForward)
            < TrollBruteForceConfig.OuterRadius);
    }

    [TestMethod]
    public void ImpactFraction_IsInsideTheClip()
    {
        Assert.IsTrue(TrollBruteForceConfig.ImpactFraction > 0f && TrollBruteForceConfig.ImpactFraction < 1f);
    }

    [TestMethod]
    public void Cooldown_OutlastsTheSmash()
    {
        Assert.IsTrue(TrollBruteForceConfig.CooldownSeconds > SmashClipSeconds);
    }

    [TestMethod]
    public void ShieldBlock_TakesLessThanAFullBlow()
    {
        Assert.IsTrue(TrollBruteForceConfig.ShieldBlockedMultiplier > 0f && TrollBruteForceConfig.ShieldBlockedMultiplier < 1f);
    }

    [TestMethod]
    public void Blow_HasDamageAndMagnitude()
    {
        Assert.IsTrue(TrollBruteForceConfig.CentreDamage > 0);
        Assert.IsTrue(TrollBruteForceConfig.BlowMagnitude > 0f);
    }
}
