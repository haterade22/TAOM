using System;
using TAOM.Adapters;
using TAOM.Client.Adapters;
using TAOM.Core.Logging;
using TAOM.Core.Validation;
using TAOM.Features.SeatOfSeeing;
using TAOM.Features.SeatOfSeeing.Domain;

namespace TAOM.Client.Features.SeatOfSeeing;

/// <summary>The Seat's state machine for one mission (build-spec 5.3): Off, Rising (the entry blend) and Seated, over the screen and hero adapters so a test drives it.</summary>
internal sealed class SeatSession
{
    private readonly ISeatScreenAdapter _screen;
    private readonly ISeatHeroAdapter _hero;
    private readonly ISeatSettingsProvider _settings;
    private readonly Func<SeatPermissions> _permissions;
    private readonly IOutcomeLog _outcomes;
    private readonly IModLogger _logger;
    private readonly string _key;
    private readonly OutcomeCounter _frameCounter;

    private SeatFlight _flight;
    private SeatFlight? _last;
    private SeatPose _from;
    private SeatPose _applied;
    private float _viewAngle;
    private float _ground;
    private float _heightLimit;
    private float _elapsed;
    private int _frames;
    private bool _controllerShut;
    private bool _firstEntryShown;

    // Set from the engine's callbacks (a mode change, a main agent change), which may not run on the thread that reads them.
    private volatile bool _deploymentEnded;
    private volatile bool _standFastOwed;

    internal SeatSession(ISeatScreenAdapter screen, ISeatHeroAdapter hero, ISeatSettingsProvider settings, Func<SeatPermissions> permissions,
        IOutcomeLog outcomes, IModLogger logger, string missionKey)
    {
        _screen = screen;
        _hero = hero;
        _settings = settings;
        _permissions = permissions;
        _outcomes = outcomes;
        _logger = logger;
        _key = missionKey;
        _frameCounter = outcomes.Counter(SeatChecks.FrameCounterId);
    }

    internal SeatState State { get; private set; }

    internal bool Faulted { get; private set; }

    /// <summary>Once per real frame, from <c>OnMissionScreenTick</c>. A frame with no key and no stored deployment edge reads no setting and no fact.</summary>
    internal void OnScreenTick(bool keyPressed)
    {
        if (Faulted)
            return;
        try
        {
            var deploymentEnded = _deploymentEnded;
            _deploymentEnded = false;
            if (State == SeatState.Off)
                WhileOff(keyPressed, deploymentEnded);
            else
                WhileInTheSeat(keyPressed);
        }
        catch (Exception ex)
        {
            Fault(ex);
        }
    }

    /// <summary>
    /// Once per real frame, from <c>UpdateOverridenCamera</c>. True when the Seat owns the frame; false while Off, in photo mode, with
    /// a custom camera set, and after a fault, so the engine's own camera runs.
    /// </summary>
    internal bool OnFrame(float dt)
    {
        if (State == SeatState.Off || Faulted)
            return false;
        try
        {
            var facts = _screen.ReadFacts();
            if (facts.PhotoMode || facts.CustomCamera)
                return false;

            var rising = State == SeatState.Rising;
            if (rising)
            {
                if (FiniteFloatValidator.IsFinite(dt) && dt > 0f)
                    _elapsed += dt;
            }
            else
            {
                _flight = Bounded(SeatCameraMath.Step(_flight, _screen.ReadInput(dt), dt), _flight);
            }

            var flightGround = _screen.GroundHeight(_flight.X, _flight.Y, _applied.Z);
            _ground = SeatCameraMath.SmoothGround(_ground, flightGround, dt);
            _flight = SeatCameraMath.CapFlight(_flight, _heightLimit, _ground);
            var goal = SeatCameraMath.Pose(_flight, _ground);
            var fraction = SeatCameraMath.BlendFraction(_elapsed);
            var pose = SeatCameraMath.CapHeight(rising ? SeatCameraMath.Blend(_from, goal, fraction) : goal, _heightLimit);

            var rawGroundUnderPose = rising ? _screen.GroundHeight(pose.X, pose.Y, pose.Z) : flightGround;
            pose = SeatCameraMath.AboveGround(pose, rawGroundUnderPose);
            _screen.ApplyFrame(pose, _viewAngle, SeatCameraMath.ListenerHeight(pose.Z, _ground));
            _applied = pose;
            if (rising && fraction >= 1f)
                State = SeatState.Seated;

            _frames++;
            _frameCounter.Pass();
            if (_frames == SeatChecks.FramesThreshold)
                _outcomes.Pass(SeatChecks.FramesId, _key, "mission=" + _key + " frames=" + _frames.ToString(System.Globalization.CultureInfo.InvariantCulture));
            return true;
        }
        catch (Exception ex)
        {
            Fault(ex);
            return false;
        }
    }

    /// <summary>
    /// From the mission tick, the only place the hero's agent is written (5.5): while in the Seat, a write that is owed is done once. A
    /// paused mission still ticks, with a <c>dt</c> of 0, so the write does not wait on time.
    /// </summary>
    internal void OnMissionTick()
    {
        if (State == SeatState.Off || Faulted || !_standFastOwed)
            return;
        try
        {
            _standFastOwed = false;
            _hero.StandFast();
        }
        catch (Exception ex)
        {
            Fault(ex);
        }
    }

    /// <summary>
    /// The engine may raise the event inside a native callback on a chosen thread, and raises it again for the same agent on any change
    /// to Player, so this only notes that a stand-fast write is owed to whichever agent is the main one then.
    /// </summary>
    internal void OnMainAgentChanged() => _standFastOwed = true;

    /// <summary>The mission went from Deployment to Battle (<c>OnMissionModeChange</c>). Stored as one bool, used or dropped at the next screen tick.</summary>
    internal void OnDeploymentEnded() => _deploymentEnded = true;

    /// <summary>The mission screen is going away: hands the hero back with no glide and no look. Safe to call twice, and from both teardown paths.</summary>
    internal void Finish()
    {
        try
        {
            if (State != SeatState.Off)
                Leave("finish", glide: false, writeLook: false);
        }
        catch (Exception ex)
        {
            Fault(ex);
        }
    }

    private void WhileOff(bool keyPressed, bool deploymentEnded)
    {
        if (!keyPressed && !deploymentEnded)
            return;

        var enabled = _settings.Enabled;
        var battleStart = deploymentEnded && enabled && _settings.BeginBattlesInSeat;
        if (!keyPressed && !battleStart)
            return;

        var facts = _screen.ReadFacts();
        battleStart &= facts.BattleMode;
        if (!keyPressed && !battleStart)
            return;
        if (facts.Deployment || facts.Interlude || facts.PhotoMode || facts.MissionEnded)
            return;

        if (!enabled)
            Refuse(keyPressed, "setting-off");
        else if (!_permissions().Camera)
            Refuse(keyPressed, "denied");
        else if (facts.RageHoldsHero)
            // A warg rage is the game saying "you lost control", and its controller reads the Seat's sink key as a dismount (5.5).
            Refuse(keyPressed, "rage", SeatMessage.RageHolds);
        else if (facts.CustomCamera)
            Refuse(keyPressed, "custom-camera", SeatMessage.OtherView);
        else
            Enter(blend: !battleStart);
    }

    private void Refuse(bool keyPressed, string reason, SeatMessage? message = null)
    {
        if (!keyPressed)
            return;
        _outcomes.Skip(SeatChecks.EnterId, _key + ":" + reason, "mission=" + _key + " reason=" + reason);
        if (message.HasValue)
            _screen.Say(message.Value);
    }

    private void WhileInTheSeat(bool keyPressed)
    {
        var facts = _screen.ReadFacts();
        if (!_settings.Enabled)
            Leave("setting-off", facts.BattleMode);
        else if (facts.Interlude)
            Leave("interlude", facts.BattleMode);
        else if (facts.RageHoldsHero)
            Leave("rage", facts.BattleMode);
        else if (!facts.PhotoMode && keyPressed)
            Leave("key", facts.BattleMode);
    }

    private void Enter(bool blend)
    {
        if (!_screen.TryReadCamera(out var camera, out var viewAngle) || !SeatCameraMath.IsFinite(camera))
        {
            _outcomes.Fail(SeatChecks.EnterId, _key + ":no-camera", "mission=" + _key + " reason=no-camera");
            return;
        }

        var ground = _screen.GroundHeight(camera.X, camera.Y, camera.Z);
        if (!FiniteFloatValidator.IsFinite(ground))
        {
            _outcomes.Fail(SeatChecks.EnterId, _key + ":no-ground", "mission=" + _key + " reason=no-ground");
            return;
        }

        var flight = blend ? SeatCameraMath.Enter(camera, _last) : SeatCameraMath.Hold(camera, ground);
        _flight = Bounded(flight, flight);
        _viewAngle = SeatCameraMath.ViewAngle(viewAngle);
        _heightLimit = _screen.ReadHeightLimit();
        _ground = ground;
        _from = camera;
        _applied = camera;
        _elapsed = 0f;
        _frames = 0;

        _controllerShut = true;
        _screen.SetHeroController(false);
        _standFastOwed = true;
        SeatPresence.Set(true);
        State = blend ? SeatState.Rising : SeatState.Seated;

        if (!_firstEntryShown)
        {
            _firstEntryShown = true;
            _screen.Say(SeatMessage.FirstEntry);
        }

        _outcomes.Pass(SeatChecks.EnterId, _key, "mission=" + _key + " blend=" + (blend ? "1" : "0"));
    }

    private void Leave(string reason, bool glide, bool writeLook = true)
    {
        var look = _applied;
        _last = _flight;
        State = SeatState.Off;
        SeatPresence.Set(false);
        ReleaseController();

        var lookWritten = !writeLook || _screen.WriteLook(look.Yaw, look.Pitch);
        var began = glide && _screen.BeginGlide();
        _outcomes.Pass(SeatChecks.LeaveId, _key + ":" + reason, "mission=" + _key + " frames="
            + _frames.ToString(System.Globalization.CultureInfo.InvariantCulture) + " glide=" + (began ? "1" : "0") + " reason=" + reason);

        if (!lookWritten)
            _outcomes.Fail(SeatChecks.GlideId, _key, "mission=" + _key + " elevation=missing");
        else if (began)
            _outcomes.Pass(SeatChecks.GlideId, _key, "mission=" + _key + " elevation=set");
        if (glide && !began)
            _outcomes.Skip(SeatChecks.GlideId, _key, "mission=" + _key + " reason=no-agent");
    }

    private void ReleaseController()
    {
        _controllerShut = false;
        _screen.SetHeroController(true);
    }

    private SeatFlight Bounded(SeatFlight next, SeatFlight fallback)
    {
        var (x, y) = _screen.ClampToBoundary(next.X, next.Y);
        return FiniteFloatValidator.IsFinite(x) && FiniteFloatValidator.IsFinite(y)
            ? next with { X = x, Y = y }
            : next with { X = fallback.X, Y = fallback.Y };
    }

    private void Fault(Exception ex)
    {
        if (Faulted)
            return;
        Faulted = true;
        State = SeatState.Off;
        SeatPresence.Set(false);
        if (_controllerShut)
            Swallow(ReleaseController);

        Swallow(() => _logger.LogError($"[SeatOfSeeing] the Seat failed and is off for this mission, the hero's own camera restored: {ex}"));
        Swallow(() => _outcomes.Fail(SeatChecks.FaultId, _key, "mission=" + _key + " error=" + ex.GetType().Name));
        Swallow(() => _screen.Say(SeatMessage.Faulted));
    }

    // Latching must finish whatever else breaks: every step of it is independent.
    private void Swallow(Action action)
    {
        try
        {
            action();
        }
        catch (Exception ex)
        {
            _outcomes.Swallowed("SeatOfSeeing", "FaultLatch", ex);
        }
    }
}
