namespace TAOM.Features.LotrIssues.Domain;

/// <summary>
/// One bandit clan a HuntBand quest may draw its band from, with the squared map distance from the issue
/// settlement to the nearest hideout of the clan's culture. <see cref="float.PositiveInfinity"/> when the
/// culture has no hideout.
/// </summary>
public readonly struct LotrBandClanOption
{
    public LotrBandClanOption(string clanId, bool isCreature, float hideoutDistanceSquared)
    {
        ClanId = clanId;
        IsCreature = isCreature;
        HideoutDistanceSquared = hideoutDistanceSquared;
    }

    public string ClanId { get; }

    /// <summary>Spider and troll clans are never swindlers.</summary>
    public bool IsCreature { get; }

    public float HideoutDistanceSquared { get; }
}
