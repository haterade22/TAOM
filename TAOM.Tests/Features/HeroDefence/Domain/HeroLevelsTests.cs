using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.HeroDefence.Domain;

namespace TAOM.Tests.Features.HeroDefence.Domain;

[TestClass]
public class HeroLevelsTests
{
    private static readonly HeroLevels Levels = new();

    [TestMethod]
    public void PriceOf_LevelTwo_IsTheShareOfTheHeroPrice()
    {
        Assert.AreEqual(400, Levels.PriceOf(1000, 2));
    }

    [TestMethod]
    public void PriceOf_EachLaterLevel_GrowsByTheGrowthFactor()
    {
        Assert.AreEqual(520, Levels.PriceOf(1000, 3));
        Assert.AreEqual(676, Levels.PriceOf(1000, 4));
    }

    [TestMethod]
    public void PriceOf_TopLevel_MatchesThePlanFormula()
    {
        // price x 0.4 x 1.3^(L-2): 1000 x 0.4 x 1.3^18 = 44,982.0 after rounding.
        Assert.AreEqual(44982, Levels.PriceOf(1000, 20));
    }

    [TestMethod]
    [DataRow(1)]
    [DataRow(0)]
    [DataRow(-5)]
    [DataRow(21)]
    [DataRow(int.MaxValue)]
    public void PriceOf_LevelOutsideTheLadder_IsZero(int level)
    {
        Assert.AreEqual(0, Levels.PriceOf(1000, level));
    }

    [TestMethod]
    public void PriceOf_BadSettingOrPrice_IsOutOfReachNeverFree()
    {
        Assert.AreEqual(int.MaxValue, new HeroLevels { PriceShare = -0.4d }.PriceOf(1000, 2));
        Assert.AreEqual(int.MaxValue, new HeroLevels { PriceShare = 0d }.PriceOf(1000, 2));
        Assert.AreEqual(int.MaxValue, new HeroLevels { PriceShare = double.NaN }.PriceOf(1000, 2));
        Assert.AreEqual(int.MaxValue, new HeroLevels { PriceGrowth = double.NaN }.PriceOf(1000, 3));
        Assert.AreEqual(int.MaxValue, new HeroLevels { PriceGrowth = -1.3d }.PriceOf(1000, 3));
        Assert.AreEqual(int.MaxValue, Levels.PriceOf(-1000, 2));
        Assert.AreEqual(int.MaxValue, Levels.PriceOf(0, 2));
    }

    [TestMethod]
    public void PriceOf_PositivePriceBelowHalfAGoldPiece_CostsOne()
    {
        Assert.AreEqual(1, Levels.PriceOf(1, 2), "1 x 0.4 rounds to 0, and a level is never free");
    }

    [TestMethod]
    public void PriceOf_HugeHeroPrice_DoesNotOverflow()
    {
        Assert.AreEqual(int.MaxValue, Levels.PriceOf(int.MaxValue, 20));
    }

    [TestMethod]
    public void Factors_LevelOne_AreAllOne()
    {
        Assert.AreEqual(1f, Levels.HealthFactor(1), 1e-6f);
        Assert.AreEqual(1f, Levels.DamageFactor(1), 1e-6f);
        Assert.AreEqual(1f, Levels.AbilityFactor(1), 1e-6f);
        Assert.AreEqual(1f, Levels.CooldownFactor(1), 1e-6f);
    }

    [TestMethod]
    public void Factors_EachLevel_AddsItsShareOfTheLevelOneValue()
    {
        Assert.AreEqual(1.9f, Levels.HealthFactor(10), 1e-5f);
        Assert.AreEqual(1.72f, Levels.DamageFactor(10), 1e-5f);
        Assert.AreEqual(1.72f, Levels.AbilityFactor(10), 1e-5f);
        Assert.AreEqual(0.82f, Levels.CooldownFactor(10), 1e-5f);
    }

    [TestMethod]
    public void Factors_TopLevel_MatchThePlan()
    {
        Assert.AreEqual(2.9f, Levels.HealthFactor(20), 1e-5f);
        Assert.AreEqual(2.52f, Levels.DamageFactor(20), 1e-5f);
        Assert.AreEqual(0.62f, Levels.CooldownFactor(20), 1e-5f);
    }

    [TestMethod]
    public void Factors_LevelOutsideTheLadder_ClampToTheEnds()
    {
        Assert.AreEqual(1f, Levels.HealthFactor(0), 1e-6f);
        Assert.AreEqual(Levels.HealthFactor(20), Levels.HealthFactor(99), 1e-6f);
    }

    [TestMethod]
    public void CooldownFactor_SteepRate_NeverFallsBelowTheFloor()
    {
        var steep = new HeroLevels { CooldownPerLevel = 0.2f };

        Assert.AreEqual(HeroLevels.CooldownFloor, steep.CooldownFactor(20), 1e-6f);
    }

    [TestMethod]
    public void CooldownFactor_NaNRate_StaysAFiniteNumberAtTheFloor()
    {
        var bad = new HeroLevels { CooldownPerLevel = float.NaN };

        Assert.AreEqual(HeroLevels.CooldownFloor, bad.CooldownFactor(5), 1e-6f);
    }

    [TestMethod]
    [DataRow(0)]
    [DataRow(-3)]
    [DataRow(int.MinValue)]
    public void Factors_TopLevelBelowOne_AreLevelOneValues(int maxLevel)
    {
        var bad = new HeroLevels { MaxLevel = maxLevel };

        Assert.AreEqual(1f, bad.HealthFactor(1), 1e-6f);
        Assert.AreEqual(1f, bad.DamageFactor(5), 1e-6f);
        Assert.AreEqual(1f, bad.AbilityFactor(20), 1e-6f);
        Assert.AreEqual(1f, bad.CooldownFactor(20), 1e-6f);
    }
}
