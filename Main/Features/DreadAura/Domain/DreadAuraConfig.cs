using System.Collections.Generic;

namespace TAOM.Features.DreadAura.Domain;

/// <summary>
/// Deserialization target for <c>dread_aura/dread_aura_config.json</c>.
///
/// Deserialized with <c>ObjectCreationHandling.Replace</c> so a JSON list/dict REPLACES the
/// compiled default instead of Json.NET's append-merge (which would leave every compiled entry
/// in place alongside the author's).
/// </summary>
public class DreadAuraConfig
{
    public bool Enabled { get; set; } = true;

    /// <summary>Seconds between dread pulses for a single source. Sources are staggered, so this is
    /// a per-source cadence, not a global one.</summary>
    public float PulseIntervalSeconds { get; set; } = 0.25f;

    /// <summary>Hard ceiling on how many sources may pulse in one frame. Each
    /// <c>Mission.GetNearbyEnemyAgents</c> call takes a process-wide static lock, so this bounds
    /// the worst-case frame cost regardless of how many wraiths are on the field.</summary>
    public int MaxSourcesPerFrame { get; set; } = 2;

    /// <summary>Per-pulse chance that one affected agent cries out. 0 disables. Degrades to
    /// silence (not an error) for races with no Fear voice clip bound.</summary>
    public float FearVoiceChancePerPulse { get; set; } = 0.02f;

    /// <summary>Named compiled hero sets that project dread. Only <c>nazgul_nine</c> is known,
    /// resolving to <see cref="NazgulFamily.INazgulRegistry"/>. Unknown names are skipped + warned.
    ///
    /// This axis exists because the Nine are NOT identifiable by race: verified 2026-08-13 against
    /// <c>lords.xslt</c>, eight of them carry no <c>race</c> attribute at all (so they inherit
    /// vanilla race 0, human) and Khamûl (<c>lord_1_48</c>) is <c>race="orc"</c>. A race-keyed
    /// source list would emit zero auras for eight of the Nine, and adding <c>orc</c> to catch
    /// Khamûl would hand the aura to every orc in the game.</summary>
    public List<string> HeroSets { get; set; } = new List<string> { "nazgul_nine" };

    /// <summary>Individual hero StringIds that project dread. <c>lord_1_17</c> is Sauron; he is
    /// listed here as well as via <see cref="Races"/> so the feature survives a future data change
    /// that drops his race attribute.</summary>
    public List<string> HeroIds { get; set; } = new List<string> { "lord_1_17" };

    /// <summary>FaceGen race names that project dread. <c>sauron</c> is the only dread-bearer the
    /// race axis can actually find in TAOM's shipped data (see <see cref="HeroSets"/>).</summary>
    public List<string> Races { get; set; } = new List<string> { "sauron" };

    public DreadProfileConfig Profile { get; set; } = new DreadProfileConfig();

    /// <summary>Per-race multiplier on the drain a TARGET takes, keyed by race NAME (precedent:
    /// <c>combat_mechanics_config.json</c> raceModifiers). 1.0 = no resistance, 0.0 = immune.
    /// Keys are validated lazily against <c>IRaceManager.IsValidRaceName</c> because the FaceGen
    /// registry is not populated at provider construction.</summary>
    public Dictionary<string, float> RaceResist { get; set; } = new Dictionary<string, float>
    {
        ["elf"] = 0.4f,
        ["dwarf"] = 0.5f,
    };
}

public class DreadProfileConfig
{
    /// <summary>Outer radius in metres. Drain falls to zero here.</summary>
    public float Radius { get; set; } = 12f;

    /// <summary>Radius within which drain is at full rate. Between this and <see cref="Radius"/>
    /// the rate falls off linearly.</summary>
    public float InnerRadius { get; set; } = 4f;

    /// <summary>Morale drained per second at full rate, BEFORE vanilla's tier/hero resistance
    /// divisor and the per-race resist multiplier.
    ///
    /// The engine regenerates morale at +0.4/sec up to half a troop's initial morale
    /// (<c>CommonAIComponent.OnTickParallel</c>), so an effective rate at or below 0.4 suppresses
    /// a target without ever routing it. That break-even is the resist mechanic, not a defect.</summary>
    public float MoralePerSecond { get; set; } = 5f;

    /// <summary>Morale below which the aura will not push a target. 0 means the aura alone can
    /// drive an agent to the engine's 0.01 panic threshold and rout it, which is the shipped
    /// behaviour. Raise it to make dread soften troops while leaving the kill to combat.</summary>
    public float MoraleFloor { get; set; }
}
