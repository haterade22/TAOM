using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.SmartCavalryAI;

namespace TAOM.Tests.Features.SmartCavalryAI;

/// <summary>
/// Pins the pure alignment decision the cavalry state machine gates Forming and Reforming on.
/// The previous metric (max lateral deviation across the line, tolerance 1.5 m at the default
/// strictness) could never be true for a real line, so the machine froze riders on their first
/// F3. These tests exist so the replacement is checked against numbers, not against a mock.
/// </summary>
[TestClass]
public class LineAlignmentTests
{
    private const float Epsilon = 0.001f;

    [TestMethod]
    public void Tolerance_StrictnessZero_IsTenMeters()
    {
        Assert.AreEqual(10f, LineAlignment.Tolerance(0f), Epsilon);
    }

    [TestMethod]
    public void Tolerance_DefaultStrictness_IsFourPointFourMeters()
    {
        Assert.AreEqual(4.4f, LineAlignment.Tolerance(0.7f), Epsilon);
    }

    [TestMethod]
    public void Tolerance_StrictnessOne_IsTwoMeters()
    {
        Assert.AreEqual(2f, LineAlignment.Tolerance(1f), Epsilon);
    }

    [TestMethod]
    public void Tolerance_StrictnessOutOfRange_IsClampedToUnitInterval()
    {
        Assert.AreEqual(10f, LineAlignment.Tolerance(-3f), Epsilon);
        Assert.AreEqual(2f, LineAlignment.Tolerance(7f), Epsilon);
    }

    [TestMethod]
    public void IsAligned_NoDistances_ReturnsTrue()
    {
        Assert.IsTrue(LineAlignment.IsAligned(new float[0], 0.7f));
    }

    [TestMethod]
    public void IsAligned_MeanBelowTolerance_ReturnsTrue()
    {
        // mean 4.0 < 4.4
        Assert.IsTrue(LineAlignment.IsAligned(new[] { 3f, 4f, 5f }, 0.7f));
    }

    [TestMethod]
    public void IsAligned_MeanAtTolerance_ReturnsFalse()
    {
        Assert.IsFalse(LineAlignment.IsAligned(new[] { 4.4f, 4.4f }, 0.7f));
    }

    [TestMethod]
    public void IsAligned_MeanAboveTolerance_ReturnsFalse()
    {
        // mean 5.5 > 4.4
        Assert.IsFalse(LineAlignment.IsAligned(new[] { 10f, 1f }, 0.7f));
    }

    [TestMethod]
    public void IsAligned_OneFarStraggler_UsesMeanNotMax()
    {
        // A single rider 12 m off must not hold the whole formation: mean 3.2 < 4.4.
        Assert.IsTrue(LineAlignment.IsAligned(new[] { 1f, 1f, 1f, 1f, 12f }, 0.7f));
    }

    [TestMethod]
    public void IsAligned_WideLineAtStrictnessZero_ReturnsTrueWhenRidersNearSlots()
    {
        // Twenty riders each 1 m from their slot, regardless of how wide the line is.
        var distances = new float[20];
        for (var i = 0; i < distances.Length; i++) distances[i] = 1f;
        Assert.IsTrue(LineAlignment.IsAligned(distances, 0f));
        Assert.IsTrue(LineAlignment.IsAligned(distances, 1f));
    }

    [TestMethod]
    public void IsAligned_NaNDistance_ReturnsFalse()
    {
        // A NaN in the mean poisons the comparison; the gate is a positive requirement so it fails.
        Assert.IsFalse(LineAlignment.IsAligned(new[] { 1f, float.NaN }, 0.7f));
    }

    [TestMethod]
    public void IsAligned_InfiniteDistance_ReturnsFalse()
    {
        Assert.IsFalse(LineAlignment.IsAligned(new[] { 1f, float.PositiveInfinity }, 0.7f));
    }
}
