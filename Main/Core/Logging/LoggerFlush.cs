using System;

namespace TAOM.Core.Logging;

// A logger that holds lines in memory between timed flushes. FileLogger implements it; test doubles and
// in-memory loggers do not, so callers go through TryFlush instead of widening IModLogger.
public interface IFlushableLogger
{
    // Writes everything queued so far to the file and flushes it to the OS. Never throws.
    void Flush();
}

public static class ModLoggerFlushExtensions
{
    // Call before reading the log file back (crash bundle) or before the process can go away. A logger
    // without buffering has nothing to flush. Never throws: this runs while the process is already crashing.
    public static void TryFlush(this IModLogger logger)
    {
        try { (logger as IFlushableLogger)?.Flush(); }
        catch (Exception) { /* a failed flush costs the tail of the log, never the crash report */ }
    }
}
