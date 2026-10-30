using System;
using System.Collections.Generic;
using TAOM.Features.CareerSystem.Domain;

namespace TAOM.Features.CareerSystem;

/// <summary>
/// The LootGold rule of the battle reward model (#769 E11), generic over the winner so it runs without the game.
/// <c>DefaultBattleRewardModel.GetLootGoldChances</c> hands back one share per winning party (its contribution
/// over the total), and <c>MapEvent.CalculatePlunderedAndLostGoldAmounts</c> pays each party
/// <c>plundered * share</c> to its LeaderHero. The shares are not renormalised afterwards, so a career bonus
/// scales one party's share and nobody else's: the bonus is extra gold the defeated side did not lose, as every
/// other loot bonus is.
///
/// Keyed on the winning party's own LeaderHero, not owner-first: an owner-first key would pay the player's bonus on
/// a caravan or a companion's party that the player owns, and the gold goes to the party's LeaderHero (a party with
/// no leader credits its trade gold instead).
/// </summary>
public static class CareerLootShares
{
    /// <summary>Each share scaled by its winner's LootGold passive, same order and keys. A new list: the engine's is never edited.</summary>
    public static List<KeyValuePair<T, float>> Scale<T>(
        IEnumerable<KeyValuePair<T, float>> chances, Func<T, string?> heroIdOf, ICareerPassiveService passives)
    {
        var scaled = new List<KeyValuePair<T, float>>();
        foreach (var chance in chances)
        {
            var share = passives.ScaleByPassive(heroIdOf(chance.Key), chance.Value, PassiveEffectType.LootGold);
            scaled.Add(new KeyValuePair<T, float>(chance.Key, share));
        }

        return scaled;
    }
}
