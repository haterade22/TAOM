using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.GameComponents;

namespace TAOM.Features.HostilePeace.Models;

/// <summary>
/// Block C, crime: a failed sneak into a Hostile realm's town adds crime, and crime alone must not start a war (owner's rule,
/// 2026-11-15: "Keep failed sneaks from starting a war, crime only"). When the player leads a realm and the faction is a Hostile realm
/// at peace (<see cref="HostilePeaceService.CapsCrimeBelowWar"/>), the new rating is capped at
/// <c>DeclareWarCrimeRatingThreshold - 1</c> (59 with the base 60).
/// <para>Why a cap and not a block on the war: <c>ChangeCrimeRatingAction</c> stores the new rating, then, at or above the
/// threshold with the player ruling and not at war, applies -10 relation with the faction leader before it declares the war. Refusing
/// the war in <c>DeclareWarAction</c> would leave the rating at or above the threshold, and that relation loss would repeat on every
/// later crime change. Below the threshold neither step runs.</para>
/// <para>Crime still does its other work: fines, and the disguise detection that gates a sneak in. The severe-crime line
/// (<c>IsPlayerCrimeRatingSevere</c>, 65) is above the cap, so it is not reached for that realm.</para>
/// Co-op: nothing beyond block C's; it reads the local main hero, as <see cref="TaomSettlementAccessModel"/> does.
/// </summary>
public sealed class TaomCrimeModel : DefaultCrimeModel
{
    private readonly HostilePeaceService _service;

    public TaomCrimeModel(HostilePeaceService service)
    {
        _service = service;
    }

    public override ExplainedNumber GetEffectiveCrimeChange(IFaction faction, float deltaCrimeRating)
    {
        var result = base.GetEffectiveCrimeChange(faction, deltaCrimeRating);
        var hero = Hero.MainHero;
        var playerFaction = hero?.MapFaction;
        if (faction == null || hero == null || playerFaction == null)
            return result;

        if (_service.CapsCrimeBelowWar(
                playerFaction.StringId,
                faction.StringId,
                FactionManager.IsAtWarAgainstFaction(playerFaction, faction),
                playerFaction.Leader == hero))
            result.LimitMax(DeclareWarCrimeRatingThreshold - 1f);

        return result;
    }
}
