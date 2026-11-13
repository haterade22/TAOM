using TaleWorlds.CampaignSystem;
using TAOM.Features.CoopInterop;

namespace TAOM.Adapters;

/// <summary>
/// "This clan is a human player's" for the adapters that exclude players' clans (#1141). Without a co-op session it
/// is the <c>Clan.PlayerClan</c> comparison. In a session only Coop's registry decides: on the co-op server
/// <c>Clan.PlayerClan</c> is the server's own clan, which no human plays, so a guest's clan is known only there.
/// <see cref="PlayerControl.Unknown"/> counts as a player's: the callers exclude players from AI help, and in doubt
/// a player gets none.
/// </summary>
internal static class PlayerClanCheck
{
    internal static bool IsAPlayersClan(Clan? clan, IPlayerControlQuery players)
        => clan != null && players.OfClan(clan) != PlayerControl.NotPlayer;
}
