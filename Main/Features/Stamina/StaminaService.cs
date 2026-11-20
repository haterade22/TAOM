using System;
using TAOM.Core.Validation;
using TAOM.Features.Stamina.Domain;

namespace TAOM.Features.Stamina;

/// <summary>
/// The stamina rules (#1497, docs/features/stamina.md): the size of a pool, the cost of each action, how a pool
/// drains and comes back between two looks at a soldier, and which band a share of the pool means. Pure: plain
/// values, the cell and the economy in, no engine type, so every rule has a unit test and a simulated fight
/// (StaminaEconomySimulationTests). The engine's action type numbers are named here and pinned against the engine
/// enum in StaminaServiceTests.
/// </summary>
public static class StaminaService
{
    // Agent.ActionCodeType, v1.5.7 Agent.cs:262-300.
    public const int DefendFirst = 1;
    public const int DefendLast = 14;
    public const int ReadyRanged = 15;
    public const int ReleaseRanged = 16;
    public const int ReleaseThrowing = 17;
    public const int ReleaseMelee = 20;
    public const int JumpStart = 24;
    public const int Kick = 28;
    public const int WeaponBash = 31;

    private const int MaxTier = 7;

    public static float MaxPool(StaminaEconomy e, int tier, bool hero, int heroLevel = 0)
    {
        var clamped = tier < 0 ? 0 : tier > MaxTier ? MaxTier : tier;
        var pool = e.PoolBase + e.PoolPerTier * clamped;
        if (!hero)
            return pool;
        var level = heroLevel < 0 ? 0f : heroLevel > e.HeroLevelCap ? e.HeroLevelCap : heroLevel;
        return pool + e.HeroBonus + e.HeroPerLevel * level;
    }

    /// <summary>
    /// A soldier's own values: the pool for his tier and rank times his people's multiplier, his people's regeneration
    /// multiplier, and the cost factor of his armour. The race and culture multipliers are multiplied and then kept
    /// inside the table's limits, so no pairing gives a soldier who cannot tire or cannot fight. An exempt people
    /// has no stamina at all.
    /// </summary>
    public static StaminaProfile Profile(StaminaEconomy e, StaminaPeoples peoples, int tier, bool hero, int heroLevel,
        string? raceName, string? cultureId, float armourKg)
    {
        StaminaPeople? race = null;
        StaminaPeople? culture = null;
        if (!string.IsNullOrEmpty(raceName))
            peoples.Races.TryGetValue(raceName!, out race);
        if (!string.IsNullOrEmpty(cultureId))
            peoples.Cultures.TryGetValue(cultureId!, out culture);
        if ((race != null && race.Exempt) || (culture != null && culture.Exempt))
            return new StaminaProfile(0f, 1f, 1f, exempt: true);
        var pool = Within(peoples, (race?.Pool ?? 1f) * (culture?.Pool ?? 1f));
        var regen = Within(peoples, (race?.Regen ?? 1f) * (culture?.Regen ?? 1f));
        return new StaminaProfile(MaxPool(e, tier, hero, heroLevel) * pool, regen, ArmourFactor(peoples, armourKg), exempt: false);
    }

    /// <summary>What armour does to every cost: 1 up to the free weight, then more with each kilogram, up to the maximum.</summary>
    public static float ArmourFactor(StaminaPeoples peoples, float armourKg)
    {
        // A positive requirement: a weight that is not a number, or at or below the free weight, costs nothing extra.
        if (!(armourKg > peoples.ArmourFreeKg) || !FiniteFloatValidator.IsFinite(armourKg))
            return 1f;
        return Math.Min(peoples.ArmourFactorMax, 1f + (armourKg - peoples.ArmourFreeKg) / peoples.ArmourKgPerStep);
    }

    private static float Within(StaminaPeoples peoples, float multiplier) =>
        !FiniteFloatValidator.IsFinite(multiplier) ? 1f : Math.Min(peoples.MultiplierMax, Math.Max(peoples.MultiplierMin, multiplier));

    /// <summary>A weight that is not a number, or below zero, counts as no weight: the swing costs its minimum.</summary>
    public static float SwingCost(StaminaEconomy e, float weaponKg)
    {
        var kg = weaponKg > 0f && FiniteFloatValidator.IsFinite(weaponKg) ? weaponKg : 0f;
        return Math.Min(e.SwingMax, Math.Max(e.SwingMin, e.SwingBase + e.SwingPerKg * kg));
    }

    public static float ActionCost(StaminaEconomy e, StaminaAction action, float weaponKg)
    {
        switch (action)
        {
            case StaminaAction.Swing: return SwingCost(e, weaponKg);
            case StaminaAction.BowShot: return e.BowShot;
            case StaminaAction.CrossbowShot: return e.CrossbowShot;
            case StaminaAction.Throw: return e.Throw;
            case StaminaAction.Jump: return e.Jump;
            case StaminaAction.Kick: return e.Kick;
            case StaminaAction.Bash: return e.Bash;
            default: return 0f;
        }
    }

    public static float BlockCost(StaminaEconomy e, StaminaBlock block, float shieldDamage)
    {
        switch (block)
        {
            case StaminaBlock.WeaponParry: return e.Parry;
            case StaminaBlock.ShieldMissile: return e.MissileBlock;
            case StaminaBlock.ShieldMelee:
                var damage = shieldDamage > 0f && FiniteFloatValidator.IsFinite(shieldDamage) ? shieldDamage : 0f;
                return Math.Min(e.ShieldMax, e.ShieldBase + e.ShieldPerDamage * damage);
            default: return 0f;
        }
    }

    /// <summary>
    /// The action a soldier started when a channel's action type changed. Only the entry counts: a release that lasts
    /// several looks is one swing. A ranged release is a crossbow shot or a bow shot by the weapon in his hand.
    /// </summary>
    public static StaminaAction Classify(int previousType, int newType, bool crossbow)
    {
        if (previousType == newType)
            return StaminaAction.None;
        switch (newType)
        {
            case ReleaseMelee: return StaminaAction.Swing;
            case ReleaseRanged: return crossbow ? StaminaAction.CrossbowShot : StaminaAction.BowShot;
            case ReleaseThrowing: return StaminaAction.Throw;
            case JumpStart: return StaminaAction.Jump;
            case Kick: return StaminaAction.Kick;
            case WeaponBash: return StaminaAction.Bash;
            default: return StaminaAction.None;
        }
    }

    /// <summary>
    /// Whether a soldier has stamina under the two MCM switches: the player can exempt the soldier he controls and
    /// leave the AI's soldiers tiring, or the reverse (lessons/gamemodels-services.md, the player opt-out rule).
    /// </summary>
    public static bool Applies(bool playerSoldier, bool applyToPlayer, bool applyToAi) => playerSoldier ? applyToPlayer : applyToAi;

    public static bool IsBlocking(int actionType) => actionType >= DefendFirst && actionType <= DefendLast;

    public static bool IsDrawn(int actionType) => actionType == ReadyRanged;

    /// <summary>The soldier's stamina as a share of his pool. A soldier with no pool yet reads full.</summary>
    public static float FractionOf(StaminaCell cell) => cell.Max > 0f ? cell.Current / cell.Max : 1f;

    /// <summary>The soldier's own action: it costs stamina and pauses his regeneration. An Exhausted soldier pays nothing.</summary>
    public static void Charge(StaminaCell cell, float cost, float now, StaminaEconomy e)
    {
        if (cell.Band == StaminaBand.Exhausted || !(cost > 0f))
            return;
        cell.Current = Math.Max(0f, cell.Current - cost * cell.CostFactor);
        cell.RegenPausedUntil = now + e.RegenPauseSeconds;
        cell.EverFought = true;
    }

    /// <summary>
    /// A look at a soldier the rules do not step now (not subject, a forced band, at a siege engine): the blows he
    /// stopped meanwhile are dropped. Kept, they would all land at the look that steps him again.
    /// </summary>
    public static void Skip(StaminaCell cell) => cell.TakePending();

    /// <summary>
    /// One look at a soldier: the costs the callbacks found (stopped blows), a held bow, the run drain down to its
    /// floor, or regeneration. While Exhausted nothing drains and the run test does not count: without that, a soldier
    /// ordered to charge at walking pace would read as running and never recover.
    /// </summary>
    public static void Step(StaminaCell cell, in StaminaSample s, StaminaEconomy e)
    {
        var exhausted = cell.Band == StaminaBand.Exhausted;
        // Always taken, so the blows an Exhausted soldier stopped cannot pile up. A soldier who is not stepped at all
        // loses his through Skip.
        var pending = cell.TakePending();
        if (!exhausted && pending > 0f)
        {
            cell.Current -= pending * cell.CostFactor;
            cell.EverFought = true;
        }

        if (!exhausted && s.BowHeldSeconds > 0f)
        {
            cell.Current -= e.BowHoldPerSecond * s.BowHeldSeconds * cell.CostFactor;
            cell.RegenPausedUntil = s.Now + e.RegenPauseSeconds;
        }

        // A positive requirement: an interval that is zero (a paused mission) or not a number moves nothing.
        if (s.Dt > 0f)
        {
            if (s.Running && !exhausted)
            {
                var floor = e.RunFloor * cell.Max;
                if (cell.Current > floor)
                    cell.Current = Math.Max(floor, cell.Current - e.RunDrainPerSecond * cell.Max * s.Dt);
            }
            else if (s.Now >= cell.RegenPausedUntil)
            {
                var rate = e.RegenPerSecond * cell.Max * cell.RegenFactor;
                if (s.Blocking)
                    rate *= e.BlockingRegen;
                if (exhausted)
                    rate *= e.ExhaustedRegen;
                cell.Current += rate * s.Dt;
            }
        }

        // A value that is not a number ends at zero, where regeneration brings it back.
        if (!(cell.Current > 0f))
            cell.Current = 0f;
        if (cell.Current > cell.Max)
            cell.Current = cell.Max;
    }

    /// <summary>
    /// The band for a share of the pool, from the band the soldier is in. Each edge has a hysteresis so a share near
    /// it does not flap. Exhausted starts at zero and ends only at RecoverAt, into Tired, so a recovered soldier is
    /// still cautious.
    /// </summary>
    public static StaminaBand NextBand(StaminaEconomy e, StaminaBand current, float fraction)
    {
        if (current == StaminaBand.Exhausted)
            return fraction >= e.RecoverAt ? StaminaBand.Tired : StaminaBand.Exhausted;
        if (!(fraction > 0f))
            return StaminaBand.Exhausted;
        var leaveTired = e.TiredBelow + e.Hysteresis;
        var leaveWinded = e.WindedBelow + e.Hysteresis;
        switch (current)
        {
            case StaminaBand.Tired:
                return fraction < leaveTired ? StaminaBand.Tired : fraction < leaveWinded ? StaminaBand.Winded : StaminaBand.Fresh;
            case StaminaBand.Winded:
                return fraction < e.TiredBelow ? StaminaBand.Tired : fraction < leaveWinded ? StaminaBand.Winded : StaminaBand.Fresh;
            default:
                return fraction < e.TiredBelow ? StaminaBand.Tired : fraction < e.WindedBelow ? StaminaBand.Winded : StaminaBand.Fresh;
        }
    }
}
