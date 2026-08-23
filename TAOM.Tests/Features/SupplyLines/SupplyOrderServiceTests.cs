using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Core.Logging;
using TAOM.Features.SupplyLines;
using TAOM.Features.SupplyLines.Domain;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Localization;

namespace TAOM.Tests.Features.SupplyLines;

/// <summary>
/// Decision-path coverage for <see cref="SupplyOrderService"/>. Campaign statics (gold, encounter
/// state, rosters, message toasts, refund plumbing) are overridden on a test subclass; those
/// virtual bodies are the honest untested boundary sliver, exercised only in game.
/// </summary>
[TestClass]
public class SupplyOrderServiceTests
{
    private sealed class TestableSupplyOrderService : SupplyOrderService
    {
        public int Gold = 1_000_000;
        public bool Blockaded;
        public bool InEncounter;
        public bool Captive;
        public string CompanionId;
        public float ElapsedFraction = 0.5f;
        public double NowHours;

        public readonly List<string> CallSequence = new List<string>();
        public readonly List<int> Charges = new List<int>();
        public readonly List<SupplyOrder> Delivered = new List<SupplyOrder>();
        public IReadOnlyDictionary<string, int> LastDeliveredGoods;
        public IReadOnlyDictionary<string, int> LastDeliveredRecruits;
        public readonly List<SupplyConsumption> Refunds = new List<SupplyConsumption>();

        public TestableSupplyOrderService(
            ISupplySourceService sources,
            ISupplyCaravanService caravans,
            ISupplyOrderEngine engine,
            ISupplyPricingService pricing,
            ISupplyLinesSettingsProvider settings,
            IModLogger logger)
            : base(sources, caravans, engine, pricing, settings, logger)
        {
        }

        protected override int PlayerGold => Gold;

        protected override string Localize(TextObject text) => "localized";

        protected override CampaignTime CampaignTimeNow() => default;

        protected override double FrameHoursNow() => NowHours;

        protected override float ElapsedFractionOf(SupplyOrder order) => ElapsedFraction;

        protected override void ChargePlayer(int amount)
        {
            CallSequence.Add("charge");
            Charges.Add(amount);
        }

        protected override string PickCompanionEscortId() => CompanionId;

        protected override bool IsPlayerBlockaded() => Blockaded;

        protected override bool IsPlayerInEncounter() => InEncounter;

        protected override bool IsPlayerCaptive() => Captive;

        protected override void DeliverCargoToPlayer(
            SupplyOrder order,
            IReadOnlyDictionary<string, int> goods,
            IReadOnlyDictionary<string, int> recruits)
        {
            CallSequence.Add("deliver");
            Delivered.Add(order);
            LastDeliveredGoods = goods;
            LastDeliveredRecruits = recruits;
        }

        protected override void RefundConsumption(SupplySourceInfo source, SupplyConsumption consumption)
        {
            CallSequence.Add("refund");
            Refunds.Add(consumption);
        }

        protected override void ShowMessage(TextObject text, bool error)
        {
        }
    }

    private ISupplySourceService _sources;
    private ISupplyCaravanService _caravans;
    private ISupplyOrderEngine _engine;
    private ISupplyPricingService _pricing;
    private ISupplyLinesSettingsProvider _settings;
    private IModLogger _logger;
    private TestableSupplyOrderService _sut;

    private SupplySourceInfo _townSource;

    [TestInitialize]
    public void Setup()
    {
        _sources = Substitute.For<ISupplySourceService>();
        _caravans = Substitute.For<ISupplyCaravanService>();
        _engine = Substitute.For<ISupplyOrderEngine>();
        _pricing = Substitute.For<ISupplyPricingService>();
        _settings = Substitute.For<ISupplyLinesSettingsProvider>();
        _logger = Substitute.For<IModLogger>();
        _sut = new TestableSupplyOrderService(_sources, _caravans, _engine, _pricing, _settings, _logger);

        _settings.Enabled.Returns(true);
        _townSource = new SupplySourceInfo { SettlementId = "town_G1", CanOrder = true };
        _sources.DistanceToPlayer(Arg.Any<SupplySourceInfo>()).Returns(10f);
        _pricing.Quote(Arg.Any<float>(), Arg.Any<int>(), Arg.Any<float>(), Arg.Any<SupplyEscortOption>())
            .Returns(new SupplyQuote(goods: 100, troops: 50, transport: 20, guard: 0));
        _pricing.PlannedHours(Arg.Any<float>()).Returns(20f);
    }

    private SupplyConsumption ArrangeConsumption(int grain = 5, int recruits = 2)
    {
        var consumption = new SupplyConsumption { GoodsMarketValue = 100f, TroopRecruitCost = 50 };
        if (grain > 0)
            consumption.Goods["grain"] = grain;
        if (recruits > 0)
            consumption.Troops["troop_a"] = recruits;
        _sources
            .Consume(Arg.Any<SupplySourceInfo>(), Arg.Any<IReadOnlyDictionary<string, int>>(),
                Arg.Any<IReadOnlyDictionary<string, int>>())
            .Returns(x =>
            {
                _sut.CallSequence.Add("consume");
                return consumption;
            });
        return consumption;
    }

    private void ArrangeSpawn(string partyId = "party_1")
    {
        _caravans.Spawn(Arg.Any<SupplyOrder>()).Returns(x =>
        {
            _sut.CallSequence.Add("spawn");
            return partyId;
        });
    }

    private SupplyOrder SeedInTransitOrder(string id = "taom_so_0")
    {
        var order = new SupplyOrder { OrderId = id, SourceSettlementId = "town_G1" };
        order.StatusEnum = SupplyOrderStatus.InTransit;
        _sut.LoadFrom(new Dictionary<string, SupplyOrder> { [id] = order }, counter: 1);
        return order;
    }

    // --- TryPlaceOrder ---

    [TestMethod]
    public void TryPlaceOrder_FeatureDisabled_FailsWithoutConsuming()
    {
        _settings.Enabled.Returns(false);

        var result = _sut.TryPlaceOrder(_townSource, null, null, SupplyEscortOption.None, out var reason);

        Assert.IsNull(result);
        Assert.IsNotNull(reason);
        _sources.DidNotReceiveWithAnyArgs().Consume(default, default, default);
    }

    [TestMethod]
    public void TryPlaceOrder_PlayerBlockaded_FailsWithoutConsuming()
    {
        _sut.Blockaded = true;

        var result = _sut.TryPlaceOrder(_townSource, null, null, SupplyEscortOption.None, out var reason);

        Assert.IsNull(result);
        Assert.IsNotNull(reason);
        _sources.DidNotReceiveWithAnyArgs().Consume(default, default, default);
    }

    [TestMethod]
    public void TryPlaceOrder_NaNDistance_FailsWithoutConsuming()
    {
        _sources.DistanceToPlayer(Arg.Any<SupplySourceInfo>()).Returns(float.NaN);

        var result = _sut.TryPlaceOrder(_townSource, null, null, SupplyEscortOption.None, out var reason);

        Assert.IsNull(result);
        Assert.IsNotNull(reason);
        _sources.DidNotReceiveWithAnyArgs().Consume(default, default, default);
    }

    [TestMethod]
    public void TryPlaceOrder_UnreachableDistance_FailsWithoutConsuming()
    {
        _sources.DistanceToPlayer(Arg.Any<SupplySourceInfo>()).Returns(float.MaxValue);

        var result = _sut.TryPlaceOrder(_townSource, null, null, SupplyEscortOption.None, out var reason);

        Assert.IsNull(result);
        Assert.IsNotNull(reason);
        _sources.DidNotReceiveWithAnyArgs().Consume(default, default, default);
    }

    [TestMethod]
    public void TryPlaceOrder_NothingObtained_FailsWithoutRefundOrCharge()
    {
        _sources
            .Consume(Arg.Any<SupplySourceInfo>(), Arg.Any<IReadOnlyDictionary<string, int>>(),
                Arg.Any<IReadOnlyDictionary<string, int>>())
            .Returns(new SupplyConsumption());

        var result = _sut.TryPlaceOrder(_townSource, null, null, SupplyEscortOption.None, out var reason);

        Assert.IsNull(result);
        Assert.IsNotNull(reason);
        Assert.AreEqual(0, _sut.Refunds.Count, "nothing was taken, so nothing must be refunded");
        Assert.AreEqual(0, _sut.Charges.Count);
    }

    [TestMethod]
    public void TryPlaceOrder_UnaffordableTotal_RefundsAndDoesNotCharge()
    {
        var consumption = ArrangeConsumption();
        _sut.Gold = 10; // quote total is 170

        var result = _sut.TryPlaceOrder(_townSource, null, null, SupplyEscortOption.None, out var reason);

        Assert.IsNull(result);
        Assert.IsNotNull(reason);
        Assert.AreSame(consumption, _sut.Refunds.Single());
        Assert.AreEqual(0, _sut.Charges.Count);
        _caravans.DidNotReceiveWithAnyArgs().Spawn(default);
    }

    [TestMethod]
    public void TryPlaceOrder_SpawnFails_RefundsAndDoesNotCharge()
    {
        var consumption = ArrangeConsumption();
        _caravans.Spawn(Arg.Any<SupplyOrder>()).Returns((string)null);

        var result = _sut.TryPlaceOrder(_townSource, null, null, SupplyEscortOption.None, out var reason);

        Assert.IsNull(result);
        Assert.IsNotNull(reason);
        Assert.AreSame(consumption, _sut.Refunds.Single());
        Assert.AreEqual(0, _sut.Charges.Count);
        Assert.AreEqual(0, _sut.ActiveOrders.Count, "a failed order must not enter the book");
    }

    [TestMethod]
    public void TryPlaceOrder_Success_ChargesOnlyAfterConsumeAndSpawn()
    {
        ArrangeConsumption();
        ArrangeSpawn();

        var result = _sut.TryPlaceOrder(_townSource, null, null, SupplyEscortOption.None, out var reason);

        Assert.IsNotNull(result);
        Assert.IsNull(reason);
        CollectionAssert.AreEqual(
            new[] { "consume", "spawn", "charge" }, _sut.CallSequence,
            "the charge must land only after the caravan exists (the source module charged first)");
        Assert.AreEqual(170, _sut.Charges.Single());
        Assert.AreEqual(170, result.TotalPaid);
        Assert.AreEqual(1, _sut.ActiveOrders.Count);
    }

    [TestMethod]
    public void TryPlaceOrder_Success_OrderCarriesConsumedAmountsNotRequested()
    {
        var consumption = ArrangeConsumption(grain: 3, recruits: 1);
        ArrangeSpawn();
        var requested = new Dictionary<string, int> { ["grain"] = 99 };

        var result = _sut.TryPlaceOrder(_townSource, requested, null, SupplyEscortOption.None, out _);

        Assert.AreEqual(3, result.Goods["grain"], "the order ships what was actually obtained");
        Assert.AreEqual(1, result.Recruits["troop_a"]);
        Assert.AreEqual(consumption.Goods.Count, result.Goods.Count);
    }

    [TestMethod]
    public void TryPlaceOrder_LordSource_ForcesEscortNone()
    {
        ArrangeConsumption(grain: 0, recruits: 2);
        ArrangeSpawn();
        var lordSource = new SupplySourceInfo { HeroId = "lord_1", CanOrder = true };

        var result = _sut.TryPlaceOrder(lordSource, null, null, SupplyEscortOption.Mercenaries, out _);

        Assert.AreEqual(SupplyEscortOption.None, result.EscortEnum);
        _pricing.Received(1).Quote(Arg.Any<float>(), Arg.Any<int>(), Arg.Any<float>(), SupplyEscortOption.None);
    }

    [TestMethod]
    public void TryPlaceOrder_CompanionEscortButNoCompanion_DowngradesToNone()
    {
        ArrangeConsumption();
        ArrangeSpawn();
        _sut.CompanionId = null;

        var result = _sut.TryPlaceOrder(_townSource, null, null, SupplyEscortOption.Companion, out _);

        Assert.AreEqual(SupplyEscortOption.None, result.EscortEnum);
        Assert.IsNull(result.EscortHeroId);
    }

    [TestMethod]
    public void TryPlaceOrder_CompanionEscortAvailable_RecordsEscortHero()
    {
        ArrangeConsumption();
        ArrangeSpawn();
        _sut.CompanionId = "companion_1";

        var result = _sut.TryPlaceOrder(_townSource, null, null, SupplyEscortOption.Companion, out _);

        Assert.AreEqual(SupplyEscortOption.Companion, result.EscortEnum);
        Assert.AreEqual("companion_1", result.EscortHeroId);
    }

    // --- HourlyTick ---

    [TestMethod]
    public void HourlyTick_DeliverVerdict_DeliversReleasesAndPurges()
    {
        var order = SeedInTransitOrder();
        _engine.Advance(Arg.Any<float>(), Arg.Any<bool>(), Arg.Any<bool>(), Arg.Any<float>(), Arg.Any<bool>())
            .Returns(SupplyOrderVerdict.Deliver);

        _sut.HourlyTick();

        Assert.AreSame(order, _sut.Delivered.Single());
        _caravans.Received(1).ReleaseEscortAndDestroy(order);
        Assert.AreEqual(0, _sut.ActiveOrders.Count, "delivered orders are purged");
    }

    [TestMethod]
    public void HourlyTick_LoseVerdict_ReleasesWithoutDeliveringAndPurges()
    {
        var order = SeedInTransitOrder();
        _engine.Advance(Arg.Any<float>(), Arg.Any<bool>(), Arg.Any<bool>(), Arg.Any<float>(), Arg.Any<bool>())
            .Returns(SupplyOrderVerdict.Lose);

        _sut.HourlyTick();

        Assert.AreEqual(0, _sut.Delivered.Count);
        _caravans.Received(1).ReleaseEscortAndDestroy(order);
        Assert.AreEqual(0, _sut.ActiveOrders.Count);
    }

    [TestMethod]
    public void HourlyTick_ContinueVerdict_KeepsOrderInBook()
    {
        SeedInTransitOrder();
        _engine.Advance(Arg.Any<float>(), Arg.Any<bool>(), Arg.Any<bool>(), Arg.Any<float>(), Arg.Any<bool>())
            .Returns(SupplyOrderVerdict.Continue);

        _sut.HourlyTick();

        Assert.AreEqual(0, _sut.Delivered.Count);
        _caravans.DidNotReceiveWithAnyArgs().ReleaseEscortAndDestroy(default);
        Assert.AreEqual(1, _sut.ActiveOrders.Count);
    }

    [TestMethod]
    public void HourlyTick_PassesEngineTheOrderStateInputs()
    {
        var order = SeedInTransitOrder();
        _sut.ElapsedFraction = 0.75f;
        _sut.InEncounter = true;
        _caravans.CaravanExists(order).Returns(true);
        _caravans.CaravanInMapEvent(order).Returns(true);
        _caravans.DistanceToPlayer(order).Returns(3.5f);
        _engine.Advance(Arg.Any<float>(), Arg.Any<bool>(), Arg.Any<bool>(), Arg.Any<float>(), Arg.Any<bool>())
            .Returns(SupplyOrderVerdict.Continue);

        _sut.HourlyTick();

        _engine.Received(1).Advance(0.75f, true, true, 3.5f, true);
    }

    [TestMethod]
    public void HourlyTick_PlayerCaptive_BlocksDeliveryLikeAnEncounter()
    {
        // A caravan must never force-hand cargo to a prisoner: captivity rides the same
        // delivery-blocked input as an encounter, so the engine holds the order.
        SeedInTransitOrder();
        _sut.InEncounter = false;
        _sut.Captive = true;
        _engine.Advance(Arg.Any<float>(), Arg.Any<bool>(), Arg.Any<bool>(), Arg.Any<float>(), Arg.Any<bool>())
            .Returns(SupplyOrderVerdict.Continue);

        _sut.HourlyTick();

        _engine.Received(1).Advance(
            Arg.Any<float>(), Arg.Any<bool>(), Arg.Any<bool>(), Arg.Any<float>(), true);
    }

    [TestMethod]
    public void HourlyTick_Deliver_CapsCargoByLiveRosters()
    {
        // The caravan set out with 5 grain + 2 recruits but arrives with 3 grain and 1 recruit
        // (food eaten, a battle lost): only what is actually aboard reaches the player.
        var order = SeedInTransitOrder();
        order.Goods["grain"] = 5;
        order.Recruits["troop_a"] = 2;
        _caravans.TryGetLiveCargo(
                order,
                out Arg.Any<IReadOnlyDictionary<string, int>>(),
                out Arg.Any<IReadOnlyDictionary<string, int>>())
            .Returns(x =>
            {
                x[1] = (IReadOnlyDictionary<string, int>)new Dictionary<string, int> { ["grain"] = 3 };
                x[2] = (IReadOnlyDictionary<string, int>)new Dictionary<string, int> { ["troop_a"] = 1 };
                return true;
            });
        _engine.Advance(Arg.Any<float>(), Arg.Any<bool>(), Arg.Any<bool>(), Arg.Any<float>(), Arg.Any<bool>())
            .Returns(SupplyOrderVerdict.Deliver);

        _sut.HourlyTick();

        Assert.AreEqual(3, _sut.LastDeliveredGoods["grain"]);
        Assert.AreEqual(1, _sut.LastDeliveredRecruits["troop_a"]);
    }

    [TestMethod]
    public void HourlyTick_Deliver_NoLiveSnapshot_DeliversOrderedAmounts()
    {
        // An unreadable party must not zero the delivery; the ordered amounts stand.
        var order = SeedInTransitOrder();
        order.Goods["grain"] = 5;
        _caravans.TryGetLiveCargo(
                order,
                out Arg.Any<IReadOnlyDictionary<string, int>>(),
                out Arg.Any<IReadOnlyDictionary<string, int>>())
            .Returns(false);
        _engine.Advance(Arg.Any<float>(), Arg.Any<bool>(), Arg.Any<bool>(), Arg.Any<float>(), Arg.Any<bool>())
            .Returns(SupplyOrderVerdict.Deliver);

        _sut.HourlyTick();

        Assert.AreEqual(5, _sut.LastDeliveredGoods["grain"]);
    }

    // --- CapByLive (pure) ---

    [TestMethod]
    public void CapByLive_CapsEachRowAndDropsZeroRows()
    {
        var ordered = new Dictionary<string, int> { ["grain"] = 5, ["fish"] = 2, ["tools"] = 3 };
        var live = new Dictionary<string, int> { ["grain"] = 3, ["fish"] = 9 };

        var result = SupplyOrderService.CapByLive(ordered, live);

        Assert.AreEqual(3, result["grain"], "capped by what is aboard");
        Assert.AreEqual(2, result["fish"], "never more than was ordered");
        Assert.IsFalse(result.ContainsKey("tools"), "nothing aboard means no row");
    }

    [TestMethod]
    public void CapByLive_NullLive_ReturnsOrderedUnchanged()
    {
        var ordered = new Dictionary<string, int> { ["grain"] = 5 };

        Assert.AreSame(ordered, SupplyOrderService.CapByLive(ordered, null));
    }

    [TestMethod]
    public void CapByLive_NullOrdered_ReturnsEmpty()
    {
        Assert.AreEqual(0, SupplyOrderService.CapByLive(null, new Dictionary<string, int>()).Count);
    }

    // --- FrameTick ---

    [TestMethod]
    public void FrameTick_WithOrders_TicksPositionsAndAppliesVerdicts()
    {
        var order = SeedInTransitOrder();
        _engine.Advance(Arg.Any<float>(), Arg.Any<bool>(), Arg.Any<bool>(), Arg.Any<float>(), Arg.Any<bool>())
            .Returns(SupplyOrderVerdict.Deliver);

        _sut.FrameTick();

        _caravans.Received(1).TickPositions();
        Assert.AreSame(order, _sut.Delivered.Single());
        Assert.AreEqual(0, _sut.ActiveOrders.Count);
    }

    [TestMethod]
    public void FrameTick_EmptyBook_DoesNoWork()
    {
        _sut.FrameTick();

        _caravans.DidNotReceive().TickPositions();
    }

    [TestMethod]
    public void FrameTick_CampaignClockFrozen_SkipsAfterTheFirstPass()
    {
        // Pause: neither the travel fraction nor any party position can change, so repeated
        // frames at the same campaign hour do no work. Time moving runs the pass again.
        SeedInTransitOrder();
        _engine.Advance(Arg.Any<float>(), Arg.Any<bool>(), Arg.Any<bool>(), Arg.Any<float>(), Arg.Any<bool>())
            .Returns(SupplyOrderVerdict.Continue);
        _sut.NowHours = 100.0;

        _sut.FrameTick();
        _sut.FrameTick();
        _caravans.Received(1).TickPositions();

        _sut.NowHours = 100.001;
        _sut.FrameTick();
        _caravans.Received(2).TickPositions();
    }

    // --- CancelAll ---

    [TestMethod]
    public void CancelAll_ReleasesEveryEscortThenEmptiesBook()
    {
        var orderA = new SupplyOrder { OrderId = "taom_so_0", SourceSettlementId = "town_G1" };
        orderA.StatusEnum = SupplyOrderStatus.InTransit;
        var orderB = new SupplyOrder { OrderId = "taom_so_1", SourceSettlementId = "town_G2" };
        orderB.StatusEnum = SupplyOrderStatus.InTransit;
        _sut.LoadFrom(
            new Dictionary<string, SupplyOrder> { [orderA.OrderId] = orderA, [orderB.OrderId] = orderB },
            counter: 2);

        _sut.CancelAll();

        // The escort release (which itself destroys the party AFTER freeing the companion, inside
        // the caravan service) must run for each order before the book is purged; purging first
        // would drop the only reference that lets the escort come home.
        Received.InOrder(() =>
        {
            _caravans.ReleaseEscortAndDestroy(orderA);
            _caravans.ReleaseEscortAndDestroy(orderB);
        });
        Assert.AreEqual(0, _sut.ActiveOrders.Count);
        Assert.AreEqual(SupplyOrderStatus.Lost, orderA.StatusEnum);
        Assert.AreEqual(SupplyOrderStatus.Lost, orderB.StatusEnum);
    }

    [TestMethod]
    public void CancelAll_EmptyBook_TouchesNothing()
    {
        _sut.CancelAll();

        _caravans.DidNotReceiveWithAnyArgs().ReleaseEscortAndDestroy(default);
    }

    // --- CancelCampOrders ---

    [TestMethod]
    public void CancelCampOrders_CancelsOnlyCampPlacedOrders()
    {
        var townOrder = new SupplyOrder { OrderId = "taom_so_0", SourceSettlementId = "town_G1" };
        townOrder.StatusEnum = SupplyOrderStatus.InTransit;
        var campOrder = new SupplyOrder
        {
            OrderId = "taom_so_1",
            SourceSettlementId = "town_G2",
            PlacedFromCamp = true,
        };
        campOrder.StatusEnum = SupplyOrderStatus.InTransit;
        _sut.LoadFrom(
            new Dictionary<string, SupplyOrder>
            {
                [townOrder.OrderId] = townOrder,
                [campOrder.OrderId] = campOrder,
            },
            counter: 2);

        _sut.CancelCampOrders();

        _caravans.Received(1).ReleaseEscortAndDestroy(campOrder);
        _caravans.DidNotReceive().ReleaseEscortAndDestroy(townOrder);
        Assert.AreEqual(SupplyOrderStatus.Lost, campOrder.StatusEnum);
        Assert.AreEqual(SupplyOrderStatus.InTransit, townOrder.StatusEnum,
            "a town-placed order must survive a camp break untouched");
        Assert.AreEqual(1, _sut.ActiveOrders.Count);
    }

    [TestMethod]
    public void CancelCampOrders_NoCampOrders_TouchesNothing()
    {
        SeedInTransitOrder(); // town-placed

        _sut.CancelCampOrders();

        _caravans.DidNotReceiveWithAnyArgs().ReleaseEscortAndDestroy(default);
        Assert.AreEqual(1, _sut.ActiveOrders.Count);
    }

    [TestMethod]
    public void TryPlaceOrder_PlacedFromCamp_MarksTheOrder()
    {
        ArrangeConsumption();
        ArrangeSpawn();

        var result = _sut.TryPlaceOrder(
            _townSource, null, null, SupplyEscortOption.None, out _, placedFromCamp: true);

        Assert.IsTrue(result.PlacedFromCamp);
    }

    [TestMethod]
    public void TryPlaceOrder_Default_NotMarkedAsCampPlaced()
    {
        ArrangeConsumption();
        ArrangeSpawn();

        var result = _sut.TryPlaceOrder(_townSource, null, null, SupplyEscortOption.None, out _);

        Assert.IsFalse(result.PlacedFromCamp);
    }

    // --- persistence plumbing ---

    [TestMethod]
    public void LoadFrom_SaveInto_RoundTripsBookAndCounter()
    {
        var order = new SupplyOrder { OrderId = "taom_so_7" };
        order.StatusEnum = SupplyOrderStatus.InTransit;
        var book = new Dictionary<string, SupplyOrder> { [order.OrderId] = order };

        _sut.LoadFrom(book, counter: 8);
        _sut.SaveInto(out var savedBook, out var savedCounter);

        Assert.AreSame(book, savedBook);
        Assert.AreEqual(8, savedCounter);
    }

    [TestMethod]
    public void LoadFrom_NullBook_InstallsEmptyBook()
    {
        _sut.LoadFrom(null, counter: -3);
        _sut.SaveInto(out var savedBook, out var savedCounter);

        Assert.IsNotNull(savedBook);
        Assert.AreEqual(0, savedBook.Count);
        Assert.AreEqual(0, savedCounter, "a negative persisted counter is clamped");
    }

    [TestMethod]
    public void OnGameLoaded_HandsInTransitOrdersToRespawn()
    {
        var inTransit = SeedInTransitOrder();

        _sut.OnGameLoaded();

        _caravans.Received(1).RespawnMissing(
            Arg.Is<IEnumerable<SupplyOrder>>(orders => orders.Single() == inTransit));
    }

    // --- session reset + transient-cache hygiene ---

    [TestMethod]
    public void LoadFrom_ClearsCaravanTrackers()
    {
        // A tracker's cached MobileParty belongs to the session that created it; installing a
        // loaded book must drop them all so OnGameLoaded rebinds against live parties.
        SeedInTransitOrder();

        _caravans.Received(1).ClearTrackers();
    }

    [TestMethod]
    public void LoadFrom_NullAndInvalidRows_DroppedWithOneWarning()
    {
        var good = new SupplyOrder { OrderId = "taom_so_1", SourceSettlementId = "town_G1" };
        good.StatusEnum = SupplyOrderStatus.InTransit;
        var noId = new SupplyOrder(); // OrderId null: unusable row
        var book = new Dictionary<string, SupplyOrder>
        {
            ["taom_so_0"] = null,
            [good.OrderId] = good,
            ["taom_so_2"] = noId,
        };

        _sut.LoadFrom(book, counter: 3);
        _sut.SaveInto(out var saved, out _);

        Assert.AreEqual(1, saved.Count, "null and id-less rows are scrubbed, valid rows survive");
        Assert.AreSame(good, saved[good.OrderId]);
        _logger.Received(1).LogWarning(Arg.Is<string>(m => m.Contains("dropped 2")));

        // The tick paths must be NRE-safe now that the bad rows are gone.
        _engine.Advance(Arg.Any<float>(), Arg.Any<bool>(), Arg.Any<bool>(), Arg.Any<float>(), Arg.Any<bool>())
            .Returns(SupplyOrderVerdict.Continue);
        _sut.HourlyTick();
    }

    [TestMethod]
    public void ResetForNewSession_ClearsBookCounterAndTrackers()
    {
        SeedInTransitOrder();
        _caravans.ClearReceivedCalls();

        _sut.ResetForNewSession();
        _sut.SaveInto(out var saved, out var counter);

        Assert.AreEqual(0, saved.Count, "a fresh session must not inherit the previous book");
        Assert.AreEqual(0, counter, "the order counter is per-campaign");
        Assert.AreEqual(0, _sut.ActiveOrders.Count);
        _caravans.Received(1).ClearTrackers();
    }
}
