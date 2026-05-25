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
// main thread id is set at Subscribe() time — if OnUnhandled fires on a different
// thread, we record a marker on the exception's Data dict so CrashReportService
// can switch to reduced-capture mode (skip Mission/Campaign reads + skip UI inquiry).
public sealed class AppDomainExceptionHook
{
    public const string OffMainThreadDataKey = "TAOM.CrashReport.OffMainThread";

    private readonly ICrashReportService _service;
    private readonly IModLogger _logger;
    private bool _subscribed;
    private int _mainThreadId;

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
        _mainThreadId = Thread.CurrentThread.ManagedThreadId;
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

            // Tag off-main-thread captures so the service can pick reduced-capture mode.
            if (Thread.CurrentThread.ManagedThreadId != _mainThreadId)
            {
                try { ex.Data[OffMainThreadDataKey] = true; } catch { /* exotic Exception types may have read-only Data */ }
            }

            _service.HandleException(ex, "AppDomain.UnhandledException");
        }
        catch { }
    }
}
