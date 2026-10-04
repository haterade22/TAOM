using System;
using System.Diagnostics;
using System.Threading;
using TAOM.Adapters;
using TAOM.Core.Logging;
using TAOM.Features.BattleLoadDiagnostics;
using TaleWorlds.MountAndBlade;

namespace TAOM.Features.MissionPerf.Hooks;

/// <summary>
/// The per-mission side of the Patch98 hitch probe and the Patch97 tick profiler: one <c>[PerfContext]</c> per
/// mission at its first tick. A mission measures when Patch98 is installed and its toggle is on, or the profiler
/// is installed, on and every hook it needs is still in place ("full"; <see cref="MissionTickProfilerHealth"/>: the
/// install is a startup fact, so the hooks are read again at each mission start, the site count on every tick); the
/// profiler's header goes out at creation, the probe's mode header at the first tick, plus one warning per MCM knob
/// replaced by its default. Measuring, <c>[TickProfile]</c> and the probe's window lines go out on the heartbeat's
/// 5 s clock (same <see cref="FrameStats"/>, same <c>t</c>), and <c>[TickSummary]</c> plus <c>[TickSummaryExtra]</c>
/// at mission end; a mission not measuring with a toggle on says why. Thin (ADR-002). Overrides only
/// <c>OnCreated</c>, <c>OnMissionTick</c> and <c>OnEndMission</c>: <c>OnBehaviorInitialize</c> never fires for a
/// behaviour TAOM adds (<c>MissionBehaviorLifecycleTests</c>). A failure disables the behaviour for the mission with
/// one line, like the heartbeat. Its status lines at creation are
/// <see cref="MissionTickProfilerHooks.OnMissionCreated"/>'s.
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
    private bool _probeOn, _behaviorTiming;
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
            _probeOn = _settings.HitchProbeEnabled;
            var hookProblems = MissionTickProfilerHooks.Installed && _toggleOn ? MissionTickProfilerHealth.Problems() : Array.Empty<string>();
            _behaviorTiming = MissionTickProfilerHooks.Installed && _toggleOn && hookProblems.Length == 0;
            _measuring = _behaviorTiming || (HitchProbeInstaller.ProbeInstalled && _probeOn);
            _generation = MissionTickProfilerHooks.BeginMission(_missionStart, _measuring, _hitchMs, _behaviorTiming);
            MissionTickProfilerHooks.ConfigureProbeMission(_measuring, _behaviorTiming);
            MissionTickProfilerHooks.OnMissionCreated(_logger, _missionNumber, _topN, _hitchMs, measuring: _measuring,
                behaviorTiming: _behaviorTiming, profilerToggleOn: _toggleOn, hookProblems: hookProblems);
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
            if (_behaviorTiming)
                MissionTickProfilerHealth.StopIfSitesLost(_logger, _generation, _missionNumber, nowSeconds);
            if (!_clock.ShouldEmit(nowSeconds))
                return;
            _clock.Emit(nowSeconds);
            _logger.LogInfo(TickProfileLines.BuildTickProfile(nowSeconds, profiler.TakeWindow(_topN), AllocationCounter.Available));
            ProbeWindowWriter.WriteExtras(_logger, nowSeconds, profiler.TakeExtrasWindow(_topN));
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
            {
                MissionTickProfilerHooks.WriteSummary(_generation, _topN);
                MissionTickProfilerHooks.WriteSummaryExtra(_generation, _topN);
            }
            MissionTickProfilerHooks.EndMission(_generation);
        }
        catch { /* diagnostic only: never throw out of a mission's end */ }
    }

    private void FirstTick()
    {
        _firstTickDone = true;
        var context = PerfContextReader.Read(Mission, _options, _settings, _missionNumber, _behaviorTiming, out var contextFault);
        _logger.LogInfo(TickProfileLines.BuildPerfContext(context));
        if (contextFault != null)
            _logger.LogInfo(contextFault);
        MissionTickProfilerHooks.OnMissionFirstTick(_logger, _missionNumber, _hitchMs, _measuring, _behaviorTiming, _toggleOn, _probeOn);
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
