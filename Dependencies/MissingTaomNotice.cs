using System;
using System.Linq;
using TaleWorlds.Library;
using TaleWorlds.Localization;
using TaleWorlds.ModuleManager;
using TAOM.Dependencies.Foundation;

namespace TAOM.Dependencies;

/// <summary>
/// Engine boundary for <see cref="MissingTaomCheck"/>: one inquiry at the first main menu when this module loaded
/// without TAOM. TAOM.Dependencies constructs before Native (ModulesToLoadAfterThis), and Native's GauntletUISubModule
/// creates the inquiry manager in its own OnBeforeInitialModuleScreenSetAsRoot, after ours; InformationManager queues
/// nothing sent before that. So the hook only arms the notice and the first application tick after it shows it. An
/// inquiry, not a chat line: the initial screen clears the chat log after the splash video. The text is inline English:
/// TAOM's language files load only with TAOM, which is exactly the module missing here. Never throws.
/// </summary>
internal static class MissingTaomNotice
{
    private static bool _armed;
    private static bool _done;

    /// <summary>From OnBeforeInitialModuleScreenSetAsRoot, which the engine calls again on every return to the menu.</summary>
    internal static void Arm()
    {
        if (!_done) _armed = true;
    }

    /// <summary>From OnApplicationTick. Shows the notice at most once per process.</summary>
    internal static void Tick()
    {
        if (!_armed || _done) return;
        _armed = false;
        _done = true;
        try
        {
            var loaded = ModuleHelper.GetModules().Select(m => m.Id).ToList();
            var reason = MissingTaomCheck.Evaluate(loaded);
            DiagLog.Log("Dependencies", $"MissingTaomNotice: reason={reason}, loaded={string.Join(",", loaded)}");
            if (reason == MissingTaomReason.None) return;

            InformationManager.ShowInquiry(new InquiryData(
                "TAOM", Text(reason), true, false,
                new TextObject("{=oHaWR73d}Ok").ToString(), string.Empty, null, null));
        }
        catch (Exception ex)
        {
            DiagLog.LogCaught("Dependencies", "MissingTaomNotice", ex);
        }
    }

    internal static string Text(MissingTaomReason reason) => reason == MissingTaomReason.WarSails
        ? new TextObject("{=taom_missing_taom_war_sails}TAOM did not load because War Sails is ticked in the launcher, and TAOM does not work with War Sails. Close the game, untick War Sails in the launcher, check that TAOM is ticked, and start again.").ToString()
        : new TextObject("{=taom_missing_taom_not_loaded}TAOM did not load, so this game is running without it. Close the game, tick TAOM in the launcher, and start again. If TAOM is already ticked, it failed to load: please report it and include your game's log files.").ToString();
}
