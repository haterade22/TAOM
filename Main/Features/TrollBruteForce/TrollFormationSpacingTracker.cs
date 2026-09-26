using System.Collections.Generic;
using TAOM.Core.Collections;
using TAOM.Core.Logging;
using TaleWorlds.MountAndBlade;

namespace TAOM.Features.TrollBruteForce;

/// <summary>
/// Feeds Patch92: twice a second, counts each formation's units and trolls and asks the service for the width the
/// formation is spaced for (<see cref="ITrollBruteForceService.FormationUnitDiameter"/>). When a width changes, it
/// rebuilds the formation's slot positions (<c>Formation.OnUnitAddedOrRemoved</c> plus
/// <c>Arrangement.OnFormationFrameChanged(updateCachedOrderedLocalPositions: true)</c>, the flag
/// <c>LineFormation.UpdateLocalPositionErrors</c> passes; the engine's own frame change passes false): with true,
/// <c>BatchUnitPositionAvailabilities</c> recomputes the cached local slot positions from the current unit width and
/// then <c>_globalPositions</c> from them. A re-issued Move order to the same position recomputes neither, so simply
/// re-ordering the formation onto its own slot left every troll on the old human-width positions (2026-09-25).
/// While the field is deploying, the trolls have already been placed at human width and nothing else walks them
/// there, so once the width changes the tick also replays vanilla's own
/// end-of-mass-transfer tail (<c>Formation.OnMassUnitTransferEnd</c>, v1.5.3 <c>Formation.cs:1958-1966</c>) so they
/// jump onto the wider slots the moment <c>Mission.IsTeleportingAgents</c> is on; after deployment they simply walk
/// there. Main thread only (OnMissionTick). Boundary code (raw Agent/Formation); game-tested per ADR-008.
/// </summary>
public sealed class TrollFormationSpacingTracker
{
    private const float RefreshSeconds = 0.5f;

    private readonly ITrollBruteForceService _service;
    private readonly IModLogger _logger;
    private readonly Dictionary<Formation, (int Units, int Trolls, float Widest)> _counts =
        new(ReferenceIdentity.Instance);
    private readonly List<Formation> _gone = new();
    private float _nextRefresh;

    public TrollFormationSpacingTracker(ITrollBruteForceService service, IModLogger logger)
    {
        _service = service;
        _logger = logger;
    }

    public void Tick(Mission mission)
    {
        if (mission.CurrentTime < _nextRefresh) return;
        _nextRefresh = mission.CurrentTime + RefreshSeconds;

        _counts.Clear();
        foreach (Agent agent in mission.Agents)
        {
            Formation formation = agent.Formation;
            if (formation == null) continue;
            _counts.TryGetValue(formation, out var c);
            c.Units++;
            // Only trolls pay for the native AgentScale read: the Monster id check is a managed lookup.
            string? id = agent.Monster?.StringId;
            float width = _service.IsBruteForceTroll(id) ? _service.TrollWidth(id, agent.AgentScale) : 0f;
            if (width > 0f)
            {
                c.Trolls++;
                if (width > c.Widest) c.Widest = width;
            }
            _counts[formation] = c;
        }

        _gone.Clear();
        foreach (object stored in TrollFormationSpacingStore.Keys)
            if (stored is Formation formation && !_counts.ContainsKey(formation)) _gone.Add(formation);
        foreach (Formation formation in _gone)
            TrollFormationSpacingStore.Set(formation, null);   // emptied: nothing left to re-form

        float vanilla = Formation.GetDefaultUnitDiameter(isMounted: false);
        foreach (var pair in _counts)
        {
            var (units, trolls, widest) = pair.Value;
            float? diameter = _service.FormationUnitDiameter(vanilla, units, trolls, widest);
            if (!TrollFormationSpacingStore.Set(pair.Key, diameter)) continue;

            Formation formation = pair.Key;
            formation.OnUnitAddedOrRemoved();
            // updateCachedOrderedLocalPositions: true is what actually rebuilds LineFormation's cached
            // slots from the new width; it sets Arrangement.AreLocalPositionsDirty internally, so no
            // separate dirty-flag write is needed.
            formation.Arrangement.OnFormationFrameChanged(updateCachedOrderedLocalPositions: true);
            if (mission.IsTeleportingAgents)
            {
                // Vanilla's own OnMassUnitTransferEnd tail: the trolls were placed at human width before
                // their width was known, and nothing else walks them mid-deployment, so replay it now
                // that the width is stored and the slots rebuilt.
                formation.ApplyActionOnEachUnit(a =>
                    a.ForceUpdateCachedAndFormationValues(updateOnlyMovement: true, arrangementChangeAllowed: false));
                formation.SetHasPendingUnitPositions(hasPendingUnitPositions: false);
            }

            _logger.LogInfo($"[TrollSpacing] team {formation.Team?.TeamIndex} formation {formation.FormationIndex}: " +
                $"{trolls} troll(s) of {units}, unit width " +
                (diameter is float d ? $"{vanilla:0.00} -> {d:0.00} m" : "back to vanilla") +
                $" (teleporting {mission.IsTeleportingAgents})");
        }
    }

    public void Clear()
    {
        _counts.Clear();
        _gone.Clear();
        _nextRefresh = 0f;
        TrollFormationSpacingStore.Clear();
    }
}
