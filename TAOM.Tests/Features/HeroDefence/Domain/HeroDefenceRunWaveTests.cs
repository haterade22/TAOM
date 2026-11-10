using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.FordsOfWar.Domain;
using TAOM.Features.HeroDefence.Domain;
using static TAOM.Tests.Features.HeroDefence.Domain.DefenceDriver;

namespace TAOM.Tests.Features.HeroDefence.Domain;

[TestClass]
public class HeroDefenceRunWaveTests
{
    private static FieldReading CreepAt(AgentKey key, float x, float y = 0f, float z = 0f) =>
        new(new[] { new UnitSample(key, new Vec3f(x, y, z)) });

    [TestMethod]
    public void Tick_PauseNotOver_StaysInThePause()
    {
        var driver = new DefenceDriver();

        driver.Tick(89f);

        Assert.AreEqual(DefencePhase.Pause, driver.Run.Phase);
        Assert.AreEqual(1f, driver.Run.PauseLeft, 0.001f);
    }

    [TestMethod]
    public void Tick_PauseRunsOut_StartsWaveOneAndReleasesItsFirstPack()
    {
        var driver = new DefenceDriver();

        var fx = driver.Tick(90f);

        Assert.AreEqual(DefencePhase.Wave, driver.Run.Phase);
        Assert.AreEqual(1, driver.Run.Wave);
        Assert.AreEqual(1, fx.WaveBegan?.Wave);
        Assert.AreEqual(1, fx.CreepBatches.Count);
    }

    [DataTestMethod]
    [DataRow(0f)]
    [DataRow(-1f)]
    [DataRow(float.NaN)]
    [DataRow(float.PositiveInfinity)]
    public void Tick_BadDt_ChangesNothing(float dt)
    {
        var driver = new DefenceDriver();

        driver.Tick(dt);

        Assert.AreEqual(90f, driver.Run.PauseLeft, 0.001f);
        Assert.AreEqual(DefencePhase.Pause, driver.Run.Phase);
    }

    [TestMethod]
    public void Tick_PauseEndsInsideATick_ChargesOnlyTheRestToTheWave()
    {
        var driver = new DefenceDriver();
        driver.Own(Melee);
        driver.Tick(90f);

        var fx = driver.Tick(185f);

        Assert.AreEqual(0, fx.Removals.Count, "the pause part of the first tick does not count toward the 186 s limit");
    }

    [TestMethod]
    public void Tick_PauseEndsInsideATick_TheRestCountsTowardTheWaveLimit()
    {
        var driver = new DefenceDriver();
        driver.Own(Melee);
        driver.Tick(100f);

        var fx = driver.Tick(176f);

        Assert.AreEqual(2, fx.Removals.Count, "10 s of the first tick and 176 s reach the 186 s limit");
    }

    [TestMethod]
    public void CreepSpawned_KeyAlreadyAlive_IsIgnored()
    {
        var driver = new DefenceDriver(null, new[] { 2, 2 }).InWave();
        driver.Run.CreepSpawned(driver.Live[0], Soldier(), 0);
        driver.End(CreepEnd.Killed);

        var fx = driver.Tick(6f);

        Assert.AreEqual(0, fx.CreepBatches.Single().Slot, "slot 0 is empty again, so the next pack takes it");
    }

    [TestMethod]
    public void Tick_FirstPack_TakesTheFirstSlot()
    {
        var driver = new DefenceDriver();
        driver.Own(Melee);
        driver.Run.TryStartWave();

        var fx = driver.Tick();

        Assert.AreEqual(0, fx.CreepBatches.Single().Slot);
    }

    [TestMethod]
    public void Tick_BeforeTheGap_HoldsTheNextPack()
    {
        var driver = new DefenceDriver(null, new[] { 2, 2 }).InWave();

        var fx = driver.Tick(5f);

        Assert.AreEqual(0, fx.CreepBatches.Count);
    }

    [TestMethod]
    public void Tick_AfterTheGap_ReleasesTheNextPackInTheNextSlot()
    {
        var driver = new DefenceDriver(null, new[] { 2, 2 }).InWave();

        var fx = driver.Tick(6f);

        Assert.AreEqual(1, fx.CreepBatches.Single().Slot);
    }

    [TestMethod]
    public void Tick_EverySlotHoldsAPack_TheNextPackWaits()
    {
        var driver = new DefenceDriver(null, Enumerable.Repeat(1, 9).ToArray()).InWave();
        for (var i = 0; i < 7; i++)
            Assert.AreEqual(1, driver.Tick(6f).CreepBatches.Count);

        Assert.AreEqual(0, driver.Tick(6f).CreepBatches.Count);
    }

    [TestMethod]
    public void Tick_ASlotEmpties_TheWaitingPackTakesIt()
    {
        var driver = new DefenceDriver(null, Enumerable.Repeat(1, 9).ToArray()).InWave();
        for (var i = 0; i < 7; i++)
            driver.Tick(6f);
        driver.Tick(6f);

        driver.Run.CreepGone(driver.Live[3], CreepEnd.Killed);
        var fx = driver.Tick(0.1f);

        Assert.AreEqual(3, fx.CreepBatches.Single().Slot);
    }

    [TestMethod]
    public void Tick_OneCreepOfAPackRemains_TheSlotStaysBusy()
    {
        var driver = new DefenceDriver(null, new[] { 2, 2, 2, 2, 2, 2, 2, 2, 2 }).InWave();
        for (var i = 0; i < 8; i++)
            driver.Tick(6f);

        driver.Run.CreepGone(driver.Live[0], CreepEnd.Killed);

        Assert.AreEqual(0, driver.Tick(0.1f).CreepBatches.Count);
    }

    [TestMethod]
    public void Tick_LastPackJustReleased_DoesNotClearInTheSameTick()
    {
        var driver = new DefenceDriver().InWave();

        Assert.AreEqual(DefencePhase.Wave, driver.Run.Phase);
        Assert.AreEqual(2, driver.Live.Count);
    }

    [TestMethod]
    public void Tick_PacksStillQueued_DoesNotClear()
    {
        var driver = new DefenceDriver(null, new[] { 1, 1 }).InWave();

        driver.End(CreepEnd.Killed);
        driver.Tick(0.1f);

        Assert.AreEqual(DefencePhase.Wave, driver.Run.Phase);
    }

    [TestMethod]
    public void Tick_LastCreepGone_ClearsTheWaveIntoTheLaterPause()
    {
        var driver = new DefenceDriver().InWave();

        driver.KillAllAndClear();

        Assert.AreEqual(DefencePhase.Pause, driver.Run.Phase);
        Assert.AreEqual(1, driver.Run.WavesCleared);
        Assert.AreEqual(45f, driver.Run.PauseLeft, 0.001f);
    }

    [TestMethod]
    public void Clear_NoLeaks_PaysTheFullBonus()
    {
        var driver = new DefenceDriver().InWave();
        var before = driver.Run.Gold;

        driver.End(CreepEnd.Culled);
        driver.Tick();

        Assert.AreEqual(before + 125, driver.Run.Gold, "100 + 25 for wave 1");
    }

    [TestMethod]
    public void Clear_HalfLeaked_PaysHalfTheBonus()
    {
        var driver = new DefenceDriver().InWave();
        var before = driver.Run.Gold;
        var keys = driver.Live.ToList();

        driver.Run.CreepGone(keys[0], CreepEnd.Leaked);
        driver.Run.CreepGone(keys[1], CreepEnd.Culled);
        driver.Tick();

        Assert.AreEqual(before + 63, driver.Run.Gold, "62.5 rounds away from zero");
    }

    [TestMethod]
    public void Clear_AllLeaked_PaysNothing()
    {
        var driver = new DefenceDriver().InWave();
        var before = driver.Run.Gold;

        driver.End(CreepEnd.Leaked);
        driver.Tick();

        Assert.AreEqual(before, driver.Run.Gold);
    }

    [TestMethod]
    public void Clear_LaterWave_PaysTheLargerBonus()
    {
        var driver = new DefenceDriver().InWave();
        driver.KillAllAndClear();
        driver.Run.TryStartWave();
        driver.Tick();
        driver.End(CreepEnd.Culled);
        var before = driver.Run.Gold;

        driver.Tick();

        Assert.AreEqual(before + 150, driver.Run.Gold, "100 + 25 x 2");
    }

    [TestMethod]
    public void Clear_AfterTheWaveLimit_UnreleasedCreepsDroppedForFreeDoNotDiluteTheLeakShare()
    {
        var driver = new DefenceDriver(null, Enumerable.Repeat(1, 9).ToArray()).InWave();
        driver.Run.CreepGone(driver.Live[0], CreepEnd.Leaked);
        var gold = driver.Run.Gold;

        driver.Tick(300f);
        driver.Tick();

        Assert.AreEqual(DefencePhase.Pause, driver.Run.Phase);
        Assert.AreEqual(gold, driver.Run.Gold, "the one creep that took the field leaked, so the share is 1 and the bonus 0");
    }

    [TestMethod]
    public void Clear_BonusPastTheGoldCeiling_StopsAtTheCeiling()
    {
        var driver = new DefenceDriver(new DefenceRules { WaveBonusBase = int.MaxValue }).InWave();

        driver.KillAllAndClear();

        Assert.AreEqual(int.MaxValue, driver.Run.Gold);
    }

    [TestMethod]
    public void CreepGone_BountyPastTheGoldCeiling_StopsAtTheCeiling()
    {
        var driver = new DefenceDriver(new DefenceRules { GeneralBounty = int.MaxValue }).InWave();
        var key = driver.AddCreep(Soldier(rank: CreepRank.General));

        driver.Run.CreepGone(key, CreepEnd.Killed);

        Assert.AreEqual(int.MaxValue, driver.Run.Gold);
    }

    [TestMethod]
    public void Clear_StandingHero_IsResetAtThePost()
    {
        var driver = new DefenceDriver().InWave();

        var fx = driver.KillAllAndClear();

        Assert.AreEqual(new HeroOrder(Melee, OnRoad(100f).Point, 1), fx.HeroResets.Single());
        Assert.AreEqual(0, fx.HeroSpawns.Count);
    }

    [TestMethod]
    public void Clear_FallenHero_ReturnsAtThePostAndKeepsThePurchase()
    {
        var driver = new DefenceDriver().InWave();
        driver.Run.HeroDown(Melee);
        var gold = driver.Run.Gold;

        driver.End(CreepEnd.Culled);
        var fx = driver.Tick();

        Assert.AreEqual(new HeroOrder(Melee, OnRoad(100f).Point, 1), fx.HeroSpawns.Single());
        Assert.AreEqual(0, fx.HeroResets.Count);
        Assert.AreEqual(DefenceHeroState.Placed, driver.Run.StateOf(Melee));
        Assert.AreEqual(gold + 125, driver.Run.Gold, "the return costs nothing");
    }

    [TestMethod]
    public void Clear_OwnedButUnplacedHero_GetsNoOrder()
    {
        var driver = new DefenceDriver();
        driver.Own(Melee);
        driver.Run.TryBuyHero(MeleeB);
        driver.Run.TryStartWave();
        driver.Tick();

        var fx = driver.KillAllAndClear();

        Assert.AreEqual(Melee, fx.HeroResets.Single().HeroId);
    }

    [TestMethod]
    public void HeroDown_DuringAWave_IsOutForTheWave()
    {
        var driver = new DefenceDriver().InWave();

        driver.Run.HeroDown(Melee);

        Assert.AreEqual(DefenceHeroState.Fallen, driver.Run.StateOf(Melee));
        Assert.AreEqual(OnRoad(100f).Point, driver.Run.PostOf(Melee));
    }

    [TestMethod]
    public void HeroDown_UnplacedOrUnknownHero_ChangesNothing()
    {
        var driver = new DefenceDriver();
        driver.Own(Melee);
        driver.Run.TryBuyHero(MeleeB);
        driver.Run.TryStartWave();
        driver.Tick();

        driver.Run.HeroDown(MeleeB);
        driver.Run.HeroDown("nobody");

        Assert.AreEqual(DefenceHeroState.Owned, driver.Run.StateOf(MeleeB));
        Assert.AreEqual(DefenceHeroState.NotOwned, driver.Run.StateOf("nobody"));
    }

    [TestMethod]
    public void HeroDown_DuringThePause_OrdersAReturnAtThePost()
    {
        var driver = new DefenceDriver();
        driver.Own(Melee);
        driver.Tick();

        driver.Run.HeroDown(Melee);

        Assert.AreEqual(new HeroOrder(Melee, OnRoad(100f).Point, 1), driver.Tick().HeroSpawns.Single());
        Assert.AreEqual(DefenceHeroState.Placed, driver.Run.StateOf(Melee));
    }

    [TestMethod]
    public void HeroDown_AfterTheRunEnded_ChangesNothing()
    {
        var driver = new DefenceDriver(new DefenceRules { StartingLives = 1 }).InWave();
        driver.Run.CreepGone(driver.Live[0], CreepEnd.Leaked);

        driver.Run.HeroDown(Melee);

        Assert.AreEqual(DefenceHeroState.Placed, driver.Run.StateOf(Melee));
    }

    [DataTestMethod]
    [DataRow(1, 2)]
    [DataRow(6, 4)]
    [DataRow(11, 5)]
    public void CreepGone_Killed_PaysTheBountyOfTheLevel(int level, int bounty)
    {
        var driver = new DefenceDriver().InWave();
        var key = driver.AddCreep(Soldier(level));
        var before = driver.Run.Gold;

        driver.Run.CreepGone(key, CreepEnd.Killed);

        Assert.AreEqual(before + bounty, driver.Run.Gold);
        Assert.AreEqual(1, driver.Run.Kills);
    }

    [TestMethod]
    public void CreepGone_MountedKilled_PaysHalfAgainBeforeRounding()
    {
        var driver = new DefenceDriver().InWave();
        var key = driver.AddCreep(Soldier(6, mounted: true));
        var before = driver.Run.Gold;

        driver.Run.CreepGone(key, CreepEnd.Killed);

        Assert.AreEqual(before + 5, driver.Run.Gold, "3.5 x 1.5 = 5.25");
    }

    [DataTestMethod]
    [DataRow((int)CreepRank.Captain, 100)]
    [DataRow((int)CreepRank.Lieutenant, 200)]
    [DataRow((int)CreepRank.General, 350)]
    public void CreepGone_LeaderKilled_PaysTheLeaderBounty(int rank, int bounty)
    {
        var driver = new DefenceDriver().InWave();
        var key = driver.AddCreep(Soldier(30, mounted: true, (CreepRank)rank));
        var before = driver.Run.Gold;

        driver.Run.CreepGone(key, CreepEnd.Killed);

        Assert.AreEqual(before + bounty, driver.Run.Gold);
    }

    [DataTestMethod]
    [DataRow((int)CreepRank.Soldier, 1)]
    [DataRow((int)CreepRank.Captain, 5)]
    [DataRow((int)CreepRank.Lieutenant, 10)]
    [DataRow((int)CreepRank.General, 20)]
    public void CreepGone_Leaked_CostsTheLivesOfTheRankAndOrdersARemoval(int rank, int cost)
    {
        var driver = new DefenceDriver().InWave();
        var key = driver.AddCreep(Soldier(rank: (CreepRank)rank));

        driver.Run.CreepGone(key, CreepEnd.Leaked);

        Assert.AreEqual(100 - cost, driver.Run.Lives);
        CollectionAssert.Contains(driver.Tick().Removals, key);
    }

    [TestMethod]
    public void CreepGone_Culled_CostsNothingPaysNothingAndOrdersARemoval()
    {
        var driver = new DefenceDriver().InWave();
        var key = driver.AddCreep(Soldier());
        var gold = driver.Run.Gold;

        driver.Run.CreepGone(key, CreepEnd.Culled);

        Assert.AreEqual(gold, driver.Run.Gold);
        Assert.AreEqual(100, driver.Run.Lives);
        Assert.AreEqual(0, driver.Run.Kills);
        CollectionAssert.Contains(driver.Tick().Removals, key);
    }

    [TestMethod]
    public void CreepGone_Fled_CostsNothingAndOrdersNoRemoval()
    {
        var driver = new DefenceDriver().InWave();
        var key = driver.AddCreep(Soldier());

        driver.Run.CreepGone(key, CreepEnd.Fled);

        Assert.AreEqual(100, driver.Run.Lives);
        Assert.AreEqual(0, driver.Tick().Removals.Count);
    }

    [TestMethod]
    public void CreepGone_SameKeyTwice_CountsOnce()
    {
        var driver = new DefenceDriver().InWave();
        var key = driver.AddCreep(Soldier());

        driver.Run.CreepGone(key, CreepEnd.Killed);
        driver.Run.CreepGone(key, CreepEnd.Leaked);

        Assert.AreEqual(1, driver.Run.Kills);
        Assert.AreEqual(100, driver.Run.Lives);
    }

    [TestMethod]
    public void CreepGone_UnknownKey_ChangesNothing()
    {
        var driver = new DefenceDriver().InWave();

        driver.Run.CreepGone(new AgentKey(9999), CreepEnd.Leaked);

        Assert.AreEqual(100, driver.Run.Lives);
    }

    [TestMethod]
    public void CreepGone_LastLife_EndsTheRunWithTheScore()
    {
        var driver = new DefenceDriver(new DefenceRules { StartingLives = 1 }).InWave();
        var keys = driver.Live.ToList();
        driver.Run.CreepGone(keys[0], CreepEnd.Killed);

        driver.Run.CreepGone(keys[1], CreepEnd.Leaked);

        Assert.AreEqual(DefencePhase.Ended, driver.Run.Phase);
        Assert.AreEqual(0, driver.Run.Lives);
        Assert.AreEqual(new DefenceResult(0, 1), driver.Run.Result);
    }

    [TestMethod]
    public void CreepGone_CostAboveTheLivesLeft_StopsAtZero()
    {
        var driver = new DefenceDriver(new DefenceRules { StartingLives = 3 }).InWave();
        var key = driver.AddCreep(Soldier(rank: CreepRank.General));

        driver.Run.CreepGone(key, CreepEnd.Leaked);

        Assert.AreEqual(0, driver.Run.Lives);
    }

    [TestMethod]
    public void CreepGone_AfterTheRunEnded_IsIgnored()
    {
        var driver = new DefenceDriver(new DefenceRules { StartingLives = 1 }).InWave();
        var keys = driver.Live.ToList();
        driver.Run.CreepGone(keys[0], CreepEnd.Leaked);
        var gold = driver.Run.Gold;

        driver.Run.CreepGone(keys[1], CreepEnd.Killed);

        Assert.AreEqual(0, driver.Run.Kills);
        Assert.AreEqual(gold, driver.Run.Gold);
    }

    [TestMethod]
    public void Tick_RunEnded_DeliversTheLastRemovalAndAdvancesNothing()
    {
        var driver = new DefenceDriver(new DefenceRules { StartingLives = 1 }).InWave();
        var key = driver.Live[0];
        driver.Run.CreepGone(key, CreepEnd.Leaked);

        var fx = driver.Tick();

        CollectionAssert.Contains(fx.Removals, key);
        Assert.AreEqual(DefencePhase.Ended, driver.Run.Phase);
        Assert.AreEqual(0, driver.Run.WavesCleared);
    }

    [TestMethod]
    public void Tick_CreepInsideTheExitRadius_LeaksAndCostsALife()
    {
        var driver = new DefenceDriver().InWave();
        var key = driver.Live[0];

        var fx = driver.Tick(0.1f, CreepAt(key, 493f));

        Assert.AreEqual(99, driver.Run.Lives);
        CollectionAssert.Contains(fx.Removals, key);
    }

    [TestMethod]
    public void Tick_CreepOutsideTheExitRadius_Marches()
    {
        var driver = new DefenceDriver().InWave();

        driver.Tick(0.1f, CreepAt(driver.Live[0], 491f));

        Assert.AreEqual(100, driver.Run.Lives);
    }

    [TestMethod]
    public void Tick_ExitRadiusIgnoresHeight()
    {
        var driver = new DefenceDriver().InWave();

        driver.Tick(0.1f, CreepAt(driver.Live[0], 495f, 0f, 40f));

        Assert.AreEqual(99, driver.Run.Lives);
    }

    [DataTestMethod]
    [DataRow(float.NaN)]
    [DataRow(float.PositiveInfinity)]
    public void Tick_BadCreepPosition_DoesNotLeak(float x)
    {
        var driver = new DefenceDriver().InWave();

        driver.Tick(0.1f, CreepAt(driver.Live[0], x));

        Assert.AreEqual(100, driver.Run.Lives);
    }

    [TestMethod]
    public void Tick_FieldReadInThePause_LeaksNothing()
    {
        var driver = new DefenceDriver();

        driver.Tick(0.1f, CreepAt(new AgentKey(1), 500f));

        Assert.AreEqual(100, driver.Run.Lives);
    }

    [TestMethod]
    public void Tick_WaveLimit_WithAHeroStanding_RemovesTheCreepsForFree()
    {
        var driver = new DefenceDriver().InWave();
        var keys = driver.Live.ToList();

        var fx = driver.Tick(186f);

        Assert.AreEqual(100, driver.Run.Lives);
        CollectionAssert.AreEquivalent(keys, fx.Removals);
    }

    [TestMethod]
    public void Tick_BeforeTheWaveLimit_KeepsTheCreeps()
    {
        var driver = new DefenceDriver().InWave();

        var fx = driver.Tick(185f);

        Assert.AreEqual(0, fx.Removals.Count, "the limit is 180 + 3 x 2 creeps");
    }

    [TestMethod]
    public void Tick_WaveLimit_NoHeroStanding_CountsEveryCreepAsALeak()
    {
        var driver = new DefenceDriver().InWave();
        driver.Run.HeroDown(Melee);

        driver.Tick(186f);

        Assert.AreEqual(98, driver.Run.Lives);
    }

    [TestMethod]
    public void Tick_WaveLimit_NoHeroStanding_CountsUnreleasedCreepsAsLeaks()
    {
        var driver = new DefenceDriver(null, Enumerable.Repeat(1, 9).ToArray()).InWave();
        driver.Run.HeroDown(Melee);

        driver.Tick(300f);

        Assert.AreEqual(91, driver.Run.Lives, "1 standing and 8 unreleased");
    }

    [TestMethod]
    public void Tick_WaveLimit_WithAHeroStanding_DropsUnreleasedCreepsForFree()
    {
        var driver = new DefenceDriver(null, Enumerable.Repeat(1, 9).ToArray()).InWave();

        driver.Tick(300f);

        Assert.AreEqual(100, driver.Run.Lives);
    }

    [TestMethod]
    public void Tick_AfterTheWaveLimit_ClearsTheWave()
    {
        var driver = new DefenceDriver().InWave();
        driver.Tick(186f);

        driver.Tick();

        Assert.AreEqual(DefencePhase.Pause, driver.Run.Phase);
        Assert.AreEqual(1, driver.Run.WavesCleared);
    }

    [TestMethod]
    public void Tick_WaveLimitTakesTheLastLife_StillRemovesEveryCreep()
    {
        var driver = new DefenceDriver(new DefenceRules { StartingLives = 1 }, new[] { 3 }).InWave();
        driver.Run.HeroDown(Melee);

        var fx = driver.Tick(190f);

        Assert.AreEqual(DefencePhase.Ended, driver.Run.Phase, "the limit is 180 + 3 x 3 creeps");
        Assert.AreEqual(3, fx.Removals.Count, "the run ends on the first leak, and the other two still leave the field");
        Assert.AreEqual(0, driver.Live.Count);
    }

    [TestMethod]
    public void Tick_TwoLeaksInOneReadingTakeTheLastLife_BothLeaveTheField()
    {
        var driver = new DefenceDriver(new DefenceRules { StartingLives = 1 }).InWave();
        var atExit = new FieldReading(driver.Live.Select(key => new UnitSample(key, Exit)).ToList());

        var fx = driver.Tick(0.1f, atExit);

        Assert.AreEqual(DefencePhase.Ended, driver.Run.Phase);
        Assert.AreEqual(2, fx.Removals.Count, "the first leak ends the run, and the second creep at the exit still leaves");
        Assert.AreEqual(0, driver.Live.Count);
    }

    [TestMethod]
    public void Tick_WaveLimitTakesTheLastLife_EndsWithoutClearing()
    {
        var driver = new DefenceDriver(new DefenceRules { StartingLives = 1 }).InWave();
        driver.Run.HeroDown(Melee);

        driver.Tick(186f);
        driver.Tick();

        Assert.AreEqual(DefencePhase.Ended, driver.Run.Phase);
        Assert.AreEqual(0, driver.Run.WavesCleared);
    }
}
