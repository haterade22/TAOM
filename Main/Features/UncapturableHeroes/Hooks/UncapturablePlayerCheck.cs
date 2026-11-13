using TAOM.Features.CoopInterop;
using TaleWorlds.CampaignSystem;

namespace TAOM.Features.UncapturableHeroes.Hooks;

/// <summary>
/// Whether a hero is a human player's, whose captivity belongs to vanilla and to the co-op mod
/// (#1139). Shared by both seams so they never disagree on the same hero.
///
/// <para>On the Coop server <c>Hero.MainHero</c> is the server's own hero, and Coop takes a
/// defeated player prisoner through <c>TakePrisonerAction.Apply</c>, so the player test must come
/// from <see cref="IPlayerControlQuery"/>. Without a session the query answers the
/// <c>Hero.MainHero</c> comparison, as before. <see cref="PlayerControl.Unknown"/> (a session whose
/// registry is unreadable) also defers, and the registry answers it for every hero at once, so in
/// such a session the feature protects nobody: a vanilla capture is a valid state, and a player made
/// a fugitive leaves Coop's captivity flow half run.</para>
///
/// <para>A null query (the seams before <c>Initialize</c>) answers true: both seams defer then
/// anyway, because their service is null too.</para>
/// </summary>
internal static class UncapturablePlayerCheck
{
    public static bool BelongsToPlayers(Hero hero, IPlayerControlQuery? players)
        => players == null || players.OfHero(hero) != PlayerControl.NotPlayer;
}
