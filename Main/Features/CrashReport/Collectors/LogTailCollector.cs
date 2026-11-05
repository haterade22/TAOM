using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TAOM.Features.CrashReport.Domain;

namespace TAOM.Features.CrashReport.Collectors;

public sealed class LogTailCollector
{
    private readonly Func<string?> _taomLogPathProvider;
    private readonly Func<string?>? _diagLogPathProvider;
    private const int DefaultTailLines = 500;

    // diagLogPathProvider is TAOM.Dependencies' RuntimeLog.Path in production. Injected rather than
    // called directly so this stays testable without loading the Dependencies assembly, matching how
    // the TAOM debug log path is already supplied. Optional because the path resolution can fail on
    // an odd install, and a bundle missing one log is worth far more than no bundle.
    public LogTailCollector(Func<string?> taomLogPathProvider, Func<string?>? diagLogPathProvider = null)
    {
        _taomLogPathProvider = taomLogPathProvider;
        _diagLogPathProvider = diagLogPathProvider;
    }

    public LogTailSnapshot Collect(int tailLines = DefaultTailLines)
    {
        string? taomPath = null;
        try { taomPath = _taomLogPathProvider(); } catch { }
        var taomTail = ReadTail(taomPath, tailLines);

        string? rglPath = FindLatestRglLog();
        var rglTail = ReadTail(rglPath, tailLines);

        // Same swallow as the TAOM path above: this runs while the process is already crashing, so a
        // throwing provider must cost us this one file and nothing else.
        string? diagPath = null;
        try { diagPath = _diagLogPathProvider?.Invoke(); } catch { }
        if (string.IsNullOrEmpty(diagPath)) diagPath = null;
        var diagTail = ReadTail(diagPath, tailLines);

        return new LogTailSnapshot(taomPath, taomTail, rglPath, rglTail, diagPath, diagTail);
    }

    private static IReadOnlyList<string> ReadTail(string? path, int lines)
    {
        if (string.IsNullOrEmpty(path) || !File.Exists(path)) return Array.Empty<string>();
        try
        {
            // Open with read+write share so we don't fight FileLogger's writer.
            using var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            using var sr = new StreamReader(fs);
            var all = new LinkedList<string>();
            string? line;
            while ((line = sr.ReadLine()) != null)
            {
                all.AddLast(line);
                if (all.Count > lines) all.RemoveFirst();
            }
            return all.ToList();
        }
        catch { return Array.Empty<string>(); }
    }

    private static string? FindLatestRglLog()
    {
        try
        {
            // The engine writes rgl_log_*.txt to %ProgramData% by default; older / Documents-
            // redirected (incl. OneDrive) installs use MyDocuments. Probe ProgramData first,
            // then MyDocuments, and take the newest across whichever directories exist —
            // resolving the MyDocuments-only assumption that left the bundle's rgl section empty
            // on ProgramData/OneDrive installs.
            var roots = new[]
            {
                Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
                Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
            };
            return roots
                .Select(r => Path.Combine(r, "Mount and Blade II Bannerlord", "logs"))
                .Where(Directory.Exists)
                .SelectMany(d => new DirectoryInfo(d).EnumerateFiles("rgl_log_*.txt"))
                .OrderByDescending(f => f.LastWriteTimeUtc)
                .FirstOrDefault()?.FullName;
        }
        catch { return null; }
    }
}
