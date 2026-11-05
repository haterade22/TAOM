using System;
using TaleWorlds.Core;
using TaleWorlds.MountAndBlade;
using TaleWorlds.MountAndBlade.View;
using TaleWorlds.MountAndBlade.View.MissionViews;
using TAOM.Adapters;
using TAOM.Client.Adapters;
using TAOM.Client.Features.SeatOfSeeing.Trial;
using TAOM.Core.Logging;
using TAOM.Features.CoopInterop;
using TAOM.Features.SeatOfSeeing;
using TAOM.Features.SeatOfSeeing.Domain;

namespace TAOM.Client.Features.SeatOfSeeing;

/// <summary>
/// The Seat of Seeing's entry point (docs/features/seat-of-seeing.md): a commander's view that any mission with a screen can use.
/// One key lifts the player's view off the hero, flight input steers it, and the same key brings it back with a glide. Built by the
/// engine as a <c>[DefaultView]</c> in every mission with a screen, because a view TAOM adds in OnMissionBehaviorInitialize is never
/// registered and never ticks (#970); so every field stays null-safe, and a player who never presses the key pays only the checks
/// that docs/features/seat-of-seeing.md lists under Performance and Field trial. The key is read in <c>OnMissionScreenTick</c> (once
/// per real frame, never in <c>OnMissionTick</c>, which fast time repeats) and the flight in <c>UpdateOverridenCamera</c>. All rules
/// are in <see cref="SeatSession"/> and <see cref="SeatCameraMath"/>.
/// </summary>
[DefaultView]
public sealed class SeatView : MissionView
{
    private static int _missionSerial;

    private SeatSession? _session;
    private SeatHotKeys? _keys;
    private SeatKeys? _moreKeys;
    private CommandTrialScript? _trial;
    private bool _seatKeyPressed;

    public override void OnMissionScreenInitialize()
    {
        base.OnMissionScreenInitialize();
        IModLogger? resolved = null;
        try
        {
            var logger = resolved = IoC.Resolve<IModLogger>();
            if (MissionScreen == null || Mission == null)
                return;

            _keys = IoC.Resolve<SeatHotKeys>();
            var coop = IoC.Resolve<ICoopSessionProvider>();
            var mission = Mission;
            bool? foreignCamera = null;
            // The walk for another mod's camera is read the first time it is asked (5.6): every view is built by then. The kind is read fresh
            // each time, because Mission.Mode changes it (section 4).
            SeatPermissions Permissions() => SeatMissionPolicy.Decide(
                SeatMissionClassifier.ReadFacts(mission, coop, foreignCamera ??= SeatMissionClassifier.ForeignCameraOwns(mission, logger)));

            var serial = (++_missionSerial).ToString(System.Globalization.CultureInfo.InvariantCulture);
            var screen = new SeatScreenAdapter(MissionScreen, mission, logger);
            var session = _session = new SeatSession(screen, IoC.Resolve<ISeatHeroAdapter>(),
                CommandTrialRun.Settings(IoC.Resolve<ISeatSettingsProvider>()), Permissions, IoC.Resolve<IOutcomeLog>(), logger, serial);
            _moreKeys = new SeatKeys(_keys, session);
            mission.OnMainAgentChanged += OnMainAgentChanged;
            _trial = CommandTrialRun.Script(() => new CommandTrialHost(this, session, screen, mission, MissionScreen, logger), logger);
        }
        catch (Exception ex)
        {
            _session = null;
            resolved?.LogError($"[SeatOfSeeing] the Seat could not start for this mission: {ex}");
        }
    }

    public override void OnMissionScreenTick(float dt)
    {
        base.OnMissionScreenTick(dt);
        if (_session != null && _keys != null)
        {
            if (SeatKeys.Pressed(_keys.TakeTheSeat, Input))
                PressSeatKey();

            var pressed = _seatKeyPressed;
            _seatKeyPressed = false;
            _moreKeys?.Tick(Input, dt);
            _session.OnScreenTick(pressed);
        }

        _trial?.OnScreenTick(dt);
    }

    public override bool UpdateOverridenCamera(float dt) => _session != null && _session.OnFrame(dt, TrialInput);

    public override void OnMissionTick(float dt)
    {
        base.OnMissionTick(dt);
        _session?.OnMissionTick();
        _trial?.OnMissionTick();
    }

    // The Deployment to Battle edge comes from the engine: Mission.SetMissionMode raises this on every behaviour (Mission.cs:1630-1633).
    public override void OnMissionModeChange(MissionMode oldMissionMode, bool atStart)
    {
        base.OnMissionModeChange(oldMissionMode, atStart);
        if (oldMissionMode == MissionMode.Deployment && Mission != null && Mission.Mode == MissionMode.Battle)
            _session?.OnDeploymentEnded();
    }

    // Two teardown paths: the end-mission path (MissionScreen.cs:4241-4245) and MissionState.OnFinalize, which removes the behaviours
    // with no EndMission. Finishing is terminal: a screen tick can still run after the second (MissionScreen.cs:3688), and must not re-enter.
    public override void OnMissionScreenFinalize()
    {
        base.OnMissionScreenFinalize();
        EndSession();
    }

    public override void OnRemoveBehavior()
    {
        base.OnRemoveBehavior();
        EndSession();
    }

    // An armed field trial runs with nobody at the keyboard, and a lost focus would open the escape menu and pause the mission.
    public override bool IsOpeningEscapeMenuOnFocusChangeAllowed() => !CommandTrialRun.IsArmed && base.IsOpeningEscapeMenuOnFocusChangeAllowed();

    private void EndSession()
    {
        _trial = null;
        _moreKeys = null;
        _session?.Finish();
        _session = null;
        if (Mission != null)
            Mission.OnMainAgentChanged -= OnMainAgentChanged;
    }

    private void OnMainAgentChanged(Agent oldAgent) => _session?.OnMainAgentChanged();

    internal void PressSeatKey() => _seatKeyPressed = true;

    internal bool PressPending => _seatKeyPressed;

    internal SeatInput? TrialInput { get; set; }
}
