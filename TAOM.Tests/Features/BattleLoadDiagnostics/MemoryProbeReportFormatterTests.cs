using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.BattleLoadDiagnostics;
using TAOM.Features.BattleLoadDiagnostics.Domain;

namespace TAOM.Tests.Features.BattleLoadDiagnostics;

/// <summary>
/// The pure rendering half of <c>taom.print_memory</c>. Everything the probe decides about how a
/// reading is presented lives here so it is 100% testable without a live engine — the four
/// <c>TaleWorlds.Engine.Utilities</c> statics behind <see cref="IEngineMemoryStatsReader"/> are
/// native and deliberately untested (ADR-008).
///
/// Two contracts these tests exist to hold:
/// * an unread value is OMITTED or marked <c>&lt;unavailable&gt;</c>, never rendered as 0 — a
///   fabricated zero in a user-uploaded log is indistinguishable from a real reading (the same rule
///   <c>MemStats()</c> follows for its process tokens);
/// * the station label is echoed into <c>taom_debug</c>, which <c>tools/triage_battle_load.py</c>
///   parses line by line, so an unvalidated label is a log-forgery vector.
/// </summary>
[TestClass]
public class MemoryProbeReportFormatterTests
{
    private static MemorySample Sample() =>
        new MemorySample(
            privMb: 8192, wsMb: 7000, heapMb: 512,
            sysCommitUsedMb: 20000, sysCommitLimitMb: 32768,
            availPhysMb: 4096, totalPhysMb: 32768, memLoadPercent: 62);

    private static EngineMemoryStats Stats(
        string? app = "app-stats",
        string? native = "native-stats",
        int gpuCostMb = 0,
        bool gpuCostRead = false,
        string? gpuDumpPath = null) =>
        new EngineMemoryStats(app, native, gpuCostMb, gpuCostRead, gpuDumpPath);

    // ---- engine statistics blocks ------------------------------------------------------------

    // The verified-real null path: the managed wrapper returns null when the native delegate
    // reports failure, so null is an ENGINE outcome and must be reported as one.
    [TestMethod]
    public void Format_NullEngineStrings_RendersUnavailableMarkers()
    {
        var text = MemoryProbeReportFormatter.Format(Stats(app: null, native: null), Sample(), osRead: true, label: null);

        StringAssert.Contains(text, "application: <unavailable>");
        StringAssert.Contains(text, "native: <unavailable>");
    }

    [TestMethod]
    public void Format_EmptyEngineStrings_RendersUnavailableMarkers()
    {
        var text = MemoryProbeReportFormatter.Format(Stats(app: "", native: ""), Sample(), osRead: true, label: null);

        StringAssert.Contains(text, "application: <unavailable>");
        StringAssert.Contains(text, "native: <unavailable>");
    }

    // These are expected to be multi-line category breakdowns. Every line must carry the tag or a
    // grep for [MemProbe] returns the header and drops the body that actually answers the question.
    [TestMethod]
    public void Format_MultilineEngineString_IndentsEveryLine()
    {
        var text = MemoryProbeReportFormatter.Format(Stats(app: "alpha\nbeta\r\ngamma"), Sample(), osRead: true, label: null);

        StringAssert.Contains(text, "[MemProbe]   alpha");
        StringAssert.Contains(text, "[MemProbe]   beta");
        StringAssert.Contains(text, "[MemProbe]   gamma");

        // The discriminating assertion. The report itself is built with AppendLine, so it is FULL of
        // legitimate CRLFs — "contains no \r" would be wrong. What must not survive is the engine's
        // OWN \r: without the Replace("\r\n", "\n"), splitting on '\n' leaves "beta\r" as the line
        // content and AppendLine then writes "beta\r\r\n". A doubled CR is the only signature that
        // separates the normalised output from the un-normalised one.
        Assert.IsFalse(text.Contains("\r\r"),
            "an engine CR survived into the line body — splitting on '\\n' without normalising CRLF first");
    }

    [TestMethod]
    public void Format_EveryLine_CarriesTheMemProbeTag()
    {
        var text = MemoryProbeReportFormatter.Format(Stats(app: "a\nb", native: "c"), Sample(), osRead: true, label: "map");

        foreach (var line in text.Split('\n'))
            Assert.IsTrue(line.StartsWith(MemoryProbeReportFormatter.Tag),
                $"untagged line in the report: '{line}'");
    }

    // ---- the OS block: never a fabricated zero -----------------------------------------------

    [TestMethod]
    public void Format_OsReadFailed_OmitsProcessTokens_NeverFabricatesZero()
    {
        var text = MemoryProbeReportFormatter.Format(Stats(), null, osRead: false, label: null);

        StringAssert.Contains(text, "os: <unavailable>");
        Assert.IsFalse(text.Contains("privMB="), $"a failed process read must omit its tokens: {text}");
        Assert.IsFalse(text.Contains("wsMB="), $"a failed process read must omit its tokens: {text}");
    }

    [TestMethod]
    public void Format_WithOsSample_IncludesEveryProcessToken()
    {
        var text = MemoryProbeReportFormatter.Format(Stats(), Sample(), osRead: true, label: null);

        StringAssert.Contains(text, "privMB=8192");
        StringAssert.Contains(text, "wsMB=7000");
        StringAssert.Contains(text, "heapMB=512");
        StringAssert.Contains(text, "sysCommitUsedMB=20000");
        StringAssert.Contains(text, "sysCommitLimitMB=32768");
        StringAssert.Contains(text, "availPhysMB=4096");
        StringAssert.Contains(text, "memLoad=62%");
    }

    // ---- GPU ---------------------------------------------------------------------------------

    [TestMethod]
    public void Format_GpuCostRead_IncludesEstimatedCost()
    {
        var text = MemoryProbeReportFormatter.Format(Stats(gpuCostMb: 3457, gpuCostRead: true), Sample(), osRead: true, label: null);

        StringAssert.Contains(text, "estimatedCostMB=3457");
    }

    [TestMethod]
    public void Format_GpuCostUnread_OmitsTheNumber()
    {
        var text = MemoryProbeReportFormatter.Format(Stats(gpuCostMb: 0, gpuCostRead: false), Sample(), osRead: true, label: null);

        StringAssert.Contains(text, "gpu: <unavailable>");
        Assert.IsFalse(text.Contains("estimatedCostMB="),
            $"an unread GPU cost must be omitted, never reported as 0: {text}");
    }

    [TestMethod]
    public void Format_WithGpuDumpPath_NamesTheAbsolutePath()
    {
        const string path = @"C:\Users\x\Documents\Mount and Blade II Bannerlord\Logs\taom_gpu_memory.txt";

        var text = MemoryProbeReportFormatter.Format(Stats(gpuDumpPath: path), Sample(), osRead: true, label: null);

        StringAssert.Contains(text, "gpu dump written: " + path);
    }

    [TestMethod]
    public void Format_NoGpuDumpPath_ClaimsNoDump()
    {
        var text = MemoryProbeReportFormatter.Format(Stats(gpuDumpPath: null), Sample(), osRead: true, label: null);

        Assert.IsFalse(text.Contains("gpu dump written"),
            $"no dump was requested, so no file may be claimed: {text}");
    }

    // ---- label validation (the log-forgery guard) ---------------------------------------------

    [TestMethod]
    public void Format_ValidLabel_AppearsInHeader()
    {
        Assert.IsTrue(MemoryProbeReportFormatter.IsValidLabel("B-battle-2min"));

        var text = MemoryProbeReportFormatter.Format(Stats(), Sample(), osRead: true, label: "B-battle-2min");

        StringAssert.Contains(text, "reading 'B-battle-2min'");
    }

    [TestMethod]
    public void Format_NoLabel_RendersBareHeader()
    {
        var text = MemoryProbeReportFormatter.Format(Stats(), Sample(), osRead: true, label: null);

        StringAssert.Contains(text, "[MemProbe] reading");
        Assert.IsFalse(text.Contains("reading '"), "no label means no quoted label in the header");
    }

    /// <summary>
    /// The label is echoed into taom_debug and tools/triage_battle_load.py parses that file line by
    /// line. A newline in a label would let a pasted string inject a fake [BattleLoad] phase line
    /// into the log the measurement runbook reads.
    /// </summary>
    [TestMethod]
    public void Format_LabelWithControlCharacters_IsRejected()
    {
        const string forged = "step0\n[BattleLoad] seq=1 t=+0ms phase=BattlePlayable scene='x' agents=0";

        Assert.IsFalse(MemoryProbeReportFormatter.IsValidLabel(forged));

        var text = MemoryProbeReportFormatter.Format(Stats(), Sample(), osRead: true, label: forged);

        Assert.IsFalse(text.Contains("BattlePlayable"),
            $"a rejected label must never reach the output — that is the whole point of rejecting it: {text}");
        StringAssert.Contains(text, "rejected the label");
    }

    // Boundary pin: the accepted/rejected pair either side of the documented limit, so an off-by-one
    // in the length check fails a test instead of shipping.
    [TestMethod]
    public void IsValidLabel_AtTheLengthLimit_IsAccepted_OneOverIsRejected()
    {
        Assert.IsTrue(MemoryProbeReportFormatter.IsValidLabel(new string('a', MemoryProbeReportFormatter.MaxLabelLength)));
        Assert.IsFalse(MemoryProbeReportFormatter.IsValidLabel(new string('a', MemoryProbeReportFormatter.MaxLabelLength + 1)));
    }

    [TestMethod]
    public void Format_LabelTooLong_IsRejected()
    {
        var tooLong = new string('a', MemoryProbeReportFormatter.MaxLabelLength + 1);

        var text = MemoryProbeReportFormatter.Format(Stats(), Sample(), osRead: true, label: tooLong);

        StringAssert.Contains(text, "rejected the label");
        Assert.IsFalse(text.Contains("privMB="), "a rejected label must not produce a reading at all");
    }

    // Documents the deliberate semantics: absent is not invalid. `taom.print_memory` with no args
    // must produce a reading, not a usage error.
    [TestMethod]
    public void IsValidLabel_NullOrEmpty_MeansNoLabel_AndIsAccepted()
    {
        Assert.IsTrue(MemoryProbeReportFormatter.IsValidLabel(null));
        Assert.IsTrue(MemoryProbeReportFormatter.IsValidLabel(string.Empty));
    }

    // Whitespace is NOT a label — it would render an empty-looking quoted token in the log.
    [TestMethod]
    public void IsValidLabel_Whitespace_IsRejected()
    {
        Assert.IsFalse(MemoryProbeReportFormatter.IsValidLabel("   "));
        Assert.IsFalse(MemoryProbeReportFormatter.IsValidLabel("a b"));
    }

    [TestMethod]
    public void IsValidLabel_AllowedPunctuation_IsAccepted()
    {
        Assert.IsTrue(MemoryProbeReportFormatter.IsValidLabel("A_b.c:d-e9"));
    }

    [TestMethod]
    public void IsValidLabel_BracketsAndQuotes_AreRejected()
    {
        Assert.IsFalse(MemoryProbeReportFormatter.IsValidLabel("[MemSample]"));
        Assert.IsFalse(MemoryProbeReportFormatter.IsValidLabel("scene='x'"));
    }
}
