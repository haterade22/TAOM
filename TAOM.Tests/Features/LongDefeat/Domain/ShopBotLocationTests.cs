using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.LongDefeat.Domain;

namespace TAOM.Tests.Features.LongDefeat.Domain;

/// <summary>
/// The bot at the armoury and the yard (phase4-plan.md D44). The decisions are judged on hand-built views; the replays drive a real <see cref="SurvivalRun"/> with the bot and a lord who walks, so the
/// errand, the buy and the horn are seen in the order a game run would give them, and each named proof run is shown to own what it needs by the wave that proves it.
/// </summary>
[TestClass]
public class ShopBotLocationTests
{
    private static readonly GearSet Mail = new("armour_1", "Mail", "", GearKind.Armour, null, new[] { new LdKitSlot("Body", "mail") }, 250);
    private static readonly GearSet Plate = new("armour_2", "Plate", "", GearKind.Armour, null, new[] { new LdKitSlot("Body", "plate") }, 400);
    private static readonly GearSet Blade = new("weapons_sword", "Blade", "", GearKind.Weapons, new[] { "sword" }, new[] { new LdKitSlot("Item0", "long_blade") }, 200);
    private static readonly LdSite Site0 = new(0, new LdPoint(70f, 20f, 10f), new[] { "mangonel" });
    private static readonly LdSite Site1 = new(1, new LdPoint(90f, 20f, 10f), new[] { "mangonel" });
    private static readonly EngineOffer MangonelRow = new("mangonel", 450);

    private static GearRow Row(GearSet set, GearState state = GearState.ForSale) => new(set, set.Price, state);

    private static SiteRow Free(LdSite site) => new(site, SiteState.Free, "", new[] { MangonelRow });

    private static LocationsView Places(LdPlaceKind near = LdPlaceKind.None, bool ownBody = true, IReadOnlyList<GearRow>? gear = null, IReadOnlyList<SiteRow>? sites = null,
        LdPlaceKind errand = LdPlaceKind.None, params LdPlaceKind[] unreached) =>
        new(near, ownBody, gear ?? new[] { Row(Mail), Row(Plate), Row(Blade) }, sites ?? new[] { Free(Site0), Free(Site1) }, errand, unreached);

    /// <summary>A view with no pack the gold reaches and no fortification, so the two places are all the bot can want.</summary>
    private static RunView View(LocationsView places, int cleared = 0, int gold = 5000, float secondsLeft = 60f, bool startGold = true, IReadOnlyList<ShopOffer>? packs = null) =>
        new(cleared == 0 ? RunPhase.Preparation : RunPhase.Intermission, cleared, cleared, gold, secondsLeft, 100, 0, 0, 0f, packs ?? Array.Empty<ShopOffer>())
        {
            Fort = FortView.None with { BuysFirst = startGold },
            Locations = places,
        };

    // ---- the decisions ----

    [TestMethod]
    public void Decide_InThePreparation_WalksToTheArmoury_ForTheFirstArmourSet()
    {
        Assert.AreEqual(new BotAction(BotActionKind.Errand, "Armoury"), ShopBot.Decide(View(Places()), 0f));
        Assert.AreEqual(new BotAction(BotActionKind.Gear, "armour_1"), ShopBot.Decide(View(Places(LdPlaceKind.Armoury)), 0f), "a lord who already stands there buys with no errand");
    }

    [TestMethod]
    public void Decide_InThePreparation_WantsNoWeaponsSetAndNoEngine()
    {
        var owned = Places(gear: new[] { Row(Mail, GearState.Owned), Row(Plate), Row(Blade) });

        Assert.AreEqual(BotActionKind.Wait, ShopBot.Decide(View(owned), 0f).Kind, "the schedule buys one armour set, and the rest waits for clear 1");
    }

    [TestMethod]
    public void Decide_AtClearOne_WantsTheWeaponsSet_ThenOneEngineAtTheFirstFreeSite()
    {
        var armoured = new[] { Row(Mail, GearState.Owned), Row(Plate), Row(Blade) };
        var armed = new[] { Row(Mail, GearState.Owned), Row(Plate), Row(Blade, GearState.Owned) };

        Assert.AreEqual(new BotAction(BotActionKind.Gear, "weapons_sword"), ShopBot.Decide(View(Places(LdPlaceKind.Armoury, gear: armoured), cleared: 1), 0f));
        Assert.AreEqual(new BotAction(BotActionKind.Errand, "Yard"), ShopBot.Decide(View(Places(LdPlaceKind.Armoury, gear: armed), cleared: 1), 0f));
        Assert.AreEqual(new BotAction(BotActionKind.Engine, "mangonel", 0), ShopBot.Decide(View(Places(LdPlaceKind.Yard, gear: armed), cleared: 1), 0f));
    }

    [TestMethod]
    public void Decide_WithAnEngineBoughtOrLost_WantsNoSecondEngine()
    {
        var armed = new[] { Row(Mail, GearState.Owned), Row(Blade, GearState.Owned) };
        var taken = new[] { new SiteRow(Site0, SiteState.Taken, "mangonel", new[] { MangonelRow }), Free(Site1) };
        var lost = new[] { new SiteRow(Site0, SiteState.Lost, "mangonel", new[] { MangonelRow }), Free(Site1) };

        Assert.AreEqual(BotActionKind.Wait, ShopBot.Decide(View(Places(LdPlaceKind.Yard, gear: armed, sites: taken), cleared: 2), 0f).Kind);
        Assert.AreEqual(BotActionKind.Wait, ShopBot.Decide(View(Places(LdPlaceKind.Yard, gear: armed, sites: lost), cleared: 2), 0f).Kind);
    }

    [TestMethod]
    public void Decide_WithAnErrandOpen_WaitsForTheLord_AndNeverSoundsTheHorn()
    {
        var walking = Places(errand: LdPlaceKind.Armoury);

        Assert.AreEqual(BotActionKind.Wait, ShopBot.Decide(View(walking), 45f).Kind, "past the 30 s at which the bot horns");
        Assert.AreEqual(new BotAction(BotActionKind.Gear, "armour_1"), ShopBot.Decide(View(walking with { Near = LdPlaceKind.Armoury }), 45f));
    }

    [TestMethod]
    public void Decide_WithAnErrandOpen_AndNothingWantedThereAnyMore_GivesItUp()
    {
        var walking = Places(errand: LdPlaceKind.Armoury);

        Assert.AreEqual(BotActionKind.EndErrand, ShopBot.Decide(View(walking, gold: 100), 5f).Kind, "the gold went elsewhere");
        Assert.AreEqual(BotActionKind.EndErrand, ShopBot.Decide(View(walking with { OwnBody = false }), 5f).Kind, "the lord fell and took a soldier");
    }

    [TestMethod]
    public void Decide_AnErrandThatCannotFitBeforeTheWave_IsNotStarted()
    {
        Assert.AreEqual(BotActionKind.Errand, ShopBot.Decide(View(Places(), secondsLeft: 20f), 0f).Kind);
        Assert.AreEqual(BotActionKind.Wait, ShopBot.Decide(View(Places(), secondsLeft: 19.9f), 0f).Kind, "the walk budget is 20 s, so this errand moves to the next clear");
    }

    [TestMethod]
    public void Decide_APlaceAnErrandFailedToReach_IsLeftAlone_AndTheOtherIsStillWalked()
    {
        var armed = new[] { Row(Mail), Row(Blade) };

        Assert.AreEqual(new BotAction(BotActionKind.Errand, "Yard"), ShopBot.Decide(View(Places(gear: armed, unreached: LdPlaceKind.Armoury), cleared: 1), 0f));
        Assert.AreEqual(BotActionKind.Wait, ShopBot.Decide(View(Places(gear: armed, unreached: new[] { LdPlaceKind.Armoury, LdPlaceKind.Yard }), cleared: 1), 0f).Kind);
    }

    [TestMethod]
    public void Decide_InATakenSoldier_WalksToNoArmoury_AndStillBuysAnEngine()
    {
        Assert.AreEqual(new BotAction(BotActionKind.Errand, "Yard"), ShopBot.Decide(View(Places(ownBody: false), cleared: 1), 0f));
        Assert.AreEqual(BotActionKind.Wait, ShopBot.Decide(View(Places(ownBody: false)), 0f).Kind);
    }

    [TestMethod]
    public void Decide_WithNoBodyOnTheField_WalksNowhere()
    {
        var down = Places(ownBody: false) with { LordStands = false };

        Assert.AreEqual(BotActionKind.Wait, ShopBot.Decide(View(down, cleared: 1), 0f).Kind, "the yard needs no own body, but it needs a body");
        Assert.AreEqual(BotActionKind.Horn, ShopBot.Decide(View(down, cleared: 1), 30f).Kind);
    }

    [TestMethod]
    public void Decide_WithNoStartGold_BuysItsPacksBeforeItWalks()
    {
        var packs = new[] { new ShopOffer("militia", "Militia", "m", FormationSlot.Infantry, 12, 180) };

        Assert.AreEqual(new BotAction(BotActionKind.Buy, "militia"), ShopBot.Decide(View(Places(), startGold: false, packs: packs), 0f));
        Assert.AreEqual(BotActionKind.Errand, ShopBot.Decide(View(Places(), startGold: true, packs: packs), 0f).Kind, "a start_gold run walks first");
        Assert.AreEqual(BotActionKind.Errand, ShopBot.Decide(View(Places(), gold: 300, startGold: false, packs: new[] { packs[0] with { Price = 400 } }), 0f).Kind, "what the packs left buys the set");
    }

    [TestMethod]
    public void Decide_WithNoPlaceOnSale_DecidesAsBefore()
    {
        var view = View(LocationsView.None, cleared: 1);

        Assert.AreEqual(BotActionKind.Wait, ShopBot.Decide(view, 10f).Kind);
        Assert.AreEqual(BotActionKind.Horn, ShopBot.Decide(view, 30f).Kind);
    }

    // ---- the replays ----

    private static readonly MusterFrame ArmouryAt = new(10f, 10f, 5f, 0f, 1f);
    private static readonly MusterFrame YardAt = new(60f, 10f, 5f, 0f, 1f);

    private static RunPlan Plan(int startGold = 5000) =>
        new(
            "helms",
            new[] { new GarrisonRow("fow_militia", FormationSlot.Infantry, 30) },
            new WavePlan(new[] { new WaveSpec(20, new[] { new WaveTroop("orc", FormationSlot.Infantry, 3) }) }, new WaveScaling(10, 300)),
            new ShopPlan(600, Array.Empty<ShopOffer>(), null,
                new LocationsPlan(new ArmouryPlan(4f, new[] { Mail, Plate, Blade }), new YardPlan(6f, new[] { MangonelRow }))),
            RunRules.Default with { PreparationSeconds = 90f, IntermissionSeconds = 60f },
            new RunOptions { Seed = 1UL, StartGold = startGold, Autoplay = true })
        {
            Bounties = new Dictionary<string, int> { ["orc"] = 4 },
            Locations = new LocationOffer(true, true, true),
            ArmouryPoint = ArmouryAt,
            YardPoint = YardAt,
            Loadout = new LdLoadout("sword", false, new[] { new LdKitSlot("Item0", "kit_sword"), new LdKitSlot("Body", "kit_tunic") }),
        };

    /// <summary>A run driven by the bot, one second a tick, with a lord who reaches a place <c>WalkSeconds</c> after the errand's start (never, when null) and at once on a teleport.</summary>
    private sealed class Replay
    {
        private readonly SurvivalRun _run;
        private static readonly LdPoint Post = new(300f, 300f, 5f);
        private LdPoint _lordAt = Post;
        private LdPlace? _goal;
        private float _walked;
        private float _clock;
        private RunPhase _phase = RunPhase.Preparation;

        public Replay(RunPlan plan)
        {
            _run = new SurvivalRun(plan);
            _run.Begin();
            _run.SetSites(new[] { Site0, Site1 });
        }

        public float? WalkSeconds { get; set; } = 8f;

        public SurvivalRun Run => _run;

        /// <summary>What the bot did, in order, waits left out: the kind with what it acted on.</summary>
        public List<string> Log { get; } = new();

        public void Step()
        {
            var view = _run.GetView();
            if (view.Phase != _phase)
            {
                _phase = view.Phase;
                _clock = 0f;
            }

            _clock += 1f;
            var action = ShopBot.Decide(view, _clock);
            switch (action.Kind)
            {
                case BotActionKind.Errand: _run.TryStartErrand((LdPlaceKind)Enum.Parse(typeof(LdPlaceKind), action.OfferId!)); break;
                case BotActionKind.EndErrand: _run.EndErrand(); break;
                case BotActionKind.Gear: Assert.AreEqual(LocationResult.Ok, _run.TryBuyGear(action.OfferId!)); break;
                case BotActionKind.Engine: Assert.AreEqual(LocationResult.Ok, _run.TryBuyEngine(action.OfferId!, action.Site)); break;
                case BotActionKind.Horn: _run.TryStartWave(); break;
            }

            if (action.Kind != BotActionKind.Wait)
                Log.Add(action.Kind + (action.OfferId != null ? ":" + action.OfferId : "") + (action.Site >= 0 ? "@" + action.Site : ""));

            var lord = LdLordFacts.None with { Present = true, Own = true, At = _lordAt };
            var fx = _run.Tick(new RunSnapshot(1f, 0, 0, 40, 0, 0, 1000) { Lord = lord });
            Walk(fx.Errand);
        }

        private void Walk(ErrandOrder? order)
        {
            if (order?.Step == ErrandStep.Start)
            {
                _goal = order.At;
                _walked = 0f;
            }
            else if (order?.Step == ErrandStep.Teleport)
            {
                _lordAt = order.At!.Point;
                _goal = null;
            }
            else if (order?.Step == ErrandStep.End)
            {
                // Released, the AI lord goes back to his formation.
                _goal = null;
                _lordAt = Post;
            }

            if (_goal == null || WalkSeconds == null)
                return;

            _walked += 1f;
            if (_walked >= WalkSeconds)
            {
                _lordAt = _goal.Point;
                _goal = null;
            }
        }

        public void Until(Func<bool> done, string what)
        {
            for (var i = 0; i < 2000 && !done(); i++)
                Step();

            Assert.IsTrue(done(), "the replay never reached: " + what);
        }
    }

    [TestMethod]
    public void Replay_WithStartGold_BuysTheArmourInThePreparation_ThenTheWeaponsAndAMangonelAtClearOne()
    {
        var replay = new Replay(Plan());

        replay.Until(() => replay.Run.WavesCleared >= 1 && replay.Run.Phase == RunPhase.Wave, "the second wave");

        CollectionAssert.AreEqual(
            new[] { "Errand:Armoury", "Gear:armour_1", "Horn", "Errand:Armoury", "Gear:weapons_sword", "Errand:Yard", "Engine:mangonel@0", "Horn" },
            replay.Log.ToArray());
        var bought = replay.Run.FortSummary().Purchases;
        CollectionAssert.AreEqual(new[] { 1, 2, 2 }, bought.Select(p => p.BeforeWave).ToArray(), "the armour before wave 1, the rest before wave 2: what the Slice 6 and 8 proof runs read");
        Assert.IsTrue(bought.All(p => p.Reach == ErrandReach.Walk));
        CollectionAssert.AreEqual(new[] { "armour_1", "weapons_sword" }, bought.Where(p => p.Kind == PurchaseKind.GearSet).Select(p => p.Id).ToArray());
    }

    [TestMethod]
    public void Replay_TheHornWaitsForTheErrand_AndSoundsAfterTheBuy()
    {
        var replay = new Replay(Plan()) { WalkSeconds = 15f };

        replay.Until(() => replay.Run.Phase == RunPhase.Wave, "the first wave");

        CollectionAssert.AreEqual(new[] { "Errand:Armoury", "Gear:armour_1", "Horn" }, replay.Log.ToArray());
    }

    [TestMethod]
    public void Replay_ALordWhoNeverArrives_IsTeleported_AndTheReportNamesIt()
    {
        var replay = new Replay(Plan()) { WalkSeconds = null };

        replay.Until(() => replay.Run.Phase == RunPhase.Wave, "the first wave");

        var bought = replay.Run.FortSummary().Purchases.Single();
        Assert.AreEqual((PurchaseKind.GearSet, ErrandReach.Teleport), (bought.Kind, bought.Reach));
    }
}
