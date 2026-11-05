using System;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Client.Features.SeatOfSeeing;
using TAOM.Core.Logging;
using TAOM.Features.SeatOfSeeing;
using TAOM.Features.SeatOfSeeing.Domain;

namespace TAOM.Tests.Features.SeatOfSeeing;

/// <summary>Stage S4 of the Seat's state machine (build-spec section 8): the hero is handed to the AI by comparing what is wanted with what was done, from the mission tick.</summary>
[TestClass]
public class SeatSessionFightTests
{
    private const string Key = "7";
    private static readonly SeatPermissions Allowed = new(Camera: true, FightOn: true, Orders: true, Time: true, TakeSoldier: true);
    private static readonly SeatPermissions NoFight = new(Camera: true, FightOn: false, Orders: true, Time: true, TakeSoldier: false);

    private FakeSeatScreen _screen = null!;
    private FakeSeatHero _hero = null!;
    private FakeSeatSettings _settings = null!;
    private RecordingOutcomeLog _log = null!;
    private IModLogger _logger = null!;
    private SeatPermissions _permissions;
    private SeatSession _sut = null!;

    [TestInitialize]
    public void Setup()
    {
        SeatPresence.Set(false);
        SeatPresence.SetHeroFightsOn(false);
        _screen = new FakeSeatScreen();
        _hero = new FakeSeatHero();
        _settings = new FakeSeatSettings { HeroFightsOn = true };
        _log = new RecordingOutcomeLog();
        _logger = Substitute.For<IModLogger>();
        _permissions = Allowed;
        _sut = new SeatSession(_screen, _hero, _settings, () => _permissions, _log, _logger, Key);
    }

    [TestCleanup]
    public void Cleanup()
    {
        SeatPresence.Set(false);
        SeatPresence.SetHeroFightsOn(false);
    }

    private void Enter() => _sut.OnScreenTick(true);

    private void Seated()
    {
        Enter();
        for (var i = 0; i < 40 && _sut.State != SeatState.Seated; i++)
            _sut.OnFrame(0.1f);
        Assert.AreEqual(SeatState.Seated, _sut.State);
    }

    private void Leave() => _sut.OnScreenTick(true);

    private SeatSession Fresh(Func<SeatPermissions> permissions) => new(_screen, _hero, _settings, permissions, _log, _logger, Key);

    // ---- Begin ----

    [TestMethod]
    public void MissionTick_AfterAnEntryThatChoseToFightOn_HandsTheHeroToTheAi_AndRaisesTheGate()
    {
        Seated();
        Assert.AreEqual(0, _hero.Begins, "the hand-off writes the agent, so it waits for the mission tick");
        Assert.IsFalse(SeatPresence.HeroFightsOn);

        _sut.OnMissionTick();

        Assert.AreEqual(1, _hero.Begins);
        Assert.IsTrue(SeatPresence.HeroFightsOn);
        Assert.AreEqual(1, _log.Count("pass", SeatChecks.FightOnId));
        StringAssert.Contains(_log.Lines.Single(l => l.Id == SeatChecks.FightOnId).Detail, "event=begin");
    }

    [TestMethod]
    public void MissionTick_WhileHanded_DoesNotBeginAgain()
    {
        Seated();
        _sut.OnMissionTick();
        _sut.OnMissionTick();
        _sut.OnMissionTick();

        Assert.AreEqual(1, _hero.Begins);
        Assert.AreEqual(0, _hero.Ends);
    }

    [TestMethod]
    public void Entry_WithTheSettingOff_NeverHandsTheHeroOver()
    {
        _settings.HeroFightsOn = false;
        Seated();

        _sut.OnMissionTick();

        Assert.AreEqual(0, _hero.Begins);
        Assert.IsFalse(SeatPresence.HeroFightsOn);
    }

    [TestMethod]
    public void Entry_WhereThePolicyAllowsNoFightOn_NeverHandsTheHeroOver()
    {
        _permissions = NoFight;
        Seated();

        _sut.OnMissionTick();

        Assert.AreEqual(0, _hero.Begins);
    }

    // The choice is made at each entry, so a setting turned on while seated waits for the next entry.
    [TestMethod]
    public void TheSettingTurnedOnWhileSeated_ActsOnTheNextEntryAndNotThisOne()
    {
        _settings.HeroFightsOn = false;
        Seated();
        _settings.HeroFightsOn = true;
        _sut.OnMissionTick();
        Assert.AreEqual(0, _hero.Begins);

        Leave();
        _sut.OnMissionTick();
        Seated();
        _sut.OnMissionTick();

        Assert.AreEqual(1, _hero.Begins);
    }

    [TestMethod]
    public void TheChoiceIsRead_FreshAtEachEntry_ThroughThePolicyAndTheSetting()
    {
        var calls = 0;
        _sut = Fresh(() =>
        {
            calls++;
            return _permissions;
        });

        Seated();
        var callsAtEntry = calls;
        _sut.OnMissionTick();
        _sut.OnMissionTick();

        Assert.IsTrue(callsAtEntry >= 1);
        Assert.AreEqual(callsAtEntry, calls, "the mission tick asks the policy nothing");
    }

    [DataTestMethod]
    [DataRow((int)SeatFightRefusal.MissionEnded, "mission-ended")]
    [DataRow((int)SeatFightRefusal.NoHero, "no-hero")]
    [DataRow((int)SeatFightRefusal.NotPlayerControlled, "not-player")]
    public void BeginRefused_WritesASkipWithItsReason_ClearsTheChoice_AndDoesNotAskAgain(int refusal, string reason)
    {
        _hero.BeginResult = (SeatFightRefusal)refusal;
        Seated();

        _sut.OnMissionTick();
        _sut.OnMissionTick();
        _sut.OnMissionTick();

        Assert.AreEqual(1, _hero.Begins, "a refusal clears the choice for this entry, so the tick does not ask again");
        Assert.IsFalse(SeatPresence.HeroFightsOn);
        var line = _log.Lines.Single(l => l.Id == SeatChecks.FightOnId);
        Assert.AreEqual("skip", line.Result);
        StringAssert.Contains(line.Detail, "reason=refused");
        StringAssert.Contains(line.Detail, "why=" + reason);

        // The next entry asks again.
        Leave();
        _sut.OnMissionTick();
        _hero.BeginResult = SeatFightRefusal.None;
        Seated();
        _sut.OnMissionTick();
        Assert.AreEqual(2, _hero.Begins);
        Assert.IsTrue(SeatPresence.HeroFightsOn);
    }

    [TestMethod]
    public void BeginThrowing_WritesAFail_LatchesTheFault_AndDoesNotRetry()
    {
        _hero.BeginThrows = new InvalidOperationException("boom");
        Seated();

        _sut.OnMissionTick();
        _sut.OnMissionTick();

        Assert.IsTrue(_sut.Faulted);
        Assert.AreEqual(1, _log.Count("fail", SeatChecks.FightOnId));
        Assert.IsFalse(SeatPresence.HeroFightsOn);
        Assert.AreEqual(SeatState.Off, _sut.State);
        Assert.AreEqual(1, _hero.Calls.Count(c => c == "begin"), "the fault ends the choice, and a faulted session wants nothing");
    }

    // ---- End ----

    [TestMethod]
    public void Leave_ThenAMissionTick_HandsTheHeroBack_AndLowersTheGate()
    {
        Seated();
        _sut.OnMissionTick();

        Leave();
        Assert.IsTrue(SeatPresence.HeroFightsOn, "the leave leaves the gate up: the hero stays on the AI until the mission tick");
        Assert.AreEqual(0, _hero.Ends);

        _sut.OnMissionTick();

        Assert.AreEqual(1, _hero.Ends);
        Assert.IsFalse(SeatPresence.HeroFightsOn);
        Assert.AreEqual(1, _log.Lines.Count(l => l.Id == SeatChecks.FightOnId && l.Detail.Contains("event=end")));
    }

    // An entry and a leave with no tick between them would reverse a hero who was never handed over.
    [TestMethod]
    public void AnEntryAndALeaveWithNoMissionTickBetween_NeverTouchTheHero()
    {
        Seated();
        Leave();

        _sut.OnMissionTick();
        _sut.OnMissionTick();

        Assert.AreEqual(0, _hero.Begins);
        Assert.AreEqual(0, _hero.Ends);
        Assert.IsFalse(SeatPresence.HeroFightsOn);
    }

    [TestMethod]
    public void AnEntryAfterALeaveWithNoTickBetween_KeepsTheHeroOnTheAi_WithoutAnEndOrASecondBegin()
    {
        Seated();
        _sut.OnMissionTick();
        Leave();

        Seated();
        _sut.OnMissionTick();

        Assert.AreEqual(1, _hero.Begins);
        Assert.AreEqual(0, _hero.Ends);
        Assert.IsTrue(SeatPresence.HeroFightsOn);
    }

    [TestMethod]
    public void End_ThatKeepsThrowing_IsTriedThreeTimesInAll_ThenWritesOneFailureLine_AndStops()
    {
        Seated();
        _sut.OnMissionTick();
        _hero.EndThrows = new InvalidOperationException("boom");
        Leave();

        for (var i = 0; i < 8; i++)
            _sut.OnMissionTick();

        Assert.AreEqual(SeatFightRule.HandBackTries, _hero.Calls.Count(c => c == "end"), "the first try and two retries, and no more");
        Assert.AreEqual(1, _hero.Calls.Count(c => c == "begin"));
        Assert.IsFalse(SeatPresence.HeroFightsOn, "a throw from the end still lowers the gate");
        var failures = _log.Lines.Where(l => l.Id == SeatChecks.FightOnId && l.Result == "fail").ToList();
        Assert.AreEqual(2, failures.Count, "the throw of the first try, and the one line that gives up");
        Assert.AreEqual(1, failures.Count(l => l.Detail.Contains("reason=hand-back-failed tries=3")));
    }

    [TestMethod]
    public void ARetryThatThrowsBelowTheLimit_LeavesOneSwallowedLine_AndTheLastTryDoesNot()
    {
        Seated();
        _sut.OnMissionTick();
        _hero.EndThrows = new InvalidOperationException("boom");
        Leave();

        for (var i = 0; i < 8; i++)
            _sut.OnMissionTick();

        var swallowed = _log.Lines.Where(l => l.Result == "swallowed").ToList();
        Assert.AreEqual(1, swallowed.Count, "the second try's exception is counted; the third becomes the give-up line");
        Assert.AreEqual("SeatOfSeeing.HandBackRetry", swallowed[0].Id);
    }

    // The fake throws before its controller write, as the adapter's first steps can: the hero is still on the AI after the fault.
    [TestMethod]
    public void End_ThatThrowsBeforeThePlayerWrite_IsRetriedOnLaterTicks_UntilTheHeroIsThePlayers()
    {
        Seated();
        _sut.OnMissionTick();
        _hero.EndThrows = new InvalidOperationException("boom");
        _hero.EndThrowsTimes = 1;
        Leave();

        _sut.OnMissionTick();

        Assert.IsTrue(_sut.Faulted);
        Assert.IsTrue(_hero.OnAi, "the first try threw before the controller write");
        Assert.IsFalse(SeatPresence.HeroFightsOn, "the gate stays down across the retries");

        _sut.OnMissionTick();

        Assert.IsFalse(_hero.OnAi, "the retry wrote the controller");
        Assert.IsFalse(SeatPresence.HeroFightsOn);
        Assert.AreEqual(2, _hero.Calls.Count(c => c == "end"));
        Assert.AreEqual(1, _log.Lines.Count(l => l.Id == SeatChecks.FightOnId && l.Detail.Contains("event=end")));

        _sut.OnMissionTick();
        _sut.OnMissionTick();

        Assert.AreEqual(2, _hero.Calls.Count(c => c == "end"), "a hand-back that worked is not asked again");
    }

    [TestMethod]
    public void AFailedHandBack_IsNotRetriedForAHeroWhoFell()
    {
        Seated();
        _sut.OnMissionTick();
        _hero.EndThrows = new InvalidOperationException("boom");
        Leave();
        _sut.OnMissionTick();
        _hero.Active = false;

        _sut.OnMissionTick();
        _sut.OnMissionTick();

        Assert.AreEqual(1, _hero.Calls.Count(c => c == "end"), "a controller write on a removed agent would make the corpse the main agent");
    }

    [TestMethod]
    public void AFailedHandBack_IsNotRetriedForAHeroWhoIsAlreadyThePlayers()
    {
        Seated();
        _sut.OnMissionTick();
        _hero.EndThrows = new InvalidOperationException("boom");
        Leave();
        _sut.OnMissionTick();
        _hero.OnAi = false;

        _sut.OnMissionTick();
        _sut.OnMissionTick();

        Assert.AreEqual(1, _hero.Calls.Count(c => c == "end"));
        Assert.AreEqual(1, _log.Lines.Count(l => l.Id == SeatChecks.FightOnId && l.Result == "fail"), "no give-up line: nothing was owed any more");
    }

    [TestMethod]
    public void AFailedHandBack_IsNotRetriedAfterTheScreenIsFinished()
    {
        Seated();
        _sut.OnMissionTick();
        _hero.EndThrows = new InvalidOperationException("boom");
        Leave();
        _sut.OnMissionTick();

        _sut.Finish();
        _sut.OnMissionTick();
        _sut.OnMissionTick();

        Assert.AreEqual(1, _hero.Calls.Count(c => c == "end"));
    }

    [TestMethod]
    public void AFault_WhileHanded_LowersTheGateAtOnce_AndTheNextMissionTickHandsTheHeroBack()
    {
        Seated();
        _sut.OnMissionTick();
        _screen.ApplyThrows = new InvalidOperationException("boom");

        _sut.OnFrame(0.1f);

        Assert.IsTrue(_sut.Faulted);
        Assert.IsFalse(SeatPresence.HeroFightsOn, "a fault clears the gate");
        Assert.AreEqual(0, _hero.Ends);

        _sut.OnMissionTick();

        Assert.AreEqual(1, _hero.Ends, "the hand-back runs before the fault's early return");
    }

    [TestMethod]
    public void Finish_WhileHanded_LowersTheGate_AndWritesNoAgent()
    {
        Seated();
        _sut.OnMissionTick();

        _sut.Finish();

        Assert.IsFalse(SeatPresence.HeroFightsOn);
        Assert.AreEqual(0, _hero.Ends, "no mission tick follows a finish");
    }

    [TestMethod]
    public void ALeave_DoesNotLowerTheGateByItself()
    {
        Seated();
        _sut.OnMissionTick();

        Leave();

        Assert.IsTrue(SeatPresence.HeroFightsOn);
    }

    // ---- The boundary hold ----

    [TestMethod]
    public void Entry_WhereThePolicyAllowsFightOn_HoldsTheBoundaryCountdown_AndTheLeaveReleasesIt()
    {
        _settings.HeroFightsOn = false;
        Seated();

        Assert.IsTrue(SeatPresence.HoldsBoundaryCountdown(true), "a stand-fast hero outside the line has the same countdown");

        Leave();

        Assert.IsFalse(SeatPresence.HoldsBoundaryCountdown(true));
    }

    [TestMethod]
    public void Entry_WhereThePolicyAllowsNoFightOn_NeverHoldsTheBoundaryCountdown()
    {
        _permissions = NoFight;
        Seated();

        Assert.IsTrue(SeatPresence.IsSeated);
        Assert.IsFalse(SeatPresence.HoldsBoundaryCountdown(true), "a network session keeps the camera and none of the simulation");
    }

    // ---- The fall ----

    [TestMethod]
    public void TheHeroFallingWhileHanded_ClearsTheStateTheGateAndTheChoice_AndOwesNothing()
    {
        Seated();
        _sut.OnMissionTick();
        _hero.Active = false;

        _sut.OnMissionTick();
        _sut.OnMissionTick();

        Assert.IsFalse(SeatPresence.HeroFightsOn);
        Assert.AreEqual(0, _hero.Ends, "a setter call on a removed agent would make the corpse the main agent");
        var line = _log.Lines.Single(l => l.Id == SeatChecks.FightOnId && l.Result == "skip");
        StringAssert.Contains(line.Detail, "reason=fell");

        // The choice is cleared too: a soldier taken later is not handed to the AI by this entry.
        _hero.Active = true;
        _hero.OnAi = false;
        _sut.OnMissionTick();
        Assert.AreEqual(1, _hero.Begins);
    }

    [TestMethod]
    public void TheHeroFallingWhileHanded_SaysTheFallOnce_InTheTickThatLowersTheGate()
    {
        Seated();
        _sut.OnMissionTick();
        Assert.IsTrue(SeatPresence.HeroFightsOn);
        _hero.Active = false;
        _sut.OnMainAgentChanged();

        _sut.OnMissionTick();

        Assert.IsFalse(SeatPresence.HeroFightsOn);
        Assert.AreEqual(1, _screen.Messages.Count(m => m == SeatMessage.HeroFell), "the hand-off's fall rule must not swallow the message");
        _sut.OnMissionTick();
        Assert.AreEqual(1, _screen.Messages.Count(m => m == SeatMessage.HeroFell), "the fall is said once");
    }

    [TestMethod]
    public void TheHeroFalling_ThenALeave_NeverCallsEnd()
    {
        Seated();
        _sut.OnMissionTick();
        _hero.Active = false;
        _sut.OnMissionTick();

        Leave();
        _sut.OnMissionTick();

        Assert.AreEqual(0, _hero.Ends);
    }

    // ---- The retreat check ----

    [TestMethod]
    public void WhileHanded_ARetreatIsStopped_AtMostOnceARealSecond()
    {
        Seated();
        _sut.OnMissionTick();
        _hero.RetreatPending = true;

        _screen.Real = 10f;
        _sut.OnMissionTick();
        Assert.AreEqual(1, _hero.RetreatStops, "the first check runs at once");

        _hero.RetreatPending = true;
        _screen.Real = 10.5f;
        _sut.OnMissionTick();
        Assert.AreEqual(1, _hero.RetreatStops, "less than a real second since the last check");

        _screen.Real = 11f;
        _sut.OnMissionTick();
        Assert.AreEqual(2, _hero.RetreatStops);
    }

    [TestMethod]
    public void WhileHanded_AStoppedRetreat_WritesOneLine()
    {
        Seated();
        _sut.OnMissionTick();
        _hero.RetreatPending = true;
        _screen.Real = 10f;
        _sut.OnMissionTick();
        _hero.RetreatPending = true;
        _screen.Real = 12f;
        _sut.OnMissionTick();

        Assert.AreEqual(1, _log.Lines.Count(l => l.Id == SeatChecks.FightOnId && l.Detail.Contains("event=retreat-stopped")));
    }

    [TestMethod]
    public void WhenNoOneIsHanded_TheTickReadsNoClockAndNoController()
    {
        _settings.HeroFightsOn = false;
        Seated();

        _sut.OnMissionTick();
        _sut.OnMissionTick();

        Assert.AreEqual(0, _screen.RealReads);
        Assert.AreEqual(0, _hero.OnAiReads);
    }

    [TestMethod]
    public void TheRetreatCheck_RunsOnlyWhileHanded()
    {
        _settings.HeroFightsOn = false;
        Seated();
        _hero.RetreatPending = true;
        _screen.Real = 50f;

        _sut.OnMissionTick();

        Assert.AreEqual(0, _hero.RetreatStops);
    }
}
