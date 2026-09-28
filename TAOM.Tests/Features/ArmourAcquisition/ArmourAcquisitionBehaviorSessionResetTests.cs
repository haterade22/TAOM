using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TaleWorlds.CampaignSystem;
using TAOM.Adapters;
using TAOM.Core.Logging;
using TAOM.Features.ArmourAcquisition;
using TAOM.Features.ArmourAcquisition.Domain;
using TAOM.Features.ArmourAcquisition.Hooks;
using TAOM.Features.CoopInterop;

namespace TAOM.Tests.Features.ArmourAcquisition;

/// <summary>
/// The campaign state is a process singleton, so a second campaign in one process, or a save made before
/// the feature existed, must start empty, and a real save must restore exactly what it held
/// (csharp-architecture.md, "Singleton Services Holding Per-Campaign State"). The daily sweep itself is
/// pinned per town in ArmourStockSweepServiceTests.
/// </summary>
[TestCategory("RequiresGame")]
[TestClass]
public class ArmourAcquisitionBehaviorSessionResetTests
{
    private ArmourAcquisitionState _state = null!;
    private IArmourGateService _gate = null!;
    private IArmouryTownAdapter _towns = null!;

    [TestInitialize]
    public void Setup()
    {
        _state = new ArmourAcquisitionState();
        _gate = Substitute.For<IArmourGateService>();
        _gate.IsActive.Returns(true);
        _towns = Substitute.For<IArmouryTownAdapter>();
        _towns.AllTownIds().Returns(new List<string>());
    }

    private ArmourAcquisitionCampaignBehavior NewBehavior()
    {
        var config = Substitute.For<IArmourAcquisitionConfigProvider>();
        config.GetConfig().Returns(ArmourAcquisitionConfig.Default);
        var settings = Substitute.For<IArmourAcquisitionSettingsProvider>();
        var visits = new VisitingArmourerService(_state, config, _towns);
        var levels = new ArmouryLevelService(visits, settings, _towns);
        return new ArmourAcquisitionCampaignBehavior(_state, _gate, visits,
            new ArmourStockSweepService(_gate, levels, Substitute.For<ITownRosterAdapter>()), _towns, settings,
            Substitute.For<ICoopSessionProvider>(), Substitute.For<IDedicatedServerProvider>(), Substitute.For<IModLogger>());
    }

    private void LeavePriorCampaignState()
    {
        _state.LadderClaimed["main_hero"] = 2;
        _state.VisitUntilDay["town_A"] = 99;
    }

    [TestMethod]
    public void OnSessionLaunched_WithoutALoad_StartsFromAnEmptyState()
    {
        LeavePriorCampaignState();

        NewBehavior().OnSessionLaunched(null);

        Assert.AreEqual(0, _state.LadderClaimed.Count);
        Assert.AreEqual(0, _state.VisitUntilDay.Count);
    }

    [TestMethod]
    public void SyncData_LoadingASaveWithoutTheKey_YieldsAnEmptyState()
    {
        LeavePriorCampaignState();
        var behavior = NewBehavior();

        behavior.SyncData(new FakeDataStore { Loading = true });
        behavior.OnSessionLaunched(null);

        Assert.AreEqual(0, _state.LadderClaimed.Count, "a key miss must not leave the previous campaign's state");
    }

    [TestMethod]
    public void SyncData_SaveThenLoadIntoAnotherCampaign_RestoresTheSavedState()
    {
        _state.LadderClaimed["main_hero"] = 3;
        _state.LadderReady["main_hero"] = 4;
        _state.LordEventLastDay["main_hero"] = 40;
        var store = new FakeDataStore();
        NewBehavior().SyncData(store);

        _state.Reset();
        _state.LadderClaimed["other_hero"] = 1;
        store.Loading = true;
        var behavior = NewBehavior();
        behavior.SyncData(store);
        behavior.OnSessionLaunched(null);

        Assert.AreEqual(3, _state.LadderClaimed["main_hero"]);
        Assert.AreEqual(4, _state.LadderReady["main_hero"]);
        Assert.AreEqual(40, _state.LordEventLastDay["main_hero"]);
        Assert.IsFalse(_state.LadderClaimed.ContainsKey("other_hero"));
    }

    /// <summary>
    /// Mirrors the engine's behavior data store (v1.5.3 BehaviorSaveData.SyncData): a load of a missing key
    /// returns false and leaves the ref unchanged; a save records what it is handed.
    /// </summary>
    private sealed class FakeDataStore : IDataStore
    {
        private readonly Dictionary<string, object?> _data = new();

        public bool Loading { get; set; }

        public bool IsSaving => !Loading;

        public bool IsLoading => Loading;

        public bool SyncData<T>(string key, ref T data)
        {
            if (Loading)
            {
                if (!_data.TryGetValue(key, out var stored))
                    return false;
                data = (T)stored!;
                return true;
            }
            _data[key] = data;
            return true;
        }
    }
}
