using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Adapters;
using TAOM.Core.Domain;
using TAOM.Core.Logging;
using TAOM.Features.HeroRace;
using TaleWorlds.CampaignSystem;

namespace TAOM.Tests.Features.HeroRace;

[TestClass]
public class RacePersistenceServiceTests
{
    private RacePersistenceService _sut;
    private IHeroRosterAdapter _heroRosterAdapter;
    private IRaceManager _raceManager;
    private IModLogger _logger;

    [TestInitialize]
    public void Setup()
    {
        _heroRosterAdapter = Substitute.For<IHeroRosterAdapter>();
        _raceManager = Substitute.For<IRaceManager>();
        _logger = Substitute.For<IModLogger>();
        // Phase 9b #171 — IRaceManager injected for validate-before-restore. Default-stub valid for
        // any non-zero so existing tests pass unchanged; specific tests override IsValidRaceId.
        _raceManager.IsValidRaceId(Arg.Any<int>()).Returns(true);
        _sut = new RacePersistenceService(_heroRosterAdapter, _raceManager, _logger);
    }

    [TestMethod]
    public void CaptureHeroRaces_StoresAllHeroesIncludingHumans()
    {
        // Phase 9b #130 P2 — pre-fix filtered out race=0 (humans) to keep map small. This silently
        // reverted deliberate human-resets on next load. Now all heroes are captured.
        _heroRosterAdapter.GetAllAliveHeroRaces().Returns(new List<HeroRaceInfo>
        {
            new HeroRaceInfo("hero_human", 0),
            new HeroRaceInfo("hero_dwarf", 1),
            new HeroRaceInfo("hero_elf", 2)
        });

        _sut.CaptureHeroRaces();

        Assert.AreEqual(3, _sut.CapturedRaceCount);
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
    public void CaptureHeroRaces_AllHumans_StoresAll()
    {
        // Phase 9b #130 P2 — humans are now captured too. Restoring race=0 is the mechanism
        // by which CharacterCreation/Patch3_SetRace/NamedCompanions can deliberately reset a hero
        // to human and have that survive save-load.
        _heroRosterAdapter.GetAllAliveHeroRaces().Returns(new List<HeroRaceInfo>
        {
            new HeroRaceInfo("hero_1", 0),
            new HeroRaceInfo("hero_2", 0)
        });

        _sut.CaptureHeroRaces();

        Assert.AreEqual(2, _sut.CapturedRaceCount);
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

    // Phase 9b #130 R1 — singleton reset on new campaign

    // Phase 9b #171 P1 — validate-before-restore. Save predating a removed race-mod can contain
    // an int ID that no longer corresponds to a valid race. Without the IsValidRaceId guard the
    // bad ID would flow into RaceManager.GetRaceNameFromId → permanent "human" fallback cached
    // for the session, silently breaking lifespan/fertility for elves/dwarves.

    [TestMethod]
    public void RestoreHeroRaces_InvalidSavedRaceId_SkipsAndLeavesCurrent()
    {
        // Hero captured with race=99 (now-removed mod ID). On restore, IsValidRaceId(99)=false →
        // skip SetHeroRace call entirely so the hero keeps its current XML-defined race.
        _heroRosterAdapter.GetAllAliveHeroRaces().Returns(new List<HeroRaceInfo>
        {
            new HeroRaceInfo("hero_removed", 99)
        });
        _sut.CaptureHeroRaces();

        // Now simulate race=99 having been removed from RaceManager (mod uninstalled).
        _raceManager.IsValidRaceId(99).Returns(false);
        _heroRosterAdapter.GetAllAliveHeroRaces().Returns(new List<HeroRaceInfo>
        {
            new HeroRaceInfo("hero_removed", 0) // Currently human at load (XML default)
        });

        _sut.RestoreHeroRaces();

        _heroRosterAdapter.DidNotReceive().SetHeroRace("hero_removed", 99);
    }

    [TestMethod]
    public void RestoreHeroRaces_SavedHumanRace_StillRestores()
    {
        // race=0 (human) is intentionally captured (Phase 9b #130 fix) and must round-trip.
        // The IsValidRaceId guard only fires for non-zero races, so race=0 is always restored.
        _heroRosterAdapter.GetAllAliveHeroRaces().Returns(new List<HeroRaceInfo>
        {
            new HeroRaceInfo("hero_reset_to_human", 0)
        });
        _sut.CaptureHeroRaces();

        _heroRosterAdapter.GetAllAliveHeroRaces().Returns(new List<HeroRaceInfo>
        {
            new HeroRaceInfo("hero_reset_to_human", 2) // Currently elf at load
        });

        _sut.RestoreHeroRaces();

        _heroRosterAdapter.Received(1).SetHeroRace("hero_reset_to_human", 0);
    }

    [TestMethod]
    public void ResetForNewCampaign_WithCapturedRaces_ClearsMap()
    {
        _heroRosterAdapter.GetAllAliveHeroRaces().Returns(new List<HeroRaceInfo>
        {
            new HeroRaceInfo("hero_dwarf", 1)
        });
        _sut.CaptureHeroRaces();
        Assert.AreEqual(1, _sut.CapturedRaceCount);

        _sut.ResetForNewCampaign();

        Assert.AreEqual(0, _sut.CapturedRaceCount);
    }

    [TestMethod]
    public void ResetForNewCampaign_EmptyState_IsNoOp()
    {
        _sut.ResetForNewCampaign();
        Assert.AreEqual(0, _sut.CapturedRaceCount);
    }

    [TestMethod]
    public void RestoreHeroRaces_AfterReset_DoesNothing()
    {
        _heroRosterAdapter.GetAllAliveHeroRaces().Returns(new List<HeroRaceInfo>
        {
            new HeroRaceInfo("hero_dwarf", 1)
        });
        _sut.CaptureHeroRaces();
        _sut.ResetForNewCampaign();

        // After reset, even if the live roster has the same hero, the map is empty and no SetHeroRace fires.
        _heroRosterAdapter.GetAllAliveHeroRaces().Returns(new List<HeroRaceInfo>
        {
            new HeroRaceInfo("hero_dwarf", 0)
        });
        _sut.RestoreHeroRaces();

        _heroRosterAdapter.DidNotReceive().SetHeroRace(Arg.Any<string>(), Arg.Any<int>());
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
        var freshRaceManager = Substitute.For<IRaceManager>();
        freshRaceManager.IsValidRaceId(Arg.Any<int>()).Returns(true);
        var freshService = new RacePersistenceService(freshAdapter, freshRaceManager, Substitute.For<IModLogger>());
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
