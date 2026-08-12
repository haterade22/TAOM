namespace TAOM.Features.Enlistment;

/// <summary>
/// Master gate for the routine <c>[EnlistDiag]</c> trace. It covers exactly five statements: TICK,
/// SYNC ok, PARK ok, RESTORE ok, and the high-volume "map event started and the commander's party
/// did NOT resolve" line. Nothing else in the feature consults it, so do not assume an absent
/// <c>[EnlistDiag]</c> line was gated — most of them are not.
///
/// OFF BY DEFAULT since 2026-08-09. The implementation resolves a missing MCM setting with
/// <c>?? false</c>, and that fallback must always agree with the compiled default in
/// <c>TaomSettings.EnableEnlistmentDiagnostics</c>, or MCM-absent behaviour would silently differ
/// from MCM-present-at-default behaviour. Both are pinned by tests; flip them together or not at all.
///
/// MCM-absent is not hypothetical. A settings file written before the enlistment keys existed
/// carries none of them, so a real player session runs the compiled defaults with no MCM row to
/// look at. The 2026-08-12 field test was diagnosed against a config last written 2026-07-07 and
/// produced no TICK/SYNC/PARK/RESTORE line all session for exactly that reason.
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
    /// <summary>True when the routine enlistment trace should be written. Default false.</summary>
    bool IsEnabled { get; }
}
