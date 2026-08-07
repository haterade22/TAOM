namespace TAOM.Features.Enlistment;

/// <summary>
/// Master gate for the routine <c>[EnlistDiag]</c> trace (TICK, SYNC ok, PARK ok, RESTORE ok, and
/// the per-map-event line). Off by default — this is a diagnostic, not a gameplay feature, and it
/// costs thousands of disk-written lines per session when on.
///
/// FAIL-CLOSED, DELIBERATELY. The implementation resolves a missing MCM setting with <c>?? false</c>,
/// which is INVERTED against <see cref="TAOM.Features.BattleLoadDiagnostics.IBattleLoadDiagnosticsSettingsProvider"/>'s
/// <c>?? true</c>. That provider's "diagnose now" posture is correct for a load-stall watchdog that
/// only speaks when something is already wrong. This one gates a high-volume routine trace: resolving
/// an absent instance to <c>true</c> would re-enable the flood for every player whose MCM failed to
/// load, which is exactly the problem the toggle exists to remove.
///
/// The toggle gates VOLUME, not the tag. Fault lines (park/sync/restore failure, a stranded
/// PlayerEncounter, a discharge that leaves the player unable to start encounters) keep the
/// <c>[EnlistDiag]</c> prefix and log regardless of this setting, because that prefix is the grep
/// handle a user needs. "Toggle off" therefore does NOT mean "zero [EnlistDiag] lines".
/// </summary>
public interface IEnlistmentDiagnosticsSettingsProvider
{
    /// <summary>True when the player has opted into the routine enlistment trace. Default false.</summary>
    bool IsEnabled { get; }
}
