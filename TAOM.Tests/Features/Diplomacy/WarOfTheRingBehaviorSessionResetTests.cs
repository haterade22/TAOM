using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Adapters;
using TAOM.Core.Logging;
using TAOM.Features.CoopInterop;
using TAOM.Features.Diplomacy;
using TAOM.Features.Diplomacy.Models;
using TaleWorlds.CampaignSystem;

namespace TAOM.Tests.Features.Diplomacy;

/// <summary>
/// #764: WarOfTheRingService is a process-lifetime singleton, so a brand-new campaign in the same
/// process must reset it before anything reads it. The behavior is built fresh in every campaign's
/// OnGameStart, before any campaign event and before a load's SyncData, so the reset sits in its
/// constructor and a load then restores the saved state over it. A reset in OnSessionLaunched ran
/// too late: campaign-event listeners run newest-first, and the momentum behavior, added after this
/// one, read the stale phase first (deep review 2026-10-08). That the behavior stays built per
/// campaign is pinned in <see cref="WarOfTheRingWiringTests"/>.
/// </summary>
[TestClass]
[TestCategory("RequiresGame")]
public class WarOfTheRingBehaviorSessionResetTests
{
    private IWarOfTheRingService _service = null!;
    private ICoopSessionProvider _coop = null!;
    private WarOfTheRingBehavior _sut = null!;

    [TestInitialize]
    public void Setup()
    {
        _service = Substitute.For<IWarOfTheRingService>();
        _coop = Substitute.For<ICoopSessionProvider>();
        _coop.IsAuthority.Returns(true);
        _sut = new WarOfTheRingBehavior(_service, Substitute.For<IModLogger>(), _coop);
    }

    private void SyncWith(bool isLoading)
    {
        var dataStore = Substitute.For<IDataStore>();
        dataStore.IsLoading.Returns(isLoading);
        dataStore.IsSaving.Returns(!isLoading);
        _sut.SyncData(dataStore);
    }

    private WarOfTheRingBehavior NewBehavior(IWarOfTheRingService service) =>
        new WarOfTheRingBehavior(service, Substitute.For<IModLogger>(), _coop);

    /// <summary>A real service with the shipped phase days (30 and 44), so a test can drive it to
    /// FullWar and WarEnded the way a campaign does.</summary>
    private static WarOfTheRingService RealService()
    {
        var config = Substitute.For<IWarOfTheRingConfigProvider>();
        config.LoadConfig().Returns(new WarOfTheRingConfig
        {
            Enabled = true,
            Phase1 = new PhaseConfig { TriggerDay = 30, Wars = new List<WarDeclaration>() },
            Phase2 = new PhaseConfig { TriggerDay = 44, Wars = new List<WarDeclaration>() },
            TestMode = new TestModeConfig { Enabled = false }
        });
        var settings = Substitute.For<ITaomSettingsProvider>();
        settings.IsAvailable.Returns(false);
        var alliances = Substitute.For<IAllianceAdapter>();
        alliances.GetAllKingdomIds().Returns(new List<string>());
        return new WarOfTheRingService(config, Substitute.For<IDiplomacyService>(), alliances, settings,
            Substitute.For<IModLogger>());
    }

    [TestMethod]
    public void Constructor_FreshCampaign_ResetsTheService()
    {
        _service.Received(1).ResetForNewSession();
    }

    [TestMethod]
    public void SyncData_LoadingASaveWithoutTheKeys_KeepsPeaceAndNoneAfterTheReset()
    {
        SyncWith(isLoading: true);

        // A substitute IDataStore leaves both refs at the fresh instance's Peace and None, which is
        // what a save written before both keys existed loads as. The real values are proven by the
        // round trip below.
        Received.InOrder(() =>
        {
            _service.ResetForNewSession();
            _service.SetPhaseFromSave(WarPhase.Peace);
            _service.SetOutcomeFromSave(WarOutcome.None);
        });
    }

    [TestMethod]
    public void SyncData_SaveThenLoadIntoTheNextCampaign_RestoresTheSavedPhaseAndOutcome()
    {
        var service = RealService();
        var campaignA = NewBehavior(service);
        service.CheckPhaseTransition(45f);
        service.EndWar(WarOutcome.EvilVictory);
        var store = new FakeDataStore();
        campaignA.SyncData(store);

        store.Loading = true;
        var campaignB = NewBehavior(service);
        Assert.AreEqual(WarPhase.Peace, service.CurrentPhase, "campaign B's OnGameStart resets first");

        campaignB.SyncData(store);

        Assert.AreEqual(WarPhase.WarEnded, service.CurrentPhase);
        Assert.AreEqual(WarOutcome.EvilVictory, service.Outcome);
    }

    [TestMethod]
    public void Constructor_NewCampaignAfterAWarEndedCampaign_StartsAtPeaceAndCanEndAgain()
    {
        var service = RealService();
        NewBehavior(service);
        service.CheckPhaseTransition(45f);
        service.EndWar(WarOutcome.EvilVictory);

        NewBehavior(service);

        Assert.AreEqual(WarPhase.Peace, service.CurrentPhase);
        Assert.AreEqual(WarOutcome.None, service.Outcome);
        service.CheckPhaseTransition(45f);
        service.EndWar(WarOutcome.FreeVictory);
        Assert.AreEqual(WarOutcome.FreeVictory, service.Outcome);
    }

    [TestMethod]
    public void SyncData_Saving_NeitherResetsAgainNorRestores()
    {
        SyncWith(isLoading: false);

        _service.Received(1).ResetForNewSession();
        _service.DidNotReceive().SetPhaseFromSave(Arg.Any<WarPhase>());
        _service.DidNotReceive().SetOutcomeFromSave(Arg.Any<WarOutcome>());
    }

    [TestMethod]
    public void OnSessionLaunched_CoopClient_SkipsThePhaseCheckAndAddsNoReset()
    {
        _coop.IsAuthority.Returns(false);
        _service.ClearReceivedCalls();

        _sut.OnSessionLaunched(null!);

        _service.DidNotReceive().ResetForNewSession();
        _service.DidNotReceive().CheckPhaseTransition(Arg.Any<float>());
    }

    /// <summary>
    /// Mirrors the engine's behavior data store (v1.5.4 BehaviorSaveData.SyncData): a load of a missing
    /// key returns false and leaves the ref unchanged; a save records what it is handed.
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
