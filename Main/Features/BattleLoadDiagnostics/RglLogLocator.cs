using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;

namespace TAOM.Features.BattleLoadDiagnostics;

internal readonly struct RglCandidate
{
    public RglCandidate(string fullPath, DateTime lastWriteUtc, int rootIndex)
    {
        FullPath = fullPath;
        LastWriteUtc = lastWriteUtc;
        RootIndex = rootIndex;
    }

    public string FullPath { get; }
    public DateTime LastWriteUtc { get; }
    public int RootIndex { get; }
}

/// <summary>
/// Finds this process's full engine log. That the file name carries the process id is UNVERIFIED on
/// v1.4.8, hence the fallback to the newest log written since the process started. The errors log
/// (<c>rgl_log_errors_*</c>) holds no compile lines, so it is never chosen.
/// </summary>
internal static class RglLogLocator
{
    private const string ErrorsPrefix = "rgl_log_errors_";

    public static string? Choose(int pid, DateTime processStartUtc, IEnumerable<RglCandidate> candidates)
    {
        string exact = "rgl_log_" + pid + ".txt";
        RglCandidate? best = null;
        RglCandidate? newest = null;
        foreach (var c in candidates)
        {
            string name = Path.GetFileName(c.FullPath);
            if (string.Equals(name, exact, StringComparison.OrdinalIgnoreCase))
            {
                if (best == null || c.RootIndex < best.Value.RootIndex) best = c;
                continue;
            }

            if (name.StartsWith(ErrorsPrefix, StringComparison.OrdinalIgnoreCase)) continue;
            if (c.LastWriteUtc < processStartUtc) continue;
            if (newest == null || c.LastWriteUtc > newest.Value.LastWriteUtc) newest = c;
        }

        return best?.FullPath ?? newest?.FullPath;
    }

    public static string? ResolveForCurrentProcess()
    {
        try
        {
            var candidates = new List<RglCandidate>();
            var roots = new[]
            {
                Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
                Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
            };
            for (int i = 0; i < roots.Length; i++)
            {
                string logs = Path.Combine(roots[i], "Mount and Blade II Bannerlord", "logs");
                if (!Directory.Exists(logs)) continue;
                foreach (string file in Directory.GetFiles(logs, "rgl_log_*.txt"))
                    candidates.Add(new RglCandidate(file, File.GetLastWriteTimeUtc(file), i));
            }

            using var process = Process.GetCurrentProcess();
            return Choose(process.Id, process.StartTime.ToUniversalTime(), candidates);
        }
        catch
        {
            return null;
        }
    }
}
