using System.Collections.Generic;

namespace TAOM.Features.Enlistment.Equipment;

/// <summary>
/// Issuance state: the highest rank already issued (once-per-rank is MONOTONIC —
/// covering a rank covers every rank below it, so a demotion never re-issues)
/// plus one entry per physical item instance issued (duplicates preserved so the
/// discharge can reclaim each one). <c>PersistedEquipmentIssueLedger</c> is the
/// production implementation.
/// </summary>
public interface IEquipmentIssueLedger
{
    EnlistmentKitTier? HighestIssuedRank { get; }

    /// <summary>One entry per issued item INSTANCE — the same id twice means two items.</summary>
    IReadOnlyList<string> IssuedItemIds { get; }

    bool HasIssuedForRank(EnlistmentKitTier rank);

    void RecordIssue(EnlistmentKitTier rank, IReadOnlyList<string> itemIds);

    void Reset();
}
