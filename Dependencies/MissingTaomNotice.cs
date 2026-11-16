using System;
using System.Linq;
using System.Runtime.CompilerServices;
using TaleWorlds.Library;
using TaleWorlds.Localization;
using TaleWorlds.ModuleManager;
using TAOM.Dependencies.Foundation;

namespace TAOM.Dependencies;

/// <summary>
/// Engine boundary for <see cref="MissingTaomCheck"/>: one inquiry at the first main menu when this module loaded
/// without TAOM. TAOM.Dependencies constructs before Native (ModulesToLoadAfterThis), and Native's GauntletUISubModule
/// creates the inquiry manager in its own OnBeforeInitialModuleScreenSetAsRoot, after ours; InformationManager queues
/// nothing sent before that. So the hook only arms the notice, and the submodule tick loop that follows all modules' hooks in the same
/// Module.OnApplicationTick shows it. An
/// inquiry, not a chat line: the initial screen clears the chat log after the splash video. The text is plain English:
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
            // Wrong game version: GameVersionNotice turned TAOM's code off, so TAOM is listed but not running.
            var versionText = GameVersionNotice.MenuText;
            if (versionText != null)
            {
                DiagLog.Log("Dependencies", "MissingTaomNotice: showing the wrong-game-version notice");
                Show(versionText, quitOnOk: true);
                return;
            }

            var loaded = ModuleHelper.GetModules().Select(m => m.Id).ToList();
            var reason = MissingTaomCheck.Evaluate(loaded);
            DiagLog.Log("Dependencies", $"MissingTaomNotice: reason={reason}, loaded={string.Join(",", loaded)}");
            if (reason == MissingTaomReason.None) return;

            Show(Text(reason), quitOnOk: false);
        }
        catch (Exception ex)
        {
            DiagLog.LogCaught("Dependencies", "MissingTaomNotice", ex);
        }
    }

    /// <summary>
    /// Only the wrong-game-version popup closes the game (owner, 16 Nov 2026): TAOM's code is off, so there is
    /// nothing to play. Its button is Native's "Exit Game" (a Native key, so it translates without TAOM's
    /// language files). The missing-TAOM popup keeps a plain Ok that only closes the popup.
    /// </summary>
    private static void Show(string text, bool quitOnOk) =>
        InformationManager.ShowInquiry(new InquiryData(
            "TAOM", text, true, false,
            quitOnOk ? new TextObject("{=YbpzLHzk}Exit Game").ToString() : new TextObject("{=oHaWR73d}Ok").ToString(),
            string.Empty,
            quitOnOk ? QuitGame : (Action?)null, null));

    // NoInlining: the engine call sits here so a member missing on another game version fails this method, not Show.
    // Utilities.QuitGame is the call TAOM's autorun hosts use at the main menu.
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void QuitGame()
    {
        try { TaleWorlds.Engine.Utilities.QuitGame(); }
        catch (Exception ex) { DiagLog.LogCaught("Dependencies", "MissingTaomNotice.QuitGame", ex); }
    }

    // Plain English: the notice shows only when TAOM did not load, and TAOM's language files load only with TAOM.
    internal static string Text(MissingTaomReason reason) => reason == MissingTaomReason.WarSails
        ? "TAOM did not load because War Sails is ticked in the launcher, and TAOM does not work with War Sails. Close the game, untick War Sails in the launcher, check that TAOM is ticked, and start again."
        : "TAOM did not load, so this game is running without it. Close the game, tick TAOM in the launcher, and start again. If TAOM is already ticked, the launcher left it out: please report it and include your game's log files.";
}
