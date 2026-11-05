using System;
using TAOM.Features.FordsOfWar.Domain;
using TAOM.Features.SeatOfSeeing.Domain;

namespace TAOM.Tests.Features.SeatOfSeeing;

/// <summary>How the stand-in breaks the Seat's answer to another writer's fast forward flag.</summary>
internal enum AdoptionFault
{
    None,
    Ignored,
    StepReadsOn,
    RequestReadsHeld,
    LevelStaysAfterTheirFlagFalls,
}

/// <summary>
/// The mission time and the lift of the view in the stand-in. The clock does what the engine does: a pause stops it, the fixed step multiplies a frame
/// by the lesser of the level and a tenth of a second over the frame time (<c>FordsGameSpeed.Frame</c>), fast forward is nine extra steps a frame, and a
/// time speed request multiplies the whole. The lift is raised and sunk by <see cref="SeatPanelLift"/> and ends at an exact zero. Each knob breaks one fact.
/// </summary>
internal sealed partial class CommandTrialWorld
{
    internal bool TimeKeysDead;
    internal bool PauseIgnoredByTheClock;
    internal bool ResumeDoesNothing;
    internal bool SlowIgnoredByTheClock;
    internal bool FastIgnoredByTheClock;
    internal bool FixedStepIgnoresTheCap;
    internal float FastForwardRate = 10f;
    internal bool OtherRequestHeld;
    internal bool OtherRequestAppearsAfterTheSettle;
    internal bool ReportsNoRequest;
    internal bool FixedStepStaysOnAtSlow;
    internal bool FastForwardStaysOnAtSlow;
    internal bool PausedFlagStaysOnAtSlow;
    internal bool PauseLeavesFastForward;
    internal bool RequestSurvivesQuicken;
    internal bool FastForwardNeverRises;
    internal bool FixedStepNeverRises;
    /// <summary>The level (2 or 4) at which the state read reports no fixed step although the clock still takes it; -1 for none.</summary>
    internal int FixedStepReportedOffAtLevel = -1;
    internal bool FixedStepStaysOnAtTen;
    internal bool FastForwardReportedOff;
    /// <summary>The level (2 or 4) at which the state read reports fast forward although the clock does not take it; -1 for none.</summary>
    internal int FastForwardReportedOnAtLevel = -1;
    /// <summary>What goes wrong when another writer's fast forward flag meets the Seat's step or request.</summary>
    internal AdoptionFault Adoption;
    /// <summary>Real seconds the Seat takes to answer another writer's flag, raised or lowered.</summary>
    internal float AdoptionLagSeconds;
    internal bool TimeKeyLeavesTheSeat;
    internal bool TimeKeyFaults;
    internal int TimeReleases;

    internal bool RiseDead;
    internal bool RiseFirstPerson;
    internal float RiseConstant = SeatPanelLift.FieldHeight;
    internal float SinkLeavesResidue;
    internal bool SinkNever;
    /// <summary>The most the lift reaches in metres, when it is less than the constant; NaN for the whole constant.</summary>
    internal float RiseReaches = float.NaN;

    private const float EngineStepCapSeconds = 0.1f;

    private float _otherFlagChangedAt = float.NegativeInfinity;
    private bool _stepReadsOn;
    private bool _requestReadsHeld;
    private bool _timeOn;
    private int _levelIndex = SeatTimeLadder.Normal;
    private bool _fastForward;
    private bool _fixedStep;
    private bool _paused;
    private float? _request;
    private float? _slowPressedAt;
    private float _riseProgress;
    private float _cameraLift;
    private float _offsetZ;

    internal float MissionClock { get; private set; }

    public float TimeLevel => SeatTimeLadder.Level(_levelIndex);

    public bool ThirdPerson => !RiseFirstPerson;

    public float LiftHeight() => RiseConstant;

    public Vec3f CameraTargetOffset() => new(0f, 0f, _offsetZ);

    public bool OtherTimeRequest => OtherRequestHeld || (OtherRequestAppearsAfterTheSettle && _slowPressedAt.HasValue && Time >= _slowPressedAt.Value + 0.5f);

    public SeatTimeState ReadTimeState() =>
        new((_fastForward && !FastForwardReportedOff) || (int)TimeLevel == FastForwardReportedOnAtLevel,
            (_fixedStep || _stepReadsOn) && (int)TimeLevel != FixedStepReportedOffAtLevel, _paused, (_request.HasValue || _requestReadsHeld) && !ReportsNoRequest);

    /// <summary>Another writer's fast forward, as the score screen's key makes it: the flag only, and the Seat's own rule answers it.</summary>
    public void SetOtherFastForward(bool raised)
    {
        Note("other-fast-forward");
        _fastForward = raised;
        _otherFlagChangedAt = Time;
    }

    public void ReleaseTime()
    {
        TimeReleases++;
        _levelIndex = SeatTimeLadder.Normal;
        _fastForward = false;
        _fixedStep = false;
        _paused = false;
        _request = null;
        _stepReadsOn = false;
        _requestReadsHeld = false;
    }

    public void PressTime(SeatTimeKey key)
    {
        if (TimeKeysDead)
            return;
        if (!_timeOn)
        {
            _timeOn = true;
            MissionClock = Time;
        }

        Faulted |= TimeKeyFaults;
        if (TimeKeyLeavesTheSeat)
            State = SeatState.Off;
        if (key == SeatTimeKey.Stay)
        {
            Pause();
            return;
        }

        var ceiling = SeatTimeLadder.Ceiling(State != SeatState.Off, HeroPresent, false);
        _levelIndex = SeatTimeLadder.Step(_levelIndex, key, ceiling);
        if (!RequestSurvivesQuicken || SeatTimeLadder.IsSlow(_levelIndex))
            _request = null;
        _fastForward = SeatTimeLadder.IsFastForward(_levelIndex) && !FastForwardNeverRises;
        _fixedStep = (SeatTimeLadder.UsesFixedStep(_levelIndex) && !FixedStepNeverRises) || (SeatTimeLadder.IsFastForward(_levelIndex) && FixedStepStaysOnAtTen);
        if (SeatTimeLadder.IsSlow(_levelIndex))
        {
            _slowPressedAt ??= Time;
            _request = SeatTimeLadder.Level(_levelIndex);
            _fastForward |= FastForwardStaysOnAtSlow;
            _fixedStep |= FixedStepStaysOnAtSlow;
            _paused |= PausedFlagStaysOnAtSlow;
        }
    }

    private void Pause()
    {
        if (_paused)
        {
            _paused = ResumeDoesNothing;
            return;
        }

        if (!PauseLeavesFastForward)
        {
            _fastForward = false;
            _fixedStep = false;
            _levelIndex = Math.Min(_levelIndex, SeatTimeLadder.Normal);
        }

        _paused = true;
    }

    // Mission seconds per real second, by the engine's own rules: MissionState.TickMission zeroes a paused frame, takes the fixed step in place of the
    // frame, runs nine extra steps of it in fast forward, and multiplies by the time speed (Mission.UpdateSceneTimeSpeed takes the least request).
    private float ClockRate(float dt)
    {
        if (_paused && !PauseIgnoredByTheClock)
            return 0f;
        var rate = 1f;
        if (_fixedStep && !FastIgnoredByTheClock)
            rate = FixedStepIgnoresTheCap ? SeatTimeLadder.Level(_levelIndex) : Math.Min(SeatTimeLadder.Level(_levelIndex) * dt, EngineStepCapSeconds) / dt;
        if (_fastForward && !FastIgnoredByTheClock)
            rate *= FastForwardRate;
        if (_request.HasValue && !SlowIgnoredByTheClock)
            rate *= _request.Value;
        if (OtherRequestHeld)
            rate *= 0.25f;
        return rate;
    }

    private void AdvanceMissionClock(float dt)
    {
        if (!_timeOn)
            return;
        MissionClock += dt * ClockRate(dt);
        FollowTheFrameRule();
    }

    private void FollowTheFrameRule()
    {
        var frame = SeatTimeLadder.Frame(_levelIndex, _fastForward);
        var answersTheirFlag = frame.Action == SeatTimeAction.Adopt || frame.Action == SeatTimeAction.Stop;
        if (answersTheirFlag && Time < _otherFlagChangedAt + AdoptionLagSeconds)
            return;
        if (frame.Action == SeatTimeAction.Adopt)
        {
            if (Adoption == AdoptionFault.Ignored)
                return;
            _request = null;
            _fixedStep = false;
            _stepReadsOn = Adoption == AdoptionFault.StepReadsOn;
            _requestReadsHeld = Adoption == AdoptionFault.RequestReadsHeld;
        }
        else if (frame.Action == SeatTimeAction.Stop)
        {
            _fastForward = false;
            _fixedStep = false;
            _stepReadsOn = false;
            if (Adoption == AdoptionFault.LevelStaysAfterTheirFlagFalls)
                return;
        }

        _levelIndex = frame.Index;
    }

    private void ResolveRise(float dt)
    {
        if (RiseDead)
            return;
        var open = _panelIsOpen && State == SeatState.Off && ThirdPerson && !Deployment;
        if (!open && _riseProgress == 0f && _offsetZ == 0f)
            return;
        if (SinkNever && !open)
            return;
        _riseProgress = SeatPanelLift.PanelProgress(_riseProgress, open, dt);
        var lift = SeatPanelLift.Lift(float.IsNaN(RiseReaches) ? RiseConstant : RiseReaches, _riseProgress);
        _offsetZ = lift == 0f ? SinkLeavesResidue : lift;
        _cameraLift = _offsetZ;
    }

    private float LiftOfTheCamera => State == SeatState.Off ? _cameraLift : 0f;
}
