using System;
using TaleWorlds.Core;
using TaleWorlds.Engine;
using TaleWorlds.InputSystem;
using TaleWorlds.MountAndBlade;
using TaleWorlds.MountAndBlade.View;
using TaleWorlds.MountAndBlade.View.MissionViews;
using TAOM.Adapters;
using TAOM.Client.Adapters;
using TAOM.Client.Features.SeatOfSeeing;
using TAOM.Core.Logging;
using TAOM.Features.MarshalsWord;
using TAOM.Features.SeatOfSeeing.Domain;

namespace TAOM.Client.Features.MarshalsWord;

/// <summary>
/// Marshal's Word's entry point (docs/features/marshals-word.md): orders in turn and keep the array, for any mission with a screen and a
/// player team. Built by the engine as a <c>[DefaultView]</c> (a view TAOM adds in OnMissionBehaviorInitialize is never registered and never
/// ticks, #970), so every field stays null-safe. It builds one <see cref="MarshalSession"/> in <c>OnMissionScreenInitialize</c> and never
/// again: a null session field is final, because the mission still calls <c>OnMissionTick</c> after <c>OnMissionScreenFinalize</c>
/// (MissionScreen.cs:4241-4245 only unregisters from the screen; Mission.cs:3751-3754 walks the behaviours), and <c>RemoveMissionBehavior</c>
/// leaves the view in the screen's list (Mission.cs:4708-4725). The keys are read in <c>OnMissionScreenTick</c> (once per real frame, never in
/// <c>OnMissionTick</c>, which fast time repeats), only while the session is live.
/// </summary>
[DefaultView]
public sealed class MarshalView : MissionView
{
    private static int _missionSerial;

    private MarshalSession? _session;
    private MarshalHotKeys? _keys;

    private volatile bool _trialQueue;
    private volatile bool _trialWidths;

    public override void OnMissionScreenInitialize()
    {
        base.OnMissionScreenInitialize();
        IModLogger? resolved = null;
        try
        {
            var logger = resolved = IoC.Resolve<IModLogger>();
            if (MissionScreen == null || Mission == null)
                return;

            _keys = IoC.Resolve<MarshalHotKeys>();
            var mission = Mission;
            var serial = (++_missionSerial).ToString(System.Globalization.CultureInfo.InvariantCulture);
            _session = new MarshalSession(IoC.Resolve<IMarshalOrderAdapter>(), new MarshalScreenAdapter(mission), IoC.Resolve<IMarshalSettingsProvider>(),
                () => MayRun(mission, logger), IoC.Resolve<IOutcomeLog>(), logger, serial);
        }
        catch (Exception ex)
        {
            _session = null;
            resolved?.LogError($"[MarshalsWord] Marshal's Word could not start for this mission: {ex}");
        }
    }

    public override void OnMissionScreenTick(float dt)
    {
        base.OnMissionScreenTick(dt);
        var session = _session;
        if (session == null || !session.IsLive || _keys == null)
            return;

        var open = Mission.IsOrderMenuOpen;
        session.OnScreenTick(new MarshalScreenInput(open && (_trialQueue || Down(_keys.Queue)), open && (_trialWidths || Down(_keys.KeepWidths)), open,
            Time.ApplicationTime));
    }

    public override void OnMissionTick(float dt)
    {
        base.OnMissionTick(dt);
        _session?.OnMissionTick();
    }

    public override void OnMissionModeChange(MissionMode oldMissionMode, bool atStart)
    {
        base.OnMissionModeChange(oldMissionMode, atStart);
        _session?.OnModeChange();
    }

    // Two teardown paths: the end-mission path (MissionScreen.cs:4241-4245) and MissionState.OnFinalize, which removes the behaviours with
    // no EndMission. Finishing is terminal: a screen tick can still run after the second.
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

    /// <summary>Field trial: the live session, so a step can read the queue; null before the mission screen starts and after a teardown.</summary>
    internal MarshalSession? Session => _session;

    /// <summary>Field trial: holds the "Give the order in turn" key as if the player did, for the next frames (the panel must be open).</summary>
    internal void TrialHoldQueue(bool held) => _trialQueue = held;

    /// <summary>Field trial: holds the "Keep the widths" key as if the player did.</summary>
    internal void TrialHoldWidths(bool held) => _trialWidths = held;

    private static bool Down(InputKey key) => key != InputKey.Invalid && TaleWorlds.InputSystem.Input.IsKeyDown(key);

    private static bool MayRun(Mission mission, IModLogger logger)
    {
        var facts = SeatMissionClassifier.ReadFacts(mission, logger);
        return SeatMissionPolicy.Decide(facts).Orders && !facts.NetworkSession;
    }

    private void EndSession()
    {
        _session?.Finish();
        _session = null;
    }
}
