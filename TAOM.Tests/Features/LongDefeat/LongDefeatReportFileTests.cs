using System;
using System.IO;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.LongDefeat;

namespace TAOM.Tests.Features.LongDefeat;

[TestClass]
public class LongDefeatReportFileTests
{
    private string _dir = null!;

    [TestInitialize]
    public void Init()
    {
        _dir = Path.Combine(Path.GetTempPath(), "taom_ld_report_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_dir);
    }

    [TestCleanup]
    public void Clean() => Directory.Delete(_dir, true);

    private string LogPath(string stamp = "2026-11-04_10-11-12") => Path.Combine(_dir, "taom_debug_" + stamp + ".log");

    [TestMethod]
    public void For_WithNoLogPath_IsNull()
    {
        Assert.IsNull(LongDefeatReportFile.For(null));
        Assert.IsNull(LongDefeatReportFile.For("  "));
    }

    [TestMethod]
    public void PathFor_NamesTheLogStampAndTheMission()
    {
        var file = LongDefeatReportFile.For(LogPath())!;

        Assert.AreEqual(Path.Combine(_dir, "long_defeat_report_2026-11-04_10-11-12_ld3.json"), file.PathFor("ld3"));
    }

    [TestMethod]
    public void PathFor_AMissionKeyCannotLeaveTheFolder()
    {
        var file = LongDefeatReportFile.For(LogPath())!;

        Assert.AreEqual(_dir, Path.GetDirectoryName(file.PathFor("../../x/y")));
    }

    [TestMethod]
    public void TryWrite_WritesTheDocument_AndLeavesNoTempFile()
    {
        var file = LongDefeatReportFile.For(LogPath())!;

        Assert.IsTrue(file.TryWrite("ld1", "{\"a\":1}", out var error), error);

        Assert.AreEqual("{\"a\":1}", File.ReadAllText(file.PathFor("ld1")));
        Assert.IsFalse(File.Exists(file.PathFor("ld1") + ".tmp"));
    }

    [TestMethod]
    public void TryWrite_WhenTheTempWriteFails_ReturnsFalse_AndLeavesTheOldReport()
    {
        var file = LongDefeatReportFile.For(LogPath())!;
        file.TryWrite("ld1", "first", out _);
        Directory.CreateDirectory(file.PathFor("ld1") + ".tmp");

        Assert.IsFalse(file.TryWrite("ld1", "second", out var error));

        Assert.IsFalse(string.IsNullOrEmpty(error));
        Assert.AreEqual("first", File.ReadAllText(file.PathFor("ld1")));
    }

    [TestMethod]
    public void TryWrite_AgainForTheSameMission_ReplacesIt()
    {
        var file = LongDefeatReportFile.For(LogPath())!;
        file.TryWrite("ld1", "first", out _);

        Assert.IsTrue(file.TryWrite("ld1", "second", out _));

        Assert.AreEqual("second", File.ReadAllText(file.PathFor("ld1")));
    }

    [TestMethod]
    public void TryWrite_KeepsOnlyTheNewestRetained()
    {
        var file = LongDefeatReportFile.For(LogPath())!;
        for (var i = 0; i < LongDefeatReportFile.Retained + 3; i++)
        {
            var path = Path.Combine(_dir, $"long_defeat_report_old{i:D2}.json");
            File.WriteAllText(path, "old");
            File.SetLastWriteTimeUtc(path, new DateTime(2026, 1, 1, 0, 0, i, DateTimeKind.Utc));
        }

        file.TryWrite("ld9", "new", out _);

        var left = Directory.GetFiles(_dir, "long_defeat_report_*.json");
        Assert.AreEqual(LongDefeatReportFile.Retained, left.Length);
        Assert.IsTrue(left.Contains(file.PathFor("ld9")));
    }

    [TestMethod]
    public void TryWrite_ToAnUnwritableFolder_ReturnsFalseAndNeverThrows()
    {
        var blocker = Path.Combine(_dir, "blocker");
        File.WriteAllText(blocker, "x");
        var file = LongDefeatReportFile.For(Path.Combine(blocker, "taom_debug_2026-11-04_10-11-12.log"))!;

        Assert.IsFalse(file.TryWrite("ld1", "{}", out var error));
        Assert.IsFalse(string.IsNullOrEmpty(error));
    }
}
