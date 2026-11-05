using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.MarshalsWord.Domain;

namespace TAOM.Tests.Features.MarshalsWord;

/// <summary>The pure judgments of the stage M5 and Massed trial steps: what each needs to read true, and the reason it names when it does not.</summary>
[TestClass]
public class MarshalTrialRulesM5Tests
{
    // ---- Massed ----

    [TestMethod]
    public void MassedWordWasDue_OnTheNumbersTheWordWasGivenOn_IsTheRulesOwnAnswer()
    {
        Assert.IsTrue(MarshalTrialRules.MassedWordWasDue(holding: 21, eligible: 30));
        Assert.IsFalse(MarshalTrialRules.MassedWordWasDue(holding: 20, eligible: 30));
        Assert.IsFalse(MarshalTrialRules.MassedWordWasDue(holding: 0, eligible: 0));
    }

    [TestMethod]
    public void MassedPasses_NeedsTheWordOnRealHolders_TheShareAndAPassingRelease()
    {
        Assert.IsTrue(MarshalTrialRules.MassedPasses(words: 1, holding: 25, eligible: 30, releasePassed: true, faulted: false));
        Assert.IsFalse(MarshalTrialRules.MassedPasses(0, 25, 30, true, false), "no word");
        Assert.IsFalse(MarshalTrialRules.MassedPasses(1, MarshalTuning.VolleyMinHolding - 1, 5, true, false), "too few holding to prove a release");
        Assert.IsFalse(MarshalTrialRules.MassedPasses(1, 10, 30, true, false), "the word was given below the share");
        Assert.IsFalse(MarshalTrialRules.MassedPasses(1, 25, 30, false, false), "the archers did not loose");
        Assert.IsFalse(MarshalTrialRules.MassedPasses(1, 25, 30, true, true), "a fault");
    }

    // ---- hold the line ----

    [TestMethod]
    public void HoldGroundReason_TheSplitMustHaveMovedSoldiers_AndBothCompaniesMustBeThePlayers()
    {
        Assert.AreEqual("", MarshalTrialRules.HoldGroundReason(heldUnits: 20, controlUnits: 20, heldControllable: true, controlControllable: true));
        Assert.AreEqual("no-split", MarshalTrialRules.HoldGroundReason(40, 0, true, false), "the engine's AI split refuses a company the player owns, and the control stays empty");
        Assert.AreEqual("no-split", MarshalTrialRules.HoldGroundReason(0, 20, false, true));
        Assert.AreEqual("not-controllable", MarshalTrialRules.HoldGroundReason(20, 20, true, false));
        Assert.AreEqual("not-controllable", MarshalTrialRules.HoldGroundReason(20, 20, false, true));
    }

    // ---- the square ----

    [TestMethod]
    public void SideKept_AfterAUnitIsRemoved_IsWithinOneUnitsWidthOfBefore()
    {
        Assert.IsTrue(MarshalTrialRules.SideKept(before: 14f, after: 14f, unitWidth: 1f));
        Assert.IsTrue(MarshalTrialRules.SideKept(14f, 13.1f, 1f));
        Assert.IsFalse(MarshalTrialRules.SideKept(14f, 12.9f, 1f));
        Assert.IsFalse(MarshalTrialRules.SideKept(14f, 22f, 1f), "a square that grew by its perimeter");
        Assert.IsFalse(MarshalTrialRules.SideKept(float.NaN, 14f, 1f));
        Assert.IsFalse(MarshalTrialRules.SideKept(14f, 14f, float.NaN));
    }

    // ---- the spacing repair ----

    [TestMethod]
    public void SpacingTrialReason_EachWayTheStepCanFail_NamesItsOwnReason_GroundFirst()
    {
        Assert.AreEqual("", MarshalTrialRules.SpacingTrialReason(before: 40, units: 41, riders: 1, any: true, significant: false));
        Assert.AreEqual("rider-not-moved", MarshalTrialRules.SpacingTrialReason(40, 40, 0, false, false), "the first game run: units stayed 40 and no rider came");
        Assert.AreEqual("rider-not-moved", MarshalTrialRules.SpacingTrialReason(40, 39, 1, true, false));
        Assert.AreEqual("no-rider-in-company", MarshalTrialRules.SpacingTrialReason(40, 41, 0, false, false), "a soldier moved and he is on foot");
        Assert.AreEqual("rider-not-counted", MarshalTrialRules.SpacingTrialReason(40, 41, 1, false, false), "the engine's own count never saw the rider");
        Assert.AreEqual("rider-not-counted", MarshalTrialRules.SpacingTrialReason(40, 41, 1, true, true), "one rider in forty-one is not a significant number of them");
    }

    // ---- the approach wait ----

    [TestMethod]
    public void ApproachWaitSeconds_FollowsTheWalkAtTheCompanysSpeed_WithAFloorAndACeiling()
    {
        Assert.AreEqual(MarshalTrialRules.ApproachFloorSeconds, MarshalTrialRules.ApproachWaitSeconds(0f, 3.5f));
        Assert.AreEqual(15f + 2f * 100f / 4f, MarshalTrialRules.ApproachWaitSeconds(100f, 4f), 1e-3f);
        Assert.AreEqual(MarshalTrialRules.ApproachCeilingSeconds, MarshalTrialRules.ApproachWaitSeconds(5000f, 3.5f));
    }

    [TestMethod]
    public void ApproachWaitSeconds_ASlowCachedSpeed_IsPlannedAtTheMinimum_SoTheFloorOfTheEnginesReadDoesNotDivideByNearZero()
    {
        Assert.AreEqual(15f + 2f * 20f / MarshalTrialRules.ApproachMinSpeed, MarshalTrialRules.ApproachWaitSeconds(20f, 0.1f), 1e-3f);
        Assert.AreEqual(15f + 2f * 20f / MarshalTrialRules.ApproachMinSpeed, MarshalTrialRules.ApproachWaitSeconds(20f, float.NaN), 1e-3f);
    }

    [TestMethod]
    public void ApproachWaitSeconds_AWalkThatIsNotANumber_WaitsTheCeiling()
    {
        Assert.AreEqual(MarshalTrialRules.ApproachCeilingSeconds, MarshalTrialRules.ApproachWaitSeconds(float.NaN, 3.5f));
        Assert.AreEqual(MarshalTrialRules.ApproachCeilingSeconds, MarshalTrialRules.ApproachWaitSeconds(float.PositiveInfinity, 3.5f));
        Assert.AreEqual(MarshalTrialRules.ApproachCeilingSeconds, MarshalTrialRules.ApproachWaitSeconds(-3f, 3.5f));
    }

    // ---- the square side, read against the side before the drag ----

    [TestMethod]
    public void SideTarget_IsSeveralUnitsOverTheSideItHad_SoALeftAloneSquareIsNotWithinOneUnitOfIt()
    {
        var target = MarshalTrialRules.SideTarget(before: 7.6f, minimum: 4.4f, maximum: 30f, unitWidth: 0.8f);

        Assert.AreEqual(7.6f + 4f * 0.8f, target, 1e-4f);
        Assert.IsFalse(MarshalTrialRules.SideKept(target, 7.6f, 0.8f), "the run that passed: a do-nothing drag reads the side it had");
    }

    [TestMethod]
    public void SideTarget_NearTheMaximum_AsksForASmallerSide_AndWithNoRoomEitherWayHasNone()
    {
        Assert.AreEqual(20f - 4f * 1f, MarshalTrialRules.SideTarget(20f, 6f, 22f, 1f), 1e-4f);
        Assert.IsTrue(float.IsNaN(MarshalTrialRules.SideTarget(10f, 8f, 12f, 1f)), "four units fit neither above nor below");
    }

    [TestMethod]
    public void SideTarget_ValuesThatAreNotNumbers_HaveNoTarget()
    {
        Assert.IsTrue(float.IsNaN(MarshalTrialRules.SideTarget(float.NaN, 4f, 30f, 1f)));
        Assert.IsTrue(float.IsNaN(MarshalTrialRules.SideTarget(10f, 4f, float.PositiveInfinity, 1f)));
        Assert.IsTrue(float.IsNaN(MarshalTrialRules.SideTarget(10f, 4f, 30f, 0f)));
        Assert.IsTrue(float.IsNaN(MarshalTrialRules.SideTarget(10f, 30f, 4f, 1f)), "a maximum under the minimum");
    }

    [TestMethod]
    public void SideMoved_NeedsMoreThanOneUnitsWidth()
    {
        Assert.IsTrue(MarshalTrialRules.SideMoved(7.6f, 10.8f, 0.8f));
        Assert.IsFalse(MarshalTrialRules.SideMoved(7.6f, 7.6f, 0.8f));
        Assert.IsFalse(MarshalTrialRules.SideMoved(8f, 9f, 1f), "exactly one unit, in numbers a float holds exactly, is not a drag");
        Assert.IsTrue(MarshalTrialRules.SideMoved(8f, 9.01f, 1f));
        Assert.IsFalse(MarshalTrialRules.SideMoved(float.NaN, 10.8f, 0.8f));
    }

    [TestMethod]
    public void SquareTrialReason_ADragThatChangedNothing_IsNamedBeforeTheSideItAskedFor()
    {
        Assert.AreEqual("", MarshalTrialRules.SquareTrialReason(plain: true, laidOut: false, dragMoved: true, askedReached: true, sideKept: true, placed: 39, units: 39, lost: 1));
        Assert.AreEqual("not-the-plain-square", MarshalTrialRules.SquareTrialReason(false, false, true, true, true, 39, 39, 1));
        Assert.AreEqual("drag-did-nothing", MarshalTrialRules.SquareTrialReason(true, false, false, false, true, 39, 39, 1));
        Assert.AreEqual("side-not-reached", MarshalTrialRules.SquareTrialReason(true, false, true, false, true, 39, 39, 1));
        Assert.AreEqual("nobody-removed", MarshalTrialRules.SquareTrialReason(true, false, true, true, true, 40, 40, 0));
        Assert.AreEqual("side-not-kept", MarshalTrialRules.SquareTrialReason(true, false, true, true, false, 39, 39, 1));
        Assert.AreEqual("unplaced", MarshalTrialRules.SquareTrialReason(true, false, true, true, true, 38, 39, 1));
    }

    [TestMethod]
    public void SquareTrialReason_SoldiersTheMixedLayoutPlaces_AreNamedLaidOut_BeforeAnythingAboutTheSide()
    {
        Assert.AreEqual("laid-out", MarshalTrialRules.SquareTrialReason(plain: true, laidOut: true, dragMoved: true, askedReached: true, sideKept: true, placed: 39, units: 39, lost: 1));
        Assert.AreEqual("laid-out", MarshalTrialRules.SquareTrialReason(true, true, false, false, false, 0, 39, 0), "the side has no effect on a laid-out company, so no side fact can pass or fail it");
        Assert.AreEqual("not-the-plain-square", MarshalTrialRules.SquareTrialReason(false, true, true, true, true, 39, 39, 1), "no proof line exists for a class that is not the plain square");
    }

    // ---- hold the line: the verdict rests on the driver's judged check ----

    private static HoldVerdict Verdict(HoldProof proof) => new(proof, FlagOff: 20, HeldBlows: 0, ControlBlows: 28, Seconds: 10.2);

    [TestMethod]
    public void HoldVerdictReason_TheDriverJudgedPass_PassesWhateverALaterWindowWouldHaveRead()
    {
        Assert.AreEqual("", MarshalTrialRules.HoldVerdictReason(Verdict(HoldProof.Pass), peakEnemiesNear: 19, restored: true, flagOffNow: 20));
    }

    [TestMethod]
    public void HoldVerdictReason_EachWayTheStepCanFail_NamesItsOwnReason_DriverFirst()
    {
        Assert.AreEqual("driver-never-judged", MarshalTrialRules.HoldVerdictReason(null, 30, true, flagOffNow: 20));
        Assert.AreEqual("flag-not-off", MarshalTrialRules.HoldVerdictReason(null, 30, true, flagOffNow: 0), "no verdict because the flag never cleared names the flag");
        Assert.AreEqual("enemy-not-in-reach", MarshalTrialRules.HoldVerdictReason(Verdict(HoldProof.Pass), MarshalTrialRules.HoldMinEnemiesInReach - 1, true, 20));
        Assert.AreEqual("flag-not-off", MarshalTrialRules.HoldVerdictReason(Verdict(HoldProof.Pending), 30, true, 20));
        Assert.AreEqual("control-quiet", MarshalTrialRules.HoldVerdictReason(Verdict(HoldProof.NoControl), 30, true, 20));
        Assert.AreEqual("held-company-struck", MarshalTrialRules.HoldVerdictReason(Verdict(HoldProof.Fail), 30, true, 20));
        Assert.AreEqual("flag-not-restored", MarshalTrialRules.HoldVerdictReason(Verdict(HoldProof.Pass), 30, false, 20));
    }

    // ---- hold the line: slot, strike back and the charge ----

    private static string SlotReason(int samples = 45, bool slotFailed = false, int steered = 30, int closer = 25, int farther = 5, string order = "Stop", int qualified = 20,
        int anchored = 20, int woundedEver = 4, int peak = 30, int clamps = 12) =>
        MarshalTrialRules.SlotTrialReason(samples, slotFailed, steered, closer, farther, order, qualified, anchored, woundedEver, peak, clamps);

    [TestMethod]
    public void SlotTrialReason_EachWayTheStepCanFail_NamesItsOwnReason_GroundFirst()
    {
        Assert.AreEqual("", SlotReason());
        Assert.AreEqual("not-qualified", SlotReason(qualified: MarshalTuning.HoldMinFlagOff - 1));
        Assert.AreEqual("order-not-stop", SlotReason(order: "Move"));
        Assert.AreEqual("enemy-not-in-reach", SlotReason(peak: MarshalTrialRules.HoldMinEnemiesInReach - 1));
        Assert.AreEqual("no-anchor", SlotReason(anchored: 9), "a zero under Stop with no anchor is not a pass");
        Assert.AreEqual("nobody-struck", SlotReason(woundedEver: 0));
        Assert.AreEqual("no-clamp", SlotReason(clamps: 0, steered: 0, closer: 0, farther: 0), "a line in place proves nothing about a clamp that never replaced an input");
        Assert.AreEqual("no-clamp", SlotReason(samples: MarshalTrialRules.SlotMinSamples - 1, clamps: 0, steered: 0, closer: 0, farther: 0), "the clamp is named before the count of samples");
        Assert.AreEqual("too-few-samples", SlotReason(samples: MarshalTrialRules.SlotMinSamples - 1));
    }

    [TestMethod]
    public void SlotTrialReason_TheDriversSlotCheckFailing_IsNamedSteeredAway_EvenWhenTheTotalsLookFine()
    {
        Assert.AreEqual("steered-away", SlotReason(slotFailed: true));
        Assert.AreEqual("steered-away", SlotReason(slotFailed: true, clamps: 0, steered: 0, closer: 0, farther: 0), "a failed check is the more useful reason when the clamp also never acted");
    }

    [TestMethod]
    public void SlotTrialReason_TotalsWhereMoreEndedFartherThanCloser_AreNamedSteeredAway_WithTheSameRuleTheDriverUses()
    {
        Assert.AreEqual("steered-away", SlotReason(steered: 12, closer: 3, farther: 9));
        Assert.AreEqual("", SlotReason(steered: 12, closer: 6, farther: 6));
    }

    [TestMethod]
    public void SlotTrialReason_ACounterThatMovedButFewerThanFiveSteeredReads_IsNamedTooFewSteers_NotAPass()
    {
        Assert.AreEqual("too-few-steers", SlotReason(steered: MarshalTuning.SlotMinSteered - 1, closer: 4, farther: 0, clamps: 5));
        Assert.AreEqual("", SlotReason(steered: MarshalTuning.SlotMinSteered, closer: 5, farther: 0, clamps: 5));
    }

    [TestMethod]
    public void SlotMinSamples_IsFifteenSecondsOfHoldReads_SoTheDriversFiveSecondWindowsHaveJudged()
    {
        Assert.AreEqual(45, MarshalTrialRules.SlotMinSamples);
        Assert.AreEqual(15.0, MarshalTrialRules.SlotMinSamples * MarshalTuning.HoldReadSeconds, 1e-9);
    }

    [TestMethod]
    public void StrikeTrialReason_EachWayTheStepCanFail_NamesItsOwnReason()
    {
        const int Off = 30;
        Assert.AreEqual("", MarshalTrialRules.StrikeTrialReason(flagOffBeforeWound: Off, woundedEver: 3, settledPeak: 3, flagOnAtPeak: 3, blowsAfterWound: 2));
        Assert.AreEqual("flag-not-off", MarshalTrialRules.StrikeTrialReason(MarshalTuning.HoldMinFlagOff - 1, 3, 3, 3, 2), "a line that was never disarmed cannot show that a wound arms it");
        Assert.AreEqual("", MarshalTrialRules.StrikeTrialReason(MarshalTuning.HoldMinFlagOff, 3, 3, 3, 2));
        Assert.AreEqual("flag-not-off", MarshalTrialRules.StrikeTrialReason(0, 0, 0, 0, 0), "the ground comes before the wound");
        Assert.AreEqual("nobody-wounded", MarshalTrialRules.StrikeTrialReason(Off, 0, 0, 0, 0));
        Assert.AreEqual("strike-window-missed", MarshalTrialRules.StrikeTrialReason(Off, 3, 0, 0, 2));
        Assert.AreEqual("flag-stayed-off", MarshalTrialRules.StrikeTrialReason(Off, 3, 4, 1, 2));
        Assert.AreEqual("no-blow-after-wound", MarshalTrialRules.StrikeTrialReason(Off, 3, 3, 3, 0));
    }

    [TestMethod]
    public void FlagTrialReason_EachWayTheStepCanFail_NamesItsOwnReason()
    {
        Assert.AreEqual("", MarshalTrialRules.FlagTrialReason(qualified: 20, flagOffBefore: 20, restored: true));
        Assert.AreEqual("not-qualified", MarshalTrialRules.FlagTrialReason(MarshalTuning.HoldMinFlagOff - 1, 20, true));
        Assert.AreEqual("flag-not-off", MarshalTrialRules.FlagTrialReason(20, MarshalTuning.HoldMinFlagOff - 1, true), "a restore proves nothing when nothing was cleared");
        Assert.AreEqual("flag-not-restored", MarshalTrialRules.FlagTrialReason(20, 20, false));
    }
}
