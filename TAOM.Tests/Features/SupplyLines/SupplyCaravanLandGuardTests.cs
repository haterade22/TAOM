using System.Text.RegularExpressions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.SupplyLines;
using TAOM.Tests.Infrastructure;

namespace TAOM.Tests.Features.SupplyLines;

/// <summary>
/// #1104: a supply caravan is a land party, and the movement pass never moves it off walkable land. Spawn and
/// RespawnMissing place a caravan without this check, so only the movement pass is guarded. The reachable triggers are
/// the smoothed point cutting a bend, the glide after a repath, and the straight-line fallback route (an endpoint face
/// is invalid or the path query fails or returns fewer than two points). The write itself goes through engine
/// statics, so its placement inside the check is pinned on the method body with comments stripped.
/// </summary>
[TestClass]
public class SupplyCaravanLandGuardTests
{
    private const string ServicePath = "Main/Features/SupplyLines/SupplyCaravanService.cs";

    private static string TickPositionsBody()
    {
        string source = RepoPaths.ReadSource(ServicePath, stripComments: true);
        int start = source.IndexOf("public void TickPositions()", System.StringComparison.Ordinal);
        Assert.IsTrue(start >= 0, "TickPositions not found in " + ServicePath);
        int end = source.IndexOf("\n    }\n", start, System.StringComparison.Ordinal);
        Assert.IsTrue(end > start, "end of TickPositions not found");
        return source.Substring(start, end - start);
    }

    [TestMethod]
    public void TickPositions_PointOffWalkableLand_IsNeverWritten()
    {
        string body = TickPositionsBody();

        var writes = Regex.Matches(body, @"party\s*\.\s*Position\s*=");
        Assert.AreEqual(1, writes.Count, "TickPositions must write the caravan position in one place only");
        Assert.IsTrue(
            Regex.IsMatch(body, @"party\s*\.\s*Position\s*=\s*position\s*;"),
            "the write must use the checked position, not a new one");

        Assert.IsTrue(
            Regex.IsMatch(body, @"if\s*\(\s*position\s*\.\s*IsValid\s*\(\s*\)\s*\)\s*\{\s*party\s*\.\s*Position\s*=\s*position\s*;"),
            "the write must be the first statement inside the CampaignVec2.IsValid block (land face, walkable terrain)");
    }

    [TestMethod]
    public void TickPositions_HeldCaravan_DoesNotTurn()
    {
        string body = TickPositionsBody();

        var bearing = Regex.Match(body, @"if\s*\(\s*placed\s*&&[^)]*\)\s*ApplyBearing\s*\(");
        Assert.IsTrue(bearing.Success, "a caravan held off a non-land point must keep its bearing");
        Assert.IsTrue(
            Regex.IsMatch(body, @"bool\s+placed\s*=\s*false\s*;"),
            "placed must start false so only a written point turns the caravan");
    }

    [TestMethod]
    public void ClearTrackers_ResetsTheOffLandLatch_SoTheLineIsOncePerCampaignSession()
    {
        string source = RepoPaths.ReadSource(ServicePath, stripComments: true);
        int start = source.IndexOf("public void ClearTrackers()", System.StringComparison.Ordinal);
        Assert.IsTrue(start >= 0, "ClearTrackers not found in " + ServicePath);
        int end = source.IndexOf("\n    }\n", start, System.StringComparison.Ordinal);
        string body = source.Substring(start, end - start);

        Assert.IsTrue(Regex.IsMatch(body, @"_offLandWarned\s*=\s*false\s*;"),
            "a new or loaded campaign must be able to log the off-land line again");
    }

    [TestMethod]
    public void OffLandWarning_CarriesPositionFaceAndTerrain()
    {
        string line = SupplyCaravanService.OffLandWarning("taom_so_7", false, 123.456f, 78.9f, 4521, "Water");

        StringAssert.Contains(line, "pos=(123.46,78.90)");
        StringAssert.Contains(line, "face=4521");
        StringAssert.Contains(line, "terrain=Water");
        StringAssert.Contains(line, "once per campaign session");
    }

    [TestMethod]
    public void OffLandWarning_PlayerAtSea_NamesTheOrderAndTheSea()
    {
        string line = SupplyCaravanService.OffLandWarning("taom_so_7", true, 1f, 2f, -1, "unknown");

        StringAssert.StartsWith(line, "[SupplyLines] ");
        StringAssert.Contains(line, "order taom_so_7");
        StringAssert.Contains(line, "playerAtSea=True");
    }

    [TestMethod]
    public void OffLandWarning_PlayerOnLand_SaysSo()
    {
        string line = SupplyCaravanService.OffLandWarning("taom_so_7", false, 1f, 2f, -1, "unknown");

        StringAssert.Contains(line, "playerAtSea=False");
    }
}
