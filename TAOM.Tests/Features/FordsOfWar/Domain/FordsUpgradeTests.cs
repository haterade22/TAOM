using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.FordsOfWar.Domain;
using static TAOM.Tests.Features.FordsOfWar.Domain.FordsMatchFixture;

namespace TAOM.Tests.Features.FordsOfWar.Domain;

/// <summary>Gold buys levels of upgrades for the whole army: their prices, what they change, and how the AI buys them.</summary>
[TestClass]
public class FordsUpgradeTests
{
    private const string Melee = "melee_damage";
    private const string Missile = "missile_damage";
    private const string MeleeArmour = "melee_armour";
    private const string MissileArmour = "missile_armour";
    private const string Siege = "siegecraft";
    private const string Fortify = "fortify";

    private static readonly FordsAiProfile Hard = new("hard", 5f, 1.2f, 0f, 100000);
    private static readonly FordsAiProfile Spender = new("spender", 5f, 1f, 0f, 100);

    private static IReadOnlyList<UpgradeTrack> Tracks => FordsMatchConfig.Default.UpgradesOrDefault;

    private static FordsMatch Match(int gold = 5000, IReadOnlyDictionary<string, float>? westCost = null, IReadOnlyList<string>? eastCycle = null)
    {
        var factions = Factions().ToDictionary(e => e.Key, e => e.Value);
        factions[MatchSide.West] = factions[MatchSide.West] with { UpgradeCost = westCost };
        factions[MatchSide.East] = factions[MatchSide.East] with { AiBuyCycle = eastCycle };
        return new FordsMatch(Config(c => c with { Economy = c.Economy with { StartingGold = gold } }), Layout(), factions, Slots("slow"), new SeededFordsRandom(7));
    }

    private static UpgradeBook Book(IReadOnlyDictionary<string, float>? westCost = null)
    {
        var factions = Factions().ToDictionary(e => e.Key, e => e.Value);
        factions[MatchSide.West] = factions[MatchSide.West] with { UpgradeCost = westCost };
        return new UpgradeBook(Tracks, factions);
    }

    private static MatchView.UpgradeView ViewOf(FordsMatch match, int slot, string id) =>
        match.GetView(slot).Upgrades!.Single(u => u.Id == id);

    private static TowerHit Blow(MatchSide attacker) =>
        new(attacker == MatchSide.West ? FordsTestLayout.EastOuter0 : FordsTestLayout.WestOuter0, false, UnitRole.Infantry, 100, attacker);

    [TestMethod]
    public void Default_HasTheSixTracks_WithThreeLevelsEach()
    {
        CollectionAssert.AreEqual(new[] { Melee, Missile, MeleeArmour, MissileArmour, Siege, Fortify }, Tracks.Select(t => t.Id).ToArray());
        Assert.IsTrue(Tracks.All(t => t.Prices.SequenceEqual(new[] { 300, 600, 1000 })));
    }

    [TestMethod]
    public void NextPrice_WalksTheLevels_ThenThereIsNone()
    {
        var book = Book();

        Assert.AreEqual(300, book.NextPrice(MatchSide.West, Melee));
        Assert.IsTrue(book.TryRaise(MatchSide.West, Melee));
        Assert.AreEqual(600, book.NextPrice(MatchSide.West, Melee));
        Assert.IsTrue(book.TryRaise(MatchSide.West, Melee));
        Assert.IsTrue(book.TryRaise(MatchSide.West, Melee));

        Assert.AreEqual(3, book.Level(MatchSide.West, Melee));
        Assert.IsNull(book.NextPrice(MatchSide.West, Melee));
        Assert.IsFalse(book.TryRaise(MatchSide.West, Melee));
        Assert.AreEqual(0, book.Level(MatchSide.East, Melee));
    }

    [TestMethod]
    public void NextPrice_ScalesWithTheFactionsCost_RoundedToFive()
    {
        var book = Book(new Dictionary<string, float> { [Melee] = 0.75f });

        Assert.AreEqual(225, book.NextPrice(MatchSide.West, Melee));
        Assert.AreEqual(300, book.NextPrice(MatchSide.East, Melee));
        Assert.AreEqual(300, book.NextPrice(MatchSide.West, Missile));
    }

    [TestMethod]
    public void Factor_DamageRises_ArmourFalls_ForTheOwnerOnly()
    {
        var book = Book();
        book.TryRaise(MatchSide.West, Melee);
        book.TryRaise(MatchSide.West, Melee);
        book.TryRaise(MatchSide.West, MissileArmour);

        Assert.AreEqual(1.2f, book.CombatFactors(MatchSide.West).Attack(false), 0.001f);
        Assert.AreEqual(1f, book.CombatFactors(MatchSide.West).Attack(true), 0.001f);
        Assert.AreEqual(1f, book.CombatFactors(MatchSide.West).Defence(false), 0.001f);
        Assert.AreEqual(0.92f, book.CombatFactors(MatchSide.West).Defence(true), 0.001f);
        Assert.AreEqual(1f, book.CombatFactors(MatchSide.East).Attack(false), 0.001f);
    }

    [TestMethod]
    public void Factor_ArmourNeverStopsAllDamage()
    {
        var tracks = new List<UpgradeTrack> { new(MeleeArmour, "{=x}Armour", UpgradeEffect.MeleeArmour, 0.5f, new[] { 1, 1, 1 }) };
        var book = new UpgradeBook(tracks, Factions());
        for (var i = 0; i < 3; i++)
            book.TryRaise(MatchSide.West, MeleeArmour);

        Assert.AreEqual(0.1f, book.CombatFactors(MatchSide.West).Defence(false), 0.001f);
    }

    [TestMethod]
    public void TryBuyUpgrade_Pays_RaisesTheLevel_AndTellsTheFeed()
    {
        var match = Match();
        var gold = match.GetView(WestSlot).Gold;

        Assert.AreEqual(CommandResult.Ok, match.TryBuyUpgrade(WestSlot, Melee));

        Assert.AreEqual(gold - 300, match.GetView(WestSlot).Gold);
        Assert.AreEqual(1, ViewOf(match, WestSlot, Melee).Level);
        Assert.AreEqual(600, ViewOf(match, WestSlot, Melee).NextPrice);
        Assert.AreEqual(1.1f, match.CombatUpgrades(MatchSide.West).Attack(false), 0.001f);
        var e = match.Tick(Snap(0.1f)).Events.Single(x => x.Kind == MatchEventKind.UpgradeBought);
        Assert.AreEqual(MatchSide.West, e.Side);
        Assert.AreEqual(Tracks[0].Name, e.Subject);
        Assert.AreEqual(300, e.Amount);
    }

    [TestMethod]
    public void TryBuyUpgrade_WithoutTheGold_IsRefused_AndChangesNothing()
    {
        var match = Match(gold: 299);

        Assert.AreEqual(CommandResult.NotEnoughGold, match.TryBuyUpgrade(WestSlot, Melee));
        Assert.AreEqual(299, match.GetView(WestSlot).Gold);
        Assert.AreEqual(0, ViewOf(match, WestSlot, Melee).Level);
        Assert.IsFalse(ViewOf(match, WestSlot, Melee).CanAfford);
    }

    [TestMethod]
    public void TryBuyUpgrade_AtTheTopLevel_IsRefused()
    {
        var match = Match();
        for (var i = 0; i < 3; i++)
            Assert.AreEqual(CommandResult.Ok, match.TryBuyUpgrade(WestSlot, Melee));
        var gold = match.GetView(WestSlot).Gold;

        Assert.AreEqual(CommandResult.UpgradeComplete, match.TryBuyUpgrade(WestSlot, Melee));
        Assert.AreEqual(gold, match.GetView(WestSlot).Gold);
        Assert.IsNull(ViewOf(match, WestSlot, Melee).NextPrice);
    }

    [TestMethod]
    public void TryBuyUpgrade_AnUnknownTrackOrSlot_IsRefused()
    {
        var match = Match();

        Assert.AreEqual(CommandResult.UnknownOffer, match.TryBuyUpgrade(WestSlot, "not_a_track"));
        Assert.AreEqual(CommandResult.LaneNotOwned, match.TryBuyUpgrade(99, Melee));
    }

    [TestMethod]
    public void Siegecraft_RaisesTheBuyersDamageToTowers()
    {
        var match = Match();
        var before = match.AdjustTowerDamage(Blow(MatchSide.West), out _);

        match.TryBuyUpgrade(WestSlot, Siege);

        Assert.AreEqual((int)System.Math.Round(before * 1.15f), match.AdjustTowerDamage(Blow(MatchSide.West), out _));
        Assert.AreEqual(before, match.AdjustTowerDamage(Blow(MatchSide.East), out _), "the other side's blows are unchanged");
    }

    [TestMethod]
    public void Fortify_LowersTheDamageTheBuyersTowersTake()
    {
        var match = Match();
        var before = match.AdjustTowerDamage(Blow(MatchSide.East), out _);

        match.TryBuyUpgrade(WestSlot, Fortify);

        Assert.AreEqual((int)System.Math.Round(before * 0.9f), match.AdjustTowerDamage(Blow(MatchSide.East), out _));
        Assert.AreEqual(before, match.AdjustTowerDamage(Blow(MatchSide.West), out _), "the other side's towers are unchanged");
    }

    private static List<AiAction> Decide(FordsMatch match, int decisions)
    {
        var ai = new AiCommander(EastSlot, Hard, new SeededFordsRandom(3));
        var actions = new List<AiAction>();
        for (var i = 1; i <= decisions; i++)
            actions.AddRange(ai.Decide(match.GetView(EastSlot), i * 10f));
        return actions;
    }

    [TestMethod]
    public void Decide_AnUpgradeEntry_BuysThatUpgrade_ThenMovesOn()
    {
        var offer = Factions()[MatchSide.East].Units[0].OfferId;
        var match = Match(eastCycle: new[] { "up:" + Melee, offer });

        var actions = Decide(match, 2).Where(a => a.Kind == AiActionKind.BuyUpgrade || a.Kind == AiActionKind.BuyPack).ToList();

        Assert.AreEqual(AiActionKind.BuyUpgrade, actions[0].Kind);
        Assert.AreEqual(Melee, actions[0].Id);
        Assert.AreEqual(AiActionKind.BuyPack, actions[1].Kind);
    }

    [TestMethod]
    public void Decide_TheAssaultLaneIsFull_StillBuysTheUpgradeDue()
    {
        var factions = Factions().ToDictionary(e => e.Key, e => e.Value);
        factions[MatchSide.East] = factions[MatchSide.East] with { AiBuyCycle = new[] { "up:" + Melee } };
        var config = Config(c => c with { Economy = c.Economy with { StartingGold = 5000 }, Lanes = c.Lanes with { AssaultThreshold = 0 } });
        var match = new FordsMatch(config, Layout(), factions, Slots("slow"), new SeededFordsRandom(7));

        Assert.AreEqual(Melee, Decide(match, 1).Single(a => a.Kind == AiActionKind.BuyUpgrade).Id);
    }

    private static FordsMatch LaneMatch(IReadOnlyList<string> cycle, int gold, bool full)
    {
        var factions = Factions().ToDictionary(e => e.Key, e => e.Value);
        factions[MatchSide.East] = factions[MatchSide.East] with { AiBuyCycle = cycle };
        var config = Config(c => c with { Economy = c.Economy with { StartingGold = gold }, Lanes = c.Lanes with { AssaultThreshold = full ? 0 : 1000 } });
        return new FordsMatch(config, Layout(), factions, Slots("slow"), new SeededFordsRandom(7));
    }

    private static List<AiAction> Spent(AiCommander ai, FordsMatch match, float now) =>
        ai.Decide(match.GetView(EastSlot), now).Where(a => a.Kind == AiActionKind.BuyUpgrade || a.Kind == AiActionKind.BuyPack).ToList();

    [TestMethod]
    public void Decide_TheAssaultLaneIsFull_SpareGoldBuysTheNextUpgradeInTheCycle()
    {
        var offer = Factions()[MatchSide.East].Units[0].OfferId;
        var ai = new AiCommander(EastSlot, Spender, new SeededFordsRandom(3));

        var spent = Spent(ai, LaneMatch(new[] { offer, "up:" + Missile, "up:" + Melee }, 5000, full: true), 10f);

        Assert.AreEqual(AiActionKind.BuyUpgrade, spent.Single().Kind);
        Assert.AreEqual(Missile, spent.Single().Id);
    }

    [TestMethod]
    public void Decide_TheAssaultLaneIsFull_ASpareUpgrade_KeepsTheCyclesPlaceForWhenTheLaneOpens()
    {
        var offer = Factions()[MatchSide.East].Units[0].OfferId;
        var cycle = new[] { offer, "up:" + Melee };
        var ai = new AiCommander(EastSlot, Spender, new SeededFordsRandom(3));

        Spent(ai, LaneMatch(cycle, 5000, full: true), 10f);
        var next = Spent(ai, LaneMatch(cycle, 5000, full: false), 20f);

        Assert.AreEqual(AiActionKind.BuyPack, next.Single().Kind);
        Assert.AreEqual(offer, next.Single().Id);
    }

    [TestMethod]
    public void Decide_TheAssaultLaneIsFull_GoldInsideTheReserve_BuysNoUpgrade()
    {
        var offer = Factions()[MatchSide.East].Units[0].OfferId;
        var ai = new AiCommander(EastSlot, Spender, new SeededFordsRandom(3));

        Assert.AreEqual(0, Spent(ai, LaneMatch(new[] { offer, "up:" + Melee }, 400, full: true), 10f).Count, "300 for the level and 100 of reserve leave nothing spare");

        Assert.AreEqual(Melee, Spent(ai, LaneMatch(new[] { offer, "up:" + Melee }, 401, full: true), 20f).Single().Id);
    }

    [TestMethod]
    public void Decide_TheAssaultLaneIsFull_ACompleteUpgrade_IsPassedOverForTheNext()
    {
        var offer = Factions()[MatchSide.East].Units[0].OfferId;
        var match = LaneMatch(new[] { offer, "up:" + Melee, "up:" + Missile }, 5000, full: true);
        for (var i = 0; i < 3; i++)
            match.TryBuyUpgrade(EastSlot, Melee);
        var ai = new AiCommander(EastSlot, Spender, new SeededFordsRandom(3));

        Assert.AreEqual(Missile, Spent(ai, match, 10f).Single().Id);
    }

    [TestMethod]
    public void Decide_TheAssaultLaneIsFull_ACycleWithNoUpgrade_BuysNothing()
    {
        var offer = Factions()[MatchSide.East].Units[0].OfferId;
        var ai = new AiCommander(EastSlot, Spender, new SeededFordsRandom(3));

        Assert.AreEqual(0, Spent(ai, LaneMatch(new[] { offer }, 5000, full: true), 10f).Count);
    }

    [TestMethod]
    public void Decide_TheLaneHasRoom_StillSavesForThePackDue_AndBuysNoUpgradeAhead()
    {
        var dearest = Factions()[MatchSide.East].Units.OrderByDescending(o => o.PackPrice).First();
        var ai = new AiCommander(EastSlot, Spender, new SeededFordsRandom(3));

        Assert.AreEqual(0, Spent(ai, LaneMatch(new[] { dearest.OfferId, "up:" + Melee }, dearest.PackPrice - 1, full: false), 10f).Count(a => a.Kind == AiActionKind.BuyUpgrade));
    }

    [TestMethod]
    public void Decide_AnUpgradeThatCostsTooMuch_SavesForIt()
    {
        var match = Match(gold: 299, eastCycle: new[] { "up:" + Melee, Factions()[MatchSide.East].Units[0].OfferId });

        Assert.AreEqual(0, Decide(match, 1).Count(a => a.Kind == AiActionKind.BuyUpgrade || a.Kind == AiActionKind.BuyPack));
    }

    [TestMethod]
    public void Decide_ACompleteUpgrade_IsSkipped()
    {
        var offer = Factions()[MatchSide.East].Units[0].OfferId;
        var match = Match(eastCycle: new[] { "up:" + Melee, offer });
        for (var i = 0; i < 3; i++)
            match.TryBuyUpgrade(EastSlot, Melee);

        var bought = Decide(match, 1).Where(a => a.Kind == AiActionKind.BuyUpgrade || a.Kind == AiActionKind.BuyPack).ToList();

        Assert.AreEqual(AiActionKind.BuyPack, bought.Single().Kind);
        Assert.AreEqual(offer, bought.Single().Id);
    }
}
