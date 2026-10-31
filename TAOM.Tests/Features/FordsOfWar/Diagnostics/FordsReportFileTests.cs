using System;
using System.IO;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.FordsOfWar.Diagnostics;

namespace TAOM.Tests.Features.FordsOfWar.Diagnostics;

[TestClass]
public class FordsReportFileTests
{
    private string _dir = "";

    [TestInitialize]
    public void Init()
    {
        _dir = Path.Combine(Path.GetTempPath(), "fords_report_tests_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_dir);
    }

    [TestCleanup]
    public void Cleanup()
    {
        try { Directory.Delete(_dir, true); } catch (IOException) { }
    }

    [TestMethod]
    public void Path_UsesTheLogStampAndTheRunNumber()
    {
        var file = FordsReportFile.For(Path.Combine(_dir, "taom_debug_2026-10-31_12-30-45.log"));

        Assert.IsNotNull(file);
        Assert.AreEqual(Path.Combine(_dir, "fords_report_2026-10-31_12-30-45_run3.json"), file!.PathFor(3));
    }

    [TestMethod]
    public void Path_OfANonLogName_UsesTheCurrentTime()
    {
        var file = FordsReportFile.For(Path.Combine(_dir, "other.txt"));

        StringAssert.Matches(Path.GetFileName(file!.PathFor(1)), new System.Text.RegularExpressions.Regex(@"^fords_report_\d{4}-\d{2}-\d{2}_\d{2}-\d{2}-\d{2}_run1\.json$"));
    }

    [TestMethod]
    public void For_NoPath_IsNull()
    {
        Assert.IsNull(FordsReportFile.For(null));
        Assert.IsNull(FordsReportFile.For(""));
    }

    [TestMethod]
    public void Write_GoesThroughTmpThenReplace()
    {
        var file = FordsReportFile.For(Path.Combine(_dir, "taom_debug_2026-10-31_12-30-45.log"))!;

        Assert.IsTrue(file.TryWrite(1, "{\"a\":1}", out var e1), e1);
        Assert.IsTrue(file.TryWrite(1, "{\"a\":2}", out var e2), e2);

        Assert.AreEqual("{\"a\":2}", File.ReadAllText(file.PathFor(1)));
        Assert.AreEqual(0, Directory.GetFiles(_dir, "*.tmp").Length, "the temp file is gone after the move or replace");
    }

    [TestMethod]
    public void Write_NeverThrows_ReturnsTheError()
    {
        var blocker = Path.Combine(_dir, "blocker");
        File.WriteAllText(blocker, "x");
        var file = FordsReportFile.For(Path.Combine(blocker, "taom_debug_2026-10-31_12-30-45.log"))!;

        var ok = file.TryWrite(1, "{}", out var error);

        Assert.IsFalse(ok);
        Assert.IsFalse(string.IsNullOrEmpty(error));
    }

    [TestMethod]
    public void Prune_KeepsTheNewest20()
    {
        for (var i = 0; i < 25; i++)
        {
            var old = Path.Combine(_dir, $"fords_report_old_{i:00}.json");
            File.WriteAllText(old, "{}");
            File.SetLastWriteTimeUtc(old, DateTime.UtcNow.AddDays(-30 + i));
        }

        var file = FordsReportFile.For(Path.Combine(_dir, "taom_debug_2026-10-31_12-30-45.log"))!;
        Assert.IsTrue(file.TryWrite(1, "{}", out _));

        var left = Directory.GetFiles(_dir, "fords_report_*.json");
        Assert.AreEqual(FordsReportFile.Retained, left.Length);
        Assert.IsTrue(left.Contains(file.PathFor(1)), "the report just written is among the newest");
        Assert.IsFalse(File.Exists(Path.Combine(_dir, "fords_report_old_00.json")));
        Assert.IsTrue(File.Exists(Path.Combine(_dir, "fords_report_old_24.json")));
    }
}
