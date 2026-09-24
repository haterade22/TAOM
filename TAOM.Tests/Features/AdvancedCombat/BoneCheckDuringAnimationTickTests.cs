using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TaleWorlds.Engine;
using TAOM.Adapters;
using TAOM.Features.AdvancedCombat;
using TAOM.Tests.Migration;

namespace TAOM.Tests.Features.AdvancedCombat;

/// <summary>
/// BoneCheckDuringAnimation.Tick runs every frame of a live bite. Two rules, pinned in the IL
/// because no test can call Tick (the ActionIndexCache static constructor needs the engine):
/// the action's progress is read once per tick, and the attacker's skeleton (a new native wrapper
/// on every GetSkeleton call) is fetched only after the progress tests, so a wind-up frame before
/// the hit window never builds one (maintainer decision 2026-09-24, #659). The in-game warg Custom
/// Battle is the behavioural proof.
/// </summary>
[TestClass]
public class BoneCheckDuringAnimationTickTests
{
    private const BindingFlags Declared = BindingFlags.Public | BindingFlags.NonPublic |
        BindingFlags.Instance | BindingFlags.DeclaredOnly;

    private static List<MethodBase> CallsInTick(System.Type type)
    {
        MethodInfo tick = type.GetMethod("Tick", Declared);
        Assert.IsNotNull(tick, $"{type.Name}.Tick not found");
        byte[] il = tick.GetMethodBody()?.GetILAsByteArray();
        Assert.IsNotNull(il, $"{type.Name}.Tick has no readable body");
        return IlCallScanner.ExtractCalledMethods(tick, il).ToList();
    }

    private static List<int> PositionsOf(List<MethodBase> calls, System.Type declaringType, string name) =>
        calls.Select((m, i) => (m, i))
            .Where(p => p.m.DeclaringType == declaringType && p.m.Name == name)
            .Select(p => p.i)
            .ToList();

    private static int ProgressReads(List<MethodBase> calls) =>
        PositionsOf(calls, typeof(IAgentAdapter), nameof(IAgentAdapter.GetCurrentActionProgress)).Count;

    /// <summary>True when Tick fetches the skeleton, and every fetch comes after every progress read.</summary>
    private static bool FetchesSkeletonAfterProgress(List<MethodBase> calls)
    {
        List<int> skeletons = PositionsOf(calls, typeof(IAgentVisualsAdapter), nameof(IAgentVisualsAdapter.GetSkeleton));
        List<int> progress = PositionsOf(calls, typeof(IAgentAdapter), nameof(IAgentAdapter.GetCurrentActionProgress));
        return skeletons.Count > 0 && progress.Count > 0 && skeletons.Min() > progress.Max();
    }

    [TestMethod]
    public void Tick_EveryFrame_ReadsTheActionProgressOnce()
        => Assert.AreEqual(1, ProgressReads(CallsInTick(typeof(BoneCheckDuringAnimation))),
            "Tick reads GetCurrentActionProgress(0) more than once; read it into a local");

    [TestMethod]
    public void Tick_BeforeTheHitWindow_FetchesTheAttackerSkeletonOnlyAfterTheProgressTests()
        => Assert.IsTrue(FetchesSkeletonAfterProgress(CallsInTick(typeof(BoneCheckDuringAnimation))),
            "Tick fetches the attacker skeleton before (or without) the progress tests; fetch it only once the hit window is reached");

    // Control: the pre-decision shape, which both rules must reject, so a scanner that stops
    // seeing a call cannot turn them into vacuous passes. Never executed.
    private sealed class SkeletonFirstTwoReads
    {
        public bool Tick(IAgentAdapter agent, float min)
        {
            Skeleton skeleton = agent.AgentVisuals?.GetSkeleton();
            if (skeleton is null || agent.GetCurrentActionProgress(0) >= 1f)
                return false;
            return agent.GetCurrentActionProgress(0) >= min;
        }
    }

    [TestMethod]
    public void ProgressRule_TwoReads_IsFound()
        => Assert.AreEqual(2, ProgressReads(CallsInTick(typeof(SkeletonFirstTwoReads))));

    [TestMethod]
    public void OrderRule_SkeletonBeforeProgress_IsFound()
        => Assert.IsFalse(FetchesSkeletonAfterProgress(CallsInTick(typeof(SkeletonFirstTwoReads))));
}
