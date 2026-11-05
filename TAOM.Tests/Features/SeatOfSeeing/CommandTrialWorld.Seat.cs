using System;
using System.Collections.Generic;
using TAOM.Client.Features.SeatOfSeeing;
using TAOM.Features.FordsOfWar.Domain;
using TAOM.Features.SeatOfSeeing.Domain;

namespace TAOM.Tests.Features.SeatOfSeeing;

/// <summary>The Seat half of the stand-in: the key (taken at the next screen tick), the blend, the flight and the return glide, by the Seat's own arithmetic in <see cref="SeatCameraMath"/>.</summary>
internal sealed partial class CommandTrialWorld
{
    internal float FlightHeight = 22f;
    /// <summary>The height a re-entry comes back at; null keeps the last flight's, as the Seat does.</summary>
    internal float? ReenterHeight;
    internal float FlightSign = 1f;
    internal bool FliesSideways;
    internal bool LiftDoesNothing;
    internal float HighestFlight = 22f;
    internal float BlendSeconds = 0.7f;
    internal SeatInput? Input;

    internal bool PressesAreIgnored;
    internal int IgnorePressesFrom;
    internal bool GlideArms = true;
    internal bool FaultOnEntry;
    internal bool FaultInDeployment;
    internal bool FaultOnLeave;
    internal float SpeedFactor = 1f;
    internal float FramesPerOwnedTick = 1f;
    internal float GlideSeconds = 1f;
    internal bool GlideStuck;
    internal bool HoldsTheSeat;
    internal bool SeatIgnoresDeployment;

    internal readonly List<float> PressTimes = new();
    internal int PressesInDeployment;
    internal float? ForwardHeldAt;
    internal float SeatedAt = float.NaN;

    private bool _pending;
    private float _blend;
    private float _framesOwned;
    private float _glideLeft;
    private bool _everEntered;

    public SeatState State { get; private set; }

    public bool Faulted { get; private set; }

    public bool GlideBegan { get; private set; }

    public bool PressPending => _pending;

    public Vec3f? ViewForward() => new Vec3f(0f, 1f, 0f);

    public int Frames => (int)_framesOwned;

    internal int Presses => PressTimes.Count;

    public void PressSeatKey()
    {
        _pending = true;
        PressTimes.Add(Time);
        if (Deployment)
            PressesInDeployment++;
    }

    internal readonly List<SeatInput?> InputLog = new();

    public void SetInput(SeatInput? input)
    {
        Input = input;
        InputLog.Add(input);
        if (input is { Forward: > 0f })
            ForwardHeldAt ??= Time;
    }

    private void TakeThePress()
    {
        if (_pending && !PressesAreIgnored && (IgnorePressesFrom == 0 || Presses < IgnorePressesFrom))
        {
            _pending = false;
            Take();
        }
    }

    private void Take()
    {
        if (Deployment && !SeatIgnoresDeployment)
        {
            Faulted |= FaultInDeployment;
            return;
        }

        if (State != SeatState.Off && HoldsTheSeat)
            return;
        if (State == SeatState.Off)
        {
            if (FaultOnEntry)
            {
                Faulted = true;
                return;
            }

            _fightChosen = FightOnSetting;
            State = BlendSeconds <= 0f ? SeatState.Seated : SeatState.Rising;
            SeatedAt = State == SeatState.Seated ? Time : SeatedAt;
            _blend = 0f;
            _framesOwned = 0f;
            if (_everEntered && ReenterHeight.HasValue)
                FlightHeight = ReenterHeight.Value;
            _everEntered = true;
            View = new Vec3f(Hero.X, Hero.Y - 9f, GroundZ + FlightHeight);
            return;
        }

        State = SeatState.Off;
        GlideBegan = GlideArms;
        Faulted |= FaultOnLeave;
        if (GlideSeconds <= 0f)
            View = HeroCamera;
        else
            _glideLeft = GlideSeconds;
    }

    private void Frame(float dt)
    {
        MarshalFrame(dt);
        ResolveStageTwoAndThree();
        ResolveStageFourAndFive();
        ResolveRise(dt);
        if (State == SeatState.Rising)
        {
            _blend += dt;
            _framesOwned += FramesPerOwnedTick;
            if (_blend >= BlendSeconds)
            {
                State = SeatState.Seated;
                SeatedAt = Time;
            }
        }
        else if (State == SeatState.Seated)
        {
            _framesOwned += FramesPerOwnedTick;
            if (!ViewFrozen && Input.HasValue)
            {
                var speed = SeatCameraMath.PanSpeed(FlightHeight) * SpeedFactor;
                var along = Input.Value.Forward * speed * dt;
                if (Input.Value.Lift > 0f && !LiftDoesNothing)
                    FlightHeight = Math.Min(SeatCameraMath.MaxHeight, FlightHeight + SeatCameraMath.LiftSpeed(FlightHeight) * dt);
                HighestFlight = Math.Max(HighestFlight, FlightHeight);
                View = FliesSideways
                    ? new Vec3f(View.X + along, View.Y, GroundZ + FlightHeight)
                    : new Vec3f(View.X, View.Y + FlightSign * along, GroundZ + FlightHeight);
            }
        }
        else if (_glideLeft > 0f && !GlideStuck)
        {
            _glideLeft = Math.Max(0f, _glideLeft - dt);
            var t = 1f - _glideLeft / GlideSeconds;
            View = Vec3f.Lerp(View, HeroCamera, Math.Min(1f, t * 3f));
        }
    }

    // ---- Stage S2: the pointer, the panel and the screen ----

    internal bool PanelSiteMissing;
    internal bool CursorWorks = true;
    internal bool KeepOpenWorks = true;
    internal bool PlayerCloseReopens;
    internal bool HideWorks = true;
    internal bool RestoreOnPanelWorks = true;
    internal bool FocusWorks = true;
    internal bool WindowHasFocus = true;
    internal bool WindowLosesFocusAfterTheMove;
    internal bool CursorGoesWhereAsked = true;
    internal bool UiStartsHidden;
    /// <summary>Screen ticks until a reopen shows: 1 is the Seat's own, on the tick after the close.</summary>
    internal int ReopenDelayFrames = 1;
    internal bool FaultMidStep;
    internal bool PanelStaysOpenOnClose;
    internal bool PlainCloseDoesNothing;
    internal bool CursorStaysOnClose;
    internal bool CursorAlreadyShown;
    internal bool LeavesSeatAtTheEnd;

    internal readonly List<string> Writes = new();
    internal readonly List<string> WritesOutsideTheMissionTick = new();
    internal bool InMissionTick;
    internal int PlayerCloses;
    internal int PlainCloses;
    internal int HidePresses;

    private bool _panelIsOpen;
    private bool _everOpened;
    private bool _closeRequested;
    private int _reopenIn;
    private bool _uiHiddenSet;
    private bool _uiHidden;
    private float _wantedX = float.NaN;
    private float _wantedY = float.NaN;
    private float _pixelX;
    private float _pixelY;

    public bool PanelOpen => _panelIsOpen;

    // The cursor follows the panel the player asked to close, so a panel that stays open on a close request (PanelStaysOpenOnClose) hides the
    // cursor on its own, and the two facts of the close step can each be broken alone.
    public bool MouseVisible => State == SeatState.Seated
        && ((CursorAlreadyShown && !_everOpened) || (CursorWorks && ((_panelIsOpen && !_closeRequested) || (CursorStaysOnClose && _everOpened))));

    public bool UiHidden => _uiHiddenSet ? _uiHidden : UiStartsHidden;

    public bool WindowFocused => WindowHasFocus && !(WindowLosesFocusAfterTheMove && !float.IsNaN(_wantedX));

    public bool OpenPanel()
    {
        Note("open-panel");
        if (PanelSiteMissing)
            return false;
        Faulted |= FaultMidStep;
        if ((_heroOnAi && !PanelOpensForAiHero) || (_taken && PanelClosedForTheTakenSoldier))
            return true;
        _panelIsOpen = true;
        _everOpened = true;
        _closeRequested = false;
        if (RestoreOnPanelWorks)
            SetUi(false);
        return true;
    }

    public void ClosePanel(bool asPlayer)
    {
        Note("close-panel");
        if (PanelSiteMissing)
            return;
        if (asPlayer)
            PlayerCloses++;
        else
            PlainCloses++;
        if (LeavesSeatAtTheEnd)
            State = SeatState.Off;
        _closeRequested = true;
        if (PanelStaysOpenOnClose || (!asPlayer && PlainCloseDoesNothing))
            return;
        _panelIsOpen = false;
        _reopenIn = (asPlayer ? PlayerCloseReopens : KeepOpenWorks) ? Math.Max(1, ReopenDelayFrames) : 0;
    }

    // The session ticks before the trial in the same screen tick (SeatView.cs:81-84), so a reopen shows to the step's next resume.
    private void TakeTheReopen()
    {
        if (_reopenIn > 0 && --_reopenIn == 0)
            _panelIsOpen = true;
    }

    public void PressHide()
    {
        HidePresses++;
        Faulted |= FaultMidStep;
        if (State == SeatState.Seated && HideWorks)
            SetUi(!UiHidden);
    }

    private void SetUi(bool hidden)
    {
        _uiHiddenSet = true;
        _uiHidden = hidden;
    }

    public void MoveCursorTo(float fractionX, float fractionY)
    {
        _wantedX = (int)(fractionX * 1920f);
        _wantedY = (int)(fractionY * 1080f);
        _pixelX = CursorGoesWhereAsked ? _wantedX : 0f;
        _pixelY = CursorGoesWhereAsked ? _wantedY : 0f;
    }

    public SeatFocusReading ProbeFocus() =>
        new(_pixelX, _pixelY, _wantedX, _wantedY, _pixelX, _pixelY, FocusWorks ? _pixelX : 960f, FocusWorks ? _pixelY : 540f, "none");

    public int Said { get; private set; }

    public SeatMessage? LastSaid { get; private set; }

    // ---- Stage S3: keep to a soldier or a company ----

    internal bool KeepFindsNobody;
    internal bool KeepsTheWrongOne;
    internal bool DeathLetsGo = true;
    internal bool SoldierAvailable = true;
    internal bool AimConverges = true;
    internal bool KeyLetsGo = true;
    internal bool CompanyDeathLetsGo = true;
    internal int DeathSays = 1;
    internal int CompanyDeathSays = 1;
    internal bool DeathSaysTheWrongThing;
    internal bool CompanyDeathSaysTheWrongThing;
    internal bool KeepsTheSoldierInstead;
    internal bool CompanyKeepFindsNobody;
    internal float? LosesTheKeepAfterSeconds;
    internal int CompanyUnits = 30;
    internal int SmallestCompanyUnits = 12;
    internal float KeptDistance = 16f;
    internal bool NoCompany;
    internal readonly List<string> Kills = new();

    private float _keptAt;
    private bool _everKept;
    private bool _deathNoticed;
    private bool _keepPressed;
    private bool _choseCompany;
    private bool _soldierDead;
    private bool _companyDead;
    private float _aimError = 0.5f;

    public SeatKeepKind Keeping { get; private set; }

    public bool KeepsChosen => Keeping != SeatKeepKind.None && !KeepsTheWrongOne;

    public void PressKeep()
    {
        _keepPressed = true;
        Faulted |= FaultMidStep;
        if (LeavesSeatAtTheEnd && Keeping != SeatKeepKind.None)
            State = SeatState.Off;
    }

    // Once something was kept the camera has a distance to it, also after the keep ended, so a keep that ends early fails on that fact alone.
    public float DistanceToKept() => _everKept ? KeptDistance : float.NaN;

    public bool ChooseSoldier()
    {
        _choseCompany = false;
        _aimError = 0.5f;
        _everKept = false;
        _deathNoticed = false;
        _soldierDead = false;
        return SoldierAvailable;
    }

    public SeatAimStep AimAtChosen()
    {
        if (_soldierDead)
            return new SeatAimStep(default, false, true);
        _aimError = AimConverges ? _aimError * 0.3f : _aimError;
        return new SeatAimStep(new SeatInput(0f, 0f, 0f, 0f, _aimError * 100f, 0f, false), SeatTrialAim.OnTarget(_aimError), false);
    }

    public int ChooseCompany(bool largest)
    {
        _choseCompany = true;
        _companyDead = false;
        _everKept = false;
        _deathNoticed = false;
        return NoCompany ? 0 : largest ? CompanyUnits : SmallestCompanyUnits;
    }

    public void SelectChosenCompany()
    {
        Note("select-company");
        Faulted |= FaultMidStep;
    }

    public void KillChosenSoldier()
    {
        Note("kill-soldier");
        Kills.Add("soldier");
        _soldierDead = true;
    }

    public void KillChosenCompany()
    {
        Note("kill-company");
        Kills.Add("company");
        _companyDead = true;
    }

    private void ResolveStageTwoAndThree()
    {
        if (State != SeatState.Seated)
        {
            Keeping = SeatKeepKind.None;
            _keepPressed = false;
            return;
        }

        if (_keepPressed)
        {
            _keepPressed = false;
            if (Keeping != SeatKeepKind.None)
            {
                if (KeyLetsGo)
                    Keeping = SeatKeepKind.None;
            }
            else if (KeepFindsNobody || (_choseCompany && CompanyKeepFindsNobody))
            {
                Say(SeatMessage.KeepNobody);
            }
            else
            {
                Keeping = _choseCompany && !KeepsTheSoldierInstead ? SeatKeepKind.Company : SeatKeepKind.Soldier;
                _keptAt = Time;
                _everKept = true;
            }
        }

        if (Keeping != SeatKeepKind.None && LosesTheKeepAfterSeconds.HasValue && Time - _keptAt >= LosesTheKeepAfterSeconds.Value)
            Keeping = SeatKeepKind.None;

        // The Seat notices the death once. Whether it lets go and what it says are separate facts, so each can be broken alone.
        var gone = (Keeping == SeatKeepKind.Soldier && _soldierDead) || (Keeping == SeatKeepKind.Company && _companyDead);
        if (gone && !_deathNoticed)
        {
            _deathNoticed = true;
            var wasCompany = Keeping == SeatKeepKind.Company;
            if (wasCompany ? CompanyDeathLetsGo : DeathLetsGo)
                Keeping = SeatKeepKind.None;
            var says = wasCompany ? CompanyDeathSays : DeathSays;
            for (var i = 0; i < says; i++)
                Say((wasCompany ? CompanyDeathSaysTheWrongThing : DeathSaysTheWrongThing) ? SeatMessage.KeepNobody : SeatMessage.KeepGone);
        }
    }

    // ---- Stage S4 and S5: the hero fights on, falls, and a soldier is taken ----

    internal bool FightOnSetting;
    internal bool HandsTheHero = true;
    internal bool HandsTheHeroBack = true;
    internal bool HeroFightsForReal = true;
    internal bool MoraleMakesHimPanic;
    internal bool MoraleMakesHimRetreat;
    /// <summary>The write of the morale reaches no component, as when the hero is not on the AI: he keeps the morale he had.</summary>
    internal bool MoraleWriteIsIgnored;
    internal bool PanicsOnHandOff;
    internal bool RetreatsOnHandOff;
    internal bool HandOffRaisesGate = true;
    internal bool GateStaysUpAfterHandBack;
    /// <summary>The company ignores the Stop order and goes on moving, as a company in the middle of a charge may.</summary>
    internal bool CompanyIgnoresTheStop;
    /// <summary>How fast the hero walks once he is the player's again, after having been on the AI.</summary>
    internal float DriftAfterHandBackPerSecond;
    internal int TakePresses;
    internal SeatKeepKind? KeepingAtTheTake;
    /// <summary>Presses of the Seat key made before the company was told to stop.</summary>
    internal int PressesWhenStopped = -1;
    internal bool TheFallEndsTheKeep;
    internal bool PanelClosedForTheTakenSoldier;
    internal float? DropsTheHandOffAfter;
    /// <summary>Seconds after the leave before the hero is handed back; 0 hands him back on the first mission tick.</summary>
    internal float HandBackDelaySeconds;
    internal bool PanelOpensForAiHero = true;
    internal bool HeroCanDie = true;
    internal bool FallLowersTheGate = true;
    /// <summary>Real seconds after the kill before the Seat reacts (gate, message). The engine clears the main agent in the kill and the Seat sees that at its next mission tick, so 0 is that tick, never the kill's own.</summary>
    internal float FallReactionSeconds;
    internal int FallSays = 1;
    internal bool FallSaysTheWrongThing;
    internal int OwnedFormations = 3;
    internal bool TakeWorks = true;
    internal bool FaultOnTake;
    internal bool TakesTheWrongSoldier;
    internal bool TakeHandsTheCommand = true;
    internal bool TakeLeavesOneFormationOnTheAi;
    internal bool TakeHandsTheController = true;
    internal int TakeHandsOver = -1;
    internal readonly List<string> FightWrites = new();

    private bool _fightChosen;
    private bool _heroOnAi;
    private bool _handedOnce;
    private bool _gate;
    private bool _charged;
    private bool _moraleWritten;
    private bool _takePressed;
    private bool _heroDead;
    private bool _fallSeen;
    private float _fallAt;
    private bool _taken;
    private float _handOffAt = float.NaN;
    private float _unwantedSince = float.NaN;

    public void SetFightOn(bool fightOn)
    {
        FightOnSetting = fightOn;
        Faulted |= FaultMidStep;
    }

    public bool FightGateUp => _gate;

    public bool HeroOnAi => _heroOnAi;

    public bool HeroRetreating { get; private set; }

    public bool HeroPanicked { get; private set; }

    public float HeroMorale => HeroPresent ? (_moraleWritten ? 0.01f : 50f) : float.NaN;

    public bool HeroMortal => HeroCanDie;

    private float FightDrift => _heroOnAi && _charged && HeroFightsForReal ? 3f : 0f;

    private float AfterHandBackDrift => _handedOnce && !_heroOnAi && HeroPresent ? DriftAfterHandBackPerSecond : 0f;

    public void ChargeHeroFormation()
    {
        Note("charge");
        _charged = true;
    }

    public void StopHeroFormation()
    {
        Note("stop-formation");
        PressesWhenStopped = Presses;
        if (!CompanyIgnoresTheStop)
            _charged = false;
    }

    public void WriteHeroMorale(float morale)
    {
        Note("morale");
        _moraleWritten = !MoraleWriteIsIgnored;
        if (_heroOnAi)
        {
            HeroPanicked |= MoraleMakesHimPanic;
            HeroRetreating |= MoraleMakesHimRetreat;
        }
    }

    public void KillHero()
    {
        Note("kill-hero");
        Kills.Add("hero");
        // The engine removes the agent and clears the main agent inside the kill (Mission.cs:3021), before the Seat's next mission tick runs.
        _heroDead = true;
        if (TheFallEndsTheKeep)
            Keeping = SeatKeepKind.None;
        _fallSeen = false;
        _fallAt = Time + FallReactionSeconds;
        HeroPresent = false;
        _heroOnAi = false;
    }

    public int FormationsOwnedByHero() => HeroPresent ? OwnedFormations : 0;

    public void PressTake()
    {
        TakePresses++;
        KeepingAtTheTake = Keeping;
        _takePressed = true;
        Faulted |= FaultOnTake;
    }

    public bool TookFromKeptCompany => _taken && !TakesTheWrongSoldier;

    public SeatHandOverReading ReadHandOver()
    {
        if (!_taken)
            return new SeatHandOverReading(false, 0, 0);
        var owned = TakeHandsOver >= 0 ? TakeHandsOver : TakeHandsTheCommand ? OwnedFormations : 0;
        return new SeatHandOverReading(TakeHandsTheController, owned, TakeLeavesOneFormationOnTheAi && owned > 0 ? 1 : 0);
    }

    // The mission tick: the hand-off compares what is wanted with what was done (the Seat's own rule), the hero falls, and a pressed take acts.
    private void ResolveStageFourAndFive()
    {
        var wanted = State != SeatState.Off && !Faulted && _fightChosen;
        if (_heroDead && !_fallSeen)
        {
            if (Time >= _fallAt)
            {
                _fallSeen = true;
                if (FallLowersTheGate)
                    _gate = false;
                if (State == SeatState.Seated)
                    for (var i = 0; i < FallSays; i++)
                        Say(FallSaysTheWrongThing ? SeatMessage.TakeNobody : SeatMessage.HeroFell);
            }
        }
        else if (HeroPresent)
        {
            if (wanted && !_heroOnAi && HandsTheHero)
            {
                _heroOnAi = true;
                _handedOnce = true;
                _gate = HandOffRaisesGate;
                _handOffAt = Time;
                HeroPanicked |= PanicsOnHandOff;
                HeroRetreating |= RetreatsOnHandOff;
            }
            else if (!wanted && _heroOnAi && HandsTheHeroBack && UnwantedLongEnough())
            {
                _unwantedSince = float.NaN;
                _heroOnAi = false;
                _gate = GateStaysUpAfterHandBack;
                _charged = false;
            }
            else if (_heroOnAi && DropsTheHandOffAfter.HasValue && Time - _handOffAt >= DropsTheHandOffAfter.Value)
            {
                _heroOnAi = false;
                HandsTheHero = false;
            }
        }

        if (_takePressed)
        {
            _takePressed = false;
            if (!HeroPresent && TakeWorks && State != SeatState.Off)
            {
                HeroPresent = true;
                _heroDead = false;
                _taken = true;
            }
        }
    }

    private bool UnwantedLongEnough()
    {
        if (float.IsNaN(_unwantedSince))
            _unwantedSince = Time;
        return Time - _unwantedSince >= HandBackDelaySeconds;
    }

    private void Note(string write)
    {
        Writes.Add(write);
        if (!InMissionTick)
            WritesOutsideTheMissionTick.Add(write);
    }

    private void Say(SeatMessage message)
    {
        Said++;
        LastSaid = message;
    }
}
