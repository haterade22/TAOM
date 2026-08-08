namespace TAOM.Features.Enlistment;

/// <summary>
/// Master gate for the routine <c>[EnlistDiag]</c> trace (TICK, SYNC ok, PARK ok, RESTORE ok, and
/// the per-map-event line). ON by default while the enlistment service loop is under active
/// diagnosis; the MCM checkbox is how it gets turned down.
///
/// FAIL-OPEN, DELIBERATELY. The implementation resolves a missing MCM setting with <c>?? true</c>,
/// matching <see cref="TAOM.Features.BattleLoadDiagnostics.IBattleLoadDiagnosticsSettingsProvider"/>'s
/// "diagnose now" posture. The fallback must always agree with the compiled default in
/// <c>TaomSettings.EnableEnlistmentDiagnostics</c>, or MCM-absent behaviour would silently differ
/// from MCM-present-at-default behaviour. Both are pinned by tests; flip them together or not at all.
///
/// WHEN ON, THE GATED LINES EMIT AT INFO, NOT DEBUG. That is the point of having a toggle at all:
/// DEBUG is <c>FileLogger</c>'s async queue, and a hard native CTD drops whatever is still queued —
/// so under a DEBUG design the lines you switched on to diagnose with are exactly the ones lost when
/// the game dies. There is no middle state: ON means a durable, crash-surviving trace; OFF means the
/// line is never built or written at all (the gate wraps the statement, so its interpolation and any
/// argument-expression work are skipped too).
///
/// The toggle gates VOLUME, not the tag. Fault lines (park/sync/restore failure, a stranded
/// PlayerEncounter, a discharge that leaves the player unable to start encounters) keep the
/// <c>[EnlistDiag]</c> prefix and log regardless of this setting, because that prefix is the grep
/// handle a user needs. "Toggle off" therefore does NOT mean "zero [EnlistDiag] lines".
/// </summary>
public interface IEnlistmentDiagnosticsSettingsProvider
{
    /// <summary>True when the routine enlistment trace should be written. Default true.</summary>
    bool IsEnabled { get; }
}
