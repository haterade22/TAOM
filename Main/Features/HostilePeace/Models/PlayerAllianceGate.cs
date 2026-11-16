using TaleWorlds.CampaignSystem;
using TAOM.Features.Diplomacy;

namespace TAOM.Features.HostilePeace.Models;

/// <summary>
/// Block D at the engine boundary: finds which of the two kingdoms the player rules and asks
/// <see cref="HostilePeaceService.BlocksPlayerAlliance"/> with each kingdom's own id and culture. Shared by the three player
/// alliance paths (the alliance score, the kingdom decision permission and the "I propose an alliance" line), so they cannot
/// disagree. A pair without a player-ruled kingdom is never blocked here: AI realms keep their Hostile-tier rule.
/// </summary>
internal static class PlayerAllianceGate
{
    internal static bool Blocks(HostilePeaceService? service, Kingdom? kingdomA, Kingdom? kingdomB)
    {
        if (service == null || kingdomA == null || kingdomB == null)
            return false;

        var player = PlayerKingdomHelper.GetPlayerRuledKingdom();
        if (player == null)
            return false;

        Kingdom other;
        if (kingdomA == player)
            other = kingdomB;
        else if (kingdomB == player)
            other = kingdomA;
        else
            return false;

        return service.BlocksPlayerAlliance(player.StringId, player.Culture?.StringId, other.StringId, other.Culture?.StringId);
    }
}
