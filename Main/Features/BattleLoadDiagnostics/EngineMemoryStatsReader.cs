using System;
using System.IO;
using TaleWorlds.Engine;
using TAOM.Features.BattleLoadDiagnostics.Domain;

namespace TAOM.Features.BattleLoadDiagnostics;

/// <summary>
/// Reads TaleWorlds.Engine.Utilities' memory surfaces. Signatures verified against the installed
/// v1.4.8 engine: GetApplicationMemoryStatistics() and GetNativeMemoryStatistics() return string,
/// GetCurrentEstimatedGPUMemoryCostMB() returns int, DumpGPUMemoryStatistics(string) returns void,
/// GetVertexBufferChunkSystemMemoryUsage() returns int, GetGPUMemoryStats takes five ref floats.
///
/// Each call is guarded SEPARATELY rather than under one try: these cross a native boundary, and one
/// unavailable surface must not blank the others. A null result is passed through untouched
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

        var vertexBuffer = 0;
        var vertexBufferRead = false;
        try
        {
            vertexBuffer = Utilities.GetVertexBufferChunkSystemMemoryUsage();
            vertexBufferRead = true;
        }
        catch { /* as above */ }

        GpuMemorySplit? split = null;
        try
        {
            float total = 0f, renderTarget = 0f, depthTarget = 0f, srv = 0f, buffer = 0f;
            Utilities.GetGPUMemoryStats(ref total, ref renderTarget, ref depthTarget, ref srv, ref buffer);
            split = new GpuMemorySplit(total, renderTarget, depthTarget, srv, buffer);
        }
        catch { /* as above */ }

        string? dumpPath = null;
        string? dumpMissingPath = null;
        if (!string.IsNullOrEmpty(gpuDumpDirectory))
        {
            // Set the "requested" marker before anything that can throw, so a failure in path
            // construction or directory creation still reports "asked, nothing came" rather than
            // looking like no dump was requested at all.
            dumpMissingPath = gpuDumpDirectory;
            try
            {
                // Fully qualified: TaleWorlds.Engine also defines a Path type, so the bare name is
                // ambiguous in any file that uses the engine namespace.
                // Name is fully constructed here — never interpolated from console input.
                var candidate = System.IO.Path.GetFullPath(
                    System.IO.Path.Combine(gpuDumpDirectory, $"taom_gpu_memory_{DateTime.Now:yyyy-MM-dd_HH-mm-ss}.txt"));
                dumpMissingPath = candidate;
                Directory.CreateDirectory(gpuDumpDirectory);
                Utilities.DumpGPUMemoryStatistics(candidate);
                // The call is void and the shipping client has been observed to write nothing
                // (2026-09-12 live run: path reported, no file anywhere). Only a file that exists is
                // a dump; anything else is reported as requested-but-absent.
                if (File.Exists(candidate))
                {
                    dumpPath = candidate;
                    dumpMissingPath = null;
                }
            }
            catch { /* the dump is a bonus; its failure must not cost the readings above */ }
        }

        return new EngineMemoryStats(app, native, gpuCostMb, gpuCostRead, dumpPath,
            vertexBuffer, vertexBufferRead, split, dumpMissingPath);
    }
}
