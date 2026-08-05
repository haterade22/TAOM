using System.Collections.Generic;

namespace TAOM.Features.Enlistment.Equipment;

/// <summary>
/// Issuance state: the highest rank already issued (once-per-rank is MONOTONIC —
/// covering a rank covers every rank below it, so a demotion never re-issues)
/// plus one entry per physical item instance issued (duplicates preserved for
/// the discharge payoff Σ). In-memory impl ships now; the orchestrator wires
/// persistence into the core EnlistmentStore in a later phase.
/// </summary>
public interface IEquipmentIssueLedger
{
    EnlistmentRank? HighestIssuedRank { get; }

    /// <summary>One entry per issued item INSTANCE — the same id twice means two items.</summary>
    IReadOnlyList<string> IssuedItemIds { get; }

    bool HasIssuedForRank(EnlistmentRank rank);

    void RecordIssue(EnlistmentRank rank, IReadOnlyList<string> itemIds);

    void Reset();
}
