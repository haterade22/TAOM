using System;
using System.Diagnostics;
using System.Threading;
using TAOM.Adapters;
using TAOM.Core.Logging;
using TAOM.Features.BattleLoadDiagnostics;
using TaleWorlds.MountAndBlade;

namespace TAOM.Features.MissionPerf.Hooks;

/// <summary>
/// The Patch97 tick profiler's per-mission side. Every mission writes one <c>[PerfContext]</c> line at its first
/// tick, profiler on or off. A mission measures when the profiler is installed, its toggle is on at the mission's
/// start and every hook it needs is still in place (<see cref="MissionTickProfilerHealth"/>: the install is a
/// startup fact, so the hooks are read again at each mission start, the site count on every tick). It then writes a
/// <c>[TickProfiler] mission</c> header at creation (before any <c>[Hitch]</c>), one warning per replaced MCM knob,
/// a <c>[TickProfile]</c> window on the heartbeat's 5 s clock (same <see cref="FrameStats"/>, same <c>t</c>) and a
/// <c>[TickSummary]</c> at mission end. A mission that does not measure says why, once. Thin (ADR-002): the
/// arithmetic is <see cref="MissionTickProfiler"/>, the lines <see cref="TickProfileLines"/>. Overrides only
/// <c>OnCreated</c>, <c>OnMissionTick</c> and <c>OnEndMission</c> (<c>OnBehaviorInitialize</c> never fires for a
/// behaviour TAOM adds, <c>MissionBehaviorLifecycleTests</c>). A failure disables it for the mission with one line.
/// </summary>
public sealed class MissionTickProfilerBehavior : MissionLogic
{
    private static int _missionsInProcess;

    private readonly IBattleLoadDiagnosticsSettingsProvider _settings;
    private readonly IGraphicsOptionsAdapter _options;
    private readonly IModLogger _logger;
    private readonly FrameStats _clock = new FrameStats();
    private long _missionStart;
    private int _missionNumber;
    private int _generation;
    private int _topN;
    private double _hitchMs;
    private bool _toggleOn;
    private bool _measuring;
    private bool _firstTickDone;
    private bool _disabled;

    public MissionTickProfilerBehavior(IBattleLoadDiagnosticsSettingsProvider settings, IGraphicsOptionsAdapter options, IModLogger logger)
    {
        _settings = settings;
        _options = options;
        _logger = logger;
    }

    public override void OnCreated()
    {
        _missionNumber = Interlocked.Increment(ref _missionsInProcess);
        _missionStart = Stopwatch.GetTimestamp();
        _clock.Reset();
        _firstTickDone = false;
        _disabled = false;
        try
        {
            _topN = _settings.TickProfilerTopN;
            _hitchMs = _settings.HitchThresholdMs;
            _toggleOn = _settings.TickProfilerEnabled;
            var hookProblems = MissionTickProfilerHooks.Installed && _toggleOn ? MissionTickProfilerHealth.Problems() : Array.Empty<string>();
            _measuring = MissionTickProfilerHooks.Installed && _toggleOn && hookProblems.Length == 0;
            _generation = MissionTickProfilerHooks.BeginMission(_missionStart, _measuring, _hitchMs);
            LogMissionStatus(hookProblems);
        }
        catch (Exception ex)
        {
            Fail(ex);
        }
    }

    public override void OnMissionTick(float dt)
    {
        if (_disabled)
            return;
        try
        {
            if (!_firstTickDone)
                FirstTick();

            var profiler = MissionTickProfilerHooks.Profiler;
            if (!_measuring || profiler == null || !profiler.Measuring || profiler.Generation != _generation)
                return;
            var nowSeconds = (Stopwatch.GetTimestamp() - _missionStart) / (double)Stopwatch.Frequency;
            MissionTickProfilerHealth.StopIfSitesLost(_logger, _generation, _missionNumber, nowSeconds);
            if (!_clock.ShouldEmit(nowSeconds))
                return;
            _clock.Emit(nowSeconds);
            _logger.LogInfo(TickProfileLines.BuildTickProfile(nowSeconds, profiler.TakeWindow(_topN), AllocationCounter.Available));
        }
        catch (Exception ex)
        {
            Fail(ex);
        }
    }

    protected override void OnEndMission()
    {
        try
        {
            if (_measuring)
                MissionTickProfilerHooks.WriteSummary(_generation, _topN);
            MissionTickProfilerHooks.EndMission(_generation);
        }
        catch { /* diagnostic only: never throw out of a mission's end */ }
    }

    private void FirstTick()
    {
        _firstTickDone = true;
        var context = PerfContextReader.Read(Mission, _options, _settings, _missionNumber, _measuring, out var contextFault);
        _logger.LogInfo(TickProfileLines.BuildPerfContext(context));
        if (contextFault != null)
            _logger.LogInfo(contextFault);
    }

    private void LogMissionStatus(string[] hookProblems)
    {
        if (_measuring)
        {
            _logger.LogInfo(TickProfileLines.BuildMissionStartLine(_missionNumber, _topN, _hitchMs,
                MissionTickProfilerHooks.OnTickSites, MissionTickProfilerHooks.OnPreTickSites,
                MissionTickProfilerHooks.WaitTickCompletionCall != null ? 2 : 1));
            var mcm = BattleLoadDiagnosticsSettings.Instance;
            if (mcm != null)
                foreach (var line in TickProfileLines.SettingFallbackLines(mcm.TickProfilerTopN, _topN, mcm.HitchThresholdMs, _hitchMs))
                    _logger.LogWarning(line);
        }
        else if (_toggleOn && hookProblems.Length > 0)
            _logger.LogWarning(TickProfileLines.BuildHooksMissingLine(_missionNumber, hookProblems));
        else if (_toggleOn)
            _logger.LogWarning(MissionTickProfilerHooks.Profiler == null ? TickProfileLines.RestartNeededLine : TickProfileLines.NotInstalledLine);
        else if (MissionTickProfilerHooks.Installed)
            _logger.LogInfo(TickProfileLines.BuildMissionOffLine(_missionNumber));
    }

    private void Fail(Exception ex)
    {
        _disabled = true;
        try
        {
            if (_generation != 0)
                MissionTickProfilerHooks.EndMission(_generation);
            _logger.LogError(TickProfileLines.BuildFault("mission behaviour", ex));
        }
        catch { /* diagnostic only */ }
    }
}
