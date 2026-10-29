using System;
using System.Collections.Generic;
using TAOM.Core.Logging;
using TaleWorlds.Core;
using TaleWorlds.MountAndBlade;
using TaleWorlds.MountAndBlade.Objects;

namespace TAOM.Features.StagedSiege.Spike;

/// <summary>
/// Holds the flags a spike command spawned and a finish request. Inert by default: it does nothing until a
/// <c>taom.ss_*</c> command touches it. MUST be <c>: MissionLogic</c>. Every override catches and logs.
/// </summary>
public sealed class StagedSiegeSpikeMissionBehavior : MissionLogic
{
    internal const string Tag = "[StagedSiegeSpike]";

    private readonly IModLogger _logger;
    private readonly List<FlagCapturePoint> _flags = new();
    private bool _moving;
    private float _logClock;
    private bool _spawnersStopped;

    public StagedSiegeSpikeMissionBehavior(IModLogger logger) => _logger = logger;

    public bool FinishRequested { get; private set; }

    public IReadOnlyList<FlagCapturePoint> Flags => _flags;

    public void AddFlag(FlagCapturePoint flag) => _flags.Add(flag);

    public void RequestFinish() => FinishRequested = true;

    public void MoveFlags(CaptureTheFlagFlagDirection direction)
    {
        foreach (var flag in _flags) flag.SetMoveFlag(direction);
        _moving = true;
        _logClock = 1f;
    }

    public void StopFlags()
    {
        foreach (var flag in _flags) flag.SetMoveNone();
        _moving = false;
    }

    public override void OnMissionTick(float dt)
    {
        try
        {
            if (!_moving || _flags.Count == 0) return;
            _logClock += dt;
            if (_logClock < 1f) return;
            _logClock = 0f;
            for (var i = 0; i < _flags.Count; i++)
                _logger.LogInfo($"{Tag} flag {i} at {_flags[i].Position} progress {_flags[i].GetFlagProgress():F3}");
        }
        catch (Exception ex) { _moving = false; Log("OnMissionTick", ex); }
    }

    public override bool MissionEnded(ref MissionResult missionResult)
    {
        try
        {
            if (!FinishRequested) return false;
            StopSpawners();
            missionResult = StagedSiegeSpikeFinish.CreateResult(Mission);
            return true;
        }
        catch (Exception ex) { Log("MissionEnded", ex); return false; }
    }

    private void StopSpawners()
    {
        if (_spawnersStopped) return;
        _spawnersStopped = true;
        var spawn = Mission.GetMissionBehavior<IMissionAgentSpawnLogic>();
        spawn?.StopSpawner(BattleSideEnum.Attacker);
        spawn?.StopSpawner(BattleSideEnum.Defender);
    }

    protected override void OnEndMission()
    {
        try { base.OnEndMission(); Clear(); }
        catch (Exception ex) { Log("OnEndMission", ex); }
    }

    public override void OnRemoveBehavior()
    {
        try { Clear(); base.OnRemoveBehavior(); }
        catch (Exception ex) { Log("OnRemoveBehavior", ex); }
    }

    private void Clear()
    {
        _flags.Clear();
        _moving = false;
        FinishRequested = false;
    }

    private void Log(string site, Exception ex) => _logger.LogError($"{Tag} {site} failed: {ex}");
}
