using System.Collections.Generic;
using TAOM.Features.AutoResolveDiagnostics.Domain;

namespace TAOM.Adapters;

/// <summary>
/// Reads every troop type's simulation-relevant stats from the live engine, so the offline
/// analyzer's derivations can be checked against ground truth instead of trusted.
/// </summary>
public interface ITroopCensusAdapter
{
    /// <summary>
    /// One record per CharacterObject. Returns an empty list rather than throwing — a diagnostic
    /// must not propagate into session launch.
    /// </summary>
    IReadOnlyList<TroopCensusRecord> Capture();
}
