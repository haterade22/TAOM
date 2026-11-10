using TAOM.Features.CoopInterop;
using TAOM.Features.Execution;

namespace TAOM.Features.KingdomAllegiance;

/// <summary>
/// The raw facts of one "would this clan join that realm" score (docs/features/kingdom-allegiance.md). Read by
/// <c>AllegianceFactsReader</c>, decided by <see cref="KingdomAllegianceService"/>.
/// </summary>
/// <param name="ClanSide">The clan's own id and culture (<c>FactionSides.Of(clan)</c>), never its realm's, which would side an orc
/// clan already inside Gondor as Free.</param>
/// <param name="KingdomSide">The realm's id and culture.</param>
/// <param name="Clan">Whether a human plays the clan (<c>IPlayerControlQuery.OfClan</c>).</param>
/// <param name="KingdomLeader">Whether a human leads the realm (<c>IPlayerControlQuery.OfHero</c> on its leader).</param>
/// <param name="AlreadyServes">The clan already belongs to the realm (<c>clan.Kingdom == kingdom</c>). Vanilla's
/// <c>GetScoreOfMercenaryToLeaveKingdom</c> subtracts the mercenary join score of the clan's own realm, so a veto there would push
/// a serving clan out.</param>
public sealed record AllegianceFacts(SideIds ClanSide, SideIds KingdomSide, PlayerControl Clan, PlayerControl KingdomLeader,
    bool AlreadyServes);
