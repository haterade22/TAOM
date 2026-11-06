using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.LongDefeat.Diagnostics;
using TAOM.Features.LongDefeat.Domain;

namespace TAOM.Tests.Features.LongDefeat.Diagnostics;

/// <summary>
/// The <c>field-care</c> verdict of #1079 (phase3-plan.md section 6): the standing defenders' mean health after the heal is the mean before plus the share of what was missing, within 1% because
/// the engine's <c>Agent.Health</c> setter ceils (E23), the purse fell by the price, and the next reading still shows the heal. Pure: the director reads the field.
/// </summary>
[TestClass]
public class LongDefeatFortChecksTests
{
    [TestMethod]
    public void FieldCare_TheMeanAfterEqualsBeforePlusTheShareOfWhatWasMissing_Passes()
    {
        var verdict = LongDefeatFortChecks.FieldCare(1, 0.75f, new LdRest(150, 0.6f, 0.9f), 0.9f, 150, 150);

        Assert.AreEqual(CheckResult.Pass, verdict.Result, verdict.Detail);
        Assert.AreEqual("c=1 share=0.75 count=150 before=0.600 after=0.900 expected=0.900 reread=0.900 paid=150 purse=150", verdict.Detail);
    }

    [TestMethod]
    public void FieldCare_TheCeilOfALimit_AddsUnderOnePercentAndStillPasses()
    {
        // 150 soldiers of 100 health each gain at most 1 health in the ceil: 1% of the limit. Half of that on average.
        Assert.AreEqual(CheckResult.Pass, LongDefeatFortChecks.FieldCare(1, 0.75f, new LdRest(150, 0.6f, 0.905f), 0.905f, 150, 150).Result);
        Assert.AreEqual(CheckResult.Pass, LongDefeatFortChecks.FieldCare(1, 0.75f, new LdRest(150, 0.6f, 0.9099f), null, 150, 150).Result);
    }

    [TestMethod]
    public void FieldCare_AHealThatMissedByMoreThanAPercent_Fails()
    {
        Assert.AreEqual(CheckResult.Fail, LongDefeatFortChecks.FieldCare(1, 0.75f, new LdRest(150, 0.6f, 0.912f), null, 150, 150).Result, "too much");
        Assert.AreEqual(CheckResult.Fail, LongDefeatFortChecks.FieldCare(1, 0.75f, new LdRest(150, 0.6f, 0.7f), null, 150, 150).Result, "too little");
        Assert.AreEqual(CheckResult.Fail, LongDefeatFortChecks.FieldCare(1, 0.75f, new LdRest(150, 0.6f, 0.6f), null, 150, 150).Result, "nothing written");
    }

    [TestMethod]
    public void FieldCare_NobodyHealed_OrNotNumbers_Fail()
    {
        Assert.AreEqual(CheckResult.Fail, LongDefeatFortChecks.FieldCare(1, 0.75f, new LdRest(0, 0f, 0f), null, 150, 150).Result, "no defender stood");
        Assert.AreEqual(CheckResult.Fail, LongDefeatFortChecks.FieldCare(1, 0.75f, new LdRest(10, 0.6f, float.NaN), null, 150, 150).Result);
        Assert.AreEqual(CheckResult.Fail, LongDefeatFortChecks.FieldCare(1, float.NaN, new LdRest(10, 0.6f, 0.9f), null, 150, 150).Result);
    }

    [TestMethod]
    public void FieldCare_TheNextReadingBelowTheHeal_Fails_AndNoReadingIsNotAFailure()
    {
        var rest = new LdRest(150, 0.6f, 0.9f);

        Assert.AreEqual(CheckResult.Fail, LongDefeatFortChecks.FieldCare(1, 0.75f, rest, 0.7f, 150, 150).Result, "the engine undid the write");
        var ended = LongDefeatFortChecks.FieldCare(1, 0.75f, rest, null, 150, 150);
        Assert.AreEqual(CheckResult.Pass, ended.Result);
        StringAssert.Contains(ended.Detail, "reread=none");
    }

    [TestMethod]
    public void FieldCare_ThePurseMustHaveFallenByExactlyThePrice()
    {
        var rest = new LdRest(150, 0.6f, 0.9f);

        Assert.AreEqual(CheckResult.Fail, LongDefeatFortChecks.FieldCare(1, 0.75f, rest, null, 150, 0).Result, "gold never left the purse");
        Assert.AreEqual(CheckResult.Fail, LongDefeatFortChecks.FieldCare(1, 0.75f, rest, null, 150, 300).Result);
        Assert.AreEqual(CheckResult.Fail, LongDefeatFortChecks.FieldCare(1, 0.75f, rest, null, 0, 0).Result, "no purchase stands behind the heal");
    }

    [TestMethod]
    public void FieldCare_NobodyMissingAnyHealth_Skips_ItProvesNothing()
    {
        var verdict = LongDefeatFortChecks.FieldCare(2, 0.75f, new LdRest(150, 1f, 1f), 1f, 150, 150);

        Assert.AreEqual(CheckResult.Skip, verdict.Result);
        StringAssert.Contains(verdict.Detail, "reason=too-little-missing");
    }

    [TestMethod]
    public void FieldCare_AGarrisonNearlyWhole_Skips_ABlankWriteWouldPassInsideTheTolerance()
    {
        // Mean 0.99 with share 0.75: a heal that did nothing leaves |after - expected| = 0.0075, inside the 1% tolerance, so a pass would prove nothing.
        var blank = LongDefeatFortChecks.FieldCare(1, 0.75f, new LdRest(150, 0.99f, 0.99f), 0.99f, 150, 150);
        Assert.AreEqual(CheckResult.Skip, blank.Result, blank.Detail);
        StringAssert.Contains(blank.Detail, "reason=too-little-missing");

        // The window ran from 0.9867 up; the share's gain must clear twice the tolerance before a pass can mean anything.
        Assert.AreEqual(CheckResult.Skip, LongDefeatFortChecks.FieldCare(1, 0.75f, new LdRest(150, 0.975f, 0.975f), 0.975f, 150, 150).Result);
    }

    [TestMethod]
    public void FieldCare_JustPastTheSkipWindow_StillJudgesTheHeal()
    {
        // Gain 0.75 * 0.03 = 0.0225 is over 0.02: a blank write misses by more than the tolerance and fails, a real one passes.
        Assert.AreEqual(CheckResult.Fail, LongDefeatFortChecks.FieldCare(1, 0.75f, new LdRest(150, 0.97f, 0.97f), 0.97f, 150, 150).Result, "nothing written");
        Assert.AreEqual(CheckResult.Pass, LongDefeatFortChecks.FieldCare(1, 0.75f, new LdRest(150, 0.97f, 0.9925f), 0.9925f, 150, 150).Result, "written");
    }

    [TestMethod]
    public void FieldCare_ALiveReadingThatIsNotAMean_FailsTheHeldLeg_OnlyNoReadingAtAllIsExempt()
    {
        var rest = new LdRest(150, 0.6f, 0.9f);

        Assert.AreEqual(CheckResult.Fail, LongDefeatFortChecks.FieldCare(1, 0.75f, rest, -1f, 150, 150).Result, "the adapter read nobody");
        Assert.AreEqual(CheckResult.Fail, LongDefeatFortChecks.FieldCare(1, 0.75f, rest, float.NaN, 150, 150).Result);
        Assert.AreEqual(CheckResult.Pass, LongDefeatFortChecks.FieldCare(1, 0.75f, rest, null, 150, 150).Result, "the run ended first");
    }

    [TestMethod]
    public void TheId_IsTheOneTheManifestReads()
    {
        Assert.AreEqual("taom.long-defeat.field-care", LongDefeatChecks.FieldCareId);
    }
}
