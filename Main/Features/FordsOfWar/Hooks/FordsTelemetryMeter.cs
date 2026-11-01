using System;
using System.Diagnostics;
using TaleWorlds.Core;
using TaleWorlds.MountAndBlade;
using TaleWorlds.MountAndBlade.Source.Missions;
using TAOM.Adapters;
using TAOM.Features.CrashReport.Hooks;
using TAOM.Features.FordsOfWar.Diagnostics;
using TAOM.Features.Warg;

namespace TAOM.Features.FordsOfWar.Hooks;

/// <summary>
/// What <see cref="FordsTelemetryLogic"/> does for the run report, kept out of the mission logic so that the logic stays an entry point
/// (ADR-002): the frame clock and the 30 second window, the 1 Hz warg watch, the escape menu's Options click, the volley hit reader
/// and the swallowed-exception watch. It throws freely; the logic's overrides catch.
/// </summary>
internal sealed class FordsTelemetryMeter
{
    private readonly FordsRunReport _report;
    private WargRageLog? _warg;
    private MissionOptionsComponent? _options;
    private long _last;
    private double _wargAt;

    internal FordsTelemetryMeter(FordsRunReport report)
    {
        _report = report;
    }

    /// <summary>Mission start: wires the Options click, starts the warg log and begins listening for swallowed exceptions.</summary>
    internal void Arm(Mission mission)
    {
        UnwireOptions();
        var options = mission.GetMissionBehavior<MissionOptionsComponent>();
        _report.OptionsWired(options != null);
        if (options != null)
        {
            options.OnOptionsAdded += OnOptionsAdded;
            _options = options;
        }

        _warg = new WargRageLog(_report.Logger, () => _report.MissionSeconds);
        WargRageLog.Activate(_warg, mission);
        WatchExceptions();
    }

    /// <summary>Mission end: unhooks everything <see cref="Arm"/> hooked, hands the report the warg counts and closes the run.</summary>
    internal void Disarm()
    {
        try
        {
            StopWatchingExceptions();
            UnwireOptions();
        }
        finally
        {
            WargRageLog.Deactivate(_warg);
        }

        if (_warg != null)
            _report.Warg(_warg.Counts());
        _report.MissionClosed();
    }

    internal void Tick(float dt, Mission mission)
    {
        var now = Stopwatch.GetTimestamp();
        var ms = _last == 0 ? -1.0 : (now - _last) * 1000.0 / Stopwatch.Frequency;
        _last = now;
        if (_report.MissionTick(dt, ms))
            _report.Window(mission.AllAgents.Count, mission.Agents.Count);

        if (_warg != null && _report.MissionSeconds >= _wargAt)
        {
            _wargAt = _report.MissionSeconds + 1.0;
            _warg.Watch();
            _report.Warg(_warg.Counts());
        }
    }

    internal void VolleyHit(Agent affectedAgent, Agent affectorAgent, in MissionWeapon affectorWeapon, in Blow blow, in AttackCollisionData attackCollisionData)
    {
        var hit = FordsVolleyHits.Read(affectedAgent, affectorAgent, in affectorWeapon, in blow, in attackCollisionData, out var filtered);
        if (hit != null)
            _report.VolleyHit(hit);
        else if (filtered != null)
            _report.VolleyHitFiltered(filtered);
    }

    /// <summary>Starts counting the exceptions Harmony finalizers swallow into the run: the run's check 3 fails when any was.</summary>
    internal void WatchExceptions()
    {
        CrashReportPatchHelper.Swallowed -= OnSwallowed;
        CrashReportPatchHelper.Swallowed += OnSwallowed;
        _report.ExceptionsWatched();
    }

    internal void StopWatchingExceptions() => CrashReportPatchHelper.Swallowed -= OnSwallowed;

    private void UnwireOptions()
    {
        if (_options != null)
            _options.OnOptionsAdded -= OnOptionsAdded;
        _options = null;
    }

    private void OnOptionsAdded() => _report.OptionsRequested();

    private void OnSwallowed(Exception exception, string site) => _report.ExceptionSwallowed(exception.GetType().Name, site);
}
