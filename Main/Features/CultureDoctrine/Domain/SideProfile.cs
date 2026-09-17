using System;
using System.Collections.Generic;

namespace TAOM.Features.CultureDoctrine.Domain;

/// <summary>One <c>IBattleCombatant</c> reduced to the three numbers the doctrine needs.</summary>
public readonly struct SideCombatant
{
    public SideCombatant(string? cultureId, int troopCount, int tacticsSkill)
    {
        CultureId = cultureId;
        TroopCount = troopCount;
        TacticsSkill = tacticsSkill;
    }

    public string? CultureId { get; }
    public int TroopCount { get; }
    public int TacticsSkill { get; }
}

/// <summary>
/// One battle side folded from its combatants: the culture fielding the most troops (parties of
/// one culture add up; a tie keeps the first listed, which is the engine's leader-first order)
/// and the best Tactics skill on the side, the same maximum vanilla gates its registration on.
/// A combatant without a culture counts toward the troop total but cannot win the majority,
/// because no doctrine could be keyed on it. <paramref name="tacticsSkillFloor"/> lets a team
/// that holds only part of a side (the player's ally team) register with the whole side's best
/// skill, as vanilla does.
/// </summary>
public sealed class SideProfile
{
    private SideProfile(string? cultureId, int troopCount, int tacticsSkill)
    {
        CultureId = cultureId;
        TroopCount = troopCount;
        TacticsSkill = tacticsSkill;
    }

    public string? CultureId { get; }
    public int TroopCount { get; }
    public int TacticsSkill { get; }

    public static SideProfile From(IEnumerable<SideCombatant> combatants) => From(combatants, 0);

    public static SideProfile From(IEnumerable<SideCombatant> combatants, int tacticsSkillFloor)
    {
        var troopsByCulture = new Dictionary<string, int>(StringComparer.Ordinal);
        var order = new List<string>();
        var total = 0;
        var skill = Math.Max(0, tacticsSkillFloor);
        foreach (var combatant in combatants ?? Array.Empty<SideCombatant>())
        {
            var troops = Math.Max(0, combatant.TroopCount);
            total += troops;
            skill = Math.Max(skill, combatant.TacticsSkill);
            if (string.IsNullOrWhiteSpace(combatant.CultureId))
                continue;
            var culture = combatant.CultureId!;
            if (!troopsByCulture.ContainsKey(culture))
            {
                troopsByCulture[culture] = 0;
                order.Add(culture);
            }
            troopsByCulture[culture] += troops;
        }

        string? majority = null;
        var best = -1;
        foreach (var culture in order)
        {
            if (troopsByCulture[culture] > best)
            {
                best = troopsByCulture[culture];
                majority = culture;
            }
        }
        return new SideProfile(majority, total, skill);
    }
}
