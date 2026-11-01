using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.FordsOfWar;
using TAOM.Features.FordsOfWar.Domain;

namespace TAOM.Tests.Features.FordsOfWar;

[TestClass]
public class FordsLayoutPlannerTests
{
    private static List<Vec3f> Box(float width, float height) => new()
    {
        new Vec3f(0f, -height / 2f, 0f), new Vec3f(width, -height / 2f, 0f), new Vec3f(width, height / 2f, 0f), new Vec3f(0f, height / 2f, 0f),
    };

    /// <summary>The director's candidate builder, in miniature: bases from the polygon on the chosen axis, lanes spread by the factor.</summary>
    private static Func<float, FordsBaseAxis, MapLayout?> BuilderFor(List<Vec3f> polygon) => (factor, axis) =>
        FordsBaseFinder.TryFind(polygon, 0.1f, axis == FordsBaseAxis.Short, out var west, out var east)
            ? LaneLayoutGenerator.Generate("test_scene", west, east, new LayoutOptions { LaneSpread = 45f * factor })
            : null;

    private static Func<MapLayout, FordsLayoutFit> FitterOver(Func<Vec3f, bool> walkable) =>
        layout => FordsLayoutFitter.Fit(layout, p => p, walkable);

    private static int ItemCount(MapLayout layout) =>
        layout.Lanes.Sum(l => l.Path.Count) + layout.Towers.Count + layout.CapturePoints.Count + layout.Camps.Count;

    private static MapLayout Plain() =>
        LaneLayoutGenerator.Generate("test_scene", new Vec3f(0f, 0f, 0f), new Vec3f(1000f, 0f, 0f), new LayoutOptions());

    [TestMethod]
    public void Plan_NearSquareBoundaryWalkableOnlyInAHorizontalStrip_PicksTheShortAxis()
    {
        var polygon = Box(1000f, 1100f);
        Assert.IsTrue(FordsBaseFinder.AspectRatio(polygon) < 1.25f);

        var plan = FordsLayoutPlanner.Plan(BuilderFor(polygon), FitterOver(p => Math.Abs(p.Y) <= 75f), FordsBaseFinder.AspectRatio(polygon));

        Assert.IsTrue(plan.Passed, string.Join("\n", plan.Attempts.Select(a => a.Summary)));
        Assert.AreEqual(FordsBaseAxis.Short, plan.Chosen!.Axis);
        var path = plan.Chosen.Fit.Layout.Lanes.Single(l => l.Index == 1).Path;
        Assert.AreEqual(0f, path[0].Y, 5f);
        Assert.AreEqual(0f, path[path.Count - 1].Y, 5f);
        Assert.IsTrue(path[path.Count - 1].X - path[0].X > 700f);
        Assert.AreEqual(4, plan.Attempts.Count, "three spreads on the long axis, then the short axis at full spread");
    }

    [TestMethod]
    public void Plan_NarrowWalkableBand_FallsBackToANarrowerSpread()
    {
        var polygon = Box(1000f, 300f);

        var plan = FordsLayoutPlanner.Plan(BuilderFor(polygon), FitterOver(p => Math.Abs(p.Y) <= 25f), FordsBaseFinder.AspectRatio(polygon));

        Assert.IsTrue(plan.Passed, string.Join("\n", plan.Attempts.Select(a => a.Summary)));
        Assert.AreEqual(0.7f, plan.Chosen!.SpreadFactor, 1e-4f);
        Assert.AreEqual(FordsBaseAxis.Long, plan.Chosen.Axis);
        Assert.AreEqual(2, plan.Attempts.Count);
        Assert.IsFalse(plan.Attempts[0].Passed);
        Assert.IsTrue(plan.Attempts[0].Fit.Dropped > 50, "the full spread leaves most of the side lanes outside the band");
    }

    [TestMethod]
    public void Plan_NothingPasses_ReturnsTheBestAttemptAsFitted_AndTheSummaryNamesEveryAttempt()
    {
        var polygon = Box(1000f, 300f);

        var plan = FordsLayoutPlanner.Plan(BuilderFor(polygon), FitterOver(_ => false), FordsBaseFinder.AspectRatio(polygon));

        Assert.IsFalse(plan.Passed);
        Assert.AreEqual(3, plan.Attempts.Count, "a long thin boundary has no second axis to try");
        Assert.IsTrue(plan.Chosen!.Fit.Rejected, "nothing walkable: the fitter itself rejects every candidate");
        StringAssert.Contains(plan.Summary, "no layout passed");
        StringAssert.Contains(plan.Summary, "spread x1.00");
        StringAssert.Contains(plan.Summary, "spread x0.50");
        Assert.IsTrue(plan.Attempts.All(a => !a.Passed));
    }

    [TestMethod]
    public void Plan_NearSquareBoundaryNothingPasses_TriesBothAxesAtEverySpread()
    {
        var polygon = Box(1000f, 1100f);

        var plan = FordsLayoutPlanner.Plan(BuilderFor(polygon), FitterOver(_ => false), FordsBaseFinder.AspectRatio(polygon));

        Assert.AreEqual(6, plan.Attempts.Count);
        Assert.AreEqual(3, plan.Attempts.Count(a => a.Axis == FordsBaseAxis.Short));
    }

    [TestMethod]
    public void Plan_FirstCandidatePasses_StopsThere()
    {
        var polygon = Box(1000f, 300f);

        var plan = FordsLayoutPlanner.Plan(BuilderFor(polygon), FitterOver(_ => true), FordsBaseFinder.AspectRatio(polygon));

        Assert.IsTrue(plan.Passed);
        Assert.AreEqual(1, plan.Attempts.Count);
        Assert.AreEqual(1f, plan.Chosen!.SpreadFactor);
        Assert.IsFalse(plan.Chosen.Fit.Rejected);
    }

    [TestMethod]
    public void Plan_BuilderGivesNothing_ReturnsNoChoice()
    {
        var plan = FordsLayoutPlanner.Plan((_, _) => null, FitterOver(_ => true), 2f);

        Assert.IsFalse(plan.Passed);
        Assert.IsNull(plan.Chosen);
        Assert.AreEqual(0, plan.Attempts.Count);
    }

    [TestMethod]
    public void Plan_BuilderGivesNothingForTheFirstSpread_SkipsToTheNext()
    {
        var plan = FordsLayoutPlanner.Plan((factor, _) => factor == 1f ? null : Plain(), FitterOver(_ => true), 2f);

        Assert.IsTrue(plan.Passed);
        Assert.AreEqual(0.7f, plan.Chosen!.SpreadFactor, 1e-4f);
    }

    [TestMethod]
    public void Plan_TooManyDroppedPoints_FailsButTheFitStaysPlayable()
    {
        var layout = Plain();
        var many = ItemCount(layout) / 5;

        var plan = FordsLayoutPlanner.Plan((_, _) => layout, l => new FordsLayoutFit(l, 0, many, 0), 2f);

        Assert.IsFalse(plan.Passed);
        Assert.IsFalse(plan.Chosen!.Fit.Rejected, "only the fitter rejects (a lane lost every tower); missed limits are a warning");
        StringAssert.Contains(plan.Summary, "no layout passed");
    }

    [TestMethod]
    public void Plan_AFewDroppedPoints_Passes()
    {
        var layout = Plain();

        var plan = FordsLayoutPlanner.Plan((_, _) => layout, l => new FordsLayoutFit(l, 0, 2, 0), 2f);

        Assert.IsTrue(plan.Passed);
    }

    private static MapLayout WithCamps(int count) => Plain() with
    {
        Camps = Enumerable.Range(0, count).Select(i => new CampSlot("camp_" + i, new Vec3f(100f + i, 40f, 0f), "", 0, 0)).ToList(),
    };

    [TestMethod]
    public void Plan_EveryCampDropped_StillPassesBecauseCampsAreNotCounted()
    {
        var layout = WithCamps(40);

        var plan = FordsLayoutPlanner.Plan((_, _) => layout, l => new FordsLayoutFit(l with { Camps = new List<CampSlot>() }, 0, 40, 0), 2f);

        Assert.IsTrue(plan.Passed, plan.Summary);
        StringAssert.Contains(plan.Summary, "dropped 0/");
        StringAssert.Contains(plan.Summary, "camps dropped 40/40");
    }

    [TestMethod]
    public void Plan_DroppedCampsAndTooManyOtherPoints_Fails()
    {
        var layout = WithCamps(40);
        var others = ItemCount(layout) - 40;

        var plan = FordsLayoutPlanner.Plan((_, _) => layout,
            l => new FordsLayoutFit(l with { Camps = new List<CampSlot>() }, 0, 40 + others / 5, 0), 2f);

        Assert.IsFalse(plan.Passed);
        StringAssert.Contains(plan.Summary, "dropped " + others / 5 + "/" + others);
    }

    [TestMethod]
    public void Plan_ATowerMovedMoreThanThirtyMetres_Fails()
    {
        var layout = Plain();
        FordsLayoutFit Fit(MapLayout l, float distance) => new FordsLayoutFit(l, 1, 0, 0)
        {
            MovedTowers = new[] { new FordsTowerMove("tower_west_0_0", distance) },
        };

        Assert.IsTrue(FordsLayoutPlanner.Plan((_, _) => layout, l => Fit(l, 30f), 2f).Passed);
        Assert.IsFalse(FordsLayoutPlanner.Plan((_, _) => layout, l => Fit(l, 30.5f), 2f).Passed);
    }

    [TestMethod]
    public void Plan_TooManyBadSegments_Fails()
    {
        var layout = Plain();
        FordsLayoutFit Fit(MapLayout l, int bad) => new FordsLayoutFit(l, 0, 0, 0)
        {
            Lanes = new[] { new FordsLaneFitStats(0, 100, 100, 0, 0, 12f, bad) },
        };

        Assert.IsTrue(FordsLayoutPlanner.Plan((_, _) => layout, l => Fit(l, 4), 2f).Passed, "4 of 99 segments");
        Assert.IsFalse(FordsLayoutPlanner.Plan((_, _) => layout, l => Fit(l, 10), 2f).Passed, "10 of 99 segments");
    }

    [TestMethod]
    public void Plan_NoCandidatePasses_ChoosesTheLowestScore()
    {
        var layout = Plain();
        var total = ItemCount(layout);
        var calls = 0;
        var dropped = new[] { total / 2, total / 4, total / 3 };

        var plan = FordsLayoutPlanner.Plan((_, _) => layout, l => new FordsLayoutFit(l, 0, dropped[calls++], 0), 2f);

        Assert.IsFalse(plan.Passed);
        Assert.AreEqual(0.7f, plan.Chosen!.SpreadFactor, 1e-4f);
        Assert.AreEqual(total / 4, plan.Chosen.Fit.Dropped);
    }

    [TestMethod]
    public void Plan_ARejectedFit_NeverPassesAndScoresWorseThanAnyUnrejectedOne()
    {
        var layout = Plain();
        var calls = 0;
        var total = ItemCount(layout);

        var plan = FordsLayoutPlanner.Plan((_, _) => layout,
            l => calls++ == 0 ? new FordsLayoutFit(l, 0, 0, 0) { Rejected = true, Reason = "lane 1 has no tower" } : new FordsLayoutFit(l, 0, total / 2, 0), 2f);

        Assert.IsFalse(plan.Attempts[0].Passed);
        Assert.IsTrue(plan.Attempts[0].Score > plan.Attempts[1].Score);
        Assert.AreEqual(0.7f, plan.Chosen!.SpreadFactor, 1e-4f);
    }
}
