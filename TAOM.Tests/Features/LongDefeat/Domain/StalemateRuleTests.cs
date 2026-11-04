using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.LongDefeat.Domain;

namespace TAOM.Tests.Features.LongDefeat.Domain;

/// <summary>The spike's numbers: 60 s quiet with half the wave down, 180 s at any count, 90 s after the charge.</summary>
[TestClass]
public class StalemateRuleTests
{
    private static readonly RunRules R = RunRules.Default;

    /// <summary>The charge path alone: the stragglers' short wait never comes.</summary>
    private static readonly RunRules LongPath = R with { StragglerCount = 0 };

    /// <summary>Ticks one second at a time and returns the first decision that is not None.</summary>
    private static StalemateDecision Quiet(ref StalemateState s, float seconds, int attackers, int defenders, int wave = 110, RunRules? rules = null)
    {
        for (var t = 0f; t < seconds; t += 1f)
        {
            var d = StalemateRule.Step(ref s, 1f, false, attackers, defenders, wave, rules ?? R);
            if (d.Action != StalemateAction.None)
                return d;
        }

        return default;
    }

    [TestMethod]
    public void FiveOrFewerAttackers_FallAfterSixtySeconds_WithNoCharge()
    {
        var s = new StalemateState();

        Assert.AreEqual(StalemateAction.None, Quiet(ref s, 59, 5, 48).Action);

        var last = StalemateRule.Step(ref s, 1f, false, 5, 48, 110, R);

        Assert.AreEqual(StalemateAction.FellStragglers, last.Action);
        Assert.AreEqual(5, last.Count);
        Assert.IsFalse(s.Charging, "the defenders stay on the walls");
    }

    [TestMethod]
    public void ADeath_DoesNotRestartTheStragglersClock()
    {
        var s = new StalemateState();
        Quiet(ref s, 59, 2, 48);

        var d = StalemateRule.Step(ref s, 1f, true, 2, 48, 110, R);

        Assert.AreEqual(StalemateAction.FellStragglers, d.Action);
    }

    [TestMethod]
    public void SixAttackers_TakeTheChargePath()
    {
        var s = new StalemateState();

        Assert.AreEqual(StalemateAction.Charge, Quiet(ref s, R.QuietSeconds, 6, 48).Action);
    }

    [TestMethod]
    public void StragglersThatOutnumberTheDefenders_TakeTheChargePath()
    {
        var s = new StalemateState();

        Assert.AreEqual(StalemateAction.Charge, Quiet(ref s, R.QuietSeconds, 5, 5).Action);
    }

    [TestMethod]
    public void TheClockStarts_WhenTheCountFirstReachesFive_EvenDuringACharge()
    {
        var s = new StalemateState();
        Assert.AreEqual(StalemateAction.Charge, Quiet(ref s, R.QuietSeconds, 40, 48).Action);

        Assert.AreEqual(StalemateAction.None, Quiet(ref s, 59, 2, 48).Action);
        Assert.AreEqual(StalemateAction.FellStragglers, StalemateRule.Step(ref s, 1f, false, 2, 48, 110, R).Action);
    }

    [TestMethod]
    public void NaNStragglerSeconds_NeverFellsByThisRule_AndTheWaveCanStillCharge()
    {
        var rules = R with { StragglerSeconds = float.NaN };
        var s = new StalemateState();

        Assert.AreEqual(StalemateAction.Charge, Quiet(ref s, R.QuietSeconds, 2, 48, rules: rules).Action);
    }

    [TestMethod]
    public void HalfTheWaveDown_ChargesAfterQuietSeconds()
    {
        var s = new StalemateState();

        Assert.AreEqual(StalemateAction.None, Quiet(ref s, 59, 55, 100).Action);
        Assert.AreEqual(StalemateAction.Charge, StalemateRule.Step(ref s, 1f, false, 55, 100, 110, R).Action);
    }

    [TestMethod]
    public void MoreThanHalfStanding_WaitsForTheAnyCountLimit()
    {
        var s = new StalemateState();

        Assert.AreEqual(StalemateAction.None, Quiet(ref s, 179, 56, 100).Action);
        Assert.AreEqual(StalemateAction.Charge, StalemateRule.Step(ref s, 1f, false, 56, 100, 110, R).Action);
    }

    [TestMethod]
    public void ALoss_RestartsTheQuiet()
    {
        var s = new StalemateState();
        Quiet(ref s, 59, 55, 100);

        StalemateRule.Step(ref s, 1f, true, 54, 100, 110, R);

        Assert.AreEqual(StalemateAction.None, Quiet(ref s, 59, 54, 100).Action);
        Assert.AreEqual(StalemateAction.Charge, StalemateRule.Step(ref s, 1f, false, 54, 100, 110, R).Action);
    }

    [TestMethod]
    public void AfterTheCharge_FewStragglersAgainstMoreDefenders_AreFelled()
    {
        var s = new StalemateState();
        Quiet(ref s, 60, 8, 31, rules: LongPath);

        Assert.AreEqual(StalemateAction.None, Quiet(ref s, 89, 8, 31, rules: LongPath).Action);
        var d = StalemateRule.Step(ref s, 1f, false, 8, 31, 110, LongPath);

        Assert.AreEqual(StalemateAction.FellStragglers, d.Action);
        Assert.AreEqual(8, d.Count);
    }

    [TestMethod]
    public void AfterTheFell_TheQuietRestarts()
    {
        var s = new StalemateState();
        Quiet(ref s, 60, 8, 31, rules: LongPath);
        Assert.AreEqual(StalemateAction.FellStragglers, Quiet(ref s, 90, 8, 31, rules: LongPath).Action);

        Assert.AreEqual(StalemateAction.None, Quiet(ref s, 89, 8, 31, rules: LongPath).Action);
    }

    [TestMethod]
    public void ElevenAttackersAgainstTwoDefenders_InAWaveOf110_IsNoResult()
    {
        var s = new StalemateState();
        Quiet(ref s, 60, 11, 2);

        var d = Quiet(ref s, 90, 11, 2);

        Assert.AreEqual(StalemateAction.NoResult, d.Action);
        Assert.AreEqual(0, d.Count);
    }

    [TestMethod]
    public void MoreThanAStragglerShare_IsNoResultEvenAgainstManyDefenders()
    {
        var s = new StalemateState();
        Quiet(ref s, 60, 12, 40);

        Assert.AreEqual(StalemateAction.NoResult, Quiet(ref s, 90, 12, 40).Action);
    }

    [TestMethod]
    public void ExactlyAStragglerShare_StillCounts()
    {
        var s = new StalemateState();
        Quiet(ref s, 60, 11, 40);

        Assert.AreEqual(StalemateAction.FellStragglers, Quiet(ref s, 90, 11, 40).Action);
    }

    [TestMethod]
    public void NoAttackersLeft_NeverFellsOrEndsIt()
    {
        var s = new StalemateState();
        Quiet(ref s, 60, 0, 40);

        Assert.AreEqual(StalemateAction.None, Quiet(ref s, 200, 0, 40).Action);
    }

    [DataTestMethod]
    [DataRow(float.NaN)]
    [DataRow(0f)]
    [DataRow(-1f)]
    [DataRow(float.PositiveInfinity)]
    public void BadDt_ChangesNothing(float dt)
    {
        var s = new StalemateState();
        Quiet(ref s, 59, 55, 100);

        var d = StalemateRule.Step(ref s, dt, false, 55, 100, 110, R);

        Assert.AreEqual(StalemateAction.None, d.Action);
        Assert.AreEqual(StalemateAction.Charge, StalemateRule.Step(ref s, 1f, false, 55, 100, 110, R).Action);
    }

    [TestMethod]
    public void NaNStragglerShare_FailsSafeToNoResult_NeverFells()
    {
        var rules = R with { StragglerShare = float.NaN };
        var s = new StalemateState();
        for (var t = 0; t < 60; t++)
            StalemateRule.Step(ref s, 1f, false, 8, 31, 110, rules);
        StalemateDecision d = default;
        for (var t = 0; t < 90; t++)
            d = StalemateRule.Step(ref s, 1f, false, 8, 31, 110, rules);

        Assert.AreEqual(StalemateAction.NoResult, d.Action);
    }

    [TestMethod]
    public void NaNQuietSeconds_NeverCharges()
    {
        var rules = LongPath with { QuietSeconds = float.NaN, QuietAnySeconds = float.NaN };
        var s = new StalemateState();
        for (var t = 0; t < 400; t++)
            Assert.AreEqual(StalemateAction.None, StalemateRule.Step(ref s, 1f, false, 8, 31, 110, rules).Action, "tick " + t);
    }

    [TestMethod]
    public void NaNStrandedSeconds_NeverFellsAndNeverEnds()
    {
        var rules = LongPath with { StrandedSeconds = float.NaN };
        var s = new StalemateState { Charging = true };
        for (var t = 0; t < 400; t++)
            Assert.AreEqual(StalemateAction.None, StalemateRule.Step(ref s, 1f, false, 8, 31, 110, rules).Action, "tick " + t);
    }

    [TestMethod]
    public void NaNKeepDominance_HoldsTheBar()
    {
        var rules = R with { KeepDominance = float.NaN };

        Assert.AreEqual(0.5f, KeepHoldRule.Step(0.5f, 50, 5, 1f, rules));
        Assert.AreEqual(0.5f, KeepHoldRule.Step(0.5f, 5, 50, 1f, rules));
    }
}
