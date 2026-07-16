using System;
using System.Collections.Concurrent;
using System.IO;
using System.Threading;

namespace TAOM.Core.Logging;

// Log lines must survive a hard crash. A native access violation kills the process without
// unwinding, so anything still sitting in the queue is lost -- which is exactly the moment the
// [BattleLoad]/[SaveLoad] phase stamps matter most. INFO/WARNING/ERROR therefore drain to disk
// synchronously on the calling thread; DEBUG (the bulk of the volume) stays async.
//
// One queue, not two: whichever thread drains writes in enqueue order, so a durable write flushes
// any DEBUG queued ahead of it and global ordering is preserved. Flush lands in the OS page cache,
// which a dying process does not lose -- only a machine crash would, so WriteThrough buys nothing
// here and would cost a physical write per line.
public class FileLogger : IModLogger
{
    private readonly ConcurrentQueue<string> _queue = new();
    private readonly object _writeLock = new();
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

    public void LogInfo(string message) => Enqueue("INFO", message, durable: true);
    public void LogDebug(string message) => Enqueue("DEBUG", message, durable: false);
    public void LogWarning(string message) => Enqueue("WARNING", message, durable: true);
    public void LogError(string message) => Enqueue("ERROR", message, durable: true);

    private void Enqueue(string level, string message, bool durable)
    {
        var timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
        _queue.Enqueue($"[{timestamp}] [{level}] {message}");
        if (durable) Drain();
    }

    // The only code that touches _logFile. StreamWriter is not thread-safe, so every path -- game
    // thread, writer thread, Dispose -- must come through here under _writeLock.
    private void Drain()
    {
        lock (_writeLock)
        {
            if (_logFile == null) return;
            try
            {
                while (_queue.TryDequeue(out var line))
                    _logFile.WriteLine(line);
                _logFile.Flush();
            }
            catch
            {
                // A transient IO fault (AV scanner, disk full) used to land harmlessly on the
                // writer thread. Durable writes run on the GAME thread, so it must be swallowed
                // here or it would propagate into engine code. Do not log from this catch: it
                // would re-enter Drain.
            }
        }
    }

    private void ProcessQueue()
    {
        while (!_stopping || !_queue.IsEmpty)
        {
            if (_queue.IsEmpty) { Thread.Sleep(50); continue; }
            Drain();
        }
    }

    public void Dispose()
    {
        _stopping = true;
        // Join can time out, leaving the writer thread live -- so the final drain and the disposal
        // of _logFile both go through the same lock the writer uses.
        _writerThread.Join(TimeSpan.FromSeconds(5));
        Drain();
        lock (_writeLock)
        {
            _logFile?.Dispose();
            _logFile = null;
        }
    }
}
