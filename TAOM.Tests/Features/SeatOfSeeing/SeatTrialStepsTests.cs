using System;
using System.Linq;
using System.Text.RegularExpressions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.SeatOfSeeing.Domain;

namespace TAOM.Tests.Features.SeatOfSeeing;

/// <summary>The field trial's step order and pass numbers (build-spec section 12): the table, the choice of steps, and the numbers a pass rests on.</summary>
[TestClass]
public class SeatTrialStepsTests
{
    private static readonly string[] StageOne =
        { "refuse_in_deployment", "enter", "fly", "hero_stands", "leave_glide", "reenter_keeps_height" };

    // ---- The table ----

    [TestMethod]
    public void All_StartsWithTheStageOneStepsInTheContractsOrder()
    {
        CollectionAssert.AreEqual(StageOne, SeatTrialSteps.All.Take(StageOne.Length).Select(s => s.Name).ToArray());
    }

    [TestMethod]
    public void All_IsTheJoinOfEachFeaturesRowList_TheSeatsThenMarshalsWord()
    {
        CollectionAssert.AreEqual(SeatTrialRows.Rows.Concat(TAOM.Features.MarshalsWord.Domain.MarshalTrialRows.Rows).ToArray(), SeatTrialSteps.All.ToArray());
    }

    [TestMethod]
    public void All_RowsAreWellFormed_NamesUniqueSnakeCaseBudgetsPositive()
    {
        Assert.AreEqual(SeatTrialSteps.All.Count, SeatTrialSteps.All.Select(s => s.Name).Distinct().Count());
        foreach (var step in SeatTrialSteps.All)
        {
            StringAssert.Matches(step.Name, new Regex("^[a-z]+(_[a-z]+)*$"), step.Name);
            Assert.IsTrue(step.BudgetSeconds > 0f, step.Name + " budget");
        }
    }

    [TestMethod]
    public void All_ARowsPrerequisitesPrecedeIt_SoTheTableOrderIsAValidRunOrder()
    {
        for (var i = 0; i < SeatTrialSteps.All.Count; i++)
        {
            foreach (var needed in SeatTrialSteps.All[i].Requires)
                Assert.IsTrue(SeatTrialSteps.All.Take(i).Any(s => s.Name == needed), SeatTrialSteps.All[i].Name + " needs " + needed + " earlier");
        }
    }

    // ---- Choosing ----

    [TestMethod]
    public void TryChoose_NoListOrABlankOne_ChoosesEveryStep()
    {
        foreach (var list in new string?[] { null, "", "  " })
        {
            Assert.IsTrue(SeatTrialSteps.TryChoose(list, out var steps, out var error), error);
            CollectionAssert.AreEqual(SeatTrialSteps.All.ToArray(), steps.ToArray());
        }
    }

    [TestMethod]
    public void TryChoose_AList_KeepsTheTablesOrderDropsDuplicatesAndIgnoresCaseAndSpaces()
    {
        Assert.IsTrue(SeatTrialSteps.TryChoose(" Fly ,enter,FLY", out var steps, out var error), error);

        CollectionAssert.AreEqual(new[] { "enter", "fly" }, steps.Select(s => s.Name).ToArray());
    }

    [TestMethod]
    public void TryChoose_AnUnknownName_IsRefusedByName()
    {
        Assert.IsFalse(SeatTrialSteps.TryChoose("enter,flye", out var steps, out var error));

        StringAssert.Contains(error, "flye");
        Assert.AreEqual(0, steps.Count);
    }

    [TestMethod]
    public void TryChoose_AListOfOnlySeparators_IsRefused_BecauseItNamesNoStep()
    {
        foreach (var list in new[] { ",", " , ,", ",,," })
        {
            Assert.IsFalse(SeatTrialSteps.TryChoose(list, out var steps, out var error), list);
            StringAssert.Contains(error, "names no step");
            Assert.AreEqual(0, steps.Count);
        }
    }

    [TestMethod]
    public void TryChoose_AStepWithoutItsPrerequisite_IsRefusedNamingBoth()
    {
        Assert.IsFalse(SeatTrialSteps.TryChoose("fly", out _, out var error));

        StringAssert.Contains(error, "fly");
        StringAssert.Contains(error, "enter");
    }

    [TestMethod]
    public void TryChoose_ReenterWithoutTheLeave_IsRefused_AndWithBothPrerequisitesIsAccepted()
    {
        Assert.IsFalse(SeatTrialSteps.TryChoose("enter,reenter_keeps_height", out _, out var error));
        StringAssert.Contains(error, "leave_glide");

        Assert.IsTrue(SeatTrialSteps.TryChoose("enter,leave_glide,reenter_keeps_height", out var steps, out error), error);
        Assert.AreEqual(3, steps.Count);
    }

    [TestMethod]
    public void TryChoose_TheDeploymentStepAlone_IsAccepted()
    {
        Assert.IsTrue(SeatTrialSteps.TryChoose("refuse_in_deployment", out var steps, out var error), error);
        Assert.AreEqual(1, steps.Count);
    }

    // ---- The numbers ----

    [TestMethod]
    public void FlewExpected_AcceptsTheExactDistanceAndTheEdgesOfTwentyFivePercent()
    {
        Assert.IsTrue(SeatTrialRules.FlewExpected(expected: 60f, moved: 60f));
        Assert.IsTrue(SeatTrialRules.FlewExpected(60f, 45f), "exactly 25 percent short");
        Assert.IsTrue(SeatTrialRules.FlewExpected(60f, 75f), "exactly 25 percent over");
    }

    [TestMethod]
    public void FlewExpected_RefusesPastTheEdgesZeroAndNonFiniteNumbers()
    {
        Assert.IsFalse(SeatTrialRules.FlewExpected(60f, 44.9f));
        Assert.IsFalse(SeatTrialRules.FlewExpected(60f, 75.1f));
        Assert.IsFalse(SeatTrialRules.FlewExpected(60f, 0f), "a view that did not move");
        Assert.IsFalse(SeatTrialRules.FlewExpected(0f, 0f), "no expected distance proves nothing");
        Assert.IsFalse(SeatTrialRules.FlewExpected(-1f, 1f));
        Assert.IsFalse(SeatTrialRules.FlewExpected(60f, float.NaN));
        Assert.IsFalse(SeatTrialRules.FlewExpected(float.NaN, 60f));
        Assert.IsFalse(SeatTrialRules.FlewExpected(float.PositiveInfinity, float.PositiveInfinity));
    }

    [TestMethod]
    public void HeroStood_NeedsASpeedUnderHalfAMetrePerSecond()
    {
        Assert.IsTrue(SeatTrialRules.HeroStood(0f));
        Assert.IsTrue(SeatTrialRules.HeroStood(0.49f));
        Assert.IsFalse(SeatTrialRules.HeroStood(0.5f));
        Assert.IsFalse(SeatTrialRules.HeroStood(3.5f));
        Assert.IsFalse(SeatTrialRules.HeroStood(float.NaN));
    }

    [TestMethod]
    public void OwnedEnoughFrames_NeedsMoreThanThirtyPerRealSecond()
    {
        Assert.IsFalse(SeatTrialRules.OwnedEnoughFrames(90, 3f), "exactly 30 a second is not above 30");
        Assert.IsTrue(SeatTrialRules.OwnedEnoughFrames(91, 3f));
        Assert.IsFalse(SeatTrialRules.OwnedEnoughFrames(91, 4f), "the window is the real seconds, so a longer window needs more frames");
        Assert.IsTrue(SeatTrialRules.OwnedEnoughFrames(61, 2f));
        Assert.IsFalse(SeatTrialRules.OwnedEnoughFrames(0, 3f));
    }

    [TestMethod]
    public void OwnedEnoughFrames_AWindowThatIsNotAPositiveNumber_ProvesNothing()
    {
        Assert.IsFalse(SeatTrialRules.OwnedEnoughFrames(1000, 0f));
        Assert.IsFalse(SeatTrialRules.OwnedEnoughFrames(1000, -1f));
        Assert.IsFalse(SeatTrialRules.OwnedEnoughFrames(1000, float.NaN));
        Assert.IsFalse(SeatTrialRules.OwnedEnoughFrames(1000, float.PositiveInfinity));
    }

    [TestMethod]
    public void HeightKept_AllowsOneMetreOrTenPercentWhicheverIsLarger()
    {
        const float entry = 22f;
        Assert.IsTrue(SeatTrialRules.HeightKept(32f, 32f, entry));
        Assert.IsTrue(SeatTrialRules.HeightKept(32f, 35.1f, entry), "inside ten percent of 32 m");
        Assert.IsFalse(SeatTrialRules.HeightKept(32f, 35.3f, entry), "outside ten percent of 32 m");
        Assert.IsTrue(SeatTrialRules.HeightKept(32f, 28.9f, entry));
        Assert.IsFalse(SeatTrialRules.HeightKept(32f, 28.7f, entry));
        Assert.IsTrue(SeatTrialRules.HeightKept(2f, 2.9f, entry), "one metre floor at low height");
        Assert.IsFalse(SeatTrialRules.HeightKept(2f, 3.2f, entry));
        Assert.IsFalse(SeatTrialRules.HeightKept(32f, 22f, entry), "a height that fell back to the entry height is not a kept one");
        Assert.IsFalse(SeatTrialRules.HeightKept(float.NaN, 22f, entry), "a step that never measured the first height cannot pass");
        Assert.IsFalse(SeatTrialRules.HeightKept(32f, float.NaN, entry));
        Assert.IsFalse(SeatTrialRules.HeightKept(32f, 32f, float.NaN));
    }

    [TestMethod]
    public void HeightKept_WhenTheHeightBeforeNeverLeftTheEntryHeight_Fails()
    {
        // A re-entry that dropped the last flight comes back at the entry height, so a height that never left it proves nothing.
        Assert.IsFalse(SeatTrialRules.HeightKept(22f, 22f, 22f), "a lift that did nothing");
        Assert.IsFalse(SeatTrialRules.HeightKept(23.9f, 23.9f, 22f), "inside the tolerance of the entry height (2.2 m at 22 m)");
        Assert.IsFalse(SeatTrialRules.HeightKept(24.2f, 24.2f, 22f), "the tolerance is the larger of one metre and a tenth of the height before");
        Assert.IsTrue(SeatTrialRules.HeightKept(24.5f, 24.5f, 22f), "just outside it");
    }

    [TestMethod]
    public void NearHero_MeansWithinTenMetres()
    {
        Assert.IsTrue(SeatTrialRules.NearHero(10f));
        Assert.IsFalse(SeatTrialRules.NearHero(10.1f));
        Assert.IsFalse(SeatTrialRules.NearHero(float.NaN));
    }

    [TestMethod]
    public void TheTimingConstants_MatchTheContract()
    {
        Assert.AreEqual(5f, SeatTrialRules.DeploymentFinishSeconds, "FinishDeployment five seconds in");
        Assert.AreEqual(3f, SeatTrialRules.EnterWindowSeconds, "owned frames are judged 3 s after the press");
        Assert.AreEqual(2f, SeatTrialRules.FlySeconds, "forward input for 2 s");
    }

    // ---- The line ----

    [TestMethod]
    public void Line_HasTheContractsShape()
    {
        Assert.AreEqual("[SeatTrial] step=fly result=PASS moved=58.2 expected=60.0", SeatTrialRules.Line("fly", true, "moved=58.2 expected=60.0"));
        Assert.AreEqual("[SeatTrial] step=enter result=FAIL reason=timeout", SeatTrialRules.Line("enter", false, "reason=timeout"));
        Assert.AreEqual("[SeatTrial] step=enter result=PASS", SeatTrialRules.Line("enter", true, ""));
    }

    [TestMethod]
    public void Num_UsesOneDecimalAndTheInvariantCulture_AndNamesANonFiniteNumber()
    {
        var keep = System.Threading.Thread.CurrentThread.CurrentCulture;
        try
        {
            System.Threading.Thread.CurrentThread.CurrentCulture = new System.Globalization.CultureInfo("de-DE");
            Assert.AreEqual("58.2", SeatTrialRules.Num(58.24f));
        }
        finally
        {
            System.Threading.Thread.CurrentThread.CurrentCulture = keep;
        }

        Assert.AreEqual("nan", SeatTrialRules.Num(float.NaN));
        Assert.AreEqual("nan", SeatTrialRules.Num(float.PositiveInfinity));
    }
}
