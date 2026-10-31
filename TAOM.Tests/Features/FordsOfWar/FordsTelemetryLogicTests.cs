using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Core.Logging;
using TAOM.Features.FordsOfWar;
using TAOM.Features.FordsOfWar.Diagnostics;
using TAOM.Features.FordsOfWar.Hooks;
using TAOM.Features.Warg;
using TAOM.Tests.Migration;

namespace TAOM.Tests.Features.FordsOfWar;

/// <summary>
/// The telemetry logic driven with no Mission, the way <see cref="FordsOfWarMissionLogicTests"/> drives its logic. Not driven here
/// (they need a live mission): <c>AfterStart</c> (the options component and the warg log's activation), the 30 second window that
/// reads the mission's agent counts, and <c>OnAgentHit</c> (engine structs). The warg log is put in place by reflection, with the
/// same seam the warg tests use.
/// </summary>
[TestClass]
[TestCategory("RequiresGame")]
public class FordsTelemetryLogicTests
{
    private static readonly FordsMatchSetup Setup = new("scene_a", "gondor", "mordor", "hard", 2);

    private readonly List<string> _lines = new();
    private FordsRunReport _report = null!;
    private FordsTelemetryLogic _logic = null!;

    [ClassInitialize]
    public static void Init(Microsoft.VisualStudio.TestTools.UnitTesting.TestContext _) => GameAssemblies.EnsureLoaded();

    [TestInitialize]
    public void Create()
    {
        var logger = Substitute.For<IModLogger>();
        logger.When(l => l.LogInfo(Arg.Any<string>())).Do(c => _lines.Add(c.Arg<string>()));
        logger.When(l => l.LogError(Arg.Any<string>())).Do(c => _lines.Add("ERROR " + c.Arg<string>()));
        _report = new FordsRunReport(logger);
        _report.BeginRun("console", Setup);
        _logic = new FordsTelemetryLogic(_report);
    }

    [TestCleanup]
    public void Clear() => WargRageLog.Deactivate(Warg());

    private WargRageLog? Warg() => (WargRageLog?)Field(typeof(FordsTelemetryLogic), "_warg").GetValue(_logic);

    private static FieldInfo Field(Type type, string name) =>
        type.GetField(name, BindingFlags.NonPublic | BindingFlags.Instance)!;

    private int FramesRecorded()
    {
        var stats = Field(typeof(FordsRunReport), "_frames").GetValue(_report)!;
        return (int)Field(stats.GetType(), "_frames").GetValue(stats)!;
    }

    [TestMethod]
    public void MissionTick_AdvancesTheRunsMissionClockByEachDt()
    {
        _logic.OnMissionTick(1f);
        _logic.OnMissionTick(1f);

        Assert.AreEqual(2.0, _report.MissionSeconds, 1e-6);
        Assert.AreEqual(2.0, _report.Run!.Mt, 1e-6);
    }

    [TestMethod]
    public void MissionTick_TheFirstTickHasNoTimerYet_SoOnlyTheSecondIsAFrame()
    {
        _logic.OnMissionTick(0.05f);
        Assert.AreEqual(0, FramesRecorded(), "frame time is -1 on the first tick");

        _logic.OnMissionTick(0.05f);
        Assert.AreEqual(1, FramesRecorded());
    }

    [TestMethod]
    public void MissionTick_APausedTick_IsNotAFrame()
    {
        _logic.OnMissionTick(0.05f);
        _logic.OnMissionTick(0f);

        Assert.AreEqual(0, FramesRecorded());
    }

    private (object Rider, Func<int> Watches) ArmWarg(double distance)
    {
        var watches = 0;
        var view = new WargRageLog.RiderView(true, false, true, true, (float)distance, 10f, 0f);
        var warg = new WargRageLog(Substitute.For<IModLogger>(), () => _report.MissionSeconds, (_, target) =>
        {
            if (target != null)
                watches++;
            return view;
        });
        Field(typeof(FordsTelemetryLogic), "_warg").SetValue(_logic, warg);
        var rider = new object();
        warg.Started(rider, true);
        warg.Ended(rider, new object());
        return (rider, () => watches);
    }

    [TestMethod]
    public void MissionTick_WatchesTheWargsOncePerMissionSecond()
    {
        var (_, watches) = ArmWarg(distance: 100.0);

        for (var i = 0; i < 8; i++)
            _logic.OnMissionTick(0.25f);

        Assert.AreEqual(2, watches(), "at 0.25 s and again at 1.25 s of the 2 s driven");
    }

    [TestMethod]
    public void MissionTick_PushesTheWargCountsIntoTheRun()
    {
        ArmWarg(distance: 0.0);

        _logic.OnMissionTick(0.25f);

        Assert.AreEqual(1, _report.Run!.Warg.Started);
        Assert.AreEqual(1, _report.Run.Warg.Returned);
    }

    [TestMethod]
    public void OnEndMission_PushesTheWargCountsBeforeTheClosingVerdictPass()
    {
        ArmWarg(distance: 100.0);

        typeof(FordsTelemetryLogic).GetMethod("OnEndMission", BindingFlags.NonPublic | BindingFlags.Instance)!.Invoke(_logic, null);

        Assert.IsTrue(_report.Run.Closed);
        Assert.AreEqual(1, _report.Run.Warg.Started);
        var check12 = _lines.Last(l => l.Contains("final=0 check=12"));
        StringAssert.Contains(check12, "started=PASS(started=1");
    }

    [TestMethod]
    public void OnEndMission_WithoutAWargLog_StillClosesTheRun()
    {
        typeof(FordsTelemetryLogic).GetMethod("OnEndMission", BindingFlags.NonPublic | BindingFlags.Instance)!.Invoke(_logic, null);

        Assert.IsTrue(_report.Run!.Closed);
        Assert.IsFalse(_lines.Any(l => l.StartsWith("ERROR")), string.Join("\n", _lines.Where(l => l.StartsWith("ERROR"))));
    }
}
