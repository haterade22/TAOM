using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.MapPerf;

namespace TAOM.Tests.Features.MapPerf;

/// <summary>
/// The pure arithmetic of the Patch101 map profiler, on synthetic ticks (1000 per second, so a tick is a
/// millisecond and a 5 s window is 5000 ticks). <c>typeof(string)</c> stands for a TAOM-owned entry and
/// <c>typeof(int)</c> for a vanilla one. A session's first <c>Boundary</c> only stamps the clock; a frame
/// closes at the next boundary when its START boundary saw no loading window, the map screen on top, and
/// exactly one TAOM application tick ran inside it.
/// </summary>
[TestClass]
public class MapFrameProfilerTests
{
    private const double D = 1e-9;

    private MapFrameProfiler _p = null!;
    private int _a;
    private int _b;

    [TestInitialize]
    public void Setup()
    {
        _p = new MapFrameProfiler(1000);
        _a = _p.Entries.SlotFor(typeof(string));
        _b = _p.Entries.SlotFor(typeof(int));
    }

    private MapWindow Take(long now) => _p.TakeWindow(now, 8, 0, 0, 0);

    // A closed frame of the given length at FF speed: app tick, then the closing boundary.
    private void CloseFrameAt(long now, MapSpeedClass next = MapSpeedClass.FF)
    {
        _p.AddAppTick(0);
        _p.Boundary(now, 0, MapSkip.None, next);
    }

    [TestMethod]
    public void Boundary_FirstOfSession_StampsOnlyAndCountsNoFrame()
    {
        _p.BeginSession(0, true, 0, 0, 0);
        _p.RecordEntry(_a, 5, 0, true);
        _p.AddPhase(MapPhase.MapState, 7);
        _p.AddAppTick(1);
        _p.Boundary(100, 0, MapSkip.None, MapSpeedClass.FF);

        var w = Take(100);

        Assert.AreEqual(0, w.Frames);
        Assert.AreEqual(0, w.Skipped);
        Assert.AreEqual(0, w.Top.Count);
    }

    [TestMethod]
    public void Boundary_TwoFrames_SumIntoTheWindow()
    {
        _p.BeginSession(0, true, 0, 0, 0);
        _p.Boundary(100, 0, MapSkip.None, MapSpeedClass.FF);
        _p.AddPhase(MapPhase.MapState, 20);
        _p.AddPhase(MapPhase.RealTick, 5);
        _p.AddPhase(MapPhase.CampaignTick, 8);
        _p.AddPhase(MapPhase.TickEvent, 3);
        _p.RecordEntry(_a, 2, 1024, taomOwned: true);
        _p.RecordEntry(_b, 1, 0, taomOwned: false);
        _p.AddPhase(MapPhase.MapScreen, 10);
        _p.AddAppTick(1);
        _p.Boundary(150, 4096, MapSkip.None, MapSpeedClass.FF);
        _p.AddPhase(MapPhase.MapState, 30);
        _p.AddPhase(MapPhase.MapScreen, 5);
        _p.AddAppTick(2);
        _p.Boundary(250, 8192, MapSkip.None, MapSpeedClass.Stop);

        var w = Take(250);

        Assert.AreEqual(2, w.Frames);
        Assert.AreEqual(150, w.WallMs, D);
        Assert.AreEqual(50, w.MapStateMs, D);
        Assert.AreEqual(5, w.RealTickMs, D);
        Assert.AreEqual(8, w.CampaignTickMs, D);
        Assert.AreEqual(3, w.TickEventMs, D);
        Assert.AreEqual(15, w.MapScreenMs, D);
        Assert.AreEqual(3, w.AppTickMs, D);
        Assert.AreEqual(82, w.OtherMs, D);
        Assert.AreEqual(5, w.TaomMs, D);
        Assert.AreEqual(100, w.MaxFrameMs, D);
        Assert.AreEqual(8192L, w.AllocBytes);
        Assert.AreEqual(MapSpeedClass.FF, w.Speed, "Both frames STARTED under FF; the Stop passed at 250 belongs to frame 3.");
        Assert.AreEqual(0, w.Skipped);
        Assert.AreEqual(2, w.Top.Count);
        Assert.AreEqual("String", w.Top[0].Name);
        Assert.AreEqual(2, w.Top[0].Ms, D);
        Assert.AreEqual(1, w.Top[0].Calls);
        Assert.AreEqual(2, w.Top[0].MaxMs, D);
        Assert.AreEqual(1024L, w.Top[0].AllocBytes);
        Assert.AreEqual("Int32", w.Top[1].Name);
        Assert.AreEqual(1, w.Top[1].Ms, D);
        Assert.AreEqual(1, w.Top[1].Calls);
        Assert.AreEqual(1, w.Top[1].MaxMs, D);
        Assert.AreEqual(0L, w.Top[1].AllocBytes);
        Assert.AreEqual(w.WallMs, w.MapStateMs + w.MapScreenMs + w.AppTickMs + w.OtherMs, D);
    }

    [TestMethod]
    public void Boundary_OtherMs_NeverNegative()
    {
        _p.BeginSession(0, true, 0, 0, 0);
        _p.Boundary(0, 0, MapSkip.None, MapSpeedClass.FF);
        _p.AddPhase(MapPhase.MapState, 80);
        CloseFrameAt(50);

        Assert.AreEqual(0, Take(50).OtherMs, D);
    }

    [TestMethod]
    public void Boundary_LoadingAtTheFramesStart_DiscardsItAsLoading()
    {
        _p.BeginSession(0, true, 0, 0, 0);
        _p.Boundary(0, 0, MapSkip.Loading, MapSpeedClass.FF);
        _p.AddAppTick(1);
        _p.Boundary(50, 0, MapSkip.None, MapSpeedClass.FF);
        _p.AddAppTick(1);
        _p.Boundary(100, 0, MapSkip.None, MapSpeedClass.FF);

        var w = Take(100);

        Assert.AreEqual(1, w.Frames);
        Assert.AreEqual(50, w.WallMs, D);
        Assert.AreEqual(1, w.Skipped);
        Assert.AreEqual(1, _p.Summarize(8).SkippedLoading);
    }

    [TestMethod]
    public void Boundary_NotTopAtTheFramesStart_DiscardsItAsNotTop()
    {
        _p.BeginSession(0, true, 0, 0, 0);
        _p.Boundary(0, 0, MapSkip.NotTop, MapSpeedClass.FF);
        _p.AddAppTick(1);
        _p.Boundary(50, 0, MapSkip.None, MapSpeedClass.FF);
        _p.AddAppTick(1);
        _p.Boundary(100, 0, MapSkip.None, MapSpeedClass.FF);

        var w = Take(100);

        Assert.AreEqual(1, w.Frames);
        Assert.AreEqual(50, w.WallMs, D);
        Assert.AreEqual(1, w.Skipped);
        Assert.AreEqual(1, _p.Summarize(8).SkippedNotTop);
    }

    [TestMethod]
    public void Boundary_NoAppTickInTheFrame_DiscardsItAsAGap()
    {
        _p.BeginSession(0, true, 0, 0, 0);
        _p.Boundary(0, 0, MapSkip.None, MapSpeedClass.FF);
        _p.Boundary(50, 0, MapSkip.None, MapSpeedClass.FF);

        Assert.AreEqual(0, Take(50).Frames);
        Assert.AreEqual(1, _p.Summarize(8).SkippedGap);
    }

    [TestMethod]
    public void Boundary_TwoAppTicksInTheFrame_DiscardsItAsAGap()
    {
        _p.BeginSession(0, true, 0, 0, 0);
        _p.Boundary(0, 0, MapSkip.None, MapSpeedClass.FF);
        _p.AddAppTick(1);
        _p.AddAppTick(1);
        _p.Boundary(50, 0, MapSkip.None, MapSpeedClass.FF);

        var w = Take(50);
        Assert.AreEqual(0, w.Frames);
        Assert.AreEqual(1, w.Skipped);
        Assert.AreEqual(1, _p.Summarize(8).SkippedGap);
    }

    [TestMethod]
    public void Boundary_DiscardedFrame_KeepsNoEntryOrPhaseTime()
    {
        _p.BeginSession(0, true, 0, 0, 0);
        _p.Boundary(0, 0, MapSkip.None, MapSpeedClass.FF);
        _p.RecordEntry(_a, 3, 64, true);
        _p.AddPhase(MapPhase.MapState, 10);
        _p.AddPhase(MapPhase.RealTick, 4);
        _p.AddPhase(MapPhase.CampaignTick, 4);
        _p.AddPhase(MapPhase.TickEvent, 3);
        _p.AddPhase(MapPhase.MapScreen, 10);
        _p.Boundary(50, 0, MapSkip.None, MapSpeedClass.FF);

        var w = Take(50);

        Assert.AreEqual(0, w.Top.Count);
        Assert.AreEqual(0, w.MapStateMs, D);
        Assert.AreEqual(0, w.RealTickMs, D);
        Assert.AreEqual(0, w.CampaignTickMs, D);
        Assert.AreEqual(0, w.TickEventMs, D);
        Assert.AreEqual(0, w.MapScreenMs, D);
        Assert.AreEqual(0, w.AppTickMs, D);
        Assert.AreEqual(0, w.TaomMs, D);
        Assert.AreEqual(0, w.WallMs, D);
    }

    [TestMethod]
    public void RecordEntry_TaomOwned_CountsTowardTaomMs_OthersDoNot()
    {
        _p.BeginSession(0, true, 0, 0, 0);
        _p.Boundary(0, 0, MapSkip.None, MapSpeedClass.FF);
        _p.RecordEntry(_a, 4, 0, true);
        _p.RecordEntry(_b, 6, 0, false);
        _p.AddAppTick(1);
        _p.Boundary(50, 0, MapSkip.None, MapSpeedClass.FF);

        Assert.AreEqual(5, Take(50).TaomMs, D);
    }

    [TestMethod]
    public void TakeWindow_Speed_IsTheClassWithMostFrames()
    {
        _p.BeginSession(0, true, 0, 0, 0);
        _p.Boundary(0, 0, MapSkip.None, MapSpeedClass.FF);
        CloseFrameAt(10, MapSpeedClass.FF);
        CloseFrameAt(20, MapSpeedClass.Stop);
        CloseFrameAt(30, MapSpeedClass.FF);

        var w = Take(30);
        Assert.AreEqual(3, w.Frames);
        Assert.AreEqual(MapSpeedClass.FF, w.Speed);
    }

    [TestMethod]
    public void TakeWindow_SpeedTie_GoesToTheFasterClass()
    {
        _p.BeginSession(0, true, 0, 0, 0);
        _p.Boundary(0, 0, MapSkip.None, MapSpeedClass.Stop);
        CloseFrameAt(10, MapSpeedClass.FF);
        CloseFrameAt(20, MapSpeedClass.FF);

        var w = Take(20);
        Assert.AreEqual(2, w.Frames);
        Assert.AreEqual(MapSpeedClass.FF, w.Speed);
    }

    [TestMethod]
    public void TakeWindow_NoClosedFrame_SpeedIsUnknown()
    {
        _p.BeginSession(0, true, 0, 0, 0);
        _p.Boundary(0, 0, MapSkip.None, MapSpeedClass.FF);

        Assert.AreEqual(MapSpeedClass.Unknown, Take(10).Speed);
    }

    [TestMethod]
    public void TakeWindow_GcDeltas_AreSinceTheSessionStartThenThePreviousWindow()
    {
        _p.BeginSession(0, true, 10, 5, 1);

        var first = _p.TakeWindow(5000, 8, 13, 6, 1);
        Assert.AreEqual(3, first.Gc0);
        Assert.AreEqual(1, first.Gc1);
        Assert.AreEqual(0, first.Gc2);

        var second = _p.TakeWindow(10000, 8, 14, 6, 2);
        Assert.AreEqual(1, second.Gc0);
        Assert.AreEqual(0, second.Gc1);
        Assert.AreEqual(1, second.Gc2);
    }

    [TestMethod]
    public void TakeWindow_ResetsTheWindowButNotTheSession()
    {
        _p.BeginSession(0, true, 0, 0, 0);
        _p.Boundary(0, 0, MapSkip.None, MapSpeedClass.FF);
        _p.RecordEntry(_a, 2, 0, true);
        CloseFrameAt(40);
        CloseFrameAt(80);

        Assert.AreEqual(2, Take(80).Frames);
        var again = Take(80);

        Assert.AreEqual(0, again.Frames);
        Assert.AreEqual(0, again.WallMs, D);
        Assert.AreEqual(0, again.Top.Count);
        Assert.AreEqual(MapSpeedClass.Unknown, again.Speed);
        var s = _p.Summarize(8);
        Assert.AreEqual(2, s.Frames);
        Assert.AreEqual(80, s.WallMs, D);
        Assert.AreEqual(1, s.Top.Count);
    }

    [TestMethod]
    public void TakeWindow_OpenFrameEntries_LandInTheNextWindow()
    {
        _p.BeginSession(0, true, 0, 0, 0);
        _p.Boundary(0, 0, MapSkip.None, MapSpeedClass.FF);
        _p.RecordEntry(_a, 2, 0, true);

        Assert.AreEqual(0, Take(10).Top.Count, "The entry's frame is still open.");

        CloseFrameAt(20);
        var w = Take(20);
        Assert.AreEqual(1, w.Top.Count);
        Assert.AreEqual("String", w.Top[0].Name);
    }

    [TestMethod]
    public void WindowDue_BeforeTheInterval_False_AtTheInterval_True()
    {
        _p.BeginSession(0, true, 0, 0, 0);

        Assert.IsFalse(_p.WindowDue(4999));
        Assert.IsTrue(_p.WindowDue(5000));
        _p.TakeWindow(5000, 8, 0, 0, 0);
        Assert.IsFalse(_p.WindowDue(9999));
        Assert.IsTrue(_p.WindowDue(10000));
    }

    [TestMethod]
    public void MaxFrameMs_IsTheSlowestClosedFrame_PerWindowAndPerSession()
    {
        _p.BeginSession(0, true, 0, 0, 0);
        _p.Boundary(0, 0, MapSkip.None, MapSpeedClass.FF);
        CloseFrameAt(30);
        CloseFrameAt(120);   // 90 ms
        CloseFrameAt(140);
        Assert.AreEqual(90, Take(140).MaxFrameMs, D);

        CloseFrameAt(180);   // 40 ms
        Assert.AreEqual(40, Take(180).MaxFrameMs, D, "A window's max covers only its own frames.");
        Assert.AreEqual(90, _p.Summarize(8).MaxFrameMs, D);
    }

    [TestMethod]
    public void BeginSession_ResetsEverythingAndIncrementsTheSession()
    {
        var first = _p.BeginSession(0, true, 0, 0, 0);
        _p.Boundary(0, 0, MapSkip.None, MapSpeedClass.FF);
        _p.RecordEntry(_a, 2, 0, true);
        CloseFrameAt(50);
        _p.Boundary(60, 0, MapSkip.Loading, MapSpeedClass.FF);
        CloseFrameAt(70);
        Assert.AreEqual(1, first);
        Assert.AreEqual(1, _p.Session);

        var second = _p.BeginSession(1000, true, 0, 0, 0);

        Assert.AreEqual(2, second);
        Assert.AreEqual(2, _p.Session);
        Assert.AreEqual(1000L, _p.SessionStartTicks);
        Assert.AreEqual(0.5, _p.SecondsSinceSessionStart(1500), D);
        var w = Take(1000);
        Assert.AreEqual(0, w.Frames);
        Assert.AreEqual(0, w.Skipped);
        Assert.AreEqual(0, w.Top.Count);
        var s = _p.Summarize(8);
        Assert.AreEqual(0, s.Frames);
        Assert.AreEqual(0, s.SkippedLoading + s.SkippedNotTop + s.SkippedGap);
        Assert.AreEqual(0, s.BySpeed.Count);
        Assert.AreEqual(0, s.Top.Count);
        Assert.AreEqual(1, s.Windows, "Only the window taken after the second BeginSession.");

        _p.Boundary(1000, 0, MapSkip.None, MapSpeedClass.FF);
        CloseFrameAt(1010);
        Assert.AreEqual(1, Take(1010).Frames, "The first boundary of the new session only stamped the clock.");
    }

    [TestMethod]
    public void StopMeasuring_OpenSession_StopsMeasuring()
    {
        _p.BeginSession(0, true, 0, 0, 0);
        Assert.IsTrue(_p.Measuring);

        _p.StopMeasuring();
        Assert.IsFalse(_p.Measuring);
    }

    [TestMethod]
    public void Summarize_CoversEveryClosedFrame_WithSkipReasonsSpeedsAndWindows()
    {
        _p.BeginSession(0, true, 0, 0, 0);
        _p.Boundary(0, 0, MapSkip.Loading, MapSpeedClass.Stop);
        _p.AddAppTick(0);
        _p.Boundary(40, 0, MapSkip.None, MapSpeedClass.Stop);    // 0-40 discarded: loading
        _p.AddPhase(MapPhase.MapState, 10);
        _p.AddAppTick(1);
        _p.Boundary(100, 0, MapSkip.None, MapSpeedClass.FF);     // 40-100 closed: 60 ms, Stop
        _p.TakeWindow(100, 8, 0, 0, 0);
        _p.AddPhase(MapPhase.MapState, 20);
        _p.AddAppTick(1);
        _p.Boundary(200, 0, MapSkip.NotTop, MapSpeedClass.FF);   // 100-200 closed: 100 ms, FF
        _p.AddAppTick(1);
        _p.Boundary(260, 0, MapSkip.None, MapSpeedClass.FF);     // 200-260 discarded: notTop
        _p.Boundary(300, 0, MapSkip.None, MapSpeedClass.FF);     // 260-300 discarded: gap (no app tick)
        _p.TakeWindow(300, 8, 0, 0, 0);

        var s = _p.Summarize(8);

        Assert.AreEqual(1, s.Session);
        Assert.AreEqual(2, s.Frames);
        Assert.AreEqual(160, s.WallMs, D);
        Assert.AreEqual(30, s.MapStateMs, D);
        Assert.AreEqual(2, s.AppTickMs, D);
        Assert.AreEqual(128, s.OtherMs, D);
        Assert.AreEqual(100, s.MaxFrameMs, D);
        Assert.AreEqual(2, s.Windows);
        Assert.AreEqual(1, s.SkippedLoading);
        Assert.AreEqual(1, s.SkippedNotTop);
        Assert.AreEqual(1, s.SkippedGap);
        Assert.AreEqual(2, s.BySpeed.Count);
        Assert.AreEqual(MapSpeedClass.Stop, s.BySpeed[0].Speed);
        Assert.AreEqual(1, s.BySpeed[0].Frames);
        Assert.AreEqual(60, s.BySpeed[0].WallMs, D);
        Assert.AreEqual(MapSpeedClass.FF, s.BySpeed[1].Speed);
        Assert.AreEqual(1, s.BySpeed[1].Frames);
        Assert.AreEqual(100, s.BySpeed[1].WallMs, D);
    }
}
