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
        _companionAdapter.IsRecruitedOrInParty("nc_a").Returns(false);
        _companionAdapter.IsPlacedInSettlement("nc_a").Returns(false);

        _sut.EnsureCompanionsPlaced();

        _companionAdapter.Received(1).PlaceInSettlement("nc_a", "town_1");
        _companionAdapter.Received(1).MarkAsMet("nc_a");
    }

    [TestMethod]
    public void EnsureCompanionsPlaced_RecruitedCompanion_SkipsPlacement()
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
        _companionAdapter.IsRecruitedOrInParty("nc_a").Returns(true);

        _sut.EnsureCompanionsPlaced();

        _companionAdapter.DidNotReceive().PlaceInSettlement(Arg.Any<string>(), Arg.Any<string>());
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

    // ── Entity State Matrix completion (#127 / #184) ──
    // Phase 9b: prior guards (HeroExists/IsHeroAlive/IsRecruitedOrInParty/IsPlacedInSettlement) covered
    // states 1-4 of the 6-state Entity State Matrix but missed Prisoner and Fugitive. Both states have
    // PartyBelongedTo=null and no CurrentSettlement/StayingInSettlement — they slipped through ALL
    // guards and got force-placed via EnterSettlementAction every load, corrupting captor prison
    // rosters and resetting fugitive state to Active. These tests pin the new IsHeroPrisoner /
    // IsHeroFugitive guards so a future refactor can't silently re-introduce the regression.

    [TestMethod]
    public void EnsureCompanionsPlaced_PrisonerCompanion_SkipsPlacement()
    {
        SetupCompanions(new NamedCompanionDefinition
        {
            CharacterId = "nc_prisoner",
            SpawnSettlement = "town_1",
            Race = "human",
            Enabled = true
        });
        _companionAdapter.HeroExists("nc_prisoner").Returns(true);
        _companionAdapter.IsHeroAlive("nc_prisoner").Returns(true);
        _companionAdapter.IsRecruitedOrInParty("nc_prisoner").Returns(false);
        _companionAdapter.IsPlacedInSettlement("nc_prisoner").Returns(false);
        _companionAdapter.IsHeroPrisoner("nc_prisoner").Returns(true);

        _sut.EnsureCompanionsPlaced();

        _companionAdapter.DidNotReceive().PlaceInSettlement(Arg.Any<string>(), Arg.Any<string>());
        _companionAdapter.DidNotReceive().MarkAsMet(Arg.Any<string>());
    }

    [TestMethod]
    public void EnsureCompanionsPlaced_FugitiveCompanion_SkipsPlacement()
    {
        SetupCompanions(new NamedCompanionDefinition
        {
            CharacterId = "nc_fugitive",
            SpawnSettlement = "town_1",
            Race = "human",
            Enabled = true
        });
        _companionAdapter.HeroExists("nc_fugitive").Returns(true);
        _companionAdapter.IsHeroAlive("nc_fugitive").Returns(true);
        _companionAdapter.IsRecruitedOrInParty("nc_fugitive").Returns(false);
        _companionAdapter.IsPlacedInSettlement("nc_fugitive").Returns(false);
        _companionAdapter.IsHeroPrisoner("nc_fugitive").Returns(false);
        _companionAdapter.IsHeroFugitive("nc_fugitive").Returns(true);

        _sut.EnsureCompanionsPlaced();

        _companionAdapter.DidNotReceive().PlaceInSettlement(Arg.Any<string>(), Arg.Any<string>());
        _companionAdapter.DidNotReceive().MarkAsMet(Arg.Any<string>());
    }

    // ── Null MapFaction repair (crash d7d9f7d3) ──
    // Named companions are XML heroes with faction="Faction.neutral"; Hero.Deserialize skips the
    // Clan assignment for that id, and XML deserialization never sets BornSettlement. Clan +
    // HomeSettlement + PartyBelongedTo all null makes Hero.MapFaction return null, and the engine
    // dereferences it unguarded — TournamentVM.OnTournamentEnd reads MapFaction.Color on the winner,
    // so any Erebor tournament won by one of the four companions living in town_E1 took the game
    // down. Repair runs before the placement-state guards so already-placed companions in existing
    // saves are fixed too, not just freshly placed ones.

    [TestMethod]
    public void SpawnCompanions_EnabledCompanion_RepairsHomeSettlementBeforePlacing()
    {
        SetupCompanions(new NamedCompanionDefinition
        {
            CharacterId = "nc_a",
            SpawnSettlement = "town_1",
            Race = "dwarf",
            Enabled = true
        });
        _companionAdapter.HeroExists("nc_a").Returns(true);
        _raceManager.GetRaceIdFromName("dwarf").Returns(1);

        _sut.SpawnCompanions();

        Received.InOrder(() =>
        {
            _companionAdapter.EnsureHomeSettlement("nc_a", "town_1");
            _companionAdapter.PlaceInSettlement("nc_a", "town_1");
        });
    }

    [TestMethod]
    public void EnsureCompanionsPlaced_AlreadyPlaced_StillRepairsHomeSettlement()
    {
        // The load-path regression that matters: an existing save has the companion already sitting
        // in the tavern, so every placement guard short-circuits. The repair must run anyway.
        SetupCompanions(new NamedCompanionDefinition
        {
            CharacterId = "nc_gimli",
            SpawnSettlement = "town_E1",
            Race = "dwarf",
            Enabled = true
        });
        _companionAdapter.HeroExists("nc_gimli").Returns(true);
        _companionAdapter.IsHeroAlive("nc_gimli").Returns(true);
        _companionAdapter.IsPlacedInSettlement("nc_gimli").Returns(true);

        _sut.EnsureCompanionsPlaced();

        _companionAdapter.Received(1).EnsureHomeSettlement("nc_gimli", "town_E1");
        _companionAdapter.DidNotReceive().PlaceInSettlement(Arg.Any<string>(), Arg.Any<string>());
    }

    [TestMethod]
    public void EnsureCompanionsPlaced_RecruitedCompanion_StillRepairsHomeSettlement()
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
        _companionAdapter.IsRecruitedOrInParty("nc_a").Returns(true);

        _sut.EnsureCompanionsPlaced();

        _companionAdapter.Received(1).EnsureHomeSettlement("nc_a", "town_1");
    }

    [TestMethod]
    public void EnsureCompanionsPlaced_DeadHero_DoesNotRepairHomeSettlement()
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

        _companionAdapter.DidNotReceive().EnsureHomeSettlement(Arg.Any<string>(), Arg.Any<string>());
    }

    [TestMethod]
    public void EnsureCompanionsPlaced_DisabledCompanion_DoesNotRepairHomeSettlement()
    {
        SetupCompanions(new NamedCompanionDefinition
        {
            CharacterId = "nc_a",
            SpawnSettlement = "town_1",
            Race = "human",
            Enabled = false
        });

        _sut.EnsureCompanionsPlaced();

        _companionAdapter.DidNotReceive().EnsureHomeSettlement(Arg.Any<string>(), Arg.Any<string>());
    }

    [TestMethod]
    public void EnsureCompanionsPlaced_RepairsApplied_LogsCount()
    {
        SetupCompanions(
            new NamedCompanionDefinition { CharacterId = "nc_a", SpawnSettlement = "town_E1", Race = "dwarf", Enabled = true },
            new NamedCompanionDefinition { CharacterId = "nc_b", SpawnSettlement = "town_E1", Race = "dwarf", Enabled = true }
        );
        foreach (var id in new[] { "nc_a", "nc_b" })
        {
            _companionAdapter.HeroExists(id).Returns(true);
            _companionAdapter.IsHeroAlive(id).Returns(true);
            _companionAdapter.IsPlacedInSettlement(id).Returns(true);
        }
        _companionAdapter.EnsureHomeSettlement("nc_a", "town_E1").Returns(true);
        _companionAdapter.EnsureHomeSettlement("nc_b", "town_E1").Returns(false);

        _sut.EnsureCompanionsPlaced();

        _logger.Received(1).LogInfo(Arg.Is<string>(s => s.Contains("home settlement") && s.Contains("1")));
    }

    [TestMethod]
    public void EnsureCompanionsPlaced_NoRepairsNeeded_LogsNothing()
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
        _companionAdapter.EnsureHomeSettlement("nc_a", "town_1").Returns(false);

        _sut.EnsureCompanionsPlaced();

        _logger.DidNotReceive().LogInfo(Arg.Any<string>());
    }

    [TestMethod]
    public void ResetSession_AllowsSpawnCompanionsAgainInSameProcess()
    {
        // #127 R1 — _spawned is a singleton field. Without ResetSession, a second campaign in the
        // same Bannerlord process would have _spawned=true from campaign 1 and SpawnCompanions would
        // early-exit, leaving the new campaign with zero placed companions.
        SetupCompanions(new NamedCompanionDefinition
        {
            CharacterId = "nc_a",
            SpawnSettlement = "town_1",
            Race = "human",
            Enabled = true
        });
        _companionAdapter.HeroExists("nc_a").Returns(true);
        _raceManager.GetRaceIdFromName("human").Returns(0);

        // Campaign 1
        _sut.SpawnCompanions();
        _companionAdapter.Received(1).PlaceInSettlement("nc_a", "town_1");

        // Campaign 2 - without ResetSession, the second call is a no-op (existing
        // SpawnCompanions_CalledTwice_OnlySpawnsOnce test) — ResetSession clears the latch.
        _companionAdapter.ClearReceivedCalls();
        _sut.ResetSession();
        _sut.SpawnCompanions();

        _companionAdapter.Received(1).PlaceInSettlement("nc_a", "town_1");
    }

    private void SetupCompanions(params NamedCompanionDefinition[] companions)
    {
        _configProvider.GetCompanions().Returns(new List<NamedCompanionDefinition>(companions));
    }
}
