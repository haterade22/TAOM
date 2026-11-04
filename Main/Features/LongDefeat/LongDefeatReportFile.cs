using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace TAOM.Features.LongDefeat;

/// <summary>
/// The JSON report file of one run, beside the live log: <c>long_defeat_report_&lt;log stamp&gt;_&lt;mission key&gt;.json</c>. Written through a
/// temp file so a crash never leaves half a document. The newest <see cref="Retained"/> reports stay; the logger prunes only
/// its own <c>taom_debug_*.log</c>.
/// </summary>
internal sealed class LongDefeatReportFile
{
    internal const int Retained = 20;

    private static readonly Regex LogStamp = new(@"^taom_debug_(\d{4}-\d{2}-\d{2}_\d{2}-\d{2}-\d{2})\.log$", RegexOptions.Compiled);

    private readonly string _directory;
    private readonly string _stamp;

    private LongDefeatReportFile(string directory, string stamp)
    {
        _directory = directory;
        _stamp = stamp;
    }

    /// <summary>Null when the logger writes nowhere (a test double): no file is written then.</summary>
    internal static LongDefeatReportFile? For(string? logFilePath)
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
            return new LongDefeatReportFile(directory!, stamp);
        }
        catch (Exception)
        {
            return null;
        }
    }

    /// <summary>The mission key is "ld" and a count; any character a file name cannot hold is replaced, so a key never leaves the folder.</summary>
    internal string PathFor(string missionKey) => Path.Combine(_directory, "long_defeat_report_" + _stamp + "_" + SafeKey(missionKey) + ".json");

    private static string SafeKey(string key) =>
        new string(key.Select(c => Array.IndexOf(Path.GetInvalidFileNameChars(), c) >= 0 || c == '.' ? '_' : c).ToArray());

    /// <summary>Writes the document through <see cref="LongDefeatFiles.WriteAtomic"/>, then prunes old reports. Never throws.</summary>
    internal bool TryWrite(string missionKey, string json, out string? error)
    {
        try
        {
            LongDefeatFiles.WriteAtomic(PathFor(missionKey), json);
            error = null;
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
            var stale = new DirectoryInfo(_directory).GetFiles("long_defeat_report_*.json")
                .OrderByDescending(f => f.LastWriteTimeUtc)
                .ThenByDescending(f => f.Name, StringComparer.Ordinal)
                .Skip(Retained);
            foreach (var file in stale)
                file.Delete();
        }
        catch (Exception)
        {
            // swallow-ok: pruning is housekeeping; a locked old report must not fail the write that just succeeded.
        }
    }
}
