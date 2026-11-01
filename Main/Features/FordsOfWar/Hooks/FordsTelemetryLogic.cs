using System;
using System.Threading;
using TaleWorlds.Core;
using TaleWorlds.MountAndBlade;
using TAOM.Features.FordsOfWar.Diagnostics;

namespace TAOM.Features.FordsOfWar.Hooks;

/// <summary>
/// The run report's eyes inside the mission: frame times, the escape menu's Options click, volley hits, the result, the end, the
/// warg rage log and the exceptions Harmony finalizers swallow. The work is <see cref="FordsTelemetryMeter"/>'s; this class is the
/// entry point. Listed after <c>FordsOfWarMissionLogic</c>, so it ticks before it each frame (reverse order, Mission.cs:3751-3754)
/// and the mission clock advances before the director stamps anything; behaviors that <c>SubModule.OnMissionBehaviorInitialize</c>
/// appends later tick earlier still. Every override sits in a try (a throw out of a start callback restarts the load, #699) with a
/// five-fault cap. MUST be <c>: MissionLogic</c> (<c>OnMissionResultReady</c> reaches logics only, Mission.cs:4836-4841).
/// </summary>
internal sealed partial class FordsTelemetryLogic : MissionLogic
{
    private const int MaxFaultsLogged = 5;

    private readonly FordsRunReport _report;
    private readonly FordsTelemetryMeter _meter;
    private int _faults;

    internal FordsTelemetryLogic(FordsRunReport report)
    {
        _report = report;
        _meter = new FordsTelemetryMeter(report);
    }

    public override void AfterStart()
    {
        base.AfterStart();
        try
        {
            _meter.Arm(Mission);
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
            _meter.Tick(dt, Mission);
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
            _meter.VolleyHit(affectedAgent, affectorAgent, in affectorWeapon, in blow, in attackCollisionData);
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
            _meter.Disarm();
        }
        catch (Exception ex)
        {
            Fault("OnEndMission", ex);
        }
    }

    private void Fault(string where, Exception ex)
    {
        if (Interlocked.Increment(ref _faults) <= MaxFaultsLogged)
            _report.Logger.LogError($"[FordsOfWar][Telemetry] {where} failed ({ex.GetType().Name}: {ex.Message})\n{ex.StackTrace}");
    }
}
