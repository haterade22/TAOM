using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TaleWorlds.CampaignSystem;
using TaleWorlds.MountAndBlade;
using TAOM.Adapters;
using TAOM.Features.Music;
using TAOM.Features.Music.Hooks;

namespace TAOM.Tests.Features.Music;

[TestClass]
public class MusicRuntimeHookTests
{
    [TestInitialize]
    public void ResetSmokeTrace()
    {
        MusicRuntimeSmokeTrace.ResetForTests();
    }

    [TestMethod]
    public void MusicCampaignBehavior_IsCampaignBehaviorBase()
    {
        var behavior = new MusicCampaignBehavior(
            Substitute.For<IMusicCampaignContextAdapter>(),
            Substitute.For<IMusicPlaybackService>());

        Assert.IsInstanceOfType(behavior, typeof(CampaignBehaviorBase));
    }

    [TestMethod]
    public void CampaignTick_FeedsCampaignSnapshotIntoPlaybackService()
    {
        var campaign = Substitute.For<IMusicCampaignContextAdapter>();
        var playback = Substitute.For<IMusicPlaybackService>();
        var campaignSnapshot = Snapshot("gondor", world: true);
        campaign.CaptureSnapshot().Returns(campaignSnapshot);
        var behavior = new MusicCampaignBehavior(campaign, playback);

        behavior.OnCampaignTick(0.5f);

        playback.Received(1).Update(MusicRouteSnapshot.Empty, campaignSnapshot, 0.5f);
    }

    [TestMethod]
    public void CampaignTick_EmitsRuntimeSmokeForPlaybackResult()
    {
        var messages = new List<string>();
        MusicRuntimeSmokeTrace.InitializeForTests(messages.Add);
        var campaign = Substitute.For<IMusicCampaignContextAdapter>();
        var playback = Substitute.For<IMusicPlaybackService>();
        var campaignSnapshot = Snapshot("gondor", world: true);
        var result = Started(MusicRouteSource.Campaign, MusicBucket.World);
        campaign.CaptureSnapshot().Returns(campaignSnapshot);
        playback.Update(MusicRouteSnapshot.Empty, campaignSnapshot, 0.5f).Returns(result);
        var behavior = new MusicCampaignBehavior(campaign, playback);

        behavior.OnCampaignTick(0.5f);

        Assert.AreEqual(1, messages.Count);
        StringAssert.Contains(messages[0], "[Patch46-Music][RuntimeSmoke] source=campaign outcome=Started");
        StringAssert.Contains(messages[0], "bucket=World");
        StringAssert.Contains(messages[0], "culture=gondor");
        StringAssert.Contains(messages[0], "track=event:/taom/test");
        StringAssert.Contains(messages[0], "channel=0");
    }

    [TestMethod]
    public void OnSessionLaunched_StopsPlaybackAndResetsTimerSoStaleBackoffIsCleared()
    {
        var campaign = Substitute.For<IMusicCampaignContextAdapter>();
        var playback = Substitute.For<IMusicPlaybackService>();
        var behavior = new MusicCampaignBehavior(campaign, playback);

        // Simulate a prior session having a played track — OnSessionLaunched must clear it
        behavior.OnSessionLaunched(null);

        playback.Received(1).Stop("session_launched");
        // After reset the behavior ticks normally with timer starting from 0
        campaign.CaptureSnapshot().Returns(MusicRouteSnapshot.Empty);
        behavior.OnCampaignTick(1f);
        playback.Received(1).Update(MusicRouteSnapshot.Empty, MusicRouteSnapshot.Empty, 1f);
    }

    [TestMethod]
    public void CampaignTick_DoesNotRunPlaybackWhileMissionBehaviorOwnsRuntime()
    {
        var campaign = Substitute.For<IMusicCampaignContextAdapter>();
        var playback = Substitute.For<IMusicPlaybackService>();
        var behavior = new MusicCampaignBehavior(campaign, playback);

        behavior.OnMissionStarted(null);
        var result = behavior.OnCampaignTick(1f);

        Assert.AreEqual(MusicPlaybackOutcome.None, result.Outcome);
        Assert.AreEqual("mission_active", result.Reason);
        playback.Received(1).Stop("mission_started");
        playback.DidNotReceive().Update(
            Arg.Any<MusicRouteSnapshot>(),
            Arg.Any<MusicRouteSnapshot>(),
            Arg.Any<float>());
    }

    [TestMethod]
    public void CampaignMissionEnd_ReopensCampaignTickWithoutStoppingPlayback()
    {
        var campaign = Substitute.For<IMusicCampaignContextAdapter>();
        var playback = Substitute.For<IMusicPlaybackService>();
        var campaignSnapshot = Snapshot("rohan", world: true);
        campaign.CaptureSnapshot().Returns(campaignSnapshot);
        var behavior = new MusicCampaignBehavior(campaign, playback);

        behavior.OnMissionStarted(null);
        behavior.OnMissionEnded(null);
        behavior.OnCampaignTick(2f);

        playback.Received(1).Stop("mission_started");
        playback.DidNotReceive().Stop("mission_ended");
        playback.Received(1).Update(MusicRouteSnapshot.Empty, campaignSnapshot, 2f);
    }

    [TestMethod]
    public void MusicMissionBehavior_IsMissionBehaviorOther()
    {
        var behavior = new MusicMissionBehavior(
            Substitute.For<IMusicMissionContextAdapter>(),
            Substitute.For<IMusicCampaignContextAdapter>(),
            Substitute.For<IMusicPlaybackService>());

        Assert.IsInstanceOfType(behavior, typeof(MissionBehavior));
        Assert.AreEqual(MissionBehaviorType.Other, behavior.BehaviorType);
    }

    [TestMethod]
    public void MissionTick_CapturesCampaignFirstAndUsesCultureAsMissionFallback()
    {
        var mission = Substitute.For<IMusicMissionContextAdapter>();
        var campaign = Substitute.For<IMusicCampaignContextAdapter>();
        var playback = Substitute.For<IMusicPlaybackService>();
        var campaignSnapshot = Snapshot("gondor", world: true);
        var missionSnapshot = Snapshot("gondor", battle: true);
        campaign.CaptureSnapshot().Returns(campaignSnapshot);
        mission.CaptureSnapshot("gondor").Returns(missionSnapshot);
        var behavior = new MusicMissionBehavior(mission, campaign, playback);

        behavior.OnMissionRuntimeTick(42f);

        Received.InOrder(() =>
        {
            campaign.CaptureSnapshot();
            mission.CaptureSnapshot("gondor");
            playback.Update(missionSnapshot, campaignSnapshot, 42f);
        });
    }

    [TestMethod]
    public void MissionTick_EmitsRuntimeSmokeForPlaybackResult()
    {
        var messages = new List<string>();
        MusicRuntimeSmokeTrace.InitializeForTests(messages.Add);
        var mission = Substitute.For<IMusicMissionContextAdapter>();
        var campaign = Substitute.For<IMusicCampaignContextAdapter>();
        var playback = Substitute.For<IMusicPlaybackService>();
        var campaignSnapshot = Snapshot("gondor", world: true);
        var missionSnapshot = Snapshot("gondor", battle: true);
        var result = Started(MusicRouteSource.Mission, MusicBucket.Battle);
        campaign.CaptureSnapshot().Returns(campaignSnapshot);
        mission.CaptureSnapshot("gondor").Returns(missionSnapshot);
        playback.Update(missionSnapshot, campaignSnapshot, 42f).Returns(result);
        var behavior = new MusicMissionBehavior(mission, campaign, playback);

        behavior.OnMissionRuntimeTick(42f);

        Assert.AreEqual(1, messages.Count);
        StringAssert.Contains(messages[0], "[Patch46-Music][RuntimeSmoke] source=mission outcome=Started");
        StringAssert.Contains(messages[0], "bucket=Battle");
        StringAssert.Contains(messages[0], "culture=gondor");
        StringAssert.Contains(messages[0], "track=event:/taom/test");
        StringAssert.Contains(messages[0], "channel=0");
    }

    [TestMethod]
    public void MissionEnd_StopsPlayback()
    {
        var playback = Substitute.For<IMusicPlaybackService>();
        var customBattle = Substitute.For<ICustomBattleMusicContextService>();
        var behavior = new MusicMissionBehavior(
            Substitute.For<IMusicMissionContextAdapter>(),
            Substitute.For<IMusicCampaignContextAdapter>(),
            playback,
            customBattle);

        behavior.OnEndMissionInternal();
        behavior.OnRemoveBehavior();

        playback.Received(1).Stop("mission_ended");
        playback.Received(1).Stop("mission_behavior_removed");
        customBattle.Received(2).ClearSelectedCulture();
    }

    private static MusicRouteSnapshot Snapshot(
        string culture,
        bool siege = false,
        bool battle = false,
        bool tavern = false,
        bool town = false,
        bool world = false)
    {
        return new MusicRouteSnapshot(true, culture, siege, battle, tavern, town, world, "test");
    }

    private static MusicPlaybackResult Started(MusicRouteSource source, MusicBucket bucket)
    {
        var track = new MusicTrackDefinition(bucket, "gondor", "event:/taom/test", "taom/test.ogg", "D:\\taom\\test.ogg");
        var pool = new MusicTrackPool(bucket, "gondor", "gondor", new[] { track });
        var decision = MusicRouteDecision.Selected(source, bucket, pool, "test");
        return MusicPlaybackResult.Started(track, decision, 0);
    }
}
