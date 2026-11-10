using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.HeroDefence.Domain;
using static TAOM.Tests.Features.HeroDefence.Domain.DefenceDriver;

namespace TAOM.Tests.Features.HeroDefence.Domain;

[TestClass]
public class HeroDefenceRunStallTests
{
    [TestMethod]
    public void Release_EachBatch_GetsItsOwnPackId()
    {
        var driver = new DefenceDriver(null, new[] { 2, 3 }).InWave();
        driver.Tick(6f);

        Assert.AreEqual(2, driver.Batches.Count);
        Assert.AreNotEqual(driver.Batches[0].PackId, driver.Batches[1].PackId);
    }

    [TestMethod]
    public void PackStalled_WithAHeroStanding_RemovesThePackForFree()
    {
        var driver = new DefenceDriver(null, new[] { 3 }).InWave();
        var lives = driver.Run.Lives;
        var gold = driver.Run.Gold;

        driver.Run.PackStalled(driver.Batches[0].PackId);
        var fx = driver.Tick();

        Assert.AreEqual(3, fx.Removals.Count);
        Assert.AreEqual(lives, driver.Run.Lives);
        Assert.AreEqual(0, driver.Run.Kills);
        Assert.AreEqual(DefencePhase.Pause, driver.Run.Phase, "the only pack is gone, so the wave clears");
        Assert.AreEqual(gold + 100 + 25, driver.Run.Gold, "a cull is not a leak, so the clear bonus is whole");
    }

    [TestMethod]
    public void PackStalled_WithNoHeroStanding_LeaksThePack()
    {
        var driver = new DefenceDriver(null, new[] { 3 }).InWave();
        driver.Run.HeroDown(Melee);
        var lives = driver.Run.Lives;

        driver.Run.PackStalled(driver.Batches[0].PackId);

        Assert.AreEqual(lives - 3 * new DefenceRules().SoldierLifeCost, driver.Run.Lives);
        Assert.AreEqual(3, driver.Tick().Removals.Count);
    }

    [TestMethod]
    public void PackStalled_OnTheLastLife_StillRemovesTheWholePack()
    {
        var driver = new DefenceDriver(new DefenceRules { StartingLives = 1 }, new[] { 3 }).InWave();
        driver.Run.HeroDown(Melee);

        driver.Run.PackStalled(driver.Batches[0].PackId);
        var fx = driver.Tick();

        Assert.AreEqual(DefencePhase.Ended, driver.Run.Phase);
        Assert.AreEqual(3, fx.Removals.Count, "the run ends on the first leak, and the other two still leave the field");
        Assert.AreEqual(0, driver.Live.Count);
    }

    [TestMethod]
    public void PackStalled_RemovesOnlyThatPack()
    {
        var driver = new DefenceDriver(null, new[] { 2, 3 }).InWave();
        driver.Tick(6f);

        driver.Run.PackStalled(driver.Batches[0].PackId);
        var fx = driver.Tick();

        Assert.AreEqual(2, fx.Removals.Count);
        Assert.AreEqual(3, driver.Live.Count);
        Assert.AreEqual(DefencePhase.Wave, driver.Run.Phase);
    }

    [TestMethod]
    public void PackStalled_LateReportAfterTheSlotIsReused_RemovesNothing()
    {
        var driver = new DefenceDriver(null, new[] { 2, 3, 3 }).InWave();
        var first = driver.Batches[0];
        driver.End(CreepEnd.Killed);
        driver.Tick(6f);
        driver.Tick(6f);
        Assert.AreEqual(first.Slot, driver.Batches[1].Slot, "the second pack takes the empty slot");
        Assert.AreEqual(first.PackId, driver.Batches[2].Slot, "the third pack sits in the slot whose number equals the old pack id");

        driver.Run.PackStalled(first.PackId);

        Assert.AreEqual(0, driver.Tick().Removals.Count);
        Assert.AreEqual(6, driver.Live.Count);
    }

    [TestMethod]
    public void PackStalled_InAPause_ChangesNothing()
    {
        var driver = new DefenceDriver().InWave();
        var batch = driver.Batches[0];
        driver.KillAllAndClear();
        driver.AddCreep(Soldier(), batch.Slot);

        driver.Run.PackStalled(batch.PackId);

        Assert.AreEqual(0, driver.Tick().Removals.Count, "a late spawn report put a creep in the pause; the pause keeps it");
        Assert.AreEqual(DefencePhase.Pause, driver.Run.Phase);
    }

    [TestMethod]
    [DataRow(-1)]
    [DataRow(0)]
    [DataRow(2)]
    [DataRow(int.MaxValue)]
    public void PackStalled_UnknownPack_ChangesNothing(int packId)
    {
        var driver = new DefenceDriver(null, new[] { 2 }).InWave();
        var lives = driver.Run.Lives;
        Assert.AreEqual(1, driver.Batches[0].PackId, "the run numbers its packs from 1");

        driver.Run.PackStalled(packId);

        Assert.AreEqual(0, driver.Tick().Removals.Count);
        Assert.AreEqual(2, driver.Live.Count);
        Assert.AreEqual(lives, driver.Run.Lives);
    }
}
