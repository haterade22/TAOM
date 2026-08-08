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
    private ICommanderLordAdapter _commanderAdapter = null!;
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
        _commanderAdapter = Substitute.For<ICommanderLordAdapter>();
        _commanderAdapter.GetSnapshot(Arg.Any<string>()).Returns(new CommanderSnapshot(exists: true, isAlive: true, partyId: "lord_party", partyIsActive: true));
        _service = new EnlistmentDailyService(_store, _contentStore, _config, _rewards, _rhythm, _skillXp, _promotion, _world, _commanderAdapter, _logger);
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
    private IHeroSkillXpAdapter _skillXp = null!;
    private ICommanderLordAdapter _commander = null!;
    private EnlistmentStore _store = null!;
    private EnlistmentContentStore _content = null!;
    private EnlistmentDailyService _service = null!;

    [TestInitialize]
    public void Setup()
    {
        var logger = Substitute.For<IModLogger>();
        _world = Substitute.For<IDutyWorldAdapter>();
        _skillXp = Substitute.For<IHeroSkillXpAdapter>();
        _commander = Substitute.For<ICommanderLordAdapter>();
        _commander.GetSnapshot(Arg.Any<string>()).Returns(new CommanderSnapshot(exists: true, isAlive: true, partyId: "lord_party", partyIsActive: true));
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
            _skillXp, promotion, _world, _commander, logger);

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

    [TestMethod]
    public void RunDailyTick_LowMorale_LiftedToTheServiceFloor()
    {
        // Below 25 an attached party counts as "low morale" in CalculateCohesionChangeInternal and
        // drags the whole army's cohesion — so this is upkeep for the commander, not just comfort.
        _service.RunDailyTick(5.0, 12.0);

        _world.Received(1).RaisePlayerMoraleTo(40f);
    }

    [TestMethod]
    public void RunDailyTick_HealsThePlayerEveryDay()
    {
        // Serving must never be worse for your health than marching alone. Vanilla gives a mobile
        // party's heroes +11/day; an enlisted player is a hidden, inactive, one-man party that the
        // engine's healing path was never written for — a real player reached 19% HP with no
        // recovery. The surgeon is explicit rather than a side effect.
        _service.RunDailyTick(5.0, 12.0);

        _world.Received(1).HealPlayerHero(11);
    }

    [TestMethod]
    public void RunDailyTick_NotEnlisted_NoUpkeepAtAll()
    {
        // The company only feeds its own. A discharged player is on their own again.
        _store.Record.State = EnlistmentState.NotEnlisted;

        _service.RunDailyTick(5.0, 12.0);

        _world.DidNotReceiveWithAnyArgs().GrantPlayerFood(default);
        _world.DidNotReceiveWithAnyArgs().RaisePlayerMoraleTo(default);
        _world.DidNotReceiveWithAnyArgs().HealPlayerHero(default);
    }

    [TestMethod]
    public void RunDailyTick_MedicineSkill_IncreasesTheHeal()
    {
        // A flat rate made both the skill and the surgeon duty inert — a physician and a farmhand
        // recovered at identical speed, telling the player their Medicine does nothing in service.
        _skillXp.GetSkillValue(Arg.Any<string>(), "Medicine").Returns(120);

        _service.RunDailyTick(5.0, 12.0);

        _world.Received(1).HealPlayerHero(23);   // 11 base + 120/10
    }

    [TestMethod]
    public void RunDailyTick_InBattle_StillHeals()
    {
        // EnlistedBattle is the other PARKED state: presence is restored for the fight itself, but
        // the party is not out living its own life on the map. Same regime as attached.
        _store.Record.State = EnlistmentState.EnlistedBattle;

        _service.RunDailyTick(5.0, 12.0);

        _world.Received(1).HealPlayerHero(11);
    }

    [TestMethod]
    public void RunDailyTick_DetachedOnDuty_DoesNotHeal_VanillaAlreadyDoes()
    {
        // THE REGIME SPLIT. A field duty calls RestorePresence() — IsActive becomes true — and
        // vanilla's PartyHealCampaignBehavior heals any ACTIVE party's heroes +11/day on its own.
        // Healing here too paid the player twice for 4–6 days per duty. 12 of the 13 field duties
        // detach (only WaitHours stays attached), so this was the common case, not an edge.
        _store.Record.State = EnlistmentState.EnlistedDetachedOnDuty;

        _service.RunDailyTick(5.0, 12.0);

        _world.DidNotReceiveWithAnyArgs().HealPlayerHero(default);
    }

    [TestMethod]
    public void RunDailyTick_CommanderUnavailable_DoesNotHeal()
    {
        // The 7-day grace also restores presence (EnlistmentReconciler:349), so vanilla is healing.
        _store.Record.State = EnlistmentState.CommanderUnavailable;

        _service.RunDailyTick(5.0, 12.0);

        _world.DidNotReceiveWithAnyArgs().HealPlayerHero(default);
    }

    [TestMethod]
    public void RunDailyTick_PlayerCaptive_DoesNotHeal()
    {
        // Vanilla captivity owns the party entirely. A prisoner is not drawing the company surgeon.
        _store.Record.State = EnlistmentState.EnlistedPlayerCaptive;

        _service.RunDailyTick(5.0, 12.0);

        _world.DidNotReceiveWithAnyArgs().HealPlayerHero(default);
    }

    [TestMethod]
    public void RunDailyTick_DetachedOnDuty_StillFedAndKeptInMorale()
    {
        // Deliberately NOT gated with the heal. On detached duty the party is active, so vanilla
        // consumes food for real — the baggage top-up is the company still provisioning a soldier
        // it sent out, which is the point of the feature. Only the HEAL double-pays.
        _store.Record.State = EnlistmentState.EnlistedDetachedOnDuty;
        _world.CountPlayerFood().Returns(0);

        _service.RunDailyTick(5.0, 12.0);

        _world.Received(1).GrantPlayerFood(3);
        _world.Received(1).RaisePlayerMoraleTo(40f);
    }

    [TestMethod]
    public void RunDailyTick_ColumnRestingInASettlement_DoublesTheHeal()
    {
        // Read from the COMMANDER: the column is what is resting, and a following player is
        // wherever it is.
        _commander.GetSnapshot(Arg.Any<string>()).Returns(new CommanderSnapshot(
            exists: true, isAlive: true, partyId: "lord_party", partyIsActive: true,
            partyIsInSettlement: true, settlementId: "town_EW1"));

        _service.RunDailyTick(5.0, 12.0);

        _world.Received(1).HealPlayerHero(22);   // 11 base x2 resting
    }
}
