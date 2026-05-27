using System;
using System.IO;
using System.Text;

namespace TAOM.Dependencies.Foundation;

/// <summary>
/// Structured runtime diagnostic logger for TAOM.Dependencies defensive infrastructure.
/// Writes to <c>&lt;game&gt;/Modules/TAOM.Dependencies/diag.log</c> (see RuntimeLog.Path).
/// All API methods are nested try/catch-wrapped so a logging failure never escapes —
/// PatchShield / SaveShield Finalizers depend on DiagLog being safe to call from
/// inside an exception handler.
///
/// Threadsafe via single lock on a private object. Append-only writes.
///
/// BetaDeps parity (DR3 Phase 4 — 2026-05-25). Ports
/// BetaDeps.Foundation.DiagLog. Distinct from EarlyLog: EarlyLog buffers static-init
/// messages for Main's FileLogger to drain; DiagLog writes runtime events directly
/// to disk for forensic inspection without requiring Main to load.
/// </summary>
public static class DiagLog
{
    private static readonly object _lock = new();
    private static bool _headerWritten;

    /// <summary>Logs an informational message under the given tag.</summary>
    public static void Log(string tag, string message) => Write(tag, "INFO", message);

    /// <summary>Logs a caught exception under the given tag with a where-context.</summary>
    public static void LogCaught(string tag, string where, Exception ex)
    {
        try
        {
            var typeName = ex?.GetType().Name ?? "(null exception)";
            var msg = ex?.Message ?? "(no message)";
            Write(tag, "CAUGHT", $"{where}: {typeName}: {msg}");
        }
        catch
        {
            // Logger itself failed — nothing we can do; swallow to protect callers.
        }
    }

    private static void Write(string tag, string level, string message)
    {
        try
        {
            var path = RuntimeLog.Path;
            if (string.IsNullOrEmpty(path)) return;

            lock (_lock)
            {
                if (!_headerWritten)
                {
                    EnsureHeader(path);
                    _headerWritten = true;
                }

                var line = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff} [{level,-6}] [{tag}] {message}{Environment.NewLine}";
                File.AppendAllText(path, line, Encoding.UTF8);
            }
        }
        catch
        {
            // Logger failure must not propagate (BetaDeps invariant).
        }
    }

    private static void EnsureHeader(string path)
    {
        try
        {
            // First write of this session — append a session marker, never truncate.
            var marker =
                $"{Environment.NewLine}" +
                $"# ============================================================{Environment.NewLine}" +
                $"# TAOM.Dependencies session start: {DateTime.Now:yyyy-MM-dd HH:mm:ss}{Environment.NewLine}" +
                $"# ============================================================{Environment.NewLine}";
            File.AppendAllText(path, marker, Encoding.UTF8);
        }
        catch
        {
            // Header is best-effort.
        }
    }
}
