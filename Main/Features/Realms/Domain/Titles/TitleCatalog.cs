using System.Collections.Generic;
using System.Linq;

namespace TAOM.Features.Realms.Domain.Titles;

/// <summary>
/// The validated de jure hierarchy. Built once per campaign and read-only afterwards.
/// Invalid input never throws: a duplicate id is dropped, and a missing or non-higher parent is
/// detached (the title becomes a root). Each repair is reported in <see cref="Problems"/>.
/// </summary>
public sealed class TitleCatalog
{
    private readonly Dictionary<string, TitleDefinition> _byId = new();
    private readonly Dictionary<string, List<string>> _children = new();
    private readonly Dictionary<string, string> _bySettlement = new();
    private readonly List<TitleDefinition> _ordered = new();
    private readonly List<string> _problems = new();

    public static readonly TitleCatalog Empty = new(new TitleDefinition[0]);

    public TitleCatalog(IEnumerable<TitleDefinition> definitions)
    {
        var accepted = new List<TitleDefinition>();
        var seen = new HashSet<string>();
        foreach (var definition in definitions)
        {
            if (!seen.Add(definition.Id))
            {
                _problems.Add($"Duplicate title id '{definition.Id}' ignored.");
                continue;
            }

            accepted.Add(definition);
        }

        var ranks = accepted.ToDictionary(d => d.Id, d => d.Rank);
        foreach (var definition in accepted)
            Add(Repair(definition, ranks));
    }

    public int Count => _ordered.Count;
    public IReadOnlyList<TitleDefinition> All => _ordered;
    public IReadOnlyList<string> Problems => _problems;

    public TitleDefinition? Get(string? id) =>
        id != null && _byId.TryGetValue(id, out var definition) ? definition : null;

    public TitleDefinition? ForSettlement(string? settlementId) =>
        settlementId != null && _bySettlement.TryGetValue(settlementId, out var id) ? _byId[id] : null;

    public IReadOnlyList<TitleDefinition> ChildrenOf(string id) =>
        _children.TryGetValue(id, out var ids) ? ids.Select(c => _byId[c]).ToList() : new List<TitleDefinition>();

    /// <summary>The parent chain from the direct parent upwards. Strictly rising ranks guarantee it ends.</summary>
    public IEnumerable<TitleDefinition> AncestorsOf(string id)
    {
        var current = Get(Get(id)?.ParentId);
        while (current != null)
        {
            yield return current;
            current = Get(current.ParentId);
        }
    }

    private TitleDefinition Repair(TitleDefinition definition, IReadOnlyDictionary<string, TitleRank> ranks)
    {
        if (definition.ParentId == null)
            return definition;
        if (!ranks.TryGetValue(definition.ParentId, out var parentRank))
        {
            _problems.Add($"Title '{definition.Id}' names missing parent '{definition.ParentId}'; detached.");
            return definition.WithParent(null);
        }

        if (parentRank <= definition.Rank)
        {
            _problems.Add($"Title '{definition.Id}' ({definition.Rank}) cannot sit under '{definition.ParentId}' ({parentRank}); detached.");
            return definition.WithParent(null);
        }

        return definition;
    }

    private void Add(TitleDefinition definition)
    {
        _byId[definition.Id] = definition;
        _ordered.Add(definition);
        if (definition.ParentId != null)
        {
            if (!_children.TryGetValue(definition.ParentId, out var list))
                _children[definition.ParentId] = list = new List<string>();
            list.Add(definition.Id);
        }

        if (definition.SettlementId == null)
            return;
        if (_bySettlement.ContainsKey(definition.SettlementId))
            _problems.Add($"Settlement '{definition.SettlementId}' already has a title; '{definition.Id}' is not indexed by it.");
        else
            _bySettlement[definition.SettlementId] = definition.Id;
    }
}
