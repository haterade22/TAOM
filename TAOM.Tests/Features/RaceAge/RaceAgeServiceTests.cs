using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Core.Domain;
using TAOM.Features.RaceAge;
using TAOM.Features.RaceAge.Models;

namespace TAOM.Tests.Features.RaceAge;

[TestClass]
public class RaceAgeServiceTests
{
    private IRaceAgeConfigProvider _configProvider;
    private IRaceManager _raceManager;
    private RaceAgeService _sut;

    [TestInitialize]
    public void Setup()
    {
        _configProvider = Substitute.For<IRaceAgeConfigProvider>();
        _raceManager = Substitute.For<IRaceManager>();

        var config = new RaceAgeConfig
        {
            DefaultRace = "human",
            Races = new Dictionary<string, RaceAgeEntry>
            {
                ["human"] = new RaceAgeEntry { MaxAge = 85, BecomeOld = 55, ComesOfAge = 18, MiddleAge = 35, FertilityEnd = 45, FertilityMod = 1.0f },
                ["dwarf"] = new RaceAgeEntry { MaxAge = 250, BecomeOld = 180, ComesOfAge = 30, MiddleAge = 80, FertilityEnd = 120, FertilityMod = 0.6f },
                ["orc"] = new RaceAgeEntry { MaxAge = 60, BecomeOld = 40, ComesOfAge = 12, MiddleAge = 25, FertilityEnd = 50, FertilityMod = 2.0f },
                ["nazghul"] = new RaceAgeEntry { MaxAge = 10000, BecomeOld = 9999, ComesOfAge = 18, MiddleAge = 100, FertilityEnd = 0, FertilityMod = 0.0f, Immortal = true }
            }
        };

        _configProvider.LoadConfig().Returns(config);

        _raceManager.GetRaceNameFromId(0).Returns("human");
        _raceManager.GetRaceNameFromId(1).Returns("dwarf");
        _raceManager.GetRaceNameFromId(2).Returns("orc");
        _raceManager.GetRaceNameFromId(3).Returns("nazghul");
        _raceManager.GetRaceNameFromId(99).Returns("unknown_race");

        _sut = new RaceAgeService(_configProvider, _raceManager);
    }

    [TestMethod]
    public void GetMaxAge_Human_Returns85()
    {
        Assert.AreEqual(85, _sut.GetMaxAge(0));
    }

    [TestMethod]
    public void GetMaxAge_Dwarf_Returns250()
    {
        Assert.AreEqual(250, _sut.GetMaxAge(1));
    }

    [TestMethod]
    public void GetMaxAge_Orc_Returns60()
    {
        Assert.AreEqual(60, _sut.GetMaxAge(2));
    }

    [TestMethod]
    public void GetMaxAge_UnknownRace_FallsBackToDefault()
    {
        Assert.AreEqual(85, _sut.GetMaxAge(99));
    }

    [TestMethod]
    public void GetBecomeOldAge_Dwarf_Returns180()
    {
        Assert.AreEqual(180, _sut.GetBecomeOldAge(1));
    }

    [TestMethod]
    public void GetComesOfAge_Orc_Returns12()
    {
        Assert.AreEqual(12, _sut.GetComesOfAge(2));
    }

    [TestMethod]
    public void GetMiddleAge_Human_Returns35()
    {
        Assert.AreEqual(35, _sut.GetMiddleAge(0));
    }

    [TestMethod]
    public void GetFertilityEndAge_Dwarf_Returns120()
    {
        Assert.AreEqual(120, _sut.GetFertilityEndAge(1));
    }

    [TestMethod]
    public void GetFertilityModifier_Orc_Returns2()
    {
        Assert.AreEqual(2.0f, _sut.GetFertilityModifier(2));
    }

    [TestMethod]
    public void GetFertilityModifier_Nazghul_ReturnsZero()
    {
        Assert.AreEqual(0.0f, _sut.GetFertilityModifier(3));
    }

    [TestMethod]
    public void IsImmortal_Nazghul_ReturnsTrue()
    {
        Assert.IsTrue(_sut.IsImmortal(3));
    }

    [TestMethod]
    public void IsImmortal_Human_ReturnsFalse()
    {
        Assert.IsFalse(_sut.IsImmortal(0));
    }

    [TestMethod]
    public void IsImmortal_UnknownRace_ReturnsFalse()
    {
        Assert.IsFalse(_sut.IsImmortal(99));
    }

    [TestMethod]
    public void ShouldDieOfOldAge_HumanAt90_ReturnsTrue()
    {
        Assert.IsTrue(_sut.ShouldDieOfOldAge(0, 90f));
    }

    [TestMethod]
    public void ShouldDieOfOldAge_HumanAt50_ReturnsFalse()
    {
        Assert.IsFalse(_sut.ShouldDieOfOldAge(0, 50f));
    }

    [TestMethod]
    public void ShouldDieOfOldAge_DwarfAt200_ReturnsFalse()
    {
        Assert.IsFalse(_sut.ShouldDieOfOldAge(1, 200f));
    }

    [TestMethod]
    public void ShouldDieOfOldAge_DwarfAt260_ReturnsTrue()
    {
        Assert.IsTrue(_sut.ShouldDieOfOldAge(1, 260f));
    }

    [TestMethod]
    public void ShouldDieOfOldAge_Immortal_NeverDies()
    {
        Assert.IsFalse(_sut.ShouldDieOfOldAge(3, 99999f));
    }
}
