namespace TAOM.Features.PlayerSwitcher.Domain;

/// <summary>
/// Everything the handover needs to remember about the pre-swap world, captured BEFORE the first
/// mutation. Once ChangePlayerCharacterAction runs, Hero.MainHero and Clan.PlayerClan no longer
/// answer questions about the character the player built, so nothing may be read back later.
/// </summary>
public readonly struct SwitchTicket
{
    public SwitchTicket(
        string originalHeroId,
        string originalClanId,
        string originalPartyId,
        string targetClanId,
        string careerId)
    {
        OriginalHeroId = originalHeroId;
        OriginalClanId = originalClanId;
        OriginalPartyId = originalPartyId;
        TargetClanId = targetClanId;
        CareerId = careerId;
    }

    /// <summary>The throwaway character-creation hero, removed at the end of the handover.</summary>
    public string OriginalHeroId { get; }

    /// <summary>
    /// The throwaway player_faction clan. Destroyed as a side effect of removing its leader,
    /// but only because the player clan pointer was moved off it first.
    /// </summary>
    public string OriginalClanId { get; }

    /// <summary>
    /// The party character creation built, captured by id so it can be dealt with explicitly.
    ///
    /// Both paths need this id and they use it differently. On the takeover path nothing is done
    /// with it: ChangePlayerCharacterAction hands the party to the new main hero without moving
    /// its ActualClan, so it is still registered to the throwaway clan and DestroyClanAction
    /// disposes of it when that clan's leader is removed. On the adoption path the throwaway clan
    /// IS the player's clan and is never destroyed, so the party would linger as a second
    /// player-owned party and it is absorbed instead.
    ///
    /// Captured as a single id on purpose. The predecessor mod swept clan war parties with a
    /// predicate whose operator precedence made its second clause match every OTHER lord's party
    /// in the clan, which on a royal clan would have merged and deleted all of them.
    /// </summary>
    public string OriginalPartyId { get; }

    /// <summary>The clan the player is taking over. Empty on the adoption path.</summary>
    public string TargetClanId { get; }

    public string CareerId { get; }

    public bool IsValid => !string.IsNullOrEmpty(OriginalHeroId);

    public static SwitchTicket None =>
        new SwitchTicket(string.Empty, string.Empty, string.Empty, string.Empty, string.Empty);
}
