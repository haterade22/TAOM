using System.Collections.Generic;
using System.Reflection;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Adapters;
using TAOM.Core.Logging;
using TAOM.Features.FieldCamp;
using TAOM.Features.FieldCamp.Domain;
using TAOM.Features.FieldCamp.Hooks;
using TAOM.Features.SupplyLines;
using TaleWorlds.CampaignSystem;

namespace TAOM.Tests.Features.FieldCamp;

/// <summary>
/// The session-reset pattern (round-A CRITICAL): the camp book lives in a process-lifetime
/// singleton and SyncData only runs when a save record exists, so a fresh campaign, or a save
/// from before the feature, MUST reset the service or it inherits (and then saves) the previous
/// campaign's camps. The behavior tracks whether a LOADING SyncData ran; anything else resets.
/// Private callbacks are reflection-invoked (the FiefHubCampaignBehaviorTests pattern);
/// ResetIfNoLoadedRecord is internal via InternalsVisibleTo.
/// </summary>
[TestClass]
public class FieldCampBehaviorSessionResetTests
{
    private ICampService _camps = null!;
    private FieldCampCampaignBehavior _sut = null!;

    [TestInitialize]
    public void Setup()
    {
        _camps = Substitute.For<ICampService>();
        _sut = new FieldCampCampaignBehavior(
            _camps,
            Substitute.For<ICampSettingsProvider>(),
            Substitute.For<ICampVisualService>(),
            Substitute.For<ISupplyLinesSettingsProvider>(),
            Substitute.For<IGameMenuAdapter>(),
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
        var method = typeof(FieldCampCampaignBehavior).GetMethod(
            "OnGameLoaded", BindingFlags.NonPublic | BindingFlags.Instance);
        Assert.IsNotNull(method, "OnGameLoaded must be a private instance method");
        method.Invoke(_sut, new object[] { null! });
    }

    [TestMethod]
    public void FreshCampaign_NoSyncData_ResetsTheService()
    {
        Assert.IsTrue(_sut.ResetIfNoLoadedRecord());

        _camps.Received(1).ResetForNewSession();
    }

    [TestMethod]
    public void ResetIfNoLoadedRecord_Latches_SecondCallNeverWipesAgain()
    {
        // The Refuge/SupplyLines twins document this latch as load-bearing: a stray second call
        // after the player pitched a camp must not wipe it (round-B parity finding).
        Assert.IsTrue(_sut.ResetIfNoLoadedRecord());

        Assert.IsFalse(_sut.ResetIfNoLoadedRecord());
        _camps.Received(1).ResetForNewSession();
    }

    [TestMethod]
    public void LoadingSyncData_MarksTheSessionSynced_NoReset()
    {
        SyncWith(isLoading: true);

        Assert.IsFalse(_sut.ResetIfNoLoadedRecord());
        _camps.DidNotReceive().ResetForNewSession();
    }

    [TestMethod]
    public void SavingSyncData_DoesNotCountAsSynced()
    {
        // Only the LOADING direction proves this session's book came from this save; a save pass
        // over a stale book must not launder it into "synced".
        SyncWith(isLoading: false);

        Assert.IsTrue(_sut.ResetIfNoLoadedRecord());
        _camps.Received(1).ResetForNewSession();
    }

    [TestMethod]
    public void OnGameLoaded_AfterLoadingSync_RunsTheServicePostLoadRepair()
    {
        SyncWith(isLoading: true);

        InvokeOnGameLoaded();

        _camps.Received(1).OnGameLoaded();
        _camps.DidNotReceive().ResetForNewSession();
    }

    [TestMethod]
    public void OnGameLoaded_PreFeatureSave_ResetsInsteadOfReshowingStaleVisuals()
    {
        // No SyncData ran (the save predates the feature): the singleton still holds the
        // previous session's book, and OnGameLoaded must not re-show its visuals.
        InvokeOnGameLoaded();

        _camps.Received(1).ResetForNewSession();
        _camps.DidNotReceive().OnGameLoaded();
    }

    [TestMethod]
    public void SyncData_LoadDirection_NullsFirstSoAMissingKeyLoadsAnEmptyBook()
    {
        // BehaviorSaveData.SyncData leaves the ref UNCHANGED on a missing key (1.4.8), and the
        // substitute store mimics exactly that. Pre-seeding from SaveInto would hand the live
        // singleton book straight back to LoadFrom; null-first means a key miss loads empty.
        SyncWith(isLoading: true);

        _camps.DidNotReceive().SaveInto(out Arg.Any<Dictionary<string, CampState>>());
        _camps.Received(1).LoadFrom(null);
    }

    [TestMethod]
    public void SyncData_SaveDirection_NeverCallsLoadFrom()
    {
        // LoadFrom has load-only semantics (it wipes the inquiry latches and the ambush-scan
        // clock); running it on every autosave mutates live state (round-B).
        SyncWith(isLoading: false);

        _camps.Received(1).SaveInto(out Arg.Any<Dictionary<string, CampState>>());
        _camps.DidNotReceive().LoadFrom(Arg.Any<Dictionary<string, CampState>>());
    }

    [TestMethod]
    public void Behavior_IsCampaignBehaviorBase()
    {
        Assert.IsInstanceOfType(_sut, typeof(CampaignBehaviorBase));
    }
}
