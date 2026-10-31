using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace TAOM.Features.FordsOfWar.Diagnostics;

/// <summary>
/// The JSON report file of one run, beside the live log: <c>fords_report_&lt;log stamp&gt;_run&lt;n&gt;.json</c>. Written through a
/// temp file so a crash never leaves half a document. The newest <see cref="Retained"/> reports stay; the logger prunes only
/// its own <c>taom_debug_*.log</c>.
/// </summary>
internal sealed class FordsReportFile
{
    internal const int Retained = 20;

    private static readonly Regex LogStamp = new(@"^taom_debug_(\d{4}-\d{2}-\d{2}_\d{2}-\d{2}-\d{2})\.log$", RegexOptions.Compiled);

    private readonly string _directory;
    private readonly string _stamp;
    private readonly HashSet<int> _pruned = new();

    private FordsReportFile(string directory, string stamp)
    {
        _directory = directory;
        _stamp = stamp;
    }

    /// <summary>Null when the logger writes nowhere (a test double): no file is written then.</summary>
    internal static FordsReportFile? For(string? logFilePath)
    {
        if (string.IsNullOrWhiteSpace(logFilePath))
            return null;

        try
        {
            var full = Path.GetFullPath(logFilePath!);
            var directory = Path.GetDirectoryName(full);
            if (string.IsNullOrEmpty(directory))
                return null;

            var match = LogStamp.Match(Path.GetFileName(full));
            var stamp = match.Success ? match.Groups[1].Value : DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss");
            return new FordsReportFile(directory!, stamp);
        }
        catch (Exception)
        {
            return null;
        }
    }

    internal string PathFor(int run) => Path.Combine(_directory, "fords_report_" + _stamp + "_run" + run + ".json");

    /// <summary>Writes the document (tmp, then replace or move) and prunes on the run's first write. Never throws.</summary>
    internal bool TryWrite(int run, string json, out string? error)
    {
        try
        {
            Directory.CreateDirectory(_directory);
            var path = PathFor(run);
            var tmp = path + ".tmp";
            File.WriteAllText(tmp, json);
            if (File.Exists(path))
                File.Replace(tmp, path, null);
            else
                File.Move(tmp, path);

            error = null;
            if (_pruned.Add(run))
                Prune();
            return true;
        }
        catch (Exception ex)
        {
            error = ex.GetType().Name + ": " + ex.Message;
            return false;
        }
    }

    private void Prune()
    {
        try
        {
            var stale = new DirectoryInfo(_directory).GetFiles("fords_report_*.json")
                .OrderByDescending(f => f.LastWriteTimeUtc)
                .ThenByDescending(f => f.Name, StringComparer.Ordinal)
                .Skip(Retained);
            foreach (var file in stale)
                file.Delete();
        }
        catch (Exception)
        {
            // Pruning is housekeeping; a locked old report must not fail the write that just succeeded.
        }
    }
}
