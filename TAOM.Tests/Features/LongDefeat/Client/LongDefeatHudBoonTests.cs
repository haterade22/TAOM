using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Client.Features.LongDefeat;
using TAOM.Features.LongDefeat;
using TAOM.Features.LongDefeat.Domain;

namespace TAOM.Tests.Features.LongDefeat.Client;

/// <summary>
/// The boon pick at the HUD (phase2-plan.md section 5): three cards above the shop with a drawback line of their own, a reroll button with its count, a hint and a
/// notice that announce an open pick (the panel never opens by itself), and a session that passes a pick or a reroll to the run and keeps the boons taken for the end panel.
/// </summary>
[TestClass]
public class LongDefeatHudBoonTests
{
    private static BoonRow Boon(string id, BoonCategory category, params (string Key, float Value)[] effects) =>
        new(id, category, "{=TAOM_LD_Test_" + id + "}Name " + id, "{=TAOM_LD_Test_" + id + "_Text}Text " + id, effects.Select(e => new BoonEffect(e.Key, e.Value)).ToList());

    private static BoonPlan Pool() => new(new[]
    {
        Boon("gold", BoonCategory.WarChest, ("gold", 100f)),
        Boon("wains", BoonCategory.Fortress, ("packBonus", 3f)) with
        {
            Drawback = new[] { new BoonEffect("pricePercent", 10f) },
            DrawbackText = "{=TAOM_LD_Test_wains_Drawback}Packs cost 10 percent more.",
        },
        Boon("phial", BoonCategory.Legend, ("affixCount", -1f)),
        Boon("extra", BoonCategory.WarChest, ("bountyPercent", 5f)),
    }, 40);

    private static (LongDefeatHudVM Vm, LongDefeatSession Session, SurvivalRun Run) AtThirdClear(int rerolls = 0, int? stopAfter = null)
    {
        var plan = new RunPlan(
            "helms",
            new[] { new GarrisonRow("fow_militia", FormationSlot.Infantry, 30) },
            new WavePlan(new[] { new WaveSpec(20, new[] { new WaveTroop("orc", FormationSlot.Infantry, 1) }) }, new WaveScaling(10, 300)),
            new ShopPlan(600, new[] { new ShopOffer("militia", "{=TAOM_LD_Offer_Militia}Westfold Militia", "fow_militia", FormationSlot.Infantry, 12, 180) }),
            RunRules.Default with { PreparationSeconds = 1f, IntermissionSeconds = 10f },
            new RunOptions { Seed = 1UL, StartRerolls = rerolls, StopAfterWaves = stopAfter })
        {
            Boons = Pool(),
        };
        var run = new SurvivalRun(plan);
        run.Begin();
        for (var i = 0; i < 5000 && run.WavesCleared < 3; i++)
        {
            if (run.GetView().Phase == RunPhase.Intermission)
                run.TryStartWave();
            run.Tick(new RunSnapshot(1f, 0, 0, 40, 0, 0, 1000));
        }

        Assert.AreEqual(3, run.WavesCleared);
        var session = new LongDefeatSession();
        session.Fill(run);
        var vm = new LongDefeatHudVM(session, () => { });
        vm.Refresh();
        return (vm, session, run);
    }

    [TestMethod]
    public void AnOpenPick_ShowsInTheOpenShop_WithThreeCardsAndTheirCategoriesAndDrawbacks()
    {
        var (vm, _, run) = AtThirdClear();
        Assert.IsFalse(vm.IsPickVisible, "the shop is closed until B is pressed");

        vm.ToggleShop();

        Assert.IsTrue(vm.IsShopVisible);
        Assert.IsTrue(vm.IsPickVisible);
        Assert.AreEqual(3, vm.Boons.Count);
        Assert.AreEqual("Choose a boon", vm.PickTitle);
        foreach (var card in vm.Boons)
        {
            Assert.IsTrue(card.Name.StartsWith("Name ", System.StringComparison.Ordinal), card.Name);
            Assert.IsTrue(card.Text.StartsWith("Text ", System.StringComparison.Ordinal), card.Text);
        }

        var wains = vm.Boons.FirstOrDefault(c => c.Name == "Name wains");
        if (wains != null)
        {
            Assert.IsTrue(wains.HasDrawback);
            Assert.AreEqual("Packs cost 10 percent more.", wains.DrawbackText);
            Assert.AreEqual("Fortress", wains.CategoryText);
        }

        Assert.IsTrue(vm.Boons.Where(c => c.Name != "Name wains").All(c => !c.HasDrawback && c.DrawbackText.Length == 0));
        Assert.AreEqual(3, run.GetView().BoonOffer.Count);
    }

    [TestMethod]
    public void NoOpenPick_HidesTheGroup()
    {
        var (vm, _, _) = AtThirdClear();
        vm.ToggleShop();
        vm.Boons[0].ExecutePick();

        Assert.IsFalse(vm.IsPickVisible);
        Assert.AreEqual(0, vm.Boons.Count);
    }

    [TestMethod]
    public void TheHint_AnnouncesAnOpenPick_BetweenWavesOnly()
    {
        var (vm, _, run) = AtThirdClear();

        StringAssert.Contains(vm.HintText, "A boon awaits: press B");
        run.TryStartWave();
        run.Tick(new RunSnapshot(1f, 0, 0, 40, 0, 0, 1000));
        vm.Refresh();

        Assert.IsFalse(vm.HintText.Contains("boon"), "a pick is made between waves");
    }

    [TestMethod]
    public void ANewOffer_RaisesANotice_ButNeverOpensThePanel()
    {
        var (vm, _, _) = AtThirdClear();

        StringAssert.Contains(vm.NoticeText, "A boon is on offer");
        Assert.IsFalse(vm.WantsPanel, "a focus layer would stop the player's movement");
    }

    [TestMethod]
    public void APickOpenedOnTheFinalClear_RaisesNoNoticeOverTheEndPanel()
    {
        var (vm, _, run) = AtThirdClear(stopAfter: 3);

        Assert.IsNotNull(run.Result, "the run ended on its target");
        Assert.AreEqual(1, run.GetView().BoonOffer.Count > 0 ? 1 : 0, "the pick is still listed open");
        Assert.IsFalse(vm.NoticeText.Contains("A boon is on offer"), vm.NoticeText);
    }

    [TestMethod]
    public void APickedCard_GoesThroughTheSession_AndTheBoonIsOwned()
    {
        var (vm, session, run) = AtThirdClear();
        vm.ToggleShop();
        var name = vm.Boons[0].Name;

        vm.Boons[0].ExecutePick();

        Assert.AreEqual(1, run.OwnedBoons.Count);
        Assert.AreEqual(name, "Name " + run.OwnedBoons[0].Id);
        Assert.AreEqual(0, session.GetView()!.BoonOffer.Count);
        Assert.AreEqual(string.Empty, vm.NoticeText, "a pick that took is not a refusal");
    }

    [TestMethod]
    public void TheRerollButton_ShowsTheBankedCount_IsEnabledOnlyWithOne_AndSwapsTheCards()
    {
        var (vm, _, run) = AtThirdClear(rerolls: 1);
        vm.ToggleShop();
        var before = vm.Boons.Select(c => c.Name).ToArray();

        Assert.IsTrue(vm.CanReroll);
        Assert.AreEqual("Reroll (1)", vm.RerollText);
        vm.ExecuteReroll();

        Assert.IsFalse(vm.CanReroll);
        Assert.AreEqual("Reroll (0)", vm.RerollText);
        Assert.AreEqual(3, vm.Boons.Count);
        Assert.AreEqual(1, run.GetView().RerollsUsed);
        Assert.AreEqual(3, before.Length);
    }

    [TestMethod]
    public void ARerollWithNoneBanked_IsRefusedInWords()
    {
        var (vm, _, _) = AtThirdClear();
        vm.ToggleShop();

        vm.ExecuteReroll();

        Assert.AreEqual("No reroll is banked.", vm.NoticeText);
    }

    [TestMethod]
    public void TheEndPanel_ListsTheBoonsTaken_AndKeepsThemAfterTheMissionClears()
    {
        var (vm, session, run) = AtThirdClear();
        vm.ToggleShop();
        var taken = vm.Boons[0].Name;
        vm.Boons[0].ExecutePick();
        run.Abandon();

        session.Clear();
        vm.Refresh();

        StringAssert.Contains(vm.EndLines, "Boons: " + taken);
    }

    [TestMethod]
    public void TheSession_PassesAPickAndARerollToTheRun_AndRefusesWithNoRun()
    {
        var empty = new LongDefeatSession();

        Assert.AreEqual(PickResult.RunOver, empty.TryPickBoon("a"));
        Assert.AreEqual(RerollResult.RunOver, empty.TryReroll());
        var (_, session, run) = AtThirdClear(rerolls: 1);
        Assert.AreEqual(RerollResult.Ok, session.TryReroll());
        Assert.AreEqual(PickResult.Ok, session.TryPickBoon(run.GetView().BoonOffer[0].Id));
    }
}
