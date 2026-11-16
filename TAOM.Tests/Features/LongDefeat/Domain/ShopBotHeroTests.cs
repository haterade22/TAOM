using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.LongDefeat.Domain;

namespace TAOM.Tests.Features.LongDefeat.Domain;

/// <summary>
/// The shop bot and the heroes (#1325): it buys a hero who is for sale once no pack fits and no fortification is wanted, before any
/// upgrade, so a default run spends its first gold on soldiers as it always did and an unattended run with gold to spare fields the heroes.
/// </summary>
[TestClass]
public class ShopBotHeroTests
{
    private static readonly ShopOffer[] Offers = { new("militia", "Militia", "m", FormationSlot.Infantry, 12, 180) };
    private static readonly HeroOffer Aragorn = new("aragorn", "Aragorn", "", "taom_hero_aragorn", 300, 3f);
    private static readonly HeroOffer Gimli = new("gimli", "Gimli", "", "taom_hero_gimli", 250, 3f);
    private static readonly UpgradeStep MilitiaToAxe = new("m>a", "Axemen", "m", "a", 15);

    private static RunView View(int gold, int standing, int cap = 220, params HeroRow[] heroes) =>
        new(RunPhase.Intermission, 1, 1, gold, 60f, standing, 0, 0, 0f, Offers) { Heroes = heroes, GarrisonCap = cap };

    private static HeroRow Row(HeroOffer hero, HeroState state = HeroState.ForSale) => new(hero, hero.Price, state);

    [TestMethod]
    public void Decide_NoPackFits_BuysTheFirstHeroForSaleTheGoldReaches()
    {
        // 170 stand: the bot's garrison ceiling is reached, so it buys no more packs.
        var action = ShopBot.Decide(View(260, 170, 220, Row(Aragorn), Row(Gimli)), 0f);

        Assert.AreEqual(BotActionKind.Hero, action.Kind);
        Assert.AreEqual("gimli", action.OfferId, "Aragorn costs 300 and the purse holds 260");
    }

    [TestMethod]
    public void Decide_APackStillFits_BuysThePackFirst()
    {
        var action = ShopBot.Decide(View(1000, 100, 220, Row(Aragorn)), 0f);

        Assert.AreEqual(BotActionKind.Buy, action.Kind);
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void Decide_AHeroAlreadyBought_IsNotAskedForAgain(bool standing)
    {
        var state = standing ? HeroState.Standing : HeroState.Pending;
        var action = ShopBot.Decide(View(1000, 170, 220, Row(Aragorn, state)), 0f);

        Assert.AreNotEqual(BotActionKind.Hero, action.Kind);
    }

    [TestMethod]
    public void Decide_TheGarrisonCapIsReached_AsksForNoHero()
    {
        // A refused buy would be asked again every tick until the intermission timed out.
        var action = ShopBot.Decide(View(1000, 170, 170, Row(Aragorn)), 0f);

        Assert.AreNotEqual(BotActionKind.Hero, action.Kind);
    }

    [TestMethod]
    public void Decide_AHeroComesBeforeAnUpgrade()
    {
        var view = View(1000, 170, 220, Row(Aragorn)) with { Upgrades = new[] { new UpgradeRow(MilitiaToAxe, 30, 30) } };

        Assert.AreEqual(BotActionKind.Hero, ShopBot.Decide(view, 0f).Kind);
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void Decide_AnAffordableFortificationComesBeforeAHero(bool buysFirst)
    {
        // The volley level is wanted at clear 1 and the purse reaches both; 170 stand, so no pack is asked for either way.
        var fort = FortView.None with { BuysFirst = buysFirst, Volley = new FortRow(0, 2, 350, true) };
        var view = View(1000, 170, 220, Row(Aragorn)) with { Fort = fort };

        Assert.AreEqual(BotActionKind.VolleyLevel, ShopBot.Decide(view, 0f).Kind);
    }

    [TestMethod]
    public void Decide_AFortificationTheGoldDoesNotReach_LeavesTheHeroToTheBot()
    {
        var fort = FortView.None with { Volley = new FortRow(0, 2, 350, true) };
        var view = View(300, 170, 220, Row(Gimli)) with { Fort = fort };

        Assert.AreEqual(BotActionKind.Hero, ShopBot.Decide(view, 0f).Kind);
    }

    [TestMethod]
    public void Decide_InAWave_BuysNoHero()
    {
        var view = View(1000, 170, 220, Row(Aragorn)) with { Phase = RunPhase.Wave };

        Assert.AreNotEqual(BotActionKind.Hero, ShopBot.Decide(view, 0f).Kind);
    }
}
