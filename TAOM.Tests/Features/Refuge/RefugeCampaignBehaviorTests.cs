using System.Collections.Generic;
using System.Reflection;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Adapters;
using TAOM.Core.Logging;
using TAOM.Features.Refuge;
using TAOM.Features.Refuge.Domain;
using TAOM.Features.Refuge.Hooks;
using TaleWorlds.CampaignSystem;

namespace TAOM.Tests.Features.Refuge;

/// <summary>
/// The session-reset pattern on <see cref="RefugeCampaignBehavior"/> (the
/// FieldCampBehaviorSessionResetTests shape): SyncData marks the session synced ONLY when the
/// store is loading; OnGameLoaded and the session-launch gate reset the process-lifetime service
/// for a fresh campaign AND for a save with no refuge record. Without it, campaign B inherits
/// campaign A's book from the singleton and then saves it as its own. The gate latches, so the
/// two callers cannot double-wipe a book founded right after launch.
/// </summary>
[TestClass]
public class RefugeCampaignBehaviorTests
{
    private IRefugeService _refuges = null!;
    private RefugeCampaignBehavior _sut = null!;

    [TestInitialize]
    public void Setup()
    {
        _refuges = Substitute.For<IRefugeService>();
        _sut = new RefugeCampaignBehavior(
            _refuges,
            Substitute.For<IRefugeSettingsProvider>(),
            Substitute.For<IRefugeVisualService>(),
            Substitute.For<IWardenService>(),
            Substitute.For<IGameMenuAdapter>(),
            Substitute.For<IEncounterAdapter>(),
            Substitute.For<IModLogger>());
    }

    private void SyncWith(bool isLoading)
    {
        var dataStore = Substitute.For<IDataStore>();
        dataStore.IsLoading.Returns(isLoading);
        _sut.SyncData(dataStore);
    }

    private void InvokeOnGameLoaded()
    {
        var method = typeof(RefugeCampaignBehavior).GetMethod(
            "OnGameLoaded", BindingFlags.NonPublic | BindingFlags.Instance);
        Assert.IsNotNull(method, "OnGameLoaded must be a private instance method");
        method!.Invoke(_sut, new object[] { null! });
    }

    // --- The reset gate ---

    [TestMethod]
    public void FreshCampaign_NoSyncData_ResetsTheService()
    {
        Assert.IsTrue(_sut.ResetIfNoLoadedRecord());

        _refuges.Received(1).ResetForNewSession();
    }

    [TestMethod]
    public void LoadingSyncData_MarksTheSessionSynced_NoReset()
    {
        SyncWith(isLoading: true);

        Assert.IsFalse(_sut.ResetIfNoLoadedRecord());
        _refuges.DidNotReceive().ResetForNewSession();
    }

    [TestMethod]
    public void SavingSyncData_DoesNotCountAsSynced()
    {
        // Only the LOADING direction proves this session's book came from this save; a save pass
        // over a stale book must not launder it into "synced".
        SyncWith(isLoading: false);

        Assert.IsTrue(_sut.ResetIfNoLoadedRecord());
        _refuges.Received(1).ResetForNewSession();
    }

    [TestMethod]
    public void ResetLatches_SecondCallerCannotWipeThePostLaunchBook()
    {
        _sut.ResetIfNoLoadedRecord();
        Assert.IsFalse(_sut.ResetIfNoLoadedRecord());

        _refuges.Received(1).ResetForNewSession();
    }

    // --- OnGameLoaded routing ---

    [TestMethod]
    public void OnGameLoaded_AfterLoadingSync_RunsTheServicePostLoadRepair()
    {
        SyncWith(isLoading: true);

        InvokeOnGameLoaded();

        _refuges.Received(1).OnGameLoaded();
        _refuges.DidNotReceive().ResetForNewSession();
    }

    [TestMethod]
    public void OnGameLoaded_PreFeatureSave_ResetsInsteadOfReconcilingStaleState()
    {
        // No SyncData ran (the save predates the feature): the singleton still holds the
        // previous session's book; reconciling it would adopt/re-show stale state.
        InvokeOnGameLoaded();

        _refuges.Received(1).ResetForNewSession();
        _refuges.DidNotReceive().OnGameLoaded();
    }

    // --- SyncData plumbing ---

    [TestMethod]
    public void SyncData_RoundTripsBookThroughTheService()
    {
        SyncWith(isLoading: false);

        _refuges.Received(1).SaveInto(out Arg.Any<Dictionary<string, RefugeData>>(), out Arg.Any<int>());
        _refuges.Received(1).LoadFrom(Arg.Any<Dictionary<string, RefugeData>>(), Arg.Any<int>());
    }

    [TestMethod]
    public void Behavior_IsCampaignBehaviorBase()
    {
        Assert.IsInstanceOfType(_sut, typeof(CampaignBehaviorBase));
    }
}
