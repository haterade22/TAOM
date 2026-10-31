using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.FordsOfWar.Domain;

namespace TAOM.Tests.Features.FordsOfWar.Domain;

/// <summary>Capture progress, contest, owner change and income per owned point.</summary>
[TestClass]
public class CapturePointRulesTests
{
    private static readonly FordsCaptureConfig Capture = FordsMatchConfig.Default.Capture;
    private static readonly FordsEconomyConfig Economy = FordsMatchConfig.Default.Economy;

    private static CapturePointRules Rules(int points = 2) =>
        new(Capture, Economy, Enumerable.Range(0, points).Select(i => new CapturePointSlot($"point_{i}", new Vec3f(i * 10f, 0f, 0f))).ToList());

    private static IReadOnlyList<CaptureCounts> Counts(string id, int west, int east) =>
        new List<CaptureCounts> { new(id, west, east) };

    private static IReadOnlyList<CaptureChange> Run(CapturePointRules rules, string id, int west, int east, float seconds, float step = 1f)
    {
        var changes = new List<CaptureChange>();
        for (var t = 0f; t < seconds - 1e-4f; t += step)
            changes.AddRange(rules.Tick(step, Counts(id, west, east)));
        return changes;
    }

    [TestMethod]
    public void New_PointsStartNeutralWithNoProgress()
    {
        var rules = Rules();

        Assert.AreEqual(2, rules.Points.Count);
        Assert.IsTrue(rules.Points.All(p => p.Owner == null && p.Capturer == null && p.Progress == 0f && !p.Contested));
        Assert.AreEqual(0, rules.OwnedBy(MatchSide.West));
    }

    [TestMethod]
    public void Get_UnknownPoint_ReturnsNull()
    {
        Assert.IsNull(Rules().Get("nowhere"));
    }

    [TestMethod]
    public void Tick_OneSideAlonePresent_RaisesProgressByTimeOverCaptureSeconds()
    {
        var rules = Rules();

        Run(rules, "point_0", 3, 0, 5f);

        var point = rules.Get("point_0")!;
        Assert.AreEqual(MatchSide.West, point.Capturer);
        Assert.AreEqual(5f / Capture.SecondsToCapture, point.Progress, 1e-4f);
        Assert.IsNull(point.Owner);
    }

    [TestMethod]
    public void Tick_FullProgress_ChangesTheOwnerOnceAndReportsIt()
    {
        var rules = Rules();

        var changes = Run(rules, "point_0", 1, 0, Capture.SecondsToCapture + 5f);

        Assert.AreEqual(1, changes.Count);
        Assert.AreEqual("point_0", changes[0].PointId);
        Assert.AreEqual(MatchSide.West, changes[0].NewOwner);
        var point = rules.Get("point_0")!;
        Assert.AreEqual(MatchSide.West, point.Owner);
        Assert.IsNull(point.Capturer);
        Assert.AreEqual(0f, point.Progress);
        Assert.AreEqual(1, rules.OwnedBy(MatchSide.West));
    }

    [TestMethod]
    public void Tick_MoreUnitsPresent_DoNotSpeedUpTheCapture()
    {
        var one = Rules();
        var many = Rules();

        Run(one, "point_0", 1, 0, 10f);
        Run(many, "point_0", 30, 0, 10f);

        Assert.AreEqual(one.Get("point_0")!.Progress, many.Get("point_0")!.Progress, 1e-5f);
    }

    [TestMethod]
    public void Tick_BothSidesPresent_ContestsAndHoldsProgress()
    {
        var rules = Rules();
        Run(rules, "point_0", 1, 0, 8f);
        var before = rules.Get("point_0")!.Progress;

        Run(rules, "point_0", 1, 1, 6f);

        var point = rules.Get("point_0")!;
        Assert.IsTrue(point.Contested);
        Assert.AreEqual(before, point.Progress, 1e-5f);
        Assert.AreEqual(MatchSide.West, point.Capturer);
    }

    [TestMethod]
    public void Tick_ContestEnds_ContestedFlagClears()
    {
        var rules = Rules();
        Run(rules, "point_0", 1, 1, 3f);

        Run(rules, "point_0", 1, 0, 1f);

        Assert.IsFalse(rules.Get("point_0")!.Contested);
    }

    [TestMethod]
    public void Tick_NobodyPresent_UnfinishedProgressDrainsToNothing()
    {
        var rules = Rules();
        Run(rules, "point_0", 1, 0, 10f);

        Run(rules, "point_0", 0, 0, 5f);
        Assert.AreEqual(5f / Capture.SecondsToCapture, rules.Get("point_0")!.Progress, 1e-4f);

        Run(rules, "point_0", 0, 0, 30f);
        var point = rules.Get("point_0")!;
        Assert.AreEqual(0f, point.Progress);
        Assert.IsNull(point.Capturer);
        Assert.IsNull(point.Owner);
    }

    [TestMethod]
    public void Tick_NobodyPresent_AnOwnedPointStaysOwned()
    {
        var rules = Rules();
        Run(rules, "point_0", 1, 0, 25f);

        Run(rules, "point_0", 0, 0, 120f);

        Assert.AreEqual(MatchSide.West, rules.Get("point_0")!.Owner);
    }

    [TestMethod]
    public void Tick_OwnerStandsOnItsOwnPoint_UndoesTheEnemyProgress()
    {
        var rules = Rules();
        Run(rules, "point_0", 1, 0, 25f);
        Run(rules, "point_0", 0, 1, 10f);
        Assert.AreEqual(MatchSide.East, rules.Get("point_0")!.Capturer);

        Run(rules, "point_0", 1, 0, 10f);

        var point = rules.Get("point_0")!;
        Assert.AreEqual(0f, point.Progress);
        Assert.IsNull(point.Capturer);
        Assert.AreEqual(MatchSide.West, point.Owner);
    }

    [TestMethod]
    public void Tick_EnemyHoldsAnOwnedPoint_TakesItAfterTheCaptureTime()
    {
        var rules = Rules();
        Run(rules, "point_0", 1, 0, 25f);

        var changes = Run(rules, "point_0", 0, 2, Capture.SecondsToCapture + 2f);

        Assert.AreEqual(1, changes.Count);
        Assert.AreEqual(MatchSide.East, changes[0].NewOwner);
        Assert.AreEqual(MatchSide.East, rules.Get("point_0")!.Owner);
        Assert.AreEqual(0, rules.OwnedBy(MatchSide.West));
        Assert.AreEqual(1, rules.OwnedBy(MatchSide.East));
    }

    [TestMethod]
    public void Tick_OwnerBeingCaptured_StillOwnsUntilTheLastSecond()
    {
        var rules = Rules();
        Run(rules, "point_0", 1, 0, 25f);

        Run(rules, "point_0", 0, 1, Capture.SecondsToCapture - 2f);

        Assert.AreEqual(MatchSide.West, rules.Get("point_0")!.Owner);
    }

    [TestMethod]
    public void Tick_NeutralPointSwitchesCapturer_DrainsTheOldProgressFirst()
    {
        var rules = Rules();
        Run(rules, "point_0", 1, 0, 10f);

        Run(rules, "point_0", 0, 1, 4f);
        Assert.AreEqual(MatchSide.West, rules.Get("point_0")!.Capturer);
        Assert.AreEqual(6f / Capture.SecondsToCapture, rules.Get("point_0")!.Progress, 1e-4f);

        Run(rules, "point_0", 0, 1, 10f);
        var point = rules.Get("point_0")!;
        Assert.AreEqual(MatchSide.East, point.Capturer);
        Assert.AreEqual(4f / Capture.SecondsToCapture, point.Progress, 1e-4f);
    }

    [TestMethod]
    public void Tick_AloneOnThePoint_ProgressIsRising()
    {
        var rules = Rules();

        Run(rules, "point_0", 1, 0, 3f);

        Assert.IsTrue(rules.Get("point_0")!.Rising);
    }

    [TestMethod]
    public void Tick_NobodyPresent_ProgressStopsRising_WhileTheCapturerStaysSet()
    {
        var rules = Rules();
        Run(rules, "point_0", 1, 0, 10f);

        Run(rules, "point_0", 0, 0, 2f);

        var point = rules.Get("point_0")!;
        Assert.AreEqual(MatchSide.West, point.Capturer, "the progress is still decaying, so the capturer stays set");
        Assert.IsFalse(point.Rising);
    }

    [TestMethod]
    public void Tick_OwnerStandsOnItsPoint_TheEnemyProgressIsNotRising()
    {
        var rules = Rules();
        Run(rules, "point_0", 1, 0, 25f);
        Run(rules, "point_0", 0, 1, 10f);
        Assert.IsTrue(rules.Get("point_0")!.Rising);

        Run(rules, "point_0", 1, 0, 2f);

        var point = rules.Get("point_0")!;
        Assert.AreEqual(MatchSide.East, point.Capturer);
        Assert.IsFalse(point.Rising);
    }

    [TestMethod]
    public void Tick_BothSidesPresent_HeldProgressIsNotRising()
    {
        var rules = Rules();
        Run(rules, "point_0", 1, 0, 8f);

        Run(rules, "point_0", 1, 1, 2f);

        Assert.IsFalse(rules.Get("point_0")!.Rising);
    }

    [TestMethod]
    public void Tick_CapturerWorksTheRivalOff_RisesAgainOnlyOnceTheRivalProgressIsGone()
    {
        var rules = Rules();
        Run(rules, "point_0", 1, 0, 10f);

        Run(rules, "point_0", 0, 1, 4f);
        Assert.IsFalse(rules.Get("point_0")!.Rising, "still working the old progress off");

        Run(rules, "point_0", 0, 1, 10f);
        var point = rules.Get("point_0")!;
        Assert.AreEqual(MatchSide.East, point.Capturer);
        Assert.IsTrue(point.Rising);
    }

    [TestMethod]
    public void Tick_CaptureFinishes_TheNewOwnerPointIsNotRising()
    {
        var rules = Rules();

        Run(rules, "point_0", 1, 0, Capture.SecondsToCapture + 5f);

        Assert.IsFalse(rules.Get("point_0")!.Rising);
    }

    [TestMethod]
    public void Tick_AMissingCountsRow_MeansNobodyIsThere()
    {
        var rules = Rules();
        Run(rules, "point_0", 1, 0, 10f);

        rules.Tick(5f, new List<CaptureCounts>());

        Assert.AreEqual(5f / Capture.SecondsToCapture, rules.Get("point_0")!.Progress, 1e-4f);
    }

    [TestMethod]
    public void Tick_CountsForAnUnknownPoint_AreIgnored()
    {
        var rules = Rules();

        var changes = rules.Tick(100f, Counts("ghost", 5, 0));

        Assert.AreEqual(0, changes.Count);
        Assert.IsTrue(rules.Points.All(p => p.Progress == 0f));
    }

    [TestMethod]
    public void Tick_ALongStep_CapturesWithoutOvershootingTheProgress()
    {
        var rules = Rules();

        var changes = rules.Tick(500f, Counts("point_0", 1, 0));

        Assert.AreEqual(1, changes.Count);
        Assert.AreEqual(0f, rules.Get("point_0")!.Progress);
    }

    [TestMethod]
    public void Tick_NonPositiveStep_ChangesNothing()
    {
        var rules = Rules();

        var changes = rules.Tick(0f, Counts("point_0", 1, 0));

        Assert.AreEqual(0, changes.Count);
        Assert.AreEqual(0f, rules.Get("point_0")!.Progress);
    }

    [TestMethod]
    public void Tick_PointsAreIndependent()
    {
        var rules = Rules();

        rules.Tick(Capture.SecondsToCapture + 1f, new List<CaptureCounts> { new("point_0", 1, 0), new("point_1", 0, 1) });

        Assert.AreEqual(MatchSide.West, rules.Get("point_0")!.Owner);
        Assert.AreEqual(MatchSide.East, rules.Get("point_1")!.Owner);
    }

    [TestMethod]
    public void CollectIncome_NoPoints_PaysNothing()
    {
        Assert.AreEqual(0, Rules().CollectIncome(MatchSide.West, 600f));
    }

    [TestMethod]
    public void CollectIncome_PerOwnedPoint_PaysCaptureIncomePerMinute()
    {
        var rules = Rules();
        rules.Tick(25f, new List<CaptureCounts> { new("point_0", 1, 0), new("point_1", 1, 0) });

        Assert.AreEqual(2 * Economy.CaptureIncomePerMinute, rules.CollectIncome(MatchSide.West, 60f));
        Assert.AreEqual(0, rules.CollectIncome(MatchSide.East, 60f));
    }

    [TestMethod]
    public void CollectIncome_FractionsCarryOverToTheNextCall()
    {
        var rules = Rules(1);
        rules.Tick(25f, Counts("point_0", 1, 0));

        var total = 0;
        for (var i = 0; i < 120; i++)
            total += rules.CollectIncome(MatchSide.West, 0.5f);

        Assert.AreEqual(Economy.CaptureIncomePerMinute, total);
    }

    [TestMethod]
    public void CollectIncome_AfterThePointIsLost_StopsPaying()
    {
        var rules = Rules(1);
        rules.Tick(25f, Counts("point_0", 1, 0));
        rules.Tick(25f, Counts("point_0", 0, 1));

        Assert.AreEqual(0, rules.CollectIncome(MatchSide.West, 60f));
        Assert.AreEqual(Economy.CaptureIncomePerMinute, rules.CollectIncome(MatchSide.East, 60f));
    }
}
