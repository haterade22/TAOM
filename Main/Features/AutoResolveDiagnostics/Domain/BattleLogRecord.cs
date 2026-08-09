using System.Collections.Generic;
using Newtonsoft.Json;

namespace TAOM.Features.AutoResolveDiagnostics.Domain;

/// <summary>
/// One completed map battle, captured as flat data with no engine types.
///
/// DELIBERATELY RAW. No troop class, no race group, no tier, no computed power — only troop ids and
/// counts. Everything derivable is derived offline by tools/analyze_battle_logs.py against
/// troops_*.xml, so the analysis can change without a rebuild and without a second play session.
///
/// SCHEMA v6 — the fielded army AND every leader-derived input come from a START-OF-BATTLE
/// snapshot; casualties come from the engine's per-troop casualty rosters, which do survive.
///
/// v5 fixed the rosters but still read morale, tactics, leader and powerModifier at battle end.
/// Measured over 5,546 battles: losing sides recorded sideMorale == 0 in 5,543 cases and a
/// resolvable leader in 17 (0%, against 74% for winners), because the engine zeroes morale and
/// removes the leader as part of losing. Those four fields described the outcome, not the input.
/// v6 captures them at start with the rosters.
///
/// v1 read Party.MemberRoster at battle end and reconstructed the fielded army from casualties.
/// That was wrong in a way that biased the result rather than breaking it: the engine strips
/// captured troops out of a DEFEATED party's MemberRoster (CaptureDefeatedPartyMembers,
/// MapEvent.cs:2018) and empties it again on a rout (MapEventSide.Route, :1250) — both BEFORE
/// FinishBattle dispatches OnMapEventEnded. So the loser's roster was near-empty and every
/// composition measurement was taken on winners only, in the one measurement this tool exists for.
///
/// v3/v4 tried MapEventParty.Troops instead, on the theory that it only flips per-descriptor state.
/// It does — but MapEventSide.MakeReadyParty calls MapEventParty.Update(), which does
/// _roster.Clear() and rebuilds from the already-stripped MemberRoster. Measured over 4,380 live
/// battles: losing sides read a median 55% short, winners 1%. Same survivorship bias, second
/// mechanism. v5 goes back to the snapshot, which is the only thing that holds the army as fielded.
/// </summary>
public sealed class BattleLogRecord
{
    /// <summary>Schema version. Bump when a field's MEANING changes; the analyzer refuses to
    /// analyse a version it does not understand rather than producing quiet nonsense.</summary>
    [JsonProperty("v")] public int Version { get; set; } = 6;

    /// <summary>
    /// MapEvent.IsPlayerSimulation — true when the PLAYER's battle was auto-resolved rather than
    /// fought in a mission. AI-vs-AI battles are always simulated, so the analyzer derives
    /// "was this simulated" as `!player || playerSimulated`. Without it, player battles that were
    /// actually fought pollute a dataset about the simulation.
    /// </summary>
    [JsonProperty("playerSimulated")] public bool PlayerSimulated { get; set; }

    /// <summary>Campaign.UniqueGameId. Without it, two campaigns — or a run before and after a
    /// balance-config change — pool silently into one dataset across the 10 rotated log files.</summary>
    [JsonProperty("session")] public string? Session { get; set; }

    [JsonProperty("id")] public string? Id { get; set; }
    [JsonProperty("day")] public int Day { get; set; }
    [JsonProperty("hour")] public float Hour { get; set; }

    /// <summary>MapEvent.BattleTypes — field, siege, raid, hideout, sally out. Different mechanics,
    /// so the analyzer segments on it rather than pooling them.</summary>
    [JsonProperty("type")] public string? Type { get; set; }

    [JsonProperty("settlement")] public string? Settlement { get; set; }

    /// <summary>
    /// MapEvent.SimulationContext. Vanilla's DefaultMilitaryPowerModel already grants
    /// type-vs-terrain power modifiers keyed on this, so without it a class-loss skew in the data
    /// cannot be separated from a counter effect.
    /// </summary>
    [JsonProperty("terrain")] public string? Terrain { get; set; }

    /// <summary>Player-involved battles use a different blunt-damage chance and difficulty
    /// multipliers, and may have been fought rather than simulated. Must be filterable.</summary>
    [JsonProperty("player")] public bool PlayerInvolved { get; set; }

    /// <summary>MapEvent.UpdateCount — simulation rounds fought. How decisive the battle was.</summary>
    [JsonProperty("rounds")] public int Rounds { get; set; }

    [JsonProperty("winner")] public string? Winner { get; set; }
    [JsonProperty("endedBy")] public string? EndedBy { get; set; }

    /// <summary>Present only for siege-family events. Null for field battles.</summary>
    [JsonProperty("siege")] public BattleLogSiege? Siege { get; set; }

    [JsonProperty("sides")] public Dictionary<string, BattleLogSide> Sides { get; set; } = new();
}

/// <summary>
/// The siege-specific terms, which dominate a siege the way nothing dominates a field battle.
///
/// `DefaultCombatSimulationModel.GetSettlementAdvantage` is roughly `(1 + 4 + wallLevel − 1)` over
/// a divisor that only siege engines reduce — so an unbreached wall-3 town hands the defender a
/// ~7× multiplier, falling to ~3.5× once the attacker has built rams, towers and artillery. That
/// single number swamps every troop-quality term in the simulation, and without it a siege outcome
/// cannot be explained at all. First live sample: attackers brought a median 3.4× the men and still
/// won only 73% — consistent with a multiplier of that size, but not demonstrable without logging it.
///
/// Also note the siege branch of GetSimulationTicksForBattleRound is a different formula entirely,
/// and a siege DEFENDER never routs (MapEventSide.OnTroopRouted gates on
/// `EventType != Siege || MissionSide == Attacker`), so `routed` is structurally empty for them.
/// </summary>
public sealed class BattleLogSiege
{
    /// <summary>GetSettlementAdvantage — the defender's multiplier. The dominant term.</summary>
    [JsonProperty("settlementAdvantage")] public float SettlementAdvantage { get; set; }

    /// <summary>Town.GetWallLevel(). Drives the numerator of the advantage.</summary>
    [JsonProperty("wallLevel")] public int WallLevel { get; set; }

    /// <summary>Settlement.SettlementTotalWallHitPoints. At ~0 the wall is breached and the
    /// advantage is quartered — the difference between a hopeless assault and a winnable one.</summary>
    [JsonProperty("wallHitPoints")] public float WallHitPoints { get; set; }

    /// <summary>GetNumberOfEquipmentsBuilt — completed attacker siege engines, the only lever that
    /// reduces the defender's multiplier.</summary>
    [JsonProperty("enginesBuilt")] public int EnginesBuilt { get; set; }

    /// <summary>GetMaximumSiegeEquipmentProgress — how far the next engine has got.</summary>
    [JsonProperty("engineProgress")] public float EngineProgress { get; set; }

    /// <summary>Which side owned the settlement, so attacker/defender can be read correctly.</summary>
    [JsonProperty("settlementOwner")] public string? SettlementOwner { get; set; }
}

/// <summary>One side of a battle: leader metadata plus a per-party breakdown.</summary>
public sealed class BattleLogSide
{
    /// <summary>The LEADER's culture. Not the side's — a side can hold parties of several cultures,
    /// which is why composition is reported per party, never from this field.</summary>
    [JsonProperty("leaderCulture")] public string? LeaderCulture { get; set; }

    [JsonProperty("kingdom")] public string? Kingdom { get; set; }
    [JsonProperty("leader")] public string? Leader { get; set; }

    /// <summary>Leader Tactics AS AT BATTLE START. Feeds GetBattleAdvantage at +0.1% per point.</summary>
    [JsonProperty("tactics")] public int Tactics { get; set; }

    /// <summary>GetPowerModifierOfHero — the leader's captain-role perks, +1% to +6% power each.</summary>
    [JsonProperty("powerModifier")] public float PowerModifier { get; set; }

    /// <summary>MapEventSide.GetSideMorale() AS AT BATTLE START — strength-weighted across the
    /// side's mobile parties, with the siege-defender clamp applied. This is what the simulation
    /// reads; a single party's Morale is not, and would be wrong for any stacked army. Captured at
    /// start because a losing side's morale is zero by the time the battle ends, every time.</summary>
    [JsonProperty("sideMorale")] public float SideMorale { get; set; }

    /// <summary>Sum of HealthyManCountAtStart across the side — men PRESENT. Compare against the
    /// summed per-party `participating` counts, not against `fielded`: when a troop limit applies
    /// the engine trims the allocated roster, so present and participating legitimately differ.
    /// (The first live run showed a −23.6% median gap for exactly this reason.)</summary>
    [JsonProperty("menStart")] public int MenStart { get; set; }

    /// <summary>
    /// MapEvent.StrengthOfSide — the engine's OWN power figure for this side.
    ///
    /// The single most valuable field here: it is ground truth for the power model. The analyzer
    /// recomputes strength offline from troop ids and compares, so a drift between what we think
    /// the simulation values and what it actually values shows up as a number instead of as a
    /// silently mistuned config.
    /// </summary>
    [JsonProperty("strength")] public float Strength { get; set; }

    /// <summary>CombatSimulationModel.GetBattleAdvantage for this side — the multiplier actually
    /// applied to every strike, folding in Tactics, perks and the siege-attacker penalty. Tactics
    /// alone does not reconstruct it.</summary>
    [JsonProperty("advantage")] public float Advantage { get; set; }

    /// <summary>
    /// MilitaryPowerModel.GetContextModifier for each troop class present on this side, keyed
    /// "Infantry" / "Archer" / "Cavalry" / "HorseArcher".
    ///
    /// The engine's real per-troop power is
    ///     GetDefaultTroopPower(troop) * (1 + leaderModifier + contextModifier)
    /// and this is the term the offline model was missing. It is a terrain x class x side lookup
    /// spanning -0.50 to +0.30 — a 0.50x to 1.30x swing on a troop's power — which is why the
    /// offline replay diverged from logged outcomes by +33 percentage points in the 0.67-1.00
    /// power band, precisely the contested region any balance tuning would target.
    ///
    /// Logged rather than hardcoded because TAOM may one day override GetContextModifier, and a
    /// hardcoded copy of vanilla's table would then be silently wrong.
    /// </summary>
    [JsonProperty("contextModifier")] public Dictionary<string, float> ContextModifier { get; set; } = new();

    [JsonProperty("parties")] public List<BattleLogParty> Parties { get; set; } = new();
}

/// <summary>One party within a side, carrying its OWN culture. Every count below comes from one
/// pass over MapEventParty.Troops, so fielded == killed + wounded + routed + survived by
/// construction.</summary>
public sealed class BattleLogParty
{
    [JsonProperty("culture")] public string? Culture { get; set; }

    /// <summary>MapEventParty.HealthyManCountAtStart — men PRESENT in this party.</summary>
    [JsonProperty("present")] public int Present { get; set; }

    /// <summary>MapEventParty.ParticipatingTroopCount — men the engine actually allocated. Differs
    /// from Present when a troop limit applies; -1 when the engine never set it.</summary>
    [JsonProperty("participating")] public int Participating { get; set; }

    /// <summary>MapEventParty.HasTroopLimit — true when the allocated roster was trimmed, which is
    /// the legitimate reason `fielded` can fall below `present`.</summary>
    [JsonProperty("troopLimit")] public bool TroopLimit { get; set; }

    /// <summary>Every man allocated to this party for the battle, troop id → count.</summary>
    [JsonProperty("fielded")] public Dictionary<string, int> Fielded { get; set; } = new();

    [JsonProperty("killed")] public Dictionary<string, int> Killed { get; set; } = new();

    /// <summary>Recoverable. The axis the cultural survival bonuses act on (Mordor −0.20,
    /// Lothlórien +0.50), so it is kept separate from killed rather than summed into casualties.</summary>
    [JsonProperty("wounded")] public Dictionary<string, int> Wounded { get; set; } = new();

    /// <summary>Structurally always empty for a SIEGE DEFENDER — MapEventSide.OnTroopRouted
    /// (:682) gates on `EventType != Siege || MissionSide == Attacker`. Not a capture bug.</summary>
    [JsonProperty("routed")] public Dictionary<string, int> Routed { get; set; } = new();
}
