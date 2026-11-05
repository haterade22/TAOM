using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TaleWorlds.CampaignSystem;
using TAOM.Core.Logging;
using TAOM.Features.CareerSystem;
using TAOM.Features.CareerSystem.Domain;
using TAOM.Tests.Infrastructure;

namespace TAOM.Tests.Features.CareerSystem;

/// <summary>
/// Preventive tests for save/load serialization.
/// Root cause: Bug #2 from Codex review — HeroCareerData is not a BL-registered type.
/// BL's SyncData only handles primitive-value dictionaries (string, int, float) and Lists.
/// These tests verify the flatten/reconstruct round-trip works and that no custom types
/// are passed to dataStore.SyncData().
/// </summary>
[TestClass]
public class CareerPersistenceTests
{
    private CareerDataService _dataService;
    private CareerPersistenceBehavior _behavior;

    [TestInitialize]
    public void Setup()
    {
        _dataService = new CareerDataService();
        _behavior = new CareerPersistenceBehavior(_dataService, Substitute.For<IModLogger>());
    }

    [TestMethod]
    public void SyncData_OnlyPassesPrimitiveDictionariesToDataStore()
    {
        // Arrange: set up career data
        _dataService.SetCareer("hero1", "warboss");
        _dataService.TryAddChoice("hero1", "wb_root", 10);
        _dataService.UnlockTier("hero1", 2);

        // Act: capture all SyncData calls
        var dataStore = Substitute.For<IDataStore>();
        var capturedTypes = new List<string>();

        // Capture the types passed to SyncData via ref parameter type inspection
        // The key check: SyncData is called 3 times, all with Dictionary<string, string>
        dataStore.SyncData(Arg.Any<string>(), ref Arg.Any<Dictionary<string, string>>())
            .Returns(x => { capturedTypes.Add("Dictionary<string,string>"); return true; });

        _behavior.SyncData(dataStore);

        // Assert: exactly 4 calls, all primitive dictionaries
        Assert.AreEqual(4, capturedTypes.Count,
            "SyncData should be called exactly 4 times (careerIds, choices, tiers, flags)");
    }

    [TestMethod]
    public void SyncData_RoundTrip_PreservesCareerAndChoices()
    {
        // Arrange: populate data
        _dataService.SetCareer("hero1", "warboss");
        _dataService.TryAddChoice("hero1", "wb_root", 10);
        _dataService.TryAddChoice("hero1", "wb_brut_key", 10);
        _dataService.UnlockTier("hero1", 2);

        // Act: simulate save then load via a fake IDataStore
        var savedCareerIds = new Dictionary<string, string>();
        var savedChoices = new Dictionary<string, string>();
        var savedTiers = new Dictionary<string, string>();

        var saveStore = new EngineDataStore();
        _behavior.SyncData(saveStore);

        // Get what was saved
        savedCareerIds = saveStore.GetSaved<Dictionary<string, string>>("_taom_careerIds");
        savedChoices = saveStore.GetSaved<Dictionary<string, string>>("_taom_careerChoices");
        savedTiers = saveStore.GetSaved<Dictionary<string, string>>("_taom_careerTiers");

        Assert.AreEqual("warboss", savedCareerIds["hero1"]);
        Assert.AreEqual("wb_root,wb_brut_key", savedChoices["hero1"]);
        Assert.AreEqual("2", savedTiers["hero1"]);

        // Simulate loading into a fresh service
        var freshService = new CareerDataService();
        var loadBehavior = new CareerPersistenceBehavior(freshService, Substitute.For<IModLogger>());
        var loadStore = new EngineDataStore { Loading = true };
        loadStore.Seed("_taom_careerIds", savedCareerIds);
        loadStore.Seed("_taom_careerChoices", savedChoices);
        loadStore.Seed("_taom_careerTiers", savedTiers);
        loadBehavior.SyncData(loadStore);

        // Assert round-trip
        Assert.AreEqual("warboss", freshService.GetCareerStringId("hero1"));
        Assert.AreEqual(2, freshService.GetChoiceCount("hero1"));
        Assert.IsTrue(freshService.IsTierUnlocked("hero1", 2));
    }

    [TestMethod]
    public void SyncData_EmptyData_DoesNotCrash()
    {
        var loadStore = new EngineDataStore { Loading = true };
        _behavior.SyncData(loadStore);

        Assert.IsFalse(_dataService.HasCareer("hero1"));
    }

    [TestMethod]
    public void SyncData_LoadWithoutTheKeys_GracefullyLoadsEmpty()
    {
        // Record present but the career keys missing: the engine returns false and leaves each ref untouched.
        var loadStore = new EngineDataStore { Loading = true };

        _behavior.SyncData(loadStore);

        Assert.IsFalse(_dataService.HasCareer("hero1"));
        Assert.AreEqual(0, _dataService.GetChoiceCount("hero1"));
    }

    [TestMethod]
    public void SyncData_MultipleHeroes_AllPreserved()
    {
        _dataService.SetCareer("hero1", "warboss");
        _dataService.TryAddChoice("hero1", "wb_root", 10);
        _dataService.SetCareer("hero2", "ranger");
        _dataService.TryAddChoice("hero2", "ranger_root", 10);

        var store = new EngineDataStore();
        _behavior.SyncData(store);

        var freshService = new CareerDataService();
        var loadBehavior = new CareerPersistenceBehavior(freshService, Substitute.For<IModLogger>());
        var loadStore = new EngineDataStore { Loading = true };
        loadStore.Seed("_taom_careerIds", store.GetSaved<Dictionary<string, string>>("_taom_careerIds"));
        loadStore.Seed("_taom_careerChoices", store.GetSaved<Dictionary<string, string>>("_taom_careerChoices"));
        loadStore.Seed("_taom_careerTiers", store.GetSaved<Dictionary<string, string>>("_taom_careerTiers"));
        loadBehavior.SyncData(loadStore);

        Assert.AreEqual("warboss", freshService.GetCareerStringId("hero1"));
        Assert.AreEqual("ranger", freshService.GetCareerStringId("hero2"));
    }

    // Phase 9b #128 P1 — guard against running reconstruct on save path. Pre-fix, save passes
    // ran the entire load reconstruction block, calling RestoreData which replaced the dict
    // reference mid-save. Verify saving does NOT mutate the service's data.

    [TestMethod]
    public void SyncData_OnSaving_DoesNotMutateServiceData()
    {
        _dataService.SetCareer("hero1", "warboss");
        _dataService.TryAddChoice("hero1", "wb_root", 10);
        _dataService.UnlockTier("hero1", 2);

        // Capture the in-memory state via reference identity. RestoreData replaces the dict
        // reference (`_heroData = data`), so if it ran during save the reference would change.
        var preReference = _dataService.GetAllData();
        var preCareer = _dataService.GetCareerStringId("hero1");
        var preChoiceCount = _dataService.GetChoiceCount("hero1");

        var saveStore = new EngineDataStore();
        _behavior.SyncData(saveStore);

        Assert.AreSame(preReference, _dataService.GetAllData(),
            "Saving must not replace the service's data dictionary (RestoreData side-effect)");
        Assert.AreEqual(preCareer, _dataService.GetCareerStringId("hero1"));
        Assert.AreEqual(preChoiceCount, _dataService.GetChoiceCount("hero1"));
    }

    [TestMethod]
    public void SyncData_RoundTrip_PreservesFlags()
    {
        _dataService.SetCareer("hero1", "warboss");
        _dataService.SetFlag("hero1", "captain_proven");
        _dataService.SetFlag("hero1", "ithilien_oath");

        var store = new EngineDataStore();
        _behavior.SyncData(store);

        var savedFlags = store.GetSaved<Dictionary<string, string>>("_taom_careerFlags");
        Assert.AreEqual("captain_proven,ithilien_oath", savedFlags["hero1"]);

        var freshService = new CareerDataService();
        var loadBehavior = new CareerPersistenceBehavior(freshService, Substitute.For<IModLogger>());
        var loadStore = new EngineDataStore { Loading = true };
        loadStore.Seed("_taom_careerIds", store.GetSaved<Dictionary<string, string>>("_taom_careerIds"));
        loadStore.Seed("_taom_careerFlags", savedFlags);
        loadBehavior.SyncData(loadStore);

        Assert.IsTrue(freshService.HasFlag("hero1", "captain_proven"));
        Assert.IsTrue(freshService.HasFlag("hero1", "ithilien_oath"));
    }
}
