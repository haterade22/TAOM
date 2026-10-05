using System;
using System.Collections.Generic;

namespace TAOM.Features.CreatureSiegeRole.Domain;

/// <summary>
/// What the two game models need to know about the running siege, immutable, published once per active mission by
/// <c>AfterStart</c> and cleared by token when the mission ends. The engine calls the models from its asynchronous AI thread
/// (<c>GetDetachmentCostMultiplierOfAgent</c>, from every detachment tick) and from whichever thread native picks for a hit
/// (<c>ApplyDamageScaling</c>), so a reader does one volatile read of <see cref="Current"/>, a reference compare and an array
/// index: no lock, no allocation, no native call. Writers are the main thread only and take a lock so a publish never
/// interleaves with a clear. Nothing here holds an agent.
///
/// The record names its mission by <see cref="MissionToken"/> and the models check it against the agent's own mission, so a
/// record a faulted behavior never cleared cannot apply to the next battle. The gate components are held as objects: the
/// damage hook compares the hit object's <c>DestructableComponent</c> to them by reference.
/// </summary>
public sealed class CreatureSiegeSnapshot
{
    private static readonly object WriteLock = new();
    private static volatile CreatureSiegeSnapshot? _current;

    private readonly bool[] _raceMask;
    private readonly object[] _gateComponents;

    public CreatureSiegeSnapshot(object missionToken, bool[] raceMask, float gateDamageMultiplier,
        IReadOnlyList<object?> gateComponents)
    {
        MissionToken = missionToken ?? throw new ArgumentNullException(nameof(missionToken));
        GateDamageMultiplier = gateDamageMultiplier;

        _raceMask = raceMask == null ? Array.Empty<bool>() : (bool[])raceMask.Clone();

        var gates = new List<object>(gateComponents?.Count ?? 0);
        if (gateComponents != null)
        {
            foreach (var gate in gateComponents)
            {
                if (gate != null)
                    gates.Add(gate);
            }
        }

        _gateComponents = gates.ToArray();
    }

    /// <summary>The published snapshot, or null. One volatile read: safe on any thread.</summary>
    public static CreatureSiegeSnapshot? Current => _current;

    /// <summary>The mission this snapshot belongs to (the <c>Mission</c>, as an object).</summary>
    public object MissionToken { get; }

    /// <summary>The validated gate damage multiplier, finite and in [1, 10].</summary>
    public float GateDamageMultiplier { get; }

    /// <summary>True when <paramref name="race"/> is a creature race. A race outside the mask, negative included, is not.</summary>
    public bool IsCreatureRace(int race) => (uint)race < (uint)_raceMask.Length && _raceMask[race];

    /// <summary>True when <paramref name="component"/> is one of the mission's gate components, by reference.</summary>
    public bool IsGateComponent(object? component)
    {
        if (component == null) return false;

        var gates = _gateComponents;
        for (var i = 0; i < gates.Length; i++)
        {
            if (ReferenceEquals(gates[i], component))
                return true;
        }

        return false;
    }

    /// <summary>Makes <paramref name="snapshot"/> the current one, replacing any other. Main thread.</summary>
    public static void Publish(CreatureSiegeSnapshot snapshot)
    {
        if (snapshot == null) throw new ArgumentNullException(nameof(snapshot));

        lock (WriteLock)
            _current = snapshot;
    }

    /// <summary>
    /// Clears the current snapshot when it belongs to <paramref name="token"/>, by reference. True when it did. A mission
    /// that never published, or an end that arrives twice (<c>OnEndMission</c> and <c>OnRemoveBehavior</c> both clear), leaves
    /// another mission's snapshot alone.
    /// </summary>
    public static bool ClearIf(object? token)
    {
        lock (WriteLock)
        {
            var current = _current;
            if (current == null || token == null || !ReferenceEquals(current.MissionToken, token))
                return false;

            _current = null;
            return true;
        }
    }

    /// <summary>Clears whatever is published. A new mission's <c>OnCreated</c> calls it: nothing from an earlier mission may survive.</summary>
    public static void Clear()
    {
        lock (WriteLock)
            _current = null;
    }
}
