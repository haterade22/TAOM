using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.AdvancedCombat;
using TaleWorlds.Library;

namespace TAOM.Tests.Features.AdvancedCombat;

/// <summary>
/// Tests for the pure synthetic-blow geometry validation extracted from
/// <see cref="CustomAttacksUtils.TakeDamage"/>. This guard exists because the only
/// TAOM-supplied data that reaches native code unguarded is the blow's float geometry
/// (GlobalPosition / SwingDirection / magnitude), and a non-finite value there — produced
/// when an attacker/victim is caught in a transitional frame and `Normalize()` of a
/// near-zero vector yields NaN — corrupts native spatial structures (MakeSound / OnAgentHit)
/// that a later Mission.Tick walks → AccessViolation reading 0x3. See the spider auto-bite
/// crash RCA (2026-06-14) and FiniteFloatValidator's shipped-3x NaN-guard history.
/// </summary>
[TestClass]
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
        // Models Normalize() of a near-zero direction vector — the real-world NaN source.
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
