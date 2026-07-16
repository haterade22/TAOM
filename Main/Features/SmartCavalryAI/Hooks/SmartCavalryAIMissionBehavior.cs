using System.Collections.Generic;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;
using TAOM.Adapters;
using TAOM.Adapters.Models;
using TAOM.Core.Logging;

namespace TAOM.Features.SmartCavalryAI.Hooks;

/// <summary>
/// Boundary class. Constructs adapters per cavalry formation each tick, drives the
/// cavalry charge service, and runs friendly-collision-avoidance on cavalry units.
/// All TaleWorlds type access happens here — service is ADR-007 compliant.
/// </summary>
public sealed class SmartCavalryAIMissionBehavior : MissionBehavior
{
    private const float FriendlyAvoidanceRadius = 3f;
    private const float FriendlyAvoidanceNudge = 0.25f;

    private readonly ICavalryChargeService _service;
    private readonly ISmartCavalryAISettingsProvider _settings;
    private readonly IBattlefieldQueryAdapter _battlefield;
    private readonly IModLogger _logger;

    private readonly List<NearbyAgentSnapshot> _nearbyBuffer = new();

    // Per-formation adapter cache — saves two heap allocs per cavalry formation per tick.
    // Cleared on OnEndMission. Keyed by Formation reference (stable for the mission lifetime).
    private readonly Dictionary<Formation, FormationAdapter> _cavCache = new();
    private readonly Dictionary<Formation, CavalryCommandAdapter> _commandCache = new();

    // BehaviorType=Other: this class inherits MissionBehavior (not MissionLogic) and does not
    // override MissionEnded/OnMissionResultReady, so it has no business in Mission.MissionLogics.
    // Returning Logic here caused vanilla AddMissionBehavior to do `MissionLogics.Add(this as MissionLogic)`
    // which evaluates to null and NREs the next CheckMissionEnded tick.
    public override MissionBehaviorType BehaviorType => MissionBehaviorType.Other;

    public SmartCavalryAIMissionBehavior()
    {
        _service = IoC.Resolve<ICavalryChargeService>();
        _settings = IoC.Resolve<ISmartCavalryAISettingsProvider>();
        _battlefield = IoC.Resolve<IBattlefieldQueryAdapter>();
        _logger = IoC.Resolve<IModLogger>();
    }

    public override void OnBehaviorInitialize()
    {
        base.OnBehaviorInitialize();
        if (!_settings.IsEnabled)
        {
            _logger.LogInfo("[SmartCavalryAI] disabled via MCM — patch postfix inert");
            return;
        }
        _logger.LogInfo($"[SmartCavalryAI] mission init — strictness={_settings.ChargeFormationStrictness:F2}, " +
            $"reformDistance={_settings.ReformDistanceAfterCharge:F0}, lineSpacing={_settings.ChargeLineSpacing:F1}, " +
            $"avoidFriendlies={_settings.AvoidFriendlies}");
    }

    public override void OnMissionTick(float dt)
    {
        base.OnMissionTick(dt);
        if (!_settings.IsEnabled) return;
        // Open-field-only. This gate is load-bearing at THIS level, not just in the service:
        // ApplyCollisionAvoidance below writes agent.SetMovementDirection directly, bypassing
        // ICavalryChargeService entirely, so the service-side gate cannot suppress it. Gating the
        // whole tick also skips the per-formation adapter build. Live read — team-AI type is set in
        // MissionCombatantsLogic.EarlyStart (after every OnBehaviorInitialize), so never cache it.
        if (!_battlefield.IsFieldBattle) return;

        var team = Mission.Current?.PlayerTeam;
        if (team == null) return;

        var time = Mission.Current?.CurrentTime ?? 0f;

        foreach (var formation in team.FormationsIncludingEmpty)
        {
            if (formation == null || formation.CountOfUnits == 0) continue;
            if (!_cavCache.TryGetValue(formation, out var cav))
            {
                cav = new FormationAdapter(formation);
                _cavCache[formation] = cav;
            }
            if (!cav.RepresentativeIsCavalry) continue;

            if (!_commandCache.TryGetValue(formation, out var commands))
            {
                commands = new CavalryCommandAdapter(formation);
                _commandCache[formation] = commands;
            }
            _service.Tick(cav, commands, _battlefield, dt, time);

            if (_settings.AvoidFriendlies)
            {
                ApplyCollisionAvoidance(formation, team);
            }
        }
    }

    /// <summary>Boundary work: per-mounted-unit friendly-infantry avoidance. Touches
    /// <c>Agent.SetMovementDirection</c> directly (public in v1.3.15) — no service
    /// abstraction since this is per-agent boundary work, not state-machine logic.</summary>
    private void ApplyCollisionAvoidance(Formation formation, Team playerTeam)
    {
        foreach (var unit in formation.UnitsWithoutLooseDetachedOnes)
        {
            if (unit is not Agent agent) continue;
            if (!agent.HasMount || !agent.IsActive()) continue;

            _battlefield.GetNearbyAgents(agent.Position.AsVec2, FriendlyAvoidanceRadius, _nearbyBuffer);

            var avoidanceVec = Vec2.Zero;
            var count = 0;
            foreach (var snap in _nearbyBuffer)
            {
                if (snap.Index == agent.Index) continue;
                if (snap.IsMounted) continue;
                if (!ReferenceEquals(snap.TeamKey, playerTeam)) continue;

                var diff = agent.Position.AsVec2 - snap.Position;
                if (diff.LengthSquared < 0.01f) continue;
                avoidanceVec += diff.Normalized();
                count++;
            }

            if (count == 0) continue;
            var blended = agent.GetMovementDirection() + avoidanceVec.Normalized() * FriendlyAvoidanceNudge;
            var newDir = blended.Normalized();
            agent.SetMovementDirection(in newDir);
        }
    }

    protected override void OnEndMission()
    {
        base.OnEndMission();
        _service.OnMissionEnd();
        _cavCache.Clear();
        _commandCache.Clear();
        // Defensive: if the mission ended with the recursion guard incremented (abnormal
        // termination during a suppressed callstack), reset it so the NEXT mission's
        // postfix doesn't see a stale suppress flag and bail out for every charge order.
        SmartCavalryRecursionGuard.Reset();
    }
}
