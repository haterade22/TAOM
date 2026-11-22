using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Features.TrainingGrounds.Waves;
using TAOM.Features.TrainingGrounds.Waves.Domain;

namespace TAOM.Tests.Features.TrainingGrounds.Waves;

/// <summary>The campaign side of the wave fight (#1529): the offer gates, the stake, the money moves and the lifecycle.</summary>
[TestClass]
public class WaveBetServiceTests
{
    private WaveBetFixture _f = null!;

    private WaveBetService Bets => _f.Service;

    [TestInitialize]
    public void Setup() => _f = new WaveBetFixture();

    // ---- the offer: one test moves each field the refusal reads

    [TestMethod]
    public void Refusal_EveryGateOpen_IsNone_AndTheOfferShows()
    {
        Assert.AreEqual(OfferRefusal.None, Bets.Refusal);
        Assert.IsTrue(Bets.ShowsOffer);
    }

    [TestMethod]
    public void Refusal_SwitchOff_IsDisabled_AndTheOfferHides()
    {
        _f.Settings.Enabled.Returns(false);

        Assert.AreEqual(OfferRefusal.Disabled, Bets.Refusal);
        Assert.IsFalse(Bets.ShowsOffer);
    }

    [TestMethod]
    public void Refusal_TournamentCapBelowTheMinimumStake_IsDisabled_BecauseNoStakeExists()
    {
        _f.Tournament.MaxBetPerRound.Returns(5);

        Assert.AreEqual(0, Bets.StakeChoices.Count);
        Assert.AreEqual(OfferRefusal.Disabled, Bets.Refusal);
    }

    [TestMethod]
    public void Refusal_CoopSessionActive_IsNotSinglePlayer()
    {
        _f.Coop.IsSessionActive.Returns(true);

        Assert.AreEqual(OfferRefusal.NotSinglePlayer, Bets.Refusal);
        Assert.IsFalse(Bets.ShowsOffer);
    }

    [TestMethod]
    public void Refusal_CoopDefersToTheHost_IsNotSinglePlayer()
    {
        _f.Coop.ShouldDeferToHost.Returns(true);

        Assert.AreEqual(OfferRefusal.NotSinglePlayer, Bets.Refusal);
    }

    [TestMethod]
    public void Refusal_DedicatedServer_IsNotSinglePlayer()
    {
        _f.Dedicated.IsDedicatedServer.Returns(true);

        Assert.AreEqual(OfferRefusal.NotSinglePlayer, Bets.Refusal);
    }

    [TestMethod]
    public void Refusal_NotTheAuthority_IsNotSinglePlayer()
    {
        _f.Coop.IsAuthority.Returns(false);

        Assert.AreEqual(OfferRefusal.NotSinglePlayer, Bets.Refusal);
    }

    [TestMethod]
    public void Refusal_ArenaNotAttached_IsNotArena_AndTheOfferHides()
    {
        Bets.DetachArena();

        Assert.AreEqual(OfferRefusal.NotArena, Bets.Refusal);
        Assert.IsFalse(Bets.ShowsOffer);
    }

    [TestMethod]
    public void Refusal_PartnerIsNotThePracticeMaster_IsNotArena()
    {
        _f.Adapter.TalkingToPracticeMaster.Returns(false);

        Assert.AreEqual(OfferRefusal.NotArena, Bets.Refusal);
    }

    [TestMethod]
    public void Refusal_NoTown_IsNotArena()
    {
        _f.Adapter.TownId.Returns((string?)null);

        Assert.AreEqual(OfferRefusal.NotArena, Bets.Refusal);
    }

    [TestMethod]
    public void Refusal_VanillaPracticeDebriefPending_IsPractising_AndTheOfferHides()
    {
        _f.Adapter.VanillaAfterPractice.Returns(true);

        Assert.AreEqual(OfferRefusal.Practising, Bets.Refusal);
        Assert.IsFalse(Bets.ShowsOffer);
    }

    [TestMethod]
    public void Refusal_TownHoldsATournament_IsTournament_ShownButRefused()
    {
        _f.Adapter.TownHasTournament.Returns(true);

        Assert.AreEqual(OfferRefusal.Tournament, Bets.Refusal);
        Assert.IsTrue(Bets.ShowsOffer);
    }

    [TestMethod]
    public void Refusal_PlayerWounded_IsWounded()
    {
        _f.Adapter.PlayerWounded.Returns(true);

        Assert.AreEqual(OfferRefusal.Wounded, Bets.Refusal);
        Assert.IsTrue(Bets.ShowsOffer);
    }

    [TestMethod]
    public void Refusal_PlayerDisguised_IsDisguised()
    {
        _f.Adapter.PlayerDisguised.Returns(true);

        Assert.AreEqual(OfferRefusal.Disguised, Bets.Refusal);
    }

    [TestMethod]
    public void Refusal_WoundedAndDisguised_NamesTheWoundFirst()
    {
        _f.Adapter.PlayerWounded.Returns(true);
        _f.Adapter.PlayerDisguised.Returns(true);

        Assert.AreEqual(OfferRefusal.Wounded, Bets.Refusal);
    }

    [TestMethod]
    public void Refusal_TheTownRanTodayAndTheRuleIsOn_IsRanToday()
    {
        Bets.Ledger.Record(WaveBetFixture.TownId, 100);

        Assert.AreEqual(OfferRefusal.RanToday, Bets.Refusal);
        Assert.IsTrue(Bets.ShowsOffer);
    }

    [TestMethod]
    public void Refusal_TheTownRanOnAnEarlierDay_IsNone()
    {
        Bets.Ledger.Record(WaveBetFixture.TownId, 99);

        Assert.AreEqual(OfferRefusal.None, Bets.Refusal);
    }

    [TestMethod]
    public void Refusal_AnotherTownRanToday_IsNone()
    {
        Bets.Ledger.Record("town_b", 100);

        Assert.AreEqual(OfferRefusal.None, Bets.Refusal);
    }

    [TestMethod]
    public void Refusal_TheTownRanTodayButTheRuleIsOff_IsNone()
    {
        var rules = new WaveRules(10, 15_000, false, 5, 3);
        _f.Config.GetTable().Returns(new WaveTable(WaveTable.Default.Waves, rules));
        Bets.Ledger.Record(WaveBetFixture.TownId, 100);

        Assert.AreEqual(OfferRefusal.None, Bets.Refusal);
    }

    [TestMethod]
    public void Refusal_PurseJustUnderTheSmallestStakeOnOffer_IsPoor()
    {
        var smallest = Bets.StakeChoices[0];
        Assert.AreEqual(120, smallest, "the default table's wave 1 offers 120 as its smallest stake");
        _f.Purse.Gold = smallest - 1;

        Assert.AreEqual(OfferRefusal.Poor, Bets.Refusal);
        Assert.IsTrue(Bets.ShowsOffer);
    }

    [TestMethod]
    public void Refusal_PurseTenDenarsWhenTheSmallestStakeIsMore_IsPoor_NotAnOpenOffer()
    {
        _f.Purse.Gold = 10;

        Assert.AreEqual(OfferRefusal.Poor, Bets.Refusal);
    }

    [TestMethod]
    public void Refusal_PurseExactlyTheSmallestStakeOnOffer_IsNone()
    {
        _f.Purse.Gold = Bets.StakeChoices[0];

        Assert.AreEqual(OfferRefusal.None, Bets.Refusal);
    }

    [TestMethod]
    public void SmallestStake_IsTheFirstStakeOnOffer_NotTheTableMinimum()
    {
        Assert.AreEqual(Bets.StakeChoices[0], Bets.SmallestStake);
        Assert.AreNotEqual(WaveRules.Default.MinStake, Bets.SmallestStake);
    }

    [TestMethod]
    public void Refusal_LogsTheOfferAsShown_OrSkippedWithItsReason()
    {
        _ = Bets.ShowsOffer;
        _f.Adapter.PlayerWounded.Returns(true);
        _ = Bets.ShowsOffer;

        _f.Outcomes.Received().Pass(TrainingWavesChecks.Offer, Arg.Any<string>(), Arg.Any<string>());
        _f.Outcomes.Received().Skip(TrainingWavesChecks.Offer, Arg.Any<string>(), Arg.Is<string>(d => d.Contains("reason=Wounded")));
    }

    // ---- stake choices

    [TestMethod]
    public void StakeChoices_FirstWave_AreAQuarterAHalfAndTheCap()
    {
        CollectionAssert.AreEqual(new[] { 120, 250, 500 }, Bets.StakeChoices.ToArray());
    }

    [TestMethod]
    public void StakeChoices_PerkFactorTwo_DoublesTheCap()
    {
        _f.Adapter.DeepPocketsFactor.Returns(2);
        Bets.PrepareRun();

        CollectionAssert.AreEqual(new[] { 250, 500, 1000 }, Bets.StakeChoices.ToArray());
    }

    [TestMethod]
    public void StakeChoices_TournamentCapOf300_HoldsTheFirstWaveDown()
    {
        _f.Tournament.MaxBetPerRound.Returns(300);

        CollectionAssert.AreEqual(new[] { 70, 150, 300 }, Bets.StakeChoices.ToArray());
    }

    [TestMethod]
    public void StakeChoices_ThenTheRunMovesToWaveTwo_RaiseTheCap()
    {
        _f.Fighting(500);
        Bets.PayClearedWave();

        CollectionAssert.AreEqual(new[] { 250, 500, 1000 }, Bets.StakeChoices.ToArray());
    }

    // ---- PrepareRun

    [TestMethod]
    public void PrepareRun_NoRun_MakesAnIdleRunAtWaveOne()
    {
        Assert.IsNull(Bets.Run);

        var run = Bets.PrepareRun();

        Assert.AreSame(run, Bets.Run);
        Assert.AreEqual(WaveRunState.Idle, run.State);
    }

    [TestMethod]
    public void PrepareRun_ARunInProgress_IsKept()
    {
        _f.Fighting();
        var run = Bets.Run;

        Assert.AreSame(run, Bets.PrepareRun());
    }

    [TestMethod]
    public void PrepareRun_AnEndedRun_IsReplacedByAFreshOne()
    {
        var first = Bets.PrepareRun();
        Bets.Withdraw();

        var second = Bets.PrepareRun();

        Assert.AreNotSame(first, second);
        Assert.AreEqual(WaveRunState.Idle, second.State);
    }

    // ---- PlaceStake

    [TestMethod]
    public void PlaceStake_ChargesTheStake_AndTheRunIsWaveDue()
    {
        Bets.PrepareRun();

        Assert.IsTrue(Bets.PlaceStake(300));

        Assert.AreEqual(99_700, _f.Purse.Gold);
        Assert.AreEqual(WaveRunState.WaveDue, Bets.Run!.State);
        Assert.AreEqual(300, Bets.Run.Stake);
    }

    [TestMethod]
    public void PlaceStake_WithNoRunYet_StartsOne()
    {
        Assert.IsTrue(Bets.PlaceStake(300));

        Assert.AreEqual(WaveRunState.WaveDue, Bets.Run!.State);
    }

    [TestMethod]
    public void PlaceStake_GoldFellByExactlyTheStake_LogsThePass()
    {
        Bets.PlaceStake(300);

        _f.Outcomes.Received().Pass(TrainingWavesChecks.Stake, Arg.Any<string>(), Arg.Any<string>());
        _f.Outcomes.DidNotReceive().Fail(TrainingWavesChecks.Stake, Arg.Any<string>(), Arg.Any<string>());
    }

    [TestMethod]
    public void PlaceStake_GoldFellByAnotherAmount_LogsTheFail_NotThePass()
    {
        _f.Purse.ChargeTakes = 299;

        Bets.PlaceStake(300);

        _f.Outcomes.Received().Fail(TrainingWavesChecks.Stake, Arg.Any<string>(), Arg.Any<string>());
        _f.Outcomes.DidNotReceive().Pass(TrainingWavesChecks.Stake, Arg.Any<string>(), Arg.Any<string>());
    }

    [TestMethod]
    public void PlaceStake_TheChargeIsRefused_ChangesNothing()
    {
        _f.Purse.ChargeRefused = true;

        Assert.IsFalse(Bets.PlaceStake(300));

        Assert.AreEqual(100_000, _f.Purse.Gold);
        Assert.IsTrue(Bets.Run == null || Bets.Run.State == WaveRunState.Idle);
        Assert.IsFalse(Bets.Ledger.RanToday(WaveBetFixture.TownId, 100));
        _f.Outcomes.DidNotReceive().Pass(TrainingWavesChecks.Stake, Arg.Any<string>(), Arg.Any<string>());
    }

    [TestMethod]
    public void PlaceStake_AboveTheCap_IsRefused_NothingCharged()
    {
        Assert.IsFalse(Bets.PlaceStake(501));

        Assert.AreEqual(100_000, _f.Purse.Gold);
    }

    [TestMethod]
    public void PlaceStake_UnderTheMinimum_IsRefused_NothingCharged()
    {
        Assert.IsFalse(Bets.PlaceStake(9));

        Assert.AreEqual(100_000, _f.Purse.Gold);
    }

    [TestMethod]
    public void PlaceStake_AboveThePurse_IsRefused_NothingCharged()
    {
        _f.Purse.Gold = 200;

        Assert.IsFalse(Bets.PlaceStake(250));

        Assert.AreEqual(200, _f.Purse.Gold);
    }

    [TestMethod]
    public void PlaceStake_ReChecksTheSwitch()
    {
        Bets.PrepareRun();
        _f.Settings.Enabled.Returns(false);

        Assert.IsFalse(Bets.PlaceStake(300));
        Assert.AreEqual(100_000, _f.Purse.Gold);
    }

    [TestMethod]
    public void PlaceStake_ReChecksTheCoopGate()
    {
        _f.Coop.IsSessionActive.Returns(true);

        Assert.IsFalse(Bets.PlaceStake(300));
        Assert.AreEqual(100_000, _f.Purse.Gold);
    }

    [TestMethod]
    public void PlaceStake_ReChecksTheArena()
    {
        Bets.DetachArena();

        Assert.IsFalse(Bets.PlaceStake(300));
        Assert.AreEqual(100_000, _f.Purse.Gold);
    }

    [TestMethod]
    public void PlaceStake_ReChecksTheTournament_TheWound_TheDisguise()
    {
        _f.Adapter.TownHasTournament.Returns(true);
        Assert.IsFalse(Bets.PlaceStake(300));
        _f.Adapter.TownHasTournament.Returns(false);

        _f.Adapter.PlayerWounded.Returns(true);
        Assert.IsFalse(Bets.PlaceStake(300));
        _f.Adapter.PlayerWounded.Returns(false);

        _f.Adapter.PlayerDisguised.Returns(true);
        Assert.IsFalse(Bets.PlaceStake(300));
        Assert.AreEqual(100_000, _f.Purse.Gold);
    }

    [TestMethod]
    public void PlaceStake_ReChecksThePracticeDebrief()
    {
        _f.Adapter.VanillaAfterPractice.Returns(true);

        Assert.IsFalse(Bets.PlaceStake(300));
    }

    [TestMethod]
    public void PlaceStake_WhenTheTownRanToday_IsRefused()
    {
        Bets.Ledger.Record(WaveBetFixture.TownId, 100);

        Assert.IsFalse(Bets.PlaceStake(300));
        Assert.AreEqual(100_000, _f.Purse.Gold);
    }

    [TestMethod]
    public void PlaceStake_WaveAlreadyDue_IsRefused_NotChargedTwice()
    {
        Bets.PlaceStake(300);

        Assert.IsFalse(Bets.PlaceStake(300));

        Assert.AreEqual(99_700, _f.Purse.Gold);
    }

    [TestMethod]
    public void PlaceStake_FirstStakeOfTheRun_RecordsTheDay()
    {
        Bets.PlaceStake(300);

        Assert.IsTrue(Bets.Ledger.RanToday(WaveBetFixture.TownId, 100));
    }

    [TestMethod]
    public void PlaceStake_LaterWave_DoesNotRecordTheDayAgain()
    {
        _f.Fighting(500);
        Bets.PayClearedWave();
        _f.Adapter.Today.Returns(101);

        Assert.IsTrue(Bets.PlaceStake(500));

        Assert.IsFalse(Bets.Ledger.RanToday(WaveBetFixture.TownId, 101), "only the first stake of a run records the day");
        Assert.IsTrue(Bets.Ledger.RanToday(WaveBetFixture.TownId, 100));
    }

    [TestMethod]
    public void PlaceStake_LaterWave_IsNotRefusedByTheDayRule()
    {
        _f.Fighting(500);
        Bets.PayClearedWave();

        Assert.IsTrue(Bets.PlaceStake(500));
        Assert.AreEqual(WaveRunState.WaveDue, Bets.Run!.State);
    }

    // ---- the money moves

    [TestMethod]
    public void PayClearedWave_GrantsThePayout_AndMovesToTheNextWave()
    {
        _f.Fighting(333);

        var paid = Bets.PayClearedWave();

        Assert.AreEqual(499, paid);
        Assert.AreEqual(100_000 - 333 + 499, _f.Purse.Gold);
        Assert.AreEqual(WaveRunState.BetweenWaves, Bets.Run!.State);
    }

    [TestMethod]
    public void PayClearedWave_GoldRoseByExactlyThePayout_LogsThePass()
    {
        _f.Fighting(333);

        Bets.PayClearedWave();

        _f.Outcomes.Received().Pass(TrainingWavesChecks.Payout, Arg.Any<string>(), Arg.Any<string>());
    }

    [TestMethod]
    public void PayClearedWave_GoldRoseByAnotherAmount_LogsTheFail()
    {
        _f.Fighting(333);
        _f.Purse.GrantAdds = 1;

        Bets.PayClearedWave();

        _f.Outcomes.Received().Fail(TrainingWavesChecks.Payout, Arg.Any<string>(), Arg.Any<string>());
        _f.Outcomes.DidNotReceive().Pass(TrainingWavesChecks.Payout, Arg.Any<string>(), Arg.Any<string>());
    }

    [TestMethod]
    public void PayClearedWave_NotFighting_PaysNothing()
    {
        Bets.PrepareRun();

        Assert.AreEqual(0, Bets.PayClearedWave());
        Assert.AreEqual(100_000, _f.Purse.Gold);
    }

    [TestMethod]
    public void PayClearedWave_TheLastWave_EndsTheRunAsCompleted()
    {
        var run = Bets.PrepareRun();
        while (run.State != WaveRunState.Ended)
        {
            Assert.IsTrue(Bets.PlaceStake(run.MaxStake));
            run.BeginFight();
            Bets.PayClearedWave();
        }

        Assert.AreEqual(WaveRunEnd.Completed, run.EndReason);
        Assert.AreEqual(100_000 + 54_200, _f.Purse.Gold);
    }

    [TestMethod]
    public void LoseWave_GivesTheStakeToTheTown_AndEndsTheRun()
    {
        _f.Fighting(250);

        var lost = Bets.LoseWave();

        Assert.AreEqual(250, lost);
        _f.Adapter.Received(1).GiveToTown(250);
        Assert.AreEqual(WaveRunEnd.Lost, Bets.Run!.EndReason);
        Assert.AreEqual(99_750, _f.Purse.Gold);
    }

    [TestMethod]
    public void LoseWave_NotFighting_ChangesNothing()
    {
        Bets.PrepareRun();

        Assert.AreEqual(0, Bets.LoseWave());
        _f.Adapter.DidNotReceiveWithAnyArgs().GiveToTown(default);
    }

    [TestMethod]
    public void Forfeit_GivesTheStakeToTheTown_AndLogsThePass()
    {
        _f.Fighting(250);

        var lost = Bets.Forfeit();

        Assert.AreEqual(250, lost);
        _f.Adapter.Received(1).GiveToTown(250);
        Assert.AreEqual(WaveRunEnd.Forfeited, Bets.Run!.EndReason);
        _f.Outcomes.Received().Pass(TrainingWavesChecks.Forfeit, Arg.Any<string>(), Arg.Any<string>());
    }

    [TestMethod]
    public void Forfeit_TheTownTakesNothing_LogsTheFail()
    {
        _f.Fighting(250);
        _f.Adapter.GiveToTown(Arg.Any<int>()).Returns(false);

        Bets.Forfeit();

        _f.Outcomes.Received().Fail(TrainingWavesChecks.Forfeit, Arg.Any<string>(), Arg.Any<string>());
        _f.Outcomes.DidNotReceive().Pass(TrainingWavesChecks.Forfeit, Arg.Any<string>(), Arg.Any<string>());
    }

    [TestMethod]
    public void Refund_ReturnsTheStakeToThePurse()
    {
        Bets.PlaceStake(400);

        var back = Bets.Refund();

        Assert.AreEqual(400, back);
        Assert.AreEqual(100_000, _f.Purse.Gold);
        Assert.AreEqual(WaveRunEnd.Refunded, Bets.Run!.EndReason);
        _f.Adapter.DidNotReceiveWithAnyArgs().GiveToTown(default);
    }

    [TestMethod]
    public void Refund_TheFirstWaveNeverStarted_GivesTheTownItsDayBack()
    {
        Bets.PlaceStake(400);
        Assert.IsTrue(Bets.Ledger.RanToday(WaveBetFixture.TownId, 100));

        Bets.Refund();

        Assert.IsFalse(Bets.Ledger.RanToday(WaveBetFixture.TownId, 100), "a run that fought no wave must not use up the town's day");
    }

    [TestMethod]
    public void AbortFight_InTheFirstWave_GivesTheTownItsDayBack()
    {
        _f.Fighting(250);

        Bets.AbortFight();

        Assert.IsFalse(Bets.Ledger.RanToday(WaveBetFixture.TownId, 100));
    }

    [TestMethod]
    public void AbortFight_InWaveTwoOrLater_KeepsTheTownsDay()
    {
        _f.Fighting(250);
        Bets.PayClearedWave();
        Bets.PlaceStake(400);
        Bets.Run!.BeginFight();
        Assert.AreEqual(2, Bets.Run.Wave);

        Bets.AbortFight();

        Assert.IsTrue(Bets.Ledger.RanToday(WaveBetFixture.TownId, 100), "a wave was cleared, so the day stays used");
    }

    [TestMethod]
    public void Refund_AfterAClearedWave_KeepsTheDay()
    {
        _f.Fighting(250);
        Bets.PayClearedWave();
        Bets.PlaceStake(400);

        Bets.Refund();

        Assert.IsTrue(Bets.Ledger.RanToday(WaveBetFixture.TownId, 100));
    }

    [TestMethod]
    public void AbortFight_Fighting_ReturnsTheStakeToThePurse_AndTheTownGetsNothing()
    {
        _f.Fighting(250);

        var back = Bets.AbortFight();

        Assert.AreEqual(250, back);
        Assert.AreEqual(100_000, _f.Purse.Gold);
        Assert.AreEqual(WaveRunEnd.Refunded, Bets.Run!.EndReason);
        _f.Adapter.DidNotReceiveWithAnyArgs().GiveToTown(default);
        _f.Outcomes.DidNotReceive().Pass(TrainingWavesChecks.Forfeit, Arg.Any<string>(), Arg.Any<string>());
    }

    [TestMethod]
    public void AbortFight_Fighting_LogsTheRunEndWithADeltaOfZero()
    {
        _f.Fighting(250);

        Bets.AbortFight();

        _f.Outcomes.Received().Pass(TrainingWavesChecks.RunEnd, Arg.Any<string>(), Arg.Is<string>(d => d.Contains("delta=0")));
    }

    [TestMethod]
    public void AbortFight_NotFighting_ChangesNothing()
    {
        Bets.PlaceStake(400);

        Assert.AreEqual(0, Bets.AbortFight());
        Assert.AreEqual(WaveRunState.WaveDue, Bets.RunState);
        Assert.AreEqual(99_600, _f.Purse.Gold);
    }

    [TestMethod]
    public void Refund_NothingDue_ChangesNothing()
    {
        Bets.PrepareRun();

        Assert.AreEqual(0, Bets.Refund());
        Assert.AreEqual(100_000, _f.Purse.Gold);
    }

    [TestMethod]
    public void Withdraw_BetweenWaves_EndsTheRun_AndLogsTheRunEndWithTheNet()
    {
        _f.Fighting(500);
        Bets.PayClearedWave();

        Assert.IsTrue(Bets.Withdraw());

        Assert.AreEqual(WaveRunEnd.Withdrew, Bets.Run!.EndReason);
        _f.Outcomes.Received().Pass(TrainingWavesChecks.RunEnd, Arg.Any<string>(), Arg.Is<string>(d => d.Contains("net=250")));
    }

    [TestMethod]
    public void RunEnd_GoldDeltaDiffersFromTheNet_LogsTheFail()
    {
        _f.Fighting(500);
        Bets.PayClearedWave();
        _f.Purse.Gold -= 7;

        Bets.Withdraw();

        _f.Outcomes.Received().Fail(TrainingWavesChecks.RunEnd, Arg.Any<string>(), Arg.Any<string>());
        _f.Outcomes.DidNotReceive().Pass(TrainingWavesChecks.RunEnd, Arg.Any<string>(), Arg.Any<string>());
    }

    [TestMethod]
    public void Withdraw_DuringAFight_IsRefused()
    {
        _f.Fighting();

        Assert.IsFalse(Bets.Withdraw());
        Assert.AreEqual(WaveRunState.Fighting, Bets.Run!.State);
    }

    [TestMethod]
    public void Withdraw_NoRun_IsRefused()
    {
        Assert.IsFalse(Bets.Withdraw());
    }

    // ---- the debrief and the end of a conversation

    [TestMethod]
    public void TakeDebrief_AfterMarkDebrief_IsTrueOnce()
    {
        Bets.MarkDebrief();

        Assert.IsTrue(Bets.DebriefPending);
        Assert.IsTrue(Bets.TakeDebrief());
        Assert.IsFalse(Bets.DebriefPending);
        Assert.IsFalse(Bets.TakeDebrief());
    }

    [TestMethod]
    public void TakeDebrief_VanillaAfterPracticeIsFalse_LogsThePass()
    {
        Bets.MarkDebrief();

        Bets.TakeDebrief();

        _f.Outcomes.Received().Pass(TrainingWavesChecks.Debrief, Arg.Any<string>(), Arg.Any<string>());
    }

    [TestMethod]
    public void TakeDebrief_VanillaAfterPracticeIsTrue_LogsTheFail()
    {
        _f.Adapter.VanillaAfterPractice.Returns(true);
        Bets.MarkDebrief();

        Bets.TakeDebrief();

        _f.Outcomes.Received().Fail(TrainingWavesChecks.Debrief, Arg.Any<string>(), Arg.Any<string>());
        _f.Outcomes.DidNotReceive().Pass(TrainingWavesChecks.Debrief, Arg.Any<string>(), Arg.Any<string>());
    }

    [TestMethod]
    public void OnConversationEnded_BetweenWaves_Withdraws()
    {
        _f.Fighting(500);
        Bets.PayClearedWave();

        Bets.OnConversationEnded();

        Assert.AreEqual(WaveRunEnd.Withdrew, Bets.Run!.EndReason);
    }

    [TestMethod]
    public void OnConversationEnded_WaveDue_DoesNotWithdraw()
    {
        Bets.PlaceStake(300);

        Bets.OnConversationEnded();

        Assert.AreEqual(WaveRunState.WaveDue, Bets.Run!.State);
    }

    [TestMethod]
    public void OnConversationEnded_Idle_DoesNotWithdraw()
    {
        Bets.PrepareRun();

        Bets.OnConversationEnded();

        Assert.AreEqual(WaveRunState.Idle, Bets.Run!.State);
    }

    [TestMethod]
    public void OnConversationEnded_AlwaysClearsTheDebriefFlag()
    {
        Bets.MarkDebrief();

        Bets.OnConversationEnded();

        Assert.IsFalse(Bets.DebriefPending);
    }

    [TestMethod]
    public void OnConversationEnded_NoRun_IsSafe()
    {
        Bets.OnConversationEnded();

        Assert.IsNull(Bets.Run);
    }

    // ---- lifecycle

    [TestMethod]
    public void AttachArena_StartsWithNoRun_AndTheArenaAttached()
    {
        _f.Fighting();

        Bets.AttachArena();

        Assert.IsTrue(Bets.ArenaAttached);
        Assert.IsNull(Bets.Run);
        Assert.IsFalse(Bets.DebriefPending);
    }

    [TestMethod]
    public void DetachArena_Twice_IsSafe()
    {
        Bets.DetachArena();
        Bets.DetachArena();

        Assert.IsFalse(Bets.ArenaAttached);
    }

    [TestMethod]
    public void DetachArena_WaveDue_RefundsTheStake()
    {
        Bets.PlaceStake(300);

        Bets.DetachArena();

        Assert.AreEqual(100_000, _f.Purse.Gold);
        Assert.AreEqual(WaveRunEnd.Refunded, Bets.Run!.EndReason);
    }

    [TestMethod]
    public void DetachArena_Fighting_ForfeitsTheStake()
    {
        _f.Fighting(300);

        Bets.DetachArena();

        _f.Adapter.Received(1).GiveToTown(300);
        Assert.AreEqual(WaveRunEnd.Forfeited, Bets.Run!.EndReason);
    }

    [TestMethod]
    public void DetachArena_BetweenWaves_Withdraws()
    {
        _f.Fighting(500);
        Bets.PayClearedWave();

        Bets.DetachArena();

        Assert.AreEqual(WaveRunEnd.Withdrew, Bets.Run!.EndReason);
    }

    [TestMethod]
    public void DetachArena_AfterTheDirectorSettledTheRun_PaysNothingTwice()
    {
        Bets.PlaceStake(300);
        Bets.Refund();

        Bets.DetachArena();

        Assert.AreEqual(100_000, _f.Purse.Gold);
    }

    [TestMethod]
    public void ResetForNewSession_ASecondCampaignInOneProcess_StartsClean()
    {
        _f.Fighting();
        Bets.MarkDebrief();
        Bets.Ledger.Record(WaveBetFixture.TownId, 100);

        Bets.ResetForNewSession();

        Assert.IsNull(Bets.Run);
        Assert.IsFalse(Bets.ArenaAttached);
        Assert.IsFalse(Bets.DebriefPending);
        Assert.IsFalse(Bets.Ledger.RanToday(WaveBetFixture.TownId, 100));
    }

    [TestMethod]
    public void ExportDays_KeepsOnlyToday()
    {
        Bets.Ledger.Record("old", 99);
        Bets.Ledger.Record("new", 100);

        var days = Bets.ExportDays();

        CollectionAssert.AreEquivalent(new[] { "new" }, days.Keys.ToArray());
    }

    [TestMethod]
    public void ImportDays_RestoresTheLedger_AndNullLeavesItFresh()
    {
        Bets.ImportDays(new Dictionary<string, int> { [WaveBetFixture.TownId] = 100 });
        Assert.IsTrue(Bets.Ledger.RanToday(WaveBetFixture.TownId, 100));

        Bets.ImportDays(null);
        Assert.IsFalse(Bets.Ledger.RanToday(WaveBetFixture.TownId, 100));
    }
}
