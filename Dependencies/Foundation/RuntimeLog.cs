using System;
using System.IO;
using System.Reflection;

namespace TAOM.Dependencies.Foundation;

/// <summary>
/// Resolves the canonical runtime-log path for TAOM.Dependencies. Used by DiagLog,
/// PatchShield, SaveShield, FailedModsCatalog, and IncompatibleModDetector for their
/// disk artifacts (diag.log, failed-mods-catalog.txt, last-good-modlist.txt,
/// *-disabled.flag files, etc.).
///
/// BetaDeps parity (DR3 Phase 4 — 2026-05-25). Re-implements the design of
/// BetaDeps.Foundation.RuntimeLog. Path resolution walks back from the deployed
/// `TAOM.Dependencies.dll` location to `&lt;game&gt;/Modules/TAOM.Dependencies/`.
/// </summary>
public static class RuntimeLog
{
    private static string? _path;
    private static string? _moduleDir;

    /// <summary>
    /// Absolute path to the runtime log file
    /// (<c>&lt;game&gt;/Modules/TAOM.Dependencies/diag.log</c>). Returns empty string
    /// if the location can't be resolved.
    /// </summary>
    public static string Path => _path ??= ResolvePath();

    /// <summary>
    /// Absolute path to the TAOM.Dependencies module directory
    /// (<c>&lt;game&gt;/Modules/TAOM.Dependencies/</c>). Used by shield disable-flag
    /// files. Returns empty string if the location can't be resolved.
    /// </summary>
    public static string ModuleDir => _moduleDir ??= ResolveModuleDir();

    private static string ResolveModuleDir()
    {
        try
        {
            // TAOM.Dependencies.dll lives at:
            //   <game>/Modules/TAOM.Dependencies/bin/Win64_Shipping_Client/TAOM.Dependencies.dll
            // Walk back 3 directories to reach the module root.
            var dllPath = typeof(RuntimeLog).Assembly.Location;
            if (string.IsNullOrEmpty(dllPath)) return string.Empty;

            var binDir = System.IO.Path.GetDirectoryName(dllPath);                // Win64_Shipping_Client
            var moduleBinDir = System.IO.Path.GetDirectoryName(binDir);           // bin
            var moduleDir = System.IO.Path.GetDirectoryName(moduleBinDir);        // TAOM.Dependencies

            return moduleDir ?? string.Empty;
        }
        catch
        {
            return string.Empty;
        }
    }

    private static string ResolvePath()
    {
        var dir = ResolveModuleDir();
        return string.IsNullOrEmpty(dir) ? string.Empty : System.IO.Path.Combine(dir, "diag.log");
    }
}
