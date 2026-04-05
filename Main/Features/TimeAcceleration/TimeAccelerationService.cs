using TaleWorlds.CampaignSystem;
using TaleWorlds.InputSystem;

namespace TAOM.Features.TimeAcceleration;

public class TimeAccelerationService : ITimeAccelerationService
{
    private readonly IMapInputAdapter _input;
    private readonly ITimeControlAdapter _timeControl;
    private readonly ITimeAccelerationSettingsProvider _settings;

    private float _savedSpeed;
    private CampaignTimeControlMode _savedMode;
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
            return;

        if (_timeControl.IsMenuOpen && !_timeControl.IsTimeControlLocked)
            return;

        var spacePressed = _input.IsKeyPressed(InputKey.Space);
        var ctrlDown = _input.IsControlDown();

        if (ctrlDown && spacePressed)
        {
            _savedSpeed = _timeControl.SpeedUpMultiplier;
            _savedMode = _timeControl.TimeControlMode;
            _timeControl.SpeedUpMultiplier = _settings.CtrlSpaceMultiplier;
            _timeControl.SetTimeSpeed(2);
            _ctrlSpaceActive = true;
        }
        else if (_ctrlSpaceActive && (!ctrlDown || _input.IsKeyReleased(InputKey.Space)))
        {
            _timeControl.SpeedUpMultiplier = _savedSpeed;
            _timeControl.TimeControlMode = _savedMode;
            _ctrlSpaceActive = false;
        }
        else if (_input.IsKeyPressed(InputKey.E))
        {
            _timeControl.SpeedUpMultiplier = _settings.ExtraFastForwardMultiplier;
            _timeControl.SetTimeSpeed(2);
        }
        else if (spacePressed)
        {
            _timeControl.SpeedUpMultiplier = _settings.FastForwardMultiplier;
        }
    }
}
