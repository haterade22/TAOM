using System;
using TAOM.Dependencies.Foundation;
using TAOM.Features.CrashReport;

namespace TAOM.Features.CrashReport.Hooks;

// Shared sink for every Harmony Finalizer. Resolves the service lazily so the
// patch types stay decoupled from DryIoc resolution timing. Re-entry is
// guarded both here and inside the service.
//
// Lifecycle: `ResetForUnload()` MUST be called from `SubModule.OnSubModuleUnloaded`
// before `IoC.Dispose()`. Bannerlord can unload/reload a module in the same process;
// without the reset, the static `_service` cache would point at a disposed
// FileLogger after reload and every subsequent capture would silently drop log lines.
// Codex review #46 (2026-05-25) HIGH-01.
internal static class CrashReportPatchHelper
{
    [ThreadStatic] private static bool _onPatchStack;

    private static ICrashReportService? _service;

    // Returns the Exception that the Finalizer should re-throw (null = swallow).
    // Always swallows in v1 — players continue past the spike, full diagnostic
    // report is in the log and bundle ZIP. If the service is unreachable, the
    // exception bubbles out so vanilla / BUTR can take over.
    //
    // Runtime-gate on the MCM master toggle (HIGH-02 fix): if EnableCrashCapture is
    // off, we return the original exception so vanilla/BUTR can handle it, as the
    // toggle's hint says ("every TAOM crash finalizer passes exceptions straight through").
    //
    // Every hand-back goes through HandBack: the caller is a value-returning Finalizer, so
    // Harmony rethrows the result with `throw`, which would otherwise erase the throw site
    // (harmony-patches.md; maintainer decision 2026-09-24, #650).
    //
    // offMainThread: the caller's own thread verdict, passed through to the service (#650).
    public static Exception? HandleAndSwallow(Exception? exception, string originatingPatchTarget, bool offMainThread = false)
    {
        if (exception == null) return null;
        if (_onPatchStack) return HandBack(exception);

        // MCM master toggle. Default true so a missing/uninitialised settings instance
        // does not silently disable the feature.
        try
        {
            if (CrashReportSettings.Instance != null && !CrashReportSettings.Instance.EnableCrashCapture)
                return HandBack(exception);
        }
        catch { /* settings read failure should not affect the capture path */ }

        _onPatchStack = true;
        try
        {
            var svc = ResolveService();
            if (svc == null) return HandBack(exception);     // unreachable: let vanilla handle it
            svc.HandleException(exception, originatingPatchTarget, offMainThread);
            return null;                                      // swallow: game keeps ticking
        }
        catch { return HandBack(exception); }
        finally { _onPatchStack = false; }
    }

    // The Finalizer's original method is not passed down, so the marker line names none.
    private static Exception? HandBack(Exception exception)
        => RethrowStackPreserver.PreserveForRethrow(exception, null);

    // Module-unload lifecycle hook. Clears the cached service reference so the next
    // module load resolves a fresh CrashReportService graph from the new IoC container.
    public static void ResetForUnload()
    {
        _service = null;
    }

    private static ICrashReportService? ResolveService()
    {
        if (_service != null) return _service;
        try { _service = TAOM.IoC.Resolve<ICrashReportService>(); }
        catch { _service = null; }
        return _service;
    }
}
