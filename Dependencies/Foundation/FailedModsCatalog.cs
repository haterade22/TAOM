using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace TAOM.Dependencies.Foundation;

/// <summary>
/// Appends shielded-exception records to <c>&lt;module&gt;/failed-mods-catalog.txt</c>
/// for user diagnosis. Dedupes within a session by (culprit, exception_type, owner)
/// tuple — log shows each distinct failure once per session, not once per occurrence.
///
/// BetaDeps parity (DR3 Phase 4 — 2026-05-25). Re-implements BetaDeps.Foundation.FailedModsCatalog.
/// </summary>
public static class FailedModsCatalog
{
    private const string Tag = "FailedModsCatalog";
    private const string CatalogFileName = "failed-mods-catalog.txt";

    private static readonly object _lock = new();
    private static readonly HashSet<string> _sessionSeen = new(StringComparer.OrdinalIgnoreCase);

    public static string ResolvePath()
    {
        try
        {
            var dir = RuntimeLog.ModuleDir;
            return string.IsNullOrEmpty(dir) ? string.Empty : Path.Combine(dir, CatalogFileName);
        }
        catch { return string.Empty; }
    }

    public static void Append(FailureRecord rec)
    {
        if (rec == null || string.IsNullOrEmpty(rec.CulpritAssembly)) return;

        var dedupeKey = $"{rec.CulpritAssembly}|{rec.ExceptionType}|{rec.OwnerType}.{rec.OwnerMethod}";
        lock (_lock)
        {
            if (_sessionSeen.Contains(dedupeKey)) return;
            _sessionSeen.Add(dedupeKey);
        }

        try
        {
            var path = ResolvePath();
            if (string.IsNullOrEmpty(path)) return;

            if (!File.Exists(path))
            {
                File.AppendAllText(path,
                    "# TAOM.Dependencies failed-mods catalog — one line per (mod, exception type) seen by a shield." + Environment.NewLine +
                    "# Format: <UTC timestamp> | <CULPRIT> | <category> | <ExceptionType> | <owner method> | <message head>" + Environment.NewLine,
                    Encoding.UTF8);
            }

            var line = string.Format("{0} | {1,-32} | {2,-12} | {3,-40} | {4} | {5}{6}",
                rec.When.ToString("yyyy-MM-dd HH:mm:ss"),
                Clip(rec.CulpritAssembly, 32),
                Clip(rec.Category, 12),
                Clip(rec.ExceptionType, 40),
                Clip(rec.OwnerType + "." + rec.OwnerMethod, 80),
                Clip(rec.Message?.Replace('\n', ' ').Replace('\r', ' ') ?? string.Empty, 200),
                Environment.NewLine);
            File.AppendAllText(path, line, Encoding.UTF8);
        }
        catch (Exception ex)
        {
            DiagLog.LogCaught(Tag, "Append", ex);
        }
    }

    private static string Clip(string? s, int max)
        => string.IsNullOrEmpty(s) ? string.Empty
         : (s!.Length <= max ? s : s.Substring(0, max - 1) + "…");
}
