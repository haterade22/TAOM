using System;
using System.Linq;
using System.Reflection;
using System.Threading;
using HarmonyLib;

namespace TAOM.Dependencies.Foundation;

/// <summary>
/// Wraps <see cref="Assembly.GetTypes"/> + <see cref="Assembly.GetExportedTypes"/>
/// with a Finalizer that catches <see cref="ReflectionTypeLoadException"/> and
/// returns the partial-type list instead of letting the exception propagate.
///
/// When a third-party mod references a removed vanilla API, calling
/// <see cref="Assembly.GetTypes"/> on its DLL throws ReflectionTypeLoadException
/// — but with a partial list of types that DID load successfully via
/// <see cref="ReflectionTypeLoadException.Types"/>. Bannerlord's launcher and
/// many tools call GetTypes() over every loaded assembly; without this shim,
/// one broken mod can cause cascade failures.
///
/// BetaDeps parity (DR3 Phase 4 — 2026-05-25). Ports
/// BetaDeps.Foundation.CollectAssemblyTypesShim.
/// </summary>
public static class CollectAssemblyTypesShim
{
    private const string Tag = "CollectAssemblyTypesShim";
    private const string HarmonyId = "TAOM.Dependencies.Foundation.CollectAssemblyTypesShim";

    private static int _installed;

    public static void Install()
    {
        DiagLog.Log(Tag, "Install: entered");
        if (Interlocked.CompareExchange(ref _installed, 1, 0) != 0)
        {
            DiagLog.Log(Tag, "Install: already installed, returning");
            return;
        }

        try
        {
            DiagLog.Log(Tag, "Install: constructing Harmony instance");
            var harmony = new Harmony(HarmonyId);
            DiagLog.Log(Tag, "Install: Harmony constructed");

            var getTypes = typeof(Assembly).GetMethod(nameof(Assembly.GetTypes),
                BindingFlags.Instance | BindingFlags.Public);
            var getExportedTypes = typeof(Assembly).GetMethod(nameof(Assembly.GetExportedTypes),
                BindingFlags.Instance | BindingFlags.Public);
            DiagLog.Log(Tag, $"Install: getTypes={getTypes != null}, getExportedTypes={getExportedTypes != null}");

            var finalizer = typeof(CollectAssemblyTypesShim).GetMethod(
                nameof(GetTypesFinalizer),
                BindingFlags.Static | BindingFlags.NonPublic);
            if (finalizer == null)
            {
                DiagLog.Log(Tag, "Install: could not resolve GetTypesFinalizer; aborting install");
                return;
            }
            DiagLog.Log(Tag, "Install: finalizer resolved");

            int installed = 0;
            if (getTypes != null)
            {
                try
                {
                    DiagLog.Log(Tag, "Install: patching Assembly.GetTypes");
                    harmony.Patch(getTypes, finalizer: new HarmonyMethod(finalizer));
                    installed++;
                    DiagLog.Log(Tag, "Install: Assembly.GetTypes patched");
                }
                catch (Exception ex) { DiagLog.LogCaught(Tag, "Patch GetTypes", ex); }
            }
            if (getExportedTypes != null)
            {
                try
                {
                    DiagLog.Log(Tag, "Install: patching Assembly.GetExportedTypes");
                    harmony.Patch(getExportedTypes, finalizer: new HarmonyMethod(finalizer));
                    installed++;
                    DiagLog.Log(Tag, "Install: Assembly.GetExportedTypes patched");
                }
                catch (Exception ex) { DiagLog.LogCaught(Tag, "Patch GetExportedTypes", ex); }
            }
            DiagLog.Log(Tag, $"Install: COMPLETE — installed {installed} Assembly.GetTypes*-Finalizer(s)");
        }
        catch (Exception ex)
        {
            DiagLog.LogCaught(Tag, "Install", ex);
        }
    }

    /// <summary>
    /// Finalizer that catches ReflectionTypeLoadException, logs the failure, and
    /// returns the partial type list (which Harmony exposes by setting __result).
    ///
    /// Note: a Finalizer can modify __result. Here we set it to the non-null types
    /// from ex.Types, then return null to swallow the exception.
    /// </summary>
    private static Exception? GetTypesFinalizer(Assembly __instance, ref Type[] __result, Exception __exception)
    {
        if (__exception == null) return null;
        if (__exception is ReflectionTypeLoadException rtle)
        {
            try
            {
                var partial = rtle.Types?.Where(t => t != null).ToArray() ?? Array.Empty<Type>();
                __result = partial;
                var asm = __instance?.GetName().Name ?? "(?)";
                DiagLog.Log(Tag,
                    $"swallowed ReflectionTypeLoadException for {asm}: returned partial " +
                    $"{partial.Length} of {rtle.Types?.Length ?? 0} types " +
                    $"(LoaderExceptions: {rtle.LoaderExceptions?.Length ?? 0})");
                return null;  // swallow
            }
            catch (Exception fxEx)
            {
                DiagLog.LogCaught(Tag, "GetTypesFinalizer/partial-extract", fxEx);
                return __exception;
            }
        }
        return __exception;
    }
}
