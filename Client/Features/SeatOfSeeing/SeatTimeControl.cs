using System;
using TAOM.Adapters;
using TAOM.Client.Adapters;
using TAOM.Core.Logging;
using TAOM.Features.SeatOfSeeing;
using TAOM.Features.SeatOfSeeing.Domain;
using static System.Globalization.CultureInfo;

namespace TAOM.Client.Features.SeatOfSeeing;

/// <summary>Mission time for one mission: the keys step the ladder and flip the pause, and a per-frame rule reads the engine's flag (build-spec section 10).</summary>
internal sealed class SeatTimeControl
{
    private readonly ISeatScreenAdapter _screen;
    private readonly ISeatHeroAdapter _hero;
    private readonly ISeatSettingsProvider _settings;
    private readonly Func<SeatPermissions> _permissions;
    private readonly IOutcomeLog _outcomes;
    private readonly string _key;
    private bool _touched;
    private bool _owed;
    private int _tries;

    internal SeatTimeControl(ISeatScreenAdapter screen, ISeatHeroAdapter hero, ISeatSettingsProvider settings, Func<SeatPermissions> permissions,
        IOutcomeLog outcomes, string missionKey)
    {
        _screen = screen;
        _hero = hero;
        _settings = settings;
        _permissions = permissions;
        _outcomes = outcomes;
        _key = missionKey;
    }

    /// <summary>The stored level, an index into the ladder.</summary>
    internal int Index { get; private set; } = SeatTimeLadder.Normal;

    /// <summary>A time key, from the screen tick; a pause is resumed in any mode, and every other press acts only in Battle mode and stays silent in any other.</summary>
    internal void Press(SeatTimeKey key, bool seatUp)
    {
        if (key == SeatTimeKey.Stay && _screen.ReadTime().Paused)
        {
            ResumeAHeldPause();
            return;
        }

        if (!_screen.ReadFacts().BattleMode)
            return;
        if (!_settings.Enabled)
        {
            Cancel();
            Skip("setting-off");
            return;
        }

        if (!_permissions().Time)
        {
            Skip("denied");
            return;
        }

        _touched = true;
        ReadFlag();
        if (key == SeatTimeKey.Stay)
        {
            Pause();
            return;
        }

        var owned = _screen.ClockOwnedElsewhere();
        if (owned && key == SeatTimeKey.Quicken && Index >= SeatTimeLadder.Normal)
        {
            Skip("other-clock");
            _screen.Say(SeatMessage.TimeOtherClock);
            return;
        }

        Start(SeatTimeLadder.Step(Index, key, SeatTimeLadder.Ceiling(seatUp, _hero.HeroActive(), owned)));
    }

    /// <summary>Once per real frame: pays a cancel's debt, then applies the per-frame rule.</summary>
    internal void OnFrame(float dt)
    {
        if (_owed)
            PayDebt();
        if (!_touched)
            return;

        if (ReadFlag() == SeatTimeAction.Apply)
            _screen.ApplyFastTime(SeatTimeLadder.FastLevel(Index), dt);
    }

    /// <summary>The Seat was left: level 10 with a living hero steps down to 4.</summary>
    internal void OnLeave()
    {
        if (!_touched)
            return;
        var next = SeatTimeLadder.AfterLeave(Index, _hero.HeroActive());
        if (next != Index)
            Start(next);
    }

    /// <summary>A soldier was taken: the engine cleared fast forward, and the fixed step stops with it.</summary>
    internal void OnTake()
    {
        if (_touched)
            DropFastTime();
    }

    /// <summary>Teardown from both exits and the fault: pause, fast forward, fixed step and request, each step apart so that one throw skips none; what still reads on is a debt.</summary>
    internal void Cancel()
    {
        if (!_touched && !_owed)
            return;
        Index = SeatTimeLadder.Normal;
        _tries = 1;
        CancelEach();
    }

    private void PayDebt()
    {
        if (_tries >= SeatTimeLadder.CancelTries)
        {
            _owed = false;
            Swallow(() => _outcomes.Fail(SeatChecks.TimeId, _key + ":cancel-failed", "mission=" + _key + " reason=cancel-failed tries="
                + _tries.ToString(InvariantCulture)));
            return;
        }

        _tries++;
        CancelEach();
    }

    private void CancelEach()
    {
        Swallow(() => _screen.SetPaused(false));
        Swallow(_screen.StopFastTime);
        Swallow(ClearRequest);
        var clean = false;
        Swallow(() => clean = _screen.ReadTime().IsClean(_screen.ClockOwnedElsewhere()));
        _owed = !clean;
        _touched = false;
    }

    private bool FlagMeansLevelTen() => _screen.ReadTime().FastForward && !_screen.ClockOwnedElsewhere();

    // The engine multiplies the request and the fixed step into the fast forward (MissionState.cs:155-173), so the Seat gives both up and writes level 10 only to turn the step off.
    private SeatTimeAction ReadFlag()
    {
        var frame = SeatTimeLadder.Frame(Index, FlagMeansLevelTen());
        Index = frame.Index;
        if (frame.Action == SeatTimeAction.Stop)
        {
            _screen.StopFastTime();
        }
        else if (frame.Action == SeatTimeAction.Adopt)
        {
            ClearRequest();
            _screen.ApplyFastTime(SeatTimeLadder.FastLevel(Index), 0f);
        }

        return frame.Action;
    }

    private void DropFastTime()
    {
        var next = SeatTimeLadder.WithoutFastTime(Index);
        if (next == Index)
            return;
        Index = next;
        _screen.StopFastTime();
    }

    private void ResumeAHeldPause()
    {
        Resume();
        if (!_settings.Enabled)
            Cancel();
    }

    private void Resume()
    {
        _screen.SetPaused(false);
        _screen.Say(SeatMessage.TimeResumed);
        _outcomes.Pass(SeatChecks.TimeId, _key + ":resume", Line("resume"));
    }

    private void Pause()
    {
        DropFastTime();
        _screen.SetPaused(true);
        _screen.Say(SeatMessage.TimeStayed);
        _outcomes.Pass(SeatChecks.TimeId, _key + ":stay", Line("stay"));
    }

    // Removing an absent request throws (Mission.cs:2115-2125) and adding does not replace (2100-2103), so a held one is removed first.
    private void Start(int next)
    {
        ClearRequest();
        Index = next;
        if (SeatTimeLadder.IsSlow(next))
        {
            _screen.StopFastTime();
            _screen.AddSlowRequest(SeatTimeLadder.Level(next));
        }
        else if (next == SeatTimeLadder.Normal)
        {
            _screen.StopFastTime();
        }
        else
        {
            _screen.ApplyFastTime(SeatTimeLadder.FastLevel(next), 0f);
        }

        var name = SeatTimeLadder.Name(next);
        _screen.Say(SeatMessage.TimeLevel, name);
        _outcomes.Pass(SeatChecks.TimeId, _key + ":" + name, Line("level"));
    }

    private void ClearRequest()
    {
        if (_screen.ReadTime().SlowRequest)
            _screen.RemoveSlowRequest();
    }

    private string Line(string kind) => "mission=" + _key + " level=" + SeatTimeLadder.Name(Index) + " event=" + kind;

    private void Skip(string reason) => _outcomes.Skip(SeatChecks.TimeId, _key + ":" + reason, "mission=" + _key + " reason=" + reason);

    private void Swallow(Action action)
    {
        try
        {
            action();
        }
        catch (Exception ex)
        {
            _outcomes.Swallowed("SeatOfSeeing", "TimeCancel", ex);
        }
    }
}
