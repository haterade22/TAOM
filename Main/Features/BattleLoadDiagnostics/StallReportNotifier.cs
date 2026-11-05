using System.Diagnostics;
using System.Globalization;
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
        string evidence = EvidenceSentence(info.Evidence);
        if (evidence.Length > 0) sb.AppendLine(evidence);
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

    // Claims only what the prior log shows. Main thread, at the main menu.
    private static string EvidenceSentence(StallEvidence? evidence)
    {
        switch (evidence?.Classify() ?? StallEvidenceKind.None)
        {
            case StallEvidenceKind.Compiling:
                return new TextObject("{=taom_bld_stall_compiling}The battle was still being prepared: the game's own log shows {COUNT} shader compiles after the loading screen stopped updating. The first battle after a game update or a change to your mods can take several minutes. Next time, give it at least 5 minutes before closing the game.")
                    .SetTextVariable("COUNT", (evidence!.FreezeRglCompiles ?? 0).ToString(CultureInfo.CurrentCulture)).ToString();
            case StallEvidenceKind.Stuck:
                return new TextObject("{=taom_bld_stall_stuck}The battle had stopped loading: the game's own log shows no shader compiles in the last {SECONDS} seconds before the game was closed. Please attach the log below to a bug report.")
                    .SetTextVariable("SECONDS", (evidence!.FreezeSilentSeconds ?? 0).ToString(CultureInfo.CurrentCulture)).ToString();
            case StallEvidenceKind.RenderWaitUnknown:
                return new TextObject("{=taom_bld_stall_render_unknown}The battle was waiting for its graphics to be prepared. After a game update or a change to your mods this can take several minutes, but the log cannot tell whether it was still working. If it happens again, wait 5 minutes before closing the game.").ToString();
            default:
                return string.Empty;
        }
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
