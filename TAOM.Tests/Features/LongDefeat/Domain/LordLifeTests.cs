using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.LongDefeat.Domain;

namespace TAOM.Tests.Features.LongDefeat.Domain;

/// <summary>
/// The death rule's state and the general's seat of phase4-plan.md (Slice 3, D40 and D41), pure: the lord's body through a wave (his own, a taken soldier, none), the rise owed at the clear,
/// the bot's take-over clock, the <c>lord_falls</c> moment and the seat. The run that drives it is in <c>SurvivalRunLordTests</c>; the engine half (the spawn, the hand-back, the blow) is Slice 4's.
/// </summary>
[TestClass]
public class LordLifeTests
{
    private static LdLordFacts Own() => LdLordFacts.None with { Present = true, Own = true, Id = "lord_4_1" };

    private static LdLordFacts Soldier(string troop = "fow_militia") => LdLordFacts.None with { Present = true, Own = false, Id = troop };

    private static LdLordFacts Gone() => LdLordFacts.None;

    // ---- LordLife ----

    [TestMethod]
    public void ANewLife_IsTheLordsOwnBody_AndOwesNoRise()
    {
        var life = new LordLife();

        Assert.AreEqual(LordBody.Own, life.Body);
        Assert.IsFalse(life.RiseOwed);
        Assert.AreEqual((0, 0, 0), (life.Summary().Deaths.Count, life.Summary().Takeovers.Count, life.Summary().Rises.Count));
    }

    [TestMethod]
    public void AFall_ATake_AFallAgain_ATakeAgain_ThenTheClear_OwesOneRise_AndBooksEveryBody()
    {
        var life = new LordLife();

        life.Fell(3, "lord_4_1");
        life.Observe(Gone(), 3, byBot: false);
        life.Observe(Soldier("fow_militia"), 3, byBot: false);
        life.Observe(Soldier("fow_militia"), 3, byBot: false);
        life.Fell(3, "fow_militia");
        life.Observe(Soldier("fow_bowman"), 3, byBot: false);

        Assert.AreEqual(LordBody.Taken, life.Body);
        Assert.IsTrue(life.RiseOwed);
        Assert.IsTrue(life.HandBack, "the taken soldier goes back to the AI at the rise");
        var summary = life.Summary();
        CollectionAssert.AreEqual(new[] { new LordDeath(3, LordBody.Own, "lord_4_1", false), new LordDeath(3, LordBody.Taken, "fow_militia", false) }, summary.Deaths.ToArray());
        CollectionAssert.AreEqual(new[] { new LordTakeover(3, false, "fow_militia"), new LordTakeover(3, false, "fow_bowman") }, summary.Takeovers.ToArray(),
            "one row per soldier taken, however many ticks he is seen");

        life.Rose(4, "recorded", handedBack: true);

        Assert.AreEqual(LordBody.Own, life.Body);
        Assert.IsFalse(life.RiseOwed);
        CollectionAssert.AreEqual(new[] { new LordRise(4, "recorded", true) }, life.Summary().Rises.ToArray());
    }

    [TestMethod]
    public void AFall_WithNoSoldierTaken_Spectates_ThenOwesARise_WithNothingToHandBack()
    {
        var life = new LordLife();

        life.Fell(1, "lord_4_1");
        life.Observe(Gone(), 1, byBot: false);

        Assert.AreEqual(LordBody.None, life.Body);
        Assert.IsTrue(life.RiseOwed);
        Assert.IsFalse(life.HandBack);
        Assert.AreEqual(0, life.Summary().Takeovers.Count);
    }

    [TestMethod]
    public void AFall_NobodySawATakeFor_BooksTheNextFallAsASoldiers()
    {
        // A fall can come before the tick that would have read the take-over; the second body to fall before a rise still cannot be the lord's own.
        var life = new LordLife();

        life.Fell(2, "lord_4_1");
        life.Fell(2, "fow_militia");

        CollectionAssert.AreEqual(new[] { LordBody.Own, LordBody.Taken }, life.Summary().Deaths.Select(d => d.Body).ToArray());
    }

    [TestMethod]
    public void Observe_WithNoReading_ChangesNothing()
    {
        var life = new LordLife();
        life.Fell(1, "lord_4_1");

        life.Observe(null, 1, byBot: false);

        Assert.AreEqual(LordBody.None, life.Body, "a tick that read no lord says nothing about him");
    }

    [TestMethod]
    public void ATakeUnderAutoplay_IsBookedToTheBot()
    {
        var life = new LordLife();
        life.Fell(1, "lord_4_1");

        life.Observe(Soldier(), 1, byBot: true);

        Assert.IsTrue(life.Summary().Takeovers.Single().ByBot);
    }

    [TestMethod]
    public void TheBotsTakeover_IsAskedForFiveSecondsAfterTheFall_AndAgainWhileNoBodyStands()
    {
        var life = new LordLife();
        Assert.IsFalse(life.BotTakeoverDue(10f), "a standing lord needs no body");
        life.Fell(1, "lord_4_1");

        var asks = Enumerable.Range(0, 12).Select(_ => life.BotTakeoverDue(1f)).ToList();

        CollectionAssert.AreEqual(new[] { 4, 9 }, asks.Select((due, i) => due ? i : -1).Where(i => i >= 0).ToArray(), "at 5 s and at 10 s");
        life.Observe(Soldier(), 1, byBot: true);
        Assert.IsFalse(life.BotTakeoverDue(10f), "a taken body ends the asking");
    }

    [TestMethod]
    public void TheBotsTakeover_CountsEachAbsenceFromItsStart_AfterARoutToo()
    {
        // A rout removes the body with no fall, so only the reading says it is gone; the seconds of an earlier absence must not shorten this one.
        var life = new LordLife();
        life.Observe(Gone(), 1, byBot: true);
        for (var i = 0; i < 4; i++)
            Assert.IsFalse(life.BotTakeoverDue(1f));
        life.Rose(2, "respawn", handedBack: false);

        life.Observe(Gone(), 2, byBot: true);

        Assert.IsFalse(life.BotTakeoverDue(1f), "one second into the second absence, not five");
        Assert.IsFalse(life.BotTakeoverDue(3.9f));
        Assert.IsTrue(life.BotTakeoverDue(0.2f));
    }

    [DataTestMethod]
    [DataRow(float.NaN)]
    [DataRow(-1f)]
    [DataRow(0f)]
    public void TheBotsTakeover_IgnoresATickThatIsNoTime(float dt)
    {
        var life = new LordLife();
        life.Fell(1, "lord_4_1");

        Assert.IsFalse(life.BotTakeoverDue(dt));
        Assert.IsFalse(life.BotTakeoverDue(4.9f), "the bad tick added nothing, and neither did it poison the clock");
        Assert.IsTrue(life.BotTakeoverDue(0.2f));
    }

    [TestMethod]
    public void TheForcedFall_ComesOnce_TwentySecondsIntoTheFight_AndMarksThatDeath()
    {
        var life = new LordLife();

        Assert.IsFalse(life.ForcedFallDue(30f, fighting: false, wave: 2), "the first batch is not out yet");
        var asks = Enumerable.Range(0, 25).Select(_ => life.ForcedFallDue(1f, fighting: true, wave: 2)).ToList();

        CollectionAssert.AreEqual(new[] { 19 }, asks.Select((due, i) => due ? i : -1).Where(i => i >= 0).ToArray());
        life.Fell(2, "lord_4_1");
        life.Fell(2, "fow_militia");
        CollectionAssert.AreEqual(new[] { true, false }, life.Summary().Deaths.Select(d => d.Forced).ToArray(), "only the death the run asked for is forced");
    }

    [TestMethod]
    public void AForcedFall_NobodyCarriedOut_MarksNoLaterDeath()
    {
        var life = new LordLife();
        Assert.IsTrue(life.ForcedFallDue(20f, fighting: true, wave: 1));

        life.Fell(2, "lord_4_1");

        Assert.IsFalse(life.Summary().Deaths.Single().Forced, "the ask was for wave 1; a natural death in wave 2 is not the test option's");
    }

    [TestMethod]
    public void AForcedFall_MarksOnlyTheLordsOwnBody()
    {
        var life = new LordLife();
        life.Fell(1, "lord_4_1");
        life.Observe(Own(), 1, byBot: false);
        Assert.IsTrue(life.ForcedFallDue(20f, fighting: true, wave: 1));
        life.Observe(Soldier(), 1, byBot: false);

        life.Fell(1, "fow_militia");

        Assert.IsFalse(life.Summary().Deaths.Last().Forced, "a soldier's fall in the armed wave is not the lord's forced fall");
    }

    [TestMethod]
    public void TheForcedFall_IsNotAskedOfATakenSoldier_OrOfNobody()
    {
        var taken = new LordLife();
        taken.Fell(1, "lord_4_1");
        taken.Observe(Soldier(), 1, byBot: true);
        Assert.IsFalse(taken.ForcedFallDue(60f, fighting: true, wave: 1), "the test option fells the lord's own body only");

        var down = new LordLife();
        down.Fell(1, "lord_4_1");
        Assert.IsFalse(down.ForcedFallDue(60f, fighting: true, wave: 1));
    }

    // ---- the general's seat (D41) ----

    [TestMethod]
    public void TheSeat_IsTheLords_WhileHeHasABody_OwnOrTaken()
    {
        Assert.AreEqual(LdGeneralSeat.Lord, GeneralSeat.Choose(LordBody.Own, new[] { "aragorn" }));
        Assert.AreEqual(LdGeneralSeat.Lord, GeneralSeat.Choose(LordBody.Taken, new[] { "aragorn" }));
    }

    [TestMethod]
    public void TheSeat_GoesToTheFirstStandingHero_WhileTheLordIsDown_AndToNobodyWithNone()
    {
        Assert.AreEqual(new LdGeneralSeat(GeneralSeatKind.Hero, "gimli"), GeneralSeat.Choose(LordBody.None, new[] { "gimli", "aragorn" }));
        Assert.AreEqual(LdGeneralSeat.Empty, GeneralSeat.Choose(LordBody.None, Array.Empty<string>()), "never a dead body");
    }
}
