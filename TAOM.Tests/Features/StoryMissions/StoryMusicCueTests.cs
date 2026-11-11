using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using TAOM.Adapters;
using TAOM.Core.Logging;
using TAOM.Features.StoryMissions;

namespace TAOM.Tests.Features.StoryMissions;

/// <summary>
/// The story mission's own track (#1105): vanilla music goes quiet only after the engine accepted the track, and comes back as soon as the track
/// can no longer play, or on the first of the two teardown hooks, whatever else fails. Nothing here touches music outside the mission that built
/// the cue.
/// </summary>
[TestClass]
public class StoryMusicCueTests
{
    private const string Path = "M:/Modules/TAOM/ModuleSounds/StoryMusic/track.ogg";

    private IMissionMusicAdapter _music = null!;
    private IOutcomeLog _outcomes = null!;
    private IModLogger _logger = null!;
    private bool _exists;

    [TestInitialize]
    public void SetUp()
    {
        _music = Substitute.For<IMissionMusicAdapter>();
        _music.TryPlay(Path).Returns(true);
        _music.Silence().Returns(true);
        _outcomes = Substitute.For<IOutcomeLog>();
        _logger = Substitute.For<IModLogger>();
        _exists = true;
    }

    private StoryMusicCue Cue() => new(_music, Path, _ => _exists, _logger, _outcomes, "story1");

    private StoryMusicCue Started()
    {
        var cue = Cue();
        cue.Start();
        _music.ClearReceivedCalls();
        _outcomes.ClearReceivedCalls();
        return cue;
    }

    // A full play, then the engine reports the event stopped: the next tick is the end of the track.
    private StoryMusicCue PlayedToTheEnd()
    {
        var cue = Started();
        cue.Tick(StoryMusicCue.MinimumPlaySeconds + 1f);
        _music.IsStopped.Returns(true);
        return cue;
    }

    private static string Has(string fragment) => Arg.Is<string>(s => s.Contains(fragment));

    [TestMethod]
    public void Start_PlaysTheTrack_ThenSilencesVanillaMusic_AndWritesOneRequestedPass()
    {
        Cue().Start();

        Received.InOrder(() =>
        {
            _music.TryPlay(Path);
            _music.Silence();
        });
        _outcomes.Received(1).Pass(StoryMusicCue.RequestedId, "story1", Has("silenced=True"));
    }

    [TestMethod]
    public void Start_AMissingFile_WritesAFail_AndNeverPlaysOrSilences()
    {
        _exists = false;

        Cue().Start();

        _music.DidNotReceiveWithAnyArgs().TryPlay(default!);
        _music.DidNotReceive().Silence();
        _outcomes.Received(1).Fail(StoryMusicCue.RequestedId, "story1", Has("reason=missing-file"));
    }

    [TestMethod]
    public void Start_APlayTheEngineRefuses_WritesAFail_DropsTheEvent_AndNeverSilences()
    {
        // The adapter may hold an event that the engine created but never started.
        _music.TryPlay(Path).Returns(false);

        Cue().Start();

        _music.DidNotReceive().Silence();
        _music.Received(1).Stop();
        _outcomes.Received(1).Fail(StoryMusicCue.RequestedId, "story1", Has("reason=play-refused"));
    }

    [TestMethod]
    public void Start_APlayThatThrows_WritesAFault_DropsTheEvent_AndNeverSilences()
    {
        _music.TryPlay(Path).Throws(new InvalidOperationException("native"));

        Cue().Start();

        _music.DidNotReceive().Silence();
        _music.Received(1).Stop();
        _outcomes.Received(1).Fail(StoryMusicCue.RequestedId, "story1", Has("reason=fault"));
    }

    [TestMethod]
    public void Start_ADropThatThrows_StillWritesTheRequestedFail()
    {
        _music.TryPlay(Path).Returns(false);
        _music.When(m => m.Stop()).Throw(new InvalidOperationException("native"));

        Cue().Start();

        _outcomes.Received(1).Swallowed("StoryMissions", "music-stop", Arg.Any<Exception>());
        _outcomes.Received(1).Fail(StoryMusicCue.RequestedId, "story1", Has("reason=play-refused"));
    }

    [TestMethod]
    public void Start_Twice_PlaysOnce()
    {
        var cue = Cue();

        cue.Start();
        cue.Start();

        _music.Received(1).TryPlay(Path);
    }

    [TestMethod]
    public void Start_WithNoMusicManager_KeepsTheTrack_AndReleaseUnsilencesNothing()
    {
        _music.Silence().Returns(false);
        var cue = Cue();

        cue.Start();
        cue.Release("screen-finalize");

        _outcomes.Received(1).Pass(StoryMusicCue.RequestedId, "story1", Has("silenced=False"));
        _music.DidNotReceive().Unsilence();
        _music.Received(1).Stop();
    }

    [TestMethod]
    public void Start_ASilenceThatThrowsPartWay_IsStillUndoneByRelease()
    {
        // DeactivateCurrentMode may have run before the throw, so the cue counts itself as silencing before it asks.
        _music.Silence().Throws(new InvalidOperationException("half way"));
        var cue = Cue();

        cue.Start();
        cue.Release("screen-finalize");

        _music.Received(1).Unsilence();
    }

    [TestMethod]
    public void Release_BeforeStart_StopsAndUnsilencesNothing_AndALateStartPlaysNothing()
    {
        var cue = Cue();

        cue.Release("remove-behavior");
        cue.Start();

        _music.DidNotReceive().Stop();
        _music.DidNotReceive().Unsilence();
        _music.DidNotReceiveWithAnyArgs().TryPlay(default!);
        _outcomes.Received(1).Pass(StoryMusicCue.StoppedId, "story1", Has("played=False"));
    }

    [TestMethod]
    public void Release_GivesVanillaMusicBackFirst_ThenStopsTheTrack()
    {
        var cue = Started();

        cue.Release("screen-finalize");

        Received.InOrder(() =>
        {
            _music.Unsilence();
            _music.Stop();
        });
        _outcomes.Received(1).Pass(StoryMusicCue.StoppedId, "story1", Has("via=screen-finalize"));
    }

    [TestMethod]
    public void Release_FromBothTeardownHooks_UndoesOnce_AndWritesOneLine()
    {
        var cue = Started();

        cue.Release("screen-finalize");
        cue.Release("remove-behavior");

        _music.Received(1).Unsilence();
        _music.Received(1).Stop();
        _outcomes.Received(1).Pass(StoryMusicCue.StoppedId, "story1", Arg.Any<string>());
    }

    [TestMethod]
    public void Release_AStopThatThrows_StillGivesVanillaMusicBack_AndCountsTheFault()
    {
        _music.When(m => m.Stop()).Throw(new InvalidOperationException("native"));
        var cue = Started();

        cue.Release("screen-finalize");

        _music.Received(1).Unsilence();
        _outcomes.Received(1).Swallowed("StoryMissions", "music-stop", Arg.Any<Exception>());
    }

    [TestMethod]
    public void Release_AnUnsilenceThatThrows_StillStopsTheTrack()
    {
        _music.When(m => m.Unsilence()).Throw(new InvalidOperationException("native"));
        var cue = Started();

        cue.Release("screen-finalize");

        _music.Received(1).Stop();
        _outcomes.Received(1).Swallowed("StoryMissions", "music-unsilence", Arg.Any<Exception>());
    }

    [TestMethod]
    public void Tick_WhileTheTrackPlays_DoesNothing()
    {
        var cue = Started();
        _music.IsStopped.Returns(false);

        cue.Tick(30f);

        _music.DidNotReceiveWithAnyArgs().TryPlay(default!);
    }

    [TestMethod]
    public void Tick_AfterTheTrackEnds_PlaysTheFileAgainOnANewEvent()
    {
        // Native releases an event that ends by itself, so a second Play on the old event fails, or plays nothing before the release: the loop asks the adapter for a new one.
        var cue = PlayedToTheEnd();

        cue.Tick(0.016f);

        _music.Received(1).TryPlay(Path);
        _music.DidNotReceive().Unsilence();
    }

    [TestMethod]
    public void Tick_ATrackThatEndsTooSoon_IsNotPlayedAgain_GivesVanillaMusicBackAtOnce_AndTheStoppedLineFails()
    {
        // A file that did not decode can end at once; a replay on every frame would decode it again on every frame.
        var cue = Started();
        _music.IsStopped.Returns(true);

        cue.Tick(1f);
        _music.Received(1).Unsilence();
        cue.Tick(StoryMusicCue.MinimumPlaySeconds + 1f);
        cue.Release("screen-finalize");

        _music.DidNotReceiveWithAnyArgs().TryPlay(default!);
        _music.Received(1).Unsilence();
        _outcomes.Received(1).Fail(StoryMusicCue.StoppedId, "story1", Has("loop=early"));
        _outcomes.DidNotReceiveWithAnyArgs().Pass(StoryMusicCue.StoppedId, default!, default!);
    }

    [DataTestMethod]
    [DataRow(float.NaN)]
    [DataRow(float.PositiveInfinity)]
    public void Tick_ABadFrameTime_DoesNotCountTowardTheMinimumPlay(float dt)
    {
        var cue = Started();

        cue.Tick(dt);
        _music.IsStopped.Returns(true);
        cue.Tick(0.016f);

        _music.DidNotReceiveWithAnyArgs().TryPlay(default!);
    }

    [TestMethod]
    public void Tick_ANegativeFrameTime_TakesNoTimeOffAFullPlay()
    {
        var cue = Started();

        cue.Tick(-20f);
        cue.Tick(StoryMusicCue.MinimumPlaySeconds + 1f);
        _music.IsStopped.Returns(true);
        cue.Tick(0.016f);

        _music.Received(1).TryPlay(Path);
    }

    [TestMethod]
    public void Tick_AReplayThatEndsTooSoon_IsNotPlayedAgain_AndTheStoppedLineFails()
    {
        // Each replay measures its own play: a counter carried over from the first play would replay a file that no longer decodes on every frame.
        var cue = PlayedToTheEnd();
        cue.Tick(0.016f);

        cue.Tick(0.016f);
        cue.Release("screen-finalize");

        _music.Received(1).TryPlay(Path);
        _music.Received(1).Unsilence();
        _outcomes.Received(1).Fail(StoryMusicCue.StoppedId, "story1", Arg.Is<string>(s => s.Contains("loop=early") && s.Contains("restarts=1")));
    }

    [TestMethod]
    public void Tick_AReplayTheEngineRefuses_EndsTheLoop_AndGivesVanillaMusicBackAtOnce()
    {
        var cue = PlayedToTheEnd();
        _music.TryPlay(Path).Returns(false);

        cue.Tick(0.016f);
        _music.Received(1).Unsilence();
        cue.Tick(StoryMusicCue.MinimumPlaySeconds + 1f);

        _music.Received(1).TryPlay(Path);
    }

    [TestMethod]
    public void Release_AfterARefusedReplay_StopsTheTrack_UnsilencesNoSecondTime_AndTheStoppedLineFails()
    {
        var cue = PlayedToTheEnd();
        _music.TryPlay(Path).Returns(false);
        cue.Tick(0.016f);

        cue.Release("screen-finalize");

        _music.Received(1).Stop();
        _music.Received(1).Unsilence();
        _outcomes.Received(1).Fail(StoryMusicCue.StoppedId, "story1", Arg.Is<string>(s => s.Contains("played=True") && s.Contains("loop=refused")));
    }

    [TestMethod]
    public void Tick_AfterRelease_NeverPlaysAgain()
    {
        var cue = Started();
        cue.Tick(StoryMusicCue.MinimumPlaySeconds + 1f);
        cue.Release("screen-finalize");
        _music.IsStopped.Returns(true);

        cue.Tick(0.016f);

        _music.DidNotReceiveWithAnyArgs().TryPlay(default!);
    }

    [TestMethod]
    public void Tick_WhenNothingPlays_AsksTheEngineNothing()
    {
        _exists = false;
        var cue = Cue();
        cue.Start();

        cue.Tick(StoryMusicCue.MinimumPlaySeconds + 1f);

        _ = _music.DidNotReceive().IsStopped;
        _music.DidNotReceiveWithAnyArgs().TryPlay(default!);
    }

    [TestMethod]
    public void Tick_AnEngineThatThrows_EndsTheLoop_GivesVanillaMusicBackAtOnce_AndNeverThrowsIntoTheScreen()
    {
        var cue = Started();
        _music.IsStopped.Throws(new InvalidOperationException("native"));

        cue.Tick(0.016f);
        cue.Tick(0.016f);
        cue.Release("screen-finalize");

        _outcomes.Received(1).Swallowed("StoryMissions", "music-loop", Arg.Any<Exception>());
        _music.Received(1).Unsilence();
        _outcomes.Received(1).Fail(StoryMusicCue.StoppedId, "story1", Has("loop=fault"));
    }

    [TestMethod]
    public void TheStoppedLine_CountsTheReplays()
    {
        var cue = PlayedToTheEnd();
        cue.Tick(0.016f);

        cue.Release("remove-behavior");

        _outcomes.Received(1).Pass(StoryMusicCue.StoppedId, "story1", "via=remove-behavior played=True restarts=1 loop=ok");
    }
}
