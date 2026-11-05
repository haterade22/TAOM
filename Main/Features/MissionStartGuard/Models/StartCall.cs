// Adapted from yotthani's VanillaTuning (MIT, (c) 2026 yotthani), mission-start-guard.
namespace TAOM.Features.MissionStartGuard.Models;

/// <summary>
/// The six start calls inside <c>Mission.AfterStart</c> that the guard wraps, named as the engine names them.
/// The name is what the log line and the on-screen message print.
/// </summary>
public enum StartCall
{
    OnBeforeMissionBehaviorInitialize,
    OnBehaviorInitialize,
    OnMissionBehaviorInitialize,
    EarlyStart,
    AfterStart,
    AfterMissionStart,
}
