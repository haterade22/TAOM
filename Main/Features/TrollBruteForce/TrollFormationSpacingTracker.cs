using System;
using System.Collections.Generic;
using TAOM.Core.Logging;
using TAOM.Features.TrollBruteForce.Hooks;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;

namespace TAOM.Features.TrollBruteForce;

/// <summary>
/// Feeds Patch92: twice a second, counts each formation's units and trolls and asks the service for the width the
/// formation is spaced for (<see cref="ITrollBruteForceService.FormationUnitDiameter"/>). When a width changes it
/// re-forms the formation the way vanilla does when a unit joins (<c>Formation.OnUnitAddedOrRemoved</c>, which
/// reapplies the form order) and marks the arrangement's cached slot positions dirty: <c>LineFormation</c> rebuilds
/// them from the width only when something asks (<c>BatchUnitPositionAvailabilities</c>), so without it the slots kept
/// the human-width positions (2026-09-25, every troll 1.6 m from its neighbour after the width was stored). The
/// layout it ends up with is logged on change. During deployment the trolls have already been placed at human width
/// and nothing walks them, so a changed formation is re-deployed the way the Deploy button does it
/// (<c>DeploymentHandler.OrderController_OnOrderIssued_Aux(Move)</c>: re-position, then every unit re-reads its
/// slot); <c>Mission.IsTeleportingAgents</c> is on for the whole field deployment (<c>BattleDeploymentMissionController
/// .OnSetupTeamsFinished</c>), so they jump onto the wider slots. After deployment they walk there. Main thread only
/// (OnMissionTick). Boundary code (raw Agent/Formation); game-tested per ADR-008.
/// </summary>
public sealed class TrollFormationSpacingTracker
{
    private const float RefreshSeconds = 0.5f;

    private readonly ITrollBruteForceService _service;
    private readonly IModLogger _logger;
    private readonly Dictionary<Formation, (int Units, int Trolls, float Widest)> _counts = new(FormationIdentity.Instance);
    private readonly List<Formation> _gone = new();
    private readonly Dictionary<Formation, string> _lastLayout = new(FormationIdentity.Instance);
    // widened before the engine switched deployment teleporting on (the first refresh can precede
    // BattleDeploymentMissionController.OnSetupTeamsFinished): re-deployed as soon as it is on
    private readonly HashSet<Formation> _pendingRedeploy = new(FormationIdentity.Instance);
    private float _nextRefresh;

    public TrollFormationSpacingTracker(ITrollBruteForceService service, IModLogger logger)
    {
        _service = service;
        _logger = logger;
    }

    public void Tick(Mission mission)
    {
        RedeployPending(mission);
        if (mission.CurrentTime < _nextRefresh) return;
        _nextRefresh = mission.CurrentTime + RefreshSeconds;

        _counts.Clear();
        foreach (Agent agent in mission.Agents)
        {
            Formation formation = agent.Formation;
            if (formation == null) continue;
            _counts.TryGetValue(formation, out var c);
            c.Units++;
            float width = _service.TrollWidth(agent.Monster?.StringId, agent.AgentScale);
            if (width > 0f)
            {
                c.Trolls++;
                if (width > c.Widest) c.Widest = width;
            }
            _counts[formation] = c;
        }

        _gone.Clear();
        foreach (Formation formation in TrollFormationSpacingStore.Formations)
            if (!_counts.ContainsKey(formation)) _gone.Add(formation);
        foreach (Formation formation in _gone)
            TrollFormationSpacingStore.Set(formation, null);   // emptied: nothing left to re-form

        float vanilla = Formation.GetDefaultUnitDiameter(isMounted: false);
        foreach (var pair in _counts)
        {
            var (units, trolls, widest) = pair.Value;
            float? diameter = _service.FormationUnitDiameter(vanilla, units, trolls, widest);
            if (trolls > 0) LogLayout(pair.Key);
            if (!TrollFormationSpacingStore.Set(pair.Key, diameter)) continue;

            pair.Key.OnUnitAddedOrRemoved();
            pair.Key.Arrangement.AreLocalPositionsDirty = true;
            if (mission.Mode == MissionMode.Deployment) _pendingRedeploy.Add(pair.Key);
            _logger.LogInfo($"[TrollSpacing] team {pair.Key.Team?.TeamIndex} formation {pair.Key.FormationIndex}: " +
                $"{trolls} troll(s) of {units}, unit width " +
                (diameter is float d ? $"{vanilla:0.00} -> {d:0.00} m" : "back to vanilla") +
                $" (mode {mission.Mode}, teleporting {mission.IsTeleportingAgents})");
        }
    }

    private void RedeployPending(Mission mission)
    {
        if (_pendingRedeploy.Count == 0) return;
        if (mission.Mode != MissionMode.Deployment)
        {
            _pendingRedeploy.Clear();   // the battle started: they walk to the new slots
            return;
        }
        if (!mission.IsTeleportingAgents) return;
        foreach (Formation formation in _pendingRedeploy)
        {
            if (formation.CountOfUnits == 0) continue;
            DeploymentHandler.OrderController_OnOrderIssued_Aux(OrderType.Move, new MBList<Formation> { formation });
            _logger.LogInfo($"[TrollSpacing] team {formation.Team?.TeamIndex} formation {formation.FormationIndex}: " +
                "re-deployed onto the new slots");
        }
        _pendingRedeploy.Clear();
    }

    // Diagnostic: proves whether the arrangement took the width (rank count, flank width) in the next smoke.
    private void LogLayout(Formation formation)
    {
        var arrangement = formation.Arrangement;
        string layout = $"{arrangement.GetType().Name} flank={arrangement.FlankWidth:0.0} ranks={arrangement.RankCount} " +
            $"unitWidth={formation.UnitDiameter:0.00} interval={formation.Interval:0.00} units={formation.CountOfUnits}";
        if (_lastLayout.TryGetValue(formation, out string last) && last == layout) return;
        _lastLayout[formation] = layout;
        _logger.LogInfo($"[TrollSpacing] team {formation.Team?.TeamIndex} formation {formation.FormationIndex} layout: {layout}");
    }

    public void Clear()
    {
        _counts.Clear();
        _gone.Clear();
        _lastLayout.Clear();
        _pendingRedeploy.Clear();
        _nextRefresh = 0f;
        TrollFormationSpacingStore.Clear();
    }
}
