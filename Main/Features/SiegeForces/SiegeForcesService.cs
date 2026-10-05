using System;
using System.Collections.Generic;
using TAOM.Adapters;
using TAOM.Core.Domain;
using TAOM.Core.Logging;
using TAOM.Features.CoopInterop;
using TAOM.Features.SiegeForces.Domain;

namespace TAOM.Features.SiegeForces;

/// <summary>
/// The siege troop picker (docs/features/siege-forces.md): before a wall battle the player picks who fights on the
/// vanilla "Manage Troops" screen, and the troops he leaves out are never allocated, so no agent spawns for them and
/// they reach no battle bookkeeping. Three things happen on the main thread, in order:
/// <list type="number">
/// <item><see cref="TryOfferPicker"/>, from Patch102's prefix on <c>PlayerSiege.StartSiegeMission</c>: the gates, the
/// snapshot, the screen. Nothing is armed until the player presses Done; Cancel has no callback.</item>
/// <item>On Done the plan is built and ARMED around the vanilla mission open (a try/finally); the model's
/// <see cref="FilterAppended"/> calls, made synchronously while the mission's behaviours are built, drop the left-out
/// entries from each party's part of the ready list. The window closes when the open returns or throws.</item>
/// <item><see cref="FitSpawnTotals"/>, from Patch102's prefix on <c>InitWithSinglePhase</c>: the player side's total and
/// initial spawn shrink by what was left out, once, for the battle the record was made for. Without it deployment
/// waits for reserved troops that never come.</item>
/// </list>
/// State is the armed plan, the per-arming counters and one pending fit, all main-thread only. Nothing is persisted and
/// nothing is per campaign: a pending fit names its map event by reference, the next <c>InitWithSinglePhase</c> consumes
/// it, a battle of another map event discards it, and every offer drops one that is still waiting (its mission never
/// reached the siege spawn handler's AfterStart). The session-reset story is that no battle of a new session can match
/// a stale record. The settings switch gates only the offer, never a state transition: a Done after the player switched
/// it off still applies his selection.
/// </summary>
public sealed class SiegeForcesService
{
    private const string Tag = "[SiegeForces]";
    private const int DefenderSide = 0;
    private const int AttackerSide = 1;

    private readonly ISiegeForcesAdapter _adapter;
    private readonly ISiegeForcesConfigProvider _config;
    private readonly ISiegeForcesSettingsProvider _settings;
    private readonly IRaceManager _raceManager;
    private readonly ICoopSessionProvider _coop;
    private readonly IDedicatedServerProvider _server;
    private readonly IModLogger _logger;

    // The armed window. Set around the vanilla mission open and cleared in a finally.
    private SiegeForcesPlan? _activePlan;
    private readonly Dictionary<string, int> _droppedByParty = new(StringComparer.Ordinal);
    private readonly Dictionary<int, int> _readyCountBySide = new();
    private int _filteredPlanParties;

    // Consumed by the next InitWithSinglePhase of any battle; applied only on a battle and side match.
    private PendingFit? _pending;

    // Whether Patch102's fit prefix is attached to InitWithSinglePhase, asked once per process at the first offer.
    private bool? _fitAttached;

    public SiegeForcesService(
        ISiegeForcesAdapter adapter,
        ISiegeForcesConfigProvider config,
        ISiegeForcesSettingsProvider settings,
        IRaceManager raceManager,
        ICoopSessionProvider coop,
        IDedicatedServerProvider server,
        IModLogger logger)
    {
        _adapter = adapter;
        _config = config;
        _settings = settings;
        _raceManager = raceManager;
        _coop = coop;
        _server = server;
        _logger = logger;
    }

    /// <summary>
    /// True when a fit is waiting for the next battle. A pure read of this service's own state: the totals prefix asks it
    /// first, so with nothing pending no engine state is read at all (Custom Battle and every field battle).
    /// </summary>
    public bool HasPending => _pending != null;

    /// <summary>
    /// Offers the picker before a wall battle. True means the screen opened and the caller must skip vanilla's mission
    /// open; <paramref name="openVanillaMission"/> is then called on Done, inside the armed window. False means vanilla
    /// runs: the setting is off, a co-op session or a dedicated server is in play, this is no wall battle, there is
    /// nothing to choose, the screen did not open, or anything failed. The co-op and server answers are read live.
    /// Whatever the answer, a fit an earlier assault left pending is dropped first.
    /// </summary>
    public bool TryOfferPicker(Action openVanillaMission)
    {
        if (openVanillaMission == null) throw new ArgumentNullException(nameof(openVanillaMission));

        try
        {
            // Every assault passes here before vanilla opens its mission, so a fit nothing consumed (its mission never
            // reached the siege spawn handler's AfterStart) ends here: a siege that continues keeps its map event, and
            // the record's token would match the next battle. First, ahead of every gate: a state transition never sits
            // behind the settings switch. Done's re-entry never gets here (Patch102's latch returns first).
            _pending = null;

            if (!_settings.PickerEnabled) return false;
            if (_coop.IsSessionActive || _coop.ShouldDeferToHost || _server.IsDedicatedServer) return false;
            if (!FitIsAttached()) return false;

            var snapshot = _adapter.CaptureWallBattle();
            if (snapshot == null) return false;

            var request = SiegeForcesRules.BuildRequest(snapshot, _config.StartOversizedUnticked, _raceManager);
            if (request == null) return false;

            var done = false;
            void OnDone(IReadOnlyDictionary<string, int> selected)
            {
                if (done) return;
                done = true;
                Done(snapshot, selected, openVanillaMission);
            }

            // Logged before the screen opens, and nothing runs after it: once the screen is up, a fault here must not
            // turn "opened" into "false" and let vanilla open the mission underneath it.
            _logger.LogInfo($"{Tag} offering the picker: {SiegeForcesRules.PartiesInScope(snapshot).Count} party(ies), " +
                            $"{request.Rows.Count} row(s), up to {request.Max} troop(s)");
            return _adapter.TryOpenPicker(request, OnDone);
        }
        catch (Exception ex)
        {
            _logger.LogWarning($"{Tag} picker not offered ({ex.GetType().Name}: {ex.Message}); the vanilla mission opens");
            return false;
        }
    }

    // No fit, no picker: a selection that leaves troops out would stall deployment. Patches apply once, at game init,
    // so the answer is asked once. A fault answers "not attached" and is cached with it.
    private bool FitIsAttached()
    {
        if (_fitAttached is bool known) return known;

        bool attached;
        try
        {
            attached = _adapter.IsSpawnTotalsFitAttached();
        }
        catch (Exception ex)
        {
            attached = false;
            _logger.LogWarning($"{Tag} the spawn fit could not be checked ({ex.GetType().Name}: {ex.Message})");
        }

        _fitAttached = attached;
        if (!attached)
            _logger.LogWarning($"{Tag} the spawn totals fit is not attached to InitWithSinglePhase, so the picker is not offered");
        return attached;
    }

    /// <summary>
    /// Called by the model after the base model appended one party's troops to a side's shared ready list. Does nothing
    /// unless a plan is armed and <paramref name="includePlayers"/> is true (simulations pass false and are never
    /// filtered). The window is built here, inside the try, from a factory: an adapter fault before any removal means no
    /// filtering and no record. A fault part way through the removals leaves the party partly filtered, with the planned
    /// drop count already recorded (see <see cref="DropLeftOut"/>).
    /// </summary>
    public void FilterAppended(bool includePlayers, Func<IReadyListWindow> windowFactory)
    {
        var plan = _activePlan;
        if (plan == null || !includePlayers) return;

        try
        {
            var window = windowFactory();
            if (window == null) return;

            if (plan.TryGetKeep(window.PartyId, out _))
            {
                _filteredPlanParties++;
                DropLeftOut(plan, window);
            }

            // The side's list is shared by every party of the side and written again by each call, so the last write is
            // the list's final size. Another party of the side that is not in the plan appends too, so it records as well.
            if (window.Side == DefenderSide || window.Side == AttackerSide)
                _readyCountBySide[window.Side] = window.ListCount;
        }
        catch (Exception ex)
        {
            _logger.LogWarning($"{Tag} a ready list could not be filtered ({ex.GetType().Name}: {ex.Message}); " +
                               "that party may be only partly filtered");
        }
    }

    /// <summary>
    /// Fits the player side's spawn totals to the troops left out, for the battle the pending record was made for, and
    /// consumes the record. Null leaves the four totals alone: nothing pending (no engine read at all), another battle,
    /// the other side, no map event (Custom Battle), or a fault. A record that does not match is dropped, so the next
    /// battle's call is a pure state read again.
    /// </summary>
    public SpawnTotals? FitSpawnTotals(bool playerIsAttacker, SpawnTotals totals)
    {
        var pending = _pending;
        if (pending == null) return null;
        _pending = null;

        string side;
        int before, initialBefore, total, initial;
        try
        {
            var token = _adapter.ReadMapEventToken();
            if (token == null || !ReferenceEquals(token, pending.Token) || playerIsAttacker != pending.PlayerIsAttacker)
            {
                _logger.LogDebug($"{Tag} dropped a pending fit that belongs to another battle");
                return null;
            }

            side = playerIsAttacker ? "attacker" : "defender";
            before = playerIsAttacker ? totals.AttackerTotal : totals.DefenderTotal;
            initialBefore = playerIsAttacker ? totals.AttackerInitial : totals.DefenderInitial;
            (total, initial) = SiegeForcesRules.FitTotals(before, initialBefore, pending.Dropped, pending.ReadyCount);
        }
        catch (Exception ex)
        {
            _logger.LogWarning($"{Tag} the spawn totals were left as they are ({ex.GetType().Name}: {ex.Message})");
            return null;
        }

        // Logged apart from the arithmetic: a logger fault must not discard a fit that is already computed.
        try
        {
            _logger.LogInfo($"{Tag} fit: {side} total {before} -> {total}, " +
                            $"initial {initialBefore} -> {initial} (left out {pending.Dropped}, ready list {pending.ReadyCount?.ToString() ?? "unknown"})");
        }
        catch (Exception)
        {
            // The fit stands; there is nothing left to report to.
        }

        return playerIsAttacker
            ? totals with { AttackerTotal = total, AttackerInitial = initial }
            : totals with { DefenderTotal = total, DefenderInitial = initial };
    }

    private void Done(SiegeForcesSnapshot snapshot, IReadOnlyDictionary<string, int> selected, Action openVanillaMission)
    {
        SiegeForcesPlan? plan = null;
        try
        {
            plan = SiegeForcesRules.BuildPlan(snapshot, selected);
        }
        catch (Exception ex)
        {
            _logger.LogWarning($"{Tag} the plan could not be built ({ex.GetType().Name}: {ex.Message}); opening the vanilla mission");
        }

        if (plan == null)
        {
            openVanillaMission();
            return;
        }

        Arm(plan);
        var opened = false;
        try
        {
            openVanillaMission();
            opened = true;
        }
        finally
        {
            Disarm(snapshot, opened);
        }
    }

    // Every arming starts from fresh counters, wherever the last one ended: this is the only place they are cleared.
    private void Arm(SiegeForcesPlan plan)
    {
        _droppedByParty.Clear();
        _readyCountBySide.Clear();
        _filteredPlanParties = 0;
        _pending = null;
        _activePlan = plan;
    }

    // Runs in a finally: it must never throw, or it would replace the fault that is in flight.
    private void Disarm(SiegeForcesSnapshot snapshot, bool opened)
    {
        _activePlan = null;
        try
        {
            var dropped = 0;
            foreach (var count in _droppedByParty.Values)
                dropped += count;
            int? readyCount = _readyCountBySide.TryGetValue(snapshot.PlayerIsAttacker ? AttackerSide : DefenderSide, out var ready)
                ? ready
                : (int?)null;
            var filtered = _filteredPlanParties;

            // Nothing dropped means nothing to fit. A throw out of the open may come after the spawn logic ran or before
            // it: the record is kept either way, because the token guard and the next offer's reset neutralise a stale one.
            _pending = dropped > 0
                ? new PendingFit(snapshot.MapEventToken, snapshot.PlayerIsAttacker, dropped, readyCount)
                : null;

            if (opened && filtered == 0)
                _logger.LogWarning($"{Tag} the mission opened without the troop supplier model filtering the player's parties " +
                                   "(another mod may own that model slot), so every troop will fight");
        }
        catch (Exception ex)
        {
            _pending = null;
            try
            {
                _logger.LogWarning($"{Tag} closing the picker's window failed ({ex.GetType().Name}: {ex.Message})");
            }
            catch (Exception)
            {
                // Nothing left to report to; the window is closed and nothing is pending.
            }
        }
    }

    // Decides over the whole appended window first, from its END: the engine appends each party's troops lowest priority
    // first (DefaultTroopSupplierProbabilityModel, ascending key within a bucket) and MapEventSide.MakeReady sorts the
    // side's list highest priority first, so the entries kept per character are the last K, the ones that would have
    // spawned first. Records the count BEFORE touching the engine's list, then removes in DESCENDING index order (the
    // order the scan collects them in) so each removal leaves the earlier indices where they were. A throw mid-removal
    // can therefore only over-subtract from the total: fewer troops spawn than the list could supply, and the battle
    // still completes.
    private void DropLeftOut(SiegeForcesPlan plan, IReadyListWindow window)
    {
        var kept = new Dictionary<string, int>(StringComparer.Ordinal);
        List<int>? drop = null;
        for (var i = window.Count - 1; i >= 0; i--)
        {
            var id = window.CharacterIdAt(i);
            var keptSoFar = 0;
            if (id != null) kept.TryGetValue(id, out keptSoFar);

            if (SiegeForcesRules.Keeps(plan, window.PartyId, id, window.IsPlayerCharacterAt(i), keptSoFar))
            {
                if (id != null) kept[id] = keptSoFar + 1;
            }
            else
            {
                (drop ??= new List<int>()).Add(i);
            }
        }

        _droppedByParty[window.PartyId] = drop?.Count ?? 0;
        if (drop == null) return;

        foreach (var index in drop)
            window.RemoveAt(index);
    }

    private sealed record PendingFit(object Token, bool PlayerIsAttacker, int Dropped, int? ReadyCount);
}
