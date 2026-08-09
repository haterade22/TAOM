using Newtonsoft.Json;

namespace TAOM.Features.AutoResolveDiagnostics.Domain;

/// <summary>
/// One troop type, as the ENGINE sees it. Emitted once per session for every CharacterObject.
///
/// This is the ground-truth companion to the battle records. Everything the offline analyzer
/// derives from `troops_*.xml` — tier, simulated power, formation class, hit points — is derived
/// again here by asking the live engine, so the two can be compared. That matters because the
/// offline derivation rests on assumptions that are easy to get quietly wrong:
///
///   * Tier = clamp(ceil((level-5)/5), 0, MaxCharacterTier), and TAOM raises MaxCharacterTier to 10
///     via TaomCharacterStatsModel. If that override ever stops being registered, every offline
///     tier silently caps at 6 and every power number is wrong.
///   * Power comes from TaomMilitaryPowerModel, whose table is MCM- and JSON-configurable. The
///     offline copy of that table is a hardcoded guess about the player's live config.
///   * The counter system will classify troops by formation class and weapon; logging the engine's
///     own DefaultFormationClass lets the offline classifier be validated BEFORE it ships.
///   * MaxHitPoints feeds the removal roll. The claim that every non-hero troop is 100 HP is worth
///     one line of evidence rather than one line of trust.
///
/// Cost: ~829 records once per session, on session launch. Nothing per battle, nothing per strike.
/// </summary>
public sealed class TroopCensusRecord
{
    [JsonProperty("v")] public int Version { get; set; } = 1;

    [JsonProperty("id")] public string? Id { get; set; }
    [JsonProperty("level")] public int Level { get; set; }

    /// <summary>CharacterObject.Tier — the engine's value, through TAOM's CharacterStatsModel.</summary>
    [JsonProperty("tier")] public int Tier { get; set; }

    /// <summary>MilitaryPowerModel.GetDefaultTroopPower — exactly what the simulation scores this
    /// troop at, including TAOM's configurable tier table and mounted multiplier.</summary>
    [JsonProperty("power")] public float Power { get; set; }

    /// <summary>CharacterObject.MaxHitPoints() — the denominator of the removal roll.</summary>
    [JsonProperty("hp")] public int HitPoints { get; set; }

    [JsonProperty("formation")] public string? Formation { get; set; }
    [JsonProperty("mounted")] public bool Mounted { get; set; }
    [JsonProperty("ranged")] public bool Ranged { get; set; }
    [JsonProperty("hero")] public bool IsHero { get; set; }
    [JsonProperty("culture")] public string? Culture { get; set; }

    /// <summary>CharacterObject.Race — the integer the engine actually holds, so the offline race
    /// grouping can be checked against it rather than against the XML attribute alone.</summary>
    [JsonProperty("race")] public int Race { get; set; }
}
