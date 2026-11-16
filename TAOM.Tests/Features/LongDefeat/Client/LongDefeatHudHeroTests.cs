using System;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Client.Features.LongDefeat;
using TAOM.Features.LongDefeat;
using TAOM.Features.LongDefeat.Domain;

namespace TAOM.Tests.Features.LongDefeat.Client;

/// <summary>The hero group of the shop panel (#1337): one row per hero of the shop in the plan's order, a state line for each of the three states, a buy through the session on a click, and a line for every refusal.</summary>
[TestClass]
[TestCategory("RequiresGame")]
public class LongDefeatHudHeroTests
{
    private static readonly HeroOffer Aragorn = new("aragorn", "{=nc_aragorn_name}Aragorn", "{=TAOM_LD_Hero_Aragorn_Text}The heir of Isildur.", "taom_hero_aragorn", 300, 3f);
    private static readonly HeroOffer Legolas = new("legolas", "{=nc_legolas_name}Legolas", "{=TAOM_LD_Hero_Legolas_Text}The prince of Mirkwood.", "taom_hero_legolas", 250, 2f);
    private static readonly HeroOffer Gimli = new("gimli", "{=nc_gimli_name}Gimli", "{=TAOM_LD_Hero_Gimli_Text}The son of Gloin.", "taom_hero_gimli", 250, 2.5f);

    private static LongDefeatHudVM Panel(out LongDefeatSession session, out SurvivalRun run, int gold = 600, bool withHeroes = true)
    {
        var waves = new WavePlan(new[] { new WaveSpec(110, new[] { new WaveTroop("orc", FormationSlot.Infantry, 1) }) }, new WaveScaling(10, 300));
        var shop = new ShopPlan(gold, new[] { new ShopOffer("militia", "{=TAOM_LD_Offer_Militia}Westfold Militia", "fow_militia", FormationSlot.Infantry, 12, 180) })
        {
            Heroes = withHeroes ? new[] { Aragorn, Legolas, Gimli } : Array.Empty<HeroOffer>(),
        };
        var plan = new RunPlan("helms", new[] { new GarrisonRow("fow_militia", FormationSlot.Infantry, 30) }, waves, shop, RunRules.Default, RunOptions.Default);
        run = new SurvivalRun(plan);
        run.Begin();
        session = new LongDefeatSession();
        session.Fill(run);
        var vm = new LongDefeatHudVM(session, () => { });
        vm.ToggleShop();
        return vm;
    }

    [TestMethod]
    public void TheShop_ListsEveryHeroInThePlansOrder_WithNameTextAndPrice()
    {
        var vm = Panel(out _, out _);

        Assert.IsTrue(vm.HasHeroes);
        Assert.AreEqual("Heroes", vm.HeroesTitle);
        CollectionAssert.AreEqual(new[] { "Aragorn", "Legolas", "Gimli" }, vm.Heroes.Select(h => h.Name).ToList());
        Assert.AreEqual("The heir of Isildur.", vm.Heroes[0].DetailText);
        Assert.AreEqual("300 gold", vm.Heroes[0].StateText);
        Assert.AreEqual("250 gold", vm.Heroes[1].StateText);
        Assert.IsTrue(vm.Heroes.All(h => h.CanBuy));
    }

    [TestMethod]
    public void AHeroTheGoldMissesIsGrey_AndOneItReachesIsNot()
    {
        var vm = Panel(out _, out _, gold: 260);

        Assert.IsFalse(vm.Heroes[0].CanBuy, "300 gold against 260");
        Assert.IsTrue(vm.Heroes[1].CanBuy);
        Assert.IsTrue(vm.Heroes[2].CanBuy);
        Assert.AreEqual("300 gold", vm.Heroes[0].StateText, "a grey row still shows its price");
    }

    [TestMethod]
    public void ABuy_SpendsThePrice_AndTheRowReadsOnHisWay()
    {
        var vm = Panel(out var session, out _);

        vm.Heroes[0].ExecuteBuy();

        Assert.AreEqual(300, session.GetView()!.Gold, "600 less 300");
        Assert.AreEqual("On his way", vm.Heroes[0].StateText);
        Assert.IsFalse(vm.Heroes[0].CanBuy);
        Assert.AreEqual("250 gold", vm.Heroes[1].StateText, "the others keep their price line");
        Assert.AreEqual(string.Empty, vm.NoticeText, "a buy that went through says nothing");
    }

    [TestMethod]
    public void AHeroOnTheField_ReadsOnTheField_AndHisRowDoesNotBuy()
    {
        var vm = Panel(out var session, out var run);
        vm.Heroes[0].ExecuteBuy();
        run.HeroSpawned("aragorn", true);
        vm.Refresh();

        Assert.AreEqual("On the field", vm.Heroes[0].StateText);
        Assert.IsFalse(vm.Heroes[0].CanBuy);

        var gold = session.GetView()!.Gold;
        vm.Heroes[0].ExecuteBuy();

        Assert.AreEqual(gold, session.GetView()!.Gold, "he is bought already");
        Assert.AreEqual(LongDefeatHudText.Refusal(HeroResult.Alive), vm.NoticeText);
    }

    [TestMethod]
    public void AHeroWhoHasFallen_IsForSaleAgain()
    {
        var vm = Panel(out _, out var run);
        vm.Heroes[0].ExecuteBuy();
        run.HeroSpawned("aragorn", true);
        run.HeroDown("aragorn", loss: true);
        vm.Refresh();

        Assert.AreEqual("300 gold", vm.Heroes[0].StateText);
        Assert.IsTrue(vm.Heroes[0].CanBuy);
    }

    [TestMethod]
    public void ABuyTheGoldMisses_SaysSo_AndSpendsNothing()
    {
        var vm = Panel(out var session, out _, gold: 100);

        vm.Heroes[0].ExecuteBuy();

        Assert.AreEqual(100, session.GetView()!.Gold);
        Assert.AreEqual(LongDefeatHudText.Refusal(HeroResult.NoGold), vm.NoticeText);
        Assert.AreEqual("300 gold", vm.Heroes[0].StateText);
    }

    [TestMethod]
    public void AShopWithNoHeroes_ListsNoRowAndNoTitle()
    {
        var vm = Panel(out _, out _, withHeroes: false);

        Assert.IsFalse(vm.HasHeroes);
        Assert.AreEqual(0, vm.Heroes.Count);
        Assert.AreEqual(string.Empty, vm.HeroesTitle);
    }

    [TestMethod]
    public void EveryHeroRefusal_HasItsOwnLine_AndOkHasNone()
    {
        Assert.AreEqual(string.Empty, LongDefeatHudText.Refusal(HeroResult.Ok));
        var lines = Enum.GetValues(typeof(HeroResult)).Cast<HeroResult>().Where(r => r != HeroResult.Ok)
            .Select(r => LongDefeatHudText.Refusal(r)).ToList();

        Assert.IsTrue(lines.All(l => l.Length > 0));
        Assert.AreEqual(lines.Count, lines.Distinct().Count(), "one sentence per result");
        StringAssert.Contains(LongDefeatHudText.Refusal(HeroResult.NoGold), "gold");
        StringAssert.Contains(LongDefeatHudText.Refusal(HeroResult.GarrisonFull), "garrison");
        StringAssert.Contains(LongDefeatHudText.Refusal(HeroResult.NotOpen), "shop");
        StringAssert.Contains(LongDefeatHudText.Refusal(HeroResult.RunOver), "run");
        StringAssert.Contains(LongDefeatHudText.Refusal(HeroResult.UnknownHero), "hero");
        StringAssert.Contains(LongDefeatHudText.Refusal(HeroResult.Alive), "hero");
    }
}
