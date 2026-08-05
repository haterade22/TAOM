using System.Collections.Generic;
using TAOM.Core.Logging;
using TAOM.Features.Enlistment.Domain;

namespace TAOM.Features.Enlistment;

public class EnlistmentStore : IEnlistmentStore
{
    public const string VersionKey = "_taom_enlistment_v";
    public const string CoreKey = "_taom_enlistment_core";
    private const string CurrentVersion = "1";

    private readonly EnlistmentRecord _record = new EnlistmentRecord();
    private readonly IModLogger _logger;

    public EnlistmentStore(IModLogger logger)
    {
        _logger = logger;
    }

    public EnlistmentRecord Record => _record;

    public Dictionary<string, string> Serialize()
    {
        if (_record.State == EnlistmentState.Discharging)
        {
            // The discharge pipeline is synchronous; reaching a save mid-Discharging means
            // it was interrupted. Record-level serialization coerces to EnlistedAttached
            // and load normalization finishes the job — but surface the anomaly loudly.
            _logger?.LogError("EnlistmentStore: serializing while Discharging — pipeline was interrupted; coercing to EnlistedAttached");
        }

        return new Dictionary<string, string>(2)
        {
            [VersionKey] = CurrentVersion,
            [CoreKey] = _record.Serialize(),
        };
    }

    public void Deserialize(IReadOnlyDictionary<string, string> data)
    {
        if (data == null || data.Count == 0)
        {
            // Fresh campaign / feature never saved — silent reset.
            _record.Reset();
            return;
        }

        if (!data.TryGetValue(VersionKey, out var version) || version != CurrentVersion)
        {
            _logger?.LogWarning(
                $"EnlistmentStore: unknown save version '{version ?? "<missing>"}' — dropping record (fail-safe NotEnlisted); load normalization will restore party presence if parked");
            _record.Reset();
            return;
        }

        if (!data.TryGetValue(CoreKey, out var core) || !EnlistmentRecord.TryParse(core, out var parsed))
        {
            _logger?.LogWarning(
                "EnlistmentStore: missing or malformed core record — dropping record (fail-safe NotEnlisted); load normalization will restore party presence if parked");
            _record.Reset();
            return;
        }

        _record.CopyFrom(parsed);
    }

    public void Clear() => _record.Reset();
}
