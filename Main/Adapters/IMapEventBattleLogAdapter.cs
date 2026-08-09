using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Party;
using TAOM.Features.AutoResolveDiagnostics.Domain;

namespace TAOM.Adapters;

/// <summary>
/// Converts a live <see cref="MapEvent"/> into engine-free battle data at the boundary, so no
/// sealed TaleWorlds type crosses into a service (ADR-007).
/// </summary>
public interface IMapEventBattleLogAdapter
{
    /// <summary>
    /// Captures every simulation INPUT at battle start: each party's roster, and per side the
    /// leader, tactics, power modifier, morale and battle advantage.
    ///
    /// All of it has to be read here. The engine strips a defeated party's roster, zeroes its
    /// morale and removes its leader as part of losing, so anything read at battle end describes
    /// the outcome rather than the inputs.
    /// </summary>
    BattleStartSnapshot SnapshotStart(MapEvent mapEvent);

    /// <summary>
    /// Folds ONE late-joining party into an existing snapshot: its roster, plus a context modifier
    /// for any troop class the battle had not seen before it arrived.
    ///
    /// Exists because the obvious implementation — re-running <see cref="SnapshotStart"/> and
    /// keeping only its rosters — re-derives every party on both sides, both leaders, morale and
    /// battle advantage, then throws all of that away. The engine's <c>PartyBase.MapEventSide</c>
    /// setter recurses into <c>MobileParty.AttachedParties</c>, so a reinforcing army of N parties
    /// raises this event N times and that shape is quadratic in the size of the battle.
    ///
    /// The per-side leader inputs are deliberately NOT revisited: a party joining mid-fight does
    /// not retroactively change the morale or advantage the simulation has already been applying.
    /// </summary>
    void SnapshotParty(PartyBase party, BattleStartSnapshot snapshot);

    /// <summary>
    /// Captures the completed battle, folding in the start snapshot. Casualties are read at end —
    /// the engine's per-troop casualty rosters accumulate and are never cleared.
    ///
    /// Returns null if the event cannot be read. Never throws — a diagnostic must not propagate
    /// into the campaign tick.
    /// </summary>
    BattleLogRecord? Capture(MapEvent mapEvent, string id, BattleStartSnapshot? start);
}
