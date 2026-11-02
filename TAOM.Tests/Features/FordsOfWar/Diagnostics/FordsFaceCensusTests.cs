using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Adapters;
using TAOM.Features.FordsOfWar;
using TAOM.Features.FordsOfWar.Diagnostics;
using TAOM.Features.FordsOfWar.Domain;
using TAOM.Tests.Features.FordsOfWar.Scenarios;

namespace TAOM.Tests.Features.FordsOfWar.Diagnostics;

/// <summary>
/// The info-only census of a tags map: which navigation face group each unit stands on, and how high it stands over the terrain
/// and under the navmesh. It never feeds a verdict; it is the evidence the lane-bar slice and the navmesh float question read.
/// </summary>
[TestClass]
public class FordsFaceCensusTests
{
    private static readonly FordsMatchSetup Setup = new("taom_fords_01", "gondor", "mordor", "hard", 3);
    private static readonly FordsMatchFacts Facts = new(60f, 8f, 12f, 3, 150, 200);

    private ScenarioLogger _log = null!;
    private FordsRunReport _report = null!;

    [TestInitialize]
    public void Init()
    {
        _log = new ScenarioLogger();
        _report = new FordsRunReport(_log);
        _report.BeginRun("menu", Setup);
        _report.MatchStarted(Facts, new List<TowerSlot>(), 0);
    }

    private static FordsFaceSample Face(MatchSide side, int lane, int face, bool mounted = false, float above = 0f, float below = 0f) =>
        new(side, lane, face, mounted, above, below);

    private List<string> Lines(string topic) => _log.Lines.Where(l => l.Contains("[FordsOfWar][" + topic + "]")).ToList();

    private void Look(double dt, params FordsFaceSample[] samples)
    {
        _report.MissionTick((float)dt, 10.0);
        _report.Faces(samples);
    }

    [TestMethod]
    public void Classify_OwnLaneId_IsLanePlusOne()
    {
        Assert.AreEqual(FordsFaceClass.Own, FordsFaceCensus.Classify(0, 1));
        Assert.AreEqual(FordsFaceClass.Own, FordsFaceCensus.Classify(2, 3));
    }

    [TestMethod]
    public void Classify_SharedForeignTopAndOff()
    {
        Assert.AreEqual(FordsFaceClass.Shared, FordsFaceCensus.Classify(1, 0));
        Assert.AreEqual(FordsFaceClass.Foreign, FordsFaceCensus.Classify(0, 3));
        Assert.AreEqual(FordsFaceClass.Top, FordsFaceCensus.Classify(1, 4));
        Assert.AreEqual(FordsFaceClass.Off, FordsFaceCensus.Classify(1, -1));
    }

    [TestMethod]
    public void Faces_BeforeThirtySeconds_WritesNoLine()
    {
        Look(1.0, Face(MatchSide.West, 0, 1));
        Look(28.0, Face(MatchSide.West, 0, 1));

        Assert.AreEqual(0, Lines("Lane").Count);
        Assert.AreEqual(0, Lines("Ground").Count);
    }

    [TestMethod]
    public void Faces_AtThirtySeconds_WritesOneLaneLinePerStreamWithUnitSeconds()
    {
        Look(1.0, Face(MatchSide.West, 0, 1), Face(MatchSide.West, 0, 0, mounted: true), Face(MatchSide.East, 2, 1));
        Look(29.0, Face(MatchSide.West, 0, 1), Face(MatchSide.West, 0, 0, mounted: true), Face(MatchSide.East, 2, 1));

        var lines = Lines("Lane");
        Assert.AreEqual(2, lines.Count, string.Join("|", lines));
        StringAssert.Contains(lines[0], "faces W0 own=29.0 shared=29.0 foreign=0.0 top=0.0 off=0.0 foot=29.0 mounted=29.0");
        StringAssert.Contains(lines[1], "faces E2 own=0.0 shared=0.0 foreign=29.0 top=0.0 off=0.0 foot=29.0 mounted=0.0");
    }

    [TestMethod]
    public void Faces_FirstLook_CountsNoSeconds()
    {
        Look(1.0, Face(MatchSide.West, 0, 4));
        Look(29.0);

        Assert.AreEqual(0, Lines("Lane").Count);
    }

    [TestMethod]
    public void Faces_GroundLine_GivesUnitsAndMinMedianMaxOfTheLatestLook()
    {
        Look(1.0, Face(MatchSide.West, 0, 1));
        Look(29.0, Face(MatchSide.West, 0, 1, above: 0.2f, below: 1.1f), Face(MatchSide.West, 0, 1, above: 0.4f, below: 1.2f),
            Face(MatchSide.East, 0, 1, above: 1.0f, below: 1.5f));

        var line = Lines("Ground").Single();
        StringAssert.Contains(line, "units=3 aboveTerrain=0.2/0.4/1.0 belowNav=1.1/1.2/1.5 offNavMoves=0");
    }

    [TestMethod]
    public void Faces_GroundLine_ANanBelowNavIsLeftOutAndAnEmptyLookReadsDashes()
    {
        Look(1.0);
        Look(29.0, Face(MatchSide.West, 0, 1, above: 0.5f, below: float.NaN));

        var line = Lines("Ground").Single();
        StringAssert.Contains(line, "units=1 aboveTerrain=0.5/0.5/0.5 belowNav=-/-/- offNavMoves=0");
    }

    [TestMethod]
    public void Faces_GroundLine_CountsTheRunsOffNavmeshRefusals()
    {
        _report.OffNavmesh(MatchSide.West, 0, 1f, 2f);
        _report.OffNavmesh(MatchSide.East, 1, 1f, 2f);
        Look(1.0, Face(MatchSide.West, 0, 1));
        Look(29.0, Face(MatchSide.West, 0, 1));

        StringAssert.Contains(Lines("Ground").Single(), "offNavMoves=2");
    }

    [TestMethod]
    public void Faces_NextLineComesThirtySecondsLater()
    {
        Look(1.0, Face(MatchSide.West, 0, 1));
        Look(29.0, Face(MatchSide.West, 0, 1));
        Look(20.0, Face(MatchSide.West, 0, 1));
        Assert.AreEqual(1, Lines("Ground").Count);

        Look(10.0, Face(MatchSide.West, 0, 1));
        Assert.AreEqual(2, Lines("Ground").Count);
    }

    [TestMethod]
    public void Faces_AreInTheEvidence_ButNeverInAVerdict()
    {
        Look(1.0, Face(MatchSide.West, 0, 4));
        Look(5.0, Face(MatchSide.West, 0, 4));

        Assert.AreEqual(5.0, _report.Run!.Faces.Streams["W0"].Top, 1e-6);
        var before = FordsVerdicts.Judge(new FordsSessionEvidence(), _report.Run).Select(r => r.Result).ToList();
        _report.Run.Faces.Streams.Clear();
        var after = FordsVerdicts.Judge(new FordsSessionEvidence(), _report.Run).Select(r => r.Result).ToList();
        CollectionAssert.AreEqual(before, after);
    }
}
