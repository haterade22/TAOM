using System.Diagnostics;
using System.IO;
using System.Text;
using TaleWorlds.Library;
using TaleWorlds.Localization;
using TAOM.Features.BattleLoadDiagnostics.Domain;

namespace TAOM.Features.BattleLoadDiagnostics;

// Player-facing surface for a battle-load stall DETECTED AFTER THE FACT. A hang can't show
// a dialog in the moment (the main thread is frozen), so we tell the player on the NEXT
// session's main menu, where the UI is live. Mirrors CrashReport's CrashNotifier: vanilla
// InformationManager.ShowInquiry with an "Open log folder" button. Localizes the title +
// buttons (the body embeds a file path, which can't be localized) — same scope as
// CrashNotifier. Soft wording: the marker can also survive a benign Alt-F4 during a load,
// so this is phrased as "may not have finished," not "crashed."
public static class StallReportNotifier
{
    public static void Notify(StallMarkerInfo info)
    {
        if (info == null) return;
        try
        {
            var folder = SafeFolder(info.LogFilePath);
            var inquiry = new InquiryData(
                titleText: new TextObject("{=taom_bld_stall_title}TAOM: last battle load may not have finished").ToString(),
                text: BuildMessage(info),
                isAffirmativeOptionShown: true,
                isNegativeOptionShown: !string.IsNullOrEmpty(folder),
                affirmativeText: new TextObject("{=taom_bld_stall_dismiss}Dismiss").ToString(),
                negativeText: new TextObject("{=taom_bld_stall_openfolder}Open log folder").ToString(),
                affirmativeAction: () => { /* dismiss */ },
                negativeAction: () => TryOpenInExplorer(info.LogFilePath, folder));
            InformationManager.ShowInquiry(inquiry, pauseGameActiveState: false);
        }
        catch
        {
            // Never break the main menu over a diagnostic notice — the marker is already
            // consumed and the log is on disk regardless.
        }
    }

    private static string BuildMessage(StallMarkerInfo info)
    {
        var sb = new StringBuilder();
        sb.AppendLine("A battle or scene load in your previous session did not finish "
                      + "(the game was closed while still loading).");
        sb.AppendLine();
        if (!string.IsNullOrEmpty(info.SceneName))
            sb.AppendLine($"Scene: {info.SceneName}");
        if (!string.IsNullOrEmpty(info.LogFilePath))
            sb.AppendLine($"Diagnostic log: {info.LogFilePath}");
        sb.AppendLine();
        sb.AppendLine("If you hit an endless loading screen, please attach that log to a "
                      + "bug report — it tells us exactly where the load stalled.");
        return sb.ToString();
    }

    private static string? SafeFolder(string? logPath)
    {
        if (string.IsNullOrEmpty(logPath)) return null;
        try { return Path.GetDirectoryName(logPath); }
        catch { return null; }
    }

    private static void TryOpenInExplorer(string? filePath, string? folder)
    {
        try
        {
            // /select highlights the log file; fall back to opening the folder.
            if (!string.IsNullOrEmpty(filePath) && File.Exists(filePath))
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = "explorer.exe",
                    Arguments = $"/select,\"{filePath}\"",
                    UseShellExecute = true,
                });
            }
            else if (!string.IsNullOrEmpty(folder))
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = "explorer.exe",
                    Arguments = $"\"{folder}\"",
                    UseShellExecute = true,
                });
            }
        }
        catch { }
    }
}
