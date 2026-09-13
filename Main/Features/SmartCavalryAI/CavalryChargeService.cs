using System;
using System.Collections.Generic;
using TaleWorlds.Library;
using TAOM.Adapters;
using TAOM.Core.Logging;
using TAOM.Features.SmartCavalryAI.Models;

namespace TAOM.Features.SmartCavalryAI;

/// <summary>
/// State machine driver for the SmartCavalryAI feature. One per IoC container (Singleton).
/// Per-formation state is keyed by <c>IFormationAdapter.FormationKey</c> and lives until
/// <see cref="OnMissionEnd"/> clears it.
///
/// <para>The cycle a player's F3 starts (#586):</para>
/// <code>
/// Forming (Move to a line 5 m ahead) --aligned or MaxLineUpSeconds--> Charging (ChargeToTarget)
///   --contact along the charge direction--> PassingThrough (Move to ReformDistance past the target)
///   --arrived--> Reforming (hold the line facing the enemy) --aligned or MaxLineUpSeconds--> Forming ...
/// Rerouting (Move around friendly infantry) --arrived or timeout--> Forming
/// </code>
///
/// <para>Two rules keep riders from ever standing still. Every hold state has a dwell budget,
/// and every exit that gives up hands the formation back to a vanilla Charge
/// (<see cref="HandOff"/>). Line-ups are Moves, never Stops: under a Stop the engine holds each
/// rider where it stands and ignores the arrangement (Formation.cs:1262, v1.4.8), so a Stop can
/// never form a line. A second F3 mid-cycle means "charge now". Any other order, or the team AI
/// taking the formation, cancels the cycle so that order stands.</para>
/// </summary>
public sealed class CavalryChargeService : ICavalryChargeService
{
    // Contact: the centroid is within this many metres of the live target, measured along the
    // charge direction fixed at launch, so a flank charge registers when it crosses the plane
    // through the enemy's centre rather than when it reaches the centre itself.
    private const float ContactDistance = 10f;
    // Arrival at a Move destination (reroute waypoint, reform point). 10 m, squared.
    private const float ArrivalDistanceSquared = 100f;
    // The line-up position sits this far ahead of the centroid, toward the target.
    private const float LineOffsetAhead = 5f;
    // Hold states may not keep riders still forever. Forming and Reforming spend the MCM
    // line-up budget; these two are fixed.
    private const float PassThroughTimeoutSeconds = 10f;
    private const float RerouteTimeoutSeconds = 12f;

    private readonly ISmartCavalryAISettingsProvider _settings;
    private readonly ICavalryPathPlanner _pathPlanner;
    private readonly IModLogger _logger;

    private readonly Dictionary<object, CavalryFormationState> _states = new();
    // #155: mirrors FormationLayoutService._lock. Patch31's team filter keeps enemy-team AI
    // threads out of this service today; the lock keeps that from being load-bearing.
    private readonly object _lock = new();

    public CavalryChargeService(
        ISmartCavalryAISettingsProvider settings,
        ICavalryPathPlanner pathPlanner,
        IModLogger logger)
    {
        _settings = settings;
        _pathPlanner = pathPlanner;
        _logger = logger;
    }

    public CavalryState GetState(object formationKey)
    {
        if (formationKey == null) return CavalryState.Idle;
        lock (_lock)
        {
            return _states.TryGetValue(formationKey, out var state) ? state.State : CavalryState.Idle;
        }
    }

    public void OnMissionEnd()
    {
        lock (_lock)
        {
            _states.Clear();
        }
    }

    public bool HasActiveCycles
    {
        get
        {
            lock (_lock)
            {
                foreach (var state in _states.Values)
                {
                    if (state.State != CavalryState.Idle) return true;
                }
                return false;
            }
        }
    }

    public void CancelCharge(object formationKey)
    {
        if (formationKey == null) return;
        lock (_lock)
        {
            if (!_states.TryGetValue(formationKey, out var state) || state.State == CavalryState.Idle) return;
            Cancel(state, "formation", "another order took the formation, or it emptied");
        }
    }

    public void HandleChargeOrder(
        IFormationAdapter cav,
        ICavalryCommandAdapter commands,
        IBattlefieldQueryAdapter battlefield,
        object targetToken,
        Vec3 targetPosition,
        float currentMissionTime)
    {
        if (cav == null || commands == null || battlefield == null) return;
        if (cav.FormationKey == null) return;
        if (!_settings.IsEnabled) return;
        if (!battlefield.HasPlayerTeam) return;
        // Open-field-only: never issue coordinated line charges (native SetPositioning /
        // SetMovementOrder) in a siege, sally-out, hideout, or any non-field mission. Re-entering
        // native formation code while the engine is still finalizing siege deployment can fault.
        if (!battlefield.IsFieldBattle) return;
        if (!cav.RepresentativeIsCavalry) return;
        // Only orders the player gives start a cycle; the team AI's formations keep vanilla.
        if (cav.IsAIControlled) return;

        var target2d = targetPosition.AsVec2;
        lock (_lock)
        {
            if (_states.TryGetValue(cav.FormationKey, out var existing) && existing.State != CavalryState.Idle)
            {
                ChargeNow(cav, commands, existing, targetToken, target2d, currentMissionTime);
                return;
            }

            StartCycle(cav, commands, battlefield, targetToken, target2d, currentMissionTime, allowReroute: true);
        }
    }

    public void RetargetCycle(
        IFormationAdapter cav,
        ICavalryCommandAdapter commands,
        IBattlefieldQueryAdapter battlefield,
        object targetToken,
        Vec3 targetPosition,
        float currentMissionTime)
    {
        if (cav == null || commands == null || battlefield == null || targetToken == null) return;
        if (cav.FormationKey == null) return;
        if (!_settings.IsEnabled || !battlefield.IsFieldBattle) return;

        var target2d = targetPosition.AsVec2;
        lock (_lock)
        {
            if (!_states.TryGetValue(cav.FormationKey, out var state) || state.State == CavalryState.Idle) return;
            if (ReferenceEquals(state.TargetToken, targetToken)) return;

            switch (state.State)
            {
                case CavalryState.Forming:
                    // The line was drawn at the nearest enemy; redraw it at the one the player named.
                    if (!InitiateLineCharge(cav, commands, battlefield, targetToken, target2d, currentMissionTime))
                    {
                        state.TargetToken = targetToken;
                    }
                    break;
                case CavalryState.Charging:
                    state.TargetToken = targetToken;
                    var toTarget = target2d - cav.CurrentPosition;
                    if (toTarget.LengthSquared >= 1f) state.ChargeDirection = toTarget.Normalized();
                    commands.IssueChargeToTarget(targetToken);
                    _logger.LogInfo($"[SmartCavalryAI] {Describe(cav)}: charging the formation the player named instead");
                    break;
                default:
                    state.TargetToken = targetToken;
                    break;
            }
        }
    }

    public void Tick(
        IFormationAdapter cav,
        ICavalryCommandAdapter commands,
        IBattlefieldQueryAdapter battlefield,
        float dt,
        float currentMissionTime)
    {
        if (cav == null || commands == null) return;
        if (cav.FormationKey == null) return;
        // Open-field-only (mirrors HandleChargeOrder): freeze the machine outside a field battle.
        if (battlefield == null || !battlefield.IsFieldBattle) return;

        lock (_lock)
        {
            if (!_states.TryGetValue(cav.FormationKey, out var state)) return;
            if (state.State == CavalryState.Idle) return;

            // Ownership first: a formation the team AI now commands gets no order from us at all.
            if (cav.IsAIControlled)
            {
                Cancel(state, Describe(cav), "the team AI took the formation");
                return;
            }

            // Riders dismounted or lost their mounts: this is no longer a cavalry charge. Hand the
            // formation back rather than leaving it on a Move only this machine would lift.
            if (!cav.RepresentativeIsCavalry)
            {
                HandOff(state, commands, cav, "the formation is no longer cavalry");
                return;
            }

            // The toggle flipped off mid-cycle: same reasoning, give it the charge the player asked for.
            if (!_settings.IsEnabled)
            {
                HandOff(state, commands, cav, "feature disabled mid-cycle");
                return;
            }

            switch (state.State)
            {
                case CavalryState.Forming:
                    UpdateForming(cav, commands, battlefield, state, currentMissionTime);
                    break;
                case CavalryState.Charging:
                    UpdateCharging(cav, commands, battlefield, state, currentMissionTime);
                    break;
                case CavalryState.PassingThrough:
                    UpdatePassingThrough(cav, commands, state, currentMissionTime);
                    break;
                case CavalryState.Reforming:
                    UpdateReforming(cav, commands, battlefield, state, currentMissionTime);
                    break;
                case CavalryState.Rerouting:
                    UpdateRerouting(cav, commands, battlefield, state, currentMissionTime);
                    break;
            }
        }
    }

    // ---------------------------------------------------------------- entries

    /// <summary>The player pressed F3 again mid-cycle: they want the charge now, not a
    /// tighter line. Re-point at the new target and go straight to Charging.</summary>
    private void ChargeNow(
        IFormationAdapter cav,
        ICavalryCommandAdapter commands,
        CavalryFormationState state,
        object targetToken,
        Vec2 targetPosition,
        float now)
    {
        state.TargetToken = targetToken;
        var toTarget = targetPosition - cav.CurrentPosition;
        if (toTarget.LengthSquared >= 1f) state.ChargeDirection = toTarget.Normalized();
        Enter(state, CavalryState.Charging, now, cav, "charge re-issued, skipping the line-up");
        commands.IssueChargeToTarget(targetToken);
    }

    private void BeginReroute(
        IFormationAdapter cav,
        ICavalryCommandAdapter commands,
        IBattlefieldQueryAdapter battlefield,
        object targetToken,
        Vec2 targetPosition,
        Vec2 waypoint,
        float now)
    {
        var state = GetOrCreateState(cav.FormationKey);
        state.TargetToken = targetToken;
        state.ReroutePoint = waypoint;
        var toTarget = targetPosition - cav.CurrentPosition;
        if (toTarget.LengthSquared >= 1f) state.ChargeDirection = toTarget.Normalized();
        Enter(state, CavalryState.Rerouting, now, cav, "friendly formation on the charge line");

        var height = battlefield.GetGroundHeightAtPosition(new Vec3(waypoint.x, waypoint.y, 0f, -1f));
        if (!commands.IssueMoveTo(waypoint, height))
        {
            HandOff(state, commands, cav, "the engine refused the reroute move");
        }
    }

    /// <summary>Draw a line 5 m ahead toward the target and Move the riders into it. Returns
    /// false, touching nothing, when the target sits on top of the formation and there is no
    /// direction to line up along.</summary>
    private bool InitiateLineCharge(
        IFormationAdapter cav,
        ICavalryCommandAdapter commands,
        IBattlefieldQueryAdapter battlefield,
        object targetToken,
        Vec2 targetPosition,
        float now)
    {
        var toTarget = targetPosition - cav.CurrentPosition;
        // Positive requirement: a NaN position must not reach Normalized() and become the direction.
        if (!(toTarget.LengthSquared >= 1f)) return false;
        var dir = toTarget.Normalized();

        var state = GetOrCreateState(cav.FormationKey);
        state.TargetToken = targetToken;
        state.ChargeDirection = dir;

        var linePosition = cav.CurrentPosition + dir * LineOffsetAhead;
        var height = battlefield.GetGroundHeightAtPosition(new Vec3(linePosition.x, linePosition.y, 0f, -1f));
        commands.ApplyChargeLine(new Vec3(linePosition.x, linePosition.y, height, -1f), dir, LineSpacing());
        Enter(state, CavalryState.Forming, now, cav, "lining up");
        if (!commands.IssueMoveTo(linePosition, height))
        {
            HandOff(state, commands, cav, "the engine refused the line-up move");
        }
        return true;
    }

    // ---------------------------------------------------------------- per-state ticks

    private void UpdateForming(
        IFormationAdapter cav,
        ICavalryCommandAdapter commands,
        IBattlefieldQueryAdapter battlefield,
        CavalryFormationState state,
        float now)
    {
        if (!TargetAlive(commands, state))
        {
            StartNextLineCharge(cav, commands, battlefield, state, now, "charge target gone while lining up", allowReroute: true);
            return;
        }

        var aligned = cav.IsAligned(_settings.ChargeFormationStrictness);
        var budgetSpent = now - state.StateEnteredTime >= _settings.MaxLineUpSeconds;
        if (!aligned && !budgetSpent) return;

        Enter(state, CavalryState.Charging, now, cav, aligned ? "line formed" : "line-up budget spent, charging anyway");
        commands.IssueChargeToTarget(state.TargetToken!);
    }

    private void UpdateCharging(
        IFormationAdapter cav,
        ICavalryCommandAdapter commands,
        IBattlefieldQueryAdapter battlefield,
        CavalryFormationState state,
        float now)
    {
        if (!TargetAlive(commands, state))
        {
            // Vanilla degrades a ChargeToTarget on an emptied formation into a hold at the order
            // position (MovementOrder.cs:536-538), so re-point the charge before that shows.
            if (battlefield.TryGetNearestEnemyFormation(cav.FormationKey, out var token, out var position) && token != null)
            {
                state.TargetToken = token;
                var toNew = position - cav.CurrentPosition;
                if (toNew.LengthSquared >= 1f) state.ChargeDirection = toNew.Normalized();
                commands.IssueChargeToTarget(token);
                _logger.LogInfo($"[SmartCavalryAI] {Describe(cav)}: charge target gone, charging the nearest enemy instead");
                return;
            }
            HandOff(state, commands, cav, "charge target gone, no enemy formation left");
            return;
        }

        if (!commands.TryGetTargetPosition(state.TargetToken!, out var targetLive)) return;
        var along = Vec2.DotProduct(targetLive - cav.CurrentPosition, state.ChargeDirection);
        // Positive requirement: a NaN keeps the formation on its charge order, which is vanilla.
        if (!(along < ContactDistance)) return;

        // Ride straight on: past the target's plane, past however deep its riders extend beyond
        // that plane along our axis (a column, a line that turned sideways), then ReformDistance
        // more. Never backwards. The depth read is a positive requirement so a NaN adds nothing.
        var reformDistance = _settings.ReformDistanceAfterCharge;
        var depthRead = commands.GetTargetDepthAlong(state.TargetToken!, state.ChargeDirection);
        var depth = depthRead > 0f ? depthRead : 0f;
        var reformPoint = cav.CurrentPosition + state.ChargeDirection * (Math.Max(along, 0f) + depth + reformDistance);
        var height = battlefield.GetGroundHeightAtPosition(new Vec3(reformPoint.x, reformPoint.y, 0f, -1f));
        commands.ApplyChargeLine(new Vec3(reformPoint.x, reformPoint.y, height, -1f), state.ChargeDirection * -1f, LineSpacing());
        state.ReformPoint = reformPoint;
        Enter(state, CavalryState.PassingThrough, now, cav, "contact, riding through to the reform point");
        if (!commands.IssueMoveTo(reformPoint, height))
        {
            HandOff(state, commands, cav, "the engine refused the reform move");
        }
    }

    private void UpdatePassingThrough(
        IFormationAdapter cav,
        ICavalryCommandAdapter commands,
        CavalryFormationState state,
        float now)
    {
        if ((cav.CurrentPosition - state.ReformPoint).LengthSquared < ArrivalDistanceSquared)
        {
            Enter(state, CavalryState.Reforming, now, cav, "reached the reform point");
            return;
        }
        if (now - state.StateEnteredTime >= PassThroughTimeoutSeconds)
        {
            HandOff(state, commands, cav, "bogged down inside the enemy");
        }
    }

    private void UpdateReforming(
        IFormationAdapter cav,
        ICavalryCommandAdapter commands,
        IBattlefieldQueryAdapter battlefield,
        CavalryFormationState state,
        float now)
    {
        var aligned = cav.IsAligned(_settings.ChargeFormationStrictness);
        var budgetSpent = now - state.StateEnteredTime >= _settings.MaxLineUpSeconds;
        if (!aligned && !budgetSpent) return;

        StartNextLineCharge(cav, commands, battlefield, state, now, aligned ? "reformed" : "reform budget spent", allowReroute: true);
    }

    private void UpdateRerouting(
        IFormationAdapter cav,
        ICavalryCommandAdapter commands,
        IBattlefieldQueryAdapter battlefield,
        CavalryFormationState state,
        float now)
    {
        var arrived = (cav.CurrentPosition - state.ReroutePoint).LengthSquared < ArrivalDistanceSquared;
        var budgetSpent = now - state.StateEnteredTime >= RerouteTimeoutSeconds;
        if (!arrived && !budgetSpent) return;

        var reason = arrived ? "reroute waypoint reached" : "reroute budget spent";
        if (TargetAlive(commands, state) && commands.TryGetTargetPosition(state.TargetToken!, out var targetLive))
        {
            if (InitiateLineCharge(cav, commands, battlefield, state.TargetToken!, targetLive, now)) return;
            HandOff(state, commands, cav, reason + "; the enemy is on top of us, no line to draw");
            return;
        }
        StartNextLineCharge(cav, commands, battlefield, state, now, reason + ", original target gone", allowReroute: false);
    }

    // ---------------------------------------------------------------- shared exits

    /// <summary>Begin a cycle at the given target: around friendly infantry first when the planner
    /// finds some on the line (and the caller allows it), otherwise straight into the line-up. A
    /// reroute never re-plans from its own waypoint, or a planner that keeps finding the same
    /// blocker would chain Moves. False only when no line can be drawn (the target is on top of the
    /// formation); the caller decides what that means.</summary>
    private bool StartCycle(
        IFormationAdapter cav,
        ICavalryCommandAdapter commands,
        IBattlefieldQueryAdapter battlefield,
        object targetToken,
        Vec2 targetPosition,
        float now,
        bool allowReroute)
    {
        if (allowReroute && _settings.AvoidFriendlies)
        {
            var friendlies = battlefield.GetFriendlyFormationsExcluding(cav.FormationKey);
            if (_pathPlanner.TryGetReroutePoint(cav.CurrentPosition, targetPosition, friendlies, out var waypoint))
            {
                BeginReroute(cav, commands, battlefield, targetToken, targetPosition, waypoint, now);
                return true;
            }
        }
        return InitiateLineCharge(cav, commands, battlefield, targetToken, targetPosition, now);
    }

    /// <summary>Begin a new cycle at the nearest live enemy formation (rerouting around friendlies
    /// like the first one did), or hand off when there is none (or it is on top of us).</summary>
    private void StartNextLineCharge(
        IFormationAdapter cav,
        ICavalryCommandAdapter commands,
        IBattlefieldQueryAdapter battlefield,
        CavalryFormationState state,
        float now,
        string reason,
        bool allowReroute)
    {
        if (battlefield.TryGetNearestEnemyFormation(cav.FormationKey, out var token, out var position) && token != null)
        {
            if (StartCycle(cav, commands, battlefield, token, position, now, allowReroute)) return;
            HandOff(state, commands, cav, reason + "; the enemy is on top of us, no line to draw");
            return;
        }
        HandOff(state, commands, cav, reason + "; no enemy formation left");
    }

    /// <summary>Give the formation back to a vanilla Charge and forget it. Every exit that
    /// gives up on a cycle comes through here, so no exit leaves riders standing still.</summary>
    private void HandOff(CavalryFormationState state, ICavalryCommandAdapter commands, IFormationAdapter cav, string reason)
    {
        var previous = state.State;
        state.State = CavalryState.Idle;
        state.TargetToken = null;
        commands.IssueCharge();
        _logger.LogInfo($"[SmartCavalryAI] {Describe(cav)}: {previous} -> Idle, vanilla charge ({reason})");
    }

    /// <summary>Forget the formation without issuing anything: the order that displaced us stands.</summary>
    private void Cancel(CavalryFormationState state, string who, string reason)
    {
        var previous = state.State;
        state.State = CavalryState.Idle;
        state.TargetToken = null;
        _logger.LogInfo($"[SmartCavalryAI] {who}: {previous} -> Idle, cancelled ({reason})");
    }

    private void Enter(CavalryFormationState state, CavalryState next, float now, IFormationAdapter cav, string reason)
    {
        var previous = state.State;
        state.State = next;
        state.StateEnteredTime = now;
        _logger.LogInfo($"[SmartCavalryAI] {Describe(cav)}: {previous} -> {next} ({reason})");
    }

    private static bool TargetAlive(ICavalryCommandAdapter commands, CavalryFormationState state)
        => state.TargetToken != null && commands.IsTargetAlive(state.TargetToken);

    private int LineSpacing() => Math.Max(1, (int)Math.Round(_settings.ChargeLineSpacing));

    private static string Describe(IFormationAdapter cav) => $"formation {cav.FormationIndex}";

    private CavalryFormationState GetOrCreateState(object formationKey)
    {
        if (!_states.TryGetValue(formationKey, out var state))
        {
            state = new CavalryFormationState();
            _states[formationKey] = state;
        }
        return state;
    }
}
