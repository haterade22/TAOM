using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Core.Logging;

namespace TAOM.Tests.Core.Logging;

/// <summary>
/// RUNTIME-PERF-09 (WP123): what one durable INFO line costs the thread that logs it. FileLogger drains INFO, WARNING
/// and ERROR on the caller (WriteLine plus Flush under one lock) so a native crash cannot lose the line; DEBUG only
/// enqueues. The line is shaped like a per-agent [BattleLoad] phase, the largest INFO stream left once auto-resolve
/// logging is gone (E4: 1,602 such lines in one arena load). Opt-in (TAOM_RUN_BENCHMARKS=1).
/// </summary>
[TestClass]
public class FileLoggerDurableWriteBenchmarkTests
{
    private const int Lines = 4000;
    private const int ArenaBurst = 1602;
    private const string Line =
        "[BattleLoad] seq=1234 t=+109604ms phase=AgentEquipOk agent#17 'Townsman' char='townsman_gondor' culture='gondor' race='human'";

    private string _testDir = null!;
    private string _originalDir = null!;

    [TestInitialize]
    public void Setup()
    {
        _testDir = Path.Combine(Path.GetTempPath(), "TAOM_LogBench_" + Path.GetRandomFileName());
        Directory.CreateDirectory(_testDir);
        _originalDir = Directory.GetCurrentDirectory();
        Directory.SetCurrentDirectory(_testDir);
    }

    [TestCleanup]
    public void Cleanup()
    {
        Directory.SetCurrentDirectory(_originalDir);
        try { Directory.Delete(_testDir, true); } catch { /* best effort */ }
    }

    private static double MicrosPerLine(Action body, int lines)
    {
        var watch = Stopwatch.StartNew();
        body();
        watch.Stop();
        return watch.Elapsed.TotalMilliseconds * 1000.0 / lines;
    }

    [TestMethod]
    [TestCategory("Benchmark")]
    public void DurableInfoLine_CostOnTheLoggingThread()
    {
        if (Environment.GetEnvironmentVariable("TAOM_RUN_BENCHMARKS") != "1")
            Assert.Inconclusive("Benchmark: set TAOM_RUN_BENCHMARKS=1");

        var logger = new FileLogger();
        double info, debug, contended;
        try
        {
            for (int i = 0; i < 200; i++) logger.LogInfo(Line); // warm-up: JIT, file open, OS cache
            info = MicrosPerLine(() => { for (int i = 0; i < Lines; i++) logger.LogInfo(Line); }, Lines);
            debug = MicrosPerLine(() => { for (int i = 0; i < Lines; i++) logger.LogDebug(Line); }, Lines);
            contended = MicrosPerLine(() => Parallel.For(0, 4, _ => { for (int i = 0; i < Lines / 4; i++) logger.LogInfo(Line); }), Lines);
        }
        finally
        {
            logger.Dispose();
        }

        Console.WriteLine(
            "[LogBench] INFO {0:0.0} us/line, DEBUG {1:0.0} us/line, INFO on 4 threads {2:0.0} us/line; " +
            "a {3}-line arena burst costs {4:0} ms durable, {5:0} ms queued",
            info, debug, contended, ArenaBurst, info * ArenaBurst / 1000.0, debug * ArenaBurst / 1000.0);

        var written = File.ReadAllLines(Directory.GetFiles(Path.Combine(_testDir, "Logs")).Single()).Length;
        Assert.AreEqual(200 + 3 * Lines, written, "every line reached the file");
    }
}
