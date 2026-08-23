using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Core.Logging;
using TAOM.Features.SupplyLines;
using TAOM.Features.SupplyLines.Domain;
using TAOM.Features.SupplyLines.Hooks;
using TaleWorlds.CampaignSystem;

namespace TAOM.Tests.Features.SupplyLines;

/// <summary>
/// The session reset contract on the behavior (round-A CRITICAL: process-singleton books leak
/// across campaigns because SyncData only fires when a saved record exists). Both boundary paths
/// are pinned: a session that loaded a record installs it and does NOT reset; a session that
/// never loaded one (fresh campaign, or a save predating the feature) resets the singleton
/// before use. The internal seam <c>EnsureSessionInitialized</c> is what OnSessionLaunched runs
/// first (InternalsVisibleTo).
/// </summary>
[TestClass]
public class SupplyLinesCampaignBehaviorTests
{
    private ISupplyOrderService _orders;
    private ISupplyLinesSettingsProvider _settings;
    private ISupplyRouteVisualService _routeVisual;
    private IModLogger _logger;
    private SupplyLinesCampaignBehavior _behavior;

    [TestInitialize]
    public void Setup()
    {
        _orders = Substitute.For<ISupplyOrderService>();
        _settings = Substitute.For<ISupplyLinesSettingsProvider>();
        _routeVisual = Substitute.For<ISupplyRouteVisualService>();
        _logger = Substitute.For<IModLogger>();
        _behavior = new SupplyLinesCampaignBehavior(_orders, _settings, _routeVisual, _logger);
    }

    [TestMethod]
    public void SessionWithoutSyncData_ResetsTheService()
    {
        // Fresh campaign AND record-less save both look like this: SyncData never ran.
        _behavior.EnsureSessionInitialized();

        _orders.Received(1).ResetForNewSession();
    }

    [TestMethod]
    public void SessionAfterLoadingSyncData_InstallsBookAndDoesNotReset()
    {
        var dataStore = Substitute.For<IDataStore>();
        dataStore.IsLoading.Returns(true);

        _behavior.SyncData(dataStore);
        _behavior.EnsureSessionInitialized();

        _orders.Received(1).LoadFrom(Arg.Any<Dictionary<string, SupplyOrder>>(), Arg.Any<int>());
        _orders.DidNotReceive().ResetForNewSession();
    }

    [TestMethod]
    public void SyncDataWhileSaving_SavesTheBookWithoutReinstallingIt()
    {
        var dataStore = Substitute.For<IDataStore>();
        dataStore.IsLoading.Returns(false);

        _behavior.SyncData(dataStore);

        _orders.Received(1).SaveInto(out Arg.Any<Dictionary<string, SupplyOrder>>(), out Arg.Any<int>());
        _orders.DidNotReceiveWithAnyArgs().LoadFrom(default, default);
    }

    [TestMethod]
    public void SyncDataWhileSaving_DoesNotMarkTheSessionSynced()
    {
        // Only a LOAD proves the singleton holds this session's book. (In practice a save
        // cannot precede OnSessionLaunched; this pins the flag's polarity regardless.)
        var dataStore = Substitute.For<IDataStore>();
        dataStore.IsLoading.Returns(false);

        _behavior.SyncData(dataStore);
        _behavior.EnsureSessionInitialized();

        _orders.Received(1).ResetForNewSession();
    }

    [TestMethod]
    public void GameLoadedWithoutRecord_ResetsBeforeRespawning()
    {
        // OnGameLoaded fires BEFORE OnSessionLaunched. A record-less save (SyncData never ran)
        // still holds the previous session's book at that point; the reset must land before the
        // respawn pass or the dead session's caravans are spawned into this campaign.
        _behavior.HandleGameLoaded();

        Received.InOrder(() =>
        {
            _orders.ResetForNewSession();
            _orders.OnGameLoaded();
        });
    }

    [TestMethod]
    public void GameLoadedAfterLoadingSyncData_RespawnsWithoutReset()
    {
        var dataStore = Substitute.For<IDataStore>();
        dataStore.IsLoading.Returns(true);
        _behavior.SyncData(dataStore);

        _behavior.HandleGameLoaded();

        _orders.DidNotReceive().ResetForNewSession();
        _orders.Received(1).OnGameLoaded();
    }

    [TestMethod]
    public void EnsureSessionInitialized_RunsTheResetOnlyOnce()
    {
        // The reset empties the book; a stray second call after orders were placed must not
        // wipe them, so the first reset latches the session as initialized.
        _behavior.EnsureSessionInitialized();
        _behavior.EnsureSessionInitialized();

        _orders.Received(1).ResetForNewSession();
    }
}
