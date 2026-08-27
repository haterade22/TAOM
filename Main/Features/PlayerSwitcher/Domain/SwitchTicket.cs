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
        string targetClanId,
        string careerId)
    {
        OriginalHeroId = originalHeroId;
        OriginalClanId = originalClanId;
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

    /// <summary>The clan the player is taking over. Empty on the adoption path.</summary>
    public string TargetClanId { get; }

    public string CareerId { get; }

    public bool IsValid => !string.IsNullOrEmpty(OriginalHeroId);

    public static SwitchTicket None =>
        new SwitchTicket(string.Empty, string.Empty, string.Empty, string.Empty);
}
