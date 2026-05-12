using System;

namespace TAOM.Features.EditorCacheRebuild;

public readonly struct CacheElementKey : IEquatable<CacheElementKey>
{
    public readonly string SettlementId;
    public readonly bool IsPortUsed;

    public CacheElementKey(string settlementId, bool isPortUsed)
    {
        SettlementId = settlementId;
        IsPortUsed = isPortUsed;
    }

    public bool Equals(CacheElementKey other) =>
        SettlementId == other.SettlementId && IsPortUsed == other.IsPortUsed;

    public override bool Equals(object obj) =>
        obj is CacheElementKey other && Equals(other);

    public override int GetHashCode()
    {
        unchecked
        {
            return ((SettlementId?.GetHashCode() ?? 0) * 397) ^ (IsPortUsed ? 1 : 0);
        }
    }

    public override string ToString() =>
        IsPortUsed ? $"{SettlementId}(port)" : SettlementId;
}
