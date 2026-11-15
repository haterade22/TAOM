using System;
using System.Collections.Generic;
using System.Linq;
using TAOM.Core.Logging;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.Localization;

namespace TAOM.Features.StartupAlerts;

/// <summary>
/// Engine boundary for the startup alerts: every check's paragraph goes into ONE inquiry, so two
/// findings never compete for the screen. An inquiry, not a chat line: the initial screen clears the
/// chat log after the splash video (docs/reviews/lessons/localization-ui.md). The button reuses
/// vanilla's own "Ok" row, already translated. Never throws.
/// </summary>
public static class StartupAlertsEntry
{
    public static void Show(IModLogger logger, IReadOnlyList<string> paragraphs)
    {
        if (paragraphs.Count == 0) return;
        try
        {
            InformationManager.ShowInquiry(new InquiryData(
                new TextObject("{=taom_patch_apply_notice_title}TAOM").ToString(), string.Join("\n\n", paragraphs), true, false,
                new TextObject("{=oHaWR73d}Ok").ToString(), string.Empty, null, null));
        }
        catch (Exception e)
        {
            logger.LogWarning($"[StartupAlerts] alert not shown: {e.GetType().Name}");
        }
    }

    public static string WarSailsParagraph() =>
        new TextObject("{=taom_startup_alert_war_sails}War Sails is loaded, but TAOM does not support it. Close the game, untick War Sails in the launcher, and start again.").ToString();

    public static string LibraryCopiesParagraph(IReadOnlyList<BundledLibraryCopy> copies)
    {
        var text = new TextObject(
            "{=taom_bundled_library_copies_body}TAOM.Dependencies already provides Harmony, ButterLib, UIExtenderEx and MCM, so these separate copies are not needed:{NEWLINE}{NEWLINE}{LIST}{NEWLINE}{NEWLINE}Unsubscribe from a Steam Workshop copy. A copy in the Modules folder took the place of TAOM's own small module with that name: reinstall TAOM to put it back, and do not delete the folder, or mods that need the library stop loading.");
        text.SetTextVariable("NEWLINE", "\n");
        text.SetTextVariable("LIST", string.Join("\n", copies.Select(Line)));
        return text.ToString();
    }

    private static string Line(BundledLibraryCopy copy)
    {
        var line = copy.InWorkshop
            ? new TextObject("{=taom_bundled_library_copy_workshop}{NAME} {VERSION}, Steam Workshop")
            : new TextObject("{=taom_bundled_library_copy_modules}{NAME} {VERSION}, folder {FOLDER}");
        line.SetTextVariable("NAME", copy.Name);
        line.SetTextVariable("VERSION", copy.Version);
        line.SetTextVariable("FOLDER", copy.Folder);
        return "- " + line.ToString();
    }
}
