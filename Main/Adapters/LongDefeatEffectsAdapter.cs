using System;
using System.Collections.Generic;
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
        if (agent == null)
            return LdStatSample.None;

        agent.UpdateAgentProperties();
        var scratch = new AgentDrivenProperties { ArmorEncumbrance = agent.AgentDrivenProperties.ArmorEncumbrance };
        var model = MissionGameModels.Current.AgentStatCalculateModel;
        LongDefeatStatHooks.Baseline(() => model.UpdateAgentStats(agent, scratch));
        var readings = new List<LdStatReading>();
        foreach (var name in stats)
        {
            if (Enum.TryParse(name, out DrivenProperty property))
                readings.Add(new LdStatReading(name, ArmourBase(agent, name) ?? scratch.GetStat(property), agent.GetAgentDrivenPropertyValue(property)));
        }

        return new LdStatSample(agent.Character.StringId, readings);
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
