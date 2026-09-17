using System;
using System.Collections.Generic;

namespace TAOM.Features.CultureDoctrine.Domain;

/// <summary>One authored row: a tactic, its weight multiplier, the commander Tactics skill it
/// needs, and the side it applies to. Immutable; a copy is captured by every wrapper the team
/// gets, so nothing on the async AI thread ever sees a change.</summary>
public sealed class TacticEntry
{
    public TacticEntry(DoctrineTactic tactic, float multiplier, int minTactics, DoctrineSide side)
    {
        Tactic = tactic;
        Multiplier = multiplier;
        MinTactics = minTactics;
        Side = side;
    }

    public DoctrineTactic Tactic { get; }
    public float Multiplier { get; }
    public int MinTactics { get; }
    public DoctrineSide Side { get; }
}

/// <summary>A culture's battle doctrine: the tactic rows it registers instead of vanilla's set.</summary>
public sealed class Doctrine
{
    public Doctrine(string cultureId, IReadOnlyList<TacticEntry> tactics, bool isDefault)
    {
        CultureId = cultureId ?? throw new ArgumentNullException(nameof(cultureId));
        Tactics = tactics ?? throw new ArgumentNullException(nameof(tactics));
        IsDefault = isDefault;
    }

    public string CultureId { get; }
    public IReadOnlyList<TacticEntry> Tactics { get; }

    /// <summary>True for the catalog's fallback, the doctrine a culture without a row receives.</summary>
    public bool IsDefault { get; }
}
