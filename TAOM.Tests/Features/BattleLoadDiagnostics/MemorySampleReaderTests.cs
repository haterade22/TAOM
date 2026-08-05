using System.Diagnostics;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.BattleLoadDiagnostics;

namespace TAOM.Tests.Features.BattleLoadDiagnostics;

/// <summary>
/// Live P/Invoke smoke tests (Windows-only test host, matching the game's platform):
/// GlobalMemoryStatusEx + GetProcessMemoryInfo return plausible values and stay far under
/// the cost budget that justified avoiding System.Diagnostics.Process (7,711 us/call on
/// net472 vs 84 us for this pair).
/// </summary>
[TestClass]
public class MemorySampleReaderTests
{
    [TestMethod]
    public void TryRead_OnWindows_ReturnsPlausibleValues()
    {
        Assert.IsTrue(MemorySampleReader.TryRead(out var sample));

        Assert.IsTrue(sample.TotalPhysMb > 1024, $"TotalPhysMb={sample.TotalPhysMb}");
        Assert.IsTrue(sample.SysCommitUsedMb > 0, $"SysCommitUsedMb={sample.SysCommitUsedMb}");
        Assert.IsTrue(sample.SysCommitLimitMb >= sample.SysCommitUsedMb,
            $"limit={sample.SysCommitLimitMb} < used={sample.SysCommitUsedMb}");
        Assert.IsTrue(sample.PrivMb > 0, $"PrivMb={sample.PrivMb}");
        Assert.IsTrue(sample.MemLoadPercent >= 0 && sample.MemLoadPercent <= 100,
            $"MemLoadPercent={sample.MemLoadPercent}");
    }

    [TestMethod]
    public void TryReadProcess_OnWindows_ReturnsPositiveCounters()
    {
        Assert.IsTrue(MemorySampleReader.TryReadProcess(out long privMb, out long wsMb));

        Assert.IsTrue(privMb > 0, $"privMb={privMb}");
        Assert.IsTrue(wsMb > 0, $"wsMb={wsMb}");
    }

    [TestMethod]
    public void TryRead_CompletesWithin500ms()
    {
        var sw = Stopwatch.StartNew();
        bool ok = MemorySampleReader.TryRead(out _);
        sw.Stop();

        Assert.IsTrue(ok);
        Assert.IsTrue(sw.ElapsedMilliseconds < 500, $"TryRead took {sw.ElapsedMilliseconds}ms");
    }
}
