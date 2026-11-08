using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Core.Logging;
using TAOM.Features.FordsOfWar;
using TAOM.Features.FordsOfWar.Diagnostics;
using TAOM.Features.FordsOfWar.Domain;
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

    private FordsTelemetryMeter Meter() => (FordsTelemetryMeter)Field(typeof(FordsTelemetryLogic), "_meter").GetValue(_logic)!;

    private WargRageLog? Warg() => (WargRageLog?)Field(typeof(FordsTelemetryMeter), "_warg").GetValue(Meter());

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
        Field(typeof(FordsTelemetryMeter), "_warg").SetValue(Meter(), warg);
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
        StringAssert.Contains(check12, "started=FAIL(started=1", "no warg rages in a Fords match, so a start FAILs");
    }

    [TestMethod]
    public void OnEndMission_WithoutAWargLog_StillClosesTheRun()
    {
        typeof(FordsTelemetryLogic).GetMethod("OnEndMission", BindingFlags.NonPublic | BindingFlags.Instance)!.Invoke(_logic, null);

        Assert.IsTrue(_report.Run!.Closed);
        Assert.IsFalse(_lines.Any(l => l.StartsWith("ERROR")), string.Join("\n", _lines.Where(l => l.StartsWith("ERROR"))));
    }

    [TestMethod]
    public void WatchExceptions_AnExceptionAFinalizerSwallows_IsCountedOnTheRunWithItsTypeAndSite_UntilTheWatchStops()
    {
        var meter = new FordsTelemetryMeter(_report);
        TAOM.Tests.Features.CrashReport.RecordingCrashService.Install();
        try
        {
            meter.WatchExceptions();
            Assert.IsTrue(_report.Run!.Exceptions.Watching);

            TAOM.Features.CrashReport.Hooks.CrashReportPatchHelper.HandleAndSwallow(new InvalidOperationException("boom"), "taom-006.test");
            meter.StopWatchingExceptions();
            TAOM.Features.CrashReport.Hooks.CrashReportPatchHelper.HandleAndSwallow(new InvalidOperationException("later"), "taom-006.later");

            Assert.AreEqual(1, _report.Run.Exceptions.Count);
            Assert.AreEqual("InvalidOperationException", _report.Run.Exceptions.FirstType);
            Assert.AreEqual("taom-006.test", _report.Run.Exceptions.FirstSite);
        }
        finally
        {
            meter.StopWatchingExceptions();
            TAOM.Features.CrashReport.Hooks.CrashReportPatchHelper.ResetForUnload();
        }
    }

    [TestMethod]
    public void WatchExceptions_ArmedTwiceOnOneMeter_CountsASwallowedExceptionOnce()
    {
        var meter = new FordsTelemetryMeter(_report);
        TAOM.Tests.Features.CrashReport.RecordingCrashService.Install();
        try
        {
            meter.WatchExceptions();
            meter.WatchExceptions();

            TAOM.Features.CrashReport.Hooks.CrashReportPatchHelper.HandleAndSwallow(new InvalidOperationException("boom"), "taom-006.test");

            Assert.AreEqual(1, _report.Run!.Exceptions.Count, "a reloaded mission reruns AfterStart; one swallow is still one");
        }
        finally
        {
            meter.StopWatchingExceptions();
            TAOM.Features.CrashReport.Hooks.CrashReportPatchHelper.ResetForUnload();
        }
    }

    [TestMethod]
    public void WatchExceptions_AnExceptionHandedBack_IsNotCounted()
    {
        var meter = new FordsTelemetryMeter(_report);
        TAOM.Features.CrashReport.Hooks.CrashReportPatchHelper.ResetForUnload();
        try
        {
            meter.WatchExceptions();

            TAOM.Features.CrashReport.Hooks.CrashReportPatchHelper.HandleAndSwallow(new InvalidOperationException("boom"), "taom-006.test");

            Assert.AreEqual(0, _report.Run!.Exceptions.Count, "no capture service is installed, so the finalizer hands the exception back to the engine");
        }
        finally
        {
            meter.StopWatchingExceptions();
        }
    }

    private static TaleWorlds.MountAndBlade.Agent FakeAgent() =>
        (TaleWorlds.MountAndBlade.Agent)System.Runtime.Serialization.FormatterServices.GetUninitializedObject(typeof(TaleWorlds.MountAndBlade.Agent));

    private void WithTowers()
    {
        _report.MatchStarted(new FordsMatchFacts(60f, 8f, 12f, 2, 150, 200), new List<TowerSlot>
        {
            new("tower_east_0_0", MatchSide.East, 0, 0, TowerKind.Tower, new Vec3f(600, 0, 0), 0f),
        }, 1);
    }

    /// <summary>The towers are East's, so every attacker that is not the gunner is a West unit.</summary>
    private void BindEnemy(TaleWorlds.MountAndBlade.Agent gunner)
    {
        _report.BindGunnerLookup(a => ReferenceEquals(a, gunner) ? "tower_east_0_0" : null);
        _report.BindAttackerLookup(a => ReferenceEquals(a, gunner) ? null : new FordsAttacker(MatchSide.West, 0));
    }

    [TestMethod]
    public void OnMeleeHit_AVictimThatIsATowerGunner_CountsAMeleeBlowOnItsTower_EvenWhenTheBlowWasCancelled()
    {
        WithTowers();
        var gunner = FakeAgent();
        BindEnemy(gunner);

        _logic.OnMeleeHit(FakeAgent(), gunner, true, default);
        _logic.OnMeleeHit(FakeAgent(), FakeAgent(), false, default);

        Assert.AreEqual(1, _report.Run!.Towers["tower_east_0_0"].GunnerBlowsMelee, "an invulnerable gunner's blow arrives cancelled");
        Assert.AreEqual(0, _report.Run.Towers["tower_east_0_0"].GunnerBlowsRanged);
    }

    [TestMethod]
    public void OnMissileHit_AVictimThatIsATowerGunner_CountsARangedBlowOnItsTower()
    {
        WithTowers();
        var gunner = FakeAgent();
        BindEnemy(gunner);

        _logic.OnMissileHit(FakeAgent(), gunner, false, default);

        Assert.AreEqual(1, _report.Run!.Towers["tower_east_0_0"].GunnerBlowsRanged);
        Assert.AreEqual(0, _report.Run.Towers["tower_east_0_0"].GunnerBlowsMelee);
    }

    [TestMethod]
    public void OnMeleeHit_AndOnMissileHit_WithNoVictim_CountNothingAndDoNotThrow()
    {
        WithTowers();
        _report.BindGunnerLookup(_ => "tower_east_0_0");
        _report.BindAttackerLookup(_ => new FordsAttacker(MatchSide.West, 0));

        _logic.OnMeleeHit(FakeAgent(), null!, false, default);
        _logic.OnMissileHit(null!, null!, false, default);

        Assert.AreEqual(0, _report.Run!.Towers["tower_east_0_0"].GunnerBlowsMelee);
        Assert.AreEqual(0, _report.Run.Towers["tower_east_0_0"].GunnerBlowsRanged);
    }
}
