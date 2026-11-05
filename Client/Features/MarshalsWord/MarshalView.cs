using System;
using TaleWorlds.Core;
using TaleWorlds.Engine;
using TaleWorlds.InputSystem;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;
using TaleWorlds.MountAndBlade.View;
using TaleWorlds.MountAndBlade.View.MissionViews;
using TAOM.Adapters;
using TAOM.Client.Adapters;
using TAOM.Client.Features.SeatOfSeeing;
using TAOM.Core.Logging;
using TAOM.Features.CoopInterop;
using TAOM.Features.MarshalsWord;
using TAOM.Features.SeatOfSeeing.Domain;

namespace TAOM.Client.Features.MarshalsWord;

/// <summary>
/// Marshal's Word's entry point (docs/features/marshals-word.md): orders in turn, keep the array, the archers' word, the mark and the outlines, for
/// any mission with a screen and a player team. Built by the engine as a <c>[DefaultView]</c> (a view TAOM adds in OnMissionBehaviorInitialize is never registered and never
/// ticks, #970), so every field stays null-safe. It builds one <see cref="MarshalSession"/> in <c>OnMissionScreenInitialize</c> and never
/// again: a null session field is final, because the mission still calls <c>OnMissionTick</c> after <c>OnMissionScreenFinalize</c>
/// (MissionScreen.cs:2918-2922 only unregisters from the screen; Mission.cs:3751-3754 walks the behaviours), and <c>RemoveMissionBehavior</c>
/// leaves the view in the screen's list (Mission.cs:4708-4725). The keys are read in <c>OnMissionScreenTick</c> (once per real frame, never in
/// <c>OnMissionTick</c>, which fast time repeats), only while the session is live: the three held keys only with the order panel open, and the three
/// pressed keys and the show-indicators key on every live frame.
/// </summary>
[DefaultView]
public sealed class MarshalView : MissionView
{
    private static int _missionSerial;

    private MarshalSession? _session;
    private MarshalHotKeys? _keys;

    private volatile bool _trialQueue;
    private volatile bool _trialMark;
    private volatile bool _trialWidths;
    private volatile bool _trialFire;
    private volatile bool _trialWord;
    private volatile bool _trialHold;

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
            var coop = IoC.Resolve<ICoopSessionProvider>();
            var mission = Mission;
            var serial = (++_missionSerial).ToString(System.Globalization.CultureInfo.InvariantCulture);
            var outlines = new MarshalOutlineService<Agent>(IoC.Resolve<IMarshalOutlineAdapter<Agent>>());
            _session = new MarshalSession(IoC.Resolve<IMarshalOrderAdapter>(), IoC.Resolve<IMarshalFireAdapter>(), outlines, new MarshalScreenAdapter(mission),
                IoC.Resolve<IMarshalSettingsProvider>(), () => MayRun(mission, coop, logger), IoC.Resolve<IOutcomeLog>(), logger, serial);
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
        var fire = _trialFire || Pressed(_keys.FireMode);
        var word = _trialWord || Pressed(_keys.Word);
        var hold = _trialHold || Pressed(_keys.HoldLine);
        _trialFire = false;
        _trialWord = false;
        _trialHold = false;
        session.OnScreenTick(new MarshalScreenInput(open && (_trialQueue || Down(_keys.Queue)), open && (_trialWidths || Down(_keys.KeepWidths)), open,
            Time.ApplicationTime, open && (_trialMark || Down(_keys.Mark)), Input.IsGameKeyDown((int)GameKeyDefinition.ShowIndicators),
            fire, word, hold));
    }

    public override void OnMissionTick(float dt)
    {
        base.OnMissionTick(dt);
        _session?.OnMissionTick();
    }

    // Native chooses this callback's thread (mission-frame-threads-and-native-costs.md), so it only adds to counters, and only with a session.
    public override void OnAgentShootMissile(Agent shooterAgent, EquipmentIndex weaponIndex, Vec3 position, Vec3 velocity, Mat3 orientation,
        bool hasRigidBody, int forcedMissileIndex)
    {
        base.OnAgentShootMissile(shooterAgent, weaponIndex, position, velocity, orientation, hasRigidBody, forcedMissileIndex);
        MarshalCombatReader.OnShot(shooterAgent);
    }

    // Native chooses this callback's thread too, so the hit callback only adds to a counter, and only with a session.
    public override void OnAgentHit(Agent affectedAgent, Agent affectorAgent, in MissionWeapon affectorWeapon, in Blow blow, in AttackCollisionData attackCollisionData)
    {
        base.OnAgentHit(affectedAgent, affectorAgent, in affectorWeapon, in blow, in attackCollisionData);
        MarshalCombatReader.OnBlow(affectorAgent, affectedAgent, blow.IsMissile);
    }

    public override void OnMissionModeChange(MissionMode oldMissionMode, bool atStart)
    {
        base.OnMissionModeChange(oldMissionMode, atStart);
        _session?.OnModeChange();
    }

    // Two teardown paths: the end-mission path (MissionScreen.cs:2918-2922) and MissionState.OnFinalize, which removes the behaviours with
    // no EndMission. Finishing is terminal: a screen tick can still run after the second. Nothing in TAOM or the cached engine removes this view while the mission runs
    // (Mission.OnMissionStateFinalize at Mission.cs:2209-2224 is the one caller that walks every behaviour), so the finish owes the soldiers no restore.
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

    /// <summary>Field trial: holds the "Mark, do not move" key as if the player did (the panel must be open).</summary>
    internal void TrialHoldMark(bool held) => _trialMark = held;

    /// <summary>Field trial: stores a press of "Change the archers' fire", which the next screen tick takes as the key's own press is.</summary>
    internal void TrialPressFire() => _trialFire = true;

    /// <summary>Field trial: stores a press of "Give the word".</summary>
    internal void TrialPressWord() => _trialWord = true;

    /// <summary>Field trial: stores a press of "Hold the line".</summary>
    internal void TrialPressHold() => _trialHold = true;

    // The static test is cheap, and the view's own context test allocates a click key array on each call (InputContext.cs:310-312).
    private bool Pressed(InputKey key) => key != InputKey.Invalid && TaleWorlds.InputSystem.Input.IsKeyPressed(key) && Input.IsKeyPressed(key);

    private static bool Down(InputKey key) => key != InputKey.Invalid && TaleWorlds.InputSystem.Input.IsKeyDown(key);

    private static bool MayRun(Mission mission, ICoopSessionProvider coop, IModLogger logger)
    {
        var facts = SeatMissionClassifier.ReadFacts(mission, coop, logger);
        return SeatMissionPolicy.Decide(facts).Orders && !facts.NetworkSession;
    }

    private void EndSession()
    {
        _session?.Finish();
        _session = null;
    }
}
