using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Adapters;
using TAOM.Core.Logging;
using TAOM.Features.Enlistment;
using TAOM.Features.Enlistment.Domain;
using TAOM.Features.FiefManagement;

namespace TAOM.Tests.Features.FiefManagement;

[TestClass]
public class FiefHubOpenerTests
{
    private IFiefManagementSettingsProvider _settings = null!;
    private IFiefHubService _service = null!;
    private IEnlistmentStateQuery _enlistment = null!;
    private IFiefHubHostAdapter _host = null!;
    private IModLogger _logger = null!;
    private FiefHubOpener _sut = null!;

    [TestInitialize]
    public void Setup()
    {
        _settings = Substitute.For<IFiefManagementSettingsProvider>();
        _service = Substitute.For<IFiefHubService>();
        _enlistment = Substitute.For<IEnlistmentStateQuery>();
        _host = Substitute.For<IFiefHubHostAdapter>();
        _logger = Substitute.For<IModLogger>();

        // Default world: feature on, map clear, not enlisted-attached, two fiefs.
        _settings.EnableFiefManagement.Returns(true);
        _host.IsMapClearForMenu.Returns(true);
        _enlistment.State.Returns(EnlistmentState.NotEnlisted);
        _service.Count.Returns(2);

        _sut = new FiefHubOpener(_settings, _service, _enlistment, _host, _logger);
    }

    // ---------- GetAvailability: one test per guard ----------

    [TestMethod]
    public void GetAvailability_EverythingClearAndFiefsOwned_IsAvailable()
    {
        Assert.AreEqual(FiefHubAvailability.Available, _sut.GetAvailability());
    }

    [TestMethod]
    public void GetAvailability_FeatureOff_IsFeatureDisabled()
    {
        _settings.EnableFiefManagement.Returns(false);

        Assert.AreEqual(FiefHubAvailability.FeatureDisabled, _sut.GetAvailability());
    }

    [TestMethod]
    public void GetAvailability_MapNotClearForMenu_IsMapBusy()
    {
        _host.IsMapClearForMenu.Returns(false);

        Assert.AreEqual(FiefHubAvailability.MapBusy, _sut.GetAvailability());
    }

    [TestMethod]
    public void GetAvailability_EnlistedAttached_IsMapBusy()
    {
        _enlistment.State.Returns(EnlistmentState.EnlistedAttached);

        Assert.AreEqual(FiefHubAvailability.MapBusy, _sut.GetAvailability());
    }

    [DataTestMethod]
    [DataRow(EnlistmentState.NotEnlisted)]
    [DataRow(EnlistmentState.PetitionPending)]
    [DataRow(EnlistmentState.EnlistedBattle)]
    [DataRow(EnlistmentState.EnlistedDetachedOnDuty)]
    [DataRow(EnlistmentState.EnlistedPlayerCaptive)]
    [DataRow(EnlistmentState.CommanderUnavailable)]
    [DataRow(EnlistmentState.Discharging)]
    public void GetAvailability_OtherEnlistmentStates_StayAvailable(EnlistmentState state)
    {
        // Only the attached state is a modal of its own; the free-roam states keep the hub. Discharging never
        // persists (EnlistmentRecord coerces it to EnlistedAttached); the query's enum can still name it,
        // and the opener treats it like any non-attached state.
        _enlistment.State.Returns(state);

        Assert.AreEqual(FiefHubAvailability.Available, _sut.GetAvailability());
    }

    [TestMethod]
    public void GetAvailability_NoFiefsOwned_IsNoFiefs()
    {
        _service.Count.Returns(0);

        Assert.AreEqual(FiefHubAvailability.NoFiefs, _sut.GetAvailability());
    }

    [TestMethod]
    public void GetAvailability_FeatureOffAndMapBusyAndNoFiefs_ReportsFeatureOffFirst()
    {
        _settings.EnableFiefManagement.Returns(false);
        _host.IsMapClearForMenu.Returns(false);
        _service.Count.Returns(0);

        Assert.AreEqual(FiefHubAvailability.FeatureDisabled, _sut.GetAvailability());
    }

    [TestMethod]
    public void GetAvailability_MapBusyAndNoFiefs_ReportsMapBusyFirst()
    {
        _host.IsMapClearForMenu.Returns(false);
        _service.Count.Returns(0);

        Assert.AreEqual(FiefHubAvailability.MapBusy, _sut.GetAvailability());
    }

    [TestMethod]
    public void GetAvailability_MapBusy_DoesNotCountFiefs()
    {
        // The button polls this every frame; a busy map must not pay for the fief count.
        _host.IsMapClearForMenu.Returns(false);

        _sut.GetAvailability();

        _ = _service.DidNotReceive().Count;
    }

    [TestMethod]
    public void GetAvailability_HasNoSideEffects()
    {
        _sut.GetAvailability();

        _host.DidNotReceive().OpenHub();
        _host.DidNotReceive().ShowNoFiefsMessage();
    }

    // ---------- TryOpen ----------

    [TestMethod]
    public void TryOpen_Available_OpensTheHubOnce()
    {
        _sut.TryOpen("F6");

        _host.Received(1).OpenHub();
        _host.DidNotReceive().ShowNoFiefsMessage();
    }

    [TestMethod]
    public void TryOpen_NoFiefs_ShowsTheMessageAndDoesNotOpen()
    {
        _service.Count.Returns(0);

        _sut.TryOpen("F6");

        _host.Received(1).ShowNoFiefsMessage();
        _host.DidNotReceive().OpenHub();
    }

    [TestMethod]
    public void TryOpen_FeatureOff_IsSilent()
    {
        _settings.EnableFiefManagement.Returns(false);

        _sut.TryOpen("F6");

        _host.DidNotReceive().OpenHub();
        _host.DidNotReceive().ShowNoFiefsMessage();
    }

    [TestMethod]
    public void TryOpen_MapBusy_IsSilent()
    {
        _host.IsMapClearForMenu.Returns(false);

        _sut.TryOpen("nav button");

        _host.DidNotReceive().OpenHub();
        _host.DidNotReceive().ShowNoFiefsMessage();
    }

    [TestMethod]
    public void TryOpen_EnlistedAttached_IsSilent()
    {
        _enlistment.State.Returns(EnlistmentState.EnlistedAttached);

        _sut.TryOpen("F6");

        _host.DidNotReceive().OpenHub();
        _host.DidNotReceive().ShowNoFiefsMessage();
    }

    // ---------- debug logging ----------

    [TestMethod]
    public void TryOpen_DebugModeOn_LogsTheSourceAndCount()
    {
        _settings.IsDebugMode.Returns(true);

        _sut.TryOpen("nav button");

        _logger.Received(1).LogInfo(Arg.Is<string>(m => m.Contains("nav button") && m.Contains("count=2")));
    }

    [TestMethod]
    public void TryOpen_DebugModeOff_LogsNothing()
    {
        _settings.IsDebugMode.Returns(false);

        _sut.TryOpen("F6");

        _logger.DidNotReceiveWithAnyArgs().LogInfo(default!);
    }

    [TestMethod]
    public void TryOpen_DebugModeOnAndNoFiefs_LogsTheSource()
    {
        _settings.IsDebugMode.Returns(true);
        _service.Count.Returns(0);

        _sut.TryOpen("F6");

        _logger.Received(1).LogInfo(Arg.Is<string>(m => m.Contains("F6") && m.Contains("no fiefs")));
    }
}
