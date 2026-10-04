using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using SandBox.View.Map;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.GameState;
using TAOM.Features.MissionPerf;

namespace TAOM.Features.MapPerf.Hooks;

/// <summary>
/// Patch101's targets and its one call-site swap, shared by the patch classes, the installer's patched check
/// and the binding tests. No static field initializer names an engine or module type, so the
/// reference-assembly unit step can load it for <c>TickEventSwapTests</c>.
/// </summary>
internal static class MapProfilerTargets
{
    private static readonly string[] PerFrameVirtuals = { "OnFrameTick", "OnMapScreenUpdate", "OnMenuModeTick", "OnIdleTick" };

    internal static IReadOnlyList<string> CoreTargetNames { get; } = new[]
    {
        "MapState.OnTick", "Campaign.RealTick", "Campaign.Tick", "CampaignEvents.Tick", "MapScreen.OnFrameTick", "SubModule.OnApplicationTick",
    };

    /// <summary>CampaignEvents.Tick's one <c>callvirt MbEvent&lt;float&gt;::Invoke(float)</c> becomes a static call
    /// of <see cref="MapFrameProfilerHooks.TimedTickEvent"/>.</summary>
    internal static IReadOnlyList<CallSwap> TickEventSwaps() => new[]
    {
        new CallSwap(AccessTools.Method(typeof(MbEvent<float>), nameof(MbEvent<float>.Invoke), new[] { typeof(float) }),
            AccessTools.Method(typeof(MapFrameProfilerHooks), nameof(MapFrameProfilerHooks.TimedTickEvent))),
    };

    /// <summary>The six core targets in <see cref="CoreTargetNames"/> order; an entry is null when it does not resolve.</summary>
    internal static IReadOnlyList<MethodBase?> CoreTargets() => new MethodBase?[]
    {
        AccessTools.Method(typeof(MapState), "OnTick", new[] { typeof(float) }),
        AccessTools.Method(typeof(Campaign), "RealTick", new[] { typeof(float) }),
        AccessTools.Method(typeof(Campaign), "Tick", Type.EmptyTypes),
        AccessTools.Method(typeof(CampaignEvents), nameof(CampaignEvents.Tick), new[] { typeof(float) }),
        AccessTools.Method(typeof(MapScreen), "OnFrameTick", new[] { typeof(float) }),
        AccessTools.Method(typeof(global::TAOM.SubModule), "OnApplicationTick", new[] { typeof(float) }),
    };

    /// <summary>The names of the core targets that do not resolve or that <paramref name="isPatched"/> rejects, in
    /// <see cref="CoreTargetNames"/> order: what the install line calls missing, and what the session hooks look
    /// for again at each session start, window start and measuring session end.</summary>
    internal static List<string> UnpatchedCore(Func<MethodBase, bool> isPatched)
    {
        var targets = CoreTargets();
        return CoreTargetNames.Where((_, i) => targets[i] == null || !isPatched(targets[i]!)).ToList();
    }

    /// <summary>Every TAOM <see cref="MapView"/> subclass's own override of a per-frame virtual, sorted by
    /// declaring type then method name. Never throws: returns what it found.</summary>
    internal static List<MethodBase> MapViewTargets()
    {
        var result = new List<MethodBase>();
        try
        {
            Type?[] types;
            try { types = typeof(MapProfilerTargets).Assembly.GetTypes(); }
            catch (ReflectionTypeLoadException ex) { types = ex.Types; }
            const BindingFlags Own = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;
            foreach (var type in types)
            {
                if (type == null || type.IsAbstract || !typeof(MapView).IsAssignableFrom(type))
                    continue;
                foreach (var name in PerFrameVirtuals)
                {
                    var method = type.GetMethod(name, Own, null, new[] { typeof(float) }, null);
                    if (method != null)
                        result.Add(method);
                }
            }
            result.Sort((a, b) =>
            {
                var byType = string.CompareOrdinal(a.DeclaringType?.FullName, b.DeclaringType?.FullName);
                return byType != 0 ? byType : string.CompareOrdinal(a.Name, b.Name);
            });
        }
        catch (Exception) { /* returns what it found */ }
        return result;
    }
}
