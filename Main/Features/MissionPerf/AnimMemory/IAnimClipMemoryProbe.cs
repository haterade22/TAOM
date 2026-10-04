namespace TAOM.Features.MissionPerf.AnimMemory;

/// <summary>
/// The engine's on-demand animation clip byte total and its budget, found once per process. Public
/// because the session tests fake it.
/// </summary>
public interface IAnimClipMemoryProbe
{
    /// <summary>First call finds the engine's counter and budget and logs the result; later calls return it.</summary>
    bool EnsureArmed();

    int BudgetBytes { get; }

    /// <summary>
    /// The engine's on-demand clip byte total. Only after EnsureArmed returned true. A negative value
    /// means the probe has turned itself off for the process and logged why.
    /// </summary>
    int ReadLoadedBytes();

    /// <summary>
    /// The engine's own query, true while any clip is in the loading state at the moment of the call. A
    /// point observation: a load that finished since the last call is not seen. Outside the probe's
    /// address checks (see <see cref="AnimMemoryProbe"/>).
    /// </summary>
    bool IsAnyClipLoading();
}
