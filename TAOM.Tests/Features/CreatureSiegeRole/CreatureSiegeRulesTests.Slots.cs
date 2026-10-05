using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.CreatureSiegeRole.Domain;
using static TAOM.Tests.Features.CreatureSiegeRole.CreatureSiegeFixtures;

namespace TAOM.Tests.Features.CreatureSiegeRole;

// Where a creature stands: the strike and stand-off slots at a gate, the spread of a hold around its anchor, the anchors
// themselves, and when a creature already holding one needs its scripted state set again.
public partial class CreatureSiegeRulesTests
{
    private const float Epsilon = 1e-4f;

    private static void AssertSlot(SiegeSlot slot, float x, float y, float faceX, float faceY)
    {
        Assert.IsTrue(slot.IsValid, "the slot should exist");
        Assert.AreEqual(x, slot.X, Epsilon, "x");
        Assert.AreEqual(y, slot.Y, Epsilon, "y");
        Assert.AreEqual(faceX, slot.FaceX, Epsilon, "faceX");
        Assert.AreEqual(faceY, slot.FaceY, Epsilon, "faceY");
    }

    // --- which side of the gate ---------------------------------------------------------------------------------------

    [TestMethod]
    public void SideOfGate_OnTheAttackerSide_IsPlusOne_AndOnTheInnerSideMinusOne()
    {
        Assert.AreEqual(1, CreatureSiegeRules.SideOfGate(100f, 230f, 100f, 200f, 0f, 1f));
        Assert.AreEqual(-1, CreatureSiegeRules.SideOfGate(100f, 170f, 100f, 200f, 0f, 1f));
    }

    [TestMethod]
    public void SideOfGate_OnTheGateLine_IsTheAttackerSide()
    {
        Assert.AreEqual(1, CreatureSiegeRules.SideOfGate(130f, 200f, 100f, 200f, 0f, 1f));
    }

    [TestMethod]
    public void SideOfGate_FollowsTheGateAxis()
    {
        Assert.AreEqual(1, CreatureSiegeRules.SideOfGate(130f, 200f, 100f, 200f, 1f, 0f));
        Assert.AreEqual(-1, CreatureSiegeRules.SideOfGate(70f, 200f, 100f, 200f, 1f, 0f));
    }

    [DataTestMethod]
    [DataRow(float.NaN, 1f)]
    [DataRow(1f, float.PositiveInfinity)]
    [DataRow(0f, 0f)]
    public void SideOfGate_WithNoUsableAxis_IsTheAttackerSide(float forwardX, float forwardY)
    {
        Assert.AreEqual(1, CreatureSiegeRules.SideOfGate(100f, 170f, 100f, 200f, forwardX, forwardY));
    }

    [TestMethod]
    public void SideOfGate_ANaNPosition_IsTheAttackerSide()
    {
        Assert.AreEqual(1, CreatureSiegeRules.SideOfGate(float.NaN, 170f, 100f, 200f, 0f, 1f));
    }

    // --- strike slots -------------------------------------------------------------------------------------------------

    [TestMethod]
    public void StrikeSlot_TheFirstSlot_IsAtTheStandOffDistanceFacingTheGate()
    {
        AssertSlot(CreatureSiegeRules.StrikeSlot(100f, 200f, 0f, 1f, side: 1, index: 0), 100f, 202.2f, 0f, -1f);
    }

    [TestMethod]
    public void StrikeSlot_TheNextTwoSlots_FillTheRowLeftAndRight()
    {
        AssertSlot(CreatureSiegeRules.StrikeSlot(100f, 200f, 0f, 1f, 1, 1), 102.8f, 202.2f, 0f, -1f);
        AssertSlot(CreatureSiegeRules.StrikeSlot(100f, 200f, 0f, 1f, 1, 2), 97.2f, 202.2f, 0f, -1f);
    }

    [TestMethod]
    public void StrikeSlot_EachFurtherRow_IsTwoPointEightMetresBack()
    {
        AssertSlot(CreatureSiegeRules.StrikeSlot(100f, 200f, 0f, 1f, 1, 3), 100f, 205f, 0f, -1f);
        AssertSlot(CreatureSiegeRules.StrikeSlot(100f, 200f, 0f, 1f, 1, 11), 97.2f, 210.6f, 0f, -1f);
    }

    [TestMethod]
    public void StrikeSlot_OnTheInnerSide_MirrorsThroughTheGate()
    {
        AssertSlot(CreatureSiegeRules.StrikeSlot(100f, 200f, 0f, 1f, side: -1, index: 0), 100f, 197.8f, 0f, 1f);
        AssertSlot(CreatureSiegeRules.StrikeSlot(100f, 200f, 0f, 1f, -1, 1), 102.8f, 197.8f, 0f, 1f);
    }

    [TestMethod]
    public void StrikeSlot_AnySideValue_IsReadAsItsSign()
    {
        Assert.AreEqual(CreatureSiegeRules.StrikeSlot(100f, 200f, 0f, 1f, 1, 0), CreatureSiegeRules.StrikeSlot(100f, 200f, 0f, 1f, 7, 0));
        Assert.AreEqual(CreatureSiegeRules.StrikeSlot(100f, 200f, 0f, 1f, -1, 0), CreatureSiegeRules.StrikeSlot(100f, 200f, 0f, 1f, -7, 0));
        Assert.AreEqual(CreatureSiegeRules.StrikeSlot(100f, 200f, 0f, 1f, 1, 0), CreatureSiegeRules.StrikeSlot(100f, 200f, 0f, 1f, 0, 0));
    }

    [TestMethod]
    public void StrikeSlot_ARotatedGate_RotatesTheWholeRow()
    {
        // Gate facing +X: the side vector is (0, -1), so slot 1 sits 2.8 m toward -Y.
        AssertSlot(CreatureSiegeRules.StrikeSlot(100f, 200f, 1f, 0f, 1, 0), 102.2f, 200f, -1f, 0f);
        AssertSlot(CreatureSiegeRules.StrikeSlot(100f, 200f, 1f, 0f, 1, 1), 102.2f, 197.2f, -1f, 0f);
    }

    [TestMethod]
    public void StrikeSlot_AScaledGateAxis_IsNormalised()
    {
        AssertSlot(CreatureSiegeRules.StrikeSlot(100f, 200f, 0f, 3.370f, 1, 0), 100f, 202.2f, 0f, -1f);
    }

    [TestMethod]
    public void StrikeSlot_EverySlotOfOneRowFacesTheSameWay_AndNoTwoSlotsShareAPlace()
    {
        var slots = Enumerable.Range(0, CreatureSiegeRules.SlotsPerRole)
            .Select(i => CreatureSiegeRules.StrikeSlot(100f, 200f, 0f, 1f, 1, i)).ToList();

        Assert.IsTrue(slots.All(s => s.IsValid));
        Assert.AreEqual(slots.Count, slots.Select(s => (System.Math.Round(s.X, 2), System.Math.Round(s.Y, 2))).Distinct().Count());
    }

    [TestMethod]
    public void StrikeSlot_ANegativeIndex_IsNone()
    {
        Assert.IsFalse(CreatureSiegeRules.StrikeSlot(100f, 200f, 0f, 1f, 1, -1).IsValid);
    }

    [DataTestMethod]
    [DataRow(float.NaN, 200f, 0f, 1f)]
    [DataRow(100f, float.NaN, 0f, 1f)]
    [DataRow(100f, 200f, float.NaN, 1f)]
    [DataRow(100f, 200f, 0f, float.NaN)]
    [DataRow(float.PositiveInfinity, 200f, 0f, 1f)]
    [DataRow(100f, 200f, 0f, float.NegativeInfinity)]
    [DataRow(100f, 200f, 0f, 0f)]
    [DataRow(100f, 200f, 0.00001f, 0f)]
    public void StrikeSlot_AnyNonFiniteOrUnusableInput_IsNone(float gateX, float gateY, float forwardX, float forwardY)
    {
        var slot = CreatureSiegeRules.StrikeSlot(gateX, gateY, forwardX, forwardY, 1, 0);

        Assert.IsFalse(slot.IsValid);
        Assert.IsTrue(float.IsNaN(slot.X) && float.IsNaN(slot.Y), "a caller that forgets to check cannot send an agent anywhere real");
    }

    // --- stand-off slots ----------------------------------------------------------------------------------------------

    [TestMethod]
    public void StandOffSlot_IsTenMetresOut_WithColumnsStartingFourMetresEitherSideOfTheRamLane()
    {
        // Gate at (100, 200) facing +Y: the side axis is +X. The gate axis itself (x = 100) is the ram lane.
        AssertSlot(CreatureSiegeRules.StandOffSlot(100f, 200f, 0f, 1f, 1, 0), 104f, 210f, 0f, -1f);
        AssertSlot(CreatureSiegeRules.StandOffSlot(100f, 200f, 0f, 1f, 1, 1), 96f, 210f, 0f, -1f);
        AssertSlot(CreatureSiegeRules.StandOffSlot(100f, 200f, 0f, 1f, 1, 2), 108f, 210f, 0f, -1f);
        AssertSlot(CreatureSiegeRules.StandOffSlot(100f, 200f, 0f, 1f, 1, 3), 104f, 212.8f, 0f, -1f);
    }

    [TestMethod]
    public void StandOffSlot_NoSlotOfAnyRow_SitsOnTheGateAxis()
    {
        for (var i = 0; i < CreatureSiegeRules.SlotsPerRole; i++)
        {
            var slot = CreatureSiegeRules.StandOffSlot(100f, 200f, 0f, 1f, 1, i);
            Assert.IsTrue(System.Math.Abs(slot.X - 100f) >= CreatureSiegeRules.StandOffLateral - Epsilon, $"slot {i} at x {slot.X}");
        }
    }

    [TestMethod]
    public void StandOffSlot_EverySlotOfTheBlock_IsADistinctPlace()
    {
        var slots = Enumerable.Range(0, CreatureSiegeRules.SlotsPerRole)
            .Select(i => CreatureSiegeRules.StandOffSlot(100f, 200f, 0f, 1f, 1, i)).ToList();

        Assert.AreEqual(slots.Count, slots.Select(s => (System.Math.Round(s.X, 2), System.Math.Round(s.Y, 2))).Distinct().Count());
    }

    [TestMethod]
    public void StandOffSlot_OnTheInnerSide_MirrorsThroughTheGate()
    {
        AssertSlot(CreatureSiegeRules.StandOffSlot(100f, 200f, 0f, 1f, -1, 0), 104f, 190f, 0f, 1f);
    }

    [TestMethod]
    public void StandOffSlot_NonFiniteOrNegative_IsNone()
    {
        Assert.IsFalse(CreatureSiegeRules.StandOffSlot(float.NaN, 200f, 0f, 1f, 1, 0).IsValid);
        Assert.IsFalse(CreatureSiegeRules.StandOffSlot(100f, 200f, 0f, 0f, 1, 0).IsValid);
        Assert.IsFalse(CreatureSiegeRules.StandOffSlot(100f, 200f, 0f, 1f, 1, -2).IsValid);
    }

    // --- hold slots ---------------------------------------------------------------------------------------------------

    [TestMethod]
    public void HoldSlot_TheFirstSlot_IsTheAnchorItself_FacingOutward()
    {
        AssertSlot(CreatureSiegeRules.HoldSlot(50f, 60f, 0f, 1f, facingSign: 1, index: 0), 50f, 60f, 0f, 1f);
    }

    [TestMethod]
    public void HoldSlot_TheNextSlots_SpreadAcrossTheGateAxis_ThenRowsGoInward()
    {
        AssertSlot(CreatureSiegeRules.HoldSlot(50f, 60f, 0f, 1f, 1, 1), 52.8f, 60f, 0f, 1f);
        AssertSlot(CreatureSiegeRules.HoldSlot(50f, 60f, 0f, 1f, 1, 2), 47.2f, 60f, 0f, 1f);
        AssertSlot(CreatureSiegeRules.HoldSlot(50f, 60f, 0f, 1f, 1, 3), 50f, 57.2f, 0f, 1f);
    }

    [TestMethod]
    public void HoldSlot_FacingInward_ReversesOnlyTheFacing()
    {
        AssertSlot(CreatureSiegeRules.HoldSlot(50f, 60f, 0f, 1f, facingSign: -1, index: 1), 52.8f, 60f, 0f, -1f);
    }

    [TestMethod]
    public void HoldSlot_NonFiniteOrNegative_IsNone()
    {
        Assert.IsFalse(CreatureSiegeRules.HoldSlot(float.NaN, 60f, 0f, 1f, 1, 0).IsValid);
        Assert.IsFalse(CreatureSiegeRules.HoldSlot(50f, 60f, float.NaN, 1f, 1, 0).IsValid);
        Assert.IsFalse(CreatureSiegeRules.HoldSlot(50f, 60f, 0f, 0f, 1, 0).IsValid);
        Assert.IsFalse(CreatureSiegeRules.HoldSlot(50f, 60f, 0f, 1f, 1, -1).IsValid);
    }

    // --- anchors ------------------------------------------------------------------------------------------------------

    [TestMethod]
    public void IsAnchorHeightValid_WithinFourMetresOfTheGateBase_IsTrue()
    {
        Assert.IsTrue(CreatureSiegeRules.IsAnchorHeightValid(10f, 10f));
        Assert.IsTrue(CreatureSiegeRules.IsAnchorHeightValid(13f, 10f));
        Assert.IsTrue(CreatureSiegeRules.IsAnchorHeightValid(6f, 10f), "exactly four below counts");
        Assert.IsTrue(CreatureSiegeRules.IsAnchorHeightValid(14f, 10f), "exactly four above counts");
    }

    [TestMethod]
    public void IsAnchorHeightValid_AWallTopOrAPit_IsFalse()
    {
        Assert.IsFalse(CreatureSiegeRules.IsAnchorHeightValid(14.1f, 10f));
        Assert.IsFalse(CreatureSiegeRules.IsAnchorHeightValid(22f, 10f), "a wall walk");
        Assert.IsFalse(CreatureSiegeRules.IsAnchorHeightValid(5.9f, 10f));
    }

    [DataTestMethod]
    [DataRow(float.NaN, 10f)]
    [DataRow(10f, float.NaN)]
    [DataRow(float.PositiveInfinity, 10f)]
    [DataRow(10f, float.NegativeInfinity)]
    public void IsAnchorHeightValid_ANonFiniteHeight_IsFalse(float groundZ, float gateBaseZ)
    {
        Assert.IsFalse(CreatureSiegeRules.IsAnchorHeightValid(groundZ, gateBaseZ));
    }

    [TestMethod]
    public void InsideOuterGatePoint_IsSixMetresBehindTheGate_AwayFromTheAttackers()
    {
        var (x, y) = CreatureSiegeRules.InsideOuterGatePoint(Gate(x: 100f, y: 200f, forwardX: 0f, forwardY: 1f));

        Assert.AreEqual(100f, x, Epsilon);
        Assert.AreEqual(194f, y, Epsilon);
    }

    [TestMethod]
    public void InsideOuterGatePoint_AScaledAxis_IsNormalised_AndABadOneIsNaN()
    {
        var (_, y) = CreatureSiegeRules.InsideOuterGatePoint(Gate(y: 200f, forwardX: 0f, forwardY: 3.37f));
        Assert.AreEqual(194f, y, Epsilon);

        var (badX, badY) = CreatureSiegeRules.InsideOuterGatePoint(Gate(forwardX: 0f, forwardY: 0f));
        Assert.IsTrue(float.IsNaN(badX) && float.IsNaN(badY));
    }

    [TestMethod]
    public void HoldGateAnchors_AreTheOuterMiddle_ThenTheInnerMiddle_ThenAPointInsideTheOuterGate()
    {
        var outer = Gate(name: "outer", z: 10f, middleX: 100f, middleY: 198f, middleZ: 10f);
        var inner = Gate(name: "inner", y: 180f, z: 11f, middleX: 100f, middleY: 178f, middleZ: 11f);

        var anchors = CreatureSiegeRules.HoldGateAnchors(outer, inner, insideGroundZ: 10.5f);

        CollectionAssert.AreEqual(
            new[] { AnchorSource.OuterMiddle, AnchorSource.InnerMiddle, AnchorSource.InsideOuterGate },
            anchors.Select(a => a.Source).ToArray());
        Assert.AreEqual(new SiegePoint(100f, 198f, 10f), anchors[0].Point);
        Assert.AreEqual(new SiegePoint(100f, 178f, 11f), anchors[1].Point);
        Assert.AreEqual(194f, anchors[2].Point.Y, Epsilon);
        Assert.AreEqual(10.5f, anchors[2].Point.Z, Epsilon);
        Assert.IsTrue(anchors.All(a => a.HeightValid));
    }

    [TestMethod]
    public void CourtyardAnchors_AreTheInnerMiddle_ThenTheOuterMiddle_ThenAPointInsideTheOuterGate()
    {
        var anchors = CreatureSiegeRules.CourtyardAnchors(Gate(), Gate(y: 180f, middleY: 178f), insideGroundZ: 10f);

        CollectionAssert.AreEqual(
            new[] { AnchorSource.InnerMiddle, AnchorSource.OuterMiddle, AnchorSource.InsideOuterGate },
            anchors.Select(a => a.Source).ToArray());
    }

    [TestMethod]
    public void Anchors_AGateWithoutAMiddle_OffersNoMiddleAnchor()
    {
        var outer = Gate(hasMiddle: false);

        var hold = CreatureSiegeRules.HoldGateAnchors(outer, inner: null, insideGroundZ: 10f);

        CollectionAssert.AreEqual(new[] { AnchorSource.InsideOuterGate }, hold.Select(a => a.Source).ToArray());
    }

    [TestMethod]
    public void Anchors_NoInnerGate_OffersNoInnerMiddle()
    {
        var hold = CreatureSiegeRules.HoldGateAnchors(Gate(), inner: null, insideGroundZ: 10f);
        var courtyard = CreatureSiegeRules.CourtyardAnchors(Gate(), inner: null, insideGroundZ: 10f);

        CollectionAssert.AreEqual(new[] { AnchorSource.OuterMiddle, AnchorSource.InsideOuterGate }, hold.Select(a => a.Source).ToArray());
        CollectionAssert.AreEqual(new[] { AnchorSource.OuterMiddle, AnchorSource.InsideOuterGate }, courtyard.Select(a => a.Source).ToArray());
    }

    [TestMethod]
    public void Anchors_AMiddleOnAWallTop_IsOfferedButMarkedHeightInvalid()
    {
        // A middle position sits over a gatehouse in some scenes (one 119 m away is dropped outright, see the distance tests).
        var outer = Gate(z: 10f, middleZ: 24f);

        var hold = CreatureSiegeRules.HoldGateAnchors(outer, inner: null, insideGroundZ: 10f);

        Assert.IsFalse(hold[0].HeightValid);
        Assert.IsTrue(hold[1].HeightValid, "the point inside the gate is the fallback");
    }

    [DataTestMethod]
    [DataRow(15f)]
    [DataRow(14.9f)]
    [DataRow(2f)]
    public void Anchors_AMiddleWithinFifteenMetresOfItsGate_IsOffered(float distance)
    {
        var outer = Gate(middleY: 200f - distance);

        var hold = CreatureSiegeRules.HoldGateAnchors(outer, inner: null, insideGroundZ: 10f);

        Assert.AreEqual(AnchorSource.OuterMiddle, hold[0].Source);
    }

    [DataTestMethod]
    [DataRow(15.5f)]
    [DataRow(119f)]
    [DataRow(27f)]
    public void Anchors_AMiddleFartherThanFifteenMetresFromItsGate_IsDroppedSoTheChainFallsThrough(float distance)
    {
        // mordor_town_minas 119 m, Osgiliath 88 to 93 m, Dale 27 m: the middle_pos belongs to another part of the scene.
        var outer = Gate(middleY: 200f - distance);

        var hold = CreatureSiegeRules.HoldGateAnchors(outer, inner: null, insideGroundZ: 10f);

        CollectionAssert.AreEqual(new[] { AnchorSource.InsideOuterGate }, hold.Select(a => a.Source).ToArray());
    }

    [TestMethod]
    public void Anchors_AFarMiddleIsMeasuredInXY_AndFromItsOwnGate()
    {
        // The inner gate stands 20 m from the outer: its middle 2 m from it is fine, though 22 m from the outer gate.
        var outer = Gate(middleX: 100f + 12f, middleY: 200f + 12f);
        var inner = Gate(y: 180f, middleY: 178f);

        var hold = CreatureSiegeRules.HoldGateAnchors(outer, inner, insideGroundZ: 10f);

        CollectionAssert.AreEqual(new[] { AnchorSource.InnerMiddle, AnchorSource.InsideOuterGate }, hold.Select(a => a.Source).ToArray());
    }

    [TestMethod]
    public void Anchors_AFarInnerMiddle_FallsThroughToTheOuterOneInTheCourtyardChain()
    {
        var inner = Gate(y: 180f, middleY: 180f - 90f);

        var courtyard = CreatureSiegeRules.CourtyardAnchors(Gate(), inner, insideGroundZ: 10f);

        CollectionAssert.AreEqual(new[] { AnchorSource.OuterMiddle, AnchorSource.InsideOuterGate }, courtyard.Select(a => a.Source).ToArray());
    }

    [DataTestMethod]
    [DataRow(float.NaN, 198f)]
    [DataRow(100f, float.NaN)]
    [DataRow(float.PositiveInfinity, 198f)]
    [DataRow(100f, float.NegativeInfinity)]
    public void Anchors_ANonFiniteMiddle_IsDropped(float middleX, float middleY)
    {
        var outer = Gate(middleX: middleX, middleY: middleY);

        var hold = CreatureSiegeRules.HoldGateAnchors(outer, inner: null, insideGroundZ: 10f);

        CollectionAssert.AreEqual(new[] { AnchorSource.InsideOuterGate }, hold.Select(a => a.Source).ToArray());
    }

    [TestMethod]
    public void Anchors_AGateWithANonFiniteOrigin_DropsItsMiddle()
    {
        var outer = Gate(x: float.NaN);

        var hold = CreatureSiegeRules.HoldGateAnchors(outer, inner: null, insideGroundZ: 10f);

        Assert.IsFalse(hold.Any(a => a.Source == AnchorSource.OuterMiddle));
    }

    [TestMethod]
    public void Anchors_AnUnknownGroundHeight_IsHeightInvalid()
    {
        var outer = Gate(middleZ: float.NaN);

        var hold = CreatureSiegeRules.HoldGateAnchors(outer, inner: null, insideGroundZ: float.NaN);

        Assert.IsTrue(hold.All(a => !a.HeightValid));
    }

    [TestMethod]
    public void Anchors_AreJudgedAgainstTheirOwnGatesBase()
    {
        // An inner gate on a terrace 6 m above the outer gate: its middle is fine for it, and would not be for the outer.
        var outer = Gate(z: 10f);
        var inner = Gate(y: 180f, z: 16f, middleY: 178f, middleZ: 16f);

        var hold = CreatureSiegeRules.HoldGateAnchors(outer, inner, insideGroundZ: 10f);

        Assert.IsTrue(hold.Single(a => a.Source == AnchorSource.InnerMiddle).HeightValid);
    }

    // --- reapply --------------------------------------------------------------------------------------------------------

    [DataTestMethod]
    [DataRow(SiegeRole.StrikeOuter)]
    [DataRow(SiegeRole.StrikeInner)]
    public void NeedsReapply_AStrikingCreatureWithBothFlagsIntact_DoesNot(SiegeRole role)
    {
        Assert.IsFalse(CreatureSiegeRules.NeedsReapply(role, hasScriptedPosition: true, isAttackingEntity: true));
    }

    [DataTestMethod]
    [DataRow(SiegeRole.StrikeOuter)]
    [DataRow(SiegeRole.StrikeInner)]
    public void NeedsReapply_AStrikingCreatureThatLostEitherFlag_Does(SiegeRole role)
    {
        Assert.IsTrue(CreatureSiegeRules.NeedsReapply(role, hasScriptedPosition: false, isAttackingEntity: true));
        Assert.IsTrue(CreatureSiegeRules.NeedsReapply(role, hasScriptedPosition: true, isAttackingEntity: false));
        Assert.IsTrue(CreatureSiegeRules.NeedsReapply(role, hasScriptedPosition: false, isAttackingEntity: false));
    }

    [DataTestMethod]
    [DataRow(SiegeRole.StandOff)]
    [DataRow(SiegeRole.HoldCourtyard)]
    [DataRow(SiegeRole.HoldGate)]
    public void NeedsReapply_AHoldingCreature_OnlyCaresAboutItsPosition(SiegeRole role)
    {
        Assert.IsFalse(CreatureSiegeRules.NeedsReapply(role, hasScriptedPosition: true, isAttackingEntity: false));
        Assert.IsFalse(CreatureSiegeRules.NeedsReapply(role, hasScriptedPosition: true, isAttackingEntity: true));
        Assert.IsTrue(CreatureSiegeRules.NeedsReapply(role, hasScriptedPosition: false, isAttackingEntity: false));
        Assert.IsTrue(CreatureSiegeRules.NeedsReapply(role, hasScriptedPosition: false, isAttackingEntity: true));
    }

    [TestMethod]
    public void NeedsReapply_AReleasedCreature_NeverDoes()
    {
        Assert.IsFalse(CreatureSiegeRules.NeedsReapply(SiegeRole.Release, false, false));
        Assert.IsFalse(CreatureSiegeRules.NeedsReapply(SiegeRole.Release, true, true));
    }
}
