using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TAOM.Features.CrashReport.Domain;

namespace TAOM.Features.CrashReport.Collectors;

public sealed class LogTailCollector
{
    private readonly Func<string?> _taomLogPathProvider;
    private const int DefaultTailLines = 500;

    public LogTailCollector(Func<string?> taomLogPathProvider)
    {
        _taomLogPathProvider = taomLogPathProvider;
    }

    public LogTailSnapshot Collect(int tailLines = DefaultTailLines)
    {
        string? taomPath = null;
        try { taomPath = _taomLogPathProvider(); } catch { }
        var taomTail = ReadTail(taomPath, tailLines);

        string? rglPath = FindLatestRglLog();
        var rglTail = ReadTail(rglPath, tailLines);

        return new LogTailSnapshot(taomPath, taomTail, rglPath, rglTail);
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
            var docs = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            var dir = Path.Combine(docs, "Mount and Blade II Bannerlord", "logs");
            if (!Directory.Exists(dir)) return null;
            return new DirectoryInfo(dir)
                .EnumerateFiles("rgl_log_*.txt")
                .OrderByDescending(f => f.LastWriteTimeUtc)
                .FirstOrDefault()?.FullName;
        }
        catch { return null; }
    }
}
