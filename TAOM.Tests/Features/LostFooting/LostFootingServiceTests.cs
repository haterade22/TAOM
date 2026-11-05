using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Adapters;
using TAOM.Core.Logging;
using TAOM.Features.LostFooting;
using TAOM.Features.LostFooting.Domain;

namespace TAOM.Tests.Features.LostFooting;

/// <summary>The Lost Footing service: the validated clock, the gate, the toggle, the pass cursor, the census and the summary.</summary>
[TestClass]
public class LostFootingServiceTests
{
    private FakeLostFootingAdapter _adapter = null!;
    private ILostFootingSettingsProvider _settings = null!;
    private List<string> _lines = null!;
    private bool _open;
    private int _gateCalls;
    private LostFootingTunables _tunables = null!;
    private LostFootingService _sut = null!;

    [TestInitialize]
    public void Setup()
    {
        _adapter = new FakeLostFootingAdapter();
        _settings = Substitute.For<ILostFootingSettingsProvider>();
        _settings.CensusEnabled.Returns(true);
        _lines = new List<string>();
        var logger = Substitute.For<IModLogger>();
        logger.When(l => l.LogInfo(Arg.Any<string>())).Do(c => _lines.Add(c.Arg<string>()));
        _open = true;
        _gateCalls = 0;
        _logger = logger;
        _tunables = new LostFootingTunables();
        _sut = New(_tunables);
    }

    private IModLogger _logger = null!;
    private SteppingStampClock _clock = new SteppingStampClock(4);

    private LostFootingService New(LostFootingTunables t) =>
        new LostFootingService(_adapter, _settings, _logger, t, _clock, () => { _gateCalls++; return _open; });

    private void Seconds(int n)
    {
        for (var i = 0; i < n; i++)
            _sut.Tick(1f);
    }

    private static AgentProbe Wading() => new AgentProbe
    {
        InWaterMode = true, Speed = 0f, DistToDestination = 20f, X = 10f, Y = 10f, Z = 4f,
    };

    private static AgentProbe Lost() => new AgentProbe { FaceMiss = true, X = 1f, Y = 2f, Z = 3f, Speed = 3f, DistToDestination = 20f };

    private int Count(string needle) => _lines.Count(l => l.Contains(needle));

    // ----- the clock and the gate

    [DataTestMethod]
    [DataRow(float.NaN)]
    [DataRow(float.PositiveInfinity)]
    [DataRow(float.NegativeInfinity)]
    [DataRow(0f)]
    [DataRow(-1f)]
    public void Tick_AnInvalidDelta_IsIgnored_BeforeTheGateOrTheAdapter(float dt)
    {
        _adapter.Agents.Add(Wading());

        _sut.Tick(dt);
        _sut.Tick(dt);

        Assert.AreEqual(0, _gateCalls);
        Assert.AreEqual(0, _adapter.SceneReads);
        Assert.AreEqual(0, _adapter.Slices.Count);
    }

    [TestMethod]
    public void Tick_ReadsTheGateOncePerSecond_NotOncePerTick()
    {
        for (var i = 0; i < 8; i++)
            _sut.Tick(0.25f);

        Assert.AreEqual(2, _gateCalls, "the first tick and one second later");
    }

    [TestMethod]
    public void Tick_AClosedGate_ScansNothing_AndWritesNoStartLine()
    {
        _open = false;
        _adapter.Agents.Add(Wading());

        Seconds(5);

        Assert.AreEqual(0, _adapter.Slices.Count);
        Assert.AreEqual(0, _adapter.SceneReads);
        Assert.AreEqual(0, _lines.Count);
    }

    [TestMethod]
    public void Tick_TheGateClosingMidBattle_StopsTheScan()
    {
        _adapter.Agents.Add(Wading());
        Seconds(3);
        var before = _adapter.Slices.Count;
        _open = false;

        Seconds(5);

        Assert.IsTrue(before > 0);
        Assert.AreEqual(before, _adapter.Slices.Count, "the next gate read is closed, so the pass that follows it never runs");
    }

    [TestMethod]
    public void Tick_TheCensusSwitchedOff_ScansNothing_AndWritesNoStartLine()
    {
        _settings.CensusEnabled.Returns(false);
        _adapter.Agents.Add(Wading());

        Seconds(5);

        Assert.AreEqual(0, _adapter.Slices.Count);
        Assert.AreEqual(0, _lines.Count);
    }

    // ----- the start line

    [TestMethod]
    public void Start_IsWrittenOncePerBattle_WithSceneTypeWaterAndAgents()
    {
        _adapter.Agents.AddRange(new[] { new AgentProbe(), new AgentProbe(), new AgentProbe() });

        Seconds(6);

        Assert.AreEqual(1, Count("start scene="));
        Assert.AreEqual("[LostFooting] start scene=test_scene type=field water=5.00 agents=3", _lines[0]);
        Assert.AreEqual(1, _adapter.SceneReads, "the scene is read once per mission");
    }

    // ----- the pass cursor

    [TestMethod]
    public void Pass_ReadsAtMostAgentsPerTick_AndWalksTheWholeList()
    {
        for (var i = 0; i < 150; i++)
            _adapter.Agents.Add(new AgentProbe());

        Seconds(4);

        CollectionAssert.AreEqual(
            new[] { (0, 64), (64, 64), (128, 22), (0, 64) },
            _adapter.Slices);
    }

    [TestMethod]
    public void Pass_AShrinkingList_NeverReadsPastTheEnd()
    {
        for (var i = 0; i < 150; i++)
            _adapter.Agents.Add(new AgentProbe());
        Seconds(2);

        _adapter.Agents.RemoveRange(10, 140);
        Seconds(2);

        Assert.AreEqual((0, 10), _adapter.Slices[2], "the cursor was past the new end, so the sweep restarts");
    }

    [TestMethod]
    public void Pass_ASmallerAgentsPerTick_IsHonoured()
    {
        _sut = New(new LostFootingTunables { AgentsPerTick = 8 });
        for (var i = 0; i < 20; i++)
            _adapter.Agents.Add(new AgentProbe());

        Seconds(3);

        CollectionAssert.AreEqual(new[] { (0, 8), (8, 8), (16, 4) }, _adapter.Slices);
    }

    [TestMethod]
    public void Pass_ALongerSweep_ReadsFewerAgentsEachTick()
    {
        _sut = New(new LostFootingTunables { SweepSeconds = 4f });
        for (var i = 0; i < 8; i++)
            _adapter.Agents.Add(new AgentProbe());

        Seconds(2);

        CollectionAssert.AreEqual(new[] { (0, 2), (2, 2) }, _adapter.Slices);
    }

    [TestMethod]
    public void Pass_PerFrameTicks_ReadEveryAgentOncePerSweep_AndWrapOnce()
    {
        for (var i = 0; i < 640; i++)
            _adapter.Agents.Add(new AgentProbe());

        for (var i = 0; i < 64; i++)
            _sut.Tick(1f / 64f);

        Assert.AreEqual(64, _adapter.Slices.Count);
        Assert.AreEqual(640, _adapter.Slices.Sum(x => x.Take), "every agent read once");
        Assert.IsTrue(_adapter.Slices.All(x => x.Take == 10));
        Assert.AreEqual(630, _adapter.Slices[63].Start);

        _sut.Tick(1f / 64f);

        Assert.AreEqual(0, _adapter.Slices[64].Start, "the next sweep starts over");
    }

    [TestMethod]
    public void Pass_AHugeDelta_NeverReadsMoreThanAgentsPerTick()
    {
        for (var i = 0; i < 640; i++)
            _adapter.Agents.Add(new AgentProbe());

        _sut.Tick(30f);

        Assert.AreEqual((0, 64), _adapter.Slices[0]);
    }

    [TestMethod]
    public void Pass_AStrandedAgentThatLeavesTheList_LeavesTheCensusWithinAboutThreeSeconds()
    {
        _sut = New(new LostFootingTunables { CensusIntervalSeconds = 2f });
        _adapter.Agents.Add(Wading());
        for (var i = 0; i < 639; i++)
            _adapter.Agents.Add(new AgentProbe());
        for (var i = 0; i < 64 * 6; i++)
            _sut.Tick(1f / 64f);
        Assert.IsTrue(Count("census t=") > 0, "stranded while it is on the list");

        _adapter.Agents.RemoveAt(0);
        for (var i = 0; i < 64 * 4; i++)
            _sut.Tick(1f / 64f);
        var after = Count("census t=");
        for (var i = 0; i < 64 * 4; i++)
            _sut.Tick(1f / 64f);

        Assert.AreEqual(after, Count("census t="), "no census names a removed agent after about three seconds");
    }

    [TestMethod]
    public void Cost_IsTimedAroundTheSliceRead_AndReportedInTheSummary()
    {
        _adapter.Agents.Add(new AgentProbe());
        Seconds(2);

        _sut.Finish();

        StringAssert.Contains(_lines.Single(l => l.Contains("summary")), "cost probes=2 totalMs=8.00 maxTickMs=4.00");
    }

    // ----- the census

    [TestMethod]
    public void Census_NothingStranded_WritesNoCensusLine()
    {
        _adapter.Agents.AddRange(new[] { new AgentProbe(), new AgentProbe() });

        Seconds(60);

        Assert.AreEqual(0, Count("census"));
        Assert.AreEqual(0, Count("sample"));
    }

    [TestMethod]
    public void Census_AStrandedAgent_WritesALinePerClassEveryCensusInterval()
    {
        _adapter.Agents.Add(Wading());

        Seconds(25);

        Assert.AreEqual(2, Count("census t="), "census at 11 s and 21 s");
        Assert.AreEqual(0, Count("class=offmesh"), "a class with nobody in it is not written");
        Assert.AreEqual(2, Count("sample class=waterstill"));
    }

    [TestMethod]
    public void Census_WritesNothingOnceTheAgentIsFree()
    {
        var p = Wading();
        _adapter.Agents.Add(p);
        Seconds(15);
        var written = Count("census");
        p.Speed = 3f;

        Seconds(30);

        Assert.AreEqual(written, Count("census"));
    }

    [TestMethod]
    public void Census_SamplesAreCappedAtSamplesPerClass()
    {
        _sut = New(new LostFootingTunables { SamplesPerClass = 2 });
        for (var i = 0; i < 4; i++)
            _adapter.Agents.Add(Wading());

        Seconds(12);

        Assert.AreEqual(1, Count("class=waterstill stranded=4"));
        Assert.AreEqual(2, Count("sample class=waterstill"));
    }

    [TestMethod]
    public void Census_APlayerControlledAgent_IsNeverCounted()
    {
        var p = Wading();
        p.IsPlayerControlled = true;
        _adapter.Agents.Add(p);

        Seconds(30);

        Assert.AreEqual(0, Count("census"));
    }

    // ----- off mesh confirmation

    [TestMethod]
    public void OffMesh_AnAdmittedAgent_IsConfirmedOnce_AndTheSampleCarriesTheSnap()
    {
        var p = Lost();
        _adapter.Agents.Add(p);

        Seconds(25);

        Assert.AreEqual(1, _adapter.Confirmed.Count, "one confirmation per episode, not one per pass");
        Assert.AreSame(p.Handle, _adapter.Confirmed[0]);
        Assert.IsTrue(_lines.Any(l => l.Contains("class=offmesh stranded=1 confirmed=1")), string.Join("\n", _lines));
        Assert.IsTrue(_lines.Any(l => l.Contains("sample class=offmesh") && l.EndsWith("confirmed=yes dynamic=no snap=2.0 snapOnMesh=yes")));
    }

    [TestMethod]
    public void OffMesh_ASuspectSeenOnce_IsNotConfirmed()
    {
        var p = Lost();
        _adapter.Agents.Add(p);

        _sut.Tick(1f);
        p.FaceMiss = false;
        Seconds(5);

        Assert.AreEqual(0, _adapter.Confirmed.Count);
        Assert.AreEqual(0, Count("census"));
    }

    [TestMethod]
    public void OffMesh_ALostThenFoundThenLostAgent_IsConfirmedAgainInTheNextEpisode()
    {
        var p = Lost();
        _adapter.Agents.Add(p);
        Seconds(4);
        p.FaceMiss = false;
        Seconds(2);
        p.FaceMiss = true;

        Seconds(5);

        Assert.AreEqual(2, _adapter.Confirmed.Count);
    }

    // ----- the summary

    [TestMethod]
    public void Finish_BeforeAnyPass_WritesNothing()
    {
        _sut.Finish();

        Assert.AreEqual(0, _lines.Count);
    }

    [TestMethod]
    public void Finish_WritesOneSummary_WithEpisodesPeaksAndHotspots_AndOnlyOnce()
    {
        var p = Wading();
        _adapter.Agents.Add(p);
        Seconds(10);
        p.Speed = 3f;
        Seconds(2);

        _sut.Finish();
        _sut.Finish();

        Assert.AreEqual(1, Count("summary scene="));
        var summary = _lines.Single(l => l.Contains("summary scene="));
        StringAssert.Contains(summary, "summary scene=test_scene");
        StringAssert.Contains(summary, "episodes=1 recovered=1 removed=0 atEnd=0");
        StringAssert.Contains(summary, "waterstill=1");
        Assert.AreEqual(1, Count("hotspot 1 x=10..15 y=10..15"), string.Join("\n", _lines));
    }

    [TestMethod]
    public void Finish_AnUntroubledBattle_StillWritesItsSummary()
    {
        _adapter.Agents.Add(new AgentProbe());
        Seconds(5);

        _sut.Finish();

        StringAssert.Contains(_lines.Single(l => l.Contains("summary")), "episodes=0 recovered=0 removed=0 atEnd=0 longest=0.0s");
        Assert.AreEqual(0, Count("hotspot"));
    }

    [TestMethod]
    public void Finish_AnEpisodeStillOpenAtTheEnd_IsCounted()
    {
        _adapter.Agents.Add(Wading());
        Seconds(8);

        _sut.Finish();

        StringAssert.Contains(_lines.Single(l => l.Contains("summary")), "episodes=1 recovered=0 removed=0 atEnd=1");
    }

    [TestMethod]
    public void Finish_AfterTheAdapterThrew_StillWritesTheSummaryOfWhatWasSeen()
    {
        _adapter.Agents.Add(Wading());
        Seconds(5);
        _adapter.ThrowOnRead = new InvalidOperationException("boom");

        Assert.ThrowsException<InvalidOperationException>(() => Seconds(2));
        _sut.Finish();

        Assert.AreEqual(1, Count("summary"));
    }

    [TestMethod]
    public void Pruning_AnAgentThatLeftTheList_StopsBeingCounted()
    {
        _adapter.Agents.Add(Wading());
        Seconds(12);
        Assert.AreEqual(1, Count("census t="));

        _adapter.Agents.Clear();
        Seconds(40);

        Assert.AreEqual(1, Count("census t="), "pruned after a few empty sweeps, so no later census names it");
    }

    // ----- a pause is not bridged

    [TestMethod]
    public void Finish_TheCensusNeverStarted_WritesNoSummary()
    {
        _settings.CensusEnabled.Returns(false);
        _adapter.Agents.Add(Wading());
        Seconds(10);

        _sut.Finish();

        Assert.AreEqual(0, _lines.Count);
    }

    [TestMethod]
    public void Resume_AfterTheCensusWasSwitchedOff_DoesNotBridgeThePausedTime()
    {
        _adapter.Agents.Add(Wading());
        Seconds(6);
        _settings.CensusEnabled.Returns(false);
        Seconds(30);
        _settings.CensusEnabled.Returns(true);
        Seconds(3);

        _sut.Finish();

        var summary = _lines.Single(l => l.Contains("summary"));
        StringAssert.Contains(summary, "episodes=2 recovered=0 removed=0 atEnd=2");
        Assert.AreEqual(1, Count("hotspot 1 x=10..15 y=10..15 agentSeconds=5.0"), string.Join(" | ", _lines));
    }

    [TestMethod]
    public void Resume_AfterTheGateReopens_AlsoStartsFresh()
    {
        _adapter.Agents.Add(Wading());
        Seconds(6);
        _open = false;
        Seconds(10);
        _open = true;
        Seconds(3);

        _sut.Finish();

        StringAssert.Contains(_lines.Single(l => l.Contains("summary")), "episodes=2");
    }
}
