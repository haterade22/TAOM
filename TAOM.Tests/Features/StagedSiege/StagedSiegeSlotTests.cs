using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Adapters;
using TAOM.Core.Logging;
using TAOM.Features.StagedSiege;
using TAOM.Features.StagedSiege.Domain;

namespace TAOM.Tests.Features.StagedSiege;

/// <summary>The one-per-process slot the AI thread, the spawn patch and the HUD read: by token, immutable, volatile.</summary>
[TestClass]
public class StagedSiegeSlotTests
{
    private object _token = null!;
    private StagedSiegeService _service = null!;

    [TestInitialize]
    public void Setup()
    {
        StagedSiegeSlot.ClearArmed();
        StagedSiegeSlot.ClearLastEnded();
        _token = new object();
        var adapter = Substitute.For<IStagedSiegeMissionAdapter>();
        adapter.MissionToken.Returns(_token);
        _service = new StagedSiegeService(adapter, StagedSiegeSceneCatalog.FromText(null, Substitute.For<IModLogger>()),
            Substitute.For<IModLogger>());
    }

    [TestCleanup]
    public void Cleanup()
    {
        StagedSiegeSlot.ClearArmed();
        StagedSiegeSlot.ClearLastEnded();
    }

    private static StageSnapshot SnapshotOf(object token) =>
        StageSnapshot.Build(token, StagedSiegeFixtures.LinePlan(), new StageMachine(StagedSide.Attacker, true), 0f, CaptureStatus.Idle);

    [TestMethod]
    public void Publish_ArmedMission_ReadBack()
    {
        StagedSiegeSlot.Arm(_service);
        var snapshot = SnapshotOf(_token);

        StagedSiegeSlot.Publish(snapshot);

        Assert.IsTrue(StagedSiegeSlot.HasArmed);
        Assert.AreSame(snapshot, StagedSiegeSlot.SnapshotFor(_token));
        Assert.AreSame(_service, StagedSiegeSlot.ServiceFor(_token));
    }

    [TestMethod]
    public void Publish_OtherMission_Dropped()
    {
        StagedSiegeSlot.Arm(_service);

        StagedSiegeSlot.Publish(SnapshotOf(new object()));

        Assert.IsNull(StagedSiegeSlot.SnapshotFor(_token));
    }

    [TestMethod]
    public void Publish_NothingArmed_Dropped()
    {
        StagedSiegeSlot.Publish(SnapshotOf(_token));

        Assert.IsNull(StagedSiegeSlot.SnapshotFor(_token));
    }

    [TestMethod]
    public void SnapshotFor_OtherMission_Null()
    {
        StagedSiegeSlot.Arm(_service);
        StagedSiegeSlot.Publish(SnapshotOf(_token));

        Assert.IsNull(StagedSiegeSlot.SnapshotFor(new object()));
        Assert.IsNull(StagedSiegeSlot.ServiceFor(new object()));
    }

    [TestMethod]
    public void ClearIf_OtherMission_Keeps()
    {
        StagedSiegeSlot.Arm(_service);
        StagedSiegeSlot.Publish(SnapshotOf(_token));

        StagedSiegeSlot.ClearIf(new object());

        Assert.IsTrue(StagedSiegeSlot.HasArmed);
        Assert.IsNotNull(StagedSiegeSlot.SnapshotFor(_token));
    }

    [TestMethod]
    public void ClearIf_Armed_ClearsKeepsLastEnded()
    {
        StagedSiegeSlot.Arm(_service);
        StagedSiegeSlot.Publish(SnapshotOf(_token));
        var when = new DateTime(2026, 10, 29, 12, 0, 0, DateTimeKind.Utc);
        StagedSiegeSlot.SetLastEnded("a_scene", StagedEnd.PlayerDefeated, when);

        StagedSiegeSlot.ClearIf(_token);

        Assert.IsFalse(StagedSiegeSlot.HasArmed);
        Assert.IsNull(StagedSiegeSlot.SnapshotFor(_token));
        Assert.IsNotNull(StagedSiegeSlot.LastEnded);
        Assert.AreEqual("a_scene", StagedSiegeSlot.LastEnded!.Value.Scene);
        Assert.AreEqual(StagedEnd.PlayerDefeated, StagedSiegeSlot.LastEnded.Value.End);
    }

    [TestMethod]
    public void ClearArmed_KeepsLastEnded()
    {
        // The next mission's OnCreated runs ClearArmed; the walls end it left must survive for that battle's inert log.
        StagedSiegeSlot.Arm(_service);
        StagedSiegeSlot.SetLastEnded("a_scene", StagedEnd.DefenderPushedBack, new DateTime(2026, 10, 29, 12, 0, 0, DateTimeKind.Utc));

        StagedSiegeSlot.ClearArmed();

        Assert.IsFalse(StagedSiegeSlot.HasArmed);
        Assert.IsNotNull(StagedSiegeSlot.LastEnded);
        Assert.AreEqual("a_scene", StagedSiegeSlot.LastEnded!.Value.Scene);
    }

    // --- the lord's hall -----------------------------------------------------------------------------------------------

    private StagedHallService _hall = null!;

    private void ArmHall(object token)
    {
        var adapter = Substitute.For<IStagedHallAdapter>();
        adapter.MissionToken.Returns(token);
        _hall = new StagedHallService(adapter, StagedSiegeSceneCatalog.FromText(null, Substitute.For<IModLogger>()), Substitute.For<IModLogger>());
        StagedSiegeSlot.ArmHall(_hall);
    }

    private static HallSnapshot HallOf(object token, float progress = 0.3f, bool finished = false) =>
        new(token, progress, CaptureStatus.Rising, new StagePoint(1f, 2f, 3f), finished);

    [TestMethod]
    public void PublishHall_ArmedMission_ReadBack()
    {
        ArmHall(_token);
        var snapshot = HallOf(_token);

        StagedSiegeSlot.PublishHall(snapshot);

        Assert.AreSame(snapshot, StagedSiegeSlot.HallFor(_token));
    }

    [TestMethod]
    public void PublishHall_OtherMission_OrNothingArmed_Dropped()
    {
        StagedSiegeSlot.PublishHall(HallOf(_token));
        Assert.IsNull(StagedSiegeSlot.HallFor(_token));

        ArmHall(_token);
        StagedSiegeSlot.PublishHall(HallOf(new object()));

        Assert.IsNull(StagedSiegeSlot.HallFor(_token));
    }

    [TestMethod]
    public void HallFor_OtherMission_Null()
    {
        ArmHall(_token);
        StagedSiegeSlot.PublishHall(HallOf(_token));

        Assert.IsNull(StagedSiegeSlot.HallFor(new object()));
        Assert.IsNull(StagedSiegeSlot.HallFor(null));
    }

    [TestMethod]
    public void ClearHallIf_OtherMissionKeeps_OwnClears()
    {
        ArmHall(_token);
        StagedSiegeSlot.PublishHall(HallOf(_token));

        StagedSiegeSlot.ClearHallIf(new object());
        Assert.IsNotNull(StagedSiegeSlot.HallFor(_token));

        StagedSiegeSlot.ClearHallIf(_token);
        Assert.IsNull(StagedSiegeSlot.HallFor(_token));
    }

    [TestMethod]
    public void ClearArmed_AlsoDropsTheHall()
    {
        ArmHall(_token);
        StagedSiegeSlot.PublishHall(HallOf(_token));

        StagedSiegeSlot.ClearArmed();

        Assert.IsNull(StagedSiegeSlot.HallFor(_token));
    }

    [TestMethod]
    public void TheHallAndTheWalls_DoNotShareASnapshot()
    {
        ArmHall(_token);
        StagedSiegeSlot.PublishHall(HallOf(_token));

        Assert.IsNull(StagedSiegeSlot.SnapshotFor(_token), "the wall snapshot reader sees no stage in a hall mission");
        Assert.IsFalse(StagedSiegeSlot.HasArmed);
    }

    [TestMethod]
    public void ClearArmed_DropsServiceAndSnapshot()
    {
        StagedSiegeSlot.Arm(_service);
        StagedSiegeSlot.Publish(SnapshotOf(_token));

        StagedSiegeSlot.ClearArmed();

        Assert.IsFalse(StagedSiegeSlot.HasArmed);
        Assert.IsNull(StagedSiegeSlot.SnapshotFor(_token));
    }
}
