using System.Collections.Generic;

namespace TAOM.Features.Enlistment.Equipment;

/// <summary>
/// In-memory <see cref="IEquipmentIssueLedger"/>. Not persisted — the orchestrator
/// wires this state into the core EnlistmentStore's save section in a later phase;
/// the interface boundary exists so the service never learns which.
/// </summary>
public sealed class InMemoryEquipmentIssueLedger : IEquipmentIssueLedger
{
    private readonly List<string> _issuedItemIds = new List<string>();

    public EnlistmentRank? HighestIssuedRank { get; private set; }

    public IReadOnlyList<string> IssuedItemIds => _issuedItemIds;

    public bool HasIssuedForRank(EnlistmentRank rank)
        => HighestIssuedRank.HasValue && rank <= HighestIssuedRank.Value;

    public void RecordIssue(EnlistmentRank rank, IReadOnlyList<string> itemIds)
    {
        if (!HighestIssuedRank.HasValue || rank > HighestIssuedRank.Value)
            HighestIssuedRank = rank;

        if (itemIds == null)
            return;
        foreach (var itemId in itemIds)
        {
            if (!string.IsNullOrEmpty(itemId))
                _issuedItemIds.Add(itemId);
        }
    }

    public void Reset()
    {
        HighestIssuedRank = null;
        _issuedItemIds.Clear();
    }
}
