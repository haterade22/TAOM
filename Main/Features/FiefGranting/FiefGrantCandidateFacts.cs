namespace TAOM.Features.FiefGranting;

/// <summary>
/// Primitive snapshot of one candidate clan in a fief-grant election (#458). Sealed TaleWorlds types
/// (<c>Clan</c>, <c>Settlement</c>) are converted to this at the decision boundary so
/// <see cref="IFiefGrantPolicyService"/> stays a pure, unit-testable function (ADR-007).
/// </summary>
public readonly struct FiefGrantCandidateFacts
{
    public FiefGrantCandidateFacts(
        int ownedFortifications,
        bool isRulingClan,
        float siegeContributionShare,
        bool siegeWasRecorded,
        bool isCultureMatch,
        bool isPlayerClan)
    {
        OwnedFortifications = ownedFortifications;
        IsRulingClan = isRulingClan;
        SiegeContributionShare = siegeContributionShare;
        SiegeWasRecorded = siegeWasRecorded;
        IsCultureMatch = isCultureMatch;
        IsPlayerClan = isPlayerClan;
    }

    /// <summary>
    /// Towns + castles the clan already holds, EXCLUDING the settlement being voted on. Villages are
    /// excluded deliberately: vanilla's own balance divisor filters on <c>item.IsFortification</c>.
    /// </summary>
    public int OwnedFortifications { get; }

    /// <summary>True when this clan leads the kingdom, which vanilla already rewards with +60 merit.</summary>
    public bool IsRulingClan { get; }

    /// <summary>
    /// The clan's contribution to the winning assault as a fraction of the top contributor's, in
    /// [0, 1]: 1 for the clan that carried the assault, 0 for a clan that fielded no party. Read
    /// from the participation record (#565), not from <c>Town.LastCapturedBy</c>: that stamp names
    /// only the assault leader's clan, is never cleared, and vanilla already pays it a flat +30.
    /// </summary>
    public float SiegeContributionShare { get; }

    /// <summary>
    /// True when a participation record exists for the settlement. Without one nobody is known to
    /// have fought (a save from before #565, or a settlement that changed hands with no assault),
    /// so both participation terms stay out of the multiplier rather than damping every clan.
    /// </summary>
    public bool SiegeWasRecorded { get; }

    /// <summary>True when the clan's culture matches the settlement's. Vanilla has no equivalent term.</summary>
    public bool IsCultureMatch { get; }

    /// <summary>True for the player's own clan, which the "Exempt Your Clan From Penalties" knob can exempt.</summary>
    public bool IsPlayerClan { get; }
}
