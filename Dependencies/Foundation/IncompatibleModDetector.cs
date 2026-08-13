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
/// BetaDeps parity (DR3 Phase 4 — 2026-05-25). Re-implements the read-only-detection subset
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
        DiagLog.Log(Tag, "RunEarlyPhase: entered");
        if (System.Threading.Interlocked.Exchange(ref _earlyPhaseRan, 1) != 0)
        {
            DiagLog.Log(Tag, "RunEarlyPhase: already ran, returning");
            return;
        }

        try
        {
            var moduleDir = RuntimeLog.ModuleDir;
            DiagLog.Log(Tag, $"RunEarlyPhase: moduleDir='{moduleDir}'");
            if (string.IsNullOrEmpty(moduleDir))
            {
                DiagLog.Log(Tag, "RunEarlyPhase: module dir unknown; skipping crash-loop detection");
                return;
            }

            var markerPath = Path.Combine(moduleDir, LaunchMarkerName);
            var lastGoodPath = Path.Combine(moduleDir, LastGoodModlistName);
            DiagLog.Log(Tag, $"RunEarlyPhase: markerPath='{markerPath}'");

            // Detect previous-session crash-loop: marker present from last launch means
            // the last session's MarkSessionLaunchSuccessful() was never called.
            var previousCrashLoop = File.Exists(markerPath);
            DiagLog.Log(Tag, $"RunEarlyPhase: previousCrashLoop={previousCrashLoop} (marker exists)");
            if (previousCrashLoop)
            {
                DiagLog.Log(Tag, "RunEarlyPhase: previous session never reached main menu — analysing culprit");
                AnalyzeCulprit(lastGoodPath);
            }

            // Write fresh marker for this session.
            DiagLog.Log(Tag, "RunEarlyPhase: writing fresh session-launching.marker");
            File.WriteAllText(markerPath,
                $"launch started {DateTime.Now:yyyy-MM-dd HH:mm:ss}{Environment.NewLine}",
                Encoding.UTF8);
            DiagLog.Log(Tag, "RunEarlyPhase: COMPLETE — marker written");
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
        DiagLog.Log(Tag, "MarkSessionLaunchSuccessful: entered");
        try
        {
            var moduleDir = RuntimeLog.ModuleDir;
            if (string.IsNullOrEmpty(moduleDir))
            {
                DiagLog.Log(Tag, "MarkSessionLaunchSuccessful: moduleDir empty; skipping");
                return;
            }

            var markerPath = Path.Combine(moduleDir, LaunchMarkerName);
            var lastGoodPath = Path.Combine(moduleDir, LastGoodModlistName);

            if (File.Exists(markerPath))
            {
                File.Delete(markerPath);
                DiagLog.Log(Tag, "MarkSessionLaunchSuccessful: launch marker deleted (crash-loop ok)");
            }

            var modlist = ReadCurrentModlist();
            DiagLog.Log(Tag, $"MarkSessionLaunchSuccessful: ReadCurrentModlist returned {modlist.Count} entries");
            if (modlist.Count > 0)
            {
                File.WriteAllLines(lastGoodPath,
                    new[] { $"# Last known-good modlist (reached main menu {DateTime.Now:yyyy-MM-dd HH:mm:ss})" }
                        .Concat(modlist),
                    Encoding.UTF8);
                DiagLog.Log(Tag, $"MarkSessionLaunchSuccessful: COMPLETE — saved {modlist.Count}-mod last-good snapshot");
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
    /// Returns the set of currently-ACTIVE module IDs. Codex A1 MED fix 2026-05-27:
    /// previously this scanned every installed `Modules/X/` directory, returning ALL
    /// installed modules — not just the enabled ones. Diff comments said "enabled"
    /// but the implementation returned "installed". Now tries
    /// <c>TaleWorlds.ModuleManager.ModuleHelper.GetActiveModules()</c> first (active /
    /// enabled at the launcher level — exactly the right granularity for crash-loop
    /// culprit identification); falls back to the directory scan only when reflection
    /// fails (very early init, before ModuleHelper has populated its internal state).
    /// </summary>
    private static List<string> ReadCurrentModlist()
    {
        // Strategy 1: TaleWorlds.ModuleManager.ModuleHelper.GetActiveModules()
        var active = TryReadActiveModuleIdsViaReflection();
        if (active.Count > 0)
        {
            DiagLog.Log(Tag, $"ReadCurrentModlist: read {active.Count} active modules via ModuleHelper.GetActiveModules");
            return active;
        }

        // Strategy 2: folder scan (returns INSTALLED modules — used only if active
        // module reflection failed, e.g., during very early init).
        DiagLog.Log(Tag, "ReadCurrentModlist: GetActiveModules unavailable, falling back to directory scan");
        return ReadInstalledModuleIdsFromFolders();
    }

    /// <summary>
    /// Reads the launcher's ACTIVE module ids via
    /// <c>TaleWorlds.ModuleManager.ModuleHelper.GetActiveModules()</c>. Returns an empty list when
    /// reflection fails (very early init, before ModuleHelper has populated its internal state) —
    /// callers must treat empty as "unknown", not as "no modules".
    ///
    /// Internal rather than private since 2026-07-31: <see cref="CoopPresence"/> needs the same
    /// active-module read and must not carry a second copy of this reflection.
    /// </summary>
    internal static List<string> TryReadActiveModuleIdsViaReflection()
    {
        var result = new List<string>();
        try
        {
            var helperType = ReflectionUtils.FindTypeAcrossLoadedAssemblies(
                "TaleWorlds.ModuleManager.ModuleHelper");
            if (helperType == null) return result;

            var getActive = helperType.GetMethod("GetActiveModules",
                System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
            if (getActive == null) return result;

            var modules = getActive.Invoke(null, null) as System.Collections.IEnumerable;
            if (modules == null) return result;

            foreach (var moduleInfo in modules)
            {
                if (moduleInfo == null) continue;
                try
                {
                    var idProp = moduleInfo.GetType().GetProperty("Id");
                    if (idProp?.GetValue(moduleInfo) is string id && !string.IsNullOrWhiteSpace(id))
                        result.Add(id);
                }
                catch { }
            }
        }
        catch (Exception ex)
        {
            DiagLog.LogCaught(Tag, "TryReadActiveModuleIdsViaReflection", ex);
        }
        return result;
    }

    private static List<string> ReadInstalledModuleIdsFromFolders()
    {
        var result = new List<string>();
        try
        {
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

                    // Strip XML comments before matching the Id — otherwise stubs
                    // with comment blocks that mention `<Id value="X"/>` get
                    // mis-recorded. Caught 2026-05-27: Bannerlord.Harmony stub's
                    // comment mentions `<Id value="TAOM.Dependencies"/>` and was
                    // recorded as TAOM.Dependencies (duplicate) instead of
                    // Bannerlord.Harmony.
                    var stripped = System.Text.RegularExpressions.Regex.Replace(
                        text, @"<!--[\s\S]*?-->", string.Empty);
                    var idMatch = System.Text.RegularExpressions.Regex.Match(
                        stripped, @"<Id\s+value\s*=\s*""([^""]+)""\s*/>");
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
