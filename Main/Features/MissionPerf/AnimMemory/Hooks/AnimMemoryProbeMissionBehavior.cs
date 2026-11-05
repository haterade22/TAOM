using System;
using System.Diagnostics;
using TaleWorlds.MountAndBlade;
using TAOM.Core.Logging;
using TAOM.Features.BattleLoadDiagnostics;

namespace TAOM.Features.MissionPerf.AnimMemory.Hooks;

/// <summary>
/// Logs the engine's on-demand animation clip memory against its 12 MiB budget in every mission, so
/// raising the budget rests on a measurement of budget pressure; making TAOM's hot clips resident also
/// needs hitch timing (docs/features/mission-perf-heartbeat.md).
/// The one-time signature scan runs in <see cref="AfterStart"/>, under the loading screen and before
/// this behavior's first tick (the engine's two preliminary 0.001 s ticks, MissionState.cs:338-341, run
/// before the behavior is added), so it costs load time and not a battle frame. It only reads engine memory.
/// Thin entry point per ADR-002: arming is <see cref="AnimMemoryProbe"/>, the sampling and lines are
/// <see cref="AnimMemorySession"/>.
/// </summary>
public sealed class AnimMemoryProbeMissionBehavior : MissionLogic
{
    private readonly IAnimClipMemoryProbe _probe;
    private readonly IModLogger _logger;
    private AnimMemorySession? _session;
    private long _start;

    public AnimMemoryProbeMissionBehavior(IAnimClipMemoryProbe probe, IModLogger logger)
    {
        _probe = probe;
        _logger = logger;
    }

    // v1.5.3 caller (v1.4.8 lines in brackets where they differ): Mission.AfterStart runs `cachedSubModule2.OnMissionBehaviorInitialize(this);`
    // (Mission.cs:3815 on v1.4.8, where AddTaomBehavior adds this behavior), then
    // `missionBehavior2.AfterStart();` over every behavior (:3825); MissionState.cs:345 calls it
    // between SetLoadingScreenPercentage(0.48f) and (0.56f), so it runs under the loading screen.
    public override void AfterStart()
    {
        _session = null;
        try
        {
            if (!(BattleLoadDiagnosticsSettings.Instance?.EnableAnimMemoryProbe ?? true))
            {
                _logger.LogInfo(AnimMemLine.OffForMission());
                return;
            }
            // A false return was already explained by the probe, once per process.
            if (!_probe.EnsureArmed())
                return;
            _start = Stopwatch.GetTimestamp();
            _session = new AnimMemorySession(_probe, _logger);
            _session.Start(0.0);
        }
        catch (Exception ex)
        {
            _session = null;
            try { _logger.LogError(AnimMemLine.Stopped(ex)); }
            catch { /* diagnostic only */ }
        }
    }

    // v1.5.3 caller (v1.4.8 lines in brackets where they differ): `MissionBehaviors[num2].OnMissionTick(dt);` (Mission.cs:3743 on v1.4.8), main thread.
    public override void OnMissionTick(float dt) => _session?.Tick(Seconds());

    // v1.5.3 caller (v1.4.8 lines in brackets where they differ): `missionBehavior.OnEndMissionInternal();` in Mission.EndMissionInternal
    // (Mission.cs:4585 on v1.4.8), which calls OnEndMission (MissionBehavior.cs:123). Leaving to the main
    // menu goes through it too: MBGameManager.EndGame calls EndMission and waits for the mission.
    protected override void OnEndMission() => EndSession();

    // v1.5.3 caller (v1.4.8 lines in brackets where they differ): `missionBehavior.OnRemoveBehavior();` in RemoveMissionBehavior (Mission.cs:4645 on v1.4.8),
    // which OnMissionStateFinalize runs for every behavior (:2219); MissionState.OnFinalize calls it
    // unconditionally (MissionState.cs:52). A teardown that never calls Mission.EndMission skips
    // EndMissionInternal and so OnEndMission, so the summary is written here when OnEndMission did
    // not run; after it did, the session is already null. (A network client that calls EndMission
    // still reaches EndMissionInternal, through CheckMissionEnd's else branch, :4885-4888.)
    public override void OnRemoveBehavior()
    {
        EndSession();
        base.OnRemoveBehavior();
    }

    private void EndSession()
    {
        _session?.End(Seconds());
        _session = null;
    }

    private double Seconds() => (Stopwatch.GetTimestamp() - _start) / (double)Stopwatch.Frequency;
}
