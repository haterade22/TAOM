using System;
using TaleWorlds.Core;

namespace TAOM.Features.CareerSystem.Domain;

/// <summary>
/// Which troops a combat passive speaks for (#769 E9): the infantry, ranged or cavalry of the career
/// hero's army. <see cref="All"/> is the unfiltered reading every shipped pip carries. <see cref="None"/>
/// is what a troop with no class (a hero, or a character whose default_group is General, Bodyguard, Unset or
/// unreadable) resolves to, never an authored value; such a troop still receives <see cref="All"/>-class effects.
/// </summary>
[Flags]
public enum TroopClassMask
{
    None = 0,
    Infantry = 1,
    Ranged = 2,
    Cavalry = 4,
    All = Infantry | Ranged | Cavalry,
}

public static class TroopClassMapper
{
    /// <summary>The class bits of a troop's default formation. A horse archer is both ranged and
    /// cavalry, a skirmisher both infantry and ranged; General, Bodyguard, Unset and anything out of
    /// range carry no class.</summary>
    public static TroopClassMask FromFormationClass(FormationClass formation)
    {
        switch (formation)
        {
            case FormationClass.Infantry:
            case FormationClass.HeavyInfantry:
                return TroopClassMask.Infantry;
            case FormationClass.Ranged:
                return TroopClassMask.Ranged;
            case FormationClass.Cavalry:
            case FormationClass.LightCavalry:
            case FormationClass.HeavyCavalry:
                return TroopClassMask.Cavalry;
            case FormationClass.HorseArcher:
                return TroopClassMask.Ranged | TroopClassMask.Cavalry;
            case FormationClass.Skirmisher:
                return TroopClassMask.Infantry | TroopClassMask.Ranged;
            default:
                return TroopClassMask.None;
        }
    }

    /// <summary>The class of a character, off its default formation: None for no character, a hero (heroes are
    /// outside the troop passives and counters) or a formation that carries no class. The one rule the damage boundary and the
    /// troop counters share. It reads the static default, so cavalry fighting on foot count as cavalry.</summary>
    public static TroopClassMask FromCharacter(BasicCharacterObject? character)
        => character == null || character.IsHero
            ? TroopClassMask.None
            : FromFormationClass(character.DefaultFormationClass);

    /// <summary>Name-only parsing of an authored <c>troop_class</c>, with the grammar of
    /// <c>attack_type_mask</c> (<see cref="AttackTypeMaskMatch.TryParse"/>): Infantry, Ranged, Cavalry and
    /// All, any case, separated by commas or pipes, with spaces trimmed around each name (a space is not a
    /// separator). An empty string, an empty name in the list, a digit string, "None" or any other unknown
    /// name fails, and the caller drops the passive: a typo must never widen an infantry bonus to the whole
    /// army.</summary>
    public static bool TryParse(string? text, out TroopClassMask mask)
    {
        mask = TroopClassMask.None;
        if (string.IsNullOrWhiteSpace(text))
            return false;

        var acc = TroopClassMask.None;
        foreach (var raw in text!.Split(',', '|'))
        {
            var name = raw.Trim();
            if (string.Equals(name, "Infantry", StringComparison.OrdinalIgnoreCase))
                acc |= TroopClassMask.Infantry;
            else if (string.Equals(name, "Ranged", StringComparison.OrdinalIgnoreCase))
                acc |= TroopClassMask.Ranged;
            else if (string.Equals(name, "Cavalry", StringComparison.OrdinalIgnoreCase))
                acc |= TroopClassMask.Cavalry;
            else if (string.Equals(name, "All", StringComparison.OrdinalIgnoreCase))
                acc |= TroopClassMask.All;
            else
                return false;
        }

        mask = acc;
        return true;
    }
}
