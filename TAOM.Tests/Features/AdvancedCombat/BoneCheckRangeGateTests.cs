using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TaleWorlds.Library;
using TAOM.Adapters;
using TAOM.Features.AdvancedCombat;

namespace TAOM.Tests.Features.AdvancedCombat;

/// <summary>
/// A warg's bite holds every agent captured within 20 m for the whole attack window, but only a
/// target whose visuals frame is inside the bone check's 20 square-metre gate (about 4.5 m) can be
/// hit. Fetching a target's skeleton builds a new native wrapper on every call, so the gate runs
/// first and a far target's skeleton is never fetched (plan 015). The attacker's bone math needs a
/// live skeleton, so these tests drive CheckTargets directly with no attacker bones. The substitute's
/// GetSkeleton returns null, so no native Skeleton is ever constructed here.
/// </summary>
[TestCategory("RequiresGame")]
[TestClass]
public class BoneCheckRangeGateTests
{
    // The warg's running bite: gate = max(20, 1 x 1 x 20) = 20 square metres.
    private const float WargRunningRadius = 1.0f;

    private sealed class Probe : BoneCheck
    {
        public Probe(List<IAgentAdapter> targets)
            : base(Substitute.For<IAgentAdapter>(), targets, new List<sbyte> { 17 }, 999f, WargRunningRadius, true, null, null) { }

        public List<IAgentAdapter> Targets => _targets;
    }

    private static readonly List<(sbyte, Vec3)> NoAttackerBones = new();

    private static MatrixFrame FrameAt(float x, float y, float z) => new MatrixFrame(Mat3.Identity, new Vec3(x, y, z));

    private static (IAgentAdapter target, IAgentVisualsAdapter visuals) LiveTargetAt(float x, float y, float z)
    {
        var visuals = Substitute.For<IAgentVisualsAdapter>();
        visuals.GetGlobalFrame().Returns(FrameAt(x, y, z));
        var target = Substitute.For<IAgentAdapter>();
        target.IsActive().Returns(true);
        target.IsFadingOut().Returns(false);
        target.AgentVisuals.Returns(visuals);
        return (target, visuals);
    }

    [TestMethod]
    public void CheckTargets_TargetBeyondTheRangeGate_NeverFetchesItsSkeleton()
    {
        var (target, visuals) = LiveTargetAt(10f, 0f, 0f); // 100 square metres; the gate is 20
        var sut = new Probe(new List<IAgentAdapter> { target });

        bool keepChecking = sut.CheckTargets(FrameAt(0f, 0f, 0f), NoAttackerBones);

        Assert.IsTrue(keepChecking);
        visuals.DidNotReceive().GetSkeleton();
    }

    [TestMethod]
    public void CheckTargets_TargetBeyondTheRangeGate_StaysATarget()
    {
        var (target, _) = LiveTargetAt(10f, 0f, 0f);
        var sut = new Probe(new List<IAgentAdapter> { target });

        sut.CheckTargets(FrameAt(0f, 0f, 0f), NoAttackerBones);

        CollectionAssert.AreEqual(new List<IAgentAdapter> { target }, sut.Targets,
            "a far target may close in on a later frame of the attack window");
    }

    [TestMethod]
    public void CheckTargets_TargetOnTheRangeGateEdge_FetchesItsSkeleton()
    {
        var (target, visuals) = LiveTargetAt(4f, 2f, 0f); // exactly 20 square metres: inside
        var sut = new Probe(new List<IAgentAdapter> { target });

        sut.CheckTargets(FrameAt(0f, 0f, 0f), NoAttackerBones);

        visuals.Received(1).GetSkeleton();
    }

    [TestMethod]
    public void CheckTargets_TargetInsideTheRangeGateWithNoSkeleton_IsDropped()
    {
        var (target, _) = LiveTargetAt(1f, 0f, 0f);
        var sut = new Probe(new List<IAgentAdapter> { target });

        bool keepChecking = sut.CheckTargets(FrameAt(0f, 0f, 0f), NoAttackerBones);

        Assert.IsTrue(keepChecking);
        Assert.AreEqual(0, sut.Targets.Count);
    }

    [TestMethod]
    public void CheckTargets_InactiveTarget_IsDroppedBeforeItsVisualsAreRead()
    {
        var target = Substitute.For<IAgentAdapter>();
        target.IsActive().Returns(false);
        var sut = new Probe(new List<IAgentAdapter> { target });

        sut.CheckTargets(FrameAt(0f, 0f, 0f), NoAttackerBones);

        Assert.AreEqual(0, sut.Targets.Count);
        _ = target.DidNotReceive().AgentVisuals;
    }

    [TestMethod]
    public void CheckTargets_NullTargetEntry_IsDropped()
    {
        var sut = new Probe(new List<IAgentAdapter> { null });

        bool keepChecking = sut.CheckTargets(FrameAt(0f, 0f, 0f), NoAttackerBones);

        Assert.IsTrue(keepChecking);
        Assert.AreEqual(0, sut.Targets.Count);
    }

    [TestMethod]
    public void CheckTargets_FadingOutTarget_IsDroppedBeforeItsVisualsAreRead()
    {
        var (target, _) = LiveTargetAt(1f, 0f, 0f);
        target.IsFadingOut().Returns(true);
        var sut = new Probe(new List<IAgentAdapter> { target });

        sut.CheckTargets(FrameAt(0f, 0f, 0f), NoAttackerBones);

        Assert.AreEqual(0, sut.Targets.Count);
        _ = target.DidNotReceive().AgentVisuals;
    }

    [TestMethod]
    public void CheckTargets_TargetWithNoVisuals_IsDropped()
    {
        var target = Substitute.For<IAgentAdapter>();
        target.IsActive().Returns(true);
        target.IsFadingOut().Returns(false);
        target.AgentVisuals.Returns((IAgentVisualsAdapter)null);
        var sut = new Probe(new List<IAgentAdapter> { target });

        sut.CheckTargets(FrameAt(0f, 0f, 0f), NoAttackerBones);

        Assert.AreEqual(0, sut.Targets.Count);
    }

    [TestMethod]
    public void CheckTargets_TargetWithANonFiniteFrame_FailsTheGateAndIsKept()
    {
        // NaN compares false both ways, so the gate must be a positive requirement (distance <= gate)
        // for a corrupt frame to fail it; an inverted `> gate` exit lets NaN reach GetSkeleton.
        var (target, visuals) = LiveTargetAt(float.NaN, 0f, 0f);
        var sut = new Probe(new List<IAgentAdapter> { target });

        bool keepChecking = sut.CheckTargets(FrameAt(0f, 0f, 0f), NoAttackerBones);

        Assert.IsTrue(keepChecking);
        visuals.DidNotReceive().GetSkeleton();
        CollectionAssert.AreEqual(new List<IAgentAdapter> { target }, sut.Targets);
    }

    [TestMethod]
    public void CheckTargets_MixedTargets_DropsAndKeepsEachWithoutSkippingTheNext()
    {
        // Removals step the index back while a far target is kept in place: a lost `i--` would skip
        // the entry after each removal, which a one-element list cannot show. Each removal below is
        // followed by an entry that must also be dropped, so a skipped entry stays in the list and
        // fails the assert; a skipped far target would look the same as a kept one, so each far
        // target's frame read is asserted as well.
        var (far, farVisuals) = LiveTargetAt(10f, 0f, 0f);
        var inactive = Substitute.For<IAgentAdapter>();
        inactive.IsActive().Returns(false);
        var noVisuals = Substitute.For<IAgentAdapter>();
        noVisuals.IsActive().Returns(true);
        noVisuals.IsFadingOut().Returns(false);
        noVisuals.AgentVisuals.Returns((IAgentVisualsAdapter)null);
        var (nearNoSkeleton, nearVisuals) = LiveTargetAt(1f, 0f, 0f);
        var (fadingOut, _) = LiveTargetAt(1f, 0f, 0f);
        fadingOut.IsFadingOut().Returns(true);
        var (far2, far2Visuals) = LiveTargetAt(0f, 10f, 0f);
        var sut = new Probe(new List<IAgentAdapter> { far, inactive, noVisuals, nearNoSkeleton, fadingOut, far2 });

        bool keepChecking = sut.CheckTargets(FrameAt(0f, 0f, 0f), NoAttackerBones);

        Assert.IsTrue(keepChecking);
        CollectionAssert.AreEqual(new List<IAgentAdapter> { far, far2 }, sut.Targets);
        nearVisuals.Received(1).GetSkeleton();
        farVisuals.Received(1).GetGlobalFrame();
        far2Visuals.Received(1).GetGlobalFrame();
        farVisuals.DidNotReceive().GetSkeleton();
        far2Visuals.DidNotReceive().GetSkeleton();
    }
}
