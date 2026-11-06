using System.Collections.Generic;
using BehaviorTrees;
using TAOM.Features.AdvancedCombat;
using TAOM.Features.CoopInterop.Missions;
using TaleWorlds.MountAndBlade;

namespace TAOM.Features.DwarfAttacks.Hooks;

/// <summary>
/// The dwarves' attack trees for one mission, held and run here: every dwarf soldier's one agent-component slot holds
/// the race-ability tree (csharp-architecture.md item 3). <see cref="RunAll"/> applies the gates that component applies:
/// the handle is still its slot's occupant and active (#592), and in co-op only the owner's tree acts (#967), or a
/// puppet's tree would land a second blow. Main thread only: the mission tick and OnAgentBuild.
/// </summary>
internal sealed class DwarfTreeHost
{
    private readonly List<(Agent Agent, BehaviorTree Tree)> _trees = new();
    private readonly DwarfAttackService _service;
    private readonly DwarfAttacksSettingsProvider _settings;
    private readonly DwarfAttackChecks _checks;
    private int _built;

    internal DwarfTreeHost(DwarfAttackService service, DwarfAttacksSettingsProvider settings, DwarfAttackChecks checks)
    {
        _service = service;
        _settings = settings;
        _checks = checks;
    }

    internal int Count => _trees.Count;

    /// <summary>Builds a tree for a dwarf that has none. False for anyone else or a dwarf already known.</summary>
    internal bool TryAttach(Agent agent)
    {
        if (agent == null || !_service.IsDwarf(agent.Monster?.StringId)) return false;
        for (int i = 0; i < _trees.Count; i++)
            if (ReferenceEquals(_trees[i].Agent, agent)) return false;

        _trees.Add((agent, DwarfAttacksBehaviorTree.Build(agent, _built++, _service, _settings, _checks)));
        return true;
    }

    /// <summary>Runs every live dwarf's tree once; drops a dwarf whose slot is gone or who is no longer active.</summary>
    internal void RunAll()
    {
        // Backwards, so a finished dwarf is removed in place.
        for (int i = _trees.Count - 1; i >= 0; i--)
        {
            (Agent agent, BehaviorTree tree) = _trees[i];
            if (!AgentSlotIdentity.IsCurrentOccupant(agent) || !agent.IsActive())
            {
                _trees.RemoveAt(i);
                continue;
            }
            if (!CoopMissionOwnership.IsOwnedHere(agent)) continue;
            tree.RunTree();
        }
    }

    internal void Clear()
    {
        _trees.Clear();
        _built = 0;
    }
}
