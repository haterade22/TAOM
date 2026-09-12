using TAOM.Features.CoopInterop;

namespace TAOM.Features.TimeAcceleration;

public class TimeAccelerationService : ITimeAccelerationService
{
    // CampaignTimeControlMode (v1.4.8): Stop 0, UnstoppablePlay 1, UnstoppableFastForward 2,
    // StoppablePlay 3, StoppableFastForward 4, UnstoppableFastForwardForPartyWaitTime 5,
    // FastForwardStop 6. Campaign.TickMapTime multiplies real time by SpeedUpMultiplier in the
    // three fast-forward modes and ignores it in the other four.
    private const int UnstoppableFastForward = 2;
    private const int StoppableFastForward = 4;
    private const int UnstoppableFastForwardForPartyWaitTime = 5;

    private readonly IMapInputAdapter _input;
    private readonly ITimeControlAdapter _timeControl;
    private readonly ITimeAccelerationSettingsProvider _settings;
    private readonly ICoopSessionProvider _coop;

    private float _savedSpeed;
    private int _savedMode;
    private bool _turboActive;

    public TimeAccelerationService(
        IMapInputAdapter input,
        ITimeControlAdapter timeControl,
        ITimeAccelerationSettingsProvider settings,
        ICoopSessionProvider coop)
    {
        _input = input;
        _timeControl = timeControl;
        _settings = settings;
        _coop = coop;
    }

    public void OnTick()
    {
        // #370 — suppressing the MapBar button was only half the fix. The keybinds (E, Space,
        // Ctrl+Space) reach this service directly and never touched the widget, so under co-op the
        // control vanished from the screen while the mechanic kept running.
        //
        // This matters beyond tidiness because the two properties are NOT equally covered. A co-op
        // host's TimeControlMode setter prefix overwrites the MODE, so SetTimeSpeed is neutralised
        // — but SpeedUpMultiplier is a SEPARATE property that nothing is known to intercept, so a
        // client pressing E still mutated campaign tick state locally, unlogged and ungated.
        //
        // Restore first, then bail: a toggle-off mid-turbo must not latch _turboActive with the
        // engine left at the boosted multiplier (harmony-patches.md "Latches & Toggle Gates" — the
        // state transition is unconditional, the gate comes after).
        if (_coop.ShouldDeferToHost)
        {
            RestoreTurboIfActive();
            return;
        }

        if (!_timeControl.IsCampaignActive || !_input.IsMapActive)
        {
            RestoreTurboIfActive();
            return;
        }

        if (_timeControl.IsMenuOpen && !_timeControl.IsTimeControlLocked)
        {
            RestoreTurboIfActive();
            return;
        }

        // !_turboActive makes the opener idempotent. Without it a second observed press while turbo
        // is already running re-saves the ALREADY BOOSTED speed and mode as the values to restore,
        // so the eventual restore leaves the engine at the turbo multiplier with the latch closed
        // and nothing left that knows to undo it.
        if (!_turboActive && _input.IsControlDown && _input.IsTurboPressed)
        {
            _savedSpeed = _timeControl.SpeedUpMultiplier;
            _savedMode = _timeControl.TimeControlMode;
            _timeControl.SpeedUpMultiplier = _settings.CtrlSpaceMultiplier;
            _timeControl.SetTimeSpeed(2);
            _turboActive = true;
        }
        else if (_turboActive && (!_input.IsControlDown || _input.IsTurboReleased))
        {
            _timeControl.SpeedUpMultiplier = _savedSpeed;
            _timeControl.TimeControlMode = _savedMode;
            _turboActive = false;
        }
        else if (_input.IsExtraFastForwardPressed)
        {
            _timeControl.SpeedUpMultiplier = _settings.ExtraFastForwardMultiplier;
            _timeControl.SetTimeSpeed(2);
        }
        else if (_input.IsFastForwardPressed)
        {
            _timeControl.SpeedUpMultiplier = _settings.FastForwardMultiplier;

            // On the shipped default this key IS vanilla's MapTimeTogglePause, and vanilla's own
            // handler owns the mode transition for the same press, so we deliberately set only the
            // multiplier and leave the toggle alone. Once the player rebinds it away from that key
            // nothing else changes the mode, and Campaign.TickMapTime applies SpeedUpMultiplier ONLY
            // in the fast-forward modes, so without this the rebound key would visibly do nothing.
            if (_input.FastForwardOwnsTimeMode) _timeControl.SetTimeSpeed(2);
        }
    }

    // #574: the MapBar buttons. Until this the Extra Fast Forward button fired vanilla
    // ExecuteTimeControlChange(2) directly, which sets the MODE only, so it was vanilla fast-forward
    // under a different tooltip whatever the MCM slider said (#168 shipped that as "Option A").
    // Vanilla's own FastForward button is rebound to EnterFastForward so it writes the normal
    // multiplier back; with the mode-only handler, one extra press left the extra value in place
    // until Space happened to restore it.

    public void EnterExtraFastForward() => EnterFastForwardAt(_settings.ExtraFastForwardMultiplier);

    public void EnterFastForward() => EnterFastForwardAt(_settings.FastForwardMultiplier);

    // "Extra" is a fast-forward mode running above the normal multiplier, read off the engine every
    // frame so a key press, a click and a vanilla toggle all agree. Turbo lights it too while Ctrl
    // is held, which is the honest reading.
    public bool IsExtraFastForwardActive =>
        _timeControl.IsCampaignActive
        && IsFastForwardMode(_timeControl.TimeControlMode)
        && _timeControl.SpeedUpMultiplier > _settings.FastForwardMultiplier;

    private void EnterFastForwardAt(int multiplier)
    {
        if (_coop.ShouldDeferToHost) return;
        if (!_timeControl.IsCampaignActive) return;

        // The gate is vanilla MapTimeControlVM.ExecuteTimeControlChange's, not the key path's: the
        // buttons keep working inside a WAIT menu that is not time-locked, where vanilla's do. It
        // runs BEFORE the write, because a multiplier written while vanilla refuses the mode change
        // would sit latent and surface on the next fast-forward.
        if (_timeControl.IsMenuOpen && !(_timeControl.IsWaitMenuActive && !_timeControl.IsTimeControlLocked))
            return;

        _timeControl.SpeedUpMultiplier = multiplier;
        _timeControl.SetTimeSpeed(2);
    }

    private static bool IsFastForwardMode(int mode) =>
        mode == UnstoppableFastForward
        || mode == StoppableFastForward
        || mode == UnstoppableFastForwardForPartyWaitTime;

    private void RestoreTurboIfActive()
    {
        if (!_turboActive) return;
        _timeControl.SpeedUpMultiplier = _savedSpeed;
        _timeControl.TimeControlMode = _savedMode;
        _turboActive = false;
    }
}
