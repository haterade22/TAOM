using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Core.Logging;
using TAOM.Features.Diplomacy.Models;
using TAOM.Features.WarOfTheRingMomentum;
using TAOM.Features.WarOfTheRingMomentum.Domain;

namespace TAOM.Tests.Features.WarOfTheRingMomentum;

[TestClass]
public class MomentumStateStoreTests
{
    private IModLogger _logger = null!;
    private MomentumStateStore _sut = null!;

    [TestInitialize]
    public void Setup()
    {
        _logger = Substitute.For<IModLogger>();
        _sut = new MomentumStateStore(_logger);
    }

    private static MomentumEvent Event(int value, double endHours = 1000.0,
        MomentumActionType type = MomentumActionType.BattleWon, string description = "test")
    {
        return new MomentumEvent(value, description, type, endHours);
    }

    // ---- Round trip ----

    [TestMethod]
    public void SerializeDeserialize_FullState_RoundTrips()
    {
        _sut.State.MarkWarStarted();
        _sut.State.Free.AddKingdom("empire_w");
        _sut.State.Free.AddKingdom("vlandia");
        _sut.State.Evil.AddKingdom("empire_s");
        _sut.State.Free.AddEvent(Event(250, 100.0, MomentumActionType.Sieges, "took Minas Tirith"));
        _sut.State.Free.AddEvent(Event(120, 90.0, MomentumActionType.BattleWon, "won a battle"));
        _sut.State.Evil.AddEvent(Event(300, 80.0, MomentumActionType.BattleWon, "orcs won"));
        _sut.State.Free.TotalStats.AddKills(42);
        _sut.State.Free.TotalStats.AddRaid();
        _sut.State.Evil.TotalStats.AddSettlementCaptured();
        _sut.PlayerEvents.Add(MomentumActionType.BattleWon);
        _sut.PlayerEvents.Add(MomentumActionType.Sieges);

        var data = _sut.Serialize();
        var restored = new MomentumStateStore(_logger);
        restored.Deserialize(data);

        Assert.IsTrue(restored.State.HasWarStarted);
        Assert.IsFalse(restored.State.HasWarEnded);
        CollectionAssert.AreEqual(new[] { "empire_w", "vlandia" }, restored.State.Free.KingdomIds.ToArray());
        CollectionAssert.AreEqual(new[] { "empire_s" }, restored.State.Evil.KingdomIds.ToArray());
        Assert.AreEqual(370, restored.State.Free.SideMomentum);
        Assert.AreEqual(300, restored.State.Evil.SideMomentum);

        var freeSieges = restored.State.Free.GetEvents(MomentumActionType.Sieges).ToList();
        Assert.AreEqual(1, freeSieges.Count);
        Assert.AreEqual(250, freeSieges[0].Value);
        Assert.AreEqual(100.0, freeSieges[0].EndTimeHours, 0.0001);
        Assert.AreEqual("took Minas Tirith", freeSieges[0].Description);

        Assert.AreEqual(42, restored.State.Free.TotalStats.TotalKills);
        Assert.AreEqual(1, restored.State.Free.TotalStats.TotalVillagesRaided);
        Assert.AreEqual(1, restored.State.Evil.TotalStats.TotalSettlementsCaptured);

        CollectionAssert.AreEqual(
            new[] { MomentumActionType.BattleWon, MomentumActionType.Sieges },
            restored.PlayerEvents.ToArray());
    }

    [TestMethod]
    public void SerializeDeserialize_EndedWar_RoundTripsVictor()
    {
        _sut.State.MarkWarStarted();
        _sut.State.MarkWarEnded(WarOutcome.EvilVictory);

        var restored = new MomentumStateStore(_logger);
        restored.Deserialize(_sut.Serialize());

        Assert.IsTrue(restored.State.HasWarEnded);
        Assert.AreEqual(WarOutcome.EvilVictory, restored.State.Victor);
    }

    [TestMethod]
    public void SerializeDeserialize_MomentumDivergedFromQueues_PreservesMomentum()
    {
        // Trimmed/decay-immune contributions mean momentum ≠ Σ(queue values):
        // momentum must persist independently of the event queues.
        _sut.State.Free.AddEvent(Event(100));
        _sut.State.Free.EditMomentum(77); // simulate a trimmed contribution

        var restored = new MomentumStateStore(_logger);
        restored.Deserialize(_sut.Serialize());

        Assert.AreEqual(177, restored.State.Free.SideMomentum);
    }

    [TestMethod]
    public void SerializeDeserialize_PipeInDescription_Survives()
    {
        _sut.State.Free.AddEvent(Event(50, 10.0, MomentumActionType.BattleWon, "army of Gondor | led by Boromir"));

        var restored = new MomentumStateStore(_logger);
        restored.Deserialize(_sut.Serialize());

        var ev = restored.State.Free.GetEvents(MomentumActionType.BattleWon).Single();
        Assert.AreEqual("army of Gondor | led by Boromir", ev.Description);
        Assert.AreEqual(50, ev.Value);
    }

    [TestMethod]
    public void Deserialize_RestoredEvents_KeepDecayOrder()
    {
        _sut.State.Free.AddEvent(Event(10, 30.0));
        _sut.State.Free.AddEvent(Event(20, 60.0));

        var restored = new MomentumStateStore(_logger);
        restored.Deserialize(_sut.Serialize());
        restored.State.Free.ProcessExpiredEvents(nowHours: 40.0);

        Assert.AreEqual(20, restored.State.Free.SideMomentum);
    }

    // ---- Hardening ----

    [TestMethod]
    public void Deserialize_Null_FreshState()
    {
        _sut.Deserialize(null);
        Assert.IsFalse(_sut.State.HasWarStarted);
        Assert.AreEqual(0, _sut.State.Free.SideMomentum);
    }

    [TestMethod]
    public void Deserialize_EmptyDict_FreshState()
    {
        _sut.Deserialize(new Dictionary<string, string>());
        Assert.IsFalse(_sut.State.HasWarStarted);
    }

    [TestMethod]
    public void Deserialize_NaNEndHours_SkipsEventAndWarns()
    {
        var data = _sut.Serialize();
        data["free.ev.BattleWon.0"] = "100|NaN|bad event";

        var restored = new MomentumStateStore(_logger);
        restored.Deserialize(data);

        Assert.AreEqual(0, restored.State.Free.GetEvents(MomentumActionType.BattleWon).Count());
        _logger.Received().LogWarning(Arg.Is<string>(s => s.Contains("free.ev.BattleWon.0")));
    }

    [TestMethod]
    public void Deserialize_InfinityEndHours_SkipsEvent()
    {
        var data = _sut.Serialize();
        data["evil.ev.Sieges.0"] = "100|Infinity|bad event";

        var restored = new MomentumStateStore(_logger);
        restored.Deserialize(data);

        Assert.AreEqual(0, restored.State.Evil.GetEvents(MomentumActionType.Sieges).Count());
    }

    [TestMethod]
    public void Deserialize_UnparseableEventValue_SkipsEvent()
    {
        var data = _sut.Serialize();
        data["free.ev.BattleWon.0"] = "notanumber|10.0|bad event";

        var restored = new MomentumStateStore(_logger);
        restored.Deserialize(data);

        Assert.AreEqual(0, restored.State.Free.GetEvents(MomentumActionType.BattleWon).Count());
    }

    [TestMethod]
    public void Deserialize_UnknownActionTypeInKey_SkipsEvent()
    {
        var data = _sut.Serialize();
        data["free.ev.NotAType.0"] = "100|10.0|bad event";

        var restored = new MomentumStateStore(_logger);
        restored.Deserialize(data);

        Assert.AreEqual(0, restored.State.Free.SideMomentum);
        _logger.Received().LogWarning(Arg.Is<string>(s => s.Contains("NotAType")));
    }

    [TestMethod]
    public void Deserialize_UnknownPlayerEventName_SkipsEntry()
    {
        var data = _sut.Serialize();
        data["player.events"] = "BattleWon,Bogus,Sieges";

        var restored = new MomentumStateStore(_logger);
        restored.Deserialize(data);

        CollectionAssert.AreEqual(
            new[] { MomentumActionType.BattleWon, MomentumActionType.Sieges },
            restored.PlayerEvents.ToArray());
    }

    [TestMethod]
    public void Deserialize_UnparseableMomentum_DefaultsToZeroAndWarns()
    {
        var data = _sut.Serialize();
        data["free.momentum"] = "garbage";

        var restored = new MomentumStateStore(_logger);
        restored.Deserialize(data);

        Assert.AreEqual(0, restored.State.Free.SideMomentum);
        _logger.Received().LogWarning(Arg.Is<string>(s => s.Contains("free.momentum")));
    }

    [TestMethod]
    public void Deserialize_UnknownVictorName_DefaultsToNone()
    {
        var data = _sut.Serialize();
        data["victor"] = "Sauron";

        var restored = new MomentumStateStore(_logger);
        restored.Deserialize(data);

        Assert.AreEqual(WarOutcome.None, restored.State.Victor);
    }

    [TestMethod]
    public void Deserialize_OverCapEvents_ReTrims()
    {
        var data = new Dictionary<string, string> { ["version"] = "1" };
        for (int i = 0; i < 150; i++)
            data[$"free.ev.BattleWon.{i}"] = "1|10.0|e" + i;

        var restored = new MomentumStateStore(_logger);
        restored.Deserialize(data);

        Assert.AreEqual(MomentumSideData.MaxEventsPerType,
            restored.State.Free.GetEvents(MomentumActionType.BattleWon).Count());
    }

    [TestMethod]
    public void Deserialize_CalledTwice_DoesNotAccumulate()
    {
        _sut.State.Free.AddKingdom("empire_w");
        _sut.State.Free.AddEvent(Event(100));
        var data = _sut.Serialize();

        var restored = new MomentumStateStore(_logger);
        restored.Deserialize(data);
        restored.Deserialize(data);

        Assert.AreEqual(1, restored.State.Free.KingdomIds.Count);
        Assert.AreEqual(100, restored.State.Free.SideMomentum);
        Assert.AreEqual(1, restored.State.Free.GetEvents(MomentumActionType.BattleWon).Count());
    }

    // ---- Lifecycle ----

    [TestMethod]
    public void ResetForNewGame_ClearsEverything()
    {
        _sut.State.MarkWarStarted();
        _sut.State.Free.AddEvent(Event(100));
        _sut.PlayerEvents.Add(MomentumActionType.Sieges);

        _sut.ResetForNewGame();

        Assert.IsFalse(_sut.State.HasWarStarted);
        Assert.AreEqual(0, _sut.State.Free.SideMomentum);
        Assert.AreEqual(0, _sut.PlayerEvents.Count);
    }

    [TestMethod]
    public void NotifyMomentumChanged_RaisesEvent()
    {
        int fired = 0;
        _sut.MomentumChanged += () => fired++;

        _sut.NotifyMomentumChanged();

        Assert.AreEqual(1, fired);
    }

    [TestMethod]
    public void NotifyMomentumChanged_NoSubscribers_DoesNotThrow()
    {
        _sut.NotifyMomentumChanged();
    }
}
