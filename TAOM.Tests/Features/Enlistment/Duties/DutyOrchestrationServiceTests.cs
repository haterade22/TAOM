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
    public void CancelActiveDuty_AlwaysDelegatesToRuntimeRegardlessOfEnlistmentState()
    {
        _store.Record.State = EnlistmentState.NotEnlisted;

        _service.CancelActiveDuty("discharge");

        _runtime.Received(1).CancelActive("discharge");
    }

    // ---- RequestDutyNow (Batch 8: asking the commander for work) ------------------------

    private void OfferReady(DutyOfferSelection selection, bool rotationAllows = true)
    {
        _rotation.ShouldRollIncident(Arg.Any<SchedulerConfig>(), Arg.Any<int>(), Arg.Any<double>(), Arg.Any<double?>()).Returns(false);
        _rotation.ShouldOfferDuty(Arg.Any<SchedulerConfig>(), Arg.Any<int>(), Arg.Any<int>(), Arg.Any<bool>(), Arg.Any<double>(), Arg.Any<double?>()).Returns(rotationAllows);
        _selector.SelectOffer(Arg.Any<EnlistmentDutiesConfig>(), Arg.Any<ServiceProgressSnapshot>(), Arg.Any<ArmyRhythmSnapshot>(), Arg.Any<IReadOnlyList<string>>(), Arg.Any<bool>())
            .Returns(selection);
    }

    [TestMethod]
    public void RequestDutyNow_NotEnlisted_ReturnsNotEnlisted()
    {
        _store.Record.State = EnlistmentState.NotEnlisted;

        Assert.AreEqual(DutyRequestResult.NotEnlisted, _service.RequestDutyNow(100.0, 12.0));
        _selector.DidNotReceiveWithAnyArgs().SelectOffer(default, default, default, default, default);
    }

    [TestMethod]
    public void RequestDutyNow_AlreadyOnDuty_ReturnsAlreadyOnDuty()
    {
        _contentStore.Record.ActiveDutyId = "recon_sweep";

        Assert.AreEqual(DutyRequestResult.AlreadyOnDuty, _service.RequestDutyNow(100.0, 12.0));
        _selector.DidNotReceiveWithAnyArgs().SelectOffer(default, default, default, default, default);
    }

    [TestMethod]
    public void RequestDutyNow_RotationSaysNo_ReturnsNoWork_AndAssignsNothing()
    {
        // Asking is free, but it cannot conjure work the rotation would not have given — the
        // whole point of sharing ONE cadence rather than adding a second cooldown.
        OfferReady(DutyOfferSelection.None, rotationAllows: false);

        Assert.AreEqual(DutyRequestResult.NoWorkAvailable, _service.RequestDutyNow(100.0, 12.0));
        _runtime.DidNotReceiveWithAnyArgs().Start(default, default);
        Assert.IsNull(_contentStore.Record.LastOfferDay);
    }

    [TestMethod]
    public void RequestDutyNow_FieldDutyAvailable_StartsItAndReportsAssigned()
    {
        var duty = new DutyDefinition { Id = "recon_sweep" };
        OfferReady(new DutyOfferSelection { FieldDuty = duty });
        _runtime.Start(duty, 100.0).Returns(true);

        Assert.AreEqual(DutyRequestResult.DutyAssigned, _service.RequestDutyNow(100.0, 12.0));
        _runtime.Received(1).Start(duty, 100.0);
        CollectionAssert.Contains(_contentStore.Record.RecentDutyIds, "recon_sweep");
    }

    [TestMethod]
    public void RequestDutyNow_FieldDutyFailsToStart_ReportsNoWorkRatherThanSuccess()
    {
        // The runtime refuses when it cannot find an anchor settlement or spawn a target. Telling
        // the player they were assigned work that does not exist is worse than telling them there
        // is none.
        var duty = new DutyDefinition { Id = "recon_sweep" };
        OfferReady(new DutyOfferSelection { FieldDuty = duty });
        _runtime.Start(duty, 100.0).Returns(false);

        Assert.AreEqual(DutyRequestResult.NoWorkAvailable, _service.RequestDutyNow(100.0, 12.0));
    }

    [TestMethod]
    public void RequestDutyNow_InteractiveDuty_PresentsItAndReportsAssigned()
    {
        var duty = new InteractiveDutyDefinition { Id = "quartermaster_count" };
        OfferReady(new DutyOfferSelection { InteractiveDuty = duty });

        Assert.AreEqual(DutyRequestResult.DutyAssigned, _service.RequestDutyNow(100.0, 12.0));
        _presenter.Received(1).PresentInteractiveDuty(duty, Arg.Any<ServiceProgressSnapshot>(), Arg.Any<int>());
    }

    [TestMethod]
    public void RequestDutyNow_AndDailyOfferTick_ShareTheSameOfferPath()
    {
        // ANTI-DRIFT PIN. Two offer implementations is how the donor ended up with a wait menu
        // that made terminal decisions under a different policy than its daily tick. Both callers
        // must consult the same rotation gate and the same selector, with the same arguments.
        var duty = new DutyDefinition { Id = "recon_sweep" };
        OfferReady(new DutyOfferSelection { FieldDuty = duty });
        _runtime.Start(duty, Arg.Any<double>()).Returns(true);

        _service.DailyOfferTick(100.0, 12.0);
        _contentStore.Record.ActiveDutyId = null;
        _contentStore.Record.LastOfferDay = null;
        _service.RequestDutyNow(100.0, 12.0);

        _rotation.Received(2).ShouldOfferDuty(
            Arg.Any<SchedulerConfig>(), Arg.Any<int>(), Arg.Any<int>(), Arg.Any<bool>(),
            Arg.Any<double>(), Arg.Any<double?>());
        _selector.Received(2).SelectOffer(
            Arg.Any<EnlistmentDutiesConfig>(), Arg.Any<ServiceProgressSnapshot>(),
            Arg.Any<ArmyRhythmSnapshot>(), Arg.Any<IReadOnlyList<string>>(), Arg.Any<bool>());
        _runtime.Received(2).Start(duty, 100.0);
    }
}
