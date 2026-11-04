using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.LongDefeat.Domain;

namespace TAOM.Tests.Features.LongDefeat.Domain;

/// <summary>The spike's numbers: 60 s quiet with half the wave down, 180 s at any count, 90 s after the charge.</summary>
[TestClass]
public class StalemateRuleTests
{
    private static readonly RunRules R = RunRules.Default;

    /// <summary>Ticks one second at a time and returns the first decision that is not None.</summary>
    private static StalemateDecision Quiet(ref StalemateState s, float seconds, int attackers, int defenders, int wave = 110)
    {
        for (var t = 0f; t < seconds; t += 1f)
        {
            var d = StalemateRule.Step(ref s, 1f, false, attackers, defenders, wave, R);
            if (d.Action != StalemateAction.None)
                return d;
        }

        return default;
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
        Quiet(ref s, 60, 8, 31);

        Assert.AreEqual(StalemateAction.None, Quiet(ref s, 89, 8, 31).Action);
        var d = StalemateRule.Step(ref s, 1f, false, 8, 31, 110, R);

        Assert.AreEqual(StalemateAction.FellStragglers, d.Action);
        Assert.AreEqual(8, d.Count);
    }

    [TestMethod]
    public void AfterTheFell_TheQuietRestarts()
    {
        var s = new StalemateState();
        Quiet(ref s, 60, 8, 31);
        Assert.AreEqual(StalemateAction.FellStragglers, Quiet(ref s, 90, 8, 31).Action);

        Assert.AreEqual(StalemateAction.None, Quiet(ref s, 89, 8, 31).Action);
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
        var rules = R with { QuietSeconds = float.NaN, QuietAnySeconds = float.NaN };
        var s = new StalemateState();
        for (var t = 0; t < 400; t++)
            Assert.AreEqual(StalemateAction.None, StalemateRule.Step(ref s, 1f, false, 8, 31, 110, rules).Action, "tick " + t);
    }

    [TestMethod]
    public void NaNStrandedSeconds_NeverFellsAndNeverEnds()
    {
        var rules = R with { StrandedSeconds = float.NaN };
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
