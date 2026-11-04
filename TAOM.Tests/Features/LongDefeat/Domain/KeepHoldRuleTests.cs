using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.LongDefeat.Domain;

namespace TAOM.Tests.Features.LongDefeat.Domain;

[TestClass]
public class KeepHoldRuleTests
{
    private static readonly RunRules R = RunRules.Default;

    [TestMethod]
    public void AttackersDominant_FillTheBarOverKeepHoldSeconds()
    {
        Assert.AreEqual(0.5f, KeepHoldRule.Step(0f, 40, 10, 22.5f, R), 1e-4f);
    }

    [TestMethod]
    public void ReachingOne_ClampsThere()
    {
        Assert.AreEqual(1f, KeepHoldRule.Step(0.99f, 40, 10, 10f, R));
    }

    [TestMethod]
    public void ElevenUnopposed_NeverMovesTheBar()
    {
        var p = 0f;
        for (var i = 0; i < 1000; i++)
            p = KeepHoldRule.Step(p, 11, 0, 1f, R);

        Assert.AreEqual(0f, p);
    }

    [TestMethod]
    public void TwelveUnopposed_FillsIt()
    {
        Assert.IsTrue(KeepHoldRule.Step(0f, 12, 0, 1f, R) > 0f);
    }

    [TestMethod]
    public void NotTwiceTheDefenders_Holds()
    {
        Assert.AreEqual(0.4f, KeepHoldRule.Step(0.4f, 19, 10, 5f, R));
    }

    [TestMethod]
    public void DefendersDominant_DrainOverKeepDecaySeconds()
    {
        Assert.AreEqual(0.5f, KeepHoldRule.Step(1f, 5, 40, 45f, R), 1e-4f);
    }

    [TestMethod]
    public void DrainStopsAtZero()
    {
        Assert.AreEqual(0f, KeepHoldRule.Step(0.1f, 0, 40, 100f, R));
    }

    [TestMethod]
    public void NoDefenders_NoAttackers_Holds()
    {
        Assert.AreEqual(0.3f, KeepHoldRule.Step(0.3f, 0, 0, 5f, R));
    }

    [DataTestMethod]
    [DataRow(float.NaN)]
    [DataRow(0f)]
    [DataRow(-2f)]
    public void BadDt_ChangesNothing(float dt)
    {
        Assert.AreEqual(0.4f, KeepHoldRule.Step(0.4f, 40, 0, dt, R));
    }

    [TestMethod]
    public void NaNProgress_RestartsAtZero()
    {
        Assert.AreEqual(0f, KeepHoldRule.Step(float.NaN, 5, 5, 1f, R));
    }

    [TestMethod]
    public void NaNHoldSeconds_NeverFillsTheBar()
    {
        var rules = R with { KeepHoldSeconds = float.NaN };

        Assert.AreEqual(0.2f, KeepHoldRule.Step(0.2f, 40, 0, 1f, rules));
    }

    [TestMethod]
    public void NaNDecaySeconds_NeverDrainsTheBar()
    {
        var rules = R with { KeepDecaySeconds = float.NaN };

        Assert.AreEqual(0.2f, KeepHoldRule.Step(0.2f, 0, 40, 1f, rules));
    }
}
