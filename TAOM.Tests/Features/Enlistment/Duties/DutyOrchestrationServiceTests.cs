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

namespace TAOM.Tests.Features.Enlistment.Duties;

[TestClass]
public class DutyOrchestrationServiceTests
{
    private EnlistmentStore _store = null!;
    private EnlistmentContentStore _contentStore = null!;
    private IEnlistmentContentConfigProvider _config = null!;
    private IArmyRhythmSnapshotService _rhythm = null!;
    private IHeroSkillXpAdapter _skillXp = null!;
    private IDutyRotationPolicy _rotation = null!;
    private IDutySelector _selector = null!;
    private IFieldDutyRuntime _runtime = null!;
    private IInteractiveDutyPresenter _presenter = null!;
    private DutyOrchestrationService _service = null!;

    [TestInitialize]
    public void Setup()
    {
        var logger = Substitute.For<IModLogger>();
        _store = new EnlistmentStore(logger);
        _contentStore = new EnlistmentContentStore(logger);
        _config = Substitute.For<IEnlistmentContentConfigProvider>();
        _config.GetConfig().Returns(EnlistmentContentConfigProvider.BuildDefaults());
        _config.GetDuties().Returns(new EnlistmentDutiesConfig());
        _rhythm = Substitute.For<IArmyRhythmSnapshotService>();
        _rhythm.GetSnapshot(Arg.Any<double>(), Arg.Any<double>()).Returns(new ArmyRhythmSnapshot());
        _skillXp = Substitute.For<IHeroSkillXpAdapter>();
        _rotation = Substitute.For<IDutyRotationPolicy>();
        _selector = Substitute.For<IDutySelector>();
        _runtime = Substitute.For<IFieldDutyRuntime>();
        _presenter = Substitute.For<IInteractiveDutyPresenter>();

        _service = new DutyOrchestrationService(_store, _contentStore, _config, _rhythm, _skillXp, _rotation, _selector, _runtime, _presenter);

        _store.Record.State = EnlistmentState.EnlistedAttached;
        _store.Record.EnlistedHeroId = "main_hero";
        _store.Record.CommanderHeroId = "lord_1_1";
    }

    // ---- HourlyTick ----

    [TestMethod]
    public void HourlyTick_NotEnlisted_DoesNotCallRuntime()
    {
        _store.Record.State = EnlistmentState.NotEnlisted;

        _service.HourlyTick(100.0);

        _runtime.DidNotReceive().HourlyUpdate(Arg.Any<double>());
    }

    [TestMethod]
    public void HourlyTick_Enlisted_DelegatesToRuntime()
    {
        _service.HourlyTick(100.0);

        _runtime.Received(1).HourlyUpdate(100.0);
    }

    // ---- DailyOfferTick ----

    [TestMethod]
    public void DailyOfferTick_NotEnlisted_NoOp()
    {
        _store.Record.State = EnlistmentState.NotEnlisted;

        _service.DailyOfferTick(100.0, 12.0);

        _rotation.DidNotReceiveWithAnyArgs().ShouldRollIncident(default, default, default, default);
    }

    [TestMethod]
    public void DailyOfferTick_ActiveDutyInProgress_NoOp()
    {
        _contentStore.Record.ActiveDutyId = "recon_sweep";

        _service.DailyOfferTick(100.0, 12.0);

        _rotation.DidNotReceiveWithAnyArgs().ShouldRollIncident(default, default, default, default);
        _rotation.DidNotReceiveWithAnyArgs().ShouldOfferDuty(default, default, default, default, default, default);
    }

    [TestMethod]
    public void DailyOfferTick_IncidentRollsAndFound_PresentsIncidentAndSkipsDutyRoll()
    {
        var incident = new IncidentDefinition { Id = "pay_delay" };
        _rotation.ShouldRollIncident(Arg.Any<SchedulerConfig>(), Arg.Any<int>(), Arg.Any<double>(), Arg.Any<double?>()).Returns(true);
        _selector.SelectIncident(Arg.Any<IReadOnlyList<IncidentDefinition>>(), Arg.Any<ServiceProgressSnapshot>(), Arg.Any<ArmyRhythmSnapshot>()).Returns(incident);

        _service.DailyOfferTick(100.0, 12.0);

        _presenter.Received(1).PresentIncident(incident, Arg.Any<ServiceProgressSnapshot>(), 0);
        _rotation.DidNotReceiveWithAnyArgs().ShouldOfferDuty(default, default, default, default, default, default);
        Assert.AreEqual(100.0, _contentStore.Record.LastIncidentDay);
    }

    [TestMethod]
    public void DailyOfferTick_IncidentRollsButNoneFound_FallsThroughToDutyRoll()
    {
        _rotation.ShouldRollIncident(Arg.Any<SchedulerConfig>(), Arg.Any<int>(), Arg.Any<double>(), Arg.Any<double?>()).Returns(true);
        _selector.SelectIncident(Arg.Any<IReadOnlyList<IncidentDefinition>>(), Arg.Any<ServiceProgressSnapshot>(), Arg.Any<ArmyRhythmSnapshot>()).Returns((IncidentDefinition)null);
        _rotation.ShouldOfferDuty(Arg.Any<SchedulerConfig>(), Arg.Any<int>(), Arg.Any<int>(), Arg.Any<bool>(), Arg.Any<double>(), Arg.Any<double?>()).Returns(false);

        _service.DailyOfferTick(100.0, 12.0);

        _rotation.Received(1).ShouldOfferDuty(Arg.Any<SchedulerConfig>(), Arg.Any<int>(), Arg.Any<int>(), Arg.Any<bool>(), Arg.Any<double>(), Arg.Any<double?>());
        Assert.AreEqual(100.0, _contentStore.Record.LastIncidentDay, "cadence-day still stamped even when no incident matched");
    }

    [TestMethod]
    public void DailyOfferTick_IncidentCadenceNotDue_GoesStraightToDutyRoll()
    {
        _rotation.ShouldRollIncident(Arg.Any<SchedulerConfig>(), Arg.Any<int>(), Arg.Any<double>(), Arg.Any<double?>()).Returns(false);
        _rotation.ShouldOfferDuty(Arg.Any<SchedulerConfig>(), Arg.Any<int>(), Arg.Any<int>(), Arg.Any<bool>(), Arg.Any<double>(), Arg.Any<double?>()).Returns(false);

        _service.DailyOfferTick(100.0, 12.0);

        _selector.DidNotReceiveWithAnyArgs().SelectIncident(default, default, default);
        Assert.IsNull(_contentStore.Record.LastIncidentDay);
    }

    [TestMethod]
    public void DailyOfferTick_OfferCadenceNotDue_NoOfferSelected()
    {
        _rotation.ShouldRollIncident(Arg.Any<SchedulerConfig>(), Arg.Any<int>(), Arg.Any<double>(), Arg.Any<double?>()).Returns(false);
        _rotation.ShouldOfferDuty(Arg.Any<SchedulerConfig>(), Arg.Any<int>(), Arg.Any<int>(), Arg.Any<bool>(), Arg.Any<double>(), Arg.Any<double?>()).Returns(false);

        _service.DailyOfferTick(100.0, 12.0);

        _selector.DidNotReceiveWithAnyArgs().SelectOffer(default, default, default, default, default);
        Assert.IsNull(_contentStore.Record.LastOfferDay);
    }

    [TestMethod]
    public void DailyOfferTick_OfferDueButNothingEligible_NoOp()
    {
        _rotation.ShouldRollIncident(Arg.Any<SchedulerConfig>(), Arg.Any<int>(), Arg.Any<double>(), Arg.Any<double?>()).Returns(false);
        _rotation.ShouldOfferDuty(Arg.Any<SchedulerConfig>(), Arg.Any<int>(), Arg.Any<int>(), Arg.Any<bool>(), Arg.Any<double>(), Arg.Any<double?>()).Returns(true);
        _selector.SelectOffer(Arg.Any<EnlistmentDutiesConfig>(), Arg.Any<ServiceProgressSnapshot>(), Arg.Any<ArmyRhythmSnapshot>(), Arg.Any<IReadOnlyList<string>>(), Arg.Any<bool>())
            .Returns(DutyOfferSelection.None);

        _service.DailyOfferTick(100.0, 12.0);

        Assert.IsNull(_contentStore.Record.LastOfferDay);
        _runtime.DidNotReceiveWithAnyArgs().Start(default, default);
    }

    [TestMethod]
    public void DailyOfferTick_FieldDutySelected_StartsRuntimeAndRecordsRecent()
    {
        var duty = new DutyDefinition { Id = "recon_sweep" };
        _rotation.ShouldRollIncident(Arg.Any<SchedulerConfig>(), Arg.Any<int>(), Arg.Any<double>(), Arg.Any<double?>()).Returns(false);
        _rotation.ShouldOfferDuty(Arg.Any<SchedulerConfig>(), Arg.Any<int>(), Arg.Any<int>(), Arg.Any<bool>(), Arg.Any<double>(), Arg.Any<double?>()).Returns(true);
        _selector.SelectOffer(Arg.Any<EnlistmentDutiesConfig>(), Arg.Any<ServiceProgressSnapshot>(), Arg.Any<ArmyRhythmSnapshot>(), Arg.Any<IReadOnlyList<string>>(), Arg.Any<bool>())
            .Returns(new DutyOfferSelection { FieldDuty = duty });

        _service.DailyOfferTick(100.0, 12.0);

        _runtime.Received(1).Start(duty, 100.0);
        Assert.AreEqual(100.0, _contentStore.Record.LastOfferDay);
        CollectionAssert.Contains(_contentStore.Record.RecentDutyIds, "recon_sweep");
    }

    [TestMethod]
    public void DailyOfferTick_InteractiveDutySelected_PresentsInteractiveDutyAndRecordsRecent()
    {
        var duty = new InteractiveDutyDefinition { Id = "night_patrol" };
        _rotation.ShouldRollIncident(Arg.Any<SchedulerConfig>(), Arg.Any<int>(), Arg.Any<double>(), Arg.Any<double?>()).Returns(false);
        _rotation.ShouldOfferDuty(Arg.Any<SchedulerConfig>(), Arg.Any<int>(), Arg.Any<int>(), Arg.Any<bool>(), Arg.Any<double>(), Arg.Any<double?>()).Returns(true);
        _selector.SelectOffer(Arg.Any<EnlistmentDutiesConfig>(), Arg.Any<ServiceProgressSnapshot>(), Arg.Any<ArmyRhythmSnapshot>(), Arg.Any<IReadOnlyList<string>>(), Arg.Any<bool>())
            .Returns(new DutyOfferSelection { InteractiveDuty = duty });

        _service.DailyOfferTick(100.0, 12.0);

        _presenter.Received(1).PresentInteractiveDuty(duty, Arg.Any<ServiceProgressSnapshot>(), 0);
        CollectionAssert.Contains(_contentStore.Record.RecentDutyIds, "night_patrol");
    }

    [TestMethod]
    public void DailyOfferTick_RecentDutyIdsCappedAtFive()
    {
        // Newest-at-front order (matches RememberOffered's Insert(0,...) convention) — "a" is oldest.
        _contentStore.Record.RecentDutyIds.AddRange(new[] { "e", "d", "c", "b", "a" });
        var duty = new DutyDefinition { Id = "f" };
        _rotation.ShouldRollIncident(Arg.Any<SchedulerConfig>(), Arg.Any<int>(), Arg.Any<double>(), Arg.Any<double?>()).Returns(false);
        _rotation.ShouldOfferDuty(Arg.Any<SchedulerConfig>(), Arg.Any<int>(), Arg.Any<int>(), Arg.Any<bool>(), Arg.Any<double>(), Arg.Any<double?>()).Returns(true);
        _selector.SelectOffer(Arg.Any<EnlistmentDutiesConfig>(), Arg.Any<ServiceProgressSnapshot>(), Arg.Any<ArmyRhythmSnapshot>(), Arg.Any<IReadOnlyList<string>>(), Arg.Any<bool>())
            .Returns(new DutyOfferSelection { FieldDuty = duty });

        _service.DailyOfferTick(100.0, 12.0);

        Assert.AreEqual(5, _contentStore.Record.RecentDutyIds.Count);
        CollectionAssert.DoesNotContain(_contentStore.Record.RecentDutyIds, "a", "oldest entry evicted");
        Assert.AreEqual("f", _contentStore.Record.RecentDutyIds[0], "newest entry is inserted at the front");
    }

    // ---- Passthrough wiring ----

    [TestMethod]
    public void OnSettlementEntered_NotEnlisted_NoOp()
    {
        _store.Record.State = EnlistmentState.NotEnlisted;

        _service.OnSettlementEntered("town_1", 100.0);

        _runtime.DidNotReceiveWithAnyArgs().OnSettlementEntered(default, default);
    }

    [TestMethod]
    public void OnSettlementEntered_Enlisted_DelegatesToRuntime()
    {
        _service.OnSettlementEntered("town_1", 100.0);

        _runtime.Received(1).OnSettlementEntered("town_1", 100.0);
    }

    [TestMethod]
    public void OnMobilePartyDestroyed_NotEnlisted_NoOp()
    {
        _store.Record.State = EnlistmentState.NotEnlisted;

        _service.OnMobilePartyDestroyed("party_1");

        _runtime.DidNotReceiveWithAnyArgs().OnTargetPartyDestroyed(default);
    }

    [TestMethod]
    public void OnMobilePartyDestroyed_Enlisted_DelegatesToRuntime()
    {
        _service.OnMobilePartyDestroyed("party_1");

        _runtime.Received(1).OnTargetPartyDestroyed("party_1");
    }

    [TestMethod]
    public void CancelActiveDuty_AlwaysDelegatesToRuntimeRegardlessOfEnlistmentState()
    {
        _store.Record.State = EnlistmentState.NotEnlisted;

        _service.CancelActiveDuty("discharge");

        _runtime.Received(1).CancelActive("discharge");
    }
}
