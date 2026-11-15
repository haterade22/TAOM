using System.Collections.Generic;
using TAOM.Features.CheatMenu.Battle;

namespace TAOM.Adapters;

/// <summary>
/// Every engine call of the battle cheat menu (docs/features/cheat-menu.md, "Battle"), so <see cref="BattleCheatService"/> stays
/// testable. Reads <c>Mission.Current</c> on each call and keeps no agent, team or mission between calls. Main thread.
/// </summary>
public interface IBattleCheatAdapter
{
    BattleCheatState ReadState();

    /// <summary>Sets the main agent's health, and its mount's, to the limit. Returns the agents healed: 0 with no living main agent.</summary>
    int HealMainAgent();

    /// <summary>Every active soldier on the player's side (the player's team and its ally team) and its mount. Returns the agents healed.</summary>
    int HealPlayerSide();

    /// <summary>Panics every active enemy soldier that has a common AI component (vanilla <c>mission.flee_enemies</c>). Returns the count.</summary>
    int RoutEnemies();

    /// <summary>Kills every AI-controlled active agent of every player-enemy team (vanilla <c>mission.kill_all_enemies</c>). Returns the count.</summary>
    int KillEnemies();

    /// <summary>One entry per active soldier (mounts left out) of the player's side or of the enemy side.</summary>
    IReadOnlyList<BattleTroop> SideTroops(bool playerSide);

    /// <summary>Spawns <paramref name="count"/> of the troop through the <c>taom.spawn_troops</c> spawner. Returns the number spawned.</summary>
    int SpawnTroops(string troopId, int count, bool playerSide, int ringStart);

    /// <summary>God mode's targets: the living main agent and the living mount it rides now. Empty with no living main agent.</summary>
    IReadOnlyList<GodModeAgent> GodModeTargets();

    /// <summary>
    /// These agents read again, with their mortality now. One no longer active, or whose index now holds another agent (its
    /// <see cref="GodModeAgent.Identity"/> differs), is left out.
    /// </summary>
    IReadOnlyList<GodModeAgent> ReadAgents(IReadOnlyList<GodModeAgent> agents);

    /// <summary>Sets the active agent's mortality to Immortal (<paramref name="immortal"/>) or Mortal. A gone agent is skipped.</summary>
    void SetImmortal(int index, bool immortal);
}
