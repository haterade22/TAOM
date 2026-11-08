using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.FordsOfWar.Domain;

namespace TAOM.Tests.Features.FordsOfWar.Domain;

/// <summary>
/// Cavalry and horses take more from pierce blows (Mike, 2026-11-07): spears and pikes on foot, arrows, javelins and a
/// tower's arrows all count, so spearmen and bowmen are the answer to riders. Any other blow, or any victim on foot, is
/// unchanged.
/// </summary>
[TestClass]
public class MountedPierceRuleTests
{
    [TestMethod]
    public void APierceBlowOnAMountedVictim_IsMultiplied()
    {
        Assert.AreEqual(2f, MountedPierceRule.Factor(2f, pierce: true, victimMounted: true));
    }

    [TestMethod]
    public void APierceBlowOnAVictimOnFoot_IsUnchanged()
    {
        Assert.AreEqual(1f, MountedPierceRule.Factor(2f, pierce: true, victimMounted: false));
    }

    [TestMethod]
    public void ACutOrBluntBlowOnAMountedVictim_IsUnchanged()
    {
        Assert.AreEqual(1f, MountedPierceRule.Factor(2f, pierce: false, victimMounted: true));
    }

    [TestMethod]
    public void AFactorOfOne_TurnsTheRuleOff()
    {
        Assert.AreEqual(1f, MountedPierceRule.Factor(1f, pierce: true, victimMounted: true));
    }

    // A scripted blow (a warg or spider bite, a signature strike's splash) writes whole damage and skips the damage model.
    [TestMethod]
    public void AScriptedPierceBlowOnAMountedVictim_IsMultiplied()
    {
        Assert.AreEqual(60, MountedPierceRule.Damage(30, 2f, pierce: true, victimMounted: true));
    }

    [TestMethod]
    public void AScriptedBlowThatTheRuleSkips_KeepsItsDamage()
    {
        Assert.AreEqual(30, MountedPierceRule.Damage(30, 2f, pierce: false, victimMounted: true));
        Assert.AreEqual(30, MountedPierceRule.Damage(30, 2f, pierce: true, victimMounted: false));
    }

    [TestMethod]
    public void AScriptedBlow_RoundsHalfToEven_AsTheEngineDoes()
    {
        // TaleWorlds.Library.MathF.Round is (int)Math.Round(f): 37.5 goes up to 38, 62.5 down to 62.
        Assert.AreEqual(38, MountedPierceRule.Damage(15, 2.5f, pierce: true, victimMounted: true));
        Assert.AreEqual(62, MountedPierceRule.Damage(25, 2.5f, pierce: true, victimMounted: true));
    }
}
