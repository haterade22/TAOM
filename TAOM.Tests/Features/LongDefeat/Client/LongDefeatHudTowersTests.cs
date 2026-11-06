using System;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Client.Features.LongDefeat;
using TAOM.Features.LongDefeat;
using TAOM.Features.LongDefeat.Domain;

namespace TAOM.Tests.Features.LongDefeat.Client;

/// <summary>
/// Phase 3 Slice 7, the Fortifications group's high towers row: listed while the build offers a tower level and the fortress records posts, described by the level the next buy brings and the posts it holds, sold
/// through the session, and worded for its own top level (the gate's "fully reinforced" would be wrong here, Slice 5's review).
/// </summary>
[TestClass]
public class LongDefeatHudTowersTests
{
    private static readonly TowerPlan Towers = new("fow_rohan_eastfold_bowman",
        new[] { new TowerLevelPlan(4, false, 250), new TowerLevelPlan(8, false, 300), new TowerLevelPlan(8, true, 400) },
        new TowerVolleyPlan("fow_tower_arrow", 6, 20f, 30f, 6f, 80f, 2.5f));

    private static LdTower Tower() => new("hornburg", Enumerable.Range(0, 8).Select(i => new MusterFrame(i, 0f, 30f, 0f, 1f)).ToList(), Array.Empty<LdPoint>());

    private static LongDefeatHudVM Panel(out LongDefeatSession session, int gold = 1000, bool posts = true, FortOffer? offered = null, int defenders = 40)
    {
        var waves = new WavePlan(new[] { new WaveSpec(110, new[] { new WaveTroop("orc", FormationSlot.Infantry, 1) }) }, new WaveScaling(10, 300));
        var shop = new ShopPlan(gold, new[] { new ShopOffer("militia", "{=TAOM_LD_Offer_Militia}Westfold Militia", "fow_militia", FormationSlot.Infantry, 12, 180) }, new FortPlan(null, Towers, null, null));
        var rules = RunRules.Default with { WaveBonusBase = 0, WaveBonusPerWave = 0, EarlyGoldPerSecond = 0f };
        var plan = new RunPlan("helms", new[] { new GarrisonRow("fow_militia", FormationSlot.Infantry, 30) }, waves, shop, rules, RunOptions.Default)
        {
            Offered = offered ?? FortOffer.Offered,
            Towers = posts ? new[] { Tower() } : Array.Empty<LdTower>(),
        };
        var run = new SurvivalRun(plan);
        run.Begin();
        run.Tick(new RunSnapshot(1f, 0, 0, defenders, 0, 0, 1000) { Gates = Array.Empty<LdGateFacts>() });
        session = new LongDefeatSession();
        session.Fill(run);
        var vm = new LongDefeatHudVM(session, () => { });
        vm.ToggleShop();
        return vm;
    }

    [TestMethod]
    public void TheShop_ListsTheTowers_WithTheNextLevelsPostsAndPrice()
    {
        var vm = Panel(out _);

        Assert.IsTrue(vm.Fort.IsVisible);
        Assert.IsTrue(vm.Fort.IsTowersVisible);
        Assert.AreEqual("High towers", vm.Fort.TowersName);
        StringAssert.Contains(vm.Fort.TowersDetail, "Level 1 of 2");
        StringAssert.Contains(vm.Fort.TowersDetail, "up to 4");
        Assert.AreEqual("250 gold", vm.Fort.TowersPrice);
        Assert.IsTrue(vm.Fort.CanBuyTowers);
    }

    [TestMethod]
    public void ABuy_GoesThroughTheSession_AndTheRowMovesToTheNextLevel()
    {
        var vm = Panel(out var session);

        vm.Fort.ExecuteBuyTowers();

        Assert.AreEqual(1, session.GetView()!.Fort.Towers.Level);
        Assert.AreEqual(750, session.GetView()!.Gold);
        StringAssert.Contains(vm.Fort.TowersDetail, "Level 2 of 2");
        StringAssert.Contains(vm.Fort.TowersDetail, "up to 8");
        Assert.AreEqual("300 gold", vm.Fort.TowersPrice);
    }

    [TestMethod]
    public void AtTheTopOfWhatTheBuildOffers_TheRowSaysFullyManned_AndBuysNoMore()
    {
        var vm = Panel(out var session);
        vm.Fort.ExecuteBuyTowers();
        vm.Fort.ExecuteBuyTowers();

        Assert.IsTrue(vm.Fort.IsTowersVisible, "a row stays listed at its top, so a player sees it is spent");
        Assert.AreEqual("Fully manned", vm.Fort.TowersPrice);
        Assert.IsFalse(vm.Fort.CanBuyTowers);
        var gold = session.GetView()!.Gold;

        vm.Fort.ExecuteBuyTowers();

        Assert.AreEqual(gold, session.GetView()!.Gold, "level 3 is the tower volley, which this build does not deliver");
    }

    [TestMethod]
    public void TheTopLevelRefusal_HasItsOwnWording_NotTheGates()
    {
        Assert.AreEqual("The high towers are fully manned.", LongDefeatHudText.RefusalTowers(FortResult.MaxLevel));
        Assert.AreEqual("The gate is fully reinforced.", LongDefeatHudText.Refusal(FortResult.MaxLevel));
        Assert.AreEqual("The Elven Volley is fully upgraded.", LongDefeatHudText.RefusalVolley(FortResult.MaxLevel));
        Assert.AreEqual(LongDefeatHudText.Refusal(FortResult.NoGold), LongDefeatHudText.RefusalTowers(FortResult.NoGold), "the other refusals are shared");
        Assert.AreEqual(string.Empty, LongDefeatHudText.RefusalTowers(FortResult.Ok));
    }

    [TestMethod]
    public void ARefusedBuy_SaysWhy_OnTheNoticeLine()
    {
        var vm = Panel(out _, gold: 100);

        Assert.IsFalse(vm.Fort.CanBuyTowers, "the row is listed and disabled when the gold is short");
        vm.Fort.ExecuteBuyTowers();

        Assert.AreEqual("Not enough gold.", vm.NoticeText);
    }

    [TestMethod]
    public void WithNoPostRecorded_OrNoTowerLevelOffered_NoRowIsListed()
    {
        var bare = Panel(out _, posts: false);
        var none = Panel(out _, offered: FortOffer.None);

        Assert.IsFalse(bare.Fort.IsTowersVisible);
        Assert.IsFalse(bare.Fort.IsVisible);
        Assert.IsFalse(none.Fort.IsTowersVisible);
    }

    [TestMethod]
    public void WithTheGarrisonTooFullForThePosts_TheRowStaysListed_Disabled_AndSaysWhyOnAClick()
    {
        var vm = Panel(out var session, defenders: 215);

        Assert.IsTrue(vm.Fort.IsTowersVisible, "a row that appears and vanishes as soldiers fall reads as a fault");
        Assert.IsFalse(vm.Fort.CanBuyTowers);
        Assert.AreEqual("250 gold", vm.Fort.TowersPrice);

        vm.Fort.ExecuteBuyTowers();

        Assert.AreEqual("The garrison is full.", vm.NoticeText);
        Assert.AreEqual(0, session.GetView()!.Fort.Towers.Level);
    }
}
