using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TaleWorlds.Engine;
using TAOM.Adapters;
using TAOM.Features.AdvancedCombat;
using TAOM.Tests.Migration;

namespace TAOM.Tests.Features.AdvancedCombat;

/// <summary>
/// BoneCheckDuringAnimation.Tick runs every frame of a live bite (maintainer decision 2026-09-24,
/// #659). Driven with substitutes below: a wind-up frame before the hit window never touches the
/// attacker's visuals (every GetSkeleton call builds a new native wrapper), the window end expires
/// the bite without them, a missing skeleton or visuals inside the window expires it, and a NaN
/// progress keeps the check without a hit test. One IL rule covers the path no test can drive (a
/// live skeleton, which cannot be built in a test): the progress is read once per tick on every
/// path. A call scan sees calls, not branches, so it cannot say which branch a fetch sits in; the
/// behaviour tests pin that. The in-game warg Custom Battle still owes the real hit window.
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

    [TestMethod]
    public void Tick_EveryFrame_ReadsTheActionProgressOnce()
        => Assert.AreEqual(1, ProgressReads(CallsInTick(typeof(BoneCheckDuringAnimation))),
            "Tick reads GetCurrentActionProgress(0) more than once; read it into a local");

    // Control: the pre-decision shape, which the rule must reject, so a scanner that stops seeing
    // a call cannot turn it into a vacuous pass. Never executed.
    private sealed class TwoReads
    {
        public bool Tick(IAgentAdapter agent, float min)
        {
            if (agent.GetCurrentActionProgress(0) >= 1f)
                return false;
            return agent.GetCurrentActionProgress(0) >= min;
        }
    }

    [TestMethod]
    public void ProgressRule_TwoReads_IsFound()
        => Assert.AreEqual(2, ProgressReads(CallsInTick(typeof(TwoReads))));

    // Behaviour: Tick driven with substitutes. The bite's action is default(ActionIndexCache):
    // the struct is beforefieldinit and its != reads only the instance Index, so no test here runs
    // its engine-backed static constructor. The standing bite's window is [0.1, 0.5)
    // (WargAttackService). The attacker's visuals return no skeleton, so a fetch in the wrong place
    // shows up as an early expiry or a GetSkeleton call; a real Skeleton cannot be built in a test,
    // so the hit-window path with a live skeleton is left to the in-game bite.
    private const float StandingMin = 0.1f;
    private const float StandingMax = 0.5f;

    private sealed class Bite
    {
        public readonly IAgentAdapter Attacker = Substitute.For<IAgentAdapter>();
        public readonly IAgentVisualsAdapter Visuals = Substitute.For<IAgentVisualsAdapter>();
        public int Expirations;
        public readonly BoneCheckDuringAnimation Check;

        public Bite(float progress, bool hasVisuals = true)
        {
            Attacker.IsActive().Returns(true);
            Attacker.IsFadingOut().Returns(false);
            Attacker.GetCurrentActionProgress(0).Returns(progress);
            Attacker.AgentVisuals.Returns(hasVisuals ? Visuals : null);
            Visuals.GetSkeleton().Returns((Skeleton)null);
            Check = new BoneCheckDuringAnimation(default, Attacker,
                new List<IAgentAdapter> { Substitute.For<IAgentAdapter>() }, new List<sbyte> { 17 },
                StandingMin, StandingMax, 0.5f, true, null, () => Expirations++);
        }
    }

    [TestCategory("RequiresGame")]
    [TestMethod]
    public void Tick_WindUpBeforeTheHitWindow_KeepsTheCheckWithoutTouchingTheAttackerVisuals()
    {
        var bite = new Bite(progress: 0.05f);

        Assert.IsTrue(bite.Check.Tick(0.016f), "a wind-up frame must not end the bite");
        Assert.AreEqual(0, bite.Expirations);
        _ = bite.Attacker.DidNotReceive().AgentVisuals;
        bite.Visuals.DidNotReceive().GetSkeleton();
        bite.Attacker.Received(1).GetCurrentActionProgress(0);
    }

    [TestCategory("RequiresGame")]
    [TestMethod]
    public void Tick_ProgressAtTheWindowEnd_ExpiresWithoutTouchingTheAttackerVisuals()
    {
        var bite = new Bite(progress: StandingMax);

        Assert.IsFalse(bite.Check.Tick(0.016f));
        Assert.AreEqual(1, bite.Expirations);
        _ = bite.Attacker.DidNotReceive().AgentVisuals;
        bite.Attacker.Received(1).GetCurrentActionProgress(0);
    }

    [TestCategory("RequiresGame")]
    [TestMethod]
    public void Tick_InTheHitWindowWithNoAttackerSkeleton_Expires()
    {
        var bite = new Bite(progress: 0.3f);

        Assert.IsFalse(bite.Check.Tick(0.016f));
        Assert.AreEqual(1, bite.Expirations);
        bite.Visuals.Received(1).GetSkeleton();
        bite.Attacker.Received(1).GetCurrentActionProgress(0);
    }

    [TestCategory("RequiresGame")]
    [TestMethod]
    public void Tick_AtTheHitWindowStartWithNoAttackerVisuals_Expires()
    {
        var bite = new Bite(progress: StandingMin, hasVisuals: false);

        Assert.IsFalse(bite.Check.Tick(0.016f));
        Assert.AreEqual(1, bite.Expirations);
    }

    [TestCategory("RequiresGame")]
    [TestMethod]
    public void Tick_NaNProgress_KeepsTheCheckWithoutAHitTest()
    {
        // Parity with the code before #659: both bounds are >= tests, so NaN neither ends the bite
        // nor reaches the collision pass; the check lives until the action changes.
        var bite = new Bite(progress: float.NaN);

        Assert.IsTrue(bite.Check.Tick(0.016f));
        Assert.AreEqual(0, bite.Expirations);
        _ = bite.Attacker.DidNotReceive().AgentVisuals;
    }
}
