using System;
using System.Threading;
using TAOM.Core.Logging;
using TAOM.Features.CrashReport;

namespace TAOM.Features.CrashReport.Hooks;

// Subscribes to AppDomain.CurrentDomain.UnhandledException — the safety net for
// exceptions that escape every Harmony Finalizer. Bannerlord is a DirectX app,
// not WinForms, so we don't bother with Application.ThreadException (it would
// never fire and pulls a System.Windows.Forms reference for nothing).
//
// Idempotent: re-subscription is a no-op. Singleton-owned so we never leak handlers.
//
// Thread safety (Codex review #46 (2026-05-25) MED-03): unhandled exceptions can
// fire on TaleWorlds worker threads (TWParallel.For agent ticks). The captured
// main thread id is set at Subscribe() time; if OnUnhandled fires on a different
// thread, it tells CrashReportService so, and the service switches to reduced-capture
// mode (skip Mission/Campaign reads + skip UI inquiry).
public sealed class AppDomainExceptionHook
{
    private readonly ICrashReportService _service;
    private readonly IModLogger _logger;
    private bool _subscribed;

    // Static so Native2ManagedBridge reads the same boot-time id to mark its own off-thread
    // captures (maintainer decision 2026-09-24, #650). 0 until Subscribe() runs; managed thread
    // ids start at 1, so 0 never names a real thread.
    private static int _mainThreadId;

    internal static int MainThreadId => Volatile.Read(ref _mainThreadId);

    // The one definition of "off the main thread" for every capture source that can run on a
    // worker (this hook and Native2ManagedBridge). An unset id (0) never equals a real thread, so
    // it counts as off-main: the safe direction, since a main-thread capture then loses only its
    // Mission and Campaign sections and the inquiry.
    internal static bool IsOffMainThread(int mainThreadId)
        => Thread.CurrentThread.ManagedThreadId != mainThreadId;

    public AppDomainExceptionHook(ICrashReportService service, IModLogger logger)
    {
        _service = service;
        _logger = logger;
    }

    public void Subscribe()
    {
        if (_subscribed) return;
        _subscribed = true;
        // Subscribe() is called from SubModule.OnSubModuleLoad on the main game thread,
        // so capturing the current managed thread id here is the right reference for
        // "main thread" comparisons inside OnUnhandled.
        Volatile.Write(ref _mainThreadId, Thread.CurrentThread.ManagedThreadId);
        // Lets one launch confirm the premise: compare with MissionThreadGuard's "main N" lines.
        _logger.LogInfo($"[CrashReport] main thread id {_mainThreadId} recorded at Subscribe()");
        try { AppDomain.CurrentDomain.UnhandledException += OnUnhandled; }
        catch (Exception ex) { _logger.LogWarning($"[CrashReport] AppDomain.UnhandledException subscribe failed: {ex.GetType().Name}"); }
    }

    public void Unsubscribe()
    {
        if (!_subscribed) return;
        try { AppDomain.CurrentDomain.UnhandledException -= OnUnhandled; } catch { }
        _subscribed = false;
    }

    private void OnUnhandled(object sender, UnhandledExceptionEventArgs args)
    {
        try
        {
            // Master toggle gate (Codex HIGH-02 fix) — when disabled, do nothing and
            // let the next handler (BUTR / vanilla) take over.
            if (CrashReportSettings.Instance != null && !CrashReportSettings.Instance.EnableCrashCapture) return;

            var ex = args?.ExceptionObject as Exception;
            if (ex == null) return;

            _service.HandleException(ex, "AppDomain.UnhandledException", IsOffMainThread(MainThreadId));
        }
        catch { }
    }
}
