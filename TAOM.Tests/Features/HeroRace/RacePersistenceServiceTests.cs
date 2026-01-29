using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Adapters;
using TAOM.Core.Logging;
using TAOM.Features.HeroRace;

namespace TAOM.Tests.Features.HeroRace;

[TestClass]
public class RacePersistenceServiceTests
{
    private RacePersistenceService _sut;
    private IHeroRosterAdapter _heroRosterAdapter;
    private IModLogger _logger;

    [TestInitialize]
    public void Setup()
    {
        _heroRosterAdapter = Substitute.For<IHeroRosterAdapter>();
        _logger = Substitute.For<IModLogger>();
        _sut = new RacePersistenceService(_heroRosterAdapter, _logger);
    }

    [TestMethod]
    public void CaptureHeroRaces_StoresOnlyNonHumanHeroes()
    {
        _heroRosterAdapter.GetAllAliveHeroRaces().Returns(new List<HeroRaceInfo>
        {
            new HeroRaceInfo("hero_human", 0),
            new HeroRaceInfo("hero_dwarf", 1),
            new HeroRaceInfo("hero_elf", 2)
        });

        _sut.CaptureHeroRaces();

        Assert.AreEqual(2, _sut.CapturedRaceCount);
    }

    [TestMethod]
    public void CaptureHeroRaces_ClearsPreviousDataOnReCapture()
    {
        _heroRosterAdapter.GetAllAliveHeroRaces().Returns(new List<HeroRaceInfo>
        {
            new HeroRaceInfo("hero_dwarf", 1)
        });

        _sut.CaptureHeroRaces();
        _sut.CaptureHeroRaces();

        Assert.AreEqual(1, _sut.CapturedRaceCount);
    }

    [TestMethod]
    public void CaptureHeroRaces_EmptyHeroList_StoresNothing()
    {
        _heroRosterAdapter.GetAllAliveHeroRaces().Returns(new List<HeroRaceInfo>());

        _sut.CaptureHeroRaces();

        Assert.AreEqual(0, _sut.CapturedRaceCount);
    }

    [TestMethod]
    public void CaptureHeroRaces_AllHumans_StoresNothing()
    {
        _heroRosterAdapter.GetAllAliveHeroRaces().Returns(new List<HeroRaceInfo>
        {
            new HeroRaceInfo("hero_1", 0),
            new HeroRaceInfo("hero_2", 0)
        });

        _sut.CaptureHeroRaces();

        Assert.AreEqual(0, _sut.CapturedRaceCount);
    }

    [TestMethod]
    public void RestoreHeroRaces_WhenMapEmpty_DoesNotCallSetRace()
    {
        _sut.RestoreHeroRaces();

        _heroRosterAdapter.DidNotReceive().SetHeroRace(Arg.Any<string>(), Arg.Any<int>());
    }

    [TestMethod]
    public void RestoreHeroRaces_RestoresRaceForCapturedHeroes()
    {
        _heroRosterAdapter.GetAllAliveHeroRaces().Returns(new List<HeroRaceInfo>
        {
            new HeroRaceInfo("hero_dwarf", 1)
        });

        _sut.CaptureHeroRaces();

        _heroRosterAdapter.GetAllAliveHeroRaces().Returns(new List<HeroRaceInfo>
        {
            new HeroRaceInfo("hero_dwarf", 0)
        });

        _sut.RestoreHeroRaces();

        _heroRosterAdapter.Received(1).SetHeroRace("hero_dwarf", 1);
    }

    [TestMethod]
    public void RestoreHeroRaces_SkipsHeroesNotInSavedMap()
    {
        _heroRosterAdapter.GetAllAliveHeroRaces().Returns(new List<HeroRaceInfo>
        {
            new HeroRaceInfo("hero_dwarf", 1)
        });

        _sut.CaptureHeroRaces();

        _heroRosterAdapter.GetAllAliveHeroRaces().Returns(new List<HeroRaceInfo>
        {
            new HeroRaceInfo("hero_new", 0)
        });

        _sut.RestoreHeroRaces();

        _heroRosterAdapter.DidNotReceive().SetHeroRace("hero_new", Arg.Any<int>());
    }

    [TestMethod]
    public void RestoreHeroRaces_SkipsHeroesWhoseRaceAlreadyMatches()
    {
        _heroRosterAdapter.GetAllAliveHeroRaces().Returns(new List<HeroRaceInfo>
        {
            new HeroRaceInfo("hero_dwarf", 1)
        });

        _sut.CaptureHeroRaces();

        _heroRosterAdapter.GetAllAliveHeroRaces().Returns(new List<HeroRaceInfo>
        {
            new HeroRaceInfo("hero_dwarf", 1)
        });

        _sut.RestoreHeroRaces();

        _heroRosterAdapter.DidNotReceive().SetHeroRace(Arg.Any<string>(), Arg.Any<int>());
    }

    [TestMethod]
    public void RestoreHeroRaces_LogsRestoredCount()
    {
        _heroRosterAdapter.GetAllAliveHeroRaces().Returns(new List<HeroRaceInfo>
        {
            new HeroRaceInfo("hero_dwarf", 1),
            new HeroRaceInfo("hero_elf", 2)
        });

        _sut.CaptureHeroRaces();

        _heroRosterAdapter.GetAllAliveHeroRaces().Returns(new List<HeroRaceInfo>
        {
            new HeroRaceInfo("hero_dwarf", 0),
            new HeroRaceInfo("hero_elf", 0)
        });

        _sut.RestoreHeroRaces();

        _logger.Received(1).LogInfo(Arg.Is<string>(s => s.Contains("2")));
    }

    [TestMethod]
    public void CaptureHeroRaces_DuplicateStringIds_StoresFirstOnly()
    {
        _heroRosterAdapter.GetAllAliveHeroRaces().Returns(new List<HeroRaceInfo>
        {
            new HeroRaceInfo("hero_dwarf", 1),
            new HeroRaceInfo("hero_dwarf", 2)
        });

        _sut.CaptureHeroRaces();

        Assert.AreEqual(1, _sut.CapturedRaceCount);
    }
}
