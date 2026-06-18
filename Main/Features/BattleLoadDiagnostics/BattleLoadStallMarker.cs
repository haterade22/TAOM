using System;
using System.Globalization;
using System.IO;
using TAOM.Core.Logging;
using TAOM.Features.BattleLoadDiagnostics.Domain;

namespace TAOM.Features.BattleLoadDiagnostics;

// File-backed implementation of IBattleLoadStallMarker. The marker lives in the same
// "Logs/" directory FileLogger and the crash bundle write to, so a player who sees the
// next-session notice finds the marker, the taom_debug log, AND any bundle in one folder.
//
// All file I/O is best-effort and swallowed — a diagnostic must never break a battle load
// or the main menu. Touched only from the main thread (Mission.Initialize / OnMissionTick /
// main-menu reach), so no locking is needed; the background watchdog never touches it.
public sealed class BattleLoadStallMarker : IBattleLoadStallMarker
{
    private const string MarkerFileName = "battle-load-inflight.marker";

    private readonly IModLogger _logger;
    private readonly string _markerPath;

    public BattleLoadStallMarker(IModLogger logger)
        : this(logger, Path.Combine("Logs", MarkerFileName)) { }

    // Test seam: inject a temp marker path so the file lifecycle is unit-tested.
    // internal (not public) so DryIoc sees a single public ctor and auto-resolves it;
    // TAOM.Tests reaches this via InternalsVisibleTo (see TAOM.csproj).
    internal BattleLoadStallMarker(IModLogger logger, string markerPath)
    {
        _logger = logger;
        _markerPath = markerPath;
    }

    public void MarkInflight(string sceneName)
    {
        try
        {
            var dir = Path.GetDirectoryName(_markerPath);
            if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
            // Store the log path ABSOLUTE. FileLogger.LogFilePath is cwd-relative ("Logs\\..."),
            // and it flows into the next-session notice's "Open log folder" button, which hands
            // it to explorer.exe — a separate process (UseShellExecute) that does NOT resolve a
            // relative path against the game's cwd. Resolve here, in the hung session whose cwd
            // is the game dir, so the marker + notice + button all point at the real file.
            File.WriteAllText(_markerPath, Format(sceneName, DateTime.UtcNow, AbsoluteLogPath()));
        }
        catch { /* a diagnostic must never break the load */ }
    }

    private string? AbsoluteLogPath()
    {
        var p = _logger?.LogFilePath;
        if (string.IsNullOrEmpty(p)) return p;
        try { return Path.GetFullPath(p); } catch { return p; }
    }

    public void ClearInflight()
    {
        try { if (File.Exists(_markerPath)) File.Delete(_markerPath); }
        catch { /* best-effort */ }
    }

    public StallMarkerInfo? TryConsumeStaleMarker()
    {
        try
        {
            if (!File.Exists(_markerPath)) return null;
            var text = File.ReadAllText(_markerPath);
            var info = Parse(text, _markerPath);   // parse the already-read content FIRST
            // Best-effort consume. If the delete fails (read-only Logs, AV lock), surfacing the
            // stall still matters more than the at-most-once guarantee — a duplicate soft notice
            // next session beats silently dropping a real hang report.
            try { File.Delete(_markerPath); } catch { /* leave the marker; it self-clears later */ }
            return info;
        }
        catch { return null; }
    }

    // ---- pure, unit-tested ---- //
    // Three key=value lines; tolerant of a missing scene / log path on read.
    public static string Format(string sceneName, DateTime utc, string? logFilePath)
    {
        return $"scene={sceneName ?? string.Empty}\n" +
               $"utc={utc.ToString("o", CultureInfo.InvariantCulture)}\n" +
               $"log={logFilePath ?? string.Empty}\n";
    }

    public static StallMarkerInfo Parse(string text, string markerPath)
    {
        string scene = string.Empty, log = string.Empty;
        DateTime? utc = null;
        foreach (var raw in (text ?? string.Empty).Split('\n'))
        {
            var line = raw.Trim();
            if (line.StartsWith("scene=")) scene = line.Substring("scene=".Length);
            else if (line.StartsWith("log=")) log = line.Substring("log=".Length);
            else if (line.StartsWith("utc="))
            {
                if (DateTime.TryParse(line.Substring("utc=".Length), CultureInfo.InvariantCulture,
                        DateTimeStyles.RoundtripKind, out var parsed))
                    utc = parsed;
            }
        }
        return new StallMarkerInfo(scene, utc, log, markerPath);
    }
}
