using System.Collections.Generic;
using TAOM.Features.CareerSystem.Domain;

namespace TAOM.Features.CareerSystem.Diagnostics;

/// <summary>
/// The passive rows <c>taom.career_perks</c> prints (#613, #769 E9, E10): one per type the hero holds, with
/// a per-hit-kind breakdown for the types whose consumer passes a hit (<see cref="PassiveEffectConsumers.MaskGated"/>)
/// and a per-troop-class view for the class-filtered ones (<see cref="PassiveEffectConsumers.ClassFiltered"/>).
/// Split out of the console command so the choice of which breakdown a type gets is reachable by a test: the
/// three troop counters read no hit, so a hit-kind list for them would describe a gate they do not have.
/// </summary>
public static class CareerPerkRows
{
    // The six kinded hits plus the two kindless ones (an Invalid DamageType with none of vanilla's
    // blunt-by-rule conditions), so a kind-only pip's zero against a kindless hit is visible.
    private static readonly AttackTypeMask[] HitMasks =
    {
        AttackTypeMask.Melee,
        AttackTypeMask.Ranged,
        AttackTypeMask.Melee | AttackTypeMask.Cut,
        AttackTypeMask.Melee | AttackTypeMask.Pierce,
        AttackTypeMask.Melee | AttackTypeMask.Blunt,
        AttackTypeMask.Ranged | AttackTypeMask.Cut,
        AttackTypeMask.Ranged | AttackTypeMask.Pierce,
        AttackTypeMask.Ranged | AttackTypeMask.Blunt,
    };

    public static List<CareerPerkRow> Gather(ICareerPassiveService passives, string heroId)
    {
        var rows = new List<CareerPerkRow>();
        foreach (var type in (PassiveEffectType[])System.Enum.GetValues(typeof(PassiveEffectType)))
        {
            var magnitude = passives.GetPassiveMagnitude(heroId, type);
            if (magnitude == 0f) continue;
            rows.Add(new CareerPerkRow(type, magnitude, MaskedBuckets(passives, heroId, type), ClassBuckets(passives, heroId, type)));
        }

        return rows;
    }

    // The mask-gated types only: the effective magnitude per hit kind, collapsed to one "All" entry when
    // every kind reads the same.
    internal static List<(AttackTypeMask, float)>? MaskedBuckets(ICareerPassiveService passives, string heroId, PassiveEffectType type)
    {
        if (!PassiveEffectConsumers.IsMaskGated(type))
            return null;

        var buckets = new List<(AttackTypeMask, float)>(HitMasks.Length);
        var allSame = true;
        float first = float.NaN;
        foreach (var hit in HitMasks)
        {
            var value = passives.GetMaskedMagnitude(heroId, type, hit);
            if (float.IsNaN(first)) first = value;
            else if (value != first) allSame = false;
            buckets.Add((hit, value));
        }

        if (allSame)
            return new List<(AttackTypeMask, float)> { (AttackTypeMask.All, first) };
        return buckets;
    }

    // The class-filtered types: what a troop of each class receives (any hit), collapsed to one "All" entry
    // when every reading is the same. A troop with no class (None) gets the army-wide pips alone, a horse
    // archer reads Ranged and Cavalry at once.
    internal static List<(TroopClassMask, float)>? ClassBuckets(ICareerPassiveService passives, string heroId, PassiveEffectType type)
    {
        if (!PassiveEffectConsumers.IsClassFiltered(type))
            return null;

        return CareerPerkReport.ByClass(
            passives.GetTroopMagnitude(heroId, type, TroopClassMask.None),
            passives.GetTroopMagnitude(heroId, type, TroopClassMask.Infantry),
            passives.GetTroopMagnitude(heroId, type, TroopClassMask.Ranged),
            passives.GetTroopMagnitude(heroId, type, TroopClassMask.Cavalry),
            passives.GetTroopMagnitude(heroId, type, TroopClassMask.Ranged | TroopClassMask.Cavalry));
    }
}
