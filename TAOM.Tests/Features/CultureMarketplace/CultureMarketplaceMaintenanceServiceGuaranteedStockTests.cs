using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Adapters;
using TAOM.Features.CultureMarketplace;
using TAOM.Features.CultureMarketplace.Domain;

namespace TAOM.Tests.Features.CultureMarketplace;

/// <summary>
/// Tests the guaranteed-stock pass on CultureMarketplaceMaintenanceService via its public
/// API. Adapter is fully mocked; Settlement is passed as null (the service never reads
/// off it — pure pass-through to the adapter per the interface XML doc).
/// </summary>
[TestClass]
public class CultureMarketplaceMaintenanceServiceGuaranteedStockTests
{
    private ICultureItemPoolService _poolService;
    private ITownRosterAdapter _townAdapter;

    [TestInitialize]
    public void Setup()
    {
        _poolService = Substitute.For<ICultureItemPoolService>();
        _townAdapter = Substitute.For<ITownRosterAdapter>();
    }

    private CultureMarketplaceMaintenanceService NewSut() =>
        new(_poolService, _townAdapter);

    private void SetupSingleWargRouted(string cultureId, int minStock)
    {
        var routed = new List<RoutedItem>
        {
            new("warg_brown", new List<string> { cultureId }, minStock),
        };
        _poolService.GetRoutedItemsForCulture(cultureId).Returns(routed);
    }

    [TestMethod]
    public void EnsureGuaranteedStock_TownHasZero_TopsUpToMinStock()
    {
        SetupSingleWargRouted("isengard", minStock: 1);
        _townAdapter.GetItemCount(null, "warg_brown").Returns(0);
        _townAdapter.AddItem(null, "warg_brown", 1).Returns(true);

        var added = NewSut().EnsureGuaranteedStock(null, "isengard");

        Assert.AreEqual(1, added);
        _townAdapter.Received(1).AddItem(null, "warg_brown", 1);
    }

    [TestMethod]
    public void EnsureGuaranteedStock_TownAlreadyHasFloor_NoOp()
    {
        SetupSingleWargRouted("isengard", minStock: 1);
        _townAdapter.GetItemCount(null, "warg_brown").Returns(1);

        var added = NewSut().EnsureGuaranteedStock(null, "isengard");

        Assert.AreEqual(0, added);
        _townAdapter.DidNotReceive().AddItem(Arg.Any<TaleWorlds.CampaignSystem.Settlements.Settlement>(), Arg.Any<string>(), Arg.Any<int>());
    }

    [TestMethod]
    public void EnsureGuaranteedStock_TownAboveFloor_NoOp()
    {
        SetupSingleWargRouted("isengard", minStock: 1);
        _townAdapter.GetItemCount(null, "warg_brown").Returns(5);

        Assert.AreEqual(0, NewSut().EnsureGuaranteedStock(null, "isengard"));
        _townAdapter.DidNotReceive().AddItem(Arg.Any<TaleWorlds.CampaignSystem.Settlements.Settlement>(), Arg.Any<string>(), Arg.Any<int>());
    }

    [TestMethod]
    public void EnsureGuaranteedStock_MinStockZero_Skipped()
    {
        SetupSingleWargRouted("isengard", minStock: 0);

        Assert.AreEqual(0, NewSut().EnsureGuaranteedStock(null, "isengard"));
        _townAdapter.DidNotReceive().GetItemCount(Arg.Any<TaleWorlds.CampaignSystem.Settlements.Settlement>(), Arg.Any<string>());
    }

    [TestMethod]
    public void EnsureGuaranteedStock_PartialStock_TopsUpDelta()
    {
        var routed = new List<RoutedItem>
        {
            new("warg_brown", new List<string> { "isengard" }, 3),
        };
        _poolService.GetRoutedItemsForCulture("isengard").Returns(routed);
        _townAdapter.GetItemCount(null, "warg_brown").Returns(1);
        _townAdapter.AddItem(null, "warg_brown", 2).Returns(true);

        var added = NewSut().EnsureGuaranteedStock(null, "isengard");

        Assert.AreEqual(2, added);
        _townAdapter.Received(1).AddItem(null, "warg_brown", 2);
    }

    [TestMethod]
    public void EnsureGuaranteedStock_MultipleRoutedItems_TopsUpEachIndependently()
    {
        var routed = new List<RoutedItem>
        {
            new("warg_brown",  new List<string> { "isengard" }, 1),
            new("warg_saddle", new List<string> { "isengard" }, 1),
            new("warg_dark",   new List<string> { "isengard" }, 1),
        };
        _poolService.GetRoutedItemsForCulture("isengard").Returns(routed);
        _townAdapter.GetItemCount(null, "warg_brown").Returns(0);
        _townAdapter.GetItemCount(null, "warg_saddle").Returns(1);
        _townAdapter.GetItemCount(null, "warg_dark").Returns(0);
        _townAdapter.AddItem(null, "warg_brown", 1).Returns(true);
        _townAdapter.AddItem(null, "warg_dark", 1).Returns(true);

        var added = NewSut().EnsureGuaranteedStock(null, "isengard");

        Assert.AreEqual(2, added);
        _townAdapter.Received(1).AddItem(null, "warg_brown", 1);
        _townAdapter.Received(1).AddItem(null, "warg_dark", 1);
        _townAdapter.DidNotReceive().AddItem(null, "warg_saddle", Arg.Any<int>());
    }

    [TestMethod]
    public void EnsureGuaranteedStock_NoRoutedItemsForCulture_NoOp()
    {
        _poolService.GetRoutedItemsForCulture("gondor").Returns(System.Array.Empty<RoutedItem>());

        Assert.AreEqual(0, NewSut().EnsureGuaranteedStock(null, "gondor"));
    }

    [TestMethod]
    public void EnsureGuaranteedStock_AddItemReturnsFalse_DoesNotCountAsAdded()
    {
        SetupSingleWargRouted("isengard", minStock: 1);
        _townAdapter.GetItemCount(null, "warg_brown").Returns(0);
        _townAdapter.AddItem(null, "warg_brown", 1).Returns(false);

        Assert.AreEqual(0, NewSut().EnsureGuaranteedStock(null, "isengard"));
    }

    [TestMethod]
    public void EnsureGuaranteedStock_NullCultureId_ReturnsZero()
    {
        Assert.AreEqual(0, NewSut().EnsureGuaranteedStock(null, null));
        Assert.AreEqual(0, NewSut().EnsureGuaranteedStock(null, ""));
        _poolService.DidNotReceive().GetRoutedItemsForCulture(Arg.Any<string>());
    }
}
