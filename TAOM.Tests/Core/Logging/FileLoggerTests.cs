using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Core.Logging;
using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace TAOM.Tests.Core.Logging;

[TestClass]
public class FileLoggerTests
{
    private string _testDir;
    private string _originalDir;

    // Reads the log WITHOUT disposing the logger. Dispose() drains the queue, so any assertion
    // made after Dispose() passes against an async implementation too -- it cannot see whether a
    // line was durable at write time. Every durability test below must use this instead.
    // FileShare.ReadWrite is required because the logger holds the file open (see the same pattern
    // in Main/Features/CrashReport/Collectors/LogTailCollector.cs).
    private string ReadLogWithoutDispose()
    {
        var logFile = Directory.GetFiles(Path.Combine(_testDir, "Logs")).First();
        using var fs = new FileStream(logFile, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        using var reader = new StreamReader(fs);
        return reader.ReadToEnd();
    }

    [TestInitialize]
    public void Setup()
    {
        _testDir = Path.Combine(Path.GetTempPath(), "TAOM_LogTest_" + Path.GetRandomFileName());
        Directory.CreateDirectory(_testDir);
        _originalDir = Directory.GetCurrentDirectory();
        Directory.SetCurrentDirectory(_testDir);
    }

    [TestCleanup]
    public void Cleanup()
    {
        Directory.SetCurrentDirectory(_originalDir);
        if (Directory.Exists(_testDir))
        {
            try { Directory.Delete(_testDir, true); } catch { }
        }
    }

    [TestMethod]
    public void LogInfo_WritesInfoMessageToLogFile()
    {
        var logger = new FileLogger();

        logger.LogInfo("Test message");
        logger.Dispose();

        var logDir = Path.Combine(_testDir, "Logs");
        var logFile = Directory.GetFiles(logDir).First();
        var content = File.ReadAllText(logFile);

        Assert.IsTrue(content.Contains("[INFO]"));
        Assert.IsTrue(content.Contains("Test message"));
    }

    [TestMethod]
    public void LogError_WritesErrorPrefixToLogFile()
    {
        var logger = new FileLogger();

        logger.LogError("Something failed");
        logger.Dispose();

        var logDir = Path.Combine(_testDir, "Logs");
        var logFile = Directory.GetFiles(logDir).First();
        var content = File.ReadAllText(logFile);

        Assert.IsTrue(content.Contains("[ERROR]"));
        Assert.IsTrue(content.Contains("Something failed"));
    }

    [TestMethod]
    public void Dispose_SubsequentWritesDoNotThrow()
    {
        var logger = new FileLogger();
        logger.Dispose();

        logger.LogInfo("After dispose");
        logger.LogError("After dispose error");
    }

    [TestMethod]
    public void LogMultipleMessages_AllWrittenToFile()
    {
        var logger = new FileLogger();

        logger.LogInfo("First");
        logger.LogWarning("Second");
        logger.LogDebug("Third");
        logger.LogError("Fourth");
        logger.Dispose();

        var logDir = Path.Combine(_testDir, "Logs");
        var logFile = Directory.GetFiles(logDir).First();
        var content = File.ReadAllText(logFile);

        Assert.IsTrue(content.Contains("First"));
        Assert.IsTrue(content.Contains("Second"));
        Assert.IsTrue(content.Contains("Third"));
        Assert.IsTrue(content.Contains("Fourth"));
    }

    // --- Crash durability -------------------------------------------------------------------
    // A hard crash (native AV) kills the process without draining the writer queue, so any line
    // still queued is lost. WARNING/ERROR and the INFO crash-phase stamps ([BattleLoad], [SaveLoad], ...
    // see FileLogger.DurableInfoTags) are what we triage crashes from -- they must be on disk the moment
    // the call returns. Plain INFO and DEBUG are written by the writer thread and flushed on a timer.
    //
    // The single-line tests below state the per-level contract but are only PROBABILISTIC
    // regression gates: a writer that left a durable line to the timed flush fails them only when the
    // read beats the next flush pass. The two bulk tests -- 101-line ordering and 400-line
    // concurrency -- are the DETERMINISTIC gates: the StreamWriter holds the tail of a batch in its
    // buffer until a flush, so a timed flush cannot have put all of it on disk by the immediate read.
    // Keep both.

    [TestMethod]
    public void LogInfo_CrashPhaseStamp_WithoutDispose_LineIsOnDiskImmediately()
    {
        var logger = new FileLogger();

        logger.LogInfo("[BattleLoad] Durable info");

        StringAssert.Contains(ReadLogWithoutDispose(), "Durable info");
    }

    [TestMethod]
    public void LogInfo_EveryDurableTag_WithoutDispose_LineIsOnDiskImmediately()
    {
        var logger = new FileLogger();

        foreach (var tag in FileLogger.DurableInfoTags) logger.LogInfo($"{tag} stamp for {tag}");

        var content = ReadLogWithoutDispose();
        foreach (var tag in FileLogger.DurableInfoTags) StringAssert.Contains(content, $"{tag} stamp for {tag}");
    }

    // The list is a contract: a doc or a tool reads these tags after a crash. One literal copy here, so that
    // removing a tag from production fails this test instead of shrinking the loop in the test above.
    [TestMethod]
    public void DurableInfoTags_AreExactlyTheTagsTheDocsAndToolsRelyOn()
    {
        var expected = new[]
        {
            "[BattleLoad]", "[SaveLoad]", "[MapLoad]", "[BlowDiag]", "[MemSample]", "[MemStation]", "[MemProbe]",
            "[ExitStall]", "[ScreenOpen]", "[CreatureBandits]", "[EnlistDiag]", "[ShaderPrecompilation]",
            "[CrashReport]", "[Shutdown]", "[PatchApply]", "[StuckBattle]", "[SmokeTestGate]", "[Session]",
            "[MissionDiag]", "[Check]",
        };

        CollectionAssert.AreEquivalent(expected, FileLogger.DurableInfoTags);
    }

    [TestMethod]
    public void IsDurableInfo_OutcomeCheckLine_True()
    {
        Assert.IsTrue(FileLogger.IsDurableInfo("[Check] id=battle.start result=pass"));
    }

    [TestMethod]
    public void IsDurableInfo_PhaseStampsAndDiagnostics_True()
    {
        Assert.IsTrue(FileLogger.IsDurableInfo("[BattleLoad] AgentEquipBegin seq=3"));
        Assert.IsTrue(FileLogger.IsDurableInfo("[SaveLoad] SaveWriteFault name='x'"));
        Assert.IsTrue(FileLogger.IsDurableInfo("[MapLoad] begin"));
        Assert.IsTrue(FileLogger.IsDurableInfo("[BlowDiag] blow victim=1"));
        Assert.IsTrue(FileLogger.IsDurableInfo("[Shutdown] clean module unload"));
    }

    [TestMethod]
    public void IsDurableInfo_PlainOrUntaggedOrMidLineTag_False()
    {
        Assert.IsFalse(FileLogger.IsDurableInfo("[Marshal] company tick"));
        Assert.IsFalse(FileLogger.IsDurableInfo("plain message"));
        Assert.IsFalse(FileLogger.IsDurableInfo("prefix [BattleLoad] later in the line"));
        Assert.IsFalse(FileLogger.IsDurableInfo("[battleload] wrong case"));
        Assert.IsFalse(FileLogger.IsDurableInfo(""));
        Assert.IsFalse(FileLogger.IsDurableInfo(null));
    }

    // Plain INFO is flushed by the writer thread within about a third of a second (FlushIntervalMs plus one 50 ms loop pass), without any further call.
    [TestMethod]
    public void LogInfo_PlainLine_IsOnDiskWithinTheFlushInterval()
    {
        var logger = new FileLogger();

        logger.LogInfo("Plain info");

        var deadline = DateTime.UtcNow.AddSeconds(5);
        var content = ReadLogWithoutDispose();
        while (!content.Contains("Plain info") && DateTime.UtcNow < deadline)
        {
            Thread.Sleep(50);
            content = ReadLogWithoutDispose();
        }
        StringAssert.Contains(content, "Plain info", "the timed flush never wrote the line");
    }

    [TestMethod]
    public void Flush_PlainInfoAndDebug_AreOnDiskAtOnce()
    {
        var logger = new FileLogger();

        logger.LogInfo("Plain info");
        logger.LogDebug("Plain debug");
        logger.Flush();

        var content = ReadLogWithoutDispose();
        StringAssert.Contains(content, "Plain info");
        StringAssert.Contains(content, "Plain debug");
    }

    [TestMethod]
    public void TryFlush_OnAFileLogger_WritesPlainLines()
    {
        IModLogger logger = new FileLogger();

        logger.LogInfo("Plain info before a crash report");
        logger.TryFlush();

        StringAssert.Contains(ReadLogWithoutDispose(), "Plain info before a crash report");
    }

    [TestMethod]
    public void TryFlush_OnALoggerWithoutBuffering_DoesNothingAndDoesNotThrow()
    {
        var logger = Substitute.For<IModLogger>();

        logger.TryFlush();

        Assert.AreEqual(0, logger.ReceivedCalls().Count(), "a logger that cannot buffer must not be called");
    }

    [TestMethod]
    public void Dispose_PlainInfoStillQueued_IsWrittenToDisk()
    {
        var logger = new FileLogger();
        var path = logger.LogFilePath;

        for (int i = 0; i < 50; i++) logger.LogInfo($"plain-{i:D2}");
        logger.Dispose();

        var lines = File.ReadAllLines(path);
        Assert.AreEqual(50, lines.Length);
        StringAssert.Contains(lines[49], "plain-49");
    }

    // A WARNING flushes at once and brings every plain line queued before it, in order.
    [TestMethod]
    public void LogWarning_AfterPlainInfo_WithoutDispose_BothOnDiskInEnqueueOrder()
    {
        var logger = new FileLogger();

        logger.LogInfo("Plain info");
        logger.LogWarning("Durable warning");

        var content = ReadLogWithoutDispose();
        Assert.IsTrue(
            content.IndexOf("Plain info", StringComparison.Ordinal) >= 0
                && content.IndexOf("Plain info", StringComparison.Ordinal)
                    < content.IndexOf("Durable warning", StringComparison.Ordinal),
            "The plain INFO was enqueued first and must be written first.");
    }

    [TestMethod]
    public void LogWarning_WithoutDispose_LineIsOnDiskImmediately()
    {
        var logger = new FileLogger();

        logger.LogWarning("Durable warning");

        StringAssert.Contains(ReadLogWithoutDispose(), "Durable warning");
    }

    [TestMethod]
    public void LogError_WithoutDispose_LineIsOnDiskImmediately()
    {
        var logger = new FileLogger();

        logger.LogError("Durable error");

        StringAssert.Contains(ReadLogWithoutDispose(), "Durable error");
    }

    // Single-queue ordering invariant: a durable write drains everything queued ahead of it, so
    // global enqueue order survives even though DEBUG alone is async. Two queues would break this.
    [TestMethod]
    public void LogDebug_ThenLogInfo_WithoutDispose_BothOnDiskInEnqueueOrder()
    {
        var logger = new FileLogger();

        logger.LogDebug("Queued debug");
        logger.LogInfo("[SaveLoad] Durable info");

        var content = ReadLogWithoutDispose();
        StringAssert.Contains(content, "Queued debug");
        StringAssert.Contains(content, "Durable info");
        Assert.IsTrue(
            content.IndexOf("Queued debug", StringComparison.Ordinal)
                < content.IndexOf("Durable info", StringComparison.Ordinal),
            "The DEBUG line was enqueued first and must be written first.");
    }

    [TestMethod]
    public void LogInfo_AfterManyDebugLines_WithoutDispose_DrainsAllPendingInOrder()
    {
        var logger = new FileLogger();

        for (int i = 0; i < 100; i++) logger.LogDebug($"debug-{i:D3}");
        logger.LogInfo("[BattleLoad] flush-marker");

        var lines = ReadLogWithoutDispose()
            .Split(new[] { Environment.NewLine }, StringSplitOptions.RemoveEmptyEntries);
        Assert.AreEqual(101, lines.Length, "The durable INFO stamp must drain all 100 queued DEBUG lines.");
        for (int i = 0; i < 100; i++) StringAssert.Contains(lines[i], $"debug-{i:D3}");
        StringAssert.Contains(lines[100], "flush-marker");
    }

    // StreamWriter is not thread-safe. Without a shared lock, concurrent drains tear lines into
    // each other; assert every line is intact, not merely that the count is right.
    [TestMethod]
    public void LogWarning_ConcurrentWritesFromManyThreads_EveryLineIntactAndCountMatches()
    {
        var logger = new FileLogger();
        const int threads = 8, perThread = 50;

        Parallel.For(0, threads, t =>
        {
            for (int i = 0; i < perThread; i++) logger.LogWarning($"t{t}-line{i:D2}");
        });

        var lines = ReadLogWithoutDispose()
            .Split(new[] { Environment.NewLine }, StringSplitOptions.RemoveEmptyEntries);
        Assert.AreEqual(threads * perThread, lines.Length);
        var wellFormed = new Regex(@"^\[.+\] \[(INFO|DEBUG|WARNING|ERROR)\] t\d+-line\d{2}$");
        foreach (var line in lines)
            Assert.IsTrue(wellFormed.IsMatch(line), $"Torn or interleaved line: '{line}'");
    }

    // Dispose's Join(5s) can time out, leaving the writer thread live while Dispose drains and
    // disposes the same StreamWriter. That race throws ObjectDisposedException on a background
    // thread, which terminates the process.
    [TestMethod]
    public void Dispose_WithConcurrentWriteInFlight_DoesNotThrow()
    {
        var logger = new FileLogger();
        using var startWriting = new ManualResetEventSlim(false);

        var writer = Task.Run(() =>
        {
            startWriting.Wait();
            for (int i = 0; i < 500; i++) logger.LogInfo($"racing-{i}");
        });

        startWriting.Set();
        logger.Dispose();

        writer.Wait(TimeSpan.FromSeconds(10));
        Assert.IsNull(writer.Exception, $"Writing across Dispose threw: {writer.Exception}");
    }

    // Sync-draining moves IO faults off the writer thread and onto the GAME thread, where they
    // would newly propagate into engine code. A logger must never take the game down.
    [TestMethod]
    public void Log_AtEveryLevelWhenWriterFaulted_DoesNotThrowToCaller()
    {
        var logger = new FileLogger();
        logger.LogInfo("before fault");

        FaultTheWriter(logger);

        logger.LogInfo("after fault");
        logger.LogWarning("after fault");
        logger.LogError("after fault");
    }

    // A write fault drops the line the writer was mid-way through. The crash-forensics log must
    // never look healthy while silently losing lines, so it reports the faults once writing recovers.
    // The notice counts faults, not lines: one fault can drop one or more.
    [TestMethod]
    public void LogWarning_AfterWriteFaultRecovers_ReportsTheWriteFaultCount()
    {
        var logger = new FileLogger();
        logger.LogInfo("before fault");

        FaultTheWriter(logger);
        logger.LogWarning("this line is lost");   // throws inside Drain, counted

        RestoreWriter(logger);
        logger.LogWarning("after recovery");

        var content = ReadLogWithoutDispose();
        StringAssert.Contains(content, "write fault(s)");
        StringAssert.Contains(content, "after recovery");
    }

    // REGRESSION: Drain() must dequeue even once _logFile is null. If it early-returns instead,
    // items stay queued, ProcessQueue's `!_queue.IsEmpty` exit condition never clears, and the
    // writer thread spins hot on a core forever. The pre-2026-07-16 loop always dequeued (writing
    // via _logFile?.), so it could not spin -- this pins that property against reintroduction.
    [TestMethod]
    public void Dispose_ThenManyWrites_QueueStillDrains_SoWriterCannotSpin()
    {
        var logger = new FileLogger();
        logger.Dispose();

        for (int i = 0; i < 200; i++) logger.LogInfo($"post-dispose-{i}");

        var queueField = typeof(FileLogger).GetField("_queue", BindingFlags.NonPublic | BindingFlags.Instance);
        Assert.IsNotNull(queueField, "FileLogger._queue was renamed -- update this regression test.");
        var queue = (System.Collections.Concurrent.ConcurrentQueue<string>)queueField.GetValue(logger);
        Assert.IsTrue(queue.IsEmpty,
            $"Drain() left {queue.Count} item(s) queued after Dispose. ProcessQueue loops while " +
            "!_queue.IsEmpty, so a live writer thread would spin at 100% CPU on this state.");
    }

    // --- Retention ---
    // Nothing ever pruned Logs/, so taom_debug_*.log accumulated for the life of the install. That
    // matters beyond disk: CrashReportService re-zips the log into every crash bundle.

    private void SeedOldLogs(int count)
    {
        var dir = Path.Combine(_testDir, "Logs");
        Directory.CreateDirectory(dir);
        var first = new DateTime(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc);
        for (int i = 0; i < count; i++)
        {
            var stamp = first.AddDays(i);
            var p = Path.Combine(dir, $"taom_debug_{stamp:yyyy-MM-dd}_12-00-00.log");
            File.WriteAllText(p, $"old log {i}");
            File.SetLastWriteTimeUtc(p, stamp);
        }
    }

    private static string[] DebugLogs(string dir) =>
        Directory.GetFiles(dir, "taom_debug_*.log");

    // The default matters on its own, separately from the parameterised behaviour above, because
    // production only ever uses the parameterless ctor. Retention is a support-window decision, not
    // a disk-space one: every launch prunes, so a player who crashes and then keeps playing pushes
    // the crash log one slot closer to deletion each time they start the game. At the original 10 it
    // survived nine relaunches, which is comfortably shorter than the round trip of noticing a
    // crash, reporting it, and being asked for logs. Sized here for a couple of weeks of daily play.
    [TestMethod]
    public void Ctor_Default_RetainsAFullSupportRoundTripOfLogs()
    {
        SeedOldLogs(40);

        using var logger = new FileLogger();

        var remaining = DebugLogs(Path.Combine(_testDir, "Logs"));
        Assert.AreEqual(30, remaining.Length,
            "the shipped default is what decides whether a player's crash log still exists when we ask for it");
        StringAssert.Contains(string.Join("|", remaining), Path.GetFileName(logger.LogFilePath),
            "the log currently being written must never be pruned");
    }

    [TestMethod]
    public void Ctor_WithRetentionLimit_KeepsOnlyTheNewestLogsIncludingItsOwn()
    {
        SeedOldLogs(12);

        using var logger = new FileLogger(retainedLogs: 5);

        var remaining = DebugLogs(Path.Combine(_testDir, "Logs"));
        Assert.AreEqual(5, remaining.Length, "retention should cap the debug logs at the configured count");
        StringAssert.Contains(string.Join("|", remaining), Path.GetFileName(logger.LogFilePath),
            "the log currently being written must never be pruned");
    }

    [TestMethod]
    public void Ctor_WithRetentionLimit_DeletesTheOldestFirst()
    {
        SeedOldLogs(12);

        using var logger = new FileLogger(retainedLogs: 3);

        var names = DebugLogs(Path.Combine(_testDir, "Logs")).Select(Path.GetFileName).ToList();
        CollectionAssert.DoesNotContain(names, "taom_debug_2026-01-01_12-00-00.log");
        CollectionAssert.Contains(names, "taom_debug_2026-01-12_12-00-00.log", "newest survivors are kept");
    }

    // Logs/ is shared with the crash bundler, the battle-load stall marker and the shader-precompile
    // sentinels. The prune must be surgical or it would eat a crash report.
    [TestMethod]
    public void Ctor_WithRetentionLimit_LeavesNonDebugLogFilesAlone()
    {
        var dir = Path.Combine(_testDir, "Logs");
        SeedOldLogs(12);
        File.WriteAllText(Path.Combine(dir, "taom_crash_2026-01-01.zip"), "bundle");
        File.WriteAllText(Path.Combine(dir, "battleload_stall.marker"), "marker");

        using var logger = new FileLogger(retainedLogs: 2);

        Assert.IsTrue(File.Exists(Path.Combine(dir, "taom_crash_2026-01-01.zip")), "crash bundles must survive");
        Assert.IsTrue(File.Exists(Path.Combine(dir, "battleload_stall.marker")), "stall markers must survive");
    }

    [TestMethod]
    public void Ctor_WithRetentionDisabled_KeepsEveryLog()
    {
        SeedOldLogs(12);

        using var logger = new FileLogger(retainedLogs: 0);

        Assert.AreEqual(13, DebugLogs(Path.Combine(_testDir, "Logs")).Length);
    }

    // A prune failure must never take the logger — or the game — down with it.
    [TestMethod]
    public void Ctor_WhenALogFileIsLocked_StillConstructsAndLogs()
    {
        var dir = Path.Combine(_testDir, "Logs");
        SeedOldLogs(12);
        using var held = new FileStream(Path.Combine(dir, "taom_debug_2026-01-01_12-00-00.log"),
            FileMode.Open, FileAccess.Read, FileShare.None);

        using var logger = new FileLogger(retainedLogs: 2);
        logger.LogInfo("still works");
        logger.Flush();

        // Read the logger's OWN file by path -- ReadLogWithoutDispose() takes the first file in the
        // directory, which here is one of the seeded 2026-01-* logs.
        using var fs = new FileStream(logger.LogFilePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        using var reader = new StreamReader(fs);
        StringAssert.Contains(reader.ReadToEnd(), "still works");
    }

    // Fault injection: dispose the underlying writer out from under the logger so the next write
    // throws from inside the drain. Reflection is the only seam -- FileLogger owns its StreamWriter
    // and takes no injectable dependency.
    private static void FaultTheWriter(FileLogger logger)
    {
        var field = typeof(FileLogger).GetField("_logFile", BindingFlags.NonPublic | BindingFlags.Instance);
        Assert.IsNotNull(field, "FileLogger._logFile was renamed -- update this fault-injection test.");
        ((StreamWriter)field.GetValue(logger)).Dispose();
    }

    private void RestoreWriter(FileLogger logger)
    {
        var field = typeof(FileLogger).GetField("_logFile", BindingFlags.NonPublic | BindingFlags.Instance);
        var logPath = Directory.GetFiles(Path.Combine(_testDir, "Logs")).First();
        field.SetValue(logger, new StreamWriter(logPath, append: true));
    }
}
