using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Adapters;
using TAOM.Core.Domain;
using TAOM.Core.Logging;
using TAOM.Features.NamedCompanions;
using TAOM.Features.NamedCompanions.Domain;

namespace TAOM.Tests.Features.NamedCompanions;

[TestClass]
public class NamedCompanionServiceTests
{
    private INamedCompanionConfigProvider _configProvider;
    private INamedCompanionAdapter _companionAdapter;
    private IHeroRosterAdapter _heroRosterAdapter;
    private IRaceManager _raceManager;
    private IModLogger _logger;
    private NamedCompanionService _sut;

    [TestInitialize]
    public void Setup()
    {
        _configProvider = Substitute.For<INamedCompanionConfigProvider>();
        _companionAdapter = Substitute.For<INamedCompanionAdapter>();
        _heroRosterAdapter = Substitute.For<IHeroRosterAdapter>();
        _raceManager = Substitute.For<IRaceManager>();
        _logger = Substitute.For<IModLogger>();

        _configProvider.GetCompanions().Returns(new List<NamedCompanionDefinition>());

        _sut = new NamedCompanionService(
            _configProvider, _companionAdapter, _heroRosterAdapter, _raceManager, _logger);
    }

    [TestMethod]
    public void SpawnCompanions_EnabledCompanion_PlacesInSettlement()
    {
        SetupCompanions(new NamedCompanionDefinition
        {
            CharacterId = "named_companion_aragorn",
            SpawnSettlement = "town_EN2",
            Race = "human",
            Enabled = true
        });
        _companionAdapter.HeroExists("named_companion_aragorn").Returns(true);
        _raceManager.GetRaceIdFromName("human").Returns(0);

        _sut.SpawnCompanions();

        _companionAdapter.Received(1).PlaceInSettlement("named_companion_aragorn", "town_EN2");
    }

    [TestMethod]
    public void SpawnCompanions_EnabledCompanion_MarksAsMet()
    {
        SetupCompanions(new NamedCompanionDefinition
        {
            CharacterId = "named_companion_aragorn",
            SpawnSettlement = "town_EN2",
            Race = "human",
            Enabled = true
        });
        _companionAdapter.HeroExists("named_companion_aragorn").Returns(true);
        _raceManager.GetRaceIdFromName("human").Returns(0);

        _sut.SpawnCompanions();

        _companionAdapter.Received(1).MarkAsMet("named_companion_aragorn");
    }

    [TestMethod]
    public void SpawnCompanions_EnabledCompanion_SetsRace()
    {
        SetupCompanions(new NamedCompanionDefinition
        {
            CharacterId = "named_companion_legolas",
            SpawnSettlement = "town_M1",
            Race = "elf",
            Enabled = true
        });
        _companionAdapter.HeroExists("named_companion_legolas").Returns(true);
        _raceManager.GetRaceIdFromName("elf").Returns(2);

        _sut.SpawnCompanions();

        _heroRosterAdapter.Received(1).SetHeroRace("named_companion_legolas", 2);
    }

    [TestMethod]
    public void SpawnCompanions_DisabledCompanion_SkipsPlacement()
    {
        SetupCompanions(new NamedCompanionDefinition
        {
            CharacterId = "named_companion_aragorn",
            SpawnSettlement = "town_EN2",
            Race = "human",
            Enabled = false
        });

        _sut.SpawnCompanions();

        _companionAdapter.DidNotReceive().PlaceInSettlement(Arg.Any<string>(), Arg.Any<string>());
    }

    [TestMethod]
    public void SpawnCompanions_HeroDoesNotExist_LogsWarningAndSkips()
    {
        SetupCompanions(new NamedCompanionDefinition
        {
            CharacterId = "named_companion_missing",
            SpawnSettlement = "town_EN2",
            Race = "human",
            Enabled = true
        });
        _companionAdapter.HeroExists("named_companion_missing").Returns(false);

        _sut.SpawnCompanions();

        _companionAdapter.DidNotReceive().PlaceInSettlement(Arg.Any<string>(), Arg.Any<string>());
        _logger.Received().LogWarning(Arg.Is<string>(s => s.Contains("named_companion_missing")));
    }

    [TestMethod]
    public void SpawnCompanions_MultipleCompanions_PlacesAllEnabled()
    {
        SetupCompanions(
            new NamedCompanionDefinition { CharacterId = "nc_a", SpawnSettlement = "town_1", Race = "human", Enabled = true },
            new NamedCompanionDefinition { CharacterId = "nc_b", SpawnSettlement = "town_2", Race = "elf", Enabled = true },
            new NamedCompanionDefinition { CharacterId = "nc_c", SpawnSettlement = "town_3", Race = "dwarf", Enabled = false }
        );
        _companionAdapter.HeroExists("nc_a").Returns(true);
        _companionAdapter.HeroExists("nc_b").Returns(true);
        _raceManager.GetRaceIdFromName("human").Returns(0);
        _raceManager.GetRaceIdFromName("elf").Returns(2);

        _sut.SpawnCompanions();

        _companionAdapter.Received(1).PlaceInSettlement("nc_a", "town_1");
        _companionAdapter.Received(1).PlaceInSettlement("nc_b", "town_2");
        _companionAdapter.DidNotReceive().PlaceInSettlement("nc_c", Arg.Any<string>());
    }

    [TestMethod]
    public void SpawnCompanions_CalledTwice_OnlySpawnsOnce()
    {
        SetupCompanions(new NamedCompanionDefinition
        {
            CharacterId = "nc_a",
            SpawnSettlement = "town_1",
            Race = "human",
            Enabled = true
        });
        _companionAdapter.HeroExists("nc_a").Returns(true);
        _raceManager.GetRaceIdFromName("human").Returns(0);

        _sut.SpawnCompanions();
        _sut.SpawnCompanions();

        _companionAdapter.Received(1).PlaceInSettlement("nc_a", "town_1");
    }

    [TestMethod]
    public void EnsureCompanionsPlaced_AliveAndNotPlaced_RePlaces()
    {
        SetupCompanions(new NamedCompanionDefinition
        {
            CharacterId = "nc_a",
            SpawnSettlement = "town_1",
            Race = "human",
            Enabled = true
        });
        _companionAdapter.HeroExists("nc_a").Returns(true);
        _companionAdapter.IsHeroAlive("nc_a").Returns(true);
        _companionAdapter.IsPlacedInSettlement("nc_a").Returns(false);

        _sut.EnsureCompanionsPlaced();

        _companionAdapter.Received(1).PlaceInSettlement("nc_a", "town_1");
        _companionAdapter.Received(1).MarkAsMet("nc_a");
    }

    [TestMethod]
    public void EnsureCompanionsPlaced_AlreadyPlaced_SkipsPlacement()
    {
        SetupCompanions(new NamedCompanionDefinition
        {
            CharacterId = "nc_a",
            SpawnSettlement = "town_1",
            Race = "human",
            Enabled = true
        });
        _companionAdapter.HeroExists("nc_a").Returns(true);
        _companionAdapter.IsHeroAlive("nc_a").Returns(true);
        _companionAdapter.IsPlacedInSettlement("nc_a").Returns(true);

        _sut.EnsureCompanionsPlaced();

        _companionAdapter.DidNotReceive().PlaceInSettlement(Arg.Any<string>(), Arg.Any<string>());
    }

    [TestMethod]
    public void EnsureCompanionsPlaced_DeadHero_SkipsPlacement()
    {
        SetupCompanions(new NamedCompanionDefinition
        {
            CharacterId = "nc_a",
            SpawnSettlement = "town_1",
            Race = "human",
            Enabled = true
        });
        _companionAdapter.HeroExists("nc_a").Returns(true);
        _companionAdapter.IsHeroAlive("nc_a").Returns(false);

        _sut.EnsureCompanionsPlaced();

        _companionAdapter.DidNotReceive().PlaceInSettlement(Arg.Any<string>(), Arg.Any<string>());
    }

    [TestMethod]
    public void EnsureCompanionsPlaced_HeroDoesNotExist_SkipsPlacement()
    {
        SetupCompanions(new NamedCompanionDefinition
        {
            CharacterId = "nc_missing",
            SpawnSettlement = "town_1",
            Race = "human",
            Enabled = true
        });
        _companionAdapter.HeroExists("nc_missing").Returns(false);

        _sut.EnsureCompanionsPlaced();

        _companionAdapter.DidNotReceive().PlaceInSettlement(Arg.Any<string>(), Arg.Any<string>());
    }

    [TestMethod]
    public void EnsureCompanionsPlaced_DisabledCompanion_SkipsPlacement()
    {
        SetupCompanions(new NamedCompanionDefinition
        {
            CharacterId = "nc_a",
            SpawnSettlement = "town_1",
            Race = "human",
            Enabled = false
        });

        _sut.EnsureCompanionsPlaced();

        _companionAdapter.DidNotReceive().PlaceInSettlement(Arg.Any<string>(), Arg.Any<string>());
    }

    private void SetupCompanions(params NamedCompanionDefinition[] companions)
    {
        _configProvider.GetCompanions().Returns(new List<NamedCompanionDefinition>(companions));
    }
}
