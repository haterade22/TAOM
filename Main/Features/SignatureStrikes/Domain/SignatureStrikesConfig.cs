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

    /// <summary>Multiplier on the damage basis when the hit landed on a shield, and on a ring
    /// victim's damage while that victim is shield-blocking (elephant trample parity). Global: a
    /// shield is a shield whoever strikes it.</summary>
    public float ShieldBlockedMultiplier { get; set; } = 0.25f;

    /// <summary>One entry per group of heroes with signature strikes (#645). An agent carries the
    /// first signature whose hero ids or hero sets name it, else the first whose races do.</summary>
    public List<SignatureConfig> Signatures { get; set; } = DefaultSignatures();

    public static List<SignatureConfig> DefaultSignatures() => new List<SignatureConfig> { Sauron(), Nazgul() };

    /// <summary>Sauron (#605): the overhead is a Slam, the side swings a Sweep. Long cooldowns on
    /// purpose, Sauron must not be overpowered (Mike, 2026-09-16).</summary>
    public static SignatureConfig Sauron() => new SignatureConfig
    {
        Id = "sauron",
        HeroIds = new List<string> { "lord_1_17" },
        Races = new List<string> { "sauron" },
        Cooldowns = new Dictionary<string, float>
        {
            [nameof(StrikeKind.Slam)] = 20f,
            [nameof(StrikeKind.Sweep)] = 12f,
        },
        Strikes = new Dictionary<string, StrikeProfileConfig>
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
            [nameof(StrikeDirection.Left)] = SauronSweep(),
            [nameof(StrikeDirection.Right)] = SauronSweep(),
        },
    };

    /// <summary>The Nine (#645): the overhead and both side swings SCREAM, one shared timer.
    /// Scream + stagger, no knockdown, morale 25, every 15 s (Mike, 2026-09-23).</summary>
    public static SignatureConfig Nazgul() => new SignatureConfig
    {
        Id = "nazgul",
        HeroSets = new List<string> { "nazgul_nine" },
        Races = new List<string> { "nazghul" },
        Cooldowns = new Dictionary<string, float> { [nameof(StrikeKind.Scream)] = 15f },
        Strikes = new Dictionary<string, StrikeProfileConfig>
        {
            [nameof(StrikeDirection.Overhead)] = NazgulScream(),
            [nameof(StrikeDirection.Left)] = NazgulScream(),
            [nameof(StrikeDirection.Right)] = NazgulScream(),
        },
    };

    private static StrikeProfileConfig SauronSweep() => new StrikeProfileConfig
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

    private static StrikeProfileConfig NazgulScream() => new StrikeProfileConfig
    {
        Kind = nameof(StrikeKind.Scream),
        Origin = nameof(StrikeOrigin.Self),
        OuterRadius = 6f,
        InnerRadius = 2.5f,
        DamageFraction = 0.3f,
        WorldHitBaseDamage = 0,
        Magnitude = 60f,
        KnockDown = false,
        KnockBack = true,
        FearMorale = 25f,
        Sound = "LOTR/Mordor/Nazgul/nazgul_scream",
    };
}

/// <summary>One group of heroes and what their swings do.</summary>
public class SignatureConfig
{
    /// <summary>Names the signature in log lines, and the key a bad field reverts by. Unique,
    /// case-insensitively.</summary>
    public string Id { get; set; } = "";

    /// <summary>Hero StringIds. In a Custom Battle the roster falls back to the character's own
    /// StringId, which for a lord is the same id.</summary>
    public List<string> HeroIds { get; set; } = new List<string>();

    /// <summary>Named compiled hero sets. Only <c>nazgul_nine</c> is known, resolving to
    /// <see cref="NazgulFamily.INazgulRegistry"/>. Unknown names are skipped with a warning.</summary>
    public List<string> HeroSets { get; set; } = new List<string>();

    /// <summary>FaceGen race names. An unknown name is skipped with a warning.</summary>
    public List<string> Races { get; set; } = new List<string>();

    /// <summary>Seconds between two strikes of one kind by the same attacker, in mission time,
    /// keyed by <see cref="StrikeKind"/> name. Every kind a strike row uses needs an entry.</summary>
    public Dictionary<string, float> Cooldowns { get; set; } = new Dictionary<string, float>();

    /// <summary>Per-direction profiles keyed by <see cref="StrikeDirection"/> name. A direction
    /// with no row is a plain hit.</summary>
    public Dictionary<string, StrikeProfileConfig> Strikes { get; set; } = new Dictionary<string, StrikeProfileConfig>();
}

/// <summary>One direction's effect. Radii in metres; the falloff is the engine's boulder curve
/// (full inside <see cref="InnerRadius"/>, one ninth at <see cref="OuterRadius"/>).</summary>
public class StrikeProfileConfig
{
    /// <summary>A <see cref="StrikeKind"/> name. Unknown names drop the row with a warning.</summary>
    public string Kind { get; set; } = nameof(StrikeKind.Slam);

    /// <summary>A <see cref="StrikeOrigin"/> name: <c>Impact</c> centres the ring on the hit
    /// point, <c>Self</c> on the attacker.</summary>
    public string Origin { get; set; } = nameof(StrikeOrigin.Impact);

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

    /// <summary>One-shot morale drain on ring victims and the struck foe, before tier and race
    /// resistance. 0 disables.</summary>
    public float FearMorale { get; set; }

    /// <summary>A <c>module_sounds.xml</c> name played once per strike at the attacker; null or
    /// blank for none.</summary>
    public string? Sound { get; set; }
}
