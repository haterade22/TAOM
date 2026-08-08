using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Adapters;
using TAOM.Core.Logging;
using TAOM.Features.Enlistment;
using TAOM.Features.Enlistment.Content;
using TAOM.Features.Enlistment.Content.Domain;
using TAOM.Features.Enlistment.Domain;

namespace TAOM.Tests.Features.Enlistment;

[TestClass]
public class EnlistmentDailyServiceTests
{
    private IModLogger _logger = null!;
    private EnlistmentStore _store = null!;
    private EnlistmentContentStore _contentStore = null!;
    private IEnlistmentContentConfigProvider _config = null!;
    private IServiceRewardService _rewards = null!;
    private IArmyRhythmSnapshotService _rhythm = null!;
    private IHeroSkillXpAdapter _skillXp = null!;
    private PromotionService _promotion = null!;
    private IDutyWorldAdapter _world = null!;
    private EnlistmentDailyService _service = null!;

    [TestInitialize]
    public void Setup()
    {
        _logger = Substitute.For<IModLogger>();
        _store = new EnlistmentStore(_logger);
        _contentStore = new EnlistmentContentStore(_logger);
        _config = Substitute.For<IEnlistmentContentConfigProvider>();
        _config.GetConfig().Returns(EnlistmentContentConfigProvider.BuildDefaults());
        _rewards = Substitute.For<IServiceRewardService>();
        _rewards.PayDailyWage().Returns(new WageDecision { PaidFromCommander = 5 });
        _rhythm = Substitute.For<IArmyRhythmSnapshotService>();
        _rhythm.GetSnapshot(Arg.Any<double>(), Arg.Any<double>()).Returns(new ArmyRhythmSnapshot());
        _skillXp = Substitute.For<IHeroSkillXpAdapter>();
        _promotion = new PromotionService(_contentStore, _config, _skillXp, _store, _logger);
        _world = Substitute.For<IDutyWorldAdapter>();
        _service = new EnlistmentDailyService(_store, _contentStore, _config, _rewards, _rhythm, _skillXp, _promotion, _world, _logger);
    }

    private void MakeEnlisted(double contractEnd = 465.0)
    {
        _store.Record.State = EnlistmentState.EnlistedAttached;
        _store.Record.EnlistedHeroId = "main_hero";
        _store.Record.CommanderHeroId = "lord_1_1";
        _store.Record.ContractEndDay = contractEnd;
    }

    [TestMethod]
    public void DailyTick_NotEnlisted_NoOp()
    {
        _service.RunDailyTick(200.0, 12.0);

        _rewards.DidNotReceive().PayDailyWage();
        Assert.AreEqual(0, _contentStore.Record.DaysServed);
    }

    [TestMethod]
    public void DailyTick_Enlisted_CountsDayPaysWageGrantsXp()
    {
        MakeEnlisted();

        var summary = _service.RunDailyTick(200.0, 12.0);

        Assert.AreEqual(1, _contentStore.Record.DaysServed);
        Assert.AreEqual(5, summary.Wage.PaidFromCommander);
        Assert.AreEqual(10, _contentStore.Record.ServiceXp, "recruit daily service XP");
        _skillXp.Received(1).AddSkillXp("main_hero", "Athletics", 10);
        _skillXp.Received(1).AddSkillXp("main_hero", "Leadership", 10);
    }

    [TestMethod]
    public void DailyTick_SiegeContext_PaysEngineeringExclusively()
    {
        // Priority-exclusive context XP — the donor stacked all four in one day.
        MakeEnlisted();
        _rhythm.GetSnapshot(Arg.Any<double>(), Arg.Any<double>())
            .Returns(new ArmyRhythmSnapshot { SiegePressure = true, Naval = true, InArmy = true });

        _service.RunDailyTick(200.0, 12.0);

        _skillXp.Received(1).AddSkillXp("main_hero", "Engineering", 8);
        _skillXp.DidNotReceive().AddSkillXp("main_hero", "Tactics", Arg.Any<float>());
    }

    [TestMethod]
    public void DailyTick_ThresholdsMet_PromotesOnce()
    {
        MakeEnlisted();
        _contentStore.Record.DaysServed = 7; // becomes 8 on tick
        _contentStore.Record.ServiceXp = 95; // +10 → 105

        var summary = _service.RunDailyTick(200.0, 12.0);

        Assert.IsTrue(summary.Promoted);
        Assert.AreEqual(ServiceRank.Soldier, summary.NewRank);
        Assert.AreEqual(ServiceRank.Soldier, _contentStore.Record.Rank);
    }

    [TestMethod]
    public void DailyTick_LeadershipGateBlocks_UntilSkillCatchesUp()
    {
        MakeEnlisted();
        _contentStore.Record.Rank = ServiceRank.Soldier;
        _contentStore.Record.DaysServed = 30;
        _contentStore.Record.ServiceXp = 400;
        _contentStore.Record.DutySuccesses = 3;
        _skillXp.GetSkillValue("main_hero", "Leadership").Returns(10);

        Assert.IsFalse(_service.RunDailyTick(200.0, 12.0).Promoted);

        _skillXp.GetSkillValue("main_hero", "Leadership").Returns(25);
        Assert.IsTrue(_service.RunDailyTick(200.0, 12.0).Promoted);
    }

    [TestMethod]
    public void DailyTick_ContractExpiry_FlagsExactlyOnce()
    {
        MakeEnlisted(contractEnd: 200.5);

        Assert.IsFalse(_service.RunDailyTick(199.8, 12.0).ContractExpiredToday);
        Assert.IsTrue(_service.RunDailyTick(200.8, 12.0).ContractExpiredToday, "first tick past the end day");
        Assert.IsFalse(_service.RunDailyTick(201.8, 12.0).ContractExpiredToday, "not re-flagged");
    }

    [TestMethod]
    public void RankOrdinals_ContentAndEquipmentLaddersStayAligned()
    {
        // The quartermaster maps (EnlistmentRank)(int)ServiceRank — pin the ordinal parity.
        Assert.AreEqual((int)ServiceRank.Recruit, (int)TAOM.Features.Enlistment.Equipment.EnlistmentRank.Recruit);
        Assert.AreEqual((int)ServiceRank.Soldier, (int)TAOM.Features.Enlistment.Equipment.EnlistmentRank.Soldier);
        Assert.AreEqual((int)ServiceRank.Veteran, (int)TAOM.Features.Enlistment.Equipment.EnlistmentRank.Veteran);
        Assert.AreEqual((int)ServiceRank.Sergeant, (int)TAOM.Features.Enlistment.Equipment.EnlistmentRank.Sergeant);
    }
}

/// <summary>
/// The commander feeds his soldiers. Reported in-game 2026-08-08: the player sat at 19% HP and
/// would not recover.
/// </summary>
[TestClass]
public class EnlistmentProvisioningTests
{
    private IDutyWorldAdapter _world = null!;
    private EnlistmentStore _store = null!;
    private EnlistmentContentStore _content = null!;
    private EnlistmentDailyService _service = null!;

    [TestInitialize]
    public void Setup()
    {
        var logger = Substitute.For<IModLogger>();
        _world = Substitute.For<IDutyWorldAdapter>();
        _store = new EnlistmentStore(logger);
        _content = new EnlistmentContentStore(logger);
        var config = Substitute.For<IEnlistmentContentConfigProvider>();
        config.GetConfig().Returns(EnlistmentContentConfigProvider.BuildDefaults());
        var promotion = Substitute.For<IPromotionService>();
        promotion.EvaluateAndApply().Returns(new PromotionOutcome());
        var rhythm = Substitute.For<IArmyRhythmSnapshotService>();
        rhythm.GetSnapshot(Arg.Any<double>(), Arg.Any<double>()).Returns(new ArmyRhythmSnapshot());

        _service = new EnlistmentDailyService(
            _store, _content, config, Substitute.For<IServiceRewardService>(), rhythm,
            Substitute.For<IHeroSkillXpAdapter>(), promotion, _world, logger);

        _store.Record.State = EnlistmentState.EnlistedAttached;
        _store.Record.EnlistedHeroId = "main_hero";
        _store.Record.CommanderHeroId = "lord_1";
        _store.Record.EnlistedAtDay = 0.0;
    }

    [TestMethod]
    public void RunDailyTick_OutOfFood_IsProvisioned()
    {
        // DefaultPartyHealingModel: a mobile party heals heroes +11 HP/day, BUT a STARVING party
        // with no settlement returns -19f — the hero loses health every day instead of recovering.
        // An enlisted player is one hero parked in the field; nothing else feeds them.
        _world.CountPlayerFood().Returns(0);

        _service.RunDailyTick(5.0, 12.0);

        _world.Received(1).GrantPlayerFood(3);
    }

    [TestMethod]
    public void RunDailyTick_AlreadyFed_GrantsNothing()
    {
        // Not a daily handout: it tops up to a floor, so it cannot be farmed as a supply source.
        _world.CountPlayerFood().Returns(5);

        _service.RunDailyTick(5.0, 12.0);

        _world.DidNotReceiveWithAnyArgs().GrantPlayerFood(default);
    }

    [TestMethod]
    public void RunDailyTick_PartiallyFed_ToppedUpToTheFloorOnly()
    {
        _world.CountPlayerFood().Returns(1);

        _service.RunDailyTick(5.0, 12.0);

        _world.Received(1).GrantPlayerFood(2);
    }
}
