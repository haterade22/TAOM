using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.SeatOfSeeing.Domain;

namespace TAOM.Tests.Features.SeatOfSeeing;

/// <summary>The geometry and the decision of the Seat's box select, and the transfer rule of Ctrl and a number.</summary>
[TestClass]
public class SeatBoxRuleTests
{
    // ---- the rectangle ----

    [TestMethod]
    public void Normalise_WhicheverWayTheDragWent_GivesTheSameRectangle()
    {
        var expected = new SeatRect(10f, 20f, 110f, 220f);

        Assert.AreEqual(expected, SeatBox.Normalise(10f, 20f, 110f, 220f));
        Assert.AreEqual(expected, SeatBox.Normalise(110f, 220f, 10f, 20f));
        Assert.AreEqual(expected, SeatBox.Normalise(110f, 20f, 10f, 220f));
        Assert.AreEqual(expected, SeatBox.Normalise(10f, 220f, 110f, 20f));
    }

    [TestMethod]
    public void Normalise_GivesTheWidthAndHeightAsPositiveNumbers()
    {
        var box = SeatBox.Normalise(300f, 400f, 100f, 150f);

        Assert.AreEqual(200f, box.Width);
        Assert.AreEqual(250f, box.Height);
    }

    [TestMethod]
    public void Contains_InsideAndOnTheEdges_IsTrue()
    {
        var box = new SeatRect(10f, 20f, 110f, 220f);

        Assert.IsTrue(SeatBox.Contains(in box, 50f, 100f));
        Assert.IsTrue(SeatBox.Contains(in box, 10f, 20f));
        Assert.IsTrue(SeatBox.Contains(in box, 110f, 220f));
    }

    [TestMethod]
    public void Contains_OutsideOnEitherAxis_IsFalse()
    {
        var box = new SeatRect(10f, 20f, 110f, 220f);

        Assert.IsFalse(SeatBox.Contains(in box, 9.9f, 100f));
        Assert.IsFalse(SeatBox.Contains(in box, 110.1f, 100f));
        Assert.IsFalse(SeatBox.Contains(in box, 50f, 19.9f));
        Assert.IsFalse(SeatBox.Contains(in box, 50f, 220.1f));
    }

    // ---- the decision ----

    [TestMethod]
    public void Decide_CompaniesInside_ReplacesTheSelection()
    {
        Assert.AreEqual(SeatSelectAction.ReplaceWith, SeatBox.Decide(false, 2, 1, false));
        Assert.AreEqual(SeatSelectAction.ReplaceWith, SeatBox.Decide(false, 1, 0, true));
    }

    [TestMethod]
    public void Decide_CompaniesInsideWithShift_AddsToTheSelection()
    {
        Assert.AreEqual(SeatSelectAction.Add, SeatBox.Decide(true, 3, 1, false));
        Assert.AreEqual(SeatSelectAction.Add, SeatBox.Decide(true, 1, 0, true));
    }

    [TestMethod]
    public void Decide_NothingInside_ClearsLikeAClickOnNothing()
    {
        Assert.AreEqual(SeatSelectAction.ClearAll, SeatBox.Decide(false, 0, 2, false));
    }

    [TestMethod]
    public void Decide_NothingInside_WithNothingSelected_WritesNothing()
    {
        Assert.AreEqual(SeatSelectAction.None, SeatBox.Decide(false, 0, 0, false));
    }

    [TestMethod]
    public void Decide_NothingInsideWithShift_NeverClears()
    {
        Assert.AreEqual(SeatSelectAction.None, SeatBox.Decide(true, 0, 2, false));
    }

    [TestMethod]
    public void Decide_NothingInsideWithThePanelOpen_NeverLeavesTheSelectionEmpty()
    {
        Assert.AreEqual(SeatSelectAction.None, SeatBox.Decide(false, 0, 2, true));
    }

    // ---- Ctrl and a number: which presses the patch takes ----

    [TestMethod]
    public void Intercepts_LiveWithControlAndANumberKey_IsTrueForEachCompany()
    {
        for (var index = 0; index < 8; index++)
            Assert.IsTrue(SeatTransferRule.Intercepts(true, true, index), index.ToString());
    }

    [TestMethod]
    public void Intercepts_NotLive_OrNoControl_OrNotACompany_RunsVanilla()
    {
        Assert.IsFalse(SeatTransferRule.Intercepts(false, true, 0));
        Assert.IsFalse(SeatTransferRule.Intercepts(true, false, 0));
        Assert.IsFalse(SeatTransferRule.Intercepts(true, true, 100), "key 0 is all companies");
        Assert.IsFalse(SeatTransferRule.Intercepts(true, true, 8));
        Assert.IsFalse(SeatTransferRule.Intercepts(true, true, -1));
    }

    // ---- Ctrl and a number: the plan ----

    [TestMethod]
    public void Plan_TargetNotSelected_MovesEverySelectedSoldier()
    {
        var plan = SeatTransferRule.Plan(new SeatTransferFacts(true, false, 2, 90, 10));

        Assert.AreEqual(SeatTransferRefusal.None, plan.Refusal);
        Assert.AreEqual(90, plan.Number);
    }

    [TestMethod]
    public void Plan_TargetSelectedToo_LeavesItOutOfTheSources()
    {
        var plan = SeatTransferRule.Plan(new SeatTransferFacts(true, true, 3, 100, 25));

        Assert.AreEqual(SeatTransferRefusal.None, plan.Refusal);
        Assert.AreEqual(75, plan.Number);
    }

    [TestMethod]
    public void Plan_EmptyTarget_IsAValidTarget()
    {
        // The troop transfer popup lists every company and bars only the selected ones (MissionOrderVM.OnTransferStarted, v1.5.7 :1036-1040).
        var plan = SeatTransferRule.Plan(new SeatTransferFacts(true, false, 1, 40, 0));

        Assert.AreEqual(SeatTransferRefusal.None, plan.Refusal);
        Assert.AreEqual(40, plan.Number);
    }

    [TestMethod]
    public void Plan_NonEmptyTarget_IsAValidTarget()
    {
        var plan = SeatTransferRule.Plan(new SeatTransferFacts(true, false, 1, 40, 60));

        Assert.AreEqual(SeatTransferRefusal.None, plan.Refusal);
        Assert.AreEqual(40, plan.Number);
    }

    [TestMethod]
    public void Plan_NothingSelected_IsRefused()
    {
        var plan = SeatTransferRule.Plan(new SeatTransferFacts(true, false, 0, 0, 10));

        Assert.AreEqual(SeatTransferRefusal.NoSource, plan.Refusal);
        Assert.AreEqual(0, plan.Number);
    }

    [TestMethod]
    public void Plan_OnlyTheTargetSelected_IsRefused()
    {
        var plan = SeatTransferRule.Plan(new SeatTransferFacts(true, true, 1, 50, 50));

        Assert.AreEqual(SeatTransferRefusal.NoSource, plan.Refusal);
    }

    [TestMethod]
    public void Plan_SourcesWithNoSoldiers_IsRefused_SoTheEngineNeverDividesByZero()
    {
        var plan = SeatTransferRule.Plan(new SeatTransferFacts(true, false, 2, 0, 10));

        Assert.AreEqual(SeatTransferRefusal.NoUnits, plan.Refusal);
    }

    [TestMethod]
    public void Plan_NoTargetCompany_IsRefused()
    {
        Assert.AreEqual(SeatTransferRefusal.NoTarget, SeatTransferRule.Plan(default).Refusal);
    }

    [TestMethod]
    public void Reason_NamesEachRefusal()
    {
        Assert.AreEqual("no-target", SeatTransferRule.Reason(SeatTransferRefusal.NoTarget));
        Assert.AreEqual("no-source", SeatTransferRule.Reason(SeatTransferRefusal.NoSource));
        Assert.AreEqual("no-units", SeatTransferRule.Reason(SeatTransferRefusal.NoUnits));
    }
}
