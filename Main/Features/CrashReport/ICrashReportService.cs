using System;
using TAOM.Features.CrashReport.Domain;

namespace TAOM.Features.CrashReport;

// Composes an ExceptionContext from every collector, writes it to TAOM's debug log,
// writes a JSON+text bundle ZIP under Logs/, optionally suspends BUTR's exception
// handler, and surfaces a one-line player-facing notification.
//
// Called from Harmony Finalizers and AppDomain.UnhandledException. Re-entry is
// protected by an internal thread-static guard — if HandleException is called while
// another handle is in progress on the same thread, the call is short-circuited.
public interface ICrashReportService
{
    // Capture + persist + notify. Returns the path to the report ZIP, or null if
    // even the report could not be written. Never throws — the caller is a
    // Finalizer that must not propagate diagnostic failures into the main exception.
    string? HandleException(Exception exception, string originatingPatchTarget);

    // Returns true while a HandleException invocation is on the current call stack —
    // Harmony Finalizers should consult this to break re-entry loops.
    bool IsHandling { get; }
}
