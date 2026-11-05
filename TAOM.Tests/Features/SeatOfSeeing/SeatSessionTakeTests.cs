using System;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Client.Features.SeatOfSeeing;
using TAOM.Core.Logging;
using TAOM.Features.SeatOfSeeing;
using TAOM.Features.SeatOfSeeing.Domain;

namespace TAOM.Tests.Features.SeatOfSeeing;

/// <summary>Stage S5 of the Seat's state machine (build-spec section 9): the message at the fall, and the take of a soldier from the mission tick.</summary>
[TestClass]
public class SeatSessionTakeTests
{
    private static readonly SeatPermissions Allowed = new(Camera: true, FightOn: true, Orders: true, Time: true, TakeSoldier: true);
    private static readonly SeatPermissions NoTake = new(Camera: true, FightOn: false, Orders: true, Time: true, TakeSoldier: false);

    private FakeSeatScreen _screen = null!;
    private FakeSeatHero _hero = null!;
    private FakeSeatSettings _settings = null!;
    private RecordingOutcomeLog _log = null!;
    private SeatPermissions _permissions;
    private SeatSession _sut = null!;

    [TestInitialize]
    public void Setup()
    {
        SeatPresence.Set(false);
        SeatPresence.SetHeroFightsOn(false);
        _screen = new FakeSeatScreen();
        _hero = new FakeSeatHero();
        _settings = new FakeSeatSettings();
        _log = new RecordingOutcomeLog();
        _permissions = Allowed;
        _sut = new SeatSession(_screen, _hero, _settings, () => _permissions, _log, Substitute.For<IModLogger>(), "7");
    }

    [TestCleanup]
    public void Cleanup()
    {
        SeatPresence.Set(false);
        SeatPresence.SetHeroFightsOn(false);
    }

    private void Seated()
    {
        _sut.OnScreenTick(true);
        for (var i = 0; i < 40 && _sut.State != SeatState.Seated; i++)
            _sut.OnFrame(0.1f);
        Assert.AreEqual(SeatState.Seated, _sut.State);
    }

    private void TheHeroFalls()
    {
        _hero.Active = false;
        _sut.OnMainAgentChanged();
        _sut.OnMissionTick();
    }

    private System.Collections.Generic.List<SeatLine> TakeLines(string result) =>
        _log.Lines.Where(l => l.Id == SeatChecks.TakeId && l.Result == result).Select(l => new SeatLine(l.Key, l.Detail)).ToList();

    private SeatLine TakeLine(string result) => TakeLines(result).Single();

    private readonly record struct SeatLine(string Key, string Detail);

    // ---- The message at the fall ----

    [TestMethod]
    public void TheFall_WhileSeated_SaysOnceThatTheTakeKeyIsAvailable()
    {
        Seated();

        TheHeroFalls();
        _sut.OnMainAgentChanged();
        _sut.OnMissionTick();

        Assert.AreEqual(1, _screen.Messages.Count(m => m == SeatMessage.HeroFell));
    }

    [TestMethod]
    public void TheFall_SaysNothing_WhereTheTakeIsNotAllowed()
    {
        _permissions = NoTake;
        Seated();

        TheHeroFalls();

        Assert.AreEqual(0, _screen.Messages.Count(m => m == SeatMessage.HeroFell));
    }

    [TestMethod]
    public void TheFall_SaysNothing_WithTheSettingOff()
    {
        _settings.TakeSoldierAfterFall = false;
        Seated();

        TheHeroFalls();

        Assert.AreEqual(0, _screen.Messages.Count(m => m == SeatMessage.HeroFell));
    }

    [TestMethod]
    public void AMainAgentChange_WithTheHeroStillThere_SaysNothing()
    {
        Seated();

        _sut.OnMainAgentChanged();
        _sut.OnMissionTick();

        Assert.AreEqual(0, _screen.Messages.Count(m => m == SeatMessage.HeroFell));
    }

    [TestMethod]
    public void TheFall_AfterALeave_SaysNothing()
    {
        Seated();
        _sut.OnScreenTick(true);

        TheHeroFalls();

        Assert.AreEqual(0, _screen.Messages.Count(m => m == SeatMessage.HeroFell));
    }

    // ---- The press ----

    [TestMethod]
    public void PressTake_IsStored_AndActsInTheNextMissionTick()
    {
        Seated();
        TheHeroFalls();

        _sut.PressTake();
        Assert.AreEqual(0, _screen.TakeCalls, "the take writes the agent, so it waits for the mission tick");
        _sut.OnMissionTick();

        Assert.AreEqual(1, _screen.TakeCalls);
        _sut.OnMissionTick();
        Assert.AreEqual(1, _screen.TakeCalls, "a press acts once");
    }

    [TestMethod]
    public void PressTake_WhileOff_IsIgnored()
    {
        _sut.PressTake();
        _sut.OnMissionTick();

        Assert.AreEqual(0, _screen.TakeCalls);
    }

    [TestMethod]
    public void PressTake_DuringTheRise_IsStoredAndActs()
    {
        _sut.OnScreenTick(true);
        Assert.AreEqual(SeatState.Rising, _sut.State);

        _sut.PressTake();
        _sut.OnMissionTick();

        Assert.AreEqual(1, _screen.TakeCalls, "key 532 acts whenever the state is not Off");
    }

    [TestMethod]
    public void PressTake_InPhotoMode_IsDropped()
    {
        Seated();
        _screen.Facts = _screen.Facts with { PhotoMode = true };

        _sut.PressTake();
        _screen.Facts = _screen.Facts with { PhotoMode = false };
        _sut.OnMissionTick();

        Assert.AreEqual(0, _screen.TakeCalls);
    }

    [TestMethod]
    public void PressTake_ThenPhotoModeStartsBeforeTheTick_IsDropped()
    {
        Seated();
        _sut.PressTake();
        _screen.Facts = _screen.Facts with { PhotoMode = true };

        _sut.OnMissionTick();

        Assert.AreEqual(0, _screen.TakeCalls);
    }

    [TestMethod]
    public void PressTake_ThatLeavesTheSeatBeforeTheTick_IsDropped()
    {
        Seated();
        _sut.PressTake();
        _sut.OnScreenTick(true);

        _sut.OnMissionTick();

        Assert.AreEqual(0, _screen.TakeCalls);
    }

    [TestMethod]
    public void PressTake_AfterAFault_IsIgnored()
    {
        Seated();
        _screen.ApplyThrows = new InvalidOperationException("boom");
        _sut.OnFrame(0.1f);

        _sut.PressTake();
        _sut.OnMissionTick();

        Assert.IsTrue(_sut.Faulted);
        Assert.AreEqual(0, _screen.TakeCalls);
    }

    // ---- The answer ----

    [TestMethod]
    public void ATake_WritesOnePassWithTheKindAndTheFormationsHandedOver()
    {
        Seated();
        TheHeroFalls();
        _screen.TakeResult = SeatTakeResult.Taken(SeatTakeKind.Soldier, 3);

        _sut.PressTake();
        _sut.OnMissionTick();

        var line = TakeLine("pass");
        StringAssert.Contains(line.Detail, "kind=Soldier");
        StringAssert.EndsWith(line.Detail, "formations=3");
    }

    [TestMethod]
    public void ATakeOfAHero_NamesTheKind()
    {
        Seated();
        TheHeroFalls();
        _screen.TakeResult = SeatTakeResult.Taken(SeatTakeKind.Hero, 1);

        _sut.PressTake();
        _sut.OnMissionTick();

        StringAssert.Contains(TakeLine("pass").Detail, "kind=Hero");
    }

    [TestMethod]
    public void ATake_AfterTheTakenSoldierFalls_SaysTheFallAgain()
    {
        Seated();
        TheHeroFalls();
        _sut.PressTake();
        _sut.OnMissionTick();
        _hero.Active = true;
        _sut.OnMainAgentChanged();
        _sut.OnMissionTick();
        var said = _screen.Messages.Count(m => m == SeatMessage.HeroFell);

        TheHeroFalls();

        Assert.AreEqual(said + 1, _screen.Messages.Count(m => m == SeatMessage.HeroFell), "a second fall is a new fall");
    }

    [TestMethod]
    public void NobodyThere_WritesASkipAndOneMessage()
    {
        Seated();
        TheHeroFalls();
        _screen.TakeResult = SeatTakeResult.Nobody();

        _sut.PressTake();
        _sut.OnMissionTick();

        StringAssert.Contains(TakeLine("skip").Detail, "reason=nobody");
        Assert.AreEqual(1, _screen.Messages.Count(m => m == SeatMessage.TakeNobody));
        Assert.AreEqual(0, TakeLines("pass").Count);
    }

    [TestMethod]
    public void AHeroWhoStillStands_WritesASkipAndOneMessage()
    {
        Seated();
        _screen.TakeResult = SeatTakeResult.HeroAlive();

        _sut.PressTake();
        _sut.OnMissionTick();

        StringAssert.Contains(TakeLine("skip").Detail, "reason=hero-alive");
        Assert.AreEqual(1, _screen.Messages.Count(m => m == SeatMessage.TakeHeroAlive));
    }

    [DataTestMethod]
    [DataRow((int)SeatTakeRefusal.NotHuman, "not-human")]
    [DataRow((int)SeatTakeRefusal.OtherTeam, "other-team")]
    [DataRow((int)SeatTakeRefusal.Hero, "hero")]
    [DataRow((int)SeatTakeRefusal.Wounded, "wounded")]
    [DataRow((int)SeatTakeRefusal.PlatformCrew, "platform-crew")]
    [DataRow((int)SeatTakeRefusal.NoFormation, "no-formation")]
    public void AnIneligibleSoldier_WritesASkipWithTheReason_AndOneMessage(int refusal, string reason)
    {
        Seated();
        TheHeroFalls();
        _screen.TakeResult = SeatTakeResult.Refused((SeatTakeRefusal)refusal);

        _sut.PressTake();
        _sut.OnMissionTick();

        StringAssert.EndsWith(TakeLine("skip").Key, ":" + reason);
        StringAssert.Contains(TakeLine("skip").Detail, "reason=" + reason);
        Assert.AreEqual(1, _screen.Messages.Count(m => m == SeatMessage.TakeRefused));
    }

    [TestMethod]
    public void WhereThePolicyAllowsNoTake_NothingIsAsked_AndASkipSaysDenied()
    {
        _permissions = NoTake;
        Seated();
        TheHeroFalls();

        _sut.PressTake();
        _sut.OnMissionTick();

        Assert.AreEqual(0, _screen.TakeCalls);
        StringAssert.Contains(TakeLine("skip").Detail, "reason=denied");
    }

    [TestMethod]
    public void WithTheSettingOff_NothingIsAsked_AndASkipSaysSettingOff()
    {
        _settings.TakeSoldierAfterFall = false;
        Seated();
        TheHeroFalls();

        _sut.PressTake();
        _sut.OnMissionTick();

        Assert.AreEqual(0, _screen.TakeCalls);
        StringAssert.Contains(TakeLine("skip").Detail, "reason=setting-off");
    }

    [TestMethod]
    public void ThePolicyAndTheSetting_AreReadWhenThePressActs_NotWhenItIsStored()
    {
        Seated();
        TheHeroFalls();
        _sut.PressTake();

        _permissions = NoTake;
        _sut.OnMissionTick();

        Assert.AreEqual(0, _screen.TakeCalls);
    }

    [TestMethod]
    public void ATakeThatThrows_LatchesTheFault()
    {
        Seated();
        TheHeroFalls();
        _screen.TakeThrows = new InvalidOperationException("boom");

        _sut.PressTake();
        _sut.OnMissionTick();

        Assert.IsTrue(_sut.Faulted);
        Assert.AreEqual(SeatState.Off, _sut.State);
    }

    [TestMethod]
    public void TheTake_NeverTouchesTheHandOffOrTheStandFastOfTheDeadHero()
    {
        Seated();
        TheHeroFalls();
        var begins = _hero.Begins;

        _sut.PressTake();
        _sut.OnMissionTick();

        Assert.AreEqual(begins, _hero.Begins);
        Assert.AreEqual(0, _hero.Ends);
    }
}
