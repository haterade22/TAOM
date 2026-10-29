using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Core.Logging;
using TAOM.Features.StagedSiege;
using TAOM.Features.StagedSiege.Domain;

namespace TAOM.Tests.Features.StagedSiege;

/// <summary>The recorder's JSONL lines turned into the shipped JSON shape, ready to paste into staged_siege.json.</summary>
[TestClass]
public class StagedSiegeRecordFragmentTests
{
    private static string Line(string scene, string stage, string kind, double x, double utc, string extra = "") =>
        "{\"scene\":\"" + scene + "\",\"stage\":\"" + stage + "\",\"kind\":\"" + kind + "\",\"x\":" + x.ToString(System.Globalization.CultureInfo.InvariantCulture) +
        ",\"y\":2.5,\"z\":3.5" + extra + ",\"utc\":\"2026-10-29T10:00:" + utc.ToString("00") + "Z\"}";

    private const string Facing = ",\"fx\":0,\"fy\":1";

    [TestMethod]
    public void ParseLine_Valid_Record()
    {
        var r = StagedSiegeRecordFragment.ParseLine(Line("s1", "b", "defender", 1.5, 7, Facing + ",\"radius\":9"));

        Assert.IsNotNull(r);
        Assert.AreEqual("s1", r!.Scene);
        Assert.AreEqual('B', r.Stage);
        Assert.AreEqual("defender", r.Kind);
        Assert.AreEqual(1.5f, r.X);
        Assert.AreEqual(2.5f, r.Y);
        Assert.AreEqual(3.5f, r.Z);
        Assert.AreEqual(1f, r.Fy);
        Assert.AreEqual(9f, r.Radius);
        Assert.AreEqual(7, r.Utc.Second);
    }

    [DataTestMethod]
    [DataRow("")]
    [DataRow("not json")]
    [DataRow("[1,2]")]
    [DataRow("{\"scene\":\"s\"}")]
    [DataRow("{\"scene\":\"s\",\"stage\":\"E\",\"kind\":\"point\",\"x\":1,\"y\":1,\"z\":1}")]
    [DataRow("{\"scene\":\"s\",\"stage\":\"A\",\"kind\":\"attacker\",\"x\":1,\"y\":1,\"z\":1}")]
    [DataRow("{\"scene\":\"s\",\"stage\":\"A\",\"kind\":\"defender\",\"x\":1,\"y\":1,\"z\":1,\"fx\":0,\"fy\":1}")]
    [DataRow("{\"scene\":\"s\",\"stage\":\"B\",\"kind\":\"detachment\",\"x\":1,\"y\":1,\"z\":1,\"fx\":0,\"fy\":1}")]
    [DataRow("{\"scene\":\"s\",\"stage\":\"A\",\"kind\":\"point\",\"x\":\"1\",\"y\":1,\"z\":1}")]
    [DataRow("{\"scene\":\"s\",\"stage\":\"C\",\"kind\":\"defender\",\"x\":1,\"y\":1,\"z\":1}")]
    public void ParseLine_Garbage_Null(string line)
    {
        Assert.IsNull(StagedSiegeRecordFragment.ParseLine(line));
    }

    [TestMethod]
    public void ParseLine_Null_Null() => Assert.IsNull(StagedSiegeRecordFragment.ParseLine(null));

    private static string[] FullSet(string scene) => new[]
    {
        Line(scene, "A", "point", 1, 1), Line(scene, "B", "point", 2, 1), Line(scene, "C", "point", 3, 1), Line(scene, "D", "point", 4, 1),
        Line(scene, "A", "detachment", 5, 1, Facing), Line(scene, "B", "defender", 6, 1, Facing),
        Line(scene, "C", "defender", 7, 1, Facing), Line(scene, "D", "defender", 8, 1, Facing),
    };

    [TestMethod]
    public void Build_NewestPerStageAndKindWins()
    {
        var lines = FullSet("s1").Concat(new[]
        {
            Line("s1", "A", "point", 99, 30),   // newer, kept
            Line("s1", "A", "point", 50, 10),   // older, ignored even though it comes later in the file
            Line("other", "A", "point", 77, 59), // another scene, ignored
        });

        var fragment = StagedSiegeRecordFragment.Build("s1", lines.Select(StagedSiegeRecordFragment.ParseLine).Where(r => r != null)!);

        Assert.IsTrue(fragment.IsComplete);
        StringAssert.Contains(fragment.Json, "99");
        Assert.IsFalse(fragment.Json.Contains("50.0") || fragment.Json.Contains(": 50,"), fragment.Json);
        Assert.IsFalse(fragment.Json.Contains("77"));
    }

    [TestMethod]
    public void Build_SameTimestamp_LaterLineWins()
    {
        var lines = new[] { Line("s1", "A", "point", 11, 5), Line("s1", "A", "point", 22, 5) };

        var fragment = StagedSiegeRecordFragment.Build("s1", lines.Select(StagedSiegeRecordFragment.ParseLine)!);

        StringAssert.Contains(fragment.Json, "22");
        Assert.IsFalse(fragment.Json.Contains("11"));
    }

    [TestMethod]
    public void Build_MissingPiecesListed()
    {
        var lines = new[] { Line("s1", "A", "point", 1, 1), Line("s1", "C", "point", 3, 1), Line("s1", "B", "defender", 6, 1, Facing) };

        var fragment = StagedSiegeRecordFragment.Build("s1", lines.Select(StagedSiegeRecordFragment.ParseLine)!);

        Assert.IsFalse(fragment.IsComplete);
        CollectionAssert.AreEquivalent(
            new[] { "point B", "point D", "detachment A", "defender C", "defender D" },
            fragment.Missing.ToArray());
    }

    [TestMethod]
    public void Build_NoRecordsForTheScene_EverythingMissing()
    {
        var fragment = StagedSiegeRecordFragment.Build("s1", new[] { StagedSiegeRecordFragment.ParseLine(Line("other", "A", "point", 1, 1))! });

        Assert.AreEqual(8, fragment.Missing.Count);
    }

    [TestMethod]
    public void Build_RoundTripsThroughCatalogWithNoWarnings()
    {
        var records = FullSet("round_trip_scene").Select(StagedSiegeRecordFragment.ParseLine)!;
        var fragment = StagedSiegeRecordFragment.Build("round_trip_scene", records);
        var logger = Substitute.For<IModLogger>();

        var catalog = StagedSiegeSceneCatalog.FromText(fragment.Json, logger);

        Assert.IsTrue(catalog.TryGetScene("round_trip_scene", out var plan));
        Assert.AreEqual(4f, plan.Zone(StageId.D).Center.X);
        Assert.AreEqual(5f, plan.Detachment.Position.X);
        Assert.AreEqual(8f, plan.DefenderFrame(StageId.D)!.Value.Position.X);
        Assert.AreEqual(1f, plan.DefenderFrame(StageId.B)!.Value.Facing.Y);
        logger.DidNotReceive().LogWarning(Arg.Any<string>());
    }
}
