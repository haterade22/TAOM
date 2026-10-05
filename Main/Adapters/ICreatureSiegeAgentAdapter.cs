using TAOM.Features.CreatureSiegeRole.Domain;

namespace TAOM.Adapters;

/// <summary>
/// One creature agent, as the creature siege role sees it for one reconcile pass (ADR-007: the service never holds an
/// <c>Agent</c>). The mission adapter builds a fresh adapter for every creature on every pass from the live agent list, so
/// none is held across frames; the service keys its route records on <see cref="Identity"/> and never dereferences it. Every
/// member is read or called on the main thread, from the mission tick, never from the engine's AI thread.
/// </summary>
public interface ICreatureSiegeAgentAdapter
{
    /// <summary>
    /// The agent object, opaque. Reference identity is the key: an agent index is recycled by the engine, a managed
    /// <c>Agent</c> object is not, so a new tenant of a slot is a new key.
    /// </summary>
    object Identity { get; }

    /// <summary>The agent is AI-controlled on this peer: its controller is the AI and this process is not a network client.</summary>
    bool IsAIControlled { get; }

    /// <summary>The agent is running away or retreating.</summary>
    bool IsFleeing { get; }

    /// <summary>The side the agent's team fights on.</summary>
    SiegeSide Side { get; }

    /// <summary>Where the agent stands now.</summary>
    SiegePoint Position { get; }

    /// <summary>The navigation face id the agent stands on (a native read).</summary>
    int NavigationFaceId { get; }

    /// <summary>Vanilla's ladder queue holds the agent. A creature that is excluded from every ladder face should never be in one.</summary>
    bool IsInLadderQueue { get; }

    /// <summary>The formation the agent stands in, and whether the player commands it.</summary>
    SiegeFormationState Formation { get; }

    /// <summary>The engine shows a scripted position on the agent (its scripted flags include "go to position").</summary>
    bool HasScriptedPosition { get; }

    /// <summary>The engine shows an attack-entity target on the agent (its combat flags include "attack entity").</summary>
    bool IsAttackingEntity { get; }

    /// <summary>
    /// Excludes the agent from a navmesh face group for good: native appends the id to the agent's list and registers the
    /// whole ordered list as one of at most six clean sets per scene, so the caller adds ids in one fixed order and never
    /// removes one.
    /// </summary>
    void ExcludeFace(int faceGroupId);

    /// <summary>
    /// Whether the agent's position and <paramref name="target"/> are on the same navmesh island (a scene query). It says nothing
    /// about a gate being open: islands merge when ladders go up.
    /// </summary>
    bool PathExists(SiegePoint target);

    /// <summary>
    /// Scripts the agent to walk to <paramref name="point"/>, face (<paramref name="faceX"/>, <paramref name="faceY"/>), and
    /// attack <paramref name="gate"/> (a gate handle from <see cref="ICreatureSiegeMissionAdapter"/>): a scripted position with
    /// the consider-rotation flag, plus a scripted attack target. Native stores the scripted flags as the flag passed or'd with 5,
    /// so they are never exactly "go to position", whichever flag is passed, and a ladder queue drops the agent. Retargeting from the outer gate to the inner gate works.
    /// </summary>
    void Strike(SiegePoint point, float faceX, float faceY, object gate);

    /// <summary>Scripts the agent to walk to <paramref name="point"/> and face (<paramref name="faceX"/>, <paramref name="faceY"/>), free to fight what comes within reach.</summary>
    void Hold(SiegePoint point, float faceX, float faceY);

    /// <summary>Clears the agent's scripted attack target. Leaving a strike for any other role does it first.</summary>
    void ClearCombatTarget();

    /// <summary>Hands the agent back to vanilla's AI: clears the scripted position and the scripted combat target.</summary>
    void Release();
}
