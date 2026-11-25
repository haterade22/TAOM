using System;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.Localization;

namespace TAOM.Features.CrashReport.UI;

// The player's side of the bug report: the offer after an unexpected end, and where the zip went. Vanilla inquiries,
// as CrashNotifier and StallReportNotifier; the paths go in as text variables so the sentences stay translatable.
public static class BugReportNotifier
{
    public static void OfferAfterUnexpectedEnd(Action makeReport)
    {
        try
        {
            InformationManager.ShowInquiry(new InquiryData(
                titleText: new TextObject("{=taom_bugreport_offer_title}TAOM: the last game ended unexpectedly").ToString(),
                text: new TextObject("{=taom_bugreport_offer_text}Your last game closed without TAOM's normal shutdown. That happens after a crash, after a freeze you had to close, or when the game was stopped some other way.{newline}{newline}Make a bug report now? TAOM puts the logs from that game into one zip file that you can attach to your report.").ToString(),
                isAffirmativeOptionShown: true,
                isNegativeOptionShown: true,
                affirmativeText: new TextObject("{=taom_bugreport_offer_yes}Make bug report").ToString(),
                negativeText: new TextObject("{=taom_bugreport_offer_no}No").ToString(),
                affirmativeAction: makeReport,
                negativeAction: () => { }), pauseGameActiveState: false);
        }
        catch (Exception) { } // swallow-ok: a notice must never break the main menu; the logs stay on disk
    }

    public static void ShowResult(string? zipPath, string? logsFolder)
    {
        try
        {
            var written = !string.IsNullOrEmpty(zipPath);
            var text = written
                ? new TextObject("{=taom_bugreport_done_text}Your bug report is ready:{newline}{PATH}{newline}{newline}Attach this file to your bug report and say what you were doing when the problem happened.")
                    .SetTextVariable("PATH", zipPath)
                : new TextObject("{=taom_bugreport_failed_text}The bug report could not be written. Please attach the newest taom_debug log from this folder instead:{newline}{PATH}")
                    .SetTextVariable("PATH", logsFolder ?? string.Empty);
            InformationManager.ShowInquiry(new InquiryData(
                titleText: new TextObject("{=taom_bugreport_done_title}TAOM bug report").ToString(),
                text: text.ToString(),
                isAffirmativeOptionShown: true,
                isNegativeOptionShown: written || !string.IsNullOrEmpty(logsFolder),
                affirmativeText: new TextObject("{=taom_bugreport_close}Close").ToString(),
                negativeText: new TextObject("{=taom_bugreport_open_folder}Open folder").ToString(),
                affirmativeAction: () => { },
                negativeAction: () => CrashNotifier.TryOpenInExplorer(written ? zipPath : logsFolder, select: written)),
                pauseGameActiveState: true);
        }
        catch (Exception) { } // swallow-ok: the zip is on disk whether or not the notice shows
    }
}
