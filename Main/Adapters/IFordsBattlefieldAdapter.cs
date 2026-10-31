using System.Collections.Generic;
using TAOM.Features.FordsOfWar.Domain;

namespace TAOM.Adapters;

/// <summary>What a spawn produced: the new unit's key, its level (prices a unit that no offer names) and whether it rides.</summary>
internal sealed record FordsSpawnedAgent(AgentKey Key, int Level, bool Mounted);

/// <summary>
/// The Fords of War mission's troops: teams, one formation per lane per side (lane index = formation class index), spawning,
/// measuring and ordering. Takes and returns domain types, so the director never sees an engine type. Engine calls that change
/// the battle (<see cref="Spawn"/>, the order methods) run on the main thread only; <see cref="TryGetKey"/> and
/// <see cref="TryRemoveUnit"/> are safe from the engine's callback threads.
/// </summary>
internal interface IFordsBattlefieldAdapter
{
    /// <summary>
    /// Finds the West team (the player's) and the East team (the player's enemy) and the formation of every lane on both.
    /// False when the mission has no such pair or more lanes than the four default formation classes.
    /// </summary>
    bool Prepare(int laneCount);

    void SetCorpseFadeOutSeconds(float seconds);

    /// <summary>
    /// Tells the mission its deployment is over, as vanilla does when no deployment controller exists: agents spawned after it
    /// get their stats and ammo initialised. Safe to call once, after the teams exist.
    /// </summary>
    void CompleteDeployment();

    /// <summary>Spawns one unit of <paramref name="troopId"/> into the side's formation for the lane. Null when the troop cannot be built.</summary>
    FordsSpawnedAgent? Spawn(string troopId, MatchSide side, int lane, Vec3f position, Vec3f facing);

    /// <summary>Every live unit this adapter spawned, with its place. Dead, fading and stale handles are left out.</summary>
    IReadOnlyList<FordsUnitSample> SampleUnits();

    /// <summary>Where a unit stands now; null when it is gone.</summary>
    Vec3f? PositionOf(AgentKey key);

    /// <summary>The key of an engine agent (passed as object so the hook seam names no engine type); false for a gunner or a stranger.</summary>
    bool TryGetKey(object? agent, out AgentKey key);

    /// <summary>
    /// The side of any engine agent's team, a tower gunner included; null for a stranger, an agent with no team or one on neither
    /// side. Safe from the engine's callback threads: it reads the agent's team and the mission's two teams, nothing it writes.
    /// </summary>
    MatchSide? SideOf(object? agent);

    /// <summary>
    /// A registered unit left the mission: returns its key and, when it was <paramref name="killed"/> by a registered unit, the
    /// killer's key, and forgets the unit. False (nothing changed) when <paramref name="affected"/> is not a registered unit.
    /// </summary>
    bool TryRemoveUnit(object affected, object? affector, bool killed, out AgentKey victim, out AgentKey? killer);

    /// <summary>
    /// Orders the lane's formation to walk to <paramref name="target"/>. False when the order was skipped (the target is off the
    /// navmesh), so the caller can try another target; true when it was given, and also when the lane has no formation, because
    /// another target would not help.
    /// </summary>
    bool Move(MatchSide side, int lane, Vec3f target);

    void Charge(MatchSide side, int lane);

    /// <summary>Orders the lane's formation to attack a tower entity. False when the tower has no live entity.</summary>
    bool AttackTower(MatchSide side, int lane, string towerId);

    /// <summary>The display name of an NPCCharacter; the id itself when it is unknown.</summary>
    string CharacterName(string characterId);

    /// <summary>Forgets every handle (mission end).</summary>
    void Clear();
}
