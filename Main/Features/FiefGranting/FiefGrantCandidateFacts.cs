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
        bool isCapturer,
        bool isCultureMatch,
        bool isPlayerClan)
    {
        OwnedFortifications = ownedFortifications;
        IsRulingClan = isRulingClan;
        IsCapturer = isCapturer;
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
    /// True when the clan actually stormed the place, read from <c>Town.LastCapturedBy</c>. Vanilla's
    /// <c>SettlementClaimantDecision._capturerHero</c> is written by the constructor and never read,
    /// and the daily-tick path passes it as <c>null</c> regardless, so this stamp is the only signal.
    /// </summary>
    public bool IsCapturer { get; }

    /// <summary>True when the clan's culture matches the settlement's. Vanilla has no equivalent term.</summary>
    public bool IsCultureMatch { get; }

    /// <summary>True for the player's own clan, which the "Apply Penalties To Player" knob exempts.</summary>
    public bool IsPlayerClan { get; }
}
