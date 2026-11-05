using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.LongDefeat.Domain;

namespace TAOM.Tests.Features.LongDefeat.Domain;

/// <summary>
/// The troop and class boons inside a run (phase2-plan.md section 2, "Boons", Slice 6): the deck is built with the run's class and the troop groups the garrison and shop hold,
/// a health boon reaches the soldiers standing by its own factor and every later defender batch by the combined factor of its group, and a pick tells the director what to publish.
/// </summary>
[TestClass]
public class SurvivalRunTargetBoonTests
{
    private static BoonRow Row(string id, BoonCategory category, string classId, params (string Key, float Value, BoonTarget Target)[] effects) =>
        new(id, category, "{=K_" + id + "}" + id, "{=T_" + id + "}text", effects.Select(e => new BoonEffect(e.Key, e.Value, e.Target)).ToList()) { ClassId = classId };

    private static BoonRow Health(string id, BoonCategory category, BoonTarget target, float percent) =>
        Row(id, category, "", (BoonKeys.HealthPercent, percent, target));

    private static RunPlan Plan(BoonPlan boons, string? classId = null, IReadOnlyDictionary<string, BoonTarget>? groups = null, params ShopOffer[] extraOffers) =>
        new(
            "helms",
            new[] { new GarrisonRow("militia", FormationSlot.Infantry, 30), new GarrisonRow("skirm", FormationSlot.Ranged, 10) },
            new WavePlan(new[] { new WaveSpec(20, new[] { new WaveTroop("orc", FormationSlot.Infantry, 3) }) }, new WaveScaling(10, 300)),
            new ShopPlan(100000, new[] { new ShopOffer("militia", "Militia", "militia", FormationSlot.Infantry, 12, 180) }.Concat(extraOffers).ToArray()),
            RunRules.Default with { PreparationSeconds = 1f, IntermissionSeconds = 10f },
            new RunOptions { Seed = 1, ClassId = classId })
        {
            Bounties = new Dictionary<string, int> { ["orc"] = 4 },
            Boons = boons,
            TroopGroups = groups ?? new Dictionary<string, BoonTarget>(),
            ClassName = classId == null ? "" : "{=C_" + classId + "}" + classId,
        };

    private static RunSnapshot Snap() => new(1f, 0, 0, 40, 0, 0, 1000);

    private static SurvivalRun ThirdClear(RunPlan plan)
    {
        var run = new SurvivalRun(plan);
        run.Begin();
        for (var i = 0; i < 5000 && run.WavesCleared < 3 && run.Result == null; i++)
        {
            if (run.GetView().Phase == RunPhase.Intermission)
                run.TryStartWave();
            run.Tick(Snap());
        }

        Assert.AreEqual(3, run.WavesCleared);
        return run;
    }

    [TestMethod]
    public void ARunOfAClass_OffersItsClassRows_AndNoOtherClasses()
    {
        var rows = new[]
        {
            Row("mine1", BoonCategory.Class, "archer", (BoonKeys.DamagePercent, 10f, BoonTarget.Player)),
            Row("mine2", BoonCategory.Class, "archer", (BoonKeys.DamagePercent, 11f, BoonTarget.Player)),
            Row("mine3", BoonCategory.Class, "archer", (BoonKeys.DamagePercent, 12f, BoonTarget.Player)),
            Row("theirs1", BoonCategory.Class, "two_handed", (BoonKeys.DamagePercent, 10f, BoonTarget.Player)),
            Row("theirs2", BoonCategory.Class, "two_handed", (BoonKeys.DamagePercent, 11f, BoonTarget.Player)),
        };
        var plan = new BoonPlan(rows, 1);

        var archer = ThirdClear(Plan(plan, "archer"));
        var other = ThirdClear(Plan(plan, "sword_and_board"));
        var none = ThirdClear(Plan(plan));

        CollectionAssert.AreEquivalent(new[] { "mine1", "mine2", "mine3" }, archer.GetView().BoonOffer.Select(c => c.Id).ToArray(), "pick 1 of the archer rows only");
        Assert.AreEqual(0, other.GetView().BoonOffer.Count, "a class with no row of its own is offered none");
        Assert.AreEqual(0, none.GetView().BoonOffer.Count, "a run with no class owns no class row");
    }

    [TestMethod]
    public void ATroopCategorysRows_AreOffered_OnlyWhenTheGarrisonOrShopHoldsAGroupTroop()
    {
        var cavalry = new BoonPlan(new[]
        {
            Health("cav1", BoonCategory.Cavalry, BoonTarget.Cavalry, 10f), Health("cav2", BoonCategory.Cavalry, BoonTarget.Cavalry, 11f), Health("cav3", BoonCategory.Cavalry, BoonTarget.Cavalry, 12f),
        }, 1);
        var archers = new BoonPlan(new[]
        {
            Health("arc1", BoonCategory.Archers, BoonTarget.Archers, 10f), Health("arc2", BoonCategory.Archers, BoonTarget.Archers, 11f), Health("arc3", BoonCategory.Archers, BoonTarget.Archers, 12f),
        }, 1);
        var groups = new Dictionary<string, BoonTarget> { ["militia"] = BoonTarget.Infantry, ["skirm"] = BoonTarget.Archers, ["knight"] = BoonTarget.Cavalry };
        var knight = new ShopOffer("knights", "Knights", "knight", FormationSlot.Infantry, 6, 360);

        Assert.AreEqual(0, ThirdClear(Plan(cavalry, null, groups)).GetView().BoonOffer.Count, "no cavalry troop in the garrison or shop: the pick lapses");
        Assert.AreEqual(3, ThirdClear(Plan(cavalry, null, groups, knight)).GetView().BoonOffer.Count, "a rider on the shop's list makes the cavalry rows eligible");
        Assert.AreEqual(3, ThirdClear(Plan(archers, null, groups)).GetView().BoonOffer.Count, "the garrison's archers make the archer rows eligible");
    }

    [TestMethod]
    public void ATroopWithNoEntryInTheGroupTable_FallsBackOnItsFormationSlot()
    {
        var plan = Plan(new BoonPlan(new[] { Health("arc1", BoonCategory.Archers, BoonTarget.Archers, 10f) }, 1));

        var groups = RunPlanTroops.DefenderGroups(plan);

        CollectionAssert.AreEquivalent(new[] { BoonCategory.Infantry, BoonCategory.Archers }, groups.ToArray(), "the garrison's Ranged row is archers, its Infantry row infantry");
    }

    [TestMethod]
    public void ARiderBoughtIntoTheInfantrySlot_IsCavalry_NotInfantry()
    {
        var knight = new ShopOffer("knights", "Knights", "knight", FormationSlot.Infantry, 6, 360);
        var plan = Plan(BoonPlan.None, null, new Dictionary<string, BoonTarget> { ["knight"] = BoonTarget.Cavalry }, knight);

        Assert.AreEqual(BoonTarget.Cavalry, plan.GroupOf("knight", FormationSlot.Infantry));
        Assert.AreEqual(BoonTarget.Infantry, plan.GroupOf("militia", FormationSlot.Infantry));
        CollectionAssert.Contains(RunPlanTroops.DefenderGroups(plan).ToList(), BoonCategory.Cavalry);
    }

    [TestMethod]
    public void AHealthPick_TellsTheDirectorToScaleThoseStandingByItsOwnFactorOnly_AndASecondOneNeverReappliesTheFirst()
    {
        var rows = new[] { Health("first", BoonCategory.Infantry, BoonTarget.Infantry, 20f), Health("second", BoonCategory.Infantry, BoonTarget.Infantry, 10f) };
        var groups = new Dictionary<string, BoonTarget> { ["militia"] = BoonTarget.Infantry };
        var run = ThirdClear(Plan(new BoonPlan(rows, 1), null, groups));
        run.TryPickBoon("first");

        var one = run.Tick(Snap());

        Assert.AreEqual(1.2f, one.ApplyBoon!.Health.Single().Factor, 1e-5f);
        Assert.AreEqual(BoonTarget.Infantry, one.ApplyBoon.Health.Single().Target);
        Assert.IsNull(run.Tick(Snap()).ApplyBoon, "told once");
        Assert.AreEqual(PickResult.NoOffer, run.TryPickBoon("second"), "the second pick opens after the sixth clear");
    }

    [TestMethod]
    public void TwoHealthPicks_ScaleByEachOwnFactor_WhileANewSpawnTakesTheirProduct()
    {
        var rows = new[] { Health("first", BoonCategory.Fortress, BoonTarget.Infantry, 20f), Health("second", BoonCategory.Fortress, BoonTarget.Infantry, 10f) };
        var groups = new Dictionary<string, BoonTarget> { ["militia"] = BoonTarget.Infantry };
        var run = ThirdClear(Plan(new BoonPlan(rows, 1), null, groups));
        var applied = new List<float>();

        run.TryPickBoon("first");
        applied.Add(run.Tick(Snap()).ApplyBoon!.Health.Single().Factor);
        for (var i = 0; i < 5000 && run.WavesCleared < 6 && run.Result == null; i++)
        {
            if (run.GetView().Phase == RunPhase.Intermission)
                run.TryStartWave();
            run.Tick(Snap());
        }

        Assert.AreEqual(PickResult.Ok, run.TryPickBoon("second"));
        applied.Add(run.Tick(Snap()).ApplyBoon!.Health.Single().Factor);
        run.TryBuy("militia");
        var batch = run.Tick(Snap()).DefenderBatches.Single();

        Assert.AreEqual(1.2f, applied[0], 1e-5f);
        Assert.AreEqual(1.1f, applied[1], 1e-5f, "the second boon scales by 1.1, not by 1.32");
        Assert.AreEqual(1.32f, batch.HealthFactor, 1e-5f, "a soldier spawned after both takes the combined factor");
        Assert.AreEqual(1.32f, run.HealthFactor(BoonTarget.Infantry), 1e-5f);
        Assert.AreEqual(1f, run.HealthFactor(BoonTarget.Player));
    }

    [TestMethod]
    public void TwoPicksBeforeTheNextTick_BothReachTheDirector_NeitherReplacesTheOther()
    {
        var rows = new[]
        {
            Health("first", BoonCategory.Fortress, BoonTarget.Infantry, 20f),
            Row("second", BoonCategory.Fortress, "", (BoonKeys.MoveSpeedPercent, 10f, BoonTarget.Cavalry), (BoonKeys.HealthPercent, 10f, BoonTarget.Archers)),
        };
        var run = ThirdClear(Plan(new BoonPlan(rows, 1)));
        for (var i = 0; i < 5000 && run.WavesCleared < 6; i++)
        {
            if (run.GetView().Phase == RunPhase.Intermission)
                run.TryStartWave();
            run.Tick(Snap());
        }

        run.TryPickBoon("first");
        run.TryPickBoon("second");
        var fx = run.Tick(Snap());

        CollectionAssert.AreEquivalent(new[] { BoonTarget.Infantry, BoonTarget.Archers }, fx.ApplyBoon!.Health.Select(h => h.Target).ToArray());
        CollectionAssert.AreEqual(new[] { BoonTarget.Cavalry }, fx.ApplyBoon.Refresh.ToArray());
    }

    [TestMethod]
    public void APackBoughtAfterAHealthPick_SpawnsWithItsGroupsFactor_AndAnotherGroupsPackWithItsOwn()
    {
        var rows = new[] { Health("tough", BoonCategory.Infantry, BoonTarget.Infantry, 20f) };
        var knight = new ShopOffer("knights", "Knights", "knight", FormationSlot.Infantry, 6, 360);
        var archers = new ShopOffer("bows", "Bows", "skirm", FormationSlot.Ranged, 8, 200);
        var groups = new Dictionary<string, BoonTarget> { ["militia"] = BoonTarget.Infantry, ["skirm"] = BoonTarget.Archers, ["knight"] = BoonTarget.Cavalry };
        var run = ThirdClear(Plan(new BoonPlan(rows, 1), null, groups, knight, archers));
        run.TryPickBoon("tough");

        run.TryBuy("militia");
        run.TryBuy("knights");
        run.TryBuy("bows");
        var batches = run.Tick(Snap()).DefenderBatches.ToDictionary(b => b.TroopId);

        Assert.AreEqual(1.2f, batches["militia"].HealthFactor, 1e-5f);
        Assert.AreEqual(1f, batches["knight"].HealthFactor, "a rider bought on foot is cavalry, which the boon does not touch");
        Assert.AreEqual(1f, batches["skirm"].HealthFactor);
    }

    [TestMethod]
    public void TheOpeningGarrison_SpawnsWithAFactorOfOne()
    {
        var run = new SurvivalRun(Plan(BoonPlan.None));

        var fx = run.Begin();

        Assert.IsTrue(fx.DefenderBatches.All(b => b.HealthFactor == 1f));
    }

    [TestMethod]
    public void AStatPick_PublishesTheTargetsRecord_AndAsksForARefreshOfThatTargetOnly()
    {
        var rows = new[] { Row("swift", BoonCategory.Cavalry, "", (BoonKeys.MoveSpeedPercent, 12f, BoonTarget.Cavalry)) };
        var knight = new ShopOffer("knights", "Knights", "knight", FormationSlot.Infantry, 6, 360);
        var groups = new Dictionary<string, BoonTarget> { ["knight"] = BoonTarget.Cavalry };
        var run = ThirdClear(Plan(new BoonPlan(rows, 1), null, groups, knight));
        run.TryPickBoon("swift");

        var fx = run.Tick(Snap());

        CollectionAssert.AreEqual(new[] { BoonTarget.Cavalry }, fx.ApplyBoon!.Refresh.ToArray());
        Assert.AreEqual(12f, fx.PublishStats!.Cavalry.Stats!.MoveSpeedPercent);
        Assert.AreEqual(12f, fx.PublishStats.Cavalry.Stats.MountSpeedPercent);
        Assert.IsNull(fx.PublishStats.Player.Stats, "the lord takes the player record only");
        Assert.AreEqual(0, fx.ApplyBoon.Health.Count);
    }

    [TestMethod]
    public void ADamagePick_PublishesTheFactor_WithNoRefresh()
    {
        var rows = new[] { Row("hard", BoonCategory.Class, "archer", (BoonKeys.DamagePercent, 25f, BoonTarget.Player)) };
        var run = ThirdClear(Plan(new BoonPlan(rows, 1), "archer"));
        run.TryPickBoon("hard");

        var fx = run.Tick(Snap());

        Assert.AreEqual(1.25f, fx.PublishStats!.Player.Damage, 1e-5f);
        Assert.AreEqual(0, fx.ApplyBoon!.Refresh.Count);
        Assert.AreEqual(0, fx.ApplyBoon.Health.Count);
    }

    [TestMethod]
    public void AWarChestPick_NeedsNoPublishAndNoApply()
    {
        var rows = new[] { Row("coin", BoonCategory.WarChest, "", ("gold", 100f, BoonTarget.Player)) };
        var run = ThirdClear(Plan(new BoonPlan(rows, 1)));
        run.TryPickBoon("coin");

        var fx = run.Tick(Snap());

        Assert.IsNull(fx.ApplyBoon);
        Assert.IsNull(fx.PublishStats);
    }

    [TestMethod]
    public void TheNextWavesPublish_CarriesTheBoonsOwned()
    {
        var rows = new[] { Row("hard", BoonCategory.Class, "archer", (BoonKeys.DamageTakenPercent, -20f, BoonTarget.Player)) };
        var run = ThirdClear(Plan(new BoonPlan(rows, 1), "archer"));
        run.TryPickBoon("hard");
        run.Tick(Snap());

        run.TryStartWave();
        var fx = run.Tick(Snap());

        Assert.AreEqual(0.8f, fx.PublishStats!.Player.DamageTaken, 1e-5f);
    }

    [TestMethod]
    public void TheView_NamesTheClass_ForTheStripAndTheEndPanel()
    {
        var run = new SurvivalRun(Plan(BoonPlan.None, "archer"));

        Assert.AreEqual("{=C_archer}archer", run.GetView().ClassName);
        Assert.AreEqual("{=C_archer}archer", run.ClassName);
        Assert.AreEqual("", new SurvivalRun(Plan(BoonPlan.None)).GetView().ClassName);
    }
}
