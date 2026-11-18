using System;
using TAOM.Core.Validation;
using TAOM.Features.RaceAbilities.Domain;

namespace TAOM.Features.HeroAuras;

/// <summary>
/// The "an effect only strengthens" check shared by the hero auras and the hero abilities: a harmful or non-finite
/// value is zeroed, and a field only a race ability reads is zeroed too. The label names the owner in each message
/// (for example <c>aura 'dwarf_aura'</c>); <c>reject</c> receives the message without any provider prefix.
/// </summary>
internal static class HeroEffectsCheck
{
    internal static RaceAbilityEffects Validate(string label, RaceAbilityEffects e, float maxMoraleFloor, Action<string> reject)
    {
        float Check(string name, float value, bool strengthens)
        {
            if (FiniteFloatValidator.IsFinite(value) && strengthens)
                return value;
            reject($"{label}: {name} {value} would weaken the soldiers, set to 0");
            return 0f;
        }

        float Gain(float value, string name) => Check(name, value, value >= 0f);

        // An aim error only improves the aim (at most 0), and below -95 the multiplier's sign would flip (the
        // race-ability check floors it at -95 too).
        float AimError(float value)
        {
            if (FiniteFloatValidator.IsFinite(value) && value < -95f)
            {
                reject($"{label}: aimErrorPercent {value} is below -95, set to 0");
                return 0f;
            }
            return Check("aimErrorPercent", value, value <= 0f);
        }

        float AbilityOnly(string name, float value)
        {
            if (value != 0f)
                reject($"{label}: {name} is read only for a race ability, set to 0");
            return 0f;
        }

        return new RaceAbilityEffects
        {
            MoveSpeedPercent = Gain(e.MoveSpeedPercent, "moveSpeedPercent"),
            AccelerationPercent = Gain(e.AccelerationPercent, "accelerationPercent"),
            SwingSpeedPercent = Gain(e.SwingSpeedPercent, "swingSpeedPercent"),
            DrawSpeedPercent = Gain(e.DrawSpeedPercent, "drawSpeedPercent"),
            ReloadSpeedPercent = Gain(e.ReloadSpeedPercent, "reloadSpeedPercent"),
            MissileSpeedPercent = Gain(e.MissileSpeedPercent, "missileSpeedPercent"),
            MountSpeedPercent = Gain(e.MountSpeedPercent, "mountSpeedPercent"),
            MeleeDamagePercent = Gain(e.MeleeDamagePercent, "meleeDamagePercent"),
            RangedDamagePercent = Gain(e.RangedDamagePercent, "rangedDamagePercent"),
            DamageReductionPercent = Gain(e.DamageReductionPercent, "damageReductionPercent"),
            MissileDamageReductionPercent = Gain(e.MissileDamageReductionPercent, "missileDamageReductionPercent"),
            KnockdownResistancePercent = Gain(e.KnockdownResistancePercent, "knockdownResistancePercent"),
            KnockbackResistancePercent = Gain(e.KnockbackResistancePercent, "knockbackResistancePercent"),
            DismountResistancePercent = Gain(e.DismountResistancePercent, "dismountResistancePercent"),
            BlockAbilityPercent = Gain(e.BlockAbilityPercent, "blockAbilityPercent"),
            ParryAbilityPercent = Gain(e.ParryAbilityPercent, "parryAbilityPercent"),
            AttackEagernessPercent = Gain(e.AttackEagernessPercent, "attackEagernessPercent"),
            AimErrorPercent = AimError(e.AimErrorPercent),
            ForceCrushThrough = e.ForceCrushThrough,
            HoldAgainstCrush = e.HoldAgainstCrush,
            ShrugOffBlows = e.ShrugOffBlows,
            MoraleFloor = Math.Min(maxMoraleFloor, Gain(e.MoraleFloor, "moraleFloor")),
            MoraleOnEnd = AbilityOnly("moraleOnEnd", e.MoraleOnEnd),
            HealPerKill = AbilityOnly("healPerKill", e.HealPerKill),
            FearOnKillRadius = AbilityOnly("fearOnKillRadius", e.FearOnKillRadius),
            FearOnKillMorale = AbilityOnly("fearOnKillMorale", e.FearOnKillMorale),
            FearAuraRadius = AbilityOnly("fearAuraRadius", e.FearAuraRadius),
            FearAuraMoralePerSecond = AbilityOnly("fearAuraMoralePerSecond", e.FearAuraMoralePerSecond),
        };
    }
}
