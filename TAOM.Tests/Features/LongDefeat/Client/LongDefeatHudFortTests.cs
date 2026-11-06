using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Client.Features.LongDefeat;
using TAOM.Features.LongDefeat;
using TAOM.Features.LongDefeat.Domain;

namespace TAOM.Tests.Features.LongDefeat.Client;

/// <summary>The Fortifications group of the shop panel (phase3-plan.md section 4), with its first row, field care: listed when the build offers it, sold through the session, buying only.</summary>
[TestClass]
public class LongDefeatHudFortTests
{
    private static readonly FortPlan WithCare = new(null, null, new CarePlan(0.75f, 150, 1), null);

    /// <summary>The panel in the first intermission (care is not for sale before the first wave, nobody being hurt yet), or in the preparation. No gold is earned on the way, so <paramref name="gold"/> is what the purse holds.</summary>
    private static LongDefeatHudVM Panel(out LongDefeatSession session, out SurvivalRun run, int gold = 600, FortOffer? offered = null, FortPlan? fort = null, bool intermission = true)
    {
        var waves = new WavePlan(new[] { new WaveSpec(110, new[] { new WaveTroop("orc", FormationSlot.Infantry, 1) }) }, new WaveScaling(10, 300));
        var shop = new ShopPlan(gold, new[] { new ShopOffer("militia", "{=TAOM_LD_Offer_Militia}Westfold Militia", "fow_militia", FormationSlot.Infantry, 12, 180) }, fort ?? WithCare);
        var rules = RunRules.Default with { WaveBonusBase = 0, WaveBonusPerWave = 0, EarlyGoldPerSecond = 0f };
        var plan = new RunPlan("helms", new[] { new GarrisonRow("fow_militia", FormationSlot.Infantry, 30) }, waves, shop, rules, RunOptions.Default) { Offered = offered ?? FortOffer.Offered };
        run = new SurvivalRun(plan);
        run.Begin();
        for (var i = 0; intermission && i < 5000 && run.Phase != RunPhase.Intermission; i++)
            run.Tick(new RunSnapshot(1f, 0, 0, 40, 0, 0, 1000));

        Assert.IsTrue(!intermission || run.Phase == RunPhase.Intermission, "the first wave never cleared");
        session = new LongDefeatSession();
        session.Fill(run);
        var vm = new LongDefeatHudVM(session, () => { });
        vm.ToggleShop();
        return vm;
    }

    [TestMethod]
    public void TheShop_ListsFieldCare_WithItsShareAndPrice()
    {
        var vm = Panel(out _, out _);

        Assert.IsTrue(vm.IsShopVisible);
        Assert.IsTrue(vm.Fort.IsVisible);
        Assert.AreEqual("Fortifications", vm.Fort.Title);
        Assert.IsTrue(vm.Fort.IsCareVisible);
        Assert.AreEqual("Field care", vm.Fort.CareName);
        StringAssert.Contains(vm.Fort.CareDetail, "75%");
        Assert.AreEqual("150 gold", vm.Fort.CarePrice);
        Assert.IsTrue(vm.Fort.CanBuyCare);
    }

    [TestMethod]
    public void InThePreparation_NoCareIsOnSale_NobodyIsHurtYet()
    {
        var vm = Panel(out var session, out _, intermission: false);

        Assert.IsFalse(vm.Fort.IsCareVisible);
        Assert.IsFalse(vm.Fort.CanBuyCare);
        Assert.AreEqual(FortResult.NotOffered, session.TryFieldCare());
        Assert.AreEqual(600, session.GetView()!.Gold);
    }

    [TestMethod]
    public void TheGroup_IsHidden_WhenTheBuildOffersNothing_OrTheShopCarriesNoRow()
    {
        var none = Panel(out _, out _, offered: FortOffer.None);
        var bare = Panel(out _, out _, fort: FortPlan.None);

        Assert.IsFalse(none.Fort.IsVisible);
        Assert.IsFalse(none.Fort.IsCareVisible);
        Assert.IsFalse(bare.Fort.IsVisible);
    }

    [TestMethod]
    public void TheGroup_IsHidden_WithTheShopClosed_AndDuringAWave()
    {
        var vm = Panel(out _, out var run);
        vm.ExecuteClose();
        Assert.IsFalse(vm.Fort.IsVisible, "the panel is closed");

        vm.ToggleShop();
        run.TryStartWave();
        vm.Refresh();
        Assert.IsFalse(vm.IsShopVisible);
        Assert.IsFalse(vm.Fort.IsVisible);
    }

    [TestMethod]
    public void BuyingCare_SpendsTheGold_AndTheRowStaysListedButSpent()
    {
        var vm = Panel(out var session, out _);

        vm.Fort.ExecuteBuyCare();

        Assert.AreEqual(450, session.GetView()!.Gold);
        Assert.AreEqual(string.Empty, vm.NoticeText);
        Assert.IsTrue(vm.Fort.IsCareVisible, "a used row is still on the panel");
        Assert.IsFalse(vm.Fort.CanBuyCare);
        Assert.AreEqual("Used", vm.Fort.CarePrice);
        Assert.AreEqual("Gold: 450", vm.GoldText);
    }

    [TestMethod]
    public void BuyingCareTwice_SaysItIsUsed_AndSpendsNothingMore()
    {
        var vm = Panel(out var session, out _);
        vm.Fort.ExecuteBuyCare();

        vm.Fort.ExecuteBuyCare();

        Assert.AreEqual(450, session.GetView()!.Gold);
        Assert.AreEqual("Field care is already used between these waves.", vm.NoticeText);
    }

    [TestMethod]
    public void WithTooLittleGold_TheRowIsListedButCannotBeBought_AndAClickSaysSo()
    {
        var vm = Panel(out _, out _, gold: 100);

        Assert.IsTrue(vm.Fort.IsCareVisible);
        Assert.IsFalse(vm.Fort.CanBuyCare);
        vm.Fort.ExecuteBuyCare();
        Assert.AreEqual("Not enough gold.", vm.NoticeText);
    }

    [TestMethod]
    public void TheSession_SellsCareThroughTheRun()
    {
        var vm = Panel(out var session, out var run);

        Assert.AreEqual(FortResult.Ok, session.TryFieldCare());
        Assert.AreEqual(FortResult.Used, session.TryFieldCare());
        Assert.AreEqual(150, run.Purse.SpentOn(PurseSink.FieldCare));
        Assert.IsNotNull(vm);
    }

    [TestMethod]
    public void ASessionWithNoRun_RefusesCare()
    {
        Assert.AreEqual(FortResult.RunOver, new LongDefeatSession().TryFieldCare());
    }
}
