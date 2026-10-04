using System;
using System.Collections.Generic;
using TAOM.Core.Validation;
using TAOM.Features.RaceAbilities.Domain;

namespace TAOM.Features.RaceAbilities;

// Every decision behind the race abilities, floats and flags in, no TaleWorlds types (ADR-007). The engine
// boundary (RaceAbilitySensor, RaceAbilityActivator, RaceAbilityTicker, RaceAbilityDeaths, RaceAbilityHooks)
// only reads the engine, asks here, and writes the answer back. Engine floats can arrive as NaN, so each
// gate is written as a positive requirement and fails closed; each piece of arithmetic hands its input back
// unchanged when the result would not be finite.
public sealed class RaceAbilityService
{
    // A rider is charging a soldier when his mount closes on him faster than this (m/s).
    public const float CavalryClosingSpeed = 3f;

    public bool IsOffCooldown(float? lastFiredAt, float now, float cooldownSeconds)
    {
        if (!lastFiredAt.HasValue)
            return FiniteFloatValidator.IsFinite(now);
        var elapsed = now - lastFiredAt.Value;
        return FiniteFloatValidator.IsFinite(elapsed) && elapsed >= cooldownSeconds;
    }

    // Fires when every Requires trigger holds and, when AnyOf is not empty, one of them does; returns the
    // trigger that decided it (the first AnyOf that holds, else the first Requires), or null. A profile with
    // no trigger at all never fires (the provider drops such a profile; this is the backstop).
    public RaceAbilityTriggerKind? FiringTrigger(RaceAbilityProfile profile, RaceAbilitySenses senses)
    {
        if (profile.Requires.Count == 0 && profile.AnyOf.Count == 0)
            return null;
        foreach (var trigger in profile.Requires)
            if (!Holds(trigger, senses))
                return null;
        if (profile.AnyOf.Count == 0)
            return profile.Requires[0].ParsedKind;
        foreach (var trigger in profile.AnyOf)
            if (Holds(trigger, senses))
                return trigger.ParsedKind;
        return null;
    }

    private static bool Holds(RaceAbilityTrigger trigger, RaceAbilitySenses senses)
    {
        switch (trigger.ParsedKind)
        {
            case RaceAbilityTriggerKind.Always:
                return true;
            case RaceAbilityTriggerKind.EnemyWithin:
                return CountEnemies(senses, trigger.Range, woundedBelow: null, closingOnly: false) >= 1;
            case RaceAbilityTriggerKind.EnemiesWithin:
                return CountEnemies(senses, trigger.Range, woundedBelow: null, closingOnly: false) >= trigger.Count;
            case RaceAbilityTriggerKind.NoEnemyWithin:
                return CountEnemies(senses, trigger.Range, woundedBelow: null, closingOnly: false) == 0;
            case RaceAbilityTriggerKind.HealthBelow:
                return senses.HealthFraction <= trigger.Fraction;
            case RaceAbilityTriggerKind.MoraleBelow:
                return senses.Morale >= 0f && senses.Morale <= trigger.Fraction * 100f;
            case RaceAbilityTriggerKind.TookDamage:
                return senses.TookDamage;
            case RaceAbilityTriggerKind.Mounted:
                return senses.Mounted;
            case RaceAbilityTriggerKind.KinFell:
                foreach (var fallen in senses.FallenKin)
                    if (fallen.Distance <= trigger.Range && IsRecent(fallen.At, senses.Now, trigger.Seconds))
                        return true;
                return false;
            case RaceAbilityTriggerKind.LandedKill:
                return senses.LastKillAt.HasValue && IsRecent(senses.LastKillAt.Value, senses.Now, trigger.Seconds);
            case RaceAbilityTriggerKind.WoundedEnemyWithin:
                return CountEnemies(senses, trigger.Range, trigger.Fraction, closingOnly: false) >= 1;
            case RaceAbilityTriggerKind.CavalryClosing:
                return CountEnemies(senses, trigger.Range, woundedBelow: null, closingOnly: true) >= 1;
            case RaceAbilityTriggerKind.RangedTargetWithin:
                return senses.WieldsRanged && CountEnemies(senses, trigger.Range, woundedBelow: null, closingOnly: false) >= 1;
            case RaceAbilityTriggerKind.KinWithin:
                return CountKinWithin(senses.KinDistances, trigger.Range) >= trigger.Count;
            default:
                return false;
        }
    }

    private static int CountEnemies(RaceAbilitySenses senses, float range, float? woundedBelow, bool closingOnly)
    {
        var count = 0;
        foreach (var enemy in senses.Enemies)
        {
            if (!(enemy.Distance <= range))
                continue;
            if (closingOnly && !enemy.CavalryClosing)
                continue;
            if (woundedBelow.HasValue && !(enemy.HealthFraction <= woundedBelow.Value))
                continue;
            count++;
        }
        return count;
    }

    public int CountKin(List<float> kinDistances, float range) => CountKinWithin(kinDistances, range);

    private static int CountKinWithin(List<float> kinDistances, float range)
    {
        var count = 0;
        foreach (var distance in kinDistances)
            if (distance <= range)
                count++;
        return count;
    }

    // An event at `at` happened no more than `seconds` ago, and not in the future.
    private static bool IsRecent(float at, float now, float seconds)
    {
        var age = now - at;
        return age >= 0f && age <= seconds;
    }

    // How far the sensor must look for enemies and kin: the widest trigger that reads them, and the kin
    // bonus radius. A fallen kinsman is remembered by the mission logic, not scanned for.
    public float ScanRange(RaceAbilityProfile profile)
    {
        var range = Math.Max(ScanRange(profile.Requires), ScanRange(profile.AnyOf));
        return profile.KinBonus != null ? Math.Max(range, profile.KinBonus.Radius) : range;
    }

    private static float ScanRange(List<RaceAbilityTrigger> triggers)
    {
        var range = 0f;
        foreach (var trigger in triggers)
        {
            switch (trigger.ParsedKind)
            {
                case RaceAbilityTriggerKind.EnemyWithin:
                case RaceAbilityTriggerKind.EnemiesWithin:
                case RaceAbilityTriggerKind.NoEnemyWithin:
                case RaceAbilityTriggerKind.WoundedEnemyWithin:
                case RaceAbilityTriggerKind.CavalryClosing:
                case RaceAbilityTriggerKind.RangedTargetWithin:
                case RaceAbilityTriggerKind.KinWithin:
                    range = Math.Max(range, trigger.Range);
                    break;
            }
        }
        return range;
    }

    // Mounted, and the mount's velocity carries it towards the soldier faster than CavalryClosingSpeed. The
    // offset runs from the rider to the soldier.
    public bool IsClosing(float offsetX, float offsetY, float velocityX, float velocityY)
    {
        var length = (float)Math.Sqrt(offsetX * offsetX + offsetY * offsetY);
        if (!(length > 0.001f))
            return false;
        var closing = (offsetX * velocityX + offsetY * velocityY) / length;
        return closing > CavalryClosingSpeed;
    }

    // Who joins a rally: a live AI soldier of the same team and the same ability, not fleeing, off cooldown.
    // The AI check is what keeps the player's own character out of a kinsman's rally.
    public bool IsRallyRecruit(bool isActive, bool isAiControlled, bool isRetreating, bool sameTeam, bool sameProfile, bool offCooldown) =>
        isActive && isAiControlled && !isRetreating && sameTeam && sameProfile && offCooldown;

    // Every third joiner shouts too: a line roars, without thirty voices at once.
    public bool ShoutsOnRally(int rallied) => rallied > 0 && rallied % 3 == 0;

    public float MagnitudeFactor(int battleTier, bool isHero, RaceAbilityTierScaling scaling)
    {
        if (isHero)
            return scaling.HeroFactor;
        var factor = 1f + (battleTier - scaling.BaseTier) * scaling.PercentPerTier / 100f;
        return Math.Min(scaling.MaxFactor, Math.Max(scaling.MinFactor, factor));
    }

    // The kin bonus's extra melee damage for this many kin close by, capped at MaxKin.
    public float KinBonusPercent(RaceAbilityKinBonus? bonus, int kinCount) =>
        bonus == null || kinCount <= 0 ? 0f : Math.Min(kinCount, bonus.MaxKin) * bonus.PerKinPercent;

    // A copy with every magnitude (prices included: an elite pays more for more) multiplied by the factor
    // and kept inside the range the provider allows. extraMeleePercent (the kin bonus) is added before the
    // factor. Booleans, the morale floor and price, and radii are not magnitudes and are copied as they are.
    public RaceAbilityEffects Scale(RaceAbilityEffects effects, float factor, float extraMeleePercent = 0f)
    {
        if (!FiniteFloatValidator.IsFinite(factor) || !(factor > 0f))
            factor = 1f;
        if (!FiniteFloatValidator.IsFinite(extraMeleePercent))
            extraMeleePercent = 0f;
        return new RaceAbilityEffects
        {
            MoveSpeedPercent = Percent(effects.MoveSpeedPercent, factor, 300f),
            AccelerationPercent = Percent(effects.AccelerationPercent, factor, 300f),
            SwingSpeedPercent = Percent(effects.SwingSpeedPercent, factor, 300f),
            DrawSpeedPercent = Percent(effects.DrawSpeedPercent, factor, 300f),
            ReloadSpeedPercent = Percent(effects.ReloadSpeedPercent, factor, 300f),
            MissileSpeedPercent = Percent(effects.MissileSpeedPercent, factor, 300f),
            MountSpeedPercent = Percent(effects.MountSpeedPercent, factor, 300f),
            MeleeDamagePercent = Percent(effects.MeleeDamagePercent + extraMeleePercent, factor, 300f),
            RangedDamagePercent = Percent(effects.RangedDamagePercent, factor, 300f),
            DamageReductionPercent = Math.Min(90f, Math.Max(0f, effects.DamageReductionPercent * factor)),
            KnockdownResistancePercent = Percent(effects.KnockdownResistancePercent, factor, 1000f),
            KnockbackResistancePercent = Percent(effects.KnockbackResistancePercent, factor, 1000f),
            DismountResistancePercent = Percent(effects.DismountResistancePercent, factor, 1000f),
            BlockAbilityPercent = Percent(effects.BlockAbilityPercent, factor, 300f),
            ParryAbilityPercent = Percent(effects.ParryAbilityPercent, factor, 300f),
            AttackEagernessPercent = Percent(effects.AttackEagernessPercent, factor, 300f),
            AimErrorPercent = Percent(effects.AimErrorPercent, factor, 300f),
            ForceCrushThrough = effects.ForceCrushThrough,
            HoldAgainstCrush = effects.HoldAgainstCrush,
            ShrugOffBlows = effects.ShrugOffBlows,
            MoraleFloor = effects.MoraleFloor,
            MoraleOnEnd = effects.MoraleOnEnd,
            HealPerKill = Math.Min(100f, effects.HealPerKill * factor),
            FearOnKillRadius = effects.FearOnKillRadius,
            FearOnKillMorale = Math.Min(100f, effects.FearOnKillMorale * factor),
            FearAuraRadius = effects.FearAuraRadius,
            FearAuraMoralePerSecond = Math.Min(50f, effects.FearAuraMoralePerSecond * factor),
        };
    }

    private static float Percent(float value, float factor, float max) => Math.Min(max, Math.Max(-95f, value * factor));

    // Each kill while active pushes the end out, never past MaxDurationSeconds from activation.
    public float ExtendOnKill(float phaseEndsAt, float activatedAt, RaceAbilityProfile profile)
    {
        if (!(profile.KillExtensionSeconds > 0f))
            return phaseEndsAt;
        var extended = Math.Min(phaseEndsAt + profile.KillExtensionSeconds, activatedAt + profile.MaxDurationSeconds);
        return FiniteFloatValidator.IsFinite(extended) ? Math.Max(phaseEndsAt, extended) : phaseEndsAt;
    }

    // A kill earns credit when the victim died (killed or knocked out) at the hand of someone else, who is
    // still alive, on another team, and carries an ability. Mirrors vanilla's own kill count, which skips a
    // same-team kill.
    public bool CreditsKill(bool victimDied, bool killerIsVictim, bool killerAlive, bool sameTeam, bool killerHasProfile) =>
        victimDied && !killerIsVictim && killerAlive && !sameTeam && killerHasProfile;

    // The killer's health after the heal, never above his limit; unchanged when the result is not finite.
    public float HealOnKill(float health, float healthLimit, float heal)
    {
        if (!(heal > 0f))
            return health;
        var healed = Math.Min(healthLimit, health + heal);
        return FiniteFloatValidator.IsFinite(healed) && healed > health ? healed : health;
    }

    // false: the defender holds and nothing crushes through him. true: a raging attacker's melee swing
    // breaks the block. null: no opinion, so the existing crush-through rules decide.
    public bool? CrushVerdict(RaceAbilityEffects? attacker, RaceAbilityEffects? defender, bool isSwing, bool isPassive)
    {
        if (defender != null && defender.HoldAgainstCrush)
            return false;
        if (attacker != null && attacker.ForceCrushThrough && isSwing && !isPassive)
            return true;
        return null;
    }

    // A melee hit takes MeleeDamagePercent, a missile RangedDamagePercent; a horse charge and a fall keep
    // their damage.
    public float AmplifyHit(float damage, RaceAbilityEffects? attacker, bool isMissile, bool isHorseCharge, bool isFallDamage)
    {
        if (attacker == null || isHorseCharge || isFallDamage)
            return damage;
        var percent = isMissile ? attacker.RangedDamagePercent : attacker.MeleeDamagePercent;
        return percent == 0f ? damage : Finite(damage * (1f + percent / 100f), damage);
    }

    // The soldier's own body only: a hit on his horse is the horse's.
    public float Reduce(float damage, RaceAbilityEffects? victim, bool victimIsMount) =>
        victim == null || victimIsMount || victim.DamageReductionPercent == 0f
            ? damage
            : Finite(damage * (1f - victim.DamageReductionPercent / 100f), damage);

    // Vanilla answers float.MaxValue for a non-human; scaling that overflows, so it stays as it was.
    public float ScaleResistance(float baseResistance, float percent) =>
        percent == 0f ? baseResistance : Finite(baseResistance * (1f + percent / 100f), baseResistance);

    // How much morale to add to hold the soldier at the floor (0 when he is above it or there is none).
    public float MoraleTopUp(float morale, float floor) =>
        floor > 0f && morale < floor ? floor - morale : 0f;

    // While a morale floor is live the soldier cannot panic at all, so a single heavy blow to his morale
    // between two top-ups cannot rout him.
    public bool HoldsNerve(RaceAbilityEffects? effects) => effects != null && effects.MoraleFloor > 0f;

    // One aura pulse: the morale an enemy loses over this many seconds.
    public float AuraDrain(float moralePerSecond, float seconds) =>
        Finite(Math.Max(0f, moralePerSecond) * Math.Max(0f, seconds), 0f);

    private static float Finite(float result, float fallback) =>
        FiniteFloatValidator.IsFinite(result) ? result : fallback;
}
