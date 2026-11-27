using System.Globalization;
using TAOM.Client.Adapters;
using TAOM.Core.Logging;
using TAOM.Features.SeatOfSeeing.Domain;

namespace TAOM.Client.Features.SeatOfSeeing;

/// <summary>The Seat's command mouse (Seat build-spec 6.4): a left click selects, a right click orders, a right drag draws a line.</summary>
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
    private bool _panelOpen;

    internal SeatCommandControl(ISeatCommandAdapter adapter, IOutcomeLog outcomes, string missionKey)
    {
        _adapter = adapter;
        _outcomes = outcomes;
        _key = missionKey;
    }

    /// <summary>Called by the screen tick with what it read of the mouse; a frame with nothing to act on is not kept.</summary>
    internal void Capture(in SeatPointerFrame frame)
    {
        if (frame.HasEdge || frame.RightDown || _rightDown)
            _buffer.Capture(in frame);
    }

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

    /// <summary>The Seat is finishing: forgets everything with no native call (the scene may be gone), unlike <see cref="Reset"/>, which hides the preview.</summary>
    internal void Finish()
    {
        _buffer.Clear();
        _leftArmed = _rightDown = _lineStarted = _refusalNoted = false;
        _adapter.Drop();
    }

    /// <summary>From the mission tick, while live and seated. Acts on the captured frame once.</summary>
    internal void OnMissionTick()
    {
        if (!_buffer.TryTake(out var frame))
            return;

        var facts = _adapter.ReadFacts();
        _panelOpen = facts.OrderMenuOpen;
        var refusal = SeatCommandRule.Refusal(facts);
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

    private void Click(in SeatPointerFrame frame)
    {
        var held = frame.Seconds - frame.LeftPressSeconds;
        if (!SeatClickRule.IsClick(frame.LeftPressPixelX, frame.LeftPressPixelY, frame.PixelX, frame.PixelY, held))
            return;

        var picked = _adapter.PickOwn(frame.PixelX, frame.PixelY);
        var action = SeatSelectRule.Decide(frame.Shift, picked.Found, picked.Selected, _adapter.SelectedCount, _panelOpen);
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
