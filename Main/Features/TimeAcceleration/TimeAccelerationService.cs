using TAOM.Features.CoopInterop;

namespace TAOM.Features.TimeAcceleration;

public class TimeAccelerationService : ITimeAccelerationService
{
    private readonly IMapInputAdapter _input;
    private readonly ITimeControlAdapter _timeControl;
    private readonly ITimeAccelerationSettingsProvider _settings;
    private readonly ICoopSessionProvider _coop;

    private float _savedSpeed;
    private int _savedMode;
    private bool _ctrlSpaceActive;

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
        // Restore first, then bail: a toggle-off mid-turbo must not latch _ctrlSpaceActive with the
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

        if (_input.IsControlDown && _input.IsSpacePressed)
        {
            _savedSpeed = _timeControl.SpeedUpMultiplier;
            _savedMode = _timeControl.TimeControlMode;
            _timeControl.SpeedUpMultiplier = _settings.CtrlSpaceMultiplier;
            _timeControl.SetTimeSpeed(2);
            _ctrlSpaceActive = true;
        }
        else if (_ctrlSpaceActive && (!_input.IsControlDown || _input.IsSpaceReleased))
        {
            _timeControl.SpeedUpMultiplier = _savedSpeed;
            _timeControl.TimeControlMode = _savedMode;
            _ctrlSpaceActive = false;
        }
        else if (_input.IsEKeyPressed)
        {
            _timeControl.SpeedUpMultiplier = _settings.ExtraFastForwardMultiplier;
            _timeControl.SetTimeSpeed(2);
        }
        else if (_input.IsSpacePressed)
        {
            _timeControl.SpeedUpMultiplier = _settings.FastForwardMultiplier;
        }
    }

    private void RestoreTurboIfActive()
    {
        if (!_ctrlSpaceActive) return;
        _timeControl.SpeedUpMultiplier = _savedSpeed;
        _timeControl.TimeControlMode = _savedMode;
        _ctrlSpaceActive = false;
    }
}
