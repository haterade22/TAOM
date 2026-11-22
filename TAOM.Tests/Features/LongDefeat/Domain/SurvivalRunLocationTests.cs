using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.LongDefeat.Domain;

namespace TAOM.Tests.Features.LongDefeat.Domain;

/// <summary>
/// The buys at the two places of phase4-plan.md (Slice 2): every result of <c>TryBuyGear</c> and <c>TryBuyEngine</c>, the near rule read from the snapshot's lord, the price factor (D43), the purse's
/// sinks and the purchase list, the effects handed out on the next tick with the one-tick hold of the wave start, and the errand the unattended bot walks (D44).
/// </summary>
[TestClass]
public class SurvivalRunLocationTests
{
    private static readonly GearSet Mail = new("armour_1", "{=K_M}Mail", "", GearKind.Armour, null, new[] { new LdKitSlot("Body", "mail"), new LdKitSlot("Head", "helm") }, 250);
    private static readonly GearSet Plate = new("armour_2", "{=K_P}Plate", "", GearKind.Armour, null, new[] { new LdKitSlot("Body", "plate") }, 400);
    private static readonly GearSet Blade = new("weapons_sword", "{=K_B}Blade", "", GearKind.Weapons, new[] { "sword" }, new[] { new LdKitSlot("Item0", "long_blade") }, 200);
    private static readonly GearSet Bow = new("weapons_archer", "{=K_W}Bow", "", GearKind.Weapons, new[] { "archer" }, new[] { new LdKitSlot("Item0", "war_bow") }, 200);

    private static readonly ArmouryPlan Armoury = new(4f, new[] { Mail, Plate, Blade, Bow });
    private static readonly YardPlan Yard = new(6f, new[] { new EngineOffer("mangonel", 450), new EngineOffer("trebuchet", 700) });
    private static readonly LocationOffer All = new(true, true, true);
    private static readonly MusterFrame ArmouryAt = new(10f, 10f, 5f, 0f, 1f);
    private static readonly MusterFrame YardAt = new(60f, 10f, 5f, 0f, 1f);
    private static readonly LdLoadout Sword = new("sword", false, new[] { new LdKitSlot("Item0", "kit_sword"), new LdKitSlot("Item1", "kit_shield"), new LdKitSlot("Body", "kit_tunic") });

    private static readonly IReadOnlyList<LdSite> Sites = new[]
    {
        new LdSite(0, new LdPoint(70f, 20f, 10f), new[] { "mangonel" }),
        new LdSite(1, new LdPoint(90f, 20f, 10f), new[] { "mangonel", "trebuchet" }),
    };

    private static BoonRow Boon(string id, params (string Key, float Value)[] effects) =>
        new(id, BoonCategory.WarChest, "{=K_" + id + "}" + id, "{=T_" + id + "}text", effects.Select(e => new BoonEffect(e.Key, e.Value)).ToList());

    private static RunPlan Plan(LocationOffer? offered = null, int gold = 5000, bool armoury = true, bool yard = true, bool armouryPoint = true, bool yardPoint = true,
        LdLoadout? loadout = null, bool noLoadout = false, IReadOnlyList<string>? hidden = null, RunRules? rules = null, BoonPlan? boons = null) =>
        new(
            "helms",
            new[] { new GarrisonRow("fow_militia", FormationSlot.Infantry, 30) },
            new WavePlan(new[] { new WaveSpec(20, new[] { new WaveTroop("orc", FormationSlot.Infantry, 3) }) }, new WaveScaling(10, 300)),
            new ShopPlan(gold, new[] { new ShopOffer("militia", "Militia", "fow_militia", FormationSlot.Infantry, 12, 180) }, null,
                new LocationsPlan(armoury ? Armoury : null, yard ? Yard : null)),
            rules ?? RunRules.Default with { PreparationSeconds = 50f, IntermissionSeconds = 60f },
            new RunOptions { Seed = 1UL })
        {
            Bounties = new Dictionary<string, int> { ["orc"] = 4 },
            Locations = offered ?? All,
            ArmouryPoint = armouryPoint ? ArmouryAt : null,
            YardPoint = yardPoint ? YardAt : null,
            Loadout = noLoadout ? null : loadout ?? Sword,
            HiddenGearSets = hidden ?? Array.Empty<string>(),
            Boons = boons ?? BoonPlan.None,
        };

    private static LdLordFacts Lord(float x, float y, float z, bool own = true) => LdLordFacts.None with { Present = true, Own = own, At = new LdPoint(x, y, z) };

    private static LdLordFacts AtArmoury(bool own = true) => Lord(ArmouryAt.X, ArmouryAt.Y, ArmouryAt.Z, own);

    private static LdLordFacts AtYard(bool own = true) => Lord(YardAt.X, YardAt.Y, YardAt.Z, own);

    private static LdLordFacts FarAway() => Lord(300f, 300f, 5f);

    private static RunSnapshot Snap(LdLordFacts? lord, float dt = 1f) => new(dt, 0, 0, 40, 0, 0, 1000) { Lord = lord };

    /// <summary>A begun run whose first tick has read the lord where the test puts him, with the yard's sites known.</summary>
    private static SurvivalRun Begun(RunPlan plan, LdLordFacts? lord, bool sites = true)
    {
        var run = new SurvivalRun(plan);
        run.Begin();
        if (sites)
            run.SetSites(Sites);
        run.Tick(Snap(lord));
        return run;
    }

    /// <summary>The gear sets the lord owns, in purchase order, as the report lists them.</summary>
    private static string[] Owned(SurvivalRun run) => run.FortSummary().Purchases.Where(p => p.Kind == PurchaseKind.GearSet).Select(p => p.Id).ToArray();

    private static RunEffects ClearTo(SurvivalRun run, int clears, LdLordFacts? lord)
    {
        RunEffects last = new();
        for (var i = 0; i < 5000 && run.WavesCleared < clears && run.Result == null; i++)
        {
            if (run.Phase == RunPhase.Intermission)
                run.TryStartWave();
            last = run.Tick(Snap(lord));
        }

        Assert.AreEqual(clears, run.WavesCleared, "the run never reached the clear");
        return last;
    }

    // ---- D46: nothing is for sale until a slice lands its effect ----

    [TestMethod]
    public void EveryBuy_WithNothingOffered_IsNotOffered_AndSpendsNothing()
    {
        var run = Begun(Plan(offered: LocationOffer.None), AtArmoury());

        Assert.AreEqual(LocationResult.NotOffered, run.TryBuyGear("armour_1"));
        Assert.AreEqual(LocationResult.NotOffered, run.TryBuyEngine("mangonel", 0));
        Assert.AreEqual(5000, run.Gold);
        Assert.AreEqual(0, run.FortSummary().Purchases.Count);
        var view = run.GetView().Locations;
        Assert.AreEqual((0, 0, LdPlaceKind.None), (view.Gear.Count, view.Sites.Count, view.Near), "a build that delivers nothing shows no row and no prompt");
    }

    // ---- the armoury ----

    [TestMethod]
    public void TryBuyGear_NearTheArmoury_SpendsAtOnce_AndTheNextTickCarriesTheMergedLoadout()
    {
        var run = Begun(Plan(), AtArmoury());

        Assert.AreEqual(LocationResult.Ok, run.TryBuyGear("armour_1"));

        Assert.AreEqual(4750, run.Gold);
        Assert.AreEqual(250, run.Purse.SpentOn(PurseSink.Gear));
        var bought = run.FortSummary().Purchases.Single();
        Assert.AreEqual((PurchaseKind.GearSet, "armour_1", 250, 250, 1, ErrandReach.Person), (bought.Kind, bought.Id, bought.BasePrice, bought.Price, bought.BeforeWave, bought.Reach));
        CollectionAssert.AreEqual(new[] { "armour_1" }, Owned(run));

        var fx = run.Tick(Snap(AtArmoury()));
        Assert.IsNotNull(fx.SwapGear);
        Assert.AreEqual("mail", fx.SwapGear!.Slots.Single(s => s.Slot == "Body").ItemId, "the set's body armour replaces the kit's");
        Assert.AreEqual("kit_sword", fx.SwapGear.Slots.Single(s => s.Slot == "Item0").ItemId, "an armour set leaves the weapons alone");
        Assert.IsNull(run.Tick(Snap(AtArmoury())).SwapGear, "the swap is handed out once");
    }

    [TestMethod]
    public void TwoSetsBoughtBeforeATick_AreHandedOutAsOneLoadout_ReadWhenItIsHandedOut()
    {
        var run = Begun(Plan(), AtArmoury());

        Assert.AreEqual(LocationResult.Ok, run.TryBuyGear("armour_1"));
        Assert.AreEqual(LocationResult.Ok, run.TryBuyGear("weapons_sword"));

        var swap = run.Tick(Snap(AtArmoury())).SwapGear!;
        Assert.AreEqual("mail", swap.Slots.Single(s => s.Slot == "Body").ItemId);
        Assert.AreEqual("long_blade", swap.Slots.Single(s => s.Slot == "Item0").ItemId);
        Assert.AreEqual("", swap.Slots.Single(s => s.Slot == "Item1").ItemId, "a weapons set clears the weapon slots it does not name");
        CollectionAssert.AreEqual(swap.Slots.ToArray(), run.EffectiveLoadout!.Slots.ToArray(), "every later spawn of the lord builds the same kit");
    }

    [DataTestMethod]
    [DataRow(14.1f, 10f, 5f, false, DisplayName = "a little over the 4 m reach on the ground")]
    [DataRow(13.9f, 10f, 5f, true, DisplayName = "inside the reach")]
    [DataRow(10f, 10f, 8.1f, false, DisplayName = "over 3 m above the place")]
    [DataRow(10f, 10f, 2.1f, true, DisplayName = "under 3 m below the place")]
    [DataRow(float.NaN, 10f, 5f, false, DisplayName = "a position that is not a number")]
    [DataRow(10f, 10f, float.NaN, false, DisplayName = "a height that is not a number")]
    public void TryBuyGear_JudgesNearFromTheLordsPlace(float x, float y, float z, bool near)
    {
        var run = Begun(Plan(), Lord(x, y, z));

        Assert.AreEqual(near ? LocationResult.Ok : LocationResult.NotNear, run.TryBuyGear("armour_1"));
        Assert.AreEqual(near ? LdPlaceKind.Armoury : LdPlaceKind.None, run.GetView().Locations.Near);
    }

    [TestMethod]
    public void TryBuyGear_WithNoLordOnTheField_IsNotNear()
    {
        Assert.AreEqual(LocationResult.NotNear, Begun(Plan(), null).TryBuyGear("armour_1"), "a snapshot that read no lord");
        Assert.AreEqual(LocationResult.NotNear, Begun(Plan(), AtArmoury() with { Present = false }).TryBuyGear("armour_1"), "a lord who has fallen");
    }

    [TestMethod]
    public void TryBuyGear_InATakenSoldier_IsNotOwnBody_AndSpendsNothing()
    {
        var run = Begun(Plan(), AtArmoury(own: false));

        Assert.AreEqual(LocationResult.NotOwnBody, run.TryBuyGear("armour_1"));
        Assert.AreEqual(5000, run.Gold);
        Assert.IsFalse(run.GetView().Locations.OwnBody);
    }

    [TestMethod]
    public void TryBuyGear_ASetBoughtTwice_IsOwned()
    {
        var run = Begun(Plan(), AtArmoury());
        run.TryBuyGear("armour_1");

        Assert.AreEqual(LocationResult.Owned, run.TryBuyGear("armour_1"));
        Assert.AreEqual(4750, run.Gold);
        Assert.AreEqual(GearState.Owned, run.GetView().Locations.Gear.Single(r => r.Set.Id == "armour_1").State);
    }

    [TestMethod]
    public void TryBuyGear_WithTooLittleGold_IsNoGold()
    {
        var run = Begun(Plan(gold: 249), AtArmoury());

        Assert.AreEqual(LocationResult.NoGold, run.TryBuyGear("armour_1"));
        Assert.AreEqual(0, Owned(run).Length);
    }

    [TestMethod]
    public void TryBuyGear_InAWave_IsNotOpen_AndAfterTheRun_IsRunOver()
    {
        var run = Begun(Plan(rules: RunRules.Default with { PreparationSeconds = 1f }), AtArmoury());
        run.Tick(Snap(AtArmoury()));
        Assert.AreEqual(RunPhase.Wave, run.Phase);

        Assert.AreEqual(LocationResult.NotOpen, run.TryBuyGear("armour_1"));
        Assert.AreEqual(LocationResult.NotOpen, run.TryBuyEngine("mangonel", 0));

        run.Abandon();
        Assert.AreEqual(LocationResult.RunOver, run.TryBuyGear("armour_1"));
        Assert.AreEqual(LocationResult.RunOver, run.TryBuyEngine("mangonel", 0));
    }

    [TestMethod]
    public void TryBuyGear_ASetTheRunDoesNotSell_IsHidden()
    {
        Assert.AreEqual(LocationResult.Hidden, Begun(Plan(), AtArmoury()).TryBuyGear("no_such_set"), "an unknown id");
        Assert.AreEqual(LocationResult.Hidden, Begun(Plan(), AtArmoury()).TryBuyGear("weapons_archer"), "a weapons set for another class");
        Assert.AreEqual(LocationResult.Hidden, Begun(Plan(hidden: new[] { "armour_1" }), AtArmoury()).TryBuyGear("armour_1"), "a set with an item the launch check refused");
        Assert.AreEqual(LocationResult.Hidden, Begun(Plan(armouryPoint: false), AtArmoury()).TryBuyGear("armour_1"), "no recorded armoury");
        Assert.AreEqual(LocationResult.Hidden, Begun(Plan(armoury: false), AtArmoury()).TryBuyGear("armour_1"), "a shop with no armoury block");
        Assert.AreEqual(LocationResult.Hidden, Begun(Plan(noLoadout: true), AtArmoury()).TryBuyGear("armour_1"), "a lord with no class kit buys nothing");
    }

    [TestMethod]
    public void TheGearRows_ListOnlyTheSetsTheLordsClassIsSold_WithTheFactoredPrice()
    {
        var boons = new BoonPlan(new[] { Boon("haggler", ("pricePercent", -20f)) }, 1);
        var run = Begun(Plan(boons: boons, hidden: new[] { "armour_2" }), AtArmoury());
        ClearTo(run, 3, AtArmoury());
        Assert.AreEqual(PickResult.Ok, run.TryPickBoon("haggler"));

        var rows = run.GetView().Locations.Gear;
        CollectionAssert.AreEqual(new[] { "armour_1", "weapons_sword" }, rows.Select(r => r.Set.Id).ToArray(), "no hidden set and no other class's weapons");
        Assert.AreEqual(200, rows[0].Price, "the row shows what the buy will take");

        Assert.AreEqual(LocationResult.Ok, run.TryBuyGear("armour_1"));
        var bought = run.FortSummary().Purchases.Last();
        Assert.AreEqual((250, 200), (bought.BasePrice, bought.Price));
        Assert.AreEqual(200, run.Purse.SpentOn(PurseSink.Gear));
    }

    [TestMethod]
    public void ABuyOnTheLastIntermissionTick_HoldsTheWaveStartOneTick()
    {
        var run = Begun(Plan(rules: RunRules.Default with { PreparationSeconds = 2f }), AtArmoury());
        Assert.AreEqual(LocationResult.Ok, run.TryBuyGear("armour_1"));

        var fx = run.Tick(Snap(AtArmoury()));

        Assert.IsNotNull(fx.SwapGear);
        Assert.AreEqual(RunPhase.Preparation, run.Phase, "the tick that hands the swap out starts no wave");
        Assert.IsNull(fx.StartWave);
        run.Tick(Snap(AtArmoury()));
        Assert.AreEqual(RunPhase.Wave, run.Phase);
    }

    // ---- the engineer's yard ----

    [TestMethod]
    public void TryBuyEngine_NearTheYard_SpendsAtOnce_AndTheNextTickCarriesTheDeploy()
    {
        var run = Begun(Plan(), AtYard());

        Assert.AreEqual(LocationResult.Ok, run.TryBuyEngine("mangonel", 1));

        Assert.AreEqual(4550, run.Gold);
        Assert.AreEqual(450, run.Purse.SpentOn(PurseSink.Engines));
        var bought = run.FortSummary().Purchases.Single();
        Assert.AreEqual((PurchaseKind.Engine, "mangonel", 1, 450, 450, ErrandReach.Person), (bought.Kind, bought.Id, bought.Site, bought.BasePrice, bought.Price, bought.Reach));

        var fx = run.Tick(Snap(AtYard()));
        Assert.AreEqual(new EngineOrder(1, "mangonel"), fx.DeployEngines.Single());
        Assert.AreEqual(0, run.Tick(Snap(AtYard())).DeployEngines.Count, "the deploy is handed out once");
        var site = run.GetView().Locations.Sites.Single(s => s.Site.Index == 1);
        Assert.AreEqual((SiteState.Taken, "mangonel"), (site.State, site.Standing));
    }

    [TestMethod]
    public void TryBuyEngine_InATakenSoldier_IsStillSold()
    {
        var run = Begun(Plan(), AtYard(own: false));

        Assert.AreEqual(LocationResult.Ok, run.TryBuyEngine("mangonel", 0), "an engine is not worn, so the body does not matter");
    }

    [TestMethod]
    public void TryBuyEngine_AKindTheBuildDoesNotDeliver_IsNotOffered()
    {
        var run = Begun(Plan(offered: new LocationOffer(true, true, false)), AtYard());

        Assert.AreEqual(LocationResult.NotOffered, run.TryBuyEngine("trebuchet", 1));
        Assert.AreEqual(LocationResult.NotOffered, run.TryBuyEngine("ballista", 1), "a kind no build sells");
        Assert.AreEqual(LocationResult.Ok, run.TryBuyEngine("mangonel", 1));
        CollectionAssert.AreEqual(new[] { "mangonel" }, run.GetView().Locations.Sites.Single(s => s.Site.Index == 0).Kinds.Select(k => k.Kind).ToArray());
    }

    [TestMethod]
    public void TryBuyEngine_ASiteThatCannotTakeIt_IsNoSite()
    {
        var run = Begun(Plan(), AtYard());

        Assert.AreEqual(LocationResult.NoSite, run.TryBuyEngine("mangonel", 7), "no such site");
        Assert.AreEqual(LocationResult.NoSite, run.TryBuyEngine("trebuchet", 0), "site 0 holds no trebuchet");
        Assert.AreEqual(LocationResult.NoSite, Begun(Plan(), AtYard(), sites: false).TryBuyEngine("mangonel", 0), "the field read no site");
        Assert.AreEqual(5000, run.Gold);
    }

    [TestMethod]
    public void TryBuyEngine_ATakenSite_IsSiteTaken_AndALostOne_IsSiteLost()
    {
        var run = Begun(Plan(), AtYard());
        Assert.AreEqual(LocationResult.Ok, run.TryBuyEngine("mangonel", 0));

        Assert.AreEqual(LocationResult.SiteTaken, run.TryBuyEngine("mangonel", 0));

        run.EngineLost(0);
        Assert.AreEqual(LocationResult.SiteLost, run.TryBuyEngine("mangonel", 0), "a destroyed engine spends its site for the run (OQ11)");
        Assert.AreEqual(SiteState.Lost, run.GetView().Locations.Sites.Single(s => s.Site.Index == 0).State);
        Assert.AreEqual(LocationResult.Ok, run.TryBuyEngine("mangonel", 1), "another free site can still be bought");
        Assert.AreEqual(4100, run.Gold);
    }

    [TestMethod]
    public void EngineLost_ForASiteWithNoEngine_ChangesNothing()
    {
        var run = Begun(Plan(), AtYard());

        run.EngineLost(0);
        run.EngineLost(9);

        Assert.AreEqual(LocationResult.Ok, run.TryBuyEngine("mangonel", 0));
    }

    [TestMethod]
    public void TryBuyEngine_AwayFromTheYard_IsNotNear()
    {
        var run = Begun(Plan(), AtArmoury());

        Assert.AreEqual(LocationResult.NotNear, run.TryBuyEngine("mangonel", 0));
        Assert.AreEqual(5000, run.Gold);
    }

    [TestMethod]
    public void TryBuyEngine_AYardTheRunDoesNotSell_IsHidden()
    {
        Assert.AreEqual(LocationResult.Hidden, Begun(Plan(yardPoint: false), AtYard()).TryBuyEngine("mangonel", 0), "no recorded yard");
        Assert.AreEqual(LocationResult.Hidden, Begun(Plan(yard: false), AtYard()).TryBuyEngine("mangonel", 0), "a shop with no yard block");
    }

    [TestMethod]
    public void TryBuyEngine_WithTooLittleGold_IsNoGold_AndTheSiteStaysFree()
    {
        var run = Begun(Plan(gold: 449), AtYard());

        Assert.AreEqual(LocationResult.NoGold, run.TryBuyEngine("mangonel", 0));
        Assert.AreEqual(SiteState.Free, run.GetView().Locations.Sites.Single(s => s.Site.Index == 0).State);
    }

    [TestMethod]
    public void AnEngineBoughtOnTheLastIntermissionTick_HoldsTheWaveStartOneTick()
    {
        var run = Begun(Plan(rules: RunRules.Default with { PreparationSeconds = 2f }), AtYard());
        Assert.AreEqual(LocationResult.Ok, run.TryBuyEngine("mangonel", 0));

        var fx = run.Tick(Snap(AtYard()));

        Assert.AreEqual(1, fx.DeployEngines.Count);
        Assert.AreEqual(RunPhase.Preparation, run.Phase, "the engine stands before the wave's tactic reads the field");
    }

    [TestMethod]
    public void TheYardRows_ListOnlySitesThatTakeAnOfferedKind()
    {
        var run = Begun(Plan(offered: new LocationOffer(true, false, true)), AtYard());

        var sites = run.GetView().Locations.Sites;

        Assert.AreEqual(1, sites.Count, "site 0 takes only the mangonel, which this build does not deliver");
        Assert.AreEqual((1, "trebuchet", 700), (sites[0].Site.Index, sites[0].Kinds.Single().Kind, sites[0].Kinds.Single().Price));
    }

    // ---- the bot's errand (D44) ----

    [TestMethod]
    public void AnErrand_ThatArrivesOnFoot_BooksTheBuyAsAWalk_AndEndsWithTheBuy()
    {
        var run = Begun(Plan(), FarAway());

        Assert.AreEqual(ErrandResult.Ok, run.TryStartErrand(LdPlaceKind.Armoury));
        var start = run.Tick(Snap(FarAway())).Errand!;
        Assert.AreEqual((ErrandStep.Start, LdPlaceKind.Armoury, LdPlace.From(ArmouryAt)), (start.Step, start.Place, start.At));
        Assert.AreEqual(LdPlaceKind.Armoury, run.GetView().Locations.Errand);

        run.Tick(Snap(AtArmoury()));
        Assert.AreEqual(LocationResult.Ok, run.TryBuyGear("armour_1"));

        Assert.AreEqual(ErrandReach.Walk, run.FortSummary().Purchases.Single().Reach);
        Assert.AreEqual(LdPlaceKind.None, run.GetView().Locations.Errand, "the buy closes the errand");
        Assert.AreEqual(ErrandStep.End, run.Tick(Snap(AtArmoury())).Errand!.Step, "the lord's scripted walk is released on the next tick");
        Assert.IsNull(run.Tick(Snap(AtArmoury())).Errand);
    }

    [TestMethod]
    public void AnErrand_ThatHasNotArrivedInTwentySeconds_TeleportsOnce_AndBooksTheBuyAsATeleport()
    {
        var run = Begun(Plan(), FarAway());
        run.TryStartErrand(LdPlaceKind.Yard);
        run.Tick(Snap(FarAway()));

        var steps = new List<ErrandStep>();
        for (var second = 0; second < 21; second++)
        {
            if (run.Tick(Snap(FarAway())).Errand is { } order)
                steps.Add(order.Step);
        }

        CollectionAssert.AreEqual(new[] { ErrandStep.Teleport }, steps, "the budget is counted from the snapshots' dt, and the teleport is asked for once");
        run.Tick(Snap(AtYard()));
        Assert.AreEqual(LocationResult.Ok, run.TryBuyEngine("mangonel", 0));
        Assert.AreEqual(ErrandReach.Teleport, run.FortSummary().Purchases.Single().Reach, "the report names the dev bypass");
    }

    [TestMethod]
    public void AnErrand_WhoseTeleportDidNotArrive_IsClosed_AndThePlaceIsUnreachedUntilTheNextWave()
    {
        var run = Begun(Plan(), FarAway());
        run.TryStartErrand(LdPlaceKind.Armoury);
        RunEffects last = new();
        for (var second = 0; second < 27 && run.GetView().Locations.Errand != LdPlaceKind.None; second++)
            last = run.Tick(Snap(FarAway()));

        Assert.AreEqual(LdPlaceKind.None, run.GetView().Locations.Errand);
        CollectionAssert.AreEqual(new[] { LdPlaceKind.Armoury }, run.GetView().Locations.Unreached.ToArray());
        Assert.AreEqual(ErrandResult.Unreached, run.TryStartErrand(LdPlaceKind.Armoury), "the bot does not walk the same errand again in this stretch");
        Assert.AreEqual(ErrandStep.End, run.Tick(Snap(FarAway())).Errand!.Step);

        ClearTo(run, 1, FarAway());
        Assert.AreEqual(0, run.GetView().Locations.Unreached.Count, "the next stretch between waves tries again");
    }

    [TestMethod]
    public void AWaveTheTimerBeginsOnTheTickOfAnErrandsOrder_WithdrawsThatOrder_AndReleasesOnTheNextTick()
    {
        var run = Begun(Plan(), FarAway());
        Assert.AreEqual(ErrandResult.Ok, run.TryStartErrand(LdPlaceKind.Armoury));

        var fx = run.Tick(Snap(FarAway(), 1000f));

        Assert.AreEqual(RunPhase.Wave, run.GetView().Phase, "the stretch ran out on this tick");
        Assert.IsNull(fx.Errand, "the director moves a rider to his sally before it serves an errand: a teleport after that would carry him back to the armoury (Data flow lens, 2026-11-22)");
        Assert.AreEqual(ErrandStep.End, run.Tick(Snap(FarAway())).Errand!.Step, "the walk is released on the next tick, and for a walk that never started that release finds nothing");
    }

    [TestMethod]
    public void AnErrand_StillOpenWhenTheWaveBegins_IsClosed()
    {
        var run = Begun(Plan(), FarAway());
        run.TryStartErrand(LdPlaceKind.Armoury);
        run.Tick(Snap(FarAway()));

        run.TryStartWave();
        var fx = run.Tick(Snap(FarAway()));

        Assert.AreEqual(ErrandStep.End, fx.Errand!.Step);
        Assert.AreEqual(LdPlaceKind.None, run.GetView().Locations.Errand);
    }

    [TestMethod]
    public void AnErrand_WhoseLordFellOnTheWay_IsClosed_AndNoTeleportIsAskedFor()
    {
        var run = Begun(Plan(), FarAway());
        run.TryStartErrand(LdPlaceKind.Armoury);
        run.Tick(Snap(FarAway()));

        var fx = run.Tick(Snap(FarAway() with { Present = false }));

        Assert.IsNull(fx.Errand);
        Assert.AreEqual(LdPlaceKind.None, run.GetView().Locations.Errand);
        Assert.IsFalse(run.GetView().Locations.LordStands);
        var orders = Enumerable.Range(0, 30).Select(_ => run.Tick(Snap(null)).Errand).Where(o => o != null).ToList();
        CollectionAssert.AreEqual(new[] { ErrandStep.End }, orders.Select(o => o!.Step).ToArray(), "the release, and never a teleport");
        Assert.AreEqual(0, run.GetView().Locations.Unreached.Count, "the place was not at fault");
    }

    [TestMethod]
    public void TryStartErrand_RefusesWhatCannotBeWalked()
    {
        var run = Begun(Plan(), FarAway());

        Assert.AreEqual(ErrandResult.NoPlace, run.TryStartErrand(LdPlaceKind.None));
        Assert.AreEqual(ErrandResult.NoPlace, Begun(Plan(yardPoint: false), FarAway()).TryStartErrand(LdPlaceKind.Yard), "a place the run does not sell");
        Assert.AreEqual(ErrandResult.NoPlace, Begun(Plan(offered: LocationOffer.None), FarAway()).TryStartErrand(LdPlaceKind.Armoury));
        Assert.AreEqual(ErrandResult.Ok, run.TryStartErrand(LdPlaceKind.Armoury));
        Assert.AreEqual(ErrandResult.Busy, run.TryStartErrand(LdPlaceKind.Yard), "one errand runs at a time");

        var inWave = Begun(Plan(rules: RunRules.Default with { PreparationSeconds = 1f }), FarAway());
        inWave.Tick(Snap(FarAway()));
        Assert.AreEqual(ErrandResult.NotOpen, inWave.TryStartErrand(LdPlaceKind.Armoury));
    }

    [TestMethod]
    public void EndErrand_ClosesAnOpenErrand_AndReleasesTheLord()
    {
        var run = Begun(Plan(), FarAway());
        run.TryStartErrand(LdPlaceKind.Armoury);
        run.Tick(Snap(FarAway()));

        run.EndErrand();

        Assert.AreEqual(LdPlaceKind.None, run.GetView().Locations.Errand);
        Assert.AreEqual(ErrandStep.End, run.Tick(Snap(FarAway())).Errand!.Step);
    }

    [TestMethod]
    public void ABuyByAPerson_WhileNoErrandIsOpen_IsBookedAsAPersonsWalk()
    {
        var run = Begun(Plan(), AtYard());

        run.TryBuyEngine("mangonel", 0);

        Assert.AreEqual(ErrandReach.Person, run.FortSummary().Purchases.Single().Reach);
    }

    [TestMethod]
    public void DropPlace_HidesTheArmouryForTheRun_AsAMissingPointDoes()
    {
        var run = Begun(Plan(), AtArmoury());
        Assert.IsTrue(run.GetView().Locations.Gear.Count > 0, "the armoury shows before it is dropped");

        run.DropPlace(LdPlaceKind.Armoury);

        Assert.AreEqual(LocationResult.Hidden, run.TryBuyGear("armour_1"));
        Assert.AreEqual(ErrandResult.NoPlace, run.TryStartErrand(LdPlaceKind.Armoury));
        var view = run.GetView().Locations;
        Assert.AreEqual((0, LdPlaceKind.None), (view.Gear.Count, view.Near));
        Assert.AreEqual(5000, run.Gold);
    }
}
