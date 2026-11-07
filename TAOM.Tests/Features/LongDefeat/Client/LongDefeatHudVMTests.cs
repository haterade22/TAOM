using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Client.Features.LongDefeat;
using TAOM.Features.LongDefeat;
using TAOM.Features.LongDefeat.Domain;

namespace TAOM.Tests.Features.LongDefeat.Client;

/// <summary>The HUD and panel view model over a real <see cref="LongDefeatSession"/> and a real run: one shop offer (180 gold, a pack of 12), 600 starting gold.</summary>
[TestClass]
[TestCategory("RequiresGame")]
public class LongDefeatHudVMTests
{
    private int _leaves;
    private SurvivalRun _run = null!;

    private static SurvivalRun NewRun(int startingGold)
    {
        var waves = new WavePlan(
            new[] { new WaveSpec(110, new[] { new WaveTroop("orc", FormationSlot.Infantry, 1) }) },
            new WaveScaling(10, 300));
        var shop = new ShopPlan(startingGold, new[] { new ShopOffer("militia", "{=TAOM_LD_Offer_Militia}Westfold Militia", "fow_militia", FormationSlot.Infantry, 12, 180) });
        var plan = new RunPlan("helms", new[] { new GarrisonRow("fow_militia", FormationSlot.Infantry, 30) }, waves, shop, RunRules.Default, RunOptions.Default);
        var run = new SurvivalRun(plan);
        run.Begin();
        return run;
    }

    private LongDefeatHudVM Panel(out LongDefeatSession session, int startingGold = 600)
    {
        _run = NewRun(startingGold);
        session = new LongDefeatSession();
        session.Fill(_run);
        var vm = new LongDefeatHudVM(session, () => _leaves++);
        vm.Refresh();
        return vm;
    }

    [TestMethod]
    public void Preparation_ShowsTheStrip_TheTimer_AndTheShopHint()
    {
        var vm = Panel(out _);

        Assert.AreEqual("Preparation", vm.WaveText);
        Assert.AreEqual("Gold: 600", vm.GoldText);
        Assert.AreEqual("1:30", vm.TimerText);
        StringAssert.Contains(vm.HintText, "B");
        Assert.IsFalse(vm.IsShopVisible, "the shop opens on B, not by itself");
        Assert.IsFalse(vm.IsEndVisible);
        Assert.IsFalse(vm.WantsPanel);
    }

    [TestMethod]
    public void TheStrip_ShowsTheSpeed_AndTheKeyThatStepsIt()
    {
        var vm = Panel(out _);

        Assert.AreEqual("Speed: 1x   M  Next speed", vm.SpeedText);
        Assert.AreEqual("Game speed", vm.SpeedTitle);
    }

    [TestMethod]
    public void CycleSpeed_StepsTheSession_AndTheStripFollows()
    {
        var vm = Panel(out var session);

        vm.CycleSpeed();
        Assert.AreEqual(2, session.Speed);
        StringAssert.Contains(vm.SpeedText, "Speed: 2x");

        vm.CycleSpeed();
        vm.CycleSpeed();
        Assert.AreEqual(10, session.Speed);
        StringAssert.Contains(vm.SpeedText, "Speed: 10x");

        vm.CycleSpeed();
        Assert.AreEqual(1, session.Speed, "after 10x the key wraps to 1x");
    }

    [TestMethod]
    public void TheSpeedButtons_AreTheFourLevels_WithTheCurrentOneMarked()
    {
        var vm = Panel(out _);

        CollectionAssert.AreEqual(new[] { "1x", "2x", "4x", "10x" }, vm.Speeds.Select(s => s.Text).ToArray());
        CollectionAssert.AreEqual(new[] { true, false, false, false }, vm.Speeds.Select(s => s.IsCurrent).ToArray());
    }

    [TestMethod]
    public void ASpeedButton_SetsThatSpeed_AndTheMarkMoves()
    {
        var vm = Panel(out var session);

        vm.Speeds[2].ExecuteSelect();

        Assert.AreEqual(4, session.Speed);
        CollectionAssert.AreEqual(new[] { false, false, true, false }, vm.Speeds.Select(s => s.IsCurrent).ToArray());
        StringAssert.Contains(vm.SpeedText, "Speed: 4x");
    }

    [TestMethod]
    public void TheMark_FollowsTheKeyToo_AndTheSpeedHoldsThroughAWave()
    {
        var vm = Panel(out var session);
        vm.CycleSpeed();
        vm.CycleSpeed();

        _run.TryStartWave();
        vm.Refresh();

        Assert.AreEqual(4, session.Speed);
        CollectionAssert.AreEqual(new[] { false, false, true, false }, vm.Speeds.Select(s => s.IsCurrent).ToArray());
    }

    [TestMethod]
    public void ANewRun_ReadsAsOneX()
    {
        var vm = Panel(out var session);
        vm.Speeds[3].ExecuteSelect();

        session.Fill(NewRun(600));
        vm.Refresh();

        Assert.AreEqual(1, session.Speed);
        StringAssert.Contains(vm.SpeedText, "Speed: 1x");
        Assert.IsTrue(vm.Speeds[0].IsCurrent);
    }

    [TestMethod]
    public void ToggleShop_OpensItInAPreparation()
    {
        var vm = Panel(out _);

        vm.ToggleShop();
        Assert.IsTrue(vm.IsShopVisible);
        Assert.IsTrue(vm.WantsPanel);
        Assert.AreEqual(1, vm.Offers.Count);
        Assert.AreEqual("Westfold Militia", vm.Offers[0].Name);
        Assert.AreEqual("Pack of 12", vm.Offers[0].PackText);
        Assert.AreEqual("180 gold", vm.Offers[0].PriceText);
        Assert.IsTrue(vm.Offers[0].CanAfford);
    }

    [TestMethod]
    public void TheShop_ClosesThroughExecuteClose_TheOnlyWayTheViewClosesIt()
    {
        var vm = Panel(out _);
        vm.ToggleShop();
        Assert.IsTrue(vm.WantsPanel);

        vm.ExecuteClose();

        Assert.IsFalse(vm.IsShopVisible);
        Assert.IsFalse(vm.WantsPanel);
    }

    [TestMethod]
    public void ToggleShop_InAWave_OnlyGivesANotice()
    {
        var vm = Panel(out var session);
        session.TryStartWave();
        vm.Refresh();

        vm.ToggleShop();

        Assert.IsFalse(vm.IsShopVisible);
        Assert.IsTrue(vm.NoticeText.Length > 0);
    }

    [TestMethod]
    public void TheShop_ClosesByItself_WhenAWaveStarts_AndHasNoTimerInTheWave()
    {
        var vm = Panel(out var session);
        vm.ToggleShop();
        Assert.IsTrue(vm.IsShopVisible);

        session.TryStartWave();
        vm.Refresh();

        Assert.AreEqual("Wave 1", vm.WaveText);
        Assert.IsFalse(vm.IsShopVisible);
        Assert.AreEqual(string.Empty, vm.TimerText, "a wave has no timer");
    }

    [TestMethod]
    public void BuyingAnOffer_SpendsGold_AndARefusalBecomesANotice()
    {
        var vm = Panel(out var session, startingGold: 400);
        vm.ToggleShop();

        vm.Offers[0].ExecuteBuy();
        Assert.AreEqual("Gold: 220", vm.GoldText);
        Assert.AreEqual(string.Empty, vm.NoticeText);
        Assert.IsTrue(vm.Offers[0].CanAfford);

        vm.Offers[0].ExecuteBuy();
        Assert.AreEqual("Gold: 40", vm.GoldText);
        Assert.IsFalse(vm.Offers[0].CanAfford);

        vm.Offers[0].ExecuteBuy();
        Assert.AreEqual(40, session.GetView()!.Gold);
        StringAssert.Contains(vm.NoticeText, "gold");
    }

    [TestMethod]
    public void TheNotice_FadesAfterItsSeconds()
    {
        var vm = Panel(out _, startingGold: 0);
        vm.ToggleShop();
        vm.Offers[0].ExecuteBuy();
        Assert.IsTrue(vm.NoticeText.Length > 0);

        vm.Tick(LongDefeatHudVM.NoticeSeconds - 0.5f);
        Assert.IsTrue(vm.NoticeText.Length > 0);
        vm.Tick(1f);
        Assert.AreEqual(string.Empty, vm.NoticeText);
    }

    [TestMethod]
    public void ANaNStep_IsNoTimePassing()
    {
        var vm = Panel(out _, startingGold: 0);
        vm.ToggleShop();
        vm.Offers[0].ExecuteBuy();

        vm.Tick(float.NaN);

        Assert.IsTrue(vm.NoticeText.Length > 0, "a NaN step is no time passing");
        vm.Tick(LongDefeatHudVM.NoticeSeconds + 1f);
        Assert.AreEqual(string.Empty, vm.NoticeText);
    }

    [TestMethod]
    public void TheHorn_StartsTheWave_AndPaysTheEarlyBonus()
    {
        var vm = Panel(out var session);

        vm.SoundHorn();
        vm.Refresh();

        Assert.AreEqual("Wave 1", vm.WaveText);
        Assert.IsTrue(session.GetView()!.Gold > 600, "an early start earns gold");
    }

    [TestMethod]
    public void TheHorn_InAWave_GivesANotice_AndStartsNothing()
    {
        var vm = Panel(out var session);
        vm.SoundHorn();
        vm.Refresh();
        var gold = session.GetView()!.Gold;

        vm.SoundHorn();

        Assert.AreEqual(gold, session.GetView()!.Gold);
        Assert.IsTrue(vm.NoticeText.Length > 0);
    }

    [TestMethod]
    public void TheKeepBar_IsHiddenInAPreparation_AndNeverWiderThanItsTrack()
    {
        var vm = Panel(out _);
        Assert.IsFalse(vm.IsKeepVisible);
        Assert.AreEqual(0f, vm.KeepFillWidth);

        Assert.AreEqual(0f, LongDefeatHudVM.KeepWidth(float.NaN));
        Assert.AreEqual(0f, LongDefeatHudVM.KeepWidth(-1f));
        Assert.AreEqual(LongDefeatHudVM.KeepTrackWidth, LongDefeatHudVM.KeepWidth(7f));
        Assert.AreEqual(LongDefeatHudVM.KeepTrackWidth / 2f, LongDefeatHudVM.KeepWidth(0.5f), 0.001f);
    }

    [TestMethod]
    public void TheKeepBar_ShowsInAWave_WhenAttackersHoldTheKeep()
    {
        var vm = Panel(out var session);
        session.TryStartWave();
        _run.Tick(new RunSnapshot(5f, 20, 0, 30, 20, 0, 100));

        vm.Refresh();

        Assert.IsTrue(_run.GetView().KeepProgress > 0f, "the run must have progressed for this test to mean anything");
        Assert.IsTrue(vm.IsKeepVisible);
        Assert.IsTrue(vm.KeepFillWidth > 0f);
        Assert.AreEqual(LongDefeatHudText.Keep(), vm.KeepText);
    }

    [TestMethod]
    public void TheDefendersLine_CountsTheSoldiersStillToMuster()
    {
        var vm = Panel(out _);
        var view = _run.GetView();

        Assert.IsTrue(view.DefendersPending > 0, "the muster has batches left after Begin");
        Assert.AreEqual(LongDefeatHudText.Defenders(view.DefendersStanding + view.DefendersPending), vm.DefendersText);
        Assert.AreNotEqual(LongDefeatHudText.Defenders(view.DefendersStanding), vm.DefendersText);
    }

    [TestMethod]
    public void AnEndedRun_ShowsTheEndPanel_WithItsNumbersAndTheBest_AndLeaveCallsBack()
    {
        var vm = Panel(out var session);
        session.RecordBest(new BestRun(5, 900));
        vm.ToggleShop();

        _run.Abandon();
        vm.Refresh();

        Assert.IsTrue(vm.IsEndVisible);
        Assert.IsFalse(vm.IsShopVisible, "an ended run has no shop");
        Assert.IsTrue(vm.WantsPanel);
        Assert.AreEqual(LongDefeatHudText.EndTitle(RunEndReason.Abandoned), vm.EndTitle);
        Assert.AreEqual("Waves cleared: 0\nKills: 0\nDefenders lost in cleared waves: 0", vm.EndLines);
        Assert.AreEqual("", vm.ReportText, "no report was written, so the panel names none");
        Assert.AreEqual("Best run: waves cleared 5, kills 900", vm.BestText);

        vm.ExecuteLeave();
        Assert.AreEqual(1, _leaves);
    }

    [TestMethod]
    public void TheEndPanel_NamesTheReportFile_WithoutItsFolder()
    {
        var vm = Panel(out var session);
        session.RecordReport(System.IO.Path.Combine("C:\\Users\\someone\\Logs", "long_defeat_report_x_ld1.json"));
        _run.Abandon();

        vm.Refresh();

        StringAssert.Contains(vm.ReportText, "long_defeat_report_x_ld1.json");
        Assert.IsFalse(vm.ReportText.Contains("someone"), "a clip of this panel must not show a Windows user name");
    }

    [TestMethod]
    public void AfterTheMissionIsCleared_TheEndPanelStillShowsTheResult()
    {
        var vm = Panel(out var session);
        _run.Abandon();
        session.Clear();

        vm.Refresh();

        Assert.IsTrue(vm.IsEndVisible, "the result outlives Clear");
        Assert.AreEqual("No best run yet", vm.BestText);
    }

    [TestMethod]
    public void ClosingTheEndPanel_DismissesIt()
    {
        var vm = Panel(out _);
        _run.Abandon();
        vm.Refresh();

        vm.ExecuteClose();
        vm.Refresh();

        Assert.IsFalse(vm.WantsPanel);
    }

    [TestMethod]
    public void NoSession_ShowsNothing_AndRefusesEveryCommandQuietly()
    {
        var vm = new LongDefeatHudVM(new LongDefeatSession(), () => _leaves++);

        vm.Refresh();
        vm.ToggleShop();
        vm.SoundHorn();

        Assert.IsFalse(vm.WantsPanel);
        Assert.AreEqual(string.Empty, vm.WaveText);
        Assert.AreEqual(0, vm.Offers.Count);
    }
}
