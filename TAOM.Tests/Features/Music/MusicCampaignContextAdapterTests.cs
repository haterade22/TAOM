using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Adapters;
using TAOM.Features.Music;

namespace TAOM.Tests.Features.Music;

[TestClass]
public class MusicCampaignContextAdapterTests
{
    [TestMethod]
    public void CaptureSnapshot_ReturnsEmptyWhenCampaignInactive()
    {
        var adapter = Adapter(MusicCampaignContextState.Inactive);

        var snapshot = adapter.CaptureSnapshot();

        Assert.IsFalse(snapshot.IsActive);
        Assert.AreEqual("empty", snapshot.Reason);
    }

    [TestMethod]
    public void CaptureSnapshot_KeepsSiegeAndBattleSignalsForResolverPriority()
    {
        var adapter = Adapter(new MusicCampaignContextState(
            true,
            siege: true,
            battle: true,
            inSettlement: true,
            inTavern: false,
            stableCultureId: "gondor",
            settlementCultureId: "rohan",
            settlementId: "osgiliath",
            reason: "combat_siege_requested_over_battle"));

        var snapshot = adapter.CaptureSnapshot();

        Assert.IsTrue(snapshot.IsActive);
        Assert.IsTrue(snapshot.Siege);
        Assert.IsTrue(snapshot.Battle);
        Assert.IsFalse(snapshot.World);
        Assert.AreEqual("gondor", snapshot.CultureId);
        Assert.AreEqual("combat_siege_requested_over_battle", snapshot.Reason);
    }

    [TestMethod]
    public void CaptureSnapshot_UsesSettlementCultureForTownBucket()
    {
        var adapter = Adapter(new MusicCampaignContextState(
            true,
            siege: false,
            battle: false,
            inSettlement: true,
            inTavern: false,
            stableCultureId: "gondor",
            settlementCultureId: "vlandia",
            settlementId: "edoras"));

        var snapshot = adapter.CaptureSnapshot();

        Assert.IsTrue(snapshot.IsActive);
        Assert.IsTrue(snapshot.Town);
        Assert.IsFalse(snapshot.Tavern);
        Assert.IsFalse(snapshot.World);
        Assert.AreEqual("vlandia", snapshot.CultureId);
        Assert.AreEqual("campaign_town:edoras", snapshot.Reason);
    }

    [TestMethod]
    public void CaptureSnapshot_UsesSettlementCultureForTavernBucket()
    {
        var adapter = Adapter(new MusicCampaignContextState(
            true,
            siege: false,
            battle: false,
            inSettlement: true,
            inTavern: true,
            stableCultureId: "gondor",
            settlementCultureId: "dolguldur",
            settlementId: "dol_guldur"));

        var snapshot = adapter.CaptureSnapshot();

        Assert.IsTrue(snapshot.IsActive);
        Assert.IsTrue(snapshot.Tavern);
        Assert.IsFalse(snapshot.Town);
        Assert.AreEqual("dolguldur", snapshot.CultureId);
        Assert.AreEqual("campaign_tavern:dol_guldur", snapshot.Reason);
    }

    [TestMethod]
    public void CaptureSnapshot_UsesStableCultureForWorldBucket()
    {
        var adapter = Adapter(new MusicCampaignContextState(
            true,
            siege: false,
            battle: false,
            inSettlement: false,
            inTavern: false,
            stableCultureId: "gondor",
            settlementCultureId: "vlandia",
            settlementId: null));

        var snapshot = adapter.CaptureSnapshot();

        Assert.IsTrue(snapshot.IsActive);
        Assert.IsTrue(snapshot.World);
        Assert.IsFalse(snapshot.Town);
        Assert.AreEqual("gondor", snapshot.CultureId);
        Assert.AreEqual("campaign_world", snapshot.Reason);
    }

    [TestMethod]
    public void CaptureSnapshot_UsesNeutralCultureWhenNoCultureExists()
    {
        var adapter = Adapter(new MusicCampaignContextState(
            true,
            siege: false,
            battle: false,
            inSettlement: false,
            inTavern: false,
            stableCultureId: null,
            settlementCultureId: null,
            settlementId: null));

        var snapshot = adapter.CaptureSnapshot();

        Assert.IsTrue(snapshot.IsActive);
        Assert.IsTrue(snapshot.World);
        Assert.AreEqual(MusicTrackIndex.NeutralCulture, snapshot.CultureId);
    }

    private static MusicCampaignContextAdapter Adapter(MusicCampaignContextState state)
    {
        return new MusicCampaignContextAdapter(new FakeCampaignSource(state));
    }

    private sealed class FakeCampaignSource : IMusicCampaignContextSource
    {
        private readonly MusicCampaignContextState _state;

        public FakeCampaignSource(MusicCampaignContextState state)
        {
            _state = state;
        }

        public MusicCampaignContextState Capture()
        {
            return _state;
        }
    }
}
