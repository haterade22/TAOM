using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using System.Reflection;
using TAOM.Core.Logging;
using TAOM.Features.CoopInterop;
using TAOM.Features.SupplyLines;
using TAOM.Features.SupplyLines.Domain;
using TAOM.Features.SupplyLines.Hooks;
using TaleWorlds.CampaignSystem;

namespace TAOM.Tests.Features.SupplyLines;

/// <summary>
/// The session reset contract on the behavior (round-A CRITICAL: process-singleton books leak
/// across campaigns because SyncData only fires when a saved record exists). The constructor
/// resets the singleton (#773), and SubModule builds the behavior in every campaign's OnGameStart
/// (pinned in <c>PerCampaignBehaviorWiringTests</c>): a session that loads a record installs it
/// over the reset, and one that never loads one (fresh campaign, or a save predating the
/// feature) keeps the empty book.
/// </summary>
[TestClass]
[TestCategory("RequiresGame")]
public class SupplyLinesCampaignBehaviorTests
{
    private ISupplyOrderService _orders;
    private ISupplyLinesSettingsProvider _settings;
    private ISupplyRouteVisualService _routeVisual;
    private IModLogger _logger;
    private ICoopSessionProvider _coop;
    private SupplyLinesCampaignBehavior _behavior;

    [TestInitialize]
    public void Setup()
    {
        _orders = Substitute.For<ISupplyOrderService>();
        _settings = Substitute.For<ISupplyLinesSettingsProvider>();
        _routeVisual = Substitute.For<ISupplyRouteVisualService>();
        _logger = Substitute.For<IModLogger>();
        _coop = Substitute.For<ICoopSessionProvider>();
        _coop.IsAuthority.Returns(true);
        _behavior = new SupplyLinesCampaignBehavior(_orders, _settings, _routeVisual, _coop, _logger);
    }

    [TestMethod]
    public void Constructor_FreshCampaign_ResetsTheService()
    {
        // Fresh campaign AND record-less save both start here: SubModule builds the behavior in
        // OnGameStart, before any campaign event and before a load's SyncData.
        _orders.Received(1).ResetForNewSession();
    }

    [TestMethod]
    public void LoadingSyncData_InstallsTheBookOverTheConstructorReset()
    {
        var dataStore = Substitute.For<IDataStore>();
        dataStore.IsLoading.Returns(true);

        _behavior.SyncData(dataStore);

        Received.InOrder(() =>
        {
            _orders.ResetForNewSession();
            _orders.LoadFrom(Arg.Any<Dictionary<string, SupplyOrder>>(), Arg.Any<int>());
        });
    }

    [TestMethod]
    public void SyncDataWhileSaving_SavesTheBookWithoutReinstallingOrResettingIt()
    {
        var dataStore = Substitute.For<IDataStore>();
        dataStore.IsLoading.Returns(false);

        _behavior.SyncData(dataStore);

        _orders.Received(1).SaveInto(out Arg.Any<Dictionary<string, SupplyOrder>>(), out Arg.Any<int>());
        _orders.DidNotReceiveWithAnyArgs().LoadFrom(default, default);
        _orders.Received(1).ResetForNewSession();
    }

    [TestMethod]
    public void GameLoadedWithoutRecord_RespawnsFromTheBookTheConstructorReset()
    {
        // OnGameLoaded fires BEFORE OnSessionLaunched. A record-less save (SyncData never ran) must
        // not respawn the previous session's caravans: the constructor already emptied the book.
        _behavior.HandleGameLoaded();

        Received.InOrder(() =>
        {
            _orders.ResetForNewSession();
            _orders.OnGameLoaded();
        });
    }

    [TestMethod]
    public void GameLoadedAfterLoadingSyncData_RespawnsWithoutASecondReset()
    {
        var dataStore = Substitute.For<IDataStore>();
        dataStore.IsLoading.Returns(true);
        _behavior.SyncData(dataStore);

        _behavior.HandleGameLoaded();

        _orders.Received(1).ResetForNewSession();
        _orders.Received(1).OnGameLoaded();
    }

    // --- The town menu option and the destroyed-caravan forward (the authority gates live in the service, #1086) ---

    /// <summary>A bare engine party carrying a supply caravan component, as the Refuge behavior tests build one.</summary>
    private static TaleWorlds.CampaignSystem.Party.MobileParty CaravanParty(string orderId)
    {
        var component = (TAOM.Features.SupplyLines.Components.SupplyCaravanComponent)
            System.Runtime.Serialization.FormatterServices.GetUninitializedObject(typeof(TAOM.Features.SupplyLines.Components.SupplyCaravanComponent));
        component.GetType().GetField("_orderId", BindingFlags.NonPublic | BindingFlags.Instance)!.SetValue(component, orderId);
        var party = (TaleWorlds.CampaignSystem.Party.MobileParty)
            System.Runtime.Serialization.FormatterServices.GetUninitializedObject(typeof(TaleWorlds.CampaignSystem.Party.MobileParty));
        typeof(TaleWorlds.CampaignSystem.Party.MobileParty)
            .GetField("_partyComponent", BindingFlags.NonPublic | BindingFlags.Instance)!.SetValue(party, component);
        return party;
    }

    [TestMethod]
    public void ADestroyedCaravan_IsForwardedWithItsOrderId()
    {
        typeof(SupplyLinesCampaignBehavior).GetMethod("OnMobilePartyDestroyed", BindingFlags.NonPublic | BindingFlags.Instance)!
            .Invoke(_behavior, new object[] { CaravanParty("order_1"), null! });

        _orders.Received(1).OnCaravanDestroyed("order_1");
    }

    [TestMethod]
    public void TownMenuOption_ForACoopGuest_IsShownDisabledWithItsReason()
    {
        // IsEnabled starts true, so only the guest branch can turn it off.
        _settings.Enabled.Returns(true);
        _coop.ShouldDeferToHost.Returns(true);
        var args = new TaleWorlds.CampaignSystem.GameMenus.MenuCallbackArgs((TaleWorlds.CampaignSystem.GameState.MenuContext)null!, null!);

        var shown = new SupplyOrderTownMenu(_settings, _coop).Condition(args);

        Assert.IsTrue(shown);
        Assert.IsFalse(args.IsEnabled);
        StringAssert.Contains(args.Tooltip.ToString(), "shared campaign");
    }

    [TestMethod]
    public void TownMenuOption_FeatureOff_IsHiddenForEveryone()
    {
        _settings.Enabled.Returns(false);
        _coop.ShouldDeferToHost.Returns(true);
        var args = new TaleWorlds.CampaignSystem.GameMenus.MenuCallbackArgs((TaleWorlds.CampaignSystem.GameState.MenuContext)null!, null!);

        Assert.IsFalse(new SupplyOrderTownMenu(_settings, _coop).Condition(args));
    }
}
