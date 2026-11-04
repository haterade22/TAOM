using System;
using TaleWorlds.Core;
using TaleWorlds.InputSystem;
using TaleWorlds.MountAndBlade;
using TaleWorlds.MountAndBlade.View;
using TaleWorlds.MountAndBlade.View.MissionViews;
using TAOM.Adapters;
using TAOM.Client.Adapters;
using TAOM.Core.Logging;
using TAOM.Features.SeatOfSeeing;
using TAOM.Features.SeatOfSeeing.Domain;

namespace TAOM.Client.Features.SeatOfSeeing;

/// <summary>
/// The Seat of Seeing's entry point (docs/features/seat-of-seeing.md): a commander's view that any mission with a screen can use.
/// One key lifts the player's view off the hero, flight input steers it, and the same key brings it back with a glide. Built by the
/// engine as a <c>[DefaultView]</c> in every mission with a screen, because a view TAOM adds in OnMissionBehaviorInitialize is never
/// registered and never ticks (#970); so every field stays null-safe, and nothing costs more than one key test a frame until the
/// key is pressed. The key is read in <c>OnMissionScreenTick</c> (once per real frame, never in <c>OnMissionTick</c>, which fast time
/// repeats) and the flight in <c>UpdateOverridenCamera</c>. All rules are in <see cref="SeatSession"/> and <see cref="SeatCameraMath"/>.
/// </summary>
[DefaultView]
public sealed class SeatView : MissionView
{
    private static int _missionSerial;

    private SeatSession? _session;
    private SeatHotKeys? _keys;

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
            var mission = Mission;
            SeatPermissions? permissions = null;
            // Read the first time it is asked (5.6): every view is built by then, so the foreign-camera walk sees them all.
            SeatPermissions Permissions() => permissions ??= SeatMissionPolicy.Decide(SeatMissionClassifier.ReadFacts(mission, logger));

            var serial = (++_missionSerial).ToString(System.Globalization.CultureInfo.InvariantCulture);
            _session = new SeatSession(new SeatScreenAdapter(MissionScreen, mission), IoC.Resolve<ISeatHeroAdapter>(),
                IoC.Resolve<ISeatSettingsProvider>(), Permissions, IoC.Resolve<IOutcomeLog>(), logger, serial);
            mission.OnMainAgentChanged += OnMainAgentChanged;
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
        if (_session == null || _keys == null)
            return;

        // The static test is cheap; the view's own context test allocates a click key array on each call (InputContext.cs:310-312) and
        // is the one that goes quiet behind a menu.
        var key = _keys.TakeTheSeat;
        _session.OnScreenTick(key != InputKey.Invalid && TaleWorlds.InputSystem.Input.IsKeyPressed(key) && Input.IsKeyPressed(key));
    }

    public override bool UpdateOverridenCamera(float dt) => _session != null && _session.OnFrame(dt);

    public override void OnMissionTick(float dt)
    {
        base.OnMissionTick(dt);
        _session?.OnMissionTick();
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

    private void EndSession()
    {
        _session?.Finish();
        _session = null;
        if (Mission != null)
            Mission.OnMainAgentChanged -= OnMainAgentChanged;
    }

    private void OnMainAgentChanged(Agent oldAgent) => _session?.OnMainAgentChanged();
}
