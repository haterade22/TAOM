using System;

namespace TAOM.Features.ImeGuard;

/// <summary>What the IME guard reads each tick: the foreground window, whether it is this game's, and whether a
/// text field has keyboard focus.</summary>
public readonly struct ImeWindowState
{
    public ImeWindowState(IntPtr window, bool ownForeground, bool textFocus)
    {
        Window = window;
        OwnForeground = ownForeground;
        TextFocus = textFocus;
    }

    public IntPtr Window { get; }

    public bool OwnForeground { get; }

    public bool TextFocus { get; }
}

/// <summary>
/// The Windows and screen calls behind the IME guard. The client adapter runs the input-context calls on the window's
/// own thread. Core registers a null adapter, which never reports the game in front, so core resolves without the client
/// (ADR-013); a dedicated server never loads the client and never ticks the guard.
/// </summary>
public interface IImeWindowAdapter
{
    ImeWindowState Read();

    /// <summary>Detaches the window's input context and keeps it to put back.</summary>
    void Detach(IntPtr window);

    /// <summary>Puts back the context <see cref="Detach"/> took, if any.</summary>
    void Reattach();

    /// <summary>At unload: removes anything native that calls back into managed code.</summary>
    void Shutdown();
}

/// <summary>Core's adapter: never in front, so the guard never detaches.</summary>
public sealed class NullImeWindowAdapter : IImeWindowAdapter
{
    public ImeWindowState Read() => default;

    public void Detach(IntPtr window) { }

    public void Reattach() { }

    public void Shutdown() { }
}
