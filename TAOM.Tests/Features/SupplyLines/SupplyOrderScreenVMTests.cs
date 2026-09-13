using System;
using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Features.SupplyLines;
using TAOM.Features.SupplyLines.Domain;
using TAOM.Features.SupplyLines.UI;

namespace TAOM.Tests.Features.SupplyLines;

/// <summary>
/// The order screen VM against mocked services only (review #26 lesson: the VM must be
/// constructible without IoC or campaign state). The pricing mock echoes its inputs into
/// distinguishable quote fields so every text line pins which argument reached the service.
/// </summary>
[TestClass]
public class SupplyOrderScreenVMTests
{
    private ISupplySourceService _sourceService = null!;
    private ISupplyPricingService _pricing = null!;
    private ISupplyOrderService _orders = null!;
    private ISupplyLinesSettingsProvider _settings = null!;
    private int _gold;
    private bool _closeCalled;

    private SupplySourceInfo _townA = null!;
    private SupplySourceInfo _townB = null!;
    private SupplySourceInfo _lordC = null!;

    private static SupplySourceInfo Town(string id, string name, float distance, bool canOrder = true, string reason = "")
        => new SupplySourceInfo
        {
            SettlementId = id,
            HeroId = null!,
            DisplayName = name,
            RelationText = "own",
            Distance = distance,
            CanOrder = canOrder,
            DisabledReason = reason,
        };

    private static SupplySourceInfo Lord(string id, string name, float distance)
        => new SupplySourceInfo
        {
            SettlementId = null!,
            HeroId = id,
            DisplayName = name,
            RelationText = "lord",
            Distance = distance,
            CanOrder = true,
            DisabledReason = "",
        };

    private static SupplyLineItem Line(string id, string name, int available, int unitPrice)
        => new SupplyLineItem { Id = id, Name = name, Available = available, UnitPrice = unitPrice };

    [TestInitialize]
    public void Setup()
    {
        _sourceService = Substitute.For<ISupplySourceService>();
        _pricing = Substitute.For<ISupplyPricingService>();
        _orders = Substitute.For<ISupplyOrderService>();
        _settings = Substitute.For<ISupplyLinesSettingsProvider>();
        _settings.Enabled.Returns(true);
        _gold = 1_000_000;
        _closeCalled = false;

        _townA = Town("town_a", "Bree", 10f);
        _townB = Town("town_b", "Dale", 20f);
        _lordC = Lord("lord_c", "Bard", 5f);
        _sourceService.GetSources().Returns(new List<SupplySourceInfo> { _townA, _townB, _lordC });

        _sourceService.GetGoods(_townA).Returns(new List<SupplyLineItem>
        {
            Line("grain", "Grain", 5, 10),
            Line("fish", "Fish", 3, 20),
        });
        _sourceService.GetTroops(_townA).Returns(new List<SupplyLineItem>
        {
            Line("militia", "Militia", 4, 50),
        });
        _sourceService.GetGoods(_townB).Returns(new List<SupplyLineItem> { Line("wine", "Wine", 2, 30) });
        _sourceService.GetTroops(_townB).Returns(new List<SupplyLineItem>());
        _sourceService.GetGoods(_lordC).Returns(new List<SupplyLineItem>());
        _sourceService.GetTroops(_lordC).Returns(new List<SupplyLineItem> { Line("guard", "Guardsman", 6, 40) });

        // Echo quote: Goods = goodsMarketValue, Troops = troopRecruitCost, Transport = distance,
        // Guard = 99 only for a mercenary escort. Each output line identifies one input.
        _pricing.Quote(Arg.Any<float>(), Arg.Any<int>(), Arg.Any<float>(), Arg.Any<SupplyEscortOption>())
            .Returns(ci => new SupplyQuote(
                (int)Math.Round(ci.ArgAt<float>(0)),
                ci.ArgAt<int>(1),
                (int)Math.Round(ci.ArgAt<float>(2)),
                ci.ArgAt<SupplyEscortOption>(3) == SupplyEscortOption.Mercenaries ? 99 : 0));
    }

    private SupplyOrderScreenVM CreateVM(bool placedFromCamp = false)
        => new SupplyOrderScreenVM(
            _sourceService, _pricing, _orders, _settings, () => _gold, () => _closeCalled = true, placedFromCamp);

    [TestMethod]
    public void ExecuteConfirm_FromCampScreen_MarksTheOrderCampPlaced()
    {
        // The camp menu opens the screen with the flag set; every order confirmed there rides
        // it into TryPlaceOrder so a later camp break cancels these orders and only these.
        _orders.TryPlaceOrder(
                Arg.Any<SupplySourceInfo>(),
                Arg.Any<IReadOnlyDictionary<string, int>>(),
                Arg.Any<IReadOnlyDictionary<string, int>>(),
                Arg.Any<SupplyEscortOption>(),
                out Arg.Any<string>(),
                Arg.Any<bool>())
            .Returns(new SupplyOrder());
        var vm = CreateVM(placedFromCamp: true);
        vm.Goods[0].ExecutePlus();

        vm.ExecuteConfirm();

        _orders.Received(1).TryPlaceOrder(
            Arg.Any<SupplySourceInfo>(),
            Arg.Any<IReadOnlyDictionary<string, int>>(),
            Arg.Any<IReadOnlyDictionary<string, int>>(),
            Arg.Any<SupplyEscortOption>(),
            out Arg.Any<string>(),
            true);
    }

    // --- population + auto-select ---

    [TestMethod]
    public void Ctor_ThreeSources_PopulatesAllRows()
    {
        var vm = CreateVM();
        Assert.AreEqual(3, vm.Settlements.Count);
    }

    [TestMethod]
    public void Ctor_FirstSourceOrderable_AutoSelectsItAndPopulates()
    {
        var vm = CreateVM();
        Assert.IsTrue(vm.Settlements[0].IsSelected);
        Assert.AreEqual(2, vm.Goods.Count);
        Assert.AreEqual(1, vm.Troops.Count);
    }

    [TestMethod]
    public void Ctor_FirstSourceNotOrderable_AutoSelectsFirstOrderable()
    {
        _townA.CanOrder = false;
        _townA.DisabledReason = "at war";

        var vm = CreateVM();

        Assert.IsFalse(vm.Settlements[0].IsSelected);
        Assert.IsTrue(vm.Settlements[1].IsSelected);
        Assert.AreEqual(1, vm.Goods.Count, "goods must come from the auto-selected town_b");
    }

    [TestMethod]
    public void Ctor_NoOrderableSources_NothingSelectedAndCannotConfirm()
    {
        _townA.CanOrder = false;
        _townB.CanOrder = false;
        _lordC.CanOrder = false;

        var vm = CreateVM();

        Assert.IsFalse(vm.Settlements[0].IsSelected);
        Assert.IsFalse(vm.Settlements[1].IsSelected);
        Assert.IsFalse(vm.Settlements[2].IsSelected);
        Assert.IsFalse(vm.CanConfirm);
        Assert.AreEqual(0, vm.Goods.Count);
    }

    // --- selection ---

    [TestMethod]
    public void ExecuteSelect_OtherSource_RepopulatesGoodsAndTroops()
    {
        var vm = CreateVM();

        vm.Settlements[2].ExecuteSelect();

        Assert.IsTrue(vm.Settlements[2].IsSelected);
        Assert.IsFalse(vm.Settlements[0].IsSelected);
        Assert.AreEqual(0, vm.Goods.Count, "lords sell no goods");
        Assert.AreEqual(1, vm.Troops.Count);
        Assert.AreEqual("guard", vm.Troops[0].ItemId);
    }

    [TestMethod]
    public void ExecuteSelect_LockedRow_DoesNotChangeSelection()
    {
        var vm = CreateVM();
        vm.Goods[0].ExecutePlus();

        vm.Settlements[1].ExecuteSelect();

        Assert.IsTrue(vm.Settlements[0].IsSelected);
        Assert.IsFalse(vm.Settlements[1].IsSelected);
    }

    // --- one-source lock ---

    [TestMethod]
    public void ExecutePlus_QtyAboveZero_LocksEveryOtherRow()
    {
        var vm = CreateVM();

        vm.Goods[0].ExecutePlus();

        Assert.IsTrue(vm.Settlements[0].RowEnabled, "the selected row stays interactive");
        Assert.IsFalse(vm.Settlements[1].RowEnabled);
        Assert.IsFalse(vm.Settlements[2].RowEnabled);
        Assert.IsTrue(vm.CanClear);
    }

    [TestMethod]
    public void ExecuteClear_ResetsQuantitiesAndUnlocksRows()
    {
        var vm = CreateVM();
        vm.Goods[0].ExecutePlus();
        vm.Troops[0].ExecutePlus();

        vm.ExecuteClear();

        Assert.AreEqual("0", vm.Goods[0].QtyText);
        Assert.AreEqual("0", vm.Troops[0].QtyText);
        Assert.IsTrue(vm.Settlements[1].RowEnabled);
        Assert.IsTrue(vm.Settlements[2].RowEnabled);
        Assert.IsFalse(vm.CanClear);
        Assert.IsFalse(vm.CanConfirm);
    }

    // --- row clamping ---

    [TestMethod]
    public void ExecutePlus_AtAvailable_ClampsAtStock()
    {
        var vm = CreateVM();
        var fish = vm.Goods[1]; // 3 available

        for (var i = 0; i < 5; i++)
            fish.ExecutePlus();

        Assert.AreEqual("3", fish.QtyText);
    }

    [TestMethod]
    public void ExecuteMinus_AtZero_StaysAtZero()
    {
        var vm = CreateVM();

        vm.Goods[0].ExecuteMinus();

        Assert.AreEqual("0", vm.Goods[0].QtyText);
    }

    // --- pricing lines ---

    [TestMethod]
    public void Recompute_TwoGrain_PriceLinesEchoQuoteFields()
    {
        var vm = CreateVM();

        vm.Goods[0].ExecutePlus();
        vm.Goods[0].ExecutePlus();

        // Echo mock: Goods = 2 x 10 market value, Transport = town_a distance (10), Guard = 0.
        StringAssert.EndsWith(vm.GoodsText, ": 20");
        StringAssert.EndsWith(vm.TroopText, ": 0");
        StringAssert.EndsWith(vm.TransportText, ": 10");
        StringAssert.EndsWith(vm.GuardText, ": 0");
        StringAssert.EndsWith(vm.TotalText, ": 30");
    }

    [TestMethod]
    public void Recompute_MercenaryEscort_QuoteReceivesMercenaries()
    {
        var vm = CreateVM();

        vm.ExecuteEscortMercenaries();
        vm.Goods[0].ExecutePlus();

        StringAssert.EndsWith(vm.GuardText, ": 99");
        Assert.IsFalse(vm.EscortNone);
        Assert.IsTrue(vm.EscortMercenaries);
    }

    [TestMethod]
    public void Recompute_LordSourceWithMercenaryEscort_QuoteForcedToNoEscort()
    {
        var vm = CreateVM();
        vm.ExecuteEscortMercenaries();

        vm.Settlements[2].ExecuteSelect();
        vm.Troops[0].ExecutePlus();

        // The escort toggle still reads Mercenaries, but a lord source is quoted with None so
        // the shown guard fee always matches what the order will charge.
        StringAssert.EndsWith(vm.GuardText, ": 0");
    }

    // --- CanConfirm matrix ---

    [TestMethod]
    public void CanConfirm_ZeroQuantity_False()
    {
        var vm = CreateVM();
        Assert.IsFalse(vm.CanConfirm);
    }

    [TestMethod]
    public void CanConfirm_QuantityAndAffordable_True()
    {
        var vm = CreateVM();
        vm.Goods[0].ExecutePlus();
        Assert.IsTrue(vm.CanConfirm);
    }

    [TestMethod]
    public void CanConfirm_Unaffordable_False()
    {
        _gold = 5; // total will be 10 (market) + 10 (transport)
        var vm = CreateVM();

        vm.Goods[0].ExecutePlus();

        Assert.IsFalse(vm.CanConfirm);
    }

    [TestMethod]
    public void CanConfirm_FeatureDisabledMidSession_False()
    {
        var vm = CreateVM();
        _settings.Enabled.Returns(false);

        vm.Goods[0].ExecutePlus();

        Assert.IsFalse(vm.CanConfirm);
    }

    // --- confirm ---

    [TestMethod]
    public void ExecuteConfirm_Success_PlacesFilteredOrderAndCloses()
    {
        _orders.TryPlaceOrder(
                Arg.Any<SupplySourceInfo>(),
                Arg.Any<IReadOnlyDictionary<string, int>>(),
                Arg.Any<IReadOnlyDictionary<string, int>>(),
                Arg.Any<SupplyEscortOption>(),
                out Arg.Any<string>())
            .Returns(new SupplyOrder());
        var vm = CreateVM();
        vm.Goods[0].ExecutePlus();
        vm.Goods[0].ExecutePlus();

        vm.ExecuteConfirm();

        _orders.Received(1).TryPlaceOrder(
            Arg.Is<SupplySourceInfo>(s => s.SettlementId == "town_a"),
            Arg.Is<IReadOnlyDictionary<string, int>>(g => g.Count == 1 && g["grain"] == 2),
            Arg.Is<IReadOnlyDictionary<string, int>>(t => t.Count == 0),
            SupplyEscortOption.None,
            out Arg.Any<string>());
        Assert.IsTrue(_closeCalled);
    }

    [TestMethod]
    public void ExecuteConfirm_ServiceRejects_ShowsReasonAndStaysOpen()
    {
        _orders.TryPlaceOrder(
                Arg.Any<SupplySourceInfo>(),
                Arg.Any<IReadOnlyDictionary<string, int>>(),
                Arg.Any<IReadOnlyDictionary<string, int>>(),
                Arg.Any<SupplyEscortOption>(),
                out Arg.Any<string>())
            .Returns(ci =>
            {
                ci[4] = "stock gone";
                return null;
            });
        var vm = CreateVM();
        vm.Goods[0].ExecutePlus();

        vm.ExecuteConfirm();

        Assert.IsFalse(_closeCalled);
        Assert.AreEqual("stock gone", vm.ErrorText);
        Assert.IsTrue(vm.HasError);
    }

    [TestMethod]
    public void ExecuteConfirm_NothingSelectedOrZeroQty_NeverCallsService()
    {
        var vm = CreateVM();

        vm.ExecuteConfirm();

        _orders.DidNotReceive().TryPlaceOrder(
            Arg.Any<SupplySourceInfo>(),
            Arg.Any<IReadOnlyDictionary<string, int>>(),
            Arg.Any<IReadOnlyDictionary<string, int>>(),
            Arg.Any<SupplyEscortOption>(),
            out Arg.Any<string>());
        Assert.IsFalse(_closeCalled);
    }

    [TestMethod]
    public void ExecuteConfirm_FailureThenQtyChange_ClearsError()
    {
        _orders.TryPlaceOrder(
                Arg.Any<SupplySourceInfo>(),
                Arg.Any<IReadOnlyDictionary<string, int>>(),
                Arg.Any<IReadOnlyDictionary<string, int>>(),
                Arg.Any<SupplyEscortOption>(),
                out Arg.Any<string>())
            .Returns(ci =>
            {
                ci[4] = "stock gone";
                return null;
            });
        var vm = CreateVM();
        vm.Goods[0].ExecutePlus();
        vm.ExecuteConfirm();
        Assert.IsTrue(vm.HasError);

        vm.Goods[0].ExecutePlus();

        Assert.IsFalse(vm.HasError);
        Assert.AreEqual(string.Empty, vm.ErrorText);
    }

    // --- cancel ---

    [TestMethod]
    public void ExecuteCancel_Always_Closes()
    {
        var vm = CreateVM();
        vm.ExecuteCancel();
        Assert.IsTrue(_closeCalled);
    }

    // --- source row composition ---

    [TestMethod]
    public void SourceRow_Disabled_ShowsReasonAndIsNotEnabled()
    {
        _townA.CanOrder = false;
        _townA.DisabledReason = "at war";

        var vm = CreateVM();

        Assert.IsFalse(vm.Settlements[0].RowEnabled);
        StringAssert.Contains(vm.Settlements[0].DisplayName, "at war");
    }

    [TestMethod]
    public void SourceRow_NonFiniteDistance_DisabledWithUnknownDistance()
    {
        // TryPlaceOrder rejects a NaN distance with no-route, so the row must not pretend the
        // source is 0 units away and orderable; the pricing-safe Distance stays 0.
        _townA.Distance = float.NaN;

        var vm = CreateVM();

        Assert.AreEqual("?", vm.Settlements[0].DistanceText);
        Assert.AreEqual(0f, vm.Settlements[0].Distance);
        Assert.IsFalse(vm.Settlements[0].RowEnabled);
    }

    [TestMethod]
    public void SourceRow_UnreachableSentinelDistance_DisabledWithNoRouteReason()
    {
        // float.MaxValue is the documented unreachable sentinel: the old row collapsed it to a
        // distance of 0 and quoted near-zero transport for an order the service would then
        // reject (review round B). The row now disables itself and says why.
        _townA.Distance = float.MaxValue;

        var vm = CreateVM();

        Assert.AreEqual("?", vm.Settlements[0].DistanceText);
        Assert.AreEqual(0f, vm.Settlements[0].Distance);
        Assert.IsFalse(vm.Settlements[0].RowEnabled);
        StringAssert.Contains(vm.Settlements[0].DisplayName, "no route");
    }

    [TestMethod]
    public void SourceRow_ReachableDistance_StaysOrderable()
    {
        var vm = CreateVM();

        Assert.AreEqual("10", vm.Settlements[0].DistanceText);
        Assert.IsTrue(vm.Settlements[0].RowEnabled);
    }

    // --- cross-market search (#587) ---

    private static SupplyLineItem DaleGrain() => Line("grain", "Grain", 9, 4);

    [TestMethod]
    public void SearchText_OneCharacter_InactiveAndNoHits()
    {
        var vm = CreateVM();

        vm.SearchText = "g";

        Assert.IsFalse(vm.IsSearchActive);
        Assert.AreEqual(0, vm.SearchHits.Count);
        Assert.AreEqual(string.Empty, vm.SearchStatusText);
    }

    [TestMethod]
    public void SearchText_Null_TreatedAsEmpty()
    {
        var vm = CreateVM();
        vm.SearchText = "gr";

        vm.SearchText = null!;

        Assert.AreEqual(string.Empty, vm.SearchText);
        Assert.IsFalse(vm.IsSearchActive);
        Assert.AreEqual(0, vm.SearchHits.Count);
    }

    [TestMethod]
    public void SearchText_TwoCharacters_HitsAcrossOrderableSourcesNearestFirst()
    {
        _sourceService.GetGoods(_townB).Returns(new List<SupplyLineItem> { DaleGrain() });
        var vm = CreateVM();

        vm.SearchText = "gr";

        Assert.IsTrue(vm.IsSearchActive);
        Assert.AreEqual(2, vm.SearchHits.Count);
        Assert.AreSame(vm.Settlements[0], vm.SearchHits[0].SourceRow, "Bree at 10 is nearer than Dale at 20");
        Assert.AreSame(vm.Settlements[1], vm.SearchHits[1].SourceRow);
        Assert.AreEqual("grain", vm.SearchHits[0].Item.Id);
    }

    [TestMethod]
    public void SearchText_BelowMinimum_NeverBuildsTheCatalogue()
    {
        var vm = CreateVM();

        vm.SearchText = "g";

        _sourceService.DidNotReceive().GetGoods(_townB);
    }

    [TestMethod]
    public void SearchText_LordRow_NeverCatalogued()
    {
        var vm = CreateVM();

        vm.SearchText = "gu";

        _sourceService.DidNotReceive().GetGoods(_lordC);
        Assert.AreEqual(0, vm.SearchHits.Count, "a lord's troops are not goods");
    }

    [TestMethod]
    public void SearchText_AtWarRow_NeverCatalogued()
    {
        _townB.CanOrder = false;
        _townB.DisabledReason = "at war";
        _sourceService.GetGoods(_townB).Returns(new List<SupplyLineItem> { DaleGrain() });
        var vm = CreateVM();

        vm.SearchText = "gr";

        _sourceService.DidNotReceive().GetGoods(_townB);
        Assert.AreEqual(1, vm.SearchHits.Count);
    }

    [TestMethod]
    public void SearchText_UnreachableRow_NeverCatalogued()
    {
        _townB.Distance = float.MaxValue;
        _sourceService.GetGoods(_townB).Returns(new List<SupplyLineItem> { DaleGrain() });
        var vm = CreateVM();

        vm.SearchText = "gr";

        _sourceService.DidNotReceive().GetGoods(_townB);
        Assert.AreEqual(1, vm.SearchHits.Count);
    }

    [TestMethod]
    public void SearchText_NaNDistanceRow_NeverCatalogued()
    {
        _townB.Distance = float.NaN;
        _sourceService.GetGoods(_townB).Returns(new List<SupplyLineItem> { DaleGrain() });
        var vm = CreateVM();

        vm.SearchText = "gr";

        _sourceService.DidNotReceive().GetGoods(_townB);
    }

    [TestMethod]
    public void SearchText_SecondKeystroke_CatalogueBuiltOnce()
    {
        // town_b is never selected, so its only GetGoods call is the catalogue build; two
        // qualifying keystrokes must not scan the map twice.
        var vm = CreateVM();

        vm.SearchText = "gr";
        vm.SearchText = "gra";

        _sourceService.Received(1).GetGoods(_townB);
    }

    [TestMethod]
    public void SearchText_NullGoodsFromService_NoHitsNoThrow()
    {
        _sourceService.GetGoods(_townB).Returns((IReadOnlyList<SupplyLineItem>)null!);
        var vm = CreateVM();

        vm.SearchText = "wi";

        Assert.AreEqual(0, vm.SearchHits.Count);
    }

    [TestMethod]
    public void SearchText_Set_NotifiesTheRawValue()
    {
        // The widget desynchronises its visible and real text if the VM echoes a normalised value.
        var vm = CreateVM();
        object? notified = null;
        vm.PropertyChangedWithValue += (_, e) =>
        {
            if (e.PropertyName == nameof(SupplyOrderScreenVM.SearchText))
                notified = e.Value;
        };

        vm.SearchText = "  Gr";

        Assert.AreEqual("  Gr", notified);
        Assert.AreEqual("  Gr", vm.SearchText);
        Assert.IsTrue(vm.IsSearchActive, "trimmed length 2 qualifies");
    }

    [TestMethod]
    public void SearchText_TypedAfterConfirmFailure_ErrorTextPreserved()
    {
        // Typing in the search box must not run Recompute, which would erase the failure line
        // the player is reading and re-quote the order per keystroke.
        _orders.TryPlaceOrder(
                Arg.Any<SupplySourceInfo>(), Arg.Any<IReadOnlyDictionary<string, int>>(),
                Arg.Any<IReadOnlyDictionary<string, int>>(), Arg.Any<SupplyEscortOption>(),
                out Arg.Any<string>(), Arg.Any<bool>())
            .Returns(ci => { ci[4] = "no route"; return null; });
        var vm = CreateVM();
        vm.Goods[0].ExecutePlus();
        vm.ExecuteConfirm();
        Assert.AreEqual("no route", vm.ErrorText);

        vm.SearchText = "gr";

        Assert.AreEqual("no route", vm.ErrorText);
    }

    [TestMethod]
    public void SearchHit_Row_ShowsItemSourceStockPriceAndDistance()
    {
        var vm = CreateVM();

        vm.SearchText = "fi";

        var hit = vm.SearchHits[0];
        StringAssert.Contains(hit.Label, "Fish");
        StringAssert.Contains(hit.Label, "Bree");
        StringAssert.Contains(hit.DetailText, "3");
        StringAssert.Contains(hit.DetailText, "20");
        StringAssert.Contains(hit.DetailText, vm.Settlements[0].DistanceText);
        Assert.IsTrue(hit.RowEnabled);
        Assert.IsFalse(hit.IsSelected);
    }

    [TestMethod]
    public void SearchHitSelect_SelectsSourceAndPromotesTheGood()
    {
        // Dale prices wine above grain, so GetGoods lists wine first; picking the grain hit
        // must still land grain at Goods[0] so the player sees what they searched for.
        _sourceService.GetGoods(_townB).Returns(new List<SupplyLineItem>
        {
            Line("wine", "Wine", 2, 30),
            DaleGrain(),
        });
        var vm = CreateVM();
        vm.SearchText = "gr";
        var daleHit = vm.SearchHits[1];

        daleHit.ExecuteSelect();

        Assert.IsTrue(vm.Settlements[1].IsSelected);
        Assert.IsFalse(vm.Settlements[0].IsSelected);
        Assert.AreEqual(2, vm.Goods.Count);
        Assert.AreEqual("grain", vm.Goods[0].ItemId);
        Assert.AreEqual("wine", vm.Goods[1].ItemId);
        Assert.IsTrue(daleHit.IsSelected);
        Assert.IsFalse(vm.SearchHits[0].IsSelected);
        Assert.IsTrue(vm.IsSearchActive, "the result list stays up after a pick");
        Assert.AreEqual("gr", vm.SearchText);
    }

    [TestMethod]
    public void SearchHitSelect_RepopulatesTroopsAndCallsGetGoodsOnce()
    {
        _sourceService.GetGoods(_townB).Returns(new List<SupplyLineItem> { DaleGrain() });
        _sourceService.GetTroops(_townB).Returns(new List<SupplyLineItem> { Line("dale_spear", "Dale Spearman", 2, 60) });
        var vm = CreateVM();
        vm.SearchText = "gr";
        _sourceService.ClearReceivedCalls();

        vm.SearchHits[1].ExecuteSelect();

        Assert.AreEqual("dale_spear", vm.Troops[0].ItemId);
        _sourceService.Received(1).GetGoods(_townB);
    }

    [TestMethod]
    public void SearchHitSelect_ResetsTheGoodsScroll()
    {
        // The goods panel keeps its scroll offset across a repopulate, so a promoted Goods[0]
        // could sit above the viewport and the click would look dead.
        _sourceService.GetGoods(_townB).Returns(new List<SupplyLineItem> { DaleGrain() });
        var vm = CreateVM();
        vm.SearchText = "gr";
        vm.GoodsScrollValue = 0.7f; // the widget writes the player's scroll back

        vm.SearchHits[1].ExecuteSelect();

        Assert.AreEqual(0f, vm.GoodsScrollValue);
    }

    [TestMethod]
    public void SourceRowSelect_AlsoResetsTheGoodsScroll()
    {
        var vm = CreateVM();
        vm.GoodsScrollValue = 0.7f;

        vm.Settlements[1].ExecuteSelect();

        Assert.AreEqual(0f, vm.GoodsScrollValue);
    }

    [TestMethod]
    public void SearchHitSelect_ThenQuantity_LocksEveryHit()
    {
        // A hit from the SELECTED source would repopulate the goods list and wipe the pending
        // quantities, so a pending order pins every hit, not only the other sources' ones.
        _sourceService.GetGoods(_townB).Returns(new List<SupplyLineItem> { DaleGrain(), Line("grapes", "Grapes", 1, 9) });
        var vm = CreateVM();
        vm.SearchText = "gra";
        vm.SearchHits[1].ExecuteSelect();

        vm.Goods[0].ExecutePlus();

        Assert.IsFalse(vm.SearchHits[0].RowEnabled, "Bree's hit");
        Assert.IsFalse(vm.SearchHits[1].RowEnabled, "the picked Dale hit");
        Assert.IsFalse(vm.SearchHits[2].RowEnabled, "Dale's other hit");
        Assert.IsTrue(vm.SearchHits[1].IsSelected, "still highlighted while locked");
        Assert.IsFalse(vm.Settlements[0].RowEnabled);
    }

    [TestMethod]
    public void SearchHitSelect_LockedHit_DoesNotChangeSelectionOrWipeQty()
    {
        _sourceService.GetGoods(_townB).Returns(new List<SupplyLineItem> { DaleGrain() });
        var vm = CreateVM();
        vm.SearchText = "gr";
        vm.Goods[0].ExecutePlus(); // Bree (auto-selected) has a pending quantity

        vm.SearchHits[1].ExecuteSelect();

        Assert.IsTrue(vm.Settlements[0].IsSelected);
        Assert.IsFalse(vm.Settlements[1].IsSelected);
        Assert.IsFalse(vm.SearchHits[1].IsSelected);
        Assert.AreEqual(1, vm.Goods[0].Qty);
    }

    [TestMethod]
    public void SearchText_RetypedWhilePending_NewHitsStartLocked()
    {
        _sourceService.GetGoods(_townB).Returns(new List<SupplyLineItem> { DaleGrain() });
        var vm = CreateVM();
        vm.Goods[0].ExecutePlus();

        vm.SearchText = "gr";

        Assert.IsFalse(vm.SearchHits[0].RowEnabled);
        Assert.IsFalse(vm.SearchHits[1].RowEnabled);
    }

    [TestMethod]
    public void ExecuteClear_UnlocksHitRowsToo()
    {
        _sourceService.GetGoods(_townB).Returns(new List<SupplyLineItem> { DaleGrain() });
        var vm = CreateVM();
        vm.SearchText = "gr";
        vm.Goods[0].ExecutePlus();

        vm.ExecuteClear();

        Assert.IsTrue(vm.SearchHits[0].RowEnabled);
        Assert.IsTrue(vm.SearchHits[1].RowEnabled);
    }

    [TestMethod]
    public void SearchText_RetypedAfterPick_KeepsSelectionAndReflectsPickedHit()
    {
        _sourceService.GetGoods(_townB).Returns(new List<SupplyLineItem> { DaleGrain() });
        var vm = CreateVM();
        vm.SearchText = "gr";
        vm.SearchHits[1].ExecuteSelect();

        vm.SearchText = "gra";

        Assert.IsTrue(vm.Settlements[1].IsSelected);
        Assert.IsTrue(vm.SearchHits[1].IsSelected, "the rebuilt hit for the picked good stays highlighted");
        Assert.IsFalse(vm.SearchHits[0].IsSelected);
    }

    [TestMethod]
    public void SourceRowSelect_AfterHitPick_NextSearchHasNoHighlightedHit()
    {
        _sourceService.GetGoods(_townB).Returns(new List<SupplyLineItem> { DaleGrain() });
        var vm = CreateVM();
        vm.SearchText = "gr";
        vm.SearchHits[1].ExecuteSelect();

        vm.Settlements[0].ExecuteSelect();
        vm.SearchText = "gra";

        Assert.IsTrue(vm.Settlements[0].IsSelected);
        Assert.IsFalse(vm.SearchHits[0].IsSelected, "a plain settlement pick forgets the picked good");
        Assert.IsFalse(vm.SearchHits[1].IsSelected);
    }

    [TestMethod]
    public void ExecuteConfirm_AfterHitPick_OrdersFromTheHitSource()
    {
        _sourceService.GetGoods(_townB).Returns(new List<SupplyLineItem> { DaleGrain() });
        _orders.TryPlaceOrder(
                Arg.Any<SupplySourceInfo>(), Arg.Any<IReadOnlyDictionary<string, int>>(),
                Arg.Any<IReadOnlyDictionary<string, int>>(), Arg.Any<SupplyEscortOption>(),
                out Arg.Any<string>(), Arg.Any<bool>())
            .Returns(new SupplyOrder());
        var vm = CreateVM();
        vm.SearchText = "gr";
        vm.SearchHits[1].ExecuteSelect();
        vm.Goods[0].ExecutePlus();

        vm.ExecuteConfirm();

        _orders.Received(1).TryPlaceOrder(
            _townB,
            Arg.Is<IReadOnlyDictionary<string, int>>(g => g.Count == 1 && g["grain"] == 1),
            Arg.Any<IReadOnlyDictionary<string, int>>(),
            Arg.Any<SupplyEscortOption>(),
            out Arg.Any<string>(),
            Arg.Any<bool>());
        Assert.IsTrue(_closeCalled);
    }

    [TestMethod]
    public void ExecuteClearSearch_EmptiesHitsAndKeepsTheSelection()
    {
        _sourceService.GetGoods(_townB).Returns(new List<SupplyLineItem> { DaleGrain() });
        var vm = CreateVM();
        vm.SearchText = "gr";
        vm.SearchHits[1].ExecuteSelect();
        vm.Goods[0].ExecutePlus();

        vm.ExecuteClearSearch();

        Assert.AreEqual(string.Empty, vm.SearchText);
        Assert.IsFalse(vm.IsSearchActive);
        Assert.AreEqual(0, vm.SearchHits.Count);
        Assert.AreEqual(string.Empty, vm.SearchStatusText);
        Assert.IsTrue(vm.Settlements[1].IsSelected);
        Assert.AreEqual("grain", vm.Goods[0].ItemId);
        Assert.AreEqual(1, vm.Goods[0].Qty, "clearing the search is not clearing the order");
    }

    [TestMethod]
    public void SearchStatusText_NoMatch_SaysSo()
    {
        var vm = CreateVM();

        vm.SearchText = "zz";

        Assert.IsTrue(vm.IsSearchActive);
        Assert.AreEqual(0, vm.SearchHits.Count);
        StringAssert.Contains(vm.SearchStatusText, "No ");
    }

    [TestMethod]
    public void SearchStatusText_Matches_ShowsTheCount()
    {
        _sourceService.GetGoods(_townB).Returns(new List<SupplyLineItem> { DaleGrain() });
        var vm = CreateVM();

        vm.SearchText = "gr";

        StringAssert.Contains(vm.SearchStatusText, "2");
    }

    [TestMethod]
    public void SearchStatusText_Capped_ShowsShownOfTotal()
    {
        int total = SupplyGoodsSearch.MaxHits + 10;
        var sources = new List<SupplySourceInfo>();
        for (int i = 0; i < total; i++)
        {
            var town = Town($"town_{i}", $"Town {i}", i + 1);
            _sourceService.GetGoods(town).Returns(new List<SupplyLineItem> { DaleGrain() });
            sources.Add(town);
        }
        _sourceService.GetSources().Returns(sources);
        var vm = CreateVM();

        vm.SearchText = "gr";

        Assert.AreEqual(SupplyGoodsSearch.MaxHits, vm.SearchHits.Count);
        StringAssert.Contains(vm.SearchStatusText, SupplyGoodsSearch.MaxHits.ToString());
        StringAssert.Contains(vm.SearchStatusText, total.ToString());
    }

    [TestMethod]
    public void SearchPlaceholderText_Ctor_IsSet()
    {
        var vm = CreateVM();
        Assert.IsFalse(string.IsNullOrEmpty(vm.SearchPlaceholderText));
    }

    [TestMethod]
    public void SearchHitSelect_PreferredGoodAbsentFromFreshList_NoPromotionNoThrow()
    {
        // The catalogue and the pick both read a frozen roster, so this cannot happen in game;
        // the accepted degradation if it ever did is "no promoted row", never a crash.
        _sourceService.GetGoods(_townB).Returns(
            new List<SupplyLineItem> { DaleGrain() },                  // catalogue build
            new List<SupplyLineItem> { Line("wine", "Wine", 2, 30) }); // the pick's fresh read
        var vm = CreateVM();
        vm.SearchText = "gr";

        vm.SearchHits[1].ExecuteSelect();

        Assert.IsTrue(vm.Settlements[1].IsSelected);
        Assert.AreEqual(1, vm.Goods.Count);
        Assert.AreEqual("wine", vm.Goods[0].ItemId);
    }

    [TestMethod]
    public void SourceRowSelect_AfterHitPick_ClearsTheHitHighlightWithoutRetyping()
    {
        _sourceService.GetGoods(_townB).Returns(new List<SupplyLineItem> { DaleGrain() });
        var vm = CreateVM();
        vm.SearchText = "gr";
        vm.SearchHits[1].ExecuteSelect();
        Assert.IsTrue(vm.SearchHits[1].IsSelected);

        vm.Settlements[0].ExecuteSelect();

        Assert.IsFalse(vm.SearchHits[1].IsSelected, "the hit list is still up; its highlight must follow the selection");
        Assert.IsFalse(vm.SearchHits[0].IsSelected, "a plain settlement pick promotes nothing");
    }

    [TestMethod]
    public void PopulateGoods_EveryRepopulate_InvokesTheWidgetScrollResetBeforeTheValueReset()
    {
        // A wheel notch leaves the goods pane coasting and the value reset alone cannot stop it
        // (Codex review #587 P2); the screen hands the VM the pane's own ResetTweenSpeed.
        _sourceService.GetGoods(_townB).Returns(new List<SupplyLineItem> { DaleGrain() });
        var vm = CreateVM();
        var calls = new List<string>();
        vm.ResetGoodsScroll = () => calls.Add("widget");
        vm.PropertyChangedWithFloatValue += (_, e) =>
        {
            if (e.PropertyName == nameof(SupplyOrderScreenVM.GoodsScrollValue))
                calls.Add("value");
        };
        vm.GoodsScrollValue = 0.5f;
        calls.Clear();

        vm.Settlements[1].ExecuteSelect();

        CollectionAssert.AreEqual(new[] { "widget", "value" }, calls);
    }

    [TestMethod]
    public void PopulateGoods_NoWidgetHook_StillResetsTheValue()
    {
        var vm = CreateVM();
        vm.GoodsScrollValue = 0.5f;

        vm.Settlements[1].ExecuteSelect();

        Assert.AreEqual(0f, vm.GoodsScrollValue);
    }
}
