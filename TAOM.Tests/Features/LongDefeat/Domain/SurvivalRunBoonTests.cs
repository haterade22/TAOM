using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.LongDefeat.Domain;

namespace TAOM.Tests.Features.LongDefeat.Domain;

/// <summary>
/// The boons inside a run (phase2-plan.md section 2): a pick owed every third clear and open until taken (D2), made between waves only, a reroll that
/// swaps the offer, and each war chest and fortress effect landing on the number it names, with the affix cut keyed on the wave a boon was taken before.
/// </summary>
[TestClass]
public class SurvivalRunBoonTests
{
    private static BoonRow Boon(string id, params (string Key, float Value)[] effects) =>
        new(id, BoonCategory.WarChest, "{=K_" + id + "}" + id, "{=T_" + id + "}text", effects.Select(e => new BoonEffect(e.Key, e.Value)).ToList());

    private static BoonPlan Alone(string id, params (string Key, float Value)[] effects) => new(new[] { Boon(id, effects) }, 1);

    private static BoonPlan Five() => new(new[] { Boon("a", ("bountyPercent", 10f)), Boon("b", ("bountyPercent", 20f)), Boon("c", ("pricePercent", -10f)),
        Boon("d", ("gold", 100f)), Boon("e", ("waveBonusPercent", 10f)) }, 1);

    private static AffixPlan Affixes() => new(
        new[] { new AffixStage(5, 1), new AffixStage(10, 2) },
        new Dictionary<string, IReadOnlyDictionary<string, float>> { ["hide"] = new Dictionary<string, float> { ["armourPercent"] = 30f } },
        AffixPlan.Ids.ToDictionary(id => id, id => "{=N_" + id + "}" + id));

    private static RunPlan Plan(BoonPlan boons, int startWave = 1, ulong seed = 1, int rerolls = 0, RunRules? rules = null, int? stopAfter = null) =>
        new(
            "helms",
            new[] { new GarrisonRow("fow_militia", FormationSlot.Infantry, 30) },
            new WavePlan(new[] { new WaveSpec(20, new[] { new WaveTroop("orc", FormationSlot.Infantry, 3), new WaveTroop("skirm", FormationSlot.Ranged, 1) }) }, new WaveScaling(10, 300)),
            new ShopPlan(600, new[] { new ShopOffer("militia", "Militia", "fow_militia", FormationSlot.Infantry, 12, 180) }),
            rules ?? RunRules.Default with { PreparationSeconds = 1f, IntermissionSeconds = 10f },
            new RunOptions { StartWave = startWave, Seed = seed, StartRerolls = rerolls, StopAfterWaves = stopAfter })
        {
            Bounties = new Dictionary<string, int> { ["orc"] = 4, ["skirm"] = 6 },
            Affixes = Affixes(),
            Boons = boons,
        };

    private static RunSnapshot Snap() => new(1f, 0, 0, 40, 0, 0, 1000);

    private static SurvivalRun Begun(RunPlan plan)
    {
        var run = new SurvivalRun(plan);
        run.Begin();
        return run;
    }

    /// <summary>Ticks until <paramref name="clears"/> waves are cleared, sounding the horn after each clear so the next wave starts at once; stops on the clear tick. Returns every event seen.</summary>
    private static List<RunEvent> ClearTo(SurvivalRun run, int clears)
    {
        var events = new List<RunEvent>();
        for (var i = 0; i < 5000 && run.WavesCleared < clears && run.Result == null; i++)
        {
            if (run.GetView().ShopOpen && run.WavesCleared > 0 && run.GetView().Phase == RunPhase.Intermission)
                run.TryStartWave();
            events.AddRange(run.Tick(Snap()).Events);
        }

        Assert.AreEqual(clears, run.WavesCleared, "the run never reached the clear");
        return events;
    }

    private static SurvivalRun AtThirdClear(RunPlan plan)
    {
        var run = Begun(plan);
        ClearTo(run, 3);
        return run;
    }

    [TestMethod]
    public void TheThirdClear_OpensAnOffer_WithThreeCardsAndAnEvent()
    {
        var run = Begun(Plan(Five()));

        var events = ClearTo(run, 3);

        var view = run.GetView();
        Assert.AreEqual(3, view.BoonOffer.Count);
        Assert.AreEqual(0, view.OwedPicks);
        Assert.AreEqual(1, events.Count(e => e.Kind == RunEventKind.BoonOffered));
        Assert.AreEqual(3, events.Single(e => e.Kind == RunEventKind.BoonOffered).Wave);
        Assert.AreEqual("a", view.BoonOffer.Select(c => c.Id).OrderBy(x => x).First(), "cards come from the pool");
        Assert.AreEqual("{=K_a}a", view.BoonOffer.First(c => c.Id == "a").Name);
    }

    [TestMethod]
    public void BeforeTheThirdClear_NoOfferIsOpen()
    {
        var run = Begun(Plan(Five()));

        ClearTo(run, 2);

        Assert.AreEqual(0, run.GetView().BoonOffer.Count);
        Assert.AreEqual(PickResult.NoOffer, run.TryPickBoon("a"));
        Assert.AreEqual(RerollResult.NoOffer, run.TryReroll());
    }

    [TestMethod]
    public void APick_IsMadeBetweenWavesOnly_AndAnOpenOfferSurvivesTheNextWave()
    {
        var run = AtThirdClear(Plan(Five()));
        var offered = run.GetView().BoonOffer[0].Id;
        run.TryStartWave();
        run.Tick(Snap());
        Assert.AreEqual(RunPhase.Wave, run.GetView().Phase);

        Assert.AreEqual(PickResult.NotOpen, run.TryPickBoon(offered));
        Assert.AreEqual(RerollResult.NotOpen, run.TryReroll());
        ClearTo(run, 4);

        Assert.AreEqual(3, run.GetView().BoonOffer.Count, "an unmade pick stays open into later intermissions (D2)");
        Assert.AreEqual(PickResult.Ok, run.TryPickBoon(offered));
    }

    [TestMethod]
    public void ACardNotOffered_IsRefused_AndALateCallIsRunOver()
    {
        var run = AtThirdClear(Plan(Five()));
        var absent = new[] { "a", "b", "c", "d", "e" }.Except(run.GetView().BoonOffer.Select(c => c.Id)).First();

        Assert.AreEqual(PickResult.NotOffered, run.TryPickBoon(absent));
        Assert.AreEqual(PickResult.NotOffered, run.TryPickBoon("no_such_boon"));
        Assert.AreEqual(3, run.GetView().BoonOffer.Count);

        run.Abandon();

        Assert.AreEqual(PickResult.RunOver, run.TryPickBoon("a"));
        Assert.AreEqual(RerollResult.RunOver, run.TryReroll());
    }

    [TestMethod]
    public void ATakenBoon_IsOwned_ClosesTheOffer_AndEmitsAnEvent()
    {
        var run = AtThirdClear(Plan(Alone("solo", ("gold", 300f))));

        Assert.AreEqual(PickResult.Ok, run.TryPickBoon("solo"));

        Assert.AreEqual(0, run.GetView().BoonOffer.Count);
        Assert.AreEqual("solo", run.GetView().Boons.Single().Id);
        var taken = run.Tick(Snap()).Events.Single(e => e.Kind == RunEventKind.BoonTaken);
        StringAssert.Contains(taken.Detail, "boon=solo");
        Assert.AreEqual(PickResult.NoOffer, run.TryPickBoon("solo"), "a boon is taken once");
    }

    [TestMethod]
    public void AGoldBoon_PaysOnceAtThePick_IntoItsOwnSource()
    {
        var run = AtThirdClear(Plan(Alone("solo", ("gold", 300f))));
        var before = run.Gold;

        run.TryPickBoon("solo");

        Assert.AreEqual(before + 300, run.Gold);
        Assert.AreEqual(300, run.Purse.EarnedFrom(PurseSource.Boon));
        Assert.AreEqual(run.Purse.TotalEarned, run.Purse.EarnedFrom(PurseSource.Bounty) + run.Purse.EarnedFrom(PurseSource.WaveBonus)
            + run.Purse.EarnedFrom(PurseSource.EarlyStart) + run.Purse.EarnedFrom(PurseSource.Boon));
    }

    [TestMethod]
    public void ABountyBoon_RaisesWhatTheNextKillPays()
    {
        var run = AtThirdClear(Plan(Alone("solo", ("bountyPercent", 50f))));
        var before = run.Purse.EarnedFrom(PurseSource.Bounty);
        run.AttackerDown("orc");
        Assert.AreEqual(before + 4, run.Purse.EarnedFrom(PurseSource.Bounty));

        run.TryPickBoon("solo");
        run.AttackerDown("orc");

        Assert.AreEqual(before + 4 + 6, run.Purse.EarnedFrom(PurseSource.Bounty), "4 times 1.5");
    }

    [TestMethod]
    public void AWaveBonusBoon_RaisesTheNextClearsBonus_AndTheEventNamesIt()
    {
        var run = AtThirdClear(Plan(Alone("solo", ("waveBonusPercent", 50f))));
        run.TryPickBoon("solo");

        var events = ClearTo(run, 4);

        Assert.AreEqual((150 + 50 * 4) * 3 / 2, int.Parse(events.Last(e => e.Kind == RunEventKind.WaveCleared).Detail.Replace("bonus=", "")));
    }

    [TestMethod]
    public void AHornBoon_ScalesBothTheRateAndTheCap()
    {
        var rules = RunRules.Default with { PreparationSeconds = 1f, IntermissionSeconds = 100f, EarlyGoldPerSecond = 2f, EarlyGoldCap = 120 };
        var run = AtThirdClear(Plan(Alone("solo", ("earlyGoldPercent", 100f)), rules: rules));
        run.TryPickBoon("solo");

        run.TryStartWave();

        Assert.AreEqual(240, run.LastHornBonus, "100 s left at 2 a second is 400, capped at 120 doubled");
    }

    [TestMethod]
    public void APriceBoon_CutsWhatABuySpends_AndWhatTheViewShows()
    {
        var plan = Plan(Alone("solo", ("pricePercent", -50f)));
        var run = AtThirdClear(plan);
        run.TryPickBoon("solo");
        var before = run.Gold;

        Assert.AreEqual(90, run.GetView().Offers.Single().Price);
        Assert.AreEqual(BuyResult.Ok, run.TryBuy("militia"));

        Assert.AreEqual(before - 90, run.Gold);
        Assert.AreEqual(90, run.Purse.Spent);
        Assert.AreEqual(180, plan.Shop.Offers.Single().Price, "the shipped offer keeps its own price");
    }

    [TestMethod]
    public void APackBonus_AddsSoldiersToABoughtPack_AndToTheGarrisonCheck()
    {
        var run = AtThirdClear(Plan(Alone("solo", ("packBonus", 3f))));
        run.TryPickBoon("solo");

        Assert.AreEqual(15, run.GetView().Offers.Single().PackSize);
        Assert.AreEqual(BuyResult.Ok, run.TryBuy("militia"));
        var batch = run.Tick(Snap()).DefenderBatches.Single(b => b.Reinforcement);

        Assert.AreEqual(15, batch.Count);
    }

    [TestMethod]
    public void AGarrisonCapBoon_LetsAPackIntoAFullKeep()
    {
        var rules = RunRules.Default with { PreparationSeconds = 1f, IntermissionSeconds = 10f, GarrisonCap = 45 };
        var run = AtThirdClear(Plan(Alone("solo", ("garrisonCap", 30f)), rules: rules));
        var snapshot = new RunSnapshot(1f, 0, 0, 40, 0, 0, 1000);
        run.Tick(snapshot);

        Assert.AreEqual(BuyResult.GarrisonFull, run.TryBuy("militia"), "40 standing and 12 more is over 45");
        run.TryPickBoon("solo");

        Assert.AreEqual(BuyResult.Ok, run.TryBuy("militia"));
    }

    [TestMethod]
    public void AnIntermissionBoon_LengthensTheNextIntermissionOnly()
    {
        var run = AtThirdClear(Plan(Alone("solo", ("intermissionSeconds", 20f))));
        Assert.AreEqual(10f, run.GetView().SecondsLeft, 1e-3f);
        run.TryPickBoon("solo");

        ClearTo(run, 4);

        Assert.AreEqual(30f, run.GetView().SecondsLeft, 1e-3f);
    }

    [TestMethod]
    public void ARestBoon_RaisesTheShareTheClearHeals()
    {
        var run = AtThirdClear(Plan(Alone("solo", ("restShare", 0.25f))));
        run.TryPickBoon("solo");
        RunEffects? clear = null;
        run.TryStartWave();
        for (var i = 0; i < 200 && clear == null; i++)
        {
            var fx = run.Tick(Snap());
            if (fx.RestShare > 0f)
                clear = fx;
        }

        Assert.AreEqual(0.75f, clear!.RestShare, 1e-5f);
    }

    [TestMethod]
    public void AnAffixCutBoon_CutsTheWaveItWasTakenBefore_AsAPrefixOfTheRoll()
    {
        var plan = Plan(Alone("solo", ("affixCount", -1f)), startWave: 8, seed: 3);
        var full = plan.Affixes.Roll(3UL, 11);
        Assert.AreEqual(2, full.Count, "wave 11 is scheduled two affixes");
        var run = Begun(plan);
        ClearTo(run, 3);
        CollectionAssert.AreEqual(full.ToArray(), run.NextAffixes.ToArray(), "before the pick, the preview is the whole roll");

        run.TryPickBoon("solo");

        CollectionAssert.AreEqual(full.Take(1).ToArray(), run.NextAffixes.ToArray(), "the HUD shows the cut list");
        run.TryStartWave();
        Assert.AreEqual(11, run.Wave);
        CollectionAssert.AreEqual(full.Take(1).ToArray(), run.WaveAffixes.ToArray());
    }

    [TestMethod]
    public void AnAffixCutBoon_NeverCutsBelowNone_AndLeavesAnEarlierWaveAlone()
    {
        var plan = Plan(Alone("solo", ("affixCount", -2f), ("bountyPercent", 5f)), startWave: 8, seed: 3);
        var run = Begun(plan);
        ClearTo(run, 3);
        run.TryPickBoon("solo");
        var earlier = run.Stats.Waves.Single(w => w.Wave == 8);

        run.TryStartWave();

        Assert.AreEqual(0, run.WaveAffixes.Count, "two scheduled, two cut");
        CollectionAssert.AreEqual(plan.Affixes.Roll(3UL, 8).ToArray(), earlier.Affixes.ToArray(), "wave 8 carried its whole roll and was never touched");
        Assert.AreEqual(1, earlier.Affixes.Count);
    }

    [TestMethod]
    public void ARerollSwapsTheOffer_SpendsTheBank_AndEmitsAnEvent()
    {
        var run = AtThirdClear(Plan(Five(), rerolls: 1));
        var before = run.GetView().BoonOffer.Select(c => c.Id).ToArray();
        Assert.AreEqual(1, run.GetView().Rerolls);

        Assert.AreEqual(RerollResult.Ok, run.TryReroll());

        var after = run.GetView().BoonOffer.Select(c => c.Id).ToArray();
        Assert.AreEqual(3, after.Length);
        Assert.AreEqual(0, run.GetView().Rerolls);
        Assert.AreEqual(1, run.GetView().RerollsUsed);
        Assert.AreEqual(RerollResult.NoReroll, run.TryReroll());
        Assert.IsTrue(run.Tick(Snap()).Events.Any(e => e.Kind == RunEventKind.Rerolled));
        Assert.AreEqual(3, before.Length);
    }

    [TestMethod]
    public void TheFifthClear_EarnsAReroll_EvenWhenTheRunEndsOnIt()
    {
        var run = Begun(Plan(Five(), stopAfter: 5));

        ClearTo(run, 5);

        Assert.IsNotNull(run.Result);
        Assert.AreEqual(1, run.BoonSummary().Earned);
        Assert.AreEqual(1, run.BoonSummary().Banked);
    }

    [TestMethod]
    public void NoBoonPool_LapsesThePick_WithAnEvent_AndOpensNothing()
    {
        var run = Begun(Plan(BoonPlan.None));

        var events = ClearTo(run, 3);

        Assert.AreEqual(1, events.Count(e => e.Kind == RunEventKind.BoonLapsed));
        Assert.AreEqual(0, run.GetView().BoonOffer.Count);
        Assert.IsTrue(run.BoonSummary().Picks.Single().Lapsed);
    }

    [TestMethod]
    public void TheBoonSummary_ListsEachPickWithItsDrawsTheBoonTakenAndTheWaveItActsOn()
    {
        var run = AtThirdClear(Plan(Five(), rerolls: 1));
        run.TryReroll();
        var taken = run.GetView().BoonOffer[0].Id;
        run.TryPickBoon(taken);

        var summary = run.BoonSummary();

        var pick = summary.Picks.Single();
        Assert.AreEqual(1, pick.Pick);
        Assert.AreEqual(3, pick.Wave);
        Assert.AreEqual(2, pick.Draws.Count);
        Assert.AreEqual(taken, pick.Taken);
        Assert.AreEqual(4, pick.TakenBeforeWave);
        Assert.AreEqual((1, 1, 0), (summary.Earned, summary.Used, summary.Banked));
    }

    [TestMethod]
    public void WithNoBoonsTaken_TheRulesAndTheViewAreTheShippedOnes()
    {
        var plan = Plan(Five());
        var run = Begun(plan);

        Assert.AreEqual(plan.Rules, run.Rules);
        Assert.AreEqual(GoldFactors.One, run.Factors);
        Assert.AreSame(plan.Shop.Offers, run.GetView().Offers, "the view allocates nothing while no boon touches the shop");
    }
}
