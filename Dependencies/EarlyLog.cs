using System;
using System.Collections.Concurrent;

namespace TAOM.Dependencies;

/// <summary>
/// Static log buffer for Dependencies module (loads before Main's FileLogger exists).
/// Main calls DrainTo() on startup to flush buffered messages into the real logger.
/// After drain, subsequent calls go directly to the provided logger.
/// </summary>
public static class EarlyLog
{
    private static readonly ConcurrentQueue<(string Level, string Message, DateTime Time)> _buffer = new();
    private static Action<string, string>? _drainTarget;

    public static void Info(string message) => Write("INFO", message);
    public static void Warning(string message) => Write("WARNING", message);
    public static void Error(string message) => Write("ERROR", message);

    private static void Write(string level, string message)
    {
        if (_drainTarget != null)
        {
            _drainTarget(level, message);
            return;
        }
        _buffer.Enqueue((level, message, DateTime.Now));
    }

    /// <summary>
    /// Called by Main's SubModule after FileLogger is created.
    /// Flushes all buffered messages and routes future calls directly to the logger.
    /// </summary>
    public static void DrainTo(Action<string, string> logger)
    {
        _drainTarget = logger;
        while (_buffer.TryDequeue(out var entry))
        {
            logger(entry.Level, $"[buffered {entry.Time:HH:mm:ss}] {entry.Message}");
        }
    }
}
