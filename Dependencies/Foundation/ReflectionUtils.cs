using System;
using System.Linq;
using System.Reflection;

namespace TAOM.Dependencies.Foundation;

/// <summary>
/// Finds a type when its home assembly is unknown. Used by IncompatibleModDetector, SaveShield
/// and VersionProbe in this assembly, and by the CoopInterop feature in TAOM.dll. Exception-safe:
/// it returns null on any failure rather than throwing.
///
/// BetaDeps parity (DR3 Phase 4 — 2026-05-25). Re-implements
/// BetaDeps.Foundation.ReflectionUtils.
/// </summary>
public static class ReflectionUtils
{
    /// <summary>
    /// Finds a type across all assemblies currently loaded in the AppDomain, by
    /// fully-qualified name (ignoring assembly identity). Used when a type's home
    /// assembly is unknown or may have been redirected by AssemblyResolve.
    /// </summary>
    public static Type? FindTypeAcrossLoadedAssemblies(string fullTypeName)
    {
        if (string.IsNullOrEmpty(fullTypeName)) return null;
        try
        {
            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                try
                {
                    var t = asm.GetType(fullTypeName, throwOnError: false);
                    if (t != null) return t;
                }
                catch
                {
                    // Skip assemblies that fail GetType (e.g., from ReflectionTypeLoadException).
                }
            }
        }
        catch
        {
            // Swallow.
        }
        return null;
    }
}
