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
/// campaign's camps. The reset runs in the constructor (#773): SubModule builds the behavior in
/// every campaign's OnGameStart, before any campaign event and before a load's SyncData, which
/// <c>PerCampaignBehaviorWiringTests</c> pins. Private callbacks are reflection-invoked (the
/// FiefHubCampaignBehaviorTests pattern).
/// </summary>
[TestClass]
[TestCategory("RequiresGame")]
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
    public void Constructor_FreshCampaign_ResetsTheService()
    {
        _camps.Received(1).ResetForNewSession();
    }

    [TestMethod]
    public void LoadingSyncData_LoadsTheBookOverTheConstructorReset()
    {
        SyncWith(isLoading: true);

        Received.InOrder(() =>
        {
            _camps.ResetForNewSession();
            _camps.LoadFrom(null);
        });
    }

    [TestMethod]
    public void SavingSyncData_DoesNotResetAgain()
    {
        SyncWith(isLoading: false);

        _camps.Received(1).ResetForNewSession();
    }

    [TestMethod]
    public void OnGameLoaded_AfterLoadingSync_RunsTheServicePostLoadRepair()
    {
        SyncWith(isLoading: true);

        InvokeOnGameLoaded();

        _camps.Received(1).OnGameLoaded();
        _camps.Received(1).ResetForNewSession();
    }

    [TestMethod]
    public void OnGameLoaded_PreFeatureSave_RepairsTheBookTheConstructorReset()
    {
        // No SyncData ran (the save predates the feature): the constructor already emptied the
        // book, so the post-load repair runs on an empty book and nothing resets a second time.
        InvokeOnGameLoaded();

        _camps.Received(1).ResetForNewSession();
        _camps.Received(1).OnGameLoaded();
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
