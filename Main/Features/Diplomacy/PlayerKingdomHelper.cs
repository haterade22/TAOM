using TaleWorlds.CampaignSystem;

namespace TAOM.Features.Diplomacy;

// Single source of truth for "the kingdom the player personally RULES" — used by the two
// diplomacy GameModel overrides and the alliance-proposal dialog to decide when the
// player-freedom alliance rules apply. Boundary helper (touches sealed Clan/Kingdom), so it
// lives alongside the entry points, not in the TaleWorlds-free service.
//
// CRITICAL: it requires the player to be the kingdom's RULING clan — NOT merely a member.
// A vassal/mercenary player's liege kingdom is AI-ruled; granting it the player-freedom
// bypass would change AI-vs-AI diplomacy (Codex review 2026-06-16, HIGH #1). The earlier
// model helpers checked only Clan.PlayerClan?.Kingdom and had exactly that bug.
internal static class PlayerKingdomHelper
{
    // The kingdom the player rules (founded / is the ruling clan of), or null when the player
    // has no kingdom or is only a vassal/mercenary within someone else's realm.
    public static Kingdom GetPlayerRuledKingdom()
    {
        var clan = Clan.PlayerClan;
        var kingdom = clan?.Kingdom;
        return kingdom != null && kingdom.RulingClan == clan ? kingdom : null;
    }

    // True when one of the two kingdoms is the kingdom the player personally rules.
    public static bool InvolvesPlayerRuledKingdom(Kingdom kingdom1, Kingdom kingdom2)
    {
        var playerKingdom = GetPlayerRuledKingdom();
        return playerKingdom != null && (kingdom1 == playerKingdom || kingdom2 == playerKingdom);
    }
}
