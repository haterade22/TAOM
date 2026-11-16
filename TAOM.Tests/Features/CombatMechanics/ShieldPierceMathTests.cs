using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.CombatMechanics;

namespace TAOM.Tests.Features.CombatMechanics;

// The engine lets a shield-blocked missile through when its damage exceeds offset + factor * armour (30 and 3 in
// managed_core_parameters.xml:232-236), so the floor is the next whole number above that threshold.
[TestClass]
public class ShieldPierceMathTests
{
    [TestMethod]
    public void Floor_Armour15_Raises_To76() => Assert.AreEqual(76f, ShieldPierceMath.Floor(20f, 30f, 3f, 15f));

    [TestMethod]
    public void Floor_AboveThreshold_Keeps() => Assert.AreEqual(120f, ShieldPierceMath.Floor(120f, 30f, 3f, 15f));

    [DataTestMethod]
    [DataRow(-1f)]
    [DataRow(float.NaN)]
    [DataRow(float.PositiveInfinity)]
    public void Floor_NaNOrNegativeArmour_Keeps(float armour) => Assert.AreEqual(20f, ShieldPierceMath.Floor(20f, 30f, 3f, armour));

    [TestMethod]
    public void Floor_NaNDamage_Keeps() => Assert.IsTrue(float.IsNaN(ShieldPierceMath.Floor(float.NaN, 30f, 3f, 15f)));

    [TestMethod]
    public void Floor_NonFiniteParameters_Keeps()
    {
        Assert.AreEqual(20f, ShieldPierceMath.Floor(20f, float.NaN, 3f, 15f));
        Assert.AreEqual(20f, ShieldPierceMath.Floor(20f, 30f, float.NaN, 15f));
    }

    [TestMethod]
    public void Floor_FractionalThreshold_CeilPlusOne() => Assert.AreEqual(32f, ShieldPierceMath.Floor(1f, 30.5f, 3f, 0f));
}
