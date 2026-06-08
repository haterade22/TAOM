using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Adapters;
using TAOM.Features.Music;

namespace TAOM.Tests.Features.Music;

[TestClass]
public class MusicMissionContextAdapterTests
{
    [TestMethod]
    public void CaptureSnapshot_ReturnsEmptyWhenMissionInactive()
    {
        var adapter = Adapter(MusicMissionContextState.Inactive);

        var snapshot = adapter.CaptureSnapshot("gondor");

        Assert.IsFalse(snapshot.IsActive);
        Assert.AreEqual("empty", snapshot.Reason);
    }

    [TestMethod]
    public void CaptureSnapshot_ReturnsEmptyWhenMissionHasNoBucketClassification()
    {
        var adapter = Adapter(new MusicMissionContextState(
            true,
            siege: false,
            battle: false,
            town: false,
            tavern: false,
            cultureId: "gondor",
            sceneId: "town_ES2",
            reason: "mission_unclassified"));

        var snapshot = adapter.CaptureSnapshot("gondor");

        Assert.IsFalse(snapshot.IsActive);
    }

    [TestMethod]
    public void CaptureSnapshot_MapsSiegeAndBattleFlagsWithoutDroppingSiegePrioritySignal()
    {
        var adapter = Adapter(new MusicMissionContextState(
            true,
            siege: true,
            battle: true,
            town: false,
            tavern: false,
            cultureId: "gondor",
            sceneId: "siege_scene",
            reason: "mission_siege_over_battle"));

        var snapshot = adapter.CaptureSnapshot("rohan");

        Assert.IsTrue(snapshot.IsActive);
        Assert.IsTrue(snapshot.Siege);
        Assert.IsTrue(snapshot.Battle);
        Assert.IsFalse(snapshot.World);
        Assert.AreEqual("gondor", snapshot.CultureId);
        Assert.AreEqual("mission_siege_over_battle", snapshot.Reason);
    }

    [TestMethod]
    public void CaptureSnapshot_UsesFallbackCultureWhenMissionHasNoCulture()
    {
        var adapter = Adapter(new MusicMissionContextState(
            true,
            siege: false,
            battle: true,
            town: false,
            tavern: false,
            cultureId: null,
            sceneId: "battle_terrain_b"));

        var snapshot = adapter.CaptureSnapshot("vlandia");

        Assert.IsTrue(snapshot.IsActive);
        Assert.IsTrue(snapshot.Battle);
        Assert.AreEqual("vlandia", snapshot.CultureId);
        Assert.AreEqual("mission:battle_terrain_b", snapshot.Reason);
    }

    [TestMethod]
    public void CaptureSnapshot_UsesCustomBattleCultureBeforeNeutralFallbackWhenMissionHasNoCulture()
    {
        var customBattle = new CustomBattleMusicContextService();
        customBattle.SelectPlayerCulture("gondor");
        var adapter = Adapter(new MusicMissionContextState(
            true,
            siege: false,
            battle: true,
            town: false,
            tavern: false,
            cultureId: null,
            sceneId: "custom_battle_scene"),
            customBattle);

        var snapshot = adapter.CaptureSnapshot(MusicTrackIndex.NeutralCulture);

        Assert.IsTrue(snapshot.IsActive);
        Assert.IsTrue(snapshot.Battle);
        Assert.AreEqual("gondor", snapshot.CultureId);
    }

    [TestMethod]
    public void CaptureSnapshot_ExplicitMissionCultureWinsOverCustomBattleCulture()
    {
        var customBattle = new CustomBattleMusicContextService();
        customBattle.SelectPlayerCulture("gondor");
        var adapter = Adapter(new MusicMissionContextState(
            true,
            siege: true,
            battle: true,
            town: false,
            tavern: false,
            cultureId: "mordor",
            sceneId: "custom_siege_scene"),
            customBattle);

        var snapshot = adapter.CaptureSnapshot(MusicTrackIndex.NeutralCulture);

        Assert.IsTrue(snapshot.IsActive);
        Assert.IsTrue(snapshot.Siege);
        Assert.AreEqual("mordor", snapshot.CultureId);
    }

    [TestMethod]
    public void CaptureSnapshot_RealCampaignFallbackWinsOverStaleCustomBattleCulture()
    {
        var customBattle = new CustomBattleMusicContextService();
        customBattle.SelectPlayerCulture("mordor");
        var adapter = Adapter(new MusicMissionContextState(
            true,
            siege: false,
            battle: true,
            town: false,
            tavern: false,
            cultureId: null,
            sceneId: "campaign_battle_scene"),
            customBattle);

        var snapshot = adapter.CaptureSnapshot("gondor");

        Assert.IsTrue(snapshot.IsActive);
        Assert.IsTrue(snapshot.Battle);
        Assert.AreEqual("gondor", snapshot.CultureId);
    }

    private static MusicMissionContextAdapter Adapter(MusicMissionContextState state)
    {
        return Adapter(state, null);
    }

    private static MusicMissionContextAdapter Adapter(
        MusicMissionContextState state,
        ICustomBattleMusicContextService customBattle)
    {
        return new MusicMissionContextAdapter(new FakeMissionSource(state), customBattle);
    }

    private sealed class FakeMissionSource : IMusicMissionContextSource
    {
        private readonly MusicMissionContextState _state;

        public FakeMissionSource(MusicMissionContextState state)
        {
            _state = state;
        }

        public MusicMissionContextState Capture(string fallbackCultureId)
        {
            return _state;
        }
    }
}
