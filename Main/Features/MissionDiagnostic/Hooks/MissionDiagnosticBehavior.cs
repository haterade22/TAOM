using System.Collections.Generic;
using System.Linq;
using TaleWorlds.Core;
using TaleWorlds.MountAndBlade;
using TAOM.Core.Domain;
using TAOM.Core.Logging;
using TAOM.Features.AdvancedCombat;

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
    private bool _censusOpened;
    private bool _readingStopped;
    private readonly PendingAgentReads<Agent> _pending;
    private float _actionSetWindowSecondsLeft = ActionSetWindowSeconds;

    public MissionDiagnosticBehavior(IMissionDiagnosticService service, IRaceManager raceManager, IModLogger logger)
    {
        _service = service;
        _raceManager = raceManager;
        _logger = logger;
        _pending = new PendingAgentReads<Agent>(message => _logger.LogWarning(message));
    }

    public override void OnMissionTick(float dt)
    {
        MissionThreadGuard.MarkMainThread(); // this behavior owns a deferral queue, so it marks the thread itself (#634)
        // First-tick snapshot: dump the full MissionBehaviors + MissionLogics view
        // AFTER vanilla and all mods have added their behaviors. Mission ctor adds
        // them before tick begins, so first OnMissionTick is the right gate.
        if (!_missionStartLogged)
        {
            _missionStartLogged = true;
            DumpMissionStart();
            OpenCensusOnce();
        }

        // The census header and summary still frame a 5 s window. Agents are not polled every frame: each one is
        // queued in OnAgentCreated and read once, here, on the next tick, so a reinforcement that spawns after the
        // window still gets its line. The summary is written once, when the window runs out or the mission ends first.
        ReadPendingAgents();

        if (_actionSetWindowSecondsLeft > 0f)
        {
            _actionSetWindowSecondsLeft -= dt;
            // A positive requirement, so a NaN frame time closes the window with its summary (NaN <= 0 is false).
            if (!(_actionSetWindowSecondsLeft > 0f)) _service.LogActionSetCensusClosed();
        }
    }

    // Mission.CreateAgent raises this for SpawnAgent, its mount and SpawnMonster; the action set an agent keeps is set after
    // SpawnAgent returns, so the agent is queued here and read on the next tick, and the census may open here (feature doc).
    public override void OnAgentCreated(Agent agent)
    {
        if (agent == null || _readingStopped) return;
        OpenCensusOnce();
        _pending.Add(agent);
    }

    // Native raises these on the thread it chooses (#634, #607): an off-thread drop waits for the next tick, before any read.
    public override void OnAgentRemoved(Agent affectedAgent, Agent affectorAgent, AgentState agentState, KillingBlow blow) =>
        _pending.RemoveOrDefer("MissionDiagnosticBehavior.OnAgentRemoved", affectedAgent);

    public override void OnAgentDeleted(Agent affectedAgent) => _pending.RemoveOrDefer("MissionDiagnosticBehavior.OnAgentDeleted", affectedAgent);

    private void ReadPendingAgents()
    {
        if (_readingStopped) return;
        try
        {
            _pending.Drain(CaptureActionSet);
        }
        catch (System.Exception ex)
        {
            // One warning, then no more reads this mission: a native fault here would repeat for every agent.
            _readingStopped = true;
            _pending.Clear();
            _logger.LogWarning($"[MissionDiag] CaptureActionSet failed, census reads stopped for this mission: {ex.GetType().Name}: {ex.Message}");
        }
    }

    private void OpenCensusOnce()
    {
        if (_censusOpened) return;
        _censusOpened = true;
        _service.ResetForNewMission();
        _service.LogActionSetCensusOpened(ActionSetWindowSeconds);
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

    // Throws on a native fault; ReadPendingAgents turns the first one into a warning and stops the census.
    private void CaptureActionSet(Agent agent)
    {
        // Cheap pre-filter with no string marshal: MBActionSet.GetHashCode() is its engine index, and
        // race id plus sex select the race name and sex the service keys its line on. Only a combination
        // not yet seen this mission reads the names below.
        var actionSet = agent.ActionSet;
        var character = agent.Character;
        var raceId = character?.Race ?? -1;
        var isFemale = character?.IsFemale ?? false;
        if (!_service.TryMarkActionSetKey(actionSet.GetHashCode(), raceId, isFemale)) return;

        // GetName() returns the engine-side string id (e.g. "as_human_warrior").
        var actionSetName = actionSet.GetName();
        var raceName = raceId >= 0 ? (_raceManager.GetRaceNameFromId(raceId) ?? $"id={raceId}") : "<none>";
        var agentName = agent.Name ?? "<unnamed>";
        // Character id + Monster id turn "a dwarf is running as_human_warrior" into an
        // actionable line. TAOM's GenerateActionSetNameWithSuffix prefix emits exactly
        // "as_human<suffix>" when the Monster is null, so a null monster here IS the
        // explanation - without it the census names a symptom and nothing else.
        var characterId = character?.StringId;
        var monsterId = agent.Monster?.StringId;
        _service.LogActionSetSeen(actionSetName, raceName, isFemale, agentName, characterId, monsterId);
    }

    public override void OnEndMissionInternal()
    {
        // Reset window flag for the next mission. The service-level dedup set
        // also resets via ResetForNewMission when the census opens. A mission shorter than the window still gets
        // its census summary, keyed on the header having been written (an agent can open the census before the
        // first tick, and a mission can end before that tick).
        if (_censusOpened && _actionSetWindowSecondsLeft > 0f) _service.LogActionSetCensusClosed();
        _pending.Clear();
        _censusOpened = false;
        _readingStopped = false;
        _missionStartLogged = false;
        _actionSetWindowSecondsLeft = ActionSetWindowSeconds;
    }
}
