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
    private int _droppedToWriteFault; // guarded by _writeLock
    private StreamWriter _logFile;
    private readonly string _logPath;
    private const string LogDirectory = "Logs";
    private const string LogPattern = "taom_debug_*.log";
    private const int DefaultRetainedLogs = 10;

    public string? LogFilePath => _logPath;

    public FileLogger() : this(DefaultRetainedLogs) { }

    // retainedLogs: total taom_debug_*.log files to leave on disk INCLUDING the one about to be
    // opened; <= 0 disables pruning. Parameterised for the tests -- production always uses the
    // default via the parameterless ctor that IoC resolves.
    internal FileLogger(int retainedLogs)
    {
        var timestamp = DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss");
        _logPath = Path.Combine(LogDirectory, $"taom_debug_{timestamp}.log");
        Directory.CreateDirectory(LogDirectory);
        PruneOldLogs(retainedLogs);
        _logFile = new StreamWriter(_logPath, true);

        _writerThread = new Thread(ProcessQueue) { IsBackground = true, Name = "TAOM.FileLogger" };
        _writerThread.Start();
    }

    // Nothing used to prune Logs/, so debug logs accumulated for the life of the install. Runs
    // BEFORE the new file is opened, so the live log is never a deletion candidate.
    //
    // Fail-safe by construction: the whole pass is swallowed, and each delete is swallowed
    // individually so one locked file (AV scanner, a second game instance) cannot stop the rest.
    // A logger that throws from its constructor takes the mod's startup down with it.
    private static void PruneOldLogs(int retainedLogs)
    {
        if (retainedLogs <= 0) return;

        try
        {
            // Only taom_debug_*.log. Logs/ is shared with crash bundles (taom_crash_*.zip), the
            // battle-load stall marker and the shader-precompile sentinels -- deleting any of those
            // would destroy the very evidence this directory exists to hold.
            var existing = new DirectoryInfo(LogDirectory).GetFiles(LogPattern);
            if (existing.Length < retainedLogs) return;

            Array.Sort(existing, (a, b) => b.LastWriteTimeUtc.CompareTo(a.LastWriteTimeUtc));
            for (var i = retainedLogs - 1; i < existing.Length; i++)
            {
                try { existing[i].Delete(); } catch { /* locked or already gone -- keep going */ }
            }
        }
        catch
        {
            // Directory unreadable. Retention is housekeeping; never let it break logging.
        }
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
            try
            {
                // Dequeue even when _logFile is gone (post-Dispose), discarding via ?. -- an early
                // return here would leave items queued, and ProcessQueue's `!_queue.IsEmpty` exit
                // condition would never clear, spinning the writer thread hot on a core forever.
                while (_queue.TryDequeue(out var line))
                    _logFile?.WriteLine(line);

                if (_logFile == null) return;

                // Self-reporting: a write fault silently drops the line it was mid-way through, so
                // say so in the log itself once writing recovers. This is the crash-forensics
                // instrument -- it must never look healthy while quietly losing lines. Written
                // directly, not enqueued, so it cannot re-enter Drain.
                if (_droppedToWriteFault > 0)
                {
                    _logFile.WriteLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] [WARNING] " +
                        $"FileLogger: {_droppedToWriteFault} line(s) lost to a write fault.");
                    _droppedToWriteFault = 0;
                }

                _logFile.Flush();
            }
            catch
            {
                // A transient IO fault (AV scanner, disk full) used to land on the writer thread --
                // where, unguarded, it would have killed the process. Durable writes now run on the
                // GAME thread, so it must be swallowed here or it would propagate into engine code.
                // Do not log from this catch: it would re-enter Drain. Count it instead; the notice
                // above emits once writing recovers. Items still queued are retried next drain.
                _droppedToWriteFault++;
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
