using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Core.Logging;
using TAOM.Dependencies;
using static TAOM.Tests.Infrastructure.RepoPaths;

namespace TAOM.Tests.Core.Logging;

/// <summary>
/// Issue #725: TAOM.Dependencies loads before Main, so its startup lines (the Harmony fork version,
/// the AssemblyResolve redirects, the UnpatchAll guard, a second 0Harmony.dll in the process) sit in
/// <see cref="EarlyLog"/>'s buffer until something hands it a logger. Nothing did, and every one of
/// them was lost. <see cref="EarlyLogBridge.Connect"/> is that hand-over; these tests pin what it does
/// with the lines, two pin what EarlyLog does with a line written on another thread while the hand-over
/// runs, and one pins that SubModule.OnSubModuleLoad really calls it.
///
/// EarlyLog is process-wide static state with no reset: DrainTo installs its target once and never
/// clears it, and the buffer is shared with any other test that runs the Dependencies SubModule (its
/// static constructor writes to it). So each test starts from an empty buffer with no target and
/// puts that back afterwards, by reaching into EarlyLog's two private fields.
/// </summary>
[TestClass]
public class EarlyLogBridgeTests
{
    private static readonly FieldInfo DrainTargetField = EarlyLogField("_drainTarget");
    private static readonly FieldInfo BufferField = EarlyLogField("_buffer");

    [TestInitialize]
    public void StartFromAnEmptyBufferWithNoTarget() => ResetEarlyLog();

    // Not just tidiness: a target left installed would send every later EarlyLog line, from any test
    // that runs the Dependencies SubModule, into a substitute nobody is looking at.
    [TestCleanup]
    public void LeaveEarlyLogAsFound() => ResetEarlyLog();

    [TestMethod]
    public void Connect_LinesBufferedBeforeIt_AreFlushedInOrderAtTheirLevel()
    {
        EarlyLog.Info("[TAOM.Dependencies] first info");
        EarlyLog.Warning("[TAOM.Dependencies] a warning");
        EarlyLog.Error("[TAOM.Dependencies] an error");
        EarlyLog.Info("[TAOM.Dependencies] second info");
        var logger = Substitute.For<IModLogger>();

        EarlyLogBridge.Connect(logger);

        var calls = LoggerCalls(logger);
        CollectionAssert.AreEqual(
            new[] { "LogInfo", "LogWarning", "LogError", "LogInfo" },
            calls.Select(call => call.Level).ToArray(),
            "every buffered line goes out exactly once, in the order it was written, at its own level");
        // EarlyLog prefixes a flushed line with the time it was buffered; the text itself must arrive whole.
        AssertFlushedLine(calls[0].Message, "[TAOM.Dependencies] first info");
        AssertFlushedLine(calls[1].Message, "[TAOM.Dependencies] a warning");
        AssertFlushedLine(calls[2].Message, "[TAOM.Dependencies] an error");
        AssertFlushedLine(calls[3].Message, "[TAOM.Dependencies] second info");
    }

    [TestMethod]
    public void Connect_LineBufferedEarlier_CarriesTheTimeItWasBufferedNotTheTimeOfTheFlush()
    {
        // A line buffered on another day, put in the buffer directly so no clock is involved. A flush that
        // stamped the moment it ran would print the current time of day, and the log would date a startup
        // event wrong.
        var bufferedAt = new DateTime(2020, 1, 2, 3, 4, 5);
        var buffer = BufferField.GetValue(null)!;
        buffer.GetType().GetMethod("Enqueue")!.Invoke(buffer, new object[] { ("INFO", "logged long ago", bufferedAt) });
        var logger = Substitute.For<IModLogger>();

        EarlyLogBridge.Connect(logger);

        Assert.AreEqual($"[buffered {bufferedAt:HH:mm:ss}] logged long ago", LoggerCalls(logger).Single().Message);
    }

    [TestMethod]
    public void Connect_LinesWrittenAfterIt_GoStraightToTheLoggerAtTheirLevel()
    {
        var logger = Substitute.For<IModLogger>();

        EarlyLogBridge.Connect(logger);

        Assert.AreEqual(0, logger.ReceivedCalls().Count(), "nothing was buffered, so connecting must log nothing");

        EarlyLog.Info("info line");
        EarlyLog.Warning("warning line");
        EarlyLog.Error("error line");

        // Exact text this time: only the drain adds a prefix, a direct write must reach the logger untouched.
        CollectionAssert.AreEqual(
            new[] { ("LogInfo", "info line"), ("LogWarning", "warning line"), ("LogError", "error line") },
            LoggerCalls(logger).Select(call => (call.Level, call.Message)).ToArray());
    }

    [TestMethod]
    public void Connect_UnrecognisedLevel_IsLoggedAsInfoWithItsLevelNameKept()
    {
        var logger = Substitute.For<IModLogger>();
        EarlyLogBridge.Connect(logger);

        // EarlyLog only ever emits INFO, WARNING and ERROR today, so call the installed target the way
        // a fourth level would. TAOM.Dependencies ships as its own module, so a newer one can pair with
        // this build; its extra level must not read as a routine INFO line.
        var target = DrainTargetField.GetValue(null) as Action<string, string>;
        Assert.IsNotNull(target, "Connect installed no drain target, so EarlyLog would keep buffering");
        target!("TRACE", "an odd level");

        CollectionAssert.AreEqual(
            new[] { ("LogInfo", "[TRACE] an odd level") },
            LoggerCalls(logger).Select(call => (call.Level, call.Message)).ToArray());
    }

    [TestMethod]
    public void Connect_LoggerThatThrows_NeverEscapesAndStillSeesEveryLine()
    {
        var logger = Substitute.For<IModLogger>();
        logger.When(l => l.LogInfo(Arg.Any<string>())).Do(_ => throw new IOException("log file locked"));
        logger.When(l => l.LogWarning(Arg.Any<string>())).Do(_ => throw new IOException("log file locked"));
        logger.When(l => l.LogError(Arg.Any<string>())).Do(_ => throw new IOException("log file locked"));
        EarlyLog.Info("buffered a");
        EarlyLog.Warning("buffered b");
        EarlyLog.Error("buffered c");

        // The drain runs the delegate once per buffered line; a leak here would abort the flush ...
        EarlyLogBridge.Connect(logger);
        // ... and a later writer, such as the AssemblyResolve handler, runs it straight from the engine's
        // load path, where a leak would turn a log line into a failed module load.
        EarlyLog.Error("written after connecting");

        Assert.AreEqual(4, logger.ReceivedCalls().Count(),
            "a line whose write throws is dropped alone: the ones behind it must still reach the logger");
    }

    [TestMethod]
    public void DrainTo_LineWrittenByAnotherThreadMidFlush_ArrivesAfterTheWholeBacklog()
    {
        EarlyLog.Info("backlog 1");
        EarlyLog.Info("backlog 2");
        EarlyLog.Info("backlog 3");
        var received = new ConcurrentQueue<string>();
        var flushStarted = 0;
        Task? writer = null;

        EarlyLog.DrainTo((level, message) =>
        {
            received.Enqueue(message);
            if (Interlocked.Exchange(ref flushStarted, 1) != 0) return;

            // The first backlog line is out and two are still waiting: another thread logs now. Whether
            // EarlyLog makes that thread wait for the hand-over or only queues its line behind the
            // backlog is its own business, so this wait is bounded and its outcome ignored.
            writer = Task.Run(() => EarlyLog.Info("written mid-flush"));
            writer.Wait(TimeSpan.FromMilliseconds(250));
        });
        Assert.IsTrue(writer!.Wait(TimeSpan.FromSeconds(10)), "the mid-flush writer never finished");

        CollectionAssert.AreEqual(
            new[] { "backlog 1", "backlog 2", "backlog 3", "written mid-flush" },
            received.Select(line => Regex.Replace(line, BufferedStampPattern, "")).ToArray(),
            "every line reaches the logger once, and one logged mid-flush belongs after the older ones, not between them");
    }

    // The hand-over is a check then an act on both sides. A writer reads "no target yet" and then enqueues;
    // DrainTo publishes the target and empties the buffer. With no lock between the two steps the writer
    // can read, DrainTo can run to completion, and the line lands in a buffer nobody drains again. The
    // window is only the clock read and the enqueue, so most single rounds miss it: this releases the
    // writers and DrainTo together thousands of times, sweeping DrainTo's start across the writers'
    // window. A pass proves little on its own; a failure is a line stranded in the buffer.
    [TestMethod]
    public void DrainTo_WritersRacingTheHandOver_StrandNoLineInTheBuffer()
    {
        const int Rounds = 3000;
        const int Writers = 3;
        var timeout = TimeSpan.FromSeconds(30);
        var roundStart = new Barrier(Writers + 1);
        var roundEnd = new Barrier(Writers + 1);
        var threads = Enumerable.Range(0, Writers).Select(index => new Thread(() =>
        {
            for (var round = 0; round < Rounds; round++)
            {
                if (!roundStart.SignalAndWait(timeout)) return;
                EarlyLog.Info("racing line");
                if (!roundEnd.SignalAndWait(timeout)) return;
            }
        }) { IsBackground = true, Name = $"EarlyLog race writer {index}" }).ToList();
        threads.ForEach(thread => thread.Start());

        var roundsWithAStrandedLine = 0;
        var firstSuchRound = -1;
        for (var round = 0; round < Rounds; round++)
        {
            ResetEarlyLog();
            var delivered = 0;
            Assert.IsTrue(roundStart.SignalAndWait(timeout), "a writer never reached the start of round " + round);
            Thread.SpinWait(round % 100);
            EarlyLog.DrainTo((level, message) => Interlocked.Increment(ref delivered));
            Assert.IsTrue(roundEnd.SignalAndWait(timeout), "a writer never finished round " + round);

            if (delivered == Writers) continue;
            roundsWithAStrandedLine++;
            if (firstSuchRound < 0) firstSuchRound = round;
        }
        threads.ForEach(thread => Assert.IsTrue(thread.Join(timeout), thread.Name + " never finished"));

        Assert.AreEqual(0, roundsWithAStrandedLine,
            $"{roundsWithAStrandedLine} of {Rounds} rounds left a line in EarlyLog's buffer, the first in round {firstSuchRound}: " +
            "the writer checked for a target before DrainTo published one and enqueued after DrainTo had emptied the buffer");
    }

    [TestMethod]
    public void SubModule_ConnectsTheEarlyLog_AfterConfiguringTheContainer()
    {
        // The helper alone fixes nothing: #725 was a flush that no code called. Comments are stripped so
        // a commented-out call cannot satisfy this.
        var source = ReadSource("Main/SubModule.cs", stripComments: true);

        var configure = source.IndexOf("IoC.Configure();", StringComparison.Ordinal);
        var connect = source.IndexOf("EarlyLogBridge.Connect(IoC.Resolve<IModLogger>())", StringComparison.Ordinal);

        Assert.IsTrue(configure >= 0, "OnSubModuleLoad no longer calls IoC.Configure(); update this test.");
        Assert.IsTrue(connect >= 0,
            "SubModule never calls EarlyLogBridge.Connect, so TAOM.Dependencies' startup lines are dropped again (#725).");
        Assert.IsTrue(connect > configure,
            "EarlyLogBridge.Connect resolves the logger from the container, so it has to follow IoC.Configure(): " +
            "ahead of it the resolve throws, and the call's catch would hide that.");
    }

    // "[buffered HH:mm:ss] " marks a line EarlyLog held back, with the time it was buffered. The separators
    // are matched loosely because the time format takes its separator from the current culture.
    private const string BufferedStampPattern = @"^\[buffered \d{2}\D\d{2}\D\d{2}\] ";

    private static void AssertFlushedLine(string actual, string text) =>
        StringAssert.Matches(actual, new Regex(BufferedStampPattern + Regex.Escape(text) + "$"));

    // What the logger received, oldest first. The method name is the level, the argument the line.
    private static List<(string Level, string Message)> LoggerCalls(IModLogger logger) =>
        logger.ReceivedCalls()
            .Select(call => (Level: call.GetMethodInfo().Name, Message: (string)call.GetArguments()[0]!))
            .ToList();

    private static void ResetEarlyLog()
    {
        DrainTargetField.SetValue(null, null);

        // ConcurrentQueue<T>.Clear does not exist on .NET Framework, so empty the buffer a TryDequeue at a time.
        var buffer = BufferField.GetValue(null)!;
        var tryDequeue = buffer.GetType().GetMethod("TryDequeue")!;
        var dequeued = new object?[1];
        while ((bool)tryDequeue.Invoke(buffer, dequeued)!)
        {
        }
    }

    private static FieldInfo EarlyLogField(string name) =>
        typeof(EarlyLog).GetField(name, BindingFlags.NonPublic | BindingFlags.Static)
        ?? throw new InvalidOperationException(
            $"EarlyLog.{name} is gone. EarlyLog has no reset, so these tests clear its state through its private fields; update EarlyLogBridgeTests to match.");
}
