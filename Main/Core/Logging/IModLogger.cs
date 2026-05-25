using System;

namespace TAOM.Core.Logging;

public interface IModLogger : IDisposable
{
    void LogInfo(string message);
    void LogDebug(string message);
    void LogWarning(string message);
    void LogError(string message);

    // Path to the currently-open log file, or null when the logger writes nowhere
    // (test doubles, in-memory loggers). Used by CrashReport to attach the live log
    // file into the crash bundle ZIP and to print "log at X" in the report header.
    string? LogFilePath { get; }
}
