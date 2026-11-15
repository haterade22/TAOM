using System;
using System.Globalization;

namespace TAOM.Features.CrashReport.BugReport;

// Pure rules of the player's bug report (docs/features/crash-report.md, "Bug report"): which files count, and when the
// previous session ended unexpectedly. The disk walk is BugReportSources; these stay testable without it.
public static class BugReportFiles
{
    // SubModule.OnSubModuleUnloaded writes this line on every quit to desktop. A taom_debug log that ends without it
    // means the process died: a native crash, a freeze the player closed, or a killed process.
    public const string CleanExitLine = "[Shutdown] clean module unload";

    // A minidump of a large session runs to hundreds of MB (#385: 1.3 GB). Bigger files stay in the engine's crashes
    // folder and the manifest says so; the rest of the report stays small enough to send.
    public const long MaxCrashFileBytes = 64L * 1024 * 1024;

    private const string TaomLogPrefix = "taom_debug_";
    private const string TaomLogStampFormat = "yyyy-MM-dd_HH-mm-ss";

    public static bool EndedUnexpectedly(string? logTail) =>
        !string.IsNullOrEmpty(logTail) && logTail!.IndexOf(CleanExitLine, StringComparison.Ordinal) < 0;

    public static bool IsTaomLog(string fileName) =>
        fileName.StartsWith(TaomLogPrefix, StringComparison.OrdinalIgnoreCase)
        && fileName.EndsWith(".log", StringComparison.OrdinalIgnoreCase);

    // The engine writes rgl_log_<n>.txt and rgl_log_errors_<n>.txt side by side; the errors file is only the error lines.
    public static bool IsMainRglLog(string fileName) =>
        fileName.StartsWith("rgl_log_", StringComparison.OrdinalIgnoreCase)
        && !fileName.StartsWith("rgl_log_errors_", StringComparison.OrdinalIgnoreCase)
        && fileName.EndsWith(".txt", StringComparison.OrdinalIgnoreCase);

    // FileLogger names each log after the local time its session started: taom_debug_yyyy-MM-dd_HH-mm-ss.log.
    public static DateTime? SessionStartUtc(string taomLogFileName)
    {
        if (!IsTaomLog(taomLogFileName)) return null;
        var stamp = taomLogFileName.Substring(TaomLogPrefix.Length, taomLogFileName.Length - TaomLogPrefix.Length - ".log".Length);
        return DateTime.TryParseExact(stamp, TaomLogStampFormat, CultureInfo.InvariantCulture,
            DateTimeStyles.AssumeLocal, out var local)
            ? local.ToUniversalTime()
            : null;
    }

    public static bool IsFromSession(DateTime writtenUtc, DateTime? sessionStartUtc) =>
        sessionStartUtc.HasValue && writtenUtc >= sessionStartUtc.Value;
}
