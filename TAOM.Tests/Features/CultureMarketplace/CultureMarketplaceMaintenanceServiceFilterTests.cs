using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Adapters;
using TAOM.Features.CultureMarketplace;
using TAOM.Features.CultureMarketplace.Domain;

namespace TAOM.Tests.Features.CultureMarketplace;

/// <summary>
/// Tests the cross-culture filter pass on CultureMarketplaceMaintenanceService via its
/// public API. Adapter and pool-service are fully mocked.
/// </summary>
[TestClass]
public class CultureMarketplaceMaintenanceServiceFilterTests
{
    private ICultureItemPoolService _poolService;
    private ITownRosterAdapter _townAdapter;

    [TestInitialize]
    public void Setup()
    {
        _poolService = Substitute.For<ICultureItemPoolService>();
        _townAdapter = Substitute.For<ITownRosterAdapter>();

        // Default: no routed items for any culture. Override in tests that need protection.
        _poolService.GetRoutedItemsForCulture(Arg.Any<string>()).Returns(System.Array.Empty<RoutedItem>());

        // Pass-through classifier: real CultureItemPoolService logic is exercised in
        // ClassifierTests; here we mock to keep the filter behavior under test.
        _poolService.ClassifyEffectiveCulture(Arg.Any<string>(), Arg.Any<string>())
            .Returns(ci => (string)ci[0]);   // attribute wins, no alias
    }

    private CultureMarketplaceMaintenanceService NewSut() => new(_poolService, _townAdapter);

    private void SetupRoster(params (string id, string culture, int count)[] rows)
    {
        var list = new List<RosterItemSnapshot>();
        foreach (var (id, c, n) in rows)
            list.Add(new RosterItemSnapshot(id, c, n));
        _townAdapter.EnumerateRoster(null).Returns(list);
    }

    [TestMethod]
    public void FilterForeignCultureItems_ForeignLotrlomeItem_Removed()
    {
        SetupRoster(("gondor_horse_armor", "gondor", 1));
        _townAdapter.RemoveItem(null, "gondor_horse_armor", 1).Returns(true);

        var removed = NewSut().FilterForeignCultureItems(null, "isengard", removalCap: 6);

        Assert.AreEqual(1, removed);
        _townAdapter.Received(1).RemoveItem(null, "gondor_horse_armor", 1);
    }

    [TestMethod]
    public void FilterForeignCultureItems_VanillaUniversal_Kept()
    {
        SetupRoster(("vanilla_horse", null, 1), ("vanilla_food", "", 3));

        var removed = NewSut().FilterForeignCultureItems(null, "isengard", removalCap: 6);

        Assert.AreEqual(0, removed);
        _townAdapter.DidNotReceive().RemoveItem(Arg.Any<TaleWorlds.CampaignSystem.Settlements.Settlement>(), Arg.Any<string>(), Arg.Any<int>());
    }

    [TestMethod]
    public void FilterForeignCultureItems_SameCultureItem_Kept()
    {
        SetupRoster(("isengard_sword", "isengard", 1));

        Assert.AreEqual(0, NewSut().FilterForeignCultureItems(null, "isengard", removalCap: 6));
    }

    [TestMethod]
    public void FilterForeignCultureItems_RoutedItemForThisCulture_Kept()
    {
        var routedHere = new List<RoutedItem>
        {
            new("warg_brown", new List<string> { "isengard", "mordor", "gundabad", "dolguldur" }, 1),
        };
        _poolService.GetRoutedItemsForCulture("mordor").Returns(routedHere);
        SetupRoster(("warg_brown", "isengard", 1));

        Assert.AreEqual(0, NewSut().FilterForeignCultureItems(null, "mordor", removalCap: 6));
        _townAdapter.DidNotReceive().RemoveItem(Arg.Any<TaleWorlds.CampaignSystem.Settlements.Settlement>(), Arg.Any<string>(), Arg.Any<int>());
    }

    [TestMethod]
    public void FilterForeignCultureItems_RemovalCapApplied()
    {
        var rows = new List<(string, string, int)>();
        for (var i = 0; i < 10; i++) rows.Add(($"gondor_item_{i}", "gondor", 1));
        SetupRoster(rows.ToArray());
        _townAdapter.RemoveItem(null, Arg.Any<string>(), Arg.Any<int>()).Returns(true);

        var removed = NewSut().FilterForeignCultureItems(null, "isengard", removalCap: 6);

        Assert.AreEqual(6, removed);
        _townAdapter.Received(6).RemoveItem(null, Arg.Any<string>(), Arg.Any<int>());
    }

    [TestMethod]
    public void FilterForeignCultureItems_ZeroCap_NoOp()
    {
        SetupRoster(("gondor_item", "gondor", 1));

        Assert.AreEqual(0, NewSut().FilterForeignCultureItems(null, "isengard", removalCap: 0));
        _townAdapter.DidNotReceive().EnumerateRoster(Arg.Any<TaleWorlds.CampaignSystem.Settlements.Settlement>());
    }

    [TestMethod]
    public void FilterForeignCultureItems_EmptyRoster_NoOp()
    {
        SetupRoster();   // empty

        Assert.AreEqual(0, NewSut().FilterForeignCultureItems(null, "isengard", removalCap: 6));
    }

    [TestMethod]
    public void FilterForeignCultureItems_MixedRoster_RemovesOnlyForeignCulturalItems()
    {
        SetupRoster(
            ("isengard_armor", "isengard", 1),      // same culture — kept
            ("gondor_armor",   "gondor",   1),      // foreign — removed
            ("vanilla_grain",  null,       10),     // universal — kept
            ("vlandia_horse",  "vlandia",  1)       // foreign — removed
        );
        _townAdapter.RemoveItem(null, "gondor_armor", 1).Returns(true);
        _townAdapter.RemoveItem(null, "vlandia_horse", 1).Returns(true);

        var removed = NewSut().FilterForeignCultureItems(null, "isengard", removalCap: 6);

        Assert.AreEqual(2, removed);
        _townAdapter.Received(1).RemoveItem(null, "gondor_armor", 1);
        _townAdapter.Received(1).RemoveItem(null, "vlandia_horse", 1);
        _townAdapter.DidNotReceive().RemoveItem(null, "isengard_armor", Arg.Any<int>());
        _townAdapter.DidNotReceive().RemoveItem(null, "vanilla_grain", Arg.Any<int>());
    }

    [TestMethod]
    public void FilterForeignCultureItems_RemoveItemReturnsFalse_DoesNotCountAsRemoved()
    {
        SetupRoster(("gondor_item", "gondor", 1));
        _townAdapter.RemoveItem(null, "gondor_item", 1).Returns(false);

        Assert.AreEqual(0, NewSut().FilterForeignCultureItems(null, "isengard", removalCap: 6));
    }

    [TestMethod]
    public void FilterForeignCultureItems_NullCultureId_ReturnsZero()
    {
        SetupRoster(("foreign_item", "gondor", 1));

        Assert.AreEqual(0, NewSut().FilterForeignCultureItems(null, null, 6));
        Assert.AreEqual(0, NewSut().FilterForeignCultureItems(null, "", 6));
        _townAdapter.DidNotReceive().EnumerateRoster(Arg.Any<TaleWorlds.CampaignSystem.Settlements.Settlement>());
    }
}
