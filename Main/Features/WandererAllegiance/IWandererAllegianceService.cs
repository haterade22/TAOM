namespace TAOM.Features.WandererAllegiance;

/// <summary>
/// The Free/Evil hire rule for wanderers. Keys on CULTURE StringIds (the keys of
/// <c>execution/alignment.json</c>) for the wanderer, because a tavern wanderer has no clan and no
/// kingdom, and on kingdom-then-culture for the player, because the side a player fights for is the
/// kingdom they serve (a Gondor-born mercenary of Mordor is Evil-aligned for this purpose).
/// </summary>
public interface IWandererAllegianceService
{
    /// <summary>
    /// Decides whether the wanderer refuses. Any null or unknown culture resolves to Neutral, and
    /// Neutral on either side allows the hire, so the rule fails open to vanilla.
    /// </summary>
    /// <param name="wandererHeroId">The wanderer's <c>Hero.StringId</c>; consulted only by the named-companions-only scope.</param>
    /// <param name="wandererCultureId">The wanderer's culture StringId.</param>
    /// <param name="playerKingdomId">The player clan's kingdom StringId, or null when kingdomless.</param>
    /// <param name="playerCultureId">The player clan's culture StringId.</param>
    WandererHireVerdict Evaluate(string? wandererHeroId, string? wandererCultureId, string? playerKingdomId, string? playerCultureId);
}
