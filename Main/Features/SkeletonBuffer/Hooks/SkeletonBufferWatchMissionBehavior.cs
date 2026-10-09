// Adapted from yotthani's VanillaTuning (MIT, (c) 2026 yotthani), frame-buffer-guard and frame-buffer-watch.
using System;
using System.Diagnostics;
using TaleWorlds.MountAndBlade;

namespace TAOM.Features.SkeletonBuffer.Hooks;

/// <summary>
/// Hands the mission's start, tick and end to <see cref="SkeletonBufferWatchService"/> (docs/features/skeleton-buffer-guard.md).
/// Thin entry point per ADR-002: the reading, the peak, the warning and the lines are the service's.
/// </summary>
public sealed class SkeletonBufferWatchMissionBehavior : MissionLogic
{
    private readonly SkeletonBufferWatchService _watch;
    private readonly Func<int> _agentCount;
    private long _start;

    public SkeletonBufferWatchMissionBehavior(SkeletonBufferWatchService watch)
    {
        _watch = watch;
        _agentCount = CountAgents;
    }

    // v1.5.4 caller: Mission.AfterStart runs `missionBehavior.AfterStart()` over every behavior after the submodules that add
    // TAOM's have run (the lifecycle table in .claude/rules/harmony-patches.md), so a behavior TAOM adds is reached here and
    // never in OnBehaviorInitialize (#606). Under the loading screen, before the first tick. The service never throws.
    public override void AfterStart()
    {
        _start = Stopwatch.GetTimestamp();
        _watch.Begin();
    }

    // Main thread: Mission.OnTick calls every behavior's OnMissionTick (the frame order in .claude/rules/harmony-patches.md).
    public override void OnMissionTick(float dt) => _watch.Tick(_agentCount, Seconds());

    // Leaving the mission through Mission.EndMission reaches OnEndMission (MissionBehavior.OnEndMissionInternal).
    protected override void OnEndMission() => _watch.End();

    // A teardown that never calls Mission.EndMission skips OnEndMission; OnMissionStateFinalize still removes every
    // behavior, so the peak line is also written here. The service writes it once.
    public override void OnRemoveBehavior()
    {
        _watch.End();
        base.OnRemoveBehavior();
    }

    private int CountAgents() => Mission?.AllAgents?.Count ?? 0;

    private double Seconds() => (Stopwatch.GetTimestamp() - _start) / (double)Stopwatch.Frequency;
}
