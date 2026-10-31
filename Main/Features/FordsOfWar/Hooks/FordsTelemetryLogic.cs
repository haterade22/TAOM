using System;
using System.Diagnostics;
using System.Threading;
using TaleWorlds.Core;
using TaleWorlds.MountAndBlade;
using TaleWorlds.MountAndBlade.Source.Missions;
using TAOM.Adapters;
using TAOM.Features.FordsOfWar.Diagnostics;
using TAOM.Features.Warg;

namespace TAOM.Features.FordsOfWar.Hooks;

/// <summary>
/// The run report's eyes inside the mission: frame times, the escape menu's Options click, volley hits, the result, the end and the
/// warg rage log. Listed after <c>FordsOfWarMissionLogic</c>, so it ticks before it each frame (reverse order, Mission.cs:3751-3754)
/// and the mission clock advances before the director stamps anything; behaviors that <c>SubModule.OnMissionBehaviorInitialize</c>
/// appends later tick earlier still. Every override sits in a try (a throw out of a start callback restarts the load, #699) with a
/// five-fault cap. MUST be <c>: MissionLogic</c> (<c>OnMissionResultReady</c> reaches logics only, Mission.cs:4836-4841).
/// </summary>
internal sealed class FordsTelemetryLogic : MissionLogic
{
    private const int MaxFaultsLogged = 5;

    private readonly FordsRunReport _report;
    private WargRageLog? _warg;
    private MissionOptionsComponent? _options;
    private long _last;
    private double _wargAt;
    private int _faults;

    internal FordsTelemetryLogic(FordsRunReport report)
    {
        _report = report;
    }

    public override void AfterStart()
    {
        base.AfterStart();
        try
        {
            var options = Mission.GetMissionBehavior<MissionOptionsComponent>();
            _report.OptionsWired(options != null);
            if (options != null)
            {
                options.OnOptionsAdded += OnOptionsAdded;
                _options = options;
            }

            _warg = new WargRageLog(_report.Logger, () => _report.MissionSeconds);
            WargRageLog.Activate(_warg, Mission);
        }
        catch (Exception ex)
        {
            Fault("AfterStart", ex);
        }
    }

    public override void OnMissionTick(float dt)
    {
        try
        {
            var now = Stopwatch.GetTimestamp();
            var ms = _last == 0 ? -1.0 : (now - _last) * 1000.0 / Stopwatch.Frequency;
            _last = now;
            if (_report.MissionTick(dt, ms))
                _report.Window(Mission.AllAgents.Count, Mission.Agents.Count);

            if (_warg != null && _report.MissionSeconds >= _wargAt)
            {
                _wargAt = _report.MissionSeconds + 1.0;
                _warg.Watch();
                _report.Warg(_warg.Counts());
            }
        }
        catch (Exception ex)
        {
            Fault("OnMissionTick", ex);
        }
    }

    public override void OnAgentHit(Agent affectedAgent, Agent affectorAgent, in MissionWeapon affectorWeapon, in Blow blow,
        in AttackCollisionData attackCollisionData)
    {
        if (!blow.IsMissile)
            return;

        try
        {
            var hit = FordsVolleyHits.Read(affectedAgent, affectorAgent, in affectorWeapon, in blow, in attackCollisionData, out var filtered);
            if (hit != null)
                _report.VolleyHit(hit);
            else if (filtered != null)
                _report.VolleyHitFiltered(filtered);
        }
        catch (Exception ex)
        {
            Fault("OnAgentHit", ex);
        }
    }

    public override void OnMissionResultReady(MissionResult missionResult)
    {
        try
        {
            _report.ResultReady(missionResult.PlayerVictory);
        }
        catch (Exception ex)
        {
            Fault("OnMissionResultReady", ex);
        }
    }

    protected override void OnEndMission()
    {
        base.OnEndMission();
        try
        {
            try
            {
                if (_options != null)
                    _options.OnOptionsAdded -= OnOptionsAdded;
            }
            finally
            {
                WargRageLog.Deactivate(_warg);
            }

            if (_warg != null)
                _report.Warg(_warg.Counts());
            _report.MissionClosed();
        }
        catch (Exception ex)
        {
            Fault("OnEndMission", ex);
        }
    }

    private void OnOptionsAdded() => _report.OptionsRequested();

    private void Fault(string where, Exception ex)
    {
        if (Interlocked.Increment(ref _faults) <= MaxFaultsLogged)
            _report.Logger.LogError($"[FordsOfWar][Telemetry] {where} failed ({ex.GetType().Name}: {ex.Message})\n{ex.StackTrace}");
    }
}
