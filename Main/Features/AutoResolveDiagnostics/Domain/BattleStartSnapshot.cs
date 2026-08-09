using System.Collections.Generic;

namespace TAOM.Features.AutoResolveDiagnostics.Domain;

/// <summary>
/// Everything about a battle that must be read BEFORE it resolves, captured at MapEventStarted.
///
/// Engine-free by construction so it can be held by a CampaignBehavior without a sealed TaleWorlds
/// type crossing a service boundary (ADR-007).
///
/// Why this exists at all: measured over 5,546 logged battles, reading these at MapEventEnded gave
///   losing sides sideMorale == 0 in 5,543 of 5,548 cases
///   losing sides a resolvable leader in 17 of 5,546 (0%), against 74% for winners
/// because the engine removes the defeated side's leader and zeroes its morale as part of losing.
/// Every one of those fields is an INPUT to the simulation, so capturing them afterwards records
/// the consequence and calls it the cause.
/// </summary>
public sealed class BattleStartSnapshot
{
    /// <summary>Party id -> troop id -> count, as fielded at battle start.</summary>
    public Dictionary<string, Dictionary<string, int>> Rosters { get; } = new();

    public BattleStartSide Attacker { get; set; } = new();
    public BattleStartSide Defender { get; set; } = new();
}

/// <summary>The per-side inputs the simulation reads, as they stood before the battle.</summary>
public sealed class BattleStartSide
{
    public string? LeaderCulture { get; set; }
    public string? Kingdom { get; set; }
    public string? Leader { get; set; }

    /// <summary>Feeds GetBattleAdvantage at +0.1% simulated advantage per point.</summary>
    public int Tactics { get; set; }

    /// <summary>GetPowerModifierOfHero — the leader's captain-role perks.</summary>
    public float PowerModifier { get; set; }

    /// <summary>MapEventSide.GetSideMorale() — strength-weighted, siege-defender clamp applied.
    /// Below 30 the whole side's power drops to x0.7, and 0 is an automatic rout.</summary>
    public float SideMorale { get; set; }

    /// <summary>CombatSimulationModel.GetBattleAdvantage for this side — the multiplier applied to
    /// every strike, folding in Tactics, perks and the siege-attacker penalty.</summary>
    public float Advantage { get; set; } = 1f;

    /// <summary>GetContextModifier per troop class present on this side — the terrain x class x
    /// side term that the offline power model was missing.</summary>
    public Dictionary<string, float> ContextModifier { get; } = new();
}
