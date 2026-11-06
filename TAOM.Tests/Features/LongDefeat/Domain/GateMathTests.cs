using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.LongDefeat.Domain;

namespace TAOM.Tests.Features.LongDefeat.Domain;

/// <summary>
/// The gate arithmetic of phase3-plan.md D17 to D18 and E9b. The reference below is a line-for-line copy of <c>DestructableComponent.CalculateNextDestructionLevel</c> (v1.5.6,
/// <c>DestructableComponent.cs:405-418</c>) with the string's length left out, because <c>CastleGate.OnCalculateDestructionStateIndex</c> returns the index unchanged under 200.
/// </summary>
[TestClass]
public class GateMathTests
{
    private static int Engine(float hitPoint, float maxHitPoint, int states)
    {
        var step = maxHitPoint / (float)states;
        var left = maxHitPoint;
        var index = 0;
        while (left - step >= hitPoint)
        {
            left -= step;
            index++;
        }

        return index;
    }

    [DataTestMethod]
    [DataRow(15000f, 4)]
    [DataRow(22500f, 4)]
    [DataRow(7500.5f, 3)]
    [DataRow(15000f, 1)]
    [DataRow(15000f, 30)]
    public void StateIndex_MatchesTheEnginesLoop_OverAGridOfHitPoints(float max, int states)
    {
        for (var i = 0; i <= 400; i++)
        {
            var hp = max * i / 400f;
            Assert.AreEqual(Engine(hp, max, states), GateMath.StateIndex(hp, max, states), $"hp {hp} of {max} in {states} states");
        }
    }

    [TestMethod]
    public void StateIndex_ReadsTheBoundaryAsTheEngineDoes()
    {
        // 11250 is exactly one step down from 15000 in 4 states: the loop's >= takes it.
        Assert.AreEqual(1, GateMath.StateIndex(11250f, 15000f, 4));
        Assert.AreEqual(0, GateMath.StateIndex(11250.5f, 15000f, 4));
        Assert.AreEqual(1, GateMath.StateIndex(11000f, 15000f, 4), "E9b: 11000 of 15000 is state 1");
        Assert.AreEqual(0, GateMath.StateIndex(18500f, 22500f, 4), "E9b: 18500 of 22500 is state 0");
    }

    [DataTestMethod]
    [DataRow(0f, 15000f, 4)]
    [DataRow(100f, 0f, 4)]
    [DataRow(100f, -5f, 4)]
    [DataRow(100f, 15000f, 0)]
    [DataRow(float.NaN, 15000f, 4)]
    [DataRow(100f, float.NaN, 4)]
    [DataRow(100f, float.PositiveInfinity, 4)]
    public void StateIndex_WithNoUsableNumbers_IsZeroInsteadOfLooping(float hp, float max, int states)
    {
        var index = GateMath.StateIndex(hp, max, states);

        Assert.IsTrue(index >= 0 && index <= states, "a destroyed or unreadable gate must still return at once");
    }

    [TestMethod]
    public void LevelledMax_IsTheVanillaMaximumTimesOnePlusThePercent()
    {
        Assert.AreEqual(22500f, GateMath.LevelledMax(15000f, 50f), 0.01f);
        Assert.AreEqual(30000f, GateMath.LevelledMax(15000f, 100f), 0.01f);
        Assert.AreEqual(15000f, GateMath.LevelledMax(15000f, 0f), 0.01f);
    }

    [TestMethod]
    public void KeepShare_TheNaiveWrite_WouldLowerTheIndex_TheSharedWriteNever()
    {
        // E9b: adding the same 7500 to the maximum and the hit points takes 11000 of 15000 (state 1) to 18500 of 22500 (state 0), and the next hit reads index -1.
        Assert.IsTrue(GateMath.StateIndex(11000f + 7500f, 22500f, 4) < GateMath.StateIndex(11000f, 15000f, 4));

        var kept = GateMath.KeepShare(11000f, 15000f, 22500f);

        Assert.IsTrue(GateMath.StateIndex(kept, 22500f, 4) >= GateMath.StateIndex(11000f, 15000f, 4));
    }

    [DataTestMethod]
    [DataRow(15000f, 22500f)]
    [DataRow(22500f, 30000f)]
    [DataRow(30000f, 37500f)]
    [DataRow(15000f, 37500f)]
    [DataRow(15000f, 15000.5f)]
    public void KeepShare_NeverLowersTheStateIndex_AtAnyHitPoints(float oldMax, float newMax)
    {
        for (var i = 1; i <= 2000; i++)
        {
            var hp = oldMax * i / 2000f;
            var written = GateMath.KeepShare(hp, oldMax, newMax);

            Assert.IsTrue(written > 0f && written <= newMax, $"hp {hp}: the write {written} must stay alive and inside the new maximum");
            Assert.IsTrue(GateMath.StateIndex(written, newMax, 4) >= GateMath.StateIndex(hp, oldMax, 4), $"hp {hp} of {oldMax} to {newMax}");
        }
    }

    [TestMethod]
    public void KeepShare_TakesOneHitPointOffOnlyAboveOne_AndNeverHealsTheMissingShare()
    {
        Assert.AreEqual(11000f * 1.5f - 1f, GateMath.KeepShare(11000f, 15000f, 22500f), 0.01f);
        Assert.AreEqual(1f * 1.5f, GateMath.KeepShare(1f, 15000f, 22500f), 0.0001f, "a gate on its last hit point keeps it, scaled, with no minus 1");
        Assert.IsTrue(GateMath.KeepShare(15000f, 15000f, 22500f) < 22500f, "a whole gate is one hit point under the new maximum: the level never heals");
    }

    [TestMethod]
    public void KeepShare_OnABrokenGate_StaysZero()
    {
        Assert.AreEqual(0f, GateMath.KeepShare(0f, 15000f, 22500f));
    }

    [DataTestMethod]
    [DataRow(200, 0f, 1)]
    [DataRow(200, 0.37f, 74)]
    [DataRow(200, 0.371f, 75)]
    [DataRow(200, 1f, 200)]
    [DataRow(160, 0.37f, 60)]
    [DataRow(160, 0.001f, 1)]
    public void RepairPrice_IsTheCeilOfThePriceTimesTheMissingShare_AtLeastOne(int price, float share, int expected)
    {
        Assert.AreEqual(expected, GateMath.RepairPrice(price, share));
    }

    [TestMethod]
    public void RepairPrice_ReadsAShareWithFloatNoiseAsItsDecimalValue()
    {
        // 0.37f is 0.3700000048, so a bare ceil(200 * share) would charge 75.
        Assert.AreEqual(74, GateMath.RepairPrice(200, 0.37f));
    }

    [TestMethod]
    public void RepairPrice_WithAPriceFactor_TakesTheFactoredPriceAsItsBase()
    {
        var factored = System.Math.Max(1, GoldFactors.Apply(200, 0.8f));

        Assert.AreEqual(160, factored);
        Assert.AreEqual(60, GateMath.RepairPrice(factored, 0.37f));
        Assert.AreEqual(160, GateMath.RepairPrice(factored, 1f), "a broken gate pays all of it");
    }

    [DataTestMethod]
    [DataRow(float.NaN)]
    [DataRow(-0.5f)]
    [DataRow(2f)]
    public void RepairPrice_ClampsAShareThatIsNoShare(float share)
    {
        var price = GateMath.RepairPrice(200, share);

        Assert.IsTrue(price >= 1 && price <= 200);
    }
}
