using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.HeroAbilities.Ranged;

namespace TAOM.Tests.Features.HeroAbilities;

[TestClass]
public class HeroRangedServiceTests
{
    private static float Chance(bool head = false, bool extra = false, bool shield = false, bool weapon = false,
        float inflicted = 40f, float headChance = 0.9f, float bodyChance = 0.1f, bool alive = true) =>
        HeroRangedService.ArrowKillChance(head, extra, shield, weapon, inflicted, headChance, bodyChance, alive);

    [TestMethod]
    public void Headshot_IsHeadshotChance() => Assert.AreEqual(0.9f, Chance(head: true));

    [TestMethod]
    public void Body_IsBodyChance() => Assert.AreEqual(0.1f, Chance());

    [TestMethod]
    public void Extra_Zero() => Assert.AreEqual(0f, Chance(head: true, extra: true));

    [TestMethod]
    public void BlockedShield_Zero() => Assert.AreEqual(0f, Chance(head: true, shield: true));

    [TestMethod]
    public void BlockedWeapon_Zero() => Assert.AreEqual(0f, Chance(head: true, weapon: true));

    // The arrow's own blow may already have killed him (Agent.cs:5513, :5529-5532); the executor rolls no corpse, so no call.
    [TestMethod]
    public void ArrowKillChance_DeadVictim_Zero() => Assert.AreEqual(0f, Chance(head: true, alive: false));

    [DataTestMethod]
    [DataRow(0f)]
    [DataRow(-5f)]
    [DataRow(float.NaN)]
    public void NoDamage_Zero(float inflicted) => Assert.AreEqual(0f, Chance(head: true, inflicted: inflicted));

    [DataTestMethod]
    [DataRow(float.NaN)]
    [DataRow(float.PositiveInfinity)]
    [DataRow(-0.5f)]
    public void NaNChance_Zero(float bad)
    {
        Assert.AreEqual(0f, Chance(head: true, headChance: bad));
        Assert.AreEqual(0f, Chance(bodyChance: bad));
    }

    [TestMethod]
    public void ChanceAboveOne_ClampedToOne() => Assert.AreEqual(1f, Chance(head: true, headChance: 3f));
}
