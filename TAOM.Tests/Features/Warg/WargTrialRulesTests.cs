using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.Warg;

namespace TAOM.Tests.Features.Warg;

/// <summary>The pass rule and the facts line of the warg gait step of the command trial (#1563).</summary>
[TestClass]
public class WargTrialRulesTests
{
    [TestMethod]
    public void Passes_WhenWargsRanFast()
    {
        Assert.IsTrue(WargTrialRules.Passes(riders: 12, top: 9f));
    }

    [TestMethod]
    public void Passes_AtTheLimitItself()
    {
        Assert.IsTrue(WargTrialRules.Passes(riders: 1, top: WargTrialRules.TopAtLeastMetresPerSecond));
    }

    [TestMethod]
    public void Fails_WhenNoWargRiderWasSeen()
    {
        Assert.IsFalse(WargTrialRules.Passes(riders: 0, top: 9f));
    }

    [TestMethod]
    public void Fails_WhenTheWargsNeverGotAboveAWalk()
    {
        Assert.IsFalse(WargTrialRules.Passes(riders: 12, top: WargTrialRules.TopAtLeastMetresPerSecond - 0.1f));
    }

    [TestMethod]
    public void Fails_WhenTheTopSpeedIsNoNumber()
    {
        Assert.IsFalse(WargTrialRules.Passes(riders: 12, top: float.NaN));
    }

    [TestMethod]
    public void Facts_NameEveryNumberAPersonNeeds()
    {
        var facts = WargTrialRules.Facts(12, 11, 0.04f, 9.26f, 13.04f, 13.5f, 1.1f);
        Assert.AreEqual("riders=12 mounted=11 stand=0.0 top=9.3 limit=13.0 horse=13.5 scale=1.10", facts);
    }
}
