using System;
using System.Collections.Generic;
using HarmonyLib;
using TaleWorlds.Core;
using TaleWorlds.Engine;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;

namespace TAOM.Features.MissionPerf.Hooks;

/// <summary>
/// The swap lists of the two attribution transpilers (Patch97_MissionAttribution.cs, applied with Patch97,
/// so only with the tick profiler on) and the site counts they found. Verified on the installed v1.5.3:
/// <c>Mission.SpawnAgent</c> holds exactly two <c>callvirt MissionBehavior::OnAgentBuild(Agent, Banner)</c>
/// (mount, then rider); <c>ManagedScriptHolder.TickComponents</c> holds exactly four
/// <c>call TWParallel::For(int32, int32, float32, ParallelForWithDtAuxPredicate, int32)</c> (three parallel
/// blocks, then the occasional one) and one <c>callvirt ScriptComponentBehavior::OnTick(float32)</c>.
/// <c>HitchProbeBindingTests</c> feeds the real IL through these lists.
/// </summary>
internal static class MissionAttributionInstaller
{
    internal static int SpawnSites;
    internal static int ScriptSites;

    /// <summary>The TickComponents sites expected: five with the script tick delegate bound, four without.</summary>
    internal static int ScriptExpected => MissionAttributionHooks.ScriptTickCall != null ? 5 : 4;

    /// <summary>Binds <c>ScriptComponentBehavior.OnTick</c> (protected internal virtual) to an open delegate,
    /// which dispatches to the override (<c>OpenDelegateDispatchTests</c>). Logs nothing; false when unbound.</summary>
    internal static bool BindScriptTickDelegate()
    {
        try
        {
            MissionAttributionHooks.ScriptTickCall = ProbeDelegates.BindOpenInstance<ScriptComponentBehavior>(
                AccessTools.Method(typeof(ScriptComponentBehavior), "OnTick", new[] { typeof(float) }));
        }
        catch (Exception)
        {
            MissionAttributionHooks.ScriptTickCall = null;
        }
        return MissionAttributionHooks.ScriptTickCall != null;
    }

    internal static IReadOnlyList<CallSwap> SpawnAgentSwaps()
    {
        var build = Helper(nameof(MissionAttributionHooks.TimedAgentBuild));
        return new[]
        {
            new CallSwap(AccessTools.Method(typeof(MissionBehavior), nameof(MissionBehavior.OnAgentBuild),
                new[] { typeof(Agent), typeof(Banner) }), new[] { build, build }),
        };
    }

    internal static IReadOnlyList<CallSwap> TickComponentsSwaps()
    {
        var parallel = Helper(nameof(MissionAttributionHooks.TimedParallelBlock));
        var swaps = new List<CallSwap>(2)
        {
            new CallSwap(AccessTools.Method(typeof(TWParallel), nameof(TWParallel.For), new[]
                {
                    typeof(int), typeof(int), typeof(float), typeof(TWParallel.ParallelForWithDtAuxPredicate), typeof(int),
                }),
                new[] { parallel, parallel, parallel, Helper(nameof(MissionAttributionHooks.TimedOccasionalBlock)) }),
        };
        if (MissionAttributionHooks.ScriptTickCall != null)
            swaps.Add(new CallSwap(AccessTools.Method(typeof(ScriptComponentBehavior), "OnTick", new[] { typeof(float) }),
                Helper(nameof(MissionAttributionHooks.TimedScriptTick))));
        return swaps;
    }

    /// <summary>Test-only: the site counts, the delegate and the helpers' fault latch back to their initial values.</summary>
    internal static void ResetForTests()
    {
        SpawnSites = 0;
        ScriptSites = 0;
        MissionAttributionHooks.ScriptTickCall = null;
        MissionAttributionHooks.ResetForTests();
    }

    private static System.Reflection.MethodInfo Helper(string name) => AccessTools.Method(typeof(MissionAttributionHooks), name);
}
