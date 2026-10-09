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
    private readonly KingdomWarSnapshotAdapter _sut = new KingdomWarSnapshotAdapter();

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
    public void FloorElapsedDays_Null_ReturnsZero()
    {
        Assert.AreEqual(0, KingdomWarSnapshotAdapter.FloorElapsedDays(null));
    }

    [DataTestMethod]
    [DataRow(float.NaN)]
    [DataRow(float.PositiveInfinity)]
    [DataRow(float.NegativeInfinity)]
    [DataRow(-1f)]
    public void FloorElapsedDays_NonFiniteOrNegative_ReturnsZero(float days)
    {
        Assert.AreEqual(0, KingdomWarSnapshotAdapter.FloorElapsedDays(days));
    }

    [TestMethod]
    public void FloorElapsedDays_AFraction_IsFloored()
    {
        Assert.AreEqual(3, KingdomWarSnapshotAdapter.FloorElapsedDays(3.9f));
    }

    [TestMethod]
    public void FloorElapsedDays_TheLargestFloatBelowTwoToThe31_IsKept()
    {
        Assert.AreEqual(2147483520, KingdomWarSnapshotAdapter.FloorElapsedDays(2147483520f));
    }

    [TestMethod]
    public void FloorElapsedDays_TwoToThe31_ReturnsZeroNotIntMinValue()
    {
        // 2^31 passes a float bound of int.MaxValue (which rounds up to 2^31), and the cast then overflows.
        Assert.AreEqual(0, KingdomWarSnapshotAdapter.FloorElapsedDays(2147483648f));
    }
}
