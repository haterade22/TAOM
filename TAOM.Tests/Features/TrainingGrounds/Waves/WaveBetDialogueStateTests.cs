using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Features.TrainingGrounds.Waves;
using TAOM.Features.TrainingGrounds.Waves.Domain;

namespace TAOM.Tests.Features.TrainingGrounds.Waves;

/// <summary>What the master's lines read from the service: the debrief to give, the stake in each slot and the run's state.</summary>
[TestClass]
public class WaveBetDialogueStateTests
{
    private WaveBetFixture _f = null!;

    private WaveBetService Bets => _f.Service;

    [TestInitialize]
    public void Setup() => _f = new WaveBetFixture();

    // ---- the debrief

    [TestMethod]
    public void PendingDebrief_NothingMarked_IsNone()
    {
        _f.Fighting(500);
        Bets.PayClearedWave();

        Assert.AreEqual(DebriefKind.None, Bets.PendingDebrief);
    }

    [TestMethod]
    public void PendingDebrief_ClearedWave_IsCleared()
    {
        _f.Fighting(500);
        Bets.PayClearedWave();
        Bets.MarkDebrief();

        Assert.AreEqual(DebriefKind.Cleared, Bets.PendingDebrief);
    }

    [TestMethod]
    public void PendingDebrief_LostWave_IsLost()
    {
        _f.Fighting(500);
        Bets.LoseWave();
        Bets.MarkDebrief();

        Assert.AreEqual(DebriefKind.Lost, Bets.PendingDebrief);
    }

    [TestMethod]
    public void PendingDebrief_LastWaveCleared_IsCompleted()
    {
        var run = Bets.PrepareRun();
        while (run.State != WaveRunState.Ended)
        {
            Bets.PlaceStake(run.MaxStake);
            run.BeginFight();
            Bets.PayClearedWave();
        }

        Bets.MarkDebrief();

        Assert.AreEqual(DebriefKind.Completed, Bets.PendingDebrief);
    }

    [TestMethod]
    public void PendingDebrief_ForfeitedWithdrewOrRefunded_IsNone()
    {
        _f.Fighting(300);
        Bets.Forfeit();
        Bets.MarkDebrief();
        Assert.AreEqual(DebriefKind.None, Bets.PendingDebrief);

        Bets.AttachArena();
        Bets.Ledger.Clear();
        Assert.IsTrue(Bets.PlaceStake(300));
        Bets.Refund();
        Bets.MarkDebrief();
        Assert.AreEqual(DebriefKind.None, Bets.PendingDebrief);
    }

    [TestMethod]
    public void PendingDebrief_PartnerIsNotThePracticeMaster_IsNone()
    {
        _f.Fighting(500);
        Bets.PayClearedWave();
        Bets.MarkDebrief();
        _f.Adapter.TalkingToPracticeMaster.Returns(false);

        Assert.AreEqual(DebriefKind.None, Bets.PendingDebrief);
    }

    [TestMethod]
    public void PendingDebrief_AfterTheConversationEnds_IsNone()
    {
        _f.Fighting(500);
        Bets.LoseWave();
        Bets.MarkDebrief();

        Bets.OnConversationEnded();

        Assert.AreEqual(DebriefKind.None, Bets.PendingDebrief);
    }

    // ---- the stake in each slot

    [TestMethod]
    public void StakeChoice_SlotsCountFromTheTop()
    {
        Assert.AreEqual(500, Bets.StakeChoice(0));
        Assert.AreEqual(250, Bets.StakeChoice(1));
        Assert.AreEqual(120, Bets.StakeChoice(2));
    }

    [TestMethod]
    public void StakeChoice_FewerChoicesThanSlots_GivesZero()
    {
        _f.Tournament.MaxBetPerRound.Returns(10);

        Assert.AreEqual(10, Bets.StakeChoice(0));
        Assert.AreEqual(0, Bets.StakeChoice(1));
        Assert.AreEqual(0, Bets.StakeChoice(2));
    }

    [TestMethod]
    public void StakeChoice_OutOfRangeSlot_GivesZero()
    {
        Assert.AreEqual(0, Bets.StakeChoice(-1));
        Assert.AreEqual(0, Bets.StakeChoice(3));
    }

    [TestMethod]
    public void CanAfford_ComparesThePurse()
    {
        _f.Purse.Gold = 250;

        Assert.IsTrue(Bets.CanAfford(250));
        Assert.IsFalse(Bets.CanAfford(251));
    }

    // ---- the run's state

    [TestMethod]
    public void RunState_NoRun_IsNull_ThenFollowsTheRun()
    {
        Assert.IsNull(Bets.RunState);

        Bets.PrepareRun();
        Assert.AreEqual(WaveRunState.Idle, Bets.RunState);

        Bets.PlaceStake(300);
        Assert.AreEqual(WaveRunState.WaveDue, Bets.RunState);
    }
}
