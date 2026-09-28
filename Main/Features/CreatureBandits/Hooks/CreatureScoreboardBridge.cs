using System.Collections.Concurrent;
using System.Collections.Generic;
using TAOM.Core.Collections;
using TaleWorlds.Core;
using TaleWorlds.MountAndBlade;

namespace TAOM.Features.CreatureBandits.Hooks;

/// <summary>
/// Puts creature bandits on the battle scoreboard (#692; Codex review 2026-09-28, F1). The engine's battle observer
/// reports humans only (v1.5.3 <c>BattleObserverMissionLogic.cs:35-76</c>): an ordinary mount counts through its rider,
/// but a creature bandit is the troop itself, so without this a brood shows no troops, casualties or kills on the
/// campaign's scoreboard and Custom Battle's alike. It makes vanilla's calls for a creature: one troop when it is built
/// (held until the scoreboard sets its observer, as vanilla holds humans), its casualty when it is removed, and the kill
/// credits vanilla's human-only gate drops (<see cref="CreatureBanditRules.ScoreboardBridgeCreditsKill"/>). Exactly
/// once: a creature is removed from the scoreboard only if it was added, and credited only while it has a row. The
/// observer's private death ratio, which only picks the victory cheer, is left alone. Boundary code over raw agents.
///
/// Threads: built and flushed on the main thread; a removal runs on whatever thread raised <c>OnAgentRemoved</c> (#634),
/// makes the same observer call vanilla makes from that callback, reads the cached observer logic and uses only the
/// concurrent set of added creatures (keyed by reference, never by the recycled agent index).
/// </summary>
internal sealed class CreatureScoreboardBridge
{
    private readonly List<Agent> _pending = new();
    private readonly ConcurrentDictionary<Agent, byte> _added = new(ReferenceIdentity.Instance);
    private BattleObserverMissionLogic? _logic;

    internal void OnBuilt(Mission mission, Agent creature)
    {
        var observer = Observer(mission);
        if (observer == null)
        {
            _pending.Add(creature);
            return;
        }
        AddTroop(observer, creature);
    }

    /// <summary>Reports the creatures built before the scoreboard set its observer; one already removed is never counted.</summary>
    internal void Flush(Mission mission)
    {
        if (_pending.Count == 0) return;
        var observer = Observer(mission);
        if (observer == null) return;
        foreach (Agent creature in _pending)
            if (creature.IsActive())
                AddTroop(observer, creature);
        _pending.Clear();
    }

    internal void OnRemoved(Agent affected, Agent? affector, AgentState state)
    {
        var observer = _logic?.BattleObserver;
        if (observer == null) return;

        bool victimCreature = CreatureBanditAgents.Is(affected);
        bool victimHasRow = !victimCreature && IsRow(affected);
        if (victimCreature && CreatureBanditRules.ScoreboardCasualty(state) is var (killed, wounded, routed)
            && _added.TryRemove(affected, out _))
        {
            victimHasRow = true;
            observer.TroopNumberChanged(affected.Team.Side, affected.Origin.BattleCombatant, affected.Character, -1,
                killed, wounded, routed);
        }

        // As vanilla, a kill is credited only inside the victim's own row (BattleObserverMissionLogic.cs:54-75).
        if (!victimHasRow || affector == null || !IsRow(affector)) return;
        bool affectorCreature = CreatureBanditAgents.Is(affector);
        if (affectorCreature && !_added.ContainsKey(affector)) return;   // no row to credit
        if (CreatureBanditRules.ScoreboardBridgeCreditsKill(affected.IsHuman, victimCreature, affector.IsHuman,
                affectorCreature, state))
            observer.TroopNumberChanged(affector.Team.Side, affector.Origin.BattleCombatant, affector.Character, killCount: 1);
    }

    internal void Clear()
    {
        _pending.Clear();
        _added.Clear();
        _logic = null;
    }

    private IBattleObserver? Observer(Mission mission)
    {
        _logic ??= mission.GetMissionBehavior<BattleObserverMissionLogic>();
        return _logic?.BattleObserver;
    }

    private void AddTroop(IBattleObserver observer, Agent creature)
    {
        if (IsRow(creature) && _added.TryAdd(creature, 0))
            observer.TroopNumberChanged(creature.Team.Side, creature.Origin.BattleCombatant, creature.Character, 1);
    }

    private static bool IsRow(Agent agent)
        => CreatureBanditRules.IsScoreboardRow(agent.Team != null && agent.Team != Team.Invalid, agent.Origin, agent.Character);
}
