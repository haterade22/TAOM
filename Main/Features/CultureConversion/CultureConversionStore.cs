using System.Collections.Generic;
using TAOM.Core.Logging;
using TAOM.Features.CultureConversion.Domain;

namespace TAOM.Features.CultureConversion;

public class CultureConversionStore : ICultureConversionStore
{
    private readonly Dictionary<string, SettlementConversionRecord> _records = new Dictionary<string, SettlementConversionRecord>();
    private readonly IModLogger _logger;

    public CultureConversionStore(IModLogger logger)
    {
        _logger = logger;
    }

    public int Count => _records.Count;

    public bool TryGet(string settlementId, out SettlementConversionRecord record)
    {
        record = null;
        if (string.IsNullOrEmpty(settlementId))
            return false;
        return _records.TryGetValue(settlementId, out record);
    }

    public void Put(SettlementConversionRecord record)
    {
        if (record == null || string.IsNullOrEmpty(record.SettlementId))
            return;
        _records[record.SettlementId] = record;
    }

    public bool Remove(string settlementId)
    {
        if (string.IsNullOrEmpty(settlementId))
            return false;
        return _records.Remove(settlementId);
    }

    public bool IsConverted(string settlementId)
    {
        if (string.IsNullOrEmpty(settlementId))
            return false;
        return _records.TryGetValue(settlementId, out var record) && record.IsConverted;
    }

    public IReadOnlyCollection<SettlementConversionRecord> GetAll() => _records.Values;

    public void Clear() => _records.Clear();

    public Dictionary<string, string> Serialize()
    {
        var dict = new Dictionary<string, string>(_records.Count);
        foreach (var kvp in _records)
            dict[kvp.Key] = kvp.Value.Serialize();
        return dict;
    }

    public void Deserialize(IReadOnlyDictionary<string, string> data)
    {
        _records.Clear();
        if (data == null)
            return;

        foreach (var kvp in data)
        {
            if (SettlementConversionRecord.TryParse(kvp.Key, kvp.Value, out var record))
                _records[kvp.Key] = record;
            else
                _logger?.LogWarning($"CultureConversionStore: dropped malformed entry for settlementId={kvp.Key}");
        }
    }
}
