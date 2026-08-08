using TAOM.Features.BattleLoadDiagnostics.Domain;

namespace TAOM.Features.BattleLoadDiagnostics;

/// <summary>
/// ADR-007 boundary over the four static <c>TaleWorlds.Engine.Utilities</c> memory calls. They are
/// statics on an engine type, so nothing above this interface may touch them directly.
///
/// Why this exists: #385 killed a tester at 20.3 GB process commit with the managed heap at ~654 MB,
/// i.e. the weight is native — and a 2026-08-07 session measured 12.1 -> 20.4 GB across 27 minutes
/// with the heap nearly flat. [MemSample] can say HOW MANY bytes; only the engine can say what
/// category they belong to. Nothing in TAOM called these before.
///
/// DELIBERATELY ABSENT: Utilities.GetMemoryUsageOfCategory(int). There is no category-count API, no
/// category-name API, no enum, and no managed caller anywhere in the shipping or editor build — the
/// index goes straight through to native with no validation, so a blind walk is an access-violation
/// risk in a diagnostic. GetApplicationMemoryStatistics/GetNativeMemoryStatistics are very likely
/// already the breakdown; read them once before deciding a numeric probe is needed at all, and if
/// one is, give it its own opt-in command with a clamped bound.
/// </summary>
public interface IEngineMemoryStatsReader
{
    /// <summary>
    /// Reads the engine's memory accounting. Never throws: each underlying call is guarded
    /// individually, so one failing surface does not cost the others.
    /// </summary>
    /// <param name="gpuDumpDirectory">
    /// When non-null, also asks the engine to write a GPU memory dump into this directory. The path
    /// is constructed by the caller, never taken from user input.
    /// </param>
    EngineMemoryStats Read(string? gpuDumpDirectory = null);
}
