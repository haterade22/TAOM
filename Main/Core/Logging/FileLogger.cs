using System;
using System.Collections.Concurrent;
using System.IO;
using System.Threading;

namespace TAOM.Core.Logging;

public class FileLogger : IModLogger
{
    private readonly ConcurrentQueue<string> _queue = new();
    private readonly Thread _writerThread;
    private volatile bool _stopping;
    private StreamWriter _logFile;
    private readonly string _logPath;
    private const string LogDirectory = "Logs";

    public string? LogFilePath => _logPath;

    public FileLogger()
    {
        var timestamp = DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss");
        _logPath = Path.Combine(LogDirectory, $"taom_debug_{timestamp}.log");
        Directory.CreateDirectory(LogDirectory);
        _logFile = new StreamWriter(_logPath, true);

        _writerThread = new Thread(ProcessQueue) { IsBackground = true, Name = "TAOM.FileLogger" };
        _writerThread.Start();
    }

    public void LogInfo(string message) => Enqueue("INFO", message);
    public void LogDebug(string message) => Enqueue("DEBUG", message);
    public void LogWarning(string message) => Enqueue("WARNING", message);
    public void LogError(string message) => Enqueue("ERROR", message);

    private void Enqueue(string level, string message)
    {
        var timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
        _queue.Enqueue($"[{timestamp}] [{level}] {message}");
    }

    private void ProcessQueue()
    {
        while (!_stopping || !_queue.IsEmpty)
        {
            if (_queue.TryDequeue(out var line))
            {
                _logFile?.WriteLine(line);
                _logFile?.Flush();
            }
            else
            {
                Thread.Sleep(50);
            }
        }
    }

    public void Dispose()
    {
        _stopping = true;
        // Wait for writer thread to drain the queue (up to 5s)
        _writerThread.Join(TimeSpan.FromSeconds(5));

        // Drain any remaining items if thread timed out
        while (_queue.TryDequeue(out var line))
            _logFile?.WriteLine(line);

        _logFile?.Flush();
        _logFile?.Dispose();
        _logFile = null;
    }
}
