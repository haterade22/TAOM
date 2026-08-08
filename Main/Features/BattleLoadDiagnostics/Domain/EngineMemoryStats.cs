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
/// </summary>
public readonly struct EngineMemoryStats
{
    public EngineMemoryStats(
        string? applicationStatistics,
        string? nativeStatistics,
        int gpuCostMb,
        bool gpuCostRead,
        string? gpuDumpPath)
    {
        ApplicationStatistics = applicationStatistics;
        NativeStatistics = nativeStatistics;
        GpuCostMb = gpuCostMb;
        GpuCostRead = gpuCostRead;
        GpuDumpPath = gpuDumpPath;
    }

    /// <summary>TaleWorlds.Engine.Utilities.GetApplicationMemoryStatistics(). Null when unavailable.</summary>
    public string? ApplicationStatistics { get; }

    /// <summary>TaleWorlds.Engine.Utilities.GetNativeMemoryStatistics(). Null when unavailable.</summary>
    public string? NativeStatistics { get; }

    /// <summary>GetCurrentEstimatedGPUMemoryCostMB(). Only meaningful when <see cref="GpuCostRead"/>.</summary>
    public int GpuCostMb { get; }

    /// <summary>False when the GPU cost call threw — the token is then omitted, never printed as 0.</summary>
    public bool GpuCostRead { get; }

    /// <summary>Absolute path written by DumpGPUMemoryStatistics, or null when not requested/failed.</summary>
    public string? GpuDumpPath { get; }
}
