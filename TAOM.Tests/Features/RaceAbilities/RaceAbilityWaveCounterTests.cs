using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.RaceAbilities;

// The message-log throttle: one line per side and ability when five or more soldiers fire it inside two
// seconds, with the total, and never one line per soldier.

namespace TAOM.Tests.Features.RaceAbilities;

[TestClass]
public class RaceAbilityWaveCounterTests
{
    private RaceAbilityWaveCounter _sut = null!;
    private readonly List<RaceAbilityWave> _due = new List<RaceAbilityWave>();

    [TestInitialize]
    public void Setup() => _sut = new RaceAbilityWaveCounter();

    [TestMethod]
    public void Flush_BeforeTheWindowEnds_AnnouncesNothing()
    {
        _sut.Record("berserk", playerSide: true, now: 100f, soldiers: 6);

        _sut.Flush(101.9f, _due);

        Assert.AreEqual(0, _due.Count);
    }

    [TestMethod]
    public void Flush_WindowOfFiveOrMore_AnnouncesTheTotalOnce()
    {
        _sut.Record("berserk", playerSide: true, now: 100f, soldiers: 4);
        _sut.Record("berserk", playerSide: true, now: 101f, soldiers: 3);

        _sut.Flush(102f, _due);
        _sut.Flush(103f, _due);

        Assert.AreEqual(1, _due.Count);
        Assert.AreEqual("berserk", _due[0].AbilityId);
        Assert.IsTrue(_due[0].PlayerSide);
        Assert.AreEqual(7, _due[0].Soldiers);
    }

    [TestMethod]
    public void Flush_WindowOfFewerThanFive_IsDropped()
    {
        _sut.Record("berserk", playerSide: true, now: 100f, soldiers: 4);

        _sut.Flush(102f, _due);

        Assert.AreEqual(0, _due.Count);
    }

    [TestMethod]
    public void Record_SidesAndAbilitiesCountApart()
    {
        _sut.Record("berserk", playerSide: true, now: 100f, soldiers: 3);
        _sut.Record("berserk", playerSide: false, now: 100f, soldiers: 3);
        _sut.Record("stand_fast", playerSide: true, now: 100f, soldiers: 3);

        _sut.Flush(102f, _due);

        Assert.AreEqual(0, _due.Count);
    }

    [TestMethod]
    public void Record_AfterAWindowCloses_StartsANewOne()
    {
        _sut.Record("berserk", playerSide: true, now: 100f, soldiers: 5);
        _sut.Flush(102f, _due);
        _sut.Record("berserk", playerSide: true, now: 103f, soldiers: 5);

        _sut.Flush(105f, _due);

        Assert.AreEqual(2, _due.Count);
    }

    [TestMethod]
    public void Flush_NaNTime_AnnouncesNothing()
    {
        _sut.Record("berserk", playerSide: true, now: 100f, soldiers: 9);

        _sut.Flush(float.NaN, _due);

        Assert.AreEqual(0, _due.Count);
    }

    [TestMethod]
    public void Clear_ForgetsOpenWindows()
    {
        _sut.Record("berserk", playerSide: true, now: 100f, soldiers: 9);

        _sut.Clear();
        _sut.Flush(200f, _due);

        Assert.AreEqual(0, _due.Count);
    }
}
