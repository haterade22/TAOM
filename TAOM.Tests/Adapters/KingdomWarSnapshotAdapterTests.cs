using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Adapters;

namespace TAOM.Tests.Adapters;

/// <summary>
/// The kingdom snapshot adapter outside a campaign (the contract in IKingdomWarSnapshotAdapter: empty or
/// zero, never a throw), and the elapsed-day cast. The no-campaign cases run Campaign.Current, so only
/// they need the game; the cast helper runs no engine code.
/// </summary>
[TestClass]
public class KingdomWarSnapshotAdapterTests
{
    private readonly KingdomWarSnapshotAdapter _sut = new KingdomWarSnapshotAdapter(NSubstitute.Substitute.For<TAOM.Features.CoopInterop.IPlayerControlQuery>());

    [TestMethod]
    [TestCategory("RequiresGame")]
    public void GetKingdoms_NoCampaign_ReturnsEmpty()
    {
        Assert.AreEqual(0, _sut.GetKingdoms().Count);
    }

    [TestMethod]
    [TestCategory("RequiresGame")]
    public void GetCampaignId_NoCampaign_ReturnsEmpty()
    {
        Assert.AreEqual(string.Empty, _sut.GetCampaignId());
    }

    [TestMethod]
    [TestCategory("RequiresGame")]
    public void GetElapsedDay_NoCampaign_ReturnsZero()
    {
        Assert.AreEqual(0, _sut.GetElapsedDay());
    }

    [TestMethod]
    [TestCategory("RequiresGame")]
    public void GetNowHours_NoCampaign_ReturnsZero()
    {
        Assert.AreEqual(0d, _sut.GetNowHours());
    }

    [TestMethod]
    [TestCategory("RequiresGame")]
    public void TryGetElapsedDay_NoCampaign_IsInvalid()
    {
        Assert.IsFalse(_sut.TryGetElapsedDay(out _));
    }

    [TestMethod]
    public void TryFloorElapsedDays_Null_IsInvalid()
    {
        Assert.IsFalse(KingdomWarSnapshotAdapter.TryFloorElapsedDays(null, out _));
    }

    [DataTestMethod]
    [DataRow(float.NaN)]
    [DataRow(float.PositiveInfinity)]
    [DataRow(float.NegativeInfinity)]
    [DataRow(-1f)]
    public void TryFloorElapsedDays_NonFiniteOrNegative_IsInvalid(float days)
    {
        // Not day 0: a day-0 claim or load marker from a broken clock is a decision on a made-up value.
        Assert.IsFalse(KingdomWarSnapshotAdapter.TryFloorElapsedDays(days, out _));
    }

    [TestMethod]
    public void TryFloorElapsedDays_AFraction_IsFloored()
    {
        Assert.IsTrue(KingdomWarSnapshotAdapter.TryFloorElapsedDays(3.9f, out var day));
        Assert.AreEqual(3, day);
    }

    [TestMethod]
    public void TryFloorElapsedDays_Zero_IsAValidDayZero()
    {
        Assert.IsTrue(KingdomWarSnapshotAdapter.TryFloorElapsedDays(0f, out var day));
        Assert.AreEqual(0, day);
    }

    [TestMethod]
    public void TryFloorElapsedDays_TheLargestFloatBelowTwoToThe31_IsKept()
    {
        Assert.IsTrue(KingdomWarSnapshotAdapter.TryFloorElapsedDays(2147483520f, out var day));
        Assert.AreEqual(2147483520, day);
    }

    [TestMethod]
    public void TryFloorElapsedDays_TwoToThe31_IsInvalidNotIntMinValue()
    {
        // 2^31 passes a float bound of int.MaxValue (which rounds up to 2^31), and the cast then overflows.
        Assert.IsFalse(KingdomWarSnapshotAdapter.TryFloorElapsedDays(2147483648f, out _));
    }
}
