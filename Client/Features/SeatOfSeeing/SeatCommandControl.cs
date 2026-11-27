using System.Globalization;
using TAOM.Client.Adapters;
using TAOM.Core.Logging;
using TAOM.Features.SeatOfSeeing.Domain;

namespace TAOM.Client.Features.SeatOfSeeing;

/// <summary>
/// The Seat's command mouse (Seat build-spec 6.4): a left click selects, a right click orders, a right drag draws a line. The screen tick only
/// captures the pointer into <see cref="SeatPointerBuffer"/>; <see cref="OnMissionTick"/> takes it once and does every projection, ray, selection
/// write and order, so fast time (which repeats the mission tick) acts once and a pause (a tick with a <c>dt</c> of 0) still acts. The decisions
/// are <see cref="SeatCommandRule"/>, <see cref="SeatClickRule"/>, <see cref="SeatSelectRule"/> and <see cref="SeatOrderRule"/>; the engine
/// is <see cref="ISeatCommandAdapter"/>. A button pressed over an order panel button never reaches it (the screen adapter takes that edge out).
/// </summary>
internal sealed class SeatCommandControl
{
    private readonly ISeatCommandAdapter _adapter;
    private readonly IOutcomeLog _outcomes;
    private readonly string _key;
    private readonly SeatPointerBuffer _buffer = new();
    private bool _leftArmed;
    private bool _rightDown;
    private bool _lineStarted;
    private bool _refusalNoted;

    internal SeatCommandControl(ISeatCommandAdapter adapter, IOutcomeLog outcomes, string missionKey)
    {
        _adapter = adapter;
        _outcomes = outcomes;
        _key = missionKey;
    }

    /// <summary>
    /// Called by the screen tick with what it read of the mouse. A frame with no button edge, no right button down and no line in the making
    /// has nothing to act on, so it is not kept and the mission tick does no work.
    /// </summary>
    internal void Capture(in SeatPointerFrame frame)
    {
        if (frame.HasEdge || frame.RightDown || _rightDown)
            _buffer.Capture(in frame);
    }

    /// <summary>A right button is down and a line may be drawn: the order flags are out.</summary>
    internal bool Drawing => _rightDown;

    /// <summary>The command mouse stopped being live (or the Seat ended): forgets a waiting frame, a held button and a previewed line.</summary>
    internal void Reset()
    {
        _buffer.Clear();
        _leftArmed = false;
        _rightDown = false;
        _lineStarted = false;
        _refusalNoted = false;
        _adapter.LineCancel();
    }

    /// <summary>From the mission tick, while live and seated. Acts on the captured frame once.</summary>
    internal void OnMissionTick()
    {
        if (!_buffer.TryTake(out var frame))
            return;

        var refusal = SeatCommandRule.Refusal(_adapter.ReadFacts());
        if (refusal != SeatCommandRefusal.None)
        {
            Refuse(frame, refusal);
            return;
        }

        _refusalNoted = false;
        if (frame.LeftPressed)
            _leftArmed = true;
        if (frame.LeftReleased && _leftArmed)
        {
            _leftArmed = false;
            Click(frame);
        }

        Right(frame);
    }

    // A refusal drops the edges of this frame and any line in the making: nothing is selected or ordered, and one skip line says why.
    private void Refuse(in SeatPointerFrame frame, SeatCommandRefusal refusal)
    {
        var edge = frame.HasEdge;
        if (_rightDown || _lineStarted)
        {
            _rightDown = false;
            _lineStarted = false;
            _adapter.LineCancel();
        }

        _leftArmed = false;
        if (!edge || _refusalNoted)
            return;

        _refusalNoted = true;
        var reason = SeatCommandRule.Reason(refusal);
        _outcomes.Skip(SeatChecks.CommandId, _key + ":" + reason, "mission=" + _key + " reason=" + reason);
    }

    // Left button let go: a click selects. A drag is the box select of a later stage and does nothing here.
    private void Click(in SeatPointerFrame frame)
    {
        var held = frame.Seconds - frame.LeftPressSeconds;
        if (!SeatClickRule.IsClick(frame.LeftPressPixelX, frame.LeftPressPixelY, frame.PixelX, frame.PixelY, held))
            return;

        var picked = _adapter.PickOwn(frame.PixelX, frame.PixelY);
        var action = SeatSelectRule.Decide(frame.Shift, picked.Found, picked.Selected, _adapter.SelectedCount);
        if (action == SeatSelectAction.None)
            return;

        _adapter.Select(action);
        _outcomes.Pass(SeatChecks.CommandId, _key + ":select", "mission=" + _key + " command=select");
    }

    private void Right(in SeatPointerFrame frame)
    {
        if (frame.RightPressed)
        {
            _rightDown = true;
            _lineStarted = _adapter.LineBegin(frame.PressRangedX, frame.PressRangedY);
        }

        if (!_rightDown)
            return;

        var held = frame.Seconds - frame.PressSeconds;
        var drag = SeatClickRule.IsDrag(frame.PressPixelX, frame.PressPixelY, frame.PixelX, frame.PixelY, held);
        if (frame.RightReleased)
        {
            _rightDown = false;
            var line = _lineStarted && drag;
            _lineStarted = false;
            if (line)
                CommitLine(frame);
            else
                RightClick(frame);
            return;
        }

        if (!frame.RightDown)
        {
            // The button went up with no release edge (another layer took it): nothing was commanded.
            _rightDown = false;
            _lineStarted = false;
            _adapter.LineCancel();
            return;
        }

        if (_lineStarted && drag)
            _adapter.LinePreview(frame.RangedX, frame.RangedY, frame.Ctrl);
    }

    private void CommitLine(in SeatPointerFrame frame)
    {
        if (_adapter.SelectedCount == 0)
        {
            _adapter.LineCancel();
            Skip("nothing-selected");
            return;
        }

        _adapter.LineCommit(frame.RangedX, frame.RangedY, frame.Ctrl);
        _outcomes.Pass(SeatChecks.CommandId, _key + ":line", "mission=" + _key + " command=line");
    }

    private void RightClick(in SeatPointerFrame frame)
    {
        _adapter.LineCancel();
        var anySelected = _adapter.SelectedCount > 0;
        var probe = _adapter.ProbeRight(frame.PressPixelX, frame.PressPixelY, frame.PressRangedX, frame.PressRangedY);
        var kind = SeatOrderRule.Decide(anySelected, probe.Enemy, probe.Orderable, probe.ValidGround);
        switch (kind)
        {
            case SeatOrderKind.Charge:
                _adapter.Charge();
                break;
            case SeatOrderKind.Orderable:
                _adapter.UseOrderable();
                break;
            case SeatOrderKind.Move:
                _adapter.MoveToProbed(frame.Ctrl);
                break;
            default:
                Skip(SeatOrderRule.Reason(anySelected));
                return;
        }

        var word = kind.ToString().ToLower(CultureInfo.InvariantCulture);
        _outcomes.Pass(SeatChecks.CommandId, _key + ":" + word, "mission=" + _key + " command=" + word);
    }

    private void Skip(string reason) =>
        _outcomes.Skip(SeatChecks.CommandId, _key + ":" + reason, "mission=" + _key + " reason=" + reason);
}
