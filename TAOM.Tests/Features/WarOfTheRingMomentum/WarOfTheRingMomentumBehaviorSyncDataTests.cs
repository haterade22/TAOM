using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Adapters;
using TAOM.Core.Logging;
using TAOM.Features.CoopInterop;
using TAOM.Features.Diplomacy;
using TAOM.Features.WarOfTheRingMomentum;
using TAOM.Features.WarOfTheRingMomentum.UI;
using TAOM.Tests.Infrastructure;

namespace TAOM.Tests.Features.WarOfTheRingMomentum;

/// <summary>
/// The momentum save record: a count key and that many chunk keys under the "_v3" names, which must stay
/// byte-identical for save compatibility. A saved count that is negative or absurd is ignored with a
/// warning and the state loads fresh.
/// </summary>
[TestClass]
[TestCategory("RequiresGame")]
public class WarOfTheRingMomentumBehaviorSyncDataTests
{
    private const string CountKey = "_taom_wotr_momentum_v3_count";
    private const string ChunkKeyPrefix = "_taom_wotr_momentum_v3_";

    private IMomentumStateStore _store = null!;
    private IModLogger _logger = null!;
    private WarOfTheRingMomentumBehavior _sut = null!;

    [TestInitialize]
    public void Setup()
    {
        _store = Substitute.For<IMomentumStateStore>();
        _logger = Substitute.For<IModLogger>();
        _sut = NewBehavior(_store);
    }

    private WarOfTheRingMomentumBehavior NewBehavior(IMomentumStateStore store) =>
        new WarOfTheRingMomentumBehavior(
            store, Substitute.For<IMomentumEventService>(), Substitute.For<IMomentumEnrollmentService>(),
            Substitute.For<IMomentumVictoryService>(), Substitute.For<IWarEventSnapshotAdapter>(),
            Substitute.For<IMomentumSettingsProvider>(), Substitute.For<IWarOfTheRingService>(),
            Substitute.For<IMomentumUIService>(), _logger, Substitute.For<ICoopSessionProvider>());

    [TestMethod]
    public void SyncData_SaveThenLoad_RoundTripsUnderTheV3Keys()
    {
        _store.Serialize().Returns(new Dictionary<string, string> { ["phase"] = "FullWar" });
        var data = new EngineDataStore { Loading = false };

        _sut.SyncData(data);

        Assert.AreEqual(1, data.Get<int>(CountKey));
        Assert.AreEqual("{\"phase\":\"FullWar\"}", data.Get<string>(ChunkKeyPrefix + "0"));

        var loadedStore = Substitute.For<IMomentumStateStore>();
        data.Loading = true;
        NewBehavior(loadedStore).SyncData(data);

        loadedStore.Received(1).Deserialize(Arg.Is<Dictionary<string, string>>(d => d["phase"] == "FullWar"));
    }

    [DataTestMethod]
    [DataRow(-1)]
    [DataRow(int.MaxValue)]
    public void SyncData_LoadingAnOutOfRangeChunkCount_LoadsFreshWithAWarning(int count)
    {
        var data = new EngineDataStore { Loading = true };
        data.Seed(CountKey, count);

        _sut.SyncData(data);

        _logger.Received(1).LogWarning(Arg.Is<string>(m => m.Contains("chunk count")));
        _store.Received(1).Deserialize(null!);
    }

    [TestMethod]
    public void SyncData_LoadingASaveWithNoV3Count_LoadsFreshWithoutAWarning()
    {
        // The v2 to v3 migration: an older save holds the single-string "_v2" record and no "_v3" keys, so
        // the absent count reads as 0 and the momentum starts fresh, the one-time reset the docs describe.
        var data = new EngineDataStore { Loading = true };
        data.Seed("_taom_wotr_momentum_v2", "{\"phase\":\"FullWar\"}");

        _sut.SyncData(data);

        _store.Received(1).Deserialize(null!);
        _logger.DidNotReceive().LogWarning(Arg.Any<string>());
    }
}
