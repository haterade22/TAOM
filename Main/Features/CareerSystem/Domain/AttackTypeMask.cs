using System;

namespace TAOM.Features.CareerSystem.Domain;

/// <summary>
/// Two axes on one mask (#613). Delivery: <see cref="Melee"/> / <see cref="Ranged"/>. Kind:
/// <see cref="Cut"/> / <see cref="Pierce"/> / <see cref="Blunt"/>, the engine's
/// <c>DamageTypes</c> per hit. A pip that names only a kind ("+5% blunt resistance") fires on
/// that kind by any delivery; a pip that names only a delivery fires on that delivery by any
/// kind; <see cref="All"/> keeps its old meaning, any delivery and any kind. <see cref="None"/>
/// is the inert sentinel an unparseable mask lands on, never an authored value.
/// </summary>
[Flags]
public enum AttackTypeMask
{
    None = 0,
    Melee = 1,
    Ranged = 2,
    All = Melee | Ranged,
    Cut = 4,
    Pierce = 8,
    Blunt = 16,
}

public static class AttackTypeMaskMatch
{
    public const AttackTypeMask DeliveryBits = AttackTypeMask.Melee | AttackTypeMask.Ranged;
    public const AttackTypeMask KindBits = AttackTypeMask.Cut | AttackTypeMask.Pierce | AttackTypeMask.Blunt;

    /// <summary>Per axis: the pip's delivery bits, if any, must overlap the hit's, and the pip's
    /// kind bits, if any, must overlap the hit's. A shared "any bit overlaps" test would let a
    /// melee-blunt pip fire on a blunt arrow through the kind bit.</summary>
    public static bool Matches(AttackTypeMask pip, AttackTypeMask hit)
    {
        if (pip == AttackTypeMask.None)
            return false;
        var pipDelivery = pip & DeliveryBits;
        var pipKind = pip & KindBits;
        return (pipDelivery == AttackTypeMask.None || (pipDelivery & hit) != AttackTypeMask.None)
            && (pipKind == AttackTypeMask.None || (pipKind & hit) != AttackTypeMask.None);
    }

    /// <summary>The hit mask from the engine's collision data: delivery from <c>IsMissile</c>, kind
    /// from <c>AttackCollisionData.DamageType</c> (Cut 0, Pierce 1, Blunt 2; anything else carries
    /// no kind bit, so a kind-specific pip stays out of it). <paramref name="bluntByRule"/> mirrors
    /// vanilla's own correction (<c>MissionCombatMechanicsHelper.GetAttackCollisionResults:200</c>):
    /// a bare-hand hit, a hit off the weapon's attach bone, a kick or bash, fall damage and a horse
    /// charge are Blunt whatever the struct says, through a local the engine never writes back.</summary>
    public static AttackTypeMask ForHit(bool isMissile, int damageType, bool bluntByRule = false)
        => (isMissile ? AttackTypeMask.Ranged : AttackTypeMask.Melee) | (bluntByRule ? AttackTypeMask.Blunt : KindOf(damageType));

    public static AttackTypeMask KindOf(int damageType)
    {
        switch (damageType)
        {
            case 0: return AttackTypeMask.Cut;
            case 1: return AttackTypeMask.Pierce;
            case 2: return AttackTypeMask.Blunt;
            default: return AttackTypeMask.None;
        }
    }

    /// <summary>Name-only parsing of an authored mask: names separated by commas or pipes, any
    /// case. A digit string, an unknown name anywhere in the list, an empty string, or the
    /// sentinel "None" all fail; the caller warns and the pip goes inert rather than widening
    /// to All (the trap that made five shipped kind pips resist everything).</summary>
    public static bool TryParse(string? text, out AttackTypeMask mask)
    {
        mask = AttackTypeMask.None;
        if (string.IsNullOrWhiteSpace(text))
            return false;

        var acc = AttackTypeMask.None;
        foreach (var raw in text!.Split(',', '|'))
        {
            var name = raw.Trim();
            if (name.Length == 0)
                return false;
            var found = false;
            foreach (var candidate in Enum.GetNames(typeof(AttackTypeMask)))
            {
                if (!string.Equals(candidate, name, StringComparison.OrdinalIgnoreCase))
                    continue;
                var value = (AttackTypeMask)Enum.Parse(typeof(AttackTypeMask), candidate);
                if (value == AttackTypeMask.None)
                    return false;
                acc |= value;
                found = true;
                break;
            }
            if (!found)
                return false;
        }

        mask = acc;
        return true;
    }
}
