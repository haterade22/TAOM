using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.Music;

namespace TAOM.Tests.Features.Music;

[TestClass]
public class MusicSettingsProviderTests
{
    [TestMethod]
    public void GetSnapshot_ReturnsSafeSourceDropDefaults()
    {
        var provider = new MusicSettingsProvider();

        var snapshot = provider.GetSnapshot();

        Assert.IsTrue(snapshot.MusicEnabled);
        Assert.IsTrue(snapshot.CampaignContextEnabled);
        Assert.IsTrue(snapshot.MissionContextEnabled);
        Assert.IsTrue(snapshot.RouteSettings.SiegeEnabled);
        Assert.IsTrue(snapshot.RouteSettings.BattleEnabled);
        Assert.IsTrue(snapshot.RouteSettings.TavernEnabled);
        Assert.IsTrue(snapshot.RouteSettings.TownEnabled);
        Assert.IsTrue(snapshot.RouteSettings.WorldEnabled);
        Assert.IsTrue(snapshot.UseNoRepeatShuffle);
        Assert.AreEqual(8, snapshot.NoRepeatHistorySize);
        Assert.AreEqual(1f, snapshot.MasterVolume);
        Assert.AreEqual(1f, snapshot.GetBucketVolume(MusicBucket.Battle));
    }

    [TestMethod]
    public void Snapshot_ClampsUnsafeNumericValues()
    {
        var snapshot = new MusicSettingsSnapshot(
            musicEnabled: true,
            campaignContextEnabled: true,
            missionContextEnabled: true,
            routeSettings: MusicRouteSettings.AllEnabled,
            rotation: default,
            useNoRepeatShuffle: true,
            noRepeatHistorySize: 500,
            masterVolume: 2f,
            worldVolume: 0.5f,
            townVolume: 3f,
            tavernVolume: -2f,
            battleVolume: 1.5f,
            siegeVolume: float.NegativeInfinity);

        Assert.AreEqual(64, snapshot.NoRepeatHistorySize);
        Assert.AreEqual(1f, snapshot.MasterVolume);
        Assert.AreEqual(0.5f, snapshot.WorldVolume);
        Assert.AreEqual(1f, snapshot.TownVolume);
        Assert.AreEqual(0f, snapshot.TavernVolume);
        Assert.AreEqual(1f, snapshot.BattleVolume);
        Assert.AreEqual(0f, snapshot.SiegeVolume);
    }
}
