using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Adapters;
using TAOM.Core.Logging;
using TAOM.Features.CoopInterop;
using TAOM.Features.Diplomacy;
using TAOM.Features.WarOfTheRingMomentum;
using TAOM.Features.WarOfTheRingMomentum.Domain;
using TAOM.Features.WarOfTheRingMomentum.UI;
using TAOM.Tests.Infrastructure;

namespace TAOM.Tests.Features.WarOfTheRingMomentum;

/// <summary>
/// #773: the momentum store is a process-lifetime singleton and the behavior is built with new in every
/// campaign, so the behavior's constructor resets the store. A new campaign, or a load of a save with no
/// momentum record (the engine then never calls SyncData), starts from a fresh war; a load of a save with a
/// record restores it afterwards.
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

    private static MomentumStateStore NewRealStore() => new MomentumStateStore(Substitute.For<IModLogger>());

    private static void PopulateAWar(MomentumStateStore store)
    {
        store.State.MarkWarStarted();
        store.State.Free.AddKingdom("empire_w");
        store.PlayerEvents.Add(MomentumActionType.BattleWon);
    }

    [TestMethod]
    public void Constructor_ResetsTheStore()
    {
        var store = Substitute.For<IMomentumStateStore>();

        NewBehavior(store);

        store.Received(1).ResetForNewGame();
    }

    [TestMethod]
    public void NewBehavior_AfterAPopulatedCampaign_StartsFromAFreshWar()
    {
        var store = NewRealStore();
        NewBehavior(store);
        PopulateAWar(store);

        NewBehavior(store);

        Assert.IsFalse(store.State.HasWarStarted);
        Assert.IsFalse(store.State.Free.ContainsKingdom("empire_w"));
        Assert.AreEqual(0, store.PlayerEvents.Count);
    }

    [TestMethod]
    public void SyncData_SaveThenLoadIntoTheNextCampaign_RestoresTheSavedWar()
    {
        var store = NewRealStore();
        var first = NewBehavior(store);
        PopulateAWar(store);
        var data = new EngineDataStore();
        first.SyncData(data);
        data.Loading = true;

        var second = NewBehavior(store);
        second.SyncData(data);

        Assert.IsTrue(store.State.HasWarStarted);
        Assert.IsTrue(store.State.Free.ContainsKingdom("empire_w"));
    }

    [TestMethod]
    public void Source_NoLongerResetsFromOnNewGameCreated()
    {
        var src = RepoPaths.ReadSource("Main/Features/WarOfTheRingMomentum/WarOfTheRingMomentumBehavior.cs", stripComments: true);

        Assert.IsFalse(src.Contains("OnNewGameCreatedEvent"),
            "The constructor is the reset (#773); a listener on OnNewGameCreatedEvent would run after a campaign event could read the old war.");
    }
}
