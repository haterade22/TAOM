using TAOM.Core.Diagnostics;
using TAOM.Core.Logging;

namespace TAOM.Features.LoadTimeStamps;

/// <summary>
/// Times the steps of one TAOM hook. Each <see cref="Mark"/> logs the time since the previous mark's
/// line was written (or since the start), so a step leaves out the previous line's own write. If the
/// clock read right after that write fails, the next step starts at the pre-write tick (the one read
/// just before that write) and carries that one write, but none of an earlier step. <see cref="End"/>
/// logs the wall-clock total from the start once, those writes included. Neither ever throws: a stamp
/// must never break a load.
/// </summary>
public sealed class HookTimer
{
    private readonly string _hook;
    private readonly string? _game;
    private readonly IStampClock _clock;
    private readonly IModLogger _logger;

    private readonly long _start;
    private long _lastMark;
    private int _steps;
    private bool _ended;

    internal HookTimer(string hook, string? game, IStampClock clock, IModLogger logger)
    {
        _hook = hook;
        _game = game;
        _clock = clock;
        _logger = logger;
        _start = clock.Now;
        _lastMark = _start;
    }

    /// <summary>Logs the step's time since the previous mark, then moves the mark past that line's own write.</summary>
    public void Mark(string step)
    {
        try
        {
            var now = _clock.Now;
            _logger.LogInfo(LoadTimeStampLines.HookStep(_hook, step, LoadTimeStampLines.ToMs(now - _lastMark, _clock.Frequency)));
            _lastMark = now;
            _steps++;
            // The next step starts once this line is written, so a slow write is never part of it. If
            // this read fails the pre-write tick stands and the written line stays counted.
            _lastMark = _clock.Now;
        }
        catch
        {
            // A stamp must never break a load.
        }
    }

    /// <summary>Logs the hook's wall-clock total since the start (its lines' writes included) and its step count, once.</summary>
    public void End()
    {
        try
        {
            if (_ended) return;
            _ended = true;
            _logger.LogInfo(LoadTimeStampLines.HookTotal(_hook, _game, LoadTimeStampLines.ToMs(_clock.Now - _start, _clock.Frequency), _steps));
        }
        catch
        {
            // A stamp must never break a load.
        }
    }
}
