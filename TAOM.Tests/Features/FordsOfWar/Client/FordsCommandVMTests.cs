using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Adapters;
using TAOM.Client.Features.FordsOfWar;
using TAOM.Core.Logging;
using TAOM.Features.FordsOfWar;
using TAOM.Features.FordsOfWar.Diagnostics;
using TAOM.Features.FordsOfWar.Domain;
using TAOM.Tests.Features.FordsOfWar.Domain;
using static TAOM.Tests.Features.FordsOfWar.Domain.FordsMatchFixture;

namespace TAOM.Tests.Features.FordsOfWar.Client;

/// <summary>
/// The command panel's view model against a real <see cref="FordsSession"/> over the small two-lane fixture match: gold 1000, no
/// income, offers inf 100, spear 120, archer 150, troll 400, elite 1500 (never affordable), heroes hero_a 700 and hero_b 400.
/// </summary>
[TestClass]
public class FordsCommandVMTests
{
    private int _exits;

    private FordsSession Session(out FordsMatch match, FordsMatchConfig? config = null)
    {
        var battlefield = Substitute.For<IFordsBattlefieldAdapter>();
        battlefield.CharacterName(Arg.Any<string>()).Returns(call => "Name_" + call.Arg<string>());
        match = Create(config);
        var session = new FordsSession();
        session.Fill(new object(), match, WestSlot, battlefield, new Vec3f(0f, 25f, 45f), new Vec3f(1000f, 25f, 0f), new List<Vec3f>());
        return session;
    }

    private FordsCommandVM Panel(FordsSession session)
    {
        var vm = new FordsCommandVM(session, () => _exits++);
        vm.Refresh();
        return vm;
    }

    private static FordsMatchConfig WithGold(int gold) => Config(c => c with { Economy = c.Economy with { StartingGold = gold } });

    private static readonly FordsMatchSetup ReportSetup = new("scene_a", "gondor", "mordor", "hard", 2);

    private static FordsRunReport NewReport()
    {
        var report = new FordsRunReport(Substitute.For<IModLogger>());
        report.BeginRun("menu", ReportSetup);
        return report;
    }

    private static int Seen(FordsRunReport report, string key) =>
        report.Run!.Client.Commands.TryGetValue(key, out var n) ? n : 0;

    [TestMethod]
    public void Commands_ReportActionAndResult()
    {
        var report = NewReport();
        var session = Session(out var match);
        var vm = new FordsCommandVM(session, () => _exits++, report);
        vm.Refresh();

        vm.BuyOffer(0);
        vm.SetOrder(LaneOrder.Hold);
        vm.FieldFirstAffordableHero();
        var spawn = match.Tick(Snap(1f)).Spawns.Single(s => s.IsHero);
        match.RegisterUnit(spawn.RequestId, Unit(50, MatchSide.West, WestSlot, role: UnitRole.Hero, heroId: "hero_a"));
        match.OnUnitKilled(Key(50), null);
        vm.Refresh();
        vm.Heroes[0].ExecuteAct();

        Assert.AreEqual(1, Seen(report, "BuyPack:Ok"));
        Assert.AreEqual(1, Seen(report, "Order:Hold:Ok"));
        Assert.AreEqual(1, Seen(report, "BuyHero:Ok"));
        Assert.AreEqual(1, Seen(report, "Buyback:NotEnoughGold"));
    }

    [TestMethod]
    public void OwnPackBoughtFeedLine_IsReported()
    {
        var report = NewReport();
        var session = Session(out _);
        session.PushEvents(new[]
        {
            new MatchEvent(MatchEventKind.PackBought, MatchSide.East, "inf", 100),
            new MatchEvent(MatchEventKind.PackBought, MatchSide.West, "inf", 100),
        });
        var vm = new FordsCommandVM(session, () => _exits++, report);

        vm.Refresh();

        Assert.AreEqual(1, report.Run!.Client.PackBoughtLines);
    }

    [TestMethod]
    public void NullReport_ChangesNothing()
    {
        var session = Session(out _);
        var vm = new FordsCommandVM(session, () => _exits++, null);
        vm.Refresh();

        vm.BuyOffer(0);

        Assert.AreEqual(900, session.GetView()!.Gold);
        Assert.AreEqual(string.Empty, vm.NoticeText);
    }

    [TestMethod]
    public void Refresh_WithNoMatch_HidesThePanel()
    {
        var vm = new FordsCommandVM(new FordsSession(), () => _exits++);

        vm.Refresh();

        Assert.IsFalse(vm.IsVisible);
        Assert.AreEqual(0, vm.Offers.Count);
        Assert.IsFalse(vm.IsResultVisible);
    }

    [TestMethod]
    public void Refresh_WithAMatch_ShowsThePanelAndTheGold()
    {
        var vm = Panel(Session(out _));

        Assert.IsTrue(vm.IsVisible);
        Assert.AreEqual("Gold: 1000", vm.GoldText);
    }

    [TestMethod]
    public void Refresh_ListsEveryUnitOfferWithItsKeyNamePackAndPrice()
    {
        var vm = Panel(Session(out _));

        Assert.AreEqual(5, vm.Offers.Count);
        var first = vm.Offers[0];
        Assert.AreEqual("F1", first.KeyText);
        Assert.AreEqual("Name_footman", first.Name);
        Assert.AreEqual("Pack of 10", first.PackText);
        Assert.AreEqual("100 gold", first.PriceText);
        Assert.AreEqual("F4", vm.Offers[3].KeyText);
        Assert.AreEqual("Name_cave_troll", vm.Offers[3].Name);
    }

    [TestMethod]
    public void Refresh_OffersBeyondTheGold_AreNotAffordable()
    {
        var vm = Panel(Session(out _));

        Assert.IsTrue(vm.Offers.Take(4).All(o => o.CanAfford));
        Assert.IsFalse(vm.Offers[4].CanAfford, "the 1500 gold elite pack against 1000 gold");
    }

    [TestMethod]
    public void Refresh_WithNoDisplayName_ShowsTheTroopId()
    {
        var battlefield = Substitute.For<IFordsBattlefieldAdapter>();
        battlefield.CharacterName(Arg.Any<string>()).Returns(string.Empty);
        var session = new FordsSession();
        session.Fill(new object(), Create(), WestSlot, battlefield, default, default, new List<Vec3f>());

        var vm = Panel(session);

        Assert.AreEqual("footman", vm.Offers[0].Name);
    }

    [TestMethod]
    public void Refresh_OffersBeyondTheEightHotkeys_ShowNoKey()
    {
        var units = Enumerable.Range(0, 10).Select(i => new UnitOffer("o" + i, "t" + i, UnitRole.Infantry, 5, 50)).ToList();
        var factions = Factions().ToDictionary(p => p.Key, p => p.Value with { Units = units });
        var match = new FordsMatch(Config(), Layout(), factions, Slots(), new SeededFordsRandom(3));
        var session = new FordsSession();
        session.Fill(new object(), match, WestSlot, Substitute.For<IFordsBattlefieldAdapter>(), default, default, new List<Vec3f>());

        var vm = Panel(session);

        Assert.AreEqual(10, vm.Offers.Count);
        Assert.AreEqual("F8", vm.Offers[7].KeyText);
        Assert.AreEqual(string.Empty, vm.Offers[8].KeyText);
    }

    [TestMethod]
    public void BuyOffer_CallsTheSessionAndTheGoldDrops()
    {
        var session = Session(out _);
        var vm = Panel(session);

        vm.BuyOffer(0);

        Assert.AreEqual(900, session.GetView()!.Gold);
        Assert.AreEqual("Gold: 900", vm.GoldText);
        Assert.AreEqual(string.Empty, vm.NoticeText);
    }

    [TestMethod]
    public void BuyOffer_ThroughTheOfferButton_BuysTheSamePack()
    {
        var session = Session(out _);
        var vm = Panel(session);

        vm.Offers[1].ExecuteBuy();

        Assert.AreEqual(880, session.GetView()!.Gold);
    }

    [TestMethod]
    public void BuyOffer_BeyondTheGold_ShowsTheRefusalAndSpendsNothing()
    {
        var session = Session(out _);
        var vm = Panel(session);

        vm.BuyOffer(4);

        Assert.AreEqual(1000, session.GetView()!.Gold);
        Assert.AreEqual("Not enough gold.", vm.NoticeText);
    }

    [TestMethod]
    public void BuyOffer_PastTheLastOffer_IsIgnored()
    {
        var session = Session(out _);
        var vm = Panel(session);

        vm.BuyOffer(12);
        vm.BuyOffer(-1);

        Assert.AreEqual(1000, session.GetView()!.Gold);
    }

    [TestMethod]
    public void BuyOffer_AfterANewPurchase_ClearsTheOldRefusal()
    {
        var vm = Panel(Session(out _));
        vm.BuyOffer(4);

        vm.BuyOffer(0);

        Assert.AreEqual(string.Empty, vm.NoticeText);
    }

    [TestMethod]
    public void BuyOffer_ToAFullQueue_ShowsTheQueueRefusalAndAnotherLaneStillWorks()
    {
        var session = Session(out _, Config(c => c with { Economy = c.Economy with { StartingGold = 5000 } }));
        var vm = Panel(session);
        for (var i = 0; i < 3; i++)
            vm.BuyOffer(0);

        vm.BuyOffer(0);
        var refusal = vm.NoticeText;
        vm.SelectLane(1);
        vm.BuyOffer(0);

        Assert.AreEqual("That lane already has packs waiting.", refusal);
        Assert.AreEqual(string.Empty, vm.NoticeText, "lane 1 has room");
        Assert.AreEqual(5000 - 400, session.GetView()!.Gold);
    }

    [TestMethod]
    public void Refresh_ListsTheOwnedLanes_WithTheFirstSelected()
    {
        var vm = Panel(Session(out _));

        Assert.AreEqual(2, vm.Lanes.Count);
        Assert.AreEqual(0, vm.SelectedLane);
        Assert.IsTrue(vm.Lanes[0].IsSelected);
        Assert.IsFalse(vm.Lanes[1].IsSelected);
        Assert.AreEqual("Lane 1", vm.Lanes[0].Title);
        Assert.AreEqual("1", vm.Lanes[0].KeyText);
        Assert.AreEqual("2", vm.Lanes[1].KeyText);
        Assert.AreEqual("0 v 0", vm.Lanes[0].CountsText);
        Assert.AreEqual("Troops for lane 1", vm.TroopsTitle);
    }

    [TestMethod]
    public void SelectLane_MovesTheSelectionAndTheTitles()
    {
        var vm = Panel(Session(out _));

        vm.SelectLane(1);

        Assert.AreEqual(1, vm.SelectedLane);
        Assert.IsFalse(vm.Lanes[0].IsSelected);
        Assert.IsTrue(vm.Lanes[1].IsSelected);
        Assert.AreEqual("Troops for lane 2", vm.TroopsTitle);
        Assert.AreEqual("Orders for lane 2", vm.OrdersTitle);
    }

    [TestMethod]
    public void SelectLane_ThroughTheLaneButton_SelectsThatLane()
    {
        var vm = Panel(Session(out _));

        vm.Lanes[1].ExecuteSelect();

        Assert.AreEqual(1, vm.SelectedLane);
    }

    [TestMethod]
    public void LaneRow_AfterUpdateWithAnotherLane_SelectsThatLane()
    {
        var picked = -1;
        var row = new FordsLaneVM(lane => picked = lane);

        row.Update(new MatchView.LaneView(2, true, LaneOrder.Push, 0f, 3, 4, 0, new List<MatchView.TowerView>()), false);
        row.ExecuteSelect();

        Assert.AreEqual(2, picked, "Update relabels the row (Lane 3), so the click must act on lane index 2");
        Assert.AreEqual("Lane 3", row.Title);
    }

    [TestMethod]
    public void SelectLane_AnUnknownLane_IsIgnored()
    {
        var vm = Panel(Session(out _));

        vm.SelectLane(3);
        vm.SelectLane(-1);

        Assert.AreEqual(0, vm.SelectedLane);
    }

    [TestMethod]
    public void Refresh_ListsThreeOrdersWithTheirKeys_PushStanding()
    {
        var vm = Panel(Session(out _));

        Assert.AreEqual(3, vm.Orders.Count);
        CollectionAssert.AreEqual(new[] { "Push", "Hold", "Fall back" }, vm.Orders.Select(o => o.Text).ToArray());
        CollectionAssert.AreEqual(new[] { "Z", "X", "C" }, vm.Orders.Select(o => o.KeyText).ToArray());
        CollectionAssert.AreEqual(new[] { true, false, false }, vm.Orders.Select(o => o.IsActive).ToArray());
        Assert.IsTrue(vm.Orders.All(o => o.CooldownText.Length == 0), "no cooldown before the first change");
    }

    [TestMethod]
    public void SetOrder_Hold_CallsTheSessionAndHoldBecomesTheStandingOrder()
    {
        var session = Session(out _);
        var vm = Panel(session);

        vm.SetOrder(LaneOrder.Hold);

        Assert.AreEqual(LaneOrder.Hold, session.GetView()!.Lanes[0].Order);
        Assert.IsTrue(vm.Orders[1].IsActive);
        Assert.IsFalse(vm.Orders[0].IsActive);
    }

    [TestMethod]
    public void SetOrder_ForTheSelectedLaneOnly()
    {
        var session = Session(out _);
        var vm = Panel(session);
        vm.SelectLane(1);

        vm.SetOrder(LaneOrder.Hold);

        Assert.AreEqual(LaneOrder.Push, session.GetView()!.Lanes[0].Order);
        Assert.AreEqual(LaneOrder.Hold, session.GetView()!.Lanes[1].Order);
    }

    [TestMethod]
    public void SetOrder_AfterAChange_ShowsTheCooldownOnTheOtherButtons()
    {
        var vm = Panel(Session(out _));

        vm.SetOrder(LaneOrder.Hold);

        Assert.AreEqual("20 s", vm.Orders[0].CooldownText);
        Assert.AreEqual("20 s", vm.Orders[2].CooldownText);
        Assert.IsFalse(vm.Orders[0].CanSet);
        Assert.IsFalse(vm.Orders[2].CanSet);
        Assert.IsTrue(vm.Orders[1].CanSet, "the standing order can be confirmed again at no cost");
        Assert.AreEqual(string.Empty, vm.Orders[1].CooldownText);
    }

    [TestMethod]
    public void SetOrder_WhileOnCooldown_ShowsTheRefusal()
    {
        var vm = Panel(Session(out _));
        vm.SetOrder(LaneOrder.Hold);

        vm.SetOrder(LaneOrder.FallBack);

        Assert.AreEqual("That order is still cooling down.", vm.NoticeText);
        Assert.IsTrue(vm.Orders[1].IsActive);
    }

    [TestMethod]
    public void SetOrder_ThroughTheOrderButton_SetsTheOrder()
    {
        var session = Session(out _);
        var vm = Panel(session);

        vm.Orders[2].ExecuteSet();

        Assert.AreEqual(LaneOrder.FallBack, session.GetView()!.Lanes[0].Order);
    }

    [TestMethod]
    public void Refresh_ListsTheHeroOffersWithTheirPrices()
    {
        var vm = Panel(Session(out _));

        Assert.AreEqual(2, vm.Heroes.Count);
        Assert.AreEqual("Name_hero_a", vm.Heroes[0].Name);
        Assert.AreEqual("Ready to field", vm.Heroes[0].StatusText);
        Assert.AreEqual("Field for 700 gold", vm.Heroes[0].ActionText);
        Assert.IsTrue(vm.Heroes[0].CanAct);
        Assert.AreEqual("Field for 400 gold", vm.Heroes[1].ActionText);
    }

    [TestMethod]
    public void Refresh_AHeroBeyondTheGold_CannotBeFielded()
    {
        var vm = Panel(Session(out _, WithGold(500)));

        Assert.IsFalse(vm.Heroes[0].CanAct, "700 gold against 500");
        Assert.IsTrue(vm.Heroes[1].CanAct, "400 gold against 500");
    }

    [TestMethod]
    public void FieldFirstAffordableHero_FieldsTheFirstHeroTheGoldReaches()
    {
        var session = Session(out _, WithGold(500));
        var vm = Panel(session);

        vm.FieldFirstAffordableHero();

        Assert.AreEqual(100, session.GetView()!.Gold, "hero_b at 400");
        Assert.AreEqual("In the field", vm.Heroes[1].StatusText);
        Assert.IsFalse(vm.Heroes[1].CanAct);
        Assert.AreEqual("Ready to field", vm.Heroes[0].StatusText);
    }

    [TestMethod]
    public void FieldFirstAffordableHero_WithTheFullPurse_FieldsTheFirstOffer()
    {
        var session = Session(out _);
        var vm = Panel(session);

        vm.FieldFirstAffordableHero();

        Assert.AreEqual(300, session.GetView()!.Gold, "hero_a at 700");
        Assert.AreEqual("In the field", vm.Heroes[0].StatusText);
    }

    [TestMethod]
    public void FieldFirstAffordableHero_WithNoneAffordable_ShowsANotice()
    {
        var session = Session(out _, WithGold(100));
        var vm = Panel(session);

        vm.FieldFirstAffordableHero();

        Assert.AreEqual(100, session.GetView()!.Gold);
        Assert.AreEqual("No hero you can afford is waiting.", vm.NoticeText);
    }

    [TestMethod]
    public void HeroButton_FieldsThatHero()
    {
        var session = Session(out _);
        var vm = Panel(session);

        vm.Heroes[1].ExecuteAct();

        Assert.AreEqual(600, session.GetView()!.Gold, "hero_b at 400");
    }

    [TestMethod]
    public void HeroButton_AfterAnEarlierHeroOfferDropsOut_ActsOnTheHeroTheRowShows()
    {
        var session = Session(out var match);
        var vm = Panel(session);
        vm.Heroes[0].ExecuteAct();
        var spawn = match.Tick(Snap(1f)).Spawns.Single(s => s.IsHero);
        match.OnSpawnFailed(spawn.RequestId, 1);

        vm.Refresh();

        Assert.AreEqual(1, vm.Heroes.Count, "hero_a's offer is gone");
        Assert.AreEqual("Name_hero_b", vm.Heroes[0].Name);
        vm.Heroes[0].ExecuteAct();
        Assert.AreEqual(600, session.GetView()!.Gold, "the row shows hero_b at 400, so the click fields hero_b");
    }

    [TestMethod]
    public void HeroButton_AtTheHeroLimit_ShowsTheLimitRefusal()
    {
        var factions = Factions().ToDictionary(p => p.Key, p => p.Value with { Heroes = p.Value.Heroes.Append(new HeroOffer("hero_c", 100)).ToList() });
        var match = new FordsMatch(WithGold(5000), Layout(), factions, Slots(), new SeededFordsRandom(3));
        var session = new FordsSession();
        session.Fill(new object(), match, WestSlot, Substitute.For<IFordsBattlefieldAdapter>(), default, default, new List<Vec3f>());
        var vm = Panel(session);
        vm.Heroes[0].ExecuteAct();
        vm.Heroes[1].ExecuteAct();

        vm.Heroes[2].ExecuteAct();

        Assert.AreEqual("You already field as many heroes as you may.", vm.NoticeText);
    }

    [TestMethod]
    public void AFallenHero_ShowsItsRespawnTimerAndTheBuybackPrice_AndTheButtonBuysItBack()
    {
        var session = Session(out var match);
        var vm = Panel(session);
        vm.FieldFirstAffordableHero();
        var spawn = match.Tick(Snap(1f)).Spawns.Single(s => s.IsHero);
        match.RegisterUnit(spawn.RequestId, Unit(50, MatchSide.West, WestSlot, role: UnitRole.Hero, heroId: "hero_a"));
        match.OnUnitKilled(Key(50), null);

        vm.Refresh();

        var left = (int)System.Math.Ceiling(session.GetView()!.Heroes.Single(h => h.HeroId == "hero_a").RespawnLeft);
        Assert.IsTrue(left > 0);
        Assert.AreEqual($"Fallen, back in {left} s", vm.Heroes[0].StatusText);
        StringAssert.StartsWith(vm.Heroes[0].ActionText, "Buy back for ");
        var price = session.GetView()!.Heroes.Single(h => h.HeroId == "hero_a").BuybackPrice;
        Assert.IsTrue(price >= 350 && price < 400, "about half of 700 a second into the match");
        Assert.AreEqual($"Buy back for {price} gold", vm.Heroes[0].ActionText);
        Assert.IsFalse(vm.Heroes[0].CanAct, "gold 300 is below the buyback price");
    }

    [TestMethod]
    public void Refresh_AHeroWhenTheSideIsAtItsHeroLimit_CannotBeFielded()
    {
        var factions = Factions().ToDictionary(p => p.Key, p => p.Value with { Heroes = p.Value.Heroes.Append(new HeroOffer("hero_c", 100)).ToList() });
        var match = new FordsMatch(WithGold(5000), Layout(), factions, Slots(), new SeededFordsRandom(3));
        var session = new FordsSession();
        session.Fill(new object(), match, WestSlot, Substitute.For<IFordsBattlefieldAdapter>(), default, default, new List<Vec3f>());
        var vm = Panel(session);
        Assert.IsTrue(vm.Heroes[2].CanAct, "a free hero slot and enough gold");

        vm.Heroes[0].ExecuteAct();
        vm.Heroes[1].ExecuteAct();

        Assert.AreEqual("Ready to field", vm.Heroes[2].StatusText);
        Assert.IsFalse(vm.Heroes[2].CanAct, "the match would answer HeroLimit, so the button must not light up");
    }

    [TestMethod]
    public void Refresh_AFallenHeroWhoseBuybackNeedsAFreeSlotTheSideLacks_CannotBeBoughtBack()
    {
        var factions = Factions().ToDictionary(p => p.Key, p => p.Value with { Heroes = p.Value.Heroes.Append(new HeroOffer("hero_c", 100)).ToList() });
        var match = new FordsMatch(WithGold(5000), Layout(), factions, Slots(), new SeededFordsRandom(3));
        var session = new FordsSession();
        session.Fill(new object(), match, WestSlot, Substitute.For<IFordsBattlefieldAdapter>(), default, default, new List<Vec3f>());
        var vm = Panel(session);
        vm.Heroes[0].ExecuteAct();
        var spawn = match.Tick(Snap(1f)).Spawns.Single(s => s.IsHero);
        match.RegisterUnit(spawn.RequestId, Unit(50, MatchSide.West, WestSlot, role: UnitRole.Hero, heroId: "hero_a"));
        match.OnUnitKilled(Key(50), null);
        vm.Refresh();
        Assert.IsTrue(vm.Heroes[0].CanAct, "fallen, gold to spare and a free slot");

        vm.Heroes[1].ExecuteAct();
        vm.Heroes[2].ExecuteAct();

        StringAssert.StartsWith(vm.Heroes[0].ActionText, "Buy back for ");
        Assert.IsFalse(vm.Heroes[0].CanAct, "the two other heroes fill the alive limit, so TryBuyback answers HeroLimit");
    }

    [TestMethod]
    public void AFallenHero_BuybackButton_CallsTheSessionBuyback()
    {
        var session = Session(out var match, Config(c => c with { Economy = c.Economy with { StartingGold = 2000 } }));
        var vm = Panel(session);
        vm.FieldFirstAffordableHero();
        var spawn = match.Tick(Snap(1f)).Spawns.Single(s => s.IsHero);
        match.RegisterUnit(spawn.RequestId, Unit(50, MatchSide.West, WestSlot, role: UnitRole.Hero, heroId: "hero_a"));
        match.OnUnitKilled(Key(50), null);
        vm.Refresh();
        var goldBefore = session.GetView()!.Gold;

        vm.Heroes[0].ExecuteAct();

        Assert.IsTrue(session.GetView()!.Gold < goldBefore, "the buyback was charged");
        Assert.AreEqual(string.Empty, vm.NoticeText);
    }

    [TestMethod]
    public void Refresh_ShowsOneTowerBarRowPerLane_WestInnerFirstThenEastOuterFirst()
    {
        var vm = Panel(Session(out _));

        Assert.AreEqual(2, vm.LaneBars.Count);
        Assert.AreEqual("Lane 1", vm.LaneBars[0].Title);
        var towers = vm.LaneBars[0].Towers;
        Assert.AreEqual(4, towers.Count);
        CollectionAssert.AreEqual(
            new[] { FordsTestLayout.WestInner0, FordsTestLayout.WestOuter0, FordsTestLayout.EastOuter0, FordsTestLayout.EastInner0 },
            towers.Select(t => t.TowerId).ToArray());
        CollectionAssert.AreEqual(new[] { true, true, false, false }, towers.Select(t => t.IsOwn).ToArray());
        Assert.IsTrue(towers.All(t => t.FillWidth == FordsTowerBarVM.FullWidth));
    }

    [TestMethod]
    public void Refresh_ADamagedTower_ShrinksItsBarInProportion()
    {
        var session = Session(out var match);
        var vm = Panel(session);
        var max = match.GetView(WestSlot).Lanes[0].Towers.Single(t => t.Id == FordsTestLayout.EastOuter0).MaxHitPoints;
        match.AdjustTowerDamage(new TowerHit(FordsTestLayout.EastOuter0, false, UnitRole.Infantry, max / 4));

        vm.Refresh();

        var bar = vm.LaneBars[0].Towers.Single(t => t.TowerId == FordsTestLayout.EastOuter0);
        Assert.AreEqual((float)System.Math.Round(FordsTowerBarVM.FullWidth * 0.75f), bar.FillWidth);
        Assert.IsFalse(bar.IsDestroyed);
    }

    [TestMethod]
    public void Refresh_AFallenTower_EmptiesItsBar()
    {
        var session = Session(out var match);
        var vm = Panel(session);
        match.OnTowerDestroyed(FordsTestLayout.WestOuter0, null);

        vm.Refresh();

        var bar = vm.LaneBars[0].Towers.Single(t => t.TowerId == FordsTestLayout.WestOuter0);
        Assert.AreEqual(0f, bar.FillWidth);
        Assert.IsTrue(bar.IsDestroyed);
    }

    [TestMethod]
    public void Refresh_AnOuterTowerStillStanding_ShowsTheInnerOneLocked()
    {
        var vm = Panel(Session(out _));

        var inner = vm.LaneBars[0].Towers.Single(t => t.TowerId == FordsTestLayout.EastInner0);
        var outer = vm.LaneBars[0].Towers.Single(t => t.TowerId == FordsTestLayout.EastOuter0);

        Assert.IsTrue(inner.IsLocked);
        Assert.IsFalse(outer.IsLocked);
        Assert.AreNotEqual(inner.FillColor, outer.FillColor, "a locked tower is drawn dimmer");
    }

    [TestMethod]
    public void Refresh_ShowsBothKeeps()
    {
        var vm = Panel(Session(out _));

        Assert.AreEqual(FordsTestLayout.WestKeep, vm.OwnKeep.TowerId);
        Assert.AreEqual(FordsTestLayout.EastKeep, vm.EnemyKeep.TowerId);
        Assert.IsTrue(vm.OwnKeep.IsOwn);
        Assert.IsFalse(vm.EnemyKeep.IsOwn);
        Assert.AreEqual("Your keep", vm.OwnKeep.Label);
        Assert.AreEqual("Enemy keep", vm.EnemyKeep.Label);
        Assert.AreEqual(FordsTowerBarVM.FullWidth, vm.OwnKeep.FillWidth);
        Assert.IsTrue(vm.OwnKeep.HitPointsText.Length > 0);
    }

    [TestMethod]
    public void Refresh_ShowsLiveUnitCountsFromTheLastTick()
    {
        var session = Session(out var match);
        match.Tick(Snap(1f, new[] { Live(MatchSide.West, 0, 12), Live(MatchSide.East, 0, 7) }));
        var vm = Panel(session);

        Assert.AreEqual("12 v 7", vm.Lanes[0].CountsText);
    }

    [TestMethod]
    public void Refresh_DrainsTheEventFeedIntoLines()
    {
        var session = Session(out _);
        session.PushEvents(new[]
        {
            new MatchEvent(MatchEventKind.TowerDestroyed, MatchSide.West, FordsTestLayout.WestOuter0, 0),
            new MatchEvent(MatchEventKind.TowerDestroyed, MatchSide.East, FordsTestLayout.EastOuter0, 300),
            new MatchEvent(MatchEventKind.HeroFielded, MatchSide.East, "lord_1_17", 0),
            new MatchEvent(MatchEventKind.HeroDied, MatchSide.West, "lord_1_9", 0),
        });
        var vm = Panel(session);

        CollectionAssert.AreEqual(
            new[] { "We lost a tower.", "An enemy tower has fallen.", "The enemy fields Name_lord_1_17.", "Name_lord_1_9 has fallen." },
            vm.Feed.Select(f => f.Text).ToArray());
        Assert.AreEqual(0, session.DrainEvents().Count, "the panel took the events");
    }

    [TestMethod]
    public void Refresh_KeepsTheLatestSixFeedLines()
    {
        var session = Session(out _);
        session.PushEvents(Enumerable.Range(0, 9).Select(i => new MatchEvent(MatchEventKind.PointCaptured, MatchSide.West, "point_" + i, 0)));
        session.PushEvents(new[] { new MatchEvent(MatchEventKind.CampCleared, MatchSide.East, "camp_east_0", 250) });
        var vm = Panel(session);

        Assert.AreEqual(6, vm.Feed.Count);
        Assert.AreEqual("The enemy cleared a camp.", vm.Feed[5].Text);
    }

    [TestMethod]
    public void Refresh_ThePackBoughtEventOfTheEnemy_IsNotListed()
    {
        var session = Session(out _);
        session.PushEvents(new[]
        {
            new MatchEvent(MatchEventKind.PackBought, MatchSide.East, "inf", 100),
            new MatchEvent(MatchEventKind.PackBought, MatchSide.West, "inf", 100),
        });
        var vm = Panel(session);

        CollectionAssert.AreEqual(new[] { "Reinforcements sent for 100 gold." }, vm.Feed.Select(f => f.Text).ToArray());
    }

    [TestMethod]
    public void Refresh_EveryEventKindOnEitherSide_HasAFilledInLine()
    {
        foreach (var kind in System.Enum.GetValues(typeof(MatchEventKind)).Cast<MatchEventKind>())
        {
            foreach (var side in new[] { MatchSide.West, MatchSide.East })
            {
                if (kind == MatchEventKind.PackBought && side == MatchSide.East)
                    continue;

                var session = Session(out _);
                session.PushEvents(new[] { new MatchEvent(kind, side, "x", 10) });
                var panel = Panel(session);

                Assert.AreEqual(1, panel.Feed.Count, $"{kind} {side}");
                Assert.IsFalse(string.IsNullOrWhiteSpace(panel.Feed[0].Text), $"{kind} {side}");
                StringAssert.DoesNotMatch(panel.Feed[0].Text, new System.Text.RegularExpressions.Regex(@"\{[A-Z]+\}"), $"{kind} {side} left a variable unfilled");
            }
        }
    }

    private static MatchSnapshot OnPoint(float dt, int west, int east) =>
        Snap(dt, capture: new[] { new CaptureCounts("point_0", west, east) });

    [TestMethod]
    public void Refresh_ListsACapturePointRow_NeutralBeforeAnyoneArrives()
    {
        var vm = Panel(Session(out _));

        Assert.AreEqual("Capture points", vm.PointsTitle);
        Assert.AreEqual(1, vm.Points.Count);
        Assert.AreEqual("Point 1: neutral", vm.Points[0].Title);
        Assert.AreEqual(string.Empty, vm.Points[0].StatusText);
        Assert.AreEqual(string.Empty, vm.Points[0].ContestedText);
    }

    [TestMethod]
    public void Refresh_WhileWeTakeAPoint_ShowsOurProgressInPercent()
    {
        var session = Session(out var match);
        var vm = Panel(session);

        match.Tick(OnPoint(5f, west: 3, east: 0));
        vm.Refresh();

        Assert.AreEqual("Point 1: neutral", vm.Points[0].Title);
        Assert.AreEqual("You are taking it: 25%", vm.Points[0].StatusText);
        Assert.AreEqual(string.Empty, vm.Points[0].ContestedText);
    }

    [TestMethod]
    public void Refresh_WhileTheEnemyTakesAPoint_ShowsTheirProgressInPercent()
    {
        var session = Session(out var match);
        var vm = Panel(session);

        match.Tick(OnPoint(10f, west: 0, east: 2));
        vm.Refresh();

        Assert.AreEqual("The enemy is taking it: 50%", vm.Points[0].StatusText);
    }

    [TestMethod]
    public void Refresh_AnOwnedPoint_ShowsItsOwnerFromTheCommandersView_WithNoProgress()
    {
        var session = Session(out var match);
        var vm = Panel(session);

        match.Tick(OnPoint(25f, west: 1, east: 0));
        vm.Refresh();
        Assert.AreEqual("Point 1: yours", vm.Points[0].Title);
        Assert.AreEqual(string.Empty, vm.Points[0].StatusText);

        match.Tick(OnPoint(25f, west: 0, east: 1));
        match.Tick(OnPoint(25f, west: 0, east: 1));
        vm.Refresh();
        Assert.AreEqual("Point 1: enemy", vm.Points[0].Title);
        Assert.AreEqual(string.Empty, vm.Points[0].StatusText);
    }

    [TestMethod]
    public void Refresh_WeRetakeAnEnemyPoint_ShowsTheEnemyAsOwnerAndOurProgress()
    {
        var session = Session(out var match);
        var vm = Panel(session);
        match.Tick(OnPoint(25f, west: 0, east: 1));

        match.Tick(OnPoint(10f, west: 1, east: 0));
        vm.Refresh();

        Assert.AreEqual("Point 1: enemy", vm.Points[0].Title);
        Assert.AreEqual("You are taking it: 50%", vm.Points[0].StatusText);
    }

    [TestMethod]
    public void Refresh_BothSidesOnAPoint_MarksItContested_AndKeepsTheProgress()
    {
        var session = Session(out var match);
        var vm = Panel(session);
        match.Tick(OnPoint(5f, west: 2, east: 0));

        match.Tick(OnPoint(1f, west: 2, east: 2));
        vm.Refresh();

        Assert.AreEqual("Contested", vm.Points[0].ContestedText);
        Assert.AreEqual("You are taking it: 25%", vm.Points[0].StatusText);

        match.Tick(OnPoint(1f, west: 0, east: 0));
        vm.Refresh();
        Assert.AreEqual(string.Empty, vm.Points[0].ContestedText);
    }

    [TestMethod]
    public void Refresh_WhenOurCaptureDecays_SaysItIsSlippingBackNotThatWeAreTakingIt()
    {
        var session = Session(out var match);
        var vm = Panel(session);
        match.Tick(OnPoint(10f, west: 3, east: 0));

        match.Tick(OnPoint(4f, west: 0, east: 0));
        vm.Refresh();

        Assert.AreEqual("Your capture is slipping back: 30%", vm.Points[0].StatusText);
    }

    [TestMethod]
    public void Refresh_WhenTheEnemyCaptureDecays_SaysItIsSlippingBack()
    {
        var session = Session(out var match);
        var vm = Panel(session);
        match.Tick(OnPoint(10f, west: 0, east: 2));

        match.Tick(OnPoint(4f, west: 0, east: 0));
        vm.Refresh();

        Assert.AreEqual("The enemy capture is slipping back: 30%", vm.Points[0].StatusText);
    }

    [TestMethod]
    public void Refresh_WhenTheDecayEndsAndWeReturn_ShowsTakingAgain()
    {
        var session = Session(out var match);
        var vm = Panel(session);
        match.Tick(OnPoint(10f, west: 3, east: 0));
        match.Tick(OnPoint(4f, west: 0, east: 0));

        match.Tick(OnPoint(2f, west: 3, east: 0));
        vm.Refresh();

        Assert.AreEqual("You are taking it: 40%", vm.Points[0].StatusText);
    }

    [TestMethod]
    public void Refresh_ThePointSwatch_IsOurColourForOurPoints_TheirsForTheirs_AndGreyForNeutral()
    {
        var session = Session(out var match);
        var vm = Panel(session);
        var neutral = vm.Points[0].OwnerColor;
        match.Tick(OnPoint(25f, west: 1, east: 0));
        vm.Refresh();
        var own = vm.Points[0].OwnerColor;
        match.Tick(OnPoint(25f, west: 0, east: 1));
        match.Tick(OnPoint(25f, west: 0, east: 1));
        vm.Refresh();
        var enemy = vm.Points[0].OwnerColor;

        Assert.AreEqual(FordsTowerBarVM.OwnColor, own);
        Assert.AreEqual(FordsTowerBarVM.EnemyColor, enemy);
        Assert.AreNotEqual(neutral, own);
        Assert.AreNotEqual(neutral, enemy);
    }

    [TestMethod]
    public void Refresh_WithTwoPoints_NumbersTheRowsInLayoutOrder_AndKeepsTheSameItems()
    {
        var layout = Layout() with { CapturePoints = new List<CapturePointSlot> { new("point_0", new Vec3f(500f, 25f, 0f)), new("point_1", new Vec3f(500f, 25f, 50f)) } };
        var match = new FordsMatch(Config(), layout, Factions(), Slots(), new SeededFordsRandom(3));
        var session = new FordsSession();
        session.Fill(new object(), match, WestSlot, Substitute.For<IFordsBattlefieldAdapter>(), default, default, new List<Vec3f>());
        var vm = Panel(session);
        var first = vm.Points[0];

        vm.Refresh();

        Assert.AreEqual(2, vm.Points.Count);
        Assert.AreEqual("Point 1: neutral", vm.Points[0].Title);
        Assert.AreEqual("Point 2: neutral", vm.Points[1].Title);
        Assert.AreSame(first, vm.Points[0]);
    }

    [TestMethod]
    public void Refresh_AMatchWithNoCapturePoints_ShowsNoPointsRowAndNoTitle()
    {
        var match = new FordsMatch(Config(), Layout() with { CapturePoints = new List<CapturePointSlot>() }, Factions(), Slots(), new SeededFordsRandom(3));
        var session = new FordsSession();
        session.Fill(new object(), match, WestSlot, Substitute.For<IFordsBattlefieldAdapter>(), default, default, new List<Vec3f>());

        var vm = Panel(session);

        Assert.AreEqual(0, vm.Points.Count);
        Assert.AreEqual(string.Empty, vm.PointsTitle);
    }

    [TestMethod]
    public void Refresh_ClockStartsAtZeroAndFollowsTheMatch()
    {
        var session = Session(out var match);
        var vm = Panel(session);
        Assert.AreEqual("00:00", vm.ClockText);

        match.Tick(Snap(75f));
        vm.Refresh();

        Assert.AreEqual("01:15", vm.ClockText);
    }

    [TestMethod]
    public void Refresh_DuringTheCountdown_ShowsTheFormingUpLineInsteadOfTheClock()
    {
        var session = Session(out _, Config(c => c with { CountdownSeconds = 10f }));
        var vm = Panel(session);

        Assert.AreEqual("The armies are forming up", vm.ClockText);
    }

    [TestMethod]
    public void Refresh_DuringTheCountdown_NoBuyOrOrderButtonIsEnabled()
    {
        var session = Session(out _, Config(c => c with { CountdownSeconds = 10f }));
        var vm = Panel(session);

        Assert.IsTrue(vm.Offers.All(o => !o.CanAfford), "the match answers NotLive to a pack bought in the countdown");
        Assert.IsTrue(vm.Heroes.All(h => !h.CanAct), "the match answers NotLive to a hero fielded in the countdown");
        Assert.IsTrue(vm.Orders.All(o => !o.CanSet), "the match answers NotLive to a lane order given in the countdown");
    }

    [TestMethod]
    public void Refresh_WhenTheCountdownEnds_TheButtonsLightUp()
    {
        var session = Session(out var match, Config(c => c with { CountdownSeconds = 10f }));
        var vm = Panel(session);

        match.Tick(Snap(11f));
        vm.Refresh();

        Assert.IsTrue(vm.Offers.Take(4).All(o => o.CanAfford));
        Assert.IsTrue(vm.Heroes[0].CanAct);
        Assert.IsTrue(vm.Orders[1].CanSet);
    }

    [TestMethod]
    public void Refresh_BeforeTheEnd_ShowsNoResultPanel()
    {
        var vm = Panel(Session(out _));

        Assert.IsFalse(vm.IsResultVisible);
    }

    [TestMethod]
    public void Refresh_AfterTheEnemyKeepFalls_ShowsTheVictoryPanel()
    {
        var session = Session(out var match);
        var vm = Panel(session);
        match.OnTowerDestroyed(FordsTestLayout.EastKeep, null);
        match.Tick(Snap(1f));

        vm.Refresh();

        Assert.IsTrue(vm.IsResultVisible);
        Assert.AreEqual("Victory!", vm.ResultTitle);
        Assert.AreEqual("The enemy keep has fallen.", vm.ResultText);
        Assert.AreEqual("Return to the main menu", vm.ResultButtonText);
    }

    [TestMethod]
    public void Refresh_AfterOurKeepFalls_ShowsTheDefeatPanel()
    {
        var session = Session(out var match);
        var vm = Panel(session);
        match.OnTowerDestroyed(FordsTestLayout.WestKeep, null);
        match.Tick(Snap(1f));

        vm.Refresh();

        Assert.IsTrue(vm.IsResultVisible);
        Assert.AreEqual("Defeat", vm.ResultTitle);
        Assert.AreEqual("Your keep has fallen.", vm.ResultText);
    }

    [TestMethod]
    public void ExecuteExit_CallsTheExitOnce()
    {
        var vm = Panel(Session(out _));

        vm.ExecuteExit();
        vm.ExecuteExit();

        Assert.AreEqual(1, _exits);
    }

    [TestMethod]
    public void Tick_AfterTheResultHasBeenShownForAWhile_DoesNotLeaveByItself()
    {
        var session = Session(out var match);
        var vm = Panel(session);
        match.OnTowerDestroyed(FordsTestLayout.EastKeep, null);
        match.Tick(Snap(1f));
        vm.Refresh();

        vm.Tick(40f);
        vm.Tick(40f, enginePaused: true);

        Assert.IsTrue(vm.IsResultVisible);
        Assert.AreEqual(0, _exits, "the automatic return belongs to the mission logic (FordsMissionExit); the panel keeps only its button");
    }

    [TestMethod]
    public void Tick_WhileTheEngineIsPaused_HoldsTheRefusalNotice()
    {
        var vm = Panel(Session(out _));
        vm.BuyOffer(4);

        vm.Tick(10f, enginePaused: true);

        Assert.AreEqual("Not enough gold.", vm.NoticeText);
    }

    [TestMethod]
    public void Tick_ClearsTheRefusalAfterAFewSeconds()
    {
        var vm = Panel(Session(out _));
        vm.BuyOffer(4);

        vm.Tick(2f);
        Assert.AreEqual("Not enough gold.", vm.NoticeText);
        vm.Tick(3f);

        Assert.AreEqual(string.Empty, vm.NoticeText);
    }

    [TestMethod]
    public void Tick_WhileTheMatchIsRunning_NeverExits()
    {
        var vm = Panel(Session(out _));

        vm.Tick(500f);

        Assert.AreEqual(0, _exits);
    }

    [TestMethod]
    public void Refresh_TheSecondTime_KeepsTheSameItemObjectsSoAClickIsNotLost()
    {
        var session = Session(out _);
        var vm = Panel(session);
        var offer = vm.Offers[0];
        var lane = vm.Lanes[0];
        var tower = vm.LaneBars[0].Towers[0];

        vm.Refresh();
        vm.Refresh();

        Assert.AreSame(offer, vm.Offers[0]);
        Assert.AreSame(lane, vm.Lanes[0]);
        Assert.AreSame(tower, vm.LaneBars[0].Towers[0]);
    }

    [TestMethod]
    public void Refresh_AfterTheSessionEnds_HidesThePanelAgain()
    {
        var session = Session(out _);
        var vm = Panel(session);
        session.Clear();

        vm.Refresh();

        Assert.IsFalse(vm.IsVisible);
    }
}
