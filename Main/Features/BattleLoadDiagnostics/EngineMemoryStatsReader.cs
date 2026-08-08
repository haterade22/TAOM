using System;
using System.IO;
using TaleWorlds.Engine;
using TAOM.Features.BattleLoadDiagnostics.Domain;

namespace TAOM.Features.BattleLoadDiagnostics;

/// <summary>
/// Reads TaleWorlds.Engine.Utilities' memory surfaces. Signatures verified against the installed
/// v1.4.7 engine: GetApplicationMemoryStatistics() and GetNativeMemoryStatistics() return string,
/// GetCurrentEstimatedGPUMemoryCostMB() returns int, DumpGPUMemoryStatistics(string) returns void.
///
/// Each call is guarded SEPARATELY rather than under one try: these cross a native boundary, and one
/// unavailable surface must not blank the other three. A null result is passed through untouched
/// because the managed wrapper genuinely returns null when the native side reports failure —
/// substituting an empty string would erase the difference between "the engine said nothing" and
/// "the engine said the empty string".
/// </summary>
public sealed class EngineMemoryStatsReader : IEngineMemoryStatsReader
{
    public EngineMemoryStats Read(string? gpuDumpDirectory = null)
    {
        string? app = null;
        try { app = Utilities.GetApplicationMemoryStatistics(); }
        catch { /* native surface unavailable — render as such, never fabricate */ }

        string? native = null;
        try { native = Utilities.GetNativeMemoryStatistics(); }
        catch { /* as above */ }

        var gpuCostMb = 0;
        var gpuCostRead = false;
        try
        {
            gpuCostMb = Utilities.GetCurrentEstimatedGPUMemoryCostMB();
            gpuCostRead = true;
        }
        catch { /* leave gpuCostRead false so the formatter omits the token rather than printing 0 */ }

        string? dumpPath = null;
        if (!string.IsNullOrEmpty(gpuDumpDirectory))
        {
            try
            {
                Directory.CreateDirectory(gpuDumpDirectory);
                // Fully qualified: TaleWorlds.Engine also defines a Path type, so the bare name is
                // ambiguous in any file that uses the engine namespace.
                // Name is fully constructed here — never interpolated from console input.
                var candidate = System.IO.Path.GetFullPath(
                    System.IO.Path.Combine(gpuDumpDirectory, $"taom_gpu_memory_{DateTime.Now:yyyy-MM-dd_HH-mm-ss}.txt"));
                Utilities.DumpGPUMemoryStatistics(candidate);
                dumpPath = candidate;
            }
            catch { /* the dump is a bonus; its failure must not cost the three readings above */ }
        }

        return new EngineMemoryStats(app, native, gpuCostMb, gpuCostRead, dumpPath);
    }
}
