using System;
using System.Collections.Generic;
using TaleWorlds.Library;
using TAOM.Adapters;
using TAOM.Features.SmartCavalryAI.Models;

namespace TAOM.Features.SmartCavalryAI;

/// <summary>
/// State machine driver for the SmartCavalryAI feature. One per IoC container (Singleton).
/// Per-formation state is keyed by <c>IFormationAdapter.FormationKey</c> and lives until
/// <see cref="OnMissionEnd"/> clears it.
///
/// <para>Differences from the v1.4 decompile baseline (intentional, port-driven):</para>
/// <list type="bullet">
///   <item>Reform alignment uses <c>ChargeFormationStrictness</c>, not a hardcoded 0.5f.
///         The original was a latent bug — settings.strictness=0.9 still reformed at 0.5.</item>
///   <item>Per-frame HUD spam is removed; routed via the <c>SmartCavalryDebug</c> flag in
///         the calling MissionBehavior + postfix.</item>
///   <item>No reflection — <c>Formation.SetPositioning</c> and <c>Agent.SetMovementDirection</c>
///         are public in v1.3.15.</item>
/// </list>
/// </summary>
public sealed class CavalryChargeService : ICavalryChargeService
{
    private const float ChargingDistanceThreshold = 10f;          // 10m (matches .Length comparison at L190)
    private const float ChargingDistanceThresholdSquared = 100f;  // (10m)^2 — sqrt avoided
    private const float ReroutingWaypointReachedDistanceSquared = 100f;  // 10m

    private readonly ISmartCavalryAISettingsProvider _settings;
    private readonly ICavalryPathPlanner _pathPlanner;

    private readonly Dictionary<object, CavalryFormationState> _states = new();

    public CavalryChargeService(
        ISmartCavalryAISettingsProvider settings,
        ICavalryPathPlanner pathPlanner)
    {
        _settings = settings;
        _pathPlanner = pathPlanner;
    }

    public CavalryState GetState(object formationKey)
    {
        if (formationKey == null) return CavalryState.Idle;
        return _states.TryGetValue(formationKey, out var state) ? state.State : CavalryState.Idle;
    }

    public void OnMissionEnd()
    {
        _states.Clear();
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
        if (!cav.RepresentativeIsCavalry) return;

        // Idempotency guard: if the formation is already mid-state-machine (Forming, Charging,
        // PassingThrough, Reforming, Rerouting), a fresh Charge order would otherwise reset
        // the line-charge positioning and throw away alignment progress. The user's intent on
        // a double-tap is "I want to charge that thing"; we're already executing that.
        if (_states.TryGetValue(cav.FormationKey, out var existing)
            && existing.State != CavalryState.Idle)
        {
            return;
        }

        // Decide: reroute or initiate line charge?
        var avoidFriendlies = _settings.AvoidFriendlies;
        var lineSpacing = _settings.ChargeLineSpacing;

        if (avoidFriendlies)
        {
            var friendlies = battlefield.GetFriendlyFormationsExcluding(cav.FormationKey);
            if (_pathPlanner.TryGetReroutePoint(
                    cav.CurrentPosition, targetPosition.AsVec2,
                    friendlies, out var waypoint))
            {
                BeginReroute(cav, commands, battlefield, targetToken, waypoint);
                return;
            }
        }

        InitiateLineCharge(cav, commands, battlefield, targetToken, targetPosition, lineSpacing, currentMissionTime);
    }

    private void BeginReroute(
        IFormationAdapter cav,
        ICavalryCommandAdapter commands,
        IBattlefieldQueryAdapter battlefield,
        object targetToken,
        Vec2 waypoint)
    {
        var state = GetOrCreateState(cav.FormationKey);
        state.State = CavalryState.Rerouting;
        state.OriginalChargeTargetToken = targetToken;
        state.ReroutePoint = waypoint;

        var groundHeight = battlefield.GetGroundHeightAtPosition(new Vec3(waypoint.x, waypoint.y, 0f, -1f));
        commands.IssueMoveTo(waypoint, groundHeight);
    }

    private void InitiateLineCharge(
        IFormationAdapter cav,
        ICavalryCommandAdapter commands,
        IBattlefieldQueryAdapter battlefield,
        object targetToken,
        Vec3 targetPosition,
        float lineSpacing,
        float currentMissionTime)
    {
        // Direction sanity check BEFORE state mutation — matches decompile (early-return on
        // zero-length direction). If we set state then bail, the formation enters Forming
        // without a charge line applied, which would cause an undefined alignment check.
        var dir2d = (targetPosition.AsVec2 - cav.CurrentPosition);
        if (dir2d.LengthSquared < 1f) return;

        var state = GetOrCreateState(cav.FormationKey);
        state.State = CavalryState.Forming;
        state.ChargeTarget = targetPosition;
        state.ChargeStartPosition = new Vec3(cav.CurrentPosition.x, cav.CurrentPosition.y, 0f, -1f);
        state.ChargeStartTime = currentMissionTime;
        state.IsCoordinatedCharge = true;
        state.OriginalChargeTargetToken = targetToken;

        var dir = dir2d.Normalized();
        var spawn2d = cav.CurrentPosition + dir * 5f;
        var groundHeight = battlefield.GetGroundHeightAtPosition(new Vec3(spawn2d.x, spawn2d.y, 0f, -1f));
        var spawn3d = new Vec3(spawn2d.x, spawn2d.y, groundHeight, -1f);
        var spacing = Math.Max(1, (int)Math.Round(lineSpacing));
        commands.ApplyChargeLine(spawn3d, dir, spacing);
        commands.IssueStop();
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
        if (!_states.TryGetValue(cav.FormationKey, out var state)) return;

        switch (state.State)
        {
            case CavalryState.Forming:
                UpdateForming(cav, commands, state);
                break;
            case CavalryState.Charging:
                UpdateCharging(cav, state);
                break;
            case CavalryState.PassingThrough:
                UpdatePassingThrough(cav, commands, state);
                break;
            case CavalryState.Reforming:
                UpdateReforming(cav, state);
                break;
            case CavalryState.Rerouting:
                UpdateRerouting(cav, commands, state);
                break;
        }
    }

    private void UpdateForming(IFormationAdapter cav, ICavalryCommandAdapter commands, CavalryFormationState state)
    {
        var strictness = _settings.ChargeFormationStrictness;
        if (!cav.IsAligned(strictness)) return;

        // If the target was destroyed between HandleChargeOrder and this tick, skip the
        // charge entirely and reset to Idle. Otherwise the formation enters Charging without
        // a meaningful order and stalls in front of the (now-empty) target position until
        // PassingThrough/Reforming run to completion against ChargeTarget that no enemy holds.
        if (state.OriginalChargeTargetToken == null
            || !commands.IsTargetAlive(state.OriginalChargeTargetToken))
        {
            state.State = CavalryState.Idle;
            state.OriginalChargeTargetToken = null;
            return;
        }

        state.State = CavalryState.Charging;
        commands.IssueChargeToTarget(state.OriginalChargeTargetToken);
    }

    private static void UpdateCharging(IFormationAdapter cav, CavalryFormationState state)
    {
        var here3d = new Vec3(cav.CurrentPosition.x, cav.CurrentPosition.y, 0f, -1f);
        var diff = here3d - state.ChargeTarget;
        if (diff.LengthSquared < ChargingDistanceThresholdSquared)
        {
            state.State = CavalryState.PassingThrough;
        }
    }

    private void UpdatePassingThrough(IFormationAdapter cav, ICavalryCommandAdapter commands, CavalryFormationState state)
    {
        var reformDistance = _settings.ReformDistanceAfterCharge;
        var here3d = new Vec3(cav.CurrentPosition.x, cav.CurrentPosition.y, 0f, -1f);
        var diff = here3d - state.ChargeTarget;
        if (diff.LengthSquared > reformDistance * reformDistance)
        {
            state.State = CavalryState.Reforming;
            commands.IssueStop();
        }
    }

    private void UpdateReforming(IFormationAdapter cav, CavalryFormationState state)
    {
        // FIX vs decompile baseline: the original used a hardcoded 0.5f tolerance here,
        // ignoring the user's strictness preference. We honor the setting.
        var strictness = _settings.ChargeFormationStrictness;
        if (cav.IsAligned(strictness))
        {
            state.State = CavalryState.Idle;
            state.IsCoordinatedCharge = false;
        }
    }

    private static void UpdateRerouting(IFormationAdapter cav, ICavalryCommandAdapter commands, CavalryFormationState state)
    {
        var offset = cav.CurrentPosition - state.ReroutePoint;
        if (offset.LengthSquared >= ReroutingWaypointReachedDistanceSquared) return;

        // Reached the waypoint. If the original target is still alive, re-issue the charge.
        if (state.OriginalChargeTargetToken != null && commands.IsTargetAlive(state.OriginalChargeTargetToken))
        {
            commands.IssueChargeToTarget(state.OriginalChargeTargetToken);
        }

        state.State = CavalryState.Idle;
        state.OriginalChargeTargetToken = null;
    }

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
