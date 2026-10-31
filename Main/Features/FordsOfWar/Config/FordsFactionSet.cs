using System;
using System.Collections.Generic;
using TAOM.Features.FordsOfWar.Domain;

namespace TAOM.Features.FordsOfWar;

/// <summary>The factions of factions.json in file order, with the raw <c>{=KEY}English</c> display name of each.</summary>
internal sealed class FordsFactionSet
{
    private readonly Dictionary<string, FactionCatalog> _byId = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, string> _names = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<FactionCatalog> _ordered = new();

    public static FordsFactionSet Empty { get; } = new();

    public IReadOnlyList<FactionCatalog> Factions => _ordered;

    public bool TryGet(string factionId, out FactionCatalog faction)
    {
        faction = null!;
        return !string.IsNullOrEmpty(factionId) && _byId.TryGetValue(factionId, out faction!);
    }

    public string DisplayName(string factionId) =>
        !string.IsNullOrEmpty(factionId) && _names.TryGetValue(factionId, out var name) ? name : factionId;

    internal void Add(FactionCatalog faction, string displayName)
    {
        _ordered.Add(faction);
        _byId[faction.FactionId] = faction;
        _names[faction.FactionId] = displayName;
    }
}
