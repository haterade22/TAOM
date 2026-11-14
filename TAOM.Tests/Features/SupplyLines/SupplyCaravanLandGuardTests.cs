using System.Text.RegularExpressions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.SupplyLines;
using TAOM.Tests.Infrastructure;

namespace TAOM.Tests.Features.SupplyLines;

/// <summary>
/// #1104: a supply caravan is a land party, so the movement pass never places it on a point that is not walkable
/// land. The straight-line fallback route (no land path, for example to a player at sea) crosses water and
/// impassable ground. The write itself goes through engine statics, so the order of the check and the write is pinned
/// on the method body with comments stripped.
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

        var check = Regex.Match(body, @"if\s*\(\s*position\s*\.\s*IsValid\s*\(\s*\)\s*\)");
        Assert.IsTrue(check.Success, "the position must pass CampaignVec2.IsValid (land face, walkable terrain)");
        Assert.IsTrue(check.Index < writes[0].Index, "the land check must come before the write");
    }

    [TestMethod]
    public void TickPositions_HeldCaravan_DoesNotTurn()
    {
        string body = TickPositionsBody();

        var bearing = Regex.Match(body, @"if\s*\(\s*placed\s*&&[^)]*\)\s*ApplyBearing\s*\(");
        Assert.IsTrue(bearing.Success, "a caravan held off a non-land point must keep its bearing");
    }

    [TestMethod]
    public void OffLandWarning_PlayerAtSea_NamesTheOrderAndTheSea()
    {
        string line = SupplyCaravanService.OffLandWarning("taom_so_7", playerAtSea: true);

        StringAssert.StartsWith(line, "[SupplyLines] ");
        StringAssert.Contains(line, "order taom_so_7");
        StringAssert.Contains(line, "playerAtSea=True");
    }

    [TestMethod]
    public void OffLandWarning_PlayerOnLand_SaysSo()
    {
        string line = SupplyCaravanService.OffLandWarning("taom_so_7", playerAtSea: false);

        StringAssert.Contains(line, "playerAtSea=False");
    }
}
