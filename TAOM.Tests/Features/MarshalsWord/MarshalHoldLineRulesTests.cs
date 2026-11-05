using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.MarshalsWord.Domain;

namespace TAOM.Tests.Features.MarshalsWord;

/// <summary>Hold the line beyond the flag (build-spec 9.2): who qualifies, the strike back, the firing order, the slot and the steering clamp.</summary>
[TestClass]
public class MarshalHoldLineRulesTests
{
    private static HoldFacts Ok() => new(ArrangementHolds: true, OrderAllowsHold: true, CompanyUnderPlayer: true, HasShield: true, Mounted: false,
        RangedWielded: false, Detached: false, UsingObject: false, IsMainAgent: false, RunningAway: false, GoingToObject: false);

    // ---- who qualifies ----

    [TestMethod]
    public void Qualifies_AShieldedFootSoldierInAWallUnderAnOrderThatHolds_Qualifies()
    {
        Assert.IsTrue(MarshalHoldLineRules.Qualifies(Ok()));
        Assert.AreEqual("", MarshalHoldLineRules.WhyNot(Ok()));
    }

    [TestMethod]
    public void Qualifies_EachClauseAloneDisqualifies_AndNamesItself()
    {
        Assert.AreEqual("arrangement", MarshalHoldLineRules.WhyNot(Ok() with { ArrangementHolds = false }));
        Assert.AreEqual("order", MarshalHoldLineRules.WhyNot(Ok() with { OrderAllowsHold = false }));
        Assert.AreEqual("company-ai", MarshalHoldLineRules.WhyNot(Ok() with { CompanyUnderPlayer = false }));
        Assert.AreEqual("no-shield", MarshalHoldLineRules.WhyNot(Ok() with { HasShield = false }));
        Assert.AreEqual("mounted", MarshalHoldLineRules.WhyNot(Ok() with { Mounted = true }));
        Assert.AreEqual("ranged", MarshalHoldLineRules.WhyNot(Ok() with { RangedWielded = true }));
        Assert.AreEqual("detached", MarshalHoldLineRules.WhyNot(Ok() with { Detached = true }));
        Assert.AreEqual("using-object", MarshalHoldLineRules.WhyNot(Ok() with { UsingObject = true }));
        Assert.AreEqual("main-agent", MarshalHoldLineRules.WhyNot(Ok() with { IsMainAgent = true }));
        Assert.AreEqual("running-away", MarshalHoldLineRules.WhyNot(Ok() with { RunningAway = true }));
        Assert.AreEqual("going-to-object", MarshalHoldLineRules.WhyNot(Ok() with { GoingToObject = true }));
        Assert.IsFalse(MarshalHoldLineRules.Qualifies(Ok() with { CompanyUnderPlayer = false }));
    }

    [TestMethod]
    public void Qualifies_ACompanyUnderTheAi_NeverQualifies_SoNoSoldierStaysDisarmedWithNobodyToPressTheKey()
    {
        var facts = Ok() with { CompanyUnderPlayer = false };

        Assert.IsFalse(MarshalHoldLineRules.Qualifies(facts));
        Assert.AreEqual(HoldWant.Off, MarshalHoldLineRules.Want(holdOn: true, qualifies: MarshalHoldLineRules.Qualifies(facts), woundedAt: double.NegativeInfinity, now: 10.0));
    }

    // ---- strike back ----

    [TestMethod]
    public void IsWound_DamageFromAMelee_Counts()
    {
        Assert.IsTrue(MarshalHoldLineRules.IsWound(damage: 1, isMissile: false));
    }

    [TestMethod]
    public void IsWound_AParriedOrBlockedBlowWithNoDamage_DoesNotCount()
    {
        Assert.IsFalse(MarshalHoldLineRules.IsWound(damage: 0, isMissile: false));
        Assert.IsFalse(MarshalHoldLineRules.IsWound(damage: -3, isMissile: false));
    }

    [TestMethod]
    public void IsWound_AMissile_DoesNotCount_SoAnArrowDoesNotArmTheLine()
    {
        Assert.IsFalse(MarshalHoldLineRules.IsWound(damage: 12, isMissile: true));
    }

    [TestMethod]
    public void StrikingBack_NeverWounded_IsFalse()
    {
        Assert.IsFalse(MarshalHoldLineRules.StrikingBack(double.NegativeInfinity, 500.0));
    }

    [TestMethod]
    public void StrikingBack_JustUnderTheWindow_IsTrue_AndAtItsEndIsNot()
    {
        Assert.IsTrue(MarshalHoldLineRules.StrikingBack(100.0, 100.0 + MarshalTuning.StrikeBackSeconds - 0.01));
        Assert.IsFalse(MarshalHoldLineRules.StrikingBack(100.0, 100.0 + MarshalTuning.StrikeBackSeconds));
    }

    [TestMethod]
    public void BlowIsStrikeBack_AWoundedSoldierOfTheLineInsideTheWindow_IsTheLinesAnswer()
    {
        Assert.IsTrue(MarshalHoldLineRules.BlowIsStrikeBack(inTheLine: true, woundedAt: 100.0, now: 101.0));
    }

    [TestMethod]
    public void BlowIsStrikeBack_ASoldierOutsideTheLine_IsAnOrdinaryBlow_SoAControlCompanyIsNeverUndercounted()
    {
        Assert.IsFalse(MarshalHoldLineRules.BlowIsStrikeBack(inTheLine: false, woundedAt: 100.0, now: 101.0));
    }

    [TestMethod]
    public void BlowIsStrikeBack_ASoldierOfTheLineNeverWoundedOrPastTheWindow_IsABlowTheFlagShouldHaveStopped()
    {
        Assert.IsFalse(MarshalHoldLineRules.BlowIsStrikeBack(true, double.NegativeInfinity, 101.0));
        Assert.IsFalse(MarshalHoldLineRules.BlowIsStrikeBack(true, 100.0, 100.0 + MarshalTuning.StrikeBackSeconds));
    }

    [TestMethod]
    public void Want_HoldOnAndQualifiedAndNotWounded_ClearsTheFlag()
    {
        Assert.AreEqual(HoldWant.FlagOff, MarshalHoldLineRules.Want(true, true, double.NegativeInfinity, 50.0));
    }

    [TestMethod]
    public void Want_HoldOnAndQualifiedAndJustWounded_GivesTheFlagBack_ThenTakesItAgainWhenTheWindowEnds()
    {
        Assert.AreEqual(HoldWant.StrikeBack, MarshalHoldLineRules.Want(true, true, 50.0, 51.0));
        Assert.AreEqual(HoldWant.FlagOff, MarshalHoldLineRules.Want(true, true, 50.0, 50.0 + MarshalTuning.StrikeBackSeconds));
    }

    [TestMethod]
    public void Want_HoldOffOrNotQualified_IsOff_EvenForAWoundedSoldier()
    {
        Assert.AreEqual(HoldWant.Off, MarshalHoldLineRules.Want(false, true, 50.0, 51.0));
        Assert.AreEqual(HoldWant.Off, MarshalHoldLineRules.Want(true, false, 50.0, 51.0));
    }

    // ---- the firing order ----

    [TestMethod]
    public void FiringAction_TheLineStandsAndTheOrderDiffers_HoldsFire()
    {
        Assert.AreEqual(FiringAct.Hold, MarshalHoldLineRules.FiringAction(lineStands: true, current: 1, holdValue: 0, formationValue: 1, owed: false));
    }

    [TestMethod]
    public void FiringAction_TheLineStandsAndTheOrderIsAlreadyHoldFire_WritesNothing()
    {
        Assert.AreEqual(FiringAct.None, MarshalHoldLineRules.FiringAction(true, 0, 0, 1, owed: true));
        Assert.AreEqual(FiringAct.None, MarshalHoldLineRules.FiringAction(true, 0, 0, 0, owed: false));
    }

    [TestMethod]
    public void FiringAction_TheLineStopsAfterWeWroteIt_GivesTheCompanysOwnOrderBack()
    {
        Assert.AreEqual(FiringAct.Restore, MarshalHoldLineRules.FiringAction(lineStands: false, current: 0, holdValue: 0, formationValue: 1, owed: true));
    }

    [TestMethod]
    public void FiringAction_TheLineStopsAndSomeoneAlreadyGaveTheOrderBack_ForgetsTheDebtWithoutAWrite()
    {
        Assert.AreEqual(FiringAct.Forget, MarshalHoldLineRules.FiringAction(false, 1, 0, 1, owed: true));
    }

    [TestMethod]
    public void FiringAction_TheLineStopsAndWeNeverWroteIt_LeavesTheOrderAlone()
    {
        Assert.AreEqual(FiringAct.None, MarshalHoldLineRules.FiringAction(false, 0, 0, 1, owed: false));
    }

    // ---- the slot ----

    [TestMethod]
    public void ReadSlot_UnderAMoveWithAValidPoint_IsThePoint_AndDropsAnyAnchor()
    {
        var slot = MarshalHoldLineRules.ReadSlot(standGround: false, anchored: true, 1f, 1f, orderValid: true, 10f, 20f, 3f, 4f);

        Assert.IsTrue(slot.Valid);
        Assert.AreEqual(10f, slot.X);
        Assert.AreEqual(20f, slot.Y);
        Assert.IsFalse(slot.Anchored);
    }

    [TestMethod]
    public void ReadSlot_UnderStopWithNoAnchor_TakesTheSoldiersOwnPlaceOnce()
    {
        var slot = MarshalHoldLineRules.ReadSlot(standGround: true, anchored: false, 0f, 0f, orderValid: true, 99f, 99f, 3f, 4f);

        Assert.IsTrue(slot.Valid);
        Assert.AreEqual(3f, slot.X);
        Assert.AreEqual(4f, slot.Y);
        Assert.IsTrue(slot.Anchored);
    }

    [TestMethod]
    public void ReadSlot_UnderStopWithAnAnchor_KeepsItWhileTheSoldierDrifts_SoAZeroUnderStopCannotHideADrift()
    {
        var slot = MarshalHoldLineRules.ReadSlot(standGround: true, anchored: true, 3f, 4f, orderValid: true, 99f, 99f, 9f, 9f);

        Assert.AreEqual(3f, slot.X);
        Assert.AreEqual(4f, slot.Y);
        Assert.IsTrue(slot.Anchored);
        Assert.IsTrue(MarshalHoldLineRules.Beyond((float)Math.Sqrt((9 - 3) * (9 - 3) + (9 - 4) * (9 - 4))));
    }

    [TestMethod]
    public void ReadSlot_AnInvalidOrNotANumberPoint_StoresNoSlot_SoNaNNeverReachesNative()
    {
        Assert.IsFalse(MarshalHoldLineRules.ReadSlot(false, false, 0f, 0f, orderValid: false, 1f, 1f, 0f, 0f).Valid);
        Assert.IsFalse(MarshalHoldLineRules.ReadSlot(false, false, 0f, 0f, true, float.NaN, 1f, 0f, 0f).Valid);
        Assert.IsFalse(MarshalHoldLineRules.ReadSlot(false, false, 0f, 0f, true, 1f, float.PositiveInfinity, 0f, 0f).Valid);
        Assert.IsFalse(MarshalHoldLineRules.ReadSlot(true, false, 0f, 0f, true, 1f, 1f, float.NaN, 0f).Valid, "an own place that is not a number is no anchor");
    }

    [TestMethod]
    public void Beyond_AtTheSlackIsInPlace_AndJustOverIsBeyond_AndNotANumberIsNot()
    {
        Assert.IsFalse(MarshalHoldLineRules.Beyond(MarshalTuning.SlotSlack));
        Assert.IsTrue(MarshalHoldLineRules.Beyond(MarshalTuning.SlotSlack + 0.01f));
        Assert.IsFalse(MarshalHoldLineRules.Beyond(float.NaN));
    }

    // ---- the steering clamp ----

    [TestMethod]
    public void ClampInput_AWalkAwayFromTheSlot_BecomesAStepTowardIt()
    {
        var result = MarshalHoldLineRules.ClampInput(inX: 0f, inY: 1f, toSlotX: 0f, toSlotY: -3f);

        Assert.AreEqual(0f, result.X, 1e-5f);
        Assert.AreEqual(-1f, result.Y, 1e-5f);
    }

    [TestMethod]
    public void ClampInput_ASidestepAlongTheLine_Stays()
    {
        var result = MarshalHoldLineRules.ClampInput(1f, 0f, 0f, -3f);

        Assert.AreEqual(1f, result.X);
        Assert.AreEqual(0f, result.Y);
    }

    [TestMethod]
    public void ClampInput_AWalkTowardTheSlot_Stays()
    {
        var result = MarshalHoldLineRules.ClampInput(0f, 0.7f, 0f, 5f);

        Assert.AreEqual(0f, result.X);
        Assert.AreEqual(0.7f, result.Y);
    }

    [TestMethod]
    public void ClampInput_AStepJustInsideTheAwayCosine_Stays_AndJustPastItIsReplaced()
    {
        var justInside = (float)Math.Acos(-MarshalTuning.SlotAwayDot + 0.01);
        var justPast = (float)Math.Acos(-MarshalTuning.SlotAwayDot - 0.01);

        var inside = MarshalHoldLineRules.ClampInput((float)Math.Sin(justInside), (float)Math.Cos(justInside), 0f, 4f);
        var past = MarshalHoldLineRules.ClampInput((float)Math.Sin(justPast), (float)Math.Cos(justPast), 0f, 4f);

        Assert.AreEqual((float)Math.Sin(justInside), inside.X, 1e-6f);
        Assert.AreEqual(0f, past.X, 1e-5f);
        Assert.AreEqual(1f, past.Y, 1e-5f);
    }

    // ---- what the clamp did to the distance ----

    [TestMethod]
    public void Trend_ASteeredSoldierWhoEndsNearerTheSlot_CountsAsCloser()
    {
        Assert.AreEqual(SlotTrend.Closer, MarshalHoldLineRules.Trend(baseline: 3.0f, current: 2.2f, steered: true, strikingBack: false));
    }

    [TestMethod]
    public void Trend_ASteeredSoldierWhoEndsFartherFromTheSlot_CountsAsFarther()
    {
        Assert.AreEqual(SlotTrend.Farther, MarshalHoldLineRules.Trend(baseline: 2.2f, current: 3.0f, steered: true, strikingBack: false));
    }

    [TestMethod]
    public void Trend_ASoldierTheClampDidNotSteer_CountsForNeitherWhicheverWayHeWent()
    {
        Assert.AreEqual(SlotTrend.None, MarshalHoldLineRules.Trend(3.0f, 2.0f, steered: false, strikingBack: false));
        Assert.AreEqual(SlotTrend.None, MarshalHoldLineRules.Trend(2.0f, 3.0f, steered: false, strikingBack: false));
    }

    [TestMethod]
    public void Trend_AChangeInsideTheDeadband_IsSteady_CountsForNeitherSide_AndJustOutsideItCounts()
    {
        var band = MarshalTuning.SlotTrendMetres;

        Assert.AreEqual(SlotTrend.Steady, MarshalHoldLineRules.Trend(2.0f, 2.0f, true, false));
        Assert.AreEqual(SlotTrend.Steady, MarshalHoldLineRules.Trend(2.0f, 2.0f - band * 0.9f, true, false));
        Assert.AreEqual(SlotTrend.Steady, MarshalHoldLineRules.Trend(2.0f, 2.0f + band * 0.9f, true, false));
        Assert.AreEqual(SlotTrend.Closer, MarshalHoldLineRules.Trend(2.0f, 2.0f - band * 1.1f, true, false));
        Assert.AreEqual(SlotTrend.Farther, MarshalHoldLineRules.Trend(2.0f, 2.0f + band * 1.1f, true, false));
    }

    [TestMethod]
    public void Trend_ASoldierInHisStrikeBackWindow_IsLeftOutWhicheverWayHeWent()
    {
        Assert.AreEqual(SlotTrend.None, MarshalHoldLineRules.Trend(3.0f, 1.0f, steered: true, strikingBack: true));
        Assert.AreEqual(SlotTrend.None, MarshalHoldLineRules.Trend(1.0f, 3.0f, steered: true, strikingBack: true));
        Assert.AreEqual(SlotTrend.None, MarshalHoldLineRules.Trend(2.0f, 2.0f, steered: true, strikingBack: true), "not even a steady read counts for a struck soldier");
    }

    [TestMethod]
    public void Trend_ADistanceThatIsNotANumber_CountsForNeither()
    {
        Assert.AreEqual(SlotTrend.None, MarshalHoldLineRules.Trend(float.NaN, 2.0f, true, false), "no previous read");
        Assert.AreEqual(SlotTrend.None, MarshalHoldLineRules.Trend(2.0f, float.NaN, true, false));
        Assert.AreEqual(SlotTrend.None, MarshalHoldLineRules.Trend(float.PositiveInfinity, 2.0f, true, false));
        Assert.AreEqual(SlotTrend.None, MarshalHoldLineRules.Trend(2.0f, float.NegativeInfinity, true, false));
    }

    [TestMethod]
    public void IsLoose_AReadBeyondTheSlackWithNoReplacedTick_IsLoose_AndASteeredOrInPlaceReadIsNot()
    {
        Assert.IsTrue(MarshalHoldLineRules.IsLoose(MarshalTuning.SlotSlack + 0.01f, steered: false));
        Assert.IsFalse(MarshalHoldLineRules.IsLoose(MarshalTuning.SlotSlack + 0.01f, steered: true), "the clamp acted since the last read");
        Assert.IsFalse(MarshalHoldLineRules.IsLoose(MarshalTuning.SlotSlack, steered: false), "inside the slack he is in place");
        Assert.IsFalse(MarshalHoldLineRules.IsLoose(float.NaN, steered: false), "no slot, no distance");
    }

    [TestMethod]
    public void Moves_ZeroNotANumberAndTinyInputsAreNotAWalk_AndEveryRealOneIs()
    {
        Assert.IsFalse(MarshalHoldLineRules.Moves(0f, 0f));
        Assert.IsFalse(MarshalHoldLineRules.Moves(1e-7f, -1e-7f));
        Assert.IsFalse(MarshalHoldLineRules.Moves(float.NaN, 1f));
        Assert.IsTrue(MarshalHoldLineRules.Moves(0f, 0.1f));
        Assert.IsTrue(MarshalHoldLineRules.Moves(-0.5f, 0f));
    }

    [TestMethod]
    public void BlowCountsForLine_ASoldierOfAHeldCompanyWhoIsNotInTheLine_IsNotALineBlow()
    {
        Assert.IsFalse(MarshalHoldLineRules.BlowCountsForLine(inTheLine: false, companyHeld: true));
    }

    [TestMethod]
    public void BlowCountsForLine_ASoldierInTheLine_OrAnyoneOfACompanyNotHeld_Counts_SoTheControlIsNeverUndercounted()
    {
        Assert.IsTrue(MarshalHoldLineRules.BlowCountsForLine(inTheLine: true, companyHeld: true));
        Assert.IsTrue(MarshalHoldLineRules.BlowCountsForLine(inTheLine: false, companyHeld: false));
        Assert.IsTrue(MarshalHoldLineRules.BlowCountsForLine(inTheLine: true, companyHeld: false));
    }

    [TestMethod]
    public void ClampInput_ASoldierInsideTheSlack_IsNeverSteered()
    {
        var result = MarshalHoldLineRules.ClampInput(0f, 1f, 0f, -MarshalTuning.SlotSlack);

        Assert.AreEqual(0f, result.X);
        Assert.AreEqual(1f, result.Y);
    }

    [TestMethod]
    public void ClampInput_NoInput_StaysZero()
    {
        var result = MarshalHoldLineRules.ClampInput(0f, 0f, 0f, -4f);

        Assert.AreEqual(0f, result.X);
        Assert.AreEqual(0f, result.Y);
    }

    [TestMethod]
    public void ClampInput_ASlotCloserThanAMetre_ScalesTheStepToTheDistance_SoTheLengthIsAtMostOne()
    {
        var near = MarshalHoldLineRules.ClampInput(0f, 1f, 0f, -0.5f);
        var far = MarshalHoldLineRules.ClampInput(0f, 1f, 0f, -50f);

        Assert.AreEqual(-0.5f, near.Y, 1e-5f);
        Assert.AreEqual(-1f, far.Y, 1e-5f);
    }

    [TestMethod]
    public void ClampInput_NotANumberOrInfinity_ComesBackUnchanged_SoNativeNeverReceivesIt()
    {
        var nan = MarshalHoldLineRules.ClampInput(float.NaN, 1f, 0f, -3f);
        var slot = MarshalHoldLineRules.ClampInput(0f, 1f, float.NaN, -3f);
        var inf = MarshalHoldLineRules.ClampInput(0f, 1f, 0f, float.NegativeInfinity);

        Assert.IsTrue(float.IsNaN(nan.X));
        Assert.AreEqual(1f, nan.Y);
        Assert.AreEqual(0f, slot.X);
        Assert.AreEqual(1f, slot.Y);
        Assert.AreEqual(1f, inf.Y);
    }

    // ---- the two judged checks ----

    [TestMethod]
    public void StrikeProof_NoSettledWound_IsPending_SoALineNeverStruckProvesNothing()
    {
        Assert.AreEqual(HoldProof.Pending, MarshalHoldLineRules.StrikeProof(settled: 0, flagOn: 0));
    }

    [TestMethod]
    public void StrikeProof_HalfTheWoundedHaveTheFlagOn_Passes_AndFewerFails()
    {
        Assert.AreEqual(HoldProof.Pass, MarshalHoldLineRules.StrikeProof(settled: 4, flagOn: 2));
        Assert.AreEqual(HoldProof.Fail, MarshalHoldLineRules.StrikeProof(settled: 4, flagOn: 1));
        Assert.AreEqual(HoldProof.Fail, MarshalHoldLineRules.StrikeProof(settled: 1, flagOn: 0), "the case of a strike back that never gives the flag back");
    }

    [TestMethod]
    public void TrendProof_FewerThanFiveSteeredReads_IsPending_WhateverTheSides()
    {
        Assert.AreEqual(HoldProof.Pending, MarshalHoldLineRules.TrendProof(steered: MarshalTuning.SlotMinSteered - 1, closer: 0, farther: 4));
        Assert.AreEqual(HoldProof.Pending, MarshalHoldLineRules.TrendProof(0, 0, 0));
    }

    [TestMethod]
    public void TrendProof_MoreSteeredSoldiersEndedFartherThanCloser_Fails()
    {
        Assert.AreEqual(HoldProof.Fail, MarshalHoldLineRules.TrendProof(steered: 9, closer: 3, farther: 4));
        Assert.AreEqual(HoldProof.Fail, MarshalHoldLineRules.TrendProof(steered: MarshalTuning.SlotMinSteered, closer: 0, farther: 1));
    }

    [TestMethod]
    public void TrendProof_AsManyFartherAsCloser_OrFewer_Passes_SoTheMeasuredRunsReadPass()
    {
        Assert.AreEqual(HoldProof.Pass, MarshalHoldLineRules.TrendProof(steered: 9, closer: 4, farther: 4));
        Assert.AreEqual(HoldProof.Pass, MarshalHoldLineRules.TrendProof(steered: 27, closer: 22, farther: 5), "closer 22 against farther 5, the three newest runs together");
        Assert.AreEqual(HoldProof.Pass, MarshalHoldLineRules.TrendProof(steered: MarshalTuning.SlotMinSteered, closer: 0, farther: 0), "five steady reads: the clamp moved nobody the wrong way");
    }

    [TestMethod]
    public void SlotProof_UnderStopWithTheLineStruckAndTheClampSteeringIt_Passes()
    {
        Assert.AreEqual(HoldProof.Pass, MarshalHoldLineRules.SlotProof("Stop", qualified: 20, anchored: 20, woundedEver: 3, steered: 8, closer: 6, farther: 2));
    }

    [TestMethod]
    public void SlotProof_MoreSteeredSoldiersEndedFartherThanCloser_Fails_HoweverNearTheAnchorsTheyStand()
    {
        Assert.AreEqual(HoldProof.Fail, MarshalHoldLineRules.SlotProof("Stop", 20, 20, woundedEver: 3, steered: 8, closer: 2, farther: 6));
    }

    [TestMethod]
    public void SlotProof_FarSoldiersAreNotAFailureAlone_ACrowdThatPushesThemIsNotTheClamp()
    {
        Assert.AreEqual(HoldProof.Pending, MarshalHoldLineRules.SlotProof("Stop", 20, 20, woundedEver: 3, steered: 0, closer: 0, farther: 0),
            "seven of nineteen stood far in run 010922 with one counted read: that proves nothing about the clamp");
    }

    [TestMethod]
    public void SlotProof_AnOrderOtherThanStop_IsPending_SoAMovingSlotIsNeverJudged()
    {
        Assert.AreEqual(HoldProof.Pending, MarshalHoldLineRules.SlotProof("Move", 20, 20, woundedEver: 3, steered: 20, closer: 0, farther: 20));
    }

    [TestMethod]
    public void SlotProof_TooFewQualifiedOrNoAnchorOrNobodyStruckOrTooFewSteered_IsPending()
    {
        Assert.AreEqual(HoldProof.Pending, MarshalHoldLineRules.SlotProof("Stop", MarshalTuning.HoldMinFlagOff - 1, 4, 3, 8, 8, 0));
        Assert.AreEqual(HoldProof.Pending, MarshalHoldLineRules.SlotProof("Stop", 20, 9, 3, 8, 8, 0), "most soldiers have read no anchor yet");
        Assert.AreEqual(HoldProof.Pending, MarshalHoldLineRules.SlotProof("Stop", 20, 20, woundedEver: 0, steered: 8, closer: 8, farther: 0), "nobody pressed the line");
        Assert.AreEqual(HoldProof.Pending, MarshalHoldLineRules.SlotProof("Stop", 20, 20, 3, steered: MarshalTuning.SlotMinSteered - 1, closer: 4, farther: 0));
    }
}
