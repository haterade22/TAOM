namespace TAOM.Features.TimeAcceleration;

public class TimeAccelerationService : ITimeAccelerationService
{
    private readonly IMapInputAdapter _input;
    private readonly ITimeControlAdapter _timeControl;
    private readonly ITimeAccelerationSettingsProvider _settings;

    private float _savedSpeed;
    private int _savedMode;
    private bool _ctrlSpaceActive;

    public TimeAccelerationService(
        IMapInputAdapter input,
        ITimeControlAdapter timeControl,
        ITimeAccelerationSettingsProvider settings)
    {
        _input = input;
        _timeControl = timeControl;
        _settings = settings;
    }

    public void OnTick()
    {
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
