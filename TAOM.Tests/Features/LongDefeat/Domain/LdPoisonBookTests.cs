using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.FordsOfWar.Domain;
using TAOM.Features.LongDefeat.Domain;

namespace TAOM.Tests.Features.LongDefeat.Domain;

/// <summary>
/// The doses of one Morgul Blades wave (phase2-plan.md section 2): Fords' <c>PoisonRules</c> underneath (one dose per victim, fractions carry), and the run's own count of doses (a victim newly
/// poisoned) apart from hits (every blades hit), since <c>PoisonRules.Totals</c> counts every <c>Apply</c>.
/// </summary>
[TestClass]
public class LdPoisonBookTests
{
    private static readonly AgentKey Victim = new(1);
    private static readonly AgentKey Poisoner = new(100);

    private static LdPoisonBook Book(float perSecond = 2f, float seconds = 6f) => new(perSecond, seconds);

    [TestMethod]
    public void AHit_AppliesADose_AndTicksTakeItsDamageOverTime()
    {
        var book = Book();
        Assert.IsTrue(book.Hit(Victim, Poisoner));

        var ticks = book.Tick(1f);

        Assert.AreEqual(1, ticks.Count);
        Assert.AreEqual(new PoisonTick(Victim, Poisoner, 2), ticks[0]);
    }

    [TestMethod]
    public void TheOwnerOfADose_IsTheHitter()
    {
        var book = Book();
        book.Hit(Victim, new AgentKey(7));

        Assert.AreEqual(new AgentKey(7), book.Tick(1f).Single().Attacker);
    }

    [TestMethod]
    public void ASecondHitWhilePoisoned_IsAcceptedButNotANewDose()
    {
        var book = Book();
        book.Hit(Victim, Poisoner);
        book.Tick(1f);

        Assert.IsTrue(book.Hit(Victim, Poisoner), "a refresh is an accepted hit");
        Assert.AreEqual(1, book.Doses);
    }

    [TestMethod]
    public void AHitAfterTheDoseRanOut_IsANewDose()
    {
        var book = Book(2f, 3f);
        book.Hit(Victim, Poisoner);
        book.Tick(1f);
        book.Tick(1f);
        book.Tick(1f);
        book.Hit(Victim, Poisoner);

        Assert.AreEqual(2, book.Doses);
    }

    [TestMethod]
    public void ADose_StopsAfterItsSeconds_AndDoesTheSecondsTimesTheRate()
    {
        var book = Book(2f, 6f);
        book.Hit(Victim, Poisoner);

        var damage = 0;
        for (var i = 0; i < 20; i++)
            damage += book.Tick(0.5f).Sum(t => t.Damage);

        Assert.AreEqual(12, damage, "6 s at 2 per second, whole points as the fractions carry");
        Assert.AreEqual(0, book.Tick(1f).Count);
    }

    [TestMethod]
    public void AForgottenVictim_LosesItsDose_AndTheLaterHitIsDropped()
    {
        var book = Book();
        book.Hit(Victim, Poisoner);
        book.Forget(Victim);

        Assert.AreEqual(0, book.Tick(1f).Count);
        Assert.IsFalse(book.Hit(Victim, Poisoner), "a victim the registry no longer holds drops the hit");
        Assert.AreEqual(1, book.Doses, "a dropped hit is not counted");
    }

    [TestMethod]
    public void AForgottenAttacker_DropsTheHit()
    {
        var book = Book();
        book.Forget(Poisoner);

        Assert.IsFalse(book.Hit(Victim, Poisoner));
        Assert.AreEqual(0, book.Doses);
    }

    [TestMethod]
    public void ADoseOfAForgottenAttacker_StillTicks_ForTheAdapterToPickAnotherOwner()
    {
        var book = Book();
        book.Hit(Victim, Poisoner);
        book.Forget(Poisoner);

        Assert.AreEqual(Poisoner, book.Tick(1f).Single().Attacker, "the key stays; the adapter finds the poisoner gone and chooses an owner");
    }

    [TestMethod]
    public void TwoVictims_HaveTwoDoses()
    {
        var book = Book();
        book.Hit(new AgentKey(1), Poisoner);
        book.Hit(new AgentKey(2), Poisoner);

        Assert.AreEqual(2, book.Doses);
        Assert.AreEqual(2, book.Tick(1f).Count);
    }

    [TestMethod]
    public void ABadRate_OrNoSeconds_PoisonsNobody()
    {
        Assert.IsFalse(Book(0f, 6f).Hit(Victim, Poisoner), "no damage per second");
        Assert.IsFalse(Book(2f, 0f).Hit(Victim, Poisoner), "no seconds");
        Assert.IsFalse(Book(float.NaN, 6f).Hit(Victim, Poisoner));
        Assert.IsFalse(Book(2f, float.PositiveInfinity).Hit(Victim, Poisoner));
    }
}
