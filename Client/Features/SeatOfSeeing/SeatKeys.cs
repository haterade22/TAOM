using TaleWorlds.InputSystem;
using TAOM.Features.SeatOfSeeing;
using TAOM.Features.SeatOfSeeing.Domain;

namespace TAOM.Client.Features.SeatOfSeeing;

/// <summary>The Seat's keys beyond "Take the Seat" (build-spec 6.0), tested once per screen tick so <see cref="SeatView"/> stays small.</summary>
internal sealed class SeatKeys
{
    private readonly SeatHotKeys _keys;
    private readonly SeatSession _session;

    internal SeatKeys(SeatHotKeys keys, SeatSession session)
    {
        _keys = keys;
        _session = session;
    }

    internal void Tick(IInputContext input)
    {
        if (_session.State != SeatState.Seated)
            return;
        if (Pressed(_keys.HideTheInterface, input))
            _session.PressHide();
        if (Pressed(_keys.KeepTo, input))
            _session.PressKeep();
    }

    // The static test is cheap; the view's own context test allocates a click key array on each call (InputContext.cs:310-312) and is the
    // one that goes quiet behind a menu.
    internal static bool Pressed(InputKey key, IInputContext input) =>
        key != InputKey.Invalid && TaleWorlds.InputSystem.Input.IsKeyPressed(key) && input.IsKeyPressed(key);
}
