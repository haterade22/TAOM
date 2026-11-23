using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Adapters;
using TAOM.Core.Logging;
using TAOM.Features.CoopInterop;
using TAOM.Features.Diplomacy;
using TAOM.Features.Diplomacy.Models;
using TAOM.Features.WarOfTheRingMomentum;
using TAOM.Features.WarOfTheRingMomentum.UI;
using TAOM.Tests.Infrastructure;
using TaleWorlds.CampaignSystem;

namespace TAOM.Tests.Features.WarOfTheRingMomentum;

/// <summary>
/// #773: the momentum store is a process-lifetime singleton, and a save written before #327 has no
/// momentum record, so its SyncData never runs. The behavior is built fresh in every campaign's
/// OnGameStart and resets the store in its constructor, before any campaign event and before a
/// load's SyncData; a load then restores its record over the reset. The old OnNewGameCreated reset
/// never ran on a load, so a record-less save loaded after another campaign kept that campaign's
/// totals and its victor. That SubModule keeps building the behavior per campaign is pinned in
/// <see cref="TAOM.Tests.Infrastructure.PerCampaignBehaviorWiringTests"/>.
/// </summary>
[TestClass]
[TestCategory("RequiresGame")]
public class WarOfTheRingMomentumBehaviorSessionResetTests
{
    private static WarOfTheRingMomentumBehavior NewBehavior(IMomentumStateStore store) =>
        new WarOfTheRingMomentumBehavior(
            store, Substitute.For<IMomentumEventService>(), Substitute.For<IMomentumEnrollmentService>(),
            Substitute.For<IMomentumVictoryService>(), Substitute.For<IWarEventSnapshotAdapter>(),
            Substitute.For<IMomentumSettingsProvider>(), Substitute.For<IWarOfTheRingService>(),
            Substitute.For<IMomentumUIService>(), Substitute.For<IModLogger>(), Substitute.For<ICoopSessionProvider>());

    [TestMethod]
    public void Constructor_ResetsTheStore()
    {
        var store = Substitute.For<IMomentumStateStore>();

        NewBehavior(store);

        store.Received(1).ResetForNewGame();
    }

    [TestMethod]
    public void Constructor_NextCampaignWithNoMomentumRecord_StartsFresh()
    {
        var store = new MomentumStateStore(Substitute.For<IModLogger>());
        NewBehavior(store);
        store.State.MarkWarStarted();
        store.State.Free.EditMomentum(4200);
        store.State.MarkWarEnded(WarOutcome.EvilVictory);

        // Campaign B: OnGameStart builds a new behavior; a pre-#327 save has no record, so no SyncData.
        NewBehavior(store);

        Assert.IsFalse(store.State.HasWarStarted);
        Assert.IsFalse(store.State.HasWarEnded);
        Assert.AreEqual(WarOutcome.None, store.State.Victor);
        Assert.AreEqual(0, store.State.Free.SideMomentum);
    }

    [TestMethod]
    public void SyncData_SaveThenLoadIntoTheNextCampaign_RestoresTheSavedRecordOverTheReset()
    {
        var store = new MomentumStateStore(Substitute.For<IModLogger>());
        var campaignA = NewBehavior(store);
        store.State.MarkWarStarted();
        store.State.Free.EditMomentum(4200);
        var data = new EngineDataStore { Loading = false };
        campaignA.SyncData(data);

        data.Loading = true;
        var campaignB = NewBehavior(store);
        Assert.IsFalse(store.State.HasWarStarted, "campaign B's OnGameStart resets first");

        campaignB.SyncData(data);

        Assert.IsTrue(store.State.HasWarStarted);
        Assert.AreEqual(4200, store.State.Free.SideMomentum);
    }

    [TestMethod]
    public void SyncData_Saving_DoesNotResetAgain()
    {
        var store = Substitute.For<IMomentumStateStore>();
        var sut = NewBehavior(store);
        var data = Substitute.For<IDataStore>();
        data.IsSaving.Returns(true);

        sut.SyncData(data);

        store.Received(1).ResetForNewGame();
    }
}
