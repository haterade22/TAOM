using System.Collections.Generic;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Settlements;
using TAOM.Features.WandererAllegiance;

namespace TAOM.Adapters;

/// <summary>
/// The engine half of wanderer placement: reads the towns and the wanderer's culture, hands plain ids to
/// <see cref="WandererTownSideService"/>, and maps the answer back to a settlement. It moves no one: the patch changes
/// the argument of vanilla's own <c>EnterSettlementAction.ApplyForCharacterOnly</c>.
/// </summary>
public sealed class WandererTownAdapter
{
    private readonly WandererTownSideService _service;

    public WandererTownAdapter(WandererTownSideService service) => _service = service;

    public bool Applies() => _service.Applies();

    /// <summary>
    /// The town the wanderer should enter instead of <paramref name="destination"/>, or null to keep it: the destination already
    /// suits the wanderer, the hero is not an unhired wanderer, or no usable town of the wanderer's side exists.
    /// </summary>
    public Settlement? RedirectedTown(Hero? hero, Settlement? destination)
    {
        if (hero == null || destination?.Town == null || !hero.IsWanderer || hero.CompanionOf != null)
            return null;

        var culture = hero.Culture?.StringId;
        if (_service.Fits(culture, FactionSides.OfSettlementOwner(destination)))
            return null;

        var towns = new List<Settlement>();
        var options = new List<WandererTownOption>();
        var playerSettlement = Hero.MainHero?.CurrentSettlement;
        foreach (var town in Town.AllTowns)
        {
            var settlement = town.Settlement;
            var usable = settlement != playerSettlement && settlement.SiegeEvent == null;
            var occupied = false;
            foreach (var other in settlement.HeroesWithoutParty)
            {
                if (other != hero && other.IsWanderer && other.CompanionOf == null)
                {
                    occupied = true;
                    break;
                }
            }

            towns.Add(settlement);
            options.Add(new WandererTownOption(FactionSides.OfSettlementOwner(settlement), usable, occupied));
        }

        var index = _service.Choose(culture, FactionSides.OfSettlementOwner(destination), options);
        return index < 0 ? null : towns[index];
    }

    /// <summary>
    /// Points a wanderer the engine is entering into <paramref name="settlement"/> at a town of its own side instead: moves
    /// <c>Hero.StayingInSettlement</c> (vanilla's <c>ApplyForCharacterOnly</c> has just set it to the first pick) and swaps the argument.
    /// </summary>
    public bool TryRedirect(Hero? hero, ref Settlement settlement)
    {
        var target = RedirectedTown(hero, settlement);
        if (target == null || hero == null)
            return false;
        hero.StayingInSettlement = target;
        settlement = target;
        return true;
    }
}
