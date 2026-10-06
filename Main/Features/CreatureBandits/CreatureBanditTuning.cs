using TAOM.Core.Validation;
using TAOM.Features.Spider;
using TaleWorlds.Core;

namespace TAOM.Features.CreatureBandits;

/// <summary>
/// The creature bandit's own numbers (#692), apart from the ridden spider's (<see cref="SpiderConfig"/>): its hit points,
/// and for each attack how many soldiers it may strike, a damage multiplier and whether only a crit knocks a soldier
/// down, plus the attack cooldowns. The constants are the defaults; the MCM "Creature Bandits" options
/// override them (<see cref="Current"/>). Every value is clamped, so a bad entry cannot break a strike or a spawn.
///
/// Mike's first cut (2026-09-28), after the playtest where one uncapped bite struck 23 soldiers and archers killed a
/// 120 HP spider in 3 to 5 s: 200 HP; the bite (standing front strike) hits 1 soldier, the pounce (running lunge) 2, the
/// swipe (left or right) up to 3 at half damage; only a crit knocks down. Damage taken: half from missiles, full from
/// melee (<see cref="TakenFactor"/>), applied by CreatureBanditDamage in both the campaign and Custom Battle damage models.
/// </summary>
public sealed class CreatureBanditTuning
{
    public const float DefaultHitPoints = 200f;
    public const float MinHitPoints = 50f;
    public const float MaxHitPoints = 1000f;
    public const int DefaultBiteTargets = 1;
    public const int DefaultPounceTargets = 2;
    public const int DefaultSwipeTargets = 3;
    public const int MaxTargetsCap = 10;
    public const int DefaultBiteDamagePercent = 100;
    public const int DefaultPounceDamagePercent = 100;
    public const int DefaultSwipeDamagePercent = 50;
    public const int MaxDamagePercent = 300;
    public const bool DefaultKnockdownOnCritOnly = true;
    public const float MinCooldownSeconds = 0.5f;
    public const float MaxCooldownSeconds = 30f;
    public const int DefaultMissileTakenPercent = 50;
    public const int DefaultMeleeTakenPercent = 100;
    public const int MaxTakenPercent = 200;

    public static readonly CreatureBanditTuning Defaults = From(DefaultHitPoints, DefaultBiteTargets, DefaultPounceTargets,
        DefaultSwipeTargets, DefaultBiteDamagePercent, DefaultPounceDamagePercent, DefaultSwipeDamagePercent,
        DefaultKnockdownOnCritOnly, (float)SpiderConfig.PounceCooldownSeconds, (float)SpiderConfig.SideAttackCooldownSeconds);

    private CreatureBanditTuning(float hitPoints, SpiderStrikeSet strikes, double pounceCooldownSeconds,
        double swipeCooldownSeconds, float missileTaken, float cutTaken, float pierceTaken, float bluntTaken)
    {
        MissileTakenFactor = missileTaken;
        CutTakenFactor = cutTaken;
        PierceTakenFactor = pierceTaken;
        BluntTakenFactor = bluntTaken;
        HitPoints = hitPoints;
        Strikes = strikes;
        PounceCooldownSeconds = pounceCooldownSeconds;
        SwipeCooldownSeconds = swipeCooldownSeconds;
    }

    public float HitPoints { get; }

    /// <summary>The bite, pounce and swipe rules; the attack tasks pick one per clip (<see cref="SpiderStrikeSet.For"/>).</summary>
    public SpiderStrikeSet Strikes { get; }
    public double PounceCooldownSeconds { get; }
    public double SwipeCooldownSeconds { get; }
    public float MissileTakenFactor { get; }
    public float CutTakenFactor { get; }
    public float PierceTakenFactor { get; }
    public float BluntTakenFactor { get; }

    /// <summary>
    /// The share of a blow's damage the creature takes: every missile (arrow, bolt, javelin, thrown weapon) by the missile
    /// rule, a melee blow by its damage type. <paramref name="bluntByRule"/> is vanilla's own correction, which makes a
    /// charge, kick, bash, hilt hit or bare hand Blunt whatever the weapon's type (TaomAgentApplyDamageModel). An
    /// unknown type is untouched.
    /// </summary>
    public float TakenFactor(bool isMissile, DamageTypes damageType, bool bluntByRule = false)
    {
        if (isMissile) return MissileTakenFactor;
        return (bluntByRule ? DamageTypes.Blunt : damageType) switch
        {
            DamageTypes.Cut => CutTakenFactor,
            DamageTypes.Pierce => PierceTakenFactor,
            DamageTypes.Blunt => BluntTakenFactor,
            _ => 1f,
        };
    }

    public static CreatureBanditTuning From(float hitPoints, int biteTargets, int pounceTargets, int swipeTargets,
        int bitePercent, int pouncePercent, int swipePercent, bool knockdownOnCritOnly, float pounceCooldown, float swipeCooldown,
        int missilePercent = DefaultMissileTakenPercent, int cutPercent = DefaultMeleeTakenPercent,
        int piercePercent = DefaultMeleeTakenPercent, int bluntPercent = DefaultMeleeTakenPercent)
    {
        SpiderStrikeProfile Strike(int targets, int percent) => new(
            SettingClamp.Clamp(targets, 1, 1, MaxTargetsCap),
            SettingClamp.Clamp(percent, 100, 0, MaxDamagePercent) / 100f,
            knockdownOnCritOnly);

        return new CreatureBanditTuning(
            SettingClamp.Clamp(hitPoints, DefaultHitPoints, MinHitPoints, MaxHitPoints),
            new SpiderStrikeSet(Strike(biteTargets, bitePercent), Strike(pounceTargets, pouncePercent), Strike(swipeTargets, swipePercent)),
            SettingClamp.Clamp(pounceCooldown, (float)SpiderConfig.PounceCooldownSeconds, MinCooldownSeconds, MaxCooldownSeconds),
            SettingClamp.Clamp(swipeCooldown, (float)SpiderConfig.SideAttackCooldownSeconds, MinCooldownSeconds, MaxCooldownSeconds),
            TakenShare(missilePercent, DefaultMissileTakenPercent), TakenShare(cutPercent, DefaultMeleeTakenPercent),
            TakenShare(piercePercent, DefaultMeleeTakenPercent), TakenShare(bluntPercent, DefaultMeleeTakenPercent));
    }

    // The settings object MCM keeps for TaomSettings, taken on the first non-null read and read through: an MCM edit lands
    // in it, so a change still applies at once, and no read walks MCM's settings containers again (#746). The write is
    // an idempotent reference publish, safe from the damage model's thread.
    private static TaomSettings? _settings;
    private static TaomSettings? Settings => _settings ??= TaomSettings.Instance;

    /// <summary>Installs the settings a test reads through; null puts MCM back.</summary>
    internal static void UseSettings(TaomSettings? settings) => _settings = settings;

    /// <summary>
    /// The MCM values (a new object each call), or the shared <see cref="Defaults"/> when MCM is not loaded, for the main thread's
    /// spawn, tree build and attack. The per-hit damage step asks <see cref="CurrentTakenFactor"/> instead.
    /// </summary>
    public static CreatureBanditTuning Current
    {
        get
        {
            var s = Settings;
            if (s == null) return Defaults;
            return From(s.CreatureBanditHitPoints, s.CreatureBanditBiteTargets, s.CreatureBanditPounceTargets,
                s.CreatureBanditSwipeTargets, s.CreatureBanditBiteDamagePercent, s.CreatureBanditPounceDamagePercent,
                s.CreatureBanditSwipeDamagePercent, s.CreatureBanditKnockdownOnCritOnly, s.CreatureBanditPounceCooldownSeconds,
                s.CreatureBanditSwipeCooldownSeconds, s.CreatureBanditMissileTakenPercent, s.CreatureBanditCutTakenPercent,
                s.CreatureBanditPierceTakenPercent, s.CreatureBanditBluntTakenPercent);
        }
    }

    /// <summary>
    /// <see cref="TakenFactor"/> on the live MCM values, without building a tuning: CreatureBanditDamage asks it on every
    /// hit against a creature, on whichever thread the engine runs the damage model, so it reads the kept settings object
    /// and allocates nothing (#746; CreatureBanditTuningLiveTests pins both the rule and the allocation).
    /// </summary>
    public static float CurrentTakenFactor(bool isMissile, DamageTypes damageType, bool bluntByRule = false)
    {
        var s = Settings;
        if (s == null) return Defaults.TakenFactor(isMissile, damageType, bluntByRule);
        if (isMissile) return TakenShare(s.CreatureBanditMissileTakenPercent, DefaultMissileTakenPercent);
        return (bluntByRule ? DamageTypes.Blunt : damageType) switch
        {
            DamageTypes.Cut => TakenShare(s.CreatureBanditCutTakenPercent, DefaultMeleeTakenPercent),
            DamageTypes.Pierce => TakenShare(s.CreatureBanditPierceTakenPercent, DefaultMeleeTakenPercent),
            DamageTypes.Blunt => TakenShare(s.CreatureBanditBluntTakenPercent, DefaultMeleeTakenPercent),
            _ => 1f,
        };
    }

    private static float TakenShare(int percent, int fallback) => SettingClamp.Clamp(percent, fallback, 0, MaxTakenPercent) / 100f;

    /// <summary>The active numbers for the spawn diagnostics line, so a balance run is attributable.</summary>
    public string Describe() => System.FormattableString.Invariant(
        $"hp={HitPoints:0} bite={Strikes.Bite.MaxTargets}x{Strikes.Bite.DamageMultiplier:0.00} pounce={Strikes.Pounce.MaxTargets}x{Strikes.Pounce.DamageMultiplier:0.00} ") +
        System.FormattableString.Invariant(
        $"swipe={Strikes.Swipe.MaxTargets}x{Strikes.Swipe.DamageMultiplier:0.00} kd={(Strikes.Bite.KnockdownOnCritOnly ? "crit" : "threshold")} ") +
        System.FormattableString.Invariant($"cd={PounceCooldownSeconds:0.0}/{SwipeCooldownSeconds:0.0} ") +
        System.FormattableString.Invariant(
        $"taken=missile{MissileTakenFactor:0.00}/cut{CutTakenFactor:0.00}/pierce{PierceTakenFactor:0.00}/blunt{BluntTakenFactor:0.00}");
}
