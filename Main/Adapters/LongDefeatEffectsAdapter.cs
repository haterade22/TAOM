using System.Collections.Generic;
using TAOM.Features.LongDefeat.Domain;
using TAOM.Features.LongDefeat.Hooks;
using TaleWorlds.MountAndBlade;

namespace TAOM.Adapters;

/// <summary>The Long Defeat mission's engine writes and reads beyond the field (#1079). Main thread.</summary>
internal sealed class LongDefeatEffectsAdapter : ILongDefeatEffectsAdapter
{
    private readonly Mission _mission;

    public LongDefeatEffectsAdapter(Mission mission) => _mission = mission;

    public void PublishStats(LdStatTable table) => LongDefeatCombatHooks.Publish(_mission, table.AttackerDamage);

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
}
