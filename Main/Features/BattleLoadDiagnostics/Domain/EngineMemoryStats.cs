namespace TAOM.Features.BattleLoadDiagnostics.Domain;

/// <summary>
/// One reading of the ENGINE's own memory accounting, as opposed to <see cref="MemorySample"/>'s
/// OS-level view. The two answer different questions: MemorySample says how many bytes the process
/// holds, this says what the engine thinks they are for.
///
/// Every field is nullable-by-convention: a null/empty string is a REAL engine outcome, not a bug.
/// The managed wrappers return null when the native delegate reports failure, so the formatter
/// renders "unavailable" rather than inventing a value — the same never-fabricate rule the
/// [MemSample] process tokens follow.
///
/// The 2026-09-12 live run showed the two string surfaces are one number each in the shipping
/// client ("Application memory size: 5839 MB", "native: 0.00 MB"), so the vertex-buffer and GPU-split
/// counters below are what actually splits the private commit into mesh and texture residency.
/// </summary>
public readonly struct EngineMemoryStats
{
    public EngineMemoryStats(
        string? applicationStatistics,
        string? nativeStatistics,
        int gpuCostMb,
        bool gpuCostRead,
        string? gpuDumpPath,
        int vertexBufferSystemMemory = 0,
        bool vertexBufferRead = false,
        GpuMemorySplit? gpuSplit = null,
        string? gpuDumpMissingPath = null)
    {
        ApplicationStatistics = applicationStatistics;
        NativeStatistics = nativeStatistics;
        GpuCostMb = gpuCostMb;
        GpuCostRead = gpuCostRead;
        GpuDumpPath = gpuDumpPath;
        VertexBufferSystemMemory = vertexBufferSystemMemory;
        VertexBufferRead = vertexBufferRead;
        GpuSplit = gpuSplit;
        GpuDumpMissingPath = gpuDumpMissingPath;
    }

    /// <summary>TaleWorlds.Engine.Utilities.GetApplicationMemoryStatistics(). Null when unavailable.</summary>
    public string? ApplicationStatistics { get; }

    /// <summary>TaleWorlds.Engine.Utilities.GetNativeMemoryStatistics(). Null when unavailable.</summary>
    public string? NativeStatistics { get; }

    /// <summary>GetCurrentEstimatedGPUMemoryCostMB(). Only meaningful when <see cref="GpuCostRead"/>.</summary>
    public int GpuCostMb { get; }

    /// <summary>False when the GPU cost call threw — the token is then omitted, never printed as 0.</summary>
    public bool GpuCostRead { get; }

    /// <summary>
    /// Absolute path of a GPU dump that EXISTS on disk after DumpGPUMemoryStatistics returned, or null.
    /// The engine call is void and the shipping client has been observed to write nothing, so a path
    /// is only reported here once the file has been seen.
    /// </summary>
    public string? GpuDumpPath { get; }

    /// <summary>
    /// The path a dump was requested at when no file appeared there (or the call threw). Null when no
    /// dump was requested or when it succeeded. Lets the report say "asked, nothing came" instead of
    /// claiming a file that does not exist.
    /// </summary>
    public string? GpuDumpMissingPath { get; }

    /// <summary>
    /// GetVertexBufferChunkSystemMemoryUsage(): the engine's CPU-side vertex buffer chunks, i.e. mesh
    /// geometry resident in system memory. Raw engine units, only meaningful when
    /// <see cref="VertexBufferRead"/>.
    /// </summary>
    public int VertexBufferSystemMemory { get; }

    /// <summary>False when the vertex-buffer call threw: the token is then omitted, never printed as 0.</summary>
    public bool VertexBufferRead { get; }

    /// <summary>GetGPUMemoryStats() split, or null when the call threw.</summary>
    public GpuMemorySplit? GpuSplit { get; }
}
