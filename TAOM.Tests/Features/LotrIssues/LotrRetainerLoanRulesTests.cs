using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.LotrIssues;
using TAOM.Features.LotrIssues.Domain;

namespace TAOM.Tests.Features.LotrIssues;

[TestClass]
public class LotrRetainerLoanRulesTests
{
    private static LotrTroopStack Stack(string id, int tier, int number, bool hero = false)
        => new LotrTroopStack(id, tier, number, hero);

    [TestMethod]
    public void TrackSurvivors_FallsToTheLineCountInParty()
        => Assert.AreEqual(3, LotrRetainerLoanRules.TrackSurvivors(8, 3));

    [TestMethod]
    public void TrackSurvivors_NeverRisesAboveLastKnown()
        => Assert.AreEqual(4, LotrRetainerLoanRules.TrackSurvivors(4, 30));

    [TestMethod]
    public void TrackSurvivors_NegativeLineCount_IsZero()
        => Assert.AreEqual(0, LotrRetainerLoanRules.TrackSurvivors(4, -2));

    [DataTestMethod]
    [DataRow(true, 0, false)]
    [DataRow(false, 5, false)]
    [DataRow(true, 5, true)]
    public void CountsAsTrainingWin_OnlyWhenPlayerWonAndRetainersLive(bool won, int survivors, bool expected)
        => Assert.AreEqual(expected, LotrRetainerLoanRules.CountsAsTrainingWin(won, survivors));

    [TestMethod]
    public void Grade_AllReturned_IsTotal()
        => Assert.AreEqual(LoanReturnGrade.Total, LotrRetainerLoanRules.Grade(8, 8));

    [TestMethod]
    public void Grade_MoreThanHalf_IsPartial()
        => Assert.AreEqual(LoanReturnGrade.Partial, LotrRetainerLoanRules.Grade(5, 8));

    [TestMethod]
    public void Grade_HalfOrFewer_IsWeak()
    {
        Assert.AreEqual(LoanReturnGrade.Weak, LotrRetainerLoanRules.Grade(4, 8));
        Assert.AreEqual(LoanReturnGrade.Weak, LotrRetainerLoanRules.Grade(1, 8));
    }

    [TestMethod]
    public void Grade_NoneReturned_IsFailed()
        => Assert.AreEqual(LoanReturnGrade.Failed, LotrRetainerLoanRules.Grade(0, 8));

    [TestMethod]
    public void Grade_ZeroLent_IsFailed()
        => Assert.AreEqual(LoanReturnGrade.Failed, LotrRetainerLoanRules.Grade(3, 0));

    [TestMethod]
    public void RelationFor_MatchesVanillaGrades()
    {
        Assert.AreEqual(5, LotrRetainerLoanRules.RelationFor(LoanReturnGrade.Total));
        Assert.AreEqual(3, LotrRetainerLoanRules.RelationFor(LoanReturnGrade.Partial));
        Assert.AreEqual(1, LotrRetainerLoanRules.RelationFor(LoanReturnGrade.Weak));
        Assert.AreEqual(-5, LotrRetainerLoanRules.RelationFor(LoanReturnGrade.Failed));
    }

    [TestMethod]
    public void PlanReturn_TakesHighestTierFirst()
    {
        var plan = LotrRetainerLoanRules.PlanReturn(
            new[] { Stack("t1", 1, 5), Stack("t3", 3, 2), Stack("t2", 2, 4) }, 4);
        CollectionAssert.AreEqual(new[] { 0, 2, 2 }, plan);
    }

    [TestMethod]
    public void PlanReturn_NeverTakesMoreThanCount()
    {
        var plan = LotrRetainerLoanRules.PlanReturn(new[] { Stack("a", 1, 9), Stack("b", 2, 9) }, 5);
        Assert.AreEqual(5, plan[0] + plan[1]);
        Assert.AreEqual(5, plan[1]);
    }

    [TestMethod]
    public void PlanReturn_TiesTakeListOrder()
    {
        var plan = LotrRetainerLoanRules.PlanReturn(new[] { Stack("a", 2, 3), Stack("b", 2, 3) }, 4);
        CollectionAssert.AreEqual(new[] { 3, 1 }, plan);
    }

    [TestMethod]
    public void PlanReturn_SkipsHeroStacks()
    {
        var plan = LotrRetainerLoanRules.PlanReturn(new[] { Stack("hero", 6, 1, hero: true), Stack("a", 1, 2) }, 5);
        CollectionAssert.AreEqual(new[] { 0, 2 }, plan);
    }

    [TestMethod]
    public void PlanReturn_NullOrEmpty_ReturnsAllZeros()
    {
        Assert.AreEqual(0, LotrRetainerLoanRules.PlanReturn(null, 3).Length);
        CollectionAssert.AreEqual(new[] { 0, 0 }, LotrRetainerLoanRules.PlanReturn(new[] { Stack("a", 1, 4), Stack("b", 2, 4) }, 0));
    }
}
