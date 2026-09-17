using System;
using System.Diagnostics;
using TAOM.Core.Logging;
using TAOM.Features.BattleLoadDiagnostics;
using TaleWorlds.MountAndBlade;

namespace TAOM.Features.MissionPerf.Hooks;

/// <summary>
/// One <c>[MissionPerf]</c> line every five seconds of wall clock while a mission ticks: frames,
/// fps, average / p95 / max frame ms, agent and formation counts, GC collections. Written for the
/// culture-doctrine A/B (#608), where the question is whether a change to the team AI costs frame
/// time, and kept for every mission because it is the only in-mission frame-time record TAOM has
/// (<c>[MemSample]</c> has no frame counter, <c>[MapLoad]</c> is campaign-map only).
///
/// Thin entry point per ADR-002: the window maths is <see cref="FrameStats"/>, the format is
/// <see cref="MissionPerfLine"/>. Frame time is wall clock between consecutive ticks, so the line
/// measures what the player feels, including time scaling and the order menu. Everything is
/// wrapped and the heartbeat self-disables for the mission after a single failure: a diagnostic
/// must never become the thing that breaks the battle it was added to measure.
/// </summary>
public sealed class MissionPerfHeartbeatBehavior : MissionLogic
{
    // The MCM instance lookup is a scan over every registered settings container; once a second
    // is plenty for a toggle, and the doctrine logic beside this reads its own toggle even less.
    private const double ToggleRefreshSeconds = 1.0;

    private readonly IModLogger _logger;
    private readonly FrameStats _stats = new FrameStats();
    private long _missionStart;
    private long _lastTick;
    private bool _enabled = true;
    private double _nextToggleCheck;
    private int _gc0;
    private int _gc1;
    private int _gc2;
    private bool _disabled;

    public MissionPerfHeartbeatBehavior(IModLogger logger)
    {
        _logger = logger;
    }

    public override void OnCreated() => Reset();

    public override void OnMissionTick(float dt)
    {
        if (_disabled)
            return;
        try
        {
            var now = Stopwatch.GetTimestamp();
            var nowSeconds = (now - _missionStart) / (double)Stopwatch.Frequency;
            if (nowSeconds >= _nextToggleCheck)
            {
                _nextToggleCheck = nowSeconds + ToggleRefreshSeconds;
                _enabled = BattleLoadDiagnosticsSettings.Instance?.EnableMissionPerfHeartbeat ?? true;
            }
            if (!_enabled)
            {
                // The gap while off must not become one giant frame when the toggle returns.
                _lastTick = 0;
                return;
            }
            if (_lastTick != 0)
                _stats.Record((now - _lastTick) * 1000d / Stopwatch.Frequency);
            _lastTick = now;

            if (!_stats.ShouldEmit(nowSeconds))
                return;

            var window = _stats.Emit(nowSeconds);
            var gc0 = GC.CollectionCount(0);
            var gc1 = GC.CollectionCount(1);
            var gc2 = GC.CollectionCount(2);
            _logger.LogInfo(MissionPerfLine.Build(
                nowSeconds, window, Mission.AllAgents.Count, Mission.Agents.Count, CountLiveFormations(),
                gc0 - _gc0, gc1 - _gc1, gc2 - _gc2));
            _gc0 = gc0;
            _gc1 = gc1;
            _gc2 = gc2;
        }
        catch (Exception ex)
        {
            _disabled = true;
            try { _logger.LogError($"[MissionPerf] heartbeat disabled for this mission after {ex.GetType().Name}: {ex.Message}"); }
            catch { /* diagnostic only */ }
        }
    }

    protected override void OnEndMission() => Reset();

    private int CountLiveFormations()
    {
        var count = 0;
        var teams = Mission.Teams;
        for (var t = 0; t < teams.Count; t++)
        {
            var formations = teams[t].FormationsIncludingEmpty;
            for (var f = 0; f < formations.Count; f++)
                if (formations[f].CountOfUnits > 0)
                    count++;
        }
        return count;
    }

    private void Reset()
    {
        _stats.Reset();
        _missionStart = Stopwatch.GetTimestamp();
        _lastTick = 0;
        _enabled = true;
        _nextToggleCheck = 0;
        _gc0 = GC.CollectionCount(0);
        _gc1 = GC.CollectionCount(1);
        _gc2 = GC.CollectionCount(2);
        _disabled = false;
    }
}
