namespace TAOM.Features.BattleLoadDiagnostics;

/// <summary>
/// Counts the engine's <c>compile_shader</c> lines as they reach its log: the only compile evidence a
/// background thread may read, because the native counter is main-thread only. Called from one timer
/// thread. Public so the watchdog tests can substitute it (DynamicProxy cannot see internals).
/// </summary>
public interface IRglCompileTail
{
    bool Available { get; }

    string? Path { get; }

    /// <summary>Remember the current end of the file and reset the count.</summary>
    void MarkFresh();

    /// <summary>Read appended bytes, at most 4 MB per call, and count compile_shader.</summary>
    void ReadNew(long nowUtcTicks);

    int CompilesSinceFresh { get; }

    double? SecondsSinceLastCompile(long nowUtcTicks);
}
