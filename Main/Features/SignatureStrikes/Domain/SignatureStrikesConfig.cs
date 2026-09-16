using System.Collections.Generic;

namespace TAOM.Features.SignatureStrikes.Domain;

/// <summary>
/// Deserialization target for <c>signature_strikes/signature_strikes_config.json</c>.
///
/// Deserialized with <c>ObjectCreationHandling.Replace</c> so a JSON list or dictionary REPLACES
/// the compiled default instead of Json.NET's append-merge. The compiled defaults below ARE the
/// shipped values; the JSON restates them so an author sees every knob.
/// </summary>
public class SignatureStrikesConfig
{
    public bool Enabled { get; set; } = true;

    /// <summary>Hero StringIds with signature strikes. <c>lord_1_17</c> is Sauron. Listed as well
    /// as via <see cref="Races"/> so the feature survives a data change that drops his race.</summary>
    public List<string> HeroIds { get; set; } = new List<string> { "lord_1_17" };

    /// <summary>FaceGen race names with signature strikes. The race axis is what finds Sauron in a
    /// custom battle, where the agent carries no HeroObject.</summary>
    public List<string> Races { get; set; } = new List<string> { "sauron" };

    /// <summary>Seconds between two slams by the same attacker, in mission time. Long on purpose:
    /// a slam is guaranteed knockdown + ring + fear in one package (Mike, 2026-09-16).</summary>
    public float SlamCooldownSeconds { get; set; } = 20f;

    /// <summary>Seconds between two sweeps by the same attacker, in mission time.</summary>
    public float SweepCooldownSeconds { get; set; } = 12f;

    /// <summary>Multiplier on the damage basis when the hit landed on a shield, and on a ring
    /// victim's damage while that victim is shield-blocking (elephant trample parity).</summary>
    public float ShieldBlockedMultiplier { get; set; } = 0.25f;

    /// <summary>Per-direction profiles keyed by <see cref="StrikeDirection"/> name. A direction
    /// with no row is a plain hit; the shipped file leaves Thrust out.</summary>
    public Dictionary<string, StrikeProfileConfig> Strikes { get; set; } = DefaultStrikes();

    public static Dictionary<string, StrikeProfileConfig> DefaultStrikes() => new Dictionary<string, StrikeProfileConfig>
    {
        [nameof(StrikeDirection.Overhead)] = new StrikeProfileConfig
        {
            Kind = nameof(StrikeKind.Slam),
            OuterRadius = 4f,
            InnerRadius = 1.5f,
            DamageFraction = 0.6f,
            WorldHitBaseDamage = 60,
            Magnitude = 80f,
            KnockDown = true,
            KnockBack = false,
            FearMorale = 15f,
        },
        [nameof(StrikeDirection.Left)] = SweepProfile(),
        [nameof(StrikeDirection.Right)] = SweepProfile(),
    };

    private static StrikeProfileConfig SweepProfile() => new StrikeProfileConfig
    {
        Kind = nameof(StrikeKind.Sweep),
        OuterRadius = 3f,
        InnerRadius = 1.5f,
        DamageFraction = 0.25f,
        WorldHitBaseDamage = 0,
        Magnitude = 60f,
        KnockDown = false,
        KnockBack = true,
        FearMorale = 0f,
    };
}

/// <summary>One direction's effect. Radii in metres; the falloff is the engine's boulder curve
/// (full inside <see cref="InnerRadius"/>, one ninth at <see cref="OuterRadius"/>).</summary>
public class StrikeProfileConfig
{
    /// <summary>A <see cref="StrikeKind"/> name. Unknown names drop the row with a warning.</summary>
    public string Kind { get; set; } = nameof(StrikeKind.Slam);

    public float OuterRadius { get; set; } = 3f;

    public float InnerRadius { get; set; } = 1.5f;

    /// <summary>Fraction of the triggering hit's damage a ring victim takes at full falloff.</summary>
    public float DamageFraction { get; set; } = 0.25f;

    /// <summary>Damage basis when the swing hits the ground instead of an agent. 0 means a ground
    /// hit does nothing for this direction.</summary>
    public int WorldHitBaseDamage { get; set; }

    /// <summary>Blow impulse for ring victims (the ragdoll shove on a kill or a knockdown).</summary>
    public float Magnitude { get; set; } = 60f;

    /// <summary>Ring victims are knocked down (mounted ones dismounted); on the primary victim,
    /// the knockdown is guaranteed regardless of the engine's sweet spot and HP threshold.</summary>
    public bool KnockDown { get; set; }

    /// <summary>Ring victims and the primary victim get the engine's knock-back stagger, which
    /// vanilla never grants to a melee swing.</summary>
    public bool KnockBack { get; set; }

    /// <summary>One-shot morale drain on ring victims, before tier and race resistance. 0 disables.</summary>
    public float FearMorale { get; set; }
}
