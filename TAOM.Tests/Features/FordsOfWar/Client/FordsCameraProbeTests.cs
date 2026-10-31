using System.Collections.Generic;
using System.Linq;
using NSubstitute;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Client.Features.FordsOfWar;
using TAOM.Core.Logging;
using TAOM.Features.FordsOfWar;
using TAOM.Features.FordsOfWar.Diagnostics;
using TAOM.Features.FordsOfWar.Domain;

namespace TAOM.Tests.Features.FordsOfWar.Client;

/// <summary>The camera probe's flag arithmetic: what the player asked for against what the camera did.</summary>
[TestClass]
public class FordsCameraProbeTests
{
    private static FordsCameraState State(float x = 0f, float y = 0f, float yaw = 0f, float height = 100f, float tilt = 0f) =>
        new FordsCameraState(x, y, yaw, height, tilt);

    private static FordsCameraInput Input(float pan = 0f, float right = 0f, float rotate = 0f, float zoom = 0f, float dragX = 0f, float dragY = 0f) =>
        new FordsCameraInput(pan, right, rotate, zoom, dragX, dragY, false);

    [TestMethod]
    public void Pose_LogsAtTheFirstFrame_ThenEveryFiveSeconds_SevenLinesInAll()
    {
        var logger = Substitute.For<IModLogger>();
        var report = new FordsRunReport(logger);
        report.BeginRun("menu", new FordsMatchSetup("scene_a", "gondor", "mordor", "hard", 2));
        var probe = new FordsCameraProbe(report, (_, _) => true, (_, _) => (10f, 12f));

        for (var i = 0; i < 400; i++)
            probe.Pose(State(x: 5f, y: 6f, height: 45f), 10f, 0.1f);

        var poses = logger.ReceivedCalls().Where(c => c.GetMethodInfo().Name == nameof(IModLogger.LogInfo))
            .Select(c => (string)c.GetArguments()[0]!).Where(m => m.Contains("[Camera]") && m.Contains(" pose ")).ToList();
        Assert.AreEqual(7, poses.Count, "at 0, 5, 10, 15, 20, 25 and 30 s");
        StringAssert.Contains(poses[0], "z=55.0");
        StringAssert.Contains(poses[0], "terrain=10.0");
        StringAssert.Contains(poses[0], "ground=12.0");
    }

    [TestMethod]
    public void Pose_ANaNStep_DoesNotLogTheRemainingLinesOnConsecutiveFrames()
    {
        var logger = Substitute.For<IModLogger>();
        var report = new FordsRunReport(logger);
        report.BeginRun("menu", new FordsMatchSetup("scene_a", "gondor", "mordor", "hard", 2));
        var probe = new FordsCameraProbe(report, (_, _) => true, (_, _) => (10f, 12f));

        probe.Pose(State(), 10f, 0.1f);
        for (var i = 0; i < 20; i++)
            probe.Pose(State(), 10f, float.NaN);

        var poses = logger.ReceivedCalls().Where(c => c.GetMethodInfo().Name == nameof(IModLogger.LogInfo))
            .Select(c => (string)c.GetArguments()[0]!).Where(m => m.Contains("[Camera]") && m.Contains(" pose ")).ToList();
        Assert.AreEqual(1, poses.Count, "a NaN clock never passes the gate");
    }

    [TestMethod]
    public void Flags_PanWithMovement()
    {
        var flags = FordsCameraProbe.Flags(State(), Input(pan: 1f), State(y: 5f));

        Assert.IsTrue(flags.Pan);
        Assert.IsTrue(flags.Moved);
        Assert.IsFalse(flags.Rotate);
        Assert.IsFalse(flags.Turned);
        Assert.IsFalse(flags.Zoom);
        Assert.IsFalse(flags.Zoomed);
    }

    [TestMethod]
    public void Flags_DragTurns()
    {
        var flags = FordsCameraProbe.Flags(State(), Input(dragX: 12f), State(yaw: 0.05f));

        Assert.IsTrue(flags.Rotate);
        Assert.IsTrue(flags.Turned);
        Assert.IsFalse(flags.Pan);
    }

    [TestMethod]
    public void Flags_ZoomAtTheHeightLimit_IsNotZoomed()
    {
        var flags = FordsCameraProbe.Flags(State(height: 200f), Input(zoom: 1f), State(height: 200f));

        Assert.IsTrue(flags.Zoom);
        Assert.IsFalse(flags.Zoomed);
    }

    [TestMethod]
    public void Flags_NoInput_AllFalse()
    {
        var flags = FordsCameraProbe.Flags(State(), Input(), State());

        Assert.IsFalse(flags.Pan || flags.Rotate || flags.Zoom || flags.Moved || flags.Turned || flags.Zoomed);
    }

    private static readonly List<Vec3f> Square = new()
    {
        new Vec3f(0f, 0f, 0f), new Vec3f(100f, 0f, 0f), new Vec3f(100f, 100f, 0f), new Vec3f(0f, 100f, 0f),
    };

    private static bool Inside(float x, float y, bool engineInside) =>
        new FordsCameraProbe(null, (_, _) => engineInside).IsInside(Square, x, y);

    [TestMethod]
    public void IsInside_APointOnAnEdgeOrAVertex_CountsAsInsideEvenWhenTheEngineSaysOutside()
    {
        Assert.IsTrue(Inside(100f, 50f, false), "on the east edge");
        Assert.IsTrue(Inside(0f, 0f, false), "on a vertex");
        Assert.IsTrue(Inside(100.4f, 50f, false), "inside the half metre");
    }

    [TestMethod]
    public void IsInside_APointAMetreOutside_CountsAsOutside_AndAnEngineInsideAlwaysCounts()
    {
        Assert.IsFalse(Inside(101f, 50f, false));
        Assert.IsTrue(Inside(50f, 50f, true));
    }

    private static FordsRunReport Report()
    {
        var report = new FordsRunReport(Substitute.For<IModLogger>());
        report.BeginRun("menu", new FordsMatchSetup("scene_a", "gondor", "mordor", "hard", 2));
        return report;
    }

    [TestMethod]
    public void Frame_CountsPanRotateAndZoomInputAgainstWhatTheCameraDid()
    {
        var report = Report();
        var probe = new FordsCameraProbe(report, (_, _) => true);

        probe.Frame(State(), Input(pan: 1f, zoom: 1f), State(y: 5f, height: 100f), 0.1f, Square);
        probe.Frame(State(), Input(rotate: 1f), State(), 0.1f, Square);

        var c = report.Run!.Client;
        Assert.AreEqual(2, c.OwnedFrames);
        Assert.AreEqual(1, c.PanInputFrames);
        Assert.AreEqual(1, c.PanMovedFrames);
        Assert.AreEqual(1, c.ZoomInputFrames);
        Assert.AreEqual(0, c.ZoomedFrames);
        Assert.AreEqual(1, c.RotateInputFrames);
        Assert.AreEqual(0, c.TurnedFrames);
    }

    [TestMethod]
    public void Frame_SamplesInsideOncePerSecond_AndAcceptsTheEdgeWithinHalfAMetre()
    {
        var report = Report();
        var probe = new FordsCameraProbe(report, (_, _) => false);

        probe.Frame(State(), Input(), State(x: 50f, y: 50f), 0.5f, Square);
        Assert.AreEqual(0, report.Run!.Client.InsideSamples + report.Run.Client.OutsideSamples, "under a second: no sample yet");

        probe.Frame(State(), Input(), State(x: 100.4f, y: 50f), 0.5f, Square);
        probe.Frame(State(), Input(), State(x: 101f, y: 50f), 1f, Square);

        var c = report.Run.Client;
        Assert.AreEqual(1, c.InsideSamples, "0.4 m past the edge counts as inside");
        Assert.AreEqual(1, c.OutsideSamples, "a metre past does not");
        Assert.AreEqual(1, c.MaxOutsideRun);
    }

    [TestMethod]
    public void PhotoVetoed_CountsTheVeto_AndAnswersFalse()
    {
        var report = Report();
        var probe = new FordsCameraProbe(report, (_, _) => true);

        Assert.IsFalse(probe.PhotoVetoed());
        Assert.IsFalse(probe.PhotoVetoed());

        Assert.AreEqual(2, report.Run!.Client.PhotoVetoes);
    }

    [TestMethod]
    public void Failed_LatchesTheCameraFailure()
    {
        var report = Report();

        new FordsCameraProbe(report, (_, _) => true).Failed();

        Assert.IsTrue(report.Run!.Client.CameraFailed);
    }

    [TestMethod]
    public void WithNoReport_EveryMemberIsANoOp()
    {
        var probe = new FordsCameraProbe(null, (_, _) => true);

        probe.Frame(State(), Input(pan: 1f), State(y: 5f), 2f, Square);
        probe.Failed();
        Assert.IsFalse(probe.PhotoVetoed());
    }
}
