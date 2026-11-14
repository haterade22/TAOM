using System.Collections.Generic;
using TAOM.Adapters;
using TAOM.Client.Features.Realms.Popups;

namespace TAOM.Client.Features.EventWindow;

/// <summary>
/// The story event windows waiting to open, one at a time, oldest first. A window opens on a frame when no Realms popup
/// is open (they share one layer slot, <see cref="RealmsPopups"/>) and no step of a closing dialog is still queued; one
/// that cannot open stays queued for the next frame. Not saved: a window still waiting when the campaign ends is dropped.
/// </summary>
internal static class EventWindows
{
    /// <summary>The oldest windows drop past this, so a burst never piles up a long chain of windows.</summary>
    private const int MaxQueued = 8;

    private const string Movie = "TaomEventWindow";

    private static readonly Queue<TAOM.Adapters.EventWindow> Waiting = new Queue<TAOM.Adapters.EventWindow>();

    /// <summary>A window is waiting or showing (any Realms popup counts: they share the slot).</summary>
    public static bool IsOpen => Waiting.Count > 0 || RealmsPopups.IsOpen;

    public static void Enqueue(TAOM.Adapters.EventWindow window)
    {
        Waiting.Enqueue(window);
        while (Waiting.Count > MaxQueued)
            Waiting.Dequeue();
    }

    /// <summary>Per frame, after the queued dialog steps: opens the oldest waiting window when the slot is free.</summary>
    public static void Tick()
    {
        // Outside a game (back at the main menu) a waiting window never opens; the next game start clears it.
        if (Waiting.Count == 0 || RealmsPopups.IsOpen || TaleWorlds.Core.Game.Current == null)
            return;
        var window = Waiting.Dequeue();
        RealmsPopups.OpenEvent(Movie, close => new EventWindowVM(window, close, EventArt.Directory));
    }

    /// <summary>A new campaign starts with nothing waiting.</summary>
    public static void Clear() => Waiting.Clear();
}

/// <summary>The client's story event windows (ADR-013): replaces core's NullEventWindowAdapter in TaomClientComposition.</summary>
internal sealed class EventWindowAdapter : IEventWindowAdapter
{
    public bool IsOpen => EventWindows.IsOpen;

    public bool Show(TAOM.Adapters.EventWindow window)
    {
        if (window == null)
            return false;
        EventWindows.Enqueue(window);
        return true;
    }
}
