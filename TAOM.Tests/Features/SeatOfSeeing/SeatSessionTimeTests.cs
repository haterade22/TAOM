using System;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Client.Features.SeatOfSeeing;
using TAOM.Core.Logging;
using TAOM.Features.SeatOfSeeing;
using TAOM.Features.SeatOfSeeing.Domain;

namespace TAOM.Tests.Features.SeatOfSeeing;

/// <summary>
/// Mission time from the session (build-spec section 10), against a stand-in that keeps the engine's flags the way the engine does: the ladder, the
/// only legal write of the request, the per-frame rule, the gates, the two hand-offs and both teardown paths.
/// </summary>
[TestClass]
public class SeatSessionTimeTests
{
    private const float Frame = 1f / 60f;
    private static readonly SeatPermissions Allowed = new(Camera: true, FightOn: true, Orders: true, Time: true, TakeSoldier: true);

    private FakeSeatScreen _screen = null!;
    private FakeSeatHero _hero = null!;
    private FakeSeatSettings _settings = null!;
    private SeatPermissions _permissions;
    private RecordingOutcomeLog _log = null!;
    private SeatSession _sut = null!;

    [TestInitialize]
    public void Setup()
    {
        SeatPresence.Set(false);
        _screen = new FakeSeatScreen();
        _hero = new FakeSeatHero();
        _settings = new FakeSeatSettings();
        _permissions = Allowed;
        _log = new RecordingOutcomeLog();
        _sut = new SeatSession(_screen, _hero, _settings, () => _permissions, _log, Substitute.For<IModLogger>(), "7");
    }

    [TestCleanup]
    public void Cleanup() => SeatPresence.Set(false);

    private void Press(SeatTimeKey key, int times = 1)
    {
        for (var i = 0; i < times; i++)
            _sut.PressTime(key);
    }

    private void Frames(int count = 3)
    {
        for (var i = 0; i < count; i++)
            _sut.OnFrameTick(Frame);
    }

    private void Seated()
    {
        _sut.OnScreenTick(true);
        for (var i = 0; i < 40 && _sut.State != SeatState.Seated; i++)
            _sut.OnFrame(0.1f);
        Assert.AreEqual(SeatState.Seated, _sut.State);
    }

    private string[] Lines() => _log.Lines.Where(l => l.Id == SeatChecks.TimeId).Select(l => l.Result + " " + l.Detail).ToArray();

    // ---- The ladder ----

    [TestMethod]
    public void Quicken_FromNormal_WritesTheFixedStepOfTwo_SaysTheLevel_AndWritesTheLine()
    {
        Press(SeatTimeKey.Quicken);
        Frames();

        Assert.IsTrue(_screen.FixedStep, "the fixed step is on");
        Assert.IsFalse(_screen.FastForward);
        Assert.AreEqual(SeatMessage.TimeLevel, _screen.Messages.Single());
        Assert.AreEqual("2", _screen.MessageArguments.Single());
        CollectionAssert.AreEqual(new[] { "pass mission=7 level=2 event=level" }, Lines());
    }

    [TestMethod]
    public void Quicken_WithTheSeatUp_ReachesTenAsTheEnginesFastForward_AndNeverWritesAFixedStep()
    {
        Seated();
        Press(SeatTimeKey.Quicken, 4);
        Frames();

        Assert.IsTrue(_screen.FastForward);
        Assert.IsFalse(_screen.FixedStep);
        CollectionAssert.AreEqual(new[] { "2", "4", "10", "10" }, _screen.MessageArguments.Where(a => a != null).ToArray(), "a fourth press at the top says the same level");
    }

    [TestMethod]
    public void Quicken_WithTheHeroAliveAndTheSeatDown_StopsAtFour()
    {
        Press(SeatTimeKey.Quicken, 5);
        Frames();

        Assert.IsFalse(_screen.FastForward, "fast forward repeats the mission tick, and a key read in each could act more than once");
        Assert.IsTrue(_screen.FixedStep);
        Assert.AreEqual("4", _screen.MessageArguments.Last());
    }

    [TestMethod]
    public void Quicken_WithNoMainAgent_ReachesTenWithTheSeatDown()
    {
        _hero.Active = false;

        Press(SeatTimeKey.Quicken, 4);

        Assert.IsTrue(_screen.FastForward);
    }

    [TestMethod]
    public void Slow_FromNormal_AddsOneRequestAtHalf_AndAnotherStepReplacesItByTheOnlyLegalWrite()
    {
        Press(SeatTimeKey.Slow);
        Press(SeatTimeKey.Slow);

        CollectionAssert.AreEqual(new[] { "stop", "add:0.5", "remove", "stop", "add:0.25" }, _screen.TimeWrites.ToArray());
        CollectionAssert.AreEqual(new[] { 0.25f }, _screen.SlowRequests, "one request, never two (adding does not replace)");
    }

    [TestMethod]
    public void Quicken_FromASlowLevel_RemovesTheRequestOnce_AndNeverRemovesAnAbsentId()
    {
        Press(SeatTimeKey.Slow);
        Press(SeatTimeKey.Quicken);
        Press(SeatTimeKey.Quicken);
        Press(SeatTimeKey.Slow, 3);

        Assert.AreEqual(1, _screen.SlowRequests.Count);
        Assert.AreEqual(2, _screen.TimeWrites.Count(w => w == "remove"), "once at the step from 0.5 up to 1 and once at the step from 0.5 down to 0.25, and never when none is held");
    }

    [TestMethod]
    public void Press_WithNoRequestPresent_NeverCallsRemove()
    {
        Press(SeatTimeKey.Quicken);
        Press(SeatTimeKey.Slow);
        Press(SeatTimeKey.Slow);

        Assert.AreEqual(0, _screen.TimeWrites.Count(w => w == "remove"), "removing an absent id throws (Mission.cs:2115-2125), so the guard reads first");

        Press(SeatTimeKey.Slow);

        Assert.AreEqual(1, _screen.TimeWrites.Count(w => w == "remove"), "the fourth press found the half-speed request and replaced it");
    }

    [TestMethod]
    public void EachMode_CancelsTheOtherBeforeItStarts()
    {
        Press(SeatTimeKey.Quicken, 2);
        Frames();
        Assert.IsTrue(_screen.FixedStep);

        Press(SeatTimeKey.Slow, 3);
        Assert.IsFalse(_screen.FixedStep, "the fixed step is off before the slow request is added");
        Assert.AreEqual(1, _screen.SlowRequests.Count);

        Press(SeatTimeKey.Quicken, 2);
        Frames();
        Assert.AreEqual(0, _screen.SlowRequests.Count, "the request is cleared before the fixed step starts");
        Assert.IsTrue(_screen.FixedStep);
    }

    // ---- The per-frame rule ----

    [TestMethod]
    public void BeforeAnyTimeKey_NoFrameReadsOrWritesTime()
    {
        Frames(30);

        Assert.AreEqual(0, _screen.TimeReads);
        Assert.AreEqual(0, _screen.TimeWrites.Count);
    }

    [TestMethod]
    public void AtTwoAndFour_EachFrameWritesTheFixedStepAgain()
    {
        Press(SeatTimeKey.Quicken);
        var before = _screen.FastApplies;

        Frames(5);

        Assert.AreEqual(before + 5, _screen.FastApplies);
        Assert.AreEqual(Frame, _screen.LastFastDt, 0.00001f);
    }

    [TestMethod]
    public void AtFour_WhenAnotherWriterRaisesTheFlag_TheFixedStepStopsAtOnce_AndTheirFlagStays()
    {
        Press(SeatTimeKey.Quicken, 2);
        Frames(2);
        Assert.IsTrue(_screen.FixedStep, "the control: the Seat's own step was on");
        var stops = _screen.Stops;
        _screen.FastForward = true;

        Frames(3);

        Assert.IsFalse(_screen.FixedStep, "a fixed step under the engine's fast forward would make ten steps of it a frame");
        Assert.IsTrue(_screen.FastForward, "the other writer's flag is not the Seat's to clear");
        Assert.AreEqual(stops, _screen.Stops, "the stop would have cleared their flag");
        Assert.AreEqual(1, _screen.TimeWrites.Count(w => w == "apply:10"), "one write, and no write on the frames that follow");
    }

    [TestMethod]
    public void AtFour_WhenAnotherWriterRaisesTheFlagAndThenLowersIt_NothingOfTheSeatsIsLeftOn()
    {
        Press(SeatTimeKey.Quicken, 2);
        Frames(2);
        _screen.FastForward = true;
        Frames(2);
        _screen.FastForward = false;

        Frames(3);

        Assert.IsFalse(_screen.FixedStep);
        Assert.IsFalse(_screen.FastForward);
        Press(SeatTimeKey.Quicken);
        Assert.AreEqual("2", _screen.MessageArguments.Last(), "the stored level fell to one");
    }

    [TestMethod]
    public void AtHalf_WhenAnotherWriterRaisesTheFlag_TheRequestIsClearedAtOnce_AndNothingIsHeldWhenTheFlagFalls()
    {
        Press(SeatTimeKey.Slow);
        Assert.AreEqual(1, _screen.SlowRequests.Count, "the control: the Seat's request was held");
        _screen.FastForward = true;

        Frames(2);

        Assert.AreEqual(0, _screen.SlowRequests.Count, "a request under the fast forward would make it a fifth of its speed");
        Assert.IsTrue(_screen.FastForward, "the other writer's flag stays");
        _screen.FastForward = false;
        Frames(3);

        Assert.AreEqual(0, _screen.SlowRequests.Count, "and none comes back with the flag's fall");
        Press(SeatTimeKey.Quicken);
        Assert.AreEqual("2", _screen.MessageArguments.Last(), "the stored level is one, so a step up is two");
    }

    [TestMethod]
    public void TheClockOwnerIsLookedUpAfterTheFirstKey_AndOnlyWhileTheFlagIsUp()
    {
        Frames(10);
        Assert.AreEqual(0, _screen.OwnerReads, "before any key nothing is read");

        Press(SeatTimeKey.Slow);
        var afterTheKey = _screen.OwnerReads;
        Frames(10);
        Assert.AreEqual(afterTheKey, _screen.OwnerReads, "a lowered flag asks nobody who owns the clock");

        _screen.FastForward = true;
        Frames(3);
        Assert.AreEqual(afterTheKey + 3, _screen.OwnerReads, "a raised flag asks once a frame");
    }

    [TestMethod]
    public void AKeyThatFindsAnotherWritersFlag_GivesUpTheRequestBeforeItActs()
    {
        Press(SeatTimeKey.Slow);
        _screen.FastForward = true;

        Press(SeatTimeKey.Stay);

        Assert.AreEqual(0, _screen.SlowRequests.Count, "the key's own read gives up what the per-frame rule would");
    }

    [TestMethod]
    public void AKeyThatFindsAnotherWritersFlagAtFour_StopsTheFixedStepBeforeThePause()
    {
        Press(SeatTimeKey.Quicken, 2);
        Frames(2);
        _screen.FastForward = true;

        Press(SeatTimeKey.Stay);

        CollectionAssert.AreEqual(new[] { "apply:10", "stop", "pause" }, _screen.TimeWrites.Skip(_screen.TimeWrites.Count - 3).ToArray(),
            "the key's own read stops the step first, and the pause then cancels fast time as it always does");
    }

    [TestMethod]
    public void AtTen_WhenSomeoneCancelsTheFlag_TheLevelBecomesOneAndFastTimeStopsOnce()
    {
        Seated();
        Press(SeatTimeKey.Quicken, 4);
        _screen.FastForward = false;
        var stops = _screen.Stops;

        Frames(5);

        Assert.AreEqual(stops + 1, _screen.Stops, "stopped once, not every frame");
        Press(SeatTimeKey.Quicken);
        Assert.AreEqual("2", _screen.MessageArguments.Last(), "the stored level is one, so a step up is two");
    }

    [TestMethod]
    public void AtTen_EachFrameWritesNothing_BecauseTheFlagIsSetOnlyOnTheKeyStep()
    {
        Seated();
        Press(SeatTimeKey.Quicken, 4);
        var writes = _screen.TimeWrites.Count;

        Frames(10);

        Assert.AreEqual(writes, _screen.TimeWrites.Count);
    }

    [TestMethod]
    public void AtOneOrBelow_WhenTheFlagIsUp_TheLevelBecomesTen()
    {
        Press(SeatTimeKey.Slow);
        _screen.FastForward = true;
        Frames();

        Seated();
        Press(SeatTimeKey.Slow);

        Assert.AreEqual("4", _screen.MessageArguments.Last(), "a step down from ten");
    }

    [TestMethod]
    public void AKeyStep_ReadsTheFlagBeforeItSteps()
    {
        Seated();
        Press(SeatTimeKey.Slow);
        _screen.FastForward = true;

        Press(SeatTimeKey.Slow);

        Assert.AreEqual("4", _screen.MessageArguments.Last(), "the flag was up, so the ladder started at ten and not at a half");
    }

    // ---- Pause ----

    [TestMethod]
    public void Stay_CancelsFastTimeThenPauses_AndAgainResumesAtOne()
    {
        Press(SeatTimeKey.Quicken, 2);
        Frames();

        Press(SeatTimeKey.Stay);

        Assert.IsTrue(_screen.Paused);
        Assert.IsFalse(_screen.FixedStep);
        Assert.IsFalse(_screen.FastForward);
        CollectionAssert.AreEqual(new[] { "stop", "pause" }, _screen.TimeWrites.Skip(_screen.TimeWrites.Count - 2).ToArray(), "fast time is cancelled before the flag flips");

        Press(SeatTimeKey.Stay);
        Frames();

        Assert.IsFalse(_screen.Paused);
        Assert.IsFalse(_screen.FixedStep, "the level after a pause is one, not the old four");
        CollectionAssert.AreEqual(new[] { SeatMessage.TimeLevel, SeatMessage.TimeLevel, SeatMessage.TimeStayed, SeatMessage.TimeResumed }, _screen.Messages.ToArray());
        CollectionAssert.AreEqual(new[] { "pass mission=7 level=2 event=level", "pass mission=7 level=4 event=level", "pass mission=7 level=1 event=stay", "pass mission=7 level=1 event=resume" }, Lines());
    }

    [TestMethod]
    public void Stay_WhenAnotherWriterAlreadyPaused_ResumesIt()
    {
        _screen.Paused = true;

        Press(SeatTimeKey.Stay);

        Assert.IsFalse(_screen.Paused, "the flag is flipped from what the engine holds");
        Assert.AreEqual(SeatMessage.TimeResumed, _screen.Messages.Single());
    }

    [TestMethod]
    public void Stay_WithTheSeatOff_ResumesAndCancelsTheRequest()
    {
        Press(SeatTimeKey.Slow);
        Press(SeatTimeKey.Stay);
        Assert.IsTrue(_screen.Paused, "the control: a pause is held over the request");
        Assert.AreEqual(1, _screen.SlowRequests.Count);
        _settings.Enabled = false;

        Press(SeatTimeKey.Stay);

        Assert.IsFalse(_screen.Paused, "the held pause is resumed");
        Assert.AreEqual(SeatMessage.TimeResumed, _screen.Messages.Last());
        Assert.AreEqual(0, _screen.SlowRequests.Count, "and with the Seat off the press cancels what the Seat set, as every other press does");
        CollectionAssert.AreEqual(new[] { "pass mission=7 level=0.5 event=level", "pass mission=7 level=0.5 event=stay", "pass mission=7 level=0.5 event=resume" }, Lines());
    }

    [TestMethod]
    public void Stay_WithoutThePermission_StillResumes()
    {
        Press(SeatTimeKey.Stay);
        _permissions = Allowed with { Time = false };

        Press(SeatTimeKey.Stay);

        Assert.IsFalse(_screen.Paused, "a pause is resumed whatever the permission says");
        Assert.AreEqual(SeatMessage.TimeResumed, _screen.Messages.Last());
        Assert.IsFalse(Lines().Any(l => l.StartsWith("skip")), "and it writes no refusal");
    }

    [TestMethod]
    public void Stay_KeepsASlowRequest_BecauseOnlyFastTimeIsCancelled()
    {
        Press(SeatTimeKey.Slow);

        Press(SeatTimeKey.Stay);

        Assert.IsTrue(_screen.Paused);
        Assert.AreEqual(1, _screen.SlowRequests.Count);
    }

    // ---- Gates ----

    [TestMethod]
    public void AKey_OutsideBattleMode_StaysSilent()
    {
        _screen.Facts = _screen.Facts with { BattleMode = false, Deployment = true };

        Press(SeatTimeKey.Quicken);
        Press(SeatTimeKey.Stay);

        Assert.AreEqual(0, _screen.TimeWrites.Count);
        Assert.AreEqual(0, Lines().Length, "an interlude or a deployment writes no line, as 5.10 treats an interlude");
        Assert.AreEqual(0, _screen.Messages.Count);
        Assert.AreEqual(0, _settings.Reads, "the mode is read before any setting");
    }

    [TestMethod]
    public void AKey_WithoutThePermission_WritesASkipAndNothingElse()
    {
        _permissions = Allowed with { Time = false };

        Press(SeatTimeKey.Quicken);

        Assert.AreEqual(0, _screen.TimeWrites.Count);
        CollectionAssert.AreEqual(new[] { "skip mission=7 reason=denied" }, Lines());
    }

    [TestMethod]
    public void AKey_WithTheSeatDisabled_CancelsWhatItDid_AndStopsActing()
    {
        Press(SeatTimeKey.Quicken, 2);
        Press(SeatTimeKey.Stay);
        _settings.Enabled = false;

        Press(SeatTimeKey.Quicken);

        Assert.IsFalse(_screen.Paused);
        Assert.IsFalse(_screen.FixedStep);
        Assert.IsFalse(_screen.FastForward);
        Assert.AreEqual(0, _screen.SlowRequests.Count);
        StringAssert.Contains(string.Join("|", Lines()), "skip mission=7 reason=setting-off");

        var writes = _screen.TimeWrites.Count;
        Press(SeatTimeKey.Slow);
        Assert.AreEqual(writes, _screen.TimeWrites.Count, "the key does nothing while the Seat is off");
    }

    [TestMethod]
    public void TheSetting_IsReadOnlyWhenATimeKeyIsPressed()
    {
        Frames(60);
        Assert.AreEqual(0, _settings.Reads);

        Press(SeatTimeKey.Quicken);
        Frames(60);

        Assert.AreEqual(1, _settings.Reads);
    }

    [TestMethod]
    public void AFaultedSession_IgnoresTheKeys()
    {
        Seated();
        _screen.ApplyThrows = new InvalidOperationException("camera");
        _sut.OnFrame(0.1f);
        Assert.IsTrue(_sut.Faulted);
        var writes = _screen.TimeWrites.Count;

        Press(SeatTimeKey.Quicken);
        Press(SeatTimeKey.Stay);

        Assert.AreEqual(writes, _screen.TimeWrites.Count);
    }

    [TestMethod]
    public void AnotherViewsClock_HoldsTheLadderAtOne_SlowTimeAndThePauseStillWork()
    {
        _screen.OwnedElsewhere = true;

        Press(SeatTimeKey.Quicken);

        Assert.AreEqual(SeatMessage.TimeOtherClock, _screen.Messages.Single());
        Assert.AreEqual(0, _screen.TimeWrites.Count);
        CollectionAssert.AreEqual(new[] { "skip mission=7 reason=other-clock" }, Lines());

        Press(SeatTimeKey.Slow);
        Press(SeatTimeKey.Stay);
        Assert.AreEqual(1, _screen.SlowRequests.Count);
        Assert.IsTrue(_screen.Paused);
    }

    [TestMethod]
    public void AnotherViewsClock_AtItsOwnTen_IsNeverTheSeatsLevel_SoSlowGivesAHalfWithNoExtraStop()
    {
        _screen.OwnedElsewhere = true;
        _screen.FastForward = true;

        Press(SeatTimeKey.Slow);

        Assert.AreEqual("0.5", _screen.MessageArguments.Single(), "the flag is theirs, so the ladder started at one");
        Assert.AreEqual(1, _screen.SlowRequests.Count);
        Assert.AreEqual(1, _screen.Stops, "the one stop is the slow level's own");
    }

    [TestMethod]
    public void AnotherViewsClock_AtItsOwnTen_LeavesTheStoredLevelAlone_FrameAfterFrame()
    {
        _screen.OwnedElsewhere = true;
        Press(SeatTimeKey.Slow);
        var writes = _screen.TimeWrites.Count;
        _screen.FastForward = true;

        Frames(5);

        Assert.AreEqual(writes, _screen.TimeWrites.Count, "no adoption and no stop while another view writes the flag");
        Assert.AreEqual(1, _screen.SlowRequests.Count, "the half-speed request is still the Seat's");
        Press(SeatTimeKey.Slow);
        Assert.AreEqual("0.25", _screen.MessageArguments.Last(), "the stored level is still a half");
    }

    [TestMethod]
    public void Leave_WithAnotherViewsClockAtTen_StartsNothing()
    {
        _screen.OwnedElsewhere = true;
        Seated();
        Press(SeatTimeKey.Slow);
        _screen.FastForward = true;
        Frames();
        var messages = _screen.Messages.Count;
        var writes = _screen.TimeWrites.Count;

        _sut.OnScreenTick(true);
        Frames();

        Assert.AreEqual(SeatState.Off, _sut.State);
        Assert.AreEqual(messages, _screen.Messages.Count, "level 10 was never the Seat's, so a leave has none to lower");
        Assert.AreEqual(writes, _screen.TimeWrites.Count);
    }

    [TestMethod]
    public void Take_WithAnotherViewsClockAtTen_StopsNothing()
    {
        _screen.OwnedElsewhere = true;
        Seated();
        Press(SeatTimeKey.Slow);
        _hero.Active = false;
        _sut.OnMissionTick();
        _screen.FastForward = true;
        Frames();
        var stops = _screen.Stops;

        _sut.PressTake();
        _sut.OnMissionTick();

        Assert.AreEqual(1, _screen.TakeCalls);
        Assert.AreEqual(stops, _screen.Stops, "the Seat stops no fast time it did not start");
    }

    [TestMethod]
    public void Stay_OutsideBattleMode_ResumesAPause_ButNeverStartsOne()
    {
        _screen.Facts = _screen.Facts with { BattleMode = false };
        _screen.Paused = true;

        Press(SeatTimeKey.Stay);

        Assert.IsFalse(_screen.Paused, "a pause can always be resumed, and a cut scene could leave the battle stopped with nothing else to end it");
        Assert.AreEqual(SeatMessage.TimeResumed, _screen.Messages.Single());

        Press(SeatTimeKey.Stay);

        Assert.IsFalse(_screen.Paused, "a new pause still needs Battle mode");
        Assert.AreEqual(1, _screen.Messages.Count);
    }

    // ---- The hand-offs ----

    [TestMethod]
    public void Leave_AtTenWithALivingHero_StepsDownToFour()
    {
        Seated();
        Press(SeatTimeKey.Quicken, 4);
        Assert.IsTrue(_screen.FastForward);

        _sut.OnScreenTick(true);
        Frames();

        Assert.AreEqual(SeatState.Off, _sut.State);
        Assert.IsFalse(_screen.FastForward);
        Assert.IsTrue(_screen.FixedStep);
        Assert.AreEqual("4", _screen.MessageArguments.Last());
    }

    [TestMethod]
    public void Leave_AtTenWithNoHero_KeepsTen()
    {
        Seated();
        Press(SeatTimeKey.Quicken, 4);
        _hero.Active = false;

        _sut.OnScreenTick(true);

        Assert.IsTrue(_screen.FastForward);
    }

    [TestMethod]
    public void Leave_AtASlowLevel_ChangesNothing()
    {
        Seated();
        Press(SeatTimeKey.Slow);
        var writes = _screen.TimeWrites.Count;

        _sut.OnScreenTick(true);

        Assert.AreEqual(writes, _screen.TimeWrites.Count);
    }

    [TestMethod]
    public void TakingASoldier_AtFour_StopsTheFixedStep()
    {
        Seated();
        Press(SeatTimeKey.Quicken, 2);
        Frames();
        Assert.IsTrue(_screen.FixedStep);
        _hero.Active = false;
        _sut.OnMissionTick();
        _sut.PressTake();
        _sut.OnMissionTick();

        Assert.IsFalse(_screen.FixedStep, "the take also stops the fixed step");
        Assert.IsFalse(_screen.FastForward);
        Press(SeatTimeKey.Quicken);
        Assert.AreEqual("2", _screen.MessageArguments.Last(), "the level after the take is one");
    }

    [TestMethod]
    public void TakingASoldier_AtASlowLevel_KeepsTheRequest()
    {
        Seated();
        Press(SeatTimeKey.Slow);
        _hero.Active = false;
        _sut.OnMissionTick();

        _sut.PressTake();
        _sut.OnMissionTick();

        Assert.AreEqual(1, _screen.TakeCalls);
        Assert.AreEqual(1, _screen.SlowRequests.Count);
    }

    [TestMethod]
    public void TakingASoldier_AtTen_FollowsTheEnginesClearedFlag()
    {
        Seated();
        _hero.Active = false;
        Press(SeatTimeKey.Quicken, 4);
        Assert.IsTrue(_screen.FastForward);

        _sut.PressTake();
        _sut.OnMissionTick();
        Frames();

        Assert.IsFalse(_screen.FastForward);
        Assert.IsFalse(_screen.FixedStep);
        Press(SeatTimeKey.Quicken);
        Assert.AreEqual("2", _screen.MessageArguments.Last());
    }

    // ---- Teardown ----

    [TestMethod]
    public void Finish_WhileOff_CancelsPauseFixedStepFlagAndRequest()
    {
        Press(SeatTimeKey.Slow);
        Press(SeatTimeKey.Stay);

        _sut.Finish();

        Assert.IsFalse(_screen.Paused);
        Assert.IsFalse(_screen.FixedStep);
        Assert.IsFalse(_screen.FastForward);
        Assert.AreEqual(0, _screen.SlowRequests.Count);
    }

    [TestMethod]
    public void Finish_WithATimeKeyNeverPressed_WritesNothing()
    {
        _sut.Finish();
        _sut.Finish();

        Assert.AreEqual(0, _screen.TimeWrites.Count, "a player who never presses a time key sees vanilla behaviour at the end too");
    }

    [TestMethod]
    public void Finish_AtTen_ClearsTheProcessGlobalFlag()
    {
        Seated();
        Press(SeatTimeKey.Quicken, 4);

        _sut.Finish();

        Assert.IsFalse(_screen.FastForward, "IMBMission_OnFastForwardStateChanged is process global and the state-finalize path has no EndMission");
    }

    [TestMethod]
    public void Finish_WhenTheLeaveThrows_StillCancelsTime()
    {
        Seated();
        Press(SeatTimeKey.Stay);
        _screen.ControllerOnThrows = new InvalidOperationException("controller");

        _sut.Finish();

        Assert.IsFalse(_screen.Paused, "the cancel sits outside the leave and after its try");
    }

    [TestMethod]
    public void Finish_Twice_IsSafe()
    {
        Press(SeatTimeKey.Quicken, 2);
        _sut.Finish();
        var writes = _screen.TimeWrites.Count;

        _sut.Finish();

        Assert.AreEqual(writes, _screen.TimeWrites.Count, "the second finish finds nothing owed");
    }

    [TestMethod]
    public void AFaultWhilePaused_LeavesTheBattleUnpaused()
    {
        Seated();
        Press(SeatTimeKey.Stay);
        Assert.IsTrue(_screen.Paused);
        _screen.ApplyThrows = new InvalidOperationException("camera");

        _sut.OnFrame(0.1f);

        Assert.IsTrue(_sut.Faulted);
        Assert.IsFalse(_screen.Paused, "a throw must not leave a battle paused with dead keys");
    }

    [TestMethod]
    public void AFault_CancelsEveryPieceEvenWhenOneStepThrows()
    {
        Seated();
        Press(SeatTimeKey.Slow);
        Press(SeatTimeKey.Stay);
        _screen.StopThrows = new InvalidOperationException("stop");
        _screen.ApplyThrows = new InvalidOperationException("camera");

        _sut.OnFrame(0.1f);

        Assert.IsFalse(_screen.Paused, "the pause is off whatever the fixed step did");
        Assert.AreEqual(0, _screen.SlowRequests.Count, "and the request is cleared");
        Assert.IsTrue(_log.Lines.Any(l => l.Result == "swallowed"), "each step goes through Swallow");
    }

    [TestMethod]
    public void ACancelThatLeavesTimeOn_IsRetriedOnLaterFramesAndGivenUpAfterThreeTries()
    {
        Seated();
        Press(SeatTimeKey.Quicken, 2);
        Frames();
        _screen.StopThrows = new InvalidOperationException("stop");
        _screen.ApplyThrows = new InvalidOperationException("camera");
        _sut.OnFrame(0.1f);
        Assert.IsTrue(_sut.Faulted);
        Assert.IsTrue(_screen.FixedStep, "the first try could not stop it");

        Frames(10);

        Assert.AreEqual(3, _screen.Stops, "three tries in all, the first included");
        CollectionAssert.AreEqual(new[] { "fail mission=7 reason=cancel-failed tries=3" }, Lines().Where(l => l.StartsWith("fail")).ToArray());
    }

    [TestMethod]
    public void ACancelThatCannotStopFastTime_KeepsTheStoredLevelAtOne_WhileItRetries()
    {
        Seated();
        Press(SeatTimeKey.Quicken, 4);
        Assert.AreEqual(10f, _sut.TimeLevel, "the control: the Seat's own ten");
        _screen.StopThrows = new InvalidOperationException("stop");
        _screen.ApplyThrows = new InvalidOperationException("camera");
        _sut.OnFrame(0.1f);
        Assert.IsTrue(_screen.FastForward, "the first try could not stop it");

        Frames(2);

        Assert.IsTrue(_screen.FastForward, "the flag is stuck");
        Assert.AreEqual(1f, _sut.TimeLevel, "a stuck flag is the cancel's debt and no level a key set");
    }

    [TestMethod]
    public void ACancelThatWorksOnTheSecondTry_StopsRetrying()
    {
        Seated();
        Press(SeatTimeKey.Quicken, 2);
        Frames();
        _screen.StopThrows = new InvalidOperationException("stop");
        _screen.StopThrowsTimes = 1;
        _screen.ApplyThrows = new InvalidOperationException("camera");
        _sut.OnFrame(0.1f);
        Assert.IsTrue(_screen.FixedStep);

        Frames(10);

        Assert.IsFalse(_screen.FixedStep);
        Assert.AreEqual(2, _screen.Stops);
        Assert.AreEqual(0, Lines().Count(l => l.StartsWith("fail")));
    }

    [TestMethod]
    public void TheCancelOfAFault_IgnoresTheFlagsAnotherViewWritesEveryFrame_ButNotWhenTheSeatOwnsTheClock()
    {
        _screen.OwnedElsewhere = true;
        _screen.StopDoesNotStick = true;
        Seated();
        Press(SeatTimeKey.Stay);
        _screen.FixedStep = true;
        _screen.ApplyThrows = new InvalidOperationException("camera");

        _sut.OnFrame(0.1f);
        Frames(10);

        Assert.IsTrue(_sut.Faulted);
        Assert.IsFalse(_screen.Paused);
        Assert.AreEqual(1, _screen.Stops, "Long Defeat's own speed is not left on by the Seat, so nothing is retried");
        Assert.AreEqual(0, Lines().Count(l => l.StartsWith("fail")));
    }

    [TestMethod]
    public void TheSameFlagAfterAFault_WhenNoOtherViewOwnsTheClock_IsTheSeatsDebt()
    {
        _screen.StopDoesNotStick = true;
        Seated();
        Press(SeatTimeKey.Stay);
        _screen.FixedStep = true;
        _screen.ApplyThrows = new InvalidOperationException("camera");

        _sut.OnFrame(0.1f);
        Frames(10);

        Assert.AreEqual(1, Lines().Count(l => l.StartsWith("fail")));
    }

    [TestMethod]
    public void TheCancelOfTheMissionsEnd_HasNoLaterTickToRetryOn_SoItTriesOnceAndWritesNoFailLine()
    {
        _screen.StopDoesNotStick = true;
        Press(SeatTimeKey.Stay);
        _screen.FixedStep = true;

        _sut.Finish();

        Assert.AreEqual(1, _screen.Stops, "one try, and the debt is never paid");
        Assert.AreEqual(0, Lines().Count(l => l.StartsWith("fail")));
    }

    [TestMethod]
    public void TheCancelOfAKeyPressedWithTheSeatOff_PaysItsDebtOnLaterFrames()
    {
        _screen.StopDoesNotStick = true;
        Press(SeatTimeKey.Stay);
        _screen.FixedStep = true;
        _settings.Enabled = false;

        Press(SeatTimeKey.Slow);
        Frames(10);

        Assert.AreEqual(3, _screen.Stops, "three tries in all");
        Assert.AreEqual(1, Lines().Count(l => l.StartsWith("fail")));
    }
}
