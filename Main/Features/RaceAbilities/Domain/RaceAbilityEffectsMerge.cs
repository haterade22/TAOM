using System;

namespace TAOM.Features.RaceAbilities.Domain;

/// <summary>
/// Merges two effect sets that reach one soldier: his own race ability and a hero aura (#1127,
/// docs/features/hero-auras.md), or two auras. Per channel the stronger value counts and the two never add,
/// because the stat post-pass multiplies and a sum would compound. A negative value in <c>own</c> is a price the
/// ability pays on purpose (berserk's block), so it stays; for <see cref="RaceAbilityEffects.AimErrorPercent"/>
/// lower is better, so the rule is mirrored. Flags are granted by either source and the morale floor is the
/// higher one. The fields only a race ability reads (the end-of-ability morale, heal and fear on a kill, the fear
/// aura) come from <c>own</c>. Pure, allocation only when both sources exist.
/// </summary>
public static class RaceAbilityEffectsMerge
{
    public static RaceAbilityEffects? Combine(RaceAbilityEffects? own, RaceAbilityEffects? aura)
    {
        if (aura == null)
            return own;
        if (own == null)
            return aura;
        return new RaceAbilityEffects
        {
            MoveSpeedPercent = Higher(own.MoveSpeedPercent, aura.MoveSpeedPercent),
            AccelerationPercent = Higher(own.AccelerationPercent, aura.AccelerationPercent),
            SwingSpeedPercent = Higher(own.SwingSpeedPercent, aura.SwingSpeedPercent),
            DrawSpeedPercent = Higher(own.DrawSpeedPercent, aura.DrawSpeedPercent),
            ReloadSpeedPercent = Higher(own.ReloadSpeedPercent, aura.ReloadSpeedPercent),
            MissileSpeedPercent = Higher(own.MissileSpeedPercent, aura.MissileSpeedPercent),
            MountSpeedPercent = Higher(own.MountSpeedPercent, aura.MountSpeedPercent),
            MeleeDamagePercent = Higher(own.MeleeDamagePercent, aura.MeleeDamagePercent),
            RangedDamagePercent = Higher(own.RangedDamagePercent, aura.RangedDamagePercent),
            DamageReductionPercent = Higher(own.DamageReductionPercent, aura.DamageReductionPercent),
            KnockdownResistancePercent = Higher(own.KnockdownResistancePercent, aura.KnockdownResistancePercent),
            KnockbackResistancePercent = Higher(own.KnockbackResistancePercent, aura.KnockbackResistancePercent),
            DismountResistancePercent = Higher(own.DismountResistancePercent, aura.DismountResistancePercent),
            BlockAbilityPercent = Higher(own.BlockAbilityPercent, aura.BlockAbilityPercent),
            ParryAbilityPercent = Higher(own.ParryAbilityPercent, aura.ParryAbilityPercent),
            AttackEagernessPercent = Higher(own.AttackEagernessPercent, aura.AttackEagernessPercent),
            AimErrorPercent = own.AimErrorPercent > 0f ? own.AimErrorPercent : Math.Min(own.AimErrorPercent, aura.AimErrorPercent),
            ForceCrushThrough = own.ForceCrushThrough || aura.ForceCrushThrough,
            HoldAgainstCrush = own.HoldAgainstCrush || aura.HoldAgainstCrush,
            ShrugOffBlows = own.ShrugOffBlows || aura.ShrugOffBlows,
            MoraleFloor = Math.Max(own.MoraleFloor, aura.MoraleFloor),
            MoraleOnEnd = own.MoraleOnEnd,
            HealPerKill = own.HealPerKill,
            FearOnKillRadius = own.FearOnKillRadius,
            FearOnKillMorale = own.FearOnKillMorale,
            FearAuraRadius = own.FearAuraRadius,
            FearAuraMoralePerSecond = own.FearAuraMoralePerSecond,
        };
    }

    private static float Higher(float own, float aura) => own < 0f ? own : Math.Max(own, aura);
}
