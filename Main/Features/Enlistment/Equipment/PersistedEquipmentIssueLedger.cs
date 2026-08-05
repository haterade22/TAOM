using System.Collections.Generic;
using System.Linq;
using TAOM.Features.Enlistment.Content;

namespace TAOM.Features.Enlistment.Equipment;

/// <summary>
/// The shipping ledger: issuance state lives in the content record, so it round-trips
/// through <c>_taom_enlistment_content</c> SyncData. The in-memory implementation survived
/// only until the content store existed — without persistence a full game restart reset
/// the ledger and re-allowed one free kit draw per rank.
/// </summary>
public class PersistedEquipmentIssueLedger : IEquipmentIssueLedger
{
    private readonly IEnlistmentContentStore _contentStore;

    public PersistedEquipmentIssueLedger(IEnlistmentContentStore contentStore)
    {
        _contentStore = contentStore;
    }

    public EnlistmentRank? HighestIssuedRank
    {
        get
        {
            var raw = _contentStore.Record.HighestIssuedEquipmentRank;
            if (!raw.HasValue)
                return null;
            // Defensive: a hand-edited save could carry a rank ordinal we don't define.
            return System.Enum.IsDefined(typeof(EnlistmentRank), raw.Value)
                ? (EnlistmentRank?)raw.Value
                : null;
        }
    }

    public IReadOnlyList<string> IssuedItemIds => _contentStore.Record.IssuedEquipmentItemIds;

    public bool HasIssuedForRank(EnlistmentRank rank)
    {
        var highest = HighestIssuedRank;
        // Monotonic: covering a rank covers everything below it, so a demotion never re-issues.
        return highest.HasValue && (int)highest.Value >= (int)rank;
    }

    public void RecordIssue(EnlistmentRank rank, IReadOnlyList<string> itemIds)
    {
        var record = _contentStore.Record;
        var highest = HighestIssuedRank;
        if (!highest.HasValue || (int)rank > (int)highest.Value)
            record.HighestIssuedEquipmentRank = (int)rank;

        if (itemIds != null && itemIds.Count > 0)
            record.IssuedEquipmentItemIds.AddRange(itemIds.Where(id => !string.IsNullOrEmpty(id)));
    }

    public void Reset()
    {
        var record = _contentStore.Record;
        record.HighestIssuedEquipmentRank = null;
        record.IssuedEquipmentItemIds.Clear();
    }
}
