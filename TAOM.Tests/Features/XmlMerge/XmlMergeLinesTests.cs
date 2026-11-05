using System.Globalization;
using System.Threading;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.XmlMerge;

namespace TAOM.Tests.Features.XmlMerge;

/// <summary>
/// Pins every [XmlMerge] line literally (plan 042, Design "The log lines"; the maintainer's D6 logging rule). A line
/// that changes shape breaks whoever greps taom_debug.log for it, so a format change has to change this file too.
/// </summary>
[TestClass]
public class XmlMergeLinesTests
{
    [TestMethod]
    public void Ready_Default_MatchesThePinnedFormat()
    {
        Assert.AreEqual(
            "[XmlMerge] fast path ready: engine bindings resolved (CreateDocumentFromXmlFile, MergeElements, ToXDocument, ToXmlDocument); " +
            "applies to validated merges (skipValidation=false); xslt cache on",
            XmlMergeLines.Ready());
    }

    [TestMethod]
    public void Off_WithProblem_MatchesThePinnedFormat()
    {
        Assert.AreEqual(
            "[XmlMerge] fast path off: MBObjectManager.CreateDocumentFromXmlFile(string, string, bool) not found; " +
            "every module XML merge runs the engine's own code",
            XmlMergeLines.Off("MBObjectManager.CreateDocumentFromXmlFile(string, string, bool) not found"));
    }

    [TestMethod]
    public void Fast_AllFields_MatchesThePinnedFormat()
    {
        Assert.AreEqual(
            "[XmlMerge] type=NPCCharacters files=56 xslt=1 ms=1774 path=fast load_ms=361 xslt_ms=1246 merge_ms=51 " +
            "xslt_compiles=0 xslt_cache_hits=1",
            XmlMergeLines.Fast("NPCCharacters", 56, 1, 1774, 361, 1246, 51, 0, 1));
    }

    [TestMethod]
    public void Fast_FractionalMilliseconds_RoundsUnderAnyCulture()
    {
        var saved = Thread.CurrentThread.CurrentCulture;
        try
        {
            Thread.CurrentThread.CurrentCulture = new CultureInfo("de-DE");

            var line = XmlMergeLines.Fast("NPCCharacters", 56, 1, 1773.6, 360.5, 1245.5, 50.4, 0, 1);

            Assert.AreEqual(
                "[XmlMerge] type=NPCCharacters files=56 xslt=1 ms=1774 path=fast load_ms=361 xslt_ms=1246 merge_ms=50 " +
                "xslt_compiles=0 xslt_cache_hits=1",
                line);
        }
        finally
        {
            Thread.CurrentThread.CurrentCulture = saved;
        }
    }

    [TestMethod]
    public void Vanilla_SkipValidation_MatchesThePinnedFormat()
    {
        Assert.AreEqual(
            "[XmlMerge] type=Monsters files=4 xslt=0 ms=12 path=vanilla reason=skip-validation result=ok",
            XmlMergeLines.Vanilla("Monsters", 4, 0, 12, "skip-validation", "ok"));
    }

    [TestMethod]
    public void StandAside_TwoPatches_SortsAndJoinsThem()
    {
        Assert.AreEqual(
            "[XmlMerge] fast path stands aside: com.other prefix MBObjectManager.CreateMergedXmlFile, " +
            "com.zed postfix MBObjectManager.ToXDocument; merges run the engine's own code while those patches are present",
            XmlMergeLines.StandAside(new[]
            {
                "com.zed postfix MBObjectManager.ToXDocument",
                "com.other prefix MBObjectManager.CreateMergedXmlFile",
            }));
    }

    [TestMethod]
    public void FastFailed_WithMessage_MatchesThePinnedFormat()
    {
        Assert.AreEqual(
            "[XmlMerge] fast path failed on type=Items: InvalidOperationException: boom; this merge re-runs the engine's own code",
            XmlMergeLines.FastFailed("Items", "InvalidOperationException", "boom"));
    }

    [TestMethod]
    public void FastFailedDetail_WithExceptionText_KeepsTheTextVerbatim()
    {
        // Exception.ToString() is several lines (the stack); the entry keeps them, as the other exception dumps in the log do.
        const string text = "System.InvalidOperationException: boom\r\n   at A.B()\r\n   at C.D()";

        Assert.AreEqual(
            "[XmlMerge] fast path failure detail on type=Items: System.InvalidOperationException: boom\r\n   at A.B()\r\n   at C.D()",
            XmlMergeLines.FastFailedDetail("Items", text));
    }

    [TestMethod]
    public void Disabled_WithType_MatchesThePinnedFormat()
    {
        Assert.AreEqual(
            "[XmlMerge] fast path disabled for this session: it threw InvalidOperationException on type=Items where the engine's own merge succeeded",
            XmlMergeLines.Disabled("Items", "InvalidOperationException"));
    }

    [TestMethod]
    public void Summary_AllFields_MatchesThePinnedFormat()
    {
        Assert.AreEqual(
            "[XmlMerge] summary game=Campaign merges=32 fast=28 vanilla=4 files=329 ms=6100 max_ms=1774 max_type=NPCCharacters " +
            "xslt_compiles=9 xslt_cache_hits=0 fast_path=on",
            XmlMergeLines.Summary("Campaign", 32, 28, 4, 329, 6100, 1774, "NPCCharacters", 9, 0, "on"));
    }
}
