using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.CastleRecruitment;

namespace TAOM.Tests.Features.CastleRecruitment;

/// <summary>The day key of the #918 check; cases ported from 1.5.x KingdomWarSnapshotAdapterTests.</summary>
[TestClass]
public class CastleRecruitDayTests
{
    [TestMethod]
    public void TryFloorElapsedDays_Null_IsInvalid()
    {
        Assert.IsFalse(CastleRecruitDay.TryFloorElapsedDays(null, out _));
    }

    [DataTestMethod]
    [DataRow(float.NaN)]
    [DataRow(float.PositiveInfinity)]
    [DataRow(float.NegativeInfinity)]
    [DataRow(-1f)]
    public void TryFloorElapsedDays_NonFiniteOrNegative_IsInvalid(float days)
    {
        // Not day 0: a day-0 claim or load marker from a broken clock is a decision on a made-up value.
        Assert.IsFalse(CastleRecruitDay.TryFloorElapsedDays(days, out _));
    }

    [TestMethod]
    public void TryFloorElapsedDays_AFraction_IsFloored()
    {
        Assert.IsTrue(CastleRecruitDay.TryFloorElapsedDays(3.9f, out var day));
        Assert.AreEqual(3, day);
    }

    [TestMethod]
    public void TryFloorElapsedDays_Zero_IsAValidDayZero()
    {
        Assert.IsTrue(CastleRecruitDay.TryFloorElapsedDays(0f, out var day));
        Assert.AreEqual(0, day);
    }

    [TestMethod]
    public void TryFloorElapsedDays_TheLargestFloatBelowTwoToThe31_IsKept()
    {
        Assert.IsTrue(CastleRecruitDay.TryFloorElapsedDays(2147483520f, out var day));
        Assert.AreEqual(2147483520, day);
    }

    [TestMethod]
    public void TryFloorElapsedDays_TwoToThe31_IsInvalidNotIntMinValue()
    {
        // 2^31 passes a float bound of int.MaxValue (which rounds up to 2^31), and the cast then overflows.
        Assert.IsFalse(CastleRecruitDay.TryFloorElapsedDays(2147483648f, out _));
    }
}
