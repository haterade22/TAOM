using System;
using System.IO;
using TAOM.Core.Logging;
using TAOM.Features.CrashReport.UI;

namespace TAOM.Features.CrashReport.BugReport;

// The two ways a player reaches the bug report: the MCM button (MakeAndShow) and the offer on the first main menu
// after a session that ended without the clean unload line (CheckPreviousSession). A native crash or a frozen game
// the player had to close shows nothing at the time; this is the first moment TAOM can say anything.
public static class BugReportStartup
{
    // The main-menu hook runs on every return to the main menu; offer once per launch.
    private static bool _checked;

    // The launch's one turn. A battle-load stall notice uses it: its marker is gone after the first menu, so a
    // later return to the menu would otherwise offer a report about the session the player was already told of.
    internal static bool TakeTurn(bool stallNoticeShown)
    {
        if (_checked) return false;
        _checked = true;
        return !stallNoticeShown;
    }

    internal static void ResetTurnForTests() => _checked = false;

    public static void CheckPreviousSession(IModLogger logger, bool stallNoticeShown)
    {
        if (!TakeTurn(stallNoticeShown)) return;
        try
        {
            var current = logger.LogFilePath;
            if (string.IsNullOrEmpty(current)) return;
            var logsDir = Path.GetDirectoryName(Path.GetFullPath(current));
            var previous = PreviousUncleanLog(logsDir, current,
                CrashReportSettings.Instance?.OfferBugReportAfterUnexpectedEnd ?? true);
            if (previous == null) return;
            logger.LogWarning($"[CrashReport] the previous session ({Path.GetFileName(previous)}) ended without a " +
                              "clean exit; offering a bug report");
            BugReportNotifier.OfferAfterUnexpectedEnd(() => MakeAndShow("after-unexpected-end"));
        }
        catch (Exception ex)
        {
            logger.LogWarning($"[CrashReport] previous-session check failed: {ex.GetType().Name}");
        }
    }

    // The previous session's log when that session ended without the clean unload line and the offer is on; else null.
    internal static string? PreviousUncleanLog(string? logsDir, string? currentLogPath, bool offerEnabled)
    {
        if (!offerEnabled || string.IsNullOrEmpty(logsDir)) return null;
        var previous = BugReportSources.PreviousTaomLog(logsDir!, currentLogPath);
        return previous != null && BugReportFiles.EndedUnexpectedly(BugReportSources.ReadTail(previous)) ? previous : null;
    }

    public static void MakeAndShow(string reason)
    {
        string? path = null;
        try { path = IoC.Resolve<ICrashReportService>().WriteBugReport(reason); }
        catch (Exception ex)
        {
            try { IoC.Resolve<IModLogger>().LogError($"[CrashReport] bug report failed: {ex.GetType().Name}"); }
            catch (Exception) { } // swallow-ok: the notice below still tells the player where the logs are
        }
        BugReportNotifier.ShowResult(path, LogsFolder());
    }

    private static string? LogsFolder()
    {
        try { return Path.Combine(Environment.CurrentDirectory, "Logs"); }
        catch (Exception) { return null; } // swallow-ok: the notice then shows no folder
    }
}
