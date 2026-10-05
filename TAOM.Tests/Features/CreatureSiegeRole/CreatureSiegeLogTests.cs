using System.Globalization;
using System.Linq;
using System.Threading;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.CreatureSiegeRole.Domain;
using static TAOM.Tests.Features.CreatureSiegeRole.CreatureSiegeFixtures;

namespace TAOM.Tests.Features.CreatureSiegeRole;

/// <summary>
/// The diagnostics Mike reads during the in-game spikes: one activation line per mission naming the scene, the gates, the
/// exclusion ids and the anchors; a role-change line; a gate-blow line; the reason a mission is inert. The text is pure, so
/// it is pinned here; the throttle that keeps the per-creature lines from flooding the log is pure too.
/// </summary>
[TestClass]
public class CreatureSiegeLogTests
{
    // --- the throttle ---------------------------------------------------------------------------------------------------

    [TestMethod]
    public void Throttle_AllowsABurst_ThenRefusesUntilTheBucketRefills()
    {
        var throttle = new SiegeLogThrottle(burst: 3, refillPerSecond: 1.0);

        Assert.IsTrue(throttle.TryAcquire(10.0, out _));
        Assert.IsTrue(throttle.TryAcquire(10.0, out _));
        Assert.IsTrue(throttle.TryAcquire(10.0, out _));
        Assert.IsFalse(throttle.TryAcquire(10.0, out _), "the fourth in the same instant");
        Assert.IsFalse(throttle.TryAcquire(10.5, out _), "half a token is not a token");
        Assert.IsTrue(throttle.TryAcquire(11.0, out _));
    }

    [TestMethod]
    public void Throttle_ReportsHowManyLinesItSwallowed_OnTheNextOneThatPassesAndThenForgetsThem()
    {
        var throttle = new SiegeLogThrottle(1, 1.0);
        Assert.IsTrue(throttle.TryAcquire(0.0, out var first));
        Assert.AreEqual(0, first);

        Assert.IsFalse(throttle.TryAcquire(0.1, out _));
        Assert.IsFalse(throttle.TryAcquire(0.2, out _));
        Assert.IsFalse(throttle.TryAcquire(0.3, out _));

        Assert.IsTrue(throttle.TryAcquire(1.5, out var swallowed));
        Assert.AreEqual(3, swallowed);
        Assert.IsTrue(throttle.TryAcquire(3.0, out var none));
        Assert.AreEqual(0, none);
    }

    [TestMethod]
    public void Throttle_AfterALongQuietSpell_NeverHoldsMoreThanTheBurst()
    {
        var throttle = new SiegeLogThrottle(2, 1.0);
        throttle.TryAcquire(0.0, out _);

        Assert.IsTrue(throttle.TryAcquire(10000.0, out _));
        Assert.IsTrue(throttle.TryAcquire(10000.0, out _));
        Assert.IsFalse(throttle.TryAcquire(10000.0, out _));
    }

    [TestMethod]
    public void Throttle_ANaNOrBackwardsClock_NeitherRefillsNorThrows()
    {
        var throttle = new SiegeLogThrottle(1, 1.0);
        Assert.IsTrue(throttle.TryAcquire(5.0, out _));

        Assert.IsFalse(throttle.TryAcquire(double.NaN, out _));
        Assert.IsFalse(throttle.TryAcquire(double.PositiveInfinity, out _));
        Assert.IsFalse(throttle.TryAcquire(1.0, out _), "time went backwards: no refill");
        Assert.IsTrue(throttle.TryAcquire(6.5, out _));
    }

    [TestMethod]
    public void Throttle_ANaNFirstClock_StillAllowsTheBurst()
    {
        var throttle = new SiegeLogThrottle(2, 1.0);

        Assert.IsTrue(throttle.TryAcquire(double.NaN, out _));
        Assert.IsTrue(throttle.TryAcquire(double.NaN, out _));
        Assert.IsFalse(throttle.TryAcquire(double.NaN, out _));
    }

    [TestMethod]
    public void Throttle_ADegenerateShape_StillAllowsOneLine()
    {
        var throttle = new SiegeLogThrottle(0, -5.0);

        Assert.IsTrue(throttle.TryAcquire(0.0, out _));
        Assert.IsFalse(throttle.TryAcquire(0.0, out _));
    }

    // --- the activation line --------------------------------------------------------------------------------------------

    private static string Activation(SiegeGateReading? inner = null, ExclusionPlan? plan = null,
        AnchorCandidate? hold = null, AnchorCandidate? courtyard = null, GateLiveState? outerState = null,
        GateLiveState? innerState = null) =>
        CreatureSiegeReport.Activation("taom_test_scene", Gate(name: "outer_gate_a"), outerState ?? new GateLiveState(false, false, 15000f),
            inner, innerState ?? new GateLiveState(false, false, 12000f), 2f,
            plan ?? new ExclusionPlan(List(1000052, 333), List<SkippedFace>(), List<int>()), hold, courtyard);

    [TestMethod]
    public void Activation_NamesTheSceneTheGatesAndTheirState()
    {
        var line = Activation(inner: Gate(name: "inner_gate_b"), outerState: new GateLiveState(false, false, 15000f),
            innerState: new GateLiveState(false, true, 12000f));

        StringAssert.Contains(line, "[CreatureSiegeRole]");
        StringAssert.Contains(line, "taom_test_scene");
        StringAssert.Contains(line, "outer_gate_a");
        StringAssert.Contains(line, "inner_gate_b");
        StringAssert.Contains(line, "closed");
        StringAssert.Contains(line, "open");
        StringAssert.Contains(line, "15000");
        StringAssert.Contains(line, "12000");
    }

    [TestMethod]
    public void Activation_AGateThatIsAlreadyDestroyed_SaysDestroyed()
    {
        StringAssert.Contains(Activation(outerState: new GateLiveState(true, false, 0f)), "destroyed");
    }

    [TestMethod]
    public void Activation_ASceneWithNoInnerGate_SaysSo()
    {
        StringAssert.Contains(Activation(inner: null), "no inner gate");
    }

    [TestMethod]
    public void Activation_ListsTheExclusionIdsInOrder()
    {
        var line = Activation(plan: new ExclusionPlan(List(1000052, 1000102, 333, 444), List<SkippedFace>(), List<int>()));

        StringAssert.Contains(line, "1000052, 1000102, 333, 444");
    }

    [TestMethod]
    public void Activation_NamesWhatWasSkippedAndWhatWasDropped()
    {
        var plan = new ExclusionPlan(List(1000052), List(new SkippedFace(ExclusionTier.TowerEntrance, 0)), List(555, 666));

        var line = Activation(plan: plan);

        StringAssert.Contains(line, "skipped");
        StringAssert.Contains(line, "TowerEntrance 0");
        StringAssert.Contains(line, "dropped");
        StringAssert.Contains(line, "555, 666");
    }

    [TestMethod]
    public void Activation_WithNothingSkippedOrDropped_SaysNone()
    {
        var line = Activation();

        StringAssert.Contains(line, "skipped none");
        StringAssert.Contains(line, "dropped none");
    }

    [TestMethod]
    public void Activation_NamesTheChosenAnchorsAndWhereTheyCameFrom()
    {
        var hold = new AnchorCandidate(AnchorSource.OuterMiddle, new SiegePoint(100f, 198f, 10f), true);
        var courtyard = new AnchorCandidate(AnchorSource.InnerMiddle, new SiegePoint(100f, 178f, 11.5f), true);

        var line = Activation(hold: hold, courtyard: courtyard);

        StringAssert.Contains(line, "OuterMiddle");
        StringAssert.Contains(line, "100.0, 198.0, 10.0");
        StringAssert.Contains(line, "InnerMiddle");
        StringAssert.Contains(line, "100.0, 178.0, 11.5");
    }

    [TestMethod]
    public void Activation_NamesEachGatesOrigin()
    {
        var line = CreatureSiegeReport.Activation("taom_test_scene", Gate(name: "outer_gate_a", x: 100f, y: 200f, z: 10f),
            new GateLiveState(false, false, 15000f), Gate(name: "inner_gate_b", x: 101.5f, y: 180f, z: 11.5f),
            new GateLiveState(false, false, 12000f), 2f, new ExclusionPlan(List(1), List<SkippedFace>(), List<int>()), null, null);

        StringAssert.Contains(line, "origin 100.0, 200.0, 10.0");
        StringAssert.Contains(line, "origin 101.5, 180.0, 11.5");
    }

    [TestMethod]
    public void Activation_NamesEachAnchorsDistanceFromTheOuterGate_WhateverGateItIsNamedAfter()
    {
        var outer = Gate(x: 100f, y: 200f);
        var inner = Gate(x: 100f, y: 180f);
        var hold = new AnchorCandidate(AnchorSource.OuterMiddle, new SiegePoint(103f, 196f, 10f), true);
        var courtyard = new AnchorCandidate(AnchorSource.InnerMiddle, new SiegePoint(100f, 175f, 10f), true);

        var line = CreatureSiegeReport.Activation("s", outer, new GateLiveState(), inner, new GateLiveState(), 2f,
            new ExclusionPlan(List(1), List<SkippedFace>(), List<int>()), hold, courtyard);

        StringAssert.Contains(line, "hold anchor OuterMiddle (103.0, 196.0, 10.0) 5.0 m from the outer gate");
        // 5 m from the inner gate (100, 180) but 25 m from the outer one: the check is that no anchor sits far from the doorway.
        StringAssert.Contains(line, "courtyard anchor InnerMiddle (100.0, 175.0, 10.0) 25.0 m from the outer gate");
    }

    [TestMethod]
    public void Activation_AnInsideAnchor_IsMeasuredFromTheOuterGate()
    {
        var hold = new AnchorCandidate(AnchorSource.InsideOuterGate, new SiegePoint(100f, 194f, 10f), true);

        var line = Activation(inner: Gate(y: 180f), hold: hold);

        StringAssert.Contains(line, "hold anchor InsideOuterGate (100.0, 194.0, 10.0) 6.0 m from the outer gate");
    }

    [TestMethod]
    public void Activation_AnAnchorWithNonFiniteInput_SaysUnknownDistance()
    {
        var courtyard = new AnchorCandidate(AnchorSource.InnerMiddle, new SiegePoint(100f, 178f, 10f), true);
        var nan = new AnchorCandidate(AnchorSource.OuterMiddle, new SiegePoint(float.NaN, 198f, 10f), false);

        var line = Activation(inner: null, hold: nan, courtyard: courtyard);

        StringAssert.Contains(line, "courtyard anchor InnerMiddle (100.0, 178.0, 10.0) 22.0 m from the outer gate");
        StringAssert.Contains(line, "hold anchor OuterMiddle (NaN, 198.0, 10.0) distance unknown");
    }

    [TestMethod]
    public void Activation_NoUsableAnchor_SaysNone()
    {
        var line = Activation(hold: null, courtyard: null);

        StringAssert.Contains(line, "hold anchor none");
        StringAssert.Contains(line, "courtyard anchor none");
    }

    [TestMethod]
    public void Activation_StatesTheGateDamageMultiplier()
    {
        StringAssert.Contains(Activation(), "gate damage x2.0");
    }

    [TestMethod]
    public void Activation_IsOneLine()
    {
        var line = Activation(inner: Gate(), plan: new ExclusionPlan(List(1, 2), List(new SkippedFace(ExclusionTier.Ladder, 0)), List(9)));

        Assert.IsFalse(line.Contains("\n") || line.Contains("\r"));
    }

    // --- the inert line -------------------------------------------------------------------------------------------------

    [DataTestMethod]
    [DataRow(ActivationVerdict.NotSiege, "not a siege battle")]
    [DataRow(ActivationVerdict.SallyOutOrRelief, "sally-out or relief")]
    [DataRow(ActivationVerdict.ClientOrReplay, "does not run the AI")]
    [DataRow(ActivationVerdict.Disabled, "setting is off")]
    [DataRow(ActivationVerdict.NoCreatureRace, "no creature race")]
    [DataRow(ActivationVerdict.NoOuterGate, "outer gate")]
    public void Inert_NamesTheSceneAndWhy(ActivationVerdict verdict, string why)
    {
        var line = CreatureSiegeReport.Inert(verdict, "taom_test_scene");

        StringAssert.Contains(line, "inert");
        StringAssert.Contains(line, "taom_test_scene");
        StringAssert.Contains(line, why);
    }

    [TestMethod]
    public void Inert_EveryVerdictButActive_HasItsOwnReason()
    {
        var lines = System.Enum.GetValues(typeof(ActivationVerdict)).Cast<ActivationVerdict>()
            .Where(v => v != ActivationVerdict.Active)
            .Select(v => CreatureSiegeReport.Inert(v, "scene")).ToList();

        Assert.AreEqual(lines.Count, lines.Distinct().Count());
    }

    // --- the face lines -------------------------------------------------------------------------------------------------

    [TestMethod]
    public void SkippedFace_NamesTheTierAndTheValue()
    {
        var line = CreatureSiegeReport.SkippedFace(new SkippedFace(ExclusionTier.TowerEntrance, 0), "taom_test_scene");

        StringAssert.Contains(line, "taom_test_scene");
        StringAssert.Contains(line, "TowerEntrance");
        StringAssert.Contains(line, "0");
    }

    [TestMethod]
    public void Truncated_NamesTheCapAndEveryDroppedId()
    {
        var plan = new ExclusionPlan(List(1, 2, 3, 4, 5, 6), List<SkippedFace>(), List(601, 602));

        var line = CreatureSiegeReport.Truncated(plan, "taom_test_scene", CreatureSiegeRules.MaxExcludedFaceGroups);

        StringAssert.Contains(line, "cut at " + CreatureSiegeRules.MaxExcludedFaceGroups + " ids");
        StringAssert.Contains(line, "601, 602");
        StringAssert.Contains(line, "taom_test_scene");
    }

    // --- the warnings and the error -------------------------------------------------------------------------------------

    [TestMethod]
    public void LadderQueue_NamesTheSceneTheSpotAndWhatItMeans()
    {
        var line = CreatureSiegeReport.LadderQueue("taom_test_scene", new SiegePoint(100f, 202.2f, 10f));

        StringAssert.Contains(line, "taom_test_scene");
        StringAssert.Contains(line, "100.0, 202.2, 10.0");
        StringAssert.Contains(line, "excluded");
        StringAssert.Contains(line, "ladder queue");
    }

    [TestMethod]
    public void NoReachableSlot_NamesTheSceneAndTheRole()
    {
        var line = CreatureSiegeReport.NoReachableSlot("taom_test_scene", SiegeRole.StrikeOuter);

        StringAssert.Contains(line, "no reachable");
        StringAssert.Contains(line, "taom_test_scene");
        StringAssert.Contains(line, "StrikeOuter");
    }

    [TestMethod]
    public void NoValidAnchor_NamesTheSceneAndTheRole()
    {
        var line = CreatureSiegeReport.NoValidAnchor("taom_test_scene", SiegeRole.HoldGate);

        StringAssert.Contains(line, "no valid anchor");
        StringAssert.Contains(line, "taom_test_scene");
        StringAssert.Contains(line, "HoldGate");
    }

    [TestMethod]
    public void Fault_NamesTheSceneTheExceptionAndThatRoutingStopped()
    {
        var line = CreatureSiegeReport.Fault("taom_test_scene", new System.InvalidOperationException("the engine said no"));

        StringAssert.Contains(line, "reconcile");
        StringAssert.Contains(line, "taom_test_scene");
        StringAssert.Contains(line, "InvalidOperationException");
        StringAssert.Contains(line, "the engine said no");
        StringAssert.Contains(line, "released");
    }

    [TestMethod]
    public void ActivationFault_NamesTheSceneTheExceptionAndThatTheRoleStaysInert()
    {
        var line = CreatureSiegeReport.ActivationFault("taom_test_scene", new System.InvalidOperationException("the engine said no"));

        StringAssert.Contains(line, "activation");
        StringAssert.Contains(line, "taom_test_scene");
        StringAssert.Contains(line, "InvalidOperationException");
        StringAssert.Contains(line, "the engine said no");
        StringAssert.Contains(line, "inert");
    }

    // --- the role line --------------------------------------------------------------------------------------------------

    [TestMethod]
    public void RoleChange_NamesTheRoleTheReasonTheSlotAndWhereItStands()
    {
        var line = CreatureSiegeReport.RoleChange(SiegeRole.StrikeOuter, RoleReason.OuterGateClosed, slot: 3,
            target: new SiegePoint(100f, 202.2f, 10f), agentAt: new SiegePoint(90f, 250f, 9.5f), inLadderQueue: false, suppressed: 0);

        StringAssert.Contains(line, "StrikeOuter");
        StringAssert.Contains(line, "OuterGateClosed");
        StringAssert.Contains(line, "slot 3");
        StringAssert.Contains(line, "100.0, 202.2, 10.0");
        StringAssert.Contains(line, "90.0, 250.0, 9.5");
        StringAssert.Contains(line, "inLadderQueue False");
        Assert.IsFalse(line.Contains("suppressed"));
    }

    [TestMethod]
    public void RoleChange_ASwallowedCount_IsAppended()
    {
        var line = CreatureSiegeReport.RoleChange(SiegeRole.Release, RoleReason.Fleeing, -1, default, default, true, suppressed: 12);

        StringAssert.Contains(line, "12 suppressed");
        StringAssert.Contains(line, "inLadderQueue True");
    }

    [TestMethod]
    public void RoleChange_AReleaseNeedsNoSlotOrTarget()
    {
        var line = CreatureSiegeReport.RoleChange(SiegeRole.Release, RoleReason.PlayerOrder, slot: -1, target: null, agentAt: default,
            inLadderQueue: false, suppressed: 0);

        StringAssert.Contains(line, "Release");
        Assert.IsFalse(line.Contains("slot"));
        Assert.IsFalse(line.Contains("-1"));
    }

    [TestMethod]
    public void Reapplied_NamesTheRoleAndWhichFlagTheEngineCleared()
    {
        var line = CreatureSiegeReport.Reapplied(SiegeRole.StrikeInner, hasScriptedPosition: false, isAttackingEntity: true, suppressed: 0);

        StringAssert.Contains(line, "StrikeInner");
        StringAssert.Contains(line, "position");
        Assert.IsFalse(line.Contains("target"), "only the position was cleared");

        var both = CreatureSiegeReport.Reapplied(SiegeRole.StrikeInner, false, false, 2);
        StringAssert.Contains(both, "position");
        StringAssert.Contains(both, "target");
        StringAssert.Contains(both, "2 suppressed");
    }

    // --- the gate-blow line ---------------------------------------------------------------------------------------------

    [TestMethod]
    public void GateBlow_NamesBaseAndScaledDamageTheMultiplierAndTheGatesHitPointsBeforeTheBlow()
    {
        var line = CreatureSiegeReport.GateBlow(baseDamage: 180f, scaledDamage: 360f, multiplier: 2f, hitPointBefore: 15000f,
            suppressed: 0);

        StringAssert.Contains(line, "[CreatureSiegeRole]");
        StringAssert.Contains(line, "180.0");
        StringAssert.Contains(line, "360.0");
        StringAssert.Contains(line, "x2.0");
        StringAssert.Contains(line, "15000.0");
        Assert.IsFalse(line.Contains("14640.0"), "no after-hp: campaign damage bonuses apply after the hook, so it would be wrong");
    }

    [TestMethod]
    public void GateBlow_ASwallowedCount_IsAppended()
    {
        StringAssert.Contains(CreatureSiegeReport.GateBlow(1f, 2f, 2f, 10f, suppressed: 5), "5 suppressed");
    }

    // --- every line is culture-proof ------------------------------------------------------------------------------------

    [TestMethod]
    public void EveryNumber_IsWrittenWithADot_UnderACommaDecimalCulture()
    {
        var saved = Thread.CurrentThread.CurrentCulture;
        try
        {
            Thread.CurrentThread.CurrentCulture = new CultureInfo("de-DE");

            var role = CreatureSiegeReport.RoleChange(SiegeRole.StrikeOuter, RoleReason.OuterGateClosed, 0,
                new SiegePoint(100.5f, 202.2f, 10f), new SiegePoint(1.5f, 2.5f, 3.5f), false, 0);
            var blow = CreatureSiegeReport.GateBlow(180.5f, 361f, 2f, 15000.5f, 0);

            StringAssert.Contains(role, "100.5, 202.2, 10.0");
            StringAssert.Contains(blow, "180.5");
            Assert.IsFalse(role.Contains("100,5") || blow.Contains("180,5"));
        }
        finally
        {
            Thread.CurrentThread.CurrentCulture = saved;
        }
    }

    [TestMethod]
    public void ANonFiniteNumber_IsWrittenAsItIs_NeverThrows()
    {
        var line = CreatureSiegeReport.GateBlow(float.NaN, float.PositiveInfinity, float.NaN, float.NaN, 0);

        StringAssert.Contains(line, "NaN");
    }
}
