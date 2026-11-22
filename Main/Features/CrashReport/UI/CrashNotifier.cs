using System;
using System.Diagnostics;
using System.IO;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.Localization;

namespace TAOM.Features.CrashReport.UI;

// v1 player-facing surface for a captured crash. Uses InformationManager.ShowInquiry
// (vanilla TaleWorlds dialog) — small but native, no Gauntlet movie risk. A richer
// Gauntlet overlay can replace this in v2; the InquiryData shape gives us two
// buttons (Continue + Open Bundle Folder).
//
// Called from CrashReportService AFTER the log + bundle are written.
public static class CrashNotifier
{
    public static void Notify(string exceptionType, string? bundlePath, string? logPath)
    {
        try
        {
            var msg = BuildMessage(exceptionType, bundlePath, logPath);
            var inquiry = new InquiryData(
                titleText: new TextObject("{=TAOM_CrashReport_Title}TAOM caught a crash").ToString(),
                text: msg,
                isAffirmativeOptionShown: true,
                isNegativeOptionShown: !string.IsNullOrEmpty(bundlePath),
                affirmativeText: new TextObject("{=TAOM_CrashReport_Continue}Continue (risky)").ToString(),
                negativeText: new TextObject("{=TAOM_CrashReport_OpenBundle}Open bundle folder").ToString(),
                affirmativeAction: () => { /* dismiss */ },
                negativeAction: () => TryOpenInExplorer(bundlePath));
            InformationManager.ShowInquiry(inquiry, pauseGameActiveState: true);
        }
        catch
        {
            // If even the inquiry fails to display (engine in shutdown, etc.) we've already
            // written the log + bundle — silently degrade.
        }
    }

    internal static string BuildMessage(string exceptionType, string? bundlePath, string? logPath)
    {
        var lines = new System.Text.StringBuilder();
        lines.AppendLine($"Exception: {exceptionType}");
        lines.AppendLine();
        lines.AppendLine("A full diagnostic report has been written to disk:");
        // FileLogger's path is relative to the game's working folder; a player cannot find "Logs\..." on their own.
        if (!string.IsNullOrEmpty(logPath)) lines.AppendLine($"  Log:    {FullPath(logPath!)}");
        if (!string.IsNullOrEmpty(bundlePath)) lines.AppendLine($"  Bundle: {bundlePath}");
        lines.AppendLine();
        lines.AppendLine("Continuing may produce additional errors if game state is corrupted. If you can, save and exit cleanly.");
        return lines.ToString();
    }

    private static string FullPath(string path)
    {
        try { return Path.GetFullPath(path); }
        catch { return path; }
    }

    // select: highlight the file in its folder; otherwise open the path as a folder. Shared with BugReportNotifier.
    internal static void TryOpenInExplorer(string? path, bool select = true)
    {
        if (string.IsNullOrEmpty(path)) return;
        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = "explorer.exe",
                Arguments = select ? $"/select,\"{path}\"" : $"\"{path}\"",
                UseShellExecute = true,
            });
        }
        catch { }
    }
}
