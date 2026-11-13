using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.LongDefeat.Domain;

namespace TAOM.Tests.Features.LongDefeat.Domain;

/// <summary>The unattended buyer's upgrades (#1161): only gold that no pack and no fortification wants buys one, the first row in shop order that the gold reaches, and never in a wave.</summary>
[TestClass]
public class ShopBotUpgradeTests
{
    private static readonly ShopOffer[] Offers = { new("militia", "Militia", "m", FormationSlot.Infantry, 12, 180) };

    private static readonly UpgradeStep MilitiaToAxe = new("m>a", "a", "m", "a", 15);
    private static readonly UpgradeStep AxeToVeteran = new("a>v", "v", "a", "v", 25);

    private static RunView View(int gold, int standing, RunPhase phase = RunPhase.Intermission, params UpgradeRow[] rows) =>
        new(phase, 1, 1, gold, 60f, standing, 0, 0, 0f, Offers) { Upgrades = rows };

    [TestMethod]
    public void Decide_GarrisonAtTheCeiling_UpgradesTheFirstRowTheGoldReaches()
    {
        var action = ShopBot.Decide(View(500, 170, RunPhase.Intermission, new UpgradeRow(MilitiaToAxe, 0, 0), new UpgradeRow(AxeToVeteran, 20, 20)), 0f);

        Assert.AreEqual(BotActionKind.Upgrade, action.Kind);
        Assert.AreEqual("a>v", action.OfferId);
    }

    [TestMethod]
    public void Decide_APackStillFits_BuysThePackFirst()
    {
        var action = ShopBot.Decide(View(500, 100, RunPhase.Intermission, new UpgradeRow(MilitiaToAxe, 30, 30)), 0f);

        Assert.AreEqual(BotActionKind.Buy, action.Kind);
    }

    [TestMethod]
    public void Decide_GoldUnderEveryPack_SpendsItOnAnUpgrade()
    {
        var action = ShopBot.Decide(View(100, 100, RunPhase.Intermission, new UpgradeRow(MilitiaToAxe, 30, 6)), 0f);

        Assert.AreEqual(BotActionKind.Upgrade, action.Kind);
        Assert.AreEqual("m>a", action.OfferId);
    }

    [TestMethod]
    public void Decide_NoRowTheGoldReaches_SoundsTheHornAfterHalfAMinute()
    {
        var view = View(10, 170, RunPhase.Intermission, new UpgradeRow(MilitiaToAxe, 30, 0));

        Assert.AreEqual(BotActionKind.Wait, ShopBot.Decide(view, 0f).Kind);
        Assert.AreEqual(BotActionKind.Horn, ShopBot.Decide(view, 30f).Kind);
    }

    [TestMethod]
    public void Decide_InAWave_NeverUpgrades()
    {
        var action = ShopBot.Decide(View(500, 170, RunPhase.Wave, new UpgradeRow(MilitiaToAxe, 30, 30)), 0f);

        Assert.AreEqual(BotActionKind.Wait, action.Kind);
    }
}
