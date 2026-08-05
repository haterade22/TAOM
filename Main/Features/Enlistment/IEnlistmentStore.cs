using System.Collections.Generic;
using TAOM.Features.Enlistment.Domain;

namespace TAOM.Features.Enlistment;

/// <summary>
/// Owns the persisted enlistment record. Serializes to primitive string keys for the
/// campaign behavior's SyncData (TAOM convention: no SaveableTypeDefiner).
/// </summary>
public interface IEnlistmentStore
{
    /// <summary>The live record. The instance is stable for the store's lifetime — Clear resets it in place.</summary>
    EnlistmentRecord Record { get; }

    Dictionary<string, string> Serialize();

    /// <summary>
    /// Restore from persisted keys. Null/empty data (fresh campaign) resets silently;
    /// an unknown version or malformed core resets with a warning — never leaves a
    /// partially-restored record.
    /// </summary>
    void Deserialize(IReadOnlyDictionary<string, string> data);

    void Clear();
}
