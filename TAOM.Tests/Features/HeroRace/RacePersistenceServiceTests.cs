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
        // Issue #330 — identity name mapping (id == index in ordered names) so capture-then-restore
        // tests behave exactly as before the legend was introduced; shift tests override per name.
        _raceManager.GetOrderedRaceNames().Returns(new[] { "human", "dwarf", "elf" });
        _raceManager.IsValidRaceName("human").Returns(true);
        _raceManager.IsValidRaceName("dwarf").Returns(true);
        _raceManager.IsValidRaceName("elf").Returns(true);
        _raceManager.GetRaceIdFromName("human").Returns(0);
        _raceManager.GetRaceIdFromName("dwarf").Returns(1);
        _raceManager.GetRaceIdFromName("elf").Returns(2);
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

    // --- Issue #330 — legend-based (name) restore: robust to skins.xml merge-order changes ---
    //
    // The saved race int is a position index into FaceGen.GetRaceNames() (the merged skins.xml
    // <race> list in module load order). CaptureHeroRaces snapshots that list as a ";"-joined
    // legend so RestoreHeroRaces can translate savedInt -> legend name -> CURRENT id. A reorder /
    // insert / remove between save and load then restores the correct race instead of whatever
    // race now happens to sit at the old index (which IsValidRaceId cannot detect — it's in-range).

    [TestMethod]
    public void RestoreHeroRaces_LegendPresent_ShiftedIndices_TranslatesByName()
    {
        // Save-time: dwarf was id 1 (legend human;dwarf;elf). Load-time: dwarf now sits at id 5.
        var store = new RoundTripDataStore
        {
            IsSaving = false,
            NextLoadDict = new Dictionary<string, int> { ["hero_dwarf"] = 1 },
            NextLoadLegend = "human;dwarf;elf"
        };
        _sut.SyncRaceData(store);
        _raceManager.GetRaceIdFromName("dwarf").Returns(5);
        _heroRosterAdapter.GetAllAliveHeroRaces().Returns(new List<HeroRaceInfo>
        {
            new HeroRaceInfo("hero_dwarf", 1) // engine loaded the hero with the stale index
        });

        _sut.RestoreHeroRaces();

        _heroRosterAdapter.Received(1).SetHeroRace("hero_dwarf", 5);
        _heroRosterAdapter.DidNotReceive().SetHeroRace("hero_dwarf", 1);
    }

    [TestMethod]
    public void RestoreHeroRaces_LegendPresent_RemovedRaceName_SkipsAndWarns()
    {
        var store = new RoundTripDataStore
        {
            IsSaving = false,
            NextLoadDict = new Dictionary<string, int> { ["hero_elf"] = 2 },
            NextLoadLegend = "human;dwarf;elf"
        };
        _sut.SyncRaceData(store);
        _raceManager.IsValidRaceName("elf").Returns(false); // race removed from the module set
        _heroRosterAdapter.GetAllAliveHeroRaces().Returns(new List<HeroRaceInfo>
        {
            new HeroRaceInfo("hero_elf", 0)
        });

        _sut.RestoreHeroRaces();

        _heroRosterAdapter.DidNotReceive().SetHeroRace(Arg.Any<string>(), Arg.Any<int>());
        _logger.Received().LogWarning(Arg.Is<string>(s => s.Contains("elf")));
        // Validate-before-lookup: GetRaceIdFromName falls back to 0/human — must not be consulted
        // for an invalid name, or the fallback would silently restore the hero as human.
        _raceManager.DidNotReceive().GetRaceIdFromName("elf");
    }

    [TestMethod]
    public void RestoreHeroRaces_LegendPresent_SavedIntOutOfLegendRange_SkipsAndWarns()
    {
        var store = new RoundTripDataStore
        {
            IsSaving = false,
            NextLoadDict = new Dictionary<string, int> { ["hero_corrupt"] = 7 },
            NextLoadLegend = "human;dwarf"
        };
        _sut.SyncRaceData(store);
        _heroRosterAdapter.GetAllAliveHeroRaces().Returns(new List<HeroRaceInfo>
        {
            new HeroRaceInfo("hero_corrupt", 0)
        });

        _sut.RestoreHeroRaces();

        _heroRosterAdapter.DidNotReceive().SetHeroRace(Arg.Any<string>(), Arg.Any<int>());
        _logger.Received().LogWarning(Arg.Is<string>(s => s.Contains("hero_corrupt")));
    }

    [TestMethod]
    public void RestoreHeroRaces_LegendPresent_TranslatedIdMatchesCurrent_DoesNotSetRace()
    {
        var store = new RoundTripDataStore
        {
            IsSaving = false,
            NextLoadDict = new Dictionary<string, int> { ["hero_dwarf"] = 1 },
            NextLoadLegend = "human;dwarf;elf"
        };
        _sut.SyncRaceData(store);
        _heroRosterAdapter.GetAllAliveHeroRaces().Returns(new List<HeroRaceInfo>
        {
            new HeroRaceInfo("hero_dwarf", 1) // identity mapping: dwarf still resolves to id 1
        });

        _sut.RestoreHeroRaces();

        _heroRosterAdapter.DidNotReceive().SetHeroRace(Arg.Any<string>(), Arg.Any<int>());
    }

    [TestMethod]
    public void CaptureHeroRaces_SetsLegendFromOrderedRaceNames()
    {
        _heroRosterAdapter.GetAllAliveHeroRaces().Returns(new List<HeroRaceInfo>
        {
            new HeroRaceInfo("hero_dwarf", 1)
        });

        _sut.CaptureHeroRaces();
        var store = new RoundTripDataStore { IsSaving = true };
        _sut.SyncRaceData(store);

        Assert.AreEqual("human;dwarf;elf", store.LastSavedLegend);
    }

    // --- Multiplayer field report 2026-08-03 §1 — degenerate-legend capture guard ---
    //
    // A headless co-op host running WITHOUT TAOM's modules has exactly one race in its FaceGen:
    // "human". Every hero there reports race 0, so an unguarded capture writes legend="human" and
    // {every hero: 0}. That map then rides the host->client save transfer, and RestoreHeroRaces on
    // a full 15-race client takes the legend path, resolves "human" to a VALID current id 0, and
    // force-sets every hero in the world to human. The save-time validation cannot catch it: every
    // value in it is individually well-formed. Only the race COUNT betrays the degenerate source.

    [TestMethod]
    public void CaptureHeroRaces_OneRaceLegend_DoesNotOverwriteRicherCapturedData()
    {
        _heroRosterAdapter.GetAllAliveHeroRaces().Returns(new List<HeroRaceInfo>
        {
            new HeroRaceInfo("hero_dwarf", 1),
            new HeroRaceInfo("hero_elf", 2)
        });
        _sut.CaptureHeroRaces();
        Assert.AreEqual(2, _sut.CapturedRaceCount, "precondition: rich capture succeeded");

        // Now the degenerate host: one race in FaceGen, so every hero reads back as human.
        _raceManager.GetOrderedRaceNames().Returns(new[] { "human" });
        _heroRosterAdapter.GetAllAliveHeroRaces().Returns(new List<HeroRaceInfo>
        {
            new HeroRaceInfo("hero_dwarf", 0),
            new HeroRaceInfo("hero_elf", 0)
        });

        _sut.CaptureHeroRaces();

        // Assert on the PERSISTED legend, not the entry count: the degenerate capture writes the
        // same NUMBER of entries (all zeroed), so a count assertion passes either way.
        var store = new RoundTripDataStore { IsSaving = true };
        _sut.SyncRaceData(store);
        Assert.AreEqual("human;dwarf;elf", store.LastSavedLegend,
            "a one-race legend must not replace the richer one — that is the mass-humanize vector");
    }

    [TestMethod]
    public void CaptureHeroRaces_OneRaceLegend_ThenRestore_DoesNotMassHumanizeHeroes()
    {
        _heroRosterAdapter.GetAllAliveHeroRaces().Returns(new List<HeroRaceInfo>
        {
            new HeroRaceInfo("hero_dwarf", 1),
            new HeroRaceInfo("hero_elf", 2)
        });
        _sut.CaptureHeroRaces();

        _raceManager.GetOrderedRaceNames().Returns(new[] { "human" });
        _heroRosterAdapter.GetAllAliveHeroRaces().Returns(new List<HeroRaceInfo>
        {
            new HeroRaceInfo("hero_dwarf", 0),
            new HeroRaceInfo("hero_elf", 0)
        });
        _sut.CaptureHeroRaces();

        // Back on a full client: heroes load at 0 and must be restored to their real races.
        _raceManager.GetOrderedRaceNames().Returns(new[] { "human", "dwarf", "elf" });
        _sut.RestoreHeroRaces();

        _heroRosterAdapter.Received(1).SetHeroRace("hero_dwarf", 1);
        _heroRosterAdapter.Received(1).SetHeroRace("hero_elf", 2);
        _heroRosterAdapter.DidNotReceive().SetHeroRace("hero_dwarf", 0);
        _heroRosterAdapter.DidNotReceive().SetHeroRace("hero_elf", 0);
    }

    [TestMethod]
    public void CaptureHeroRaces_OneRaceLegend_LogsWarning()
    {
        _raceManager.GetOrderedRaceNames().Returns(new[] { "human" });
        _heroRosterAdapter.GetAllAliveHeroRaces().Returns(new List<HeroRaceInfo>
        {
            new HeroRaceInfo("hero_dwarf", 0)
        });

        _sut.CaptureHeroRaces();

        _logger.Received().LogWarning(Arg.Is<string>(m => m.Contains("race")));
    }

    [TestMethod]
    public void CaptureHeroRaces_OneRaceLegend_EmptyPriorState_StaysEmpty()
    {
        // A genuinely one-race world with nothing captured yet: skipping leaves the map empty, so
        // RestoreHeroRaces takes its "no saved data" path and heroes keep their XML races.
        _raceManager.GetOrderedRaceNames().Returns(new[] { "human" });
        _heroRosterAdapter.GetAllAliveHeroRaces().Returns(new List<HeroRaceInfo>
        {
            new HeroRaceInfo("hero_a", 0)
        });

        _sut.CaptureHeroRaces();

        Assert.AreEqual(0, _sut.CapturedRaceCount);
        _sut.RestoreHeroRaces();
        _heroRosterAdapter.DidNotReceive().SetHeroRace(Arg.Any<string>(), Arg.Any<int>());
    }

    [TestMethod]
    public void CaptureHeroRaces_EmptyRaceList_DoesNotOverwriteRicherCapturedData()
    {
        // Same guard, degenerate in the other direction: a race manager that failed to initialise
        // at all reports zero races. Capturing then would write an EMPTY legend, which RestoreHeroRaces
        // reads as "pre-#330 save" and falls through to the raw-index path — silently reinterpreting
        // every saved value against whatever race order the next session happens to have.
        _heroRosterAdapter.GetAllAliveHeroRaces().Returns(new List<HeroRaceInfo>
        {
            new HeroRaceInfo("hero_dwarf", 1)
        });
        _sut.CaptureHeroRaces();

        _raceManager.GetOrderedRaceNames().Returns(new string[0]);
        _sut.CaptureHeroRaces();

        var store = new RoundTripDataStore { IsSaving = true };
        _sut.SyncRaceData(store);
        Assert.AreEqual("human;dwarf;elf", store.LastSavedLegend,
            "an empty race list must not blank the legend — an empty legend silently downgrades " +
            "restore to the pre-#330 raw-index path");
    }

    // Issue #330 — clear-on-load. SyncData with an absent key leaves the ref value unchanged, so
    // loading an older-format (or pre-TAOM) save after a newer session in the same process would
    // otherwise restore the PREVIOUS campaign's data onto colliding StringIds (#130-R1 bug class,
    // previously only fixed for new campaigns via ResetForNewCampaign).

    [TestMethod]
    public void SyncRaceData_Loading_AbsentKeys_ClearsStaleState()
    {
        _heroRosterAdapter.GetAllAliveHeroRaces().Returns(new List<HeroRaceInfo>
        {
            new HeroRaceInfo("hero_dwarf", 1),
            new HeroRaceInfo("hero_elf", 2)
        });
        _sut.CaptureHeroRaces();
        Assert.AreEqual(2, _sut.CapturedRaceCount);

        // Load a save that predates this feature entirely: neither key present.
        _sut.SyncRaceData(new RoundTripDataStore { IsSaving = false });

        Assert.AreEqual(0, _sut.CapturedRaceCount);
        _sut.RestoreHeroRaces();
        _heroRosterAdapter.DidNotReceive().SetHeroRace(Arg.Any<string>(), Arg.Any<int>());
    }

    [TestMethod]
    public void SyncRaceData_Saving_DoesNotClear()
    {
        _heroRosterAdapter.GetAllAliveHeroRaces().Returns(new List<HeroRaceInfo>
        {
            new HeroRaceInfo("hero_dwarf", 1)
        });
        _sut.CaptureHeroRaces();

        var store = new RoundTripDataStore { IsSaving = true };
        _sut.SyncRaceData(store);

        Assert.AreEqual(1, _sut.CapturedRaceCount);
        Assert.AreEqual(1, store.LastSavedDict["hero_dwarf"]);
    }

    [TestMethod]
    public void ResetForNewCampaign_ClearsLegend()
    {
        _heroRosterAdapter.GetAllAliveHeroRaces().Returns(new List<HeroRaceInfo>
        {
            new HeroRaceInfo("hero_dwarf", 1)
        });
        _sut.CaptureHeroRaces();

        _sut.ResetForNewCampaign();
        var store = new RoundTripDataStore { IsSaving = true };
        _sut.SyncRaceData(store);

        Assert.AreEqual("", store.LastSavedLegend);
    }

    // Issue #330 — legacy path: a save written before the legend existed restores by raw int,
    // byte-for-byte today's behavior (incl. the #171 IsValidRaceId guard and the race-0 bypass).

    [TestMethod]
    public void RestoreHeroRaces_NoLegend_LegacyIntPath_RestoresRawInt()
    {
        var store = new RoundTripDataStore
        {
            IsSaving = false,
            NextLoadDict = new Dictionary<string, int> { ["hero_dwarf"] = 1 }
            // NextLoadLegend deliberately absent — pre-#330 save
        };
        _sut.SyncRaceData(store);
        _heroRosterAdapter.GetAllAliveHeroRaces().Returns(new List<HeroRaceInfo>
        {
            new HeroRaceInfo("hero_dwarf", 0)
        });

        _sut.RestoreHeroRaces();

        _heroRosterAdapter.Received(1).SetHeroRace("hero_dwarf", 1);
    }

    [TestMethod]
    public void RestoreHeroRaces_NoLegend_InvalidSavedRaceId_SkipsAndLeavesCurrent()
    {
        var store = new RoundTripDataStore
        {
            IsSaving = false,
            NextLoadDict = new Dictionary<string, int> { ["hero_removed"] = 99 }
        };
        _sut.SyncRaceData(store);
        _raceManager.IsValidRaceId(99).Returns(false);
        _heroRosterAdapter.GetAllAliveHeroRaces().Returns(new List<HeroRaceInfo>
        {
            new HeroRaceInfo("hero_removed", 0)
        });

        _sut.RestoreHeroRaces();

        _heroRosterAdapter.DidNotReceive().SetHeroRace(Arg.Any<string>(), Arg.Any<int>());
    }

    [TestMethod]
    public void RestoreHeroRaces_NoLegend_SavedHumanRace_StillRestores()
    {
        var store = new RoundTripDataStore
        {
            IsSaving = false,
            NextLoadDict = new Dictionary<string, int> { ["hero_reset_to_human"] = 0 }
        };
        _sut.SyncRaceData(store);
        _heroRosterAdapter.GetAllAliveHeroRaces().Returns(new List<HeroRaceInfo>
        {
            new HeroRaceInfo("hero_reset_to_human", 2)
        });

        _sut.RestoreHeroRaces();

        _heroRosterAdapter.Received(1).SetHeroRace("hero_reset_to_human", 0);
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
        Assert.AreEqual("human;dwarf;elf", savingStore.LastSavedLegend, "Legend must be in the save snapshot (#330)");

        // Step 4 — Load: NEW service instance (simulates Bannerlord process restart),
        // SyncData (loading) populates the new instance's map + legend from the persisted snapshot.
        // Issue #330 — the load-side race list has SHIFTED (a race was inserted before the
        // LOTRLOME block): dwarf is now id 4, elf id 7. Restore must follow the names.
        var freshAdapter = Substitute.For<IHeroRosterAdapter>();
        var freshRaceManager = Substitute.For<IRaceManager>();
        freshRaceManager.IsValidRaceName("dwarf").Returns(true);
        freshRaceManager.IsValidRaceName("elf").Returns(true);
        freshRaceManager.GetRaceIdFromName("dwarf").Returns(4);
        freshRaceManager.GetRaceIdFromName("elf").Returns(7);
        var freshService = new RacePersistenceService(freshAdapter, freshRaceManager, Substitute.For<IModLogger>());
        var loadingStore = new RoundTripDataStore
        {
            IsSaving = false,
            NextLoadDict = savedSnapshot,
            NextLoadLegend = savingStore.LastSavedLegend
        };
        freshService.SyncRaceData(loadingStore);
        Assert.AreEqual(2, freshService.CapturedRaceCount, "Fresh instance must rehydrate the snapshot");

        // Step 5 — OnSessionLaunched restore: heroes are loaded with race=0 (vanilla), Restore
        // re-applies the captured races translated through the legend to the CURRENT ids.
        freshAdapter.GetAllAliveHeroRaces().Returns(new List<HeroRaceInfo>
        {
            new HeroRaceInfo(playerId, 0),
            new HeroRaceInfo("npc_dwarf", 0)
        });
        freshService.RestoreHeroRaces();

        freshAdapter.Received(1).SetHeroRace(playerId, 7);
        freshAdapter.Received(1).SetHeroRace("npc_dwarf", 4);
    }
}

/// <summary>
/// Minimal hand-rolled IDataStore stub for the Phase 9b #181 round-trip + #330 legend tests.
/// NSubstitute can't easily model ref-parameter `SyncData<T>(string, ref T)` with the Do-callback
/// pattern, so we capture the saved values on save and re-inject them on load directly. Like the
/// engine, an absent key on load (null Next*) leaves the ref value unchanged and returns false.
/// </summary>
internal class RoundTripDataStore : IDataStore
{
    public bool IsSaving { get; set; }
    public bool IsLoading => !IsSaving;
    public Dictionary<string, int> LastSavedDict { get; private set; }
    public Dictionary<string, int> NextLoadDict { get; set; }
    public string LastSavedLegend { get; private set; }
    public string NextLoadLegend { get; set; }

    public bool SyncData<T>(string key, ref T data)
    {
        if (IsSaving)
        {
            if (data is Dictionary<string, int> dict)
            {
                LastSavedDict = new Dictionary<string, int>(dict);
                return true;
            }
            if (data is string legend)
            {
                LastSavedLegend = legend;
                return true;
            }
            return false;
        }
        if (typeof(T) == typeof(Dictionary<string, int>) && NextLoadDict != null)
        {
            data = (T)(object)NextLoadDict;
            return true;
        }
        if (typeof(T) == typeof(string) && NextLoadLegend != null)
        {
            data = (T)(object)NextLoadLegend;
            return true;
        }
        return false;
    }
}
