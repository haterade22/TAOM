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

    // Lost health since the previous decision. The first decision has no sample (-1), and a NaN on either side
    // reads as unhurt.
    public static bool TookDamage(float lastHealth, float health) => lastHealth >= 0f && health < lastHealth;

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
                return CountKin(senses.KinDistances, trigger.Range) >= trigger.Count;
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

    // Kin no further than the range. The engine's proximity query is flat, so a kinsman it returns can still
    // be further away than the range in three dimensions.
    public static int CountKin(List<float> kinDistances, float range)
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

    // What the sensor still has to gather once the soldier's own senses are in: enemies out to the widest
    // trigger that can still hold, the closing speed only as far as a CavalryClosing trigger reads, kin only
    // as far as a KinWithin trigger reads (the kin bonus counts its own kin at activation), and the fallen
    // kin only for a KinFell trigger. A Requires trigger his own state already fails, or an AnyOf list in
    // which no trigger can still hold, means nothing can fire him this pass, so nothing is scanned. Never
    // changes what FiringTrigger answers; it only leaves out what could not change it.
    public RaceAbilityScanPlan PlanScan(RaceAbilityProfile profile, RaceAbilitySenses self)
    {
        foreach (var trigger in profile.Requires)
            if (!CanStillHold(trigger, self))
                return default;
        if (profile.AnyOf.Count > 0)
        {
            var any = false;
            foreach (var trigger in profile.AnyOf)
                any |= CanStillHold(trigger, self);
            if (!any)
                return default;
        }

        float enemy = 0f, closing = 0f, kin = 0f;
        var fallen = false;
        Widen(profile.Requires, self, ref enemy, ref closing, ref kin, ref fallen);
        Widen(profile.AnyOf, self, ref enemy, ref closing, ref kin, ref fallen);
        return new RaceAbilityScanPlan(enemy, closing, kin, fallen);
    }

    private static void Widen(List<RaceAbilityTrigger> triggers, RaceAbilitySenses self,
        ref float enemy, ref float closing, ref float kin, ref bool fallen)
    {
        foreach (var trigger in triggers)
        {
            if (!CanStillHold(trigger, self))
                continue;
            switch (trigger.ParsedKind)
            {
                case RaceAbilityTriggerKind.EnemyWithin:
                case RaceAbilityTriggerKind.EnemiesWithin:
                case RaceAbilityTriggerKind.NoEnemyWithin:
                case RaceAbilityTriggerKind.WoundedEnemyWithin:
                case RaceAbilityTriggerKind.RangedTargetWithin:
                    enemy = Math.Max(enemy, trigger.Range);
                    break;
                case RaceAbilityTriggerKind.CavalryClosing:
                    enemy = Math.Max(enemy, trigger.Range);
                    closing = Math.Max(closing, trigger.Range);
                    break;
                case RaceAbilityTriggerKind.KinWithin:
                    kin = Math.Max(kin, trigger.Range);
                    break;
                case RaceAbilityTriggerKind.KinFell:
                    fallen = true;
                    break;
            }
        }
    }

    // A trigger that reads only the soldier is decided by his own senses already, and RangedTargetWithin
    // cannot hold without a ranged weapon in hand; every other trigger waits for the scan.
    private static bool CanStillHold(RaceAbilityTrigger trigger, RaceAbilitySenses self) => trigger.ParsedKind switch
    {
        RaceAbilityTriggerKind.Always or RaceAbilityTriggerKind.HealthBelow or RaceAbilityTriggerKind.MoraleBelow
            or RaceAbilityTriggerKind.TookDamage or RaceAbilityTriggerKind.Mounted or RaceAbilityTriggerKind.LandedKill
            => Holds(trigger, self),
        RaceAbilityTriggerKind.RangedTargetWithin => self.WieldsRanged,
        _ => true,
    };

    // After the enemy scan: whether every Requires trigger that kin and fallen kin do not decide holds. When
    // one fails nothing can fire him, so the ally scan and the fallen-kin walk are skipped.
    public bool RequiresHoldBeforeKin(RaceAbilityProfile profile, RaceAbilitySenses senses)
    {
        foreach (var trigger in profile.Requires)
            if (trigger.ParsedKind != RaceAbilityTriggerKind.KinWithin && trigger.ParsedKind != RaceAbilityTriggerKind.KinFell
                && !Holds(trigger, senses))
                return false;
        return true;
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

    // A kill earns credit when a soldier died (killed or knocked out) at the hand of someone else, who is
    // still alive, on another team, and carries an ability. A horse is no soldier: mounts carry no team, so
    // without that gate a dead horse would pass the same-team check and feed every kill effect. Vanilla's
    // morale-on-kill likewise skips non-humans and same-team kills.
    public bool CreditsKill(bool victimDied, bool victimIsSoldier, bool killerIsVictim, bool killerAlive, bool sameTeam,
        bool killerHasProfile) =>
        victimDied && victimIsSoldier && !killerIsVictim && killerAlive && !sameTeam && killerHasProfile;

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
    public float AmplifyHit(float damage, RaceAbilityEffects? attacker, bool isMissile, bool isHorseCharge, bool isFallDamage) =>
        attacker == null || isHorseCharge || isFallDamage
            ? damage
            : Times(damage, isMissile ? attacker.RangedDamagePercent : attacker.MeleeDamagePercent);

    // Blows on the soldier's own body only: a hit on his horse is the horse's, and a fall keeps its damage.
    public float Reduce(float damage, RaceAbilityEffects? victim, bool victimIsMount, bool isFallDamage) =>
        victim == null || victimIsMount || isFallDamage ? damage : Times(damage, -victim.DamageReductionPercent);

    // Vanilla answers float.MaxValue for a non-human; scaling that overflows, so it stays as it was.
    public float ScaleResistance(float baseResistance, float percent) => Times(baseResistance, percent);

    // The morale an active window costs when it ends (the orcs' Swarm); a spent phase ending costs nothing.
    public float MoraleOnEnd(RaceAbilityState before) =>
        before.Phase == RaceAbilityPhase.Active ? before.ActiveEffects.MoraleOnEnd : 0f;

    // How much morale to add to hold the soldier at the floor (0 when he is above it or there is none).
    public float MoraleTopUp(float morale, float floor) =>
        floor > 0f && morale < floor ? floor - morale : 0f;

    // While a morale floor is live the soldier cannot panic at all, so a single heavy blow to his morale
    // between two top-ups cannot rout him.
    public bool HoldsNerve(RaceAbilityEffects? effects) => effects != null && effects.MoraleFloor > 0f;

    // One aura pulse: the morale an enemy loses over this many seconds.
    public float AuraDrain(float moralePerSecond, float seconds) =>
        Finite(Math.Max(0f, moralePerSecond) * Math.Max(0f, seconds), 0f);

    // A value raised by a percentage (negative lowers it); unchanged at 0%, or when the result would not be
    // finite. Every percentage the abilities apply, to damage, resistances and driven properties, goes here.
    public static float Times(float value, float percent) =>
        percent == 0f ? value : Finite(value * (1f + percent / 100f), value);

    // A duration shortened by the percentage (TopSpeedReachDuration: faster acceleration, less time).
    public static float Over(float value, float percent) =>
        percent == 0f ? value : Finite(value / (1f + percent / 100f), value);

    // A probability raised by the percentage and kept within 0 to 1.
    public static float Chance(float value, float percent)
    {
        var result = Times(value, percent);
        return FiniteFloatValidator.IsFinite(result) ? Math.Min(1f, Math.Max(0f, result)) : result;
    }

    private static float Finite(float result, float fallback) =>
        FiniteFloatValidator.IsFinite(result) ? result : fallback;
}
