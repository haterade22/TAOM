using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace TAOM.Features.CrashReport.BugReport;

// One file for the bug report zip: its name inside the zip and where it is read from.
public sealed record BugReportFile(string EntryName, string SourcePath);

// The files a player's bug report adds to the crash bundle's contents (which already carry this session's taom_debug
// log and newest rgl_log): the previous session's taom_debug log, the rgl_log before the newest, and the engine's crash
// folder from the previous session. Notes go to the manifest and name files only, never a folder, because a folder path
// can carry the Windows user name.
public sealed class BugReportSources
{
    public BugReportSources(IReadOnlyList<BugReportFile> files, IReadOnlyList<string> notes)
    {
        Files = files;
        Notes = notes;
    }

    public IReadOnlyList<BugReportFile> Files { get; }
    public IReadOnlyList<string> Notes { get; }

    // The engine's own folders: %ProgramData% by default, Documents on older or redirected installs (LogTailCollector).
    public static IEnumerable<string> DefaultEngineDirs() => new[]
    {
        Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
        Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
    }.Select(r => Path.Combine(r, "Mount and Blade II Bannerlord"));

    public static BugReportSources Collect(string logsDir, string? currentLogPath, IEnumerable<string> engineDirs,
        long maxCrashFileBytes = BugReportFiles.MaxCrashFileBytes)
    {
        var files = new List<BugReportFile>();
        var notes = new List<string>();
        var dirs = engineDirs.Where(SafeDirectoryExists).ToList();

        var previousLog = PreviousTaomLog(logsDir, currentLogPath);
        DateTime? previousStart = null;
        if (previousLog == null)
            notes.Add("previous session: no earlier taom_debug log");
        else
        {
            var name = Path.GetFileName(previousLog);
            previousStart = BugReportFiles.SessionStartUtc(name);
            files.Add(new BugReportFile("taom_debug_previous.log", previousLog));
            var unclean = BugReportFiles.EndedUnexpectedly(ReadTail(previousLog));
            notes.Add($"previous session: {name}, clean exit: {(unclean ? "no" : "yes")}");
        }

        var previousRgl = Newest(dirs.SelectMany(d => FilesIn(Path.Combine(d, "logs")))
            .Where(f => BugReportFiles.IsMainRglLog(f.Name))).Skip(1).FirstOrDefault();
        if (previousRgl != null)
            files.Add(new BugReportFile("rgl_log_previous.txt", previousRgl.FullName));

        var crash = Newest(dirs.SelectMany(d => FoldersIn(Path.Combine(d, "crashes"))))
            .FirstOrDefault(f => BugReportFiles.IsFromSession(f.LastWriteTimeUtc, previousStart));
        if (crash == null)
            notes.Add("engine crash folder: none from the previous session");
        else
        {
            notes.Add($"engine crash folder: {crash.Name}");
            foreach (var f in FilesIn(crash.FullName))
            {
                var entry = "crash/" + f.Name;
                if (f.Length <= maxCrashFileBytes)
                    files.Add(new BugReportFile(entry, f.FullName));
                else
                    notes.Add($"{entry} not included: {f.Length / (1024 * 1024)} MB is over the " +
                              $"{maxCrashFileBytes / (1024 * 1024)} MB limit; it stays in the game's crashes folder");
            }
        }

        return new BugReportSources(files, notes);
    }

    // The newest taom_debug log other than this session's own.
    public static string? PreviousTaomLog(string logsDir, string? currentLogPath)
    {
        var current = string.IsNullOrEmpty(currentLogPath) ? null : Path.GetFileName(currentLogPath);
        return Newest(FilesIn(logsDir).Where(f => BugReportFiles.IsTaomLog(f.Name)
                && !string.Equals(f.Name, current, StringComparison.OrdinalIgnoreCase)))
            .FirstOrDefault()?.FullName;
    }

    // The end of a log, enough to hold the unload line and what other modules write after it.
    public static string? ReadTail(string path, int maxBytes = 64 * 1024)
    {
        try
        {
            using var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            var start = Math.Max(0, fs.Length - maxBytes);
            fs.Seek(start, SeekOrigin.Begin);
            var buffer = new byte[fs.Length - start];
            var read = fs.Read(buffer, 0, buffer.Length);
            return Encoding.UTF8.GetString(buffer, 0, read);
        }
        catch (Exception) { return null; } // swallow-ok: a log that cannot be read is reported as absent
    }

    private static IEnumerable<T> Newest<T>(IEnumerable<T> items) where T : FileSystemInfo =>
        items.OrderByDescending(i => i.LastWriteTimeUtc);

    private static IEnumerable<FileInfo> FilesIn(string dir)
    {
        try { return Directory.Exists(dir) ? new DirectoryInfo(dir).GetFiles() : Array.Empty<FileInfo>(); }
        catch (Exception) { return Array.Empty<FileInfo>(); } // swallow-ok: a folder that cannot be listed adds nothing
    }

    private static IEnumerable<DirectoryInfo> FoldersIn(string dir)
    {
        try { return Directory.Exists(dir) ? new DirectoryInfo(dir).GetDirectories() : Array.Empty<DirectoryInfo>(); }
        catch (Exception) { return Array.Empty<DirectoryInfo>(); } // swallow-ok: a folder that cannot be listed adds nothing
    }

    private static bool SafeDirectoryExists(string dir)
    {
        try { return Directory.Exists(dir); }
        catch (Exception) { return false; } // swallow-ok: an unusable path is skipped
    }
}
