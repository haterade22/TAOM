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

    private const float ActionSetWindowSeconds = 5f;

    private bool _missionStartLogged;
    private float _actionSetWindowSecondsLeft = ActionSetWindowSeconds;

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
            _service.LogActionSetCensusOpened(ActionSetWindowSeconds);
        }

        // Action-set capture window: 5s of agent observation, then disable. The summary is
        // written once, when the window runs out or a failed capture closes it.
        if (_actionSetWindowSecondsLeft > 0f)
        {
            _actionSetWindowSecondsLeft -= dt;
            CaptureActionSetsFromAgents();
            // A positive requirement, so a NaN frame time closes the window with its summary (NaN <= 0 is false).
            if (!(_actionSetWindowSecondsLeft > 0f)) _service.LogActionSetCensusClosed();
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
                // Cheap pre-filter with no string marshal: MBActionSet.GetHashCode() is its engine index, and
                // race id plus sex select the race name and sex the service keys its line on. Only a combination
                // not yet seen this mission reads the names below.
                var actionSet = agent.ActionSet;
                var character = agent.Character;
                var raceId = character?.Race ?? -1;
                var isFemale = character?.IsFemale ?? false;
                if (!_service.TryMarkActionSetKey(actionSet.GetHashCode(), raceId, isFemale)) continue;

                // GetName() returns the engine-side string id (e.g. "as_human_warrior").
                var actionSetName = actionSet.GetName();
                var raceName = raceId >= 0 ? (_raceManager.GetRaceNameFromId(raceId) ?? $"id={raceId}") : "<none>";
                var agentName = agent.Name ?? "<unnamed>";
                // Character id + Monster id turn "a dwarf is running as_human_warrior" into an
                // actionable line. TAOM's GenerateActionSetNameWithSuffix prefix emits exactly
                // "as_human<suffix>" when the Monster is null, so a null monster here IS the
                // explanation — without it the census names a symptom and nothing else.
                var characterId = character?.StringId;
                var monsterId = agent.Monster?.StringId;
                _service.LogActionSetSeen(actionSetName, raceName, isFemale, agentName, characterId, monsterId);
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
        // also resets via ResetForNewMission on the next OnMissionTick. A mission shorter
        // than the window still gets its census summary.
        if (_missionStartLogged && _actionSetWindowSecondsLeft > 0f) _service.LogActionSetCensusClosed();
        _missionStartLogged = false;
        _actionSetWindowSecondsLeft = ActionSetWindowSeconds;
    }
}
