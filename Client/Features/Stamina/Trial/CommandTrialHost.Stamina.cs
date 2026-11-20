using System;
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.Core;
using TaleWorlds.MountAndBlade;
using TAOM.Features.CoopInterop.Missions;
using TAOM.Features.Stamina.Domain;
using TAOM.Features.Stamina.Hooks;

namespace TAOM.Client.Features.SeatOfSeeing.Trial;

/// <summary>The stamina steps' window onto the engine (#1506): the cells, the groups the steps judge, and what each soldier's two action channels show.</summary>
internal sealed partial class CommandTrialHost
{
    private List<Agent>[] _staminaGroups = Array.Empty<List<Agent>>();
    private List<int>[] _staminaTypes = Array.Empty<List<int>>();
    private long[] _staminaReleases = Array.Empty<long>();
    private long[] _staminaDefends = Array.Empty<long>();

    public StaminaCensus ReadStamina()
    {
        var runtime = StaminaHooks.Runtime;
        if (runtime == null)
            return new StaminaCensus(false, "no stamina runtime", 0, 0, false, 0, 0L, 0f);

        var store = runtime.Store;
        var tracked = 0;
        for (var i = 0; i < store.Count; i++)
        {
            var pair = store.At(i);
            var cell = pair.Value;
            if (!cell.Removed && cell.Tracked && IsLive(pair.Key))
                tracked++;
        }

        var soldiers = 0;
        foreach (var agent in _mission.AllAgents)
        {
            if (IsLive(agent) && agent.IsHuman && !agent.IsMount)
                soldiers++;
        }

        var main = _mission.MainAgent;
        var heroCell = main != null && store.TryGet(main, out var hero) && hero.Tracked && !hero.Removed;
        return new StaminaCensus(runtime.Armed, runtime.MissionGate, tracked, soldiers, heroCell, runtime.Refreshes.Count,
            runtime.Telemetry.AiStrips, runtime.Tuning.Economy.RunSpeedThreshold);
    }

    public int ChooseStaminaGroups(bool enemies)
    {
        var count = enemies ? 3 : 2;
        _staminaGroups = new List<Agent>[count];
        _staminaTypes = new List<int>[count];
        for (var group = 0; group < count; group++)
        {
            _staminaGroups[group] = new List<Agent>();
            _staminaTypes[group] = new List<int>();
        }

        _staminaReleases = new long[count];
        _staminaDefends = new long[count];
        var runtime = StaminaHooks.Runtime;
        var team = _mission.PlayerTeam;
        if (runtime == null || team == null)
            return 0;

        var dealt = 0;
        foreach (var agent in _mission.AllAgents)
        {
            if (!InStaminaGroups(agent, team, enemies, runtime))
                continue;
            var group = dealt % count;
            _staminaGroups[group].Add(agent);
            _staminaTypes[group].Add((int)agent.GetCurrentActionType(0));
            _staminaTypes[group].Add((int)agent.GetCurrentActionType(1));
            dealt++;
        }

        return _staminaGroups.Min(members => members.Count);
    }

    public StaminaGroupSample SampleStaminaGroup(int group)
    {
        var runtime = StaminaHooks.Runtime;
        if (runtime == null || group < 0 || group >= _staminaGroups.Length)
            return default;

        var members = _staminaGroups[group];
        var types = _staminaTypes[group];
        var threshold = runtime.Tuning.Economy.RunSpeedThreshold;
        var live = 0;
        var runners = 0;
        var locked = 0;
        var speedSum = 0f;
        var chanceSum = 0f;
        var defensivenessSum = 0f;
        for (var i = 0; i < members.Count; i++)
        {
            var agent = members[i];
            if (!IsLive(agent) || agent.IsRunningAway)
                continue;

            live++;
            var speed = agent.MovementVelocity.Length;
            speedSum += speed;
            if (speed > threshold)
                runners++;
            if (runtime.Store.TryGet(agent, out var cell) && cell.AppliedPath != StaminaLockPath.None)
                locked++;
            chanceSum += agent.AgentDrivenProperties.AIAttackOnDecideChance;
            defensivenessSum += agent.Defensiveness;

            var type0 = (int)agent.GetCurrentActionType(0);
            var type1 = (int)agent.GetCurrentActionType(1);
            if (StaminaTrialRules.IsRelease(types[2 * i], type0) || StaminaTrialRules.IsRelease(types[2 * i + 1], type1))
                _staminaReleases[group]++;
            if (StaminaTrialRules.IsDefendEntry(types[2 * i], type0) || StaminaTrialRules.IsDefendEntry(types[2 * i + 1], type1))
                _staminaDefends[group]++;
            types[2 * i] = type0;
            types[2 * i + 1] = type1;
        }

        return new StaminaGroupSample(live, runners, speedSum, locked, _staminaReleases[group], _staminaDefends[group], chanceSum, defensivenessSum);
    }

    public int ForceStaminaGroup(int group, StaminaBand band)
    {
        NoteWrite("CommandTrialHost.ForceStaminaGroup");
        var runtime = StaminaHooks.Runtime;
        if (runtime == null || group < 0 || group >= _staminaGroups.Length)
            return 0;

        var forced = 0;
        foreach (var agent in _staminaGroups[group])
        {
            if (!runtime.Store.TryGet(agent, out var cell))
                continue;
            if (cell.Removed || !IsLive(agent) || !CoopMissionOwnership.IsOwnedHere(agent) || !cell.Tracked)
                continue;

            cell.Forced = band;
            forced++;
        }

        return forced;
    }

    public int StaminaReleaseForced()
    {
        var runtime = StaminaHooks.Runtime;
        if (runtime == null)
            return 0;

        var store = runtime.Store;
        var released = 0;
        for (var i = 0; i < store.Count; i++)
        {
            var cell = store.At(i).Value;
            if (cell.Removed)
                continue;
            if (cell.Forced != null)
                released++;
            cell.Forced = null;
        }

        return released;
    }

    public int CountStaminaSoldiers(bool enemies)
    {
        var team = _mission.PlayerTeam;
        if (team == null)
            return 0;

        var count = 0;
        foreach (var agent in _mission.AllAgents)
        {
            if (!IsLive(agent) || !agent.IsHuman || agent.IsMount || agent.Team == null)
                continue;
            if (agent.Team.IsEnemyOf(team) == enemies)
                count++;
        }

        return count;
    }

    private bool InStaminaGroups(Agent agent, Team team, bool enemies, StaminaRuntime runtime)
    {
        if (!IsLive(agent) || !agent.IsHuman || agent.IsMount || agent.IsMainAgent || agent.HasMount || agent.IsRunningAway || agent.Team == null)
            return false;
        if (enemies ? !agent.Team.IsEnemyOf(team) : agent.Team != team || agent.Formation?.FormationIndex != FormationClass.Infantry)
            return false;
        return runtime.Store.TryGet(agent, out var cell) && cell.Tracked && !cell.Removed;
    }
}
