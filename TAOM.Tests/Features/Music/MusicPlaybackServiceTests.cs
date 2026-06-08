using System;
using System.Xml.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Adapters;
using TAOM.Features.Music;

namespace TAOM.Tests.Features.Music;

[TestClass]
public class MusicPlaybackServiceTests
{
    private const string ModuleRoot = "D:/TAOM/Main/_Module";

    private IMusicEngineAdapter _engine;
    private IMusicSettingsProvider _settings;
    private MusicPlaybackService _service;

    [TestInitialize]
    public void SetUp()
    {
        _engine = Substitute.For<IMusicEngineAdapter>();
        _settings = Substitute.For<IMusicSettingsProvider>();
        _settings.GetSnapshot().Returns(Settings(useNoRepeatShuffle: false));

        _service = new MusicPlaybackService(
            _engine,
            _settings,
            Index(),
            new MusicTransitionResolver(),
            new NoRepeatShufflePicker());
    }

    [TestMethod]
    public void Update_StartsResolvedTrackThroughEngineMusicAdapter()
    {
        _engine.GetFreeMusicChannelIndex().Returns(7);
        _engine.IsClipLoaded(7).Returns(true);

        var result = _service.Update(Snapshot("gondor", battle: true), MusicRouteSnapshot.Empty, timerSeconds: 0f);

        Assert.AreEqual(MusicPlaybackOutcome.Started, result.Outcome);
        Assert.AreEqual(7, result.Channel);
        Assert.AreEqual("taom/battle_music/gondor/battle_a", result.Track.EventName);
        _engine.Received(1).LoadClip(7, Arg.Is<string>(path =>
            EndsWithNormalizedPath(path, "ModuleSounds/taom/battle_music/gondor/battle_a.ogg")));
        Received.InOrder(() =>
        {
            _engine.SetVolume(7, 0.125f);
            _engine.PlayMusic(7);
        });
    }

    [TestMethod]
    public void Update_StartsResolvedTrackWithoutSuppressionOwnerSideEffects()
    {
        _engine.GetFreeMusicChannelIndex().Returns(7);
        _engine.IsClipLoaded(7).Returns(true);

        var result = _service.Update(Snapshot("gondor", battle: true), MusicRouteSnapshot.Empty, timerSeconds: 0f);

        Assert.AreEqual(MusicPlaybackOutcome.Started, result.Outcome);
        _engine.Received(1).GetFreeMusicChannelIndex();
    }

    [TestMethod]
    public void Update_AllocatesFreeChannelDirectly()
    {
        _engine.GetFreeMusicChannelIndex().Returns(7);
        _engine.IsClipLoaded(7).Returns(true);

        var result = _service.Update(Snapshot("gondor", battle: true), MusicRouteSnapshot.Empty, timerSeconds: 0f);

        Assert.AreEqual(MusicPlaybackOutcome.Started, result.Outcome);
        _engine.Received(1).GetFreeMusicChannelIndex();
    }

    [TestMethod]
    public void Update_ReturnsFailedWithoutLoadingWhenNoFreeChannelExistsEvenAfterEviction()
    {
        // All 5 GetFreeMusicChannelIndex calls return -1: initial attempt + 4 eviction attempts
        _engine.GetFreeMusicChannelIndex().Returns(-1);

        var result = _service.Update(Snapshot("gondor", battle: true), MusicRouteSnapshot.Empty, timerSeconds: 0f);

        Assert.AreEqual(MusicPlaybackOutcome.Failed, result.Outcome);
        Assert.AreEqual("no_free_music_channel", result.Reason);
        _engine.DidNotReceive().LoadClip(Arg.Any<int>(), Arg.Any<string>());
        _engine.DidNotReceive().PlayMusic(Arg.Any<int>());
        // Eviction was attempted on all 4 channels before giving up
        _engine.Received(1).StopMusic(0);
        _engine.Received(1).StopMusic(1);
        _engine.Received(1).StopMusic(2);
        _engine.Received(1).StopMusic(3);
    }

    [TestMethod]
    public void Update_EvictsVanillaMusicAndStartsWhenChannelFreeAfterEviction()
    {
        // Initial check returns -1 (vanilla holds channel 0), eviction of channel 0 frees it
        _engine.GetFreeMusicChannelIndex().Returns(-1, 7);
        _engine.IsClipLoaded(7).Returns(true);

        var result = _service.Update(Snapshot("gondor", battle: true), MusicRouteSnapshot.Empty, timerSeconds: 0f);

        Assert.AreEqual(MusicPlaybackOutcome.Started, result.Outcome);
        Assert.AreEqual(7, result.Channel);
        // Eviction only needed channel 0 before a channel became free
        _engine.Received(1).StopMusic(0);
        _engine.Received(1).UnloadClip(0);
        _engine.DidNotReceive().StopMusic(1);
        _engine.Received(1).PlayMusic(7);
    }

    [TestMethod]
    public void Update_BacksOffNoFreeChannelRetriesForSameRouteUntilRetryWindow()
    {
        // All 5 calls (initial + 4 evictions) return -1, then 7 on the retry after back-off
        _engine.GetFreeMusicChannelIndex().Returns(-1, -1, -1, -1, -1, 7);
        _engine.IsClipLoaded(7).Returns(true);

        var first = _service.Update(Snapshot("gondor", battle: true), MusicRouteSnapshot.Empty, timerSeconds: 0f);
        var second = _service.Update(Snapshot("gondor", battle: true), MusicRouteSnapshot.Empty, timerSeconds: 0.25f);
        var third = _service.Update(Snapshot("gondor", battle: true), MusicRouteSnapshot.Empty, timerSeconds: 1.1f);

        Assert.AreEqual(MusicPlaybackOutcome.Failed, first.Outcome);
        Assert.AreEqual("no_free_music_channel", first.Reason);
        Assert.AreEqual(MusicPlaybackOutcome.None, second.Outcome);
        Assert.AreEqual("waiting_for_free_music_channel", second.Reason);
        Assert.AreEqual(MusicPlaybackOutcome.Started, third.Outcome);
        Assert.AreEqual(7, third.Channel);
        _engine.Received(1).LoadClip(7, Arg.Is<string>(path =>
            EndsWithNormalizedPath(path, "ModuleSounds/taom/battle_music/gondor/battle_b.ogg")));
        _engine.Received(1).PlayMusic(7);
    }

    [TestMethod]
    public void Update_StopsOwnedRouteBeforeAllocatingChannelForNextRoute()
    {
        var firstAllocation = true;
        var activeChannelReleased = false;
        _engine.GetFreeMusicChannelIndex().Returns(_ =>
        {
            if (firstAllocation)
            {
                firstAllocation = false;
                return 7;
            }

            return activeChannelReleased ? 7 : -1;
        });
        _engine.When(e => e.StopMusic(7)).Do(_ => activeChannelReleased = true);
        _engine.IsClipLoaded(7).Returns(true);

        var first = _service.Update(MusicRouteSnapshot.Empty, Snapshot("gondor", world: true), timerSeconds: 0f);
        Assert.AreEqual(MusicPlaybackOutcome.Started, first.Outcome);
        _engine.ClearReceivedCalls();

        var second = _service.Update(MusicRouteSnapshot.Empty, Snapshot("gondor", town: true), timerSeconds: 10f);

        Assert.AreEqual(MusicPlaybackOutcome.Started, second.Outcome);
        Assert.AreEqual(MusicBucket.Town, second.Decision.Bucket);
        Assert.AreEqual(7, second.Channel);
        Received.InOrder(() =>
        {
            _engine.StopMusic(7);
            _engine.UnloadClip(7);
            _engine.GetFreeMusicChannelIndex();
            _engine.LoadClip(7, Arg.Is<string>(path =>
                EndsWithNormalizedPath(path, "ModuleSounds/taom/town_wander/gondor/town_a.ogg")));
            _engine.PlayMusic(7);
        });
    }

    [TestMethod]
    public void Update_TriesSameResolvedPoolCandidatesBeforeFailingWhenClipsNeverLoad()
    {
        _engine.GetFreeMusicChannelIndex().Returns(7);
        _engine.IsClipLoaded(7).Returns(false);

        var result = _service.Update(Snapshot("gondor", battle: true), MusicRouteSnapshot.Empty, timerSeconds: 0f);

        Assert.AreEqual(MusicPlaybackOutcome.Failed, result.Outcome);
        Assert.AreEqual("clip_not_loaded", result.Reason);
        Assert.AreEqual("taom/battle_music/gondor/battle_b", result.Track.EventName);
        _engine.Received(2).LoadClip(7, Arg.Is<string>(path =>
            EndsWithNormalizedPath(path, "ModuleSounds/taom/battle_music/gondor/battle_a.ogg")));
        _engine.Received(2).LoadClip(7, Arg.Is<string>(path =>
            EndsWithNormalizedPath(path, "ModuleSounds/taom/battle_music/gondor/battle_b.ogg")));
        _engine.Received(4).UnloadClip(7);
        _engine.DidNotReceive().PlayMusic(Arg.Any<int>());
    }

    [TestMethod]
    public void Update_WhenSelectedClipFailsToLoad_StartsAnotherTrackFromSameResolvedPool()
    {
        _engine.GetFreeMusicChannelIndex().Returns(7, 8);
        _engine.IsClipLoaded(7).Returns(false);
        _engine.IsClipLoaded(8).Returns(true);

        var result = _service.Update(Snapshot("gondor", battle: true), MusicRouteSnapshot.Empty, timerSeconds: 0f);

        Assert.AreEqual(MusicPlaybackOutcome.Started, result.Outcome);
        Assert.AreEqual(8, result.Channel);
        Assert.AreEqual(MusicBucket.Battle, result.Decision.Bucket);
        Assert.AreEqual("gondor", result.Decision.Pool.ResolvedCultureId);
        Assert.AreEqual("taom/battle_music/gondor/battle_b", result.Track.EventName);
        _engine.Received(2).LoadClip(7, Arg.Is<string>(path =>
            EndsWithNormalizedPath(path, "ModuleSounds/taom/battle_music/gondor/battle_a.ogg")));
        _engine.Received(2).UnloadClip(7);
        _engine.Received(1).LoadClip(8, Arg.Is<string>(path =>
            EndsWithNormalizedPath(path, "ModuleSounds/taom/battle_music/gondor/battle_b.ogg")));
        _engine.Received(1).PlayMusic(8);
    }

    [TestMethod]
    public void Update_ReusesActiveTrackUntilRotationBoundary()
    {
        _engine.GetFreeMusicChannelIndex().Returns(7);
        _engine.IsClipLoaded(7).Returns(true);
        _engine.IsMusicPlaying(7).Returns(true);

        var first = _service.Update(Snapshot("gondor", battle: true), MusicRouteSnapshot.Empty, timerSeconds: 0f);
        var second = _service.Update(Snapshot("gondor", battle: true), MusicRouteSnapshot.Empty, timerSeconds: 10f);

        Assert.AreEqual(MusicPlaybackOutcome.Started, first.Outcome);
        Assert.AreEqual(MusicPlaybackOutcome.Continued, second.Outcome);
        _engine.Received(1).LoadClip(7, Arg.Any<string>());
        _engine.Received(1).PlayMusic(7);
    }

    [TestMethod]
    public void Update_ContinuesActiveOwnedTrackWithoutSuppressionOwnerSideEffects()
    {
        _engine.GetFreeMusicChannelIndex().Returns(7);
        _engine.IsClipLoaded(7).Returns(true);
        _engine.IsMusicPlaying(7).Returns(true);

        _service.Update(MusicRouteSnapshot.Empty, Snapshot("gondor", world: true), timerSeconds: 0f);
        _engine.ClearReceivedCalls();

        var result = _service.Update(MusicRouteSnapshot.Empty, Snapshot("gondor", world: true), timerSeconds: 10f);

        Assert.AreEqual(MusicPlaybackOutcome.Continued, result.Outcome);
        _engine.DidNotReceive().GetFreeMusicChannelIndex();
        _engine.DidNotReceive().LoadClip(Arg.Any<int>(), Arg.Any<string>());
        _engine.DidNotReceive().PlayMusic(Arg.Any<int>());
    }

    [TestMethod]
    public void Update_DoesNotRestartOwnedChannelDuringStartGraceWhenEngineReportsNotPlayingYet()
    {
        _engine.GetFreeMusicChannelIndex().Returns(7, 8);
        _engine.IsClipLoaded(7).Returns(true);
        _engine.IsClipLoaded(8).Returns(true);
        _engine.IsMusicPlaying(7).Returns(false);

        var first = _service.Update(MusicRouteSnapshot.Empty, Snapshot("gondor", world: true), timerSeconds: 20f);
        _engine.ClearReceivedCalls();

        var second = _service.Update(MusicRouteSnapshot.Empty, Snapshot("gondor", world: true), timerSeconds: 21f);

        Assert.AreEqual(MusicPlaybackOutcome.Started, first.Outcome);
        Assert.AreEqual(MusicPlaybackOutcome.Continued, second.Outcome);
        Assert.AreEqual(7, second.Channel);
        Assert.AreEqual("taom/worldmap/gondor/world_a", second.Track.EventName);
        _engine.DidNotReceive().StopMusic(7);
        _engine.DidNotReceive().UnloadClip(7);
        _engine.DidNotReceive().LoadClip(Arg.Any<int>(), Arg.Any<string>());
        _engine.DidNotReceive().PlayMusic(Arg.Any<int>());
    }

    [TestMethod]
    public void Update_BacksOffLostOwnedWorldChannelBeforeRestartingSameRoute()
    {
        _engine.GetFreeMusicChannelIndex().Returns(7, 8);
        _engine.IsClipLoaded(7).Returns(true);
        _engine.IsClipLoaded(8).Returns(true);
        _engine.IsMusicPlaying(7).Returns(false);

        var first = _service.Update(MusicRouteSnapshot.Empty, Snapshot("gondor", world: true), timerSeconds: 0f);
        _engine.ClearReceivedCalls();

        var second = _service.Update(MusicRouteSnapshot.Empty, Snapshot("gondor", world: true), timerSeconds: 10f);
        var third = _service.Update(MusicRouteSnapshot.Empty, Snapshot("gondor", world: true), timerSeconds: 10.25f);
        var fourth = _service.Update(MusicRouteSnapshot.Empty, Snapshot("gondor", world: true), timerSeconds: 11.1f);

        Assert.AreEqual(MusicPlaybackOutcome.Started, first.Outcome);
        Assert.AreEqual(MusicPlaybackOutcome.None, second.Outcome);
        Assert.AreEqual("waiting_for_lost_owned_music_channel", second.Reason);
        Assert.AreEqual(MusicPlaybackOutcome.None, third.Outcome);
        Assert.AreEqual("waiting_for_lost_owned_music_channel", third.Reason);
        Assert.AreEqual(MusicPlaybackOutcome.Started, fourth.Outcome);
        Assert.AreEqual(8, fourth.Channel);
        Assert.AreEqual("taom/worldmap/gondor/world_a", fourth.Track.EventName);
        _engine.Received(1).StopMusic(7);
        _engine.Received(1).UnloadClip(7);
        _engine.Received(1).GetFreeMusicChannelIndex();
        _engine.Received(1).LoadClip(8, Arg.Is<string>(path =>
            EndsWithNormalizedPath(path, "ModuleSounds/taom/worldmap/gondor/world_a.ogg")));
        _engine.Received(1).PlayMusic(8);
    }

    [TestMethod]
    public void Update_RestartsWhenOwnedChannelStopsPlayingBeforeRotationBoundary()
    {
        _engine.GetFreeMusicChannelIndex().Returns(7, 8);
        _engine.IsClipLoaded(7).Returns(true);
        _engine.IsClipLoaded(8).Returns(true);
        _engine.IsMusicPlaying(7).Returns(false);

        var first = _service.Update(Snapshot("gondor", battle: true), MusicRouteSnapshot.Empty, timerSeconds: 0f);
        var second = _service.Update(Snapshot("gondor", battle: true), MusicRouteSnapshot.Empty, timerSeconds: 10f);

        Assert.AreEqual(MusicPlaybackOutcome.Started, first.Outcome);
        Assert.AreEqual(MusicPlaybackOutcome.Started, second.Outcome);
        Assert.AreEqual(8, second.Channel);
        Assert.AreEqual("taom/battle_music/gondor/battle_b", second.Track.EventName);
        _engine.Received(1).StopMusic(7);
        _engine.Received(1).UnloadClip(7);
        _engine.Received(1).PlayMusic(8);
    }

    [TestMethod]
    public void Update_RotatesWithinSamePoolAfterRotationBoundary()
    {
        _engine.GetFreeMusicChannelIndex().Returns(7, 8);
        _engine.IsClipLoaded(7).Returns(true);
        _engine.IsClipLoaded(8).Returns(true);

        var first = _service.Update(Snapshot("gondor", battle: true), MusicRouteSnapshot.Empty, timerSeconds: 0f);
        var second = _service.Update(Snapshot("gondor", battle: true), MusicRouteSnapshot.Empty, timerSeconds: 181f);

        Assert.AreEqual(MusicPlaybackOutcome.Started, first.Outcome);
        Assert.AreEqual(MusicPlaybackOutcome.Started, second.Outcome);
        Assert.AreEqual("taom/battle_music/gondor/battle_b", second.Track.EventName);
        _engine.Received(1).StopMusic(7);
        _engine.Received(1).UnloadClip(7);
        _engine.Received(1).PlayMusic(8);
    }

    [TestMethod]
    public void Update_StopsOwnedChannelWhenMusicBecomesDisabled()
    {
        _engine.GetFreeMusicChannelIndex().Returns(7);
        _engine.IsClipLoaded(7).Returns(true);
        _engine.IsMusicPlaying(7).Returns(true);

        _service.Update(Snapshot("gondor", battle: true), MusicRouteSnapshot.Empty, timerSeconds: 0f);
        _settings.GetSnapshot().Returns(Settings(musicEnabled: false));

        var result = _service.Update(Snapshot("gondor", battle: true), MusicRouteSnapshot.Empty, timerSeconds: 1f);

        Assert.AreEqual(MusicPlaybackOutcome.Stopped, result.Outcome);
        Assert.AreEqual("music_disabled", result.Reason);
        _engine.Received(1).StopMusic(7);
        _engine.Received(1).UnloadClip(7);
    }

    [TestMethod]
    public void IsActiveBucket_ReturnsOnlyCurrentOwnedBucket()
    {
        Assert.IsFalse(_service.IsActiveBucket(MusicBucket.Tavern));

        _engine.GetFreeMusicChannelIndex().Returns(7);
        _engine.IsClipLoaded(7).Returns(true);

        _service.Update(Snapshot("gondor", tavern: true), MusicRouteSnapshot.Empty, timerSeconds: 0f);

        Assert.IsTrue(_service.IsActiveBucket(MusicBucket.Tavern));
        Assert.IsFalse(_service.IsActiveBucket(MusicBucket.Battle));

        _service.Stop("test");

        Assert.IsFalse(_service.IsActiveBucket(MusicBucket.Tavern));
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

    private static MusicSettingsSnapshot Settings(bool musicEnabled = true, bool useNoRepeatShuffle = false)
    {
        return new MusicSettingsSnapshot(
            musicEnabled: musicEnabled,
            campaignContextEnabled: true,
            missionContextEnabled: true,
            routeSettings: MusicRouteSettings.AllEnabled,
            rotation: new MusicRotationPolicy.RotationSnapshot(
                musicEnabled: musicEnabled,
                enableWorldRotation: true,
                enableTownRotation: true,
                enableBattleRotation: true,
                worldRotateIntervalSeconds: 180f,
                townRotateIntervalSeconds: 180f,
                battleRotateIntervalSeconds: 180f,
                characterCreationRotateIntervalSeconds: 180f),
            useNoRepeatShuffle: useNoRepeatShuffle,
            noRepeatHistorySize: 8,
            masterVolume: 0.5f,
            worldVolume: 1f,
            townVolume: 1f,
            tavernVolume: 1f,
            battleVolume: 0.25f,
            siegeVolume: 1f);
    }

    private static MusicTrackIndex Index()
    {
        return MusicTrackIndex.LoadFromModuleSoundDocuments(
            ModuleRoot,
            new[] { XDocument.Parse(Xml()) });
    }

    private static bool EndsWithNormalizedPath(string path, string expectedSuffix)
    {
        return NormalizePath(path).EndsWith(NormalizePath(expectedSuffix), StringComparison.OrdinalIgnoreCase);
    }

    private static string NormalizePath(string path)
    {
        return (path ?? string.Empty).Replace('\\', '/');
    }

    private static string Xml()
    {
        return @"<?xml version=""1.0"" encoding=""utf-8""?>
<base type=""module_sound"">
  <module_sounds>
    <module_sound name=""taom/battle_music/gondor/battle_a"" is_2d=""true"" sound_category=""music"" path=""taom/battle_music/gondor/battle_a.ogg"" />
    <module_sound name=""taom/battle_music/gondor/battle_b"" is_2d=""true"" sound_category=""music"" path=""taom/battle_music/gondor/battle_b.ogg"" />
    <module_sound name=""taom/tavern_wander/gondor/tavern_a"" is_2d=""true"" sound_category=""music"" path=""taom/tavern_wander/gondor/tavern_a.ogg"" />
    <module_sound name=""taom/town_wander/gondor/town_a"" is_2d=""true"" sound_category=""music"" path=""taom/town_wander/gondor/town_a.ogg"" />
    <module_sound name=""taom/worldmap/gondor/world_a"" is_2d=""true"" sound_category=""music"" path=""taom/worldmap/gondor/world_a.ogg"" />
  </module_sounds>
</base>";
    }
}
