using System.Collections.Generic;

namespace TAOM.Features.EconomyDiagnostics;

/// <summary>
/// The ledger surface the recorder patch writes to and the console command reads from.
/// TaleWorlds-free: ids cross the boundary as strings (ADR-007).
/// </summary>
public interface ITownGoldLedger
{
    void Record(string settlementId, TownGoldFlow flow, int delta);

    /// <summary>Rolls every town onto a fresh bucket and evicts anything past the retention window.</summary>
    void BeginDay();

    /// <summary>Newest day first. Empty for a settlement that has never moved gold.</summary>
    IReadOnlyList<TownGoldDay> GetHistory(string settlementId);

    /// <summary>Per-flow sums across the whole retained window.</summary>
    TownGoldDay GetTotals(string settlementId);

    void ClearAll();
}
