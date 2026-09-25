using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.AdvancedCombat;
using TaleWorlds.Library;

namespace TAOM.Tests.Features.AdvancedCombat;

/// <summary>
/// Tests for <see cref="CustomAttacksUtils.IsBlowGeometrySafe"/>, the pure synthetic-blow geometry
/// check extracted from <see cref="CustomAttacksUtils.TakeDamage"/>. Why the blow's floats are
/// refused before native code, and what the guard is and is not known to prevent, is on that
/// method.
/// </summary>
[TestClass]
[TestCategory("RequiresGame")]
public class CustomAttacksUtilsTests
{
    private static Vec3 FinitePos => new Vec3(10f, 20f, 1.7f);
    private static Vec3 FiniteDir => new Vec3(0f, 1f, 0f);

    [TestMethod]
    public void IsBlowGeometrySafe_AllFinite_ReturnsTrue()
    {
        Assert.IsTrue(CustomAttacksUtils.IsBlowGeometrySafe(FinitePos, FiniteDir, 50f));
    }

    [TestMethod]
    public void IsBlowGeometrySafe_ZeroMagnitude_ReturnsTrue()
    {
        // Zero is finite and non-negative — a 0-magnitude blow is geometrically valid.
        Assert.IsTrue(CustomAttacksUtils.IsBlowGeometrySafe(FinitePos, FiniteDir, 0f));
    }

    [TestMethod]
    public void IsBlowGeometrySafe_NaNPositionX_ReturnsFalse()
    {
        var pos = new Vec3(float.NaN, 20f, 1.7f);
        Assert.IsFalse(CustomAttacksUtils.IsBlowGeometrySafe(pos, FiniteDir, 50f));
    }

    [TestMethod]
    public void IsBlowGeometrySafe_NaNPositionY_ReturnsFalse()
    {
        var pos = new Vec3(10f, float.NaN, 1.7f);
        Assert.IsFalse(CustomAttacksUtils.IsBlowGeometrySafe(pos, FiniteDir, 50f));
    }

    [TestMethod]
    public void IsBlowGeometrySafe_InfinityPositionZ_ReturnsFalse()
    {
        var pos = new Vec3(10f, 20f, float.PositiveInfinity);
        Assert.IsFalse(CustomAttacksUtils.IsBlowGeometrySafe(pos, FiniteDir, 50f));
    }

    [TestMethod]
    public void IsBlowGeometrySafe_NaNSwingDirection_ReturnsFalse()
    {
        // A non-finite direction is refused whatever produced it: Vec3.Normalize() turns a vector
        // with an infinite component into NaN (a near-zero one becomes (0, 1, 0)).
        var dir = new Vec3(float.NaN, float.NaN, float.NaN);
        Assert.IsFalse(CustomAttacksUtils.IsBlowGeometrySafe(FinitePos, dir, 50f));
    }

    [TestMethod]
    public void IsBlowGeometrySafe_InfinitySwingDirection_ReturnsFalse()
    {
        var dir = new Vec3(0f, float.NegativeInfinity, 0f);
        Assert.IsFalse(CustomAttacksUtils.IsBlowGeometrySafe(FinitePos, dir, 50f));
    }

    [TestMethod]
    public void IsBlowGeometrySafe_NaNMagnitude_ReturnsFalse()
    {
        Assert.IsFalse(CustomAttacksUtils.IsBlowGeometrySafe(FinitePos, FiniteDir, float.NaN));
    }

    [TestMethod]
    public void IsBlowGeometrySafe_InfinityMagnitude_ReturnsFalse()
    {
        Assert.IsFalse(CustomAttacksUtils.IsBlowGeometrySafe(FinitePos, FiniteDir, float.PositiveInfinity));
    }

    [TestMethod]
    public void IsBlowGeometrySafe_NegativeMagnitude_ReturnsFalse()
    {
        // BaseMagnitude is a physical intensity; negative is nonsensical and must be rejected.
        Assert.IsFalse(CustomAttacksUtils.IsBlowGeometrySafe(FinitePos, FiniteDir, -1f));
    }
}
