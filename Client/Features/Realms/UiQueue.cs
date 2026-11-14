using System;
using TaleWorlds.Library;

namespace TAOM.Client.Features.Realms;

/// <summary>
/// Runs a UI action on the next application tick. Opening a dialog from inside another dialog's
/// button callback can be swallowed while the first one is still closing, so every step waits a frame.
/// A story event window (<see cref="EventWindow.EventWindows"/>) opens only on a frame this queue had nothing to run,
/// so the step a dialog takes after its callback (refreshing the page) does not replace it.
/// </summary>
internal static class UiQueue
{
    private static Action? _next;

    public static void Post(Action? action)
    {
        if (action != null)
            _next = action;
    }

    /// <summary>Runs the queued step, if any; false when there was none.</summary>
    public static bool Run()
    {
        var action = _next;
        _next = null;
        if (action == null)
            return false;
        try
        {
            action();
        }
        catch (Exception ex)
        {
            Debug.Print($"[TAOM.Realms] UI error: {ex}");
            InformationManager.DisplayMessage(new InformationMessage("TAOM.Realms: the titles window failed, see the game log.", Colors.Red));
        }

        return true;
    }
}
