using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.MapLoadDiagnostics;

namespace TAOM.Tests.Features.MapLoadDiagnostics;

/// <summary>
/// The campaign map never finishes loading on v1.5.0 though it did on v1.4.8, and every offline
/// gate is green. Dump sampling established that the map screen is live and Campaign.RealTick is
/// executing, and that both engine hot paths are byte-identical to v1.4.8, so the question is not
/// "where is it stuck" but "what fails to converge". This heartbeat answers that from a log.
///
/// The service is pure, so the emit decision and the formatting are testable without a game; the
/// patch supplies the engine readings.
/// </summary>
[TestClass]
public class MapLoadHeartbeatServiceTests
{
    private static readonly DateTime T0 = new DateTime(2026, 8, 20, 9, 0, 0, DateTimeKind.Utc);

    private static MapLoadSample Sample(int parties = 2113, int lord = 300, int villager = 988,
                                        int caravan = 120, int bandit = 500, int militia = 100,
                                        int garrison = 100, int other = 5, int heroes = 900,
                                        int clans = 120, double campaignTime = 1.0,
                                        bool loading = true, double tickMs = 12.5,
                                        string activeState = "MapState",
                                        string stack = "MapState",
                                        string topScreen = "MapScreen",
                                        string timeControl = "Stop")
        => new MapLoadSample(parties, lord, villager, caravan, bandit, militia, garrison, other,
                             988, heroes, clans, campaignTime, loading, tickMs,
                             activeState, stack, topScreen, timeControl);

    private static string FirstLine(MapLoadHeartbeatService sut, MapLoadSample s, DateTime t)
    {
        sut.ShouldEmit(t, 10d);
        return sut.BuildLine(t, s);
    }

    [TestMethod]
    public void ShouldEmit_FirstTick_EmitsImmediately()
        => Assert.IsTrue(new MapLoadHeartbeatService().ShouldEmit(T0, 10d),
            "The first tick must emit a baseline, or later lines have nothing to compare against.");

    [TestMethod]
    public void ShouldEmit_BeforeInterval_Suppresses()
    {
        var sut = new MapLoadHeartbeatService();
        FirstLine(sut, Sample(), T0);
        Assert.IsFalse(sut.ShouldEmit(T0.AddSeconds(1), 10d),
            "A per-frame log would flood the file and distort the timing it measures.");
    }

    [TestMethod]
    public void ShouldEmit_AfterInterval_Emits()
    {
        var sut = new MapLoadHeartbeatService();
        FirstLine(sut, Sample(), T0);
        Assert.IsTrue(sut.ShouldEmit(T0.AddSeconds(5), 10d));
    }

    [TestMethod]
    public void Line_ReportsFps_FromFramesBetweenEmits()
    {
        var sut = new MapLoadHeartbeatService();
        FirstLine(sut, Sample(), T0);
        for (int i = 0; i < 9; i++) sut.ShouldEmit(T0.AddSeconds(1), 10d);
        sut.ShouldEmit(T0.AddSeconds(5), 10d);
        StringAssert.Contains(sut.BuildLine(T0.AddSeconds(5), Sample()), "fps=2.0",
            "10 frames over 5s is 2 fps; that number separates a slow load from a stopped one.");
    }

    [TestMethod]
    public void TickMsAverage_AveragesAcrossTheWindow_SoSimulationCostIsSeparableFromFrameCost()
    {
        var sut = new MapLoadHeartbeatService();
        FirstLine(sut, Sample(), T0);
        sut.ShouldEmit(T0.AddSeconds(1), 10d);
        sut.ShouldEmit(T0.AddSeconds(5), 30d);
        Assert.AreEqual(20d, sut.TickMsAverage, 0.001,
            "A small tickMs against a long frame puts the cost outside the simulation.");
    }

    [TestMethod]
    public void Line_ReportsPartyDelta_SoARunawaySpawnIsDistinguishableFromAFlatCount()
    {
        var sut = new MapLoadHeartbeatService();
        FirstLine(sut, Sample(parties: 2000), T0);
        sut.ShouldEmit(T0.AddSeconds(5), 10d);
        StringAssert.Contains(sut.BuildLine(T0.AddSeconds(5), Sample(parties: 2113)),
            "parties=2113(+113)",
            "A climbing count means something spawns without end; a flat one exonerates spawning.");
    }

    [TestMethod]
    public void Line_ReportsPerTypeCensus_SoAClimbingCountNamesItsOwnCulprit()
    {
        var sut = new MapLoadHeartbeatService();
        var line = FirstLine(sut, Sample(lord: 300, villager: 988, caravan: 120, bandit: 500,
                                         militia: 100, garrison: 100, other: 5), T0);
        StringAssert.Contains(line, "[lord=300 villager=988 caravan=120 bandit=500 militia=100 garrison=100 other=5]",
            "988 villagers against 988 settlements is expected; 500 bandits climbing is not.");
    }

    [TestMethod]
    public void Line_ReportsHeroDelta_TheUsualUpstreamCauseWhenLordPartiesClimb()
    {
        var sut = new MapLoadHeartbeatService();
        FirstLine(sut, Sample(heroes: 900), T0);
        sut.ShouldEmit(T0.AddSeconds(5), 10d);
        StringAssert.Contains(sut.BuildLine(T0.AddSeconds(5), Sample(heroes: 950)), "heroes=950(+50)");
    }

    [TestMethod]
    public void Line_ReportsCampaignTimeAdvance_SoAPausedSimulationIsVisible()
    {
        var sut = new MapLoadHeartbeatService();
        FirstLine(sut, Sample(campaignTime: 1.0), T0);
        sut.ShouldEmit(T0.AddSeconds(5), 10d);
        StringAssert.Contains(sut.BuildLine(T0.AddSeconds(5), Sample(campaignTime: 1.0)),
            "campaignTime=1.000(+0.000)",
            "Campaign time not advancing separates a paused simulation from a slow one.");
    }

    [TestMethod]
    public void Line_ReportsLoadingWindowState_TheSignalThatSplitsTheDiagnosis()
        => StringAssert.Contains(FirstLine(new MapLoadHeartbeatService(), Sample(loading: true), T0),
            "loadingWindow=True",
            "Up means the engine still thinks it is loading; down means the map is live and merely "
            + "invisible. Those are different bugs with different fixes.");

    [TestMethod]
    public void Line_ReportsTheWholeStateStack_NotJustTheActiveState()
    {
        // A state pushed above MapState holds the overlay while the map ticks underneath. Only the
        // full stack shows that; ActiveState alone would read "MapState" and look healthy.
        var line = FirstLine(new MapLoadHeartbeatService(),
            Sample(activeState: "MapState", stack: "MapState > VideoPlaybackState"), T0);
        StringAssert.Contains(line, "stack=[MapState > VideoPlaybackState]");
        StringAssert.Contains(line, "activeState=MapState");
    }

    [TestMethod]
    public void Line_ReportsTimeControlAndTopScreen()
    {
        var line = FirstLine(new MapLoadHeartbeatService(),
            Sample(timeControl: "Stop(locked)", topScreen: "MapScreen"), T0);
        StringAssert.Contains(line, "timeControl=Stop(locked)",
            "A locked time control would explain a frozen clock that is not the normal start pause.");
        StringAssert.Contains(line, "topScreen=MapScreen");
    }

    [TestMethod]
    public void Line_NeverDividesByZeroOnTheFirstEmit()
        => StringAssert.Contains(FirstLine(new MapLoadHeartbeatService(), Sample(), T0), "fps=0.0",
            "The first line has no window to average over and must not emit NaN or Infinity.");
}
