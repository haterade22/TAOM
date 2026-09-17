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

/// <summary>A culture's battle doctrine: the tactic rows it registers instead of vanilla's set,
/// how its soldiers hold under fear (<see cref="Morale"/>), how they fight in the melee
/// (<see cref="Aggression"/>) and which named troops spawn into a formation of their own
/// (<see cref="Formations"/>). The three-argument constructor keeps the vanilla blocks.</summary>
public sealed class Doctrine
{
    public Doctrine(string cultureId, IReadOnlyList<TacticEntry> tactics, bool isDefault)
        : this(cultureId, tactics, isDefault, CultureMorale.Vanilla, CultureAggression.Vanilla, FormationRouting.None)
    {
    }

    public Doctrine(string cultureId, IReadOnlyList<TacticEntry> tactics, bool isDefault, CultureMorale morale, CultureAggression aggression, FormationRouting formations)
    {
        CultureId = cultureId ?? throw new ArgumentNullException(nameof(cultureId));
        Tactics = tactics ?? throw new ArgumentNullException(nameof(tactics));
        IsDefault = isDefault;
        Morale = morale ?? throw new ArgumentNullException(nameof(morale));
        Aggression = aggression ?? throw new ArgumentNullException(nameof(aggression));
        Formations = formations ?? throw new ArgumentNullException(nameof(formations));
    }

    public string CultureId { get; }
    public IReadOnlyList<TacticEntry> Tactics { get; }

    /// <summary>True for the catalog's fallback, the doctrine a culture without a row receives.</summary>
    public bool IsDefault { get; }

    public CultureMorale Morale { get; }
    public CultureAggression Aggression { get; }
    public FormationRouting Formations { get; }
}
