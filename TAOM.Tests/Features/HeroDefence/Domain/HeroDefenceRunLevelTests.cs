using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.HeroDefence.Domain;
using static TAOM.Tests.Features.HeroDefence.Domain.DefenceDriver;

namespace TAOM.Tests.Features.HeroDefence.Domain;

[TestClass]
public class HeroDefenceRunLevelTests
{
    [TestMethod]
    public void LevelOf_BoughtHero_IsOneAndAnUnboughtHeroIsZero()
    {
        var driver = new DefenceDriver();
        driver.Run.TryBuyHero(Melee);

        Assert.AreEqual(1, driver.Run.LevelOf(Melee));
        Assert.AreEqual(0, driver.Run.LevelOf(MeleeB));
        Assert.AreEqual(0, driver.Run.LevelOf("nobody"));
    }

    [TestMethod]
    public void TryLevelHero_InThePause_ChargesTheLevelPriceAndRaisesTheLevel()
    {
        var driver = new DefenceDriver();
        driver.Run.TryBuyHero(Melee);

        var result = driver.Run.TryLevelHero(Melee);

        Assert.AreEqual(LevelResult.Levelled, result);
        Assert.AreEqual(2, driver.Run.LevelOf(Melee));
        Assert.AreEqual(1000 - 500 - 200, driver.Run.Gold, "level 2 costs 500 x 0.4");
    }

    [TestMethod]
    public void TryLevelHero_TwiceInARow_PaysTheGrownPrice()
    {
        var driver = new DefenceDriver(new DefenceRules { StartingGold = 5000 });
        driver.Run.TryBuyHero(Melee);

        driver.Run.TryLevelHero(Melee);
        driver.Run.TryLevelHero(Melee);

        Assert.AreEqual(3, driver.Run.LevelOf(Melee));
        Assert.AreEqual(5000 - 500 - 200 - 260, driver.Run.Gold, "level 3 costs 200 x 1.3");
    }

    [TestMethod]
    public void TryLevelHero_DuringAWave_IsRefused()
    {
        var driver = new DefenceDriver().InWave();
        var gold = driver.Run.Gold;

        Assert.AreEqual(LevelResult.WrongPhase, driver.Run.TryLevelHero(Melee));
        Assert.AreEqual(1, driver.Run.LevelOf(Melee));
        Assert.AreEqual(gold, driver.Run.Gold);
    }

    [TestMethod]
    public void TryLevelHero_UnknownOrUnboughtHero_IsRefused()
    {
        var driver = new DefenceDriver();

        Assert.AreEqual(LevelResult.UnknownHero, driver.Run.TryLevelHero("nobody"));
        Assert.AreEqual(LevelResult.NotOwned, driver.Run.TryLevelHero(Melee));
        Assert.AreEqual(1000, driver.Run.Gold);
    }

    [TestMethod]
    public void TryLevelHero_NotEnoughGold_ChangesNothing()
    {
        var driver = new DefenceDriver(new DefenceRules { StartingGold = 650 });
        driver.Run.TryBuyHero(Melee);

        Assert.AreEqual(LevelResult.NotEnoughGold, driver.Run.TryLevelHero(Melee));
        Assert.AreEqual(1, driver.Run.LevelOf(Melee));
        Assert.AreEqual(150, driver.Run.Gold);
    }

    [TestMethod]
    public void TryLevelHero_AtTheTopLevel_IsRefused()
    {
        var driver = DefenceDriver.WithLevels(new HeroLevels { MaxLevel = 2 });
        driver.Run.TryBuyHero(Melee);
        driver.Run.TryLevelHero(Melee);
        var gold = driver.Run.Gold;

        Assert.AreEqual(LevelResult.TopLevel, driver.Run.TryLevelHero(Melee));
        Assert.AreEqual(2, driver.Run.LevelOf(Melee));
        Assert.AreEqual(gold, driver.Run.Gold);
    }

    [TestMethod]
    [DataRow(-1d)]
    [DataRow(0d)]
    [DataRow(double.NaN)]
    public void TryLevelHero_BadPriceSetting_IsRefusedAndNeverFree(double priceShare)
    {
        var driver = DefenceDriver.WithLevels(new HeroLevels { PriceShare = priceShare });
        driver.Own(Melee);
        driver.Tick();
        var gold = driver.Run.Gold;

        Assert.AreEqual(LevelResult.NotEnoughGold, driver.Run.TryLevelHero(Melee));
        Assert.AreEqual(1, driver.Run.LevelOf(Melee));
        Assert.AreEqual(gold, driver.Run.Gold);
        var fx = driver.Tick();
        Assert.AreEqual(0, fx.HeroResets.Count + fx.HeroSpawns.Count + fx.HeroMoves.Count, "a refused level orders nothing");
    }

    [TestMethod]
    public void TryLevelHero_BadPriceSettingWithAFullPurse_IsStillRefused()
    {
        var driver = DefenceDriver.WithLevels(new HeroLevels { PriceShare = double.NaN }, new DefenceRules { WaveBonusBase = int.MaxValue });
        driver.InWave();
        driver.KillAllAndClear();
        Assert.AreEqual(int.MaxValue, driver.Run.Gold, "the clear bonus fills the purse to its cap");

        Assert.AreEqual(LevelResult.NotEnoughGold, driver.Run.TryLevelHero(Melee));
        Assert.AreEqual(1, driver.Run.LevelOf(Melee));
        Assert.AreEqual(int.MaxValue, driver.Run.Gold);
    }

    [TestMethod]
    public void TryLevelHero_PlacedHero_OrdersAResetAtItsPostWithTheNewLevel()
    {
        var driver = new DefenceDriver();
        driver.Own(MeleeB);
        driver.Tick();

        driver.Run.TryLevelHero(MeleeB);

        var fx = driver.Tick();
        Assert.AreEqual(new HeroOrder(MeleeB, OnRoad(100f).Point, 2), fx.HeroResets.Single());
        Assert.AreEqual(0, fx.HeroSpawns.Count);
        Assert.AreEqual(0, fx.HeroMoves.Count);
    }

    [TestMethod]
    public void TryLevelHero_UnplacedHero_OrdersNothing()
    {
        var driver = new DefenceDriver();
        driver.Run.TryBuyHero(MeleeB);

        Assert.AreEqual(LevelResult.Levelled, driver.Run.TryLevelHero(MeleeB));

        var fx = driver.Tick();
        Assert.AreEqual(2, driver.Run.LevelOf(MeleeB));
        Assert.AreEqual(1000 - 200 - 80, driver.Run.Gold);
        Assert.AreEqual(0, fx.HeroResets.Count);
        Assert.AreEqual(0, fx.HeroSpawns.Count);
        Assert.AreEqual(0, fx.HeroMoves.Count);
    }

    [TestMethod]
    public void HeroDown_AfterReadyBeforeTheNextTick_DropsTheHerosPendingOrder()
    {
        var driver = new DefenceDriver();
        driver.Own(MeleeB);
        driver.Tick();
        driver.Run.TryLevelHero(MeleeB);
        driver.Run.TryPlaceHero(MeleeB, OnRoad(200f));
        driver.Run.TryStartWave();

        driver.Run.HeroDown(MeleeB);

        var fx = driver.Tick();
        Assert.AreEqual(DefenceHeroState.Fallen, driver.Run.StateOf(MeleeB));
        Assert.AreEqual(0, fx.HeroResets.Count + fx.HeroSpawns.Count + fx.HeroMoves.Count, "a fallen hero waits for the clear");
    }

    [TestMethod]
    public void TryLevelHero_AfterTheFirstPlacementInTheSameTick_OrdersOneSpawnAtTheNewLevel()
    {
        var driver = new DefenceDriver();
        driver.Own(MeleeB);

        driver.Run.TryLevelHero(MeleeB);

        var fx = driver.Tick();
        Assert.AreEqual(new HeroOrder(MeleeB, OnRoad(100f).Point, 2), fx.HeroSpawns.Single());
        Assert.AreEqual(0, fx.HeroResets.Count, "the spawn already carries the new level");
    }

    [TestMethod]
    public void TryLevelHero_ThenAMoveInTheSameTick_OrdersOneResetAtTheNewPost()
    {
        var driver = new DefenceDriver();
        driver.Own(MeleeB);
        driver.Tick();

        driver.Run.TryLevelHero(MeleeB);
        driver.Run.TryPlaceHero(MeleeB, OnRoad(200f));

        var fx = driver.Tick();
        Assert.AreEqual(new HeroOrder(MeleeB, OnRoad(200f).Point, 2), fx.HeroResets.Single());
        Assert.AreEqual(0, fx.HeroMoves.Count, "the reset already puts the hero at the new post");
    }

    [TestMethod]
    public void TryLevelHero_AfterAMoveInTheSameTick_OrdersOneResetAtTheNewPost()
    {
        var driver = new DefenceDriver();
        driver.Own(MeleeB);
        driver.Tick();

        driver.Run.TryPlaceHero(MeleeB, OnRoad(200f));
        driver.Run.TryLevelHero(MeleeB);

        var fx = driver.Tick();
        Assert.AreEqual(new HeroOrder(MeleeB, OnRoad(200f).Point, 2), fx.HeroResets.Single());
        Assert.AreEqual(0, fx.HeroMoves.Count);
    }

    [TestMethod]
    public void TryLevelHero_SeveralTimesInOneTick_OrdersOneResetAtTheLastLevel()
    {
        var driver = new DefenceDriver();
        driver.Own(MeleeB);
        driver.Tick();

        driver.Run.TryLevelHero(MeleeB);
        driver.Run.TryLevelHero(MeleeB);
        driver.Run.TryLevelHero(MeleeB);

        Assert.AreEqual(new HeroOrder(MeleeB, OnRoad(100f).Point, 4), driver.Tick().HeroResets.Single());
    }

    [TestMethod]
    public void HeroDown_InThePauseAfterALevel_OrdersOneSpawnAtTheNewLevel()
    {
        var driver = new DefenceDriver();
        driver.Own(MeleeB);
        driver.Tick();
        driver.Run.TryLevelHero(MeleeB);

        driver.Run.HeroDown(MeleeB);

        var fx = driver.Tick();
        Assert.AreEqual(new HeroOrder(MeleeB, OnRoad(100f).Point, 2), fx.HeroSpawns.Single());
        Assert.AreEqual(0, fx.HeroResets.Count, "the hero left the field, so the spawn replaces the reset");
    }

    [TestMethod]
    public void Clear_FallenHero_ReturnsAtItsLevel()
    {
        var driver = new DefenceDriver(new DefenceRules { StartingGold = 5000 });
        driver.Own(Melee);
        driver.Run.TryLevelHero(Melee);
        driver.Run.TryStartWave();
        driver.Tick();
        driver.Run.HeroDown(Melee);

        var fx = driver.KillAllAndClear();

        Assert.AreEqual(new HeroOrder(Melee, OnRoad(100f).Point, 2), fx.HeroSpawns.Single());
        Assert.AreEqual(0, fx.HeroResets.Count + fx.HeroMoves.Count);
        Assert.AreEqual(2, driver.Run.LevelOf(Melee));
    }
}
