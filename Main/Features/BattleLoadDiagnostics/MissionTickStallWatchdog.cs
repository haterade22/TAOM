using System;
using System.Threading;
using TAOM.Core.Logging;

namespace TAOM.Features.BattleLoadDiagnostics;

/// <summary>
/// Names the frame a frozen battle is stuck in (#634). A player's game stopped mid-battle with the
/// heap flat, no exception and no crash report: the next frame's <c>Mission.OnPreTick</c> waits in
/// <c>WaitTickCompletion</c> (<c>while (!tickCompleted) Thread.Sleep(1)</c>) for the asynchronous agent
/// tick, so an agent tick that never finishes leaves nothing in the log. A 1 s timer reads the two
/// <see cref="MissionTickStallProbe"/>s; a tick in flight past 10, 20 and 40 s gets its thread's
/// managed stack written as a <c>[MissionStall]</c> ERROR, which names the spinning method and its
/// caller without a dump. One stack per thread: an agent tick running inline inside the frame
/// (fast-forward) is covered by the frame's sample. Stands down during the mission-exit window, which
/// <see cref="ExitStallSampler"/> owns, so two samplers never suspend the same thread; the frame probe
/// also never arms on a teardown frame. Crash forensics, not phase logging: like the memory sampler it
/// answers to its own MCM switch only, not the master Battle Load Diagnostics toggle.
/// </summary>
public sealed class MissionTickStallWatchdog : IDisposable
{
    private const string Tag = "[MissionStall]";
    private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(1);
    internal static readonly double[] SampleThresholdsSeconds = { 10.0, 20.0, 40.0 };

    private readonly IModLogger _logger;
    private readonly IBattleLoadDiagnosticsSettingsProvider _settings;
    private readonly IBattleLoadDiagnosticsService _service;
    private readonly Episode _frame;
    private readonly Episode _agentTick;

    private Timer? _timer;
    private int _pollActive; // a capture that outlives the period must not overlap the next poll

    // One probe's current stall: when it started and how many samples it has had.
    private sealed class Episode
    {
        public readonly MissionTickStallProbe Probe;
        public long Since;
        public int SamplesTaken;

        public Episode(MissionTickStallProbe probe) => Probe = probe;
    }

    public MissionTickStallWatchdog(
        IModLogger logger,
        IBattleLoadDiagnosticsSettingsProvider settings,
        IBattleLoadDiagnosticsService service)
        : this(logger, settings, service, MissionTickStallProbe.MissionFrame, MissionTickStallProbe.AsyncAgentTick)
    {
    }

    internal MissionTickStallWatchdog(
        IModLogger logger,
        IBattleLoadDiagnosticsSettingsProvider settings,
        IBattleLoadDiagnosticsService service,
        MissionTickStallProbe frame,
        MissionTickStallProbe agentTick)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _settings = settings ?? throw new ArgumentNullException(nameof(settings));
        _service = service ?? throw new ArgumentNullException(nameof(service));
        _frame = new Episode(frame ?? throw new ArgumentNullException(nameof(frame)));
        _agentTick = new Episode(agentTick ?? throw new ArgumentNullException(nameof(agentTick)));
    }

    /// <summary>The stack source; tests swap it for a fake that suspends nothing.</summary>
    internal Func<Thread, string> DescribeStack { get; set; } =
        thread => ThreadStackCapture.FormatFrames(ThreadStackCapture.Capture(thread));

    /// <summary>Idempotent, like its siblings.</summary>
    public void Start()
    {
        if (_timer != null) return;
        _timer = new Timer(_ => SafePoll(), null, PollInterval, PollInterval);
    }

    public static bool ShouldSample(double elapsedSeconds, int samplesTaken)
        => samplesTaken < SampleThresholdsSeconds.Length
           && elapsedSeconds >= SampleThresholdsSeconds[samplesTaken];

    private void SafePoll()
    {
        if (Interlocked.Exchange(ref _pollActive, 1) == 1) return;
        try
        {
            Poll(DateTime.UtcNow.Ticks);
        }
        catch (Exception ex)
        {
            try { _logger.LogWarning($"{Tag} poll failed: {ex.GetType().Name}: {ex.Message}"); }
            catch { /* never propagate from a timer callback */ }
        }
        finally
        {
            Interlocked.Exchange(ref _pollActive, 0);
        }
    }

    internal void Poll(long nowUtcTicks)
    {
        if (!_settings.MissionTickStallSamplerEnabled || _service.IsExitWindowActive) return;

        bool frameLive = _frame.Probe.TryRead(out long frameSince, out Thread? frameThread);
        bool agentLive = _agentTick.Probe.TryRead(out long agentSince, out Thread? agentThread);

        // Fast-forward runs the agent tick inline inside the frame, on the frame's thread: one stack,
        // the frame's. Suspending one thread twice per threshold buys nothing.
        if (agentLive && frameLive && ReferenceEquals(agentThread, frameThread))
            agentLive = false;

        Judge(_frame, frameLive, frameSince, frameThread, nowUtcTicks, string.Empty);
        Judge(_agentTick, agentLive, agentSince, agentThread, nowUtcTicks,
            " (the next frame's Mission.WaitTickCompletion waits on its agent and team ticks)");
    }

    private void Judge(Episode episode, bool live, long since, Thread? thread, long nowUtcTicks, string note)
    {
        if (!live)
        {
            episode.Since = 0L;
            episode.SamplesTaken = 0;
            return;
        }

        if (since != episode.Since)
        {
            episode.Since = since;
            episode.SamplesTaken = 0;
        }

        double elapsed = TimeSpan.FromTicks(nowUtcTicks - since).TotalSeconds;
        if (!ShouldSample(elapsed, episode.SamplesTaken)) return;

        Report(episode.Probe, thread, ++episode.SamplesTaken, elapsed, note);
    }

    private void Report(MissionTickStallProbe probe, Thread? thread, int sampleIndex, double elapsedSeconds, string note)
    {
        string frames;
        if (thread == null)
        {
            frames = "    <thread unknown>";
        }
        else
        {
            try { frames = DescribeStack(thread); }
            catch (Exception ex) { frames = $"    <capture failed: {ex.GetType().Name}: {ex.Message}>"; }
        }

        string threadId = thread?.ManagedThreadId.ToString() ?? "?";
        _logger.LogError(
            $"{Tag} {probe.Name} in flight {elapsedSeconds:F0}s on thread {threadId}, sample#{sampleIndex}{note}:\n{frames}");
    }

    public void Dispose()
    {
        _timer?.Dispose();
        _timer = null;
    }
}
