using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Adapters;
using TAOM.Core.Logging;
using TAOM.Features.Enlistment;
using TAOM.Features.Enlistment.Content;
using TAOM.Features.Enlistment.Content.Domain;
using TAOM.Features.Enlistment.Domain;
using TAOM.Features.Enlistment.Duties;
using TAOM.Features.TroopProgression;

namespace TAOM.Tests.Features.Enlistment.Duties;

[TestClass]
public class FieldDutyRuntimeTests
{
    private IModLogger _logger = null!;
    private EnlistmentStore _store = null!;
    private EnlistmentContentStore _contentStore = null!;
    private IEnlistmentContentConfigProvider _config = null!;
    private IEnlistmentStateMachine _stateMachine = null!;
    private IServiceAttachmentService _attachment = null!;
    private IDutyWorldAdapter _world = null!;
    private ICommanderLordAdapter _commander = null!;
    private IServiceRewardService _rewards = null!;
    private IRandomProvider _random = null!;
    private FieldDutyRuntime _runtime = null!;

    [TestInitialize]
    public void Setup()
    {
        _logger = Substitute.For<IModLogger>();
        _store = new EnlistmentStore(_logger);
        _contentStore = new EnlistmentContentStore(_logger);
        _config = Substitute.For<IEnlistmentContentConfigProvider>();
        _stateMachine = Substitute.For<IEnlistmentStateMachine>();
        _attachment = Substitute.For<IServiceAttachmentService>();
        _world = Substitute.For<IDutyWorldAdapter>();
        _commander = Substitute.For<ICommanderLordAdapter>();
        _rewards = Substitute.For<IServiceRewardService>();
        _random = Substitute.For<IRandomProvider>();

        _stateMachine.TryTransition(Arg.Any<EnlistmentState>()).Returns(true);
        _commander.GetSnapshot(Arg.Any<string>()).Returns(new CommanderSnapshot(exists: true, settlementId: "town_1"));

        _runtime = new FieldDutyRuntime(_store, _contentStore, _config, _stateMachine, _attachment, _world, _commander, _rewards, _random, _logger);

        MakeEnlisted();
    }

    private void MakeEnlisted()
    {
        _store.Record.State = EnlistmentState.EnlistedAttached;
        _store.Record.EnlistedHeroId = "main_hero";
        _store.Record.CommanderHeroId = "lord_1_1";
    }

    private void ConfigureDuties(params DutyDefinition[] duties)
    {
        _config.GetDuties().Returns(new EnlistmentDutiesConfig { FieldDuties = new List<DutyDefinition>(duties) });
    }

    private static DutyDefinition HuntDuty(string id = "recon_sweep") => new DutyDefinition
    {
        Id = id,
        Mechanic = DutyMechanic.HuntSpawnedParty,
        TargetKind = DutyTargetKind.SpawnedLooterParty,
        TargetAi = DutyTargetAi.PatrolAnchor,
        DeadlineDays = 5,
        ReportReward = new RewardSpec { ServiceXp = 10 },
    };

    private static DutyDefinition VisitDuty(string id = "road_patrol") => new DutyDefinition
    {
        Id = id,
        Mechanic = DutyMechanic.VisitSettlement,
        TargetKind = DutyTargetKind.FriendlySettlement,
        DeadlineDays = 4,
        ReportReward = new RewardSpec { ServiceXp = 10 },
    };

    private static DutyDefinition DeliverFoodDuty(string id = "supply_delivery") => new DutyDefinition
    {
        Id = id,
        Mechanic = DutyMechanic.DeliverFood,
        TargetKind = DutyTargetKind.FriendlySettlement,
        DeadlineDays = 5,
        ReportReward = new RewardSpec { ServiceXp = 10 },
    };

    private static DutyDefinition CollectFoodDuty(string id = "forage") => new DutyDefinition
    {
        Id = id,
        Mechanic = DutyMechanic.CollectFood,
        TargetKind = DutyTargetKind.FriendlyVillage,
        DeadlineDays = 4,
        ReportReward = new RewardSpec { ServiceXp = 10 },
    };

    private static DutyDefinition WaitDuty(string id = "service_shift") => new DutyDefinition
    {
        Id = id,
        Mechanic = DutyMechanic.WaitHours,
        TargetKind = DutyTargetKind.None,
        DeadlineDays = 1,
        ReportReward = new RewardSpec { ServiceXp = 10 },
    };

    // ---- Start ----

    [TestMethod]
    public void Start_ActiveDutyAlreadyExists_ReturnsFalse()
    {
        _contentStore.Record.ActiveDutyId = "existing";

        var result = _runtime.Start(HuntDuty(), 100.0);

        Assert.IsFalse(result);
    }

    [TestMethod]
    public void Start_NullDuty_ReturnsFalse()
    {
        Assert.IsFalse(_runtime.Start(null, 100.0));
    }

    [TestMethod]
    public void Start_HuntSpawnedParty_SpawnFails_ReturnsFalseAndLeavesRecordClear()
    {
        _world.SpawnLooterParty(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<bool>()).Returns((string)null);

        var result = _runtime.Start(HuntDuty(), 100.0);

        Assert.IsFalse(result);
        Assert.IsFalse(_contentStore.Record.HasActiveDuty);
    }

    [TestMethod]
    public void Start_HuntSpawnedParty_CommanderInTheField_AnchorsOnTheNearestSettlement()
    {
        // Regression (in-game 2026-08-07): the anchor used CommanderSnapshot.SettlementId, which is
        // empty whenever the column is marching — i.e. nearly always. Every recon_sweep failed with
        // "SpawnLooterParty: settlement=''". Hunt duties could only start while the commander sat
        // inside a town, which is the rarer state.
        _commander.GetSnapshot(Arg.Any<string>()).Returns(new CommanderSnapshot(exists: true, settlementId: null));
        _world.FindNearestFriendlySettlement("lord_1_1").Returns("town_nearby");
        _world.SpawnLooterParty(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<bool>()).Returns("taom_enlist_duty_abc123");

        var result = _runtime.Start(HuntDuty("recon_sweep"), 100.0);

        Assert.IsTrue(result);
        _world.Received(1).SpawnLooterParty(Arg.Any<string>(), "town_nearby", Arg.Any<bool>());
    }

    [TestMethod]
    public void Start_HuntSpawnedParty_CommanderInSettlement_PrefersThatSettlement()
    {
        _commander.GetSnapshot(Arg.Any<string>()).Returns(new CommanderSnapshot(exists: true, settlementId: "town_1"));
        _world.SpawnLooterParty(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<bool>()).Returns("taom_enlist_duty_abc123");

        _runtime.Start(HuntDuty("recon_sweep"), 100.0);

        _world.Received(1).SpawnLooterParty(Arg.Any<string>(), "town_1", Arg.Any<bool>());
        _world.DidNotReceive().FindNearestFriendlySettlement(Arg.Any<string>());
    }

    [TestMethod]
    public void Start_HuntSpawnedParty_NoAnchorAnywhere_FailsWithoutSpawning()
    {
        _commander.GetSnapshot(Arg.Any<string>()).Returns(new CommanderSnapshot(exists: true, settlementId: null));
        _world.FindNearestFriendlySettlement(Arg.Any<string>()).Returns((string)null);

        var result = _runtime.Start(HuntDuty("recon_sweep"), 100.0);

        Assert.IsFalse(result);
        _world.DidNotReceive().SpawnLooterParty(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<bool>());
        Assert.IsFalse(_contentStore.Record.HasActiveDuty);
    }

    [TestMethod]
    public void Start_HuntSpawnedParty_Success_SetsActiveDutyTransitionsAndRestoresPresence()
    {
        _world.SpawnLooterParty(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<bool>()).Returns("taom_enlist_duty_abc123");

        var result = _runtime.Start(HuntDuty("recon_sweep"), 100.0);

        Assert.IsTrue(result);
        Assert.AreEqual("recon_sweep", _contentStore.Record.ActiveDutyId);
        Assert.AreEqual("taom_enlist_duty_abc123", _contentStore.Record.ActiveDutyTargetPartyId);
        Assert.AreEqual(105.0, _contentStore.Record.ActiveDutyDeadlineDay);
        _stateMachine.Received(1).TryTransition(EnlistmentState.EnlistedDetachedOnDuty);
        _attachment.Received(1).RestorePresence();
    }

    [TestMethod]
    public void Start_TransitionFails_DestroysSpawnedTargetAndReturnsFalse()
    {
        _world.SpawnLooterParty(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<bool>()).Returns("party_xyz");
        _stateMachine.TryTransition(EnlistmentState.EnlistedDetachedOnDuty).Returns(false);

        var result = _runtime.Start(HuntDuty(), 100.0);

        Assert.IsFalse(result);
        _world.Received(1).DestroyParty("party_xyz");
        Assert.IsFalse(_contentStore.Record.HasActiveDuty);
    }

    [TestMethod]
    public void Start_VisitSettlement_NoEligibleSettlement_ReturnsFalse()
    {
        _world.FindNearestFriendlySettlement(Arg.Any<string>()).Returns((string)null);

        var result = _runtime.Start(VisitDuty(), 100.0);

        Assert.IsFalse(result);
    }

    [TestMethod]
    public void Start_VisitSettlement_Success_SetsSettlementTargetNoPartyTarget()
    {
        _world.FindNearestFriendlySettlement(Arg.Any<string>()).Returns("town_2");

        var result = _runtime.Start(VisitDuty("road_patrol"), 100.0);

        Assert.IsTrue(result);
        Assert.AreEqual("town_2", _contentStore.Record.ActiveDutySettlementId);
        Assert.IsNull(_contentStore.Record.ActiveDutyTargetPartyId);
    }

    [TestMethod]
    public void Start_DeliverFood_SetsFoodRequirement()
    {
        _world.FindNearestFriendlySettlement(Arg.Any<string>()).Returns("town_2");

        _runtime.Start(DeliverFoodDuty(), 100.0);

        Assert.AreEqual(6, _contentStore.Record.ActiveDutyFoodRequired);
    }

    [TestMethod]
    public void Start_CollectFood_UsesFriendlyVillageLookup()
    {
        _world.FindNearestFriendlyVillage(Arg.Any<string>()).Returns("village_1");

        var result = _runtime.Start(CollectFoodDuty(), 100.0);

        Assert.IsTrue(result);
        Assert.AreEqual("village_1", _contentStore.Record.ActiveDutySettlementId);
        _world.DidNotReceive().FindNearestFriendlySettlement(Arg.Any<string>());
    }

    [TestMethod]
    public void Start_WaitHours_StaysAttached_NoTransitionAndSetsShiftEnd()
    {
        var result = _runtime.Start(WaitDuty(), 100.0);

        Assert.IsTrue(result);
        Assert.AreEqual("service_shift", _contentStore.Record.ActiveDutyId);
        Assert.IsTrue(_contentStore.Record.ShiftEndDay.HasValue);
        _stateMachine.DidNotReceive().TryTransition(EnlistmentState.EnlistedDetachedOnDuty);
        _attachment.DidNotReceive().RestorePresence();
    }

    [TestMethod]
    public void Start_TrustAboveBonusThreshold_AddsDeadlineBonusDays()
    {
        _contentStore.Record.Trust = 20;
        var duty = HuntDuty();
        duty.TrustBonusThreshold = 10;
        duty.TrustDeadlineBonusDays = 2;
        _world.SpawnLooterParty(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<bool>()).Returns("party_1");

        _runtime.Start(duty, 100.0);

        Assert.AreEqual(107.0, _contentStore.Record.ActiveDutyDeadlineDay); // 100 + 5 (deadline) + 2 (bonus)
    }

    [TestMethod]
    public void Start_TrustBelowBonusThreshold_NoDeadlineBonus()
    {
        _contentStore.Record.Trust = 0;
        var duty = HuntDuty();
        duty.TrustBonusThreshold = 10;
        duty.TrustDeadlineBonusDays = 2;
        _world.SpawnLooterParty(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<bool>()).Returns("party_1");

        _runtime.Start(duty, 100.0);

        Assert.AreEqual(105.0, _contentStore.Record.ActiveDutyDeadlineDay);
    }

    // ---- HourlyUpdate ----

    [TestMethod]
    public void HourlyUpdate_NoActiveDuty_NoOp()
    {
        _runtime.HourlyUpdate(100.0);

        _rewards.DidNotReceiveWithAnyArgs().Grant(default, default);
    }

    [TestMethod]
    public void HourlyUpdate_NoLongerEnlisted_CancelsActiveDutyArtifacts()
    {
        ConfigureDuties(HuntDuty("recon_sweep"));
        _contentStore.Record.ActiveDutyId = "recon_sweep";
        _contentStore.Record.ActiveDutyTargetPartyId = "party_1";
        _store.Record.State = EnlistmentState.NotEnlisted;

        _runtime.HourlyUpdate(100.0);

        _world.Received(1).DestroyParty("party_1");
        Assert.IsFalse(_contentStore.Record.HasActiveDuty);
        _rewards.DidNotReceiveWithAnyArgs().Grant(default, default);
    }

    [TestMethod]
    public void HourlyUpdate_MissingDutyDefinition_CancelsActiveDutyArtifacts()
    {
        ConfigureDuties(); // empty pool -> FindDuty returns null
        _contentStore.Record.ActiveDutyId = "gone_from_config";

        _runtime.HourlyUpdate(100.0);

        Assert.IsFalse(_contentStore.Record.HasActiveDuty);
    }

    [TestMethod]
    public void HourlyUpdate_EnemyContactAltCompletion_CompletesDutyWithReward()
    {
        var duty = HuntDuty("scout_route");
        duty.AltCompletion = "EnemyContact";
        ConfigureDuties(duty);
        _contentStore.Record.ActiveDutyId = "scout_route";
        _contentStore.Record.ActiveDutyDeadlineDay = 200.0; // far from expiry
        _world.IsEnemyNearPlayer(Arg.Any<float>()).Returns(true);

        _runtime.HourlyUpdate(100.0);

        _rewards.Received(1).Grant(duty.ReportReward, "duty:scout_route");
        Assert.AreEqual(1, _contentStore.Record.DutySuccesses);
        Assert.IsFalse(_contentStore.Record.HasActiveDuty);
    }

    [TestMethod]
    public void HourlyUpdate_WaitHoursShiftEnded_CompletesDuty()
    {
        var duty = WaitDuty();
        ConfigureDuties(duty);
        _contentStore.Record.ActiveDutyId = "service_shift";
        _contentStore.Record.ShiftEndDay = 100.0;
        _contentStore.Record.ActiveDutyDeadlineDay = 200.0;

        _runtime.HourlyUpdate(100.5);

        Assert.AreEqual(1, _contentStore.Record.DutySuccesses);
        Assert.IsFalse(_contentStore.Record.HasActiveDuty);
    }

    [TestMethod]
    public void HourlyUpdate_WaitHoursShiftNotYetEnded_StaysActive()
    {
        var duty = WaitDuty();
        ConfigureDuties(duty);
        _contentStore.Record.ActiveDutyId = "service_shift";
        _contentStore.Record.ShiftEndDay = 100.5;
        _contentStore.Record.ActiveDutyDeadlineDay = 200.0;

        _runtime.HourlyUpdate(100.1);

        Assert.IsTrue(_contentStore.Record.HasActiveDuty);
    }

    [TestMethod]
    public void HourlyUpdate_DeadlineExpired_FailsDutyAndDestroysTarget()
    {
        var duty = HuntDuty("bandit_hunt");
        ConfigureDuties(duty);
        _contentStore.Record.ActiveDutyId = "bandit_hunt";
        _contentStore.Record.ActiveDutyTargetPartyId = "party_1";
        _contentStore.Record.ActiveDutyDeadlineDay = 100.0;

        _runtime.HourlyUpdate(100.0);

        _rewards.Received(1).AdjustTrust(-2);
        Assert.AreEqual(1, _contentStore.Record.DutyFailures);
        _world.Received(1).DestroyParty("party_1");
        Assert.IsFalse(_contentStore.Record.HasActiveDuty);
    }

    [TestMethod]
    public void HourlyUpdate_BeforeDeadlineAndNoAltCompletion_StaysActive()
    {
        var duty = HuntDuty("bandit_hunt");
        ConfigureDuties(duty);
        _contentStore.Record.ActiveDutyId = "bandit_hunt";
        _contentStore.Record.ActiveDutyDeadlineDay = 105.0;

        _runtime.HourlyUpdate(100.0);

        Assert.IsTrue(_contentStore.Record.HasActiveDuty);
        _rewards.DidNotReceiveWithAnyArgs().Grant(default, default);
    }

    [TestMethod]
    public void HourlyUpdate_FailWhileDetached_TransitionsBackAndParks()
    {
        var duty = HuntDuty("bandit_hunt");
        ConfigureDuties(duty);
        _contentStore.Record.ActiveDutyId = "bandit_hunt";
        _contentStore.Record.ActiveDutyDeadlineDay = 100.0;
        _stateMachine.State.Returns(EnlistmentState.EnlistedDetachedOnDuty);

        _runtime.HourlyUpdate(100.0);

        _stateMachine.Received(1).TryTransition(EnlistmentState.EnlistedAttached);
        _attachment.Received(1).EnsureParked("lord_1_1");
    }

    // ---- OnTargetPartyDestroyed ----

    [TestMethod]
    public void OnTargetPartyDestroyed_NoActiveDuty_NoOp()
    {
        _runtime.OnTargetPartyDestroyed("party_1");

        _rewards.DidNotReceiveWithAnyArgs().Grant(default, default);
    }

    [TestMethod]
    public void OnTargetPartyDestroyed_NonMatchingPartyId_NoOp()
    {
        ConfigureDuties(HuntDuty("recon_sweep"));
        _contentStore.Record.ActiveDutyId = "recon_sweep";
        _contentStore.Record.ActiveDutyTargetPartyId = "party_1";

        _runtime.OnTargetPartyDestroyed("party_other");

        Assert.IsTrue(_contentStore.Record.HasActiveDuty);
    }

    [TestMethod]
    public void OnTargetPartyDestroyed_MatchingPartyId_CompletesDuty()
    {
        var duty = HuntDuty("recon_sweep");
        ConfigureDuties(duty);
        _contentStore.Record.ActiveDutyId = "recon_sweep";
        _contentStore.Record.ActiveDutyTargetPartyId = "party_1";

        _runtime.OnTargetPartyDestroyed("party_1");

        _rewards.Received(1).Grant(duty.ReportReward, "duty:recon_sweep");
        Assert.AreEqual(1, _contentStore.Record.DutySuccesses);
        Assert.IsFalse(_contentStore.Record.HasActiveDuty);
    }

    // ---- OnSettlementEntered ----

    [TestMethod]
    public void OnSettlementEntered_NoActiveDuty_NoOp()
    {
        _runtime.OnSettlementEntered("town_1", 100.0);

        _rewards.DidNotReceiveWithAnyArgs().Grant(default, default);
    }

    [TestMethod]
    public void OnSettlementEntered_NonMatchingSettlementId_NoOp()
    {
        ConfigureDuties(VisitDuty("road_patrol"));
        _contentStore.Record.ActiveDutyId = "road_patrol";
        _contentStore.Record.ActiveDutySettlementId = "town_1";

        _runtime.OnSettlementEntered("town_2", 100.0);

        Assert.IsTrue(_contentStore.Record.HasActiveDuty);
    }

    [TestMethod]
    public void OnSettlementEntered_VisitSettlementMechanic_CompletesDuty()
    {
        var duty = VisitDuty("road_patrol");
        ConfigureDuties(duty);
        _contentStore.Record.ActiveDutyId = "road_patrol";
        _contentStore.Record.ActiveDutySettlementId = "town_1";

        _runtime.OnSettlementEntered("town_1", 100.0);

        Assert.AreEqual(1, _contentStore.Record.DutySuccesses);
        Assert.IsFalse(_contentStore.Record.HasActiveDuty);
    }

    [TestMethod]
    public void OnSettlementEntered_DeliverFoodEnoughFood_ConsumesAndCompletes()
    {
        var duty = DeliverFoodDuty("supply_delivery");
        ConfigureDuties(duty);
        _contentStore.Record.ActiveDutyId = "supply_delivery";
        _contentStore.Record.ActiveDutySettlementId = "town_1";
        _contentStore.Record.ActiveDutyFoodRequired = 6;
        _world.CountPlayerFood().Returns(10);
        // Consumption reports what it actually took; completion keys on that, not on the count.
        _world.ConsumePlayerFood(6).Returns(6);

        _runtime.OnSettlementEntered("town_1", 100.0);

        _world.Received(1).ConsumePlayerFood(6);
        Assert.AreEqual(1, _contentStore.Record.DutySuccesses);
        Assert.IsFalse(_contentStore.Record.HasActiveDuty);
    }

    [TestMethod]
    public void OnSettlementEntered_DeliverFoodCountLiesAboutDeliverableFood_StaysActive()
    {
        // Codex P2-2: the count once included livestock that consumption could never
        // remove, so the delivery completed for free. Completion now needs real handover.
        var duty = DeliverFoodDuty("supply_delivery");
        ConfigureDuties(duty);
        _contentStore.Record.ActiveDutyId = "supply_delivery";
        _contentStore.Record.ActiveDutySettlementId = "town_1";
        _contentStore.Record.ActiveDutyFoodRequired = 6;
        _world.CountPlayerFood().Returns(10);
        _world.ConsumePlayerFood(6).Returns(2);

        _runtime.OnSettlementEntered("town_1", 100.0);

        Assert.AreEqual(0, _contentStore.Record.DutySuccesses);
        Assert.IsTrue(_contentStore.Record.HasActiveDuty);
    }

    [TestMethod]
    public void OnSettlementEntered_DeliverFoodNotEnoughFood_StaysActiveNoConsumption()
    {
        var duty = DeliverFoodDuty("supply_delivery");
        ConfigureDuties(duty);
        _contentStore.Record.ActiveDutyId = "supply_delivery";
        _contentStore.Record.ActiveDutySettlementId = "town_1";
        _contentStore.Record.ActiveDutyFoodRequired = 6;
        _world.CountPlayerFood().Returns(2);

        _runtime.OnSettlementEntered("town_1", 100.0);

        _world.DidNotReceive().ConsumePlayerFood(Arg.Any<int>());
        Assert.IsTrue(_contentStore.Record.HasActiveDuty);
    }

    [TestMethod]
    public void OnSettlementEntered_CollectFoodMechanic_GrantsFoodAndCompletes()
    {
        var duty = CollectFoodDuty("forage");
        ConfigureDuties(duty);
        _contentStore.Record.ActiveDutyId = "forage";
        _contentStore.Record.ActiveDutySettlementId = "village_1";
        _random.Next(7).Returns(3); // bonus = 4 + 3 = 7

        _runtime.OnSettlementEntered("village_1", 100.0);

        _world.Received(1).GrantPlayerFood(13); // 6 base + 7 bonus
        Assert.AreEqual(1, _contentStore.Record.DutySuccesses);
        Assert.IsFalse(_contentStore.Record.HasActiveDuty);
    }

    // ---- CancelActive ----

    [TestMethod]
    public void CancelActive_NoActiveDuty_NoOp()
    {
        _runtime.CancelActive("test");

        _world.DidNotReceiveWithAnyArgs().DestroyParty(default);
    }

    [TestMethod]
    public void CancelActive_ActiveDutyWithTargetWhileDetached_DestroysTargetAndRestoresAttachment()
    {
        _contentStore.Record.ActiveDutyId = "recon_sweep";
        _contentStore.Record.ActiveDutyTargetPartyId = "party_1";
        _stateMachine.State.Returns(EnlistmentState.EnlistedDetachedOnDuty);

        _runtime.CancelActive("discharge");

        _world.Received(1).DestroyParty("party_1");
        _stateMachine.Received(1).TryTransition(EnlistmentState.EnlistedAttached);
        _attachment.Received(1).EnsureParked("lord_1_1");
        Assert.IsFalse(_contentStore.Record.HasActiveDuty);
    }

    [TestMethod]
    public void CancelActive_StateNotDetached_DoesNotTouchAttachmentOrStateMachine()
    {
        _contentStore.Record.ActiveDutyId = "service_shift"; // WaitHours duty stays attached
        _stateMachine.State.Returns(EnlistmentState.EnlistedAttached);

        _runtime.CancelActive("discharge");

        _attachment.DidNotReceive().EnsureParked(Arg.Any<string>());
        _stateMachine.DidNotReceive().TryTransition(EnlistmentState.EnlistedAttached);
        Assert.IsFalse(_contentStore.Record.HasActiveDuty);
    }
}
