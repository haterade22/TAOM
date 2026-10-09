// Adapted from yotthani's VanillaTuning (MIT, (c) 2026 yotthani), mission-start-guard.
namespace TAOM.Features.MissionStartGuard;

/// <summary>The MCM toggle that decides whether a throw out of a mission start call is survived.</summary>
public interface IMissionStartGuardSettingsProvider
{
    /// <summary>"Survive Mission Start Failures" (CrashReport page, Master group). Read when an exception arrives.</summary>
    bool SurviveMissionStartFailures { get; }
}
