using System;
using System.Globalization;
using TAOM.Client.Adapters;
using TAOM.Core.Logging;
using TAOM.Features.SeatOfSeeing;
using TAOM.Features.SeatOfSeeing.Domain;

namespace TAOM.Client.Features.SeatOfSeeing;

/// <summary>The lift of the hero's third-person view while the order panel is open: its own setting is read on the open edge, the Seat's master setting never, and the offset ends in an exact zero (build-spec section 11).</summary>
internal sealed class SeatRiseControl
{
    private readonly ISeatScreenAdapter _screen;
    private readonly ISeatSettingsProvider _settings;
    private readonly Func<SeatPermissions> _permissions;
    private readonly IOutcomeLog _outcomes;
    private readonly string _key;
    private float _progress;
    private float _lift;
    private float _height;
    private bool _wasOpen;
    private bool _enabled;
    private bool _noted;

    internal SeatRiseControl(ISeatScreenAdapter screen, ISeatSettingsProvider settings, Func<SeatPermissions> permissions, IOutcomeLog outcomes, string missionKey)
    {
        _screen = screen;
        _settings = settings;
        _permissions = permissions;
        _outcomes = outcomes;
        _key = missionKey;
    }

    /// <summary>Once per real frame; a view in the Seat counts as a closed panel, because the Seat does not use the lift.</summary>
    internal void OnTick(float dt, bool seatUp)
    {
        var read = _screen.ReadRise();
        var open = read.PanelOpen && read.BattleMode && !read.FirstPerson && !seatUp;
        if (open && !_wasOpen)
        {
            _enabled = _settings.RiseWithPanel && _permissions().Orders;
            if (_enabled)
                _height = _screen.LiftHeight();
        }

        _wasOpen = open;
        if (!open && _progress == 0f && _lift == 0f)
            return;

        _progress = SeatPanelLift.PanelProgress(_progress, open && _enabled && _height > 0f, dt);
        var lift = SeatPanelLift.Lift(_height, _progress);
        if (lift == _lift)
            return;

        _screen.SetPanelLift(lift);
        _lift = lift;
        if (_noted || lift <= 0f)
            return;
        _noted = true;
        _outcomes.Pass(SeatChecks.RiseId, _key, "mission=" + _key + " height=" + _height.ToString("0.0", CultureInfo.InvariantCulture));
    }

    /// <summary>Back to nothing, as the exact zero when a lift is up; a finish writes nothing, because the offset lives on the Mission instance.</summary>
    internal void Zero()
    {
        _progress = 0f;
        _wasOpen = false;
        if (_lift == 0f)
            return;
        _screen.SetPanelLift(0f);
        _lift = 0f;
    }
}
