using System;
using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Client.Features.LongDefeat;
using TAOM.Features.LongDefeat;
using TAOM.Features.LongDefeat.Domain;
using TAOM.Tests.Infrastructure;

namespace TAOM.Tests.Features.LongDefeat.Client;

/// <summary>
/// The Elven Volley on the HUD and the panel (phase3-plan.md D24, Slice 6): the shop row only buys, the key N looses the volley in a wave, the strip shows ready, the cooldown or why a shot was withheld, and
/// the row has its own wording for a volley that is fully upgraded.
/// </summary>
[TestClass]
public class LongDefeatHudVolleyTests
{
    private static readonly VolleyPlan Volley = new("ld_elven_arrow", 3f,
        new[] { new VolleyLevelPlan(350, 12, 45f, 8f, 90f, VolleyAimMode.Auto), new VolleyLevelPlan(300, 16, 40f, 8f, 90f, VolleyAimMode.Crosshair) });

    private static readonly LdVolleyOrigins Origins = new(new[] { new LdPoint(0f, 0f, 0f) });

    private static LdGateFacts[] Gates() => new[] { new LdGateFacts(0, 15000f, 15000f, 15000f, false, false) };

    private static RunSnapshot Snap(int attackers = 10) => new(1f, attackers, 0, 40, 0, 0, 1000) { Gates = Gates() };

    /// <summary>A panel in the preparation with the shop open and <paramref name="gold"/> in the purse; the volley row is carried when <paramref name="origins"/> are.</summary>
    private static LdPanel Panel(int gold = 1000, bool origins = true, FortOffer? offered = null)
    {
        var waves = new WavePlan(new[] { new WaveSpec(110, new[] { new WaveTroop("orc", FormationSlot.Infantry, 1) }) }, new WaveScaling(10, 300));
        var shop = new ShopPlan(gold, new[] { new ShopOffer("militia", "{=TAOM_LD_Offer_Militia}Westfold Militia", "fow_militia", FormationSlot.Infantry, 12, 180) }, new FortPlan(null, null, null, Volley));
        var rules = RunRules.Default with { WaveBonusBase = 0, WaveBonusPerWave = 0, EarlyGoldPerSecond = 0f, QuietSeconds = 1e6f, QuietAnySeconds = 1e6f, StrandedSeconds = 1e6f };
        var plan = new RunPlan("helms", new[] { new GarrisonRow("fow_militia", FormationSlot.Infantry, 30) }, waves, shop, rules, RunOptions.Default)
        {
            Offered = offered ?? FortOffer.Offered,
            Volley = origins ? Origins : null,
        };
        var run = new SurvivalRun(plan);
        run.Begin();
        var session = new LongDefeatSession();
        session.Fill(run);
        var vm = new LongDefeatHudVM(session, () => { });
        vm.ToggleShop();
        return new LdPanel(vm, session, run);
    }

    private sealed record LdPanel(LongDefeatHudVM Vm, LongDefeatSession Session, SurvivalRun Run)
    {
        /// <summary>Closes the shop and plays into the first wave, where the volley is ready at once.</summary>
        public void IntoTheWave()
        {
            Vm.ExecuteClose();
            for (var i = 0; i < 200 && Run.Phase != RunPhase.Wave; i++)
                Run.Tick(Snap());
            Assert.AreEqual(RunPhase.Wave, Run.Phase);
            Run.Tick(Snap());
            Vm.Refresh();
        }
    }

    // ---- the shop row ----

    [TestMethod]
    [TestCategory("RequiresGame")]
    public void TheShop_ListsTheElvenVolley_WithItsFirstLevelsDetailAndPrice()
    {
        var p = Panel();

        Assert.IsTrue(p.Vm.Fort.IsVisible);
        Assert.IsTrue(p.Vm.Fort.IsVolleyVisible);
        Assert.AreEqual("Elven Volley", p.Vm.Fort.VolleyName);
        StringAssert.Contains(p.Vm.Fort.VolleyDetail, "Level 1 of 2");
        StringAssert.Contains(p.Vm.Fort.VolleyDetail, "12 arrows every 45 s");
        StringAssert.Contains(p.Vm.Fort.VolleyDetail, "densest group");
        StringAssert.Contains(p.Vm.Fort.VolleyDetail, "N during a wave");
        Assert.AreEqual("350 gold", p.Vm.Fort.VolleyPrice);
        Assert.IsTrue(p.Vm.Fort.CanBuyVolley);
    }

    [TestMethod]
    [TestCategory("RequiresGame")]
    public void TheRow_IsBuyingOnly_APressBuysTheLevel_AndTheSecondLevelAimsAtTheCrosshair()
    {
        var p = Panel();

        p.Vm.Fort.ExecuteBuyVolley();

        Assert.AreEqual(650, p.Session.GetView()!.Gold);
        Assert.AreEqual(1, p.Run.VolleyLevel);
        Assert.AreEqual(string.Empty, p.Vm.NoticeText);
        StringAssert.Contains(p.Vm.Fort.VolleyDetail, "Level 2 of 2");
        StringAssert.Contains(p.Vm.Fort.VolleyDetail, "16 arrows every 40 s");
        StringAssert.Contains(p.Vm.Fort.VolleyDetail, "where you look");
        Assert.AreEqual("300 gold", p.Vm.Fort.VolleyPrice);
        Assert.AreEqual(0, p.Run.WaveSeconds, "buying fires nothing");
    }

    [TestMethod]
    [TestCategory("RequiresGame")]
    public void TheTopLevel_StaysListedAsFullyUpgraded_AndASpareBuyHasItsOwnWording()
    {
        var p = Panel();
        p.Vm.Fort.ExecuteBuyVolley();
        p.Vm.Fort.ExecuteBuyVolley();

        Assert.IsTrue(p.Vm.Fort.IsVolleyVisible);
        Assert.AreEqual("Fully upgraded", p.Vm.Fort.VolleyPrice);
        Assert.IsFalse(p.Vm.Fort.CanBuyVolley);

        p.Vm.Fort.ExecuteBuyVolley();

        Assert.AreEqual("The Elven Volley is fully upgraded.", p.Vm.NoticeText, "not the gate's wording");
    }

    [TestMethod]
    [TestCategory("RequiresGame")]
    public void TooLittleGold_RefusesAndDisablesTheRow()
    {
        var p = Panel(gold: 200);

        Assert.IsFalse(p.Vm.Fort.CanBuyVolley);
        p.Vm.Fort.ExecuteBuyVolley();

        Assert.AreEqual("Not enough gold.", p.Vm.NoticeText);
        Assert.AreEqual(200, p.Session.GetView()!.Gold);
    }

    [TestMethod]
    [TestCategory("RequiresGame")]
    public void TheRow_IsHidden_WithNoRecordedOrigin_OrWhenTheBuildOffersNoVolley()
    {
        Assert.IsFalse(Panel(origins: false).Vm.Fort.IsVolleyVisible, "Helm's Deep records no origin yet");
        Assert.IsFalse(Panel(offered: new FortOffer(3, 0, true, 0)).Vm.Fort.IsVolleyVisible);
    }

    [TestMethod]
    [TestCategory("RequiresGame")]
    public void TheGroup_IsHidden_WhenTheVolleyIsTheOnlyRow_AndItIsHidden()
    {
        Assert.IsFalse(Panel(origins: false).Vm.Fort.IsVisible);
    }

    // ---- the strip ----

    [TestMethod]
    [TestCategory("RequiresGame")]
    public void InAWave_TheStripShowsReady_ThenTheCooldown_AndTheKeyHintIsOnTheHintLine()
    {
        var p = Panel();
        p.Vm.Fort.ExecuteBuyVolley();
        p.IntoTheWave();

        Assert.AreEqual("Elven Volley ready", p.Vm.VolleyText);
        StringAssert.Contains(p.Vm.HintText, "N  Loose the Elven Volley");

        Assert.AreEqual(VolleyArm.Armed, p.Session.TryVolley());
        var order = p.Run.Tick(Snap()).FireVolleys[0];
        p.Run.VolleyFired(order.Source, 12);
        p.Run.Tick(Snap());
        p.Vm.Refresh();

        StringAssert.StartsWith(p.Vm.VolleyText, "Elven Volley in ");
        StringAssert.EndsWith(p.Vm.VolleyText, " s");
    }

    [TestMethod]
    [TestCategory("RequiresGame")]
    public void AWithheldShot_ShowsNoAttackersInReach_ForThreeSeconds_ThenTheStripReadsReadyAgain()
    {
        var p = Panel();
        p.Vm.Fort.ExecuteBuyVolley();
        p.IntoTheWave();
        p.Session.TryVolley();
        var order = p.Run.Tick(Snap()).FireVolleys[0];
        p.Run.VolleyWithheld(order.Source, VolleyWithheldReason.NoTargets);
        p.Vm.Refresh();

        Assert.AreEqual("No attackers in reach", p.Vm.VolleyText);

        for (var i = 0; i < 4; i++)
            p.Run.Tick(Snap());
        p.Vm.Refresh();

        Assert.AreEqual("Elven Volley ready", p.Vm.VolleyText);
    }

    [TestMethod]
    [TestCategory("RequiresGame")]
    public void OtherWithheldReasons_AreSaidPlainly()
    {
        var p = Panel();
        p.Vm.Fort.ExecuteBuyVolley();
        p.IntoTheWave();
        p.Session.TryVolley();
        var order = p.Run.Tick(Snap()).FireVolleys[0];
        p.Run.VolleyWithheld(order.Source, VolleyWithheldReason.NoOwner);
        p.Vm.Refresh();

        Assert.AreEqual("The volley cannot be loosed now", p.Vm.VolleyText);
    }

    [TestMethod]
    [TestCategory("RequiresGame")]
    public void TheStrip_IsEmpty_WhenTheVolleyIsNotOwned_AndBetweenWaves()
    {
        var p = Panel();
        p.IntoTheWave();
        Assert.AreEqual(string.Empty, p.Vm.VolleyText, "not owned");
        Assert.IsFalse(p.Vm.HintText.Contains("Elven Volley"), p.Vm.HintText);

        var owned = Panel();
        owned.Vm.Fort.ExecuteBuyVolley();
        owned.Vm.Refresh();
        Assert.AreEqual(string.Empty, owned.Vm.VolleyText, "the preparation");
    }

    // ---- the key ----

    [TestMethod]
    [TestCategory("RequiresGame")]
    public void TheKey_InAWave_ArmsTheVolley_AndSaysNothing()
    {
        var p = Panel();
        p.Vm.Fort.ExecuteBuyVolley();
        p.IntoTheWave();

        p.Vm.FireVolley();

        Assert.AreEqual(string.Empty, p.Vm.NoticeText);
        Assert.AreEqual(1, p.Run.Tick(Snap()).FireVolleys.Count, "armed: the next tick hands the director the order");
    }

    [TestMethod]
    [TestCategory("RequiresGame")]
    public void TheKey_Refuses_WhenNotOwned_OnCooldown_OrBetweenWaves()
    {
        var notOwned = Panel();
        notOwned.IntoTheWave();
        notOwned.Vm.FireVolley();
        Assert.AreEqual("Buy the Elven Volley between waves first.", notOwned.Vm.NoticeText);

        var between = Panel();
        between.Vm.Fort.ExecuteBuyVolley();
        between.Vm.FireVolley();
        Assert.AreEqual("The Elven Volley is loosed during a wave.", between.Vm.NoticeText);

        var cooling = Panel();
        cooling.Vm.Fort.ExecuteBuyVolley();
        cooling.IntoTheWave();
        cooling.Vm.FireVolley();
        var order = cooling.Run.Tick(Snap()).FireVolleys[0];
        cooling.Run.VolleyFired(order.Source, 12);
        cooling.Vm.FireVolley();
        Assert.AreEqual("The Elven Volley is not ready yet.", cooling.Vm.NoticeText);
    }

    [TestMethod]
    [TestCategory("RequiresGame")]
    public void TheKey_SaysNothing_WhereNoVolleyIsForSale_SoShippedHelmsDeepPlaysAsBefore()
    {
        // Helm's Deep records no origin yet, so the row is hidden: a press must not send the player to a shop row that does not exist.
        var noOrigin = Panel(origins: false);
        noOrigin.IntoTheWave();
        noOrigin.Vm.FireVolley();
        Assert.AreEqual(string.Empty, noOrigin.Vm.NoticeText);

        var notOffered = Panel(offered: new FortOffer(3, 0, true, 0));
        notOffered.IntoTheWave();
        notOffered.Vm.FireVolley();
        Assert.AreEqual(string.Empty, notOffered.Vm.NoticeText);
    }

    [TestMethod]
    public void TheKeyN_IsReadByTheHost_OnlyWhileNoPanelIsOpen_AndNeverFromThePanel()
    {
        var host = RepoPaths.ReadSource("Client/Features/LongDefeat/LongDefeatPanelHost.cs", stripComments: true);
        var keys = host.Substring(host.IndexOf("internal void ReadKeys(", StringComparison.Ordinal));
        keys = keys.Substring(0, keys.IndexOf("internal static bool ProbeLoads", StringComparison.Ordinal));

        var noPanel = keys.IndexOf("if (_layer != null)", StringComparison.Ordinal);
        var n = keys.IndexOf("InputKey.N", StringComparison.Ordinal);
        Assert.IsTrue(noPanel >= 0 && n > noPanel, "N is read after the open panel returns, as B and H are: " + keys);
        StringAssert.Contains(keys, "vm.FireVolley()");
        Assert.IsFalse(File.ReadAllText(RepoPaths.RepoPath("Client", "Features", "LongDefeat", "LongDefeatFortVM.cs")).Contains("TryVolley"), "the panel only buys (D24)");
    }
}
