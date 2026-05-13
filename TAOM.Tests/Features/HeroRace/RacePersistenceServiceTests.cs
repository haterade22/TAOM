using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Adapters;
using TAOM.Core.Logging;
using TAOM.Features.HeroRace;
using TaleWorlds.CampaignSystem;

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

    // --- Phase 9b #181 — CharacterCreation × HeroRace round-trip via save/load ---
    //
    // Closes the cross-feature contract gap from Phase 6 #171: a player race assigned at
    // OnCharacterCreationFinalize must survive save/load via RacePersistence. Existing tests
    // verify Capture and Restore independently; this one ties them into a single round-trip
    // simulating the production save/load handoff.
    //
    // SyncData is the engine handoff (Dictionary<string,int> serialized via IDataStore). The
    // test simulates the engine's role by reading `_heroRaceMap` through SyncData and re-injecting
    // it on a fresh service instance.

    [TestMethod]
    public void CaptureRestore_RoundTrip_PreservesPlayerRaceSetByCharacterCreation()
    {
        // Step 1 — CharacterCreation finalize: player gets race=2 (elf).
        const string playerId = "player_hero";
        const int elfRace = 2;
        _heroRosterAdapter.GetAllAliveHeroRaces().Returns(new List<HeroRaceInfo>
        {
            new HeroRaceInfo(playerId, elfRace),
            new HeroRaceInfo("npc_dwarf", 1)
        });

        // Step 2 — OnBeforeSave: capture races
        _sut.CaptureHeroRaces();
        Assert.AreEqual(2, _sut.CapturedRaceCount, "Capture must include both non-human heroes");

        // Step 3 — Save: simulate engine SyncData by capturing the ref-parameter value via fake.
        var savingStore = new RoundTripDataStore { IsSaving = true };
        _sut.SyncRaceData(savingStore);
        var savedSnapshot = savingStore.LastSavedDict;
        Assert.IsNotNull(savedSnapshot, "SyncData must publish a snapshot when saving");
        Assert.AreEqual(elfRace, savedSnapshot[playerId], "Player race must be in the save snapshot");
        Assert.AreEqual(1, savedSnapshot["npc_dwarf"], "NPC race must also be in the save snapshot");

        // Step 4 — Load: NEW service instance (simulates Bannerlord process restart),
        // SyncData (loading) populates the new instance's map from the persisted snapshot.
        var freshAdapter = Substitute.For<IHeroRosterAdapter>();
        var freshService = new RacePersistenceService(freshAdapter, Substitute.For<IModLogger>());
        var loadingStore = new RoundTripDataStore { IsSaving = false, NextLoadDict = savedSnapshot };
        freshService.SyncRaceData(loadingStore);
        Assert.AreEqual(2, freshService.CapturedRaceCount, "Fresh instance must rehydrate the snapshot");

        // Step 5 — OnSessionLaunched restore: heroes are loaded with race=0 (vanilla), Restore
        // re-applies the captured race=2 to the player.
        freshAdapter.GetAllAliveHeroRaces().Returns(new List<HeroRaceInfo>
        {
            new HeroRaceInfo(playerId, 0),
            new HeroRaceInfo("npc_dwarf", 0)
        });
        freshService.RestoreHeroRaces();

        freshAdapter.Received(1).SetHeroRace(playerId, elfRace);
        freshAdapter.Received(1).SetHeroRace("npc_dwarf", 1);
    }
}

/// <summary>
/// Minimal hand-rolled IDataStore stub for Phase 9b #181 round-trip test. NSubstitute can't easily
/// model ref-parameter `SyncData<T>(string, ref T)` with the Do-callback pattern, so we capture
/// the saved dict on save and re-inject it on load directly.
/// </summary>
internal class RoundTripDataStore : IDataStore
{
    public bool IsSaving { get; set; }
    public bool IsLoading => !IsSaving;
    public Dictionary<string, int> LastSavedDict { get; private set; }
    public Dictionary<string, int> NextLoadDict { get; set; }

    public bool SyncData<T>(string key, ref T data)
    {
        if (IsSaving && data is Dictionary<string, int> dict)
        {
            LastSavedDict = new Dictionary<string, int>(dict);
            return true;
        }
        if (!IsSaving && NextLoadDict != null)
        {
            data = (T)(object)NextLoadDict;
            return true;
        }
        return false;
    }
}
