using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.WarOfTheRingMomentum;
using TAOM.Features.WarOfTheRingMomentum.Cheats;

namespace TAOM.Tests.Features.WarOfTheRingMomentum;

/// <summary>
/// Pins the `taom.print_momentum` console report.
///
/// Why this command exists: the engine's `ArchiveSerializer.SerializeEntry` writes each entry's
/// length as `(short)Data.Length`, so a single SyncData string over ~32 KB is written with a wrong
/// length and corrupts the save at write time. `MomentumSyncChunker` keeps every chunk under the cap,
/// but confirming that today means playing a campaign to roughly day 50 with active wars and then
/// saving. The command reads the same number in one line.
///
/// The `WOULD CORRUPT` branch is unreachable by construction right now (10,000 chars × 3 bytes
/// worst-case = 30,000 &lt; 32,763). That is exactly why it is pinned: a future bump to
/// <see cref="MomentumSyncChunker.MaxChunkChars"/> is the change that makes it reachable, and this
/// test is what turns that into a red build instead of a corrupted save.
/// </summary>
[TestClass]
public class MomentumCheatsFormatTests
{
    private static IReadOnlyList<string> Chunk(string payload) => MomentumSyncChunker.Split(payload);

    [TestMethod]
    public void FormatPayloadReport_EmptyPayload_ReportsZeroChunksAndDoesNotClaimCorruption()
    {
        // Arrange
        var payload = string.Empty;

        // Act
        var report = MomentumCheats.FormatPayloadReport(0, payload, Chunk(payload));

        // Assert
        StringAssert.Contains(report, "chunks: 0");
        StringAssert.Contains(report, "OK");
        Assert.IsFalse(report.Contains("WOULD CORRUPT"));
    }

    [TestMethod]
    public void FormatPayloadReport_PayloadAtChunkLimit_ReportsOneChunk()
    {
        var payload = new string('a', MomentumSyncChunker.MaxChunkChars);

        var report = MomentumCheats.FormatPayloadReport(3, payload, Chunk(payload));

        StringAssert.Contains(report, "chunks: 1");
        StringAssert.Contains(report, "OK");
    }

    [TestMethod]
    public void FormatPayloadReport_PayloadOneOverChunkLimit_ReportsTwoChunks()
    {
        var payload = new string('a', MomentumSyncChunker.MaxChunkChars + 1);

        var report = MomentumCheats.FormatPayloadReport(3, payload, Chunk(payload));

        StringAssert.Contains(report, "chunks: 2");
    }

    /// <summary>
    /// The branch that matters. Feeds a hand-built chunk past the engine's entry limit — which the
    /// real chunker cannot currently produce — and asserts the report says so loudly and names the
    /// offending chunk, rather than reporting a cheerful byte count.
    /// </summary>
    [TestMethod]
    public void FormatPayloadReport_ChunkOverEngineEntryLimit_ReportsWouldCorruptAndNamesTheChunk()
    {
        var oversized = new string('a', MomentumSyncChunker.EngineEntryByteLimit + 1);
        var chunks = new List<string> { "small", oversized };

        var report = MomentumCheats.FormatPayloadReport(2, "small" + oversized, chunks);

        StringAssert.Contains(report, "WOULD CORRUPT");
        // Chunk index is 1-based in the report so it reads naturally next to "chunks: 2".
        StringAssert.Contains(report, "chunk 2");
        Assert.IsFalse(report.Contains("status: OK"));
    }

    /// <summary>
    /// The cap is a CHARACTER cap but the engine limit is a BYTE limit, so a payload of multi-byte
    /// codepoints has more bytes than chars. A report that conflated the two would understate how
    /// close a non-ASCII save is to the cliff — and TAOM ships 12 localizations.
    /// </summary>
    [TestMethod]
    public void FormatPayloadReport_MultiByteUtf8_ReportsByteLengthDistinctFromCharLength()
    {
        // U+00E9 is two UTF-8 bytes; 10 chars → 20 bytes.
        var payload = new string('é', 10);

        var report = MomentumCheats.FormatPayloadReport(1, payload, Chunk(payload));

        StringAssert.Contains(report, "10 chars");
        StringAssert.Contains(report, "20 bytes");
    }

    [TestMethod]
    public void FormatPayloadReport_ReportsKeyCountAndHeadroom()
    {
        var payload = new string('a', 100);

        var report = MomentumCheats.FormatPayloadReport(41, payload, Chunk(payload));

        StringAssert.Contains(report, "41 keys");
        StringAssert.Contains(report, "headroom");
    }

    // ---- key breakdown ----------------------------------------------------

    [TestMethod]
    public void FormatKeyReport_OrdersKeysByDescendingSize()
    {
        var data = new Dictionary<string, string>
        {
            ["small"] = "x",
            ["big"] = new string('y', 500),
            ["medium"] = new string('z', 50),
        };

        var report = MomentumCheats.FormatKeyReport(data, top: 10);

        var bigIndex = report.IndexOf("big", System.StringComparison.Ordinal);
        var mediumIndex = report.IndexOf("medium", System.StringComparison.Ordinal);
        var smallIndex = report.IndexOf("small", System.StringComparison.Ordinal);
        Assert.IsTrue(bigIndex < mediumIndex && mediumIndex < smallIndex, report);
    }

    [TestMethod]
    public void FormatKeyReport_TruncatesToTopNAndSaysSo()
    {
        var data = new Dictionary<string, string>();
        for (var i = 0; i < 25; i++) data[$"key{i:00}"] = new string('x', i + 1);

        var report = MomentumCheats.FormatKeyReport(data, top: 10);

        StringAssert.Contains(report, "10 of 25");
    }

    [TestMethod]
    public void FormatKeyReport_EmptyState_SaysSoRatherThanRenderingAnEmptyBlock()
    {
        var report = MomentumCheats.FormatKeyReport(new Dictionary<string, string>(), top: 10);

        StringAssert.Contains(report, "no keys");
    }

    /// <summary>A null value in the dict must count as 0 bytes, not throw.</summary>
    [TestMethod]
    public void FormatKeyReport_NullValue_CountsAsZeroBytes()
    {
        var data = new Dictionary<string, string> { ["nullish"] = null, ["real"] = "abc" };

        var report = MomentumCheats.FormatKeyReport(data, top: 10);

        StringAssert.Contains(report, "nullish");
        StringAssert.Contains(report, "real");
    }
}
