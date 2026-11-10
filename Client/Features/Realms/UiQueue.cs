using System;
using TaleWorlds.Library;

namespace TAOM.Client.Features.Realms;

/// <summary>
/// Runs a UI action on the next application tick. Opening a dialog from inside another dialog's
/// button callback can be swallowed while the first one is still closing, so every step waits a frame.
/// A popup (<see cref="PostPopup"/>) waits until the steps posted with it have run, so the step a dialog
/// takes after its callback (refreshing the page) does not replace it.
/// </summary>
internal static class UiQueue
{
    private static Action? _next;
    private static Action? _popup;

    public static void Post(Action? action)
    {
        if (action != null)
            _next = action;
    }

    public static void PostPopup(Action? action)
    {
        if (action != null)
            _popup = action;
    }

    public static void Run()
    {
        var action = _next;
        _next = null;
        if (action == null)
        {
            action = _popup;
            _popup = null;
        }

        if (action == null)
            return;
        try
        {
            action();
        }
        catch (Exception ex)
        {
            Debug.Print($"[TAOM.Realms] UI error: {ex}");
            InformationManager.DisplayMessage(new InformationMessage("TAOM.Realms: the titles window failed, see the game log.", Colors.Red));
        }
    }
}
