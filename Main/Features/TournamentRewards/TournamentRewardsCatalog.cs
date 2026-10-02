using System;
using System.Collections.Generic;

namespace TAOM.Features.TournamentRewards;

/// <summary>One culture's tournament reward factors: 1 is vanilla.</summary>
public sealed class TournamentCultureFactors
{
    public static readonly TournamentCultureFactors Neutral = new(1f, 1f, 1f);

    public TournamentCultureFactors(float renown, float influence, float skillXp)
    {
        Renown = renown;
        Influence = influence;
        SkillXp = skillXp;
    }

    /// <summary>Scales a tournament winner's renown, by the winner's culture.</summary>
    public float Renown { get; }

    /// <summary>Scales a tournament winner's influence, by the winner's culture.</summary>
    public float Influence { get; }

    /// <summary>Scales the player's tournament skill XP, by the player's culture.</summary>
    public float SkillXp { get; }
}

/// <summary>The validated per-culture factors from tournament_rewards.json, with the default row as fallback.</summary>
public sealed class TournamentRewardsCatalog
{
    private readonly IReadOnlyDictionary<string, TournamentCultureFactors> _byCulture;

    public TournamentRewardsCatalog(IReadOnlyDictionary<string, TournamentCultureFactors> byCulture, TournamentCultureFactors fallback)
    {
        _byCulture = byCulture;
        Default = fallback;
    }

    public static TournamentRewardsCatalog AllNeutral() =>
        new(new Dictionary<string, TournamentCultureFactors>(), TournamentCultureFactors.Neutral);

    public TournamentCultureFactors Default { get; }

    /// <summary>The culture ids with their own row, the default excluded.</summary>
    public IEnumerable<string> CultureIds => _byCulture.Keys;

    public TournamentCultureFactors For(string? cultureId)
    {
        if (string.IsNullOrEmpty(cultureId))
            return Default;
        return _byCulture.TryGetValue(cultureId!.Trim().ToLowerInvariant(), out var factors) ? factors : Default;
    }
}
