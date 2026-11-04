using System.Collections.Generic;
using System.Linq;

namespace TAOM.Features.FordsOfWar.Domain;

/// <summary>What a hero's blows and the blows it takes are multiplied by. A troop that is no hero of this match gets 1. Immutable, so a blow can read it from any thread.</summary>
internal sealed class HeroCombatFactors
{
    private readonly HashSet<string> _heroIds;
    private readonly float _damage;
    private readonly float _taken;

    private HeroCombatFactors(HashSet<string> heroIds, float damage, float taken)
    {
        _heroIds = heroIds;
        _damage = damage;
        _taken = taken;
    }

    public static HeroCombatFactors Of(IEnumerable<HeroOffer> heroes, FordsHeroConfig config) =>
        new(new HashSet<string>(heroes.Select(h => h.HeroId)), config.DamageFactor, config.DamageTakenFactor);

    public float Damage(string? troopId) => IsHero(troopId) ? _damage : 1f;

    public float Taken(string? troopId) => IsHero(troopId) ? _taken : 1f;

    private bool IsHero(string? troopId) => troopId != null && _heroIds.Contains(troopId);
}
