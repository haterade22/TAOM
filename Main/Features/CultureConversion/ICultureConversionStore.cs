using System.Collections.Generic;
using TAOM.Features.CultureConversion.Domain;

namespace TAOM.Features.CultureConversion;

/// <summary>
/// Persistent store of per-settlement culture-conversion records. Single source of truth shared by
/// the behavior (SyncData), the service (logic), and the recruitment context adapter (<see cref="IsConverted"/>).
/// Mirrors <c>IMessengerStateStore</c> — owns the save serialization format.
/// </summary>
public interface ICultureConversionStore
{
    bool TryGet(string settlementId, out SettlementConversionRecord record);
    void Put(SettlementConversionRecord record);
    bool Remove(string settlementId);

    /// <summary>True when the settlement has an applied culture override (used by recruitment).</summary>
    bool IsConverted(string settlementId);

    IReadOnlyCollection<SettlementConversionRecord> GetAll();
    int Count { get; }
    void Clear();

    Dictionary<string, string> Serialize();
    void Deserialize(IReadOnlyDictionary<string, string> data);
}
