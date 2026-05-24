using System.Collections.Generic;
using System.Linq;
using TaleWorlds.MountAndBlade;
using TAOM.Core.Domain;
using TAOM.Core.Logging;

namespace TAOM.Features.MissionDiagnostic.Hooks;

// Boundary MissionLogic that captures diagnostic snapshots and forwards to the
// service. Inherits MissionLogic (not just MissionBehavior) per the
// feedback_missionbehaviortype_logic_requires_missionlogic_inheritance rule.
public sealed class MissionDiagnosticBehavior : MissionLogic
{
    private readonly IMissionDiagnosticService _service;
    private readonly IRaceManager _raceManager;
    private readonly IModLogger _logger;

    private bool _missionStartLogged;
    private float _actionSetWindowSecondsLeft = 5f;

    public MissionDiagnosticBehavior(IMissionDiagnosticService service, IRaceManager raceManager, IModLogger logger)
    {
        _service = service;
        _raceManager = raceManager;
        _logger = logger;
    }

    public override void OnMissionTick(float dt)
    {
        // First-tick snapshot: dump the full MissionBehaviors + MissionLogics view
        // AFTER vanilla and all mods have added their behaviors. Mission ctor adds
        // them before tick begins, so first OnMissionTick is the right gate.
        if (!_missionStartLogged)
        {
            _missionStartLogged = true;
            _service.ResetForNewMission();
            DumpMissionStart();
        }

        // Action-set capture window: 5s of agent observation, then disable.
        if (_actionSetWindowSecondsLeft > 0f)
        {
            _actionSetWindowSecondsLeft -= dt;
            CaptureActionSetsFromAgents();
        }
    }

    private void DumpMissionStart()
    {
        try
        {
            var mission = Mission;
            if (mission == null) return;

            // Read both collections via vanilla properties. Indexable copies so the
            // service sees a stable snapshot regardless of further mid-tick adds.
            var behaviors = mission.MissionBehaviors?.ToList() ?? new List<MissionBehavior>();
            var missionLogics = mission.MissionLogics?.ToList() ?? new List<MissionLogic>();

            var sceneName = mission.SceneName ?? "<unknown>";
            _service.LogMissionStartSnapshot(sceneName, behaviors, missionLogics);
        }
        catch (System.Exception ex)
        {
            _logger.LogWarning($"[MissionDiag] DumpMissionStart failed: {ex.GetType().Name}: {ex.Message}");
        }
    }

    private void CaptureActionSetsFromAgents()
    {
        try
        {
            var mission = Mission;
            if (mission?.Agents == null) return;

            foreach (var agent in mission.Agents)
            {
                if (agent == null) continue;
                // GetName() returns the engine-side string id (e.g. "as_human_warrior").
                var actionSetName = agent.ActionSet.GetName();
                var raceId = agent.Character?.Race ?? -1;
                var raceName = raceId >= 0 ? (_raceManager.GetRaceNameFromId(raceId) ?? $"id={raceId}") : "<none>";
                var agentName = agent.Name ?? "<unnamed>";
                _service.LogActionSetSeen(actionSetName, raceName, agentName);
            }
        }
        catch (System.Exception ex)
        {
            _logger.LogWarning($"[MissionDiag] CaptureActionSetsFromAgents failed: {ex.GetType().Name}: {ex.Message}");
            _actionSetWindowSecondsLeft = 0f; // stop trying if it's not working
        }
    }

    public override void OnEndMissionInternal()
    {
        // Reset window flag for the next mission. The service-level dedup set
        // also resets via ResetForNewMission on the next OnMissionTick.
        _missionStartLogged = false;
        _actionSetWindowSecondsLeft = 5f;
    }
}
