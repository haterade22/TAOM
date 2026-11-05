using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using TAOM.Core.Logging;

namespace TAOM.Features.BattleLoadDiagnostics;

/// <summary>
/// Names what a frozen game is doing, anywhere in the game. A 1 s timer reads
/// <see cref="MainThreadHeartbeat"/>; at 10, 20, 40 and 80 s of silence it writes a [Freeze] line with
/// the engine's own compile_shader count since the last frame and, when that is safe, the main
/// thread's stack. A new beat is the only end of an episode. Stands down for the shader pre-compilation
/// walk and the mission-exit window; leaves the stack to [MissionStall] while a mission frame is in
/// flight. Never calls the native shader counter, which is main-thread only: the compile evidence
/// comes from a file tail. Answers to its own MCM switch, not the master toggle.
/// </summary>
public sealed class MainThreadFreezeWatchdog : IDisposable
{
    private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(1);

    private readonly IModLogger _logger;
    private readonly IBattleLoadDiagnosticsSettingsProvider _settings;
    private readonly IBattleLoadDiagnosticsService _service;
    private readonly IRenderWaitNotice _notice;
    private readonly MainThreadHeartbeat _heartbeat;
    private readonly MissionTickStallProbe _missionFrame;
    private readonly IRglCompileTail _tail;
    private readonly Func<int> _gen0;
    private readonly Func<bool> _walkActive;
    private readonly Func<bool> _inRenderWait;

    private Timer? _timer;
    private int _pollActive; // a capture that outlives the period must not overlap the next poll
    private long _lastSeenBeat;
    private Episode? _episode;
    private int _gcRef;
    private long _gcRefTicks;
    private bool _dumpAttempted; // the singleton lives for the process, so this is once per process

    // One stretch of silence: the beat it started from and what it has logged.
    private sealed class Episode
    {
        public readonly long Since;
        public int SamplesTaken;
        public int LinesLogged;
        public bool HeapLineLogged;

        public Episode(long since) => Since = since;
    }

    public MainThreadFreezeWatchdog(
        IModLogger logger,
        IBattleLoadDiagnosticsSettingsProvider settings,
        IBattleLoadDiagnosticsService service,
        IRenderWaitNotice notice)
        : this(logger, settings, service, notice, MainThreadHeartbeat.Instance, MissionTickStallProbe.MissionFrame,
            new RglCompileTail(RglLogLocator.ResolveForCurrentProcess),
            () => GC.CollectionCount(0), () => BattleLoadStallWatchdog.SuppressStallDetection,
            () => BattleLoadLoadingWindow.IsOpen && BattleLoadRenderWaitProbe.ShadersInFlight >= 0)
    {
    }

    internal MainThreadFreezeWatchdog(
        IModLogger logger,
        IBattleLoadDiagnosticsSettingsProvider settings,
        IBattleLoadDiagnosticsService service,
        IRenderWaitNotice notice,
        MainThreadHeartbeat heartbeat,
        MissionTickStallProbe missionFrame,
        IRglCompileTail tail,
        Func<int> gen0Collections,
        Func<bool> walkActive,
        Func<bool> inRenderWait)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _settings = settings ?? throw new ArgumentNullException(nameof(settings));
        _service = service ?? throw new ArgumentNullException(nameof(service));
        _notice = notice ?? throw new ArgumentNullException(nameof(notice));
        _heartbeat = heartbeat ?? throw new ArgumentNullException(nameof(heartbeat));
        _missionFrame = missionFrame ?? throw new ArgumentNullException(nameof(missionFrame));
        _tail = tail ?? throw new ArgumentNullException(nameof(tail));
        _gen0 = gen0Collections ?? throw new ArgumentNullException(nameof(gen0Collections));
        _walkActive = walkActive ?? throw new ArgumentNullException(nameof(walkActive));
        _inRenderWait = inRenderWait ?? throw new ArgumentNullException(nameof(inRenderWait));
    }

    /// <summary>The stack source; tests swap it for a fake that suspends nothing.</summary>
    internal Func<Thread, string> DescribeStack { get; set; } =
        thread => FormatStack(ThreadStackCapture.Capture(thread));

    internal delegate bool TryWriteDump(string directory, out string pathOrError);

    /// <summary>The dump source; tests swap it for a fake that writes nothing.</summary>
    internal TryWriteDump WriteDump { get; set; } = FreezeDumpWriter.TryWrite;

    internal static string FormatStack(StackTrace stack)
        => stack.FrameCount == 0 ? FreezeLog.NoManagedFrames : ThreadStackCapture.FormatFrames(stack);

    /// <summary>Called on the main thread; idempotent, because its hook fires on every return to the main menu.</summary>
    public void Start()
    {
        if (_timer != null) return;
        _heartbeat.BindCurrentThread();
        _logger.LogInfo(FreezeLog.FormatStarted(_tail.Path));
        _timer = new Timer(_ => SafePoll(), null, PollInterval, PollInterval);
    }

    private void SafePoll()
    {
        if (Interlocked.Exchange(ref _pollActive, 1) == 1) return;
        try
        {
            Poll(DateTime.UtcNow.Ticks);
        }
        catch (Exception ex)
        {
            try { _logger.LogWarning($"{FreezeLog.Tag} poll failed: {ex.GetType().Name}: {ex.Message}"); }
            catch { /* never propagate from a timer callback */ }
        }
        finally
        {
            Interlocked.Exchange(ref _pollActive, 0);
        }
    }

    internal void Poll(long now)
    {
        if (!_settings.MainThreadFreezeSamplerEnabled)
        {
            _episode = null;
            ShowNotice(RenderWaitNoticeDecision.Hidden);
            return;
        }

        if (!_heartbeat.TryRead(out long beat, out Thread? main)) return;

        if (beat != _lastSeenBeat)
        {
            _lastSeenBeat = beat;
            ShowNotice(RenderWaitNoticeDecision.Hidden);
            if (_episode != null && _episode.LinesLogged > 0)
                _logger.LogInfo(FreezeLog.FormatRecovered(Secs(beat - _episode.Since), _tail.Available, _tail.CompilesSinceFresh));
            _episode = null;
            _tail.MarkFresh();
            _gcRef = _gen0();
            _gcRefTicks = now;
            return;
        }

        double silent = SecsD(now - beat);
        _episode ??= new Episode(beat);
        _tail.ReadNew(now);
        ShowNotice(RenderWaitNoticeDecider.Decide(_inRenderWait(), _walkActive(), silent,
            _tail.Available, _tail.CompilesSinceFresh, _tail.SecondsSinceLastCompile(now)));

        if (!FreezeSamplePolicy.CrossesThreshold(silent, _episode.SamplesTaken)) return;

        int index = _episode.SamplesTaken++;
        var standDown = _walkActive() ? FreezeStandDown.PrecompileWalk
            : _service.ExitWindowOpenedUtcTicks != 0L ? FreezeStandDown.ExitSampler
            : _missionFrame.TryRead(out _, out _) ? FreezeStandDown.MissionFrame
            : FreezeStandDown.None;

        int gen0Now = _gen0();
        int gcDelta = gen0Now - _gcRef;
        double window = SecsD(now - _gcRefTicks);
        bool heapMoving = FreezeSamplePolicy.HeapMoving(gcDelta, window);
        var action = FreezeSamplePolicy.Decide(index, standDown, heapMoving, _episode.HeapLineLogged);

        // The heap window restarts after every threshold, logged or not.
        _gcRef = gen0Now;
        _gcRefTicks = now;

        // Independent of the sample action; the line, when there is one, goes first.
        bool writeDump = FreezeDumpPolicy.ShouldWrite(_settings.FreezeDumpEnabled, _dumpAttempted, index, standDown,
            heapMoving, _tail.Available, _tail.SecondsSinceLastCompile(now));

        if (action != FreezeSampleAction.None) LogSample(action, standDown, index, silent, gcDelta, window, main, now);
        if (writeDump) WriteDumpOnce();
    }

    private void LogSample(FreezeSampleAction action, FreezeStandDown standDown, int index, double silent, int gcDelta, double window, Thread? main, long now)
    {
        string second = standDown == FreezeStandDown.MissionFrame ? FreezeLog.NotSampledMissionFrame
            : action == FreezeSampleAction.LogWithoutStack ? FreezeLog.NotSampledHeapMoving(gcDelta, (int)window)
            : Describe(main);
        if (action == FreezeSampleAction.LogWithoutStack && standDown == FreezeStandDown.None)
            _episode!.HeapLineLogged = true;
        _episode!.LinesLogged++;

        _logger.LogWarning(
            FreezeLog.FormatSample((int)silent, index + 1, Context(), ShadersIfWindowOpen(),
                _tail.Available, _tail.CompilesSinceFresh, LastCompileAgo(now), ProcessMemoryTokens.Format(),
                _service.CurrentStatusLine)
            + "\n" + second);
    }

    private void WriteDumpOnce()
    {
        _dumpAttempted = true;
        string directory = DumpDirectory();
        _logger.LogInfo(WriteDump(directory, out string pathOrError)
            ? $"{FreezeLog.Tag} memory dump written: {pathOrError} ({FileMegabytes(pathOrError)} MB)"
            : $"{FreezeLog.Tag} memory dump failed: {pathOrError}");
    }

    private string DumpDirectory()
    {
        try
        {
            string? log = _logger.LogFilePath;
            string? folder = string.IsNullOrEmpty(log) ? null : Path.GetDirectoryName(Path.GetFullPath(log));
            if (!string.IsNullOrEmpty(folder)) return folder!;
        }
        catch (Exception)
        {
            // fall through to the default folder
        }

        return "Logs";
    }

    private static long FileMegabytes(string path)
    {
        try { return new FileInfo(path).Length / (1024 * 1024); }
        catch (Exception) { return 0L; }
    }

    // A notice failure never stops the sampling.
    private void ShowNotice(RenderWaitNoticeDecision decision)
    {
        try { _notice.Show(decision); }
        catch (Exception ex) { _logger.LogWarning($"{FreezeLog.Tag} notice failed: {ex.GetType().Name}: {ex.Message}"); }
    }

    private string Describe(Thread? main)
    {
        if (main == null) return FreezeLog.ThreadUnknown;
        try { return DescribeStack(main); }
        catch (Exception ex) { return FreezeLog.CaptureFailed(ex); }
    }

    private string Context()
    {
        if (BattleLoadLoadingWindow.IsOpen) return "battle-load";
        return _missionFrame.TryRead(out _, out _) ? "mission-frame" : "other";
    }

    private static int ShadersIfWindowOpen()
        => BattleLoadLoadingWindow.IsOpen ? BattleLoadRenderWaitProbe.ShadersInFlight : -1;

    private int? LastCompileAgo(long now)
    {
        double? seconds = _tail.SecondsSinceLastCompile(now);
        return seconds.HasValue ? (int)seconds.Value : (int?)null;
    }

    private static double SecsD(long ticks) => TimeSpan.FromTicks(ticks).TotalSeconds;

    private static int Secs(long ticks) => (int)SecsD(ticks);

    public void Dispose()
    {
        _timer?.Dispose();
        _timer = null;
    }
}
