using System;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Adapters;
using TAOM.Core.Logging;
using TAOM.Features.CoopInterop;
using TAOM.Features.Execution;
using TAOM.Features.WarPledge;
using TAOM.Tests.Features.WarChronicle;
using TAOM.Tests.Infrastructure;

namespace TAOM.Tests.Features.WarPledge;

/// <summary>
/// #1113: the pledge store is a process-lifetime singleton. The behavior is built fresh for every campaign
/// start, before any event and before a load's SyncData, so its constructor resets the store and a load then
/// restores the saved record over the reset (csharp-architecture.md, the session-reset rule). The save round
/// trip goes through a dictionary-backed store into a FRESH behavior, so a dropped SyncData key or LoadFrom
/// line cannot hide behind the old instance. The daily step's gates are pinned here too.
/// </summary>
[TestClass]
[TestCategory("RequiresGame")]
public class WarPledgeBehaviorSessionResetTests
{
    private WarPledgeFixture _f = null!;
    private WarPledgeService _service = null!;
    private IKingdomWarSnapshotAdapter _clock = null!;
    private ICoopSessionProvider _coop = null!;
    private IModLogger _logger = null!;

    [TestInitialize]
    public void SetUp()
    {
        _f = new WarPledgeFixture();
        _service = _f.Build();
        _clock = Substitute.For<IKingdomWarSnapshotAdapter>();
        _clock.SetElapsedDay(50);
        _coop = Substitute.For<ICoopSessionProvider>();
        _coop.IsAuthority.Returns(true);
        _coop.ShouldDeferToHost.Returns(false);
        _logger = Substitute.For<IModLogger>();
    }

    private WarPledgeBehavior NewBehavior() => new(_service, _f.Store, _clock, _coop, _logger);

    [TestMethod]
    public void Constructor_ResetsThePledgesAndTheCourtingDay_ForANewCampaign()
    {
        _f.Store.Pledge("cardolan", FactionSide.Free);
        _f.Store.NextCourtingDay = 300;

        NewBehavior();

        Assert.AreEqual(0, _f.Store.Count);
        Assert.AreEqual(WarPledgeStore.NoCourtingDay, _f.Store.NextCourtingDay);
        Assert.AreEqual(FactionSide.Neutral, _f.Alignment.GetKingdomSide("cardolan"),
            "the next campaign must not read the last campaign's pledge as a side");
    }

    [TestMethod]
    public void SaveThenLoad_IntoAFreshBehavior_RestoresEveryPledgeAndTheCourtingDay()
    {
        var a = NewBehavior();
        _f.Store.Pledge("cardolan", FactionSide.Free);
        _f.Store.Pledge("rhudaur", FactionSide.Evil);
        _f.Store.NextCourtingDay = 110;
        var store = new EngineDataStore { Loading = false };
        a.SyncData(store);

        store.Loading = true;
        var b = NewBehavior();
        Assert.AreEqual(0, _f.Store.Count, "precondition: the fresh behavior's constructor reset the store");
        b.SyncData(store);

        CollectionAssert.AreEqual(new[] { "cardolan", "rhudaur" }, _f.Store.Snapshot().Select(p => p.Key).ToList());
        Assert.AreEqual(FactionSide.Free, _f.Alignment.GetKingdomSide("cardolan"));
        Assert.AreEqual(FactionSide.Evil, _f.Alignment.GetKingdomSide("rhudaur"));
        Assert.AreEqual(110, _f.Store.NextCourtingDay);
    }

    [TestMethod]
    public void Save_WritesBothKeys()
    {
        var behavior = NewBehavior();
        _f.Store.Pledge("cardolan", FactionSide.Free);
        _f.Store.NextCourtingDay = 110;
        var store = new EngineDataStore { Loading = false };

        behavior.SyncData(store);

        Assert.AreEqual("cardolan=Free", store.Get<string>(WarPledgeBehavior.RowsKey));
        Assert.AreEqual(110, store.Get<int>(WarPledgeBehavior.NextCourtingDayKey));
    }

    [TestMethod]
    public void SaveWithoutAPledge_LoadsWithoutOne()
    {
        var store = new EngineDataStore { Loading = false };
        NewBehavior().SyncData(store);

        store.Loading = true;
        _f.Store.Pledge("cardolan", FactionSide.Free);
        NewBehavior().SyncData(store);

        Assert.AreEqual(0, _f.Store.Count);
        Assert.AreEqual(WarPledgeStore.NoCourtingDay, _f.Store.NextCourtingDay);
    }

    // A record from a build that saved other keys: both of ours are missing.
    [TestMethod]
    public void LoadOfARecordWithoutTheKeys_LoadsNoPledgeAndNoCourtingDay()
    {
        var store = new EngineDataStore { Loading = true };

        NewBehavior().SyncData(store);

        Assert.AreEqual(0, _f.Store.Count);
        Assert.AreEqual(WarPledgeStore.NoCourtingDay, _f.Store.NextCourtingDay);
    }

    // The load path makes no engine call: the wars of a loaded pledge are the War of the Ring's own reconcile.
    [TestMethod]
    public void Load_MakesNoWarAndNoPeace()
    {
        var store = new EngineDataStore { Loading = true };
        store.Seed(WarPledgeBehavior.RowsKey, "cardolan=Free");
        store.Seed(WarPledgeBehavior.NextCourtingDayKey, 110);

        NewBehavior().SyncData(store);

        Assert.IsTrue(_f.Store.IsPledged("cardolan"));
        Assert.AreEqual(0, _f.Alliance.Calls.Count);
    }

    private void ArrangeAJoinToday()
    {
        _f.Momentum.SliderValue.Returns(-40);
        _f.Store.NextCourtingDay = 50;
    }

    [TestMethod]
    public void OnDailyTick_Authority_RunsTheCourtingWithTodaysDay()
    {
        var behavior = NewBehavior();
        ArrangeAJoinToday();

        behavior.OnDailyTick();

        Assert.IsTrue(_f.Store.IsPledged("cardolan"));
        Assert.AreEqual(80, _f.Store.NextCourtingDay, "day 50 from the clock, plus the 30-day wait");
    }

    [TestMethod]
    public void OnDailyTick_NotAuthority_CourtsNobody()
    {
        var behavior = NewBehavior();
        ArrangeAJoinToday();
        _coop.IsAuthority.Returns(false);

        behavior.OnDailyTick();

        Assert.AreEqual(0, _f.Store.Count);
    }

    // IsAuthority alone is true on both peers under a co-op mod TAOM cannot probe.
    [TestMethod]
    public void OnDailyTick_DeferToHost_CourtsNobody()
    {
        var behavior = NewBehavior();
        ArrangeAJoinToday();
        _coop.ShouldDeferToHost.Returns(true);

        behavior.OnDailyTick();

        Assert.AreEqual(0, _f.Store.Count);
    }

    [TestMethod]
    public void OnDailyTick_AnInvalidClock_CourtsNobodyAndSetsNoAnchor()
    {
        var behavior = NewBehavior();
        _f.Momentum.SliderValue.Returns(-40);
        _clock.SetClockInvalid();

        behavior.OnDailyTick();

        Assert.AreEqual(0, _f.Store.Count);
        Assert.AreEqual(WarPledgeStore.NoCourtingDay, _f.Store.NextCourtingDay);
    }

    [TestMethod]
    public void OnDailyTick_TheStepThrows_LogsTheErrorAndDoesNotThrow()
    {
        var behavior = NewBehavior();
        _f.Store.NextCourtingDay = 50;
        _f.Momentum.SliderValue.Returns(_ => throw new InvalidOperationException("meter"));

        behavior.OnDailyTick();

        _logger.Received(1).LogError(Arg.Is<string>(s => s.Contains("[WarPledge] daily step failed") && s.Contains("meter")));
    }
}
