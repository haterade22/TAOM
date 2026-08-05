using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Adapters;
using TAOM.Core.Logging;
using TAOM.Features.Enlistment;
using TAOM.Features.Enlistment.Content;
using TAOM.Features.Enlistment.Content.Domain;
using TAOM.Features.Enlistment.Domain;

namespace TAOM.Tests.Features.Enlistment;

/// <summary>
/// Orchestration tests for the wage/reward chokepoint. These exist because the pure
/// WagePolicy tests passed while PayDailyWage double-paid arrears (mint mode ran BOTH the
/// commander transfer and the mint) and double-counted the transfer shortfall — the pure
/// function was right and the orchestration was wrong.
/// </summary>
[TestClass]
public class ServiceRewardServiceTests
{
    private IModLogger _logger = null!;
    private EnlistmentStore _store = null!;
    private EnlistmentContentStore _contentStore = null!;
    private IEnlistmentContentConfigProvider _config = null!;
    private EnlistmentContentConfig _configValue = null!;
    private IHeroSkillXpAdapter _skillXp = null!;
    private IGoldGiftAdapter _goldGift = null!;
    private IGoldTransferAdapter _goldTransfer = null!;
    private ICommanderLordAdapter _commander = null!;
    private ServiceRewardService _service = null!;

    [TestInitialize]
    public void Setup()
    {
        _logger = Substitute.For<IModLogger>();
        _store = new EnlistmentStore(_logger);
        _contentStore = new EnlistmentContentStore(_logger);
        _configValue = EnlistmentContentConfigProvider.BuildDefaults();
        _config = Substitute.For<IEnlistmentContentConfigProvider>();
        _config.GetConfig().Returns(_ => _configValue);
        _skillXp = Substitute.For<IHeroSkillXpAdapter>();
        _goldGift = Substitute.For<IGoldGiftAdapter>();
        _goldTransfer = Substitute.For<IGoldTransferAdapter>();
        _commander = Substitute.For<ICommanderLordAdapter>();
        var playerParty = Substitute.For<IPlayerPartyAdapter>();
        playerParty.GetMainHeroId().Returns("main_hero");
        _service = new ServiceRewardService(
            _store, _contentStore, _config, _skillXp, _goldGift, _goldTransfer, _commander, playerParty, _logger);

        _store.Record.State = EnlistmentState.EnlistedAttached;
        _store.Record.EnlistedHeroId = "main_hero";
        _store.Record.CommanderHeroId = "lord_1_1";

        // Commander pays in full unless a test says otherwise.
        _goldTransfer.GetHeroGold("lord_1_1").Returns(10000);
        _goldTransfer.TransferToPlayer("lord_1_1", Arg.Any<int>()).Returns(call => call.ArgAt<int>(1));
    }

    [TestMethod]
    public void PayDailyWage_SolventCommander_TransfersWageOnlyNoMint()
    {
        var decision = _service.PayDailyWage();

        Assert.AreEqual(5, decision.PaidFromCommander, "recruit wage");
        Assert.AreEqual(0, decision.Minted);
        _goldTransfer.Received(1).TransferToPlayer("lord_1_1", 5);
        _goldGift.DidNotReceive().GiveGoldToHero(Arg.Any<string>(), Arg.Any<int>());
        Assert.AreEqual(0, _contentStore.Record.DeferredWages);
    }

    [TestMethod]
    public void PayDailyWage_BrokeCommander_DefersWholeWage()
    {
        _goldTransfer.GetHeroGold("lord_1_1").Returns(400); // below the 500 floor

        var decision = _service.PayDailyWage();

        Assert.AreEqual(0, decision.PaidFromCommander);
        Assert.AreEqual(5, decision.NewlyDeferred);
        Assert.AreEqual(5, _contentStore.Record.DeferredWages);
        _goldTransfer.DidNotReceive().TransferToPlayer(Arg.Any<string>(), Arg.Any<int>());
    }

    [TestMethod]
    public void PayDailyWage_SolventAgain_ClearsArrearsExactlyOnce()
    {
        _contentStore.Record.DeferredWages = 20;

        var decision = _service.PayDailyWage();

        // 5 wage + 20 arrears = 25 delivered, through the commander channel only.
        _goldTransfer.Received(1).TransferToPlayer("lord_1_1", 25);
        _goldGift.DidNotReceive().GiveGoldToHero(Arg.Any<string>(), Arg.Any<int>());
        Assert.AreEqual(5, decision.PaidFromCommander);
        Assert.AreEqual(20, decision.ArrearsReleased);
        Assert.AreEqual(0, _contentStore.Record.DeferredWages);
    }

    [TestMethod]
    public void PayDailyWage_MintMode_MintsOnlyAndNeverTouchesCommanderGold()
    {
        // The HIGH bug: mint mode used to ALSO transfer arrears from the commander and
        // then mint them again — the player got paid ~1.67x and the lord was drained.
        _configValue.WagePolicy.PayFromCommanderGold = false;
        _contentStore.Record.DeferredWages = 20;

        var decision = _service.PayDailyWage();

        _goldTransfer.DidNotReceive().TransferToPlayer(Arg.Any<string>(), Arg.Any<int>());
        _goldGift.Received(1).GiveGoldToHero("main_hero", 25);
        Assert.AreEqual(5, decision.Minted);
        Assert.AreEqual(20, decision.ArrearsReleased);
        Assert.AreEqual(0, decision.PaidFromCommander);
        Assert.AreEqual(0, _contentStore.Record.DeferredWages);
    }

    [TestMethod]
    public void PayDailyWage_TransferShortfall_LedgerMatchesConservation()
    {
        // Prior debt 5, wage 5 (recruit) → 10 requested; only 8 lands.
        // Owed 10, delivered 8 → remaining debt is exactly 2 (the old code said 4).
        _contentStore.Record.DeferredWages = 5;
        _goldTransfer.TransferToPlayer("lord_1_1", 10).Returns(8);

        _service.PayDailyWage();

        Assert.AreEqual(2, _contentStore.Record.DeferredWages);
    }

    [TestMethod]
    public void PayDailyWage_ArrearsNeverExceedCap()
    {
        _configValue.WagePolicy.MaxDeferredWages = 8;
        _contentStore.Record.DeferredWages = 8;
        _goldTransfer.GetHeroGold("lord_1_1").Returns(0);

        _service.PayDailyWage();

        Assert.AreEqual(8, _contentStore.Record.DeferredWages, "overflow forfeited, never exceeds the cap");
    }

    [TestMethod]
    public void PayDailyWage_ArrearsNeverGoNegative()
    {
        _contentStore.Record.DeferredWages = 0;

        _service.PayDailyWage();

        Assert.IsTrue(_contentStore.Record.DeferredWages >= 0);
    }

    [TestMethod]
    public void PayDailyWage_HigherRank_UsesRankWage()
    {
        _contentStore.Record.Rank = ServiceRank.Sergeant;

        var decision = _service.PayDailyWage();

        Assert.AreEqual(22, decision.PaidFromCommander);
    }

    // ---- Grant ----

    [TestMethod]
    public void Grant_FullReward_RoutesEveryChannel()
    {
        _service.Grant(new RewardSpec
        {
            ServiceXp = 30,
            Gold = 20,
            SkillId = "Scouting",
            SkillXp = 15,
            Trust = 2,
            Relation = 1,
            RepDomain = ReputationDomain.Field,
            RepAmount = 3,
        }, "test");

        Assert.AreEqual(30, _contentStore.Record.ServiceXp);
        _goldGift.Received(1).GiveGoldToHero("main_hero", 20);
        _skillXp.Received(1).AddSkillXp("main_hero", "Scouting", 15);
        _commander.Received(1).ApplyPlayerRelation("lord_1_1", 1);
        Assert.AreEqual(2, _contentStore.Record.Trust);
        Assert.AreEqual(3, _contentStore.Record.FieldRep);
    }

    [TestMethod]
    public void Grant_ZeroFields_NoSideEffects()
    {
        _service.Grant(new RewardSpec(), "empty");

        _goldGift.DidNotReceive().GiveGoldToHero(Arg.Any<string>(), Arg.Any<int>());
        _skillXp.DidNotReceive().AddSkillXp(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<float>());
        _commander.DidNotReceive().ApplyPlayerRelation(Arg.Any<string>(), Arg.Any<int>());
        Assert.AreEqual(0, _contentStore.Record.Trust);
    }

    [TestMethod]
    public void Grant_Null_DoesNotThrow()
    {
        _service.Grant(null, "null");
    }

    [TestMethod]
    public void AdjustTrust_ClampsAtConfiguredCeiling()
    {
        _contentStore.Record.Trust = 19;

        _service.AdjustTrust(10);

        Assert.AreEqual(20, _contentStore.Record.Trust);
    }
}
