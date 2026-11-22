using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TaleWorlds.CampaignSystem;
using TAOM.Features.TrainingGrounds.Waves;
using TAOM.Features.TrainingGrounds.Waves.Domain;
using TAOM.Features.TrainingGrounds.Waves.Hooks;
using TAOM.Tests.Infrastructure;

namespace TAOM.Tests.Features.TrainingGrounds.Waves;

/// <summary>
/// The dialog behavior is a thin boundary over <see cref="WaveBetService"/> and derives from the engine's campaign behavior base, so
/// constructing it needs the engine assemblies. The conditions that set a text variable are not run here (the text tests pin the
/// variables); the decisions they make, the consequences and the save and reset story are.
/// </summary>
[TestCategory("RequiresGame")]
[TestClass]
public class TrainingWavesBehaviorTests
{
    private WaveBetFixture _f = null!;
    private TrainingWavesDialogBehavior _behavior = null!;

    [TestInitialize]
    public void Setup()
    {
        _f = new WaveBetFixture();
        _behavior = new TrainingWavesDialogBehavior(_f.Service, _f.Logger);
        _f.Service.AttachArena();
    }

    private static EngineDataStore Reload(EngineDataStore saved)
    {
        saved.Loading = true;
        return saved;
    }

    // ---- reset and save

    [TestMethod]
    public void TheConstructor_ResetsTheServiceForANewSession()
    {
        _f.Fighting();
        _f.Service.Ledger.Record(WaveBetFixture.TownId, 100);
        _f.Service.MarkDebrief();

        _ = new TrainingWavesDialogBehavior(_f.Service, _f.Logger);

        Assert.IsNull(_f.Service.Run);
        Assert.IsFalse(_f.Service.ArenaAttached);
        Assert.IsFalse(_f.Service.DebriefPending);
        Assert.IsFalse(_f.Service.Ledger.RanToday(WaveBetFixture.TownId, 100));
    }

    [TestMethod]
    public void SyncData_Save_WritesOnlyTheTownsThatRanToday_UnderTheirKey()
    {
        _f.Service.Ledger.Record("old", 99);
        _f.Service.Ledger.Record("new", 100);
        var store = new EngineDataStore { Loading = false };

        _behavior.SyncData(store);

        var days = store.Get<Dictionary<string, int>>("_taom_tgwaves_days");
        CollectionAssert.AreEquivalent(new[] { "new" }, days.Keys.ToArray());
        Assert.AreEqual("_taom_tgwaves_days", TrainingWavesDialogBehavior.DaysKey);
    }

    [TestMethod]
    public void SyncData_Load_RestoresTheLedger_OverTheConstructorsReset()
    {
        _f.Service.Ledger.Record(WaveBetFixture.TownId, 100);
        var store = new EngineDataStore { Loading = false };
        _behavior.SyncData(store);

        var loaded = new TrainingWavesDialogBehavior(_f.Service, _f.Logger);
        Assert.IsFalse(_f.Service.Ledger.RanToday(WaveBetFixture.TownId, 100), "the constructor resets first");
        loaded.SyncData(Reload(store));

        Assert.IsTrue(_f.Service.Ledger.RanToday(WaveBetFixture.TownId, 100));
    }

    [TestMethod]
    public void SyncData_ASaveWithoutARecord_StaysFresh()
    {
        _f.Service.Ledger.Record(WaveBetFixture.TownId, 100);
        var campaignB = new TrainingWavesDialogBehavior(_f.Service, _f.Logger);

        campaignB.SyncData(new EngineDataStore { Loading = true });

        Assert.IsFalse(_f.Service.Ledger.RanToday(WaveBetFixture.TownId, 100), "campaign B must not inherit campaign A's day");
    }

    [TestMethod]
    public void SyncData_ALoadedDayThatIsNotToday_CountsAsNoRun()
    {
        _f.Service.Ledger.Record(WaveBetFixture.TownId, 100);
        var store = new EngineDataStore { Loading = false };
        _behavior.SyncData(store);
        var loaded = new TrainingWavesDialogBehavior(_f.Service, _f.Logger);
        loaded.SyncData(Reload(store));

        _f.Adapter.Today.Returns(101);

        Assert.IsFalse(_f.Service.Ledger.RanToday(WaveBetFixture.TownId, 101));
    }

    // ---- the end of a conversation

    [TestMethod]
    public void OnConversationEnded_HandsTheEndToTheService()
    {
        _f.Fighting(500);
        _f.Service.PayClearedWave();
        _f.Service.MarkDebrief();

        _behavior.OnConversationEnded(new List<CharacterObject>());

        Assert.AreEqual(WaveRunEnd.Withdrew, _f.Service.Run!.EndReason);
        Assert.IsFalse(_f.Service.DebriefPending);
    }

    // ---- the lines' decisions and consequences

    [TestMethod]
    public void CanOffer_FollowsTheServicesOffer()
    {
        Assert.IsTrue(_behavior.CanOffer());

        _f.Settings.Enabled.Returns(false);

        Assert.IsFalse(_behavior.CanOffer());
    }

    [TestMethod]
    public void OfferClickable_IsTrueWhenTheOfferIsOpen_AndExplainsEachRefusal()
    {
        Assert.IsTrue(_behavior.OfferClickable(out _));

        _f.Service.Ledger.Record(WaveBetFixture.TownId, 100);
        Assert.IsFalse(_behavior.OfferClickable(out var why));
        Assert.IsFalse(string.IsNullOrEmpty(why.ToString()));
    }

    [TestMethod]
    public void OfferClickable_TooPoor_NamesTheSmallestWagerOnOffer()
    {
        _f.Purse.Gold = 50;

        Assert.IsFalse(_behavior.OfferClickable(out var why));

        StringAssert.Contains(why.ToString(), "120");
    }

    [TestMethod]
    public void StakeSlots_ListTheTopThreeChoices_AndAbsentOnesDoNotShow()
    {
        Assert.IsTrue(_behavior.WantsStake(0));
        Assert.IsTrue(_behavior.WantsStake(1));
        Assert.IsTrue(_behavior.WantsStake(2));

        _f.Tournament.MaxBetPerRound.Returns(10);

        Assert.IsTrue(_behavior.WantsStake(0));
        Assert.IsFalse(_behavior.WantsStake(1));
        Assert.IsFalse(_behavior.WantsStake(2));
    }

    [TestMethod]
    public void Stake_PlacesTheChoiceOfItsSlot()
    {
        _behavior.Stake(1);

        Assert.AreEqual(250, _f.Service.Run!.Stake);
        Assert.AreEqual(100_000 - 250, _f.Purse.Gold);
    }

    [TestMethod]
    public void Stake_ReChecksEveryGate_SoAStaleLineChargesNothing()
    {
        _f.Settings.Enabled.Returns(false);

        _behavior.Stake(0);

        Assert.AreEqual(100_000, _f.Purse.Gold);
    }

    [TestMethod]
    public void StakeClickable_IsFalseWhenThePurseIsShort_WithAReason()
    {
        _f.Purse.Gold = 100;

        Assert.IsFalse(_behavior.StakeClickable(0, out var why));
        Assert.IsFalse(string.IsNullOrEmpty(why.ToString()));
        _f.Purse.Gold = 500;
        Assert.IsTrue(_behavior.StakeClickable(0, out _));
    }

    [TestMethod]
    public void TheGoLine_FollowsAWaveDueRun_AndTheFallbackTakesOverOtherwise()
    {
        Assert.IsFalse(_behavior.WantsGo());

        _behavior.Stake(0);
        Assert.IsTrue(_behavior.WantsGo());

        _f.Service.Run!.BeginFight();
        Assert.IsFalse(_behavior.WantsGo());
    }

    [TestMethod]
    public void NotToday_IsOfferedBeforeAnyStake_AndNotBetweenWaves()
    {
        _f.Service.PrepareRun();
        Assert.IsTrue(_behavior.CanNotToday());

        _f.Fighting(500);
        _f.Service.PayClearedWave();
        Assert.IsFalse(_behavior.CanNotToday());
    }

    [TestMethod]
    public void Withdraw_IsOfferedOnlyBetweenWaves_AndEndsTheRun()
    {
        _f.Service.PrepareRun();
        Assert.IsFalse(_behavior.CanWithdraw());

        _f.Fighting(500);
        _f.Service.PayClearedWave();
        Assert.IsTrue(_behavior.CanWithdraw());

        _behavior.Withdraw();

        Assert.AreEqual(WaveRunEnd.Withdrew, _f.Service.Run!.EndReason);
    }

    [TestMethod]
    public void TheDebriefLines_EachFollowTheirOwnEnding_AndTheConsequenceTakesTheDebrief()
    {
        _f.Fighting(500);
        _f.Service.PayClearedWave();
        _f.Service.MarkDebrief();
        Assert.IsTrue(_behavior.WantsDebrief(DebriefKind.Cleared));
        Assert.IsFalse(_behavior.WantsDebrief(DebriefKind.Lost));
        Assert.IsFalse(_behavior.WantsDebrief(DebriefKind.Completed));

        _behavior.Debrief();

        Assert.IsFalse(_f.Service.DebriefPending);
        Assert.IsFalse(_behavior.WantsDebrief(DebriefKind.Cleared));
    }

    [TestMethod]
    public void AThrowingService_IsSwallowedAtTheBoundary_AndTheConditionIsFalse()
    {
        _f.Settings.Enabled.Returns(_ => throw new InvalidOperationException("seam"));

        Assert.IsFalse(_behavior.CanOffer());
        _behavior.Stake(0);

        _f.Logger.Received().LogError(Arg.Is<string>(m => m.Contains("TrainingWaves")));
    }
}
