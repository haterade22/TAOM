using System.Collections.Generic;
using TAOM.Adapters;
using TAOM.Features.FiefManagement.Models;

namespace TAOM.Features.FiefManagement;

public class FiefHubService : IFiefHubService
{
    private readonly ISettlementOwnershipAdapter _ownership;

    public FiefHubService(ISettlementOwnershipAdapter ownership)
    {
        _ownership = ownership;
    }

    public IReadOnlyList<FiefSummary> GetOrderedFiefs()
    {
        var raw = _ownership.GetPlayerOwnedFiefs();
        var towns = new List<FiefSummary>();
        var castles = new List<FiefSummary>();
        foreach (var s in raw)
        {
            if (s == null) continue;
            if (s.IsTown) towns.Add(s);
            else if (s.IsCastle) castles.Add(s);
        }

        towns.Sort(CompareByName);
        castles.Sort(CompareByName);

        var result = new List<FiefSummary>(towns.Count + castles.Count);
        result.AddRange(towns);
        result.AddRange(castles);
        return result;
    }

    // Fast path: delegates to the adapter, which iterates Clan.PlayerClan.Settlements (small
    // cached list) instead of Settlement.All (~862 entries) and skips FiefSummary construction.
    // Patch36_MapScreenF6.Postfix polls this every frame for the empty-fief gate; Clamp / Next /
    // Previous benefit transparently. The slow `GetOrderedFiefs()` path is still used by the
    // presenter's Refresh() when it actually needs the ordered list. Audit issue #143.
    public int Count => _ownership.GetPlayerOwnedFiefCount();

    public int Clamp(int index)
    {
        var count = Count;
        if (count <= 0) return 0;
        if (index < 0) return 0;
        if (index >= count) return count - 1;
        return index;
    }

    public int Next(int index)
    {
        var count = Count;
        if (count <= 0) return 0;
        return ((index + 1) % count + count) % count;
    }

    public int Previous(int index)
    {
        var count = Count;
        if (count <= 0) return 0;
        return ((index - 1) % count + count) % count;
    }

    public FiefSummary GetAt(int index)
    {
        var fiefs = GetOrderedFiefs();
        if (fiefs.Count == 0) return null;
        var clamped = Clamp(index);
        return fiefs[clamped];
    }

    public bool PlayerIsAt(FiefSummary fief)
    {
        if (fief == null) return false;
        return _ownership.IsPlayerCurrentlyAt(fief.Id);
    }

    private static int CompareByName(FiefSummary a, FiefSummary b) =>
        string.Compare(a.Name, b.Name, System.StringComparison.OrdinalIgnoreCase);
}
