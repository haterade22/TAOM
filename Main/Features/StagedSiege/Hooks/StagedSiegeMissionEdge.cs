using System;
using TAOM.Adapters;
using TAOM.Core.Logging;
using TAOM.Features.StagedSiege.Domain;
using TaleWorlds.Core;
using TaleWorlds.MountAndBlade;

namespace TAOM.Features.StagedSiege.Hooks;

/// <summary>
/// The engine-facing work of <see cref="StagedSiegeMissionBehavior"/> that is more than a delegation (ADR-002): mapping the end to
/// a <see cref="MissionResult"/>, stopping the spawners, filtering built agents, draining the console's queued requests and the S5
/// log lines. Every method catches, so a
/// fault logs once per call and vanilla carries on. Main thread only.
/// </summary>
internal sealed class StagedSiegeMissionEdge
{
    private readonly IModLogger _logger;
    private bool _spawnersStopped;
    private bool _endLogged;
    private bool _hallEndLogged;

    public StagedSiegeMissionEdge(IModLogger logger) => _logger = logger;

    // The console's own adapter: built the first time a command arrives, so a battle nobody types into never makes one.
    private IStagedSiegeMissionAdapter? _commandAdapter;

    /// <summary>
    /// A catch at the mission boundary. The line names its effect: after most catches the service stays live and only this call is
    /// skipped; the <c>EarlyStart</c> catch drops the service and hall, so it passes <see cref="RulesOffForMission"/>. The service and hall
    /// the caller holds are told, so the run report's <c>faults</c> check sees a fault that never reached them.
    /// </summary>
    internal const string RulesOffForMission = "staged rules off for this mission";

    public void Fault(string site, Exception ex, StagedSiegeService? service = null, StagedHallService? hall = null, string effect = "this call skipped")
    {
        _logger.LogError($"{StagedSiegeService.Tag} boundary fault in {site}: {ex}; {effect}, vanilla continues");
        try
        {
            service?.NoteBoundaryFault(site, ex);
            hall?.NoteBoundaryFault(site, ex);
        }
        catch (Exception)
        {
            // The report is a diagnostic: never let it throw out of a catch.
        }
    }

    /// <summary>The start of <c>OnMissionTick</c>: applies the console's queued requests, if any, on the main thread.</summary>
    public void DrainCommands(StagedSiegeCommandQueue queue, StagedSiegeService? service, Mission mission)
    {
        if (queue.IsEmpty)
            return;
        try
        {
            _commandAdapter ??= new StagedSiegeMissionAdapter(mission, null);
            queue.Drain(service, _commandAdapter, _logger);
        }
        catch (Exception ex)
        {
            Fault("DrainCommands", ex, service);
        }
    }

    /// <summary>The <c>MissionEnded</c> body: true, with the result set, once the last point has fallen.</summary>
    public bool TryEnd(StagedSiegeService? service, Mission mission, ref MissionResult missionResult)
    {
        try
        {
            if (service == null || !service.TryGetEnd(out var end))
                return false;

            var kind = StagedResult.For(end);
            if (!kind.HasValue)
                return false;
            StopSpawners(mission);
            missionResult = kind.Value == StagedResultKind.DefenderPushedBack ? MissionResult.CreateDefenderPushedBack()
                : kind.Value == StagedResultKind.Successful ? MissionResult.CreateSuccessful(mission)
                : MissionResult.CreateDefeated(mission);
            service.AnnounceEnd(end);
            if (!_endLogged)
            {
                _endLogged = true;
                _logger.LogInfo($"{StagedSiegeService.Tag} S5 MissionEnded returned {end}");
            }

            return true;
        }
        catch (Exception ex)
        {
            Fault("MissionEnded", ex, service);
            return false;
        }
    }

    public void LogResultReady(StagedSiegeService? service, StagedHallService? hall, MissionResult? missionResult)
    {
        try
        {
            if (missionResult == null)
                return;
            // The services take plain values (ADR-007). A faulted service takes them too: its report matters most then.
            var state = missionResult.BattleState.ToString();
            var victory = missionResult.PlayerVictory;
            var defeated = missionResult.PlayerDefeated;
            if (service?.IsArmed == true)
                _logger.LogInfo($"{StagedSiegeService.Tag} S5 result ready: state {state} victory {victory} defeated {defeated}");
            service?.NoteMissionResult(state, victory, defeated);
            if (hall?.IsArmed == true)
                _logger.LogInfo($"{StagedSiegeService.Tag} hall result ready: state {state} victory {victory} defeated {defeated}");
            hall?.NoteMissionResult(state, victory, defeated);
        }
        catch (Exception ex)
        {
            Fault("OnMissionResultReady", ex, service, hall);
        }
    }

    /// <summary>The <c>OnAgentBuild</c> body: a human on an attacking or defending team is noted for the S2 reinforcement lines.</summary>
    public void NoteAgentBuilt(StagedSiegeService? service, Agent? agent)
    {
        try
        {
            if (service == null || !service.IsArmed || agent == null || !agent.IsHuman || agent.Team == null)
                return;
            var side = agent.Team.Side;
            if (side != BattleSideEnum.Attacker && side != BattleSideEnum.Defender)
                return;
            var position = agent.Position;
            service.NoteAgentBuilt(side == BattleSideEnum.Attacker ? StagedSide.Attacker : StagedSide.Defender,
                new StagePoint(position.x, position.y, position.z));
        }
        catch (Exception ex)
        {
            Fault("OnAgentBuild", ex, service);
        }
    }

    /// <summary>
    /// The <c>EarlyStart</c> half of the lord's hall: a service for a hall mission, armed or inert, and null for any other mission so
    /// the caller runs the walls path. Decided here, before the walls adapter is built, so a hall mission never reaches the walls rules.
    /// </summary>
    public StagedHallService? TryArmHall(Mission mission, StagedSiegeSceneCatalog catalog, bool enabled)
    {
        var adapter = new StagedHallMissionAdapter(mission);
        if (!adapter.IsHallMission)
            return null;
        var hall = new StagedHallService(adapter, catalog, _logger);
        hall.TryArm(enabled);
        return hall;
    }

    /// <summary>
    /// The hall's <c>MissionEnded</c> body: true, with an attacker victory, once point E is taken. It runs after the walls half and
    /// after vanilla's <c>BattleEndLogic</c> in the behavior list, so a depleted side still ends the hall the vanilla way first.
    /// </summary>
    public bool TryEndHall(StagedHallService? hall, Mission mission, ref MissionResult missionResult)
    {
        try
        {
            if (hall == null || !hall.TryGetCaptured())
                return false;

            missionResult = MissionResult.CreateSuccessful(mission);
            if (!_hallEndLogged)
            {
                _hallEndLogged = true;
                _logger.LogInfo($"{StagedSiegeService.Tag} hall MissionEnded returned CreateSuccessful");
            }

            return true;
        }
        catch (Exception ex)
        {
            Fault("MissionEnded (hall)", ex, null, hall);
            return false;
        }
    }

    public void Shutdown(StagedSiegeService? service, StagedHallService? hall)
    {
        try
        {
            service?.Shutdown();
            hall?.Shutdown();
        }
        catch (Exception ex)
        {
            Fault("Shutdown", ex, service, hall);
        }
    }

    private void StopSpawners(Mission mission)
    {
        if (_spawnersStopped)
            return;
        _spawnersStopped = true;
        var spawn = mission.GetMissionBehavior<IMissionAgentSpawnLogic>();
        spawn?.StopSpawner(BattleSideEnum.Attacker);
        spawn?.StopSpawner(BattleSideEnum.Defender);
    }
}
