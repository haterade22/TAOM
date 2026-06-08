using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using System.Collections.Generic;
using System.Linq;
using TAOM.Features.Music;
using TAOM.Features.Music.Hooks;

namespace TAOM.Tests.Features.Music;

[TestClass]
public class CharacterCreationMusicScreenPatchHelperTests
{
    [TestCleanup]
    public void Cleanup()
    {
        CharacterCreationMusicScreenPatchHelper.ResetForTests();
        CharacterCreationMusicSmokeTrace.ResetForTests();
    }

    [TestMethod]
    public void FirstFrame_EntersCharacterCreationAndFeedsSnapshotIntoPlayback()
    {
        var context = Substitute.For<ICharacterCreationMusicContextService>();
        var playback = Substitute.For<IMusicPlaybackService>();
        var snapshot = Snapshot("gondor");
        context.CaptureSnapshot().Returns(snapshot);
        CharacterCreationMusicScreenPatchHelper.InitializeForTests(context, playback);

        CharacterCreationMusicScreenPatchHelper.OnFrameTick(0.5f);

        context.Received(1).EnterCharacterCreation();
        playback.Received(1).Update(MusicRouteSnapshot.Empty, snapshot, 0.5f);
    }

    [TestMethod]
    public void FrameTick_SmokeTraceOrdersTaomCharacterCreationStartBeforeVanillaAmbientStop()
    {
        var screen = new object();
        var events = new List<string>();
        var context = Substitute.For<ICharacterCreationMusicContextService>();
        var playback = Substitute.For<IMusicPlaybackService>();
        var snapshot = Snapshot("gondor");
        var result = Started(MusicBucket.CharacterCreation);
        context.CaptureSnapshot().Returns(snapshot);
        playback.Update(MusicRouteSnapshot.Empty, snapshot, 1f).Returns(result);
        CharacterCreationMusicSmokeTrace.InitializeForTests(message => events.Add("log:" + message));
        CharacterCreationMusicScreenPatchHelper.InitializeForTests(context, playback, instance =>
        {
            events.Add("ambient_stop");
            return ReferenceEquals(screen, instance);
        });

        CharacterCreationMusicScreenPatchHelper.OnFrameTick(screen, 1f);
        CharacterCreationMusicScreenPatchHelper.OnFrameTick(screen, 1f);

        Assert.AreEqual(3, events.Count, string.Join(" | ", events));
        StringAssert.Contains(events[0], "cc_bucket_owned");
        StringAssert.Contains(events[0], "outcome=Started");
        Assert.AreEqual("ambient_stop", events[1]);
        StringAssert.Contains(events[2], "vanilla_ambient_suppressed");
    }

    [TestMethod]
    public void FrameTick_DoesNotSuppressVanillaAmbientWhenTaomCharacterCreationTrackFails()
    {
        var suppressed = new List<object>();
        var messages = new List<string>();
        var context = Substitute.For<ICharacterCreationMusicContextService>();
        var playback = Substitute.For<IMusicPlaybackService>();
        var snapshot = Snapshot("gondor");
        context.CaptureSnapshot().Returns(snapshot);
        playback.Update(MusicRouteSnapshot.Empty, snapshot, 1f)
            .Returns(MusicPlaybackResult.Failed("no_track", decision: Decision(MusicBucket.CharacterCreation)));
        CharacterCreationMusicSmokeTrace.InitializeForTests(messages.Add);
        CharacterCreationMusicScreenPatchHelper.InitializeForTests(context, playback, instance =>
        {
            suppressed.Add(instance);
            return true;
        });

        CharacterCreationMusicScreenPatchHelper.OnFrameTick(new object(), 1f);

        Assert.AreEqual(0, suppressed.Count);
        Assert.IsFalse(messages.Any(message => message.Contains("vanilla_ambient_suppressed")));
    }

    [TestMethod]
    public void LaterFrames_DoNotReenterAndAccumulateSanitizedTime()
    {
        var context = Substitute.For<ICharacterCreationMusicContextService>();
        var playback = Substitute.For<IMusicPlaybackService>();
        var snapshot = Snapshot("rohan");
        context.CaptureSnapshot().Returns(snapshot);
        CharacterCreationMusicScreenPatchHelper.InitializeForTests(context, playback);

        CharacterCreationMusicScreenPatchHelper.OnFrameTick(float.NaN);
        CharacterCreationMusicScreenPatchHelper.OnFrameTick(-2f);
        CharacterCreationMusicScreenPatchHelper.OnFrameTick(0.25f);

        context.Received(1).EnterCharacterCreation();
        playback.Received(2).Update(MusicRouteSnapshot.Empty, snapshot, 0f);
        playback.Received(1).Update(MusicRouteSnapshot.Empty, snapshot, 0.25f);
    }

    [TestMethod]
    public void Finalize_ExitsCharacterCreationStopsPlaybackAndAllowsNextEnter()
    {
        var context = Substitute.For<ICharacterCreationMusicContextService>();
        var playback = Substitute.For<IMusicPlaybackService>();
        context.CaptureSnapshot().Returns(Snapshot("gondor"));
        CharacterCreationMusicScreenPatchHelper.InitializeForTests(context, playback);

        CharacterCreationMusicScreenPatchHelper.OnFrameTick(1f);
        CharacterCreationMusicScreenPatchHelper.OnFinalize();
        CharacterCreationMusicScreenPatchHelper.OnFrameTick(1f);

        context.Received(2).EnterCharacterCreation();
        context.Received(1).ExitCharacterCreation("character_creation_screen_finalized");
        playback.Received(1).Stop("character_creation_screen_finalized");
    }

    private static MusicRouteSnapshot Snapshot(string cultureId)
    {
        return new MusicRouteSnapshot(
            true,
            cultureId,
            false,
            false,
            false,
            false,
            false,
            true,
            "character_creation");
    }

    private static MusicPlaybackResult Started(MusicBucket bucket)
    {
        var track = new MusicTrackDefinition(bucket, "gondor", "event:/taom/test", "taom/test.ogg", "D:\\taom\\test.ogg");
        return MusicPlaybackResult.Started(track, Decision(bucket), 0);
    }

    private static MusicRouteDecision Decision(MusicBucket bucket)
    {
        var track = new MusicTrackDefinition(bucket, "gondor", "event:/taom/test", "taom/test.ogg", "D:\\taom\\test.ogg");
        var pool = new MusicTrackPool(bucket, "gondor", "gondor", new[] { track });
        return MusicRouteDecision.Selected(MusicRouteSource.Campaign, bucket, pool, "test");
    }
}
