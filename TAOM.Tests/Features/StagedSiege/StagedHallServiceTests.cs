using System;
using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using TAOM.Adapters;
using TAOM.Core.Logging;
using TAOM.Features.StagedSiege;
using TAOM.Features.StagedSiege.Domain;

namespace TAOM.Tests.Features.StagedSiege;

/// <summary>Point E in the lord's hall (docs/features/staged-siege.md "The lord's hall"): arming, the capture, the end and the faults.</summary>
[TestClass]
public partial class StagedHallServiceTests
{
    private const string Settlement = "castle_sturgia_a";
    private const string WallsScene = "sturgia_castle_siege_001";

    private IStagedHallAdapter _m = null!;
    private IModLogger _log = null!;
    private StagedHallService _svc = null!;
    private object _token = null!;
    private float _now;
    private ZoneCount _inE;
    private int _attackersStanding;
    private int _defendersStanding;

    [TestInitialize]
    public void Setup()
    {
        StagedSiegeSlot.ClearArmed();
        StagedSiegeSlot.ClearLastEnded();
        StagedSiegeSlot.ClearCampaignSettlement();
        _now = 0f;
        _token = new object();
        _inE = new ZoneCount(0, 0);
        _attackersStanding = 30;
        _defendersStanding = 40;
        _log = Substitute.For<IModLogger>();
        _m = Substitute.For<IStagedHallAdapter>();
        _m.MissionToken.Returns(_token);
        _m.Time.Returns(_ => _now);
        _m.IsHallMission.Returns(true);
        _m.HasCampaign.Returns(true);
        _m.IsInLordsHall.Returns(true);
        _m.EncounterSettlementId.Returns(Settlement);
        _m.WallsSceneName.Returns(WallsScene);
        _m.ReadMarkers().Returns(new List<HallMarker>
        {
            new(1, 10f, 10f, 5f, 3f), new(2, 50f, 50f, 5f, 3f), new(3, 100f, 100f, 5f, 3f), new(3, 104f, 100f, 7f, 3f),
        });
        _m.SpawnFlag(Arg.Any<StagePoint>()).Returns(1);
        _m.CountInHall(Arg.Any<HallZone>()).Returns(_ => _inE);
        _m.CountStanding(StagedSide.Attacker).Returns(_ => _attackersStanding);
        _m.CountStanding(StagedSide.Defender).Returns(_ => _defendersStanding);
        _svc = new StagedHallService(_m, StagedSiegeSceneCatalog.FromText(StagedSiegeSceneCatalogTests.PilotText, Substitute.For<IModLogger>()), _log);
    }

    [TestCleanup]
    public void Cleanup()
    {
        StagedSiegeSlot.ClearArmed();
        StagedSiegeSlot.ClearLastEnded();
        StagedSiegeSlot.ClearCampaignSettlement();
    }

    [TestMethod]
    public void TryArm_SwitchOff_NeverLoadsTheCatalog()
    {
        // The switch gates I/O: with it off (the shipped default) a hall fight must not load staged_siege.json.
        var catalogLog = Substitute.For<IModLogger>();
        var svc = new StagedHallService(_m, StagedSiegeSceneCatalog.FromText(StagedSiegeSceneCatalogTests.PilotText, catalogLog), _log);

        Assert.IsFalse(svc.TryArm(false));

        catalogLog.DidNotReceive().LogInfo(Arg.Any<string>());
    }

    private static void RecordWallsEnd(StagedEnd end = StagedEnd.DefenderPushedBack) =>
        StagedSiegeSlot.SetLastEnded(WallsScene, end, DateTime.UtcNow.AddSeconds(-20));

    private void At(float time)
    {
        _now = time;
        _svc.Tick();
    }

    private void ArmAndStart()
    {
        Assert.IsTrue(_svc.TryArm(true));
        At(1f);
    }

    // Half-second passes until the hall is taken.
    private void CaptureTheHall()
    {
        _inE = new ZoneCount(10, 0);
        for (var i = 0; i < 100 && !_svc.IsCaptured; i++)
            At(_now + 0.5f);
        Assert.IsTrue(_svc.IsCaptured, "the hall was never captured");
    }

    private void AssertLogged(string fragment) =>
        _log.Received().LogInfo(Arg.Is<string>(s => s.StartsWith("[StagedSiege] ") && s.Contains(fragment)));

    // --- arming ----------------------------------------------------------------------------------------------------

    [TestMethod]
    public void TryArm_AllPass_ArmsFromTheSavedSiegeStateAndSpawnsTheFlagAtTheCentroid()
    {
        Assert.IsNull(StagedSiegeSlot.LastEnded, "no in-memory walls end is needed: a retreat, a reload or a restart arms the same");

        Assert.IsTrue(_svc.TryArm(true));

        Assert.IsTrue(_svc.IsArmed);
        AssertLogged($"hall armed: settlement {Settlement}, area 3, markers 2");
        _m.Received(1).SpawnFlag(Arg.Is<StagePoint>(p => Math.Abs(p.X - 102f) < 0.01f && Math.Abs(p.Y - 100f) < 0.01f && Math.Abs(p.Z - 6f) < 0.01f));
        _m.Received(1).ColourFlag(StagedSide.Defender);
        var snapshot = StagedSiegeSlot.HallFor(_token)!;
        Assert.IsFalse(snapshot.Finished);
        Assert.AreEqual(0f, snapshot.Progress);
        Assert.AreEqual(102f, snapshot.Centre.X, 0.01f);
    }

    [DataTestMethod]
    [DataRow("NoCampaign")]
    [DataRow("Client")]
    [DataRow("NotInTheLordsHall")]
    [DataRow("Disabled")]
    [DataRow("WallsSceneNotRecorded")]
    [DataRow("WallsSceneUnknown")]
    [DataRow("NoMarkers")]
    public void TryArm_GateFails_LogsReasonAndArmsNothing(string verdict)
    {
        var enabled = true;
        var expected = verdict;
        switch (verdict)
        {
            case "NoCampaign": _m.HasCampaign.Returns(false); break;
            case "Client": _m.IsClientOrReplay.Returns(true); break;
            case "NotInTheLordsHall": _m.IsInLordsHall.Returns(false); break;
            case "Disabled": enabled = false; break;
            case "WallsSceneNotRecorded": _m.WallsSceneName.Returns(string.Empty); break;
            case "WallsSceneUnknown": _m.WallsSceneName.Returns("vlandia_castle_siege_099"); expected = "WallsSceneNotRecorded"; break;
            case "NoMarkers": _m.ReadMarkers().Returns(new List<HallMarker>()); break;
        }

        Assert.IsFalse(_svc.TryArm(enabled));

        Assert.IsFalse(_svc.IsArmed);
        Assert.IsNull(StagedSiegeSlot.HallFor(_token));
        AssertLogged("hall inert: " + expected);
        _m.DidNotReceive().SpawnFlag(Arg.Any<StagePoint>());
    }

    [TestMethod]
    public void TryArm_LeavesTheWallsEndRecordAlone_WhateverItSays()
    {
        RecordWallsEnd(StagedEnd.PlayerDefeated);

        Assert.IsTrue(_svc.TryArm(true), "the hall no longer depends on how the walls fight ended");

        Assert.IsNotNull(StagedSiegeSlot.LastEnded);
        Assert.AreEqual(StagedEnd.PlayerDefeated, StagedSiegeSlot.LastEnded!.Value.End);
    }

    [TestMethod]
    public void TryArm_NotAHallMission_TouchesNothing()
    {
        RecordWallsEnd();
        _m.IsHallMission.Returns(false);

        Assert.IsFalse(_svc.TryArm(true));

        Assert.IsNotNull(StagedSiegeSlot.LastEnded);
        Assert.IsNull(StagedSiegeSlot.HallFor(_token));
    }

    [TestMethod]
    public void TryArm_ReadThrows_FaultsAndArmsNothing()
    {
        _m.ReadMarkers().Throws(new InvalidOperationException("no scene"));

        Assert.IsFalse(_svc.TryArm(true));

        Assert.IsFalse(_svc.IsArmed);
        Assert.IsTrue(_svc.IsFaulted);
        _log.Received(1).LogError(Arg.Is<string>(s => s.Contains("fault in TryArm")));
        Assert.IsNull(StagedSiegeSlot.HallFor(_token));
    }

    // --- the capture -----------------------------------------------------------------------------------------------

    [TestMethod]
    public void Tick_CountsOnlyOncePerReconcile()
    {
        ArmAndStart();
        _m.ClearReceivedCalls();

        At(1.2f);
        At(1.4f);

        _m.DidNotReceive().CountInHall(Arg.Any<HallZone>());

        At(1.6f);

        _m.Received(1).CountInHall(Arg.Any<HallZone>());
    }

    [TestMethod]
    public void Tick_AttackersAlone_RiseThenCapture_StopSpawnersAnnounceOnceAndRecolour()
    {
        ArmAndStart();

        CaptureTheHall();
        for (var i = 0; i < 6; i++)
            At(_now + 0.5f);

        Assert.IsTrue(_svc.TryGetCaptured());
        _m.Received(1).StopSpawners();
        _m.Received(1).AnnounceHallTaken();
        _m.Received(1).ColourFlag(StagedSide.Attacker);
        AssertLogged("hall E captured at");
        var snapshot = StagedSiegeSlot.HallFor(_token)!;
        Assert.IsTrue(snapshot.Finished);
        Assert.AreEqual(CaptureStatus.Captured, snapshot.Status);
    }

    [TestMethod]
    public void Tick_RisingProgress_IsPublishedAsItClimbs()
    {
        ArmAndStart();
        _inE = new ZoneCount(10, 0);

        At(1.5f);
        At(2f);
        At(2.5f);

        var snapshot = StagedSiegeSlot.HallFor(_token)!;
        Assert.AreEqual(CaptureStatus.Rising, snapshot.Status);
        Assert.IsTrue(snapshot.Progress > 0f && snapshot.Progress < 1f);
        Assert.IsFalse(snapshot.Finished);
    }

    [TestMethod]
    public void Tick_DefendersDominating_DrainTheBar()
    {
        ArmAndStart();
        _inE = new ZoneCount(10, 0);
        for (var i = 0; i < 8; i++)
            At(_now + 0.5f);
        var risen = StagedSiegeSlot.HallFor(_token)!.Progress;

        _inE = new ZoneCount(0, 12);
        At(_now + 0.5f);
        At(_now + 0.5f);

        var snapshot = StagedSiegeSlot.HallFor(_token)!;
        Assert.AreEqual(CaptureStatus.Falling, snapshot.Status);
        Assert.IsTrue(snapshot.Progress < risen);
        _m.DidNotReceive().StopSpawners();
    }

    [TestMethod]
    public void Tick_ASideWiped_LeavesTheEndToVanilla()
    {
        ArmAndStart();
        _defendersStanding = 0;
        _inE = new ZoneCount(10, 0);

        for (var i = 0; i < 80; i++)
            At(_now + 0.5f);

        Assert.IsFalse(_svc.IsCaptured, "BattleEndLogic ends a depleted hall first; the service steps aside");
        _m.DidNotReceive().StopSpawners();
        _m.DidNotReceive().AnnounceHallTaken();
    }

    [TestMethod]
    public void Tick_AttackersWiped_NoCapture()
    {
        ArmAndStart();
        _attackersStanding = 0;
        _inE = new ZoneCount(10, 0);

        for (var i = 0; i < 80; i++)
            At(_now + 0.5f);

        Assert.IsFalse(_svc.IsCaptured);
    }

    [TestMethod]
    public void Tick_NotArmed_DoesNothing()
    {
        _m.HasCampaign.Returns(false);
        _svc.TryArm(true);
        _m.ClearReceivedCalls();

        At(5f);
        At(10f);

        _m.DidNotReceive().CountInHall(Arg.Any<HallZone>());
        _m.DidNotReceive().StopSpawners();
        Assert.IsFalse(_svc.IsCaptured);
        Assert.IsFalse(_svc.TryGetCaptured());
    }

    [TestMethod]
    public void Tick_ANaNClock_IsIgnored()
    {
        ArmAndStart();
        _m.ClearReceivedCalls();

        At(float.NaN);

        _m.DidNotReceive().CountInHall(Arg.Any<HallZone>());
        Assert.IsFalse(_svc.IsFaulted);
    }

    // --- faults and teardown ---------------------------------------------------------------------------------------

    [TestMethod]
    public void Tick_CountThrows_FaultsOnce_ClearsTheHud_AndNeverCaptures()
    {
        ArmAndStart();
        _m.CountInHall(Arg.Any<HallZone>()).Throws(new InvalidOperationException("scene gone"));

        At(2f);
        At(3f);
        At(4f);

        Assert.IsTrue(_svc.IsFaulted);
        _log.Received(1).LogError(Arg.Is<string>(s => s.Contains("fault in Tick")));
        Assert.IsNull(StagedSiegeSlot.HallFor(_token));
        Assert.IsFalse(_svc.TryGetCaptured());
    }

    [TestMethod]
    public void Tick_AfterTheCapture_ReadsNothingMore()
    {
        ArmAndStart();
        CaptureTheHall();
        _m.ClearReceivedCalls();

        At(_now + 1f);
        At(_now + 1f);

        _m.DidNotReceive().CountInHall(Arg.Any<HallZone>());
        _m.DidNotReceive().CountStanding(Arg.Any<StagedSide>());
        Assert.IsTrue(_svc.TryGetCaptured());
    }

    [TestMethod]
    public void Shutdown_ClearsTheHallSlot_Twice_IsHarmless()
    {
        ArmAndStart();

        _svc.Shutdown();
        _svc.Shutdown();

        Assert.IsNull(StagedSiegeSlot.HallFor(_token));
        At(5f);
        Assert.IsFalse(_svc.IsCaptured);
    }

    [TestMethod]
    public void DescribeStatus_NamesTheHallState()
    {
        Assert.IsTrue(_svc.DescribeStatus().Contains("inert"));
        ArmAndStart();
        _inE = new ZoneCount(4, 1);
        At(2f);

        var text = _svc.DescribeStatus();

        StringAssert.Contains(text, "hall");
        StringAssert.Contains(text, Settlement);
        StringAssert.Contains(text, "area 3");
    }
}
