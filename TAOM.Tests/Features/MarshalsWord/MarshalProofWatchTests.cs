using System;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.MarshalsWord;
using TAOM.Features.MarshalsWord.Domain;
using TAOM.Features.MarshalsWord.Hooks;
using TAOM.Tests.Features.SeatOfSeeing;

namespace TAOM.Tests.Features.MarshalsWord;

/// <summary>The square's and the spacing repair's proof lines (build-spec 9.1 and 9.3), over a fake order adapter.</summary>
[TestClass]
public class MarshalProofWatchTests
{
    private static readonly SpacingLimits Infantry = new(0f, 0.4f, 0f, 0.38f);
    private static readonly SpacingLimits Cavalry = new(1.7f, 2.0f, 0.18f, 0.5f);

    private FakeMarshalOrders _orders = null!;
    private RecordingOutcomeLog _log = null!;
    private ListLogger _logger = null!;
    private MarshalProofWatch _watch = null!;

    [TestInitialize]
    public void Setup()
    {
        _orders = new FakeMarshalOrders();
        _log = new RecordingOutcomeLog();
        _logger = new ListLogger();
        _watch = new MarshalProofWatch(_orders, _log, _logger, "7");
        MarshalSpacingPatch.WhyNot = "";
    }

    [TestCleanup]
    public void Cleanup() => MarshalSpacingPatch.WhyNot = "not-prepared";

    private void Pass(double now)
    {
        _orders.Now = now;
        _watch.Pass();
    }

    private static SquareRead Square(int units = 40, int placed = 40, bool laidOut = false) => new(2, 14.5f, 9, 5, units, placed, laidOut);

    // ---- the square ----

    [TestMethod]
    public void Pass_ASquareWithEverySoldierPlaced_WritesItsLineAndThePassingCheck()
    {
        _orders.Squares = new[] { Square() };

        Pass(1.0);

        Assert.AreEqual("[Marshal] square company=2 side=14.5 files=9 ranks=5 units=40 placed=40 laidOut=0", _logger.Lines.Single());
        var line = _log.Lines.Single(l => l.Id == MarshalChecks.SquareId);
        Assert.AreEqual("pass", line.Result);
        Assert.AreEqual("mission=7 company=2 side=14.5 files=9 ranks=5 units=40 placed=40 laidOut=0", line.Detail);
    }

    [TestMethod]
    public void Pass_ASquareWithOneSoldierUnplaced_FailsTheCheck()
    {
        _orders.Squares = new[] { Square(units: 40, placed: 39) };

        Pass(1.0);

        Assert.AreEqual("fail", _log.Lines.Single(l => l.Id == MarshalChecks.SquareId).Result);
    }

    [TestMethod]
    public void Pass_ASquareOfAMixedLayout_SaysSoOnItsLine()
    {
        _orders.Squares = new[] { Square(laidOut: true) };

        Pass(1.0);

        StringAssert.EndsWith(_logger.Lines.Single(), "laidOut=1");
    }

    [TestMethod]
    public void Pass_TheSameSquareAgain_WritesNothingMore_AndAChangedCountWaitsForTheSecond()
    {
        _orders.Squares = new[] { Square() };
        Pass(1.0);

        Pass(1.25);
        _orders.Squares = new[] { Square(units: 39, placed: 39) };
        Pass(1.5);
        Assert.AreEqual(1, _logger.Lines.Count, "the count changed inside the second");
        Pass(2.0);

        Assert.AreEqual(2, _logger.Lines.Count);
        StringAssert.Contains(_logger.Lines.Last(), "units=39 placed=39");
    }

    [TestMethod]
    public void Pass_ACompanyThatSquaresBreaksAndSquaresAgainWithTheSameCount_WritesALineForEachSquare()
    {
        _orders.Squares = new[] { Square() };
        Pass(1.0);
        _orders.Squares = Array.Empty<SquareRead>();
        Pass(1.25);
        _orders.Squares = new[] { Square() };

        Pass(1.5);

        Assert.AreEqual(2, _logger.Lines.Count, "a square that formed again is a new square");
        Assert.AreEqual(2, _log.Calls("pass", MarshalChecks.SquareId), "and the check judges it again; the log writes a line once per key");
    }

    [TestMethod]
    public void Pass_TwoSquaresOfTwoCompanies_KeepEachOthersLastLine_WhenOnlyOneBreaks()
    {
        _orders.Squares = new[] { Square(), new SquareRead(5, 14.5f, 9, 5, 40, 40, false) };
        Pass(1.0);
        _orders.Squares = new[] { new SquareRead(5, 14.5f, 9, 5, 40, 40, false) };
        Pass(1.25);
        _orders.Squares = new[] { Square(), new SquareRead(5, 14.5f, 9, 5, 40, 40, false) };

        Pass(1.5);

        Assert.AreEqual(3, _logger.Lines.Count, "company 2 formed again and wrote, company 5 stood throughout and wrote only its first line");
    }

    [TestMethod]
    public void Pass_NoSquare_WritesNothing()
    {
        Pass(1.0);

        Assert.AreEqual(0, _logger.Lines.Count);
        Assert.AreEqual(0, _log.Lines.Count);
    }

    [TestMethod]
    public void Pass_ReadsOnlyAtTheSlowCadence()
    {
        _orders.Squares = new[] { Square() };
        Pass(1.0);
        _orders.Squares = new[] { Square(units: 10, placed: 10) };

        Pass(1.1);

        Assert.AreEqual(1, _logger.Lines.Count);
    }

    // ---- the spacing repair ----

    private static SpacingRead Read(SpacingLimits measured) => new(1, 41, 1, measured, Infantry, Cavalry);

    [TestMethod]
    public void Pass_ACompanyWithOneRiderThatReadsTheFootLimits_PassesTheSpacingCheck()
    {
        _orders.Spacing = Read(Infantry);

        Pass(1.0);

        var line = _log.Lines.Single(l => l.Id == MarshalChecks.SpacingId);
        Assert.AreEqual("pass", line.Result);
        StringAssert.Contains(line.Detail, "company=1 units=41 riders=1 minDistance=0.00 maxDistance=0.40");
        StringAssert.Contains(_logger.Lines.Single(), "repair=applied reads=Infantry");
        Assert.AreEqual(SpacingReading.Infantry, _watch.SpacingVerdict);
    }

    [TestMethod]
    public void Pass_ACompanyWithOneRiderThatStillReadsTheCavalryLimits_FailsTheSpacingCheck()
    {
        _orders.Spacing = Read(Cavalry);

        Pass(1.0);

        var line = _log.Lines.Single(l => l.Id == MarshalChecks.SpacingId);
        Assert.AreEqual("fail", line.Result);
        StringAssert.Contains(line.Detail, "reads=Cavalry");
    }

    [TestMethod]
    public void Pass_ACompanyWithHalfTheLimitsRepaired_FailsTheSpacingCheck()
    {
        _orders.Spacing = Read(Infantry with { MinDistance = Cavalry.MinDistance });

        Pass(1.0);

        StringAssert.Contains(_log.Lines.Single(l => l.Id == MarshalChecks.SpacingId).Detail, "reads=Mixed");
        Assert.AreEqual("fail", _log.Lines.Single(l => l.Id == MarshalChecks.SpacingId).Result);
    }

    [TestMethod]
    public void Pass_TheRepairNotApplied_SkipsWithItsReason_AndNeverJudgesTheLimits()
    {
        MarshalSpacingPatch.WhyNot = "coop";
        _orders.Spacing = Read(Cavalry);

        Pass(1.0);

        var line = _log.Lines.Single(l => l.Id == MarshalChecks.SpacingId);
        Assert.AreEqual("skip", line.Result);
        StringAssert.Contains(line.Detail, "reason=coop");
        StringAssert.Contains(_logger.Lines.Single(), "repair=coop");
        Assert.IsNull(_watch.SpacingVerdict);
    }

    [TestMethod]
    public void Pass_LimitsThatInfantryAndCavalryShare_SkipAsNoTest()
    {
        _orders.Spacing = new SpacingRead(1, 41, 1, Infantry, Infantry, Infantry);

        Pass(1.0);

        var line = _log.Lines.Single(l => l.Id == MarshalChecks.SpacingId);
        Assert.AreEqual("skip", line.Result);
        StringAssert.Contains(line.Detail, "reason=no-test");
    }

    [TestMethod]
    public void Pass_AfterTheSpacingVerdict_ReadsNoMoreCompanies()
    {
        _orders.Spacing = Read(Infantry);
        Pass(1.0);
        var reads = _orders.SpacingReads;

        Pass(2.0);
        Pass(3.0);

        Assert.AreEqual(reads, _orders.SpacingReads);
    }

    [TestMethod]
    public void Pass_NoCompanyWithARider_KeepsLookingAtTheSlowCadence()
    {
        Pass(1.0);
        Pass(1.25);
        _orders.Spacing = Read(Infantry);

        Pass(1.5);

        Assert.AreEqual(3, _orders.SpacingReads);
        Assert.AreEqual(1, _log.Lines.Count(l => l.Id == MarshalChecks.SpacingId));
    }
}
