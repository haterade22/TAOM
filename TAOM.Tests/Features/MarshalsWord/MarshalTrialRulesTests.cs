using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.MarshalsWord.Domain;

namespace TAOM.Tests.Features.MarshalsWord;

/// <summary>The numbers the Marshal's Word field trial judges with (build-spec 11), and the two drag lines it plans with the solver itself.</summary>
[TestClass]
public class MarshalTrialRulesTests
{
    private const float Bipedal = 0.35f;

    // Two companies of a line, side by side facing +Y (the solver's frame: forward (0, 1), right (1, 0)).
    private static ArrayCompany Company(int index, float x, float y, int units = 40, float width = 20f) =>
        new(index, x, y, width, 4f, 60f, 3f, units, false, 0f, 1f);

    private static ArrayCompany[] SideBySide(float gap = 40f) => new[] { Company(0, 0f, 0f), Company(1, gap, 0f) };

    private static ArrayCompany[] Staggered() => new[] { Company(0, 0f, 0f), Company(1, 40f, 25f) };

    // ---- the verdict numbers ----

    [TestMethod]
    public void ReachedInOrder_TheSecondTwoSecondsAfterTheFirst_Passes_AndOneSecondDoesNot()
    {
        Assert.IsTrue(MarshalTrialRules.ReachedInOrder(5f, 7f));
        Assert.IsFalse(MarshalTrialRules.ReachedInOrder(5f, 6.9f));
        Assert.IsFalse(MarshalTrialRules.ReachedInOrder(7f, 5f), "the wrong order");
    }

    [TestMethod]
    public void ReachedInOrder_ANumberThatIsNotReal_NeverPasses()
    {
        Assert.IsFalse(MarshalTrialRules.ReachedInOrder(float.NaN, 20f), "a first point never reached");
        Assert.IsFalse(MarshalTrialRules.ReachedInOrder(5f, float.NaN));
        Assert.IsFalse(MarshalTrialRules.ReachedInOrder(5f, float.PositiveInfinity));
    }

    [TestMethod]
    public void OffsetKept_ExactlyThreeMetresOfShift_Passes_AndOverThreeDoesNot()
    {
        Assert.IsTrue(MarshalTrialRules.OffsetKept(40f, 25f, 40f, 28f));
        Assert.IsFalse(MarshalTrialRules.OffsetKept(40f, 25f, 40f, 28.1f));
        Assert.IsTrue(MarshalTrialRules.OffsetKept(40f, 25f, 40f, 25f));
    }

    [TestMethod]
    public void OffsetKept_TheShiftIsTheLengthOfTheChange_NotAnAxis()
    {
        Assert.IsFalse(MarshalTrialRules.OffsetKept(0f, 0f, 2.5f, 2.5f), "3.5 m diagonally is over the tolerance though neither axis is");
        Assert.AreEqual(5f, MarshalTrialRules.OffsetShift(0f, 0f, 3f, 4f), 1e-5f);
    }

    [TestMethod]
    public void OffsetKept_ANumberThatIsNotReal_NeverPasses()
    {
        Assert.IsFalse(MarshalTrialRules.OffsetKept(float.NaN, 0f, 0f, 0f));
        Assert.IsFalse(MarshalTrialRules.OffsetKept(0f, 0f, 0f, float.PositiveInfinity));
        Assert.IsTrue(float.IsNaN(MarshalTrialRules.OffsetShift(0f, float.NaN, 0f, 0f)));
    }

    [TestMethod]
    public void StaggerProves_NeedsAtLeastFifteenMetres_AndARealNumber()
    {
        Assert.IsTrue(MarshalTrialRules.StaggerProves(15f));
        Assert.IsFalse(MarshalTrialRules.StaggerProves(14.9f));
        Assert.IsFalse(MarshalTrialRules.StaggerProves(float.NaN));
    }

    // ---- the shift of the array step ----

    [TestMethod]
    public void ShiftAlong_MovesBothEndsByTheMetresAlongTheLine_AndNamesTheShift()
    {
        var shifted = MarshalTrialRules.ShiftAlong((0f, 0f, 30f, 40f), 20f)!.Value;

        Assert.AreEqual(12f, shifted.ShiftX, 1e-4f);
        Assert.AreEqual(16f, shifted.ShiftY, 1e-4f);
        Assert.AreEqual(12f, shifted.BeginX, 1e-4f);
        Assert.AreEqual(16f, shifted.BeginY, 1e-4f);
        Assert.AreEqual(42f, shifted.EndX, 1e-4f);
        Assert.AreEqual(56f, shifted.EndY, 1e-4f);
    }

    [TestMethod]
    public void ShiftAlong_ALineWithNoLengthOrANumberThatIsNotReal_IsNull()
    {
        Assert.IsNull(MarshalTrialRules.ShiftAlong((1f, 1f, 1f, 1f), 20f));
        Assert.IsNull(MarshalTrialRules.ShiftAlong((0f, 0f, float.NaN, 5f), 20f));
        Assert.IsNull(MarshalTrialRules.ShiftAlong((0f, 0f, 5f, 5f), float.PositiveInfinity));
    }

    [TestMethod]
    public void ShiftAlong_ThroughTheRealSolver_MovesEveryCompanyByExactlyTheShift_AndKeepsTheirOffsets()
    {
        foreach (var group in new[] { SideBySide(), Staggered() })
        {
            var shifted = MarshalTrialRules.ShiftAlong(MarshalTrialRules.FrontLine(group)!.Value, MarshalTrialRules.ShiftMetres)!.Value;

            var solved = MarshalArraySolver.Solve(group, shifted.BeginX, shifted.BeginY, shifted.EndX, shifted.EndY, keepWidths: false, Bipedal);

            Assert.IsTrue(solved.Ok);
            for (var i = 0; i < group.Length; i++)
            {
                var miss = MarshalTrialRules.ShiftMiss(group[i].X, group[i].Y, shifted.ShiftX, shifted.ShiftY, solved.Placements[i].X, solved.Placements[i].Y);
                Assert.IsTrue(MarshalTrialRules.ShiftFollowed(miss), "company " + i + " missed by " + miss);
            }
        }
    }

    [TestMethod]
    public void ShiftProves_NeedsAtLeastFifteenMetres_OfRealNumbers()
    {
        Assert.IsTrue(MarshalTrialRules.ShiftProves(0f, -15f));
        Assert.IsTrue(MarshalTrialRules.ShiftProves(12f, 9f));
        Assert.IsFalse(MarshalTrialRules.ShiftProves(10f, 10f), "14.1 m");
        Assert.IsFalse(MarshalTrialRules.ShiftProves(float.NaN, 20f));
    }

    [TestMethod]
    public void ShiftFollowed_ExactlyThreeMetresOff_Passes_AndOverThreeDoesNot_AndNaNNever()
    {
        Assert.IsTrue(MarshalTrialRules.ShiftFollowed(MarshalTrialRules.ShiftMiss(0f, 0f, 0f, -20f, 3f, -20f)));
        Assert.IsFalse(MarshalTrialRules.ShiftFollowed(MarshalTrialRules.ShiftMiss(0f, 0f, 0f, -20f, 3.1f, -20f)));
        Assert.IsFalse(MarshalTrialRules.ShiftFollowed(MarshalTrialRules.ShiftMiss(0f, 0f, 0f, -20f, 0f, 0f)), "a company that never moved misses by the whole shift");
        Assert.IsFalse(MarshalTrialRules.ShiftFollowed(float.NaN));
    }

    // ---- the front line ----

    [TestMethod]
    public void FrontLine_OfTwoCompaniesSideBySide_RunsFlankToFlankThroughTheirFront()
    {
        var front = MarshalTrialRules.FrontLine(SideBySide())!.Value;

        Assert.AreEqual(-10f, front.BeginX, 1e-3f);
        Assert.AreEqual(50f, front.EndX, 1e-3f);
        Assert.AreEqual(0f, front.BeginY, 1e-3f);
        Assert.AreEqual(0f, front.EndY, 1e-3f);
    }

    [TestMethod]
    public void FrontLine_OfStaggeredCompanies_LiesOnTheForemostOne()
    {
        var front = MarshalTrialRules.FrontLine(Staggered())!.Value;

        Assert.AreEqual(25f, front.BeginY, 1e-3f);
        Assert.AreEqual(25f, front.EndY, 1e-3f);
        Assert.IsTrue(front.EndX > front.BeginX, "the line runs to the group's right");
    }

    [TestMethod]
    public void FrontLine_DraggedThroughTheRealSolver_ChangesNoCompanysPlace()
    {
        foreach (var group in new[] { SideBySide(), Staggered() })
        {
            var front = MarshalTrialRules.FrontLine(group)!.Value;

            var solved = MarshalArraySolver.Solve(group, front.BeginX, front.BeginY, front.EndX, front.EndY, keepWidths: false, Bipedal);

            Assert.IsTrue(solved.Ok);
            for (var i = 0; i < group.Length; i++)
            {
                Assert.AreEqual(group[i].X, solved.Placements[i].X, 0.05f, "company " + i + " in x");
                Assert.AreEqual(group[i].Y, solved.Placements[i].Y, 0.05f, "company " + i + " in y");
            }
        }
    }

    [TestMethod]
    public void FrontLine_NoCompanies_ANonFiniteInputOrAZeroWidthGroup_IsNull()
    {
        Assert.IsNull(MarshalTrialRules.FrontLine(Array.Empty<ArrayCompany>()));
        Assert.IsNull(MarshalTrialRules.FrontLine(new[] { Company(0, float.NaN, 0f) }));
        Assert.IsNull(MarshalTrialRules.FrontLine(new[] { Company(0, 0f, 0f, width: 0f) }));
    }

    // ---- the arrival plan ----

    [TestMethod]
    public void PlanArrival_ForTwoCompaniesSideBySide_FindsAQuarterTurnAboutTwoAndAHalfToOne_WithTheFarWalkOverTheFloor()
    {
        var plan = MarshalTrialRules.PlanArrival(SideBySide(60f), speed: 4f, Bipedal);

        Assert.IsNotNull(plan);
        var ratio = plan!.Value.Far / plan.Value.Near;
        Assert.IsTrue(ratio >= MarshalTrialRules.RatioLow && ratio <= MarshalTrialRules.RatioHigh, "ratio " + ratio);
        Assert.IsTrue((plan.Value.Far - MarshalTuning.ArrivalSlack) / 4f >= MarshalTrialRules.FarSeconds);
    }

    [TestMethod]
    public void PlanArrival_ThePlannedLine_ThroughTheRealSolver_GivesTheWalksItNames()
    {
        var group = SideBySide(60f);
        var plan = MarshalTrialRules.PlanArrival(group, 4f, Bipedal)!.Value;

        var solved = MarshalArraySolver.Solve(group, plan.BeginX, plan.BeginY, plan.EndX, plan.EndY, keepWidths: true, Bipedal);

        var walks = group.Select((c, i) => Math.Sqrt(Math.Pow(solved.Placements[i].X - c.X, 2) + Math.Pow(solved.Placements[i].Y - c.Y, 2))).ToArray();
        Assert.AreEqual(plan.Near, walks.Min(), 0.01);
        Assert.AreEqual(plan.Far, walks.Max(), 0.01);
        Assert.IsFalse(solved.IsLineShort, "a drag, not a click");
    }

    [TestMethod]
    public void PlanArrival_ASlowerCompany_NeedsALongerFarWalk()
    {
        var fast = MarshalTrialRules.PlanArrival(SideBySide(60f), 3f, Bipedal);
        var slow = MarshalTrialRules.PlanArrival(SideBySide(60f), 6f, Bipedal);

        Assert.IsNotNull(fast);
        Assert.IsTrue(slow == null || slow.Value.Far > fast!.Value.Far, "6 m/s needs 17 s of walk at 6 m/s, which is more metres than at 3 m/s");
    }

    [TestMethod]
    public void PlanArrival_NeverSendsTheFarCompanyOnAWalkLongerThanThirtySeconds_WhateverTheGroupLooksLike()
    {
        // A long walk takes the group to the enemy before the march ends, and the battle runs out under the step (a run's far walk was 142.6 m).
        var longest = 0f;
        foreach (var gap in new[] { 25f, 40f, 60f, 80f, 100f })
        {
            foreach (var stagger in new[] { 0f, 15f, 25f, 40f })
            {
                foreach (var speed in new[] { 3f, 3.5f, 4f, 5f })
                {
                    var group = new[] { Company(0, 0f, 0f), Company(1, gap, stagger) };
                    var plan = MarshalTrialRules.PlanArrival(group, speed, Bipedal);
                    if (plan == null)
                        continue;
                    var seconds = (plan.Value.Far - MarshalTuning.ArrivalSlack) / speed;
                    longest = Math.Max(longest, seconds);
                    Assert.IsTrue(seconds <= MarshalTrialRules.FarSecondsMax, "gap " + gap + " stagger " + stagger + " speed " + speed + ": " + MarshalTrialRules.Describe(plan.Value));
                }
            }
        }

        Assert.IsTrue(longest >= MarshalTrialRules.FarSeconds, "the sweep found plans: " + longest);
    }

    [TestMethod]
    public void PlanArrival_ForTheColumnTheStepStages_FindsAPlanAtEverySpeedACompanyWalksAt_AndWithUnevenWidths()
    {
        foreach (var speed in new[] { 3f, 3.5f, 4f, 4.5f })
        {
            foreach (var secondWidth in new[] { 15f, 20f, 30f })
            {
                var group = new[] { Company(0, 0f, 0f), Company(1, MarshalTrialRules.ColumnSideMetres, -MarshalTrialRules.ColumnBehindMetres, width: secondWidth) };

                var plan = MarshalTrialRules.PlanArrival(group, speed, Bipedal);

                Assert.IsNotNull(plan, "speed " + speed + ", second width " + secondWidth);
            }
        }
    }

    [TestMethod]
    public void PlanArrival_ForCompaniesCloserThanTheColumn_HasNoPlanInsideTheCap_WhichIsWhyTheStepStagesThem()
    {
        foreach (var behind in new[] { 0f, 10f, 25f })
        {
            var group = new[] { Company(0, 0f, 0f), Company(1, 0f, -behind) };

            Assert.IsNull(MarshalTrialRules.PlanArrival(group, 3.5f, Bipedal), "second company " + behind + " m behind");
        }
    }

    [TestMethod]
    public void PlanArrival_WithNoSpeedOneCompanyOrANonFiniteInput_IsNull()
    {
        Assert.IsNull(MarshalTrialRules.PlanArrival(SideBySide(), 0f, Bipedal));
        Assert.IsNull(MarshalTrialRules.PlanArrival(SideBySide(), float.NaN, Bipedal));
        Assert.IsNull(MarshalTrialRules.PlanArrival(new[] { Company(0, 0f, 0f) }, 4f, Bipedal));
        Assert.IsNull(MarshalTrialRules.PlanArrival(new[] { Company(0, float.NaN, 0f), Company(1, 40f, 0f) }, 4f, Bipedal));
    }

    [TestMethod]
    public void PlanArrival_TwoCompaniesOnTopOfEachOther_HasNoPlanAtTheRatio()
    {
        var onTop = new[] { Company(0, 0f, 0f), Company(1, 0f, 0f) };

        var plan = MarshalTrialRules.PlanArrival(onTop, 4f, Bipedal);

        Assert.IsTrue(plan == null || plan.Value.Far / Math.Max(plan.Value.Near, 0.001f) >= MarshalTrialRules.RatioLow);
    }

    [TestMethod]
    public void ApproachWalk_AnEnemyWalkingAtMySpeed_MeetsMeHalfWay_SoTheGapAtTheEndIsTheReach()
    {
        Assert.AreEqual(110f, MarshalTrialRules.ApproachWalk(300f, 80f, 4f, 4f), 1e-3f);
        Assert.AreEqual(220f / 2.25f, MarshalTrialRules.ApproachWalk(300f, 80f, 4f, 5f), 1e-3f);
        Assert.AreEqual(220f, MarshalTrialRules.ApproachWalk(300f, 80f, 4f, 0f), 1e-3f, "an enemy that stands still");
    }

    [TestMethod]
    public void ApproachWalk_AGapAlreadyUnderTheReach_IsNoWalk_AndANumberThatIsNotRealIsNaN()
    {
        Assert.AreEqual(0f, MarshalTrialRules.ApproachWalk(60f, 80f, 4f, 4f));
        Assert.IsTrue(float.IsNaN(MarshalTrialRules.ApproachWalk(float.NaN, 80f, 4f, 4f)));
        Assert.IsTrue(float.IsNaN(MarshalTrialRules.ApproachWalk(300f, 80f, 0f, 4f)), "no speed of my own");
        Assert.IsTrue(float.IsNaN(MarshalTrialRules.ApproachWalk(300f, 80f, 4f, -1f)));
    }

    [TestMethod]
    public void Describe_NamesBothWalks_WithOneDecimal_ForAnyCulture()
    {
        var keep = System.Threading.Thread.CurrentThread.CurrentCulture;
        try
        {
            System.Threading.Thread.CurrentThread.CurrentCulture = new System.Globalization.CultureInfo("de-DE");
            Assert.AreEqual("near=40.0 far=100.5", MarshalTrialRules.Describe(new TrialDrag(0f, 0f, 1f, 1f, 40f, 100.46f)));
        }
        finally
        {
            System.Threading.Thread.CurrentThread.CurrentCulture = keep;
        }
    }
}
