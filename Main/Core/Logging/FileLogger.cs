using System;
using System.IO;

namespace TAOM.Core.Logging;

public class FileLogger : IModLogger
{
    private StreamWriter? _logFile;
    private readonly object _writeLock = new();
    private const string LogDirectory = "Logs";

    public FileLogger()
    {
        Initialize();
    }

    private void Initialize()
    {
        var timestamp = DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss");
        var logPath = Path.Combine(LogDirectory, $"taom_debug_{timestamp}.log");
        Directory.CreateDirectory(LogDirectory);
        _logFile = new StreamWriter(logPath, true) { AutoFlush = true };
    }

    public void LogInfo(string message)
    {
        WriteLog("INFO", message);
    }

    public void LogDebug(string message)
    {
        WriteLog("DEBUG", message);
    }

    public void LogWarning(string message)
    {
        WriteLog("WARNING", message);
    }

    public void LogError(string message)
    {
        WriteLog("ERROR", message);
    }

    private void WriteLog(string level, string message)
    {
        lock (_writeLock)
        {
            var timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
            var logMessage = $"[{timestamp}] [{level}] {message}";
            _logFile?.WriteLine(logMessage);
        }
    }

    public void Dispose()
    {
        lock (_writeLock)
        {
            _logFile?.Dispose();
            _logFile = null;
        }
    }
}
