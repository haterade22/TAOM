namespace TAOM.Adapters;

/// <summary>
/// The inputs of the Eye of Sauron hunt's in-sight test (#765 M8), read on the main thread every frame while a hunt
/// runs. A value type, so a frame allocates nothing. <see cref="Missing"/> (all false) reads as out of sight.
/// </summary>
public readonly struct HuntSightReading
{
    public static readonly HuntSightReading Missing = default;

    public bool Valid { get; init; }

    public bool HunterActive { get; init; }

    public bool PlayerActive { get; init; }

    public bool HunterInMapEvent { get; init; }

    public bool PlayerInMapEvent { get; init; }

    public bool PlayerAtSea { get; init; }

    /// <summary>The player's seeing range, S.</summary>
    public float SeeingRange { get; init; }

    /// <summary>Straight-line distance from the hunter to the player.</summary>
    public float Distance { get; init; }

    /// <summary><c>MobileParty.MainParty.Speed</c>.</summary>
    public float PlayerSpeed { get; init; }

    /// <summary>The hunter's <c>Ai.DoNotAttackMainPartyUntil</c> is in the future (see <see cref="HuntPartySnapshot.HoldsOffPlayer"/>).</summary>
    public bool HunterHoldsOffPlayer { get; init; }
}
