using System;
using System.Collections.Generic;
using TAOM.Core.Validation;
using TAOM.Features.SignatureStrikes.Domain;

namespace TAOM.Features.SignatureStrikes;

/// <summary>
/// Pure decisions for a signature strike. Profiles are indexed once per process from the
/// validated config (the provider already dropped unknown direction and kind names, so the parse
/// here cannot fail on a row that reached it); the MCM cooldown multiplier is read live.
///
/// Every gate is a positive requirement, so a NaN mission time or last-strike time fails closed
/// (csharp-architecture.md "Engine-Float Decision Gates").
/// </summary>
public sealed class SignatureStrikeService : ISignatureStrikeService
{
    private readonly ISignatureStrikesConfigProvider _configProvider;
    private readonly ISignatureStrikesSettingsProvider _settings;
    private readonly Lazy<Dictionary<StrikeDirection, Profile>> _profiles;

    public SignatureStrikeService(
        ISignatureStrikesConfigProvider configProvider,
        ISignatureStrikesSettingsProvider settings)
    {
        _configProvider = configProvider;
        _settings = settings;
        _profiles = new Lazy<Dictionary<StrikeDirection, Profile>>(IndexProfiles);
    }

    public bool IsEnabled => _settings.IsEnabled;

    public StrikeEffect? Evaluate(in StrikeContext context)
    {
        if (!PassesCommonGates(in context, out var profile))
            return null;

        var basis = ResolveDamageBasis(in context, profile);
        if (!(basis > 0))
            return null;

        return new StrikeEffect(
            profile.Kind,
            profile.OuterRadius,
            profile.InnerRadius,
            basis,
            profile.DamageFraction,
            profile.Magnitude,
            profile.KnockDown,
            profile.KnockBack,
            profile.FearMorale);
    }

    public bool? DecideKnockdown(in StrikeContext context)
        => DecideVerdict(in context, knockdown: true);

    public bool? DecideKnockback(in StrikeContext context)
        => DecideVerdict(in context, knockdown: false);

    public int ComputeRingDamage(int damageBasis, float damageFraction, float falloff, bool victimBlocking)
    {
        if (!(damageBasis > 0)
            || !FiniteFloatValidator.IsFiniteInRange(damageFraction, 0f, 1f)
            || !FiniteFloatValidator.IsFiniteInRange(falloff, 0f, 1f))
            return 0;

        var raw = damageBasis * damageFraction * falloff;
        if (victimBlocking)
            raw *= _configProvider.GetConfig().ShieldBlockedMultiplier;

        // (int) of a float at or past int.MaxValue is int.MinValue on net472: gate the cast on
        // the value, not on arithmetic after it (csharp-architecture.md, category 3).
        if (!FiniteFloatValidator.IsFinite(raw) || !(raw < int.MaxValue) || !(raw >= 0f))
            return 0;

        return (int)Math.Round(raw);
    }

    public float ComputeFearDrain(float scaledFear, float falloff, float raceResist, float currentMorale)
    {
        if (!FiniteFloatValidator.IsFinite(scaledFear)
            || !FiniteFloatValidator.IsFinite(falloff)
            || !FiniteFloatValidator.IsFinite(raceResist)
            || !FiniteFloatValidator.IsFinite(currentMorale))
            return 0f;

        // GetMorale() answers -1f for an agent with no CommonAIComponent; that is "no morale",
        // never "already drained".
        if (!(currentMorale > 0f))
            return 0f;

        var drain = scaledFear * falloff * raceResist;
        if (!(drain > 0f))
            return 0f;

        return Math.Min(drain, currentMorale);
    }

    // A verdict is granted only for the case the engine's own decider would otherwise evaluate:
    // a real strike on an unmounted human that did not shrug it off. Everything else returns null
    // so `base` runs; the knockdown/knockback deciders are also invoked on blocked hits and horse
    // charges, and those must stay vanilla.
    private bool? DecideVerdict(in StrikeContext context, bool knockdown)
    {
        if (!PassesCommonGates(in context, out var profile))
            return null;

        if (!(knockdown ? profile.KnockDown : profile.KnockBack))
            return null;

        if (context.Collision != StrikeCollision.StrikeAgent
            || !context.IsColliderAgent
            || context.AttackBlockedWithShield
            || !context.VictimIsHuman
            || context.VictimIsMounted
            || context.HasShrugOff
            || !(context.InflictedDamage > 0))
            return null;

        return true;
    }

    private bool PassesCommonGates(in StrikeContext context, out Profile profile)
    {
        profile = default!;

        if (!_settings.IsEnabled
            || !context.IsSignatureAttacker
            || context.IsCanceled
            || context.IsAlternativeAttack
            || context.IsMissile
            || context.IsHorseCharge
            || !context.HasMeleeWeapon)
            return false;

        if (!_profiles.Value.TryGetValue(context.Direction, out profile))
            return false;

        return CooldownElapsed(in context, profile.Kind);
    }

    private bool CooldownElapsed(in StrikeContext context, StrikeKind kind)
    {
        if (!FiniteFloatValidator.IsFinite(context.MissionTime))
            return false;

        var last = kind == StrikeKind.Slam ? context.LastSlamTime : context.LastSweepTime;

        // A non-finite last-strike time is "never struck"; the roster seeds entries with NaN.
        if (!FiniteFloatValidator.IsFinite(last))
            return true;

        var config = _configProvider.GetConfig();
        var cooldown = (kind == StrikeKind.Slam ? config.SlamCooldownSeconds : config.SweepCooldownSeconds)
            * _settings.CooldownMultiplier;

        // Positive requirement: elapsed must be at least the cooldown.
        return context.MissionTime - last >= cooldown;
    }

    private int ResolveDamageBasis(in StrikeContext context, Profile profile)
    {
        switch (context.Collision)
        {
            case StrikeCollision.StrikeAgent:
                return context.IsColliderAgent ? context.InflictedDamage : 0;

            case StrikeCollision.HitWorld:
                return profile.WorldHitBaseDamage;

            case StrikeCollision.Blocked:
                // A shield block still computes damage (to the shield); a weapon block cancels it.
                if (!context.AttackBlockedWithShield || !(context.InflictedDamage > 0))
                    return 0;
                return (int)Math.Round(context.InflictedDamage * _configProvider.GetConfig().ShieldBlockedMultiplier);

            default:
                return 0;
        }
    }

    private Dictionary<StrikeDirection, Profile> IndexProfiles()
    {
        var index = new Dictionary<StrikeDirection, Profile>();
        var strikes = _configProvider.GetConfig().Strikes;
        if (strikes == null)
            return index;

        foreach (var pair in strikes)
        {
            if (pair.Value == null
                || !StrikeNames.TryParseDirection(pair.Key, out var direction)
                || !StrikeNames.TryParseKind(pair.Value.Kind, out var kind))
                continue;

            index[direction] = new Profile(kind, pair.Value);
        }

        return index;
    }

    private sealed class Profile
    {
        public Profile(StrikeKind kind, StrikeProfileConfig config)
        {
            Kind = kind;
            OuterRadius = config.OuterRadius;
            InnerRadius = config.InnerRadius;
            DamageFraction = config.DamageFraction;
            WorldHitBaseDamage = config.WorldHitBaseDamage;
            Magnitude = config.Magnitude;
            KnockDown = config.KnockDown;
            KnockBack = config.KnockBack;
            FearMorale = config.FearMorale;
        }

        public StrikeKind Kind { get; }
        public float OuterRadius { get; }
        public float InnerRadius { get; }
        public float DamageFraction { get; }
        public int WorldHitBaseDamage { get; }
        public float Magnitude { get; }
        public bool KnockDown { get; }
        public bool KnockBack { get; }
        public float FearMorale { get; }
    }
}
