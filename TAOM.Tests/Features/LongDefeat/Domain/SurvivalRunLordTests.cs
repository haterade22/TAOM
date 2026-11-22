using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.LongDefeat.Domain;

namespace TAOM.Tests.Features.LongDefeat.Domain;

/// <summary>
/// The lord's side of the run (phase4-plan.md, Slice 3; <c>SurvivalRun.Lord.cs</c>): the rise a clear orders, the bot's take-over and the forced fall as the run's clocks ask for them, the
/// general's seat as it changes hands, and what the report lists. The state machine itself is in <c>LordLifeTests</c>.
/// </summary>
[TestClass]
public class SurvivalRunLordTests
{
    private static LdLordFacts Own() => LdLordFacts.None with { Present = true, Own = true, Id = "lord_4_1" };

    private static LdLordFacts Soldier(string troop = "fow_militia") => LdLordFacts.None with { Present = true, Own = false, Id = troop };

    private static LdLordFacts Gone() => LdLordFacts.None;

    private static readonly HeroOffer Aragorn = new("aragorn", "Aragorn", "", "taom_hero_aragorn", 300, 3f);
    private static readonly HeroOffer Gimli = new("gimli", "Gimli", "", "taom_hero_gimli", 250, 2.5f);

    private static RunPlan Plan(bool autoplay = false, int? lordFalls = null, int? stopAfter = null, bool mounted = false) =>
        new(
            "helms",
            new[] { new GarrisonRow("fow_militia", FormationSlot.Infantry, 30) },
            new WavePlan(new[] { new WaveSpec(20, new[] { new WaveTroop("orc", FormationSlot.Infantry, 3) }) }, new WaveScaling(10, 300)),
            new ShopPlan(5000, Array.Empty<ShopOffer>()) { Heroes = new[] { Aragorn, Gimli } },
            RunRules.Default with { PreparationSeconds = 1f, IntermissionSeconds = 30f },
            new RunOptions { Seed = 1UL, Autoplay = autoplay, LordFalls = lordFalls, StopAfterWaves = stopAfter })
        {
            Bounties = new Dictionary<string, int> { ["orc"] = 4 },
            Loadout = new LdLoadout(mounted ? "cavalry" : "sword", mounted, new[] { new LdKitSlot("Item0", "kit_sword") }),
        };

    private static RunSnapshot Snap(LdLordFacts? lord, int attackers = 20, int defenders = 40) => new(1f, attackers, 0, defenders, 0, 0, 1000) { Lord = lord };

    /// <summary>A run in the fight of wave 1: the preparation lasts a tick, and the first batch of 20 is out after a few more.</summary>
    private static SurvivalRun Fighting(RunPlan plan)
    {
        var run = new SurvivalRun(plan);
        run.Begin();
        for (var i = 0; i < 20 && run.Tick(Snap(Own(), attackers: 0)).AttackerBatches.Count == 0; i++)
        {
        }

        Assert.AreEqual(RunPhase.Wave, run.Phase);
        run.Tick(Snap(Own()));
        return run;
    }

    /// <summary>Ticks with no attacker standing until the wave clears, and returns the effects of the clear's tick.</summary>
    private static RunEffects Clear(SurvivalRun run, LdLordFacts? lord)
    {
        var cleared = run.WavesCleared;
        for (var i = 0; i < 200; i++)
        {
            var fx = run.Tick(Snap(lord, attackers: 0));
            if (run.WavesCleared > cleared || run.Result != null)
                return fx;
        }

        Assert.Fail("the wave never cleared");
        return new RunEffects();
    }

    [TestMethod]
    public void AClear_WithTheLordInHisOwnBody_OrdersNoRise()
    {
        var run = Fighting(Plan());

        Assert.IsNull(Clear(run, Own()).RiseLord);
    }

    [TestMethod]
    public void AClear_AfterAFall_OrdersTheRise_AndTheAnswerEndsTheDebt()
    {
        var run = Fighting(Plan());
        run.PlayerDown("lord_4_1");

        var rise = Clear(run, Gone()).RiseLord;

        Assert.AreEqual(new LordRiseOrder(HandBack: false, AtHome: false), rise);
        run.LordRose("respawn", handedBack: false);
        CollectionAssert.AreEqual(new[] { new LordRise(2, "respawn", false) }, run.LordSummary().Rises.ToArray(), "the rise precedes wave 2");
        Assert.AreEqual(1, run.Stats.PlayerDeaths);
        run.TryStartWave();
        for (var i = 0; i < 20 && run.Tick(Snap(Own(), attackers: 0)).AttackerBatches.Count == 0; i++)
        {
        }

        run.Tick(Snap(Own()));
        Assert.IsNull(Clear(run, Own()).RiseLord, "he is in a body of his own again, so the next clear owes no rise");
    }

    [TestMethod]
    public void AClear_InATakenSoldier_OrdersTheRise_WithTheHandBack()
    {
        var run = Fighting(Plan());
        run.PlayerDown("lord_4_1");
        run.Tick(Snap(Soldier()));

        Assert.AreEqual(new LordRiseOrder(HandBack: true, AtHome: false), Clear(run, Soldier()).RiseLord);
    }

    [TestMethod]
    public void AMountedLord_RisesAtHisHomePoint()
    {
        var run = Fighting(Plan(mounted: true));
        run.PlayerDown("lord_4_1");

        Assert.IsTrue(Clear(run, Gone()).RiseLord!.AtHome, "the cavalry class respawns where it sallies from (D48)");
    }

    [TestMethod]
    public void ARiseTheFieldRefused_IsOrderedAgainAtTheNextClear()
    {
        var run = Fighting(Plan());
        run.PlayerDown("lord_4_1");
        Assert.IsNotNull(Clear(run, Gone()).RiseLord);

        run.TryStartWave();
        for (var i = 0; i < 20 && run.Tick(Snap(Gone(), attackers: 0)).AttackerBatches.Count == 0; i++)
        {
        }

        run.Tick(Snap(Gone()));
        Assert.IsNotNull(Clear(run, Gone()).RiseLord, "nobody answered the first order, so the lord is still down");
    }

    [TestMethod]
    public void TheClearThatEndsTheRun_OrdersNoRise()
    {
        var run = Fighting(Plan(stopAfter: 1));
        run.PlayerDown("lord_4_1");

        var fx = Clear(run, Gone());

        Assert.AreEqual(RunEndReason.TargetReached, run.Result!.Reason);
        Assert.IsNull(fx.RiseLord, "a finished run has no intermission to rise into");
    }

    [TestMethod]
    public void AFall_AsTheLastDefender_EndsTheRun_AsDefendersDead()
    {
        var run = Fighting(Plan());
        for (var i = 0; i < 300 && run.GetView().DefendersPending > 0; i++)
            run.Tick(Snap(Own()));
        Assert.AreEqual(0, run.GetView().DefendersPending, "the garrison has mustered, so nobody is still to come");
        run.PlayerDown("lord_4_1");

        var fx = run.Tick(Snap(Gone(), defenders: 0));

        Assert.AreEqual(RunEndReason.DefendersDead, run.Result!.Reason);
        Assert.IsNull(fx.RiseLord);
    }

    [TestMethod]
    public void UnderAutoplay_TheRunAsksTheBotToTakeOver_FiveSecondsAfterTheFall_AndNeverForAPerson()
    {
        var bot = Fighting(Plan(autoplay: true));
        bot.PlayerDown("lord_4_1");
        var asks = Enumerable.Range(0, 6).Select(_ => bot.Tick(Snap(Gone())).TakeOverForBot).ToList();
        CollectionAssert.AreEqual(new[] { false, false, false, false, true, false }, asks);

        var person = Fighting(Plan());
        person.PlayerDown("lord_4_1");
        Assert.IsFalse(Enumerable.Range(0, 12).Any(_ => person.Tick(Snap(Gone())).TakeOverForBot), "a person presses E himself");
    }

    [TestMethod]
    public void WithLordFalls_TheRunFellsTheLordOnce_InThatWave_AndTheReportMarksTheDeathForced()
    {
        var run = Fighting(Plan(lordFalls: 1));

        var asks = Enumerable.Range(0, 30).Select(_ => run.Tick(Snap(Own())).FellLord).ToList();

        Assert.AreEqual(1, asks.Count(a => a), "once");
        Assert.IsTrue(asks.IndexOf(true) >= 17, "about 20 s after the first batch is out");
        run.PlayerDown("lord_4_1");
        Assert.IsTrue(run.LordSummary().Deaths.Single().Forced);
    }

    [TestMethod]
    public void TheTickThatClearsTheWave_AsksForNoTakeover()
    {
        var run = Fighting(Plan(autoplay: true));
        run.PlayerDown("lord_4_1");
        for (var i = 0; i < 4; i++)
            Assert.IsFalse(run.Tick(Snap(Gone())).TakeOverForBot);

        var fx = run.Tick(Snap(Gone(), attackers: 0));

        Assert.AreEqual(1, run.WavesCleared, "the fifth second is the clear's tick");
        Assert.IsNotNull(fx.RiseLord);
        Assert.IsFalse(fx.TakeOverForBot, "the wave is over: the rise answers the fall, and no soldier is taken after it");
    }

    [TestMethod]
    public void WithLordFalls_ForAnotherWave_NobodyIsFelled()
    {
        var run = Fighting(Plan(lordFalls: 2));

        Assert.IsFalse(Enumerable.Range(0, 40).Any(_ => run.Tick(Snap(Own())).FellLord));
    }

    [TestMethod]
    public void TheSeat_IsWrittenWhenItChanges_ToAStandingHero_AndBackToTheLord()
    {
        var run = new SurvivalRun(Plan());
        run.Begin();
        Assert.AreEqual(HeroResult.Ok, run.TryBuyHero("gimli"));
        Assert.AreEqual(HeroResult.Ok, run.TryBuyHero("aragorn"));
        var first = run.Tick(Snap(Own(), attackers: 0));
        run.HeroSpawned("gimli", true);
        run.HeroSpawned("aragorn", true);
        Assert.IsNull(first.General, "the lord holds the seat from the start, as his adoption writes it");
        Assert.IsNull(run.Tick(Snap(Own())).General);

        run.PlayerDown("lord_4_1");
        Assert.AreEqual(new LdGeneralSeat(GeneralSeatKind.Hero, "aragorn"), run.Tick(Snap(Gone())).General, "the first standing hero in the shop's order");
        Assert.IsNull(run.Tick(Snap(Gone())).General, "written once, not every tick");

        run.HeroDown("aragorn", loss: true);
        Assert.AreEqual(new LdGeneralSeat(GeneralSeatKind.Hero, "gimli"), run.Tick(Snap(Gone())).General);
        run.HeroDown("gimli", loss: true);
        Assert.AreEqual(LdGeneralSeat.Empty, run.Tick(Snap(Gone())).General);

        Assert.AreEqual(LdGeneralSeat.Lord, run.Tick(Snap(Soldier())).General, "a taken soldier is the player's body, so the seat is his again");
    }
}
