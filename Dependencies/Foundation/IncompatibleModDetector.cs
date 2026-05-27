using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace TAOM.Dependencies.Foundation;

/// <summary>
/// Crash-loop detector. Maintains a <c>session-launching.marker</c> file that's written
/// at SubModule construction and deleted on successful main-menu reach (via the
/// <see cref="MarkSessionLaunchSuccessful"/> hook). If the marker still exists on the
/// NEXT launch's <see cref="RunEarlyPhase"/>, the previous session crashed before
/// reaching main menu — likely because a newly-enabled mod is incompatible.
///
/// Diffs the current modlist against <c>last-good-modlist.txt</c> (the modlist that
/// last reached main menu) to identify which mod is the likely culprit. Logs to
/// DiagLog. Does NOT modify launcher data (BetaDeps's <c>auto-disable-enabled.flag</c>
/// gating of XML mutation is deferred to a future revision — TAOM Phase 4 ships
/// detection only, not auto-disable).
///
/// BetaDeps parity (DR3 Phase 4 — 2026-05-25). Ports the read-only-detection subset
/// of BetaDeps.Foundation.IncompatibleModDetector. The XML auto-modify path is
/// intentionally omitted; we'll revisit if TAOM users hit recurring crash-loops.
/// </summary>
public static class IncompatibleModDetector
{
    private const string Tag = "IncompatibleModDetector";
    private const string LaunchMarkerName = "session-launching.marker";
    private const string LastGoodModlistName = "last-good-modlist.txt";

    private static int _earlyPhaseRan;

    /// <summary>
    /// Run at SubModule construction time (idempotent across stub ctors).
    /// Checks whether the previous session's launch marker still exists; if so,
    /// the previous launch crashed pre-menu. Diffs modlist to identify culprit.
    /// Writes a fresh launch marker for THIS session.
    /// </summary>
    public static void RunEarlyPhase()
    {
        if (System.Threading.Interlocked.Exchange(ref _earlyPhaseRan, 1) != 0) return;

        try
        {
            var moduleDir = RuntimeLog.ModuleDir;
            if (string.IsNullOrEmpty(moduleDir))
            {
                DiagLog.Log(Tag, "RunEarlyPhase: module dir unknown; skipping crash-loop detection");
                return;
            }

            var markerPath = Path.Combine(moduleDir, LaunchMarkerName);
            var lastGoodPath = Path.Combine(moduleDir, LastGoodModlistName);

            // Detect previous-session crash-loop: marker present from last launch means
            // the last session's MarkSessionLaunchSuccessful() was never called.
            var previousCrashLoop = File.Exists(markerPath);
            if (previousCrashLoop)
            {
                DiagLog.Log(Tag, "RunEarlyPhase: previous session never reached main menu (launch marker present)");
                AnalyzeCulprit(lastGoodPath);
            }

            // Write fresh marker for this session.
            File.WriteAllText(markerPath,
                $"launch started {DateTime.Now:yyyy-MM-dd HH:mm:ss}{Environment.NewLine}",
                Encoding.UTF8);
        }
        catch (Exception ex)
        {
            DiagLog.LogCaught(Tag, "RunEarlyPhase", ex);
        }
    }

    /// <summary>
    /// Call from a known main-menu reach point (e.g., MainMenuScreen.OnInitialize via
    /// Harmony patch). Deletes the launch marker AND snapshots the current modlist
    /// to <c>last-good-modlist.txt</c> for next-launch diff comparison.
    /// </summary>
    public static void MarkSessionLaunchSuccessful()
    {
        try
        {
            var moduleDir = RuntimeLog.ModuleDir;
            if (string.IsNullOrEmpty(moduleDir)) return;

            var markerPath = Path.Combine(moduleDir, LaunchMarkerName);
            var lastGoodPath = Path.Combine(moduleDir, LastGoodModlistName);

            if (File.Exists(markerPath)) File.Delete(markerPath);

            var modlist = ReadCurrentModlist();
            if (modlist.Count > 0)
            {
                File.WriteAllLines(lastGoodPath,
                    new[] { $"# Last known-good modlist (reached main menu {DateTime.Now:yyyy-MM-dd HH:mm:ss})" }
                        .Concat(modlist),
                    Encoding.UTF8);
                DiagLog.Log(Tag, $"MarkSessionLaunchSuccessful: saved {modlist.Count}-mod last-good snapshot");
            }
        }
        catch (Exception ex)
        {
            DiagLog.LogCaught(Tag, "MarkSessionLaunchSuccessful", ex);
        }
    }

    private static void AnalyzeCulprit(string lastGoodPath)
    {
        try
        {
            if (!File.Exists(lastGoodPath))
            {
                DiagLog.Log(Tag, "  AnalyzeCulprit: no last-good-modlist.txt yet — first crash on a fresh install. Skipping diff.");
                return;
            }

            var lastGood = File.ReadAllLines(lastGoodPath)
                .Select(l => l.Trim())
                .Where(l => !string.IsNullOrEmpty(l) && !l.StartsWith("#"))
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            var current = ReadCurrentModlist();
            var newlyEnabled = current.Where(m => !lastGood.Contains(m)).ToList();

            if (newlyEnabled.Count == 0)
            {
                DiagLog.Log(Tag, "  AnalyzeCulprit: no new mods since last-good. Crash may be from a Bannerlord update or save corruption.");
            }
            else
            {
                DiagLog.Log(Tag, $"  AnalyzeCulprit: {newlyEnabled.Count} mod(s) added/re-enabled since last-good — likely culprit(s):");
                foreach (var m in newlyEnabled)
                {
                    DiagLog.Log(Tag, $"    NEW: {m}");
                }
            }
        }
        catch (Exception ex)
        {
            DiagLog.LogCaught(Tag, "AnalyzeCulprit", ex);
        }
    }

    /// <summary>
    /// Returns the set of currently-loaded module IDs by querying the loaded
    /// MBSubModuleBase types. Returns empty list if reflection fails.
    /// </summary>
    private static List<string> ReadCurrentModlist()
    {
        var result = new List<string>();
        try
        {
            // Walk Modules/ directory and read each <Id> from SubModule.xml.
            // Faster than reflecting through TaleWorlds.MountAndBlade.Module.AllModules
            // and works even very early in load.
            var moduleDir = RuntimeLog.ModuleDir;
            if (string.IsNullOrEmpty(moduleDir)) return result;

            var modulesRoot = Path.GetDirectoryName(moduleDir);  // <game>/Modules
            if (string.IsNullOrEmpty(modulesRoot) || !Directory.Exists(modulesRoot)) return result;

            foreach (var dir in Directory.GetDirectories(modulesRoot))
            {
                var subModuleXml = Path.Combine(dir, "SubModule.xml");
                var moduleSubModuleXml = Path.Combine(dir, "_Module", "SubModule.xml");
                var xmlPath = File.Exists(subModuleXml) ? subModuleXml
                            : File.Exists(moduleSubModuleXml) ? moduleSubModuleXml
                            : null;
                if (xmlPath == null) continue;

                try
                {
                    var text = File.ReadAllText(xmlPath);
                    var idMatch = System.Text.RegularExpressions.Regex.Match(
                        text, @"<Id\s+value\s*=\s*""([^""]+)""\s*/>");
                    if (idMatch.Success) result.Add(idMatch.Groups[1].Value);
                }
                catch
                {
                    // Skip unreadable SubModule.xml; not our problem.
                }
            }
        }
        catch
        {
            // Best-effort.
        }
        return result;
    }
}
