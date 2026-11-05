using System;
using TAOM.Features.LongDefeat.Domain;
using TAOM.Features.RaceAbilities;
using TAOM.Features.RaceAbilities.Hooks;
using TaleWorlds.Core;
using TaleWorlds.MountAndBlade;

namespace TAOM.Features.LongDefeat.Hooks;

/// <summary>
/// The stat powers and <c>hide</c> of a wave's affixes (phase2-plan.md section 4, "Hooks and models"), applied from the Custom Battle stat
/// model. <see cref="UpdateStats"/> runs after the race abilities' post-pass on every <c>UpdateAgentStats</c>, on any thread the engine uses, so it
/// reads one volatile table and compares the agent's mission with the armed one before it touches anything else; the record is the same
/// <c>RaceAbilityEffects</c> the abilities use and goes through the same applier, so a percentage multiplies what the engine just derived and never
/// compounds (<see cref="RaceAbilityStatApplier"/>). A horse takes its rider's record (a riderless one, whose own Character is null, takes nothing).
/// <see cref="InitializeArmour"/> runs after <c>InitializeAgentStats</c>, which writes armour once, at spawn
/// (<c>CustomBattleAgentStatCalculateModel.cs:25-28</c>) and which <c>UpdateAgentStats</c> never touches, so armour is raised only for a soldier
/// built after the wave's table was published. Armed from the mission logic's <c>AfterStart</c> and disarmed on both exits, like <see cref="LongDefeatMorale"/>.
/// </summary>
internal static class LongDefeatStatHooks
{
    private static volatile Mission? _armed;
    private static volatile LdStatTable? _table;

    // Set only while the effects adapter re-derives a baseline for a read-back, on that one thread, so the engine's own threads still see the table.
    [ThreadStatic]
    private static bool _baseline;

    internal static void Arm(Mission? mission)
    {
        _table = null;
        _armed = mission;
    }

    internal static void Disarm(Mission? mission)
    {
        if (mission == null || !ReferenceEquals(_armed, mission))
            return;

        _armed = null;
        _table = null;
    }

    /// <summary>Takes the wave's table; ignored from any mission but the armed one. The table must never be changed after this call.</summary>
    internal static void Publish(Mission? mission, LdStatTable table)
    {
        if (mission == null || !ReferenceEquals(_armed, mission))
            return;

        _table = table;
    }

    /// <summary>Runs <paramref name="derive"/> with the stat hook inert on this thread, so a baseline is what the model gives with no affix applied.</summary>
    internal static void Baseline(Action derive)
    {
        _baseline = true;
        try
        {
            derive();
        }
        finally
        {
            _baseline = false;
        }
    }

    /// <summary>The affix stat record of an attacker of the armed mission, written onto <paramref name="props"/>. Anything else passes through.</summary>
    internal static void UpdateStats(Agent agent, AgentDrivenProperties props)
    {
        var armed = _armed;
        var fx = _table?.Attackers;
        if (armed == null || fx == null || _baseline || !ReferenceEquals(agent.Mission, armed))
            return;

        var mount = agent.IsMount;
        var body = mount ? agent.RiderAgent : agent;
        if (body == null || body.Team?.Side != BattleSideEnum.Attacker)
            return;

        if (mount)
            RaceAbilityStatApplier.ApplyMount(props, fx);
        else
            RaceAbilityStatApplier.Apply(props, fx);
    }

    /// <summary>
    /// The four armour values of a human attacker built after the wave's <c>hide</c> was published, raised by its percent. The model is global to every
    /// Custom Battle, and <c>SpawnMonster</c> passes null build data, so null build data and anything that is not a human of the armed mission's attackers
    /// return at once. A horse is built with its rider's build data, so it is excluded by <c>IsHuman</c>.
    /// </summary>
    internal static void InitializeArmour(Agent agent, AgentDrivenProperties props, AgentBuildData? buildData)
    {
        var armed = _armed;
        var percent = _table?.AttackerArmourPercent ?? 0f;
        if (buildData == null || armed == null || percent == 0f || !ReferenceEquals(agent.Mission, armed))
            return;

        if (buildData.AgentTeam?.Side != BattleSideEnum.Attacker || !agent.IsHuman)
            return;

        props.ArmorHead = RaceAbilityService.Times(props.ArmorHead, percent);
        props.ArmorTorso = RaceAbilityService.Times(props.ArmorTorso, percent);
        props.ArmorLegs = RaceAbilityService.Times(props.ArmorLegs, percent);
        props.ArmorArms = RaceAbilityService.Times(props.ArmorArms, percent);
    }
}
