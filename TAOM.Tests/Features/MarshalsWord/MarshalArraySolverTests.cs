using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.MarshalsWord.Domain;

namespace TAOM.Tests.Features.MarshalsWord;

/// <summary>Golden tests of the array solver (build-spec 6.2). Facing north is (0,1); the group's right is then (1,0).</summary>
[TestClass]
public class MarshalArraySolverTests
{
    private const float Radius = 0.6f;
    private const float Tolerance = 0.01f;

    private static ArrayCompany Company(int id, float x, float y, float width = 10f, int units = 50, float min = 4f, float max = 40f,
        float depth = 6f, bool column = false, float dirX = 0f, float dirY = 1f) =>
        new(id, x, y, width, min, max, depth, units, column, dirX, dirY);

    private static ArrayResult Drag(IReadOnlyList<ArrayCompany> group, float bx, float by, float ex, float ey, bool keepWidths = false) =>
        MarshalArraySolver.Solve(group, bx, by, ex, ey, keepWidths, Radius);

    private static ArrayPlacement Of(ArrayResult result, int company) => result.Placements.Single(p => p.Company == company);

    private static float Distance(float ax, float ay, float bx, float by) => (float)Math.Sqrt((ax - bx) * (ax - bx) + (ay - by) * (ay - by));

    private static readonly ArrayCompany[] SideBySide =
    {
        Company(1, -6, 0),
        Company(2, 6, 0),
    };

    // ---- the short line test, restated from vanilla (OrderController 1208-1221) ----

    [TestMethod]
    public void IsLineShort_UnderTheBipedalRadius_IsShort()
    {
        Assert.IsTrue(MarshalArraySolver.IsLineShort(0.5f, Radius, sumMinimumWidth: 4f, count: 1));
    }

    [TestMethod]
    public void IsLineShort_UnderTheMinimumWidthsAndGaps_IsShort()
    {
        // 2 companies of minimum width 4 need 4 + 4 + 1.5 = 9.5 m.
        Assert.IsTrue(MarshalArraySolver.IsLineShort(9.4f, Radius, sumMinimumWidth: 8f, count: 2));
        Assert.IsFalse(MarshalArraySolver.IsLineShort(9.6f, Radius, sumMinimumWidth: 8f, count: 2));
    }

    // ---- a click ----

    [TestMethod]
    public void Solve_AClick_KeepsEveryOffsetBetweenTheCompanies()
    {
        var group = new[] { Company(1, -6, 0), Company(2, 6, 0), Company(3, 0, -12) };

        var result = Drag(group, 100, 50, 100, 50);

        Assert.IsTrue(result.Ok);
        Assert.IsTrue(result.IsLineShort);
        var a = Of(result, 1);
        var b = Of(result, 2);
        var c = Of(result, 3);
        Assert.AreEqual(12f, b.X - a.X, Tolerance);
        Assert.AreEqual(0f, b.Y - a.Y, Tolerance);
        Assert.AreEqual(0f, c.X - (a.X + b.X) / 2f, Tolerance, "the rear company stays centred behind the pair");
        Assert.AreEqual(-12f, c.Y - a.Y, Tolerance);
    }

    [TestMethod]
    public void Solve_AClick_PutsTheUnitWeightedCentreOnTheClickedPoint()
    {
        var group = new[] { Company(1, -6, 0, units: 30), Company(2, 6, 0, units: 10) };

        var result = Drag(group, 100, 50, 100, 50);

        var centreX = (Of(result, 1).X * 30f + Of(result, 2).X * 10f) / 40f;
        var centreY = (Of(result, 1).Y * 30f + Of(result, 2).Y * 10f) / 40f;
        Assert.AreEqual(100f, centreX, Tolerance);
        Assert.AreEqual(50f, centreY, Tolerance);
    }

    [TestMethod]
    public void Solve_AClick_TurnsNothingAndKeepsEachCompanysOwnWidthAndDirection()
    {
        var group = new[] { Company(1, -6, 0, width: 9f, dirX: 0.6f, dirY: 0.8f), Company(2, 6, 0, width: 14f) };

        var result = Drag(group, 100, 50, 100, 50);

        Assert.AreEqual(9f, Of(result, 1).Width, 1e-5f);
        Assert.AreEqual(14f, Of(result, 2).Width, 1e-5f);
        Assert.AreEqual(0.6f, Of(result, 1).DirX, 1e-5f);
        Assert.AreEqual(0.8f, Of(result, 1).DirY, 1e-5f);
        Assert.AreEqual(1f, Of(result, 2).DirY, 1e-5f);
    }

    // ---- a drag ----

    [TestMethod]
    public void Solve_ALineFromFlankToFlankAlongTheCurrentFront_ChangesNothing()
    {
        // The group spans -11..11 across and lies on y = 0, so a line from (-11,0) to (11,0) is its own front.
        var result = Drag(SideBySide, -11, 0, 11, 0);

        Assert.IsTrue(result.Ok);
        Assert.IsFalse(result.IsLineShort);
        foreach (var company in SideBySide)
        {
            var placed = Of(result, company.Company);
            Assert.AreEqual(company.X, placed.X, Tolerance);
            Assert.AreEqual(company.Y, placed.Y, Tolerance);
            Assert.AreEqual(company.Width, placed.Width, Tolerance);
            Assert.AreEqual(0f, placed.DirX, 1e-4f);
            Assert.AreEqual(1f, placed.DirY, 1e-4f);
        }
    }

    [TestMethod]
    public void Solve_ADrag_PutsTheFrontOfTheGroupOnTheLine()
    {
        var group = new[] { Company(1, -6, 0, units: 60), Company(2, 6, 0), Company(3, 0, -12) };

        var result = Drag(group, -30, 100, 30, 100);

        // The line runs along +x at y = 100, so the new forward is +y and a company at the front lies on y = 100.
        Assert.AreEqual(100f, Of(result, 1).Y, Tolerance);
        Assert.AreEqual(100f, Of(result, 2).Y, Tolerance);
        Assert.AreEqual(88f, Of(result, 3).Y, Tolerance, "the rear company keeps its 12 m depth behind the front");
    }

    [TestMethod]
    public void Solve_ADrag_CentresTheGroupsWidthOnTheMiddleOfTheLine()
    {
        var result = Drag(SideBySide, 40, 100, 80, 100);

        var left = Of(result, 1);
        var right = Of(result, 2);
        var extentLow = left.X - left.Width / 2f;
        var extentHigh = right.X + right.Width / 2f;
        Assert.AreEqual(60f, (extentLow + extentHigh) / 2f, Tolerance);
    }

    [TestMethod]
    public void Solve_ALongerLine_StretchesTheWidthsAndTheSpacingTogether()
    {
        // Span 22 m on a 44 m line: stretch 2, so the widths double and the centres move twice as far apart.
        var result = Drag(SideBySide, -22, 0, 22, 0);

        Assert.AreEqual(20f, Of(result, 1).Width, Tolerance);
        Assert.AreEqual(20f, Of(result, 2).Width, Tolerance);
        Assert.AreEqual(24f, Of(result, 2).X - Of(result, 1).X, Tolerance);
    }

    [TestMethod]
    public void Solve_KeepTheWidths_OnlyTurnsTheGroup()
    {
        var result = Drag(SideBySide, -22, 0, 22, 0, keepWidths: true);

        Assert.AreEqual(10f, Of(result, 1).Width, Tolerance);
        Assert.AreEqual(12f, Of(result, 2).X - Of(result, 1).X, Tolerance, "the spacing is not stretched either");
    }

    [TestMethod]
    public void Solve_ADragAcrossTheGroup_TurnsItNinetyDegreesAndMapsTheFrontToTheNewFront()
    {
        // The group faces +y. A line drawn along +y at x = 50 turns the front to face -x (forward = (-right.y, right.x) = (-1, 0)).
        var group = new[] { Company(1, -6, 0, units: 60), Company(2, 6, 0), Company(3, 0, -12) };

        var result = Drag(group, 50, -11, 50, 11, keepWidths: true);

        var a = Of(result, 1);
        Assert.AreEqual(-1f, a.DirX, 1e-4f);
        Assert.AreEqual(0f, a.DirY, 1e-4f);
        Assert.AreEqual(50f, a.X, Tolerance, "the front company lies on the line");
        Assert.AreEqual(50f + 12f, Of(result, 3).X, Tolerance, "the rear company is 12 m behind, now toward +x");
        Assert.IsTrue(Of(result, 1).Y < Of(result, 2).Y, "the left-to-right order of the front is kept along the new right (+y)");
    }

    [TestMethod]
    public void Solve_AStretch_KeepsARearCompanyBehind()
    {
        var group = new[] { Company(1, -6, 0), Company(2, 6, 0), Company(3, 0, -15) };

        var narrow = Drag(group, -11, 0, 11, 0);
        var wide = Drag(group, -33, 0, 33, 0);

        Assert.AreEqual(-15f, Of(narrow, 3).Y - Of(narrow, 1).Y, Tolerance);
        Assert.AreEqual(-15f, Of(wide, 3).Y - Of(wide, 1).Y, Tolerance);
    }

    [TestMethod]
    public void Solve_ACompanyClampedAtItsMaxWidth_DoesNotShiftTheRelativePlaceOfTheOthers()
    {
        var free = new[] { Company(1, -20, 0), Company(2, 0, 0, max: 10f), Company(3, 20, 0) };
        var allFree = new[] { Company(1, -20, 0), Company(2, 0, 0), Company(3, 20, 0) };

        var clamped = Drag(free, -60, 0, 60, 0);
        var open = Drag(allFree, -60, 0, 60, 0);

        Assert.AreEqual(10f, Of(clamped, 2).Width, Tolerance, "the middle company stops at its maximum");
        Assert.AreEqual(Of(open, 3).X - Of(open, 1).X, Of(clamped, 3).X - Of(clamped, 1).X, Tolerance);
    }

    [TestMethod]
    public void Solve_ACompanyAtItsMinimumWidth_NeverGoesBelowIt()
    {
        var result = Drag(WideAndNarrow, -7, 0, 7, 0);

        Assert.IsTrue(Of(result, 2).Width >= 4f - Tolerance, "the narrow company stops at its minimum");
    }

    // A wide company beside a narrow one that cannot shrink: compressing the line pulls their centres together faster than the narrow
    // company's width falls, so they overlap by about 0.45 m before the shoulder pass.
    private static readonly ArrayCompany[] WideAndNarrow =
    {
        Company(1, -16, 0, width: 30f, min: 4f, max: 40f),
        Company(2, 3, 0, width: 4f, min: 4f, max: 4f),
    };

    [TestMethod]
    public void Solve_ACompressedDragOfSideBySideCompanies_NeverLeavesThemOverlapping()
    {
        var result = Drag(WideAndNarrow, -7, 0, 7, 0);

        var left = Of(result, 1);
        var right = Of(result, 2);
        var gap = (right.X - right.Width / 2f) - (left.X + left.Width / 2f);
        Assert.IsTrue(gap >= MarshalTuning.GapInLine - Tolerance, "gap was " + gap);
    }

    [TestMethod]
    public void Solve_ACompressedDrag_KeepsTheGroupCentredOnTheLine()
    {
        var result = Drag(WideAndNarrow, 93, 20, 107, 20);

        var left = Of(result, 1);
        var right = Of(result, 2);
        Assert.AreEqual(100f, ((left.X - left.Width / 2f) + (right.X + right.Width / 2f)) / 2f, Tolerance);
    }

    [TestMethod]
    public void Solve_ACompanyFarBehindTheOthers_IsNotPushedByTheShoulderPass()
    {
        // Company 3 stands 30 m behind, so its depth band never meets the front pair's, although its width would overlap company 1's.
        var group = new[] { WideAndNarrow[0], WideAndNarrow[1], Company(3, 0, -30, width: 10f, min: 4f, max: 40f) };

        var result = Drag(group, -8, 0, 8, 0);

        // Offsets from the unit-weighted centre are -11.667 and +4.333; the stretch is 16 / 36. (A 14 m line would be short: 3 x 4 m + 2 gaps.)
        Assert.AreEqual(16f * 16f / 36f, Of(result, 3).X - Of(result, 1).X, Tolerance);
    }

    [TestMethod]
    public void Solve_AColumn_KeepsItsWidthOnADrag()
    {
        var group = new[] { Company(1, -6, 0), Company(2, 6, 0, width: 6f, column: true, min: 6f, max: 6f) };

        var result = Drag(group, -44, 0, 44, 0);

        Assert.AreEqual(6f, Of(result, 2).Width, 1e-5f);
        Assert.IsTrue(Of(result, 1).Width > 10f);
    }

    [TestMethod]
    public void Solve_TheLargestCompany_SetsTheForward()
    {
        // Company 2 is larger and faces +x, so the group's forward is +x and its right is (0,-1). A drag along that right keeps the turn at zero.
        var group = new[] { Company(1, 0, 6, units: 10), Company(2, 0, -6, units: 90, dirX: 1f, dirY: 0f) };

        var result = Drag(group, 0, 11, 0, -11);

        Assert.AreEqual(1f, Of(result, 2).DirX, 1e-4f);
        Assert.AreEqual(0f, Of(result, 2).DirY, 1e-4f);
        Assert.AreEqual(6f, Of(result, 1).Y, Tolerance);
        Assert.AreEqual(-6f, Of(result, 2).Y, Tolerance);
    }

    // ---- inputs that would break arithmetic ----

    [TestMethod]
    public void Solve_OneCompany_GivesFiniteOutputsOnAClickAndADrag()
    {
        var one = new[] { Company(1, 3, 4) };

        foreach (var result in new[] { Drag(one, 10, 10, 10, 10), Drag(one, 0, 0, 30, 0) })
        {
            Assert.IsTrue(result.Ok);
            AssertFinite(result);
        }
    }

    [TestMethod]
    public void Solve_ZeroUnitsZeroWidthsAndAZeroLine_GivesFiniteOutputs()
    {
        var group = new[] { Company(1, 0, 0, width: 0f, units: 0, min: 0f, max: 0f, depth: 0f, dirX: 0f, dirY: 0f), Company(2, 0, 0, width: 0f, units: 0, min: 0f, max: 0f, depth: 0f, dirX: 0f, dirY: 0f) };

        var click = Drag(group, 0, 0, 0, 0);
        var drag = Drag(group, 0, 0, 40, 0);

        Assert.IsTrue(click.Ok);
        Assert.IsTrue(drag.Ok);
        AssertFinite(click);
        AssertFinite(drag);
    }

    [TestMethod]
    public void Solve_HugeCoordinates_GiveFiniteOutputs()
    {
        var group = new[] { Company(1, -1e30f, 1e30f), Company(2, 1e30f, 1e30f) };

        var result = Drag(group, -1e30f, 0, 1e30f, 0);

        Assert.IsTrue(result.Ok);
        AssertFinite(result);
    }

    [DataTestMethod]
    [DataRow(float.NaN, 0f)]
    [DataRow(0f, float.PositiveInfinity)]
    public void Solve_ANonFiniteInput_FailsSoVanillaRuns(float x, float y)
    {
        var group = new[] { Company(1, x, y), Company(2, 6, 0) };

        var result = Drag(group, -11, 0, 11, 0);

        Assert.IsFalse(result.Ok);
        Assert.AreEqual(0, result.Placements.Count);
    }

    [TestMethod]
    public void Solve_ANonFiniteLine_FailsSoVanillaRuns()
    {
        Assert.IsFalse(Drag(SideBySide, float.NaN, 0, 11, 0).Ok);
    }

    [TestMethod]
    public void Solve_NoCompanies_Fails()
    {
        Assert.IsFalse(Drag(Array.Empty<ArrayCompany>(), 0, 0, 10, 0).Ok);
    }

    private static void AssertFinite(ArrayResult result)
    {
        foreach (var p in result.Placements)
            foreach (var value in new[] { p.X, p.Y, p.Width, p.DirX, p.DirY })
                Assert.IsTrue(!float.IsNaN(value) && !float.IsInfinity(value), "company " + p.Company + " has a non-finite output");
    }
}
