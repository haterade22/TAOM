using System;
using System.Collections.Concurrent;
using System.IO;
using System.Threading;

namespace TAOM.Core.Logging;

// Log lines must survive a hard crash. A native access violation kills the process without
// unwinding, so anything still sitting in the queue or in the StreamWriter buffer is lost -- which is
// exactly the moment the [BattleLoad]/[SaveLoad] phase stamps matter most (#350). So the lines a crash
// is triaged from drain to disk synchronously on the calling thread: WARNING, ERROR, and INFO lines
// that start with one of DurableInfoTags (the phase stamps and the diagnostics whose docs promise the
// last line survives a crash).
//
// Everything else (plain INFO, and DEBUG) is queued and written by the writer thread. Its timer starts when
// the thread writes the line and is checked once per loop pass after a 50 ms Sleep, so a plain line can wait
// about 300 ms after it was written and about 350 ms after the call. Before that, nearly every INFO line
// (about 3,300 call sites) flushed on the game thread under the lock the writer thread also held, which showed up as hitch tails
// during load, spawn and the daily campaign tick. A plain line can now be lost on a hard crash if it
// was written in that window; Flush() (called by the crash reporter before it reads the
// log, on process exit and on Dispose) closes it for an orderly exit. Environment.FailFast, a stack
// overflow, a native crash and a killed process skip Flush, so a plain line can still be lost there.
//
// One queue, not two: whichever thread drains writes in enqueue order, so a durable write flushes
// any queued line ahead of it and global ordering is preserved. Flush lands in the OS page cache,
// which a dying process does not lose -- only a machine crash would, so WriteThrough buys nothing
// here and would cost a physical write per line.
public class FileLogger : IModLogger, IFlushableLogger
{
    private readonly ConcurrentQueue<string> _queue = new();
    private readonly object _writeLock = new();
    private readonly Thread _writerThread;
    private volatile bool _stopping;
    private int _droppedToWriteFault; // guarded by _writeLock
    private bool _unflushed;           // guarded by _writeLock: lines written to the StreamWriter but not yet flushed
    private long _unflushedSinceTick;  // guarded by _writeLock: Environment.TickCount when _unflushed became true
    private readonly EventHandler _processExitHandler;
    private StreamWriter _logFile;
    private readonly string _logPath;
    private const string LogDirectory = "Logs";
    private const string LogPattern = "taom_debug_*.log";
    // Sized for a support round trip, not for disk. Every launch prunes, so a player who crashes and
    // then keeps playing pushes the crash log one slot closer to deletion on each start; at the
    // original 10 it survived nine relaunches, which is shorter than the time it usually takes to
    // notice a crash, report it, and be asked for the log. Raised 2026-08-19 after a player report
    // arrived with a log from the wrong session and no way to tell whether the real one still
    // existed. Debug logs are small next to the crash bundles that already share this directory.
    private const int DefaultRetainedLogs = 30;

    // How long the writer thread lets written plain INFO/DEBUG lines sit before it flushes them to the OS.
    // The check runs once per loop pass after a 50 ms Sleep, so the real wait is a little longer.
    internal const int FlushIntervalMs = 250;

    // INFO lines that start with one of these flush at once. These are the crash-phase stamps and the
    // diagnostics that tell the player to "send the log, the LAST line names the cause". Adding a tag
    // here costs a synchronous flush per line, so keep it to lines that are not per-frame and that a
    // crash investigation reads. Matched case-sensitively against the start of the message.
    internal static readonly string[] DurableInfoTags =
    {
        "[BattleLoad]", "[SaveLoad]", "[MapLoad]", "[BlowDiag]", "[MemSample]", "[MemStation]", "[MemProbe]",
        "[ExitStall]", "[ScreenOpen]", "[CreatureBandits]", "[EnlistDiag]", "[ShaderPrecompilation]",
        "[CrashReport]", "[Shutdown]", "[PatchApply]", "[StuckBattle]", "[SmokeTestGate]", "[Session]",
        "[MissionDiag]", "[Check]",
    };

    internal static bool IsDurableInfo(string message)
    {
        if (string.IsNullOrEmpty(message)) return false;
        foreach (var tag in DurableInfoTags)
            if (message.StartsWith(tag, StringComparison.Ordinal)) return true;
        return false;
    }

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

        // Last chance for plain lines still queued when the process exits without Dispose.
        _processExitHandler = (_, _) => Flush();
        try { AppDomain.CurrentDomain.ProcessExit += _processExitHandler; } catch { /* best effort */ }
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

    public void LogInfo(string message) => Enqueue("INFO", message, durable: IsDurableInfo(message));
    public void LogDebug(string message) => Enqueue("DEBUG", message, durable: false);
    public void LogWarning(string message) => Enqueue("WARNING", message, durable: true);
    public void LogError(string message) => Enqueue("ERROR", message, durable: true);

    private void Enqueue(string level, string message, bool durable)
    {
        var timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
        _queue.Enqueue($"[{timestamp}] [{level}] {message}");
        // After Dispose the writer thread is gone, so nothing would ever drain a queued line: write it now
        // (Drain discards it once the file is closed) rather than let the queue grow for the rest of the session.
        if (durable || _stopping) Drain(flush: true);
    }

    // Writes everything queued and flushes it to the OS. Called by the crash reporter before it reads
    // the log, on process exit and from Dispose.
    public void Flush() => Drain(flush: true);

    // The only code that touches _logFile. StreamWriter is not thread-safe, so every path -- game
    // thread, writer thread, Dispose -- must come through here under _writeLock.
    private void Drain(bool flush)
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
                        $"FileLogger: {_droppedToWriteFault} write fault(s); one or more lines may have been lost at each.");
                    _droppedToWriteFault = 0;
                }

                if (flush)
                {
                    _logFile.Flush();
                    _unflushed = false;
                }
                else if (!_unflushed)
                {
                    _unflushed = true;
                    _unflushedSinceTick = Environment.TickCount;
                }
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
            if (!_queue.IsEmpty) Drain(flush: false);
            else Thread.Sleep(50);
            FlushIfDue();
        }
    }

    // Timed flush for the plain lines the writer thread has written but not flushed.
    private void FlushIfDue()
    {
        lock (_writeLock)
        {
            if (!_unflushed || _logFile == null) return;
            if (unchecked(Environment.TickCount - (int)_unflushedSinceTick) < FlushIntervalMs) return;
            try { _logFile.Flush(); _unflushed = false; }
            catch { _droppedToWriteFault++; _unflushed = false; }
        }
    }

    public void Dispose()
    {
        try { AppDomain.CurrentDomain.ProcessExit -= _processExitHandler; } catch { /* best effort */ }
        _stopping = true;
        // Join can time out, leaving the writer thread live -- so the final drain and the disposal
        // of _logFile both go through the same lock the writer uses.
        _writerThread.Join(TimeSpan.FromSeconds(5));
        Drain(flush: true);
        lock (_writeLock)
        {
            _logFile?.Dispose();
            _logFile = null;
        }
    }
}
