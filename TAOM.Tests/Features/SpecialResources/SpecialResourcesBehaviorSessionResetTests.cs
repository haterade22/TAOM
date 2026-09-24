using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TaleWorlds.CampaignSystem;
using TAOM.Core.Logging;
using TAOM.Features.CoopInterop;
using TAOM.Features.SpecialResources;
using TAOM.Features.TroopWeight;

namespace TAOM.Tests.Features.SpecialResources;

/// <summary>
/// Plan 001: the balance storage is a process-lifetime singleton, so a second campaign in the same
/// process, or a save whose behavior record lacks the balances key, must not keep the previous
/// campaign's balances. Real storage, so the assertions read what the next campaign would see.
/// </summary>
[TestClass]
public class SpecialResourcesBehaviorSessionResetTests
{
    private const string Key = "_taom_specialResources";

    private SpecialResourceStorageService _storage = null!;
    private SpecialResourcesBehavior _sut = null!;

    [TestInitialize]
    public void Setup()
    {
        _storage = new SpecialResourceStorageService();
        _sut = NewBehavior(_storage);
    }

    private static SpecialResourcesBehavior NewBehavior(ISpecialResourceStorageService storage) =>
        new SpecialResourcesBehavior(
            Substitute.For<ISpecialResourceService>(),
            storage,
            Substitute.For<ISpecialResourceConfigProvider>(),
            Substitute.For<IModLogger>(),
            Substitute.For<ITroopWeightService>(),
            Substitute.For<IDedicatedServerProvider>());

    private void LeavePriorCampaignBalances()
    {
        _storage.Set("main_hero", "war_spoils", 120f);
        _storage.Set("main_hero", "castar", 0f);
        _storage.Set("lord_1_1", "gems", 450f);
    }

    [TestMethod]
    public void OnNewGameCreated_AfterPriorCampaignBalances_StartsWithEmptyStorage()
    {
        LeavePriorCampaignBalances();

        // Outside a campaign Hero.MainHero is null, so this also pins that the wipe does not
        // depend on the main hero existing yet.
        _sut.OnNewGameCreated(null!);

        Assert.AreEqual(0, _storage.GetAllData().Count);
        Assert.IsFalse(_storage.Contains("main_hero", "war_spoils"));
        Assert.IsFalse(_storage.Contains("main_hero", "castar"),
            "a stale spent-to-zero pair would suppress the next campaign's legacy-save seed");
    }

    [TestMethod]
    public void OnNewGameCreated_NoMainHeroYet_StillResetsTheServiceSessionState()
    {
        var service = Substitute.For<ISpecialResourceService>();
        var sut = new SpecialResourcesBehavior(
            service,
            _storage,
            Substitute.For<ISpecialResourceConfigProvider>(),
            Substitute.For<IModLogger>(),
            Substitute.For<ITroopWeightService>(),
            Substitute.For<IDedicatedServerProvider>());

        sut.OnNewGameCreated(null!);

        service.Received(1).ResetSessionState();
        service.DidNotReceive().InitializeHero(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>());
    }

    [TestMethod]
    public void SyncData_LoadingASaveWithoutTheKey_YieldsEmptyStorage()
    {
        LeavePriorCampaignBalances();
        var store = new FakeDataStore { Mode = FakeDataStore.StoreMode.Loading };

        _sut.SyncData(store);

        Assert.AreEqual(0, _storage.GetAllData().Count,
            "a key miss leaves the ref unchanged, so the previous campaign's balances must not be the ref");
    }

    [TestMethod]
    public void SyncData_SaveThenLoadIntoAnotherCampaign_RestoresExactlyTheSavedBalances()
    {
        _storage.Set("main_hero", "war_spoils", 123.25f);
        _storage.Set("main_hero", "castar", 0f);
        _storage.Set("lord_1_1", "gems", 600f);
        var expected = new Dictionary<string, float>(_storage.GetAllData());
        var store = new FakeDataStore { Mode = FakeDataStore.StoreMode.Saving };
        _sut.SyncData(store);

        // A different campaign's balances are live in the singleton when the save is loaded.
        var loadStorage = new SpecialResourceStorageService();
        loadStorage.Set("other_hero", "elven_wine", 77f);
        store.Mode = FakeDataStore.StoreMode.Loading;
        NewBehavior(loadStorage).SyncData(store);

        var loaded = loadStorage.GetAllData();
        CollectionAssert.AreEquivalent(expected.Keys.ToList(), loaded.Keys.ToList());
        foreach (var pair in expected)
            Assert.AreEqual(pair.Value, loaded[pair.Key], 0f, pair.Key);
    }

    [TestMethod]
    public void SyncData_Saving_WritesTheLiveBalancesUnderTheKeyAndLeavesStorageAlone()
    {
        LeavePriorCampaignBalances();
        var live = _storage.GetAllData();
        var store = new FakeDataStore { Mode = FakeDataStore.StoreMode.Saving };

        _sut.SyncData(store);

        Assert.AreSame(live, store.GetSaved<Dictionary<string, float>>(Key));
        Assert.AreSame(live, _storage.GetAllData());
        Assert.AreEqual(3, live.Count);
    }

    /// <summary>
    /// Mirrors the engine's behavior data store: a load of a missing key returns false and leaves
    /// the ref unchanged; a save records what it is handed.
    /// </summary>
    private class FakeDataStore : IDataStore
    {
        public enum StoreMode { Saving, Loading }

        private readonly Dictionary<string, object> _data = new();

        public StoreMode Mode { get; set; }

        public bool SyncData<T>(string key, ref T data)
        {
            if (Mode == StoreMode.Loading)
            {
                if (!_data.TryGetValue(key, out var stored)) return false;
                data = (T)stored;
                return true;
            }

            if (data != null) _data[key] = data;
            return true;
        }

        public T GetSaved<T>(string key) => _data.TryGetValue(key, out var v) ? (T)v : default!;

        public bool IsSaving => Mode == StoreMode.Saving;
        public bool IsLoading => Mode == StoreMode.Loading;
    }
}
