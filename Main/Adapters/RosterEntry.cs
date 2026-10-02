namespace TAOM.Adapters;

/// <summary>
/// One character or hero as the faction screen (#704) reads it: the engine object as an opaque
/// <see cref="Source"/> token, plus the values the faction screen's rules decide on.
/// </summary>
public sealed class RosterEntry
{
    public RosterEntry(object source, string id, string name, bool isHero, bool isAlive = true,
        int tier = 0, int level = 0, bool isInfantry = false, bool isRanged = false)
    {
        Source = source;
        Id = id;
        Name = name;
        IsHero = isHero;
        IsAlive = isAlive;
        Tier = tier;
        Level = level;
        IsInfantry = isInfantry;
        IsRanged = isRanged;
    }

    /// <summary>The engine's <c>Hero</c> when <see cref="IsHero"/>, else its <c>CharacterObject</c>.</summary>
    public object Source { get; }

    public string Id { get; }

    /// <summary>The display name in the player's language.</summary>
    public string Name { get; }

    /// <summary>A living hero from a kingdom's clans, copied with his name when picked; otherwise a
    /// character (a named card or a wanderer template), copied without one.</summary>
    public bool IsHero { get; }

    public bool IsAlive { get; }

    public int Tier { get; }

    public int Level { get; }

    public bool IsInfantry { get; }

    public bool IsRanged { get; }
}
