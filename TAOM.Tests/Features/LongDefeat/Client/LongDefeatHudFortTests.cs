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
            run.Tick(new RunSnapshot(1f, 0, 0, 40, 0, 0, 1000) { Gates = new[] { new LdGateFacts(0, 15000f, 15000f, 15000f, false, false) } });

        Assert.IsTrue(!intermission || run.Phase == RunPhase.Intermission, "the first wave never cleared");
        session = new LongDefeatSession();
        session.Fill(run);
        var vm = new LongDefeatHudVM(session, () => { });
        vm.ToggleShop();
        return vm;
    }

    [TestMethod]
    [TestCategory("RequiresGame")]
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
    [TestCategory("RequiresGame")]
    public void InThePreparation_NoCareIsOnSale_NobodyIsHurtYet()
    {
        var vm = Panel(out var session, out _, intermission: false);

        Assert.IsFalse(vm.Fort.IsCareVisible);
        Assert.IsFalse(vm.Fort.CanBuyCare);
        Assert.AreEqual(FortResult.NotOffered, session.TryFieldCare());
        Assert.AreEqual(600, session.GetView()!.Gold);
    }

    [TestMethod]
    [TestCategory("RequiresGame")]
    public void TheGroup_IsHidden_WhenTheBuildOffersNothing_OrTheShopCarriesNoRow()
    {
        var none = Panel(out _, out _, offered: FortOffer.None);
        var bare = Panel(out _, out _, fort: FortPlan.None);

        Assert.IsFalse(none.Fort.IsVisible);
        Assert.IsFalse(none.Fort.IsCareVisible);
        Assert.IsFalse(bare.Fort.IsVisible);
    }

    [TestMethod]
    [TestCategory("RequiresGame")]
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
    [TestCategory("RequiresGame")]
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
    [TestCategory("RequiresGame")]
    public void BuyingCareTwice_SaysItIsUsed_AndSpendsNothingMore()
    {
        var vm = Panel(out var session, out _);
        vm.Fort.ExecuteBuyCare();

        vm.Fort.ExecuteBuyCare();

        Assert.AreEqual(450, session.GetView()!.Gold);
        Assert.AreEqual("Field care is already used between these waves.", vm.NoticeText);
    }

    [TestMethod]
    [TestCategory("RequiresGame")]
    public void WithTooLittleGold_TheRowIsListedButCannotBeBought_AndAClickSaysSo()
    {
        var vm = Panel(out _, out _, gold: 100);

        Assert.IsTrue(vm.Fort.IsCareVisible);
        Assert.IsFalse(vm.Fort.CanBuyCare);
        vm.Fort.ExecuteBuyCare();
        Assert.AreEqual("Not enough gold.", vm.NoticeText);
    }

    [TestMethod]
    [TestCategory("RequiresGame")]
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
        Assert.AreEqual(FortResult.RunOver, new LongDefeatSession().TryBuyGateLevel());
        Assert.AreEqual(FortResult.RunOver, new LongDefeatSession().TryRepairGates());
    }

    // ---- the gate rows (Slice 5) ----

    private static readonly FortPlan WithGate = new(new GatePlan(200, new[] { new GateLevelPlan(50f, 200), new GateLevelPlan(100f, 300), new GateLevelPlan(150f, 450) }), null, new CarePlan(0.75f, 150, 1), null);

    private static RunSnapshot WithGates(params LdGateFacts[] gates) => new(1f, 0, 0, 40, 0, 0, 1000) { Gates = gates };

    private static LdGateFacts Broken() => new(4, 0f, 15000f, 15000f, true, false);

    private static LdGateFacts Whole() => new(0, 15000f, 15000f, 15000f, false, false);

    [TestMethod]
    [TestCategory("RequiresGame")]
    public void TheShop_ListsTheGateRow_WithItsLevelPercentAndPrice_AndTheRepairRowBesideIt()
    {
        var vm = Panel(out _, out _, fort: WithGate);

        Assert.IsTrue(vm.Fort.IsVisible);
        Assert.IsTrue(vm.Fort.IsGateVisible);
        Assert.AreEqual("Reinforce the gate", vm.Fort.GateName);
        StringAssert.Contains(vm.Fort.GateDetail, "Level 1 of 3");
        StringAssert.Contains(vm.Fort.GateDetail, "50%");
        Assert.AreEqual("200 gold", vm.Fort.GatePrice);
        Assert.IsTrue(vm.Fort.CanBuyGate);
        Assert.IsTrue(vm.Fort.IsRepairVisible);
        Assert.AreEqual("Repair the gate", vm.Fort.RepairName);
        Assert.AreEqual("Nothing to repair", vm.Fort.RepairPrice, "no gate has been read as hurt");
        Assert.IsFalse(vm.Fort.CanRepair);
    }

    [TestMethod]
    [TestCategory("RequiresGame")]
    public void BuyingAGateLevel_SpendsTheGold_AndTheRowMovesToTheNextLevel_UntilTheTop()
    {
        var vm = Panel(out var session, out _, fort: WithGate, gold: 2000);

        vm.Fort.ExecuteBuyGate();
        StringAssert.Contains(vm.Fort.GateDetail, "Level 2 of 3");
        StringAssert.Contains(vm.Fort.GateDetail, "100%");
        Assert.AreEqual("300 gold", vm.Fort.GatePrice);
        vm.Fort.ExecuteBuyGate();
        vm.Fort.ExecuteBuyGate();

        Assert.AreEqual(2000 - 200 - 300 - 450, session.GetView()!.Gold);
        Assert.AreEqual("Fully reinforced", vm.Fort.GatePrice);
        Assert.IsFalse(vm.Fort.CanBuyGate);
        StringAssert.Contains(vm.Fort.GateDetail, "Level 3 of 3");
        Assert.IsTrue(vm.Fort.IsGateVisible, "a row at its top is still listed");

        vm.Fort.ExecuteBuyGate();
        Assert.AreEqual("The gate is fully reinforced.", vm.NoticeText);
    }

    [TestMethod]
    [TestCategory("RequiresGame")]
    public void ABrokenGate_MakesTheRepairRowLive_AtItsPrice_AndBuyingItSpendsThePrice()
    {
        var vm = Panel(out var session, out var run, fort: WithGate);
        run.Tick(WithGates(Broken()));
        vm.Refresh();

        Assert.AreEqual("200 gold", vm.Fort.RepairPrice);
        Assert.IsTrue(vm.Fort.CanRepair);
        vm.Fort.ExecuteRepair();

        Assert.AreEqual(400, session.GetView()!.Gold);
        Assert.AreEqual(string.Empty, vm.NoticeText);
        Assert.IsFalse(vm.Fort.CanRepair, "bought, and written on the next tick");
        Assert.AreEqual("Nothing to repair", vm.Fort.RepairPrice);
    }

    [TestMethod]
    [TestCategory("RequiresGame")]
    public void ARepairClickedOnAWholeGate_SaysItNeedsNone_AndSpendsNothing()
    {
        var vm = Panel(out var session, out var run, fort: WithGate);
        run.Tick(WithGates(Whole()));
        vm.Refresh();

        vm.Fort.ExecuteRepair();

        Assert.AreEqual("The gate needs no repair.", vm.NoticeText);
        Assert.AreEqual(600, session.GetView()!.Gold);
    }

    [TestMethod]
    [TestCategory("RequiresGame")]
    public void TheGateRows_AreHidden_WhenTheBuildOffersNoGate_OrTheShopCarriesNone_ButTheGroupStaysForCare()
    {
        var noneOffered = Panel(out _, out _, fort: WithGate, offered: new FortOffer(0, 0, true, 0));
        var carried = Panel(out _, out _);

        Assert.IsFalse(noneOffered.Fort.IsGateVisible);
        Assert.IsFalse(noneOffered.Fort.IsRepairVisible);
        Assert.IsTrue(noneOffered.Fort.IsVisible, "field care is still offered");
        Assert.IsFalse(carried.Fort.IsGateVisible);
        Assert.IsFalse(Panel(out _, out _, fort: WithGate, offered: FortOffer.None).Fort.IsVisible);
    }

    [TestMethod]
    [TestCategory("RequiresGame")]
    public void TheGateRows_CanBeBoughtInThePreparation_UnlikeCare()
    {
        var vm = Panel(out _, out _, fort: WithGate, intermission: false);

        Assert.IsTrue(vm.Fort.IsGateVisible);
        Assert.IsTrue(vm.Fort.CanBuyGate);
        Assert.IsFalse(vm.Fort.IsCareVisible);
    }
}
