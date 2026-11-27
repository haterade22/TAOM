namespace TAOM.Features.SeatOfSeeing.Domain;

/// <summary>
/// What the screen tick captures of the mouse: the button edges and levels, the pointer as a pixel and as a fraction of the window, and
/// the two modifier keys. It holds no engine type, so the tick that captures it never projects, casts a ray or writes an order; the first mission
/// tick of the frame does (as the Seat's keep and take do). The press fields name where and when each button last went down.
/// </summary>
internal readonly record struct SeatPointerFrame(
    bool LeftPressed, bool LeftReleased, bool RightPressed, bool RightReleased, bool RightDown,
    float PixelX, float PixelY, float RangedX, float RangedY, bool Shift, bool Ctrl, float Seconds,
    float PressPixelX = 0f, float PressPixelY = 0f, float PressRangedX = 0f, float PressRangedY = 0f, float PressSeconds = 0f,
    float LeftPressPixelX = 0f, float LeftPressPixelY = 0f, float LeftPressSeconds = 0f)
{
    /// <summary>Any button edge: a click, a press or a release to act on.</summary>
    internal bool HasEdge => LeftPressed || LeftReleased || RightPressed || RightReleased;
}

/// <summary>
/// Frames captured by the screen tick, taken once by the next mission tick. Fast time repeats the mission tick several times a frame, and a pause
/// still ticks it with a <c>dt</c> of 0, so the buffer answers a frame once per capture and never twice. Edges of several captures before one take
/// are joined, so a press and its release are never lost; the levels and the pointer are the latest.
/// </summary>
internal sealed class SeatPointerBuffer
{
    private SeatPointerFrame _frame;
    private bool _fresh;
    private float _pressPixelX, _pressPixelY, _pressRangedX, _pressRangedY, _pressSeconds;
    private float _leftPixelX, _leftPixelY, _leftSeconds;

    /// <summary>A capture waits to be taken.</summary>
    internal bool Pending => _fresh;

    internal void Capture(in SeatPointerFrame captured)
    {
        if (captured.RightPressed)
        {
            _pressPixelX = captured.PixelX;
            _pressPixelY = captured.PixelY;
            _pressRangedX = captured.RangedX;
            _pressRangedY = captured.RangedY;
            _pressSeconds = captured.Seconds;
        }

        if (captured.LeftPressed)
        {
            _leftPixelX = captured.PixelX;
            _leftPixelY = captured.PixelY;
            _leftSeconds = captured.Seconds;
        }

        var wasFresh = _fresh;
        var before = _frame;
        _frame = captured with
        {
            LeftPressed = captured.LeftPressed || (wasFresh && before.LeftPressed),
            LeftReleased = captured.LeftReleased || (wasFresh && before.LeftReleased),
            RightPressed = captured.RightPressed || (wasFresh && before.RightPressed),
            RightReleased = captured.RightReleased || (wasFresh && before.RightReleased),
            PressPixelX = _pressPixelX,
            PressPixelY = _pressPixelY,
            PressRangedX = _pressRangedX,
            PressRangedY = _pressRangedY,
            PressSeconds = _pressSeconds,
            LeftPressPixelX = _leftPixelX,
            LeftPressPixelY = _leftPixelY,
            LeftPressSeconds = _leftSeconds,
        };
        _fresh = true;
    }

    /// <summary>The captured frame, once; false when nothing was captured since the last take.</summary>
    internal bool TryTake(out SeatPointerFrame frame)
    {
        frame = _frame;
        if (!_fresh)
            return false;
        _fresh = false;
        return true;
    }

    /// <summary>Forgets a waiting frame and the press: the Seat is left, or the command mouse stopped being live.</summary>
    internal void Clear()
    {
        _fresh = false;
        _frame = default;
        _pressSeconds = 0f;
        _leftSeconds = 0f;
    }
}
