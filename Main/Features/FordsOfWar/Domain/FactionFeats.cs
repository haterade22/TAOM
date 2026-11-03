namespace TAOM.Features.FordsOfWar.Domain;

/// <summary>
/// What a faction does better or worse than the rules' defaults. Every factor is relative to 1 (no change). A tower damage factor of 0
/// means no damage at all, where the table's other factors never go below 1 point; a TowerFireRate of 0 keeps the faction's towers silent.
/// </summary>
internal sealed record FactionFeats
{
    public static FactionFeats None { get; } = new();

    /// <summary>Volleys per second of this faction's towers.</summary>
    public float TowerFireRate { get; init; } = 1f;

    /// <summary>Hit points of this faction's towers and keep.</summary>
    public float TowerHitPoints { get; init; } = 1f;

    /// <summary>Damage this faction's units deal to enemy towers and keeps.</summary>
    public float DamageToTowers { get; init; } = 1f;

    /// <summary>A further factor on that damage for its cavalry and horse archers.</summary>
    public float MountedDamageToTowers { get; init; } = 1f;

    /// <summary>The time between this faction's free waves.</summary>
    public float WaveInterval { get; init; } = 1f;

    /// <summary>Units this faction may field above the side cap; the lane cap rises by this over the number of lanes.</summary>
    public int UnitCapBonus { get; init; }

    /// <summary>This faction's passive income.</summary>
    public float Income { get; init; } = 1f;

    /// <summary>The gold this faction's kills pay.</summary>
    public float Bounty { get; init; } = 1f;
}

/// <summary>One line that tells a player what a faction is good or bad at. Text is a <c>{=TAOM_FOW_...}English</c> string.</summary>
internal sealed record FactionTrait(string Text, bool Strength);
