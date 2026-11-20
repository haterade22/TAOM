using System.Collections.Generic;
using System.Diagnostics;
using TAOM.Adapters;
using TAOM.Features.AdvancedCombat;
using TAOM.Features.CoopInterop.Missions;
using TAOM.Features.Stamina.Cheats;
using TAOM.Features.Stamina.Domain;
using TaleWorlds.MountAndBlade;

namespace TAOM.Features.Stamina.Hooks;

/// <summary>
/// Stamina's mission tick (#1497): admits new soldiers, looks at the player's soldier every frame and at a share of
/// the others so each is seen once per sweep period, publishes a changed band, applies the Exhausted lock for the
/// soldier's controller, and recomputes the stats of a few changed soldiers per frame. Main thread. It holds engine
/// reads and writes only: every decision is in StaminaLockRules, StaminaRefreshQueue and StaminaInputRules, which have
/// unit tests. In this phase a band comes only from the dev console (StaminaCell.Forced); the drain rules come next.
/// </summary>
public sealed class StaminaTicker
{
    private readonly StaminaRuntime _runtime;
    private readonly List<Agent> _due = new List<Agent>();
    private int _cursor;
    private float _owed;

    public StaminaTicker(StaminaRuntime runtime) => _runtime = runtime;

    public void Tick(float now, float dt)
    {
        MissionThreadGuard.NoteCall("StaminaTicker.Tick", _runtime.Warn);
        var store = _runtime.Store;
        var tuning = _runtime.Tuning;
        store.Admit(now, IsLive);

        var main = Mission.Current?.MainAgent;
        if (main != null && store.TryGet(main, out var mainCell))
            Visit(main, mainCell);

        // dt is 0 while the mission is paused: nothing is owed and nothing moves. A positive requirement, so a dt that
        // is not a number adds nothing; after a long frame one full pass is enough.
        if (dt > 0f)
            _owed += store.Count * dt / tuning.SweepSeconds;
        if (!(_owed < store.Count))
            _owed = store.Count;
        var visits = (int)_owed;
        _owed -= visits;
        for (var i = 0; i < visits && store.Count > 0; i++)
        {
            if (_cursor >= store.Count)
                _cursor = 0;
            var entry = store.At(_cursor);
            if (entry.Value.Removed)
            {
                Drop(entry.Key, entry.Value);
                store.RemoveAt(_cursor);
                continue;
            }

            Visit(entry.Key, entry.Value);
            _cursor++;
        }

        Refresh(now, tuning);
    }

    /// <summary>
    /// Mission end, before the state is cleared: drops every AI input callback claim stamina holds. It reads no agent
    /// and makes no engine call. When a behavior is removed the engine has already cleared every agent's native
    /// pointers (Mission.EndMissionInternal calls Agent.Clear on each, Mission.cs:4652-4659, and raises no
    /// OnAgentDeleted), so even Agent.IsActive would fault there, and a callback flag on an agent that leaves with
    /// the mission changes nothing. Marshal's Word's Drop follows the same rule.
    /// </summary>
    public void Teardown() => AiInputCallbackClaims.DropOwner(AiInputCallbackClaims.Stamina);

    public void Clear()
    {
        _due.Clear();
        _cursor = 0;
        _owed = 0f;
    }

    /// <summary>
    /// <c>taom.set_stamina</c>: forces a band on the owned soldiers of the target, or with a null band gives them back
    /// to the rules. Called from the console on the main thread. It only sets each cell's forced band; the next sweep
    /// makes every engine write, from the mission tick (lesson "Agent fields vanilla writes from the pre-mission tick").
    /// </summary>
    public string Force(StaminaBand? band, StaminaCheatTarget target)
    {
        if (!_runtime.Armed)
            return $"Stamina is not running in this mission. Mission gate: {_runtime.MissionGate}";
        var playerTeam = Mission.Current?.PlayerTeam;
        if (playerTeam == null && (target == StaminaCheatTarget.Allies || target == StaminaCheatTarget.Enemies))
            return "This mission has no player team; use the target \"all\".";

        var store = _runtime.Store;
        var before = new int[4];
        var changed = 0;
        for (var i = 0; i < store.Count; i++)
        {
            var entry = store.At(i);
            var agent = entry.Key;
            if (entry.Value.Removed || !IsLive(agent) || !CoopMissionOwnership.IsOwnedHere(agent) || !Matches(agent, target, playerTeam))
                continue;
            before[(int)entry.Value.Band]++;
            entry.Value.Forced = band;
            changed++;
        }

        return $"{changed} soldier(s) set to {(band == null ? "auto (the rules decide)" : band.ToString())}. "
               + $"Before: fresh {before[0]}, winded {before[1]}, tired {before[2]}, exhausted {before[3]}. "
               + $"Each soldier changes within {_runtime.Tuning.SweepSeconds:0.##} s.";
    }

    /// <summary><c>taom.print_stamina</c> and the mission-end log line: the gate, the bands, the locks and the counters.</summary>
    public string Describe()
    {
        var store = _runtime.Store;
        var bands = new int[4];
        var playerLocks = 0;
        var aiLocks = 0;
        var forced = 0;
        for (var i = 0; i < store.Count; i++)
        {
            var cell = store.At(i).Value;
            if (cell.Removed)
                continue;
            bands[(int)cell.Band]++;
            if (cell.AppliedPath == StaminaLockPath.PlayerInput)
                playerLocks++;
            else if (cell.AppliedPath == StaminaLockPath.AiInput)
                aiLocks++;
            if (cell.Forced != null)
                forced++;
        }

        return $"Stamina: {(_runtime.Settings.Enabled ? "enabled" : "DISABLED in MCM")}; mission gate: {_runtime.MissionGate}\n"
               + $"Soldiers tracked: {store.Count}; fresh {bands[0]}, winded {bands[1]}, tired {bands[2]}, exhausted {bands[3]}; forced by console: {forced}\n"
               + $"Locked now: player input {playerLocks}, AI input {aiLocks}; stat refreshes waiting: {_runtime.Refreshes.Count}\n"
               + _runtime.Telemetry.Report();
    }

    private static bool Matches(Agent agent, StaminaCheatTarget target, Team? playerTeam)
    {
        if (target == StaminaCheatTarget.All)
            return true;
        if (target == StaminaCheatTarget.Player)
            return agent.IsMainAgent;
        var team = agent.Team;
        if (team == null || playerTeam == null)
            return false;
        return target == StaminaCheatTarget.Enemies ? team.IsEnemyOf(playerTeam) : !team.IsEnemyOf(playerTeam);
    }

    // A dead agent's handle answers for the slot's next tenant (#592): the slot's owner first, then his state, and
    // both before any other read. Only inside a running mission: see Teardown.
    private static bool IsLive(Agent agent) => AgentSlotIdentity.IsCurrentOccupant(agent) && agent.IsActive();

    // What a forced band shows as stamina until the drain rules give a real number.
    private static float FractionOf(StaminaBand band) =>
        band == StaminaBand.Fresh ? 1f : band == StaminaBand.Winded ? 0.5f : band == StaminaBand.Tired ? 0.2f : 0f;

    private void Visit(Agent agent, StaminaCell cell)
    {
        if (cell.Removed || !IsLive(agent))
            return;
        // Co-op: a soldier another peer owns stays Fresh and unlocked here; his owner tires him (coop-interop.md).
        var owned = CoopMissionOwnership.IsOwnedHere(agent);
        var band = owned ? cell.Forced ?? StaminaBand.Fresh : StaminaBand.Fresh;
        if (band != cell.Band)
        {
            cell.Publish(band, FractionOf(band));
            _runtime.Refreshes.Enqueue(agent, cell.BornAt, first: agent.IsMainAgent);
        }

        var path = StaminaLockRules.PathFor(band == StaminaBand.Exhausted, agent.IsAIControlled, agent.IsUsingGameObject);
        var step = StaminaLockRules.Step(cell.AppliedPath, path);
        if (step.StopAiStrip)
            StopAiStrip(agent);
        if (path == StaminaLockPath.AiInput)
            HoldAiStrip(agent);
        if (step.StartAiStrip)
            AiInputCallbackClaims.Claim(agent, AiInputCallbackClaims.Stamina);
        cell.AppliedPath = path;
    }

    // On every look at a soldier on the AI path: the input component is on him, it is his last component, and its
    // strip is on. Last, because the engine calls the components in list order (Agent.cs:1660-1664) and another
    // feature may add its own after ours: Marshal's Word's hold adds an attack input when it takes a soldier.
    private void HoldAiStrip(Agent agent)
    {
        var component = agent.GetComponent<StaminaInputComponent>();
        var components = agent.Components;
        if (component == null || components[components.Count - 1] != component)
        {
            if (component != null)
                agent.RemoveComponent(component);
            component ??= new StaminaInputComponent(agent, _runtime.Telemetry);
            agent.AddComponent(component);
        }

        component.Set(deny: true, mounted: agent.HasMount, inWater: agent.IsInWater());
    }

    private static void StopAiStrip(Agent agent)
    {
        agent.GetComponent<StaminaInputComponent>()?.Set(deny: false, mounted: false, inWater: false);
        AiInputCallbackClaims.Release(agent, AiInputCallbackClaims.Stamina);
    }

    // The soldier left the mission: the claim goes with no engine call on his handle.
    private static void Drop(Agent agent, StaminaCell cell)
    {
        if (cell.AppliedPath == StaminaLockPath.AiInput)
            AiInputCallbackClaims.Forget(agent, AiInputCallbackClaims.Stamina);
        cell.AppliedPath = StaminaLockPath.None;
    }

    private void Refresh(float now, StaminaTuning tuning)
    {
        _runtime.Refreshes.Take(now, tuning.MaxRefreshesPerFrame, tuning.MinRefreshAgeSeconds, _due);
        if (_due.Count == 0)
            return;
        var started = Stopwatch.GetTimestamp();
        var refreshed = 0;
        foreach (var agent in _due)
        {
            if (!IsLive(agent))
                continue;
            // The whole stat model runs again and reads the published band (StaminaHooks.ApplyStats). The mount is
            // not refreshed: stamina changes nothing on a mount.
            agent.UpdateAgentProperties();
            refreshed++;
        }

        _runtime.Telemetry.NoteRefreshBatch(refreshed, Stopwatch.GetTimestamp() - started);
        _due.Clear();
    }
}
