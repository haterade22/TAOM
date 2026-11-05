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
    private bool _focusNoted;
    private SeatKeepKind _keep;
    private SeatOrbitState _orbit;
    private bool _cursorShown;
    private bool _hideInForce;
    private bool _hideSaved;
    private bool _panelWasOpen;
    private bool _keepOwed;
    private SeatCloseSignals _previousSignals;
    private bool _fightChosen;
    private bool _handed;
    private bool _handBackOwed;
    private int _handBackTries;
    private float _lastRetreatCheck = float.NaN;
    private bool _takeOwed;
    private bool _fallSaid;

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

    /// <summary>What the view keeps to; the field trial reads it.</summary>
    internal SeatKeepKind Keep => _keep;

    /// <summary>Frames the Seat has owned since the latest entry.</summary>
    internal int Frames => _frames;

    /// <summary>True when the latest leave armed the engine's return glide.</summary>
    internal bool GlideBegan { get; private set; }

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
    /// a custom camera set, and after a fault, so the engine's own camera runs. <paramref name="input"/> replaces the screen's flight
    /// input when it is set; only the field trial sets it (build-spec section 12).
    /// </summary>
    internal bool OnFrame(float dt, SeatInput? input = null)
    {
        if (State == SeatState.Off || Faulted)
            return false;
        try
        {
            var facts = _screen.ReadFacts();
            if (facts.PhotoMode || facts.CustomCamera)
                return false;

            var rising = State == SeatState.Rising;
            var fraction = 1f;
            SeatPose pose;
            if (rising)
            {
                pose = FlightPose(true, default, dt, out fraction);
            }
            else
            {
                var read = input ?? _screen.ReadInput(dt);
                if (!TryOrbitPose(read, dt, out pose))
                    pose = FlightPose(false, read, dt, out _);
            }

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
    /// From the mission tick, the only place the hero's agent is written (5.5): the stand-fast write that is owed is done once while in the Seat,
    /// and a stored keep press is acted on. A paused mission still ticks, with a <c>dt</c> of 0, so neither waits on time.
    /// </summary>
    internal void OnMissionTick()
    {
        FightTick();
        if (Faulted)
            return;
        try
        {
            if (State != SeatState.Off && _standFastOwed)
            {
                _standFastOwed = false;
                _hero.StandFast();
                NoteTheFall();
            }

            if (_keepOwed)
            {
                _keepOwed = false;
                if (State == SeatState.Seated && !_screen.ReadFacts().PhotoMode)
                    KeepPress();
            }

            if (_takeOwed)
            {
                _takeOwed = false;
                if (State != SeatState.Off && !_screen.ReadFacts().PhotoMode)
                    TakePress();
            }
        }
        catch (Exception ex)
        {
            Fault(ex);
        }
    }

    /// <summary>The key to keep to a soldier or a company, stored for the next mission tick, where the rays are cast. Only a seated view acts on it.</summary>
    internal void PressKeep()
    {
        if (State != SeatState.Seated)
            return;
        try
        {
            if (!_screen.ReadFacts().PhotoMode)
                _keepOwed = true;
        }
        catch (Exception ex)
        {
            Fault(ex);
        }
    }

    /// <summary>The key to take a soldier after the fall, stored for the next mission tick and dropped in photo mode.</summary>
    internal void PressTake()
    {
        if (State == SeatState.Off)
            return;
        try
        {
            if (!_screen.ReadFacts().PhotoMode)
                _takeOwed = true;
        }
        catch (Exception ex)
        {
            Fault(ex);
        }
    }

    /// <summary>The key to hide the interface, from the screen tick and only while seated: it hides, or brings the interface back.</summary>
    internal void PressHide()
    {
        if (State != SeatState.Seated)
            return;
        try
        {
            // Photo mode is the engine's second writer of the flag (MissionGauntletPhotoMode.cs:118, 139, 183, 192, 200, 317), and no Seat key
            // acts in it (5.3).
            if (_screen.ReadFacts().PhotoMode)
                return;
            if (_hideInForce)
                RestoreHide("key");
            else
                BeginHide();
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

        _handBackOwed = false;
        Swallow(() => SeatPresence.SetHeroFightsOn(false));
    }

    private void FightTick()
    {
        if (!_handed && !_fightChosen && !_handBackOwed)
            return;
        try
        {
            if (_handBackOwed)
            {
                RetryHandBack();
                return;
            }

            if (_handed && !_hero.HeroOnAi())
            {
                HeroFell();
                return;
            }

            switch (SeatFightRule.Owed(SeatFightRule.Wanted(State, Faulted, _fightChosen), _handed))
            {
                case SeatFightAction.Begin:
                    BeginFight();
                    break;
                case SeatFightAction.End:
                    EndFight();
                    break;
                default:
                    if (_handed)
                        CheckForRetreat();
                    break;
            }
        }
        catch (Exception ex)
        {
            _fightChosen = false;
            Swallow(() => _outcomes.Fail(SeatChecks.FightOnId, _key + ":throw", "mission=" + _key + " error=" + ex.GetType().Name));
            Fault(ex);
        }
    }

    private void BeginFight()
    {
        var refusal = _hero.BeginFighting();
        if (refusal != SeatFightRefusal.None)
        {
            _fightChosen = false;
            var why = SeatFightRule.Reason(refusal);
            _outcomes.Skip(SeatChecks.FightOnId, _key + ":refused-" + why, "mission=" + _key + " reason=refused why=" + why);
            return;
        }

        _handed = true;
        _lastRetreatCheck = float.NaN;
        SeatPresence.SetHeroFightsOn(true);
        _outcomes.Pass(SeatChecks.FightOnId, _key + ":begin", "mission=" + _key + " event=begin");
    }

    private void EndFight()
    {
        _handed = false;
        _handBackOwed = true;
        _handBackTries = 0;
        HandBackOnce();
    }

    private void HandBackOnce()
    {
        _handBackTries++;
        try
        {
            _hero.EndFighting();
        }
        finally
        {
            SeatPresence.SetHeroFightsOn(false);
        }

        _handBackOwed = false;
        _outcomes.Pass(SeatChecks.FightOnId, _key + ":end", "mission=" + _key + " event=end");
    }

    private void RetryHandBack()
    {
        if (!_hero.HeroOnAi())
        {
            _handBackOwed = false;
            return;
        }

        try
        {
            HandBackOnce();
        }
        catch (Exception ex)
        {
            if (_handBackTries >= SeatFightRule.HandBackTries)
                GiveUpOnHandBack(ex);
            else
                _outcomes.Swallowed("SeatOfSeeing", "HandBackRetry", ex);
        }
    }

    private void GiveUpOnHandBack(Exception ex)
    {
        _handBackOwed = false;
        Swallow(() => _outcomes.Fail(SeatChecks.FightOnId, _key + ":hand-back-failed", "mission=" + _key + " reason=hand-back-failed tries="
            + _handBackTries.ToString(System.Globalization.CultureInfo.InvariantCulture) + " error=" + ex.GetType().Name));
    }

    private void HeroFell()
    {
        _handed = false;
        _fightChosen = false;
        SeatPresence.SetHeroFightsOn(false);
        _outcomes.Skip(SeatChecks.FightOnId, _key + ":fell", "mission=" + _key + " reason=fell");
    }

    // A Retreat order or a rout would retreat the handed hero as it retreats every AI unit of his company, and a rout wounds a hero.
    private void CheckForRetreat()
    {
        var now = _screen.RealSeconds();
        if (!SeatFightRule.RetreatCheckDue(now, _lastRetreatCheck))
            return;
        _lastRetreatCheck = now;
        if (_hero.StopHeroRetreat())
            _outcomes.Pass(SeatChecks.FightOnId, _key + ":retreat-stopped", "mission=" + _key + " event=retreat-stopped");
    }

    private void NoteTheFall()
    {
        if (_hero.HeroActive())
        {
            _fallSaid = false;
            return;
        }

        if (_fallSaid || !_permissions().TakeSoldier || !_settings.TakeSoldierAfterFall)
            return;
        _fallSaid = true;
        _screen.Say(SeatMessage.HeroFell);
    }

    private void TakePress()
    {
        if (!_permissions().TakeSoldier)
        {
            SkipTake("denied");
            return;
        }

        if (!_settings.TakeSoldierAfterFall)
        {
            SkipTake("setting-off");
            return;
        }

        var result = _screen.TryTake();
        switch (result.Outcome)
        {
            case SeatTakeOutcome.Taken:
                _fallSaid = false;
                _outcomes.Pass(SeatChecks.TakeId, _key + ":" + result.Kind, "mission=" + _key + " kind=" + result.Kind + " formations="
                    + result.Formations.ToString(System.Globalization.CultureInfo.InvariantCulture));
                break;
            case SeatTakeOutcome.HeroAlive:
                SkipTake("hero-alive");
                _screen.Say(SeatMessage.TakeHeroAlive);
                break;
            case SeatTakeOutcome.Nobody:
                SkipTake("nobody");
                _screen.Say(SeatMessage.TakeNobody);
                break;
            default:
                SkipTake(SeatTakeRule.Reason(result.Refusal));
                _screen.Say(SeatMessage.TakeRefused);
                break;
        }
    }

    private void SkipTake(string reason) =>
        _outcomes.Skip(SeatChecks.TakeId, _key + ":" + reason, "mission=" + _key + " reason=" + reason);

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
        if (facts.Deployment || facts.Interlude || facts.PhotoMode || facts.MissionEnded || facts.RadialMenuActive)
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
        else if (!facts.PhotoMode && State == SeatState.Seated)
            PanelWork();
    }

    private void PanelWork()
    {
        var open = _screen.IsPanelOpen();

        // The close signals are game keys that only the order handler registers on the scene layer, and a read of a key nobody registered
        // throws (InputContext.cs:226-233). A mission with no handler has no panel, and a closing edge always has the panel open on the
        // frame before it, so no edge is lost by reading only around an open panel (6.2).
        var keepOpen = (open || _panelWasOpen) && _settings.KeepPanelOpen;
        var signals = keepOpen ? _screen.ReadCloseSignals() : default;
        if (keepOpen && SeatPanelRule.ClosingEdge(_panelWasOpen, open) && SeatPanelRule.ShouldReopen(signals, _previousSignals, _screen.TransferMenuOpen()))
            open = TryReopen();

        _panelWasOpen = open;
        _previousSignals = signals;

        var wanted = open && _settings.CursorWithPanel;
        var look = wanted && _screen.LookHeld();
        if (look)
            _screen.HoldCursorPosition();
        ShowCursor(wanted && !look);
        if (_cursorShown && !_focusNoted && SeatPresence.TakeFocusSet())
        {
            _focusNoted = true;
            _outcomes.Pass(SeatChecks.FocusId, _key, "mission=" + _key + " focus=cursor");
        }

        if (_hideInForce)
        {
            var why = SeatHideRule.RestoreReason(open, _screen.DialogShown(), _screen.OtherLayerFocused());
            if (why != null)
                RestoreHide(why);
        }
    }

    // A refused reopen is not a reopen: OpenToggleOrder returns without a word when the hero or the last troop is gone
    // (MissionOrderVM.cs:716-719), so the line is a pass only if the field it sets (:720) reads open.
    private bool TryReopen()
    {
        if (!_screen.OpenPanel())
        {
            _outcomes.Fail(SeatChecks.PanelReopenedId, _key, "mission=" + _key + " reason=site");
            return false;
        }

        if (!_screen.IsPanelOpen())
        {
            _outcomes.Skip(SeatChecks.PanelReopenedId, _key + ":refused", "mission=" + _key + " reason=refused");
            return false;
        }

        _outcomes.Pass(SeatChecks.PanelReopenedId, _key, "mission=" + _key + " panel=reopened");
        return true;
    }

    private void ShowCursor(bool show)
    {
        if (show == _cursorShown)
            return;
        _cursorShown = show;
        SeatPresence.SetCursor(show);
        _screen.SetCursor(show);
        if (show)
            _outcomes.Pass(SeatChecks.CursorId, _key, "mission=" + _key + " cursor=shown");
    }

    private void BeginHide()
    {
        _hideSaved = _screen.ReadUiHidden();
        _screen.WriteUiHidden(true);
        _hideInForce = true;
        _outcomes.Pass(SeatChecks.HideId, _key + ":hidden", "mission=" + _key + " interface=hidden");
    }

    private void RestoreHide(string reason)
    {
        _hideInForce = false;
        _screen.WriteUiHidden(_hideSaved);
        _outcomes.Pass(SeatChecks.HideId, _key + ":restored-" + reason, "mission=" + _key + " interface=restored reason=" + reason);
    }

    private void KeepPress()
    {
        if (_keep != SeatKeepKind.None)
        {
            LetGo("key");
            return;
        }

        var kind = _screen.TryKeep();
        if (kind == SeatKeepKind.None)
        {
            _outcomes.Skip(SeatChecks.KeepId, _key + ":nobody", "mission=" + _key + " reason=nobody");
            _screen.Say(SeatMessage.KeepNobody);
            return;
        }

        _keep = kind;
        _orbit = SeatOrbit.Begin(_applied);
        _outcomes.Pass(SeatChecks.KeepId, _key + ":" + kind, "mission=" + _key + " kind=" + kind);
    }

    private void LetGo(string reason, SeatMessage? message = null)
    {
        if (_keep == SeatKeepKind.None)
            return;
        var kind = _keep;
        _keep = SeatKeepKind.None;
        _screen.Unkeep();
        _outcomes.Pass(SeatChecks.LetGoId, _key + ":" + reason, "mission=" + _key + " reason=" + reason + " kind=" + kind);
        if (message.HasValue)
            _screen.Say(message.Value);
    }

    private SeatPose FlightPose(bool rising, SeatInput read, float dt, out float fraction)
    {
        if (rising)
        {
            if (FiniteFloatValidator.IsFinite(dt) && dt > 0f)
                _elapsed += dt;
        }
        else
        {
            _flight = Bounded(SeatCameraMath.Step(_flight, read, dt), _flight);
        }

        var flightGround = _screen.GroundHeight(_flight.X, _flight.Y, _applied.Z);
        _ground = SeatCameraMath.SmoothGround(_ground, flightGround, dt);
        _flight = SeatCameraMath.CapFlight(_flight, _heightLimit, _ground);
        var goal = SeatCameraMath.Pose(_flight, _ground);
        fraction = SeatCameraMath.BlendFraction(_elapsed);
        var pose = SeatCameraMath.CapHeight(rising ? SeatCameraMath.Blend(_from, goal, fraction) : goal, _heightLimit);

        var rawGroundUnderPose = rising ? _screen.GroundHeight(pose.X, pose.Y, pose.Z) : flightGround;
        return SeatCameraMath.AboveGround(pose, rawGroundUnderPose);
    }

    private bool TryOrbitPose(SeatInput read, float dt, out SeatPose pose)
    {
        pose = default;
        if (_keep == SeatKeepKind.None)
            return false;
        var target = _screen.ReadKept();
        if (target.Gone)
        {
            LetGo("gone", SeatMessage.KeepGone);
            return false;
        }

        if (SeatKeepRule.LetsGo(read))
        {
            LetGo("flight");
            return false;
        }

        _orbit = SeatOrbit.Step(_orbit, read, target.X, target.Y, target.Z, dt);
        var raw = SeatOrbit.Pose(_orbit);
        var (x, y) = _screen.ClampToBoundary(raw.X, raw.Y);
        var inside = FiniteFloatValidator.IsFinite(x) && FiniteFloatValidator.IsFinite(y);
        pose = SeatCameraMath.CapHeight(new SeatPose(inside ? x : _applied.X, inside ? y : _applied.Y, raw.Z, raw.Yaw, raw.Pitch), _heightLimit);
        var rawGround = _screen.GroundHeight(pose.X, pose.Y, pose.Z);
        pose = SeatCameraMath.AboveGround(pose, rawGround, SeatCameraMath.MinHeight);
        _ground = SeatCameraMath.SmoothGround(_ground, rawGround, dt);
        _flight = SeatCameraMath.FlightOf(pose, _ground);
        return true;
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
        _panelWasOpen = false;

        _controllerShut = true;
        _screen.SetHeroController(false);
        _standFastOwed = true;
        var fightOn = _permissions().FightOn;
        _fightChosen = fightOn && _settings.HeroFightsOn;
        SeatPresence.Set(true, holdsBoundary: fightOn);
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
        Cleanup(latching: false);

        var lookWritten = !writeLook || _screen.WriteLook(look.Yaw, look.Pitch);
        var began = glide && _screen.BeginGlide();
        GlideBegan = began;
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

    private void Cleanup(bool latching)
    {
        Step(latching, () => SeatPresence.Set(false));
        if (_controllerShut)
            Step(latching, ReleaseController);
        Step(latching, () => ShowCursor(false));
        if (_hideInForce)
            Step(latching, () => RestoreHide(latching ? "fault" : "leave"));
        Step(latching, () => LetGo("leave"));
    }

    private void Step(bool latching, Action step)
    {
        if (latching)
            Swallow(step);
        else
            step();
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
        Cleanup(latching: true);
        Swallow(() => SeatPresence.SetHeroFightsOn(false));

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
