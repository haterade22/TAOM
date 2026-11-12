namespace TAOM.Adapters;

/// <summary>
/// Vanilla's siege-retreat branch (<c>SiegeAssaultEventComponent.OnPlayerEncounterContinueBattle</c>) commits gold,
/// renown, influence and morale but never reaches <c>MapEvent.CalculateAndCommitMapEventResults</c>, so a defender
/// gets no loot from the attackers who fell (#1121). This adapter runs vanilla's own casualty-loot step for that case
/// and opens the loot screen.
/// </summary>
public interface ISiegeRetreatLootAdapter
{
    /// <summary>
    /// True when the player defends a siege event, the player's map event already stands at <c>DefenderVictory</c>
    /// (the mission end sets it through <c>PlayerEncounter.SetPlayerVictorious</c>) and the component allows casualty
    /// loot. A stale <c>EnemyRetreated</c> result on a fresh assault leaves no victory, so it answers false. Never throws.
    /// </summary>
    bool CanLootCasualties();

    /// <summary>
    /// Runs the private <c>MapEvent.LootDefeatedPartyCasualties</c> step. False when the step is missing (an engine
    /// change). A step that throws is logged and still answers true: it may have moved items before the throw, and
    /// the screen shows them.
    /// </summary>
    bool RunCasualtyLoot();

    /// <summary>
    /// Opens the inventory loot screen with a copy of the pending items and clears the encounter's roster, because
    /// the siege encounter is reused and would show stale loot after the next assault. Returns the item units shown,
    /// 0 when nothing is pending, and -1 when the screen could not open (it is logged). Never throws.
    /// </summary>
    int OpenLootScreen();
}
