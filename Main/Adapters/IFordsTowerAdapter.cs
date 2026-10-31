using System;
using TAOM.Features.FordsOfWar;
using TAOM.Features.FordsOfWar.Diagnostics;
using TAOM.Features.FordsOfWar.Domain;

namespace TAOM.Adapters;

/// <summary>
/// The towers and keeps of a Fords of War mission: the destructible entities, their garrisons and their volleys. Hit points
/// come from the domain; the entity only mirrors them.
/// </summary>
internal interface IFordsTowerAdapter
{
    /// <summary>
    /// Puts a tower on the field: the scene's own marked entity when it has a destructible component, else an instance of the
    /// prefab at the slot, scaled. Sets its maximum and current hit points. False when no entity could be made.
    /// </summary>
    bool SpawnTower(TowerSlot slot, TowerPrefab prefab, int hitPoints);

    /// <summary>Adds hit points to a standing tower (the regeneration the rules computed).</summary>
    void Heal(string towerId, int amount);

    /// <summary>Called with the tower id when a tower entity's hit points reach zero. May run off the main thread.</summary>
    void SetDestroyedHandler(Action<string> onDestroyed);

    /// <summary>
    /// Spawns <paramref name="count"/> invulnerable archers of the slot's side behind the tower, held in place. Returns how many
    /// stand. Archers are not Fords units: they have no key, no formation and no bounty. Their quivers are topped up by the
    /// mission's <c>AmmoSupplyLogic</c> (AI agents of a listed side), so nothing here refills them.
    /// </summary>
    int SpawnGunners(TowerSlot slot, string troopId, int count);

    /// <summary>Removes the garrison of a fallen tower.</summary>
    void KillGunners(string towerId);

    /// <summary>
    /// Looses <paramref name="missiles"/> javelins from above the tower toward a ground point, each owned by a living gunner of
    /// that tower, at the javelin's own missile speed. Returns how many missiles were added: 0 when the tower has no living gunner,
    /// the javelin item is missing or the target position is not finite. A target out of reach is still aimed at 45 degrees and counts.
    /// </summary>
    int Fire(string towerId, Vec3f target, int missiles);

    /// <summary>The tower id of a destructible component (passed as object); null when it is not one of ours.</summary>
    string? TowerIdOf(object? component);

    /// <summary>
    /// The volley javelin as the engine resolved it (looking it up logs its numbers once); null when the item does not exist.
    /// </summary>
    FordsVolleyItemInfo? VolleyItemInfo();

    /// <summary>The entity's current hit points; null for an unknown tower.</summary>
    float? HitPoints(string towerId);

    /// <summary>Forgets every handle (mission end). The entities go with the scene.</summary>
    void Clear();
}
