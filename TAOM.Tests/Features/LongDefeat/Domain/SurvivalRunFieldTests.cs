using System;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.LongDefeat.Domain;

namespace TAOM.Tests.Features.LongDefeat.Domain;

/// <summary>
/// A run on a field scene (#1105 slice 2) may hold fewer attackers alive than a siege. A siege keeps at least 101 alive, because at 100 or fewer the breach
/// tactic stops every siege engine; a field has no engine. Run 20261121-183616 showed why a field mission wants a small number: 25 attackers who arrive
/// together kill a hero in about five seconds, and the same wave as a stream of small groups is a fight.
/// </summary>
[TestClass]
public class SurvivalRunFieldTests
{
    private static RunPlan Plan(bool field, int liveCap, int wave = 40) =>
        new(
            "parth_galen",
            new[] { new GarrisonRow("guard", FormationSlot.Infantry, 5) },
            new WavePlan(new[] { new WaveSpec(wave, new[] { new WaveTroop("uruk", FormationSlot.Infantry, 1) }) }, new WaveScaling(10, 300)),
            new ShopPlan(0, Array.Empty<ShopOffer>()),
            RunRules.Default with { PreparationSeconds = 1f, LiveCap = liveCap, SpawnsPerTick = 8 },
            new RunOptions { StopAfterWaves = 1 })
        {
            Field = field ? new FieldPlaces(null, null) : null,
        };

    private static RunSnapshot Snap(int attackers) => new(1f, attackers, 0, 5, 0, 0, 1000);

    [TestMethod]
    public void OnAField_WhenTheBattleSizeIsSmallerThanTheCap_TheGroupIsHalfTheRealRoom_SoTheWaveNeverStalls()
    {
        // The rules' cap is 150, but a battle size of 60 with 20 defenders leaves room for 40. A group of half the cap (75) would never fit.
        var run = new SurvivalRun(Plan(field: true, liveCap: 150, wave: 100));
        run.Begin();
        RunSnapshot Small(int attackers) => new(1f, attackers, 0, 20, 0, 0, 60);

        var standing = 0;
        for (var i = 0; i < 5; i++)
            standing += run.Tick(Small(standing)).AttackerBatches.Sum(b => b.Count);
        Assert.AreEqual(40, standing, "the opening fills the real room");

        var next = run.Tick(Small(0)).AttackerBatches.Sum(b => b.Count) + run.Tick(Small(8)).AttackerBatches.Sum(b => b.Count)
            + run.Tick(Small(16)).AttackerBatches.Sum(b => b.Count);

        Assert.AreEqual(20, next, "half of the room of 40, over three ticks of 8");
    }

    [TestMethod]
    public void LiveRoom_OnASiege_NeverGoesBelowTheFloorOf101()
    {
        Assert.AreEqual(101, SurvivalRun.LiveRoom(RunRules.Default with { LiveCap = 10 }, 1000, 5));
        Assert.AreEqual(150, SurvivalRun.LiveRoom(RunRules.Default with { LiveCap = 150 }, 1000, 5));
    }

    [TestMethod]
    public void LiveRoom_OnAField_IsTheRulesCap_AndNeverBelowOne()
    {
        Assert.AreEqual(10, SurvivalRun.LiveRoom(RunRules.Default with { LiveCap = 10 }, 1000, 5, field: true));
        Assert.AreEqual(1, SurvivalRun.LiveRoom(RunRules.Default with { LiveCap = 10 }, 3, 5, field: true), "a battle size below the defenders still lets one attacker in");
    }

    [TestMethod]
    public void OnAField_TheWaveNeverHoldsMoreThanTheCapAlive_AndADeathLetsTheNextOneIn()
    {
        var run = new SurvivalRun(Plan(field: true, liveCap: 10));
        run.Begin();

        // The tick that ends the one second of preparation starts the wave and spawns its first part.
        var first = run.Tick(Snap(0)).AttackerBatches.Sum(b => b.Count);
        var second = run.Tick(Snap(first)).AttackerBatches.Sum(b => b.Count);
        var third = run.Tick(Snap(first + second)).AttackerBatches.Sum(b => b.Count);
        var afterThreeDeaths = run.Tick(Snap(first + second - 3)).AttackerBatches.Sum(b => b.Count);
        var afterFiveDeaths = run.Tick(Snap(first + second - 5)).AttackerBatches.Sum(b => b.Count);
        var withTheGroupIn = run.Tick(Snap(first + second)).AttackerBatches.Sum(b => b.Count);

        Assert.AreEqual(8, first, "the wave's first tick");
        Assert.AreEqual(2, second, "up to the cap of 10");
        Assert.AreEqual(0, third, "the cap holds while ten stand");
        Assert.AreEqual(0, afterThreeDeaths, "no single soldier walks in alone: the next group waits for room for half the cap");
        Assert.AreEqual(5, afterFiveDeaths, "a group of half the cap");
        Assert.AreEqual(0, withTheGroupIn);
    }

    [TestMethod]
    public void OnAField_AGroupLargerThanOneTicksSpawns_ComesInOverTwoTicks_AndTheLastGroupIsWhatIsLeft()
    {
        var run = new SurvivalRun(Plan(field: true, liveCap: 24, wave: 40));
        run.Begin();

        // The opening: 24 over three ticks of 8.
        var standing = 0;
        for (var i = 0; i < 3; i++)
            standing += run.Tick(Snap(standing)).AttackerBatches.Sum(b => b.Count);
        Assert.AreEqual(24, standing);

        // 12 fall: room for one group of 12, which needs two ticks at 8 a tick, though the room shrinks as the group's first part lands.
        standing -= 12;
        var part1 = run.Tick(Snap(standing)).AttackerBatches.Sum(b => b.Count);
        var part2 = run.Tick(Snap(standing + part1)).AttackerBatches.Sum(b => b.Count);
        Assert.AreEqual(8, part1);
        Assert.AreEqual(4, part2);

        // 4 are left in the queue: the last group is those 4, once there is room for them.
        standing = 12;
        Assert.AreEqual(4, run.Tick(Snap(standing)).AttackerBatches.Sum(b => b.Count));
    }

    [TestMethod]
    public void OnASiege_TheSameRulesStillFieldTheWholeWave()
    {
        var run = new SurvivalRun(Plan(field: false, liveCap: 10));
        run.Begin();

        var spawned = 0;
        for (var i = 0; i < 6; i++)
            spawned += run.Tick(Snap(spawned)).AttackerBatches.Sum(b => b.Count);

        Assert.AreEqual(40, spawned);
    }
}
