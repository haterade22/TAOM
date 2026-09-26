using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using HarmonyLib;
using TaleWorlds.MountAndBlade;

namespace TAOM.Features.TrollBruteForce.Hooks;

/// <summary>
/// Spaces a troll formation for trolls. The engine spaces every foot unit for a 0.76 m human
/// (<c>Formation.UnitDiameter</c> is <c>BipedalRadius</c> x 2 whatever the Monster), so a line of trolls stood inside
/// each other. This postfix raises the width to the one <see cref="TrollFormationSpacingTracker"/> computed for the
/// formation; the line arrangement reads <c>owner.UnitDiameter</c> for every slot, file count and rank depth, and
/// MixedFormations reads it through <c>IFormationAdapter.UnitDiameter</c>.
/// The getter also runs on the asynchronous AI thread and the TWParallel workers
/// (<c>Formation.GetOrderPositionOfUnit</c>), so the postfix only reads a concurrent map; the tracker writes it on
/// the main thread.
/// </summary>
[HarmonyPatch(typeof(Formation), nameof(Formation.UnitDiameter), MethodType.Getter)]
[HarmonyPatchCategory("Patch92_TrollFormationSpacing")]
public static class Patch92_TrollFormationSpacing
{
    [HarmonyPostfix]
    public static void Postfix(Formation __instance, ref float __result)
    {
        // A throw here lands in the engine's order preview and layout code on any thread: keep vanilla instead.
        try
        {
            if (TrollFormationSpacingStore.TryGet(__instance, out float diameter) && diameter > __result)
                __result = diameter;
        }
        catch
        {
        }
    }
}

/// <summary>
/// The order preview, the deployment placement and the spawn frames lay a formation out on a team-less copy: the
/// real formation's <c>GetUnitPositionWithIndexAccordingToNewOrder</c> overloads and <c>GetUnitSpawnFrameWithIndex</c>
/// hand a <c>new Formation(null, -1)</c> to one private static layout (v1.5.3 <c>Formation.cs:1680-1698, 3097</c>).
/// The copy is in no store, so trolls were placed at human width and their capsules pushed them to touching (every
/// neighbour 1.6 m apart, 2026-09-25). This scope names the real formation for the duration of the call, on the
/// calling thread, so the copy takes its width.
/// </summary>
[HarmonyPatch]
[HarmonyPatchCategory("Patch92_TrollFormationSpacing")]
public static class Patch92_TrollFormationSpacingSimulation
{
    internal static IEnumerable<MethodBase> TargetMethods() =>
        typeof(Formation).GetMethods(BindingFlags.Instance | BindingFlags.Public)
            .Where(m => m.Name == nameof(Formation.GetUnitPositionWithIndexAccordingToNewOrder)
                || m.Name == nameof(Formation.GetUnitSpawnFrameWithIndex));

    [HarmonyPrefix]
    public static void Prefix(Formation __instance, out Formation? __state)
    {
        __state = TrollFormationSpacingStore.LayingOut;
        TrollFormationSpacingStore.LayingOut = __instance;
    }

    [HarmonyFinalizer]
    public static void Finalizer(Formation? __state) => TrollFormationSpacingStore.LayingOut = __state;
}

/// <summary>The troll width per formation: written on the main thread, read from any thread by Patch92.</summary>
internal static class TrollFormationSpacingStore
{
    [ThreadStatic] private static Formation? _layingOut;

    /// <summary>The real formation whose layout is being simulated on this thread, or null.</summary>
    internal static Formation? LayingOut
    {
        get => _layingOut;
        set => _layingOut = value;
    }

    // By reference: Formation.GetHashCode is Team.TeamIndex * 10 + FormationIndex, and the order preview's
    // simulation formations have no Team, so the default comparer threw a NullReferenceException (2026-09-25).
    private static readonly ConcurrentDictionary<Formation, float> Diameters = new(FormationIdentity.Instance);

    // the getter fires per unit per layout pass: skip the map while no formation holds trolls
    private static volatile bool _any;

    internal static bool TryGet(Formation formation, out float diameter)
    {
        diameter = 0f;
        if (!_any || formation == null) return false;
        if (Diameters.TryGetValue(formation, out diameter)) return true;
        // a team-less simulation copy laid out for a troll formation takes that formation's width
        Formation? real = _layingOut;
        return real != null && formation.Team == null && Diameters.TryGetValue(real, out diameter);
    }

    /// <summary>Stores the width (null removes it). True when the formation's width changed.</summary>
    internal static bool Set(Formation formation, float? diameter)
    {
        bool changed;
        if (diameter is float d)
        {
            changed = !Diameters.TryGetValue(formation, out float old) || old != d;
            Diameters[formation] = d;
        }
        else
            changed = Diameters.TryRemove(formation, out _);
        _any = !Diameters.IsEmpty;
        return changed;
    }

    internal static ICollection<Formation> Formations => Diameters.Keys;

    internal static void Clear()
    {
        Diameters.Clear();
        _any = false;
    }
}

/// <summary>Reference identity for Formation keys: its own GetHashCode dereferences Team, null on a simulation formation.</summary>
internal sealed class FormationIdentity : IEqualityComparer<Formation>
{
    internal static readonly FormationIdentity Instance = new();

    public bool Equals(Formation? x, Formation? y) => ReferenceEquals(x, y);

    public int GetHashCode(Formation obj) => RuntimeHelpers.GetHashCode(obj);
}
