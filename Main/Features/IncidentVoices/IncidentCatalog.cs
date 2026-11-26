using System;
using System.Collections.Generic;
using TAOM.Features.IncidentVoices.Models;

namespace TAOM.Features.IncidentVoices;

/// <summary>The validated <c>incidents_lotr/incidents_lotr.json</c>: the rule of every vanilla incident id, and the side vocabulary words.</summary>
internal sealed class IncidentCatalog
{
    public static readonly IncidentCatalog Empty = new(new Dictionary<string, IncidentRule>(), new string[0]);

    private readonly Dictionary<string, IncidentRule> _rules;

    public IncidentCatalog(Dictionary<string, IncidentRule> rules, IReadOnlyList<string> vocabulary)
    {
        _rules = rules;
        Vocabulary = vocabulary;
    }

    /// <summary>The words every side text may use as <c>{WORD}</c>: set on the incident's text objects before it shows.</summary>
    public IReadOnlyList<string> Vocabulary { get; }

    public IReadOnlyCollection<IncidentRule> Rules => _rules.Values;

    public IncidentRule? Find(string? id) => id != null && _rules.TryGetValue(id, out var rule) ? rule : null;

    /// <summary>The rule for an id typed as <c>incident_x</c> or just <c>x</c>, any case.</summary>
    public IncidentRule? FindLoose(string? typed)
    {
        var id = typed?.Trim().ToLowerInvariant();
        if (string.IsNullOrEmpty(id))
            return null;
        return Find(id) ?? Find(IncidentRule.IdPrefix + id);
    }
}

/// <summary>The catalog, loaded once per process.</summary>
internal interface IIncidentCatalogProvider
{
    IncidentCatalog GetCatalog();
}
