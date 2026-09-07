using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.MarriageAlignment;

namespace TAOM.Tests.Features.MarriageAlignment;

/// <summary>
/// The candidate-clan pool cache is static state inside a Harmony patch, which is the shape that
/// produced the shader-precompilation sentinel-collision RCA. The invalidation decision was pulled
/// out into <see cref="MarriageClanPoolStamp"/> precisely so it could be pinned here instead of
/// being unreachable behind an engine-only code path.
/// </summary>
[TestClass]
public class MarriageClanPoolStampTests
{
    private const string CampaignA = "abc123def456";
    private const string CampaignB = "zzz999yyy888";

    private MarriageClanPoolStamp _sut = null!;

    [TestInitialize]
    public void Setup() => _sut = new MarriageClanPoolStamp();

    [TestMethod]
    public void ShouldInvalidate_FirstObservation_Invalidates()
    {
        // The initial state is (null, -1, -1), which no live campaign can produce, so the first
        // call must always clear. This is the sentinel case, and it is intended, not a collision.
        Assert.IsTrue(_sut.ShouldInvalidate(CampaignA, clanCount: 145, day: 0));
    }

    [TestMethod]
    public void ShouldInvalidate_FirstObservationWithZeroCountAndDay_StillInvalidates()
    {
        // Guards the exact shape of the shader-precompilation bug: a sentinel of -1 meeting a first
        // real observation of 0. Zero must NOT be mistaken for "same as sentinel".
        Assert.IsTrue(_sut.ShouldInvalidate(CampaignA, clanCount: 0, day: 0));
    }

    [TestMethod]
    public void ShouldInvalidate_RepeatedIdenticalObservation_DoesNotInvalidate()
    {
        _sut.ShouldInvalidate(CampaignA, 145, 12);

        Assert.IsFalse(_sut.ShouldInvalidate(CampaignA, 145, 12));
        Assert.IsFalse(_sut.ShouldInvalidate(CampaignA, 145, 12));
    }

    [TestMethod]
    public void ShouldInvalidate_DayAdvances_Invalidates()
    {
        _sut.ShouldInvalidate(CampaignA, 145, 12);

        Assert.IsTrue(_sut.ShouldInvalidate(CampaignA, 145, 13));
    }

    [TestMethod]
    public void ShouldInvalidate_ClanCreatedOrEliminated_Invalidates()
    {
        _sut.ShouldInvalidate(CampaignA, 145, 12);

        // A clan's culture is only ever assigned at creation, and creation moves this count, so the
        // count is what actually covers culture churn in the clan population.
        Assert.IsTrue(_sut.ShouldInvalidate(CampaignA, 146, 12));
        Assert.IsTrue(_sut.ShouldInvalidate(CampaignA, 145, 12));
    }

    [TestMethod]
    public void ShouldInvalidate_SecondCampaignSameProcess_Invalidates()
    {
        // The reason this class exists. Pools built for campaign A must never be served to
        // campaign B, even when the clan count and day happen to coincide.
        _sut.ShouldInvalidate(CampaignA, 145, 12);

        Assert.IsTrue(_sut.ShouldInvalidate(CampaignB, 145, 12));
    }

    [TestMethod]
    public void ShouldInvalidate_NullCampaignId_IsHandledAndDistinctFromARealId()
    {
        Assert.IsTrue(_sut.ShouldInvalidate(null, 145, 12));
        Assert.IsFalse(_sut.ShouldInvalidate(null, 145, 12));
        Assert.IsTrue(_sut.ShouldInvalidate(CampaignA, 145, 12));
    }

    [TestMethod]
    public void ShouldInvalidate_AfterInvalidating_RecordsTheNewObservation()
    {
        _sut.ShouldInvalidate(CampaignA, 145, 12);
        _sut.ShouldInvalidate(CampaignA, 145, 13);

        // The clear happens once per change, not on every call afterwards, or the cache would be
        // rebuilt on every single lookup and the whole cache would be pointless.
        Assert.IsFalse(_sut.ShouldInvalidate(CampaignA, 145, 13));
    }
}
