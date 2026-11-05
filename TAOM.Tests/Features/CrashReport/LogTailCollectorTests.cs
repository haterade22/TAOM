using System;
using System.IO;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.CrashReport.Collectors;

namespace TAOM.Tests.Features.CrashReport;

/// <summary>
/// Pins the diag.log limb of the crash bundle's log collection.
///
/// PatchShield writes the MissingMethod / MissingField / TypeLoad trinity that means "this build was
/// compiled against an older Bannerlord" to TAOM.Dependencies/diag.log, which is a different file
/// from the taom_debug log players send. Before 2026-08-19 the bundle collected the debug log and
/// the RGL log and not that one, so the single most diagnostic artifact for an engine mismatch was
/// the one artifact never uploaded.
/// </summary>
[TestClass]
public class LogTailCollectorTests
{
    private string _dir = string.Empty;

    [TestInitialize]
    public void Setup()
    {
        _dir = Path.Combine(Path.GetTempPath(), "TAOM_LogTail_" + Path.GetRandomFileName());
        Directory.CreateDirectory(_dir);
    }

    [TestCleanup]
    public void Cleanup()
    {
        try { Directory.Delete(_dir, true); } catch { }
    }

    private string WriteFile(string name, params string[] lines)
    {
        var p = Path.Combine(_dir, name);
        File.WriteAllLines(p, lines);
        return p;
    }

    [TestMethod]
    public void Collect_WithDiagLogPresent_CapturesItsPathAndTail()
    {
        var taom = WriteFile("taom_debug.log", "debug line");
        var diag = WriteFile("diag.log", "PatchShield swallowed MissingMethodException from a patch on Foo.Bar");

        var snapshot = new LogTailCollector(() => taom, () => diag).Collect();

        Assert.AreEqual(diag, snapshot.DiagLogPath);
        StringAssert.Contains(string.Join("|", snapshot.DiagLogTail),
            "MissingMethodException",
            "the engine-mismatch evidence is the whole reason this file is collected");
    }

    [TestMethod]
    public void Collect_DiagLogMissingOrUnset_DegradesQuietly()
    {
        var taom = WriteFile("taom_debug.log", "debug line");

        // Two real shapes: TAOM.Dependencies resolved no module directory (provider returns empty),
        // and the module is there but has never written a diag line (file absent).
        foreach (var missing in new Func<string?>[] { () => null, () => string.Empty, () => Path.Combine(_dir, "nope.log") })
        {
            var snapshot = new LogTailCollector(() => taom, missing).Collect();
            Assert.AreEqual(0, snapshot.DiagLogTail.Count);
            StringAssert.Contains(string.Join("|", snapshot.TaomDebugLogTail), "debug line",
                "a missing diag.log must not cost us the debug log");
        }
    }

    [TestMethod]
    public void Collect_DiagProviderThrows_StillReturnsTheOtherLogs()
    {
        // RuntimeLog.Path touches the module directory, and this runs while the process is already
        // crashing. A throw here must not take the rest of the bundle down with it.
        var taom = WriteFile("taom_debug.log", "debug line");

        var snapshot = new LogTailCollector(() => taom, () => throw new InvalidOperationException("boom")).Collect();

        Assert.IsNull(snapshot.DiagLogPath);
        StringAssert.Contains(string.Join("|", snapshot.TaomDebugLogTail), "debug line");
    }

    [TestMethod]
    public void Collect_WithNoDiagProvider_KeepsTheOriginalTwoLogBehaviour()
    {
        var taom = WriteFile("taom_debug.log", "debug line");

        var snapshot = new LogTailCollector(() => taom).Collect();

        Assert.IsNull(snapshot.DiagLogPath);
        Assert.AreEqual(0, snapshot.DiagLogTail.Count);
    }

    [TestMethod]
    public void Collect_DiagLogLongerThanTheTail_KeepsTheNEWESTLines()
    {
        // PatchShield writes its SESSION SUMMARY last, so a head-biased read would drop the one line
        // that totals the swallowed exceptions.
        var taom = WriteFile("taom_debug.log", "debug line");
        var diag = WriteFile("diag.log", Enumerable.Range(1, 50).Select(i => "line " + i).ToArray());

        var snapshot = new LogTailCollector(() => taom, () => diag).Collect(tailLines: 5);

        Assert.AreEqual(5, snapshot.DiagLogTail.Count);
        CollectionAssert.Contains(snapshot.DiagLogTail.ToList(), "line 50");
        CollectionAssert.DoesNotContain(snapshot.DiagLogTail.ToList(), "line 1");
    }
}
