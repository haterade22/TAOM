using System;
using System.Collections.Generic;
using System.Linq;

namespace TAOM.Features.CultureConversion.GarrisonSwap.Domain;

/// <summary>One candidate troop of a culture, reduced to the two axes the mapper matches on.</summary>
public readonly struct CultureTroopCandidate
{
    public readonly string TroopId;
    public readonly int Tier;
    public readonly TroopRole Role;

    public CultureTroopCandidate(string troopId, int tier, TroopRole role)
    {
        TroopId = troopId;
        Tier = tier;
        Role = role;
    }
}

/// <summary>
/// An immutable, engine-free view of one culture's regular troops, addressed by
/// <c>(role, tier)</c>. Built once per culture at the adapter boundary and handed to the pure
/// <see cref="TAOM.Features.CultureConversion.GarrisonSwap.ITroopCultureMapper"/>, which is why the
/// whole class is data and not a lookup service.
///
/// Every candidate list is sorted ordinally by troop id so a given cell always enumerates in the
/// same order. That is what makes the mapper's deterministic pick reproducible across runs,
/// machines and save reloads; do not swap it for a hash-ordered collection.
/// </summary>
public sealed class CultureTroopIndex
{
    private static readonly IReadOnlyList<string> NoCandidates = new string[0];
    private static readonly IReadOnlyList<int> NoTiers = new int[0];
    private static readonly IReadOnlyList<TroopRole> NoRoles = new TroopRole[0];

    private readonly Dictionary<TroopRole, Dictionary<int, IReadOnlyList<string>>> _byRole;
    private readonly Dictionary<TroopRole, IReadOnlyList<int>> _tiersByRole;
    private readonly Dictionary<int, IReadOnlyList<TroopRole>> _rolesByTier;
    private readonly HashSet<string> _allTroopIds;

    public string CultureId { get; }

    /// <summary>Every tier this culture has any troop at, ascending.</summary>
    public IReadOnlyList<int> AllTiers { get; }

    public bool IsEmpty => AllTiers.Count == 0;

    public CultureTroopIndex(string cultureId, IEnumerable<CultureTroopCandidate> candidates)
    {
        CultureId = cultureId;
        _byRole = new Dictionary<TroopRole, Dictionary<int, IReadOnlyList<string>>>();
        _tiersByRole = new Dictionary<TroopRole, IReadOnlyList<int>>();
        _rolesByTier = new Dictionary<int, IReadOnlyList<TroopRole>>();

        // Dedupe on troop id: a malformed index that listed the same troop twice would otherwise
        // bias the deterministic pick toward it.
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var cells = new Dictionary<(TroopRole Role, int Tier), List<string>>();

        foreach (var candidate in candidates ?? Enumerable.Empty<CultureTroopCandidate>())
        {
            if (string.IsNullOrEmpty(candidate.TroopId) || !seen.Add(candidate.TroopId))
                continue;
            var key = (candidate.Role, candidate.Tier);
            if (!cells.TryGetValue(key, out var list))
                cells[key] = list = new List<string>();
            list.Add(candidate.TroopId);
        }

        foreach (var cell in cells)
        {
            cell.Value.Sort(StringComparer.Ordinal);
            if (!_byRole.TryGetValue(cell.Key.Role, out var byTier))
                _byRole[cell.Key.Role] = byTier = new Dictionary<int, IReadOnlyList<string>>();
            byTier[cell.Key.Tier] = cell.Value;
        }

        foreach (var role in _byRole)
            _tiersByRole[role.Key] = role.Value.Keys.OrderBy(t => t).ToList();

        foreach (var group in cells.Keys.GroupBy(k => k.Tier))
            _rolesByTier[group.Key] = group.Select(k => k.Role).Distinct().OrderBy(r => (int)r).ToList();

        AllTiers = cells.Keys.Select(k => k.Tier).Distinct().OrderBy(t => t).ToList();
        _allTroopIds = seen;
    }

    /// <summary>Candidate troop ids in one exact cell, ordinally sorted. Empty when the cell is unpopulated.</summary>
    public IReadOnlyList<string> Candidates(TroopRole role, int tier)
        => _byRole.TryGetValue(role, out var byTier) && byTier.TryGetValue(tier, out var list) ? list : NoCandidates;

    /// <summary>Tiers this culture fields the given role at, ascending. Empty when it has no such troop at all.</summary>
    public IReadOnlyList<int> TiersFor(TroopRole role)
        => _tiersByRole.TryGetValue(role, out var tiers) ? tiers : NoTiers;

    /// <summary>Roles this culture fields at the given tier, in enum order. Empty when the tier is unpopulated.</summary>
    public IReadOnlyList<TroopRole> RolesAt(int tier)
        => _rolesByTier.TryGetValue(tier, out var roles) ? roles : NoRoles;

    /// <summary>
    /// True when this culture already fields the troop. Not the same question as "is this troop
    /// tagged with this culture": a culture that borrows another's line (Lothlorien fields Rivendell
    /// troops) answers true for troops carrying the donor's culture, which is exactly what stops the
    /// mapper churning an already-correct garrison.
    /// </summary>
    public bool Contains(string troopId) => !string.IsNullOrEmpty(troopId) && _allTroopIds.Contains(troopId);
}
