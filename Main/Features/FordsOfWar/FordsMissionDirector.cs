using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using BehaviorTreeWrapper;
using TAOM.Adapters;
using TAOM.Core.Logging;
using TAOM.Features.AdvancedCombat;
using TAOM.Features.FordsOfWar.Diagnostics;
using TAOM.Features.FordsOfWar.Domain;
using TAOM.Features.FordsOfWar.Hooks;

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
    internal const int WestSlotId = 1;
    internal const int EastSlotId = 2;

    private const float CorpseFadeSeconds = 4f;
    private const float InsetFraction = 0.1f;
    private const float CameraHeight = 45f;
    private const float CameraSetback = 25f;
    private const string RemovedSite = "FordsMissionDirector.OnAgentRemoved";
    private const string ArrowHitSite = "FordsMissionDirector.OnArrowHit";
    private const string CampRemovedSite = "FordsMissionDirector.OnCampUnitGone";

    /// <summary>How far from a camp's centre its creatures start, so they do not stand inside each other.</summary>
    private const float CampSpreadMeters = 3f;

    private sealed class PendingSpawn
    {
        public PendingSpawn(SpawnRequest request) => Request = request;

        public SpawnRequest Request { get; }
        public int Remaining { get; set; }
    }

    private readonly FordsMatchPlan _plan;
    private readonly HeroCombatFactors _heroCombat;
    private readonly IFordsBattlefieldAdapter _battlefield;
    private readonly IFordsTowerAdapter _towers;
    private readonly IFordsCampAdapter _camps;
    private readonly IFordsSceneAdapter _scene;
    private readonly FordsSession _session;
    private readonly IModLogger _logger;
    private readonly IFordsRandom _random;
    private readonly DeferredCallbackQueue _deferred;
    private readonly FordsRunReport _report;

    private readonly object _gate = new();
    private readonly Dictionary<AgentKey, UnitRecord> _units = new();
    private readonly Dictionary<(MatchSide, int), LaneCommand> _applied = new();
    private readonly Dictionary<(MatchSide, int), LaneCommand> _sent = new();
    private readonly Dictionary<(MatchSide, int), LaneArrangement> _arranged = new();
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
    private readonly FordsTroopTally _tally = new();
    // Kept after a death: the killer may already be dead when its victim's death arrives.
    private readonly Dictionary<AgentKey, string> _troopOf = new();
    private bool _sceneAuthored;
    private double _garrisonLookAt;
    private float _housekeepIn = HousekeepSeconds;
    private Vec3f? _firstRefused;
    private const double GarrisonLookSeconds = 30.0;

    // The engine's AmmoSupplyLogic also runs on 3 s.
    private const float HousekeepSeconds = 3f;

    private volatile bool _started;
    private volatile bool _shutDown;

    public FordsMissionDirector(FordsMatchPlan plan, IFordsBattlefieldAdapter battlefield, IFordsTowerAdapter towers,
        IFordsCampAdapter camps, IFordsSceneAdapter scene, FordsSession session, IModLogger logger, IFordsRandom random, FordsRunReport report)
    {
        _plan = plan;
        _heroCombat = HeroCombatFactors.Of(plan.Factions.Values.SelectMany(f => f.Heroes), plan.Config.Heroes);
        _battlefield = battlefield;
        _towers = towers;
        _camps = camps;
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
    /// running, when the scene cannot host a match. An exception shuts the partly started director down before it propagates, so
    /// nothing it bound (the report's lookups, the session) outlives it. Call once, from the first mission tick (never inside
    /// <c>AfterStart</c>: see <c>FordsOfWarMissionLogic</c>).
    /// </summary>
    public bool Start()
    {
        if (_started)
            return true;

        try
        {
            return StartCore();
        }
        catch
        {
            Shutdown();
            throw;
        }
    }

    private bool StartCore()
    {
        var planTimer = Stopwatch.StartNew();
        if (!PlanLayout(out var fit, out var expectedIds))
            return false;

        var planTime = planTimer.Elapsed;

        if (fit.Rejected)
        {
            _logger.LogError($"[FordsOfWar] the layout for {_plan.Map.SceneId} was refused: {fit.Reason}");
            return false;
        }

        var layout = fit.Layout;
        var expected = expectedIds.Count;
        if (!_battlefield.Prepare(layout.Lanes.Count))
        {
            _logger.LogError("[FordsOfWar] the mission has no West and East teams (or too many lanes); the match cannot start");
            return false;
        }

        _battlefield.SetLaneBars(LaneBarsFor(layout));

        var towerTimer = Stopwatch.StartNew();
        var built = BuildTowers(layout);
        _logger.LogInfo(FordsLayoutFitLines.Timing(planTime, towerTimer.Elapsed));
        if (built == null)
            return false;

        var emptied = FordsLayoutFitter.EmptiedGroups(layout.Towers, built, "could not be built");
        if (emptied.Length > 0)
        {
            _logger.LogError($"[FordsOfWar] the layout for {_plan.Map.SceneId} was refused after the towers were built: {emptied}");
            return false;
        }

        layout = layout with { Towers = built };
        _battlefield.SetCorpseFadeOutSeconds(CorpseFadeSeconds);
        var match = CreateMatch(layout);
        lock (_gate)
        {
            _layout = layout;
            _match = match;
        }

        ReportMatchStart(layout, built, expected);
        ReportLayout(layout, expectedIds, match);
        _report.BindGunnerLookup(_towers.TowerIdOfGunner);
        _report.BindTowerLookup(_towers.TowerIdOf);
        _report.BindAttackerLookup(Attacker);

        _towers.SetDestroyedHandler(OnTowerDestroyed);
        SpawnGunners(built);
        SpawnCamps(layout, match);
        _battlefield.CompleteDeployment();
        FillSession(layout, match);
        _started = true;
        _logger.LogInfo($"[FordsOfWar] match started on {layout.SceneName}: {layout.Lanes.Count} lane(s), {built.Count} tower(s) and keep(s), AI profile {_plan.AiProfile}"
            + (_plan.WestAi ? "; an AI commands both sides" : ""));
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

        _housekeepIn -= dt;
        if (_housekeepIn <= 0f)
        {
            _housekeepIn = HousekeepSeconds;
            _battlefield.Housekeep();
        }

        var samples = _battlefield.SampleUnits();
        MatchEffects effects;
        MatchPhase phase;
        lock (_gate)
        {
            var snapshot = FordsSnapshotBuilder.Build(dt, samples, IsHero, StandingTowers(), _layout.CapturePoints,
                _plan.Config.Towers, _plan.Config.Capture, t => match.FireRange(t.Id));
            effects = match.Tick(snapshot);
            FordsUpgradeHooks.Set(match.CombatUpgrades(MatchSide.West), match.CombatUpgrades(MatchSide.East), _heroCombat);
            phase = match.Phase;
        }

        if (!_liveStamped && phase == MatchPhase.Live)
        {
            _liveStamped = true;
            _report.Stage("live");
        }

        ApplyEffects(effects);
        Probe(match, samples);
        ReportLaneStates(match);
    }

    /// <summary>An arrow or bolt struck an agent. May run off the main thread.</summary>
    public void OnArrowHit(object affected, object? affector)
    {
        if (!_started || !_battlefield.TryGetKey(affected, out var victim) || !_battlefield.TryGetKey(affector, out var attacker))
            return;

        _deferred.RunOrDefer(ArrowHitSite, () =>
        {
            lock (_gate)
                _match?.OnArrowHit(victim, attacker);
        });
    }

    /// <summary>The engine removed an agent (killed, knocked out, routed or deleted). May run off the main thread.</summary>
    public void OnAgentRemoved(object affected, object? affector, bool killed)
    {
        if (!_started)
            return;

        if (_camps.TryRemove(affected, affector, killed, out var campId, out var campKiller))
        {
            _deferred.RunOrDefer(CampRemovedSite, () => OnCampUnitGone(campId, campKiller));
            return;
        }

        if (!_battlefield.TryRemoveUnit(affected, affector, killed, out var victim, out var killer))
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
            {
                _report.Death(record.Side, paid);
                // The tally is the match's trade: a death in the result window changes no result, so it stays out (Codex, 2026-11-05).
                if (paid != KillCredit.AfterEnd)
                    TallyDeath(victim, killer, record.Value);
            }
        });
    }

    private void TallyDeath(AgentKey victim, AgentKey? killer, int value)
    {
        lock (_gate)
        {
            if (!_troopOf.TryGetValue(victim, out var troop))
                return;

            string? killerTroop = null;
            if (killer is { } key)
                _troopOf.TryGetValue(key, out killerTroop);
            _tally.Death(troop, killerTroop, value);
        }
    }

    private void LogBalance()
    {
        lock (_gate)
        {
            foreach (var side in new[] { MatchSide.West, MatchSide.East })
            {
                if (_match?.PoisonTotals(side) is { Doses: > 0 } poison)
                    _logger.LogInfo($"[FordsOfWar][Balance] poison side={side} doses={poison.Doses} damage={poison.Damage}");
            }

            foreach (var line in _tally.Lines())
                _logger.LogInfo("[FordsOfWar][Balance] " + line);
            if (_match == null)
                return;

            foreach (var gold in _match.GoldTotals())
                _logger.LogInfo($"[FordsOfWar][Balance] side={gold.Side} slot={gold.Slot} earned={gold.Earned} spent={gold.Spent} left={gold.Left} {gold.Breakdown}");
        }
    }

    private void OnCampUnitGone(string campId, AgentKey? killer)
    {
        CampCleared? cleared;
        lock (_gate)
            cleared = _match?.OnCampUnitKilled(campId, killer);

        if (cleared != null)
            _logger.LogInfo($"[FordsOfWar][Camp] {campId} cleared by {cleared.Side?.ToString() ?? "nobody"}: slot {cleared.Slot} paid {cleared.Reward}");
    }

    public int AdjustTowerDamage(object component, object? attacker, bool ranged, int inflictedDamage, int thrustDamage)
    {
        if (!_started)
            return inflictedDamage;

        var towerId = _towers.TowerIdOf(component);
        if (towerId == null)
            return inflictedDamage;

        var who = AttackerOf(attacker, out var record);
        int applied;
        TowerDamageReason reason;
        lock (_gate)
        {
            if (_match == null)
                return inflictedDamage;

            applied = _match.AdjustTowerDamage(new TowerHit(towerId, ranged, record?.Role ?? UnitRole.Infantry, inflictedDamage, who?.Side, thrustDamage), out reason);
        }

        _report.TowerBlow(towerId, ClassOf(record, ranged), reason, inflictedDamage, applied, who);
        return applied;
    }

    public void NoteUnappliedBlow(object component, object? attacker, bool weaponless, bool ranged)
    {
        if (!_started)
            return;

        var towerId = _towers.TowerIdOf(component);
        if (towerId == null)
            return;

        var who = AttackerOf(attacker, out var record);
        var reason = TowerDamageReason.Weaponless;
        if (!weaponless)
        {
            // The table says whether a zero-damage blow came from the tower's own side or from anyone else. The query changes nothing,
            // so this method keeps the interface's promise by construction.
            lock (_gate)
                reason = _match?.ReasonFor(new TowerHit(towerId, ranged, record?.Role ?? UnitRole.Infantry, 0, who?.Side)) ?? TowerDamageReason.RawZero;
        }

        _report.TowerBlow(towerId, ClassOf(record, ranged), reason, 0, 0, who);
    }

    /// <summary>
    /// The side of an attacker (and its lane, when it is a lane unit) and its unit record, when it has one. The adapter lookups run
    /// outside this class's gate, which is never held across an adapter call. An attacker with no unit record still has a team,
    /// except a tower's missile owner, whose team has no side: its side is its tower's.
    /// </summary>
    private FordsAttacker? AttackerOf(object? attacker, out UnitRecord? record)
    {
        AgentKey attackerKey = default;
        var hasKey = attacker != null && _battlefield.TryGetKey(attacker, out attackerKey);
        record = null;
        if (hasKey)
        {
            lock (_gate)
                _units.TryGetValue(attackerKey, out record);
        }

        var side = record?.Side ?? (attacker != null ? _battlefield.SideOf(attacker) ?? _towers.OwnerSideOfGunner(attacker) : null);
        return side is { } known ? new FordsAttacker(known, record?.Lane ?? -1) : null;
    }

    private FordsAttacker? Attacker(object? agent) => AttackerOf(agent, out _);

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
        LogBalance();
        var upgradeHits = FordsUpgradeHooks.Counts;
        var heroHits = FordsUpgradeHooks.HeroCounts;
        _logger.LogInfo($"[FordsOfWar][Upgrade] blows raised={upgradeHits.Raised} lowered={upgradeHits.Lowered} heroRaised={heroHits.Raised} heroLowered={heroHits.Lowered} ownTowerShots={FordsUpgradeHooks.OwnTowerShots} (totals)");
        FordsUpgradeHooks.Clear();
        _report.BindGunnerLookup(null);
        _report.BindTowerLookup(null);
        _report.BindAttackerLookup(null);
        _session.Clear();
        _towers.Clear();
        _camps.Clear();
        _battlefield.Clear();
        _deferred.Clear();
        lock (_gate)
        {
            _units.Clear();
            _backlog.Clear();
            _match = null;
        }
    }

    /// <summary>
    /// The layout to play, fitted to the scene, and the tower ids it named before the fit. A tagged layout is fitted as authored; a
    /// generated one goes through <see cref="FordsLayoutPlanner"/> (narrower spreads, then the other axis of a near-square
    /// boundary) and the best attempt is kept, with a warning when none met the limits. False only when no layout could be built.
    /// </summary>
    private bool PlanLayout(out FordsLayoutFit fit, out IReadOnlyList<string> expectedIds)
    {
        var map = _plan.Map;
        MapLayout? candidate = null;
        fit = default;
        IReadOnlyList<FordsLayoutAttempt> attempts = Array.Empty<FordsLayoutAttempt>();
        if (map.Layout == MapLayoutMode.Tags)
        {
            if (_scene.TryReadTaggedLayout(map.SceneId, map.Camp, out var tagged, out var problem))
            {
                candidate = tagged;
                _sceneAuthored = true;
                fit = _scene.Fit(tagged);
            }
            else
            {
                _logger.LogWarning($"[FordsOfWar] scene {map.SceneId} asks for a tagged layout but {problem}; generating the layout from the boundary instead");
            }
        }

        if (candidate == null)
        {
            var bases = new Dictionary<FordsBaseAxis, (Vec3f West, Vec3f East)?>();
            var plan = FordsLayoutPlanner.Plan((factor, axis) => Generate(map, factor, axis, bases), _scene.Fit,
                FordsBaseFinder.AspectRatio(_scene.BoundaryPolygon()));
            attempts = plan.Attempts;
            if (plan.Chosen is not { } chosen)
            {
                _logger.LogError($"[FordsOfWar] scene {map.SceneId} has no usable boundary for the bases; the match cannot start");
                expectedIds = Array.Empty<string>();
                return false;
            }

            if (!plan.Passed && !chosen.Fit.Rejected)
                _logger.LogWarning($"[FordsOfWar] {plan.Summary}; playing the best attempt: {chosen.Summary}");
            candidate = chosen.Candidate;
            fit = chosen.Fit;
        }

        expectedIds = candidate.Towers.Select(t => t.Id).ToList();
        foreach (var line in FordsLayoutFitLines.Of(attempts, fit))
            _logger.LogInfo(line);
        return true;
    }

    /// <summary>One generated candidate: the bases for the axis (found once per axis and cached), the lanes at the spread factor.</summary>
    private MapLayout? Generate(MapEntry map, float spreadFactor, FordsBaseAxis axis, Dictionary<FordsBaseAxis, (Vec3f West, Vec3f East)?> bases)
    {
        if (!bases.TryGetValue(axis, out var found))
        {
            var ok = _scene.TryGetBasePoints(InsetFraction, axis == FordsBaseAxis.Short, out var west, out var east);
            found = ok ? (west, east) : null;
            bases[axis] = found;
        }

        if (found is not { } points)
            return null;

        var options = map.Options with { LaneSpread = map.Options.LaneSpread * spreadFactor };
        var generated = LaneLayoutGenerator.Generate(map.SceneId, points.West, points.East, options);
        var camps = generated.Camps.Select(c => c with { TroopId = map.Camp.TroopId, Count = map.Camp.Count, Reward = map.Camp.Reward }).ToList();
        return generated with { Camps = camps };
    }

    /// <summary>Builds every tower entity; the layout's list without the ones that could not be made, or null when a keep failed.</summary>
    private List<TowerSlot>? BuildTowers(MapLayout layout)
    {
        var chain = new TowerChain(layout, _plan.Config.Towers, _plan.Factions.ToDictionary(f => f.Key, f => f.Value.FeatsOrNone));
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
            new(WestSlotId, MatchSide.West, !_plan.WestAi, _plan.WestAi ? _plan.AiProfile : null, lanes),
            new(EastSlotId, MatchSide.East, false, _plan.AiProfile, lanes),
        };
        return new FordsMatch(_plan.Config, layout, _plan.Factions, slots, _random);
    }

    private LaneFaceBars? LaneBarsFor(MapLayout layout)
    {
        var bars = _plan.Map.LaneBars;
        if (bars == null)
            return null;

        if (bars.LaneCount != layout.Lanes.Count)
        {
            _logger.LogWarning($"[FordsOfWar] laneFaceIds names {bars.LaneCount} lane(s) and the layout has {layout.Lanes.Count}; no lane bars");
            return null;
        }

        for (var lane = 0; lane < bars.LaneCount; lane++)
            _logger.LogInfo($"[FordsOfWar][Lane] bars lane={lane} faces=[{string.Join(",", bars.BarsFor(lane))}] sets={bars.RegisteredSets}");
        return bars;
    }

    /// <summary>
    /// Each camp's ring and creatures. A creature that cannot be made counts as gone, so a camp missing some can still be cleared and
    /// one missing all of them is cleared from the start, which no lane will try to seize.
    /// </summary>
    private void SpawnCamps(MapLayout layout, FordsMatch match)
    {
        if (layout.Camps.Count == 0)
            return;

        var prepared = _camps.Prepare();
        if (!prepared)
            _logger.LogWarning("[FordsOfWar][Camp] the neutral team could not be made; the map plays without camps");

        foreach (var camp in layout.Camps)
        {
            var spawned = 0;
            for (var i = 0; i < camp.Count; i++)
            {
                var angle = i * 2f * (float)Math.PI / camp.Count;
                var spot = new Vec3f(camp.Position.X + CampSpreadMeters * (float)Math.Cos(angle), camp.Position.Y + CampSpreadMeters * (float)Math.Sin(angle), camp.Position.Z);
                if (prepared && _camps.Spawn(camp.Id, camp.TroopId, spot))
                    spawned++;
                else
                    match.OnCampUnitKilled(camp.Id, null);
            }

            if (spawned > 0)
            {
                _camps.AddRing(camp.Id, camp.Position, CampRules.Radius);
                _camps.SetAlerted(camp.Id, camp.Position, false);
            }

            _logger.LogInfo($"[FordsOfWar][Camp] {camp.Id}: {spawned} of {camp.Count} {camp.TroopId} at ({camp.Position.X:F0}, {camp.Position.Y:F0}), ring {(spawned > 0 ? CampRules.Radius : 0f):F0} m, reward {camp.Reward}");
        }
    }

    private void SpawnGunners(IReadOnlyList<TowerSlot> towers)
    {
        // One hidden missile owner per tower. A real tower's stands on the scene's first bowman spot; one with no spots, a keep, fires nothing.
        var realTowers = _plan.Map.TowerHalfWidth > 0f;
        FordsUpgradeHooks.SetTowerShots(_towers.IsOwnShot);
        foreach (var side in new[] { MatchSide.West, MatchSide.East })
        {
            var troop = _plan.Factions[side].Units.FirstOrDefault(u => u.Role == UnitRole.Ranged)?.TroopId;
            if (troop == null)
            {
                _logger.LogWarning($"[FordsOfWar] the {side} faction has no ranged offer; its towers get no garrison");
                continue;
            }

            foreach (var tower in towers.Where(t => t.Owner == side))
            {
                if (!realTowers || tower.GunnerSpots is { Count: > 0 })
                    _towers.SpawnGunners(tower, troop, 1);
            }
        }
    }

    private void FillSession(MapLayout layout, FordsMatch match)
    {
        var keep = layout.Towers.First(t => t.Kind == TowerKind.Keep && t.Owner == MatchSide.West);
        var enemyKeep = layout.Towers.First(t => t.Kind == TowerKind.Keep && t.Owner == MatchSide.East);
        var march = FordsTowerGeometry.MarchDirection(keep);
        var start = keep.Position - march * CameraSetback + new Vec3f(0f, 0f, CameraHeight);
        _session.Fill(_gate, match, WestSlotId, _battlefield, start, enemyKeep.Position, _scene.BoundaryPolygon());
    }

    private bool IsHero(AgentKey key) => _units.TryGetValue(key, out var record) && record.Role == UnitRole.Hero;

    private IReadOnlyList<TowerSlot> StandingTowers() => _layout.Towers.Where(t => !_fallen.Contains(t.Id)).ToList();

    private void ApplyEffects(MatchEffects effects)
    {
        foreach (var spawn in effects.Spawns)
        {
            _backlog.Add(new PendingSpawn(spawn) { Remaining = spawn.Count });
            // A guard may share its troop with a bought pack, so the spawn counts by troop cannot show it (Lens 5, 2026-11-05).
            if (spawn.IsGuard)
                _logger.LogInfo($"[FordsOfWar][Hero] guard side={spawn.Side} lane={spawn.Lane} troop={spawn.TroopId} count={spawn.Count}");
        }

        SpawnFromBacklog();

        foreach (var command in effects.Commands)
            ApplyCommand(command);

        foreach (var poison in effects.PoisonTicks)
            _battlefield.ApplyPoison(poison.Victim, poison.Attacker, poison.Damage);

        foreach (var order in effects.CampOrders)
        {
            var home = _layout.Camps.FirstOrDefault(c => c.Id == order.CampId)?.Position ?? default;
            _camps.SetAlerted(order.CampId, home, order.Alerted);
            _logger.LogInfo($"[FordsOfWar][Camp] {order.CampId} {(order.Alerted ? "alerted: creatures go out" : "quiet: creatures go home")}");
        }

        foreach (var heal in effects.Heals)
        {
            _towers.Heal(heal.TowerId, heal.Amount);
            _report.TowerHealed(heal.TowerId, heal.Amount);
        }

        foreach (var volley in effects.Volleys)
        {
            var target = _battlefield.PositionOf(volley.Target);
            var fired = target is { } at ? _towers.Fire(volley.TowerId, at, volley.Missiles, _plan.Map.TowerHalfWidth, volley.Rock) : 0;
            _report.Volley(target != null, fired, volley.Rock && _towers.RockLoaded);
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
            case MatchEventKind.UpgradeBought:
                _logger.LogInfo($"[FordsOfWar][Upgrade] side={e.Side} track={e.Subject} price={e.Amount}");
                break;
            case MatchEventKind.WallsWeaken:
                _logger.LogInfo("[FordsOfWar][Wear] the walls begin to weaken");
                break;
            case MatchEventKind.TimeUp:
                _logger.LogInfo($"[FordsOfWar][Time] time is up: {e.Side} wins, {e.Subject} (towers/share of hit points)");
                _report.EndedOnTime();
                break;
        }
    }

    private void ReportMatchStart(MapLayout layout, IReadOnlyList<TowerSlot> built, int expected)
    {
        var ids = new List<string>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var faction in _plan.Factions.Values)
        {
            foreach (var unit in faction.AllOffers)
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
        _report.VolleyItem(_towers.VolleyItemInfo(), _plan.LongestFireRange);
        _report.ArrowItem(_towers.ArrowItemInfo(), _plan.LongestFireRange);
        _report.TowerConfig(config.Towers);
        _facts = new FordsMatchFacts(config.Towers.FireRange, config.Towers.AttackRange, config.Capture.Radius, layout.Lanes.Count,
            config.Waves.MaxLivePerSide, config.Heroes.MaxAlivePerSide, KeepDistance(built), _battlefield.MaxAgents,
            FordsTowerGeometry.ContactMetres(_plan.Map.TowerHalfWidth),
            _plan.Factions[MatchSide.West].FeatsOrNone.UnitCapBonus + _plan.Factions[MatchSide.West].FeatsOrNone.HeroSlotBonus,
            _plan.Factions[MatchSide.East].FeatsOrNone.UnitCapBonus + _plan.Factions[MatchSide.East].FeatsOrNone.HeroSlotBonus,
            _plan.Factions[MatchSide.West].FeatsOrNone.TowerRange, _plan.Factions[MatchSide.East].FeatsOrNone.TowerRange);
        _report.MatchStarted(_facts, built, expected);
    }

    /// <summary>
    /// The layout line and check 4's layout part: which towers the layout named and the engine did not build, and whether either keep
    /// could already be damaged (read from the match's tower chain through the human slot's view).
    /// </summary>
    private void ReportLayout(MapLayout layout, IReadOnlyList<string> expectedIds, FordsMatch match)
    {
        MatchView view;
        lock (_gate)
            view = match.GetView(WestSlotId);
        _report.Layout(FordsLayoutFacts.Of(layout, expectedIds, view));
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
    /// Once per mission second hands the report a look at the field (taken under the gate, merged outside it) and the rules' hit points
    /// of each standing tower; every fifth look also compares them with the entity's.
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
            view = match.GetView(WestSlotId);
            sample = FordsFieldProbe.Sample(samples, _units, _layout, _fallen, _applied, view, facts, BacklogUnits());
        }

        _report.Field(sample);
        if (_sceneAuthored)
            _report.Faces(_battlefield.SampleFaces());

        if (_plan.Map.TowerHalfWidth > 0f && now >= _garrisonLookAt)
        {
            _garrisonLookAt = now + GarrisonLookSeconds;
            _towers.LogGarrison();
        }

        var compare = ++_probes % 5 == 0;
        foreach (var lane in view.Lanes)
        {
            foreach (var tower in lane.Towers)
                ReportTowerHealth(tower, compare);
        }

        foreach (var keep in view.Keeps)
            ReportTowerHealth(keep, compare);
    }

    /// <summary>
    /// Every tick asks the report whether a stream's state line is due (every 5 mission seconds) and builds its text only then, so a
    /// tick that writes nothing costs one comparison per stream. A stream the rules have not decided yet writes nothing.
    /// </summary>
    private void ReportLaneStates(FordsMatch match)
    {
        var facts = _facts;
        if (facts == null)
            return;

        var now = _report.MissionSeconds;
        for (var s = 0; s < LaneStreams.Sides; s++)
        {
            var side = (MatchSide)s;
            for (var lane = 0; lane < facts.Lanes; lane++)
            {
                if (!_report.LaneStateDue(side, lane, now))
                    continue;

                string? text = null;
                lock (_gate)
                {
                    var decision = match.LastDecisions.FirstOrDefault(d => d.Side == side && d.Lane == lane);
                    if (decision != null)
                    {
                        _sent.TryGetValue((side, lane), out var sent);
                        text = FordsLaneStateLine.Format(new FordsLaneStateFacts(sent, decision));
                    }
                }

                if (text != null)
                    _report.LaneState(side, lane, now, text);
            }
        }
    }

    /// <summary>Every look records the rules' hit points of a standing tower; every fifth also compares them with its entity's.</summary>
    private void ReportTowerHealth(MatchView.TowerView tower, bool compare)
    {
        if (tower.Destroyed)
            return;

        _report.TowerRulesHp(tower.Id, tower.HitPoints);
        if (!compare)
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
            _troopOf[record.Key] = request.TroopId;
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

        var offer = _plan.Factions[request.Side].AllOffers.FirstOrDefault(u => u.TroopId == request.TroopId);
        if (offer != null && offer.PackSize > 0)
            return Math.Max(1, offer.PackPrice / offer.PackSize);

        return Math.Max(1, (int)Math.Round(TroopPricing.UnitPrice(spawned.Level, spawned.Mounted, 1f)));
    }

    private void ApplyCommand(FormationCommand command)
    {
        var key = (command.Side, command.Lane);
        ApplyArrangement(command);
        if (_applied.TryGetValue(key, out var last) && last == command.Command)
            return;

        // Recorded only once an order was given. A Move the adapter skips (target off the navmesh) falls back to the nearest
        // waypoint of the lane's own path; when even that is skipped the command is tried again on the next tick. Either way the
        // refused target is counted off the navmesh once per command: a command that went through the fallback is recorded as
        // applied and never sent again, and one refused outright is counted only the first time.
        _firstRefused = null;
        var sent = Send(command);
        var counted = _refused.TryGetValue(key, out var lastRefused) && lastRefused == command.Command;
        if (sent == null)
        {
            if (counted)
                return;

            _refused[key] = command.Command;
            ReportOffNavmesh(command);
            _report.LaneCommand(command.Side, command.Lane, command.Command.Kind, false);
            return;
        }

        _refused.Remove(key);
        if (!counted)
            ReportOffNavmesh(command);
        _report.LaneCommand(command.Side, command.Lane, command.Command.Kind, true);
        _applied[key] = command.Command;
        _sent[key] = sent;
    }

    /// <summary>
    /// The tactic's arrangement, given before the movement order and only when it changed: each change runs a pass over every unit
    /// of the formation (shield use, agent properties, behaviour values; entering or leaving a wall also sets each unit's
    /// defensiveness), and a change into or out of Square, Column or Skein also rebuilds the arrangement. One the adapter could not
    /// give (the lane has no formation) is asked again on the next tick.
    /// </summary>
    private void ApplyArrangement(FormationCommand command)
    {
        var key = (command.Side, command.Lane);
        if (command.Arrangement is not { } arrangement || (_arranged.TryGetValue(key, out var had) && had == arrangement))
            return;

        if (_battlefield.SetArrangement(command.Side, command.Lane, arrangement))
            _arranged[key] = arrangement;
    }

    private void ReportOffNavmesh(FormationCommand command)
    {
        if (_firstRefused is { } refusedAt)
            _report.OffNavmesh(command.Side, command.Lane, refusedAt.X, refusedAt.Y);
    }

    /// <summary>The command the engine was actually given, which differs from the rules' when a fallback was used; null when nothing could be sent.</summary>
    private LaneCommand? Send(FormationCommand command)
    {
        var wanted = command.Command;
        switch (wanted.Kind)
        {
            case LaneCommandKind.MoveTo:
            case LaneCommandKind.HoldAt:
                return MoveOrNearestWaypoint(command, wanted.Target) is { } point ? wanted with { Target = point } : null;
            case LaneCommandKind.Charge:
                _battlefield.Charge(command.Side, command.Lane);
                return wanted;
            case LaneCommandKind.AttackTower:
                return AttackTower(command);
            default:
                return wanted;
        }
    }

    private LaneCommand? AttackTower(FormationCommand command)
    {
        var towerId = command.Command.TowerId ?? string.Empty;
        if (_battlefield.AttackTower(command.Side, command.Lane, towerId))
            return command.Command;

        // No live entity to attack (it fell between ticks): walk to where it stood.
        var slot = _layout.Towers.FirstOrDefault(t => t.Id == towerId);
        if (slot == null)
            return LaneCommand.None;

        return MoveOrNearestWaypoint(command, slot.Position) is { } point ? LaneCommand.MoveTo(point) : null;
    }

    /// <summary>
    /// The point the lane was sent to: the target, else the lane path's waypoint nearest it, null when both were refused. The
    /// interior path points were checked walkable at setup, so the nearest one to a skipped target is a safe place to send the
    /// lane. The fitter keeps a lane's two ends even off the navmesh: for such an end both Moves fail and the lane keeps its last order.
    /// </summary>
    private Vec3f? MoveOrNearestWaypoint(FormationCommand command, Vec3f target)
    {
        if (Move(command, target))
            return target;

        var lane = _layout.Lanes.FirstOrDefault(l => l.Index == command.Lane);
        if (lane == null || lane.Path.Count == 0)
            return null;

        var waypoint = lane.Path[LaneGeometry.NearestIndex(lane.Path, target)];
        return Move(command, waypoint) ? waypoint : null;
    }

    /// <summary>The adapter refuses a Move only when its target is off the navmesh; the first target refused for a command is kept for the report.</summary>
    private bool Move(FormationCommand command, Vec3f target)
    {
        if (_battlefield.Move(command.Side, command.Lane, target))
            return true;

        _firstRefused ??= target;
        return false;
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
            _towers.LogFall(towerId);
            _report.TowerFell(towerId, _layout.Towers.FirstOrDefault(t => t.Id == towerId)?.Kind == TowerKind.Keep);
        });
}
