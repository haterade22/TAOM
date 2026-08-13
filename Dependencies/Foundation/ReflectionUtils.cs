using System;
using System.Linq;
using System.Reflection;

namespace TAOM.Dependencies.Foundation;

/// <summary>
/// Small reflection helpers used by the defensive shields (PatchShield, SaveShield,
/// VersionProbe, SubModuleConstructionGuard). All methods are exception-safe — they
/// return null/empty/false on any failure rather than throwing.
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

    /// <summary>
    /// Tries to invoke a static method by name on a type, returning the result or null.
    /// Catches all exceptions. Used by VersionProbe for safe API-discovery calls.
    /// </summary>
    public static object? TryInvokeStatic(Type type, string methodName, params object[] args)
    {
        if (type == null || string.IsNullOrEmpty(methodName)) return null;
        try
        {
            var m = type.GetMethod(methodName, BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
            return m?.Invoke(null, args);
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Gets the simple assembly name (no version/culture/pubkey) that defines the
    /// given type. Returns empty string on failure. Used by SaveShield for culprit
    /// attribution.
    /// </summary>
    public static string GetAssemblySimpleName(Type? type)
    {
        try
        {
            return type?.Assembly.GetName().Name ?? string.Empty;
        }
        catch
        {
            return string.Empty;
        }
    }
}
