using System.Collections.Generic;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Party;
using TAOM.Adapters;
using TAOM.Features.AutoResolveDiagnostics.Domain;

namespace TAOM.Features.AutoResolveDiagnostics;

/// <summary>
/// Records every completed map battle as one raw data line, so auto-resolve balance can be tuned
/// against battles that actually happened instead of against party templates.
///
/// Why it exists: simulated-battle strength reads ONLY troop tier. Skills, weapons, armour and race
/// are invisible to it. Before changing that we need to know what real mid-campaign armies look
/// like — their composition, size, and how lopsided their outcomes are.
///
/// WHY THERE IS A START SNAPSHOT. Nothing readable at battle end still holds the army as fielded:
/// the engine strips a defeated party's MemberRoster before the event fires, and MapEventParty.Troops
/// is rebuilt from that same stripped roster by the readiness pass. Measured over 4,380 live
/// battles, losing sides came back a median 55% short while winners were 1% — a survivorship bias
/// in the one measurement this feature exists to make. The snapshot also carries every
/// LEADER-DERIVED input (morale, tactics, leader, advantage) for the same reason.
///
/// This type is deliberately thin (ADR-002): event wiring plus the pending-battle map. Everything
/// else — the census, record ids, emit policy — lives in <see cref="IAutoResolveLogWriter"/>. The
/// pending map stays here because it is keyed by the sealed <see cref="MapEvent"/>, which must not
/// cross into a service (ADR-007).
///
/// LATCH DISCIPLINE (rca-tournament-exit-hang-2026-07-06.md). The MCM master switch gates the
/// OPENER as well as the write, so "off" costs nothing rather than merely producing nothing. That
/// is safe because the CLOSER stays unconditional: OnMapEventEnded removes the pending entry in a
/// `finally` whatever the toggle says, so a gated opener can strand nothing. A hard cap and a
/// session-launch clear catch any battle that somehow never ends.
/// </summary>
public class AutoResolveDiagnosticsBehavior : CampaignBehaviorBase
{
    /// <summary>Backstop only. Concurrent map battles number in the low tens; if this is ever hit,
    /// something is leaking and dropping the newest is better than growing without bound.</summary>
    internal const int MaxTrackedBattles = 256;

    private readonly IMapEventBattleLogAdapter _adapter;
    private readonly IAutoResolveLogWriter _writer;
    private readonly IAutoResolveDiagnosticsSettingsProvider _settings;

    private readonly Dictionary<MapEvent, BattleStartSnapshot> _pending = new();

    public AutoResolveDiagnosticsBehavior(
        IMapEventBattleLogAdapter adapter,
        IAutoResolveLogWriter writer,
        IAutoResolveDiagnosticsSettingsProvider settings)
    {
        _adapter = adapter;
        _writer = writer;
        _settings = settings;
    }

    public override void RegisterEvents()
    {
        CampaignEvents.MapEventStarted.AddNonSerializedListener(this, OnMapEventStarted);
        CampaignEvents.OnPartyAddedToMapEventEvent.AddNonSerializedListener(this, OnPartyAddedToMapEvent);
        CampaignEvents.MapEventEnded.AddNonSerializedListener(this, OnMapEventEnded);
        CampaignEvents.OnSessionLaunchedEvent.AddNonSerializedListener(this, _ => OnSessionLaunched());
    }

    /// <summary>A second campaign in the same process must not inherit the first one's state.</summary>
    internal void OnSessionLaunched()
    {
        try
        {
            _pending.Clear();
            _writer.BeginSession();
            _writer.WriteCensus();
        }
        catch
        {
            // A diagnostic must never propagate into session launch — a throw here would take the
            // whole campaign down at the least recoverable moment.
        }
    }

    // No persistence. A diagnostic record has no business in the save file. Records carry
    // Campaign.UniqueGameId, so two campaigns in one process can be told apart during analysis.
    public override void SyncData(IDataStore dataStore) { }

    internal int TrackedBattles => _pending.Count;

    /// <summary>
    /// Whether a battle start is worth snapshotting. Pure so the toggle gate is testable — MapEvent
    /// cannot be constructed outside the game, and the null path returns before any settings read,
    /// so a handler-level assertion could not distinguish "gated" from "never reached".
    ///
    /// Cheap checks precede <paramref name="isEnabled"/> deliberately: the settings read is an MCM
    /// registry lookup (BaseSettingsProvider.GetSettings), not a field read.
    /// </summary>
    internal static bool ShouldCaptureStart(bool isEnabled, bool hasMapEvent, int pendingCount) =>
        hasMapEvent && pendingCount < MaxTrackedBattles && isEnabled;

    internal void OnMapEventStarted(MapEvent mapEvent, PartyBase attacker, PartyBase defender)
    {
        try
        {
            // The toggle gates the SNAPSHOT, not just the write: capturing here costs a roster walk
            // per involved party per battle start, world-wide, and battle count scales with
            // campaign speed.
            if (!ShouldCaptureStart(_settings.IsEnabled, mapEvent != null, _pending.Count))
                return;
            _pending[mapEvent] = _adapter.SnapshotStart(mapEvent);
        }
        catch
        {
            // A diagnostic must never propagate into the campaign tick.
        }
    }

    /// <summary>
    /// A party can join a battle already in progress, which MapEventStarted cannot see
    /// (EnlistmentMaintenanceBehavior.cs:12 documents the same engine gap). Fold the newcomer in so
    /// its men are not missing from the fielded army.
    /// </summary>
    internal void OnPartyAddedToMapEvent(PartyBase party)
    {
        try
        {
            // Gated like its siblings. Without this a toggle flipped off mid-battle left every
            // subsequent reinforcement still doing real work on an already-tracked battle.
            if (!_settings.IsEnabled)
                return;

            var mapEvent = party?.MapEvent;
            if (mapEvent == null || !_pending.TryGetValue(mapEvent, out var snapshot))
                return;

            // Only the joining party. The obvious alternative — re-running SnapshotStart and
            // keeping its rosters — re-derives every party on both sides plus both leaders, morale
            // and advantage, and discards all of it. PartyBase.MapEventSide's setter recurses into
            // MobileParty.AttachedParties, so a reinforcing army raises this event once per
            // attached party and that shape is quadratic in the size of the battle.
            _adapter.SnapshotParty(party, snapshot);
        }
        catch
        {
        }
    }

    internal void OnMapEventEnded(MapEvent mapEvent)
    {
        try
        {
            if (mapEvent == null)
                return;

            // A record with no start snapshot is not a thin record, it is a MISLEADING one: every
            // leader field reads 0, advantage 1.0 and `fielded` empty, which is indistinguishable
            // from a real battle fought by leaderless empty armies. Reachable when the toggle was
            // off at start, when the cap was hit, or when a save was loaded mid-battle. Dropping it
            // costs one outcome row; keeping it silently poisons every composition statistic.
            if (_settings.IsEnabled && _pending.TryGetValue(mapEvent, out var start))
                _writer.Emit(_adapter.Capture(mapEvent, _writer.NextRecordId(), start));
        }
        catch
        {
            // A diagnostic must never propagate into the campaign tick.
        }
        finally
        {
            // Unconditional: a toggle flipped mid-session must never strand a pending entry.
            if (mapEvent != null)
                _pending.Remove(mapEvent);
        }
    }
}
