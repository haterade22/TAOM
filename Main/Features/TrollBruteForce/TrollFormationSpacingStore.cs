using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using TAOM.Core.Collections;

namespace TAOM.Features.TrollBruteForce;

/// <summary>
/// The troll width per formation: written on the main thread by <see cref="TrollFormationSpacingTracker"/>,
/// read from any thread by Patch92 (<c>Formation.UnitDiameter</c> runs on the AI thread and the TWParallel
/// workers too). Keyed by <c>object</c> so the store holds no engine type and its unit tests can use plain
/// objects. The reference comparer (<see cref="ReferenceIdentity"/>) is what makes a <c>Formation</c> key safe:
/// the order preview, deployment placement and spawn frames lay a formation out on a team-less copy
/// (<c>new Formation(null, -1)</c>), and <c>Formation.GetHashCode</c> is <c>Team.TeamIndex * 10 + FormationIndex</c>,
/// which throws on that copy; the comparer never calls it. A simulation copy is never itself
/// stored; only real formations are <see cref="Set"/>, and a copy's <see cref="TryGet"/> borrows the
/// width of whichever real formation <see cref="LayingOut"/> scopes on the calling thread for the
/// duration of the layout call (Patch92_TrollFormationSpacingSimulation's prefix/finalizer).
/// </summary>
internal static class TrollFormationSpacingStore
{
    [ThreadStatic] private static object? _layingOut;

    /// <summary>The real formation whose layout is being simulated on this thread, or null.</summary>
    internal static object? LayingOut
    {
        get => _layingOut;
        set => _layingOut = value;
    }

    private static readonly ConcurrentDictionary<object, float> Diameters = new(ReferenceIdentity.Instance);

    // the getter fires per unit per layout pass: skip the map while no formation holds trolls
    private static volatile bool _any;

    /// <summary>
    /// The width stored for <paramref name="formation"/>. When <paramref name="isSimulationCopy"/> is
    /// true (a team-less layout copy: the postfix passes <c>__instance.Team == null</c>), the copy is
    /// never itself a key; instead this returns the width of whichever real formation
    /// <see cref="LayingOut"/> currently scopes on this thread.
    /// </summary>
    internal static bool TryGet(object formation, bool isSimulationCopy, out float diameter)
    {
        diameter = 0f;
        if (!_any || formation == null) return false;
        if (!isSimulationCopy) return Diameters.TryGetValue(formation, out diameter);

        object? real = _layingOut;
        return real != null && Diameters.TryGetValue(real, out diameter);
    }

    /// <summary>Stores the width (null removes it). True when the formation's width changed.</summary>
    internal static bool Set(object formation, float? diameter)
    {
        if (diameter is float d)
        {
            // Lock-free unchanged check: skip the write (and the volatile _any store) when the
            // tracker's twice-a-second poll re-reports the same width.
            bool changed = !Diameters.TryGetValue(formation, out float old) || old != d;
            if (changed)
            {
                Diameters[formation] = d;
                _any = true;
            }
            return changed;
        }

        bool removed = Diameters.TryRemove(formation, out _);
        if (removed) _any = !Diameters.IsEmpty;   // recompute only on removal: an add already knows the map is non-empty
        return removed;
    }

    /// <summary>Every formation the store currently holds a width for (the tracker's gone-pass).</summary>
    internal static ICollection<object> Keys => Diameters.Keys;

    internal static void Clear()
    {
        Diameters.Clear();
        _any = false;
    }
}
