using System;
using System.Collections.Generic;
using System.Linq;
using TAOM.Adapters;
using TAOM.Core.Collections;
using TAOM.Core.Domain;
using TAOM.Core.Logging;
using TAOM.Core.Validation;
using TAOM.Features.CreatureSiegeRole.Domain;
using TAOM.Features.SiegeForces.Domain;

namespace TAOM.Features.CreatureSiegeRole;

/// <summary>
/// The creature siege role for one wall battle (docs/features/creature-siege-role.md). Built by the mission behavior, one per
/// mission, and used on the main thread only.
///
/// <b>Activation</b> (<see cref="TryActivate"/>, from <c>AfterStart</c>): active only in a siege battle that is no sally-out or
/// relief force, on a peer that runs the AI, with the setting on, a creature race registered and an outer gate to strike. When
/// active it publishes the <see cref="CreatureSiegeSnapshot"/> the two game models read, so every creature costs +Infinity to a
/// standing point and its blow on a gate is multiplied. Otherwise it logs why it is inert and does nothing.
///
/// <b>The reconcile</b> (<see cref="Tick"/>, from <c>OnMissionTick</c>): once the deployment is over, every
/// <see cref="CreatureSiegeRules.PassStrideSeconds"/> it (1) excludes each AI-controlled creature from the ladder faces and tower
/// entrances, once, in one fixed order, never removed, (2) keeps a gate status per gate (destroyed is latched, open counts only
/// after two unbroken seconds, a re-closed gate is impassable at once), and (3) gives each creature a role: strike the outer gate,
/// stand off while a ram works, strike the inner gate, hold the courtyard, or, for a defender, hold the gate. A creature is
/// scripted only when its role changes or the engine clears a flag we set; anything the player commands, anything that is
/// fleeing and anything not AI-controlled is released to vanilla, once. A fault releases every routed creature, each in its own
/// try, stops routing for the battle and logs one error; the snapshot stays so the models keep working.
///
/// Nothing is held across frames: the adapters are rebuilt every pass from the live agent list, route records are keyed by object
/// reference and never dereferenced, and nothing is keyed on an agent index (the engine recycles them, #592).
/// </summary>
public sealed class CreatureSiegeRoleService
{
    private const int RoleLogBurst = 8;
    private const double RoleLogPerSecond = 2.0;

    private readonly ICreatureSiegeMissionAdapter _mission;
    private readonly IRaceManager _raceManager;
    private readonly float _gateDamageMultiplier;
    private readonly IModLogger _logger;

    private readonly Dictionary<object, Route> _routes = new(ReferenceIdentity.Instance);
    private readonly List<object> _forget = new();
    private readonly int[] _slotCounters = new int[Enum.GetValues(typeof(SiegeRole)).Length];
    private readonly SiegeLogThrottle _roleLog = new(RoleLogBurst, RoleLogPerSecond);

    private bool _active;
    private bool _armed;
    private bool _stopped;
    private object? _token;
    private Func<int, bool>? _isCreatureRace;
    private SiegeGateReading? _outer;
    private SiegeGateReading? _inner;
    private GateTrack _outerTrack = GateTrack.Closed;
    private GateTrack _innerTrack = GateTrack.Closed;
    private bool _outerPassable;
    private bool _innerPassable;
    private bool _ramWorking;
    private float _ramLastUsed = float.NaN;
    private float _nextPassAt = float.NegativeInfinity;
    private float _now;
    private int _pass;
    private int[] _exclusion = Array.Empty<int>();
    private IReadOnlyList<AnchorCandidate> _holdAnchors = Array.Empty<AnchorCandidate>();
    private IReadOnlyList<AnchorCandidate> _courtyardAnchors = Array.Empty<AnchorCandidate>();
    private bool _warnedNoSlot;
    private bool _warnedNoAnchor;
    private bool _warnedQueue;

    public CreatureSiegeRoleService(ICreatureSiegeMissionAdapter mission, IRaceManager raceManager, float gateDamageMultiplier,
        IModLogger logger)
    {
        _mission = mission;
        _raceManager = raceManager;
        _gateDamageMultiplier = gateDamageMultiplier;
        _logger = logger;
    }

    /// <summary>True from a successful <see cref="TryActivate"/> until <see cref="Shutdown"/>. A fault stops routing but not this.</summary>
    public bool IsActive => _active;

    /// <summary>
    /// Decides whether the role acts in this mission and, if so, publishes the snapshot. Cheap checks run first, so a field
    /// battle or a tournament reads no gate and no race. Reads the mission once; call it from <c>AfterStart</c>, inside a try.
    /// </summary>
    public bool TryActivate(bool roleEnabled)
    {
        var scene = _mission.SceneName;
        var isSiege = _mission.IsSiegeBattle;
        var hasSallyOut = _mission.HasSallyOutController;
        var isClient = _mission.IsClientOrReplay;

        var early = CreatureSiegeRules.Activate(isSiege, hasSallyOut, isClient, roleEnabled, hasCreatureRaces: true, hasOuterGate: true);
        if (early != ActivationVerdict.Active)
        {
            LogInert(early, scene);
            return false;
        }

        var raceIds = OversizedCreatureRaces.ResolveRaceIds(_raceManager);
        var outer = CreatureSiegeRules.ResolveGate(_mission.OuterGateCandidates());
        var inner = CreatureSiegeRules.ResolveGate(_mission.InnerGateCandidates());

        var verdict = CreatureSiegeRules.Activate(isSiege, hasSallyOut, isClient, roleEnabled, raceIds.Length > 0, outer != null);
        if (verdict != ActivationVerdict.Active)
        {
            LogInert(verdict, scene);
            return false;
        }

        var snapshot = new CreatureSiegeSnapshot(_mission.MissionToken, CreatureSiegeRules.BuildRaceMask(raceIds),
            _gateDamageMultiplier, new[] { outer!.DestructionComponent, inner?.DestructionComponent });

        _token = snapshot.MissionToken;
        _isCreatureRace = snapshot.IsCreatureRace;
        _outer = outer;
        _inner = inner;
        CreatureSiegeSnapshot.Publish(snapshot);
        _active = true;
        return true;
    }

    /// <summary>
    /// One tick of the mission: nothing until the deployment is over, then one pass per stride. Never throws: a fault inside a
    /// pass is the fail-safe's.
    /// </summary>
    public void Tick()
    {
        if (!_active || _stopped) return;
        if (!_mission.IsDeploymentFinished) return;

        var now = _mission.Time;
        if (!(now >= _nextPassAt)) return;

        _nextPassAt = now + CreatureSiegeRules.PassStrideSeconds;
        _now = now;
        IReadOnlyList<ICreatureSiegeAgentAdapter>? creatures = null;
        try
        {
            if (!_armed) Arm();

            UpdateGates(now);
            creatures = _mission.CollectCreatures(_isCreatureRace!);
            _pass++;
            for (var i = 0; i < creatures.Count; i++)
                Process(creatures[i]);

            Sweep();
        }
        catch (Exception ex)
        {
            FailSafe(ex, creatures);
        }
    }

    /// <summary>Ends the role for this mission: clears its snapshot (by token) and stops the tick. Idempotent. Makes no native call.</summary>
    public void Shutdown()
    {
        _active = false;
        _routes.Clear();
        if (_token != null)
            CreatureSiegeSnapshot.ClearIf(_token);
    }

    // --- activation and arming ------------------------------------------------------------------------------------------

    private void LogInert(ActivationVerdict verdict, string scene)
    {
        var line = CreatureSiegeReport.Inert(verdict, scene);
        switch (verdict)
        {
            case ActivationVerdict.NotSiege:
                _logger.LogDebug(line);
                break;
            case ActivationVerdict.NoOuterGate:
                _logger.LogWarning(line);
                break;
            default:
                _logger.LogInfo(line);
                break;
        }
    }

    // The first armed pass: the ids, the anchors and the one INFO line. A fault here is the fail-safe's, so it is never retried.
    private void Arm()
    {
        var scene = _mission.SceneName;
        var outer = _outer!;
        var plan = CreatureSiegeRules.BuildExclusionIds(_mission.ReadTowers(), _mission.ReadLadderWallIds(),
            CreatureSiegeRules.MaxExcludedFaceGroups);
        _exclusion = plan.Ids.ToArray();

        foreach (var skipped in plan.Skipped)
            _logger.LogWarning(CreatureSiegeReport.SkippedFace(skipped, scene));
        if (plan.WasTruncated)
            _logger.LogWarning(CreatureSiegeReport.Truncated(plan, scene, CreatureSiegeRules.MaxExcludedFaceGroups));

        var (insideX, insideY) = CreatureSiegeRules.InsideOuterGatePoint(outer);
        var insideZ = FiniteFloatValidator.IsFinite(insideX) && FiniteFloatValidator.IsFinite(insideY)
            ? _mission.GroundHeight(insideX, insideY, outer.OriginZ)
            : float.NaN;
        _holdAnchors = CreatureSiegeRules.HoldGateAnchors(outer, _inner, insideZ);
        _courtyardAnchors = CreatureSiegeRules.CourtyardAnchors(outer, _inner, insideZ);

        var outerState = _mission.ReadGate(outer.Handle);
        var innerState = _inner == null ? default : _mission.ReadGate(_inner.Handle);
        _logger.LogInfo(CreatureSiegeReport.Activation(scene, outer, outerState, _inner, innerState, _gateDamageMultiplier, plan,
            FirstUsable(_holdAnchors), FirstUsable(_courtyardAnchors)));
        _armed = true;
    }

    private static AnchorCandidate? FirstUsable(IReadOnlyList<AnchorCandidate> anchors)
    {
        foreach (var anchor in anchors)
        {
            if (anchor.HeightValid)
                return anchor;
        }

        return null;
    }

    // --- one pass ---------------------------------------------------------------------------------------------------------

    private void UpdateGates(float now)
    {
        var outer = _mission.ReadGate(_outer!.Handle);
        _outerTrack = CreatureSiegeRules.Track(_outerTrack, outer.Destroyed, outer.Open, now);
        _outerPassable = CreatureSiegeRules.IsPassable(_outerTrack, now);

        if (_inner != null)
        {
            var inner = _mission.ReadGate(_inner.Handle);
            _innerTrack = CreatureSiegeRules.Track(_innerTrack, inner.Destroyed, inner.Open, now);
            _innerPassable = CreatureSiegeRules.IsPassable(_innerTrack, now);
        }

        var ram = _mission.ReadRam();
        if (ram.IsUsed)
            _ramLastUsed = now;
        _ramWorking = CreatureSiegeRules.RamWorking(ram.Present, ram.Deactivated, ram.UserCount, now - _ramLastUsed);
    }

    private void Process(ICreatureSiegeAgentAdapter agent)
    {
        var key = agent.Identity;
        if (!_routes.TryGetValue(key, out var route))
        {
            route = new Route();
            _routes.Add(key, route);
        }

        route.Seen = _pass;

        var isAIControlled = agent.IsAIControlled;
        if (isAIControlled && !route.Excluded)
        {
            for (var i = 0; i < _exclusion.Length; i++)
                agent.ExcludeFace(_exclusion[i]);
            route.Excluded = true;
        }

        if (route.Excluded && !_warnedQueue && agent.IsInLadderQueue)
        {
            _warnedQueue = true;
            _logger.LogWarning(CreatureSiegeReport.LadderQueue(_mission.SceneName, agent.Position));
        }

        var decision = CreatureSiegeRules.DecideRole(InputsOf(agent, isAIControlled));

        // A failed placement is remembered only for the role that failed: any other decision starts afresh.
        if (route.FailedRole != decision.Role)
            route.FailedRole = SiegeRole.Release;

        if (decision.Role == SiegeRole.Release)
        {
            ReleaseIfRouted(agent, route, decision.Reason);
            return;
        }

        if (route.Role == decision.Role)
            Maintain(agent, route);
        else
            Enter(agent, route, decision);
    }

    private RoleInputs InputsOf(ICreatureSiegeAgentAdapter agent, bool isAIControlled)
    {
        var innerPresent = _inner != null;
        if (!isAIControlled)
            return new RoleInputs(false, false, SiegeSide.None, SiegeFormationState.None, _outerPassable, innerPresent, _innerPassable, false, _ramWorking);

        var side = agent.Side;
        var past = side == SiegeSide.Attacker && !_outerPassable && IsPastOuterGate(agent);
        return new RoleInputs(true, agent.IsFleeing, side, agent.Formation, _outerPassable, innerPresent, _innerPassable, past, _ramWorking);
    }

    private bool IsPastOuterGate(ICreatureSiegeAgentAdapter agent)
    {
        var outer = _outer!;
        var at = agent.Position;
        return CreatureSiegeRules.IsPastOuterGate(agent.NavigationFaceId, at.X, at.Y, outer.OriginX, outer.OriginY, outer.ForwardX,
            outer.ForwardY);
    }

    // --- roles ------------------------------------------------------------------------------------------------------------

    private void Enter(ICreatureSiegeAgentAdapter agent, Route route, RoleDecision decision)
    {
        var role = decision.Role;

        // The path queries are the cost: a creature that found no place for this role a few passes ago is not asked again yet.
        if (route.FailedRole == role && _pass - route.FailedPass < CreatureSiegeRules.PlaceRetryPasses) return;

        var slotIndex = _slotCounters[(int)role] % CreatureSiegeRules.SlotsPerRole;
        if (!TryPlace(agent, role, slotIndex, out var placement))
        {
            route.FailedRole = role;
            route.FailedPass = _pass;
            WarnNoPlace(role);
            ReleaseIfRouted(agent, route, decision.Reason);
            return;
        }

        route.FailedRole = SiegeRole.Release;

        _slotCounters[(int)role]++;

        // Leaving a strike for anything but another strike clears the attack target first: a held creature that kept it would
        // stand at its hold swinging at a gate it no longer faces.
        if (IsStrike(route.Role) && !IsStrike(role))
            agent.ClearCombatTarget();

        // Flagged routed BEFORE the engine is asked, so a throw part-way through leaves a creature the fail-safe releases.
        route.Role = role;
        route.Placement = placement;
        Apply(agent, role, placement);
        LogRole(agent, role, decision.Reason, placement.Slot, placement.Point);
    }

    private void Maintain(ICreatureSiegeAgentAdapter agent, Route route)
    {
        var hasPosition = agent.HasScriptedPosition;

        // Only a strike has an attack target to lose: asking a holder for one is a native call for nothing.
        var attacking = !IsStrike(route.Role) || agent.IsAttackingEntity;
        if (!CreatureSiegeRules.NeedsReapply(route.Role, hasPosition, attacking)) return;

        Apply(agent, route.Role, route.Placement);
        if (_roleLog.TryAcquire(_now, out var suppressed))
            _logger.LogDebug(CreatureSiegeReport.Reapplied(route.Role, hasPosition, attacking, suppressed));
    }

    private static void Apply(ICreatureSiegeAgentAdapter agent, SiegeRole role, Placement placement)
    {
        if (IsStrike(role))
            agent.Strike(placement.Point, placement.FaceX, placement.FaceY, placement.Gate!);
        else
            agent.Hold(placement.Point, placement.FaceX, placement.FaceY);
    }

    private void ReleaseIfRouted(ICreatureSiegeAgentAdapter agent, Route route, RoleReason reason)
    {
        if (route.Role == SiegeRole.Release) return;

        agent.Release();
        route.Role = SiegeRole.Release;
        LogRole(agent, SiegeRole.Release, reason, -1, null);
    }

    private static bool IsStrike(SiegeRole role) => role == SiegeRole.StrikeOuter || role == SiegeRole.StrikeInner;

    private void LogRole(ICreatureSiegeAgentAdapter agent, SiegeRole role, RoleReason reason, int slot, SiegePoint? target)
    {
        // The line is built only when the throttle lets it through, so a refused one costs nothing.
        if (_roleLog.TryAcquire(_now, out var suppressed))
            _logger.LogDebug(CreatureSiegeReport.RoleChange(role, reason, slot, target, agent.Position, agent.IsInLadderQueue, suppressed));
    }

    private void WarnNoPlace(SiegeRole role)
    {
        var scene = _mission.SceneName;
        if (role == SiegeRole.HoldGate || role == SiegeRole.HoldCourtyard)
        {
            if (_warnedNoAnchor) return;
            _warnedNoAnchor = true;
            _logger.LogWarning(CreatureSiegeReport.NoValidAnchor(scene, role));
        }
        else
        {
            if (_warnedNoSlot) return;
            _warnedNoSlot = true;
            _logger.LogWarning(CreatureSiegeReport.NoReachableSlot(scene, role));
        }
    }

    // --- places -----------------------------------------------------------------------------------------------------------

    private bool TryPlace(ICreatureSiegeAgentAdapter agent, SiegeRole role, int slotIndex, out Placement placement)
    {
        switch (role)
        {
            case SiegeRole.StrikeOuter:
            case SiegeRole.StandOff:
                return TryGateSlot(agent, _outer!, role, slotIndex, out placement);
            case SiegeRole.StrikeInner:
                return TryGateSlot(agent, _inner!, role, slotIndex, out placement);
            case SiegeRole.HoldGate:
                return TryHold(agent, _holdAnchors, 1, slotIndex, out placement);
            case SiegeRole.HoldCourtyard:
                return TryHold(agent, _courtyardAnchors, -1, slotIndex, out placement);
            default:
                placement = default;
                return false;
        }
    }

    // A slot at a gate on the creature's own side of it, falling back to the centre slot of that side. Never the far side of a
    // shut gate: the path query only compares navmesh islands, and islands merge when ladders go up, so a "same island" answer
    // for a slot behind the gate says nothing about the gate being open. Every slot is checked from the creature itself, because
    // the answer depends on which island the asker stands on; nothing is cached for that reason.
    private bool TryGateSlot(ICreatureSiegeAgentAdapter agent, SiegeGateReading gate, SiegeRole role, int slotIndex, out Placement placement)
    {
        var at = agent.Position;
        var side = CreatureSiegeRules.SideOfGate(at.X, at.Y, gate.OriginX, gate.OriginY, gate.ForwardX, gate.ForwardY);

        for (var attempt = 0; attempt < 2; attempt++)
        {
            // Attempt 0 is the creature's own slot, attempt 1 the centre slot; for the centre slot itself they are the same.
            if (attempt == 1 && slotIndex == 0) break;

            var index = attempt == 0 ? slotIndex : 0;
            var slot = role == SiegeRole.StandOff
                ? CreatureSiegeRules.StandOffSlot(gate.OriginX, gate.OriginY, gate.ForwardX, gate.ForwardY, side, index)
                : CreatureSiegeRules.StrikeSlot(gate.OriginX, gate.OriginY, gate.ForwardX, gate.ForwardY, side, index);
            if (!slot.IsValid) continue;

            var point = new SiegePoint(slot.X, slot.Y, gate.OriginZ);
            if (!agent.PathExists(point)) continue;

            placement = new Placement(point, slot.FaceX, slot.FaceY, IsStrike(role) ? gate.Handle : null, slotIndex);
            return true;
        }

        placement = default;
        return false;
    }

    // The first anchor that is ground level and that this creature can walk to, then its slot around it: the anchor itself for
    // the first creature, and a spread slot for the rest when that slot can be walked to too.
    private bool TryHold(ICreatureSiegeAgentAdapter agent, IReadOnlyList<AnchorCandidate> anchors, int facingSign, int slotIndex,
        out Placement placement)
    {
        var outer = _outer!;
        foreach (var candidate in anchors)
        {
            if (!candidate.HeightValid || !agent.PathExists(candidate.Point)) continue;

            var anchor = candidate.Point;
            var centre = CreatureSiegeRules.HoldSlot(anchor.X, anchor.Y, outer.ForwardX, outer.ForwardY, facingSign, 0);
            if (!centre.IsValid) continue;

            if (slotIndex > 0)
            {
                var spread = CreatureSiegeRules.HoldSlot(anchor.X, anchor.Y, outer.ForwardX, outer.ForwardY, facingSign, slotIndex);
                var spreadPoint = new SiegePoint(spread.X, spread.Y, anchor.Z);
                if (spread.IsValid && agent.PathExists(spreadPoint))
                {
                    placement = new Placement(spreadPoint, spread.FaceX, spread.FaceY, null, slotIndex);
                    return true;
                }
            }

            placement = new Placement(anchor, centre.FaceX, centre.FaceY, null, slotIndex);
            return true;
        }

        placement = default;
        return false;
    }

    // --- the sweep and the fail-safe ----------------------------------------------------------------------------------------

    // Mark and sweep: a record not seen this pass belongs to an agent that is gone. Forgetting it makes no native call.
    private void Sweep()
    {
        _forget.Clear();
        foreach (var entry in _routes)
        {
            if (entry.Value.Seen != _pass)
                _forget.Add(entry.Key);
        }

        for (var i = 0; i < _forget.Count; i++)
            _routes.Remove(_forget[i]);
        _forget.Clear();
    }

    private void FailSafe(Exception exception, IReadOnlyList<ICreatureSiegeAgentAdapter>? creatures)
    {
        // Tick returns at once once _stopped is set, so this runs at most once per battle and logs one error.
        _stopped = true;
        _logger.LogError(CreatureSiegeReport.Fault(_mission.SceneName, exception));

        // A fault before the creatures were collected still has to release what earlier passes routed.
        if (creatures == null)
        {
            try
            {
                creatures = _mission.CollectCreatures(_isCreatureRace!);
            }
            catch (Exception)
            {
                creatures = null;
            }
        }

        if (creatures != null)
        {
            for (var i = 0; i < creatures.Count; i++)
            {
                try
                {
                    var agent = creatures[i];
                    if (_routes.TryGetValue(agent.Identity, out var route) && route.Role != SiegeRole.Release)
                    {
                        route.Role = SiegeRole.Release;
                        agent.Release();
                    }
                }
                catch (Exception)
                {
                    // Each release has its own try: one agent the engine refuses must not strand the others.
                }
            }
        }

        _routes.Clear();
    }

    // --- records ----------------------------------------------------------------------------------------------------------

    private sealed class Route
    {
        public bool Excluded;
        public SiegeRole Role = SiegeRole.Release;
        public Placement Placement;
        public int Seen;

        // The role whose placement failed last, and the pass it failed on; Release means none.
        public SiegeRole FailedRole = SiegeRole.Release;
        public int FailedPass;
    }

    private readonly record struct Placement(SiegePoint Point, float FaceX, float FaceY, object? Gate, int Slot);
}
