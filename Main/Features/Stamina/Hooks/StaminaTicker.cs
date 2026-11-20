using System;
using System.Collections.Generic;
using System.Diagnostics;
using TAOM.Adapters;
using TAOM.Features.AdvancedCombat;
using TAOM.Features.CoopInterop.Missions;
using TAOM.Features.Stamina.Cheats;
using TAOM.Features.Stamina.Domain;
using TaleWorlds.Core;
using TaleWorlds.MountAndBlade;

namespace TAOM.Features.Stamina.Hooks;

/// <summary>
/// Stamina's mission tick (#1497). Each frame it admits new soldiers and reads every tracked soldier's two action
/// indices (pointer reads) to find the start of a swing, a shot, a throw, a jump, a kick or a bash. It takes a full
/// look at the player's soldier every frame and at a share of the others, so each is seen once per sweep period:
/// that look runs the drain and regeneration, publishes the band, applies the Exhausted lock for the soldier's
/// controller, and queues a stat recompute when the band changed. Main thread. It holds engine reads and writes only:
/// every rule is in StaminaService, StaminaLockRules, StaminaRefreshQueue and StaminaInputRules, which have unit tests.
/// </summary>
public sealed class StaminaTicker
{
    private const float PulseSeconds = 30f;
    // An Exhausted soldier recovers in about 4 to 6 s; one still locked after this long means the rules trapped him.
    private const float StuckSeconds = 15f;

    private readonly StaminaRuntime _runtime;
    private readonly List<Agent> _due = new List<Agent>();
    private int _cursor;
    private float _owed;
    private float _nextPulse;
    private float _costScale = 1f;
    private bool _applyToPlayer = true;
    private bool _applyToAi = true;

    public StaminaTicker(StaminaRuntime runtime) => _runtime = runtime;

    public void Tick(float now, float dt)
    {
        MissionThreadGuard.NoteCall("StaminaTicker.Tick", _runtime.Warn);
        var store = _runtime.Store;
        var tuning = _runtime.Tuning;
        // Once per tick, never per soldier (config-providers.md rule 9).
        _costScale = _runtime.Settings.CostScale;
        _applyToPlayer = _runtime.Settings.ApplyToPlayer;
        _applyToAi = _runtime.Settings.ApplyToAi;
        store.Admit(now, IsLive);

        for (var i = 0; i < store.Count; i++)
        {
            var entry = store.At(i);
            if (entry.Value.Tracked && !entry.Value.Removed)
                Poll(entry.Key, entry.Value, now, tuning);
        }

        var main = Mission.Current?.MainAgent;
        if (main != null && store.TryGet(main, out var mainCell))
            Visit(main, mainCell, now, tuning);

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

            Visit(entry.Key, entry.Value, now, tuning);
            _cursor++;
        }

        Refresh(now, tuning);
        if (now >= _nextPulse)
        {
            _nextPulse = now + PulseSeconds;
            _runtime.Logger.LogInfo($"[Stamina][Pulse] t={now:0} {Counts()} {_runtime.Telemetry.Report()}");
        }
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
        _nextPulse = 0f;
        _costScale = 1f;
        _applyToPlayer = true;
        _applyToAi = true;
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
            // A soldier with no stamina (an exempt people, an MCM opt-out) reads Fresh whatever is forced: not counted.
            if (band != null && !entry.Value.Tracked)
                continue;
            before[(int)entry.Value.Band]++;
            entry.Value.Forced = band;
            changed++;
        }

        return $"{changed} soldier(s) set to {(band == null ? "auto (the rules decide)" : band.ToString())}. "
               + $"Before: fresh {before[0]}, winded {before[1]}, tired {before[2]}, exhausted {before[3]}. "
               + $"Each soldier changes within {_runtime.Tuning.SweepSeconds:0.##} s. A forced band neither drains nor comes back.";
    }

    /// <summary><c>taom.print_stamina</c> and the mission-end log line: the gate, the bands, the locks and the counters.</summary>
    public string Describe() =>
        $"Stamina: {(_runtime.Settings.Enabled ? "enabled" : "DISABLED in MCM")}; mission gate: {_runtime.MissionGate}; cost scale {_costScale:0.##}\n"
        + Counts() + $"; stat refreshes waiting: {_runtime.Refreshes.Count}\n"
        + _runtime.Telemetry.Report() + "\n"
        + _runtime.Telemetry.PeoplesReport();

    // The roster as counts: no agent is read, so this is safe at mission end.
    private string Counts()
    {
        var store = _runtime.Store;
        var bands = new int[4];
        var playerLocks = 0;
        var aiLocks = 0;
        var forced = 0;
        var tracked = 0;
        var fill = 0f;
        for (var i = 0; i < store.Count; i++)
        {
            var cell = store.At(i).Value;
            if (cell.Removed || !cell.Tracked)
                continue;
            tracked++;
            fill += cell.Fraction;
            bands[(int)cell.Band]++;
            if (cell.AppliedPath == StaminaLockPath.PlayerInput)
                playerLocks++;
            else if (cell.AppliedPath == StaminaLockPath.AiInput)
                aiLocks++;
            if (cell.Forced != null)
                forced++;
        }

        var average = tracked == 0 ? 1f : fill / tracked;
        return $"tracked={tracked} fresh={bands[0]} winded={bands[1]} tired={bands[2]} exh={bands[3]} avgFill={average:0.00} "
               + $"lockedPlayer={playerLocks} lockedAI={aiLocks} forced={forced}";
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

    // What a forced band shows as stamina.
    private static float FractionOfForced(StaminaBand band) =>
        band == StaminaBand.Fresh ? 1f : band == StaminaBand.Winded ? 0.5f : band == StaminaBand.Tired ? 0.2f : 0f;

    // Every frame, for a soldier the last look found alive and owned: two pointer reads (Agent.cs:2782-2785). Only
    // when an action index changed does it ask the engine for the action types, and charge the action that began.
    private void Poll(Agent agent, StaminaCell cell, float now, StaminaTuning tuning)
    {
        var action0 = agent.GetCurrentAction(0).Index;
        var action1 = agent.GetCurrentAction(1).Index;
        if (action0 == cell.LastAction0 && action1 == cell.LastAction1)
            return;
        cell.LastAction0 = action0;
        cell.LastAction1 = action1;
        if (!IsLive(agent))
            return;

        var type0 = (int)agent.GetCurrentActionType(0);
        var type1 = (int)agent.GetCurrentActionType(1);
        var usage = agent.WieldedWeapon.CurrentUsageItem;
        var kind0 = StaminaService.Classify(cell.Type0, type0, crossbow: false);
        var kind1 = StaminaService.Classify(cell.Type1, type1, usage != null && usage.WeaponClass == WeaponClass.Crossbow);
        // The held-draw cost is the player's alone: Marshal's Word holds AI archers at the draw on purpose.
        if (!StaminaService.IsDrawn(type1))
            cell.DrawSince = float.NaN;
        else if (!StaminaService.IsDrawn(cell.Type1))
            cell.DrawSince = !agent.IsAIControlled && usage != null && usage.WeaponClass == WeaponClass.Bow ? now : float.NaN;
        cell.Type0 = type0;
        cell.Type1 = type1;

        // Nothing is charged to a forced soldier, to one who works a siege engine, or for the horse's jump and a rider's kick.
        if (cell.Forced != null || agent.IsUsingGameObject)
            return;
        if (agent.HasMount)
        {
            kind0 = OnFootOnly(kind0);
            kind1 = OnFootOnly(kind1);
        }

        if (kind1 == StaminaAction.None && kind0 == StaminaAction.None)
            return;
        var economy = tuning.Economy;
        var weaponKg = agent.WieldedWeapon.Item?.Weight ?? 0f;
        if (kind1 != StaminaAction.None)
            Charge(cell, kind1, weaponKg, now, economy);
        // A whole-body action can show on both channels in the same frame: it is one action.
        if (kind0 != StaminaAction.None && kind0 != kind1)
            Charge(cell, kind0, weaponKg, now, economy);
        // At once, not at the next look: a swing that empties the pool locks the soldier now.
        Settle(agent, cell, now, tuning, owned: true);
    }

    private static StaminaAction OnFootOnly(StaminaAction kind) =>
        kind == StaminaAction.Jump || kind == StaminaAction.Kick ? StaminaAction.None : kind;

    private void Charge(StaminaCell cell, StaminaAction kind, float weaponKg, float now, StaminaEconomy economy)
    {
        var fought = cell.EverFought;
        StaminaService.Charge(cell, StaminaService.ActionCost(economy, kind, weaponKg), now, economy);
        _runtime.Telemetry.CountAction(kind);
        if (cell.EverFought && !fought)
            _runtime.Telemetry.CountFighter(cell.People);
    }

    private void Visit(Agent agent, StaminaCell cell, float now, StaminaTuning tuning)
    {
        if (cell.Removed || !IsLive(agent))
        {
            cell.Tracked = false;
            return;
        }

        // Co-op: a soldier another peer owns stays Fresh and unlocked here; his owner tires him (coop-interop.md).
        var owned = CoopMissionOwnership.IsOwnedHere(agent);
        if (owned && !cell.Enrolled)
            Enrol(agent, cell, tuning);
        // An exempt people (the Nazgul) has no stamina, and the player can exempt his own soldier or the AI's in MCM:
        // such a soldier is handled as one this peer does not tire. The player's soldier is the main agent (also while
        // Seat of Seeing has handed him to the AI) or one a player controls. An agent with no controller is nobody's.
        var subject = owned && !cell.Exempt
                      && StaminaService.Applies(agent.IsMainAgent || agent.IsMine, _applyToPlayer, _applyToAi);
        cell.Tracked = subject;
        if (subject && cell.Forced == null)
        {
            var economy = tuning.Economy;
            cell.CostFactor = _costScale * cell.OwnCostFactor;
            // The first look moves nothing; a long gap counts as two periods, so a hitch gives no burst.
            var dt = cell.LastVisitAt < 0f ? 0f : Math.Min(now - cell.LastVisitAt, 2f * tuning.SweepSeconds);
            var fought = cell.EverFought;
            // A soldier at a siege engine pays nothing, a blow he stopped there included.
            if (agent.IsUsingGameObject)
                StaminaService.Skip(cell);
            StaminaService.Step(cell, Sample(agent, cell, now, dt, economy), economy);
            if (cell.EverFought && !fought)
                _runtime.Telemetry.CountFighter(cell.People);
        }
        else
        {
            StaminaService.Skip(cell);
        }

        cell.LastVisitAt = now;
        Settle(agent, cell, now, tuning, subject);
    }

    // Once per soldier: his pool for his tier, rank and people, his people's regeneration and his armour's cost factor.
    private void Enrol(Agent agent, StaminaCell cell, StaminaTuning tuning)
    {
        var character = agent.Character;
        var hero = agent.IsHero;
        var race = _runtime.RaceNameOf(character?.Race);
        var profile = StaminaService.Profile(tuning.Economy, tuning.Peoples, character?.GetBattleTier() ?? 0, hero,
            hero ? character?.Level ?? 0 : 0, race, CharacterCulture.IdOf(character),
            agent.SpawnEquipment?.GetTotalWeightOfArmor(true) ?? 0f);
        cell.Enrolled = true;
        cell.People = race ?? "unknown";
        cell.Exempt = profile.Exempt;
        cell.Max = profile.Pool;
        cell.Current = profile.Pool;
        cell.RegenFactor = profile.RegenFactor;
        cell.OwnCostFactor = profile.CostFactor;
    }

    private static StaminaSample Sample(Agent agent, StaminaCell cell, float now, float dt, StaminaEconomy economy)
    {
        // Riding is free, a siege engine's crew pays nothing, and an Exhausted soldier's pace is not a run (StaminaService.Step).
        var running = cell.Band != StaminaBand.Exhausted && !agent.HasMount && !agent.IsUsingGameObject
                      && agent.MovementVelocity.LengthSquared > economy.RunSpeedThreshold * economy.RunSpeedThreshold;
        var held = 0f;
        if (!float.IsNaN(cell.DrawSince))
        {
            var past = now - cell.DrawSince - economy.BowHoldGraceSeconds;
            if (past > 0f)
                held = Math.Min(past, dt);
        }

        return new StaminaSample(now, dt, running, StaminaService.IsBlocking(cell.Type1), held);
    }

    // The band for the soldier's stamina now, published for the other threads; then the lock for his controller.
    private void Settle(Agent agent, StaminaCell cell, float now, StaminaTuning tuning, bool owned)
    {
        var before = cell.Band;
        StaminaBand band;
        float fraction;
        if (!owned)
        {
            band = StaminaBand.Fresh;
            fraction = 1f;
        }
        else if (cell.Forced != null)
        {
            band = cell.Forced.Value;
            fraction = FractionOfForced(band);
        }
        else
        {
            fraction = StaminaService.FractionOf(cell);
            band = StaminaService.NextBand(tuning.Economy, before, fraction);
        }

        // The lock counters take only a band the rules decided (StaminaLockRules.Tally).
        var byRules = owned && cell.Forced == null;
        var tally = StaminaLockRules.Tally(before, band, byRules, cell.BandByRules, cell.EntryCounted);
        cell.BandByRules = byRules;
        cell.EntryCounted = tally.EntryCounted;
        cell.Publish(band, fraction);
        if (tally.StartClock)
        {
            cell.ExhaustedSince = now;
            cell.StuckReported = false;
        }

        if (tally.Entered)
        {
            _runtime.Telemetry.CountExhausted(first: !cell.EverExhausted, cell.People);
            cell.EverExhausted = true;
        }

        if (tally.Recovered)
            _runtime.Telemetry.CountRecovered();

        if (band != before)
        {
            OnBandChanged(agent, cell, before, band);
            _runtime.Refreshes.Enqueue(agent, cell.BornAt, first: agent.IsMainAgent);
        }
        else if (band == StaminaBand.Exhausted && byRules && !tally.StartClock && !cell.StuckReported && now - cell.ExhaustedSince > StuckSeconds)
        {
            cell.StuckReported = true;
            _runtime.Telemetry.CountStuck();
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

    // The messages to the player. The counters are Settle's.
    private static void OnBandChanged(Agent agent, StaminaCell cell, StaminaBand before, StaminaBand band)
    {
        // The player's own soldier, and only while the player controls him.
        if (!agent.IsMainAgent || agent.IsAIControlled)
            return;
        if (band == StaminaBand.Exhausted)
        {
            StaminaTexts.ShowExhausted();
        }
        else if (band == StaminaBand.Tired && before != StaminaBand.Exhausted && !cell.TiredWarned)
        {
            cell.TiredWarned = true;
            StaminaTexts.ShowTired();
        }
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
