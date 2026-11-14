using System;
using System.IO;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.CrashReport.BugReport;

namespace TAOM.Tests.Features.CrashReport;

/// <summary>
/// The player's bug report (BugReportFiles, BugReportSources): which files go into the zip and when the previous session
/// counts as ended unexpectedly. The pure half needs no disk; the sources half builds the real folders in a temp
/// directory: the TAOM Logs folder and the engine's "Mount and Blade II Bannerlord" folder with logs\ and crashes\
/// (TaleWorlds.Library.Watchdog, v1.5.7: the dump folder is the logs folder's sibling).
/// </summary>
[TestClass]
public class BugReportFilesTests
{
    private string _root = string.Empty;
    private string _logs = string.Empty;
    private string _engine = string.Empty;

    [TestInitialize]
    public void Setup()
    {
        _root = Path.Combine(Path.GetTempPath(), "TAOM_BugReport_" + Path.GetRandomFileName());
        _logs = Path.Combine(_root, "Logs");
        _engine = Path.Combine(_root, "Mount and Blade II Bannerlord");
        Directory.CreateDirectory(_logs);
        Directory.CreateDirectory(Path.Combine(_engine, "logs"));
    }

    [TestCleanup]
    public void Cleanup()
    {
        try { Directory.Delete(_root, true); } catch { }
    }

    // ---- unclean end (pure) ----

    [TestMethod]
    public void EndedUnexpectedly_TailWithTheCleanUnloadLine_ReturnsFalse()
    {
        var tail = "[2026-11-13 23:59:01] [INFO] [Session] summary reason=exit\n" +
                   "[2026-11-13 23:59:01] [INFO] [Shutdown] clean module unload\n";

        Assert.IsFalse(BugReportFiles.EndedUnexpectedly(tail));
    }

    [TestMethod]
    public void EndedUnexpectedly_TailThatJustStops_ReturnsTrue()
    {
        var tail = "[2026-11-13 23:12:40] [INFO] [BattleLoad] seq=4 phase=MissionInitializeDone\n";

        Assert.IsTrue(BugReportFiles.EndedUnexpectedly(tail));
    }

    [TestMethod]
    public void EndedUnexpectedly_EmptyOrMissingTail_ReturnsFalse()
    {
        Assert.IsFalse(BugReportFiles.EndedUnexpectedly(null));
        Assert.IsFalse(BugReportFiles.EndedUnexpectedly(string.Empty));
    }

    // ---- file names (pure) ----

    [TestMethod]
    public void IsMainRglLog_MainLog_True_ErrorsLogAndOthers_False()
    {
        Assert.IsTrue(BugReportFiles.IsMainRglLog("rgl_log_5256.txt"));
        Assert.IsTrue(BugReportFiles.IsMainRglLog("RGL_LOG_5256.TXT"));
        Assert.IsFalse(BugReportFiles.IsMainRglLog("rgl_log_errors_5256.txt"));
        Assert.IsFalse(BugReportFiles.IsMainRglLog("rgl_log_5256.log"));
        Assert.IsFalse(BugReportFiles.IsMainRglLog("launcher_log_5256.txt"));
    }

    [TestMethod]
    public void SessionStartUtc_TaomLogName_ReadsTheLocalStartTime()
    {
        var expected = new DateTime(2026, 11, 13, 23, 12, 5, DateTimeKind.Local).ToUniversalTime();

        Assert.AreEqual(expected, BugReportFiles.SessionStartUtc("taom_debug_2026-11-13_23-12-05.log"));
        Assert.IsNull(BugReportFiles.SessionStartUtc("taom_debug.log"));
        Assert.IsNull(BugReportFiles.SessionStartUtc("taom_crash_20261113_231205_deadbeef.zip"));
    }

    [TestMethod]
    public void IsFromSession_CrashBeforeTheSessionStarted_False_DuringOrAfter_True_NoStart_False()
    {
        var start = new DateTime(2026, 11, 13, 22, 0, 0, DateTimeKind.Utc);

        Assert.IsFalse(BugReportFiles.IsFromSession(start.AddMinutes(-1), start));
        Assert.IsTrue(BugReportFiles.IsFromSession(start, start));
        Assert.IsTrue(BugReportFiles.IsFromSession(start.AddHours(2), start));
        Assert.IsFalse(BugReportFiles.IsFromSession(start.AddHours(2), null));
    }

    // ---- previous TAOM log (disk) ----

    [TestMethod]
    public void PreviousTaomLog_SeveralLogs_ReturnsTheNewestThatIsNotThisSessions()
    {
        var older = Log("taom_debug_2026-11-12_20-00-00.log", hoursAgo: 30);
        var previous = Log("taom_debug_2026-11-13_23-12-05.log", hoursAgo: 5);
        var current = Log("taom_debug_2026-11-14_10-00-00.log", hoursAgo: 0);
        Log("taom_crash_20261114_100000_deadbeef.zip", hoursAgo: 0);

        Assert.AreEqual(previous, BugReportSources.PreviousTaomLog(_logs, Path.Combine("Logs", Path.GetFileName(current))));
        Assert.AreNotEqual(older, BugReportSources.PreviousTaomLog(_logs, current));
    }

    [TestMethod]
    public void PreviousTaomLog_OnlyThisSessionsLog_ReturnsNull()
    {
        var current = Log("taom_debug_2026-11-14_10-00-00.log", hoursAgo: 0);

        Assert.IsNull(BugReportSources.PreviousTaomLog(_logs, current));
    }

    // ---- what goes into the zip (disk) ----

    [TestMethod]
    public void Collect_PreviousSession_AddsItsTaomLogAndTheSecondNewestMainRglLog()
    {
        Log("taom_debug_2026-11-13_23-12-05.log", hoursAgo: 5);
        var current = Log("taom_debug_2026-11-14_10-00-00.log", hoursAgo: 0);
        Rgl("rgl_log_100.txt", hoursAgo: 5);
        Rgl("rgl_log_errors_100.txt", hoursAgo: 4);
        Rgl("rgl_log_200.txt", hoursAgo: 0);
        Rgl("rgl_log_errors_200.txt", hoursAgo: 0);

        var sources = BugReportSources.Collect(_logs, current, new[] { _engine });

        Assert.AreEqual(Path.Combine(_logs, "taom_debug_2026-11-13_23-12-05.log"), Source(sources, "taom_debug_previous.log"));
        Assert.AreEqual(Path.Combine(_engine, "logs", "rgl_log_100.txt"), Source(sources, "rgl_log_previous.txt"));
        Assert.IsFalse(sources.Files.Any(f => f.SourcePath.Contains("rgl_log_errors_")), "the errors log is not the main log");
    }

    [TestMethod]
    public void Collect_CrashFolderFromThePreviousSession_AddsItsFiles()
    {
        Log("taom_debug_2026-11-13_23-12-05.log", hoursAgo: 5);
        var current = Log("taom_debug_2026-11-14_10-00-00.log", hoursAgo: 0);
        var start = BugReportFiles.SessionStartUtc("taom_debug_2026-11-13_23-12-05.log")!.Value;
        Crash("old", start.AddDays(-2), ("dump.dmp", 10));
        Crash("new", start.AddHours(1), ("dump.dmp", 10), ("crash_tags.txt", 5));

        var sources = BugReportSources.Collect(_logs, current, new[] { _engine });

        Assert.AreEqual(Path.Combine(_engine, "crashes", "new", "dump.dmp"), Source(sources, "crash/dump.dmp"));
        Assert.AreEqual(Path.Combine(_engine, "crashes", "new", "crash_tags.txt"), Source(sources, "crash/crash_tags.txt"));
        Assert.IsFalse(sources.Files.Any(f => f.SourcePath.Contains(Path.Combine("crashes", "old"))));
    }

    [TestMethod]
    public void Collect_CrashFolderOlderThanThePreviousSession_AddsNothing()
    {
        Log("taom_debug_2026-11-13_23-12-05.log", hoursAgo: 5);
        var current = Log("taom_debug_2026-11-14_10-00-00.log", hoursAgo: 0);
        var start = BugReportFiles.SessionStartUtc("taom_debug_2026-11-13_23-12-05.log")!.Value;
        Crash("old", start.AddMinutes(-5), ("dump.dmp", 10));

        var sources = BugReportSources.Collect(_logs, current, new[] { _engine });

        Assert.IsFalse(sources.Files.Any(f => f.EntryName.StartsWith("crash/")));
    }

    [TestMethod]
    public void Collect_CrashFileOverTheLimit_IsLeftOutWithANote()
    {
        Log("taom_debug_2026-11-13_23-12-05.log", hoursAgo: 5);
        var current = Log("taom_debug_2026-11-14_10-00-00.log", hoursAgo: 0);
        var start = BugReportFiles.SessionStartUtc("taom_debug_2026-11-13_23-12-05.log")!.Value;
        Crash("new", start.AddHours(1), ("dump.dmp", 10), ("crash_tags.txt", 5));

        var sources = BugReportSources.Collect(_logs, current, new[] { _engine }, maxCrashFileBytes: 8);

        Assert.IsNull(Source(sources, "crash/dump.dmp"));
        Assert.IsNotNull(Source(sources, "crash/crash_tags.txt"));
        Assert.IsTrue(sources.Notes.Any(n => n.Contains("crash/dump.dmp") && n.Contains("not included")),
            string.Join("\n", sources.Notes));
    }

    [TestMethod]
    public void Collect_Notes_NameFilesOnly_NeverAFolderPath()
    {
        Log("taom_debug_2026-11-13_23-12-05.log", hoursAgo: 5);
        var current = Log("taom_debug_2026-11-14_10-00-00.log", hoursAgo: 0);

        var sources = BugReportSources.Collect(_logs, current, new[] { _engine });

        Assert.IsTrue(sources.Notes.Any(n => n.Contains("taom_debug_2026-11-13_23-12-05.log")), string.Join("\n", sources.Notes));
        Assert.IsFalse(sources.Notes.Any(n => n.Contains(_root)), string.Join("\n", sources.Notes));
    }

    [TestMethod]
    public void Collect_PreviousLogEndedUnexpectedly_SaysSoInTheNotes()
    {
        Log("taom_debug_2026-11-13_23-12-05.log", hoursAgo: 5, text: "[INFO] [BattleLoad] seq=4\n");
        var current = Log("taom_debug_2026-11-14_10-00-00.log", hoursAgo: 0);

        var sources = BugReportSources.Collect(_logs, current, new[] { _engine });

        Assert.IsTrue(sources.Notes.Any(n => n.Contains("clean exit: no")), string.Join("\n", sources.Notes));
    }

    // ---- next-start offer (BugReportStartup.PreviousUncleanLog) ----

    [TestMethod]
    public void PreviousUncleanLog_PreviousSessionStoppedWithoutTheUnloadLine_ReturnsItsLog()
    {
        var previous = Log("taom_debug_2026-11-13_23-12-05.log", hoursAgo: 5, text: "[INFO] [BattleLoad] seq=4\n");
        var current = Log("taom_debug_2026-11-14_10-00-00.log", hoursAgo: 0);

        Assert.AreEqual(previous, BugReportStartup.PreviousUncleanLog(_logs, current, offerEnabled: true));
    }

    [TestMethod]
    public void PreviousUncleanLog_PreviousSessionUnloadedCleanly_ReturnsNull()
    {
        Log("taom_debug_2026-11-13_23-12-05.log", hoursAgo: 5);
        var current = Log("taom_debug_2026-11-14_10-00-00.log", hoursAgo: 0);

        Assert.IsNull(BugReportStartup.PreviousUncleanLog(_logs, current, offerEnabled: true));
    }

    [TestMethod]
    public void PreviousUncleanLog_OfferSwitchedOff_ReturnsNull()
    {
        Log("taom_debug_2026-11-13_23-12-05.log", hoursAgo: 5, text: "[INFO] [BattleLoad] seq=4\n");
        var current = Log("taom_debug_2026-11-14_10-00-00.log", hoursAgo: 0);

        Assert.IsNull(BugReportStartup.PreviousUncleanLog(_logs, current, offerEnabled: false));
    }

    [TestMethod]
    public void PreviousUncleanLog_FirstSessionEver_ReturnsNull()
    {
        var current = Log("taom_debug_2026-11-14_10-00-00.log", hoursAgo: 0);

        Assert.IsNull(BugReportStartup.PreviousUncleanLog(_logs, current, offerEnabled: true));
    }

    [TestMethod]
    public void PreviousUncleanLog_UnloadLineFarAboveALongTail_StillFindsTheCleanEnd()
    {
        // Lines written after the unload line (other modules' unload, the exit summary) must not hide it.
        var text = "[INFO] [Shutdown] clean module unload\n" + new string('x', 20_000) + "\n";
        Log("taom_debug_2026-11-13_23-12-05.log", hoursAgo: 5, text: text);
        var current = Log("taom_debug_2026-11-14_10-00-00.log", hoursAgo: 0);

        Assert.IsNull(BugReportStartup.PreviousUncleanLog(_logs, current, offerEnabled: true));
    }

    // ---- helpers ----

    private static string? Source(BugReportSources s, string entry) =>
        s.Files.FirstOrDefault(f => f.EntryName == entry)?.SourcePath;

    private string Log(string name, int hoursAgo, string text = "[INFO] [Shutdown] clean module unload\n")
    {
        var path = Path.Combine(_logs, name);
        File.WriteAllText(path, text);
        File.SetLastWriteTimeUtc(path, DateTime.UtcNow.AddHours(-hoursAgo));
        return path;
    }

    private void Rgl(string name, int hoursAgo)
    {
        var path = Path.Combine(_engine, "logs", name);
        File.WriteAllText(path, "rgl");
        File.SetLastWriteTimeUtc(path, DateTime.UtcNow.AddHours(-hoursAgo));
    }

    private void Crash(string folder, DateTime writtenUtc, params (string Name, int Bytes)[] files)
    {
        var dir = Path.Combine(_engine, "crashes", folder);
        Directory.CreateDirectory(dir);
        foreach (var (name, bytes) in files)
            File.WriteAllBytes(Path.Combine(dir, name), new byte[bytes]);
        Directory.SetLastWriteTimeUtc(dir, writtenUtc);
    }
}
