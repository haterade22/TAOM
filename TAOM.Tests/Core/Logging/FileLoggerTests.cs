using Microsoft.VisualStudio.TestTools.UnitTesting;
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
    // still queued is lost. INFO/WARNING/ERROR carry the [BattleLoad]/[SaveLoad] phase stamps we
    // triage crashes from -- they must be on disk the moment the call returns.
    //
    // The three single-line tests below state the per-level contract but are only PROBABILISTIC
    // regression gates: against an async writer they fail only when the read beats the drain
    // (observed 2/5 then 4/5 failing across two runs). The two bulk tests -- 101-line ordering and
    // 400-line concurrency -- are the DETERMINISTIC gates: an async writer sleeping 50ms cannot
    // have drained a full batch, so they read an empty file every time. Keep both.

    [TestMethod]
    public void LogInfo_WithoutDispose_LineIsOnDiskImmediately()
    {
        var logger = new FileLogger();

        logger.LogInfo("Durable info");

        StringAssert.Contains(ReadLogWithoutDispose(), "Durable info");
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
        logger.LogInfo("Durable info");

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
        logger.LogInfo("flush-marker");

        var lines = ReadLogWithoutDispose()
            .Split(new[] { Environment.NewLine }, StringSplitOptions.RemoveEmptyEntries);
        Assert.AreEqual(101, lines.Length, "The durable INFO must drain all 100 queued DEBUG lines.");
        for (int i = 0; i < 100; i++) StringAssert.Contains(lines[i], $"debug-{i:D3}");
        StringAssert.Contains(lines[100], "flush-marker");
    }

    // StreamWriter is not thread-safe. Without a shared lock, concurrent drains tear lines into
    // each other; assert every line is intact, not merely that the count is right.
    [TestMethod]
    public void LogInfo_ConcurrentWritesFromManyThreads_EveryLineIntactAndCountMatches()
    {
        var logger = new FileLogger();
        const int threads = 8, perThread = 50;

        Parallel.For(0, threads, t =>
        {
            for (int i = 0; i < perThread; i++) logger.LogInfo($"t{t}-line{i:D2}");
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
    public void LogInfo_WhenWriterFaulted_DoesNotThrowToCaller()
    {
        var logger = new FileLogger();
        logger.LogInfo("before fault");

        FaultTheWriter(logger);

        logger.LogInfo("after fault");
        logger.LogWarning("after fault");
        logger.LogError("after fault");
    }

    // A write fault drops the line the writer was mid-way through. The crash-forensics log must
    // never look healthy while silently losing lines, so it reports the loss once writing recovers.
    [TestMethod]
    public void LogInfo_AfterWriteFaultRecovers_ReportsTheLostLineCount()
    {
        var logger = new FileLogger();
        logger.LogInfo("before fault");

        FaultTheWriter(logger);
        logger.LogInfo("this line is lost");   // throws inside Drain, counted

        RestoreWriter(logger);
        logger.LogInfo("after recovery");

        var content = ReadLogWithoutDispose();
        StringAssert.Contains(content, "line(s) lost to a write fault");
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
