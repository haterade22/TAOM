using TaleWorlds.CampaignSystem;
using TAOM.Features.CoopInterop;

namespace TAOM.Adapters;

/// <summary>
/// "This clan is a human player's" for the War Chronicle boundaries that leave players' clans alone (#1141): the
/// escape and rally adapters and the two volunteer boundaries. Without a co-op session it is the
/// <c>Clan.PlayerClan</c> comparison. In a session only Coop's registry decides: on the co-op server
/// <c>Clan.PlayerClan</c> is the server's own clan, which no human plays, so a guest's clan is known only there.
/// <see cref="PlayerControl.Unknown"/> counts as a player's: the escape roll and the rally are help for AI, and the
/// volunteer multiplier can raise or cut production, so in doubt a clan gets no effect. Check a new caller against
/// that rule before you add it.
/// </summary>
internal static class PlayerClanCheck
{
    internal static bool IsAPlayersClan(Clan? clan, IPlayerControlQuery players)
        => clan != null && players.OfClan(clan) != PlayerControl.NotPlayer;
}
