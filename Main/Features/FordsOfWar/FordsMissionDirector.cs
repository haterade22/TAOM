using System;
using System.Collections.Generic;
using System.Linq;
using BehaviorTreeWrapper;
using TAOM.Adapters;
using TAOM.Core.Logging;
using TAOM.Features.AdvancedCombat;
using TAOM.Features.FordsOfWar.Diagnostics;
using TAOM.Features.FordsOfWar.Domain;

namespace TAOM.Features.FordsOfWar;

/// <summary>
/// The Fords of War match inside one mission (docs/features/fords-of-war/build-spec.md section 4): builds the layout, the
/// towers, the garrisons and the <see cref="FordsMatch"/>, then each tick measures the field through the adapters, runs the
/// match and applies what it decided (spawns within the per-tick budget, a lane order only when it changed, tower volleys,
/// regenerated hit points, the winner). It issues every movement order for both sides and never hands a lane formation to AI
/// control. Owned by the mission logic; it names no engine type (ADR-007).
///
/// Threads. The match is not thread safe, so every call into it takes <c>_gate</c>, and the gate is never held across an
/// adapter call, so the tower hit hook (which can arrive on an engine thread, see <see cref="IFordsTowerHitHook"/>) can never
/// wait on a main-thread engine call that waits on it. An agent removal can also arrive off the main thread:
/// it goes through <see cref="DeferredCallbackQueue.RunOrDefer"/> and lands at the start of the next tick. A tower's fall is reported from
/// inside the engine's hit callback, so it is always parked for the next tick, whichever thread it came on.
/// </summary>
internal sealed class FordsMissionDirector : IFordsTowerHitHook
{
    internal const int HumanSlotId = 1;
    internal const int AiSlotId = 2;

    private const float CorpseFadeSeconds = 4f;
    private const float InsetFraction = 0.1f;
    private const float CameraHeight = 45f;
    private const float CameraSetback = 25f;
    private const string RemovedSite = "FordsMissionDirector.OnAgentRemoved";

    private sealed class PendingSpawn
    {
        public PendingSpawn(SpawnRequest request) => Request = request;

        public SpawnRequest Request { get; }
        public int Remaining { get; set; }
    }

    private readonly FordsMatchPlan _plan;
    private readonly IFordsBattlefieldAdapter _battlefield;
    private readonly IFordsTowerAdapter _towers;
    private readonly IFordsSceneAdapter _scene;
    private readonly FordsSession _session;
    private readonly IModLogger _logger;
    private readonly IFordsRandom _random;
    private readonly DeferredCallbackQueue _deferred;
    private readonly FordsRunReport _report;

    private readonly object _gate = new();
    private readonly Dictionary<AgentKey, UnitRecord> _units = new();
    private readonly Dictionary<(MatchSide, int), LaneCommand> _applied = new();
    private readonly Dictionary<(MatchSide, int), LaneCommand> _refused = new();
    private readonly Dictionary<(MatchSide, int), int> _ranks = new();
    private readonly List<PendingSpawn> _backlog = new();
    private readonly HashSet<string> _fallen = new();
    private readonly HashSet<string> _unbuildable = new();

    private FordsMatch? _match;
    private MapLayout _layout = new("", new List<LaneLayout>(), new List<TowerSlot>(), new List<CapturePointSlot>(), new List<CampSlot>());
    private MatchSide? _winner;
    private FordsMatchFacts? _facts;
    private double _probeAt;
    private int _probes;
    private bool _liveStamped;
    private volatile bool _started;
    private volatile bool _shutDown;

    public FordsMissionDirector(FordsMatchPlan plan, IFordsBattlefieldAdapter battlefield, IFordsTowerAdapter towers,
        IFordsSceneAdapter scene, FordsSession session, IModLogger logger, IFordsRandom random, FordsRunReport report)
    {
        _plan = plan;
        _battlefield = battlefield;
        _towers = towers;
        _scene = scene;
        _session = session;
        _logger = logger;
        _random = random;
        _report = report;
        _deferred = new DeferredCallbackQueue(message => _logger.LogWarning(message));
    }

    /// <summary>The side that destroyed the other's keep; null while the match runs. Set by the tick that learns of it.</summary>
    public MatchSide? Winner
    {
        get
        {
            lock (_gate)
                return _winner;
        }
    }

    /// <summary>
    /// Builds the layout, the towers and the match, and fills the session. False, with an error logged and nothing left
    /// running, when the scene cannot host a match. Call once, from the mission's <c>AfterStart</c>.
    /// </summary>
    public bool Start()
    {
        if (_started)
            return true;

        var layout = BuildLayout();
        if (layout == null)
            return false;

        var expected = layout.Towers.Count;
        var fit = _scene.Fit(layout);
        layout = fit.Layout;
        if (fit.Moved + fit.Dropped + fit.Unfixed > 0)
            _logger.LogInfo($"[FordsOfWar] layout fitted to the scene: {fit.Moved} point(s) moved onto the navmesh, {fit.Dropped} dropped, {fit.Unfixed} left on bad ground");

        if (!_battlefield.Prepare(layout.Lanes.Count))
        {
            _logger.LogError("[FordsOfWar] the mission has no West and East teams (or too many lanes); the match cannot start");
            return false;
        }

        var built = BuildTowers(layout);
        if (built == null)
            return false;

        layout = layout with { Towers = built };
        _battlefield.SetCorpseFadeOutSeconds(CorpseFadeSeconds);
        var match = CreateMatch(layout);
        lock (_gate)
        {
            _layout = layout;
            _match = match;
        }

        ReportMatchStart(layout, built, expected);

        _towers.SetDestroyedHandler(OnTowerDestroyed);
        SpawnGunners(built);
        _battlefield.CompleteDeployment();
        FillSession(layout, match);
        _started = true;
        _logger.LogInfo($"[FordsOfWar] match started on {layout.SceneName}: {layout.Lanes.Count} lane(s), {built.Count} tower(s) and keep(s), AI profile {_plan.AiProfile}");
        _report.Stage("matchStarted");
        return true;
    }

    /// <summary>One mission tick. Main thread. Does nothing before <see cref="Start"/> and after the winner is known.</summary>
    public void Tick(float dt)
    {
        MissionThreadGuard.MarkMainThread();
        _deferred.Drain();
        var match = _match;
        if (!_started || _shutDown || match == null || _winner != null)
            return;

        var samples = _battlefield.SampleUnits();
        MatchEffects effects;
        MatchPhase phase;
        lock (_gate)
        {
            var snapshot = FordsSnapshotBuilder.Build(dt, samples, IsHero, StandingTowers(), _layout.CapturePoints,
                _plan.Config.Towers, _plan.Config.Capture);
            effects = match.Tick(snapshot);
            phase = match.Phase;
        }

        if (!_liveStamped && phase == MatchPhase.Live)
        {
            _liveStamped = true;
            _report.Stage("live");
        }

        ApplyEffects(effects);
        Probe(match, samples);
    }

    /// <summary>The engine removed an agent (killed, knocked out, routed or deleted). May run off the main thread.</summary>
    public void OnAgentRemoved(object affected, object? affector, bool killed)
    {
        if (!_started || !_battlefield.TryRemoveUnit(affected, affector, killed, out var victim, out var killer))
            return;

        _deferred.RunOrDefer(RemovedSite, () =>
        {
            UnitRecord? record;
            KillCredit? credit;
            lock (_gate)
            {
                _units.TryGetValue(victim, out record);
                _units.Remove(victim);
                credit = _match?.OnUnitKilled(victim, killer);
            }

            if (record != null && credit is { } paid)
                _report.Death(record.Side, paid);
        });
    }

    public int AdjustTowerDamage(object component, object? attacker, bool ranged, int inflictedDamage)
    {
        if (!_started)
            return inflictedDamage;

        var towerId = _towers.TowerIdOf(component);
        if (towerId == null)
            return inflictedDamage;

        var side = AttackerOf(attacker, out var record);
        int applied;
        TowerDamageReason reason;
        lock (_gate)
        {
            if (_match == null)
                return inflictedDamage;

            applied = _match.AdjustTowerDamage(new TowerHit(towerId, ranged, record?.Role ?? UnitRole.Infantry, inflictedDamage, side), out reason);
        }

        _report.TowerBlow(towerId, ClassOf(record, ranged), reason, inflictedDamage, side == null);
        return applied;
    }

    public void NoteUnappliedBlow(object component, object? attacker, bool weaponless, bool ranged)
    {
        if (!_started)
            return;

        var towerId = _towers.TowerIdOf(component);
        if (towerId == null)
            return;

        var side = AttackerOf(attacker, out var record);
        _report.TowerBlow(towerId, ClassOf(record, ranged), weaponless ? TowerDamageReason.Weaponless : TowerDamageReason.RawZero, 0, side == null);
    }

    /// <summary>
    /// The side of an attacker and its unit record, when it has one. The adapter lookups run outside this class's gate, which is
    /// never held across an adapter call. An attacker with no unit record, such as a tower gunner, still has a team.
    /// </summary>
    private MatchSide? AttackerOf(object? attacker, out UnitRecord? record)
    {
        AgentKey attackerKey = default;
        var hasKey = attacker != null && _battlefield.TryGetKey(attacker, out attackerKey);
        record = null;
        if (hasKey)
        {
            lock (_gate)
                _units.TryGetValue(attackerKey, out record);
        }

        return record?.Side ?? (attacker != null ? _battlefield.SideOf(attacker) : null);
    }

    private static TowerBlowClass ClassOf(UnitRecord? record, bool ranged)
    {
        if (record == null)
            return TowerBlowClass.Other;

        switch (record.Role)
        {
            case UnitRole.Hero:
                return TowerBlowClass.Hero;
            case UnitRole.Siege:
                return TowerBlowClass.Siege;
            case UnitRole.Ranged:
            case UnitRole.HorseArcher:
                return TowerBlowClass.Ranged;
            default:
                return ranged ? TowerBlowClass.Ranged : TowerBlowClass.Melee;
        }
    }

    /// <summary>Ends the director's work: the session, the adapters and anything parked. Safe to call twice.</summary>
    public void Shutdown()
    {
        if (_shutDown)
            return;

        _shutDown = true;
        _session.Clear();
        _towers.Clear();
        _battlefield.Clear();
        _deferred.Clear();
        lock (_gate)
        {
            _units.Clear();
            _backlog.Clear();
            _match = null;
        }
    }

    private MapLayout? BuildLayout()
    {
        var map = _plan.Map;
        if (map.Layout == MapLayoutMode.Tags)
        {
            if (_scene.TryReadTaggedLayout(map.SceneId, map.Camp, out var tagged, out var problem))
                return tagged;

            _logger.LogWarning($"[FordsOfWar] scene {map.SceneId} asks for a tagged layout but {problem}; generating the layout from the boundary instead");
        }

        if (!_scene.TryGetBasePoints(InsetFraction, out var west, out var east))
        {
            _logger.LogError($"[FordsOfWar] scene {map.SceneId} has no usable boundary for the bases; the match cannot start");
            return null;
        }

        var generated = LaneLayoutGenerator.Generate(map.SceneId, west, east, map.Options);
        var camps = generated.Camps.Select(c => c with { TroopId = map.Camp.TroopId, Count = map.Camp.Count, Reward = map.Camp.Reward }).ToList();
        return generated with { Camps = camps };
    }

    /// <summary>Builds every tower entity; the layout's list without the ones that could not be made, or null when a keep failed.</summary>
    private List<TowerSlot>? BuildTowers(MapLayout layout)
    {
        var chain = new TowerChain(layout, _plan.Config.Towers);
        var built = new List<TowerSlot>();
        foreach (var slot in layout.Towers)
        {
            var prefab = slot.Kind == TowerKind.Keep ? _plan.Map.Keep : _plan.Map.Tower;
            if (_towers.SpawnTower(slot, prefab, chain.MaxHitPoints(slot.Id)))
            {
                built.Add(slot);
                continue;
            }

            if (slot.Kind == TowerKind.Keep)
            {
                _logger.LogError($"[FordsOfWar] the keep {slot.Id} could not be built; the match cannot start");
                return null;
            }

            _logger.LogWarning($"[FordsOfWar] tower {slot.Id} could not be built; it is left out of the match");
        }

        return built;
    }

    private FordsMatch CreateMatch(MapLayout layout)
    {
        var lanes = layout.Lanes.Select(l => l.Index).ToList();
        var slots = new List<CommanderSlot>
        {
            new(HumanSlotId, MatchSide.West, true, null, lanes),
            new(AiSlotId, MatchSide.East, false, _plan.AiProfile, lanes),
        };
        return new FordsMatch(_plan.Config, layout, _plan.Factions, slots, _random);
    }

    private void SpawnGunners(IReadOnlyList<TowerSlot> towers)
    {
        var count = _plan.Config.Towers.GunnersPerTower;
        if (count <= 0)
            return;

        foreach (var side in new[] { MatchSide.West, MatchSide.East })
        {
            var troop = _plan.Factions[side].Units.FirstOrDefault(u => u.Role == UnitRole.Ranged)?.TroopId;
            if (troop == null)
            {
                _logger.LogWarning($"[FordsOfWar] the {side} faction has no ranged offer; its towers get no garrison");
                continue;
            }

            foreach (var tower in towers.Where(t => t.Owner == side))
                _towers.SpawnGunners(tower, troop, count);
        }
    }

    private void FillSession(MapLayout layout, FordsMatch match)
    {
        var keep = layout.Towers.First(t => t.Kind == TowerKind.Keep && t.Owner == MatchSide.West);
        var enemyKeep = layout.Towers.First(t => t.Kind == TowerKind.Keep && t.Owner == MatchSide.East);
        var march = FordsTowerGeometry.MarchDirection(keep);
        var start = keep.Position - march * CameraSetback + new Vec3f(0f, 0f, CameraHeight);
        _session.Fill(_gate, match, HumanSlotId, _battlefield, start, enemyKeep.Position, _scene.BoundaryPolygon());
    }

    private bool IsHero(AgentKey key) => _units.TryGetValue(key, out var record) && record.Role == UnitRole.Hero;

    private IReadOnlyList<TowerSlot> StandingTowers() => _layout.Towers.Where(t => !_fallen.Contains(t.Id)).ToList();

    private void ApplyEffects(MatchEffects effects)
    {
        foreach (var spawn in effects.Spawns)
            _backlog.Add(new PendingSpawn(spawn) { Remaining = spawn.Count });
        SpawnFromBacklog();

        foreach (var command in effects.Commands)
            ApplyCommand(command);

        foreach (var heal in effects.Heals)
            _towers.Heal(heal.TowerId, heal.Amount);

        foreach (var volley in effects.Volleys)
        {
            var target = _battlefield.PositionOf(volley.Target);
            _report.Volley(target != null, target is { } at ? _towers.Fire(volley.TowerId, at, volley.Missiles) : 0);
        }

        if (effects.Events.Count > 0)
        {
            _session.PushEvents(effects.Events);
            foreach (var e in effects.Events)
                ReportEvent(e);
        }

        if (effects.Winner is { } winner)
        {
            lock (_gate)
                _winner = winner;
            _logger.LogInfo($"[FordsOfWar] the {winner} side has won");
            _report.Winner(winner);
        }
    }

    // A fallen tower or keep is reported by OnTowerDestroyed, so only heroes and captured points are marked here.
    private void ReportEvent(MatchEvent e)
    {
        switch (e.Kind)
        {
            case MatchEventKind.HeroFielded:
                _report.HeroMark("fielded", e.Subject, e.Side);
                break;
            case MatchEventKind.HeroDied:
                _report.HeroMark("died", e.Subject, e.Side);
                break;
            case MatchEventKind.HeroRespawned:
                _report.HeroMark("respawned", e.Subject, e.Side);
                break;
            case MatchEventKind.PointCaptured:
                _report.PointCaptured(e.Subject);
                break;
        }
    }

    private void ReportMatchStart(MapLayout layout, IReadOnlyList<TowerSlot> built, int expected)
    {
        var ids = new List<string>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var faction in _plan.Factions.Values)
        {
            foreach (var unit in faction.Units)
                AddId(ids, seen, unit.TroopId);
            foreach (var hero in faction.Heroes)
                AddId(ids, seen, hero.HeroId);
            foreach (var tier in faction.FreeWaves)
            {
                foreach (var unit in tier.Units)
                    AddId(ids, seen, unit.TroopId);
            }
        }

        var missing = ids.Where(id => !_battlefield.CharacterExists(id)).ToList();
        _report.Preflight(ids.Count, missing);
        var config = _plan.Config;
        _report.VolleyItem(_towers.VolleyItemInfo(), config.Towers.FireRange);
        _facts = new FordsMatchFacts(config.Towers.FireRange, config.Towers.AttackRange, config.Capture.Radius, layout.Lanes.Count,
            config.Waves.MaxLivePerSide, config.Heroes.MaxAlivePerSide, KeepDistance(built), _battlefield.MaxAgents);
        _report.MatchStarted(_facts, built, expected);
    }

    /// <summary>The ground distance between the two keeps, the base-to-base distance; 0 when a keep is missing.</summary>
    private static float KeepDistance(IReadOnlyList<TowerSlot> built)
    {
        var west = built.FirstOrDefault(t => t.Kind == TowerKind.Keep && t.Owner == MatchSide.West);
        var east = built.FirstOrDefault(t => t.Kind == TowerKind.Keep && t.Owner == MatchSide.East);
        return west == null || east == null ? 0f : west.Position.Distance2D(east.Position);
    }

    private static void AddId(List<string> ids, HashSet<string> seen, string id)
    {
        if (seen.Add(id))
            ids.Add(id);
    }

    private int BacklogUnits()
    {
        var total = 0;
        foreach (var pending in _backlog)
            total += pending.Remaining;
        return total;
    }

    /// <summary>
    /// Once per mission second hands the report a look at the field (taken under the gate, merged outside it); every fifth look also
    /// compares each standing tower's rules hit points with its entity's.
    /// </summary>
    private void Probe(FordsMatch match, IReadOnlyList<FordsUnitSample> samples)
    {
        var now = _report.MissionSeconds;
        var facts = _facts;
        if (facts == null || now < _probeAt)
            return;

        _probeAt = now + 1.0;
        MatchView view;
        FordsFieldSample sample;
        lock (_gate)
        {
            view = match.GetView(HumanSlotId);
            sample = FordsFieldProbe.Sample(samples, _units, _layout, _fallen, _applied, view, facts, BacklogUnits());
        }

        _report.Field(sample);
        if (++_probes % 5 != 0)
            return;

        foreach (var lane in view.Lanes)
        {
            foreach (var tower in lane.Towers)
                ReportTowerHealth(tower);
        }

        foreach (var keep in view.Keeps)
            ReportTowerHealth(keep);
    }

    private void ReportTowerHealth(MatchView.TowerView tower)
    {
        if (tower.Destroyed)
            return;

        var engine = _towers.HitPoints(tower.Id);
        if (engine.HasValue)
            _report.TowerHealth(tower.Id, tower.HitPoints, engine.Value);
    }

    private void SpawnFromBacklog()
    {
        var budget = Math.Max(1, _plan.Config.Waves.MaxSpawnsPerTick);
        for (var i = 0; i < _backlog.Count && budget > 0;)
        {
            var pending = _backlog[i];
            while (pending.Remaining > 0 && budget > 0)
            {
                if (!SpawnOne(pending.Request))
                {
                    ReportSpawnFailure(pending);
                    pending.Remaining = 0;
                    break;
                }

                pending.Remaining--;
                budget--;
            }

            if (pending.Remaining == 0)
                _backlog.RemoveAt(i);
            else
                i++;
        }
    }

    /// <summary>Tells the match how many units of a request will never spawn, so it can refund and free what the request held.</summary>
    private void ReportSpawnFailure(PendingSpawn pending)
    {
        lock (_gate)
            _match?.OnSpawnFailed(pending.Request.RequestId, pending.Remaining);
    }

    private bool SpawnOne(SpawnRequest request)
    {
        var lane = _layout.Lanes.FirstOrDefault(l => l.Index == request.Lane);
        if (lane == null)
        {
            _report.SpawnFailed(request.TroopId, "no-lane");
            return false;
        }

        var rankKey = (request.Side, request.Lane);
        _ranks.TryGetValue(rankKey, out var rank);
        _ranks[rankKey] = rank + 1;
        var spot = FordsSpawnPlacement.Place(lane, request.Side, rank);

        var spawned = _battlefield.Spawn(request.TroopId, request.Side, request.Lane, spot.Position, spot.Facing);
        if (spawned == null)
        {
            if (_unbuildable.Add(request.TroopId))
                _logger.LogWarning($"[FordsOfWar] could not spawn '{request.TroopId}' (request {request.RequestId}); the rest of that request is dropped");
            _report.SpawnFailed(request.TroopId, "spawn-null");
            return false;
        }

        var record = new UnitRecord(spawned.Key, request.Side, request.Lane, request.OwnerSlot, UnitValue(request, spawned),
            request.Role, request.IsHero ? request.TroopId : null);
        lock (_gate)
        {
            _units[spawned.Key] = record;
            _match?.RegisterUnit(request.RequestId, record);
        }

        _report.Spawned(request.Side, request.Lane, request.TroopId);
        if (request.IsHero)
            _report.HeroMark("spawned", request.TroopId, request.Side, request.Lane);
        return true;
    }

    /// <summary>What a kill of this unit is worth: its pack price per unit when an offer names the troop, else the default price for its level.</summary>
    private int UnitValue(SpawnRequest request, FordsSpawnedAgent spawned)
    {
        if (request.IsHero)
            return _plan.Factions[request.Side].Heroes.FirstOrDefault(h => h.HeroId == request.TroopId)?.Price ?? 0;

        var offer = _plan.Factions[request.Side].Units.FirstOrDefault(u => u.TroopId == request.TroopId);
        if (offer != null && offer.PackSize > 0)
            return Math.Max(1, offer.PackPrice / offer.PackSize);

        return Math.Max(1, (int)Math.Round(TroopPricing.UnitPrice(spawned.Level, spawned.Mounted, 1f)));
    }

    private void ApplyCommand(FormationCommand command)
    {
        var key = (command.Side, command.Lane);
        if (_applied.TryGetValue(key, out var last) && last == command.Command)
            return;

        // Recorded only once an order was given. A Move the adapter skips (target off the navmesh) falls back to the nearest
        // waypoint of the lane's own path; when even that is skipped the command is tried again on the next tick.
        var given = true;
        switch (command.Command.Kind)
        {
            case LaneCommandKind.MoveTo:
            case LaneCommandKind.HoldAt:
                given = MoveOrNearestWaypoint(command, command.Command.Target);
                break;
            case LaneCommandKind.Charge:
                _battlefield.Charge(command.Side, command.Lane);
                break;
            case LaneCommandKind.AttackTower:
                given = AttackTower(command);
                break;
        }

        if (!given)
        {
            if (_refused.TryGetValue(key, out var lastRefused) && lastRefused == command.Command)
                return;

            _refused[key] = command.Command;
            _report.LaneCommand(command.Side, command.Lane, command.Command.Kind, false);
            return;
        }

        _refused.Remove(key);
        _report.LaneCommand(command.Side, command.Lane, command.Command.Kind, true);
        _applied[key] = command.Command;
    }

    private bool AttackTower(FormationCommand command)
    {
        var towerId = command.Command.TowerId ?? string.Empty;
        if (_battlefield.AttackTower(command.Side, command.Lane, towerId))
            return true;

        // No live entity to attack (it fell between ticks): walk to where it stood.
        var slot = _layout.Towers.FirstOrDefault(t => t.Id == towerId);
        return slot == null || MoveOrNearestWaypoint(command, slot.Position);
    }

    /// <summary>
    /// The interior path points were checked walkable at setup, so the nearest one to a skipped target is a safe place to send the
    /// lane. The fitter keeps a lane's two ends even off the navmesh: for such an end both Moves fail and the lane keeps its last order.
    /// </summary>
    private bool MoveOrNearestWaypoint(FormationCommand command, Vec3f target)
    {
        if (_battlefield.Move(command.Side, command.Lane, target))
            return true;

        var lane = _layout.Lanes.FirstOrDefault(l => l.Index == command.Lane);
        if (lane == null || lane.Path.Count == 0)
            return false;

        return _battlefield.Move(command.Side, command.Lane, lane.Path[LaneGeometry.NearestIndex(lane.Path, target)]);
    }

    // Always parked, even on the main thread: the engine reports a fall from inside its native hit callback, and KillGunners fades agents out.
    private void OnTowerDestroyed(string towerId) =>
        _deferred.Enqueue(() =>
        {
            lock (_gate)
            {
                _fallen.Add(towerId);
                _match?.OnTowerDestroyed(towerId, null);
            }

            _towers.KillGunners(towerId);
            _report.TowerFell(towerId, _layout.Towers.FirstOrDefault(t => t.Id == towerId)?.Kind == TowerKind.Keep);
        });
}
