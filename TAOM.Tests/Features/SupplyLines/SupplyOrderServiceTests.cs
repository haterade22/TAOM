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
[TestCategory("RequiresGame")]
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
        // Every gold move a charge makes, in order: "lord:<heroId>:<amount>",
        // "settlement:<settlementId>:<amount>", "destroy:<amount>".
        public readonly List<string> Charges = new List<string>();
        public readonly HashSet<string> UnreachablePayees = new HashSet<string>();
        public readonly List<SupplyOrder> Delivered = new List<SupplyOrder>();
        public IReadOnlyDictionary<string, int> LastDeliveredGoods;
        public IReadOnlyDictionary<string, int> LastDeliveredRecruits;
        // Every refund step, in order: "source:<id>" (the reachability check),
        // "lord:<heroId>:<troopId>:<count>", "goods:<settlementId>:<itemId>:<count>",
        // "volunteer:<settlementId>:<notable>:<slot>:<troopId>".
        public readonly List<string> Refunds = new List<string>();
        public readonly HashSet<string> UnreachableSources = new HashSet<string>();

        // Per notable, true for an empty volunteer slot; a null entry is a notable without a slot
        // array, a null list a settlement without notables. A fill writes back here, like the engine.
        public List<bool[]> VolunteerSlots = new List<bool[]> { new[] { true, true, true } };
        public int VolunteerSlotReads;
        public bool GoodsRefundThrows;

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

        protected override bool PayLord(string heroId, int amount)
        {
            CallSequence.Add("charge");
            Charges.Add("lord:" + heroId + ":" + amount);
            return !UnreachablePayees.Contains(heroId);
        }

        protected override bool PaySettlement(string settlementId, int amount)
        {
            CallSequence.Add("charge");
            Charges.Add("settlement:" + settlementId + ":" + amount);
            return !UnreachablePayees.Contains(settlementId);
        }

        protected override void DestroyPlayerGold(int amount)
        {
            CallSequence.Add("charge");
            Charges.Add("destroy:" + amount);
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

        protected override bool CanReturnTroopsToLord(string heroId)
        {
            Refunds.Add("source:" + heroId);
            return !UnreachableSources.Contains(heroId);
        }

        protected override void ReturnTroopsToLord(string heroId, string troopId, int count) =>
            Refunds.Add("lord:" + heroId + ":" + troopId + ":" + count);

        protected override bool CanReturnStockToSettlement(string settlementId)
        {
            Refunds.Add("source:" + settlementId);
            return !UnreachableSources.Contains(settlementId);
        }

        protected override void ReturnGoodsToSettlement(string settlementId, string itemId, int count)
        {
            if (GoodsRefundThrows)
                throw new System.InvalidOperationException("roster locked");
            Refunds.Add("goods:" + settlementId + ":" + itemId + ":" + count);
        }

        protected override IReadOnlyList<bool[]> ReadEmptyVolunteerSlots(string settlementId)
        {
            VolunteerSlotReads++;
            // A fresh copy per read, like the engine snapshot: the service sees its own earlier
            // fills only by reading again.
            return VolunteerSlots?.ConvertAll(slots => slots == null ? null : (bool[])slots.Clone());
        }

        protected override void FillVolunteerSlot(string settlementId, int notableIndex, int slotIndex, string troopId)
        {
            VolunteerSlots[notableIndex][slotIndex] = false;
            Refunds.Add("volunteer:" + settlementId + ":" + notableIndex + ":" + slotIndex + ":" + troopId);
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
        CollectionAssert.AreEqual(
            new[] { "source:town_G1", "goods:town_G1:grain:5", "volunteer:town_G1:0:0:troop_a", "volunteer:town_G1:0:1:troop_a" },
            _sut.Refunds, "the consumed grain and recruits go back to the source");
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
        CollectionAssert.AreEqual(
            new[] { "source:town_G1", "goods:town_G1:grain:5", "volunteer:town_G1:0:0:troop_a", "volunteer:town_G1:0:1:troop_a" },
            _sut.Refunds, "the consumed grain and recruits go back to the source");
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
            new[] { "consume", "spawn", "charge", "charge" }, _sut.CallSequence,
            "the charge must land only after the caravan exists (the source module charged first)");
        CollectionAssert.AreEqual(
            new[] { "settlement:town_G1:150", "destroy:20" }, _sut.Charges,
            "the source is credited its share (round B: payments were a pure sink) and the 20 "
            + "transport fee is destroyed: 150 + 20 is the 170 total");
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
    public void CancelCampOrders_StatusIsLostBeforeThePartyIsDestroyed()
    {
        // DestroyPartyAction raises MobilePartyDestroyed synchronously; OnCaravanDestroyed must
        // see a finished status or our own teardown is recorded as a second battle loss.
        var campOrder = new SupplyOrder
        {
            OrderId = "taom_so_0",
            SourceSettlementId = "town_G1",
            PlacedFromCamp = true,
        };
        campOrder.StatusEnum = SupplyOrderStatus.InTransit;
        _sut.LoadFrom(new Dictionary<string, SupplyOrder> { [campOrder.OrderId] = campOrder }, counter: 1);
        SupplyOrderStatus statusAtDestroy = default;
        _caravans.When(c => c.ReleaseEscortAndDestroy(campOrder))
            .Do(_ => statusAtDestroy = campOrder.StatusEnum);

        _sut.CancelCampOrders();

        Assert.AreEqual(SupplyOrderStatus.Lost, statusAtDestroy);
    }

    [TestMethod]
    public void HourlyTick_Deliver_StatusIsDeliveredBeforeThePartyIsDestroyed()
    {
        var order = SeedInTransitOrder();
        _engine.Advance(Arg.Any<float>(), Arg.Any<bool>(), Arg.Any<bool>(), Arg.Any<float>(), Arg.Any<bool>())
            .Returns(SupplyOrderVerdict.Deliver);
        SupplyOrderStatus statusAtDestroy = default;
        _caravans.When(c => c.ReleaseEscortAndDestroy(order))
            .Do(_ => statusAtDestroy = order.StatusEnum);

        _sut.HourlyTick();

        Assert.AreEqual(SupplyOrderStatus.Delivered, statusAtDestroy,
            "a synchronous MobilePartyDestroyed must not see the just-delivered order as InTransit");
    }

    // --- OnCaravanDestroyed (engine destroyed the party, e.g. a lost AI battle) ---

    [TestMethod]
    public void OnCaravanDestroyed_InTransitOrder_MarksLostForgetsTrackerAndPurges()
    {
        var order = SeedInTransitOrder();

        _sut.OnCaravanDestroyed(order.OrderId);

        Assert.AreEqual(SupplyOrderStatus.Lost, order.StatusEnum);
        _caravans.Received(1).ForgetDestroyed(order);
        _caravans.DidNotReceiveWithAnyArgs().ReleaseEscortAndDestroy(default);
        Assert.AreEqual(0, _sut.ActiveOrders.Count,
            "the loss is recorded immediately so no later save carries a stale InTransit row");
    }

    [TestMethod]
    public void OnCaravanDestroyed_FinishedOrder_IsIgnored()
    {
        // Our own Deliver/Lose/Cancel paths destroy the party AFTER flipping the status; the
        // synchronous destroy event must fall through for them.
        var order = SeedInTransitOrder();
        order.StatusEnum = SupplyOrderStatus.Delivered;

        _sut.OnCaravanDestroyed(order.OrderId);

        Assert.AreEqual(SupplyOrderStatus.Delivered, order.StatusEnum);
        _caravans.DidNotReceiveWithAnyArgs().ForgetDestroyed(default);
    }

    [TestMethod]
    public void OnCaravanDestroyed_UnknownOrNullOrderId_DoesNothing()
    {
        SeedInTransitOrder();

        _sut.OnCaravanDestroyed("taom_so_99");
        _sut.OnCaravanDestroyed(null);

        Assert.AreEqual(1, _sut.ActiveOrders.Count);
        _caravans.DidNotReceiveWithAnyArgs().ForgetDestroyed(default);
    }

    // --- dispatch message ---

    [TestMethod]
    public void DispatchMessageTemplate_LordOrder_NamesTheLord()
    {
        // Source parity: the module's '{LORD} sends reinforcements' messenger line.
        StringAssert.Contains(SupplyOrderService.DispatchMessageTemplate(isFromLord: true), "{LORD}");
        StringAssert.Contains(
            SupplyOrderService.DispatchMessageTemplate(isFromLord: true), "taom_sl_lord_dispatched");
    }

    [TestMethod]
    public void DispatchMessageTemplate_SettlementOrder_UsesTheGenericCaravanLine()
    {
        StringAssert.Contains(
            SupplyOrderService.DispatchMessageTemplate(isFromLord: false), "taom_sl_dispatched");
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
    public void LoadFrom_CounterBelowLoadedIds_DerivedFromTheIds()
    {
        // A recovered save can carry the book without the counter key (0 comes in); trusting it
        // would mint taom_so_7 again over the live order (Codex round 2 #2).
        var order = new SupplyOrder { OrderId = "taom_so_7", SourceSettlementId = "town_G1" };
        order.StatusEnum = SupplyOrderStatus.InTransit;

        _sut.LoadFrom(new Dictionary<string, SupplyOrder> { [order.OrderId] = order }, counter: 0);
        _sut.SaveInto(out _, out var counter);

        Assert.AreEqual(8, counter, "the loaded ids are authoritative; the persisted counter is only a floor");
    }

    [TestMethod]
    public void LoadFrom_CounterAheadOfLoadedIds_Kept()
    {
        var order = new SupplyOrder { OrderId = "taom_so_2", SourceSettlementId = "town_G1" };
        order.StatusEnum = SupplyOrderStatus.InTransit;

        _sut.LoadFrom(new Dictionary<string, SupplyOrder> { [order.OrderId] = order }, counter: 12);
        _sut.SaveInto(out _, out var counter);

        Assert.AreEqual(12, counter, "purged orders legitimately leave the counter ahead of the book");
    }

    [TestMethod]
    public void LoadFrom_MalformedOrderIds_DoNotPoisonTheCounter()
    {
        var alien = new SupplyOrder { OrderId = "not_our_prefix_5", SourceSettlementId = "town_G1" };
        alien.StatusEnum = SupplyOrderStatus.InTransit;
        var garbage = new SupplyOrder { OrderId = "taom_so_notanumber", SourceSettlementId = "town_G1" };
        garbage.StatusEnum = SupplyOrderStatus.InTransit;

        _sut.LoadFrom(
            new Dictionary<string, SupplyOrder> { [alien.OrderId] = alien, [garbage.OrderId] = garbage },
            counter: 3);
        _sut.SaveInto(out _, out var counter);

        Assert.AreEqual(3, counter);
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

    // --- ChargePlayer (payee routing; the three gold seams only move gold) ---

    [TestMethod]
    public void ChargePlayer_SettlementSource_CreditsSettlementThenDestroysFees()
    {
        _sut.ChargePlayer(_townSource, new SupplyQuote(goods: 100, troops: 50, transport: 20, guard: 5));

        CollectionAssert.AreEqual(new[] { "settlement:town_G1:150", "destroy:25" }, _sut.Charges);
    }

    [TestMethod]
    public void ChargePlayer_LordSource_CreditsLordThenDestroysFees()
    {
        var lord = new SupplySourceInfo { HeroId = "lord_1" };

        _sut.ChargePlayer(lord, new SupplyQuote(goods: 0, troops: 80, transport: 10, guard: 0));

        CollectionAssert.AreEqual(new[] { "lord:lord_1:80", "destroy:10" }, _sut.Charges);
    }

    [TestMethod]
    public void ChargePlayer_HeroIdAndSettlementIdBothSet_PaysTheLord()
    {
        var source = new SupplySourceInfo { HeroId = "lord_1", SettlementId = "town_G1" };

        _sut.ChargePlayer(source, new SupplyQuote(goods: 100, troops: 0, transport: 0, guard: 0));

        CollectionAssert.AreEqual(new[] { "lord:lord_1:100" }, _sut.Charges);
    }

    [TestMethod]
    public void ChargePlayer_EmptyHeroId_PaysTheSettlement()
    {
        var source = new SupplySourceInfo { HeroId = "", SettlementId = "town_G1" };

        _sut.ChargePlayer(source, new SupplyQuote(goods: 100, troops: 0, transport: 0, guard: 0));

        CollectionAssert.AreEqual(new[] { "settlement:town_G1:100" }, _sut.Charges);
    }

    [TestMethod]
    public void ChargePlayer_LordUnreachable_DestroysHisShareAndWarns()
    {
        _sut.UnreachablePayees.Add("lord_1");

        _sut.ChargePlayer(new SupplySourceInfo { HeroId = "lord_1" },
            new SupplyQuote(goods: 0, troops: 80, transport: 10, guard: 0));

        CollectionAssert.AreEqual(new[] { "lord:lord_1:80", "destroy:80", "destroy:10" }, _sut.Charges);
        _logger.Received(1).LogWarning("[SupplyLines] charge: lord 'lord_1' unreachable, his share is destroyed");
    }

    [TestMethod]
    public void ChargePlayer_SettlementUnreachable_DestroysItsShareAndWarns()
    {
        _sut.UnreachablePayees.Add("town_G1");

        _sut.ChargePlayer(_townSource, new SupplyQuote(goods: 100, troops: 50, transport: 20, guard: 0));

        CollectionAssert.AreEqual(new[] { "settlement:town_G1:150", "destroy:150", "destroy:20" }, _sut.Charges);
        _logger.Received(1).LogWarning("[SupplyLines] charge: settlement 'town_G1' unreachable, its share is destroyed");
    }

    [TestMethod]
    public void ChargePlayer_ReachablePayee_LogsNothing()
    {
        _sut.ChargePlayer(_townSource, new SupplyQuote(goods: 100, troops: 50, transport: 20, guard: 0));

        _logger.DidNotReceiveWithAnyArgs().LogWarning(default);
    }

    [TestMethod]
    public void ChargePlayer_NoSourceShare_OnlyDestroysFees()
    {
        _sut.ChargePlayer(_townSource, new SupplyQuote(goods: 0, troops: 0, transport: 20, guard: 5));

        CollectionAssert.AreEqual(new[] { "destroy:25" }, _sut.Charges);
    }

    [TestMethod]
    public void ChargePlayer_NoFees_DestroysNothing()
    {
        _sut.ChargePlayer(_townSource, new SupplyQuote(goods: 100, troops: 0, transport: 0, guard: 0));

        CollectionAssert.AreEqual(new[] { "settlement:town_G1:100" }, _sut.Charges);
    }

    [TestMethod]
    public void ChargePlayer_NonPositiveAmounts_MoveNoGold()
    {
        _sut.ChargePlayer(_townSource, new SupplyQuote(goods: -5, troops: 0, transport: -1, guard: 0));

        Assert.AreEqual(0, _sut.Charges.Count, "only a positive share or fee moves gold");
    }

    // --- RefundConsumption (route, count filter and the volunteer-slot walk; the six refund seams each read or write once) ---

    private static SupplyConsumption Consumed(
        Dictionary<string, int> goods = null, Dictionary<string, int> troops = null) =>
        new SupplyConsumption
        {
            Goods = goods ?? new Dictionary<string, int>(),
            Troops = troops ?? new Dictionary<string, int>(),
        };

    [TestMethod]
    public void RefundConsumption_SettlementSource_ReturnsGoodsThenVolunteers()
    {
        _sut.RefundConsumption(_townSource, Consumed(
            goods: new Dictionary<string, int> { ["grain"] = 5, ["wine"] = 2 },
            troops: new Dictionary<string, int> { ["troop_a"] = 1 }));

        CollectionAssert.AreEqual(
            new[] { "source:town_G1", "goods:town_G1:grain:5", "goods:town_G1:wine:2", "volunteer:town_G1:0:0:troop_a" },
            _sut.Refunds);
        _logger.DidNotReceiveWithAnyArgs().LogWarning(default);
        _logger.DidNotReceiveWithAnyArgs().LogError(default);
    }

    [TestMethod]
    public void RefundConsumption_EmptyHeroId_UsesTheSettlement()
    {
        var source = new SupplySourceInfo { HeroId = "", SettlementId = "town_G1" };

        _sut.RefundConsumption(source, Consumed(goods: new Dictionary<string, int> { ["grain"] = 1 }));

        CollectionAssert.AreEqual(new[] { "source:town_G1", "goods:town_G1:grain:1" }, _sut.Refunds);
    }

    [TestMethod]
    public void RefundConsumption_LordSource_ReturnsOnlyTroopsToHisParty()
    {
        var source = new SupplySourceInfo { HeroId = "lord_1", SettlementId = "town_G1" };

        _sut.RefundConsumption(source, Consumed(
            goods: new Dictionary<string, int> { ["grain"] = 5 },
            troops: new Dictionary<string, int> { ["troop_a"] = 3, ["troop_b"] = 1 }));

        CollectionAssert.AreEqual(
            new[] { "source:lord_1", "lord:lord_1:troop_a:3", "lord:lord_1:troop_b:1" }, _sut.Refunds,
            "a lord source takes back troops only; its settlement id and any goods are ignored");
        Assert.AreEqual(0, _sut.VolunteerSlotReads);
    }

    [TestMethod]
    public void RefundConsumption_LordUnreachable_WarnsAndReturnsNothing()
    {
        _sut.UnreachableSources.Add("lord_1");

        _sut.RefundConsumption(new SupplySourceInfo { HeroId = "lord_1" },
            Consumed(troops: new Dictionary<string, int> { ["troop_a"] = 3 }));

        CollectionAssert.AreEqual(new[] { "source:lord_1" }, _sut.Refunds);
        _logger.Received(1).LogWarning("[SupplyLines] refund: lord 'lord_1' unreachable, troops not restored");
    }

    [TestMethod]
    public void RefundConsumption_SettlementUnreachable_WarnsAndReturnsNothing()
    {
        _sut.UnreachableSources.Add("town_G1");

        _sut.RefundConsumption(_townSource, Consumed(
            goods: new Dictionary<string, int> { ["grain"] = 5 },
            troops: new Dictionary<string, int> { ["troop_a"] = 1 }));

        CollectionAssert.AreEqual(new[] { "source:town_G1" }, _sut.Refunds);
        Assert.AreEqual(0, _sut.VolunteerSlotReads);
        _logger.Received(1).LogWarning("[SupplyLines] refund: settlement 'town_G1' unreachable, stock not restored");
    }

    [TestMethod]
    public void RefundConsumption_SettlementNonPositiveCounts_Skipped()
    {
        _sut.RefundConsumption(_townSource, Consumed(
            goods: new Dictionary<string, int> { ["grain"] = 0, ["wine"] = -1 },
            troops: new Dictionary<string, int> { ["troop_a"] = 0, ["troop_b"] = -2 }));

        CollectionAssert.AreEqual(new[] { "source:town_G1" }, _sut.Refunds);
        Assert.AreEqual(0, _sut.VolunteerSlotReads, "a non-positive recruit count never walks the slots");
    }

    [TestMethod]
    public void RefundConsumption_LordNonPositiveCounts_Skipped()
    {
        _sut.RefundConsumption(new SupplySourceInfo { HeroId = "lord_1" }, Consumed(
            troops: new Dictionary<string, int> { ["troop_a"] = 0, ["troop_b"] = -1, ["troop_c"] = 2 }));

        CollectionAssert.AreEqual(new[] { "source:lord_1", "lord:lord_1:troop_c:2" }, _sut.Refunds);
    }

    [TestMethod]
    public void RefundConsumption_Volunteers_FillTheFirstEmptySlotsInNotableOrder()
    {
        _sut.VolunteerSlots = new List<bool[]> { new[] { false, true, false, true }, new[] { true, true } };

        _sut.RefundConsumption(_townSource, Consumed(troops: new Dictionary<string, int> { ["troop_a"] = 3 }));

        CollectionAssert.AreEqual(
            new[] { "source:town_G1", "volunteer:town_G1:0:1:troop_a", "volunteer:town_G1:0:3:troop_a", "volunteer:town_G1:1:0:troop_a" },
            _sut.Refunds);
    }

    [TestMethod]
    public void RefundConsumption_NotableWithoutSlots_Skipped()
    {
        _sut.VolunteerSlots = new List<bool[]> { null, new[] { true } };

        _sut.RefundConsumption(_townSource, Consumed(troops: new Dictionary<string, int> { ["troop_a"] = 1 }));

        CollectionAssert.AreEqual(new[] { "source:town_G1", "volunteer:town_G1:1:0:troop_a" }, _sut.Refunds);
    }

    [TestMethod]
    public void RefundConsumption_MoreRecruitsThanFreeSlots_ExtraDroppedSilently()
    {
        _sut.VolunteerSlots = new List<bool[]> { new[] { true, false }, new[] { true } };

        _sut.RefundConsumption(_townSource, Consumed(troops: new Dictionary<string, int> { ["troop_a"] = 5 }));

        CollectionAssert.AreEqual(
            new[] { "source:town_G1", "volunteer:town_G1:0:0:troop_a", "volunteer:town_G1:1:0:troop_a" },
            _sut.Refunds, "the source drops recruits beyond the free slots without a word");
        _logger.DidNotReceiveWithAnyArgs().LogWarning(default);
        _logger.DidNotReceiveWithAnyArgs().LogError(default);
    }

    [TestMethod]
    public void RefundConsumption_SecondTroopType_ReadsTheSlotsAgain()
    {
        _sut.VolunteerSlots = new List<bool[]> { new[] { true, true, true } };

        _sut.RefundConsumption(_townSource, Consumed(
            troops: new Dictionary<string, int> { ["troop_a"] = 2, ["troop_b"] = 2 }));

        CollectionAssert.AreEqual(
            new[] { "source:town_G1", "volunteer:town_G1:0:0:troop_a", "volunteer:town_G1:0:1:troop_a", "volunteer:town_G1:0:2:troop_b" },
            _sut.Refunds, "troop_b never lands on a slot troop_a just filled");
        Assert.AreEqual(2, _sut.VolunteerSlotReads);
    }

    [TestMethod]
    public void RefundConsumption_NoNotables_ReturnsGoodsOnly()
    {
        _sut.VolunteerSlots = null;

        _sut.RefundConsumption(_townSource, Consumed(
            goods: new Dictionary<string, int> { ["grain"] = 1 },
            troops: new Dictionary<string, int> { ["troop_a"] = 2 }));

        CollectionAssert.AreEqual(new[] { "source:town_G1", "goods:town_G1:grain:1" }, _sut.Refunds);
        _logger.DidNotReceiveWithAnyArgs().LogWarning(default);
    }

    [TestMethod]
    public void RefundConsumption_SeamThrows_LogsErrorAndStops()
    {
        _sut.GoodsRefundThrows = true;

        _sut.RefundConsumption(_townSource, Consumed(
            goods: new Dictionary<string, int> { ["grain"] = 1 },
            troops: new Dictionary<string, int> { ["troop_a"] = 1 }));

        CollectionAssert.AreEqual(new[] { "source:town_G1" }, _sut.Refunds, "nothing after the throw runs");
        Assert.AreEqual(0, _sut.VolunteerSlotReads);
        _logger.Received(1).LogError("[SupplyLines] refund after failed order placement threw: roster locked");
    }
}
