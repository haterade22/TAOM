using System;
using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Client.Features.LongDefeat;
using TAOM.Features.LongDefeat;
using TAOM.Features.LongDefeat.Domain;

namespace TAOM.Tests.Features.LongDefeat.Client;

/// <summary>The upgrade group of the shop panel (#1161): one row per step whose troop stands, with its counts and its price for one soldier, a buy through the session, and a line for every refusal.</summary>
[TestClass]
[TestCategory("RequiresGame")]
public class LongDefeatHudUpgradeTests
{
    private static readonly UpgradeStep Axemen = new("fow_militia>fow_axeman", "{=TAOM_LD_Up_Axemen}Militia to Westfold Axemen", "fow_militia", "fow_axeman", 15);

    /// <summary>The open shop in the preparation, with <paramref name="standing"/> militia read from the field.</summary>
    private static LongDefeatHudVM Panel(out LongDefeatSession session, int gold = 600, int standing = 30)
    {
        var waves = new WavePlan(new[] { new WaveSpec(110, new[] { new WaveTroop("orc", FormationSlot.Infantry, 1) }) }, new WaveScaling(10, 300));
        var shop = new ShopPlan(gold, new[] { new ShopOffer("militia", "{=TAOM_LD_Offer_Militia}Westfold Militia", "fow_militia", FormationSlot.Infantry, 12, 180) }) { Upgrades = new[] { Axemen } };
        var plan = new RunPlan("helms", new[] { new GarrisonRow("fow_militia", FormationSlot.Infantry, 30) }, waves, shop, RunRules.Default, RunOptions.Default);
        var run = new SurvivalRun(plan);
        run.Begin();
        run.ObserveStanding(new Dictionary<string, int> { ["fow_militia"] = standing });
        session = new LongDefeatSession();
        session.Fill(run);
        var vm = new LongDefeatHudVM(session, () => { });
        vm.ToggleShop();
        return vm;
    }

    [TestMethod]
    public void TheShop_ListsAStepWhoseTroopStands_WithItsCountsAndItsPriceForOne()
    {
        var vm = Panel(out _);

        Assert.IsTrue(vm.IsShopVisible);
        Assert.IsTrue(vm.HasUpgrades);
        Assert.AreEqual("Upgrade your soldiers", vm.UpgradesTitle);
        Assert.AreEqual(1, vm.Upgrades.Count);
        Assert.AreEqual("Militia to Westfold Axemen", vm.Upgrades[0].Name);
        Assert.AreEqual("30 standing, gold for 30", vm.Upgrades[0].DetailText);
        Assert.AreEqual("15 gold each", vm.Upgrades[0].PriceText);
        Assert.IsTrue(vm.Upgrades[0].CanAfford);
    }

    [TestMethod]
    public void TheGold_CapsWhatARowSells_AndARowTheGoldMissesIsGrey()
    {
        Assert.AreEqual("30 standing, gold for 6", Panel(out _, gold: 100).Upgrades[0].DetailText);

        var poor = Panel(out _, gold: 10);
        Assert.AreEqual("30 standing, gold for 0", poor.Upgrades[0].DetailText);
        Assert.IsFalse(poor.Upgrades[0].CanAfford);
    }

    [TestMethod]
    public void ABuy_SpendsForEverySoldierTheGoldPaysFor_AndTheRowGoesWhenNoneIsLeftToSell()
    {
        var vm = Panel(out var session);

        vm.Upgrades[0].ExecuteUpgrade();

        Assert.AreEqual(150, session.GetView()!.Gold, "30 militia at 15 each");
        Assert.AreEqual(0, vm.Upgrades.Count, "every militia is sold to the order that goes out on the next tick");
        Assert.IsFalse(vm.HasUpgrades);
        Assert.AreEqual(string.Empty, vm.NoticeText, "a buy that went through says nothing");
    }

    [TestMethod]
    public void NoSoldierStanding_ListsNoRowAndNoTitle()
    {
        var vm = Panel(out _, standing: 0);

        Assert.IsFalse(vm.HasUpgrades);
        Assert.AreEqual(0, vm.Upgrades.Count);
        Assert.AreEqual(string.Empty, vm.UpgradesTitle);
    }

    [TestMethod]
    public void EveryUpgradeRefusal_HasALine_AndOkHasNone()
    {
        Assert.AreEqual(string.Empty, LongDefeatHudText.Refusal(UpgradeResult.Ok));
        foreach (UpgradeResult result in Enum.GetValues(typeof(UpgradeResult)))
        {
            if (result != UpgradeResult.Ok)
                Assert.IsTrue(LongDefeatHudText.Refusal(result).Length > 0, result.ToString());
        }

        StringAssert.Contains(LongDefeatHudText.Refusal(UpgradeResult.NoneStanding), "upgrade");
        StringAssert.Contains(LongDefeatHudText.Refusal(UpgradeResult.NoGold), "gold");
    }
}
