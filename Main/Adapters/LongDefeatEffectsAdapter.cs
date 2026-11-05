using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using TAOM.Features.LongDefeat.Domain;
using TAOM.Features.LongDefeat.Hooks;
using TAOM.Features.RaceAbilities.Hooks;
using TaleWorlds.Core;
using TaleWorlds.MountAndBlade;

namespace TAOM.Adapters;

/// <summary>The Long Defeat mission's engine writes and reads beyond the field (#1079). Main thread.</summary>
internal sealed class LongDefeatEffectsAdapter : ILongDefeatEffectsAdapter
{
    private readonly Mission _mission;

    public LongDefeatEffectsAdapter(Mission mission) => _mission = mission;

    public void PublishStats(LdStatTable table)
    {
        LongDefeatCombatHooks.Publish(_mission, table.AttackerDamage);
        LongDefeatCombatHooks.PublishTargets(_mission, table);
        LongDefeatStatHooks.Publish(_mission, table);
    }

    // BaseHealthLimit is MaxHitPoints() until something writes it (Agent.cs:1438-1439), so the limit over MaxHitPoints() is the factor SpawnBatch wrote.
    public LdScaleSample ReadScale()
    {
        var blows = LongDefeatCombatHooks.Raised;
        IEnumerable<Agent>? standing = _mission.AttackerTeam?.ActiveAgents;
        if (standing == null)
            return new LdScaleSample("", 0f, 0f, blows);

        foreach (var agent in standing)
        {
            var character = agent.Character;
            if (agent.IsHuman && agent.IsActive() && !agent.IsRunningAway && character != null)
                return new LdScaleSample(character.StringId, agent.HealthLimit, character.MaxHitPoints(), blows);
        }

        return new LdScaleSample("", 0f, 0f, blows);
    }

    // The baseline is the stat model's own answer for the same agent with the stat hook inert on this thread. The model reads the agent, and it also reads one
    // value from the property object it fills: "float num2 = agentDrivenProperties.ArmorEncumbrance + num;" (CustomBattleAgentStatCalculateModel.cs:190), which
    // only InitializeAgentStats writes (:22). The scratch therefore starts with the agent's own ArmorEncumbrance, or the drums baselines would be those of an
    // unarmoured soldier. The agent is refreshed first, as the engine does when a weapon changes (OnWeaponUsageIndexChange, Agent.cs:2380-2383, calls
    // UpdateAgentProperties), so the value read and the baseline are both the model's answer for the agent as it stands now, not for the weapon it held when
    // it spawned. The four armour values are written once at spawn and not by UpdateAgentStats, so their baseline is what the spawn equipment sums to.
    public LdStatSample ReadStats(IReadOnlyList<string> stats)
    {
        var agent = FirstPlainAttacker();
        return agent == null ? LdStatSample.None : new LdStatSample(agent.Character.StringId, Readings(agent, stats));
    }

    // The seed below is read by a source pin: the baseline starts from the agent's own encumbrance, then the stat model runs once with the hooks inert on this thread.
    private static List<LdStatReading> Readings(Agent agent, IReadOnlyList<string> stats)
    {
        agent.UpdateAgentProperties();
        var scratch = new AgentDrivenProperties { ArmorEncumbrance = agent.AgentDrivenProperties.ArmorEncumbrance };
        var model = MissionGameModels.Current.AgentStatCalculateModel;
        LongDefeatStatHooks.Baseline(() => model.UpdateAgentStats(agent, scratch));
        var readings = new List<LdStatReading>();
        foreach (var name in stats)
        {
            if (name == nameof(DrivenProperty.MountSpeed))
            {
                // The horse's speed is the horse's own property, written through its rider's record; a soldier on foot has none to read.
                if (agent.MountAgent is { } mount)
                    readings.Add(MountReading(mount));
                continue;
            }

            if (Enum.TryParse(name, out DrivenProperty property))
                readings.Add(new LdStatReading(name, ArmourBase(agent, name) ?? scratch.GetStat(property), agent.GetAgentDrivenPropertyValue(property)));
        }

        return readings;
    }

    // The horse as the engine holds it, against the stat model's answer for the same horse with the hook inert. MountSpeed is assigned afresh on every UpdateHorseStats
    // ("agentDrivenProperties.MountSpeed = environmentSpeedFactor * 0.22f * (1f + num);", CustomBattleAgentStatCalculateModel.cs:391), so a scratch object needs no seed.
    private static LdStatReading MountReading(Agent mount)
    {
        mount.UpdateAgentProperties();
        var scratch = new AgentDrivenProperties();
        var model = MissionGameModels.Current.AgentStatCalculateModel;
        LongDefeatStatHooks.Baseline(() => model.UpdateAgentStats(mount, scratch));
        return new LdStatReading(nameof(DrivenProperty.MountSpeed), scratch.MountSpeed, mount.GetAgentDrivenPropertyValue(DrivenProperty.MountSpeed));
    }

    public LdTouched RefreshStats(BoonTarget target)
    {
        var watch = Stopwatch.StartNew();
        var standing = Standing(target);
        foreach (var agent in standing)
        {
            agent.UpdateAgentProperties();
            agent.MountAgent?.UpdateAgentProperties();
        }

        return new LdTouched(standing.Count, watch.Elapsed.TotalMilliseconds);
    }

    public LdTouched ScaleHealth(BoonTarget target, float factor)
    {
        var watch = Stopwatch.StartNew();
        var standing = Standing(target);
        foreach (var agent in standing)
            Rescale(agent, agent.BaseHealthLimit * factor);

        return new LdTouched(standing.Count, watch.Elapsed.TotalMilliseconds);
    }

    public void SetPlayerHealth(float factor)
    {
        var main = Standing(BoonTarget.Player).FirstOrDefault();
        if (main?.Character != null)
            Rescale(main, main.Character.MaxHitPoints() * factor);
    }

    public LdTargetSample ReadTarget(BoonTarget target, IReadOnlyList<string> stats, bool newest)
    {
        var plain = Standing(target).Where(a => !a.IsRunningAway && !RaceAbilityHooks.HasLiveAbility(a)).ToList();
        var agent = newest ? plain.LastOrDefault() : plain.FirstOrDefault();
        return agent == null
            ? LdTargetSample.None
            : new LdTargetSample(agent.Character.StringId, agent.HealthLimit, agent.Character.MaxHitPoints(), Readings(agent, stats));
    }

    // The standing humans of one group of the player's side, in spawn order (Team.cs:658 appends). The main agent belongs to the player target alone, as the hooks decide.
    private List<Agent> Standing(BoonTarget target)
    {
        IEnumerable<Agent>? defenders = _mission.DefenderTeam?.ActiveAgents;
        if (defenders == null)
            return new List<Agent>();

        return defenders.Where(a => a.IsHuman && a.IsActive() && LongDefeatTargets.Of(a, _mission) == target).ToList();
    }

    // A new limit on the three writes the engine's horse spawn makes (Mission.cs:4607-4609), the health following at the share it had.
    private static void Rescale(Agent agent, float limit)
    {
        var share = agent.HealthLimit > 0f ? agent.Health / agent.HealthLimit : 1f;
        agent.BaseHealthLimit = limit;
        agent.HealthLimit = limit;
        agent.Health = share * limit;
    }

    private Agent? FirstPlainAttacker()
    {
        IEnumerable<Agent>? standing = _mission.AttackerTeam?.ActiveAgents;
        if (standing == null)
            return null;

        foreach (var agent in standing)
        {
            if (agent.IsHuman && agent.IsActive() && !agent.IsRunningAway && agent.Character != null && !RaceAbilityHooks.HasLiveAbility(agent))
                return agent;
        }

        return null;
    }

    private static float? ArmourBase(Agent agent, string name)
    {
        switch (name)
        {
            case "ArmorHead":
                return agent.SpawnEquipment.GetHeadArmorSum();
            case "ArmorTorso":
                return agent.SpawnEquipment.GetHumanBodyArmorSum();
            case "ArmorLegs":
                return agent.SpawnEquipment.GetLegArmorSum();
            case "ArmorArms":
                return agent.SpawnEquipment.GetArmArmorSum();
            default:
                return null;
        }
    }
}
