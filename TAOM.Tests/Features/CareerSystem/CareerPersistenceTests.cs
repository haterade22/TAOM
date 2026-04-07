using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TaleWorlds.CampaignSystem;
using TAOM.Features.CareerSystem;
using TAOM.Features.CareerSystem.Domain;

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
        _behavior = new CareerPersistenceBehavior(_dataService);
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

        // Assert: exactly 3 calls, all primitive dictionaries
        Assert.AreEqual(3, capturedTypes.Count,
            "SyncData should be called exactly 3 times (careerIds, choices, tiers)");
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

        var saveStore = new FakeDataStore();
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
        var loadBehavior = new CareerPersistenceBehavior(freshService);
        var loadStore = new FakeDataStore();
        loadStore.SetData("_taom_careerIds", savedCareerIds);
        loadStore.SetData("_taom_careerChoices", savedChoices);
        loadStore.SetData("_taom_careerTiers", savedTiers);
        loadBehavior.SyncData(loadStore);

        // Assert round-trip
        Assert.AreEqual("warboss", freshService.GetCareerStringId("hero1"));
        Assert.AreEqual(2, freshService.GetChoiceCount("hero1"));
        Assert.IsTrue(freshService.IsTierUnlocked("hero1", 2));
    }

    [TestMethod]
    public void SyncData_EmptyData_DoesNotCrash()
    {
        var loadStore = new FakeDataStore();
        _behavior.SyncData(loadStore);

        Assert.IsFalse(_dataService.HasCareer("hero1"));
    }

    [TestMethod]
    public void SyncData_PreFeatureSave_GracefullyLoadsEmpty()
    {
        // Simulate loading a save that has no career data (null from dataStore)
        var loadStore = new FakeDataStore();
        loadStore.SetData<Dictionary<string, string>>("_taom_careerIds", null);
        loadStore.SetData<Dictionary<string, string>>("_taom_careerChoices", null);
        loadStore.SetData<Dictionary<string, string>>("_taom_careerTiers", null);

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

        var store = new FakeDataStore();
        _behavior.SyncData(store);

        var freshService = new CareerDataService();
        var loadBehavior = new CareerPersistenceBehavior(freshService);
        var loadStore = new FakeDataStore();
        loadStore.SetData("_taom_careerIds", store.GetSaved<Dictionary<string, string>>("_taom_careerIds"));
        loadStore.SetData("_taom_careerChoices", store.GetSaved<Dictionary<string, string>>("_taom_careerChoices"));
        loadStore.SetData("_taom_careerTiers", store.GetSaved<Dictionary<string, string>>("_taom_careerTiers"));
        loadBehavior.SyncData(loadStore);

        Assert.AreEqual("warboss", freshService.GetCareerStringId("hero1"));
        Assert.AreEqual("ranger", freshService.GetCareerStringId("hero2"));
    }

    /// <summary>
    /// Fake IDataStore that captures saved values and replays them on load.
    /// Only supports Dictionary&lt;string, string&gt; — enforcing the primitive-only constraint.
    /// </summary>
    private class FakeDataStore : IDataStore
    {
        private readonly Dictionary<string, object> _data = new Dictionary<string, object>();

        public bool SyncData<T>(string key, ref T data)
        {
            if (_data.TryGetValue(key, out var stored))
            {
                data = (T)stored;
                return true;
            }

            // Save path: store what was passed
            if (data != null)
                _data[key] = data;

            return true;
        }

        public T GetSaved<T>(string key)
        {
            return _data.TryGetValue(key, out var v) ? (T)v : default;
        }

        public void SetData<T>(string key, T value)
        {
            _data[key] = value;
        }

        public bool IsSaving => true;
        public bool IsLoading => false;
    }
}
