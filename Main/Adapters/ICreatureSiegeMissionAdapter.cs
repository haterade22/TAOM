using System;
using System.Collections.Generic;
using TAOM.Features.CreatureSiegeRole.Domain;

namespace TAOM.Adapters;

/// <summary>
/// The wall battle, as the creature siege role sees it (ADR-007: the service never holds a <c>Mission</c>, a gate or a siege
/// weapon). One adapter per mission, built by the mission behavior. Gates are read as <see cref="SiegeGateReading"/> facts and
/// referred to afterwards by their opaque <c>Handle</c>. Everything is read on the main thread, from the mission tick or
/// <c>AfterStart</c>.
/// </summary>
public interface ICreatureSiegeMissionAdapter
{
    /// <summary>The mission, as an object. The snapshot is keyed on it, by reference.</summary>
    object MissionToken { get; }

    /// <summary>The scene's name, for the log.</summary>
    string SceneName { get; }

    /// <summary>The mission clock in seconds. May not be finite if native state is corrupt.</summary>
    float Time { get; }

    /// <summary>The mission is a siege battle (the engine's team AI type is Siege).</summary>
    bool IsSiegeBattle { get; }

    /// <summary>The mission carries a sally-out controller: a sally-out or a relief force, which vanilla plays.</summary>
    bool HasSallyOutController { get; }

    /// <summary>This process is a network client or a replay: it does not run the AI.</summary>
    bool IsClientOrReplay { get; }

    /// <summary>The deployment phase is over, which is when scripted movement stops teleporting agents.</summary>
    bool IsDeploymentFinished { get; }

    /// <summary>
    /// Every castle gate tagged as the outer gate, the engine's own choice first, then the rest in scene order. Empty when
    /// the siege team AI has none. Read once, when the role activates.
    /// </summary>
    IReadOnlyList<SiegeGateReading> OuterGateCandidates();

    /// <summary>The same for the inner gate.</summary>
    IReadOnlyList<SiegeGateReading> InnerGateCandidates();

    /// <summary>A gate's state now. <paramref name="gate"/> is the <c>Handle</c> of a reading this adapter made.</summary>
    GateLiveState ReadGate(object gate);

    /// <summary>The battering ram's state now.</summary>
    RamReading ReadRam();

    /// <summary>The <c>OnWallNavMeshId</c> of every ladder that is not disabled. Read once, at the first armed pass.</summary>
    IReadOnlyList<int> ReadLadderWallIds();

    /// <summary>The navmesh ids of every siege tower that is not disabled. Read once, at the first armed pass.</summary>
    IReadOnlyList<TowerFaces> ReadTowers();

    /// <summary>
    /// The ground height at a point, or NaN when nothing solid lies under it (a missed ray, not the engine's 0 for a miss).
    /// <paramref name="heightHint"/> picks the level.
    /// </summary>
    float GroundHeight(float x, float y, float heightHint);

    /// <summary>
    /// One adapter for every live agent whose race <paramref name="isCreatureRace"/> accepts, on every side, built fresh
    /// from the mission's active agents. The returned list is the adapter's own buffer, valid until the next call: the
    /// caller walks it and keeps nothing.
    /// </summary>
    IReadOnlyList<ICreatureSiegeAgentAdapter> CollectCreatures(Func<int, bool> isCreatureRace);
}
