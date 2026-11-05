using System;
using System.Collections.Generic;
using TAOM.Features.MissionPerf;

namespace TAOM.Features.MapPerf;

/// <summary>The bracketed phases of a campaign-map frame. <c>RealTick</c> and <c>CampaignTick</c> are inside
/// <c>MapState</c>; <c>TickEvent</c> is inside <c>CampaignTick</c>.</summary>
public enum MapPhase { MapState, RealTick, CampaignTick, TickEvent, MapScreen }

/// <summary>Why a frame is discarded, read at the frame's START boundary.</summary>
public enum MapSkip { None, Loading, NotTop }

/// <summary>
/// The pure accumulator behind the Patch101 campaign map profiler. One frame is one application tick in
/// which <c>MapState.OnTick</c> ran; its prefix calls <see cref="Boundary"/>. A frame closes into the window
/// and the session only when a previous boundary exists in this session, its start boundary saw no loading
/// window and the map screen on top, and TAOM's <c>OnApplicationTick</c> ran exactly once inside it;
/// otherwise its phase sums and entry calls are dropped and it is counted as <c>loading</c>, <c>notTop</c>
/// or <c>gap</c>. Per closed frame <c>otherMs = max(0, frameMs - mapStateMs - mapScreenMs - appTickMs)</c>,
/// so <c>wallMs = mapStateMs + mapScreenMs + appTickMs + otherMs</c> whenever nothing was clamped.
///
/// <see cref="Entries"/> is plan 028's per-type table; its "mission" layer is this profiler's session layer.
/// No TaleWorlds type, no allocation per frame after a type's first sighting. Main thread only, NOT
/// thread-safe: every caller is a Patch101 hook on the main thread.
/// </summary>
public sealed class MapFrameProfiler
{
    private readonly long _ticksPerSecond;
    private readonly double _windowTicks;

    private bool _haveBoundary;
    private long _lastBoundary;
    private long _lastAlloc;
    private MapSkip _openSkip;
    private MapSpeedClass _openSpeed;

    private Totals _frame;
    private int _frameAppTicks;
    private Totals _window;
    private Totals _session;
    private long _windowOpen;
    private int _windowSkipped;
    private int _windows;
    private int _skippedLoading;
    private int _skippedNotTop;
    private int _skippedGap;
    private readonly int[] _windowSpeedFrames = new int[SpeedCount];
    private readonly int[] _sessionSpeedFrames = new int[SpeedCount];
    private readonly long[] _sessionSpeedTicks = new long[SpeedCount];
    private int _gc0;
    private int _gc1;
    private int _gc2;

    private const int SpeedCount = (int)MapSpeedClass.FF3 + 1;

    // byspeed order: Stop, Play, FF, FF2, FF3, then na.
    private static readonly MapSpeedClass[] SpeedOrder =
    {
        MapSpeedClass.Stop, MapSpeedClass.Play, MapSpeedClass.FF, MapSpeedClass.FF2, MapSpeedClass.FF3, MapSpeedClass.Unknown,
    };

    public MapFrameProfiler(long ticksPerSecond, double windowSeconds = 5.0)
    {
        _ticksPerSecond = ticksPerSecond;
        _windowTicks = windowSeconds * ticksPerSecond;
    }

    public BehaviorTickTable Entries { get; } = new BehaviorTickTable();

    public bool Measuring { get; private set; }

    public int Session { get; private set; }

    public long SessionStartTicks { get; private set; }

    public double SecondsSinceSessionStart(long nowTicks) => (nowTicks - SessionStartTicks) / (double)_ticksPerSecond;

    /// <summary>Opens a new session: resets every accumulator, stores the GC counts, returns the session number.</summary>
    public int BeginSession(long nowTicks, bool measuring, int gc0, int gc1, int gc2)
    {
        _haveBoundary = false;
        _lastBoundary = 0;
        _lastAlloc = 0;
        _openSkip = MapSkip.None;
        _openSpeed = MapSpeedClass.Unknown;
        ClearFrame();
        _window = default;
        _session = default;
        _windowSkipped = 0;
        _windows = 0;
        _skippedLoading = 0;
        _skippedNotTop = 0;
        _skippedGap = 0;
        Array.Clear(_windowSpeedFrames, 0, SpeedCount);
        Array.Clear(_sessionSpeedFrames, 0, SpeedCount);
        Array.Clear(_sessionSpeedTicks, 0, SpeedCount);
        SessionStartTicks = nowTicks;
        _windowOpen = nowTicks;
        _gc0 = gc0;
        _gc1 = gc1;
        _gc2 = gc2;
        Entries.ResetFrame();
        Entries.ResetWindow();
        Entries.ResetMission();
        Session++;
        Measuring = measuring;
        return Session;
    }

    /// <summary>Stops measuring the open session.</summary>
    public void StopMeasuring() => Measuring = false;

    public void AddPhase(MapPhase phase, long elapsedTicks)
    {
        switch (phase)
        {
            case MapPhase.MapState: _frame.MapState += elapsedTicks; break;
            case MapPhase.RealTick: _frame.RealTick += elapsedTicks; break;
            case MapPhase.CampaignTick: _frame.CampaignTick += elapsedTicks; break;
            case MapPhase.TickEvent: _frame.TickEvent += elapsedTicks; break;
            case MapPhase.MapScreen: _frame.MapScreen += elapsedTicks; break;
        }
    }

    /// <summary>TAOM's <c>OnApplicationTick</c>: adds its time and counts it for the continuity rule.</summary>
    public void AddAppTick(long elapsedTicks)
    {
        _frame.AppTick += elapsedTicks;
        _frameAppTicks++;
    }

    public void RecordEntry(int slot, long elapsedTicks, long allocBytes, bool taomOwned)
    {
        Entries.Record(slot, elapsedTicks, allocBytes);
        if (taomOwned)
            _frame.Taom += elapsedTicks;
    }

    /// <summary>The <c>MapState.OnTick</c> prefix: closes or discards the open frame, then opens the next one
    /// with the skip reason and speed class read now.</summary>
    public void Boundary(long nowTicks, long allocBytesNow, MapSkip skipNow, MapSpeedClass speedNow)
    {
        if (!_haveBoundary)
        {
            _haveBoundary = true;
            Entries.ResetFrame();
        }
        else if (_openSkip == MapSkip.Loading)
        {
            Discard();
            _skippedLoading++;
        }
        else if (_openSkip == MapSkip.NotTop)
        {
            Discard();
            _skippedNotTop++;
        }
        else if (_frameAppTicks != 1)
        {
            Discard();
            _skippedGap++;
        }
        else
        {
            var frameTicks = nowTicks - _lastBoundary;
            _frame.Wall = frameTicks;
            _frame.Other = Math.Max(0L, frameTicks - _frame.MapState - _frame.MapScreen - _frame.AppTick);
            _frame.Alloc = allocBytesNow - _lastAlloc;
            _frame.MaxFrame = frameTicks;
            _frame.Frames = 1;
            _frame.Taom += _frame.AppTick;   // taomMs: TAOM-owned listener and view entries plus TAOM's app tick
            Entries.FoldFrame();
            _window.Add(in _frame);
            _session.Add(in _frame);
            var speed = (int)_openSpeed;
            _windowSpeedFrames[speed]++;
            _sessionSpeedFrames[speed]++;
            _sessionSpeedTicks[speed] += frameTicks;
        }

        _lastBoundary = nowTicks;
        _lastAlloc = allocBytesNow;
        _openSkip = skipNow;
        _openSpeed = speedNow;
        ClearFrame();
    }

    public bool WindowDue(long nowTicks) => nowTicks - _windowOpen >= _windowTicks;

    /// <summary>Builds the window line's values, then resets the window. Never touches the open frame.</summary>
    public MapWindow TakeWindow(long nowTicks, int topN, int gc0, int gc1, int gc2)
    {
        var speed = MapSpeedClass.Unknown;
        var best = 0;
        for (var s = 0; s < SpeedCount; s++)
        {
            // Ascending enum order with >=, so a tie goes to the faster (larger) class.
            if (_windowSpeedFrames[s] > 0 && _windowSpeedFrames[s] >= best)
            {
                best = _windowSpeedFrames[s];
                speed = (MapSpeedClass)s;
            }
        }

        var w = _window;
        var window = new MapWindow(w.Frames, Ms(w.Wall), Ms(w.RealTick), Ms(w.MapScreen), Ms(w.Other), w.Alloc, speed,
            Ms(w.MapState), Ms(w.CampaignTick), Ms(w.TickEvent), Ms(w.AppTick), Ms(w.Taom), Ms(w.MaxFrame),
            _windowSkipped, gc0 - _gc0, gc1 - _gc1, gc2 - _gc2, Entries.WindowTop(topN, _ticksPerSecond));

        _gc0 = gc0;
        _gc1 = gc1;
        _gc2 = gc2;
        _window = default;
        _windowSkipped = 0;
        Array.Clear(_windowSpeedFrames, 0, SpeedCount);
        Entries.ResetWindow();
        _windowOpen = nowTicks;
        _windows++;
        return window;
    }

    /// <summary>The session's totals over every closed frame. Resets nothing.</summary>
    public MapSummary Summarize(int topN)
    {
        var bySpeed = new List<SpeedTotal>(SpeedCount);
        foreach (var speed in SpeedOrder)
        {
            var s = (int)speed;
            if (_sessionSpeedFrames[s] > 0)
                bySpeed.Add(new SpeedTotal(speed, _sessionSpeedFrames[s], Ms(_sessionSpeedTicks[s])));
        }

        var t = _session;
        return new MapSummary(Session, t.Frames, Ms(t.Wall), Ms(t.RealTick), Ms(t.MapScreen), Ms(t.Other), t.Alloc,
            Ms(t.MapState), Ms(t.CampaignTick), Ms(t.TickEvent), Ms(t.AppTick), Ms(t.Taom), Ms(t.MaxFrame),
            _windows, _skippedLoading, _skippedNotTop, _skippedGap, bySpeed, Entries.MissionTop(topN, _ticksPerSecond));
    }

    private void Discard()
    {
        Entries.ResetFrame();
        _windowSkipped++;
    }

    private void ClearFrame()
    {
        _frame = default;
        _frameAppTicks = 0;
    }

    private double Ms(long ticks) => ticks * 1000d / _ticksPerSecond;

    /// <summary>Tick sums of one layer (the open frame, the window or the session).</summary>
    private struct Totals
    {
        public int Frames;
        public long Wall;
        public long MapState;
        public long RealTick;
        public long CampaignTick;
        public long TickEvent;
        public long MapScreen;
        public long AppTick;
        public long Taom;
        public long Other;
        public long Alloc;
        public long MaxFrame;

        public void Add(in Totals f)
        {
            Frames += f.Frames;
            Wall += f.Wall;
            MapState += f.MapState;
            RealTick += f.RealTick;
            CampaignTick += f.CampaignTick;
            TickEvent += f.TickEvent;
            MapScreen += f.MapScreen;
            AppTick += f.AppTick;
            Taom += f.Taom;
            Other += f.Other;
            Alloc += f.Alloc;
            if (f.MaxFrame > MaxFrame)
                MaxFrame = f.MaxFrame;
        }
    }
}

/// <summary>One <c>[MapProfile]</c> window's values, in line order.</summary>
public sealed class MapWindow
{
    public MapWindow(int frames, double wallMs, double realTickMs, double mapScreenMs, double otherMs, long allocBytes,
        MapSpeedClass speed, double mapStateMs, double campaignTickMs, double tickEventMs, double appTickMs, double taomMs,
        double maxFrameMs, int skipped, int gc0, int gc1, int gc2, IReadOnlyList<BehaviorTotal> top)
    {
        Frames = frames;
        WallMs = wallMs;
        RealTickMs = realTickMs;
        MapScreenMs = mapScreenMs;
        OtherMs = otherMs;
        AllocBytes = allocBytes;
        Speed = speed;
        MapStateMs = mapStateMs;
        CampaignTickMs = campaignTickMs;
        TickEventMs = tickEventMs;
        AppTickMs = appTickMs;
        TaomMs = taomMs;
        MaxFrameMs = maxFrameMs;
        Skipped = skipped;
        Gc0 = gc0;
        Gc1 = gc1;
        Gc2 = gc2;
        Top = top;
    }

    public int Frames { get; }
    public double WallMs { get; }
    public double RealTickMs { get; }
    public double MapScreenMs { get; }
    public double OtherMs { get; }
    public long AllocBytes { get; }
    public MapSpeedClass Speed { get; }
    public double MapStateMs { get; }
    public double CampaignTickMs { get; }
    public double TickEventMs { get; }
    public double AppTickMs { get; }
    public double TaomMs { get; }
    public double MaxFrameMs { get; }
    public int Skipped { get; }
    public int Gc0 { get; }
    public int Gc1 { get; }
    public int Gc2 { get; }
    public IReadOnlyList<BehaviorTotal> Top { get; }
}

/// <summary>One <c>[MapProfileSummary]</c> session's values, in line order.</summary>
public sealed class MapSummary
{
    public MapSummary(int session, int frames, double wallMs, double realTickMs, double mapScreenMs, double otherMs,
        long allocBytes, double mapStateMs, double campaignTickMs, double tickEventMs, double appTickMs, double taomMs,
        double maxFrameMs, int windows, int skippedLoading, int skippedNotTop, int skippedGap,
        IReadOnlyList<SpeedTotal> bySpeed, IReadOnlyList<BehaviorTotal> top)
    {
        Session = session;
        Frames = frames;
        WallMs = wallMs;
        RealTickMs = realTickMs;
        MapScreenMs = mapScreenMs;
        OtherMs = otherMs;
        AllocBytes = allocBytes;
        MapStateMs = mapStateMs;
        CampaignTickMs = campaignTickMs;
        TickEventMs = tickEventMs;
        AppTickMs = appTickMs;
        TaomMs = taomMs;
        MaxFrameMs = maxFrameMs;
        Windows = windows;
        SkippedLoading = skippedLoading;
        SkippedNotTop = skippedNotTop;
        SkippedGap = skippedGap;
        BySpeed = bySpeed;
        Top = top;
    }

    public int Session { get; }
    public int Frames { get; }
    public double WallMs { get; }
    public double RealTickMs { get; }
    public double MapScreenMs { get; }
    public double OtherMs { get; }
    public long AllocBytes { get; }
    public double MapStateMs { get; }
    public double CampaignTickMs { get; }
    public double TickEventMs { get; }
    public double AppTickMs { get; }
    public double TaomMs { get; }
    public double MaxFrameMs { get; }
    public int Windows { get; }
    public int SkippedLoading { get; }
    public int SkippedNotTop { get; }
    public int SkippedGap { get; }
    public IReadOnlyList<SpeedTotal> BySpeed { get; }
    public IReadOnlyList<BehaviorTotal> Top { get; }
}

/// <summary>One speed class's closed frames and wall time over a session (<c>byspeed=</c>).</summary>
public sealed class SpeedTotal
{
    public SpeedTotal(MapSpeedClass speed, int frames, double wallMs)
    {
        Speed = speed;
        Frames = frames;
        WallMs = wallMs;
    }

    public MapSpeedClass Speed { get; }
    public int Frames { get; }
    public double WallMs { get; }
}
