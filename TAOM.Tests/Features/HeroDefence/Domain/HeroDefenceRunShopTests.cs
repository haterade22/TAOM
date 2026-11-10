using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.FordsOfWar.Domain;
using TAOM.Features.HeroDefence.Domain;
using static TAOM.Tests.Features.HeroDefence.Domain.DefenceDriver;

namespace TAOM.Tests.Features.HeroDefence.Domain;

[TestClass]
public class HeroDefenceRunShopTests
{
    [TestMethod]
    public void New_StartsInThePauseWithTheRuleValues()
    {
        var run = new DefenceDriver().Run;

        Assert.AreEqual(DefencePhase.Pause, run.Phase);
        Assert.AreEqual(0, run.Wave);
        Assert.AreEqual(1000, run.Gold);
        Assert.AreEqual(100, run.Lives);
        Assert.AreEqual(90f, run.PauseLeft, 0.001f);
    }

    [TestMethod]
    public void TryBuyHero_InThePauseWithGold_TakesThePriceAndOwnsTheHero()
    {
        var run = new DefenceDriver().Run;

        Assert.AreEqual(BuyResult.Bought, run.TryBuyHero(Melee));

        Assert.AreEqual(500, run.Gold);
        Assert.AreEqual(DefenceHeroState.Owned, run.StateOf(Melee));
    }

    [TestMethod]
    public void TryBuyHero_UnknownHero_RefusesAndKeepsTheGold()
    {
        var run = new DefenceDriver().Run;

        Assert.AreEqual(BuyResult.UnknownHero, run.TryBuyHero("nobody"));

        Assert.AreEqual(1000, run.Gold);
    }

    [TestMethod]
    public void TryBuyHero_SecondCopy_Refuses()
    {
        var run = new DefenceDriver().Run;
        run.TryBuyHero(Melee);

        Assert.AreEqual(BuyResult.AlreadyOwned, run.TryBuyHero(Melee));

        Assert.AreEqual(500, run.Gold);
    }

    [TestMethod]
    public void TryBuyHero_TooDear_RefusesAndDoesNotOwn()
    {
        var run = new DefenceDriver().Run;

        Assert.AreEqual(BuyResult.NotEnoughGold, run.TryBuyHero(Costly));

        Assert.AreEqual(1000, run.Gold);
        Assert.AreEqual(DefenceHeroState.NotOwned, run.StateOf(Costly));
    }

    [TestMethod]
    public void TryBuyHero_DuringAWave_Refuses()
    {
        var driver = new DefenceDriver().InWave();

        Assert.AreEqual(BuyResult.WrongPhase, driver.Run.TryBuyHero(Bow));

        Assert.AreEqual(DefenceHeroState.NotOwned, driver.Run.StateOf(Bow));
    }

    [TestMethod]
    public void TryPlaceHero_FirstPlacement_OrdersASpawnAtThePost()
    {
        var driver = new DefenceDriver();
        driver.Run.TryBuyHero(Melee);

        Assert.AreEqual(PlaceResult.Placed, driver.Run.TryPlaceHero(Melee, OnRoad(120f)));

        var fx = driver.Tick();
        Assert.AreEqual(DefenceHeroState.Placed, driver.Run.StateOf(Melee));
        Assert.AreEqual(new HeroOrder(Melee, OnRoad(120f).Point), fx.HeroSpawns.Single());
        Assert.AreEqual(0, fx.HeroMoves.Count);
    }

    [TestMethod]
    public void TryPlaceHero_SecondPlacementOfTheSameHero_OrdersAFreeMove()
    {
        var driver = new DefenceDriver();
        driver.Own(Melee, 120f);
        driver.Tick();
        var goldBefore = driver.Run.Gold;

        Assert.AreEqual(PlaceResult.Placed, driver.Run.TryPlaceHero(Melee, OnRoad(200f)));

        var fx = driver.Tick();
        Assert.AreEqual(new HeroOrder(Melee, OnRoad(200f).Point), fx.HeroMoves.Single());
        Assert.AreEqual(0, fx.HeroSpawns.Count);
        Assert.AreEqual(goldBefore, driver.Run.Gold);
        Assert.AreEqual(OnRoad(200f).Point, driver.Run.PostOf(Melee));
    }

    [TestMethod]
    public void TryPlaceHero_NotOwned_Refuses()
    {
        var run = new DefenceDriver().Run;

        Assert.AreEqual(PlaceResult.NotOwned, run.TryPlaceHero(Melee, OnRoad(100f)));
    }

    [TestMethod]
    public void TryPlaceHero_UnknownHero_Refuses()
    {
        var run = new DefenceDriver().Run;

        Assert.AreEqual(PlaceResult.NotOwned, run.TryPlaceHero("nobody", OnRoad(100f)));
    }

    [TestMethod]
    public void TryPlaceHero_DuringAWave_Refuses()
    {
        var driver = new DefenceDriver().InWave();

        Assert.AreEqual(PlaceResult.WrongPhase, driver.Run.TryPlaceHero(Melee, OnRoad(200f)));

        Assert.AreEqual(OnRoad(100f).Point, driver.Run.PostOf(Melee));
    }

    [TestMethod]
    public void TryPlaceHero_ARuleRefuses_LeavesTheHeroUnplaced()
    {
        var run = new DefenceDriver().Run;
        run.TryBuyHero(Melee);

        Assert.AreEqual(PlaceResult.WrongGround, run.TryPlaceHero(Melee, OnPlateau(100f)));

        Assert.AreEqual(DefenceHeroState.Owned, run.StateOf(Melee));
    }

    [TestMethod]
    public void TryPlaceHero_FieldIsFull_RefusesANewHero()
    {
        var run = new DefenceDriver(new DefenceRules { MaxPlaced = 1 }).Run;
        run.TryBuyHero(Melee);
        run.TryBuyHero(MeleeB);
        run.TryPlaceHero(Melee, OnRoad(100f));

        Assert.AreEqual(PlaceResult.FieldFull, run.TryPlaceHero(MeleeB, OnRoad(200f)));
    }

    [TestMethod]
    public void TryPlaceHero_FieldIsFull_StillMovesAPlacedHero()
    {
        var run = new DefenceDriver(new DefenceRules { MaxPlaced = 1 }).Run;
        run.TryBuyHero(Melee);
        run.TryPlaceHero(Melee, OnRoad(100f));

        Assert.AreEqual(PlaceResult.Placed, run.TryPlaceHero(Melee, OnRoad(200f)));
    }

    [TestMethod]
    public void TryPlaceHero_OnItsOwnPost_IsNotTooCloseToItself()
    {
        var run = new DefenceDriver().Run;
        run.TryBuyHero(Melee);
        run.TryPlaceHero(Melee, OnRoad(100f));

        Assert.AreEqual(PlaceResult.Placed, run.TryPlaceHero(Melee, OnRoad(100.5f)));
    }

    [TestMethod]
    public void TryPlaceHero_NextToAnotherHero_Refuses()
    {
        var run = new DefenceDriver().Run;
        run.TryBuyHero(Melee);
        run.TryBuyHero(MeleeB);
        run.TryPlaceHero(Melee, OnRoad(100f));

        Assert.AreEqual(PlaceResult.TooCloseToPost, run.TryPlaceHero(MeleeB, OnRoad(101f)));
    }

    [TestMethod]
    public void TryStartWave_WithTimeLeft_PaysEarlyGoldAndStarts()
    {
        var driver = new DefenceDriver();
        driver.Own(MeleeB);
        var before = driver.Run.Gold;
        driver.Tick(40f);

        Assert.AreEqual(StartResult.Started, driver.Run.TryStartWave());

        Assert.AreEqual(DefencePhase.Wave, driver.Run.Phase);
        Assert.AreEqual(1, driver.Run.Wave);
        Assert.AreEqual(before + 90, driver.Run.Gold, "50 s left at 2 gold a second is 100, and the cap is 90");
    }

    [TestMethod]
    public void TryStartWave_LittleTimeLeft_PaysTheRate()
    {
        var driver = new DefenceDriver();
        driver.Own(MeleeB);
        var before = driver.Run.Gold;
        driver.Tick(80.5f);

        driver.Run.TryStartWave();

        Assert.AreEqual(before + 19, driver.Run.Gold, "9.5 s left at 2 gold a second is 19");
    }

    [TestMethod]
    public void TryStartWave_AFractionOfGold_RoundsDown()
    {
        var driver = new DefenceDriver();
        driver.Own(MeleeB);
        var before = driver.Run.Gold;
        driver.Tick(80.7f);

        driver.Run.TryStartWave();

        Assert.AreEqual(before + 18, driver.Run.Gold, "9.3 s left at 2 gold a second is 18.6");
    }

    [TestMethod]
    public void TryStartWave_NoHeroPlaced_RefusesAndStaysInThePause()
    {
        var driver = new DefenceDriver();
        driver.Run.TryBuyHero(Melee);

        Assert.AreEqual(StartResult.NoHeroPlaced, driver.Run.TryStartWave());

        Assert.AreEqual(DefencePhase.Pause, driver.Run.Phase);
    }

    [TestMethod]
    public void TryStartWave_DuringAWave_Refuses()
    {
        var driver = new DefenceDriver().InWave();
        var gold = driver.Run.Gold;

        Assert.AreEqual(StartResult.WrongPhase, driver.Run.TryStartWave());

        Assert.AreEqual(gold, driver.Run.Gold);
        Assert.AreEqual(1, driver.Run.Wave);
    }

    [TestMethod]
    public void TryStartWave_ReportsTheBriefBeforeTheFirstPack()
    {
        var driver = new DefenceDriver();
        driver.Own(Melee);
        driver.Run.TryStartWave();

        var fx = driver.Tick();

        Assert.AreEqual(1, fx.WaveBegan?.Wave);
        Assert.AreEqual(1, fx.CreepBatches.Count);
    }

    [TestMethod]
    public void PostOf_UnplacedHero_IsNull()
    {
        var run = new DefenceDriver().Run;
        run.TryBuyHero(Melee);

        Assert.IsNull(run.PostOf(Melee));
        Assert.IsNull(run.PostOf("nobody"));
    }
}
