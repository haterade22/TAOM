using System;

namespace TAOM.Features.BattleLoadDiagnostics;

/// <summary>
/// The process-memory token tail shared by every phase-stamped log line: gen0/gen1/gen2 collection
/// counts + managed heap size, plus the process footprint (privMB/wsMB via one GetProcessMemoryInfo
/// syscall, #386). Extracted from BattleLoadDiagnosticsService.MemStats so the [SaveLoad]
/// campaign-launch phases carry the same vocabulary as the [BattleLoad] ones; the output is
/// byte-identical to what [BattleLoad] carried before the extraction.
///
/// On reader failure both process tokens are omitted (never a fabricated 0 in a user log).
/// </summary>
internal static class ProcessMemoryTokens
{
    public static string Format()
    {
        string gcStats;
        try
        {
            long heapMb = GC.GetTotalMemory(forceFullCollection: false) / (1024 * 1024);
            gcStats = $"gc={GC.CollectionCount(0)}/{GC.CollectionCount(1)}/{GC.CollectionCount(2)} heapMB={heapMb}";
        }
        catch { gcStats = "gc=<unavailable>"; }

        return MemorySampleReader.TryReadProcess(out long privMb, out long wsMb)
            ? $"{gcStats} privMB={privMb} wsMB={wsMb}"
            : gcStats;
    }
}
