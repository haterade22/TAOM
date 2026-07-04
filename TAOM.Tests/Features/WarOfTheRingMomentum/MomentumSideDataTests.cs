using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.WarOfTheRingMomentum.Domain;

namespace TAOM.Tests.Features.WarOfTheRingMomentum;

[TestClass]
public class MomentumSideDataTests
{
    private MomentumSideData _sut;

    [TestInitialize]
    public void Setup()
    {
        _sut = new MomentumSideData();
    }

    private static MomentumEvent Event(int value, double endHours = 1000.0,
        MomentumActionType type = MomentumActionType.BattleWon)
    {
        return new MomentumEvent(value, "test", type, endHours);
    }

    // ---- AddEvent / momentum accumulation ----

    [TestMethod]
    public void AddEvent_SingleEvent_AddsValueToMomentum()
    {
        _sut.AddEvent(Event(250));
        Assert.AreEqual(250, _sut.SideMomentum);
    }

    [TestMethod]
    public void AddEvent_MultipleEvents_Accumulates()
    {
        _sut.AddEvent(Event(250));
        _sut.AddEvent(Event(300, type: MomentumActionType.Sieges));
        Assert.AreEqual(550, _sut.SideMomentum);
    }

    [TestMethod]
    public void EditMomentum_NegativeAmount_CanGoNegative()
    {
        _sut.EditMomentum(-50);
        Assert.AreEqual(-50, _sut.SideMomentum);
    }

    [TestMethod]
    public void AddEvent_AtCap_TrimsOldestWithoutSubtractingItsValue()
    {
        // LOTRAOM parity: the while-loop dequeues at cap but never subtracts the
        // trimmed event's value — its contribution becomes permanent (it can also
        // never decay). Pin that exact semantics.
        for (int i = 0; i < MomentumSideData.MaxEventsPerType; i++)
            _sut.AddEvent(Event(1));
        Assert.AreEqual(100, _sut.SideMomentum);

        _sut.AddEvent(Event(5));

        Assert.AreEqual(105, _sut.SideMomentum);
        Assert.AreEqual(MomentumSideData.MaxEventsPerType,
            System.Linq.Enumerable.Count(_sut.GetEvents(MomentumActionType.BattleWon)));
    }

    [TestMethod]
    public void AddEvent_CapIsPerType_OtherTypeUnaffected()
    {
        for (int i = 0; i < MomentumSideData.MaxEventsPerType; i++)
            _sut.AddEvent(Event(1));

        _sut.AddEvent(Event(1, type: MomentumActionType.VillageRaided));

        Assert.AreEqual(1,
            System.Linq.Enumerable.Count(_sut.GetEvents(MomentumActionType.VillageRaided)));
        Assert.AreEqual(MomentumSideData.MaxEventsPerType,
            System.Linq.Enumerable.Count(_sut.GetEvents(MomentumActionType.BattleWon)));
    }

    // ---- Decay ----

    [TestMethod]
    public void ProcessExpiredEvents_ExpiredEvent_SubtractsValueAndRemoves()
    {
        _sut.AddEvent(Event(250, endHours: 10.0));

        int change = _sut.ProcessExpiredEvents(nowHours: 11.0);

        Assert.AreEqual(-250, change);
        Assert.AreEqual(0, _sut.SideMomentum);
        Assert.AreEqual(0, System.Linq.Enumerable.Count(_sut.GetEvents(MomentumActionType.BattleWon)));
    }

    [TestMethod]
    public void ProcessExpiredEvents_MixedExpiry_RemovesOnlyExpired()
    {
        _sut.AddEvent(Event(100, endHours: 10.0));
        _sut.AddEvent(Event(200, endHours: 50.0));

        _sut.ProcessExpiredEvents(nowHours: 20.0);

        Assert.AreEqual(200, _sut.SideMomentum);
        Assert.AreEqual(1, System.Linq.Enumerable.Count(_sut.GetEvents(MomentumActionType.BattleWon)));
    }

    [TestMethod]
    public void ProcessExpiredEvents_MultipleTypes_DrainsAll()
    {
        _sut.AddEvent(Event(100, endHours: 10.0, type: MomentumActionType.BattleWon));
        _sut.AddEvent(Event(200, endHours: 10.0, type: MomentumActionType.Sieges));
        _sut.AddEvent(Event(50, endHours: 10.0, type: MomentumActionType.RelativeStrength));

        int change = _sut.ProcessExpiredEvents(nowHours: 11.0);

        Assert.AreEqual(-350, change);
        Assert.AreEqual(0, _sut.SideMomentum);
    }

    [TestMethod]
    public void ProcessExpiredEvents_NothingExpired_NoChange()
    {
        _sut.AddEvent(Event(100, endHours: 100.0));

        int change = _sut.ProcessExpiredEvents(nowHours: 50.0);

        Assert.AreEqual(0, change);
        Assert.AreEqual(100, _sut.SideMomentum);
    }

    [TestMethod]
    public void ProcessExpiredEvents_EmptyQueues_NoChange()
    {
        int change = _sut.ProcessExpiredEvents(nowHours: 1000.0);
        Assert.AreEqual(0, change);
        Assert.AreEqual(0, _sut.SideMomentum);
    }

    [TestMethod]
    public void ProcessExpiredEvents_EventEndingExactlyNow_NotExpired()
    {
        // LOTRAOM parity: strict `EndTime < now` comparison.
        _sut.AddEvent(Event(100, endHours: 10.0));

        _sut.ProcessExpiredEvents(nowHours: 10.0);

        Assert.AreEqual(100, _sut.SideMomentum);
    }

    // ---- Kingdom enrollment bookkeeping ----

    [TestMethod]
    public void AddKingdom_New_ReturnsTrueAndContains()
    {
        Assert.IsTrue(_sut.AddKingdom("empire_w"));
        Assert.IsTrue(_sut.ContainsKingdom("empire_w"));
    }

    [TestMethod]
    public void AddKingdom_Duplicate_ReturnsFalse()
    {
        _sut.AddKingdom("empire_w");
        Assert.IsFalse(_sut.AddKingdom("empire_w"));
        Assert.AreEqual(1, _sut.KingdomIds.Count);
    }

    [TestMethod]
    public void AddKingdom_NullOrEmpty_ReturnsFalse()
    {
        Assert.IsFalse(_sut.AddKingdom(null));
        Assert.IsFalse(_sut.AddKingdom(""));
        Assert.AreEqual(0, _sut.KingdomIds.Count);
    }

    [TestMethod]
    public void RemoveKingdom_Existing_RemovesAndReturnsTrue()
    {
        _sut.AddKingdom("empire_w");
        Assert.IsTrue(_sut.RemoveKingdom("empire_w"));
        Assert.IsFalse(_sut.ContainsKingdom("empire_w"));
    }

    [TestMethod]
    public void RemoveKingdom_Unknown_ReturnsFalse()
    {
        Assert.IsFalse(_sut.RemoveKingdom("nope"));
    }

    // ---- Save-load rehydration ----

    [TestMethod]
    public void RestoreEvent_DoesNotChangeMomentum()
    {
        _sut.RestoreEvent(Event(500));
        Assert.AreEqual(0, _sut.SideMomentum);
        Assert.AreEqual(1, System.Linq.Enumerable.Count(_sut.GetEvents(MomentumActionType.BattleWon)));
    }

    [TestMethod]
    public void RestoreEvent_OverCap_TrimsOldest()
    {
        for (int i = 0; i < MomentumSideData.MaxEventsPerType + 10; i++)
            _sut.RestoreEvent(Event(1));

        Assert.AreEqual(MomentumSideData.MaxEventsPerType,
            System.Linq.Enumerable.Count(_sut.GetEvents(MomentumActionType.BattleWon)));
        Assert.AreEqual(0, _sut.SideMomentum);
    }

    [TestMethod]
    public void RestoreMomentum_SetsValueDirectly()
    {
        _sut.RestoreMomentum(1234);
        Assert.AreEqual(1234, _sut.SideMomentum);
    }
}
