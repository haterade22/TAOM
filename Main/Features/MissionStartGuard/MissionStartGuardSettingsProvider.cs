// Adapted from yotthani's VanillaTuning (MIT, (c) 2026 yotthani), mission-start-guard.
using TAOM.Features.CrashReport;

namespace TAOM.Features.MissionStartGuard;

/// <summary>
/// Reads the CrashReport MCM page when an exception arrives, never at install. Fail-open to the shipped default
/// (on): an MCM that is not ready must not re-open the reload loop the guard closes.
/// </summary>
public sealed class MissionStartGuardSettingsProvider : IMissionStartGuardSettingsProvider
{
    public bool SurviveMissionStartFailures =>
        CrashReportSettings.Instance?.SurviveMissionStartFailures ?? true;
}
