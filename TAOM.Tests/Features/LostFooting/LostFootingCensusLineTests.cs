using System.Globalization;
using System.Threading;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.LostFooting;
using TAOM.Features.LostFooting.Domain;

namespace TAOM.Tests.Features.LostFooting;

/// <summary>The text of the Lost Footing log lines. Testers read these, so the shape is pinned.</summary>
[TestClass]
public class LostFootingCensusLineTests
{
    private static LostFootingTally TallyOf(params (StrandClass Class, AgentKind Kind)[] stranded)
    {
        var s = new LostFootingMissionState();
        var i = 0;
        foreach (var (c, k) in stranded)
        {
            var e = new StrandEntry { Kind = k, StrandedSince = 0f };
            switch (c)
            {
                case StrandClass.OffMesh: e.OffMesh = true; break;
                case StrandClass.WaterStill: e.WaterStill = true; break;
                default: e.LandBelow = true; break;
            }

            s.Entries[new object()] = e;
            i++;
        }

        return LostFootingRules.Tally(s);
    }

    [TestMethod]
    public void Start_NamesTheSceneTypeWaterAndAgents()
    {
        var line = CensusLine.Start(new SceneFacts { SceneId = "some_scene", MissionType = "siege", WaterLevel = 12.5f }, 480);

        Assert.AreEqual("[LostFooting] start scene=some_scene type=siege water=12.50 agents=480", line);
    }

    [TestMethod]
    public void Start_ANonFiniteWaterLevel_ReadsAsNotAvailable()
    {
        var line = CensusLine.Start(new SceneFacts { SceneId = "s", MissionType = "field", WaterLevel = float.NaN }, 3);

        Assert.AreEqual("[LostFooting] start scene=s type=field water=n/a agents=3", line);
    }

    [TestMethod]
    public void ClassLine_ShowsCountsByKind_AndTheConfirmedCountForOffMesh()
    {
        var tally = TallyOf((StrandClass.OffMesh, AgentKind.Soldier), (StrandClass.OffMesh, AgentKind.Soldier), (StrandClass.OffMesh, AgentKind.Creature));

        var line = CensusLine.Class(StrandClass.OffMesh, 130f, tally, 412);

        Assert.AreEqual(
            "[LostFooting] census t=130s class=offmesh stranded=3 confirmed=0 soldier=2 router=0 mount=0 creature=1 object=0 other=0 tracked=412",
            line);
    }

    [TestMethod]
    public void ClassLine_WaterClasses_CarryNoConfirmedField()
    {
        var tally = TallyOf((StrandClass.WaterStill, AgentKind.Router), (StrandClass.LandBelowWater, AgentKind.UsingObject));

        Assert.AreEqual(
            "[LostFooting] census t=10s class=waterstill stranded=1 soldier=0 router=1 mount=0 creature=0 object=0 other=0 tracked=9",
            CensusLine.Class(StrandClass.WaterStill, 10f, tally, 9));
        Assert.AreEqual(
            "[LostFooting] census t=10s class=landbelowwater stranded=1 soldier=0 router=0 mount=0 creature=0 object=1 other=0 tracked=9",
            CensusLine.Class(StrandClass.LandBelowWater, 10f, tally, 9));
    }

    [TestMethod]
    public void Sample_ShowsPositionDepthTimeAndBothDistances()
    {
        var e = new StrandEntry
        {
            Kind = AgentKind.Soldier, WaterStill = true, StrandedSince = 100f, X = 12.34f, Y = -5f, Z = 7.8f, Depth = 0.4f,
            DistToFormation = 3.25f, DistToDestination = 15f,
        };

        var line = CensusLine.Sample(StrandClass.WaterStill, e, 112.4f);

        Assert.AreEqual(
            "[LostFooting] sample class=waterstill kind=soldier x=12.3 y=-5.0 z=7.8 depth=0.40 secs=12.4 toFormation=3.3 toDest=15.0 unreachable=no",
            line);
    }

    [TestMethod]
    public void Sample_MissingDistances_ReadAsNotAvailable()
    {
        var e = new StrandEntry { Kind = AgentKind.Creature, LandBelow = true, StrandedSince = 0f };

        var line = CensusLine.Sample(StrandClass.LandBelowWater, e, 5f);

        StringAssert.Contains(line, "depth=n/a");
        StringAssert.Contains(line, "toFormation=n/a toDest=n/a unreachable=no");
    }

    [TestMethod]
    public void Sample_OffMeshWithASnapCandidate_ShowsItsDistanceAndWhetherItIsOnTheMesh()
    {
        var e = new StrandEntry
        {
            Kind = AgentKind.Soldier, OffMesh = true, StrandedSince = 0f,
            Detail = new OffMeshDetail { Confirmed = true, HasSnap = true, SnapDistance = 2.1f, SnapHasNavMesh = true },
        };

        StringAssert.EndsWith(CensusLine.Sample(StrandClass.OffMesh, e, 5f), " confirmed=yes dynamic=no snap=2.1 snapOnMesh=yes");
    }

    [TestMethod]
    public void Sample_OffMeshWithNoSnapCandidate_SaysNone()
    {
        var e = new StrandEntry
        {
            Kind = AgentKind.Soldier, OffMesh = true, StrandedSince = 0f,
            Detail = new OffMeshDetail { Confirmed = false, OnDynamicNavMesh = true },
        };

        StringAssert.EndsWith(CensusLine.Sample(StrandClass.OffMesh, e, 5f), " confirmed=no dynamic=yes snap=none");
    }

    [TestMethod]
    public void Summary_ShowsTheSceneEpisodesByEndAndKind_ThePeaksAndTheCost()
    {
        var s = new LostFootingMissionState
        {
            Clock = 1234.4f, SceneId = "some_scene", Recovered = 2, Removed = 1, AtEnd = 0, LongestEpisode = 45.25f,
            PeakOffMesh = 7, PeakConfirmed = 5, PeakWaterStill = 2, PeakLandBelow = 1, Probes = 9000, TotalMs = 12.345, MaxTickMs = 0.5,
        };
        s.EpisodeKinds[(int)AgentKind.Soldier] = 2;
        s.EpisodeKinds[(int)AgentKind.Creature] = 1;

        Assert.AreEqual(
            "[LostFooting] summary scene=some_scene time=1234s episodes=3 recovered=2 removed=1 atEnd=0 longest=45.3s " +
            "peak offmesh=7 confirmed=5 waterstill=2 landbelowwater=1 soldier=2 router=0 mount=0 creature=1 object=0 other=0 " +
            "cost probes=9000 totalMs=12.35 maxTickMs=0.50",
            CensusLine.Summary(s));
    }

    [DataTestMethod]
    [DataRow(AgentKind.Soldier, "soldier")]
    [DataRow(AgentKind.Router, "router")]
    [DataRow(AgentKind.RiderlessMount, "mount")]
    [DataRow(AgentKind.Creature, "creature")]
    [DataRow(AgentKind.UsingObject, "object")]
    [DataRow(AgentKind.Other, "other")]
    public void Sample_NamesEveryKind(AgentKind kind, string name)
    {
        var e = new StrandEntry { Kind = kind, WaterStill = true, StrandedSince = 0f };

        StringAssert.Contains(CensusLine.Sample(StrandClass.WaterStill, e, 1f), " kind=" + name + " ");
    }

    [TestMethod]
    public void Sample_AnUnreachableAgent_SaysSo()
    {
        var e = new StrandEntry { Kind = AgentKind.Soldier, WaterStill = true, StrandedSince = 0f, Unreachable = true };

        StringAssert.Contains(CensusLine.Sample(StrandClass.WaterStill, e, 1f), " unreachable=yes");
    }

    [TestMethod]
    public void Hotspot_NamesTheCellBoundsAndTheAgentSeconds()
    {
        var line = CensusLine.Hotspot(2, new HotCell { CellX = 2, CellY = -1, AgentSeconds = 45f }, 5f);

        Assert.AreEqual("[LostFooting] hotspot 2 x=10..15 y=-5..0 agentSeconds=45.0", line);
    }

    [TestMethod]
    public void Lines_UseTheInvariantCulture_WhateverTheMachine()
    {
        var saved = Thread.CurrentThread.CurrentCulture;
        try
        {
            Thread.CurrentThread.CurrentCulture = new CultureInfo("de-DE");

            Assert.AreEqual("[LostFooting] start scene=s type=field water=1.50 agents=1",
                CensusLine.Start(new SceneFacts { SceneId = "s", MissionType = "field", WaterLevel = 1.5f }, 1));
        }
        finally
        {
            Thread.CurrentThread.CurrentCulture = saved;
        }
    }
}
