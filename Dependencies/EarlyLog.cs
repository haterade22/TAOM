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

    // Guards the hand-over. A writer's "no target yet, so enqueue" and DrainTo's "buffer empty, so publish
    // the target" must not interleave: a line enqueued after the last dequeue but before the publish would
    // sit in the buffer for the rest of the process. It is never held while a logger runs, so a logger that
    // logs again (or triggers an AssemblyResolve that does) cannot deadlock on it.
    private static readonly object _gate = new();
    private static Action<string, string>? _drainTarget;

    public static void Info(string message) => Write("INFO", message);
    public static void Warning(string message) => Write("WARNING", message);
    public static void Error(string message) => Write("ERROR", message);

    private static void Write(string level, string message)
    {
        Action<string, string>? target;
        lock (_gate)
        {
            target = _drainTarget;
            if (target == null)
            {
                _buffer.Enqueue((level, message, DateTime.Now));
                return;
            }
        }
        target(level, message);
    }

    /// <summary>
    /// Called by Main's SubModule after FileLogger is created.
    /// Flushes all buffered messages and routes future calls directly to the logger.
    /// A line logged while the flush runs queues behind the older ones, so the log keeps the order the
    /// lines were written in and none is left behind.
    /// </summary>
    public static void DrainTo(Action<string, string> logger)
    {
        while (true)
        {
            (string Level, string Message, DateTime Time) entry;
            lock (_gate)
            {
                if (!_buffer.TryDequeue(out entry))
                {
                    _drainTarget = logger;
                    return;
                }
            }
            logger(entry.Level, $"[buffered {entry.Time:HH:mm:ss}] {entry.Message}");
        }
    }
}
