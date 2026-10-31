using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Adapters;
using TAOM.Core.Domain;
using TAOM.Core.Logging;
using TAOM.Features.HeroRace;
using TAOM.Features.NazgulFamily;
using TaleWorlds.CampaignSystem;

namespace TAOM.Tests.Features.HeroRace;

[TestClass]
public class RacePersistenceServiceTests
{
    private RacePersistenceService _sut;
    private IHeroRosterAdapter _heroRosterAdapter;
    private IRaceManager _raceManager;
    private IModLogger _logger;
    private INazgulRegistry _nazgul;
    private IOutcomeLog _outcomes;

    [TestInitialize]
    public void Setup()
    {
        _heroRosterAdapter = Substitute.For<IHeroRosterAdapter>();
        _raceManager = Substitute.For<IRaceManager>();
        _logger = Substitute.For<IModLogger>();
        // #644: no hero is a wraith unless a test says so, so every pre-#644 test runs unchanged.
        _nazgul = Substitute.For<INazgulRegistry>();
        _outcomes = Substitute.For<IOutcomeLog>();
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
        _sut = new RacePersistenceService(_heroRosterAdapter, _raceManager, _logger, _nazgul, _outcomes);
    }

    [TestMethod]
    public void CaptureHeroRaces_StoresAllHeroesIncludingHumans()
    {
        // Phase 9b #130 P2 — pre-fix filtered out race=0 (humans) to keep map small. This silently
        // reverted deliberate human-resets on next load. Now all heroes are captured.
        _heroRosterAdapter.GetAllHeroRaces().Returns(new List<HeroRaceInfo>
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
        _heroRosterAdapter.GetAllHeroRaces().Returns(new List<HeroRaceInfo>
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
        _heroRosterAdapter.GetAllHeroRaces().Returns(new List<HeroRaceInfo>());

        _sut.CaptureHeroRaces();

        Assert.AreEqual(0, _sut.CapturedRaceCount);
    }

    [TestMethod]
    public void CaptureHeroRaces_AllHumans_StoresAll()
    {
        // Phase 9b #130 P2 — humans are now captured too. Restoring race=0 is the mechanism
        // by which CharacterCreation/Patch3_SetRace/NamedCompanions can deliberately reset a hero
        // to human and have that survive save-load.
        _heroRosterAdapter.GetAllHeroRaces().Returns(new List<HeroRaceInfo>
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

    // A new campaign has no race data by design, so the empty map is not worth a warning there.
    [TestMethod]
    public void RestoreHeroRaces_NewCampaign_LogsInfoNotWarning()
    {
        _sut.ResetForNewCampaign();

        _sut.RestoreHeroRaces();

        _logger.DidNotReceive().LogWarning(Arg.Any<string>());
        _logger.Received(1).LogInfo(Arg.Is<string>(m => m.Contains("new campaign")));
    }

    [TestMethod]
    public void RestoreHeroRaces_LoadedSaveWithoutRaceData_Warns()
    {
        _sut.SyncRaceData(new RoundTripDataStore { IsSaving = false });

        _sut.RestoreHeroRaces();

        _logger.Received(1).LogWarning(Arg.Is<string>(m => m.Contains("No saved race data")));
    }

    // #1035: the behavior's constructor resets the singleton for every campaign, so a pre-TAOM save loaded after another
    // campaign in the same process neither inherits that campaign's races nor its new-campaign flag.
    [TestMethod]
    public void ResetForNewSession_AfterAPreviousCampaign_LeavesNoRacesAndWarnsOnAnEmptyRestore()
    {
        _heroRosterAdapter.GetAllHeroRaces().Returns(new List<HeroRaceInfo> { new HeroRaceInfo("lord_1_1", 3) });
        _sut.ResetForNewCampaign();
        _sut.CaptureHeroRaces();
        _heroRosterAdapter.ClearReceivedCalls();

        _sut.ResetForNewSession();
        _sut.RestoreHeroRaces();

        Assert.AreEqual(0, _sut.CapturedRaceCount);
        _heroRosterAdapter.DidNotReceive().SetHeroRace(Arg.Any<string>(), Arg.Any<int>());
        _logger.Received(1).LogWarning(Arg.Is<string>(m => m.Contains("No saved race data")));
    }

    // A save with no RacePersistenceBehavior record: the engine calls SyncData only for a behavior the save holds a
    // record for (CampaignBehaviorDataStore.LoadBehaviorData), so a pre-TAOM save reaches the restore with neither call.
    [TestMethod]
    public void RestoreHeroRaces_WithoutResetOrSync_Warns()
    {
        _sut.RestoreHeroRaces();

        _logger.Received(1).LogWarning(Arg.Is<string>(m => m.Contains("No saved race data")));
        _logger.DidNotReceive().LogInfo(Arg.Is<string>(m => m.Contains("new campaign")));
    }

    [TestMethod]
    public void RestoreHeroRaces_LoadAfterNewCampaignInSameProcess_Warns()
    {
        _sut.ResetForNewCampaign();
        _sut.SyncRaceData(new RoundTripDataStore { IsSaving = false });

        _sut.RestoreHeroRaces();

        _logger.Received(1).LogWarning(Arg.Is<string>(m => m.Contains("No saved race data")));
    }

    [TestMethod]
    public void RestoreHeroRaces_RestoresRaceForCapturedHeroes()
    {
        _heroRosterAdapter.GetAllHeroRaces().Returns(new List<HeroRaceInfo>
        {
            new HeroRaceInfo("hero_dwarf", 1)
        });

        _sut.CaptureHeroRaces();

        _heroRosterAdapter.GetAllHeroRaces().Returns(new List<HeroRaceInfo>
        {
            new HeroRaceInfo("hero_dwarf", 0)
        });

        _sut.RestoreHeroRaces();

        _heroRosterAdapter.Received(1).SetHeroRace("hero_dwarf", 1);
    }

    [TestMethod]
    public void RestoreHeroRaces_SkipsHeroesNotInSavedMap()
    {
        _heroRosterAdapter.GetAllHeroRaces().Returns(new List<HeroRaceInfo>
        {
            new HeroRaceInfo("hero_dwarf", 1)
        });

        _sut.CaptureHeroRaces();

        _heroRosterAdapter.GetAllHeroRaces().Returns(new List<HeroRaceInfo>
        {
            new HeroRaceInfo("hero_new", 0)
        });

        _sut.RestoreHeroRaces();

        _heroRosterAdapter.DidNotReceive().SetHeroRace("hero_new", Arg.Any<int>());
    }

    [TestMethod]
    public void RestoreHeroRaces_SkipsHeroesWhoseRaceAlreadyMatches()
    {
        _heroRosterAdapter.GetAllHeroRaces().Returns(new List<HeroRaceInfo>
        {
            new HeroRaceInfo("hero_dwarf", 1)
        });

        _sut.CaptureHeroRaces();

        _heroRosterAdapter.GetAllHeroRaces().Returns(new List<HeroRaceInfo>
        {
            new HeroRaceInfo("hero_dwarf", 1)
        });

        _sut.RestoreHeroRaces();

        _heroRosterAdapter.DidNotReceive().SetHeroRace(Arg.Any<string>(), Arg.Any<int>());
    }

    [TestMethod]
    public void RestoreHeroRaces_LogsRestoredCount()
    {
        _heroRosterAdapter.GetAllHeroRaces().Returns(new List<HeroRaceInfo>
        {
            new HeroRaceInfo("hero_dwarf", 1),
            new HeroRaceInfo("hero_elf", 2)
        });

        _sut.CaptureHeroRaces();

        _heroRosterAdapter.GetAllHeroRaces().Returns(new List<HeroRaceInfo>
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
        _heroRosterAdapter.GetAllHeroRaces().Returns(new List<HeroRaceInfo>
        {
            new HeroRaceInfo("hero_removed", 99)
        });
        _sut.CaptureHeroRaces();

        // Now simulate race=99 having been removed from RaceManager (mod uninstalled).
        _raceManager.IsValidRaceId(99).Returns(false);
        _heroRosterAdapter.GetAllHeroRaces().Returns(new List<HeroRaceInfo>
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
        _heroRosterAdapter.GetAllHeroRaces().Returns(new List<HeroRaceInfo>
        {
            new HeroRaceInfo("hero_reset_to_human", 0)
        });
        _sut.CaptureHeroRaces();

        _heroRosterAdapter.GetAllHeroRaces().Returns(new List<HeroRaceInfo>
        {
            new HeroRaceInfo("hero_reset_to_human", 2) // Currently elf at load
        });

        _sut.RestoreHeroRaces();

        _heroRosterAdapter.Received(1).SetHeroRace("hero_reset_to_human", 0);
    }

    [TestMethod]
    public void ResetForNewCampaign_WithCapturedRaces_ClearsMap()
    {
        _heroRosterAdapter.GetAllHeroRaces().Returns(new List<HeroRaceInfo>
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
        _heroRosterAdapter.GetAllHeroRaces().Returns(new List<HeroRaceInfo>
        {
            new HeroRaceInfo("hero_dwarf", 1)
        });
        _sut.CaptureHeroRaces();
        _sut.ResetForNewCampaign();

        // After reset, even if the live roster has the same hero, the map is empty and no SetHeroRace fires.
        _heroRosterAdapter.GetAllHeroRaces().Returns(new List<HeroRaceInfo>
        {
            new HeroRaceInfo("hero_dwarf", 0)
        });
        _sut.RestoreHeroRaces();

        _heroRosterAdapter.DidNotReceive().SetHeroRace(Arg.Any<string>(), Arg.Any<int>());
    }

    [TestMethod]
    public void CaptureHeroRaces_DuplicateStringIds_StoresFirstOnly()
    {
        _heroRosterAdapter.GetAllHeroRaces().Returns(new List<HeroRaceInfo>
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
        _heroRosterAdapter.GetAllHeroRaces().Returns(new List<HeroRaceInfo>
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
        _heroRosterAdapter.GetAllHeroRaces().Returns(new List<HeroRaceInfo>
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
        _heroRosterAdapter.GetAllHeroRaces().Returns(new List<HeroRaceInfo>
        {
            new HeroRaceInfo("hero_corrupt", 0)
        });

        _sut.RestoreHeroRaces();

        _heroRosterAdapter.DidNotReceive().SetHeroRace(Arg.Any<string>(), Arg.Any<int>());
        _logger.Received().LogWarning(Arg.Is<string>(s => s.Contains("hero_corrupt")));
        // 1011.toml fails a load on this text, because the [Check] line itself passes (the hero is skipped).
        _logger.Received().LogWarning(Arg.Is<string>(s => s.Contains("is outside the save")));
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
        _heroRosterAdapter.GetAllHeroRaces().Returns(new List<HeroRaceInfo>
        {
            new HeroRaceInfo("hero_dwarf", 1) // identity mapping: dwarf still resolves to id 1
        });

        _sut.RestoreHeroRaces();

        _heroRosterAdapter.DidNotReceive().SetHeroRace(Arg.Any<string>(), Arg.Any<int>());
    }

    [TestMethod]
    public void CaptureHeroRaces_SetsLegendFromOrderedRaceNames()
    {
        _heroRosterAdapter.GetAllHeroRaces().Returns(new List<HeroRaceInfo>
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
        _heroRosterAdapter.GetAllHeroRaces().Returns(new List<HeroRaceInfo>
        {
            new HeroRaceInfo("hero_dwarf", 1),
            new HeroRaceInfo("hero_elf", 2)
        });
        _sut.CaptureHeroRaces();
        Assert.AreEqual(2, _sut.CapturedRaceCount, "precondition: rich capture succeeded");

        // Now the degenerate host: one race in FaceGen, so every hero reads back as human.
        _raceManager.GetOrderedRaceNames().Returns(new[] { "human" });
        _heroRosterAdapter.GetAllHeroRaces().Returns(new List<HeroRaceInfo>
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
        _heroRosterAdapter.GetAllHeroRaces().Returns(new List<HeroRaceInfo>
        {
            new HeroRaceInfo("hero_dwarf", 1),
            new HeroRaceInfo("hero_elf", 2)
        });
        _sut.CaptureHeroRaces();

        _raceManager.GetOrderedRaceNames().Returns(new[] { "human" });
        _heroRosterAdapter.GetAllHeroRaces().Returns(new List<HeroRaceInfo>
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
        _heroRosterAdapter.GetAllHeroRaces().Returns(new List<HeroRaceInfo>
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
        _heroRosterAdapter.GetAllHeroRaces().Returns(new List<HeroRaceInfo>
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
        _heroRosterAdapter.GetAllHeroRaces().Returns(new List<HeroRaceInfo>
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
        _heroRosterAdapter.GetAllHeroRaces().Returns(new List<HeroRaceInfo>
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
        _heroRosterAdapter.GetAllHeroRaces().Returns(new List<HeroRaceInfo>
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
        _heroRosterAdapter.GetAllHeroRaces().Returns(new List<HeroRaceInfo>
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
        _heroRosterAdapter.GetAllHeroRaces().Returns(new List<HeroRaceInfo>
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
        _heroRosterAdapter.GetAllHeroRaces().Returns(new List<HeroRaceInfo>
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
        _heroRosterAdapter.GetAllHeroRaces().Returns(new List<HeroRaceInfo>
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
        _heroRosterAdapter.GetAllHeroRaces().Returns(new List<HeroRaceInfo>
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
        var freshService = new RacePersistenceService(freshAdapter, freshRaceManager, Substitute.For<IModLogger>(),
            Substitute.For<INazgulRegistry>(), Substitute.For<IOutcomeLog>());
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
        freshAdapter.GetAllHeroRaces().Returns(new List<HeroRaceInfo>
        {
            new HeroRaceInfo(playerId, 0),
            new HeroRaceInfo("npc_dwarf", 0)
        });
        freshService.RestoreHeroRaces();

        freshAdapter.Received(1).SetHeroRace(playerId, 7);
        freshAdapter.Received(1).SetHeroRace("npc_dwarf", 4);
    }

    // --- #644: the Nine keep the race their XML gives them ---
    //
    // Vanilla never saves Race, so a save written before #644 carries the Nine's OLD race (human
    // for six, uruk for three) in the captured map. Restoring it would undo the data change on
    // every existing campaign. A wraith's race is fixed by its XML, so the restore leaves it alone.

    [TestMethod]
    public void RestoreHeroRaces_Wraith_LegendPath_KeepsTheXmlRace()
    {
        _nazgul.IsWraith("lord_1_15").Returns(true);
        var store = new RoundTripDataStore
        {
            IsSaving = false,
            NextLoadDict = new Dictionary<string, int> { ["lord_1_15"] = 0 }, // saved as human
            NextLoadLegend = "human;dwarf;elf"
        };
        _sut.SyncRaceData(store);
        _heroRosterAdapter.GetAllHeroRaces().Returns(new List<HeroRaceInfo>
        {
            new HeroRaceInfo("lord_1_15", 2) // the race the XML gave him this session
        });

        _sut.RestoreHeroRaces();

        _heroRosterAdapter.DidNotReceive().SetHeroRace(Arg.Any<string>(), Arg.Any<int>());
    }

    [TestMethod]
    public void RestoreHeroRaces_Wraith_LegacyPath_KeepsTheXmlRace()
    {
        _nazgul.IsWraith("lord_1_48_1").Returns(true);
        var store = new RoundTripDataStore
        {
            IsSaving = false,
            NextLoadDict = new Dictionary<string, int> { ["lord_1_48_1"] = 1 }
            // NextLoadLegend deliberately absent: a pre-#330 save
        };
        _sut.SyncRaceData(store);
        _heroRosterAdapter.GetAllHeroRaces().Returns(new List<HeroRaceInfo>
        {
            new HeroRaceInfo("lord_1_48_1", 2)
        });

        _sut.RestoreHeroRaces();

        _heroRosterAdapter.DidNotReceive().SetHeroRace(Arg.Any<string>(), Arg.Any<int>());
    }

    [TestMethod]
    public void RestoreHeroRaces_WraithBesideNonWraith_RestoresOnlyTheNonWraith()
    {
        _nazgul.IsWraith("lord_1_15").Returns(true);
        var store = new RoundTripDataStore
        {
            IsSaving = false,
            NextLoadDict = new Dictionary<string, int> { ["lord_1_15"] = 0, ["hero_dwarf"] = 1 },
            NextLoadLegend = "human;dwarf;elf"
        };
        _sut.SyncRaceData(store);
        _heroRosterAdapter.GetAllHeroRaces().Returns(new List<HeroRaceInfo>
        {
            new HeroRaceInfo("lord_1_15", 2),
            new HeroRaceInfo("hero_dwarf", 0)
        });

        _sut.RestoreHeroRaces();

        _heroRosterAdapter.Received(1).SetHeroRace("hero_dwarf", 1);
        _heroRosterAdapter.DidNotReceive().SetHeroRace("lord_1_15", Arg.Any<int>());
    }

    [TestMethod]
    public void RestoreHeroRaces_Wraith_LogsHowManyKeptTheirXmlRace()
    {
        _nazgul.IsWraith("lord_1_15").Returns(true);
        _nazgul.IsWraith("lord_1_16").Returns(true);
        var store = new RoundTripDataStore
        {
            IsSaving = false,
            NextLoadDict = new Dictionary<string, int> { ["lord_1_15"] = 0, ["lord_1_16"] = 0 },
            NextLoadLegend = "human;dwarf;elf"
        };
        _sut.SyncRaceData(store);
        _heroRosterAdapter.GetAllHeroRaces().Returns(new List<HeroRaceInfo>
        {
            new HeroRaceInfo("lord_1_15", 2),
            new HeroRaceInfo("lord_1_16", 2)
        });

        _sut.RestoreHeroRaces();

        _logger.Received(1).LogInfo(Arg.Is<string>(s => s.Contains("kept the XML race for 2 Ringwraiths")));
    }

    // --- #1011: dead and disabled heroes. The engine resets CharacterObject.Race from the template
    // for every non-original character on load, dead ones included, so the roster the adapter
    // returns holds every hero character and the service treats a dead hero like any other entry.

    [TestMethod]
    public void CaptureHeroRaces_DeadHeroInRoster_StoresItsRace()
    {
        _heroRosterAdapter.GetAllHeroRaces().Returns(new List<HeroRaceInfo>
        {
            new HeroRaceInfo("hero_alive", 0),
            new HeroRaceInfo("hero_dead_elf", 2)
        });

        _sut.CaptureHeroRaces();

        Assert.AreEqual(2, _sut.CapturedRaceCount);
        var store = new RoundTripDataStore { IsSaving = true };
        _sut.SyncRaceData(store);
        Assert.AreEqual(2, store.LastSavedDict["hero_dead_elf"]);
    }

    [TestMethod]
    public void RestoreHeroRaces_DeadHeroRaceDiffers_SetsSavedRace()
    {
        var store = new RoundTripDataStore
        {
            IsSaving = false,
            NextLoadDict = new Dictionary<string, int> { ["hero_dead_dwarf"] = 1 },
            NextLoadLegend = "human;dwarf;elf"
        };
        _sut.SyncRaceData(store);
        _heroRosterAdapter.GetAllHeroRaces().Returns(new List<HeroRaceInfo>
        {
            new HeroRaceInfo("hero_dead_dwarf", 0) // reverted to the template race on load
        });

        _sut.RestoreHeroRaces();

        _heroRosterAdapter.Received(1).SetHeroRace("hero_dead_dwarf", 1);
    }

    [TestMethod]
    public void RestoreHeroRaces_DeadWraith_KeepsTheXmlRace()
    {
        _nazgul.IsWraith("lord_1_15").Returns(true);
        var store = new RoundTripDataStore
        {
            IsSaving = false,
            NextLoadDict = new Dictionary<string, int> { ["lord_1_15"] = 0 },
            NextLoadLegend = "human;dwarf;elf"
        };
        _sut.SyncRaceData(store);
        _heroRosterAdapter.GetAllHeroRaces().Returns(new List<HeroRaceInfo>
        {
            new HeroRaceInfo("lord_1_15", 2)
        });

        _sut.RestoreHeroRaces();

        _heroRosterAdapter.DidNotReceive().SetHeroRace(Arg.Any<string>(), Arg.Any<int>());
    }

    [TestMethod]
    public void RestoreHeroRaces_DeadHeroAbsentFromOldSave_DoesNotSetRace()
    {
        // A hero who died before #1011 was never captured: keep the reverted race, touch nothing.
        var store = new RoundTripDataStore
        {
            IsSaving = false,
            NextLoadDict = new Dictionary<string, int> { ["hero_alive"] = 1 },
            NextLoadLegend = "human;dwarf;elf"
        };
        _sut.SyncRaceData(store);
        _heroRosterAdapter.GetAllHeroRaces().Returns(new List<HeroRaceInfo>
        {
            new HeroRaceInfo("hero_alive", 1),
            new HeroRaceInfo("hero_died_before_fix", 0)
        });

        _sut.RestoreHeroRaces();

        _heroRosterAdapter.DidNotReceive().SetHeroRace("hero_died_before_fix", Arg.Any<int>());
    }

    [TestMethod]
    public void RestoreHeroRaces_DeadHeroRemovedRaceName_SkipsAndWarns()
    {
        _raceManager.IsValidRaceName("elf").Returns(false);
        var store = new RoundTripDataStore
        {
            IsSaving = false,
            NextLoadDict = new Dictionary<string, int> { ["hero_dead_elf"] = 2 },
            NextLoadLegend = "human;dwarf;elf"
        };
        _sut.SyncRaceData(store);
        _heroRosterAdapter.GetAllHeroRaces().Returns(new List<HeroRaceInfo>
        {
            new HeroRaceInfo("hero_dead_elf", 0)
        });

        _sut.RestoreHeroRaces();

        _heroRosterAdapter.DidNotReceive().SetHeroRace(Arg.Any<string>(), Arg.Any<int>());
        _logger.Received(1).LogWarning(Arg.Is<string>(s => s.Contains("hero_dead_elf") && s.Contains("no longer exists")));
    }

    [TestMethod]
    public void RestoreHeroRaces_DeadHeroRaceAlreadyMatches_DoesNotSetRace()
    {
        var store = new RoundTripDataStore
        {
            IsSaving = false,
            NextLoadDict = new Dictionary<string, int> { ["hero_dead_dwarf"] = 1 },
            NextLoadLegend = "human;dwarf;elf"
        };
        _sut.SyncRaceData(store);
        _heroRosterAdapter.GetAllHeroRaces().Returns(new List<HeroRaceInfo>
        {
            new HeroRaceInfo("hero_dead_dwarf", 1)
        });

        _sut.RestoreHeroRaces();

        _heroRosterAdapter.DidNotReceive().SetHeroRace(Arg.Any<string>(), Arg.Any<int>());
    }

    [TestMethod]
    public void CaptureHeroRaces_OneRaceLegend_KeepsPriorDeadEntries()
    {
        _heroRosterAdapter.GetAllHeroRaces().Returns(new List<HeroRaceInfo>
        {
            new HeroRaceInfo("hero_dead_elf", 2)
        });
        _sut.CaptureHeroRaces();

        _raceManager.GetOrderedRaceNames().Returns(new[] { "human" });
        _heroRosterAdapter.GetAllHeroRaces().Returns(new List<HeroRaceInfo>
        {
            new HeroRaceInfo("hero_dead_elf", 0)
        });
        _sut.CaptureHeroRaces();

        _raceManager.GetOrderedRaceNames().Returns(new[] { "human", "dwarf", "elf" });
        _sut.RestoreHeroRaces();

        _heroRosterAdapter.Received(1).SetHeroRace("hero_dead_elf", 2);
    }

    // --- #1011 check lines (#1072): taom.race-persist.capture and taom.race-persist.restore ---

    private const string CaptureId = "taom.race-persist.capture";
    private const string RestoreId = "taom.race-persist.restore";

    private static List<HeroRaceInfo> Roster(params HeroRaceInfo[] heroes) => new(heroes);

    // Loads the map through the session-launch capture, which writes no check line.
    private void CaptureMap(params HeroRaceInfo[] heroes)
    {
        _heroRosterAdapter.GetAllHeroRaces().Returns(Roster(heroes));
        _sut.CaptureHeroRaces();
    }

    [TestMethod]
    public void CaptureHeroRacesForSave_PassesHeroAndDeadCounts()
    {
        _heroRosterAdapter.GetAllHeroRaces().Returns(Roster(
            new HeroRaceInfo("a", 0), new HeroRaceInfo("dead_elf", 2, false), new HeroRaceInfo("dead_dwarf", 1, false)));

        _sut.CaptureHeroRacesForSave();

        _outcomes.Received(1).Pass(CaptureId, "1", "heroes=3 dead=2");
        Assert.AreEqual(3, _sut.CapturedRaceCount);
    }

    [TestMethod]
    public void CaptureHeroRacesForSave_DuplicateId_CountsTheFirstOnly()
    {
        _heroRosterAdapter.GetAllHeroRaces().Returns(Roster(new HeroRaceInfo("x", 1, false), new HeroRaceInfo("x", 2)));

        _sut.CaptureHeroRacesForSave();

        _outcomes.Received(1).Pass(CaptureId, "1", "heroes=1 dead=1");
    }

    [TestMethod]
    public void CaptureHeroRacesForSave_EachSaveHasItsOwnKey()
    {
        _heroRosterAdapter.GetAllHeroRaces().Returns(Roster(new HeroRaceInfo("a", 0)));

        _sut.CaptureHeroRacesForSave();
        _sut.CaptureHeroRacesForSave();

        _outcomes.Received(1).Pass(CaptureId, "1", "heroes=1 dead=0");
        _outcomes.Received(1).Pass(CaptureId, "2", "heroes=1 dead=0");
    }

    [TestMethod]
    public void CaptureHeroRacesForSave_DegenerateRaceTable_Fails()
    {
        _heroRosterAdapter.GetAllHeroRaces().Returns(Roster(new HeroRaceInfo("a", 0), new HeroRaceInfo("b", 1)));
        _sut.CaptureHeroRacesForSave();
        _raceManager.GetOrderedRaceNames().Returns(new[] { "human" });

        _sut.CaptureHeroRacesForSave();

        _outcomes.Received(1).Fail(CaptureId, "2", "reason=race-table races=1 kept=2");
        _outcomes.DidNotReceive().Pass(CaptureId, "2", Arg.Any<string>());
        Assert.AreEqual(2, _sut.CapturedRaceCount);
    }

    [TestMethod]
    public void CaptureHeroRaces_WritesNoCheckLine()
    {
        CaptureMap(new HeroRaceInfo("a", 0), new HeroRaceInfo("dead", 1, false));

        _outcomes.DidNotReceiveWithAnyArgs().Pass(default, default, default);
        _outcomes.DidNotReceiveWithAnyArgs().Fail(default, default, default);
        _outcomes.DidNotReceiveWithAnyArgs().Skip(default, default, default);
    }

    [TestMethod]
    public void Restore_NewCampaign_Skips()
    {
        _sut.ResetForNewCampaign();

        _sut.RestoreHeroRaces();

        _outcomes.Received(1).Skip(RestoreId, "1", "reason=new-campaign");
        _outcomes.DidNotReceiveWithAnyArgs().Fail(default, default, default);
    }

    // The engine calls SyncData only when the save has a record for the behavior, so a save older than
    // RacePersistenceBehavior gets none: nothing to restore, not a lost map.
    [TestMethod]
    public void Restore_LoadWithNoRecord_Skips()
    {
        _sut.RestoreHeroRaces();

        _outcomes.Received(1).Skip(RestoreId, "1", "reason=no-record");
        _outcomes.DidNotReceiveWithAnyArgs().Fail(default, default, default);
        _logger.Received(1).LogWarning(Arg.Is<string>(m => m.Contains("No saved race data found")));
    }

    [TestMethod]
    public void Restore_RecordLoadedButMapEmpty_Fails()
    {
        _sut.SyncRaceData(new RoundTripDataStore { IsSaving = false });

        _sut.RestoreHeroRaces();

        _outcomes.Received(1).Fail(RestoreId, "1", "reason=no-saved-data");
        _outcomes.DidNotReceiveWithAnyArgs().Skip(default, default, default);
        _logger.Received(1).LogWarning(Arg.Is<string>(m => m.Contains("No saved race data found")));
    }

    [TestMethod]
    public void Restore_ResetForNewSession_ForgetsTheLoadedRecord()
    {
        _sut.SyncRaceData(new RoundTripDataStore { IsSaving = false });
        _sut.ResetForNewSession();

        _sut.RestoreHeroRaces();

        _outcomes.Received(1).Skip(RestoreId, "1", "reason=no-record");
        _outcomes.DidNotReceiveWithAnyArgs().Fail(default, default, default);
    }

    [TestMethod]
    public void Restore_DeadHeroRestored_ReadBackAgrees_Passes()
    {
        CaptureMap(new HeroRaceInfo("dead_dwarf", 1, false), new HeroRaceInfo("elf", 2));
        _heroRosterAdapter.GetAllHeroRaces().Returns(
            Roster(new HeroRaceInfo("dead_dwarf", 0, false), new HeroRaceInfo("elf", 0)),
            Roster(new HeroRaceInfo("dead_dwarf", 1, false), new HeroRaceInfo("elf", 2)));

        _sut.RestoreHeroRaces();

        _outcomes.Received(1).Pass(RestoreId, "1", "restored=2 dead_restored=1 wraiths=0 skipped=0 mismatch=0 saved=2");
        _outcomes.DidNotReceiveWithAnyArgs().Fail(default, default, default);
    }

    [TestMethod]
    public void Restore_ReadBackDisagrees_Fails()
    {
        CaptureMap(new HeroRaceInfo("dead_dwarf", 1, false), new HeroRaceInfo("elf", 2));
        _heroRosterAdapter.GetAllHeroRaces().Returns(
            Roster(new HeroRaceInfo("dead_dwarf", 0, false), new HeroRaceInfo("elf", 0)),
            Roster(new HeroRaceInfo("dead_dwarf", 0, false), new HeroRaceInfo("elf", 2)));

        _sut.RestoreHeroRaces();

        _outcomes.Received(1).Fail(RestoreId, "1",
            "mismatch=1 first=dead_dwarf:1->0 restored=2 dead_restored=1 wraiths=0 skipped=0 saved=2");
        _outcomes.DidNotReceiveWithAnyArgs().Pass(default, default, default);
    }

    // #330: on the legend path the saved int indexes the save-time names, so the comparison uses the current id.
    [TestMethod]
    public void Restore_LegendShift_ComparesTheTranslatedId()
    {
        CaptureMap(new HeroRaceInfo("d", 1));
        _raceManager.GetRaceIdFromName("dwarf").Returns(4);
        _heroRosterAdapter.GetAllHeroRaces().Returns(Roster(new HeroRaceInfo("d", 0)), Roster(new HeroRaceInfo("d", 4)));

        _sut.RestoreHeroRaces();

        _outcomes.Received(1).Pass(RestoreId, "1", "restored=1 dead_restored=0 wraiths=0 skipped=0 mismatch=0 saved=1");
    }

    [TestMethod]
    public void Restore_WraithAndRemovedRace_AreCountedNotCompared()
    {
        _raceManager.GetOrderedRaceNames().Returns(new[] { "human", "dwarf", "elf", "gone" });
        CaptureMap(new HeroRaceInfo("wraith", 1), new HeroRaceInfo("lost", 3));
        _nazgul.IsWraith("wraith").Returns(true);
        _heroRosterAdapter.GetAllHeroRaces().Returns(
            Roster(new HeroRaceInfo("wraith", 0), new HeroRaceInfo("lost", 0)),
            Roster(new HeroRaceInfo("wraith", 0), new HeroRaceInfo("lost", 0)));

        _sut.RestoreHeroRaces();

        _outcomes.Received(1).Pass(RestoreId, "1", "restored=0 dead_restored=0 wraiths=1 skipped=1 mismatch=0 saved=2");
    }

    [TestMethod]
    public void Restore_UnchangedHero_IsStillCompared()
    {
        CaptureMap(new HeroRaceInfo("elf", 2));
        _heroRosterAdapter.GetAllHeroRaces().Returns(Roster(new HeroRaceInfo("elf", 2)), Roster(new HeroRaceInfo("elf", 0)));

        _sut.RestoreHeroRaces();

        _heroRosterAdapter.DidNotReceive().SetHeroRace(Arg.Any<string>(), Arg.Any<int>());
        _outcomes.Received(1).Fail(RestoreId, "1",
            "mismatch=1 first=elf:2->0 restored=0 dead_restored=0 wraiths=0 skipped=0 saved=1");
    }

    // SetHeroRace reaches the first hero with the id (Hero.FindFirst), so the read-back compares that one.
    [TestMethod]
    public void Restore_DuplicateId_ReadBackUsesTheFirstOccurrence()
    {
        CaptureMap(new HeroRaceInfo("x", 1));
        _heroRosterAdapter.GetAllHeroRaces().Returns(
            Roster(new HeroRaceInfo("x", 0)),
            Roster(new HeroRaceInfo("x", 1), new HeroRaceInfo("x", 5)));

        _sut.RestoreHeroRaces();

        _outcomes.Received(1).Pass(RestoreId, "1", "restored=1 dead_restored=0 wraiths=0 skipped=0 mismatch=0 saved=1");
    }

    // The latch is once per (id, result, key) for the process, so the load serial never resets.
    [TestMethod]
    public void Restore_EachLoadHasItsOwnKey()
    {
        _sut.RestoreHeroRaces();
        _sut.ResetForNewSession();
        _sut.RestoreHeroRaces();

        _outcomes.Received(1).Skip(RestoreId, "1", "reason=no-record");
        _outcomes.Received(1).Skip(RestoreId, "2", "reason=no-record");
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
