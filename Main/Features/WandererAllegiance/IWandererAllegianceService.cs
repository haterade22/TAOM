using TAOM.Features.Execution;

namespace TAOM.Features.WandererAllegiance;

/// <summary>
/// The Free/Evil hire rule for wanderers. Keys on CULTURE StringIds (the keys of
/// <c>execution/alignment.json</c>) for the wanderer, because a tavern wanderer has no clan and no
/// kingdom, and on the player clan's map faction with that faction's own culture for the player,
/// because the side a player fights for is the realm they serve: a Gondor-born mercenary of Mordor is
/// Evil-aligned for this purpose, and a vassal of a Neutral realm is Neutral (#957).
/// </summary>
public interface IWandererAllegianceService
{
    /// <summary>
    /// Decides whether the wanderer refuses. Any null or unknown culture resolves to Neutral, and
    /// Neutral on either side allows the hire, so the rule fails open to vanilla.
    /// </summary>
    /// <param name="wandererHeroId">The wanderer's <c>Hero.StringId</c>; consulted only by the named-companions-only scope.</param>
    /// <param name="wandererCultureId">The wanderer's culture StringId.</param>
    /// <param name="player">The player clan's map faction id with that faction's culture (<c>FactionSides.OfPlayer()</c>).</param>
    WandererHireVerdict Evaluate(string? wandererHeroId, string? wandererCultureId, SideIds player);
}
