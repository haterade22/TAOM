using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.LongDefeat.Domain;

namespace TAOM.Tests.Features.LongDefeat.Domain;

/// <summary>The health share a remount carries onto the new body (decision D3): a number between 0 and 1 for any health the engine can report, and never NaN.</summary>
[TestClass]
public class LdRemountTests
{
    [TestMethod]
    public void TheShare_IsHealthOverLimit()
    {
        Assert.AreEqual(0.5f, LdRemount.ShareOf(50f, 100f), 0.0001f);
    }

    [TestMethod]
    public void TheShare_IsCappedAtOne_WhenHealthIsAboveTheLimit()
    {
        Assert.AreEqual(1f, LdRemount.ShareOf(150f, 100f));
    }

    [TestMethod]
    public void TheShare_IsOne_WhenTheLimitIsNotPositive()
    {
        Assert.AreEqual(1f, LdRemount.ShareOf(10f, 0f));
        Assert.AreEqual(1f, LdRemount.ShareOf(10f, -5f));
    }

    [TestMethod]
    public void TheShare_IsOne_NotNaN_WhenHealthOrLimitIsNaN()
    {
        // On .NET Framework Math.Min and Math.Max return the second argument when it is NaN, so Math.Max(0f, Math.Min(1f, NaN)) is NaN.
        Assert.AreEqual(1f, LdRemount.ShareOf(float.NaN, 100f));
        Assert.AreEqual(1f, LdRemount.ShareOf(10f, float.NaN));
    }

    [TestMethod]
    public void TheShare_IsZero_WhenHealthIsNegative_SoTheNewBodyStartsAtItsFloor()
    {
        Assert.AreEqual(0f, LdRemount.ShareOf(-3f, 100f));
    }

    [TestMethod]
    public void TheShare_IsOne_WhenTheRatioIsNotANumber()
    {
        Assert.AreEqual(1f, LdRemount.ShareOf(float.PositiveInfinity, float.PositiveInfinity));
    }
}
