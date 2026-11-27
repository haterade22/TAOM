using System;
using System.Collections.Generic;
using System.Linq;
using TAOM.Features.SeatOfSeeing.Domain;

namespace TAOM.Tests.Features.SeatOfSeeing;

/// <summary>
/// The command mouse half of the stand-in: two companies of the player's side and one of the enemy's, a pointer whose clicks are applied in the next
/// mission tick (as the Seat's are), and the order each click gives, each fault switchable. A point's pixel names what it stands on: the pixel
/// x of a soldier is 100 plus his company, and 300 for the enemy.
/// </summary>
internal sealed partial class CommandTrialWorld
{
    private const float OwnPixelBase = 100f;
    private const float EnemyPixel = 300f;
    private const float GroundPixelY = 500f;

    internal bool CommandNeverLive;
    internal bool NoSecondCompany;
    internal bool NoEnemy;
    internal bool SoldierOffScreen;
    internal bool GroundOffScreen;
    internal bool SelectDoesNothing;
    internal bool ShiftActsAsPlain;
    internal bool MoveIgnoresTheClick;
    internal float MoveOffByMetres;
    internal bool DragActsAsClick;
    internal bool DragFacesAlongTheLine;
    internal bool ChargeGoesNowhere;
    internal int MouseReleases;
    internal bool BoxNeverShown;
    internal bool BoxStaysShown;
    internal bool BoxSelectsNothing;
    internal bool TransferDoesNothing;
    internal bool TransferLosesSoldiers;
    internal int TransferLeftBehind;

    private bool _commandOn;
    private int _firstIdx = -1;
    private int _secondIdx = -1;
    private int _chosenIdx = -1;
    private bool _chosenIsEnemy;
    private readonly HashSet<int> _mouseSelected = new();
    private readonly Queue<(bool Right, bool Shift, SeatMousePoint At, SeatMousePoint? To)> _clicks = new();
    private (float X, float Y)? _orderAt;
    private (float X, float Y)? _facing;
    private bool _charging;
    private bool _chargeTargetsEnemy;
    private readonly Queue<(SeatMousePoint From, SeatMousePoint To)> _leftDrags = new();
    private (SeatMousePoint From, SeatMousePoint To)? _boxDrag;
    private bool _boxShown;
    private bool _transferAsked;
    private readonly int[] _companySoldiers = { 60, 40 };

    public bool CommandLive => _commandOn && !CommandNeverLive && State == SeatState.Seated;

    public void SetCommandMouse(bool on) => _commandOn = on;

    public bool MouseChooseSoldier(bool otherCompany)
    {
        _chosenIsEnemy = false;
        _soldierDead = false;
        if (otherCompany)
        {
            if (NoSecondCompany)
                return false;
            _secondIdx = 1;
            _chosenIdx = 1;
            return true;
        }

        _firstIdx = 0;
        _chosenIdx = 0;
        _aimError = 0.5f;
        return SoldierAvailable;
    }

    public bool MouseChooseEnemy()
    {
        if (NoEnemy)
            return false;
        _chosenIsEnemy = true;
        _soldierDead = false;
        _chosenIdx = 0;
        _aimError = 0.5f;
        return true;
    }

    public void MouseSelectFirstCompany()
    {
        Note("mouse-select-first");
        Faulted |= FaultMidStep;
        _mouseSelected.Clear();
        _mouseSelected.Add(_firstIdx);
    }

    public SeatMousePoint MouseProjectChosen()
    {
        if (SoldierOffScreen || _chosenIdx < 0)
            return default;
        var x = _chosenIsEnemy ? EnemyPixel : OwnPixelBase + _chosenIdx;
        return new SeatMousePoint(true, x, 200f, x / 1000f, 200f / 1000f, x, 200f);
    }

    public SeatMousePoint MouseGround(float rightMetres)
    {
        if (GroundOffScreen)
            return default;
        var x = 400f + rightMetres * 20f;
        return new SeatMousePoint(true, x, GroundPixelY, x / 1000f, GroundPixelY / 1000f, 100f + rightMetres, 130f);
    }

    public void MouseClick(SeatMousePoint at, bool right, bool shift, bool ctrl) => _clicks.Enqueue((right, shift, at, null));

    public void MouseDragRight(SeatMousePoint from, SeatMousePoint to) => _clicks.Enqueue((true, false, from, to));

    public void MouseDragLeft(SeatMousePoint from, SeatMousePoint to) => _leftDrags.Enqueue((from, to));

    public bool MouseBoxShown => _boxShown;

    public bool MouseFirstSelected => _firstIdx >= 0 && _mouseSelected.Contains(_firstIdx);

    public int MouseFirstCount => _firstIdx < 0 ? -1 : _companySoldiers[_firstIdx];

    public int MouseSecondCount => _secondIdx < 0 ? -1 : _companySoldiers[_secondIdx];

    public void MouseRequestTransfer() => _transferAsked = true;

    public void MouseTransferBack(int number)
    {
        Note("mouse-transfer-back");
        Faulted |= FaultMidStep;
        if (_firstIdx < 0 || _secondIdx < 0 || number <= 0)
            return;
        _companySoldiers[_secondIdx] -= number;
        _companySoldiers[_firstIdx] += number;
    }

    public int MouseSelectedCount => _mouseSelected.Count;

    public bool MouseOnlyFirstSelected => _mouseSelected.Count == 1 && _mouseSelected.Contains(_firstIdx);

    public bool MouseFirstAndSecondSelected => _mouseSelected.Count == 2 && _mouseSelected.Contains(_firstIdx) && _mouseSelected.Contains(_secondIdx);

    public float MouseOrderDistance(SeatMousePoint to) =>
        _orderAt == null ? float.NaN : (float)Math.Sqrt(Math.Pow(_orderAt.Value.X - to.WorldX, 2) + Math.Pow(_orderAt.Value.Y - to.WorldY, 2));

    public float MouseFacingError(SeatMousePoint from, SeatMousePoint to) =>
        _facing == null ? float.NaN : SeatTrialMouse.NormalError(to.WorldX - from.WorldX, to.WorldY - from.WorldY, _facing.Value.X, _facing.Value.Y);

    public bool MouseChargesChosenEnemy => _charging && _chargeTargetsEnemy;

    public void ReleaseMouseTrial()
    {
        MouseReleases++;
        _commandOn = false;
        _clicks.Clear();
        _leftDrags.Clear();
        _boxShown = false;
        _boxDrag = null;
        _transferAsked = false;
        MarshalReleaseKeys();
    }

    // A box drag takes two mission ticks, as the real one does: the held frame shows the rectangle, the release hides it and selects.
    private void ApplyBox()
    {
        if (_boxDrag.HasValue)
        {
            var (from, to) = _boxDrag.Value;
            _boxDrag = null;
            _boxShown = BoxStaysShown;
            if (BoxSelectsNothing)
                return;
            _mouseSelected.Clear();
            var left = Math.Min(from.PixelX, to.PixelX);
            var right = Math.Max(from.PixelX, to.PixelX);
            var top = Math.Min(from.PixelY, to.PixelY);
            var bottom = Math.Max(from.PixelY, to.PixelY);
            for (var company = 0; company < _companySoldiers.Length; company++)
                if (OwnPixelBase + company >= left && OwnPixelBase + company <= right && 200f >= top && 200f <= bottom)
                    _mouseSelected.Add(company);
        }
        else if (_leftDrags.Count > 0)
        {
            _boxDrag = _leftDrags.Dequeue();
            _boxShown = !BoxNeverShown;
        }
    }

    private void ApplyTransfer()
    {
        if (!_transferAsked)
            return;
        _transferAsked = false;
        if (TransferDoesNothing || _firstIdx < 0 || _secondIdx < 0 || !_mouseSelected.Contains(_firstIdx))
            return;

        var moved = _companySoldiers[_firstIdx] - TransferLeftBehind;
        _companySoldiers[_firstIdx] = TransferLeftBehind;
        _companySoldiers[_secondIdx] += TransferLosesSoldiers ? moved - 5 : moved;
    }

    // Called at the start of the mission tick, before the script: the pointer's clicks of the screen ticks since the last one.
    private void ApplyMouse()
    {
        if (CommandLive)
        {
            ApplyBox();
            ApplyTransfer();
        }

        while (_clicks.Count > 0)
        {
            var (right, shift, at, to) = _clicks.Dequeue();
            if (!CommandLive)
                continue;
            if (!right)
                ApplyLeft(shift, at);
            else if (to.HasValue && !DragActsAsClick)
                ApplyLine(at, to.Value);
            else
                ApplyRight(shift, at);
        }
    }

    private void ApplyLeft(bool shift, SeatMousePoint at)
    {
        if (SelectDoesNothing)
            return;
        var company = (int)(at.PixelX - OwnPixelBase);
        if (shift && !ShiftActsAsPlain)
        {
            if (!_mouseSelected.Remove(company))
                _mouseSelected.Add(company);
            return;
        }

        _mouseSelected.Clear();
        _mouseSelected.Add(company);
    }

    private void ApplyRight(bool shift, SeatMousePoint at)
    {
        if (_mouseSelected.Count == 0 && _selected.Count == 0)
            return;
        if (at.PixelX == EnemyPixel)
        {
            if (!ChargeGoesNowhere)
            {
                _charging = true;
                _chargeTargetsEnemy = true;
            }

            return;
        }

        if (_selected.Count > 0)
        {
            // Marshal's companies: the same order path the Marshal's step uses, so its capture takes the order when the queue key is armed.
            _points[99] = (at.WorldX, at.WorldY);
            MarshalMoveTo(99);
            return;
        }

        if (!MoveIgnoresTheClick)
            _orderAt = (at.WorldX + MoveOffByMetres, at.WorldY);
    }

    private void ApplyLine(SeatMousePoint from, SeatMousePoint to)
    {
        if (_mouseSelected.Count == 0)
            return;
        var dx = to.WorldX - from.WorldX;
        var dy = to.WorldY - from.WorldY;
        _orderAt = ((from.WorldX + to.WorldX) / 2f, (from.WorldY + to.WorldY) / 2f);
        _facing = DragFacesAlongTheLine ? (dx, dy) : (-dy, dx);
    }
}
