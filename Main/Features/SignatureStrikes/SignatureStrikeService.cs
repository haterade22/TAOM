using System;
using System.Collections.Generic;
using TAOM.Core.Validation;
using TAOM.Features.SignatureStrikes.Domain;

namespace TAOM.Features.SignatureStrikes;

/// <summary>
/// Pure decisions for a signature strike. Each signature's profiles and cooldowns are indexed once
/// per process from the validated config, in config order, so the index the registry resolved at
/// spawn addresses them directly (the provider already dropped unknown direction and kind names
/// and every strike whose kind has no cooldown, so the parse here cannot fail on a row that
/// reached it); the MCM cooldown multiplier is read live.
///
/// Every gate is a positive requirement, so a NaN mission time or last-strike time fails closed
/// (csharp-architecture.md "Engine-Float Decision Gates").
/// </summary>
public sealed class SignatureStrikeService : ISignatureStrikeService
{
    private readonly ISignatureStrikesConfigProvider _configProvider;
    private readonly ISignatureStrikesSettingsProvider _settings;
    private readonly Lazy<SignatureTable[]> _signatures;

    public SignatureStrikeService(
        ISignatureStrikesConfigProvider configProvider,
        ISignatureStrikesSettingsProvider settings)
    {
        _configProvider = configProvider;
        _settings = settings;
        _signatures = new Lazy<SignatureTable[]>(IndexSignatures);
    }

    public bool IsEnabled => _settings.IsEnabled;

    public StrikeEffect? Evaluate(in StrikeContext context)
    {
        if (!PassesCommonGates(in context, out var signature, out var profile))
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
            profile.FearMorale,
            profile.Origin,
            profile.Sound,
            signature.Id);
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

        return RoundToDamage(raw);
    }

    // (int) of a float at or past int.MaxValue is int.MinValue on net472: gate the cast on the
    // value, not on arithmetic after it (csharp-architecture.md, category 3). One cast for both
    // damage bases so the two cannot drift (Codex review 114, O2).
    private static int RoundToDamage(float raw)
    {
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
        if (!PassesCommonGates(in context, out _, out var profile))
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

    private bool PassesCommonGates(in StrikeContext context, out SignatureTable signature, out Profile profile)
    {
        signature = default!;
        profile = default!;

        if (!_settings.IsEnabled
            || !context.IsSignatureAttacker
            || context.IsCanceled
            || context.IsAlternativeAttack
            || context.IsMissile
            || context.IsHorseCharge
            || !context.HasMeleeWeapon)
            return false;

        var signatures = _signatures.Value;
        if (context.SignatureIndex < 0 || context.SignatureIndex >= signatures.Length)
            return false;

        signature = signatures[context.SignatureIndex];
        if (!signature.Profiles.TryGetValue(context.Direction, out profile)
            || !signature.Cooldowns.TryGetValue(profile.Kind, out var cooldownSeconds))
            return false;

        return CooldownElapsed(in context, profile.Kind, cooldownSeconds);
    }

    private bool CooldownElapsed(in StrikeContext context, StrikeKind kind, float cooldownSeconds)
    {
        if (!FiniteFloatValidator.IsFinite(context.MissionTime))
            return false;

        var last = context.LastStrikeTimes.Get(kind);

        // A non-finite last-strike time is "never struck"; the roster seeds entries with NaN.
        if (!FiniteFloatValidator.IsFinite(last))
            return true;

        // Positive requirement: elapsed must be at least the cooldown.
        return context.MissionTime - last >= cooldownSeconds * _settings.CooldownMultiplier;
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
                return RoundToDamage(context.InflictedDamage * _configProvider.GetConfig().ShieldBlockedMultiplier);

            default:
                return 0;
        }
    }

    private SignatureTable[] IndexSignatures()
    {
        var signatures = _configProvider.GetConfig().Signatures ?? new List<SignatureConfig>();
        var tables = new SignatureTable[signatures.Count];
        for (var i = 0; i < signatures.Count; i++)
            tables[i] = new SignatureTable(signatures[i]);
        return tables;
    }

    private sealed class SignatureTable
    {
        public SignatureTable(SignatureConfig? config)
        {
            Id = config?.Id ?? "";

            if (config?.Strikes != null)
            {
                foreach (var pair in config.Strikes)
                {
                    // All three names fail closed alike: a ring centred on the wrong point is worse
                    // than no ring (the provider reverts an unknown origin before it gets here).
                    if (pair.Value == null
                        || !StrikeNames.TryParseDirection(pair.Key, out var direction)
                        || !StrikeNames.TryParseKind(pair.Value.Kind, out var kind)
                        || !StrikeNames.TryParseOrigin(pair.Value.Origin, out var origin))
                        continue;

                    Profiles[direction] = new Profile(kind, origin, pair.Value);
                }
            }

            if (config?.Cooldowns != null)
            {
                foreach (var pair in config.Cooldowns)
                {
                    if (StrikeNames.TryParseKind(pair.Key, out var kind))
                        Cooldowns[kind] = pair.Value;
                }
            }
        }

        public string Id { get; }

        public Dictionary<StrikeDirection, Profile> Profiles { get; } = new Dictionary<StrikeDirection, Profile>();

        public Dictionary<StrikeKind, float> Cooldowns { get; } = new Dictionary<StrikeKind, float>();
    }

    private sealed class Profile
    {
        public Profile(StrikeKind kind, StrikeOrigin origin, StrikeProfileConfig config)
        {
            Kind = kind;
            Origin = origin;
            OuterRadius = config.OuterRadius;
            InnerRadius = config.InnerRadius;
            DamageFraction = config.DamageFraction;
            WorldHitBaseDamage = config.WorldHitBaseDamage;
            Magnitude = config.Magnitude;
            KnockDown = config.KnockDown;
            KnockBack = config.KnockBack;
            FearMorale = config.FearMorale;
            Sound = string.IsNullOrWhiteSpace(config.Sound) ? null : config.Sound;
        }

        public StrikeKind Kind { get; }
        public StrikeOrigin Origin { get; }
        public float OuterRadius { get; }
        public float InnerRadius { get; }
        public float DamageFraction { get; }
        public int WorldHitBaseDamage { get; }
        public float Magnitude { get; }
        public bool KnockDown { get; }
        public bool KnockBack { get; }
        public float FearMorale { get; }
        public string? Sound { get; }
    }
}
