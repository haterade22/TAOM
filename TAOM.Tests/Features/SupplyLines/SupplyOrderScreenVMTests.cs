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
    public void SourceRow_NonFiniteDistance_SanitizedToZero()
    {
        _townA.Distance = float.NaN;

        var vm = CreateVM();

        Assert.AreEqual("0", vm.Settlements[0].DistanceText);
        Assert.AreEqual(0f, vm.Settlements[0].Distance);
    }
}
