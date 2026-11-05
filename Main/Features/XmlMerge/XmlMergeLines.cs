using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace TAOM.Features.XmlMerge;

/// <summary>
/// Every [XmlMerge] line taom_debug.log carries (docs/features/xml-merge-fast-path.md, "Log lines"). One method per
/// line so XmlMergeLinesTests can pin each format literally. Numbers are written with the invariant culture and
/// milliseconds are rounded to whole numbers, half away from zero, so a German or French Windows writes the same line.
/// </summary>
internal static class XmlMergeLines
{
    private const string Prefix = "[XmlMerge] ";

    internal static string Ready() =>
        Prefix + "fast path ready: engine bindings resolved (CreateDocumentFromXmlFile, MergeElements, ToXDocument, " +
        "ToXmlDocument); applies to validated merges (skipValidation=false); xslt cache on";

    internal static string Off(string problem) =>
        Prefix + "fast path off: " + problem + "; every module XML merge runs the engine's own code";

    internal static string Fast(string type, int files, int xslts, double ms, double loadMs, double xsltMs, double mergeMs,
                                long xsltCompiles, long xsltCacheHits) =>
        Prefix + "type=" + type + " files=" + Int(files) + " xslt=" + Int(xslts) + " ms=" + Ms(ms) + " path=fast" +
        " load_ms=" + Ms(loadMs) + " xslt_ms=" + Ms(xsltMs) + " merge_ms=" + Ms(mergeMs) +
        " xslt_compiles=" + Int(xsltCompiles) + " xslt_cache_hits=" + Int(xsltCacheHits);

    internal static string Vanilla(string type, int files, int xslts, double ms, string reason, string result) =>
        Prefix + "type=" + type + " files=" + Int(files) + " xslt=" + Int(xslts) + " ms=" + Ms(ms) + " path=vanilla" +
        " reason=" + reason + " result=" + result;

    internal static string StandAside(IReadOnlyList<string> patches) =>
        Prefix + "fast path stands aside: " + string.Join(", ", patches.OrderBy(p => p, StringComparer.Ordinal)) +
        "; merges run the engine's own code while those patches are present";

    internal static string FastFailed(string type, string exceptionType, string message) =>
        Prefix + "fast path failed on type=" + type + ": " + exceptionType + ": " + message +
        "; this merge re-runs the engine's own code";

    /// <summary>
    /// The DEBUG line every fast-path fallback adds, after <see cref="FastFailed"/> when that exception type's WARNING is
    /// written (the WARNING is once per type, this line is not): the exception's own <c>ToString()</c>, stack and inner
    /// exceptions included, kept verbatim and so several lines long, as the log's other exception dumps are.
    /// </summary>
    internal static string FastFailedDetail(string type, string exceptionText) =>
        Prefix + "fast path failure detail on type=" + type + ": " + exceptionText;

    internal static string Disabled(string type, string exceptionType) =>
        Prefix + "fast path disabled for this session: it threw " + exceptionType + " on type=" + type +
        " where the engine's own merge succeeded";

    internal static string Summary(string gameType, int merges, int fast, int vanilla, int files, double ms, double maxMs,
                                   string maxType, long xsltCompiles, long xsltCacheHits, string fastPathState) =>
        Prefix + "summary game=" + gameType + " merges=" + Int(merges) + " fast=" + Int(fast) + " vanilla=" + Int(vanilla) +
        " files=" + Int(files) + " ms=" + Ms(ms) + " max_ms=" + Ms(maxMs) + " max_type=" + maxType +
        " xslt_compiles=" + Int(xsltCompiles) + " xslt_cache_hits=" + Int(xsltCacheHits) + " fast_path=" + fastPathState;

    private static string Int(long value) => value.ToString(CultureInfo.InvariantCulture);

    private static string Ms(double value) =>
        Math.Round(value, MidpointRounding.AwayFromZero).ToString("0", CultureInfo.InvariantCulture);
}
